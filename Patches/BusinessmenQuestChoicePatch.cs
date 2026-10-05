using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
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
    // Johnny Smith's quest dialogue. Same idea as BikersQuestChoicePatch: the leader is one shared object, so we add
    // our options once and each option checks that the current leader is Johnny.
    [HarmonyPatch(typeof(SpecialCustomerLeader), nameof(SpecialCustomerLeader.SetCustomerData))]
    internal class BusinessmenQuestChoicePatch
    {
        private const string JohnnyId = "johnny_smith";
        private static SpecialCustomerLeader _leader;
        private static readonly HashSet<int> Added = new HashSet<int>();

        public static void Postfix(SpecialCustomerLeader __instance)
        {
            _leader = __instance;

            var controller = __instance.DialogueHandler.GetComponent<DialogueController>();
            if (controller == null || !Added.Add(controller.GetInstanceID())) return;

            var ask = new DialogueController.DialogueChoice();
            ask.ChoiceText = "Anything I can help with?";
            ask.Enabled = true;
            ask.shouldShowCheck = enabled => enabled && __instance.ID == JohnnyId && CanAsk();
            ask.onChoosen.AddListener(OnAsk);
            controller.AddDialogueChoice(ask);

            var handIn = new DialogueController.DialogueChoice();
            handIn.ChoiceText = "The audit won't be a problem anymore.";
            handIn.Enabled = true;
            handIn.shouldShowCheck = enabled => enabled && __instance.ID == JohnnyId && IsReadyToHandIn();
            handIn.onChoosen.AddListener(OnHandIn);
            controller.AddDialogueChoice(handIn);
            MelonLogger.Msg("[quest] added the Johnny Smith quest choices");
        }

        private static readonly string[] NoJobLines =
        {
            "Nothing at the moment. Business is... stable. Enjoy it.",
            "Not today. If something comes up, you'll be the first call.",
            "We're fully staffed on problems right now. Check back later.",
        };

        private static void OnAsk()
        {
            if (_leader == null) return;

            var save = BusinessmenQuestSave.Instance;
            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            string groupId = group?.GroupId;
            if (save == null) return;
            if (!string.IsNullOrEmpty(groupId)) save.GroupId = groupId;

            string line;
            if (save.Completed && !BikersQuest.DebugMode)
            {
                line = "You've done enough for us this time. We'll be in touch.";
            }
            else
            {
                if (save.Completed) AuditQuest.ResetRun(); // debug mode only: start a fresh run

                // Roll once per visit (every time in debug mode); BikersQuest owns the chance and the debug switch.
                if (save.OfferState == 0 || BikersQuest.DebugMode)
                    save.OfferState = UnityEngine.Random.value < BikersQuest.OfferChance ? 1 : 2;

                if (save.OfferState == 1)
                {
                    AuditQuest.Start(groupId); // picks the target's name
                    line = $"As it happens, yes. An auditor, {save.TargetName}, has been looking at the books for the wrong reasons. " +
                           "He keeps to a strict routine: home, the office, lunch. Make sure he doesn't finish his report.";
                }
                else
                {
                    line = NoJobLines[UnityEngine.Random.Range(0, NoJobLines.Length)];
                }
            }

            _leader.DialogueHandler.ShowWorldspaceDialogue(line, 8f);
        }

        private static void OnHandIn()
        {
            if (_leader != null)
                _leader.DialogueHandler.ShowWorldspaceDialogue("Excellent. A tidy solution. Our accountants will be very pleased.", 6f);
            AuditQuest.HandIn();
        }

        private static bool IsReadyToHandIn()
        {
            var save = BusinessmenQuestSave.Instance;
            return save != null && save.TargetKilled && !save.Completed;
        }

        // The ask option shows unless a job is currently open.
        private static bool CanAsk()
        {
            var save = BusinessmenQuestSave.Instance;
            return save == null || !save.Accepted || save.Completed;
        }
    }
}
