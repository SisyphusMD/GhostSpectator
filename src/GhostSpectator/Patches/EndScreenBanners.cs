using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Core;

namespace GhostSpectator.Patches;

// Replace the "verdict" banner text on the EndScreen with spectator-themed
// alternates when the local player is a ghost spectator (this mod's permanent
// spectator only; vanilla mid-run dead-and-not-revived players keep PEAK's
// original wording -- they didn't sign up to be a spectator and the original
// "YOUR BODY WAS NEVER FOUND" / "YOUR FRIENDS LEFT YOU TO DIE" fit their run
// better than the witness-themed alternates do).
//
// PEAK has four banner GameObjects on EndScreen (peakBanner, miniWinBanner,
// yourFriendsWonBanner, deadBanner); EndSequenceRoutine SetActives exactly
// one based on run outcome. A spectator is permanently dead so they only
// ever hit deadBanner (nobody summited) or yourFriendsWonBanner (someone
// else summited). The two win banners can't fire for a spectator -- skip
// overriding them entirely.
//
// Each banner has a LocalizedText component that resolves a localization
// key (BODYNEVERFOUND / LEFTTODIE / YOUPEAKED / YOUWIN) to the actual
// rendered string at OnEnable. Calling LocalizedText.SetText(string)
// bypasses the localization lookup AND clears the index field, so a
// subsequent RefreshText() won't restore the localized value.
//
// Hook: postfix on the EndSequenceRoutine coroutine's MoveNext (same
// compiler-generated state machine the AllCharacters transpiler also
// patches). The banner activations happen synchronously near the top of the
// coroutine, so our postfix sees the active banner on its next iteration.
// BannersOverriddenThisRun is the one-shot guard so we don't rotate the
// Random.Range pick every frame; reset by Patch_Character_Start.Postfix
// for the local player.
[HarmonyPatch]
internal static class Patch_EndScreen_EndSequenceRoutine_Banners
{
    static System.Reflection.MethodBase TargetMethod() =>
        SpectatorState.GetCoroutineMoveNext(
            typeof(EndScreen), "<EndSequenceRoutine>",
            nameof(Patch_EndScreen_EndSequenceRoutine_Banners))!;

    // Spectator-themed alternates for the two banners a permanent spectator
    // can hit. Each set is rolled at random per run. Visual layout matches
    // PEAK's vanilla strings: small line over a 125% line, separated by <br>.
    private static readonly string[] DeadBannerMessages = new[]
    {
        "NO BODIES.<br><size=125%>NOT EVEN YOURS",
        "A WITNESS<br><size=125%>TO NOTHING",
        "THE MOUNTAIN<br><size=125%>KEPT THEM ALL",
    };

    private static readonly string[] FriendsWonBannerMessages = new[]
    {
        "GLORY BY<br><size=125%>PROXY",
        "YOU HAUNTED THEM<br><size=125%>TO THE TOP",
        "THEY SUMMITED<br><size=125%>YOU WITNESSED",
    };

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (SpectatorState.BannersOverriddenThisRun) return;
        if (!Plugin.SpectatorEnabled.Value) return;
        var local = Character.localCharacter;
        if (local == null) return;
        if (!SpectatorState.IsLocalSpectator(local)) return;

        var es = EndScreen.instance;
        if (es == null) return;

        GameObject? activeBanner = null;
        string[]? messages = null;
        if (es.deadBanner != null && es.deadBanner.activeSelf)
        {
            activeBanner = es.deadBanner;
            messages = DeadBannerMessages;
        }
        else if (es.yourFriendsWonBanner != null && es.yourFriendsWonBanner.activeSelf)
        {
            activeBanner = es.yourFriendsWonBanner;
            messages = FriendsWonBannerMessages;
        }
        if (activeBanner == null || messages == null) return;

        var pick = messages[Random.Range(0, messages.Length)];

        // Prefer LocalizedText.SetText: it both updates the TMP and clears
        // the index so any later RefreshText() (e.g. a language change while
        // the EndScreen is open) won't restore the original localized value.
        var localized = activeBanner.GetComponentInChildren<LocalizedText>(includeInactive: true);
        if (localized != null)
        {
            localized.SetText(pick);
            SpectatorState.BannersOverriddenThisRun = true;
            Plugin.TraceDebug($"[trace] EndScreen banner overridden via LocalizedText: \"{pick}\"");
            return;
        }

        // Defensive fallback: if PEAK ever drops the LocalizedText component
        // from the banner prefab, mutate the TMP_Text directly. This works
        // for the duration of the EndScreen but doesn't survive a language
        // refresh; acceptable for a never-actually-hit fallback path.
        var tmp = activeBanner.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmp == null) return;
        tmp.text = pick;
        SpectatorState.BannersOverriddenThisRun = true;
        Plugin.TraceDebug($"[trace] EndScreen banner overridden via direct TMP_Text (no LocalizedText found): \"{pick}\"");
    }
}

// Post-EndScreen "waiting for others" portrait strip. WaitingForPlayersUI's
// scoutImages[] is configured in the Unity prefab as a fixed 4-slot Image
// array; vanilla Update walks PlayerHandler.GetAllPlayers() and silently
// drops anything past slot 3. With our raised lobby cap (4 live + 16
// spectators), most rooms exceed 4 total players, so live climbers can get
// dropped if spectators are ahead of them in iteration order.
//
// Spectators have their own Next button and are waited on, so they DO
// belong in the strip -- we just need more slots. Approach: prefix expands
// scoutImages[] to Character.AllCharacters.Count by Instantiate-cloning the
// first slot under the same parent (preserves the UI prefab's layout group
// + sprite + size + anchors). Vanilla Update then fills the expanded array
// normally; the prefix is idempotent so it's safe to re-run per frame.
//
// Approach lifted from PEAK-Unlimited's WaitingForPlayersUIPatch
// (https://github.com/glarmer/PEAK-Unlimited).
[HarmonyPatch(typeof(WaitingForPlayersUI), "Update")]
internal static class Patch_WaitingForPlayersUI_Update
{
    [HarmonyPrefix]
    private static void Prefix(WaitingForPlayersUI __instance)
    {
        if (__instance.scoutImages == null) return;
        if (__instance.scoutImages.Length == 0) return;
        if (__instance.scoutImages.Length >= Character.AllCharacters.Count) return;

        var template = __instance.scoutImages[0];
        if (template == null) return;

        var expanded = new Image[Character.AllCharacters.Count];
        for (int i = 0; i < Character.AllCharacters.Count; i++)
        {
            if (i < __instance.scoutImages.Length)
                expanded[i] = __instance.scoutImages[i];
            else
                expanded[i] = UnityEngine.Object.Instantiate(template, template.transform.parent);
        }
        __instance.scoutImages = expanded;
    }
}
