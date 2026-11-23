using System;
using System.Collections.Generic;   // List
using MelonLoader;
using S1API.Internal.Abstraction;   // Saveable
using S1API.Map;                    // Region
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField
using UnityEngine;                  // Vector3, Mathf
#if MONO
using ScheduleOne.Cartel;           // CartelRegionActivities, CartelAmbushLocation
#elif IL2CPP
using Il2CppScheduleOne.Cartel;
#endif

namespace BetterSpecialCustomers.Quests
{
    // Diesel Rodd's second job: "Club Brawl". A rival club has gathered somewhere in town, and the player has to
    // take them all out. The shared rules (roll, start, reward, reset) are in JobController. The rivals themselves
    // are our own NPCs (see RivalBiker.cs); only the cartel's ambush SPOTS are borrowed.

    // What this run needs to remember. Saved with the game.
    public class BrawlRun
    {
        public int Region = -1;         // which region the rivals are in (-1 = not picked yet)
        public int LocationIndex = -1;  // which ambush spot in that region (-1 = not picked yet)
        public bool Spawned;            // are the rivals out right now?
    }

    public class ClubBrawlQuestSave : Saveable
    {
        public static ClubBrawlQuestSave Instance { get; private set; }

        public ClubBrawlQuestSave() { Instance = this; }

        // These names must be unique! They're how the save file finds this data.
        [SaveableField("clubbrawl_quest")]
        public JobSaveData Data = new JobSaveData();

        [SaveableField("clubbrawl_quest_run")]
        public BrawlRun Run = new BrawlRun();

        protected override void OnLoaded()
        {
            Instance = this;
            // The rivals' fight isn't saved, so after a reload they need to come out again when the player comes back.
            if (Run.Spawned && !Data.TargetKilled) Run.Spawned = false;
        }
    }

    internal static class ClubBrawlTask
    {
        private static ClubBrawlQuestSave Save => ClubBrawlQuestSave.Instance;

        // The rivals fighting in this brawl. Not saved (see ClubBrawlQuestSave.OnLoaded).
        private static readonly List<RivalBiker> _rivals = new List<RivalBiker>();

        // True while the job is open and the rivals haven't been beaten yet.
        private static bool IsRunning()
        {
            var save = Save;
            return save != null && save.Data.Accepted && !save.Data.TargetKilled && !save.Data.Completed;
        }

        // The region's name for the quest text and Diesel's line, e.g. "Westville".
        public static string RegionName()
        {
            var save = Save;
            if (save == null || save.Run.Region < 0) return "town";
            return ((Region)save.Run.Region).ToString();
        }

        // ---- Picking the spot ----

        // The ambush spots in one region, or null if the region has none.
        private static CartelAmbushLocation[] SpotsIn(int region)
        {
            foreach (var activities in UnityEngine.Object.FindObjectsOfType<CartelRegionActivities>(true))
            {
                // The game and S1API number regions the same way, so we compare them as numbers.
                if ((int)activities.Region != region) continue; // not our region, skip to the next one

                var spots = activities.AmbushLocations;
                if (spots == null || spots.Length == 0) return null;

                // Copy them into a normal array, so this works the same on Mono and IL2CPP.
                var result = new CartelAmbushLocation[spots.Length];
                for (int i = 0; i < spots.Length; i++) result[i] = spots[i];
                return result;
            }
            return null;
        }

        // Picks where the brawl happens. Called once, when the job starts.
        private static void PickSpot(BrawlRun run)
        {
            // Shuffle the regions, then take the first one that has ambush spots.
            // TODO: only use regions the player has unlocked.
            var regions = new List<Region>((Region[])Enum.GetValues(typeof(Region)));
            for (int i = regions.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (regions[i], regions[j]) = (regions[j], regions[i]); // swap two regions
            }

            foreach (var region in regions)
            {
                var spots = SpotsIn((int)region);
                if (spots == null) continue; // no spots here, try the next region

                run.Region = (int)region;
                run.LocationIndex = UnityEngine.Random.Range(0, spots.Length);
                Melon<Core>.Logger.Msg($"[quest] club brawl picked {RegionName()}, spot {run.LocationIndex} at {spots[run.LocationIndex].transform.position}");
                return; // found one, stop looking
            }

            Melon<Core>.Logger.Warning("[quest] club brawl: no region has any ambush spots!");
        }

