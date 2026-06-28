# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-28

### Added
- Initial release.
- `SteamBuildPipelineWindow` Editor window (Tools ▸ Steam Build ▸ Build Pipeline).
- `SteamBuildConfig` ScriptableObject with per-environment AppId / depot / branch settings.
- `SteamBuildPipelineRunner` — version bump, Standalone Windows64 build, VDF generation, steamcmd upload.
- `SteamAppIdBuildPostProcessor` — copies `steam_appid.txt` next to standalone builds.
- Build-only and steamcmd preview modes.
- Steam Guard cached-token (passwordless) upload flow.
