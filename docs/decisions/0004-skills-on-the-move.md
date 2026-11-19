# 0004 Skills on the move

Status: **confirmed by Kristian 2026-11-19, after he had played it: "veldig bra. fantastisk." (very good, fantastic).** He asked for it the same day. The rule and every value are the producer's; he approved the whole, tuned no value and ruled on none of the open points below. The version before it is the W A S D work of decision 0003, which was never committed by itself: both went into one commit. The old behaviour comes back in `FeelSettings.asset` with `SkillRecoveryFree` off and `SkillSlowdown`, `SkillSpeedRecovery` and `AimHold` at 0.

## What he said (Kristian, 2026-11-19, after playing W A S D)

- Casting skills does not feel as good as the rest with W A S D.
- There is a small delay before she can start shooting again after a skill.
- Stopping dead for a skill may be wrong, except for the long casts. He pictures a gradual slowdown to a full stop, depending on how long the cast or channel is.
- "If we get this to work smoothly we are onto something."

## Where the delay came from

Read from the code, not measured in play:

- **The skill's recovery locked the basic shot too.** After a skill's last hit she was locked for its recovery (0.2 to 0.5 s in `Skills.asset`). A click in that time only waited, and its shot then needed its own 0.08 s wind-up. So the first basic shot went off 0.28 to 0.58 s after the skill's last hit (Quick Shot 0.38 s, Heavy Volley 0.58 s). Between two basic shots the wait is 0.225 s.
- A skill hit that lands adds a 0.05 s hit stop on top.
- The arms came down when the skill ended and up again for the shot. That is a twitch, not a delay: the shot did not wait for it.
- A click that was already waiting when a skill started was dropped, by rule. This is unchanged.

## Decision

- **A skill commits her until its last hit.** The startup, the cast, the hold, the charge and the burst are locked as before, and only the dash cancels them (pillar 1).
- **The recovery after the last hit no longer holds back the basic shot or the move keys.**
  - A click that waited during the skill starts its shot at the last hit; a new click in the recovery shoots at once.
  - A move key held in the recovery moves her.
  - **The next skill still waits out the recovery,** so skills chain exactly as fast as before. A skill key pressed in the recovery goes through the input buffer, as before.
  - Switch: `SkillRecoveryFree` in `FeelSettings.asset`. Off gives the old lock back.
- **A skill pressed on the move slows her to a stop instead of stopping her dead.**
  - She slides on the way she was moving, and the speed eases down to zero. The move keys do not steer the slide: it is momentum, not control.
  - The slowdown is the same for every skill: `SkillSlowdown` seconds from full run speed to standing (0.3), less from a lower speed.
  - So the length of the skill decides how far it goes. A short skill (Quick Shot fires after 0.15 s) only dips her speed and she never stands. A long cast or a hold (Heavy Volley, Fan Fire) brings her to a full stop early and she stands for the rest.
  - She faces the cursor through it, as skills always have. Each hit is measured from where she is when it lands.
  - While she slides she shows the shooting gait, forward or backward, with the arms aimed at the skill's aim. The arms are aimed through every skill.
  - When she is let go (at the last hit, or at the end of the recovery with the switch off), the speed eases back up over `SkillSpeedRecovery` seconds from a stand (0.25), less from a higher speed. This is an exception to "starting at once" in decision 0003.
  - A skill that moves her itself (Backstep) does not slide. A dash ends the slide at once.
  - `SkillSlowdown` 0 gives the dead stop back.
- **The arms stay up for a moment after a shot or a skill:** `AimHold` seconds (0.3), aimed at the live cursor. Clicking slower than the fire rate no longer drops and raises the arms between shots, and a shot straight after a skill starts from raised arms. Kristian agreed to this on 2026-11-19.
- **The dash direction is a setting:** `DashDirection` in `InputBindings.asset`, toward the cursor (the start value, his answer in 0003) or toward the way she moves. Asked for by Kristian 2026-11-19.
  - Toward the way she moves, with no move key held, the dash goes the way she faces.

## Open: decided by the producer, not yet by Kristian

- That the recovery is free for the basic shot **and** for movement, and not for the next skill.
- That the slowdown is one rate for every skill, rather than a time set per skill or scaled by the cast.
- That the move keys do not steer the slide.
- That the long casts slide to a stop like the rest. His words leave room for the long casts to stop dead; if he wants that, it becomes a per-skill switch.
- Every value: `SkillSlowdown` 0.3, `SkillSpeedRecovery` 0.25, `AimHold` 0.3.
- The dash with no move key held going the way she faces.

Settled by the agent while building, 2026-11-19, and open in the same way:

- **Letting go of the move keys during a skill stops her at once when the skill lets her go.** The slide does not coast on. Quick Shot pressed on the run with the key released drops from about 3 m/s to a stand at its hit.
- **The ease back up follows every skill,** also one pressed while standing: walking off right after any skill takes up to 0.25 s to reach run speed. Backstep is the exception.
- **A skill key pressed early in a free recovery is dropped,** as it was in the old lock: only a press in the last 0.15 s (`InputBuffer`) before the recovery's end fires. She looks free in that time now, so a dropped press may read as a missed key. First thing to watch in play.
- **A dash ends a free recovery,** as a dash cancels a skill: after the dash a skill may start at once.
- **A Holding skill lets her go at the release** (or at its longest hold), a Charge skill at its shot.
- **When the arm hold starts, the arms go from the shot's aim to the live cursor without smoothing.** A small snap if the cursor moved during the shot.

## Found in review, not fixed

An independent review of the change on 2026-11-19 found one fault in the W A S D work, older than this decision:

- **A move key pressed during a standing shot's wind-up makes her start at full speed and then slow down.** The shot becomes a shot on the move and eases down from full speed, although she was standing: 0, then 6, then 3, then back to 6 m/s. It should ease from the speed she has. The slide in this decision reads her real speed; the shot on the move does not. No test checks it.

## Measured in tests (default values, run speed 6 m/s)

| | Old lock | Free recovery |
|---|---|---|
| Quick Shot: last hit to first basic shot | 0.38 s | 0.08 s |
| Powder Keg | 0.28 s | 0.09 s |
| Heavy Volley | 0.58 s | 0.08 s |

- Quick Shot from a run: slowest at 3.0 m/s at its hit, 0.73 m slid, back at run speed 0.27 s after the press.
- Heavy Volley from a run: standing 0.29 s after the press, 0.91 m slid, then still until its hit at 1.0 s.

## The pillar

Pillar 1 says every skill has a phase you are locked into. That holds: the lock now ends at the skill's last hit for shooting and moving, and at the end of the recovery for the next skill. The wording of the pillar is not changed.
