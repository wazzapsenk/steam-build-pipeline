#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WazangGames.SteamBuildPipeline
{
    /// <summary>
    /// Copies steam_appid.txt from the project root next to the built executable
    /// after every standalone build. Steamworks reads this file from the process
    /// working directory to bind the running exe to an AppId — without it, overlay
    /// hooks and shutdown deregistration misbehave when the build is launched
    /// directly (not through the Steam client).
    /// </summary>
    public class SteamAppIdBuildPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string SourceFileName = "steam_appid.txt";

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64
             && target != BuildTarget.StandaloneWindows
             && target != BuildTarget.StandaloneOSX
             && target != BuildTarget.StandaloneLinux64)
            {
                return;
            }

            // Application.dataPath points to <project>/Assets in Editor — go up one
            // level to reach the project root where steam_appid.txt lives.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(projectRoot, SourceFileName);

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[SteamAppIdBuildPostProcessor] {SourceFileName} not found at project root ({sourcePath}). Skipping copy.");
                return;
            }

            string buildExePath = report.summary.outputPath;
            string buildDir = Path.GetDirectoryName(buildExePath);
            if (string.IsNullOrEmpty(buildDir))
            {
                Debug.LogWarning("[SteamAppIdBuildPostProcessor] Could not resolve build output directory.");
                return;
            }

            string destPath = Path.Combine(buildDir, SourceFileName);
            try
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                Debug.Log($"[SteamAppIdBuildPostProcessor] Copied {SourceFileName} to {destPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SteamAppIdBuildPostProcessor] Failed to copy {SourceFileName}: {e.Message}");
            }
        }
    }
}
#endif
