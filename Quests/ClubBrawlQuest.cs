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
    //
    // How it plays: when the job starts, the rivals gather at the spot (marked on the map) and hang around. When the
    // player gets close, or picks a fight with any of them, the whole gang attacks.

    // What this run needs to remember. Saved with the game.
    public class BrawlRun
    {
        public int Region = -1;         // which region the rivals are in (-1 = not picked yet)
        public int LocationIndex = -1;  // which ambush spot in that region (-1 = not picked yet)
        public bool Spawned;            // have the rivals gathered at the spot?
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
            // The rivals aren't saved, so after a reload they gather at the spot again.
            if (Run.Spawned && !Data.TargetKilled) Run.Spawned = false;
        }
    }

    internal static class ClubBrawlTask
    {
        // How close the player has to get before the gang attacks (or the spot's own radius, if that's bigger).
        private const float AggroDistance = 15f;

        private static ClubBrawlQuestSave Save => ClubBrawlQuestSave.Instance;

        // The rivals at the spot this run, and whether they've started fighting. Not saved (see OnLoaded above).
        private static readonly List<RivalBiker> _rivals = new List<RivalBiker>();
        private static bool _fighting;
        private static CartelAmbushLocation _spot; // remembered, so we don't search for it every second

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

        // Where the brawl is, for the quest marker. Vector3.zero if there's no spot yet.
        public static Vector3 SpotPosition()
        {
            var spot = GetSpot();
            return spot != null ? spot.transform.position : Vector3.zero;
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

        // This run's ambush spot, found once from the saved region and index. Null if it can't be found.
        private static CartelAmbushLocation GetSpot()
        {
            if (_spot != null) return _spot;

            var run = Save?.Run;
            if (run == null || run.Region < 0 || run.LocationIndex < 0) return null;
            var spots = SpotsIn(run.Region);
            if (spots == null || run.LocationIndex >= spots.Length) return null;

            _spot = spots[run.LocationIndex];
            return _spot;
        }

        // ---- The gang ----

        // The player nearest to a position, or null if there are no players.
        private static S1API.Entities.Player NearestPlayer(Vector3 position)
        {
            S1API.Entities.Player nearest = null;
            float best = float.MaxValue;
            foreach (var player in S1API.Entities.Player.All)
            {
                if (player == null) continue;
                float distance = Vector3.Distance(player.Position, position);
                if (distance < best) { best = distance; nearest = player; }
            }
            return nearest;
        }

        // Brings one rival to each ambush point, where they hang around until provoked.
        private static void GatherRivals(CartelAmbushLocation spot, BrawlRun run)
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
                _rivals.Add(rival);
            }

            _fighting = false;
            run.Spawned = true;
            Melon<Core>.Logger.Msg($"[quest] club brawl: {_rivals.Count} rivals gathered in {RegionName()}");
        }

        // Has the gang been provoked? The player came close, or picked a fight with (or knocked down) one of them.
        private static bool IsProvoked(CartelAmbushLocation spot, out S1API.Entities.Player target)
        {
            Vector3 centre = spot.transform.position;
            target = NearestPlayer(centre);
            if (target == null) return false;

            float aggroRange = Mathf.Max(AggroDistance, spot.DetectionRadius);
            if (Vector3.Distance(target.Position, centre) <= aggroRange) return true;

            foreach (var rival in _rivals)
                if (rival.IsFighting || rival.IsDown) return true;

            return false;
        }

        // Called about once a second from Core.OnUpdate (like HippiesTask.Tick).
        public static void Tick()
        {
            try
            {
                if (!IsRunning()) return;
                var run = Save.Run;
                var spot = GetSpot();
                if (spot == null) return;

                // 1. The job has started: the rivals gather at the spot.
                if (!run.Spawned)
                {
                    GatherRivals(spot, run);
                    return;
                }

                // 2. They hang around until provoked, then the whole gang attacks.
                if (!_fighting)
                {
                    if (IsProvoked(spot, out var target))
                    {
                        _fighting = true;
                        foreach (var rival in _rivals)
                            if (!rival.IsDown) rival.Attack(target);
                        Melon<Core>.Logger.Msg("[quest] club brawl: the rivals are attacking!");
                    }
                    return;
                }

                // 3. The fight is on: once every rival is down, the job's done.
                foreach (var rival in _rivals)
                {
                    if (rival.IsDown || rival.IsFighting) continue; // already down, or already fighting
                    var target = NearestPlayer(rival.Position);
                    if (target == null) continue;
                    rival.Attack(target);
                    if (ModConfig.DebugMode.Value) Melon<Core>.Logger.Msg($"[quest] club brawl: sent {rival.FirstName} back into the fight");
                }

                // Once every rival is down, the job's done.
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
                _fighting = false;
                _spot = null;
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
            var find = AddEntry($"Find the rival club in {ClubBrawlTask.RegionName()} and take them out");
            AddEntry("Tell Diesel the rivals are dealt with");
            SetMarker(find);
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
            if (QuestEntries.Count > 0) SetMarker(QuestEntries[0]);
        }

        // Puts the quest marker (map and compass) on the brawl spot.
        private static void SetMarker(QuestEntry entry)
        {
            Vector3 position = ClubBrawlTask.SpotPosition();
            if (position != Vector3.zero) entry.POIPosition = position;
            else Melon<Core>.Logger.Warning("[quest] club brawl: no spot found for the quest marker");
        }
    }
}