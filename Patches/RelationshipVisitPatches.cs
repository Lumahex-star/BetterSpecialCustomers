using System.Reflection;
using HarmonyLib;
using MelonLoader;
using BetterSpecialCustomers.Diagnostics;
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

// To find out WHICH group is arriving we read the manager's private "_currentData" (it holds the GroupId).
// Earlier this used Harmony's "____currentData" parameter injection, which only works when _currentData is a
// real FIELD. In the IL2CPP build the game's classes are wrapper classes where a private field may be exposed
// as a PROPERTY (or not at all), so we look for either a field or a property by name, using reflection.
//
// Caution: the game also calls RunArrivalPhase when a save is loaded in the middle of a visit. That is why
// GroupRelationships.BeginVisit remembers (and saves) whether the group is already in town.
[HarmonyPatch(typeof(SpecialCustomerManager), "RunArrivalPhase")]
public class RelationshipArrivalPatch
{
    private static readonly FieldInfo CurrentDataField = AccessTools.Field(typeof(SpecialCustomerManager), "_currentData");
    private static readonly PropertyInfo CurrentDataProperty = AccessTools.Property(typeof(SpecialCustomerManager), "_currentData");
    private static bool _reportedMissing;

    private static string GetArrivingGroupId(SpecialCustomerManager manager)
    {
        object data = CurrentDataField != null
            ? CurrentDataField.GetValue(manager)
            : CurrentDataProperty?.GetValue(manager);

        if (data == null && CurrentDataField == null && CurrentDataProperty == null && !_reportedMissing)
        {
            _reportedMissing = true;
            Melon<Core>.Logger.Error("RelationshipArrivalPatch: can't find SpecialCustomerManager._currentData as a field or property; visits will NOT be tracked. See the [diag] Probe lines.");
        }
        return (data as SpecialCustomerSaveData)?.GroupId;
    }

    public static void Prefix(SpecialCustomerManager __instance)
    {
        PatchDiagnostics.Hit("RelationshipArrivalPatch (group arriving)");
        string groupId = GetArrivingGroupId(__instance);
        if (string.IsNullOrEmpty(groupId))
            return;

        GroupRelationships.BeginVisit(groupId);

        // IL2CPP only: the buy limit patches on the method itself aren't reached there (see BuyLimitFieldSync).
        // The relationship was just updated above, so bring the game's own numbers in line with it.
        BuyLimitFieldSync.Apply(__instance, groupId);
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
        PatchDiagnostics.Hit("RelationshipDeparturePatch (group leaving)");
        SpecialCustomerData group = __instance.CurrentGroupData;
        if (group == null)
            return;

        GroupRelationships.EndVisit(group.GroupId);
        BetterSpecialCustomers.Quests.BikersQuest.ResetForNewVisit(group.GroupId);
    }
}
