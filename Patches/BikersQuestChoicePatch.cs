using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using BetterSpecialCustomers.Quests;
#if MONO
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.SpecialCustomers;
using ScheduleOne.NPCs;
#elif IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.SpecialCustomers;
using Il2CppScheduleOne.NPCs;
#endif

namespace BetterSpecialCustomers.Patches
{
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class BikersQuestChoicePatch
    {
        private const string DieselId = "diesel_rodd";
        private static SpecialCustomerLeader _diesel;

        // The leader (and its DialogueController) is one object reused for every group,
        // so add our choice only once.
        private static readonly HashSet<int> Added = new HashSet<int>();

        public static void Postfix(SpecialCustomerLeader __instance)
        {
            _diesel = __instance;

            var controller = __instance.DialogueHandler.GetComponent<DialogueController>();
            if (controller == null || !Added.Add(controller.GetInstanceID())) return;

            var choice = new DialogueController.DialogueChoice();
            choice.ChoiceText = "You got any jobs for me?";
            choice.Enabled = true;

            // Asked every time the menu opens. The leader's ID changes with each group,
            // so this shows the option only while Diesel is the one in town.
            // It stays visible even when he has no job; he just says so. It is only hidden while a job is open.
            choice.shouldShowCheck = enabled => enabled && __instance.ID == DieselId && CanAsk();

            choice.onChoosen.AddListener(OnQuestChoice);
            controller.AddDialogueChoice(choice);

            // Second option: report back once the informant is dead.
            var handIn = new DialogueController.DialogueChoice();
            handIn.ChoiceText = "He's dealt with.";
            handIn.Enabled = true;
            handIn.shouldShowCheck = enabled => enabled && __instance.ID == DieselId && IsReadyToHandIn();
            handIn.onChoosen.AddListener(OnHandIn);
            controller.AddDialogueChoice(handIn);
            MelonLogger.Msg("[quest] added the Diesel quest choices");
        }

        private static void OnQuestChoice()
        {
            MelonLogger.Msg("[quest] clicked");

            if (_diesel == null) { MelonLogger.Msg("[quest] _diesel is EMPTY"); return; }

            var save = BikersQuestSave.Instance;
            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            string groupId = group?.GroupId;
            if (save == null) return;
            if (!string.IsNullOrEmpty(groupId)) save.GroupId = groupId; // lets the visit reset find this group

            string line;
            if (save.Completed)
            {
                line = "You already did right by us this run. Come back when we roll through again.";
            }
            else
            {
                // Roll once per visit and remember it, so asking again can't re-roll.
                if (save.OfferState == 0)
                    save.OfferState = UnityEngine.Random.value < BikersQuest.OfferChance ? 1 : 2;

                if (save.OfferState == 1)
                {
                    line = "Somebody's been feedin' the cops about club business. I've got a name now. Find him, and make sure he never talks again.";
                    BikersQuest.Start(groupId);
                }
                else
                {
                    line = NoJobLines[UnityEngine.Random.Range(0, NoJobLines.Length)];
                }
            }

            _diesel.DialogueHandler.ShowWorldspaceDialogue(line, 7f);
        }

        private static readonly string[] NoJobLines =
        {
            "Not this time, prospect. Road's quiet. Come find me when it ain't.",
            "Nothin' right now. The club'll let you know when we need a hand.",
            "Ain't got nothin' for you today. Go count your money.",
        };

        private static void OnHandIn()
        {
            if (_diesel != null)
                _diesel.DialogueHandler.ShowWorldspaceDialogue("Good. Rats don't get to walk away. The club won't forget this.", 6f);
            BikersQuest.HandIn();
        }

        private static bool IsReadyToHandIn()
        {
            var save = BikersQuestSave.Instance;
            return save != null && save.TargetKilled && !save.Completed;
        }

        // The ask option shows unless a job is currently open (accepted but not handed in).
        private static bool CanAsk()
        {
            var save = BikersQuestSave.Instance;
            return save == null || !save.Accepted || save.Completed;
        }
    }
}