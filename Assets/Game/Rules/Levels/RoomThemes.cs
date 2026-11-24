using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// What a room is (work order 028; Kristian, 2026-11-19 and 2026-11-20: "good variety", and the same pieces everywhere
    /// make the floors alike, so one room is to differ from the next by what it is, not only by its shape). None: dressed
    /// as before work order 028 (the entrance always; every room with <see cref="LevelValues.Themes"/> off).
    /// </summary>
    public enum RoomTheme { None, TombHall, Ossuary, Shrine, Archive, ThroneRoom, Vault, ExitHall }

    /// <summary>
    /// The room themes (work order 028): which room gets which, drawn from the floor's seed on a stream of its own
    /// (<see cref="Stage"/>), so that no other stage's draws move; and what each recipe's stances mean
    /// (<see cref="PropPlacer"/> places them). The recipes are data (<see cref="ThemeValues"/>).
    ///
    /// Which room: the exit room is always the throne room (work order 089: the exit hall instead on floors 1 to 4 with
    /// <see cref="LevelValues.ExitRoomsOfTheirOwn"/> on, and in an exit room of its own: <see cref="ExitRooms.ThemeOf"/>); the
    /// entrance has none. The combat rooms are drawn by their
    /// size (each theme's weight from <see cref="ThemeRecipe.SmallWeight"/> in the smallest combat room to
    /// <see cref="ThemeRecipe.LargeWeight"/> in the largest, by floor area), the elite room first with
    /// <see cref="ThemeRecipe.EliteWeight"/> (the shrine is favoured there), then the others along the chain, each never
    /// the theme of a combat room next to it on the chain while another is left: no theme twice in a row. Each treasure
    /// room is a vault about <see cref="ThemeValues.VaultShare"/> of the time, else an archive, but never an archive off an
    /// archive. Every combat room draws one number and every treasure room one, whatever it gets.
    /// </summary>
    public static class RoomThemes
    {
        public const string Stage = "themes";

        /// <summary>The themes a combat room may have, in the table's order.</summary>
        public static readonly RoomTheme[] CombatThemes = { RoomTheme.TombHall, RoomTheme.Ossuary, RoomTheme.Shrine, RoomTheme.Archive };

        /// <summary>The theme's name in words, as the map's legend and the play session recorder say it.</summary>
        public static string Word(RoomTheme theme)
        {
            switch (theme)
            {
                case RoomTheme.TombHall: return "tomb hall";
                case RoomTheme.Ossuary: return "ossuary";
                case RoomTheme.Shrine: return "shrine";
                case RoomTheme.Archive: return "archive";
                case RoomTheme.ThroneRoom: return "throne room";
                case RoomTheme.Vault: return "vault";
                case RoomTheme.ExitHall: return "exit hall"; // work order 089
                default: return "";
            }
        }

        /// <summary>The themes a room of this kind may have.</summary>
        public static RoomTheme[] ThemesFor(RoomKind kind) =>
            kind == RoomKind.Combat ? CombatThemes
            : kind == RoomKind.Treasure ? new[] { RoomTheme.Vault, RoomTheme.Archive }
            : kind == RoomKind.Exit ? new[] { RoomTheme.ThroneRoom, RoomTheme.ExitHall } // work order 089: the exit hall of floors 1 to 4
            : new RoomTheme[0];

        /// <summary>Every model a recipe may put down.</summary>
        public static PropModel[] ModelsOf(ThemeRecipe recipe) =>
            recipe.Props.Where(p => p != null && p.Models != null).SelectMany(p => p.Models).Distinct().ToArray();

        /// <summary>Where a room is between the smallest (0) and the largest (1) of its kind, by floor area, as the combat rooms' prop counts were.</summary>
        public static double SizeShare(LevelRoom room, RoomSizeRange size)
        {
            // Work order 058: a template room's open cells.
            int cells = room.Rect.Width * room.Rect.Height - (room.Template != null ? room.SolidCount : 0), fewest = size.Min * size.Min, most = size.Max * size.Max;
            return most > fewest ? Math.Max(0, Math.Min(1, (cells - fewest) / (double)(most - fewest))) : 0;
        }

        /// <summary>How many of a recipe's line a room of this size share holds.</summary>
        public static int CountOf(ThemeProp line, double t) =>
            line.Fewest + (int)Math.Round(t * (line.Most - line.Fewest), MidpointRounding.AwayFromZero);

        static double Unit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Gives each room its theme (see the class), from the stream given.</summary>
        public static void Assign(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            ThemeValues tv = v.ThemeRules ?? new ThemeValues();
            foreach (LevelRoom room in level.Rooms) room.Theme = RoomTheme.None;
            LevelRoom exit = level.ExitRoom;
            RoomTheme exitTheme = ExitRooms.ThemeOf(exit, level.Floor, v); // work order 089: the throne room, or the exit hall; no draw either way
            if (exit != null && tv.Of(exitTheme, RoomKind.Exit) != null) exit.Theme = exitTheme;

            List<LevelRoom> chain = LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();
            foreach (LevelRoom room in chain.Where(r => r.Elite).Concat(chain.Where(r => !r.Elite)).ToList())
            {
                int at = chain.IndexOf(room);
                var near = new HashSet<RoomTheme>();
                if (at > 0) near.Add(chain[at - 1].Theme);
                if (at + 1 < chain.Count) near.Add(chain[at + 1].Theme);
                double t = SizeShare(room, v.Plan.CombatSize);
                var options = CombatThemes.Where(th => tv.Of(th, RoomKind.Combat) != null && !near.Contains(th)).ToList();
                if (options.Count == 0) options = CombatThemes.Where(th => tv.Of(th, RoomKind.Combat) != null).ToList();
                double u = Unit(rng);
                if (options.Count == 0) continue;
                var weights = options.Select(th =>
                {
                    ThemeRecipe r = tv.Of(th, RoomKind.Combat);
                    double w = r.SmallWeight + t * (r.LargeWeight - r.SmallWeight);
                    return Math.Max(0, w * (room.Elite ? r.EliteWeight : 1f) * RoomTemplates.SuitsFactor(room, th, v) // work order 058: a theme its template suits
                                       * FloorCharacterRules.ThemeWeight(th, level.Floor, v)); // work order 104: the floor's theme
                }).ToList();
                double total = weights.Sum(), pick = u * total;
                room.Theme = options[options.Count - 1];
                for (int i = 0; i < options.Count; i++)
                {
                    if (pick < weights[i])
                    {
                        room.Theme = options[i];
                        break;
                    }
                    pick -= weights[i];
                }
            }

            foreach (LevelRoom room in level.Rooms.Where(r => r.Kind == RoomKind.Treasure))
            {
                double u = Unit(rng);
                LevelDoor door = level.DoorsOf(room.Order).FirstOrDefault();
                LevelRoom parent = door != null && door.Other(room.Order) >= 0 && door.Other(room.Order) < level.Rooms.Count ? level.Rooms[door.Other(room.Order)] : null;
                // Work order 059: on a floor with passages the room before it is past its passage.
                if (parent != null && parent.Kind == RoomKind.Passage)
                {
                    int before = Passages.RoomBefore(level, room);
                    parent = before >= 0 && before < level.Rooms.Count ? level.Rooms[before] : null;
                }
                bool vault = u < tv.VaultShare || (parent != null && parent.Theme == RoomTheme.Archive);
                RoomTheme theme = vault ? RoomTheme.Vault : RoomTheme.Archive;
                if (tv.Of(theme, RoomKind.Treasure) == null) theme = vault ? RoomTheme.Archive : RoomTheme.Vault;
                room.Theme = tv.Of(theme, RoomKind.Treasure) != null ? theme : RoomTheme.None;
            }
        }
    }

    /// <summary>
    /// The themed rooms' props (work order 028): each line of the room's recipe in order, as many as the room's size
    /// gives (<see cref="RoomThemes.CountOf"/>), each model drawn (one for a pair), each put at the first of the places its
    /// stance tries that keeps every rule of <see cref="PropRules"/> (the clear lane last), or left out. Nothing is put
    /// down that would leave a combat room less than <see cref="PropValues.MinWalkable"/> of its floor (reckoned on the
    /// footprints grown by <see cref="PropValues.WalkRadius"/>, which never counts less ground taken than the validator's
    /// grid does), nor in a recipe's clear middle.
    ///
    /// The stances. Row: along the room's long axis (drawn when square), on two rows at <see cref="ThemeValues.RowAcross"/>
    /// across it, from the middle out, long side along the row, head or foot drawn; the open floor's spacing keeps lanes
    /// between them. Cluster: round two drawn middles at least <see cref="ThemeValues.ClusterFromWalls"/> from the walls,
    /// within <see cref="ThemeValues.ClusterReach"/> of one, nearest first, in a drawn order within bands of
    /// <see cref="ThemeValues.ClusterBand"/>. CornerOrWall: a drawn one of
    /// the two first, the other after: near a drawn corner, or at a wall. AlongWall: at a wall, facing into the room, at a
    /// drawn place; a wall piece with its back <see cref="PropValues.WallPieceGap"/> past the wall's face and never at the
    /// south wall, nor anything taller than <see cref="PropValues.SouthWallMostHeight"/>. OpenFloor: anywhere at least
    /// <see cref="ThemeValues.OpenFromWalls"/> from the walls, drawn. EdgeFloor: within <see cref="ThemeValues.EdgeBand"/>
    /// of a wall, drawn. Ring: pillars round the middle at
    /// <see cref="ThemeValues.RingShare"/> of the short side, or (drawn) in two rows as the Row's, each near its place.
    /// FarAltar: at the middle of the wall opposite the door in, or the nearest place along it, far enough out for the
    /// statue behind it (BehindAltar: at the wall right behind it, else along that wall); FlankAltar: either side of it;
    /// BeforeAltar: before its front. BesideShelf: beside an end of a bookshelf placed before it. Throne: at the north
    /// wall's middle facing south, else the nearest place along that wall, else the side walls (the one farther from the
    /// door in first) facing into the room; never the south wall. FlankThrone: either side of it. CornerPillars: the boss
    /// room's corner pillars as before. Chest: the treasure room's chest as before; FlankChest: either side of it as the
    /// braziers stood.
    /// </summary>
    public static partial class PropPlacer
    {
        sealed partial class Room
        {
            ThemeValues _tv;
            ThemeRecipe _recipe;
            LevelProp _altar, _throne, _chest;
            LevelPoint _altarWanted;
            int _farSide = LevelGenerator.North;
            readonly List<LevelProp> _shelves = new List<LevelProp>();
            double _grownArea, _mostGrownArea = double.MaxValue;

            double X0 => _room.Rect.X * _cs;
            double X1 => _room.Rect.Right * _cs;
            double Y0 => _room.Rect.Y * _cs;
            double Y1 => _room.Rect.Top * _cs;

            public void Themed(LevelRandom rng, LevelValues values, List<PropRules.WallSpan> niches)
            {
                _tv = values.ThemeRules ?? new ThemeValues();
                _niches = niches;
                _recipe = _tv.Of(_room.Theme, _room.Kind);
                _bank = values.RoomMood && values.Mood != null && values.Mood.Compose; // work order 044
                _daisRoom = _bank ? Math.Max(0, values.Mood.DaisMargin) + 0.1 : 0; // work order 051
                _modelDais = _bank; // and room for the dais model of work order 052 (4 m square) first; not its switch: the generator never reads it
                if (_recipe == null || _recipe.Props == null) return;
                if (_room.Kind == RoomKind.Combat)
                {
                    double r = _v.WalkRadius, floor = Math.Max(0, (X1 - X0 - 2 * r) * (Y1 - Y0 - 2 * r));
                    // Work order 051: a carved room's floor is its outline's, counted on the validator's own grid.
                    if (_room.SolidCount > 0) floor = PropRules.FloorSquares(_level, _room, _v) * _v.LaneGrid * _v.LaneGrid;
                    _mostGrownArea = (1 - _v.MinWalkable) * floor;
                }
                double t = RoomThemes.SizeShare(_room, values.Plan.SizeOf(_room.Kind));
                foreach (ThemeProp line in _recipe.Props)
                {
                    if (line == null || line.Models == null || line.Models.Length == 0) continue;
                    int n = RoomThemes.CountOf(line, t);
                    if (n <= 0) continue;
                    PropModel pair = line.Models[rng.Below(line.Models.Length)];
                    PropModel Pick() => line.OneModel || line.Models.Length == 1 ? pair : line.Models[rng.Below(line.Models.Length)];
                    switch (line.Stance)
                    {
                        case PropStance.Row: Row(n, Pick, rng); break;
                        case PropStance.Cluster: Cluster(n, Pick, rng); break;
                        case PropStance.CornerOrWall: CornerOrWall(n, Pick, rng); break;
                        case PropStance.AlongWall: for (int k = 0; k < n; k++) AlongWall(Pick(), rng); break;
                        case PropStance.OpenFloor: for (int k = 0; k < n; k++) OpenFloor(Pick(), rng, double.MaxValue); break;
                        case PropStance.EdgeFloor: for (int k = 0; k < n; k++) OpenFloor(Pick(), rng, _tv.EdgeBand); break;
                        case PropStance.Ring: Ring(n, Pick, rng); break;
                        case PropStance.FarAltar: for (int k = 0; k < n; k++) FarAltar(Pick()); break;
                        case PropStance.BehindAltar: for (int k = 0; k < n; k++) BehindAltar(Pick()); break;
                        case PropStance.FlankAltar: for (int k = 0; k < n; k++) Flank(Pick(), _altar, _altarWanted, _farSide, k, _tv.FlankGap, false); break;
                        case PropStance.BeforeAltar: for (int k = 0; k < n; k++) BeforeAltar(Pick()); break;
                        case PropStance.BesideShelf: for (int k = 0; k < n; k++) BesideShelf(Pick(), k, rng); break;
                        case PropStance.Throne: for (int k = 0; k < n; k++) Throne(Pick()); break;
                        case PropStance.FlankThrone:
                            for (int k = 0; k < n; k++)
                                Flank(Pick(), _throne, OnWall(LevelGenerator.North, (X0 + X1) * 0.5, 0), _throne != null ? SideOf(_throne) : LevelGenerator.North, k, _tv.FlankGap, true);
                            break;
                        case PropStance.CornerPillars: CornerPillars(n, Pick, rng); break;
                        case PropStance.Chest: for (int k = 0; k < n; k++) Chest(Pick()); break;
                        case PropStance.FlankChest: for (int k = 0; k < n; k++) FlankChest(Pick(), k); break;
                    }
                }
            }

            // ---------------- Putting one down ----------------

            /// <summary>The ground a prop takes for the walkable reckoning: its footprint grown by the walk radius.</summary>
            double GrownArea(LevelProp p)
            {
                double r = _v.WalkRadius;
                return p.Shape == PropShape.Circle ? Math.PI * (p.Radius + r) * (p.Radius + r) : (p.Width + 2 * r) * (p.Depth + 2 * r);
            }

            /// <summary>A prop at the first of the places that keeps every rule, the theme's own wishes besides; null if none.</summary>
            LevelProp Put(PropModel model, IEnumerable<(LevelPoint at, int rotation, PropPlace place)> places)
            {
                Want(1);
                int laneTries = 0;
                LevelPoint middle = _room.Middle(_cs);
                // A prop this near the middle closes the lane's way to it whatever else stands: no need to ask the lane.
                double closes = _room.Kind == RoomKind.Treasure ? -1 : _v.LaneWidth * 0.5 - _v.LaneGrid;
                foreach ((LevelPoint at, int rotation, PropPlace place) in places)
                {
                    if (laneTries >= _v.LaneTriesPerProp) break;
                    // Work order 051: a place on a carved cell keeps no rule; passed at once (the rules would refuse it the same).
                    if (_room.SolidCount > 0 && _room.IsSolid((int)Math.Floor(at.X / _cs), (int)Math.Floor(at.Y / _cs))) continue;
                    LevelProp p = LevelProp.Make(model, at, ((rotation % 360) + 360) % 360, place);
                    if (_grownArea + GrownArea(p) > _mostGrownArea) continue;
                    PropFootprint f = p.Footprint;
                    double fromMiddle = f.DistanceTo(middle);
                    if (fromMiddle < closes || (_recipe.ClearMiddle > 0 && fromMiddle < _recipe.ClearMiddle)) continue;
                    if (Crowds(p, f)) continue;
                    if (!TryAdd(p, ref laneTries)) continue;
                    _grownArea += GrownArea(p);
                    return p;
                }
                Missed(laneTries);
                return null;
            }

            /// <summary>
            /// True when the prop overlaps one already in the room or stands nearer one than the open floor keeps: a fault
            /// <see cref="PropRules.Fault"/> finds too, asked first because it is the one most places of a theme break.
            /// </summary>
            bool Crowds(LevelProp p, PropFootprint f)
            {
                foreach (LevelProp o in _room.Props)
                {
                    double apart = f.DistanceTo(o.Footprint);
                    if (apart <= 1e-9) return true;
                    if ((p.Place == PropPlace.Floor || o.Place == PropPlace.Floor) && apart < _v.FloorSpacing - 1e-9) return true;
                }
                return false;
            }

            /// <summary>Places round a wanted one, nearest first, within <see cref="PropValues.SetSearch"/>.</summary>
            IEnumerable<(LevelPoint, int, PropPlace)> Nearby(LevelPoint wanted, int rotation, PropPlace place) =>
                Around(_v.SetSearch).Select(o => (new LevelPoint(wanted.X + o.x, wanted.Y + o.y), rotation, place));

            static int Turn(PropModel model, LevelRandom rng) => PropSize.Of(model).AnyAngle ? 15 * rng.Below(24) : 90 * rng.Below(4);

            /// <summary>The turn that has a model's front (or its +Z) face along (x, y).</summary>
            static int Facing(PropModel model, LevelPoint d) => (PropRules.Facing(d.X, d.Y) + PropSize.Of(model).FrontTurn) % 360;

            /// <summary>Half the most a footprint reaches from its middle along a side: a circle's radius, a box's larger half side.</summary>
            static double Reach(PropModel model)
            {
                PropSize s = PropSize.Of(model);
                return s.Shape == PropShape.Circle ? s.Radius : Math.Max(s.Width, s.Depth) * 0.5;
            }

            List<LevelPoint> _grid;

            /// <summary>A new list of the open floor's places, every <see cref="PropValues.PlaceStep"/>, in their first order.</summary>
            List<LevelPoint> Grid()
            {
                if (_grid != null) return new List<LevelPoint>(_grid);
                var grid = _grid = new List<LevelPoint>();
                double step = _v.PlaceStep;
                for (double x = X0 + step; x < X1 - 1e-9; x += step)
                    for (double y = Y0 + step; y < Y1 - 1e-9; y += step)
                        grid.Add(new LevelPoint(x, y));
                return new List<LevelPoint>(grid);
            }

            // A wall's line: where along it a point is, a point at a depth into the room from it, and its ends.
            static bool AlongX(int side) => side == LevelGenerator.North || side == LevelGenerator.South;
            static double AlongOf(int side, LevelPoint p) => AlongX(side) ? p.X : p.Y;

            LevelPoint OnWall(int side, double along, double depth)
            {
                switch (side)
                {
                    case LevelGenerator.North: return new LevelPoint(along, Y1 - depth);
                    case LevelGenerator.South: return new LevelPoint(along, Y0 + depth);
                    case LevelGenerator.East: return new LevelPoint(X1 - depth, along);
                    default: return new LevelPoint(X0 + depth, along);
                }
            }

            void Ends(int side, out double lo, out double hi)
            {
                lo = AlongX(side) ? X0 : Y0;
                hi = AlongX(side) ? X1 : Y1;
            }

            int SideOf(LevelProp p) => p.Footprint.NearestSide(_room.Rect, _cs, out _);

            /// <summary>Places along a wall at a depth from its line, from <paramref name="from"/> outward both ways, every step.</summary>
            List<LevelPoint> Sweep(int side, double depth, double from)
            {
                Ends(side, out double lo, out double hi);
                var list = new List<LevelPoint>();
                double step = _v.PlaceStep;
                for (int s = 0; ; s++)
                {
                    double a = from + s * step, b = from - s * step;
                    bool any = false;
                    if (a > lo && a < hi) { list.Add(OnWall(side, a, depth)); any = true; }
                    if (s > 0 && b > lo && b < hi) { list.Add(OnWall(side, b, depth)); any = true; }
                    if (!any) break;
                }
                return list;
            }

            /// <summary>Every place at a wall for this model, its back at the wall and facing into the room, in a drawn order.</summary>
            List<(LevelPoint, int, PropPlace)> WallPlaces(PropModel model, LevelRandom rng)
            {
                PropSize s = PropSize.Of(model);
                double back = s.WallPiece ? _v.WallFace + _v.WallPieceGap : _v.AtWallGap;
                double depth = back + (s.Shape == PropShape.Circle ? s.Radius : s.Depth * 0.5);
                bool south = !s.WallPiece && s.Height <= _v.SouthWallMostHeight + 1e-3;
                var places = new List<(LevelPoint, int, PropPlace)>();
                double step = _v.PlaceStep;
                foreach (int side in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                {
                    if (side == LevelGenerator.South && !south) continue;
                    Ends(side, out double lo, out double hi);
                    int turn = Facing(model, PropRules.Inward(side));
                    for (double a = lo + step; a < hi - 1e-9; a += step) places.Add((OnWall(side, a, depth), turn, PropPlace.Wall));
                }
                // Work order 051: the carved cells' faces are walls too (an alcove's back, a pier's side); a low one as the south wall.
                foreach (WallFace face in RoomShapes.CarvedFaces(_room))
                {
                    if (face.Low && !south) continue;
                    int turn = Facing(model, PropRules.Inward(face.Side));
                    LevelPoint into = PropRules.Inward(face.Side);
                    double line = face.Line * _cs;
                    for (double a = face.From * _cs + step; a < face.To * _cs - 1e-9; a += step)
                        places.Add((face.Vertical ? new LevelPoint(line + into.X * depth, a) : new LevelPoint(a, line + into.Y * depth), turn, PropPlace.Wall));
                }
                rng.Shuffle(places);
                return places;
            }

            // ---------------- The stances ----------------

            void Row(int n, Func<PropModel> pick, LevelRandom rng)
            {
                double w = X1 - X0, h = Y1 - Y0;
                bool alongX = w > h + 1e-9 || (Math.Abs(w - h) <= 1e-9 && rng.Below(2) == 0);
                double a0 = alongX ? X0 : Y0, a1 = alongX ? X1 : Y1, c0 = alongX ? Y0 : X0, c1 = alongX ? Y1 : X1;
                double[] rows = { c0 + (c1 - c0) * _tv.RowAcross, c0 + (c1 - c0) * (1 - _tv.RowAcross) };
                double mid = (a0 + a1) * 0.5, step = _v.PlaceStep;
                var places = new List<LevelPoint>();
                for (int s = 0; s * step < (a1 - a0) * 0.5; s++)
                    foreach (int sign in s == 0 ? new[] { 1 } : new[] { 1, -1 })
                        foreach (double row in rows)
                        {
                            double along = mid + sign * s * step;
                            places.Add(alongX ? new LevelPoint(along, row) : new LevelPoint(row, along));
                        }
                for (int k = 0; k < n; k++)
                {
                    PropModel model = pick();
                    int turn = (alongX ? 0 : 90) + 180 * rng.Below(2); // its long side (its own x) along the row
                    Put(model, RoomTemplates.CoverPlaces(_room, _cs).Concat(places).Select(p => (p, turn, PropPlace.Floor))); // work order 058: the c marks first
                }
            }

            void Cluster(int n, Func<PropModel> pick, LevelRandom rng)
            {
                List<LevelPoint> grid = Grid();
                if (grid.Count == 0) return;
                var inner = grid.Where(p => RoomShapes.DistanceToWalls(_room, p, _cs) >= _tv.ClusterFromWalls).ToList(); // work order 051: to the outline
                if (inner.Count == 0) inner = grid;
                LevelPoint first = inner[rng.Below(inner.Count)];
                var apart = inner.Where(p => p.DistanceTo(first) >= 2 * _v.FloorSpacing).ToList();
                LevelPoint second = (apart.Count > 0 ? apart : inner)[rng.Below(apart.Count > 0 ? apart.Count : inner.Count)];
                double band = Math.Max(0.25, _tv.ClusterBand);
                rng.Shuffle(grid);
                // Round each middle, within its reach, nearest band first, in the drawn order within a band.
                List<LevelPoint> Round(LevelPoint c) =>
                    grid.Where(p => p.DistanceTo(c) <= _tv.ClusterReach).OrderBy(p => Math.Floor(p.DistanceTo(c) / band)).ToList();
                List<LevelPoint>[] round = { Round(first), Round(second) };
                for (int k = 0; k < n; k++)
                {
                    PropModel model = pick();
                    int turn = Turn(model, rng);
                    IEnumerable<(LevelPoint, int, PropPlace)> places = round[k % 2].Select(p => (p, turn, PropPlace.Floor));
                    // Work order 044 (the ossuary of the concept pictures): banked against its far wall, the clusters only where
                    // the wall holds no more. The same draws as without, so the room holds the same props.
                    if (_bank) places = BankPlaces(model, turn).Concat(places);
                    Put(model, places);
                }
            }

            bool _bank;
            double _daisRoom; // work order 051: how far the throne keeps from a carved cell, for its dais
            bool _modelDais;

            /// <summary>
            /// The ossuary's bank (work order 044): set pieces along the wall opposite the door in, a clearance and their reach
            /// out from it, from the wall's middle outward every step; the rules keep them off its doors.
            /// </summary>
            IEnumerable<(LevelPoint, int, PropPlace)> BankPlaces(PropModel model, int turn)
            {
                LevelDoor entry = LevelGeometry.EntryDoor(_level, _room);
                int wayIn = entry != null ? LevelGeometry.WallOf(_room.Rect, entry) : -1;
                int far = wayIn >= 0 ? LevelGenerator.Opposite(wayIn) : LevelGenerator.North;
                Ends(far, out double lo, out double hi);
                double depth = _v.WallClearance + Reach(model) + 0.05;
                return Sweep(far, depth, (lo + hi) * 0.5).Select(p => (p, turn, PropPlace.Set));
            }

            List<LevelPoint> Corners(PropModel model)
            {
                double inset = _v.WallClearance + Reach(model) + 0.1;
                return new List<LevelPoint>
                {
                    new LevelPoint(X0 + inset, Y0 + inset), new LevelPoint(X1 - inset, Y0 + inset),
                    new LevelPoint(X1 - inset, Y1 - inset), new LevelPoint(X0 + inset, Y1 - inset),
                };
            }

            void CornerOrWall(int n, Func<PropModel> pick, LevelRandom rng)
            {
                for (int k = 0; k < n; k++)
                {
                    PropModel model = pick();
                    bool corner = rng.Below(2) == 0;
                    int turn = Turn(model, rng);
                    List<LevelPoint> corners = Corners(model);
                    rng.Shuffle(corners);
                    var atCorners = corners.SelectMany(c => Nearby(c, turn, PropPlace.Set)).ToList();
                    List<(LevelPoint, int, PropPlace)> atWalls = WallPlaces(model, rng);
                    Put(model, corner ? atCorners.Concat(atWalls) : atWalls.Concat(atCorners));
                }
            }

            void AlongWall(PropModel model, LevelRandom rng)
            {
                LevelProp p = Put(model, WallPlaces(model, rng));
                if (p != null && p.Model == PropModel.Bookshelf) _shelves.Add(p);
            }

            void OpenFloor(PropModel model, LevelRandom rng, double fromWallsMost)
            {
                int turn = Turn(model, rng);
                List<LevelPoint> grid = Grid();
                grid = fromWallsMost < double.MaxValue
                    ? grid.Where(p => RoomShapes.DistanceToWalls(_room, p, _cs) <= fromWallsMost).ToList() // work order 051: to the outline
                    : grid.Where(p => RoomShapes.DistanceToWalls(_room, p, _cs) >= _tv.OpenFromWalls).ToList();
                rng.Shuffle(grid);
                Put(model, RoomTemplates.CoverPlaces(_room, _cs).Concat(grid).Select(p => (p, turn, PropPlace.Floor))); // work order 058: the c marks first
            }

            void Ring(int n, Func<PropModel> pick, LevelRandom rng)
            {
                bool ring = rng.Below(2) == 0;
                int start = rng.Below(24);
                double w = X1 - X0, h = Y1 - Y0;
                LevelPoint m = _room.Middle(_cs);
                var points = new List<LevelPoint>();
                if (ring)
                {
                    double radius = _tv.RingShare * Math.Min(w, h);
                    for (int k = 0; k < n; k++)
                    {
                        double a = (start * 15.0 + k * 360.0 / n) * Math.PI / 180.0;
                        points.Add(new LevelPoint(m.X + radius * Math.Sin(a), m.Y + radius * Math.Cos(a)));
                    }
                }
                else
                {
                    bool alongX = w >= h;
                    double a0 = alongX ? X0 : Y0, a1 = alongX ? X1 : Y1, c0 = alongX ? Y0 : X0, c1 = alongX ? Y1 : X1;
                    int first = (n + 1) / 2;
                    for (int k = 0; k < n; k++)
                    {
                        int row = k < first ? 0 : 1, count = row == 0 ? first : n - first, i = row == 0 ? k : k - first;
                        double along = a0 + (i + 0.5) * (a1 - a0) / Math.Max(1, count);
                        double across = c0 + (c1 - c0) * (row == 0 ? _tv.RowAcross : 1 - _tv.RowAcross);
                        points.Add(alongX ? new LevelPoint(along, across) : new LevelPoint(across, along));
                    }
                }
                for (int k = 0; k < n; k++)
                {
                    PropModel model = pick();
                    int turn = Turn(model, rng);
                    Put(model, RoomTemplates.CoverPlaces(_room, _cs).Select(p => (p, turn, PropPlace.Floor)).Concat(Nearby(points[k], turn, PropPlace.Floor))); // work order 058: the c marks first
                }
            }

            void FarAltar(PropModel model)
            {
                LevelDoor entry = LevelGeometry.EntryDoor(_level, _room);
                int wayIn = entry != null ? LevelGeometry.WallOf(_room.Rect, entry) : -1;
                _farSide = wayIn >= 0 ? LevelGenerator.Opposite(wayIn) : LevelGenerator.North;
                PropSize altar = PropSize.Of(model), statue = PropSize.Of(PropModel.Statue);
                double depth = _v.AtWallGap + statue.Depth + _tv.AltarStatueGap + altar.Depth * 0.5;
                Ends(_farSide, out double lo, out double hi);
                List<LevelPoint> places = Sweep(_farSide, depth, RoomTemplates.FarMarkAlong(_room, _farSide, _cs) ?? (lo + hi) * 0.5); // work order 058: from the A mark
                _altarWanted = places.Count > 0 ? places[0] : _room.Middle(_cs);
                int turn = Facing(model, PropRules.Inward(_farSide));
                _altar = Put(model, places.Select(p => (p, turn, PropPlace.Set)));
            }

            void BehindAltar(PropModel model)
            {
                PropSize s = PropSize.Of(model);
                LevelPoint at = _altar != null ? _altar.Position : _altarWanted;
                double depth = _v.AtWallGap + (s.Shape == PropShape.Circle ? s.Radius : s.Depth * 0.5);
                int turn = Facing(model, PropRules.Inward(_farSide));
                Put(model, Sweep(_farSide, depth, AlongOf(_farSide, at)).Select(p => (p, turn, PropPlace.Wall)));
            }

            /// <summary>One of a pair either side of a set piece (or of where it was wanted), along its wall.</summary>
            void Flank(PropModel model, LevelProp piece, LevelPoint wanted, int side, int k, double gap, bool outFromWall)
            {
                LevelPoint at = piece != null ? piece.Position : wanted;
                double half = piece != null ? (piece.Shape == PropShape.Circle ? piece.Radius : piece.Width * 0.5) : 1.0;
                double off = half + gap + Reach(model);
                double along = AlongOf(side, at) + (k % 2 == 0 ? -off : off);
                double depth = outFromWall ? _v.WallClearance + Reach(model) + 0.25 : DepthOf(side, at);
                // Turned as the piece is (a multiple of 90 degrees for the altar and the throne), or facing in if it has a front.
                int turn = PropSize.Of(model).HasFront ? Facing(model, PropRules.Inward(side)) : piece != null ? piece.Rotation - piece.Rotation % 90 : 0;
                Put(model, Nearby(OnWall(side, along, depth), turn, PropPlace.Set));
            }

            double DepthOf(int side, LevelPoint p)
            {
                switch (side)
                {
                    case LevelGenerator.North: return Y1 - p.Y;
                    case LevelGenerator.South: return p.Y - Y0;
                    case LevelGenerator.East: return X1 - p.X;
                    default: return p.X - X0;
                }
            }

            void BeforeAltar(PropModel model)
            {
                LevelPoint at = _altar != null ? _altar.Position : _altarWanted;
                double half = PropSize.Of(PropModel.Altar).Depth * 0.5;
                LevelPoint into = PropRules.Inward(_farSide);
                double off = half + _tv.BeforeAltar + Reach(model);
                Put(model, Nearby(new LevelPoint(at.X + into.X * off, at.Y + into.Y * off), 0, PropPlace.Set));
            }

            void BesideShelf(PropModel model, int k, LevelRandom rng)
            {
                bool left = rng.Below(2) == 0;
                if (_shelves.Count == 0)
                {
                    OpenFloor(model, rng, _tv.EdgeBand);
                    return;
                }
                LevelProp shelf = _shelves[k % _shelves.Count];
                int side = SideOf(shelf);
                double off = shelf.Width * 0.5 + _tv.ShelfCandleGap + Reach(model);
                double along = AlongOf(side, shelf.Position) + (left ? -off : off);
                LevelPoint wanted = OnWall(side, along, _v.WallClearance + Reach(model) + 0.05);
                Put(model, Nearby(wanted, shelf.Rotation, PropPlace.Set));
            }

            void Throne(PropModel model)
            {
                PropSize s = PropSize.Of(model);
                double depth = _v.WallFace + _v.WallPieceGap + s.Depth * 0.5;
                var places = new List<(LevelPoint, int, PropPlace)>();
                void Wall(int side)
                {
                    Ends(side, out double lo, out double hi);
                    int turn = Facing(model, PropRules.Inward(side));
                    places.AddRange(Sweep(side, depth, (lo + hi) * 0.5).Select(p => (p, turn, PropPlace.Wall)));
                }
                // Work order 058: in a template room the far wall from its A mark first, when that wall is not the south one.
                bool fromMark = RoomTemplates.FarMark(_room, _cs, out int markSide, out double markAlong) && markSide != LevelGenerator.South;
                if (fromMark)
                {
                    int turn = Facing(model, PropRules.Inward(markSide));
                    places.AddRange(Sweep(markSide, depth, markAlong).Select(p => (p, turn, PropPlace.Wall)));
                }
                if (!fromMark || markSide != LevelGenerator.North) Wall(LevelGenerator.North);
                // Work order 051: else the northmost carved faces that face south into the room (a gate's back, an apse's
                // side), nearest the room's middle first.
                {
                    int turn = Facing(model, PropRules.Inward(LevelGenerator.North));
                    double mid = (X0 + X1) * 0.5;
                    foreach (WallFace face in RoomShapes.CarvedFaces(_room).Where(f => f.Side == LevelGenerator.North)
                                                   .OrderByDescending(f => f.Line).ThenBy(f => Math.Abs((f.From + f.To) * 0.5 * _cs - mid)))
                        for (double a = face.From * _cs + _v.PlaceStep; a < face.To * _cs - 1e-9; a += _v.PlaceStep)
                        {
                            var at = new LevelPoint(a, face.Line * _cs - depth);
                            // Only where the north wall is still its nearest side: the throne room's north end.
                            if (LevelProp.Make(model, at, turn, PropPlace.Wall).Footprint.NearestSide(_room.Rect, _cs, out _) == LevelGenerator.North)
                                places.Add((at, turn, PropPlace.Wall));
                        }
                }
                // Else the side walls, the one farther from the door in first; never the south wall.
                LevelDoor entry = LevelGeometry.EntryDoor(_level, _room);
                LevelPoint door = _room.Middle(_cs);
                if (entry != null)
                {
                    entry.Opening(_cs, out LevelPoint a, out LevelPoint b);
                    door = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                }
                bool eastFirst = Math.Abs(X1 - door.X) >= Math.Abs(door.X - X0);
                Wall(eastFirst ? LevelGenerator.East : LevelGenerator.West);
                Wall(eastFirst ? LevelGenerator.West : LevelGenerator.East);
                if (fromMark) places = places.GroupBy(p => (p.Item1.X, p.Item1.Y, p.Item2)).Select(g => g.First()).ToList(); // the far wall's places once
                // Work order 051: in a carved room, room for its dais (work order 044) between it and any carved cell.
                if (_room.SolidCount > 0 && _daisRoom > 0)
                {
                    places = places.Where(p => RoomShapes.DistanceToSolid(_room, LevelProp.Make(model, p.Item1, p.Item2, p.Item3).Footprint, _cs) >= _daisRoom).ToList();
                    // Those where the dais model has room too first, the others after them (keeping their order).
                    bool ModelFits((LevelPoint, int, PropPlace) p)
                    {
                        LevelProp t = LevelProp.Make(model, p.Item1, p.Item2, p.Item3);
                        // The dais model: its back at the wall's face where the throne's is a hand's gap out, as wide and deep as it is.
                        PropSize ds = PropSize.Of(PropModel.Dais);
                        LevelPoint front = t.Front;
                        double ahead = ds.Depth * 0.5 - _v.WallPieceGap - t.Depth * 0.5;
                        var dais = new PropFootprint
                        {
                            Shape = PropShape.Rectangle, Centre = new LevelPoint(t.Position.X + front.X * ahead, t.Position.Y + front.Y * ahead),
                            Rotation = t.Rotation, HalfWidth = ds.Width * 0.5, HalfDepth = ds.Depth * 0.5,
                        };
                        return RoomShapes.DistanceToSolid(_room, dais, _cs) >= 0.05;
                    }
                    if (_modelDais) places = places.Where(ModelFits).Concat(places.Where(p => !ModelFits(p))).ToList();
                }
                _throne = Put(model, places);
            }

            void CornerPillars(int n, Func<PropModel> pick, LevelRandom rng)
            {
                double inset = _v.BossPillarInset;
                LevelPoint[] corners =
                {
                    new LevelPoint(X0 + inset, Y0 + inset), new LevelPoint(X1 - inset, Y0 + inset),
                    new LevelPoint(X1 - inset, Y1 - inset), new LevelPoint(X0 + inset, Y1 - inset),
                };
                for (int k = 0; k < n; k++)
                {
                    PropModel model = pick();
                    Put(model, Nearby(corners[k % 4], Turn(model, rng), PropPlace.Set));
                }
            }

            void Chest(PropModel model)
            {
                LevelDoor door = _level.DoorsOf(_room.Order).FirstOrDefault();
                LevelPoint m = _room.Middle(_cs);
                int facing = 0;
                if (door != null)
                {
                    DoorFrame(door, out _, out _, out _, out _, out double nx, out double ny);
                    facing = PropRules.Facing(-nx, -ny);
                    m = new LevelPoint(m.X + nx * _v.ChestBack, m.Y + ny * _v.ChestBack);
                }
                _chest = Put(model, Nearby(m, facing, PropPlace.Set));
            }

            void FlankChest(PropModel model, int k)
            {
                if (_chest == null)
                {
                    Want(1);
                    return;
                }
                double a = _chest.Rotation * Math.PI / 180.0, ux = Math.Cos(a), uy = -Math.Sin(a); // the chest's own x
                double off = _chest.Width * 0.5 + _v.BesideGap + Reach(model);
                double s = k % 2 == 0 ? -1 : 1;
                LevelPoint c = _chest.Position;
                Put(model, Nearby(new LevelPoint(c.X + s * ux * off, c.Y + s * uy * off), _chest.Rotation, PropPlace.Set));
            }
        }
    }
}
