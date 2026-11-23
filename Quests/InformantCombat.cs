using System;
using MelonLoader;
using S1API.Entities.Equippables; // Weapon (asset paths)
using S1API.Leveling;             // LevelManager, FullRank, Rank
using UnityEngine;
#if MONO
using FishNet;
using ScheduleOne.NPCs.Responses;
using ScheduleOne.PlayerScripts;
#elif IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.NPCs.Responses;
using Il2CppScheduleOne.PlayerScripts;
#endif

namespace BetterSpecialCustomers.Quests
{
    // Makes the bikers' informant fight back whenever he is provoked: aimed at, hit, shot, pickpocketed, or hearing
    // gunshots or explosions. He fights with a knife until the player reaches Hustler III, then with an M1911.
    //
    // Two parts, because the game handles threats and attacks separately:
    //   - Threats (aimed at, gunshots, explosions): the civilian response component has a built-in override, set to Fight.
    //   - Attacks (hit, shot, failed pickpocket): we listen for them and send him at the attacker ourselves.
    internal static class InformantCombat
    {
        private static readonly FullRank GunRank = new FullRank(Rank.Hustler, 3);
        private static int _subscribedTo; // instance id of the responses component we listen to (0 = none)

        // The weapon for the player's current rank.
        private static string WeaponForRank() =>
            LevelManager.Exists && LevelManager.CurrentRank >= GunRank ? Weapon.M1911 : Weapon.Knife;

        // Called each time the informant is deployed.
        public static void Setup(Informant informant, string npcId)
        {
            try
            {
                informant.Aggressiveness = 1f;
                Arm(informant);

                var npc = QuestNpcUtil.FindGameNpc(npcId);
                var responses = npc?.Responses;
                if (responses == null) { Melon<Core>.Logger.Warning("[quest] the informant has no responses component, he won't fight back"); return; }

#if MONO
                var civilian = responses as NPCResponses_Civilian;
#elif IL2CPP
                var civilian = responses.TryCast<NPCResponses_Civilian>();
#endif
                if (civilian != null)
                {
                    civilian.OverrideThreatResponses = true;
                    civilian.ThreatResponseOverride = NPCResponses_Civilian.EAttackResponse.Fight;
                }

                int id = responses.GetInstanceID();
                if (_subscribedTo == id) return; // already listening
                _subscribedTo = id;
#if MONO
                responses.OnNonLethallyAttackedByPlayer += OnProvoked;
                responses.OnRepeatedlyNonLethallyAttackedByPlayer += OnProvoked;
                responses.OnLethallyAttackedByPlayer += OnProvoked;
                responses.OnAimedAtByPlayer += OnProvoked;
                responses.OnPickpocketFailed += OnProvoked;
#elif IL2CPP
                // See RelationshipLossPatch.AddHandler for why IL2CPP needs this instead of "+=".
                responses.OnNonLethallyAttackedByPlayer = Patches.RelationshipLossPatch.AddHandler(responses.OnNonLethallyAttackedByPlayer, OnProvoked);
                responses.OnRepeatedlyNonLethallyAttackedByPlayer = Patches.RelationshipLossPatch.AddHandler(responses.OnRepeatedlyNonLethallyAttackedByPlayer, OnProvoked);
                responses.OnLethallyAttackedByPlayer = Patches.RelationshipLossPatch.AddHandler(responses.OnLethallyAttackedByPlayer, OnProvoked);
                responses.OnAimedAtByPlayer = Patches.RelationshipLossPatch.AddHandler(responses.OnAimedAtByPlayer, OnProvoked);
                responses.OnPickpocketFailed = Patches.RelationshipLossPatch.AddHandler(responses.OnPickpocketFailed, OnProvoked);
#endif
                Melon<Core>.Logger.Msg("[quest] the informant will fight back when provoked");
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error($"[quest] setting up the informant's combat failed: {ex}");
            }
        }

        // Gives him the weapon for the player's current rank (checked again each time he's provoked, in case they ranked up).
        private static void Arm(Informant informant)
        {
            string weapon = WeaponForRank();
            if (informant.CombatBehaviour.DefaultWeaponAssetPath == weapon) return;
            informant.CombatBehaviour.DefaultWeaponAssetPath = weapon;
            if (ModConfig.DebugMode.Value) Melon<Core>.Logger.Msg($"[quest] the informant is armed with {weapon}");
        }

        // Any provocation: arm him, then go after whoever did it.
        private static void OnProvoked(Player attacker)
        {
            try
            {
                if (!InstanceFinder.IsServer || attacker == null) return; // combat is run by the host
                var informant = Informant.ActiveInstance;
                if (informant == null || informant.IsDead) return;

                Arm(informant);
                var npc = QuestNpcUtil.FindGameNpc(Informant.Id);
                var combat = npc?.Behaviour?.CombatBehaviour;
                if (combat == null) return;
                if (combat.Active) return; // already fighting
                combat.SetTargetAndEnable_Server(attacker.NetworkObject);
                if (ModConfig.DebugMode.Value) Melon<Core>.Logger.Msg("[quest] the informant was provoked and is attacking");
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error($"[quest] the informant couldn't fight back: {ex}");
            }
        }

        // True while he's fighting (so he isn't put back in his hideout mid-fight).
        public static bool IsFighting(string npcId)
        {
            var combat = QuestNpcUtil.FindGameNpc(npcId)?.Behaviour?.CombatBehaviour;
            return combat != null && combat.Active;
        }
    }
}
