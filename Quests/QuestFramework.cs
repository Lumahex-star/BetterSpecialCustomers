using System;
using System.Collections.Generic;
using System.Reflection;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using S1API.Entities;               // NPC, NPCAppearance
using S1API.Leveling;               // LevelManager
using S1API.Money;                  // Money
using S1API.Quests;                 // Quest
using UnityEngine;
#if MONO
using GameNPC = ScheduleOne.NPCs.NPC;
using GameBuilding = ScheduleOne.Map.NPCEnterableBuilding;
using GameItemInstance = ScheduleOne.ItemFramework.ItemInstance;
using GameRegistry = ScheduleOne.Registry;
using GameInventory = ScheduleOne.PlayerScripts.PlayerInventory;
#elif IL2CPP
using GameNPC = Il2CppScheduleOne.NPCs.NPC;
using GameBuilding = Il2CppScheduleOne.Map.NPCEnterableBuilding;
using GameItemInstance = Il2CppScheduleOne.ItemFramework.ItemInstance;
using GameRegistry = Il2CppScheduleOne.Registry;
using GameInventory = Il2CppScheduleOne.PlayerScripts.PlayerInventory;
#endif

namespace BetterSpecialCustomers.Quests
{
    // Everything the "kill a target for a group leader" jobs need to remember. Each group stores one of these inside
    // its own Saveable (see BikersQuestSave / BusinessmenQuestSave); S1API saves it as JSON.
    public class JobSaveData
    {
        public bool Accepted;

        // The group the job belongs to, so the reward goes to the right group even after a reload.
        public string GroupId = string.Empty;

        // The target is dead; the leader still has to be told.
        public bool TargetKilled;

        // Reported back, finished and rewarded.
        public bool Completed;

        // This visit's roll: 0 = not asked yet, 1 = the leader has a job, 2 = nothing this time.
        public int OfferState;

        // This run's target, so he keeps the same name after a reload.
        public string TargetName = string.Empty;

        // How many times the job has been started. Gives each run its own quest id.
        public int Runs;
    }

    // What differs between one group's job and another's.
    internal sealed class JobDefinition
    {
        public string QuestIdPrefix;
        public string RewardLogLabel;                 // e.g. "Diesel's informant job"
        public float RelationshipReward = 0.5f;       // takes effect from the group's next visit
        public float CashReward = 5000f;
        public int XpReward = 100;
        public Action GrantExtraReward;               // optional: e.g. hand the player items

        public Func<JobSaveData> GetSave;             // null until the save has loaded
        public Func<string> PickName;                 // a fresh random target name
        public Action RefreshTarget;                  // put the target NPC where the job's state says he belongs
        public Func<string, Quest> CreateQuest;       // creates the quest object with the given id
    }

    // What the leader says. Used by JobDialogue.
    internal sealed class JobLines
    {
        public Func<string, string> OfferLine;        // given the target's name
        public string[] NoJobLines;
        public string AlreadyDoneLine;
        public string HandInLine;
    }

    // The shared rules of a job: roll for it once per visit, start it, complete it, reward it, reset it.
    internal sealed class JobController
    {
        // DEBUG: when true the leader always has a job and you can take it again straight after finishing it,
        // as often as you like within one visit. Set to false for normal play.
        public static readonly bool DebugMode = false;

        // Chance (0 to 1) that a leader has a job on a given visit.
        public static float OfferChance => DebugMode ? 1f : 0.5f;

        private static readonly List<JobController> All = new List<JobController>();

        private readonly JobDefinition _def;
        private Quest _quest;

        public JobController(JobDefinition def)
        {
            _def = def;
            All.Add(this);
        }

        public JobSaveData Save => _def.GetSave();

        // The open quest, once S1API has created it (null before then, and between jobs).
        public Quest Quest => _quest;

        // Called by the quest class when S1API creates or reloads it.
        public void Attach(Quest quest)
        {
            _quest = quest;
            quest.OnComplete -= HandleComplete; // quest.OnComplete is an event; unsubscribe first so we never subscribe twice
            quest.OnComplete += HandleComplete;
        }

