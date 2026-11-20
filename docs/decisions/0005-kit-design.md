# 0005 The kit: four skills growing to eight, marks, a cooldown ladder

Status: **proposal by the producer, 2026-11-19. Kristian has ruled on the three points under "What he decided"; everything else is a draft. After two recorded play sessions he said he would rather try this kit than go on adjusting the old one: "jeg tror de mikrojusteringene ikke er noe poeng nå, jeg vil heller forsøke kittet foreslått av researchen." He did not answer the open questions at the end, so the first build uses the producer's choices for them.** The core (marks, the start four, light skills, the recast, the focus spend) is being built in work order 005. The research behind it is in `docs/research/g4-kit-design.md`.

## What he decided (2026-11-19)

Asked three questions after the research came in:

- **A run starts with four skills and ends with eight.** "4 fra start og 8 til slutt høres bra ut."
- **Whether focus is a small, frequent spend or a rare, big window can be decided inside the run.** "Det kan vel kanskje drops/valg i runnet avgjøre?" (drops or choices in the run could decide that).
- **The light skills may be cut short by moving; only the heavy ones lock her.** Asked whether this was allowed under pillar 1 (every skill has a phase you are locked into): "drit i pilar 1. de må skrives om uansett" (never mind pillar 1; the pillars have to be rewritten anyway).

What he wants from the kit, from the same day: a high tempo above all ("veldig glad i høy apm"), skill expression, combos and timing-based skills, some skills that are situational rather than part of the best damage rotation, and upgrade paths in the run that change how it is played.

Later the same day, on two of the open questions at the end:

- **The dash stays scarce, but can be upgraded during the run.** "Ja, men kan oppgraderes i løpet av runnet."
- **How skills are pressed should vary.** "Jeg synes vi skal ha hold for å skyte på noen, kanskje en sweetspot på noen spells, noen kombobaserte osv." (hold to shoot on some, maybe a sweet spot on some spells, some combo-based, and so on). The producer's reading: he wants the kit to mix held skills, timed releases and multi-press skills, which this proposal does (Fan Fire, Long Shot's perfect zone, Triple Tap's three presses). The question he was asked was about the basic shot, one click a shot or held fire, and this does not settle it.

**He played the core that evening** (work order 005, merged as 313a63b): "veldig bra. vi nærmer oss noe heftig" (very good; we are getting close to something great). What he said:

