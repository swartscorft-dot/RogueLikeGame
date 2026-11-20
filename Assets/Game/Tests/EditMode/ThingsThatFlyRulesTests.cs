using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The things that fly (work order 036, 2026-11-20; decision 0010), as the rules see them: a projectile's step and its
    /// sweep (a fast arrow passes neither through her nor through a thin wall between two long steps), its end at its
    /// longest flight, what she ducked behind, the arrow's lead (standing, straight, capped), the fan's directions, which
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
            // Half a second a step at 24 m/s: 12 m. She stands 6 m ahead, inside the step.
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

        [Test]
        public void HerOrAWall_WhicheverComesFirstAlongTheStep()
        {
            var her = new GroundPoint(0f, 8f);
            Projectile behind = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref behind, 0.5f, her, her, Her, WallAt(10f)).Hit, Is.EqualTo(ProjectileHit.MetHer), "the wall is behind her");
            Projectile before = Arrow(Origin, North);
            Assert.That(ProjectileRules.Step(ref before, 0.5f, her, her, Her, WallAt(4f)).Hit, Is.EqualTo(ProjectileHit.Wall), "the wall is before her");
        }

        [Test]
        public void TheSweep_TakesHerMovingToo_AcrossItsPathWithinTheStep()
        {
            // She crosses its path between two frames: at neither frame's place is she in reach, but they meet in between.
            Projectile p = Arrow(Origin, North);
            ProjectileStep s = ProjectileRules.Step(ref p, 0.25f, new GroundPoint(-3f, 3f), new GroundPoint(3f, 3f), Her, null);
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
            var her = new GroundPoint(0f, 8f);
            Projectile p = Arrow(Origin, North);
            ProjectileStep s = ProjectileRules.Step(ref p, 0.5f, her, her, Her, null, coverEntry: 6.5f);
            Assert.That(s.Hit, Is.EqualTo(ProjectileHit.HerCover));
            Assert.That(s.WouldHaveMetHer, Is.True, "aimed at her: her cover stopped it");
            Assert.That(p.At.Z, Is.EqualTo(6.5f).Within(1e-3f));

            // A fan's side arrow that only grazes the thing she is behind, far from her.
            Projectile side = Arrow(Origin, ProjectileRules.FanDirection(North, 0, 3, 12f));
            ProjectileStep t = ProjectileRules.Step(ref side, 0.5f, her, her, Her, null, coverEntry: 6.5f);
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

        [Test]
        public void TheLead_RunningStraight_MeetsHerWhereSheGoes_At8And16Metres()
        {
            var v = new ShooterValues();
            foreach (float d in new[] { 8f, 16f })
            {
                // She runs across at her full run, 6 m/s, straight on.
                var her = new GroundPoint(0f, d);
                var run = new GroundPoint(6f, 0f);
                GroundPoint aim = ProjectileRules.LeadAim(Origin, her, run, v.ArrowSpeed, v.LeadShare, v.LeadMost);
                Projectile p = Arrow(Origin, aim);
                bool met = false;
                for (float t = 0f; t < 2f && !met; t += 0.02f)
                {
                    GroundPoint from = her + run * t, to = her + run * (t + 0.02f);
                    ProjectileStep s = ProjectileRules.Step(ref p, 0.02f, from, to, Her, null);
                    met = s.Hit == ProjectileHit.MetHer;
                    if (s.Hit == ProjectileHit.FlightOver) break;
                }
                Assert.That(met, Is.True, "running straight she is met at " + d + " m");

                // A turn back the moment it leaves: missed.
                Projectile q = Arrow(Origin, aim);
                bool hit = false;
                for (float t = 0f; t < 2f && !hit; t += 0.02f)
                {
                    GroundPoint from = her - run * t, to = her - run * (t + 0.02f);
                    ProjectileStep s = ProjectileRules.Step(ref q, 0.02f, from, to, Her, null);
                    hit = s.Hit == ProjectileHit.MetHer;
                    if (s.Hit == ProjectileHit.FlightOver) break;
                }
                Assert.That(hit, Is.False, "turning back after the release she is missed at " + d + " m");
            }
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
            Assert.That(v.FanFromFloor, Is.EqualTo(3), "floors 3 to 5");
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
            Assert.That(t.Peek(10f, 3, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Peek(10f, 3, false, v).Name, Is.EqualTo(ShooterValues.Arrow), "asking twice moves nothing");
            Assert.That(t.Shots, Is.EqualTo(0));
            Assert.That(t.Choose(30f, 3, false, v), Is.Null, "out of its band: nothing, and nothing counted");
            Assert.That(t.Shots, Is.EqualTo(0));
            Assert.That(t.Choose(10f, 3, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Choose(10f, 3, false, v).Name, Is.EqualTo(ShooterValues.Arrow));
            Assert.That(t.Choose(10f, 3, false, v).Name, Is.EqualTo(ShooterValues.ArrowFan), "the third on floor 3");
            Assert.That(t.Shots, Is.EqualTo(3));
            EnemyAttackDefinition arrow = CasterTactics.Find(v.Arrows, ShooterValues.Arrow), fan = CasterTactics.Find(v.Arrows, ShooterValues.ArrowFan);
            EnemyAttackDefinition shot = v.Enemy.Attacks[0];
            Assert.That((arrow.Damage, arrow.Windup, arrow.Recovery), Is.EqualTo((shot.Damage, shot.Windup, shot.Recovery)), "the arrow deals, draws and rests as the shot did");
            Assert.That(fan.Windup, Is.GreaterThan(arrow.Windup), "a fan's draw is a little longer");
            Assert.That(arrow.MaxDistance, Is.GreaterThanOrEqualTo(16f), "it shoots at 16 m");
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
