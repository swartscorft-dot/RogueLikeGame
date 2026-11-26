using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 129's measure, from the rules alone (no play): for each kind that rages, its attacks a minute calm and enraged, the
    /// share of its shots that would meet her standing still and running straight across its line at her full run speed, at the
    /// kind's usual range (the middle of the band it keeps), and so its expected damage a minute (the attack's own damage, before the
    /// floor's scale). A shot is flown by the game's own projectile rule (<see cref="ProjectileRules.Step"/>, the arrow led as the
    /// archer leads it, the orb aimed where she is), the charge by the lancer's own (<see cref="LancerCharge"/>); the angle is spread
    /// evenly over its width, as the stream draws it. A kind's enraged rest is its own share (<see cref="PartRageRules.RestShare"/>:
    /// the melee kinds, the lancer among them, <see cref="PartRageValues.MeleeRestShare"/>, the others <see cref="PartRageValues.RestShare"/>;
    /// the merge of 2026-11-26, "Nærkamp raser svakere"), and the melee lines state it at both. Attacks a minute are the wind-up and the rest alone, back to back: the turns to
    /// attack, the screen rule, her cover and its walking all come on top, the same calm and enraged. The table is written to the
    /// test's output (and asserted only where it must hold).
    /// </summary>
    public class PartRageMeasureTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int Angles = 401;      // angles spread over a width (kept small: Unity's runtime is slower than .NET's)
        const float Dt = 1f / 120f;   // s a step of a flight

        static readonly PartRageValues V = new PartRageValues();
        static readonly ShooterValues Shooter = new ShooterValues();
        static readonly CasterValues Caster = new CasterValues();
        static readonly WarderValues Warder = new WarderValues();
        static readonly LancerValues Lancer = new LancerValues();
        static readonly float Run = new FeelValues().RunSpeed;

        enum Flies { Arrow, Orb, Charge }

        /// <summary>One attack as it goes off: its rule, what it flies as, how many at once, how far apart, from how far out of its body.</summary>
        sealed class Shot
        {
            public EnemyAttackDefinition Attack;
            public Flies As;
            public int Count = 1;
            public float Apart;
            public float Out;
            /// <summary>s it takes besides its wind-up and its rest: the lancer's run to her (its recovery counts from where it stops).</summary>
            public float Besides;
        }

        /// <summary>Hits on her of one attack at this angle: she at <paramref name="range"/> north of it, standing or running east.</summary>
        static int Hits(Shot s, float range, float angle, bool running)
        {
            var me = new GroundPoint(0f, 0f);
            var her = new GroundPoint(0f, range);
            var vel = new GroundPoint(running ? Run : 0f, 0f);
            GroundPoint aim = s.As == Flies.Arrow ? ProjectileRules.LeadAim(me, her, vel, Shooter.ArrowSpeed, Shooter.LeadShare, Shooter.LeadMost) : her;
            GroundPoint dir = aim - me;
            dir = PartRageRules.Turn(dir * (1f / dir.Length), angle);
            if (s.As == Flies.Charge) return ChargeHits(s.Attack, dir, her, vel) ? 1 : 0;
            int hits = 0;
            for (int i = 0; i < s.Count; i++)
            {
                GroundPoint way = ProjectileRules.FanDirection(dir, i, s.Count, s.Apart);
                Projectile p = s.As == Flies.Arrow ? ProjectileRules.Arrow(Shooter, me + dir * s.Out, way) : ProjectileRules.Orb(Caster, me + dir * s.Out, way);
                GroundPoint at = her;
                for (int step = 0; step < 4000; step++)
                {
                    GroundPoint next = at + vel * Dt;
                    ProjectileStep r = ProjectileRules.Step(ref p, Dt, at, next, Placeholders.PlayerRadius, null);
                    at = next;
                    if (r.Hit == ProjectileHit.MetHer) { hits++; break; }
                    if (r.Hit != ProjectileHit.None) break;
                }
            }
            return hits;
        }

        static bool ChargeHits(EnemyAttackDefinition charge, GroundPoint dir, GroundPoint her, GroundPoint vel)
        {
            var c = new LancerCharge();
            c.Begin(new GroundPoint(0f, 0f), dir, charge.Rush, charge.Length, float.PositiveInfinity, Lancer.StopShort, LancerStopper.None, 0f);
            GroundPoint at = her;
            for (int step = 1; step < 4000 && c.Running; step++)
            {
                float t0 = (step - 1) * Dt, t1 = step * Dt;
                at = at + vel * Dt;
                if (c.Step(c.At(t0), c.At(t1), t1, at, Placeholders.PlayerRadius, charge.HalfWidth, true) == LancerEnd.MetHer) return true;
            }
            return false;
        }

        /// <summary>Hits on her per attack, the angle spread evenly over the kind's width (none when calm).</summary>
        static double HitsPerAttack(Shot s, float range, float width, bool running)
        {
            if (width <= 0f) return Hits(s, range, 0f, running);
            long total = 0;
            for (int i = 0; i < Angles; i++)
            {
                float a = -width + (i + 0.5f) * (2f * width / Angles);
                total += Hits(s, range, a, running);
            }
            return total / (double)Angles;
        }

        sealed class Row
        {
            public string Name;
            public float Range;
            public double CalmPerMinute, RagePerMinute;
            public double CalmStanding, RageStanding, CalmRunning, RageRunning;       // hits on her per attack, the mean of its mix
            public double CalmDpmStanding, RageDpmStanding, CalmDpmRunning, RageDpmRunning; // her damage a minute, the attack's own
        }

        /// <summary>A kind's mix of attacks (its order: two arrows and a fan, two orbs and a fan), each as often as the mix says.</summary>
        static Row Measure(string name, EnemyKind kind, float range, params Shot[] mix)
        {
            float width = PartRageRules.Degrees(kind, V);
            float share = Share(kind);
            double calmTime = 0, rageTime = 0;
            var r = new Row { Name = name, Range = range };
            foreach (Shot s in mix)
            {
                calmTime += s.Attack.Windup + s.Besides + s.Attack.Recovery;
                rageTime += s.Attack.Windup + s.Besides + s.Attack.Recovery * share;
                double a = HitsPerAttack(s, range, 0f, false), b = HitsPerAttack(s, range, width, false);
                double c = HitsPerAttack(s, range, 0f, true), d = HitsPerAttack(s, range, width, true);
                r.CalmStanding += a / mix.Length; r.RageStanding += b / mix.Length; r.CalmRunning += c / mix.Length; r.RageRunning += d / mix.Length;
                r.CalmDpmStanding += a * s.Attack.Damage; r.RageDpmStanding += b * s.Attack.Damage;
                r.CalmDpmRunning += c * s.Attack.Damage; r.RageDpmRunning += d * s.Attack.Damage;
            }
            r.CalmPerMinute = 60.0 * mix.Length / calmTime;
            r.RagePerMinute = 60.0 * mix.Length / rageTime;
            r.CalmDpmStanding *= 60.0 / calmTime; r.CalmDpmRunning *= 60.0 / calmTime;
            r.RageDpmStanding *= 60.0 / rageTime; r.RageDpmRunning *= 60.0 / rageTime;
            return r;
        }

        /// <summary>The kind's own share of its rest while enraged with the switch on (the rules' choice, not the static switch's state).</summary>
        static float Share(EnemyKind kind) => PartRageRules.Melee(kind) ? V.MeleeRestShare : V.RestShare;

        /// <summary>A melee attack's damage a minute in her reach, every strike landing, at this share of its rest.</summary>
        static double MeleeDamageAMinute(EnemyAttackDefinition a, float share) => 60.0 / (a.Windup + a.Recovery * share) * a.Damage;

        static Shot ArrowShot(string name, int count = 1) =>
            new Shot { Attack = Find(Shooter.Arrows, name), As = Flies.Arrow, Count = count, Apart = Shooter.FanDegrees, Out = Shooter.BodyRadius + 0.2f };

        static Shot OrbShot(EnemyAttackDefinition a, int count, float out_) =>
            new Shot { Attack = a, As = Flies.Orb, Count = count, Apart = Caster.FanDegrees, Out = out_ };

        static EnemyAttackDefinition Find(EnemyAttackDefinition[] all, string name)
        {
            foreach (EnemyAttackDefinition a in all) if (a.Name == name) return a;
            throw new ArgumentException(name);
        }

        static List<Row> Table()
        {
            float shooterRange = 0.5f * (Shooter.KeepNear + Shooter.KeepFar);
            float casterRange = 0.5f * (Caster.KeepNear + Caster.KeepFar);
            float warderRange = 0.5f * (Warder.KeepNear + Warder.KeepFar);
            float lancerRange = 0.5f * (Lancer.KeepNear + Lancer.KeepFar);
            Shot arrow = ArrowShot(ShooterValues.Arrow), fan = ArrowShot(ShooterValues.ArrowFan, Shooter.FanArrows);
            EnemyAttackDefinition orb = Find(Caster.Orbs, CasterValues.BoneOrb), orbFan = Find(Caster.Orbs, CasterValues.OrbFan);
            var rows = new List<Row>
            {
                Measure("Archer, floors 1-3 (an arrow)", EnemyKind.Shooter, shooterRange, arrow),
                Measure("Archer, floors 4-5 (two arrows, a fan of three)", EnemyKind.Shooter, shooterRange, arrow, arrow, fan),
                Measure("Caster (two orbs, a fan of three)", EnemyKind.Caster, casterRange,
                        OrbShot(orb, 1, Caster.BodyRadius + 0.3f), OrbShot(orb, 1, Caster.BodyRadius + 0.3f), OrbShot(orbFan, Caster.FanOrbs, Caster.BodyRadius + 0.3f)),
                Measure("Warder, alone (its bolt)", EnemyKind.Warder, warderRange, OrbShot(Warder.Enemy.Attacks[0], 1, Warder.BodyRadius + 0.3f)),
                Measure("Lancer (its charge)", EnemyKind.Lancer, lancerRange, new Shot { Attack = Lancer.Enemy.Attacks[0], As = Flies.Charge, Besides = lancerRange / Lancer.Enemy.Attacks[0].Rush }),
            };
            return rows;
        }

        /// <summary>
        /// A melee kind's ordinary attack: its aim is left alone, so only how often changes (she in its reach, every strike landing),
        /// stated calm, enraged at <see cref="PartRageValues.RestShare"/> (work order 129's half, every kind's share before the merge
        /// of 2026-11-26) and enraged at <see cref="PartRageValues.MeleeRestShare"/> (the melee kinds' own share now).
        /// </summary>
        static string MeleeLine(string name, EnemyAttackDefinition a)
        {
            double calm = 60.0 / (a.Windup + a.Recovery), half = 60.0 / (a.Windup + a.Recovery * V.RestShare);
            double melee = 60.0 / (a.Windup + a.Recovery * V.MeleeRestShare);
            return string.Format(Inv, "| {0} | {1:0.00}+{2:0.00} s -> {1:0.00}+{3:0.00} s / {1:0.00}+{4:0.00} s | {5:0.0} -> {6:0.0} / {7:0.0} | {8:0} -> {9:0} / {10:0} (in reach) |",
                                 name, a.Windup, a.Recovery, a.Recovery * V.RestShare, a.Recovery * V.MeleeRestShare,
                                 calm, half, melee, calm * a.Damage, half * a.Damage, melee * a.Damage);
        }

        [Test]
        public void TheMeasure_AttacksAMinute_HitShares_AndExpectedDamage_CalmAndEnraged()
        {
            List<Row> rows = Table();
            var sb = new StringBuilder();
            sb.AppendLine("WORK ORDER 129 MEASURE (rules only; the attack's own damage, before the floor's scale; she runs across at " + Run.ToString("0.#", Inv) + " m/s)");
            sb.AppendLine("| Kind (its mix) | range | attacks/min calm -> enraged | hits/attack standing calm -> enraged | hits/attack running across calm -> enraged | damage/min standing calm -> enraged | damage/min running calm -> enraged |");
            foreach (Row r in rows)
                sb.AppendLine(string.Format(Inv, "| {0} | {1:0.##} m | {2:0.0} -> {3:0.0} | {4:0.00} -> {5:0.00} | {6:0.00} -> {7:0.00} | {8:0} -> {9:0} | {10:0} -> {11:0} |",
                                            r.Name, r.Range, r.CalmPerMinute, r.RagePerMinute, r.CalmStanding, r.RageStanding, r.CalmRunning, r.RageRunning,
                                            r.CalmDpmStanding, r.RageDpmStanding, r.CalmDpmRunning, r.RageDpmRunning));
            sb.AppendLine("Melee (aim left alone, StrikeDegrees 0; calm -> enraged at RestShare " + V.RestShare.ToString("0.##", Inv)
                          + " / enraged at MeleeRestShare " + V.MeleeRestShare.ToString("0.##", Inv) + ", the melee kinds' share now): | attack | wind-up+rest | attacks/min | damage/min |");
            var brute = MeleeAttacks.Brute();
            var guard = MeleeAttacks.Guard();
            var swarmer = MeleeAttacks.Swarmer();
            sb.AppendLine(MeleeLine("Brute's Stroke", Find(brute, MeleeAttacks.Stroke)));
            sb.AppendLine(MeleeLine("Guard's Fist", Find(guard, MeleeAttacks.Fist)));
            sb.AppendLine(MeleeLine("Swarmer's Bite", Find(swarmer, MeleeAttacks.Bite)));
            string table = sb.ToString();
            TestContext.Out.WriteLine(table);
            Console.WriteLine(table);

            foreach (Row r in rows)
            {
                // The lancer least (its run is not cut, and since the merge of 2026-11-26 it keeps three quarters of its rest, a melee kind's share).
                Assert.That(r.RagePerMinute, Is.GreaterThan(r.CalmPerMinute * (r.Name.StartsWith("Lancer", StringComparison.Ordinal) ? 1.05 : 1.1)), r.Name + ": more often");
                Assert.That(r.RageStanding, Is.LessThanOrEqualTo(r.CalmStanding + 1e-9), r.Name + ": standing still, no better aimed");
            }
            Assert.That(rows[0].CalmStanding, Is.EqualTo(1.0), "a calm arrow meets her standing still, every one");
            Assert.That(rows[0].RageStanding, Is.LessThan(0.9), "an enraged one misses her standing still now and then");

            // The melee kinds rage weaker (the merge of 2026-11-26): more often than calm, less often than at the shooters' half.
            Assert.That(Share(EnemyKind.Brute), Is.EqualTo(0.75f), "the brute's share: three quarters");
            Assert.That(Share(EnemyKind.Swarmer), Is.EqualTo(0.75f), "the swarmer's share: three quarters");
            Assert.That(Share(EnemyKind.Shooter), Is.EqualTo(0.5f), "the archer's share: a half");
            foreach (EnemyAttackDefinition a in new[] { Find(brute, MeleeAttacks.Stroke), Find(guard, MeleeAttacks.Fist), Find(swarmer, MeleeAttacks.Bite) })
            {
                double calm = MeleeDamageAMinute(a, 1f), half = MeleeDamageAMinute(a, V.RestShare), now = MeleeDamageAMinute(a, V.MeleeRestShare);
                Assert.That(now, Is.GreaterThan(calm), a.Name + ": enraged it still strikes more often than calm");
                Assert.That(now, Is.LessThan(half), a.Name + ": but less often than at the shooters' half");
            }
        }
    }
}
