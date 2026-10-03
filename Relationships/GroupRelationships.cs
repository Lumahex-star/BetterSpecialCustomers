using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MelonLoader;
#if MONO
using ScheduleOne.NPCs.Relation;
#elif IL2CPP
using Il2CppScheduleOne.NPCs.Relation;
#endif

namespace BetterSpecialCustomers.Relationships;

/// <summary>
/// Holds the relationship state of every Special Customer GROUP, keyed by the game's GroupId (e.g. "hippies").
///
/// This copies the shape of the game's own NPCRelationData: a float from 0 to 5 that is changed and clamped,
/// with the vanilla RelationshipCategory (Hostile ... Loyal) used for naming the levels. We can't reuse
/// NPCRelationData itself because it needs an NPC instance (it networks itself through NPC.SendRelationship),
/// and the game reuses ONE leader NPC for every group - so an NPC-owned value would be shared by all groups.
///
/// Each group has two relationship numbers:
///   Current - what the player has earned. Rises by GainPerVisit, at most once per visit.
///   Applied - what the buy quantity multiplier actually uses. It is a snapshot of Current taken when the
///             group ARRIVES, so a gain only takes effect the next time the group comes to town.
/// </summary>
public static class GroupRelationships
{
    // Same scale as NPCRelationData.MinRelationship / MaxRelationship.
    public const float Min = 0f;
    public const float Max = 5f;

    // New groups start in the middle of the scale ("Neutral"), where the multiplier is exactly 1.0,
    // so a fresh save behaves like it did before this feature existed.
    public const float DefaultValue = 2.5f;

    // Buy quantity multiplier at relationship 0 and 5. It is a straight line between the two, the same
    // Mathf.Lerp(min, max, delta / 5) pattern the game uses for Supplier order limits.
    public const float MultiplierAtMin = 0.5f;
    public const float MultiplierAtMax = 1.5f;

    // Relationship gained per visit, no matter how many sales the player makes during it.
    // (For comparison, a vanilla customer deal changes relationship by -0.5 to +0.5.)
    public const float GainPerVisit = 0.5f;

    // The gain is only given once the player has sold at least this fraction of the group's buy limit
    // (the same "x / limit" progress the handover screen shows).
    public const float MinFractionOfLimitForGain = 0.75f;

    private class State
    {
        public float Current = DefaultValue;
        public float Applied = DefaultValue;
        public bool InVisit;          // the group is in town right now
        public bool GainedThisVisit;  // the one gain for this visit has already been given
    }

    private static readonly Dictionary<string, State> States = new Dictionary<string, State>();

    private static string Key(string groupId) => (groupId ?? string.Empty).ToLowerInvariant();

    private static State GetState(string groupId)
    {
        string key = Key(groupId);
        if (!States.TryGetValue(key, out State state))
        {
            state = new State();
            States[key] = state;
        }
        return state;
    }

    private static float Clamp(float value) => Math.Max(Min, Math.Min(Max, value));

    /// <summary>The relationship the player has earned (not necessarily applied yet).</summary>
    public static float GetCurrent(string groupId) => GetState(groupId).Current;

    /// <summary>The relationship that is currently affecting the buy quantity.</summary>
    public static float GetApplied(string groupId) => GetState(groupId).Applied;

    public static ERelationshipCategory GetCategory(float value) => RelationshipCategory.GetCategory(value);

    public static float GetMultiplier(string groupId)
    {
        float normalized = (GetApplied(groupId) - Min) / (Max - Min);
        return MultiplierAtMin + (MultiplierAtMax - MultiplierAtMin) * normalized;
    }

    // ---- Visit lifecycle (called from RelationshipVisitPatches / RelationshipGainPatch) ------------

    /// <summary>
    /// A group is arriving. If this really is a new visit, snapshot Current into Applied and re-arm the
    /// once-per-visit gain. If we are already inside a visit (the save was loaded mid-visit and the game
    /// re-ran its arrival code), do nothing.
    /// </summary>
    public static void BeginVisit(string groupId)
    {
        State state = GetState(groupId);
        if (state.InVisit)
            return;

        state.InVisit = true;
        state.GainedThisVisit = false;
        state.Applied = state.Current;
        Melon<Core>.Logger.Msg(
            $"[{groupId}] new visit. Relationship {state.Applied:0.00} ({GetCategory(state.Applied)}) now applies: buy multiplier x{GetMultiplier(groupId):0.00}");
    }

