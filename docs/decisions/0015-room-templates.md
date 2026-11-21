# 0015 Rooms drawn by hand: templates joined at their doors

Status: **the direction is Kristian's: on 2026-11-21 he ticked "Rom-maler slik Diablo og PoE gjør" as the next thing to build, beside "Ganger mellom rommene". How the templates are written, chosen and joined, and the first library of eleven, are the producer's proposal (the research report d's proposal B, cut to a first order). Nothing of it is built. He has not played work order 051's carved rooms either.**

## What he said

On 2026-11-20, after his third session: "bedre bruk av mappet. nå har vi veldig klare firkanter per rom. et rom må ikke være en firkant, eller det kan godt være det i "backend" men det føles veldig unaturlig ut at alt er firkant, så igjen - her må vi ta litt mer inspo fra diablo+poe2 på hvordan deres genereatorer lager og designer levler. litt dungen crawler følelse trenger vi, ikke inn i store kontorlandskap med monstre i."

Earlier that day, about finding his way: "It is not quite intuitive where the chest rooms are; I end up double-checking several rooms for them. I need the run to be more linear", and asked how linear: fully.

On 2026-11-21, asked what to build next, he ticked "Rom-maler slik Diablo og PoE gjør" and "Ganger mellom rommene".

## What follows

1. **A room should look designed, not drawn by a rule.** Work order 051 carves each rectangle by one of six rules (a notch, a cross, an apse, alcoves, aisles, an octagon). Diablo 2 and Path of Exile build indoor levels room by room from rooms made by hand ("pockets of designed layout and set pieces that get randomly distributed, but it's still quite controlled", GGG). A hand-made room can combine things, set its doors where the fight wants them, and say where the shooters, the elite, the eye-catcher and the cover belong.
2. **The run stays one row.** A template is one room of the row with one door in and one out. Its other door marks are only options, and the ones not used are wall.
3. **Rules first.** A template that would leave a pocket, a slot to be stuck in or a shot-through corner must fail a test, never a run.

## The producer's proposal

| Choice | Proposal | Why |
|---|---|---|
| **Format** | A text grid in C#, one character per 4 m cell, north up, written as if she comes in from the bottom: `.` floor, `#` solid, `?` solid or floor (drawn), `I` where the door in may be, `O` where the door out may be, `h` shooters' perch, `e` the elite's place, `A` the eye-catcher at the far wall, `c` a cover prop. A name, a family, the rooms it is for, the floor it starts on, the themes it suits. | A person can read and edit it in any editor; the rules (plain C#, no engine) and the tests read the same text. Not an Inspector list or an asset: values saved in the scene would freeze the library, and agents may not make `.asset` files. |
| **Checked when** | An EditMode test checks every template in all eight orientations, with every `?` drawn both ways and every pair of door marks, against work order 051's rules (`RoomShapes.Faults`). At run time a template that fails the check is left out, and a room that still breaks a rule falls back to 051's carving. | "A pocket fails a test, not a run." Checking every case is cheap: eleven templates, about 1 100 cases. |
| **Rules** | Work order 051's, with two changes for a template room only. Up to 40 % of its cells may be solid (25 % for 051's shapes), but its open floor must be between today's smallest and largest combat room (25 to 49 cells). And its size is its own, not the plan's 5 to 7. | A cross hall is about 37 % solid. The open-area rule says better what the 25 % guarded, that a room plays no smaller. The open square of 12 m round the middle stays, so the boss, the lanes, the fog and the drops all keep a middle. |
| **Chosen how** | On a stream of its own ("templates"), before the layout: the elite room always takes an elite template and the exit room an exit template; three combat rooms in four take one. The template is drawn by weight from those its kind and floor allow, never one used already on the floor, and never the family of the combat room before it. | GGG's room types ("not a bunch of trap rooms one after another"). One combat room in four keeps 051's drawn shapes, against template fatigue. |
| **Theme** | The theme follows the template: a theme the template suits is 3 times as likely. | The themes are drawn after the layout, so a template cannot be chosen by its theme. The other way round costs no extra draw. |
| **Turned and mirrored** | Turned so that its bottom wall is the wall of the door in, and mirrored when drawn (if the mirror finds no place, the other is tried). | All eight orientations occur, and the design holds: the eye-catcher is always across from where she comes in, the perch always at the far end. |
| **Joined** | In the layout. A template gives its room its size. The room is put against the room before it only where the shared wall has a cell that is an `O` of the one and an `I` of the other, and the door is drawn among those cells. | This is Gungeon's and Path of Exile's join, kept to touching rooms. Fitting templates to the layout's rectangles afterwards would need a template for every size and would move doors; most rooms would fall back. |
| **One representation** | A template IS a shape (`RoomShape.Template`). Its `#` cells become the room's solid cells, the gate is added as in every carved room, and the carve stage stamps it in place of a drawn shape. 051's walls, kit, colliders, NavMesh, map, fog and fallbacks then follow unchanged. | Work order 051 built that seam for this. The order touches no file of the game's runtime. |
| **Marks** | The marked places are wishes, tried first: the shooters and casters try `h` first, the elite `e`, the altar, the throne and the mood's large-prop place `A`, and the floor props of a theme `c`. A place that breaks a rule is passed, and the old search follows. | It is how the themes already work ("the theme says what and suggests where"). Nothing ever stands against a rule. |
| **The first library** | Eleven templates. Combat (seven, five families): nave, nave with apse, corner (an L), long corner, twin chambers, colonnade, transept. Elite: the knight's apse, the cross hall. Exit: the throne nave, the throne arcade. All of them pass the rules (checked by the drafting agent's script against a copy of 051's rules, every door pair and every draw). | Enough that no two combat rooms in a row look alike, and small enough to write and test in one night. |

