using HarmonyLib;

namespace GhostSpectator;

// Load-time self-check that every [HarmonyPatch] target method, field, and
// property our patches reach into still exists in the current PEAK assembly.
// Catches "renamed in this PEAK update" / "wrong declaring type" / "property
// became a method" classes of breakage without needing a runtime test --
// the missing-target lines in BepInEx log point at exactly what to look at.
//
// Does NOT catch transpiler IL errors, which only surface on patch attach.
//
// Callers supply a `logError` delegate so this can run both at plugin load
// (route to BepInEx Log.LogError) and from a standalone CLI preflight
// (route to Console.Error). Returns (ok, missing) counts; the trailing
// summary line is the caller's responsibility.
public static class PatchValidator
{
    public static (int ok, int missing) Validate(System.Action<string> logError)
    {
        int ok = 0, missing = 0;

        // Each [HarmonyPatch] target method must resolve via reflection.
        // Two dispatch styles:
        //   1. Attribute carries declaring type + method name -> resolve via AccessTools.Method.
        //   2. Attribute is empty, patch class defines static MethodBase TargetMethod()
        //      (e.g. for compiler-generated coroutine state machines) -> invoke and validate.
        foreach (var type in typeof(Plugin).Assembly.GetTypes())
        {
            var attrs = type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
            if (attrs.Length == 0) continue;
            var attr = (HarmonyPatch)attrs[0];
            var info = attr.info;

            if (info.declaringType == null || info.methodName == null)
            {
                var targetMethodFn = AccessTools.Method(type, "TargetMethod");
                if (targetMethodFn == null)
                {
                    logError($"[validate] {type.Name}: [HarmonyPatch] has no target info AND no TargetMethod() dispatcher");
                    missing++;
                    continue;
                }
                try
                {
                    var resolved = (System.Reflection.MethodBase)targetMethodFn.Invoke(null, null);
                    if (resolved != null)
                    {
                        ok++;
                    }
                    else
                    {
                        logError($"[validate] {type.Name}: TargetMethod() returned null");
                        missing++;
                    }
                }
                catch (System.Exception ex)
                {
                    logError($"[validate] {type.Name}: TargetMethod() threw {ex.GetType().Name}: {ex.Message}");
                    missing++;
                }
                continue;
            }

            // HarmonyX surfaces property getters/setters via MethodType.Getter
            // / MethodType.Setter on the patch attribute; in that case
            // info.methodName is the property name (e.g. "canPing") and the
            // underlying method to validate is `get_canPing` / `set_canPing`.
            // AccessTools.Method on a bare property name returns null, so we
            // dispatch to AccessTools.PropertyGetter / PropertySetter here so
            // these patches are not falsely flagged TARGET NOT FOUND.
            System.Reflection.MethodBase method;
            if (info.methodType == MethodType.Getter)
            {
                method = AccessTools.PropertyGetter(info.declaringType, info.methodName);
            }
            else if (info.methodType == MethodType.Setter)
            {
                method = AccessTools.PropertySetter(info.declaringType, info.methodName);
            }
            else
            {
                method = AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
            }
            if (method == null)
            {
                logError($"[validate] {type.Name}: TARGET NOT FOUND: {info.declaringType.FullName}.{info.methodName} (methodType={info.methodType})");
                missing++;
                continue;
            }
            ok++;
        }

        // Members the transpilers and patches directly read or write. Each is
        // checked against the kind it actually is in the game assembly (field
        // vs property) so we don't trigger HarmonyX's "Could not find field"
        // warning for things that are properties. If PEAK ever changes one of
        // these from a field to a property (or vice versa), we want validation
        // to flag it as missing rather than silently fall through.
        var fieldChecks = new (string label, System.Type type, string name)[]
        {
            ("Character.AllCharacters (static field)", typeof(Character), "AllCharacters"),
            ("Character.localCharacter (static field)", typeof(Character), "localCharacter"),
            ("Character.refs (field)", typeof(Character), "refs"),
            ("Character.CharacterRefs.ragdoll (field)", typeof(Character.CharacterRefs), "ragdoll"),
            ("CharacterAfflictions.character (field)", typeof(CharacterAfflictions), "character"),
            ("CharacterData.fullyPassedOut (field)", typeof(CharacterData), "fullyPassedOut"),
            ("CharacterData.deathTimer (field)", typeof(CharacterData), "deathTimer"),
            ("CharacterData.character (field)", typeof(CharacterData), "character"),
            ("GUIManager.dyingBarObject (field)", typeof(GUIManager), "dyingBarObject"),
            ("GUIManager.staminaCanvasGroup (field)", typeof(GUIManager), "staminaCanvasGroup"),
            ("GUIManager.mushroomsCanvasGroup (field)", typeof(GUIManager), "mushroomsCanvasGroup"),
            ("GUIManager.items (field)", typeof(GUIManager), "items"),
            ("GUIManager.backpack (field)", typeof(GUIManager), "backpack"),
            ("PointPinger.character (field)", typeof(PointPinger), "character"),
            ("PointPinger.pingInstance (field)", typeof(PointPinger), "pingInstance"),
            ("PointPinger.pointPrefab (field)", typeof(PointPinger), "pointPrefab"),
            ("PointPinger.coolDown (field)", typeof(PointPinger), "coolDown"),
            ("PointPinger._timeLastPinged (field)", typeof(PointPinger), "_timeLastPinged"),
        };
        foreach (var (label, type, name) in fieldChecks)
        {
            if (AccessTools.Field(type, name) != null) ok++;
            else { logError($"[validate] {label}: NOT FOUND"); missing++; }
        }

        var propertyChecks = new (string label, System.Type type, string name)[]
        {
            ("CharacterData.dead (property)", typeof(CharacterData), "dead"),
            ("CharacterData.canBeSpectated (property)", typeof(CharacterData), "canBeSpectated"),
            ("MainCameraMovement.specCharacter (static property)", typeof(MainCameraMovement), "specCharacter"),
            ("PhotonNetwork.PlayerList (property)", typeof(Photon.Pun.PhotonNetwork), "PlayerList"),
        };
        foreach (var (label, type, name) in propertyChecks)
        {
            if (AccessTools.Property(type, name) != null) ok++;
            else { logError($"[validate] {label}: NOT FOUND"); missing++; }
        }

        return (ok, missing);
    }
}
