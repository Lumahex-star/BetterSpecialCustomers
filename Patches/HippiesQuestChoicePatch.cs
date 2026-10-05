using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    // Krystal Bird's quest dialogue (see JobDialogue).
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class HippiesQuestChoicePatch
    {
        private static readonly JobDialogue Dialogue = new JobDialogue
        {
            LeaderId = "krystal_bird",
            AskText = "Can I do anything for the group?",
            HandInText = "The package has been delivered.",
            AskSeconds = 8f,
            Job = HippiesQuest.Job,
            Lines = new JobLines
            {
                OfferLine = _ => "Oh, bless you. There's a little something that needs to get to a friend, and we can't be seen with it. " +
                                 "I've left it at a drop. Take it to the other one, and be gentle with it.",
                NoJobLines = new[]
                {
                    "The universe has been quiet today, friend. Come back when it speaks.",
                    "Not right now, sweetheart. Go enjoy the sunshine.",
                    "Nothing this time. Everything is as it should be.",
                },
                AlreadyDoneLine = "You've done so much already. We're grateful. Rest a while.",
                HandInLine = "Wonderful. The garden will be so happy. Please, take these, with our love.",
            },
        };

        public static void Postfix(SpecialCustomerLeader __instance) => Dialogue.Register(__instance);
    }
}
