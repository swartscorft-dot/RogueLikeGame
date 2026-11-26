using System.Collections;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The boss calls nobody in its floor (work order 140, part 1; Kristian, 2026-11-26: "Ingen flokker"): on the benchmark's floor 5,
    /// with the boss awake and hurt past both of its old shares (to <see cref="BenchmarkStage.BossShareLeft"/>, angry), no pack came,
    /// nothing was made along its room's walls, it told no call and the room's fight is the boss alone; with the switch off the same
    /// stage calls both packs as before (BenchmarkStageTests' own test, with the switch named off).
    /// </summary>
    public class BossCallsNobodyPlayTests
    {
        PlayerCharacter _player;
        BenchmarkStage _stage;
        bool _before;
        int _callsTold;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _before = BossValues.NoPacks;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            GeneratedLevels.Ensure();
            _callsTold = 0;
        }

        [TearDown]
        public void LetGo()
        {
            if (_stage?.Boss != null) _stage.Boss.Reported -= Count;
            BossValues.NoPacks = _before;
            _stage?.End();
            _stage = null;
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
        }

        void Count(EnemyReport r)
        {
            if (r.Kind == EnemyReportKind.CalledPack) _callsTold++;
        }

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
                _stage.Keep();
            }
        }

        [UnityTest]
        public IEnumerator On_TheBossHurtPastBothShares_CallsNobody_AndItsRoomIsTheBossAlone()
        {
            BossValues.NoPacks = true;
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Boss);
            Assert.That(_stage, Is.Not.Null);
            Assert.That(_stage.Boss, Is.Not.Null, "the act boss in its room");
            _stage.Boss.Reported += Count; // before its first frame: the hurt comes after it
            yield return Frames(30);
            EnemyBoss boss = _stage.Boss;
            Assert.That(boss, Is.Not.Null);
            Assert.That(_stage.BossHurt, Is.True);
            Assert.That(boss.HealthShare, Is.LessThan(1f / 3f), "past both of its old shares");
            Assert.That(boss.IsAngry, Is.True, "its anger as before");
            Assert.That(boss.PacksCalled, Is.EqualTo(0), "no pack");
            Assert.That(boss.Called.Count, Is.EqualTo(0));
            Assert.That(_stage.Level.Called.Count, Is.EqualTo(0), "nothing made along its walls");
            Assert.That(_callsTold, Is.EqualTo(0), "no call told (no ring, no sound, no log line)");
            Assert.That(_stage.Fighting.Count, Is.EqualTo(_stage.Level.ExitEnemies.Count));
            foreach (Enemy e in _stage.Fighting) Assert.That(e, Is.InstanceOf<EnemyBoss>(), "the boss alone: " + e.name);
        }

        [UnityTest]
        public IEnumerator Off_TheSameStage_CallsBothPacks_AsBefore()
        {
            BossValues.NoPacks = false;
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Boss);
            Assert.That(_stage, Is.Not.Null);
            Assert.That(_stage.Boss, Is.Not.Null, "the act boss in its room");
            _stage.Boss.Reported += Count; // before its first frame: the hurt comes after it
            yield return Frames(30);
            Assert.That(_stage.Boss.PacksCalled, Is.EqualTo(2));
            Assert.That(_stage.Level.Called.Count, Is.GreaterThan(0));
            Assert.That(_callsTold, Is.EqualTo(2));
        }
    }
}
