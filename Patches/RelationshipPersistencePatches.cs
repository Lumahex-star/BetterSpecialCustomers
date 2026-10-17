using System;
using System.IO;
using HarmonyLib;
using BetterSpecialCustomers.Diagnostics;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
#if MONO
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

// Saving: SpecialCustomerManager.GetSaveString() returns the JSON the game writes to SpecialCustomers.json.
// We add our relationship values to that same JSON object.
[HarmonyPatch(typeof(SpecialCustomerManager), nameof(SpecialCustomerManager.GetSaveString))]
public class RelationshipSavePatch
{
    public static void Postfix(ref string __result)
    {
        PatchDiagnostics.Hit("RelationshipSavePatch (writing save)");
        try
        {
            __result = GroupRelationships.AddToSaveJson(__result);
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Error("Failed to add relationships to the save string: " + ex);
        }
    }
}

// Loading: the game's SpecialCustomerLoader.Load(mainPath) reads "<mainPath>.json". We read the same file
// ourselves first. The prefix always runs (even for a brand new save with no file), which also makes sure
// values from a previously loaded save never leak into this one.
[HarmonyPatch(typeof(SpecialCustomerLoader), nameof(SpecialCustomerLoader.Load))]
public class RelationshipLoadPatch
{
    public static void Prefix(string mainPath)
    {
        PatchDiagnostics.Hit("RelationshipLoadPatch (reading save)");
        try
        {
            string file = mainPath + ".json";
            GroupRelationships.LoadFromJson(File.Exists(file) ? File.ReadAllText(file) : null);
        }
        catch (Exception ex)
        {
            GroupRelationships.Clear();
            Melon<Core>.Logger.Error("Failed to load relationships: " + ex);
        }
    }
}
