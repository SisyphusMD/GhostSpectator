using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace GhostSpectator.Patches;

// Ghost ping: publish the ghost's current camera position as a Photon custom
// property on their LocalPlayer immediately before DoPing fires its RPC.
// Photon delivers property updates and RPCs in send-order on the reliable
// channel, so by the time remote clients receive the ping RPC they already
// see the latest camera position in the pinger's Player.CustomProperties.
// This lets remote ReceivePoint_Rpc handlers compute opacity from where the
// ghost was actually looking from, not from their DeathPos-teleported body.
// Local pingers don't need this (they use Camera.main directly in the receive
// prefix), but the property write happens regardless and is cheap.
[HarmonyPatch(typeof(PointPinger), "DoPing")]
internal static class Patch_PointPinger_DoPing
{
    [HarmonyPrefix]
    private static void Prefix(PointPinger __instance)
    {
        if (!SpectatorState.IsGhost(__instance.character)) return;
        var cam = Camera.main;
        if (cam == null) return;
        var ht = new ExitGames.Client.Photon.Hashtable
        {
            { SpectatorState.GhostPingerCameraKey, cam.transform.position },
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }
}

// Ghost ping: PEAK's PointPinger gates `canPing` on `character.data.fullyConscious`,
// which is false for any ghost (our permanent spectator AND vanilla mid-run
// dead-and-not-revived players). Allow any ghost to ping, just respect the
// cooldown so they can't spam.
[HarmonyPatch(typeof(PointPinger), nameof(PointPinger.canPing), MethodType.Getter)]
internal static class Patch_PointPinger_get_canPing
{
    [HarmonyPrefix]
    private static bool Prefix(PointPinger __instance, ref bool __result)
    {
        if (!SpectatorState.IsGhost(__instance.character)) return true;

        // Mirror vanilla's `!inCooldown` without the fullyConscious requirement.
        __result = (Time.time - __instance._timeLastPinged) >= __instance.coolDown;
        return false;
    }
}

// Ghost ping visibility, both directions, any ghost (vanilla or our mod).
// PEAK's ReceivePoint_Rpc computes opacity from `Vector3.Distance(pinger.Head,
// localCharacter.Head)`. For any ghost (data.dead || data.fullyPassedOut) the
// body is teleported to DeathPos `(0, 5000, -5000)` every FixedUpdate, so:
//   - Ghost pings: live receivers see ~5000 units away, opacity zeroes out.
//   - Live pings to a ghost: receiver's own localCharacter.Head is at DeathPos,
//     again ~5000 units, opacity zeroes out.
// We re-run the vanilla visibility math but substitute the camera position
// for either side that is a ghost (whichever side we have visibility into
// locally -- the local character we know via Camera.main; a remote ghost
// pinger's camera position isn't visible from our client, so we fall back to
// their character.Head and the distance will be huge unless we are also a
// ghost). Opacity is still distance-and-LOS gated, just measured from where
// the ghost actually looks from. Non-ghost-to-non-ghost pings run vanilla.
[HarmonyPatch(typeof(PointPinger), "ReceivePoint_Rpc")]
internal static class Patch_PointPinger_ReceivePoint_Rpc
{
    [HarmonyPrefix]
    private static bool Prefix(PointPinger __instance, Vector3 point, Vector3 hitNormal)
    {
        if (__instance.character == null) return true;

        // PunRPC payloads are attacker-controlled. NaN or Infinity in `point`
        // or `hitNormal` would propagate through Quaternion.LookRotation
        // (ArgumentException, spams the log) and Physics.Linecast (Unity's
        // PhysX has been observed to crash on non-finite endpoints in some
        // builds). Validate up front and drop.
        if (!SpectatorState.IsFiniteVec(point) || !SpectatorState.IsFiniteVec(hitNormal))
        {
            Plugin.TraceWarn($"GhostSpectator: dropped ReceivePoint_Rpc with non-finite payload (point={point}, hitNormal={hitNormal}).");
            return false;
        }

        var local = Character.localCharacter;
        bool pingerIsGhost = SpectatorState.IsGhost(__instance.character);
        bool localIsGhost = SpectatorState.IsGhost(local);
        if (!pingerIsGhost && !localIsGhost) return true;

        var cam = Camera.main;
        Vector3 pingerHead;
        if (pingerIsGhost)
        {
            if (__instance.character == local && cam != null)
            {
                // Local pinger: we have direct access to our own camera.
                pingerHead = cam.transform.position;
            }
            else
            {
                // Remote ghost pinger: read the camera position they published
                // via Photon custom property right before sending the RPC. If
                // it's missing (first ping race, or a non-modded ghost on the
                // other side), fall back to character.Head -- distance will
                // be huge from DeathPos and opacity may zero out, but better
                // than nothing.
                pingerHead = __instance.character.Head;
                if (SpectatorState.TryGetOwner(__instance.character, out var owner)
                    && owner.CustomProperties.TryGetValue(SpectatorState.GhostPingerCameraKey, out var raw)
                    && raw is Vector3 remoteCam)
                {
                    pingerHead = remoteCam;
                }
            }
        }
        else
        {
            pingerHead = __instance.character.Head;
        }
        Vector3 receiverHead = (localIsGhost && cam != null)
            ? cam.transform.position
            : (local != null ? local.Head : __instance.character.Head);

        // pingerHead can originate from a remote-published Photon custom
        // property (also attacker-controlled, separate channel from the RPC
        // payload). Validate both heads before they reach Physics.Linecast /
        // Quaternion.LookRotation.
        if (!SpectatorState.IsFiniteVec(pingerHead) || !SpectatorState.IsFiniteVec(receiverHead))
        {
            Plugin.TraceWarn($"GhostSpectator: dropped ReceivePoint_Rpc with non-finite head position (pingerHead={pingerHead}, receiverHead={receiverHead}).");
            return false;
        }

        // LookRotation throws ArgumentException on a zero look vector;
        // (point - pingerHead).normalized degenerates to Vector3.zero when
        // point == pingerHead (which can happen if a ghost pings exactly
        // through their own camera position). Bail rather than spawn a
        // mis-oriented or exception-trace ping.
        if ((point - pingerHead).sqrMagnitude < 1e-8f)
        {
            return false;
        }

        // Vanilla visibility math, with effective head positions.
        bool blocked = Physics.Linecast(pingerHead, receiverHead, HelperFunctions.terrainMapMask);
        float dist = Vector3.Distance(pingerHead, receiverHead);

        // Defensive null-walk. If a remote ghost ping arrives before
        // PointPinger has had pointPrefab assigned, or PEAK changes the
        // prefab to not carry the PointPing component, GetComponent returns
        // null and the next line NREs. Returning false drops the ping
        // silently rather than throwing on every received ping.
        if (__instance.pointPrefab == null) return false;
        var prefab = __instance.pointPrefab.GetComponent<PointPing>();
        if (prefab == null) return false;
        var v = prefab.visibilityFullNoneNoLos;
        float opacity = 1f - Mathf.InverseLerp(
            v.x,
            v.x + (v.y - v.x) * (blocked ? prefab.NoLosVisibilityMul : 1f),
            dist);

        if (opacity <= 0f) return false;

        if (__instance.pingInstance != null)
        {
            Object.DestroyImmediate(__instance.pingInstance);
        }
        __instance.pingInstance = Object.Instantiate(
            __instance.pointPrefab,
            point,
            Quaternion.LookRotation((point - pingerHead).normalized, Vector3.up));

        var component = __instance.pingInstance.GetComponent<PointPing>();
        component.hitNormal = hitNormal;
        component.Init(__instance.character);
        component.pointPinger = __instance;
        if (__instance.character.refs != null && __instance.character.refs.mainRenderer != null)
        {
            component.renderer.material = Object.Instantiate(
                __instance.character.refs.mainRenderer.sharedMaterial);
        }
        component.material.SetFloat("_Opacity", opacity);
        Object.Destroy(__instance.pingInstance, 2f);
        return false;
    }
}
