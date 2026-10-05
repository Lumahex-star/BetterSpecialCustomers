using System;
using System.Collections.Generic;
using MelonLoader;
using S1API.Internal.Abstraction;   // Saveable
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField

namespace BetterSpecialCustomers.Quests
{
    // Chad Chaddington's job: "spread the word". The player invites ordinary NPCs to the Party Bus, and some say no.
    // The shared rules (roll, start, reward, reset) are in JobController; the invite option on each NPC is added
    // by PartyInvitePatch.

    // Who has been asked this run, and how many said yes. Saved with the game.
    public class PartyRun
    {
        public List<string> Asked = new List<string>(); // NPC ids already asked (accepted or not)
        public int Accepted;
    }

    public class PartyBusQuestSave : Saveable
    {
        public static PartyBusQuestSave Instance { get; private set; }

        public PartyBusQuestSave() { Instance = this; }

        [SaveableField("partybus_quest")]
        public JobSaveData Data = new JobSaveData();

        [SaveableField("partybus_quest_run")]
        public PartyRun Run = new PartyRun();

        protected override void OnLoaded() { Instance = this; }
    }

    internal static class PartyTask
    {
        public const int PeopleNeeded = 5;

        // Chance that an invited NPC turns the invitation down.
        public const float RejectChance = 0.3f;

        // The reward: party supplies that are useful to the player (mixing ingredients). Item id, amount.
        private static readonly KeyValuePair<string, int>[] PartySupplies =
        {
            new KeyValuePair<string, int>("energydrink", 5),
            new KeyValuePair<string, int>("donut", 5),
            new KeyValuePair<string, int>("megabean", 2),
        };

        public static void GiveSupplies() => RewardUtil.GiveItems(PartySupplies);

        private static PartyBusQuestSave Save => PartyBusQuestSave.Instance;

        // The invite option shows on an NPC while the job is open, the word is not yet out, and he has not been asked.
        public static bool CanInvite(string npcId)
        {
            var save = Save;
            if (save == null || string.IsNullOrEmpty(npcId)) return false;
            var data = save.Data;
            return data.Accepted && !data.TargetKilled && !data.Completed && !save.Run.Asked.Contains(npcId);
        }

        // Asks one NPC. Returns true if he accepted. The caller shows his reply.
        public static bool Invite(string npcId)
        {
            var save = Save;
            if (save == null || !CanInvite(npcId)) return false;

            save.Run.Asked.Add(npcId);
            bool accepted = UnityEngine.Random.value >= RejectChance;
            if (accepted) save.Run.Accepted++;

            UpdateObjectiveTitle();
            Melon<Core>.Logger.Msg($"[quest] {npcId} {(accepted ? "accepted" : "turned down")} the invite ({save.Run.Accepted}/{PeopleNeeded})");

            if (save.Run.Accepted >= PeopleNeeded) PartyBusQuest.Job.OnObjectiveDone();
            return accepted;
        }

        public static string ObjectiveText(int accepted) =>
            $"Tell people the Party Bus is in town ({Math.Min(accepted, PeopleNeeded)}/{PeopleNeeded})";

        private static void UpdateObjectiveTitle()
        {
            var entries = PartyBusQuest.Job.Quest?.QuestEntries;
            if (entries != null && entries.Count > 0) entries[0].Title = ObjectiveText(Save.Run.Accepted);
        }

        // Called when the job's state changes: forget the run when the job is not running.
        public static void Refresh()
        {
            var save = Save;
            if (save == null) return;
            bool wanted = save.Data.Accepted && !save.Data.TargetKilled;
            if (!wanted && (save.Run.Asked.Count > 0 || save.Run.Accepted > 0)) save.Run = new PartyRun();
        }
    }

    public class PartyBusQuest : Quest
    {
        internal static readonly JobController Job = new JobController(new JobDefinition
        {
            QuestIdPrefix = "partybus_word_quest",
            RewardLogLabel = "Chad's spread-the-word job",
            RelationshipReward = 1f,
            CashReward = 0f,                       // the reward is party supplies and a bigger relationship boost
            GrantExtraReward = PartyTask.GiveSupplies,
            GetSave = () => PartyBusQuestSave.Instance?.Data,
            PickName = () => string.Empty,         // no target to name
            RefreshTarget = PartyTask.Refresh,
            CreateQuest = id => QuestManager.CreateQuest<PartyBusQuest>(id),
        });

        protected override string Title => "Spread the Word";
        protected override string Description => "Chad Chaddington wants the whole town to know the Party Bus is here.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            int accepted = PartyBusQuestSave.Instance?.Run.Accepted ?? 0;
            AddEntry(PartyTask.ObjectiveText(accepted));
            AddEntry("Tell Chad Chaddington the word is out");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
        }
    }
}
