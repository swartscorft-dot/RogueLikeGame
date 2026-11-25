using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The generator's part of the ranged enemies around the room (work order 124, version 23; <see cref="LevelRanged"/> says the
    /// rules): where a room's shooters, casters and warder stand when <see cref="LevelValues.RangedAroundTheRoom"/> is on. Asked by
    /// <see cref="PlaceGroups"/> for each such group in its rank order (the first ranged group plans and stands them all); every place
    /// is one of the room's places in the contents stream's drawn order, kept by the same rules as every other enemy's
    /// (<c>Fits</c>). The room's own draws come from the stream <see cref="LevelRanged.Stage"/>, four a room that holds a ranged
    /// enemy (in this order: singles, pair or nest; the near flank; the crossfire; the first side), so the contents stream draws what
    /// it drew.
    /// </summary>
    public static partial class LevelGenerator
    {
        delegate bool FitsAt(EnemyGroup g, LevelPoint p, out LevelPoint[] members);

        sealed class RangedRoom
        {
            readonly LevelLayout _level;
            readonly LevelRoom _room;
            readonly LevelValues _v;
            readonly RangedAroundValues _r;
            readonly LevelRandom _rng;
            readonly List<LevelPoint> _places;
            readonly Func<LevelPoint, double> _share;
            readonly FitsAt _fits;
            readonly Func<EnemyGroup, LevelPoint, LevelPoint[], bool> _put;
            readonly LevelPoint _door, _into, _line, _middle;
            readonly double _cs;
            readonly bool _northOrSouth;
            readonly HashSet<EnemyGroup> _done = new HashSet<EnemyGroup>();
            bool _planned, _ok = true;
            int _clusters;

            RangedRoom(LevelLayout level, LevelRoom room, LevelDoor entry, LevelValues v, LevelRandom rng, List<LevelPoint> places,
                       Func<LevelPoint, double> share, FitsAt fits, Func<EnemyGroup, LevelPoint, LevelPoint[], bool> put)
            {
                _level = level;
                _room = room;
                _v = v;
                _r = v.RangedAround;
                _rng = rng;
                _places = places;
                _share = share;
                _fits = fits;
                _put = put;
                _cs = v.CellSize;
                LevelRanged.DoorWay(level, room, entry, out _door, out _into);
                _line = LevelRanged.LineMiddle(level, room, entry, v);
                _middle = room.Middle(_cs);
                _northOrSouth = LevelRanged.EnteredNorthOrSouth(room, entry);
            }

            /// <summary>The rules for a room, or null where they stand nothing: the switch off, no stream, no door in on a wall of the room.</summary>
            public static RangedRoom For(LevelLayout level, LevelRoom room, LevelDoor entry, LevelValues v, LevelRandom rng, List<LevelPoint> places,
                                         Func<LevelPoint, double> share, FitsAt fits, Func<EnemyGroup, LevelPoint, LevelPoint[], bool> put)
            {
                if (rng == null || !LevelRanged.On(v) || entry == null || LevelGeometry.WallOf(room.Rect, entry) < 0) return null;
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) return null;
                return new RangedRoom(level, room, entry, v, rng, places, share, fits, put);
            }

            /// <summary>Does it stand this group: a ranged one (not the elite nor the boss), or the warder with part 7 on?</summary>
            public bool Takes(EnemyGroup g) => LevelRanged.IsRanged(g) || IsPackWarder(g);

            bool IsPackWarder(EnemyGroup g) => g != null && g.Kind == EnemyKind.Warder && !g.Elite && !g.Boss && _r.WarderInThePack;

            /// <summary>Part 7: the warder is stood after every other group, so the melee it stands among have their points.</summary>
            public bool WarderLast => _r.WarderInThePack;

            /// <summary>Stands the group (the first ranged group of <paramref name="ranked"/> stands them all); false when one has no place.</summary>
            public bool Place(EnemyGroup g, List<EnemyGroup> ranked)
            {
                if (IsPackWarder(g)) return PlaceWarder(g);
                if (!_planned)
                {
                    _planned = true;
                    _ok = PlanAll(ranked);
                }
                return _ok && _done.Contains(g);
            }

            double Draw() => _rng.Below(1 << 24) / (double)(1 << 24);

            bool InFar(LevelPoint p) => _share(p) >= _v.FarBand - 1e-9;

            double SideOf(LevelPoint p) => LevelRanged.Side(_door, _into, p);

            bool Put(EnemyGroup g, LevelPoint p, LevelPoint[] members, RangedPlace place)
            {
                _put(g, p, members);
                g.Ranged = place;
                _done.Add(g);
                return true;
            }

            /// <summary>The first of the room's places (its drawn order) that the test passes and the group fits at; null with none.</summary>
            bool First(EnemyGroup g, Func<LevelPoint, bool> ok, out LevelPoint at, out LevelPoint[] members)
            {
                foreach (LevelPoint p in _places)
                    if (ok(p) && _fits(g, p, out members))
                    {
                        at = p;
                        return true;
                    }
                at = default;
                members = null;
                return false;
            }

            bool PlanAll(List<EnemyGroup> ranked)
            {
                List<EnemyGroup> units = ranked.Where(LevelRanged.IsRanged).ToList();
                if (units.Count == 0) return true;
                // The room's four draws, always all four, in this order.
                double mix = Draw(), flank = Draw(), cross = Draw(), side = Draw();

                // Part 4: singles, one pair, or one nest of three (the first shooters of the room's order), by the room's draw.
                var pool = units.Select(g => new List<EnemyGroup> { g }).ToList();
                if (_r.PairsAndNests)
                {
                    List<EnemyGroup> shooters = units.Where(g => g.Kind == EnemyKind.Shooter && g.Count == 1).ToList();
                    int want = mix < _r.SinglesShare ? 1 : mix < _r.SinglesShare + _r.PairShare ? 2 : 3;
                    if (want == 3 && shooters.Count < 3) want = 2;
                    if (want == 2 && shooters.Count < 2) want = 1;
                    if (want > 1)
                    {
                        List<EnemyGroup> together = shooters.Take(want).ToList();
                        pool.RemoveAll(u => together.Contains(u[0]));
                        pool.Insert(0, together);
                    }
                }

                // Part 6: the one group at the back: a pair, else a caster, else a shooter (a line of them with part 4 off); never the nest.
                // None when a ranged elite already stands in the far band.
                bool eliteBack = false;
                for (int i = 0; i < _room.EnemyGroups.Count && i < _room.EnemyPoints.Count; i++)
                {
                    EnemyGroup e = _room.EnemyGroups[i];
                    if (e != null && e.Elite && (e.Kind == EnemyKind.Shooter || e.Kind == EnemyKind.Caster) && InFar(_room.EnemyPoints[i])) eliteBack = true;
                }
                if (_r.OneAtTheBack && !eliteBack)
                {
                    List<EnemyGroup> back = pool.FirstOrDefault(u => u.Count == 2)
                                            ?? pool.FirstOrDefault(u => u.Count == 1 && u[0].Kind == EnemyKind.Caster)
                                            ?? pool.FirstOrDefault(u => u.Count == 1 && u[0].Kind == EnemyKind.Shooter);
                    if (back != null)
                    {
                        pool.Remove(back);
                        if (!PlaceCluster(back, InFar, RangedPost.Far, 0, true) && !FallBackAll(back)) return false;
                    }
                }

                // Part 8: the crossfire, on its floors, in a combat room, by the room's draw, of two that stand alone.
                if (_r.Crossfire && _level.Floor >= _r.CrossfireFromFloor && _room.Kind == RoomKind.Combat && cross < _r.CrossfireShare
                    && (!_r.CrossfireNorthOrSouthOnly || _northOrSouth))
                {
                    List<List<EnemyGroup>> singles = pool.Where(u => u.Count == 1 && u[0].Count == 1).Take(2).ToList();
                    if (singles.Count == 2 && PlaceCrossfire(singles[0][0], singles[1][0]))
                    {
                        pool.Remove(singles[0]);
                        pool.Remove(singles[1]);
                    }
                }

                // Part 3: the near flank, by the room's draw, in a room with enough ranged enemies: a shooter alone first, else a caster.
                int nearSide = 0;
                if (_r.NearFlank && units.Count >= _r.NearFlankRangedAtLeast && flank < _r.NearFlankShare)
                {
                    List<EnemyGroup> one = pool.FirstOrDefault(u => u.Count == 1 && u[0].Count == 1 && u[0].Kind == EnemyKind.Shooter)
                                           ?? pool.FirstOrDefault(u => u.Count == 1 && u[0].Count == 1 && u[0].Kind == EnemyKind.Caster);
                    int first = side < 0.5 ? -1 : 1;
                    if (one != null)
                        foreach (int s in new[] { first, -first })
                            if (PlaceNear(one[0], s))
                            {
                                pool.Remove(one);
                                nearSide = s;
                                break;
                            }
                }

                // Part 5: the ladder for the rest, rung on rung, alternating sides; then whatever finds no rung.
                int rung = nearSide != 0 ? 2 : 1, way = nearSide != 0 ? -nearSide : side < 0.5 ? 1 : -1;
                LevelPoint? before = null;
                foreach (List<EnemyGroup> u in pool.ToList())
                {
                    bool placed = false;
                    if (_r.Ladder)
                        for (int k = rung; k <= _r.Rungs && !placed; k++)
                        {
                            LevelRanged.RungBand(_r, k, out double lo, out double hi);
                            foreach (int s in new[] { way, -way })
                            {
                                LevelPoint? last = before;
                                bool Ok(LevelPoint p) => LevelRanged.Within(_line, p, lo, hi) && s * SideOf(p) >= _r.RungSideLeast - 1e-9
                                                         && (last == null || last.Value.DistanceTo(p) <= _r.RungFromBefore + 1e-9)
                                                         && !(_r.OneAtTheBack && InFar(p));
                                if (!PlaceCluster(u, Ok, RangedPost.Rung, k, false)) continue;
                                placed = true;
                                rung = k + 1;
                                way = -s;
                                before = _room.EnemyPoints[_room.EnemyGroups.IndexOf(u[0])];
                                break;
                            }
                        }
                    // More than the rungs: a rung again, on either side (its band and short of the far band; no step from the one before).
                    if (_r.Ladder)
                        for (int k = 1; k <= _r.Rungs && !placed; k++)
                        {
                            LevelRanged.RungBand(_r, k, out double lo, out double hi);
                            bool Again(LevelPoint p) => LevelRanged.Within(_line, p, lo, hi) && Math.Abs(SideOf(p)) >= _r.RungSideLeast - 1e-9 && !(_r.OneAtTheBack && InFar(p));
                            placed = PlaceCluster(u, Again, RangedPost.Rung, k, false);
                        }
                    if (!placed && !FallBackAll(u)) return false;
                }
                return true;
            }

            /// <summary>
            /// A pair, a nest or one alone: its first where <paramref name="ok"/> holds (on a template's h first when <paramref name="mark"/>),
            /// each other 4 to 6 m from every one before it, in the same band if it can, else short of the far band (or anywhere for the
            /// group at the back); one that finds no such place stands alone by the fallback. False when the first finds no place.
            /// </summary>
            bool PlaceCluster(List<EnemyGroup> u, Func<LevelPoint, bool> ok, RangedPost post, int rung, bool mark)
            {
                EnemyGroup first = u[0];
                LevelPoint at;
                LevelPoint[] members;
                bool found = false;
                char h = mark ? RoomTemplates.MarkFor(_room, first) : '\0';
                if (h != '\0') found = First(first, p => RoomTemplates.MarkAt(_room, p, _cs) == h, out at, out members);
                else
                {
                    at = default;
                    members = null;
                }
                if (!found && !First(first, ok, out at, out members)) return false;
                int cluster = u.Count > 1 ? ++_clusters : 0;
                Put(first, at, members, new RangedPlace { Post = post, Rung = rung, Cluster = cluster, ClusterSize = u.Count, Anchor = true });
                var together = new List<EnemyGroup> { first };
                var points = new List<LevelPoint> { at };
                for (int i = 1; i < u.Count; i++)
                {
                    EnemyGroup g = u[i];
                    bool Near(LevelPoint p) => points.All(q => LevelRanged.Within(q, p, _r.ClusterApartLeast, _r.ClusterApartMost));
                    bool InBand(LevelPoint p) => Near(p) && ((h != '\0' && RoomTemplates.MarkAt(_room, p, _cs) == h) || (post == RangedPost.Far ? InFar(p) : !(_r.OneAtTheBack && InFar(p))));
                    bool Short(LevelPoint p) => Near(p) && (post == RangedPost.Far || !(_r.OneAtTheBack && InFar(p)));
                    if (First(g, InBand, out LevelPoint p1, out LevelPoint[] m1) || First(g, Short, out p1, out m1))
                    {
                        Put(g, p1, m1, new RangedPlace { Post = post, Rung = rung, Cluster = cluster, ClusterSize = u.Count, Anchor = false });
                        together.Add(g);
                        points.Add(p1);
                    }
                    else if (!FallBack(g)) return false;
                }
                // As many as stand together.
                foreach (EnemyGroup g in together)
                {
                    g.Ranged.ClusterSize = together.Count;
                    if (together.Count == 1) g.Ranged.Cluster = 0;
                }
                return true;
            }

            /// <summary>Every one of these alone by the fallback; false when one has no place at all.</summary>
            bool FallBackAll(List<EnemyGroup> u)
            {
                foreach (EnemyGroup g in u)
                    if (!_done.Contains(g) && !FallBack(g)) return false;
                return true;
            }

            /// <summary>
            /// No pattern found it room: with part 6 on, short of the far band, else anywhere (Elsewhere); with it off as before work order
            /// 124 (the template's h first, the far band, anywhere: at the back).
            /// </summary>
            bool FallBack(EnemyGroup g)
            {
                LevelPoint at;
                LevelPoint[] members;
                if (!_r.OneAtTheBack)
                {
                    char h = RoomTemplates.MarkFor(_room, g);
                    if ((h != '\0' && First(g, p => RoomTemplates.MarkAt(_room, p, _cs) == h, out at, out members)) || First(g, InFar, out at, out members))
                        return Put(g, at, members, new RangedPlace { Post = RangedPost.Far });
                }
                else if (First(g, p => !InFar(p), out at, out members)) return Put(g, at, members, new RangedPlace { Post = RangedPost.Elsewhere });
                if (First(g, p => true, out at, out members)) return Put(g, at, members, new RangedPlace { Post = RangedPost.Elsewhere });
                return false;
            }

            /// <summary>Part 3: 8 to 11 m from the middle of the fight's line, 6 to 10 m to this side of the door's straight-in line, in view from the line's middle.</summary>
            bool PlaceNear(EnemyGroup g, int side)
            {
                bool Ok(LevelPoint p)
                {
                    if (!LevelRanged.Within(_line, p, _r.NearFromLine, _r.NearToLine)) return false;
                    double s = side * SideOf(p);
                    return s >= _r.NearSideLeast - 1e-9 && s <= _r.NearSideMost + 1e-9 && LevelRanged.InDesignView(_line, p, _r, _r.ViewMargin);
                }
                return First(g, Ok, out LevelPoint at, out LevelPoint[] members) && Put(g, at, members, new RangedPlace { Post = RangedPost.NearFlank });
            }

            /// <summary>Part 8: one west and one east of the room's middle, 9 to 13 m from it, within 5 m north or south, 16 m apart at least; with part 6 on, short of the far band.</summary>
            bool PlaceCrossfire(EnemyGroup west, EnemyGroup east)
            {
                var easts = new List<LevelPoint>();
                foreach (LevelPoint p in _places)
                    if (LevelRanged.CrossPlace(_middle, p, true, _r) && !(_r.OneAtTheBack && InFar(p)) && _fits(east, p, out _)) easts.Add(p);
                if (easts.Count == 0) return false;
                foreach (LevelPoint w in _places)
                {
                    if (!LevelRanged.CrossPlace(_middle, w, false, _r) || (_r.OneAtTheBack && InFar(w)) || !_fits(west, w, out LevelPoint[] wm)) continue;
                    foreach (LevelPoint e in easts)
                    {
                        if (e.DistanceTo(w) < _r.CrossApartLeast - 1e-9) continue;
                        Put(west, w, wm, new RangedPlace { Post = RangedPost.Crossfire });
                        if (_fits(east, e, out LevelPoint[] em)) return Put(east, e, em, new RangedPlace { Post = RangedPost.Crossfire });
                        // Cannot be with the first standing (it never is, 16 m apart): the first is taken back.
                        Unput(west);
                        break;
                    }
                }
                return false;
            }

            void Unput(EnemyGroup g)
            {
                int i = _room.EnemyGroups.IndexOf(g);
                if (i < 0) return;
                _room.EnemyGroups.RemoveAt(i);
                _room.EnemyPoints.RemoveAt(i);
                _room.EnemyPlaces.RemoveAt(i);
                g.Ranged = null;
                _done.Remove(g);
            }

            /// <summary>Part 7: behind the room's largest melee group as seen from the door, 3 to 5 m from it, in the middle band; else short of the far band, or anywhere.</summary>
            bool PlaceWarder(EnemyGroup g)
            {
                int m = LevelRanged.LargestMelee(_room);
                LevelPoint at;
                LevelPoint[] members;
                // Each melee group as large as the largest, in the room's order.
                for (int i = 0; m >= 0 && i < _room.EnemyGroups.Count && i < _room.EnemyPoints.Count; i++)
                {
                    if (!LevelRanged.IsMelee(_room.EnemyGroups[i]) || _room.EnemyGroups[i].Count != _room.EnemyGroups[m].Count) continue;
                    LevelPoint melee = _room.EnemyPoints[i];
                    if (First(g, p => LevelRanged.PackPlace(_door, melee, p, _share(p), _r), out at, out members))
                        return Put(g, at, members, new RangedPlace { Post = RangedPost.Pack });
                }
                return FallBack(g);
            }
        }
    }
}
