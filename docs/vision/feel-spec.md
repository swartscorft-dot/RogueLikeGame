# Feel spec

The table Kristian judges against and the agent tests against.
Status: guess -> tuned -> locked. Only Kristian changes a locked value.
Values marked "guess" are starting points to tune from. Double or halve; do not nudge.

| Parameter | Unit | Start value | Range | Status | Origin |
|---|---|---|---|---|---|
| Camera pitch from ground | degrees | 55 | 45-65 | guess | measure from Lost Ark footage |
| Camera distance | m | 14 | 10-18 | guess | measure from footage |
| Field of view | degrees | 30 | 20-40 | guess | measure from footage |
| Run speed | m/s | 6 | 4-8 | guess | own choice |
| Time to full speed | s | 0 | 0-0.1 | guess | own choice |
| Turn time | s | 0 | 0-0.1 | guess | own choice |
| Move input | - | right-click, held click follows cursor | - | decided | Kristian 2026-11-16 |
| Attack-move | - | A shows basic-attack range, left-click confirms | - | decided | Kristian 2026-11-16 |
| Dash cooldown | s | 10 | 6-10 | documented | Lost Ark, Sharpshooter (ProGameGuides) |
| Dash distance | m | 6 | 4-8 | guess | own choice |
| Dash duration | s | 0.2 | 0.15-0.3 | guess | own choice |
| Dash invulnerability | s | 0.1 | 0.05-0.2 | guess | "a small i-frame", Kristian 2026-11-16 |
| Dash cancels a skill in progress | yes/no | yes (animation cancel) | - | decided | Kristian 2026-11-16 |
| Character faces cursor on cast | yes/no | yes | - | documented | Lost Ark (Fextralife) |
| Input buffer | s | 0.15 | 0.1-0.25 | guess | own choice |
| Basic shot: startup / active / recovery | s | 0.15 / 0.05 / 0.25 | - | guess | own choice |
| Basic attack range | m | 6 | 4-8 | guess | added in G2 by the agent, sized to what the camera shows |
| Hit-stop on hit | s | 0.05 | 0-0.1 | guess | own choice |
| Back attack | - | 90 degree arc, +5% damage, +10% crit | - | documented | Lost Ark (VULKK) |
| Head attack | - | 90 degree arc, +20% damage, +10% stagger | - | documented | Lost Ark (VULKK) |
| Telegraph colours | - | red damage, yellow push, blue CC | - | documented | Lost Ark (VULKK) |
| Run length, target | min | 40-50 | - | decided | Kristian 2026-11-16 |
| Run length, first complete run | min | 15-20 | - | decided | Kristian 2026-11-16 |

Not in any source: camera values and Lost Ark's cancel rules. Kristian measures them from footage.
Attack-move with a visible basic-attack range is a League-style addition on top of Lost Ark control. It implies a basic attack with a defined range.
