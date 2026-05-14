using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GhostSpectator.Patches;

// Camera fallback for any scenario where MainCameraMovement.Spectate can't
// find a spectatable target. In solo dev runs that's "no live teammates ever
// existed"; in real multiplayer it could be a brief window where everyone is
// dead, a Photon hiccup, or some state we didn't anticipate. Without this
// fallback the spec camera just freezes wherever it last was. Postfix: if
// PEAK's own Spectate logic ran without picking a target, point the camera
// at the local body using the same `GetSpectatePosition()` PEAK uses for
// self-spectating during the 5s post-death grace window -- so the spectator
// sees their own body on the shore instead of empty space. Gated on "no
// valid spec target" so the common multiplayer path (vanilla attached us to
// a live teammate) is untouched.
[HarmonyPatch(typeof(MainCameraMovement), "Spectate")]
internal static class Patch_MainCameraMovement_Spectate
{
    [HarmonyPostfix]
    private static void Postfix(MainCameraMovement __instance)
    {
        if (!Plugin.SpectatorEnabled.Value) return;
        if (SceneManager.GetActiveScene().name == "Airport") return;

        // Defer to vanilla whenever it picked a real (non-spectator) target.
        // Vanilla's `specCharacter.GetSpectatePosition()` handles the right
        // anchor in every case where the target is a regular player:
        //   - alive / passed-out: returns `Center` (current body position),
        //     so the camera stays on the last living teammate even after
        //     they fully pass out;
        //   - recently-dead (within the 5s grace): returns
        //     `LastLivingPosition`, the last frame they were alive.
        // While vanilla is driving the camera we also snapshot the resulting
        // transform every frame, so that if the live target later disappears
        // (e.g. host leaves the room mid-run) we can hold position where the
        // live body just was instead of jumping somewhere else.
        var spec = MainCameraMovement.specCharacter;
        if (spec != null && !SpectatorState.IsTrustedSpectator(spec))
        {
            SpectatorState.LastSpectateCameraPosition = __instance.transform.position;
            SpectatorState.LastSpectateCameraRotation = __instance.transform.rotation;
            SpectatorState.LastSpectateCameraValid = true;
            return;
        }

        // Vanilla didn't pick a real target (no candidates left, or it picked
        // a spectator which would point the camera at Vector3.zero). Hold the
        // last good camera transform if we ever had one this run -- this is
        // the host-left-mid-run case the user explicitly wants: keep the
        // camera where the live body was, not jump to our own spawn anchor.
        if (SpectatorState.LastSpectateCameraValid)
        {
            __instance.transform.position = SpectatorState.LastSpectateCameraPosition;
            __instance.transform.rotation = SpectatorState.LastSpectateCameraRotation;
            return;
        }

        // Never had a live target this run (solo dev run, all-spectator
        // lobby, or hitting the post-spawn grace window before vanilla has
        // picked a teammate yet). Fall back to our captured spawn anchor so
        // the camera sees the spectator's own body on the beach rather than
        // freezing at wherever it last was.
        var local = Character.localCharacter;
        if (local == null) return;

        // Character.FixedUpdate teleports the ragdoll to DeathPos
        // ((0, 5000, -5000)) every frame while data.dead is true, so
        // local.Center / local.LastLivingPosition are both useless (the
        // ragdoll is at DeathPos and LastLivingPosition stays at Vector3.zero
        // because vanilla writes it only on the not-dead branch of
        // FixedUpdate and we flip dead=true before the first FixedUpdate
        // ever runs). We captured the spawn position ourselves in
        // Character.Start postfix.
        if (!SpectatorState.SpectatorSpawnPositionValid) return;
        var anchor = SpectatorState.SpectatorSpawnPosition;
        var back = -local.data.lookDirection;
        var camPos = anchor + back * 3f + Vector3.up * 2f;
        // Aim the camera at a point 1.5m above the anchor (roughly eye/chest
        // height of where the standing character was) rather than the anchor
        // itself, which sits at ground level. Looking at the raw anchor tilts
        // the camera ~33 degrees below horizon ("ground-pointed"); raising the
        // look target softens that to ~9 degrees below horizon.
        var lookTarget = anchor + Vector3.up * 1.5f;
        __instance.transform.position = camPos;
        __instance.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized);

        if (_fallbackLogCount < 3)
        {
            Plugin.Trace($"[trace] camera spawn-anchor fallback active (no live target ever this run). anchor={anchor}, lookTarget={lookTarget}, camPos={camPos}");
            _fallbackLogCount++;
        }
    }

    private static int _fallbackLogCount = 0;
}
