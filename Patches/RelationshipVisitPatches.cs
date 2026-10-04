using HarmonyLib;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// These two patches tell GroupRelationships when a visit starts and ends.
//
// Both target PRIVATE methods of SpecialCustomerManager, so they name the method with a string
// (nameof() can't see private members).

// SpecialCustomerManager.RunArrivalPhase() is the game's "group arrives in town" code. It works out the
// group's buy quantity, so our prefix runs BEFORE it - the new relationship must already be applied by then.
//
// "____currentData" is a Harmony feature: a parameter named three underscores + the field's exact name
// gives the patch access to that PRIVATE field of the patched object. The field here is called
// "_currentData" (it starts with its own underscore), so the parameter has FOUR underscores in total.
// It holds the GroupId of the group that is arriving.
//
// Caution: the game also calls RunArrivalPhase when a save is loaded in the middle of a visit. That is why
// GroupRelationships.BeginVisit remembers (and saves) whether the group is already in town.
[HarmonyPatch(typeof(SpecialCustomerManager), "RunArrivalPhase")]
public class RelationshipArrivalPatch
{
    public static void Prefix(SpecialCustomerSaveData ____currentData)
    {
        if (____currentData == null || string.IsNullOrEmpty(____currentData.GroupId))
            return;

        GroupRelationships.BeginVisit(____currentData.GroupId);
    }
}

// SpecialCustomerManager.RemoveCustomerGroup() is called when the group packs up and leaves (and when a
// group is replaced by the debug QueueCustomerGroup command). It is NOT called when you quit to the menu,
// so a visit isn't ended just because the player closed the game. The group is still in CurrentGroupData
// at the start of the method, so we read it in a prefix.
[HarmonyPatch(typeof(SpecialCustomerManager), "RemoveCustomerGroup")]
public class RelationshipDeparturePatch
{
    public static void Prefix(SpecialCustomerManager __instance)
    {
        SpecialCustomerData group = __instance.CurrentGroupData;
        if (group == null)
            return;

        GroupRelationships.EndVisit(group.GroupId);
    }
}
