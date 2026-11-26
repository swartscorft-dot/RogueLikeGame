using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 134, the timing at the sight, in the ControlRoom scene through the intents: with each cue (the cylinder, the
    /// ring, the hammer) a press is clean exactly while the cue says so, frame by frame, measured against the character's own clean
    /// part (<see cref="PlayerCharacter.RecastCleanPart"/>, the ChainCleanPart the judge reads) and its judgement
    /// (<see cref="PlayerCharacter.RecastPressWouldBeClean"/>), for Triple Tap's chain and Backstep's press; a press made when the
    /// cue says clean is clean, one made before is not. The cue follows the cursor and changes no aim: her aim and her shot's
    /// direction with a cue on equal them with it off. Nothing is drawn when no window is open or coming. Nothing is allocated per
    /// frame while a cue runs. The click: once as each clean part opens, a step up the chain, and the old tick stands aside for it
    /// only while it is on. The setting saved and read back, and his own saved values put back after every test. The switch off:
    /// nothing drawn or sounded, the old tick as before.
    ///
    /// Waits are in game time or real time with a limit, never frames of a supposed length.
    /// </summary>
    public class TimingAtTheSightPlayTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here (PlayerSignsTests')
        static readonly TimingCue[] Cues = { TimingCue.Cylinder, TimingCue.Ring, TimingCue.Hammer };

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Damageable _a;
        TimingAtTheSight _cue;
        PlayerSigns _signs;
        PlaceholderSounds _sounds;
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<PlaceholderSound> _heard = new List<PlaceholderSound>();
        readonly List<float> _clicks = new List<float>();
        static byte[] s_junk;

        // His own saved values of this order's two keys, put back after every test (the Editor's tests share his PlayerPrefs).
        bool _hadCue, _hadSound;
        int _cueWas, _soundWas;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _hadCue = PlayerPrefs.HasKey(TimingCueSettings.CueKey);
            _cueWas = PlayerPrefs.GetInt(TimingCueSettings.CueKey, 0);
            _hadSound = PlayerPrefs.HasKey(TimingCueSettings.SoundKey);
            _soundWas = PlayerPrefs.GetInt(TimingCueSettings.SoundKey, 0);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            PlaceholderSounds.Requested += OnSound;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(10000000);
            _player.Reported += OnReport;
            _cue = TimingAtTheSight.Ensure();
            _cue.Clicked += OnClick;
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            yield return null;
            _recasts.Clear();
            _heard.Clear();
            _clicks.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= OnSound;
            if (_player != null) _player.Reported -= OnReport;
            if (_cue != null)
            {
                _cue.Clicked -= OnClick;
                Object.Destroy(_cue.gameObject);
            }
            _cue = null;
            if (_signs != null) Object.Destroy(_signs.gameObject);
            _signs = null;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            TimingAtTheSightValues.On = true;
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            // His saved values as they were.
            if (_hadCue) PlayerPrefs.SetInt(TimingCueSettings.CueKey, _cueWas); else PlayerPrefs.DeleteKey(TimingCueSettings.CueKey);
            if (_hadSound) PlayerPrefs.SetInt(TimingCueSettings.SoundKey, _soundWas); else PlayerPrefs.DeleteKey(TimingCueSettings.SoundKey);
            PlayerPrefs.Save();
            if (GameSettings.IsLoaded) GameSettings.Unload();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r);
        }

        void OnSound(PlaceholderSoundRequest r) => _heard.Add(r.Sound);
        void OnClick(float pitch) => _clicks.Add(pitch);

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

        IEnumerator Ready()
        {
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            yield return null;
        }

        /// <summary>What the judge says of a press now, as the character's own clean part and clock give it (its ChainCleanPart).</summary>
        bool JudgeSaysClean => _player.IsRecastWindowOpen && _player.RecastCleanPart.Contains(_player.RecastSinceOpened);

        int _framesCompared, _framesClean;

        /// <summary>This frame's cue and judge, compared.</summary>
        void Compare(string at)
        {
            _cue.Tick();
            bool judge = JudgeSaysClean;
            Assert.That(_cue.SaysClean, Is.EqualTo(judge), at + ": the " + _cue.Cue + " says clean exactly while the clean part holds the clock (since " + _player.RecastSinceOpened + " s)");
            Assert.That(_cue.SaysClean, Is.EqualTo(_player.RecastPressWouldBeClean), at + ": and exactly while a press now would be judged clean");
            Assert.That(_cue.Lit, Is.EqualTo(_player.RecastPressWouldBeClean), at + ": its light the judge's");
            _framesCompared++;
            if (judge) _framesClean++;
        }

        /// <summary>Every frame until the window of press <paramref name="press"/> is open and the cue says clean (or it runs out); compared each frame.</summary>
        IEnumerator FollowUntilClean(int press, string what)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Compare(what + ", press " + press);
                if (_player.RecastSlot < 0) yield break;
                if (_player.RecastShots + 1 == press && _cue.SaysClean) yield break;
            }
        }

        // ---------------- 1. A press is clean exactly while the cue says so ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator TripleTap_WithEachCue_APressIsCleanExactlyWhileTheCueSaysSo_ThroughTheWholeChain()
        {
            int slot = Slot("Triple Tap");
            foreach (TimingCue cue in Cues)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                yield return Ready();
                _recasts.Clear();
                _framesCompared = _framesClean = 0;
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), cue + ": the first press");
                _cue.Tick();
                Assert.That(_cue.ChainUp, Is.True, cue + ": the window coming, the cue up");
                Assert.That(_cue.SaysClean, Is.False, cue + ": not clean before the window opens");
                yield return FollowUntilClean(2, cue.ToString());
                Assert.That(_cue.SaysClean, Is.True, cue + ": the second press's clean part reached");
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), cue + ": the second press, when the cue says clean");
                Assert.That(_recasts.Last().Timing, Is.EqualTo(RecastTiming.Clean), cue + ": it fell clean");
                yield return FollowUntilClean(3, cue.ToString());
                Assert.That(_cue.SaysClean, Is.True, cue + ": the last press's clean part reached");
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), cue + ": the last press, when the cue says clean");
                Assert.That(_recasts.Last().Clean, Is.True, cue + ": a clean tap");
                Assert.That(_framesCompared, Is.GreaterThan(20), cue + ": frames compared");
                // A chain run through, every frame of its windows compared, pressed nowhere: the cue and the judge agree to the end.
                yield return Ready();
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
                float deadline = Time.realtimeSinceStartup + 3f;
                while (_player.RecastSlot == slot && Time.realtimeSinceStartup < deadline)
                {
                    Compare(cue + ", the second window run out");
                    yield return null;
                }
                Assert.That(_player.RecastTimingOf(2), Is.EqualTo(RecastTiming.Missed), cue + ": the window ran out");
                _cue.Tick();
                Assert.That(_cue.SaysClean, Is.False);
                Assert.That(_framesClean, Is.GreaterThan(0), cue + ": clean frames seen");
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Backstep_WithEachCue_APressWhenTheCueSaysCleanIsClean_OneBeforeItIsNot()
        {
            int slot = Slot("Backstep");
            foreach (TimingCue cue in Cues)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                // When it says clean: clean.
                yield return Ready();
                _recasts.Clear();
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), cue + ": the hop");
                _cue.Tick();
                Assert.That(_cue.ChainUp, Is.True, cue + ": up from the hop (the window coming)");
                yield return FollowUntilClean(2, cue + " Backstep");
                Assert.That(_cue.SaysClean, Is.True);
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
                Assert.That(_recasts.Last().Clean, Is.True, cue + ": a press when the cue says clean is clean");
                // In the window, before it says clean: not clean.
                yield return Ready();
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                _cue.Tick();
                Assume.That(_player.RecastSinceOpened, Is.LessThan(_player.RecastCleanFrom), "the frame after the window opened comes before the clean part (0.03 s)");
                Assert.That(_cue.SaysClean, Is.False, cue + ": the window open, the clean part not yet");
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
                Assert.That(_recasts.Last().Clean, Is.False, cue + ": a press before the cue says clean is not");
            }
        }

        // ---------------- 2. It follows the cursor and changes no aim ----------------

        Vector2 _cursorScreen;
        bool _shoot;

        InputFrame? Feed()
        {
            var f = new InputFrame { CursorScreen = _cursorScreen, ShootPressed = _shoot };
            _shoot = false;
            return f;
        }

        /// <summary>A basic shot and a Triple Tap chain at the cursor, as the input driver gives them; her aim at each shot and her facing after.</summary>
        IEnumerator AimRun(List<Vector3> aims)
        {
            // Every frame the same length of game time, so the two runs are frame for frame the same (her turn and the camera's
            // follow ease over frames: with frames of the machine's own length the aim differed by 2 cm from run to run, cue or none).
            Time.captureDeltaTime = 1f / 60f;
            yield return Ready();
            for (int i = 0; i < 60; i++) yield return null; // the camera settled on her
            Camera cam = Camera.main;
            _cursorScreen = cam.WorldToScreenPoint(_a.transform.position + Vector3.up * 1f);
            _driver.InputSource = Feed;
            yield return null;
            yield return null;
            int shots = 0;
            void OnShot(PlayerReport r)
            {
                if (r.Kind == PlayerReportKind.ShotFired || r.Kind == PlayerReportKind.SkillFired) { aims.Add(_player.AimPoint); shots++; }
            }
            _player.Reported += OnShot;
            try
            {
                _shoot = true;
                yield return Until(() => shots >= 1, 2f);
                yield return Until(() => _player.Action == ActionKind.None, 2f);
                int slot = Slot("Triple Tap");
                _player.PressSkill(slot, _player.AimPoint);
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                yield return Until(() => _player.RecastPressWouldBeClean, 2f);
                _player.PressSkill(slot, _player.AimPoint);
                yield return Until(() => shots >= 3, 2f);
                aims.Add(_player.AimCursor);
            }
            finally
            {
                _player.Reported -= OnShot;
                Time.captureDeltaTime = 0f;
            }
            _driver.InputSource = () => null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCue_FollowsTheCursor_AndChangesNoAim_HerShotsWithTheCueOnAreAsWithItOff()
        {
            Camera cam = Camera.main;
            TimingCueSettings.SetCue(TimingCue.Cylinder, keep: false);
            yield return Ready();
            int slot = Slot("Triple Tap");
            _driver.InputSource = Feed;
            foreach (Vector3 world in new[] { _a.transform.position + Vector3.up, _player.Position + new Vector3(-4f, 0f, 3f) })
            {
                _cursorScreen = cam.WorldToScreenPoint(world);
                yield return null;
                yield return null;
                if (_player.RecastSlot < 0) _player.PressSkill(slot, _a.transform.position);
                yield return null;
                _cue.Tick();
                Assert.That(_cue.Drawn, Is.True, "drawn while the chain runs");
                var want = new Vector2(_cursorScreen.x, InterfaceStyle.ScreenHeight - _cursorScreen.y);
                Assert.That(Vector2.Distance(_cue.Sight, want), Is.LessThan(2f), "drawn round the cursor's point: " + _cue.Sight + " for " + want);
                // In this very frame: her aim, her cursor and her facing, before and after the cue has worked out its frame many times.
                Vector3 aim = _player.AimPoint, cursor = _player.AimCursor;
                Quaternion facing = _player.transform.rotation;
                foreach (TimingCue cue in Cues)
                {
                    TimingCueSettings.SetCue(cue, keep: false);
                    for (int i = 0; i < 20; i++) _cue.Tick();
                }
                TimingCueSettings.SetCue(TimingCue.Cylinder, keep: false);
                Assert.That(_player.AimPoint, Is.EqualTo(aim), "her aim untouched by the cue");
                Assert.That(_player.AimCursor, Is.EqualTo(cursor), "her cursor untouched");
                Assert.That(_player.transform.rotation, Is.EqualTo(facing), "her facing untouched");
            }
            _driver.InputSource = () => null;

            // Her aim with each cue on, and with the order's switch off: the same, number for number.
            var off = new List<Vector3>();
            TimingAtTheSightValues.On = false;
            yield return AimRun(off);
            TimingAtTheSightValues.On = true;
            foreach (TimingCue cue in Cues)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                var on = new List<Vector3>();
                yield return AimRun(on);
                Assert.That(on.Count, Is.EqualTo(off.Count), cue + ": as many shots");
                // Each shot's aim (the last entry is her cursor's point after the chain): number for number. The cursor's point after
                // the chain only within a centimetre: the camera's follow eases on real time through the hit stops, so it stood half a
                // millimetre apart between two runs (the hammer's and the switch-off one), the shots themselves exactly the same.
                for (int i = 0; i < on.Count - 1; i++)
                    Assert.That(Vector3.Distance(on[i], off[i]), Is.LessThan(1e-4f), cue + ": her aim at shot " + (i + 1) + " with the cue on is as with it off (" + on[i] + ", " + off[i] + ")");
                Assert.That(Vector3.Distance(on[on.Count - 1], off[off.Count - 1]), Is.LessThan(0.01f), cue + ": her cursor's point after the chain");
            }
        }

        // ---------------- 3. Nothing drawn when no window is open or coming ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator NothingIsDrawn_WhenNoWindowIsOpenOrComing()
        {
            foreach (TimingCue cue in Cues)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                yield return Ready();
                _cue.Tick();
                Assert.That(_cue.Drawn, Is.False, cue + ": nothing while she stands");
                Assert.That(_cue.HammerShown, Is.False, cue + ": no light at her muzzles");
                _player.Shoot(_a.transform.position);
                for (int i = 0; i < 10; i++) { yield return null; _cue.Tick(); Assert.That(_cue.Drawn, Is.False, cue + ": nothing for a basic shot"); }
                yield return Until(() => _player.Action == ActionKind.None, 2f);
                _player.PressSkill(Slot("Quick Shot"), _a.transform.position);
                for (int i = 0; i < 10; i++) { yield return null; _cue.Tick(); Assert.That(_cue.Drawn, Is.False, cue + ": nothing for a skill with no timed press"); }
                yield return Ready();
                _player.PressSkill(Slot("Triple Tap"), _a.transform.position);
                yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.05f, 2f);
                _cue.Tick();
                Assert.That(_cue.Drawn, Is.True, cue + ": drawn while its window is open");
                yield return Until(() => _player.RecastSlot < 0, 3f);
                float end = Time.time + _cue.Values.LingerSeconds + 0.05f;
                yield return Until(() => Time.time > end, 3f);
                _cue.Tick();
                Assert.That(_cue.Drawn, Is.False, cue + ": gone after the chain (its linger over)");
                Assert.That(_cue.HammerShown, Is.False);
            }
            // The bar only: never.
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            yield return Ready();
            _player.PressSkill(Slot("Triple Tap"), _a.transform.position);
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            _cue.Tick();
            Assert.That(_cue.Drawn, Is.False, "the bar only: nothing at the sight");
            Assert.That(_cue.SaysClean, Is.False);
        }

        // ---------------- 4. Nothing allocated per frame ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator WhileACueRuns_NothingIsAllocatedPerFrame()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            TimingCueSettings.SetSound(true, keep: false);
            foreach (TimingCue cue in Cues)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                yield return Ready();
                _player.PressSkill(Slot("Triple Tap"), _a.transform.position);
                _cue.Tick(); // its pieces made (the hammer's, the click's) the first time
                yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.02f, 2f);
                _cue.Tick();
                Assume.That(_cue.ChainUp, Is.True);
                Assert.That(() => { for (int i = 0; i < 300; i++) _cue.Tick(); }, NotAllocating, cue + ": its frame allocates nothing");
                yield return Until(() => _player.RecastPressWouldBeClean, 2f);
                _cue.Tick();
                Assert.That(() => { for (int i = 0; i < 300; i++) _cue.Tick(); }, NotAllocating, cue + ": nor while lit");
            }
        }

        // ---------------- 5. The click ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheClick_OnceAsEachCleanPartOpens_AStepUpTheChain_AndTheOldTickStandsAsideOnlyWhileItIsOn()
        {
            _sounds = PlaceholderSounds.Ensure();
            _signs = PlayerSigns.Ensure();
            int slot = Slot("Triple Tap");
            TimingCueSettings.SetCue(TimingCue.Cylinder, keep: false);
            Assert.That(TimingCueSettings.SoundNow, Is.True, "on by default with a cue");
            Assert.That(TimingAtTheSight.TakesTheTick, Is.True);
            yield return Ready();
            _heard.Clear();
            _clicks.Clear();
            _player.PressSkill(slot, _a.transform.position);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            yield return null;
            _player.PressSkill(slot, _a.transform.position);
            yield return Until(() => _player.RecastShots == 2 && _player.RecastPressWouldBeClean, 2f);
            yield return null;
            _player.PressSkill(slot, _a.transform.position);
            yield return Until(() => _player.RecastSlot < 0, 2f);
            yield return null;
            Assert.That(_clicks.Count, Is.EqualTo(2), "one click as each clean part opened");
            Assert.That(_clicks[0], Is.EqualTo(_cue.Values.ClickPitch).Within(1e-5f), "the second press's at its own pitch");
            Assert.That(_clicks[1], Is.GreaterThan(_clicks[0]), "the third's a step up");
            Assert.That(_heard.Count(s => s == PlaceholderSound.Menu), Is.EqualTo(0), "the old tick stood aside: one sound, not two");
            Assert.That(_heard.Count(s => s == PlaceholderSound.CleanTick), Is.GreaterThan(0), "the clean press's own sound, as before (not doubled by the click)");

            // The bar only: no click, the old tick as today.
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            Assert.That(TimingAtTheSight.TakesTheTick, Is.False);
            yield return Ready();
            _heard.Clear();
            _clicks.Clear();
            _player.PressSkill(slot, _a.transform.position);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            yield return null;
            yield return null;
            Assert.That(_clicks.Count, Is.EqualTo(0), "no click with the bar only");
            Assert.That(_heard.Count(s => s == PlaceholderSound.Menu), Is.EqualTo(1), "the old tick, as today");

            // The click alone, with the bar.
            TimingCueSettings.SetSound(true, keep: false);
            yield return Ready();
            _clicks.Clear();
            _player.PressSkill(slot, _a.transform.position);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            yield return null;
            Assert.That(_clicks.Count, Is.EqualTo(1), "the click alone");
        }

        // ---------------- 6. The hammer at her muzzles ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheHammer_ItsLightAtBothMuzzles_BrightestOnlyInTheCleanPart()
        {
            TimingCueSettings.SetCue(TimingCue.Hammer, keep: false);
            yield return Ready();
            _player.PressSkill(Slot("Triple Tap"), _a.transform.position);
            _cue.Tick();
            Assert.That(_cue.HammerShown, Is.False, "dark before the window opens");
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.01f && !_player.RecastPressWouldBeClean, 2f);
            _cue.Tick();
            Assert.That(_cue.HammerShown, Is.True, "lit once the window opens");
            Assert.That(_cue.HammerLevel, Is.GreaterThan(0f).And.LessThan(HammerCue.Brightest), "a step under the brightest before the clean part");
            for (int hand = 0; hand < 2; hand++)
            {
                Transform muzzle = hand == 0 ? _player.RightMuzzle : _player.LeftMuzzle;
                if (muzzle == null) continue;
                Assert.That(_cue.GlowOf(hand).activeSelf, Is.True);
                Assert.That(Vector3.Distance(_cue.GlowOf(hand).transform.position, muzzle.position), Is.LessThan(0.2f), "at the muzzle");
            }
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            _cue.Tick();
            Assert.That(_cue.HammerLevel, Is.EqualTo(HammerCue.Brightest), "the brightest in the clean part");
            Assert.That(_cue.HammerLight.enabled, Is.True);
        }

        // ---------------- 7. The setting, the key and the switch ----------------

        [UnityTest]
        public IEnumerator TheSetting_SavedAsTheCameraAngleIs_ReadBackAfterALoad_AndHisOwnValuesPutBack()
        {
            GameSettings.Load();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(_hadCue ? TimingCues.Step(_cueWas) : TimingCues.Default), "his stored cue, or the default");
            TimingCueSettings.SetCue(TimingCue.Ring);
            TimingCueSettings.SetSound(false);
            Assert.That(PlayerPrefs.GetInt(TimingCueSettings.CueKey, -9), Is.EqualTo((int)TimingCue.Ring), "kept");
            Assert.That(PlayerPrefs.GetInt(TimingCueSettings.SoundKey, -9), Is.EqualTo(TimingCues.SoundOff));
            GameSettings.Unload();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly), "unloaded: the defaults, as every setting");
            Assert.That(TimingCueSettings.SoundNow, Is.False);
            GameSettings.Load();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.Ring), "read back");
            Assert.That(TimingCueSettings.SoundNow, Is.False, "the click off as kept, though a cue is chosen");
            TimingCueSettings.ForgetSound();
            Assert.That(TimingCueSettings.SoundNow, Is.True, "none stored: on with a cue");
            PlayerPrefs.SetInt(TimingCueSettings.CueKey, 99);
            GameSettings.Unload();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly)); // read between, as the cue does every frame
            GameSettings.Load();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly), "a stored value that is no cue: the default");
            GameSettings.Unload();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheTemporaryKey_StepsTheCues_ShowsTheName_ShiftTurnsTheClick_AndTIsBoundToNothingElse()
        {
            InputBindings b = _driver.ActiveBindings;
            Assert.That(b.MoveUp != Key && b.MoveDown != Key && b.MoveLeft != Key && b.MoveRight != Key && b.Dash != Key && b.Focus != Key
                        && b.Interact != Key && b.TakeCover != Key && b.Sprint != Key && b.Sheet != Key, Is.True, "no binding of hers on T");
            Assert.That(b.Skills.All(s => s.Source != BindingSource.Key || s.Key != Key), Is.True, "no skill on T");
            Assert.That(DeveloperKeys.IsDeveloperKey(Key), Is.False, "no developer's key on T");
            Assert.That(Key, Is.Not.EqualTo(GeneratedLevels.NewLevelKey).And.Not.EqualTo(FloorMap.LargeKey).And.Not.EqualTo(GeneratedLevels.RedrawKey));
            Assert.That(Key, Is.Not.EqualTo(GeneratedLevels.HairTriggerKey).And.Not.EqualTo(GeneratedLevels.SteadyHandKey).And.Not.EqualTo(GeneratedLevels.TodaysFocusKey), "not the run's keys");
            Assert.That(Key, Is.Not.EqualTo(b.Interact), "not the chest's");
            var seen = new List<TimingCue>();
            for (int i = 0; i < 4; i++)
            {
                _cue.PressStepKey(false);
                seen.Add(TimingCueSettings.CueChosen);
                Assert.That(_cue.NameShowing, Is.True, "the name shows");
                Assert.That(_cue.NameText, Does.Contain(TimingCues.Names[(int)TimingCueSettings.CueChosen].ToUpperInvariant()));
            }
            Assert.That(seen, Is.EqualTo(new[] { TimingCue.Cylinder, TimingCue.Ring, TimingCue.Hammer, TimingCue.BarOnly }), "round and back");
            _cue.PressStepKey(false);
            Assert.That(TimingCueSettings.SoundNow, Is.True);
            _cue.PressStepKey(true);
            Assert.That(TimingCueSettings.SoundNow, Is.False, "Shift and T: the click off");
            Assert.That(_cue.NameText, Does.Contain("NO CLICK"));
            float end = Time.realtimeSinceStartup + _cue.Values.NameSeconds + 0.1f;
            yield return Until(() => Time.realtimeSinceStartup > end, 3f);
            Assert.That(_cue.NameShowing, Is.False, "for a second");
        }

        static Key Key => TimingAtTheSight.StepKey;

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSwitchOff_NothingDrawnOrSounded_TheSettingIgnored_TheOldTickAsBefore()
        {
            _sounds = PlaceholderSounds.Ensure();
            _signs = PlayerSigns.Ensure();
            TimingAtTheSightValues.On = false;
            TimingCueSettings.SetCue(TimingCue.Cylinder, keep: false);
            TimingCueSettings.SetSound(true, keep: false);
            Assert.That(TimingCueSettings.CueNow, Is.EqualTo(TimingCue.BarOnly), "the setting ignored");
            Assert.That(TimingAtTheSight.TakesTheTick, Is.False);
            yield return Ready();
            _heard.Clear();
            _clicks.Clear();
            _player.PressSkill(Slot("Triple Tap"), _a.transform.position);
            bool drawn = false;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_player.RecastPressWouldBeClean && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                _cue.Tick();
                drawn |= _cue.Drawn || _cue.HammerShown || _cue.ChainUp;
            }
            yield return null;
            yield return null;
            _cue.Tick();
            Assert.That(drawn || _cue.Drawn, Is.False, "nothing drawn, nothing followed");
            Assert.That(_clicks.Count, Is.EqualTo(0), "no click");
            Assert.That(_heard.Count(s => s == PlaceholderSound.Menu), Is.EqualTo(1), "the clean window's tick as before the order");
            _cue.PressStepKey(false);
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.Cylinder), "the key does nothing");
        }
    }
}
