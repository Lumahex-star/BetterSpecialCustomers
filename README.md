# Better Special Customers

A [MelonLoader](https://melonwiki.xyz/) mod for **Schedule I** that makes the Special Customer groups (hippies, businessmen, party bus, and the rest) more rewarding to deal with: they can buy much more from you, and each group remembers how well you've treated them.

**Current version: 1.0.0**

## Features (version 1)

### Higher buy limits

Vanilla Special Customer groups stop buying at 120 units, and the game's own formula (20 + 10 per rank) already reaches that at the top rank. This mod replaces the formula with a straight line from each group's normal starting amount at the lowest rank up to **200 units at Kingpin**, so the limit keeps growing with your rank all the way up.

### Relationships

Every Special Customer group now has its own **relationship** with you, kept separately for each group. It uses the same 0 to 5 scale and the same level names as the game's vanilla relationships (Hostile, Unfriendly, Neutral, Friendly, Loyal).

- **It changes how much they buy.** The group's limit is multiplied by a relationship bonus, on top of the rank-based amount: **x0.5** at relationship 0, **x1.0** at 2.5, and **x1.5** at 5. Every group starts at 2.5 (Neutral), so a fresh save plays exactly like before until you start building relationships.
- **You earn it by selling.** If you sell a group at least **75% of their buy limit** during a visit, the relationship goes up by **0.5**. The total counts across the whole visit, so you can spread it over the days they stay. It can only increase once per visit.
- **It takes effect on their next visit.** Whatever you earn this visit applies the next time the group comes to town, not the visit it was earned in.
- **Hurting a group costs you.** Attacking, aiming at or killing a group's customers, or failing a pickpocket on them, lowers the relationship (each action has a short cooldown, so one fight doesn't wipe it out).
- **It's saved with your game.** Relationships are stored in your save, per group, and survive saving and reloading in the middle of a visit. If you remove the mod, your save still loads normally.

At the top rank and top relationship, a group can buy up to **300 units** (200 x 1.5).

### Special Customer quests

Each group's leader can now ask you for a job. Open the leader's dialogue and ask. Each visit there is a **50% chance** they have something for you, decided once per visit. If they don't, they say so and you can try again next visit. Finishing a job pays a reward and adds **+0.5** to your relationship with the group, which takes effect on their next visit like any other gain. Jobs can be done once per visit, and an unfinished job is dropped when the group leaves.

| Group | Leader | Job | Reward |
|---|---|---|---|
| Bikers | Diesel Rodd | **Loose Lips.** A club informant is hiding in The Piss Hut. Knock on the door to bring him out and kill him. | $5,000, 100 XP |
| Businessmen | Johnny Smith | **The Audit.** An auditor follows a daily routine around town (home, the Tall Tower, a cafe, dinner). Find and kill him. | $5,000, 100 XP |
| Hippies | Krystal Bird | **Special Delivery.** Carry a package from one dead drop to another. | Grow supplies (3 Extra Long-Life Soil, 3 Speed Grow, PGR, Fertilizer), 100 XP |
| Party Bus | Chad Chaddington | **Spread the Word.** Invite 5 townspeople to the Party Bus. Each one can turn you down. | $2,500, 5 Energy Drinks, 100 XP |

## Requirements

- Schedule I
- MelonLoader
- [S1API](https://github.com/ifBars/S1API) (used by the quests). Use a recent version; older ones don't support everything the quests need.

## Installation

1. Install MelonLoader for Schedule I if you haven't already.
2. Put the mod's `.dll` into the game's `Mods` folder:
   - `BetterSpecialCustomers_Mono.dll` for the Mono version of the game
   - `BetterSpecialCustomers_IL2Cpp.dll` for the IL2CPP version
3. Start the game.

> The Mono build has been tested. The IL2CPP build compiles from the same code but hasn't been tested yet.
## Notes and limitations

- **Multiplayer:** relationships are tracked by the host. Other players' screens (for example the arrival popup) may show the normal, relationship-free number.
- **Quests are new and lightly tested.** In particular the IL2CPP build, multiplayer clients, and the exact clothing items on the quest NPCs (a missing item is logged and skipped).
- **Debug mode:** set `DebugMode` to `true` in `Quests/QuestFramework.cs` to guarantee a job and repeat it as often as you like within a visit.
- **Tuning:** the numbers above are constants in `Relationships/GroupRelationships.cs` (starting value, bonus range, gain per visit, and the 75% requirement) and `Patches/MaxBuyLimitPatch.cs` (the 200 cap).

## Building

The project has two configurations, `MONO` and `IL2CPP`. Set `S1Dir` in `BetterSpecialCustomers.csproj` to your game folder (the references point at the game's own DLLs), pick a configuration and build. A post-build step copies the mod into the game's `Mods` folder.

## Credits

Made by Lumahex.
