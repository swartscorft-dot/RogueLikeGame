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
    /// Work order 037 (Kristian's session of 2026-11-20), her two signs, in the ControlRoom scene through the intents. The
    /// clean window: the ring is up while a chain's next press can be clean, its band lit exactly while a press would be
    /// clean (a press at the lit moment is clean, one outside is not), and it is gone after the press, on time-out, on
    /// death and on restart; it stands still in a pause and a hit stop; the key's mark agrees with it; the tick is asked
    /// for once a window and the line once a session. Focus that is full: the flash, the word and the sound once when the
    /// gauge fills, the pulse while it stays full, all gone when it is spent, and none of it under the pause menu or a
    /// choice. With each switch off nothing of that sign exists and the game is as it was; the effects' and the sounds'
    /// switches still rule. Nothing is allocated per frame while a sign is up.
    ///
    /// Waits are in game time or real time with a limit, never frames of a supposed length (frames without a window are
    /// about 0.3 ms long). Before a press the signs are worked out for that very frame (<see cref="PlayerSigns.Tick"/>),
    /// so the band a press is judged against is the one of its own moment.
    /// </summary>
    public class PlayerSignsTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        InputBindings _bindingsBefore;
        InputBindings _ownBindings;
        Damageable _a;
        PlayerSigns _signs;
        CombatEffects _fx;
        PlaceholderSounds _sounds;
        GeneratedLevels _levels;
        readonly List<PlaceholderSound> _heard = new List<PlaceholderSound>();
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<CombatFx> _spawned = new List<CombatFx>();
        int _ticks;
        int _filled;
        static byte[] s_junk;

        /// <summary>The test framework's measure of no GC allocation (its extension, called by name: its own Is would clash with NUnit's).</summary>
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            PlaceholderSounds.Requested += OnSound;
            CombatEffects.Spawned += OnSpawned;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _bindingsBefore = _driver.Bindings;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _player.Reported += OnReport;
            _fx = CombatEffects.Ensure();
            _sounds = PlaceholderSounds.Ensure();
            PlayerSigns.ForgetSessionLine();
            _signs = PlayerSigns.Ensure();
            _signs.TickAsked += OnTick;
            _signs.FocusFilled += OnFilled;
            yield return null;
            yield return null;
            _heard.Clear();
            _recasts.Clear();
            _spawned.Clear();
            _ticks = 0;
            _filled = 0;
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= OnSound;
            CombatEffects.Spawned -= OnSpawned;
            if (_player != null) _player.Reported -= OnReport;
            if (_driver != null) _driver.Bindings = _bindingsBefore;
            if (_ownBindings != null) Object.Destroy(_ownBindings);
            _ownBindings = null;
            if (_signs != null)
            {
                _signs.TickAsked -= OnTick;
                _signs.FocusFilled -= OnFilled;
                Object.Destroy(_signs.gameObject);
            }
            _signs = null;
            if (_fx != null) Object.Destroy(_fx.gameObject);
            _fx = null;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            PlayerSigns.ForgetSessionLine();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _heard.Clear();
            _recasts.Clear();
            _spawned.Clear();
        }

        void OnSound(PlaceholderSoundRequest r) => _heard.Add(r.Sound);
        void OnSpawned(CombatFx fx) => _spawned.Add(fx);
        void OnTick() => _ticks++;
        void OnFilled() => _filled++;

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r);
        }

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

        static IEnumerator ForGameTime(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        PlayerSignValues V => _signs.Values;

        /// <summary>Is the ring's outer edge inside its band now.</summary>
        bool RingInBand => _signs.RingRadius <= V.BandOuter + 1e-4f && _signs.RingRadius >= V.BandInner - 1e-4f;

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>
        /// A chain up to its last press, not made: every press but the last in its window, and then the frame the sign
        /// of the last window first shows. Returns with the window for the last press not yet open, or just open.
        /// </summary>
        IEnumerator UpToTheLastPress(string skillName)
        {
            int slot = Slot(skillName);
            SkillDefinition skill = _player.Skills[slot];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the first press");
            for (int press = 2; press < skill.RecastCount; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                _signs.Tick();
                Assert.That(_signs.ChainSignUp, Is.False, "press " + press + " cannot be clean: no ring for its window");
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "press " + press);
            }
            yield return null;
            Assert.That(_player.RecastShots, Is.EqualTo(skill.RecastCount - 1), "the next press is the last");
            Assert.That(_player.RecastCleanShare, Is.GreaterThan(0f), "and it can be clean");
        }

        // ---------------- 1. The clean window ----------------

        [UnityTest]
        public IEnumerator TripleTap_TheRingIsUp_ItsBandLitExactlyWhileAPressWouldBeClean_AndAPressAtTheLitMomentIsClean()
        {
            int slot = Slot("Triple Tap");
            yield return UpToTheLastPress("Triple Tap");
            Assert.That(_signs.ChainSignUp, Is.True, "the ring is up as soon as the next press can be clean");
            Assert.That(_signs.ChainSlot, Is.EqualTo(slot));
            Assert.That(_signs.Ring, Is.Not.Null, "drawn with the effects' pieces");
            Assert.That(_signs.Band, Is.Not.Null);
            Assert.That(_signs.LitBand, Is.Not.Null);
            if (!_player.IsRecastWindowOpen)
                Assert.That(_signs.RingRadius, Is.EqualTo(CleanWindowSign.StartRadius(_player.RecastCleanShare, V)).Within(1e-4f), "waiting at its start");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);

            // Watch every frame of the window until the band lights: the ring and the band agree with the game's judge.
            int frames = 0;
            float lastRadius = float.PositiveInfinity;
            while (_player.IsRecastWindowOpen && frames < 100000)
            {
                _signs.Tick();
                Assert.That(_signs.ChainSignUp, Is.True);
                Assert.That(_signs.BandLit, Is.EqualTo(_player.RecastPressWouldBeClean), "lit exactly when a press would be clean");
                float since = _player.RecastSinceOpened, edge = _player.RecastWindowSeconds * (1f - _player.RecastCleanShare);
                if (Mathf.Abs(since - edge) > 2e-4f) Assert.That(RingInBand, Is.EqualTo(_signs.BandLit), "the ring in the band exactly then, at " + since);
                Assert.That(_signs.RingRadius, Is.LessThanOrEqualTo(lastRadius + 1e-5f), "it closes");
                lastRadius = _signs.RingRadius;
                // The pieces: the ring scaled to where it is and on her; the band lit or dim.
                Assert.That(_signs.Ring.transform.localScale.x * 2.2f, Is.EqualTo(_signs.RingRadius).Within(1e-3f), "drawn where it is");
                Assert.That(Flat(_signs.Ring.transform.position, _player.Position), Is.LessThan(0.01f), "round her");
                Assert.That(_signs.LitBand.GetComponent<MeshRenderer>().enabled, Is.EqualTo(_signs.BandLit));
                Assert.That(_signs.Band.GetComponent<MeshRenderer>().enabled, Is.EqualTo(!_signs.BandLit));
                // The key: its mark where the recast bar's end is, its band the clean share.
                Assert.That(_signs.KeySignShown(slot), Is.True);
                Assert.That(_signs.KeySignShown(Slot("Backstep")), Is.False, "only its own key");
                Assert.That(_signs.KeyMark, Is.EqualTo(_player.RecastWindowLeft).Within(2e-3f), "the key's mark agrees with the ring");
                Assert.That(_signs.KeyBand, Is.EqualTo(_player.RecastCleanShare).Within(1e-4f));
                if (_signs.BandLit) break;
                frames++;
                yield return null;
            }
            Assert.That(_signs.BandLit, Is.True, "the band lit within the window");
            Assert.That(_ticks, Is.EqualTo(1), "the tick, once, as the band lit");
            Assert.That(PlayerSigns.LinesShown, Is.EqualTo(1), "and the line, the first time this session");
            Assert.That(_signs.LineShowing, Is.True);
            Assert.That(_signs.LineText, Is.EqualTo("Press again inside the band: a clean press"));

            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the last press, at the lit moment");
            Assert.That(_recasts.Last().Clean, Is.True, "a press at the lit moment is clean");
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.False, "gone after the press");
            Assert.That(_signs.KeySignShown(slot), Is.False);
            yield return null;
            Assert.That(_spawned.Where(f => f != null && f.name.StartsWith("PlayerSign_")), Is.Empty, "its pieces destroyed");
            yield return null; // the end of frame where the sound's crowd lets it through
            Assert.That(_heard.Count(s => s == PlaceholderSound.Menu), Is.EqualTo(1), "the tick heard once");
            Assert.That(_ticks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TripleTap_APressBeforeTheBandLights_IsNotClean_AndTheRingGoes()
        {
            int slot = Slot("Triple Tap");
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSinceOpened > 0.1f, 2f);
            _signs.Tick();
            Assume.That(_signs.BandLit, Is.False, "outside the band");
            Assert.That(RingInBand, Is.False, "the ring is outside it too");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Clean, Is.False, "a press outside the lit band is not clean");
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.False, "gone after the press");
            Assert.That(_ticks, Is.EqualTo(0), "no tick: the clean part never opened");
        }

        [UnityTest]
        public IEnumerator Backstep_TheRingFollowsHerHop_AndAPressAtTheLitMomentIsClean()
        {
            int slot = Slot("Backstep");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.True, "up from the hop: its second press can be clean");
            Assert.That(_player.IsRecastWindowOpen, Is.False, "the window opens when the hop ends");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return null;
            Assert.That(Flat(_signs.Ring.transform.position, _player.Position), Is.LessThan(0.01f), "the ring went with her");
            yield return Until(() => _signs.BandLit, 2f);
            _signs.Tick();
            Assume.That(_signs.BandLit, Is.True);
            Assert.That(_player.RecastPressWouldBeClean, Is.True);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Clean, Is.True, "Backstep's second press at the lit moment is clean");
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.False);
            Assert.That(_ticks, Is.EqualTo(1), "one tick for its window");
        }

        [UnityTest]
        public IEnumerator TheRing_IsGone_WhenTheWindowRunsOut()
        {
            yield return UpToTheLastPress("Triple Tap");
            CombatFx ring = _signs.Ring;
            Assert.That(ring, Is.Not.Null);
            yield return Until(() => !_signs.ChainSignUp, 3f);
            Assert.That(_signs.ChainSignUp, Is.False, "gone");
            Assert.That(_player.RecastSlot < 0 || _player.RecastSinceOpened > _player.RecastWindowSeconds, Is.True, "because the window ran out");
            Assert.That(_ticks, Is.EqualTo(1), "its clean part opened once on the way");
            yield return null;
            Assert.That(ring == null, Is.True, "its pieces destroyed");
        }

        [UnityTest]
        public IEnumerator TheRing_IsGone_WhenSheDies()
        {
            yield return UpToTheLastPress("Triple Tap");
            Assert.That(_signs.ChainSignUp, Is.True);
            _health.TakeHit(100000);
            Assume.That(_health.IsAlive, Is.False);
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.False, "gone on her death");
            Assert.That(_signs.Ring == null, Is.True);
        }

        [UnityTest]
        public IEnumerator TheRing_IsGone_OnARestart()
        {
            yield return UpToTheLastPress("Triple Tap");
            Assert.That(_signs.ChainSignUp, Is.True);
            _player.StartFresh(); // what a run's start and end call
            yield return null;
            Assert.That(_signs.ChainSignUp, Is.False, "gone on a restart");
        }

        [UnityTest]
        public IEnumerator TheRing_StandsStill_InAPauseAndAHitStop()
        {
            _levels = GeneratedLevels.Ensure();
            yield return null;
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _player.RecastSinceOpened > 0.05f, 2f);
            Assume.That(_signs.ChainSignUp && !_signs.BandLit, Is.True);
            Assert.That(_levels.Pause(), Is.True, "paused");
            yield return null;
            float r = _signs.RingRadius;
            yield return ForRealTime(0.3f);
            Assert.That(_signs.ChainSignUp, Is.True, "still up under the pause menu");
            Assert.That(_signs.RingRadius, Is.EqualTo(r), "and still: a pause holds it");
            _levels.Resume();
            yield return null;
            HitStop.Trigger(0.25f);
            yield return null;
            r = _signs.RingRadius;
            yield return ForRealTime(0.15f);
            Assume.That(HitStop.IsActive, Is.True);
            Assert.That(_signs.RingRadius, Is.EqualTo(r), "a hit stop holds it too");
            yield return Until(() => !HitStop.IsActive, 2f);
            yield return ForGameTime(0.05f);
            Assert.That(_signs.RingRadius, Is.LessThan(r), "and it closes on after");
        }

        [UnityTest]
        public IEnumerator TheLine_ShowsOnceASession_NotForTheNextWindow()
        {
            yield return UpToTheLastPress("Backstep");
            yield return Until(() => _signs.BandLit, 3f);
            Assert.That(PlayerSigns.LinesShown, Is.EqualTo(1));
            Assert.That(PlayerSigns.SessionLineDone, Is.True);
            yield return Until(() => !_signs.ChainSignUp, 3f);
            // Triple Tap: another chain, another clean window.
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _signs.BandLit, 3f);
            Assert.That(_ticks, Is.EqualTo(2), "a tick for each window");
            Assert.That(PlayerSigns.LinesShown, Is.EqualTo(1), "but the line never twice");
        }

        [UnityTest]
        public IEnumerator WithTheCleanWindowSwitchOff_NothingOfItExists_AndTheCleanPressIsAsItWas()
        {
            V.CleanWindow = false;
            int slot = Slot("Triple Tap");
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastWindowLeft <= _player.Kit.CleanShare * 0.5f, 2f);
            _signs.Tick();
            Assert.That(_signs.ChainSignUp, Is.False, "no ring");
            Assert.That(_signs.Ring, Is.Null);
            Assert.That(_signs.KeySignShown(slot), Is.False, "no mark on the key");
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Clean, Is.True, "the clean press as it always was");
            yield return null;
            yield return null;
            Assert.That(_ticks, Is.EqualTo(0), "no tick");
            Assert.That(_heard.Contains(PlaceholderSound.Menu), Is.False);
            Assert.That(PlayerSigns.LinesShown, Is.EqualTo(0), "no line");
            Assert.That(_spawned.Where(f => f != null && f.name.StartsWith("PlayerSign_")), Is.Empty, "no piece");
        }

        [UnityTest]
        public IEnumerator WithTheEffectsOff_NothingIsDrawnOnTheFloor_AndWithTheSoundsOff_NothingIsHeard()
        {
            _fx.Values.On = false;
            _sounds.Values.On = false;
            int slot = Slot("Triple Tap");
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _signs.BandLit, 2f);
            Assert.That(_signs.ChainSignUp, Is.True, "the sign is on");
            Assert.That(_signs.Ring, Is.Null, "but the effects draw nothing");
            Assert.That(_signs.Band, Is.Null);
            Assert.That(_signs.KeySignShown(slot), Is.True, "the key on the skill bar still shows it");
            Assert.That(_ticks, Is.EqualTo(1), "the tick is asked for...");
            yield return null;
            yield return null;
            Assert.That(_heard.Contains(PlaceholderSound.Menu), Is.False, "...and not heard");
        }

        // ---------------- 2. Focus that is full ----------------

        [UnityTest]
        public IEnumerator WhenFocusFills_TheFlashWordAndSoundComeOnce_ThePulseRunsWhileFull_AndAllStopsWhenSpent()
        {
            _ownBindings = ScriptableObject.CreateInstance<InputBindings>();
            _ownBindings.Focus = Key.X; // whatever it is bound to, not a Z written in
            _driver.Bindings = _ownBindings;
            yield return null;
            Assert.That(_player.IsFocusFull, Is.False);
            _player.FillFocus();
            yield return null;
            Assert.That(_filled, Is.EqualTo(1), "the flash and the word, once");
            Assert.That(_signs.Flashes, Is.EqualTo(1));
            Assert.That(_signs.WordText, Is.EqualTo("FOCUS  X"), "FOCUS and the key as the bindings have it");
            Assert.That(_signs.WordShowing, Is.True);
            Assert.That(PlayerSigns.FocusFullText, Is.EqualTo("FOCUS FULL: press X"), "the gauge's words with the bound key");
            List<CombatFx> flash = _spawned.Where(f => f != null && f.name.StartsWith("PlayerSign_Focus") && !f.name.StartsWith("PlayerSign_FocusPulse")).ToList();
            Assert.That(flash.Count, Is.EqualTo(2), "a ring and a star on her");
            Assert.That(flash.All(f => f.Piece.Rim.b < 0.5f && f.Piece.Rim.r > 0.9f), Is.True, "in the full gauge's gold, not an enemy's red or blue");
            yield return null;
            yield return null;
            Assert.That(_heard.Count(s => s == PlaceholderSound.FocusFull), Is.EqualTo(1), "its sound, once");

            // The pulse: about every 1.5 s while it stays full.
            var starts = new List<float>();
            bool shownBefore = _signs.PulseShown;
            int spawnedBefore = _spawned.Count;
            float end = Time.time + 3.7f;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline)
            {
                if (_signs.PulseShown && !shownBefore) starts.Add(Time.time);
                shownBefore = _signs.PulseShown;
                Assert.That(_signs.FocusSignUp, Is.True);
                Assert.That(_signs.GaugeSignShown, Is.True, "the gauge pulses with its key");
                Assert.That(_signs.GaugeAlpha, Is.InRange(V.GaugeLow - 1e-3f, V.GaugeHigh + 1e-3f));
                if (_signs.PulseShown) Assert.That(_signs.PulseRadius, Is.InRange(V.PulseFrom - 1e-3f, V.PulseTo + 1e-3f));
                yield return null;
            }
            Assert.That(_filled, Is.EqualTo(1), "the flash came once only");
            Assert.That(_heard.Count(s => s == PlaceholderSound.FocusFull), Is.EqualTo(1), "and its sound");
            Assert.That(starts.Count, Is.InRange(2, 3), "pulses: " + string.Join(", ", starts));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.EqualTo(V.PulseSeconds).Within(0.05f), "once every 1.5 s");
            Assert.That(_signs.FocusPieces, Is.EqualTo(3), "the pulse's pieces, made once (the flash has gone)");
            Assert.That(_spawned.Count - spawnedBefore, Is.EqualTo(0), "nothing made again pulse after pulse");

            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "spent");
            yield return null;
            Assert.That(_signs.FocusSignUp, Is.False, "all of it stops the moment she spends it");
            Assert.That(_signs.PulseShown, Is.False);
            Assert.That(_signs.WordShowing, Is.False);
            Assert.That(_signs.GaugeSignShown, Is.False);
            yield return null;
            Assert.That(_signs.FocusPieces, Is.EqualTo(0), "its pieces gone");
        }

        [UnityTest]
        public IEnumerator AFullGauge_ShowsNothing_UnderThePauseMenuOrAChoice()
        {
            _levels = GeneratedLevels.Ensure();
            yield return null;
            _player.FillFocus();
            yield return null;
            Assume.That(_signs.FocusSignUp && _signs.WordShowing, Is.True);
            Assert.That(_levels.Pause(), Is.True);
            yield return null;
            AssertNothingOfFocusShows("the pause menu");
            _levels.Resume();
            yield return ForGameTime(V.PulseDelay + 0.1f);
            Assert.That(_signs.PulseShown, Is.True, "back after the pause");
            Assert.That(_signs.GaugeSignShown, Is.True);
            PlayerInputDriver.HeldByChoice = true; // what a choice on screen holds
            yield return null;
            AssertNothingOfFocusShows("a choice");
            PlayerInputDriver.HeldByChoice = false;
            yield return null;
            Assert.That(_signs.GaugeSignShown, Is.True, "and back after it");
            Assert.That(_filled, Is.EqualTo(1), "and no second flash for it");
        }

        void AssertNothingOfFocusShows(string under)
        {
            Assert.That(_signs.PulseShown, Is.False, "no pulse under " + under);
            Assert.That(_signs.WordShowing, Is.False, "no word under " + under);
            Assert.That(_signs.GaugeSignShown, Is.False, "no pulsing gauge under " + under);
            foreach (CombatFx f in _spawned.Where(f => f != null && f.name.StartsWith("PlayerSign_Focus")))
            {
                bool drawn = f.gameObject.activeInHierarchy && f.GetComponent<MeshRenderer>().enabled;
                Assert.That(drawn, Is.False, f.name + " not drawn under " + under);
            }
        }

        [UnityTest]
        public IEnumerator WithTheFocusSwitchOff_NothingOfItExists_AndTheGaugeIsAsItWas()
        {
            V.FocusFull = false;
            _player.FillFocus();
            yield return ForGameTime(2f);
            Assert.That(_signs.FocusSignUp, Is.False);
            Assert.That(_signs.Flashes, Is.EqualTo(0), "no flash");
            Assert.That(_filled, Is.EqualTo(0));
            Assert.That(_signs.WordShowing, Is.False, "no word");
            Assert.That(_signs.PulseShown, Is.False, "no pulse");
            Assert.That(_signs.GaugeSignShown, Is.False, "no pulsing gauge");
            Assert.That(_signs.FocusPieces, Is.EqualTo(0));
            Assert.That(_spawned.Where(f => f != null && f.name.StartsWith("PlayerSign_")), Is.Empty, "no piece");
            Assert.That(PlayerSigns.FocusFullText, Is.EqualTo("FOCUS FULL: press Z"), "the gauge's words as they were");
            Assert.That(_heard.Count(s => s == PlaceholderSound.FocusFull), Is.EqualTo(1), "the sound it always had, as before");
        }

        // ---------------- Nothing per frame ----------------

        [UnityTest]
        public IEnumerator WhileASignIsUp_NothingIsAllocatedPerFrame_AndNoPieceIsRebuilt()
        {
            // The measure is the test framework's own (it counts the GC.Alloc samples on this thread); first, that it sees one.
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");

            // The ring, in the window before its band lights.
            yield return UpToTheLastPress("Triple Tap");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assume.That(_signs.ChainSignUp && !_signs.BandLit, Is.True);
            _signs.Tick();
            int ringBuilds = _signs.Ring.Builds, bandBuilds = _signs.Band.Builds, spawned = _spawned.Count;
            Assert.That(() => { for (int i = 0; i < 300; i++) _signs.Tick(); }, NotAllocating, "the ring's frame allocates nothing");
            yield return null;
            yield return null;
            Assert.That(_signs.Ring.Builds, Is.EqualTo(ringBuilds), "the ring is moved, not rebuilt");
            Assert.That(_signs.Band.Builds, Is.EqualTo(bandBuilds));
            Assert.That(_spawned.Count, Is.EqualTo(spawned), "nothing new made while it is up");
            yield return Until(() => !_signs.ChainSignUp, 3f);

            // A full gauge, while it pulses.
            _player.FillFocus();
            yield return ForGameTime(V.PulseDelay + 0.2f);
            Assume.That(_signs.PulseShown, Is.True);
            _signs.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) _signs.Tick(); }, NotAllocating, "the pulse's frame allocates nothing");
            spawned = _spawned.Count;
            yield return ForGameTime(V.PulseSeconds * 1.5f);
            Assert.That(_spawned.Count, Is.EqualTo(spawned), "pulse after pulse with the same pieces");
        }
    }
}
