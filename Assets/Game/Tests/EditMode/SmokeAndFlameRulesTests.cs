using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 126 (2026-11-25, her skills' smoke, flame and blast drawn as the game is drawn) as rules: the drawn cloud's puffs
    /// with their outer side on the radius and none past it; their low dome; smoke eaten away to nothing exactly at its end; the
    /// fire running out to its radius in the blast's time and never past it; embers held inside it; the named values quiet (no
    /// fire tone past the bloom's threshold) and inside their rules. No engine.
    /// </summary>
    public class SmokeAndFlameRulesTests
    {
        const float Metres = 1e-4f;
        static readonly SkillDefinition[] s_Kit = SkillDefaults.Create();

        [Test]
        public void TheDrawnCloud_ItsEdgePuffsOnTheRadius_TheRestInside_NoneReachingPastIt()
        {
            var v = new SkillLookValues();
            SkillDefinition smoke = s_Kit.Single(s => s.Name == "Smoke Round");
            foreach (float radius in new[] { smoke.Radius, 2f, 5f })
            {
                float edgeHalf = Math.Min(v.SmokeEdgeSize * 0.5f, radius * 0.5f), innerHalf = Math.Min(v.SmokeInnerSize * 0.5f, radius * 0.6f);
                float farthest = 0f;
                for (int i = 0; i < v.SmokePuffs; i++)
                {
                    GroundPoint at = SkillLookRules.CloudPuffAt(i, v.SmokePuffs, v.SmokeEdgePuffs, radius, edgeHalf, innerHalf, out float share);
                    bool edge = i < v.SmokeEdgePuffs;
                    float reach = SkillLookRules.Edge(at.Length, edge ? edgeHalf : innerHalf);
                    Assert.That(reach, Is.LessThanOrEqualTo(radius + Metres), "puff " + i + " inside the circle at radius " + radius);
                    if (edge) Assert.That(reach, Is.EqualTo(radius).Within(Metres), "puff " + i + " of the edge on the radius");
                    Assert.That(share, Is.InRange(0f, 1f));
                    farthest = Math.Max(farthest, reach);
                }
                Assert.That(farthest, Is.EqualTo(radius).Within(Metres));
            }
        }

        [Test]
        public void TheDrawnCloud_IsALowDome_HighestInItsMiddle()
        {
            Assert.That(SkillLookRules.CloudPuffHeight(0f, 0.3f, 1.15f), Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(SkillLookRules.CloudPuffHeight(1f, 0.3f, 1.15f), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(SkillLookRules.CloudPuffHeight(0.5f, 0.3f, 1.15f), Is.InRange(0.3f, 1.15f));
        }

        [Test]
        public void Smoke_IsEatenAway_FromNothingToAll_EndingExactlyAtItsTime()
        {
            const float until = 10f, seconds = 0.55f;
            Assert.That(SkillLookRules.Worn(5f, until, seconds), Is.Zero, "whole while it stands");
            Assert.That(SkillLookRules.Worn(until - seconds, until, seconds), Is.Zero);
            Assert.That(SkillLookRules.Worn(until - seconds * 0.5f, until, seconds), Is.EqualTo(0.25f).Within(1e-4f), "slowly at first, quickly at the end");
            Assert.That(SkillLookRules.Worn(until, until, seconds), Is.EqualTo(1f), "all of it at its end");
            float last = 0f;
            for (float t = until - seconds; t < until; t += 0.01f)
            {
                float w = SkillLookRules.Worn(t, until, seconds);
                Assert.That(w, Is.GreaterThanOrEqualTo(last - 1e-6f), "never grows back");
                last = w;
            }
        }

        [Test]
        public void TheFire_RunsOutToItsRadiusInTheBlastsTime_AndNeverPastIt()
        {
            var v = new SkillLookValues();
            SkillDefinition keg = s_Kit.Single(s => s.Name == "Powder Keg");
            float half = v.FireBlobSize * 0.5f;
            Assert.That(SkillLookRules.FireReach(keg.Radius, 1f, half, 0f), Is.Zero, "from its middle");
            Assert.That(SkillLookRules.Edge(SkillLookRules.FireReach(keg.Radius, 1f, half, 1f), half), Is.EqualTo(keg.Radius).Within(Metres), "on the radius at the blast's end");
            Assert.That(SkillLookRules.Edge(SkillLookRules.FireReach(keg.Radius, 0.6f, half, 1f), half), Is.EqualTo(keg.Radius * 0.6f).Within(Metres), "a smaller fire: its share");
            Assert.That(SkillLookRules.FireReach(keg.Radius, 1.5f, half, 2f), Is.EqualTo(keg.Radius - half).Within(Metres), "never past the radius, never after its time");
            float last = 0f;
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                float r = SkillLookRules.FireReach(keg.Radius, 1f, half, t);
                Assert.That(r, Is.GreaterThanOrEqualTo(last - 1e-6f), "out, never back");
                last = r;
            }
        }

        [Test]
        public void AnEmber_IsHeldBackByTheAir_NeverFartherThanItsSpeedOverItsDrag()
        {
            Assert.That(SkillLookRules.EmberOut(6f, 3f, 0f), Is.Zero);
            Assert.That(SkillLookRules.EmberOut(6f, 3f, 100f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(SkillLookRules.EmberOut(6f, 3f, 0.5f), Is.LessThan(2f));
        }

        [Test]
        public void TheDrawnLooksValues_AreQuiet_AndInsideTheirRules()
        {
            var v = new SkillLookValues();
            Assert.That(v.ToonLook, Is.True, "the drawn look is on by default");
            Assert.That(v.KegStarburst, Is.True, "the keg's starburst kept (he liked it)");
            foreach (Rgba c in new[] { v.FireCoreTone, v.FireYellow, v.FireOrange, v.FireRed })
                Assert.That(Math.Max(c.R, Math.Max(c.G, c.B)), Is.LessThanOrEqualTo(1f), "the fire under the bloom's threshold (1.2)");
            Assert.That(v.FireSize, Is.InRange(0f, 1f), "the fire never past the blast's radius");
            Assert.That(v.SmokeThickness, Is.InRange(0f, 1f));
            Assert.That(v.SmokeSwellSeconds + v.SmokeStagger + v.SmokeEatSeconds, Is.LessThan(s_Kit.Single(s => s.Name == "Smoke Round").SmokeSeconds), "the cloud whole for most of its time");
            Assert.That(v.LitBand, Is.GreaterThan(v.ShadeBand));
            Assert.That(v.SmokeEdgePuffs, Is.LessThanOrEqualTo(v.SmokePuffs));
            Assert.That(v.FireLight, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
