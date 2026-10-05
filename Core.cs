using MelonLoader;
using UnityEngine;
using MelonLoader.Utils;
using HarmonyLib;

// Conditional compilation example for IL2CPP and MONO
// #if <Build config> is used to check the build configuration
#if IL2CPP
using Il2CppScheduleOne.NPCs; // IL2Cpp using directive
#elif MONO
using ScheduleOne.NPCs; // Mono using directive
#else
// Other build configs
#endif

[assembly: MelonInfo(typeof(BetterSpecialCustomers.Core), "BetterSpecialCustomers", "1.0.0", "Lumahex", null)] 
[assembly: MelonGame("TVGS", "Schedule I")]
// MelonLoader already applies every [HarmonyPatch] class in the mod assembly on load.
// Opt out so the manual PatchAll() below is the only thing applying them (otherwise each patch is applied twice).
[assembly: HarmonyDontPatchAll]

namespace BetterSpecialCustomers
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized. (Quest test)");
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.Probe();
            // Patch each class separately (instead of PatchAll) so one failing patch can't stop the others.
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.PatchAllClasses(HarmonyInstance);
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.ListPatchedMethods(HarmonyInstance);
            BetterSpecialCustomers.Quests.BikersQuest.Register();
        }

    }
}