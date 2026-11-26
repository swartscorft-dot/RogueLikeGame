using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 129 (Kristian 2026-11-25: "De sikter dårligere, men fyrer av mye oftere. en slags rage mode."): the rules of rage
    /// when a part is lost. It begins at work order 122's first part's share of health lost and not before, by 122's rules value and
    /// not its switch; kinds that lose no part (the boss) never rage; the rest after a strike is cut and the wind-up is not; the aim's
    /// angle stays within its kind's width, from a stream that gives the same angles for the same seed; the switch is on by default,
    /// and off every enemy attacks as today, field for field.
    /// </summary>
    public class PartRageRulesTests
    {
        static readonly PartRageValues V = new PartRageValues();
        static readonly EnemyKind[] s_Kinds = (EnemyKind[])Enum.GetValues(typeof(EnemyKind));

        bool _onWas, _bonesWas;

        [SetUp]
        public void Remember()
        {
            _onWas = PartRageValues.On;
            _bonesWas = BoneBreakValues.On;
        }

        [TearDown]
        public void Restore()
        {
            PartRageValues.On = _onWas;
            BoneBreakValues.On = _bonesWas;
        }

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheValuesAreTheFirstGuesses()
        {
            Assert.That(PartRageValues.On, Is.True, "on by default (the order's point 6)");
            Assert.That(V.RestShare, Is.EqualTo(0.5f), "the rest cut to half (the order's first guess): the shooters, the casters, the warder");
            Assert.That(V.MeleeRestShare, Is.EqualTo(0.75f), "the melee kinds' rest cut to three quarters (the merge of 2026-11-26, \"Nærkamp raser svakere\")");
            Assert.That(V.ShotDegrees, Is.EqualTo(12f), "a shot or a bolt up to 12 degrees to either side");
            Assert.That(V.ChargeDegrees, Is.GreaterThan(0f).And.LessThan(V.ShotDegrees), "the charge: a smaller angle");
            Assert.That(V.StrikeDegrees, Is.EqualTo(0f), "a melee strike's aim left alone");
        }

        // ---------------- When ----------------

        [Test]
        public void TheRage_BeginsAtThePartsShare_AndNotBefore_ForEveryKindThatRages()
        {
            var parts = new BoneBreakValues();
            foreach (EnemyKind kind in s_Kinds)
            {
                if (!PartRageRules.Rages(kind)) continue;
                foreach (int max in new[] { 100, 220, 300, 400, 800, 1600, 6000 })
                {
                    int first = -1; // the highest health at which it is due
                    for (int h = max; h >= 0; h--)
                        if (PartRageRules.Due(kind, h, max, parts)) { first = h; break; }
                    Assert.That(first, Is.GreaterThanOrEqualTo(0), kind + " " + max + ": it rages before it dies");
                    Assert.That(BoneBreakRules.PartsDue(first, max, parts), Is.EqualTo(1), kind + " " + max + ": exactly at the first part");
                    Assert.That(BoneBreakRules.PartsDue(first + 1, max, parts), Is.EqualTo(0), kind + " " + max + ": one point more and no part is due");
                    for (int h = first; h >= 0; h--)
                        Assert.That(PartRageRules.Due(kind, h, max, parts), Is.True, kind + " " + max + " at " + h + ": and under it");
                    Assert.That(PartRageRules.Due(kind, max, max, parts), Is.False, "whole, calm");
                }
            }
            // A third lost: an 800 shooter rages at 533 left, not at 534.
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 534, 800, parts), Is.False);
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 533, 800, parts), Is.True);
        }

        [Test]
        public void TheRage_HangsOnTheBoneRulesValue_NotOnTheLook()
        {
            var parts = new BoneBreakValues();
            BoneBreakValues.On = false; // 122's look off
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 533, 800, parts), Is.True, "the look off: the same share");
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 534, 800, parts), Is.False);
            BoneBreakValues.On = true;
            parts.PartShare = 0.5; // the part's share changed: the rage moves with it
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 401, 800, parts), Is.False);
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 400, 800, parts), Is.True);
            parts.PartsBeforeDeath = 0; // no part ever comes off alive: no rage
            Assert.That(PartRageRules.Due(EnemyKind.Shooter, 1, 800, parts), Is.False);
        }

        [Test]
        public void KindsThatLoseNoPart_NeverRage()
        {
            var parts = new BoneBreakValues();
            foreach (EnemyKind kind in s_Kinds)
            {
                Assert.That(PartRageRules.Rages(kind), Is.EqualTo(BoneBreakRules.Breaks(kind)), kind + ": rages exactly when it loses parts");
                if (BoneBreakRules.Breaks(kind)) continue;
                for (int h = 0; h <= 1000; h += 7)
                    Assert.That(PartRageRules.Due(kind, h, 1000, parts), Is.False, kind + " at " + h);
                Assert.That(PartRageRules.Degrees(kind, V), Is.EqualTo(0f));
                Assert.That(PartRageRules.Angle(kind, true, 1.0, V), Is.EqualTo(0f));
            }
            Assert.That(PartRageRules.Rages(EnemyKind.Boss), Is.False, "the boss, as 122 left it");
        }

        // ---------------- Fires much more often ----------------

        /// <summary>A brain stepped every millisecond with her 8 m away (or at <paramref name="distance"/>): when its wind-ups begin and when it strikes.</summary>
        static (List<float> windups, List<float> strikes) Run(EnemyBrain brain, EnemyAttackDefinition attack, float seconds, float distance = 8f)
        {
            var values = new EnemyValues { Health = 800, AggroRange = 14f, LeashRange = 26f, Attacks = new[] { attack } };
            var windups = new List<float>();
            var strikes = new List<float>();
            EnemyState before = brain.State;
            int steps = (int)(seconds * 1000f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i * 0.001f;
                bool struck = brain.Step(t, distance, true, true, values);
                if (brain.State == EnemyState.Windup && before != EnemyState.Windup) windups.Add(t);
                if (struck) strikes.Add(t);
                before = brain.State;
            }
            return (windups, strikes);
        }

        [Test]
        public void TheRestIsCut_AndTheWindupIsNot()
        {
            EnemyAttackDefinition arrow = new ShooterValues().Arrows[0];
            var whole = new EnemyBrain { RestShare = PartRageRules.RestShare(EnemyKind.Shooter, false, V) };
            var raging = new EnemyBrain { RestShare = PartRageRules.RestShare(EnemyKind.Shooter, true, V) };
            Assert.That(whole.RestShare, Is.EqualTo(1f), "calm: today's rest");
            Assert.That(raging.RestShare, Is.EqualTo(V.RestShare), "enraged: its share");
            var w = Run(whole, arrow, 12f);
            var r = Run(raging, arrow, 12f);
            const float Step = 0.0025f;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(w.strikes[i] - w.windups[i], Is.EqualTo(arrow.Windup).Within(Step), "calm: its wind-up");
                Assert.That(r.strikes[i] - r.windups[i], Is.EqualTo(arrow.Windup).Within(Step), "enraged: the same wind-up, to the step");
                Assert.That(w.windups[i + 1] - w.windups[i], Is.EqualTo(arrow.Windup + arrow.Recovery).Within(Step), "calm: 0.9 + 1.8 s");
                Assert.That(r.windups[i + 1] - r.windups[i], Is.EqualTo(arrow.Windup + arrow.Recovery * V.RestShare).Within(Step), "enraged: 0.9 + 0.9 s");
            }
            Assert.That(r.strikes.Count, Is.GreaterThan(w.strikes.Count), "more strikes in the same time");
        }

        [Test]
        public void TheMeleeKinds_RageWeaker_WithTheirOwnShare_AndTheOthersKeepTheHalf()
        {
            // The merge of 2026-11-26 (Kristian's tick of 2026-11-25, "Nærkamp raser svakere"): the kinds that strike in reach stand
            // three quarters of their rest, the shooters, the casters and the warder a half; the boss never rages.
            PartRageValues.On = true;
            var melee = new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard, EnemyKind.Lancer, EnemyKind.Bomber };
            var ranged = new[] { EnemyKind.Shooter, EnemyKind.Caster, EnemyKind.Warder };
            foreach (EnemyKind kind in s_Kinds)
            {
                bool isMelee = Array.IndexOf(melee, kind) >= 0, isRanged = Array.IndexOf(ranged, kind) >= 0;
                Assert.That(isMelee || isRanged || kind == EnemyKind.Boss, Is.True, kind + ": a kind this test does not know (say whether it strikes in reach)");
                Assert.That(PartRageRules.Melee(kind), Is.EqualTo(isMelee), kind + ": melee");
                if (isMelee) Assert.That(PartRageRules.RestShare(kind, true, V), Is.EqualTo(0.75f), kind + ": three quarters of its rest");
                if (isRanged) Assert.That(PartRageRules.RestShare(kind, true, V), Is.EqualTo(0.5f), kind + ": half its rest");
                Assert.That(PartRageRules.RestShare(kind, false, V), Is.EqualTo(1f), kind + ": calm, its whole rest");
            }

            // And a brain stepped by it: the brute's Stroke, its wind-up kept, three quarters of its rest.
            EnemyAttackDefinition stroke = null;
            foreach (EnemyAttackDefinition a in MeleeAttacks.Brute()) if (a.Name == MeleeAttacks.Stroke) stroke = a;
            Assert.That(stroke, Is.Not.Null);
            var raging = new EnemyBrain { RestShare = PartRageRules.RestShare(EnemyKind.Brute, true, V) };
            var r = Run(raging, stroke, 12f, 0.5f * (stroke.MinDistance + stroke.MaxDistance)); // she in its reach
            const float Step = 0.0025f;
            Assert.That(r.windups.Count, Is.GreaterThanOrEqualTo(4), "it strikes in its reach");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(r.strikes[i] - r.windups[i], Is.EqualTo(stroke.Windup).Within(Step), "enraged: the same wind-up");
                Assert.That(r.windups[i + 1] - r.windups[i], Is.EqualTo(stroke.Windup + stroke.Recovery * 0.75f).Within(Step), "enraged: three quarters of its rest");
            }
        }

        // ---------------- Aims worse ----------------

        [Test]
        public void TheAimsAngle_StaysWithinItsWidth_AndIsTheSameForTheSameSeed()
        {
            ulong seed = PartRageRules.Seed(0x1234_5678_9ABC_DEF0UL, 5, "Enemy_C4_1_0", 0);
            Assert.That(PartRageRules.Seed(0x1234_5678_9ABC_DEF0UL, 5, "Enemy_C4_1_0", 0), Is.EqualTo(seed), "the same seed from the same floor and place");
            Assert.That(PartRageRules.Seed(0x1234_5678_9ABC_DEF0UL, 5, "Enemy_C4_1_1", 0), Is.Not.EqualTo(seed), "its neighbour has its own");
            Assert.That(PartRageRules.Seed(0x1234_5678_9ABC_DEF1UL, 5, "Enemy_C4_1_0", 0), Is.Not.EqualTo(seed), "another run's floor its own");

            foreach (EnemyKind kind in s_Kinds)
            {
                float width = PartRageRules.Degrees(kind, V);
                var a = new BoneBreakRandom(seed);
                var b = new BoneBreakRandom(seed);
                float least = float.PositiveInfinity, most = float.NegativeInfinity;
                for (int i = 0; i < 5000; i++)
                {
                    float x = PartRageRules.Angle(kind, true, a.Signed(), V);
                    float y = PartRageRules.Angle(kind, true, b.Signed(), V);
                    Assert.That(x, Is.EqualTo(y), kind + ": the same seed, the same angle");
                    Assert.That(Math.Abs(x), Is.LessThanOrEqualTo(width), kind + ": within its width");
                    least = Math.Min(least, x);
                    most = Math.Max(most, x);
                }
                if (width > 0f)
                {
                    Assert.That(least, Is.LessThan(-0.9f * width), kind + ": to the right");
                    Assert.That(most, Is.GreaterThan(0.9f * width), kind + ": and to the left");
                }
            }
            // Out-of-range numbers are held to the width.
            Assert.That(PartRageRules.Angle(EnemyKind.Shooter, true, 7.0, V), Is.EqualTo(V.ShotDegrees));
            Assert.That(PartRageRules.Angle(EnemyKind.Shooter, true, -7.0, V), Is.EqualTo(-V.ShotDegrees));
        }

        [Test]
        public void EachKind_HasItsOwnAim()
        {
            Assert.That(PartRageRules.Degrees(EnemyKind.Shooter, V), Is.EqualTo(V.ShotDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Caster, V), Is.EqualTo(V.ShotDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Warder, V), Is.EqualTo(V.ShotDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Lancer, V), Is.EqualTo(V.ChargeDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Brute, V), Is.EqualTo(V.StrikeDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Guard, V), Is.EqualTo(V.StrikeDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Swarmer, V), Is.EqualTo(V.StrikeDegrees));
            Assert.That(PartRageRules.Degrees(EnemyKind.Bomber, V), Is.EqualTo(0f), "its blast is a ring round itself");
            Assert.That(PartRageRules.Degrees(EnemyKind.Boss, V), Is.EqualTo(0f), "never rages");
        }

        [Test]
        public void ATurn_TurnsByItsAngle_AndAtNoneIsItselfExactly()
        {
            var d = new GroundPoint(0.3f, -0.95f);
            Assert.That(PartRageRules.Turn(d, 0f).X, Is.EqualTo(d.X));
            Assert.That(PartRageRules.Turn(d, 0f).Z, Is.EqualTo(d.Z));
            foreach (float deg in new[] { -12f, -6f, -0.5f, 0.5f, 6f, 12f })
            {
                GroundPoint t = PartRageRules.Turn(d, deg);
                Assert.That(PartRageRules.DegreesBetween(d, t), Is.EqualTo(Math.Abs(deg)).Within(1e-3f), deg + " degrees");
                Assert.That(t.Length, Is.EqualTo(d.Length).Within(1e-5f), "its length kept");
                float cross = d.X * t.Z - d.Z * t.X;
                Assert.That(Math.Sign(cross), Is.EqualTo(Math.Sign(deg)), "+ turns to the left seen from above");
            }
        }

        // ---------------- The switch off: today, field for field ----------------

        [Test]
        public void TheSwitchOff_EveryEnemyAttacksAsToday_FieldForField()
        {
            PartRageValues.On = false;
            foreach (EnemyKind kind in s_Kinds)
            {
                Assert.That(PartRageRules.RestShare(kind, true, V), Is.EqualTo(1f), kind + ": the whole rest");
                Assert.That(PartRageRules.Angle(kind, true, 1.0, V), Is.EqualTo(0f), kind + ": no angle");
                Assert.That(PartRageRules.Angle(kind, true, -0.37, V), Is.EqualTo(0f), kind + ": no angle");
            }

            // A brain told the rest share the rules give an enraged enemy with the switch off steps exactly as one never told,
            // through every state, for every kind's attacks, her near and far, coming and going.
            var attacks = new List<EnemyAttackDefinition>();
            attacks.AddRange(new ShooterValues().Arrows);
            attacks.AddRange(new CasterValues().Orbs);
            attacks.AddRange(new WarderValues().Enemy.Attacks);
            attacks.AddRange(new LancerValues().Enemy.Attacks);
            attacks.AddRange(MeleeAttacks.Brute());
            attacks.AddRange(MeleeAttacks.Guard());
            attacks.AddRange(MeleeAttacks.Swarmer());
            foreach (EnemyAttackDefinition attack in attacks)
            {
                var values = new EnemyValues { Health = 800, AggroRange = 14f, LeashRange = 26f, Attacks = new[] { attack } };
                var today = new EnemyBrain();
                var off = new EnemyBrain { RestShare = PartRageRules.RestShare(EnemyKind.Brute, true, V) };
                float near = Math.Max(attack.MinDistance, Math.Min(attack.MaxDistance, 0.5f * (attack.MinDistance + attack.MaxDistance)));
                for (int i = 0; i < 20000; i++)
                {
                    float t = i * 0.0013f;
                    float distance = (i / 3000) % 4 == 3 ? 30f : near; // now and then she leaves its leash
                    bool a = today.Step(t, distance, true, i < 200, values);
                    bool b = off.Step(t, distance, true, i < 200, values);
                    Assert.That(b, Is.EqualTo(a), attack.Name + " at " + t + ": the strike");
                    Assert.That(off.State, Is.EqualTo(today.State), attack.Name + " at " + t + ": the state");
                    Assert.That(off.Attack, Is.SameAs(today.Attack), attack.Name + " at " + t + ": the attack");
                    Assert.That(off.WindupRemaining(t), Is.EqualTo(today.WindupRemaining(t)), attack.Name + " at " + t + ": the wind-up");
                }
            }

            // And the aim: no angle, so every way is turned by nothing, bit for bit.
            var dir = new GroundPoint(-0.61f, 0.79f);
            GroundPoint same = PartRageRules.Turn(dir, PartRageRules.Angle(EnemyKind.Shooter, true, 0.8, V));
            Assert.That(same.X, Is.EqualTo(dir.X));
            Assert.That(same.Z, Is.EqualTo(dir.Z));
        }

        [Test]
        public void ACalmEnemy_AttacksAsToday_WithTheSwitchOn()
        {
            PartRageValues.On = true;
            foreach (EnemyKind kind in s_Kinds)
            {
                Assert.That(PartRageRules.RestShare(kind, false, V), Is.EqualTo(1f), kind + ": its whole rest");
                Assert.That(PartRageRules.Angle(kind, false, 1.0, V), Is.EqualTo(0f), kind.ToString());
            }
        }
    }
}
