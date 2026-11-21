using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The things that fly (work order 036, 2026-11-20; decision 0010), as the rules see them: a projectile's step and its
    /// sweep (a fast arrow passes neither through her nor through a thin wall between two long steps), its end at its
    /// longest flight, what she ducked behind, the arrow's lead (work order 039's cases, capped), the fan's directions, which
    /// shot is a fan on which floor and for an elite, the caster's order with orbs, and the wave's size and reach. Walls
    /// are drawn here as lines across the ground. Every number is read from the values.
    /// </summary>
    public class ThingsThatFlyRulesTests
    {
        const float Her = Placeholders.PlayerRadius;
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);
        static readonly GroundPoint North = new GroundPoint(0f, 1f);

        /// <summary>A wall across the ground at z, of no thickness, from x = -<paramref name="half"/> to +<paramref name="half"/>.</summary>
        static CoverLine WallAt(float z, float half = 50f) => (from, to) =>
        {
            GroundPoint d = to - from;
            float length = d.Length;
            if (length < 1e-6f || Math.Abs(d.Z) < 1e-6f) return length;
            float t = (z - from.Z) / d.Z;
            if (t < 0f || t > 1f) return length;
            float x = from.X + d.X * t;
            return Math.Abs(x) <= half ? t * length : length;
        };

        static Projectile Arrow(GroundPoint from, GroundPoint dir) => ProjectileRules.Arrow(new ShooterValues(), from, dir);

        // ---------------- The step ----------------

        [Test]
        public void AStep_MovesItsSpeedTimesTheTime_AndCountsTheFlight()
        {
            Projectile p = Arrow(Origin, North);
            ProjectileStep s = ProjectileRules.Step(ref p, 0.1f, new GroundPoint(20f, 0f), new GroundPoint(20f, 0f), Her, null);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.None));
            Assert.That(p.At.Z, Is.EqualTo(new ShooterValues().ArrowSpeed * 0.1f).Within(1e-4f));
            Assert.That(p.Flown, Is.EqualTo(p.At.Z).Within(1e-4f));
            ProjectileStep none = ProjectileRules.Step(ref p, 0f, new GroundPoint(0f, 2.6f), new GroundPoint(0f, 2.6f), Her, null);
            Assert.That(none.Hit, Is.EqualTo(ProjectileHit.None), "no time, no step: a pause holds it");
            Assert.That(p.At.Z, Is.EqualTo(new ShooterValues().ArrowSpeed * 0.1f).Within(1e-4f));
        }

        [Test]
        public void ItEnds_AtItsLongestFlight_AndNotBefore()
        {
            var v = new ShooterValues();
            Projectile p = Arrow(Origin, North);
            GroundPoint far = new GroundPoint(100f, 0f);
            int steps = 0;
            ProjectileStep s;
            do
            {
                s = ProjectileRules.Step(ref p, 0.25f, far, far, Her, null);
                steps++;
            } while (s.Hit == ProjectileHit.None && steps < 100);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.FlightOver));
            Assert.That(p.Flown, Is.EqualTo(v.ArrowFlight).Within(1e-3f), "exactly its longest flight");
            Assert.That(p.At.Z, Is.EqualTo(v.ArrowFlight).Within(1e-3f));
            Assert.That(steps, Is.EqualTo((int)Math.Ceiling(v.ArrowFlight / (v.ArrowSpeed * 0.25f))));
        }

        [Test]
        public void AFastArrow_DoesNotPassThroughHer_BetweenTwoLongSteps()
        {
            // Half a second a step: 6.5 m at 13 m/s (12 m at 036's 24 m/s). She stands 6 m ahead, inside the step.
            Projectile p = Arrow(Origin, North);
            var her = new GroundPoint(0.6f, 6f);
            ProjectileStep s = ProjectileRules.Step(ref p, 0.5f, her, her, Her, null);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.MetHer));
            Assert.That(p.At.Z, Is.LessThan(6f), "it stopped where it met her, before her middle");
            Assert.That(new GroundPoint(p.At.X, p.At.Z).DistanceTo(her), Is.EqualTo(p.Radius + Her).Within(0.01f), "at the moment they first touched");

            Projectile wide = Arrow(Origin, North);
            var aside = new GroundPoint(p.Radius + Her + 0.05f, 6f);
            Assert.That(ProjectileRules.Step(ref wide, 0.5f, aside, aside, Her, null).Hit, Is.EqualTo(ProjectileHit.None), "a hair beside its reach, it passes");
        }

        [Test]
        public void AFastArrow_DoesNotPassThroughAThinWall_BetweenTwoLongSteps()
        {
            Projectile p = Arrow(Origin, North);
            var far = new GroundPoint(30f, 0f);
            ProjectileStep s = ProjectileRules.Step(ref p, 0.5f, far, far, Her, WallAt(5f));
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.Wall));
            Assert.That(p.At.Z, Is.EqualTo(5f).Within(1e-3f), "it stops at the wall");
        }

        /// <summary>
        /// Found in work order 039: a step whose end fell a hair (under a millimetre) past a wall's face was not stopped, and
        /// the next step, starting past the face, never met it. Steps are millimetres long at the frames of a test without a
        /// window, so an arrow now and then flew through a wall.
        /// </summary>
        [Test]
        public void AStepThatEndsAHairPastAWall_IsStoppedByIt()
        {
            var far = new GroundPoint(30f, 0f);
            foreach (float past in new[] { 0f, 0.0002f, 0.0005f, 0.0009f, 0.002f })
            {
                Projectile p = Arrow(Origin, North);
                ProjectileStep s = ProjectileRules.Step(ref p, (5f + past) / p.Speed, far, far, Her, WallAt(5f));
                if (s.Hit == ProjectileHit.None) s = ProjectileRules.Step(ref p, 0.004f / p.Speed, far, far, Her, WallAt(5f));
                Assert.That(s.Hit, Is.EqualTo(ProjectileHit.Wall), past + " m past");
                Assert.That(p.At.Z, Is.EqualTo(5f).Within(1e-3f));
            }
            Projectile before = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref before, 4.999f / before.Speed, far, far, Her, WallAt(5f)).Hit, Is.EqualTo(ProjectileHit.None), "short of it, on");
            Assert.That(ProjectileRules.Step(ref before, 0.004f / before.Speed, far, far, Her, WallAt(5f)).Hit, Is.EqualTo(ProjectileHit.Wall), "and the next step meets it");
        }

        [Test]
        public void HerOrAWall_WhicheverComesFirstAlongTheStep()
        {
            // One step of a second, long enough to reach her and both walls (work order 039: at 13 m/s half a second fell short of her).
            var her = new GroundPoint(0f, 8f);
            Projectile behind = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref behind, 1f, her, her, Her, WallAt(10f)).Hit, Is.EqualTo(ProjectileHit.MetHer), "the wall is behind her");
            Projectile before = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref before, 1f, her, her, Her, WallAt(4f)).Hit, Is.EqualTo(ProjectileHit.Wall), "the wall is before her");
        }

        [Test]
        public void TheSweep_TakesHerMovingToo_AcrossItsPathWithinTheStep()
        {
            // She crosses its path between two frames: at neither frame's place is she in reach, but they meet in between.
            // A step of 6 m, whatever its speed (work order 039 slowed it: a quarter second at 13 m/s no longer met her).
            Projectile p = Arrow(Origin, North);
            ProjectileStep s = ProjectileRules.Step(ref p, 6f / new ShooterValues().ArrowSpeed, new GroundPoint(-3f, 3f), new GroundPoint(3f, 3f), Her, null);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.MetHer));
            // Running away from it, faster than it on this step, she is not caught.
            Projectile q = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref q, 0.1f, new GroundPoint(0f, 4f), new GroundPoint(0f, 8f), Her, null).Hit, Is.EqualTo(ProjectileHit.None));
        }

        [Test]
        public void OnceItHasPassedHer_OrIsTold_ItMeetsHerNoMore()
        {
            var her = new GroundPoint(0f, 3f);
            Projectile p = Arrow(Origin, North);
            p.PassedHer = true;
            Assert.That(ProjectileRules.Step(ref p, 0.5f, her, her, Her, null).Hit, Is.EqualTo(ProjectileHit.None), "the dash let it pass: it flies on");
            Projectile q = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref q, 0.5f, her, her, Her, null, ignoreHer: true).Hit, Is.EqualTo(ProjectileHit.None));
        }

        [Test]
        public void WhatSheDuckedBehind_StopsIt_AndSaysWhetherItWouldHaveMetHer()
        {
            // Steps of a second, reaching past what she is behind (work order 039: at 13 m/s half a second just fell short of it).
            var her = new GroundPoint(0f, 8f);
            Projectile p = Arrow(Origin, North);
            ProjectileStep s = ProjectileRules.Step(ref p, 1f, her, her, Her, null, coverEntry: 6.5f);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.HerCover));
            Assert.That(s.WouldHaveMetHer, Is.True, "aimed at her: her cover stopped it");
            Assert.That(p.At.Z, Is.EqualTo(6.5f).Within(1e-3f));

            // A fan's side arrow that only grazes the thing she is behind, far from her.
            Projectile side = Arrow(Origin, ProjectileRules.FanDirection(North, 0, 3, 12f));
            ProjectileStep t = ProjectileRules.Step(ref side, 1f, her, her, Her, null, coverEntry: 6.5f);
            Assert.That(t.Hit, Is.EqualTo(ProjectileHit.HerCover));
            Assert.That(t.WouldHaveMetHer, Is.False, "it would have missed her: a prop stopped it, not her cover");
        }

        // ---------------- The lead ----------------

        [Test]
        public void TheLead_StandingStill_AimsAtHer()
        {
            var her = new GroundPoint(3f, 12f);
            GroundPoint aim = ProjectileRules.LeadAim(Origin, her, new GroundPoint(0f, 0f), 24f, 1f, 5f);
            Assert.That(aim.DistanceTo(her), Is.LessThan(1e-5f));
        }

        /// <summary>
        /// One arrow loosed from the origin at her, aimed with the lead of the values at her velocity at the release
        /// (<paramref name="atRelease"/>, or how <paramref name="her"/> goes from time 0), and flown in steps of a fiftieth of
        /// a second while she goes where <paramref name="her"/> says: did it meet her?
        /// </summary>
        static bool Meets(ShooterValues v, Func<float, GroundPoint> her, GroundPoint? atRelease = null)
        {
            GroundPoint at = her(0f), velocity = atRelease ?? (her(0.02f) - at) * 50f;
            GroundPoint aim = ProjectileRules.LeadAim(Origin, at, velocity, v.ArrowSpeed, v.LeadShare, v.LeadMost);
            Projectile p = Arrow(Origin, aim);
            for (float t = 0f; t < 4f; t += 0.02f)
            {
                ProjectileStep s = ProjectileRules.Step(ref p, 0.02f, her(t), her(t + 0.02f), Her, null);
                if (s.Hit == ProjectileHit.MetHer) return true;
                if (s.Hit == ProjectileHit.FlightOver) return false;
            }
            return false;
        }

        /// <summary>
        /// The lead, as work order 039 asks it (Kristian's second session of 2026-11-20: 38 of 40 arrows in the open landed;
        /// "the shot comes a little too fast"): at 8 m and 13 m, at her full run of 6 m/s, standing she is hit, running
        /// straight across she is missed behind, turning back after the release she is missed, running straight at it or
        /// away from it she is hit; and at 13 m a sidestep a quarter second after the release takes her out of its way.
        /// (036's lead met her running straight across; that is what this order took away.)
        /// </summary>
        [Test]
        public void TheLead_HitsHerStandingAndRunningAtOrAway_MissesHerRunningAcrossOrSidestepping()
        {
            var v = new ShooterValues();
            const float Run = 6f;
            foreach (float d in new[] { 8f, 13f })
            {
                Assert.That(Meets(v, t => new GroundPoint(0f, d)), Is.True, "standing she is hit at " + d + " m");
                Assert.That(Meets(v, t => new GroundPoint(Run * t, d)), Is.False, "running straight across she is missed at " + d + " m");
                Assert.That(Meets(v, t => new GroundPoint(-Run * t, d), new GroundPoint(Run, 0f)), Is.False, "turning back after the release she is missed at " + d + " m");
                Assert.That(Meets(v, t => new GroundPoint(0f, d - Run * t)), Is.True, "running straight at it she is hit at " + d + " m");
                Assert.That(Meets(v, t => new GroundPoint(0f, d + Run * t)), Is.True, "running straight away she is hit at " + d + " m");
                Assert.That(Meets(v, t => new GroundPoint(1f * t, d)), Is.True, "drifting at 1 m/s she is hit at " + d + " m");
            }
            Assert.That(Meets(v, t => new GroundPoint(t < 0.25f ? 0f : Run * (t - 0.25f), 12f)), Is.False,
                        "standing at 12 m, a sidestep 0.25 s after the release takes her out of its way");
            Assert.That(Meets(v, t => new GroundPoint(t < 0.25f ? 0f : Run * (t - 0.25f), 13f)), Is.False, "and at 13 m");
            // It is aimed a little ahead of her, never more than LeadMost.
            GroundPoint lead = ProjectileRules.LeadAim(Origin, new GroundPoint(0f, 12f), new GroundPoint(Run, 0f), v.ArrowSpeed, v.LeadShare, v.LeadMost);
            Assert.That(lead.X, Is.InRange(0.5f, v.LeadMost), "a little ahead of her");
        }

        /// <summary>Work order 039's arrow: about 13 m/s, a share of 0.35 of the lead up to 2.5 m, its hit size kept.</summary>
        [Test]
        public void TheArrow_Is13MetresASecond_ItsLeadPartial_ItsHitSizeKept()
        {
            var v = new ShooterValues();
            Assert.That(v.ArrowSpeed, Is.EqualTo(13f));
            Assert.That(v.LeadShare, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(v.LeadMost, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(v.ArrowRadius, Is.EqualTo(0.35f).Within(1e-4f), "its hit size as before");
            Assert.That(v.FanDegrees, Is.EqualTo(16f), "a fan to step through");
            Assert.That(12f / v.ArrowSpeed, Is.GreaterThan(0.9f), "almost a second in the air over 12 m");
            // Between two arrows of a fan, 10 m out, her middle has a band at least as wide as she is to pass in.
            float apart = 2f * 10f * (float)Math.Sin(v.FanDegrees * 0.5 * Math.PI / 180.0);
            Assert.That(apart - 2f * (v.ArrowRadius + Her), Is.GreaterThanOrEqualTo(2f * Her), "a gap she fits through at 10 m");
        }

        [Test]
        public void TheLead_IsCappedAt_LeadMost_AndScaledBy_LeadShare()
        {
            var v = new ShooterValues();
            var her = new GroundPoint(0f, 16f);
            var fast = new GroundPoint(20f, 0f); // far faster than she runs: the lead would be long
            GroundPoint aim = ProjectileRules.LeadAim(Origin, her, fast, v.ArrowSpeed, v.LeadShare, v.LeadMost);
            Assert.That(aim.DistanceTo(her), Is.EqualTo(v.LeadMost).Within(1e-4f), "no longer than LeadMost");
            var run = new GroundPoint(6f, 0f);
            float full = ProjectileRules.LeadAim(Origin, her, run, v.ArrowSpeed, 1f, 100f).DistanceTo(her);
            float half = ProjectileRules.LeadAim(Origin, her, run, v.ArrowSpeed, 0.5f, 100f).DistanceTo(her);
            Assert.That(half, Is.EqualTo(full * 0.5f).Within(1e-4f));
            Assert.That(ProjectileRules.LeadAim(Origin, her, run, v.ArrowSpeed, 0f, 100f).DistanceTo(her), Is.LessThan(1e-5f), "a share of 0: no lead");
            // The flight time is the true meeting time: at it the arrow is where she will be.
            float t = ProjectileRules.MeetingTime(her - Origin, run, v.ArrowSpeed);
            Assert.That((her + run * t).Length, Is.EqualTo(v.ArrowSpeed * t).Within(1e-3f));
        }

        // ---------------- Fans ----------------

        [Test]
        public void AFan_IsItsCount_ItsDegreesApart_TheMiddleAlongTheAim()
        {
            var v = new ShooterValues();
            var aim = new GroundPoint(1f, 2f);
            GroundPoint unit = aim * (1f / aim.Length);
            GroundPoint[] dirs = new GroundPoint[v.FanArrows];
            for (int i = 0; i < dirs.Length; i++) dirs[i] = ProjectileRules.FanDirection(aim, i, dirs.Length, v.FanDegrees);
            Assert.That(dirs.Length, Is.EqualTo(3));
            Assert.That(dirs[1].DistanceTo(unit), Is.LessThan(1e-5f), "the middle one aimed as one alone");
            for (int i = 0; i + 1 < dirs.Length; i++)
            {
                float angle = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, dirs[i].Dot(dirs[i + 1])))) * 180.0 / Math.PI);
                Assert.That(angle, Is.EqualTo(v.FanDegrees).Within(1e-2f));
                Assert.That(dirs[i].Length, Is.EqualTo(1f).Within(1e-5f));
            }
            Assert.That(ProjectileRules.FanDirection(aim, 0, 1, 30f).DistanceTo(unit), Is.LessThan(1e-5f), "one alone is the aim");
            var c = new CasterValues();
            GroundPoint left = ProjectileRules.FanDirection(North, 0, c.FanOrbs, c.FanDegrees), right = ProjectileRules.FanDirection(North, 2, c.FanOrbs, c.FanDegrees);
            Assert.That((float)(Math.Acos(left.Dot(right)) * 180.0 / Math.PI), Is.EqualTo(2f * c.FanDegrees).Within(1e-2f), "the caster's fan, 20 degrees apart");
        }

        [Test]
        public void WhichShotIsAFan_ByFloor_AndForAnElite()
        {
            var v = new ShooterValues();
            for (int floor = 0; floor <= 5; floor++)
            {
                for (int n = 1; n <= 9; n++)
                {
                    bool fan = floor >= v.FanFromFloor && n % v.FanEvery == 0;
                    Assert.That(ShooterTactics.NameOf(n, floor, false, v), Is.EqualTo(fan ? ShooterValues.ArrowFan : ShooterValues.Arrow), "floor " + floor + " shot " + n);
                    Assert.That(ShooterTactics.NameOf(n, floor, true, v), Is.EqualTo(n % v.FanEvery == 0 ? ShooterValues.ArrowFan : ShooterValues.Volley),
                                "an elite on floor " + floor + " shot " + n);
                }
            }
            Assert.That(v.FanFromFloor, Is.EqualTo(4), "floors 4 and 5 (work order 046; 3 to 5 before)");
            Assert.That(ShooterTactics.AtOnce(ShooterValues.ArrowFan, v), Is.EqualTo(3));
            Assert.That(ShooterTactics.AtOnce(ShooterValues.Volley, v), Is.EqualTo(1), "a volley's arrows leave one after the other");
            Assert.That(ShooterTactics.InAll(ShooterValues.Volley, v), Is.EqualTo(2));
            Assert.That(ShooterTactics.InAll(ShooterValues.Arrow, v), Is.EqualTo(1));
        }

        [Test]
        public void TheShooter_CountsTheShotsItBegins_AndAQuestionCountsNothing()
        {
            var v = new ShooterValues();
            var t = new ShooterTactics();
            Assert.That(t.Peek(10f, 4, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Peek(10f, 4, false, v).Name, Is.EqualTo(ShooterValues.Arrow), "asking twice moves nothing");
            Assert.That(t.Shots, Is.EqualTo(0));
            Assert.That(t.Choose(30f, 4, false, v), Is.Null, "out of its band: nothing, and nothing counted");
            Assert.That(t.Shots, Is.EqualTo(0));
            Assert.That(t.Choose(10f, 4, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Choose(10f, 4, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Choose(10f, 4, false, v).Name, Is.EqualTo(ShooterValues.ArrowFan), "the third on floor 4 (work order 046: fans from floor 4)");
            Assert.That(t.Shots, Is.EqualTo(3));
            EnemyAttackDefinition arrow = CasterTactics.Find(v.Arrows, ShooterValues.Arrow), fan = CasterTactics.Find(v.Arrows, ShooterValues.ArrowFan);
            EnemyAttackDefinition shot = v.Enemy.Attacks[0];
            Assert.That((arrow.Damage, arrow.Windup), Is.EqualTo((shot.Damage, shot.Windup)), "the arrow deals and draws as the shot did");
            // Work order 039: it rests 1.2 s, not the shot's 1.5 s (a moving player steps out of most arrows now), and its
            // band is the shot's 14 m again (036 had 17 m).
            foreach (EnemyAttackDefinition a in v.Arrows)
            {
                Assert.That(a.Recovery, Is.EqualTo(1.8f).Within(1e-4f), a.Name); // work order 046's flights: 1.8 s (1.2 before)
                Assert.That(a.MaxDistance, Is.EqualTo(shot.MaxDistance), a.Name + ": the shot's band");
            }
            Assert.That(shot.Recovery, Is.EqualTo(1.5f).Within(1e-4f), "the switch's off position as before");
            Assert.That(fan.Windup, Is.GreaterThan(arrow.Windup), "a fan's draw is a little longer");
            Assert.That(arrow.MaxDistance, Is.EqualTo(14f), "it shoots at 13 m, not at 16");
        }

        // ---------------- The caster ----------------

        [Test]
        public void TheCaster_KeepsItsOrder_WithOrbsInThePlaceOfItsCasts()
        {
            var v = new CasterValues();
            var off = new CasterTactics();
            var on = new CasterTactics();
            for (int i = 0; i < 9; i++)
            {
                string was = off.Choose(10f, v).Name;
                string now = on.Choose(10f, v, flies: true).Name;
                Assert.That(now, Is.EqualTo(was == CasterValues.GraveRain ? CasterValues.OrbFan : CasterValues.BoneOrb), "cast " + (i + 1));
            }
            Assert.That(on.Casts, Is.EqualTo(9));
            var peek = new CasterTactics();
            Assert.That(peek.Peek(10f, v, true).Name, Is.EqualTo(CasterValues.BoneOrb));
            Assert.That(peek.Casts, Is.EqualTo(0), "a question moves nothing");
            Assert.That(peek.Choose(2f, v, true), Is.Null, "its distances as before");

            EnemyAttackDefinition orb = v.Find(CasterValues.BoneOrb), fan = v.Find(CasterValues.OrbFan);
            EnemyAttackDefinition spikes = v.Find(CasterValues.BoneSpikes), rain = v.Find(CasterValues.GraveRain);
            Assert.That(PoolRules.IsCounterable(fan), Is.True, "the fan is blue and countered as Grave Rain is");
            Assert.That(PoolRules.IsCounterable(orb), Is.False);
            Assert.That((orb.MinDistance, orb.MaxDistance, orb.Windup, orb.Recovery), Is.EqualTo((spikes.MinDistance, spikes.MaxDistance, spikes.Windup, spikes.Recovery)));
            Assert.That((fan.MinDistance, fan.MaxDistance, fan.Windup, fan.Recovery), Is.EqualTo((rain.MinDistance, rain.MaxDistance, rain.Windup, rain.Recovery)));
            // Keeping it hard: standing still, a round of three casts deals at least what it did (only a fan's middle orb meets her).
            Assert.That(2 * orb.Damage + fan.Damage, Is.GreaterThanOrEqualTo(2 * spikes.Damage + v.RainCircles * rain.Damage));
            Assert.That(EnemyModelSpec.Caster.ClipForAttack(CasterValues.BoneOrb).Name, Is.EqualTo("Cast"));
            Assert.That(EnemyModelSpec.Caster.ClipForAttack(CasterValues.OrbFan).Name, Is.EqualTo("CastBig"));
            Assert.That(EnemyModelSpec.Shooter.ClipForAttack(ShooterValues.ArrowFan).Name, Is.EqualTo("Shoot"));
        }

        [Test]
        public void AnOrb_IsSlowAndLarge_AndFliesItsFlight()
        {
            var v = new CasterValues();
            Projectile p = ProjectileRules.Orb(v, Origin, North);
            Assert.That((p.Speed, p.Radius, p.MaxFlight, p.HalfWidth), Is.EqualTo((v.OrbSpeed, v.OrbRadius, v.OrbFlight, 0f)));
            var her = new GroundPoint(1.3f, 10f); // within its size and hers of its line
            bool met = false;
            for (int i = 0; i < 400 && !met; i++) met = ProjectileRules.Step(ref p, 0.02f, her, her, Her, null).Hit == ProjectileHit.MetHer;
            Assert.That(met, Is.True, "it is large: 1.3 m beside its line it still meets her");
        }

        // ---------------- The wave ----------------

        [Test]
        public void TheWave_IsAsWideAsItHits_AndGoesItsReach()
        {
            var v = new BossValues();
            float edge = v.WaveWidth * 0.5f;
            foreach ((float across, bool hits) in new[] { (0f, true), (edge - 0.1f, true), (edge + Her - 0.05f, true), (edge + Her + 0.1f, false) })
            {
                Projectile w = ProjectileRules.Wave(v, Origin, North);
                var her = new GroundPoint(across, 8f);
                bool met = false;
                for (int i = 0; i < 200 && !met; i++)
                {
                    ProjectileStep s = ProjectileRules.Step(ref w, 0.05f, her, her, Her, null);
                    met = s.Hit == ProjectileHit.MetHer;
                    if (s.Hit == ProjectileHit.FlightOver) break;
                }
                Assert.That(met, Is.EqualTo(hits), "her middle " + across + " m off its line");
            }
            Projectile far = ProjectileRules.Wave(v, Origin, North);
            var past = new GroundPoint(0f, v.WaveFlight + v.WaveDepth + Her + 0.2f);
            ProjectileStep last;
            int n = 0;
            do last = ProjectileRules.Step(ref far, 0.05f, past, past, Her, null);
            while (last.Hit == ProjectileHit.None && ++n < 400);
            Assert.That(last.Hit, Is.EqualTo(ProjectileHit.FlightOver), "past its reach she is safe");
            Assert.That(far.Flown, Is.EqualTo(v.WaveFlight).Within(1e-3f));
            Assert.That(far.Speed, Is.EqualTo(v.WaveSpeed));
            Assert.That(v.WaveAttack.Damage, Is.EqualTo(v.Enemy.Attacks[0].Damage / 2), "half the Cleave's");
        }

        [Test]
        public void AWallStopsThePartOfTheWaveBehindIt_TheRestGoesOn()
        {
            var v = new BossValues();
            // A short wall across the left of its way, 4 m out: she stands behind it, then on the open right.
            CoverLine wall = (from, to) =>
            {
                GroundPoint d = to - from;
                float length = d.Length;
                if (length < 1e-6f || Math.Abs(d.Z) < 1e-6f) return length;
                float t = (4f - from.Z) / d.Z;
                if (t < 0f || t > 1f) return length;
                float x = from.X + d.X * t;
                return x <= -0.5f && x >= -10f ? t * length : length;
            };
            foreach ((float across, bool hits) in new[] { (-2f, false), (2f, true) })
            {
                Projectile w = ProjectileRules.Wave(v, Origin, North);
                var her = new GroundPoint(across, 8f);
                bool met = false;
                for (int i = 0; i < 200 && !met; i++)
                {
                    ProjectileStep s = ProjectileRules.Step(ref w, 0.05f, her, her, Her, wall);
                    met = s.Hit == ProjectileHit.MetHer;
                    if (s.Hit != ProjectileHit.None && s.Hit != ProjectileHit.MetHer) break;
                }
                Assert.That(met, Is.EqualTo(hits), "her at " + across + " m");
            }
        }
    }
}
