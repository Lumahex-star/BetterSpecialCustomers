using System;
using System.Diagnostics;
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

// The game computes min(base + perRank * rank, 120) with base = 20 and perRank = 10, so Kingpin (rank 10)
// lands exactly on 120 and raising only the cap changes nothing. Instead, replace the curve with a straight
// line from the group's base quantity at Street Rat to NewMaxBuyQuantity at Kingpin.
// The method is called from several places (arrival, daily update, arrival popup), so it runs more than
// once per arrival - that is normal.
[HarmonyPatch(typeof(SpecialCustomerData), nameof(SpecialCustomerData.GetBuyQuantityForPlayerRank))]
public class MaxBuyPatch
{
    public const int NewMaxBuyQuantity = 200;

    // Flip to true to log every call with its caller.
    private const bool DebugLogging = false;

    public static bool Prefix(SpecialCustomerData __instance, ERank rank, ref int __result)
    {
        int baseQuantity = __instance.BaseBuyQuantity;
        int topRank = (int)ERank.Kingpin;
        int clampedRank = Math.Max(0, Math.Min((int)rank, topRank));
        __result = baseQuantity + (NewMaxBuyQuantity - baseQuantity) * clampedRank / topRank;
        return false; // skip the original
    }

    // Priority.Last makes this postfix run after the other postfixes (e.g. the relationship multiplier),
    // so the log shows the FINAL quantity the game receives.
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(SpecialCustomerData __instance, ERank rank, int __result)
    {
        if (!DebugLogging) return;
        string caller = new StackTrace().GetFrame(2)?.GetMethod()?.DeclaringType?.Name ?? "?";
        Melon<Core>.Logger.Msg($"[{__instance.GroupId}] rank {rank}: {__result} (relationship x{Relationships.GroupRelationships.GetMultiplier(__instance.GroupId):0.00}) (caller: {caller})");
    }
}
