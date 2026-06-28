#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WazangGames.SteamBuildPipeline
{
    /// <summary>
    /// Executes a single end-to-end Steam build:
    ///   1. bump PlayerSettings.bundleVersion
    ///   2. Unity BuildPipeline → standalone Windows64 in Builds/&lt;env&gt;/&lt;version&gt;/
    ///   3. write app_build_&lt;env&gt;.vdf + depot_build_&lt;depot&gt;.vdf into Tools/Steam/scripts/
    ///   4. shell out to steamcmd to upload (Steam Guard handled by cached credentials)
    ///
    /// All paths are resolved from the project root so the same pipeline works from
    /// CI and the editor. Failures throw — caller (the window) catches and surfaces to UI.
    /// </summary>
    public static class SteamBuildPipelineRunner
    {
        public class Request
        {
            public SteamBuildConfig config;
            public EnvironmentSettings environment;
            public string version;
            public string buildDescription;
            public string branch;          // setlive target ("" = upload only, no set-live)
            public bool previewOnly;       // steamcmd preview mode: validates but does not upload
            public bool uploadToSteam;     // false = build only, skip steamcmd

            // Credentials supplied by the window. Username is mandatory when uploading;
            // password is optional — leave empty to reuse the cached Steam Guard token
            // produced by running 'steamcmd +login user pass' once interactively.
            public string steamUsername;
            public string steamPassword;
        }

        public class Result
        {
            public bool success;
            public string buildOutputDir;
            public string appVdfPath;
            public int steamCmdExitCode;
            public string error;
        }

        public static Result Run(Request req)
        {
            var result = new Result();
            try
            {
                ValidateRequest(req);

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string envSlug = Sanitize(req.environment.name);
                string versionSlug = Sanitize(req.version);

                string buildDir = Path.Combine(projectRoot, req.config.buildOutputRoot, envSlug, versionSlug);
                Directory.CreateDirectory(buildDir);

                // Step 1: version bump (always — even if upload is off, the exe should carry the right version).
                PlayerSettings.bundleVersion = req.version;
                AssetDatabase.SaveAssets();

                // Step 2: Unity build.
                Debug.Log($"[SteamBuild] Building '{req.environment.name}' v{req.version} → {buildDir}");
                string exePath = Path.Combine(buildDir, $"{PlayerSettings.productName}.exe");
                var buildOpts = new BuildPlayerOptions
                {
                    scenes = GetEnabledScenes(),
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(buildOpts);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    result.error = $"Unity build failed: {report.summary.result} " +
                                   $"({report.summary.totalErrors} errors)";
                    return result;
                }
                result.buildOutputDir = buildDir;

                // Step 3: VDF scripts. We put both files in the same folder so relative paths
                // inside the app_build vdf (referencing the depot script by filename) just work.
                string scriptsDir = Path.Combine(projectRoot, req.config.steamWorkingDir, "scripts");
                string logsDir = Path.Combine(projectRoot, req.config.steamWorkingDir, "BuildOutput");
                Directory.CreateDirectory(scriptsDir);
                Directory.CreateDirectory(logsDir);

                var depotVdfPaths = new System.Collections.Generic.List<(uint depotId, string vdfFile)>();
                foreach (var depot in req.environment.depots)
                {
                    string depotVdfName = $"depot_build_{depot.depotId}.vdf";
                    string depotVdfPath = Path.Combine(scriptsDir, depotVdfName);
                    File.WriteAllText(depotVdfPath, BuildDepotVdf(depot.depotId, buildDir));
                    depotVdfPaths.Add((depot.depotId, depotVdfName));
                }

                string appVdfPath = Path.Combine(scriptsDir,
                    $"app_build_{envSlug}_{req.environment.appId}.vdf");
                File.WriteAllText(appVdfPath, BuildAppVdf(
                    req.environment.appId,
                    req.buildDescription,
                    logsDir,
                    req.branch,
                    req.previewOnly,
                    depotVdfPaths));
                result.appVdfPath = appVdfPath;

                if (!req.uploadToSteam)
                {
                    Debug.Log($"[SteamBuild] Build complete (upload skipped). VDF: {appVdfPath}");
                    result.success = true;
                    return result;
                }

                // Step 4: steamcmd. If password is empty, relies on a cached Steam Guard
                // token from a prior interactive `steamcmd +login user pass` run.
                int exitCode = RunSteamCmd(
                    req.config.steamCmdPath,
                    req.steamUsername,
                    req.steamPassword,
                    appVdfPath);
                result.steamCmdExitCode = exitCode;
                if (exitCode != 0)
                {
                    result.error = $"steamcmd exited with code {exitCode}. See console for output.";
                    return result;
                }

                result.success = true;
                Debug.Log($"[SteamBuild] Upload finished. App {req.environment.appId} branch '{req.branch}'.");
                return result;
            }
            catch (Exception e)
            {
                result.error = e.Message;
                Debug.LogException(e);
                return result;
            }
        }

        private static void ValidateRequest(Request req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (req.config == null) throw new InvalidOperationException("SteamBuildConfig missing.");
            if (req.environment == null) throw new InvalidOperationException("Environment not selected.");
            if (req.environment.appId == 0)
                throw new InvalidOperationException(
                    $"AppId not set for environment '{req.environment.name}'. Fill it in SteamBuildConfig.");
            if (string.IsNullOrWhiteSpace(req.version))
                throw new InvalidOperationException("Version cannot be empty.");
            if (string.IsNullOrWhiteSpace(req.buildDescription))
                throw new InvalidOperationException("Build description (changelog) cannot be empty.");
            foreach (var d in req.environment.depots)
                if (d.depotId == 0)
                    throw new InvalidOperationException(
                        $"DepotId not set for one of the depots in '{req.environment.name}'.");

            if (req.uploadToSteam)
            {
                if (string.IsNullOrWhiteSpace(req.config.steamCmdPath) || !File.Exists(req.config.steamCmdPath))
                    throw new InvalidOperationException(
                        $"steamcmd not found at '{req.config.steamCmdPath}'.");
                if (string.IsNullOrWhiteSpace(req.steamUsername))
                    throw new InvalidOperationException(
                        "Steam username is required for upload. Fill it in the Steam Build window.");
            }
        }

        /// <summary>
        /// Opens an interactive CMD window running `steamcmd +login &lt;user&gt; +quit` so the
        /// operator can complete the Steam Guard 2FA flow once. After this, subsequent
        /// non-interactive runs work without a password.
        /// </summary>
        public static void RunInteractiveLogin(string steamCmdPath, string username)
        {
            if (!File.Exists(steamCmdPath))
                throw new InvalidOperationException($"steamcmd not found at '{steamCmdPath}'.");
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Username required.");

            string workDir = Path.GetDirectoryName(Path.GetFullPath(steamCmdPath));
            // /K keeps the window open so user can read output and re-enter password if mistyped.
            string args = $"/K \"\"{steamCmdPath}\" +login \"{username}\" +quit\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = workDir
            });
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var list = new System.Collections.Generic.List<string>(scenes.Length);
            foreach (var s in scenes)
                if (s.enabled) list.Add(s.path);
            if (list.Count == 0)
                throw new InvalidOperationException("No scenes enabled in Build Settings.");
            return list.ToArray();
        }

        private static string BuildAppVdf(
            uint appId,
            string description,
            string buildOutputDir,
            string setLiveBranch,
            bool previewOnly,
            System.Collections.Generic.List<(uint depotId, string vdfFile)> depots)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"appbuild\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"appid\" \"{appId}\"");
            sb.AppendLine($"\t\"desc\" \"{EscapeVdf(description)}\"");
            sb.AppendLine($"\t\"buildoutput\" \"{buildOutputDir}\"");
            sb.AppendLine($"\t\"contentroot\" \"\"");
            sb.AppendLine($"\t\"setlive\" \"{EscapeVdf(setLiveBranch ?? string.Empty)}\"");
            sb.AppendLine($"\t\"preview\" \"{(previewOnly ? "1" : "0")}\"");
            sb.AppendLine($"\t\"local\" \"\"");
            sb.AppendLine("\t\"depots\"");
            sb.AppendLine("\t{");
            foreach (var (depotId, vdfFile) in depots)
                sb.AppendLine($"\t\t\"{depotId}\" \"{vdfFile}\"");
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildDepotVdf(uint depotId, string contentRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"DepotBuildConfig\"");
            sb.AppendLine("{");
            sb.AppendLine($"\t\"DepotID\" \"{depotId}\"");
            sb.AppendLine($"\t\"ContentRoot\" \"{contentRoot}\"");
            sb.AppendLine("\t\"FileMapping\"");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t\"LocalPath\" \"*\"");
            sb.AppendLine("\t\t\"DepotPath\" \".\"");
            sb.AppendLine("\t\t\"recursive\" \"1\"");
            sb.AppendLine("\t}");
            // Unity ships a BurstDebugInformation folder marked "DoNotShip" — strip it.
            // Also skip pdb files (debug symbols) and IL2CPP intermediates.
            sb.AppendLine("\t\"FileExclusion\" \"*_BurstDebugInformation_DoNotShip*\"");
            sb.AppendLine("\t\"FileExclusion\" \"*_BackUpThisFolder_ButDontShipItWithYourGame*\"");
            sb.AppendLine("\t\"FileExclusion\" \"*.pdb\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static int RunSteamCmd(string steamCmdPath, string username, string password, string appVdfPath)
        {
            string runAppPath = appVdfPath.Replace('\\', '/');
            string loginArg = string.IsNullOrEmpty(password)
                ? $"+login \"{username}\""
                : $"+login \"{username}\" \"{password}\"";
            string args = $"{loginArg} +run_app_build \"{runAppPath}\" +quit";

            // Loggable variant: never echo the password into the Unity console.
            string sanitizedArgs = string.IsNullOrEmpty(password)
                ? args
                : args.Replace($"\"{password}\"", "\"***\"");

            var psi = new ProcessStartInfo
            {
                FileName = steamCmdPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Debug.Log($"[SteamBuild] $ steamcmd {sanitizedArgs}");
            using var p = Process.Start(psi);
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log("[steamcmd] " + e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Debug.LogWarning("[steamcmd] " + e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        }

        private static string EscapeVdf(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }
    }
}
#endif
