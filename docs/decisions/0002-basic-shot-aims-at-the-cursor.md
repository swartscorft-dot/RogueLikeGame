# 0002 The basic shot aims at the cursor

Status: **on trial: decided by Kristian 2026-11-18, to be confirmed after he has played it.** The version before it is commit `668c5c5` on `art/grip`; if the trial is not confirmed, we go back to that.

## Decision

The basic shot goes where the mouse points. There is no target lock.

- **A left click fires one basic shot toward the cursor.**
  - The shot is a straight line from her toward the cursor's ground point, 10 m long and 1 m wide (half-width 0.5 m). Both values are guesses, tunable in `FeelSettings.asset`.
  - It hits the first living target the line touches, nearest along the line, and only that one.
  - With nothing in the line it is a miss: it still fires, still costs its shot time, and shows its streak out to the full range.
- **The aim follows the live cursor until the shot goes off,** as a skill's aim does. A click with the cursor on her shoots where she faces.
- **A click never moves her.** Standing, she fires from where she stands. Walking on a move order, she fires a shot on the move, always, with the eased slow-down, the gait, the upper-body twist and the recoil as before.
- **Click to shoot stays as decided earlier the same day:** one click, one shot, one waiting click at most, the fire-rate cap, and a move order, dash or skill drops the waiting click.
- **Body and upper body:** the front-angle rule is measured against the aim direction. Inside 60 degrees only the upper body turns; outside it the whole body turns.
- **Right click only moves,** also when it lands on an enemy.
- **While she is free, she looks toward the cursor:** her head, neck and a little of the upper spine turn toward it. Her arms come up only for shots.

## Added to the trial (Kristian, 2026-11-18, after playing it)

- **Front angle 120 degrees.** He set `ShotFrontAngle` to 120 while playing and it "works fine": she runs on forward and aims with the upper body out to 120 degrees to either side. 120 is now the start value.
  - At wide aims the spine stops turning at 36 degrees (it takes 60% of the turn up to 60 degrees, nothing beyond). The head (up to 75 degrees) and the arms do the rest, as in his Blender pose of a 90 degree side aim.
- **Shooting backward is a backpedal, not a flip.** His words: "It gets corny when she has to shoot backward, she snaps back and forth. I imagine it is natural that she jogs backward while she shoots then."
  - A shot aimed outside the front angle turns her body toward the aim at a set rate (a half turn in 0.2 s); the shot does not wait for the turn.
  - She stays turned to the live aim, jogging backward along her path, for 0.5 s after the last shot; a new shot renews it.
  - Then she turns back to her run at the same rate. The hold also ends when the cursor comes back inside the front angle of her run direction, and a new move order ends it with the same smooth turn back. A dash or a skill ends it at once, since they face their own way.
  - The backward gait is his own backward jog, made on his rig.
- **Shot sounds:** gunshot 54 on the left-click shot, his choice. The others are mapped at random for the skills; he re-picks them by ear.

## Why

Kristian, 2026-11-18: "I wonder if we must make it so that we always shoot in the direction the mouse points. Won't it be easier to keep track of animations then?"

- **Lost Ark does it this way:** every attack, basic ones too, goes toward the cursor.
- **One aiming model for shots and skills.** The skills already aim at the cursor. Now the basic shot does too, so the player aims everything the same way.
- **The animation follows one aim direction.** There is no target to track, no walking into range, and no turning toward a target the cursor is not on.

## What it replaces

- **Attack-move on A, with its range ring.** The ring object, its component and its material are removed from the scene and the builder.
- **Target choice:** the attack-move click radius, the target nearest the click, the nearest target in range, and attack-move to the ground.
- **The attack-move and attack-target orders.** The only order left is a move.
- **Right-click on an enemy as an attack.**
- **Walking into range automatically.**
- **The basic attack range of 6 m,** which was sized for a shot that could not miss. It is now the shot's reach, 10 m.

The click-to-shoot decision from the same day stays.

## His decisions on the open choices (Kristian, 2026-11-18)

- Holding the left button does not repeat the shot.
- Right click on an enemy just moves.
- The shot does not pierce.
- There is no aim assist.
- "I think she can look toward the mouse at least": she looks toward the cursor while she is free. The arms stay down, and come up only for shots.

## Still open

- The range (10 m) and the half-width (0.5 m) are guesses.
- Whether a miss reads clearly enough as a miss at the game camera.
- Hold-to-fire, if he wants it later. It is about an hour's work: re-send the shot while the button is held, capped by the waiting rule.

## If the trial is confirmed

Pillar 1 in `docs/vision/pillars.md` says "attack-move on A". If the trial is confirmed, its wording changes to say that the basic shot and the skills are aimed at the cursor. This file does not edit the pillars.
