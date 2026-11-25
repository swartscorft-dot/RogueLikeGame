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

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Hold to fire (work order 132; Kristian ticked "Ja, hold = skyt"), in the ControlRoom scene through the input driver as the
    /// mouse feeds it: one press and the button held fires again and again at the shot's own pace, as many shots as clicking on
    /// every frame gives, none faster; let go, nothing more starts; each shot aims at the cursor where it is when it goes off; after
    /// a choice or a screen held the input the held button fires nothing until it has gone up; held, it fires nothing in a skill,
    /// a dash, a stun or a sprint (and is not counted as refused there) and goes on after; down in a held duck it does not bring
    /// her up (a click does); and with the switch off one click is one shot as before.
    ///
    /// Every target of the practice room is put away, so no shot hits (no hit stop slows the clock). Times are the game's; no
    /// assertion counts frames.
    /// </summary>
    public class HoldToFirePlayTests
    {
        const float CursorMetres = 6f;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Camera _cam;
        FeelValues _feel;
        bool _duckHoldWas, _quickDuckWas, _takeCoverWas;
        Vector2 _cursor, _move;
        bool _press, _held, _cover, _sprint;
        readonly List<(PlayerReportKind Kind, float At, Vector3 Aim, Vector3 Cursor)> _reports = new List<(PlayerReportKind, float, Vector3, Vector3)>();
        readonly List<Damageable> _putAway = new List<Damageable>();
        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            HoldToFireValues.On = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _cam = Camera.main;
            Assert.That(_cam, Is.Not.Null);
            _feel = _player.Feel;
            _duckHoldWas = _feel.DuckHold;
            _quickDuckWas = _feel.QuickDuck;
            _takeCoverWas = _feel.TakeCover;
            _press = _held = _cover = _sprint = false;
            _move = Vector2.zero;
            _driver.InputSource = () =>
            {
                var f = new InputFrame { CursorScreen = _cursor, Move = _move, ShootPressed = _press, ShootHeld = _held || _press, CoverHeld = _cover, SprintHeld = _sprint };
                _press = false;
                return f;
            };
            _putAway.Clear();
            _putAway.AddRange(Damageable.All);
            foreach (Damageable d in _putAway) d.gameObject.SetActive(false);
            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.enabled = false;
            _player.Teleport(Vector3.zero);
            yield return null;
            yield return null;
            _cursor = CursorAt(Vector3.right);
            _reports.Clear();
            _player.Reported += r => _reports.Add((r.Kind, Time.time, _player.AimPoint, _player.AimCursor));
        }

        [TearDown]
        public void CleanUp()
        {
            HoldToFireValues.On = true;
            if (_feel != null)
            {
                _feel.DuckHold = _duckHoldWas; // the feel values live in an asset
                _feel.QuickDuck = _quickDuckWas;
                _feel.TakeCover = _takeCoverWas;
            }
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Holding ----------------

        [UnityTest]
        public IEnumerator OnePressAndTheButtonHeld_FiresAgainAndAgainAtTheShotsOwnPace_AndLetGoNothingMoreStarts()
        {
            float interval = _player.BasicShotInterval;
            _press = true;
            _held = true;
            yield return null;
            float from = Time.time;
            yield return ForSeconds(1.5f);
            _held = false;
            float letGo = Time.time;
            List<float> starts = Starts(from - 0.01f, letGo);
            Assert.That(starts.Count, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(1.5f / (interval + 0.02f))), "one press, and the button held: a shot each cycle");
            Assert.That(starts.Count, Is.LessThanOrEqualTo(Mathf.FloorToInt(1.5f / interval) + 2));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.GreaterThanOrEqualTo(interval - 1e-4f), "never sooner than the shot's own pace");
            Assert.That(_player.HasPendingShot, Is.False, "holding leaves no click waiting");
            yield return null;
            float afterUp = Time.time;
            yield return ForSeconds(0.8f);
            Assert.That(Starts(afterUp, float.PositiveInfinity), Is.Empty, "let go: nothing more starts");
            TestContext.WriteLine($"held 1.5 s: {starts.Count} shots, intervals {Describe(starts)} (the shot's {interval:0.000} s)");
        }

        [UnityTest]
        public IEnumerator HeldForTwoSeconds_FiresTheShotsOfClickingOnEveryFrame_NoMoreNoFewer()
        {
            float interval = _player.BasicShotInterval;
            const float window = 2f;
            // Clicking on every frame: the most a click can do (a click while she cannot shoot waits and goes the moment it may).
            float from = Time.time;
            while (Time.time < from + window)
            {
                _press = true;
                yield return null;
            }
            int clicked = Starts(from, from + window).Count;
            yield return ForSeconds(0.5f); // the last shot's time over
            // Holding.
            _press = true;
            _held = true;
            float heldFrom = Time.time;
            yield return ForSeconds(window);
            _held = false;
            List<float> held = Starts(heldFrom, heldFrom + window);
            TestContext.WriteLine($"in {window} s: clicked on every frame {clicked} shots, held {held.Count}; the shot's {interval:0.000} s allows {Mathf.FloorToInt(window / interval) + 1}");
            Assert.That(held.Count, Is.EqualTo(clicked).Within(1), "the shots a second are the same");
            Assert.That(held.Count, Is.LessThanOrEqualTo(Mathf.FloorToInt(window / interval) + 1), "no faster than the shot allows");
            for (int i = 1; i < held.Count; i++) Assert.That(held[i] - held[i - 1], Is.GreaterThanOrEqualTo(interval - 1e-4f));
        }

        [UnityTest]
        public IEnumerator EachHeldShot_AimsAtTheCursor_WhereItIsWhenTheShotGoesOff()
        {
            // The camera settled on her first; the cursor's point on the ground is read through it as the driver reads it, at the
            // gun's height, which moves a little with her pose: so the shot is checked against the cursor's own point as the
            // character had it when the shot went off (to three degrees: the shot lays the point at the height its line is drawn at, CursorDrift), and against the way it was put (to ten degrees: the two
            // ways are ninety apart).
            float settle = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            _cursor = CursorAt(Vector3.right);
            yield return null;
            _press = true;
            _held = true;
            yield return Until(() => Fired().Count >= 2, 2f);
            Assert.That(Fired().Count, Is.GreaterThanOrEqualTo(2));
            foreach (var r in Fired())
            {
                Assert.That(Degrees(r.Aim, r.Cursor - _player.Position), Is.LessThan(3f), "at the cursor's point as it was when it went off");
                Assert.That(Degrees(r.Aim, Vector3.right), Is.LessThan(10f), "east while the cursor is east");
                TestContext.WriteLine($"east: {Degrees(r.Aim, Vector3.right):0.0} degrees off east");
            }
            _cursor = CursorAt(Vector3.forward);
            float moved = Time.time;
            yield return Until(() => Fired().Count(r => r.At > moved + _player.BasicShotInterval) >= 2, 2f);
            _held = false;
            List<(PlayerReportKind Kind, float At, Vector3 Aim, Vector3 Cursor)> after = Fired().Where(r => r.At > moved + _player.BasicShotInterval).ToList();
            Assert.That(after.Count, Is.GreaterThanOrEqualTo(2), "it goes on firing");
            foreach (var r in after)
            {
                Assert.That(Degrees(r.Aim, r.Cursor - _player.Position), Is.LessThan(3f), "at the moved cursor's point");
                Assert.That(Degrees(r.Aim, Vector3.forward), Is.LessThan(10f), "north once the cursor moved north, without a new press");
                TestContext.WriteLine($"north: {Degrees(r.Aim, Vector3.forward):0.0} degrees off north");
            }
        }

        // ---------------- After a choice or a screen ----------------

        [UnityTest]
        public IEnumerator AfterAChoiceOrAScreen_TheHeldButtonFiresNothing_UntilItHasGoneUp()
        {
            foreach (bool choice in new[] { true, false })
            {
                _press = true;
                _held = true;
                yield return Until(() => Fired().Count > 0, 1f);
                if (choice) PlayerInputDriver.HeldByChoice = true;
                else PlayerInputDriver.HeldByScreen = true;
                for (int i = 0; i < 5; i++) yield return null;
                yield return ForSeconds(0.4f);
                float letGo = Time.time;
                if (choice) PlayerInputDriver.HeldByChoice = false;
                else PlayerInputDriver.HeldByScreen = false;
                yield return ForSeconds(0.8f); // still held, all along
                string what = choice ? "a choice" : "a screen";
                Assert.That(Starts(letGo, float.PositiveInfinity), Is.Empty, "after " + what + ", the button still held: nothing");
                _held = false;
                yield return null;
                yield return null;
                float up = Time.time;
                _press = true;
                _held = true;
                yield return ForSeconds(0.6f);
                _held = false;
                Assert.That(Starts(up, float.PositiveInfinity).Count, Is.GreaterThanOrEqualTo(2), "up once and pressed again: it fires, and holding goes on");
                yield return ForSeconds(0.4f);
                _reports.Clear();
            }
        }

        // ---------------- Refused wherever a click is ----------------

        [UnityTest]
        public IEnumerator Held_ItFiresNothingInASkillADashAStunOrASprint_IsNotCountedAsRefused_AndGoesOnAfter()
        {
            _press = true;
            _held = true;
            yield return Until(() => Fired().Count > 0, 1f);

            // A skill.
            int slot = FirstPlainSkill();
            Assert.That(_player.PressSkill(slot, _player.Position + Vector3.right * 5f), Is.EqualTo(IntentResult.Started));
            float from = Time.time, freeAt = -1f;
            while (Time.time < from + 5f)
            {
                if (_player.ActiveSkillSlot < 0) { freeAt = Time.time; break; }
                yield return null;
            }
            Assert.That(freeAt, Is.GreaterThan(0f), "the skill ended");
            Assert.That(Starts(from + 1e-4f, freeAt - 1e-4f), Is.Empty, "no shot in the skill");
            yield return Until(() => Starts(freeAt - 1e-4f, float.PositiveInfinity).Count > 0, 1f);
            Assert.That(Starts(freeAt - 1e-4f, float.PositiveInfinity), Is.Not.Empty, "held, it goes on after");

            // A dash.
            yield return ForSeconds(0.3f);
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 6f), Is.EqualTo(IntentResult.Started));
            from = Time.time;
            yield return Until(() => !_player.IsDashing, 2f);
            float dashOver = Time.time;
            Assert.That(Starts(from + 1e-4f, dashOver - 1e-4f), Is.Empty, "no shot in the dash");
            yield return Until(() => Starts(dashOver - 1e-4f, float.PositiveInfinity).Count > 0, 1f);
            Assert.That(Starts(dashOver - 1e-4f, float.PositiveInfinity), Is.Not.Empty, "and after it");

            // A stun.
            yield return ForSeconds(0.3f);
            Assert.That(_player.Stun(0.5f, "test"), Is.True);
            from = Time.time;
            yield return Until(() => !_player.IsStunned, 2f);
            float stunOver = Time.time;
            Assert.That(Starts(from + 1e-4f, stunOver - 1e-4f), Is.Empty, "no shot while stunned");
            yield return Until(() => Starts(stunOver - 1e-4f, float.PositiveInfinity).Count > 0, 1f);
            Assert.That(Starts(stunOver - 1e-4f, float.PositiveInfinity), Is.Not.Empty, "and after it");

            // A sprint: a click is refused and counted; held, nothing is asked, so nothing is counted.
            _player.Teleport(Vector3.zero);
            yield return null;
            int refused = _player.RefusedWhileSprinting;
            _move = new Vector2(1f, 0f);
            _sprint = true;
            yield return Until(() => _player.IsSprinting, 1f);
            Assert.That(_player.IsSprinting, Is.True, "she sprints");
            from = Time.time;
            yield return ForSeconds(0.5f);
            Assert.That(Starts(from + 0.05f, Time.time), Is.Empty, "no shot while she sprints");
            Assert.That(_player.RefusedWhileSprinting, Is.EqualTo(refused), "and the held button is not counted as refused every frame");
            _sprint = false;
            _move = Vector2.zero;
            float sprintOver = Time.time;
            yield return Until(() => Starts(sprintOver, float.PositiveInfinity).Count > 0, 1f);
            Assert.That(Starts(sprintOver, float.PositiveInfinity), Is.Not.Empty, "the key up, it goes on");
            _held = false;
        }

        [UnityTest]
        public IEnumerator DownInAHeldDuck_TheHeldButtonDoesNotBringHerUp_AClickDoes()
        {
            _feel.TakeCover = true;
            _feel.QuickDuck = true;
            _feel.DuckHold = true;
            Vector3 way = Vector3.back;
            Prop(PropModel.Sarcophagus, way, 0.5f);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Not.Null, "a low prop in reach");
            _press = true;
            _held = true;
            yield return Until(() => Fired().Count > 0, 1f);
            _cover = true; // the cover key goes down: she ducks, the shoot button held all along
            yield return Until(() => _player.IsQuickDucking, 1f);
            Assert.That(_player.IsQuickDucking, Is.True, "down");
            float down = Time.time;
            yield return ForSeconds(0.8f);
            Assert.That(_player.IsQuickDucking, Is.True, "the held button does not bring her up");
            Assert.That(Starts(down + 1e-4f, float.PositiveInfinity), Is.Empty, "nor fire");
            _press = true; // a click does, as before (work order 086)
            yield return null;
            yield return null;
            Assert.That(_player.IsQuickDucking, Is.False, "a click brings her up");
            Assert.That(Starts(down + 1e-4f, float.PositiveInfinity), Is.Not.Empty, "and it fires");
            _held = false;
            _cover = false;
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator SwitchOff_OnePressAndTheButtonHeld_FiresOneShot_AsBefore()
        {
            HoldToFireValues.On = false;
            _press = true;
            _held = true;
            float from = Time.time;
            yield return ForSeconds(1.2f);
            _held = false;
            Assert.That(Starts(from - 0.01f, float.PositiveInfinity).Count, Is.EqualTo(1), "one click, one shot: holding never repeats");
        }

        // ---------------- Helpers ----------------

        List<float> Starts(float from, float to) =>
            _reports.Where(r => r.Kind == PlayerReportKind.ShotStarted && r.At >= from && r.At <= to).Select(r => r.At).ToList();

        List<(PlayerReportKind Kind, float At, Vector3 Aim, Vector3 Cursor)> Fired() => _reports.Where(r => r.Kind == PlayerReportKind.ShotFired).ToList();

        float Degrees(Vector3 aim, Vector3 way)
        {
            Vector3 d = aim - _player.Position;
            d.y = 0f;
            way.y = 0f;
            return Vector3.Angle(d, way);
        }

        /// <summary>The cursor on the screen over the point <see cref="CursorMetres"/> from her along <paramref name="way"/>, at the gun's height.</summary>
        Vector2 CursorAt(Vector3 way)
        {
            Vector3 at = _player.Position + way * CursorMetres + Vector3.up * _player.ShotHeight;
            Vector3 s = _cam.WorldToScreenPoint(at);
            return new Vector2(s.x, s.y);
        }

        int FirstPlainSkill()
        {
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot) && _player.SkillCooldownRemaining(slot) <= 0f && _player.Skills[slot].Input == SkillInput.Normal
                    && _player.Skills[slot].RecastCount <= 1 && _player.Skills[slot].MoveDistance == 0f)
                    return slot;
            Assert.Fail("no plain skill ready");
            return -1;
        }

        /// <summary>A low prop as a level builds it, its near face <paramref name="face"/> m from her along <paramref name="way"/>.</summary>
        GameObject Prop(PropModel model, Vector3 way, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            Vector3 at = _player.Position + way * (face + half);
            Vector3 toHer = _player.Position - at;
            toHer.y = 0f;
            int rotation = (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, new Vector3(at.x, 0f, at.z), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        static IEnumerator ForSeconds(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds * 4f + 5f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static string Describe(List<float> starts)
        {
            var parts = new List<string>();
            for (int i = 1; i < starts.Count; i++) parts.Add((starts[i] - starts[i - 1]).ToString("0.000"));
            return string.Join(" ", parts);
        }
    }
}
