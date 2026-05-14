using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using GhostSpectator.Runtime;

namespace GhostSpectator.Patches;

// Shared state + helper functions used by the patch classes in this folder.
// No [HarmonyPatch] marker on this type so Harmony's scanner ignores it.
// Each patch lives in its own concern-scoped file; this hub holds the pieces
// they all need to share (death flags, captured spawn anchor, EndGame guard,
// the IsSpectator query family, and the shared transpiler factories).
internal static class SpectatorState
{
    // Vanilla PEAK caps Photon rooms at 4 players (NetworkingUtilities.MAX_PLAYERS).
    // We raise the Photon room cap to LiveCap + SpectatorCap so a full live team
    // can be joined by additional spectators. The live cap is enforced
    // client-side by the mid-run join popup and airport UI (Photon itself only
    // knows about the combined total, since MaxPlayers can't distinguish player
    // categories). Hard-coded for v0.2; could become configurable later.
    internal const int LiveCap = 4;
    internal const int SpectatorCap = 16;
    internal const int TotalRoomCap = LiveCap + SpectatorCap;

    internal static bool IsLocalSpectator(Character character)
    {
        return Plugin.SpectatorEnabled.Value
            && Character.localCharacter != null
            && character == Character.localCharacter;
    }

    // Captured by Patch_Character_Start.Postfix on the island, BEFORE flipping
    // data.dead=true. Vanilla Character.FixedUpdate would have populated
    // Character.LastLivingPosition with the spawn point on its first not-dead
    // tick, but we set dead=true synchronously in our postfix, so the
    // FixedUpdate else-branch never fires and LastLivingPosition stays at
    // Vector3.zero. We capture transform.position ourselves and the camera
    // fallback reads from here as a last-resort anchor.
    internal static Vector3 SpectatorSpawnPosition = Vector3.zero;
    internal static bool SpectatorSpawnPositionValid = false;

    // Snapshot of the spec camera's transform from the most recent frame in
    // which vanilla's Spectate picked a real (non-spectator) target. Used by
    // the camera-fallback patch to hold position when the live target later
    // disappears (e.g. host left the room mid-run) instead of warping to
    // the spectator's own spawn anchor. Reset in Character.Start postfix
    // for the local character (new run = no remembered anchor yet).
    internal static Vector3 LastSpectateCameraPosition = Vector3.zero;
    internal static Quaternion LastSpectateCameraRotation = Quaternion.identity;
    internal static bool LastSpectateCameraValid = false;

    // Stamped by the AirportCheckInKiosk.StartGame prefix when it refuses a
    // run. SpectatorMenuUI.OnGUI reads timestamp + message to draw a brief
    // on-screen banner so the player who pressed the kiosk knows why nothing
    // happened. -1 means never refused this session. The message field is
    // populated alongside the timestamp because there's more than one reason
    // we can refuse (all spectators, or one or more players missing the mod).
    internal static float KioskRefusedTimestamp = -1f;
    internal static string KioskRefusedMessage = string.Empty;
    internal const float KioskRefusedDisplayDuration = 5f;

    internal static void RefuseKiosk(string message)
    {
        KioskRefusedTimestamp = UnityEngine.Time.realtimeSinceStartup;
        KioskRefusedMessage = message;
    }

    // Photon custom-property key on a ghost's Player carrying their current
    // Camera.main position. Published by the DoPing prefix immediately before
    // the ping RPC, read by ReceivePoint_Rpc on remote clients to compute
    // ping opacity from where the ghost is actually looking from instead of
    // from their DeathPos-teleported body.
    internal const string GhostPingerCameraKey = "GhostSpec.PingerCamera";

    // One-shot guard against PEAK's Character.EndGame firing multiple times in
    // the same run. Each invocation RPCs RPCEndGame to all clients, which
    // re-opens the EndScreen and re-starts EndSequenceRoutine -- but the prior
    // EndSequenceRoutine was killed mid-way through TimelineRoutine /
    // AscentRoutine / BadgeRoutine by the close + reopen, leaving
    // `buttons.SetActive(true)` (the line that activates the Next button)
    // never reached. PEAK calls EndGame from each character's RPCA_Die's
    // CheckEndGame chain, and our all-spectators flow triggers EndGame more
    // than once (once per death). Reset on Character.Start postfix for the
    // local player, on AirportCheckInKiosk.StartGame, and on host migration
    // (RoomCallbackHandler.OnMasterClientSwitched).
    internal static bool RunHasEnded = false;

