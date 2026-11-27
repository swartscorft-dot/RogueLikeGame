using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Tests
{
    /// <summary>
    /// Work order 147, part 7: Ruin's shadows as geometry in both throne templates. From the room's middle (where Judgement kneels),
    /// each solid cell a 4 m block, from every place she can stand within 10 m of the middle, the walk round the blocks to the nearest
    /// place a shadow holds her middle must fit in Ruin's time for that template, with 0.25 s to react, at her run speed (6 m/s) and
    /// without a dash. The numbers are written to the test's output for the report.
    /// </summary>
    public class BossTrialRuinRulesTests
    {
        const float Cell = 4f, Within = 10f, Grid = 0.25f, HerRadius = Placeholders.PlayerRadius, Run = 6f, React = 0.25f;

        static RoomTemplate Template(string name)
        {
            foreach (RoomTemplate t in RoomTemplateLibrary.All) if (t.Name == name) return t;
            Assert.Fail("no template " + name);
            return null;
        }

        static RuinShadows.Reach MeasureOf(string name, out GroundPoint middle)
        {
            RoomTemplate t = Template(name);
            List<GroundRect> blocks = RuinShadows.Blocks(t, Cell, out GroundRect room);
            middle = new GroundPoint((room.X0 + room.X1) * 0.5f, (room.Z0 + room.Z1) * 0.5f);
            var v = new BossTrialValues();
            return RuinShadows.Measure(middle, blocks, room, v.ShadowSamples, Within, Grid, HerRadius, Run, React, new BossValues().BodyRadius);
        }

        static string Line(string name, RuinShadows.Reach r, float seconds) =>
            name + ": " + r.Shadows + " shadows, " + r.Places + " places within " + Within + " m (" + r.InShadow + " in a shadow already); the farthest "
            + r.Farthest.ToString("0.0", CultureInfo.InvariantCulture) + " m on foot at " + r.Worst + ", needing "
            + r.Needed.ToString("0.00", CultureInfo.InvariantCulture) + " s; Ruin gives " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

        [TestCase("throne-nave")]
        [TestCase("throne-arcade")]
        public void FromWithinTenMetresOfTheMiddle_AShadowIsReachedOnFoot_InRuinsTimeForTheTemplate(string name)
        {
            RuinShadows.Reach r = MeasureOf(name, out _);
            float seconds = new BossTrialValues().RuinSecondsFor(name);
            TestContext.WriteLine(Line(name, r, seconds));
            Assert.That(r.Places, Is.GreaterThan(500), "the circle has places");
            Assert.That(r.Shadows, Is.GreaterThanOrEqualTo(2), "the piers cast shadows");
            Assert.That(r.Needed, Is.LessThanOrEqualTo(seconds), Line(name, r, seconds));
        }

        [Test]
        public void SectionSevensTwoSeconds_AreShortInBoth_EachTemplateIsLengthenedByNoMoreThanItNeedsAndATenth()
        {
            RuinShadows.Reach nave = MeasureOf("throne-nave", out _);
            RuinShadows.Reach arcade = MeasureOf("throne-arcade", out _);
            var v = new BossTrialValues();
            Assert.That(v.RuinSeconds, Is.EqualTo(2.0f), "section 7.2's 2.0 s, for a room not measured");
            Assert.That(nave.Needed, Is.GreaterThan(2.0f), Line("throne-nave", nave, v.RuinSecondsNave));
            Assert.That(arcade.Needed, Is.GreaterThan(2.0f), "the arcade's two piers stand in its front half: 2.0 s is not enough from its back half");
            Assert.That(v.RuinSecondsNave, Is.LessThanOrEqualTo(nave.Needed + 0.1f));
            Assert.That(v.RuinSecondsArcade, Is.LessThanOrEqualTo(arcade.Needed + 0.1f));
            Assert.That(v.RuinSecondsFor("throne-nave"), Is.EqualTo(v.RuinSecondsNave));
            Assert.That(v.RuinSecondsFor("throne-arcade"), Is.EqualTo(v.RuinSecondsArcade));
            Assert.That(v.RuinSecondsFor(null), Is.EqualTo(v.RuinSeconds));
        }

        /// <summary>m his camera shows of the ground round her (research R3's metrics): to each side, above her and below her.</summary>
        const float ViewSide = 13.3f, ViewUp = 11.3f, ViewDown = 7.7f;

        /// <summary>
        /// The second round: the shadows are drawn (dim) from the moment the ward rises. With the room as drawn (the door at his screen's
        /// bottom), from how many places within 10 m of the middle is a shadow on his screen, and is any shadow one he could not have
        /// seen from anywhere near the boss?
        /// </summary>
        [TestCase("throne-nave")]
        [TestCase("throne-arcade")]
        public void WithTheShadowsDrawnFromTheWardsRise_AShadowIsOnHisScreen_FromWhereHeStands(string name)
        {
            RoomTemplate t = Template(name);
            List<GroundRect> blocks = RuinShadows.Blocks(t, Cell, out GroundRect room);
            var middle = new GroundPoint((room.X0 + room.X1) * 0.5f, (room.Z0 + room.Z1) * 0.5f);
            float[] clear = RuinShadows.Table(middle, blocks, room, new BossTrialValues().ShadowSamples);
            var shadow = new List<GroundPoint>();
            var places = new List<GroundPoint>();
            float body = new BossValues().BodyRadius;
            for (float x = room.X0 + 0.25f; x < room.X1; x += 0.5f)
                for (float z = room.Z0 + 0.25f; z < room.Z1; z += 0.5f)
                {
                    bool open = true;
                    foreach (GroundRect b in blocks) if (b.Contains(x, z, HerRadius)) { open = false; break; }
                    if (!open) continue;
                    var p = new GroundPoint(x, z);
                    if (RuinShadows.InShadow(clear, middle, p)) shadow.Add(p);
                    else if (p.DistanceTo(middle) <= Within && p.DistanceTo(middle) >= body + HerRadius) places.Add(p);
                }
            bool InView(GroundPoint her, GroundPoint q) => Math.Abs(q.X - her.X) <= ViewSide && q.Z - her.Z <= ViewUp && her.Z - q.Z <= ViewDown;
            int blind = 0;
            GroundPoint worst = default;
            foreach (GroundPoint her in places)
            {
                bool any = false;
                foreach (GroundPoint q in shadow) if (InView(her, q)) { any = true; break; }
                if (!any) { blind++; worst = her; }
            }
            int unseen = 0;
            foreach (GroundPoint q in shadow)
            {
                bool seen = false;
                foreach (GroundPoint her in places) if (InView(her, q)) { seen = true; break; }
                if (!seen) unseen++;
            }
            TestContext.WriteLine(name + ": " + places.Count + " places within 10 m; from " + blind + " of them no shadow is on his screen"
                                  + (blind > 0 ? " (e.g. " + worst + ")" : "") + "; " + unseen + " of " + shadow.Count + " shadow cells never on his screen from anywhere within 10 m");
            Assert.That(shadow.Count, Is.GreaterThan(0));
            Assert.That(unseen, Is.LessThan(shadow.Count), "some shelter can be seen from near the boss");
        }

        [Test]
        public void AShadow_IsBehindAPier_AwayFromTheBoss_AndJudgedByTheSameTable()
        {
            // One 4 m pier 6 m east of the boss in a 32 m room: east of it is shadow, west and north are not.
            var room = new GroundRect(-16f, -16f, 16f, 16f);
            var blocks = new List<GroundRect> { new GroundRect(6f, -2f, 10f, 2f) };
            var boss = new GroundPoint(0f, 0f);
            float[] clear = RuinShadows.Table(boss, blocks, room, 720);
            Assert.That(RuinShadows.InShadow(clear, boss, new GroundPoint(12f, 0f)), Is.True, "behind it");
            Assert.That(RuinShadows.InShadow(clear, boss, new GroundPoint(14f, 2.5f)), Is.True, "the shadow widens away from the boss");
            Assert.That(RuinShadows.InShadow(clear, boss, new GroundPoint(4f, 0f)), Is.False, "in front of it");
            Assert.That(RuinShadows.InShadow(clear, boss, new GroundPoint(12f, 6f)), Is.False, "beside its shadow");
            Assert.That(RuinShadows.InShadow(clear, boss, new GroundPoint(-12f, 0f)), Is.False, "the other way");
            Assert.That(RuinShadows.Runs(clear, RuinShadows.Edges(boss, room, 720), 0.5f), Is.EqualTo(1));
        }
    }
}
