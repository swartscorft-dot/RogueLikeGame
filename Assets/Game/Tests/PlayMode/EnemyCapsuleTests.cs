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
    /// With <see cref="EnemyValues.UseModels"/> off on the practice enemy's values (work order 011), every enemy is its
    /// coloured capsule again, as before the models: no model on any kind, the capsule shown in its colour, tinted
    /// while dazed, flashing on a hit, gone at once when killed, and the attacks strike as ever. The switch is put back
    /// after each test.
    /// </summary>
    public class EnemyCapsuleTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        // Held here so the settings asset stays loaded, with the switch off, across the scene's reload.
        static EnemySettings s_settings;

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        bool _wasOn;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            s_settings = Object.FindAnyObjectByType<EnemyBrute>().Settings;
            Assert.That(s_settings, Is.Not.Null, "the enemy settings");
            _wasOn = s_settings.Values.UseModels;
            s_settings.Values.UseModels = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy.Settings, Is.SameAs(s_settings));
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (s_settings != null) s_settings.Values.UseModels = _wasOn;
            s_settings = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;

        static Renderer Capsule(Enemy e)
        {
            Transform body = e.transform.Find("Body");
            return body != null && body.gameObject.activeInHierarchy ? body.GetComponent<Renderer>() : null;
        }

        [UnityTest]
        public IEnumerator EveryKind_IsItsCapsule_TintedAndFlashing_StrikesAsEver_AndIsGoneAtOnceWhenKilled()
        {
            var made = new List<Enemy> { _levels.SpawnPractice(EnemyKind.Swarmer)[0], _levels.SpawnPractice(EnemyKind.Shooter)[0],
                                         _levels.SpawnPractice(EnemyKind.Guard)[0] };
            yield return null;
            foreach (Enemy e in new List<Enemy>(made) { _practiceEnemy })
            {
                Assert.That(e.Look, Is.Null, e.Kind + " wears no model");
                Assert.That(e.transform.Find(EnemyLook.PivotName), Is.Null);
                Assert.That(Capsule(e), Is.Not.Null, e.Kind + ": its capsule is there");
                Assert.That(Capsule(e).enabled, Is.True);
            }
            Assert.That(Near(made[0].BodyRenderer.sharedMaterial.color, EnemyFactory.SwarmerColor), Is.True, "the swarmer pale");
            Assert.That(Near(made[1].BodyRenderer.sharedMaterial.color, EnemyFactory.ShooterColor), Is.True, "the shooter yellow");
            Assert.That(Near(made[2].BodyRenderer.sharedMaterial.color, EnemyFactory.GuardColor), Is.True, "the guard dark red");

            // The guard dazed is tinted; a hit flashes.
            Enemy guard = made[2];
            guard.Body.TakeHit(1, 1e6f);
            yield return null;
            Assert.That(guard.IsDazed, Is.True);
            Assert.That(Near(guard.BodyRenderer.sharedMaterial.color, EnemyFactory.GuardColor), Is.False, "tinted while dazed");
            Assert.That(guard.GetComponent<HitFlash>().IsFlashing, Is.True, "the hit flashes");

            // The brute strikes as ever.
            _practiceEnemy.enabled = true;
            _player.Teleport(_practiceEnemy.transform.position + Vector3.left * 2f);
            yield return Until(() => _practiceEnemy.Strikes > 0, 4f);
            Assert.That(_practiceEnemy.Strikes, Is.EqualTo(1));

            // Killed, the capsule is gone at once.
            Enemy swarmer = made[0];
            swarmer.Body.TakeHit(swarmer.Body.MaxHealth);
            yield return null;
            yield return null;
            Assert.That(swarmer.IsDead, Is.True);
            Assert.That(swarmer.ShownDeadFor(swarmer.Values), Is.EqualTo(0f));
            Assert.That(swarmer.transform.Find("Body").GetComponent<Renderer>().enabled, Is.False, "a dead capsule is not shown");
        }
    }
}
