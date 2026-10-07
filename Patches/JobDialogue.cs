using System.Collections.Generic;
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
    // Adds one group leader's two job options to the leader's dialogue menu: "ask for a job" and "report back".
    // The leader (and its DialogueController) is a single object reused for every group, so the options are added
    // once and each one checks that the current leader is this group's before showing.
    internal sealed class JobDialogue
    {
        public string LeaderId;
        public string AskText;
        public string HandInText;
        public JobController Job;
        public JobLines Lines;
        public float AskSeconds = 7f;
        public float HandInSeconds = 6f;

        private SpecialCustomerLeader _leader;
        private readonly HashSet<int> _added = new HashSet<int>();

        // Called after the game sets up a group leader.
        public void Register(SpecialCustomerLeader leader)
        {
            _leader = leader;

            var controller = leader.DialogueHandler.GetComponent<DialogueController>();
            if (controller == null || !_added.Add(controller.GetInstanceID())) return;

            // The check is asked every time the menu opens, and the leader's ID changes with each group.
            DialogueChoiceUtil.Add(controller, AskText, () => leader.ID == LeaderId && Job.CanAsk(), OnAsk);
            DialogueChoiceUtil.Add(controller, HandInText, () => leader.ID == LeaderId && Job.IsReadyToHandIn(), OnHandIn);

            MelonLogger.Msg($"[quest] added the quest choices for leader '{LeaderId}'");
        }

        private void OnAsk()
        {
            if (_leader == null) return;
            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            string line = Job.Ask(group?.GroupId, Lines);
            if (line != null) _leader.DialogueHandler.ShowWorldspaceDialogue(line, AskSeconds);
        }

        private void OnHandIn()
        {
            if (_leader != null) _leader.DialogueHandler.ShowWorldspaceDialogue(Lines.HandInLine, HandInSeconds);
            Job.HandIn();
        }
    }
}
