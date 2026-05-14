using System.Linq;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace GhostSpectator.Runtime;

// Broadcasts the local player's spectator status to other clients via a Photon
// custom property, and exposes a helper that counts non-spectator players across
// the current room. Used by the AirportCheckInKiosk.StartGame prefix to refuse
// runs that would have no live player to spectate.
internal class RoomCallbackHandler : MonoBehaviourPunCallbacks
{
    public const string IsSpectatorKey = "GhostSpectator.IsSpectator";

    private System.EventHandler? _settingChangedHandler;

    private void Awake()
    {
        // Plugin loads before any room exists, so a publish at Awake would
        // always log "skipped, not InRoom" -- noise on every boot. Wait for
        // OnJoinedRoom to do the first publish. The SettingChanged hook
        // covers any toggle the user makes once they're in a room. Field-
        // stored so OnDestroy can unsubscribe (prevents handler accumulation
        // across hot-reloads / scene-reloads that recreate the component).
        if (PhotonNetwork.InRoom)
        {
            PublishSpectatorStatus();
        }
        _settingChangedHandler = (_, _) => PublishSpectatorStatus();
        Plugin.SpectatorEnabled.SettingChanged += _settingChangedHandler;
    }

    private void OnDestroy()
    {
        if (_settingChangedHandler != null)
        {
            Plugin.SpectatorEnabled.SettingChanged -= _settingChangedHandler;
            _settingChangedHandler = null;
        }
    }

