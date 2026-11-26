using System;
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
    /// The merge of 2026-11-26: work order 132's hold to fire meets work order 138's basic-shot upgrades. Each order proved its
    /// own part alone (132 with no target hit, 138 with one click a shot); here the button is held through the input driver as the
    /// mouse feeds it, on Dummy_A of the practice room, with Hot Barrel and Marking Rounds held: every held shot is judged on its
    /// own, as a click's is (the build-up rises a step a hit, the fourth hit marks). Her stats are off (no crit roll), so every
    /// number is exact; every other target is put away, so nothing else is hit or pierced.
    /// </summary>
    public class HeldShotUpgradesPlayTests
    {
        const int Hits = 4; // Marking Rounds at tier I marks the fourth basic hit

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
        public IEnumerator TheButtonHeld_EachShotCountsOnItsOwn_HotBarrelAStepAHit_MarkingRoundsTheFourthHit()
        {
            _m.BasicShot.Take(BasicShotUpgradeKind.HotBarrel);
            _m.BasicShot.Take(BasicShotUpgradeKind.MarkingRounds);
            Assert.That(_player.BasicHitsPerMark, Is.EqualTo(Hits));
            int started = 0;
            _player.Reported += r => { if (r.Kind == PlayerReportKind.ShotStarted) started++; };

            var lost = new List<int>();
            var marks = new List<int>();
            int hits = _a.HitsTaken, health = _a.CurrentHealth;
            _press = true; // one press, then the button held
            _held = true;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (lost.Count < Hits && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_a.HitsTaken == hits) continue;
                Assert.That(_a.HitsTaken, Is.EqualTo(hits + 1), "one shot, one hit, a frame at a time");
                lost.Add(health - _a.CurrentHealth);
                marks.Add(_a.MarkCount);
                hits = _a.HitsTaken;
                health = _a.CurrentHealth;
            }
            _held = false;
            Assert.That(lost.Count, Is.EqualTo(Hits), "the held button hit Dummy_A four times");
            Assert.That(started, Is.GreaterThanOrEqualTo(Hits), "every hit was a shot of its own, started by the held button");

            int b = Placeholders.BasicShotDamage;
            Assert.That(lost, Is.EqualTo(new[] { b, Mathf.RoundToInt(b * 1.03f), Mathf.RoundToInt(b * 1.06f), Mathf.RoundToInt(b * 1.09f) }),
                        "Hot Barrel: +3 % a hit in a row, each held shot a step");
            Assert.That(marks, Is.EqualTo(new[] { 0, 0, 0, 1 }), "Marking Rounds: the fourth held shot's hit marks");
            Assert.That(_player.HeatHits, Is.GreaterThanOrEqualTo(Hits), "the heat counts the held shots' hits");
        }
    }
}
