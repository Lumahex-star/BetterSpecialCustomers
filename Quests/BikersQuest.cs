using System;
using MelonLoader;
using S1API.Entities;               // NPC
using S1API.Entities.Appearances.AccessoryFields;     // Chest, Feet, Neck, Head, Hands, Waist
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
    // Diesel Rodd's job: a club informant hiding in The Piss Hut. The shared rules (roll, start, reward, reset) are in
    // JobController; this file holds only what is specific to the bikers.

    // Saved progress. S1API saves the one SaveableField as JSON.
    public class BikersQuestSave : Saveable
    {
        public static BikersQuestSave Instance { get; private set; }

        public BikersQuestSave() { Instance = this; }

        [SaveableField("bikers_quest")]
        public JobSaveData Data = new JobSaveData();

        protected override void OnLoaded()
        {
            Instance = this;
            Informant.Refresh(); // the informant may have been created before the save finished loading
        }
    }

    // The target. S1API creates this NPC when the world loads, and there is no way to spawn or remove it later,
    // so instead it is "parked" far away and made invincible until the quest needs him, then moved to his hiding place.
    public sealed class Informant : NPC
    {
        private const string NpcId = "bikers_informant";
        private const string HideoutName = "The Piss Hut";

        private static Informant _instance;
        private static int _lookAppliedForRun = -1; // in-memory only: which run his random look was made for

        private static readonly string[] FirstNames =
            { "Dale", "Rusty", "Vince", "Eddie", "Skeet", "Lenny", "Wade", "Cody", "Marv", "Gus", "Terry", "Bo" };
        private static readonly string[] LastNames =
            { "Hollis", "Brandt", "Keane", "Mercer", "Voss", "Pruitt", "Lund", "Garrity", "Boone", "Sykes", "Dunn", "Rourke" };

        public static string PickName() => QuestNpcUtil.RandomName(FirstNames, LastNames);

        public override bool IsPhysical => true;

        public Informant() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity(NpcId, "Informant", string.Empty)
                   .WithSpawnPosition(QuestNpcUtil.ParkedPosition)
                   // Once he has come out (summoned by a knock) he walks back in and stays in.
                   .WithSchedule(plan => plan.StayInBuilding(Building.Get<S1API.Map.Buildings.ThePissHut>(), 0, 1439));
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Appearance.Build();
            _instance = this;
            Melon<Core>.Logger.Msg("[quest] the informant NPC was created");

            // Vanilla's Quest_DefeatCartel only counts a kill (IsDead), so we do the same.
            OnDeath += BikersQuest.Job.OnObjectiveDone;
            Refresh();
        }

        // Puts him inside The Piss Hut while the job is open and he is still alive, otherwise parks him.
        public static void Refresh()
        {
            if (_instance == null) return;
            var save = BikersQuestSave.Instance?.Data;
            bool wanted = save != null && save.Accepted && !save.TargetKilled;
            try
            {
                if (wanted) _instance.Deploy(save); else QuestNpcUtil.Park(_instance, NpcId);
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error($"[quest] informant {(wanted ? "deploy" : "park")} failed: {ex}");
            }
        }

        private void Deploy(JobSaveData save)
        {
            QuestNpcUtil.ApplyName(this, save.TargetName);
            if (_lookAppliedForRun != save.Runs)
            {
                _lookAppliedForRun = save.Runs;
                ApplyBikerLook(); // a different face each run, but always a believable biker
            }

            QuestNpcUtil.Revive(this);
            QuestNpcUtil.EnterBuilding(NpcId, HideoutName);
            Schedule.Enable(); // after a summon he returns to the hut by himself
        }

        // ---- Look ----------------------------------------------------------------------------------------------
        // A controlled look instead of S1API's fully random one (random skin and clothing colours looked wrong):
        // a natural skin tone and hair colour, and the game's biker clothes.

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

        private void ApplyBikerLook()
        {
            AppearanceUtil.ClearLayers(Appearance);
            Color hair = QuestNpcUtil.Pick(HairColors);

            Appearance.Set<Gender>(0f);
            Appearance.Set<Height>(UnityEngine.Random.Range(0.95f, 1.05f));
            Appearance.Set<Weight>(UnityEngine.Random.Range(0.45f, 0.75f));
            Appearance.Set<SkinColor>((Color)QuestNpcUtil.Pick(QuestNpcUtil.SkinTones));
            Appearance.Set<EyeBallTint>(Color.white);
            Appearance.Set<HairColor>(hair);
            Appearance.Set<HairStyle>(QuestNpcUtil.Pick(HairStyles));

            Color featureColor = new Color(0.15f, 0.1f, 0.1f);
            Appearance.WithFaceLayer<Face>(UnityEngine.Random.value < 0.5f ? Face.Neutral : Face.SlightFrown, featureColor);
            if (UnityEngine.Random.value < 0.6f)
                Appearance.WithFaceLayer<FacialHair>(UnityEngine.Random.value < 0.5f ? FacialHair.Stubble : FacialHair.Goatee, hair);

            // Top and bottom layers (the paths for these are in S1API's lists).
            string[] tops = { Shirts.TShirt, Shirts.TShirt, Shirts.VNeck, Shirts.FlannelButtonUp, Shirts.RolledButtonUp };
            Appearance.WithBodyLayer<Shirts>(QuestNpcUtil.Pick(tops), (Color)QuestNpcUtil.Pick(ShirtColors));
            Appearance.WithBodyLayer<Pants>(UnityEngine.Random.value < 0.7f ? Pants.Jeans : Pants.CargoPants, (Color)QuestNpcUtil.Pick(JeansColors));

            // Outerwear: mostly the biker jacket, sometimes a leather jacket or an open vest.
            Color leather = UnityEngine.Random.value < 0.7f ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.2f, 0.12f, 0.07f);
            float jacket = UnityEngine.Random.value;
            if (jacket < 0.6f)
                AppearanceUtil.Wear<Chest>(Appearance, leather, "Avatar/Accessories/Chest/BikerJacket/BikerJacket", "Avatar/Accessories/Chest/BikerJacket/Biker Jacket");
            else if (jacket < 0.85f)
                AppearanceUtil.Wear<Chest>(Appearance, leather, "Avatar/Accessories/Chest/LeatherJacket/LeatherJacket", "Avatar/Accessories/Chest/LeatherJacket/leatherJacket");
            else
                AppearanceUtil.Wear<Chest>(Appearance, leather, Chest.OpenVest);

            // Boots, a belt, and one of: bandana, beanie, shades or nothing on the head.
            AppearanceUtil.Wear<Feet>(Appearance, new Color(0.1f, 0.1f, 0.1f), UnityEngine.Random.value < 0.85f ? Feet.CombatBoots : Feet.Sneakers);
            if (UnityEngine.Random.value < 0.5f)
                AppearanceUtil.Wear<Waist>(Appearance, new Color(0.1f, 0.07f, 0.05f), "Avatar/Accessories/Waist/Belt/Belt");

            float head = UnityEngine.Random.value;
            if (head < 0.3f)
                AppearanceUtil.Wear<Head>(Appearance, new Color(0.1f, 0.1f, 0.1f), "Avatar/Accessories/Head/Bandana/Bandana", "Avatar/Accessories/Head/PaisleyBandana/PaisleyBandana");
            else if (head < 0.5f)
                AppearanceUtil.Wear<Head>(Appearance, new Color(0.12f, 0.12f, 0.12f), Head.Beanie);
            else if (head < 0.7f)
                AppearanceUtil.Wear<Head>(Appearance, Color.white, Head.Oakleys);

            // Hands and neck: fingerless gloves sometimes, a chain sometimes.
            if (UnityEngine.Random.value < 0.4f)
                AppearanceUtil.Wear<Hands>(Appearance, new Color(0.1f, 0.1f, 0.1f), "Avatar/Accessories/Hands/FingerlessGloves/FingerlessGloves");
            if (UnityEngine.Random.value < 0.4f)
                AppearanceUtil.Wear<Neck>(Appearance, Color.white, Neck.GoldChain, "Avatar/Accessories/Neck/SilverChain/SilverChain");

            AppearanceUtil.ApplyNow(Appearance);
        }
    }

    public class BikersQuest : Quest
    {
        internal static readonly JobController Job = new JobController(new JobDefinition
        {
            QuestIdPrefix = "bikers_informant_quest",
            RewardLogLabel = "Diesel's informant job",
            GetSave = () => BikersQuestSave.Instance?.Data,
            PickName = Informant.PickName,
            RefreshTarget = Informant.Refresh,
            CreateQuest = id => QuestManager.CreateQuest<BikersQuest>(id),
        });

        protected override string Title => "Loose Lips";
        protected override string Description => "Someone in the club has been talking to the cops. Diesel Rodd wants the informant dealt with.";
        // S1API finishes setting a quest up (OnCreated, entries) on the NEXT frame after CreateQuest, so we can't call
        // Begin() ourselves straight away. We only create this quest when Diesel gives the job, so let it begin itself.
        protected override bool AutoBegin => true;

        protected override void OnCreated()
        {
            base.OnCreated();
            Job.Attach(this);
            string name = Job.TargetNameOr("the informant");
            var kill = AddEntry($"Knock on the door of The Piss Hut and kill {name}");
            kill.SetPOIToNPC<Informant>(); // the marker follows him; while hidden he sits at the hut's door
            AddEntry($"Tell Diesel {name} is dealt with");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            Job.Attach(this); // S1API only reloads quests that were still active, so this is the open job
        }
    }
}
