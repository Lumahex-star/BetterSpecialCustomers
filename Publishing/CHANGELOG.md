# Changelog

Nexus Mods takes one plain-text line per change; paste the lines under a version as its changelog entries.

## 1.2.0 (in development)

Changed: Diesel Rodd's informant in Loose Lips hides in a different building each run, and Diesel tells you which one.
Fixed: Diesel Rodd's informant wasn't wearing his biker outfit. He now wears a biker jacket, leather jacket or open vest, with gloves, boots, a chain and more.
New: Diesel Rodd's informant can have a chest or arm tattoo.
Fixed: the businessmen's auditor in The Audit now wears his suit, tie and glasses again.
Changed: both quest targets are dressed with the current game's outfit items.

## 1.1.0

New: Krystal's Cosmic Caps, an exclusive shroom product ($98 a unit, about $1,960 a brick) that can't be grown, cooked or mixed.
New: the Cosmic effect, only found on Krystal's Cosmic Caps. NPCs who take it move a little slower.
New: Krystal's Cosmic Caps are 40% addictive and deep purple with violet and teal spots, both as a brick and unpacked.
Changed: Krystal Bird's Special Delivery job now has you carry a brick of Krystal's Cosmic Caps between two dead drops within 2 days.
New: you can keep Krystal's brick instead. If it isn't delivered in time, or the hippies leave town first, the job fails, the hippies lose 1.0 relationship, and the shrooms are added to your Product Manager so you can sell them.
New: Krystal has new dialogue for the job, including a line for players who stole from her.
New: settings for the buy cap, relationship numbers (starting value, multiplier range, gain per visit, sales needed, penalties and their cooldown) and quest rewards (job chance, XP, relationship, cash per job, debug mode). Edit them in your mod manager (r2modman, Gale, Thunderstore Mod Manager) or in UserData/MelonPreferences.cfg.
Changed: relationships are now saved with Newtonsoft.Json, which is sturdier. Saves from 1.0.0 load as before.
Changed: now requires S1API 3.2.1 or later.
Note: every player in a multiplayer game needs the mod, because Krystal's Cosmic Caps only exist with it.
Note: if you saved in the middle of Krystal's job in 1.0.0, the old package in the dead drop is swapped for a brick of Krystal's Cosmic Caps.

## 1.0.0

Initial release: higher buy limits, per-group relationships, and a job from each group leader.
