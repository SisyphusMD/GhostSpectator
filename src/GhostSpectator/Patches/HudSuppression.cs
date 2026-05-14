using HarmonyLib;
using UnityEngine.SceneManagement;

namespace GhostSpectator.Patches;

// Manage the local HUD for a permanent spectator. PEAK's HUD elements split
// into two groups by what character they read:
//   - dyingBarObject reads `Character.localCharacter.data.deathTimer`
//     directly -- always our own (empty) revive timer.
//   - staminaCanvasGroup / mushroomsCanvasGroup / items[] / backpack read
//     `Character.observedCharacter` (= specCharacter if set, else
//     localCharacter). When the spectator is tracking an alive teammate
//     these show the teammate's state and we WANT them visible -- vanilla
//     ghosts get this for free. When the spectator has no real teammate to
//     observe, observedCharacter falls back to the local (dead) spectator
//     and these bars render as empty/zero, which is the visual clutter we
//     want to suppress.
// So: always hide the dying bar (strictly local), and only hide the
// observed-character-driven bars when there's no live non-spectator to
// observe.
[HarmonyPatch(typeof(GUIManager), "UpdateDyingBar")]
internal static class Patch_GUIManager_UpdateDyingBar
{
    // Single mutation site for the four observed-character-driven widgets.
    // Caller picks `visible=true` when a real teammate is being observed
    // (we want to see their stamina/items/backpack), `false` when the
    // observed character is ourselves or another spectator. Memoized: only
    // writes the underlying widgets when the desired state changes (or the
    // GUIManager instance changes, e.g. after a scene reload that
    // re-instantiated the HUD). Vanilla UpdateDyingBar runs every frame, so
    // without memoization this would re-issue six Unity property writes per
    // frame.
    private static GUIManager? _lastInstance;
    private static bool _lastObservedVisible;

    private static void SetObservedHudVisible(GUIManager m, bool visible)
    {
        if (ReferenceEquals(m, _lastInstance) && _lastObservedVisible == visible) return;
        _lastInstance = m;
        _lastObservedVisible = visible;

        float a = visible ? 1f : 0f;
        if (m.staminaCanvasGroup != null) m.staminaCanvasGroup.alpha = a;
        if (m.mushroomsCanvasGroup != null) m.mushroomsCanvasGroup.alpha = a;
        if (m.items != null)
        {
            for (int i = 0; i < m.items.Length; i++)
            {
                if (m.items[i] != null) m.items[i].gameObject.SetActive(visible);
            }
        }
        if (m.backpack != null) m.backpack.gameObject.SetActive(visible);
    }

    [HarmonyPrefix]
    private static bool Prefix(GUIManager __instance)
    {
        if (!Plugin.SpectatorEnabled.Value) return true;
        if (Character.localCharacter == null) return true;
        if (SceneManager.GetActiveScene().name == "Airport") return true;

        // Only flip when the underlying state changed; same memoization
        // rationale as SetObservedHudVisible.
        if (__instance.dyingBarObject != null && __instance.dyingBarObject.activeSelf)
        {
            __instance.dyingBarObject.SetActive(false);
        }

        // observedCharacter is `specCharacter ?? localCharacter`. If it's
        // our own spectator (no spec target picked, or spec target IS us
        // during the 5s post-spawn canBeSpectated window) the HUD reads
        // empty values; hide it. If it's a real teammate, show their
        // stamina/items so the spectator sees what they're watching.
        var observed = Character.observedCharacter;
        bool observingSelf = observed == null || observed == Character.localCharacter;
        bool observingSpectator = observed != null && !observingSelf
            && SpectatorState.IsTrustedSpectator(observed);
        SetObservedHudVisible(__instance, visible: !(observingSelf || observingSpectator));
        return false;
    }
}

// CharacterAfflictions.UpdateNormalStatuses keeps adding the night-cold status
// to the local player as long as Ascents.isNightCold and the day clock are in
// the right range -- it does NOT gate on dead/fullyPassedOut and it does NOT
// check the body's position, so the spectator accumulates cold (and the cold
// meter shows on their HUD) forever even though the body is just sitting on
// the beach. Block AddStatus entirely for the local spectator so no
// affliction can stick. NB: this is local-only on purpose; remote players
// who happen to have the mod installed should still see their own afflictions
// tick normally on their own character.
[HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
internal static class Patch_CharacterAfflictions_AddStatus
{
    [HarmonyPrefix]
    private static bool Prefix(CharacterAfflictions __instance, ref bool __result)
    {
        if (SpectatorState.IsLocalSpectator(__instance.character))
        {
            __result = false;
            return false;
        }
        return true;
    }
}

// EndScreen.TimelineRoutine accesses Character.localCharacter.refs.stats
// .GetFinalTimelineInfo().time / .GetFirstTimelineInfo().time unconditionally
// when computing the maxTime axis for the timeline animation. The spectator
// never moves or interacts, so their CharacterStats.timelineInfo list stays
// empty, and both Get*TimelineInfo accessors index into an empty list and
// throw ArgumentOutOfRangeException without these guards.
//
// We can't just return `default` (time=0) -- TimelineRoutine uses the local
// values as `(maxTime, startTime)` and falls back to maxTime=1 when they're
// equal, which then crushes every other character's pip x-positions to the
// rightmost edge via `Mathf.Clamp01((heightTime.time - 0) / 1)`. Visible
// result on a spectator client: alive teammates' columns render but their
// path is collapsed to a single dot at the right.
//
// Instead, borrow first/final from a non-spectator character that has real
// timeline entries -- their start/end times produce a correct axis so pips
// spread across the panel. If no climber has data yet (very early end), fall
// through to default and let PEAK's own maxTime==0 -> 1 guard handle it.
[HarmonyPatch(typeof(CharacterStats), nameof(CharacterStats.GetFinalTimelineInfo))]
internal static class Patch_CharacterStats_GetFinalTimelineInfo
{
    [HarmonyPrefix]
    private static bool Prefix(CharacterStats __instance, ref EndScreen.TimelineInfo __result)
    {
        if (__instance.timelineInfo != null && __instance.timelineInfo.Count > 0) return true;
        if (SpectatorState.TryBorrowTimelineInfo(first: false, out var borrowed))
        {
            __result = borrowed;
            return false;
        }
        __result = default;
        return false;
    }
}

// See Patch_CharacterStats_GetFinalTimelineInfo comment for the rationale on
// borrowing from a non-spectator character.
[HarmonyPatch(typeof(CharacterStats), nameof(CharacterStats.GetFirstTimelineInfo))]
internal static class Patch_CharacterStats_GetFirstTimelineInfo
{
    [HarmonyPrefix]
    private static bool Prefix(CharacterStats __instance, ref EndScreen.TimelineInfo __result)
    {
        if (__instance.timelineInfo != null && __instance.timelineInfo.Count > 0) return true;
        if (SpectatorState.TryBorrowTimelineInfo(first: true, out var borrowed))
        {
            __result = borrowed;
            return false;
        }
        __result = default;
        return false;
    }
}
