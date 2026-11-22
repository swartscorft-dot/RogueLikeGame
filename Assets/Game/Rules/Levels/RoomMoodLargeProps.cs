using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The eight large props in the rooms' compositions (work order 052; the concept pictures in docs/art/concepts/rooms and
    /// Kristian's choice of 2026-11-20, "build toward them"), with <see cref="LevelValues.LargeProps"/> on. They are props of
    /// the floor (<see cref="PropSize.Large"/>): the floor builds them with the others, with their colliders, before its
    /// NavMesh, and the description stays as the generator made it. Off, the plan is work order 044's exactly.
    ///
    /// They are placed last, after the room's mood is planned, from what the room holds and the composition says, and draw
    /// nothing from the stream: the mood's wear and candles keep their numbers, and the wear a large prop stands on is taken
    /// away. Every one keeps every rule of the props (<see cref="PropRules.Fault"/>, with the room's niches and lanterns for
    /// the pieces at a wall) with the room's props and the large props before it, the open floor's spacing waived only for
    /// the ones walked over or under and for a candle cluster set beside what it lights (as work order 044's candles stood),
    /// and the dais, walked over as the code-made one was, kept off the doorways by a metre and off every place an enemy or
    /// she stands at by a walk's radius (<see cref="PropValues.WalkRadius"/>), not by the room a prop that blocks walking
    /// leaves; one that blocks walking keeps the clear lane (<see cref="PropRules.Lane"/>) and stays out of the middle as a
    /// themed room's props do (the lane's half width less a step round it, and the recipe's clear middle), and off the
    /// composition's code-made pieces. Each is put at the first of its places that keeps them all, or left out.
    ///
    /// Throne room: the throne on the dais model (sunk so <see cref="PropSize.DaisShows"/> of it shows, walked over: no
    /// special case for the boss or the NavMesh), its back at the wall's face, in place of the code-made dais; the
    /// composition's candles on its top and the carpet to its front edge; a great brazier either side of it in place of the
    /// throne's pair of braziers or basins (one of the pair that the dais does not touch stays where no great brazier
    /// could stand); two to four banners on the far wall and the side walls nearest it. Tomb hall: the knight at the empty
    /// place of the far wall (else along that wall, else a side wall; never the low south wall), facing into the room, a
    /// candle cluster either side. Shrine: the great altar in place of the basin's composition (the basin, its round dais
    /// and its candles), facing into the room, at the nearest place to the basin's within 3 m (else before the altar on the
    /// axis); a candle cluster on the floor at each of its ends. Vault and the treasure room's archive: the chest, and what stands beside it, on the dais model where the
    /// room has room for it (nothing touching it but what stands wholly on it), else the code-made dais; the vault's coin
    /// heaps beside the dais in place of the code-made ones. Ossuary: a bone wall against the far wall, on the plain section
    /// nearest its middle (between two piers; the banked piles stand a clearance out from the wall, so it stands where the
    /// wall is free of them), else on a side wall; a second on a side wall in a room larger than the middle of its kind.
    /// Every combat room: a banner, two in a room larger than the
    /// middle, on a plain section of a full wall facing it (no niche, lantern or door beside it), clear of every prop by
    /// <see cref="BannerClear"/>; candle clusters in place of the code-made candles at coffins, sarcophagi and bookshelves.
    /// </summary>
    public sealed partial class RoomMoodPlan
    {
        /// <summary>Metres a banner keeps from every prop: the wall it hangs on is free.</summary>
        public const double BannerClear = 0.3;
        /// <summary>Metres a large prop that blocks walking keeps from the composition's code-made pieces.</summary>
        public const double PieceClear = 0.1;

        static List<(double x, double y)> s_rings;

        /// <summary>Places round a wanted one every quarter metre out to 3 m, nearest first.</summary>
        static List<(double x, double y)> Rings()
        {
            if (s_rings != null) return s_rings;
            var list = new List<(double x, double y)>();
            for (int i = -12; i <= 12; i++)
                for (int j = -12; j <= 12; j++)
                    if (i * i + j * j <= 144) list.Add((i * 0.25, j * 0.25));
            return s_rings = list.OrderBy(o => o.x * o.x + o.y * o.y).ThenBy(o => o.y).ThenBy(o => o.x).ToList();
        }

        /// <summary>One room's large props being placed: its rules, its lane, what stands in it.</summary>
        sealed class Large
        {
            public LevelLayout Layout;
            public LevelRoom Room;
            public MoodRoom Mr;
            public PropValues Pv, Close, Walk;
            public ThemeValues Tv;
            public PropRules.Lane Lane;
            public List<PropRules.WallSpan> Spans;
            public double ClearMiddle;
            public double Cs;

            public List<LevelProp> Others(IEnumerable<LevelProp> without = null)
            {
                var list = new List<LevelProp>();
                foreach (LevelProp p in Room.Props)
                    if (!Mr.Replaced.Contains(p) && (without == null || !without.Contains(p))) list.Add(p);
                list.AddRange(Mr.LargeProps);
                return list;
            }

            /// <summary>The first rule the large prop breaks there, or null.</summary>
            public string Why(LevelProp p, List<LevelProp> carried = null)
            {
                PropSize size = PropSize.Of(p.Model);
                List<LevelProp> others = Others(carried);
                PropValues rules = p.Model == PropModel.Dais ? Walk : size.Walkable || p.Model == PropModel.CandleCluster ? Close : Pv;
                string fault = PropRules.Fault(Layout, Room, p, others, rules, null, Spans);
                if (fault != null) return fault;
                PropFootprint f = p.Footprint;
                if (p.Model == PropModel.Banner)
                {
                    foreach (LevelProp o in others)
                        if (o.Footprint.DistanceTo(f) < BannerClear) return "hangs where the " + o.Model + " stands";
                    return null;
                }
                if (size.Walkable) return null;
                double fromMiddle = f.DistanceTo(Room.Middle(Cs));
                double closes = Room.Kind == RoomKind.Treasure ? -1 : Pv.LaneWidth * 0.5 - Pv.LaneGrid;
                if (fromMiddle < closes || (ClearMiddle > 0 && fromMiddle < ClearMiddle)) return "stands in the room's middle";
                foreach (CompositionPiece c in Mr.Pieces)
                {
                    double gap;
                    switch (c.Kind)
                    {
                        case CompositionKind.Carpet: gap = f.DistanceTo(c.At, c.To) - c.Width * 0.5; break;
                        case CompositionKind.Dais:
                            gap = f.DistanceTo(new PropFootprint { Shape = PropShape.Rectangle, Centre = c.At, Rotation = c.Turn, HalfWidth = c.Width * 0.5, HalfDepth = c.Depth * 0.5 });
                            break;
                        case CompositionKind.Coins: continue;
                        default: gap = f.DistanceTo(c.At) - c.Reach; break;
                    }
                    if (gap < PieceClear) return "stands on the composition's " + c.Kind;
                }
                if (!Lane.Clear(f)) return "closes the lane";
                return null;
            }

            /// <summary>Puts the large prop down if it keeps every rule; true when it did.</summary>
            public bool Add(LevelProp p, List<LevelProp> carried = null)
            {
                if (Why(p, carried) != null) return false;
                Mr.LargeProps.Add(p);
                if (!PropSize.Of(p.Model).Walkable) Lane.Block(p.Footprint);
                return true;
            }

            /// <summary>The model at the nearest place to <paramref name="wanted"/>, within <paramref name="search"/>, that keeps every rule; null if none.</summary>
            public LevelProp Near(PropModel model, LevelPoint wanted, int rotation, double search)
            {
                foreach ((double x, double y) in Rings())
                {
                    if (x * x + y * y > search * search + 1e-9) break;
                    var at = new LevelPoint(wanted.X + x, wanted.Y + y);
                    LevelProp p = LevelProp.Make(model, at, ((rotation % 360) + 360) % 360, PlaceFor(model, at));
                    if (Add(p)) return p;
                }
                return null;
            }

            /// <summary>At a wall when it is within reach of one (and may be), else a set piece the clearance from the walls.</summary>
            public PropPlace PlaceFor(PropModel model, LevelPoint at)
            {
                PropSize s = PropSize.Of(model);
                if (s.WallPiece) return PropPlace.Wall;
                PropFootprint f = PropFootprint.Of(s, at, 0);
                return s.AtWall && f.DistanceToWalls(Room.Rect, Cs) <= Pv.AtWallMost ? PropPlace.Wall : PropPlace.Set;
            }

            public void RemovePiece(CompositionPiece c)
            {
                Mr.Pieces.Remove(c);
                if (c.Kind == CompositionKind.Candles)
                    Mr.Sources.RemoveAll(s => !s.OfProp && s.Kind == MoodSourceKind.Candle && s.At.DistanceTo(c.At) < 1e-6);
            }

            /// <summary>The composition's pieces and sources as they are, to put back as they were (in their order) when what was tried fails.</summary>
            public (List<CompositionPiece> pieces, List<MoodSource> sources) Keep() => (new List<CompositionPiece>(Mr.Pieces), new List<MoodSource>(Mr.Sources));

            public void PutBack((List<CompositionPiece> pieces, List<MoodSource> sources) kept)
            {
                Mr.Pieces.Clear();
                Mr.Pieces.AddRange(kept.pieces);
                Mr.Sources.Clear();
                Mr.Sources.AddRange(kept.sources);
            }

            public MoodSource SourceOf(CompositionPiece c) =>
                Mr.Sources.FirstOrDefault(s => !s.OfProp && s.Kind == MoodSourceKind.Candle && s.At.DistanceTo(c.At) < 1e-6);
        }

        /// <summary>The stretches of the room's walls a piece at a wall keeps clear of: its niches and its lanterns (work order 052).</summary>
        static List<PropRules.WallSpan> WallSpansOf(LevelLayout layout, LevelRoom room, CryptKitPlan kit)
        {
            List<PropRules.WallSpan> spans = PropRules.NichesOf(layout, room, kit);
            double cs = layout.CellSize;
            CellRect r = room.Rect;
            foreach (CryptKitLantern l in kit.Lanterns)
            {
                if (l.Room != room.Order) continue;
                CryptKitSection s = kit.Sections[l.Section];
                int side = s.Vertical ? (s.Line == r.X ? LevelGenerator.West : s.Line == r.Right ? LevelGenerator.East : -1)
                                      : (s.Line == r.Y ? LevelGenerator.South : s.Line == r.Top ? LevelGenerator.North : -1);
                if (side >= 0) spans.Add(new PropRules.WallSpan { Side = side, From = s.Cell * cs, To = (s.Cell + 1) * cs });
            }
            return spans;
        }

        /// <summary>The middles of the room's plain wall sections on a side, where a banner may hang: full height, facing it, no niche toward it, no lantern of its own, no door beside it.</summary>
        static List<double> PlainSections(LevelLayout layout, LevelRoom room, CryptKitPlan kit, int side)
        {
            var list = new List<double>();
            double cs = layout.CellSize;
            CellRect r = room.Rect;
            for (int i = 0; i < kit.Sections.Count; i++)
            {
                CryptKitSection s = kit.Sections[i];
                if (s.Low || s.AtDoor) continue;
                bool on = side == LevelGenerator.West ? s.Vertical && s.Line == r.X && s.RoomPlus == room.Order
                        : side == LevelGenerator.East ? s.Vertical && s.Line == r.Right && s.RoomMinus == room.Order
                        : side == LevelGenerator.South ? !s.Vertical && s.Line == r.Y && s.RoomPlus == room.Order
                        : !s.Vertical && s.Line == r.Top && s.RoomMinus == room.Order;
                if (!on) continue;
                int face = side == LevelGenerator.West || side == LevelGenerator.South ? 1 : -1;
                if (s.Niche && s.Facing == face) continue;
                bool lantern = false;
                foreach (CryptKitLantern l in kit.Lanterns)
                    if (l.Section == i && l.Room == room.Order) lantern = true;
                if (lantern) continue;
                list.Add((s.Cell + 0.5) * cs);
            }
            return list;
        }

        /// <summary>The two side walls of a far wall, the one farther from the door in first (as <see cref="LargePlace"/> orders them).</summary>
        static int[] SideWalls(LevelLayout layout, LevelRoom room, int far)
        {
            int a = (far + 1) % 4, b = (far + 3) % 4;
            LevelDoor entry = LevelGeometry.EntryDoor(layout, room);
            if (entry != null)
            {
                entry.Opening(layout.CellSize, out LevelPoint e0, out LevelPoint e1);
                var door = new LevelPoint((e0.X + e1.X) * 0.5, (e0.Y + e1.Y) * 0.5);
                double cs = layout.CellSize;
                double Dist(int w) => w == LevelGenerator.North ? room.Rect.Top * cs - door.Y : w == LevelGenerator.South ? door.Y - room.Rect.Y * cs
                                      : w == LevelGenerator.East ? room.Rect.Right * cs - door.X : door.X - room.Rect.X * cs;
                if (Dist(b) > Dist(a)) { int t = a; a = b; b = t; }
            }
            return new[] { a, b };
        }

        /// <summary>Is the footprint wholly on the rectangle of <paramref name="outer"/>?</summary>
        static bool WhollyOn(PropFootprint inner, PropFootprint outer)
        {
            Axes(outer.Rotation, out LevelPoint ux, out LevelPoint uz);
            bool On(LevelPoint p, double inset)
            {
                double dx = p.X - outer.Centre.X, dy = p.Y - outer.Centre.Y;
                return Math.Abs(dx * ux.X + dy * ux.Y) <= outer.HalfWidth - inset + 1e-9 && Math.Abs(dx * uz.X + dy * uz.Y) <= outer.HalfDepth - inset + 1e-9;
            }
            if (inner.Shape == PropShape.Circle) return On(inner.Centre, inner.Radius);
            foreach (LevelPoint c in inner.Corners())
                if (!On(c, 0)) return false;
            return true;
        }

        static int Facing(PropModel model, LevelPoint d) => (PropRules.Facing(d.X, d.Y) + PropSize.Of(model).FrontTurn) % 360;

        /// <summary>The large props of one room (see the class): after its mood is planned; nothing is drawn from the stream.</summary>
        static void PlaceLarge(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelValues values, ref CryptKitPlan kit)
        {
            if (room.Kind == RoomKind.Entrance) return;
            kit = kit ?? PropRules.KitPlanForProps(layout);
            PropValues pv = values.PropRules ?? new PropValues();
            PropValues close = pv.Copy();
            close.FloorSpacing = 0f;
            // The dais is walked over, as the code-made one was: off the doorways and off every place an enemy or she stands at
            // by a walk's radius, not by the room a prop that blocks walking leaves.
            PropValues walk = close.Copy();
            walk.FromDoor = Math.Min(pv.FromDoor, 1f);
            walk.FromPoints = Math.Min(pv.FromPoints, pv.WalkRadius);
            ThemeRecipe recipe = room.Theme == RoomTheme.None ? null : values.ThemeRules?.Of(room.Theme, room.Kind);
            var g = new Large
            {
                Layout = layout, Room = room, Mr = mr, Pv = pv, Close = close, Walk = walk, Cs = layout.CellSize, Tv = values.ThemeRules ?? new ThemeValues(),
                Spans = WallSpansOf(layout, room, kit), ClearMiddle = recipe != null ? recipe.ClearMiddle : 0,
            };
            Relane(g);

            switch (room.Theme)
            {
                case RoomTheme.ThroneRoom: Throne(g); break;
                case RoomTheme.TombHall: Knight(g); break;
                case RoomTheme.Shrine: GreatAltar(g); break;
                case RoomTheme.Vault: ChestDais(g, true, !(values.Cave && values.CaveRules != null && values.CaveRules.NoGoldHeaps)); break;
                case RoomTheme.Archive when room.Kind == RoomKind.Treasure: ChestDais(g, false, false); break;
                case RoomTheme.Ossuary: BoneWalls(g, values, kit); break;
            }
            // The lane is built with the room's props as they stand once the theme's piece is down.
            if (room.Theme == RoomTheme.TombHall || room.Theme == RoomTheme.Archive) Clusters(g);
            if (room.Kind == RoomKind.Combat || room.Theme == RoomTheme.ThroneRoom) Banners(g, values, kit);

            if (mr.LargeProps.Count > 0 || mr.Replaced.Count > 0) Finish(g);
        }

        /// <summary>Builds the room's lane with what stands now (the replaced props gone, the large props in).</summary>
        static void Relane(Large g)
        {
            g.Lane = new PropRules.Lane(g.Layout, g.Room, g.Pv);
            foreach (LevelProp p in g.Others())
                if (!PropSize.Of(p.Model).Walkable) g.Lane.Block(p.Footprint);
        }

        // ---------------- The throne room ----------------

        static void Throne(Large g)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            LevelProp throne = room.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
            if (throne == null)
            {
                mr.LargeLeftOut = "no throne";
                return;
            }
            double cs = g.Cs, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            int side = throne.Footprint.NearestSide(room.Rect, cs, out _);
            LevelPoint into = PropRules.Inward(side);
            double along = AlongX(side) ? throne.Position.X : throne.Position.Y;
            PropSize ds = PropSize.Of(PropModel.Dais);
            var dais = LevelProp.Make(PropModel.Dais, OnWall(side, along, g.Pv.WallFace + ds.Depth * 0.5, x0, x1, y0, y1), Facing(PropModel.Dais, into), PropPlace.Wall);

            // The throne's pair, which the great braziers stand in place of; what the dais touches stands wholly on it.
            List<LevelProp> pair = room.Props.Where(p => p.Model == PropModel.Brazier || p.Model == PropModel.Basin).ToList();
            mr.Replaced.AddRange(pair);
            PropFootprint df = dais.Footprint;
            List<LevelProp> carried = room.Props.Where(p => !mr.Replaced.Contains(p) && p.Footprint.DistanceTo(df) <= 1e-9).ToList();
            Relane(g);
            string why = carried.All(p => WhollyOn(p.Footprint, df)) ? g.Why(dais, carried) : "a prop stands half on it";
            if (why != null)
            {
                mr.Replaced.Clear();
                Relane(g);
                mr.LargeLeftOut = "the dais " + why;
                return;
            }
            mr.LargeProps.Add(dais);
            mr.Raised.AddRange(carried);
            OnTheDais(g, dais);
            // The carpet runs to the dais's front edge (work order 051: its last run, in a carved room it may bend on the way).
            CompositionPiece carpet = mr.Pieces.LastOrDefault(c => c.Kind == CompositionKind.Carpet);
            if (carpet != null)
            {
                LevelPoint was = carpet.To;
                carpet.To = OnWall(side, along, g.Pv.WallFace + ds.Depth - 0.1, x0, x1, y0, y1);
                if (RunOnCarved(room, carpet, cs)) carpet.To = was; // work order 051: never over a carved cell
                if (carpet.At.DistanceTo(carpet.To) < 1) mr.Pieces.Remove(carpet);
            }

            // A great brazier either side of it; one of the pair the dais does not touch stays where none can stand.
            PropSize gb = PropSize.Of(PropModel.GreatBrazier);
            foreach (int s in new[] { -1, 1 })
            {
                double off = ds.Width * 0.5 + 0.6 + gb.Radius;
                LevelPoint wanted = OnWall(side, along + s * off, g.Pv.WallClearance + gb.Radius + 0.25, x0, x1, y0, y1);
                if (g.Near(PropModel.GreatBrazier, wanted, 0, 1.5) != null) continue;
                foreach (LevelProp p in pair)
                {
                    double pa = AlongX(side) ? p.Position.X : p.Position.Y;
                    if (Math.Sign(pa - along) != s || p.Footprint.DistanceTo(df) <= 1e-9) continue;
                    if (PropRules.Fault(g.Layout, room, p, g.Others(), g.Pv) != null) continue;
                    mr.Replaced.Remove(p);
                    g.Lane.Block(p.Footprint);
                }
            }
        }

        /// <summary>The dais model is down: the code-made dais goes, the code-made pieces on it stand on its top.</summary>
        static void OnTheDais(Large g, LevelProp dais)
        {
            MoodRoom mr = g.Mr;
            PropFootprint df = dais.Footprint;
            foreach (CompositionPiece c in mr.Pieces.Where(c => c.Kind == CompositionKind.Dais || c.Kind == CompositionKind.RoundDais).ToList()) mr.Pieces.Remove(c);
            foreach (CompositionPiece c in mr.Pieces)
            {
                if (c.Kind != CompositionKind.Candles || df.DistanceTo(c.At) > 0) continue;
                MoodSource src = g.SourceOf(c);
                c.Height = PropSize.DaisShows;
                if (src != null) src.Height = PropSize.DaisShows + 0.3;
            }
            foreach (MoodSource s in mr.Sources)
                if (s.OfProp && mr.Raised.Any(p => p.Position.DistanceTo(s.At) < 1e-6)) s.Height += PropSize.DaisShows;
        }

        // ---------------- The vault and the treasure room's archive ----------------

        /// <summary>
        /// Work order 076 (Kristian, 2026-11-21: "chestrommet så blokker den gull-proppen. syntes den er litt unødvendig"):
        /// <paramref name="heaps"/> false, the vault's two large coin heaps are not placed (<see cref="CaveValues.NoGoldHeaps"/>);
        /// the code-made heaps on the dais and the spilled coins stay, as with no room for a large one.
        /// </summary>
        static void ChestDais(Large g, bool vault, bool heaps)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            LevelProp chest = room.Props.FirstOrDefault(p => p.Model == PropModel.Chest);
            if (chest == null)
            {
                mr.LargeLeftOut = "no chest";
                return;
            }
            var dais = LevelProp.Make(PropModel.Dais, chest.Position, chest.Rotation, PropPlace.Set);
            PropFootprint df = dais.Footprint;
            List<LevelProp> carried = room.Props.Where(p => p.Footprint.DistanceTo(df) <= 1e-9).ToList();
            string why = carried.All(p => WhollyOn(p.Footprint, df)) ? g.Why(dais, carried) : "a prop stands half on it";
            double half;
            if (why == null)
            {
                mr.LargeProps.Add(dais);
                mr.Raised.AddRange(carried);
                OnTheDais(g, dais);
                foreach (CompositionPiece c in mr.Pieces.Where(c => c.Kind == CompositionKind.Coins)) c.Width = Math.Max(c.Width, 2.75);
                half = PropSize.Of(PropModel.Dais).Width * 0.5;
            }
            else
            {
                mr.LargeLeftOut = "the dais " + why;
                CompositionPiece code = mr.Pieces.FirstOrDefault(c => c.Kind == CompositionKind.Dais);
                half = code != null ? code.Width * 0.5 : chest.Width * 0.5;
            }
            if (!vault || !heaps) return;

            // Two coin heaps beside the dais, a little behind the chest, in place of the code-made heaps.
            Axes(chest.Rotation, out LevelPoint ux, out LevelPoint uz);
            PropSize heap = PropSize.Of(PropModel.CoinHeap);
            int placed = 0;
            foreach (int s in new[] { -1, 1 })
            {
                LevelPoint wanted = Off(chest.Position, ux, s * (half + heap.Width * 0.5 + 0.15), uz, -0.5);
                if (g.Near(PropModel.CoinHeap, wanted, chest.Rotation + s * 15, 1.5) != null) placed++;
            }
            if (placed > 0) mr.Pieces.RemoveAll(c => c.Kind == CompositionKind.CoinHeap);
            else mr.LargeLeftOut = mr.LargeLeftOut ?? "no room for a coin heap";
        }

        // ---------------- The tomb hall ----------------

        static void Knight(Large g)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            double cs = g.Cs, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            PropSize statue = PropSize.Of(PropModel.Statue);
            // At the depth of the empty place (work order 044's, made for the game's statue): its back a little inside the at-wall reach.
            double depth = g.Pv.WallClearance + Math.Max(statue.Width, statue.Depth) * 0.5 + 0.1;
            var walls = new List<int>();
            if (mr.LargePropPlace.HasValue) walls.Add(mr.LargePropSide);
            if (!walls.Contains(mr.FarSide)) walls.Add(mr.FarSide);
            foreach (int w in SideWalls(g.Layout, room, mr.FarSide))
                if (!walls.Contains(w)) walls.Add(w);
            LevelProp knight = null;
            double half = PropSize.Of(PropModel.KnightStatue).Width * 0.5;
            foreach (int w in walls)
            {
                double lo = AlongX(w) ? x0 : y0, hi = AlongX(w) ? x1 : y1;
                double from = mr.LargePropPlace.HasValue && w == mr.LargePropSide
                    ? (AlongX(w) ? mr.LargePropPlace.Value.X : mr.LargePropPlace.Value.Y) : (lo + hi) * 0.5;
                int turn = Facing(PropModel.KnightStatue, PropRules.Inward(w));
                for (int s = 0; knight == null; s++)
                {
                    bool any = false;
                    for (int k = 0; k < (s == 0 ? 1 : 2) && knight == null; k++)
                    {
                        double a = from + (k == 0 ? s : -s) * 0.5;
                        if (a - half < lo + g.Pv.WallClearance || a + half > hi - g.Pv.WallClearance) continue;
                        any = true;
                        var p = LevelProp.Make(PropModel.KnightStatue, OnWall(w, a, depth, x0, x1, y0, y1), turn, PropPlace.Wall);
                        if (g.Add(p)) knight = p;
                    }
                    if (!any) break;
                }
                if (knight == null) continue;
                // A candle cluster either side, in place of the composition's candles there.
                int side = w;
                LevelPoint into = PropRules.Inward(side), alongU = new LevelPoint(-into.Y, into.X);
                double off = PropSize.Of(PropModel.KnightStatue).Width * 0.5 + 0.12 + PropSize.Of(PropModel.CandleCluster).Radius;
                foreach (int sgn in new[] { -1, 1 })
                {
                    var wanted = new LevelPoint(knight.Position.X + alongU.X * off * sgn, knight.Position.Y + alongU.Y * off * sgn);
                    ClusterInPlaceOf(g, wanted, 0.6, new[] { wanted, new LevelPoint(wanted.X + alongU.X * 0.15 * sgn, wanted.Y + alongU.Y * 0.15 * sgn) });
                }
                return;
            }
            mr.LargeLeftOut = "no place for the knight at a wall";
        }

        /// <summary>
        /// A candle cluster at the first of <paramref name="places"/> that keeps the rules, with the composition's candles within
        /// <paramref name="reach"/> of <paramref name="near"/> taken away for it; where none does, they stay.
        /// </summary>
        static bool ClusterInPlaceOf(Large g, LevelPoint near, double reach, IEnumerable<LevelPoint> places)
        {
            var kept = g.Keep();
            foreach (CompositionPiece c in g.Mr.Pieces.Where(c => c.Kind == CompositionKind.Candles && c.Height <= 0 && c.At.DistanceTo(near) < reach).ToList()) g.RemovePiece(c);
            foreach (LevelPoint at in places)
                if (g.Add(LevelProp.Make(PropModel.CandleCluster, at, 0, g.PlaceFor(PropModel.CandleCluster, at)))) return true;
            g.PutBack(kept);
            return false;
        }

        /// <summary>Candle clusters where the composition put code-made candles at the ends of coffins, sarcophagi and bookshelves.</summary>
        static void Clusters(Large g)
        {
            bool tomb = g.Room.Theme == RoomTheme.TombHall;
            foreach (LevelProp p in g.Room.Props.ToList())
            {
                if (g.Mr.Replaced.Contains(p)) continue;
                if (tomb ? p.Model != PropModel.Coffin && p.Model != PropModel.Sarcophagus : p.Model != PropModel.Bookshelf) continue;
                Axes(p.Rotation, out LevelPoint ux, out LevelPoint uz);
                double r = PropSize.Of(PropModel.CandleCluster).Radius;
                foreach (int s in new[] { -1, 1 })
                {
                    LevelPoint code = Off(p.Position, ux, s * (p.Width * 0.5 + (tomb ? 0.3 : 0.28)), uz, 0);
                    if (!g.Mr.Pieces.Any(c => c.Kind == CompositionKind.Candles && c.At.DistanceTo(code) < 1e-6)) continue; // work order 044 put none there
                    var places = new[] { 0.12, 0.25, 0.4 }.Select(gap => Off(p.Position, ux, s * (p.Width * 0.5 + gap + r), uz, 0)).ToList();
                    ClusterInPlaceOf(g, code, 1e-3, places);
                }
            }
        }

        // ---------------- The shrine ----------------

        static void GreatAltar(Large g)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            LevelProp basin = room.Props.FirstOrDefault(p => p.Model == PropModel.Basin);
            LevelProp altar = room.Props.FirstOrDefault(p => p.Model == PropModel.Altar);
            LevelPoint into = PropRules.Inward(mr.FarSide);
            PropSize ga = PropSize.Of(PropModel.GreatAltar);
            LevelPoint wanted;
            if (basin != null) wanted = basin.Position;
            else if (altar != null)
            {
                double off = altar.Depth * 0.5 + g.Tv.BeforeAltar + ga.Depth * 0.5;
                wanted = new LevelPoint(altar.Position.X + into.X * off, altar.Position.Y + into.Y * off);
            }
            else
            {
                mr.LargeLeftOut = "no altar";
                return;
            }
            // In place of the basin's composition: the basin, its round dais and the candles round it.
            var kept = g.Keep();
            if (basin != null)
            {
                mr.Replaced.Add(basin);
                double reach = basin.Radius + 1.0;
                foreach (CompositionPiece c in mr.Pieces.Where(c => (c.Kind == CompositionKind.RoundDais || c.Kind == CompositionKind.Candles) && c.At.DistanceTo(basin.Position) < reach).ToList())
                    g.RemovePiece(c);
                Relane(g);
            }
            int turn = Facing(PropModel.GreatAltar, into);
            LevelProp great = g.Near(PropModel.GreatAltar, wanted, turn, 3.0);
            if (great == null)
            {
                string why = g.Why(LevelProp.Make(PropModel.GreatAltar, wanted, turn, PropPlace.Set));
                if (basin != null)
                {
                    mr.Replaced.Remove(basin);
                    g.PutBack(kept);
                    Relane(g);
                }
                mr.LargeLeftOut = "no place for the great altar within 3 m of where it was wanted, where it " + why;
                return;
            }
            // A candle cluster on the floor at each of its ends.
            Axes(great.Rotation, out LevelPoint ux, out LevelPoint uz);
            double r = PropSize.Of(PropModel.CandleCluster).Radius;
            foreach (int s in new[] { -1, 1 })
                foreach (double gap in new[] { 0.12, 0.3 })
                    if (g.Add(LevelProp.Make(PropModel.CandleCluster, Off(great.Position, ux, s * (ga.Width * 0.5 + gap + r), uz, 0), 0, PropPlace.Set))) break;
        }

        // ---------------- The ossuary ----------------

        static void BoneWalls(Large g, LevelValues values, CryptKitPlan kit)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            double cs = g.Cs, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            PropSize bw = PropSize.Of(PropModel.BoneWall);
            double depth = g.Pv.WallFace + g.Pv.WallPieceGap + bw.Depth * 0.5;
            // On a plain section of the wall, between its piers (3 m of the 3.2 they leave), from the wall's middle outward.
            bool OnWallOf(int w)
            {
                double middle = (AlongX(w) ? x0 + x1 : y0 + y1) * 0.5;
                int turn = Facing(PropModel.BoneWall, PropRules.Inward(w));
                foreach (double a in PlainSections(g.Layout, room, kit, w).OrderBy(a => Math.Abs(a - middle)))
                    if (g.Add(LevelProp.Make(PropModel.BoneWall, OnWall(w, a, depth, x0, x1, y0, y1), turn, PropPlace.Wall))) return true;
                return false;
            }
            // Where the far wall has no free section (its door, its lanterns and niches, the bank before it), a side wall;
            // a second on a side wall in a room larger than the middle of its kind.
            bool far = OnWallOf(mr.FarSide);
            if (!far || RoomThemes.SizeShare(room, values.Plan.SizeOf(room.Kind)) >= 0.5)
                foreach (int w in SideWalls(g.Layout, room, mr.FarSide))
                    if (OnWallOf(w)) break;
            if (!mr.LargeProps.Any(p => p.Model == PropModel.BoneWall)) mr.LargeLeftOut = "no free plain section of the far wall or a side wall";
        }

        // ---------------- Banners ----------------

        static void Banners(Large g, LevelValues values, CryptKitPlan kit)
        {
            MoodRoom mr = g.Mr;
            LevelRoom room = g.Room;
            bool throneRoom = room.Theme == RoomTheme.ThroneRoom;
            int want = throneRoom ? 4 : RoomThemes.SizeShare(room, values.Plan.SizeOf(room.Kind)) >= 0.5 ? 2 : 1;
            double cs = g.Cs, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            int far = mr.FarSide;
            LevelProp throne = room.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
            double farFrom = throneRoom && throne != null ? (AlongX(far) ? throne.Position.X : throne.Position.Y) : (AlongX(far) ? x0 + x1 : y0 + y1) * 0.5;
            double farLine = far == LevelGenerator.North ? y1 : far == LevelGenerator.South ? y0 : far == LevelGenerator.East ? x1 : x0;
            var candidates = new List<(int side, double along)>();
            foreach (double a in PlainSections(g.Layout, room, kit, far).OrderBy(a => Math.Abs(a - farFrom))) candidates.Add((far, a));
            foreach (int w in SideWalls(g.Layout, room, far))
                foreach (double a in PlainSections(g.Layout, room, kit, w).OrderBy(a => Math.Abs(a - farLine))) candidates.Add((w, a));
            PropSize bs = PropSize.Of(PropModel.Banner);
            double depth = g.Pv.WallFace + g.Pv.WallPieceGap + bs.Depth * 0.5;
            int placed = 0;
            foreach ((int side, double along) in candidates)
            {
                if (placed >= want) break;
                var p = LevelProp.Make(PropModel.Banner, OnWall(side, along, depth, x0, x1, y0, y1), Facing(PropModel.Banner, PropRules.Inward(side)), PropPlace.Wall);
                if (g.Add(p)) placed++;
            }
            // The throne room's in pairs.
            if (throneRoom && placed == 3) mr.LargeProps.RemoveAt(mr.LargeProps.FindLastIndex(p => p.Model == PropModel.Banner));
        }

        // ---------------- The rest of the room's mood ----------------

        /// <summary>The sources the large props are and the replaced props were; the wear a large prop stands on goes.</summary>
        static void Finish(Large g)
        {
            MoodRoom mr = g.Mr;
            foreach (LevelProp p in mr.Replaced)
                mr.Sources.RemoveAll(s => s.OfProp && s.At.DistanceTo(p.Position) < 1e-6);
            foreach (LevelProp p in mr.LargeProps)
            {
                switch (p.Model)
                {
                    case PropModel.GreatBrazier: mr.Sources.Add(new MoodSource { Room = g.Room.Order, Kind = MoodSourceKind.Brazier, At = p.Position, Height = 1.5, Flame = true, OfProp = true }); break;
                    case PropModel.GreatAltar: mr.Sources.Add(new MoodSource { Room = g.Room.Order, Kind = MoodSourceKind.Brazier, At = p.Position, Height = 1.82, OfProp = true }); break;
                    case PropModel.CandleCluster: mr.Sources.Add(new MoodSource { Room = g.Room.Order, Kind = MoodSourceKind.Candle, At = p.Position, Height = 1.0, OfProp = true }); break;
                }
            }
            // A real light goes to the first ones, as before: braziers, the gold, then candles, nearest the middle first.
            LevelPoint m = g.Room.Middle(g.Cs);
            List<MoodSource> ranked = mr.Sources.OrderBy(s => s.Kind == MoodSourceKind.Brazier ? 0 : s.Kind == MoodSourceKind.Gold ? 1 : 2)
                                                .ThenBy(s => s.At.DistanceTo(m)).ToList();
            mr.Sources.Clear();
            mr.Sources.AddRange(ranked);
            mr.Wear.RemoveAll(w => mr.LargeProps.Any(p => p.Model != PropModel.Banner && p.Footprint.DistanceTo(w.At) - w.Reach < 0.05));
        }
    }
}
