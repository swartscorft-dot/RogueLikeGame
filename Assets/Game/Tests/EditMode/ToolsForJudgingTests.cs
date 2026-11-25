using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 090, the tools for judging the floors: the room lab's presets one a floor, each the generator's own draw for a
    /// room of its kind on its floor (over a hundred seeds, kinds and counts, beside the floors' rooms of a hundred seeds); the
    /// session summary's "where time went" (fights, rooms, passages adding up to the floor's time, the screens beside it); and its
    /// "doors opened" line (each reason and each pull counted from its own recorded event, the rare lock case said at a glance).
    /// "LABPRESET" lines print what each preset places, for the report.
    /// </summary>
    public class ToolsForJudgingTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int Seeds = 100;

        // ---------------- The lab's presets ----------------

        sealed class Tally
        {
            public int Rooms;
            public readonly Dictionary<EnemyKind, int> Total = new Dictionary<EnemyKind, int>();
            public readonly Dictionary<EnemyKind, int> Holding = new Dictionary<EnemyKind, int>();
            public readonly Dictionary<EnemyKind, int> Most = new Dictionary<EnemyKind, int>();
            public int Enemies;

            public void Add(IEnumerable<EnemyGroup> groups)
            {
                Rooms++;
                var here = new Dictionary<EnemyKind, int>();
                foreach (EnemyGroup g in groups.Where(g => g != null && !g.Elite && !g.Boss))
                    here[g.Kind] = (here.TryGetValue(g.Kind, out int n) ? n : 0) + g.Count;
                foreach (KeyValuePair<EnemyKind, int> p in here)
                {
                    Total[p.Key] = (Total.TryGetValue(p.Key, out int t) ? t : 0) + p.Value;
                    Holding[p.Key] = (Holding.TryGetValue(p.Key, out int h) ? h : 0) + 1;
                    Most[p.Key] = Math.Max(Most.TryGetValue(p.Key, out int m) ? m : 0, p.Value);
                }
                Enemies += here.Values.Sum();
            }

            public double Mean(EnemyKind k) => Rooms > 0 && Total.TryGetValue(k, out int t) ? t / (double)Rooms : 0d;
            public double Share(EnemyKind k) => Rooms > 0 && Holding.TryGetValue(k, out int h) ? h / (double)Rooms : 0d;
            public int MostOf(EnemyKind k) => Most.TryGetValue(k, out int m) ? m : 0;

            public string Words()
            {
                var parts = new List<string>();
                foreach (EnemyKind k in Total.Keys.OrderBy(k => k))
                    parts.Add(string.Format(Inv, "{0} {1:0.00} (in {2:0}%, most {3})", EnemyKinds.Word(k), Mean(k), Share(k) * 100.0, MostOf(k)));
                return string.Format(Inv, "{0} rooms, {1:0.0} enemies: ", Rooms, Rooms > 0 ? Enemies / (double)Rooms : 0d) + string.Join(", ", parts);
            }
        }

        /// <summary>
        /// The order's test: each preset's room of each kind (ordinary, elite, exit) against the rooms of that kind the generator
        /// makes on that floor, a hundred seeds each: the kinds met (every kind in at least 5 % of either's rooms is in both), each
        /// kind's mean a room and the share of rooms holding it close (the two are samples of the same draw), and the most of each of
        /// the act plan's new kinds a room holds the same. The ordinary rooms of a floor are its combat rooms but the first (which by
        /// the floors' rule never holds a warder or a bomber) and the elite.
        /// </summary>
        [Test]
        public void EachPreset_IsTheGeneratorsOwnDraw_ForItsFloorsRooms_OverAHundredSeeds()
        {
            var v = new LevelValues();
            // Changed deliberately (work order 108): the floors' own presets, one a floor; after them "floor 4 with a lancer", which
            // the floors do not hold yet (LancerTests holds it to its rule).
            Assert.That(RoomLab.Mixes.Where(m => m.Lancers == 0).Select(m => m.Floor), Is.EqualTo(Enumerable.Range(1, v.FloorsPerAct)), "one preset a floor, in order");
            var log = new StringBuilder();
            foreach (LabMix mix in RoomLab.Mixes.Where(m => m.Lancers == 0))
            {
                var floorRooms = new Dictionary<LabKind, Tally> { [LabKind.Ordinary] = new Tally(), [LabKind.Elite] = new Tally(), [LabKind.Exit] = new Tally() };
                var labRooms = new Dictionary<LabKind, Tally> { [LabKind.Ordinary] = new Tally(), [LabKind.Elite] = new Tally(), [LabKind.Exit] = new Tally() };
                for (ulong s = 1; s <= Seeds; s++)
                {
                    LevelLayout floor = LevelGenerator.Generate(s * 7907UL + 90, mix.Floor, v);
                    LevelRoom first = LevelGeometry.MainPath(floor).FirstOrDefault(r => r.Kind == RoomKind.Combat);
                    foreach (LevelRoom r in floor.Rooms)
                    {
                        if (r.Kind == RoomKind.Exit) floorRooms[LabKind.Exit].Add(r.EnemyGroups);
                        else if (r.Kind == RoomKind.Combat && r.Elite) floorRooms[LabKind.Elite].Add(r.EnemyGroups);
                        else if (r.Kind == RoomKind.Combat && r != first) floorRooms[LabKind.Ordinary].Add(r.EnemyGroups);
                    }
                    foreach (LabKind kind in labRooms.Keys)
                        labRooms[kind].Add(RoomLab.Groups(kind, mix, LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0), v));
                }
                foreach (LabKind kind in labRooms.Keys)
                {
                    Tally a = labRooms[kind], b = floorRooms[kind];
                    string where = mix + " " + RoomLab.Word(kind);
                    log.AppendLine("LABPRESET " + where + "  lab:   " + a.Words());
                    log.AppendLine("LABPRESET " + where + "  floor: " + b.Words());
                    var met = new HashSet<EnemyKind>(a.Total.Keys.Concat(b.Total.Keys));
                    foreach (EnemyKind k in met)
                    {
                        // Work order 118 (changed deliberately): since the generator's version 22 the floors' rooms of floors 4 and 5 hold
                        // the lancer; the lab's floor presets do not draw it ("floor 4 with a lancer" stands one, LancerTests), so it is
                        // left out of the kinds met here. Every other kind and the count a room are held to the floors with it.
                        if (k == EnemyKind.Lancer) continue;
                        if (Math.Max(a.Share(k), b.Share(k)) >= 0.05)
                        {
                            Assert.That(a.Share(k) > 0 && b.Share(k) > 0, Is.True, where + ": " + EnemyKinds.Word(k) + " met in both");
                            Assert.That(a.Mean(k), Is.EqualTo(b.Mean(k)).Within(0.25 * Math.Max(a.Mean(k), b.Mean(k)) + 0.35), where + ": " + EnemyKinds.Word(k) + " a room");
                            Assert.That(a.Share(k), Is.EqualTo(b.Share(k)).Within(0.18), where + ": rooms holding a " + EnemyKinds.Word(k));
                        }
                    }
                    foreach (EnemyKind k in new[] { EnemyKind.Caster, EnemyKind.Warder, EnemyKind.Bomber })
                        Assert.That(a.MostOf(k), Is.EqualTo(b.MostOf(k)), where + ": the most " + EnemyKinds.Word(k) + "s a room holds");
                    Assert.That(a.Enemies / (double)a.Rooms, Is.EqualTo(b.Enemies / (double)b.Rooms).Within(0.12 * b.Enemies / b.Rooms), where + ": enemies a room");
                }
            }
            TestContext.WriteLine(log.ToString());
        }

        /// <summary>The act's plan in the presets: no caster on floor 2, a warder can come from floor 3, a bomber from floor 4 (over the hundred seeds).</summary>
        [Test]
        public void ThePresets_FollowTheActsPlan_NoCasterOnFloor2_TheWarderFrom3_TheBomberFrom4()
        {
            var v = new LevelValues();
            bool Any(int floor, EnemyKind k) =>
                Enumerable.Range(1, Seeds).Any(s => RoomLab.Groups(LabKind.Ordinary, RoomLab.Mixes[floor - 1], LevelRandom.Stream((ulong)s, LevelGenerator.ContentsStage, 0), v).Any(g => g.Kind == k));
            Assert.That(Any(2, EnemyKind.Caster), Is.False, "floor 2: no caster");
            Assert.That(Any(1, EnemyKind.Warder) || Any(2, EnemyKind.Warder), Is.False, "no warder before floor 3");
            Assert.That(Any(3, EnemyKind.Warder), Is.True, "floor 3: its warder");
            Assert.That(Any(3, EnemyKind.Caster), Is.True, "floor 3: its casters");
            Assert.That(Any(3, EnemyKind.Bomber), Is.False, "no bomber before floor 4");
            Assert.That(Any(4, EnemyKind.Bomber) && Any(5, EnemyKind.Bomber), Is.True, "floors 4 and 5: their bombers");
            // What each preset places in a combat room of the lab's own seed (the report's line), the same each time.
            var seeds = new[] { 0x0900090UL };
            foreach (LabMix mix in RoomLab.Mixes)
                foreach (ulong seed in seeds)
                {
                    List<EnemyGroup> a = RoomLab.Groups(LabKind.Ordinary, mix, LevelRandom.Stream(seed, LevelGenerator.ContentsStage, 0), v);
                    List<EnemyGroup> b = RoomLab.Groups(LabKind.Ordinary, mix, LevelRandom.Stream(seed, LevelGenerator.ContentsStage, 0), v);
                    Assert.That(a.Select(g => g.ToString()), Is.EqualTo(b.Select(g => g.ToString())), "the same seed, the same room");
                    TestContext.WriteLine("LABPRESET " + mix + ", a combat room, seed " + LevelSeed.ToText(seed) + ": " + Placed(a));
                }
        }

        static string Placed(List<EnemyGroup> groups)
        {
            var by = new SortedDictionary<EnemyKind, int>();
            foreach (EnemyGroup g in groups) by[g.Kind] = (by.TryGetValue(g.Kind, out int n) ? n : 0) + g.Count;
            return by.Values.Sum() + " enemies: " + string.Join(", ", by.Select(p => p.Value + " " + EnemyKinds.Word(p.Key)));
        }

        // ---------------- Where the time went ----------------

        static SessionEvent E(double t, string type, string name = null, params string[] extras)
        {
            var e = new SessionEvent(t, type, name);
            for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
            return e;
        }

        static SessionEvent Wake(double t, int id) => new SessionEvent(t, SessionEventTypes.EnemyWake) { EnemyId = id, Enemy = "brute" };
        static SessionEvent Death(double t, int id) => new SessionEvent(t, SessionEventTypes.EnemyDeath) { EnemyId = id, Enemy = "brute" };

        /// <summary>
        /// A run of two floors: floor 1 from 0 to 105 s (the entrance, a passage 10 to 20, the room's fight 25 to 60, a passage 70 to
        /// 80 with a fight in it 72 to 78, the exit room; a reward screen of 12.5 s real time and a pause of 3 s), floor 2 from 105 to
        /// 150 s (the run's end), with a fight 110 to 140 in a room.
        /// </summary>
        static List<SessionEvent> TwoFloors()
        {
            return new List<SessionEvent>
            {
                E(0, SessionEventTypes.SessionStart),
                E(0, SessionEventTypes.RunStart, "RUNSEED"),
                E(0, SessionEventTypes.FloorEnter, "F1", "floor", "1"),
                E(0, SessionEventTypes.RoomEntered, "entrance", "room", "0"),
                E(10, SessionEventTypes.RoomEntered, "passage", "room", "1"),
                E(20, SessionEventTypes.RoomEntered, "combat", "room", "2"),
                Wake(25, 1), Wake(25, 2), Death(50, 1), Death(60, 2),
                E(40, SessionEventTypes.Screen, "pause menu", "run", "1", "floor", "1", "seconds", "3"),
                E(70, SessionEventTypes.RoomEntered, "passage", "room", "3"),
                Wake(72, 3), Death(78, 3),
                E(80, SessionEventTypes.RoomEntered, "exit", "room", "4"),
                E(100, SessionEventTypes.FloorFinish, null, "floor", "1", "seconds", "100", "health", "80"),
                E(104, SessionEventTypes.Screen, "floor reward", "run", "1", "floor", "1", "seconds", "12.5"),
                E(105, SessionEventTypes.FloorEnter, "F2", "floor", "2"),
                E(105, SessionEventTypes.RoomEntered, "entrance", "room", "0"),
                E(108, SessionEventTypes.RoomEntered, "combat", "room", "2"),
                Wake(110, 4), Death(140, 4),
                new SessionEvent(150, SessionEventTypes.RunEnd) { Result = "left", Extra = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("floor", "2"), new KeyValuePair<string, string>("room", "1"), new KeyValuePair<string, string>("seconds", "150") } },
                E(160, SessionEventTypes.SessionEnd),
            };
        }

        [Test]
        public void WhereTimeWent_FightsRoomsAndPassages_AddUpToTheFloorsTime_AndTheScreensAreBesideIt()
        {
            SessionStats stats = SessionSummary.Compute(TwoFloors());
            RunRow run = stats.Runs.Single();
            FloorTimeRow one = run.FloorTimes[1], two = run.FloorTimes[2];
            Assert.That(one.Seconds, Is.EqualTo(105).Within(1e-6), "floor 1: from her coming onto it to the next floor");
            Assert.That(one.Fights, Is.EqualTo(35 + 6).Within(1e-6), "the room's fight and the one in the passage");
            Assert.That(one.Passages, Is.EqualTo(10 + 10 - 6).Within(1e-6), "the passages, less the fight in one");
            Assert.That(one.Rooms, Is.EqualTo(105 - 41 - 14).Within(1e-6), "the rest: in rooms outside a fight");
            Assert.That(one.Screens, Is.EqualTo(15.5).Within(1e-6), "the pause and the reward, real time, beside the floor's time");
            Assert.That(one.ScreensShown, Is.EqualTo(2));
            Assert.That(two.Seconds, Is.EqualTo(45).Within(1e-6));
            Assert.That(two.Fights, Is.EqualTo(30).Within(1e-6));
            Assert.That(two.Passages, Is.Zero);
            Assert.That(two.Screens, Is.Zero);
            foreach (FloorTimeRow f in run.FloorTimes.Values)
                Assert.That(f.Fights + f.Rooms + f.Passages, Is.EqualTo(f.Seconds).Within(1.0), "floor " + f.Floor + ": the parts add up to the floor's time within a second");

            string text = SessionSummary.Format(stats);
            TestContext.WriteLine(text);
            StringAssert.Contains("  where time went  run 2:30.0 = fights 1:11.0 + rooms 1:05.0 + passages 14.0 s;  screens and menus 15.5 s more (real time, the game stood still; 2 shown)", text);
            StringAssert.Contains("floor 1 1:45.0 = fights 41.0 s + rooms 50.0 s + passages 14.0 s;  screens 15.5 s more", text);
            StringAssert.Contains("floor 2 45.0 s = fights 30.0 s + rooms 15.0 s + passages 0.0 s", text);
            // Read back from the log's lines, the same.
            string again = SessionSummary.Format(SessionSummary.Compute(TwoFloors().Select(e => SessionEvent.Parse(e.ToJson())).ToList()));
            Assert.That(again, Is.EqualTo(text), "the summary recomputed from the log is the same");
        }

        [Test]
        public void WhereTimeWent_ASessionWithNoFloor_HasNoSuchLine_AndTheLabIsNoRunsFloor()
        {
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.SessionStart),
                E(1, LabRoomEvents.Room, "nave", "kind", "ordinary", "mix", "floor 3", "floor", "3", "seed", "AB", "ms", "50"),
                E(2, SessionEventTypes.RoomEntered, "passage", "room", "0"),
                E(5, SessionEventTypes.RoomDoor, "closed", "floor", "3", "room", "1", "order", "1"),
                E(9, SessionEventTypes.RoomDoor, "opened", "floor", "3", "room", "1", "order", "1", "seconds", "4", "why", "an enemy outside"),
                E(20, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs, Is.Empty);
            string text = SessionSummary.Format(stats);
            StringAssert.DoesNotContain("where time went", text);
            StringAssert.DoesNotContain("doors opened", text);
        }

        // ---------------- Why the doors opened ----------------

        static SessionEvent Opened(double t, string why) =>
            E(t, SessionEventTypes.RoomDoor, "opened", "floor", "1", "room", "1", "order", "2", "seconds", "5", "why", why);

        static SessionEvent Pull(double t, bool shut, int putIn, int left) =>
            E(t, SessionEventTypes.RoomDoor, "pull", "floor", "1", "room", "1", "order", "2", "shut", shut ? "true" : "false", "in", putIn.ToString(Inv), "left", left.ToString(Inv));

        [Test]
        public void TheDoorsReasons_AreCountedEachFromItsEvent_AndTheRareLockCaseIsSaidAtAGlance()
        {
            List<SessionEvent> Run(params SessionEvent[] doors)
            {
                var list = new List<SessionEvent> { E(0, SessionEventTypes.SessionStart), E(0, SessionEventTypes.RunStart, "S"), E(0, SessionEventTypes.FloorEnter, "F", "floor", "1") };
                list.AddRange(doors);
                list.Add(E(100, SessionEventTypes.SessionEnd));
                return list;
            }
            // A run without it: two rooms cleared, one pull that put two in before the doors shut.
            SessionStats calm = SessionSummary.Compute(Run(E(5, SessionEventTypes.RoomDoor, "closed", "floor", "1", "room", "1", "order", "2"),
                                                           Pull(4, false, 2, 0), Opened(20, "cleared"), Opened(50, "cleared")));
            DoorReasons c = calm.Runs.Single().Doors;
            Assert.That((c.Cleared, c.HerDeath, c.SheLeft, c.EnemyOutside), Is.EqualTo((2, 0, 0, 0)));
            Assert.That((c.Pulls, c.PullsThatPutIn, c.PullsFailed, c.PullsWhileShut, c.PutIn), Is.EqualTo((1, 1, 0, 0, 2)));
            Assert.That(c.LockCase, Is.False);
            string calmText = SessionSummary.Format(calm);
            StringAssert.Contains("  doors opened     cleared 2, her death 0, she left 0, an enemy outside 0;  pulls 1 (0 while shut): 1 put an enemy inside, 0 failed;  the rare lock case: no", calmText);

            // A run with it: a pull that failed and one while shut, the doors opened for the one outside; a death and a teleport.
            SessionStats locked = SessionSummary.Compute(Run(Pull(4, false, 0, 1), Pull(4.5, false, 1, 0), Opened(20, "cleared"), Pull(30, true, 0, 1),
                                                             Opened(31, "an enemy outside"), Opened(40, "she left"), Opened(60, "her death")));
            DoorReasons d = locked.Runs.Single().Doors;
            Assert.That((d.Cleared, d.HerDeath, d.SheLeft, d.EnemyOutside), Is.EqualTo((1, 1, 1, 1)), "each reason from its own event");
            Assert.That((d.Pulls, d.PullsThatPutIn, d.PullsFailed, d.PullsWhileShut, d.PutIn), Is.EqualTo((3, 1, 2, 1, 1)), "each pull from its own event");
            Assert.That(d.LockCase, Is.True);
            StringAssert.Contains("THE RARE LOCK CASE HAPPENED", SessionSummary.Format(locked));

            // Her death opens the doors in the frame after the run's end is told: still that run's.
            SessionStats died = SessionSummary.Compute(new List<SessionEvent>
            {
                E(0, SessionEventTypes.RunStart, "S"), E(0, SessionEventTypes.FloorEnter, "F", "floor", "1"),
                new SessionEvent(30, SessionEventTypes.RunEnd) { Result = "death" }, Opened(30, "her death"),
                E(31, SessionEventTypes.RunStart, "T"), Opened(40, "cleared"),
            });
            Assert.That(died.Runs[0].Doors.HerDeath, Is.EqualTo(1));
            Assert.That(died.Runs[1].Doors.HerDeath, Is.Zero);
            Assert.That(died.Runs[1].Doors.Cleared, Is.EqualTo(1));
        }

        [Test]
        public void ALogFromBefore_HasNeitherNewLine()
        {
            var events = new List<SessionEvent> { E(0, SessionEventTypes.SessionStart), E(0, SessionEventTypes.RunStart, "S") };
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            StringAssert.DoesNotContain("where time went", text);
            StringAssert.DoesNotContain("doors opened", text);
        }
    }
}