    /// <summary>The group left town.</summary>
    public static void EndVisit(string groupId)
    {
        GetState(groupId).InVisit = false;
    }

    /// <summary>
    /// The player completed a sale to the group. fractionOfLimitSold is how much of the group's current buy
    /// limit has been sold so far (0 to 1), counting this sale. The gain is given once per visit, as soon as
    /// that reaches MinFractionOfLimitForGain.
    /// </summary>
    public static void RegisterSale(string groupId, float fractionOfLimitSold)
    {
        State state = GetState(groupId);
        if (state.GainedThisVisit)
            return;

        if (fractionOfLimitSold < MinFractionOfLimitForGain)
        {
            Melon<Core>.Logger.Msg(
                $"[{groupId}] sold {fractionOfLimitSold:P0} of their limit; need {MinFractionOfLimitForGain:P0} for a relationship gain this visit");
            return;
        }

        state.GainedThisVisit = true;
        float before = state.Current;
        state.Current = Clamp(before + GainPerVisit);
        Melon<Core>.Logger.Msg(
            $"[{groupId}] relationship {before:0.00} -> {state.Current:0.00} ({GetCategory(state.Current)}). It applies from their next visit; this visit stays at x{GetMultiplier(groupId):0.00}");
    }

    // ---- Persistence -------------------------------------------------------------------------------
    // We store the state INSIDE the game's own SpecialCustomers.json as an extra property:
    //
    //   "BetterSpecialCustomers_Relationships": [
    //       { "id": "hippies", "current": 3, "applied": 2.5, "inVisit": 1, "gained": 1 }, ...
    //   ]
    //
    // The game reads that file with JsonUtility, which ignores properties it doesn't know, so the save
    // stays valid if the mod is removed. Doing it this way (instead of our own file) matters because the
    // game deletes unknown files from the save folder every time it saves. See RelationshipPersistencePatches.cs.
    // The tiny hand-written reader/writer avoids JsonUtility, which can't serialize mod classes on IL2CPP.
    // InVisit and Applied are saved too, otherwise loading a save in the middle of a visit would
    // start a "new" visit and apply the relationship early.

    public const string JsonKey = "BetterSpecialCustomers_Relationships";

    private static readonly Regex SectionRegex =
        new Regex("\"" + JsonKey + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex EntryRegex = new Regex("\\{([^}]*)\\}", RegexOptions.Compiled);

    private static string ReadField(string entry, string name)
    {
        Match m = Regex.Match(entry, "\"" + name + "\"\\s*:\\s*\"?([^\",\\s}]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static float ReadFloat(string entry, string name, float fallback)
    {
        string raw = ReadField(entry, name);
        return raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? Clamp(v)
            : fallback;
    }

    public static void Clear()
    {
        States.Clear();
    }

    /// <summary>The JSON fragment to insert into the game's save string.</summary>
    public static string ToJsonProperty()
    {
        List<string> entries = new List<string>();
        foreach (KeyValuePair<string, State> pair in States)
        {
            State s = pair.Value;
            entries.Add(string.Format(CultureInfo.InvariantCulture,
                "{{ \"id\": \"{0}\", \"current\": {1}, \"applied\": {2}, \"inVisit\": {3}, \"gained\": {4} }}",
                pair.Key, s.Current.ToString("R", CultureInfo.InvariantCulture), s.Applied.ToString("R", CultureInfo.InvariantCulture),
                s.InVisit ? 1 : 0, s.GainedThisVisit ? 1 : 0));
        }
        return $"\"{JsonKey}\": [ {string.Join(", ", entries)} ]";
    }

    public static void LoadFromJson(string json)
    {
        States.Clear();
        if (string.IsNullOrEmpty(json))
            return;

        Match section = SectionRegex.Match(json);
        if (!section.Success)
            return;

        foreach (Match entryMatch in EntryRegex.Matches(section.Groups[1].Value))
        {
            string entry = entryMatch.Groups[1].Value;
            string id = ReadField(entry, "id");
            if (string.IsNullOrEmpty(id))
                continue;

            State state = GetState(id);
            state.Current = ReadFloat(entry, "current", DefaultValue);
            state.Applied = ReadFloat(entry, "applied", state.Current);
            state.InVisit = ReadField(entry, "inVisit") == "1";
            state.GainedThisVisit = ReadField(entry, "gained") == "1";
        }
    }
}
