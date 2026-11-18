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

### The grip (2026-11-17, branch `art/grip`)

Kristian asked whether he should try to fix how the right and left hand hold the pistols. Close-ups showed two things: the hands were turned differently on the two sides, so each pistol sat beside its hand in its own way; and the hands are open, because the Meshy rig has no finger bones.

- **In the aim the hands are now turned too**: fingers along the aim, palms facing each other, the same on both sides.
- **Where a pistol sits in a hand is a live setting**, `Assets/Game/Settings/Grip.asset`: metres from the wrist to the middle of the pistol, forward, up and outward. It is applied every frame, so it can be tuned in the Inspector while playing, and the builder never overwrites it.
  Start values 0.17, 0.04 and -0.02, chosen from four tries by looking at close-ups: the hand covers the grip.
- One new PlayMode test: standing, running and shooting, each pistol is where the settings put it, within 1 cm of the set distance from its wrist. With the seating removed it failed (0.200 m, not 0.176).
  The turning of the hands is **not** covered by a test: with it removed, every test still passed. It is checked only in the close-ups.
- What still looks wrong: the fingers cannot close, so the hands lie flat against the grips. Standing, the left pistol hangs by the wrist rather than in the hand, because that hand is posed by the idle clip.
- 82 rule tests and 72 PlayMode tests pass; one is inconclusive as before.

### Kristian's Blender export with a closed hand (2026-11-17, branch `art/grip`)

Kristian edited the rigged figure in Blender, closed the left hand around a pistol, and exported it as `Assets/Game/Art/Player/Outrider/Rigged_lukket.fbx`.

- **It is compatible.** The same 24 bones at the same places (plus the leaf bones Blender adds), the same skin weights except for the reshaped left hand (973 vertices on that bone, 851 before), and the idle and run clips pose it exactly like the Meshy file: the right hand lands on the same point to the centimetre.
- **The builder uses it when it is there**, and falls back to the Meshy file when it is not.
- **The grip comes from the file.** The pistol Kristian left in the hand is a loose mesh, not tied to a bone, so it would not follow the hand. The builder reads where it lies, puts our own pistol there on the hand bone, mirrors it into the right hand, and turns his loose one off. The pistols are now 0.29 m long, as his is, not 0.38 m.
- **In the aim the hand keeps hold of the pistol exactly as placed**: the hand is turned so that the pistol points at the target. This replaced the rule that turned the palms toward each other, which did not know how the hand was shaped.
- The grip settings asset is used only when a model file has no pistol placed in it.
- The file also carried Blender's camera and light. The builder turns off the import of both.
- Seen in close-ups: the left hand wraps the grip standing and shooting. The right hand is still open, so its mirrored pistol lies against an open hand.
- 82 rule tests and 72 PlayMode tests pass, one inconclusive as before. The pistol test now checks that each pistol stays where it was at the hand through standing, running and shooting, with or without grip settings.
- A first posed render of the file looked unposed. That was my test: it rendered the same skinned mesh twice in one frame. A fresh copy posed correctly.

### Shot effects (2026-11-18, branch `art/grip`, uncommitted)

The shots used to be flat beams, then for a few hours flying bullets. Kristian saw the bullets and wanted something "a bit cartoon/anime-like... some muzzle flash and effects are enough. It depends a bit on the skill", with Lost Ark and League as references. Now every shot is shown by flat, bright shapes with hard edges that glow under bloom. They are only a picture: damage, hit rules and timing are as before, the damage lands the moment the shot goes off, and the effects appear in that same frame. Nothing in `Roguelike.Rules` changed. After the first round he asked for bloom (as a trial) and another round on Scatter Blast, Powder Keg and Fan Fire; this describes the result of the second round.

