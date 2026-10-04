using HarmonyLib;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Levelling;
using ScheduleOne.NPCs;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// When the player finishes a sale to a Special Customer group, tell GroupRelationships how many units were
// sold and what the group's buy limit is. It adds the sales up over the visit and gives the relationship
// gain once, after at least MinFractionOfLimitForGain of the limit has been sold in total.
//
// SpecialCustomerLeader.DealSubmit_Server runs only on the server/host, and it calls
// SpecialCustomerManager.DealCompleted_Client(receipt) to announce the finished sale. A postfix on that
// call is therefore a server-side "a sale just happened" hook.
// (Relationships are authoritative on the host; see the notes in the PR about multiplayer.)
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

        GroupRelationships.RegisterSale(group.GroupId, soldNow, limit);
    }
}
