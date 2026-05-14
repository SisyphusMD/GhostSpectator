using HarmonyLib;
using Photon.Pun;

namespace GhostSpectator.Patches;

// Vanilla rejoin race: when a player leaves and rejoins, they enter as a new
// ActorNumber. The (remaining) master's PersistentPlayerDataService auto-
// initializes a default-zero entry for the new actor (any GetPlayerData call
// triggers `if (!dict.ContainsKey) dict[actor] = new PersistentPlayerData()`),
// then `OnCharacterRegistered` fires `SyncToPlayer(rejoiner)` which iterates
// the dict and broadcasts every entry back to the rejoiner -- including that
// just-init'd zero for the rejoiner themselves. The rejoiner's
// CharacterCustomization has by then already published proper Steam-stat-
// backed data via SetCustomizationData, but the master's stale zero arrives
// after and overwrites the rejoiner's local dict, so the rejoiner's own
// character (and our ghost icon, and vanilla render) flips to skin index 0
// (red default).
//
// State-based gate (no time check): drop self-targeted syncs only AFTER the
// local client has already published its own data (SetPlayerData postfix
// below flips HasPublishedLocalCustomizationData). Before that, the network is our only
// source for our own entry, so accept the sync. After that, the local copy
// is authoritative and any incoming self-sync is either an echo of our own
// publish or the master's stale rejoin-race echo. Drop either way.
// HasPublishedLocalCustomizationData is reset by RoomCallbackHandler.OnJoinedRoom.
[HarmonyPatch(typeof(PersistentPlayerDataService), "OnSyncReceived")]
internal static class Patch_PersistentPlayerDataService_OnSyncReceived
{
    [HarmonyPrefix]
    private static bool Prefix(SyncPersistentPlayerDataPackage package)
    {
        if (!PhotonNetwork.InRoom) return true;
        var local = PhotonNetwork.LocalPlayer;
        if (local == null) return true;
        if (package.ActorNumber != local.ActorNumber) return true;
        if (!SpectatorState.HasPublishedLocalCustomizationData) return true;
        Plugin.Trace($"[trace] ignored echoed self-sync (actor #{package.ActorNumber}); local is authoritative.");
        return false;
    }
}

// Flip HasPublishedLocalCustomizationData=true the first (and every) time the local client
// publishes its own PersistentPlayerData. Postfix on PEAK's SetPlayerData,
// which is the single entry point CharacterCustomization uses to push local
// Steam-stat-derived customization onto the local dict and broadcast it to
// other clients via SendPackage(ReceiverGroup.Others). Only sets the flag
// when the player being set is the local actor.
[HarmonyPatch(typeof(PersistentPlayerDataService), nameof(PersistentPlayerDataService.SetPlayerData))]
internal static class Patch_PersistentPlayerDataService_SetPlayerData
{
    [HarmonyPostfix]
    private static void Postfix(Photon.Realtime.Player player)
    {
        if (player == null) return;
        if (!PhotonNetwork.InRoom) return;
        var local = PhotonNetwork.LocalPlayer;
        if (local == null) return;
        if (player.ActorNumber != local.ActorNumber) return;
        SpectatorState.HasPublishedLocalCustomizationData = true;
    }
}
