# G2 control: the agent's checks

Run on 2026-11-17 on branch `g2/control` (based on `main`, nothing committed). Slice: `docs/slices/g2-control.md`.
This is the agent's half of G2. Kristian's half is to play `ControlRoom` and tune the feel spec.

Unity 6000.6.9f1, one open Editor, never in focus during the checks. Model: Claude Fable 5.2.

## Criteria

| # | What | Result | How it was checked |
|---|---|---|---|
| C1 | Compile and rules | **pass** | No compile errors. 30 of 30 EditMode rule tests, one of which checks the code's start values against the feel-spec table |
| C2 | Move | **pass** | Arrives in distance / run speed (within -0.2 s, +0.5 s), and the order ends |
| C3 | Dash | **pass** | Covers 6 m within 0.3 m, then is refused while on cooldown |
| C4 | Attack-move | **pass** | Click near no dummy: two shots on the dummy nearest the character, none on the farther, no walking (tested from both sides). Out of range: walks, stops within 0.6 m of the range, then shoots. Click near a dummy: see "Change after the first run" below |
| C5 | Attack-target | **pass** | Shoots the chosen dummy and leaves the nearer one alone. The order ends when the target dies |
| C6 | Commitment | **pass** | A move order during a shot moves nothing until recovery ends. The shot lands, then the move runs and no second shot is fired |
| C7 | Dash-cancel | **pass** | A dash during startup starts at once. The cast never fires and the dummy keeps its health |
| C8 | Input buffer | **pass** | A skill pressed 0.075 s before a dash ends fires after it. One pressed 0.2 s before is dropped |
| C9 | Hit-stop | **pass** | Time scale is 0 on the frame of the hit and 1 again after the hit-stop time |
| C10 | Input driver | **pass, fed frames only** | Six tests feed `InputFrame`s: held right-click, A and the ring, ground click, click on a dummy, right-click and Esc cancel, Space and Q |
| C11 | Live and seen | **pass** | Play mode, Editor unfocused (focus log: the terminal held focus for all 170 samples). Frames 751 → 777. A showed the ring; a click on Dummy_C sent the character into range and Dummy_C went 1000 → 160. A capture shows the character, two dummies and the ring |
| C12 | Scene by code | **pass** | Two builder runs: same hierarchy, same file set, no orphaned `.meta`. The settings asset has the same hash before and after the second run |

PlayMode suite: 20 of 20 at the first run, 25 of 25 after the change below. The whole loop (recompile, both suites) takes about 50 s in the open Editor.

## Change after the first run: the click picks the attack-move target

Kristian, 2026-11-17, before playing: "nearest the click is what we go for". Until then attack-move shot the dummy nearest the character.

- **The rule now.** A ground click picks the living dummy nearest the click, if its edge is within 2 m of the click (the new feel value "attack-move click radius").
  The character walks into range of that dummy and shoots it, and leaves nearer dummies alone.
  With no dummy near the click, or once the picked one is dead, it shoots the dummy nearest the character, as before.
- **Two details are my choices.** The pick is made once, at the click, not re-made for every shot. A picked dummy that is out of range is walked to, past dummies that are in range.
- **Five new PlayMode tests**, all passing:
  - A click beside the farther dummy shoots that one twice and the nearer one never (from both sides).
  - A click beside Dummy_C, out of range, walks to it and shoots it; Dummy_A, in range from the start, takes no hit.
  - When the picked dummy dies, the next two shots go to the dummy nearest the character and the order stays.
  - Fed frames: A, then a click on the ground beside Dummy_B, orders an attack-move that goes for Dummy_B.
- **The tests can fail.** They passed on their first run, so I planted two bugs in turn: the click picks nothing (5 tests failed), and the pick is made but ignored when shooting (4 tests failed). Both were removed; 30 rule tests and 25 PlayMode tests pass on the real code.
- The debug readout now shows the picked dummy: `order AttackMove -> Dummy_B`.
- The settings asset was saved through the Editor so the new value is in it. No tuned value changed.
- Not checked live in Play mode or in a capture, and not played. There is no marker on screen for which dummy a click will pick.

