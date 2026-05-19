using System.Collections;
using HarmonyLib;
using Photon.Pun;
using Steamworks;
using UnityEngine;
using GhostSpectator.Runtime;

namespace GhostSpectator.Patches;

// Option-A mid-run join flow: when a friend invites a player into a lobby
// that's mid-run (host is on a non-Airport scene), block the joiner's scene
// load behind a choice popup. Sequence on the joiner's client:
//
//   1. Steam invite -> SteamLobbyHandler.HandleMessage case RoomID:
//      Our prefix on HandleMessage records the destination scene name
//      (read from Steam lobby's CurrentScene metadata) into a static field.
//   2. SteamLobbyHandler then calls LoadingScreenHandler.Load(...
//        LoadSceneProcess(text, networked: false, ...)).
//      Our prefix on Load sees the recorded scene name, and if it's
//      non-Airport, prepends WaitForMidRunChoice() ahead of LoadSceneProcess
//      in the processes[] array.
//   3. LoadingScreenHandler starts processes sequentially. Our coroutine
//      runs first: it waits for PhotonNetwork.InRoom, checks live cap, then
//      either auto-spectates (if cap full) or shows the popup and waits for
//      a click. The loading screen stays visible the whole time.
//   4. Once the coroutine completes, LoadSceneProcess runs, the scene loads,
//      and Character.Start fires with the correct IsSpectator value -- no
//      spawn-then-revert flash.
//
// The kiosk + mid-run-join mod-mandatory gates (handled separately in
// RunGating + RoomCallbackHandler) ensure every player in the room has the
// mod installed, so we can assume the popup's Photon-property publish will
// be readable by all clients.
internal static class MidRunJoinGate
{
    // Shared between the two prefixes. Set by HandleMessage prefix at the
    // RoomID case, consumed and cleared by the LoadingScreenHandler.Load
    // prefix. Null means "no mid-run join in flight; don't inject anything."
    internal static string? PendingSceneName;

    // Pre-decided role for the joiner, read from Steam lobby data in the
    // HandleMessage prefix when we detect ourselves in the master's RunRoles
    // dict (= we've been in this run before). null means "first-time joiner,
    // show the popup as usual." Consumed by the wait coroutine.
    internal static bool? LockedRoleForJoiner;

    // Number of live (non-spectator) players already in the run, counted from
    // the master's Steam-lobby-mirrored RunRoles at HandleMessage time. Used
    // by the wait coroutine to decide whether to disable the popup's [Play]
    // button -- if this is already >= LiveCap, joining as a live climber
    // would push past the cap. -1 = "didn't count" (no RunRoles read, or
    // Airport scene). Consumed and reset by the wait coroutine.
    internal static int LiveCountAtJoin = -1;
}

