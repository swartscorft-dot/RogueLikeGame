using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 154: two rules of the game found to change with the frame's length while the tests that failed under load were
    /// read, measured at fixed frame steps of game time (1/30, 1/60 and 1/144 s, 0.2 and 0.33 s: a loaded machine's) and written
    /// down as a table ("[154] finding"). Proof only: the order changes no game rule for them. Each test asserts only that what it
    /// measures happened (shots fired, a pack round her); the numbers are the finding.
    /// - The basic shot held down (a click every frame): the next shot starts on the first frame after the cycle, so the time from
    ///   shot to shot is the cycle rounded up to whole frames (ClickToShootTests' subject).
    /// - A swarmer pack round her: each swarmer is pushed apart from the others once a frame by half of what they overlap, so how
    ///   close two stand depends on how far they walk in a frame (EnemyKindsTests.SwarmerPack's "they do not stack").
    /// Nobody has played it.
    /// </summary>
    public class FrameStepFindingsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);  // A is 2.3 m away here (FocusFeltTests')
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);  // EnemyKindsTests' place for the pack
        static readonly float[] Steps = { 1f / 30f, 1f / 60f, 1f / 144f, 0.2f, 0.33f };
        /// <summary>The real seconds one measurement may take at most, however busy the machine is.</summary>
        const float RealLimit = 300f;
        const float ShootingSeconds = 3f;   // s of game time of held fire at each step
        const float PackSeconds = 6f;       // s of game time the pack stands round her at each step

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false; // as EnemyKindsTests: the attacks as they were
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false;
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator ForGameSeconds(float seconds, Action eachFrame)
        {
            float end = Time.time + seconds, deadline = Time.realtimeSinceStartup + RealLimit;
            while (Time.time < end && Time.realtimeSinceStartup < deadline)
            {
                eachFrame?.Invoke();
                yield return null;
            }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Finding_TheHeldBasicShot_FiresOnWholeFrames_ItsCycleRoundedUp()
        {
            FeelValues f = _player.Feel;
            float cycle = f.BasicShotStartup + f.BasicShotActive + f.BasicShotRecovery;
            Damageable a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            a.SetMaxHealth(10000000);
            var starts = new List<float>();
            void OnReport(PlayerReport r) { if (r.Kind == PlayerReportKind.ShotStarted) starts.Add(Time.time); }
            _player.Reported += OnReport;
            var table = new StringBuilder("[154] finding: the basic shot held down (a click every frame), its cycle " + cycle.ToString("0.000", Inv) + " s\n");
            try
            {
                foreach (float step in Steps)
                {
                    Time.captureDeltaTime = 1f / 60f;
                    _player.StartFresh();
                    _player.Teleport(NearA);
                    yield return null;
                    yield return null;
                    starts.Clear();
                    Time.captureDeltaTime = step;
                    yield return ForGameSeconds(ShootingSeconds, () => _player.Shoot(a.transform.position));
                    _player.Stop();
                    Time.captureDeltaTime = 1f / 60f;
                    yield return ForGameSeconds(0.5f, null);
                    var gaps = new List<float>();
                    for (int i = 1; i < starts.Count; i++) gaps.Add(starts[i] - starts[i - 1]);
                    float mean = gaps.Count > 0 ? gaps.Average() : 0f, least = gaps.Count > 0 ? gaps.Min() : 0f;
                    float rounded = Mathf.Ceil(cycle / step - 1e-4f) * step;
                    table.AppendLine(string.Format(Inv, "[154]   frames of {0:0.0000} s: {1} shots in {2:0.0} s, shot to shot {3:0.0000} s on average (least {4:0.0000}), the cycle rounded up to whole frames {5:0.0000}; {6:0.00} shots a second for the cycle's {7:0.00}",
                                                   step, starts.Count, ShootingSeconds, mean, least, rounded, mean > 0f ? 1f / mean : 0f, 1f / cycle));
                    Assert.That(starts.Count, Is.GreaterThanOrEqualTo(2), "shots fired at frames of " + step);
                }
            }
            finally
            {
                _player.Reported -= OnReport;
                Time.captureDeltaTime = 0f;
            }
            TestContext.WriteLine(table.ToString());
            Debug.Log(table.ToString());
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [UnityTest, Timeout(600000)]
        public IEnumerator Finding_ASwarmerPack_StandsCloserTogether_WithLongerFrames()
        {
            _levels = GeneratedLevels.Ensure();
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            if (practice != null) practice.enabled = false;
            var values = new SwarmerValues();
            var table = new StringBuilder("[154] finding: a swarmer pack round her standing still, the closest two within 2 m of her (spacing " + values.Spacing.ToString("0.00", Inv) + " m)\n");
            try
            {
                foreach (float step in Steps)
                {
                    Time.captureDeltaTime = 1f / 60f;
                    _levels.ClearSpawned();
                    _player.StartFresh();
                    _player.Teleport(Stand);
                    yield return null;
                    List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
                    yield return null;
                    pack[0].Body.TakeHit(1); // the whole pack wakes with it
                    Time.captureDeltaTime = step;
                    float closest = float.MaxValue;
                    int framesRound = 0;
                    yield return ForGameSeconds(PackSeconds, () =>
                    {
                        List<Enemy> near = pack.Where(e => e != null && !e.IsDead && Flat(e.transform.position, _player.Position) < 2f).ToList();
                        if (near.Count >= 2) framesRound++;
                        for (int i = 0; i < near.Count; i++)
                            for (int j = i + 1; j < near.Count; j++)
                                closest = Mathf.Min(closest, Flat(near[i].transform.position, near[j].transform.position));
                    });
                    Time.captureDeltaTime = 1f / 60f;
                    table.AppendLine(string.Format(Inv, "[154]   frames of {0:0.0000} s: the closest two {1} m apart ({2} frames with two or more round her){3}",
                                                   step, closest == float.MaxValue ? "-" : closest.ToString("0.00", Inv), framesRound,
                                                   closest < values.Spacing - 0.1f ? ", under EnemyKindsTests' spacing less 0.1" : ""));
                    Assert.That(pack.Count, Is.EqualTo(values.PracticePack), "a pack at frames of " + step);
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
            TestContext.WriteLine(table.ToString());
            Debug.Log(table.ToString());
        }
    }
}
