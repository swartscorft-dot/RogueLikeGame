using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The timed chest (work order 153; Kristian's tick of 2026-11-25, "Tidskisten": "open it to fight, and the reward shrinks while you
    /// fight"): its numbers and switch, which room of a floor holds it (from floor 2, one a floor, drawn from the floor's seed, never the
    /// first combat room, the elite, exit or throne room, nor a caller's), where it stands (a corner of the open floor, apart from the doors,
    /// the props and the room's enemies, in view from the room's middle), its wave's counts, its reward's decay (whole until 9 s, 1 % a
    /// second, never under 10 %, frozen at the last kill), its ROOMS line, the summary's block, and that the generator is untouched: on or
    /// off, every floor is the same, field for field. The numbers for the report are in <see cref="TheNumbers_ByFloor"/>.
    /// </summary>
    public class TimedChestTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly TimedChestValues C = new TimedChestValues();

        static LevelLayout Floor(ulong seed, int floor, LevelValues v = null) => LevelGenerator.Generate(seed * 7919UL, floor, v ?? new LevelValues());

        /// <summary>The rooms the callers' rule gives a caller on this floor (callers on, as by default).</summary>
        static List<LevelRoom> Callers(LevelLayout l, LevelValues v) => RoomCallerRules.Rooms(l, v);

        [TearDown]
        public void SwitchBack() => LevelValues.TimedChests = true;

        // ---------------- The switch and the numbers ----------------

        [Test]
        public void TheSwitch_IsOn_WithTheOrdersNumbers()
        {
            Assert.That(LevelValues.TimedChests, Is.True, "his tick: \"Tidskisten\"");
            var v = new LevelValues();
            Assert.That(v.TimedChestRules, Is.Not.Null);
            Assert.That(TimedChestRules.On(v), Is.True);
            Assert.That(C.FromFloor, Is.EqualTo(2), "from floor 2");
            Assert.That(C.DecayAfter, Is.EqualTo(9f), "begins to fall 9 s after the opening");
            Assert.That(C.DecayShare, Is.EqualTo(0.01f), "1 % of the whole");
            Assert.That(C.DecayEvery, Is.EqualTo(1f), "a second");
            Assert.That(C.Least, Is.EqualTo(0.1f), "never under 10 %");
            Assert.That(C.WaveShare.Skip(2), Is.All.EqualTo(0.6f), "about 60 % of the room's own fight");
            Assert.That(C.OpenWords, Is.EqualTo("Open: a fight, the reward shrinks"), "the order's prompt");
            Assert.That(TimedChestRules.Whole(1, C), Is.EqualTo(0), "none on floor 1");
            for (int floor = 2; floor <= 5; floor++) Assert.That(TimedChestRules.Whole(floor, C), Is.GreaterThan(0), "floor " + floor);
            Assert.That(TimedChestRules.Whole(9, C), Is.EqualTo(C.Echoes[C.Echoes.Length - 1]), "a floor past the table reads its last");
            LevelValues.TimedChests = false;
            Assert.That(TimedChestRules.On(v), Is.False);
            Assert.That(TimedChestRules.On(new LevelValues { TimedChestRules = null }), Is.False);
        }

        /// <summary>The chest is the fight's, not the generator's: on or off, every floor is the same, field for field, its map too.</summary>
        [Test]
        public void OnOrOff_EveryFloorIsTheSame_FieldForField()
        {
            for (ulong seed = 1; seed <= 30; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelValues.TimedChests = true;
                    LevelLayout on = Floor(seed, floor);
                    LevelValues.TimedChests = false;
                    LevelLayout off = Floor(seed, floor);
                    Assert.That(FloorPrint.Of(off, 14695981039346656037UL), Is.EqualTo(FloorPrint.Of(on, 14695981039346656037UL)), "seed " + seed + " floor " + floor);
                    Assert.That(LevelMap.Draw(off), Is.EqualTo(LevelMap.Draw(on)));
                    Assert.That(on.Version, Is.EqualTo(off.Version));
                    Assert.That(TimedChestRules.Room(off, new LevelValues(), Callers(off, new LevelValues())), Is.Null, "off: no chest");
                }
        }

        // ---------------- Which room ----------------

        [Test]
        public void WhichRoom_FromFloor2_OneAFloor_DrawnFromTheSeed_NeverAForbiddenRoom()
        {
            var v = new LevelValues();
            for (ulong seed = 1; seed <= 60; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = Floor(seed, floor);
                    List<LevelRoom> callers = Callers(l, v);
                    LevelRoom r = TimedChestRules.Room(l, v, callers);
                    string where = "seed " + seed + " floor " + floor;
                    if (floor == 1)
                    {
                        Assert.That(r, Is.Null, where + ": none on floor 1");
                        continue;
                    }
                    if (r == null) continue; // counted in TheNumbers_ByFloor: every floor from 2 should hold one
                    string room = where + " room " + r.Letter + r.Order;
                    Assert.That(r.Kind, Is.EqualTo(RoomKind.Combat), room);
                    Assert.That(r.Elite, Is.False, room + ": never the elite room");
                    Assert.That(r.EnemyGroups.Any(g => g.Boss), Is.False, room + ": never the throne room");
                    Assert.That(r, Is.Not.SameAs(l.ExitRoom), room + ": never the exit room");
                    Assert.That(RoomCallerRules.CombatRooms(l).IndexOf(r), Is.GreaterThan(0), room + ": never the floor's first combat room");
                    Assert.That(callers, Has.No.Member(r), room + ": never a caller's room");
                    Assert.That(r.LineUp, Is.Not.Null, room);
                    if (r.LineUp.Full)
                        Assert.That(RoomCallerRules.CombatRooms(l).All(x => TimedChestRules.RoomFault(l, x, C, callers) != null || TimedChestRules.Places(l, x, v).Count == 0),
                                    Is.True, room + ": the full room only where no room of two kinds may");
                    Assert.That(TimedChestRules.RoomFault(l, r, C, callers, fullMay: r.LineUp.Full), Is.Null, room);
                    Assert.That(TimedChestRules.Places(l, r, v), Is.Not.Empty, room + ": a place");
                    Assert.That(TimedChestRules.Room(l, v, callers), Is.SameAs(r), where + ": the same each time (the floor's seed)");
                }
        }

        [Test]
        public void WithTheCallersOff_ACallersRoomMayHoldIt_AndSeedsDiffer()
        {
            var v = new LevelValues();
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                LevelLayout l = Floor(seed, 3);
                LevelRoom r = TimedChestRules.Room(l, v, null);
                if (r != null) seen.Add(RoomCallerRules.CombatRooms(l).IndexOf(r));
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "the seed draws among the rooms that may: not always the same place on the row");
        }

        [Test]
        public void RoomFault_SaysWhy()
        {
            LevelLayout l = Floor(3, 3);
            List<LevelRoom> combat = RoomCallerRules.CombatRooms(l);
            Assert.That(TimedChestRules.RoomFault(l, combat[0], C, null), Is.EqualTo("is the floor's first combat room"));
            Assert.That(TimedChestRules.RoomFault(l, l.ExitRoom, C, null), Is.EqualTo("is not a combat room"));
            LevelLayout one = Floor(3, 1);
            Assert.That(TimedChestRules.RoomFault(one, RoomCallerRules.CombatRooms(one)[1], C, null), Does.StartWith("is on floor 1"));
            LevelRoom elite = l.Rooms.FirstOrDefault(r => r.Elite);
            if (elite != null) Assert.That(TimedChestRules.RoomFault(l, elite, C, null), Is.EqualTo("is the elite room"));
            LevelRoom some = combat.Skip(1).FirstOrDefault(r => !r.Elite && r.LineUp != null && !r.LineUp.Full);
            Assert.That(some, Is.Not.Null);
            Assert.That(TimedChestRules.RoomFault(l, some, C, new List<LevelRoom> { some }), Is.EqualTo("holds a caller"));
        }

        // ---------------- Where it stands ----------------

        [Test]
        public void ItsPlaces_AtACorner_ApartFromDoorsPropsAndEnemies_InViewFromTheMiddle()
        {
            var v = new LevelValues();
            RangedAroundValues r = v.RangedAround;
            PropSize size = PropSize.Of(PropModel.Chest);
            int rooms = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 2; floor <= 5; floor++)
                {
                    LevelLayout l = Floor(seed, floor);
                    LevelRoom room = TimedChestRules.Room(l, v, Callers(l, v));
                    if (room == null) continue;
                    rooms++;
                    double cs = l.CellSize;
                    List<LevelPoint> places = TimedChestRules.PlacesInOrder(l, room, v);
                    Assert.That(places, Is.EquivalentTo(TimedChestRules.Places(l, room, v)), "the same places, drawn");
                    Assert.That(places.Count, Is.InRange(1, 4), "one a corner at most");
                    var corners = new[]
                    {
                        new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs), new LevelPoint(room.Rect.Right * cs, room.Rect.Y * cs),
                        new LevelPoint(room.Rect.Right * cs, room.Rect.Top * cs), new LevelPoint(room.Rect.X * cs, room.Rect.Top * cs),
                    };
                    LevelPoint middle = room.Middle(cs);
                    foreach (LevelPoint p in places)
                    {
                        string at = "seed " + seed + " floor " + floor + " room " + room.Order + " at " + p.X.ToString("0.0", Inv) + ", " + p.Y.ToString("0.0", Inv);
                        Assert.That(corners.Min(k => k.DistanceTo(p)), Is.LessThanOrEqualTo(C.CornerMost + 1e-9), at + ": at a corner");
                        foreach (LevelDoor d in l.DoorsOf(room.Order))
                            Assert.That(LevelGeometry.DistanceToDoor(p, d, cs), Is.GreaterThanOrEqualTo(C.FromDoors - 1e-9), at + ": from the doors");
                        PropFootprint chest = PropFootprint.Of(size, p, 0);
                        foreach (LevelProp prop in room.Props)
                            Assert.That(prop.Footprint.DistanceTo(chest), Is.GreaterThanOrEqualTo(C.FromProps - 1e-9), at + ": from the props");
                        for (int i = 0; i < room.EnemyPoints.Count; i++)
                            foreach (LevelPoint e in room.PlacesAt(i))
                                Assert.That(e.DistanceTo(p), Is.GreaterThanOrEqualTo(C.FromEnemies - 1e-9), at + ": from the room's enemies");
                        Assert.That(LevelGeometry.DistanceToWalls(p, room.Rect, cs), Is.GreaterThanOrEqualTo(C.FromWalls - 1e-9), at + ": from the walls");
                        Assert.That(LevelRanged.InDesignView(middle, p, r, C.ViewMargin), Is.True, at + ": in view from the room's middle");
                        Assert.That(p.DistanceTo(middle), Is.GreaterThanOrEqualTo(C.MiddleLeast - 1e-9), at + ": not the middle");
                    }
                }
            Assert.That(rooms, Is.GreaterThan(100), "chests were placed");
        }

        // ---------------- Its wave ----------------

        [Test]
        public void ItsWave_SixTenthsOfEachOfTheRoomsTwoKinds_AtLeastOneEach()
        {
            var v = new LevelValues();
            int rooms = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 2; floor <= 5; floor++)
                {
                    LevelLayout l = Floor(seed, floor);
                    LevelRoom room = TimedChestRules.Room(l, v, Callers(l, v));
                    if (room == null) continue;
                    rooms++;
                    TimedChestRules.WaveCounts(l, room, C, out int lead, out int partner);
                    Assert.That(TimedChestRules.WaveKinds(room, out EnemyKind leadKind, out EnemyKind partnerKind), Is.True);
                    if (!room.LineUp.Full)
                    {
                        Assert.That(leadKind, Is.EqualTo(room.LineUp.Lead), "the room's own two kinds");
                        Assert.That(partnerKind, Is.EqualTo(room.LineUp.Partner));
                    }
                    int leads = TimedChestRules.CountOf(room, leadKind), partners = partnerKind == leadKind ? 0 : TimedChestRules.CountOf(room, partnerKind);
                    Assert.That(lead, Is.EqualTo(leads == 0 ? 0 : Math.Max(1, (int)Math.Round(leads * 0.6, MidpointRounding.AwayFromZero))));
                    Assert.That(partner, Is.EqualTo(partners == 0 ? 0 : Math.Max(1, (int)Math.Round(partners * 0.6, MidpointRounding.AwayFromZero))));
                    Assert.That(lead + partner, Is.GreaterThan(0));
                    Assert.That(lead, Is.LessThanOrEqualTo(leads), "never more than the room's own");
                    Assert.That(partner, Is.LessThanOrEqualTo(partners));
                }
            Assert.That(rooms, Is.GreaterThan(100));
            var full = new LevelRoom { Kind = RoomKind.Combat, LineUp = new LineUp { Full = true } };
            TimedChestRules.WaveCounts(Floor(1, 3), full, C, out int a, out int b);
            Assert.That(a + b, Is.EqualTo(0), "the full room has no line-up of two kinds");
        }

        // ---------------- Its reward ----------------

        [Test]
        public void ItsReward_WholeUntil9s_ThenOnePercentASecond_NeverUnderTenPercent()
        {
            Assert.That(TimedChestRules.Share(0d, C), Is.EqualTo(1d));
            Assert.That(TimedChestRules.Share(8.99d, C), Is.EqualTo(1d), "whole until 9 s");
            Assert.That(TimedChestRules.Share(9d, C), Is.EqualTo(1d), "its first loss a second after it begins to fall");
            Assert.That(TimedChestRules.Share(9.99d, C), Is.EqualTo(1d));
            Assert.That(TimedChestRules.Share(10d, C), Is.EqualTo(0.99d).Within(1e-6), "1 % a second");
            Assert.That(TimedChestRules.Share(19.5d, C), Is.EqualTo(0.9d).Within(1e-6));
            Assert.That(TimedChestRules.Share(39d, C), Is.EqualTo(0.7d).Within(1e-6), "30 s in, 70 %");
            Assert.That(TimedChestRules.Share(99d, C), Is.EqualTo(0.1d).Within(1e-6), "90 s after it began to fall: the floor");
            Assert.That(TimedChestRules.Share(500d, C), Is.EqualTo(0.1d).Within(1e-6), "never under 10 %");
            for (double t = 0; t < 120; t += 0.25)
                Assert.That(TimedChestRules.Share(t + 0.25, C), Is.LessThanOrEqualTo(TimedChestRules.Share(t, C)), "it never rises");
        }

        [Test]
        public void ItsReward_IsFrozenAtTheLastKill_AndRoundsToEchoes()
        {
            // The game asks the share at the seconds of the last kill, whatever the clock says after it.
            double atKill = 25.4d, later = 80d;
            double frozen = TimedChestRules.Share(Math.Min(later, atKill), C);
            Assert.That(frozen, Is.EqualTo(0.84d).Within(1e-6), "16 s after it began: 84 %");
            Assert.That(TimedChestRules.Share(Math.Min(later + 50d, atKill), C), Is.EqualTo(frozen), "frozen");
            Assert.That(TimedChestRules.Left(13, 1d, C), Is.EqualTo(13));
            Assert.That(TimedChestRules.Left(13, 0.84d, C), Is.EqualTo(11), "10.92 rounds to 11");
            Assert.That(TimedChestRules.Left(13, 0.1d, C), Is.EqualTo(1), "the floor's 1.3 rounds to 1");
            Assert.That(TimedChestRules.Left(13, 0d, C), Is.EqualTo(1), "never under the floor's own");
            Assert.That(TimedChestRules.Left(0, 1d, C), Is.EqualTo(0));
            Assert.That(TimedChestRules.Left(100, 0.735d, C), Is.EqualTo(74));
        }

        // ---------------- The ROOMS page ----------------

        [Test]
        public void TheRoomsPage_HasTheTimedChestsLine_RightAfterTheElites()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Rooms, true, lines);
            Assert.That(lines.IndexOf(SettingLine.TimedChest), Is.EqualTo(lines.IndexOf(SettingLine.Elites) + 1), "right after the elites, found by its line");
            Assert.That(lines.IndexOf(SettingLine.Elites), Is.GreaterThan(0));
            Assert.That(SettingsPages.PageOf(SettingLine.TimedChest), Is.EqualTo(SettingsPage.Rooms));
            var trials = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, trials);
            Assert.That(trials, Has.No.Member(SettingLine.TimedChest), "not on her trials' page");
            Assert.That(TimedChestLines.Name, Is.EqualTo("Timed chest"));
            Assert.That(TimedChestLines.Words(true), Is.EqualTo(GraphicsRules.On));
            Assert.That(TimedChestLines.Words(false), Is.EqualTo(GraphicsRules.Off));
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_HasATimedChestBlock_OnlyWithAChest()
        {
            var events = new List<SessionEvent> { new SessionEvent(1d, SessionEventTypes.SessionStart) };
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events)), Does.Not.Contain("Timed chest (work order 153)"));
            var e = new SessionEvent(60d, TimedChestEvents.Chest, TimedChestEvents.Taken);
            foreach (var kv in new[]
            {
                ("floor", "2"), ("room", "4"), ("order", "6"), ("opened", "true"), ("after_clear", "3.5"), ("kinds", "swarmer 4, brute 2"),
                ("count", "6"), ("clear", "21.25"), ("share", "0.88"), ("whole", "13"), ("echoes", "11"), ("damage", "17"),
            }) e.AddExtra(kv.Item1, kv.Item2);
            events.Add(e);
            var n = new SessionEvent(90d, TimedChestEvents.Chest, TimedChestEvents.NotOpened);
            foreach (var kv in new[] { ("floor", "3"), ("room", "5"), ("opened", "false"), ("whole", "13"), ("echoes", "0") }) n.AddExtra(kv.Item1, kv.Item2);
            events.Add(n);
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.TimedChests.Count, Is.EqualTo(2));
            TimedChestRow r = stats.TimedChests[0];
            Assert.That(r.Floor, Is.EqualTo(2));
            Assert.That(r.Opened, Is.True);
            Assert.That(r.Clear, Is.EqualTo(21.25d).Within(1e-6));
            Assert.That(r.Echoes, Is.EqualTo(11));
            Assert.That(stats.TimedChests[1].Opened, Is.False);
            string text = SessionSummary.Format(stats);
            Assert.That(text, Does.Contain("Timed chest (work order 153)"));
            Assert.That(text, Does.Contain("floor 2, room 4"));
            Assert.That(text, Does.Contain("yes, 3.5 s after the room was cleared"));
            Assert.That(text, Does.Contain("swarmer 4, brute 2 (6)"));
            Assert.That(text, Does.Contain("88 % / 11 of 13"));
            Assert.That(text, Does.Contain("not opened"));
        }

        // ---------------- The numbers for the report ----------------

        /// <summary>
        /// For the report: on 300 seeds a floor, how often a floor holds a chest, why the combat rooms could not, its wave's count and weight
        /// against the room's own fight, its places, and a floor's ordinary Echoes today (the floor, its elite room and its other combat
        /// rooms) against the chest's whole. Writes "TIMEDCHEST" lines; asserts that floors 2 to 5 nearly always hold one.
        /// </summary>
        [Test]
        public void TheNumbers_ByFloor()
        {
            var v = new LevelValues();
            var meta = new MetaValues();
            const int Seeds = 300;
            for (int floor = 2; floor <= 5; floor++)
            {
                int with = 0, places = 0, count = 0, most = 0;
                double weight = 0, own = 0, ownAll = 0, echoes = 0;
                var why = new Dictionary<string, int>();
                var lineUps = new Dictionary<string, int>();
                var pointWhy = new Dictionary<string, int>();
                for (ulong seed = 1; seed <= Seeds; seed++)
                {
                    LevelLayout l = Floor(seed, floor);
                    List<LevelRoom> callers = Callers(l, v);
                    List<LevelRoom> combat = RoomCallerRules.CombatRooms(l);
                    echoes += meta.PerFloor + (combat.Any(r => r.Elite) ? meta.PerElite : 0) + meta.PerRoom * combat.Count(r => !r.Elite);
                    foreach (LevelRoom r in combat)
                    {
                        string f = TimedChestRules.RoomFault(l, r, C, callers) ?? (TimedChestRules.Places(l, r, v).Count == 0 ? "has no corner place" : "may");
                        why[f] = (why.TryGetValue(f, out int k) ? k : 0) + 1;
                        if (f == "has no corner place")
                        {
                            double cs = l.CellSize;
                            for (double y = r.Rect.Y * cs + 0.25; y < r.Rect.Top * cs; y += 0.5)
                                for (double x = r.Rect.X * cs + 0.25; x < r.Rect.Right * cs; x += 0.5)
                                {
                                    string pf = TimedChestRules.PlaceFault(l, r, new LevelPoint(x, y), v) ?? "good";
                                    if (pf.Contains("wall") || pf.Contains("outside") || pf.Contains("solid") || pf.Contains("carved")) continue;
                                    pointWhy[pf] = (pointWhy.TryGetValue(pf, out int q) ? q : 0) + 1;
                                }
                        }
                    }
                    LevelRoom room = TimedChestRules.Room(l, v, callers);
                    if (room == null) continue;
                    with++;
                    places += TimedChestRules.Places(l, room, v).Count;
                    string name = room.LineUp.Full ? "the full room" : room.LineUp.Name;
                    lineUps[name] = (lineUps.TryGetValue(name, out int n) ? n : 0) + 1;
                    TimedChestRules.WaveCounts(l, room, C, out int lead, out int partner);
                    TimedChestRules.WaveKinds(room, out EnemyKind leadKind, out EnemyKind partnerKind);
                    count += lead + partner;
                    most = Math.Max(most, lead + partner);
                    weight += lead * v.LineUpRules.WeightOf(leadKind) + partner * v.LineUpRules.WeightOf(partnerKind);
                    own += room.EnemyGroups.Where(g => g != null && !g.Elite && !g.Boss && (g.Kind == leadKind || g.Kind == partnerKind)).Sum(g => g.Count * v.LineUpRules.WeightOf(g.Kind));
                    ownAll += room.EnemyGroups.Where(g => g != null && !g.Elite && !g.Boss).Sum(g => g.Count * v.LineUpRules.WeightOf(g.Kind));
                }
                string line = string.Format(Inv,
                    "TIMEDCHEST floor {0}: {1} seeds; floors with a chest {2:0.0} %; its wave mean {3:0.0} enemies (most {4}), its weight {5:0} % of the room's own two kinds ({12:0} % of all the room's own); corner places a chest's room {6:0.0}; a floor's ordinary Echoes mean {7:0.0}, the chest's whole {8} ({9:0} % of them); line-ups: {10}; the combat rooms: {11}",
                    floor, Seeds, 100.0 * with / Seeds, with > 0 ? (double)count / with : 0, most, own > 0 ? 100.0 * weight / own : 0,
                    with > 0 ? (double)places / with : 0, echoes / Seeds, TimedChestRules.Whole(floor, C), 100.0 * TimedChestRules.Whole(floor, C) / (echoes / Seeds),
                    string.Join(", ", lineUps.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value)),
                    string.Join(", ", why.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value)), ownAll > 0 ? 100.0 * weight / ownAll : 0);
                line += "; in rooms with no corner place, the open floor's points: " + string.Join(", ", pointWhy.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value));
                TestContext.WriteLine(line);
                Console.WriteLine(line);
                Assert.That(with, Is.GreaterThanOrEqualTo(Seeds * 95 / 100), "floor " + floor + ": nearly every floor from 2 holds one");
            }
        }
    }
}
