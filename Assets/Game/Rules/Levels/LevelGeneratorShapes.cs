using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The generator's part of the rooms' carved outlines (work order 051, <see cref="RoomShapes"/>), kept apart from its
    /// stages so the two can be read and merged alone: when the contents find no place for a room's enemies on a carved
    /// floor, that room is made plainer and the contents are drawn again.
    /// </summary>
    public static partial class LevelGenerator
    {
        /// <summary>
        /// The contents of a carved floor once more, after they failed (work order 051): the room whose enemies found no place
        /// (in a line the first whose groups are not all placed; with side rooms the first combat or exit room left empty and
        /// the one before it) gives up its shape for its gate alone, or its gate for the plain rectangle; when no such room has
        /// anything to give, every room does. Then the contents are drawn again from the start of their stream. True once they
        /// fit; false when every room is its plain rectangle and they still do not, which is when they did not fit without the
        /// shapes either (the same stream on the same rectangles), so an attempt fails only where it failed before.
        /// </summary>
        static bool PlaceAgainPlainer(LevelLayout level, List<List<EnemyGroup>> fights, ulong seed, int attempt, int warmUp, LevelValues v)
        {
            if (!level.Shapes) return false;
            for (int round = 0; round < 3 * level.Rooms.Count + 2; round++)
            {
                bool changed = false;
                foreach (LevelRoom r in Blamed(level, fights)) changed |= Plainer(level, r);
                if (!changed)
                    foreach (LevelRoom r in level.Rooms) changed |= Plainer(level, r);
                if (!changed) return false;
                foreach (LevelRoom r in level.Rooms)
                {
                    r.PlayerStart = null;
                    r.EnemyPoints.Clear();
                    r.EnemyGroups.Clear();
                    r.EnemyPlaces.Clear();
                }
                LevelRandom contents = LevelRandom.Stream(seed, ContentsStage, attempt);
                for (int i = 0; i < warmUp; i++) contents.NextBits();
                if (fights != null ? PlaceFights(level, fights, contents, v) : PlaceContents(level, contents, v)) return true;
            }
            return false;
        }

        /// <summary>The rooms whose enemies found no place (see <see cref="PlaceAgainPlainer"/>).</summary>
        static List<LevelRoom> Blamed(LevelLayout level, List<List<EnemyGroup>> fights)
        {
            var blamed = new List<LevelRoom>();
            int next = 0;
            LevelRoom before = null;
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                if (fights != null)
                {
                    int wanted = next < fights.Count ? fights[next].Count : 0;
                    next++;
                    if (room.EnemyGroups.Count != wanted)
                    {
                        blamed.Add(room);
                        return blamed;
                    }
                }
                else if (room.EnemyPoints.Count == 0)
                {
                    blamed.Add(room);
                    if (before != null) blamed.Add(before);
                    return blamed;
                }
                before = room;
            }
            if (fights == null && before != null) blamed.Add(before);
            return blamed;
        }

        /// <summary>One step plainer: a shape gives way to the gate alone, the gate to the plain rectangle; false when it is plain already.</summary>
        static bool Plainer(LevelLayout level, LevelRoom room)
        {
            if (room.Shape != RoomShape.Plain && room.Shape != RoomShape.None)
            {
                // Its solid cells but the gate's go: the gate is drawn again on the bare rectangle by the shapes' own rule.
                room.ClearSolid();
                RoomShapes.Gate(level, room);
                room.Shape = RoomShape.Plain;
                room.ShapeFellBack = true;
                return true;
            }
            if (room.SolidCount > 0 || room.Shape == RoomShape.Plain)
            {
                room.ClearSolid();
                room.Shape = RoomShape.None;
                room.ShapeFellBack = true;
                return true;
            }
            return false;
        }
    }
}
