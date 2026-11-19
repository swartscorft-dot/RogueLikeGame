# Feel spec

The table Kristian judges against and the agent tests against.
Status: guess -> tuned -> locked. Only Kristian changes a locked value.
Values marked "guess" are starting points to tune from. Double or halve; do not nudge.

| Parameter | Unit | Start value | Range | Status | Origin |
|---|---|---|---|---|---|
| Camera pitch from ground | degrees | 55 | 45-65 | guess | measure from Lost Ark footage |
| Camera distance | m | 28 | 20-36 | guess | 14 was too close (Kristian 2026-11-17). Doubled after one rough measurement: in a Lost Ark screenshot the character is about 10% of the screen height; at 14 m ours was 21%. Kristian on 28: "much better" |
| Field of view | degrees | 30 | 20-40 | guess | measure from footage |
| Run speed | m/s | 6 | 4-8 | guess | own choice |
| Time to full speed | s | 0 | 0-0.1 | guess | own choice |
| Turn time | s | 0 | 0-0.1 | guess | own choice |
| Move input | - | W A S D, relative to the screen (W up the screen, away from the camera); a diagonal is as fast as straight; she moves while a key is held and stands when none is; move keys do nothing while a skill locks her | - | decided | Kristian 2026-11-16 (right-click); W A S D: Kristian 2026-11-18, decision 0003, confirmed by him 2026-11-19 after playing. Right after a skill the speed eases back up instead (decision 0004) |
| Basic shot input | - | left-click fires one basic shot toward the cursor: a straight line from her toward the cursor's ground point, aimed at the live cursor until it goes off. It hits the first living target the line touches, only that one (it does not pierce); with nothing in the line it is a miss that still fires and costs its shot time. A click never moves her: standing she shoots from where she stands. No aim assist | - | decided | Kristian 2026-11-18, decision 0002 (replaces attack-move on A, target choice and right-click attack); confirmed in effect 2026-11-19, when he confirmed W A S D, which is built on it |
| Dash cooldown | s | 10 | 6-10 | documented | Lost Ark, Sharpshooter (ProGameGuides) |
| Dash distance | m | 6 | 4-8 | guess | own choice |
| Dash duration | s | 0.2 | 0.15-0.3 | guess | own choice |
| Dash invulnerability | s | 0.1 | 0.05-0.2 | guess | "a small i-frame", Kristian 2026-11-16 |
| Dash cancels a skill in progress | yes/no | yes (animation cancel) | - | decided | Kristian 2026-11-16 |
| Character faces cursor on cast | yes/no | yes | - | documented | Lost Ark (Fextralife) |
| Input buffer | s | 0.15 | 0.1-0.25 | guess | own choice |
| Basic shot: startup / active / recovery | s | 0.08 / 0.02 / 0.125 | - | guess | own choice at 0.15 / 0.05 / 0.25; halved by Kristian 2026-11-18: shoot more often. 0.225 s a shot, 4.4 shots/s |
| Basic shot lock | - | none. A skill or a dash cancels it before it has gone off; movement never does (a move key in the wind-up makes it a shot on the move, one after it has gone off moves her at once); a shot that went off makes the next one wait the whole shot time | - | decided (movement part open) | Kristian 2026-11-17 (League rule); retired for movement in decision 0003, 2026-11-18, by the producer, not yet ruled on by Kristian |
| Skill keys | - | Q 1 2 3 4 R F and the right mouse button, in slot order (Quick Shot, Powder Keg, Fan Fire, Long Shot, Scatter Blast, Heavy Volley, Backstep, Triple Tap); in InputBindings.asset | - | decided | Kristian 2026-11-18, decision 0003, confirmed 2026-11-19 |
| Basic attack repeat | - | none: every basic shot needs its own left click, and holding the button never repeats it. A click while a shot is still in its cycle waits and fires as the next shot the moment the cycle allows, at the live cursor; one waits at most. A dash or a skill drops it; movement does not | - | decided (movement part open) | Kristian 2026-11-18: click to shoot, no auto-attack; holding does not repeat confirmed 2026-11-18 (0002). That movement does not drop the waiting click is the producer's choice in 0003, not yet ruled on by Kristian |
| Basic shot range | m | 10 | 6-14 | guess | 6 in G2, when the shot could not miss; 10 with the cursor aim (decision 0002, 2026-11-18) |
| Basic shot half-width | m | 0.5 | 0.3-0.8 | guess | added 2026-11-18 with the cursor aim: generous on purpose, for 4.4 shots a second at small targets from a 28 m camera |
| Shot front angle | degrees | 120 | 60-150 | guess | 60 by the agent 2026-11-18: a basic shot aimed within this angle to either side of where the body faces turns only the upper body; beyond it the whole body turns. Kristian 2026-11-18: set to 120 in play, works fine |
| Shot turn time | s for a half turn | 0.2 | 0.1-0.4 | guess | added 2026-11-18 by the agent: how fast the body turns to a shot aimed outside the front angle, and back to the run after; the shot does not wait for it (decision 0002 trial) |
| Shot turn hold | s after the last shot | 0.5 | 0.25-1 | guess | added 2026-11-18 by the agent: she stays turned to the aim, backpedalling, this long after a shot that turned her; a new shot renews it (Kristian 2026-11-18: "natural that she jogs backward while she shoots") |
| Backpedal speed cap | share of run speed | 0.5 | 0.3-0.8 | guess | added 2026-11-18 by the producer (0003): while held turned away from her movement by a shot she moves at no more than this (3 m/s, the speed the v2_30 backward jog was built for); eases in and out with the moving-shot curve |
| A click while moving | - | always a shot on the move toward the cursor; she keeps moving the held way, slowed while the shot winds up. A click during the shot's cycle waits for the next shot, as standing | - | decided | Kristian 2026-11-18: "she slows down and shoots before she speeds up again"; click to shoot; moving means a move key is held (0003) |
| Shot on the move: speed | share of run speed | 0.5 | 0.3-0.8 | guess | added 2026-11-18 by the agent |
| Shot on the move: slow-down | s to ease down to that share | 0.15 | 0.1-0.3 | guess | added 2026-11-18 by the agent: smoothstep from the share it has when the shot starts (Kristian 2026-11-18: "the deceleration and the acceleration must be slower") |
| Shot on the move: back to full speed | s to ease back up | 0.5 | 0.25-1 | guess | 0.2 by the agent; 0.5 on 2026-11-18 with the easing. Smoothstep; starts when the shot has gone off and the slow-down is done. A dash or a skill ends the slow at once |
| Shot on the move: gait cross-fade | s between the run and the shooting gait | 0.15 | 0.1-0.3 | guess | added 2026-11-18 by the agent |
| Skill recovery free | yes/no | yes: after a skill's last hit the basic shot and the move keys are free; the next skill still waits until the recovery would have ended (a press in that time waits in the input buffer and starts only if it is no older than the buffer when the recovery ends; an older one is dropped). No gives the old lock through the recovery | - | guess | added 2026-11-19 by the producer, decision 0004; played by Kristian the same day, who approved the whole and tuned no value |
| Skill slowdown | s from full run speed to a stand | 0.3 | 0.15-0.6 | guess | added 2026-11-19 by the producer, decision 0004: a skill pressed on the move slides her to a stand along the way she was moving, from the speed she had (less time from a lower speed), smoothstep; 0 is the old dead stop. Not for a skill that moves her itself (Backstep) |
| Skill speed recovery | s from a stand back to full speed | 0.25 | 0.1-0.5 | guess | added 2026-11-19 by the producer, decision 0004: when a skill lets her go, the speed she may move at eases back up from what she has (less time from a higher speed), smoothstep |
| Aim hold | s | 0.3 | 0.15-0.6 | guess | added 2026-11-19 by the producer, decision 0004: the arms stay aimed at the live cursor this long after a shot or a skill ends; standing, the body keeps the Shoot stance. A dash, or the cursor leaving the shot front angle, ends it |
| Hit-stop on hit | s | 0.05 | 0-0.1 | guess | own choice; skill hits |
| Hit-stop on a basic shot hit | s | 0.025 | 0-0.05 | guess | added 2026-11-18 by the agent with the faster shot: 0.05 would be 22% of a 0.225 s shot |
| Back attack | - | 90 degree arc, +5% damage, +10% crit | - | documented | Lost Ark (VULKK) |
| Head attack | - | 90 degree arc, +20% damage, +10% stagger | - | documented | Lost Ark (VULKK) |
| Telegraph colours | - | red damage, yellow push, blue CC | - | documented | Lost Ark (VULKK) |
| Run length, target | min | 40-50 | - | decided | Kristian 2026-11-16 |
| Run length, first complete run | min | 15-20 | - | decided | Kristian 2026-11-16 |

Not in any source: camera values and Lost Ark's cancel rules. Kristian measures them from footage.
The agent searched for Lost Ark's camera pitch, field of view and distance on 2026-11-17, in English and Korean, and found no published or datamined numbers. The pitch and the field of view are still guesses.
Attack-move with a visible basic-attack range was a League-style addition on top of Lost Ark control. Decision 0002 (2026-11-18, confirmed in effect 2026-11-19) replaces it: the basic shot goes toward the cursor, as every attack does in Lost Ark and as the skills already did.
