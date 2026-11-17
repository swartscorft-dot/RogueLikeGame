# 0001 Engine: G1 probe results (Unity)

Status: **decided. The engine is Unity.** Kristian, 2026-11-17.

## Decision

Kristian played the probe scene on 2026-11-17, said "it works", and chose Unity.
The Godot 4.7 comparison build was not made.
His play session was the first run of the real mouse and keyboard input (right-click, Space, Q), which the probe itself could not test.

The rest of this file is the probe's results as they stood before the decision.

- Probe: `docs/slices/g1-probe.md`, run 2026-11-16 and 2026-11-17 on branch `probe/g1` (head `85b361f`).
- Unity 6000.6.9f1, Unity CLI 1.0.1, `com.unity.pipeline` 1.0.0, Windows 11.
- Model: Claude Fable 5.2. The optional Opus 5.5 run was not done.
- `docs/slices/g1-probe-progress.md` holds the working notes from day one. This file replaces it.

## Results

| # | Loop element | Result | Time | Kristian acted |
|---|---|---|---|---|
| P2 | Compile gate | **pass** | 2 s to exit 6, 4 s to exit 0 after the fix | 0 |
| P4 | EditMode rule tests | **pass headless, with a caveat in the open Editor** | 9 s headless, 15 s in the open Editor | 0 |
| P6 | Headless suite | **pass** | EditMode 13 s, PlayMode 24 s, both from a cold start | 0 |
| P7 | Live Play mode, unfocused | **pass** | 10 s to enter Play mode and prove frames advance, 20 s more for the move and the hit | 0 |
| P3 | Safe Mode recovery | **not shown as written** | 128 s from `unity open` to a ready Editor with the fix | 2 |
| P5 | PlayMode tests | **pass** | 24 s headless, 3 of 3 | 0 |
| P7b | Locked screen | **pass** | 21 s locked | 1 (approve the lock, unlock after) |
| P8 | Seeing | **pass** | 3 s for two captures | 0 |
| P9 | Scene by code | **pass** | 5 s for two builds | 0 |
| P10 | Dialogs and hangs | **partly** | | 0 |

Details per criterion:

- **P2.** Planted error: exit 6 with `Assets\Probe\Rules\Dash.cs`, line 25, column 58, CS1002. After the fix: exit 0.
- **P4.** The 19 rule tests pass. With the dash cooldown halved, headless `unity test --mode EditMode` gives **exit 8** (`TESTS_FAILED`, 18 of 19, the failing test named in the report).
  In the open Editor, `unity command test run` gives **exit 6** for the same bug, the same code as a compile error. There the verdict has to be read from the results JSON, which names the failed test.
- **P6.** No Editor open. EditMode: exit 0, 19 of 19. PlayMode: exit 0, 3 of 3. A failing EditMode test gives exit 8 (see P4).
- **P7.** Editor not in focus, with `editor set_auto_tick` and `editor.playmode set_run_in_background` on.
  Frame count moved 718 → 744 in 113 ms. Player position (0, 0) → (-4, -3) after `MoveTo`. Dummy health 1000 → 900 → 800 after two `CastAt` calls.
  At one check the Editor window had focus, so I repeated the whole thing with a focus log (foreground window sampled every 200 ms). In 211 samples over 43 s the terminal held focus the whole time: frames 27736 → 27761, position (0, 0) → (2, 1), health 800 → 700.
- **P3.** See "What happened in P3" below. I never saw Safe Mode through `unity status`.
- **P5.** `MoveTo_ArrivesAtPoint`, `SkillShot_DamagesDummyByExpectedAmount` and `Dash_CoversDashDistance_ThenIsOnCooldown` pass.
  To check the tests can fail, I halved the damage in `PlayerCharacter`: the skill-shot test failed with "Expected: 100, But was: 50" and the other two passed. After the revert: 3 of 3.
- **P7b.** Session locked with `LockWorkStation` (the lock screen process was running at the start and the end of the run).
  Frames 143411 → 143436, position (3, 3.5) → (0, 0), health 700 → 600. Both capture sources gave a normal image while locked.
- **P8.** 1280 x 720 captures, `--source camera` and `--source screen`, not one colour.
  With the player at (3, 3.5): the blue capsule is at the centre of the frame and the red cylinder is above it and slightly right. The readout in the image (position, dummy 700/1000, shots 3) matches what `script eval` returned.
- **P9.** Two builder runs gave the same hierarchy dump and the same set of files, with no orphaned `.meta` files. Scene YAML fileIDs differ between runs.
- **P10.** `unity open` reported the "Administrator Privileges Detected" dialog in 4 s (exit 6, `OPEN_BLOCKED_BY_DIALOG`), and I recorded it instead of retrying.
  I never saw the "Enter Safe Mode?" prompt: Kristian had answered it before my next status check.
  On day one, the first `unity recompile` hung for its full 600 s timeout once before I reported it.

## Stop rule

P2, P6 and P7 pass. P7 works with the Editor unfocused and with the screen locked.
P4 gives exit 8 headless and exit 6 in the open Editor. Whether that counts as a pass for P4 is Kristian's call.

## Edit to verified result

Samples in seconds: P2: 2, 4, 2, 5. P4: 9, 15. P7: 14, 30.
**Median: 7 s.**

