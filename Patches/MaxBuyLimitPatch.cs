using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
#if MONO
using ScheduleOne.Levelling;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// The game computes: min(baseBuyQuantity + additionalPerRank * rank, _maxBuyQuantity), with the cap at 120.
// Raising the cap itself (instead of multiplying the result) keeps the per-rank scaling for lower ranks
// and gives exactly NewMaxBuyQuantity at the top. The method is called from several places (arrival,
// daily update, arrival popup), so it runs more than once per arrival - that is normal.
[HarmonyPatch(typeof(SpecialCustomerData), nameof(SpecialCustomerData.GetBuyQuantityForPlayerRank))]
public class MaxBuyPatch
{
    public const int NewMaxBuyQuantity = 200;

    // Flip to true to log every call with its caller.
    private const bool DebugLogging = true;

#if MONO
    private static readonly FieldInfo MaxField = AccessTools.Field(typeof(SpecialCustomerData), "_maxBuyQuantity");
#elif IL2CPP
    private static readonly PropertyInfo MaxField = AccessTools.Property(typeof(SpecialCustomerData), "_maxBuyQuantity");
#endif

    public static void Prefix(SpecialCustomerData __instance)
    {
        MaxField.SetValue(__instance, NewMaxBuyQuantity);
    }

    public static void Postfix(SpecialCustomerData __instance, ERank rank, int __result)
    {
        if (!DebugLogging) return;
        string caller = new StackTrace().GetFrame(2)?.GetMethod()?.DeclaringType?.Name ?? "?";
        Melon<Core>.Logger.Msg($"[{__instance.GroupId}] rank {rank}: {__result} (caller: {caller})");
    }
}
