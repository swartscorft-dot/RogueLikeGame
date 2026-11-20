using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 042 (Kristian's answers of 2026-11-20), as rules: where a reward drop lands (on ground she can stand at,
    /// within reach of where the last enemy fell, never inside a wall or a prop, never behind one; from the room's middle
    /// when the enemy fell where she cannot stand); how it flies there; the guard on a choice (a key held from before does
    /// not pick, a fresh press after 0.35 s does, a fresh press before it does not); and the small bars' rules (when one
    /// shows, how many pips, the widths of the kinds, the draw calls).
    /// </summary>
    public class RewardDropAndBarRulesTests
    {
        static readonly RewardDropValues V = new RewardDropValues();

        /// <summary>A room of 12 by 10 m from (0, 0), and props as circles in it: the ground of the tests.</summary>
        sealed class Room
        {
            public float Width = 12f, Depth = 10f;
            public (float x, float z, float r)[] Props = Array.Empty<(float, float, float)>();

            public bool Standable(GroundPoint p)
            {
                if (p.X < V.Clearance || p.Z < V.Clearance || p.X > Width - V.Clearance || p.Z > Depth - V.Clearance) return false;
                foreach (var prop in Props)
                    if (new GroundPoint(prop.x, prop.z).DistanceTo(p) < prop.r + V.Clearance) return false;
                return true;
            }

            /// <summary>The straight way is open: it stays in the room and passes through no prop.</summary>
            public bool Open(GroundPoint a, GroundPoint b)
            {
                for (int i = 0; i <= 40; i++)
                {
                    GroundPoint p = a + (b - a) * (i / 40f);
                    if (p.X < 0f || p.Z < 0f || p.X > Width || p.Z > Depth) return false;
                    foreach (var prop in Props)
                        if (new GroundPoint(prop.x, prop.z).DistanceTo(p) < prop.r) return false;
                }
                return true;
            }

            public bool InsideAProp(GroundPoint p)
            {
                foreach (var prop in Props)
                    if (new GroundPoint(prop.x, prop.z).DistanceTo(p) < prop.r) return true;
                return false;
            }

            public GroundPoint Middle => new GroundPoint(Width * 0.5f, Depth * 0.5f);

            public GroundPoint Land(GroundPoint fell) => RewardDropRules.Landing(fell, Middle, Standable, Open, V);
        }

        [Test]
        public void ADrop_LandsOnOpenGround_WithinACoupleOfMetres_OfWhereTheEnemyFell()
        {
            var room = new Room();
            var fell = new GroundPoint(4f, 3f);
            GroundPoint at = room.Land(fell);
            Assert.That(room.Standable(at), Is.True, "on ground she can stand at");
            Assert.That(at.DistanceTo(fell), Is.LessThanOrEqualTo(V.MostLandDistance + 1e-3f), "within a couple of metres");
            Assert.That(at.DistanceTo(fell), Is.GreaterThan(0.5f), "it bursts out from the body, not on it");
            Assert.That(at.DistanceTo(room.Middle), Is.LessThan(fell.DistanceTo(room.Middle)), "inward, toward the room's middle");
        }

        [Test]
        public void ADrop_NeverLandsInAWall_OrAProp_OrBehindOne()
        {
            // A wall corner, and props all round the inward way.
            var room = new Room { Props = new[] { (2.5f, 2.5f, 0.8f), (1.2f, 3.4f, 0.5f), (3.4f, 1.2f, 0.5f) } };
            for (float x = 0.6f; x < 4f; x += 0.4f)
            for (float z = 0.6f; z < 4f; z += 0.4f)
            {
                var fell = new GroundPoint(x, z);
                if (!room.Standable(fell)) continue;
                GroundPoint at = room.Land(fell);
                Assert.That(room.Standable(at), Is.True, "standable, from " + fell);
                Assert.That(room.InsideAProp(at), Is.False, "never in a prop, from " + fell);
                Assert.That(at.X > 0f && at.Z > 0f && at.X < room.Width && at.Z < room.Depth, Is.True, "never in a wall, from " + fell);
                Assert.That(room.Open(fell, at), Is.True, "never behind a prop, from " + fell);
                Assert.That(at.DistanceTo(fell), Is.LessThanOrEqualTo(V.MostLandDistance + 1e-3f), "within reach of where it fell, from " + fell);
            }
        }

        [Test]
        public void AnEnemyThatFellWhereSheCannotStand_LeavesItsDrop_FromTheRoomsMiddle()
        {
            var room = new Room { Props = new[] { (9f, 7f, 1f) } };
            var inProp = new GroundPoint(9f, 7f);
            GroundPoint at = room.Land(inProp);
            Assert.That(room.Standable(at), Is.True);
            Assert.That(at.DistanceTo(room.Middle), Is.LessThanOrEqualTo(V.MostLandDistance + 1e-3f), "round the middle");

            var inWall = new GroundPoint(-0.3f, 5f);
            at = room.Land(inWall);
            Assert.That(room.Standable(at), Is.True);
            Assert.That(at.DistanceTo(room.Middle), Is.LessThanOrEqualTo(V.MostLandDistance + 1e-3f));
        }

        [Test]
        public void WithNowhereRoundIt_ADropLiesWhereItFell()
        {
            // A ring of props close round the spot: nothing at one to two and a half metres can be stood at.
            var props = new (float, float, float)[16];
            for (int i = 0; i < props.Length; i++)
            {
                double a = i * Math.PI * 2 / props.Length;
                props[i] = (6f + 1.6f * (float)Math.Cos(a), 5f + 1.6f * (float)Math.Sin(a), 0.9f);
            }
            var room = new Room { Props = props };
            var fell = new GroundPoint(6f, 5f);
            Assume.That(room.Standable(fell), Is.True);
            GroundPoint at = room.Land(fell);
            Assert.That(at.DistanceTo(fell), Is.LessThan(1e-4f), "on the spot, which she could stand at");
        }

        [Test]
        public void TheFlight_ArcsUp_Settles_AfterAbout08Seconds_AndThenFloats()
        {
            Assert.That(V.SettleSeconds, Is.EqualTo(0.8f).Within(0.1f), "about 0.8 s");
            DropPose start = RewardDropRules.Pose(0f, V);
            Assert.That(start.Along, Is.EqualTo(0f));
            Assert.That(start.Height, Is.EqualTo(V.BurstHeight).Within(1e-4f), "it bursts up from the body");
            DropPose top = RewardDropRules.Pose(V.ArcSeconds * 0.5f, V);
            Assert.That(top.Height, Is.GreaterThan(V.BurstHeight + V.ArcHeight * 0.5f), "it arcs high");
            Assert.That(top.Settled, Is.False);
            DropPose landed = RewardDropRules.Pose(V.ArcSeconds + 0.01f, V);
            Assert.That(landed.Along, Is.EqualTo(1f), "over where it lands from the end of the arc");
            Assert.That(landed.Settled, Is.False, "still bouncing");
            Assert.That(RewardDropRules.Pose(V.SettleSeconds - 0.01f, V).Settled, Is.False);
            for (float t = V.SettleSeconds; t < V.SettleSeconds + 5f; t += 0.13f)
            {
                DropPose p = RewardDropRules.Pose(t, V);
                Assert.That(p.Settled, Is.True, "lies still from " + V.SettleSeconds + " s");
                Assert.That(p.Height, Is.EqualTo(V.FloatHeight).Within(V.Bob + 1e-4f), "floats, bobbing");
                Assert.That(p.Scale, Is.InRange(0.89f, 1.11f), "and pulses");
            }
        }

        [Test]
        public void TheGuard_AKeyHeldFromBefore_DoesNotPick()
        {
            var guard = new ChoiceGuard();
            guard.Open(10f, V.GuardSeconds);
            Assert.That(V.GuardSeconds, Is.EqualTo(0.35f));
            // Down at 9.9, before the choice opened, and held: whatever the time now, it went down before.
            Assert.That(guard.MayPick(9.9f), Is.False);
            Assert.That(guard.Live(12f), Is.True, "the guard is long over");
            Assert.That(guard.MayPick(9.9f), Is.False, "still the same press: it never picks");
        }

        [Test]
        public void TheGuard_AFreshPressAfter035Seconds_Picks_AndOneBefore_DoesNot()
        {
            var guard = new ChoiceGuard();
            guard.Open(10f, V.GuardSeconds);
            Assert.That(guard.Live(10f), Is.False);
            Assert.That(guard.MayPick(10.01f), Is.False, "a press at once");
            Assert.That(guard.MayPick(10.34f), Is.False, "a press just before the guard ends");
            Assert.That(guard.Live(10.34f), Is.False);
            Assert.That(guard.MayPick(10.351f), Is.True, "a fresh press after it");
            Assert.That(guard.Live(10.351f), Is.True);
            // A press in the guard, held past it, is still a press from inside it.
            Assert.That(guard.MayPick(10.2f), Is.False);
            Assert.That(guard.Share(10f), Is.EqualTo(0f), "the cards dim when it opens");
            Assert.That(guard.Share(10.175f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(guard.Share(11f), Is.EqualTo(1f), "and lit after");

            // A new choice starts it again.
            guard.Open(20f, V.GuardSeconds);
            Assert.That(guard.MayPick(20.1f), Is.False);
            Assert.That(guard.MayPick(20.4f), Is.True);
        }

        [Test]
        public void TheBar_ShowsFromTheFirstHitOrMark_AndHidesAtFullHealthWithNone()
        {
            Assert.That(EnemyBarRules.Shows(true, 100, 100, 0, false, false), Is.False, "full health, no marks: hidden");
            Assert.That(EnemyBarRules.Shows(true, 99, 100, 0, false, false), Is.True, "from the first hit");
            Assert.That(EnemyBarRules.Shows(true, 100, 100, 1, false, false), Is.True, "from the first mark");
            Assert.That(EnemyBarRules.Shows(true, 100, 100, 0, true, false), Is.True, "a tag");
            Assert.That(EnemyBarRules.Shows(true, 100, 100, 0, false, true), Is.True, "stagger taken");
            Assert.That(EnemyBarRules.Shows(false, 0, 100, 3, false, false), Is.False, "never over the dead");
        }

        [Test]
        public void TheBar_ShowsAPipPerMark_AndTheCapAsEmptyPips()
        {
            Assert.That(EnemyBarRules.Pips(0, 5), Is.EqualTo(5));
            Assert.That(EnemyBarRules.LitPips(0, 5), Is.EqualTo(0));
            Assert.That(EnemyBarRules.Pips(2, 5), Is.EqualTo(5));
            Assert.That(EnemyBarRules.LitPips(2, 5), Is.EqualTo(2), "two lit, three empty");
            Assert.That(EnemyBarRules.LitPips(5, 5), Is.EqualTo(5), "all lit at the cap");
            Assert.That(EnemyBarRules.Pips(6, 5), Is.EqualTo(6), "never fewer pips than marks");
            var v = new EnemyBarValues();
            Assert.That(v.Pip, Is.InRange(4f, 6f), "about 5 pixels each");
            Assert.That(EnemyBarRules.PipsWidth(5, v), Is.EqualTo(5 * v.Pip + 4 * v.PipGap));
            Assert.That(EnemyBarRules.PipsWidth(0, v), Is.EqualTo(0f));
        }

        [Test]
        public void TheBar_IsAboutAsWideAsTheEnemy_AndAnElitesALittleWider()
        {
            var v = new EnemyBarValues();
            Assert.That(v.Thickness, Is.InRange(5f, 6f), "5 to 6 pixels thick");
            foreach (EnemyKind kind in new[] { EnemyKind.Brute, EnemyKind.Swarmer, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster })
            {
                float w = EnemyBarRules.Width(kind, false, v);
                Assert.That(w, Is.InRange(60f, 80f), kind + ": 60 to 80 pixels at 1080");
                Assert.That(EnemyBarRules.Width(kind, true, v), Is.GreaterThan(w).And.LessThanOrEqualTo(w + 20f), kind + ": an elite's a little wider");
            }
            Assert.That(EnemyBarRules.Width(EnemyKind.Swarmer, false, v), Is.LessThan(EnemyBarRules.Width(EnemyKind.Brute, false, v)), "a swarmer's narrower than a brute's");
            Assert.That(EnemyBarRules.StackHeight(true, v), Is.GreaterThan(EnemyBarRules.StackHeight(false, v)), "the stagger bar under it");
            Assert.That(v.On, Is.True, "on by default");
            Assert.That(v.BigMarks, Is.False, "the large marks off by default");
        }

        [Test]
        public void TheBar_CostsAFewDrawCalls()
        {
            Assert.That(EnemyBarRules.DrawCalls(false, false, 0, 0, false), Is.EqualTo(2), "hurt, unmarked: back and health");
            Assert.That(EnemyBarRules.DrawCalls(false, false, 2, 3, false), Is.EqualTo(4), "with marks: a call per row of pips");
            Assert.That(EnemyBarRules.DrawCalls(false, false, 5, 0, false), Is.EqualTo(3));
            Assert.That(EnemyBarRules.DrawCalls(true, true, 2, 3, true), Is.EqualTo(7), "the most: an elite with a stagger bar, marks and a tag");
        }

        [Test]
        public void TheDrops_AreOnByDefault()
        {
            Assert.That(new RewardDropValues().On, Is.True);
        }
    }
}
