using HarmonyLib;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.Dialogue;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.Police;
#elif IL2CPP
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Schedules;
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

        // Not the group leaders (they have their own jobs), the police, our own quest targets, or anyone whose
        // schedule turns on a dialogue choice by its position in the list (see UsesChoiceByIndex).
        private static bool IsInvitable(NPC npc)
        {
            if (npc.GetComponent<SpecialCustomerLeader>() != null) return false;
            if (npc.GetComponent<SpecialCustomer>() != null) return false;
            if (npc.GetComponent<PoliceOfficer>() != null) return false;
            if (UsesChoiceByIndex(npc)) return false;
            return npc.ID != "bikers_informant" && npc.ID != "businessmen_auditor";
        }

        // A NPCEvent_LocationDialogue enables Choices[ChoiceToEnable] when it starts. Suppliers (Shirley, Fungal Phil...)
        // use this for their in-person meeting: their "Yes" choice opens the shop and is expected at a fixed index. The game
        // adds that choice in Supplier.Start, which can run after DialogueController.Start, so our option could take
        // that index, get enabled instead, and the "Yes" choice would never show. Skip these NPCs so their indices stay put.
        // The events can be on inactive objects (suppliers disable theirs until a meeting), hence includeInactive.
        private static bool UsesChoiceByIndex(NPC npc)
        {
            foreach (var evt in npc.GetComponentsInChildren<NPCEvent_LocationDialogue>(true))
            {
                if (evt.ChoiceToEnable >= 0) return true;
            }
            return false;
        }

        private static void OnInvite(NPC npc)
        {
            bool accepted = PartyTask.Invite(npc.ID);
            string[] lines = accepted ? YesLines : NoLines;
            npc.DialogueHandler.ShowWorldspaceDialogue(lines[UnityEngine.Random.Range(0, lines.Length)], 5f);
        }
    }
}
