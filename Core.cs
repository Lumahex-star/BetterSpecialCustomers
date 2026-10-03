using MelonLoader;
using UnityEngine;
using MelonLoader.Utils;

// Conditional compilation example for IL2CPP and MONO
// #if <Build config> is used to check the build configuration
#if IL2CPP
using Il2CppScheduleOne.NPCs; // IL2Cpp using directive
#elif MONO
using ScheduleOne.NPCs; // Mono using directive
#else
// Other build configs
#endif

[assembly: MelonInfo(typeof(MONO_IL2CPP_Template.Core), "BetterSpecialCustomers", "1.0.0", "Lumahex", null)] 
[assembly: MelonGame("TVGS", "Schedule I")]

namespace MONO_IL2CPP_Template
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }
    }
}