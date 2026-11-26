using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 065 (Kristian, 2026-11-21: "kisten skal ha flere valg, gjerne 3", "oppgraderingene trenger mer informasjon ...
    /// gjerne i midten som en større pop-up", "og mere stats"), in the ControlRoom scene through the intents: a chest opens three
    /// cards in the middle, at a view 1920 wide (a 1920 by 1080 or a 1280 by 720 screen) and 1440 wide (4:3), inside the view,
    /// side by side, every part measured inside its rectangle and on the card's leaning shape; a card's numbers are true (taken,
    /// her stat is its "after"); the guard holds; a card's leaning corner is on it and the patch beside it is not; the keys
    /// pick; the floor's reward, the elite's upgrades and the focus choice come in the middle with their sentences; each new
    /// stat does what its card says; the pause menu and the recorder name the new stats; the old look has the same words;
    /// nothing is made per frame while an offer is open. No frame is drawn: what the cards are is worked out without a window.
    /// </summary>
    public class OfferPopUpTests
    {
        const ulong RunSeed = 0x065C4E57UL;
        const int MostFrames = 600;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            // Changed deliberately (work order 138): these pin the floor reward of before (three skills, the focus screen
            // after floor 1) on eight slots, so its six slots and its reward of three kinds are off here.
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            KitSwitchesForTests.EightSlots(_player);
            _reports.Clear();
            _player.Reported += r => _reports.Add(r);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.Shared.Kinetic = true;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_health != null) _health.Untouchable = false;
            if (_player != null && (_levels == null || _player.Modifiers != _levels.Modifiers)) _player.Modifiers = null; // those put on by hand
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator RealTime(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            RealTime(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        int Slot(string name) => _player.SlotOf(name);

        static Upgrade Step(UpgradeKind kind, float amount, string skill = "") => new Upgrade { Kind = kind, Amount = amount, Tiered = true, Skill = skill };

        RunModifiers ByHand()
        {
            var m = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = 0f } };
            _player.Modifiers = m;
            return m;
        }

        List<PlayerReport> Hits(int slot) => _reports.Where(r => r.Kind == PlayerReportKind.Hit && r.Target == _a && r.Slot == slot).ToList();

        IEnumerator OpenAChest(int which = 0)
        {
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assert.That(chests.Count, Is.GreaterThan(which), "a chest on the floor");
            Assert.That(_levels.OpenChest(chests[which]), Is.True);
            yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
        }

        /// <summary>
        /// The cards on screen in the middle at a view <paramref name="viewWidth"/> wide: inside the view with their lean, side by
        /// side and never overlapping, centred, every part measured inside its rectangle, every rectangle on the card as it is seen.
        /// </summary>
        void CardsInTheMiddle(string what, float viewWidth, int expected = -1)
        {
            _levels.ViewWidthForTests = viewWidth;
            int count = _levels.KineticCards();
            if (expected >= 0) Assert.That(count, Is.EqualTo(expected), what + ": the cards");
            Assert.That(_levels.OfferPopUp, Is.True);
            float slant = _levels.OfferSlant, viewHeight = InterfaceStyle.V.LayoutHeight;
            Rect first = _levels.CardRect(0), last = _levels.CardRect(count - 1);
            Assert.That((first.xMin + last.xMax) * 0.5f, Is.EqualTo(viewWidth * 0.5f).Within(0.5f), what + ": centred");
            Assert.That(first.center.y, Is.EqualTo(viewHeight * _levels.Offers.RowMiddle).Within(0.5f), what + ": in the middle band");
            float row = last.xMax - first.xMin + slant * first.height;
            Assert.That(row, Is.LessThanOrEqualTo(_levels.Offers.MostRowShare * viewWidth + 0.5f), what + ": never wider than its share");
            for (int i = 0; i < count; i++)
            {
                Rect r = _levels.CardRect(i);
                string n = what + ", card " + i + " (" + _levels.CardTitle(i) + "): ";
                Assert.That(r.height, Is.EqualTo(first.height), n + "one height");
                Assert.That(r.xMin - slant * r.height * 0.5f, Is.GreaterThanOrEqualTo(0f), n + "inside the view");
                Assert.That(r.xMax + slant * r.height * 0.5f, Is.LessThanOrEqualTo(viewWidth), n + "inside the view");
                Assert.That(r.yMin, Is.GreaterThan(0f));
                Assert.That(r.yMax, Is.LessThan(viewHeight));
                if (i > 0) Assert.That(r.xMin, Is.GreaterThan(_levels.CardRect(i - 1).xMax), n + "beside the last, not over it");
                Rect name = _levels.CardNameRect(i);
                Assert.That(InterfaceStyle.MeasureHeight(_levels.CardTitle(i), _levels.CardNameStyle, name.width), Is.LessThanOrEqualTo(name.height + 0.5f), n + "the name fits");
                OnTheCard(i, name, n + "the name");
                OnTheCard(i, _levels.CardKeyRect(i), n + "the key");
                Assert.That(_levels.CardPartCount(i), Is.GreaterThan(0), n + "its parts");
                for (int p = 0; p < _levels.CardPartCount(i); p++)
                {
                    Rect part = _levels.CardPartRect(i, p);
                    GUIStyle style = _levels.CardPartStyle(i, p);
                    string text = _levels.CardPartText(i, p);
                    Assert.That(InterfaceStyle.MeasureHeight(text, style, part.width), Is.LessThanOrEqualTo(part.height + 0.5f), n + "\"" + text + "\" fits");
                    OnTheCard(i, part, n + "\"" + text + "\"");
                    if (p > 0) Assert.That(part.yMin, Is.GreaterThanOrEqualTo(_levels.CardPartRect(i, p - 1).yMax - 0.01f), n + "under the part before");
                    if (_levels.CardPartKind(i, p) == OfferPartKind.Sentence)
                        Assert.That(InterfaceStyle.MeasureHeight(text, style, part.width), Is.LessThanOrEqualTo(_levels.Offers.MostSentenceLines * style.lineHeight + 1f),
                                    n + "the sentence in two lines at most");
                }
                StringAssert.Contains(_levels.CardText(i).Split('\n').Last(), _levels.OfferCardAt(i).ToString(), n + "the card is the rule's");
            }
        }

        void OnTheCard(int card, Rect r, string what)
        {
            foreach (Vector2 corner in new[] { new Vector2(r.xMin + 0.5f, r.yMin + 0.5f), new Vector2(r.xMax - 0.5f, r.yMin + 0.5f), new Vector2(r.xMin + 0.5f, r.yMax - 0.5f), new Vector2(r.xMax - 0.5f, r.yMax - 0.5f) })
                Assert.That(_levels.CardHit(card, corner), Is.True, what + ": its corner " + corner + " on the card as it leans");
        }

        // ---------------- A chest: three cards in the middle ----------------

        [UnityTest]
        public IEnumerator AChest_OpensThreeCards_InTheMiddle_At1920And1440Wide_EveryPartInsideItsCard()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return OpenAChest();
            IReadOnlyList<Upgrade> offer = _levels.OfferedUpgrades;
            Assert.That(offer.Count, Is.EqualTo(3), "three choices");
            Assert.That(offer.Select(u => u.Stat).Distinct().Count(), Is.EqualTo(3), "of three different stats");
            Assert.That(_levels.ChoiceKinetic, Is.True);
            CardsInTheMiddle("a chest at 1920", 1920f, 3);
            Assert.That(_levels.OfferScale, Is.EqualTo(1f), "three cards at 16:9: their full size");
            Assert.That(_levels.CardRect(0).width, Is.EqualTo(380f).Within(0.01f), "380 layout pixels: 380 real at 1080, 253 at 720");
            Assert.That(_levels.CardRect(0).height, Is.GreaterThanOrEqualTo(300f));
            for (int i = 0; i < 3; i++)
            {
                OfferCard card = _levels.OfferCardAt(i);
                Assert.That(card.Name, Is.EqualTo(OfferTexts.Name(offer[i])), "its name");
                Assert.That(card.TierLine, Is.EqualTo(offer[i].TierWord.ToUpperInvariant()), "its tier");
                Assert.That(card.Numbers, Is.Not.Empty, "its numbers");
                Assert.That(_levels.CardNumeralInk(i), Is.EqualTo(InterfaceRules.TierInk(true, offer[i].Tier)));
            }
            CardsInTheMiddle("a chest at 1440", 1440f, 3);
            Assert.That(_levels.OfferScale, Is.LessThan(1f), "4:3: scaled to fit");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator ACardsNumbers_AreTrue_TakenHerStatIsItsAfter()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.TakeHit(30);
            for (int chest = 0; chest < 2; chest++)
            {
                yield return OpenAChest(chest);
                _levels.KineticCards();
                int pick = chest; // the first card of one chest, the second of the next
                Upgrade u = _levels.OfferedUpgrades[pick];
                OfferCard card = _levels.OfferCardAt(pick);
                Assert.That(_levels.PickUpgrade(pick), Is.True);
                yield return null;
                // Her numbers now, read the same way: a card of the same kind with nothing in it says them as its "before".
                var nothing = new Upgrade { Kind = u.Kind, Amount = 0f, Heal = 0, Skill = u.Skill, Tiered = true };
                var context = new OfferContext
                {
                    Modifiers = _levels.Modifiers, Run = _levels.RunValues, Feel = _player.Feel, Kit = _player.Kit, Identity = _player.Identity,
                    FocusValues = _player.FocusModeValues, FocusMode = _player.FocusMode, OwnMaxHealth = _health.MaxHealth, Health = _health.Current,
                    Equipped = Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).Select(s => _player.Skills[s]).ToList(),
                };
                OfferCard now = OfferTexts.ForUpgrade(nothing, context);
                for (int line = 0; line < card.Numbers.Count; line++)
                {
                    string after = card.Numbers[line].Substring(card.Numbers[line].IndexOf(OfferTexts.Arrow, StringComparison.Ordinal) + OfferTexts.Arrow.Length);
                    string nowLine = now.Numbers[line];
                    string nowValue = nowLine.Substring(0, nowLine.IndexOf(OfferTexts.Arrow, StringComparison.Ordinal)); // its label and her number now
                    string unit = after.EndsWith(" shots a second", StringComparison.Ordinal) ? " shots a second" : after.EndsWith(" m a second", StringComparison.Ordinal) ? " m a second" : "";
                    StringAssert.EndsWith("  " + After(after, unit), "  " + nowValue, u.Short + ": the card's after, \"" + card.Numbers[line] + "\", is hers now: \"" + nowLine + "\"");
                }
            }

            // Work order 077 (the sixth review's first finding: the lines above hold a card against the card code's own sums):
            // an Attack speed and a Reach card from her values as the game hands them to a card, against what PlayerCharacter
            // itself gives once the card is taken, her interval and her reach; the stock gun, then with her pistols' parts fitted.
            OfferContext Context() => new OfferContext
            {
                Modifiers = _levels.Modifiers, Run = _levels.RunValues, Feel = _player.Feel, Kit = _player.Kit, Identity = _player.Identity,
                FocusValues = _player.FocusModeValues, FocusMode = _player.FocusMode, OwnMaxHealth = _health.MaxHealth, Health = _health.Current,
                Equipped = Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).Select(s => _player.Skills[s]).ToList(),
            };
            Assert.That(_player.Modifiers, Is.SameAs(_levels.Modifiers), "her run's modifiers");
            for (int fitted = 0; fitted < 2; fitted++)
            {
                if (fitted == 1)
                    foreach (string id in new[] { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel })
                        _levels.Modifiers.Weapon.Add(WeaponRack.Find(_levels.MetaValues, id));
                foreach (UpgradeKind kind in new[] { UpgradeKind.AttackSpeed, UpgradeKind.LongerBarrel })
                {
                    bool speed = kind == UpgradeKind.AttackSpeed;
                    var step = new Upgrade { Kind = kind, Amount = speed ? 0.12f : 1f, Tiered = true, Tier = UpgradeTier.Rare };
                    string Game() => speed ? (1.0 / _player.BasicShotInterval).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                           : OfferTexts.Metres(_player.BasicShotRange);
                    OfferCard shown = OfferTexts.ForUpgrade(step, Context());
                    string was = Game();
                    _levels.Modifiers.Take(step);
                    string then = Game();
                    _levels.Modifiers.TakeBack(step);
                    Assert.That(shown.Numbers.First(), Is.EqualTo((speed ? "" : "reach  ") + was + OfferTexts.Arrow + then + (speed ? " shots a second" : "")),
                                OfferTexts.Name(step) + (fitted == 1 ? ", a Laser Sight and a Bull Barrel fitted" : ", the stock gun") + ": the game's number now and once taken");
                }
            }
            // Work order 082 (the sixth review's first finding, what 077 left): a Cooldown card against CooldownOf, the game's own
            // reader of a skill's cooldown, now and once taken.
            int slot = Enumerable.Range(0, _player.Skills.Count).First(s => _player.IsEquipped(s) && _player.Skills[s].Cooldown > 0f);
            string skillName = _player.Skills[slot].Name;
            var cut = new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.1f, Skill = skillName, Tiered = true, Tier = UpgradeTier.Rare };
            OfferCard cooldownCard = OfferTexts.ForUpgrade(cut, Context());
            string cooldownWas = OfferTexts.Sec(_player.CooldownOf(slot));
            _levels.Modifiers.Take(cut);
            string cooldownThen = OfferTexts.Sec(_player.CooldownOf(slot));
            _levels.Modifiers.TakeBack(cut);
            Assert.That(cooldownWas, Is.Not.EqualTo(cooldownThen), "the cut moves it");
            Assert.That(cooldownCard.Numbers.First(), Is.EqualTo(skillName + " cooldown  " + cooldownWas + OfferTexts.Arrow + cooldownThen), "a Cooldown card: the game's cooldown now and once taken");
            Assert.That(_player.BasicShotRange, Is.EqualTo(12f + _levels.Modifiers.BasicShotRangeBonus).Within(1e-4f), "the sight's 2 m are on her");
            _levels.Modifiers.Weapon.Clear();
            _levels.BackToPracticeRoom();
        }

        /// <summary>The "after" of a number line without its trailing unit.</summary>
        static string After(string after, string unit) => unit.Length > 0 ? after.Substring(0, after.Length - unit.Length) : after;

        [UnityTest]
        public IEnumerator TheGuardHolds_TheKeysPick_AndACardsLeaningCornerIsOnIt()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            yield return OpenAChest();
            _levels.ViewWidthForTests = 1920f;
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(_levels.ChoiceLive, Is.False, "guarded at first");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Digit2));
            yield return Frames(() => _keyboard.digit2Key.isPressed);
            Assume.That(Time.unscaledTime, Is.LessThan(_levels.ChoiceLiveFrom), "still inside the guard");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest), "a press inside the guard picks nothing");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return Frames(() => !_keyboard.anyKey.isPressed);
            yield return RealTime(() => _levels.ChoiceLive, 3f);

            // The leaning shape at the new size: the seen tips are on a card, the patches beside them are not.
            for (int i = 0; i < 3; i++)
            {
                Rect r = _levels.CardRect(i);
                float slant = _levels.OfferSlant, half = r.height * 0.5f;
                Assume.That(slant * (half - 2f), Is.GreaterThan(4f), "the card leans enough to tell");
                Assert.That(_levels.CardHit(i, new Vector2(r.xMax + slant * half - 3f, r.y + 2f)), Is.True, "card " + i + ": the seen upper right tip");
                Assert.That(_levels.CardHit(i, new Vector2(r.x - slant * half + 3f, r.yMax - 2f)), Is.True, "card " + i + ": the seen lower left tip");
                Assert.That(_levels.CardHit(i, new Vector2(r.xMax - 3f, r.yMax - 2f)), Is.False, "card " + i + ": beside its lower right corner");
                Assert.That(_levels.CardHit(i, new Vector2(r.x + 3f, r.y + 2f)), Is.False, "card " + i + ": beside its upper left corner");
                for (int j = 0; j < 3; j++)
                    if (j != i) Assert.That(_levels.CardHit(j, r.center), Is.False, "card " + i + "'s middle is on no other card");
            }

            // A fresh 2 after the guard takes the second card.
            Upgrade second = _levels.OfferedUpgrades[1];
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Digit2));
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "the key picked");
            Assert.That(_levels.Modifiers.Taken, Is.EqualTo(new[] { second }), "the second card");
            Assert.That(_levels.LastPickedCard, Is.EqualTo(1));
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;

            // The other chest: 3 takes the third.
            yield return OpenAChest(1);
            yield return RealTime(() => _levels.ChoiceLive, 3f);
            Upgrade third = _levels.OfferedUpgrades[2];
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Digit3));
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Modifiers.Taken.Last(), Is.SameAs(third), "3 picks the third");
            _levels.BackToPracticeRoom();
        }

        // ---------------- The other offers, in the middle with their sentences ----------------

        [UnityTest]
        public IEnumerator TheFloorsReward_TheFocusChoice_AndTheElitesUpgrades_ComeInTheMiddle_WithTheirSentences()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            CardsInTheMiddle("a floor reward", 1920f, _levels.OfferedSkills.Count);
            for (int i = 0; i < _levels.CardCount; i++)
            {
                SkillDefinition s = _player.Skills[_levels.OfferedSkills[i]];
                OfferCard card = _levels.OfferCardAt(i);
                Assert.That(card.Name, Is.EqualTo(s.Name));
                Assert.That(card.Sentence, Is.EqualTo(OfferTexts.SkillSentence(s)), "what it is for and how it is pressed");
                StringAssert.StartsWith("key ", card.For, "its key and cooldown under its name");
                StringAssert.Contains("cooldown ", card.For);
                SkillTierOffer tier = _levels.OfferedTierAt(i);
                if (tier != null && tier.Tiered) StringAssert.StartsWith(tier.Word.ToUpperInvariant(), card.TierLine, "a rare's or an epic's tier");
                if (tier != null && tier.Change != null) Assert.That(card.Notes.Single(), Does.Contain(tier.Change.Name.ToUpperInvariant()), "the epic's change");
            }
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                CardsInTheMiddle("the focus choice", 1920f, 2);
                Assert.That(_levels.OfferCardAt(0).Name, Is.EqualTo("Hair Trigger"));
                Assert.That(_levels.OfferCardAt(0).Sentence, Is.EqualTo(OfferTexts.ForFocus(FocusMode.HairTrigger, null).Sentence));
                Assert.That(_levels.OfferCardAt(1).Numbers.Count, Is.GreaterThan(0));
                Assert.That(_levels.PickFocusChoice(0), Is.True);
            }
            yield return null;
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's upgrade on floor 2");
            CardsInTheMiddle("a skill upgrade", 1920f, _levels.OfferedSkillUpgrades.Count);
            for (int i = 0; i < _levels.CardCount; i++)
            {
                SkillUpgradeDefinition u = _levels.OfferedSkillUpgrades[i];
                OfferCard card = _levels.OfferCardAt(i);
                Assert.That(card.Name, Is.EqualTo(u.Name));
                Assert.That(card.Sentence, Is.EqualTo(OfferTexts.UpgradeSentence(u)));
                StringAssert.StartsWith(u.Skill, card.For, "the skill it is for");
                Assert.That(card.Numbers, Is.Not.Empty, u.FullName + ": its numbers");
            }
            _levels.BackToPracticeRoom();
        }

        // ---------------- Each new stat does what its card says ----------------

        [UnityTest]
        public IEnumerator Cooldowns_CutsEverySkill_AndSecondDash_HoldsTwoDashes()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            int scatter = Slot("Scatter Blast"), backstep = Slot("Backstep");
            float s0 = _player.CooldownOf(scatter), b0 = _player.CooldownOf(backstep);
            _levels.Modifiers.Take(Step(UpgradeKind.AllCooldowns, 0.08f));
            Assert.That(_player.CooldownOf(scatter), Is.EqualTo(s0 * 0.92f).Within(1e-4f), "every skill 8 % shorter");
            Assert.That(_player.CooldownOf(backstep), Is.EqualTo(b0 * 0.92f).Within(1e-4f));

            Assert.That(_player.DashMaxCharges, Is.EqualTo(1));
            _levels.Modifiers.Take(Step(UpgradeKind.SecondDash, 1f));
            _player.RefreshDashCharges();
            Assert.That(_player.DashMaxCharges, Is.EqualTo(2), "two dashes held");
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started));
            yield return RealTime(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started), "the second at once");
            yield return RealTime(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Rejected), "then it must recharge");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_player.DashMaxCharges, Is.EqualTo(1), "out of the run: one again");
        }

        [UnityTest]
        public IEnumerator CleanPress_RaisesACleanTap_AndMarkedPrey_AHitOnAMarkedTarget()
        {
            RunModifiers m = ByHand();
            m.Take(Step(UpgradeKind.CleanPress, 0.14f));
            Upgrade prey = Step(UpgradeKind.MarkedPrey, 0.1f);
            m.Take(prey);
            int tap = Slot("Triple Tap");
            _player.Teleport(NearA);
            yield return null;
            _a.TakeMarks();
            _player.Shoot(_a.transform.position);
            yield return RealTime(() => Hits(-1).Count > 0, 2f);
            Assert.That(Hits(-1).Last().Damage, Is.EqualTo(Placeholders.BasicShotDamage), "an unmarked target: as it was");
            yield return UntilFree();
            _a.TakeMarks();
            _a.AddMarks(2, _player.Kit.MaxMarks, 60f);
            int had = Hits(-1).Count;
            _player.Shoot(_a.transform.position);
            yield return RealTime(() => Hits(-1).Count > had, 2f);
            Assert.That(Hits(-1).Last().Damage, Is.EqualTo(Mathf.RoundToInt(Placeholders.BasicShotDamage * 1.1f)), "a marked target: 10 % more");
            yield return UntilFree();

            m.TakeBack(prey); // Triple Tap marks what it hits: Marked prey off, so the clean tap's damage is the clean press's alone
            _a.TakeMarks();
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return RealTime(() => _player.IsRecastWindowOpen, 2f);
            _a.TakeMarks();
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return RealTime(() => _player.RecastPressWouldBeClean, 2f);
            Assert.That(_player.RecastPressWouldBeClean, Is.True, "the clean part of the last window");
            _a.TakeMarks();
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return RealTime(() => Hits(tap).Count >= 3, 2f);
            int damage = _player.Skills[tap].Damage;
            PlayerReport clean = Hits(tap).Last();
            Assert.That(clean.Crit, Is.True);
            Assert.That(clean.Damage, Is.EqualTo(CritRules.Apply(Mathf.RoundToInt(damage * CritRules.Folded(_player.Kit.CleanDamage, m.StatValues) * 1.14f), m.Stats.CritDamage)),
                        "the clean press 14 % more, on its own bonus");
        }

        [UnityTest]
        public IEnumerator Mending_HealsMoreForAClearedRoom_AndLongFocus_LengthensTheWindow()
        {
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Take(Step(UpgradeKind.Mending, 5f));
            _levels.Modifiers.Take(Step(UpgradeKind.LongFocus, 0.18f));
            _health.TakeHit(60);
            int before = _health.Current;
            LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite && level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) && l.Count > 0);
            foreach (Enemy e in level.EnemiesByRoom[room.Order]) e.Body.TakeHit(100000000);
            yield return Frames(() => _health.Current > before);
            int expected = FloorHeals.Amount(_health.Max, _levels.RunValues.RoomHeal) + 5;
            Assert.That(_health.Current - before, Is.EqualTo(expected), "the room's own heal and Mending's 5");

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.EmpoweredRemaining, Is.EqualTo(_player.Identity.EmpoweredDuration * 1.18f).Within(0.05f), "the window 18 % longer");
            _levels.BackToPracticeRoom();
        }

        // ---------------- The pause menu, the recorder, the old look, nothing per frame ----------------

        [UnityTest]
        public IEnumerator ThePauseMenu_AndTheRecorder_NameTheNewStats()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeOfferTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _hud = RunHud.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Take(Step(UpgradeKind.CleanPress, 0.08f));
            _levels.Modifiers.Take(Step(UpgradeKind.SecondDash, 1f));
            Assert.That(_hud.Escape(), Is.True);
            List<string> lines = _hud.StatLines();
            Assert.That(lines, Does.Contain("Clean press  +8 %  (+0 %)"));
            Assert.That(lines, Does.Contain("Dashes  2  (1)"));
            Assert.That(lines.Any(l => l.StartsWith("Marked prey")) && lines.Any(l => l.StartsWith("Mending")) && lines.Any(l => l.StartsWith("Focus time")), Is.True);
            Assert.That(_hud.Escape(), Is.True);
            yield return OpenAChest();
            Assert.That(_levels.PickUpgrade(0), Is.True);
            SessionEvent chest = _recorder.Events.Last(e => e.Type == SessionEventTypes.ChestOpen);
            Assert.That(chest.ExtraValue("offered").Split('|').Length, Is.EqualTo(3), "the three it offered");
            StringAssert.Contains("clean +8%", chest.ExtraValue("stats"));
            StringAssert.Contains("dashes 2", chest.ExtraValue("stats"));
            _levels.BackToPracticeRoom();
            yield return null;
            StringAssert.Contains("clean +8%", _recorder.Events.Last(e => e.Type == SessionEventTypes.RunEnd).ExtraValue("stats"), "her final stats, in the summary's line");
        }

        [UnityTest]
        public IEnumerator TheOldLook_DrawsTheSameWords_AndNothingIsMadePerFrame()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return OpenAChest();
            _levels.ViewWidthForTests = 1920f;
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.KineticCards(); }, NotAllocating, "the pop-up's cards are made once");
            InterfaceStyle.Shared.Kinetic = false;
            Assert.That(_levels.PlainCards(), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                StringAssert.Contains(_levels.OfferCardAt(i).Body, _levels.PlainCardText(i), "the same words in the plain box");
                StringAssert.Contains(_levels.OfferCardAt(i).Name.ToUpperInvariant(), _levels.PlainCardText(i));
            }
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.PlainCards(); }, NotAllocating, "the plain boxes' words are made once");
            InterfaceStyle.Shared.Kinetic = true;
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator WithThePopUpOff_TheCardsAreThisMorningsAtTheRightEdge()
        {
            _levels.Offers.PopUp = false;
            _levels.StartRun(RunSeed);
            yield return null;
            yield return OpenAChest();
            _levels.ViewWidthForTests = 1920f;
            Assert.That(_levels.KineticCards(), Is.EqualTo(3), "still three: the count is the chest's");
            Assert.That(_levels.CardRect(0).xMin, Is.GreaterThan(1920f * 0.6f), "at the right edge");
            Assert.That(_levels.OfferCardAt(0), Is.Null, "no pop-up card");
            _levels.Offers.PopUp = true;
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(_levels.CardRect(0).xMin, Is.LessThan(1920f * 0.5f), "switched on: in the middle");
            _levels.BackToPracticeRoom();
        }
    }
}
