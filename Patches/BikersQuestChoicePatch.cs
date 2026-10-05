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
            // It is also hidden once the quest has been accepted (this flag is saved with the game).
            choice.shouldShowCheck = enabled => enabled && __instance.ID == DieselId && !IsQuestAccepted();

            choice.onChoosen.AddListener(OnQuestChoice);
            controller.AddDialogueChoice(choice);
            MelonLogger.Msg("[quest] added the Diesel quest choice");
        }

        private static void OnQuestChoice()
        {
            MelonLogger.Msg("[quest] clicked");

            if (_diesel == null) { MelonLogger.Msg("[quest] _diesel is EMPTY"); return; }

            MelonLogger.Msg("[quest] calling ShowWorldspaceDialogue");
            _diesel.DialogueHandler.ShowWorldspaceDialogue("Yeah, sure. There's a guy down at the docks who's been giving us some trouble recently. Go take care of him for us.", 7f);

            var rend = _diesel.DialogueHandler.WorldspaceRend;
            MelonLogger.Msg($"[quest] after: shown='{rend.ShownText}' visible={rend.IsVisible}");

            // Starts the quest and remembers it was accepted, so the option disappears (and stays gone after a reload).
            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            BikersQuest.Start(group?.GroupId);
        }

        private static bool IsQuestAccepted()
        {
            return BikersQuestSave.Instance != null && BikersQuestSave.Instance.Accepted;
        }
    }
}