# G4 kit: the agent's checks

Run on 2026-11-17 on branch `g4/kit` (based on `g3/skills`, nothing committed). Slice: `docs/slices/g4-kit.md`.
This is the agent's half of G4. Kristian's half is to play `ControlRoom`, fight the enemy, and say what stays.

Unity 6000.6.9f1, one open Editor, not in focus during the checks. Model: Claude Fable 5.2.

## Criteria

| # | What | Result | How it was checked |
|---|---|---|---|
| K1 | Compile and rules | **pass** | No compile errors. 82 of 82 EditMode rule tests: 62 from before and 20 new ones for the burst, the startup progress, the focus gauge and the enemy's decisions |
| K2 | G2 and G3 still hold | **pass** | All 52 earlier PlayMode tests. Two lines changed: Heavy Volley is now the sixth skill, and there are eight skills |
| K3 | Scatter Blast | **pass** | Takes 220 off Dummy_A at 2.3 m and nothing off Dummy_B at 5.8 m |
| K4 | Backstep | **pass** | The character ends 5.0 m (within 0.3) from where it stood, straight away from the aim, and Dummy_A loses 80 |
| K5 | Triple Tap | **pass** | Three hits, 210 damage, the last 0.24 s (within 0.06) after the first |
| K6 | Focus | **pass** | A 100-damage hit gives 5 focus; Z is refused below full. Empowered: Quick Shot deals 150 and a basic shot 60, the gauge stays at 0, a second Z is refused. After 8.0 s (within 0.1) damage is back to normal and focus is gained again |
| K7 | Enemy at rest | **pass** | Idle at home for 1 s with the player 18 m away. Hurt, it is in Chase after 1 s and at least 2.1 m nearer. With the player moved beyond 22 m it goes Idle and is home within 8 s |
| K8 | Slam | **pass** | Player 2 m away: the telegraph shows, nothing lands before the strike, then 20 health is gone. Player moved 5.5 m away during the windup: it strikes, 0 landed. Dash along the circle 0.05 s before the strike: 1 hit avoided, health full |
| K9 | Shockwave | **pass** | Player 7 m away and still: 30 health gone. Player moved 3.2 m to the side after the windup began: it strikes, 0 landed |
| K10 | Life and death | **pass** | A basic shot takes 40 off the enemy, whose 3000 health comes from the settings. Killed, it is hidden, and after 4.5 s it is alive, full, at home and Idle. The player at 0 health: dead, order dropped, cannot be hit again; after 2.3 s alive, full and at the start. A dead character with a held right-click fed to it does not move |
| K11 | Input driver | **pass, fed frames only** | Z on an empty gauge does nothing; Z on a full one starts the empowered mode. Each of the eight key bits starts its own skill |
| K12 | Live and seen | **pass, in part** | Play mode, Editor unfocused. The enemy wound up a Shockwave at 7 m with its telegraph on, and landed three in a row on a player who stood still: 100 → 10 health. The player then died and stood at the start again with 100. Captures show the red strip, the health bar, the focus bar and the eight skill boxes. The Slam was not caught in a capture |
| K13 | The tests can fail | **pass** | See below |

PlayMode suite: 71 of 71 (52 from before, 19 new). The whole loop takes about two minutes in the open Editor.

## Can the tests fail?

All 19 new PlayMode tests passed on their first run, so I planted five bugs at once:

| Planted bug | Caught by |
|---|---|
| Backstep does not move the character | `Backstep_MovesTheCharacterBack_AndShootsWhereItWasAimed` (moved 0.0 m, not 5.0) |
| A burst hits only once | `TripleTap_HitsThreeTimesInABurst` and two rule tests |
| The empowered mode does not raise damage | `Empowered_SkillsAndShotsDealMore_AndNoFocusIsGained` (100, not 150) |
| The enemy's attack follows the player during the windup | `Enemy_Shockwave_IsAimedOnce_SoAStepToTheSideAvoidsIt` |
| The dash's i-frame does not protect | `Enemy_Slam_IsAvoidedByTheDashsIFrame` |

Seven tests failed in all. The bugs were removed again; the suites are green on the restored code.

## Kristian acted: 0

He answered one question: what G4 must hold (the enemy that fights back; no awakening, no tripods).

## Surprises

- **The real mouse and keyboard steered four tests.** After a change to the on-screen text only, four old tests failed: a character that should stand was running, two dashes were short, a move order appeared from nowhere.
  Kristian was playing another game on the same computer. In the first frame after the scene loads, before a test could switch the input driver to its own frames, the driver read his real clicks and key presses.
  Tests now shut the real devices out before they load the scene (`PlayerInputDriver.ReadRealDevices`), and the suites pass while he plays. This is also what was behind the stale cursor in G3.
  It means earlier runs could have been steered the same way without failing. I have no sign that any was.
