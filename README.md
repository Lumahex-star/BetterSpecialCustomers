# Better Special Customers

A [MelonLoader](https://melonwiki.xyz/) mod for **Schedule I** that makes the Special Customer groups (hippies, businessmen, party bus, and the rest) more rewarding to deal with: they can buy much more from you, and each group remembers how well you've treated them.

**Current version: 1.1.0**

### What's new in 1.1.0

- **Krystal's Cosmic Caps.** The hippies' job now has you carry a brick of a new, mod-only shroom product, and you can keep it if you're willing to anger the hippies. See [Krystal's Cosmic Caps](#krystals-cosmic-caps).
- **Settings.** The buy cap, relationship numbers and quest rewards can now be changed from your mod manager. See [Settings](#settings).
- **Sturdier saving.** Relationships are now read and written with Newtonsoft.Json. Saves from 1.0.0 load as before.

## Features

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

Each group's leader can now ask you for a job. Open the leader's dialogue and ask. Each visit there is a **50% chance** they have something for you, decided once per visit. If they don't, they say so and you can try again next visit. Finishing a job pays a reward and adds **+0.5** to your relationship with the group, which takes effect on their next visit like any other gain. Jobs can be done once per visit, and an unfinished job is dropped when the group leaves. The chance and the rewards can be changed in the [settings](#settings).

| Group | Leader | Job | Reward |
|---|---|---|---|
| Bikers | Diesel Rodd | **Loose Lips.** A club informant is hiding in The Piss Hut. Knock on the door to bring him out and kill him. | $5,000, 100 XP |
| Businessmen | Johnny Smith | **The Audit.** An auditor follows a daily routine around town (home, the Tall Tower, a cafe, dinner). Find and kill him. | $5,000, 100 XP |
| Hippies | Krystal Bird | **Special Delivery.** Carry a brick of Krystal's Cosmic Caps from one dead drop to another within 2 days. You can keep it instead (see below). | Grow supplies (3 Extra Long-Life Soil, 3 Speed Grow, PGR, Fertilizer), 100 XP |
| Party Bus | Chad Chaddington | **Spread the Word.** Invite 5 townspeople to the Party Bus. Each one can turn you down. | $2,500, 5 Energy Drinks, 100 XP |

#### Krystal's Cosmic Caps

The hippies' package is a brick of **Krystal's Cosmic Caps**, a shroom product added by the mod. Nobody can grow, cook or mix it, so it only ever comes from Krystal.

- **Worth more than regular shrooms:** $98 a unit (1.5x the usual $65), about **$1,960** a brick of 20.
- **Effect:** **Cosmic**, an effect only these shrooms have. NPCs who take Cosmic move 10% slower while it lasts.
- **40% addictive**, and coloured through the game's own shroom shading: deep purple with lighter violet and teal spots, both as a brick and unpacked.
- **It can't be mixed.**
- **Delivering it:** drop the brick in the second dead drop. The hippies collect it straight away, then tell Krystal for the reward. Only the brick itself counts, so it has to arrive packed.
- **Stealing it:** if the brick hasn't reached the second drop by the second day change after you picked it up, or the hippies leave town first, it counts as stolen. The job fails, the hippies lose **1.0** relationship (the same as killing one of them), Krystal has a few words for you, and the product is added to your Product Manager so you can sell it. Unpacking the brick counts as stealing too.
- **Putting it back** in the first drop before the deadline fails the job without a penalty.
- Every player in a multiplayer game needs the mod, since the product only exists with it. If you remove the mod, any of these shrooms in your save disappear.
- For testing, `give cosmiccaps` in the console gives you some.

## Settings

The mod's settings are MelonLoader preferences, kept in `UserData/MelonPreferences.cfg`. Mod managers such as r2modman, Gale and Thunderstore Mod Manager show them as a settings page with a description for each value; you can also edit the file by hand with the game closed. They appear after the game has been started once with the mod. Restart the game after changing them. Every value is kept within a sensible range.

**Better Special Customers: Buy Limits & Relationships**

| Setting | Default | What it does |
|---|---|---|
| `MaxBuyQuantity` | 200 | Units a group buys at Kingpin, before the relationship multiplier |
| `StartingRelationship` | 2.5 | Relationship (0 to 5) a group starts at |
| `BuyMultiplierAtWorst` | 0.5 | Buy limit multiplier at relationship 0 |
| `BuyMultiplierAtBest` | 1.5 | Buy limit multiplier at relationship 5 |
| `RelationshipGainPerVisit` | 0.5 | Relationship gained for selling enough in one visit |
| `SalesNeededForGain` | 0.75 | Share of the buy limit you must sell in a visit for that gain |
| `PunchPenalty` | 0.25 | Relationship lost for punching a customer |
| `AimPenalty` | 0.25 | Relationship lost for aiming a weapon at a customer |
| `PickpocketPenalty` | 0.25 | Relationship lost for a failed pickpocket |
| `KillPenalty` | 1.0 | Relationship lost for killing a customer |
| `PenaltyCooldownSeconds` | 20 | Seconds before the same kind of penalty can happen again |

**Better Special Customers: Quests**

| Setting | Default | What it does |
|---|---|---|
| `JobChance` | 0.5 | Chance a leader has a job on a visit |
| `JobRelationshipReward` | 0.5 | Relationship gained for finishing a job |
| `JobXpReward` | 100 | XP for finishing a job |
| `BikersCashReward` | 5000 | Cash for Loose Lips |
| `BusinessmenCashReward` | 5000 | Cash for The Audit |
| `PartyBusCashReward` | 2500 | Cash for Spread the Word |
| `DebugMode` | false | Leaders always have a job, and jobs can be repeated within a visit |

Krystal's shrooms (price, addictiveness, colours, the Cosmic slowdown) and the hippies' job (theft penalty, deadline, grow-supply reward) are not settings yet; they are constants at the top of `Quests/KrystalsShrooms.cs` and `Quests/HippiesQuest.cs`.

## Requirements

- Schedule I
- MelonLoader
- [S1API](https://github.com/ifBars/S1API) **3.2.1 or later** (used by the quests). Krystal's shrooms need its custom product and custom effect support.

## Installation

1. Install MelonLoader for Schedule I if you haven't already.
2. Put the mod's `.dll` into the game's `Mods` folder:
   - `BetterSpecialCustomers_Mono.dll` for the Mono version of the game
   - `BetterSpecialCustomers_IL2Cpp.dll` for the IL2CPP version
3. Start the game.

> The Mono build has been tested. The IL2CPP build compiles from the same code but hasn't been tested yet.

### Updating from 1.0.0

Replace the `.dll` and update S1API to 3.2.1 or later. Your relationships carry over. If you saved in the middle of Krystal's job, the old package in the dead drop is swapped for a brick of Krystal's Cosmic Caps, so pick it up again.

## Notes and limitations

- **Multiplayer:** relationships are tracked by the host. Other players' screens (for example the arrival popup) may show the normal, relationship-free number. Every player needs the mod for Krystal's Cosmic Caps to exist on their side.
- **Quests are new and lightly tested.** In particular the IL2CPP build, multiplayer clients, and the exact clothing items on the quest NPCs (a missing item is logged and skipped).
- **Debug mode:** turn on the `DebugMode` [setting](#settings) to guarantee a job and repeat it as often as you like within a visit.

## Building

The project has two configurations, `MONO` and `IL2CPP`. Set `S1Dir` in `BetterSpecialCustomers.csproj` to your game folder (the references point at the game's own DLLs) and point the S1API references at your S1API 3.2.1+ DLLs, pick a configuration and build. A post-build step copies the mod into the game's `Mods` folder.

The IL2CPP build references the plain Newtonsoft.Json 13.0.2 (the same build S1API uses) from `Libs/`, not the game's Il2Cpp-wrapped copy; it is already loaded at runtime, so it isn't copied to the output.

## Credits

Made by Lumahex.
