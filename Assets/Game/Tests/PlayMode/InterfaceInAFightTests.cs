using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Work order 063 (his words of 2026-11-21), in the ControlRoom scene through the intents: a skill put on cooldown and
    /// waited out kicks and flashes its box once and no other, none when a run starts or ends or the floor changes, none
    /// while paused; the strokes stand under her feet by the gap in metres as this frame's camera shows her; the strip on
    /// screen and clear of the strokes, the run box and the map; the cooldown's number and every label measure inside their
    /// boxes. Item 2b: an open recast window lights its box with the clean part's band at its shares, lit exactly when the
    /// judge would call a press clean (Backstep and Triple Tap, pressed before, in and after it), nothing while no window is
    /// open, nothing allocated while one is; the blade follows her through Backstep's hop with the window's progress.
    ///
    /// Nothing is seen: what the bar draws is worked out every frame without a window, and that is what these read. Waits are
    /// real time with a limit, or the window's own clock, never frames of a supposed length.
    /// </summary>
    public class InterfaceInAFightTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here (CleanEarlyAndCastBarTests')
        const ulong RunSeed = 0x0E117E17UL;

        PlayerCharacter _player;
        PlayerHealth _health;
        SkillBar _bar;
        CastBar _cast;
        GeneratedLevels _levels;
        Damageable _a;
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<GameObject> _made = new List<GameObject>();

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
            _health.Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _bar = Object.FindAnyObjectByType<SkillBar>();
            Assert.That(_bar, Is.Not.Null, "the skill bar in the scene");
            _cast = CastBar.Ensure();
            _made.Add(_cast.gameObject);
            _player.Reported += OnReport;
            yield return null;
            _recasts.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Reported -= OnReport;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
                _made.Add(_levels.gameObject);
            }
            foreach (GameObject o in _made)
                if (o != null) Object.Destroy(o);
            _made.Clear();
            _levels = null;
            InterfaceStyle.Shared.Kinetic = true;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

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

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator UntilFree() => Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);

        /// <summary>Her feet on the screen, in the skin's layout pixels.</summary>
        Vector2 Feet()
        {
            Vector3 feet = Camera.main.WorldToScreenPoint(_player.Position);
            float s = InterfaceStyle.Scale;
            return new Vector2(feet.x / s, (Screen.height - feet.y) / s);
        }

        /// <summary>The strokes' gap this frame's camera gives: 0.35 m down its picture at her feet, in layout pixels.</summary>
        float Under()
        {
            Camera cam = Camera.main;
            Vector3 down = cam.WorldToScreenPoint(_player.Position - cam.transform.up * InterfaceStyle.V.UnderFeetMetres);
            return (Screen.height - down.y) / InterfaceStyle.Scale - Feet().y;
        }

        /// <summary>Quick Shot pressed toward the dummy (on its cooldown after).</summary>
        IEnumerator QuickShotOnCooldown(int quick)
        {
            yield return UntilFree();
            yield return Until(() => _player.SkillCooldownRemaining(quick) <= 0f, 6f);
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started), "Quick Shot");
            yield return Until(() => _player.SkillCooldownRemaining(quick) > 0f, 2f);
            Assert.That(_player.SkillCooldownRemaining(quick), Is.GreaterThan(0f), "on its cooldown");
        }

        /// <summary>Backstep's hop, then the window for its shot open; ready again first, as at a run's start.</summary>
        IEnumerator BackstepWindowOpen()
        {
            int slot = Slot("Backstep");
            yield return UntilFree();
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.IsRecastWindowOpen, Is.True, "the window opened when the hop ended");
        }

        /// <summary>Triple Tap's first two presses, then the window for its last (clean-able) press open.</summary>
        IEnumerator TripleTapLastWindowOpen()
        {
            int slot = Slot("Triple Tap");
            yield return UntilFree();
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the first press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            _bar.Tick();
            KineticSlot second = _bar.SlotOf(_player.KeyOfSkill(slot));
            Assert.That(second.Recast, Is.True, "the second press's window lights the box");
            Assert.That(second.CleanShare, Is.Zero, "with no band: the second press cannot be clean");
            Assert.That(second.CleanLit, Is.False);
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastCleanTo, Is.GreaterThan(_player.RecastCleanFrom), "the last press can be clean");
        }

        /// <summary>
        /// The press of the open window made when its clock reaches <paramref name="share"/> of the way from 0 (the moment it
        /// opened) through the clean part to the window's end: what the box showed in that frame and what the judge said.
        /// </summary>
        IEnumerator PressAt(string skill, Func<float, float, float, float> moment, Action<float, bool, PlayerReport?> done)
        {
            int slot = Slot(skill), key = _player.KeyOfSkill(slot);
            float from = _player.RecastCleanFrom, to = _player.RecastCleanTo, window = _player.RecastWindowSeconds;
            float at = moment(from, to, window);
            yield return Until(() => _player.RecastSinceOpened >= at || !_player.IsRecastWindowOpen, 2f);
            _bar.Tick();
            float since = _player.RecastSinceOpened;
            bool lit = _bar.SlotOf(key).CleanLit;
            int before = _recasts.Count;
            _player.PressSkill(slot, _a.transform.position);
            done(since, lit, _recasts.Count > before ? _recasts.Last() : (PlayerReport?)null);
            _bar.Tick();
            Assert.That(_bar.SlotOf(key).Recast, Is.False, "the press made: the box returns at once");
            Assert.That(_bar.SlotOf(key).Box.size, Is.EqualTo(_bar.SlotOf(key).Home.size), "to its size");
        }

        static float Before(float from, float to, float window) => from * 0.5f;
        static float In(float from, float to, float window) => (from + to) * 0.5f;
        static float After(float from, float to, float window) => (to + window) * 0.5f;

        // ---------------- 2. Off cooldown: a kick and a flash, once ----------------

        [UnityTest]
        public IEnumerator ASkillPutOnCooldownAndWaitedOut_KicksAndFlashesItsBoxOnce_AndNoOther()
        {
            int quick = Slot("Quick Shot"), key = _player.KeyOfSkill(quick);
            Assume.That(key, Is.GreaterThanOrEqualTo(0), "Quick Shot on a key");
            _player.Teleport(NearA);
            yield return null;
            _bar.Tick();
            int total = _bar.ReadyFlashes, on = _bar.ReadyFlashesOn(key);
            yield return QuickShotOnCooldown(quick);
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "no flash while it cools");
            yield return Until(() => _player.SkillCooldownRemaining(quick) <= 0f, 6f);
            _bar.Tick(); // the frame it came ready
            Assert.That(_bar.ReadyFlashesOn(key), Is.EqualTo(on + 1), "one flash on its box");
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total + 1), "and on no other");
            KineticSlot s = _bar.SlotOf(key);
            Assert.That(s.Flash, Is.GreaterThan(0.8f), "bright at once");
            for (int k = 0; k < _bar.SlotCount; k++)
                if (k != key) Assert.That(_bar.SlotOf(k).Flash, Is.Zero, "key " + k + " does not flash");
            yield return ForRealTime(InterfaceStyle.V.ReadyFlashSeconds * InterfaceStyle.V.ReadyKickRise);
            _bar.Tick();
            s = _bar.SlotOf(key);
            Assume.That(s.Flash, Is.GreaterThan(0f), "still flashing (a busy machine may have passed it)");
            Assert.That(s.Kick, Is.GreaterThan(0f), "kicked");
            Assert.That(s.Box.y, Is.LessThan(s.Home.y), "up along the slant");
            Assert.That(s.Box.x, Is.GreaterThan(s.Home.x), "and right by its lean");
            yield return ForRealTime(InterfaceStyle.V.ReadyFlashSeconds + 0.05f);
            _bar.Tick();
            s = _bar.SlotOf(key);
            Assert.That(s.Flash, Is.Zero, "gone after a quarter second");
            Assert.That(s.Box, Is.EqualTo(s.Home), "settled");
            Assert.That(_bar.ReadyFlashesOn(key), Is.EqualTo(on + 1), "one per cooldown");
        }

        [UnityTest]
        public IEnumerator NoFlash_WhenARunStarts_OrEnds_AndAFloorChangeNeitherResetsNorShowsOne()
        {
            _levels = GeneratedLevels.Ensure();
            yield return null;
            int quick = Slot("Quick Shot");
            _player.Teleport(NearA);
            yield return null;
            yield return QuickShotOnCooldown(quick);
            int total = _bar.ReadyFlashes;
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            _bar.Tick();
            Assert.That(_player.SkillCooldownRemaining(quick), Is.Zero, "a run's start makes it ready");
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "without a flash");

            // In the run, on its cooldown, back to the practice room: ready again, no flash.
            yield return UntilFree();
            Assert.That(_player.PressSkill(quick, _player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillCooldownRemaining(quick) > 0f, 2f);
            total = _bar.ReadyFlashes;
            _levels.BackToPracticeRoom();
            yield return null;
            yield return null;
            _bar.Tick();
            Assert.That(_player.SkillCooldownRemaining(quick), Is.Zero, "the run's end makes it ready");
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "without a flash");

            // A floor's change: the cooldown carries on (nothing resets it), no flash at the change, one when it runs out.
            _levels.StartRun(RunSeed);
            yield return null;
            yield return UntilFree();
            Assert.That(_player.PressSkill(quick, _player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillCooldownRemaining(quick) > 0f, 2f);
            total = _bar.ReadyFlashes;
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 3f);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 (work order 025)
            yield return null;
            yield return null;
            _bar.Tick();
            Assume.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "no flash at the floor's change");
            int key = _player.KeyOfSkill(quick);
            if (_player.SkillCooldownRemaining(quick) > 0f)
            {
                int on = _bar.ReadyFlashesOn(key);
                yield return Until(() => _player.SkillCooldownRemaining(quick) <= 0f, 6f);
                _bar.Tick();
                Assert.That(_bar.ReadyFlashesOn(key), Is.EqualTo(on + 1), "one when it runs out on the new floor");
            }
        }

        [UnityTest]
        public IEnumerator NoFlash_WhilePaused_AndAResetInAPauseShowsNoneAfter()
        {
            _levels = GeneratedLevels.Ensure();
            yield return null;
            int quick = Slot("Quick Shot"), key = _player.KeyOfSkill(quick);
            _player.Teleport(NearA);
            yield return null;
            yield return QuickShotOnCooldown(quick);
            int on = _bar.ReadyFlashesOn(key);
            yield return Until(() => _bar.ReadyFlashesOn(key) > on, 6f);
            Assert.That(_bar.ReadyFlashesOn(key), Is.EqualTo(on + 1), "it flashed");
            Assert.That(_levels.Pause(), Is.True, "paused while it flashes");
            yield return null;
            _bar.Tick();
            Assert.That(_bar.SlotOf(key).Flash, Is.Zero, "nothing flashes while paused");
            Assert.That(_bar.SlotOf(key).Box, Is.EqualTo(_bar.SlotOf(key).Home));
            _levels.Resume();
            yield return null;

            // On its cooldown again, then made ready by a reset under the pause: nothing then, nothing after.
            yield return QuickShotOnCooldown(quick);
            int total = _bar.ReadyFlashes;
            Assert.That(_levels.Pause(), Is.True);
            yield return null;
            _player.StartFresh();
            yield return null;
            _bar.Tick();
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "none in the pause");
            _levels.Resume();
            yield return null;
            yield return null;
            _bar.Tick();
            Assert.That(_bar.ReadyFlashes, Is.EqualTo(total), "and none after it");
        }

        // ---------------- 3. The strokes under her feet ----------------

        [UnityTest]
        public IEnumerator TheStrokes_AreUnderHerFeetByTheGap_AsThisFramesCameraShowsHer_AfterAMove()
        {
            Vector2 feet = default;
            float under = 0f, lowest = 0f;
            Rect h = default, f = default;
            float gap = 0f;
            bool shown = false;
            int looked = 0;
            Renderer[] hers = _player.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer) && r.GetComponent<CombatFx>() == null).ToArray();
            var end = new GameObject("LooksAtTheFramesEnd").AddComponent<LooksAtTheFramesEnd>();
            _made.Add(end.gameObject);
            end.Look = () =>
            {
                looked++;
                feet = Feet();
                under = Under();
                shown = _bar.StrokesShown;
                h = _bar.HealthStroke;
                f = _bar.FocusStroke;
                gap = _bar.StrokesUnder;
                // For the report: how far down the screen the lowest corner of her drawn bounds reaches under her feet.
                lowest = float.NegativeInfinity;
                foreach (Renderer r in hers)
                {
                    Bounds b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 p = Camera.main.WorldToScreenPoint(corner);
                        lowest = Mathf.Max(lowest, (Screen.height - p.y) / InterfaceStyle.Scale - feet.y);
                    }
                }
                end.Look = null;
            };
            _player.Teleport(new Vector3(-6f, 0f, -3f));
            yield return null;
            Assert.That(looked, Is.EqualTo(1));
            Assert.That(shown, Is.True);
            InterfaceValues v = InterfaceStyle.V;
            Debug.Log($"[063] the strokes' gap under her feet: {gap:0.0} layout px (this morning {v.UnderFeet}); the lowest corner of her drawn bounds {lowest:0.0} px under her feet's point");
            Assert.That(gap, Is.EqualTo(under).Within(0.05f), "the gap of 0.35 m, made pixels this frame");
            Assert.That(h.y - feet.y, Is.EqualTo(under).Within(0.5f), "the strokes' top that far under her feet as this frame's camera shows them");
            Assert.That((h.xMin + f.xMax) * 0.5f, Is.EqualTo(feet.x).Within(0.5f), "centred on her");
            Assert.That(under, Is.EqualTo(25.2f).Within(1.5f), "about 25 layout pixels with the game's camera: lower than this morning's 16");
            Assert.That(h.height, Is.EqualTo(v.StrokeHeight * InterfaceRules.Fight(v)).Within(1e-3f), "grown with the master scale");
        }

        [UnityTest]
        public IEnumerator AtThisResolution_TheStripIsOnScreen_ClearOfTheStrokesTheRunBoxAndTheMap()
        {
            _levels = GeneratedLevels.Ensure();
            FloorMap map = FloorMap.Ensure();
            _made.Add(map.gameObject);
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            map.Tick();
            _bar.Tick();
            Assume.That(map.Shown, Is.True, "the map in a floor");
            InterfaceValues v = InterfaceStyle.V;
            float width = Screen.width / InterfaceStyle.Scale;
            UiBox whole = InterfaceRules.WholeStrip(_bar.SlotCount, width, v);
            var strip = new Rect(whole.X, whole.Y, whole.W, whole.H);
            Assert.That(strip.xMin, Is.GreaterThan(0f));
            Assert.That(strip.xMax, Is.LessThan(width));
            Assert.That(strip.yMax, Is.EqualTo(v.LayoutHeight * (1f - v.StripLift)).Within(0.5f), "lifted 9 % over the bottom");
            float k = v.LayoutHeight / SkillBar.LayoutHeight; // the HUD's and the map's 720 layout in the skin's 1080 one
            Rect m = map.SmallRect, run = RunHud.RunBoxKinetic(Screen.width / (Screen.height / SkillBar.LayoutHeight), 6);
            var map1080 = new Rect(m.x * k, m.y * k, m.width * k, m.height * k);
            var run1080 = new Rect(run.x * k, run.y * k, run.width * k, run.height * k);
            Assert.That(strip.Overlaps(map1080), Is.False, "clear of the map");
            Assert.That(strip.Overlaps(run1080), Is.False, "clear of the run box");
            Assert.That(run1080.Overlaps(map1080), Is.False, "and the run box clear of the map, as before");
            // She stands at the middle (the camera looks at her feet): the strokes and their number over the strip.
            Assert.That(_bar.StrokesShown, Is.True);
            Assert.That(Mathf.Max(_bar.HealthStroke.yMax, _bar.HealthNumberBox.yMax), Is.LessThan(strip.yMin), "the strokes over the strip");
            for (int key = 0; key < _bar.SlotCount; key++)
            {
                Rect box = _bar.SlotOf(key).Box;
                Assert.That(box.yMax, Is.LessThan(v.LayoutHeight), "box " + key + " on screen");
                Assert.That(box.xMin, Is.GreaterThan(0f));
                Assert.That(box.xMax, Is.LessThan(width));
            }
        }

        [UnityTest]
        public IEnumerator TheCooldownsNumber_AndEveryLabel_MeasureInsideTheirBoxes()
        {
            yield return null;
            _bar.Tick();
            InterfaceValues v = InterfaceStyle.V;
            Assume.That(InterfaceStyle.Heavy, Is.Not.Null, "the font");
            Rect face = _bar.SlotOf(0).Face, plate = new Rect(face.x, _bar.SlotOf(0).Box.yMax - InterfaceRules.KeyPlateHeight(v), face.width, InterfaceRules.KeyPlateHeight(v));
            GUIStyle number = InterfaceStyle.Text(true, InterfaceRules.CooldownFont(v), TextAnchor.MiddleCenter);
            foreach (string n in new[] { "12", "99", "9.9", "3.2", "0.1" })
            {
                Vector2 size = InterfaceStyle.Measure(n, number);
                Assert.That(size.x, Is.LessThan(face.width), n + " within the face's width");
                Assert.That(size.y, Is.LessThan(face.height), n + " within its height");
            }
            Vector2 digit = InterfaceStyle.Measure("8", number);
            Debug.Log($"[063] the cooldown's number: font {number.fontSize}, a line {digit.y:0.0} layout px high, \"12\" {InterfaceStyle.Measure("12", number).x:0.0} and \"9.9\" {InterfaceStyle.Measure("9.9", number).x:0.0} wide in a face {face.width:0.0} by {face.height:0.0}");
            GUIStyle key = InterfaceStyle.Text(true, InterfaceRules.KeyFont(v), TextAnchor.MiddleCenter);
            InputBindings bindings = _player.GetComponent<PlayerInputDriver>().ActiveBindings;
            for (int k = 0; k < _bar.SlotCount; k++)
            {
                string label = bindings.SkillLabel(k);
                Vector2 size = InterfaceStyle.Measure(label, key);
                Assert.That(size.x, Is.LessThan(plate.width), label + " within its plate");
                Assert.That(size.y, Is.LessThanOrEqualTo(plate.height + 0.5f), label + " within its plate's height");
                Vector2 big = InterfaceStyle.Measure(label, InterfaceStyle.Text(true, InterfaceRules.Size(InterfaceRules.KeyFont(v), v.RecastKeyGrow), TextAnchor.MiddleCenter));
                Assert.That(big.x, Is.LessThan(face.width), label + " large on a lit face");
                Assert.That(big.y, Is.LessThan(face.height));
            }
            GUIStyle shortName = InterfaceStyle.Text(true, InterfaceRules.Size(v.ShortNameSize, InterfaceRules.Strip(v)), TextAnchor.MiddleCenter);
            foreach (SkillDefinition s in _player.Skills)
                Assert.That(InterfaceStyle.Measure(InterfaceRules.ShortName(s.Name), shortName).x, Is.LessThan(face.width), s.Name + "'s short name within a face");
            Rect left = _bar.LeftEnd, right = _bar.RightEnd;
            Assert.That(InterfaceStyle.Measure(new InputBinding(bindings.Dash).Label, InterfaceStyle.Text(true, InterfaceRules.KeyFont(v) - 2, TextAnchor.MiddleCenter)).x, Is.LessThan(left.width), "the dash's key");
            Assert.That(InterfaceStyle.Measure("FOCUS", shortName).x, Is.LessThan(right.width), "FOCUS, without its icon");
            GUIStyle health = InterfaceStyle.Text(true, InterfaceRules.Size(v.HealthNumberSize, InterfaceRules.Fight(v)), TextAnchor.MiddleLeft);
            Assert.That(InterfaceStyle.Measure("100", health).x, Is.LessThan(_bar.HealthNumberBox.width), "her health's number");
        }

        // ---------------- 2b. The recast window on its box ----------------

        [UnityTest]
        public IEnumerator BackstepsWindow_LightsItsBox_WithTheBandAtTheCleanPartsShares_AndNothingWhileClosed()
        {
            int slot = Slot("Backstep"), key = _player.KeyOfSkill(slot);
            Assume.That(key, Is.GreaterThanOrEqualTo(0), "Backstep on a key");
            _player.Teleport(NearA);
            yield return UntilFree();
            _bar.Tick();
            for (int k = 0; k < _bar.SlotCount; k++) Assert.That(_bar.SlotOf(k).Recast, Is.False, "no window: nothing lit");
            _player.StartFresh();
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            _bar.Tick();
            Assume.That(_player.IsRecastWindowOpen, Is.False, "in the hop");
            Assert.That(_bar.SlotOf(key).Recast, Is.False, "not lit in the hop: the window has not opened");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            _bar.Tick();
            KineticSlot s = _bar.SlotOf(key);
            Assert.That(s.Recast, Is.True, "lit");
            Assert.That(s.Box.width, Is.GreaterThan(s.Home.width), "a little larger than its neighbours");
            float window = _player.RecastWindowSeconds;
            Assert.That(s.RecastNow, Is.EqualTo(Mathf.Clamp01(_player.RecastSinceOpened / window)).Within(1e-4f), "where the window's clock is, this frame");
            Assert.That(s.Band.xMin, Is.EqualTo(s.RecastBar.x + s.RecastBar.width * _player.RecastCleanFrom / window).Within(1f), "the band's start at the clean part's start");
            Assert.That(s.Band.xMax, Is.EqualTo(s.RecastBar.x + s.RecastBar.width * _player.RecastCleanTo / window).Within(1f), "and its end at its end");
            Assert.That(s.RecastBar.xMin, Is.GreaterThanOrEqualTo(s.Face.xMin));
            Assert.That(s.RecastBar.xMax, Is.LessThanOrEqualTo(s.Face.xMax), "across the box's face");
            for (int k = 0; k < _bar.SlotCount; k++)
                if (k != key) Assert.That(_bar.SlotOf(k).Recast, Is.False, "only its own box");
            Assert.That(() => { for (int i = 0; i < 300; i++) { _bar.Tick(); _cast.Tick(); } }, NotAllocating, "nothing allocated while the window is open");
            yield return Until(() => !_player.IsRecastWindowOpen || _player.RecastSinceOpened > window, 2f);
            _bar.Tick();
            Assert.That(_bar.SlotOf(key).Recast, Is.False, "the window run out: the box returns at once");
            Assert.That(_bar.SlotOf(key).Box, Is.EqualTo(_bar.SlotOf(key).Home));
        }

        [UnityTest]
        public IEnumerator BackstepsBand_IsLitExactlyWhenTheJudgeCallsThePressClean_BeforeInAndAfter()
        {
            Assume.That(Slot("Backstep"), Is.GreaterThanOrEqualTo(0));
            var moments = new Func<float, float, float, float>[] { Before, In, After };
            string[] names = { "before", "in", "after" };
            for (int m = 0; m < 3; m++)
            {
                yield return BackstepWindowOpen();
                float from = _player.RecastCleanFrom, to = _player.RecastCleanTo;
                float since = 0f;
                bool lit = false;
                PlayerReport? press = null;
                yield return PressAt("Backstep", moments[m], (s, l, r) => { since = s; lit = l; press = r; });
                Assert.That(press.HasValue, Is.True, names[m] + ": a recast press");
                Assert.That(press.Value.Clean, Is.EqualTo(lit), names[m] + ": the judge says what the box showed, at " + since);
                bool inPart = since >= from - 1e-5f && since <= to + 1e-5f;
                Assume.That(inPart, Is.EqualTo(m == 1), names[m] + ": the press came where it was meant to (" + since + " in " + from + " to " + to + ")");
                Assert.That(lit, Is.EqualTo(m == 1), names[m]);
            }
        }

        [UnityTest]
        public IEnumerator TripleTapsLastBand_IsLitExactlyWhenTheJudgeCallsThePressClean_BeforeInAndAfter()
        {
            var moments = new Func<float, float, float, float>[] { Before, In, After };
            string[] names = { "before", "in", "after" };
            for (int m = 0; m < 3; m++)
            {
                yield return TripleTapLastWindowOpen();
                _bar.Tick();
                KineticSlot s = _bar.SlotOf(_player.KeyOfSkill(Slot("Triple Tap")));
                Assert.That(s.Recast, Is.True, "the last window lights the box");
                float window = _player.RecastWindowSeconds, from = _player.RecastCleanFrom, to = _player.RecastCleanTo;
                Assert.That(s.Band.xMin, Is.EqualTo(s.RecastBar.x + s.RecastBar.width * from / window).Within(1f), "its band at the clean part's shares");
                Assert.That(s.Band.xMax, Is.EqualTo(s.RecastBar.x + s.RecastBar.width * to / window).Within(1f));
                float since = 0f;
                bool lit = false;
                PlayerReport? press = null;
                yield return PressAt("Triple Tap", moments[m], (sn, l, r) => { since = sn; lit = l; press = r; });
                Assert.That(press.HasValue, Is.True, names[m] + ": the last press");
                Assert.That(press.Value.Clean, Is.EqualTo(lit), names[m] + ": the judge says what the box showed, at " + since);
                bool inPart = since >= from - 1e-5f && since <= to + 1e-5f;
                Assume.That(inPart, Is.EqualTo(m == 1), names[m] + ": the press came where it was meant to");
                Assert.That(lit, Is.EqualTo(m == 1), names[m]);
            }
        }

        [UnityTest]
        public IEnumerator TheBlade_FollowsHerThroughBackstepsHop_AndItsProgressIsTheWindows()
        {
            int slot = Slot("Backstep");
            yield return UntilFree();
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            FieldInfo herTop = typeof(CastBar).GetField("_herTop", BindingFlags.NonPublic | BindingFlags.Instance);
            int frames = 0;
            float worstPlace = 0f, worstMarker = 0f;
            Vector3? first = null, last = null;
            var end = new GameObject("LooksAtTheFramesEnd").AddComponent<LooksAtTheFramesEnd>();
            _made.Add(end.gameObject);
            end.Look = () =>
            {
                if (!_cast.BladeDrawn || _player.RecastSlot != slot) return;
                frames++;
                float headX = Camera.main.WorldToScreenPoint(_player.Position + Vector3.up * (float)herTop.GetValue(_cast)).x / InterfaceStyle.Scale;
                worstPlace = Mathf.Max(worstPlace, Mathf.Abs(_cast.Blade.center.x - headX));
                CastBarView want = CastBarRules.Recast(_player.RecastLead, _player.RecastWindowSeconds, _player.RecastSincePress, _player.RecastSinceOpened, _player.RecastCleanPart);
                worstMarker = Mathf.Max(worstMarker, Mathf.Abs(_cast.View.Marker - want.Marker));
                if (first == null) first = _player.Position;
                last = _player.Position;
            };
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.05f, 2f);
            end.Look = null;
            InterfaceValues v = InterfaceStyle.V;
            Assert.That(frames, Is.GreaterThan(1), "the blade drawn through the hop");
            Assert.That(worstPlace, Is.LessThan(0.5f), "over her head in every frame, as that frame's camera shows her");
            Assert.That(worstMarker, Is.LessThan(1e-4f), "its marker the window's own progress in every frame");
            // The camera follows her, so on the screen the blade stays over her head while she travels the hop's metres.
            Assert.That(Vector3.Distance(last.Value, first.Value), Is.GreaterThan(1f), "she travelled the hop, the blade over her head all the way");
            Assert.That(_cast.Blade.width, Is.EqualTo(v.BladeWidth * InterfaceRules.BladeFactor(v)).Within(1e-3f), "1.5 times on top of the master scale");
            _bar.Tick();
            Assert.That(_bar.SlotOf(_player.KeyOfSkill(slot)).Recast, Is.True, "and the box lit with it");
        }
    }
}
