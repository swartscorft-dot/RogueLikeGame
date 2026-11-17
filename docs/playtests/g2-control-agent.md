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
| C4 | Attack-move | **pass** | In range: two shots on the nearest dummy, none on the farther, no walking (tested from both sides). Out of range: walks, stops within 0.6 m of the range, then shoots |
| C5 | Attack-target | **pass** | Shoots the chosen dummy and leaves the nearer one alone. The order ends when the target dies |
| C6 | Commitment | **pass** | A move order during a shot moves nothing until recovery ends. The shot lands, then the move runs and no second shot is fired |
| C7 | Dash-cancel | **pass** | A dash during startup starts at once. The cast never fires and the dummy keeps its health |
| C8 | Input buffer | **pass** | A skill pressed 0.075 s before a dash ends fires after it. One pressed 0.2 s before is dropped |
| C9 | Hit-stop | **pass** | Time scale is 0 on the frame of the hit and 1 again after the hit-stop time |
| C10 | Input driver | **pass, fed frames only** | Six tests feed `InputFrame`s: held right-click, A and the ring, ground click, click on a dummy, right-click and Esc cancel, Space and Q |
| C11 | Live and seen | **pass** | Play mode, Editor unfocused (focus log: the terminal held focus for all 170 samples). Frames 751 → 777. A showed the ring; a click on Dummy_C sent the character into range and Dummy_C went 1000 → 160. A capture shows the character, two dummies and the ring |
| C12 | Scene by code | **pass** | Two builder runs: same hierarchy, same file set, no orphaned `.meta`. The settings asset has the same hash before and after the second run |

PlayMode suite: 20 of 20. The whole loop (recompile, both suites) takes about 50 s in the open Editor.

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

I also added one row to `docs/vision/feel-spec.md`: basic attack range, 6 m, status guess.

## State for the play session

- The Editor is open on `Assets/Game/Scenes/ControlRoom.unity`, Play mode stopped, no compile errors.
- Controls: hold right-click to move. A, then left-click on the ground (attack-move) or on a dummy (attack it). Right-click or Esc cancels the aim. Space dashes toward the cursor. Q casts the placeholder skill at the cursor.
- To tune: select `Assets/Game/Settings/FeelSettings.asset` and change values in the Inspector while playing. They apply at once and stay after Play mode. The asset's context menu has "Reset to feel-spec start values".
- Three dummies with 1000 health. A basic shot does 40, the skill 100. A dummy refills two seconds after it reaches zero.
- Nothing is committed. The new files are `Assets/Game/`, `AgentScripts/G2SceneBuilder.cs` and the docs; `ProjectSettings/EditorBuildSettings.asset` now lists the scene.
