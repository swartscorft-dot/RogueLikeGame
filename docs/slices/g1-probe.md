# G1 probe: can the agent verify its own work in Unity?

This probe tests the agent's loop, not the game. The code is throwaway.
Work on a branch named `probe/g1`. Nothing from this branch is merged; only the results file is kept.

Read first: `docs/vision/pillars.md` and `docs/vision/feel-spec.md`.

## Scope (build exactly this, nothing more)

One scene with:
- a flat grey-box floor with a NavMesh
- a character that moves with right-click (held click follows the cursor), using the start values in the feel spec
- a dash on Space with the cooldown, distance and duration from the feel spec
- one skill shot on Q, aimed at the cursor, with startup, active and recovery from the feel spec
- a training dummy with health that the skill shot damages

Out of scope: attack-move, UI beyond a debug readout, art, audio, more skills, enemies that act.

## Rules for how you work

1. Keep one Editor open on this project and drive it through the `unity` CLI.
2. Never hand-edit `.unity`, `.prefab`, `.asset`, `.meta` or `Packages/manifest.json`. Build the scene from a builder script kept outside `Assets/` and run it through the CLI.
3. Put the rules (dash cooldown, damage, skill-shot hit maths) in plain C# with its own assembly definition and no scene dependency.
4. Drive the character in tests through a small intent API (move to point, dash toward point, cast at point). Do not simulate OS input.
5. A result of "no verdict" is never a pass. Treat exit 12, 14, 17, 18 and Safe Mode as NO VERDICT, report it, and stop.
6. Do not ask Kristian to paste errors or take screenshots. If you need him, count it as an intervention and say why.

## Pass criteria

Must pass without help from Kristian:

| # | Loop element | Pass when |
|---|---|---|
| P2 | Compile gate | A deliberately planted compile error gives `unity recompile` exit 6 with file and line; after the fix it gives 0 |
| P4 | EditMode rule tests | The plain C# rules pass; a deliberately planted bug gives exit 8, not 6 |
| P6 | Headless suite | With no Editor open, `unity test` returns a verdict (0 or 8) for EditMode and PlayMode from a cold start |
| P7 | Live Play mode, unfocused | With the Editor not in focus, frames advance, and you read position before and after a move order and dummy health before and after a hit |

Also try, and report separately (these may pass with a workaround):

| # | Loop element | Pass when |
|---|---|---|
| P3 | Safe Mode recovery | With the Editor closed, commit a compile error, start the Editor, diagnose Safe Mode from `unity status`, fix it and get a ready Editor |
| P5 | PlayMode tests | A test moves the character to a point and asserts arrival; another asserts the dummy loses the expected health from one skill shot |
| P7b | Locked screen | Repeat P7 with the Windows session locked |
| P8 | Seeing | A Play mode capture is not a single colour, and you can name where the character and the dummy are in it |
| P9 | Scene by code | Running the builder script twice gives the same hierarchy; `git diff` shows no hand-edited YAML and no orphaned `.meta` files |
| P10 | Dialogs and hangs | When a modal dialog or stall happens, you report it instead of looping |

## What to record

For every criterion: pass or fail, wall-clock time, and the number of times Kristian had to act.
Also record the median seconds from an edit to a verified result across P2, P4 and P7.

## When you are done

1. Write `docs/decisions/0001-engine.md` with the results table, the interventions, what surprised you, and what you could not verify. Do not write a recommendation as if it were a decision.
2. Tell Kristian the scene is ready to play. He plays for ten minutes and decides.
3. Stop. Do not start anything else.

## Stop rule

Unity fails the probe if P2, P4, P6 or P7 fails, or if P7 only works with the Editor window in focus.
If it fails, the same scope is built in Godot 4.7 with C# and Kristian chooses between them.

## Optional variant

To compare models, run this file once with Opus 5.5 and once with Fable 5.2, each in its own git worktree with its own Editor. Kristian plays both and picks.