        public string TargetNameOr(string fallback)
        {
            string name = Save?.TargetName;
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        // ---- Dialogue-facing rules ----

        // The ask option shows unless a job is currently open (accepted but not handed in).
        public bool CanAsk()
        {
            var save = Save;
            return save == null || !save.Accepted || save.Completed;
        }

        public bool IsReadyToHandIn()
        {
            var save = Save;
            return save != null && save.TargetKilled && !save.Completed;
        }

        // Handles the player asking for a job and returns what the leader should say.
        public string Ask(string groupId, JobLines lines)
        {
            var save = Save;
            if (save == null) return null;
            if (!string.IsNullOrEmpty(groupId)) save.GroupId = groupId; // lets the visit reset find this group

            if (save.Completed && !DebugMode) return lines.AlreadyDoneLine;
            if (save.Completed) ResetRun(); // debug mode only: start a fresh run

            // Roll once per visit and remember it, so asking again can't re-roll (debug mode re-rolls every time).
            if (save.OfferState == 0 || DebugMode)
                save.OfferState = UnityEngine.Random.value < OfferChance ? 1 : 2;

            if (save.OfferState == 1)
            {
                Start(groupId); // picks the target's name
                return lines.OfferLine(save.TargetName);
            }
            return lines.NoJobLines[UnityEngine.Random.Range(0, lines.NoJobLines.Length)];
        }

        // ---- Job lifecycle ----

        public void Start(string groupId)
        {
            var save = Save;
            if (save != null)
            {
                save.Accepted = true;
                save.GroupId = groupId ?? string.Empty;
                save.Runs++;
                save.TargetName = _def.PickName();
            }
            if (_quest == null)
            {
                // Each run gets its own id, so a new run never collides with an earlier finished one.
                // S1API sets the quest up (OnCreated, entries) and begins it on the NEXT frame.
                _quest = _def.CreateQuest($"{_def.QuestIdPrefix}_{save?.Runs ?? 0}");
            }
            if (_quest == null) { Melon<Core>.Logger.Error("[quest] could not create the quest"); return; }
            _def.RefreshTarget();
            Melon<Core>.Logger.Msg("[quest] created; S1API sets up its entries and begins it on the next frame");
        }

        // Called when the job's task is done (the target died, the package arrived): every entry but the last is
        // completed and the last one, "report back to the leader", begins.
        public void OnObjectiveDone()
        {
            var save = Save;
            if (_quest == null || save == null || !save.Accepted || save.TargetKilled) return;
            save.TargetKilled = true;
            int last = _quest.QuestEntries.Count - 1;
            for (int i = 0; i < last; i++) _quest.QuestEntries[i].Complete();
            if (last > 0) _quest.QuestEntries[last].Begin();
            Melon<Core>.Logger.Msg("[quest] task done, report back to the leader");
        }

        // Called from the leader's hand-in dialogue option.
        public void HandIn()
        {
            var save = Save;
            if (_quest == null || save == null || !save.TargetKilled || save.Completed) return;
            save.Completed = true;
            int last = _quest.QuestEntries.Count - 1;
            if (last > 0) _quest.QuestEntries[last].Complete();
            _quest.Complete();
            _def.RefreshTarget();
        }

        private void HandleComplete()
        {
            if (_def.CashReward > 0f) Money.ChangeCashBalance(_def.CashReward, true, true);
            LevelManager.AddXP(_def.XpReward);
            _def.GrantExtraReward?.Invoke();
            Melon<Core>.Logger.Msg($"[quest] paid ${_def.CashReward}, {_def.XpReward} XP");

            string group = Save?.GroupId;
            if (!string.IsNullOrEmpty(group))
                GroupRelationships.Reward(group, _def.RelationshipReward, "completed " + _def.RewardLogLabel);
            else
                Melon<Core>.Logger.Warning("[quest] no saved group id, relationship reward skipped");
        }

        // Clears the saved progress (and drops an open job) so the job can be taken again.
        public void ResetRun()
        {
            var save = Save;
            if (save == null) return;
            if (_quest != null && save.Accepted && !save.Completed) _quest.Cancel();
            _quest = null;
            save.OfferState = 0;
            save.TargetName = string.Empty;
            save.Accepted = false;
            save.TargetKilled = false;
            save.Completed = false;
            _def.RefreshTarget();
        }

        // Called when any customer group leaves: that group's job is once per visit, so it can be taken again next time.
        public static void OnGroupLeft(string groupId)
        {
            foreach (var job in All)
            {
                var save = job.Save;
                if (save == null || !string.Equals(save.GroupId, groupId, StringComparison.OrdinalIgnoreCase)) continue;
                job.ResetRun();
                Melon<Core>.Logger.Msg($"[quest] {groupId} left, their job can be taken again next visit");
            }
        }
    }

    // Helpers for making items and handing them to the player. Uses the game's own item registry and inventory.
    internal static class RewardUtil
    {
        // Makes a stack of an item by its id (see the F10 item list); null, with a log line, if the id does not exist.
        public static GameItemInstance CreateItem(string itemId, int quantity)
        {
            var definition = GameRegistry.GetItem(itemId);
            if (definition == null)
            {
                Melon<Core>.Logger.Warning($"[quest] item '{itemId}' does not exist");
                return null;
            }
            return definition.GetDefaultInstance(quantity);
        }

