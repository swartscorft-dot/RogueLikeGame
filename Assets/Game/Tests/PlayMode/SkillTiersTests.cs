using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 057 (decision 0013; Kristian, 2026-11-20: "vi har ikke noe tiers på upgrades og spells", and his choice of
    /// 2026-11-21, "Tiers på ferdigheter"): the floor reward's skill tiers in the game, through the intents. Floor 1 shows a
    /// rare or better; an epic card's skill has the step's damage and its change, one pip, and the elite room never offers
    /// that change; a rare's has no pip and deals 1.3 times; the cards say the tier (kinetic and the old look) and fit their
    /// text; the bell; the drop on the ground and Second Look keep and redraw the tiers; the pause menu's key line, the end
    /// screen and the recorder show them; and with the switch off all is as before.
    ///
    /// The seeds are found from the rules (<see cref="SkillTiers.ForReward"/>) with the scene's own skills, so the test says
    /// which it used. Her stats and tiers (work order 050) are off here, so no crit changes a hit's damage; her luck is then 0,
    /// as it is on floor 1 with no chest opened. Waits are frames or real time with a limit, never frames of a supposed length.
    /// </summary>
    public class SkillTiersTests
    {
        const int MostFrames = 600;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        GeneratedLevels _levels;
        RunHud _hud;
        PlaceholderSounds _sounds;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<int> _damage = new List<int>();
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            PlaceholderSounds.Requested += Record;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false;
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _damage.Clear();
            _a.Damaged += d => _damage.Add(d);
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.StatsAndTiers = false; // no crit in a hit's damage; luck 0, as on floor 1 with no chest opened
            Assert.That(_levels.RunValues.SkillTiers, Is.True, "on by default");
            _hud = RunHud.Ensure();
            yield return null;
            _played.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            InterfaceStyle.Shared.Kinetic = true;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (_player != null) _player.SkillUpgrades = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _played.Clear();
        }

        void Record(PlaceholderSoundRequest r) => _played.Add(r);

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        List<bool> StartFour() => Enumerable.Range(0, _player.Skills.Count).Select(i => _player.Skills[i].StartEquipped).ToList();

        /// <summary>Floor 1's reward of a run seed as the rules draw it, with the scene's skills: its slots and their tiers.</summary>
        List<SkillTierOffer> FloorOne(ulong seed, RunValues values = null)
        {
            values = values ?? _levels.RunValues;
            List<int> slots = FloorRewards.Offer(seed, 1, StartFour(), values.SkillsOffered);
            return SkillTiers.ForReward(values, seed, 1, 0, slots, _player.Skills, _levels.UpgradeValues.Table, Array.Empty<SkillUpgradeDefinition>(), 0f);
        }

        /// <summary>The first run seed whose floor-1 reward has this tier on its first card (of this skill, when named).</summary>
        ulong SeedWithFirstCard(UpgradeTier tier, string skill = null)
        {
            for (ulong seed = 1; seed < 100000; seed++)
            {
                List<SkillTierOffer> offer = FloorOne(seed);
                if (offer[0].Tier == tier && (skill == null || offer[0].Skill == skill)) return seed;
            }
            Assert.Fail("no seed with a " + tier + " " + skill + " first");
            return 0;
        }

        /// <summary>A run of this seed with floor 1 finished: its reward up, checked against the rules.</summary>
        IEnumerator FloorOnesReward(ulong seed)
        {
            _levels.StartRun(seed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            List<SkillTierOffer> rules = FloorOne(seed);
            Assert.That(_levels.OfferedSkills, Is.EqualTo(rules.Select(o => o.Slot)), "the slots as the rules give them");
            Assert.That(_levels.OfferedSkillTiers.Select(o => o.ToString()), Is.EqualTo(rules.Select(o => o.ToString())),
                        "the tiers as the rules give them, seed " + seed + " (" + LevelSeed.ToText(seed) + ")");
            TestContext.WriteLine("seed " + seed + " (" + LevelSeed.ToText(seed) + "): " + string.Join(", ", rules.Select(o => o.ToString())));
        }

        int Slot(string name)
        {
            int slot = _player.SlotOf(name);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no skill " + name);
            return slot;
        }

        /// <summary>A keg thrown at Dummy_A in the practice room, left to burst on its fuse: the hit it dealt.</summary>
        IEnumerator KegAtTheDummy(Action<int> dealt)
        {
            int keg = Slot("Powder Keg");
            _player.EquipSkill(keg); // had in the practice room, on a free key
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            _damage.Clear();
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 5f);
            Assert.That(_damage.Count, Is.EqualTo(1), "one burst");
            dealt(_damage[0]);
        }

        // ---------------- Floor 1, and an epic and a rare picked ----------------

        [UnityTest]
        public IEnumerator FloorOnesReward_ShowsARareOrBetter_AsTheRulesDrawIt()
        {
            ulong seed = 0x0E117E17UL;
            yield return FloorOnesReward(seed);
            Assert.That(_levels.OfferedSkillTiers.Count, Is.EqualTo(_levels.OfferedSkills.Count), "a tier per card");
            Assert.That(_levels.OfferedSkillTiers.Any(t => t.Tiered), Is.True, "a rare or better on floor 1");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator AnEpicCard_Picked_GivesTheStepsDamage_AndItsChange_OnePip_AndTheEliteNeverOffersIt()
        {
            ulong seed = SeedWithFirstCard(UpgradeTier.Epic, "Powder Keg");
            yield return FloorOnesReward(seed);
            SkillTierOffer epic = _levels.OfferedSkillTiers[0];
            Assert.That(epic.Change.Name, Is.EqualTo("Twin Kegs"), "Powder Keg's one change");
            int slot = _levels.OfferedSkills[0];
            SkillDefinition preview = SkillTiers.Preview(_player.SettingsSkills[slot], epic);
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_player.IsEquipped(slot), Is.True);
            SkillDefinition now = _player.Skills[slot];
            Assert.That(now.Damage, Is.EqualTo((int)Math.Round(_player.SettingsSkills[slot].Damage * (double)1.3f, MidpointRounding.AwayFromZero)), "the step's damage");
            Assert.That(now.Damage, Is.EqualTo(preview.Damage), "what the card previewed");
            Assert.That(now.Charges, Is.EqualTo(2), "Twin Kegs");
            Assert.That(_player.UpgradesOn(slot), Is.EqualTo(1), "one pip: the change; the step is none");
            Assert.That(_levels.SkillUpgrades.TierOf("Powder Keg"), Is.EqualTo(UpgradeTier.Epic));
            Assert.That(_levels.SkillUpgrades.NamesFor("Powder Keg"), Is.EqualTo(new[] { "Twin Kegs" }));
            Assert.That(_levels.SkillsTaken, Is.EqualTo(new[] { "Powder Keg" }), "the names stay names");
            Assert.That(_levels.Record.SkillUpgrades, Is.Empty, "the change that came with a skill is not an elite pick");
            Assert.That(_levels.Record.Picks, Is.EqualTo(2), "the skill and the focus choice");
            Assert.That(_player.SettingsSkills[slot].Charges, Is.EqualTo(1), "nothing written to the settings");

            // The elite room of the next floor: never Twin Kegs again.
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            Assert.That(_levels.OfferedSkillUpgrades.Select(u => u.Name), Has.None.EqualTo("Twin Kegs"));
            Assert.That(_levels.OfferedSkillUpgrades.Any(u => u.TierStep), Is.False);
            var had = new HashSet<string>(Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).Select(s => _player.Skills[s].Name));
            Assert.That(_levels.OfferedSkillUpgrades.Select(u => u.FullName),
                        Is.EqualTo(SkillUpgradeOffer.Offer(seed, 2, _levels.UpgradeValues.Table, had, _levels.SkillUpgrades.Taken, _levels.UpgradeValues.Offered).Select(u => u.FullName)));
            _levels.PickSkillUpgrade(0);

            // Its change works and its damage is the preview's, fired at a dummy in the practice room through the same list.
            _levels.BackToPracticeRoom();
            yield return null;
            var run = new RunSkillUpgrades();
            run.TakeTier(epic);
            _player.SkillUpgrades = run;
            int keg = Slot("Powder Keg");
            Assert.That(_player.SkillChargesReady(keg), Is.EqualTo(2), "two kegs held: Twin Kegs");
            int dealt = 0;
            yield return KegAtTheDummy(d => dealt = d);
            Assert.That(dealt, Is.EqualTo(preview.Damage), "the burst deals the preview's damage");
        }

        [UnityTest]
        public IEnumerator ARareCard_Picked_HasNoPip_AndDeals13Times()
        {
            ulong seed = SeedWithFirstCard(UpgradeTier.Rare, "Powder Keg");
            yield return FloorOnesReward(seed);
            SkillTierOffer rare = _levels.OfferedSkillTiers[0];
            Assert.That(rare.Change, Is.Null);
            int slot = _levels.OfferedSkills[0];
            int own = _player.SettingsSkills[slot].Damage;
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;
            Assert.That(_player.UpgradesOn(slot), Is.Zero, "no pip");
            Assert.That(_player.Skills[slot].Damage, Is.EqualTo((int)Math.Round(own * (double)1.3f, MidpointRounding.AwayFromZero)));
            Assert.That(_player.Skills[slot].Charges, Is.EqualTo(1), "no change");
            Assert.That(_levels.SkillUpgrades.TierOf("Powder Keg"), Is.EqualTo(UpgradeTier.Rare));
            Assert.That(_levels.SkillUpgrades.Text(), Is.EqualTo("none"), "a step has no name in the lists");

            _levels.BackToPracticeRoom();
            yield return null;
            int plain = 0, tiered = 0;
            yield return KegAtTheDummy(d => plain = d);
            Assert.That(plain, Is.EqualTo(own), "the practice room's keg, as it is");
            yield return new WaitForSeconds(0.2f);
            var run = new RunSkillUpgrades();
            run.TakeTier(rare);
            _player.SkillUpgrades = run;
            _player.StartFresh();
            yield return KegAtTheDummy(d => tiered = d);
            Assert.That(tiered, Is.EqualTo((int)Math.Round(own * (double)1.3f, MidpointRounding.AwayFromZero)), "1.3 times");
        }

        // ---------------- The cards ----------------

        [UnityTest]
        public IEnumerator TheKineticCards_OfARareAndAnEpic_SayTheTier_InItsInk_AndFitTheirText_ACommonsIsTodays()
        {
            _levels.Offers.PopUp = false; // work order 065: this morning's cards at the right edge, with this morning's words, pinned here
            _levels.RunValues.SkillTierNumbers.Odds = new[] { 100f, 0f, 0f }; // commons, but floor 1 raises the last
            ulong seed = 0x0E117E17UL;
            _levels.StartRun(seed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assume.That(_levels.OfferedSkills.Count, Is.EqualTo(3));
            // The three tiers on its three cards, from data made as the rules make it, before the cards are made.
            var tiers = (List<SkillTierOffer>)_levels.OfferedSkillTiers;
            SkillDefinition s1 = _player.Skills[_levels.OfferedSkills[1]], s2 = _player.Skills[_levels.OfferedSkills[2]];
            var v = new SkillTierValues();
            tiers[0] = new SkillTierOffer { Slot = _levels.OfferedSkills[0], Skill = _player.Skills[_levels.OfferedSkills[0]].Name, Tier = UpgradeTier.Common };
            tiers[1] = new SkillTierOffer { Slot = _levels.OfferedSkills[1], Skill = s1.Name, Tier = UpgradeTier.Rare, Step = SkillTiers.Step(s1, v) };
            List<SkillUpgradeDefinition> changes = SkillTiers.OpenChanges(_levels.UpgradeValues.Table, s2.Name, _levels.SkillUpgrades.Taken);
            Assume.That(changes.Count, Is.GreaterThan(0));
            // The longest change line of the table, so the measure is tried on the most an epic says.
            SkillUpgradeDefinition longest = changes.OrderByDescending(c => c.Line.Length).First();
            tiers[2] = new SkillTierOffer { Slot = _levels.OfferedSkills[2], Skill = s2.Name, Tier = UpgradeTier.Epic, Step = SkillTiers.Step(s2, v), Change = longest };

            int count = _levels.KineticCards();
            Assert.That(count, Is.EqualTo(3));
            Assert.That(_levels.CardNumeralInk(0), Is.EqualTo(UiInk.White), "a common");
            Assert.That(_levels.CardNumeralInk(1), Is.EqualTo(UiInk.Teal), "a rare's numeral teal");
            Assert.That(_levels.CardNumeralInk(2), Is.EqualTo(UiInk.Gold), "an epic's gold");
            for (int i = 0; i < 3; i++) Assert.That(_levels.CardTitle(i), Is.EqualTo(_player.Skills[_levels.OfferedSkills[i]].Name), "the title is the skill's name");

            string Today(SkillDefinition s, float cooldown) =>
                "key " + _player.GetComponent<PlayerInputDriver>().ActiveBindings.SkillLabel(_player.FirstFreeKey) + "   cooldown "
                + cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s   " + SkillDefaults.MarkRole(s) + "\n" + SkillDefaults.Line(s.Name);
            SkillDefinition s0 = _player.Skills[_levels.OfferedSkills[0]];
            Assert.That(_levels.CardText(0), Is.EqualTo(Today(s0, s0.Cooldown)), "a common card's text is today's, word for word");
            string rareText = _levels.CardText(1), epicText = _levels.CardText(2);
            Assert.That(rareText.Split('\n')[0], Does.StartWith("RARE   "), "the tier's word first");
            Assert.That(rareText, Is.EqualTo(SkillTiers.CardTierLine(tiers[1]) + "\n" + Today(s1, SkillTiers.Preview(s1, tiers[1]).Cooldown)));
            Assert.That(epicText.Split('\n')[0], Does.StartWith("EPIC   "));
            Assert.That(epicText, Does.EndWith("\n" + longest.Name.ToUpperInvariant() + "  " + longest.Line), "an epic's change last");
            StringAssert.Contains(SkillDefaults.Line(s2.Name), epicText);

            // Nothing a card says is cut off (work order 055's rule): each text inside its rectangle, at the width it is drawn in.
            InterfaceValues iv = InterfaceStyle.V;
            GUIStyle titleStyle = InterfaceStyle.Text(true, iv.CardTitleSize, TextAnchor.UpperLeft, true);
            GUIStyle textStyle = InterfaceStyle.Text(false, iv.CardTextSize, TextAnchor.UpperLeft, true);
            for (int i = 0; i < 3; i++)
            {
                Rect title = _levels.CardTitleRect(i), text = _levels.CardTextRect(i), card = _levels.CardRect(i);
                Assert.That(InterfaceStyle.MeasureHeight(_levels.CardTitle(i), titleStyle, title.width), Is.LessThanOrEqualTo(title.height + 0.5f), "card " + i + "'s title fits");
                Assert.That(InterfaceStyle.MeasureHeight(_levels.CardText(i), textStyle, text.width), Is.LessThanOrEqualTo(text.height + 0.5f), "card " + i + "'s text fits");
                Assert.That(text.yMax, Is.LessThanOrEqualTo(card.yMax + 0.5f), "inside the card");
                if (i > 0) Assert.That(card.yMin, Is.GreaterThan(_levels.CardRect(i - 1).yMax), "stacked down");
            }

            // The old look: the same words, and the tier's edge.
            InterfaceStyle.Shared.Kinetic = false;
            string old0 = _levels.RewardChoiceText(0), old1 = _levels.RewardChoiceText(1), old2 = _levels.RewardChoiceText(2);
            Assert.That(old0.Contains("RARE") || old0.Contains("EPIC"), Is.False, "a common: today's");
            Assert.That(old0, Does.EndWith("\n" + SkillDefaults.Line(s0.Name)));
            StringAssert.Contains(SkillTiers.CardTierLine(tiers[1]), old1);
            StringAssert.Contains(SkillTiers.CardTierLine(tiers[2]), old2);
            StringAssert.Contains(SkillTiers.CardChangeLine(tiers[2]), old2);
            StringAssert.Contains(SkillDefaults.Line(s2.Name), old2);
            yield return null; // a frame of the old look drawn with them
            Assert.That(_levels.PickReward(0), Is.True);
            _levels.BackToPracticeRoom();
        }

        /// <summary>
        /// Work order 082 (review 6, finding 7): the same forced rare and epic, in the pop-up he plays (the test above pins the
        /// right-edge look): the rare's numeral teal, the epic's gold, each card's tier line, and the epic's "+ CHANGE" note (none
        /// on the rare's), so a pop-up card that lost its tier's ink or its change is seen whatever the seed draws.
        /// </summary>
        [UnityTest]
        public IEnumerator TheKineticCards_OfARareAndAnEpic_InThePopUp_SayTheTier_InItsInk_AndTheEpicsChange()
        {
            Assert.That(_levels.Offers.PopUp, Is.True, "the pop-up on by default");
            _levels.RunValues.SkillTierNumbers.Odds = new[] { 100f, 0f, 0f };
            ulong seed = 0x0E117E17UL;
            _levels.StartRun(seed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.OfferedSkills.Count, Is.EqualTo(3));
            var tiers = (List<SkillTierOffer>)_levels.OfferedSkillTiers;
            SkillDefinition s1 = _player.Skills[_levels.OfferedSkills[1]], s2 = _player.Skills[_levels.OfferedSkills[2]];
            var v = new SkillTierValues();
            tiers[0] = new SkillTierOffer { Slot = _levels.OfferedSkills[0], Skill = _player.Skills[_levels.OfferedSkills[0]].Name, Tier = UpgradeTier.Common };
            tiers[1] = new SkillTierOffer { Slot = _levels.OfferedSkills[1], Skill = s1.Name, Tier = UpgradeTier.Rare, Step = SkillTiers.Step(s1, v) };
            List<SkillUpgradeDefinition> changes = SkillTiers.OpenChanges(_levels.UpgradeValues.Table, s2.Name, _levels.SkillUpgrades.Taken);
            Assert.That(changes.Count, Is.GreaterThan(0), "an epic's change open for " + s2.Name);
            SkillUpgradeDefinition change = changes[0];
            tiers[2] = new SkillTierOffer { Slot = _levels.OfferedSkills[2], Skill = s2.Name, Tier = UpgradeTier.Epic, Step = SkillTiers.Step(s2, v), Change = change };

            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(_levels.CardNumeralInk(0), Is.EqualTo(UiInk.White), "a common");
            Assert.That(_levels.CardNumeralInk(1), Is.EqualTo(UiInk.Teal), "a rare's numeral teal");
            Assert.That(_levels.CardNumeralInk(2), Is.EqualTo(UiInk.Gold), "an epic's gold");
            OfferCard common = _levels.OfferCardAt(0), rare = _levels.OfferCardAt(1), epic = _levels.OfferCardAt(2);
            Assert.That(common != null && rare != null && epic != null, "the pop-up's cards");
            Assert.That(common.Tiered, Is.False);
            Assert.That(rare.Tier, Is.EqualTo(UpgradeTier.Rare));
            StringAssert.StartsWith("RARE", rare.TierLine, "a rare's tier line");
            Assert.That(rare.Notes, Is.Empty, "a rare has no change");
            Assert.That(epic.Tier, Is.EqualTo(UpgradeTier.Epic));
            StringAssert.StartsWith("EPIC", epic.TierLine, "an epic's tier line");
            Assert.That(epic.Notes.Count, Is.EqualTo(1), "the epic's change, one note");
            StringAssert.StartsWith("+ " + change.Name.ToUpperInvariant(), epic.Notes[0], "the epic's change by its name");
            Assert.That(_levels.PickReward(0), Is.True);
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator ACommonOnlyOffer_OnALaterFloor_HasTodaysCards()
        {
            _levels.RunValues.SkillTierNumbers.Odds = new[] { 100f, 0f, 0f };
            _levels.StartRun(0x0E117E17UL);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.OfferedSkillTiers.Last().Tiered, Is.True, "floor 1: the last card raised");
            _levels.PickReward(0);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0);
            yield return null;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.OfferedSkillTiers.All(t => !t.Tiered), Is.True, "floor 2: commons only");
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(_levels.CardNumeralInk(i), Is.EqualTo(UiInk.White));
                Assert.That(_levels.CardText(i), Does.Not.Contain("RARE").And.Not.Contain("EPIC"));
                Assert.That(_levels.CardText(i), Does.StartWith("key "), "today's first line");
            }
            _levels.BackToPracticeRoom();
        }

        // ---------------- The bell ----------------

        IEnumerator RewardWithOdds(float[] odds, int floors = 1)
        {
            _levels.RunValues.SkillTierNumbers.Odds = odds;
            _levels.StartRun(0x0E117E17UL);
            yield return null;
            _health.Untouchable = true;
            _sounds = _sounds != null ? _sounds : PlaceholderSounds.Ensure();
            yield return null;
            yield return null;
            _played.Clear();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f); // an epic's second bell, in real time
            yield return null;
        }

        int Count(PlaceholderSound sound) => _played.Count(r => r.Sound == sound);
        string Heard => string.Join(", ", _played.Select(r => r.Sound + "@" + r.Frame));

        [UnityTest]
        public IEnumerator TheBell_RingsOnceForARareFloorReward_AndTwiceForAnEpic()
        {
            yield return RewardWithOdds(new[] { 0f, 100f, 0f });
            Assume.That(_levels.OfferedSkillTiers.All(t => t.Tier == UpgradeTier.Rare), Is.True);
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(1), "a bell with the reward up: " + Heard);
            Assert.That(_played.Single(r => r.Sound == PlaceholderSound.RareOffer).Bells, Is.EqualTo(1), "one");
            _levels.BackToPracticeRoom();
            yield return null;

            yield return RewardWithOdds(new[] { 0f, 0f, 100f });
            Assume.That(_levels.OfferedSkillTiers.Any(t => t.Tier == UpgradeTier.Epic), Is.True);
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(1), "one sound: " + Heard);
            Assert.That(_played.Single(r => r.Sound == PlaceholderSound.RareOffer).Bells, Is.EqualTo(2), "two bells");
            _levels.BackToPracticeRoom();
            yield return null;

            _levels.RunValues.SkillTiers = false;
            yield return RewardWithOdds(new[] { 0f, 0f, 100f });
            Assert.That(_levels.OfferedSkillTiers, Is.Empty);
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(0), "the switch off: no bell, as before: " + Heard);
            _levels.BackToPracticeRoom();
        }

        // ---------------- The drop and Second Look ----------------

        [UnityTest]
        public IEnumerator TheRewardAsADrop_PickedUp_ShowsTheSameTiers()
        {
            ulong seed = 0x0E117E17UL;
            _levels.StartRun(seed);
            yield return null;
            _health.Untouchable = true;
            List<Enemy> alive = _levels.Level.ExitEnemies.Where(e => e != null && !e.IsDead).ToList();
            Assert.That(alive.Count, Is.GreaterThan(0));
            foreach (Enemy e in alive) e.Body.TakeHit(100000000);
            yield return Frames(() => _levels.FloorDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
            RewardDrop drop = _levels.FloorDrop;
            Assert.That(drop, Is.Not.Null, "the reward lies on the ground");
            Assert.That(_levels.OfferedSkillTiers, Is.Empty, "nothing offered on screen");
            yield return Until(() => drop.Settled, 5f);
            _player.Teleport(new Vector3(drop.Landing.x, 0f, drop.Landing.z));
            yield return null;
            yield return null;
            Assert.That(_levels.PickUpDrop(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.OfferedSkillTiers.Select(o => o.ToString()), Is.EqualTo(FloorOne(seed).Select(o => o.ToString())), "the same tiers");
            Assert.That(_levels.OfferedSkillTiers.Any(t => t.Tiered), Is.True);
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator SecondLook_DrawsNewTiers_WithTheNewSlots()
        {
            ulong seed = 0x0E117E17UL;
            yield return FloorOnesReward(seed);
            _levels.Modifiers.Meta.RedrawsLeft = 1; // Second Look, as the rack would give it
            List<string> before = _levels.OfferedSkillTiers.Select(o => o.ToString()).ToList();
            _levels.KineticCards();
            Assert.That(_levels.RedrawOffer(), Is.True);
            List<int> again = _levels.OfferedSkills.ToList();
            List<SkillTierOffer> rules = SkillTiers.ForReward(_levels.RunValues, seed, 1, 1, again, _player.Skills, _levels.UpgradeValues.Table, _levels.SkillUpgrades.Taken, 0f);
            Assert.That(_levels.OfferedSkillTiers.Select(o => o.ToString()), Is.EqualTo(rules.Select(o => o.ToString())), "drawn again with draw 1");
            Assert.That(_levels.OfferedSkillTiers.Select(o => o.Slot), Is.EqualTo(again), "with the new slots");
            Assert.That(_levels.OfferedSkillTiers.Any(t => t.Tiered), Is.True, "floor 1's rule holds for the redraw too");
            Assert.That(_levels.KineticCards(), Is.EqualTo(again.Count), "the cards made again");
            for (int i = 0; i < again.Count; i++)
                if (_levels.OfferedSkillTiers[i].Tiered) StringAssert.StartsWith(_levels.OfferedSkillTiers[i].Word.ToUpperInvariant(), _levels.CardText(i));
            TestContext.WriteLine("before: " + string.Join(", ", before) + "; after: " + string.Join(", ", rules));
            _levels.BackToPracticeRoom();
        }

        // ---------------- Where the tier shows afterwards, and the recorder ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusKeyLine_TheEndScreen_AndTheRecorder_ShowTheTier()
        {
            ulong seed = SeedWithFirstCard(UpgradeTier.Epic);
            _folder = Path.Combine(Path.GetTempPath(), "SkillTiersTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return FloorOnesReward(seed);
            SkillTierOffer epic = _levels.OfferedSkillTiers[0];
            List<string> words = _levels.OfferedSkillTiers.Select(t => t.Word).ToList();
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;

            string line = _hud.KeyLines().Single(l => l.Contains(epic.Skill));
            int key = _player.KeyOfSkill(Slot(epic.Skill));
            Assert.That(line, Is.EqualTo(_player.GetComponent<PlayerInputDriver>().ActiveBindings.SkillLabel(key) + "  " + epic.Skill + "  epic  (" + epic.Change.Name + ")"));
            StringAssert.Contains(epic.Skill + " (epic, " + epic.Change.Name + ")", _levels.StatusText(), "the developer's box");

            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            yield return new WaitForSecondsRealtime(HudRules.EndSlowSeconds + 0.25f);
            yield return null;
            StringAssert.Contains("Skills taken  " + epic.Skill + " (epic, " + epic.Change.Name + ")", _hud.EndScreenText);

            List<SessionEvent> events = _recorder.Events.ToList();
            SessionEvent offered = events.Single(e => e.Type == SessionEventTypes.RewardOffer);
            Assert.That(offered.ExtraValue("tiers"), Is.EqualTo(string.Join(" | ", words)), "in the order of offered");
            Assert.That(offered.ExtraValue("offered").Split('|').Length, Is.EqualTo(3), "offered as before");
            SessionEvent taken = events.Single(e => e.Type == SessionEventTypes.RewardTake);
            Assert.That(taken.Name, Is.EqualTo(epic.Skill));
            Assert.That(taken.ExtraValue("tier"), Is.EqualTo("epic"));
            Assert.That(taken.ExtraValue("with"), Is.EqualTo(epic.Change.Name));
            _recorder.StopRecording();
            StringAssert.Contains("  skills taken     " + epic.Skill + " (epic, " + epic.Change.Name + ")", File.ReadAllText(_recorder.SummaryPath));
        }

        [UnityTest]
        public IEnumerator ARareTaken_IsWrittenWithItsTier_AndAnEmptyWith()
        {
            ulong seed = SeedWithFirstCard(UpgradeTier.Rare);
            _folder = Path.Combine(Path.GetTempPath(), "SkillTiersTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return FloorOnesReward(seed);
            string skill = _levels.OfferedSkillTiers[0].Skill;
            Assert.That(_levels.PickReward(0), Is.True);
            SessionEvent taken = _recorder.Events.Single(e => e.Type == SessionEventTypes.RewardTake);
            Assert.That(taken.ExtraValue("tier"), Is.EqualTo("rare"));
            Assert.That(taken.ExtraValue("with"), Is.EqualTo(""));
            Assert.That(_levels.Record.SkillsText, Is.EqualTo(skill + " (rare)"));
            Assert.That(_hud.KeyLines().Single(l => l.Contains(skill)), Does.EndWith(skill + "  rare"), "no upgrades: the tier's word last");
            _levels.BackToPracticeRoom();
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator SwitchedOff_TheFloorRewardsChoice_Cards_Events_AndTheSkillsNumbers_AreAsBefore()
        {
            ulong seed = SeedWithFirstCard(UpgradeTier.Epic); // a seed whose first card would be an epic with the switch on
            _levels.RunValues.SkillTiers = false;
            _folder = Path.Combine(Path.GetTempPath(), "SkillTiersTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(seed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.OfferedSkills, Is.EqualTo(FloorRewards.Offer(seed, 1, StartFour(), 3)), "the same slots");
            Assert.That(_levels.OfferedSkillTiers, Is.Empty, "no tiers");
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(_levels.CardNumeralInk(i), Is.EqualTo(UiInk.White));
                Assert.That(_levels.CardText(i), Does.StartWith("key "));
                Assert.That(_levels.RewardChoiceText(i).Split('\n').Length, Is.EqualTo(2), "the old look's two lines");
            }
            int slot = _levels.OfferedSkills[0];
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0);
            yield return null;
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero);
            Assert.That(_player.Skills, Is.SameAs(_player.SettingsSkills), "the settings' numbers");
            Assert.That(_player.UpgradesOn(slot), Is.Zero);
            Assert.That(_levels.Record.SkillsText, Is.EqualTo(_player.Skills[slot].Name));
            SessionEvent offered = _recorder.Events.Single(e => e.Type == SessionEventTypes.RewardOffer);
            Assert.That(offered.Extra.Select(x => x.Key), Is.EqualTo(new[] { "floor", "offered" }), "today's fields");
            SessionEvent taken = _recorder.Events.Single(e => e.Type == SessionEventTypes.RewardTake);
            Assert.That(taken.Extra.Select(x => x.Key), Is.EqualTo(new[] { "floor" }));
            _levels.BackToPracticeRoom();
        }
    }
}
