# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-05-19

### Validated against

- PEAK build 23203792

### Added
- **Climber icon on live player rows in the Ghost Spectator panel**, drawn procedurally as a person silhouette and tinted to that player's chosen character skin color. Spectator rows continue to show the ghost icon, also skin-color tinted.
- **State-aware live-row icon during a run.** Live players whose character is currently in vanilla mid-run dead/passedOut state (waiting to be revived at a statue/effigy/chest) render with the ghost icon instead of the climber silhouette, while still appearing in the LIVE section. They flip back to the climber icon once revived.
- **Ghost sprite for spectator slots in the post-EndScreen "waiting for others" portrait strip.** Spectator slots now show a ghost shape instead of the vanilla scout sprite, tinted with the spectator's character skin color. Live climbers keep PEAK's vanilla scout sprite.

### Changed
- **Consistent player ordering across clients.** The Ghost Spectator panel previously sorted the local player first (different per client). Now sorts by Photon `ActorNumber` ascending -- host/first-joiner first, then each player in join order. Every client renders the same player order. The local player's row is still tagged "(you)" in the label.
- **Mid-run join popup auto-spectates when the live cap is full.** Previously the [Play] / [Spectate] choice always showed even if joining as live would push past the 4-climber cap. Now the joiner's HandleMessage prefix counts live players from the master's Steam-lobby-mirrored `RunRoles` dict; if `live >= LiveCap`, the popup is skipped and the joiner is auto-routed to Spectate. Holds slots for leavers (per-run role lock semantics): a player who leaves mid-run keeps their slot until run end, and a friend invited while the seat is "empty" will spectate.

## [0.1.1] - 2026-05-19

### Validated against

- PEAK build 23203792

### Fixed
- **Post-EndScreen "waiting for others" strip now shows every player.** Vanilla's `WaitingForPlayersUI.scoutImages[]` is a fixed 4-slot array, which silently dropped climbers when the raised lobby cap pushed total players above 4. The array now dynamically expands to `Character.AllCharacters.Count` by cloning the prefab's first slot under the same parent. Approach lifted from [PEAK-Unlimited](https://github.com/glarmer/PEAK-Unlimited).

## [0.1.0] - 2026-05-19

### Validated against

- PEAK build 23203792

Initial implementation of permanent spectator-ghost mode for the Steam game [PEAK](https://store.steampowered.com/app/3527290/PEAK/).

### Added
- **Permanent ghost mode for the run.** When enabled, your character spawns on the island already dead, uses the vanilla ghost camera and voice chat, drops no items, leaves no skeleton, and is never revived by Scout Effigies, Ancient Statues, or Respawn Chests.
- **Airport panel with live + spectator sections.** Top-left "Ghost Spectator" panel splits the roster into "LIVE n/4" and "SPECTATORS n/16" headers. Live section always renders all 4 slots so an open seat is visible at a glance; spectator section renders only occupied rows, each with a ghost icon tinted by the player's chosen skin color. A mouse-only power button toggles your spectator status, broadcast to the lobby and persisted across launches.
- **Spectator-aware revive plumbing.** Scout Effigy, Ancient Statue, and Respawn Chest candidate pools skip spectators. A Respawn Chest correctly drops items when the only "down" player is a spectator.
- **Spectator-aware item spawning.** Marshmallows, hotdogs, backpacks, and other per-player items spawn based on live-player count rather than raw room size: a 4-player lobby with one spectator gets items for 3.
- **Spectator-aware fog and lava pacing.** Fog can advance early once every live player has climbed past the segment threshold (vanilla behavior, with the spectator excluded from the "everyone moved on" check). Lava ascent is gated on real climbers.
- **Ghost pings in both directions.** Any ghost (this mod's spectator OR a vanilla mid-run dead-and-not-revived player) can fire pings, and ping opacity is computed from the ghost's camera position rather than their teleport-to-DeathPos body, so visibility matches what they're actually looking at.
- **Spectator-safe EndScreen.** The run-end results screen renders without crashing for spectator clients. The Scouting Report and timeline graph hide spectators (they never moved), and the Next button reliably activates so the player can return to the airport.
- **Spectator HUD passes through the spectated teammate.** Stamina, mushroom canvas, item hotbar, and backpack reflect the player you're following. Spectator-only widgets (dying bar, afflictions, achievement / ascent progression) are suppressed.
- **Customization preserved on rejoin.** A spectator who leaves and returns to a room keeps their Steam-stat-backed character cosmetics instead of resetting to the default red skin.
- **Spectator camera fallback.** When the teammate you're following disappears mid-run, the camera holds position rather than jumping. Only falls back to the beach if no live teammate ever existed this run.
- **Spectator-themed EndScreen banners.** Replaces the "YOUR BODY WAS NEVER FOUND" / "YOUR FRIENDS LEFT YOU TO DIE" verdict with witness-themed alternates ("A WITNESS TO NOTHING," "GLORY BY PROXY," etc.) rolled at random per run. Vanilla mid-run ghosts keep PEAK's original wording; only this mod's permanent spectator sees the alternates.
- **Spectator slots beyond the 4-player room cap.** Photon room cap raised to 4 live climbers + 16 spectators. Live cap is enforced client-side so vanilla expectations (4 helicopter seats, 4 scout windows) hold; only the spectator count grows.
- **Mid-run join popup.** When invited to a friend's lobby mid-run, the joiner sees a centered [Play] vs [Spectate] choice over the loading screen. The choice commits before the character spawns, so the joiner lands on the island already in the right state.
- **Per-run role lock.** Once a player has been outside the airport this run, their role is locked for the rest of the run. Leaving and rejoining mid-run skips the popup and re-applies the previously chosen role; switching roles requires waiting for the next run.
- **Mod-mandatory enforcement.** When the host has GhostSpectator installed, every other player must too. The kiosk refuses to start a run if any player lacks the mod, with a banner naming the offender. Mid-run joiners without the mod are detected at character spawn and kicked immediately; their character is hidden on every moded client during the sub-second window between detection and disconnect.
- **All-spectator-lobby protection.** The airport kiosk also refuses runs where every player is a spectator, since there'd be nobody to follow.

### Known limitations
- The mid-run join popup doesn't yet auto-spectate when all 4 live slots are full; both [Play] and [Spectate] options remain enabled. (PEAK's auto-scene-sync prevents knowing the live count before the popup needs to show. Workaround: just pick Spectate if you know the team is full.)
