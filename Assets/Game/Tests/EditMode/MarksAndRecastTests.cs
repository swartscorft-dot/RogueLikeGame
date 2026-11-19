using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>Decision 0005 (2026-11-19): marks on a target, how they are added, capped, lost and taken.</summary>
    public class MarksTests
    {
        const int Max = 5;
        const float Life = 6f;

        [Test]
        public void Added_CountUp_ToTheCap()
        {
            var marks = new Marks();
            Assert.That(marks.Count(0f), Is.EqualTo(0));
            Assert.That(marks.Add(1, 0f, Max, Life), Is.EqualTo(1));
            Assert.That(marks.Add(2, 0.5f, Max, Life), Is.EqualTo(3));
            Assert.That(marks.Add(4, 1f, Max, Life), Is.EqualTo(Max), "never more than the cap");
            Assert.That(marks.Count(1f), Is.EqualTo(Max));
        }

        [Test]
        public void TheyAllFallOff_ALifetimeAfterTheLastWasAdded()
        {
            var marks = new Marks();
            marks.Add(1, 0f, Max, Life);
            marks.Add(1, 4f, Max, Life); // the lifetime starts again here
            Assert.That(marks.Count(9.9f), Is.EqualTo(2), "the first mark is older than the lifetime, but the last is not");
            Assert.That(marks.Count(10f), Is.EqualTo(0), "all at once, a lifetime after the last");
            Assert.That(marks.Add(1, 11f, Max, Life), Is.EqualTo(1), "and they start again from none");
        }

        [Test]
        public void FallenOff_AreCountedOnce()
        {
            var marks = new Marks();
            marks.Add(3, 0f, Max, Life);
            Assert.That(marks.TakeFallenOff(5f), Is.EqualTo(0), "not yet");
            Assert.That(marks.TakeFallenOff(6f), Is.EqualTo(3));
            Assert.That(marks.TakeFallenOff(7f), Is.EqualTo(0), "counted once");
        }

        [Test]
        public void ASpender_TakesThemAll()
        {
            var marks = new Marks();
            marks.Add(4, 0f, Max, Life);
            Assert.That(marks.TakeAll(1f), Is.EqualTo(4));
            Assert.That(marks.Count(1f), Is.EqualTo(0));
            Assert.That(marks.TakeAll(1f), Is.EqualTo(0));
            Assert.That(marks.TakeFallenOff(100f), Is.EqualTo(0), "taken marks do not fall off as well");
        }

        [Test]
        public void Cleared_LeavesNone()
        {
            var marks = new Marks();
            marks.Add(2, 0f, Max, Life);
            Assert.That(marks.Clear(1f), Is.EqualTo(2));
            Assert.That(marks.Count(1f), Is.EqualTo(0));
        }

        [Test]
        public void SpenderDamage_IsTheBonusPerMarkTaken()
        {
            Assert.That(MarkRules.SpenderDamage(220, 0, 0.25f), Is.EqualTo(220));
            Assert.That(MarkRules.SpenderDamage(220, 1, 0.25f), Is.EqualTo(275));
            Assert.That(MarkRules.SpenderDamage(220, 5, 0.25f), Is.EqualTo(495), "+125 % at five");
            Assert.That(MarkRules.SpenderDamage(400, 5, 0.4f), Is.EqualTo(1200));
        }

        [Test]
        public void TheBasicShot_MarksEveryFourthHit()
        {
            var count = new BasicShotMarks();
            bool[] marked = Enumerable.Range(0, 8).Select(_ => count.Hit(4, everyHit: false)).ToArray();
            Assert.That(marked, Is.EqualTo(new[] { false, false, false, true, false, false, false, true }));
        }

        [Test]
        public void TheBasicShot_MarksEveryHit_InTheFocusWindow_AndTheCountStands()
        {
            var count = new BasicShotMarks();
            count.Hit(4, false);
            count.Hit(4, false);
            Assert.That(count.Hit(4, everyHit: true), Is.True);
            Assert.That(count.Hit(4, everyHit: true), Is.True);
            Assert.That(count.Counted, Is.EqualTo(2), "the count stands");
            Assert.That(count.Hit(4, false), Is.False);
            Assert.That(count.Hit(4, false), Is.True);
        }

        [Test]
        public void TheKitsStartValues()
        {
            var kit = new KitValues();
            Assert.That(kit.MaxMarks, Is.EqualTo(5));
            Assert.That(kit.MarkLifetime, Is.EqualTo(6f));
            Assert.That(kit.BasicShotsPerMark, Is.EqualTo(4));
            Assert.That(kit.StartWithAllSkills, Is.False);
            Assert.That(kit.CleanShare, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(kit.CleanDamage, Is.EqualTo(1.5f));
        }

        [Test]
        public void FourStartSkills_TwoLight_OneSpenderAmongThem()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            Assert.That(skills.Where(s => s.StartEquipped).Select(s => s.Name),
                        Is.EquivalentTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }));
            Assert.That(skills.Where(s => s.Lock == SkillLock.Light).Select(s => s.Name), Is.EquivalentTo(new[] { "Quick Shot", "Triple Tap" }));
            Assert.That(skills.Where(MarkRules.IsSpender).Select(s => s.Name), Is.EquivalentTo(new[] { "Scatter Blast", "Heavy Volley" }));
            Assert.That(skills.Where(s => s.MarksAdded > 0 && MarkRules.IsSpender(s)), Is.Empty, "a spender adds no marks");
        }
    }

    /// <summary>Decision 0005: spending focus cuts every running cooldown by a share of what is left.</summary>
    public class CooldownCutTests
    {
        [Test]
        public void ACut_TakesTheShareOfWhatIsLeft()
        {
            var cooldown = new Cooldown();
            cooldown.Start(0f, 10f);
            Assert.That(cooldown.Cut(5f, 0.4f), Is.EqualTo(2f).Within(1e-5f), "5 s left: 2 s cut");
            Assert.That(cooldown.Remaining(5f), Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void AReadyCooldown_IsLeftAlone()
        {
            var cooldown = new Cooldown();
            Assert.That(cooldown.Cut(1f, 0.4f), Is.EqualTo(0f));
            Assert.That(cooldown.IsReady(1f), Is.True);
        }

        [Test]
        public void TheDashsCooldown_IsCutTheSameWay()
        {
            var dash = new Dash();
            Assert.That(dash.TryStart(0f, new FeelValues { DashCooldown = 2f }), Is.True);
            Assert.That(dash.CutCooldown(1f, 0.4f), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(dash.CooldownRemaining(1f), Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void FocusFromAnySource_StopsAtFull_AndNoneWhileEmpowered()
        {
            var values = new IdentityValues();
            var gauge = new FocusGauge();
            Assert.That(gauge.Add(values.FocusPerMarkSpent * 5, values, 0f), Is.EqualTo(25f));
            Assert.That(gauge.AddDamage(1000, values, 0f), Is.EqualTo(5f).Within(1e-4f), "a trickle: 0.005 a point");
            Assert.That(gauge.Add(200f, values, 0f), Is.EqualTo(70f).Within(1e-4f), "never more than full");
            Assert.That(gauge.TryActivate(0f, values), Is.True);
            Assert.That(gauge.Add(5f, values, 1f), Is.EqualTo(0f), "none while the window runs");
        }
    }

    /// <summary>Decision 0005: a skill pressed several times, one shot a press, the last one clean late in its window.</summary>
    public class RecastChainTests
    {
        const float Window = 0.6f;
        const float Share = 1f / 3f;

        [Test]
        public void ACountOfOne_IsNoChain()
        {
            var chain = new RecastChain();
            chain.Start(0, 1, Window);
            Assert.That(chain.Slot, Is.EqualTo(-1));
            Assert.That(chain.Judge(0, 0.5f, Share, true), Is.EqualTo(RecastPress.None));
        }

        [Test]
        public void BeforeTheShotHasGoneOff_APressIsEarly()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            Assert.That(chain.Judge(7, 0.1f, Share, true), Is.EqualTo(RecastPress.Early));
            Assert.That(chain.Judge(3, 0.1f, Share, true), Is.EqualTo(RecastPress.None), "another key is no recast");
        }

        [Test]
        public void ThreePresses_InTheirWindows_TheLastCleanOnlyLate()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            chain.ShotWentOff(0.15f);
            // The second press is never clean, however late.
            Assert.That(chain.Judge(7, 0.7f, Share, true), Is.EqualTo(RecastPress.Shot));
            chain.Recast();
            Assert.That(chain.Shots, Is.EqualTo(2));
            Assert.That(chain.Judge(7, 0.75f, Share, true), Is.EqualTo(RecastPress.Early), "its window opens when its shot goes off");
            chain.ShotWentOff(0.85f); // the window runs to 1.45; its clean part from 1.25
            Assert.That(chain.NextIsLast, Is.True);
            Assert.That(chain.Judge(7, 1.0f, Share, true), Is.EqualTo(RecastPress.Shot), "early in the window: an ordinary shot");
            Assert.That(chain.Judge(7, 1.3f, Share, true), Is.EqualTo(RecastPress.Clean), "in the last third: clean");
            Assert.That(chain.Judge(7, 1.3f, Share, false), Is.EqualTo(RecastPress.Shot), "a skill with no clean tap");
            Assert.That(chain.Judge(7, 1.5f, Share, true), Is.EqualTo(RecastPress.None), "late: the window has run out");
            chain.Recast();
            chain.ShotWentOff(1.45f);
            Assert.That(chain.IsComplete, Is.True);
            Assert.That(chain.End(), Is.EqualTo(3));
        }

        [Test]
        public void ARunOutWindow_EndsTheChain_ShortOfItsCount()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            chain.ShotWentOff(0.15f);
            Assert.That(chain.Expired(0.7f), Is.False);
            Assert.That(chain.Expired(0.76f), Is.True);
            Assert.That(chain.End(), Is.EqualTo(1));
            Assert.That(chain.Slot, Is.EqualTo(-1));
        }

        [Test]
        public void TheWindowLeft_RunsFromOneToZero()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            Assert.That(chain.WindowLeft(0.1f), Is.EqualTo(0f), "no window before the shot goes off");
            chain.ShotWentOff(0f);
            Assert.That(chain.WindowLeft(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(chain.WindowLeft(0.3f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Clean_IsTheLastShareOfTheWindow()
        {
            Assert.That(RecastChain.IsClean(0.39f, Window, Share), Is.False);
            Assert.That(RecastChain.IsClean(0.4f, Window, Share), Is.True);
            Assert.That(RecastChain.IsClean(0.6f, Window, Share), Is.True);
            Assert.That(RecastChain.IsClean(0.61f, Window, Share), Is.False);
        }
    }
}
