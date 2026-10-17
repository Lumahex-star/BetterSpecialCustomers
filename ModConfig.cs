using MelonLoader;
using MelonLoader.Preferences;

namespace BetterSpecialCustomers
{
    // The mod's settings, kept in MelonLoader's own preferences file (UserData/MelonPreferences.cfg), which mod managers
    // (r2modman, Gale, Thunderstore Mod Manager) show as a settings page. Each category becomes a section there.
    // Values are read whenever they are used, so a change takes effect the next time it matters; restarting the game
    // is the safe way to pick up edits made while it is running.
    internal static class ModConfig
    {
        private static readonly MelonPreferences_Category Relationships =
            MelonPreferences.CreateCategory("BetterSpecialCustomers_Relationships", "Better Special Customers: Buy Limits & Relationships");

        private static readonly MelonPreferences_Category Quests =
            MelonPreferences.CreateCategory("BetterSpecialCustomers_Quests", "Better Special Customers: Quests");

        // ---- Buy limits & relationships ----

        public static readonly MelonPreferences_Entry<int> MaxBuyQuantity = Relationships.CreateEntry(
            "MaxBuyQuantity", 200, "Max buy quantity at Kingpin",
            "How many units a group buys at the top rank (before the relationship multiplier). Vanilla stops at 120.",
            validator: new ValueRange<int>(20, 1000));

        public static readonly MelonPreferences_Entry<float> StartingRelationship = Relationships.CreateEntry(
            "StartingRelationship", 2.5f, "Starting relationship",
            "The relationship (0 to 5) a group starts at. At 2.5 (Neutral) groups buy exactly their normal amount.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> MultiplierAtWorst = Relationships.CreateEntry(
            "BuyMultiplierAtWorst", 0.5f, "Buy multiplier at relationship 0",
            "How much a group's buy limit is multiplied by when the relationship is 0 (Hostile).",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> MultiplierAtBest = Relationships.CreateEntry(
            "BuyMultiplierAtBest", 1.5f, "Buy multiplier at relationship 5",
            "How much a group's buy limit is multiplied by when the relationship is 5 (Loyal).",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> GainPerVisit = Relationships.CreateEntry(
            "RelationshipGainPerVisit", 0.5f, "Relationship gained per visit",
            "Relationship gained when you sell a group enough during a visit (at most once per visit).",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> SalesNeededForGain = Relationships.CreateEntry(
            "SalesNeededForGain", 0.75f, "Share of the buy limit needed for a gain",
            "How much of a group's buy limit (0 to 1) you must sell them over a visit to earn the relationship gain.",
            validator: new ValueRange<float>(0f, 1f));

        public static readonly MelonPreferences_Entry<float> PunchPenalty = Relationships.CreateEntry(
            "PunchPenalty", 0.25f, "Penalty for punching a customer",
            "Relationship lost for punching one of the group's customers.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> AimPenalty = Relationships.CreateEntry(
            "AimPenalty", 0.25f, "Penalty for aiming at a customer",
            "Relationship lost for aiming a weapon at one of the group's customers.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> PickpocketPenalty = Relationships.CreateEntry(
            "PickpocketPenalty", 0.25f, "Penalty for a failed pickpocket",
            "Relationship lost for getting caught pickpocketing one of the group's customers.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> KillPenalty = Relationships.CreateEntry(
            "KillPenalty", 1f, "Penalty for killing a customer",
            "Relationship lost for killing one of the group's customers.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<float> PenaltyCooldownSeconds = Relationships.CreateEntry(
            "PenaltyCooldownSeconds", 20f, "Penalty cooldown (seconds)",
            "After a penalty, the same kind of penalty can't happen again for this many seconds, so one fight doesn't wipe the relationship out.",
            validator: new ValueRange<float>(0f, 600f));

        // ---- Quests ----

        public static readonly MelonPreferences_Entry<float> JobChance = Quests.CreateEntry(
            "JobChance", 0.5f, "Chance a leader has a job",
            "Chance (0 to 1) that a group leader has a job for you on a given visit.",
            validator: new ValueRange<float>(0f, 1f));

        public static readonly MelonPreferences_Entry<float> JobRelationshipReward = Quests.CreateEntry(
            "JobRelationshipReward", 0.5f, "Relationship reward for a job",
            "Relationship gained with the group for finishing their job. It takes effect on their next visit.",
            validator: new ValueRange<float>(0f, 5f));

        public static readonly MelonPreferences_Entry<int> JobXpReward = Quests.CreateEntry(
            "JobXpReward", 100, "XP reward for a job",
            "XP for finishing any group leader's job.",
            validator: new ValueRange<int>(0, 100000));

        public static readonly MelonPreferences_Entry<int> BikersCashReward = Quests.CreateEntry(
            "BikersCashReward", 5000, "Bikers' job cash reward",
            "Cash for Diesel Rodd's job, Loose Lips.",
            validator: new ValueRange<int>(0, 1000000));

        public static readonly MelonPreferences_Entry<int> BusinessmenCashReward = Quests.CreateEntry(
            "BusinessmenCashReward", 5000, "Businessmen's job cash reward",
            "Cash for Johnny Smith's job, The Audit.",
            validator: new ValueRange<int>(0, 1000000));

        public static readonly MelonPreferences_Entry<int> PartyBusCashReward = Quests.CreateEntry(
            "PartyBusCashReward", 2500, "Party Bus job cash reward",
            "Cash for Chad Chaddington's job, Spread the Word (on top of the energy drinks).",
            validator: new ValueRange<int>(0, 1000000));

        public static readonly MelonPreferences_Entry<bool> DebugMode = Quests.CreateEntry(
            "DebugMode", false, "Quest debug mode",
            "For testing: leaders always have a job, and you can repeat a job as often as you like within one visit.");

        // Creates the settings (and the file entries, so mod managers can show them before the game first quits).
        public static void Init()
        {
            Relationships.SaveToFile(false);
            Quests.SaveToFile(false);
        }
    }
}
