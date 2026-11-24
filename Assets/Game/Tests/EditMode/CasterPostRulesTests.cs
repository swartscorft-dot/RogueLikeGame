using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Casters that hold their place (work order 101; Kristian's choice of 2026-11-24 afternoon, "Behold, og kasterne også";
    /// <see cref="ShooterPostRules"/> read with <see cref="PostValues.Of(CasterValues)"/>, <see cref="CasterValues.HoldsPlace"/>): beyond
    /// KeepFar it does not approach; inside its circle it steps as before; at the circle's edge it stops; back to its post when she is
    /// out of its reach; the switch off (or no post) gives the caster's chase of before for the same inputs, field for field. The one
    /// rule serves both: the shooter's choice is work order 098's for the same inputs. The summary's second line from the casters' own
    /// lines; and, over the floors, every caster's post is joined to the fight's line by floor she can stand on.
    /// </summary>
    public class CasterPostRulesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly GroundPoint Post = new GroundPoint(10f, 20f);
        static readonly GroundPoint North = new GroundPoint(0f, 1f);

        static GroundPoint P(float x, float z) => new GroundPoint(x, z);

        /// <summary>The caster's chase before work order 101 (EnemyCaster.Chase at d90d66d), line for line, with the ground for the scene.</summary>
        static ShooterMove Before(CasterValues t, GroundPoint me, GroundPoint forward, GroundPoint her, bool sees, bool backingOff, bool approaching)
        {
            GroundPoint toPlayer = her - me;
            float distance = toPlayer.Length;
            if (backingOff)
            {
                // Vector3 away = distance > 1e-3f ? -toPlayer / distance : -transform.forward; to = from + away * (KeepNear - distance + 1f).
                GroundPoint away = distance > 1e-3f ? new GroundPoint(-toPlayer.X / distance, -toPlayer.Z / distance) : new GroundPoint(-forward.X, -forward.Z);
                GroundPoint to = me + away * (t.KeepNear - distance + 1f);
                return new ShooterMove { Go = ShooterGo.BackOff, To = to, Approaching = approaching };
            }
            if (distance > t.KeepFar) approaching = true;
            if (distance <= (t.KeepNear + t.KeepFar) * 0.5f) approaching = false;
            // if (_approaching || !SeesHer()) Walk(Player.Position); else Halt();
            return new ShooterMove { Go = approaching || !sees ? ShooterGo.WalkToHer : ShooterGo.Stand, Approaching = approaching };
        }

        /// <summary>
        /// The shooter's choice of work order 098, copied as it stood at d90d66d (ShooterPostRules.Choose and AwayFrom), to show the one
        /// rule now serving both gives the shooter the very same choice.
        /// </summary>
        static ShooterMove Choose098(ShooterValues t, GroundPoint me, GroundPoint forward, GroundPoint her, bool seesHer, bool backingOff,
                                     bool approaching, bool holds, GroundPoint post)
        {
            GroundPoint toHer = her - me;
            float distance = toHer.Length;
            if (!holds)
            {
                if (backingOff) return new ShooterMove { Go = ShooterGo.BackOff, To = AwayFrom098(t, me, forward, toHer, distance), Approaching = approaching };
                if (distance > t.KeepFar) approaching = true;
                if (distance <= (t.KeepNear + t.KeepFar) * 0.5f) approaching = false;
                return new ShooterMove { Go = approaching || !seesHer ? ShooterGo.WalkToHer : ShooterGo.Stand, Approaching = approaching };
            }
            float radius = Math.Max(0f, t.HoldRadius);
            if (!ShooterPostRules.Inside(me, post, radius, ShooterPostRules.EdgeSlack)) return new ShooterMove { Go = ShooterGo.ToPost };
            if (backingOff) return new ShooterMove { Go = ShooterGo.BackOff, To = ShooterPostRules.Clip(me, AwayFrom098(t, me, forward, toHer, distance), post, radius) };
            if (distance > ShooterValues.ShootsFrom)
                return new ShooterMove { Go = me.DistanceTo(post) > ShooterPostRules.AtPost ? ShooterGo.ToPost : ShooterGo.Stand };
            return new ShooterMove { Go = seesHer ? ShooterGo.Stand : ShooterGo.SeekLine };
        }

        static GroundPoint AwayFrom098(ShooterValues t, GroundPoint me, GroundPoint forward, GroundPoint toHer, float distance)
        {
            GroundPoint away = distance > 1e-3f ? new GroundPoint(-toHer.X / distance, -toHer.Z / distance) : new GroundPoint(-forward.X, -forward.Z);
            return me + away * (t.KeepNear - distance + 1f);
        }

        static ShooterMove Hold(CasterValues t, GroundPoint me, GroundPoint her, bool sees = true, bool backingOff = false, bool approaching = false) =>
            ShooterPostRules.Choose(t, me, North, her, sees, backingOff, approaching, ShooterPostRules.Holds(t, true), Post);

        static void Same(ShooterMove now, ShooterMove then, string what)
        {
            Assert.That(now.Go, Is.EqualTo(then.Go), what);
            Assert.That(now.Approaching, Is.EqualTo(then.Approaching), what);
            Assert.That(now.To.X, Is.EqualTo(then.To.X), what);
            Assert.That(now.To.Z, Is.EqualTo(then.To.Z), what);
        }

        [Test]
        public void TheValues_AreTheOrdersAndTheCastersOwnAreUnchanged()
        {
            var t = new CasterValues();
            Assert.That(t.HoldsPlace, Is.True, "on by default (Kristian, 2026-11-24: \"Behold, og kasterne også\")");
            Assert.That(t.HoldRadius, Is.EqualTo(3f), "the producer's starting value, as the shooter's");
            Assert.That(t.KeepNear, Is.EqualTo(7.5f));
            Assert.That(t.KeepFar, Is.EqualTo(10f));
            Assert.That(t.BackOffUnder, Is.EqualTo(6f));
            Assert.That(CasterValues.CastsFrom, Is.EqualTo(11f));
            foreach (EnemyAttackDefinition a in t.Enemy.Attacks.Concat(t.Orbs))
            {
                Assert.That(a.MinDistance, Is.EqualTo(4f), a.Name);
                Assert.That(a.MaxDistance, Is.EqualTo(CasterValues.CastsFrom), a.Name);
            }
            PostValues p = PostValues.Of(t);
            Assert.That(p.KeepNear, Is.EqualTo(t.KeepNear));
            Assert.That(p.KeepFar, Is.EqualTo(t.KeepFar));
            Assert.That(p.HoldRadius, Is.EqualTo(t.HoldRadius));
            Assert.That(p.Reach, Is.EqualTo(CasterValues.CastsFrom), "its reach is its casts'");
            Assert.That(ShooterPostRules.Holds(t, true), Is.True);
            Assert.That(ShooterPostRules.Holds(t, false), Is.False, "no post known: as before");
            Assert.That(ShooterPostRules.Holds((CasterValues)null, true), Is.False);
            t.HoldsPlace = false;
            Assert.That(ShooterPostRules.Holds(t, true), Is.False, "the switch off: as before");
            // The shooter's own are not this order's.
            var s = new ShooterValues();
            Assert.That(s.HoldsPlace, Is.True);
            Assert.That(s.HoldRadius, Is.EqualTo(3f));
            Assert.That(PostValues.Of(s).Reach, Is.EqualTo(ShooterValues.ShootsFrom));
        }

        [Test]
        public void BeyondKeepFar_ItDoesNotApproach_ItStandsAtItsPost()
        {
            var t = new CasterValues();
            foreach (float d in new[] { 10.5f, 11f, 12f, 20f, 40f })
            {
                GroundPoint her = Post + P(0f, -d);
                ShooterMove m = Hold(t, Post, her);
                Assert.That(m.Go, Is.EqualTo(ShooterGo.Stand), "at its post, she " + d + " m away and seen: it stands");
                Assert.That(m.Approaching, Is.False);
                Assert.That(Hold(t, Post, her, approaching: true).Go, Is.Not.EqualTo(ShooterGo.WalkToHer), "an approach flag left on does not walk it to her");
                if (d > CasterValues.CastsFrom)
                    Assert.That(Hold(t, Post, her, sees: false).Go, Is.EqualTo(ShooterGo.Stand), "out of its reach, unseen: it waits at its post, it does not come");
                Assert.That(Before(t, Post, North, her, true, false, false).Go, Is.EqualTo(ShooterGo.WalkToHer), "before, it walked to her from " + d + " m");
            }
        }

        [Test]
        public void InItsReach_UnseenItLooksForItsSight_SeenItStands()
        {
            var t = new CasterValues();
            foreach (float d in new[] { 4f, 7f, 9f, 10.5f }) // from 2 m aside of its post too, within its 11 m
            {
                GroundPoint her = Post + P(0f, -d);
                Assert.That(Hold(t, Post, her, sees: true).Go, Is.EqualTo(ShooterGo.Stand), d + " m, seen: it stands (and casts by its own rules)");
                Assert.That(Hold(t, Post, her, sees: false).Go, Is.EqualTo(ShooterGo.SeekLine), d + " m, unseen: it steps for its sight, inside its circle");
                Assert.That(Hold(t, Post + P(2f, 0f), her, sees: false).Go, Is.EqualTo(ShooterGo.SeekLine), "anywhere inside the circle");
            }
        }

        [Test]
        public void InsideTheCircle_ItBacksOffAsBefore_AtTheEdgeItStops()
        {
            // A circle wide enough that the back-off's end of before is inside it: the very same end, with the caster's own KeepNear.
            var wide = new CasterValues { HoldRadius = 10f };
            foreach ((GroundPoint me, GroundPoint her) in new[] { (Post, Post + P(0f, -4f)), (Post + P(1f, 1f), Post + P(-2f, 3f)), (Post + P(-1f, 0f), Post + P(-1f, 0.0001f)) })
            {
                ShooterMove now = Hold(wide, me, her, backingOff: true);
                ShooterMove then = Before(wide, me, North, her, false, true, false);
                Assert.That(now.Go, Is.EqualTo(ShooterGo.BackOff));
                Assert.That(now.To.X, Is.EqualTo(then.To.X), "the same end as before");
                Assert.That(now.To.Z, Is.EqualTo(then.To.Z));
            }
            // She 4 m from it: before, 4.5 m straight away (to KeepNear 7.5 plus a metre ... from her); at 3 m it stops at the edge.
            var t = new CasterValues();
            ShooterMove then4 = Before(t, Post, North, Post + P(0f, -4f), false, true, false);
            Assert.That(then4.To.Z - Post.Z, Is.EqualTo(4.5f).Within(1e-4f), "before: 4.5 m away from her");
            ShooterMove cut = Hold(t, Post, Post + P(0f, -4f), backingOff: true);
            Assert.That(cut.Go, Is.EqualTo(ShooterGo.BackOff));
            Assert.That(cut.To.X, Is.EqualTo(Post.X).Within(1e-4f), "straight away from her");
            Assert.That(cut.To.Z - Post.Z, Is.EqualTo(3f).Within(1e-4f), "to the circle's edge, 3 m from its post");
            // Its back to the edge, going out: it does not move (and so it casts from where it stands, as with its back to a wall).
            ShooterMove cornered = Hold(t, Post + P(0f, 3f), Post + P(0f, -1f), backingOff: true);
            Assert.That(cornered.To.DistanceTo(Post + P(0f, 3f)), Is.LessThan(ShooterPostRules.CorneredUnder), "under the cornered half metre");
        }

        [Test]
        public void ItNeverLeavesTheCircle_TheBackOffsAndTheSpots()
        {
            var t = new CasterValues();
            var rnd = new Random(101);
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
        }

        [Test]
        public void PutOutOfItsCircle_OrSheOutOfItsReach_ItGoesBackToItsPost()
        {
            var t = new CasterValues();
            GroundPoint far = Post + P(0f, -15f), near = Post + P(0f, -8f);
            Assert.That(Hold(t, Post + P(2f, 0f), far).Go, Is.EqualTo(ShooterGo.ToPost), "she out of its reach: back to its post");
            Assert.That(Hold(t, Post + P(2f, 0f), Post + P(0f, -11.5f)).Go, Is.EqualTo(ShooterGo.ToPost), "just past the casts' 11 m: back");
            Assert.That(Hold(t, Post + P(0.2f, 0f), far).Go, Is.EqualTo(ShooterGo.Stand), "at its post (within the 0.3 m of home): it stands");
            Assert.That(Hold(t, Post + P(2f, 0f), near).Go, Is.EqualTo(ShooterGo.Stand), "she in its reach and seen: it stays where it stepped to");
            GroundPoint pushed = Post + P(t.HoldRadius + ShooterPostRules.EdgeSlack + 0.5f, 0f);
            Assert.That(Hold(t, pushed, near).Go, Is.EqualTo(ShooterGo.ToPost), "pushed off: back");
            Assert.That(Hold(t, pushed, pushed + P(2f, 0f), backingOff: true).Go, Is.EqualTo(ShooterGo.ToPost), "pushed off, backing off: back first");
            Assert.That(Hold(t, Post + P(t.HoldRadius + 0.1f, 0f), near).Go, Is.EqualTo(ShooterGo.Stand), "an arrival a little past the edge is not leaving");
        }

        [Test]
        public void TheSwitchOff_OrNoPost_GivesTheCastersChaseOfBefore_FieldForField()
        {
            var off = new CasterValues { HoldsPlace = false };
            var on = new CasterValues();
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
                Same(ShooterPostRules.Choose(off, me, forward, her, sees, backing, approaching, ShooterPostRules.Holds(off, true), Post), then, "the switch off");
                Same(ShooterPostRules.Choose(on, me, forward, her, sees, backing, approaching, ShooterPostRules.Holds(on, false), Post), then, "no post known");
                compared += 2;
            }
            Assert.That(compared, Is.EqualTo(40000));
        }

        [Test]
        public void TheShootersChoice_IsWorkOrder098s_ForTheSameInputs()
        {
            var rnd = new Random(98101);
            var values = new[] { new ShooterValues(), new ShooterValues { HoldsPlace = false }, new ShooterValues { HoldRadius = 10f }, new ShooterValues { HoldRadius = 0f } };
            int compared = 0;
            for (int i = 0; i < 40000; i++)
            {
                ShooterValues t = values[i % values.Length];
                GroundPoint post = P((float)(rnd.NextDouble() * 40), (float)(rnd.NextDouble() * 40));
                // Near its post mostly (so the circle's choices are taken), now and then far from it.
                double a = rnd.NextDouble() * Math.PI * 2, r = rnd.Next(5) == 0 ? rnd.NextDouble() * 10 : rnd.NextDouble() * 3.5;
                GroundPoint me = post + P((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
                GroundPoint her = i % 50 == 0 ? me : me + P((float)(rnd.NextDouble() * 32 - 16), (float)(rnd.NextDouble() * 32 - 16));
                double fa = rnd.NextDouble() * Math.PI * 2;
                GroundPoint forward = P((float)Math.Cos(fa), (float)Math.Sin(fa));
                bool sees = rnd.Next(2) == 0, backing = rnd.Next(3) == 0, approaching = rnd.Next(2) == 0, known = rnd.Next(4) != 0;
                bool holds = ShooterPostRules.Holds(t, known);
                ShooterMove then = Choose098(t, me, forward, her, sees, backing, approaching, holds, post);
                Same(ShooterPostRules.Choose(t, me, forward, her, sees, backing, approaching, holds, post), then, "the shooter's own overload, " + i);
                Same(ShooterPostRules.Choose(PostValues.Of(t), me, forward, her, sees, backing, approaching, ShooterPostRules.Holds(PostValues.Of(t), known), post), then,
                     "the shared values, " + i);
                compared++;
            }
            Assert.That(compared, Is.EqualTo(40000));
        }

        [Test]
        public void TheCaster_UsesItsOwnNumbers_NotTheShooters()
        {
            // The same place and the same her: the back-off of before ends by each one's KeepNear (7 m the shooter's, 7.5 m the caster's).
            var c = new CasterValues { HoldRadius = 10f };
            var s = new ShooterValues { HoldRadius = 10f };
            GroundPoint her = Post + P(0f, -4f);
            ShooterMove mc = ShooterPostRules.Choose(c, Post, North, her, false, true, false, true, Post);
            ShooterMove ms = ShooterPostRules.Choose(s, Post, North, her, false, true, false, true, Post);
            Assert.That(mc.To.Z - Post.Z, Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(ms.To.Z - Post.Z, Is.EqualTo(4f).Within(1e-4f));
            // A circle of its own: the caster's 1 m stops it where the shooter's 3 m does not.
            var tight = new CasterValues { HoldRadius = 1f };
            Assert.That(ShooterPostRules.Choose(tight, Post, North, her, false, true, false, true, Post).To.Z - Post.Z, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(ShooterPostRules.Choose(tight, Post + P(2f, 0f), North, Post + P(0f, -8f), true, false, false, true, Post).Go, Is.EqualTo(ShooterGo.ToPost),
                        "2 m from its post is out of a 1 m circle");
        }

        // ---------------- The summary's line ----------------

        static SessionEvent Line(string type, double t, int floor, int order, double woke, bool held, string reach, string died)
        {
            var e = new SessionEvent(t, type);
            e.AddExtra("floor", floor.ToString(Inv));
            e.AddExtra("order", order.ToString(Inv));
            e.AddExtra("woke", woke.ToString("0.000", Inv));
            e.AddExtra("held", held ? "1" : "0");
            e.AddExtra("reach", reach);
            e.AddExtra("died", died);
            return e;
        }

        [Test]
        public void TheSummary_HasTheCastersLine_BesideTheShooters_EachWithItsOwnCount()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                // Floor 3 room 3, woke at 10: a shooter and two casters; she reached the shooter after 4 s, the casters after 5 and 7 s.
                Line(ShooterPostEvents.Post, 22, 3, 3, 10, true, "4.00", "12.00"),
                Line(CasterPostEvents.Post, 25, 3, 3, 10, true, "5.00", "15.00"), Line(CasterPostEvents.Post, 31, 3, 3, 10, true, "7.00", "21.00"),
                // Floor 3 room 5 (the elite room), woke at 40: the elite caster, reached after 9 s, dead at 30 s.
                Line(CasterPostEvents.Post, 70, 3, 5, 40, true, "9.00", "30.00"),
                // Floor 4 room 2, woke at 100: one that held no post (its home off the NavMesh), never reached, alive when she died.
                Line(CasterPostEvents.Post, 130, 4, 2, 100, false, "-", "-"),
                new SessionEvent(200, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events)
            {
                SessionEvent back = SessionEvent.Parse(e.ToJson());
                Assert.That(back.Type, Is.EqualTo(e.Type));
                Assert.That(back.ExtraValue("died"), Is.EqualTo(e.ExtraValue("died")));
            }
            ShooterPostStats c = SessionSummary.ComputeCasterPosts(events);
            Assert.That(c.Shooters, Is.EqualTo(4), "four casters told");
            Assert.That(c.Held, Is.EqualTo(3));
            Assert.That(c.Rooms, Is.EqualTo(3));
            Assert.That(c.RoomsReached, Is.EqualTo(2));
            Assert.That(c.ReachMedian, Is.EqualTo(7d).Within(1e-9), "rooms 3 (5 s, its first caster) and 5 (9 s)");
            Assert.That(c.RoomsCleared, Is.EqualTo(2));
            Assert.That(c.LastDeathMedian, Is.EqualTo(25.5d).Within(1e-9), "rooms 3 (its last at 21 s) and 5 (30 s); floor 4's was not cleared");
            ShooterPostStats s = SessionSummary.ComputeShooterPosts(events);
            Assert.That(s.Shooters, Is.EqualTo(1), "the shooters' count is their own");
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            TestContext.WriteLine(text);
            string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            int shooters = Array.FindIndex(lines, l => l.StartsWith("Shooters ", StringComparison.Ordinal));
            int casters = Array.FindIndex(lines, l => l.StartsWith("Casters ", StringComparison.Ordinal));
            Assert.That(lines[shooters], Is.EqualTo("Shooters 1 of 1 shooter held a post, in 1 room; from the room waking she came within a shooter's reach after a median of 4.0 s (1 room), "
                                                   + "and the last shooter fell after a median of 12.0 s (1 room)"));
            Assert.That(lines[casters], Is.EqualTo("Casters 3 of 4 casters held a post, in 3 rooms; from the room waking she came within a caster's reach after a median of 7.0 s (2 rooms), "
                                                  + "and the last caster fell after a median of 25.5 s (2 rooms)"));
            Assert.That(casters, Is.EqualTo(shooters + 1), "the casters' line beside the shooters'");
            string onlyShooters = SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != CasterPostEvents.Post).ToList()));
            Assert.That(onlyShooters, Does.Not.Contain("Casters "), "nothing for a session without the casters' lines");
            string onlyCasters = SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != ShooterPostEvents.Post).ToList()));
            Assert.That(onlyCasters, Does.Contain("Casters 3 of 4 casters held a post"));
            Assert.That(onlyCasters, Does.Not.Contain("Shooters "), "the casters' line stands without the shooters'");
        }

        // ---------------- She can reach every post ----------------

        /// <summary>
        /// Over floors 3 to 5 of forty seeds (casters from floor 3), every caster the generator stood in a room with a fight (the elite's
        /// too): the floor she can stand on (<see cref="FightCover.FreeSpot"/>, on a 0.5 m grid, joined side to side from the fight's
        /// line) comes within 2 m of its post, so she can always get within her reach (10 m) of a caster that never leaves its post's
        /// 3 m. And how many posts lie within the casts' reach of the line's middle, where she stands as the room wakes (for the report).
        /// </summary>
        [Test]
        public void EveryCastersPost_IsJoinedToTheFightsLine_ByFloorSheStandsOn()
        {
            var v = new LevelValues();
            const double step = 0.5;
            int posts = 0, elites = 0, inReachAtTheLine = 0;
            var nearest = new List<double>();
            var fromLine = new List<double>();
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 3; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    foreach (LevelRoom room in l.Rooms)
                    {
                        FightFrame f = FightCover.FrameOf(l, room, v);
                        if (f == null) continue;
                        var mine = new List<(LevelPoint at, bool elite)>();
                        for (int i = 0; i < room.EnemyPoints.Count; i++)
                        {
                            EnemyGroup g = room.GroupAt(i);
                            if (g.Kind != EnemyKind.Caster) continue;
                            foreach (LevelPoint m in room.PlacesAt(i)) mine.Add((m, g.Elite));
                        }
                        if (mine.Count == 0) continue;
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
                        foreach ((LevelPoint q, bool elite) in mine)
                        {
                            posts++;
                            if (elite) elites++;
                            double line = q.DistanceTo(f.LineMiddle);
                            fromLine.Add(line);
                            if (line <= CasterValues.CastsFrom) inReachAtTheLine++;
                            double best = double.PositiveInfinity;
                            for (int k = 0; k < nx * ny; k++)
                                if (reached[k]) best = Math.Min(best, new LevelPoint(x0 + (k % nx + 0.5) * step, y0 + (k / nx + 0.5) * step).DistanceTo(q));
                            nearest.Add(best);
                            Assert.That(best, Is.LessThanOrEqualTo(2.0), "floor joined to the line within 2 m of the caster's post at " + q + (elite ? " (the elite)" : "")
                                                                         + ", seed " + seed + " floor " + floor + " room " + room.Order);
                        }
                    }
                }
            Assert.That(posts, Is.GreaterThan(30));
            Assert.That(elites, Is.GreaterThan(0), "elite casters among them");
            nearest.Sort();
            fromLine.Sort();
            TestContext.WriteLine(string.Format(Inv, "CASTERPOSTS {0} caster posts in rooms with a fight ({1} elites); the floor she stands on joined to the line comes within a median "
                                                     + "of {2:0.00} m of a post, {3:0.00} m at most; {4} posts ({5:0} %) within 11 m of the line's middle; from the line's middle a median "
                                                     + "of {6:0.0} m, {7:0.0} to {8:0.0} m", posts, elites, nearest[nearest.Count / 2], nearest[nearest.Count - 1],
                                                inReachAtTheLine, 100.0 * inReachAtTheLine / posts, fromLine[fromLine.Count / 2], fromLine[0], fromLine[fromLine.Count - 1]));
        }
    }
}
