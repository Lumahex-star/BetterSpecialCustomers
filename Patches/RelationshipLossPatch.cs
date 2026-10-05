using HarmonyLib;
using BetterSpecialCustomers.Diagnostics;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using UnityEngine;

#if MONO
using ScheduleOne.Dialogue;
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.Responses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppFishNet;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Responses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches
{
    [HarmonyPatch(typeof(SpecialCustomer), nameof(SpecialCustomer.SetGroupMates))]
    internal class RelationshipLossPatch
    {
        private static readonly HashSet<int> Subscribed = new HashSet<int>();

        // One cooldown timer per kind of event, so e.g. a punch doesn't silence an aimed weapon.
        // They start well in the past so the very first penalty isn't blocked by the cooldown.
        private static float _lastPunchTime = -100f;
        private static float _lastKillTime = -100f;
        private static float _lastAimTime = -100f;
        private static float _lastPickpocketTime = -100f;

        public static void Postfix(SpecialCustomer __instance)
        {
            PatchDiagnostics.Hit("RelationshipLossPatch (subscribing to NPC reactions)");
            if (!Subscribed.Add(__instance.GetInstanceID())) return;

            NPCResponses r = __instance.Responses;
#if MONO
            r.OnNonLethallyAttackedByPlayer += OnPunched;
            r.OnRepeatedlyNonLethallyAttackedByPlayer += OnPunched;
            r.OnLethallyAttackedByPlayer += OnKilledSomeone;
            r.OnAimedAtByPlayer += OnAimedAt;
            r.OnPickpocketFailed += OnPickpocketFailed;
#elif IL2CPP
            r.OnNonLethallyAttackedByPlayer = AddHandler(r.OnNonLethallyAttackedByPlayer, OnPunched);
            r.OnRepeatedlyNonLethallyAttackedByPlayer = AddHandler(r.OnRepeatedlyNonLethallyAttackedByPlayer, OnPunched);
            r.OnLethallyAttackedByPlayer = AddHandler(r.OnLethallyAttackedByPlayer, OnKilledSomeone);
            r.OnAimedAtByPlayer = AddHandler(r.OnAimedAtByPlayer, OnAimedAt);
            r.OnPickpocketFailed = AddHandler(r.OnPickpocketFailed, OnPickpocketFailed);
#endif
        }

#if IL2CPP
        // On IL2CPP the game's events are Il2CppSystem.Action<Player>, a different type from the normal
        // C# Action<Player>, so a plain "+=" with our method doesn't compile. Instead:
        //   1. ConvertDelegate wraps our normal C# method in the game's delegate type.
        //   2. Combine adds it to whatever handlers the event already has (this is the same thing the game
        //      itself does in SpecialCustomer.SetGroupMates), and Cast gives the result back its exact type.
        private static Il2CppSystem.Action<Player> AddHandler(Il2CppSystem.Action<Player> existing, Action<Player> handler)
        {
            var converted = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Player>>(handler);
            return Il2CppSystem.Delegate.Combine(existing, converted).Cast<Il2CppSystem.Action<Player>>();
        }
#endif

        private static void OnPunched(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPunchTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPunchTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "punched");
        }

        private static void OnKilledSomeone(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastKillTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastKillTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 1f, "customer killed");
        }
        private static void OnAimedAt(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastAimTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastAimTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "aimed weapon");
        }
        private static void OnPickpocketFailed(Player thief)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPickpocketTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPickpocketTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "pickpocket failed");
        }
    }
}