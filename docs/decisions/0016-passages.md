# 0016 Passages between the rooms

Status: **Kristian chose the direction on 2026-11-21: he ticked "Ganger mellom rommene" and "Rom-maler slik Diablo og PoE gjør". What follows is the producer's proposal for the first order of the passages (proposal C of the research report `docs/research/reports/d-how-dungeon-floors-are-built.md`). He has not played it, and he has not yet played the carved rooms of work order 051 either.**

## What he said

After his third session, 2026-11-20: "bedre bruk av mappet. nå har vi veldig klare firkanter per rom. et rom må ikke være en firkant, eller det kan godt være det i "backend" men det føles veldig unaturlig ut at alt er firkant, så igjen - her må vi ta litt mer inspo fra diablo+poe2 på hvordan deres genereatorer lager og designer levler. litt dungen crawler følelse trenger vi, ikke inn i store kontorlandskap med monstre i."

Earlier that day, about finding his way: "It is not quite intuitive where the chest rooms are; I end up double-checking several rooms for them. I need the run to be more linear." Asked how linear: fully.

On 2026-11-21, asked what to build next, he ticked two things: passages between the rooms, and room templates the way Diablo and Path of Exile make them.

## What follows

1. **The rooms stop touching.** Today each room is put flush against the room before it, and a door is a hole in the wall they share. That is what makes a floor read as one block of offices. The rooms should stand apart, with walls of their own, and a passage should join each room to the next.
2. **The row stays a row.** Fully linear still holds: every room has one way in and one way out, a passage never branches, and nothing hangs off the side of the way that he would have to look into.
3. **What lies between the rooms is a place of its own,** narrow and dark. The room after it opens wide: narrow first, then wide.

## The producer's proposal

**A passage is one cell (4 m) wide.** That is the width of the doors and of the kit's arch (3.1 m clear), and it leaves 3.5 m between the wall faces and about 2.5 m of NavMesh. Two cells would be an 8 m hall, which is what he called an office.

**It runs 2 to 4 cells (8 to 16 m) from one room's wall to the next room's wall.** Two cells is the shortest gap that reads as a passage and not a thick doorway. From three cells on, the camera (about 11 m ahead to the north) no longer shows the next door from the room, and the next room's enemies (aggro 9 m, shooters 14 m, all at least 8 m in from its door) stay asleep until she is at its door. At 4 cells the passage is about 2.7 s at a run, and anything longer becomes a tube.

