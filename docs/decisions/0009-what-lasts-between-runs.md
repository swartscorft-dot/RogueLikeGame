# 0009 What lasts between runs

Status: **proposal by the producer, 2026-11-20. He has not ruled on it.** Later the same day he asked for a first version to be built behind a switch so he could try it (work order 029): it is in the game, **off by default**, turned on with the line "Between runs (trial)" in the pause menu. Off, nothing is earned, shown or saved. What was built follows this proposal, with nine pieces and a budget of 3 to 8 points; the save is `between-runs.json` under the game's persistent data folder. When this was first written: Kristian asked that morning for a written proposal and no code ("Skriv et forslag, bygg ingenting"). The research behind it is `docs/research/reports/m1-meta-progression.md` (a web research job of the same day; its quotes are not checked against the sources by the producer, and it marks its own gaps).

## What is already fixed

From the pillars and the core loop, signed by him:

- "Meta mostly opens new choices, plus some raw stats in noticeable, capped steps." Not: "permanent upgrades of a few percent that cannot be felt."
- "You choose a loadout in the hub." A run that ends, by the boss or by death, gives "something lasting that opens new choices for the next run".
- Before the demo: no gear, no story, no town, no second class.

## What the research found, in five lines

- The structure that fits the pillar best is **owning more than you can equip**: Hades II's Arcana cards cost points to equip and the budget is smaller than the set, so every unlock is a new choice and never plain growth. Shape of Dreams moved from permanent stat upgrades to the same idea before its launch.
- The closest game to ours, Wizard of Legend 2, keeps two things apart: **unlocking a skill** and **choosing which skills a run starts with**.
- Our run offers three skills out of a pool after each floor. Every skill added to that pool makes a wanted skill rarer. Games answer this with a ban, a reroll, or by putting unlocks in the loadout and not in the random pool.
- Where games went wrong it was the pace, not the structure: Rogue Legacy 2 and Shape of Dreams both had to cut the grind after release.
- Developers disagree on raw power between runs. Supergiant gives it, in steps that are "immediately palpable" (20 %). The Risk of Rain team gives none and wants a fresh save to be able to win.

## The proposal

**One screen between runs, one currency, a rack with a budget.**

1. **One currency**, earned in every run and never lost: a little per floor cleared, more per elite room cleared, a large fixed sum for the act boss. A run that dies on floor 2 still pays for what it cleared. The name is open.
2. **No hub to walk in before the demo.** After a run's end screen comes one screen with two parts, the rack and the loadout, and a button that starts the next run. The practice room stays where it is.
3. **The rack** (the Arcana idea). She owns pieces and equips as many as the budget allows. The budget starts at 3 points and grows to about 8, by buying and by first-time feats (the first boss kill, the first act without a death under half health, and so on). A piece costs 1 to 3 points to equip. Two kinds:
   - **Rule changers**, the majority: start a run with focus half full; the first shot on an enemy with no marks gives two; one reroll of a skill offer per run; one ban of a pool skill per run; a second dash charge on a longer cooldown; the first chest of every floor offers three.
   - **Stat steps**, a few, large and capped: health +20 % (two ranks at most), damage +15 % (one rank). Each is felt at once, and each competes for the same budget as a rule changer, so nobody equips only numbers without giving something up.
4. **The loadout.** The start four stay fixed for the demo: they are the loop (two builders, a spender, a move). What she chooses before a run is the rack. Choosing which skills a run starts with comes when the pool has grown past eight, and then unlocks go to the loadout, never straight into the random offers.
5. **The boss gives an option, not only currency**: the first kill of the act boss opens a fixed, named piece.
6. **Not before the demo:** a difficulty she can turn up, a walkable hub, more than one currency, lasting unlocks of skill upgrades.

Size of the first build, if he says yes: a save file, the currency, the screen, about ten pieces. One large work order, or two.

## Why this shape

- It is the smallest thing that makes a second run differ from the first by a choice she made, which is what the pillar asks for.
- The budget is what keeps "mostly new choices" true as the set grows: more pieces is more to choose from, not more power.
- The reroll and the ban live in the same system, so the three-of-eight offers do not get worse as content is added.
- One currency and one screen are one economy to tune, with the play-session recorder already measuring what a run earns.

## What it costs and where it can go wrong

- The pace must be tuned by play: how many runs until the rack is full. The research's two cautionary cases were both too slow. A first guess to test: a new piece every one or two runs, the whole rack in fifteen to twenty.
- Rule changers are code each; stat steps are data. Ten pieces is about seven small features.
- If the game cannot be won on an empty rack, the stat steps stop being a choice and become a requirement. The act should be winnable with nothing equipped, by a good player.

## Added 2026-11-24: ranks bought with Echoes (work order 099)

**What he said.** 2026-11-23, about this proposal's trial: "noen som bør være på rene stats og. så er det også en sink". Asked on 2026-11-24 what he meant, with three choices, he ticked "Rang som kjøpes mange ganger": pure stats bought rank by rank between runs, the price rising with each rank. He did not tick "fixed stat pieces that take budget points" nor "offers inside a run". This answers open point 2 below in part: raw power, yes, and as its own store beside the rack.

**What was built**, behind the same switch as the rest of the trial (off by default; "Between runs (trial)" in the pause menu):

- Four stats, each a line of data in `Rules/BetweenRunsRanks.cs`. The sizes and the number of ranks are the producer's, his to tune:

  | Stat | A rank gives | Ranks |
  |---|---|---|
  | Health | +10 % of her own health | 10 |
  | Damage | +6 % | 10 |
  | Crit chance | +3 points | 8 |
  | Room heal | +2 points of the share healed when a room is cleared | 5 |

- A rank is not a rack piece: it takes no budget point, is never equipped or taken off, and always counts once bought. A buy cannot be undone.
- One price rule for all four: the first rank costs `MetaValues.FirstPrice` (40), each next one the one before times `MetaValues.PriceGrowth` (1.4), rounded to the nearest 5: 40, 55, 80, 110, 155, 215, 300, 420, 590, 825. The whole store is 7395 Echoes; the rack is 810. A run that dies on floor 3 earns 50, a cleared act 225.
- A rank's share adds to the rack's stat pieces' shares (Thick Hide, Heavy Hand) by the rule `HerStats` already had for them.
- The save stays `between-runs.json`. It becomes version 2 once a rank is bought; an older save reads as rank 0 of everything.

**Where it parts from the proposal above.** Point 3 said stat steps should be few, large, capped and compete for the rack's budget, "so nobody equips only numbers without giving something up". The ranks are capped steps she can feel, as the pillar asks, but they do not compete for the budget: every rank bought is plain power on top of whatever the rack holds. With everything bought she has twice her health and 1.6 times her damage. The risk named under "What it costs" (the act must be winnable with nothing bought) is now larger and has to be watched in his sessions.

**Open, for him, once he has tried it:** whether each stat should have a last rank at all (the pillar he signed says "capped"; a sink without an end would need the pillar changed); whether the first price should be halved (one stat's first three ranks take about four runs that die on floor 3, all four stats' take about fourteen); whether health should stop at 5 ranks instead of doubling at 10; whether room heal ranks count in the elite room (built: yes).

## Open, for Kristian

1. The shape: a rack with a budget, as above? Or the smaller version with no rack (buy skills for the pool and choose the start four), or the larger one with a hub to walk in?
2. Raw power: the few large capped steps above, or none at all so that only the player improves?
3. Is the start four fixed, or does she choose it before a run once more skills exist?
4. How long should the demo's rack last: about fifteen to twenty runs?
5. What the first boss kill opens.
6. Should progress in the demo carry into the full game?
