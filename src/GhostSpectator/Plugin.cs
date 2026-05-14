using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using GhostSpectator.Runtime;

namespace GhostSpectator;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> SpectatorEnabled { get; private set; } = null!;

    // BepInEx's disk log writer buffers runtime entries on Crossover/Wine and
    // only flushes on a clean shutdown, so a force-quit drops everything we
    // logged at runtime. Unity's Debug.Log writes to Player.log eagerly and
    // survives hard kills, so we mirror trace events through both channels.
    internal static void Trace(string msg)
    {
        Log.LogInfo(msg);
        UnityEngine.Debug.Log($"[GhostSpectator] {msg}");
    }

    internal static void TraceWarn(string msg)
    {
        Log.LogWarning(msg);
        UnityEngine.Debug.LogWarning($"[GhostSpectator] {msg}");
    }

    // For high-frequency events that are useful to debugging but noise for
    // end users (per-spawn Character.Start traces, per-join/leave callbacks,
    // per-blocked-RPC traces). End users with default BepInEx log levels
    // don't see LogDebug, so these stay quiet in production but remain
    // available in the BepInEx log when LogLevel is opened up. No Unity
    // Debug.Log mirror because the Crossover/Wine flush concern that drives
    // Trace's dual-channel design matters for important state transitions
    // (which keep Trace), not for routine event tracing.
    internal static void TraceDebug(string msg)
    {
        Log.LogDebug(msg);
    }

    private void Awake()
    {
        Log = Logger;

        SpectatorEnabled = Config.Bind(
            "Spectator",
            "Enabled",
            false,
            "Toggled in-game via the 'Ghost Spectator' button in the airport lobby. " +
            "When true, this player spawns into every match as a permanent ghost: dead from " +
            "the start, with the vanilla ghost camera and voice chat, and never revived by " +
            "Scout Effigies, Ancient Statues, or Respawn Chests. Other players are unaffected. " +
            "Don't edit this directly, use the in-game button so the change broadcasts to " +
            "your lobby properly.");

        // Self-verification: walk each [HarmonyPatch] class and confirm the target
        // method actually exists in the game assembly. Catches "method renamed",
        // "wrong class", "different parameter signature" classes of bug at plugin
        // load time, without needing a runtime test. Does NOT catch transpiler IL
        // errors, those still require an attach attempt.
        var validation = PatchValidator.Validate(Log.LogError);
        Log.LogInfo($"[validate] {validation.ok} targets resolved, {validation.missing} missing");

        var harmony = new Harmony(Id);
        int attached = 0, failed = 0;
        foreach (var type in typeof(Plugin).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0) continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                Log.LogInfo($"[trace] attached: {type.Name}");
                attached++;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"[trace] FAILED to attach {type.Name}: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Log.LogError($"[trace]   inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                failed++;
            }
        }
        Log.LogInfo($"[trace] patches attached: {attached}, failed: {failed}");
        if (attached == 0)
        {
            Log.LogError("GhostSpectator: no patches attached. Mod is non-functional.");
            return;
        }

        var handlerObj = new UnityEngine.GameObject("GhostSpectator.Runtime");
        UnityEngine.Object.DontDestroyOnLoad(handlerObj);
        handlerObj.AddComponent<RoomCallbackHandler>();
        handlerObj.AddComponent<SpectatorMenuUI>();
        handlerObj.AddComponent<MidRunJoinPopup>();

        Log.LogInfo($"{Name} {Version} loaded, spectator={SpectatorEnabled.Value}");
    }

}