What each is like to fight in, in a line:
- **Nave** (20 by 36 m): she comes in at one end, the shooters stand at the far end, the swarm comes down the length, and the chapels' buttresses are cover.
- **Nave with apse**: the same hall, closed by an apse with its eye-catcher. The way on is a side door just before the apse, so the whole hall must be crossed.
- **Corner** (an L, arms 16 m wide): the shooters wait round the corner, out of the line from the door.
- **Long corner**: a short arm turns into a 32 m bar, a long field opening at once.
- **Twin chambers** (from floor 2): a near room and a far room joined by a 12 m waist, the place to hold or the trap.
- **Colonnade**: four great piers and four pillars round an open middle. She circles them and the shooters lose their line.
- **Transept** (from floor 2): she comes in at the middle of a long side with enemies at both ends, a crossfire.
- **The knight's apse** (elite): the elite waits in the mouth of the apse, before the altar.
- **The cross hall** (elite, from floor 3): the elite stands at the crossing, the shooters at the head arm, and the solid corners can be kited round.
- **The throne nave** and **the throne arcade** (exit): an aisled hall, or a round hall entered through a narrow slot, the throne at the far wall.

## Considered and not chosen

- **Templates for the entrance and the treasure rooms.** A 3 x 3 treasure room with its gate leaves nothing to design, and the chest was found once the run became one row. Its `$` mark comes with treasure templates later, after work order 055's changes to the drops.
- **A room round a solid core, or a raised middle.** Both need the room's middle elsewhere than its rectangle's (the boss, the lanes, the fog, the drops and the benchmark all stand at the middle), or a floor with height. That is a later order with a room's own "heart" point. A raised floor is also a Blender job.
- **The report's small connector templates.** They belong to the passages order (proposal C).
- **Free rotation regardless of the door.** It would put the apse beside the door she enters by.

## The passages order (proposal C) beside this one

- **Both on at once.** Both can be on: this order decides a room's shape, size and where its doors may be; C decides how far apart the rooms stand and the passage between a door-out mark and the next room's door-in mark.
- **Order of building.** This one first. It is rules only, and it answers his first complaint, "everything is square". It also makes the door marks that C's passages need for their ends.
- **What each leaves alone.** This order leaves the grid's size, the room kinds, the door signs, the map and the runtime alone, and keeps the join in one function C can widen. C must not carve or change a template room.
- **Not at the same time.** Both change `LevelGenerator.TryLine` and `PlaceChain`, so they are never built side by side.

## Open, for Kristian

1. Should every combat room be a template, or three in four with 051's shapes among them (the proposal)?
2. Templates change rooms' proportions: a nave is 20 by 36 m and a transept 36 by 20 m, where today's rooms are 20 to 28 m a side. Is that welcome?
3. Will he sketch rooms himself (on squared paper, one square per 4 m; the producer types them in), or leave the library to the producer?
4. Is a room round a solid core wanted soon? It is the next thing the format can't do.
