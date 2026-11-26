using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 138, part 1 (Kristian, 2026-11-25: "Kanskje fjerne 2 spell slots"; his tick "To av de tomme"): six skill
    /// slots, Q, 1, 2, R, F and the right button, the start skills on a fixed table of keys, keys 3 and 4 holding nothing;
    /// with the switch off the bar of work order 015, field for field.
    /// </summary>
    public class SixSlotsTests
    {
        static SkillDefinition[] Kit => SkillDefaults.Create();
        static int Index(string name) => System.Array.FindIndex(Kit, s => s.Name == name);
        static int[] OnKeys(SkillLoadout bar) => Enumerable.Range(0, SkillLoadout.KeyCount).Select(bar.SkillOn).ToArray();

        [Test]
        public void TheSwitch_IsOnByDefault_AndABareLoadoutIsTheOldEight()
        {
            Assert.That(new KitValues().SixSlots, Is.True, "his words: two slots fewer");
            var bare = new SkillLoadout();
            Assert.That(bare.SixSlots, Is.False, "a bare loadout, as tests and tools made it before");
            Assert.That(bare.SlotsInUse, Is.EqualTo(8));
            Assert.That(Enumerable.Range(0, 8).Select(bare.KeyAt), Is.EqualTo(Enumerable.Range(0, 8)));
        }

        [Test]
        public void Off_TheBarIsWorkOrder015s_FieldForField()
        {
            var bar = new SkillLoadout { SixSlots = false };
            bar.Start(Kit, all: false);
            Assert.That(OnKeys(bar), Is.EqualTo(new[] { 0, -1, -1, -1, 4, -1, 6, 7 }));
            Assert.That(bar.Take(Index("Slide")), Is.EqualTo(1));
            Assert.That(bar.Take(Index("Tag Round")), Is.EqualTo(2));
            Assert.That(bar.Take(Index("Powder Keg")), Is.EqualTo(3));
            Assert.That(bar.Take(Index("Stopping Shot")), Is.EqualTo(5));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(-1));
            Assert.That(Enumerable.Range(0, 8).All(bar.InUse), Is.True);
            var all = new SkillLoadout { SixSlots = false };
            all.Start(Kit, all: true);
            Assert.That(OnKeys(all), Is.EqualTo(Enumerable.Range(0, 8)), "every skill: the first eight on the keys of their places");
        }

        [Test]
        public void On_TheStartFour_SitOnTheTablesKeys_QRFAndTheRightButton()
        {
            var bar = new SkillLoadout { SixSlots = true };
            bar.Start(Kit, all: false);
            Assert.That(bar.SlotsInUse, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, 6).Select(bar.KeyAt), Is.EqualTo(new[] { 0, 1, 2, 4, 6, 7 }), "Q 1 2 R F and the right button");
            Assert.That(bar.KeyAt(6), Is.EqualTo(-1));
            Assert.That(bar.SkillOn(0), Is.EqualTo(Index("Quick Shot")));
            Assert.That(bar.SkillOn(4), Is.EqualTo(Index("Scatter Blast")));
            Assert.That(bar.SkillOn(6), Is.EqualTo(Index("Backstep")));
            Assert.That(bar.SkillOn(7), Is.EqualTo(Index("Triple Tap")));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(1));
            Assert.That(bar.InUse(3) || bar.InUse(5), Is.False, "keys 3 and 4 hold nothing");
        }

        [Test]
        public void On_TheTable_HoldsWhateverTheListsOrder()
        {
            // The start rule of before put a start skill on the key of its own place; the table is by name, so a list in
            // another order (here reversed) still puts the four where they sat.
            List<SkillDefinition> reversed = Kit.Reverse().ToList();
            var bar = new SkillLoadout { SixSlots = true };
            bar.Start(reversed, all: false);
            string On(int key) => bar.SkillOn(key) >= 0 ? reversed[bar.SkillOn(key)].Name : "";
            Assert.That(On(0), Is.EqualTo("Quick Shot"));
            Assert.That(On(4), Is.EqualTo("Scatter Blast"));
            Assert.That(On(6), Is.EqualTo("Backstep"));
            Assert.That(On(7), Is.EqualTo("Triple Tap"));
            Assert.That(On(1) + On(2) + On(3) + On(5), Is.Empty, "nothing slid onto 1 and 2");
        }

        [Test]
        public void On_TwoFoundSkills_GoOn1Then2_AndAThirdIsHadWithNoKey()
        {
            var bar = new SkillLoadout { SixSlots = true };
            bar.Start(Kit, all: false);
            int slide = Index("Slide"), tag = Index("Tag Round"), keg = Index("Powder Keg");
            Assert.That(bar.Take(slide), Is.EqualTo(1));
            Assert.That(bar.Take(tag), Is.EqualTo(2));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(-1), "six skills on six keys: full");
            Assert.That(bar.Take(keg), Is.EqualTo(-1), "never on 3 or 4");
            Assert.That(bar.Has(keg), Is.True, "had, on no key, as before with a ninth");
            Assert.That(bar.SkillOn(3), Is.EqualTo(-1));
            Assert.That(bar.SkillOn(5), Is.EqualTo(-1));
            Assert.That(bar.Take(slide), Is.EqualTo(1), "taken again, it stays");
        }

        [Test]
        public void On_EverySkill_TheFirstTwoOfTheListGoOn1And2_TheRestOnNone()
        {
            // The practice room's "every skill" switch (KitValues.StartWithAllSkills): with six slots the first two found of
            // the list (Powder Keg, Fan Fire) sit on 1 and 2; Long Shot and Heavy Volley, on 3 and 4 before, are had on no key.
            var bar = new SkillLoadout { SixSlots = true };
            bar.Start(Kit, all: true);
            Assert.That(OnKeys(bar), Is.EqualTo(new[] { 0, 1, 2, -1, 4, -1, 6, 7 }));
            Assert.That(Enumerable.Range(0, Kit.Length).All(bar.Has), Is.True, "she has every skill");
        }

        [Test]
        public void TheTable_SaysTheFourAndNoMore()
        {
            Assert.That(SkillSlots.StartKeys.Select(k => k.Key), Is.EqualTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }));
            Assert.That(SkillSlots.StartKeys.Select(k => k.Value), Is.EqualTo(new[] { 0, 4, 6, 7 }));
            Assert.That(SkillSlots.StartKeyOf("Slide"), Is.EqualTo(-1));
            foreach (KeyValuePair<string, int> k in SkillSlots.StartKeys) Assert.That(SkillSlots.InUse(k.Value, true), Is.True, k.Key);
            Assert.That(Kit.Where(s => s.StartEquipped).Select(s => s.Name), Is.EquivalentTo(SkillSlots.StartKeys.Select(k => k.Key)),
                        "every start skill has its key in the table");
        }
    }
}
