using System;
using HarmonyLib;
using BetterSpecialCustomers.Diagnostics;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// final quantity = (rank-based quantity) x (relationship multiplier)
//
// MaxBuyPatch (in MaxBuyLimitPatch.cs) produces the rank-based number. This postfix runs afterwards and
// scales it (using the relationship that was snapshotted when the group arrived). A postfix receives the method's return value as "ref int __result" and may change it.
// Both patches target the same method; Harmony runs every prefix, the original, then every postfix.
[HarmonyPatch(typeof(SpecialCustomerData), nameof(SpecialCustomerData.GetBuyQuantityForPlayerRank))]
public class RelationshipBuyQuantityPatch
{
    public static void Postfix(SpecialCustomerData __instance, ref int __result)
    {
        PatchDiagnostics.Hit("RelationshipBuyQuantityPatch (relationship multiplier)");
        float multiplier = GroupRelationships.GetMultiplier(__instance.GroupId);
        __result = Math.Max(1, (int)Math.Round(__result * multiplier));
    }
}
