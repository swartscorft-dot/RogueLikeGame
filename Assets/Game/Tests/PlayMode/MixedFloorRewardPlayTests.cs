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
    /// Work order 138, part 2 (his ticks of 2026-11-25, "Tre kort av tre slag" and "Kortet blir et annet slag"), in a run: the
    /// floor reward shows three cards of three kinds, each card says its kind (a skill card its slot, "slot 5 of 6"), each kind
    /// taken does what its card says, she is healed and goes on with no focus screen after floor 1, with six slots full there is
    /// no skill card, Second Look draws only the skill card again, and the log names each card's kind and the one taken. Off,
    /// the reward of before with its focus screen.
    /// </summary>
    public class MixedFloorRewardPlayTests
    {
        const ulong FirstSeed = 0x13800100UL;
        const int MostFrames = 600;
        const int MostSeeds = 40;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject); // made by a test here; it outlives the scene
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        IEnumerator FinishFloor()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "floor " + _levels.Floor + "'s reward");
        }

        int CardOf(FloorRewardKind kind)
        {
            IReadOnlyList<FloorRewardCard> cards = _levels.OfferedRewardCards;
            for (int i = 0; i < cards.Count; i++) if (cards[i].Kind == kind) return i;
            return -1;
        }

        /// <summary>Runs from seed after seed until floor 1's reward shows a card of this kind; its place.</summary>
        IEnumerator FirstRewardWith(FloorRewardKind kind, Action<int> found)
        {
            for (int n = 0; n < MostSeeds; n++)
            {
                _levels.StartRun(FirstSeed + (ulong)n);
                yield return null;
                yield return FinishFloor();
                int at = CardOf(kind);
                if (at >= 0)
                {
                    found(at);
                    yield break;
                }
            }
            Assert.Fail("no floor 1 reward of " + MostSeeds + " seeds showed a " + kind + " card");
        }

        IEnumerator TakeAndGoOn(int card)
        {
            _health.RestoreFull();
            _health.TakeHit(60); // under her maximum, to see the heal (from full, so four rewards in a row never kill her)
            yield return null;
            int health = _health.Current, floor = _levels.Floor;
            Assert.That(_levels.PickReward(card), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no screen after it: the focus screen after floor 1 is gone");
            Assert.That(_levels.Floor, Is.EqualTo(floor + 1), "on to the next floor");
            Assert.That(_health.Current, Is.EqualTo(health + FloorHeals.Amount(_health.Max, _levels.RunValues.FloorHeal)), "the heal of before, on any card");
            Assert.That(_levels.OfferedRewardCards, Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheReward_ShowsThreeKinds_EachSayingWhatItIs_TheSkillCardItsSlot()
        {
            Assert.That(_levels.RunValues.MixedFloorReward, Is.True, "on by default");
            _levels.StartRun(FirstSeed);
            yield return null;
            yield return FinishFloor();
            IReadOnlyList<FloorRewardCard> cards = _levels.OfferedRewardCards;
            Assert.That(cards.Count, Is.EqualTo(3));
            Assert.That(cards.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(3), "three kinds");
            Assert.That(cards[0].Kind, Is.EqualTo(FloorRewardKind.Skill));
            Assert.That(cards[1].Kind, Is.EqualTo(FloorRewardKind.BasicShot));
            Assert.That(_levels.OfferedSkills, Is.EqualTo(new[] { cards[0].Slot }), "the skill card's skill, where the drop and the tiers read it");
            Assert.That(_levels.KineticCards(), Is.EqualTo(3), "three cards drawn");
            for (int i = 0; i < 3; i++)
            {
                OfferCard card = _levels.OfferCardAt(i);
                Assert.That(card, Is.Not.Null);
                Assert.That(card.Parts().First().Kind, Is.EqualTo(OfferPartKind.Kind), "its kind first under the name: " + card);
                Debug.Log("[138] card " + (i + 1) + ": " + card.Name + "  |  " + card.KindLine);
            }
            Assert.That(_levels.OfferCardAt(0).KindLine, Is.EqualTo("NEW SKILL   SLOT 5 OF 6"));
            Assert.That(_levels.OfferCardAt(1).KindLine, Is.EqualTo("BASIC SHOT UPGRADE"));
            Assert.That(_levels.OfferCardAt(2).KindLine, Is.EqualTo(cards[2].KindWord));
            Assert.That(_levels.CardPartKind(0, 0), Is.EqualTo(OfferPartKind.Kind), "drawn first");
        }

        [UnityTest]
        public IEnumerator TheSkillCard_EquipsItOnKey1_AndNoFocusScreenFollows()
        {
            _levels.StartRun(FirstSeed);
            yield return null;
            yield return FinishFloor();
            int slot = _levels.OfferedRewardCards[0].Slot;
            yield return TakeAndGoOn(0);
            Assert.That(_player.IsEquipped(slot), Is.True);
            Assert.That(_player.KeyOfSkill(slot), Is.EqualTo(1), "on key 1");
            Assert.That(_levels.SkillsTaken, Is.EqualTo(new[] { _player.Skills[slot].Name }));
            Assert.That(_levels.FocusChoice.Offered, Is.False, "no focus screen");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today), "today's gauge, as without a way");
        }

        [UnityTest]
        public IEnumerator TheBasicShotCard_TakesItsUpgrade_IntoTheRunAndHerStats()
        {
            _levels.StartRun(FirstSeed);
            yield return null;
            yield return FinishFloor();
            int at = CardOf(FloorRewardKind.BasicShot);
            BasicShotOffer shot = _levels.OfferedRewardCards[at].Shot;
            Assert.That(shot.Tier, Is.EqualTo(1), "a first tier");
            yield return TakeAndGoOn(at);
            Assert.That(_levels.Modifiers.BasicShot.TierOf(shot.Kind), Is.EqualTo(1));
            Assert.That(_player.ShotTier(shot.Kind), Is.EqualTo(1), "it works on her shot");
            Assert.That(_levels.Record.Upgrades, Does.Contain(shot.Short), "on the end screen's list");
            RunHud hud = RunHud.Ensure();
            yield return null; // the pause menu's panel finds her on its first frame
            if (_levels.RunValues.StatsAndTiers)
                Assert.That(hud.StatLines(), Has.Some.StartsWith(shot.Short), "in the pause menu's stats");
            Assert.That(_levels.SkillsTaken, Is.Empty, "no skill taken");
        }

        [UnityTest]
        public IEnumerator TheFocusCard_TakesItsWay_ForTheRun()
        {
            int at = -1;
            yield return FirstRewardWith(FloorRewardKind.Focus, i => at = i);
            FocusMode mode = _levels.OfferedRewardCards[at].Focus;
            yield return TakeAndGoOn(at);
            Assert.That(_levels.FocusChoice.Taken, Is.True);
            Assert.That(_player.FocusMode, Is.EqualTo(mode));
            Assert.That(_levels.Record.FocusChoice, Is.EqualTo(FocusModeRules.Name(mode)));
            // Never again in the run.
            for (int floor = 2; floor <= 3; floor++)
            {
                yield return FinishFloor();
                Assert.That(CardOf(FloorRewardKind.Focus), Is.EqualTo(-1), "no focus card once one is taken (floor " + floor + ")");
                yield return TakeAndGoOn(CardOf(FloorRewardKind.BasicShot));
            }
        }

        [UnityTest]
        public IEnumerator TheSkillUpgradeCard_TakesItsUpgrade()
        {
            int at = -1;
            yield return FirstRewardWith(FloorRewardKind.SkillUpgrade, i => at = i);
            SkillUpgradeDefinition u = _levels.OfferedRewardCards[at].Upgrade;
            Assert.That(_player.IsEquipped(_player.SlotOf(u.Skill)), Is.True, "of a skill she has");
            yield return TakeAndGoOn(at);
            Assert.That(_levels.SkillUpgrades.Taken, Does.Contain(u));
            Assert.That(_levels.Record.SkillUpgrades, Does.Contain(u.FullName));
        }

        [UnityTest]
        public IEnumerator TheStatCard_TakesItsLargeStep()
        {
            int at = -1;
            yield return FirstRewardWith(FloorRewardKind.Stat, i => at = i);
            Upgrade u = _levels.OfferedRewardCards[at].Stat;
            Assert.That(u.Tier, Is.Not.EqualTo(UpgradeTier.Common), "at the reward odds: rare or epic");
            yield return TakeAndGoOn(at);
            Assert.That(_levels.Modifiers.Taken, Does.Contain(u));
        }

        [UnityTest]
        public IEnumerator WithSixFull_ThereIsNoSkillCard_AndThreeOtherKinds()
        {
            _levels.StartRun(FirstSeed);
            yield return null;
            for (int floor = 1; floor <= 2; floor++)
            {
                yield return FinishFloor();
                Assert.That(CardOf(FloorRewardKind.Skill), Is.EqualTo(0));
                yield return TakeAndGoOn(0);
            }
            Assert.That(_player.FirstFreeKey, Is.EqualTo(-1), "six full");
            yield return FinishFloor();
            IReadOnlyList<FloorRewardCard> cards = _levels.OfferedRewardCards;
            Assert.That(cards.Any(c => c.Kind == FloorRewardKind.Skill), Is.False, "no skill card");
            Assert.That(cards.Count, Is.EqualTo(3), "another kind in its place");
            Assert.That(cards.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(3));
            Assert.That(_levels.OfferedSkills, Is.Empty);
            Assert.That(_levels.CanRedraw, Is.False, "no skill card: nothing for Second Look");
        }

        [UnityTest]
        public IEnumerator SecondLook_DrawsOnlyTheSkillCardAgain()
        {
            _levels.StartRun(FirstSeed);
            yield return null;
            _levels.Modifiers.Meta.RedrawsLeft = 1;
            yield return FinishFloor();
            List<FloorRewardCard> before = _levels.OfferedRewardCards.ToList();
            Assert.That(_levels.CanRedraw, Is.True);
            Assert.That(_levels.RedrawOffer(), Is.True);
            IReadOnlyList<FloorRewardCard> after = _levels.OfferedRewardCards;
            Assert.That(after.Count, Is.EqualTo(3));
            Assert.That(after[0].Kind, Is.EqualTo(FloorRewardKind.Skill));
            Assert.That(after[0].Slot, Is.Not.EqualTo(before[0].Slot), "another skill");
            Assert.That(_levels.OfferedSkills, Is.EqualTo(new[] { after[0].Slot }));
            Assert.That(after[1], Is.SameAs(before[1]), "the basic-shot card stays");
            Assert.That(after[2], Is.SameAs(before[2]), "and the third");
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(_levels.OfferCardAt(0).Name, Is.EqualTo(_player.Skills[after[0].Slot].Name), "the card made again");
        }

        [UnityTest]
        public IEnumerator TheLog_NamesEachCardsKind_TheOneTaken_AndWhatTheUpgradesDid()
        {
            _folder = Path.Combine(Path.GetTempPath(), "MixedRewardTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(FirstSeed);
            yield return null;
            yield return FinishFloor();
            int at = CardOf(FloorRewardKind.BasicShot);
            string taken = _levels.OfferedRewardCards[at].Shot.Short;
            yield return TakeAndGoOn(at);
            yield return FinishFloor(); // floor 2's reward: the upgrade's use on floor 2 is logged
            List<SessionEvent> events = _recorder.Events.ToList();
            SessionEvent offer = events.First(e => e.Type == SessionEventTypes.RewardOffer);
            Assert.That(offer.ExtraValue("kinds").Split(new[] { " | " }, StringSplitOptions.None).Length, Is.EqualTo(3));
            StringAssert.StartsWith("skill | basic shot | ", offer.ExtraValue("kinds"));
            Assert.That(offer.ExtraValue("offered").Split(new[] { " | " }, StringSplitOptions.None)[1], Is.EqualTo(_levels.Record.Upgrades[0]));
            SessionEvent take = events.Single(e => e.Type == SessionEventTypes.RewardTake);
            Assert.That(take.Name, Is.EqualTo(taken));
            Assert.That(take.ExtraValue("kind"), Is.EqualTo("basic shot"));
            SessionEvent use = events.Single(e => e.Type == SessionEventTypes.BasicShotUse);
            Assert.That(use.Name, Is.EqualTo(taken));
            Assert.That(use.ExtraValue("floor"), Is.EqualTo("2"));
            Assert.That(use.ExtraValue("fired"), Is.EqualTo("0"), "nothing shot on floor 2");
            Assert.That(use.ExtraValue("unit"), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator Off_TheRewardOfBefore_ThreeSkills_AndTheFocusScreenAfterFloor1()
        {
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            _levels.StartRun(FirstSeed);
            yield return null;
            yield return FinishFloor();
            Assert.That(_levels.OfferedRewardCards, Is.Empty);
            Assert.That(_levels.OfferedSkills.Count, Is.EqualTo(3));
            Assert.That(_levels.KineticCards(), Is.EqualTo(3));
            Assert.That(_levels.OfferCardAt(0).KindLine, Is.Empty, "no kind line on the cards of before");
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "the focus screen of before");
        }
    }
}
