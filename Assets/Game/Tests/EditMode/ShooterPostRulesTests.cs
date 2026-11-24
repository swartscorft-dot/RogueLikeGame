using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Shooters that hold their place (work order 098; Kristian's choice of 2026-11-24, "Skyttere som holder plassen";
    /// <see cref="ShooterPostRules"/>, <see cref="ShooterValues.HoldsPlace"/>): beyond KeepFar it does not approach; inside its circle it
    /// steps as before; it never leaves the circle; it goes back to its post when she is out of its reach; with the switch off, or no
    /// post known, the choice is the one of before for the same inputs; the summary's line from the recorder's lines; and, over the
    /// floors, every shooter's post is joined to the fight's line by floor she can stand on, within her reach of it.
    /// </summary>
    public class ShooterPostRulesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly GroundPoint Post = new GroundPoint(10f, 20f);
        static readonly GroundPoint North = new GroundPoint(0f, 1f);

        static GroundPoint P(float x, float z) => new GroundPoint(x, z);

        /// <summary>The shooter's chase before work order 098 (EnemyShooter.Chase at 8de648b), line for line, with the ground for the scene.</summary>
        static ShooterMove Before(ShooterValues t, GroundPoint me, GroundPoint forward, GroundPoint her, bool sees, bool backingOff, bool approaching)
        {
            GroundPoint toPlayer = her - me;
            float distance = toPlayer.Length;
            if (backingOff)
            {
                GroundPoint away = distance > 1e-3f ? new GroundPoint(-toPlayer.X / distance, -toPlayer.Z / distance) : new GroundPoint(-forward.X, -forward.Z);
                GroundPoint to = me + away * (t.KeepNear - distance + 1f);
                return new ShooterMove { Go = ShooterGo.BackOff, To = to, Approaching = approaching };
            }
            if (distance > t.KeepFar) approaching = true;
            if (distance <= (t.KeepNear + t.KeepFar) * 0.5f) approaching = false;
            return new ShooterMove { Go = approaching || !sees ? ShooterGo.WalkToHer : ShooterGo.Stand, Approaching = approaching };
        }

        static ShooterMove Hold(ShooterValues t, GroundPoint me, GroundPoint her, bool sees = true, bool backingOff = false, bool approaching = false) =>
            ShooterPostRules.Choose(t, me, North, her, sees, backingOff, approaching, ShooterPostRules.Holds(t, true), Post);

        [Test]
        public void TheValues_AreTheOrdersAndTheShootersOwnAreUnchanged()
        {
            var t = new ShooterValues();
            Assert.That(t.HoldsPlace, Is.True, "on by default (Kristian, 2026-11-24)");
            Assert.That(t.HoldRadius, Is.EqualTo(3f), "the producer's starting value");
            Assert.That(t.KeepNear, Is.EqualTo(7f));
            Assert.That(t.KeepFar, Is.EqualTo(10f));
            Assert.That(t.BackOffUnder, Is.EqualTo(6f));
            Assert.That(ShooterValues.ShootsFrom, Is.EqualTo(11f));
            Assert.That(ShooterPostRules.Holds(t, true), Is.True);
            Assert.That(ShooterPostRules.Holds(t, false), Is.False, "no post known: as before");
            t.HoldsPlace = false;
            Assert.That(ShooterPostRules.Holds(t, true), Is.False, "the switch off: as before");
        }

        [Test]
        public void BeyondKeepFar_ItDoesNotApproach_ItStandsAtItsPost()
        {
            var t = new ShooterValues();
            foreach (float d in new[] { 10.5f, 11f, 12f, 20f, 40f })
            {
                GroundPoint her = Post + P(0f, -d);
                ShooterMove m = Hold(t, Post, her);
                Assert.That(m.Go, Is.EqualTo(ShooterGo.Stand), "at its post, she " + d + " m away and seen: it stands");
                Assert.That(m.Approaching, Is.False);
                Assert.That(Hold(t, Post, her, approaching: true).Go, Is.Not.EqualTo(ShooterGo.WalkToHer), "an approach flag left on does not walk it to her");
                if (d > ShooterValues.ShootsFrom)
                    Assert.That(Hold(t, Post, her, sees: false).Go, Is.EqualTo(ShooterGo.Stand), "out of its reach, unseen: it waits at its post, it does not come");
                Assert.That(Before(t, Post, North, her, true, false, false).Go, Is.EqualTo(ShooterGo.WalkToHer), "before, it walked to her from " + d + " m");
            }
        }

        [Test]
        public void InItsReach_UnseenItLooksForItsLine_SeenItStands()
        {
            var t = new ShooterValues();
            GroundPoint her = Post + P(0f, -9f);
            Assert.That(Hold(t, Post, her, sees: true).Go, Is.EqualTo(ShooterGo.Stand));
            Assert.That(Hold(t, Post, her, sees: false).Go, Is.EqualTo(ShooterGo.SeekLine), "it steps for its line, inside its circle");
            Assert.That(Hold(t, Post + P(2f, 0f), her, sees: false).Go, Is.EqualTo(ShooterGo.SeekLine), "anywhere inside the circle");
        }

        [Test]
        public void InsideTheCircle_ItBacksOffAsBefore()
        {
            // A circle wide enough that the back-off's end of before is inside it: the very same end.
            var wide = new ShooterValues { HoldRadius = 10f };
            foreach ((GroundPoint me, GroundPoint her) in new[] { (Post, Post + P(0f, -4f)), (Post + P(1f, 1f), Post + P(-2f, 3f)), (Post + P(-1f, 0f), Post + P(-1f, 0.0001f)) })
            {
                ShooterMove now = Hold(wide, me, her, backingOff: true);
                ShooterMove then = Before(wide, me, North, her, false, true, false);
                Assert.That(now.Go, Is.EqualTo(ShooterGo.BackOff));
                Assert.That(now.To.X, Is.EqualTo(then.To.X), "the same end as before");
                Assert.That(now.To.Z, Is.EqualTo(then.To.Z));
            }
            // At 3 m the same way, stopped at the edge.
            var t = new ShooterValues();
            ShooterMove cut = Hold(t, Post, Post + P(0f, -4f), backingOff: true);
            Assert.That(cut.Go, Is.EqualTo(ShooterGo.BackOff));
            Assert.That(cut.To.X, Is.EqualTo(Post.X).Within(1e-4f), "straight away from her");
            Assert.That(cut.To.Z - Post.Z, Is.EqualTo(3f).Within(1e-4f), "to the circle's edge, 3 m from its post (4 m before)");
            // Its back to the edge, going out: it does not move (and so it stands and shoots, as with its back to a wall).
            ShooterMove cornered = Hold(t, Post + P(0f, 3f), Post + P(0f, -1f), backingOff: true);
            Assert.That(cornered.To.DistanceTo(Post + P(0f, 3f)), Is.LessThan(ShooterPostRules.CorneredUnder), "under the cornered half metre");
        }

        [Test]
        public void ItNeverLeavesTheCircle_TheBackOffsAndTheSpots()
        {
            var t = new ShooterValues();
            var rnd = new Random(98);
            int backOffs = 0;
            for (int i = 0; i < 20000; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = Math.Sqrt(rnd.NextDouble()) * t.HoldRadius;
                GroundPoint me = Post + P((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
                GroundPoint her = Post + P((float)(rnd.NextDouble() * 30 - 15), (float)(rnd.NextDouble() * 30 - 15));
                bool backing = rnd.Next(2) == 0, sees = rnd.Next(2) == 0;
                ShooterMove m = Hold(t, me, her, sees, backing, rnd.Next(2) == 0);
                Assert.That(m.Go, Is.Not.EqualTo(ShooterGo.WalkToHer), "holding its place it never walks to her");
                if (m.Go != ShooterGo.BackOff) continue;
                backOffs++;
                Assert.That(m.To.DistanceTo(Post), Is.LessThanOrEqualTo(t.HoldRadius + 1e-3f), "a back-off ends inside the circle, from " + me + " she at " + her);
            }
            Assert.That(backOffs, Is.GreaterThan(5000));
            for (int i = 0; i < ShooterPostRules.SpotCount; i++)
                Assert.That(ShooterPostRules.Spot(i, Post, t.HoldRadius).DistanceTo(Post), Is.LessThanOrEqualTo(t.HoldRadius), "spot " + i);
            Assert.That(ShooterPostRules.Spot(0, Post, t.HoldRadius).DistanceTo(Post), Is.EqualTo(0f), "the post first");
            Assert.That(Enumerable.Range(0, ShooterPostRules.SpotCount).Select(i => ShooterPostRules.Spot(i, Post, t.HoldRadius).ToString()).Distinct().Count(),
                        Is.EqualTo(ShooterPostRules.SpotCount), "every spot its own");
            // The clip itself: inside stays, a way out ends on the edge, from outside it does not move.
            Assert.That(ShooterPostRules.Clip(Post, Post + P(1f, 0f), Post, 3f).X, Is.EqualTo(Post.X + 1f));
            Assert.That(ShooterPostRules.Clip(Post + P(-2f, 0f), Post + P(8f, 0f), Post, 3f).X, Is.EqualTo(Post.X + 3f).Within(1e-4f));
            GroundPoint outside = Post + P(5f, 0f);
            Assert.That(ShooterPostRules.Clip(outside, outside + P(2f, 0f), Post, 3f).X, Is.EqualTo(outside.X));
        }

        [Test]
        public void PutOutOfItsCircle_OrSheOutOfItsReach_ItGoesBackToItsPost()
        {
            var t = new ShooterValues();
            GroundPoint far = Post + P(0f, -15f), near = Post + P(0f, -8f);
            Assert.That(Hold(t, Post + P(2f, 0f), far).Go, Is.EqualTo(ShooterGo.ToPost), "she out of its reach: back to its post");
            Assert.That(Hold(t, Post + P(0.2f, 0f), far).Go, Is.EqualTo(ShooterGo.Stand), "at its post (within the 0.3 m of home): it stands");
            Assert.That(Hold(t, Post + P(2f, 0f), near).Go, Is.EqualTo(ShooterGo.Stand), "she in its reach and seen: it stays where it stepped to");
            GroundPoint pushed = Post + P(t.HoldRadius + ShooterPostRules.EdgeSlack + 0.5f, 0f);
            Assert.That(Hold(t, pushed, near).Go, Is.EqualTo(ShooterGo.ToPost), "pushed off: back");
            Assert.That(Hold(t, pushed, pushed + P(2f, 0f), backingOff: true).Go, Is.EqualTo(ShooterGo.ToPost), "pushed off, backing off: back first");
            Assert.That(Hold(t, Post + P(t.HoldRadius + 0.1f, 0f), near).Go, Is.EqualTo(ShooterGo.Stand), "an arrival a little past the edge is not leaving");
        }

        [Test]
        public void TheSwitchOff_OrNoPost_GivesTheChoiceOfBefore_ForTheSameInputs()
        {
            var off = new ShooterValues { HoldsPlace = false };
            var on = new ShooterValues();
            var rnd = new Random(1124);
            int compared = 0;
            for (int i = 0; i < 20000; i++)
            {
                GroundPoint me = P((float)(rnd.NextDouble() * 40), (float)(rnd.NextDouble() * 40));
                GroundPoint her = i % 50 == 0 ? me : P((float)(rnd.NextDouble() * 40), (float)(rnd.NextDouble() * 40));
                double fa = rnd.NextDouble() * Math.PI * 2;
                GroundPoint forward = P((float)Math.Cos(fa), (float)Math.Sin(fa));
                bool sees = rnd.Next(2) == 0, backing = rnd.Next(3) == 0, approaching = rnd.Next(2) == 0;
                ShooterMove then = Before(off, me, forward, her, sees, backing, approaching);
                foreach (ShooterMove now in new[]
                         {
                             ShooterPostRules.Choose(off, me, forward, her, sees, backing, approaching, ShooterPostRules.Holds(off, true), Post),
                             ShooterPostRules.Choose(on, me, forward, her, sees, backing, approaching, ShooterPostRules.Holds(on, false), Post),
                         })
                {
                    Assert.That(now.Go, Is.EqualTo(then.Go));
                    Assert.That(now.Approaching, Is.EqualTo(then.Approaching));
                    Assert.That(now.To.X, Is.EqualTo(then.To.X));
                    Assert.That(now.To.Z, Is.EqualTo(then.To.Z));
                    compared++;
                }
            }
            Assert.That(compared, Is.EqualTo(40000));
        }

        // ---------------- The summary's line ----------------

        static SessionEvent Line(double t, int floor, int order, double woke, bool held, string reach, string died)
        {
            var e = new SessionEvent(t, ShooterPostEvents.Post);
            e.AddExtra("floor", floor.ToString(Inv));
            e.AddExtra("order", order.ToString(Inv));
            e.AddExtra("woke", woke.ToString("0.000", Inv));
            e.AddExtra("held", held ? "1" : "0");
            e.AddExtra("reach", reach);
            e.AddExtra("died", died);
            return e;
        }

        [Test]
        public void TheSummary_HasOneLineBesideTheRoomUse_FromTheShootersLines()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                // Floor 1 room 3, woke at 10: two shooters, she reached the first after 4 s, both dead at 12 and 20 s.
                Line(22, 1, 3, 10, true, "4.00", "12.00"), Line(30, 1, 3, 10, true, "6.00", "20.00"),
                // Floor 1 room 5, woke at 40: one, reached after 8 s, alive when she died.
                Line(60, 1, 5, 40, true, "8.00", "-"),
                // Floor 2 room 2, woke at 100: one that held no post (its home off the NavMesh), never reached, killed by a blast at 30 s.
                Line(130, 2, 2, 100, false, "-", "30.00"),
                new SessionEvent(200, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events)
            {
                SessionEvent back = SessionEvent.Parse(e.ToJson());
                Assert.That(back.Type, Is.EqualTo(e.Type));
                Assert.That(back.ExtraValue("reach"), Is.EqualTo(e.ExtraValue("reach")));
            }
            ShooterPostStats s = SessionSummary.ComputeShooterPosts(events);
            Assert.That(s.Shooters, Is.EqualTo(4));
            Assert.That(s.Held, Is.EqualTo(3));
            Assert.That(s.Rooms, Is.EqualTo(3));
            Assert.That(s.RoomsReached, Is.EqualTo(2));
            Assert.That(s.ReachMedian, Is.EqualTo(6d).Within(1e-9), "rooms 3 (4 s, its first shooter) and 5 (8 s)");
            Assert.That(s.RoomsCleared, Is.EqualTo(2));
            Assert.That(s.LastDeathMedian, Is.EqualTo(25d).Within(1e-9), "rooms 3 (its last at 20 s) and floor 2's (30 s); room 5 was not cleared of its shooter");
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            TestContext.WriteLine(text);
            string line = text.Split('\n').Select(l => l.TrimEnd('\r')).Single(l => l.StartsWith("Shooters ", StringComparison.Ordinal));
            Assert.That(line, Is.EqualTo("Shooters 3 of 4 shooters held a post, in 3 rooms; from the room waking she came within a shooter's reach after a median of 6.0 s (2 rooms), "
                                         + "and the last shooter fell after a median of 25.0 s (2 rooms)"));
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != ShooterPostEvents.Post).ToList())), Does.Not.Contain("held a post"),
                        "nothing for a session without the lines");
        }

        // ---------------- She can reach every post ----------------

        /// <summary>
        /// Over floors 1 to 5 of forty seeds, every shooter the generator stood in a room with a fight (the elite's too): the floor she
        /// can stand on (<see cref="FightCover.FreeSpot"/>, on a 0.5 m grid, joined side to side from the fight's line) comes within
        /// 2 m of its post, so she can always get within her reach (10 m) of a shooter that never leaves its post's 3 m. And how many
        /// posts lie within the shooter's reach of the line's middle, where she stands as the room wakes (for the report).
        /// </summary>
        [Test]
        public void EveryPost_IsJoinedToTheFightsLine_ByFloorSheStandsOn()
        {
            var v = new LevelValues();
            const double step = 0.5;
            int posts = 0, inReachAtTheLine = 0;
            var nearest = new List<double>();
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    foreach (LevelRoom room in l.Rooms)
                    {
                        FightFrame f = FightCover.FrameOf(l, room, v);
                        if (f == null || !f.HasShooters) continue;
                        double x0 = room.Rect.X * l.CellSize, y0 = room.Rect.Y * l.CellSize;
                        int nx = (int)Math.Round(room.Rect.Width * l.CellSize / step), ny = (int)Math.Round(room.Rect.Height * l.CellSize / step);
                        var free = new bool[nx * ny];
                        int start = -1;
                        double startD = double.PositiveInfinity;
                        for (int j = 0; j < ny; j++)
                            for (int i = 0; i < nx; i++)
                            {
                                var c = new LevelPoint(x0 + (i + 0.5) * step, y0 + (j + 0.5) * step);
                                if (!FightCover.FreeSpot(l, room, c, v)) continue;
                                free[j * nx + i] = true;
                                double d = c.DistanceTo(f.LineMiddle);
                                if (d < startD)
                                {
                                    startD = d;
                                    start = j * nx + i;
                                }
                            }
                        Assert.That(start, Is.GreaterThanOrEqualTo(0), "free floor at the line, seed " + seed + " floor " + floor + " room " + room.Order);
                        var reached = new bool[nx * ny];
                        var queue = new Queue<int>();
                        reached[start] = true;
                        queue.Enqueue(start);
                        while (queue.Count > 0)
                        {
                            int k = queue.Dequeue(), i = k % nx, j = k / nx;
                            foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                int a = i + di, b = j + dj;
                                if (a < 0 || b < 0 || a >= nx || b >= ny) continue;
                                int n = b * nx + a;
                                if (!free[n] || reached[n]) continue;
                                reached[n] = true;
                                queue.Enqueue(n);
                            }
                        }
                        foreach (LevelPoint q in f.Shooters)
                        {
                            posts++;
                            if (q.DistanceTo(f.LineMiddle) <= ShooterValues.ShootsFrom) inReachAtTheLine++;
                            double best = double.PositiveInfinity;
                            for (int k = 0; k < nx * ny; k++)
                                if (reached[k]) best = Math.Min(best, new LevelPoint(x0 + (k % nx + 0.5) * step, y0 + (k / nx + 0.5) * step).DistanceTo(q));
                            nearest.Add(best);
                            Assert.That(best, Is.LessThanOrEqualTo(2.0), "floor joined to the line within 2 m of the shooter's post at " + q + ", seed " + seed + " floor " + floor + " room " + room.Order);
                        }
                    }
                }
            Assert.That(posts, Is.GreaterThan(50));
            nearest.Sort();
            TestContext.WriteLine(string.Format(Inv, "SHOOTERPOSTS {0} posts in rooms with a fight; the floor she stands on joined to the line comes within a median of {1:0.00} m of a post, {2:0.00} m at most; "
                                                     + "{3} posts ({4:0} %) within 11 m of the line's middle", posts, nearest[nearest.Count / 2], nearest[nearest.Count - 1],
                                                inReachAtTheLine, 100.0 * inReachAtTheLine / posts));
        }
    }
}
