using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The act's plan (work order 087; Kristian, 2026-11-22: "Ja, én ny ting per etasje", both new kinds into the floors, and
    /// "Trekk fiendene nærmere"): the plan as a table (floor 2 the guard and no caster, floor 3 the caster and the warder, floor
    /// 4 the bomber and the shooter's fan, floor 5 the act boss), one number per kind for its first floor, the ranged enemies'
    /// new distances, and a thousand floors of each number with the plan off (version 17's) and on (version 18's): what each
    /// room and floor holds by kind, the rooms that hold each new kind, the count of a room the same in the mean, and where the
    /// ranged enemies stand and first can shoot from when she crosses the room's point. With the plan off every floor is
    /// version 17's, field for field (the fingerprint below, and FloorPlansStayTests).
    /// </summary>
    public class ActPlanTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int Seeds = 1000;

        // The ranged enemies' distances before this order (version 17's ShooterValues and CasterValues), for the "before" table.
        const float ShootsFromBefore = 14f, CastsFromBefore = 15f;
        const float ShooterKeepBefore = (8f + 12f) / 2f, CasterKeepBefore = (9f + 13f) / 2f; // where each stops walking closer

        static ulong RunSeedOf(int i) => (ulong)i * 0x9E3779B97F4A7C15UL + 87UL;

        // ---------------- The plan, as a table ----------------

        [Test]
        public void ThePlan_OneNewThingAFloor_AsATable()
        {
            var v = new LevelValues { FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 104 (changed deliberately): 087's plan; the floors' character moves the elites a floor on (FloorCharacterTests)
            Assert.That(v.ActPlan, Is.True, "on by default (work order 087)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(24)); // work order 088; 103: 20; 104: 21; 118: 22; 124: 23; 130: 24
            var shooter = new ShooterValues();
            var lines = new List<string> { "ACTPLANTABLE floor  guards  casters (exit)  caster elite  warders  bombers (exit)  shooter's fan  act boss" };
            int[] guards = { 0, 1, 1, 1, 1 }, casters = { 0, 0, 1, 1, 2 }, castersExit = { 0, 0, 2, 2, 3 };
            int[] warders = { 0, 0, 1, 1, 1 }, bombers = { 0, 0, 0, 1, 2 }, bombersExit = { 0, 0, 0, 2, 3 }; // 087's follow-up: one on its first floor, two after
            for (int f = 1; f <= 5; f++)
            {
                int i = f - 1;
                Assert.That(v.GuardsMost(RoomKind.Combat, f), Is.EqualTo(guards[i]), "guards, floor " + f);
                Assert.That(v.CastersMost(RoomKind.Combat, f), Is.EqualTo(casters[i]), "casters in a combat room, floor " + f);
                Assert.That(v.CastersMost(RoomKind.Exit, f), Is.EqualTo(castersExit[i]), "casters in the exit room, floor " + f);
                bool casterElite = v.EliteKinds(f).Contains(EnemyKind.Caster);
                Assert.That(casterElite, Is.EqualTo(f >= 3), "an elite caster, floor " + f);
                Assert.That(LevelWarders.Most(v, RoomKind.Combat, f, false, 3), Is.EqualTo(warders[i]), "warders, floor " + f);
                Assert.That(LevelWarders.Most(v, RoomKind.Exit, f, false, 3), Is.EqualTo(warders[i]), "warders in the exit room, floor " + f);
                Assert.That(LevelBombers.Most(v, RoomKind.Combat, f, false), Is.EqualTo(bombers[i]), "bombers, floor " + f);
                Assert.That(LevelBombers.Most(v, RoomKind.Exit, f, false), Is.EqualTo(bombersExit[i]), "bombers in the exit room, floor " + f);
                bool fan = ShooterTactics.IsFan(shooter.FanEvery, f, false, shooter);
                Assert.That(fan, Is.EqualTo(f >= 4), "the shooter's fan, floor " + f);
                bool boss = LevelGenerator.DrawGroups(RoomKind.Exit, f, new LevelRandom(1UL), v).Any(g => g.Boss);
                Assert.That(boss, Is.EqualTo(f == 5), "the act boss, floor " + f);
                Assert.That(v.EliteKinds(f), Has.No.Member(EnemyKind.Warder).And.No.Member(EnemyKind.Bomber), "never an elite");
                lines.Add(string.Format(Inv, "ACTPLANTABLE {0}  {1}  {2} ({3})  {4}  {5}  {6} ({7})  {8}  {9}", f, guards[i], casters[i], castersExit[i],
                                        casterElite ? "yes" : "no", warders[i], bombers[i], bombersExit[i], fan ? "yes" : "no", boss ? "yes" : "no"));
            }
            // Never in the floor's first combat room, the warder only with three others: their rules of 073 and 079.
            Assert.That(LevelWarders.Most(v, RoomKind.Combat, 3, true, 9), Is.EqualTo(0));
            Assert.That(LevelWarders.Most(v, RoomKind.Combat, 3, false, 2), Is.EqualTo(0));
            Assert.That(LevelBombers.Most(v, RoomKind.Combat, 4, true), Is.EqualTo(0));
            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void EachKindsFirstFloor_IsOneNumber_ThatMovesIt()
        {
            // The caster: CastersFromFloor, the rooms' and the elite's.
            var v = new LevelValues { CastersFromFloor = 2, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 104: 087's elite rule
            Assert.That(v.CastersMost(RoomKind.Combat, 2), Is.EqualTo(1), "moved down to floor 2: one a room there");
            Assert.That(v.EliteKinds(2), Has.Member(EnemyKind.Caster), "and the elite may be one");
            v = new LevelValues { CastersFromFloor = 4, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false };
            Assert.That(v.CastersMost(RoomKind.Combat, 3) + v.CastersMost(RoomKind.Exit, 3), Is.EqualTo(0), "moved up to floor 4: none on floor 3");
            Assert.That(v.EliteKinds(3), Has.No.Member(EnemyKind.Caster));
            Assert.That(v.CastersMost(RoomKind.Combat, 4), Is.EqualTo(1));
            // The warder: WarderFloors.FromFloor.
            v = new LevelValues();
            v.WarderFloors.FromFloor = 4;
            Assert.That(LevelWarders.Most(v, RoomKind.Combat, 3, false, 5), Is.EqualTo(0));
            Assert.That(LevelWarders.Most(v, RoomKind.Combat, 4, false, 5), Is.EqualTo(1));
            // The bomber: BomberFloors.FromFloor.
            v = new LevelValues();
            v.BomberFloors.FromFloor = 3;
            Assert.That(LevelBombers.Most(v, RoomKind.Combat, 3, false), Is.EqualTo(1), "moved down to floor 3: one a room there, its first floor");
            Assert.That(LevelBombers.Most(v, RoomKind.Combat, 4, false), Is.EqualTo(2), "and two from the floor after: the caps move with it");
            Assert.That(new BomberFloorValues().MostInRoom, Is.EqualTo(new[] { 1, 2 }), "counted from its first floor (087's follow-up)");
            // The fan: ShooterValues.FanFromFloor.
            var s = new ShooterValues { FanFromFloor = 3 };
            Assert.That(ShooterTactics.IsFan(s.FanEvery, 3, false, s), Is.True);
            Assert.That(ShooterTactics.IsFan(s.FanEvery, 2, false, s), Is.False);
            // And their defaults.
            v = new LevelValues();
            Assert.That((v.CastersFromFloor, v.WarderFloors.FromFloor, v.BomberFloors.FromFloor, new ShooterValues().FanFromFloor, v.GuardsFromFloor),
                        Is.EqualTo((3, 3, 4, 4, 2)));
        }

        [Test]
        public void ThePlanOff_IsVersion17sRules()
        {
            var off = new LevelValues { ActPlan = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, UsableRooms = false };
            int[] casters = { 0, 1, 1, 2, 2 }, castersExit = { 0, 2, 2, 3, 3 };
            for (int f = 1; f <= 5; f++)
            {
                Assert.That(off.CastersMost(RoomKind.Combat, f), Is.EqualTo(casters[f - 1]), "version 17's casters, floor " + f);
                Assert.That(off.CastersMost(RoomKind.Exit, f), Is.EqualTo(castersExit[f - 1]));
                Assert.That(off.EliteKinds(f).Contains(EnemyKind.Caster), Is.EqualTo(f >= off.EliteCasterFromFloor));
                foreach (RoomKind k in new[] { RoomKind.Combat, RoomKind.Exit })
                {
                    Assert.That(LevelWarders.Most(off, k, f, false, 9), Is.EqualTo(0), "no warder, whatever its switch says");
                    Assert.That(LevelBombers.Most(off, k, f, false), Is.EqualTo(0), "no bomber, whatever its switch says");
                }
            }
            Assert.That(off.Warders && off.Bombers, Is.True, "their switches on, and the plan off reads neither");
            Assert.That(off.WardersInFloors || off.BombersInFloors, Is.False);
        }

        // ---------------- The ranged enemies' distances ----------------

        /// <summary>The floor the camera shows from her, ahead (north) and behind (south), and half its width at a forward offset, in metres (decision 0014's camera).</summary>
        static void Screen(out double ahead, out double behind, out Func<double, double> halfWidth)
        {
            var feel = new FeelValues();
            double pitch = feel.CameraPitch * Math.PI / 180.0, half = feel.FieldOfView * 0.5 * Math.PI / 180.0;
            double h = feel.CameraDistance * Math.Sin(pitch), back = feel.CameraDistance * Math.Cos(pitch);
            ahead = h / Math.Tan(pitch - half) - back;
            behind = back - h / Math.Tan(pitch + half);
            double across = Math.Atan(Math.Tan(half) * 16.0 / 9.0); // 1920 by 1080
            double hh = h, bb = back;
            halfWidth = y => ((y + bb) * Math.Cos(pitch) + hh * Math.Sin(pitch)) * Math.Tan(across);
        }

        [Test]
        public void TheRangedEnemies_KeepAndShootFromWithinWhatTheScreenShowsAhead()
        {
            Screen(out double ahead, out double behind, out _);
            Assert.That(ahead, Is.EqualTo(11.3).Within(0.05), "the metric sheet's 11.3 m ahead");
            Assert.That(behind, Is.EqualTo(7.7).Within(0.05), "and 7.7 m behind");

            var s = new ShooterValues();
            Assert.That((s.KeepNear, s.KeepFar, s.BackOffUnder), Is.EqualTo((7f, 10f, 6f)), "keeps 7 to 10 m (8 to 12 before), backs off under 6 m as before");
            Assert.That(ShooterValues.ShootsFrom, Is.EqualTo(11f));
            foreach (EnemyAttackDefinition a in s.Arrows.Concat(s.Enemy.Attacks))
                Assert.That((a.MinDistance, a.MaxDistance), Is.EqualTo((0f, 11f)), a.Name + ": from up to 11 m (14 before)");
            Assert.That((s.Enemy.AggroRange, s.Enemy.LeashRange, s.Enemy.MoveSpeed), Is.EqualTo((14f, 26f, 3f)), "its wake range and speed untouched");
            EnemyAttackDefinition arrow = CasterTactics.Find(s.Arrows, ShooterValues.Arrow);
            Assert.That((arrow.Windup, arrow.Recovery, arrow.Damage), Is.EqualTo((0.9f, 1.8f, 15)), "its timings and damage untouched");
            Assert.That(new ShooterTactics().Peek(11f, 1, false, s), Is.Not.Null, "it shoots at 11 m");
            Assert.That(new ShooterTactics().Peek(11.5f, 1, false, s), Is.Null, "not at 11.5 m");
            Assert.That(new ShooterTactics().Peek(11.5f, 1, true, s), Is.Null, "nor as an elite (its volley)");

            var c = new CasterValues();
            Assert.That((c.KeepNear, c.KeepFar, c.BackOffUnder), Is.EqualTo((7.5f, 10f, 6f)), "keeps 7.5 to 10 m (9 to 13 before), backs off under 6 m as before");
            Assert.That(CasterValues.CastsFrom, Is.EqualTo(11f));
            foreach (EnemyAttackDefinition a in c.Enemy.Attacks.Concat(c.Orbs))
                Assert.That((a.MinDistance, a.MaxDistance), Is.EqualTo((4f, 11f)), a.Name + ": from 4 to 11 m (4 to 15 before)");
            Assert.That((c.Enemy.AggroRange, c.Enemy.LeashRange, c.Enemy.MoveSpeed), Is.EqualTo((14f, 26f, 2.6f)), "its wake range and speed untouched");
            EnemyAttackDefinition orb = c.Find(CasterValues.BoneOrb);
            Assert.That((orb.Windup, orb.Recovery, orb.Damage), Is.EqualTo((1.0f, 1.3f, 40)), "its timings and damage untouched");
            Assert.That(new CasterTactics().Peek(11f, c, true), Is.Not.Null, "it casts at 11 m");
            Assert.That(new CasterTactics().Peek(11.5f, c, true), Is.Null, "not at 11.5 m");
            Assert.That(new CasterTactics().Peek(3.9f, c, true), Is.Null, "nor nearer than 4 m, as before");

            // The warder's bolt is the caster's orb: it follows, and its own distances (8 to 10 m) were inside the screen already.
            var w = new WarderValues();
            EnemyAttackDefinition bolt = WarderValues.Bolt();
            Assert.That((bolt.MinDistance, bolt.MaxDistance), Is.EqualTo((4f, 11f)), "the warder's bolt from 4 to 11 m (15 before), as the orb");
            Assert.That((w.KeepNear, w.KeepFar), Is.EqualTo((8f, 10f)), "the warder keeps as it did");

            foreach (float d in new[] { s.KeepFar, ShooterValues.ShootsFrom, c.KeepFar, CasterValues.CastsFrom, bolt.MaxDistance, w.KeepFar })
                Assert.That(d, Is.LessThanOrEqualTo(ahead), "within the floor the screen shows ahead of her");
        }

        // ---------------- A thousand floors of each number, before and after ----------------

        sealed class Tally
        {
            public int Floors, Rooms;
            public readonly Dictionary<EnemyKind, long> RoomSum = new Dictionary<EnemyKind, long>();
            public readonly Dictionary<EnemyKind, int> RoomMost = new Dictionary<EnemyKind, int>();
            public readonly Dictionary<EnemyKind, long> FloorSum = new Dictionary<EnemyKind, long>();
            public readonly Dictionary<EnemyKind, int> FloorMost = new Dictionary<EnemyKind, int>();
            public readonly Dictionary<EnemyKind, long> RoomsWithLong = new Dictionary<EnemyKind, long>();
            public long Enemies;       // every room's count, the elite and the boss apart
            public int EnemiesMost;
            public readonly Dictionary<string, List<Ranged>> RangedOf = new Dictionary<string, List<Ranged>>();
        }

        struct Ranged
        {
            public double FromDoor, FromHer, Walk, Seconds, ShootsFromDoor, WalksIn;
            public bool AtOnce, OffScreen;
        }

        static readonly EnemyKind[] Kinds =
        {
            EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Warder, EnemyKind.Bomber, EnemyKind.Boss,
        };

        static void Add<T>(Dictionary<T, long> d, T k, long n) => d[k] = (d.TryGetValue(k, out long was) ? was : 0) + n;

        static void Most<T>(Dictionary<T, int> d, T k, int n) => d[k] = Math.Max(d.TryGetValue(k, out int was) ? was : 0, n);

        /// <summary>Where she stands when she crosses a room's point: straight in from the middle of its door in, as deep as the point.</summary>
        static LevelPoint HerAtThePoint(LevelLayout level, LevelRoom room, LevelDoor door, LevelValues v)
        {
            door.Opening(level.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5, depth = RoomFightRules.PointDepth(level, room, door, v.Fights);
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: return new LevelPoint(mx, my + depth);
                case LevelGenerator.North: return new LevelPoint(mx, my - depth);
                case LevelGenerator.West: return new LevelPoint(mx + depth, my);
                default: return new LevelPoint(mx - depth, my);
            }
        }

        static void Count(LevelLayout level, LevelValues v, bool after, Tally t)
        {
            Screen(out double ahead, out double behind, out Func<double, double> halfWidth);
            var s = new ShooterValues();
            var c = new CasterValues();
            var w = new WarderValues();
            t.Floors++;
            var onFloor = new Dictionary<EnemyKind, long>();
            foreach (LevelRoom r in level.Rooms)
            {
                if (r.Kind != RoomKind.Combat && r.Kind != RoomKind.Exit) continue;
                t.Rooms++;
                int count = r.EnemyGroups.Where(g => !g.Elite && !g.Boss).Sum(g => g.Count);
                t.Enemies += count;
                t.EnemiesMost = Math.Max(t.EnemiesMost, count);
                foreach (EnemyKind k in Kinds)
                {
                    int n = r.EnemyGroups.Where(g => g.Kind == k).Sum(g => g.Count);
                    Add(t.RoomSum, k, n);
                    Most(t.RoomMost, k, n);
                    Add(onFloor, k, n);
                    if (n > 0) Add(t.RoomsWithLong, k, 1);
                }
                LevelDoor door = LevelGeometry.EntryDoor(level, r);
                if (door == null) continue;
                LevelPoint her = HerAtThePoint(level, r, door, v);
                for (int i = 0; i < r.EnemyGroups.Count; i++)
                {
                    EnemyGroup g = r.EnemyGroups[i];
                    float reach, speed, keep;
                    if (g.Kind == EnemyKind.Shooter) { reach = after ? ShooterValues.ShootsFrom : ShootsFromBefore; speed = s.Enemy.MoveSpeed; keep = after ? (s.KeepNear + s.KeepFar) / 2f : ShooterKeepBefore; }
                    else if (g.Kind == EnemyKind.Caster) { reach = after ? CasterValues.CastsFrom : CastsFromBefore; speed = c.Enemy.MoveSpeed; keep = after ? (c.KeepNear + c.KeepFar) / 2f : CasterKeepBefore; }
                    else if (g.Kind == EnemyKind.Warder) { reach = WarderValues.Bolt().MaxDistance; speed = w.Enemy.MoveSpeed; keep = (w.KeepNear + w.KeepFar) / 2f; }
                    else continue;
                    string name = (g.Elite ? "elite " : "") + EnemyKinds.Word(g.Kind);
                    if (!t.RangedOf.TryGetValue(name, out List<Ranged> list)) t.RangedOf[name] = list = new List<Ranged>();
                    foreach (LevelPoint p in r.PlacesAt(i))
                    {
                        double fromHer = p.DistanceTo(her);
                        double walk = Math.Max(0d, fromHer - reach);
                        // Where it first can shoot: where it stands, or on its way straight at her, as far from her as it reaches.
                        LevelPoint first = walk <= 0d ? p : new LevelPoint(her.X + (p.X - her.X) * reach / fromHer, her.Y + (p.Y - her.Y) * reach / fromHer);
                        double dx = first.X - her.X, dy = first.Y - her.Y; // the camera looks north: +Y ahead
                        bool on = dy <= ahead + 1e-9 && dy >= -behind - 1e-9 && Math.Abs(dx) <= halfWidth(dy) + 1e-9;
                        list.Add(new Ranged
                        {
                            FromDoor = LevelGeometry.DistanceToDoor(p, door, level.CellSize), FromHer = fromHer, Walk = walk, Seconds = walk / Math.Max(0.1f, speed),
                            ShootsFromDoor = LevelGeometry.DistanceToDoor(first, door, level.CellSize), WalksIn = Math.Max(0d, fromHer - keep),
                            AtOnce = walk <= 0d, OffScreen = !on,
                        });
                    }
                }
            }
            foreach (EnemyKind k in Kinds)
            {
                long n = onFloor.TryGetValue(k, out long x) ? x : 0;
                Add(t.FloorSum, k, n);
                Most(t.FloorMost, k, (int)n);
            }
        }

        static string KindsLine(Tally t, bool rooms)
        {
            var parts = new List<string>();
            foreach (EnemyKind k in Kinds)
            {
                long sum = (rooms ? t.RoomSum : t.FloorSum).TryGetValue(k, out long x) ? x : 0;
                int most = (rooms ? t.RoomMost : t.FloorMost).TryGetValue(k, out int m) ? m : 0;
                parts.Add(string.Format(Inv, "{0} {1:0.00}/{2}", EnemyKinds.Word(k), sum / (double)Math.Max(1, rooms ? t.Rooms : t.Floors), most));
            }
            return string.Join(", ", parts);
        }

        static string RangedLine(string name, List<Ranged> list)
        {
            if (list.Count == 0) return name + " none";
            return string.Format(Inv,
                "{0} {1}: from the door mean {2:0.0} m (least {3:0.0}, most {4:0.0}); from her at the point mean {5:0.0} m; can shoot at once {6:0%}; " +
                "else walks first mean {7:0.0} m ({8:0.0} s), all of them {9:0.0} m ({10:0.0} s); first shot from the door mean {11:0.0} m; walks in to its keep mean {12:0.0} m; " +
                "first shot from off screen {13:0%}",
                name, list.Count, list.Average(r => r.FromDoor), list.Min(r => r.FromDoor), list.Max(r => r.FromDoor), list.Average(r => r.FromHer),
                list.Count(r => r.AtOnce) / (double)list.Count,
                list.Where(r => !r.AtOnce).Select(r => r.Walk).DefaultIfEmpty(0).Average(), list.Where(r => !r.AtOnce).Select(r => r.Seconds).DefaultIfEmpty(0).Average(),
                list.Average(r => r.Walk), list.Average(r => r.Seconds), list.Average(r => r.ShootsFromDoor), list.Average(r => r.WalksIn),
                list.Count(r => r.OffScreen) / (double)list.Count);
        }

        /// <summary>
        /// A thousand floors of this number (as a run makes their seeds) with the plan off (version 17's floors, the distances of
        /// before) and on: no caster before floor 3, in a room or as the elite; no warder before 3, at most one a room; no bomber
        /// before 4; every cap; the count of a room the same in the mean (each warder and bomber takes a swarmer's place, a caster
        /// held back leaves the shooter or brute it would have taken the place of). Prints both tables and where the ranged
        /// enemies stand and first can shoot from when she crosses the room's point.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandFloors_BeforeAndAfter_ThePlanHolds_AndARoomsCountStays(int floor)
        {
            LevelValues before = new LevelValues { ActPlan = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, UsableRooms = false }, after = new LevelValues { BossAlone = false }; // work order 130 (changed deliberately): the plan against version 17, the boss room as it was before version 24 emptied it
            Tally tb = new Tally(), ta = new Tally();
            int same = 0, rooms = 0;
            for (int i = 0; i < Seeds; i++)
            {
                ulong seed = RunSeeds.FloorSeed(RunSeedOf(i), floor);
                LevelLayout lb = LevelGenerator.Generate(seed, floor, before), la = LevelGenerator.Generate(seed, floor, after);
                Count(lb, before, false, tb);
                Count(la, after, true, ta);
                List<LevelRoom> fb = lb.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                List<LevelRoom> fa = la.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                Assert.That(fa.Count, Is.EqualTo(fb.Count), "as many rooms with a fight, seed " + la.SeedText);
                LevelRoom first = LevelGeometry.MainPath(la).First(r => r.Kind == RoomKind.Combat);
                for (int k = 0; k < fa.Count; k++)
                {
                    LevelRoom r = fa[k];
                    string what = "seed " + la.SeedText + " floor " + floor + " " + r.Letter + r.Order;
                    int Of(EnemyKind kind, bool elites = true) => r.EnemyGroups.Where(g => g.Kind == kind && (elites || !g.Elite)).Sum(g => g.Count);
                    if (floor < after.CastersFromFloor) Assert.That(Of(EnemyKind.Caster), Is.EqualTo(0), what + ": no caster, in the room or as the elite");
                    Assert.That(Of(EnemyKind.Caster, false), Is.LessThanOrEqualTo(after.CastersMost(r.Kind, floor)), what + ": the casters' cap");
                    Assert.That(Of(EnemyKind.Warder), Is.LessThanOrEqualTo(floor < 3 ? 0 : 1), what + ": the warders");
                    Assert.That(Of(EnemyKind.Bomber), Is.LessThanOrEqualTo(LevelBombers.Most(after, r.Kind, floor, r == first)), what + ": the bombers");
                    if (floor < 4) Assert.That(Of(EnemyKind.Bomber), Is.EqualTo(0), what + ": no bomber before floor 4");
                    if (r == first) Assert.That(Of(EnemyKind.Warder) + Of(EnemyKind.Bomber), Is.EqualTo(0), what + ": neither in the floor's first combat room");
                    int countA = r.EnemyGroups.Where(g => !g.Elite && !g.Boss).Sum(g => g.Count), countB = fb[k].EnemyGroups.Where(g => !g.Elite && !g.Boss).Sum(g => g.Count);
                    rooms++;
                    if (countA == countB) same++;
                }
            }
            double meanB = tb.Enemies / (double)tb.Rooms, meanA = ta.Enemies / (double)ta.Rooms;
            var lines = new List<string>
            {
                string.Format(Inv, "ACTPLAN floor {0}, {1} floors (run seeds), {2} rooms with a fight", floor, Seeds, ta.Rooms),
                "ACTPLAN floor " + floor + " before (version 17) a room, mean/most: " + KindsLine(tb, true),
                "ACTPLAN floor " + floor + " after  (version 18) a room, mean/most: " + KindsLine(ta, true),
                "ACTPLAN floor " + floor + " before (version 17) a floor, mean/most: " + KindsLine(tb, false),
                "ACTPLAN floor " + floor + " after  (version 18) a floor, mean/most: " + KindsLine(ta, false),
                string.Format(Inv, "ACTPLAN floor {0} rooms holding a caster / a warder / a bomber: before {1:0%} / {2:0%} / {3:0%}, after {4:0%} / {5:0%} / {6:0%}",
                    floor, Share(tb, EnemyKind.Caster), Share(tb, EnemyKind.Warder), Share(tb, EnemyKind.Bomber),
                    Share(ta, EnemyKind.Caster), Share(ta, EnemyKind.Warder), Share(ta, EnemyKind.Bomber)),
                string.Format(Inv, "ACTPLAN floor {0} enemies a room (the elite and the boss apart): before mean {1:0.000} most {2}, after mean {3:0.000} most {4}; the same count in {5} of {6} rooms",
                    floor, meanB, tb.EnemiesMost, meanA, ta.EnemiesMost, same, rooms),
            };
            foreach (string name in tb.RangedOf.Keys.Union(ta.RangedOf.Keys).OrderBy(n => n))
            {
                lines.Add("ACTPLANRANGED floor " + floor + " before: " + RangedLine(name, tb.RangedOf.TryGetValue(name, out List<Ranged> b) ? b : new List<Ranged>()));
                lines.Add("ACTPLANRANGED floor " + floor + " after:  " + RangedLine(name, ta.RangedOf.TryGetValue(name, out List<Ranged> a) ? a : new List<Ranged>()));
            }
            TestContext.WriteLine(string.Join("\n", lines));

            Assert.That(meanA, Is.EqualTo(meanB).Within(0.01 * meanB), "a room's count the same in the mean");
            if (floor < 3) Assert.That(Share(ta, EnemyKind.Caster) + Share(ta, EnemyKind.Warder) + Share(ta, EnemyKind.Bomber), Is.EqualTo(0d));
            if (floor >= 3) Assert.That(Share(ta, EnemyKind.Caster), Is.GreaterThan(0d), "the caster from floor 3");
            if (floor >= 3) Assert.That(Share(ta, EnemyKind.Warder), Is.GreaterThan(0d), "the warder from floor 3");
            if (floor >= 4) Assert.That(Share(ta, EnemyKind.Bomber), Is.GreaterThan(0d), "the bomber from floor 4");
            if (floor == 3) Assert.That(Share(ta, EnemyKind.Bomber), Is.EqualTo(0d));
            if (floor == 2) Assert.That(Share(tb, EnemyKind.Caster), Is.GreaterThan(0d), "before, floor 2 had its casters");
        }

        static double Share(Tally t, EnemyKind k) => (t.RoomsWithLong.TryGetValue(k, out long n) ? n : 0) / (double)Math.Max(1, t.Rooms);

        // ---------------- The plan off is version 17's floors ----------------

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>
        /// Two thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the act's plan off, folded as
        /// MoreRoomTemplatesTests folds version 16's (<see cref="FloorPrint"/>, each room's shape and solid cells, and its template):
        /// version 17's default floors are version 16's, field for field (the second set of templates off), so with the plan off
        /// these floors fold to that test's numbers. Each runtime has its own (Unity's Math folds the doubles otherwise): Unity's
        /// is the pin of the filtered Unity EditMode run of 2026-11-22 (work order 072), and is checked by this order's Unity
        /// run; .NET's is what the .NET rules run of the code before this order (main 9a79acf) printed for those floors
        /// (MOREOFFPRINT).
        /// </summary>
        [Test]
        public void ThePlanOff_TwoThousandFloors_AreVersion17s_FieldForField()
        {
            var off = new LevelValues { ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 103: nor the exit rooms of their own
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, off);
                Assert.That(level.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Warder || g.Kind == EnemyKind.Bomber), Is.False, "seed " + level.SeedText);
                h = FloorPrint.Of(level, h);
                foreach (LevelRoom r in level.Rooms)
                {
                    h = Mix(h, (ulong)r.Shape);
                    h = Mix(h, (ulong)r.SolidCount);
                    foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                    foreach (char ch in r.Template?.ToString() ?? "-") h = Mix(h, ch);
                }
            }
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x5852E0964A18277EUL; // Unity's EditMode run of work order 072 (MoreRoomTemplatesTests' pin)
#else
            const ulong pinned = 0xC1B46D643FD6BB04UL; // .NET, the code before this order (9a79acf)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("ACTPLANOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the two thousand floors of version 17, found " + found);
        }
    }
}