        // Puts each (item id, amount) pair in the player's inventory.
        public static void GiveItems(params KeyValuePair<string, int>[] items)
        {
#if MONO
            var inventory = ScheduleOne.DevUtilities.PlayerSingleton<GameInventory>.Instance;
#else
            var inventory = Il2CppScheduleOne.DevUtilities.PlayerSingleton<GameInventory>.Instance;
#endif
            if (inventory == null) { Melon<Core>.Logger.Warning("[quest] no inventory to give the reward to"); return; }
            foreach (var pair in items)
            {
                var item = CreateItem(pair.Key, pair.Value);
                if (item != null) inventory.AddItemToInventory(item);
            }
        }
    }

    // Helpers shared by the target NPCs (the informant and the auditor).
    internal static class QuestNpcUtil
    {
        public static readonly Vector3 ParkedPosition = new Vector3(0f, -200f, 0f);

        public static readonly Color32[] SkinTones =
        {
            new Color32(236, 200, 170, 255), new Color32(224, 172, 138, 255), new Color32(198, 140, 100, 255),
            new Color32(168, 112, 78, 255), new Color32(130, 84, 58, 255),
        };

        public static T Pick<T>(T[] items) => items[UnityEngine.Random.Range(0, items.Length)];

        // A fresh random name for each run, so it is never the same person twice.
        public static string RandomName(string[] firstNames, string[] lastNames) => Pick(firstNames) + " " + Pick(lastNames);

        public static void ApplyName(NPC npc, string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return;
            int split = fullName.IndexOf(' ');
            npc.FirstName = split > 0 ? fullName.Substring(0, split) : fullName;
            npc.LastName = split > 0 ? fullName.Substring(split + 1) : string.Empty;
        }

        // ---- Hiding in a building ----
        // Entering a building hides an NPC (its avatar is switched off) and parks it at the door; knocking on that
        // door summons it back out for a few seconds. A building needs no interior for this to work.

        private static GameNPC FindGameNpc(string npcId)
        {
            var all = UnityEngine.Object.FindObjectsOfType<GameNPC>(true);
            foreach (var n in all)
                if (n != null && n.ID == npcId) return n;
            Melon<Core>.Logger.Warning($"[quest] NPC '{npcId}' is not among the {all.Length} NPCs in the scene");
            return null;
        }

        private static GameBuilding FindBuilding(string name)
        {
            foreach (var b in UnityEngine.Object.FindObjectsOfType<GameBuilding>(true))
                if (b != null && string.Equals(b.BuildingName, name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        public static void EnterBuilding(string npcId, string buildingName)
        {
            var npc = FindGameNpc(npcId);
            var building = FindBuilding(buildingName);
            if (npc == null || building == null || building.Doors.Length == 0)
            {
                Melon<Core>.Logger.Warning($"[quest] could not hide '{npcId}' in '{buildingName}' (npc found: {npc != null}, building found: {building != null}, doors: {building?.Doors.Length})");
                return;
            }
            if (npc.CurrentBuilding == building) return;
            if (npc.isInBuilding) npc.ExitBuilding();
            npc.EnterBuilding(null, building.GUID.ToString(), 0);
            Melon<Core>.Logger.Msg($"[quest] '{npcId}' is now hidden in '{buildingName}'");
        }

        public static void LeaveBuilding(string npcId)
        {
            var npc = FindGameNpc(npcId);
            if (npc != null && npc.isInBuilding) npc.ExitBuilding();
        }

        // Takes the NPC out of the world: out of any building, schedule off, invincible, far away.
        public static void Park(NPC npc, string npcId)
        {
            npc.Schedule.Disable(); // a parked NPC has no navmesh to walk on
            LeaveBuilding(npcId);
            npc.IsInvincible = true;
            if ((npc.Position - ParkedPosition).sqrMagnitude > 1f) npc.Position = ParkedPosition;
        }

        // Brings a (possibly dead) NPC back to full health and makes him killable.
        public static void Revive(NPC npc)
        {
            if (npc.IsDead) npc.Revive();
            npc.Heal((int)npc.MaxHealth);
            npc.IsInvincible = false;
        }
    }

    // Helpers for changing an S1API NPC's look after it was created. S1API's Set/With... calls only change its stored
    // settings; they reach the model when S1API applies them, which it does just once (right after creation). So we
    // clear and apply them ourselves, through reflection because the methods are internal.
    internal static class AppearanceUtil
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Empties the face, body and accessory layer lists in S1API's stored settings (earlier runs' clothes would pile up).
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

        // Wears the first of the given accessory paths that the game can actually load. Some paths are educated guesses
        // (the game's newer items aren't in S1API's lists), so a missing one is logged and skipped.
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
}
