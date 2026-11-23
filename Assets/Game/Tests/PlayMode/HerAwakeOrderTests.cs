using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Her components may be asked before they have woken (the morning of 2026-11-23, main <c>c7921e2</c>): in the scene her
    /// character's Awake runs before her health's, and the sprint of work order 092 asked "is she alive" from that Awake. The
    /// health had no value yet, the Awake threw, Unity switched her character off, and she could not move, shoot or end a dash.
    /// No test saw it, because the tests' scene load wakes the two in the other order. Here the two are made on an object that
    /// is off, so neither has woken, and asked the same questions.
    /// </summary>
    public class HerAwakeOrderTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
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
            Assert.That(her.IsSprinting, Is.False, "no key, no move: not sprinting, and nothing else is asked");
            her.HoldSprint(true);
            Assert.That(her.IsSprinting, Is.False, "the key alone, standing still: not sprinting, and nothing else is asked");
        }
    }
}
