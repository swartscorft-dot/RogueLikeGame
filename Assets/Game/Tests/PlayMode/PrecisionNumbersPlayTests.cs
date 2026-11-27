using System;
using System.Collections;
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
    /// Work order 143's numbers for him, played by a test's bot in the control room: two targets 9 m away (the order's "room with
    /// two shooters at 9 m"; dummies, which do not shoot back), the bot standing, running across them with the button held, and
    /// counter-strafing (a short run, the opposite key, a shot as soon as she is accurate, the next run the other way), for Off and
    /// the three modes, stock grip: damage a second and the share of shots that hit, written to the test's output. The floor 5
    /// fight of the benchmark is not played here (see the order's report).
    /// </summary>
    public class PrecisionNumbersPlayTests
    {
        const float Seconds = 4f;     // s of game time a style is played
        const float Leg = 0.35f;      // s of a run between counter-strafes
        const float Edge = 4f;        // m either side of the middle she runs to
        const float AccurateEnough = 0.5f; // degrees: the bot lets go of the keys once the cone is under this

        PlayerCharacter _p;
        Damageable _a, _b, _c;
        int _damage;
        float _ra, _rb;
        const float EnemyRadius = 0.3f;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            PrecisionSettings.ForTests = null;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _p = Object.FindAnyObjectByType<PlayerCharacter>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            Place(_a, -1.5f, 9f);
            Place(_b, 1.5f, 9f);
            // Enemy-sized: an ordinary enemy's 0.3 m radius (the dummies' 0.5 would forgive a cone far more).
            _ra = _a.Radius;
            _rb = _b.Radius;
            _a.Radius = _b.Radius = EnemyRadius;
            Place(_c, 40f, -40f);
            _a.Damaged += Count;
            _b.Damaged += Count;
        }

        [TearDown]
        public void Restore()
        {
            if (_a != null) { _a.Damaged -= Count; _a.Radius = _ra; }
            if (_b != null) { _b.Damaged -= Count; _b.Radius = _rb; }
            Time.timeScale = 1f;
            PrecisionSettings.ForTests = null;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Count(int amount) => _damage += amount;

        enum Style { Standing, Running, CounterStrafing }

        [UnityTest]
        public IEnumerator TwoTargetsAt9m_EachModeAndStyle_DamageASecondAndHitShare()
        {
            var sb = new StringBuilder();
            sb.AppendLine("two targets at 9 m, " + Seconds.ToString("0", CultureInfo.InvariantCulture) + " s each | standing | running, held | counter-strafing");
            foreach (PrecisionMode mode in new[] { PrecisionMode.Off, PrecisionMode.Inertia, PrecisionMode.Meter, PrecisionMode.Ghost })
            {
                sb.Append(mode);
                foreach (Style style in new[] { Style.Standing, Style.Running, Style.CounterStrafing })
                {
                    PrecisionSettings.ForTests = mode;
                    _p.SeedPrecision(143u);
                    _p.Teleport(new Vector3(0f, 0f, 0f));
                    _a.Refill();
                    _b.Refill();
                    yield return null;
                    int fired = _p.BasicShotsFired, hits = _p.BasicShotHits;
                    _damage = 0;
                    yield return Play(style);
                    int f = _p.BasicShotsFired - fired, h = _p.BasicShotHits - hits;
                    sb.Append(" | " + (_damage / Seconds).ToString("0", CultureInfo.InvariantCulture) + " a s, "
                              + (f > 0 ? (100f * h / f).ToString("0", CultureInfo.InvariantCulture) : "-") + " % of " + f);
                    Assert.That(f, Is.GreaterThan(3), mode + " " + style + ": shots fired");
                }
                sb.AppendLine();
            }
            TestContext.WriteLine(sb.ToString());
            Debug.Log("PrecisionNumbersPlayTests:\n" + sb);
        }

        IEnumerator Play(Style style)
        {
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 120f;
            float dir = 1f, legAt = t0;
            bool countered = false;
            int firedAtCounter = 0;
            while (Time.time - t0 < Seconds && Time.realtimeSinceStartup < deadline)
            {
                Vector3 target = (_a.IsAlive ? _a : _b).transform.position;
                _p.AimAt(target);
                switch (style)
                {
                    case Style.Standing:
                        _p.Steer(Vector3.zero);
                        _p.Shoot(target);
                        break;
                    case Style.Running:
                        if (_p.Position.x > Edge) dir = -1f;
                        else if (_p.Position.x < -Edge) dir = 1f;
                        _p.Steer(Vector3.right * dir);
                        _p.Shoot(target);
                        break;
                    default:
                        if (!countered)
                        {
                            _p.Steer(Vector3.right * dir);
                            if (Time.time - legAt >= Leg || Mathf.Abs(_p.Position.x) > Edge)
                            {
                                dir = -dir; // the opposite key: the counter-strafe
                                countered = true;
                                firedAtCounter = _p.BasicShotsFired;
                                legAt = Time.time;
                                _p.Steer(Vector3.right * dir);
                            }
                        }
                        else
                        {
                            // Held against her motion until she is all but accurate (or a moment has passed), then the keys let go and the
                            // shot; the run goes on that way. (The meter builds again while the opposite key is held: a tap is its counter-strafe.)
                            bool accurate = _p.PrecisionCone < AccurateEnough || Time.time - legAt > 0.25f;
                            _p.Steer(accurate && _p.BasicShotsFired > firedAtCounter ? Vector3.right * dir : (accurate ? Vector3.zero : Vector3.right * dir));
                            if (accurate && _p.BasicShotsFired == firedAtCounter) _p.Shoot(target);
                            if (_p.BasicShotsFired > firedAtCounter)
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

        static void Place(Damageable d, float x, float z)
        {
            Vector3 p = d.transform.position;
            d.transform.position = new Vector3(x, p.y, z);
        }
    }
}