    public override void OnJoinedRoom()
    {
        Plugin.TraceDebug($"[trace] OnJoinedRoom fired. RoomName={PhotonNetwork.CurrentRoom?.Name}");
        // Rejoin: clear the local-publish flag so OnSyncReceived will accept
        // the master's broadcast of our entry until we've published our own
        // authoritative copy this session. See
        // Patch_PersistentPlayerDataService_OnSyncReceived.
        Patches.SpectatorState.HasPublishedLocalCustomizationData = false;
        // Clear any stale kiosk-refused banner state from a prior room/session.
        Patches.SpectatorState.KioskRefusedTimestamp = -1f;
        Patches.SpectatorState.KioskRefusedMessage = string.Empty;
        PublishSpectatorStatus();
        RebuildSortedPlayers();
        // Seed in-memory RunRoles from the room property (if any exists
        // from a run already in progress). For mid-run joiners this catches
        // up the local copy with whatever master has recorded so far.
        if (PhotonNetwork.CurrentRoom?.CustomProperties != null
            && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                Patches.RoleLock.RoomRunRolesKey, out var raw))
        {
            Patches.RoleLock.DeserializeRunRolesFromHashtable(raw);
            Plugin.TraceDebug($"[trace] RunRoles seeded from room property at join: {Patches.RoleLock.RunRoles.Count} entries");
        }
    }

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        Plugin.TraceDebug($"[trace] OnPlayerEnteredRoom fired for {newPlayer?.NickName}");
        PublishSpectatorStatus();
        RebuildSortedPlayers();
        // Mod-validation for mid-run joiners is handled synchronously by
        // Patch_Character_Start_ValidateOwner (in Patches/MidRunJoinGate.cs)
        // rather than a grace-timer here. By the time their Character spawns,
        // Photon's reliable channel guarantees their IsSpectator property
        // (if they have the mod) has already arrived. Absence = no mod,
        // detected and kicked synchronously.
    }

    public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, Hashtable changedProps)
    {
        // Per-run role lock: when a player publishes their IsSpectator
        // property mid-run, master records it to RunRoles (first time only)
        // and publishes the updated dict to all clients via room property +
        // Steam lobby data. Rejoiners (already in RunRoles) don't update
        // their stored role -- the popup-bypass at HandleMessage already
        // applied the locked value; any subsequent property change is just
        // the joiner echoing it back.
        if (!PhotonNetwork.IsMasterClient) return;
        if (targetPlayer == null || changedProps == null) return;
        if (!changedProps.ContainsKey(IsSpectatorKey)) return;
        // Don't record airport-time toggles. The lock fires on kiosk press;
        // anything happening before that is mutable lobby state.
        var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "Airport") return;
        // UserId may not be populated yet on the master's view of the player
        // (early-join race where the property arrives before UserId is set).
        // Retry on a short coroutine so we don't silently miss recording.
        if (string.IsNullOrEmpty(targetPlayer.UserId))
        {
            StartCoroutine(DeferredRecordRole(targetPlayer));
            return;
        }

        RecordRoleIfNew(targetPlayer);
    }

    private System.Collections.IEnumerator DeferredRecordRole(PhotonPlayer targetPlayer)
    {
        // Retry up to ~2s for UserId to populate. Each yield is one frame;
        // 120 frames is roughly 2 seconds at 60fps. If UserId never arrives
        // (Photon-side data corruption, no Steam auth), give up silently --
        // the player isn't locked, falls back to ClaimsSpectator everywhere.
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            yield return null;
            if (targetPlayer == null) yield break;
            if (!string.IsNullOrEmpty(targetPlayer.UserId))
            {
                if (PhotonNetwork.IsMasterClient) RecordRoleIfNew(targetPlayer);
                yield break;
            }
        }
        Plugin.TraceWarn($"[trace] DeferredRecordRole gave up after {maxFrames}f waiting for UserId on {targetPlayer?.NickName}");
    }

    private static void RecordRoleIfNew(PhotonPlayer targetPlayer)
    {
        if (string.IsNullOrEmpty(targetPlayer.UserId)) return;
        bool isSpec = Patches.SpectatorState.ClaimsSpectator(targetPlayer);
        bool changed = false;
        lock (Patches.RoleLock.RunRoles)
        {
            if (!Patches.RoleLock.RunRoles.ContainsKey(targetPlayer.UserId))
            {
                Patches.RoleLock.RunRoles[targetPlayer.UserId] = isSpec;
                changed = true;
                Plugin.Trace($"[trace] recorded run role for {targetPlayer.NickName} ({targetPlayer.UserId}): isSpec={isSpec}");
            }
        }
        if (changed) Patches.RoleLock.PublishRunRolesToNetwork();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // All moded clients sync their in-memory RunRoles from the room
        // property whenever it changes. Master is the writer, but master
        // also receives this callback when their own SetCustomProperties
        // completes -- the resulting deserialize-then-clear-then-repopulate
        // is a no-op for state but keeps the code symmetrical (no master/
        // non-master split in the read path).
        if (propertiesThatChanged == null) return;
        if (!propertiesThatChanged.ContainsKey(Patches.RoleLock.RoomRunRolesKey)) return;

        // Master-side conflict detection. PUN doesn't restrict
        // Room.SetCustomProperties to master, so a malicious non-master
        // could overwrite RunRoles with garbage. Master compares the
        // incoming payload against its own in-memory authoritative copy;
        // if they diverge, master rewrites the authoritative value, which
        // propagates back via another OnRoomPropertiesUpdate. The
        // dictionary equality check below short-circuits when the update
        // is master's own write echoing back, so this only fires on real
        // adversarial mutations.
        if (PhotonNetwork.IsMasterClient)
        {
            var inbound = propertiesThatChanged[Patches.RoleLock.RoomRunRolesKey] as Hashtable;
            if (!Patches.RoleLock.RunRolesHashtableMatchesInMemory(inbound))
            {
                Plugin.TraceWarn("[trace] non-master overwrote RunRoles room property; republishing authoritative value");
                Patches.RoleLock.PublishRunRolesToNetwork();
                return;
            }
        }

        Patches.RoleLock.DeserializeRunRolesFromHashtable(
            propertiesThatChanged[Patches.RoleLock.RoomRunRolesKey]);
        Plugin.TraceDebug($"[trace] RunRoles synced from room property: {Patches.RoleLock.RunRoles.Count} entries");
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        Plugin.TraceDebug($"[trace] OnPlayerLeftRoom fired for {otherPlayer?.NickName}");
        RebuildSortedPlayers();
    }

    public override void OnLeftRoom()
    {
        _sortedPlayers = _empty;
        _sortedPlayerLabels = _emptyLabels;
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        Plugin.TraceDebug($"[trace] OnMasterClientSwitched fired. newMaster=#{newMasterClient?.ActorNumber}");
        // Clear the EndGame one-shot guard on host migration. The old master
        // may have set RunHasEnded=true during the run that just transitioned;
        // the new master inherits that stale flag on its own
        // PlayerHandler.CharacterRegistered tick and would silently suppress
        // legitimate EndGame for the rest of the migrated run. Resetting on
        // the migration callback restores correct end-of-run behavior.
        Patches.SpectatorState.RunHasEnded = false;
        // Symmetric reset for the EndScreen banner one-shot guard. Normally
        // reset by Character.Start.Postfix on a fresh local respawn, but
        // host migration mid-run doesn't trigger that; leave the flag stale
        // and the banner would never re-roll for the next run after a
        // migration. Belt-and-suspenders mirroring the RunHasEnded reset.
        Patches.SpectatorState.BannersOverriddenThisRun = false;

        // If we just became master, take over Steam-lobby mirroring of the
        // RunRoles dict. The in-memory copy is already in sync with the
        // room property (we've been listening to OnRoomPropertiesUpdate all
        // along); the room property survived the master switch automatically.
        // What didn't survive is the previous master writing to Steam lobby
        // data -- pick that up here so future mid-run joiners can still
        // detect themselves as rejoiners via the pre-join Steam lobby read.
        if (PhotonNetwork.IsMasterClient)
        {
            Patches.RoleLock.TryWriteRunRolesToSteamLobby();
        }
    }

    // Cached, local-first, ActorNumber-sorted snapshot of the room's player
    // list. Rebuilt on every join/leave callback so SpectatorMenuUI.OnGUI can
    // read it without allocating a fresh LINQ-projected array every frame
    // (OnGUI fires ~120 Hz). Set to empty when not in a room.
    private static PhotonPlayer[] _sortedPlayers = new PhotonPlayer[0];
    public static PhotonPlayer[] SortedPlayers => _sortedPlayers;

    // Per-player pre-formatted label string ("NickName" or "NickName (you)"),
    // computed once per join/leave callback and parallel-indexed to
    // SortedPlayers. Saves a string interpolation per row per OnGUI frame.
    // NickName can in theory change mid-session via player properties; not
    // currently observed in PEAK and not handled here -- worst case the cache
    // is stale until the next join/leave callback.
    private static readonly string[] _emptyLabels = new string[0];
    private static string[] _sortedPlayerLabels = _emptyLabels;
    public static string[] SortedPlayerLabels => _sortedPlayerLabels;

    private static void RebuildSortedPlayers()
    {
        if (!PhotonNetwork.InRoom)
        {
            _sortedPlayers = _empty;
            _sortedPlayerLabels = _emptyLabels;
            return;
        }
        _sortedPlayers = PhotonNetwork.PlayerList
            .OrderByDescending(p => p.IsLocal)
            .ThenBy(p => p.ActorNumber)
            .ToArray();
        _sortedPlayerLabels = new string[_sortedPlayers.Length];
        for (int i = 0; i < _sortedPlayers.Length; i++)
        {
            var p = _sortedPlayers[i];
            string nick = string.IsNullOrEmpty(p?.NickName) ? "(unnamed)" : p!.NickName;
            _sortedPlayerLabels[i] = p != null && p.IsLocal ? $"{nick} (you)" : nick;
        }
    }

    public static void PublishSpectatorStatus()
    {
        if (!PhotonNetwork.InRoom)
        {
            Plugin.TraceDebug("[trace] PublishSpectatorStatus skipped, not InRoom");
            return;
        }
        var ht = new Hashtable { { IsSpectatorKey, Plugin.SpectatorEnabled.Value } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
        Plugin.TraceDebug($"[trace] published IsSpectator={Plugin.SpectatorEnabled.Value}");
    }

    // Counts players in the current room who are NOT claiming spectator status.
    // Property-only check (ClaimsSpectator): runs in the airport before any
    // Character has spawned, so there's no in-game state to corroborate
    // against. Treats both an explicit `false` and an absent property (e.g. a
    // player without the mod installed) as a non-spectator. Also used by
    // SpectatorMenuUI and the mid-run join popup to display "Live (n/4)" and
    // gate the [Play] button when live slots are full.
    public static int CountNonSpectators()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return 0;
        int count = 0;
        foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (Patches.SpectatorState.ClaimsSpectator(player)) continue;
            count++;
        }
        return count;
    }

    // Returns PhotonNetwork.PlayerList minus any trusted spectator. Used by
    // transpilers in per-player-count spawn sites (SingleItemSpawner, Spawner,
    // DestroyBasedOnPlayerCount) so marshmallows / hotdogs / backpacks etc.
    // spawn based on the count of LIVE players, not the raw room size --
    // otherwise a 4-player lobby with one spectator gets 4 marshmallows for 3
    // live mouths. Uses IsTrustedSpectator (which respects the per-run lock
    // when set, or property+state corroboration in airport) so a troll who
    // publishes IsSpectator=true while playing alive doesn't shrink the
    // team's item-spawn count.
    private static readonly PhotonPlayer[] _empty = new PhotonPlayer[0];
    public static PhotonPlayer[] GetNonSpectatorPlayerList()
    {
        if (!PhotonNetwork.InRoom) return _empty;
        var list = new System.Collections.Generic.List<PhotonPlayer>(PhotonNetwork.PlayerList.Length);
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player == null) continue;
            if (Patches.SpectatorState.IsTrustedSpectator(player)) continue;
            // Remote players without the mod are filtered out: on master
            // they're already being kicked at Character.Start; on non-master
            // clients they'll be gone the moment the kick disconnect
            // propagates. Excluding them keeps per-player item spawns from
            // briefly counting them as live during the sub-second window
            // before the disconnect arrives.
            if (!player.IsLocal && !Patches.SpectatorState.HasGhostSpectatorMod(player)) continue;
            list.Add(player);
        }
        return list.ToArray();
    }
}
