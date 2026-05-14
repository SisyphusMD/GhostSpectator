using HarmonyLib;
using Photon.Pun;
using UnityEngine.SceneManagement;

namespace GhostSpectator.Patches;

[HarmonyPatch(typeof(Character), nameof(Character.Start))]
internal static class Patch_Character_Start
{
    [HarmonyPostfix]
    private static void Postfix(Character __instance)
    {
        var sceneName = SceneManager.GetActiveScene().name;
        var isLocal = Character.localCharacter != null && __instance == Character.localCharacter;
        Plugin.TraceDebug($"[trace] Character.Start postfix fired. scene={sceneName}, isLocal={isLocal}, SpectatorEnabled={Plugin.SpectatorEnabled.Value}");

        // Any time the local character respawns (between scenes, after a run,
        // etc.) reset state that's scoped to a single run: captured spawn
        // position for the camera fallback, and the EndGame one-shot guard.
        if (isLocal)
        {
            SpectatorState.SpectatorSpawnPositionValid = false;
            SpectatorState.LastSpectateCameraValid = false;
            SpectatorState.RunHasEnded = false;
            SpectatorState.BannersOverriddenThisRun = false;
        }

        if (!SpectatorState.IsLocalSpectator(__instance)) return;
        if (SceneManager.GetActiveScene().name == "Airport") return;

        // Capture the spawn position BEFORE flipping to dead, so the camera
        // fallback has a real anchor. Once dead=true, FixedUpdate stops
        // writing LastLivingPosition and starts teleporting the ragdoll to
        // DeathPos every frame, so this is our only chance to snapshot it.
        SpectatorState.SpectatorSpawnPosition = __instance.transform.position;
        SpectatorState.SpectatorSpawnPositionValid = true;

        // Use PEAK's own RPCA_SetDead RPC instead of setting fields directly.
        // RPCA_SetDead is parameterless and on each client sets
        //   data.dead = true; fullyPassedOut = true; deathTimer = 1f; passedOut = true.
        // PUN's RpcTarget.All executes the RPC locally synchronously then
        // broadcasts to others, so we get immediate local effect AND remote
        // sync from one call. The remote sync is what lets the master
        // client's RunManager.CheckForGameEnd -> Character.CheckEndGame loop
        // observe all characters as dead, which is the trigger for EndGame ->
        // EndCutscene -> EndScreen. Without the broadcast our spectator dies
        // only locally and the master never fires the run-end flow.
        __instance.photonView.RPC("RPCA_SetDead", RpcTarget.All);
        __instance.refs.ragdoll.ToggleCollision(enableCollision: false);
        Plugin.Trace("GhostSpectator: local character forced into ghost state at spawn (RPCA_SetDead broadcast).");
    }
}

[HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RPC_SyncOnJoin))]
internal static class Patch_CharacterData_RPC_SyncOnJoin
{
    [HarmonyPostfix]
    private static void Postfix(CharacterData __instance)
    {
        if (!SpectatorState.IsLocalSpectator(__instance.character)) return;
        if (SceneManager.GetActiveScene().name == "Airport") return;

        __instance.fullyPassedOut = true;
        __instance.dead = true;
    }
}

