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
    /// The fifth review of 2026-11-21 (work order 077), in the ControlRoom scene through the intents: on a floor with passages
    /// the run's end line, the developer's box and the recorder count the rooms as the end screen's record does, without the
    /// passages, and a passage has no number; and the edge it left open, a Long Shot charge held across a chest choice in which
    /// Wide window is taken, is judged, drawn on the cast bar and worded by the band it started with. (Its first fault, the
    /// chest cards' numbers with her pistols' parts fitted, is OfferCardsTests' and OfferPopUpTests'; its third, the screen
    /// between runs' weapon lines, WeaponAttachmentsTests'.)
    ///
    /// A paused game has a time scale of 0: the waits count frames or real time. The charge is timed on frames of a fixed
    /// 2 ms of game time (<see cref="Time.captureDeltaTime"/>), so where it is let go does not hang on the machine.
    /// </summary>
    public class ReviewFix5Tests
    {
        const ulong RunSeed = 0x12345678UL; // RunTests'
        const int MostFrames = 600;
        /// <summary>Game time a frame while the charge is timed: 25 frames across the 0.05 s the pick widens the band's start by.</summary>
        const float FrameSeconds = 0.002f;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        CastBar _bar;
        TimingFeedback _feedback;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<string> _words = new List<string>();
        readonly List<PlayerReport> _fired = new List<PlayerReport>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.DropValues.On = false; // no reward drop lying about: a cleared room's choice as the test pilot's harness has it (work order 042)
            _player.Reported += OnReport;
            _words.Clear();
            _fired.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            if (_player != null) _player.Reported -= OnReport;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_feedback != null)
            {
                _feedback.WordShown -= OnWord;
                Object.Destroy(_feedback.gameObject);
            }
            _feedback = null;
            if (_bar != null) Object.Destroy(_bar.gameObject);
            _bar = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.SkillFired) _fired.Add(r);
        }

        void OnWord(FeedbackShown w) => _words.Add(w.Text);

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

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        IEnumerator StandBy(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }

        // ---------------- The second fault: the rooms counted without the passages ----------------

        [UnityTest]
        public IEnumerator OnAFloorWithPassages_TheEndLine_TheBoxAndTheRecorder_CountTheRoomsAsTheEndScreenDoes()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ReviewFix5Tests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            LevelLayout layout = level.Layout;
            Assert.That(layout.Passages, Is.True, "the floor has passages (LevelValues.Passages, on)");
            int rooms = LevelGeometry.RoomCount(layout);
            Assert.That(rooms, Is.LessThan(layout.Rooms.Count), "the passages are members of the row, not rooms");
            StringAssert.Contains("room 1 of " + rooms + " (entrance)", _levels.StatusText());

            // In a passage: no number.
            LevelRoom passage = layout.Rooms.First(r => r.Kind == RoomKind.Passage);
            _player.Teleport(level.Middle(passage));
            yield return null;
            yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(passage));
            StringAssert.Contains("in a passage (" + rooms + " rooms)", _levels.StatusText());

            // The second combat room: the third room, while its place in the row (its Order) is later.
            LevelRoom second = layout.Rooms.Where(r => r.Kind == RoomKind.Combat).OrderBy(r => r.Order).Skip(1).First();
            int number = LevelGeometry.RoomNumber(layout, second) + 1;
            Assert.That(number, Is.EqualTo(3), "entrance, a combat room, then this one");
            Assert.That(second.Order + 1, Is.Not.EqualTo(number), "counted with the passages it would be another number: the test tells them apart");
            _player.Teleport(level.Middle(second));
            yield return null;
            yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(second));
            StringAssert.Contains("room " + number + " of " + rooms + " (", _levels.StatusText(), "the developer's box");

            _health.TakeHit(100000);
            yield return Frames(() => !_levels.InRun);
            Assert.That(_levels.InRun, Is.False, "death ends the run");
            Assert.That(_levels.Record.Room + 1, Is.EqualTo(number), "the end screen's record");
            StringAssert.Contains(number + " of " + rooms, _levels.Record.RoomText);
            StringAssert.Contains("DIED on floor 1, room " + number + " (", _levels.LastRunLine, "the run's end line says the end screen's number");
            SessionEvent end = _recorder.Events.Last(e => e.Type == SessionEventTypes.RunEnd);
            Assert.That(end.ExtraValue("room"), Is.EqualTo(number.ToString()), "the recorder's run end");
            Assert.That(end.ExtraValue("kind"), Is.EqualTo("combat"));
        }

        // ---------------- The edge: a charge held across a chest choice that widens its band ----------------

        /// <summary>
        /// Long Shot charged, then the interact key's chest opened while it is held, and Wide window taken there (a step of
        /// +50 %, its cap, so the band's start comes 0.05 s earlier). Let go after the widened band's start but before the band
        /// it started with: the producer's reading (work order 077) is that a charge already held is judged AND drawn by the
        /// band it started with. So: not perfect; the cast bar's zone and its light are the old band's; the word over her is
        /// EARLY, by that band (before the fix the word read the slot's widened skill and said nothing at all).
        /// </summary>
        [UnityTest]
        public IEnumerator ALongShotChargeHeldAcrossAChestChoice_WhereWideWindowIsTaken_IsJudgedDrawnAndWordedByTheBandItStartedWith()
        {
            _levels.RunValues.StatsAndTiers = true;
            _levels.RunValues.TieredUpgrades = new[] { new UpgradeDefinition(UpgradeKind.WideWindow, 0.5f, rare: 0.5f, epic: 0.5f) }; // the chest offers only it
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true; // nothing here is about her health, and nothing may stun her out of the charge
            int slot = Slot("Long Shot");
            _player.EquipSkill(slot);
            Assert.That(_player.IsEquipped(slot), Is.True, "Long Shot is hers");
            _bar = CastBar.Ensure();
            _feedback = TimingFeedback.Ensure();
            _feedback.WordShown += OnWord;
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            yield return StandBy(chest);
            Assert.That(_levels.ChestInReach(), Is.SameAs(chest), "within reach");
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

            Time.captureDeltaTime = FrameSeconds;
            Vector3 aim = _levels.Level.Middle(chest.Room);
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), "the charge");
            yield return Until(() => _player.SkillPhase == SkillPhase.Channel && _player.SkillChargedSeconds > 0.2f, 10f);
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel), "charging");
            SkillDefinition started = _player.ActiveSkill;
            float oldFrom = started.PerfectFrom, oldTo = started.PerfectTo;
            Assert.That(oldTo, Is.GreaterThan(oldFrom), "Long Shot has its band");

            // The chest, opened while the key is held: the game stops, the charge with it.
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            Assert.That(_levels.OfferedUpgrades.Select(u => u.Kind), Is.EqualTo(new[] { UpgradeKind.WideWindow }));
            float held = _player.SkillChargedSeconds;
            yield return null;
            yield return null;
            Assert.That(_player.SkillChargedSeconds, Is.EqualTo(held).Within(1e-5f), "the charge stands still while the choice is up");
            Assert.That(_levels.PickUpgrade(0), Is.True, "Wide window taken");
            yield return null;
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel), "the charge held across the choice");
            Assert.That(_player.ActiveSkill, Is.SameAs(started), "the skill in use is the one she started charging");
            SkillDefinition widened = _player.Skills[slot];
            Debug.Log("[077] Long Shot's band: started " + oldFrom + " to " + oldTo + " s, as she has it now " + widened.PerfectFrom + " to " + widened.PerfectTo + " s");
            Assert.That(widened.PerfectFrom, Is.LessThan(oldFrom - 0.02f), "the slot's skill has the widened band");

            // Let go between the widened band's start and the old band's.
            float at = 0.5f * (widened.PerfectFrom + oldFrom);
            yield return Until(() => _player.SkillChargedSeconds >= at, 20f);
            float charged = _player.SkillChargedSeconds;
            Assert.That(charged, Is.InRange(widened.PerfectFrom + 1e-3f, oldFrom - 1e-3f), "inside the widened band, before the old one");
            Assert.That(PoolRules.IsPerfect(widened, charged), Is.True, "the widened band would call it perfect");
            Assert.That(PoolRules.IsPerfect(started, charged), Is.False, "the band it started with does not");
            // Drawn: the cast bar's zone is the band it started with, and it is not lit.
            yield return null;
            charged = _player.SkillChargedSeconds;
            Assume.That(charged, Is.LessThan(oldFrom - 1e-3f), "still before the old band");
            Assert.That(_bar.Up, Is.True, "the cast bar is up");
            Assert.That(_bar.View.Kind, Is.EqualTo(CastBarKind.Charge));
            CastBarView old = CastBarRules.Charge(charged, started.HoldMax, oldFrom, oldTo);
            CastBarView wide = CastBarRules.Charge(charged, widened.HoldMax, widened.PerfectFrom, widened.PerfectTo);
            Assert.That(wide.ZoneFrom, Is.LessThan(old.ZoneFrom - 1e-3f), "the two bands draw apart");
            Assert.That(_bar.View.ZoneFrom, Is.EqualTo(old.ZoneFrom).Within(1e-4f), "the zone drawn is the band it started with");
            Assert.That(_bar.View.ZoneTo, Is.EqualTo(old.ZoneTo).Within(1e-4f));
            Assert.That(_bar.Lit, Is.False, "not lit: a release now is not perfect");
            Assert.That(_player.InPerfectBand, Is.False);

            int perfectBefore = _player.PerfectReleases;
            _fired.Clear();
            _player.ReleaseSkill(slot);
            yield return Until(() => _fired.Count > 0, 10f);
            yield return null;
            Time.captureDeltaTime = 0f;
            Assert.That(_fired.Count, Is.EqualTo(1), "the shot went off");
            Assert.That(_fired[0].Clean, Is.False, "judged by the band it started with: not perfect");
            Assert.That(_player.PerfectReleases, Is.EqualTo(perfectBefore));
            Assert.That(_words, Does.Contain("EARLY"), "worded by that band too; words: " + string.Join(", ", _words));
            Assert.That(_words, Does.Not.Contain("PERFECT"));

            // The next charge is judged by the widened band, as she has it now.
            yield return Until(() => _player.Action == ActionKind.None, 5f);
            Assert.That(_player.Skills[slot].PerfectFrom, Is.EqualTo(widened.PerfectFrom).Within(1e-6f));
        }
    }
}
