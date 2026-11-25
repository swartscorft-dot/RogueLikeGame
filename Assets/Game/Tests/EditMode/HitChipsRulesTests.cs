using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 127 (Kristian, 2026-11-25: "kan vi drope at de flasher sånn når de blir truffet, heller at litt bein eller ligende
    /// spruter av. [...] du kan droppe den sirkelen når døde enemies despawner"): the rules of chips, not flashes. How many chips for
    /// which hit (more for a harder one, within bounds); a crit's spray larger and harder by itself; a fan of pellets into one body
    /// grows the spray but never past its cap; a hit that takes a part off throws half, a killing hit that breaks the body none; the
    /// chips' stuff by the body's kind; one bounce then rest, then shrink and gone in their time; the ring of slots takes the oldest;
    /// the switch on by default, and off giving back the flash and the ring.
    /// </summary>
    public class HitChipsRulesTests
    {
        static readonly HitChipsValues V = new HitChipsValues();

        [Test]
        public void TheSwitch_IsOnByDefault_TheRingItsOwnBool_Off_AndTheValuesAreFirstGuessesInTheirBounds()
        {
            Assert.That(HitChipsValues.On, Is.True, "on by default (the order's point 7)");
            Assert.That(HitChipsValues.RingWhenABodyGoes, Is.False, "the ring under it, off");
            Assert.That(V.ChipsLeast, Is.InRange(2, 10));
            Assert.That(V.ChipsMost, Is.GreaterThanOrEqualTo(V.ChipsLeast));
            Assert.That(V.MostPerTarget, Is.GreaterThanOrEqualTo(Count(1000, true)), "a crit's full spray fits under a target's cap");
            Assert.That(V.MostChips, Is.InRange(60, 600));
            Assert.That(V.Size, Is.InRange(0.08, 0.3), "seen at 28 m: about 6 to 21 pixels at 1080 rows");
            Assert.That(V.Bounce, Is.InRange(0.0, 1.0));
            Assert.That(V.LieSeconds, Is.GreaterThan(0));
            Assert.That(V.ShrinkSeconds, Is.GreaterThan(0));
            Assert.That(V.Gravity, Is.EqualTo(9.81).Within(1e-9), "falling by the world's gravity");
        }

        [Test]
        public void TheSwitchOff_GivesBackTheFlash_AndTheRing_On_NeitherUnlessTheRingIsAskedFor()
        {
            Assert.That(HitChipsRules.Flashes(false), Is.True, "off: the flash as before");
            Assert.That(HitChipsRules.Flashes(true), Is.False, "on: no flash");
            Assert.That(HitChipsRules.RingWhenABodyGoes(false, false), Is.True, "off: the ring as before");
            Assert.That(HitChipsRules.RingWhenABodyGoes(false, true), Is.True);
            Assert.That(HitChipsRules.RingWhenABodyGoes(true, false), Is.False, "on: no ring");
            Assert.That(HitChipsRules.RingWhenABodyGoes(true, true), Is.True, "on, and its own bool asks for it");
        }

        static int Count(int damage, bool crit) => HitChipsRules.Count(damage, crit, V);

        [Test]
        public void APlainHit_ThrowsMoreChipsForMoreDamage_WithinItsBounds()
        {
            Assert.That(Count(0, false), Is.EqualTo(V.ChipsLeast), "a hit of nothing still shows");
            Assert.That(Count(40, false), Is.EqualTo(3), "her basic shot: about 3 or 4 (the second round)");
            Assert.That(Count(80, false), Is.EqualTo(4));
            Assert.That(Count(200, false), Is.EqualTo(5));
            Assert.That(Count(100000, false), Is.EqualTo(V.ChipsMost), "however hard, a plain hit stays a spray");
            int last = 0;
            foreach (int d in new[] { 0, 20, 40, 80, 120, 200, 400 })
            {
                int n = Count(d, false);
                Assert.That(n, Is.GreaterThanOrEqualTo(last), "never fewer for a harder hit: " + d);
                last = n;
            }
            Assert.That(Count(-50, false), Is.EqualTo(V.ChipsLeast), "no damage below none");
        }

        [Test]
        public void ACrit_ReadsByItsSprayAlone_MoreChips_ThrownHarder_Larger()
        {
            foreach (int d in new[] { 0, 40, 80, 200, 1000 })
            {
                Assert.That(Count(d, true), Is.GreaterThanOrEqualTo(Count(d, false) * 1.5), "a crit's spray at least half as many again: " + d);
                Assert.That(Count(d, true), Is.GreaterThan(V.ChipsMost), "more than any plain hit, so it reads as a crit: " + d);
                if (d <= 150) // her hits: past them both are at the most a chip is thrown
                    Assert.That(HitChipsRules.Speed(d, true, V), Is.GreaterThan(HitChipsRules.Speed(d, false, V)), "thrown harder: " + d);
            }
            Assert.That(Count(80, true), Is.EqualTo(9), "her basic shot's crit (about 80): 4 x 1.6 + 3");
            Assert.That(HitChipsRules.SizeOf(true, 1, V), Is.GreaterThan(HitChipsRules.SizeOf(false, 1, V)), "a crit's chips larger");
        }

        [Test]
        public void TheSpeed_FollowsTheDamage_WithinItsBounds()
        {
            Assert.That(HitChipsRules.Speed(0, false, V), Is.EqualTo(V.SpeedBase).Within(1e-9));
            Assert.That(HitChipsRules.Speed(40, false, V), Is.EqualTo(V.SpeedBase + 40 * V.SpeedPerDamage).Within(1e-9));
            Assert.That(HitChipsRules.Speed(400, false, V), Is.GreaterThan(HitChipsRules.Speed(40, false, V)), "harder for a harder hit");
            Assert.That(HitChipsRules.Speed(100000, true, V), Is.EqualTo(V.SpeedMost).Within(1e-9), "at most");
            Assert.That(HitChipsRules.Speed(10, false, null), Is.EqualTo(0));
        }

        [Test]
        public void AFanOfPelletsIntoOneBody_GrowsItsSpray_NeverPastTheTargetsCap()
        {
            int one = HitChipsRules.ForFrame(1, 12, false, false, false, V);
            int six = HitChipsRules.ForFrame(6, 12, false, false, false, V);
            Assert.That(one, Is.EqualTo(Count(12, false)), "one pellet, one hit's spray");
            Assert.That(six, Is.GreaterThan(one), "six pellets throw more");
            Assert.That(six, Is.LessThan(one * 6), "but not six sprays: one spray, larger");
            Assert.That(HitChipsRules.ForFrame(40, 1000, true, false, false, V), Is.EqualTo(V.MostPerTarget), "never past the target's cap in a frame");
            Assert.That(HitChipsRules.ForFrame(2, 40, true, false, false, V), Is.EqualTo(Math.Min(V.MostPerTarget, Count(40, true) + V.ChipsPerMoreHit)),
                        "a crit among them makes it a crit's spray");
            Assert.That(HitChipsRules.ForFrame(0, 40, false, false, false, V), Is.EqualTo(0), "no hit, no chips");
        }

        [Test]
        public void TogetherWith122_APartOffThrowsHalf_AKillingHitThatBreaksTheBodyNone()
        {
            int plain = HitChipsRules.ForFrame(1, 40, false, false, false, V);
            Assert.That(HitChipsRules.ForFrame(1, 40, false, true, false, V), Is.EqualTo((int)Math.Floor(plain * V.PartOffShare)), "a part flies off: half the chips under it");
            Assert.That(HitChipsRules.ForFrame(1, 40, false, true, false, V), Is.GreaterThan(0), "still a few");
            Assert.That(HitChipsRules.ForFrame(1, 40, true, false, true, V), Is.EqualTo(0), "its body breaks into its pieces: those are the sign");
            Assert.That(HitChipsRules.ForFrame(3, 400, true, true, true, V), Is.EqualTo(0));
        }

        [Test]
        public void TheStuff_FollowsTheBody_BoneOnSkeletons_RobeOnTheHooded_SteelOnTheBoss()
        {
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Brute), Is.EqualTo(ChipStuff.Bone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Swarmer), Is.EqualTo(ChipStuff.Bone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Bomber), Is.EqualTo(ChipStuff.Bone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Shooter), Is.EqualTo(ChipStuff.Bone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Guard), Is.EqualTo(ChipStuff.GreyBone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Lancer), Is.EqualTo(ChipStuff.RedBone));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Caster), Is.EqualTo(ChipStuff.Robe));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Warder), Is.EqualTo(ChipStuff.Robe));
            Assert.That(HitChipsRules.StuffOf(EnemyKind.Boss), Is.EqualTo(ChipStuff.Armour));
            Assert.That(HitChipsRules.Second(ChipStuff.Robe), Is.EqualTo(ChipColour.Bone), "a hooded one: cloth and bone");
            Assert.That(HitChipsRules.First(ChipStuff.Armour), Is.EqualTo(ChipColour.Steel));
            foreach (ChipStuff s in new[] { ChipStuff.Bone, ChipStuff.GreyBone, ChipStuff.RedBone, ChipStuff.Robe, ChipStuff.Armour })
            {
                ChipColour main = HitChipsRules.MainOf(s, 0.99, V);
                Assert.That(main, Is.EqualTo(HitChipsRules.First(s)), s + ": its own colour");
                ChipColour end = HitChipsRules.EndOf(main);
                Assert.That(end, Is.Not.EqualTo(main), s + ": two tones, its broken ends darker");
                Rgb a = HitChipsRules.ColourOf(main, V), b = HitChipsRules.ColourOf(end, V);
                Assert.That(a.R + a.G + a.B, Is.GreaterThan((b.R + b.G + b.B) * 1.2f), s + ": the ends clearly darker");
            }
            Assert.That(HitChipsRules.MainOf(ChipStuff.Robe, 0.0, V), Is.EqualTo(ChipColour.Bone), "a hooded one: some bone among the cloth");
            Assert.That(HitChipsRules.MainOf(ChipStuff.Bone, 0.0, V), Is.EqualTo(ChipColour.Bone));
            Assert.That(HitChipsRules.MainOf(ChipStuff.Armour, 0.0, V), Is.EqualTo(ChipColour.Steel), "the boss: steel only");
            Rgb robe = V.Robe;
            Assert.That(Math.Max(robe.R, Math.Max(robe.G, robe.B)), Is.GreaterThan(0.75f), "robe scraps light enough to be seen on a dark robe and floor");
            foreach (ChipColour c in Enum.GetValues(typeof(ChipColour)).Cast<ChipColour>())
            {
                Rgb rgb = HitChipsRules.ColourOf(c, V);
                float bright = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
                Assert.That(bright, Is.GreaterThan(0.45f), c + ": bright enough to be seen on the crypt's floor");
            }
            Assert.That(Enum.GetValues(typeof(ChipColour)).Length, Is.EqualTo(HitChipsRules.ColourCount));
        }

        [Test]
        public void TheSpray_LandsOnItsEnemy_APlainHitsWithinAbout06m_ACritsWithinAbout1m()
        {
            // The fastest chip of each (its speed and its jitter, straight along the shot, its least lift), from about her chest's
            // height on a skeleton (1.4 m), leaving from the body's skin on her side, about 0.4 m before its middle.
            const double height = 1.4, before = 0.4;
            double plain = HitChipsRules.Speed(Placeholders.BasicShotDamage, false, V) * (1 + V.SpeedJitter);
            double crit = HitChipsRules.Speed(2 * Placeholders.BasicShotDamage, true, V) * (1 + V.SpeedJitter);
            double plainReach = HitChipsRules.Reach(plain, V.UpBase + V.UpShare * plain, height, V) - before;
            double critReach = HitChipsRules.Reach(crit, V.UpBase + V.UpShare * crit, height, V) - before;
            Assert.That(plainReach, Is.LessThanOrEqualTo(0.65), "a plain hit's chips land within about 0.6 m beyond the body's middle: " + plainReach.ToString("0.00"));
            Assert.That(critReach, Is.LessThanOrEqualTo(1.1), "a crit's within about 1 m: " + critReach.ToString("0.00"));
            Assert.That(critReach, Is.GreaterThan(plainReach), "a crit's farther");
            Assert.That(HitChipsRules.Reach(1, 0, 0, V), Is.EqualTo(0).Within(1e-9), "from the floor, none");
        }

        [Test]
        public void AChip_BouncesOnce_IfItFallsFastEnough_ThenLies()
        {
            Assert.That(HitChipsRules.BounceUp(4, false, V), Is.EqualTo(4 * V.Bounce).Within(1e-9), "the first time it meets the floor");
            Assert.That(HitChipsRules.BounceUp(4, true, V), Is.EqualTo(0), "once");
            Assert.That(HitChipsRules.BounceUp(V.RestSpeed * 0.5, false, V), Is.EqualTo(0), "too slow to bounce: it lies");
            Assert.That(HitChipsRules.Next(ChipState.Flying, 0.3, 0, true, V), Is.EqualTo(ChipState.Lying));
            Assert.That(HitChipsRules.Next(ChipState.Flying, 0.3, 0, false, V), Is.EqualTo(ChipState.Flying));
            Assert.That(HitChipsRules.Next(ChipState.Flying, V.MostFlySeconds, 0, false, V), Is.EqualTo(ChipState.Lying), "one that never finds its floor goes anyway");
        }

        [Test]
        public void AChip_LiesItsTime_ShrinksAway_AndIsGone()
        {
            Assert.That(HitChipsRules.Shown(0, V), Is.EqualTo(1));
            Assert.That(HitChipsRules.Shown(V.LieSeconds, V), Is.EqualTo(1));
            Assert.That(HitChipsRules.Shown(V.LieSeconds + V.ShrinkSeconds * 0.5, V), Is.EqualTo(0.5).Within(1e-6));
            Assert.That(HitChipsRules.Shown(V.LieSeconds + V.ShrinkSeconds, V), Is.EqualTo(0).Within(1e-9));
            Assert.That(HitChipsRules.Next(ChipState.Lying, 1, V.LieSeconds, false, V), Is.EqualTo(ChipState.Lying));
            Assert.That(HitChipsRules.Next(ChipState.Lying, 2, V.LieSeconds + V.ShrinkSeconds, false, V), Is.EqualTo(ChipState.Free), "gone after its time");
            Assert.That(HitChipsRules.Next(ChipState.Free, 9, 9, true, V), Is.EqualTo(ChipState.Free));
        }

        [Test]
        public void TheRingOfSlots_TakesEachInTurn_SoAFullRingGivesUpItsOldest()
        {
            const int cap = 5;
            Assert.That(Enumerable.Range(0, 12).Select(i => HitChipsRules.Slot(i, cap)), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1 }));
            // The chip thrown as the sixth takes the first's slot: the first is the oldest living.
            Assert.That(HitChipsRules.Slot(5, cap), Is.EqualTo(HitChipsRules.Slot(0, cap)));
            Assert.That(HitChipsRules.Slot(long.MaxValue - 1, 240), Is.InRange(0, 239), "far past any play session");
            Assert.That(HitChipsRules.Slot(7, 0), Is.EqualTo(0));
        }

        [Test]
        public void ASize_FollowsTheEnemysShownScale_WithinBounds()
        {
            Assert.That(HitChipsRules.SizeOf(false, 1, V), Is.EqualTo(V.Size).Within(1e-9));
            Assert.That(HitChipsRules.SizeOf(false, 4.0 / 3.0, V), Is.EqualTo(V.Size * 4.0 / 3.0).Within(1e-9), "an elite's chips as much larger as it is");
            Assert.That(HitChipsRules.SizeOf(false, 10, V), Is.EqualTo(V.Size * V.MostSizeScale).Within(1e-9), "within bounds");
            Assert.That(HitChipsRules.SizeOf(false, 0.5, V), Is.EqualTo(V.Size).Within(1e-9), "never smaller than a chip");
        }
    }
}
