using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using BetterSpecialCustomers.Diagnostics;
using BetterSpecialCustomers.Relationships;
#if MONO
using ScheduleOne.Levelling;
using ScheduleOne.SpecialCustomers;
#elif IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.SpecialCustomers;
#endif

namespace BetterSpecialCustomers.Patches;

/// <summary>
/// IL2CPP-only fallback for the buy limit.
///
/// Problem: in the IL2CPP build the game calls SpecialCustomerData.GetBuyQuantityForPlayerRank without going
/// through our Harmony patch (the diagnostics showed the patch applies but is never called - the compiler
/// has most likely folded this tiny method into its callers, so there is no separate method left to hook).
///
/// Fix: the game's own formula is  min(baseBuyQuantity + additionalBuyQuantityPerRank * rank, maxBuyQuantity),
/// reading three values stored on each group's data object. Instead of changing the method, we change the
/// numbers it reads, so the game's own (inlined) formula produces the result we want:
///   top-rank limit      T = 200 x relationship multiplier   (same as the Mono patches produce)
///   additionalPerRank   = enough to reach T at Kingpin      (rounded up, then capped at T)
///   maxBuyQuantity      = T
/// The base quantity is scaled by the relationship multiplier too, so the multiplier also works at the lowest
/// ranks, where the limit is just the base quantity. The game uses the base for XP as well
/// (xp = units sold / base x 40), which stays roughly balanced: selling x1.1 more units against a x1.1 larger base
/// gives about the same XP for a full sale.
///
/// When it runs: at the start of every group arrival (including when a save is loaded mid-visit), right after
/// the relationship has been snapshotted. Because the relationship only changes at arrival, the numbers stay
/// correct for the whole visit - the daily refill and the arrival popup both read the same values.
///
/// Because the base is scaled from the game's ORIGINAL base (remembered the first time we see each group), the
/// numbers don't compound from visit to visit.
/// Since the game update that stores each visit's limit on the group leader (starting / remaining buy quantity) and
/// in the save (RunningBudget), the result also gets written there once, at arrival and at each morning's refill.
/// PushToLeader overwrites those stored values with our own number, worked out here in C#, so the limit is right
/// even if the inlined formula doesn't pick up the fields above.
///
/// On Mono this class does nothing; the Harmony patches on the method itself work there.
/// </summary>
internal static class BuyLimitFieldSync
{
#if IL2CPP
    // In the IL2CPP wrappers a private C# field shows up as a PROPERTY with the same name (the probe confirmed
    // this for _currentData). We look for either a field or a property so this also works if that changes.
    private sealed class IntMember
    {
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;

        public IntMember(Type type, string name)
        {
            _field = AccessTools.Field(type, name);
            _property = _field == null ? AccessTools.Property(type, name) : null;
        }

        public bool Exists => _field != null || _property != null;

        public int Get(object target) => Convert.ToInt32(_field != null ? _field.GetValue(target) : _property.GetValue(target));

        public void Set(object target, int value)
        {
            if (_field != null) _field.SetValue(target, value);
            else _property.SetValue(target, value);
        }
    }

    private static readonly IntMember BaseQuantity = new IntMember(typeof(SpecialCustomerData), "_baseBuyQuantity");
    private static readonly IntMember PerRank = new IntMember(typeof(SpecialCustomerData), "_additionalBuyQuantityPerRank");
    private static readonly IntMember MaxQuantity = new IntMember(typeof(SpecialCustomerData), "_maxBuyQuantity");
    private static bool _reportedMissing;

    // The game's own base quantity per group, remembered the first time we see it (before we change it).
    private static readonly System.Collections.Generic.Dictionary<string, int> OriginalBase =
        new System.Collections.Generic.Dictionary<string, int>();
#endif

