using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 040 (Kristian's second session of 2026-11-20), in the ControlRoom scene through the intents. The clean
    /// part comes early: a press 0.2 s after the window opens is clean, one at 0.05 s and one at 0.45 s is not (the late one
    /// says so), and a press that waited from before the window opened is not; the old placement with its switch. The cast
    /// bar is up exactly while a timed thing of hers is, its marker in the clean zone exactly when a press would be clean,
    /// through a hit stop and a pause; it shows Long Shot's charge; it flashes the colour of the press; with its switch off
    /// nothing is drawn; nothing is allocated per frame while it is up. And quicker off the mark: from the last shot to 90 %
    /// of run speed is about a third shorter than with the old numbers, measured both ways in one test.
    ///
    /// Waits are in game time, the window's own clock, or real time with a limit, never frames of a supposed length (frames
    /// without a window are about 0.3 ms long). Where a long frame could carry a press past the moment it means, the
    /// moment actually reached is checked first (an Assume), so a busy machine gives an inconclusive test, not a false one.
    /// </summary>
    public class CleanEarlyAndCastBarTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here
        static readonly Vector3 RunFrom = new Vector3(-6f, 0f, 0f); // open floor toward +X (MovingShotStartTests')

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        Damageable _a;
        CastBar _bar;
        GeneratedLevels _levels;
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<Object> _copies = new List<Object>();
        int _flashes;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _player.Reported += OnReport;
            _bar = CastBar.Ensure();
            _bar.Flashed += OnFlash;
            yield return null;
            _recasts.Clear();
            _flashes = 0;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Reported -= OnReport;
            if (_bar != null)
            {
                _bar.Flashed -= OnFlash;
                Object.Destroy(_bar.gameObject);
            }
            _bar = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            foreach (Object o in _copies)
                if (o != null) Object.Destroy(o);
            _copies.Clear();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _recasts.Clear();
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r);
        }

        void OnFlash(CastBarFlash f) => _flashes++;

        // ---------------- Helpers ----------------

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        void OwnKit(Action<KitValues> change)
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet);
            _copies.Add(copy);
            change(copy.Kit);
            _player.SkillSet = copy;
        }

        void OwnFeel(Action<FeelValues> change)
        {
            FeelSettings copy = Object.Instantiate(_player.Settings);
            _copies.Add(copy);
            change(copy.Values);
            _player.Settings = copy;
        }

        /// <summary>Backstep's hop, then the window for its shot open; ready again first, as at a run's start.</summary>
        IEnumerator BackstepWindowOpen()
        {
            int slot = Slot("Backstep");
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.IsRecastWindowOpen, Is.True, "the window opened when the hop ended");
        }

        /// <summary>
        /// Backstep's shot pressed when the window's clock has reached <paramref name="at"/>. Returns through
        /// <paramref name="done"/> the clock at the press and what the press was (null when it was no recast).
        /// </summary>
        IEnumerator BackstepPressedAt(float at, Action<float, PlayerReport?> done)
        {
            yield return BackstepWindowOpen();
            yield return Until(() => _player.RecastSinceOpened >= at, 2f);
            float since = _player.RecastSinceOpened;
            int before = _recasts.Count;
            _player.PressSkill(Slot("Backstep"), _a.transform.position);
            done(since, _recasts.Count > before ? _recasts.Last() : (PlayerReport?)null);
        }

        // ---------------- 1. The clean part comes early ----------------

        [UnityTest]
        public IEnumerator Backstep_APressAt02sIsClean_At005sAndAt045sIsNot_AndTheLateOneSaysSo()
        {
            Assume.That(_player.Kit.CleanEarly, Is.True, "the new placement as the game ships");
            float since = 0f;
            PlayerReport? press = null;
            yield return BackstepPressedAt(0.2f, (s, r) => { since = s; press = r; });
            Assume.That(since, Is.InRange(_player.Kit.CleanFrom, _player.Kit.CleanTo), "the press came in the clean seconds");
            Assert.That(press.HasValue && press.Value.Clean, Is.True, "0.2 s after the window opened: clean");

            yield return BackstepPressedAt(0.05f, (s, r) => { since = s; press = r; });
            Assume.That(since, Is.LessThan(_player.Kit.CleanFrom), "the press came before the clean part");
            Assert.That(press.HasValue, Is.True, "a shot");
            Assert.That(press.Value.Clean, Is.False, "0.05 s in: not clean");
            Assert.That(press.Value.Late, Is.False, "early, not late");

            yield return BackstepPressedAt(0.45f, (s, r) => { since = s; press = r; });
            Assume.That(since, Is.InRange(_player.Kit.CleanTo + 1e-3f, 0.6f), "the press came after the clean part, in the window");
            Assert.That(press.HasValue, Is.True, "a shot");
            Assert.That(press.Value.Clean, Is.False, "0.45 s in: not clean");
            Assert.That(press.Value.Late, Is.True, "late, and it says so");
        }

        [UnityTest]
        public IEnumerator TripleTap_ALastPressAt02sAfterItsWindowOpened_IsClean()
        {
            int slot = Slot("Triple Tap");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(0.1f).Within(1e-5f), "the clean part from 0.1 s...");
            Assert.That(_player.RecastCleanTo, Is.EqualTo(0.3f).Within(1e-5f), "...to 0.3 s after the window opened");
            yield return Until(() => _player.RecastSinceOpened >= 0.2f, 2f);
            Assume.That(_player.RecastSinceOpened, Is.InRange(0.1f, 0.3f));
            Assert.That(_player.RecastPressWouldBeClean, Is.True);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the last press");
            Assert.That(_recasts.Last().Clean, Is.True, "clean");
        }

        [UnityTest]
        public IEnumerator APressThatWaitedFromBeforeTheWindowOpened_IsNotClean_EvenWhenTheCleanPartOpensWithTheWindow()
        {
            // The clean part from the very instant the window opens, so that only the rule keeps the waiting press out.
            OwnKit(k => { k.CleanFrom = 0f; k.CleanTo = 0.3f; });
            int slot = Slot("Backstep");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.RecastSincePress >= 0.1f, 2f);
            Assume.That(_player.IsRecastWindowOpen, Is.False, "still in the hop");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Buffered), "a press in the hop waits");
            yield return Until(() => _recasts.Count > 0, 2f);
            Assert.That(_recasts.Count, Is.EqualTo(1), "it fired when the window opened");
            Assert.That(_recasts[0].Clean, Is.False, "and is not clean: mashing never counts");

            // The same press made in the window, at once, is.
            yield return BackstepWindowOpen();
            Assert.That(_player.RecastPressWouldBeClean, Is.True, "clean from the window's opening with this kit");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Clean, Is.True, "a press made in the window is");
        }

        [UnityTest]
        public IEnumerator WithTheOldPlacementSwitchedOn_TheLastThirdIsClean_AsBefore()
        {
            OwnKit(k => k.CleanEarly = false);
            float since = 0f;
            PlayerReport? press = null;
            yield return BackstepPressedAt(0.2f, (s, r) => { since = s; press = r; });
            Assume.That(since, Is.LessThan(0.4f));
            Assert.That(press.HasValue && !press.Value.Clean, Is.True, "0.2 s in: early, as before");
            yield return BackstepPressedAt(0.5f, (s, r) => { since = s; press = r; });
            Assume.That(since, Is.InRange(0.4f, 0.6f));
            Assert.That(press.HasValue && press.Value.Clean, Is.True, "0.5 s in: clean, as before");
        }

        // ---------------- 2. The cast bar ----------------

        [UnityTest]
        public IEnumerator TheBar_IsUpExactlyWhileAChainWaitsForAPress_ItsMarkerInTheZoneExactlyWhenAPressWouldBeClean_AndItFlashes()
        {
            int slot = Slot("Triple Tap");
            _player.Teleport(NearA);
            yield return null;
            _bar.Tick();
            Assert.That(_bar.Up, Is.False, "nothing timed: no bar");
            Assert.That(_bar.Showing, Is.False);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the first press");
            _bar.Tick();
            Assert.That(_bar.Up, Is.True, "up from the first press");
            Assert.That(_bar.Showing, Is.True);
            Assert.That(_bar.View.Kind, Is.EqualTo(CastBarKind.Recast));
            Assert.That(_bar.Label, Is.EqualTo("Triple Tap 2 / 3"));
            Assert.That(_bar.View.HasZone, Is.False, "the second press cannot be clean");
            Assert.That(_bar.View.Marker, Is.LessThanOrEqualTo(_bar.View.DimTo + 1e-5f), "in the dim part while the first shot winds up");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            Assert.That(_flashes, Is.EqualTo(1), "it flashed for the press");
            Assert.That(_bar.LastFlash, Is.EqualTo(CastBarFlash.Plain));
            _bar.Tick();
            Assert.That(_bar.Label, Is.EqualTo("Triple Tap 3 / 3"));
            Assert.That(_bar.View.HasZone, Is.True, "the last can be clean: its zone");

            // Every frame of the last window, up to the middle of the clean part: the marker and the judge agree.
            int frames = 0, inZone = 0;
            float middle = (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f;
            while (frames < 100000 && (!_player.IsRecastWindowOpen || _player.RecastSinceOpened < middle))
            {
                _bar.Tick();
                Assert.That(_bar.Up, Is.True, "up while the press waits");
                float since = _player.RecastSinceOpened;
                bool nearEdge = Mathf.Abs(since - _player.RecastCleanFrom) < 2e-3f || Mathf.Abs(since - _player.RecastCleanTo) < 2e-3f;
                if (!nearEdge) Assert.That(_bar.View.MarkerInZone, Is.EqualTo(_player.RecastPressWouldBeClean), "the marker in the zone exactly when a press would be clean, at " + since);
                Assert.That(_bar.Lit, Is.EqualTo(_player.RecastPressWouldBeClean), "lit exactly then");
                if (_bar.View.MarkerInZone) inZone++;
                frames++;
                yield return null;
            }
            _bar.Tick();
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "at the middle of the clean part");
            Assert.That(_bar.View.MarkerInZone, Is.True);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the last press");
            Assert.That(_recasts.Last().Clean, Is.True);
            Assert.That(_bar.LastFlash, Is.EqualTo(CastBarFlash.Clean), "it flashes the clean colour");
            _bar.Tick();
            Assert.That(_bar.Up, Is.False, "nothing waits for a press any more");
            Assert.That(_bar.Showing, Is.True, "but it flashes for a moment");
            Assert.That(_bar.Flash, Is.EqualTo(CastBarFlash.Clean));
            yield return ForRealTime(_bar.Values.FlashSeconds + 0.05f);
            _bar.Tick();
            Assert.That(_bar.Showing, Is.False, "and goes");
        }

        [UnityTest]
        public IEnumerator TheBar_GoesWhenTheWindowRunsOut()
        {
            yield return BackstepWindowOpen();
            _bar.Tick();
            Assert.That(_bar.Up, Is.True);
            Assert.That(_bar.Label, Is.EqualTo("Backstep 2 / 2"));
            yield return Until(() => _player.RecastSinceOpened > _player.RecastWindowSeconds || _player.RecastSlot < 0, 2f);
            _bar.Tick();
            Assert.That(_bar.Up, Is.False, "gone with the window");
            Assert.That(_flashes, Is.EqualTo(0), "no press, no flash");
        }

        [UnityTest]
        public IEnumerator TheBarsMarker_AgreesWithTheJudge_ThroughAHitStop_AndStandsStillInAPause()
        {
            _levels = GeneratedLevels.Ensure();
            yield return null;
            yield return BackstepWindowOpen();
            _bar.Tick();
            Assume.That(_bar.Up && !_bar.Lit, Is.True, "before the clean part");
            HitStop.Trigger(0.3f); // over the start of the clean part, and on into it
            yield return null;
            int frames = 0;
            while (HitStop.IsActive && _player.RecastSinceOpened < _player.RecastCleanFrom + 0.05f && frames < 100000)
            {
                _bar.Tick();
                float since = _player.RecastSinceOpened;
                if (Mathf.Abs(since - _player.RecastCleanFrom) > 2e-3f)
                    Assert.That(_bar.View.MarkerInZone, Is.EqualTo(_player.RecastPressWouldBeClean), "in the hit stop, at " + since);
                Assert.That(_bar.Lit, Is.EqualTo(_player.RecastPressWouldBeClean));
                frames++;
                yield return null;
            }
            Assert.That(HitStop.IsActive, Is.True, "still in the hit stop");
            _bar.Tick();
            Assert.That(_bar.View.MarkerInZone, Is.True, "the marker ran on into the zone through the hit stop, as the window does");
            Assert.That(_player.RecastPressWouldBeClean, Is.True);

            // A pause: the marker stands still, and the bar is not drawn under the pause menu.
            yield return Until(() => !HitStop.IsActive, 2f);
            Assert.That(_levels.Pause(), Is.True, "paused");
            yield return null;
            _bar.Tick();
            float marker = _bar.View.Marker;
            bool clean = _player.RecastPressWouldBeClean;
            yield return ForRealTime(0.3f);
            _bar.Tick();
            Assert.That(_bar.Up, Is.True, "still up under the pause");
            Assert.That(_bar.View.Marker, Is.EqualTo(marker), "and still: a pause holds it");
            Assert.That(_bar.View.MarkerInZone, Is.EqualTo(clean), "where the judge still has it");
            Assert.That(_bar.Showing, Is.False, "not drawn under the pause menu");
            _levels.Resume();
            yield return null;
            _bar.Tick();
            Assert.That(_bar.Showing, Is.True, "back after it");
        }

        [UnityTest]
        public IEnumerator TheBar_ShowsLongShotsCharge_WithItsPerfectBand_AndFlashesClean_ForAPerfectRelease()
        {
            int slot = Slot("Long Shot");
            _player.EquipSkill(slot);
            SkillDefinition shot = _player.Skills[slot];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the charge");
            _bar.Tick();
            Assert.That(_bar.Up, Is.True);
            Assert.That(_bar.View.Kind, Is.EqualTo(CastBarKind.Charge));
            Assert.That(_bar.Label, Is.EqualTo("Long Shot"));
            Assert.That(_bar.View.Seconds, Is.EqualTo(shot.HoldMax).Within(1e-4f), "from the start of the charge to where it fires by itself");
            Assert.That(_bar.View.ZoneFrom, Is.EqualTo(shot.PerfectFrom / shot.HoldMax).Within(1e-4f), "the perfect band, marked");
            Assert.That(_bar.View.ZoneTo, Is.EqualTo(shot.PerfectTo / shot.HoldMax).Within(1e-4f));
            float middle = (shot.PerfectFrom + shot.PerfectTo) * 0.5f;
            while (_player.SkillChargedSeconds < middle && _player.ActiveSkillSlot == slot)
            {
                _bar.Tick();
                float charged = _player.SkillChargedSeconds;
                if (Mathf.Abs(charged - shot.PerfectFrom) > 2e-3f)
                    Assert.That(_bar.View.MarkerInZone, Is.EqualTo(_player.InPerfectBand), "the marker in the band exactly when a release would be perfect, at " + charged);
                yield return null;
            }
            _bar.Tick();
            Assume.That(_player.InPerfectBand, Is.True);
            Assert.That(_bar.Lit, Is.True);
            int fired = _player.SkillShotsFired;
            _player.ReleaseSkill(slot);
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            Assert.That(_bar.LastFlash, Is.EqualTo(CastBarFlash.Clean), "a perfect release flashes the clean colour");
            _bar.Tick();
            Assert.That(_bar.Up, Is.False, "the charge is over");
        }

        [UnityTest]
        public IEnumerator WithItsSwitchOff_NothingIsDrawn()
        {
            _bar.Values.On = false;
            int slot = Slot("Triple Tap");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            _bar.Tick();
            Assert.That(_bar.Up, Is.False, "no bar");
            Assert.That(_bar.Showing, Is.False, "nothing drawn");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            _bar.Tick();
            Assert.That(_flashes, Is.EqualTo(0), "no flash");
            Assert.That(_bar.Showing, Is.False);
        }

        [UnityTest]
        public IEnumerator WhileTheBarIsUp_NothingIsAllocatedPerFrame()
        {
            int slot = Slot("Triple Tap");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            _bar.Tick();
            Assume.That(_bar.Up, Is.True);
            Assert.That(() => { for (int i = 0; i < 300; i++) _bar.Tick(); }, NotAllocating, "the bar's frame allocates nothing");
        }

        // ---------------- 3. Quicker off the mark ----------------

        /// <summary>
        /// Runs toward +X at full speed, one basic shot straight ahead, and on: the game seconds from the shot going off to
        /// her speed being back at 90 % of run speed.
        /// </summary>
        IEnumerator AfterAShotOnTheMove(Action<float> seconds)
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right);
            yield return Until(() => _player.Position.x >= -3.5f, 4f);
            float run = _player.Feel.RunSpeed;
            Assume.That(_player.Speed, Is.EqualTo(run).Within(0.05f), "at full speed");
            int shots = _player.BasicShotsFired;
            _player.Shoot(_player.Position + Vector3.right * 8f);
            Assume.That(_player.IsShootingOnTheMove, Is.True);
            yield return Until(() => _player.BasicShotsFired > shots, 2f);
            float firedAt = Time.time;
            yield return Until(() => _player.Speed >= 0.9f * run, 3f);
            seconds(Time.time - firedAt);
            _player.Stop();
        }

        /// <summary>Backstep's shot pressed at a run, the move key held on: the game seconds from the shot going off to 90 % of run speed.</summary>
        IEnumerator AfterBackstepsShot(Action<float> seconds)
        {
            yield return BackstepWindowOpen();
            _player.Steer(Vector3.right);
            yield return Until(() => _player.Speed >= _player.Feel.RunSpeed - 0.05f, 1f);
            float run = _player.Feel.RunSpeed;
            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Slot("Backstep"), _a.transform.position), Is.EqualTo(IntentResult.Started), "the shot");
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            float firedAt = Time.time;
            yield return Until(() => _player.Speed >= 0.9f * run, 3f);
            seconds(Time.time - firedAt);
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator FromTheLastShot_To90PercentOfRunSpeed_IsAboutAThirdShorter_ThanWithTheOldNumbers()
        {
            Assume.That(_player.Feel.QuickOffTheMark, Is.True, "on as the game ships");
            float quickShot = 0f, quickStep = 0f, oldShot = 0f, oldStep = 0f;
            yield return AfterAShotOnTheMove(s => quickShot = s);
            yield return AfterBackstepsShot(s => quickStep = s);
            OwnFeel(f => f.QuickOffTheMark = false); // the old numbers
            yield return AfterAShotOnTheMove(s => oldShot = s);
            yield return AfterBackstepsShot(s => oldStep = s);
            Debug.Log($"[040] to 90 % of run speed after the shot went off: basic shot {oldShot:0.000} s before, {quickShot:0.000} s now; "
                    + $"Backstep's shot {oldStep:0.000} s before, {quickStep:0.000} s now");
            Assert.That(oldShot, Is.GreaterThan(0.1f), "the old numbers held her back");
            Assert.That(quickShot / oldShot, Is.InRange(0.5f, 0.8f), "the basic shot: about a third shorter");
            Assert.That(oldStep, Is.GreaterThan(0.05f));
            Assert.That(quickStep / oldStep, Is.InRange(0.45f, 0.85f), "Backstep's shot: about a third shorter");
        }
    }
}
