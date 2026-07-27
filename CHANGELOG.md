# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-27

### Added
- *Development build* toggle in the build window — enables `BuildOptions.Development`
  (profiler + dev console). Off by default, remembered per machine via `EditorPrefs`,
  shown in the confirmation dialog, and warned about when uploading a real build.
- Per-environment scripting define symbols (`EnvironmentSettings.scriptingDefines`) plus an
  "extra defines for this build" field, both editable from the build window. Applied through
  `BuildPlayerOptions.extraScriptingDefines` — Player Settings are left untouched. Invalid
  symbols are rejected with a warning instead of reaching the compiler.

## [1.0.0] - 2026-06-28

### Added
- Initial release.
- `SteamBuildPipelineWindow` Editor window (Tools ▸ Steam Build ▸ Build Pipeline).
- `SteamBuildConfig` ScriptableObject with per-environment AppId / depot / branch settings.
- `SteamBuildPipelineRunner` — version bump, Standalone Windows64 build, VDF generation, steamcmd upload.
- `SteamAppIdBuildPostProcessor` — copies `steam_appid.txt` next to standalone builds.
- Build-only and steamcmd preview modes.
- Steam Guard cached-token (passwordless) upload flow.
- Distributed both as a UPM git package and as a `.unitypackage` (lands under `Assets/Plugins/WazangGames/SteamBuildPipeline/`).