    public static void Apply(SpecialCustomerManager manager, string groupId)
    {
#if IL2CPP
        try
        {
            if (!PerRank.Exists || !MaxQuantity.Exists)
            {
                if (!_reportedMissing)
                {
                    _reportedMissing = true;
                    Melon<Core>.Logger.Error(
                        $"BuyLimitFieldSync: can't find the game's per-rank / max buy quantity values (perRank found: {PerRank.Exists}, max found: {MaxQuantity.Exists}). See the [diag] Probe lines. The buy limit will stay vanilla.");
                }
                return;
            }

            SpecialCustomerData data = manager.GetSpecialCustomerData(groupId);
            if (data == null)
                return;

            int topRank = (int)ERank.Kingpin;
            double multiplier = GroupRelationships.GetMultiplier(groupId);

            // The game's own base, remembered before we first change it.
            string key = groupId.ToLowerInvariant();
            if (!OriginalBase.TryGetValue(key, out int originalBase))
            {
                originalBase = data.BaseBuyQuantity;
                OriginalBase[key] = originalBase;
            }

            int baseQuantity = BaseQuantity.Exists ? (int)Math.Round(originalBase * multiplier) : originalBase;
            int top = (int)Math.Round(MaxBuyPatch.NewMaxBuyQuantity * multiplier);
            int perRank = Math.Max(0, (int)Math.Ceiling((top - baseQuantity) / (double)topRank));

            int oldPerRank = PerRank.Get(data);
            int oldMax = MaxQuantity.Get(data);
            if (BaseQuantity.Exists) BaseQuantity.Set(data, baseQuantity);
            PerRank.Set(data, perRank);
            MaxQuantity.Set(data, top);

            if (PatchDiagnostics.Enabled)
                Melon<Core>.Logger.Msg(
                    $"[diag] BuyLimitFieldSync [{groupId}]: base {originalBase} -> {baseQuantity}, perRank {oldPerRank} -> {perRank}, max {oldMax} -> {top} " +
                    $"(relationship x{GroupRelationships.GetMultiplier(groupId):0.00}; Kingpin limit = {Math.Min(baseQuantity + perRank * topRank, top)})");
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Error("BuyLimitFieldSync failed: " + ex);
        }
#endif
    }

#if IL2CPP
    // What MaxBuyPatch and RelationshipBuyQuantityPatch produce on Mono, from the game's ORIGINAL base quantity.
    private static int TargetLimit(SpecialCustomerData data, string groupId)
    {
        if (!OriginalBase.TryGetValue(groupId.ToLowerInvariant(), out int originalBase))
            originalBase = data.BaseBuyQuantity; // not changed yet, so still the game's own
        int topRank = (int)ERank.Kingpin;
        int rank = Math.Max(0, Math.Min((int)NetworkSingleton<LevelManager>.Instance.Rank, topRank));
        int rankBased = originalBase + (MaxBuyPatch.NewMaxBuyQuantity - originalBase) * rank / topRank;
        return Math.Max(1, (int)Math.Round(rankBased * GroupRelationships.GetMultiplier(groupId)));
    }
#endif

    /// <summary>
    /// Writes our limit over the one the game just stored for the group in town. Called after the game sets it:
    /// on arrival (starting and remaining quantity) and after the daily phase update, which refills a group in town
    /// (remaining quantity and the saved RunningBudget). Host only, like the game's own setters. Does nothing on Mono.
    /// </summary>
    public static void PushToLeader(SpecialCustomerManager manager, bool arrival)
    {
#if IL2CPP
        try
        {
            if (!InstanceFinder.IsServer)
                return;
            SpecialCustomerSaveData save = manager._currentData;
            SpecialCustomerData group = manager._currentCustomerGroup;
            var leader = manager._leaderInstance;
            if (save == null || group == null || leader == null)
                return;
            if (!arrival && (int)save.Phase != 2)
                return; // the group isn't in town, so there was no refill

            int limit = TargetLimit(group, group.GroupId);
            if (arrival)
            {
                leader.SetStartingBuyQuantity(limit);
                leader.SetRemainingBuyQuantity(limit); // a save loaded mid-visit puts its own remaining amount back right after
            }
            else
            {
                save.RunningBudget = limit;
                leader.SetRemainingBuyQuantity(limit);
            }

            if (PatchDiagnostics.Enabled)
                Melon<Core>.Logger.Msg($"[diag] BuyLimitFieldSync.PushToLeader [{group.GroupId}]: limit {limit} ({(arrival ? "arrival" : "morning refill")})");
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Error("BuyLimitFieldSync.PushToLeader failed: " + ex);
        }
#endif
    }
}