// Capture the destination scene name when a join-host-room message arrives.
// We do this in a prefix rather than reading from inside HandleMessage's
// case block because Harmony can't cleanly hook a switch case. Reading the
// scene metadata directly via the SteamMatchmaking API works just as well
// because PEAK uses the same source itself (see SteamLobbyHandler's own
// OnLobbyEnter caching of CurrentScene into m_currentlyWaitingForRoomID).
//
// Targeting "HandleMessage" by string because the method is private; Harmony
// resolves the name lookup against the type at patch attach time.
[HarmonyPatch(typeof(SteamLobbyHandler), "HandleMessage")]
internal static class Patch_SteamLobbyHandler_HandleMessage_CaptureScene
{
    [HarmonyPrefix]
    private static void Prefix(SteamLobbyHandler.MessageType messageType, CSteamID lobbyID)
    {
        if (messageType != SteamLobbyHandler.MessageType.RoomID) return;

        // Capture destination scene name (consumed by the LoadingScreenHandler
        // Load prefix to decide whether to inject the wait coroutine).
        string sceneName;
        try
        {
            sceneName = SteamMatchmaking.GetLobbyData(lobbyID, "CurrentScene");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[trace] reading Steam lobby CurrentScene threw: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        if (string.IsNullOrEmpty(sceneName)) sceneName = "Airport";
        MidRunJoinGate.PendingSceneName = sceneName;
        Plugin.TraceDebug($"[trace] HandleMessage RoomID: captured target scene '{sceneName}' for upcoming Load");

        // Per-run role lock: read the master's RunRoles dict from Steam
        // lobby data (where master mirrors it on every change). If our
        // Steam UserId is in there, we've been in this run before -- skip
        // the popup and apply the recorded role. This works even though
        // Photon JoinRoom hasn't completed yet because Steam lobby data is
        // accessible the moment we're in the Steam lobby (which happens
        // earlier in the join flow). MidRunJoinGate.LockedRoleForJoiner is then
        // consumed by the wait coroutine.
        MidRunJoinGate.LockedRoleForJoiner = null;
        MidRunJoinGate.LiveCountAtJoin = -1;
        if (sceneName == "Airport") return;
        try
        {
            string runRolesStr = SteamMatchmaking.GetLobbyData(lobbyID, Patches.RoleLock.RoomRunRolesKey);
            if (string.IsNullOrEmpty(runRolesStr)) return;
            var roles = new System.Collections.Generic.Dictionary<string, bool>();
            Patches.RoleLock.DeserializeRunRolesFromString(runRolesStr, roles);

            // Rejoiner check (existing).
            var ourUserId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            if (roles.TryGetValue(ourUserId, out var prefixedRole))
            {
                MidRunJoinGate.LockedRoleForJoiner = prefixedRole;
                Plugin.Trace($"[trace] HandleMessage RoomID: rejoiner detected (userId={ourUserId}); locked role isSpec={prefixedRole}");
            }

            // Live-cap pre-count for the popup gating: tally the master's
            // RunRoles entries where isSpec == false. This count reflects
            // the locked roles at run-start plus any first-time mid-run
            // joiners already recorded on master. Used by the wait coroutine
            // to disable the [Play] button when the cap is already full.
            int live = 0;
            foreach (var kvp in roles)
            {
                if (!kvp.Value) live++;
            }
            MidRunJoinGate.LiveCountAtJoin = live;
            Plugin.TraceDebug($"[trace] HandleMessage RoomID: counted {live} live player(s) already in run");
        }
        catch (System.Exception ex)
        {
            Plugin.TraceDebug($"[trace] reading Steam lobby RunRoles failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

// Inject our wait-for-choice coroutine at the front of LoadingScreenHandler's
// process list when the captured scene name indicates a mid-run join. The
// LoadingRoutine in LoadingScreenHandler runs processes sequentially via
// yield return StartCoroutine, so prepending ours blocks the actual scene
// load behind our popup. The loading screen stays visible the entire time.
//
// Skip injection when:
//   - PendingSceneName is null (Load was called from somewhere other than
//     SteamLobbyHandler's RoomID case -- airport return, kiosk, etc.)
//   - The target scene is "Airport" (joining a host who's still in the
//     lobby; no popup needed because the airport panel handles it)
[HarmonyPatch(typeof(LoadingScreenHandler), nameof(LoadingScreenHandler.Load))]
internal static class Patch_LoadingScreenHandler_Load_InjectPopupWait
{
    [HarmonyPrefix]
    private static void Prefix(ref IEnumerator[] processes)
    {
        var sceneName = MidRunJoinGate.PendingSceneName;
        // Clear BOTH baton fields before any early return. LockedRoleForJoiner is
        // normally consumed by WaitForMidRunChoice, but on early-return
        // paths (null/Airport/null-processes) the coroutine never runs and
        // a stale value would be picked up by the NEXT mid-run join,
        // applying a role from a different lobby.
        MidRunJoinGate.PendingSceneName = null;
        var leftoverLockedRoleForJoiner = MidRunJoinGate.LockedRoleForJoiner;
        var leftoverLiveCountAtJoin = MidRunJoinGate.LiveCountAtJoin;
        MidRunJoinGate.LockedRoleForJoiner = null;
        MidRunJoinGate.LiveCountAtJoin = -1;
        if (sceneName == null) return;
        if (sceneName == "Airport") return;
        if (processes == null) return;
        // Restore the baton fields only when we actually inject the wait
        // coroutine, since the coroutine is the consumer.
        MidRunJoinGate.LockedRoleForJoiner = leftoverLockedRoleForJoiner;
        MidRunJoinGate.LiveCountAtJoin = leftoverLiveCountAtJoin;

        Plugin.Trace($"[trace] injecting mid-run popup wait into Load() processes for scene '{sceneName}'");
        var wrapped = new IEnumerator[processes.Length + 1];
        wrapped[0] = WaitForMidRunChoice();
        for (int i = 0; i < processes.Length; i++) wrapped[i + 1] = processes[i];
        processes = wrapped;
    }

    private static IEnumerator WaitForMidRunChoice()
    {
        // Rejoiner fast path: HandleMessage prefix already looked up our
        // userId in master's Steam-lobby-mirrored RunRoles and recorded the
        // locked role. Apply it and skip the popup. Don't bother waiting
        // for anything else -- we already know the answer.
        var prefixed = MidRunJoinGate.LockedRoleForJoiner;
        MidRunJoinGate.LockedRoleForJoiner = null;
        if (prefixed.HasValue)
        {
            Plugin.Trace($"[trace] mid-run rejoiner: applying locked role spectate={prefixed.Value}, skipping popup");
            Plugin.SpectatorEnabled.Value = prefixed.Value;
            yield break;
        }

        // Live-cap-full fast path: HandleMessage prefix counted live players
        // from master's RunRoles. If the live cap is already saturated, joining
        // as a climber would push past it -- so the only sensible option is
        // [Spectate]. Skip the popup entirely and auto-spectate; clicking a
        // single-option modal is friction with no information value.
        int liveAtJoin = MidRunJoinGate.LiveCountAtJoin;
        MidRunJoinGate.LiveCountAtJoin = -1;
        if (liveAtJoin >= 0 && liveAtJoin >= Patches.SpectatorState.LiveCap)
        {
            Plugin.Trace($"[trace] mid-run joiner: live cap saturated ({liveAtJoin}/{Patches.SpectatorState.LiveCap}); auto-spectating, skipping popup");
            Plugin.SpectatorEnabled.Value = true;
            yield break;
        }

        // First-time mid-run joiner with cap room available: PEAK uses
        // PhotonNetwork.AutomaticallySyncScene = true, which holds the
        // JoinRoom handshake until the joiner is on the master's scene.
        // PhotonNetwork.InRoom can't flip to true until the scene load
        // completes -- which is what our wait coroutine is blocking. So we
        // can't wait for InRoom here. Show the popup unconditionally, wait
        // for the click, set SpectatorEnabled accordingly. The IsSpectator
        // property update is queued locally and broadcasts whenever
        // LoadingRoutine restores IsMessageQueueRunning (after scene load
        // completes). Master receives the property in OnPlayerPropertiesUpdate,
        // records it to RunRoles, and republishes to room property + Steam
        // lobby data.
        var popup = MidRunJoinPopup.Instance;
        if (popup == null)
        {
            Plugin.TraceWarn("[trace] mid-run wait coroutine: MidRunJoinPopup.Instance missing; defaulting to live join");
            Plugin.SpectatorEnabled.Value = false;
            yield break;
        }
        popup.Show();
        Plugin.Trace("[trace] mid-run wait coroutine: popup shown, waiting for click");
        // Defensive: also break if the popup hides without a click being
        // received (5-min timeout watchdog, future bug, etc.). Without this
        // the coroutine spins forever and blocks scene load -> JoinRoom ->
        // "forever loading screen."
        while (popup.IsShowing && !popup.HasChoice) yield return null;
        if (!popup.HasChoice)
        {
            Plugin.TraceWarn("[trace] mid-run wait coroutine: popup hid without a click; defaulting to live join");
            Plugin.SpectatorEnabled.Value = false;
            yield break;
        }
        popup.Hide();
        Plugin.Trace($"[trace] mid-run popup choice received: spectate={popup.ChoiceIsSpectate}");
    }
}

// Synchronous mod-validation gate. Fires on every client when any Character
// spawns (the joiner's Character.Start runs on their machine, the remote
// instantiation event reaches every other client, each running this postfix).
// Relies on Photon's reliable-channel message ordering: a moded joiner
// publishes their IsSpectator property (a Photon operation) BEFORE scene
// load + Character.Instantiate (also Photon operations), so by the time any
// client sees this character spawn, a moded owner's property is already on
// their Player.CustomProperties. Absence at this point is conclusive: the
// owner has no mod.
//
// Behavior on detection:
//   - Every moded client hides the character visually (renderers + UI
//     Graphics disabled) so they never paint to screen, eliminating the
//     "ghost player appears then vanishes" UX from the prior time-based
//     design.
//   - Master client also issues the kick via PEAK's PlayerHandler.Kick,
//     which RPCs RPC_GetKicked on the target -> their NetworkConnector
//     switches to KickedState -> they disconnect. Photon then destroys
//     the Character GameObject on all clients automatically.
//
// This replaces the previous OnPlayerEnteredRoom + grace-timer +
// PendingValidation design with a single synchronous check. No timer, no
// pending set, no race window: by Character.Start the answer is knowable.
[HarmonyPatch(typeof(Character), nameof(Character.Start))]
internal static class Patch_Character_Start_ValidateOwner
{
    [HarmonyPostfix]
    private static void Postfix(Character __instance)
    {
        if (__instance == null) return;
        // Local character: we're running the mod, so our owner trivially
        // has it. Skip the check (and the Owner lookup, which is brittle
        // during earliest spawn).
        if (Character.localCharacter != null && __instance == Character.localCharacter) return;

        if (!SpectatorState.TryGetOwner(__instance, out var owner)) return;
        if (SpectatorState.HasGhostSpectatorMod(owner)) return;

        // Owner doesn't have the mod -- as of THIS frame. Photon's
        // operation/instantiation ordering usually delivers the IsSpectator
        // property update before the Character.Instantiate event, but the
        // guarantee is best-effort across deliveries from different
        // senders. A late property arrival would cause us to false-positive
        // kick a legitimate moded peer with no recovery path. Defer the
        // hide-and-kick decision by one frame via a coroutine on the
        // Character itself (which is a MonoBehaviour and lives at least as
        // long as the not-yet-kicked owner). If the property arrives in
        // the deferral window, we never act.
        __instance.StartCoroutine(DeferredValidate(__instance, owner));
    }

    private static System.Collections.IEnumerator DeferredValidate(Character c, Photon.Realtime.Player owner)
    {
        // One frame is enough for any in-flight property update that was
        // sent on the wire before the instantiation event we just observed.
        // We also yield WaitForEndOfFrame to be sure we're past Photon's
        // dispatch tick within the frame.
        yield return null;
        yield return new UnityEngine.WaitForEndOfFrame();

        if (c == null || owner == null) yield break;
        if (SpectatorState.HasGhostSpectatorMod(owner))
        {
            Plugin.TraceDebug($"[trace] deferred validate: {owner.NickName} (#{owner.ActorNumber}) property arrived in deferral window, no action");
            yield break;
        }

        Plugin.Trace($"[trace] unmoded joiner confirmed at Character.Start+1frame: {owner.NickName} (#{owner.ActorNumber}); hiding visuals");
        SpectatorState.HideCharacterVisuals(c);

        if (PhotonNetwork.IsMasterClient)
        {
            Plugin.Trace($"[trace] kicking {owner.NickName} (#{owner.ActorNumber}): GhostSpectator mod not installed");
            SpectatorState.TryAddConnectionLogMessage($"{owner.NickName} was disconnected: GhostSpectator mod required");
            try
            {
                PlayerHandler.Kick(owner.ActorNumber);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[trace] PlayerHandler.Kick threw on {owner.NickName}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
