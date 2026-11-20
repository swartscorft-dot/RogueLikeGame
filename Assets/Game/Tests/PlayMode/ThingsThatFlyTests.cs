using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// Things that fly (work order 036, 2026-11-20; decision 0010), in the ControlRoom scene, through the practice room's
    /// spawns and the character's intents: with the switch on no aim line and no circle is drawn, and off they are; an
    /// arrow hits her standing and running straight at 8 m and 16 m, and misses when she turns after the release; a wall,
    /// a tall prop and what she ducked behind each stop one; the dash lets one pass; a fan is three, an elite's volley two;
    /// an orb hits her standing and is walked around; the caster's fan is blue and Stopping Shot from its front drops it;
    /// the boss's Cleave sends a wave that hurts once, that a prop does not stop and that the dash passes; what flies freezes
    /// with a pause and is gone on clearing the practice room, on a new floor, on a restart and at a run's end; nothing is
    /// made per shot once warm; the recorder counts them. And the measure of "keeping it hard": the hits of one shooter at
    /// 12 m and one caster at 10 m over thirty seconds, standing, running straight and turning every second, switch off and
    /// on. Every number is read from the values.
    ///
    /// The practice brute is switched off and the dummies are moved out of the way; the enemies are put where each test
    /// says, unable to walk.
    /// </summary>
    public class ThingsThatFlyTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 EnemyAt = new Vector3(0f, 0f, 8f);
        static readonly Vector3 West = Vector3.left;

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<Object> _made = new List<Object>();
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
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
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            _player.Teleport(EnemyAt + Vector3.back * 12f);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
            }
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        /// <summary>One enemy of a kind from the practice room's intent, unable to walk, put at <paramref name="at"/>.</summary>
        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        EnemyShooter Shooter(Vector3? at = null) => Spawn<EnemyShooter>(EnemyKind.Shooter, at ?? EnemyAt);
        EnemyCaster Caster(Vector3? at = null) => Spawn<EnemyCaster>(EnemyKind.Caster, at ?? EnemyAt);

        /// <summary>She stands <paramref name="distance"/> south of the enemy.</summary>
        void StandSouthOf(Enemy e, float distance) => _player.Teleport(Flat(e.transform.position) + Vector3.back * distance);

        /// <summary>The strikes an enemy reports, in order: what became of each thing it loosed.</summary>
        List<EnemyReport> Strikes(Enemy e)
        {
            var list = new List<EnemyReport>();
            e.Reported += r => { if (r.Kind == EnemyReportKind.Struck) list.Add(r); };
            return list;
        }

        List<ThingsThatFly.Shown> Flying(Enemy owner = null)
        {
            _fly.ListFlying(_shown);
            return _shown.Where(s => owner == null || s.Owner == owner).ToList();
        }

        IEnumerator UntilNothingFlies(float seconds = 4f) => Until(() => _fly.Live == 0, seconds);

        static GameObject Wall(Vector3 centre, Vector3 across, float length)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(centre.x, 1.5f, centre.z), Quaternion.LookRotation(Vector3.Cross(Vector3.up, across)));
            wall.transform.localScale = new Vector3(length, 3f, 0.3f);
            wall.AddComponent<Cover>();
            wall.AddComponent<Shelter>();
            return wall;
        }

        /// <summary>A prop as a level builds it: cover when it is tall enough, and something to take cover behind.</summary>
        GameObject Prop(PropModel model, Vector3 at, float yaw)
        {
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), Mathf.RoundToInt(yaw) % 360, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(new PropValues())) go.AddComponent<Cover>();
            go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        // ---------------- No shape on the floor ----------------

        [UnityTest]
        public IEnumerator TheShooter_DrawsNoAimLine_WithTheSwitchOn_AndDrawsItOff()
        {
            foreach (bool on in new[] { true, false })
            {
                _fly.Values.On = on;
                EnemyShooter shooter = Shooter();
                StandSouthOf(shooter, 10f);
                yield return Until(() => shooter.State == EnemyState.Windup, 4f);
                Assert.That(shooter.State, Is.EqualTo(EnemyState.Windup), "it draws its bow");
                yield return null;
                Assert.That(shooter.IsTelegraphing, Is.EqualTo(!on), on ? "no aim line with the switch on" : "the aim line with it off");
                Assert.That(shooter.GetComponentsInChildren<LineRenderer>(true).Any(l => l.enabled), Is.EqualTo(!on), "and nothing on the floor of its own");
                Assert.That(shooter.Attack.Name, Is.EqualTo(on ? ShooterValues.Arrow : "Shot"));
                int fired = _fly.Fired;
                yield return Until(() => shooter.Strikes > 0, 3f);
                Assert.That(_fly.Fired - fired, Is.EqualTo(on ? 1 : 0), on ? "an arrow flies" : "the old line, nothing flies");
                _levels.ClearSpawned();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheCaster_PlacesNoCircle_WithTheSwitchOn_AndPlacesThemOff()
        {
            foreach (bool on in new[] { true, false })
            {
                _fly.Values.On = on;
                EnemyCaster caster = Caster();
                StandSouthOf(caster, 10f);
                yield return Until(() => caster.State == EnemyState.Windup, 4f);
                Assert.That(caster.Attack.Name, Is.EqualTo(on ? CasterValues.BoneOrb : CasterValues.BoneSpikes));
                yield return Until(() => caster.Strikes > 0, 3f);
                yield return null;
                bool circleObjects = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l => l.name.StartsWith("CasterCircle_", StringComparison.Ordinal));
                Assert.That(caster.Circles.Count > 0, Is.EqualTo(!on), on ? "no circle with the switch on" : "a circle with it off");
                if (on)
                {
                    Assert.That(circleObjects, Is.False, "none on the floor");
                    Assert.That(Flying(caster).Count, Is.EqualTo(1), "an orb flies instead");
                }
                _levels.ClearSpawned();
                yield return null;
            }
        }

        // ---------------- The shooter's arrows ----------------

        [UnityTest]
        public IEnumerator AnArrow_HitsHerStanding_At8And16Metres()
        {
            foreach (float d in new[] { 8f, 16f })
            {
                EnemyShooter shooter = Shooter();
                List<EnemyReport> strikes = Strikes(shooter);
                StandSouthOf(shooter, d);
                shooter.Provoke();
                int health = _health.Current;
                yield return Until(() => shooter.Strikes > 0, 5f);
                Assert.That(shooter.Strikes, Is.EqualTo(1), "it loosed at " + d + " m");
                Assert.That(Flying(shooter).Count, Is.EqualTo(1));
                yield return Until(() => strikes.Count > 0, 3f);
                Assert.That(strikes.Count, Is.EqualTo(1));
                Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.Landed), "standing at " + d + " m she is hit");
                Assert.That(strikes[0].Flown, Is.True);
                Assert.That(health - _health.Current, Is.EqualTo(CasterTactics.Find(shooter.Tuning.Arrows, ShooterValues.Arrow).Damage));
                _levels.ClearSpawned();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AnArrow_HitsHerRunningStraight_At8And16Metres_AndMissesHerWhenSheTurnsAfterTheRelease()
        {
            foreach (float d in new[] { 8f, 16f })
            {
                foreach (bool turn in new[] { false, true })
                {
                    EnemyShooter shooter = Shooter(EnemyAt + Vector3.right * 8f);
                    List<EnemyReport> strikes = Strikes(shooter);
                    StandSouthOf(shooter, d);
                    shooter.Provoke();
                    yield return Until(() => shooter.State == EnemyState.Windup, 4f);
                    Assert.That(shooter.State, Is.EqualTo(EnemyState.Windup));
                    _player.Steer(West); // running straight across, from the draw on
                    yield return Until(() => shooter.Strikes > 0, 3f);
                    Assert.That(shooter.Strikes, Is.EqualTo(1));
                    float speed = _player.Velocity.magnitude;
                    if (turn) _player.Steer(-West); // a turn the moment it leaves
                    yield return Until(() => strikes.Count > 0, 3f);
                    _player.Stop();
                    string what = (turn ? "turning back after the release" : "running straight") + " at " + d + " m (at " + speed.ToString("0.0", s_Inv) + " m/s)";
                    Debug.Log("[036] arrow, " + what + ": " + (strikes.Count > 0 ? strikes[0].Outcome.ToString() : "nothing"));
                    Assert.That(strikes.Count, Is.EqualTo(1), what);
                    Assert.That(speed, Is.GreaterThan(4f), "she was running when it left");
                    Assert.That(strikes[0].Outcome, Is.EqualTo(turn ? StrikeOutcome.OutOfArea : StrikeOutcome.Landed), what);
                    _levels.ClearSpawned();
                    _player.Teleport(EnemyAt + Vector3.back * 12f);
                    yield return null;
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator AWall_ATallProp_AndWhatSheDuckedBehind_EachStopOne()
        {
            // A wall, and a pillar, put between them while the arrow is in the air (it does not loose at what it cannot see).
            foreach (bool pillar in new[] { false, true })
            {
                EnemyShooter shooter = Shooter();
                List<EnemyReport> strikes = Strikes(shooter);
                StandSouthOf(shooter, 12f);
                shooter.Provoke();
                int health = _health.Current;
                yield return Until(() => shooter.Strikes > 0, 5f);
                Vector3 mid = Flat(shooter.transform.position) + Vector3.back * 7f;
                if (pillar)
                {
                    GameObject p = Prop(PropModel.Pillar, mid, 0f);
                    Assert.That(p.GetComponent<Cover>(), Is.Not.Null, "a tall prop");
                }
                else _made.Add(Wall(mid, Vector3.right, 4f));
                yield return Until(() => strikes.Count > 0, 3f);
                Assert.That(strikes.Count, Is.EqualTo(1));
                Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.StoppedByWall), pillar ? "the pillar stopped it" : "the wall stopped it");
                Assert.That(_health.Current, Is.EqualTo(health));
                List<ThingsThatFly.Shown> left = Flying(shooter);
                if (left.Count > 0) Assert.That(FlatDistance(left[0].Position, mid), Is.LessThan(1f), "it stopped at it, puffing");
                _levels.ClearSpawned();
                foreach (Object o in _made) if (o != null) Object.Destroy(o);
                _made.Clear();
                yield return null;
                yield return null;
            }

            // Ducked behind a low prop: it does not loose at her while she is down; she stands for a shot, it draws, she
            // ducks again before the release, and what she is behind stops the arrow.
            Vector3 stand = _player.Position;
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, stand + Vector3.forward * (1.2f + (float)PropSize.Of(PropModel.Sarcophagus).Depth * 0.5f), 0f);
            Assert.That(sarcophagus.GetComponent<Cover>(), Is.Null, "a low prop");
            Physics.SyncTransforms();
            yield return null;
            _player.HoldCover(true);
            yield return Until(() => _player.IsDucked, 1f);
            Assert.That(_player.IsDucked, Is.True, "ducked in cover");
            EnemyShooter s2 = Shooter(Flat(_player.Position) + Vector3.forward * 9f);
            List<EnemyReport> got = Strikes(s2);
            s2.Provoke();
            int before = _health.Current;
            yield return For(0.5f);
            Assert.That(s2.State, Is.Not.EqualTo(EnemyState.Windup), "ducked, she is not seen");
            _player.Shoot(_player.Position + Vector3.right * 5f);
            yield return Until(() => s2.State == EnemyState.Windup, 2f);
            Assert.That(s2.State, Is.EqualTo(EnemyState.Windup), "standing for her shot, she is seen");
            yield return Until(() => got.Count > 0, 4f);
            Assert.That(_player.IsDucked, Is.True, "ducked again when it came");
            Assert.That(got.Count, Is.EqualTo(1));
            Assert.That(got[0].Outcome, Is.EqualTo(StrikeOutcome.StoppedByCover), "what she ducked behind stopped it");
            Assert.That(_player.EnemyShotsStoppedByCover, Is.EqualTo(1));
            Assert.That(_health.Current, Is.EqualTo(before));
            _player.HoldCover(false);
        }

        [UnityTest]
        public IEnumerator TheDash_LetsOnePass()
        {
            EnemyShooter shooter = Shooter();
            List<EnemyReport> strikes = Strikes(shooter);
            StandSouthOf(shooter, 14f);
            shooter.Provoke();
            int health = _health.Current;
            yield return Until(() => shooter.Strikes > 0, 5f);
            // Into it: when it is two metres off, she dashes at it, through it.
            yield return Until(() => Flying(shooter).Any(s => FlatDistance(s.Position, _player.Position) < 2f), 2f);
            Assert.That(_player.DashToward(shooter.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => strikes.Count > 0, 1f);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.AvoidedByDash), "the dash's protected part let it pass");
            Assert.That(_health.Current, Is.EqualTo(health));
            List<ThingsThatFly.Shown> on = Flying(shooter);
            Assert.That(on.Count == 0 || on[0].Passed, Is.True, "and it flies on behind her");
            yield return UntilNothingFlies();
            Assert.That(strikes.Count, Is.EqualTo(1), "it hurt her no more after");
            Assert.That(_health.Current, Is.EqualTo(health));
        }

        [UnityTest]
        public IEnumerator AFan_IsThreeArrows_AndItsDrawShowsLarger()
        {
            CombatEffects fx = CombatEffects.Ensure();
            var windups = new List<float>();
            Action<CombatFx> listen = f => { if (f.Moment == FxMoment.WindupStarted) windups.Add(f.Piece.Size); };
            CombatEffects.Spawned += listen;
            try
            {
                EnemyShooter shooter = Shooter();
                shooter.Floor = 3;
                shooter.Tuning.FanEvery = 2; // the second shot a fan, so the test need not wait for the third
                StandSouthOf(shooter, 10f);
                shooter.Provoke();
                yield return Until(() => shooter.Strikes >= 1, 5f);
                Assert.That(Flying(shooter).Count, Is.EqualTo(1), "the first: one arrow");
                yield return Until(() => shooter.State == EnemyState.Windup && shooter.Attack.Name == ShooterValues.ArrowFan, 4f);
                Assert.That(shooter.Attack.Name, Is.EqualTo(ShooterValues.ArrowFan), "the second is a fan on floor 3");
                Assert.That(shooter.Attack.Windup, Is.GreaterThan(CasterTactics.Find(shooter.Tuning.Arrows, ShooterValues.Arrow).Windup), "drawn a little longer");
                int fired = _fly.Fired;
                yield return Until(() => shooter.Strikes >= 2, 3f);
                List<ThingsThatFly.Shown> fan = Flying(shooter).Where(s => s.Number > fired).ToList();
                Assert.That(fan.Count, Is.EqualTo(shooter.Tuning.FanArrows), "a fan is three");
                Vector3 toHer = Flat(_player.Position - shooter.transform.position).normalized;
                fan = fan.OrderBy(s => Vector3.SignedAngle(toHer, s.Direction, Vector3.up)).ToList();
                for (int i = 0; i + 1 < fan.Count; i++)
                    Assert.That(Vector3.Angle(fan[i].Direction, fan[i + 1].Direction), Is.EqualTo(shooter.Tuning.FanDegrees).Within(0.1f));
                Assert.That(Vector3.Angle(fan[1].Direction, toHer), Is.LessThan(1f), "the middle one at her (she stands)");
                Assert.That(windups.Count, Is.GreaterThanOrEqualTo(2), "a wind-up effect each");
                Assert.That(windups[1], Is.EqualTo(windups[0] * shooter.Tuning.FanWindupLook).Within(1e-3f), "the fan's is larger");
            }
            finally
            {
                CombatEffects.Spawned -= listen;
            }
            Assert.That(fx, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AnElitesVolley_IsTwoArrows_AQuarterSecondApart_EachAimedAsItLeaves()
        {
            EnemyShooter shooter = Shooter();
            EnemyFactory.MakeElite(shooter, 2000, 1.25f, 4f / 3f, 0.5f);
            List<EnemyReport> strikes = Strikes(shooter);
            var times = new List<float>();
            Action<Enemy, EnemyAttackDefinition, int> loosed = (e, a, n) => { if (e == shooter) times.Add(Time.time); };
            ThingsThatFly.Loosed += loosed;
            try
            {
                StandSouthOf(shooter, 10f);
                shooter.Provoke();
                yield return Until(() => shooter.State == EnemyState.Windup, 4f);
                Assert.That(shooter.Attack.Name, Is.EqualTo(ShooterValues.Volley), "an elite's ordinary shot");
                yield return Until(() => shooter.Strikes > 0, 3f);
                Vector3 first = Flying(shooter)[0].Direction;
                _player.Steer(West); // she moves between the two
                yield return Until(() => times.Count >= 2, 1f);
                _player.Stop();
                Assert.That(times.Count, Is.EqualTo(2), "two arrows");
                Assert.That(times[1] - times[0], Is.EqualTo(shooter.Tuning.VolleyGap).Within(0.05f), "a quarter second apart");
                List<ThingsThatFly.Shown> both = Flying(shooter).OrderBy(s => s.Number).ToList();
                Assert.That(both.Last().Number, Is.GreaterThan(both.First().Number));
                Assert.That(Vector3.Angle(first, both.Last().Direction), Is.GreaterThan(1f), "the second aimed where she is going when it leaves");
                yield return UntilNothingFlies();
                Assert.That(strikes.Count, Is.EqualTo(2), "each ends once");
                float volley = CasterTactics.Find(shooter.Tuning.Arrows, ShooterValues.Volley).Recovery;
                Assert.That(times.Count(t => t - times[0] < volley), Is.EqualTo(2), "no third in the volley");
            }
            finally
            {
                ThingsThatFly.Loosed -= loosed;
            }
        }

        // ---------------- The caster's orbs ----------------

        [UnityTest]
        public IEnumerator AnOrb_HitsHerStanding_AndIsWalkedAround()
        {
            EnemyCaster caster = Caster();
            List<EnemyReport> strikes = Strikes(caster);
            StandSouthOf(caster, 10f);
            caster.Provoke();
            int health = _health.Current;
            yield return Until(() => caster.Strikes > 0, 5f);
            Assert.That(Flying(caster).Single().Kind, Is.EqualTo(ProjectileKind.Orb));
            Assert.That(Flying(caster).Single().RingDrawn, Is.True, "with its ring on the floor under it");
            yield return Until(() => strikes.Count > 0, 4f);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.Landed), "standing she is hit");
            Assert.That(health - _health.Current, Is.EqualTo(caster.Tuning.Find(CasterValues.BoneOrb).Damage));

            // The next: she walks out of its way once it has left.
            yield return Until(() => caster.Strikes > 1, 5f);
            Assert.That(caster.Attack.Name, Is.EqualTo(CasterValues.BoneOrb), "the second cast, an orb");
            _player.Steer(West);
            yield return For(0.6f);
            _player.Stop();
            yield return Until(() => strikes.Count > 1, 4f);
            Assert.That(strikes.Count, Is.EqualTo(2));
            Assert.That(strikes[1].Outcome, Is.EqualTo(StrikeOutcome.OutOfArea), "walked around");
        }

        [UnityTest]
        public IEnumerator TheCastersFan_IsBlue_AndStoppingShotFromItsFront_DropsIt()
        {
            CombatEffects.Ensure();
            var blue = new List<bool>();
            Action<CombatFx> listen = f => { if (f.Moment == FxMoment.WindupStarted) blue.Add(f.Piece.Rim == CombatEffects.Current.Values.CounterRim); };
            CombatEffects.Spawned += listen;
            try
            {
                int stop = Slot("Stopping Shot");
                EnemyCaster caster = Caster();
                caster.Tuning.BigCastEvery = 1; // every cast the big one
                List<EnemyReport> strikes = Strikes(caster);
                StandSouthOf(caster, 8f); // within Stopping Shot's reach, in its front
                caster.Provoke();
                yield return Until(() => caster.State == EnemyState.Windup, 4f);
                Assert.That(caster.Attack.Name, Is.EqualTo(CasterValues.OrbFan));
                Assert.That(PoolRules.IsCounterable(caster.Attack), Is.True);
                Assert.That(blue.Count, Is.EqualTo(1));
                Assert.That(blue[0], Is.True, "its wind-up is blue");
                int fired = _fly.Fired;
                Assert.That(_player.PressSkill(stop, caster.transform.position), Is.EqualTo(IntentResult.Started));
                yield return Until(() => caster.Countered > 0 || caster.State != EnemyState.Windup, 2f);
                Assert.That(caster.Countered, Is.EqualTo(1), "countered from its front");
                Assert.That(caster.IsDazed, Is.True);
                yield return For(caster.Attack != null ? caster.Attack.Windup : 1.4f);
                Assert.That(_fly.Fired, Is.EqualTo(fired), "nothing left its hands");
                Assert.That(strikes, Is.Empty);

                // Not countered, a fan is three orbs.
                yield return Until(() => !caster.IsDazed, 6f);
                yield return Until(() => caster.Strikes > 0, 6f);
                Assert.That(Flying(caster).Count(s => s.Kind == ProjectileKind.Orb), Is.EqualTo(caster.Tuning.FanOrbs), "a fan of three");
            }
            finally
            {
                CombatEffects.Spawned -= listen;
            }
        }

        // ---------------- The boss's wave ----------------

        [UnityTest]
        public IEnumerator TheBossesCleave_SendsAWave_ThatHurtsOnce_ThatAPropDoesNotStop_AndThatTheDashPasses()
        {
            EnemyBoss boss = Spawn<EnemyBoss>(EnemyKind.Boss, EnemyAt);
            foreach (EnemyAttackDefinition a in boss.Tuning.Enemy.Attacks)
                if (a.Rush > 0f) a.MaxDistance = -1f; // no charge: the test is about the Cleave
            EnemyAttackDefinition cleave = boss.Tuning.Enemy.Attacks.First(a => a.Name == BossValues.Cleave);
            var struck = new List<EnemyReport>();
            boss.Reported += r => { if (r.Kind == EnemyReportKind.Struck) struck.Add(r); };
            const float Near = 4.6f; // the Cleave's reach and not the Spin's

            // In its Cleave: hurt by the Cleave, once; the wave passes her.
            StandSouthOf(boss, Near);
            boss.Provoke();
            int health = _health.Current;
            yield return Until(() => boss.WavesSent > 0, 5f);
            Assert.That(boss.WavesSent, Is.EqualTo(1), "its Cleave sent a wave");
            Assert.That(Flying(boss).Single().Kind, Is.EqualTo(ProjectileKind.Wave));
            yield return UntilNothingFlies();
            Assert.That(health - _health.Current, Is.EqualTo(cleave.Damage), "hurt once, by the Cleave");
            Assert.That(struck.Count(r => r.Attack == BossValues.Cleave && r.Outcome == StrikeOutcome.Landed), Is.EqualTo(1));
            Assert.That(struck.Count(r => r.Attack == BossValues.Wave && r.Outcome == StrikeOutcome.NoEffect), Is.EqualTo(1), "its wave spared her");

            // Out of the Cleave in its wind-up, with a pillar between: the wave reaches her through the prop.
            for (int round = 0; round < 2; round++)
            {
                bool dash = round == 1;
                _player.Teleport(Flat(boss.transform.position) + Flat(boss.transform.forward) * Near);
                GameObject pillar = null;
                yield return Until(() => boss.State == EnemyState.Windup && boss.Attack == cleave, 5f);
                Assert.That(boss.Attack, Is.SameAs(cleave));
                Vector3 ahead = Flat(boss.AimPoint - boss.transform.position).normalized;
                _player.Teleport(Flat(boss.transform.position) + ahead * (cleave.Length + 2f));
                if (!dash) pillar = Prop(PropModel.Pillar, Flat(boss.transform.position) + ahead * (cleave.Length - 0.5f), 0f); // right between them
                Physics.SyncTransforms();
                health = _health.Current;
                int waves = boss.WavesSent, ends = struck.Count;
                yield return Until(() => boss.WavesSent > waves, 3f);
                Assert.That(boss.WavesSent, Is.EqualTo(waves + 1));
                Assert.That(struck.Last().Attack, Is.EqualTo(BossValues.Cleave));
                Assert.That(struck.Last().Outcome, Is.EqualTo(StrikeOutcome.OutOfArea), "out of the Cleave");
                if (dash)
                {
                    yield return Until(() => Flying(boss).Any(s => FlatDistance(s.Position, _player.Position) < 2f), 3f);
                    Assert.That(_player.DashToward(boss.transform.position), Is.EqualTo(IntentResult.Started));
                }
                yield return Until(() => struck.Count > ends + 1, 3f);
                EnemyReport wave = struck.Last();
                Assert.That(wave.Attack, Is.EqualTo(BossValues.Wave));
                if (dash)
                {
                    Assert.That(wave.Outcome, Is.EqualTo(StrikeOutcome.AvoidedByDash), "the dash passed it");
                    Assert.That(_health.Current, Is.EqualTo(health));
                }
                else
                {
                    Assert.That(wave.Outcome, Is.EqualTo(StrikeOutcome.Landed), "the wave hit her, the prop did not stop it");
                    Assert.That(health - _health.Current, Is.EqualTo(boss.Tuning.WaveAttack.Damage), "half the Cleave's");
                }
                if (pillar != null) Object.Destroy(pillar);
                yield return UntilNothingFlies();
            }
        }

        // ---------------- Game time, and the floor it belongs to ----------------

        [UnityTest]
        public IEnumerator WhatFlies_FreezesWithAPause_AndIsGoneOnClearingThePracticeRoom_OnANewFloor_OnARestart_AndAtARunsEnd()
        {
            // A pause holds it where it is.
            EnemyCaster caster = Caster();
            StandSouthOf(caster, 12f);
            caster.Provoke();
            yield return Until(() => _fly.Live > 0, 5f);
            Assert.That(_levels.Pause(), Is.True);
            Vector3 at = Flying()[0].Position;
            float until = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(FlatDistance(Flying()[0].Position, at), Is.LessThan(1e-4f), "frozen with the pause");
            Assert.That(_levels.Resume(), Is.True);
            yield return For(0.1f);
            Assert.That(FlatDistance(Flying()[0].Position, at), Is.GreaterThan(0.2f), "and on again after");

            // Clearing the practice room takes it.
            _levels.ClearSpawned();
            yield return null;
            yield return null;
            Assert.That(_fly.Live, Is.EqualTo(0), "gone with the practice room's spawns");

            // A new floor takes what the practice room loosed.
            caster = Caster();
            StandSouthOf(caster, 12f);
            caster.Provoke();
            yield return Until(() => _fly.Live > 0, 5f);
            _practiceEnemy.enabled = true;
            GeneratedLevel level = _levels.Build(36UL, 1);
            yield return null;
            Assert.That(_fly.Live, Is.EqualTo(0), "gone on a new floor");

            // A restart, and the end of a run: one of a floor's enemies looses one, far from her.
            _levels.StartRun(3601UL);
            yield return null;
            Enemy owner = _levels.Level.Enemies.First(e => e != null);
            Fire(owner);
            Assert.That(_fly.Live, Is.EqualTo(1));
            yield return null;
            Assert.That(_fly.Live, Is.EqualTo(1), "it flies on its floor");
            _levels.RestartRun();
            yield return null;
            Assert.That(_fly.Live, Is.EqualTo(0), "gone on a restart");
            owner = _levels.Level.Enemies.First(e => e != null);
            Fire(owner);
            yield return null;
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_fly.Live, Is.EqualTo(0), "gone at the run's end");
            Assert.That(level == null, Is.True, "the first floor itself is gone too");
        }

        void Fire(Enemy owner)
        {
            var v = new ShooterValues();
            Vector3 p = owner.transform.position;
            _fly.Fire(owner, v.Arrows[0], ProjectileRules.Arrow(v, new GroundPoint(p.x, p.z), new GroundPoint(0f, 1f)));
        }

        [UnityTest]
        public IEnumerator NothingIsMadePerShot_OnceWarm()
        {
            EnemyShooter shooter = Shooter();
            foreach (EnemyAttackDefinition a in shooter.Tuning.Arrows)
            {
                a.Windup = 0.05f; // quick shots, many of them
                a.Recovery = 0.05f;
            }
            StandSouthOf(shooter, 10f);
            shooter.Provoke();
            int made = _fly.Made, pooled = _fly.Pooled, most = 0;
            yield return Until(() =>
            {
                most = Mathf.Max(most, _fly.Live);
                return _fly.Fired >= 60;
            }, 20f);
            Debug.Log(string.Format(s_Inv, "[036] the pool: {0} loosed, at most {1} live at once, {2} slots, {3} objects made at the start and {4} since",
                                    _fly.Fired, most, _fly.Pooled, made, _fly.Made - made));
            Assert.That(_fly.Fired, Is.GreaterThanOrEqualTo(60));
            Assert.That(most, Is.LessThanOrEqualTo(pooled), "never more at once than the pool holds");
            Assert.That(_fly.Made, Is.EqualTo(made), "nothing made after the start");
            Assert.That(_fly.Pooled, Is.EqualTo(pooled));
        }

        // ---------------- The recorder ----------------

        [UnityTest]
        public IEnumerator TheRecorder_CountsThem_LandedAvoidedAndStoppedByAWall()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ThingsThatFlyTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            EnemyShooter shooter = Shooter();
            List<EnemyReport> strikes = Strikes(shooter);
            StandSouthOf(shooter, 12f);
            shooter.Provoke();
            // The first she takes standing.
            yield return Until(() => strikes.Count >= 1, 6f);
            // The second she steps out of after it leaves.
            yield return Until(() => shooter.Strikes >= 2, 4f);
            _player.Steer(West);
            yield return For(0.4f);
            _player.Stop();
            yield return Until(() => strikes.Count >= 2, 3f);
            // The third a wall stops, put up while it flies.
            yield return Until(() => shooter.Strikes >= 3, 4f);
            _made.Add(Wall(Flat(shooter.transform.position) + Flat(_player.Position - shooter.transform.position) * 0.5f, Vector3.right, 4f));
            yield return Until(() => strikes.Count >= 3, 3f);
            Assert.That(strikes.Select(r => r.Outcome), Is.EqualTo(new[] { StrikeOutcome.Landed, StrikeOutcome.OutOfArea, StrikeOutcome.StoppedByWall }));

            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            List<SessionEvent> mine = events.Where(e => e.EnemyId == shooter.Id).ToList();
            Assert.That(mine.Count(e => e.Type == SessionEventTypes.EnemyLoose && e.Name == ShooterValues.Arrow && e.Enemy == "shooter"), Is.EqualTo(3), "each release");
            List<SessionEvent> ends = mine.Where(e => e.Type == SessionEventTypes.EnemyStrike).ToList();
            Assert.That(ends.Select(e => e.Result), Is.EqualTo(new[] { SessionWords.Landed, SessionWords.OutOfArea, SessionWords.StoppedByWall }));
            Assert.That(ends.All(e => e.Name == ShooterValues.Arrow && e.ExtraValue("flown") == "true"), Is.True, "under its kind and attack, as flown");
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Session.StrikesLanded, Is.EqualTo(1));
            Assert.That(stats.Session.StrikesOutOfArea, Is.EqualTo(1));
            Assert.That(stats.Session.StrikesStoppedByWall, Is.EqualTo(1));
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("stopped by a wall or prop", summary);
            StringAssert.Contains(ShooterValues.Arrow, summary);
            Assert.That(events.First(e => e.Type == SessionEventTypes.SessionStart).ExtraValue("things_that_fly"), Is.EqualTo("true"));
            _recorder = null;
        }

        // ---------------- Keeping it hard: the measure ----------------

        enum Run { Stands, Straight, Turns }

        /// <summary>
        /// Work order 036's measure: one shooter at 12 m and one caster at 10 m, alone, for thirty seconds of game time from
        /// the moment it wakes, while she stands still in the open, runs a straight line across in front of it (turning only
        /// at the ends, 12 m apart: legs of 14 m took 2.33 s, all but the shooter's 2.4 s round, so its releases kept
        /// falling just before a turn), and runs and turns back every second; with the switch off (today's game) and on. What
        /// it began in the thirty seconds is let finish, both ways alike. Game time goes on at 50 frames a second, whatever
        /// the frames take. The numbers are logged ("[036] measure") and the rules of keeping it hard are asserted:
        /// standing still she takes at least what she took, and running straight she is not safe from a shooter.
        /// </summary>
        [UnityTest]
        public IEnumerator KeepingItHard_TheHitsBeforeAndAfter()
        {
            var rows = new List<(EnemyKind kind, Run run, bool on, int hits, int damage)>();
            foreach (EnemyKind kind in new[] { EnemyKind.Shooter, EnemyKind.Caster })
            foreach (Run run in new[] { Run.Stands, Run.Straight, Run.Turns })
            foreach (bool on in new[] { false, true })
            {
                int hits = 0, damage = 0;
                yield return Measure(kind, run, on, (h, d) => { hits = h; damage = d; });
                rows.Add((kind, run, on, hits, damage));
            }
            var text = new System.Text.StringBuilder("[036] measure: hits (damage) in thirty seconds, today's game | things that fly\n");
            foreach (EnemyKind kind in new[] { EnemyKind.Shooter, EnemyKind.Caster })
            foreach (Run run in new[] { Run.Stands, Run.Straight, Run.Turns })
            {
                var off = rows.Single(r => r.kind == kind && r.run == run && !r.on);
                var on = rows.Single(r => r.kind == kind && r.run == run && r.on);
                text.AppendFormat(s_Inv, "  {0,-8} {1,-9} {2,3} ({3,4}) | {4,3} ({5,4})\n", kind, run, off.hits, off.damage, on.hits, on.damage);
            }
            Debug.Log(text.ToString());
            TestContext.WriteLine(text.ToString());
            foreach (EnemyKind kind in new[] { EnemyKind.Shooter, EnemyKind.Caster })
            {
                var off = rows.Single(r => r.kind == kind && r.run == Run.Stands && !r.on);
                var on = rows.Single(r => r.kind == kind && r.run == Run.Stands && r.on);
                Assert.That(on.damage, Is.GreaterThanOrEqualTo(off.damage), kind + ": standing still she takes at least what she took");
            }
            var straight = rows.Single(r => r.kind == EnemyKind.Shooter && r.run == Run.Straight && r.on);
            Assert.That(straight.hits, Is.GreaterThan(0), "running straight she is not safe from a shooter");
        }

        IEnumerator Measure(EnemyKind kind, Run run, bool on, Action<int, int> result)
        {
            _levels.ClearSpawned();
            _fly.Clear();
            yield return null;
            _fly.Values.On = on;
            _health.RestoreFull();
            float distance = kind == EnemyKind.Shooter ? 12f : 10f;
            Vector3 foot = EnemyAt + Vector3.back * distance; // the middle of her line, straight in front of it
            _player.Teleport(foot);
            Enemy e = kind == EnemyKind.Shooter ? (Enemy)Shooter() : Caster();
            yield return null;
            Time.captureDeltaTime = 1f / 50f;
            int start = _health.Current, landed = e.StrikesLanded;
            e.Provoke();
            float t0 = Time.time, nextTurn = t0 + 1f;
            Vector3 heading = West;
            _player.Steer(run == Run.Stands ? Vector3.zero : heading);
            bool stopped = false;
            while (Time.time - t0 < 33f)
            {
                yield return null;
                float x = _player.Position.x - foot.x;
                if (run == Run.Straight && ((heading == West && x < -6f) || (heading == -West && x > 6f)))
                {
                    heading = -heading; // a turn only at the end of the line
                    _player.Steer(heading);
                }
                if (run == Run.Turns && Time.time >= nextTurn)
                {
                    nextTurn += 1f;
                    heading = -heading;
                    _player.Steer(heading);
                }
                if (!stopped && Time.time - t0 >= 30f)
                {
                    // Thirty seconds: it begins nothing more; what it began is let finish.
                    stopped = true;
                    foreach (EnemyAttackDefinition a in AttacksOf(e)) a.MaxDistance = -1f;
                }
            }
            _player.Stop();
            Time.captureDeltaTime = 0f;
            result(e.StrikesLanded - landed, start - _health.Current);
        }

        static IEnumerable<EnemyAttackDefinition> AttacksOf(Enemy e)
        {
            if (e is EnemyShooter s) return s.Tuning.Enemy.Attacks.Concat(s.Tuning.Arrows);
            if (e is EnemyCaster c) return c.Tuning.Enemy.Attacks.Concat(c.Tuning.Orbs);
            return e.Values.Attacks;
        }
    }
}
