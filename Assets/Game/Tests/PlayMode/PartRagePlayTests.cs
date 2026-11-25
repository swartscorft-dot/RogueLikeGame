using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 129 (Kristian 2026-11-25: "De sikter dårligere, men fyrer av mye oftere. en slags rage mode."), in the ControlRoom
    /// scene through the practice room's spawns: an archer shot to under the first part's share fires again sooner than a whole one,
    /// its draw as long; its arrow leaves along the line its draw showed, and that line is off her by no more than the width; with
    /// the switch off one under the share shoots as a whole one, straight at her; an enemy killed before the share never rages, one
    /// hurt just short of it does not, and one past it does at once. And the measure: the benchmark's floor 5 fight (seed BENCH032,
    /// its fullest combat room) at the floor's own health, she untouchable shooting her basic shot at the nearest: how many are
    /// enraged at once at its worst. No pictures.
    /// </summary>
    public class PartRagePlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 EnemyAt = new Vector3(0f, 0f, 8f);
        const float StandFrom = 9f;   // m she stands south of the archer: in its band, inside its keep (7 to 10 m)
        const int Shots = 4;          // releases measured a case
        const float MeasureSeconds = 120f; // s of the floor 5 fight watched at most (its worst came at 4.8 s when 240 s were watched)

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        bool _onWas;
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _onWas = PartRageValues.On;
            PartRageValues.On = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            if (_practiceEnemy.TryGetComponent(out NavMeshAgent agent)) agent.Warp(new Vector3(-17f, 0f, -17f));
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) d.transform.position = new Vector3(-17f + 3f * k++, d.transform.position.y, 18f);
            }
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            _player.Teleport(EnemyAt + Vector3.back * 12f);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            PartRageValues.On = _onWas;
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (_health != null) _health.Untouchable = false;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static GroundPoint G(Vector3 v) => new GroundPoint(v.x, v.z);

        /// <summary>Signed degrees from <paramref name="a"/> to <paramref name="b"/> on the ground, + to the left seen from above.</summary>
        static float Signed(Vector3 a, Vector3 b)
        {
            float deg = PartRageRules.DegreesBetween(G(a), G(b));
            return a.x * b.z - a.z * b.x >= 0f ? deg : -deg;
        }

        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        /// <summary>Hurts it to just past (or just short of) 122's first part's share of its health.</summary>
        static void HurtTo(Enemy e, bool past)
        {
            int max = e.Body.MaxHealth;
            int share = Mathf.CeilToInt((float)(max * BoneBreakValues.Live.PartShare));
            e.Body.TakeHit(past ? share + 2 : share - 2);
        }

        /// <summary>One release of an archer, as measured: when its draw began, when the arrow left, its draw's facing, the arrow's way, the way to her.</summary>
        struct Release
        {
            public float DrawAt, LeftAt, RageAngle;
            public Vector3 Facing, Arrow, ToHer;
        }

        /// <summary>Waits for <paramref name="count"/> releases of one archer, she standing still, and reads each.</summary>
        IEnumerator Releases(EnemyShooter s, int count, List<Release> into)
        {
            float drawAt = -1f;
            Vector3 facing = Vector3.zero;
            EnemyState before = s.State;
            int seen = 0;
            void OnLoosed(Enemy e, EnemyAttackDefinition a, int n)
            {
                if (e != s) return;
                _fly.ListFlying(_shown);
                ThingsThatFly.Shown newest = default;
                int most = -1;
                foreach (ThingsThatFly.Shown x in _shown)
                    if (x.Owner == s && x.Number > most) { most = x.Number; newest = x; }
                into.Add(new Release
                {
                    DrawAt = drawAt, LeftAt = Time.time, RageAngle = s.RageAngle, Facing = facing, Arrow = Flat(newest.Direction),
                    ToHer = Flat(_player.Position - s.transform.position),
                });
                seen++;
            }
            ThingsThatFly.Loosed += OnLoosed;
            try
            {
                float deadline = Time.realtimeSinceStartup + 30f;
                while (seen < count && Time.realtimeSinceStartup < deadline)
                {
                    if (s.State == EnemyState.Windup && before != EnemyState.Windup)
                    {
                        drawAt = Time.time;
                        facing = Flat(s.transform.forward);
                    }
                    before = s.State;
                    yield return null;
                }
            }
            finally
            {
                ThingsThatFly.Loosed -= OnLoosed;
            }
            Assert.That(seen, Is.EqualTo(count), "it loosed " + count + " arrows");
        }

        static float MeanGap(List<Release> r)
        {
            float sum = 0f;
            for (int i = 1; i < r.Count; i++) sum += r[i].LeftAt - r[i - 1].LeftAt;
            return sum / (r.Count - 1);
        }

        // ---------------- An archer shot to under the share ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator AnArcherShotUnderTheShare_FiresAgainSooner_ItsArrowAlongItsDrawsLine_WithinTheWidth()
        {
            PartRageValues.On = true;
            EnemyAttackDefinition arrow = new ShooterValues().Arrows[0];

            EnemyShooter whole = Spawn<EnemyShooter>(EnemyKind.Shooter, EnemyAt);
            _player.Teleport(Flat(whole.transform.position) + Vector3.back * StandFrom);
            yield return null;
            yield return null;
            whole.Provoke();
            var calm = new List<Release>();
            yield return Releases(whole, Shots, calm);
            Assert.That(whole.IsEnraged, Is.False, "whole, calm");
            _levels.ClearSpawned();
            yield return null;

            EnemyShooter hurt = Spawn<EnemyShooter>(EnemyKind.Shooter, EnemyAt);
            _player.Teleport(Flat(hurt.transform.position) + Vector3.back * StandFrom);
            yield return null;
            yield return null;
            HurtTo(hurt, past: true);
            yield return null;
            Assert.That(hurt.IsEnraged, Is.True, "past the share it is enraged");
            var rage = new List<Release>();
            yield return Releases(hurt, Shots + 2, rage);

            float calmGap = MeanGap(calm), rageGap = MeanGap(rage);
            Debug.Log(string.Format(Inv, "PART RAGE archer: shot to shot calm {0:0.000} s, enraged {1:0.000} s; draws calm {2}, enraged {3}; angles {4}",
                                    calmGap, rageGap, string.Join(", ", calm.Skip(1).Select(r => (r.LeftAt - r.DrawAt).ToString("0.000", Inv))),
                                    string.Join(", ", rage.Skip(1).Select(r => (r.LeftAt - r.DrawAt).ToString("0.000", Inv))),
                                    string.Join(", ", rage.Select(r => r.RageAngle.ToString("0.00", Inv)))));
            Assert.That(calmGap, Is.EqualTo(arrow.Windup + arrow.Recovery).Within(0.15f), "whole: its draw and its whole rest");
            Assert.That(rageGap, Is.EqualTo(arrow.Windup + arrow.Recovery * PartRageValues.Live.RestShare).Within(0.15f), "enraged: its draw and half its rest");
            Assert.That(rageGap, Is.LessThan(calmGap - 0.6f), "it fires again sooner");
            for (int i = 1; i < Shots; i++)
                Assert.That(rage[i].LeftAt - rage[i].DrawAt, Is.EqualTo(calm[i].LeftAt - calm[i].DrawAt).Within(0.05f), "its draw as long as a whole one's");

            float width = PartRageValues.Live.ShotDegrees;
            bool someOff = false;
            foreach (Release r in rage)
            {
                if (r.DrawAt < 0f) continue; // its first draw may have begun before it was watched
                Assert.That(PartRageRules.DegreesBetween(G(r.Facing), G(r.Arrow)), Is.LessThan(0.5f), "the arrow leaves along its draw's line");
                float off = Signed(r.ToHer, r.Arrow);
                Assert.That(Mathf.Abs(off), Is.LessThanOrEqualTo(width + 0.05f), "off her by no more than the width");
                Assert.That(off, Is.EqualTo(r.RageAngle).Within(0.1f), "by its own angle");
                someOff |= Mathf.Abs(off) > 1f;
            }
            Assert.That(someOff, Is.True, "it aims worse: some arrow is off her by more than a degree");
            foreach (Release r in calm)
                Assert.That(PartRageRules.DegreesBetween(G(r.ToHer), G(r.Arrow)), Is.LessThan(0.1f), "whole, straight at her standing");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheSwitchOff_AnArcherUnderTheShare_ShootsAsAWholeOne()
        {
            PartRageValues.On = false;
            EnemyAttackDefinition arrow = new ShooterValues().Arrows[0];
            EnemyShooter hurt = Spawn<EnemyShooter>(EnemyKind.Shooter, EnemyAt);
            _player.Teleport(Flat(hurt.transform.position) + Vector3.back * StandFrom);
            yield return null;
            yield return null;
            HurtTo(hurt, past: true);
            yield return null;
            Assert.That(hurt.IsEnraged, Is.False, "the switch off: no rage");
            var shots = new List<Release>();
            yield return Releases(hurt, Shots, shots);
            Assert.That(MeanGap(shots), Is.EqualTo(arrow.Windup + arrow.Recovery).Within(0.15f), "its whole rest");
            foreach (Release r in shots)
            {
                Assert.That(r.RageAngle, Is.EqualTo(0f));
                Assert.That(PartRageRules.DegreesBetween(G(r.ToHer), G(r.Arrow)), Is.LessThan(0.1f), "straight at her");
            }
        }

        // ---------------- Before the share ----------------

        [UnityTest]
        public IEnumerator AnEnemyKilledBeforeTheShare_NeverRages_OneJustShortDoesNot_AndOnePastDoesAtOnce()
        {
            PartRageValues.On = true;
            _player.Teleport(EnemyAt + Vector3.back * 30f); // far: they do nothing but stand
            EnemyShooter killed = Spawn<EnemyShooter>(EnemyKind.Shooter, EnemyAt + Vector3.left * 3f);
            EnemyCaster shortOf = Spawn<EnemyCaster>(EnemyKind.Caster, EnemyAt + Vector3.right * 3f);
            yield return null;
            yield return null;
            killed.Body.TakeHit(killed.Body.MaxHealth); // dead from whole in one hit
            HurtTo(shortOf, past: false);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(killed.IsEnraged, Is.False, "killed before the share: never");
                Assert.That(killed.EnragedSince, Is.LessThan(0f));
                Assert.That(shortOf.IsEnraged, Is.False, "just short of the share: not yet");
                yield return null;
            }
            Assert.That(killed.IsDead, Is.True);
            shortOf.Body.TakeHit(4);
            yield return null;
            Assert.That(shortOf.IsEnraged, Is.True, "past it: at once");
            Assert.That(shortOf.EnragedSince, Is.GreaterThanOrEqualTo(0f));
            shortOf.Body.TakeHit(shortOf.Body.MaxHealth);
            yield return null;
            Assert.That(shortOf.IsEnraged, Is.False, "dead, its rage is over");
        }

        // ---------------- The measure: the benchmark's floor 5 fight ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator TheMeasure_TheBenchmarksFloor5Fight_AtTheFloorsHealth_EnragedAtOnceAtItsWorst()
        {
            PartRageValues.On = true;
            _levels.ClearSpawned();
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Level, Is.Not.Null, "floor 5 built");
            yield return null;
            yield return null; // every enemy has had its Start
            List<Enemy> theirs = stage.Fighting.Where(x => x != null).ToList();
            foreach (Enemy e in theirs)
                if (e.Body != null && !e.IsDead)
                    e.Body.SetMaxHealth(e.IsElite ? stage.Level.Values.EliteHealthOf(e.Kind) : stage.Level.Values.HealthOf(e.Kind)); // the floor's own health
            string kinds = string.Join(", ", theirs.GroupBy(x => x.LogWord).Select(g => g.Count() + " " + g.Key));
            int rages = theirs.Count(x => PartRageRules.Rages(x.Kind));
            int most = 0, ever = 0;
            float mostAt = 0f, started = Time.time, until = Time.realtimeSinceStartup + MeasureSeconds;
            var seen = new HashSet<Enemy>();
            while (Time.realtimeSinceStartup < until && theirs.Any(x => x != null && !x.IsDead))
            {
                stage.Keep();
                int now = 0;
                foreach (Enemy e in theirs)
                {
                    if (e == null || e.IsDead || !e.IsEnraged) continue;
                    now++;
                    seen.Add(e);
                }
                if (now > most)
                {
                    most = now;
                    mostAt = Time.time - started;
                }
                yield return null;
            }
            ever = seen.Count;
            int left = theirs.Count(x => x != null && !x.IsDead);
            stage.End();
            string line = string.Format(Inv, "PART RAGE floor 5 (seed BENCH032, its fullest combat room): {0} enemies ({1}), {2} of kinds that rage; at the floor's health, she untouchable shooting her basic shot at the nearest: most enraged at once {3} (at {4:0.0} s), enraged in all {5}, {6} left alive after {7:0.0} s",
                                        theirs.Count, kinds, rages, most, mostAt, ever, left, Time.time - started);
            Debug.Log(line);
            TestContext.Out.WriteLine(line);
            Assert.That(most, Is.LessThanOrEqualTo(rages), "only kinds that rage");
            Assert.That(ever, Is.GreaterThan(0), "some were enraged before they died");
        }
    }
}
