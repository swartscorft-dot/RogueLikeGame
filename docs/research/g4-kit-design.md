# Research for the kit design (G4)

Started 2026-11-19. The first G4 build put eight skills, the focus gauge and one enemy in the game (`docs/slices/g4-kit.md`), but the skills were made to cover input types, not as a designed kit. Kristian's aim for the next step: design the character, get a flow in the rotation, and give every key press a meaning.

## What Kristian wants from the kit (his words, 2026-11-19, translated)

- He likes high actions per minute and skill expression. He came back to this the same day to underline it: "merk, at jeg er veldig glad i høy apm" (note that I am very fond of high APM). Treat high tempo as a requirement of the kit, not as one preference among several.
- Combos and timing-based skills are fun.
- Not every skill has to be part of the optimal damage rotation. Some should be situational: "for example mobility, stagger, stun or block as in Lost Ark, something for area damage and so on".
- Because the game has roguelike elements like Hades, there must be upgrade paths during the run that change how you play and what the optimal rotation is.

## The research he agreed to

| # | What | How | State |
|---|---|---|---|
| 1 | His own taste | Asked | Answered above |
| 2 | Three Lost Ark kits taken apart skill by skill, tripods that change a skill's job, boss damage windows | Web research on the API credits | Done: `reports/a-lost-ark-kits.md` |
| 3 | How Hades, Shape of Dreams and Risk of Rain 2 let a run change the way a kit is played, and what developers have said makes a rotation feel good | Web research on the API credits | Done: `reports/b-roguelike-kits-and-principles.md` |
| 4 | A recorder for his own play sessions: which skills he uses and does not, time between presses, damage per skill, dead time | Work order 004 for the runner | Being built |

## How far to trust the two reports

They were written by an unattended research agent (Opus 5.5) on 2026-11-19 and read in full by the producer.

- **Game facts** (which skills, inputs and upgrades exist in Lost Ark, Hades, Risk of Rain 2 and Shape of Dreams) agree with what the producer knows of those games. Not checked against the sources one by one.
- **Developer quotes** are as the agent reported them, with links. The producer has not opened the sources. The reports mark what was seen only as a search snippet.
- **Lost Ark numbers** are base cooldowns and 2022 tripod data: read them as structure, not as current values. No source publishes animation lock times or actions per minute; the per-minute counts in report A are the agent's own arithmetic.
- **"Against our kit"** in both reports is written from a description of our game, not from playing it.

## Findings that shape the kit

From Lost Ark (report A):

1. **A cooldown ladder.** Fillers at 6 to 15 s, mid skills at 16 to 24 s, heavy hitters at 27 to 36 s, one extra skill at 60 to 75 s. The peaks of a rotation come from the top rungs. Our eight are all at 5 to 12 s: every press weighs the same and nothing is a payoff.
2. **Spending the gauge gives more presses, not only bigger numbers.** The two gauges studied cut all running cooldowns by 25 to 50 % when spent, on top of a damage bonus. Ours multiplies damage by 1.5 for 8 s and changes nothing about what is pressed.
3. **The gauge gives skills roles.** Builders fill it and are used outside the window, spenders are saved for it, and one builder is to be avoided inside it. Ours fills from any damage, so no skill has a gauge role.
4. **Small loops inside the kit.** Light skills stack something that one heavy skill cashes in; one group of skills speeds up the cooldowns of another. This is what gives fillers a reason to exist.
5. **Hits that land later than they are pressed.** A delayed heavy hit cast just before the window lands inside it. Timing skill without fast hands.
6. **Utility rides on damage skills.** Mobility, the party debuff and the counter are mostly properties of skills that also do damage. Slots that do no rotation damage are 0 to 2 of 8. Stagger is a grade on every skill, raised by an upgrade, not a slot of its own.
7. **Three to five input types per kit, each a small test.** The two our kit lacks are the two he named: a recast inside a short window, and a hold with a marked "perfect zone".
8. **Tripods change a skill's job.** They change the input type (charge to instant, hold to cast), add a recast or a second charge, move damage into stagger, remove an aim or position test, or tie a skill to the gauge as a builder or a spender.
9. **The second playstyle of a class is the same kit at another tempo.** It changes the size of the spend (one big or many small), the fill rule, or removes a subsystem. One of the pair is usually the high-tempo one.
10. **Rotations are priority lists, not scripts.** The guides say so in as many words.

From the roguelikes and the developers (report B):

