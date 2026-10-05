using System;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.Entities;               // NPC
using S1API.Internal.Abstraction;   // Saveable
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField
using UnityEngine;

namespace BetterSpecialCustomers.Quests
{
    // Small saved flags that live outside the quest itself.
    public class BikersQuestSave : Saveable
    {
        public static BikersQuestSave Instance { get; private set; }

        public BikersQuestSave() { Instance = this; }

        [SaveableField("bikers_quest_accepted")]
        public bool Accepted;

        // The group the quest belongs to, so the reward goes to the right group even after a reload.
        [SaveableField("bikers_quest_group")]
        public string GroupId = string.Empty;

        // The thief is dead; Diesel still has to be told.
        [SaveableField("bikers_quest_thief_killed")]
        public bool ThiefKilled;

        // Reported back to Diesel, quest finished and rewarded.
        [SaveableField("bikers_quest_completed")]
        public bool Completed;

        protected override void OnLoaded() { Instance = this; }
    }

    // The target. S1API NPCs are created from their class, so this one always exists in the world.
    // PLACEHOLDER: set DocksPosition to a real spot at the docks before testing.
    public sealed class DocksThief : NPC
    {
        public static readonly Vector3 DocksPosition = new Vector3(0f, 0f, 0f);

        public override bool IsPhysical => true;

        public DocksThief() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity("bikers_docks_thief", "Docks", "Thief")
                   .WithSpawnPosition(DocksPosition);
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();

            // Vanilla's Quest_DefeatCartel only counts a kill (IsDead), so we do the same.
            OnDeath += BikersQuest.OnThiefKilled;
        }
    }

    public class BikersQuest : Quest
    {
        // Reward for finishing the job, applied to the group's relationship (it takes effect next visit).
        private const float RelationshipReward = 1f;

        private static BikersQuest _instance;

        protected override string Title => "Trouble at the Docks";
        protected override string Description => "Diesel Rodd wants a thief at the docks dealt with.";
        protected override bool AutoBegin => false;

        protected override void OnCreated()
        {
            base.OnCreated();
            _instance = this;
            var kill = AddEntry("Kill the thief at the docks", DocksThief.DocksPosition);
            kill.SetPOIToNPC<DocksThief>(); // make the marker follow him (returns false if he isn't found)
            AddEntry("Tell Diesel the thief is dealt with");
            Subscribe();
            Melon<Core>.Logger.Msg("[quest] BikersQuest created");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            _instance = this;
            Subscribe();
        }

        // Quest.OnComplete is an event, not a method to override. Unsubscribe first so we never subscribe twice.
        private void Subscribe()
        {
            OnComplete -= HandleComplete;
            OnComplete += HandleComplete;
        }

        // Called by DocksThief when its health reaches zero.
        public static void OnThiefKilled()
        {
            var save = BikersQuestSave.Instance;
            if (_instance == null || save == null || !save.Accepted || save.ThiefKilled) return;
            save.ThiefKilled = true;
            if (_instance.QuestEntries.Count > 0) _instance.QuestEntries[0].Complete();
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Begin();
            Melon<Core>.Logger.Msg("[quest] thief killed, return to Diesel");
        }

        // Called from the "He's dealt with." dialogue choice.
        public static void HandIn()
        {
            var save = BikersQuestSave.Instance;
            if (_instance == null || save == null || !save.ThiefKilled || save.Completed) return;
            save.Completed = true;
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Complete();
            _instance.Complete();
        }

        private void HandleComplete()
        {
            string group = BikersQuestSave.Instance?.GroupId;
            if (!string.IsNullOrEmpty(group))
                GroupRelationships.Reward(group, RelationshipReward, "completed Diesel's docks job");
            else
                Melon<Core>.Logger.Warning("[quest] no saved group id, relationship reward skipped");
        }

        // Called from the dialogue choice.
        public static void Start(string groupId)
        {
            if (BikersQuestSave.Instance != null)
            {
                BikersQuestSave.Instance.Accepted = true;
                BikersQuestSave.Instance.GroupId = groupId ?? string.Empty;
            }
            if (_instance == null)
            {
                // Fixed id so the quest is the same one after a reload.
                _instance = QuestManager.CreateQuest<BikersQuest>("bikers_docks_quest") as BikersQuest;
            }
            if (_instance == null) { Melon<Core>.Logger.Error("[quest] could not create BikersQuest"); return; }
            _instance.Begin();
            if (_instance.QuestEntries.Count > 0) _instance.QuestEntries[0].Begin();
            Melon<Core>.Logger.Msg($"[quest] begun, {_instance.QuestEntries.Count} entries");
        }
    }
}
