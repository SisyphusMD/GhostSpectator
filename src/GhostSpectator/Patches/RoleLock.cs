using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Steamworks;
using UnityEngine;
using Zorro.Core;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace GhostSpectator.Patches;

// Per-run role lock. Records each player's role (live / spectator) at run
// start and prevents mid-run changes for the lifetime of that run. Three
// transport / consumer surfaces:
//
//   1. In-memory dict (RunRoles) on every moded client. Source of truth for
//      runtime trust checks (SpectatorState.TryGetLockedRole reads it).
//   2. Photon room custom property (RoomRunRolesKey) for in-room clients --
//      master writes, every moded client mirrors via OnRoomPropertiesUpdate.
//   3. Steam lobby data (same key) for pre-join reads by future joiners --
//      our HandleMessage prefix uses this to detect rejoiners and skip the
//      mid-run join popup before Photon JoinRoom completes.
//
// This file holds the dict, the (de)serializers, the master-side publisher,
// and the conflict-detection helper. The Photon callbacks that drive
// recording and synchronization live in RoomCallbackHandler (because they're
// MonoBehaviourPunCallbacks overrides); the snapshot at kiosk press lives
// in RunGating.OnRunStartedMaster (because it co-located with the gate);
// the rejoiner fast-path lives in MidRunJoinGate's wait coroutine (because
// it gates the popup display).
internal static class RoleLock
{
    // Shared key used by both Photon room custom property + Steam lobby
    // data. The latter is the pre-join read channel that lets joiners
    // detect rejoiner status before their Photon handshake completes.
    internal const string RoomRunRolesKey = "GhostSpectator.RunRoles";

    // userId -> IsSpectator role for the current run. Master is the only
    // writer; non-master clients update from OnRoomPropertiesUpdate. Cleared
    // on run start (kiosk gate) and seeded by master in OnRunStartedMaster.
    // A player whose userId appears here is "locked" -- their role can't be
    // changed mid-run by republishing IsSpectator or by leave+rejoin (the
    // popup-bypass at HandleMessage re-applies the stored value).
    internal static readonly Dictionary<string, bool> RunRoles = new Dictionary<string, bool>();

    // Serialize RunRoles for use as a Photon room custom property value.
    // Photon Hashtable natively handles string keys with bool values, so
    // no JSON / custom encoding is needed for the in-network channel.
    internal static Hashtable SerializeRunRolesToHashtable()
    {
        var ht = new Hashtable();
        lock (RunRoles)
        {
            foreach (var kvp in RunRoles)
            {
                ht[kvp.Key] = kvp.Value;
            }
        }
        return ht;
    }

    // Update RunRoles from a value received via OnRoomPropertiesUpdate or
    // read from CustomProperties at OnJoinedRoom time. Defensive against
    // unexpected types in case PEAK or another mod scribbles non-bool
    // entries over the same key.
    internal static void DeserializeRunRolesFromHashtable(object? raw)
    {
        lock (RunRoles)
        {
            RunRoles.Clear();
            if (raw is not Hashtable ht) return;
            foreach (var key in ht.Keys)
            {
                if (key is string userId && ht[key] is bool isSpec)
                {
                    RunRoles[userId] = isSpec;
                }
            }
        }
    }

    // Compact string encoding for Steam lobby data (Steam values are strings
    // only, ~8KB total per lobby). Format: "userId1:0;userId2:1;userId3:1".
    // Bool as 0/1, entries by ';', key/value by ':'. With our 20-player cap
    // and ~17-char Steam IDs, max payload is well under 1KB.
    internal static string SerializeRunRolesToString()
    {
        var sb = new System.Text.StringBuilder();
        lock (RunRoles)
        {
            bool first = true;
            foreach (var kvp in RunRoles)
            {
                if (!first) sb.Append(';');
                sb.Append(kvp.Key);
                sb.Append(':');
                sb.Append(kvp.Value ? '1' : '0');
                first = false;
            }
        }
        return sb.ToString();
    }

    // Inverse of SerializeRunRolesToString. Writes into a caller-provided
    // dict (not into RunRoles) so HandleMessage's prefix can parse without
    // touching shared state.
    internal static void DeserializeRunRolesFromString(string? s, Dictionary<string, bool> target)
    {
        target.Clear();
        if (string.IsNullOrEmpty(s)) return;
        var entries = s!.Split(';');
        foreach (var entry in entries)
        {
            var idx = entry.IndexOf(':');
            if (idx <= 0 || idx == entry.Length - 1) continue;
            var key = entry.Substring(0, idx);
            var val = entry.Substring(idx + 1);
            if (val == "1") target[key] = true;
            else if (val == "0") target[key] = false;
        }
    }

    // Master-only publish: mirrors RunRoles to both Photon room property
    // (the in-room source of truth) and Steam lobby data (the pre-join read
    // channel). Called whenever master mutates RunRoles -- kiosk-press
    // snapshot, first-time mid-run joiner recording, post-conflict
    // republish.
    internal static void PublishRunRolesToNetwork()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom == null) return;
        try
        {
            var roleHashtable = SerializeRunRolesToHashtable();
            var props = new Hashtable { { RoomRunRolesKey, roleHashtable } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[trace] PublishRunRolesToNetwork SetCustomProperties failed: {ex.GetType().Name}: {ex.Message}");
        }
        TryWriteRunRolesToSteamLobby();
    }

    // Write the encoded RunRoles into the Steam lobby's custom data.
    // Wrapped in try/catch because Steam APIs can fail for many reasons
    // (not in lobby yet, transient network) and a Steam-side write failure
    // should never block the Photon-side write (our actual source of
    // truth). Only the lobby owner can set lobby data; PEAK's design
    // aligns Photon master with Steam lobby owner so the IsMasterClient
    // guard in PublishRunRolesToNetwork is sufficient in practice.
    internal static void TryWriteRunRolesToSteamLobby()
    {
        try
        {
            var lobbyHandler = GameHandler.GetService<SteamLobbyHandler>();
            if (lobbyHandler == null) return;
            var steamId = lobbyHandler.LobbySteamId;
            if (steamId == CSteamID.Nil) return;
            var encoded = SerializeRunRolesToString();
            SteamMatchmaking.SetLobbyData(steamId, RoomRunRolesKey, encoded);
            Plugin.TraceDebug($"[trace] mirrored RunRoles to Steam lobby data ({encoded.Length} chars)");
        }
        catch (System.Exception ex)
        {
            Plugin.TraceDebug($"[trace] TryWriteRunRolesToSteamLobby failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // True when the inbound RunRoles hashtable has identical contents to
    // the in-memory copy. Master uses this in OnRoomPropertiesUpdate to
    // detect non-master writes (PUN doesn't restrict Room.SetCustomProperties
    // to master) -- a divergence triggers an authoritative republish that
    // overwrites the adversarial mutation. Master's own write-echo matches
    // and short-circuits to a no-op.
    internal static bool RunRolesHashtableMatchesInMemory(Hashtable? inbound)
    {
        if (inbound == null) return RunRoles.Count == 0;
        lock (RunRoles)
        {
            if (inbound.Count != RunRoles.Count) return false;
            foreach (var key in inbound.Keys)
            {
                if (key is not string userId) return false;
                if (!RunRoles.TryGetValue(userId, out var ourVal)) return false;
                if (inbound[key] is not bool theirVal) return false;
                if (theirVal != ourVal) return false;
            }
            return true;
        }
    }
}
