using System;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.Entities;               // NPC
using S1API.Internal.Abstraction;   // Saveable
using S1API.Leveling;               // LevelManager
using S1API.Money;                  // Money
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

        // The informant is dead; Diesel still has to be told.
        [SaveableField("bikers_quest_target_killed")]
        public bool TargetKilled;

        // Reported back to Diesel, quest finished and rewarded.
        [SaveableField("bikers_quest_completed")]
        public bool Completed;

        // This visit's roll for whether Diesel has a job: 0 = not asked yet, 1 = he has one, 2 = nothing this time.
        [SaveableField("bikers_quest_offer")]
        public int OfferState;

        // How many times the quest has been started. Gives each run its own quest id.
        [SaveableField("bikers_quest_runs")]
        public int Runs;

        protected override void OnLoaded()
        {
            Instance = this;
            Informant.Refresh(); // the informant may have been created before the save finished loading
        }
    }

    // The target. S1API creates this NPC when the world loads, and there is no way to spawn or remove it later,
    // so instead it is "parked" far away and made invincible until the quest needs him, then moved to his hiding place.
    // PLACEHOLDER: ParkedPosition is a guess; if he falls out of the world or gets stuck, pick a safer spot.
    public sealed class Informant : NPC
    {
        public static readonly Vector3 HidingPosition = new Vector3(-78.2f, -2.3f, -33.2f);
        public static readonly Vector3 ParkedPosition = new Vector3(0f, -200f, 0f);

        private static Informant _instance;

        public override bool IsPhysical => true;

        public Informant() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity("bikers_informant", "Club", "Informant")
                   .WithSpawnPosition(ParkedPosition);
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();
            _instance = this;

            // Vanilla's Quest_DefeatCartel only counts a kill (IsDead), so we do the same.
            OnDeath += BikersQuest.OnTargetKilled;
            Refresh();
        }

        // Puts him at his hiding place while the job is open and he is still alive, otherwise parks him.
        public static void Refresh()
        {
            if (_instance == null) return;
            var save = BikersQuestSave.Instance;
            bool wanted = save != null && save.Accepted && !save.TargetKilled;
            if (wanted) _instance.Deploy(); else _instance.Park();
        }

        private void Deploy()
        {
            if (IsDead) Revive();
            Heal((int)MaxHealth);
            IsInvincible = false;
            if ((Position - HidingPosition).sqrMagnitude > 1f) Position = HidingPosition;
        }

        private void Park()
        {
            IsInvincible = true;
            if ((Position - ParkedPosition).sqrMagnitude > 1f) Position = ParkedPosition;
        }
    }

    public class BikersQuest : Quest
    {
        // Chance (0 to 1) that Diesel has a job on a given visit.
        public const float OfferChance = 0.5f;

        // Rewards for finishing the job. The relationship bonus takes effect from the group's next visit.
        private const float RelationshipReward = 0.5f;
        private const float CashReward = 5000f;
        private const int XpReward = 100;

        private static BikersQuest _instance;

        protected override string Title => "Loose Lips";
        protected override string Description => "Someone in the club has been talking to the cops. Diesel Rodd wants the informant dealt with.";
        // S1API finishes setting a quest up (OnCreated, entries) on the NEXT frame after CreateQuest, so we can't call
        // Begin() ourselves straight away. We only create this quest when Diesel gives the job, so let it begin itself.
        protected override bool AutoBegin => true;

        protected override void OnCreated()
        {
            base.OnCreated();
            _instance = this;
            var kill = AddEntry("Find and kill the informant", Informant.HidingPosition);
            kill.SetPOIToNPC<Informant>(); // make the marker follow him (returns false if he isn't found)
            AddEntry("Tell Diesel the informant is dealt with");
            Subscribe();
            Melon<Core>.Logger.Msg("[quest] BikersQuest created");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            // S1API only reloads quests that were still active, so this is the open job.
            _instance = this;
            Subscribe();
        }

        // Quest.OnComplete is an event, not a method to override. Unsubscribe first so we never subscribe twice.
        private void Subscribe()
        {
            OnComplete -= HandleComplete;
            OnComplete += HandleComplete;
        }

        // Called by Informant when its health reaches zero.
        public static void OnTargetKilled()
        {
            var save = BikersQuestSave.Instance;
            if (_instance == null || save == null || !save.Accepted || save.TargetKilled) return;
            save.TargetKilled = true;
            if (_instance.QuestEntries.Count > 0) _instance.QuestEntries[0].Complete();
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Begin();
            Melon<Core>.Logger.Msg("[quest] informant killed, return to Diesel");
        }

        // Called from the "He's dealt with." dialogue choice.
        public static void HandIn()
        {
            var save = BikersQuestSave.Instance;
            if (_instance == null || save == null || !save.TargetKilled || save.Completed) return;
            save.Completed = true;
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Complete();
            _instance.Complete();
            Informant.Refresh();
        }

        private void HandleComplete()
        {
            Money.ChangeCashBalance(CashReward, true, true);
            LevelManager.AddXP(XpReward);
            Melon<Core>.Logger.Msg($"[quest] paid ${CashReward} and {XpReward} XP");

            string group = BikersQuestSave.Instance?.GroupId;
            if (!string.IsNullOrEmpty(group))
                GroupRelationships.Reward(group, RelationshipReward, "completed Diesel's informant job");
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
            if (BikersQuestSave.Instance != null) BikersQuestSave.Instance.Runs++;
            if (_instance == null)
            {
                // Each run gets its own id, so a new run never collides with an earlier finished one.
                int run = BikersQuestSave.Instance?.Runs ?? 0;
                _instance = QuestManager.CreateQuest<BikersQuest>($"bikers_informant_quest_{run}") as BikersQuest;
            }
            if (_instance == null) { Melon<Core>.Logger.Error("[quest] could not create BikersQuest"); return; }
            Informant.Refresh();
            Melon<Core>.Logger.Msg("[quest] created; S1API sets up its entries and begins it on the next frame");
        }

        // Called when a customer group leaves. The job is once per visit: when Diesel's group leaves, an open job is
        // dropped and the quest can be taken again on their next visit.
        public static void ResetForNewVisit(string groupId)
        {
            var save = BikersQuestSave.Instance;
            if (save == null) return;
            if (!string.Equals(save.GroupId, groupId, StringComparison.OrdinalIgnoreCase)) return;

            if (_instance != null && save.Accepted && !save.Completed) _instance.Cancel();
            _instance = null;
            save.OfferState = 0;
            save.Accepted = false;
            save.TargetKilled = false;
            save.Completed = false;
            Informant.Refresh();
            Melon<Core>.Logger.Msg("[quest] group left, the job can be taken again next visit");
        }
    }
}