**About half of them jog.** A jog is a step sideways of 1 or 2 cells in the middle, with two bends. A passage deeper than 3 cells always jogs, so no straight run is longer than 12 m (the report's rule). A jog never turns in the cell next to a room: a passage leaves a room straight and arrives straight. The jog hides the far end. It keeps the rooms' placement as it is today: the next room still faces the room before across the gap. A single L-bend would need rooms set diagonally to each other, which is a new way of placing rooms and is left for later.

**A passage is a room of its own kind (`RoomKind.Passage`) in the row: E P C P C P T P L … P X.** Its rectangle is its bounding box, and the cells off its path are solid, using work order 051's mask. Because it is a room, the walls, the kit, the colliders, the NavMesh, the text map and the small map follow by themselves, through `LevelLayout.RoomAtCell`. The row's rules (two doors, neighbours only) and "the room she is in" work for it unchanged. The cost of this choice is in the code that loops over the rooms: each loop must skip passages where it means real rooms. That is the shapes stage, the mood's plan, the validator's sizes and lane, and the room numbers in the HUD. There are about a dozen such places, each named in the order.

The other choice was a new kind of thing beside the rooms, with doors that have two openings each. It would leave those loops alone but break everything that asks "which room is this cell in": the walls, the kit, the NavMesh floors, the maps, the door signs and her current room. All of those would have to learn about passages.

**Nothing stands in a passage in the first order.** It holds no enemies and no props. Nothing ever closes, so no fight can lock her in. Enemies that chase her may follow her into a passage, and a passage is a way to fall back. A pack of swarmers on a small landing is for a later order, once the empty passages have been played.

**The ends.** At each end of a passage there is a door, with the room's own wall running on at least a cell either side of it.
- **No gate where a passage comes in.** Work order 051's gate (a one-cell slot inside the door in) is dropped: the passage itself is the threshold, and the room opens straight from its mouth.
- **No new arch on a north or south door.** An arch across a doorway that faces the camera would hide her feet as she walks through it. This is the project's existing rule for low walls.
- **The door signs.** A door into a passage shows the sign of the room the passage leads to. So the gold chest hangs over the passage mouth seen from the room before (lit there), and again over the far end seen from inside the passage (lit there). There is still at most one door light.

**The camera and the dark.** The camera does not change.
- **Walls.** By the existing wall rule, a north-south passage has full-height side walls, and an east-west one has a low south wall. Where she walks in a passage, the wall straight south of her (toward the camera) is always low or a doorway, so she is never hidden.
- **Light.** The mood's lights stay in her room only, so a passage is lit by her own light and the glowing crystals of the kit's lanterns on its walls. The mood's "next room" (air, flicker) becomes the next real room. From the room before, the light at the far end of a dark passage draws the eye on.

**The map.** A room appears one step ahead when the room before its passage has been entered. From any room she sees the passage and the room after it, never more. The row stays one line on the map.

**Chapels budded on: not in this order.** In the back end a room would become several rectangles, and every check that uses a room's rectangle would have to change: enemy places, props, the map, the benchmark, the pilots. That is most of a second order. Work order 051's alcoves and apses already carve two-cell chapels into the walls of the rectangle. When chapels come, they are at most 2 cells deep, always fully seen from the room's middle, and never hold a chest, so they are never a room he has to look into.

**The switch.** `LevelValues.Passages` is on, with version 16 of the generator. With it off, every floor is the version before's, field for field.

## Passages and the room templates (proposal B)

- **Both can be on at once,** if the templates are built as a stage for the inside of a room, where work order 051's shapes are now: after the layout and the passages, before the contents, taking the doors where the passages put them.
- **The passages should be built first.** They change where rooms stand and what joins them, and the door signs, map, mood and pilots learn about them once. Built the other way round, the templates would bring connectors of their own and do half of this work in another shape. The templates also replace the carved shapes he has not played yet; the passages do not depend on whether he likes them.
- **The passages order leaves alone** what is inside a room: its sizes and which of its cells are solid. Its only touches there are skipping passages in the shapes stage and dropping the gate where a passage comes in. It keeps the choice of a passage's two door cells in one method, so that the templates can later choose them from their exits.
- **The templates order leaves alone** the placement of rooms apart, the passages and the passage kind's rules. If it wants an L-bend or a landing, it adds it as a new form of passage in the passage stage, not as a second kind of corridor.

## Considered and not chosen

- **Passages as a new thing beside the rooms:** see above.
- **Two cells wide:** a hall, not a passage.
- **Straight passages only:** fewer lines of code, but every passage would be a pipe with the next door in sight.
- **Keeping the gate inside the passage's door:** one more cell of slot after 8 to 16 m of passage, and three fewer cells for the room's shape.
- **A small pack in some passages now:** that needs new placement rules in a 3.5 m slot, and the floor would no longer hold version 9's enemies. Better after he has walked the empty ones.
- **Grates in the walls beside a passage** (the report's barred arches): this needs a kit piece the kit does not have.

## Open, for Kristian

1. **Length:** 8 to 16 m between rooms: too short, right, or too long?
2. **Jogs:** should more of the passages bend, or fewer?
3. **The gate:** keep a gate inside the door even after a passage?
4. **Dark:** is a passage too dark with only her light and the lanterns?
5. **Later orders:** a small pack waiting on a landing in some passages; chapels; grates to see into the next room before the door.