## Change after the first run: a rigged player model

Art was out of scope for G2. Kristian brought it in on 2026-11-17: a gunslinger made in Meshy, in place of the grey capsule.
Control does not depend on it. The player's root, agent and hit size are unchanged, and without the model files the builder falls back to the capsule.

- **Where it came from.** Generated through Kristian's Meshy account from his own concept image, in T-pose, with Meshy's game-character model (15 807 triangles, 2K colour texture, no normal or metal maps).
  Then auto-rigged (24 bones) and given three library animations. Cost: 15 + 5 + 9 = 29 credits. Run came free with the rig.
- **Files.** `Assets/Game/Art/Player/Gunslinger/`: `Gunslinger_Rigged.fbx`, `Gunslinger_Run.fbx`, `Gunslinger_Actions.fbx` (Idle, Cowboy Quick Draw Shooting, Roll Dodge), the extracted colour texture, and `Gunslinger.controller`, which the builder writes.
- **How it is shown.** `CharacterAnimation` reads the character and cross-fades between four Animator states: Idle, Run, Shoot, Dash. Which one is a rule in `PoseRule` (dash, then shot, then the dash animation's tail, then run or idle).
- **Clip timings**, read off the clips by sampling the hips and hands every 0.1 s and looking at rendered poses:
  - Shoot: the 7.3 s clip is a slow draw, a hold with both hands in front of the chest, and a holster. The draw ends at 2.4 s. The clip is entered one startup before that, so 2.4 s falls on the shot.
  - Dash: the roll clip winds up for 0.6 s and then travels 3.7 m. It is played from 0.6 s to its end over the dash plus 0.25 s, and the hips are held over the character so the model does not leave it.
  - Run: plays at character speed / 6 m/s. The 6 is a guess; the cycle is 0.63 s.
- **Tests:** 4 rule tests for `PoseRule` and 3 PlayMode tests (the four poses follow the character; the Shoot clip is at 2.4 s when the hit lands; the hips stay within 0.3 m of the character through a dash).
  They passed on their first run, so I planted two bugs: hips not held (the model drifted 3.7 m, caught) and the Shoot clip entered at its start (clip at 0.15 s at the hit, caught). Both removed. 34 rule tests and 28 PlayMode tests pass.
- **Seen live**, Editor unfocused: captures of the shooting pose facing Dummy_A and of the roll. Not captured: the run, because the CLI was slower than the walk. Not played.

What does not look right yet:

- **The roll does not fit a 0.2 s dash.** The dash covers 6 m in 0.2 s; the roll then finishes on the spot, at 2.7 times its speed. Either the dash gets longer (a feel value) or it wants a lunge rather than a roll.
- **The idle turns the body** up to 80 degrees to the side while looking around, so a standing character does not always show where it faces.
- **The revolvers stay in their holsters.** They are part of the body mesh, so the shooting pose fans an empty hand.
- **The same clip is used for the basic shot and the skill.**

## Can the tests fail?

All PlayMode tests passed on their first run, so I planted four bugs at once and ran both suites:

| Planted bug | Caught by |
|---|---|
| A move order cancels the shot | `MoveOrderDuringAShot_WaitsForRecovery_ThenMoves` |
| The input buffer never expires | `Buffer_SkillPressedEarlierThanTheBufferTime_IsDropped` |
| No hit-stop | `HitStop_FreezesTimeAtTheHit_ThenReleases` |
| Attack-move takes the first target in range, not the nearest | The rule test at once. The PlayMode test **missed it** at first |

The miss: the test stood next to Dummy_A, and the game happened to list Dummy_B first, so "first" and "nearest" were the same dummy.
The test now runs from both sides, and with the bug planted the Dummy_A side fails. All four bugs were removed again; the suites are green on the restored code.

## Kristian acted: 1

1. Clicked "Don't Save" in a Unity dialog (see the first surprise).

Kristian also answered two questions: what to continue with, and which button he clicked.

## Surprises

- **Changing branch with a scene open blocks the Editor.** The probe scene was open when I switched to `g2/control`, where it does not exist.
  The next test run opened a "Scene(s) Have Been Modified" dialog and sat for its 600 s timeout (exit 14, no verdict). `unity status` said `ready` the whole time.
  The CLI cannot answer dialogs on this Editor version, and my attempt to press the button from a script was refused by the permission system, so Kristian had to click.
  To avoid it: run the scene builder (it opens a new scene) before anything else after a branch change.
- **The camera shows less than the basic-attack range.** With the feel-spec camera the 6 m range ring leaves the screen above the character.
  In the live run the character shot Dummy_C from 6 m while Dummy_C was off-screen. Two knobs: camera distance and field of view, or the range.
- The game lists the dummies in an order that is not the scene order (see the missed bug above).
- Measured fire rate: 19 shots in 8.78 s of game time, 0.46 s per shot against 0.45 s from the timings. In real time 0.51 s per shot, because each hit adds 0.05 s of hit-stop.
- The console errors at 10:10 UTC ("ReloadScene cannot be used with a scene without a SceneAsset: G1Probe", "Test tree is not available") belong to the stalled run. Nothing has been logged as an error since.

## What I could not verify

- **The mouse and keyboard themselves.** `PlayerInputDriver.ReadDevices` (seven lines that read the mouse and keyboard) has never run under my control. A, left-click and Esc are new since the probe.
  Kristian played on 2026-11-17 and said the mouse and keyboard work. He also said the roll after the dash and the idle that turns the body are fine for now.
- **A headless run.** `unity test` with no Editor open was not run for G2. Closing the Editor means an administrator dialog when it reopens, because this shell is elevated.
- **Whether any of it feels right.**
- The dash's i-frame beyond its timing rule. Nothing damages the player in G2.
- Paths the start values never reach: time to full speed and turn time above 0, "dash cancels a skill" off, "face cursor on cast" off.
- A dash into a wall or the floor's edge. The room has no walls.
- The dummy's white flash as a picture. The test checks the flag; I did not catch it in a capture.

## Decisions waiting for Kristian

The six behaviours under "How the pieces behave" in the slice file are my choices. The ones most likely to matter in the hand:

1. A move click during a shot waits for the recovery (up to 0.45 s). Only the dash cancels.
2. A dash or skill pressed more than 0.15 s before a lock ends is dropped, not queued.
3. Attack-move keeps shooting for as long as a target is in range.
4. Hit-stop freezes the whole game, on every hit, including every basic shot.

I added two value rows to `docs/vision/feel-spec.md`, both status guess: basic attack range, 6 m, and attack-move click radius, 2 m.
The row "Attack-move target" records Kristian's decision of 2026-11-17.

## State for the play session

- The Editor is open on `Assets/Game/Scenes/ControlRoom.unity`, Play mode stopped, no compile errors.
- Controls: hold right-click to move. A, then left-click on the ground (attack-move) or on a dummy (attack it). Right-click or Esc cancels the aim. Space dashes toward the cursor. Q casts the placeholder skill at the cursor.
- To tune: select `Assets/Game/Settings/FeelSettings.asset` and change values in the Inspector while playing. They apply at once and stay after Play mode. The asset's context menu has "Reset to feel-spec start values".
- Three dummies with 1000 health. A basic shot does 40, the skill 100. A dummy refills two seconds after it reaches zero.
- Nothing is committed. The new files are `Assets/Game/`, `AgentScripts/G2SceneBuilder.cs` and the docs; `ProjectSettings/EditorBuildSettings.asset` now lists the scene.