- He likes that the right-click attack (Triple Tap) does not slow her.
- She should pick up speed faster after she stops shooting: still eased, not at full speed at once. Done the same evening as a value: `MovingShotRecovery` in `FeelSettings.asset` from 0.5 to 0.25 s (the producer's first try at what he meant; he has not felt it).
- She needs more ways to use the five marks up, not to build them (the producer first read it the other way and he corrected it): "R hadde veldig lang cd, så fienden var alltid på 5 stacks. Så den kan kanskje ta litt lengre tid, + ha en ny måte å bruke de opp på" (R had a very long cooldown, so the enemy was always at five; building could take a little longer, and there should be a new way to spend them). F (Backstep) should be one such way, "på en vanskelig måte" (in a hard way); he did not know how. The producer's answer, in work order 007: Backstep becomes two presses, the hop and then the shot, and a shot pressed late in its window spends the marks; Scatter Blast's cooldown goes from 8 to 6 s; a mark from basic shots comes every fifth hit, not every fourth. All guesses. Told how F would work, he said: "enig i f forresten" (agreed on F, by the way). He has not played it.

## Built on 2026-11-20, and what the producer chose for him

He was at the office that day. Each choice below is his to overrule after playing; the numbers are first guesses in `Skills.asset` and the values classes.

- **The pool is in** (work order 015): twelve skills, eight keys. The start four keep their keys; a skill taken as a floor reward goes on the first free key. The four new skills were built as the table below describes them: Tag Round (10 % more damage to the tagged target for 8 s), Breaker Slug (stagger 80, 30 % more stagger per mark spent), Slide (6 m, two charges, shoots during it, no invulnerability) and Stopping Shot (counters a blue wind-up from within 60 degrees of the enemy's front: the attack is dropped, the enemy dazed 2 s, a third of the focus gauge). Counterable today: the brute's Shockwave, the guard's Sweep, the boss's Charge, the caster's Grave Rain.
- **Long Shot's perfect band** is from 0.8 to 1.0 s of charge: twice the damage and stagger. **Powder Keg** lies with a fuse of 1.5 s and bursts at once, for half as much again, when a shot of hers passes through it.
- **Upgrades** (017): 27 in the first table, two or three a skill, taken one of three after each floor's elite room, at least one of the three a change to how the skill is played. The table as built is in `Assets/Game/Rules/SkillUpgrades.cs`.
- **The game says when she got it right** (019, 021, 024): the words CLEAN, PERFECT, COUNTER, the marks spent, DAZED, and EARLY or LATE for a missed timing; damage numbers that can be switched off in the pause menu; a tone for a clean tap, a perfect release and a counter; an effect for each.
- **The focus choice** (025) was being built when this was written: offered once, after floor 1's reward, between Hair Trigger and Steady Hand as described under Focus below.
- **Still unanswered by him:** whether the basic shot gets held fire (his ruling in decision 0002 stands: one click, one shot).

## The idea in four sentences

The pistols set up and the shotgun cashes in. Pistol skills and well-timed basic shots put **marks** on an enemy; shotgun skills spend the marks for a bigger hit, so every press either builds or pays off, and the loop pulls her in close and back out again. Cooldowns form a ladder, so a few presses are events and the rest are rhythm. A run starts with the fast core of that loop and adds the heavy and the situational skills as rewards.

## Marks

- An enemy carries up to **5 marks**, shown over it. They fall off after 6 s without a new one.
- **Builders** add marks: the pistol skills, and every fourth basic shot that hits.
- **Spenders** take all marks off the enemies they hit and deal more for each: the shotgun skills and the heavy shot.
- This is the small loop inside the kit that the research found in every Lost Ark class and that ours lacks. It gives the fillers a job and makes "when do I walk in" a decision every few seconds.

## The four skills a run starts with

All values are first guesses. Three of the four exist in the game today and change jobs.

| Skill | Weapon | Job | Input | Cooldown | Lock | What changes from today |
|---|---|---|---|---|---|---|
| **Quick Shot** | Pistol | Filler and builder: 1 mark | Press | 3 s | None: fires on the move | Shorter cooldown, no lock, adds a mark |
| **Triple Tap** | Pistols | Builder with a timing test | **Recast**: three presses, each a shot, the next within 0.6 s | 5 s | None | Today it is one press. Each shot adds a mark; a third shot pressed in the last third of its window is a "clean" tap: 2 marks and a crit |
| **Scatter Blast** | Shotgun | Spender, close range | Press | 8 s | Short, cut by the dash | Spends marks: +25 % damage per mark. Medium stagger |
| **Backstep** | Pistols | Mobility that does damage | Press | 10 s | Its own hop | Unchanged, plus: the shot adds 1 mark, so it also restarts the loop after a blast |

The loop with these four: basic shots and pistol skills from range until the target has marks, walk in, Scatter Blast, Backstep out, again. About eight seconds a turn.

## The pool for skills five to eight

At four fixed points in a run she is offered **one of three** skills from this pool and takes one into the next free slot. Eight in the pool, four taken: no two runs need end with the same eight. Four of these exist today.

| Skill | Rung | Job | Input | Cooldown | Lock | Notes |
|---|---|---|---|---|---|---|
| **Long Shot** | Mid | Single-target hit with a timing test | **Charge with a perfect zone**: a marked band on the bar | 12 s | While charging, slowed not stopped | Release in the band: double damage and it pierces. Outside it: the weak shot, and the cooldown is the same. Exists, without the band |
| **Powder Keg** | Mid | Area damage, and a hit that lands later than it is pressed | Place at the cursor | 14 s | None | Goes off after 1.5 s, or at once when she shoots it: a two-step combo. Exists, without the delay |
| **Tag Round** | Light | The "keep it up" skill | Press | 6 s | None | The target takes 10 % more damage for 8 s. Low damage. New |
| **Stopping Shot** | Light | Counter | Press | 10 s | None | Hits an enemy in a counterable wind-up from its front: the attack is cancelled, the enemy is dazed, focus fills by a third. Otherwise an ordinary small hit, so it is never a dead press. New, and needs an enemy that can be countered |
| **Fan Fire** | Mid | Stagger and crowd control | Hold | 16 s | While held, slowed | High stagger over its length, 1 mark a second to everything in the cone. Exists |
| **Breaker Slug** | Heavy | Stagger on demand | Press | 20 s | Short | Shotgun. Highest stagger in the kit, modest damage, spends marks for stagger instead of damage. New |
| **Heavy Volley** | Heavy | The big payoff | 1 s cast | 24 s | Full: only the dash ends it | Highest damage in the kit, +40 % per mark. The one skill that asks for a real opening. Exists |
| **Slide** | Light | A second way to move | Press, direction of the move keys | 12 s, 2 charges | Its own slide | She can fire basic shots during it. New |

The ladder, all twelve: light 3 to 6 s, core 8 to 10 s, mid 12 to 16 s, heavy 20 to 24 s. Today all eight sit between 5 and 12 s.

## Locks

- **Light skills do not lock her.** They fire while she moves, like the basic shot.
- **Mid skills** hold her for their active part and are cut short by the dash, with the slide of decision 0004.
- **Heavy skills lock fully** until the last hit. That is what makes them heavy: the size of the hit pays for the opening it needs.

## Focus

- **What fills it:** playing the loop well. Marks spent, clean taps, perfect releases and counters fill it; plain damage only trickles. Today any damage fills it at the same rate.
- **Base spend (Z, when full):** all running cooldowns are cut by 40 % at once, and for 6 s every basic shot that hits adds a mark. So spending it means more presses and a faster loop, not a bigger number.
- **The choice in the run** (his idea), offered once, early:
  - **Hair Trigger:** the gauge has three parts and each can be spent by itself: a 15 % cut and the next skill fires as a clean one. A metronome every few seconds; the high-tempo way.
  - **Steady Hand:** only a full gauge can be spent, on 10 s in which the start-kit skills change shape (five taps for three, the blast fired twice). Rarer, bigger, and it rewards saving the heavy skills for it.

## Upgrades during a run

Per skill, two tiers with two or three choices (the reduced tripod set of `docs/vision/first-class.md`), offered one of three. The research gives five kinds of upgrade that change how a skill is played, and most picks should still be plain numbers. One example of each kind:

- **Shape or range:** Scatter Blast becomes a narrow slug that reaches 10 m, so the walk-in disappears.
- **Remove or add a task:** Long Shot fires at once, always as a perfect release, with a longer cooldown. The timing test is traded for reliability.
- **Charges:** Quick Shot holds two.
- **Two buttons tied together:** every basic shot that hits takes 0.2 s off Triple Tap's cooldown.
- **Automatic:** Tag Round is applied by every clean tap, and its key becomes free for another skill.

## What enemies must do for this to matter

With one enemy type nothing here can be judged. The kit needs, in the practice room:

- A **crowd** of weak enemies, for Powder Keg and Fan Fire.
- An enemy with a **stagger bar** that is dazed when it empties and takes more damage while dazed: this is what gives Heavy Volley its opening and Breaker Slug its job.
- An enemy with a **counterable wind-up**, shown in its own colour.
- An enemy that **punishes standing still** at range, for the mobility skills.

## Tempo

No source gives a target number, so this is ours, to be checked with the play-session recorder: the start kit asks for about 70 skill presses a minute if everything is used on cooldown (Quick Shot 20, Triple Tap 36, Scatter Blast 7, Backstep 6), on top of the basic shot. With eight skills, about 90. The research counted 45 to 50 a minute for Lost Ark's fast Gunslinger playstyle, by the same arithmetic.

Two things follow that he should feel for rather than read about: whether a click for every basic shot still works at this tempo (hold-to-fire is not built; his ruling in decision 0002 was one click per shot), and whether the start kit is already too busy for a first room.

## What this changes in the vision documents

- **The pillars** were rewritten the same day, after he had read this proposal and a draft of them: decision 0006. Whether the dash stays scarce with a second mobility skill in the pool is still open; the new pillar 1 does not mention it.
- `docs/vision/first-class.md`: the loadout row now reads four skills at the start and four more from a pool of eight, on his decision above.

## Open, for Kristian

1. Marks as the heart of the kit: pistols build, shotgun spends. Yes, or another loop?
2. The start four. Is it right that the first heavy skill is a reward and not there from the start?
3. The two focus choices, and whether the base spend should exist at all before the choice.
4. Light skills with no lock at all, or a very short one?
5. ~~Does the dash stay scarce?~~ Answered: yes, and upgrades in the run may change that.
6. Names: all placeholders.

## What to build first, if he says yes

The smallest thing that lets him feel the loop: marks, the start four with their new jobs and locks, and the base focus spend, in the practice room, with the other four slots empty. Then the stagger-bar enemy and the crowd. Skills five to eight and the upgrades come after he has played the core.
