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
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 064 (Kristian, 2026-11-21), the feel of firing, in the built ControlRoom scene through the intents:
    /// Backstep's shot goes off soon after its second press, its window as long as before, a first press just before the
    /// cooldown ends goes through, and the hop is not held back by another skill's free recovery; a clean Triple Tap chain
    /// pays more, a full clean chain most; the muzzle light comes and goes, the camera's kick comes back and never adds up,
    /// and with its setting off the camera stays at the rig's place; a hit gives one number of its kind, a blast's numbers
    /// are spread, and nothing is allocated per frame; four basic shots are never the same variation twice running; the
    /// effects are off in a test run as on a pilot's flight. Nobody has seen or heard any of it: these are numbers.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3). The set-up equips all eight skills.
    /// </summary>
    public class FeelOfFiringTests
    {
        const int Quick = 0, Scatter = 4, Backstep = 6, Triple = 7;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);

        PlayerCharacter _player;
        Damageable _a;
        FiringWeight _weight;
        TimingFeedback _feedback;
        readonly List<Object> _made = new List<Object>();
        readonly List<ShotSoundRequest> _sounds = new List<ShotSoundRequest>();
        Action _restore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _sounds.Clear();
            ShotSounds.Requested += Record;
            _restore = null;
        }

        [TearDown]
        public void CleanUp()
        {
            ShotSounds.Requested -= Record;
            _restore?.Invoke();
            _restore = null;
            if (_weight != null) Object.Destroy(_weight.gameObject);
            _weight = null;
            if (_feedback != null) Object.Destroy(_feedback.gameObject);
            _feedback = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            GameSettings.Unload(); // the camera's kick back on (a test keeps nothing)
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Record(ShotSoundRequest r) => _sounds.Add(r);

        // ---------------- 1. Backstep ----------------

        [UnityTest]
        public IEnumerator Backstep_TheShotGoesOff008AfterTheSecondPress_TheWindowAsLongAsBefore_ItsCleanPartEarly()
        {
            SkillDefinition step = _player.Skills[Backstep];
            KitValues kit = _player.Kit;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            float openedAt = Time.time;
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(kit.OpeningMoveCleanFrom).Within(1e-5f), "the clean part after the hop: 0.03 s");
            Assert.That(_player.RecastCleanTo, Is.EqualTo(kit.OpeningMoveCleanTo).Within(1e-5f), "to 0.23 s");
            Assert.That(_player.RecastCleanFromShare, Is.EqualTo(kit.OpeningMoveCleanFrom / step.RecastWindow).Within(1e-4f), "as a share, for the strip");
            Assert.That(_player.RecastWindowOpenFor(Backstep), Is.True);
            Assert.That(_player.RecastWindowOpenFor(Triple), Is.False);
            yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assert.That(_player.RecastWindowProgress, Is.InRange(0.1f, 0.4f), "the window's progress, 0 to 1");
            int fired = _player.SkillShotsFired;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started), "the clean press");
            Assert.That(_player.RecastLead, Is.EqualTo(kit.OpeningMoveShotStartup).Within(1e-5f), "the cast bar reads the shot's own lead");
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            float toShot = Time.time - pressedAt;
            Debug.Log($"[064] Backstep: shot {toShot:0.000} s after the second press (was 0.2), window opened {openedAt:0.000}");
            Assert.That(toShot, Is.EqualTo(kit.OpeningMoveShotStartup).Within(0.02f), "0.08 s from the press to the shot");
            Assert.That(_a.HitsTaken, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Backstep_TheWindowAfterTheHop_RunsItsFull06Seconds()
        {
            SkillDefinition step = _player.Skills[Backstep];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            float openedAt = Time.time;
            yield return Until(() => _player.RecastSlot == -1, 2f);
            Assert.That(Time.time - openedAt, Is.EqualTo(step.RecastWindow).Within(0.05f), "0.6 s, as before");
        }

        [UnityTest]
        public IEnumerator Backstep_APressJustBeforeTheCooldownEnds_WaitsAndGoes_OneEarlierIsRefused()
        {
            KitValues kit = _player.Kit;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.RecastSlot == -1, 2f); // the window runs out: the chain is over, the cooldown runs
            Time.timeScale = 4f; // the waits are in game time
            yield return Until(() => _player.SkillCooldownRemaining(Backstep) <= 0.5f, 6f);
            Assert.That(_player.SkillCooldownRemaining(Backstep), Is.GreaterThan(kit.CooldownBuffer + 0.02f));
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "0.5 s before: refused, as before");
            yield return Until(() => _player.SkillCooldownRemaining(Backstep) <= kit.CooldownBuffer * 0.6f, 4f);
            float left = _player.SkillCooldownRemaining(Backstep);
            Assert.That(left, Is.GreaterThan(0f));
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Buffered), left.ToString("0.000") + " s before: it waits");
            Assert.That(_player.HasBufferedPress, Is.True);
            yield return Until(() => _player.RecastSlot == Backstep, 2f);
            Time.timeScale = 1f;
            Assert.That(_player.RecastSlot, Is.EqualTo(Backstep), "it went through: the hop");
            Assert.That(Time.time - pressedAt, Is.EqualTo(left).Within(0.05f), "when the cooldown ended");
        }

        [UnityTest]
        public IEnumerator Backstep_InAQuickShotsFreeRecovery_HopsAtOnce_WithTheSwitchOffItWaits()
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet); // the settings asset is shared: a copy of it
            _made.Add(copy);
            _player.SkillSet = copy;
            KitValues kit = _player.Kit;
            foreach (bool cuts in new[] { true, false })
            {
                kit.OpeningMoveCutsRecovery = cuts;
                _player.Teleport(NearA);
                yield return new WaitForSeconds(0.3f);
                yield return Until(() => _player.SkillCooldownRemaining(Quick) <= 0f && _player.SkillCooldownRemaining(Backstep) <= 0f, 12f);
                int fired = _player.SkillShotsFired;
                Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
                yield return Until(() => _player.SkillShotsFired > fired && _player.Action == ActionKind.None, 2f);
                Assert.That(_player.SkillGateRemaining, Is.GreaterThan(0f), "in Quick Shot's free recovery");
                IntentResult r = _player.PressSkill(Backstep, _a.transform.position);
                Assert.That(r, Is.EqualTo(cuts ? IntentResult.Started : IntentResult.Buffered), cuts ? "the hop cuts it" : "the morning's: it waits it out");
                yield return Until(() => _player.RecastSlot == -1 && _player.Action == ActionKind.None, 3f);
                Time.timeScale = 4f;
                yield return Until(() => _player.SkillCooldownRemaining(Backstep) <= 0f && _player.SkillCooldownRemaining(Quick) <= 0f, 6f);
                Time.timeScale = 1f;
            }
        }

        // ---------------- 2. A clean Triple Tap chain ----------------

        [UnityTest] public IEnumerator TripleTap_AFullCleanChain_ItsLastTapDealsTwiceAClean_AndAMarkMore() => TripleChain(fullClean: true);
        [UnityTest] public IEnumerator TripleTap_ACleanLastTapAfterAnEarlySecond_PaysAsBefore() => TripleChain(fullClean: false);

        IEnumerator TripleChain(bool fullClean)
        {
            SkillDefinition tap = _player.Skills[Triple];
            KitValues kit = _player.Kit;
            _player.Teleport(NearA);
            yield return null;
            int chainsBefore = _player.FullCleanChains;
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            // The second press: in its window's clean part, or at once. Work order 069: its own clean part (0.12 to 0.30 s), read
            // from the game as the last's is (until then the kit's CleanFrom and CleanTo, the same place as the last's).
            if (fullClean) yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the clean last press");
            yield return Until(() => _a.HitsTaken >= 3, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            float scale = fullClean ? kit.CleanDamage * kit.FullChainScale : kit.CleanDamage; // not in a run: no stats, no crit
            int third = Mathf.RoundToInt(tap.Damage * scale);
            Debug.Log($"[064] Triple Tap {(fullClean ? "full clean" : "clean last")}: third tap {third} (clean damage {kit.CleanDamage} in the asset)");
            Assert.That(_a.HitsTaken, Is.EqualTo(3));
            Assert.That(_a.MaxHealth - _a.CurrentHealth, Is.EqualTo(2 * tap.Damage + third));
            int marks = 2 * tap.MarksAdded + tap.CleanTapMarks + (fullClean ? kit.FullChainMarks : 0);
            Assert.That(_a.MarkCount, Is.EqualTo(Mathf.Min(kit.MaxMarks, marks)));
            Assert.That(_player.FullCleanChains - chainsBefore, Is.EqualTo(fullClean ? 1 : 0));
            Assert.That(_player.LastShotWeight, Is.EqualTo(fullClean ? ShotWeight.FullClean : ShotWeight.Skill), "the strongest weight for the full chain's tap");
        }

        // ---------------- 3. The weight of a shot ----------------

        [UnityTest]
        // Work order 082 (review 6, finding 29): renamed from ..._AndNoneLeftAfterTheSceneGoes: no light is left after a load by
        // the light's own 0.05 s life (the wait below is 0.1 s), not by anything the scene does; the check stays, the name no
        // longer claims it.
        public IEnumerator TheMuzzleLight_IsOnForItsTime_ThenOff_NeverMoreThanItsMost()
        {
            _weight = FiringWeight.Ensure();
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            float firedAt = Time.unscaledTime;
            Assert.That(_weight.LightsOn, Is.EqualTo(1), "a light at the muzzle");
            Light light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.name == "MuzzleLight" && l.enabled);
            Assert.That(light.type, Is.EqualTo(LightType.Point));
            Assert.That(light.color.r, Is.GreaterThan(light.color.b), "warm");
            yield return Until(() => _weight.LightsOn == 0, 1f);
            float on = Time.unscaledTime - firedAt;
            Debug.Log($"[064] the muzzle light was on {on:0.000} s (real time)");
            Assert.That(on, Is.EqualTo(_weight.Values.LightSeconds).Within(0.03f), "about 0.05 s");
            for (int i = 0; i < 10; i++) _weight.Fired(_player.Position + Vector3.up, Vector3.forward, ShotWeight.Heavy);
            Assert.That(_weight.LightsMade, Is.LessThanOrEqualTo(_weight.Values.MaxLights), "pooled");
            Assert.That(_weight.LightsOn, Is.EqualTo(_weight.Values.MaxLights), "never more than four alive");
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.name == "MuzzleLight" && l.enabled), Is.EqualTo(0), "none left alive");
        }

        [UnityTest]
        public IEnumerator TheCameraKick_ComesBackInItsTime_NeverAddsUpOverTwentyShots_AndTheCameraStaysAtTheRigsPlace()
        {
            _weight = FiringWeight.Ensure();
            Camera cam = Camera.main;
            Assert.That(cam != null && cam.GetComponent<CameraRig>() != null, Is.True);
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            float firedAt = Time.unscaledTime;
            yield return null; // a frame on: the kick has begun
            Assert.That(_weight.KickOffset.magnitude, Is.GreaterThan(0f), "it kicks");
            Vector3 dir = (_a.transform.position - _player.Position);
            dir.y = 0f;
            Assert.That(Vector3.Dot(_weight.KickOffset, dir), Is.LessThanOrEqualTo(0f), "back along the shot's line");
            yield return Until(() => _weight.KickOffset == Vector3.zero, 1f);
            Assert.That(Time.unscaledTime - firedAt, Is.LessThanOrEqualTo(_weight.Values.KickSeconds + 0.03f), "back where it was in about 0.08 s");

            var measured = new Measured();
            yield return TwentyShots(cam, measured);
            Debug.Log($"[064] twenty basic shots: the kick at most {measured.Most:0.0000} m, the camera off the rig's place at most {measured.Drift:0.0000} m");
            Assert.That(measured.Most, Is.LessThanOrEqualTo(_weight.Values.BasicKick + 1e-4f), "never added up");
            Assert.That(measured.Most, Is.GreaterThan(0f), "it kicked");
            Assert.That(measured.Drift, Is.LessThan(1e-4f), "outside its drawing the camera is where the rig put it: the cursor's ray never moves");
        }

        /// <summary>
        /// Work order 082 (review 6, finding 9): the camera itself while the kick is on it. The two tests above read the kick's
        /// own sum (KickOffset) and the camera between frames, where it is always back; the camera is moved only inside the
        /// render pipeline's beginCameraRendering and put back in endCameraRendering, which the windowless batch run never
        /// calls (work order 082's probe), and Camera.Render() there crashes Unity. So FiringWeight's two hooks are called here
        /// as the pipeline calls them: while it draws the camera stands at the rig's place plus the kick, and after it the
        /// rig's place again, so the cursor's ray never moves.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCameraKick_IsOnTheCameraWhileItDraws_AndTheRigsPlaceAfter()
        {
            _weight = FiringWeight.Ensure();
            Camera cam = Camera.main;
            Assert.That(cam != null && cam.GetComponent<CameraRig>() != null, Is.True, "the rig's camera");
            const System.Reflection.BindingFlags Own = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.MethodInfo begin = typeof(FiringWeight).GetMethod("OnBeginCamera", Own), end = typeof(FiringWeight).GetMethod("OnEndCamera", Own);
            Assert.That(begin != null && end != null, Is.True, "FiringWeight's two camera hooks");
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return null; // a frame on: the kick has begun
            Vector3 kick = _weight.KickOffset;
            Assert.That(kick.magnitude, Is.GreaterThan(0f), "a kick runs");
            Vector3 rig = cam.transform.position;
            var context = default(UnityEngine.Rendering.ScriptableRenderContext);
            begin.Invoke(_weight, new object[] { context, cam });
            Vector3 drawn = cam.transform.position;
            end.Invoke(_weight, new object[] { context, cam });
            Vector3 after = cam.transform.position;
            Assert.That(Vector3.Distance(drawn, rig + kick), Is.LessThan(1e-4f), "while it draws: the rig's place plus the kick (" + kick.magnitude.ToString("0.0000") + " m)");
            Assert.That(Vector3.Distance(after, rig), Is.LessThan(1e-5f), "after it drew: the rig's place");
        }

        [UnityTest]
        public IEnumerator WithTheKicksSettingOff_TheCameraNeverMovesFromTheRigsPlace()
        {
            _weight = FiringWeight.Ensure();
            GameSettings.SetCameraKick(false, keep: false);
            Camera cam = Camera.main;
            _player.Teleport(NearA);
            yield return null;
            var measured = new Measured();
            yield return TwentyShots(cam, measured);
            Assert.That(measured.Most, Is.EqualTo(0f), "no kick at all");
            Assert.That(measured.Drift, Is.LessThan(1e-4f), "the rig's place, every frame");
        }

        sealed class Measured
        {
            public float Most, Drift;
        }

        /// <summary>Twenty basic shots at the fire rate: the most kick, and the most the camera stood off the rig's place in a frame.</summary>
        IEnumerator TwentyShots(Camera cam, Measured measured)
        {
            CameraRig rig = cam.GetComponent<CameraRig>();
            FeelValues feel = rig.Settings != null ? rig.Settings.Values : new FeelValues();
            int start = _player.BasicShotsFired;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_player.BasicShotsFired < start + 20 && Time.realtimeSinceStartup < deadline)
            {
                if (!_player.HasPendingShot) _player.Shoot(_a.transform.position);
                yield return null;
                measured.Most = Mathf.Max(measured.Most, _weight.KickOffset.magnitude);
                Vector3 t = rig.Target != null ? rig.Target.position : _player.Position;
                Vector3 place = new Vector3(t.x, 0f, t.z) - Quaternion.Euler(feel.CameraPitch, 0f, 0f) * Vector3.forward * feel.CameraDistance;
                measured.Drift = Mathf.Max(measured.Drift, Vector3.Distance(cam.transform.position, place));
            }
            Assert.That(_player.BasicShotsFired - start, Is.GreaterThanOrEqualTo(20), "twenty shots");
        }

        [UnityTest]
        public IEnumerator InATestRun_AsOnAPilotsFlight_NoShotHasWeight_AndSwitchingTheEffectsOffTurnsItOff()
        {
            // The flights switch the effects off by never installing them (they install themselves only outside a test run).
            Assert.That(FiringWeight.Current, Is.Null, "not installed in a test run");
            Assert.That(FiringWeight.Running, Is.Null);
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.name == "MuzzleLight"), Is.EqualTo(0), "no muzzle light");
            _weight = FiringWeight.Ensure();
            Assert.That(FiringWeight.Running, Is.SameAs(_weight));
            CombatEffects effects = CombatEffects.Ensure();
            _made.Add(effects.gameObject);
            effects.Values.On = false;
            Assert.That(FiringWeight.Running, Is.Null, "the effects switched off: no weight either");
            effects.Values.On = true;
            _weight.Values.On = false;
            Assert.That(FiringWeight.Running, Is.Null, "its own switch off");
            Assert.That(FiringWeightRules.HitStop(ShotWeight.Heavy, 0.05f, FiringWeight.ValuesNow), Is.EqualTo(0.05f), "the hit stop as before");
        }

        // ---------------- 4. The numbers ----------------

        [UnityTest]
        public IEnumerator AHit_GivesOneNumberOfItsKind_ABlastsNumbersAreSpread_AndATickAllocatesNothing()
        {
            _feedback = TimingFeedback.Ensure();
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotHits >= 1, 2f);
            yield return null;
            List<FeedbackShown> numbers = _feedback.Numbers.Where(n => n.Target == _a).ToList();
            Assert.That(numbers.Count, Is.EqualTo(1), "one number");
            Assert.That(numbers[0].Kind, Is.EqualTo(NumberKind.Plain), "an ordinary hit");
            Assert.That(numbers[0].Life, Is.EqualTo(_feedback.NumberLook.PlainSeconds), "its life");
            yield return new WaitForSeconds(0.7f);

            // Two more targets beside A: one Scatter Blast hits all three in one frame.
            for (int i = 0; i < 2; i++)
            {
                GameObject copy = Object.Instantiate(_a.gameObject, _a.transform.position + new Vector3(i == 0 ? 0.6f : -0.5f, 0f, i == 0 ? -0.4f : 0.4f), Quaternion.identity);
                _made.Add(copy);
            }
            yield return null;
            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _feedback.Numbers.Count >= 3, 2f);
            List<FeedbackShown> blast = _feedback.Numbers.ToList();
            Debug.Log("[064] a blast's numbers: " + string.Join(", ", blast.Select(n => n.Sum + " in lane " + n.Lane)));
            Assert.That(blast.Count, Is.EqualTo(3), "one number on each");
            Assert.That(blast.Select(n => n.Lane).Distinct().Count(), Is.EqualTo(3), "each in its own lane: not piled on one point");
            Assert.That(blast.Select(n => n.Target).Distinct().Count(), Is.EqualTo(3));

            NUnit.Framework.Constraints.IResolveConstraint notAllocating = AllocExt.AllocatingGCMemory(Is.Not);
            Assert.That(() => { for (int i = 0; i < 300; i++) _feedback.Tick(); }, notAllocating, "numbers showing: a frame allocates nothing");
        }

        // ---------------- 5. The basic shot's variations ----------------

        [UnityTest]
        public IEnumerator FourBasicShotsInARow_AreNeverTheSameVariationTwiceRunning_AndQuieter()
        {
            Assert.That(_player.Sounds != null && _player.ShotLooks != null && _player.ShotLooks.Basic.Sound != null, Is.True, "the basic shot has a sound");
            _player.SeedShotVariations(64UL);
            _player.Teleport(NearA);
            yield return null;
            int start = _player.BasicShotsFired;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_player.BasicShotsFired < start + 4 && Time.realtimeSinceStartup < deadline)
            {
                if (!_player.HasPendingShot) _player.Shoot(_a.transform.position);
                yield return null;
            }
            List<ShotSoundRequest> basic = _sounds.Where(s => s.Variation >= 0).ToList();
            Debug.Log("[064] four basic shots: variations " + string.Join(", ", basic.Select(s => s.Variation + " (" + s.Cutoff + " Hz, tail " + s.Tail + ")")));
            Assert.That(basic.Count, Is.EqualTo(4), "four sounds, each a variation");
            for (int i = 1; i < basic.Count; i++) Assert.That(basic[i].Variation, Is.Not.EqualTo(basic[i - 1].Variation), "never the same twice running");
            ShotLook look = _player.ShotLooks.Basic;
            float morning = look.SoundVolume * _player.ShotLooks.SoundVolume;
            foreach (ShotSoundRequest s in basic)
            {
                Assert.That(s.Clip, Is.SameAs(look.Sound), "the recording, no clips of its own in Resources");
                Assert.That(s.Volume, Is.LessThan(morning * ShotVariationPicker.DbToGain(-1.4f)), "quieter than this morning by about 3 dB");
            }
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
