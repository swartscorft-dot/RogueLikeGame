using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The aim outline during a cast and a hold (work order 140, part 3; his tick of 2026-11-26: "Vis under kast og hold"), as rules: the
    /// switch on; for every skill of her kit and every phase, the outline stands while Long Shot's key is held (as work order 123 left it),
    /// while a Casting skill is cast (Heavy Volley, Deadeye) and while a Holding skill's key is held and it fires (Fan Fire), never from the
    /// moment it resolves and never for a skill with only a short startup after a press; off, as work order 123 left it; with work order
    /// 123's switch off, as before it. Deadeye's outline is the circle of its reach, no other skill's.
    /// </summary>
    public class OutlineInCastAndHoldRulesTests
    {
        static readonly SkillDefinition[] s_Kit = SkillDefaults.Create();
        static readonly SkillPhase[] s_Phases = (SkillPhase[])Enum.GetValues(typeof(SkillPhase));

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(SkillLookValues.OutlineInCastAndHold, Is.True, "on by default (his tick: \"Vis under kast og hold\")");
        }

        [Test]
        public void TheSkillsWithACastOrAHold_AreHeavyVolleyDeadeyeFanFireAndLongShot()
        {
            Assert.That(s_Kit.Where(s => s.Input == SkillInput.Casting).Select(s => s.Name), Is.EquivalentTo(new[] { "Heavy Volley", "Deadeye" }));
            Assert.That(s_Kit.Where(s => s.Input == SkillInput.Holding).Select(s => s.Name), Is.EquivalentTo(new[] { "Fan Fire" }));
            Assert.That(s_Kit.Where(s => s.Input == SkillInput.Charge).Select(s => s.Name), Is.EquivalentTo(new[] { "Long Shot" }));
        }

        [Test]
        public void On_TheOutlineStands_InTheCastAndTheHold_AndWhileLongShotCharges_NeverAfter()
        {
            foreach (SkillDefinition s in s_Kit)
                foreach (SkillPhase p in s_Phases)
                {
                    bool expected = (s.Input == SkillInput.Charge && p == SkillPhase.Channel)
                                    || (s.Input == SkillInput.Casting && p == SkillPhase.Startup)
                                    || (s.Input == SkillInput.Holding && (p == SkillPhase.Startup || p == SkillPhase.Channel));
                    Assert.That(SkillLookRules.OutlineShows(true, true, s.Input, p), Is.EqualTo(expected), s.Name + " " + p);
                }
        }

        [Test]
        public void Off_AsWorkOrder123LeftIt_AndWith123Off_AsBeforeIt()
        {
            foreach (SkillDefinition s in s_Kit)
                foreach (SkillPhase p in s_Phases)
                {
                    Assert.That(SkillLookRules.OutlineShows(true, false, s.Input, p), Is.EqualTo(SkillLookRules.OutlineShows(true, s.Input, p)), s.Name + " " + p);
                    foreach (bool castAndHold in new[] { true, false })
                        Assert.That(SkillLookRules.OutlineShows(false, castAndHold, s.Input, p), Is.EqualTo(p != SkillPhase.None && p != SkillPhase.Recovery), s.Name + " " + p);
                }
        }

        [Test]
        public void DeadeyesOutline_IsTheCircleOfItsReach_NoOtherSkills_AndOnlyWithBothSwitches()
        {
            Assert.That(s_Kit.Where(s => SkillLookRules.OutlineIsReach(s, true, true)).Select(s => s.Name), Is.EqualTo(new[] { "Deadeye" }));
            SkillDefinition deadeye = s_Kit.Single(s => s.Name == "Deadeye");
            Assert.That(SkillLookRules.OutlineIsReach(deadeye, true, false), Is.False);
            Assert.That(SkillLookRules.OutlineIsReach(deadeye, false, true), Is.False);
            Assert.That(SkillLookRules.OutlineIsReach(null, true, true), Is.False);
        }
    }
}
