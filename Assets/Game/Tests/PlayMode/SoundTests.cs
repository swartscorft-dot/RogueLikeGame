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
    /// The shot sounds, through the seam that records what was asked to play (<see cref="ShotSounds.Requested"/>),
    /// not the speakers. Nobody but Kristian can hear whether they sound right.
    /// </summary>
    public class SoundTests
    {
        const int Keg = 1, Fan = 2, Triple = 7; // key order: Q W E R, S D F G

        PlayerCharacter _player;
        FeelValues _feel;
        Damageable _a;
        readonly List<ShotSoundRequest> _played = new List<ShotSoundRequest>();
        Action _restore;

        float Cycle => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            _played.Clear();
            ShotSounds.Requested += Record;
            _restore = null;
        }

        [TearDown]
        public void Restore()
        {
            ShotSounds.Requested -= Record;
            _restore?.Invoke();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Record(ShotSoundRequest r) => _played.Add(r);

        [UnityTest]
        public IEnumerator TheSceneHasOneListener_AndThePlayerHasItsSounds()
        {
            yield return null;
            Assert.That(Object.FindObjectsByType<AudioListener>().Length, Is.EqualTo(1), "exactly one audio listener");
            Assert.That(_player.Sounds, Is.Not.Null, "the shot sounds are wired to the player");
            Assume.That(_player.ShotLooks != null && _player.ShotLooks.Basic.Sound != null, Is.True, "the basic shot has a sound");
        }

        [UnityTest]
        public IEnumerator ABasicShot_AsksForItsClipOnce_InTheFrameItFires_OnAMissAndOnAHit()
        {
            ShotLook basic = AssumeSound(null);
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;

            int fireFrame = -1;
            _player.Shoot(new Vector3(-10f, 0f, -10f)); // open floor: a miss
            yield return Watch(() => _player.BasicShotsFired >= 1, f => fireFrame = f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_player.BasicShotHits, Is.EqualTo(0), "a miss");
            Assert.That(_played.Count, Is.EqualTo(1), "one sound for the miss");
            Assert.That(_played[0].Clip, Is.SameAs(basic.Sound));
            Assert.That(_played[0].Frame, Is.EqualTo(fireFrame), "in the frame the shot fired");
            Assert.That(_played[0].StartAt, Is.EqualTo(basic.SoundStart).Within(1e-3f), "started past the clip's leading silence");

            yield return new WaitForSeconds(Cycle);
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 2, 2f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "a hit");
            Assert.That(_played.Count, Is.EqualTo(2), "one sound for the hit");
        }

        [UnityTest]
        public IEnumerator FourShotsAtTheFireRate_GiveFourSounds_NoneCutOffByTheNext()
        {
            AssumeSound(null);
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            int clicks = 0;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_player.BasicShotsFired < 4 && Time.realtimeSinceStartup < deadline)
            {
                if (clicks < 4 && !_player.HasPendingShot) { _player.Shoot(_a.transform.position); clicks++; }
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(4));
            Assert.That(_played.Count, Is.EqualTo(4), "four shots, four sounds");
            foreach (ShotSoundRequest r in _played) Assert.That(r.CutOffAnother, Is.False, "the tails overlap: no sound cut off another");
        }

        [UnityTest]
        public IEnumerator EachSkill_PlaysTheClipItsLookNames_OncePerBurstHitAndPerTick()
        {
            ShotLook triple = AssumeSound("Triple Tap"), fan = AssumeSound("Fan Fire"), keg = AssumeSound("Powder Keg");
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;

            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.Not.EqualTo(IntentResult.Rejected));
            _player.ReleaseSkill(Triple);
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f); // and its free recovery (0004)
            int hits = _player.SkillShotsFired - fired;
            Assume.That(hits, Is.GreaterThan(1), "a burst");
            Assert.That(_played.Count, Is.EqualTo(hits), "one sound per hit of the burst");
            foreach (ShotSoundRequest r in _played) Assert.That(r.Clip, Is.SameAs(triple.Sound));

            _played.Clear();
            fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Fan, _a.transform.position), Is.Not.EqualTo(IntentResult.Rejected));
            yield return new WaitForSeconds(0.5f);
            _player.ReleaseSkill(Fan);
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f); // and its free recovery (0004)
            int ticks = _player.SkillShotsFired - fired;
            Assume.That(ticks, Is.GreaterThan(1), "several ticks");
            Assert.That(_played.Count, Is.EqualTo(ticks), "one sound per tick of the hold");
            foreach (ShotSoundRequest r in _played) Assert.That(r.Clip, Is.SameAs(fan.Sound));

            _played.Clear();
            Assert.That(_player.PressSkill(Keg, _a.transform.position), Is.Not.EqualTo(IntentResult.Rejected));
            _player.ReleaseSkill(Keg);
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f); // and its free recovery (0004)
            Assert.That(_played.Count, Is.EqualTo(1), "Powder Keg: one sound when it lands");
            Assert.That(_played[0].Clip, Is.SameAs(keg.Sound));
            Assert.That(_played[0].Pitch, Is.EqualTo(keg.SoundPitch * (_player.IsEmpowered ? _player.ShotLooks.EmpoweredPitch : 1f)).Within(keg.SoundPitchSpread + 1e-3f));
        }

        [UnityTest]
        public IEnumerator ALookWithNoClip_PlaysNothing_AndDoesNotError()
        {
            ShotLook basic = AssumeSound(null);
            AudioClip kept = basic.Sound;
            basic.Sound = null;
            _restore = () => basic.Sound = kept; // the settings asset is shared: put it back
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "the shot still fires");
            Assert.That(_played.Count, Is.EqualTo(0), "silently");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------------- Helpers ----------------

        ShotLook AssumeSound(string skill)
        {
            ShotFxSettings looks = _player.ShotLooks;
            Assume.That(looks, Is.Not.Null);
            ShotLook look = skill == null ? looks.Basic : looks.For(skill);
            Assume.That(look != null && look.Sound != null, Is.True, (skill ?? "the basic shot") + " has a sound");
            Assume.That(_player.Sounds, Is.Not.Null);
            return look;
        }

        /// <summary>Waits for a condition, noting the frame in which it first held.</summary>
        static IEnumerator Watch(Func<bool> condition, Action<int> atFrame)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition()) { atFrame(Time.frameCount); yield break; }
                yield return null;
            }
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
