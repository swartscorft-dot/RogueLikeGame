using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Her components may be asked before they have woken (the morning of 2026-11-23, main <c>c7921e2</c>): in the scene her
    /// character's Awake runs before her health's, and the sprint of work order 092 asked "is she alive" from that Awake. The
    /// health had no value yet, the Awake threw, Unity switched her character off, and she could not move, shoot or end a dash.
    /// No test saw it, because the tests' scene load wakes the two in the other order. Here the two are made on an object that
    /// is off, so neither has woken, and asked the same questions.
    ///
    /// And the scene's own her, woken in BOTH orders (work order 096): the scene's player object is copied while it is off (so
    /// nothing on the copy has woken), its components are put in the order wanted with the Editor's own reordering (Awake runs
    /// in the order of the components on an object that is switched on; in the scene her character is above her health, which
    /// is the order he met), and the copy is switched on. Once her character wakes first, once her health: in each, nothing is
    /// logged (the test framework fails on an exception or error log), every component that is on in the scene is on, she
    /// moves when steered, and a shot goes off when asked. On <c>c7921e2</c> the character-first case fails and the health-first
    /// case passes (seen on a scratch copy of that state, work order 096).
    /// </summary>
    public class HerAwakeOrderTests
    {
        GameObject _go;
        GameObject _original;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            if (_original != null) _original.SetActive(true);
            _original = null;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [Test]
        public void HerHealth_AskedBeforeItHasWoken_IsAlive_AndHerSprint_AskedBeforeAnythingHasWoken_IsOffWithoutAsking()
        {
            _go = new GameObject("HerBeforeAwake");
            _go.SetActive(false); // nothing on it wakes
            PlayerHealth health = _go.AddComponent<PlayerHealth>(); // brings her character and its agent with it
            PlayerCharacter her = _go.GetComponent<PlayerCharacter>();
            Assert.That(her, Is.Not.Null, "her health requires her character");

            Assert.That(health.IsAlive, Is.True, "not yet woken: alive, and no exception");
            Assert.That(health.Current, Is.EqualTo(health.MaxHealth), "not yet woken: her health is the full one she wakes with (work order 096)");
            Assert.That(health.Max, Is.EqualTo(health.MaxHealth), "not yet woken: her maximum is the one she wakes with (work order 096)");
            Assert.That(her.IsSprinting, Is.False, "no key, no move: not sprinting, and nothing else is asked");
            her.HoldSprint(true);
            Assert.That(her.IsSprinting, Is.False, "the key alone, standing still: not sprinting, and nothing else is asked");
        }

        [Test]
        public void ATarget_AskedBeforeItHasWoken_IsAlive_AndFull()
        {
            // Work order 096: an enemy's IsDead asks its Damageable, which may not have woken in another's Awake or OnEnable.
            _go = new GameObject("TargetBeforeAwake");
            _go.SetActive(false); // nothing on it wakes
            Damageable target = _go.AddComponent<Damageable>();
            Assert.That(target.IsAlive, Is.True, "not yet woken: alive, and no exception");
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth), "not yet woken: full");
            Assert.That(target.Info.Alive, Is.True, "and what the shots read of it");
        }

        [UnityTest]
        public IEnumerator TheScenesHer_WokenCharacterFirst_LogsNothing_StaysOn_MovesAndShoots() => WokenInOrder(characterFirst: true);

        [UnityTest]
        public IEnumerator TheScenesHer_WokenHealthFirst_LogsNothing_StaysOn_MovesAndShoots() => WokenInOrder(characterFirst: false);

        IEnumerator WokenInOrder(bool characterFirst)
        {
#if !UNITY_EDITOR
            Assert.Ignore("the components are reordered with the Editor's ComponentUtility");
            yield break;
#else
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            PlayerCharacter scenes = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(scenes, Is.Not.Null, "PlayerCharacter in scene");
            _original = scenes.gameObject;
            var onInTheScene = new HashSet<string>(_original.GetComponents<MonoBehaviour>().Where(b => b.enabled).Select(b => b.GetType().Name));
            _original.SetActive(false);
            _go = Object.Instantiate(_original); // off, as the original is now: nothing on the copy has woken
            _go.name = _original.name;
            Assert.That(_go.activeSelf, Is.False);

            PlayerCharacter her = _go.GetComponent<PlayerCharacter>();
            PlayerHealth health = _go.GetComponent<PlayerHealth>();
            PlayerInputDriver driver = _go.GetComponent<PlayerInputDriver>();
            Assert.That(her != null && health != null && driver != null, "her character, health and input driver on the copy");
            int Place(Component c) => System.Array.IndexOf(_go.GetComponents<Component>(), c);
            for (int guard = 0; guard < 10 && (Place(her) < Place(health)) != characterFirst; guard++)
                UnityEditorInternal.ComponentUtility.MoveComponentUp(characterFirst ? her : (Component)health);
            string order = string.Join(", ", _go.GetComponents<Component>().Select(c => c.GetType().Name));
            Assert.That(Place(her) < Place(health), Is.EqualTo(characterFirst), "the order wanted: " + order);
            TestContext.WriteLine("AWAKEORDER " + (characterFirst ? "character first" : "health first") + ": " + order);

            _go.SetActive(true); // they wake now, in this order; an exception in an Awake fails the test as an unexpected log
            driver.InputSource = () => null;
            health.Untouchable = true;
            yield return null;
            yield return null;

            foreach (MonoBehaviour b in _go.GetComponents<MonoBehaviour>())
                if (onInTheScene.Contains(b.GetType().Name)) Assert.That(b.enabled, Is.True, b.GetType().Name + " is on (" + order + ")");
            Assert.That(her.enabled && health.enabled && driver.enabled, "her character, health and input driver are on (" + order + ")");
            Assert.That(health.IsAlive, Is.True);

            Vector3 from = her.Position;
            her.Steer(Vector3.right);
            float until = Time.time + 0.6f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
            her.Stop();
            float moved = new Vector2(her.Position.x - from.x, her.Position.z - from.z).magnitude;
            Assert.That(moved, Is.GreaterThan(1f), "she moves when steered (" + order + ")");

            int shots = her.BasicShotsFired;
            her.Shoot(her.Position + Vector3.forward * 6f);
            until = Time.time + 1f;
            deadline = Time.realtimeSinceStartup + 10f;
            while (her.BasicShotsFired == shots && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(her.BasicShotsFired, Is.GreaterThan(shots), "a shot goes off when asked (" + order + ")");
#endif
        }
    }
}
