using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Two waves (work order 136; Kristian's ticks of 2026-11-25: waves "Ja, i de fleste rom", the next wave "Når halve første er død"):
    /// which rooms bring waves (from their place on the floor), who is in which wave, when the next starts, where an arrival may stand,
    /// the switch and its Trials line, and that the generator is untouched: with the waves on or off every floor is the same, field for
    /// field. The numbers of the rooms' first rush for the report are in <see cref="FirstRushOnThreeHundredFloors"/>.
    /// </summary>
    public class RoomWavesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly RoomWaveValues W = new RoomWaveValues();

        static List<LevelRoom> Combats(LevelLayout l) => l.Rooms.Where(r => r.Kind == RoomKind.Combat).OrderBy(r => r.Order).ToList();

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOn_WithTheOrdersNumbers()
        {
            var v = new LevelValues();
            Assert.That(v.Waves, Is.True, "his ticks: waves in most rooms");
            Assert.That(v.WaveRules, Is.Not.Null);
            Assert.That(W.Waves, Is.EqualTo(2));
            Assert.That(W.LastRoomWaves, Is.EqualTo(3));
            Assert.That(W.TriggerShare, Is.EqualTo(0.5f), "his tick: when half of the first is dead");
            Assert.That(W.Nearest, Is.EqualTo(7f));
            Assert.That(W.Farthest, Is.EqualTo(11f));
            Assert.That(W.NeverNearer, Is.EqualTo(3f));
            Assert.That(W.DiscSeconds, Is.EqualTo(1f), "the order's 1.0 s");
            Assert.That(W.DiscRadius, Is.EqualTo(1f));
            Assert.That(W.MostAtOnce, Is.EqualTo(5), "the producer's ruling: a large wave in groups of five");
        }

        /// <summary>The waves are the fight's, not the generator's: on or off, every floor is the same, field for field, its map too.</summary>
        [Test]
        public void OnOrOff_EveryFloorIsTheSame_FieldForField()
        {
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout on = LevelGenerator.Generate(seed * 7919UL, floor, new LevelValues());
                    LevelLayout off = LevelGenerator.Generate(seed * 7919UL, floor, new LevelValues { Waves = false });
                    Assert.That(FloorPrint.Of(off, 14695981039346656037UL), Is.EqualTo(FloorPrint.Of(on, 14695981039346656037UL)), "seed " + seed + " floor " + floor);
                    Assert.That(LevelMap.Draw(off), Is.EqualTo(LevelMap.Draw(on)));
                    Assert.That(on.Version, Is.EqualTo(off.Version));
                }
        }

        // ---------------- Which rooms ----------------

        [Test]
        public void WhichRooms_ByTheirPlaceOnTheFloor()
        {
            int lastThree = 0, fulls = 0, twos = 0;
            for (ulong seed = 1; seed <= 60; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed * 104729UL, floor, new LevelValues());
                    List<LevelRoom> combats = Combats(l);
                    Assert.That(combats.Count, Is.GreaterThan(2));
                    foreach (LevelRoom r in l.Rooms)
                    {
                        int n = RoomWaveRules.WavesOf(l, r, W);
                        string where = "seed " + seed + " floor " + floor + " room " + r.Letter + r.Order + " (" + (r.LineUp?.Name ?? "no line-up") + ")";
                        if (r.Kind != RoomKind.Combat || r.Elite || r.LineUp == null) Assert.That(n, Is.EqualTo(1), where + ": as before");
                        else if (r.LineUp.Full) { Assert.That(n, Is.EqualTo(2), where + ": the full room, two"); fulls++; }
                        else if (floor == 1 && r == combats[0]) Assert.That(n, Is.EqualTo(1), where + ": floor 1's first combat room, one");
                        else if (r == combats[combats.Count - 1]) { Assert.That(n, Is.EqualTo(3), where + ": the last before the exit, three"); lastThree++; }
                        else { Assert.That(n, Is.EqualTo(2), where); twos++; }
                    }
                    Assert.That(RoomWaveRules.WavesOf(l, l.ExitRoom, W), Is.EqualTo(1), "the exit room as before");
                }
            Assert.That(lastThree, Is.GreaterThan(100));
            Assert.That(fulls, Is.GreaterThan(100));
            Assert.That(twos, Is.GreaterThan(300));
        }

        // ---------------- Who comes in which wave ----------------

        static int[] Split(LineUp u, int waves, params EnemyKind[] kinds) => RoomWaveRules.Split(kinds, kinds.Select(_ => false).ToList(), u, waves);

        [Test]
        public void TwoWaves_TheLeadFirst_ThePartnerSecond()
        {
            var pack = new LineUp("pack", EnemyKind.Swarmer, 6, 12, EnemyKind.Brute, 1, 2);
            int[] w = Split(pack, 2, EnemyKind.Swarmer, EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Swarmer, EnemyKind.Brute);
            Assert.That(w, Is.EqualTo(new[] { 1, 1, 2, 1, 2 }));
            Assert.That(RoomWaveRules.Count(w), Is.EqualTo(2));
            Assert.That(Split(pack, 1, EnemyKind.Swarmer, EnemyKind.Brute), Is.EqualTo(new[] { 1, 1 }), "one wave: all at once");
        }

        [Test]
        public void ThreeWaves_TheLeadInTwoHalves_TheLargerFirst_ThePartnerBetween()
        {
            var gallery = new LineUp("gallery", EnemyKind.Shooter, 2, 3, EnemyKind.Swarmer, 4, 10);
            int[] w = Split(gallery, 3, EnemyKind.Shooter, EnemyKind.Shooter, EnemyKind.Shooter, EnemyKind.Swarmer, EnemyKind.Swarmer);
            Assert.That(w, Is.EqualTo(new[] { 1, 1, 3, 2, 2 }), "three shooters: two first, one last; the swarmers between");
            var guard = new LineUp("guard and pack", EnemyKind.Guard, 1, 1, EnemyKind.Swarmer, 8, 14);
            int[] g = Split(guard, 3, EnemyKind.Guard, EnemyKind.Swarmer, EnemyKind.Swarmer);
            Assert.That(g, Is.EqualTo(new[] { 1, 2, 2 }), "a lead of one: its second half is empty, and the waves are two");
            Assert.That(RoomWaveRules.Count(g), Is.EqualTo(2));
        }

        [Test]
        public void TheFullRoom_ItsTwoLargestKindsFirst_TheRestSecond()
        {
            LineUp full = LineUp.FullRoom("full");
            int[] w = Split(full, 2, EnemyKind.Shooter, EnemyKind.Swarmer, EnemyKind.Swarmer, EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Brute, EnemyKind.Caster);
            Assert.That(w, Is.EqualTo(new[] { 2, 1, 1, 1, 1, 1, 2 }), "swarmers 3 and brutes 2 first; the shooter and the caster second");
            int[] two = Split(full, 2, EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Swarmer);
            Assert.That(two, Is.EqualTo(new[] { 1, 2, 1 }), "only two kinds: the fewer second");
        }

        [Test]
        public void TheElite_StaysInTheFirstWave()
        {
            var pack = new LineUp("pack", EnemyKind.Swarmer, 6, 12, EnemyKind.Brute, 1, 2);
            int[] w = RoomWaveRules.Split(new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Brute }, new[] { false, false, true }, pack, 2);
            Assert.That(w, Is.EqualTo(new[] { 1, 2, 1 }));
        }

        /// <summary>On two hundred floors a floor: the waves divide each room's enemies (none added, none lost), and every later wave holds only the room's partner or the lead's second half.</summary>
        [Test]
        public void OnGeneratedFloors_TheWavesDivideTheRoom_NoneAdded()
        {
            for (ulong seed = 1; seed <= 200; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed * 15485863UL, floor, new LevelValues());
                    foreach (LevelRoom r in Combats(l))
                    {
                        List<(EnemyKind kind, bool elite)> members = Members(r);
                        int waves = RoomWaveRules.WavesOf(l, r, W);
                        int[] split = RoomWaveRules.Split(members.Select(m => m.kind).ToList(), members.Select(m => m.elite).ToList(), r.LineUp, waves);
                        Assert.That(split.Length, Is.EqualTo(r.EnemyCount), "every enemy in a wave");
                        Assert.That(RoomWaveRules.Count(split), Is.LessThanOrEqualTo(waves));
                        if (r.LineUp == null || r.LineUp.Full || waves <= 1) continue;
                        for (int i = 0; i < split.Length; i++)
                        {
                            if (members[i].elite) continue;
                            if (split[i] == 1) Assert.That(members[i].kind, Is.EqualTo(r.LineUp.Lead), "wave 1 is the lead's");
                            if (split[i] == 2) Assert.That(members[i].kind, Is.EqualTo(r.LineUp.Partner), "wave 2 is the partner's");
                            if (split[i] == 3) Assert.That(members[i].kind, Is.EqualTo(r.LineUp.Lead), "wave 3 is the lead's second half");
                        }
                    }
                }
        }

        static List<(EnemyKind, bool)> Members(LevelRoom r)
        {
            var list = new List<(EnemyKind, bool)>();
            for (int i = 0; i < r.EnemyPoints.Count; i++)
            {
                EnemyGroup g = r.GroupAt(i);
                for (int m = 0; m < r.PlacesAt(i).Length; m++) list.Add((g.Kind, g.Elite));
            }
            return list;
        }

        /// <summary>The first rush and the waves' sizes on three hundred floors a floor, for the report (the order: "the first rush is smaller now: say by how much").</summary>
        [Test]
        public void FirstRushOnThreeHundredFloors()
        {
            for (int floor = 1; floor <= 5; floor++)
            {
                int rooms = 0, waved = 0, three = 0, all = 0, first = 0, mostFirst = 0, mostRoom = 0, lone = 0;
                var firstBy = new Dictionary<EnemyKind, int>();
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed * 32452843UL + 17UL, floor, new LevelValues());
                    foreach (LevelRoom r in Combats(l))
                    {
                        List<(EnemyKind kind, bool elite)> members = Members(r);
                        int waves = RoomWaveRules.WavesOf(l, r, W);
                        int[] split = RoomWaveRules.Split(members.Select(m => m.kind).ToList(), members.Select(m => m.elite).ToList(), r.LineUp, waves);
                        int n = RoomWaveRules.Count(split);
                        rooms++;
                        if (n > 1) waved++;
                        if (n > 2) three++;
                        int w1 = split.Count(x => x == 1);
                        all += split.Length;
                        first += w1;
                        mostFirst = Math.Max(mostFirst, w1);
                        mostRoom = Math.Max(mostRoom, split.Length);
                        if (n > 1 && w1 == 1) lone++;
                        if (n > 1 && r.LineUp != null && !r.LineUp.Full) firstBy[r.LineUp.Lead] = Math.Max(firstBy.TryGetValue(r.LineUp.Lead, out int b) ? b : 0, w1);
                    }
                }
                string line = string.Format(Inv,
                    "WAVES floor {0}: {1} combat rooms, {2} with waves ({3:0.0} %), {4} of three; enemies {5}, in the first rush {6} ({7:0.0} % of them, {8:0.0} % fewer); the most in one room {9}, in one first rush {10}; a first rush of one enemy in {11} rooms; the most in a first rush by lead: {12}",
                    floor, rooms, waved, 100.0 * waved / rooms, three, all, first, 100.0 * first / all, 100.0 - 100.0 * first / all, mostRoom, mostFirst, lone,
                    string.Join(", ", firstBy.OrderBy(k => k.Key).Select(k => EnemyKinds.Word(k.Key) + " " + k.Value)));
                Console.WriteLine(line);
                TestContext.WriteLine(line);
                Assert.That(waved, Is.GreaterThan(rooms / 2), "waves in most rooms");
            }
        }

        // ---------------- When the next wave starts ----------------

        [Test]
        public void TheNextWave_WhenHalfOfTheOneBeforeIsDead_RoundedUp_NeverWhileOneArrives()
        {
            Assert.That(RoomWaveRules.DeadFor(1, W), Is.EqualTo(1));
            Assert.That(RoomWaveRules.DeadFor(2, W), Is.EqualTo(1));
            Assert.That(RoomWaveRules.DeadFor(3, W), Is.EqualTo(2));
            Assert.That(RoomWaveRules.DeadFor(5, W), Is.EqualTo(3), "half of five, rounded up");
            Assert.That(RoomWaveRules.DeadFor(6, W), Is.EqualTo(3));
            Assert.That(RoomWaveRules.DeadFor(14, W), Is.EqualTo(7));
            Assert.That(RoomWaveRules.NextDue(5, 2, false, W), Is.False, "two of five dead: not yet");
            Assert.That(RoomWaveRules.NextDue(5, 3, false, W), Is.True, "three of five: now");
            Assert.That(RoomWaveRules.NextDue(5, 5, true, W), Is.False, "never while a wave is still arriving");
        }

        // ---------------- Where an arrival stands ----------------

        static GroundQuad Screen(double herX, double herZ) => ScreenGround.Design(herX, herZ, 20, 55, 40, 16.0 / 9.0);

        [Test]
        public void AnArrival_SevenToElevenMetres_InView_NeverBelowHer_ApartFromTheOthers()
        {
            var her = new LevelPoint(50, 50);
            GroundQuad view = Screen(50, 50);
            var none = new List<LevelPoint>();
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 59), her, view, 0.5, 0, 1, none, W), Is.Null, "9 m straight up the screen");
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 55), her, view, 0.5, 0, 1, none, W), Does.Contain("nearer her than 7"));
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 52), her, view, 0.5, 0, 1, none, W), Does.Contain("nearer her than 3"));
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 62), her, view, 0.5, 0, 1, none, W), Does.Contain("farther"));
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 41), her, view, 0.5, 0, 1, none, W), Does.Contain("below her"));
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(59, 49.9), her, view, 0.5, 0, 1, none, W), Does.Contain("below her"), "a hair below her row");
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(59, 50), her, view, 0.5, 0, 1, none, W), Is.Null, "on her row, to her side");
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 59), her, view, 0.5, 0, 1, new List<LevelPoint> { new LevelPoint(51, 59) }, W), Does.Contain("another enemy"));
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 59), her, view, 0.5, 0, 1, new List<LevelPoint> { new LevelPoint(52.5, 59) }, W), Is.Null);
            // Off his screen: a camera turned away shows another part of the ground.
            GroundQuad away = Screen(50, 90);
            Assert.That(RoomWaveRules.ArrivalFault(new LevelPoint(50, 59), her, away, 0.5, 0, 1, none, W), Does.Contain("not on his screen"));
        }

        /// <summary>The order's "never BELOW her on his screen (the screen shows only about 7 m below her)": the game's camera shows about 7 m below her and about 11 m above.</summary>
        [Test]
        public void TheCameraShowsLittleBelowHer_SoNothingArrivesThere()
        {
            GroundQuad view = Screen(0, 0);
            double below = 0, above = 0;
            for (double d = 0; d < 40; d += 0.1) if (ScreenGround.Shows(view, 0, -d, 0.5)) below = d;
            for (double d = 0; d < 40; d += 0.1) if (ScreenGround.Shows(view, 0, d, 0.5)) above = d;
            TestContext.WriteLine(string.Format(Inv, "a design camera shows {0:0.0} m below her and {1:0.0} m above inside the margin", below, above));
            Assert.That(below, Is.LessThan(W.Nearest + 1), "below her the screen ends near the band's near edge");
            Assert.That(above, Is.GreaterThan(W.Nearest), "above her the band is on screen");
        }

        [Test]
        public void Pick_KeepsOnlyGoodPoints_AsManyAsWanted_TheSameEachTime()
        {
            var her = new LevelPoint(50, 50);
            GroundQuad view = Screen(50, 50);
            var points = new List<LevelPoint>();
            for (double y = 30; y <= 70; y += 1) for (double x = 30; x <= 70; x += 1) points.Add(new LevelPoint(x, y));
            List<int> a = RoomWaveRules.Pick(points, 6, her, view, 0.5, 0, 1, new List<LevelPoint>(), W, LevelRandom.Stream(9, RoomWaveRules.Stage, 3), null);
            List<int> b = RoomWaveRules.Pick(points, 6, her, view, 0.5, 0, 1, new List<LevelPoint>(), W, LevelRandom.Stream(9, RoomWaveRules.Stage, 3), null);
            Assert.That(a, Is.EqualTo(b), "the same draw each time");
            Assert.That(a.Count, Is.EqualTo(6));
            for (int i = 0; i < a.Count; i++)
            {
                LevelPoint p = points[a[i]];
                Assert.That(p.DistanceTo(her), Is.InRange(7.0, 11.0));
                Assert.That(p.Y, Is.GreaterThanOrEqualTo(50.0), "never below her");
                for (int j = 0; j < i; j++) Assert.That(points[a[j]].DistanceTo(p), Is.GreaterThanOrEqualTo(2.0));
            }
            List<int> refused = RoomWaveRules.Pick(points, 6, her, view, 0.5, 0, 1, new List<LevelPoint>(), W, LevelRandom.Stream(9, RoomWaveRules.Stage, 3), i => false);
            Assert.That(refused, Is.Empty, "a point the floor refuses (off the NavMesh, no path) is never kept");
            // She at a room's top wall: nothing above her within the band, everyone waits.
            var low = points.Where(p => p.Y <= 50).ToList();
            Assert.That(RoomWaveRules.Pick(low, 6, her, view, 0.5, 0, 1, new List<LevelPoint>(), W, LevelRandom.Stream(9, RoomWaveRules.Stage, 3), null).Count, Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void TheRoomsPoints_AreOnItsOpenFloor_ClearOfItsWallsAndDoors()
        {
            var v = new LevelValues();
            LevelLayout l = LevelGenerator.Generate(0x136UL, 2, v);
            foreach (LevelRoom r in Combats(l))
            {
                List<LevelPoint> points = RoomWaveRules.RoomPoints(l, r, v, W.PointStep);
                Assert.That(points.Count, Is.GreaterThan(50), r.Letter + r.Order.ToString(Inv));
                var doors = new List<LevelDoor>(l.DoorsOf(r.Order));
                foreach (LevelPoint p in points)
                {
                    Assert.That(l.RoomAt(p.X, p.Y), Is.SameAs(r));
                    Assert.That(LevelGeometry.EnemyPlaceFault(l, r, p, null, doors, v), Is.Null);
                }
            }
        }

        [Test]
        public void TheKindsAsTheRecorderWritesThem()
        {
            Assert.That(RoomWaveRules.KindsWords(new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Swarmer }), Is.EqualTo("swarmer 2, brute 1"));
            Assert.That(RoomWaveRules.KindsWords(new EnemyKind[0]), Is.EqualTo(""));
        }

        // ---------------- The Trials page ----------------

        [Test]
        public void TheTrialsPage_EndsWithTheWavesLine_AfterTheLineUps()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines[lines.Count - 1], Is.EqualTo(SettingLine.Waves));
            Assert.That(lines[lines.Count - 2], Is.EqualTo(SettingLine.LineUps));
            Assert.That(SettingsPages.PageOf(SettingLine.Waves), Is.EqualTo(SettingsPage.Trials));
            Assert.That(RoomTrialLines.Is(SettingLine.Waves), Is.True);
            Assert.That(RoomTrialLines.Name(SettingLine.Waves), Is.EqualTo("Waves"));
            Assert.That(RoomTrialLines.Name(SettingLine.LineUps), Is.EqualTo("Line-ups"));
        }
    }
}
