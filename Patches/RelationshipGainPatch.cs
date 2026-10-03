using HarmonyLib;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// When the player finishes a sale to a Special Customer group, tell GroupRelationships about it. It only
// raises the relationship for the FIRST sale of each visit; later sales that visit change nothing.
//
// SpecialCustomerLeader.DealSubmit_Server runs only on the server/host, and it calls
// SpecialCustomerManager.DealCompleted_Client(receipt) to announce the finished sale. A postfix on that
// call is therefore a server-side "a sale just happened" hook.
// (Relationships are authoritative on the host; see the notes in the PR about multiplayer.)
[HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.DealCompleted_Client))]
public class RelationshipGainPatch
{
    public static void Postfix(SpecialCustomerManager __instance)
    {
        SpecialCustomerData group = __instance.CurrentGroupData;
        if (group == null)
            return;

        GroupRelationships.RegisterSale(group.GroupId);
    }
}
