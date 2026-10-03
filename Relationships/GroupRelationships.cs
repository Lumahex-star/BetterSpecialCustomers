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
/// Holds one relationship value per Special Customer GROUP (keyed by the game's GroupId, e.g. "hippies").
///
/// This copies the shape of the game's own NPCRelationData: a float from 0 to 5 that is changed with
/// Change/Set and clamped. We can't reuse NPCRelationData itself because it needs an NPC instance
/// (it networks itself through NPC.SendRelationship), and the game reuses ONE leader NPC for every
/// group - so an NPC-owned value would be shared by all groups.
///
/// The vanilla RelationshipCategory (Hostile ... Loyal) is reused as-is for naming the levels.
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

    // Relationship gained for selling a group its ENTIRE current order. Smaller sales give a
    // proportional share. (For comparison, a vanilla customer deal changes relationship by -0.5 to +0.5.)
    public const float GainForFullOrder = 0.5f;

    private static readonly Dictionary<string, float> Values = new Dictionary<string, float>();

    private static string Key(string groupId) => (groupId ?? string.Empty).ToLowerInvariant();

    public static float Get(string groupId)
    {
        return Values.TryGetValue(Key(groupId), out float value) ? value : DefaultValue;
    }

    public static void Set(string groupId, float newValue)
    {
        Values[Key(groupId)] = Math.Max(Min, Math.Min(Max, newValue));
    }

    public static void Change(string groupId, float delta)
    {
        float before = Get(groupId);
        Set(groupId, before + delta);
        float after = Get(groupId);
        Melon<Core>.Logger.Msg(
            $"[{groupId}] relationship {before:0.00} -> {after:0.00} ({GetCategory(groupId)}, buy multiplier x{GetMultiplier(groupId):0.00})");
    }

    public static ERelationshipCategory GetCategory(string groupId)
    {
        return RelationshipCategory.GetCategory(Get(groupId));
    }

    public static float GetMultiplier(string groupId)
    {
        float normalized = (Get(groupId) - Min) / (Max - Min);
        return MultiplierAtMin + (MultiplierAtMax - MultiplierAtMin) * normalized;
    }

    // ---- Persistence -------------------------------------------------------------------------------
    // We store the values INSIDE the game's own SpecialCustomers.json as an extra property:
    //
    //   "BetterSpecialCustomers_Relationships": { "hippies": 3.2, "partybus": 2.5 }
    //
    // The game reads that file with JsonUtility, which ignores properties it doesn't know, so the save
    // stays valid if the mod is removed. Doing it this way (instead of our own file) matters because the
    // game deletes unknown files from the save folder every time it saves. See PersistencePatches.cs.
    // The tiny hand-written reader/writer avoids JsonUtility, which can't serialize mod classes on IL2CPP.

    public const string JsonKey = "BetterSpecialCustomers_Relationships";

    private static readonly Regex SectionRegex =
        new Regex("\"" + JsonKey + "\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Compiled);

    private static readonly Regex PairRegex =
        new Regex("\"([^\"]+)\"\\s*:\\s*(-?[0-9.eE+-]+)", RegexOptions.Compiled);

    public static void Clear()
    {
        Values.Clear();
    }

    /// <summary>The JSON fragment to insert into the game's save string, e.g. "BetterSpecialCustomers_Relationships": {...}</summary>
    public static string ToJsonProperty()
    {
        List<string> pairs = new List<string>();
        foreach (KeyValuePair<string, float> pair in Values)
        {
            pairs.Add($"\"{pair.Key}\": {pair.Value.ToString("R", CultureInfo.InvariantCulture)}");
        }
        return $"\"{JsonKey}\": {{ {string.Join(", ", pairs)} }}";
    }

    public static void LoadFromJson(string json)
    {
        Values.Clear();
        if (string.IsNullOrEmpty(json))
            return;

        Match section = SectionRegex.Match(json);
        if (!section.Success)
            return;

        foreach (Match pair in PairRegex.Matches(section.Groups[1].Value))
        {
            if (float.TryParse(pair.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                Set(pair.Groups[1].Value, value);
        }
    }
}
