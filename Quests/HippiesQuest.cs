using System;
using System.Collections.Generic;
using MelonLoader;
using S1API.Internal.Abstraction;   // Saveable
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField
using UnityEngine;
#if MONO
using GameDeadDrop = ScheduleOne.Economy.DeadDrop;
#elif IL2CPP
using GameDeadDrop = Il2CppScheduleOne.Economy.DeadDrop;
#endif

namespace BetterSpecialCustomers.Quests
{
    // Krystal Bird's job: a package to carry from one dead drop to another. The shared rules (roll, start, reward,
    // reset) are in JobController. There is no target NPC; progress is read from what is in the two drops.

    // Which two drops this run uses, and how far along the package is. Saved with the game.
    public class DropRun
    {
        public string FromGuid = string.Empty;
        public string ToGuid = string.Empty;
        public string FromName = string.Empty;
        public string ToName = string.Empty;
        public bool Placed;     // the package has been put in the first drop
        public bool PickedUp;   // the package has left the first drop
    }

    public class HippiesQuestSave : Saveable
    {
        public static HippiesQuestSave Instance { get; private set; }

        public HippiesQuestSave() { Instance = this; }

        [SaveableField("hippies_quest")]
        public JobSaveData Data = new JobSaveData();

        [SaveableField("hippies_quest_drops")]
        public DropRun Drops = new DropRun();

        protected override void OnLoaded() { Instance = this; }
    }

    // The package, the two drops and the reward. Works on the game's own dead drops and inventory.
    internal static class HippiesTask
    {
        // PLACEHOLDER: an existing item stands in for the package, so no new item or model is needed.
        private const string PackageItemId = "paisleybandana";

        // The reward: grow supplies (item id, amount). The ids are best guesses; a missing one is logged and skipped.
        private static readonly KeyValuePair<string, int>[] Supplies =
        {
            new KeyValuePair<string, int>("extralonglifesoil", 3),
            new KeyValuePair<string, int>("speedgrow", 3),
            new KeyValuePair<string, int>("pgr", 1),
            new KeyValuePair<string, int>("fertilizer", 1),
        };

        // The drops must be at least this far apart, so it is a proper delivery.
        private const float MinDropDistance = 80f;

        private static HippiesQuestSave Save => HippiesQuestSave.Instance;

        private static GameDeadDrop FindDrop(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var drop in GameDeadDrop.DeadDrops)
                if (drop != null && string.Equals(drop.GUID.ToString(), guid, StringComparison.OrdinalIgnoreCase)) return drop;
            return null;
        }

        public static Vector3? PositionOf(string guid)
        {
            var drop = FindDrop(guid);
            return drop != null ? drop.transform.position : (Vector3?)null;
        }

        private static int Count(GameDeadDrop drop, string itemId)
        {
            int total = 0;
            foreach (var slot in drop.Storage.ItemSlots)
            {
                var item = slot.ItemInstance;
                if (item != null && item.Definition.ID == itemId) total += item.Quantity;
            }
            return total;
        }

        private static void RemoveAll(GameDeadDrop drop, string itemId)
        {
            foreach (var slot in drop.Storage.ItemSlots)
            {
                var item = slot.ItemInstance;
                if (item != null && item.Definition.ID == itemId) slot.ClearStoredInstance();
            }
        }

        // Called whenever the job's state changes: sets the job up when it should be running, cleans up when it shouldn't.
        public static void Refresh()
        {
            var save = Save;
            if (save == null) return;
            bool wanted = save.Data.Accepted && !save.Data.TargetKilled;
            if (wanted)
            {
                if (!save.Drops.Placed) ChooseAndPlace(save.Drops);
            }
            else
            {
                Cleanup(save);
            }
        }

        private static void ChooseAndPlace(DropRun run)
        {
            var all = new List<GameDeadDrop>();
            foreach (var drop in GameDeadDrop.DeadDrops)
                if (drop != null && drop.Storage.ItemCount == 0) all.Add(drop);
            if (all.Count < 2)
            {
                Melon<Core>.Logger.Warning("[quest] not enough empty dead drops for the hippies' job");
                return;
            }

            var from = all[UnityEngine.Random.Range(0, all.Count)];
            var far = all.FindAll(d => (d.transform.position - from.transform.position).magnitude >= MinDropDistance);
            var to = far.Count > 0 ? far[UnityEngine.Random.Range(0, far.Count)] : all.Find(d => d != from);

            var package = RewardUtil.CreateItem(PackageItemId, 1);
            if (package == null) return;
            from.Storage.InsertItem(package);

            run.FromGuid = from.GUID.ToString();
            run.ToGuid = to.GUID.ToString();
            run.FromName = from.DeadDropName;
            run.ToName = to.DeadDropName;
            run.Placed = true;
            run.PickedUp = false;
            Melon<Core>.Logger.Msg($"[quest] package placed at '{run.FromName}', to be delivered to '{run.ToName}'");
        }

        // Takes any leftover package out of the world and forgets the run.
        private static void Cleanup(HippiesQuestSave save)
        {
            var run = save.Drops;
            if (!run.Placed) return;
            var from = FindDrop(run.FromGuid);
            var to = FindDrop(run.ToGuid);
            if (from != null) RemoveAll(from, PackageItemId);
            if (to != null) RemoveAll(to, PackageItemId);
            save.Drops = new DropRun();
        }

        // Runs about once a second: moves the job along when the package leaves the first drop and reaches the second.
        public static void Tick()
        {
            try
            {
                var save = Save;
                if (save == null || !save.Data.Accepted || save.Data.TargetKilled || save.Data.Completed) return;
                var run = save.Drops;
                if (!run.Placed) return;

                var job = HippiesQuest.Job;
                if (!run.PickedUp)
                {
                    var from = FindDrop(run.FromGuid);
                    if (from == null || Count(from, PackageItemId) > 0) return;
                    run.PickedUp = true;
                    var entries = job.Quest?.QuestEntries;
                    if (entries != null && entries.Count > 1)
                    {
                        entries[0].Complete();
                        entries[1].Begin();
                    }
                    Melon<Core>.Logger.Msg("[quest] package picked up");
                }

                var to = FindDrop(run.ToGuid);
                if (to != null && Count(to, PackageItemId) > 0) job.OnObjectiveDone();
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Warning("[quest] hippies tick failed: " + ex.Message);
            }
        }

        // The reward: put the grow supplies in the player's inventory.
        public static void GiveSupplies() => RewardUtil.GiveItems(Supplies);
    }

    public class HippiesQuest : Quest
    {
        internal static readonly JobController Job = new JobController(new JobDefinition
        {
            QuestIdPrefix = "hippies_package_quest",
            RewardLogLabel = "Krystal's package job",
            CashReward = 0f,                       // the hippies pay in grow supplies, not money
            GetSave = () => HippiesQuestSave.Instance?.Data,
            PickName = () => string.Empty,         // no target to name
            RefreshTarget = HippiesTask.Refresh,
            GrantExtraReward = HippiesTask.GiveSupplies,
            CreateQuest = id => QuestManager.CreateQuest<HippiesQuest>(id),
        });

        protected override string Title => "Special Delivery";
        protected override string Description => "Krystal Bird needs a package carried between two dead drops.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            var run = HippiesQuestSave.Instance?.Drops ?? new DropRun();
            AddEntry($"Pick up the package from the dead drop at {run.FromName}", HippiesTask.PositionOf(run.FromGuid));
            AddEntry($"Deliver it to the dead drop at {run.ToName}", HippiesTask.PositionOf(run.ToGuid));
            AddEntry("Tell Krystal Bird the package has been delivered");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
        }
    }
}
