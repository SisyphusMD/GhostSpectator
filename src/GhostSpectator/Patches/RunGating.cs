using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using GhostSpectator.Runtime;

namespace GhostSpectator.Patches;

// Shared kiosk-precondition check used by both prefix sites (clicker-side
// StartGame and master-side LoadIslandMaster). Two gates: (1) mod-mandatory
// -- every room member must have published the GhostSpectator.IsSpectator
// property at least once; (2) at-least-one-non-spectator -- a run with
// everyone in spectator mode has nobody to follow.
//
// Returns (passed, refuseMessage). If passed=true, refuseMessage is null.
internal static class KioskGate
{
    internal static (bool passed, string? refuseMessage) CheckRunStartPreconditions()
    {
        if (PhotonNetwork.CurrentRoom == null) return (true, null);

        foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player == null) continue;
            if (SpectatorState.HasGhostSpectatorMod(player)) continue;
            var name = string.IsNullOrEmpty(player.NickName) ? "(unnamed)" : player.NickName;
            return (false, $"Can't start: {name} doesn't have GhostSpectator installed. " +
                           "All players must install the mod from Thunderstore.");
        }

        if (RoomCallbackHandler.CountNonSpectators() == 0)
        {
            return (false, "Can't start: every player is set to Ghost Spectator. " +
                           "Toggle off in the airport panel, or wait for a non-spectator to join.");
        }

        return (true, null);
    }

    // Run-start state setup, called on master after preconditions pass.
    // Snapshots the current room into RunRoles (per-run role lock starts
    // here) and resets RunHasEnded. Master is the only writer of RunRoles;
    // the snapshot is published to room property + Steam lobby data via
    // PublishRunRolesToNetwork for all other clients to consume.
    internal static void OnRunStartedMaster()
    {
        SpectatorState.RunHasEnded = false;
        Patch_Character_EndGame.ResetSuppressionTimer();
        lock (RoleLock.RunRoles)
        {
            RoleLock.RunRoles.Clear();
            foreach (var p in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (p == null) continue;
                if (string.IsNullOrEmpty(p.UserId)) continue;
                RoleLock.RunRoles[p.UserId] = SpectatorState.ClaimsSpectator(p);
            }
        }
        Plugin.Trace($"[trace] run started: snapshotted {RoleLock.RunRoles.Count} player(s) into RunRoles");
        RoleLock.PublishRunRolesToNetwork();
    }
}

// Clicker-side prefix. Runs on whoever physically pressed the kiosk and
// only does anything if they have the mod (otherwise no patch exists on
// their client at all). Purpose: snappy local banner UX when a moded
// clicker would have been refused. The canonical master-side enforcement
// runs in Patch_AirportCheckInKiosk_LoadIslandMaster below.
[HarmonyPatch(typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.StartGame))]
internal static class Patch_AirportCheckInKiosk_StartGame
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        Plugin.TraceDebug($"[trace] StartGame prefix fired. InRoom={PhotonNetwork.InRoom}, " +
            $"SpectatorEnabled={Plugin.SpectatorEnabled.Value}, " +
            $"CurrentRoomPlayerCount={(PhotonNetwork.CurrentRoom?.PlayerCount.ToString() ?? "n/a")}, " +
            $"NonSpectators={RoomCallbackHandler.CountNonSpectators()}");

        var (passed, refuseMsg) = KioskGate.CheckRunStartPreconditions();
        if (!passed)
        {
            Plugin.TraceWarn($"GhostSpectator: clicker-side gate refused: {refuseMsg}");
            SpectatorState.RefuseKiosk(refuseMsg!);
            return false;
        }
        // RunHasEnded reset for the local client. Master also resets in the
        // LoadIslandMaster prefix; this handles the local-clicker case.
        SpectatorState.RunHasEnded = false;
        return true;
    }
}