- **The parts** (`ShotFx`, one small mesh each, drawn unlit in its vertex colours: an outer shape in the look's rim colour and an inner, white core):
  - a **flash** at the muzzle: a spiky star whose long spike points along the shot; it pops and shrinks;
  - a **streak** from the muzzle to where the shot went: a tapered bar, pointed and widest at its far end; it appears whole, then pulls into that end while it thins out;
  - a **spark** on every target the shot or skill hurts, drawn just in front of it so the target does not hide it (the white hit flash on the target stays);
  - a **wedge** for Scatter Blast: a jagged, level fan at the height of the gun that fired, starting level with its muzzle and reaching as far as the cone, which blows outward to its edge;
  - for Powder Keg, an **outline**: a thin ring on the ground exactly at the circle's edge (the area hit), a **ring** shockwave that runs out to the edge, and a big **burst** star over the middle, drawn in front of anything standing in it. The old opaque orange disc is gone.
  Flash, streak and spark face the camera. Everything runs on game time, so the hit stop holds it for 0.05 s, and each part removes itself when its time is up.
- **Muzzles and hands** are as built for the bullets: each hit fires from the barrel end of one pistol, right and left in turn; without the figure, from over the character at 0.6 m.
- **Per shot** (start values, all guesses; sizes in metres, times in seconds):

| Shot | Colour | Flash | Streak width / time | Hit spark | Other |
|---|---|---|---|---|---|
| Basic shot | pale yellow | 0.45 / 0.07 | 0.12 / 0.10 | 0.6 | |
| Quick Shot | orange | 1.0 / 0.10 | 0.40 / 0.16 | 1.0 | |
| Powder Keg | red, yellow core | none | none | 0.8 | outline, shockwave and burst, 0.35 s (the outline 0.56 s) |
| Fan Fire | yellow | 0.7 / 0.08 | 0.22 / 0.11 | 0.6 | 4 streaks a tick at random angles in the cone |
| Long Shot | deep blue | 1.1 / 0.12 | 0.45 / 0.40 | 1.3 | streak and flash twice as wide at full charge |
| Scatter Blast | amber | 1.3 / 0.10 | 0.22 / 0.12 | 1.0 | 5 streaks edge to edge, wedge 0.22 s |
| Heavy Volley | red | 2.0 / 0.14 | 0.40 / 0.22, four side by side | 1.3 | |
| Backstep | cyan | 1.0 / 0.10 | 0.36 / 0.16 | 1.0 | |
| Triple Tap | violet | 0.85 / 0.08 | 0.32 / 0.10 | 0.9 | a short streak for each of the three hits |

  While empowered (focus spent), every part is 1.25 times larger and its rim is pink.
- **Bloom.** The camera now has post-processing on, and a global Volume, `PostProcessing`, uses `Assets/Game/Settings/PostProcessing.asset`: Bloom with threshold 1.2, intensity 2, scatter 0.65, high-quality filtering (start values, guesses). Only colours brighter than white pass the threshold, and only the effects are drawn that bright: their white cores 4 times white and their rims 1.8 times their colour (`CoreGlow` and `RimGlow` in `ShotFx.asset`). So the cores glow white and the rims glow in their own colour. Scatter Blast's large flat wedge glows less, or it blooms into a white blob.
  - **The base picture is unchanged.** Captured with no effect on screen, bloom off and on differ by at most 2 of 255 in any pixel. One thing had to be dealt with first: the render pipeline asset (`Assets/SourceFiles/Settings/PC_RPAsset.asset`) carries a volume profile of its own from the URP template, `SampleSceneProfile`, with Neutral tonemapping, a vignette and a weak bloom. It had never shown because the camera had post-processing off. Turned on, it darkened the bright reds by up to 28 of 255. `PostProcessing.asset` therefore also sets tonemapping to None and the vignette to 0. **No pipeline setting was changed:** HDR was already on (32-bit), and post-processing data was already assigned to the renderer.
  - **To turn bloom off:** untick Bloom in `Assets/Game/Settings/PostProcessing.asset`, or set its Intensity to 0. Both are kept after Play mode. Setting `CoreGlow` and `RimGlow` in `ShotFx.asset` to 1 takes the glow out of the effects as well. In the scene, unticking the `PostProcessing` Volume or Post Processing on the Main Camera also works, but changes made to the scene in Play mode are lost when it stops.
- **To tune:** `Assets/Game/Settings/ShotFx.asset` (`ShotFxSettings`), in the Inspector while playing: the basic shot's look, one look per skill (found by the skill's name), the glow, and the empowered scale and colour. A change shows from the next shot and is kept. The bloom is tuned in `PostProcessing.asset`. The builder creates both assets when they are missing and never overwrites them; `ShotFx.asset` has a reset in its context menu. The 0.5 m spacing of streaks across a wide line is a constant in `PlayerCharacter`.
- **Replaced:** `Bullet`, `BulletSettings` and `Bullets.asset` are gone; the builder deleted the old asset (never tuned). The ground disc and its `SkillMaterial` field are gone from `PlayerCharacter`; `Skill.mat` and `BasicShot.mat` are still on disk, unused. The effect material is `Assets/Game/Materials/ShotFx.mat` (URP's unlit particle shader, opaque, both sides), made by the builder. It passes vertex colours above 1 through unchanged, which is what the glow relies on.
- **Tests.** 82 rule tests pass. PlayMode: 82 of 83 pass, one inconclusive as before (`Animation_TheShotGoesOffAtTheClipsFiringMoment`): 72 from before and 10 in `ShotFxTests`.
  - A basic shot shows one flash and one streak from a barrel end (within 3 cm) and one spark on the target, and all of them are gone after their time.
  - Two shots come from different hands; without muzzles a shot starts over the character.
  - The damage lands in the frame the effects appear, for a shot and for a skill.
  - A narrow line shows one flash and one streak over the skill's range along the aim; the wide line shows its streaks side by side across its width.
  - Each hit of Triple Tap has its own streak, from the hands in turn.
  - Scatter Blast's streaks reach both edges of the cone, and its wedge is level with the gun that fired, as far ahead as its muzzle, on the aim line, as wide as the cone and reaching as far as it.
  - Fan Fire, held for several ticks, shows a flash and its streaks per tick, all inside the cone, and leaves no effect behind.
  - Powder Keg shows no flash or streak; an outline exactly at the circle's edge; a burst over the middle at least as big as the radius; a shockwave ring; and a spark on each of the three dummies it hits.
- **Can the tests fail?** Planted one at a time, each removed again:

| Planted bug | Caught by |
|---|---|
| Every shot from the right hand | `TwoShotsInARow_ComeFromDifferentHands`, `EachHitOfABurst_HasItsOwnStreak_FromTheHandsInTurn` |
| The flash at the character's centre, not the muzzle | the basic shot, the narrow line and the no-pistol tests (0.82 m from the barrel end, not under 0.03) |
| Effects never remove themselves | the basic shot test and `FanFire_EveryTickFiresInsideTheCone_AndLeavesNothingBehind` |
| Scatter Blast's wedge half as wide as the cone (first round) | `ScatterBlast_StreaksAcrossTheCone_AndAWedgeOverIt` (35, not 70) |
| Scatter Blast's wedge back on the ground at the character | the same test ("level with the gun that fired": 0.06, not 1.54) |
| Powder Keg's outline at 0.8 of the radius | `PowderKeg_NoFlashOrStreak_AnOutlineOfItsCircle_ABurst_AndASparkOnEveryTargetHit` (2.0, not 2.5) |

- **Seen** in Play mode with the Editor unfocused, from the game camera at 28 m, with bloom on. These are captures of the Game view itself at 1280 × 720. Each skill and the basic shot was captured in the frame it fires and a few frames after, by pausing the Editor and stepping it at 60 frames a second; nothing was slowed down. Also the same frame with bloom off and on for the basic shot, Quick Shot, Heavy Volley and a frame with no effect. Captures of the camera alone do not show the bloom, because they render without HDR. Everything is in `E:/repos/A-Roguelike-Game/blender-work/shot-fx-captures/round2/`; the first round is one folder up.
  - **Scatter Blast** now reads as one blast out of the guns, sideways and toward the top of the screen. The wedge is a level fan at gun height, which hides the lower half of a dummy standing in it for its 0.22 s.
  - **Powder Keg** reads as a bang: a big red-and-white burst in front of the dummies, a glowing ring at the edge of the circle and a shockwave inside it.
  - **Fan Fire's** four glowing streaks a tick have presence without becoming a sheet.
  - **Colours:** the shots are still told apart by colour. Long Shot was moved from light blue to a deeper blue, because under bloom the light blue turned cyan, like Backstep.
  - **The basic shot** is a thin glowing yellow line with small stars, brighter than before but still much lighter than any skill.
- **What still looks weak:** the long lines pull into far ends that are often off screen; a hit spark on a dummy that is flashing white is hard to see; the effects are flat cut-outs facing the camera, which shows only in side close-ups. Whether any of it reads well at play speed needs Kristian's eye.
- **Not covered by a test:** how anything looks (colours, sizes, shapes, glow); the bloom settings; the charge scaling of Long Shot; the empowered look; the hit-spark offset toward the camera.
- The muzzle is read in the frame the shot goes off, as the pistol was posed in the frame before. Every skill has at least 0.1 s of startup and the arms come up in 0.08 s, so the pistols are raised when a shot goes off.

### Both hands closed (2026-11-18, branch `art/grip`, uncommitted)

The Blender agent found that Kristian's closed left hand is not baked into the mesh: it is a shape key, `GripR`, at full weight (the name says R; it moves the left hand). It made `Rigged_lukket_begge.fbx`, his file plus a second shape key, `GripRightHand`, the left one mirrored. It is in `Assets/Game/Art/Player/Outrider/` next to his file, which is unchanged and stays as the fallback.

- **The builder takes the first figure that is there**: `Rigged_lukket_begge.fbx`, then `Rigged_lukket.fbx`, then the Meshy file (`PlayerModelPreference` in `ControlRoomBuilder.cs`; delete its `BothHandsClosedModelPath,` line to go back). Blender's camera and light in the new file are switched off on import, as for his.
- **The shape keys come in at full weight by themselves.** Both files: `GripR` at 100 on import; the new file also `GripRightHand` at 100. Nothing in the builder or the scene sets them (no override in the scene), and both stay at 100 in Play mode, standing and shooting. The new file's mesh imports with 18 343 vertices against 18 240 for Kristian's (the triangle count is the same, 12 447). I take the difference to be vertices Unity splits where the new shape key's normals differ; I did not look further.
- **Does the right pistol sit in the right hand?** The builder mirrors the left pistol into the right hand exactly. Measured in the built scene: the hand bones are 0.40 cm off a perfect mirror, so the right pistol is 0.40 cm off where the left one sits relative to its hand. The closed right hand's mesh, mirrored, lies within 0.38 cm of the left hand's (median; 1.0 cm for 95% of the vertices, 1.9 cm at most).
- **Seen in close-ups** (in `E:/repos/A-Roguelike-Game/blender-work/right-hand/unity-captures/`), standing and shooting, from the outside, the front and, while shooting, from below: the right hand closes around the grip as the left does. Fingers wrap the front of the grip, the base of the grip sits in the palm, and there is no gap and no pistol floating beside the hand. Both hands have the same flaws, which come from the shape key: thin, stretched slivers of glove at the fingertips and by the trigger guard. On the right hand one sliver is longer and points along the frame toward the trigger. Standing, the two hands are not posed alike (the idle clip lowers the left arm and holds the right forward), so the standing pictures are not mirror images. From the game camera at 28 m the hands cannot be told apart.
- 82 rule tests and 81 of 82 PlayMode tests pass; the one inconclusive is the same as before.

### Kristian's own rig (2026-11-18, branch `art/grip`, uncommitted)

Kristian found the Meshy rig badly set up and rebuilt it in Blender. He moved or turned almost every body bone, removed `head_end` and added three-bone finger chains to both hands. He decided his rig is the one that counts. The Blender agent weighted the fingers, he posed the left hand around the pistol, and the agent mirrored the pose to the right hand and baked the grip in as the rest pose. The file is `Assets/Game/Art/Player/Outrider/Outrider_grip.fbx`: the same mesh, material and UVs, 60 bones, no blend shapes, and his pistol loose in the left hand as before.

- **The builder takes it first**, ahead of `Rigged_lukket_begge.fbx` (`OwnRigModelPath` in `PlayerModelPreference`; delete that one line to go back). Blender's cameras and lights are switched off on import, as for the others. Switching back was tried: the builder then makes the clip files Generic again, and the old figure runs exactly as before.
- **The clips as they were (Generic) do not fit his rig.** They write the Meshy bones' local placement, and his bones are placed and turned differently. Standing and running, the body and arms looked about right, but the feet were bent down to the toes and the boots stretched to points (captures `generic-*`).
- **The fix is Humanoid retargeting.** His model, `Outrider_Run.fbx` and `Outrider_Actions.fbx` are each imported as Humanoid with an avatar of their own, and Mecanim carries the pose from the Meshy rig over to his.
  - The builder maps the 22 body bones explicitly by their Meshy names (`HumanMap`: `Spine02` → Spine, `Spine01` → Chest, `Spine` → UpperChest, `LeftArm` → LeftUpperArm, `LeftToeBase` → LeftToes, and so on). The fingers and `headfront` are left unmapped, so the fingers keep the baked grip instead of being driven to Humanoid's default open hand.
  - The reference pose of each avatar is the file's own rest pose, an A-pose with the arms down; no T-pose was supplied. All three avatars come in valid and human, and the console shows no import warning about them. The two rigs share the same A-pose, and the result looks right, so no T-pose was built.
  - The clips bake the root's turn, height and ground position into the pose, so nothing moves the root; root motion stays off. The Animator controller, the four states, the dash showing the run clip, and the clip names and loops are as before.
  - What `CharacterAnimation` does after the Animator works only from bone positions and from how each pistol was held at the start: the hips held over the character, the arms pointed at the target, the hands turned so the pistols aim. None of it depends on the Meshy bone axes, and all of it still works. The pistols hang on the hand bones and the shotgun on `Spine`, placed where the rest pose has them.
- **Seen** in Play mode, with the Editor unfocused, from the game camera and from a side view 4 m away. The captures are in `E:/repos/A-Roguelike-Game/blender-work/kristian-rig-captures/`; `before-*` are the previous figure, `generic-*` his rig with the clips unchanged, and `after-*` the result.
  - **Standing:** feet flat on the ground, knees and elbows natural, head and shoulders fine. The left pistol hangs at the hip and the right is held a little forward, as the idle clip has the arms.
  - **Running** (four frames over a cycle): legs bend the right way and the feet land flat. The airborne frame tucks both legs up more than the Meshy rig did, like a short hop. The right arm carries the pistol forward.
  - **Dash:** the run clip at speed. The same tuck shows mid-dash.
  - **Shooting:** both arms straight out, both pistols pointing at the target, the muzzle flash and streak from the barrel end. The pistols point at the target within 14 degrees: the existing test for this passes on the new rig.
  - **Hands:** much better. Close-ups from mirrored viewpoints, standing and shooting, show the fingers wrapped round both grips, the base of the grip in the palm, no gap, nothing floating and none of the stretched slivers of the blend-shape hands. The two hands look alike.
  - The coat follows the body without tearing in every frame looked at. The whole figure stands about 8 cm above the floor, as before; that is the height of the NavMesh, not the rig.
- **The right pistol** is mirrored exactly from the left in the figure's space. His right hand bone is 2.3 cm off the mirror of the left (he moved and shortened it), so relative to that bone the right pistol is 2.3 cm off the left one's place. The closed right hand's mesh lies within 0.41 cm of the mirrored left hand's (median; 1.4 cm for 95% of the vertices), so the pistol sits in the right hand as in the left.
- **Tests:** 82 rule tests and 82 of 83 PlayMode tests pass, the one inconclusive as before. No test had to change. The four animation tests (the poses, the figure staying on the character through a dash, the pistols pointing at the target, the pistols staying at the hands) pass on the new rig.
- **Not done:** motion polish. Kristian said animation does not matter much for now. The run clip's airborne tuck is the most visible oddity.
- **Superseded later the same day:** the Humanoid retargeting described here posed the run and idle wrongly (see "The run on his rig" below). His rig now plays clips baked for it, and the avatars are gone.

### Aiming with the upper body, and shooting on the move (2026-11-18, branch `art/grip`, uncommitted)

After playing, Kristian asked for two things, both about the basic shot only. First, she should not always turn her whole body to the target, only the upper body when the target is in front of her. Second, an attack while walking should not stop the walk: "she slows down and shoots before she speeds up again". He then decided: one shot per command, the rhythm is the player's; and a target outside the front angle turns the whole body while she keeps gliding along her way. Animations for a target behind her come later. Skills keep their commitment and facing exactly as they were.

- **Whether the body turns** is a rule, `UpperBodyAim.BodyMustTurn` in `Roguelike.Rules` (`MovingShot.cs`). A target more than the front angle to either side of where the body faces turns the whole body, as before. A target within it does not: the body keeps its direction. It is decided when the shot starts, standing or moving, and also applies while she stands waiting for the next shot.
- **The upper-body turn** is presentation, in `CharacterAnimation`, on his rig's bone names:
  - The three spine bones (`Spine02`, `Spine01`, `Spine`) take 60% of the turn toward the target between them, so it is spread up the back instead of hinging at the waist. The neck and head turn on so the head faces the target.
  - The arms, aimed after that, make up the rest, so both pistols point at the target.
  - It blends in and out with the aim (0.08 s), never goes past 90 degrees, and works over the run clip as well as the idle stance.
  - `TorsoShare` and `MaxTwist` are on the component, which the builder sets up.
  - The arms now aim at the target or the skill's aim point, not straight ahead of the body.
- **Shooting on the move** (`PlayerCharacter`, speed rule `MovingShot.SpeedShare` in `Roguelike.Rules`):
  - When she is walking on a move order and an attack command (attack-move click near a target, or right-click on an enemy) names a target within basic-attack range, the move order stays and she fires one basic shot with its normal timing.
  - Her speed limit drops to 50% while the shot winds up. From the moment it goes off, it climbs back in a straight line to full speed 0.2 s later, and she walks on to where she was going.
  - Inside the front angle the legs keep running and the upper body aims. Outside it, the body faces the target for the shot while she keeps going along her path at the slowed speed, then faces the way she goes again. There is no backward or sideways run clip, so the run clip just plays.
  - The run clip's speed already follows the walking speed, so it slows with her.
  - The decisions as built:
    1. Standing when the command comes: as before.
    2. Target out of range: as before; the attack order takes over and she walks to it.
    3. A new move order while a shot on the move winds up changes where she goes and does not cancel the shot. A dash or a skill cancels it at no cost. Standing shots keep the League rule: a move order cancels them before they go off.
    4. The cadence is unchanged. An attack command on the move that comes before the next shot is allowed waits as a buffered press does (0.15 s) and then lapses; it is not kept until the shot is ready.
    5. No automatic repeat on the move.
- **New feel values**, in `FeelValues` (tuned in `Assets/Game/Settings/FeelSettings.asset` while playing) and in the feel-spec table as guesses:
  - `ShotFrontAngle` 60 degrees;
  - `MovingShotSpeedShare` 0.5;
  - `MovingShotRecovery` 0.2 s.
  The feel-spec also has a "decided" row for the attack command while walking.
- **One engine detail:** the NavMeshAgent does not slow down by itself when its speed limit drops below its current speed, so the character's velocity is capped to the limit each frame.
- **Tests.**
  - Rules: 90 of 90, which is 82 from before and 8 new: the twist angle and its sign, inside and outside the front angle, a target on the character, the speed share while winding up, during the climb back and without a shot, and the feel-spec start values for the three new values.
  - PlayMode: 91 of 92 pass, the one inconclusive as before (`Animation_TheShotGoesOffAtTheClipsFiringMoment`). That is 82 from before and 9 new in `MoveShotTests`:
    - an attack in range on the move keeps the move order, slows to the set share with the body still facing the run, hits once, is back at full speed after the recovery time, arrives, and fires no second shot;
    - out of range on the move is as before;
    - standing is as before (the order takes over and repeats);
    - a target outside the angle on the move turns the body to it while the position keeps advancing along the path, then the body faces the run again;
    - a move order during the wind-up does not cancel the shot, and she goes to the new destination;
    - a dash during the wind-up cancels it;
    - a skill on the move stops, faces and locks as before;
    - standing with a target within the angle: the body does not turn, the upper body does, and both pistols point at the target within 14 degrees (the tolerance of the existing aim test);
    - standing with a target outside the angle: the whole body turns.
  - **One existing test changed:** `CuttingTheRecoveryShort_DoesNotRaiseTheFireRate`. It steps away and orders the attack again at once. That is now a shot on the move, and a command that early lapses (decision 4), so the second shot never came. The test now repeats the command every frame, as a player hammering the button would, and still checks that the second shot comes a whole shot time after the first. No other test needed changing; the existing aim test passes with the upper body turned.
- **Can the tests fail?** Planted one at a time and removed again:

| Planted bug | Caught by |
|---|---|
| An attack on the move cancels the move | the moving-shot test and the outside-the-angle test (two others turn inconclusive, as their premise fails) |
| The body always turns | the standing-within-the-angle test and the moving-shot test |
| The slow never ends | the moving-shot test (3.0 m/s, not 6.0) |
| A move order during the wind-up cancels the shot | `OnTheMove_ANewMoveOrderDuringTheWindUp_ChangesTheWay_ButDoesNotCancelTheShot` |

- **Seen** in Play mode with the Editor unfocused, stepping at 60 frames a second. Each scenario has three frames: the wind-up, the frame the shot goes off and 24 frames later. Each frame was captured from the game camera, from a closer view in front and to the side, and from straight above. The captures are in `E:/repos/A-Roguelike-Game/blender-work/move-shot-captures/`.
  - **Running with the target 45 to 50 degrees to the side** (`side45-*`): the legs run on while the chest, shoulders and head turn toward the target. It reads as a person turning her upper body, not a broken waist. Both pistols point at the target, and the streak leaves a barrel end.
  - **Target straight ahead** (`ahead-*`): no turn; she shoots ahead while running.
  - **Target behind her** (`behind-*`): she turns her whole body to it and glides backward along her path with the run clip playing. That is the accepted gap until there are back and side run animations. Afterwards she faces the run again.
  - **Standing at 45 to 50 degrees** (`stand45-*`): the feet stay; the upper body and head turn and both arms point at the target.
  - The coat and the shotgun on the back follow the turned spine without tearing or poking through in any frame looked at.
- **The NaN wedge from the effects work:** no degenerate input was found. A zero direction, a zero angle, a zero range and a muzzle on the character's centre are all handled or refused. The one burst of errors came five seconds after a burst of "PlayerLoop called recursively" errors from my capture tool's frame stepping, and it never came back in any test or capture since. The likeliest cause is that tool, not the game, so no guard was added.
- **Not verified:** play with the real mouse and keyboard, which is where the timing of a-move and moving shots will be judged; the feel of the 50% slow and the 0.2 s climb back; a target that moves during the shot.

### Left-click is attack-move ("smart cast") (2026-11-18, branch `art/grip`, uncommitted)

Kristian: "maybe left click is 'smart cast' a-move, then it gets even more intuitive."

- **Before:** a left-click did something only after A. On its own it did nothing, and a test pinned exactly that.
- **Now:** a left-click is the attack-move at the cursor, with or without A first. On a target it attacks that target; on the ground it attack-moves there and goes for the target nearest the click. While walking on a move order, with that target in range, it is a shot on the move, as A then left-click is.
- **Unchanged:** A then left-click works as before (A still shows the range ring), Esc and right-click still cancel the aim, and right-click is unchanged.
- **No conflict found.** No skill uses the left button (skills fire on their keys at the cursor, with no confirming click), and the on-screen readout and skill bar are drawn boxes with no buttons. A left-click on them is a click on the ground under them, as a right-click already was.
- The help line on screen now reads "LMB (or A, LMB) attack-move".
- **Tests:**
  - The test "a left-click without A orders nothing" is now `Input_Esc_CancelsTheAim_WithoutOrderingAnything`; the part that pinned the old left-click is gone.
  - New, fed frames: `Input_ALeftClickWithoutA_OrdersAttackMoveAtTheCursor` (the order, no aim left behind, she arrives at the point) and `Input_ALeftClickNearATargetInRange_WhileWalking_IsAShotOnTheMove`.
  - With the left-click put back behind A (a planted bug), both new tests failed.

### The run on his rig (2026-11-18, branch `art/grip`, uncommitted)

Kristian: "I think we must look at the run animations again after the new rig." The target was that his rig looks as the old Meshy rig did at the same clip times; it is the same mesh on a different skeleton.

- **Measured first.** Both rigs were posed by the clips in a preview scene, so the open scene was not touched. The run was sampled at 16 evenly spaced times and the idle at 8, and the joint positions were compared in the figure's own space.
  - The joints are not in the same places even at rest: his hips are 3.9 cm from the Meshy hips, his knees 4 to 7 cm, his ankles 5 to 6 cm and his toe joints 12 cm.
  - **Humanoid, as it was** (old figure against his rig, mean and largest difference over the run): left foot 63 and 87 cm, left toes 75 and 106 cm, right foot 39 and 46 cm, left knee 28 and 32 cm, hands 27 to 33 and up to 38 cm, head 5 and 9 cm.
  - So it was not only the airborne tuck. The left leg was pulled up by up to 0.9 m at the worst times, the arms swung differently, and in the idle the left foot and toes stood 20 to 30 cm off.
- **Cause.** The Humanoid avatars of the two clip files took their reference pose from the files' default pose. In those files that is not a rest pose but a moment of the animation: the left knee bent 121 degrees in the run file, the left upper arm 70 degrees off. With a wrong reference, Mecanim's muscle space is skewed. Avatars set up by Unity's own automatic mapping of the same files gave a different but also wrong result (legs within 10 cm, hands off by 42 to 45 cm).
- **"Hips held over the character" plays no part.** It takes out the clip's side-to-side and fore-and-aft hip sway (1.8 and 7.4 cm), the same on both rigs.
- **The fix: the clips are baked for his rig.**
  - The builder (`BakeForRig`) poses the Meshy rig (`Outrider_Rigged.fbx`) with each original clip, frame by frame. For each of the 22 body bones it takes how the bone has turned from its rest in the figure's own space, and turns his bone the same way from his rest. The hips move as the Meshy hips move from their rest.
  - This gives new Generic clips, `Assets/Game/Art/Player/Outrider/Baked/Outrider_grip_Idle.anim` and `Outrider_grip_Run.anim`, made by the builder on every run and kept at the same path.
  - His model is Generic again. The clip files are Generic again, as they were before the rig work.
  - The finger bones, `headfront` and the end bones get no curves, so the baked grip stays.
  - The Humanoid setup is gone; there are no avatars.
- **After the fix,** old figure against his rig, mean and largest over the run:

| Body part | Rest difference | Run: mean | Run: largest |
|---|---|---|---|
| Head | 0.0 | 1.0 | 1.3 |
| Hands | 0.5 – 2.1 | 1.2 – 3.1 | 3.6 |
| Elbows | 0.0 – 1.0 | 1.0 – 1.6 | 2.1 |
| Hips | 3.9 | 3.9 | 4.2 |
| Knees | 4.3 – 7.4 | 3.8 – 6.6 | 8.2 |
| Ankles | 4.8 – 6.0 | 7.1 – 8.3 | 9.8 |
| Toe joints | 12 | 6.9 | 12 |

  All values are in cm. Idle: head 0.7, hands 0.8 to 1.9, knees 1.8 to 5.3, ankles 4.6 to 5.2, toe joints 8.6 to 9.4. The differences are now the size of the joints' own different placement, not of the motion. The dash plays the run clip, so the run numbers stand for it.
- **Seen.** Captures are in `E:/repos/A-Roguelike-Game/blender-work/run-fix-captures/`.
  - **Old figure against his rig** (`compare-run-t0..7-side/game.png`, `compare-idle-*.png`): both are posed at the same clip times in a preview scene, without the pistols, which the game adds. They are practically the same pose at all eight times: legs, feet, arms, lean and head. The one visible difference: in the push-off frames the boot toes bend up a little more on his rig, where his toe joint sits 12 cm from the Meshy one.
  - **In Play mode after the fix** (`play-idle-*`, `play-run-0..3-*`, `play-dash-*`, `play-shoot-*`, `side45-*`, `hands-shoot-*`): idle with feet flat; a run with no hop, legs bending right and feet landing flat; the dash; the standing shot; and a shot on the move at 45 to 50 degrees with the upper body turned and both pistols on the target. The hands are closed round both grips.
  - **"Before" renders are missing.** The preview renderer drew his Humanoid rig unposed, though its bones did move, so it made no usable "before" pictures, and they were not kept. The numbers above and the earlier in-game captures (`kristian-rig-captures/after-run-*`) show the before.
- **What it means for later clips.**
  - **Clips made for the Meshy rig of this same figure**, such as more Meshy library clips bought for the Outrider: the builder bakes them exactly. Add a line per clip, the way the idle and run are added.
  - **Clips from other sources or other figures** (a library with other bone names or other proportions): this bake needs the same bone names and the same body. For those, Humanoid retargeting is the general route, but each avatar needs a true rest or T-pose reference, which the clip files here did not have. The work is then in setting up the avatars (Unity's avatar configuration with Enforce T-Pose, or a T-pose supplied by script), not in the clips.
  - For figures Kristian rigs himself: if he keeps the bone names and exports the rest pose as the file's default pose, Humanoid on his own rigs should behave. An exported T-pose would make it more reliable.
- **Tests:** 90 rule tests, 93 of 94 PlayMode tests (with the two new input tests), the one inconclusive as before. No test pinned the old animation.
- **Way back:** delete the `OwnRigModelPath,` line in `PlayerModelPreference`. The builder then uses `Rigged_lukket_begge.fbx` with the original clips, as before; the baked clips are then unused.

### A faster shot, a shooting gait, eased speed and recoil (2026-11-18, branch `art/grip`, uncommitted, on top of 160885d)

Kristian asked for four things. First, shoot more often. Second, use the bought clip `Walk_Forward_While_Shooting` for the legs during a shot on the move. Third, ease the speed down and up: "I imagine the deceleration and the acceleration must be slower". Fourth, if it can be done, a simple recoil on every shot.

**1. The faster basic shot.**
- Startup, active and recovery go from 0.15 / 0.05 / 0.25 s to 0.08 / 0.02 / 0.125 s. The change is in `FeelValues`, in the feel-spec row (still a guess, noted "Kristian 2026-11-18: shoot more often") and in `FeelSettings.asset`. An editor script first checked that the asset still held 0.15 / 0.05 / 0.25, then set those three fields only. Damage stays at 40.
- **Shots per second:** 2.2 before (one shot every 0.45 s), 4.4 now (one every 0.225 s). Basic-shot damage per second goes from 89 to 178.
- **Focus:** a shot gives 2, so it takes 50 shots to fill the gauge: 22.5 s of steady fire before, 11.25 s now.
- **Knock-on effects:**
  - **Aim blend.** At 0.08 s the arms would only just reach the target as the shot goes off, so the blend (and the twist that uses it) is now 0.05 s. The arms are up before the shot.
  - **Hit stop.** 0.05 s would be 22% of a 0.225 s shot, so basic-shot hits have their own `BasicShotHitStop` of 0.025 s, which is 11% of a shot. Skill hits keep 0.05 s. This is new and also a guess.
  - **Effects.** The flash (0.07 s) and the streak (0.1 s) are over before the next shot. Each pistol fires every 0.45 s, so a hand's effects never overlap.
  - **Input buffer.** The buffer (0.15 s) is now more than half a shot. A second command in the first 0.075 s of a shot lapses. A command any later is kept and fires as soon as the shot allows. A test pins this.
  - **The moving shot's slow** would have lasted only 0.08 s under the old rule. The new eased rule (part 3) replaces it.
  - **Changed test:** `MoveOrderInAShotsRecovery_MovesAtOnce` assumed a recovery longer than 0.15 s, so it turned inconclusive. It now steps 0.15 m (not 0.3 m) and checks she left within 80% of the recovery.

**2. The shooting gait.**
- `Walk_Forward_While_Shooting` is baked for his rig like the idle and the run (`Baked/Outrider_grip_ShootWalk.anim`, 3.27 s, looping). The Animator has a new state, `ShootWalk`.
- New pose `ShootMove` (`PoseRule`): while a shot on the move slows her (target in front), the legs, hips and body play the gait. The arms and the upper-body twist are procedural on top, as before.
- It cross-fades from and to the run over `ShootGaitFade` (0.15 s, in `FeelSettings.asset`, a guess). Chained shots stay in the gait. The gait ends when her speed is back up. A target behind her keeps the run, as before.
- A hit stop no longer flashes the idle pose for a frame on the move. During a hit stop her speed reads 0, so the animation now keeps the speed from before it. This was a bug before too, with the run.
- **Playback speed comes from her actual speed. That rate is absurd, so here it is in numbers:**
  - Measured on his rig, a planted foot in the clip slides back at 0.30 to 0.34 m/s. It is a slow creeping walk, with a 3.27 s cycle and 0.49 m steps.
  - Her moving shot runs at 3 m/s at the slowest (share 0.5) and up to 6 m/s as she speeds up. With the feet matched to the ground, the clip plays at 10 to 20 times its speed: a whole cycle in 0.16 to 0.33 s. That is quick tiny steps, and the captures show it.
  - **What would look right:** the clip at its own speed fits a share of about 0.05 (0.3 m/s). At a normal walking cadence (about 3 times, a 1.1 s cycle) it fits a share of about 0.15 (0.9 m/s), with the feet still matched. At a share of 0.5, no walking clip fits; it would need a jog or run-and-gun clip.
  - My choice: build it as asked and leave it on. In the gait the speed is matched to the ground (`ShootWalkClipSpeed` 0.3 on the `CharacterAnimation` component, clamped at 20 times). Kristian should judge it in the hand.
  - The way back is one line: delete the `shootingGait` condition in `PoseRule.Pick`, or set `ShootGaitFade` large.
- `RunClipSpeed` stays at 6. Measured on his rig, the run clip's planted foot slides back at 5.7 to 6.1 m/s, so it fits. The comment now says so.

**3. Eased speed around a shot on the move** (`MovingShot.SpeedShare`, rule in `Roguelike.Rules`).
- **Slowing down:** smoothstep from the share she has when the shot starts down to `MovingShotSpeedShare` (0.5), over `MovingShotSlowdown` (0.15 s, new).
- **Speeding up:** starts only once the shot has gone off and the slow-down is done. Smoothstep back to full speed over `MovingShotRecovery`, now 0.5 s (was 0.2; it was not in the asset, so the asset now has 0.5).
- **A chained shot** eases down from the share she has at that moment.
- **A dash, a skill, or a shot cancelled before it goes off** ends the slow at once.
- All three values are in `FeelValues`, in `FeelSettings.asset` and in the feel-spec, as guesses.
- **Speed over time from the game,** stepping at 60 frames a second. "Limit" is the share of run speed allowed; "actual" is the NavMeshAgent's speed. Raw tables are in `shoot-gait-captures/speed-table-raw.txt`.

| t (s) | One shot: limit | One shot: actual | Chain of three (commands at 0, 0.28, 0.52 s): limit | Chain: actual |
|---|---|---|---|---|
| 0.02 | 1.00 (command) | 1.00 | 1.00 (command 1) | 1.00 |
| 0.08 | 0.73 (shot) | 0.82 | 0.73 (shot 1) | 0.82 |
| 0.14 | 0.51 | 0.55 | 0.51 | 0.55 |
| 0.20 | 0.51 | 0.50 | 0.51 | 0.50 |
| 0.26 | 0.56 | 0.53 | 0.56 | 0.53 |
| 0.32 / 0.34 | 0.63 | 0.58 | 0.53 (shot 2) | 0.54 |
| 0.38 / 0.40 | 0.72 | 0.66 | 0.50 | 0.51 |
| 0.44 / 0.46 | 0.81 | 0.75 | 0.51 | 0.50 |
| 0.50 / 0.52 | 0.89 | 0.84 | 0.54 (command 3) | 0.53 |
| 0.56 / 0.58 | 0.96 | 0.92 | 0.52 (shot 3) | 0.53 |
| 0.62 / 0.64 | 0.99 | 0.97 | 0.50 | 0.50 |
| 0.68 / 0.70 | 1.00 (run again) | 1.00 | 0.51 | 0.50 |
| 0.76 – 1.12 | 1.00 | 1.00 | 0.56, 0.63, 0.72, 0.81, 0.89, 0.96, 0.99 | lags about 0.05 behind |
| 1.18 | | | 1.00 (run again) | 1.00 |

- **Metres lost per moving shot,** at 6 m/s:
  - Old rule with the old timing: 0.75 m, calculated (0.15 s at half speed, then a 0.2 s straight climb).
  - Old rule with the new timing: 0.54 m, calculated.
  - New rule: 0.98 m, calculated. **Measured in the game: 1.15 m**, because the agent's actual speed lags the limit on the way up.
  - A chain of three loses 2.59 m (measured).
  - So the eased version costs her about 0.4 m more per shot than before. That is what "slower deceleration and acceleration" buys. The 0.5 s climb is the value to tune.
- The gait's playback follows the actual speed, so the steps slow and quicken with the curve.

**4. Recoil** (`CharacterAnimation`, presentation only).
- On every shot from a pistol, only the hand that fired kicks:
  - the hand comes back toward the shoulder and the elbow gives (a two-bone bend);
  - the muzzle flips up;
  - the shoulder on that side is nudged back by 15% of the flip, as part of the upper-body turn, so the other arm still aims true.
- The kick is fast (ease-out to the peak) and the settle slower (smoothstep back).
- **Values, all in `ShotFx.asset` and all guesses:**
  - the base kick: `RecoilAngle` 16 degrees, `RecoilPullBack` 0.07 m, `RecoilKickSeconds` 0.04, `RecoilSettleSeconds` 0.16;
  - per look, `Recoil` as a share of the base: basic 1, Quick Shot 1.5, Long Shot 1.3 (and it grows with the charge, as the flash does), Scatter Blast 2, Heavy Volley 2.2, Backstep 1.4, Triple Tap 1, Fan Fire 0.5 (small), **Powder Keg 0 (none: it is thrown, not fired)**;
  - empowered shots kick 25% harder, with their effects;
  - caps at 40 degrees and 0.15 m.
  These were written into his `ShotFx.asset` by script: only the new fields, nothing else changed.
- A kick lasts 0.2 s, less than a shot (0.225 s) and far less than that hand's next shot (0.45 s).
- **It never moves where the shot starts.** The shot reads the muzzle in the fire frame, before the kick is posed. The flash stays where the muzzle was, so in the frames after the shot the pistol is seen pulled back from its own flash by a few centimetres. That is the trade-off of "do not move the muzzle-effect start".

**Tests.**
- Rules: 92 of 92 (90 before; the moving-shot tests rewritten into five; one new pose test).
  - Moving-shot tests: the slow-down starts at the current share and reaches the slow share at the slow-down time and not before; the speed-up waits for both conditions; full speed after the recovery time; the curve goes one way at a time without jumps; a chained shot eases from the current share.
  - Pose test: walking while slowed by a shot shows the shooting gait, and a dash still wins.
- PlayMode: 98 of 99 pass, with the one inconclusive as before (`Animation_TheShotGoesOffAtTheClipsFiringMoment`). New:
  - `FireRate_IsFromTheSettings_TwoCommandsACycleApartGiveTwoShots_AFasterPairOne`;
  - `OnTheMove_TheLegsShowTheShootingGait_ThenTheRunAgain` (pose, Animator state, playback at her speed, back to Run);
  - `OnTheMove_ChainedShots_StayInTheShootingGait`;
  - `Animation_TheShootingGaitIsBakedForThisRig`;
  - `Recoil_KicksOnlyThePistolThatFired_AndEachIsBackOnTheTargetBeforeItsNextShot`. Over a standing burst of four, it checks for each shot that the flash is at the barrel end, the hands take turns, the firing pistol was on target when it fired, the muzzle flips up by 70 to 110% of the kick angle, and the other pistol stays on target and does not flip. It also checks that the firing pistol has settled back afterwards.
- Changed: the moving-shot test now checks the eased curve: it starts slowing at once but not in a jump, is at the slow share when the slow-down is done, is not yet at full speed just before the recovery is up, and is at full speed after it. Also `MoveOrderInAShotsRecovery_MovesAtOnce` (see part 1), and `HitStop_FreezesTimeAtTheHit_ThenReleases`, which now uses the basic-shot hit stop.
- **Planted bugs,** one at a time and removed again:

| Planted bug | Caught by |
|---|---|
| The next shot waits twice the shot time | the fire-rate test (one shot, not two) |
| The shooting gait is never shown | both gait tests |
| The speed-up does not wait for the slow-down to finish | three of the five rule tests |
| Both pistols kick on every shot | the recoil test ("the other pistol's muzzle does not flip up", 8 degrees). Before that check was added, the other-pistol-on-target check caught it only narrowly (0.969 against 0.97), so the flip check was added. |

**Seen.** Captures are in `E:/repos/A-Roguelike-Game/blender-work/shoot-gait-captures/`. They were taken in Play mode at 60 frames a second. Each frame has the game camera (`-game`), the game camera's angle moved in close (`-gamezoom`), and a side or close view. Each sequence has a `.txt` with time, speed share, pose and shots per frame.
- `moveshot-ahead-01..08` and `moveshot-45-01..08`, every 5 frames over 0.6 s: from the run into the gait, the shot, and back to the run. The gait reads as a walk with legs apart and both pistols on the target, but the steps are very fast (see part 2).
- `chain3-01..10`: three shots, in the gait all the way.
- `burst4-01..08`: standing, four shots, hands in turn.
- `recoil-f00..f13`: a single kick, frame by frame. The near hand comes back with the elbow bent and the muzzle up at frames 2 to 4, and is back on line by frame 13.
- **Found, not fixed:** in every pose she stands 8.3 cm above the floor. The NavMesh surface is at y 0.083 over a floor at 0, and the model is placed for y 0. It shows in the side views. This was there before this work; the fix would be one line in the builder or `CharacterAnimation`.

**Not verified:** play with the real mouse and keyboard; how 4.4 shots a second feels; whether the fast gait reads well at the game camera's distance in motion (stills cannot show it); the recoil on skills other than the basic shot, which has no test of its own; Triple Tap and Fan Fire, whose hits come closer together than a kick lasts.

### Click to shoot, feet on the floor, and the bought shooting gaits (2026-11-18, branch `art/grip`, uncommitted)

Kristian liked the faster shot, the eased speed and the recoil ("yes, like it very much"); those stand. Then: "I think auto attack is not a thing any more, but click to shoot. Then it hangs together better."

**1. Click to shoot, no auto-attack.**
- **Before:** a standing attack order kept firing until something else was ordered. Only shots on the move were one per command. An attack command on the move in the first 0.075 s of a shot lapsed.
- **Now every basic shot needs its own command:** a left click, a right click on an enemy, or A then left click.
  - **Standing, target in range:** one shot, then she stands.
  - **Walking on a move order, target in range:** one shot on the move, as before.
  - **Target out of range:** she walks until it is in range, fires once and stands.
  - **Attack-move on the ground with nothing near the click:** she walks toward the point as before. If a target comes into range on the way, she fires once at it and stands; she does not walk on to the point.
- **Fast clicking loses nothing.** A click while a shot is still in its cycle waits, wherever in the cycle it lands, and fires as the next shot the moment the cycle allows, standing or moving.
  - Only one click waits. A newer click replaces it, so the waiting shot goes at the latest target, and ten clicks in one cycle are still one shot.
  - A move order, a dash or a skill drops the waiting click.
  - The fire rate is still capped by the whole shot time (0.225 s), however fast the clicks come.
- **Holding a button does not repeat:** one press, one shot. Holding the right button on the ground still steers the walk after the cursor.
- **The League rule is unchanged:** a move order before a standing shot has gone off cancels it at no cost, and an attack on another target before it has gone off switches at once.
- **How it is built:**
  - **The rule** is `ShotCommands` in `Roguelike.Rules` (`Assets/Game/Rules/ShotCommands.cs`), with EditMode tests. Each attack command gets a number. A shot is tagged with the command it was started for. When the shot goes off, it answers its command only if no newer command has come in meanwhile; a newer one still waits for a shot of its own. `MustWait` is the cycle cap. `PendingOne<T>` holds at most one waiting item, with no expiry.
  - **Standing,** the attack order itself is the waiting shot. It is done, and cleared, when its own shot goes off, so nothing repeats. A click during a shot replaces the order. It then waits for the cycle in the existing "next shot waits the whole shot time" check, walking into range first if it must.
  - **On the move,** the waiting click is a `PendingOne<Damageable>`. It no longer lapses after the input buffer. When it fires, if she is still walking and the target is in range, it is a shot on the move. If she has stopped meanwhile, it becomes a standing attack order. If she is still walking but the target has left the range, it is let go.
  - **The general input buffer** (0.15 s, for dashes and skills during a lock) is untouched. Shots no longer go through anything like it, which kept the code simpler than bending the buffer.
  - **The held right button:** the input driver sends an attack or a move only on the press. On the held frames it calls a new `SteerTo`. `SteerTo` walks like `MoveTo` but keeps a waiting click on the move, and leaves alone an attack order given while the button was held; once that order's shot has gone off, the steering takes over again. Without this, holding right to walk would have dropped every waiting click on every frame.
  - **A dash or a skill** drops a click on the move, and an attack order while a shot is in its cycle (including the shot it cancels). An attack order still walking to a target out of range, with no shot in its cycle, survives a dash, as a move order does.
  - **Arms between rhythm clicks.** With a click waiting on a target in range from where she stands, the shot's recovery is no longer cut short. It runs out exactly when the next shot may start, so the arms stay up between shots. Cutting it short dropped them for a moment, which the recoil test caught.
- **On-screen help line:** "RMB move, or shoot a target | LMB (or A, LMB) attack-move: one click, one shot".
- **Feel-spec rows:** Attack-move, Right-click on an enemy, Attack command while walking, and a new "Basic attack repeat" row, all marked "Kristian 2026-11-18: click to shoot, no auto-attack".
- **Clicks and shots from the running game** (`click-shoot-captures/click-table.txt`, frames step 0.02 s). Six clicks, one every 7 frames, which is about twice the fire rate:

| | Clicks (s) | Shots started (s) | Gaps between shots (s) | Shots |
|---|---|---|---|---|
| Standing | 0.00, 0.12, 0.24, 0.38, 0.48, 0.60 | 0.04, 0.28, 0.52, 0.76 | 0.24, 0.24, 0.24 | 4 of 6 |
| Walking | 0.00, 0.10, 0.24, 0.34, 0.48, 0.58 | 0.02, 0.24, 0.48, 0.72 | 0.22, 0.24, 0.24 | 4 of 6 |

  Every shot starts on the first frame the 0.225 s cycle allows. Two clicks each time fall inside one cycle, so under the one-waiting rule they make one shot: 6 clicks give 4 shots, and no click is lost beyond that rule.
- **Hold-to-fire, if he wants it:** small, about an hour with tests. The driver would re-send the attack while the left button (or the right button on a target) is held, whenever no click is waiting. The waiting rule already caps it at the fire rate. It needs a "left button held" field in the input frame and one test.

**2. Feet on the floor.**
- **Cause:** the baked NavMesh lies 0.083 m over the floor (voxel rounding). The agents stand on it, and both figures were placed for a floor at y = 0.
- **The enemy** had the same offset: its root stood at 0.083 and its body bottom was at 0.083. **The dummies** did not: they are not agents, and their bottoms are at 0.000.
- **Fix:** in the builder, after the NavMesh is baked, measure how far it lies over the floor (`NavMeshLift`) and lower the player's model (`Visual`) and the enemy's body and nose by that.
  - The roots are not moved, so no gameplay position changes: ground points, ranges, the agents and the shot origins are all unchanged.
  - The muzzles sit in the model's hands, so they come down with it, and every effect still starts at the barrel end. All shot-effect tests pass.
  - The builder reports "floor: the NavMesh lies 0.083 m over it".
- **Measured** (`click-shoot-captures/feet-before.txt` and `feet-after.txt`):
  - Idle toe joints: 0.116 / 0.144 m before, 0.035 / 0.060 m after. The clip's own lowest are 0.032 / 0.059.
  - Run foot contact, lowest toe: 0.145 m before, 0.062 m after.
  - Enemy body bottom: 0.083 before, 0.000 after.
- **Captures:** `feet-before-idle.png`, `feet-before-run-contact.png` and `feet-before-enemy.png`, and the same three "after". Before, the boots hang over their shadows; after, the soles are on the floor at idle and at run contact.


**3. The shooting gait from the bought clips.**
- **The file:** `Assets/Game/Art/Player/Outrider/Outrider_ShootGaits.fbx`, copied from `blender-work/meshy-clips-2026-11-18/Outrider_RunShoot_WalkBackShoot.fbx`. The builder imports it Generic, with cameras and lights off, and bakes both clips for his rig like the idle and the run: `Baked/Outrider_grip_ShootRun.anim` and `Baked/Outrider_grip_ShootBack.anim`.
- **Clip names after import:** `Run_and_Shoot` (frames 0–20, 0.667 s) and `Walk_Backward_While_Shooting_inplace` (frames 0–38, 1.267 s). They are renamed to the states `ShootRun` and `ShootBack`, both looping.
- **The creeping walk is removed:** its state, its bake and its baked file are gone. Its take is still in `Outrider_Actions.fbx`, unused.
- **Measured on his rig, in a preview scene:**
  - **Run and Shoot** has root motion: the hips travel 1.50 m in 0.667 s (2.25 m/s), while a planted foot creeps forward 0.1 to 0.3 m/s. The hips hold takes the travel out, so the feet pass under the body at about 2.1 m/s (`ShootRunClipSpeed` 2.1).
    - Over her moving-shot speeds (3 to 6 m/s) it plays at 1.4 to 2.9 times. In the captures it ran at 1.43 times at the slowest and 2.7 times just before the run takes over.
    - **The 10 to 20 times problem is gone.**
  - **Walk Backward While Shooting** is in place; a planted foot slides at 0.76 to 0.93 m/s (`ShootBackClipSpeed` 0.85).
    - **It cannot honestly carry 3 m/s:** that is 3.5 times (a 1.27 s cycle in 0.36 s), and up to 7 times at the moment the shot starts, when she is still near full speed. The gait clamp is now 8 times.
    - It would look right at about 1 to 1.3 m/s, a share of about 0.2.
    - What saves it: it only plays while the body is turned, which is the shot itself, about 0.25 s; then the body turns back to her path and the forward gait takes over. So it is a quick backward shuffle for a moment, which reads better than the glide, but not as a backward run.
- **The rule for forward, backward and sideways** is in `PoseRule.Pick`, which now takes a "moving backward" flag. `CharacterAnimation` sets the flag when her velocity points more against where the body faces than with it (dot product below 0).
  - In the shooting gait, backward gives `ShootBack` and anything else gives `ShootRun`.
  - **Sideways** (body square to the path) has no clip of its own: it gets whichever side of 90 degrees the lean is on. At exactly square, it is the forward Run and Shoot, the clip that fits her speed better.
  - During a hit stop the flag keeps its last value, as the speed does.
- **The shooting gait now also plays when the body is turned to a target behind** (it used to be the run there). It ends when her speed is back up, as before. The cross-fades (`ShootGaitFade`) cover run, forward and backward gait. Chained shots, including clicks in rhythm, stay in the gait.
- **Tests:**
  - `OnTheMove_TheLegsShowTheShootingGait_ThenTheRunAgain` (forward, now on Run and Shoot);
  - new `OnTheMove_ATargetBehind_ShowsTheBackwardGait_ThenTheRun` (the pose, her velocity against the facing, the Animator in `ShootBack` at the right rate, then back to the run);
  - `Animation_BothShootingGaitsAreBakedForThisRig` (replaces the single-clip test);
  - `OnTheMove_ChainedShots_StayInTheShootingGait`;
  - rule test `MovingBackwardWhileSlowedByAShot_ShowsTheBackwardGait`.
  - **Planted bug:** the backward check never true. Caught by the target-behind test ("Expected: ShootBack, But was: ShootMove").
- **Seen** (`shoot-gait-captures/round2/`): `moveshot-ahead-01..08`, `moveshot-45-01..08`, `behind-01..08` and `chain3-01..08`. Each frame has the game camera, a close game-angle view and a side view, plus a `.txt` per sequence with pose, Animator state and playback rate.
  - **Forward and 45 degrees:** it reads as running and shooting, crouched and leaning in, with long strides. The upper-body twist, both pistols on the target and the recoil all work on top. It is much better than the creeping walk.
  - **Target behind:** the body faces the target with the legs in the backward walk for the shot, then turns back into the forward gait. It reads better than the glide, but the backward steps are fast.
  - **Chain of three:** in the forward gait all the way, at 1.4 to 1.8 times.
  - **Not verified:** whether the feet slide in motion. The rates follow the measured clip speeds, but stills cannot show sliding, and the forward clip's own feet creep a little.

**Tests overall:** rules 100 of 100. PlayMode 111 of 112, with the one inconclusive as before (`Animation_TheShotGoesOffAtTheClipsFiringMoment`).

**Tests changed for click to shoot:**
- `AttackMove_NearA/B_ShootsARepeatedly_WithoutWalking` → `..._ShootsAOnce/BOnce_WithoutWalking_ThenStands`: one hit, order done, no repeat in two shot times, no walking.
- `AttackMove_ClickNearTheFartherDummy_ShootsThatOne_FromNearA/B`: one hit on the clicked dummy, then the order is None.
- `AttackMove_TargetOutOfRange_WalksUntilInRange_ThenShoots`: now also checks no second shot and that she stands.
- `AttackMove_WhenTheClickedDummyDies_GoesOnWithTheNearest` → split in two:
  - `AttackMove_WhenTheClickedDummyDiesBeforeTheShot_TheNearestGetsTheOneShot`;
  - `AttackMove_KillingTheClickedDummy_EndsTheOrder_TheNextOneIsLeftAlone`.
- `AttackTarget_ShootsOnlyTheChosenTarget`: one hit, then the order is None.
- `Input_HeldRightClickOnADummy_AttacksIt_AtTheNormalRate` → `Input_HeldRightClickOnADummy_ShootsOnce_HoldingDoesNotRepeat`: three shot times held gives one shot; let go and press again gives a second.
- `Standing_AnAttack_IsAsBefore_TheOrderTakesOverAndRepeats` → `Standing_AnAttack_FiresOnce_AndSheStands`.
- `FireRate_..._AFasterPairOne` → `FireRate_..._AFasterPairToo_ButNotSooner`: a second click early in the first shot's cycle now gives a second shot, exactly a cycle later, and no third.
- `TwoShotsInARow_ComeFromDifferentHands`: gives a second click.
- `Recoil_...`: clicks whenever none is waiting, five clicks for the four shots it measures.
- `CuttingTheRecoveryShort_DoesNotRaiseTheFireRate`: comment only.

**New tests:**
- In `ClickToShootTests`:
  - one command is one shot, and a second needs a second command;
  - clicking every frame fires once per cycle, never sooner;
  - a click early in the shot is the next shot, a cycle later;
  - ten clicks in one cycle give one waiting shot;
  - a newer click replaces the waiting one;
  - a waiting shot is dropped by a move order, by a dash and by a skill;
  - on the move, a waiting shot is dropped by a move order;
  - on the move, holding the move button keeps a waiting shot.
- `Input_LeftClicksInRhythm_EveryClickIsAShot`.
- Seven rule tests for `ShotCommands` and `PendingOne`.

**Planted bugs for click to shoot,** one at a time and removed again:

| Planted bug | Caught by |
|---|---|
| The repeat is back (the order is not done when its shot goes off) | `Standing_OneCommandIsOneShot_...` and `Standing_AnAttack_FiresOnce_AndSheStands` (3 shots, not 1) |
| Two waiting shots kept (commands counted up to the shot going and two more) | `TenClicksInOneCycle_GiveOneWaitingShot` (3, not 2), `ANewerClick_ReplacesTheWaitingOne_...`, rule test `TenCommandsDuringOneShot_AreOneWaitingCommand` |
| A click in the first part of the cycle is lost (a command during an unanswered one is not counted) | `AClickEarlyInTheShot_IsNotLost_...` (1 shot, not 2), two rule tests |
| Holding the right button repeats | `Input_HeldRightClickOnADummy_ShootsOnce_HoldingDoesNotRepeat` (3 shots, not 1) |

## Decisions waiting for Kristian

The ten behaviours under "How the pieces behave" in the slice file are my choices, and so are the three new skills, the gauge's numbers and the enemy's. The ones most likely to matter in the hand:

1. Focus comes from damage dealt, so the big skills fill it fastest.
2. Z takes no time and can be pressed in the middle of a skill.
3. Backstep is a second way out, which makes the dash less scarce (pillar 1).
4. The enemy's attack is aimed once and then committed, so every hit taken is a mistake of the player's (pillar 4).
5. Death in this room is a reset after 2 s.

## State for the play session

- The Editor is open on `Assets/Game/Scenes/ControlRoom.unity`, Play mode stopped, no compile errors.
- Controls: right-click moves (held, it follows the cursor), or shoots what is under the cursor once. Left-click (or A, then left-click) is attack-move. One click, one shot: there is no auto-attack. Space dashes. Skills: Q Quick Shot, W Powder Keg, E Fan Fire (hold), R Long Shot (charge), S Scatter Blast, D Heavy Volley (cast), F Backstep, G Triple Tap. Z spends a full focus gauge.
- The enemy is the tall purple shape far to the right and down from the start, at (15, -10). It stands still until you come within 9 m or hurt it. Its red outlines show where it will hit.
- To tune: `Assets/Game/Settings/Skills.asset` (skills and the gauge), `Assets/Game/Settings/Enemy.asset` (the enemy), `Assets/Game/Settings/ShotFx.asset` (how the shots look, and the recoil) and `Assets/Game/Settings/PostProcessing.asset` (the bloom; untick Bloom there to turn it off), in the Inspector while playing.
- Nothing is committed on `g4/kit`.
