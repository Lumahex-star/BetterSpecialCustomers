using HarmonyLib;
using BetterSpecialCustomers.Relationships;
using MelonLoader;
using UnityEngine;

#if MONO
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.Responses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppFishNet;
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

        // Start well in the past so the very first penalty isn't blocked by the cooldown.
        private static float _lastPenaltyTime = -100f;

        public static void Postfix(SpecialCustomer __instance)
        {
            if (!Subscribed.Add(__instance.GetInstanceID())) return;

            NPCResponses r = __instance.Responses;
            r.OnNonLethallyAttackedByPlayer += OnPunched;
            r.OnRepeatedlyNonLethallyAttackedByPlayer += OnPunched;
            r.OnLethallyAttackedByPlayer += OnKilledSomeone;
            r.OnAimedAtByPlayer += OnAimedAt;
            r.OnPickpocketFailed += OnPickpocketFailed;
        }

        private static void OnPunched(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPenaltyTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPenaltyTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "punched");
        }

        private static void OnKilledSomeone(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPenaltyTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPenaltyTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 1.f, "customer killed");
        }
        private static void OnAimedAt(Player attacker)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPenaltyTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPenaltyTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "aimed weapom");
        }
        private static void OnPickpocketFailed(Player thief)
        {
            if (!InstanceFinder.IsServer) return;
            if (Time.time - _lastPenaltyTime < 20f) return;

            var group = NetworkSingleton<SpecialCustomerManager>.Instance.CurrentGroupData;
            if (group == null) return;

            _lastPenaltyTime = Time.time;
            GroupRelationships.Penalize(group.GroupId, 0.25f, "pickpocket failed");
        }
    }
}