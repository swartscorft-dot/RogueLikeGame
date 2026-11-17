# G3 skills: the agent's checks

Run on 2026-11-17 on branch `g3/skills` (based on `g2/control`, nothing committed). Slice: `docs/slices/g3-skills.md`.
This is the agent's half of G3. Kristian's half is to play `ControlRoom` and say which skills and rules stay.

Unity 6000.6.9f1, one open Editor, not in focus during the checks. Model: Claude Fable 5.2.

## Criteria

| # | What | Result | How it was checked |
|---|---|---|---|
| S1 | Compile and rules | **pass** | No compile errors. 61 of 61 EditMode rule tests: the 34 from G2 and 27 new ones for skill timing, areas, damage and cooldown |
| S2 | G2 still holds | **pass** | All 28 G2 PlayMode tests. Two were changed to the new skill calls, and the dash-cancel test now also checks that the cancelled skill is on cooldown |
| S3 | Normal | **pass** | Quick Shot takes 100 off the dummy on its line and nothing off the other, is refused at once after, and starts again when the cooldown is over |
| S4 | Point | **pass** | Powder Keg on the group takes 180 off each of the three and nothing off the two outside. Aimed at the group from 13.5 m, it goes off and touches none of them |
| S5 | Holding | **pass** | Fan Fire held through three and a half ticks hits 4 times and not again after the release. Never released, it hits 14 times and ends. With the cursor moved, the second dummy starts taking hits and the first stops |
| S6 | Charge | **pass** | Long Shot released at 0.3 s deals between 143 and 184. Held past 1 s it deals 300. Never released, it deals 300 at 2.6 s |
| S7 | Casting | **pass** | Heavy Volley: nothing at half the cast, with the bar half full; 400 when the cast is over. A dash at half the cast: no damage, and the skill is refused after |
| S8 | Buffer | **pass** | Heavy Volley pressed 0.075 s before Quick Shot's lock ends is the skill that starts. Long Shot pressed and released while it waits deals 90, its damage at no charge |
| S9 | Input driver | **pass, fed frames only** | Each of the five key bits starts its own skill. A held Fan Fire key keeps it going, the aim follows the fed cursor to within 0.2 m, and dropping the bit ends the hold. The aim outline is on during the hold and off after |
| S10 | Live and seen | **pass** | Play mode, Editor unfocused. Powder Keg: three dummies 1000 → 820. Fan Fire: Dummy_A 1000 → 790. Long Shot: 790 → 490. Heavy Volley: 490 → 90. Captures show the circle, the cone, the strip, the charge bar and the cast bar |
| S11 | The tests can fail | **pass** | See below |

PlayMode suite: 44 of 44 (28 from G2, 16 new). The whole loop takes about 80 s in the open Editor.

## Change after Kristian played G3: the basic shot no longer locks after it has gone off

Kristian played G3 on 2026-11-17 and said: the auto-attack locks the character too long after the shot has gone off; it must feel responsive.
He also said the animation does not matter much for now, since it was not in scope to begin with.

- **The rule now.** The basic shot locks for its startup only (0.15 s). Once the shot has gone off, a move or attack order, a skill or a dash takes over at once.
  An order or skill press made during the startup runs right after the shot, without waiting for the recovery.
- **The fire rate is unchanged.** The next basic shot cannot start before the whole shot time (0.45 s) has passed since the last one started.
  Standing still and shooting looks as before. Stepping between shots is now possible and gains no shots.
- **Skills are unchanged.** They keep their recovery lock.
- This replaces decision 2 of the G2 slice ("the shot finishes its recovery first"). The G2 test for it is replaced by five new PlayMode tests, and there is one new rule test:
  - A move order in the startup does not move the character until the shot lands, and then moves it within 0.15 s.
  - A move order in the recovery moves it within 0.15 s.
  - Stepping away and ordering the attack again at once: the second shot lands no sooner than 0.42 s after the first.
  - A skill pressed in the recovery starts at once. A skill pressed halfway through the startup starts right after the shot.
- **The tests can fail.** With the recovery never cut short, three of them failed (the character left 0.35 s after the shot, not within 0.15 s).
  With no wait between shots, the fire-rate test failed (second shot after 0.15 s). The two bugs had to be planted one at a time: together, the first hid the second.
- After the change: 62 of 62 rule tests and 48 of 48 PlayMode tests. The counts in the criteria table above are from before it.
- One new test failed on its first run because of the test: it pressed the skill at the very start of the startup, 0.15 s before the shot, which is the edge of the input buffer. It now presses halfway through.
- Not played by me, so whether 0.15 s of lock feels responsive is for Kristian to say.

### Then: the basic attack as in League

Kristian played that and said: "I want it as in League. One must still be able to right-click on enemies to attack."

- **The basic shot never locks.** Before it has gone off, a move order, an attack on another target, a skill or a dash cancels it. The cancelled shot does not land and does not make the next one wait.
  An attack order that names the same target, or no target, lets the shot go off.
- **A right-click on a dummy attacks it**; on the ground it moves as before. While the button is held, the order follows what is under the cursor.
  Until now the only way to attack was A and a left-click, so "still" in Kristian's sentence describes League, not what G2 had.
- The two tests for the startup lock are replaced, and five are added: a move order in the startup cancels the shot; a cancelled shot does not delay the next; the same attack ordered again every frame does not restart the shot; an attack on another target in the startup switches at once; a held right-click on a dummy shoots it at the normal rate without walking.
- **The tests can fail.** Four bugs, planted in two rounds, were all caught: a right-click on a dummy is a move; no order cancels a shot in its startup; ordering the same attack again cancels the shot; a cancelled shot still makes the next one wait (0.60 s where under 0.25 s was expected).
- After the change: 62 of 62 rule tests and 52 of 52 PlayMode tests.
- The real right mouse button on a dummy has not run under my control; the tests feed the frames.

