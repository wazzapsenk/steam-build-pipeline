# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.2] - 2026-10-13

### Fixed
- `steam_appid.txt` is no longer uploaded to Steam. The post-processor copies it next to the
  exe for local testing, but the depot mapped the whole folder, so it shipped to players —
  with a single AppId that is wrong for Demo/Playtest apps. The depot VDF now excludes it.
- An expired Steam Guard token no longer hangs the Editor: steamcmd runs with
  `+@NoPromptForPassword 1`, so it exits with an error instead of waiting for input.
- The build folder (`<buildOutputRoot>/<env>/<version>/`) is wiped before each build, so files
  left over from an earlier build of the same version are no longer uploaded. Environment
  names / versions that are empty or dots only are rejected.
- The build button is disabled when there is no environment or no allowed branch, instead of
  throwing when clicked.

## [1.1.1] - 2026-07-27

### Fixed
- Moved `build-unitypackage.sh` to `Tools~/`. At the repo root it had no `.meta`, and Unity
  cannot generate one inside `Packages/` (an immutable folder), so every import of the UPM
  package logged *"has no meta file, but it's in an immutable folder"*. Folders ending in
  `~` are excluded from the asset database entirely.

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
