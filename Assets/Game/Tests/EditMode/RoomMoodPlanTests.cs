using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The rooms' mood (work order 044; Kristian, 2026-11-20: "The rooms must also feel more alive and create atmosphere like
    /// Diablo and Path of Exile", and of the concept pictures, "build toward them"): its stream changes no seed's layout,
    /// enemies or props, but for the ossuary's piles, banked at its far wall on purpose (pinned here); the wear never lies on a
    /// doorway, the room's middle, an enemy's or her place, or (lying on the floor as rubble and bones) on a prop; each theme
    /// has its eye-catcher and its empty place for a large prop, where the game's statue would keep every rule; generation is
    /// as quick as it was; the plan is the same for the same seed; the room tone loops quietly and cleanly. The numbers are
    /// written as "MOODSTATS".
    /// </summary>
    public class RoomMoodPlanTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static LevelValues Off() => new LevelValues { RoomMood = false };

        static string Models(LevelRoom r) => string.Join(",", r.Props.Select(p => p.Model.ToString()).OrderBy(x => x, StringComparer.Ordinal));

        // ---------------- The stream changes nothing but the ossuary's bank ----------------

        [TestCase(1, 1000)]
        [TestCase(2, 250)]
        [TestCase(3, 250)]
        [TestCase(4, 250)]
        [TestCase(5, 1000)]
        public void TheMasterOnAndOff_GiveTheSameFloors_ButTheOssuarysPiles(int floor, int seeds)
        {
            // Work order 088 (changed deliberately): the rooms you can use off on every side, so the bank alone is measured (with
            // them on, the cover given to an ossuary's fight follows where its piles stand).
            var on = new LevelValues { UsableRooms = false };
            Assert.That(on.RoomMood && on.Mood.Compose, "on by default");
            // Work order 051: the rooms' carved outlines off on both sides, so this measures the bank on the walls it was made for
            // (with them on an ossuary's far wall may be an apse, and the bank holds a few less: 94.5 % on floor 5).
            on.Shapes = false;
            var composeOff = new LevelValues { Shapes = false, UsableRooms = false };
            composeOff.Mood.Compose = false;
            int ossuaries = 0, sameModels = 0, otherRooms = 0;
            for (ulong s = 0; s < (ulong)seeds; s++)
            {
                LevelValues off = Off();
                off.Shapes = false;
                off.UsableRooms = false;
                LevelLayout a = LevelGenerator.Generate(s, floor, on), b = LevelGenerator.Generate(s, floor, off);
                string where = "seed " + s + " floor " + floor;
                Assert.That(a.Describe(props: false), Is.EqualTo(b.Describe(props: false)), "layout and enemies, " + where);
                for (int i = 0; i < a.Rooms.Count; i++)
                {
                    LevelRoom ra = a.Rooms[i], rb = b.Rooms[i];
                    Assert.That(ra.Theme, Is.EqualTo(rb.Theme), where);
                    if (ra.Theme == RoomTheme.Ossuary)
                    {
                        ossuaries++;
                        if (Models(ra) == Models(rb)) sameModels++;
                        continue;
                    }
                    otherRooms++;
                    Assert.That(string.Join("\n", ra.Props.Select(p => p.ToString())), Is.EqualTo(string.Join("\n", rb.Props.Select(p => p.ToString()))),
                                "every room but an ossuary keeps its props to the millimetre, " + where + " room " + ra.Letter + ra.Order);
                }
                if (s % 5 == 0)
                {
                    // The eye-catchers off: the floor is the master's off, field for field; making the plan changes nothing of it.
                    LevelLayout c = LevelGenerator.Generate(s, floor, composeOff);
                    Assert.That(c.Describe(), Is.EqualTo(b.Describe()), "compositions off, " + where);
                    string before = a.Describe();
                    RoomMoodPlan.Make(a, on);
                    Assert.That(a.Describe(), Is.EqualTo(before), "making the plan changes nothing of the floor, " + where);
                }
            }
            TestContext.WriteLine(string.Format(s_Inv, "MOODSTATS floor {0} seeds 0-{1}: {2} other rooms the same to the millimetre; {3} ossuaries, {4} holding the same props ({5:0.0%}) where the bank moved them",
                                                floor, seeds - 1, otherRooms, ossuaries, sameModels, ossuaries > 0 ? sameModels / (double)ossuaries : 1));
            Assert.That(ossuaries, Is.GreaterThan(0));
            Assert.That(sameModels, Is.GreaterThanOrEqualTo(ossuaries * 95 / 100), "an ossuary holds the same props, only elsewhere, nearly always");
        }

        /// <summary>The share of an ossuary's skull and bone piles within a metre past the clearance of the wall opposite the door in.</summary>
        static double Banked(LevelLayout l, LevelRoom r, out int piles)
        {
            LevelDoor entry = LevelGeometry.EntryDoor(l, r);
            int wayIn = entry != null ? LevelGeometry.WallOf(r.Rect, entry) : -1;
            int far = wayIn >= 0 ? LevelGenerator.Opposite(wayIn) : LevelGenerator.North;
            List<LevelProp> list = r.Props.Where(p => (p.Model == PropModel.SkullPile || p.Model == PropModel.BonePile) && p.Place != PropPlace.Wall).ToList();
            piles = list.Count;
            return list.Count == 0 ? 1 : list.Count(p => p.Footprint.DistanceToSide(r.Rect, l.CellSize, far) <= 1.0) / (double)list.Count;
        }

        // Pinned (part 4 of the order): the ossuary's piles banked against its far wall, where with the master off they stand
        // in two clusters away from the walls.
        [Test]
        public void TheOssuarysPiles_AreBankedAtItsFarWall_WithTheMasterOn()
        {
            double shareOn = 0, shareOff = 0;
            int rooms = 0, pilesOn = 0;
            for (ulong s = 0; s < 400; s++)
                for (int floor = 1; floor <= 5; floor += 2)
                {
                    LevelLayout a = LevelGenerator.Generate(s, floor), b = LevelGenerator.Generate(s, floor, Off());
                    for (int i = 0; i < a.Rooms.Count; i++)
                    {
                        if (a.Rooms[i].Theme != RoomTheme.Ossuary) continue;
                        rooms++;
                        shareOn += Banked(a, a.Rooms[i], out int n);
                        pilesOn += n;
                        shareOff += Banked(b, b.Rooms[i], out _);
                        Assert.That(LevelValidator.Validate(a), Is.Empty, "seed " + s);
                    }
                }
            shareOn /= rooms;
            shareOff /= rooms;
            TestContext.WriteLine(string.Format(s_Inv, "MOODSTATS ossuaries {0} (seeds 0-399, floors 1, 3, 5): {1} piles, {2:0.0%} of them banked at the far wall with the master on, {3:0.0%} off",
                                                rooms, pilesOn, shareOn, shareOff));
            Assert.That(shareOn, Is.GreaterThan(0.6), "banked at the far wall");
            Assert.That(shareOff, Is.LessThan(0.2), "in clusters off the walls with the master off");
        }

        // ---------------- The wear ----------------

        [Test]
        public void TheWear_NeverLiesOnADoorway_TheMiddle_AnEnemysOrHerPlace_OrAProp()
        {
            var values = new LevelValues();
            MoodValues mv = values.Mood;
            int pieces = 0, rooms = 0;
            var byKind = new Dictionary<WearKind, int>();
            for (ulong s = 0; s < 300; s++)
                foreach (int floor in new[] { 1, 5 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, values);
                    RoomMoodPlan plan = RoomMoodPlan.Make(l, values);
                    double cs = l.CellSize;
                    foreach (MoodRoom mr in plan.Rooms)
                    {
                        rooms++;
                        LevelRoom r = mr.Room;
                        double middle = Math.Min(mv.MiddleClear, 0.3 * Math.Min(r.Rect.Width, r.Rect.Height) * cs);
                        foreach (WearPiece w in mr.Wear)
                        {
                            pieces++;
                            byKind.TryGetValue(w.Kind, out int k);
                            byKind[w.Kind] = k + 1;
                            string what = "seed " + s + " floor " + floor + " " + r.Letter + r.Order + ": " + w.Kind + " at " + w.At;
                            double reach = w.Reach;
                            Assert.That(l.RoomAt(w.At.X, w.At.Y), Is.SameAs(r), "in its room: " + what);
                            Assert.That(LevelGeometry.DistanceToWalls(w.At, r.Rect, cs), Is.GreaterThanOrEqualTo(reach + 0.3 - 1e-9), "clear of the walls: " + what);
                            Assert.That(w.At.DistanceTo(r.Middle(cs)) - reach, Is.GreaterThanOrEqualTo(middle - 1e-9), "off the middle: " + what);
                            foreach (LevelDoor d in l.DoorsOf(r.Order))
                            {
                                d.Opening(cs, out LevelPoint a, out LevelPoint b);
                                Assert.That(LevelGeometry.DistanceToSegment(w.At, a, b) - reach, Is.GreaterThanOrEqualTo(mv.DoorClear - 1e-9), "off the doorway: " + what);
                            }
                            if (r.PlayerStart.HasValue) Assert.That(r.PlayerStart.Value.DistanceTo(w.At) - reach, Is.GreaterThanOrEqualTo(mv.PlaceClear - 1e-9), "off her start: " + what);
                            for (int i = 0; i < r.EnemyPoints.Count; i++)
                                foreach (LevelPoint p in r.PlacesAt(i).Concat(new[] { r.EnemyPoints[i] }))
                                    Assert.That(p.DistanceTo(w.At) - reach, Is.GreaterThanOrEqualTo(mv.PlaceClear - 1e-9), "off an enemy's place: " + what);
                            if (!w.Flat)
                                foreach (LevelProp p in r.Props)
                                    Assert.That(p.Footprint.DistanceTo(w.At) - reach, Is.GreaterThan(0), "off the " + p.Model + ": " + what);
                        }
                    }
                }
            TestContext.WriteLine("MOODSTATS wear (seeds 0-299, floors 1 and 5): " + pieces + " pieces in " + rooms + " rooms, "
                                  + string.Join(", ", byKind.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            Assert.That(pieces, Is.GreaterThan(rooms * 3), "a worn floor");
            foreach (WearKind k in Enum.GetValues(typeof(WearKind))) Assert.That(byKind.ContainsKey(k), k + " comes up");
        }

        [Test]
        public void WearOff_NoWear_AndTheSwitchesLeaveTheFloorAlone()
        {
            var values = new LevelValues();
            values.Mood.Wear = false;
            for (ulong s = 0; s < 20; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, 3, values);
                Assert.That(RoomMoodPlan.Make(l, values).WearCount, Is.EqualTo(0));
                Assert.That(l.Describe(), Is.EqualTo(LevelGenerator.Generate(s, 3).Describe()), "the wear's switch is never read by the generator");
            }
        }

        // ---------------- The eye-catchers ----------------

        static bool InDais(CompositionPiece dais, LevelPoint p)
        {
            double a = dais.Turn * Math.PI / 180.0, dx = p.X - dais.At.X, dy = p.Y - dais.At.Y;
            double x = dx * Math.Cos(a) - dy * Math.Sin(a), z = dx * Math.Sin(a) + dy * Math.Cos(a);
            return Math.Abs(x) <= dais.Width * 0.5 + 1e-6 && Math.Abs(z) <= dais.Depth * 0.5 + 1e-6;
        }

        [Test]
        public void EachTheme_HasItsEyeCatcher_AndAnEmptyPlaceWhereAStatueKeepsEveryRule()
        {
            // Changed deliberately (work order 052): work order 044's code-made pieces, as they are with the large props off;
            // LargePropsPlanTests has the large props in their place.
            var values = new LevelValues { LargeProps = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 103 (changed deliberately): the exit hall has no eye-catcher yet (its look is a later order); the throne room as before
            PropValues pv = values.PropRules;
            var rooms = new Dictionary<RoomTheme, int>();
            var places = new Dictionary<RoomTheme, int>();
            var whole = new Dictionary<RoomTheme, int>();
            void Count(Dictionary<RoomTheme, int> d, RoomTheme t)
            {
                d.TryGetValue(t, out int n);
                d[t] = n + 1;
            }
            for (ulong s = 0; s < 300; s++)
                foreach (int floor in new[] { 1, 3, 5 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, values);
                    RoomMoodPlan plan = RoomMoodPlan.Make(l, values);
                    foreach (MoodRoom mr in plan.Rooms)
                    {
                        LevelRoom r = mr.Room;
                        string where = "seed " + s + " floor " + floor + " " + r.Letter + r.Order + " " + RoomThemes.Word(r.Theme);
                        if (r.Kind == RoomKind.Entrance)
                        {
                            Assert.That(mr.Pieces, Is.Empty, where);
                            Assert.That(mr.LargePropPlace.HasValue, Is.False, where);
                            continue;
                        }
                        Count(rooms, r.Theme);
                        bool complete = false;
                        switch (r.Theme)
                        {
                            case RoomTheme.ThroneRoom:
                            {
                                LevelProp throne = r.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                                if (throne == null) break;
                                CompositionPiece dais = mr.Pieces.Single(p => p.Kind == CompositionKind.Dais);
                                Assert.That(throne.Footprint.Corners().All(c => InDais(dais, c)), "the throne on its dais: " + where);
                                // Work order 051: in a carved room the carpet may run in through the gate first and bend once or
                                // twice on its way to the dais, each run joined to the next.
                                List<CompositionPiece> runs = mr.Pieces.Where(p => p.Kind == CompositionKind.Carpet).ToList();
                                Assert.That(runs, Is.Not.Empty, "the carpet: " + where);
                                if (r.SolidCount == 0) Assert.That(runs.Count, Is.EqualTo(1), "one run in a room that is its rectangle: " + where);
                                for (int k = 1; k < runs.Count; k++) Assert.That(runs[k].At.DistanceTo(runs[k - 1].To), Is.LessThan(1e-6), "its runs joined: " + where);
                                CompositionPiece carpet = runs[0], last = runs[runs.Count - 1];
                                LevelDoor entry = LevelGeometry.EntryDoor(l, r);
                                entry.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                                Assert.That(carpet.At.DistanceTo(new LevelPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2)), Is.LessThan(0.6), "from the door in: " + where);
                                if (runs.Count == 1 || last.To.DistanceTo(carpet.At) > 4.5)
                                    Assert.That(InDais(dais, new LevelPoint((last.To.X * 0.98 + dais.At.X * 0.02), (last.To.Y * 0.98 + dais.At.Y * 0.02))), "to the dais: " + where);
                                complete = mr.Pieces.Count(p => p.Kind == CompositionKind.Candles) == 2;
                                break;
                            }
                            case RoomTheme.TombHall:
                                complete = mr.Pieces.Count(p => p.Kind == CompositionKind.Candles) >= r.Props.Count(p => p.Model == PropModel.Coffin || p.Model == PropModel.Sarcophagus);
                                break;
                            case RoomTheme.Vault:
                            {
                                LevelProp chest = r.Props.Single(p => p.Model == PropModel.Chest);
                                CompositionPiece dais = mr.Pieces.Single(p => p.Kind == CompositionKind.Dais);
                                Assert.That(chest.Footprint.Corners().All(c => InDais(dais, c)), "the chest on its dais: " + where);
                                Assert.That(mr.Pieces.Count(p => p.Kind == CompositionKind.CoinHeap), Is.EqualTo(4), where);
                                foreach (CompositionPiece heap in mr.Pieces.Where(p => p.Kind == CompositionKind.CoinHeap))
                                    Assert.That(InDais(dais, heap.At), "a heap on the dais: " + where);
                                Assert.That(mr.Sources.Any(x => x.Kind == MoodSourceKind.Gold), "the gold glow: " + where);
                                complete = true;
                                break;
                            }
                            case RoomTheme.Archive when r.Kind == RoomKind.Treasure:
                                Assert.That(mr.Pieces.Count(p => p.Kind == CompositionKind.Dais), Is.EqualTo(1), "the chest's dais: " + where);
                                complete = true;
                                break;
                            case RoomTheme.Shrine:
                            {
                                LevelProp basin = r.Props.FirstOrDefault(p => p.Model == PropModel.Basin);
                                if (basin != null)
                                {
                                    CompositionPiece dais = mr.Pieces.Single(p => p.Kind == CompositionKind.RoundDais);
                                    Assert.That(dais.At.DistanceTo(basin.Position), Is.LessThan(1e-9), where);
                                    Assert.That(dais.Width, Is.GreaterThan(basin.Radius), where);
                                }
                                complete = basin != null && mr.Pieces.Count(p => p.Kind == CompositionKind.Candles) >= 4;
                                break;
                            }
                            case RoomTheme.Ossuary:
                                complete = Banked(l, r, out _) >= 0.5;
                                break;
                            case RoomTheme.Archive:
                                complete = !r.Props.Any(p => p.Model == PropModel.Bookshelf) || mr.Pieces.Any(p => p.Kind == CompositionKind.Candles);
                                break;
                        }
                        if (complete) Count(whole, r.Theme);

                        // Every set of candles inside its room, on no prop (on a dais it stands on the dais).
                        foreach (CompositionPiece c in mr.Pieces.Where(p => p.Kind == CompositionKind.Candles))
                        {
                            Assert.That(LevelGeometry.DistanceToWalls(c.At, r.Rect, l.CellSize), Is.GreaterThanOrEqualTo(0.35 - 1e-9), "candles in the room: " + where);
                            if (c.Height <= 0)
                                foreach (LevelProp p in r.Props)
                                    Assert.That(p.Footprint.DistanceTo(c.At), Is.GreaterThan(0.2), "candles on no prop: " + where);
                        }

                        // The empty place: the game's statue would keep every rule of the props there.
                        if (!mr.LargePropPlace.HasValue) continue;
                        Count(places, r.Theme);
                        LevelProp statue = LevelProp.Make(PropModel.Statue, mr.LargePropPlace.Value, mr.LargePropTurn, PropPlace.Set);
                        Assert.That(PropRules.Fault(l, r, statue, r.Props, pv), Is.Null, "a statue keeps every rule at the empty place: " + where);
                        var lane = new PropRules.Lane(l, r, pv);
                        foreach (LevelProp p in r.Props) lane.Block(p.Footprint);
                        Assert.That(lane.Clear(statue.Footprint), "and the lane: " + where);
                        LevelPoint front = statue.Front, into = PropRules.Inward(mr.LargePropSide);
                        Assert.That(front.X * into.X + front.Y * into.Y, Is.GreaterThan(0.99), "facing into the room: " + where);
                    }
                }
            TestContext.WriteLine("MOODSTATS eye-catchers (seeds 0-299, floors 1, 3, 5): " + string.Join("; ", rooms.OrderBy(k => k.Key).Select(k =>
                RoomThemes.Word(k.Key) + " " + k.Value + " rooms, whole in " + (whole.TryGetValue(k.Key, out int w) ? w : 0) + ", an empty place in " + (places.TryGetValue(k.Key, out int p) ? p : 0))));
            foreach (KeyValuePair<RoomTheme, int> t in rooms)
            {
                Assert.That(whole.TryGetValue(t.Key, out int w) ? w : 0, Is.GreaterThanOrEqualTo(t.Value * 3 / 4), RoomThemes.Word(t.Key) + ": its eye-catcher whole in most rooms");
                Assert.That(places.TryGetValue(t.Key, out int p) ? p : 0, Is.GreaterThanOrEqualTo(t.Value * 3 / 4), RoomThemes.Word(t.Key) + ": an empty place for a large prop in most rooms");
            }
        }

        // ---------------- Quick, the same, its own ----------------

        static double Median(List<double> xs)
        {
            xs.Sort();
            return xs.Count == 0 ? 0 : xs[xs.Count / 2];
        }

        [Test]
        public void Generation_IsAsQuickAsItWas_ByTheMedian()
        {
            var on = new LevelValues();
            LevelGenerator.Generate(424242UL, 3, on);
            LevelGenerator.Generate(424242UL, 3, Off());
            var tOn = new List<double>();
            var tOff = new List<double>();
            var tPlan = new List<double>();
            var timer = new Stopwatch();
            for (ulong s = 0; s < 300; s++)
            {
                timer.Restart();
                LevelLayout l = LevelGenerator.Generate(s, 3, on);
                tOn.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                LevelGenerator.Generate(s, 3, Off());
                tOff.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                RoomMoodPlan.Make(l, on);
                tPlan.Add(timer.Elapsed.TotalMilliseconds);
            }
            double mOn = Median(tOn), mOff = Median(tOff), mPlan = Median(tPlan);
            TestContext.WriteLine(string.Format(s_Inv, "MOODSTATS generation, floor 3, seeds 0-299: median {0:0.000} ms with the master on, {1:0.000} ms off; the mood's plan (made when a floor is built) median {2:0.000} ms, slowest {3:0.000} ms",
                                                mOn, mOff, mPlan, tPlan.Max()));
            Assert.That(mOn, Is.LessThan(mOff * 1.25 + 0.2), "generation as quick as it was, by the median");
            Assert.That(mPlan, Is.LessThan(20), "the plan is quick");
        }

        [Test]
        public void ThePlan_IsTheSameForTheSameSeed_AndItsStreamIsItsOwn()
        {
            ulong first = LevelRandom.Stream(9UL, RoomMoodPlan.Stage).NextBits();
            foreach (string other in new[] { LevelGenerator.LayoutStage, LevelGenerator.ContentsStage, LevelGenerator.PropsStage, LevelGenerator.ThemesStage, CryptKitPlan.Stage })
                Assert.That(LevelRandom.Stream(9UL, other).NextBits(), Is.Not.EqualTo(first), other);
            var values = new LevelValues();
            string one = RoomMoodPlan.Make(LevelGenerator.Generate(42UL, 2, values), values).Describe();
            Assert.That(RoomMoodPlan.Make(LevelGenerator.Generate(42UL, 2, values), values).Describe(), Is.EqualTo(one));
            Assert.That(RoomMoodPlan.Make(LevelGenerator.Generate(43UL, 2, values), values).Describe(), Is.Not.EqualTo(one));
        }

        [Test]
        public void TheSources_AreTheGlowingProps_AndTheCandles_BraziersFirst()
        {
            // Changed deliberately (work order 052): the sources of work order 044's props and candles, with the large props off;
            // LargePropsPlanTests has the large props' sources.
            var values = new LevelValues { LargeProps = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 103 (changed deliberately): the exit hall has no eye-catcher yet (its look is a later order); the throne room as before
            for (ulong s = 0; s < 50; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, 1, values);
                foreach (MoodRoom mr in RoomMoodPlan.Make(l, values).Rooms)
                {
                    int braziers = mr.Room.Props.Count(p => p.Model == PropModel.Brazier || p.Model == PropModel.Basin);
                    Assert.That(mr.Sources.Count(x => x.Kind == MoodSourceKind.Brazier), Is.EqualTo(braziers));
                    Assert.That(mr.Sources.Count(x => x.Kind == MoodSourceKind.Candle && x.OfProp), Is.EqualTo(mr.Room.Props.Count(p => p.Model == PropModel.Candelabra)));
                    Assert.That(mr.Sources.Count(x => x.Kind == MoodSourceKind.Candle && !x.OfProp), Is.EqualTo(mr.Pieces.Count(p => p.Kind == CompositionKind.Candles)));
                    for (int i = 1; i < Math.Min(2, mr.Sources.Count); i++)
                        Assert.That(mr.Sources[i - 1].Kind != MoodSourceKind.Candle || mr.Sources[i].Kind == MoodSourceKind.Candle, "braziers first, for the real lights");
                }
            }
        }

        [Test]
        public void TheRoomTone_IsSevenSecondsThatLoop_QuietAndClean()
        {
            float[] s = Synth.Render(RoomMoodPlan.RoomTone());
            Assert.That(s.Length, Is.EqualTo(7 * Synth.SampleRate));
            Assert.That(s.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
            Assert.That(s.Max(Math.Abs), Is.LessThanOrEqualTo(0.5f + 1e-4f), "peaks at half of full scale");
            Assert.That(s[0], Is.EqualTo(0f));
            Assert.That(s[s.Length - 1], Is.EqualTo(0f), "ends where it starts: the loop does not click");
            Assert.That(new MoodValues().ToneVolume, Is.LessThanOrEqualTo(0.1f), "very quiet");
        }
    }
}