11. **Few buttons, fixed for the run.** Hades has five inputs, Risk of Rain 2 four skills, Shape of Dreams two skills growing to four. None starts or ends near eight, and none adds a button that was not a slot from the start. Scarce slots are what make a reward a trade.
12. **Upgrades that change how the kit is played do one of five things:** change an input's range or shape, remove or add a task (a reload), add charges, tie two buttons together (each basic shot shortens a skill's cooldown), or make a press automatic. They are a minority everywhere: about two per run in Hades.
13. **Choices come in threes, tied to a slot, with exclusions.** That, more than the size of the pool, keeps runs from ending in the same build.
14. **Commitment is the price of a big payoff; the rest should be cancellable.** We lock her the same way on all eight skills.
15. **A timing window needs a visible target, a real reward and a clear price for missing.**
16. **Enemies are what make the rotation differ from fight to fight.** With one enemy type we cannot see whether ours does.

Against what he asked for:

- **High tempo is treated as a cost by the developers who are best at it.** Smilegate said the Gunslinger's stance swapping "led to increased difficulty" and changed it to "lessen the burden"; the guides list "high APM" as a drawback of every build that has it. Lost Ark's answer is not a lower ceiling but a second, calmer playstyle beside the fast one. Path of Exile 2 and Smilegate's "minor stress is intentional" support him.
- **Situational skills are liked when they also do damage and disliked as dead slots.** A stun earns its place when using it at the right moment also pays in damage.
- **Not every upgrade can change the rotation.** In the games studied most picks change numbers and a few change play.
- No source gives a number for actions per minute or decisions per second. The recorder (item 4) is our only measure.

## The first recorded session (2026-11-19, 27 s on the dummies, no fight, commit 0bedc06)

One short session on targets that do not fight back: a first reading, not a baseline to hold anything to. From `PlaySessions/2026-11-19_15-58-50-summary.txt` (the folder is not in git).

- **He presses almost twice as often as the kit lets him act.** 147 presses, 81 of them started something: 326 presses a minute against 180 actions a minute, and 438 against 234 in the busiest ten seconds.
- **22 presses hit a skill on cooldown,** 11 of them on Triple Tap alone (pressed 16 times, started 3). He hammers it like a skill with several presses in it.
- **40 of 106 clicks fired no shot:** he clicks faster than the basic shot's 4.4 a second.
- **The basic shot did a third of the damage** (33 %), more than any skill; Fan Fire 18 %, Heavy Volley 11 %.
- **Long Shot was tapped, not charged:** held 0.10 s, fired at 10 % charge.
- **Focus filled in under 8 s** and was spent within a second of filling; 46 % of the damage was dealt while empowered. On dummies this says little.
- A flaw in the summary: time spent holding Fan Fire or casting Heavy Volley counts as a gap between actions, so "gaps over 1 s" (4, 6.2 s) is mostly holds and casts, not idling.

## The second recorded session (2026-11-19, one fight of 14 s against the enemy, commit 0bedc06)

From `PlaySessions/2026-11-19_16-01-26-summary.txt`. Still one short session.

- **The fight is over before a rotation can exist.** The enemy's 3000 health lasted 14.1 s. Every skill but Quick Shot was started once. With cooldowns of 5 to 12 s, a 14 s fight is one press of each.
- **The enemy never touched him.** It started 7 attacks; 6 were avoided by walking out of the area, none landed, none needed the dash's protection. He was on the move 68 % of the fight. Nothing in the fight asks for a dash, a counter or a stagger.
- **The basic shot did 48 % of the damage,** all eight skills together 52 %.
- **Tempo in the fight:** 217 started actions a minute, 344 presses a minute. 12 presses hit a cooldown (Quick Shot 3, Triple Tap 3, Scatter Blast 2, the dash 2), and 17 of 56 clicks fired no shot.
- **Hold and charge skills were tapped again:** Long Shot at 10 % charge after 0.10 s, Fan Fire held 0.26 s (1.57 s on the dummies). Under pressure he does not stand and hold.
- **Focus filled once, after 11.9 s, and was never spent:** the fight ended two seconds later.

What both sessions say together: he plays faster than the kit answers, the basic shot carries the damage, the skills that ask him to stand are cut short, and the enemy neither lasts long enough nor threatens enough to make any press matter. The kit and the enemy have to be redesigned together.

## Questions the design has to answer first

1. Does a run start with all eight skills, or with a few and slots that fill during the run? (Pillar 2 says eight skills; pillar 3 says skills are rewards along the way.)
2. What does focus do when spent, and what builds it?
3. Which three or four skills are the core loop, and what is the job of each of the others?
4. Which skills lock her, and which can be cut short?
5. Where does the timing test live, and what does missing it cost?

The full list of ten is at the end of report B.

## What comes out of it

A proposal for the kit that he reacts to: a job for each of the eight skills, how the gauge drives the rotation, what upgrades change during a run, and what an enemy must do for the presses to matter.
