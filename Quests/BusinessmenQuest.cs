using System;
using S1API.Entities;               // NPC
using S1API.Entities.Appearances.AccessoryFields;     // Chest, Feet, Neck, Head, Hands
using S1API.Entities.Appearances.BodyLayerFields;     // Shirts, Pants
using S1API.Entities.Appearances.CustomizationFields; // SkinColor, HairStyle, ...
using S1API.Entities.Appearances.FaceLayerFields;     // Face, FacialHair
using S1API.Internal.Abstraction;   // Saveable
using S1API.Map;                    // Building
using S1API.Quests;                 // Quest
using S1API.Saveables;              // SaveableField
using UnityEngine;

namespace BetterSpecialCustomers.Quests
{
    // Johnny Smith's job: an auditor with a strict daily routine. The shared rules (roll, start, reward, reset) are in
    // JobController; this file holds only what is specific to the businessmen.

    // Saved progress. S1API saves the one SaveableField as JSON.
    public class BusinessmenQuestSave : Saveable
    {
        public static BusinessmenQuestSave Instance { get; private set; }

        public BusinessmenQuestSave() { Instance = this; }

        [SaveableField("businessmen_quest")]
        public JobSaveData Data = new JobSaveData();

        protected override void OnLoaded()
        {
            Instance = this;
            Auditor.Refresh();
        }
    }

    // The auditor. Like the informant he is created with the world and then "parked" until needed. Unlike the informant
    // he has a daily routine: while a job is open he starts hidden at home and then follows his schedule around town.
    public sealed class Auditor : NPC
    {
        private const string NpcId = "businessmen_auditor";
        private const string HomeName = "Upscale Apartments";

        private static Auditor _instance;
        private static int _lookAppliedForRun = -1;

        private static readonly string[] FirstNames =
            { "Gordon", "Nigel", "Howard", "Clive", "Warren", "Preston", "Desmond", "Edwin", "Leonard", "Franklin" };
        private static readonly string[] LastNames =
            { "Whitlock", "Ashcroft", "Pembroke", "Thorne", "Hargrove", "Fairbanks", "Lockhart", "Sterling", "Winslow", "Crane" };

        public static string PickName() => QuestNpcUtil.RandomName(FirstNames, LastNames);

        public override bool IsPhysical => true;

        public Auditor() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity(NpcId, "Auditor", string.Empty)
                   .WithSpawnPosition(QuestNpcUtil.ParkedPosition)
                   .WithSchedule(plan =>
                   {
                       // Times are HHMM; durations are in minutes.
                       var home = Building.Get<S1API.Map.Buildings.UpscaleApartments>();
                       var office = Building.Get<S1API.Map.Buildings.TallTower>();
                       var lunch = Building.Get<S1API.Map.Buildings.Cafe>();
                       var dinner = Building.Get<S1API.Map.Buildings.ChineseRestaurant>();
                       plan.StayInBuilding(home, 0, 450);      // 00:00-07:30 at home
                       plan.StayInBuilding(office, 800, 240);  // 08:00-12:00 at the office (the Tall Tower)
                       plan.StayInBuilding(lunch, 1215, 60);   // lunch at the cafe
                       plan.StayInBuilding(office, 1330, 210); // 13:30-17:00 at the office (the Tall Tower)
                       plan.StayInBuilding(dinner, 1800, 120); // dinner
                       plan.StayInBuilding(home, 2100, 179);   // 21:00-23:59 at home
                   });
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();
            _instance = this;
            OnDeath += AuditQuest.Job.OnTargetKilled;
            Refresh();
        }

        public static void Refresh()
        {
            if (_instance == null) return;
            var save = BusinessmenQuestSave.Instance?.Data;
            bool wanted = save != null && save.Accepted && !save.TargetKilled;
            if (wanted) _instance.Deploy(save); else QuestNpcUtil.Park(_instance, NpcId);
        }

        private void Deploy(JobSaveData save)
        {
            QuestNpcUtil.ApplyName(this, save.TargetName);
            if (_lookAppliedForRun != save.Runs)
            {
                _lookAppliedForRun = save.Runs;
                ApplyBusinessLook();
            }

            QuestNpcUtil.Revive(this);
            QuestNpcUtil.EnterBuilding(NpcId, HomeName); // start hidden at home...
            Schedule.Enable();                           // ...then his routine takes him out and about
            Schedule.EnforceState();
        }

        // ---- Look ----------------------------------------------------------------------------------------------