- **The enemy never has to walk to a player who walks up to it.** Its Shockwave reaches 11 m and it wakes at 9 m, so the moment it wakes it has an attack that fits. It walks only when it is hurt from farther away.
- **Standing still in front of it is fatal in about nine seconds**: a Shockwave every 2.3 s for 30 of 100 health. That is the live run above.
- **The camera again.** With the player 7 m from the enemy, the enemy is at the edge of the screen, and its 12 m strip runs off it.
- The debug readout had grown to a third of the screen with seven health lines. It is now smaller. The text on the focus bar was white on yellow and is now dark when the bar is bright.

## What I could not verify

- **The real keys.** S, F, G and Z have never run on a real keyboard under my control. The same holds for the right mouse button on the enemy.
- **Whether any of it feels right**: whether eight skills are an instrument, whether the gauge matters, whether the telegraphs can be read at play speed.
- **The Slam as a picture**, and the strike effects of both attacks. The tests cover what they do, not how they look.
- **The last two UI changes as a picture** (the smaller readout, the dark text on the focus bar). They were made after the captures.
- **A fight from start to finish.** No test and no live run kills the enemy with skills; the kill in the tests is one large hit.
- **Agent against agent.** The player and the enemy both walk on the NavMesh. I have not looked at how they push each other when they meet.
- **A headless run.** Not run, for the same reason as before.

## After G4: the camera and a new figure (2026-11-17)

Kristian played G4: "fun", "the boss was fine". Two things followed.

- **Camera.** He found it too close. No published numbers for the Lost Ark camera exist; one rough measurement from a real screenshot put its character at about 10% of the screen height, against 21% in ours.
  The camera distance went from 14 m to 28 m. Kristian: "much better". The pitch and the field of view are untouched.
- **A new player figure.** Kristian no longer wanted the realistic gunslinger and made a drawn-style figure in Meshy (the Outrider) with a Desert Eagle, to be held one in each hand, and a shotgun.
  - Through his Meshy account: the figure remeshed to 12 447 triangles and rigged, the pistol to 3 964 and the shotgun to 3 453 (the raw weapons were 470 000 and 224 000). Two clips bought: Idle 1 and Walk Forward While Shooting. 26 credits.
  - The library has no clip that holds a pistol in each hand, and Walk Forward While Shooting turned out to be a long-gun stance (the hands 30 cm apart). So the Shoot pose is finished in code: `CharacterAnimation` straightens both arms toward the point the character faces and turns both pistols to point there. The bought clip is kept for the shotgun.
  - The pistols hang on the hand bones and the shotgun on the spine. There is no dash clip; the dash shows the run clip at speed.
  - One new PlayMode test: while shooting, both pistols point at the target within 14 degrees and both hands are held at least 0.35 m out in front; the arms go down again when the shooting stops.
    The test for the old model's firing moment is inconclusive on this model, which has no such clip. 82 rule tests, 71 PlayMode tests pass.
  - Seen in close-ups taken in Play mode: standing, shooting at a dummy, running.
  - Not checked: how the hands sit on the grips up close, the shotgun against the coat when she runs, and whether she reads from the game camera at 28 m, where she is small and dark against the floor.
  - The old gunslinger and the replaced files went to the Windows recycle bin. Kristian's reference images are in `docs/art/references/`, his original zips in `E:/repos/A-Roguelike-Game/meshy-downloads/`, outside the repository.

## Decisions waiting for Kristian

The ten behaviours under "How the pieces behave" in the slice file are my choices, and so are the three new skills, the gauge's numbers and the enemy's. The ones most likely to matter in the hand:

1. Focus comes from damage dealt, so the big skills fill it fastest.
2. Z takes no time and can be pressed in the middle of a skill.
3. Backstep is a second way out, which makes the dash less scarce (pillar 1).
4. The enemy's attack is aimed once and then committed, so every hit taken is a mistake of the player's (pillar 4).
5. Death in this room is a reset after 2 s.

## State for the play session

- The Editor is open on `Assets/Game/Scenes/ControlRoom.unity`, Play mode stopped, no compile errors.
- Controls: right-click moves, or attacks what is under the cursor. A then left-click is attack-move. Space dashes. Skills: Q Quick Shot, W Powder Keg, E Fan Fire (hold), R Long Shot (charge), S Scatter Blast, D Heavy Volley (cast), F Backstep, G Triple Tap. Z spends a full focus gauge.
- The enemy is the tall purple shape far to the right and down from the start, at (15, -10). It stands still until you come within 9 m or hurt it. Its red outlines show where it will hit.
- To tune: `Assets/Game/Settings/Skills.asset` (skills and the gauge) and `Assets/Game/Settings/Enemy.asset` (the enemy), in the Inspector while playing. Both have a reset in their context menu.
- Nothing is committed on `g4/kit`.