The P7 samples start from an Editor that is already open. The 30 s sample includes 10 s to enter Play mode.
A cold headless run costs 13 s (EditMode) or 24 s (PlayMode) on top.

## Interventions: 4

1. Day one: the `unity` CLI was not installed. Kristian approved installing it from `https://unity.com/install.ps1` (22 s).
2. P3: Kristian clicked "Restart as Standard User" in the "Administrator Privileges Detected" dialog.
3. P3: Kristian answered the "Enter Safe Mode?" prompt.
4. P7b: Kristian approved locking the screen and unlocked it afterwards.

I also asked Kristian one question after the fact: whether he had clicked the two dialogs in P3. The logs could not tell me.

## What happened in P3

1. Editor closed. I committed a compile error (`9581d48`, a missing semicolon in `Dash.cs`) and ran `unity open --until-ready`.
2. It returned after 4 s with exit 6 and named the dialog "Administrator Privileges Detected", with the buttons "Restart as Standard User" and "Ignore Warning".
   The cause is that this Claude Code session runs in an elevated shell, so the Editor started elevated.
3. I could not answer the dialog from the CLI. `unity command editor dialog` needs an Editor that reports its dialogs, and this one did not answer (`PIPELINE_UNREACHABLE`).
4. Kristian clicked both dialogs within about 25 s. The Editor restarted as a standard user, and its log says "Exiting safe mode".
5. `unity status` showed `STATUS_PIPELINE_LOAD_PENDING` and then `ready`. It never showed `STATUS_SAFE_MODE`.
6. From there the recovery worked without help: `console status` reported `compilationFailed: true`, `console get --level error` gave `Dash.cs(32,29): error CS1002`, and `unity recompile` gave exit 6.
   I reverted the error in a new commit (`85b361f`), and `unity recompile` gave exit 0 with the Editor `ready`.

So the part that passed is "diagnose a compile error in an Editor that opened with one, fix it and get a ready Editor".
The part that did not is "start the Editor unattended" and "diagnose Safe Mode from `unity status`".

## Surprises

Day one:

- An Editor was already open and its package state did not match `manifest.json` (`com.unity.ai.navigation` missing). `unity command package resolve` fixed it.
- Exit 6 means a compile error, a timeout or a busy Editor. P2's gate needs `errors[0].code` or the diagnostics, not the exit code alone.
- In the open Editor, a failing test exits 6, not 8.
- `script run` output and dump formatting follow the Norwegian locale (decimal commas) unless the code forces invariant formatting. The debug readout in the scene shows decimal commas too.
- A float boundary bug in my own test was caught by the first EditMode run.

Day two:

- `unity open` from an elevated shell stops at a dialog that the CLI cannot answer on this Editor version (see P3).
- When the Editor is already in a failed-compile state, `unity recompile` exits 6 with `COMMAND_FAILED` and **no file or line**. The file and line come from `console get`.
- The first `unity recompile` after the fix returned exit 0 but its data still said `"compilationFailed": true`. `console status` said `false` a few seconds later. I trust `console status` after a recompile, not that field.
- A command sent during a domain reload fails with `PIPELINE_UNREACHABLE`, and the reload logged two Pipeline errors (`ObjectDisposedException` serving `/api/exec`). `unity status --until-ready` before the next command avoids it.
- The first headless run with the planted bug ended at my shell with exit 255 and no output, although its report file was written with 18 of 19. The rerun gave exit 8. I did not find the cause and could not reproduce it.
- Screen captures worked with the session locked. The CLI documentation warns that they can come back as one colour.
- The skill shot reaches further than its 12 m range: the hit test adds the shot's half-width (0.3 m) and the dummy's radius (0.5 m), so a shot from 12.04 m hit. I had predicted a miss.
- With the feel-spec camera (pitch 55, distance 14, field of view 30), a dummy 7.2 m away is only a sliver at the top edge of the frame. The 12 m skill range is longer than what the screen shows upward.

## What I could not verify

- **Real mouse and keyboard input.** Tests and P7 drive the character through `MoveTo`, `DashToward` and `CastAt`, as the probe rules require. `PlayerInputDriver` (right-click, Space, Q) has never run under my control. Kristian's play session is its first test.
- Whether any of it feels right.
- Safe Mode as reported by `unity status`, and an unattended `unity open` from a shell that is not elevated.
- A dash in live Play mode. The dash is covered by the PlayMode test only.
- Exit 8 from headless `unity test` for a failing **PlayMode** test. I saw exit 8 for EditMode, and the failing PlayMode test in the open Editor only.
- The probe-chosen values that are not in the feel spec: skill-shot range 12 m, half-width 0.3 m, damage 100, dummy health 1000.

## State for the play session

- The Editor is open on `Assets/Probe/Scenes/G1Probe.unity`, Play mode stopped, no compile errors, console clean. It runs as a standard user.
- Controls: hold right-click to move (the character follows the cursor), Space to dash, Q for the skill shot toward the cursor.
- The dummy has 1000 health, loses 100 per hit and refills two seconds after it reaches zero.
- I turned auto-tick and the Run In Background override off again.
- Branch `probe/g1`: the code is the same as before P3 (commit `9581d48` planted the error and `85b361f` reverted it). This file is not committed.