        private static readonly Color32[] HairColors =
        {
            new Color32(30, 22, 18, 255), new Color32(60, 40, 28, 255), new Color32(95, 65, 40, 255),
            new Color32(120, 120, 120, 255), new Color32(190, 190, 190, 255),
        };
        private static readonly string[] HairStyles =
            { HairStyle.CloseBuzzCut, HairStyle.Peaked, HairStyle.Receding, HairStyle.Balding, HairStyle.MidFringe, HairStyle.SidePartBob };
        private static readonly Color32[] ShirtColors =
        {
            new Color32(235, 235, 235, 255), new Color32(180, 200, 225, 255), new Color32(210, 210, 200, 255),
        };
        private static readonly Color32[] SuitColors =
        {
            new Color32(25, 28, 40, 255), new Color32(45, 45, 50, 255), new Color32(20, 20, 20, 255), new Color32(55, 45, 38, 255),
        };

        // A suit: blazer, button-up, dark trousers, dress shoes, a tie, sometimes glasses and a watch.
        private void ApplyBusinessLook()
        {
            AppearanceUtil.ClearLayers(Appearance);
            Color hair = QuestNpcUtil.Pick(HairColors);

            Appearance.Set<Gender>(0f);
            Appearance.Set<Height>(UnityEngine.Random.Range(0.95f, 1.05f));
            Appearance.Set<Weight>(UnityEngine.Random.Range(0.35f, 0.65f));
            Appearance.Set<SkinColor>((Color)QuestNpcUtil.Pick(QuestNpcUtil.SkinTones));
            Appearance.Set<EyeBallTint>(Color.white);
            Appearance.Set<HairColor>(hair);
            Appearance.Set<HairStyle>(QuestNpcUtil.Pick(HairStyles));

            Appearance.WithFaceLayer<Face>(UnityEngine.Random.value < 0.6f ? Face.Neutral : Face.SlightFrown, new Color(0.15f, 0.1f, 0.1f));
            if (UnityEngine.Random.value < 0.25f)
                Appearance.WithFaceLayer<FacialHair>(FacialHair.Stubble, hair);

            Color suit = QuestNpcUtil.Pick(SuitColors);
            Appearance.WithBodyLayer<Shirts>(UnityEngine.Random.value < 0.8f ? Shirts.Buttonup : Shirts.RolledButtonUp, (Color)QuestNpcUtil.Pick(ShirtColors));
            Appearance.WithBodyLayer<Pants>(Pants.Jeans, suit); // there are no dress trousers, so dark jeans in the suit's colour
            AppearanceUtil.Wear<Chest>(Appearance, suit, Chest.Blazer);
            AppearanceUtil.Wear<Feet>(Appearance, new Color(0.08f, 0.06f, 0.05f), Feet.DressShoes);

            // One tie from the game's four (the paths are guesses from the item ids; missing ones are logged and skipped).
            string[] ties =
            {
                "Avatar/Accessories/Neck/StripedTie/StripedTie", "Avatar/Accessories/Neck/CheckedTie/CheckedTie",
                "Avatar/Accessories/Neck/DottedTie/DottedTie", "Avatar/Accessories/Neck/CrossTie/CrossTie",
            };
            AppearanceUtil.Wear<Neck>(Appearance, new Color(0.35f, 0.08f, 0.1f), QuestNpcUtil.Pick(ties));

            float extras = UnityEngine.Random.value;
            if (extras < 0.35f)
                AppearanceUtil.Wear<Head>(Appearance, Color.white, Head.RectangleFrameGlasses);
            else if (extras < 0.5f)
                AppearanceUtil.Wear<Head>(Appearance, Color.white, Head.SmallRoundGlasses);
            if (UnityEngine.Random.value < 0.5f)
                AppearanceUtil.Wear<Hands>(Appearance, Color.white, Hands.Polex);

            AppearanceUtil.ApplyNow(Appearance);
        }
    }

    public class AuditQuest : Quest
    {
        internal static readonly JobController Job = new JobController(new JobDefinition
        {
            QuestIdPrefix = "businessmen_audit_quest",
            RewardLogLabel = "Johnny's audit job",
            GetSave = () => BusinessmenQuestSave.Instance?.Data,
            PickName = Auditor.PickName,
            RefreshTarget = Auditor.Refresh,
            CreateQuest = id => QuestManager.CreateQuest<AuditQuest>(id),
        });

        protected override string Title => "The Audit";
        protected override string Description => "An auditor is getting too close to the books. Johnny Smith wants him dealt with.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            string name = Job.TargetNameOr("the auditor");
            var kill = AddEntry($"Find and kill {name}. He keeps a strict daily routine");
            kill.SetPOIToNPC<Auditor>();
            AddEntry($"Tell Johnny Smith {name} is dealt with");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active
        }
    }
}
