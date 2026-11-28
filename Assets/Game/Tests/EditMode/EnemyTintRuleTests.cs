using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 151's rules (<see cref="EnemyTintRules"/>): when a tinted enemy's added picture on a floor carries its tint, and with
    /// what colour; composed with the hit's flash and the Grave Ram's daze, which win while they are on.
    /// </summary>
    public class EnemyTintRuleTests
    {
        static readonly Rgb Gold = new Rgb(1f, 0.86f, 0.22f);       // EnemyFactory.EliteTint
        static readonly Rgb Green = new Rgb(0.25f, 0.75f, 0.45f);   // EnemyFactory.WarderTint
        static readonly Rgb Daze = new Rgb(0.55f, 0.75f, 1f);       // Enemy's daze tint
        static readonly Rgb Ash = new Rgb(0.86f, 0.86f, 0.84f);     // GraveRamValues.Tint
        static readonly Rgb Magenta = new Rgb(1f, 0.3f, 0.85f);     // CallerValues.Tint
        static readonly Rgb Red = new Rgb(0.72f, 0.18f, 0.2f);      // EnemyFactory.LancerTint

        [TearDown]
        public void PutBack() => EnemyTintValues.Show = true;

        [Test]
        public void TheSwitch_IsOnByDefault() => Assert.That(EnemyTintValues.Show, Is.True);

        [Test]
        public void White_IsNotATint_AndEveryNamedTintOfTheGameIs()
        {
            Assert.That(EnemyTintRules.Tinted(new Rgb(1f, 1f, 1f)), Is.False, "white");
            Assert.That(EnemyTintRules.Tinted(new Rgb(1f, 1f, 1f - EnemyTintValues.WhiteWithin * 0.5f)), Is.False, "white within half a step");
            foreach (Rgb tint in new[] { Gold, Green, Daze, Ash, Magenta, Red })
                Assert.That(EnemyTintRules.Tinted(tint), Is.True, tint.ToString());
            Assert.That(EnemyTintRules.Tinted(new Rgb(2.4f, 2.4f, 2.4f)), Is.True, "a body painted brighter than white (the bomber's fuse) is a colour of its own too");
        }

        [Test]
        public void Laid_OnlyWithTheSwitchOn_ATintedMesh_ATexturedAddedPicture_AndNoFlashOrOtherBlockOverIt()
        {
            Assert.That(EnemyTintRules.Lays(true, true, true, false, false), Is.True, "the tint laid");
            Assert.That(EnemyTintRules.Lays(false, true, true, false, false), Is.False, "the switch off: today's");
            Assert.That(EnemyTintRules.Lays(true, false, true, false, false), Is.False, "an untinted mesh: nothing on it");
            Assert.That(EnemyTintRules.Lays(true, true, false, false, false), Is.False, "a capsule's added picture, made in its colour: left as it is");
            Assert.That(EnemyTintRules.Lays(true, true, true, true, false), Is.False, "a hit's flash: the flash's block alone, as today");
            Assert.That(EnemyTintRules.Lays(true, true, true, false, true), Is.False, "the Grave Ram's daze over the whole renderer: its block alone, as today");
        }

        [Test]
        public void AFlash_OnAGoldElite_ThenGoldAgain_ADaze_OnTheRam_ThenAshWhiteAgain()
        {
            // A frame at a time: the elite tinted, a flash, the flash over.
            bool[] flashing = { false, true, true, false };
            bool[] expect = { true, false, false, true };
            for (int i = 0; i < flashing.Length; i++)
                Assert.That(EnemyTintRules.Lays(true, true, true, flashing[i], false), Is.EqualTo(expect[i]), "frame " + i);
            // The Ram: ash white, its daze's block over it, the daze over.
            bool[] dazed = { false, true, false };
            bool[] laid = { true, false, true };
            for (int i = 0; i < dazed.Length; i++)
                Assert.That(EnemyTintRules.Lays(true, EnemyTintRules.Tinted(Ash), true, false, dazed[i]), Is.EqualTo(laid[i]), "frame " + i);
        }

        [Test]
        public void TheAddedPicture_IsTheFloorsLevel_TimesTheTint_InLinearLight_NoStronger()
        {
            var level = new Rgb(0.42f, 0.42f, 0.42f); // floor 5's lamplit light floor for enemies, as the mood sets it (linear)
            var tint = new Rgb(1f, 0.7f, 0.04f);
            Rgb added = EnemyTintRules.Added(level, tint);
            Assert.That(added.R, Is.EqualTo(0.42f).Within(1e-6f), "full red stays at the level");
            Assert.That(added.G, Is.EqualTo(0.42f * 0.7f).Within(1e-6f));
            Assert.That(added.B, Is.EqualTo(0.42f * 0.04f).Within(1e-6f));
            Rgb white = EnemyTintRules.Added(level, new Rgb(1f, 1f, 1f));
            Assert.That(white.R, Is.EqualTo(level.R).And.EqualTo(white.G).And.EqualTo(white.B), "white: the level exactly, as today");
            foreach (float c in new[] { added.R, added.G, added.B })
                Assert.That(c, Is.LessThanOrEqualTo(level.R + 1e-6f), "never brighter than the untinted picture");
        }
    }
}
