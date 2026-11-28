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
    /// Work order 071 (Kristian, 2026-11-21: "så må vi reworke focus, det føles ræva ... føler man må få mere attackdmg,
    /// attackspeed, cooldown osv."), in the ControlRoom scene through the intents: fill the gauge, press Z, and her next
    /// shots deal the gain, the time between basic shots is shorter by the gain, a running cooldown ends sooner by the gain,
    /// she runs faster; when the gains end everything is back. Hair Trigger's part gives them for its seconds without a
    /// window. Then how it is felt (<see cref="FocusFeel"/>, <see cref="FocusEdge"/>): the ready glow while full and
    /// unspent, the burst with its freeze, kick and sound at the moment of spending, the aura, the light and the edge while
    /// it lasts, the flicker and the falling sound in the last second, the teal and larger shots, and nothing left behind
    /// after; nothing allocated per frame. Nobody has seen or heard any of it: these are counts, sizes and times.
    ///
    /// Waits are in game or real time with a limit, never frames of a supposed length (frames without a window are about
    /// 0.3 ms long). The settings asset is never written: the shorter seconds a test needs are set on the character's own
    /// focus values (<see cref="PlayerCharacter.FocusModeValues"/>, made new with each scene).
    /// </summary>
    public class FocusFeltTests
    {
        const int Quick = 0;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here
        static readonly Vector3 RunFrom = new Vector3(-6f, 0f, 0f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Damageable _a;
        FocusFeel _feel;
        CombatEffects _fx;
        FiringWeight _weight;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<float> _reportTimes = new List<float>();
        readonly List<FocusFeelSoundRequest> _heard = new List<FocusFeelSoundRequest>();
        readonly List<float> _flashSizes = new List<float>();

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            FocusFeel.Requested += OnSound;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false;
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _player.Reported += OnReport;
            _reports.Clear();
            _reportTimes.Clear();
            _heard.Clear();
            _flashSizes.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            FocusFeel.Requested -= OnSound;
            if (_player != null) _player.Reported -= OnReport;
            if (_feel != null) Object.Destroy(_feel.gameObject);
            _feel = null;
            if (_fx != null) Object.Destroy(_fx.gameObject);
            _fx = null;
            if (_weight != null) Object.Destroy(_weight.gameObject);
            _weight = null;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnSound(FocusFeelSoundRequest r) => _heard.Add(r);

        void OnReport(PlayerReport r)
        {
            _reports.Add(r);
            _reportTimes.Add(Time.time);
            // The basic shot's muzzle flash is made just before its ShotFired report, in the same frame: read its size now.
            if (r.Kind == PlayerReportKind.ShotFired)
            {
                float newest = 0f, size = 0f;
                foreach (ShotFx fx in Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None))
                    if (fx.Kind == ShotFxKind.Flash && fx.Age <= 1e-4f && fx.Size > newest) { newest = fx.Size; size = fx.Size; }
                if (size > 0f) _flashSizes.Add(size);
            }
        }

        void Install()
        {
            _fx = CombatEffects.Ensure();
            _feel = FocusFeel.Ensure();
        }

        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() => Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f);

        /// <summary>
        /// Work order 154: s of game time a frame where a test's subject is a rule in game time (Time.captureDeltaTime): the
        /// machine's own frames were up to a third of a second long beside another run, and 0.3 ms alone.
        /// </summary>
        const float Step = 1f / 60f;
        /// <summary>Work order 154: the real seconds a wait in game time may take at most, however busy the machine is.</summary>
        const float RealLimit = 60f;

        /// <summary>Work order 154: until the condition holds, at most this many seconds of game time (and <see cref="RealLimit"/> real).</summary>
        static IEnumerator UntilInGameTime(Func<bool> condition, float gameSeconds)
        {
            float end = Time.time + gameSeconds, deadline = Time.realtimeSinceStartup + RealLimit;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Basic shots at A for this many seconds of game time, a click every frame: the times they started.</summary>
        IEnumerator ShootFor(float seconds, List<float> starts)
        {
            int from = _reports.Count;
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + RealLimit; // work order 154: game time is the measure, real time only the limit
            while (Time.time < until && Time.realtimeSinceStartup < deadline)
            {
                _player.Shoot(_a.transform.position);
                yield return null;
            }
            _player.Stop();
            for (int i = from; i < _reports.Count; i++)
                if (_reports[i].Kind == PlayerReportKind.ShotStarted) starts.Add(_reportTimes[i]);
        }

        static float MedianGap(List<float> starts)
        {
            var gaps = new List<float>();
            for (int i = 1; i < starts.Count; i++) gaps.Add(starts[i] - starts[i - 1]);
            gaps.Sort();
            return gaps.Count == 0 ? 0f : gaps[gaps.Count / 2];
        }

        // ---------------- The gains ----------------

        [UnityTest]
        public IEnumerator Spending_HerSkillAndHerBasicShot_DealTheGain_AndAfterTheWindow_AsBefore()
        {
            IdentityValues id = _player.Identity;
            SkillDefinition quick = _player.Skills[Quick];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.FocusDamageMultiplier, Is.EqualTo(1f), "before");
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsFocusBoosted, Is.True);
            Assert.That(_player.FocusDamageMultiplier, Is.EqualTo(id.EmpoweredDamage * id.EmpoweredDamageScale).Within(1e-5f));

            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(Lost(_a), Is.EqualTo(Mathf.RoundToInt(quick.Damage * 1.35f)), "Quick Shot's 100: 135");
            yield return UntilFree();
            int before = Lost(_a), hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(Lost(_a) - before, Is.EqualTo(Mathf.RoundToInt(Placeholders.BasicShotDamage * 1.35f)), "the basic shot 35 % more");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.Hit).Empowered, Is.True, "reported as empowered");

            yield return Until(() => !_player.IsFocusBoosted, id.EmpoweredDuration + 3f);
            Assert.That(_player.IsFocusBoosted, Is.False);
            yield return UntilFree();
            before = Lost(_a);
            hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(Lost(_a) - before, Is.EqualTo(Placeholders.BasicShotDamage), "after: as before");
            Assert.That(_player.FocusDamageMultiplier, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Spending_TheTimeBetweenBasicShots_IsShorterByTheGain_AndBackAfter()
        {
            FeelValues f = _player.Feel;
            float plain = f.BasicShotStartup + f.BasicShotActive + f.BasicShotRecovery;
            // Work order 154: every frame 1/60 s of game time: a shot starts on a frame, and with frames of 40 ms (LoadOnPurpose) shot to
            // shot was 0.249 s for the 0.225 of the feel; at a sixtieth it is the cycle rounded up to whole frames (FrameStepFindingsTests).
            Time.captureDeltaTime = Step;
            _player.FocusModeValues.PartBoostSeconds = 2f;
            Assert.That(_player.SetPracticeFocusMode(FocusMode.HairTrigger), Is.True);
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.BasicShotInterval, Is.EqualTo(plain).Within(1e-5f), "0.225 s in the practice room");
            var before = new List<float>();
            yield return ShootFor(1.2f, before);
            Assert.That(MedianGap(before), Is.EqualTo(plain).Within(0.02f), "shot to shot as the feel has it");

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "a Hair Trigger part");
            Assert.That(_player.IsEmpowered, Is.False, "no window");
            Assert.That(_player.IsFocusBoosted, Is.True, "but the gains");
            Assert.That(_player.BasicShotInterval, Is.EqualTo(plain / _player.Identity.EmpoweredFireRate).Within(1e-4f), "0.161 s");
            var during = new List<float>();
            yield return ShootFor(1.5f, during);
            Assert.That(during.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(MedianGap(during), Is.EqualTo(plain / 1.4f).Within(0.02f), "40 % faster, as fired");
            Assert.That(MedianGap(during), Is.LessThan(MedianGap(before) - 0.04f));

            yield return UntilInGameTime(() => !_player.IsFocusBoosted, 3f);
            Assert.That(_player.BasicShotInterval, Is.EqualTo(plain).Within(1e-5f), "back after its two seconds");
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator Spending_ARunningCooldown_IsCut_ThenRunsSixtyPercentFaster_AndPlainAfter()
        {
            IdentityValues id = _player.Identity;
            // Work order 154: every frame 1/60 s of game time, so the frame it is seen ready is the rule's to a sixtieth (beside a full
            // run a frame carried up to a third of a second). The fault it met in every full run (0.84 to 0.88 for 1.03) was the
            // game's, repaired by ExactCooldowns: its 0.3 ms frames late in a long run rounded every frame's cut to the clock's step.
            Time.captureDeltaTime = Step;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return UntilFree();
            float left = _player.SkillCooldownRemaining(Quick);
            Assume.That(left, Is.GreaterThan(1.5f), "Quick Shot's 3 s running");

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            float spentAt = Time.time;
            float afterCut = _player.SkillCooldownRemaining(Quick);
            Assert.That(afterCut, Is.EqualTo(left * (1f - id.CooldownCut)).Within(1e-3f), "the cut at the moment of spending stays");
            Assert.That(_player.FocusCooldownRate(Quick), Is.EqualTo(1.6f).Within(1e-5f));
            yield return UntilInGameTime(() => _player.SkillCooldownRemaining(Quick) <= 0f, 4f);
            float took = Time.time - spentAt;
            Assert.That(took, Is.EqualTo(afterCut / 1.6f).Within(0.05f), "what was left, 1.6 times as fast");

            // After the window a cooldown runs in plain time again.
            yield return UntilInGameTime(() => !_player.IsFocusBoosted, id.EmpoweredDuration + 2f);
            Assert.That(_player.FocusCooldownRate(Quick), Is.EqualTo(1f));
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            float startedAt = Time.time;
            yield return UntilFree();
            yield return UntilInGameTime(() => _player.SkillCooldownRemaining(Quick) <= 0f, 5f);
            Assert.That(Time.time - startedAt, Is.EqualTo(_player.CooldownOf(Quick)).Within(0.05f), "its own 3 s");
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator Spending_SheRunsTenPercentFaster_AndAtHerSpeedAfter()
        {
            FeelValues f = _player.Feel;
            _player.FocusModeValues.PartBoostSeconds = 1.5f;
            Assert.That(_player.SetPracticeFocusMode(FocusMode.HairTrigger), Is.True);
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right); // held, as a key
            yield return Until(() => _player.Position.x >= -4f, 3f);
            Assume.That(_player.Speed, Is.EqualTo(f.RunSpeed).Within(0.4f), "at full speed");
            Assert.That(_player.SpeedLimit, Is.EqualTo(f.RunSpeed).Within(1e-3f));

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.SpeedLimit, Is.EqualTo(f.RunSpeed * 1.1f).Within(1e-3f), "6.6 m a second");
            Assert.That(_player.Speed, Is.EqualTo(f.RunSpeed * 1.1f).Within(0.4f), "and running at it");
            Assert.That(_player.RunSpeedShare, Is.EqualTo(1.1f).Within(1e-5f));

            yield return Until(() => !_player.IsFocusBoosted, 3f);
            yield return null;
            Assert.That(_player.SpeedLimit, Is.EqualTo(f.RunSpeed).Within(1e-3f), "back after");
            Assert.That(_player.RunSpeedShare, Is.EqualTo(1f));
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator HairTrigger_APartsGains_RunTwoSeconds_ThreePartsSix_AndBasicShotsDoNotMark()
        {
            Assert.That(_player.SetPracticeFocusMode(FocusMode.HairTrigger), Is.True);
            _player.Teleport(NearA);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.FocusBoostRemaining, Is.EqualTo(_player.FocusModeValues.PartBoostSeconds).Within(1e-3f), "two seconds");
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.FocusBoostRemaining, Is.EqualTo(3f * _player.FocusModeValues.PartBoostSeconds).Within(0.02f), "three parts: six");
            Assert.That(_player.IsEmpowered, Is.False, "and still no window");
            int marks = _a.MarkCount, hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(_a.MarkCount, Is.EqualTo(marks), "a basic shot hit does not mark in a part's gains: Hair Trigger keeps its own way");
        }

        // ---------------- Felt: ready ----------------

        [UnityTest]
        public IEnumerator TheReadyGlow_IsOnWhileFullAndUnspent_WeakerThanTheAura_AndOffWhenSpent()
        {
            Install();
            _player.Teleport(NearA);
            yield return null;
            _feel.Tick();
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.None), "filling: nothing on her");
            Assert.That(_feel.LightOn, Is.False);

            _player.FillFocus();
            _feel.Tick();
            Assert.That(_player.IsFocusReady, Is.True);
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Ready));
            Assert.That(_feel.LightOn, Is.True, "a quiet teal glow on her");
            Assert.That(_feel.LightIntensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(_feel.Values.ReadyLight + 1e-4f));
            Assert.That(_feel.TealLight.color.g, Is.GreaterThan(_feel.TealLight.color.r * 2f), "teal");
            Assert.That(Vector3.Distance(_feel.TealLight.transform.position, _player.Position + Vector3.up * _feel.Values.AuraHeight), Is.LessThan(0.01f), "on her");
            Assert.That(_feel.AuraPieces, Is.EqualTo(0), "no aura for ready");
            Assert.That(_feel.EdgeAlpha, Is.EqualTo(0f), "no edge for ready");
            float ready = _feel.LightIntensity;

            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "the key stays his");
            _feel.Tick();
            Assert.That(_player.IsFocusReady, Is.False, "spent");
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Running), "the ready glow gives way to the aura");
            Assert.That(_feel.LightIntensity, Is.GreaterThan(ready * 2f), "brighter: the two told apart");
        }

        [UnityTest]
        public IEnumerator NothingSpendsItForHim_AFullGaugeStaysFull()
        {
            Install();
            _player.FillFocus();
            float until = Time.time + 1f;
            yield return Until(() => Time.time >= until, 3f);
            Assert.That(_player.IsFocusFull, Is.True);
            Assert.That(_player.IsFocusBoosted, Is.False);
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Ready));
        }

        // ---------------- Felt: spent, running, ending, gone ----------------

        // Work order 154: its freeze is a few hundredths of real time (HitStop, unscaled), read once a frame: real time is part of its
        // subject, and the category lets the runner run it alone (it held with frames of 40 ms).
        [UnityTest, Category("RealTime")]
        public IEnumerator TheMomentOfSpending_ABurstAFreezeAKickAndItsSound_ThenTheAuraTheLightAndTheEdge_ThenTheFlicker_ThenNothingLeft()
        {
            Install();
            _weight = FiringWeight.Ensure();
            _player.FocusModeValues.PartBoostSeconds = 1f;
            Assert.That(_player.SetPracticeFocusMode(FocusMode.HairTrigger), Is.True);
            _player.Teleport(NearA);
            _player.AimAt(_a.transform.position);
            yield return null;
            _feel.Tick();
            int rings = Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Count(f => f.Kind == ShotFxKind.Ring);
            int lightsBefore = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            int alive = CombatFx.Alive;

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "all three parts: 3 s of gains, an empty gauge");
            Assert.That(_feel.Bursts, Is.EqualTo(3));
            Assert.That(HitStop.IsActive, Is.True, "a short freeze");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Count(f => f.Kind == ShotFxKind.Ring) - rings, Is.EqualTo(6), "two rings of light a spend");
            ShotFx big = Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Where(f => f.Kind == ShotFxKind.Ring).OrderByDescending(f => f.Size).First();
            Assert.That(big.Size, Is.EqualTo(_feel.Values.BurstRadius * _feel.Values.PartShare).Within(1e-3f), "a part's: a share of a full gauge's 7 m");
            Assert.That(_heard.Count(s => s.Sound == FocusFeelSound.Spend), Is.EqualTo(3), "its sound");
            Assert.That(_heard.First().Pitch, Is.EqualTo(_feel.Values.PartPitch), "a part's a little higher");
            Assert.That(_heard.First().Volume, Is.GreaterThan(0f));
            float realAt = Time.realtimeSinceStartup;
            yield return Until(() => !HitStop.IsActive, 1f);
            Assert.That(Time.realtimeSinceStartup - realAt, Is.LessThan(0.2f), "a few hundredths");
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            _feel.Tick();
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Running));
            Assert.That(_feel.AuraPieces, Is.EqualTo(2), "the ring at her feet and the stars round her");
            Assert.That(_feel.AuraShown, Is.True);
            Assert.That(_feel.AuraRing.Piece.Follow, Is.SameAs(_player.transform), "following her");
            Assert.That(_feel.LightOn, Is.True);
            Assert.That(_feel.EdgeAlpha, Is.EqualTo(_feel.Values.EdgeStrength).Within(1e-5f), "the screen's edge");
            Assert.That(_feel.GetComponent<FocusEdge>().Alpha, Is.EqualTo(_feel.EdgeAlpha));
            Assert.That(FocusEdge.Depth, Is.GreaterThan(10), "under the enemy bars' 10, and so under everything");
            Assert.That(() => { for (int i = 0; i < 300; i++) _feel.Tick(); }, NotAllocating, "while it lasts a frame allocates nothing");
            Assert.That(() => { for (int i = 0; i < 300; i++) { _player.FocusCooldownRate(i % 9); _ = _player.BasicShotInterval; _ = _player.RunSpeedShare; _ = _player.FocusDamageMultiplier; } },
                        NotAllocating, "nor do the gains as she reads them each frame");

            // The last second: the flicker and the falling sound, once.
            yield return Until(() => _player.FocusBoostRemaining <= _feel.Values.EndingSeconds * 0.5f, 4f);
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Ending));
            Assert.That(_heard.Count(s => s.Sound == FocusFeelSound.Ending), Is.EqualTo(1));
            bool sawOff = false, sawOn = false;
            float end = Time.time + 0.4f;
            while (Time.time < end && _player.IsFocusBoosted)
            {
                _feel.Tick();
                if (_feel.AuraShown) sawOn = true; else sawOff = true;
                yield return null;
            }
            Assert.That(sawOn && sawOff, Is.True, "the aura flickers");

            yield return Until(() => !_player.IsFocusBoosted, 2f);
            yield return null;
            _feel.Tick();
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.None), "an empty gauge: nothing");
            Assert.That(_feel.AuraPieces, Is.EqualTo(0), "the aura is gone");
            Assert.That(Object.FindObjectsByType<CombatFx>(FindObjectsSortMode.None).Count(f => f != null && f.gameObject.name.StartsWith("FocusFeel_")), Is.EqualTo(0), "nothing left behind");
            Assert.That(_feel.LightOn, Is.False);
            Assert.That(_feel.EdgeAlpha, Is.EqualTo(0f));
            Assert.That(_heard.Count(s => s.Sound == FocusFeelSound.Ending), Is.EqualTo(1), "once");
            yield return new WaitForSeconds(_feel.Values.BurstSeconds + 0.1f);
            Assert.That(Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Count(f => f.Kind == ShotFxKind.Ring), Is.EqualTo(rings), "the bursts' rings are gone");
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length, Is.EqualTo(lightsBefore), "its one light kept, off: none made more");
            Assert.That(CombatFx.Alive, Is.LessThanOrEqualTo(alive), "no piece of it alive");
            Assert.That(() => { for (int i = 0; i < 300; i++) _feel.Tick(); }, NotAllocating, "and nothing allocated while nothing changes");
        }

        // Work order 154: the camera's kick runs on real time (FiringWeight, unscaled), and its peak is read once a frame: with frames of
        // 40 ms (LoadOnPurpose) the frames missed it (0.05 for 0.15). Real time is its subject: the category lets the runner run it alone.
        [UnityTest, Category("RealTime")]
        public IEnumerator AFullGaugesBurst_IsTheLargest_AndKicksTheCamera()
        {
            Install();
            _weight = FiringWeight.Ensure();
            _player.Teleport(NearA);
            _player.AimAt(_a.transform.position);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            ShotFx big = Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Where(f => f.Kind == ShotFxKind.Ring).OrderByDescending(f => f.Size).First();
            Assert.That(big.Size, Is.EqualTo(_feel.Values.BurstRadius).Within(1e-3f), "7 m of teal light running out from her");
            Assert.That(_heard.Single().Sound, Is.EqualTo(FocusFeelSound.Spend));
            Assert.That(_heard.Single().Pitch, Is.EqualTo(1f));
            float basic = _player.ShotLooks != null && _player.ShotLooks.Basic != null ? _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume : 1f;
            Assert.That(_heard.Single().Volume, Is.EqualTo(_feel.Values.SpendVolume * basic).Within(1e-5f), "its share of the basic shot's volume");
            Assert.That(_feel.Values.SpendVolume, Is.GreaterThan(new PlaceholderSoundValues().Volume(PlaceholderSound.FocusFull) * 2f), "louder than the full chime");
            float peak = 0f;
            float deadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (GameSettings.CameraKick) peak = Mathf.Max(peak, _weight.KickOffset.magnitude);
                yield return null;
            }
            if (GameSettings.CameraKick)
                Assert.That(peak, Is.EqualTo(Mathf.Min(_feel.Values.BurstKick, _weight.Values.MostKick)).Within(0.03f), "the camera's kick");
            _feel.Tick();
            Assert.That(_feel.Phase, Is.EqualTo(FocusFeelPhase.Running), "today's six seconds");
        }

        [UnityTest]
        public IEnumerator WhileItRuns_HerShotsAreTeal_AndLarger_ThanTheMorningsEmpoweredLook()
        {
            _player.Teleport(NearA);
            yield return null;
            int hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            yield return UntilFree();
            Assume.That(_flashSizes.Count, Is.EqualTo(1), "the plain shot's flash, read");
            float plain = _flashSizes[0];

            // Without the feel running: the asset's empowered look, as before the order (1.25).
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            yield return UntilFree();
            float morning = _player.ShotLooks != null ? _player.ShotLooks.EmpoweredScale : ShotFxSettings.StartEmpoweredScale;
            Assert.That(_flashSizes[1] / plain, Is.EqualTo(morning).Within(1e-3f));

            Install();
            Assert.That(FocusFeel.ShotLookNow, Is.Not.Null);
            hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(_flashSizes[2] / plain, Is.EqualTo(_feel.Values.ShotScale).Within(1e-3f), "1.45 times the plain flash");
            _feel.Values.On = false;
            Assert.That(FocusFeel.ShotLookNow, Is.Null, "off: the morning's look");
        }

        [UnityTest]
        public IEnumerator WithItsSwitchOff_NothingOfItShows_AndTheGainsStillRun()
        {
            Install();
            _feel.Values.On = false;
            _player.FillFocus();
            _feel.Tick();
            Assert.That(_feel.LightOn, Is.False);
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            yield return null;
            _feel.Tick();
            Assert.That(_feel.Bursts, Is.EqualTo(0));
            Assert.That(_feel.AuraPieces, Is.EqualTo(0));
            Assert.That(_feel.EdgeAlpha, Is.EqualTo(0f));
            Assert.That(_heard, Is.Empty);
            Assert.That(HitStop.IsActive, Is.False, "no freeze");
            Assert.That(_player.IsFocusBoosted, Is.True, "the gains are the rules', not the feel's");
        }
    }
}