// Master-side canonical enforcement. AirportCheckInKiosk.StartGame is the
// clicker's local entry point; vanilla then RPCs LoadIslandMaster to the
// master. By patching the master side, we enforce the gate regardless of
// who clicks (unmoded non-master clickers have no clicker-side prefix and
// would otherwise bypass everything). Also where the canonical RunRoles
// snapshot happens, since master is the source of truth for the dict.
[HarmonyPatch(typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.LoadIslandMaster))]
internal static class Patch_AirportCheckInKiosk_LoadIslandMaster
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        Plugin.TraceDebug($"[trace] LoadIslandMaster prefix fired. IsMasterClient={PhotonNetwork.IsMasterClient}");
        // Defense: PUN delivers RpcTarget.MasterClient only to master, but
        // if for any reason this fires on a non-master, do nothing.
        if (!PhotonNetwork.IsMasterClient) return true;

        var (passed, refuseMsg) = KioskGate.CheckRunStartPreconditions();
        if (!passed)
        {
            Plugin.TraceWarn($"GhostSpectator: master-side gate refused: {refuseMsg}");
            SpectatorState.RefuseKiosk(refuseMsg!);
            // Returning false skips the original LoadIslandMaster body, so
            // BeginIslandLoadRPC never broadcasts and the run doesn't start.
            return false;
        }

        KioskGate.OnRunStartedMaster();
        return true;
    }
}

// Degenerate run safety. PeakHandler.SetCosmetics is called synchronously from
// EndCutscene before StartCoroutine(OpenEndscreen()), and vanilla does
// `characters[0].refs.customization.SetCustomizationForRef(firstCutsceneScout)`
// after filtering to .won characters. In an all-spectators run nobody won, the
// filtered list is empty, and characters[0] throws -- aborting EndCutscene
// before OpenEndscreen starts, leaving the player with no EndScreen and no
// Next button to return to airport. When the filter would produce an empty
// list, skip the cosmetic setup entirely; OpenEndscreen still starts and the
// EndScreen renders with a working Next.
[HarmonyPatch(typeof(PeakHandler), "SetCosmetics")]
internal static class Patch_PeakHandler_SetCosmetics
{
    [HarmonyPrefix]
    private static bool Prefix(List<Character> characters)
    {
        if (characters == null) return true;
        for (int i = 0; i < characters.Count; i++)
        {
            var c = characters[i];
            if (c != null && c.refs != null && c.refs.stats != null && c.refs.stats.won) return true;
        }
        Plugin.Trace("[trace] SetCosmetics: nobody won this run, skipping cosmetic setup to keep EndCutscene from crashing.");
        return false;
    }
}

// Spectator gets no achievement credit. AchievementManager.ThrowAchievement
// is the single point all Steam achievement unlocks flow through; prefix-skip
// it when SpectatorEnabled is on. The spectator didn't climb, so awarding
// them the climb's achievements would be cheating Steam stats.
[HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.ThrowAchievement))]
internal static class Patch_AchievementManager_ThrowAchievement
{
    [HarmonyPrefix]
    private static bool Prefix(ACHIEVEMENTTYPE type)
    {
        if (!Plugin.SpectatorEnabled.Value) return true;
        Plugin.TraceDebug($"[trace] blocked achievement {type} on spectator");
        return false;
    }
}

// Spectator gets no ascent progression either. TryCompleteAscent populates
// runBasedValueData.completedAscentsThisRun, which the EndScreen reads to
// drive the AscentRoutine cutscene + ascent unlocks. Block it so the
// spectator's "completed ascents this run" stays empty and nothing unlocks
// from a run they didn't actually participate in.
[HarmonyPatch(typeof(AchievementManager), "TryCompleteAscent")]
internal static class Patch_AchievementManager_TryCompleteAscent
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!Plugin.SpectatorEnabled.Value) return true;
        Plugin.TraceDebug("[trace] blocked TryCompleteAscent on spectator");
        return false;
    }
}
