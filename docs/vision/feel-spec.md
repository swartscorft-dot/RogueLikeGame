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
| Move input | - | right-click, held click follows cursor | - | decided | Kristian 2026-11-16 |
| Attack-move | - | left-click at the cursor; A first shows the basic-attack range, left-click confirms. One click, one shot: she walks into range if she must, fires once and stands | - | decided | Kristian 2026-11-16; left-click alone ("smart cast") Kristian 2026-11-18; one shot per click: Kristian 2026-11-18: click to shoot, no auto-attack |
| Dash cooldown | s | 10 | 6-10 | documented | Lost Ark, Sharpshooter (ProGameGuides) |
| Dash distance | m | 6 | 4-8 | guess | own choice |
| Dash duration | s | 0.2 | 0.15-0.3 | guess | own choice |
| Dash invulnerability | s | 0.1 | 0.05-0.2 | guess | "a small i-frame", Kristian 2026-11-16 |
| Dash cancels a skill in progress | yes/no | yes (animation cancel) | - | decided | Kristian 2026-11-16 |
| Character faces cursor on cast | yes/no | yes | - | documented | Lost Ark (Fextralife) |
| Input buffer | s | 0.15 | 0.1-0.25 | guess | own choice |
| Basic shot: startup / active / recovery | s | 0.08 / 0.02 / 0.125 | - | guess | own choice at 0.15 / 0.05 / 0.25; halved by Kristian 2026-11-18: shoot more often. 0.225 s a shot, 4.4 shots/s |
| Basic shot lock | - | none, as in League: a move, a skill or a dash cancels it before it has gone off and takes over at once after; a shot that went off makes the next one wait the whole shot time | - | decided | Kristian 2026-11-17 |
| Right-click on an enemy | - | attacks it with one shot; right-click on the ground moves, and held it follows the cursor. Holding the button never repeats a shot | - | decided | Kristian 2026-11-17; one shot per click: Kristian 2026-11-18: click to shoot, no auto-attack |
| Basic attack repeat | - | none: every basic shot needs its own click. A click while a shot is still in its cycle waits and fires as the next shot the moment the cycle allows; one waits at most, and a newer click replaces it. A move order, a dash or a skill drops it | - | decided | Kristian 2026-11-18: click to shoot, no auto-attack |
| Basic attack range | m | 6 | 4-8 | guess | added in G2 by the agent, sized to what the camera shows |
| Attack-move target | - | the target nearest the click; nearest the character when none is near the click | - | decided | Kristian 2026-11-17 |
| Attack-move click radius | m | 2 | 1-4 | guess | added in G2 by the agent; how near the click a target must be to count as "near the click" |
| Shot front angle | degrees | 60 | 30-90 | guess | added 2026-11-18 by the agent: a basic shot at a target within this angle to either side of where the body faces turns only the upper body; beyond it the whole body turns (Kristian 2026-11-18) |
| Attack command while walking | - | a target in basic-attack range is shot once on the move; the move goes on. A click during the shot's cycle waits for the next shot, as standing | - | decided | Kristian 2026-11-18: "she slows down and shoots before she speeds up again"; one shot per command; click to shoot, no auto-attack |
| Shot on the move: speed | share of run speed | 0.5 | 0.3-0.8 | guess | added 2026-11-18 by the agent |
| Shot on the move: slow-down | s to ease down to that share | 0.15 | 0.1-0.3 | guess | added 2026-11-18 by the agent: smoothstep from the share it has when the shot starts (Kristian 2026-11-18: "the deceleration and the acceleration must be slower") |
| Shot on the move: back to full speed | s to ease back up | 0.5 | 0.25-1 | guess | 0.2 by the agent; 0.5 on 2026-11-18 with the easing. Smoothstep; starts when the shot has gone off and the slow-down is done. A dash or a skill ends the slow at once |
| Shot on the move: gait cross-fade | s between the run and the shooting gait | 0.15 | 0.1-0.3 | guess | added 2026-11-18 by the agent |
| Hit-stop on hit | s | 0.05 | 0-0.1 | guess | own choice; skill hits |
| Hit-stop on a basic shot hit | s | 0.025 | 0-0.05 | guess | added 2026-11-18 by the agent with the faster shot: 0.05 would be 22% of a 0.225 s shot |
| Back attack | - | 90 degree arc, +5% damage, +10% crit | - | documented | Lost Ark (VULKK) |
| Head attack | - | 90 degree arc, +20% damage, +10% stagger | - | documented | Lost Ark (VULKK) |
| Telegraph colours | - | red damage, yellow push, blue CC | - | documented | Lost Ark (VULKK) |
| Run length, target | min | 40-50 | - | decided | Kristian 2026-11-16 |
| Run length, first complete run | min | 15-20 | - | decided | Kristian 2026-11-16 |

Not in any source: camera values and Lost Ark's cancel rules. Kristian measures them from footage.
The agent searched for Lost Ark's camera pitch, field of view and distance on 2026-11-17, in English and Korean, and found no published or datamined numbers. The pitch and the field of view are still guesses.
Attack-move with a visible basic-attack range is a League-style addition on top of Lost Ark control. It implies a basic attack with a defined range.
