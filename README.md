# Steam Build Pipeline

One-click Unity → [steamcmd](https://partner.steamgames.com/doc/sdk/uploading) build & upload pipeline, driven from a single Editor window.

It builds a **Standalone Windows64** player, generates the Steamworks `app_build` / `depot_build` VDF scripts, and uploads to Steam via `steamcmd` — without leaving the Unity Editor. Multiple environments (e.g. **Demo / Playtest / Prod**) are supported, each with its own AppId, depots, and whitelisted branches so you can't accidentally push a Demo build to your Prod app.

## Features

- **Environments** — Demo / Playtest / Prod, each a separate Steam App with its own AppId + depots.
- **Branch whitelist** — the "set live" dropdown only shows branches you explicitly allowed per environment (`default`, `beta`, …), so a misclick can't ship to the wrong branch.
- **Version bump** — sets `PlayerSettings.bundleVersion` as part of the build.
- **steam_appid.txt copy** — a post-process step drops `steam_appid.txt` next to the built `.exe` so the overlay works when launched directly.
- **Steam Guard friendly** — cache the login token once per machine; later uploads run non-interactively with no password stored anywhere.
- **Build-only / Preview modes** — build locally without uploading, or run `steamcmd` in preview mode to validate without shipping.

## Requirements

- Unity **2021.3** or newer (Windows Editor).
- [Steamworks SDK](https://partner.steamgames.com/) with `steamcmd.exe` (ships under `tools/ContentBuilder/builder/`).
- A Steamworks partner account with at least one App + Depot configured.

## Installation

### Package Manager (Git URL) — recommended

1. In Unity: **Window ▸ Package Manager**.
2. **+ ▸ Add package from git URL…**
3. Paste:

   ```
   https://github.com/wazzapsenk/steam-build-pipeline.git
   ```

To pin a specific version, append a tag:

```
https://github.com/wazzapsenk/steam-build-pipeline.git#v1.1.0
```

### Or via `manifest.json`

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.wazanggames.steam-build-pipeline": "https://github.com/wazzapsenk/steam-build-pipeline.git#v1.1.0"
  }
}
```

> With the UPM install the files live **read-only** under `Packages/com.wazanggames.steam-build-pipeline/`. This keeps your `Assets/` clean and lets you update via git — but you can't move or edit the package files in place. If you want the sources copied into your project instead, use the `.unitypackage` below.

### Or import the `.unitypackage`

Grab `SteamBuildPipeline.unitypackage` from the [Releases](https://github.com/wazzapsenk/steam-build-pipeline/releases) page and double-click it (or **Assets ▸ Import Package ▸ Custom Package…**). The files land under `Assets/Plugins/WazangGames/SteamBuildPipeline/` and behave like normal project assets — move or edit them freely. Updates are manual: re-import a newer `.unitypackage`.

## Setup

1. (Optional) In the Project window, select the folder where you want the config to live. Then open **Tools ▸ Steam Build ▸ Build Pipeline** — on first open a `SteamBuildConfig` asset is created in the selected folder (or under `Assets/` if nothing is selected). You can move it anywhere afterwards; it is located by type, not path.
2. Select the asset and fill in:
   - **steamCmdPath** — absolute path to your `steamcmd.exe`.
   - **steamUsername** — your Steam partner upload account.
   - **environments** — for each environment set the real **AppId**, **DepotId(s)**, and allowed branches from your Steamworks dashboard.
3. (Optional) Put a `steam_appid.txt` file containing your AppId at the **project root** (next to `Assets/`). It will be copied next to every standalone build automatically.
4. In the window, click **First Time Setup (interactive login)** once per machine and complete the Steam Guard 2FA. After that, uploads run without a password.

## Usage

In **Tools ▸ Steam Build ▸ Build Pipeline**:

1. Pick the **Environment** (Demo / Playtest / Prod).
2. Set the **version** and write a **build description** (this becomes the Steam build changelog).
3. Choose the **set-live branch** (only whitelisted branches appear).
4. Click **Build + Upload to Steam**.
   - Uncheck *Upload* for a local-only build.
   - Check *Preview only* to validate via `steamcmd` without actually shipping.
   - Check *Development build* for a player with the profiler and dev console enabled
     (`BuildOptions.Development`). Useful for Playtest/QA drops; leave it off for public
     releases. The window warns if a dev build is about to be uploaded for real.

### Scripting defines

Each environment carries its own define symbols (`DEMO_BUILD`, `PLAYTEST`, …), stored in
`SteamBuildConfig` and editable straight from the build window. A second field takes one-off
symbols for the next build only.

Both are passed via `BuildPlayerOptions.extraScriptingDefines`, so they apply to that build
and nothing else — your Player Settings defines are never rewritten, there is no domain
reload mid-build, and a failed build can't leave the project stuck in `DEMO_BUILD`.
Defines from Player Settings still apply on top.

Build output lands in `Builds/<env>/<version>/`; generated VDF scripts and steamcmd logs go to `Tools/Steam/`.

## Security notes

- The password is held **in memory for the current Unity session only** — it is never written to disk. The recommended flow leaves it blank and relies on the cached Steam Guard token.
- If you set `steamUsername` (or any credential) inside `SteamBuildConfig`, **do not commit that asset to a public repo.** Add your `SteamBuildConfig.asset` to your project's `.gitignore` if it carries account info.

## License

[MIT](LICENSE.md)
