# G2 control: is the character right to control?

G1 tested the agent's loop. G2 builds the first kept code: the character control that pillar 1 describes.
The slice is done when the agent's checks pass **and** Kristian has played it and tuned the feel spec.
Skills are not designed here. They come in G3 and G4, once control is right (`docs/vision/first-class.md`).

Drafted by the agent on 2026-11-17 at Kristian's request ("draft G2 and build it"). Kristian can change anything in it.

Read first: `docs/vision/pillars.md`, `docs/vision/feel-spec.md`, `docs/decisions/0001-engine.md`.

## Scope (build exactly this, nothing more)

One grey-box scene, `ControlRoom`, with:

- **Move.** Right-click moves; a held click follows the cursor. Run speed, time to full speed and turn time from the feel spec.
- **Dash** on Space, toward the cursor. Cooldown, distance, duration and i-frame from the feel spec. It cancels a shot or skill in progress.
- **Basic attack with attack-move.** A shows the basic-attack range, left-click confirms, right-click or Esc cancels.
  A click on the ground walks there and shoots the nearest target that comes into range. A click on a target attacks that target.
  The shot has startup, active and recovery from the feel spec, and the character is locked for all three.
- **One placeholder skill** on Q, aimed at the cursor. It exists so cursor aim, facing on cast, the cast lock and dash-cancel can be judged. G3 replaces it.
- **Input buffer.** A dash or skill pressed while the character is locked fires when the lock ends, if it was pressed within the buffer time.
- **Hit-stop** on every hit that lands.
- **Camera** with the feel-spec pitch, distance and field of view.
- **Three training dummies** and a debug readout.
- **Live tuning.** Every feel-spec value used here lives in one asset, `Assets/Game/Settings/FeelSettings.asset`. A change in the Inspector applies while playing and is kept.

Out of scope: enemies that act, damage to the player, real skills, the identity gauge, tripods, back and head attacks, telegraphs, UI beyond the debug readout, art, audio, rooms, the hub.

## How the pieces behave (decisions the agent made; Kristian can overrule)

1. **Orders stay, presses expire.** Move, attack-move and attack-target are orders. The newest order replaces the old one and takes effect when the current lock ends, however early it was given.
   Dash and skill are presses. They go through the input buffer and are dropped when they are older than the buffer time.
2. **Only the dash cancels.** A move order does not cancel a shot. The shot finishes its recovery first.
3. **Attack-move keeps shooting** while a target is in range, and continues to the clicked point when none is.
4. **The basic shot is targeted** and cannot miss a living target. The placeholder skill is a line that can miss.
5. **Hit-stop freezes the whole game** (time scale 0) for the feel-spec time.
6. **In range** means: distance to the target's edge is at most the range.

## Values this slice adds

Added to the feel spec as a guess, for Kristian to tune:

| Parameter | Unit | Start value | Why |
|---|---|---|---|
| Basic attack range | m | 6 | The feel-spec camera shows about 5 m above the character, 4 m below and 8 m to each side |

Placeholders that are not feel values (in `Placeholders.cs`, replaced in G3): basic shot damage 40, skill range 12 m, skill half-width 0.3 m, skill damage 100, dummy health 1000 with refill after 2 s, cursor pick slack 0.5 m.
The placeholder skill uses the basic shot's startup, active and recovery.

## Rules for how you work

1. Kept code lives in `Assets/Game/`. Work on branch `g2/control`, based on `main`. The probe code is reference only; nothing is merged from `probe/g1`.
2. Never hand-edit `.unity`, `.prefab`, `.asset`, `.meta` or `Packages/manifest.json`. The scene, the materials and the settings asset come from `AgentScripts/G2SceneBuilder.cs`, run through the CLI.
   The builder never overwrites an existing `FeelSettings.asset`, because it holds Kristian's tuning.
3. Rules (timings, buffer, hit maths, target choice) are plain C# in `Roguelike.Rules`, with no engine reference, and have EditMode tests.
4. Tests and live checks drive the character through the intent API (`MoveTo`, `AttackMoveTo`, `AttackTarget`, `DashToward`, `CastAt`). No simulated OS input.
   The input driver reads one `InputFrame` per frame, so tests can feed it frames.
5. Tests read values from the settings asset, so they still hold after Kristian tunes.
6. The loop, from the probe:
   - After `unity recompile`, read `console status` for `compilationFailed`. Exit 6 alone does not say why.
   - Wait with `unity status --until-ready` after a recompile or a Play mode change.
   - In the open Editor a failing test exits 6. Read the results JSON.
   - Before Play mode, turn on `editor set_auto_tick` and `editor.playmode set_run_in_background`, and prove frames advance. Turn both off after.
   - Do not close the Editor. This shell is elevated, and `unity open` then stops at a dialog only Kristian can answer.
7. A result of "no verdict" is never a pass.
8. Do not ask Kristian to paste errors or take screenshots.

## Pass criteria (the agent checks these)

| # | What | Pass when |
|---|---|---|
| C1 | Compile and rules | The project compiles, and the EditMode rule tests pass, including one that checks the code's start values against the feel-spec table |
| C2 | Move | A PlayMode test: a move order arrives at the point in distance / run speed, within a tolerance |
| C3 | Dash | Covers the dash distance, then is refused while on cooldown |
| C4 | Attack-move | In range: shoots the nearest target, repeatedly, without walking. Out of range: walks until in range, then shoots |
| C5 | Attack-target | Shoots only the chosen target |
| C6 | Commitment | A move order given during a shot does not move the character until recovery ends, and then it moves |
| C7 | Dash-cancel | A dash during a cast starts at once, and the cast deals no damage |
| C8 | Input buffer | A skill pressed late in a dash fires after it; one pressed earlier than the buffer time is dropped |
| C9 | Hit-stop | Time is frozen at the hit and runs again after the hit-stop time |
| C10 | Input driver | Fed frames: held right-click moves toward the cursor; A shows the range ring; left-click orders attack-move or attack-target; right-click cancels the aim; Space and Q reach the character |
| C11 | Live and seen | In Play mode with the Editor unfocused: frames advance, an attack-move order lowers a dummy's health, and a capture shows the character, a dummy and the range ring |
| C12 | Scene by code | Two builder runs give the same hierarchy, and the settings asset is unchanged by the second run |

## What Kristian judges (the agent cannot)

Play `ControlRoom` and change values in `FeelSettings.asset` while playing. Double or halve; do not nudge.

- Does a right-click move feel instant and exact? (run speed, time to full speed, turn time)
- Is the dash a scarce, deliberate tool? (cooldown, distance, duration)
- Does the shot's lock feel committed without feeling stuck? (startup, active, recovery, input buffer)
- Is attack-move what you expected from League? (basic attack range, decisions 1 to 4 above)
- Is the hit-stop readable or annoying? (hit-stop)
- Does the camera show enough? (pitch, distance, field of view)

## When you are done

1. Write `docs/playtests/g2-control-agent.md` with the criteria table, what could not be verified, and the surprises.
2. Tell Kristian the scene is ready to play, and how to tune.
3. Stop. The feel-spec statuses change from guess to tuned only when Kristian says so.
