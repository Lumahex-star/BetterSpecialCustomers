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

// When the player finishes a sale to a Special Customer group, raise that group's relationship.
//
// SpecialCustomerLeader.DealSubmit_Server runs only on the server/host, and it calls
// SpecialCustomerManager.DealCompleted_Client(receipt) to announce the finished sale. A postfix on that
// call is therefore a server-side "a sale just happened" hook, with the receipt as a parameter.
// (Relationships are authoritative on the host; see the notes in the PR about multiplayer.)
[HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.DealCompleted_Client))]
public class RelationshipGainPatch
{
    public static void Postfix(SpecialCustomerManager __instance, SpecialCustomerDealReceipt dealRecipe)
    {
        SpecialCustomerData group = __instance.CurrentGroupData;
        if (group == null || dealRecipe?.Items == null)
            return;

        int sold = 0;
        foreach (var item in dealRecipe.Items)
            sold += item.GetTotalAmount();

        // What a full order is for this group at the player's current rank (includes our own patches).
        int fullOrder = group.GetBuyQuantityForPlayerRank(NetworkSingleton<LevelManager>.Instance.Rank);
        if (sold <= 0 || fullOrder <= 0)
            return;

        float share = System.Math.Min(1f, (float)sold / fullOrder);
        GroupRelationships.Change(group.GroupId, GroupRelationships.GainForFullOrder * share);
    }
}
