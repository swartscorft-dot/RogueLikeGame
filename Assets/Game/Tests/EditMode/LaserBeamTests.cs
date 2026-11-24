using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// The Laser Sight's beam (work order 102), its rules: the producer's starts (a sight's red, 0.012 m at the muzzle, gone 4 m
    /// out, the dot off); the fade along it (strongest at the muzzle, nothing at the end); a narrower than a pixel beam drawn a
    /// pixel wide and that much dimmer; the fog's share (full low by the line it is laid along, gone far from it and high up);
    /// the beam brighter in fog than in clear air and never over the bloom's threshold; the wisps uneven and slow; the motes
    /// lit only a share of their time, never everywhere at once; and the ray against an enemy's upright body.
    /// </summary>
    public class LaserBeamTests
    {
        /// <summary>The post-processing's bloom threshold (Assets/Game/Settings/PostProcessing.asset): the beam stays under it.</summary>
        const float BloomThreshold = 1.2f;
        /// <summary>The motes the drawing has room for on a beam (LaserSight.MostSpecks, in the engine's part).</summary>
        const int LaserSightMostSpecks = 12;

        [Test]
        public void TheStarts_ARealSightsRed_Thin_Short_TheDotOff()
        {
            var v = new LaserBeamValues();
            Assert.That((v.Colour.R, v.Colour.G, v.Colour.B), Is.EqualTo((1f, 0.08f, 0.06f)), "a real sight's red, not her orange");
            Assert.That(v.Width, Is.EqualTo(0.012f), "about 0.012 m across at the muzzle");
            Assert.That(v.EndWidth, Is.GreaterThanOrEqualTo(v.Width).And.LessThan(0.03f), "widening very slightly, never a band");
            Assert.That(v.Length, Is.EqualTo(4f), "fully gone 4 m out");
            Assert.That(v.DotOn, Is.False, "the dot is built and off");
            Assert.That(v.Specks, Is.InRange(1, LaserSightMostSpecks), "a few motes");
        }

        [Test]
        public void TheFade_IsFullAtTheMuzzle_FallsAllTheWay_AndIsNothingAtTheEnd()
        {
            var v = new LaserBeamValues();
            Assert.That(LaserBeam.Fade(0f, v), Is.EqualTo(1f));
            Assert.That(LaserBeam.Fade(1f, v), Is.EqualTo(0f));
            Assert.That(LaserBeam.Fade(1.5f, v), Is.EqualTo(0f), "past the end");
            float last = 1f;
            for (int i = 1; i <= 20; i++)
            {
                float f = LaserBeam.Fade(i / 20f, v);
                Assert.That(f, Is.LessThan(last), "it falls all along");
                last = f;
            }
            v.FadePower = 1f;
            Assert.That(LaserBeam.Fade(0.5f, v), Is.EqualTo(0.5f).Within(1e-6f), "1: a straight fade");
        }

        [Test]
        public void ABeamThinnerThanAPixel_IsDrawnThatWide_AndThatMuchDimmer()
        {
            var v = new LaserBeamValues();
            float drawn = LaserBeam.Drawn(0.012f, 0.0139f, v, out float share); // the game's camera: 28 m away, 30 degrees, 1080 rows
            Assert.That(drawn, Is.EqualTo(v.LeastPixels * 0.0139f).Within(1e-6f));
            Assert.That(drawn * share, Is.EqualTo(0.012f).Within(1e-6f), "the same light in all");
            drawn = LaserBeam.Drawn(0.012f, 0.001f, v, out share); // close up
            Assert.That(drawn, Is.EqualTo(0.012f));
            Assert.That(share, Is.EqualTo(1f));
            Assert.That(LaserBeam.WidthAt(0f, v), Is.EqualTo(v.Width));
            Assert.That(LaserBeam.WidthAt(1f, v), Is.EqualTo(v.EndWidth));
        }

        [Test]
        public void TheFog_IsFullLowByItsLine_GoneFarFromIt_AndGoneHighUp()
        {
            var v = new LaserBeamValues();
            Assert.That(LaserBeam.FogAt(0f, 0.3f, v), Is.EqualTo(1f));
            Assert.That(LaserBeam.FogAt(-0.5f, 0.2f, v), Is.EqualTo(1f), "either side of the line");
            Assert.That(LaserBeam.FogAt(v.FogReach, 0.3f, v), Is.EqualTo(0f), "far from the line");
            Assert.That(LaserBeam.FogAt(0f, v.FogTop, v), Is.EqualTo(0f), "high up");
            float atGun = LaserBeam.FogAt(0f, 1.25f, v);
            Assert.That(atGun, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.6f), "thinner at the gun's height");
            // The edge of a 10 by 6 m rectangle round (0, 0).
            Assert.That(LaserBeam.FromEdge(0f, 0f, 0f, 0f, 5f, 3f), Is.EqualTo(3f), "the middle: 3 m to the nearer side");
            Assert.That(LaserBeam.FromEdge(4.5f, 0f, 0f, 0f, 5f, 3f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(LaserBeam.FromEdge(6f, 0f, 0f, 0f, 5f, 3f), Is.EqualTo(1f).Within(1e-6f), "outside");
        }

        [Test]
        public void TheBeam_IsBrighterInFogThanInClearAir_AndNeverOverTheBloom()
        {
            var v = new LaserBeamValues();
            for (float w = 0f; w <= 1f; w += 0.25f)
            {
                Assert.That(LaserBeam.Light(0f, 1f, w, v), Is.GreaterThan(LaserBeam.Light(0f, 0f, w, v)), "fog shows it");
                Assert.That(LaserBeam.Light(0f, 1f, w, v), Is.LessThan(BloomThreshold), "under the bloom at its brightest");
                Assert.That(LaserBeam.Light(0f, 0f, w, v), Is.GreaterThan(0f).And.LessThan(0.5f), "faint in clear air");
                Assert.That(LaserBeam.Light(1f, 1f, w, v), Is.EqualTo(0f), "nothing at the end");
            }
            Assert.That(v.SpeckGlow, Is.LessThan(BloomThreshold));
            Assert.That(v.DotGlow, Is.LessThan(BloomThreshold));
            Assert.That(LaserBeam.DotLight(1f, v), Is.LessThan(LaserBeam.DotLight(0f, v)), "a dot farther out is weaker");
            Assert.That(LaserBeam.DotLight(1.01f, v), Is.EqualTo(0f), "and none past the beam's length");
        }

        [Test]
        public void TheWisps_AreUneven_InTheAir_AndSlow()
        {
            var v = new LaserBeamValues();
            float least = 1f, most = 0f;
            for (int i = 0; i < 200; i++)
            {
                float w = LaserBeam.Wisp(i * 0.1f, 1.2f, i * 0.05f, 0f, v);
                Assert.That(w, Is.InRange(0f, 1f));
                least = System.Math.Min(least, w);
                most = System.Math.Max(most, w);
            }
            Assert.That(most - least, Is.GreaterThan(0.4f), "uneven along 20 m of air");
            float now = LaserBeam.Wisp(1f, 1.2f, 2f, 10f, v), frame = LaserBeam.Wisp(1f, 1.2f, 2f, 10f + 1f / 60f, v);
            Assert.That(System.Math.Abs(frame - now), Is.LessThan(0.02f), "slow: a frame changes it by little");
        }

        [Test]
        public void TheMotes_AreLitOnlyAShareOfTheirTime_AtPlacesAlongTheBeam_AndNeverAllAtOnce()
        {
            var v = new LaserBeamValues();
            int samples = 0, lit = 0, allLit = 0;
            for (int f = 0; f < 6000; f++)
            {
                float t = f / 60f;
                int litNow = 0;
                for (int i = 0; i < v.Specks; i++)
                {
                    LaserBeam.Speck(i, 1, t, v, out float along, out float l);
                    Assert.That(l, Is.InRange(0f, 1f));
                    Assert.That(along, Is.InRange(0f, 1f), "on the beam");
                    samples++;
                    if (l > 0f) { lit++; litNow++; }
                }
                if (litNow == v.Specks) allLit++;
            }
            float share = (float)lit / samples;
            Assert.That(share, Is.EqualTo(v.SpeckSeen).Within(0.06f), "each lit about its share of the time");
            Assert.That(allLit, Is.LessThan(60), "all of them lit at once almost never");
            LaserBeam.Speck(0, 1, 3.3f, v, out float a1, out _);
            LaserBeam.Speck(0, 2, 3.3f, v, out float a2, out _);
            Assert.That(a1, Is.Not.EqualTo(a2), "the two beams' motes are their own");
        }

        [Test]
        public void TheRay_MeetsAnUprightBodyAtItsSide_NotOneBesideIt_NorOneBehindOrOverIt()
        {
            // From 1.3 m up at the origin, east along the level.
            Assert.That(LaserBeam.HitsBody(0f, 1.3f, 0f, 1f, 0f, 0f, 3f, 0f, 0.5f, 2f, 4f, out float d), Is.True);
            Assert.That(d, Is.EqualTo(2.5f).Within(1e-4f), "at its near side");
            Assert.That(LaserBeam.HitsBody(0f, 1.3f, 0f, 1f, 0f, 0f, 3f, 0.6f, 0.5f, 2f, 4f, out _), Is.False, "beside the line");
            Assert.That(LaserBeam.HitsBody(0f, 1.3f, 0f, 1f, 0f, 0f, 5f, 0f, 0.5f, 2f, 4f, out _), Is.False, "past the beam's length");
            Assert.That(LaserBeam.HitsBody(0f, 1.3f, 0f, -1f, 0f, 0f, 3f, 0f, 0.5f, 2f, 4f, out _), Is.False, "behind");
            Assert.That(LaserBeam.HitsBody(0f, 2.5f, 0f, 1f, 0f, 0f, 3f, 0f, 0.5f, 2f, 4f, out _), Is.False, "over its top");
            Assert.That(LaserBeam.HitsBody(3f, 1.3f, 0f, 1f, 0f, 0f, 3f, 0f, 0.5f, 2f, 4f, out _), Is.False, "from inside it");
            // Down at 45 degrees toward a body 1 m ahead: met at its side, at a height on it.
            float k = 0.70710678f;
            Assert.That(LaserBeam.HitsBody(0f, 1.3f, 0f, k, -k, 0f, 1f, 0f, 0.3f, 2f, 4f, out d), Is.True);
            Assert.That(d * k, Is.EqualTo(0.7f).Within(1e-4f));
        }
    }
}
