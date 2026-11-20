using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Cover stops her shots (work order 010; Kristian, 2026-11-19: "Skudd skal stoppes av vegg og søyler"), as the rules
    /// see it, against cover these tests draw themselves: pillars as circles and walls as segments on the ground.
    /// </summary>
    public class CoverRulesTests
    {
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);
        static readonly GroundPoint North = new GroundPoint(0f, 20f);

        /// <summary>Cover on the ground for the rules: pillars (circles) and walls (segments, of no thickness).</summary>
        sealed class Drawn
        {
            public readonly List<(GroundPoint c, float r)> Pillars = new List<(GroundPoint, float)>();
            public readonly List<(GroundPoint a, GroundPoint b)> Walls = new List<(GroundPoint, GroundPoint)>();

            public float Clear(GroundPoint from, GroundPoint to)
            {
                GroundPoint d = to - from;
                float length = d.Length;
                if (length < 1e-6f) return 0f;
                GroundPoint u = d * (1f / length);
                float nearest = length;
                foreach ((GroundPoint c, float r) in Pillars)
                {
                    GroundPoint m = from - c;
                    float b = m.Dot(u), cc = m.Dot(m) - r * r;
                    float disc = b * b - cc;
                    if (cc <= 0f || disc < 0f) continue; // starts inside, or misses
                    float t = -b - (float)Math.Sqrt(disc);
                    if (t >= 0f && t < nearest) nearest = t;
                }
                foreach ((GroundPoint a, GroundPoint b) in Walls)
                {
                    GroundPoint e = b - a;
                    float den = u.X * e.Z - u.Z * e.X;
                    if (Math.Abs(den) < 1e-6f) continue;
                    GroundPoint w = a - from;
                    float t = (w.X * e.Z - w.Z * e.X) / den;
                    float s = (w.X * u.Z - w.Z * u.X) / den;
                    if (t >= 0f && s >= 0f && s <= 1f && t < nearest) nearest = t;
                }
                return nearest;
            }
        }

        static Drawn WallAcrossAt(float z)
        {
            var cover = new Drawn();
            cover.Walls.Add((new GroundPoint(-10f, z), new GroundPoint(10f, z)));
            return cover;
        }

        static SkillDefinition Skill(string name) => SkillDefaults.Create().Single(s => s.Name == name);

        [Test]
        public void Defaults_TheSwitchIsOn_AndCoverIs1Point2MetresTall()
        {
            Assert.That(new FeelValues().CoverStopsShots, Is.True);
            Assert.That(new PropValues().CoverHeight, Is.EqualTo(1.2f));
            Assert.That(CoverRules.ShortOfCover, Is.EqualTo(0.3f));
        }

        [Test]
        public void TallProps_AreCover_LowOnesAreNot()
        {
            var v = new PropValues();
            PropModel[] cover = Enum.GetValues(typeof(PropModel)).Cast<PropModel>()
                                    .Where(m => LevelProp.Make(m, new LevelPoint(0, 0), 0, PropPlace.Floor).IsCover(v)).ToArray();
            // Work order 028 added the bone throne, the bookshelf and the cage; the candelabra is tall but slender, and not cover.
            Assert.That(cover, Is.EquivalentTo(new[] { PropModel.Altar, PropModel.GraveMarker, PropModel.Pillar, PropModel.PillarBroken, PropModel.Statue,
                                                       PropModel.BoneThrone, PropModel.Bookshelf, PropModel.Cage }));
            LevelProp candelabra = LevelProp.Make(PropModel.Candelabra, new LevelPoint(0, 0), 0, PropPlace.Set);
            Assert.That(candelabra.Height, Is.GreaterThan(v.CoverHeight), "the candelabra is tall");
            Assert.That(candelabra.Slender && !candelabra.IsCover(v), "and slender: no cover");
            v.CoverHeight = 2f;
            Assert.That(LevelProp.Make(PropModel.PillarBroken, new LevelPoint(0, 0), 0, PropPlace.Floor).IsCover(v), Is.False, "it follows the level's value");
        }

        [Test]
        public void LineStop_IsTheRange_WithNoCover_OrCoverPastIt()
        {
            Assert.That(CoverRules.LineStop(Origin, North, 10f, null), Is.EqualTo(10f));
            Assert.That(CoverRules.LineStop(Origin, North, 10f, WallAcrossAt(12f).Clear), Is.EqualTo(10f));
            Assert.That(CoverRules.LineStop(Origin, North, 10f, WallAcrossAt(6f).Clear), Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void ALineShot_StopsAtCover_AndHitsNothingPastIt()
        {
            CoverLine cover = WallAcrossAt(6f).Clear;
            float stop = CoverRules.LineStop(Origin, North, 10f, cover);
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 4f), 0.5f, stop), Is.True, "in front of the wall");
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 8f), 0.5f, stop), Is.False, "behind it");
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 6.4f), 0.5f, stop), Is.True, "its near side before the wall");
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 6.6f), 0.5f, stop), Is.False, "its near side past the wall");
            // Heavy Volley's wide line, cut square: a target just behind the wall and off the centre line is not reached.
            Assert.That(LineShot.Hits(Origin, North, 14f, 1f, new GroundPoint(0.8f, 7.2f), 0.5f, stop), Is.False);

            var targets = new List<TargetInfo> { new TargetInfo(new GroundPoint(0f, 8f), 0.5f, true) };
            Assert.That(LineShot.FirstHit(Origin, North, 10f, 0.5f, targets, stop), Is.EqualTo(-1));
            Assert.That(LineShot.FirstHit(Origin, North, 10f, 0.5f, targets), Is.EqualTo(0), "without cover it is hit, as before");
        }

        [Test]
        public void ALineShot_GrazingAPillarWithItsSide_IsNotStopped()
        {
            var cover = new Drawn();
            cover.Pillars.Add((new GroundPoint(0.9f, 5f), 0.6f)); // its edge 0.3 m off the centre line, inside the shot's 0.5 m half width
            float stop = CoverRules.LineStop(Origin, North, 10f, cover.Clear);
            Assert.That(stop, Is.EqualTo(10f));
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 8f), 0.5f, stop), Is.True);

            cover.Pillars.Add((new GroundPoint(0f, 5f), 0.6f)); // square in the way
            stop = CoverRules.LineStop(Origin, North, 10f, cover.Clear);
            Assert.That(stop, Is.EqualTo(4.4f).Within(1e-3f), "stopped at the pillar's face");
            Assert.That(LineShot.Hits(Origin, North, 10f, 0.5f, new GroundPoint(0f, 8f), 0.5f, stop), Is.False);
        }

        [Test]
        public void APiercingLine_HitsEverythingBeforeCover_AndNothingAfter()
        {
            SkillDefinition volley = Skill("Heavy Volley");
            var area = new SkillArea(volley, Origin, North, WallAcrossAt(7f).Clear);
            Assert.That(area.Stopped, Is.True);
            Assert.That(area.Stop, Is.EqualTo(7f).Within(1e-4f));
            Assert.That(area.Hits(new GroundPoint(0f, 3f), 0.5f), Is.True);
            Assert.That(area.Hits(new GroundPoint(0.5f, 5f), 0.5f), Is.True);
            Assert.That(area.Hits(new GroundPoint(0f, 9f), 0.5f), Is.False);
        }

        [Test]
        public void ACone_HitsWhatItSees_NotWhatIsBehindAWall()
        {
            SkillDefinition scatter = Skill("Scatter Blast");
            var cover = new Drawn();
            cover.Walls.Add((new GroundPoint(0.8f, 2f), new GroundPoint(3f, 2f))); // a wall over the right half of the cone
            var area = new SkillArea(scatter, Origin, North, cover.Clear);
            var open = new GroundPoint(-1f, 3.5f);
            var behind = new GroundPoint(1.5f, 3.5f);
            Assert.That(SkillRules.InCone(Origin, North, scatter.Range, scatter.Angle, behind, 0.5f), Is.True, "both are in the cone");
            Assert.That(area.Hits(open, 0.5f), Is.True);
            Assert.That(area.Hits(behind, 0.5f), Is.False);
        }

        [Test]
        public void ACircle_LandsShortOfCover_AndHitsOnlyWhatItsCentreSees()
        {
            SkillDefinition keg = Skill("Powder Keg");
            CoverLine cover = WallAcrossAt(5f).Clear;
            var aim = new GroundPoint(0f, 7f);
            var area = new SkillArea(keg, Origin, aim, cover);
            Assert.That(area.Centre.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(area.Centre.Z, Is.EqualTo(5f - CoverRules.ShortOfCover).Within(1e-4f), "0.3 m short of the wall");
            Assert.That(area.Hits(new GroundPoint(1f, 3.5f), 0.5f), Is.True, "on her side");
            Assert.That(area.Hits(new GroundPoint(0f, 6f), 0.5f), Is.False, "within the radius, but on the far side");

            var clear = new SkillArea(keg, Origin, new GroundPoint(0f, 4f), cover);
            Assert.That(clear.Centre.Z, Is.EqualTo(4f).Within(1e-4f), "short of the wall it lands where aimed");
        }

        [Test]
        public void WithNoCover_EveryShapeIsAsBefore()
        {
            var rng = new Random(10);
            foreach (SkillDefinition skill in SkillDefaults.Create())
                for (int i = 0; i < 200; i++)
                {
                    var aim = new GroundPoint((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 20f - 10f);
                    var target = new GroundPoint((float)rng.NextDouble() * 30f - 15f, (float)rng.NextDouble() * 30f - 15f);
                    var area = new SkillArea(skill, Origin, aim, null);
                    Assert.That(area.Hits(target, 0.5f), Is.EqualTo(SkillRules.Hits(skill, Origin, aim, target, 0.5f)), skill.Name + " at " + target);
                }
        }
    }
}
