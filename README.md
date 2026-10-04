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
- **It's saved with your game.** Relationships are stored in your save, per group, and survive saving and reloading in the middle of a visit. If you remove the mod, your save still loads normally.

At the top rank and top relationship, a group can buy up to **300 units** (200 x 1.5).

## Planned for future versions

- **Special Customer quests.** Groups will sometimes ask you to do something for them. Completing these quests will raise your relationship with the group even further, on top of what you earn from selling.

## Requirements

- Schedule I
- MelonLoader

## Installation

1. Install MelonLoader for Schedule I if you haven't already.
2. Put the mod's `.dll` into the game's `Mods` folder:
   - `BetterSpecialCustomers_Mono.dll` for the Mono version of the game
   - `BetterSpecialCustomers_IL2Cpp.dll` for the IL2CPP version
3. Start the game.

> The Mono build has been tested. The IL2CPP build compiles from the same code but hasn't been tested yet.

## Notes and limitations

- **Multiplayer:** relationships are tracked by the host. Other players' screens (for example the arrival popup) may show the normal, relationship-free number.
- **Tuning:** the numbers above are constants in `Relationships/GroupRelationships.cs` (starting value, bonus range, gain per visit, and the 75% requirement) and `Patches/MaxBuyLimitPatch.cs` (the 200 cap).

## Building

The project has two configurations, `MONO` and `IL2CPP`. Set `S1Dir` in `BetterSpecialCustomers.csproj` to your game folder (the references point at the game's own DLLs), pick a configuration and build. A post-build step copies the mod into the game's `Mods` folder.

## Credits

Made by Lumahex.
