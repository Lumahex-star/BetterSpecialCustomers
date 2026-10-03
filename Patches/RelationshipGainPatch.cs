using System;
using HarmonyLib;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// When the player finishes a sale to a Special Customer group, work out how much of the group's buy limit
// has been sold and tell GroupRelationships. It gives the relationship gain once per visit, and only after
// at least MinFractionOfLimitForGain of the limit has been sold.
//
// SpecialCustomerLeader.DealSubmit_Server runs only on the server/host, and it calls
// SpecialCustomerManager.DealCompleted_Client(receipt) to announce the finished sale. A postfix on that
// call is therefore a server-side "a sale just happened" hook.
// (Relationships are authoritative on the host; see the notes in the PR about multiplayer.)
//
// How "sold so far" is measured: the leader's buy limit works like a budget. It starts at the limit
// (StartingBuyQuantity) and each sale lowers RemainingBuyQuantity; the game refills it every morning while
// the group is in town. So "sold so far" = limit - remaining, which is also what the handover screen's
// progress bar shows. The game lowers RemainingBuyQuantity just AFTER announcing the sale, so at this point
// it doesn't include the sale yet and we add that sale ourselves.
[HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.DealCompleted_Client))]
public class RelationshipGainPatch
{
    public static void Postfix(SpecialCustomerManager __instance, SpecialCustomerDealReceipt dealRecipe)
    {
        SpecialCustomerData group = __instance.CurrentGroupData;
        SpecialCustomerLeader leader = dealRecipe?.Leader;
        if (group == null || leader == null || dealRecipe.Items == null)
            return;

        int soldNow = 0;
        foreach (var item in dealRecipe.Items)
            soldNow += item.GetTotalAmount();

        // StartingBuyQuantity isn't restored when a save is loaded mid-visit, so fall back to recalculating it.
        int limit = leader.StartingBuyQuantity;
        if (limit <= 0)
            limit = group.GetBuyQuantityForPlayerRank(NetworkSingleton<LevelManager>.Instance.Rank);
        if (limit <= 0)
            return;

        int soldEarlier = Math.Max(0, limit - leader.RemainingBuyQuantity);
        float fractionSold = Math.Min(1f, (soldEarlier + soldNow) / (float)limit);
        GroupRelationships.RegisterSale(group.GroupId, fractionSold);
    }
}
