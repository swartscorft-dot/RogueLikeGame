using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Checks a floor's description against the rules of a floor and says every fault it finds; an empty list means
    /// valid. It does not trust the generator: it reads only the description, so it can be run on any floor.
    ///
    /// It checks: the rooms are listed in the order of the run; every room is inside the grid, of a legal size for its
    /// kind and over no other; the plan's number of rooms of each kind; every door joins two rooms that share that
    /// stretch of wall, is as wide as a door, and has wall on both sides of it, and no two doors join the same rooms;
    /// every room can be reached from the entrance through doors; the main path runs entrance, the combat rooms, exit,
    /// each joined to the next by a door, and the exit room is last on it, has exactly one door and does not touch the
    /// entrance; every treasure room has exactly one door, to a combat room, and no combat room has two; no other
    /// door; the player's start is in the entrance only. Since work order 012 (decision 0008, 2026-11-19) also the chain:
    /// no room of the main path has its door out on the wall of its door in, and a treasure room's door is on a side
    /// wall of its combat room (one with neither the door in nor the door out); and the enemies: the floor's number is
    /// in the act; only combat and exit rooms hold enemies, as many as the floor's range allows (the act boss apart), of
    /// at least two kinds, no more guards than the floor allows, the act boss in the exit room of the act's last floor
    /// and nowhere else; packs of swarmers, lines of shooters and of brutes no larger than allowed, guards alone; each
    /// group's enemies stand where <see cref="LevelGeometry.MemberPlaces"/> puts them; every enemy inside its room, clear
    /// of its walls and doors and of the door she comes in by, enemy points apart, and enemies of different groups apart.
    /// Since work order 009 also the props: each is a model its kind of room may hold, with its model's footprint, and
    /// keeps every rule of <see cref="PropRules"/>; no room holds more than its kind may; and every room keeps its
    /// clear lane. Since work order 017 the elite room: exactly the combat room of the chain the plan names
    /// (<see cref="LevelPlan.EliteRoomNumber"/>) is marked elite, and it alone holds an elite: one, of a kind its floor can
    /// have, counted apart from the room's range, kinds and guards, with its larger body's room round it. Since work order
    /// 027 the casters: no more in a room than its floor allows (<see cref="LevelValues.CastersMost"/>), each alone, and
    /// <see cref="LevelValues.CasterSpacing"/> apart. Since work order 028 the rooms' themes: each one its kind of room may
    /// have, no two combat rooms next to each other on the chain of the same theme, a themed room's props only of its
    /// recipe's models and no more than it holds, the wall pieces' rules of <see cref="PropRules"/> (the crypt kit's niches
    /// read from its plan), and every combat room with at least <see cref="PropValues.MinWalkable"/> of its floor walkable.
    /// Since work order 041 a floor in a line (<see cref="LevelLayout.InALine"/>, as the values' <see cref="LevelValues.InALine"/>
    /// must say): the main path is every room, in the order of <see cref="LevelPlan.LineBetween"/> between the entrance and
    /// the exit, the treasure rooms on it; each room has a door in and a door out (the entrance one out, the exit one in),
    /// so none has three and none is a dead end; every door joins two neighbours of the row; and the chain's way on holds
    /// for every room of it, the treasure rooms too. The side rooms' rules are not asked of it.
    /// Since work order 051 the rooms' carved outlines: every room with solid cells or a shape keeps the rules of
    /// <see cref="RoomShapes.Faults"/> (one open area, the open square round its middle, open floor straight in behind every
    /// door, no dead-end slot, no thin corner, no more than its share solid, and on a carved floor its gate), and the
    /// enemies, her start and the props keep off the solid cells, measured to the outline.
    /// Since work order 058 the rooms drawn by hand (<see cref="RoomTemplates.Check"/>): a template room's rectangle is its
    /// template's turned size in place of the plan's, its kind the template's, its door in beside an I and its door out beside
    /// an O, a stamped one's solid cells its template's # and ? cells and its gate, every # solid; no two neighbouring combat
    /// rooms of the row of one family, and no template twice on a floor.
    /// Since work order 059 a floor with passages (<see cref="LevelLayout.Passages"/>, as the values say): the main path is the
    /// row with a passage between every two rooms, and the passages are one fewer than the rooms; a passage's size is its own
    /// rule, and its rules are <see cref="Passages.Faults"/> (two doors at the ends of one path a cell wide, its depth, at most
    /// two bends, none next to a room, no straight run too long, touching only its two rooms, holding nothing); a door that
    /// joins a passage is on its room's wall with that wall running on a cell either side of it and no other door there
    /// (<see cref="Passages.DoorFault"/>), in place of the wall on both sides of the stretch the two share; no two rooms that
    /// are not passages touch; a passage keeps no lane; and the walk over cells from her start reaches every open cell of the
    /// floor (<see cref="Passages.WalkFault"/>).
    /// </summary>
    public static class LevelValidator
    {
        const double Slack = 1e-9;

        public static List<string> Validate(LevelLayout level, LevelValues values = null)
        {
            values = values ?? new LevelValues();
            List<string> faults = Check(level, values);
            // Work order 041: the values say which rules a floor keeps, and the floor says which it was made by.
            if (level != null && level.InALine != values.InALine)
                faults.Add(level.InALine ? "the floor is in a line and the values' InALine is off" : "the floor has side rooms and the values' InALine is on");
            // Work order 059: and whether its rooms stand apart with passages between them.
            bool passages = values.Passages && values.InALine && values.DoorWidth == 1;
            if (level != null && level.InALine == values.InALine && level.Passages != passages)
                faults.Add(level.Passages ? "the floor has passages and the values' Passages is off" : "the floor has no passages and the values' Passages is on");
            return faults;
        }

        /// <summary>
        /// Every fault of the floor by the rules it says it keeps (<see cref="LevelLayout.InALine"/>), whatever the values'
        /// switch: <see cref="Validate"/> without its last check, for the generator's floor of version 9 (work order 041).
        /// </summary>
        internal static List<string> Check(LevelLayout level, LevelValues values)
        {
            var faults = new List<string>();
            if (level == null)
            {
                faults.Add("no level");
                return faults;
            }
            LevelPlan plan = values.Plan;
            List<LevelRoom> rooms = level.Rooms;
            double cs = level.CellSize;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);

            if (cs <= 0) faults.Add("cell size " + cs + " is not positive");
            if (level.Floor < 1 || level.Floor > values.FloorsPerAct) faults.Add($"floor {level.Floor} is not 1 to {values.FloorsPerAct}");

            // The order of the run.
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Order != i) faults.Add($"room {i} in the list has order {rooms[i].Order}");

            // Each room: bounds, size, overlaps.
            foreach (LevelRoom r in rooms)
            {
                CellRect c = r.Rect;
                if (c.X < 0 || c.Y < 0 || c.Right > level.GridCells || c.Top > level.GridCells)
                    faults.Add($"{Name(r)} {c} is outside the grid of {level.GridCells}");
                RoomSizeRange size = plan.SizeOf(r.Kind);
                if (r.Kind == RoomKind.Passage) continue; // work order 059: a passage's size is its own rule (Passages.Faults)
                if (r.Template == null && (!size.Allows(c.Width) || !size.Allows(c.Height))) // work order 058: a template room's size is its template's, below
                    faults.Add($"{Name(r)} is {c.Width}x{c.Height}, not {size.Min} to {size.Max} on a side for a {r.Kind} room");
            }
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                    if (rooms[i].Rect.Overlaps(rooms[j].Rect)) faults.Add($"{Name(rooms[i])} overlaps {Name(rooms[j])}");

            // The plan.
            void Count(RoomKind kind, int wanted)
            {
                int n = rooms.Count(r => r.Kind == kind);
                if (n != wanted) faults.Add($"{n} {kind} rooms, the plan has {wanted}");
            }
            Count(RoomKind.Entrance, 1);
            int combatRooms = level.InALine ? plan.LineCombatRooms : plan.CombatRooms; // work order 046: a row has one more
            Count(RoomKind.Combat, combatRooms);
            Count(RoomKind.Treasure, plan.TreasureRooms);
            Count(RoomKind.Exit, 1);
            Count(RoomKind.Passage, level.Passages ? Math.Max(0, rooms.Count(r => r.Kind != RoomKind.Passage) - 1) : 0); // work order 059: one fewer than the rooms

            // The doors.
            bool Known(int order) => order >= 0 && order < rooms.Count;
            var pairs = new HashSet<(int, int)>();
            foreach (LevelDoor d in level.Doors)
            {
                string label = $"door {d.RoomA}-{d.RoomB}";
                if (!Known(d.RoomA) || !Known(d.RoomB) || d.RoomA == d.RoomB)
                {
                    faults.Add(label + " does not join two rooms of the level");
                    continue;
                }
                if (!pairs.Add((Math.Min(d.RoomA, d.RoomB), Math.Max(d.RoomA, d.RoomB))))
                    faults.Add(label + " joins two rooms another door already joins");
                if (d.Width != values.DoorWidth) faults.Add($"{label} is {d.Width} cells wide, not {values.DoorWidth}");
                CellRect a = rooms[d.RoomA].Rect, b = rooms[d.RoomB].Rect;
                if (!LevelGeometry.SharedWall(a, b, out bool vertical, out int line, out int lo, out int hi)
                    || vertical != d.Vertical || line != d.Line)
                {
                    faults.Add($"{label}: the two rooms do not share that wall ({(d.Vertical ? "vertical" : "horizontal")} line {d.Line})");
                    continue;
                }
                bool passageEnd = rooms[d.RoomA].Kind == RoomKind.Passage || rooms[d.RoomB].Kind == RoomKind.Passage; // work order 059
                if (d.Width < 1 || d.From < lo || d.From + d.Width > hi)
                    faults.Add($"{label} at {d.From}+{d.Width} is not on the stretch the rooms share ({lo} to {hi})");
                else if (passageEnd)
                {
                    // Work order 059: the stretch is the passage's own end; the room's wall runs on a cell either side instead.
                    string end = Passages.DoorFault(level, d);
                    if (end != null) faults.Add($"{label} at {d.From}+{d.Width} {end}");
                }
                else if (d.From - 1 < lo || d.From + d.Width + 1 > hi)
                    faults.Add($"{label} at {d.From}+{d.Width} has no wall on both sides (shared stretch {lo} to {hi})");
            }

            // Reaching every room from the entrance.
            LevelRoom start = rooms.FirstOrDefault(r => r.Kind == RoomKind.Entrance);
            if (start != null)
            {
                var reached = new HashSet<int> { start.Order };
                var queue = new Queue<int>();
                queue.Enqueue(start.Order);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    foreach (LevelDoor d in level.Doors)
                    {
                        if (!d.Joins(at) || !Known(d.Other(at))) continue;
                        if (reached.Add(d.Other(at))) queue.Enqueue(d.Other(at));
                    }
                }
                foreach (LevelRoom r in rooms)
                    if (!reached.Contains(r.Order)) faults.Add($"{Name(r)} cannot be reached from the entrance");
            }

            // The main path.
            List<LevelRoom> path = LevelGeometry.MainPath(level);
            var wantedPath = new List<RoomKind> { RoomKind.Entrance };
            if (level.InALine) wantedPath.AddRange(plan.LineBetween()); // work order 041: the row, the treasure rooms on it
            else wantedPath.AddRange(Enumerable.Repeat(RoomKind.Combat, plan.CombatRooms));
            wantedPath.Add(RoomKind.Exit);
            if (level.Passages) // work order 059: a passage between every two rooms of the row
                for (int i = wantedPath.Count - 1; i > 0; i--) wantedPath.Insert(i, RoomKind.Passage);
            if (!path.Select(r => r.Kind).SequenceEqual(wantedPath))
                faults.Add("the main path runs " + string.Join(" ", path.Select(Name)) + ", not "
                           + (level.InALine ? string.Join(" ", wantedPath.Select(LevelRoom.KindLetter)) + " (the row of work order 041)"
                                            : "entrance, " + plan.CombatRooms + " combat rooms, exit"));
            // The row (work order 041): every room has a door in and a door out, the entrance only out and the exit only in.
            if (level.InALine)
                foreach (LevelRoom r in rooms)
                {
                    int doors = level.DoorsOf(r.Order).Count();
                    int wanted = r.Kind == RoomKind.Entrance || r.Kind == RoomKind.Exit ? 1 : 2;
                    if (doors != wanted) faults.Add($"{Name(r)} has {doors} doors, and a room of the row has {wanted}");
                }
            bool Joined(int x, int y) => level.Doors.Any(d => d.Joins(x) && d.Other(x) == y);
            for (int i = 1; i < path.Count; i++)
                if (!Joined(path[i - 1].Order, path[i].Order)) faults.Add($"no door from {Name(path[i - 1])} to {Name(path[i])} on the main path");
            // The elite room (work order 017): the plan's combat room of the chain, and no other.
            int eliteNumber = plan.EliteRoomNumber(combatRooms);
            List<LevelRoom> combatsInOrder = path.Where(r => r.Kind == RoomKind.Combat).ToList();
            for (int i = 0; i < combatsInOrder.Count; i++)
            {
                bool wanted = i + 1 == eliteNumber;
                if (combatsInOrder[i].Elite != wanted)
                    faults.Add($"{Name(combatsInOrder[i])}, combat room {i + 1} of the chain, "
                               + (wanted ? "is not the elite room" : "is marked elite, and the plan's elite room is combat room " + eliteNumber));
            }
            foreach (LevelRoom r in rooms)
                if (r.Elite && r.Kind != RoomKind.Combat) faults.Add($"{Name(r)} is marked elite and is not a combat room");
            LevelRoom exit = rooms.FirstOrDefault(r => r.Kind == RoomKind.Exit);
            if (exit != null)
            {
                if (path.Count > 0 && path[path.Count - 1] != exit) faults.Add("the exit room is not the last room of the main path");
                int doors = level.DoorsOf(exit.Order).Count();
                if (doors != 1) faults.Add($"the exit room has {doors} doors, not 1");
                if (start != null && exit.Rect.Touches(start.Rect)) faults.Add("the exit room touches the entrance");
            }

            // The chain (work order 012): the way out of a room is never on the wall of the way in.
            foreach (LevelRoom r in path)
            {
                LevelDoor wayIn = LevelGeometry.EntryDoor(level, r), wayOut = LevelGeometry.ExitDoor(level, r);
                if (wayIn == null || wayOut == null) continue;
                int inWall = LevelGeometry.WallOf(r.Rect, wayIn), outWall = LevelGeometry.WallOf(r.Rect, wayOut);
                if (inWall >= 0 && inWall == outWall) faults.Add($"{Name(r)}'s door out is on the wall of its door in");
            }

            // The treasure rooms off the sides (in a line, work order 041, they are rooms of the row, checked with it).
            var treasuresOf = new Dictionary<int, int>();
            foreach (LevelRoom r in rooms.Where(x => x.Kind == RoomKind.Treasure && !level.InALine))
            {
                List<LevelDoor> doors = level.DoorsOf(r.Order).ToList();
                if (doors.Count != 1)
                {
                    faults.Add($"{Name(r)} has {doors.Count} doors, not 1");
                    continue;
                }
                int other = doors[0].Other(r.Order);
                if (!Known(other) || rooms[other].Kind != RoomKind.Combat)
                {
                    faults.Add($"{Name(r)}'s door does not lead to a combat room");
                    continue;
                }
                treasuresOf.TryGetValue(other, out int n);
                treasuresOf[other] = n + 1;
                if (n + 1 == 2) faults.Add($"{Name(rooms[other])} has two treasure rooms");
                // On a side wall of its combat room (work order 012): not the wall of its door in nor of its door out.
                LevelRoom combat = rooms[other];
                int wall = LevelGeometry.WallOf(combat.Rect, doors[0]);
                LevelDoor cin = LevelGeometry.EntryDoor(level, combat), cout = LevelGeometry.ExitDoor(level, combat);
                if ((cin != null && LevelGeometry.WallOf(combat.Rect, cin) == wall) || (cout != null && LevelGeometry.WallOf(combat.Rect, cout) == wall))
                    faults.Add($"{Name(r)}'s door is not on a side wall of {Name(combat)}");
            }

            // No other door: each joins two neighbours on the main path, or a treasure room to its combat room.
            foreach (LevelDoor d in level.Doors)
            {
                if (!Known(d.RoomA) || !Known(d.RoomB)) continue;
                LevelRoom a = rooms[d.RoomA], b = rooms[d.RoomB];
                if (!level.InALine && (a.Kind == RoomKind.Treasure || b.Kind == RoomKind.Treasure)) continue; // checked above
                int ia = path.IndexOf(a), ib = path.IndexOf(b);
                if (ia < 0 || ib < 0 || Math.Abs(ia - ib) != 1) faults.Add($"door {d.RoomA}-{d.RoomB} joins rooms that are not neighbours on the main path");
            }

            // What stands in the rooms.
            foreach (LevelRoom r in rooms)
            {
                if (r.Kind == RoomKind.Entrance && !r.PlayerStart.HasValue) faults.Add($"{Name(r)} has no player start");
                if (r.Kind != RoomKind.Entrance && r.PlayerStart.HasValue) faults.Add($"{Name(r)} has a player start but is not the entrance");
                if (r.PlayerStart.HasValue)
                {
                    LevelPoint p = r.PlayerStart.Value;
                    List<LevelDoor> doors = level.DoorsOf(r.Order).Where(d => Known(d.Other(r.Order))).ToList();
                    string fault = LevelGeometry.EnemyPlaceFault(level, r, p, null, doors, values);
                    if (fault != null) faults.Add($"{Name(r)}'s player start at {p} {fault}");
                    if (doors.Any(d => LevelGeometry.DistanceToDoor(p, d, cs) < values.WallClearance - Slack))
                        faults.Add($"{Name(r)}'s player start at {p} is nearer a door than {values.WallClearance} m");
                }
                CheckEnemies(level, r, values, faults, Name(r));
            }
            LevelBombers.Faults(level, values, faults); // work order 073: the bombers' rules

            // Work order 051: every room's carved outline keeps the rules of RoomShapes.Faults (its gate among them).
            foreach (LevelRoom r in rooms)
            {
                var broken = new List<string>();
                RoomShapes.Faults(level, r, values, broken);
                foreach (string fault in broken) faults.Add(Name(r) + " " + fault);
            }
            RoomTemplates.Check(level, faults); // work order 058: the template rooms' size, kind, door marks, stamp and families
            if (level.Passages)
            {
                // Work order 059: every passage's rules, no two rooms touching, and the walk over cells reaching every open cell.
                Passages.Faults(level, values, faults);
                string walk = Passages.WalkFault(level);
                if (walk != null) faults.Add(walk);
            }
            else if (rooms.Any(r => r.Kind == RoomKind.Passage)) faults.Add("the floor holds a passage and is not marked as one with passages");

            // The props (work order 009).
            PropValues pv = values.PropRules;
            // Work order 028: the rooms' themes, each one its kind may have, and no combat theme twice in a row on the chain.
            foreach (LevelRoom r in rooms)
            {
                if (r.Theme == RoomTheme.None) continue;
                if (Array.IndexOf(RoomThemes.ThemesFor(r.Kind), r.Theme) < 0 || values.ThemeRules?.Of(r.Theme, r.Kind) == null)
                    faults.Add($"{Name(r)} is a {RoomThemes.Word(r.Theme)}, which a {r.Kind} room is never");
            }
            List<LevelRoom> combatChain = path.Where(r => r.Kind == RoomKind.Combat).ToList();
            for (int i = 1; i < combatChain.Count; i++)
                if (combatChain[i].Theme != RoomTheme.None && combatChain[i].Theme == combatChain[i - 1].Theme)
                    faults.Add($"{Name(combatChain[i - 1])} and {Name(combatChain[i])}, next to each other on the chain, are both a {RoomThemes.Word(combatChain[i].Theme)}");
            CryptKitPlan kit = null; // where the kit's niches are, read only when a wall piece stands somewhere
            foreach (LevelRoom r in rooms)
            {
                PropModel[] allowed = PropRules.ModelsFor(r, values);
                int mostIn = PropRules.MostIn(r, values);
                string what = r.Theme == RoomTheme.None ? $"a {r.Kind} room" : $"a {RoomThemes.Word(r.Theme)}";
                if (r.Props.Count > mostIn)
                    faults.Add($"{Name(r)} has {r.Props.Count} props, {what} at most {mostIn}");
                List<PropRules.WallSpan> niches = null;
                if (r.Props.Any(p => PropSize.Of(p.Model).WallPiece))
                {
                    niches = PropRules.NichesOf(level, r, kit = kit ?? PropRules.KitPlanForProps(level));
                    niches.AddRange(PropRules.CarvedNichesOf(level, r, kit)); // work order 051
                }
                for (int i = 0; i < r.Props.Count; i++)
                {
                    LevelProp p = r.Props[i];
                    string label = $"{Name(r)}'s prop {i} ({p.Model} at {p.Position})";
                    if (Array.IndexOf(allowed, p.Model) < 0) faults.Add($"{label} does not belong in {what}");
                    LevelProp table = LevelProp.Make(p.Model, p.Position, p.Rotation, p.Place);
                    if (p.Shape != table.Shape || p.Width != table.Width || p.Depth != table.Depth || p.Radius != table.Radius || p.Height != table.Height
                        || p.Slender != table.Slender)
                        faults.Add($"{label} has a footprint that is not its model's");
                    var broken = new List<string>();
                    PropRules.Fault(level, r, p, r.Props, pv, broken, niches);
                    foreach (string fault in broken) faults.Add(label + " " + fault);
                }
                // Work order 028: a combat room keeps two thirds of its floor walkable.
                if (r.Kind == RoomKind.Combat && r.Props.Count > 0)
                {
                    double share = PropRules.WalkableShare(level, r, pv);
                    if (share < pv.MinWalkable - Slack) faults.Add($"{Name(r)} keeps {share:P0} of its floor walkable, less than {pv.MinWalkable:P0}");
                }
                if (r.Kind == RoomKind.Passage) continue; // work order 059: a passage keeps no lane (its middle may be a solid cell) and holds no props (Passages.Faults)
                var lane = new PropRules.Lane(level, r, pv);
                foreach (LevelProp p in r.Props) lane.Block(p.Footprint);
                if (!lane.Clear())
                    faults.Add($"{Name(r)} has no clear lane {pv.LaneWidth} m wide from each door to each other" + (r.Kind == RoomKind.Treasure ? "" : " and to its middle"));
            }
            return faults;
        }

        /// <summary>The enemies of one room (work order 012): their numbers, kinds and groups, and where each stands. Work order 084: the room lab's check asks it too.</summary>
        internal static void CheckEnemies(LevelLayout level, LevelRoom r, LevelValues values, List<string> faults, string name)
        {
            int floor = level.Floor;
            if (r.EnemyGroups.Count != r.EnemyPoints.Count)
                faults.Add($"{name} has {r.EnemyGroups.Count} enemy groups for {r.EnemyPoints.Count} enemy points");
            if (r.EnemyPlaces.Count != r.EnemyPoints.Count)
                faults.Add($"{name} has places for the enemies of {r.EnemyPlaces.Count} points, and {r.EnemyPoints.Count} points");

            bool holds = r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit;
            if (!holds)
            {
                if (r.EnemyPoints.Count > 0) faults.Add($"{name} holds enemies, and a {r.Kind} room holds none");
                return;
            }

            List<EnemyGroup> groups = r.EnemyGroups.Where(g => g != null).ToList();
            List<EnemyGroup> others = groups.Where(g => !g.Boss && !g.Elite).ToList();
            int count = others.Sum(g => g.Count);
            values.EnemyRange(r.Kind, floor, out int fewest, out int most);
            if (count < fewest || count > most) faults.Add($"{name} holds {count} enemies, floor {floor} has {fewest} to {most} in a {r.Kind} room");
            int kinds = others.Select(g => g.Kind).Distinct().Count();
            if (kinds < 2) faults.Add($"{name} holds {kinds} kind of enemy, not at least two");
            int guards = groups.Where(g => g.Kind == EnemyKind.Guard && !g.Elite).Sum(g => g.Count);
            int guardsMost = values.GuardsMost(r.Kind, floor);
            if (guards > guardsMost) faults.Add($"{name} holds {guards} guards, floor {floor} at most {guardsMost} in a {r.Kind} room");
            // The casters (work order 027): no more than the floor allows, the elite apart; each alone, and far from each other.
            int casters = groups.Where(g => g.Kind == EnemyKind.Caster && !g.Elite).Sum(g => g.Count);
            int castersMost = values.CastersMost(r.Kind, floor);
            if (casters > castersMost) faults.Add($"{name} holds {casters} casters, floor {floor} at most {castersMost} in a {r.Kind} room");
            for (int i = 0; i < r.EnemyPoints.Count && i < r.EnemyGroups.Count; i++)
                for (int j = i + 1; j < r.EnemyPoints.Count && j < r.EnemyGroups.Count; j++)
                    if (r.EnemyGroups[i]?.Kind == EnemyKind.Caster && r.EnemyGroups[j]?.Kind == EnemyKind.Caster
                        && r.EnemyPoints[i].DistanceTo(r.EnemyPoints[j]) < values.CasterSpacing - Slack)
                        faults.Add($"{name}'s casters {i} and {j} are nearer each other than {values.CasterSpacing} m");
            int bosses = groups.Count(g => g.Boss);
            bool wantsBoss = r.Kind == RoomKind.Exit && floor >= values.FloorsPerAct;
            if (bosses != (wantsBoss ? 1 : 0)) faults.Add($"{name} holds {bosses} act bosses, not {(wantsBoss ? 1 : 0)}");
            // The elite (work order 017): one in the elite room, of a kind its floor can have; none elsewhere.
            List<EnemyGroup> elites = groups.Where(g => g.Elite).ToList();
            if (elites.Count != (r.Elite ? 1 : 0)) faults.Add($"{name} holds {elites.Count} elites, not {(r.Elite ? 1 : 0)}");
            foreach (EnemyGroup g in elites)
            {
                if (g.Count != 1 || g.Boss) faults.Add($"{name}'s elite is not one enemy");
                if (Array.IndexOf(values.EliteKinds(floor), g.Kind) < 0) faults.Add($"{name}'s elite is a {EnemyKinds.Word(g.Kind)}, which floor {floor} has no elite of");
            }

            for (int i = 0; i < r.EnemyGroups.Count; i++)
            {
                EnemyGroup g = r.EnemyGroups[i];
                if (g == null || g.Count < 1)
                {
                    faults.Add($"{name}'s enemy {i} has no enemy in its group");
                    continue;
                }
                if (g.Boss && (g.Kind != EnemyKind.Boss || g.Count != 1)) faults.Add($"{name}'s act boss {i} is not one boss");
                if (!g.Boss && g.Kind == EnemyKind.Boss) faults.Add($"{name}'s enemy {i} is a boss that is not the act boss");
                int largest = g.Kind == EnemyKind.Swarmer ? values.PackMost : g.Kind == EnemyKind.Shooter ? values.ShooterLineMost
                            : g.Kind == EnemyKind.Brute ? values.BruteLineMost : 1;
                if (g.Count > largest) faults.Add($"{name}'s group {i} is {g.Count} {EnemyKinds.Word(g.Kind)}s, at most {largest} stand together");
            }

            LevelDoor entry = LevelGeometry.EntryDoor(level, r);
            List<LevelDoor> doors = level.DoorsOf(r.Order).Where(d => d.Other(r.Order) >= 0 && d.Other(r.Order) < level.Rooms.Count).ToList();
            for (int i = 0; i < r.EnemyPoints.Count; i++)
            {
                LevelPoint p = r.EnemyPoints[i];
                EnemyGroup g = r.GroupAt(i);
                double extra = LevelGeometry.ExtraRoom(g, values); // an elite's larger body (work order 017)
                string pointFault = LevelGeometry.EnemyPlaceFault(level, r, p, entry, doors, values, extra);
                if (pointFault != null) faults.Add($"{name}'s enemy {i} at {p} {pointFault}");
                LevelPoint[] places = r.PlacesAt(i);
                LevelPoint[] wanted = LevelGeometry.MemberPlaces(level, r, p, g, values);
                bool same = places.Length == wanted.Length;
                for (int m = 0; same && m < places.Length; m++) same = places[m].DistanceTo(wanted[m]) < 1e-6;
                if (!same) faults.Add($"{name}'s group {i} ({g}) does not stand as its kind of group stands round its point {p}");
                for (int m = 0; m < places.Length; m++)
                {
                    string fault = LevelGeometry.EnemyPlaceFault(level, r, places[m], entry, doors, values, extra);
                    if (fault != null) faults.Add($"{name}'s enemy {i}.{m} at {places[m]} {fault}");
                }
                for (int j = i + 1; j < r.EnemyPoints.Count; j++)
                {
                    if (p.DistanceTo(r.EnemyPoints[j]) < values.EnemySpacing - Slack)
                        faults.Add($"{name}'s enemies {i} and {j} are nearer each other than {values.EnemySpacing} m");
                    double apart = values.MemberSpacing + Math.Max(extra, LevelGeometry.ExtraRoom(r.GroupAt(j), values));
                    foreach (LevelPoint a in places)
                        foreach (LevelPoint b in r.PlacesAt(j))
                            if (a.DistanceTo(b) < apart - Slack)
                            {
                                faults.Add($"{name}'s groups {i} and {j} stand nearer each other than {apart} m");
                                goto nextPair;
                            }
                    nextPair:;
                }
            }
        }
    }
}
