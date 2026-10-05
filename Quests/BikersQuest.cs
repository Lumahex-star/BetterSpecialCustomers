using System;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.Entities;               // NPC
using S1API.Entities.Appearances.AccessoryFields;   // Chest, Feet, Neck
using S1API.Entities.Appearances.BodyLayerFields;   // Shirts, Pants
using S1API.Entities.Appearances.CustomizationFields; // SkinColor, HairStyle, ...
using S1API.Entities.Appearances.FaceLayerFields;   // Face, FacialHair
using S1API.Internal.Abstraction;   // Saveable
using S1API.Leveling;               // LevelManager
using S1API.Map;                    // Building
using S1API.Money;                  // Money
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField
using UnityEngine;
#if MONO
using GameNPC = ScheduleOne.NPCs.NPC;
using GameBuilding = ScheduleOne.Map.NPCEnterableBuilding;
#elif IL2CPP
using GameNPC = Il2CppScheduleOne.NPCs.NPC;
using GameBuilding = Il2CppScheduleOne.Map.NPCEnterableBuilding;
#endif

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

        // This run's target, so he keeps the same name after a reload.
        [SaveableField("bikers_quest_target_name")]
        public string TargetName = string.Empty;

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
        private const string NpcId = "bikers_informant";
        private const string HideoutName = "The Piss Hut";
        public static readonly Vector3 ParkedPosition = new Vector3(0f, -200f, 0f);

        private static Informant _instance;
        private static int _lookAppliedForRun = -1; // in-memory only: which run his random look was made for

        private static readonly string[] FirstNames =
            { "Dale", "Rusty", "Vince", "Eddie", "Skeet", "Lenny", "Wade", "Cody", "Marv", "Gus", "Terry", "Bo" };
        private static readonly string[] LastNames =
            { "Hollis", "Brandt", "Keane", "Mercer", "Voss", "Pruitt", "Lund", "Garrity", "Boone", "Sykes", "Dunn", "Rourke" };

        // A fresh random name for each run, so it is never the same person twice.
        public static string PickName() =>
            FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)] + " " + LastNames[UnityEngine.Random.Range(0, LastNames.Length)];

        public override bool IsPhysical => true;

        public Informant() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity("bikers_informant", "Informant", string.Empty)
                   .WithSpawnPosition(ParkedPosition)
                   // Once he has come out (summoned by a knock) he walks back in and stays in.
                   .WithSchedule(plan => plan.StayInBuilding(Building.Get<S1API.Map.Buildings.ThePissHut>(), 0, 1439));
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();
            _instance = this;

            // Vanilla's Quest_DefeatCartel only counts a kill (IsDead), so we do the same.
            OnDeath += BikersQuest.OnTargetKilled;
            Schedule.InitializeActions(); // the schedule is switched on/off in Deploy/Park
            Refresh();
        }

        // Puts him inside The Piss Hut while the job is open and he is still alive, otherwise parks him.
        public static void Refresh()
        {
            if (_instance == null) return;
            var save = BikersQuestSave.Instance;
            bool wanted = save != null && save.Accepted && !save.TargetKilled;
            if (wanted) _instance.Deploy(); else _instance.Park();
        }

        private void Deploy()
        {
            var save = BikersQuestSave.Instance;
            if (save != null && !string.IsNullOrEmpty(save.TargetName))
            {
                int split = save.TargetName.IndexOf(' ');
                FirstName = split > 0 ? save.TargetName.Substring(0, split) : save.TargetName;
                LastName = split > 0 ? save.TargetName.Substring(split + 1) : string.Empty;
            }
            if (save != null && _lookAppliedForRun != save.Runs)
            {
                _lookAppliedForRun = save.Runs;
                ApplyBikerLook(); // a different face each run, but always a believable biker
            }

            if (IsDead) Revive();
            Heal((int)MaxHealth);
            IsInvincible = false;
            EnterHideout();
            Schedule.Enable(); // after a summon he returns to the hut by himself
        }

        private void Park()
        {
            Schedule.Disable(); // a parked NPC has no navmesh to walk on
            LeaveHideout();
            IsInvincible = true;
            if ((Position - ParkedPosition).sqrMagnitude > 1f) Position = ParkedPosition;
        }

        // ---- Look ----------------------------------------------------------------------------------------------
        // Replaces S1API's fully random look (random skin and clothing colours looked wrong) with a controlled one:
        // a natural skin tone and hair colour, a plain tee, jeans, a leather vest and boots.

        private static readonly Color32[] SkinTones =
        {
            new Color32(236, 200, 170, 255), new Color32(224, 172, 138, 255), new Color32(198, 140, 100, 255),
            new Color32(168, 112, 78, 255), new Color32(130, 84, 58, 255),
        };
        private static readonly Color32[] HairColors =
        {
            new Color32(30, 22, 18, 255), new Color32(60, 40, 28, 255), new Color32(95, 65, 40, 255),
            new Color32(150, 120, 80, 255), new Color32(120, 120, 120, 255),
        };
        private static readonly string[] HairStyles =
        {
            HairStyle.BuzzCut, HairStyle.CloseBuzzCut, HairStyle.Balding, HairStyle.Receding,
            HairStyle.LongSlicked, HairStyle.ShoulderLength, HairStyle.Peaked,
        };
        private static readonly Color32[] ShirtColors =
        {
            new Color32(25, 25, 25, 255), new Color32(60, 60, 60, 255), new Color32(110, 25, 25, 255), new Color32(90, 90, 90, 255),
        };
        private static readonly Color32[] JeansColors =
        {
            new Color32(30, 38, 60, 255), new Color32(20, 20, 24, 255), new Color32(55, 65, 90, 255),
        };

        private static T Pick<T>(T[] items) => items[UnityEngine.Random.Range(0, items.Length)];

        private void ApplyBikerLook()
        {
            Color hair = Pick(HairColors);

            Appearance.Set<Gender>(0f);
            Appearance.Set<Height>(UnityEngine.Random.Range(0.95f, 1.05f));
            Appearance.Set<Weight>(UnityEngine.Random.Range(0.45f, 0.75f));
            Appearance.Set<SkinColor>((Color)Pick(SkinTones));
            Appearance.Set<EyeBallTint>(Color.white);
            Appearance.Set<HairColor>(hair);
            Appearance.Set<HairStyle>(Pick(HairStyles));

            Color featureColor = new Color(0.15f, 0.1f, 0.1f);
            Appearance.WithFaceLayer<Face>(UnityEngine.Random.value < 0.5f ? Face.Neutral : Face.SlightFrown, featureColor);
            if (UnityEngine.Random.value < 0.6f)
                Appearance.WithFaceLayer<FacialHair>(UnityEngine.Random.value < 0.5f ? FacialHair.Stubble : FacialHair.Goatee, hair);

            Appearance.WithBodyLayer<Shirts>(Shirts.TShirt, (Color)Pick(ShirtColors));
            Appearance.WithBodyLayer<Pants>(Pants.Jeans, (Color)Pick(JeansColors));
            Appearance.WithAccessoryLayer<Chest>(Chest.OpenVest, new Color(0.12f, 0.09f, 0.07f)); // the leather "cut"
            Appearance.WithAccessoryLayer<Feet>(Feet.CombatBoots, new Color(0.1f, 0.1f, 0.1f));
            if (UnityEngine.Random.value < 0.4f)
                Appearance.WithAccessoryLayer<Neck>(Neck.GoldChain, Color.white);
        }

        // ---- Hiding in a building ----------------------------------------------------------------------------
        // Entering a building hides an NPC (its avatar is switched off) and parks it at the door; knocking on that
        // door summons it back out for a few seconds. The Piss Hut needs no interior for this to work.

        private static GameNPC FindGameNpc()
        {
            foreach (var n in UnityEngine.Object.FindObjectsOfType<GameNPC>(true))
                if (n != null && n.ID == NpcId) return n;
            return null;
        }

        private static GameBuilding FindHideout()
        {
            foreach (var b in UnityEngine.Object.FindObjectsOfType<GameBuilding>(true))
                if (b != null && string.Equals(b.BuildingName, HideoutName, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        private static void EnterHideout()
        {
            var npc = FindGameNpc();
            var hut = FindHideout();
            if (npc == null || hut == null || hut.Doors.Length == 0)
            {
                Melon<Core>.Logger.Warning($"[quest] could not hide the informant (npc found: {npc != null}, building found: {hut != null})");
                return;
            }
            if (npc.CurrentBuilding == hut) return;
            if (npc.isInBuilding) npc.ExitBuilding();
            npc.EnterBuilding(null, hut.GUID.ToString(), 0);
        }

        private static void LeaveHideout()
        {
            var npc = FindGameNpc();
            if (npc != null && npc.isInBuilding) npc.ExitBuilding();
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
            string name = BikersQuestSave.Instance?.TargetName;
            if (string.IsNullOrEmpty(name)) name = "the informant";
            var kill = AddEntry($"Knock on the door of The Piss Hut and kill {name}");
            kill.SetPOIToNPC<Informant>(); // the marker follows him; while hidden he sits at the hut's door
            AddEntry($"Tell Diesel {name} is dealt with");
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
            if (BikersQuestSave.Instance != null)
            {
                BikersQuestSave.Instance.Runs++;
                BikersQuestSave.Instance.TargetName = Informant.PickName();
            }
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
            save.TargetName = string.Empty;
            save.Accepted = false;
            save.TargetKilled = false;
            save.Completed = false;
            Informant.Refresh();
            Melon<Core>.Logger.Msg("[quest] group left, the job can be taken again next visit");
        }
    }
}
