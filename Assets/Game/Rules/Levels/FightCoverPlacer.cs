using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The props stage's part of the rooms you can use (work order 088), with <see cref="LevelValues.UsableRooms"/> on (the
    /// generator's version 19). Nothing is drawn: every choice below is made in a fixed order from what the room holds, so the
    /// props stream's draws are those of version 18 and the cover follows from them.
    ///
    /// The joined floor: every prop of every room but a passage is tried against <see cref="JoinedFloor.Keeps"/> after every other
    /// rule; a place it refuses counts as one the lane refused, so the prop takes its next place (it is MOVED, counted in
    /// <see cref="LevelRoom.PropsMovedForFloor"/>) or, with none left within its tries, is left out (DROPPED,
    /// <see cref="LevelRoom.PropsDroppedForFloor"/>). Moving was chosen over dropping: the prop the theme wanted stays in the room.
    ///
    /// The cover, after the room's own props (<see cref="FightCover"/> says the rules): in a room with a fight that holds shooters or
    /// casters, while its chain of tall cover does not hold and fewer than <see cref="CoverValues.TallAddMost"/> are added, a tall
    /// prop of its theme (<see cref="FightCover.TallModel"/>) that extends the longest chain it can (<see cref="PutTall"/>): the
    /// places of the room's floor that do, tried nearest the place the chain wants first (<see cref="CoverValues.FirstIdeal"/> from
    /// the line's middle toward the nearest ranged enemy for a first link, <see cref="CoverValues.StepIdeal"/> on from the chain's
    /// last toward the one nearest it for a next), outside the room's open middle square (<see cref="ShapeValues.MiddleSquare"/>)
    /// before in it, until one hides her from a ranged enemy and keeps every rule: the props' rules (an added cover is a set piece,
    /// <see cref="LevelProp.Fight"/>, <see cref="PropValues.CoverSpacing"/> from the others), the clear lane, the joined floor, the
    /// walkable share; besides, clear of every door's straight run (<see cref="ShapeValues.DoorStraightIn"/>) and of the throne's
    /// dais, and <see cref="CoverValues.SouthClear"/> north of the low south wall. Then, in a room that holds shooters, while fewer
    /// than <see cref="CoverValues.LowFewest"/> low props serve the duck and fewer than <see cref="CoverValues.LowAddMost"/> are
    /// added, a low prop of its theme (<see cref="FightCover.LowModel"/>) turned across a shooter's line (<see cref="PutLow"/>): the
    /// places of the room's floor paired with the shooters whose line through them it serves, tried with her spot nearest
    /// <see cref="CoverValues.LowIdeal"/> from the shooter and nearest the shooter's line to the line's middle first, until one
    /// serves the duck and keeps every rule.
    /// </summary>
    public static partial class PropPlacer
    {
        sealed partial class Room
        {
            JoinedFloor _floor;
            LevelValues _values;
            int _floorRefused;

            /// <summary>Work order 088: the props of this room keep the joined floor (<see cref="JoinedFloor"/>).</summary>
            public void UseJoinedFloor(LevelValues values)
            {
                _values = values;
                _floor = new JoinedFloor(_level, _room, values);
                foreach (LevelProp p in _room.Props) _floor.Block(p);
            }

            /// <summary>Would the joined floor keep its rules with the prop standing too? True without the rule.</summary>
            bool FloorKeeps(LevelProp p) => _floor == null || _floor.Keeps(p);

            // ---------------- The cover ----------------

            /// <summary>The room's cover for its fight (see the class).</summary>
            public void FightCover(LevelValues values)
            {
                _values = values;
                FightFrame f = Rules.FightCover.FrameOf(_level, _room, values);
                if (f == null || !f.HasRanged) return;
                CoverValues cv = values.CoverRules ?? new CoverValues();
                ShapeValues sv = values.ShapeRules ?? new ShapeValues();
                List<PropFootprint> runs = DoorRuns(sv.DoorStraightIn);
                // The throne room's dais: the mood stands the throne on the dais model (4 m square before it) with a great brazier
                // either side (work order 052); no cover is put where they go.
                LevelProp throne = _room.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                if (throne != null)
                {
                    PropSize ds = PropSize.Of(PropModel.Dais);
                    LevelPoint front = throne.Front;
                    double ahead = ds.Depth * 0.5 - _v.WallPieceGap - throne.Depth * 0.5;
                    runs.Add(new PropFootprint
                    {
                        Shape = PropShape.Rectangle, Centre = new LevelPoint(throne.Position.X + front.X * ahead, throne.Position.Y + front.Y * ahead),
                        Rotation = throne.Rotation, HalfWidth = ds.Width * 0.5 + 2.5, HalfDepth = ds.Depth * 0.5 + 1,
                    });
                }
                LevelPoint middle = _room.Middle(_cs);
                double halfSquare = (_room.Kind == RoomKind.Exit ? sv.ExitMiddleSquare : sv.MiddleSquare) * _cs * 0.5;
                bool InMiddle(PropFootprint fp)
                {
                    fp.Extent(out double minX, out double minY, out double maxX, out double maxY);
                    return maxX > middle.X - halfSquare && minX < middle.X + halfSquare && maxY > middle.Y - halfSquare && minY < middle.Y + halfSquare;
                }

                bool InBand(LevelPoint at)
                {
                    double d = Rules.FightCover.DepthOf(_level, f, at);
                    return d >= f.LineDepth && d <= f.FarDepth;
                }

                // The chain of tall cover (see the class).
                PropModel tall = Rules.FightCover.TallModel(_room.Theme);
                for (int added = 0; added < cv.TallAddMost; added++)
                {
                    List<CoverThing> links = Rules.FightCover.Links(_level, f, values);
                    List<LevelPoint> ends = Rules.FightCover.Ends(_level, f, links, values, out bool holds);
                    if (holds || !PutTall(f, tall, ends, runs, InMiddle, values, cv)) break;
                    _room.CoverTallAdded++;
                }

                // The low cover for the duck (see the class).
                if (!f.HasShooters) return;
                PropModel low = Rules.FightCover.LowModel(_room.Theme);
                int lowAdded = 0;
                while (lowAdded < cv.LowAddMost && Rules.FightCover.LowCount(_level, f, values) < cv.LowFewest)
                {
                    if (!PutLow(f, low, runs, values, cv)) break;
                    _room.CoverLowAdded++;
                    lowAdded++;
                }
            }

            bool HidesAny(FightFrame f, LevelProp p, LevelValues values)
            {
                CoverThing t = Rules.FightCover.PropThing(p);
                foreach (LevelPoint q in f.Ranged)
                    if (Rules.FightCover.Hides(_level, _room, t, q, values, p)) return true;
                return false;
            }

            /// <summary>
            /// One low prop for the duck (see the class): the places of the room's open floor on a half-metre grid, clear of every
            /// enemy by the props' rule, paired with each shooter whose line through one leaves her spot past the fight's line, 7 to
            /// 14 m from it (<see cref="CoverValues.LowFromShooter"/> to <see cref="CoverValues.LowIdeal"/> and 5), at most 80
            /// degrees off the shooter's line to the line's middle; tried best first (her spot's distance from the shooter nearest
            /// <see cref="CoverValues.LowIdeal"/>, a degree off the line counting as 1/20 m), each turned across its shooter's line,
            /// until one keeps every rule and serves the duck (at most <see cref="CoverLooks"/> looked at). True when one was put.
            /// </summary>
            bool PutLow(FightFrame f, PropModel low, List<PropFootprint> runs, LevelValues values, CoverValues cv)
            {
                PropSize ls = PropSize.Of(low);
                double half = ls.Shape == PropShape.Circle ? ls.Radius : ls.Depth * 0.5; // along the line: its own z, its long side across
                double least = ls.Shape == PropShape.Circle ? ls.Radius : Math.Min(ls.Width, ls.Depth) * 0.5;
                var enemies = new List<LevelPoint>();
                for (int i = 0; i < _room.EnemyPoints.Count; i++) enemies.AddRange(_room.PlacesAt(i));
                var places = new List<(double score, LevelPoint at, LevelPoint q)>();
                CellRect r = _room.Rect;
                const double step = 0.5;
                int nx = (int)Math.Round(r.Width * _cs / step), ny = (int)Math.Round(r.Height * _cs / step);
                var known = new byte[nx * ny]; // 0 not asked yet, 1 a place a prop may stand, 2 not
                double far = cv.LowIdeal + 5, cosMost = Math.Cos(80 * Math.PI / 180.0);
                foreach (LevelPoint q in f.Shooters)
                {
                    LevelPoint w = UnitTo(q, f.LineMiddle);
                    int i0 = Math.Max(0, (int)Math.Floor((q.X - far - r.X * _cs) / step)), i1 = Math.Min(nx - 1, (int)Math.Ceiling((q.X + far - r.X * _cs) / step));
                    int j0 = Math.Max(0, (int)Math.Floor((q.Y - far - r.Y * _cs) / step)), j1 = Math.Min(ny - 1, (int)Math.Ceiling((q.Y + far - r.Y * _cs) / step));
                    for (int i = i0; i <= i1; i++)
                        for (int j = j0; j <= j1; j++)
                        {
                            var at = new LevelPoint(r.X * _cs + (i + 0.5) * step, r.Y * _cs + (j + 0.5) * step);
                            double d = q.DistanceTo(at), toSpot = d + half + cv.DuckSpot;
                            if (toSpot < cv.LowFromShooter || toSpot > far || d < 1e-9) continue;
                            var u = new LevelPoint((at.X - q.X) / d, (at.Y - q.Y) / d);
                            double cos = u.X * w.X + u.Y * w.Y;
                            if (cos < cosMost) continue;
                            int k = j * nx + i;
                            if (known[k] == 0)
                            {
                                bool ok = _room.IsOpen((int)Math.Floor(at.X / _cs), (int)Math.Floor(at.Y / _cs));
                                if (ok && SurelyFaulted(at, least, enemies)) ok = false;
                                if (ok && RoomShapes.DistanceToWalls(_room, at, _cs) < _v.WallClearance + least) ok = false;
                                if (ok && TooNear(at, least)) ok = false;
                                known[k] = ok ? (byte)1 : (byte)2;
                            }
                            if (known[k] != 1) continue;
                            var spot = new LevelPoint(at.X + u.X * (half + cv.DuckSpot), at.Y + u.Y * (half + cv.DuckSpot));
                            if (Rules.FightCover.DepthOf(_level, f, spot) < f.LineDepth + 0.25) continue;
                            double off = Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180.0 / Math.PI;
                            places.Add((Math.Abs(toSpot - cv.LowIdeal) + off / 20.0, at, q));
                        }
                }
                long[] order = Order(places.Count, i => places[i].score);
                int tries = 0, looked = 0;
                foreach (long key in order)
                {
                    (double score, LevelPoint at, LevelPoint q) = places[(int)(key & IndexMask)];
                    if (++looked > CoverLooks) break;
                    LevelProp p = Cover(low, at, AcrossTurn(low, UnitTo(q, at)));
                    if (!Clear(p, runs) || PropRules.Fault(_level, _room, p, _room.Props, _v, null, _niches) != null) continue;
                    if (!Rules.FightCover.LowServes(_level, f, p, q, values, p)) continue;
                    if (TryAddCover(p)) return true;
                    if (++tries >= CoverTries) break;
                }
                return false;
            }

            /// <summary>Places an added cover tries that keep the props' rules but not the lane, the joined floor or the walkable share, at most, before the room is left with what it has.</summary>
            const int CoverTries = 100;

            /// <summary>Chains' ends an added tall cover is tried after, at most (the longest chains' first).</summary>
            const int EndsTried = 4;

            /// <summary>Places an added cover is looked at, at most, before the room is left with what it has (the rest are worse).</summary>
            const int CoverLooks = 1500;

            /// <summary>
            /// One tall cover that extends a chain (see the class): every place of the room's open floor on a half-metre grid, clear of
            /// every enemy by the props' rule, between the fight's line and the far band, paired with the first of the chain's ends
            /// (<paramref name="ends"/>, the longest chain's first; then a new first link from the line's middle) it extends: within
            /// <see cref="CoverValues.StepMost"/> of it and nearer the ranged enemies (a first link: within
            /// <see cref="CoverValues.FirstMost"/> of the line's middle), at least <see cref="CoverValues.StepLeast"/> from it;
            /// tried best first (outside the open middle before in it, then nearest the wanted place, 2 m counted for each end after
            /// the first: <see cref="CoverValues.StepIdeal"/> on from the end toward its nearest ranged enemy,
            /// <see cref="CoverValues.FirstIdeal"/> from the line's middle toward its nearest), until one hides her from a ranged enemy
            /// and keeps every rule. True when one was put.
            /// </summary>
            bool PutTall(FightFrame f, PropModel tall, List<LevelPoint> ends, List<PropFootprint> runs, Func<PropFootprint, bool> inMiddle, LevelValues values, CoverValues cv)
            {
                PropSize ts = PropSize.Of(tall);
                double least = ts.Shape == PropShape.Circle ? ts.Radius : Math.Min(ts.Width, ts.Depth) * 0.5;
                var sources = new List<(LevelPoint from, bool first, LevelPoint ideal, double toRanged)>();
                foreach (LevelPoint e in ends.Take(EndsTried)) // the longest chains' ends
                {
                    LevelPoint u = UnitTo(e, f.NearestRanged(e));
                    sources.Add((e, false, new LevelPoint(e.X + u.X * cv.StepIdeal, e.Y + u.Y * cv.StepIdeal), f.ToRanged(e)));
                }
                if (ends.Count == 0) // a first link only while there is none: else a chain is extended
                {
                    LevelPoint u = UnitTo(f.LineMiddle, f.NearestRanged(f.LineMiddle));
                    sources.Add((f.LineMiddle, true, new LevelPoint(f.LineMiddle.X + u.X * cv.FirstIdeal, f.LineMiddle.Y + u.Y * cv.FirstIdeal), double.PositiveInfinity));
                }
                var enemies = new List<LevelPoint>();
                for (int i = 0; i < _room.EnemyPoints.Count; i++) enemies.AddRange(_room.PlacesAt(i));
                var places = new List<(double score, LevelPoint at)>();
                CellRect r = _room.Rect;
                const double step = 0.5;
                for (double x = r.X * _cs + step * 0.5; x < r.Right * _cs; x += step)
                    for (double y = r.Y * _cs + step * 0.5; y < r.Top * _cs; y += step)
                    {
                        var at = new LevelPoint(x, y);
                        double depth = Rules.FightCover.DepthOf(_level, f, at);
                        if (depth < f.LineDepth || depth > f.FarDepth) continue;
                        if (!_room.IsOpen((int)Math.Floor(x / _cs), (int)Math.Floor(y / _cs))) continue;
                        bool reach = false;
                        foreach ((LevelPoint from, bool first, LevelPoint ideal, double fromRanged) in sources)
                            reach |= at.DistanceTo(from) <= (first ? cv.FirstMost : cv.StepMost);
                        if (!reach) continue;
                        if (SurelyFaulted(at, least, enemies) || RoomShapes.DistanceToWalls(_room, at, _cs) < _v.WallClearance + least || TooNear(at, least)) continue;
                        double toRanged = f.ToRanged(at);
                        for (int k = 0; k < sources.Count; k++)
                        {
                            (LevelPoint from, bool first, LevelPoint ideal, double fromRanged) = sources[k];
                            if (first ? at.DistanceTo(from) > cv.FirstMost : at.DistanceTo(from) > cv.StepMost || at.DistanceTo(from) < cv.StepLeast || toRanged >= fromRanged) continue;
                            bool middle = inMiddle(PropFootprint.Of(ts, at, 0));
                            places.Add(((middle ? 1e6 : 0) + k * 2 + at.DistanceTo(ideal), at));
                            break;
                        }
                    }
                long[] order = Order(places.Count, i => places[i].score);
                int tries = 0, looked = 0;
                foreach (long key in order)
                {
                    (double score, LevelPoint at) = places[(int)(key & IndexMask)];
                    if (++looked > CoverLooks) break;
                    LevelProp p = Cover(tall, at, 0);
                    if (!Clear(p, runs) || !SouthClear(p.Footprint, cv.SouthClear)) continue;
                    if (PropRules.Fault(_level, _room, p, _room.Props, _v, null, _niches) != null || !HidesAny(f, p, values)) continue;
                    if (TryAddCover(p)) return true;
                    if (++tries >= CoverTries) break;
                }
                return false;
            }

            /// <summary>
            /// A cover whose footprint holds a circle of <paramref name="inner"/> round <paramref name="at"/> surely breaks a rule of the
            /// props there (<see cref="PropRules.Fault"/>): nearer an enemy than <see cref="PropValues.FromPoints"/>, or in the exit
            /// room nearer its middle than <see cref="PropValues.BossMiddleClear"/>. A quick answer before the rules' own.
            /// </summary>
            bool SurelyFaulted(LevelPoint at, double inner, List<LevelPoint> enemies)
            {
                foreach (LevelPoint e in enemies)
                    if (e.DistanceTo(at) - inner < _v.FromPoints - 1e-9) return true;
                return _room.Kind == RoomKind.Exit && _room.Middle(_cs).DistanceTo(at) - inner < _v.BossMiddleClear - 1e-9;
            }

            /// <summary>
            /// A cover whose footprint holds a circle of <paramref name="inner"/> round <paramref name="at"/> is surely nearer a prop of
            /// the room than the props' rules let it stand (<see cref="PropValues.CoverSpacing"/> from a prop of the open floor or
            /// another cover, apart from the rest): the inner circles of the two are nearer than that. A quick answer before the rules'
            /// own (<see cref="PropRules.Fault"/>), which every place it lets by still keeps.
            /// </summary>
            bool TooNear(LevelPoint at, double inner)
            {
                foreach (LevelProp o in _room.Props)
                {
                    if (PropSize.Of(o.Model).Walkable) continue;
                    double oi = o.Shape == PropShape.Circle ? o.Radius : Math.Min(o.Width, o.Depth) * 0.5;
                    double needs = o.Place == PropPlace.Floor || o.Fight ? _v.CoverSpacing : 0;
                    if (at.DistanceTo(o.Position) - inner - oi < needs - 1e-9) return true;
                }
                return false;
            }

            const long IndexMask = (1L << 22) - 1;

            /// <summary>
            /// The places' order, best first: each one's score to a millimetre, and of two as good the one made first (a key of the
            /// score and the index, so the order is the same in every runtime); the index is the key's low bits (<see cref="IndexMask"/>).
            /// </summary>
            static long[] Order(int count, Func<int, double> score)
            {
                var keys = new long[count];
                for (int i = 0; i < count; i++) keys[i] = ((long)Math.Round(score(i) * 1000.0) << 22) | (long)i;
                Array.Sort(keys);
                return keys;
            }

            /// <summary>A cover put for the room's fight: a set piece (<see cref="PropPlace.Set"/>), marked <see cref="LevelProp.Fight"/>.</summary>
            static LevelProp Cover(PropModel model, LevelPoint at, int turn)
            {
                LevelProp p = LevelProp.Make(model, at, turn, PropPlace.Set);
                p.Fight = true;
                return p;
            }

            static LevelPoint UnitTo(LevelPoint from, LevelPoint to)
            {
                double dx = to.X - from.X, dy = to.Y - from.Y, l = Math.Sqrt(dx * dx + dy * dy);
                return l < 1e-9 ? new LevelPoint(0, 1) : new LevelPoint(dx / l, dy / l);
            }

            /// <summary>
            /// The turn that lays a model's long side (its own x) across a line along <paramref name="u"/>: its own x along the line's
            /// normal, rounded to the turns the model may take (15 degrees, or 90).
            /// </summary>
            static int AcrossTurn(PropModel model, LevelPoint u)
            {
                // Its own x on the ground is (cos r, -sin r) for a turn r clockwise; the normal of u is (-u.Y, u.X).
                double r = Math.Atan2(-u.X, -u.Y) * 180.0 / Math.PI;
                int step = PropSize.Of(model).AnyAngle ? 15 : 90;
                int turn = (int)Math.Round(r / step) * step;
                return ((turn % 360) + 360) % 360;
            }

            /// <summary>The straight run behind each door of the room (<see cref="ShapeValues.DoorStraightIn"/>), as rectangles on its floor.</summary>
            List<PropFootprint> DoorRuns(double run)
            {
                var list = new List<PropFootprint>();
                foreach (LevelDoor d in _level.DoorsOf(_room.Order))
                {
                    int wall = LevelGeometry.WallOf(_room.Rect, d);
                    if (wall < 0) continue;
                    d.Opening(_cs, out LevelPoint a, out LevelPoint b);
                    LevelPoint into = PropRules.Inward(wall);
                    double half = d.Width * _cs * 0.5;
                    var centre = new LevelPoint((a.X + b.X) * 0.5 + into.X * run * 0.5, (a.Y + b.Y) * 0.5 + into.Y * run * 0.5);
                    bool acrossX = !d.Vertical; // the opening runs along x on a north or south wall
                    list.Add(new PropFootprint
                    {
                        Shape = PropShape.Rectangle, Centre = centre, Rotation = 0,
                        HalfWidth = acrossX ? half : run * 0.5, HalfDepth = acrossX ? run * 0.5 : half,
                    });
                }
                return list;
            }

            /// <summary>The added cover keeps out of the doors' straight runs (and the throne's dais), and off the carved cells.</summary>
            bool Clear(LevelProp p, List<PropFootprint> runs)
            {
                if (_room.IsSolid((int)Math.Floor(p.Position.X / _cs), (int)Math.Floor(p.Position.Y / _cs))) return false;
                PropFootprint f = p.Footprint;
                foreach (PropFootprint r in runs)
                    if (f.DistanceTo(r) <= 1e-9) return false;
                return true;
            }

            /// <summary>
            /// An added tall cover keeps <paramref name="clear"/> north of the room's low south wall and of every carved cell's low
            /// north face below it: the camera looks over them at her.
            /// </summary>
            bool SouthClear(PropFootprint f, double clear)
            {
                if (f.DistanceToSide(_room.Rect, _cs, LevelGenerator.South) < clear - 1e-9) return false;
                if (_room.SolidCount == 0) return true;
                f.Extent(out double minX, out double minY, out double maxX, out _);
                foreach ((int x, int y) in _room.SolidList)
                {
                    double top = (y + 1) * _cs;
                    if (top > minY + 1e-9 || minY - top >= clear - 1e-9) continue;
                    if (maxX > x * _cs + 1e-9 && minX < (x + 1) * _cs - 1e-9) return false;
                }
                return true;
            }

            double _walkLeft = double.NaN; // work order 088: the floor's walkable squares' area an added cover may still take

            /// <summary>
            /// Adds an added cover if it keeps every rule a prop keeps (<see cref="PropRules.Fault"/>), the clear lane, the joined
            /// floor and, in a combat room, the walkable share; true when it did. The share is reckoned as the themed props' is
            /// (<see cref="PropValues.MinWalkable"/>): the ground a prop takes is its footprint grown by the walk radius, which never
            /// counts less than the validator's grid does, taken from what the room's share leaves above the least; when that says
            /// no, the validator's own grid is asked.
            /// </summary>
            bool TryAddCover(LevelProp p)
            {
                double grown = 0;
                if (_room.Kind == RoomKind.Combat)
                {
                    if (double.IsNaN(_walkLeft))
                        _walkLeft = (PropRules.WalkableShare(_level, _room, _v) - _v.MinWalkable) * PropRules.FloorSquares(_level, _room, _v) * _v.LaneGrid * _v.LaneGrid;
                    double r = _v.WalkRadius;
                    grown = p.Shape == PropShape.Circle ? Math.PI * (p.Radius + r) * (p.Radius + r) : (p.Width + 2 * r) * (p.Depth + 2 * r);
                }
                if (PropRules.Fault(_level, _room, p, _room.Props, _v, null, _niches) != null) return false;
                if (!_lane.Clear(p.Footprint)) return false;
                if (!FloorKeeps(p)) return false;
                if (_room.Kind == RoomKind.Combat)
                {
                    if (grown > _walkLeft)
                    {
                        // The quick reckoning says no: the validator's own grid, with it standing.
                        _room.Props.Add(p);
                        double share = PropRules.WalkableShare(_level, _room, _v);
                        _room.Props.RemoveAt(_room.Props.Count - 1);
                        if (share < _v.MinWalkable - 1e-9) return false;
                        _walkLeft = (share - _v.MinWalkable) * PropRules.FloorSquares(_level, _room, _v) * _v.LaneGrid * _v.LaneGrid;
                    }
                    else _walkLeft -= grown;
                }
                _room.Props.Add(p);
                _lane.Block(p.Footprint);
                _floor?.Block(p);
                return true;
            }
        }
    }
}
