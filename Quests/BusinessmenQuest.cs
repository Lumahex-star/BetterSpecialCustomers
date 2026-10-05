using System;
using System.Reflection;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.Entities;               // NPC, NPCAppearance
using S1API.Entities.Appearances.AccessoryFields;     // Chest, Feet, Neck, Head, Hands
using S1API.Entities.Appearances.BodyLayerFields;     // Shirts, Pants
using S1API.Entities.Appearances.CustomizationFields; // SkinColor, HairStyle, ...
using S1API.Entities.Appearances.FaceLayerFields;     // Face, FacialHair
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
    // Saved progress for Johnny Smith's quest. Same shape as BikersQuestSave, with its own save keys.
    public class BusinessmenQuestSave : Saveable
    {
        public static BusinessmenQuestSave Instance { get; private set; }

        public BusinessmenQuestSave() { Instance = this; }

        [SaveableField("businessmen_quest_accepted")]
        public bool Accepted;

        [SaveableField("businessmen_quest_group")]
        public string GroupId = string.Empty;

        // The auditor is dead; Johnny still has to be told.
        [SaveableField("businessmen_quest_target_killed")]
        public bool TargetKilled;

        [SaveableField("businessmen_quest_completed")]
        public bool Completed;

        // This visit's roll: 0 = not asked yet, 1 = he has a job, 2 = nothing this time.
        [SaveableField("businessmen_quest_offer")]
        public int OfferState;

        [SaveableField("businessmen_quest_target_name")]
        public string TargetName = string.Empty;

        [SaveableField("businessmen_quest_runs")]
        public int Runs;

        protected override void OnLoaded()
        {
            Instance = this;
            Auditor.Refresh();
        }
    }

    // Helpers for changing an S1API NPC's look after it was created (see BikersQuest's Informant for the why).
    internal static class AppearanceUtil
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Empties the face, body and accessory layer lists in S1API's stored settings.
        public static void ClearLayers(NPCAppearance appearance)
        {
            try
            {
                object settings = typeof(NPCAppearance).GetField("_customAvatarSettings", Flags)?.GetValue(appearance);
                if (settings == null) return;
                foreach (string name in new[] { "FaceLayerSettings", "BodyLayerSettings", "AccessorySettings" })
                {
                    object list = settings.GetType().GetField(name, Flags)?.GetValue(settings)
                                  ?? settings.GetType().GetProperty(name, Flags)?.GetValue(settings);
                    list?.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(list, null);
                }
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Warning("[quest] clearing old layers failed: " + ex.Message);
            }
        }

        // Pushes the stored settings onto the model.
        public static void ApplyNow(NPCAppearance appearance)
        {
            try
            {
                var type = typeof(NPCAppearance);
                object avatar = type.GetField("_runtimeAvatar", Flags)?.GetValue(appearance);
                var apply = type.GetMethod("ApplyToAvatar", Flags);
                if (avatar == null || apply == null)
                {
                    Melon<Core>.Logger.Warning($"[quest] could not apply the look (avatar found: {avatar != null}, method found: {apply != null})");
                    return;
                }
                apply.Invoke(appearance, new[] { avatar });
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Warning("[quest] applying the look failed: " + ex.Message);
            }
        }

        // Wears the first of the given accessory paths that the game can actually load; logs the ones it can't.
        public static void Wear<T>(NPCAppearance appearance, Color color, params string[] candidatePaths)
            where T : S1API.Entities.Appearances.Base.BaseAccessoryAppearance
        {
            foreach (string path in candidatePaths)
            {
                bool exists = false;
                try { exists = Resources.Load(path) != null; } catch { }
                if (exists)
                {
                    appearance.WithAccessoryLayer<T>(path, color);
                    return;
                }
                Melon<Core>.Logger.Warning($"[quest] outfit path not found: {path}");
            }
        }
    }

    // The auditor. Like the informant he is created with the world and then "parked" until needed. Unlike the informant
    // he has a daily routine: while a job is open he starts hidden at home and then follows his schedule around town.
    public sealed class Auditor : NPC
    {
        private const string NpcId = "businessmen_auditor";
        private const string HomeName = "Upscale Apartments";
        public static readonly Vector3 ParkedPosition = new Vector3(0f, -200f, 0f);

        private static Auditor _instance;
        private static int _lookAppliedForRun = -1;

        private static readonly string[] FirstNames =
            { "Gordon", "Nigel", "Howard", "Clive", "Warren", "Preston", "Desmond", "Edwin", "Leonard", "Franklin" };
        private static readonly string[] LastNames =
            { "Whitlock", "Ashcroft", "Pembroke", "Thorne", "Hargrove", "Fairbanks", "Lockhart", "Sterling", "Winslow", "Crane" };

        public static string PickName() =>
            FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)] + " " + LastNames[UnityEngine.Random.Range(0, LastNames.Length)];

        public override bool IsPhysical => true;

        public Auditor() : base() { }

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            builder.WithIdentity(NpcId, "Auditor", string.Empty)
                   .WithSpawnPosition(ParkedPosition)
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
            OnDeath += AuditQuest.OnTargetKilled;
            Refresh();
        }

        public static void Refresh()
        {
            if (_instance == null) return;
            var save = BusinessmenQuestSave.Instance;
            bool wanted = save != null && save.Accepted && !save.TargetKilled;
            if (wanted) _instance.Deploy(); else _instance.Park();
        }

        private void Deploy()
        {
            var save = BusinessmenQuestSave.Instance;
            if (save != null && !string.IsNullOrEmpty(save.TargetName))
            {
                int split = save.TargetName.IndexOf(' ');
                FirstName = split > 0 ? save.TargetName.Substring(0, split) : save.TargetName;
                LastName = split > 0 ? save.TargetName.Substring(split + 1) : string.Empty;
            }
            if (save != null && _lookAppliedForRun != save.Runs)
            {
                _lookAppliedForRun = save.Runs;
                ApplyBusinessLook();
            }

            if (IsDead) Revive();
            Heal((int)MaxHealth);
            IsInvincible = false;
            EnterHome();          // start hidden at home...
            Schedule.Enable();    // ...then his routine takes him out and about
            Schedule.EnforceState();
        }

        private void Park()
        {
            Schedule.Disable();
            LeaveBuilding();
            IsInvincible = true;
            if ((Position - ParkedPosition).sqrMagnitude > 1f) Position = ParkedPosition;
        }

        // ---- Look ----------------------------------------------------------------------------------------------

        private static readonly Color32[] SkinTones =
        {
            new Color32(236, 200, 170, 255), new Color32(224, 172, 138, 255), new Color32(198, 140, 100, 255),
            new Color32(168, 112, 78, 255), new Color32(130, 84, 58, 255),
        };
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

        private static T Pick<T>(T[] items) => items[UnityEngine.Random.Range(0, items.Length)];

        // A suit: blazer, button-up, dark trousers, dress shoes, a tie, sometimes glasses and a watch.
        private void ApplyBusinessLook()
        {
            AppearanceUtil.ClearLayers(Appearance);
            Color hair = Pick(HairColors);

            Appearance.Set<Gender>(0f);
            Appearance.Set<Height>(UnityEngine.Random.Range(0.95f, 1.05f));
            Appearance.Set<Weight>(UnityEngine.Random.Range(0.35f, 0.65f));
            Appearance.Set<SkinColor>((Color)Pick(SkinTones));
            Appearance.Set<EyeBallTint>(Color.white);
            Appearance.Set<HairColor>(hair);
            Appearance.Set<HairStyle>(Pick(HairStyles));

            Appearance.WithFaceLayer<Face>(UnityEngine.Random.value < 0.6f ? Face.Neutral : Face.SlightFrown, new Color(0.15f, 0.1f, 0.1f));
            if (UnityEngine.Random.value < 0.25f)
                Appearance.WithFaceLayer<FacialHair>(FacialHair.Stubble, hair);

            Color suit = Pick(SuitColors);
            Appearance.WithBodyLayer<Shirts>(UnityEngine.Random.value < 0.8f ? Shirts.Buttonup : Shirts.RolledButtonUp, (Color)Pick(ShirtColors));
            Appearance.WithBodyLayer<Pants>(Pants.Jeans, suit); // there are no dress trousers, so dark jeans in the suit's colour
            AppearanceUtil.Wear<Chest>(Appearance, suit, Chest.Blazer);
            AppearanceUtil.Wear<Feet>(Appearance, new Color(0.08f, 0.06f, 0.05f), Feet.DressShoes);

            // One tie from the game's four (the paths are guesses from the item ids; missing ones are logged and skipped).
            string[] ties =
            {
                "Avatar/Accessories/Neck/StripedTie/StripedTie", "Avatar/Accessories/Neck/CheckedTie/CheckedTie",
                "Avatar/Accessories/Neck/DottedTie/DottedTie", "Avatar/Accessories/Neck/CrossTie/CrossTie",
            };
            AppearanceUtil.Wear<Neck>(Appearance, new Color(0.35f, 0.08f, 0.1f), Pick(ties));

            float extras = UnityEngine.Random.value;
            if (extras < 0.35f)
                AppearanceUtil.Wear<Head>(Appearance, Color.white, Head.RectangleFrameGlasses);
            else if (extras < 0.5f)
                AppearanceUtil.Wear<Head>(Appearance, Color.white, Head.SmallRoundGlasses);
            if (UnityEngine.Random.value < 0.5f)
                AppearanceUtil.Wear<Hands>(Appearance, Color.white, Hands.Polex);

            AppearanceUtil.ApplyNow(Appearance);
        }

        // ---- Hiding in a building ------------------------------------------------------------------------------

        private static GameNPC FindGameNpc()
        {
            foreach (var n in UnityEngine.Object.FindObjectsOfType<GameNPC>(true))
                if (n != null && n.ID == NpcId) return n;
            return null;
        }

        private static GameBuilding FindBuilding(string name)
        {
            foreach (var b in UnityEngine.Object.FindObjectsOfType<GameBuilding>(true))
                if (b != null && string.Equals(b.BuildingName, name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        private static void EnterHome()
        {
            var npc = FindGameNpc();
            var home = FindBuilding(HomeName);
            if (npc == null || home == null || home.Doors.Length == 0)
            {
                Melon<Core>.Logger.Warning($"[quest] could not place the auditor at home (npc found: {npc != null}, building found: {home != null})");
                return;
            }
            if (npc.CurrentBuilding == home) return;
            if (npc.isInBuilding) npc.ExitBuilding();
            npc.EnterBuilding(null, home.GUID.ToString(), 0);
        }

        private static void LeaveBuilding()
        {
            var npc = FindGameNpc();
            if (npc != null && npc.isInBuilding) npc.ExitBuilding();
        }
    }

    public class AuditQuest : Quest
    {
        // Rewards for finishing the job. The relationship bonus takes effect from the group's next visit.
        private const float RelationshipReward = 0.5f;
        private const float CashReward = 5000f;
        private const int XpReward = 100;

        private static AuditQuest _instance;

        protected override string Title => "The Audit";
        protected override string Description => "An auditor is getting too close to the books. Johnny Smith wants him dealt with.";
        protected override bool AutoBegin => true; // see BikersQuest: S1API sets the quest up on the next frame

        protected override void OnCreated()
        {
            base.OnCreated();
            _instance = this;
            string name = BusinessmenQuestSave.Instance?.TargetName;
            if (string.IsNullOrEmpty(name)) name = "the auditor";
            var kill = AddEntry($"Find and kill {name}. He keeps a strict daily routine");
            kill.SetPOIToNPC<Auditor>();
            AddEntry($"Tell Johnny Smith {name} is dealt with");
            Subscribe();
            Melon<Core>.Logger.Msg("[quest] AuditQuest created");
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            _instance = this; // S1API only reloads quests that were still active
            Subscribe();
        }

        private void Subscribe()
        {
            OnComplete -= HandleComplete;
            OnComplete += HandleComplete;
        }

        public static void OnTargetKilled()
        {
            var save = BusinessmenQuestSave.Instance;
            if (_instance == null || save == null || !save.Accepted || save.TargetKilled) return;
            save.TargetKilled = true;
            if (_instance.QuestEntries.Count > 0) _instance.QuestEntries[0].Complete();
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Begin();
            Melon<Core>.Logger.Msg("[quest] auditor killed, return to Johnny");
        }

        public static void HandIn()
        {
            var save = BusinessmenQuestSave.Instance;
            if (_instance == null || save == null || !save.TargetKilled || save.Completed) return;
            save.Completed = true;
            if (_instance.QuestEntries.Count > 1) _instance.QuestEntries[1].Complete();
            _instance.Complete();
            Auditor.Refresh();
        }

        private void HandleComplete()
        {
            Money.ChangeCashBalance(CashReward, true, true);
            LevelManager.AddXP(XpReward);
            Melon<Core>.Logger.Msg($"[quest] paid ${CashReward} and {XpReward} XP");

            string group = BusinessmenQuestSave.Instance?.GroupId;
            if (!string.IsNullOrEmpty(group))
                GroupRelationships.Reward(group, RelationshipReward, "completed Johnny's audit job");
            else
                Melon<Core>.Logger.Warning("[quest] no saved group id, relationship reward skipped");
        }

        public static void Start(string groupId)
        {
            var save = BusinessmenQuestSave.Instance;
            if (save != null)
            {
                save.Accepted = true;
                save.GroupId = groupId ?? string.Empty;
                save.Runs++;
                save.TargetName = Auditor.PickName();
            }
            if (_instance == null)
            {
                int run = save?.Runs ?? 0;
                _instance = QuestManager.CreateQuest<AuditQuest>($"businessmen_audit_quest_{run}") as AuditQuest;
            }
            if (_instance == null) { Melon<Core>.Logger.Error("[quest] could not create AuditQuest"); return; }
            Auditor.Refresh();
            Melon<Core>.Logger.Msg("[quest] created; S1API sets up its entries and begins it on the next frame");
        }

        // Clears the saved progress (and drops an open job) so the quest can be taken again.
        public static void ResetRun()
        {
            var save = BusinessmenQuestSave.Instance;
            if (save == null) return;
            if (_instance != null && save.Accepted && !save.Completed) _instance.Cancel();
            _instance = null;
            save.OfferState = 0;
            save.TargetName = string.Empty;
            save.Accepted = false;
            save.TargetKilled = false;
            save.Completed = false;
            Auditor.Refresh();
        }

        // Called when a customer group leaves: the job is once per visit.
        public static void ResetForNewVisit(string groupId)
        {
            var save = BusinessmenQuestSave.Instance;
            if (save == null) return;
            if (!string.Equals(save.GroupId, groupId, StringComparison.OrdinalIgnoreCase)) return;
            ResetRun();
            Melon<Core>.Logger.Msg("[quest] group left, the audit job can be taken again next visit");
        }
    }
}
