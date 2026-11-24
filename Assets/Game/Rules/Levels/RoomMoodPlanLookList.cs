using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    public sealed partial class RoomMoodPlan
    {
        /// <summary>
        /// Work order 112 (<see cref="LookListValues.OssuaryCandles"/>; 091's picture: "the middle of the room is a cold blue-grey,
        /// the darkest of the theme rooms"): the ossuary's eye-catcher gets what the other themes' have, sets of candles at its
        /// props, and they go to the props nearest the room's middle, so its middle gets warm light. Its skull and bone piles, cages,
        /// urns and rubble, nearest the middle first (only those whose edge is within <see cref="LookListValues.CandleReach"/>), get
        /// sets on their side toward the middle, <see cref="LookListValues.CandleGap"/> past the edge, there and turned 30, 60 and 90
        /// degrees either way (a prop may hold more than one, as the shrine's basin does), each set itself within the reach, until
        /// <see cref="LookListValues.CandleSets"/>. A set keeps the mood's rules for candles
        /// (inside the room clear of its walls and carved cells, off every prop, away from the doors) and
        /// <see cref="LookListValues.CandleSpacing"/> from another set, <see cref="LookListValues.CandleFromPlaces"/> from her start and
        /// every enemy's place, off the large props, the empty place and the wear that stands up. Made last: it draws nothing from the
        /// mood's stream (each set's number is its place's), so the rest of the plan is as before. A set is a source of light, a
        /// candle, like every set of candles (its flames, its pool, a lamp in the lamplit light).
        /// </summary>
        static void OssuaryCandles(LevelLayout layout, LevelRoom room, MoodRoom mr, MoodValues mv)
        {
            if (room.Theme != RoomTheme.Ossuary || LookListValues.CandleSets <= 0) return;
            double cs = layout.CellSize;
            LevelPoint middle = room.Middle(cs);
            var props = room.Props
                .Where(p => (p.Model == PropModel.SkullPile || p.Model == PropModel.BonePile || p.Model == PropModel.Cage || p.Model == PropModel.Urns || p.Model == PropModel.Rubble)
                            && !mr.Replaced.Contains(p))
                .Select((p, i) => (p, i, d: p.Footprint.DistanceTo(middle)))
                .Where(t => t.d <= LookListValues.CandleReach)
                .OrderBy(t => t.d).ThenBy(t => t.i)
                .ToList();
            int placed = 0;
            foreach ((LevelProp prop, int _, double _) in props)
            {
                if (placed >= LookListValues.CandleSets) break;
                double dx = middle.X - prop.Position.X, dy = middle.Y - prop.Position.Y, len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) { dx = 0; dy = -1; len = 1; }
                dx /= len;
                dy /= len;
                foreach (int turn in new[] { 0, 30, -30, 60, -60, 90, -90 })
                {
                    double a = turn * Math.PI / 180.0, ux = dx * Math.Cos(a) - dy * Math.Sin(a), uy = dx * Math.Sin(a) + dy * Math.Cos(a);
                    LevelPoint at = prop.Position;
                    for (double t = 0; t < 4; t += 0.05)
                    {
                        at = new LevelPoint(prop.Position.X + ux * t, prop.Position.Y + uy * t);
                        if (prop.Footprint.DistanceTo(at) >= LookListValues.CandleGap) break;
                    }
                    if (at.DistanceTo(middle) > LookListValues.CandleReach || CandleFault(layout, room, mr, at, mv) != null) continue;
                    uint variant = unchecked((uint)(Math.Round(at.X * 1000) * 73856093) ^ (uint)(Math.Round(at.Y * 1000) * 19349663) ^ 0x112u);
                    mr.Pieces.Add(new CompositionPiece { Room = room.Order, Kind = CompositionKind.Candles, At = at, Height = 0, Variant = variant });
                    mr.Sources.Add(new MoodSource { Room = room.Order, Kind = MoodSourceKind.Candle, At = at, Height = 0.3, Flame = true, ByLookList = true });
                    placed++;
                    if (placed >= LookListValues.CandleSets) break;
                }
            }
        }

        /// <summary>The first rule a set of the ossuary's candles at <paramref name="at"/> breaks (work order 112), or null.</summary>
        public static string CandleFault(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelPoint at, MoodValues mv)
        {
            double cs = layout.CellSize;
            if (layout.RoomAt(at.X, at.Y) != room) return "outside its room";
            if (!InsideClear(layout, room, at, 0.2)) return "on a wall or a prop";
            if (RoomShapes.DistanceToWalls(room, at, cs) < 0.35) return "by a wall";
            foreach (LevelDoor d in layout.DoorsOf(room.Order))
                if (LevelGeometry.DistanceToDoor(at, d, cs) < LookListValues.CandleFromDoors) return "in a doorway";
            foreach (CompositionPiece c in mr.Pieces)
                if (c.Kind == CompositionKind.Candles && c.At.DistanceTo(at) < LookListValues.CandleSpacing) return "by another set";
            if (room.PlayerStart.HasValue && room.PlayerStart.Value.DistanceTo(at) < LookListValues.CandleFromPlaces) return "on her start";
            for (int i = 0; i < room.EnemyPoints.Count; i++)
            {
                if (room.EnemyPoints[i].DistanceTo(at) < LookListValues.CandleFromPlaces) return "on an enemy's place";
                foreach (LevelPoint m in room.PlacesAt(i))
                    if (m.DistanceTo(at) < LookListValues.CandleFromPlaces) return "on an enemy's place";
            }
            foreach (LevelProp p in mr.LargeProps)
                if (p.Footprint.DistanceTo(at) < 0.3) return "on a large prop";
            if (mr.LargePropPlace.HasValue && mr.LargePropPlace.Value.DistanceTo(at) < 1.5) return "on the empty place";
            foreach (WearPiece w in mr.Wear)
                if (!w.Flat && w.At.DistanceTo(at) < w.Reach + 0.15) return "on the wear";
            return null;
        }
    }
}