### What Kristian said about judging the skills

"It is impossible to decide which skills stay without the whole character experience in." So the stop at the end of the G3 slice ("G4 does not start before Kristian has said which skills and rules stay") no longer holds.
The five skills stay as they are for now, and are judged together with the rest of the kit.

## Can the tests fail?

Three of the 16 new PlayMode tests failed on their first run, for a real reason (see the first surprise). The rest passed, so I planted five bugs at once:

| Planted bug | Caught by |
|---|---|
| The cooldown never starts | `Normal_HitsOnce_ThenWaitsForItsCooldown`, `Casting_ADashCancelsIt_AndTheSkillIsSpent`, `Dash_CancelsACastBeforeItHits` |
| A Point skill lands however far away it is aimed | `Point_LandsNoFartherAwayThanItsRange` (540 damage where 0 was expected) and a rule test |
| Letting go of the key does nothing | `Holding_HitsWhileTheKeyIsDown_AndStopsAtTheRelease` (14 hits, not 4), `Input_ASkillKeyHeld_ThenLetGo_ReachesTheCharacter` |
| A Charge skill always deals full damage | `Charge_ReleasedEarly_HitsWeakly`, `Buffer_AChargeSkillReleasedWhileItWaits_FiresWithNoCharge` and a rule test |
| A buffered skill press starts the first skill, whichever key it was | `Buffer_ASkillPressedLateInAnothersRecovery_FiresWhenItEnds` |

Eleven tests failed in all. The bugs were removed again; the suites are green on the restored code.

## Kristian acted: 0

He answered one question: whether control is good enough to build on, and whether to build G3 without approving the scope.

## Surprises

- **A stale cursor steered the charged shot.** The three Charge tests hit nothing. In the first frames after the scene loads, the input driver still reads the real mouse, and a held or charged skill then followed that old cursor point instead of its aim.
  The rule is now: a skill follows only a cursor that is reported after it started. In play the driver reports the cursor every frame, so this changes nothing there.
- **I recompiled while the Editor was in Play mode, and broke the running game.** The console filled with `NullReferenceException` from `DebugReadout`, because a recompile during Play mode drops what the scripts set up at start.
  I do not know who started Play mode; it was not one of my commands. I asked Kristian to stop it and waited nine minutes. Windows then showed no keyboard or mouse input for 22 minutes, so I stopped Play mode myself.
  The slice file now says: check for Play mode before a recompile or a scene build.
- **The camera shows less than the skills reach.** It shows about 5 m above the character. Powder Keg at its 8 m range lands at the top edge of the screen, and the far ends of the 12 to 16 m lines are off it.
  This is the G2 finding again, and larger. Camera distance and field of view are in `FeelSettings.asset`; the ranges are in `Skills.asset`.
- **The on-screen text was unreadable at 2560 x 1440.** It was drawn in fixed pixels. The readout and the skill bar are now laid out for a view 720 pixels high and scaled to the real height.
- **The body turns side-on during the Heavy Volley cast.** The shooting clip is entered 1 s before its firing moment, which is the part where the hand goes back to the holster.

## What I could not verify

- **The real keys.** Q is known to work from G2. W, E, R and D, and holding and letting go of a real key, have never run under my control: the lines that read them are new.
- **Whether any of it feels right**, and whether five input types are five different things in the hand.
- **The skill bar's look.** In the one capture of the whole view, the text and the bars are there, but the boxes behind the text are not drawn. I do not know whether that is the capture or the game.
- **The effects as pictures**, beyond one capture each of the Powder Keg disc and the aim outlines. Fan Fire's five beams and the Long Shot beam were not caught in a capture.
- **The shooting clip going back to its firing pose** during a long hold. It is built, but no test covers it.
- **A headless run.** Not run, for the same reason as in G2.
- Paths the start values never reach: a Holding skill with no tick interval, a Charge skill with no charge time.

## Decisions waiting for Kristian

The ten behaviours under "How the pieces behave" in the slice file are my choices, and so are the five skills and their numbers. The ones most likely to matter in the hand:

1. A held or charged skill turns freely with the cursor.
2. A cancelled skill is spent.
3. Hit-stop on every Fan Fire hit: up to 14 freezes of 0.05 s in one use.
4. Casting is only a long startup with a bar.
5. The fifth skill is on D.

## State for the play session

- The Editor is open on `Assets/Game/Scenes/ControlRoom.unity`, Play mode stopped, no compile errors.
- Controls as in G2, plus the skill keys: Q Quick Shot, W Powder Keg (lands under the cursor), E Fan Fire (hold), R Long Shot (hold to charge, let go to fire), D Heavy Volley (1 s cast).
- To tune: select `Assets/Game/Settings/Skills.asset` and change values in the Inspector while playing. They apply to the next use and stay after Play mode. The asset's context menu has "Reset to the G3 start values".
- Six dummies with 1000 health: A, B and C as before, and D, E and F in a tight group up to the left, around (-10.5, 6.7).
- Nothing is committed on `g3/skills`. New: the skill rules and tests, `SkillSettings`, `SkillIndicator`, `SkillBar`, `Skills.asset`. The scene builder is renamed to `AgentScripts/ControlRoomBuilder.cs`.
