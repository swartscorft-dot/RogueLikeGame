using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Work order 143's second round, the numbers left out: the benchmark's floor 5 fight (<see cref="BenchmarkScenario.Floor5Combat"/>,
    /// its fullest combat room, every enemy awake, none of them able to die) played by a test's bot in Off and the three modes,
    /// standing, running across her target with the button held, and counter-strafing (a short run across, the opposite key until
    /// she is all but accurate, the keys let go and a shot, the next run the other way): damage a second, the share of basic shots
    /// that hit, and damage taken a second. Her health is made too large to end the fight, so what she takes is counted (the
    /// benchmark makes her untouchable). The bot aims at the nearest enemy, as the benchmark's does. Written to the test's output.
    /// </summary>
    public class PrecisionFloorFightPlayTests
    {
        const float Seconds = 6f;          // s of game time a style is played
        const float Leg = 0.35f;           // s of a run between counter-strafes
        const float RunLeg = 0.6f;         // s of a run before it turns, running with the button held
        const float AccurateEnough = 0.5f; // degrees: the bot lets go of the keys once the cone is under this
        const int HerHealth = 10000000;    // too much to die: what she takes is counted

        PlayerCharacter _p;
        PlayerHealth _health;
        BenchmarkStage _stage;
        int _dealt, _taken, _herMaxBefore;
        readonly List<Damageable> _bodies = new List<Damageable>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            PrecisionSettings.ForTests = null;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _p = Object.FindAnyObjectByType<PlayerCharacter>();
            _health = _p.GetComponent<PlayerHealth>();
            _herMaxBefore = _health.MaxHealth;
            _health.Damaged += Taken;
            GeneratedLevels.Ensure();
        }

        [TearDown]
        public void LetGo()
        {
            Unlisten();
            if (_health != null)
            {
                _health.Damaged -= Taken;
                _health.MaxHealth = _herMaxBefore;
            }
            _stage?.End();
            _stage = null;
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
            Time.timeScale = 1f;
            PrecisionSettings.ForTests = null;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Taken(int amount) => _taken += amount;
        void Dealt(int amount) => _dealt += amount;

        void Unlisten()
        {
            foreach (Damageable d in _bodies) if (d != null) d.Damaged -= Dealt;
            _bodies.Clear();
        }

        enum Style { Standing, Running, CounterStrafing }

        [UnityTest]
        public IEnumerator Floor5Fight_EachModeAndStyle_DamageASecond_HitShare_DamageTaken()
        {
            var sb = new StringBuilder();
            sb.AppendLine("floor 5's fight (benchmark), " + Seconds.ToString("0", CultureInfo.InvariantCulture) + " s each: damage a second, hits of shots, damage taken a second | standing | running, held | counter-strafing");
            foreach (PrecisionMode mode in new[] { PrecisionMode.Off, PrecisionMode.Inertia, PrecisionMode.Meter, PrecisionMode.Ghost })
            {
                sb.Append(mode);
                foreach (Style style in new[] { Style.Standing, Style.Running, Style.CounterStrafing })
                {
                    PrecisionSettings.ForTests = mode;
                    _p.SeedPrecision(143u);
                    _stage?.End();
                    _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
                    Assert.That(_stage, Is.Not.Null);
                    Assert.That(_stage.Level, Is.Not.Null, "floor 5 built");
                    PlayerInputDriver driver = _p.GetComponent<PlayerInputDriver>();
                    if (driver != null) driver.InputSource = () => null; // the bot's intents, not the benchmark's mouse
                    _health.MaxHealth = HerHealth;
                    _health.RestoreFull();
                    yield return null;
                    _stage.Keep();
                    Unlisten();
                    foreach (Enemy e in _stage.Fighting)
                        if (e != null && e.Body != null) { e.Body.Damaged += Dealt; _bodies.Add(e.Body); }
                    int fired = _p.BasicShotsFired, hits = _p.BasicShotHits;
                    _dealt = _taken = 0;
                    float t0 = Time.time;
                    yield return Play(style);
                    float seconds = Mathf.Max(0.1f, Time.time - t0);
                    int f = _p.BasicShotsFired - fired, h = _p.BasicShotHits - hits;
                    sb.Append(" | " + (_dealt / seconds).ToString("0", CultureInfo.InvariantCulture) + ", "
                              + (f > 0 ? (100f * h / f).ToString("0", CultureInfo.InvariantCulture) + " % of " + f : "no shot") + ", "
                              + (_taken / seconds).ToString("0.0", CultureInfo.InvariantCulture));
                    Assert.That(f, Is.GreaterThan(3), mode + " " + style + ": shots fired");
                }
                sb.AppendLine();
            }
            TestContext.WriteLine(sb.ToString());
            Debug.Log("PrecisionFloorFightPlayTests:\n" + sb);
        }

        IEnumerator Play(Style style)
        {
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 120f;
            float side = 1f, legAt = t0;
            bool countered = false;
            int firedAtCounter = 0;
            while (Time.time - t0 < Seconds && Time.realtimeSinceStartup < deadline)
            {
                _stage.Keep();
                _health.Untouchable = false; // the benchmark makes her untouchable each frame: here she is hurt, and too strong to die
                Enemy target = _stage.Target();
                Vector3 at = target != null ? target.transform.position : _p.Position + Vector3.forward;
                Vector3 to = new Vector3(at.x - _p.Position.x, 0f, at.z - _p.Position.z);
                Vector3 across = to.sqrMagnitude > 1e-4f ? new Vector3(to.z, 0f, -to.x).normalized : Vector3.right;
                _p.AimAt(at);
                switch (style)
                {
                    case Style.Standing:
                        _p.Steer(Vector3.zero);
                        if (target != null) _p.Shoot(at);
                        break;
                    case Style.Running:
                        if (Time.time - legAt >= RunLeg) { side = -side; legAt = Time.time; }
                        _p.Steer(across * side);
                        if (target != null) _p.Shoot(at);
                        break;
                    default:
                        if (!countered)
                        {
                            _p.Steer(across * side);
                            if (Time.time - legAt >= Leg)
                            {
                                side = -side; // the opposite key: the counter-strafe
                                countered = true;
                                firedAtCounter = _p.BasicShotsFired;
                                legAt = Time.time;
                                _p.Steer(across * side);
                            }
                        }
                        else
                        {
                            bool accurate = _p.PrecisionCone < AccurateEnough || Time.time - legAt > 0.25f;
                            bool fired = _p.BasicShotsFired > firedAtCounter;
                            _p.Steer(fired ? across * side : accurate ? Vector3.zero : across * side);
                            if (accurate && !fired && target != null) _p.Shoot(at);
                            if (fired || Time.time - legAt > 1f)
                            {
                                countered = false;
                                legAt = Time.time;
                            }
                        }
                        break;
                }
                yield return null;
            }
            _p.Steer(Vector3.zero);
        }
    }
}
