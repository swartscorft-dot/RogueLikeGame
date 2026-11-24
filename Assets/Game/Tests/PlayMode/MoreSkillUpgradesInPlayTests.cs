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
    /// Work order 111 (2026-11-24, every skill three upgrade choices), narrow: only the rows that change what happens in the scene
    /// through code of their own: Gun Slide (a basic shot that hits during the slide deals 50 % more), Marked Opening (a counter puts
    /// three marks on the enemy it stops) and Recoil Slug (the slug carries her 3 m back). In the ControlRoom scene through the
    /// intents, the rows taken as a run takes them (<see cref="RunSkillUpgrades"/>). Nobody has played any of it.
    /// </summary>
    public class MoreSkillUpgradesInPlayTests
    {
        PlayerCharacter _player;
        Damageable _a;
        RunSkillUpgrades _upgrades;
        readonly List<int> _damage = new List<int>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false; // the brute's Shockwave of before, as SkillPoolTests counters it
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _damage.Clear();
            _a.Damaged += d => _damage.Add(d);
            _upgrades = new RunSkillUpgrades();
            _player.SkillUpgrades = _upgrades;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.SkillUpgrades = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        void Take(string skill, string row)
        {
            SkillUpgradeDefinition u = SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, row);
            Assert.That(u, Is.Not.Null, skill + ": " + row);
            _upgrades.Take(u);
            _player.EquipSkill(Slot(skill));
        }

        /// <summary>A basic shot at A from 3 m, during a slide or not; the damage it dealt.</summary>
        IEnumerator BasicShotAtA(bool sliding, Action<int> got)
        {
            Vector3 at = _a.transform.position;
            Vector3 from = at + Vector3.back * 3f;
            _player.Teleport(from);
            yield return null;
            _damage.Clear();
            if (sliding)
            {
                _player.Steer(Vector3.right); // she slides across, A ahead of her the whole time
                Assert.That(_player.PressSkill(Slot("Slide"), at), Is.EqualTo(IntentResult.Started));
                Assert.That(_player.IsKeyMoving, Is.True);
            }
            _player.Shoot(at);
            yield return Until(() => _damage.Count > 0 || _player.BasicShotsFired > 0 && !_player.IsKeyMoving && _player.Action == ActionKind.None, 2f);
            _player.Steer(Vector3.zero);
            got(_damage.Count > 0 ? _damage[0] : 0);
        }

        [UnityTest]
        public IEnumerator GunSlide_ABasicShotThatHitsDuringTheSlide_Deals50PercentMore_AndOneAfterIt_AsBefore()
        {
            int plain = 0, slid = 0;
            yield return BasicShotAtA(false, d => plain = d);
            Assert.That(plain, Is.GreaterThan(0), "a basic shot that hits");
            Take("Slide", "Gun Slide");
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            yield return BasicShotAtA(true, d => slid = d);
            Assert.That(slid, Is.EqualTo(MoreUpgradeRules.SlideShot(plain, 0.5f)), "half as much again, during the slide");
            yield return Until(() => !_player.IsKeyMoving && _player.Action == ActionKind.None, 3f);
            int after = 0;
            yield return BasicShotAtA(false, d => after = d);
            Assert.That(after, Is.EqualTo(plain), "after the slide, as before");
        }

        [UnityTest]
        public IEnumerator MarkedOpening_ACounterOnTheBrutesShockwave_PutsThreeMarksOnIt_BesidesTheShotsOwn()
        {
            Take("Stopping Shot", "Marked Opening");
            int stop = Slot("Stopping Shot");
            SkillDefinition skill = _player.Skills[stop];
            Assert.That(skill.CounterMarks, Is.EqualTo(3));
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            Vector3 home = brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * 7f);
            yield return Until(() => brute.State == EnemyState.Windup, 6f);
            Assume.That(brute.State, Is.EqualTo(EnemyState.Windup), "the brute winds up");
            Assume.That(brute.Attack?.Name, Is.EqualTo("Shockwave"));
            Assert.That(brute.Body.MarkCount, Is.Zero);
            Assert.That(_player.PressSkill(stop, brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Countered > 0 || brute.State != EnemyState.Windup, 3f);
            Assert.That(brute.Countered, Is.EqualTo(1), "countered");
            Assert.That(brute.Body.MarkCount, Is.EqualTo(skill.CounterMarks + skill.MarksAdded), "three for the counter, one for the shot");
        }

        [UnityTest]
        public IEnumerator RecoilSlug_TheSlugCarriesHer3mBack_AwayFromWhereSheAims()
        {
            Take("Breaker Slug", "Recoil Slug");
            int slug = Slot("Breaker Slug");
            Vector3 at = _a.transform.position;
            Vector3 from = at + Vector3.back * 3f;
            _player.Teleport(from);
            yield return null;
            int hits = _a.HitsTaken;
            Assert.That(_player.PressSkill(slug, at), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken > hits && _player.Action == ActionKind.None, 3f);
            Assert.That(_a.HitsTaken, Is.GreaterThan(hits), "the slug hit A, from 6 m once she had moved");
            Assert.That(Flat(_player.Position, from), Is.EqualTo(3f).Within(0.3f), "3 m moved");
            Assert.That(Flat(_player.Position, at), Is.GreaterThan(Flat(from, at) + 2.5f), "away from A");
        }
    }
}
