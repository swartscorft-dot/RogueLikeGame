using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The room themes and the seven new props (work order 028; Kristian, 2026-11-19 and 2026-11-20: "good variety", and
    /// more of the crypt): each theme's recipe gives props only of its list; over a thousand seeds per floor number with
    /// the themes on, every prop keeps every rule, every room keeps its lane and two thirds of its floor, no combat theme
    /// comes twice in a row, the wall pieces stand at walls facing in, the throne stands at the exit room's north wall
    /// facing south wherever that wall can hold it, and the layouts and enemies are those with the themes off; the themes
    /// stream is its own; the map and the description say the themes; and the validator reports each new fault. The
    /// numbers the order asks for (how often each theme comes up, what was left out, cover and floor per theme against
    /// the rooms of before) are written as "THEMESTATS".
    /// </summary>
    public class RoomThemeTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static LevelLayout Make(ulong seed, int floor = 1, int propsWarmUp = 0) => LevelGenerator.Generate(seed, floor, new LevelValues(), 0, propsWarmUp);

        static LevelValues Off() => new LevelValues { Themes = false };

        /// <summary>The cover a prop gives, as the order counts it: a tall prop's width (a circle's across), else 0.</summary>
        static double CoverWidth(LevelProp p, PropValues v) => !p.IsCover(v) ? 0 : p.Shape == PropShape.Circle ? 2 * p.Radius : p.Width;

        /// <summary>
        /// The longest one generation with themes may take, in milliseconds (as LevelPropsTests'). The slowest seed is timed
        /// again for it, as one time can be slow for the machine's sake. Work order 088 (changed deliberately): 150 ms, LevelPropsTests'
        /// bound, 50 before; version 19's props stage does more (the joined floor, the fights' cover: about 1.7 times version 18's
        /// generation on .NET), and the filtered Unity EditMode run of it under load found the slowest seed of a floor 51 to 87 ms.
        /// </summary>
        const double SlowestAllowedMs = 150;

        sealed class Tally
        {
            public int Rooms, Wanted, Placed;
            public double Tall, CoverMetres, Taken, Walkable, LeastWalkable = 1, Last;

            public void Add(LevelLayout l, LevelRoom r, PropValues v)
            {
                Rooms++;
                Wanted += r.PropsWanted;
                Placed += r.Props.Count;
                Tall += r.Props.Count(p => p.IsCover(v));
                CoverMetres += r.Props.Sum(p => CoverWidth(p, v));
                Taken += r.Props.Sum(p => p.Area) / (r.Rect.Width * r.Rect.Height * l.CellSize * l.CellSize);
                double w = Last = PropRules.WalkableShare(l, r, v);
                Walkable += w;
                LeastWalkable = Math.Min(LeastWalkable, w);
            }

            public string Say(string name) => string.Format(s_Inv,
                "{0}: {1} rooms, props {2:0.0} of {3:0.0} wanted ({4:0.0%} left out), tall {5:0.00} ({6:0.00} m wide), floor taken {7:0.0%}, walkable {8:0.0%} (least {9:0.0%})",
                name, Rooms, Placed / (double)Rooms, Wanted / (double)Rooms, Wanted > 0 ? (Wanted - Placed) / (double)Wanted : 0,
                Tall / Rooms, CoverMetres / Rooms, Taken / Rooms, Walkable / Rooms, LeastWalkable);
        }

        /// <summary>True when some place along the north wall at the throne's depth keeps every rule with the room empty of props.</summary>
        static bool NorthWallCouldHoldTheThrone(LevelLayout l, LevelRoom r, LevelValues v)
        {
            PropValues pv = v.PropRules;
            PropSize s = PropSize.Of(PropModel.BoneThrone);
            double cs = l.CellSize, y = r.Rect.Top * cs - (pv.WallFace + pv.WallPieceGap + s.Depth * 0.5);
            var lane = new PropRules.Lane(l, r, pv);
            List<PropRules.WallSpan> niches = PropRules.NichesOf(l, r, PropRules.KitPlanForProps(l));
            var none = new List<LevelProp>();
            for (double x = r.Rect.X * cs + pv.PlaceStep; x < r.Rect.Right * cs - 1e-9; x += pv.PlaceStep)
            {
                LevelProp p = LevelProp.Make(PropModel.BoneThrone, new LevelPoint(x, y), 0, PropPlace.Wall);
                if (PropRules.Fault(l, r, p, none, pv, null, niches) == null && lane.Clear(p.Footprint)) return true;
            }
            return false;
        }

        // ---------------- A thousand seeds per floor ----------------

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_WithThemes_KeepEveryRule_AndTheirLayoutsAndEnemies(int floor)
        {
            var values = new LevelValues();
            var noProps = new LevelValues { Props = false };
            PropValues pv = values.PropRules;
            LevelGenerator.Generate(424242UL, floor); // the first call pays for compiling the code
            var byTheme = new Dictionary<string, Tally>();
            var counts = new Dictionary<string, int>();
            int repeats = 0, throneNorth = 0, throneFar = 0, throneElsewhere = 0, throneNone = 0, wallPieces = 0;
            int throneTemplated = 0, throneTemplatedNotNorth = 0, throneTemplatedMissed = 0;
            double slowest = 0, total = 0;
            ulong slowestSeed = 0;
            var timer = new Stopwatch();
            void Count(string key)
            {
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
            }

            for (ulong s = 0; s < 1000; s++)
            {
                timer.Restart();
                LevelLayout level = LevelGenerator.Generate(s, floor);
                timer.Stop();
                double ms = timer.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > slowest) { slowest = ms; slowestSeed = s; }
                string where = "seed " + s + " floor " + floor;

                List<string> faults = LevelValidator.Validate(level);
                if (faults.Count > 0) Assert.Fail(where + ": " + string.Join("; ", faults) + "\n" + LevelMap.Draw(level));
                // The rooms, doors and enemies are those of the floor without props, which has no themes stage either (and
                // the props never change them: LevelPropsTests); with the themes off: TheSums_PerTheme_AgainstTheRoomsOfBefore.
                if (level.Describe(props: false) != LevelGenerator.Generate(s, floor, noProps).Describe(props: false))
                    Assert.Fail(where + ": the themes changed its rooms, doors or enemies");

                List<LevelRoom> chain = LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();
                for (int i = 1; i < chain.Count; i++)
                    if (chain[i].Theme == chain[i - 1].Theme) repeats++;
                CryptKitPlan kit = PropRules.KitPlanForProps(level);

                foreach (LevelRoom r in level.Rooms)
                {
                    if (r.Kind == RoomKind.Passage) // work order 059: a passage between two rooms has no theme and holds nothing
                    {
                        if (r.Theme != RoomTheme.None || r.Props.Count > 0) Assert.Fail(where + " " + r.Letter + r.Order + ": a passage with a theme or props");
                        continue;
                    }
                    if ((r.Theme == RoomTheme.None) != (r.Kind == RoomKind.Entrance)) Assert.Fail(where + " " + r.Letter + r.Order + ": a theme for every room but the entrance");
                    if (r.Theme == RoomTheme.None) continue;
                    ThemeRecipe recipe = values.ThemeRules.Of(r.Theme, r.Kind);
                    Assert.That(recipe, Is.Not.Null, where);
                    PropModel[] list = RoomThemes.ModelsOf(recipe);
                    foreach (LevelProp p in r.Props)
                    {
                        // Work order 088 (changed deliberately): a cover put for the room's fight is its theme's cover model.
                        if (p.Fight && Array.IndexOf(FightCover.CoverModels(r), p.Model) >= 0) continue;
                        if (Array.IndexOf(list, p.Model) < 0) Assert.Fail(where + " " + RoomThemes.Word(r.Theme) + ": " + p + " is not of its theme's list");
                    }

                    string name = RoomThemes.Word(r.Theme) + (r.Kind == RoomKind.Treasure ? " (treasure room)" : "");
                    if (!byTheme.TryGetValue(name, out Tally t)) byTheme[name] = t = new Tally();
                    t.Add(level, r, pv);
                    Count(name);
                    if (r.Elite) Count("elite room " + RoomThemes.Word(r.Theme));
                    if (r.Kind == RoomKind.Combat && t.Last < pv.MinWalkable)
                        Assert.Fail(where + " " + r.Letter + r.Order + " keeps " + t.Last + " of its floor walkable, less than two thirds");

                    // Wall pieces at a wall, their backs within a hand of its face, facing in, never at the south wall nor a niche.
                    List<PropRules.WallSpan> niches = PropRules.NichesOf(level, r, kit);
                    niches.AddRange(PropRules.CarvedNichesOf(level, r, kit)); // work order 051
                    foreach (LevelProp p in r.Props.Where(p => PropSize.Of(p.Model).WallPiece))
                    {
                        wallPieces++;
                        string what = where + " " + r.Letter + r.Order + ": " + p;
                        Assert.That(p.Place, Is.EqualTo(PropPlace.Wall), what);
                        // Work order 051: its wall may be a carved cell's face (an alcove's back), measured as a side.
                        WallFace face = RoomShapes.NearestFace(r, p.Footprint, level.CellSize, out double back);
                        int side = face.Side;
                        Assert.That(back, Is.InRange(pv.WallFace - 1e-6, pv.WallFace + pv.HandWidth + 1e-6), "its back within a hand of the wall's face: " + what);
                        LevelPoint into = PropRules.Inward(side), front = p.Front;
                        Assert.That(front.X * into.X + front.Y * into.Y, Is.GreaterThan(0.99), "facing into the room: " + what);
                        Assert.That(side, Is.Not.EqualTo(LevelGenerator.South), "never at the low south wall: " + what);
                        p.Footprint.Extent(out double minX, out double minY, out double maxX, out double maxY);
                        bool alongX = side == LevelGenerator.North || side == LevelGenerator.South;
                        foreach (PropRules.WallSpan n in niches.Where(n => n.Side == side && n.Carved == face.Carved && (!n.Carved || n.Line == face.Line)))
                            Assert.That((alongX ? maxX : maxY) <= n.From + 1e-9 || (alongX ? minX : minY) >= n.To - 1e-9, "not in front of a niche: " + what);
                    }

                    if (r.Kind == RoomKind.Exit)
                    {
                        Assert.That(r.Theme, Is.EqualTo(RoomTheme.ThroneRoom), where);
                        LevelProp throne = r.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                        int side = throne != null ? throne.Footprint.NearestSide(r.Rect, level.CellSize, out _) : -1;
                        // Work order 058: in an exit room drawn by hand its far wall (its A's, never the south one) is the throne's.
                        int wanted = r.Template != null ? r.Template.FarWall : LevelGenerator.North;
                        // Work order 082 (review 6, finding 16): a templated room's throne at its template's far wall, the north
                        // wall no longer passing for it (the north wall's own check below is for a room without a template).
                        if (r.Template != null)
                        {
                            throneTemplated++;
                            if (wanted != LevelGenerator.North) throneTemplatedNotNorth++;
                            if (side != wanted) throneTemplatedMissed++;
                            if (side == wanted)
                            {
                                throneNorth++;
                                if (wanted != LevelGenerator.North) throneFar++;
                                LevelPoint inward = PropRules.Inward(side);
                                Assert.That(throne.Front.X * inward.X + throne.Front.Y * inward.Y, Is.GreaterThan(0.99), "the throne faces into the room from its far wall: " + where);
                            }
                            else if (throne == null) throneNone++;
                            else throneElsewhere++;
                        }
                        else if (side == wanted)
                        {
                            throneNorth++;
                            if (side == wanted && wanted != LevelGenerator.North) throneFar++;
                            LevelPoint into = PropRules.Inward(side);
                            Assert.That(throne.Front.X * into.X + throne.Front.Y * into.Y, Is.GreaterThan(0.99), "the throne faces into the room from its wall: " + where);
                        }
                        else
                        {
                            // The north wall could not hold it: then a side wall, facing in, or nothing.
                            Assert.That(NorthWallCouldHoldTheThrone(level, r, values), Is.False, where + ": the north wall could have held the throne\n" + LevelMap.Draw(level));
                            if (throne == null) throneNone++;
                            else throneElsewhere++;
                        }
                    }
                }
            }

            // The slowest seed timed again, the best of three: one slow time can be the machine's (other work, the collector),
            // not the generator's.
            double again = double.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                timer.Restart();
                LevelGenerator.Generate(slowestSeed, floor);
                timer.Stop();
                again = Math.Min(again, timer.Elapsed.TotalMilliseconds);
            }
            TestContext.WriteLine(string.Format(s_Inv,
                "THEMESTATS floor {0} seeds 0-999: {1}; combat themes twice in a row {2}; throne at the north wall or its template's far wall {3} (the far wall not the north {11}), at a side wall {4}, left out {5}; " +
                "wall pieces {6}; slowest generation {7:0.00} ms (seed {8}; timed again {10:0.00} ms), mean {9:0.000} ms",
                floor, string.Join(", ", counts.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)), repeats, throneNorth, throneElsewhere, throneNone,
                wallPieces, slowest, slowestSeed, total / 1000, again, throneFar));
            foreach (KeyValuePair<string, Tally> t in byTheme.OrderBy(k => k.Key)) TestContext.WriteLine("THEMESTATS " + t.Value.Say(t.Key));
            TestContext.WriteLine(string.Format(s_Inv, "THEMESTATS floor {0}: templated exit rooms {1} ({2} with a far wall not the north), the throne off its far wall in {3}",
                floor, throneTemplated, throneTemplatedNotNorth, throneTemplatedMissed));

            Assert.That(repeats, Is.EqualTo(0), "no combat theme twice in a row (with four there is always another)");
            Assert.That(again, Is.LessThan(SlowestAllowedMs), "slowest generation with themes, ms (seed " + slowestSeed + ", timed again)");
            Assert.That(throneNorth, Is.GreaterThan(950), "the throne at the north wall (its template's far wall) nearly always");
            // Work order 082: the far wall that is not the north one is seen and held.
            if (throneTemplatedNotNorth > 0) Assert.That(throneFar, Is.GreaterThan(0), "a throne at a far wall that is not the north one");
            Assert.That(throneTemplatedMissed, Is.LessThanOrEqualTo(throneTemplated / 20), "a templated room's throne off its far wall in at most 1 room of 20");
        }

        // The sums for the fights (work order 028): each theme's cover and floor against the combat rooms of before (the
        // same seeds with the themes off): the ossuary clearly more open, the tomb hall clearly more closed and the most
        // closed of the four; and with the themes off the rooms, doors and enemies are the same.
        [Test]
        public void TheSums_PerTheme_AgainstTheRoomsOfBefore()
        {
            var values = new LevelValues();
            PropValues pv = values.PropRules;
            var byTheme = new Dictionary<string, Tally>();
            var before = new Tally();
            for (ulong s = 0; s < 500; s++)
            {
                LevelLayout level = Make(s), plain = LevelGenerator.Generate(s, 1, Off());
                Assert.That(plain.Rooms.All(r => r.Theme == RoomTheme.None), "seed " + s + ": no theme with the themes off");
                if (level.Describe(props: false) != plain.Describe(props: false)) Assert.Fail("seed " + s + ": the themes off and on, other rooms, doors or enemies");
                foreach (LevelRoom r in plain.Rooms.Where(r => r.Kind == RoomKind.Combat)) before.Add(plain, r, pv);
                foreach (LevelRoom r in level.Rooms.Where(r => r.Kind == RoomKind.Combat))
                {
                    string name = RoomThemes.Word(r.Theme);
                    if (!byTheme.TryGetValue(name, out Tally t)) byTheme[name] = t = new Tally();
                    t.Add(level, r, pv);
                }
            }
            TestContext.WriteLine("THEMESUMS seeds 0-499 floor 1, " + before.Say("combat rooms with the themes off (as before)"));
            foreach (KeyValuePair<string, Tally> t in byTheme.OrderBy(k => k.Key)) TestContext.WriteLine("THEMESUMS " + t.Value.Say(t.Key));
            double cover = before.CoverMetres / before.Rooms;
            double Cover(string theme) => byTheme[theme].CoverMetres / byTheme[theme].Rooms;
            Assert.That(Cover("ossuary"), Is.LessThan(cover * 0.75), "the ossuary has clearly less cover");
            Assert.That(Cover("tomb hall"), Is.GreaterThan(cover * 1.5), "the tomb hall has clearly more cover");
            foreach (string combat in new[] { "ossuary", "shrine", "archive" })
                Assert.That(Cover(combat), Is.LessThan(Cover("tomb hall")), "the tomb hall has the most cover, " + combat + " less");
            foreach (KeyValuePair<string, Tally> t in byTheme)
                Assert.That(t.Value.LeastWalkable, Is.GreaterThanOrEqualTo(pv.MinWalkable), t.Key + " keeps two thirds of its floor walkable");
        }

        // ---------------- Which room gets which ----------------

        [Test]
        public void EveryTheme_ComesUp_TheEliteRoomFavoursTheShrine_AndTheExitIsTheThroneRoom()
        {
            var combat = new Dictionary<RoomTheme, int>();
            var treasure = new Dictionary<RoomTheme, int>();
            int combats = 0, elites = 0, eliteShrines = 0, otherShrines = 0;
            for (ulong s = 0; s < 500; s++)
            {
                LevelLayout level = Make(s);
                foreach (LevelRoom r in level.Rooms)
                {
                    if (r.Kind == RoomKind.Exit) Assert.That(r.Theme, Is.EqualTo(RoomTheme.ThroneRoom), "seed " + s);
                    if (r.Kind == RoomKind.Entrance) Assert.That(r.Theme, Is.EqualTo(RoomTheme.None), "seed " + s);
                    if (r.Kind == RoomKind.Combat)
                    {
                        combat.TryGetValue(r.Theme, out int n);
                        combat[r.Theme] = n + 1;
                        if (r.Elite) { elites++; if (r.Theme == RoomTheme.Shrine) eliteShrines++; }
                        else { combats++; if (r.Theme == RoomTheme.Shrine) otherShrines++; }
                    }
                    if (r.Kind == RoomKind.Treasure)
                    {
                        Assert.That(r.Theme, Is.EqualTo(RoomTheme.Vault).Or.EqualTo(RoomTheme.Archive), "seed " + s);
                        treasure.TryGetValue(r.Theme, out int n);
                        treasure[r.Theme] = n + 1;
                        LevelRoom parent = level.Rooms[level.DoorsOf(r.Order).First().Other(r.Order)];
                        if (parent.Theme == RoomTheme.Archive) Assert.That(r.Theme, Is.EqualTo(RoomTheme.Vault), "no archive off an archive, seed " + s);
                        Assert.That(r.Props.Count(p => p.Model == PropModel.Chest), Is.EqualTo(1), "every treasure room keeps its chest, seed " + s);
                    }
                }
            }
            TestContext.WriteLine("THEMEDRAW seeds 0-499 floor 1: combat rooms " + string.Join(", ", combat.OrderBy(k => k.Key).Select(k => RoomThemes.Word(k.Key) + " " + k.Value))
                                  + "; treasure rooms " + string.Join(", ", treasure.OrderBy(k => k.Key).Select(k => RoomThemes.Word(k.Key) + " " + k.Value))
                                  + "; elite rooms that are shrines " + eliteShrines + " of " + elites + ", other combat rooms " + otherShrines + " of " + combats);
            foreach (RoomTheme t in RoomThemes.CombatThemes)
                Assert.That(combat.TryGetValue(t, out int n) ? n : 0, Is.GreaterThan(200), RoomThemes.Word(t) + " comes up");
            Assert.That(treasure[RoomTheme.Vault], Is.GreaterThan(treasure[RoomTheme.Archive]), "more vaults than archives");
            Assert.That(treasure[RoomTheme.Archive], Is.GreaterThan(100));
            Assert.That(eliteShrines / (double)elites, Is.GreaterThan(1.5 * otherShrines / combats), "the shrine is favoured for the elite room");
        }

        [Test]
        public void ThemesStream_IsItsOwn_ThePropsDrawsLeaveTheThemes()
        {
            ulong first = LevelRandom.Stream(9UL, LevelGenerator.ThemesStage).NextBits();
            foreach (string other in new[] { LevelGenerator.LayoutStage, LevelGenerator.ContentsStage, LevelGenerator.PropsStage, CryptKitPlan.Stage })
                Assert.That(LevelRandom.Stream(9UL, other).NextBits(), Is.Not.EqualTo(first), other);
            for (ulong s = 0; s < 30; s++)
            {
                LevelLayout plain = Make(s), shifted = Make(s, 1, propsWarmUp: 5);
                Assert.That(shifted.Rooms.Select(r => r.Theme), Is.EqualTo(plain.Rooms.Select(r => r.Theme)), "seed " + s);
                Assert.That(LevelValidator.Validate(shifted), Is.Empty, "seed " + s);
            }
        }

        [Test]
        public void ThemesOff_NoRoomHasATheme_AndNoNewPropStands()
        {
            var olds = new HashSet<PropModel>((PropModel[])Enum.GetValues(typeof(PropModel)));
            olds.ExceptWith(new[] { PropModel.Coffin, PropModel.SkullPile, PropModel.Candelabra, PropModel.BoneThrone, PropModel.Bookshelf, PropModel.Cage, PropModel.Basin });
            olds.RemoveWhere(m => PropSize.Of(m).Large); // changed deliberately (work order 052): the eight large props, which the props stage never puts down
            Assert.That(olds.Count, Is.EqualTo(11));
            for (ulong s = 0; s < 100; s++)
                for (int floor = 1; floor <= 5; floor += 4)
                {
                    LevelLayout level = LevelGenerator.Generate(s, floor, Off());
                    Assert.That(level.Rooms.All(r => r.Theme == RoomTheme.None));
                    Assert.That(level.Rooms.SelectMany(r => r.Props).All(p => olds.Contains(p.Model)), "seed " + s);
                    Assert.That(LevelMap.Draw(level).Split('\n')[0], Does.Not.Contain("themes"));
                }
        }

        [Test]
        public void EachRecipe_GivesPropsOnlyOfItsList_AndMoreInALargerRoom()
        {
            var v = new ThemeValues();
            foreach (ThemeRecipe r in v.Recipes)
            {
                Assert.That(r.Props, Is.Not.Empty, RoomThemes.Word(r.Theme));
                Assert.That(r.Props.All(p => p.Models.Length > 0 && p.Fewest <= p.Most), RoomThemes.Word(r.Theme));
                Assert.That(r.MostProps, Is.EqualTo(r.Props.Sum(p => p.Most)));
                Assert.That(RoomThemes.ThemesFor(r.Kind), Does.Contain(r.Theme), "a " + RoomThemes.Word(r.Theme) + " is for a " + r.Kind + " room");
                foreach (ThemeProp line in r.Props)
                    Assert.That(RoomThemes.CountOf(line, 1), Is.GreaterThanOrEqualTo(RoomThemes.CountOf(line, 0)));
            }
            Assert.That(v.Of(RoomTheme.Archive, RoomKind.Combat), Is.Not.SameAs(v.Of(RoomTheme.Archive, RoomKind.Treasure)), "the archive has a recipe for each");
            Assert.That(RoomThemes.ModelsOf(v.Of(RoomTheme.Ossuary, RoomKind.Combat)), Has.No.Member(PropModel.Bookshelf));
            Assert.That(RoomThemes.ModelsOf(v.Of(RoomTheme.ThroneRoom, RoomKind.Exit)), Has.Member(PropModel.BoneThrone).And.No.Member(PropModel.Altar));
        }

        // ---------------- The throne ----------------

        [Test]
        public void TheThrone_GoesToASideWall_WhenTheNorthWallCannotHoldIt()
        {
            var values = new LevelValues();
            var noProps = new LevelValues { Props = false };
            int sideWall = 0, leftOut = 0;
            for (ulong s = 0; s < 20; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, 1, noProps);
                LevelRoom exit = level.ExitRoom;
                double cs = level.CellSize;
                // Enemies all along the north wall, 2.5 m out: no place there keeps 2 m from them.
                for (double x = exit.Rect.X * cs + 1.5; x < exit.Rect.Right * cs - 1; x += 3)
                {
                    var p = new LevelPoint(x, exit.Rect.Top * cs - 2.5);
                    exit.EnemyPoints.Add(p);
                    exit.EnemyGroups.Add(new EnemyGroup(EnemyKind.Brute));
                    exit.EnemyPlaces.Add(new[] { p });
                }
                Assert.That(NorthWallCouldHoldTheThrone(level, exit, values), Is.False);
                RoomThemes.Assign(level, LevelRandom.Stream(s, LevelGenerator.ThemesStage), values);
                PropPlacer.Place(level, LevelRandom.Stream(s, LevelGenerator.PropsStage), values);

                LevelProp throne = exit.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                if (throne == null)
                {
                    leftOut++;
                    continue;
                }
                int side = throne.Footprint.NearestSide(exit.Rect, cs, out _);
                Assert.That(side, Is.EqualTo(LevelGenerator.East).Or.EqualTo(LevelGenerator.West), "seed " + s + ": a side wall, never the south");
                LevelPoint into = PropRules.Inward(side), front = throne.Front;
                Assert.That(front.X * into.X + front.Y * into.Y, Is.GreaterThan(0.99), "facing into the room");
                var niches = PropRules.NichesOf(level, exit, PropRules.KitPlanForProps(level));
                foreach (LevelProp p in exit.Props)
                    Assert.That(PropRules.Fault(level, exit, p, exit.Props, values.PropRules, null, niches), Is.Null, "seed " + s + ": " + p);
                sideWall++;
            }
            TestContext.WriteLine("THRONE with the north wall taken: at a side wall " + sideWall + ", left out " + leftOut + " of 20");
            Assert.That(sideWall, Is.GreaterThan(10));
        }

        // ---------------- What the map and the description say ----------------

        [Test]
        public void TheMapsLegend_AndTheDescription_SayEachRoomsTheme()
        {
            LevelLayout level = Make(42UL);
            string first = LevelMap.Draw(level).Split('\n')[0];
            string description = level.Describe();
            foreach (LevelRoom r in level.Rooms.Where(r => r.Theme != RoomTheme.None))
            {
                Assert.That(first, Does.Contain(r.Letter + r.Order.ToString(s_Inv) + " " + RoomThemes.Word(r.Theme)), first);
                Assert.That(description, Does.Contain("  theme " + RoomThemes.Word(r.Theme) + "\n"));
            }
            Assert.That(level.Describe(props: false), Does.Not.Contain("theme"), "the themes go with the props");
            Assert.That(RoomThemes.Word(RoomTheme.TombHall), Is.EqualTo("tomb hall"));
            Assert.That(RoomThemes.Word(RoomTheme.None), Is.Empty);
        }

        // ---------------- The validator ----------------

        static void AssertFault(Action<LevelLayout> breakIt, string expected)
        {
            LevelLayout level = Make(42UL);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            breakIt(level);
            List<string> faults = LevelValidator.Validate(level);
            Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  "
                        + string.Join("\n  ", faults) + "\n" + LevelMap.Draw(level));
        }

        static LevelProp Throne(LevelLayout l) => l.ExitRoom.Props.First(p => p.Model == PropModel.BoneThrone);

        [Test]
        public void Validator_ReportsAWallPieceOffTheWallsFace() =>
            // Work order 058: 0.3 m out from its wall, whichever wall its template's far wall is (the north one's before).
            AssertFault(l => { LevelProp t = Throne(l); LevelPoint f = t.Front; t.Position = new LevelPoint(t.Position.X + f.X * 0.3, t.Position.Y + f.Y * 0.3); }, "not within");

        [Test]
        public void Validator_ReportsAWallPieceFacingTheWall() =>
            AssertFault(l => { LevelProp t = Throne(l); t.Rotation = (t.Rotation + 180) % 360; }, "does not face into the room from its wall");

        [Test]
        public void Validator_ReportsAWallPieceThatDoesNotStandAtAWall() =>
            AssertFault(l => Throne(l).Place = PropPlace.Set, "is a wall piece and does not stand at a wall");

        [Test]
        public void Validator_ReportsATallPropAtTheSouthWall() =>
            AssertFault(l =>
            {
                LevelRoom r = l.ExitRoom;
                double cs = l.CellSize;
                r.Props.Add(LevelProp.Make(PropModel.Statue, new LevelPoint(r.Middle(cs).X + 6, r.Rect.Y * cs + 0.1 + 0.55), 0, PropPlace.Wall));
            }, "stands at the south wall");

        [Test]
        public void Validator_ReportsAPropNotOfItsThemesList() =>
            AssertFault(l => { LevelProp t = Throne(l); l.ExitRoom.Props[l.ExitRoom.Props.IndexOf(t)] = LevelProp.Make(PropModel.Altar, t.Position, 0, PropPlace.Wall); },
                        "does not belong in a throne room");

        [Test]
        public void Validator_ReportsTheSameThemeTwiceInARow() =>
            AssertFault(l =>
            {
                List<LevelRoom> chain = LevelGeometry.MainPath(l).Where(r => r.Kind == RoomKind.Combat).ToList();
                chain[1].Theme = chain[0].Theme;
            }, "next to each other on the chain");

        [Test]
        public void Validator_ReportsAThemeARoomOfItsKindIsNever() =>
            AssertFault(l => l.ExitRoom.Theme = RoomTheme.Ossuary, "which a Exit room is never");

        [Test]
        public void Validator_ReportsAWallPieceInFrontOfANiche()
        {
            // A seed whose exit room has a niche on its north wall, and the throne moved in front of it.
            for (ulong s = 0; s < 200; s++)
            {
                LevelLayout l = Make(s);
                List<PropRules.WallSpan> niches = PropRules.NichesOf(l, l.ExitRoom, PropRules.KitPlanForProps(l))
                    .Where(n => n.Side == LevelGenerator.North).ToList();
                LevelProp t = l.ExitRoom.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                if (niches.Count == 0 || t == null || t.Footprint.NearestSide(l.ExitRoom.Rect, l.CellSize, out _) != LevelGenerator.North) continue;
                Assert.That(LevelValidator.Validate(l), Is.Empty);
                t.Position = new LevelPoint((niches[0].From + niches[0].To) * 0.5, t.Position.Y);
                List<string> faults = LevelValidator.Validate(l);
                Assert.That(faults.Any(f => f.Contains("stands in front of a wall niche")), string.Join("\n", faults));
                return;
            }
            Assert.Inconclusive("no seed of 0 to 199 has a niche on its exit room's north wall");
        }

        [Test]
        public void Validator_ReportsACombatRoomWithTooLittleFloorLeft() =>
            AssertFault(l =>
            {
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat);
                double cs = l.CellSize;
                for (double x = r.Rect.X * cs + 2; x < r.Rect.Right * cs - 2; x += 2.2)
                    for (double y = r.Rect.Y * cs + 2; y < r.Rect.Top * cs - 2; y += 1.6)
                        r.Props.Add(LevelProp.Make(PropModel.Rubble, new LevelPoint(x, y), 0, PropPlace.Floor));
            }, "of its floor walkable, less than");
    }
}
