using System;
using System.Collections.Generic;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.GameTime;               // TimeManager
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
    // Krystal Bird's job: a brick of her shrooms (see KrystalsShrooms) to carry from one dead drop to another. The shared
    // rules (roll, start, reward, reset) are in JobController. There is no target NPC; progress is read from what is in
    // the two drops. Keeping the brick is allowed, but it counts as stealing and costs relationship with the hippies.

    // Which two drops this run uses, and how far along the package is. Saved with the game.
    public class DropRun
    {
        public string FromGuid = string.Empty;
        public string ToGuid = string.Empty;
        public string FromName = string.Empty;
        public string ToName = string.Empty;
        public string Package = string.Empty;  // the item placed; anything else is an older version's run
        public bool Placed;     // the package has been put in the first drop
        public bool PickedUp;   // the package has left the first drop
        public int PickedUpDay; // the game's ElapsedDays when it was picked up
        public bool Delivered;  // the package reached the second drop
        public bool Stolen;     // judged stolen and already penalised
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
        private const string PackageItemId = KrystalsShrooms.ProductId;

        // What older versions of the mod put in the drop, so a run saved by them can be swapped for the brick.
        private const string LegacyPackageItemId = "paisleybandana";

        // Not delivered this many game days after pickup (or before the hippies leave town): the brick counts as stolen.
        private const int DeadlineDays = 2;

        // Relationship lost for stealing the brick (the scale is 0 to 5; killing a group member also costs 1.0).
        private const float TheftPenalty = 1f;

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

        private static bool HasBrick(GameDeadDrop drop)
        {
            if (drop == null) return false;
            foreach (var slot in drop.Storage.ItemSlots)
                if (KrystalsShrooms.IsBrick(slot.ItemInstance)) return true;
            return false;
        }

        // The hippies collect the brick: it leaves the drop the moment it is delivered, so it can't be taken back out.
        private static void CollectBrick(GameDeadDrop drop)
        {
            foreach (var slot in drop.Storage.ItemSlots)
            {
                if (!KrystalsShrooms.IsBrick(slot.ItemInstance)) continue;
                slot.ClearStoredInstance();
                return;
            }
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
            bool wanted = save.Data.Accepted && !save.Data.TargetKilled && !save.Data.Completed;
            if (wanted)
            {
                if (!save.Drops.Placed) ChooseAndPlace(save.Drops);
                else MigrateLegacyRun(save.Drops);
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

            var package = KrystalsShrooms.CreateBrick();
            if (package == null) return;
            from.Storage.InsertItem(package);

            run.FromGuid = from.GUID.ToString();
            run.ToGuid = to.GUID.ToString();
            run.FromName = from.DeadDropName;
            run.ToName = to.DeadDropName;
            run.Package = PackageItemId;
            run.Placed = true;
            run.PickedUp = false;
            Melon<Core>.Logger.Msg($"[quest] package placed at '{run.FromName}', to be delivered to '{run.ToName}'");
        }

        // A run saved by an older version carries a bandana. Swap it for a brick in the same first drop, so the brick
        // missing from that drop isn't mistaken for a theft.
        private static void MigrateLegacyRun(DropRun run)
        {
            if (run.Package == PackageItemId || !KrystalsShrooms.IsRegistered) return;
            var from = FindDrop(run.FromGuid);
            var to = FindDrop(run.ToGuid);
            if (from != null) RemoveAll(from, LegacyPackageItemId);
            if (to != null) RemoveAll(to, LegacyPackageItemId);
            var package = KrystalsShrooms.CreateBrick();
            if (from == null || package == null) return;
            from.Storage.InsertItem(package);
            run.Package = PackageItemId;
            run.PickedUp = false;
            run.Delivered = false;
            Melon<Core>.Logger.Msg($"[quest] swapped an older run's package for a brick at '{run.FromName}'");
        }

        // The brick has been picked up and isn't in either drop.
        private static bool IsMissing(DropRun run) =>
            run.PickedUp && !run.Delivered && !HasBrick(FindDrop(run.FromGuid)) && !HasBrick(FindDrop(run.ToGuid));

        // The brick is gone for good: the hippies lose relationship and the product shows up in the player's Product Manager.
        private static void PenalizeTheft(HippiesQuestSave save)
        {
            save.Drops.Stolen = true;
            string group = save.Data.GroupId;
            if (!string.IsNullOrEmpty(group)) GroupRelationships.Penalize(group, TheftPenalty, "stole Krystal's shrooms");
            KrystalsShrooms.Discover();
        }

        // Takes any leftover package out of the world and forgets the run. Runs when the job ends for any reason, so a
        // brick that left with the player when the hippies left town is judged stolen here.
        private static void Cleanup(HippiesQuestSave save)
        {
            var run = save.Drops;
            if (!run.Placed) return;
            bool legacy = run.Package != PackageItemId;
            if (!legacy && !run.Stolen && IsMissing(run)) PenalizeTheft(save);
            string package = legacy ? LegacyPackageItemId : PackageItemId;
            var from = FindDrop(run.FromGuid);
            var to = FindDrop(run.ToGuid);
            if (from != null) RemoveAll(from, package);
            if (to != null) RemoveAll(to, package);
            save.Drops = new DropRun();
        }

        // Runs about once a second: moves the job along when the package leaves the first drop and reaches the second,
        // and fails it once the deadline passes.
        public static void Tick()
        {
            try
            {
                var save = Save;
                if (save == null || !save.Data.Accepted || save.Data.TargetKilled || save.Data.Completed) return;
                var run = save.Drops;
                if (!run.Placed) return;
                MigrateLegacyRun(run);
                if (run.Package != PackageItemId) return;

                var job = HippiesQuest.Job;
                if (!run.PickedUp)
                {
                    var from = FindDrop(run.FromGuid);
                    if (from == null || HasBrick(from)) return;
                    run.PickedUp = true;
                    run.PickedUpDay = TimeManager.ElapsedDays;
                    var entries = job.Quest?.QuestEntries;
                    if (entries != null && entries.Count > 1)
                    {
                        entries[0].Complete();
                        entries[1].Begin();
                    }
                    Melon<Core>.Logger.Msg("[quest] package picked up");
                }

                var to = FindDrop(run.ToGuid);
                if (HasBrick(to))
                {
                    CollectBrick(to);
                    run.Delivered = true;
                    job.OnObjectiveDone();
                    return;
                }

                if (TimeManager.ElapsedDays - run.PickedUpDay < DeadlineDays) return;
                if (IsMissing(run))
                {
                    Melon<Core>.Logger.Msg("[quest] deadline passed, the brick was stolen");
                    PenalizeTheft(save);
                }
                else
                {
                    Melon<Core>.Logger.Msg("[quest] deadline passed, the brick was put back undelivered");
                }
                job.Fail();
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
            // no CashReward: the hippies pay in grow supplies, not money
            GetSave = () => HippiesQuestSave.Instance?.Data,
            PickName = () => string.Empty,         // no target to name
            RefreshTarget = HippiesTask.Refresh,
            GrantExtraReward = HippiesTask.GiveSupplies,
            CreateQuest = id => QuestManager.CreateQuest<HippiesQuest>(id),
        });

        protected override string Title => "Special Delivery";
        protected override string Description => "Krystal Bird needs a brick of her shrooms carried between two dead drops. Keeping it would be stealing.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            var run = HippiesQuestSave.Instance?.Drops ?? new DropRun();
            AddEntry($"Pick up Krystal's shrooms from the dead drop at {run.FromName}", HippiesTask.PositionOf(run.FromGuid));
            AddEntry($"Deliver the brick to the dead drop at {run.ToName} within 2 days", HippiesTask.PositionOf(run.ToGuid));
            AddEntry("Tell Krystal Bird the package has been delivered");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
        }
    }
}
