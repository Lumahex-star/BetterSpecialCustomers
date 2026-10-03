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

// SpecialCustomerData.GetBuyQuantityForPlayerRank is a pure getter that the game calls from several
// places (SpecialCustomerManager when a group arrives, SpecialCustomerManager on every daily phase
// update while the group is here, and the arrival popup UI). Seeing the postfix run more than once
// per arrival is expected - it does NOT mean the patch is applied twice. The postfix is stateless,
// so extra calls are harmless.
[HarmonyPatch(typeof(SpecialCustomerData), nameof(SpecialCustomerData.GetBuyQuantityForPlayerRank))]
public class MaxBuyPatch
{
    public const int Multiplier = 2;

    // Flip to true to log every call with its caller, to tell "called twice" from "patched twice".
    private const bool DebugLogging = true;

    public static void Postfix(SpecialCustomerData __instance, ERank rank, ref int __result)
    {
        int original = __result;
        __result = original * Multiplier;

        if (DebugLogging)
        {
            string caller = new StackTrace().GetFrame(2)?.GetMethod()?.DeclaringType?.Name ?? "?";
            Melon<Core>.Logger.Msg($"[{__instance.GroupId}] rank {rank}: {original} -> {__result} (caller: {caller})");
        }
    }
}
