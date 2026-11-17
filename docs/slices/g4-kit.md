# G4 kit: the whole character, against something that fights back

G3 gave five skills. Kristian played them and said the skills cannot be judged one by one: the whole character has to be in.
G4 puts it in: the full loadout of eight skills, the identity gauge, and one enemy that fights back, so the kit is used under pressure.
The slice is done when the agent's checks pass **and** Kristian has played it and said what stays.

Scope chosen by Kristian on 2026-11-17: skills six to eight and the identity gauge, plus an enemy that fights back. No awakening and no tripods yet.
Drafted and built by the agent the same day. Kristian can change anything in it.

Read first: `docs/vision/pillars.md`, `docs/vision/first-class.md`, `docs/slices/g3-skills.md`.

## Scope (build exactly this, nothing more)

In the `ControlRoom` scene:

- **Skills six to eight**, on S, F and G. The eight skill keys are Q W E R and S D F G; A stays attack-move.
- **The identity gauge: focus.** Damage dealt fills it. Full, it is spent with Z on an empowered mode in which every shot and skill deals more.
- **The player's health**, a health bar, and death: the character drops everything and stands at the start again after a moment.
- **One enemy** that walks up to the player and strikes with two telegraphed attacks, and can be killed.
- **Live tuning.** Skills and gauge in `Assets/Game/Settings/Skills.asset`, the enemy in `Assets/Game/Settings/Enemy.asset`.

Out of scope: the awakening, tripods, engravings, Combo and Chain skills, stagger and counter, more than one enemy, enemy variety, damage numbers, a death screen, body animation for the new skills, sound.

## The three new skills (start values, all guesses)

| Key | Skill | Input | Area | Damage | Cooldown | What it is for |
|---|---|---|---|---|---|---|
| S | Scatter Blast | Normal | Cone, 5 m long, 70 degrees | 220 | 9 s | The reward for standing close: the hardest single hit that is not a cast |
| F | Backstep | Normal | Line, 10 m long, 0.8 m wide | 80 | 12 s | Moves the character 5 m straight back from the aim in 0.2 s, then shoots: a second way out besides the dash |
| G | Triple Tap | Normal | Line, 12 m long, 0.6 m wide | 3 x 70 | 7 s | A burst of three hits 0.12 s apart: a short-cooldown filler |

Two new mechanics carry them: a skill can hit several times in a burst, and a skill can move the character during its startup.
The five skills of G3 are unchanged, and Heavy Volley stays on D.

## The focus gauge (start values, all guesses)

| Value | Start value |
|---|---|
| Focus gained per point of damage dealt | 0.05 (so 2000 damage fills the gauge) |
| Full gauge | 100 |
| Empowered mode lasts | 8 s |
| Damage while empowered | x 1.5 |

## The enemy (start values, all guesses)

| Value | Start value |
|---|---|
| Health | 3000 |
| Walking speed | 3.5 m/s (the player runs at 6) |
| Wakes when the player is within | 9 m, or when it is hurt |
| Gives up when the player is beyond | 22 m |
| Slam, used within 3 m | circle of 3 m around it, 0.9 s windup, 20 damage, 1 s recovery |
| Shockwave, used from 3 to 11 m | strip 12 m long and 2.4 m wide, 1.1 s windup, 30 damage, 1.2 s recovery |
| Comes back after its death in | 4 s |

The player has 100 health and stands at the start again 2 s after dying.

## How the pieces behave (decisions the agent made; Kristian can overrule)

1. **Focus comes from damage dealt**, not from hits, so every skill fills it in proportion to what it does.
2. **The gauge is spent whole**, only when full. Spending it takes no time and locks nothing, so Z can be pressed in the middle of a skill.
3. **No focus is gained while empowered.**
4. **The enemy's attack is aimed once**, when its windup begins. The red outline shows where it will land and thickens until it does. Leaving the outline avoids the hit.
5. **A windup is committed.** The strike comes even if the player has left or is out of reach.
6. **The dash's i-frame avoids a strike** that lands during it.
7. **The enemy stands at home until woken**, so the dummies can be used in peace. Hurt, it comes from any distance within its leash.
8. **The enemy is a target like the dummies**: attack-move, right-click and every skill work on it.
9. **Death in the control room is a reset**, not the end of a run: the room is for practice.
10. **Backstep moves away from the aim**, not away from where the character faces, and the shot goes to the aimed point from where it ends up.

## Rules for how you work

The rules of `docs/slices/g2-control.md` and `docs/slices/g3-skills.md` apply, with these changes:

1. Branch `g4/kit`, based on `g3/skills`.
2. What the enemy decides is plain C# in `Roguelike.Rules` (`EnemyBrain`) with EditMode tests. The component only walks, draws and lands the hit.
3. Check that the Editor is not in Play mode before every recompile, scene build and planted bug.

## Pass criteria (the agent checks these)

| # | What | Pass when |
|---|---|---|
| K1 | Compile and rules | The project compiles and the EditMode rule tests pass, including the burst, the gauge and the enemy's decisions |
| K2 | G2 and G3 still hold | Every earlier PlayMode test passes |
| K3 | Scatter Blast | Hits up close and not beyond its range |
| K4 | Backstep | Moves the character its distance straight back from the aim, and the shot lands where it was aimed |
| K5 | Triple Tap | Three hits, a hit interval apart |
| K6 | Focus | Fills with damage dealt; cannot be spent until full; empowered, skills and basic shots deal more and no focus is gained; it ends after its duration |
| K7 | Enemy at rest | Stands at home while the player is far away; walks to the player when hurt; goes home when the player is beyond its leash |
| K8 | Slam | Hits a player who stays in the circle, misses one who leaves it, and is avoided by the dash's i-frame |
| K9 | Shockwave | Hits a player who stays in the line, and misses one who steps aside after the windup began |
| K10 | Life and death | The enemy can be shot, dies, and stands at home again. The player dies at zero health, takes no input while dead, and stands at the start again |
| K11 | Input driver | Fed frames: Z spends a full gauge and does nothing on an empty one; each of the eight keys starts its own skill |
| K12 | Live and seen | In Play mode with the Editor unfocused: the enemy telegraphs and hits, and captures show the telegraph, the health bar and the gauge |
| K13 | The tests can fail | Planted bugs are caught |

## What Kristian judges (the agent cannot)

Play `ControlRoom`, fight the enemy, and change values in the two assets while playing. Double or halve; do not nudge.

- Do the eight skills play together as an instrument? Which stay, which change, which go?
- Is there a rotation worth learning, or is one button always the answer?
- Does the focus gauge change how you play, or is it just a damage button?
- Can you read the enemy's telegraphs, and do you lose health to your own mistakes?
- Is the dash still a scarce, deliberate tool now that Backstep exists?
- Does the camera show enough to fight in?
- Are S, D, F and G the right keys?

## When you are done

1. Write `docs/playtests/g4-kit-agent.md` with the criteria table, what could not be verified, and the surprises.
2. Tell Kristian the scene is ready to play, and how to tune.
3. Stop.
