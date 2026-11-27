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
    /// The merge of 2026-11-27, W3 (the producer's ruling on GeneratedLevel.PlaceEnemies): with every room trial on, as he will play it
    /// (work order 135's line-ups, 136's waves, 137's callers, 147's trial boss and 149's unique elite), floor 1's elite room holds the
    /// Grave Ram alone, in one wave and with no caller, and its fight ends when the Ram dies; floors 2 to 5 have their waves, their
    /// callers and an ordinary elite in an elite room of one wave with no caller. 136's and 137's own rules leave the elite room out;
    /// 149 makes only the Ram there: this asks that the three hold together on a run's floors.
    /// </summary>
    public class AllRoomTrialsOnPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run, as RoomCallersPlayTests'

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;

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
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void EverySwitchOn()
        {
            LevelValues v = _levels.Values;
            Assert.That(v.LineUps && v.Waves && v.Callers, Is.True, "work orders 135, 136 and 137 on, as he plays");
            Assert.That(RoomTrialSettings.InUse(v) && RoomTrialSettings.WavesInUse(v) && RoomTrialSettings.CallersInUse(v), Is.True, "and on the ROOMS page");
            Assert.That(BossValues.Trial, Is.True, "work order 147 on");
            Assert.That(EliteValues.Unique && EliteSettings.UniqueNow, Is.True, "work order 149 on");
        }

        [UnityTest]
        public IEnumerator Floor1sEliteRoom_HoldsTheRamAlone_InOneWave_WithNoCaller_AndItsFightEndsWithIt()
        {
            EverySwitchOn();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            yield return null;
            yield return null;
            LevelRoom elite = level.Layout.EliteRoom;
            Assert.That(elite, Is.Not.Null, "floor 1 has its elite room");
            Assert.That(level.Elite, Is.InstanceOf<EnemyGraveRam>(), "the Ram is floor 1's elite");
            Assert.That(level.EliteRoomEnemies.Count, Is.EqualTo(1), "alone in its room");
            Assert.That(level.EliteRoomEnemies[0], Is.SameAs(level.Elite));
            Assert.That(level.EnemiesByRoom[elite.Order], Is.EqualTo(new Enemy[] { level.Elite }), "its room's only enemy");
            Assert.That(_levels.WaveCountOf(elite), Is.EqualTo(1), "the elite room brings no waves (work order 136's rule)");
            Assert.That(_levels.CallerOf(elite), Is.Null, "nor a caller (work order 137's rule)");
            Assert.That(level.Elite.gameObject.activeInHierarchy, Is.True, "in the game, not waiting for a wave");
            Assert.That(level.Elite.Fight, Is.SameAs(_levels.FightOf(elite)), "its room's fight is its");
            Assert.That(level.EliteRoomCleared, Is.False);
            level.Elite.Body.TakeHit(100000000);
            for (int i = 0; i < 600 && !level.EliteRoomCleared; i++) yield return null;
            Assert.That(level.Elite.IsDead, Is.True);
            Assert.That(level.EliteRoomCleared, Is.True, "the Ram dead: its room is cleared, its fight over");
            for (int i = 0; i < 600 && _levels.EliteDrop == null && _levels.Choice == GeneratedLevels.ChoiceKind.None; i++) yield return null;
            Assert.That(_levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None, Is.True, "and its reward comes, as today's elite's");
        }

        [UnityTest]
        public IEnumerator Floors2To5_HaveTheirWaves_TheirCallers_AndAnOrdinaryElite_InAnEliteRoomOfOneWaveWithNoCaller()
        {
            EverySwitchOn();
            int callers = 0;
            for (int floor = 2; floor <= 5; floor++)
            {
                GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                Assert.That(level, Is.Not.Null, "floor " + floor);
                yield return null;
                yield return null;
                Assert.That(_levels.RoomWaveList.Count(w => w.Count > 1), Is.GreaterThan(0), "floor " + floor + ": rooms in waves");
                callers += _levels.RoomCallerList.Count;
                LevelRoom elite = level.Layout.EliteRoom;
                if (elite == null) continue;
                Assert.That(level.Elite, Is.Not.Null, "floor " + floor + ": its elite");
                Assert.That(level.Elite, Is.Not.InstanceOf<EnemyGraveRam>(), "floor " + floor + ": no Ram past floor 1");
                Assert.That(level.Elite.IsElite, Is.True);
                Assert.That(level.EliteRoomEnemies.Count, Is.GreaterThan(1), "floor " + floor + ": the elite with its room's others, as today");
                Assert.That(_levels.WaveCountOf(elite), Is.EqualTo(1), "floor " + floor + ": the elite room in one wave");
                Assert.That(_levels.CallerOf(elite), Is.Null, "floor " + floor + ": no caller in the elite room");
                Assert.That(level.EliteRoomEnemies.All(e => e.gameObject.activeInHierarchy), Is.True, "floor " + floor + ": none waits for a wave");
            }
            Assert.That(callers, Is.GreaterThan(0), "callers on floors 2 to 5");
            _levels.BackToPracticeRoom();
        }
    }
}
