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
    /// The sound's levels in play (work order 131; a friend's first run: "Lyden er helt sjukt høy første gang man logger inn, også er
    /// det forskjell i volum på de høye skytelydene"), in the ControlRoom scene: the listener's volume at a first start (nothing
    /// saved) and with a saved step, as the settings put it on (<see cref="GameSettings.Load"/>, which the HUD calls as it starts);
    /// and a shot's voice playing at its clip's gain times the volume it had, for the basic shot (the same seeded variation with the
    /// switch off and on) and for two skills. Through what was asked to play (<see cref="ShotSounds.Requested"/>), never the
    /// speakers: nobody has listened to it. The volume key this machine had is put back after each test.
    /// </summary>
    public class SoundLevelsPlayTests
    {
        const int QuickShot = 0, ScatterBlast = 4; // key order: Q 1 2 3 R 4 F and the right button

        PlayerCharacter _player;
        Damageable _a;
        readonly List<ShotSoundRequest> _played = new List<ShotSoundRequest>();
        int? _volumeBefore;
        bool _switchWas;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            _switchWas = SoundLevelsValues.On;
            _volumeBefore = PlayerPrefs.HasKey(GameSettings.VolumeKey) ? PlayerPrefs.GetInt(GameSettings.VolumeKey) : (int?)null;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            _played.Clear();
            ShotSounds.Requested += Record;
        }

        [TearDown]
        public void Restore()
        {
            ShotSounds.Requested -= Record;
            SoundLevelsValues.On = _switchWas;
            GameSettings.Unload();
            PlayerPrefs.DeleteKey(GameSettings.VolumeKey);
            if (_volumeBefore.HasValue) PlayerPrefs.SetInt(GameSettings.VolumeKey, _volumeBefore.Value);
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Record(ShotSoundRequest r) => _played.Add(r);

        static float Trim => SoundLevels.DbToGain(SoundLevelsValues.Live.MasterTrimDb);

        // ---------------- The listener ----------------

        [UnityTest]
        public IEnumerator AtAFirstStart_TheListenerIsStepEightUnderTheTrim_AndNothingIsSaved()
        {
            PlayerPrefs.DeleteKey(GameSettings.VolumeKey);
            SoundLevelsValues.On = true;
            GameSettings.Load();
            yield return null;
            Assert.That(GameSettings.VolumeStep, Is.EqualTo(8), "never set: the first start's step");
            Assert.That(AudioListener.volume, Is.EqualTo(0.8f * Trim).Within(1e-5f), "80 % of the step, under the -6 dB trim");
            Assert.That(PlayerPrefs.HasKey(GameSettings.VolumeKey), Is.False, "a first start writes nothing: the default may move again");

            // The switch off: the top step and full volume, as before the order.
            GameSettings.Unload();
            SoundLevelsValues.On = false;
            GameSettings.Load();
            yield return null;
            Assert.That(GameSettings.VolumeStep, Is.EqualTo(SettingsRules.DefaultVolumeStep));
            Assert.That(AudioListener.volume, Is.EqualTo(1f), "full, exactly as before");
        }

        [UnityTest]
        public IEnumerator ASavedStep_IsKeptAsItIs_AndOnlyTheTrimIsUnderIt()
        {
            SoundLevelsValues.On = true;
            foreach (int saved in new[] { 10, 7, 3 })
            {
                PlayerPrefs.SetInt(GameSettings.VolumeKey, saved);
                GameSettings.Load();
                yield return null;
                Assert.That(GameSettings.VolumeStep, Is.EqualTo(saved), "saved " + saved + ": kept");
                Assert.That(PlayerPrefs.GetInt(GameSettings.VolumeKey, -1), Is.EqualTo(saved), "never written over");
                Assert.That(AudioListener.volume, Is.EqualTo(saved / 10f * Trim).Within(1e-5f), "its own step, under the trim");
                GameSettings.Unload();
            }

            // A step set in the menu: kept, and the trim under it.
            GameSettings.Load();
            GameSettings.SetVolumeStep(6);
            Assert.That(PlayerPrefs.GetInt(GameSettings.VolumeKey, -1), Is.EqualTo(6));
            Assert.That(AudioListener.volume, Is.EqualTo(0.6f * Trim).Within(1e-5f));

            // The switch off: a saved step plays at exactly its own volume.
            GameSettings.Unload();
            SoundLevelsValues.On = false;
            PlayerPrefs.SetInt(GameSettings.VolumeKey, 7);
            GameSettings.Load();
            Assert.That(AudioListener.volume, Is.EqualTo(SettingsRules.Volume(7)));
        }

        // ---------------- The shots ----------------

        [UnityTest]
        public IEnumerator ABasicShotsVoice_PlaysAtItsClipsGain_TimesTheVolumeItHad()
        {
            ShotLook basic = Look(null);
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            float cycle = _player.Feel.BasicShotStartup + _player.Feel.BasicShotActive + _player.Feel.BasicShotRecovery;

            // The same seeded variation, its jitter and all, with the switch off and on.
            SoundLevelsValues.On = false;
            _player.SeedShotVariations(131UL);
            yield return OneBasicShot();
            Assert.That(_played.Count, Is.EqualTo(1), "one sound, the switch off");
            ShotSoundRequest off = _played[0];
            yield return new WaitForSeconds(cycle + 0.05f);

            _played.Clear();
            SoundLevelsValues.On = true;
            _player.SeedShotVariations(131UL);
            yield return OneBasicShot();
            Assert.That(_played.Count, Is.EqualTo(1), "one sound, the switch on");
            ShotSoundRequest on = _played[0];

            Assert.That(on.Clip, Is.SameAs(basic.Sound));
            Assert.That(on.Clip.name, Is.EqualTo("gunshot_54"), "her basic shot's recording");
            Assert.That(on.Variation, Is.EqualTo(off.Variation), "the same variation");
            Assert.That(on.Pitch, Is.EqualTo(off.Pitch), "the same pitch: only its level moves");
            float gain = SoundLevels.DbToGain(SoundLevels.ClipGainDb("gunshot_54"));
            Assert.That(on.Volume, Is.EqualTo(off.Volume * gain).Within(1e-6f), "its old volume times the clip's -5.1 dB");
            Assert.That(20f * Mathf.Log10(on.Volume / off.Volume), Is.EqualTo(-5.1f).Within(0.01f));
            float morning = basic.SoundVolume * _player.ShotLooks.SoundVolume;
            Assert.That(off.Volume, Is.InRange(morning * ShotVariationPicker.DbToGain(-4.5f) - 1e-5f, morning * ShotVariationPicker.DbToGain(-1.5f) + 1e-5f),
                        "off: the volume it had (the asset's, -3 dB, its jitter)");
        }

        [UnityTest]
        public IEnumerator ASkillsVoice_PlaysAtItsClipsGain_TimesTheVolumeItHad_AndOffAtTheVolumeItHad()
        {
            ShotLook quick = Look("Quick Shot"), scatter = Look("Scatter Blast");
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            float master = _player.ShotLooks.SoundVolume;

            SoundLevelsValues.On = true;
            yield return OneSkill(QuickShot, quick);
            ShotSoundRequest q = _played.Find(r => r.Clip == quick.Sound);
            Assert.That(q.Clip, Is.Not.Null, "Quick Shot sounded");
            Assert.That(q.Clip.name, Is.EqualTo("gunshot_42"));
            Assert.That(q.Volume, Is.EqualTo(quick.SoundVolume * master * SoundLevels.ClipGainOf("gunshot_42")).Within(1e-6f), "the asset's 0.65 times -1 dB");

            _played.Clear();
            SoundLevelsValues.On = false;
            yield return OneSkill(ScatterBlast, scatter);
            ShotSoundRequest s = _played.Find(r => r.Clip == scatter.Sound);
            Assert.That(s.Clip, Is.Not.Null, "Scatter Blast sounded");
            Assert.That(s.Volume, Is.EqualTo(Mathf.Clamp01(scatter.SoundVolume * master)), "off: exactly the volume it had");
        }

        // ---------------- Helpers ----------------

        ShotLook Look(string skill)
        {
            ShotFxSettings looks = _player.ShotLooks;
            Assume.That(looks, Is.Not.Null);
            ShotLook look = skill == null ? looks.Basic : looks.For(skill);
            Assume.That(look != null && look.Sound != null, Is.True, (skill ?? "the basic shot") + " has a sound");
            Assume.That(_player.Sounds, Is.Not.Null);
            return look;
        }

        IEnumerator OneBasicShot()
        {
            int fired = _player.BasicShotsFired;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            yield return null;
        }

        IEnumerator OneSkill(int slot, ShotLook look)
        {
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.Not.EqualTo(IntentResult.Rejected), look.Name);
            _player.ReleaseSkill(slot);
            yield return Until(() => _played.Exists(r => r.Clip == look.Sound), 3f);
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f);
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
