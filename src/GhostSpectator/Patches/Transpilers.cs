using System.Collections.Generic;
using HarmonyLib;

namespace GhostSpectator.Patches;

// All patches in this file are tiny shells over the shared transpiler factories
// in SpectatorState (ReplaceAllCharactersTranspiler / ReplacePlayerListTranspiler).
// Grouped here because each is a one-liner with no per-patch logic of its own.

[HarmonyPatch(typeof(Character), nameof(Character.PlayerIsDeadOrDown))]
internal static class Patch_Character_PlayerIsDeadOrDown
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}

[HarmonyPatch(typeof(ScoutEffigy), nameof(ScoutEffigy.FinishConstruction))]
internal static class Patch_ScoutEffigy_FinishConstruction
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}

[HarmonyPatch(typeof(RespawnChest), "RespawnAllPlayersHere")]
internal static class Patch_RespawnChest_RespawnAllPlayersHere
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}

// Filter spectators out of Fog.PlayersHaveMovedOn's iteration over
// Character.AllCharacters. Vanilla requires *every* character to be above
// (StopHeight + threshold) before the fog can advance early; a permanent
// spectator stuck at the beach spawn would otherwise block the early-advance
// for the entire run. Fog still advances on its TimeToMove() timer either
// way, but filtering restores responsive vanilla pacing once all live
// players have actually climbed past.
[HarmonyPatch(typeof(Fog), "PlayersHaveMovedOn")]
internal static class Patch_Fog_PlayersHaveMovedOn
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}

// Filter spectators out of MovingLava.PlayersHaveMovedOn's iteration. Vanilla
// returns true (lava rises) as soon as ANY character is above y=879. A
// permanent spectator at the beach spawn is well below 879 and doesn't
// trigger early lava on its own, but if a future change ever warps the
// spectator's body upward (or a vanilla-dead ghost ends up high), this
// filter prevents a non-climber from triggering the lava ascent. Symmetric
// with the Fog filter and cheap.
[HarmonyPatch(typeof(MovingLava), "PlayersHaveMovedOn")]
internal static class Patch_MovingLava_PlayersHaveMovedOn
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}

[HarmonyPatch(typeof(SingleItemSpawner), nameof(SingleItemSpawner.TrySpawnItems))]
internal static class Patch_SingleItemSpawner_TrySpawnItems
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplacePlayerListTranspiler(instructions);
}

[HarmonyPatch(typeof(Spawner), nameof(Spawner.TrySpawnItems))]
internal static class Patch_Spawner_TrySpawnItems
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplacePlayerListTranspiler(instructions);
}

// DestroyBasedOnPlayerCount.Start is a Unity coroutine (returns IEnumerator),
// so the PhotonNetwork.PlayerList.Length check lives in the compiler-generated
// state machine's MoveNext, not the outer wrapper. Patching the outer hits
// "IL Compile Error" at JIT and torpedoes the whole PatchAll. We use a
// TargetMethod() that reaches into the inner state machine class (named
// `<Start>d__<n>` by the compiler) and applies the existing PlayerList
// transpiler to its MoveNext.
[HarmonyPatch]
internal static class Patch_DestroyBasedOnPlayerCount_StateMachine
{
    static System.Reflection.MethodBase TargetMethod() =>
        SpectatorState.GetCoroutineMoveNext(
            typeof(DestroyBasedOnPlayerCount), "<Start>",
            nameof(Patch_DestroyBasedOnPlayerCount_StateMachine))!;

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplacePlayerListTranspiler(instructions);
}

// EndScreen.EndSequenceRoutine is a coroutine; its `Character.AllCharacters`
// reads live in the compiler-generated state machine's MoveNext (named
// `<EndSequenceRoutine>d__<n>`). Filtering AllCharacters here hides
// spectators from the post-run Scouting Report -- only players who actually
// climbed get a scout window and a timeline column. The downstream
// `TimelineRoutine(activeCharacters)` already takes the filtered list as a
// parameter, so no separate patch is needed there. RPCEndGame's
// AllCharacters iteration (which calls Win()/Lose() on each character) is
// intentionally NOT filtered: the spectator should still get Lose() so
// their local stats.lost flag is set consistently.
[HarmonyPatch]
internal static class Patch_EndScreen_EndSequenceRoutine_StateMachine
{
    static System.Reflection.MethodBase TargetMethod() =>
        SpectatorState.GetCoroutineMoveNext(
            typeof(EndScreen), "<EndSequenceRoutine>",
            nameof(Patch_EndScreen_EndSequenceRoutine_StateMachine))!;

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        SpectatorState.ReplaceAllCharactersTranspiler(instructions);
}
