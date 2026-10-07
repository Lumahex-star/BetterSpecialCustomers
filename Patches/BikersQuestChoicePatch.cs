using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    // Diesel Rodd's quest dialogue (see JobDialogue).
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class BikersQuestChoicePatch
    {
        private static readonly JobDialogue Dialogue = new JobDialogue
        {
            LeaderId = "diesel_rodd",
            AskText = "You got any jobs for me?",
            HandInText = "He's dealt with.",
            Job = BikersQuest.Job,
            Lines = new JobLines
            {
                OfferLine = name => $"Somebody's been feedin' the cops about club business. Name's {name}. He's holed up at The Piss Hut. Knock on the door, and make sure he never talks again.",
                NoJobLines = new[]
                {
                    "Not this time, prospect. Road's quiet. Come find me when it ain't.",
                    "Nothin' right now. The club'll let you know when we need a hand.",
                    "Ain't got nothin' for you today. Go count your money.",
                },
                AlreadyDoneLine = "You already did right by us this run. Come back when we roll through again.",
                HandInLine = "Good. Rats don't get to walk away. The club won't forget this.",
            },
        };

        public static void Postfix(SpecialCustomerLeader __instance) => Dialogue.Register(__instance);
    }
}
