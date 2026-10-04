using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Diagnostics;

/// <summary>
/// Temporary helpers for finding out WHY patches don't work (written for the IL2CPP build, harmless on Mono).
/// There are three separate things that can go wrong, and the log from this class tells them apart:
///   1. The game member we want to patch isn't there / has a different shape  -> "Probe" lines
///   2. Harmony fails to apply the patch                                      -> "Patch" lines (OK / FAILED)
///   3. The patch applies but the game never calls it (e.g. IL2CPP compiled
///      the tiny method inline so there is nothing to hook)                    -> a patch's "first call" line never appears
/// Search the log for "[diag]". Delete this file (and its .csproj line) when the problem is solved.
/// </summary>
public static class PatchDiagnostics
{
    public static readonly bool Enabled = true;

    private static readonly HashSet<string> Seen = new HashSet<string>();

    /// <summary>Call at the top of a patch. Logs once, the first time that patch actually runs.</summary>
    public static void Hit(string patchName)
    {
        if (!Enabled || !Seen.Add(patchName))
            return;
        Melon<Core>.Logger.Msg($"[diag] first call: {patchName} (the game DID call this patch)");
    }

    /// <summary>Applies every [HarmonyPatch] class in this mod ONE AT A TIME, so one failure can't stop the rest.</summary>
    public static void PatchAllClasses(HarmonyLib.Harmony harmony)
    {
        IEnumerable<Type> patchClasses = typeof(Core).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            .OrderBy(t => t.Name);

        int ok = 0, failed = 0;
        foreach (Type type in patchClasses)
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                ok++;
                if (Enabled)
                    Melon<Core>.Logger.Msg($"[diag] Patch OK:     {type.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                // Only the first line of the (long) Harmony exception chain is useful here; the innermost
                // exception says what actually went wrong.
                Exception inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;
                Melon<Core>.Logger.Error($"[diag] Patch FAILED: {type.Name} -> {inner.GetType().Name}: {inner.Message}");
            }
        }
        Melon<Core>.Logger.Msg($"[diag] Patches applied: {ok} ok, {failed} failed.");
    }

    /// <summary>Logs what the game types actually look like in this build (IL2CPP wrappers can differ from Mono).</summary>
    public static void Probe()
    {
        if (!Enabled)
            return;

#if IL2CPP
        const string flavor = "IL2CPP";
#else
        const string flavor = "MONO";
#endif
        Melon<Core>.Logger.Msg($"[diag] Build: {flavor}. Harmony {typeof(HarmonyLib.Harmony).Assembly.GetName().Version}");

        ProbeMethod(typeof(SpecialCustomerData), "GetBuyQuantityForPlayerRank");
        ProbeMethod(typeof(SpecialCustomerManager), "DealCompleted_Client");
        ProbeMethod(typeof(SpecialCustomerManager), "GetSaveString");
        ProbeMethod(typeof(SpecialCustomerManager), "RunArrivalPhase");
        ProbeMethod(typeof(SpecialCustomerManager), "RemoveCustomerGroup");
        ProbeMethod(typeof(SpecialCustomerLoader), "Load");

        // The arrival patch needs the manager's private "_currentData". On Mono that is a field; in the
        // IL2CPP wrapper it may be a property, or have a different name.
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        MemberInfo[] matches = typeof(SpecialCustomerManager).GetMembers(all)
            .Where(m => m.Name.IndexOf("currentData", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();
        if (matches.Length == 0)
            Melon<Core>.Logger.Warning("[diag] Probe: SpecialCustomerManager has NO member containing 'currentData'.");
        foreach (MemberInfo m in matches)
            Melon<Core>.Logger.Msg($"[diag] Probe: SpecialCustomerManager.{m.Name} is a {m.MemberType}");
    }

    private static void ProbeMethod(Type type, string name)
    {
        MethodBase method = AccessTools.Method(type, name);
        if (method == null)
            Melon<Core>.Logger.Warning($"[diag] Probe: {type.Name}.{name} NOT FOUND");
        else
            Melon<Core>.Logger.Msg($"[diag] Probe: {type.Name}.{name} found ({(method.IsPublic ? "public" : "non-public")})");
    }

    /// <summary>Logs which methods Harmony says it has patched.</summary>
    public static void ListPatchedMethods(HarmonyLib.Harmony harmony)
    {
        if (!Enabled)
            return;
        foreach (MethodBase m in harmony.GetPatchedMethods())
            Melon<Core>.Logger.Msg($"[diag] Harmony reports patched: {m.DeclaringType?.Name}.{m.Name}");
    }
}
