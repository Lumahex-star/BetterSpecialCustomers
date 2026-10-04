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
/// The base quantity is left alone (the game also uses it for XP).
///
/// When it runs: at the start of every group arrival (including when a save is loaded mid-visit), right after
/// the relationship has been snapshotted. Because the relationship only changes at arrival, the numbers stay
/// correct for the whole visit - the daily refill and the arrival popup both read the same values.
///
/// Difference from the Mono build: at the LOWEST ranks the relationship multiplier does not shrink or grow the
/// limit (it only scales the top of the curve), because the base value is not touched.
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

    private static readonly IntMember PerRank = new IntMember(typeof(SpecialCustomerData), "_additionalBuyQuantityPerRank");
    private static readonly IntMember MaxQuantity = new IntMember(typeof(SpecialCustomerData), "_maxBuyQuantity");
    private static bool _reportedMissing;
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
            int baseQuantity = data.BaseBuyQuantity;
            int top = (int)Math.Round(MaxBuyPatch.NewMaxBuyQuantity * GroupRelationships.GetMultiplier(groupId));
            int perRank = Math.Max(0, (int)Math.Ceiling((top - baseQuantity) / (double)topRank));

            int oldPerRank = PerRank.Get(data);
            int oldMax = MaxQuantity.Get(data);
            PerRank.Set(data, perRank);
            MaxQuantity.Set(data, top);

            if (PatchDiagnostics.Enabled)
                Melon<Core>.Logger.Msg(
                    $"[diag] BuyLimitFieldSync [{groupId}]: base {baseQuantity}, perRank {oldPerRank} -> {perRank}, max {oldMax} -> {top} " +
                    $"(relationship x{GroupRelationships.GetMultiplier(groupId):0.00}; Kingpin limit = {Math.Min(baseQuantity + perRank * topRank, top)})");
        }
        catch (Exception ex)
        {
            Melon<Core>.Logger.Error("BuyLimitFieldSync failed: " + ex);
        }
#endif
    }
}