    // Flips true the first time the local client publishes its own
    // PersistentPlayerData (via PersistentPlayerDataService.SetPlayerData with
    // the local Player). Reset to false on every OnJoinedRoom so a rejoin
    // starts from scratch. Used by Patch_PersistentPlayerDataService_OnSyncReceived
    // to decide whether to accept or drop a self-targeted sync: before we've
    // published locally we have no authoritative data and the network is our
    // only source, so accept; after we've published, the local copy is
    // authoritative and any self-targeted sync is either a redundant echo or
    // the master's rejoin-race echo of a stale zero entry. Drop either way.
    internal static bool HasPublishedLocalCustomizationData = false;

    // One-shot guard for the EndScreen banner-text override. The postfix on
    // EndSequenceRoutine's MoveNext fires repeatedly during the coroutine's
    // life and we only want to swap text once per run (Random.Range picks
    // would otherwise rotate every frame). Reset in Character.Start postfix
    // for the local player so the next run gets a fresh random pick.
    internal static bool BannersOverriddenThisRun = false;

    // True when the character is in any ghost state -- our permanent
    // spectator OR a vanilla mid-run dead-and-not-revived player. Both
    // PEAK's death state machine flags get the same treatment downstream
    // (camera, voice, ping visibility, can't-be-revived checks).
    internal static bool IsGhost(Character c)
    {
        if (c == null) return false;
        var data = c.data;
        return data != null && (data.dead || data.fullyPassedOut);
    }

