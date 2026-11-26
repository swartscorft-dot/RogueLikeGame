using System.Collections;
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
    /// The benchmark's scenarios stage what they claim (work order 032, 2026-11-20), without measuring anything: each is
    /// set up with <see cref="BenchmarkStage.Begin"/> in the ControlRoom scene, given a few frames, and looked at. The
    /// combat rooms are their floor's fullest combat room with every enemy of it awake and her in it; the boss's room has
    /// the boss awake, angry, both its packs called and every one of them awake; she cannot be hurt and presses the shot
    /// at the nearest enemy, and it fires; nobody dies. The practice room's stages nothing and she does not shoot.
    /// </summary>
    public class BenchmarkStageTests
    {
        PlayerCharacter _player;
        BenchmarkStage _stage;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            GeneratedLevels.Ensure();
        }

        [TearDown]
        public void LetGo()
        {
            BossValues.NoPacks = true; // work order 140: back to its default
            _stage?.End();
            _stage = null;
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
        }

        /// <summary>A few frames with the stage kept, as the benchmark keeps it after every frame's Updates.</summary>
        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
                _stage.Keep();
            }
        }

        [UnityTest]
        public IEnumerator Floor5Boss_TheBossIsAwakeAngry_BothPacksAreCalled_AndAllOfThemFight()
        {
            BossValues.NoPacks = false; // work order 140 (changed deliberately): this test pins the packs of before "Ingen flokker"; TearDown puts it back
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Boss);
            Assert.That(_stage, Is.Not.Null);
            StandsWithinEveryLeash();
            yield return Frames(12);
            GeneratedLevel level = _stage.Level;
            Assert.That(level, Is.Not.Null);
            Assert.That(level.Layout.Floor, Is.EqualTo(5));
            Assert.That(level.Layout.Seed, Is.EqualTo(RunSeeds.FloorSeed(BenchmarkStage.RunSeed, 5)));
            Assert.That(_stage.Room, Is.SameAs(level.Layout.ExitRoom));
            Assert.That(level.RoomAt(_player.Position), Is.SameAs(_stage.Room), "she stands in the boss's room");
            EnemyBoss boss = _stage.Boss;
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss, Is.SameAs(level.Boss));
            Assert.That(boss.IsAwake, Is.True, "the boss is awake");
            Assert.That(boss.PacksCalled, Is.EqualTo(2), "both packs called");
            Assert.That(boss.IsAngry, Is.True);
            Assert.That(boss.HealthShare, Is.LessThan(1f / 3f));
            Assert.That(level.Called.Count, Is.GreaterThan(0), "swarmers were called");
            Assert.That(level.Called.Count, Is.EqualTo(boss.Called.Count));
            Assert.That(_stage.Fighting.Count, Is.EqualTo(level.ExitEnemies.Count + level.Called.Count));
            foreach (Enemy e in _stage.Fighting)
            {
                Assert.That(e.IsDead, Is.False, e.name);
                Assert.That(e.IsAwake, Is.True, e.name + " awake");
                Assert.That(e.Body.MaxHealth, Is.EqualTo(BenchmarkStage.EnemyHealth), e.name);
            }
            Assert.That(_player.GetComponent<PlayerHealth>().Untouchable, Is.True, "she cannot be hurt");
            Assert.That(GeneratedLevels.Current.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "nothing waits for a choice");
        }

        [UnityTest]
        public IEnumerator Floor1Combat_TheFullestCombatRoom_EveryEnemyOfItAwake_SheShootsTheNearest()
        {
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor1Combat);
            StandsWithinEveryLeash();
            yield return Frames(3);
            GeneratedLevel level = _stage.Level;
            Assert.That(level.Layout.Floor, Is.EqualTo(1));
            Assert.That(level.Layout.Seed, Is.EqualTo(RunSeeds.FloorSeed(BenchmarkStage.RunSeed, 1)));
            LevelRoom room = _stage.Room;
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat));
            Assert.That(room.Elite, Is.False);
            int most = level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat && !r.Elite)
                                         .Max(r => level.EnemiesByRoom.TryGetValue(r.Order, out var l) ? l.Count : 0);
            Assert.That(level.EnemiesByRoom[room.Order].Count, Is.EqualTo(most), "the fullest combat room");
            Assert.That(most, Is.GreaterThan(0));
            Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "she stands in it");
            foreach (Enemy e in _stage.Fighting) Assert.That(e.IsAwake, Is.True, e.name + " awake");

            // The shot is pressed toward the nearest enemy, and it fires.
            InputFrame? input = _stage.Input();
            Assert.That(input.HasValue && input.Value.ShootPressed, Is.True);
            Enemy nearest = _stage.Target();
            Assert.That(nearest, Is.Not.Null);
            float d = Vector3.Distance(Flat(nearest.transform.position), Flat(_player.Position));
            foreach (Enemy e in _stage.Fighting)
                Assert.That(Vector3.Distance(Flat(e.transform.position), Flat(_player.Position)), Is.GreaterThanOrEqualTo(d - 1e-4f));
            int before = _player.BasicShotsFired;
            yield return Frames(90);
            Assert.That(_player.BasicShotsFired, Is.GreaterThan(before), "she fires");
            foreach (Enemy e in _stage.Fighting) Assert.That(e.IsDead, Is.False, e.name + " still alive");
        }

        [UnityTest]
        public IEnumerator Floor5Combat_IsFloor5sFullestCombatRoom()
        {
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            StandsWithinEveryLeash();
            yield return Frames(3);
            Assert.That(_stage.Level.Layout.Floor, Is.EqualTo(5));
            Assert.That(_stage.Room, Is.SameAs(BenchmarkStage.FullestCombatRoom(_stage.Level)));
            Assert.That(_stage.Level.RoomAt(_player.Position), Is.SameAs(_stage.Room));
            Assert.That(_stage.Fighting.Count, Is.GreaterThan(0));
            foreach (Enemy e in _stage.Fighting) Assert.That(e.IsAwake, Is.True, e.name + " awake");
        }

        [UnityTest]
        public IEnumerator PracticeIdle_StagesNothing_AndSheDoesNotShoot()
        {
            _stage = BenchmarkStage.Begin(BenchmarkScenario.PracticeIdle);
            yield return Frames(3);
            Assert.That(_stage.Level, Is.Null);
            Assert.That(GeneratedLevels.Current.IsInLevel, Is.False);
            Assert.That(_stage.Input().HasValue, Is.False, "no input: she stands");
            Assert.That(_stage.Target(), Is.Null);
        }

        /// <summary>
        /// Work order 032, as amended for the floor in one row (work order 041): where she is put, every enemy of the room is
        /// within its leash of her (farther, the game does not wake it when provoked) and she is clear of each one's body.
        /// </summary>
        void StandsWithinEveryLeash()
        {
            Assert.That(_stage.Room, Is.Not.Null);
            Assert.That(_stage.Level.RoomAt(_stage.Stand), Is.SameAs(_stage.Room), "her place is in the room");
            foreach (Enemy e in _stage.Level.EnemiesByRoom[_stage.Room.Order])
            {
                float d = Vector3.Distance(Flat(e.transform.position), Flat(_stage.Stand));
                Assert.That(d, Is.LessThan(e.Values.LeashRange), e.name + " within its leash of her");
                Assert.That(d, Is.GreaterThanOrEqualTo(e.Body.Radius + BenchmarkStage.StandClear - 1e-3f), e.name + " not on her");
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
