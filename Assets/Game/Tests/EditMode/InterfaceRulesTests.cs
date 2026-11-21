using System;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 045 (decision 0011, Kristian's choices of 2026-11-20): the interface's kinetic skin as plain rules. Sizes
    /// at 720 and 1080; where the strokes sit under her for a given screen place, and the blade over her head; which words
    /// get a plate and which colour a word or a number gets; how a skill's slot shows and its cooldown's number; the strip
    /// and the cards in their places; the white of lost health; the icon's mask (brightness to alpha) and the names icons are
    /// found by for every skill in the pool.
    /// </summary>
    public class InterfaceRulesTests
    {
        static InterfaceValues V => new InterfaceValues();

        // ---------------- Sizes ----------------

        [Test]
        public void TheSkinIsOnByDefault() => Assert.That(V.Kinetic, Is.True);

        [Test]
        public void SizesScaleWithTheHeight_OneAt1080_TwoThirdsAt720()
        {
            Assert.That(InterfaceRules.Scale(1080f, V), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(InterfaceRules.Scale(720f, V), Is.EqualTo(2f / 3f).Within(1e-6f));
            Assert.That(InterfaceRules.Px(V.StrokeWidth, 1080f, V), Is.EqualTo(V.StrokeWidth).Within(1e-4f));
            Assert.That(InterfaceRules.Px(V.StrokeWidth, 720f, V), Is.EqualTo(V.StrokeWidth * 2f / 3f).Within(1e-4f));
            Assert.That(InterfaceRules.Px(V.SlotWidth, 2160f, V), Is.EqualTo(V.SlotWidth * 2f).Within(1e-4f), "and twice at 4K");
        }

        [Test]
        public void ItIsSmall_ThePiecesAtTheFigureAreThin()
        {
            InterfaceValues v = V;
            Assert.That(v.StrokeHeight, Is.LessThanOrEqualTo(10f), "thin strokes");
            Assert.That(2f * v.StrokeWidth + v.StrokeGap, Is.LessThan(200f), "about the width of her shadow");
            Assert.That(v.BladeWidth, Is.LessThan(200f), "a short blade");
            float strip = InterfaceRules.Slot(7, 8, 1920f, v).Right - InterfaceRules.Slot(0, 8, 1920f, v).X;
            // Work order 063 ("en del større"): 1.6 times the boxes, so under 45 % now (it was under a third).
            Assert.That(strip, Is.LessThan(1920f * 0.45f), "the eight skills under 45 % of a 1080p view's width");
        }

        // ---------------- 1. At the figure ----------------

        [TestCase(960f, 600f)]
        [TestCase(300f, 900f)]
        public void TheStrokes_SitSideBySide_JustUnderHerFeet_CentredOnHer(float feetX, float feetY)
        {
            InterfaceValues v = V;
            UiBox health = InterfaceRules.HealthStroke(feetX, feetY, v);
            UiBox focus = InterfaceRules.FocusStroke(feetX, feetY, v);
            float s = InterfaceRules.Fight(v); // work order 063: grown with the master scale
            Assert.That(health.Y, Is.EqualTo(feetY + v.UnderFeet * s).Within(1e-4f), "just under her feet");
            Assert.That(focus.Y, Is.EqualTo(health.Y), "side by side");
            Assert.That(health.W, Is.EqualTo(v.StrokeWidth * s).Within(1e-4f));
            Assert.That(health.H, Is.EqualTo(v.StrokeHeight * s).Within(1e-4f));
            Assert.That(focus.X, Is.EqualTo(health.Right + v.StrokeGap * s).Within(1e-4f), "health left, focus right, a gap between");
            Assert.That((health.X + focus.Right) * 0.5f, Is.EqualTo(feetX).Within(1e-3f), "centred on her");
            UiBox number = InterfaceRules.HealthNumber(feetX, feetY, v);
            Assert.That(number.X, Is.GreaterThan(focus.Right), "the one number beside them");
            Assert.That(number.CenterY, Is.EqualTo(focus.CenterY).Within(1e-3f));
        }

        [Test]
        public void TheBlade_SitsJustOverHerHead_Centred()
        {
            InterfaceValues v = V;
            UiBox b = InterfaceRules.Blade(800f, 400f, v);
            Assert.That(b.CenterX, Is.EqualTo(800f).Within(1e-3f));
            Assert.That(b.Bottom, Is.EqualTo(400f - v.OverHead * InterfaceRules.Fight(v)).Within(1e-3f), "its bottom over the top of her head");
            Assert.That(b.W, Is.EqualTo(v.BladeWidth * InterfaceRules.BladeFactor(v)).Within(1e-3f), "work order 063: grown with the master scale and its own factor");
        }

        [Test]
        public void TheWhiteOfLostHealth_ReachesUpToWhereItWas_AndFades()
        {
            InterfaceValues v = V;
            float from = InterfaceRules.LostFrom(0.9f, 0.9f, 0.6f, 10f, v);
            Assert.That(from, Is.EqualTo(0.9f).Within(1e-5f), "a loss: from the health before");
            Assert.That(InterfaceRules.LostAlpha(0f, v), Is.EqualTo(1f));
            Assert.That(InterfaceRules.LostAlpha(v.LostSeconds * 0.5f, v), Is.EqualTo(0.5f).Within(1e-4f), "fading");
            Assert.That(InterfaceRules.LostAlpha(v.LostSeconds, v), Is.Zero, "gone");
            Assert.That(InterfaceRules.LostFrom(0.9f, 0.6f, 0.4f, 0.1f, v), Is.EqualTo(0.9f).Within(1e-5f), "a second loss while it shows keeps the top");
            Assert.That(InterfaceRules.LostFrom(0.9f, 0.6f, 0.6f, 0.2f, v), Is.EqualTo(0.9f).Within(1e-5f), "held while it fades");
            Assert.That(InterfaceRules.LostFrom(0.9f, 0.6f, 0.6f, v.LostSeconds + 0.1f, v), Is.EqualTo(0.6f).Within(1e-5f), "let go once faded");
            Assert.That(InterfaceRules.LostFrom(0.9f, 0.6f, 1f, 0.1f, v), Is.EqualTo(1f).Within(1e-5f), "and when health is back over it");
        }

        // ---------------- 3. The strip ----------------

        [Test]
        public void TheStrip_IsCentredAtTheBottom_WithItsEndsOutside()
        {
            InterfaceValues v = V;
            UiBox first = InterfaceRules.Slot(0, 8, 1920f, v), last = InterfaceRules.Slot(7, 8, 1920f, v);
            Assert.That((first.X + last.Right) * 0.5f, Is.EqualTo(960f).Within(1e-3f), "bottom centre");
            Assert.That(first.Bottom, Is.EqualTo(v.LayoutHeight - InterfaceRules.StripBottomEdge(v)).Within(1e-3f), "work order 063: lifted");
            Assert.That(InterfaceRules.Slot(1, 8, 1920f, v).X, Is.EqualTo(first.Right + v.SlotGap * InterfaceRules.Strip(v)).Within(1e-3f));
            Assert.That(InterfaceRules.LeftEnd(8, 1920f, v).Right, Is.LessThan(first.X), "the dash left of the slots");
            Assert.That(InterfaceRules.RightEnd(8, 1920f, v).X, Is.GreaterThan(last.Right), "focus right of them");
        }

        [Test]
        public void ASlot_IsTeal_WhenSomethingWaits_GreyOnCooldown_WhiteReady()
        {
            Assert.That(InterfaceRules.Look(false, false, false, 0f), Is.EqualTo(SlotLook.Empty));
            Assert.That(InterfaceRules.Look(true, false, false, 0f), Is.EqualTo(SlotLook.Ready));
            Assert.That(InterfaceRules.Look(true, false, false, 2f), Is.EqualTo(SlotLook.Cooling));
            Assert.That(InterfaceRules.Look(true, false, true, 2f), Is.EqualTo(SlotLook.Live), "a recast or a charge waiting");
            Assert.That(InterfaceRules.Look(true, true, false, 0f), Is.EqualTo(SlotLook.Live), "in use");
            Assert.That(InterfaceRules.Sweep(1.5f, 3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(InterfaceRules.Sweep(0f, 3f), Is.Zero);
        }

        [TestCase(3.2f, "3.2")]
        [TestCase(3.21f, "3.3")]
        [TestCase(0.04f, "0.1")]
        [TestCase(9.95f, "10")]
        [TestCase(12.3f, "13")]
        [TestCase(0f, "")]
        public void TheCooldownsNumber_TenthsUnderTen_WholeSecondsFromTen_NeverZeroWhileWaiting(float seconds, string shown) =>
            Assert.That(InterfaceRules.CooldownText(seconds), Is.EqualTo(shown));

        // ---------------- 5. A choice ----------------

        [Test]
        public void TheCards_StackAtTheRightEdge_WithTheirNumerals()
        {
            InterfaceValues v = V;
            for (int i = 0; i < 3; i++)
            {
                UiBox c = InterfaceRules.Card(i, 3, v.CardHeight, 1920f, v);
                Assert.That(c.Right + InterfaceRules.Lean(v.CardHeight, v), Is.EqualTo(1920f - v.CardRight).Within(1e-3f), "at the right edge, its lean inside");
                Assert.That(c.W, Is.LessThan(1920f * 0.2f), "slim");
                if (i > 0) Assert.That(c.Y, Is.EqualTo(InterfaceRules.Card(i - 1, 3, v.CardHeight, 1920f, v).Bottom + v.CardGap).Within(1e-3f), "stacked down");
            }
            Assert.That(InterfaceRules.Numeral(0), Is.EqualTo("01"));
            Assert.That(InterfaceRules.Numeral(2), Is.EqualTo("03"));
        }

        [Test]
        public void AChestCardsTier_ColoursItsNumeral_WhiteCommon_TealRare_GoldEpic()
        {
            Assert.That(InterfaceRules.TierInk(true, UpgradeTier.Common), Is.EqualTo(UiInk.White));
            Assert.That(InterfaceRules.TierInk(true, UpgradeTier.Rare), Is.EqualTo(UiInk.Teal));
            Assert.That(InterfaceRules.TierInk(true, UpgradeTier.Epic), Is.EqualTo(UiInk.Gold));
            Assert.That(InterfaceRules.TierInk(false, UpgradeTier.Epic), Is.EqualTo(UiInk.White), "an upgrade with no tier: white");
        }

        // ---------------- 6. Words and numbers ----------------

        [Test]
        public void OnlyPerfectAndCounter_GetAPlate()
        {
            foreach (FeedbackMoment m in Enum.GetValues(typeof(FeedbackMoment)))
                Assert.That(InterfaceRules.HasPlate(m), Is.EqualTo(m == FeedbackMoment.Perfect || m == FeedbackMoment.Counter), m.ToString());
            Assert.That(InterfaceRules.PlateInk(FeedbackMoment.Perfect), Is.EqualTo(UiInk.Teal), "teal, as on the sheet");
            Assert.That(InterfaceRules.PlateInk(FeedbackMoment.Counter), Is.EqualTo(UiInk.White));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Perfect, false), Is.EqualTo(UiInk.Black), "the word on its plate black");
        }

        [Test]
        public void TheBareWords_AreInTheirColours()
        {
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Clean, false), Is.EqualTo(UiInk.White));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Early, false), Is.EqualTo(UiInk.Red));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Late, false), Is.EqualTo(UiInk.Late));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Ducked, false), Is.EqualTo(UiInk.Teal));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Stunned, false), Is.EqualTo(UiInk.Red));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.Pulled, false), Is.EqualTo(UiInk.Red));
            Assert.That(InterfaceRules.WordInk(FeedbackMoment.MarksSpent, true), Is.EqualTo(UiInk.Red), "the marks at the cap red");
            Assert.That(InterfaceRules.WordSize(FeedbackSize.Large, V), Is.GreaterThan(InterfaceRules.WordSize(FeedbackSize.Normal, V)));
            Assert.That(InterfaceRules.WordSize(FeedbackSize.Small, V), Is.LessThan(InterfaceRules.WordSize(FeedbackSize.Normal, V)));
        }

        [Test]
        public void ANumbersColour_OrdinaryWhite_StrongTeal_PerfectWhiteOverARedSlash_HersRed()
        {
            InterfaceValues v = V;
            Assert.That(InterfaceRules.NumberInk(NumberKind.Plain), Is.EqualTo(UiInk.White));
            Assert.That(InterfaceRules.NumberInk(NumberKind.Marks), Is.EqualTo(UiInk.Teal));
            Assert.That(InterfaceRules.NumberInk(NumberKind.MarksFull), Is.EqualTo(UiInk.Teal));
            Assert.That(InterfaceRules.NumberInk(NumberKind.Dazed), Is.EqualTo(UiInk.Teal));
            Assert.That(InterfaceRules.NumberInk(NumberKind.Perfect), Is.EqualTo(UiInk.White));
            Assert.That(InterfaceRules.NumberSlash(NumberKind.Perfect), Is.True);
            Assert.That(InterfaceRules.NumberSlash(NumberKind.Plain), Is.False);
            Assert.That(InterfaceRules.NumberSlash(NumberKind.Marks), Is.False, "no plate or slash behind any other number");
            Assert.That(InterfaceRules.NumberInk(NumberKind.Hurt), Is.EqualTo(UiInk.Red));
            Assert.That(InterfaceRules.NumberSize(NumberKind.Plain, v), Is.LessThan(InterfaceRules.NumberSize(NumberKind.Marks, v)), "strong a little larger");
            Assert.That(InterfaceRules.NumberSize(NumberKind.Marks, v), Is.LessThan(v.WordSize), "numbers smaller than the words: calmer than the sheet");
        }

        [Test]
        public void EveryWord_KnowsItsMoment()
        {
            foreach (FeedbackMoment m in Enum.GetValues(typeof(FeedbackMoment)))
                Assert.That(FeedbackRules.Word(m, 3, 5).Moment, Is.EqualTo(m), m.ToString());
        }

        // ---------------- The icons ----------------

        [Test]
        public void TheIcons_AreFoundByTheSkillsNameWithoutSpaces_ForEverySkillInThePool()
        {
            string[] names = SkillDefaults.Create().Select(s => s.Name).ToArray();
            string[] icons = names.Select(InterfaceRules.IconName).ToArray();
            Assert.That(icons, Is.EquivalentTo(new[]
            {
                "QuickShot", "PowderKeg", "FanFire", "LongShot", "ScatterBlast", "HeavyVolley", "Backstep", "TripleTap", "TagRound",
                "BreakerSlug", "Slide", "StoppingShot",
            }), "the twelve skills of the pool");
            Assert.That(icons.Distinct().Count(), Is.EqualTo(icons.Length), "one icon each");
            Assert.That(InterfaceRules.IconName("Dash"), Is.EqualTo("Dash"));
            Assert.That(InterfaceRules.IconName("Focus"), Is.EqualTo("Focus"));
        }

        [TestCase("Scatter Blast", "SCATTER")]
        [TestCase("Backstep", "BACKSTEP")]
        [TestCase("Long Shot", "LONG")]
        [TestCase("Stopping Shot", "STOPPING")]
        public void ASkillWithoutAnIcon_ShowsAShortName(string skill, string shortName) =>
            Assert.That(InterfaceRules.ShortName(skill), Is.EqualTo(shortName));

        [Test]
        public void TheMask_IsThePicturesBrightness_AveragedDown()
        {
            Assert.That(InterfaceRules.Brightness(255, 255, 255), Is.EqualTo(255), "white: opaque");
            Assert.That(InterfaceRules.Brightness(0, 0, 0), Is.EqualTo(0), "black: clear");
            Assert.That(InterfaceRules.Brightness(128, 128, 128), Is.EqualTo(128));
            Assert.That(InterfaceRules.Brightness(255, 0, 0), Is.EqualTo(76), "by the eye's weights");

            // 4 by 4, white on the left half: a 2 by 2 mask opaque on the left, clear on the right.
            var grey = new byte[16];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 2; x++) grey[y * 4 + x] = 255;
            byte[] mask = InterfaceRules.Mask(grey, 4, 4, 2);
            Assert.That(mask, Is.EqualTo(new byte[] { 255, 0, 255, 0 }));
            // A cell half covered is half opaque.
            var half = new byte[] { 255, 0, 255, 0 };
            Assert.That(InterfaceRules.Mask(half, 2, 2, 1)[0], Is.EqualTo(128));
            Assert.That(InterfaceRules.Mask(null, 4, 4, 2), Is.EqualTo(new byte[4]), "nothing to read: clear");
        }
    }
}
