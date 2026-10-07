# Better Special Customers

Make the Special Customer groups worth your time. They buy far more, they remember how you treat them, and each group's leader now has a job for you.

> **AI disclosure:** A large part of this mod was produced with AI. The code, the quest and dialogue text, and this description were written largely with Claude, Anthropic's AI assistant, working from my design decisions. I (Lumahex) chose what the mod should do, directed the work, and tested it in the game, but I did not write most of the code by hand. Please keep that in mind when you report problems, and see **Known issues** below.

---

## What it does

### Higher buy limits
Vanilla groups stop buying at 120 units, and the game's own formula already reaches that at the top rank, so higher ranks did nothing. This mod replaces it with a straight line from each group's normal starting amount up to **200 units at Kingpin**.

### Relationships
Every Special Customer group now has its own relationship with you, from 0 to 5, using the same level names as the game's own relationships (Hostile, Unfriendly, Neutral, Friendly, Loyal).

- **It changes how much they buy.** The group's limit is multiplied by **x0.5** at relationship 0, **x1.0** at 2.5 and **x1.5** at 5. Every group starts at 2.5, so a fresh game plays like vanilla until you start building relationships.
- **You earn it by selling.** Sell a group at least **75% of their limit** during a visit and the relationship goes up by **0.5**. It can only rise once per visit, and it takes effect on their **next** visit.
- **Hurting a group costs you.** Attacking, aiming at or killing their customers, or failing a pickpocket on them, lowers the relationship. Each action has a short cooldown, so one fight doesn't wipe it out.
- **It's saved with your game,** per group. If you remove the mod, your save still loads.

At the top rank and top relationship a group can buy up to **300 units**.

### Quests from the group leaders
Open a leader's dialogue and ask for work. Each visit there is a **50% chance** they have a job (decided once per visit). Finishing a job pays its reward and adds **+0.5** to your relationship with the group. Each job can be done once per visit, and an unfinished job is dropped when the group leaves.

| Group | Leader | Job | Reward |
|---|---|---|---|
| Bikers | Diesel Rodd | **Loose Lips.** A club informant is hiding in The Piss Hut. Knock on the door to bring him out, then deal with him. | $5,000 and 100 XP |
| Businessmen | Johnny Smith | **The Audit.** An auditor follows a daily routine around town. Find him and deal with him. | $5,000 and 100 XP |
| Hippies | Krystal Bird | **Special Delivery.** Carry a package from one dead drop to another. | Grow supplies (3 Extra Long-Life Soil, 3 Speed Grow, 1 PGR, 1 Fertilizer) and 100 XP |
| Party Bus | Chad Chaddington | **Spread the Word.** Invite 5 townspeople to the Party Bus. Some will say no. | $2,500, 5 Energy Drinks and 100 XP |

The bikers' and businessmen's jobs involve violence against an NPC. The target is a new character made for the job, with a random name and look each time.

---

## Requirements

- Schedule I
- [MelonLoader](https://melonwiki.xyz/)
- [S1API](https://github.com/ifBars/S1API) (used by the quests). Use a recent version, and **only one copy**. Having two copies of S1API in your `Mods` folder breaks custom NPCs.

## Installation

1. Install MelonLoader for Schedule I if you haven't already.
2. Install S1API for your version of the game (Mono or IL2CPP).
3. Put the mod's `.dll` in your game's `Mods` folder:
   - `BetterSpecialCustomers_Mono.dll` for the Mono version
   - `BetterSpecialCustomers_IL2Cpp.dll` for the IL2CPP version
4. Start the game.

## Compatibility and known issues

- **Both builds exist, but the Mono build has had the most testing.** The IL2CPP build is newer and less tested.
- **Multiplayer:** relationships are tracked by the host. Other players' screens (for example the arrival popup) may show the normal number without the relationship bonus. Quest XP is awarded by the host.
- **Quests are new.** Some quest NPC outfits use items that may not exist in every game version. A missing item is skipped and logged instead of crashing.
- **There is no config file yet.** The numbers (the 200 cap, the relationship range, rewards) are set in the code. If you want something adjustable, tell me.

## Reporting problems

Please include your `MelonLoader/Latest.log` and say whether you use the Mono or IL2CPP version. Lines starting with `[BetterSpecialCustomers]` and `[quest]` are the useful ones.

## Credits

Made by Lumahex, with a lot of help from Claude (Anthropic).
Built on [MelonLoader](https://melonwiki.xyz/), [Harmony](https://github.com/pardeike/Harmony) and [S1API](https://github.com/ifBars/S1API) by ifBars.
