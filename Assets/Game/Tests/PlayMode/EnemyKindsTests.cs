using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The three new kinds of enemy (work order 008), in the ControlRoom scene, spawned through the practice room's
    /// intents (<see cref="GeneratedLevels.SpawnPractice"/>) and fought through the character's intents: a swarmer pack
    /// wakes together, strikes and keeps apart; the shooter keeps its distance, its line hits a player who stands and
    /// misses one who steps out, and it does not shoot through a wall; the guard's bar empties under skills with stagger
    /// and not under basic shots, the daze lasts its time, raises the damage it takes and lets no attack land, and the
    /// bar is full after; a generated level holds the kinds of its plan; nothing spawned comes back. Every number is
    /// read from the values classes.
    ///
    /// The practice brute is switched off (it stays in the scene as the template the spawns copy their telegraph from),
    /// so it does not join in. She stands at <see cref="Stand"/>; the spawns come 12 m north of her.
    /// </summary>
    public class EnemyKindsTests
    {
        const int Scatter = 4, FanFire = 2, Volley = 5;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

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

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        T SpawnOne<T>(EnemyKind kind) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            Assert.That(Flat(made[0].transform.position, Stand), Is.EqualTo(GeneratedLevels.SpawnDistance).Within(0.5f), "12 m from her");
            return (T)made[0];
        }

        // ---------------- The swarmer ----------------

        [UnityTest]
        public IEnumerator SwarmerPack_WakesTogether_ItsMembersStrike_AndKeepApart()
        {
            List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
            var values = new SwarmerValues();
            Assert.That(pack.Count, Is.EqualTo(values.PracticePack), "a pack");
            Assert.That(pack.All(e => e is EnemySwarmer));
            yield return null;
            yield return null;
            Assert.That(pack.All(e => !e.IsAwake), "12 m away, beyond their aggro range, they stand");

            pack[0].Body.TakeHit(1);
            int frames = 0;
            while (!pack.All(e => e.IsAwake) && frames < 10)
            {
                yield return null;
                frames++;
            }
            Assert.That(pack.All(e => e.IsAwake), "the whole pack woke with the one that was hurt");
            Assert.That(frames, Is.LessThanOrEqualTo(3), "together, within a frame or two");

            // She stands still; they come and strike. While two or more stand round her, they keep apart.
            int healthBefore = _health.Current;
            float closest = float.MaxValue;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                List<Enemy> near = pack.Where(e => !e.IsDead && Flat(e.transform.position, _player.Position) < 2f).ToList();
                for (int i = 0; i < near.Count; i++)
                    for (int j = i + 1; j < near.Count; j++)
                        closest = Mathf.Min(closest, Flat(near[i].transform.position, near[j].transform.position));
                if (pack.Count(e => e.Strikes > 0) >= 2 && pack.Sum(e => e.StrikesLanded) >= 2) break;
                yield return null;
            }
            int landed = pack.Sum(e => e.StrikesLanded);
            Debug.Log($"[008] swarmer pack: {pack.Count(e => e.Strikes > 0)} struck, {landed} landed; closest two round her {closest:0.00} m (spacing {values.Spacing} m)");
            Assert.That(pack.Count(e => e.Strikes > 0), Is.GreaterThanOrEqualTo(2), "more than one member struck");
            Assert.That(landed, Is.GreaterThanOrEqualTo(1), "on a player who stands still");
            Assert.That(healthBefore - _health.Current, Is.EqualTo(landed * values.Enemy.Attacks[0].Damage), "each landed strike does its damage");
            Assert.That(closest, Is.Not.EqualTo(float.MaxValue), "two stood round her at once");
            Assert.That(closest, Is.GreaterThanOrEqualTo(values.Spacing - 0.1f), "they do not stack");
        }

        // ---------------- The shooter ----------------

        [UnityTest]
        public IEnumerator Shooter_KeepsItsDistance_AndBacksOffWhenSheComesNear()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            ShooterValues values = shooter.Tuning;
            yield return null;
            shooter.Body.TakeHit(1);
            yield return For(3f);
            float d = Flat(shooter.transform.position, _player.Position);
            Assert.That(d, Is.InRange(values.KeepNear - 0.5f, values.KeepFar + 0.5f), "it stands off");

            // She walks up to 3 m on its far side from where she was: it backs off, toward open floor.
            Vector3 away = (shooter.transform.position - _player.Position);
            away.y = 0f;
            _player.Teleport(shooter.transform.position + away.normalized * 3f);
            yield return Until(() => shooter.IsBackingOff, 4f);
            Assert.That(shooter.IsBackingOff, Is.True, "nearer than " + values.BackOffUnder + " m it backs off");
            yield return Until(() => Flat(shooter.transform.position, _player.Position) >= values.KeepNear - 0.3f, 8f);
            d = Flat(shooter.transform.position, _player.Position);
            Assert.That(d, Is.GreaterThanOrEqualTo(values.KeepNear - 0.3f), "back to its distance");
            yield return For(1f);
            Assert.That(shooter.IsBackingOff, Is.False, "and stops");
            Assert.That(Flat(shooter.transform.position, _player.Position), Is.LessThanOrEqualTo(values.KeepFar + 0.5f));
        }

        [UnityTest]
        public IEnumerator Shooter_HitsHerStandingStill_MissesHerWhenSheStepsOut()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            EnemyAttackDefinition shot = shooter.Tuning.Enemy.Attacks[0];
            yield return null;
            shooter.Body.TakeHit(1);

            // Standing still.
            yield return Until(() => shooter.State == EnemyState.Windup, 4f);
            Assert.That(shooter.State, Is.EqualTo(EnemyState.Windup), "it aims");
            Assert.That(shooter.IsTelegraphing, Is.True, "and shows the line");
            int health = _health.Current, landed = shooter.StrikesLanded, strikes = shooter.Strikes;
            yield return Until(() => shooter.Strikes > strikes, shot.Windup + 1f);
            Assert.That(shooter.Strikes, Is.EqualTo(strikes + 1));
            Assert.That(shooter.StrikesLanded, Is.EqualTo(landed + 1), "the line hits her where she stood");
            Assert.That(health - _health.Current, Is.EqualTo(shot.Damage));

            // Stepping out sideways during the next windup.
            yield return Until(() => shooter.State == EnemyState.Windup, shot.Recovery + 2f);
            Assert.That(shooter.State, Is.EqualTo(EnemyState.Windup));
            Vector3 aim = shooter.AimPoint - shooter.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, new Vector3(aim.x, 0f, aim.z)).normalized;
            health = _health.Current;
            landed = shooter.StrikesLanded;
            strikes = shooter.Strikes;
            Vector3 from = _player.Position;
            _player.Steer(side);
            yield return Until(() => Flat(_player.Position, from) >= shot.HalfWidth + Placeholders.PlayerRadius + 0.5f, shot.Windup);
            _player.Steer(Vector3.zero);
            yield return Until(() => shooter.Strikes > strikes, shot.Windup + 1f);
            Assert.That(shooter.Strikes, Is.EqualTo(strikes + 1), "it shot");
            Assert.That(shooter.StrikesLanded, Is.EqualTo(landed), "the line did not follow her");
            Assert.That(_health.Current, Is.EqualTo(health));
        }

        [UnityTest]
        public IEnumerator Shooter_DoesNotShootThroughAWall()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            shooter.Tuning.Enemy.MoveSpeed = 0f; // it would walk round: here it must stand behind the wall
            Vector3 mid = (shooter.transform.position + _player.Position) * 0.5f;
            Vector3 across = shooter.transform.position - _player.Position;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(mid.x, 1.5f, mid.z), Quaternion.LookRotation(new Vector3(across.x, 0f, across.z)));
            wall.transform.localScale = new Vector3(4f, 3f, 0.5f);
            Physics.SyncTransforms(); // frames without a window are far shorter than a physics step
            yield return null;
            Assert.That(shooter.Sees(_player.Position), Is.False, "the wall is between them");

            shooter.Body.TakeHit(1);
            yield return For(3f);
            Assert.That(shooter.IsAwake, Is.True);
            Assert.That(shooter.Strikes, Is.EqualTo(0), "no shot through the wall");
            Assert.That(shooter.State, Is.EqualTo(EnemyState.Chase), "not even a windup");

            Object.Destroy(wall);
            yield return null;
            Assert.That(shooter.Sees(_player.Position), Is.True);
            yield return Until(() => shooter.Strikes > 0, 4f);
            Assert.That(shooter.Strikes, Is.GreaterThan(0), "with the wall gone it shoots: the test means something");
        }

        // ---------------- The guard ----------------

        /// <summary>The guard, standing still, with her 5.7 m south of it: in reach of every skill used here, out of its attacks' reach.</summary>
        IEnumerator GuardInReach(Action<EnemyGuard> got)
        {
            var guard = SpawnOne<EnemyGuard>(EnemyKind.Guard);
            guard.Tuning.Enemy.MoveSpeed = 0f;
            float farthest = guard.Tuning.Enemy.Attacks.Max(a => a.MaxDistance);
            Assume.That(farthest, Is.LessThan(5.7f));
            _player.Teleport(guard.transform.position + Vector3.back * 5.7f);
            yield return null;
            yield return null;
            got(guard);
        }

        IEnumerator UntilFree() => Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 4f);

        [UnityTest]
        public IEnumerator Guard_BasicShots_TakeNoStagger()
        {
            EnemyGuard guard = null;
            yield return GuardInReach(g => guard = g);
            StaggerBar bar = guard.Stagger;
            Assert.That(bar, Is.Not.Null);
            Assert.That(bar.Current, Is.EqualTo(guard.Tuning.Stagger.Max));
            int hp = guard.Body.CurrentHealth;
            for (int i = 0; i < 8; i++)
            {
                int hits = _player.BasicShotHits;
                _player.Shoot(guard.transform.position);
                yield return Until(() => _player.BasicShotHits > hits, 2f);
            }
            Assert.That(guard.Body.CurrentHealth, Is.EqualTo(hp - 8 * Placeholders.BasicShotDamage), "eight shots hit it");
            Assert.That(bar.Current, Is.EqualTo(guard.Tuning.Stagger.Max), "and took nothing off the bar");
        }

        [UnityTest]
        public IEnumerator Guard_SkillsEmptyTheBar_TheDazeLastsItsTime_HurtsMore_LetsNothingLand_ThenTheBarIsFull()
        {
            _folder = Path.Combine(Path.GetTempPath(), "EnemyKindsTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            EnemyGuard guard = null;
            yield return GuardInReach(g => guard = g);
            StaggerValues stagger = guard.Tuning.Stagger;
            StaggerBar bar = guard.Stagger;
            float dazedAt = -1f, endedAt = -1f;
            guard.Body.Dazed += () => dazedAt = Time.time;
            guard.Body.DazeEnded += () => endedAt = Time.time;
            _player.EquipSkill(Volley);
            _player.EquipSkill(FanFire);
            Vector3 at = guard.transform.position;

            // Heavy Volley, then Scatter Blast: each takes its own stagger.
            Assert.That(_player.PressSkill(Volley, at), Is.EqualTo(IntentResult.Started));
            yield return Until(() => bar.Current < stagger.Max, 3f);
            Assert.That(bar.Current, Is.EqualTo(stagger.Max - _player.Skills[Volley].Stagger), "Heavy Volley's stagger");
            yield return UntilFree();
            Assert.That(_player.PressSkill(Scatter, at), Is.EqualTo(IntentResult.Started));
            float before = bar.Current;
            yield return Until(() => bar.Current < before, 2f);
            Assert.That(bar.Current, Is.EqualTo(before - _player.Skills[Scatter].Stagger), "Scatter Blast's stagger");
            yield return UntilFree();

            // Fan Fire held until the bar is empty: 4 a tick.
            Assert.That(_player.PressSkill(FanFire, at), Is.EqualTo(IntentResult.Started));
            yield return Until(() => guard.IsDazed, 3f);
            _player.ReleaseSkill(FanFire);
            Assert.That(guard.IsDazed, Is.True, "the empty bar dazes it");
            Assert.That(bar.Current, Is.EqualTo(0f));
            Assert.That(guard.State, Is.Not.EqualTo(EnemyState.Windup));
            Assert.That(guard.IsTelegraphing, Is.False);
            yield return UntilFree();

            // Dazed: a basic shot deals the bonus.
            int hp = guard.Body.CurrentHealth;
            int hits = _player.BasicShotHits;
            _player.Shoot(at);
            yield return Until(() => _player.BasicShotHits > hits, 2f);
            int bonus = Mathf.RoundToInt(Placeholders.BasicShotDamage * stagger.DazedDamageMultiplier);
            Assert.That(hp - guard.Body.CurrentHealth, Is.EqualTo(bonus), "a basic shot hits " + stagger.DazedDamageMultiplier + " times as hard");

            // She walks into its slam: nothing lands while it is dazed.
            _player.Teleport(at + Vector3.back * 1.5f);
            int strikes = guard.Strikes, health = _health.Current;
            yield return Until(() => !guard.IsDazed, stagger.DazeSeconds + 1f);
            Assert.That(guard.Strikes, Is.EqualTo(strikes), "no attack while dazed");
            Assert.That(_health.Current, Is.EqualTo(health));
            yield return null;
            Assert.That(endedAt - dazedAt, Is.EqualTo(stagger.DazeSeconds).Within(0.1f), "dazed for the set time");
            Assert.That(bar.Current, Is.EqualTo(stagger.Max), "the bar is full again");

            // And then it fights again: the test means something.
            yield return Until(() => guard.Strikes > strikes, 4f);
            Assert.That(guard.Strikes, Is.GreaterThan(strikes), "awake again, it attacks");

            // The recorder saw it all, with the kind.
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Any(e => e.Type == SessionEventTypes.StaggerTaken && e.Enemy == "guard" && e.EnemyId == guard.Id && e.Stagger > 0f));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyDazed && e.EnemyId == guard.Id), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.DazeEnded && e.EnemyId == guard.Id), Is.EqualTo(1));
            Assert.That(events.Any(e => e.Type == SessionEventTypes.Hit && e.Enemy == "guard"), "hits name the kind");
            Assert.That(events.Any(e => e.Type == SessionEventTypes.EnemyWake && e.Enemy == "guard"), "so does its waking");
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Guards, whole session", summary);
            StringAssert.Contains("Enemies by kind, whole session", summary);
            Debug.Log("[008] guard summary\n" + summary);
        }

        // ---------------- Levels and spawns ----------------

        [UnityTest]
        public IEnumerator AGeneratedLevel_HoldsTheKindsOfItsPlan()
        {
            _practiceEnemy.enabled = true;
            GeneratedLevel level = _levels.Build(42UL);
            yield return null;
            var plan = level.Values.Plan;
            int combat = 0;
            foreach (LevelRoom r in level.Layout.Rooms)
            {
                EnemyGroup[] planned = plan.GroupsFor(r.Kind, r.Kind == RoomKind.Combat ? combat++ : 0);
                List<string> want = planned.SelectMany(g => Enumerable.Repeat(g.Kind.ToString(), g.Count)).OrderBy(s => s).ToList();
                List<string> have = (level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> list) ? list : new List<Enemy>())
                                    .Select(e => e.Kind.ToString()).OrderBy(s => s).ToList();
                Assert.That(have, Is.EqualTo(want), "room " + r.Letter + r.Order);
            }
            Assert.That(level.Boss, Is.InstanceOf<EnemyGuard>(), "the boss is a guard");
            Assert.That(level.Boss.Body.MaxHealth, Is.EqualTo(level.Values.BossHealth));
            Assert.That(level.Boss.Body.Stagger, Is.Not.Null, "with its bar");
            Assert.That(level.Boss.GetComponentInChildren<StaggerBarView>(), Is.Not.Null, "shown over it");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator NothingSpawned_ComesBack_AndF12RemovesIt()
        {
            var all = new List<Enemy>();
            all.AddRange(_levels.SpawnPractice(EnemyKind.Swarmer));
            all.AddRange(_levels.SpawnPractice(EnemyKind.Shooter));
            all.AddRange(_levels.SpawnPractice(EnemyKind.Guard));
            Assert.That(all.Count, Is.EqualTo(new SwarmerValues().PracticePack + 2));
            Assert.That(_levels.Spawned.Count, Is.EqualTo(all.Count));
            yield return null;
            foreach (Enemy e in all) Assert.That(e.ComesBack, Is.False, e.name);
            foreach (Enemy e in all) e.Body.TakeHit(100000);
            yield return null;
            yield return null;
            Assert.That(all.All(e => e.IsDead));
            float wait = all.Max(e => e.Values.RespawnDelay) + 0.5f;
            yield return For(wait);
            Assert.That(all.All(e => e.IsDead), "none comes back");

            _levels.ClearSpawned();
            yield return null;
            Assert.That(all.All(e => e == null), "F12 removes them");
            Assert.That(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None), Is.EqualTo(new Enemy[] { _practiceEnemy }), "only the practice enemy is left");
            Assert.That(_levels.Spawned, Is.Empty);
        }
    }
}
