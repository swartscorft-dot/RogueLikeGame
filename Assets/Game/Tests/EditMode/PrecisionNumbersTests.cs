using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 143's numbers for him, from the rules: for Off and the three modes, with the stock grip, Runner's Grip and Weighted
    /// Grip (as the store has them now, work order 139's gentler costs), held fire on the move from a run: the cone at the first and
    /// at the steady shot (read at the lowest share of each shot's wind-up), and the miss chance on a lone 0.6 m enemy at 4, 8 and
    /// 10 m with the cursor on its middle (the centre-weighted draw, worked out exactly); and Inertia against an instant start for
    /// leaving a 3 m circle. Each table is written to the test's output. Also her Precision (the Laser Sight's cut, the cap), her
    /// sheet's line, the Trials page's line and the summary's counts.
    /// </summary>
    public class PrecisionNumbersTests
    {
        static readonly PrecisionValues V = new PrecisionValues();
        static readonly FeelValues F = new FeelValues();
        const float Run = 6f;

        [TearDown]
        public void PutBack()
        {
            PrecisionValues.On = true;
            PrecisionValues.ModeSource = null;
        }

        /// <summary>The cone of each shot of held fire on the move (shots every interval, from a run at full speed).</summary>
        public static List<float> HeldFireCones(PrecisionMode mode, float gripShare, int shots, float cut = 0f)
        {
            var cones = new List<float>();
            float startup = F.BasicShotStartup, interval = F.BasicShotStartup + F.BasicShotActive + F.BasicShotRecovery;
            float recovery = OffTheMark.MovingShotRecovery(F);
            const float dt = 1f / 2000f;
            // Running at full speed for a second first: the meter full, the ghost at her speed.
            float bodyX = 0f, bodyZ = Run, ghostX = 0f, ghostZ = Run, momentum = 1f;
            float easeFrom = 1f, easeAt = 0f;
            float t = 0f;
            for (int shot = 0; shot < shots; shot++)
            {
                float shotAt = shot * interval;
                // The ease starts from the share she may move at now (the cap's curve where it stands).
                float capNow = shot == 0 ? 1f : MovingShot.SpeedShare(easeFrom, shotAt - easeAt, startup, gripShare, F.MovingShotSlowdown, recovery);
                easeFrom = capNow;
                easeAt = shotAt;
                float lowest = float.PositiveInfinity;
                while (t < shotAt + interval - 1e-6f)
                {
                    float cap = MovingShot.SpeedShare(easeFrom, t - easeAt, startup, gripShare, F.MovingShotSlowdown, recovery) * Run;
                    float realShare;
                    if (mode == PrecisionMode.Inertia)
                    {
                        PrecisionRules.InertiaFrame(ref bodyX, ref bodyZ, 0f, 1f, cap, dt, V);
                        realShare = bodyZ / Run;
                    }
                    else realShare = cap / Run;
                    float share = realShare;
                    if (mode == PrecisionMode.Meter)
                    {
                        momentum = PrecisionRules.MeterStep(momentum, realShare, dt, V, V.AccurateShare);
                        share = PrecisionRules.MeterShare(momentum, V.AccurateShare);
                    }
                    else if (mode == PrecisionMode.Ghost)
                    {
                        PrecisionRules.GhostFrame(ref ghostX, ref ghostZ, 0f, realShare * Run, dt, V);
                        share = (float)Math.Sqrt(ghostX * ghostX + ghostZ * ghostZ) / Run;
                    }
                    if (t - shotAt <= startup) lowest = Math.Min(lowest, share);
                    t += dt;
                }
                cones.Add(mode == PrecisionMode.Off ? 0f : PrecisionRules.AfterCut(PrecisionRules.Cone(lowest, V), cut, V));
            }
            return cones;
        }

        /// <summary>The miss chance, the cursor on a lone enemy's middle at <paramref name="d"/> m: the deflected line must pass within the line's half width and the enemy's radius of its middle; the draw triangular on the half cone.</summary>
        public static double MissChance(float cone, float d, float reach = 0.8f)
        {
            double half = cone * 0.5;
            if (half <= 0.0 || d <= reach) return 0.0;
            double limit = Math.Asin(reach / d) * 180.0 / Math.PI;
            if (limit >= half) return 0.0;
            double k = 1.0 - limit / half;
            return k * k;
        }

        static float Grip(string id) => WeaponAttachments.Create().First(p => p.Id == id).MovingSpeedShare;

        [Test]
        public void HeldFireOnTheMove_TheConesAndMissChances_ForEachModeAndGrip()
        {
            var grips = new[] { ("stock", F.MovingShotSpeedShare), ("Runner's Grip", Grip(WeaponAttachments.RunnersGrip)), ("Weighted Grip", Grip(WeaponAttachments.WeightedGrip)) };
            var sb = new StringBuilder();
            sb.AppendLine("mode | grip (share) | cone first / steady | miss 4 m | miss 8 m | miss 10 m (first / steady, centre-weighted)");
            foreach (PrecisionMode mode in new[] { PrecisionMode.Off, PrecisionMode.Inertia, PrecisionMode.Meter, PrecisionMode.Ghost })
                foreach (var (name, share) in grips)
                {
                    List<float> cones = HeldFireCones(mode, share, 8);
                    float first = cones[0], steady = cones[cones.Count - 1];
                    sb.AppendLine(mode + " | " + name + " (" + N(share, "0.###") + ") | " + N(first) + "° / " + N(steady) + "° | "
                                  + P(MissChance(first, 4f)) + " / " + P(MissChance(steady, 4f)) + " | "
                                  + P(MissChance(first, 8f)) + " / " + P(MissChance(steady, 8f)) + " | "
                                  + P(MissChance(first, 10f)) + " / " + P(MissChance(steady, 10f)));
                    if (mode == PrecisionMode.Off) Assert.That(cones.All(c => c == 0f), "Off: no cone");
                    else
                    {
                        Assert.That(first, Is.GreaterThan(0f).And.LessThanOrEqualTo(24f), mode + " " + name + ": a cone on the move");
                        Assert.That(MissChance(steady, 4f), Is.EqualTo(0.0).Within(0.01), mode + " " + name + ": nothing misses at 4 m");
                    }
                }
            // Research r4's own numbers for the instant body (its table: stock 13.8 then 11.2, Runner's 18.9 then 17.6): the ghost and
            // the meter lag her, Inertia's body is near the instant curve.
            List<float> stock = HeldFireCones(PrecisionMode.Inertia, F.MovingShotSpeedShare, 8);
            Assert.That(stock[0], Is.EqualTo(13.8f).Within(2f), "Inertia, stock: about research r4's 13.8 degrees at the first shot");
            TestContext.WriteLine(sb.ToString());
        }

        [Test]
        public void Inertia_LeavingA3mCircle_AgainstTheInstantStart()
        {
            var sb = new StringBuilder();
            sb.AppendLine("leaving a 3 m circle from its middle | instant (today) | Inertia");
            foreach (var (name, vx, vz, wx, wz) in new[]
                     {
                         ("from a stand", 0f, 0f, 0f, 1f),
                         ("from a run, the same way", 0f, Run, 0f, 1f),
                         ("from a run, a quarter turn", Run, 0f, 0f, 1f),
                         ("from a run, back the way she came", 0f, -Run, 0f, 1f),
                     })
            {
                Leave(vx, vz, wx, wz, out float seconds, out float path);
                sb.AppendLine(name + " | " + Ms(3f / Run) + " | " + Ms(seconds) + " (" + N(path, "0.00") + " m walked)");
                Assert.That(seconds, Is.GreaterThanOrEqualTo(3f / Run - 0.01f).And.LessThan(1.2f), name);
            }
            TestContext.WriteLine(sb.ToString());
        }

        static void Leave(float vx, float vz, float wx, float wz, out float seconds, out float path)
        {
            const float dt = 1f / 2000f;
            float x = 0f, z = 0f, t = 0f;
            path = 0f;
            while (x * x + z * z < 9f && t < 3f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, wx, wz, Run, dt, V);
                x += vx * dt;
                z += vz * dt;
                path += (float)Math.Sqrt(vx * vx + vz * vz) * dt;
                t += dt;
            }
            seconds = t;
        }

        // ---------------- Her Precision, her sheet, the Trials page, the summary ----------------

        static RunModifiers WithLaser(bool laser)
        {
            var m = new RunModifiers();
            if (laser) m.Weapon.Add(WeaponAttachments.Create().First(p => p.Id == WeaponAttachments.LaserSight));
            return m;
        }

        [Test]
        public void HerPrecision_IsTheLaserSightsCut_OnlyWhilePrecisionIsInUse()
        {
            PrecisionValues.ModeSource = () => PrecisionMode.Inertia;
            Assert.That(WithLaser(false).Stats.Precision, Is.EqualTo(0f), "her start: no cut");
            Assert.That(WithLaser(true).Stats.Precision, Is.EqualTo(0.4f).Within(1e-5f), "the Laser Sight's −40 %");
            RunModifiers two = WithLaser(true);
            two.Weapon.Add(new WeaponAttachment { Id = "test", Name = "Test", MovingConeCut = 0.4f });
            Assert.That(two.Stats.Precision, Is.EqualTo(0.6f).Within(1e-5f), "capped at 60 %");
            PrecisionValues.ModeSource = () => PrecisionMode.Off;
            Assert.That(WithLaser(true).Stats.Precision, Is.EqualTo(0f), "Off: nothing");
            PrecisionValues.ModeSource = null;
            Assert.That(WithLaser(true).Stats.Precision, Is.EqualTo(0f), "no source: Off");
        }

        [Test]
        public void HerSheet_HasPrecisionInTheBasicShotGroup_OnlyWhilePrecisionIsInUse()
        {
            PrecisionValues.ModeSource = () => PrecisionMode.Inertia;
            SheetStat line = HerSheet.Stats(new OfferContext { Modifiers = WithLaser(true) }).Single(s => s.Name == "Precision");
            Assert.That(line.Group, Is.EqualTo(SheetGroup.BasicShot));
            Assert.That(line.Value, Is.EqualTo("Spread at a run 24° to 14°"));
            line = HerSheet.Stats(new OfferContext { Modifiers = WithLaser(false) }).Single(s => s.Name == "Precision");
            Assert.That(line.Value, Is.EqualTo("Spread at a run 24°"), "no cut: the spread alone");
            PrecisionValues.ModeSource = () => PrecisionMode.Off;
            Assert.That(HerSheet.Stats(new OfferContext { Modifiers = WithLaser(true) }).Any(s => s.Name == "Precision"), Is.False, "Off: no line");
        }

        [Test]
        public void TheTrialsPage_HasPrecisionLast_WithTheSwitchOn()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            // The merge of 2026-11-27, W2 (changed deliberately): right after work order 134's click, found by its line, not counted
            // from the end (144's sight, 135's line-ups and 136's waves come after it).
            Assert.That(lines.IndexOf(SettingLine.Precision), Is.EqualTo(lines.IndexOf(SettingLine.TimingClick) + 1), "after the timing click");
            Assert.That(SettingsPages.PageOf(SettingLine.Precision), Is.EqualTo(SettingsPage.Trials));
            PrecisionValues.On = false;
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines, Has.No.Member(SettingLine.Precision), "the switch off: no line");
            Assert.That(PrecisionLines.Stored(-1), Is.EqualTo(PrecisionMode.Inertia), "none stored: his tick");
            Assert.That(PrecisionLines.Stored(2), Is.EqualTo(PrecisionMode.Meter));
            Assert.That(PrecisionLines.Stepped(PrecisionMode.Ghost, 1), Is.EqualTo(PrecisionMode.Off), "round");
            Assert.That(PrecisionLines.Stepped(PrecisionMode.Off, -1), Is.EqualTo(PrecisionMode.Ghost));
            Assert.That(PrecisionLines.WordsOf(PrecisionMode.Inertia), Is.EqualTo("Inertia"));
        }

        [Test]
        public void TheSummary_CountsAccurateShots_ConeMisses_AndCounterStrafes()
        {
            var events = new List<SessionEvent>();
            SessionEvent Shot(double t, float cone, bool miss, float since)
            {
                var e = new SessionEvent(t, SessionEventTypes.ShotFired, SessionWords.BasicShot) { Hit = !miss };
                e.AddExtra(PrecisionLog.Cone, N(cone, "0.###"));
                e.AddExtra(PrecisionLog.ConeMiss, miss ? "true" : "false");
                if (since >= 0f) e.AddExtra(PrecisionLog.SinceCounterStrafe, N(since, "0.###"));
                return e;
            }
            events.Add(new SessionEvent(1.0, PrecisionLog.CounterStrafe, SessionWords.BasicShot));
            events.Add(Shot(1.1, 0f, false, 0.1f));
            events.Add(Shot(2.0, 12f, true, 1f));
            events.Add(Shot(3.0, 0f, false, 0.2f));
            events.Add(Shot(4.0, 8f, false, 5f));
            events.Add(new SessionEvent(5.0, SessionEventTypes.ShotFired, SessionWords.BasicShot) { Hit = true }); // Off: no extras
            PrecisionStats s = SessionSummary.ComputePrecision(events, 120.0);
            Assert.That(s.Shots, Is.EqualTo(4), "only the shots with a cone read");
            Assert.That(s.Accurate, Is.EqualTo(2));
            Assert.That(s.ConeMisses, Is.EqualTo(1));
            Assert.That(s.CounterStrafes, Is.EqualTo(1));
            Assert.That(s.Minutes, Is.EqualTo(2f));
            Assert.That(s.CounterToShotCount, Is.EqualTo(3), "within a second of one");
            Assert.That(s.CounterToShotMedian, Is.EqualTo(0.2f).Within(1e-5f));
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            StringAssert.Contains("Precision (work order 143)", text);
            Assert.That(SessionSummary.Format(SessionSummary.Compute(new List<SessionEvent> { events[5] })), Does.Not.Contain("Precision (work order 143)"),
                        "a log without precision's extras: no block");
        }

        static string N(double v, string f = "0.0") => v.ToString(f, CultureInfo.InvariantCulture);
        static string P(double share) => (share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
        static string Ms(float s) => (s * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms";
    }
}