// PEAK's CharacterSpawner.KillImmediately RPCs RPCA_Die on any character it
// considers "dead on arrival" -- which fires for our spectators because their
// data.dead is true at spawn (broadcast by our RPCA_SetDead). The vanilla
// RPCA_Die spawns a Skelleton at the body's position, drops items, records
// stats, etc. We don't need any of that for a spectator: the death flags
// are already set, the body is teleported to DeathPos by FixedUpdate, and
// the visible skeleton is just confusing clutter. Block RPCA_Die entirely
// for any spectator; non-spectator deaths run vanilla untouched.
[HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
internal static class Patch_Character_RPCA_Die
{
    [HarmonyPrefix]
    private static bool Prefix(Character __instance)
    {
        if (SpectatorState.IsTrustedSpectator(__instance))
        {
            Plugin.TraceDebug("[trace] blocked RPCA_Die on spectator (already dead via RPCA_SetDead, no skeleton/items needed).");
            return false;
        }
        // Race-window fallback. IsTrustedSpectator requires either a locked
        // role in RunRoles or in-game corroboration via IsGhost. If RPCA_Die
        // arrives before our RPCA_SetDead has flipped data.dead/fullyPassedOut
        // on this client AND the per-run lock hasn't propagated yet (very
        // brief window on a non-master moded client receiving both RPCs
        // back-to-back), neither check fires and vanilla RPCA_Die runs --
        // spawning a skeleton and dropping items for what should be a
        // spectator. ClaimsSpectator is property-only and arrives at join
        // time, so it's reliably populated by the time Die is RPC'd. The
        // worst-case false-positive is also benign for RPCA_Die: we'd
        // suppress death effects for a moded climber who briefly published
        // IsSpectator=true and is now dying, which is a narrow misuse window.
        if (SpectatorState.TryGetOwner(__instance, out var owner)
            && SpectatorState.ClaimsSpectator(owner))
        {
            Plugin.TraceDebug("[trace] blocked RPCA_Die on owner-claims-spectator (pre-corroboration race).");
            return false;
        }
        return true;
    }
}

// Guard on Character.EndGame. Two failure modes to defend against:
//
// 1) Spectator-as-host spawn race. Our Character.Start postfix broadcasts
//    RPCA_SetDead immediately on the spectator. If the spectator is also
//    the master client, `Character.AllCharacters` on the master at that
//    moment may only contain themselves -- the remote (alive) player's
//    Character hasn't synced over Photon yet. CheckEndGame iterates
//    AllCharacters, sees the lone spectator who is now dead, declares
//    all-dead, and fires EndGame prematurely. The alive teammate then
//    arrives mid-EndGame and CharacterSpawner.KillImmediately RPCs
//    RPCA_Die on them (spawning a skeleton and warping to DeathPos).
//    Suppress EndGame while the visible character list is shorter than
//    the room player count; the master polls again every 2 seconds via
//    ScheduleNextEndGameCheck, so once everyone has synced it'll fire
//    legitimately.
//
// 2) Duplicate EndGame within the same run. PEAK's CheckEndGame fires
//    EndGame more than once in multi-death scenarios -- each subsequent
//    call re-RPCs RPCEndGame, which closes and re-opens the EndScreen,
//    killing the in-progress EndSequenceRoutine before it reaches
//    `buttons.SetActive(true)` (the line that activates the Next button).
//    Skip subsequent EndGame calls within the same run; the flag is reset
//    by Patch_Character_Start.Postfix when a new local character is
//    spawned, by Patch_AirportCheckInKiosk_StartGame on run start, and by
//    RoomCallbackHandler.OnMasterClientSwitched on host migration.
[HarmonyPatch(typeof(Character), "EndGame")]
internal static class Patch_Character_EndGame
{
    // Tracks when our sync-wait suppression first kicked in for this run.
    // If suppression lasts longer than EndGameSuppressionTimeout, we give
    // up waiting and allow EndGame -- closes the "rapid join/leave spam
    // softlocks EndGame forever" attack where a malicious peer can keep
    // PlayerCount > AllCharacters.Count indefinitely.
    private static float? _firstSuppressUnscaledTime;
    private const float EndGameSuppressionTimeoutSeconds = 10f;

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (SpectatorState.RunHasEnded)
        {
            Plugin.Trace("[trace] EndGame called again in same run, suppressing duplicate to keep EndScreen's Next button intact.");
            return false;
        }
        if (!SpectatorState.AllExpectedCharactersSpawned(out var expectedPlayers, out var actualCharacters))
        {
            if (!_firstSuppressUnscaledTime.HasValue)
            {
                _firstSuppressUnscaledTime = UnityEngine.Time.realtimeSinceStartup;
            }
            var suppressedFor = UnityEngine.Time.realtimeSinceStartup - _firstSuppressUnscaledTime.Value;
            if (suppressedFor < EndGameSuppressionTimeoutSeconds)
            {
                Plugin.Trace($"[trace] EndGame suppressed: {actualCharacters} of {expectedPlayers} Characters spawned, waiting for sync (suppressed {suppressedFor:0.0}s).");
                return false;
            }
            Plugin.TraceWarn($"[trace] EndGame suppression timeout ({suppressedFor:0.0}s); allowing EndGame despite {expectedPlayers - actualCharacters} missing Character(s).");
        }
        _firstSuppressUnscaledTime = null;
        SpectatorState.RunHasEnded = true;
        Plugin.Trace("[trace] EndGame firing for the first time this run.");
        return true;
    }

    // Called from RunGating's OnRunStartedMaster to reset the suppression
    // timer at run start so a stale value from the previous run doesn't
    // immediately time out the first EndGame check of the new run.
    internal static void ResetSuppressionTimer()
    {
        _firstSuppressUnscaledTime = null;
    }
}

// Prevent the fog from advancing while we're still waiting for non-spectator
// Characters to sync over Photon. PEAK's OrbFogHandler.PlayersHaveMovedOn
// iterates Character.AllCharacters and only treats !data.dead characters as
// "still on the beach." When the master client is a spectator, their
// `data.dead` is true from spawn, so vanilla sees an empty "blockers" list,
// declares "Players have moved on", and RPCs StartMovingRPC. Every client
// then has `isMoving=true` -> `IsFoggingCurrentSegment=true` ->
// `PlayersHaveLeftShore=true`. The alive teammate's NewPlayerRoutine
// (driven by the master's RPC_NewPlayerSpawn) hits the
// `PlayersHaveLeftShore` branch and goes through SpawnDeadAtBaseCamp ->
// KillImmediately -> RPCA_Die (visible skeleton + warp to DeathPos). Same
// invariant as the EndGame sync guard: if AllCharacters.Count is less than
// the room's PlayerCount, remote Characters haven't synced yet and we must
// not advance the run. Once everyone has spawned, vanilla's check fires
// legitimately.
[HarmonyPatch(typeof(OrbFogHandler), "PlayersHaveMovedOn")]
internal static class Patch_OrbFogHandler_PlayersHaveMovedOn
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (!SpectatorState.AllExpectedCharactersSpawned(out _, out _))
        {
            __result = false;
            return false;
        }
        // Also block if every present Character is a spectator: vanilla would
        // return true (no living blocker) but in that case there's nobody
        // legitimately on the mountain to drive the fog forward.
        bool anyNonSpectator = false;
        if (Character.AllCharacters != null)
        {
            for (int i = 0; i < Character.AllCharacters.Count; i++)
            {
                var c = Character.AllCharacters[i];
                if (c != null && !SpectatorState.IsTrustedSpectator(c))
                {
                    anyNonSpectator = true;
                    break;
                }
            }
        }
        if (!anyNonSpectator)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