    // Vector finiteness check. Lives here (outside any [HarmonyPatch] class)
    // because Harmony's analyzer mistakenly flags `v.x` reads inside a patch
    // class as patch-parameter modifications (Harmony003 false-positive when
    // the helper's parameter happens to be named `v`).
    internal static bool IsFiniteVec(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
        && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

    // Resolve a Character's Photon owner with full null-walk + NRE guard.
    // The photonView reference can be momentarily missing during the earliest
    // spawn frames (Awake before Start), and Owner can briefly be null mid-
    // destroy. Multiple patches in the codebase did this dance with subtle
    // variations (try/catch vs ?., ternary vs explicit checks); funnel here.
    // [NotNullWhen(true)] lets callers use owner without `!` suppression
    // when this method has returned true.
    internal static bool TryGetOwner(Character? character, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Photon.Realtime.Player? owner)
    {
        owner = null;
        if (character == null) return false;
        try
        {
            var view = character.photonView;
            if (view == null) return false;
            owner = view.Owner;
            return owner != null;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
    }

    // Locate a Roslyn-emitted coroutine state-machine MoveNext for a Harmony
    // TargetMethod() that needs to patch IL inside an IEnumerator-returning
    // method. Returns null with an error log if the inner type can't be found
    // (PEAK rename, compiler change). Caller should propagate the null --
    // Harmony will skip the patch with a clear error rather than throw
    // ArgumentNullException inside AccessTools.Method.
    internal static System.Reflection.MethodBase? GetCoroutineMoveNext(System.Type outer, string namePrefix, string patchLabel)
    {
        var stateMachine = AccessTools.FirstInner(outer, t => t.Name.Contains(namePrefix));
        if (stateMachine == null)
        {
            Plugin.Log.LogError($"[trace] {patchLabel}: '{namePrefix}' state machine type not found on {outer.Name}, skipping patch");
            return null;
        }
        return AccessTools.Method(stateMachine, "MoveNext");
    }

    // Property-only check: does the player claim to be a spectator by publishing
    // GhostSpectator.IsSpectator=true on their Photon custom properties? Cheap
    // (no character lookup, no state corroboration). Used in airport-time and
    // display-only paths where we can't verify the claim against in-game state
    // (e.g. the kiosk veto, the SpectatorMenuUI row coloring).
    internal static bool ClaimsSpectator(Photon.Realtime.Player player)
    {
        if (player == null) return false;
        try
        {
            return player.CustomProperties != null
                && player.CustomProperties.TryGetValue(RoomCallbackHandler.IsSpectatorKey, out var raw)
                && raw is bool b && b;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
    }

    // Does the player have GhostSpectator installed? Distinguished from
    // ClaimsSpectator by checking presence of the property (with any bool
    // value) rather than its specific true value. PublishSpectatorStatus
    // writes the property on OnJoinedRoom for any client running this mod, so
    // the property's mere presence is our signal of installation. Used by
    // both the kiosk gate (refuse run start if any player lacks the mod) and
    // the mid-run join validator (Patch_Character_Start_ValidateOwner, which
    // hides + kicks unmoded joiners detected at their Character spawn).
    internal static bool HasGhostSpectatorMod(Photon.Realtime.Player player)
    {
        if (player == null) return false;
        try
        {
            return player.CustomProperties != null
                && player.CustomProperties.TryGetValue(RoomCallbackHandler.IsSpectatorKey, out var raw)
                && raw is bool;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
    }

    // Disable every visible renderer + UI Graphic on a Character so an unmoded
    // mid-run joiner's body never paints to screen between Character.Start
    // detection and the master-issued kick taking effect (typically <500ms).
    // We don't SetActive(false) the GameObject because the Character is still
    // a live Photon entity until the kick disconnect propagates, and vanilla
    // game logic may assume newly-spawned characters are active. Disabling
    // visuals is the surgical hide. Renderers are not re-enabled because the
    // GameObject is destroyed shortly after by Photon's owner-disconnect
    // cleanup -- if the character were to somehow persist, it would stay
    // invisible, which is still the safer failure mode.
    internal static void HideCharacterVisuals(Character c)
    {
        if (c == null) return;
        try
        {
            var renderers = c.GetComponentsInChildren<UnityEngine.Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }
            var graphics = c.GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].enabled = false;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.TraceDebug($"[trace] HideCharacterVisuals failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // PEAK's PlayerConnectionLog.AddMessage is private, so we invoke via
    // reflection (cached MethodInfo via AccessTools). Wrapped in try/catch:
    // a PEAK update could rename the method, and a logging failure should
    // never block the surrounding action (e.g. a kick still fires even if
    // we can't post a chat line). Instance lookup uses FindAnyObjectByType
    // because PEAK doesn't expose a static singleton -- there's exactly one
    // PlayerConnectionLog on the HUD in-run.
    internal static void TryAddConnectionLogMessage(string message)
    {
        try
        {
            var log = UnityEngine.Object.FindAnyObjectByType<PlayerConnectionLog>();
            if (log == null) return;
            var addMessage = AccessTools.Method(typeof(PlayerConnectionLog), "AddMessage");
            if (addMessage == null) return;
            addMessage.Invoke(log, new object[] { message });
        }
        catch (System.Exception ex)
        {
            Plugin.TraceDebug($"[trace] TryAddConnectionLogMessage failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Property AND in-game-state corroborated check. A real spectator (ours or
    // a vanilla mid-run dead-and-not-revived player) always satisfies IsGhost.
    // A player who claims spectator status but whose character is alive is
    // either a troll spoofing the property to shrink item-spawn counts /
    // revive-candidate pools, or a brief state-race (just toggled, character
    // not yet flipped). Either way, treat them as a non-spectator until
    // in-game state corroborates the claim. Used by transpiler-included
    // candidate-pool filters and the in-run camera / RPC blocker patches.
    // Per-run lock lookup. Returns true if the player's userId is in
    // RoleLock.RunRoles (= they were captured at run-start snapshot or
    // recorded on their first mid-run publish). When locked, the stored
    // value is authoritative: a malicious or buggy client can flip
    // Plugin.SpectatorEnabled and republish IsSpectator with the opposite
    // value, but the lock-enforced checks below ignore the live property.
    // Only callers BEFORE the run starts (RunRoles empty) fall back to
    // ClaimsSpectator.
    private static bool TryGetLockedRole(Photon.Realtime.Player player, out bool isSpectator)
    {
        isSpectator = false;
        if (player == null || string.IsNullOrEmpty(player.UserId)) return false;
        lock (RoleLock.RunRoles)
        {
            return RoleLock.RunRoles.TryGetValue(player.UserId, out isSpectator);
        }
    }

    internal static bool IsTrustedSpectator(Character character)
    {
        if (character == null) return false;
        if (IsLocalSpectator(character)) return true;
        if (!TryGetOwner(character, out var owner)) return false;
        // Locked role takes precedence over live property. Closes the
        // mid-run "republish IsSpectator to bypass the lock" attack.
        if (TryGetLockedRole(owner, out var lockedIsSpec)) return lockedIsSpec;
        // Pre-run (airport) or brand-new joiner not yet recorded: fall
        // back to property + state-corroborated check.
        if (!ClaimsSpectator(owner)) return false;
        return IsGhost(character);
    }

    // Player-keyed overload for the item-spawn transpiler paths
    // (RoomCallbackHandler.GetNonSpectatorPlayerList). When locked, the
    // RunRoles value is authoritative. When not locked (pre-run), looks up
    // the player's character in Character.AllCharacters by ActorNumber and
    // applies the corroborated check. If no character is found (player in
    // room but their character hasn't spawned this scene yet) we trust the
    // claim -- the race window is small and the safe default during it is
    // "exclude from count."
    internal static bool IsTrustedSpectator(Photon.Realtime.Player player)
    {
        if (player == null) return false;
        if (TryGetLockedRole(player, out var lockedIsSpec)) return lockedIsSpec;
        if (!ClaimsSpectator(player)) return false;
        var all = Character.AllCharacters;
        if (all == null) return true;
        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i];
            if (c == null) continue;
            var view = c.photonView;
            if (view != null && view.Owner != null && view.Owner.ActorNumber == player.ActorNumber)
            {
                return IsGhost(c);
            }
        }
        return true;
    }

    // Borrow first/final timelineInfo from any non-spectator character that
    // has real entries. Used by the GetFirst/GetFinalTimelineInfo patches
    // when the local (spectator) character's own timeline is empty, so
    // EndScreen.TimelineRoutine computes a real time axis for everyone
    // else's pip placement. Returns false if no climber has data yet.
    internal static bool TryBorrowTimelineInfo(bool first, out EndScreen.TimelineInfo result)
    {
        foreach (var c in Character.AllCharacters)
        {
            if (c == null || IsTrustedSpectator(c)) continue;
            var stats = c.refs?.stats;
            if (stats == null || stats.timelineInfo == null || stats.timelineInfo.Count == 0) continue;
            result = first ? stats.timelineInfo[0] : stats.timelineInfo[stats.timelineInfo.Count - 1];
            return true;
        }
        result = default;
        return false;
    }

    // Sync guard used by EndGame and OrbFogHandler patches: when
    // Character.AllCharacters.Count < PhotonNetwork.CurrentRoom.PlayerCount,
    // remote characters haven't synced over Photon yet and we must suppress
    // run-advancing decisions (premature EndGame, premature fog advance)
    // until everyone is present. Returns true when all expected characters
    // are spawned (or when there are no expected players). Emits the counts
    // via out params so callers can log them in their trace messages.
    internal static bool AllExpectedCharactersSpawned(out int expectedPlayers, out int actualCharacters)
    {
        var room = PhotonNetwork.CurrentRoom;
        expectedPlayers = room != null ? room.PlayerCount : 0;
        actualCharacters = Character.AllCharacters != null ? Character.AllCharacters.Count : 0;
        return expectedPlayers <= 0 || actualCharacters >= expectedPlayers;
    }

    internal static List<Character> GetCharactersIgnoringSpectator()
    {
        var filtered = new List<Character>(Character.AllCharacters.Count);
        foreach (var c in Character.AllCharacters)
        {
            if (c == null) continue;
            if (IsTrustedSpectator(c)) continue;
            // Exclude characters whose owner doesn't have the mod installed.
            // On master these represent unmoded joiners in the brief window
            // between Character.Start (where we detect + queue the kick) and
            // the disconnect taking effect. On non-master moded clients
            // they're the same window before the disconnect propagates. We
            // skip the local character because our own owner always has the
            // mod (it's us).
            if (Character.localCharacter != c
                && TryGetOwner(c, out var owner)
                && !HasGhostSpectatorMod(owner)) continue;
            filtered.Add(c);
        }
        return filtered;
    }

    // Mutate the matching instruction in place rather than yielding a fresh one.
    // CodeInstruction carries `labels` (branch targets) and `blocks` (try/catch
    // boundaries); a new CodeInstruction has neither, so swapping it in drops
    // any inbound branches that targeted the original op, producing
    // "IL Compile Error" at JIT time.
    internal static IEnumerable<CodeInstruction> ReplaceAllCharactersTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var allCharactersField = AccessTools.Field(typeof(Character), nameof(Character.AllCharacters));
        var helperMethod = AccessTools.Method(
            typeof(SpectatorState), nameof(GetCharactersIgnoringSpectator));

        foreach (var instr in instructions)
        {
            if (instr.LoadsField(allCharactersField))
            {
                instr.opcode = OpCodes.Call;
                instr.operand = helperMethod;
            }
            yield return instr;
        }
    }

    internal static IEnumerable<CodeInstruction> ReplacePlayerListTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var playerListGetter = AccessTools.PropertyGetter(typeof(PhotonNetwork), nameof(PhotonNetwork.PlayerList));
        var helperMethod = AccessTools.Method(
            typeof(RoomCallbackHandler), nameof(RoomCallbackHandler.GetNonSpectatorPlayerList));

        foreach (var instr in instructions)
        {
            if (instr.Calls(playerListGetter))
            {
                instr.opcode = OpCodes.Call;
                instr.operand = helperMethod;
            }
            yield return instr;
        }
    }
}
