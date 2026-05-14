using HarmonyLib;
using Peak.Network;

namespace GhostSpectator.Patches;

// Raise PEAK's vanilla 4-player Photon room cap to LiveCap + SpectatorCap so
// extra spectators can join past the 4-live limit. Patching the MAX_PLAYERS
// getter is sufficient because every consumer in PEAK reads it lazily:
// - NetworkingUtilities.HostRoomOptions sets RoomOptions.MaxPlayers from it
//   when creating a new Photon room
// - SteamLobbyAPI.CreateLobby passes it as the Steam-lobby member cap
// - SteamRichPresence reads it for the displayed "n/max_players" string
//
// The vanilla 4-cap on *live* climbers is preserved by client-side checks
// elsewhere: the mid-run join popup disables [Play] when live slots are full,
// and the airport menu shows live vs. spectator sections so a player can't
// accidentally fill all 4 climber slots with spectators. Photon itself has no
// concept of "live vs. spectator" -- it only knows the combined cap.
[HarmonyPatch(typeof(NetworkingUtilities), nameof(NetworkingUtilities.MAX_PLAYERS), MethodType.Getter)]
internal static class Patch_NetworkingUtilities_MAX_PLAYERS
{
    [HarmonyPrefix]
    private static bool Prefix(ref int __result)
    {
        __result = SpectatorState.TotalRoomCap;
        return false;
    }
}
