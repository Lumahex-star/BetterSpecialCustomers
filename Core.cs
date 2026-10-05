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
            LoggerInstance.Msg("Initialized.");
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.Probe();
            // Patch each class separately (instead of PatchAll) so one failing patch can't stop the others.
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.PatchAllClasses(HarmonyInstance);
            BetterSpecialCustomers.Diagnostics.PatchDiagnostics.ListPatchedMethods(HarmonyInstance);
        }

        // TEMPORARY: press F9 to list every clothing item in the log (to find the biker clothes). Remove later.
        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                BetterSpecialCustomers.Diagnostics.ClothingDump.Run();
            if (Input.GetKeyDown(KeyCode.F10))
                BetterSpecialCustomers.Diagnostics.ClothingDump.RunAllItems();

            // About once a second: let the hippies' package job notice the package moving between dead drops.
            if (Time.unscaledTime >= _nextQuestTick)
            {
                _nextQuestTick = Time.unscaledTime + 1f;
                BetterSpecialCustomers.Quests.HippiesTask.Tick();
            }
        }

        private float _nextQuestTick;
    }
}