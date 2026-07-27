#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WazangGames.SteamBuildPipeline
{
    /// <summary>
    /// Persisted settings for the Steam build pipeline. Holds the steamcmd location,
    /// the upload account, and per-environment (Demo/Playtest/Prod) AppId + Depot
    /// configuration. The asset lives in the consuming project (NOT in the package —
    /// AppIds are project specific); on first run it is created in whatever folder is
    /// selected in the Project window, or under Assets/ if nothing is selected.
    ///
    /// Keep this asset out of public branches if you fill in steamUsername with a
    /// shared account — Steamworks credentials in version control are a footgun.
    /// </summary>
    [CreateAssetMenu(fileName = "SteamBuildConfig", menuName = "Steam Build Pipeline/Steam Build Config", order = 100)]
    public class SteamBuildConfig : ScriptableObject
    {
        [Header("steamcmd")]
        [Tooltip("Absolute path to steamcmd.exe. Required for uploading builds.")]
        public string steamCmdPath = @"C:\steamworks_sdk\tools\ContentBuilder\builder\steamcmd.exe";

        [Tooltip("Steam Partner account used for uploading. Cache login once via " +
                 "'steamcmd +login <user> <pass>' on this machine — pipeline reuses the saved token.")]
        public string steamUsername = "";

        [Header("Local Paths")]
        [Tooltip("Root folder for Unity build output, relative to project root. Each build " +
                 "lands under <buildOutputRoot>/<env>/<version>/.")]
        public string buildOutputRoot = "Builds";

        [Tooltip("Root folder for generated VDF scripts and steamcmd output, relative to project root.")]
        public string steamWorkingDir = "Tools/Steam";

        [Header("Environments")]
        [Tooltip("Each environment is a separate Steam App (Demo and Playtest get their own AppIds). " +
                 "Fill in the real AppId / DepotId values from your Steamworks dashboard.")]
        public List<EnvironmentSettings> environments = new()
        {
            new EnvironmentSettings
            {
                name = "Prod",
                appId = 0,
                depots = new List<DepotSettings>
                {
                    new DepotSettings { depotId = 0, depotPathLabel = "Windows 64-bit Content" }
                },
                allowedBranches = new List<string> { "default", "beta" }
            },
            new EnvironmentSettings
            {
                name = "Demo",
                appId = 0,
                depots = new List<DepotSettings>
                {
                    new DepotSettings { depotId = 0, depotPathLabel = "Demo Windows Content" }
                },
                allowedBranches = new List<string> { "default" }
            },
            new EnvironmentSettings
            {
                name = "Playtest",
                appId = 0,
                depots = new List<DepotSettings>
                {
                    new DepotSettings { depotId = 0, depotPathLabel = "Playtest Windows Content" }
                },
                allowedBranches = new List<string> { "default" }
            }
        };

        public static SteamBuildConfig FindOrCreate()
        {
            var guids = AssetDatabase.FindAssets("t:SteamBuildConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<SteamBuildConfig>(path);
            }

            // Create the asset wherever the user currently has a folder selected in the
            // Project window; if nothing is selected, drop it at the project root (Assets).
            // The user can freely move it afterwards — FindAssets locates it by type, not path.
            string folder = GetSelectedFolderOrAssetsRoot();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/SteamBuildConfig.asset");

            var asset = CreateInstance<SteamBuildConfig>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        /// <summary>
        /// Returns the folder currently clicked/selected in the Project window.
        /// If a file is selected, returns its containing folder; if a folder is selected,
        /// returns it; if nothing is selected, returns "Assets". The result is always an
        /// existing folder, so no folder creation is needed.
        /// </summary>
        private static string GetSelectedFolderOrAssetsRoot()
        {
            foreach (var obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                    return path;                                  // a folder is selected

                string dir = Path.GetDirectoryName(path);         // a file is selected → its folder
                if (!string.IsNullOrEmpty(dir))
                    return dir.Replace('\\', '/');
            }

            return "Assets";                                      // nothing selected → project root
        }

        public EnvironmentSettings GetEnvironment(string envName)
        {
            return environments.Find(e =>
                string.Equals(e.name, envName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Serializable]
    public class EnvironmentSettings
    {
        [Tooltip("Display name, e.g. Demo / Playtest / Prod.")]
        public string name;

        [Tooltip("Steam AppId for this environment. Demo and Playtest are SEPARATE apps with their own ids.")]
        public uint appId;

        [Tooltip("Depots that ship with this app. Usually one Windows depot; add more for OSX/Linux/DLC.")]
        public List<DepotSettings> depots = new();

        [Tooltip("Branch names you allow shipping to. Pipeline limits the dropdown to this set " +
                 "so nobody accidentally pushes to 'default' instead of 'beta'.")]
        public List<string> allowedBranches = new() { "default" };

        [Tooltip("Scripting define symbols compiled into THIS environment's builds, e.g. " +
                 "DEMO_BUILD. Passed to the build only — Player Settings are left untouched, " +
                 "so there is no domain reload and nothing stale left behind afterwards.")]
        public List<string> scriptingDefines = new();

        [TextArea(2, 4)]
        [Tooltip("Optional reminder shown in the build window when this environment is selected.")]
        public string notes;
    }

    [Serializable]
    public class DepotSettings
    {
        public uint depotId;

        [Tooltip("Human label used in the UI; not sent to Steam.")]
        public string depotPathLabel;
    }
}
#endif
