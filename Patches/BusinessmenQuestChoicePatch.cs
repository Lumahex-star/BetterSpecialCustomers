using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    // Johnny Smith's quest dialogue (see JobDialogue).
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class BusinessmenQuestChoicePatch
    {
        private static readonly JobDialogue Dialogue = new JobDialogue
        {
            LeaderId = "johnny_smith",
            AskText = "Anything I can help with?",
            HandInText = "The audit won't be a problem anymore.",
            AskSeconds = 8f,
            Job = AuditQuest.Job,
            Lines = new JobLines
            {
                OfferLine = name => $"As it happens, yes. An auditor, {name}, has been looking at the books for the wrong reasons. " +
                                    "He keeps to a strict routine: home, the office, lunch. Make sure he doesn't finish his report.",
                NoJobLines = new[]
                {
                    "Nothing at the moment. Business is... stable. Enjoy it.",
                    "Not today. If something comes up, you'll be the first call.",
                    "We're fully staffed on problems right now. Check back later.",
                },
                AlreadyDoneLine = "You've done enough for us this time. We'll be in touch.",
                HandInLine = "Excellent. A tidy solution. Our accountants will be very pleased.",
            },
        };

        public static void Postfix(SpecialCustomerLeader __instance) => Dialogue.Register(__instance);
    }
}
