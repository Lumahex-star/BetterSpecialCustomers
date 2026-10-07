using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    // Chad Chaddington's quest dialogue (see JobDialogue).
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class PartyBusQuestChoicePatch
    {
        private static readonly JobDialogue Dialogue = new JobDialogue
        {
            LeaderId = "chad_chaddington",
            AskText = "Need a hand with anything?",
            HandInText = "The word is out.",
            AskSeconds = 8f,
            Job = PartyBusQuest.Job,
            Lines = new JobLines
            {
                OfferLine = _ => $"Dude, yes! We need people to know the bus is in town and tonight is ON. " +
                                 $"Go tell {PartyTask.PeopleNeeded} people. Some will say no. Their loss. Keep going!",
                NoJobLines = new[]
                {
                    "We're good for now, bro. The vibe is already unreal.",
                    "Nothing right now! Just enjoy the party.",
                    "Not today, my guy. We got it handled.",
                },
                AlreadyDoneLine = "You already got the word out, legend! Go have some fun.",
                HandInLine = "LEGEND! The crowd is gonna be insane. Here, you earned it!",
            },
        };

        public static void Postfix(SpecialCustomerLeader __instance) => Dialogue.Register(__instance);
    }
}
