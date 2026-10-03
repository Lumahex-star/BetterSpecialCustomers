using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MelonLoader;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;
[HarmonyPatch(typeof(SpecialCustomerData), "GetBuyQuantityForPlayerRank")]
public class MaxBuyPatch
{
    public static void Postfix(ref int __result)
    {
        __result = __result * 2;
    }
}