#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WazangGames.SteamBuildPipeline
{
    /// <summary>
    /// Tools → Steam Build → Build Pipeline.
    ///
    /// Operator chooses environment (Demo / Playtest / Prod), version, changelog,
    /// and branch (default / beta / etc.) — pipeline builds Windows64, writes VDFs,
    /// and uploads via steamcmd.
    /// </summary>
    public class SteamBuildPipelineWindow : EditorWindow
    {
        private SteamBuildConfig _config;

        private int _envIndex;
        private int _branchIndex;
        private string _version = "1.0.0";
        private string _description = "";
        private bool _uploadToSteam = true;
        private bool _previewOnly;
        private bool _developmentBuild;
        private string _extraDefines = "";
        private Vector2 _scroll;

        // Username persists across Unity sessions (EditorPrefs); password is session-only
        // and never saved — operator types it once per Unity launch, or leaves it blank
        // to use the cached Steam Guard token from First Time Setup.
        private string _steamUsername = "";
        private string _steamPassword = "";

        private const string PrefVersion = "WazangGames.SteamBuildPipeline.Version";
        private const string PrefDescription = "WazangGames.SteamBuildPipeline.Description";
        private const string PrefEnv = "WazangGames.SteamBuildPipeline.EnvIndex";
        private const string PrefBranch = "WazangGames.SteamBuildPipeline.BranchIndex";
        private const string PrefSteamUser = "WazangGames.SteamBuildPipeline.SteamUsername";
        private const string PrefDevBuild = "WazangGames.SteamBuildPipeline.DevelopmentBuild";
        private const string PrefExtraDefines = "WazangGames.SteamBuildPipeline.ExtraDefines";

        [MenuItem("Tools/Steam Build/Build Pipeline", priority = 2000)]
        public static void Open()
        {
            var w = GetWindow<SteamBuildPipelineWindow>("Steam Build");
            w.minSize = new Vector2(480, 520);
            w.Show();
        }

        private void OnEnable()
        {
            _config = SteamBuildConfig.FindOrCreate();
            _version = EditorPrefs.GetString(PrefVersion, PlayerSettings.bundleVersion);
            _description = EditorPrefs.GetString(PrefDescription, "");
            _envIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefEnv, 0), 0, Mathf.Max(0, _config.environments.Count - 1));
            _branchIndex = EditorPrefs.GetInt(PrefBranch, 0);
            // Seed username from EditorPrefs first, then fall back to the asset (for new
            // machines where the prefs key doesn't exist yet).
            _steamUsername = EditorPrefs.GetString(PrefSteamUser, _config.steamUsername ?? "");
            // Defaults to OFF: a release build is the safe assumption if nobody chose.
            _developmentBuild = EditorPrefs.GetBool(PrefDevBuild, false);
            _extraDefines = EditorPrefs.GetString(PrefExtraDefines, "");
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefVersion, _version);
            EditorPrefs.SetString(PrefDescription, _description);
            EditorPrefs.SetInt(PrefEnv, _envIndex);
            EditorPrefs.SetInt(PrefBranch, _branchIndex);
            EditorPrefs.SetString(PrefSteamUser, _steamUsername ?? "");
            EditorPrefs.SetBool(PrefDevBuild, _developmentBuild);
            EditorPrefs.SetString(PrefExtraDefines, _extraDefines ?? "");
            // _steamPassword intentionally not persisted.
        }

        private void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("Could not load SteamBuildConfig.", MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Steam Build Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawConfigSection();
            EditorGUILayout.Space();
            DrawCredentialsSection();
            EditorGUILayout.Space();
            DrawEnvironmentSection();
            EditorGUILayout.Space();
            DrawDefinesSection();
            EditorGUILayout.Space();
            DrawVersionSection();
            EditorGUILayout.Space();
            DrawActionSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Asset", _config, typeof(SteamBuildConfig), false);
                if (GUILayout.Button("Edit", GUILayout.Width(60)))
                    Selection.activeObject = _config;
                EditorGUILayout.EndHorizontal();

                bool steamCmdOk = File.Exists(_config.steamCmdPath);
                var oldColor = GUI.contentColor;
                GUI.contentColor = steamCmdOk ? oldColor : new Color(1f, 0.55f, 0.4f);
                EditorGUILayout.LabelField("steamcmd",
                    steamCmdOk ? _config.steamCmdPath : "(MISSING) " + _config.steamCmdPath);
                GUI.contentColor = oldColor;
            }
        }

        private void DrawCredentialsSection()
        {
            EditorGUILayout.LabelField("Steam Credentials", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                _steamUsername = EditorGUILayout.TextField("Username", _steamUsername ?? "");

                // PasswordField hides the value; it's only kept in memory for this Unity
                // session. Leave blank to reuse the cached Steam Guard token.
                _steamPassword = EditorGUILayout.PasswordField("Password (optional)", _steamPassword ?? "");

                EditorGUILayout.HelpBox(
                    "Password is held in memory for this Unity session only — it is NOT saved.\n" +
                    "Recommended flow: leave blank and use 'First Time Setup' once per machine to " +
                    "cache the Steam Guard token; later uploads then run without prompts.",
                    MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(
                    string.IsNullOrWhiteSpace(_steamUsername) ||
                    !File.Exists(_config.steamCmdPath)))
                {
                    if (GUILayout.Button("First Time Setup (interactive login)", GUILayout.Height(24)))
                    {
                        try
                        {
                            SteamBuildPipelineRunner.RunInteractiveLogin(
                                _config.steamCmdPath, _steamUsername);
                            EditorUtility.DisplayDialog("Steam Login",
                                "A CMD window has opened.\n\n" +
                                "1. Enter password\n" +
                                "2. Enter Steam Guard code if asked\n" +
                                "3. Wait for 'OK'\n" +
                                "4. Close the window\n\n" +
                                "After this, uploads work without a password.",
                                "OK");
                        }
                        catch (System.Exception e)
                        {
                            EditorUtility.DisplayDialog("Failed", e.Message, "OK");
                        }
                    }
                }

                if (GUILayout.Button("Clear Password", GUILayout.Width(120), GUILayout.Height(24)))
                    _steamPassword = "";

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            if (_config.environments.Count == 0)
            {
                EditorGUILayout.HelpBox("No environments configured. Edit SteamBuildConfig.", MessageType.Warning);
                return;
            }

            var envNames = new string[_config.environments.Count];
            for (int i = 0; i < _config.environments.Count; i++)
                envNames[i] = $"{_config.environments[i].name} (app {_config.environments[i].appId})";
            _envIndex = EditorGUILayout.Popup("Environment", _envIndex, envNames);
            _envIndex = Mathf.Clamp(_envIndex, 0, _config.environments.Count - 1);
            var env = _config.environments[_envIndex];

            // Depots summary so the operator can see what gets uploaded.
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var d in env.depots)
                    EditorGUILayout.LabelField($"Depot {d.depotId}",
                        string.IsNullOrEmpty(d.depotPathLabel) ? "(no label)" : d.depotPathLabel);
            }

            if (env.allowedBranches.Count == 0)
            {
                EditorGUILayout.HelpBox("No branches whitelisted for this environment.", MessageType.Warning);
                return;
            }
            _branchIndex = Mathf.Clamp(_branchIndex, 0, env.allowedBranches.Count - 1);
            _branchIndex = EditorGUILayout.Popup("Set live on branch", _branchIndex, env.allowedBranches.ToArray());

            if (!string.IsNullOrEmpty(env.notes))
                EditorGUILayout.HelpBox(env.notes, MessageType.Info);
        }

        /// <summary>
        /// Two levels of defines: the environment's own list (persisted in the config asset,
        /// so Demo always builds with DEMO_BUILD) and a scratch field for one-off symbols.
        /// Both are handed to the build via BuildPlayerOptions.extraScriptingDefines — the
        /// project's Player Settings are never modified.
        /// </summary>
        private void DrawDefinesSection()
        {
            EditorGUILayout.LabelField("Scripting Defines", EditorStyles.boldLabel);
            if (_config.environments.Count == 0) return;

            var env = _config.environments[_envIndex];

            using (new EditorGUI.IndentLevelScope())
            {
                string current = string.Join(";", env.scriptingDefines ?? new System.Collections.Generic.List<string>());
                string edited = EditorGUILayout.DelayedTextField(
                    new GUIContent($"{env.name} defines",
                        "Saved into SteamBuildConfig — every build of this environment gets them. " +
                        "Separate with ';'."),
                    current);

                if (edited != current)
                {
                    Undo.RecordObject(_config, "Edit Environment Defines");
                    env.scriptingDefines = new System.Collections.Generic.List<string>(
                        (edited ?? "").Split(new[] { ';', ',', ' ', '\t' },
                            System.StringSplitOptions.RemoveEmptyEntries));
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                }

                _extraDefines = EditorGUILayout.TextField(
                    new GUIContent("Extra (this build)",
                        "Ad-hoc symbols for the next build only. Remembered on this machine, " +
                        "not saved into the config asset."),
                    _extraDefines ?? "");

                var effective = SteamBuildPipelineRunner.ResolveScriptingDefines(
                    env, _extraDefines, out var rejected);

                EditorGUILayout.LabelField("Effective",
                    effective.Length > 0 ? string.Join(";", effective) : "(none)");

                if (rejected.Length > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Ignored — not valid define symbols: " + string.Join(", ", rejected) + "\n" +
                        "A symbol must start with a letter or '_' and contain only letters, digits and '_'.",
                        MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    "Applied to this build only (BuildPlayerOptions.extraScriptingDefines). " +
                    "Your Player Settings defines stay untouched and are still compiled in.",
                    MessageType.None);
            }
        }

        private void DrawVersionSection()
        {
            EditorGUILayout.LabelField("Build Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current bundleVersion", PlayerSettings.bundleVersion);
            _version = EditorGUILayout.TextField("New version", _version);

            EditorGUILayout.LabelField("Build description (Steam changelog)");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(80));
        }

        private void DrawActionSection()
        {
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);

            _developmentBuild = EditorGUILayout.ToggleLeft(
                "Development build (profiler + dev console; leave OFF for public releases)",
                _developmentBuild);

            // Shipping a development player to a live branch is almost always a mistake:
            // slower, and it exposes the debug console to players. Warn loudly, don't block —
            // a dev build on a Playtest/beta branch is a legitimate workflow.
            if (_developmentBuild && _uploadToSteam && !_previewOnly)
            {
                EditorGUILayout.HelpBox(
                    "This uploads a DEVELOPMENT build to Steam. Fine for Playtest/QA branches, " +
                    "but never ship one to players.",
                    MessageType.Warning);
            }

            _uploadToSteam = EditorGUILayout.ToggleLeft(
                "Upload to Steam after build (uncheck for local-only build)", _uploadToSteam);

            using (new EditorGUI.DisabledScope(!_uploadToSteam))
            {
                _previewOnly = EditorGUILayout.ToggleLeft(
                    "Preview only (steamcmd validates; no actual upload)", _previewOnly);
            }

            EditorGUILayout.Space();

            string buttonLabel = _uploadToSteam
                ? (_previewOnly ? "Build + Preview Upload" : "Build + Upload to Steam")
                : "Build (local only)";
            if (_developmentBuild) buttonLabel += "  [DEV]";

            GUI.backgroundColor = _uploadToSteam && !_previewOnly ? new Color(0.85f, 0.55f, 0.55f) : Color.white;
            if (GUILayout.Button(buttonLabel, GUILayout.Height(40)))
            {
                if (Confirm())
                    Execute();
            }
            GUI.backgroundColor = Color.white;
        }

        private bool Confirm()
        {
            var env = _config.environments[_envIndex];
            string branch = env.allowedBranches[_branchIndex];
            var defines = SteamBuildPipelineRunner.ResolveScriptingDefines(env, _extraDefines, out _);
            string msg = $"Environment: {env.name} (AppId {env.appId})\n" +
                         $"Version: {_version}\n" +
                         $"Branch: {branch}\n" +
                         $"Build type: {(_developmentBuild ? "DEVELOPMENT" : "Release")}\n" +
                         $"Defines: {(defines.Length > 0 ? string.Join(";", defines) : "(none)")}\n" +
                         $"Upload: {(_uploadToSteam ? (_previewOnly ? "preview" : "yes") : "no")}\n\n" +
                         "Proceed?";
            return EditorUtility.DisplayDialog("Steam Build Pipeline", msg, "Build", "Cancel");
        }

        private void Execute()
        {
            var env = _config.environments[_envIndex];
            var req = new SteamBuildPipelineRunner.Request
            {
                config = _config,
                environment = env,
                version = _version.Trim(),
                buildDescription = _description.Trim(),
                branch = env.allowedBranches[_branchIndex],
                uploadToSteam = _uploadToSteam,
                previewOnly = _previewOnly,
                developmentBuild = _developmentBuild,
                additionalScriptingDefines = _extraDefines ?? "",
                steamUsername = (_steamUsername ?? "").Trim(),
                steamPassword = _steamPassword ?? ""
            };

            var result = SteamBuildPipelineRunner.Run(req);

            if (result.success)
            {
                EditorUtility.DisplayDialog("Steam Build",
                    $"Done.\n\nBuild: {result.buildOutputDir}\nVDF: {result.appVdfPath}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Steam Build — FAILED",
                    result.error ?? "Unknown error. See Console.", "OK");
            }
        }
    }
}
#endif
