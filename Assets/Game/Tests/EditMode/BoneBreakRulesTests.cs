using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 122 (2026-11-25, "bein flyr rund ... Gjerne knuse i biter"): the rules of bones that fly. Which part comes off
    /// when (a part each third of health lost, two at most, the skull first and then the near shin, never an arm); how hard a hit
    /// throws (more for more damage, within its bounds); which kinds break (all but the boss; the bomber at its blast); the pieces'
    /// bones (every one of the models' 24 bones in a piece, a lost part's own bone at its root); the caps and the clean-up (the
    /// pool takes a free slot, then a new one up to its cap, then the oldest; a piece flies, rests, sinks and is gone in its time);
    /// breaking again only once; the mass by size; the switch on by default; its random stream its own.
    /// </summary>
    public class BoneBreakRulesTests
    {
        static readonly BoneBreakValues V = new BoneBreakValues();

        static readonly string[] s_Bones =
        {
            "Hips", "Spine02", "Spine01", "Spine", "neck", "Head", "head_end", "headfront",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        };

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheValuesAreFirstGuessesInTheirBounds()
        {
            Assert.That(BoneBreakValues.On, Is.True, "on by default (the order's point 7)");
            Assert.That(V.PartShare, Is.InRange(0.1, 0.5));
            Assert.That(V.PartsBeforeDeath, Is.InRange(1, 3));
            Assert.That(V.MostPieces, Is.InRange(40, 400));
            Assert.That(V.LieSeconds, Is.GreaterThan(0));
            Assert.That(V.SinkSeconds, Is.GreaterThan(0));
            Assert.That(V.ThrowMost, Is.GreaterThan(V.ThrowBase));
            Assert.That(V.StaticFriction, Is.GreaterThanOrEqualTo(V.DynamicFriction));
            Assert.That(V.Bounce, Is.InRange(0.0, 1.0));
        }

        // ---------------- Which part comes off when ----------------

        [TestCase(1000, 0)]
        [TestCase(700, 0)]
        [TestCase(667, 0)]
        [TestCase(666, 1)]
        [TestCase(400, 1)]
        [TestCase(334, 1)]
        [TestCase(333, 2)]
        [TestCase(1, 2)]
        [TestCase(0, 2)]
        public void APart_ForEachThirdOfHealthLost_TwoAtMost(int health, int due)
        {
            Assert.That(BoneBreakRules.PartsDue(health, 1000, V), Is.EqualTo(due));
        }

        [Test]
        public void PartsDue_NeverFallsAsHealthFalls_AndNoneWithoutAMaximum()
        {
            int last = 0;
            for (int h = 3000; h >= 0; h -= 7)
            {
                int n = BoneBreakRules.PartsDue(h, 3000, V);
                Assert.That(n, Is.GreaterThanOrEqualTo(last), "at " + h);
                last = n;
            }
            Assert.That(BoneBreakRules.PartsDue(0, 0, V), Is.EqualTo(0));
            Assert.That(BoneBreakRules.PartsDue(5000, 3000, V), Is.EqualTo(0), "over its maximum: none");
            Assert.That(BoneBreakRules.PartsDue(10, 3000, new BoneBreakValues { PartsBeforeDeath = 0 }), Is.EqualTo(0));
        }

        [Test]
        public void TheSkullGoesFirst_ThenTheShinOnHerSide_ThenTheOther_NeverAnArm()
        {
            Assert.That(BoneBreakRules.AlivePart(0, true), Is.EqualTo(BonePart.Skull));
            Assert.That(BoneBreakRules.AlivePart(0, false), Is.EqualTo(BonePart.Skull));
            Assert.That(BoneBreakRules.AlivePart(1, true), Is.EqualTo(BonePart.ShinLeft));
            Assert.That(BoneBreakRules.AlivePart(1, false), Is.EqualTo(BonePart.ShinRight));
            Assert.That(BoneBreakRules.AlivePart(2, true), Is.EqualTo(BonePart.ShinRight));
            var arms = new[] { BonePart.UpperArmLeft, BonePart.UpperArmRight, BonePart.ForearmLeft, BonePart.ForearmRight };
            for (int i = 0; i < 5; i++)
                foreach (bool left in new[] { true, false })
                    Assert.That(arms, Has.No.Member(BoneBreakRules.AlivePart(i, left)), "an arm carries its weapon or its wind-up");
        }

        [Test]
        public void EveryKindWithASkeletonsBody_Breaks_NotTheBoss_AndTheBomberAtItsBlast()
        {
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind)))
                Assert.That(BoneBreakRules.Breaks(k), Is.EqualTo(k != EnemyKind.Boss), k.ToString());
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind)))
                Assert.That(BoneBreakRules.WaitsForItsBlast(k), Is.EqualTo(k == EnemyKind.Bomber), k.ToString());
        }

        // ---------------- How hard ----------------

        [Test]
        public void AHarderHit_ThrowsHarder_WithinItsBounds()
        {
            double last = -1;
            for (int d = 0; d <= 2000; d += 10)
            {
                double s = BoneBreakRules.ThrowSpeed(d, V);
                Assert.That(s, Is.GreaterThanOrEqualTo(last));
                Assert.That(s, Is.InRange(V.ThrowBase, V.ThrowMost));
                last = s;
            }
            Assert.That(BoneBreakRules.ThrowSpeed(40, V), Is.EqualTo(V.ThrowBase + 40 * V.ThrowPerDamage).Within(1e-9), "her basic shot");
            Assert.That(BoneBreakRules.ThrowSpeed(400, V), Is.GreaterThan(BoneBreakRules.ThrowSpeed(40, V) * 2), "a skill's hit, much harder");
            Assert.That(BoneBreakRules.ThrowSpeed(100000, V), Is.EqualTo(V.ThrowMost));
            Assert.That(BoneBreakRules.ThrowSpeed(-5, V), Is.EqualTo(V.ThrowBase));
        }

        [Test]
        public void Mass_GrowsWithSize_WithinItsBounds()
        {
            double small = BoneBreakRules.Mass(0.05, 0.05, 0.1, V), big = BoneBreakRules.Mass(0.3, 0.4, 0.25, V);
            Assert.That(big, Is.GreaterThan(small));
            Assert.That(BoneBreakRules.Mass(0, 0, 0, V), Is.EqualTo(V.MassLeast));
            Assert.That(BoneBreakRules.Mass(10, 10, 10, V), Is.EqualTo(V.MassMost));
            Assert.That(BoneBreakRules.Mass(0.2, 0.2, 0.2, V), Is.EqualTo(0.008 * V.BoneDensity * V.FillShare).Within(1e-9));
        }

        // ---------------- The pieces' bones ----------------

        [Test]
        public void EveryOneOfTheModels24Bones_IsInAPiece_AndEachPartsRootBoneIsItsOwn()
        {
            foreach (string b in s_Bones)
                Assert.That(BoneParts.Of(b), Is.InRange(0, BoneParts.Count - 1), b);
            Assert.That(s_Bones.Select(BoneParts.Of).Distinct().Count(), Is.EqualTo(BoneParts.Count), "every piece has a bone");
            Assert.That(BoneParts.Of("mixamorig:LeftHand"), Is.EqualTo((int)BonePart.ForearmLeft), "a prefix before ':' is read past");
            Assert.That(BoneParts.Of("Armature|Head"), Is.EqualTo((int)BonePart.Skull));
            Assert.That(BoneParts.Of("Bow"), Is.EqualTo(-1), "not a bone of the 24");
            Assert.That(BoneParts.Of(null), Is.EqualTo(-1));
            foreach (BonePart p in Enum.GetValues(typeof(BonePart)))
                Assert.That(BoneParts.Of(BoneParts.RootBone(p)), Is.EqualTo((int)p), p + "'s root bone is in it");
            Assert.That(BoneParts.RootBone(BonePart.Skull), Is.EqualTo("Head"));
            Assert.That(BoneParts.RootBone(BonePart.ShinLeft), Is.EqualTo("LeftLeg"));
        }

        // ---------------- The caps and the clean-up ----------------

        [Test]
        public void ThePool_TakesAFreeSlot_ThenANewOne_UpToItsCap_ThenTheOldest()
        {
            var v = new BoneBreakValues { MostPieces = 4 };
            var states = new BonePieceState[4];
            var born = new double[4];
            int made = 0;
            for (int i = 0; i < 4; i++)
            {
                int s = BoneBreakRules.Slot(states, born, made, v);
                Assert.That(s, Is.EqualTo(made), "a new slot while under the cap");
                states[s] = BonePieceState.Flying;
                born[s] = 10 + i;
                made++;
            }
            born[2] = 1; // the oldest
            Assert.That(BoneBreakRules.Slot(states, born, made, v), Is.EqualTo(2), "full: the oldest");
            states[3] = BonePieceState.Free;
            Assert.That(BoneBreakRules.Slot(states, born, made, v), Is.EqualTo(3), "a free one first");
            states[3] = BonePieceState.Resting;
            born[0] = 0.5;
            Assert.That(BoneBreakRules.Slot(states, born, made, v), Is.EqualTo(0), "whatever it is doing, the oldest");

            // Slots made beforehand (the pool made whole as the first skeleton appears): free ones are not taken past the cap.
            var many = new BonePieceState[10];
            var when = new double[10];
            for (int i = 0; i < 4; i++) { many[i] = BonePieceState.Flying; when[i] = 5 - i; }
            Assert.That(BoneBreakRules.Slot(many, when, 10, v), Is.EqualTo(3), "four living, the cap: the oldest, not a free one");
            many[1] = BonePieceState.Free;
            Assert.That(BoneBreakRules.Slot(many, when, 10, v), Is.EqualTo(1), "under the cap: the first free one");
        }

        [Test]
        public void APiece_Flies_Rests_Sinks_AndIsGone_InItsTime()
        {
            Assert.That(BoneBreakRules.Next(BonePieceState.Flying, 1, 0, 1, 1, V), Is.EqualTo(BonePieceState.Flying));
            Assert.That(BoneBreakRules.Next(BonePieceState.Flying, 1, V.RestHold, 1, 0.1, V), Is.EqualTo(BonePieceState.Resting), "still long enough: at rest");
            Assert.That(BoneBreakRules.Next(BonePieceState.Flying, V.MostFlySeconds, 0, V.MostFlySeconds, 1, V), Is.EqualTo(BonePieceState.Sinking), "too long moving: it sinks anyway");
            Assert.That(BoneBreakRules.Next(BonePieceState.Flying, 1, 0, 1, V.FallOutBelow - 1, V), Is.EqualTo(BonePieceState.Free), "out of the world: gone at once");
            Assert.That(BoneBreakRules.Next(BonePieceState.Resting, 9, 9, V.LieSeconds - 0.01, 0, V), Is.EqualTo(BonePieceState.Resting));
            Assert.That(BoneBreakRules.Next(BonePieceState.Resting, 9, 9, V.LieSeconds, 0, V), Is.EqualTo(BonePieceState.Sinking));
            Assert.That(BoneBreakRules.Next(BonePieceState.Sinking, 9, 9, V.SinkSeconds - 0.01, 0, V), Is.EqualTo(BonePieceState.Sinking));
            Assert.That(BoneBreakRules.Next(BonePieceState.Sinking, 9, 9, V.SinkSeconds, 0, V), Is.EqualTo(BonePieceState.Free));
            Assert.That(BoneBreakRules.Next(BonePieceState.Free, 99, 99, 99, -99, V), Is.EqualTo(BonePieceState.Free));
            Assert.That(BoneBreakRules.Still(V.RestSpeed * 0.5, V.RestSpin * 0.5, V), Is.True);
            Assert.That(BoneBreakRules.Still(V.RestSpeed * 2, 0, V), Is.False);
            Assert.That(BoneBreakRules.Still(0, V.RestSpin * 2, V), Is.False);
        }

        [Test]
        public void APiecesWholeLife_IsBounded_ByItsValues()
        {
            // Simulated by the rule alone: a piece that never stops moving is gone by its most flying time, its lie and its sink.
            const double dt = 0.02;
            BonePieceState s = BonePieceState.Flying;
            double age = 0, since = 0;
            while (s != BonePieceState.Free && age < 100)
            {
                age += dt;
                since += dt;
                BonePieceState next = BoneBreakRules.Next(s, age, 0, since, 0.1, V);
                if (next != s) since = 0;
                s = next;
            }
            Assert.That(age, Is.LessThanOrEqualTo(V.MostFlySeconds + V.SinkSeconds + 3 * dt));
        }

        // ---------------- Breaking again ----------------

        [Test]
        public void AWholePiece_BreaksAgainOnAHardLanding_Once()
        {
            Assert.That(BoneBreakRules.Shatters(true, true, V.ShatterSpeed, V), Is.True);
            Assert.That(BoneBreakRules.Shatters(true, true, V.ShatterSpeed - 0.01, V), Is.False, "too soft");
            Assert.That(BoneBreakRules.Shatters(false, true, 100, V), Is.False, "a half never breaks again");
            Assert.That(BoneBreakRules.Shatters(true, false, 100, V), Is.False, "nothing to cut it into");
            Assert.That(BoneBreakRules.CanBeCut(V.ShatterLeastSize, V.ShatterLeastTriangles, V), Is.True);
            Assert.That(BoneBreakRules.CanBeCut(V.ShatterLeastSize * 0.9, 1000, V), Is.False, "too small");
            Assert.That(BoneBreakRules.CanBeCut(1, V.ShatterLeastTriangles - 1, V), Is.False, "too few triangles");
        }

        // ---------------- Its random stream ----------------

        [Test]
        public void ItsRandom_IsItsOwn_TheSameForTheSameSeed_AndSpread()
        {
            var a = new BoneBreakRandom(42);
            var b = new BoneBreakRandom(42);
            var c = new BoneBreakRandom(43);
            var seen = new List<double>();
            bool differs = false;
            for (int i = 0; i < 1000; i++)
            {
                double x = a.Next();
                Assert.That(x, Is.EqualTo(b.Next()));
                differs |= x != c.Next();
                Assert.That(x, Is.InRange(0.0, 1.0));
                Assert.That(x, Is.Not.EqualTo(1.0));
                seen.Add(x);
            }
            Assert.That(differs, Is.True);
            Assert.That(seen.Average(), Is.InRange(0.45, 0.55));
            Assert.That(seen.Count(x => x < 0.1), Is.InRange(60, 140));
        }
    }
}