        // Finds this run's ambush spot again from the saved region and index. Returns null if it can't.
        private static CartelAmbushLocation FindSpot(BrawlRun run)
        {
            if (run.Region < 0 || run.LocationIndex < 0) return null;
            var spots = SpotsIn(run.Region);
            if (spots == null || run.LocationIndex >= spots.Length) return null;
            return spots[run.LocationIndex];
        }

        // ---- The fight ----

        // The first player within the spot's detection radius, or null if there isn't one.
        private static S1API.Entities.Player PlayerInZone(CartelAmbushLocation spot)
        {
            Vector3 centre = spot.transform.position;
            foreach (var player in S1API.Entities.Player.All)
            {
                if (player == null) continue;
                if (Vector3.Distance(player.Position, centre) <= spot.DetectionRadius) return player;
            }
            return null;
        }

        // Brings one rival to each ambush point and sends them all at the player who walked in.
        private static void DeployRivals(CartelAmbushLocation spot, S1API.Entities.Player target, BrawlRun run)
        {
            if (RivalBiker.All.Count == 0)
            {
                Melon<Core>.Logger.Warning("[quest] club brawl: no rival bikers exist, check they were created on load");
                return;
            }

            var points = spot.AmbushPoints;
            int count = Mathf.Min(points.Length, RivalBiker.All.Count); // never more rivals than we have

            _rivals.Clear();
            for (int i = 0; i < count; i++)
            {
                var rival = RivalBiker.All[i];
                rival.Deploy(points[i].position);
                rival.Attack(target);
                _rivals.Add(rival);
            }

            run.Spawned = true;
            Melon<Core>.Logger.Msg($"[quest] club brawl: {_rivals.Count} rivals deployed");
        }

        // Called about once a second from Core.OnUpdate (like HippiesTask.Tick).
        public static void Tick()
        {
            try
            {
                if (!IsRunning()) return;
                var run = Save.Run;

                // Waiting for the player to walk into the spot.
                if (!run.Spawned)
                {
                    var spot = FindSpot(run);
                    if (spot == null) return;
                    var target = PlayerInZone(spot);
                    if (target != null) DeployRivals(spot, target, run);
                    return;
                }

                // The fight is on: once every rival is down, the job's done.
                if (_rivals.Count > 0 && _rivals.TrueForAll(r => r.IsDown))
                {
                    Melon<Core>.Logger.Msg("[quest] club brawl: all rivals are down");
                    ClubBrawlQuest.Job.OnObjectiveDone();
                }
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error($"[quest] club brawl tick failed: {ex}");
            }
        }

        // Called when the job's state changes (started, finished, dropped).
        public static void Refresh()
        {
            var save = Save;
            if (save == null) return;

            if (IsRunning())
            {
                if (save.Run.Region < 0) PickSpot(save.Run); // the job just started: choose where
            }
            else
            {
                // The job isn't running: put the rivals away and forget this run.
                foreach (var rival in RivalBiker.All) rival.Park();
                _rivals.Clear();
                save.Run = new BrawlRun();
            }
        }
    }

    public class ClubBrawlQuest : Quest
    {
        internal static readonly JobController Job = new JobController(new JobDefinition
        {
            QuestIdPrefix = "bikers_brawl_quest",
            RewardLogLabel = "Diesel's club brawl job",
            CashReward = () => ModConfig.BikersCashReward.Value, // TODO: give Club Brawl its own (bigger) setting
            GetSave = () => ClubBrawlQuestSave.Instance?.Data,
            PickName = () => string.Empty,   // no target to name
            RefreshTarget = ClubBrawlTask.Refresh,
            CreateQuest = id => QuestManager.CreateQuest<ClubBrawlQuest>(id),
        });

        protected override string Title => "Club Brawl";
        protected override string Description => "A rival club is getting too comfortable in town. Diesel Rodd wants you to teach them a lesson.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            AddEntry($"Find the rival club in {ClubBrawlTask.RegionName()} and take them out");
            AddEntry("Tell Diesel the rivals are dealt with");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
        }
    }
}