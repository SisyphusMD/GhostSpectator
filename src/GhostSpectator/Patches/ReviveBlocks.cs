using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;

namespace GhostSpectator.Patches;

// Block any revive RPC arriving for any spectator on any client. Uses
// IsTrustedSpectator (remote-aware + corroborated against data.dead) rather
// than IsLocalSpectator so the host's copy of the revive also no-ops --
// otherwise the host's local view of a remote spectator would briefly think
// they were alive until PEAK's next state sync corrects it.
[HarmonyPatch(typeof(Character), nameof(Character.RPCA_Revive))]
internal static class Patch_Character_RPCA_Revive
{
    [HarmonyPrefix]
    private static bool Prefix(Character __instance)
    {
        if (SpectatorState.IsTrustedSpectator(__instance))
        {
            Plugin.TraceDebug("GhostSpectator: blocked RPCA_Revive on spectator.");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.RPCA_ReviveAtPosition))]
internal static class Patch_Character_RPCA_ReviveAtPosition
{
    [HarmonyPrefix]
    private static bool Prefix(Character __instance)
    {
        if (SpectatorState.IsTrustedSpectator(__instance))
        {
            Plugin.TraceDebug("GhostSpectator: blocked RPCA_ReviveAtPosition on spectator.");
            return false;
        }
        return true;
    }
}

// FULL REPLACEMENT, not a transpiler, because vanilla's Start calls
// `list.RandomSelection(...)` on an unguarded list -- if the filtered
// candidate pool is empty (only the spectator was dead) RandomSelection
// throws and the statue activation crashes mid-flight. A transpiler over
// `Character.AllCharacters` would still hit that unguarded call.
//
// Vanilla shape (verified against /tmp/peak-decompiled/RespawnRandomScout.cs):
//   if (PhotonNetwork.IsMasterClient) {
//     List<Character> list = new List<Character>();
//     foreach (var c in Character.AllCharacters)
//       if (c.data.dead || c.data.fullyPassedOut) list.Add(c);
//     list.RandomSelection(c => 1).photonView.RPC("RPCA_ReviveAtPosition",
//       RpcTarget.All, base.transform.position, false, -1);
//   }
//   Object.Destroy(base.gameObject);
//
// Our two deliberate deltas vs vanilla:
//   1. Filter `!IsTrustedSpectator(c)` so the host's candidate pool also
//      excludes any remote player flagged as a spectator (the host reads the
//      published Photon custom property).
//   2. Guard the RandomSelection call with `if (candidates.Count > 0)`,
//      matching vanilla's behavior when no real teammate is down (the
//      statue silently destroys itself, same as a run where everyone is
//      alive -- noted in CHANGELOG known limitations).
//
// IF PEAK updates RespawnRandomScout.Start to do anything else (sound,
// stat record, "no candidates" notification), this replacement silently
// drops it. Re-verify against the decompile after each PEAK update.
[HarmonyPatch(typeof(RespawnRandomScout), "Start")]
internal static class Patch_RespawnRandomScout_Start
{
    [HarmonyPrefix]
    private static bool Prefix(RespawnRandomScout __instance)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            var candidates = new List<Character>();
            foreach (var c in Character.AllCharacters)
            {
                if ((c.data.dead || c.data.fullyPassedOut) && !SpectatorState.IsTrustedSpectator(c))
                    candidates.Add(c);
            }

            if (candidates.Count > 0)
            {
                candidates.RandomSelection((Character _) => 1).photonView.RPC(
                    "RPCA_ReviveAtPosition",
                    RpcTarget.All,
                    __instance.transform.position,
                    false,
                    -1);
            }
        }
        UnityEngine.Object.Destroy(__instance.gameObject);
        return false;
    }
}
