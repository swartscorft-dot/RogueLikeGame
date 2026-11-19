# 0003 W A S D movement

Status: **confirmed by Kristian 2026-11-19, after he had played it: "this feels very good". "W A S D for now."** Decided by him 2026-11-18. The version before it is commit `2094a67` on `art/grip`.

He added on 2026-11-19, not built yet:

- The movement scheme should be a setting the player can choose. W A S D is the one we build on for now.
- The dash direction should be a setting too: toward the way she moves, or toward the mouse.
- Casting skills does not feel as good as the rest yet: there is a small delay before she can shoot again after a skill, and stopping dead for a skill may be wrong except for the long casts. He pictures a gradual slowdown to a full stop, depending on how long the cast or channel is. This gets its own decision.

## Decision

- **W A S D move her, relative to the screen.** W is up the screen (away from the camera along the ground), S down, A left, D right.
  - A diagonal is as fast as straight.
  - She moves at run speed while a key is held and stands when none is, starting and stopping at once. (Decision 0004 adds one exception: right after a skill the speed eases back up.)
  - She stays on the NavMesh and slides along its edges.
  - Movement is a direction the input gives every frame, not an order with a destination.
- **The skills move to Q 1 2 3 4 R F and the right mouse button,** in their old order: Q Quick Shot, 1 Powder Keg, 2 Fan Fire, 3 Long Shot, 4 Scatter Blast, R Heavy Volley, F Backstep, right click Triple Tap.
  - The bindings are in `Assets/Game/Settings/InputBindings.asset`, which he can edit: keyboard keys or mouse buttons per slot, plus the move keys, the shoot button, dash and focus.
  - The skill bar and the help line show what is there.
- **Unchanged:** left click shoots at the cursor (decision 0002); Space dashes toward the cursor; Z spends the focus gauge.
- **Facing:** she faces the way she moves, except while the shot-turn hold keeps her turned to her aim.
  - "Her run direction" in the shot rules (front angle, shot facing, forward or backward gait) is the direction she is moving this frame.
  - Standing, she keeps her facing and looks toward the cursor as before.
- **Skills keep their commitment** (pillar 1). While a skill locks her, move keys do nothing; when the lock ends, a key still held moves her at once.

## Why

Kristian, 2026-11-18: "I feel that WASD movement would be nice. Then Q-1-2-3-4-R-F and right click are abilities."

## His answers (Kristian, 2026-11-18)

- The dash (Space) goes **always toward the mouse**, as before.
- The skills keep their order on the new keys, as listed above.
- Commit first: done (`2094a67`).

## What it replaces

- **Right-click to move**, with move orders, destinations and arrival.
- **The old skill keys:** Q W E R S D F G. E and G are free now; W, S and D are movement.
- **The League rule for the basic shot.** A move order used to cancel a standing shot before it went off and drop a waiting click. Now movement never cancels or drops a shot:
  - a move key in a standing shot's wind-up makes it a shot on the move;
  - one in its recovery moves her at once.

  A dash or a skill still cancels a shot that has not gone off, and drops a waiting click.

## Open: decided by the producer, not yet by Kristian

- **Movement never cancels a shot,** as above.
- **The backpedal speed cap.** While she is held turned away from her movement by a shot, she moves at no more than half run speed (3 m/s, the speed the `v2_30` backward jog was built for). It eases in and out with the moving-shot curve. Tunable in `FeelSettings.asset` (`BackpedalSpeedShare`, 0.5).
- **Focus stays on Z.**

## The pillar

Pillar 1 in `docs/vision/pillars.md` said "Right-click to move". With the trial confirmed, its wording was changed on 2026-11-19 to W A S D movement. Its point about a scarce dash and committed skills is untouched.
