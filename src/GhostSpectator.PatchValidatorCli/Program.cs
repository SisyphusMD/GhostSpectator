using System.Reflection;

namespace GhostSpectator.Tools;

internal sealed class Program
{
    private const int PeakAppId = 3527290;

    static int Main(string[] args)
    {
        string? managedDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--managed-dir" && i + 1 < args.Length)
            {
                managedDir = args[++i];
            }
            else if (args[i] == "-h" || args[i] == "--help")
            {
                PrintUsage();
                return 0;
            }
        }

        managedDir ??= Environment.GetEnvironmentVariable("ManagedDir");

        if (string.IsNullOrWhiteSpace(managedDir) || !Directory.Exists(managedDir))
        {
            Console.Error.WriteLine("error: --managed-dir not provided or path does not exist");
            PrintUsage();
            return 2;
        }

        // Resolve TypeRefs from the plugin DLL by probing two locations:
        //   1. The game's Managed/ folder -- supplies Character, BaseUnityPlugin
        //      (BepInEx is provided by the game's BepInEx install), and the
        //      UnityEngine modules.
        //   2. The NuGet global packages cache -- supplies 0Harmony.dll and
        //      similar build-time-only refs that aren't in the CLI's bin/
        //      because their packages mark runtime assets compile-only for
        //      mod builds (the game provides them at runtime in production).
        // Keeps proprietary game DLLs out of the CLI output entirely.
        var capturedManagedDir = managedDir;
        var nugetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            var name = new AssemblyName(eventArgs.Name).Name;
            if (string.IsNullOrEmpty(name)) return null;

            var managedCandidate = Path.Combine(capturedManagedDir, name + ".dll");
            if (File.Exists(managedCandidate)) return Assembly.LoadFrom(managedCandidate);

            if (Directory.Exists(nugetCache))
            {
                // Walk all NuGet packages looking for lib/<tfm>/<name>.dll.
                // Can't assume package-name == assembly-name (e.g. HarmonyX
                // package contains 0Harmony.dll). Prefers netstandard2.x first
                // since that's what mod-targeted packages ship.
                var preferredTfms = new[] { "netstandard2.1", "netstandard2.0", "net6.0", "net8.0", "net10.0", "net472", "net45", "net35" };
                foreach (var pkgDir in Directory.EnumerateDirectories(nugetCache))
                {
                    foreach (var verDir in Directory.EnumerateDirectories(pkgDir))
                    {
                        var lib = Path.Combine(verDir, "lib");
                        if (!Directory.Exists(lib)) continue;
                        foreach (var tfm in preferredTfms)
                        {
                            var dll = Path.Combine(lib, tfm, name + ".dll");
                            if (File.Exists(dll)) return Assembly.LoadFrom(dll);
                        }
                    }
                }
            }
            return null;
        };

        var buildId = TryGetPeakBuildId(managedDir);
        Console.WriteLine($"PEAK buildid: {buildId ?? "<unknown>"}");
        Console.WriteLine($"Managed dir: {managedDir}");

        var (ok, missing) = PatchValidator.Validate(msg => Console.Error.WriteLine(msg));
        Console.WriteLine($"[validate] {ok} targets resolved, {missing} missing");
        return missing > 0 ? 1 : 0;
    }

    // PEAK install layout: <steamapps>/common/PEAK/PEAK_Data/Managed/
    // The Steam appmanifest is at <steamapps>/appmanifest_<appid>.acf and
    // contains a "buildid" line — the definitive identifier for which exact
    // build the maintainer has installed.
    private static string? TryGetPeakBuildId(string managedDir)
    {
        try
        {
            var dir = new DirectoryInfo(managedDir);
            // Need ~5 hops on a typical Linux/Windows Steam layout
            // (Managed/PEAK_Data/PEAK/common/steamapps) and one extra for
            // sandboxed installs like CrossOver bottles. Cap at 8 as a fence.
            for (int hop = 0; hop < 8 && dir != null; hop++)
            {
                var candidate = Path.Combine(dir.FullName, $"appmanifest_{PeakAppId}.acf");
                if (File.Exists(candidate))
                {
                    foreach (var line in File.ReadAllLines(candidate))
                    {
                        var trimmed = line.TrimStart();
                        if (!trimmed.StartsWith("\"buildid\"", StringComparison.Ordinal)) continue;
                        // Format: "buildid"		"18234567"
                        var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3) return parts[^1].Trim();
                    }
                }
                dir = dir.Parent;
            }
        }
        catch
        {
            // best-effort -- a missing or unparseable appmanifest just means
            // the release script can't auto-fill the buildid and the maintainer
            // sets it manually.
        }
        return null;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: GhostSpectator.PatchValidatorCli --managed-dir <path>");
        Console.Error.WriteLine("       (or set ManagedDir environment variable)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Validates that every [HarmonyPatch] target in the GhostSpectator");
        Console.Error.WriteLine("plugin still resolves against the game DLLs at <managed-dir>.");
        Console.Error.WriteLine("Exit code 0 = all targets resolved; 1 = one or more missing.");
    }
}
