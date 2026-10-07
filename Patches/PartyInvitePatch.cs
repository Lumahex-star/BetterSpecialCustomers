using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.Dialogue;
using ScheduleOne.NPCs;
using ScheduleOne.Police;
#elif IL2CPP
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Police;
#endif

namespace BetterSpecialCustomers.Patches
{
    // Adds the "The Party Bus is in town" option to ordinary NPCs, for Chad Chaddington's spread-the-word job.
    // Every NPC has its own DialogueController; we add our option when each one starts up.
    [HarmonyPatch(typeof(DialogueController), "Start")]
    internal class PartyInvitePatch
    {
        private const string InviteText = "The Party Bus is in town tonight. You should come!";

        private static readonly string[] YesLines =
        {
            "A party bus? Count me in!",
            "Sounds like a good time. I'll be there.",
            "Finally, something fun around here.",
            "Oh, nice. I'll swing by.",
        };

        private static readonly string[] NoLines =
        {
            "Not really my scene, sorry.",
            "I've got stuff to do tonight.",
            "No thanks, I'm good.",
            "Parties aren't for me.",
        };

        public static void Postfix(DialogueController __instance)
        {
            var npc = __instance.GetComponentInParent<NPC>();
            if (npc == null || !IsInvitable(npc)) return;

            DialogueChoiceUtil.Add(__instance, InviteText, () => PartyTask.CanInvite(npc.ID), () => OnInvite(npc));
        }

        // Not the group leaders (they have their own jobs), the police, or our own quest targets.
        private static bool IsInvitable(NPC npc)
        {
            if (npc.GetComponent<SpecialCustomerLeader>() != null) return false;
            if (npc.GetComponent<SpecialCustomer>() != null) return false;
            if (npc.GetComponent<PoliceOfficer>() != null) return false;
            return npc.ID != "bikers_informant" && npc.ID != "businessmen_auditor";
        }

        private static void OnInvite(NPC npc)
        {
            bool accepted = PartyTask.Invite(npc.ID);
            string[] lines = accepted ? YesLines : NoLines;
            npc.DialogueHandler.ShowWorldspaceDialogue(lines[UnityEngine.Random.Range(0, lines.Length)], 5f);
        }
    }
}
