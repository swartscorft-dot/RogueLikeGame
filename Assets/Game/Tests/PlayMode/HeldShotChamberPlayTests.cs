using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 141 with work order 132's hold to fire (the order: "a held stream of shots counting the 6th one by one"): the button
    /// held through the input driver as the mouse feeds it, on Dummy_A of the practice room, with Sixth Chamber held: every held shot is
    /// a shot fired of its own, so the sixth is the heavy round, as with clicks. Her stats are off (no crit roll); every other target
    /// is put away (as HeldShotUpgradesPlayTests does).
    /// </summary>
    public class HeldShotChamberPlayTests
    {
        const int Shots = 6;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Camera _cam;
        Damageable _a;
        RunModifiers _m;
        Vector2 _cursor;
        bool _press, _held;
        readonly List<Damageable> _putAway = new List<Damageable>();
        EnemyBrute _brute;

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
            _press = _held = false;
            _driver.InputSource = () =>
            {
                var f = new InputFrame { CursorScreen = _cursor, ShootPressed = _press, ShootHeld = _held || _press };
                _press = false;
                return f;
            };
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _putAway.Clear();
            foreach (Damageable d in Damageable.All) if (d != _a) _putAway.Add(d);
            foreach (Damageable d in _putAway) d.gameObject.SetActive(false);
            _brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (_brute != null) _brute.enabled = false;
            _m = new RunModifiers { StatsAndTiers = false };
            _player.Modifiers = _m;
            _player.Teleport(Vector3.zero);
            yield return null;
            yield return null;
            Vector3 at = new Vector3(_a.transform.position.x, _player.ShotHeight, _a.transform.position.z);
            Vector3 s = _cam.WorldToScreenPoint(at);
            _cursor = new Vector2(s.x, s.y);
        }

        [TearDown]
        public void CleanUp()
        {
            HoldToFireValues.On = true;
            if (_player != null) _player.Modifiers = null;
            foreach (Damageable d in _putAway) if (d != null) d.gameObject.SetActive(true);
            _putAway.Clear();
            if (_brute != null) _brute.enabled = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest]
        public IEnumerator TheButtonHeld_CountsEachShotOnItsOwn_TheSixthIsTheHeavyRound()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.SixthChamber);
            var lost = new List<int>();
            var fired = new List<int>();
            int hits = _a.HitsTaken, health = _a.CurrentHealth;
            _press = true; // one press, then the button held
            _held = true;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (lost.Count < Shots && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_a.HitsTaken == hits) continue;
                Assert.That(_a.HitsTaken, Is.EqualTo(hits + 1), "one shot, one hit, a frame at a time");
                lost.Add(health - _a.CurrentHealth);
                fired.Add(_player.ChamberFired);
                hits = _a.HitsTaken;
                health = _a.CurrentHealth;
            }
            _held = false;
            Assert.That(lost.Count, Is.EqualTo(Shots), "the held button hit Dummy_A six times");
            int b = Placeholders.BasicShotDamage;
            Assert.That(fired, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 0 }), "each held shot counted one by one");
            Assert.That(lost, Is.EqualTo(new[] { b, b, b, b, b, 2 * b }), "the sixth held shot is the heavy round");
            Assert.That(_player.HeavyRounds, Is.EqualTo(1));
        }
    }
}
