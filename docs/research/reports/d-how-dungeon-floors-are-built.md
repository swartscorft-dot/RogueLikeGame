# Dungeon floors that feel like a crawl: how Diablo and Path of Exile build them, and three ways for this generator

Research report, 2026-11-20. Read-only on the project. Every outside claim has its source beside it. "Not confirmed" means I could not fetch a primary page that says it.

## 0. The generator today (read from the code)

- **Grid and plan.**
  - Cells are 4 m, on a 48 x 48 grid.
  - The floor is one row, E C C T L T C X (`LevelPlan.LineBetween`).
  - Sizes: entrance 4 x 4 cells (16 m), combat rooms 5 to 7 a side (20 to 28 m), treasure rooms 3 x 3 (12 m), exit room 8 x 8 (32 m).
- **Placement.**
  - Each room is put flush against the room before it.
  - The two share a wall, and a 1-cell door sits in it (the kit's arch leaves about 3.1 m clear).
  - There are no corridors (`PlaceChain`, `DoorBetween`).
- **Walls.**
  - Wherever two neighbouring cells have different owners and there is no door, there is a wall.
  - Walls are 3 m high, but only 1 m where a room lies to the north (the camera looks over them).
  - The grey boxes (`GeneratedLevel.BuildWalls`), the kit plan (`CryptKitPlan.Make`) and the text map (`LevelMap`) all ask one function, `LevelLayout.RoomAtCell`. That is the seam every proposal below uses.
- **Kit.** Straight 4 m walls, low walls, piers and posts at grid points, an arch, lanterns and floors. No kit piece has a collider; the grey boxes keep the colliders.
- **Camera.** From `FeelValues` (28 m, 55 degrees, 30 degree vertical field of view, 16:9) she sees about 27 x 19 m on the ground: about 11 m ahead to the north, 8 m to the south and 13 m to each side (my arithmetic). A 28 m room does not fit on one screen.

**Why it reads as offices.** Every room is a box, and the boxes touch. A door is a hole in a shared partition, the walls are long, straight and all one height, and nothing breaks them into a rhythm. Diablo 1's rooms are boxy too (below). What gives its levels a crawl's feel is scale, thresholds and rhythm.

## 1. How the games build a floor

### Diablo 1

Source: Boris the Brave's teardown of the released source, https://www.boristhebrave.com/2019/07/14/dungeon-generation-in-diablo-1/

- **Every stage.**
  - A 40 x 40 tile grid, with a separate generator per stage.
  - A "lockout" pass throws away any level that cannot be walked in full.
  - Small hand-made "minisets" place stairs and decoration.
  - One hand-made set piece per level holds a quest.
- **Cathedral.**
  - Up to three 10 x 10 rooms stand on a central axis, joined by a wide hall with a colonnade of arches.
  - Smaller rooms are "budded" off their sides, with sides of 2, 4 or 6 tiles.
  - Dividing walls are solid (50 %), arched (25 %) or barred arches (25 %).
  - The result: boxy rooms with many choke points at the doorways.
- **Catacombs.** Rooms of 4 to 9 tiles a side by recursive splitting, joined by hallways 1, 2 or 3 wide. The level is filled out to at least 700 floor tiles.
- **David Brevik.** He describes the generator in his GDC 2016 postmortem (https://pcgamer.com/diablo-designer-david-breviks-full-gdc-post-mortem-is-now-online). I did not watch it, so his words are not confirmed here.

### Diablo 2

- **What the data files show.** In `levels.txt`, `DrlgType` is "1 = Random Size, amount of rooms defined by LVLMAZE.TXT, 2 = pre set map, 3 = Random Area with preset size (wildernesses)" (Nefarius's modding guide: https://www.d2mods.info/forum/kb/viewarticle?a=232). The preset rooms are DS1 files, up to six versions each (https://www.d2mods.info/forum/kb/viewarticle?a=357). So a dungeon is a counted number of hand-made rooms joined into a maze.
- **Not confirmed:** how the maze chooses and joins them.

### Diablo 3

**Not confirmed from a primary page.**
- A fan wiki says D3 dungeons are fixed-size tiles, usually rooms, which the generator picks and attaches to one another. I saw this only as a search excerpt; the page refused fetching (https://www.diablowiki.net/Random).
- A BlizzCon 2016 talk by Ed Hanes covered pacing and fixed against procedural dungeons, but its write-up gives no detail (https://blizzpro.com/?p=69232).

### Diablo 4

Blizzard's quarterly update, March 2022 (https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022):
- **Tile-sets.** "We break it all down into what we call tile-sets." They are dressed with themes, props and lighting to make dungeons "varied, handcrafted and yet procedurally created", "keeping the randomized layouts".
- **Transitions.** Transition scenes "connect two different tile-sets together in the same dungeon".
- **Leading the eye.** "Our aim is to subtly lead you through the dungeon."

### Path of Exile 1 and 2

- **Two generators.** Dan_GGG, quoted on devtrackers from https://www.pathofexile.com/forum/view-thread/1998012 (https://devtrackers.gg/pathofexile/p/f1a21585-dan_ggg-explains-the-mystery-behind-square-cemetary-maps-and-insights-into-procedural-level-generation):
  - "In an indoor area such as The Prison, or The Crypt, we build the level room-by-room."
  - Outdoor areas are "defined by tile edges": nodes are joined by typed edges, and tiles that connect along those edges fill the gaps.
- **GGG's level design department** (https://pathofexile.com/forum/view-thread/1654235):
  - "pockets of designed layout and set pieces that get randomly distributed, but it's still quite controlled".
  - Rooms "can be rotated in any direction or even flipped".
  - Room types keep the generator from putting "a bunch of trap rooms one after another".
  - Balance is checked on a sheet of monster and chest counts "batch produced from running thousands of generated levels".
- **Rhys Abraham's ExileCon 2019 talk** (https://www.youtube.com/watch?v=EXnoHTqO7TE): no transcript I could fetch. **Not confirmed.**
- **Path of Exile 2, Act 3 (April 2025)** (https://pathofexile.com/forum/view-thread/3748698). After measuring clear times, GGG:
  - removed large dead ends, so players "take the wrong path" less;
  - turned Azak Bog from a large square into a rectangle with the boss opposite the start;
  - added a bottleneck near a start;
  - made an exit "clearer", with its checkpoint moved further in so it can be seen from a distance.
- **Path of Exile 2, patch 0.1.1** (https://www.pathofexile.com/forum/view-thread/3695606). Monsters no longer spawn close to a map's entrance.

### Enter the Gungeon

Boris's teardown: https://www.boristhebrave.com/2019/07/28/dungeon-generation-in-enter-the-gungeon/
- **Flows.** A floor starts from a hand-made "flow", a graph of room types with no positions (4 per stage in the Hollow, 8 in Gungeon Proper).
- **Rooms.** Each node gets a hand-made room. Stage 1 has nearly 300 candidates, never repeated.
- **Joining.** Every room lists its exits, and the next room is aligned "so its exit meets the existing one directly". A loop is closed by a pathfound "narrow corridor between 4 and 30 units long".
- **What the developers wanted.** A Zelda dungeon's feel: the layouts "just feel right".

### Hades

Ed Gorinstein (Supergiant), via Kotaku (https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545):
- **Chambers.** Hand-made chambers are recombined each run. Tartarus has "medium-sized chambers that are almost always completely walled in", smaller early in a biome and larger later.
- **Rejected.** "Huge rooms with doors scattered around the map": they did not fit the hand-painted art or the "high-speed room-to-room action pacing".

### Spelunky

Darius Kazemi: https://tinysubversions.com/spelunkyGen/ and https://tinysubversions.com/spelunkyGen2/
- **Grid and path.** The level is a 4 x 4 grid of rooms. A solution path is walked first, and each room on it is given a type that guarantees its exits.
- **Templates.** "Typically 8 to 16" templates per type, each 10 x 8 tiles. Some tiles are left to chance: a "2" is 50/50 solid or empty. Rooms are sometimes mirrored.

### The Binding of Isaac (Rebirth)

https://www.boristhebrave.com/2020/09/12/dungeon-generation-in-binding-of-isaac/
- **Grid.** A 9 x 8 grid of room cells.
- **Large rooms.** "11 large rooms: 2x2, 2x1, L-shaped, and narrow corridors, in various rotations" each cover several cells. A rectangle grid in the back end need not look like boxes on the screen.

### Unexplored and Darkest Dungeon

- **Unexplored** (https://www.boristhebrave.com/2021/04/10/dungeon-generation-in-unexplored/). Its levels grow from one loop of two arcs, with locks, keys and one-way valves. Dormans: "it is so much more interesting to generate small levels than it is to generate large ones." For a fully linear run it adds only loops inside a room.
- **Darkest Dungeon** (https://darkestdungeon.wiki.gg/wiki/Dungeon_map). Corridors of 1 to 8 segments join the rooms, and a segment can hold a battle, a curio, a trap or a secret room. The walk between rooms is content.

## 2. What makes a floor feel like a crawl

The numbers below are for this game's camera (about 27 x 19 m on screen), her run (6 m/s) and dash (6 m), aggro range (9 m, shooters 14 m), and the brute's Shockwave (a 9 m line).

1. **Narrow, then wide.**
   - Sources: Diablo 1's doorway choke points (Boris), and Totten's alternation of exposed "Prospect" space and enclosed "Refuge" (https://gamedeveloper.com/design/designing-better-levels-through-human-survival-instincts).
   - Here: a 4 m gate or passage, then a room of 20 m or more. One cell gives 3.5 m between wall faces and about 2.5 m of NavMesh.
   - Totten: in a narrow space "an enemy can literally become another wall of the space". A pack of five swarmers fills a 1-cell passage.
2. **Thresholds.** A gate one cell deep is the cheapest way to say "you are entering a place".
3. **Outlines that are not boxes.**
   - At a 4 m grain: notches, crosses, apses, alcoves and pillar aisles (Isaac's L rooms; Diablo 1's budding and colonnades).
   - Bauer: "a square room or straight hallway ... allows the player a clean shot", while uneven shapes "hide enemies in a compact space" (https://www.gamedeveloper.com/design/level-design-room-shapes).
   - For a gunslinger the trade is fewer clean lines and more angles.
4. **Broken sight lines and glimpses ahead.**
   - Every south wall is already low, so she sees toward the camera for free.
   - Glimpses northward need grates in full-height walls (Diablo 1's barred arches).
   - With 11 m visible to the north, a passage of 2 cells shows the next door; 3 or more hides it.
5. **Pockets, not dead ends.** PoE 2 removed dead ends that made players backtrack, and Kristian already double-checks rooms. So pockets are one cell deep and always fully on screen.
6. **Loops inside the room.** Gungeon builds its floors around "tight loops". In a linear run, the loop belongs in the room: a solid core she can circle while kiting, with 8 to 12 m all round it.
7. **The walk between fights.** Every 4 m cell is 0.67 s at a run. Seven passages of 3 cells add about 14 s a floor. That matters for feel, but it is small for length.
8. **Leading the eye.** Diablo 4's "subtly lead you", and PoE 2's clearer exits. Here: the way-out arch lit (DoorSigns already dims the way in), and denser lanterns on the way on.

## 3. Three proposals

**Map legend.** LevelMap's own legend, plus these:
- A walled cell with nothing inside is solid.
- A lone `+` inside a room is a free-standing pier, with a collider.
- `"` is a grate: it blocks walking and shots, but not sight.
- `P` is a passage.

The enemies and props on the maps are illustrative.

### A. The same rectangles, carved, with gates (smallest change; 1 to 2 days)

**The plan.**
- The chain and the rectangles stay as they are.
- A "shapes" stage, on its own stream between the layout and the contents, marks some cells solid. `RoomAtCell` returns null for those cells, so walls, kit and map follow by themselves.
- Each room draws one outline from its theme:
  - **notch or L**: 1 x 1, or 2 x 1, at one or two corners;
  - **cross**: four 1 x 1 corners, or 2 x 2 in the exit room;
  - **apse**: the two end cells of the far wall, leaving a recess where the shrine's altar and the throne already stand;
  - **alcoves**: alternate cells of a long wall with no door, as 4 m chapels for sarcophagi and shelves;
  - **stepped octagon**: in the exit room;
  - **pillar aisles**: free piers at grid points, one cell in from the long walls and 8 m apart.
- **A gate at every door.** The two cells beside the door's inner cell are filled, so she passes a 4 m slot before the room opens.
- **Bigger combat rooms.** 6 to 8 cells instead of 5 to 7, so the open area stays near today's.

**What must stay true** (new validator rules):
- The open cells of a room are 4-connected, and no two of them meet only at a corner.
- No more than 25 % of a combat room is solid, and an open 3 x 3 cell square (12 m) remains.
- The inner (w-2) x (h-2) cells stay open, except a one-cell core, only in rooms of 7 x 7 or more, with 2 cells clear all round it.
- No open cell has three walls round it, except an alcove.
- Distances to walls are measured to the mask, not the rectangle. This affects `DistanceToWalls`, `EnemyPlaceFault`, and the props' lane and wall stances.
- Every reflex corner and every free pier gets a box collider as wide as the drawn pier, so no shot slips past the corner.

**Corridors.** None; the gate is the threshold. The floor grows only by about a tenth.

```
proposal A, floor 1 (the rectangles of today, carved; a gate at every door)
                                  +-+-+-+
                                  |L4A  |
                              +-+-+     +-+-+
                              |        h    |
                              +   +     +   +-+-+-+
                              |             |T5   |
                              +             +     +
                              |      e         $  |
                        +-+-+-+-+ +     +   +     +
                        |T3   | |           |     |
                        +     +-+           +-+ +-+-+
                        |  $       p        |C6     |
                        +     +-+           +       +
                        |     | |           |       |
                      +-+-+ +-+-+-+-+-+-+-+-+       +-+-+
                      |C2       |           |    h      |
        +-+-+-+-+-+ +-+         +-+         +           +
        |C1       | |             |         |  B   p    |
+-+-+-+-+-+       +-+   +     +   +         +-+         +
|E0     | |      h  |      p      |           |         |
+       +-+         +             +           +         +
|              p       b       H  |           |         |
+       +-+         +   +     +   +         +-+-+-+-+ +-+
|  @    | |    B    |             |         |X7         |
+       + +-+       +-+         +-+         +           +
|       |   |       | |         |           |           |
+-+-+-+-+   +-+-+-+-+ +-+-+-+-+-+         +-+           +-+
                                          |          p    |
                                          +   +     +     +
                                          |    B          |
                                          +               +
                                          |               |
                                          +   +     +     +
                                          |        H      |
                                          +               +
                                          |        Y      |
                                          +-+           +-+
                                            |           |
                                            +-+-+-+-+-+-+
```

```
proposal A, floor 2 (another seed: the way on turns south, the near walls stay low)
                    +-+-+-+-+
                    |E0     |
                    +       +
                    |  @    |
                    +       +
                    |       |
                    +       + +-+-+-+-+-+
                    |       | |C2       |
                  +-+-+-+-+ +-+         +-+
                  |C1   |     |           |
                +-+ +-+-+     +           +
                |             |           |
                +             +           +-+-+-+
                |  B p             p      |T3   |
                +             +           +     +
                |          h  |              $  |
                +-+         +-+         +-+     +
                  |         | |        H| |     |
  +-+-+-+-+-+-+   +-+-+-+-+-+ +-+-+-+-+-+-+-+ +-+
  |X7         |                     |L4       |
  +           +-+                 +-+         +-+
  |    Y        |                 |             |
+-+             +-+-+-+-+-+       +             +
|      H        |C6       |       |             |
+   +       +   +         +-+-+-+-+     +-+     +
|               |           |T5   |     | |  p  |
+               +   +   +   +     +     +-+     +
|  p                           $                |
+   +       +   +           +     +             +
|          B    |  h        |     |        e    |
+-+             +           +-+-+-+-+         +-+
  |             |      B    |       |h        |
  +           +-+-+       +-+       +-+-+-+-+-+
  |           |   |       |
  +-+-+-+-+-+-+   +-+-+-+-+
```
(In A2 the elite room L4 has a solid core to circle.)

**What it costs.**
- `LevelRoom` gets a set of solid cells.
- A `LevelValues.Shapes` switch: off gives version 10's floors.
- `Version` goes to 11, and every golden map is re-pasted (the notches show by themselves).
- The floor boxes become one box per run of open cells, so no floor island lies under a solid cell for NavMesh sampling to snap to.
- The piers get colliders.
- No new kit piece: about 8 to 16 more wall sections and 4 to 8 piers a room.
- `FloorMapRules` must draw outlines from the mask.
- The props' wall stances must try the new inner faces.
- The door signs need no change.
- The benchmark is re-run.

**What could go wrong, and how to test it without eyes.**
1. **She snags while dashing.** Over 2000 seeds: a NavMesh path from the door in to every open cell and every enemy point, and a dash raycast from every cell centre along both axes.
2. **A shot slips past a corner.** At every reflex corner, cast 32 rays offset 0.05 m past it. Assert a Cover hit exactly when the segment crosses a solid or foreign cell.
3. **Rooms play smaller.** The pilots' time and deaths per room, shapes on against off.
4. **Sameness.** The share of each outline over 2000 floors, GGG-style; aim for no combat room whose open cells still form a rectangle.

### B. Hand-described room templates joined at exits (the Diablo and Path of Exile way; about 3 days on top of A's seam)

**The plan.**
- **The row stays as the flow** (as Gungeon's flow is a hand-made graph).
- **Each node takes a template of its kind.** A template is a text grid in C#, one character per 4 m cell, like Spelunky's templates:
  - `.` floor, `#` solid, `+` a pier;
  - `?` solid half the time (Spelunky's "2");
  - `N`, `E`, `S` and `W` mark exit cells;
  - `a` marks an alcove prop spot, `h` a shooter perch, `c` cells kept clear.
- **Orientations.** Every template is used in its 8 rotations and mirrors (PoE: rooms "rotated ... or even flipped").
- **Connectors.** Small templates join the rooms: a straight 1 x 2 or 1 x 3, an L-bend, or a 2 x 2 landing (Diablo 4's transition scenes, small).
- **Joining.** The new piece is aligned so its exit cell meets the open exit (Gungeon), with the current backtracking. No two combat templates in a row come from the same family (GGG's room types).
- **A first library.** 14 combat, 3 elite, 2 exit, 3 treasure and 4 connectors; half a day to write. Spelunky lived on 8 to 16 per type.

**Fights.**
- A test checks every template in every orientation against A's rules once, and the generator trusts the result.
- Perches, clear zones and cores are authored, not drawn.

**Corridors.**
- The connectors: 1 cell wide, 1 to 3 cells long, at most one bend.
- No fights, except an optional pack of 3 swarmers on a landing, one floor in three.
- About 40 to 80 m more a floor.

```
proposal B, floor 1 (templates: nave, cross, vault, apse hall, gallery, throne; P a connector)
                                          +-+-+
                                          |L4 |
                                      +-+-+   +-+-+
                                      |           |
                              +-+-+   +           +
                              |T3 |   |      e   h|
                              +   +-+-+   +   +   +
                              |    P              |
                              + +-+-+-+           +-+-+-+
                              |P|     |            P T5 |
                              + +     +   +   +   +-+   +
                              |C2     |  p        | |  $|
                            +-+ +-+   +           + +-+ +
                            |  H  |   |           |   |P|
                          +-+     +-+ +-+-+-+-+-+-+   + +
                          |         |                 | |
                          +         +             +-+-+ +-+-+
                          |p        |             |C6       |
                  +-+     +         +           +-+         +-+
                  |C1     |        b|           |    p        |
+-+-+-+-+   +-+-+-+ +-+-+-+-+-+ +-+-+           +-+         +-+
|E0     |   |      p      |   | |                 |      h  |
+       +-+-+   +   +     +-+-+ +                 +-+-+ +-+-+
|        P               h P    |                     |P|
+       +-+-+   +   +     +-+-+-+                     + +
|  @    |   |          B  |                           | |
+       +   +-+-+-+ +-+-+-+                       +-+-+ +-+-+-+-+-+
|       |         | |                             |X7             |
+-+-+-+-+         +-+                             +               +
                                                  |            H  |
                                                  +     +   +     +
                                                  |               |
                                                  +               +
                                                  |  B            |
                                                  +     +   +     +
                                                  |               |
                                                  +               +
                                                  |               |
                                                  +-+-+       +-+-+
                                                      |  Y    |
                                                      +-+-+-+-+
```

```
proposal B, floor 2 (other templates and turns: crossing, L hall, ring round a solid core)
                    +-+-+-+-+
                    |E0     |
                    +       +
                    |       |
                    +       +
                    |  @    |
                    +       +
                    |       |
                    +-+ +-+-+
                      |P|
                      + +                       +-+-+-+-+
                      | |                       |X7     |
              +-+-+-+-+ +-+-+                 +-+       +-+
              |C1        h  |                 |      Y    |
              +             +                 +           +
              |  p          |                 |    H      |
  +-+-+-+-+   +   +-+-+-+-+-+ +-+       +-+   +   +   +   +
  |C2     |   |   |           |C6       | |   |           |
+-+       +   +   +           + +-+-+-+-+ +   +           +
|  b   p  |   |B  |           |      h    |   |           |
+   +-+   +-+-+   +           +           +-+-+           +
|   | |    P      |           |            P              |
+   +-+   +-+-+   +           +           +-+-+   +   +   +
|H        |   |   |           |  p     B  |   |           |
+       +-+   +-+-+-+-+       +           +   +           +
|       |     |L4     |       |           |   |        B  |
+-+-+ +-+   +-+       +-+     +-+ +-+-+-+-+   +-+       +-+
    |P|     |    p      |       |P|             |       |
  +-+ +-+-+-+ +     +   + +-+-+ + +             +-+-+-+-+
  |T3$   P             h| |T5 | | |
  +-+-+-+-+-+           +-+   +-+ +
            |      e     P   $    |
            + +     +   +-+   +-+-+
            |           | |   |
            +-+         + +-+-+
              |         |
              +-+-+ +-+-+
                  | |
                  +-+
```

**What it costs.**
- Everything A costs, plus a parser, the orientations, exit matching and the library.
- The plan's sizes become template lists, and the validator's size rules become "a template of its kind".
- The themes can name the template's spots, so an alcove is a sarcophagus spot.
- A 45 degree wall piece would soften the look most. It is a Blender job: leave it until A has been played.

**What could go wrong.**
- **Template fatigue.** Fans feared D3's small tile pool would bring "sameness" (fan wiki above). Use `?` cells and the themes, and count repeats per act.
- **Placement failures.** Fixed shapes fail to place more often. Log attempts per seed over 2000 seeds, as `LevelCramped` does.
- **Tests.** A's tests, run once per template orientation: complete and cheap.

### C. Rooms apart, chapels budded on, passages between (Diablo 1's cathedral at this scale; 2 to 3 days)

**The plan.**
- **Spacing.** The rooms of the row stand 2 to 4 cells apart.
- **Passages.** Each pair is joined by a passage, found on the cell grid with at most one bend and kept one cell from other rooms (Gungeon's pathfound corridor).
- **Chapels.** Each combat, elite and exit room gets 1 to 3 buds of 1 x 1 to 2 x 2 cells on walls with no door, fully open to the room (Diablo 1's budding). In the back end a room is a list of rectangles.
- **Grates.** Where a passage runs along a full-height east or west wall of the next room, one or two wall cells become grates. She sees the room and its shooters before the door (Diablo 1's barred arches).

**Corridors.**
- **Size.** 1 cell (4 m) wide, 2 to 5 cells (8 to 20 m) long, at most one bend, no straight run over 3 cells.
- **Who may stand in one.**
  - Never a brute or a shooter: the Shockwave's 9 m line cannot be sidestepped in a 3.5 m slot.
  - A 2 x 2 landing may hold 3 swarmers (Darkest Dungeon's hallway battles), at most once a floor.
- **Pace.** Passages longer than 2 cells hide the next door and keep its room asleep (aggro 9 m). They add 80 to 120 m, about 15 to 20 s a floor.

```
proposal C, floor 1 (rooms apart; P passages one cell wide, bent once; " a grate she sees through)
                              +-+-+-+       +-+-+-+-+-+-+-+
                              |T3   |       |L4    A      |
                              +     +-+-+-+-+             + +-+-+-+
                              |  $   P                 h  | |T5   |
                              +     +-+-+-+-+ +     +     +-+     +
                              |     |       |              P   $  |
                              +-+ +-+       +             +-+     +
                                |P|         |      e      | |     |
                                + +         + +     +     + +-+ +-+
                                | |         |             |   |P|
                +-+-+         +-+ +-+-+-+-+ +             +   + +
                |C1 |         |C2         | |             |   | |
            +-+-+   +-+-+   +-+           + +-+-+-+ +-+-+-+   + +
            |           |   |P         H  |       | |         |p|
+-+-+-+-+   +           +   + +           +       +-+         + +
|E0     |   |        h  |   | "           |                   | |
+       +-+-+   +   +   +   + +           +             +-+-+-+ +-+-+
|        P              |   | "    p      |             |C6         |
+       +-+-+           +-+-+ +           +-+           +           +
|  @    |   |      B       p  |             |           |        h  |
+       +   +           +-+-+-+           +-+           +           +
|       |   |           |     |  b        |             |           |
+-+-+-+-+   +-+-+-+-+   +     +-+-+-+-+-+-+             +           +
                    |   |                               |    B      |
                    +-+-+             +-+-+-+-+-+-+-+   +           +
                                      |X7           |   |           |
                                    +-+             +-+-+ +-+-+-+-+-+
                                    |            B   P    |
                                    +     +   +     +-+-+-+
                                    |               |
                                    +               +
                                    |    H          |
                                    +     +   +     +
                                    |               |
                                    +-+           +-+
                                      |      Y    |
                                      +-+-+-+-+-+-+
```

```
proposal C, floor 2 (the way on turns south; a landing in the long passage holds a pack)
                      +-+-+-+-+
                      |E0     |
                      +       +
                      |  @    |
                      +       +
                      |       |
                      +       +
                      |       |
                      +-+-+ +-+
                          |P|
  +-+-+-+-+-+-+           + +
  |X7  Y      |           | |
+-+           +-+ +-+-+-+-+ +-+-+       +-+-+-+-+-+-+
|    H          | |C1           |       |C2         |
+     +   +     + +             +       +           +
|               | |          h  |       |        H  |
+               + +   +   +     +-+-+-+-+           +-+-+-+
|               | |  p             P                |T3   |
+               + +             +-+-+-+-+           +     +
|          B    | |             |       |  p           $  |
+-+             + +             +       +           +     +
  |             | |      B      |       |           |     |
  +-+-+-+-+-+-+ + +-+-+-+ +-+-+-+       +           +-+ +-+
              |P|       | |             |           | |P|
              + +       +-+             +-+-+   +-+-+-+ +
              | |                           |   |   |   |
              + +-+                         +-+-+ +-+ +-+
              |   |                               |  p|
              +-+ + +-+-+                         +   +
                | | |C6 |                         |   |
            +-+-+ +-+   +             +-+-+-+-+-+-+ +-+
            |           |             |L4           |
            +           +             +             +
            |  h        |             |  h          |
            +           +     +-+-+-+ +             +
            |        B  |     |T5   | |             |
            +           +-+-+-+     +-+   +     +   +
            |            P       $   P       e      |
            +           +-+-+-+     +-+             +
            |           |     |     | |             |
            +-+       +-+     +-+-+-+ +   +     +   +
              |       |               |          p  |
              +-+-+-+-+               +             +
                                      |             |
                                      +-+-+-+ +-+-+-+
                                            | |
                                            +-+
```

**What it costs.**
- A's mask, a `Passage` room kind and a passage stage.
- `GridCells` from 48 to 64.
- Door signs must light from the passage, not only the room before it.
- The corner map reveals a passage with the room it leads from.
- The benchmark and the pilots' navigation are re-measured.

**What could go wrong.**
- **Doorway cheese.** She stands in a slot and shoots a funnelling crowd (Diablo 1's doorway trick). Compare her damage taken in passages and in rooms.
- **Jams.** Swarmers or an elite jam in a bend. Log any agent blocked for more than 2 s.
- **Long and empty.** Longer floors that are no richer. Measure the time between fights with the recorder.

## 4. Which first, and the playtest

**Build A first, as the seam:** the solid-cell mask in `RoomAtCell`, mask-aware distances, floor boxes per run of open cells, pier colliders and the switch.
- It answers "everything is a square" directly.
- It leaves placement untouched.
- B and C both stand on it.
- **Then:** C's passages, if floors still feel short or flat (a switch on top of A). B is the destination if he likes the shapes, because authored rooms carry themes and fights better than rules can.

**Playtest.** The same seed with shapes off, then on, with a key that redraws it with the switch flipped. Ask him:
1. Office or crypt?
2. Did the gate make the room open up?
3. Did anything stick, or a shot pass a corner?
4. Was any room too cramped to kite?
5. Was the way on ever unclear?

Alongside: the recorder's floor time, and the pilots' deaths and time per room.

## 5. Open questions for Kristian

1. **"A little short" means:**
   - (a) too few fights: a fifth combat room;
   - (b) too little walk between them: passages;
   - (c) both;
   - (d) the rooms feel small.
2. **Between rooms:**
   - (a) gates only, rooms touching;
   - (b) short empty passages, 8 to 16 m;
   - (c) passages where a small pack sometimes waits.
3. **How far from the box:**
   - (a) mild, notches and gates;
   - (b) strong in every room;
   - (c) strong in the elite and exit rooms, mild elsewhere.
4. **Pockets beside the way:**
   - (a) none;
   - (b) one-cell alcoves with props only;
   - (c) alcoves that sometimes hold a small find, always visible from the room.
5. **Hand-made room templates (B):**
   - (a) yes, soon, the producer writes them;
   - (b) yes, and he sketches a few first;
   - (c) not until A and the passages have been played.

## Not confirmed

- How Diablo 2's maze joins its preset rooms.
- Diablo 3's tiles (fan sources only).
- The content of Rhys Abraham's ExileCon talk and of Brevik's GDC talk (videos I did not watch).
- Anything GGG has said about Path of Exile 2's generator beyond the area and patch notes above.
- Corridor widths in metres: no source gives them. Every metre figure here is mine, from this game's camera, speeds and grid.
