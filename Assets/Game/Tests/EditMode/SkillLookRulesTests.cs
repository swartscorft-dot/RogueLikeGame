using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 123 (2026-11-25, her skills shown by what flies and burns, not by a flat shape on the ground) as rules: the switch on
    /// by default; the aim outline, before (every phase from the press to the last hit) and now (only while Long Shot's key is held
    /// to charge); a cone's tracers on its edges and inside it; a blast's ring that stops at its radius; the smoke's puffs that reach
    /// its radius and are drawn exactly while it stands; the throw's arc that ends where the round lands. No engine.
    /// </summary>
    public class SkillLookRulesTests
    {
        /// <summary>How near an effect's reach must be to its rule's, in metres (work order 123's named tolerance for the rules).</summary>
        const float Metres = 1e-4f;

        static readonly SkillDefinition[] s_Kit = SkillDefaults.Create();
        static readonly SkillPhase[] s_Phases = (SkillPhase[])Enum.GetValues(typeof(SkillPhase));

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(SkillLookValues.EffectsNotShapes, Is.True);
            Assert.That(SkillLookValues.Live, Is.Not.Null);
        }

        [Test]
        public void SwitchOff_TheOutline_ShowsAsBefore_FromThePressToTheLastHit_ForEverySkill()
        {
            foreach (SkillDefinition s in s_Kit)
                foreach (SkillPhase p in s_Phases)
                    Assert.That(SkillLookRules.OutlineShows(false, s.Input, p), Is.EqualTo(p != SkillPhase.None && p != SkillPhase.Recovery), s.Name + " " + p);
        }

        [Test]
        public void SwitchOn_TheOutline_ShowsOnlyWhileAChargeSkillsKeyIsHeld_NeverAtOrAfterFiring()
        {
            foreach (SkillDefinition s in s_Kit)
                foreach (SkillPhase p in s_Phases)
                {
                    bool held = s.Input == SkillInput.Charge && p == SkillPhase.Channel;
                    Assert.That(SkillLookRules.OutlineShows(true, s.Input, p), Is.EqualTo(held), s.Name + " " + p);
                }
            Assert.That(s_Kit.Where(s => s.Input == SkillInput.Charge).Select(s => s.Name), Is.EqualTo(new[] { "Long Shot" }), "the one skill held to aim before it fires");
        }

        [Test]
        public void ConeTracers_FiveOrMore_EvenlyFromEdgeToEdge_AsBefore()
        {
            foreach (float angle in new[] { 50f, 70f })
                for (int n = 5; n <= 9; n++)
                    for (int i = 0; i < n; i++)
                    {
                        float before = -angle * 0.5f + angle * i / (n - 1f); // Mathf.Lerp(-a/2, a/2, i/(n-1)), the old spread
                        Assert.That(SkillLookRules.ConeTracerYaw(i, n, angle, 0.123f, 0.8f), Is.EqualTo(before).Within(1e-4f));
                    }
        }

        [Test]
        public void ConeTracers_FewerThanFive_TheOuterTwoOnTheEdges_TheInnerInsideTheirOwnShare()
        {
            var rng = new System.Random(123);
            foreach (float angle in new[] { 50f, 70f })
                for (int n = 2; n <= 4; n++)
                    for (int round = 0; round < 200; round++)
                    {
                        float u = (float)rng.NextDouble();
                        Assert.That(SkillLookRules.ConeTracerYaw(0, n, angle, u, 0.8f), Is.EqualTo(-angle * 0.5f).Within(1e-4f), "the first on one edge");
                        Assert.That(SkillLookRules.ConeTracerYaw(n - 1, n, angle, u, 0.8f), Is.EqualTo(angle * 0.5f).Within(1e-4f), "the last on the other");
                        float slice = angle / (n - 1f);
                        for (int i = 1; i < n - 1; i++)
                        {
                            float yaw = SkillLookRules.ConeTracerYaw(i, n, angle, u, 0.8f), even = -angle * 0.5f + slice * i;
                            Assert.That(Math.Abs(yaw - even), Is.LessThanOrEqualTo(0.4f * slice + 1e-4f), "within its own share");
                            Assert.That(Math.Abs(yaw), Is.LessThanOrEqualTo(angle * 0.5f + 1e-4f), "inside the cone");
                        }
                    }
            for (int round = 0; round < 50; round++)
            {
                float yaw = SkillLookRules.ConeTracerYaw(0, 1, 50f, (float)rng.NextDouble(), 0.8f);
                Assert.That(Math.Abs(yaw), Is.LessThanOrEqualTo(25f + 1e-4f), "one alone: anywhere inside");
            }
        }

        [Test]
        public void BlastRing_RunsOutSlowing_ReachesItsReachAtItsArrival_AndNeverGoesPast()
        {
            foreach (float reach in new[] { 2.125f, 2.5f, 4f })
            {
                float last = -1f;
                for (int k = 0; k <= 1000; k++)
                {
                    float t = k / 1000f, r = SkillLookRules.RingReach(reach, t, 0.7f);
                    Assert.That(r, Is.GreaterThanOrEqualTo(last - 1e-6f), "outward only");
                    Assert.That(r, Is.LessThanOrEqualTo(reach + Metres), "never past its reach");
                    last = r;
                }
                Assert.That(SkillLookRules.RingReach(reach, 0f, 0.7f), Is.EqualTo(0f).Within(Metres), "from the middle");
                Assert.That(SkillLookRules.RingReach(reach, 0.7f, 0.7f), Is.EqualTo(reach).Within(Metres), "at the edge at its arrival");
                Assert.That(SkillLookRules.RingReach(reach, 1f, 0.7f), Is.EqualTo(reach).Within(Metres), "and held there");
                Assert.That(SkillLookRules.RingReach(reach, 0.35f, 0.7f) - SkillLookRules.RingReach(reach, 0f, 0.7f),
                            Is.GreaterThan(SkillLookRules.RingReach(reach, 0.7f, 0.7f) - SkillLookRules.RingReach(reach, 0.35f, 0.7f)), "slowing");
            }
        }

        [Test]
        public void Smoke_DrawnExactlyWhileTheCloudStands_FadingIn_AndFadingOutToNothingAtItsEnd()
        {
            const float from = 10f, until = 13f;
            Assert.That(SkillLookRules.SmokeShare(9.999f, from, until, 0.15f, 0.35f), Is.Zero, "not before it lands");
            Assert.That(SkillLookRules.SmokeShare(from, from, until, 0.15f, 0.35f), Is.Zero.Within(1e-6f), "coming up from nothing");
            Assert.That(SkillLookRules.SmokeShare(from + 0.075f, from, until, 0.15f, 0.35f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(SkillLookRules.SmokeShare(11.5f, from, until, 0.15f, 0.35f), Is.EqualTo(1f), "full while it stands");
            Assert.That(SkillLookRules.SmokeShare(until - 0.001f, from, until, 0.15f, 0.35f), Is.GreaterThan(0f), "still drawn the moment before it ends");
            Assert.That(SkillLookRules.SmokeShare(until, from, until, 0.15f, 0.35f), Is.Zero, "gone as it ends");
            Assert.That(SkillLookRules.SmokeShare(until + 1f, from, until, 0.15f, 0.35f), Is.Zero, "never after");
            var cloud = new SmokeCloud(new GroundPoint(0f, 0f), 3.5f, from, until);
            for (float now = 9f; now < 14f; now += 0.01f)
                Assert.That(SkillLookRules.SmokeShare(now, from, until, 0.15f, 0.35f) > 0f, Is.EqualTo(cloud.IsUp(now) && now > from), "drawn exactly while it is up, at " + now);
        }

        [Test]
        public void SmokePuffs_TheOuterRingOnTheRadius_NoneReachingPastIt()
        {
            SkillLookValues v = new SkillLookValues();
            SkillDefinition smoke = s_Kit.Single(s => s.Name == "Smoke Round");
            foreach (float radius in new[] { smoke.Radius, 2f, 5f })
            {
                float half = v.SmokePuffShare * radius * 0.5f, farthest = 0f;
                for (int i = 0; i < v.SmokePuffs; i++)
                {
                    GroundPoint at = SkillLookRules.SmokePuffAt(i, v.SmokePuffs, v.SmokeEdgePuffs, radius, half);
                    float edge = SkillLookRules.Edge(at.Length, half);
                    Assert.That(edge, Is.LessThanOrEqualTo(radius + Metres), "puff " + i + " inside the cloud's circle");
                    if (i < v.SmokeEdgePuffs) Assert.That(edge, Is.EqualTo(radius).Within(Metres), "puff " + i + " of the edge on the radius");
                    farthest = Math.Max(farthest, edge);
                }
                Assert.That(farthest, Is.EqualTo(radius).Within(Metres), "the smoke reaches exactly the rule's radius");
            }
        }

        [Test]
        public void TheThrow_RisesOverItsLine_AndEndsOnIt()
        {
            Assert.That(SkillLookRules.ThrowLift(0f, 1.4f), Is.Zero);
            Assert.That(SkillLookRules.ThrowLift(1f, 1.4f), Is.Zero, "it lands where the rule does");
            Assert.That(SkillLookRules.ThrowLift(0.5f, 1.4f), Is.EqualTo(1.4f).Within(1e-5f), "its peak at the middle");
            Assert.That(SkillLookRules.ThrowLift(2f, 1.4f), Is.Zero, "held at the end");
        }

        [Test]
        public void TheNamedValues_AreQuiet_AndInsideTheirRules()
        {
            var v = new SkillLookValues();
            Assert.That(v.SparkGlow, Is.LessThanOrEqualTo(1f), "her kit's extras glow under the bloom's threshold (1.2)");
            Assert.That(v.ArriveShare, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(v.SmokeEdgePuffs, Is.LessThanOrEqualTo(v.SmokePuffs));
            Assert.That(v.AfterSmokeReach, Is.LessThanOrEqualTo(1f), "the smoke after a blast stands inside its circle");
            Assert.That(v.SmokeFadeIn + v.SmokeFadeOut, Is.LessThan(s_Kit.Single(s => s.Name == "Smoke Round").SmokeSeconds), "the cloud is fully up for most of its time");
            Assert.That(v.MostPieces, Is.GreaterThanOrEqualTo(v.PoolStart));
        }
    }
}
