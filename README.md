# GhostSpectator

A [BepInEx](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) plugin for [PEAK](https://store.steampowered.com/app/3527290/PEAK/) that lets a player spawn into every match as a permanent ghost, never alive, never revived, so they can spectate, switch between live players, and talk on voice chat exactly like a vanilla ghost.

> **Primary repository**: This project is developed at [forgejo.bryantserver.com/SisyphusMD/GhostSpectator](https://forgejo.bryantserver.com/SisyphusMD/GhostSpectator). The [GitHub copy](https://github.com/SisyphusMD/GhostSpectator) is a read-only mirror.

## Why

PEAK already has a ghost mechanic: when a player dies, they become a camera that follows live teammates and can talk on voice. This mod makes that state the *starting* state for one player, so you can join the lobby as a permanent ghost spectator and follow your teammates' climb.

## How it works

PEAK gates the vanilla ghost camera, voice handler, and player-ghost spawning on a single boolean: `CharacterData.fullyPassedOut`. GhostSpectator sets that flag (and `dead`) on the local player's `Character.Start` *when the run starts*, i.e., only on the island scene, not in the airport lobby. This way the spectator is alive in the airport (can walk around, socialize, watch you press the kiosk) and becomes a ghost the moment the run begins. To keep the spectator from breaking other players' revive economy, the mod also filters the local spectator out of every revive-candidate pool the game builds (Scout Effigy, Ancient Statue, Respawn Chest) and adjusts per-player item spawns, fog/lava pacing, and the end-of-run UI to ignore spectator presence.

Beyond the single-player ghost mechanic, GhostSpectator raises PEAK's room cap from 4 to 4 live + 16 spectator (20 total), gates mid-run joiners through a [Play] vs [Spectate] popup, and locks each player's role for the duration of a run.

## Installation

PEAK mods are distributed through [Thunderstore](https://thunderstore.io/c/peak/), a community-hosted mod registry for Unity games. There are two common ways to install: through a graphical mod manager (recommended, handles dependencies and updates) or by dropping files into the game's BepInEx folder manually.

### Requirements

- PEAK (Steam) -- [store page](https://store.steampowered.com/app/3527290/PEAK/)
- [BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) -- the mod loader. Listed as a dependency of GhostSpectator, so the mod manager pulls it automatically.

### Recommended: Thunderstore Mod Manager

1. Install one of:
   - [r2modman](https://thunderstore.io/package/ebkr/r2modman/) -- cross-platform, open source.
   - [Thunderstore Mod Manager](https://www.overwolf.com/app/thunderstore-thunderstore_mod_manager) -- official Overwolf-based client (Windows).
2. In the mod manager, select PEAK as the game, then find and install **GhostSpectator** by SisyphusMD. The manager will pull `BepInExPack_PEAK` automatically as a dependency.
3. Launch PEAK *through the mod manager* (the "Start modded" button). Launching from Steam directly will bypass the mods.

### Manual install

1. Download [BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/) from Thunderstore.
2. Extract its contents into your PEAK install directory (alongside `PEAK.exe`). On a default Steam install this is at:
   - Windows: `C:\Program Files (x86)\Steam\steamapps\common\PEAK\`
   - Linux: `~/.local/share/Steam/steamapps/common/PEAK/`
3. Launch PEAK once so BepInEx generates its directory structure, then quit.
4. Download the latest GhostSpectator zip from any of:
   - [forgejo releases](https://forgejo.bryantserver.com/SisyphusMD/GhostSpectator/releases) (primary)
   - [GitHub releases](https://github.com/SisyphusMD/GhostSpectator/releases) (mirror)
   - [Thunderstore](https://thunderstore.io/c/peak/p/SisyphusMD/GhostSpectator/) (package page)
5. Open the zip and copy `plugins/SisyphusMD.GhostSpectator.dll` into `<PEAK>/BepInEx/plugins/`.
6. Launch PEAK. The plugin generates its config file at `<PEAK>/BepInEx/config/SisyphusMD.GhostSpectator.cfg`.

## Usage

### Toggling spectator mode

In the airport lobby, click the **power button** in the top-left **Ghost Spectator** panel to toggle. The button is gray when `OFF` (the default) and green when `ON`. The setting persists across launches.

The panel shows two sections: **LIVE n/4** with one row per live-climber slot (empty slots show as "(open)"), and **SPECTATORS n/16** with the current spectators. Each spectator row has a ghost icon tinted by that player's chosen skin color.

When `ON`, you'll stay alive in the airport (walk around, socialize, watch the host press the kiosk) and transition to the ghost camera the moment the run starts on the island. Standard ghost-camera controls apply (hotbar `1`/`2` to switch between live players, scroll to zoom). Voice chat works normally.

Click the button again to toggle back to `OFF` and play normally.

> The button is the only intended interface for changing your spectator status, toggling it broadcasts your new status to the rest of your lobby. Don't edit `<PEAK>/BepInEx/config/SisyphusMD.GhostSpectator.cfg` directly; changes made there won't be communicated to other players until the next time you join a room.

### Joining a friend's run mid-game

When a friend invites you to a lobby that's already on the island, you'll see a centered **[Play] vs [Spectate]** popup over the loading screen. Pick one, and you'll spawn into the run in that role. If you pick `Play`, you join as a vanilla mid-run ghost (revivable at the next statue / chest, per PEAK's normal mid-run join behavior). If you pick `Spectate`, you spawn as a permanent ghost spectator.

Once you've made the choice, your role is **locked for the rest of that run**. If you leave the room and rejoin (intentional disconnect, wifi blip, etc.), the popup won't show again -- you'll auto-rejoin in the same role. To switch roles, wait for the host to start a new run from the airport.

### Required setup for groups

**If the host has GhostSpectator installed, every other player must too.** When the host clicks the kiosk, the run refuses to start if any player in the lobby is missing the mod (with a banner naming them). Mid-run joiners without the mod are kicked immediately. This is what makes the per-player spectator/live distinction reliable across the whole lobby.

Suggest having everyone subscribe to the mod once via Thunderstore so the group's runs always work.

## Compatibility

<!-- COMPAT:start -->
Validated against PEAK build **23203792** as of 2026-05-19 (GhostSpectator 0.2.0).
<!-- COMPAT:end -->

Each release is validated against a specific PEAK build before publishing (see [CHANGELOG.md](CHANGELOG.md) for per-version history). When PEAK updates, an older GhostSpectator may continue to work or may break on specific patch targets; if you hit issues after a PEAK update, check whether a newer GhostSpectator release is out.

## Bug reports & support

Found a bug or have a feature request? Email [high.goose3602@fastmail.com](mailto:high.goose3602@fastmail.com). Include your PEAK build, GhostSpectator version, and the relevant `<PEAK>/BepInEx/LogOutput.log` output. Multiplayer issues are easier to debug if you can also provide logs from the host.

This is a hobby project, not commercially supported. Best-effort response times.

## Support development

If GhostSpectator makes your runs better and you'd like to chip in: [buymeacoffee.com/sisyphusmd](https://buymeacoffee.com/sisyphusmd). Donations are entirely optional and don't change anything about the mod -- everything's free and AGPL-3.0.

## Acknowledgements

- [PEAKModding/BepInExTemplate](https://github.com/PEAKModding/BepInExTemplate), the official template this mod is built on.
- [leer-h/AliveSpectator-Mod-for-PEAK](https://github.com/leer-h/AliveSpectator-Mod-for-PEAK), reference for how PEAK's ghost camera is gated on `fullyPassedOut`.
- [glarmer/PEAK-Unlimited](https://github.com/glarmer/PEAK-Unlimited), reference for PEAK's Photon PUN 2 networking patterns.

## License

GNU AGPL-3.0, see [LICENSE](LICENSE).

---

Building from source or running a local release? See [DEVELOPING.md](DEVELOPING.md).
