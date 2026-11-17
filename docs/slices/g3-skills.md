# G3 skills: do the five input types work in the hand?

G2 built the control. G3 builds the skill system on it and the first five skills, one for each input type the first class needs.
The slice is done when the agent's checks pass **and** Kristian has played it and said which skills and rules stay.
G4 takes the rest of the kit: skills six to eight, the identity gauge and tripods (`docs/vision/first-class.md`).

Drafted and built by the agent on 2026-11-17. Kristian said: control is good enough to build on, draft G3 and build it without waiting for approval of the scope.
He can change anything in it.

Read first: `docs/vision/pillars.md`, `docs/vision/first-class.md`, `docs/slices/g2-control.md`.

## Scope (build exactly this, nothing more)

In the `ControlRoom` scene:

- **A skill system** with five input types: Normal, Point, Holding, Charge, Casting. Combo and Chain wait, as the first-class table allows.
- **Five skills**, one per input type, on Q, W, E, R and D. A is taken by attack-move.
- **Cooldowns**, one per skill.
- **A skill bar** at the bottom of the screen: key, name and cooldown per skill, and a bar for the cast, hold or charge.
- **An aim outline** on the ground for the skill in use: the area it will hit.
- **Three more dummies** in a tight group, for skills that hit an area.
- **Live tuning.** Every skill value lives in `Assets/Game/Settings/Skills.asset`. A change in the Inspector applies to the next use and is kept.

Out of scope: skills six to eight, the identity gauge, tripods, the awakening, engravings, Combo and Chain, enemies that act, damage to the player, a body animation per skill, effects beyond grey-box shapes, sound.

## The five skills (start values, all guesses)

| Key | Skill | Input | Area | Damage | Cooldown | Timing |
|---|---|---|---|---|---|---|
| Q | Quick Shot | Normal | Line, 12 m long, 0.6 m wide | 100 | 4 s | 0.15 s startup, 0.3 s recovery |
| W | Powder Keg | Point | Circle, 2.5 m radius, placed up to 8 m away | 180 | 8 s | 0.5 s startup, 0.2 s recovery |
| E | Fan Fire | Holding | Cone, 7 m long, 50 degrees | 30 per hit | 10 s | 0.2 s startup, a hit every 0.15 s for up to 2 s, 0.3 s recovery |
| R | Long Shot | Charge | Line, 16 m long, 0.5 m wide | 90 to 300 | 12 s | 1 s to full charge, held at most 2.5 s, 0.1 s from release to shot, 0.4 s recovery |
| D | Heavy Volley | Casting | Line, 14 m long, 2 m wide | 400 | 16 s | 1 s cast, 0.5 s recovery |

The names and numbers are the agent's own. Nothing is taken from Lost Ark's Gunslinger except the input types.

## How the pieces behave (decisions the agent made; Kristian can overrule)

1. **The cooldown starts when the skill starts.** A skill that a dash cancels is spent.
2. **A skill hits every target in its area.** There is no cap and no falloff.
3. **Aim.** Normal, Point and Casting skills keep the aim of the press. Holding and Charge skills follow the cursor until the key goes up, and the character turns with them at once.
4. **Point** lands on the ground point under the cursor. A point beyond the range is pulled in to the range.
5. **Holding** hits once when its startup ends and then on every tick while the key is down. It ends at the release or at its longest hold. Released during the startup, it still hits once.
6. **Charge** fires at the release, with 30% of its damage at no charge and all of it at full charge. Held past its longest hold, it fires by itself.
7. **Casting** is a long startup with a bar. It differs from Normal only in length and in the bar. Nothing interrupts it yet except the dash, because nothing hits the player.
8. **The G2 rules hold for every skill.** The character is locked from the press to the end of recovery. Only the dash cancels. A move order waits. A skill pressed during a lock is buffered for the input buffer time, then dropped.
   A buffered Holding or Charge skill whose key went up while it waited starts as already released.
9. **Hit-stop on every hit** that lands, so Fan Fire stops the game for a moment on each of its hits.
10. **One body animation for all skills**: the shooting clip, entered so that its firing moment falls on the first hit.

## Rules for how you work

The rules of `docs/slices/g2-control.md` apply, with these changes:

1. Branch `g3/skills`, based on `g2/control`.
2. The scene, the materials and both settings assets come from `AgentScripts/ControlRoomBuilder.cs` (the G2 builder, renamed). It never overwrites `FeelSettings.asset` or `Skills.asset`.
3. Skill rules (timing, areas, damage, cooldown) are plain C# in `Roguelike.Rules` with EditMode tests.
4. Tests drive skills through `PressSkill`, `ReleaseSkill` and `AimAt`, or by feeding the input driver frames.
5. **Never recompile or rebuild the scene while the Editor is in Play mode.** Check first. A recompile during Play mode breaks the running game.

## Pass criteria (the agent checks these)

| # | What | Pass when |
|---|---|---|
| S1 | Compile and rules | The project compiles and the EditMode rule tests pass, including the timing of each input type |
| S2 | G2 still holds | Every G2 PlayMode test passes |
| S3 | Normal | Hits once, is refused while on cooldown, and works again after it |
| S4 | Point | Hits every dummy in its circle and none outside it. Aimed beyond its range, it lands short |
| S5 | Holding | Hits while the key is down, stops at the release, ends by itself at its longest hold, and follows the cursor |
| S6 | Charge | Released early it hits weakly, held to full it hits with all its damage, never released it fires by itself |
| S7 | Casting | Nothing lands before the cast is over. A dash cancels it and the skill is spent |
| S8 | Buffer | A skill pressed late in another skill's recovery is the one that starts. A Charge skill released while it waits fires with no charge |
| S9 | Input driver | Fed frames: each skill key starts its own skill; a held key keeps a Holding skill going, the aim follows the cursor, and letting go ends it |
| S10 | Live and seen | In Play mode with the Editor unfocused: each skill lowers a dummy's health, and captures show the aim outline and the skill bar |
| S11 | The tests can fail | Planted bugs in the skill system are caught |

## What Kristian judges (the agent cannot)

Play `ControlRoom` and change values in `Skills.asset` while playing. Double or halve; do not nudge.

- Are the five input types different enough in the hand to be worth having?
- Which of the five skills stay, which change, and which go?
- Does a held or charged skill that turns freely with the cursor still feel committed? (decision 3)
- Is a cancelled skill being spent right? (decision 1)
- Is hit-stop on every Fan Fire hit readable or annoying? (decision 9)
- Does the camera show enough for skills of 12 to 16 m?
- Is D the right fifth key, and where do skills six to eight go?

## When you are done

1. Write `docs/playtests/g3-skills-agent.md` with the criteria table, what could not be verified, and the surprises.
2. Tell Kristian the scene is ready to play, and how to tune.
3. Stop. ~~G4 does not start before Kristian has said which skills and rules stay.~~
   Changed by Kristian on 2026-11-17, after playing: the skills cannot be judged one by one, only with the whole character in. They are judged in G4, together with the rest of the kit.
