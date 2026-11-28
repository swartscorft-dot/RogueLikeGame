using System;
using System.Collections;
using System.Collections.Generic;
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
            // The merge of 2026-11-28, W4: work order 153's timed chest and work order 151's tints among them.
            Assert.That(LevelValues.TimedChests && TimedChestSettings.InUse(v), Is.True, "work order 153 on");
            Assert.That(EnemyTintValues.Show && EnemyTintSettings.InUse, Is.True, "work order 151 on");
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 30f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float until = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 30f;
            while (!condition() && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;

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

        /// <summary>
        /// The merge of 2026-11-28, W4: with every switch on, work order 153's timed chest on a floor from 2 stands in a room without a
        /// caller; she clears the room (its waves and its caller's room's rules as they come), opens the chest, its wave comes by work order
        /// 136's arrival rule and is fought to its end, and the doors open again.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator FromFloor2_ItsTimedChest_StandsInARoomWithNoCaller_OpenedAndFoughtToItsEnd_TheDoorsOpen()
        {
            EverySwitchOn();
            GeneratedLevel level = null;
            GeneratedLevels.TimedChest t = null;
            for (int floor = 2; floor <= 5 && t == null; floor++)
            {
                level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                yield return null;
                yield return null;
                t = _levels.TimedChestOfFloor;
            }
            Assert.That(t, Is.Not.Null, "a timed chest on a floor from 2");
            Assert.That(level.Layout.Rooms.Any(r => r.LineUp != null), Is.True, "the floor's line-ups");
            Assert.That(_levels.RoomWaveList.Count(w => w.Count > 1), Is.GreaterThan(0), "its rooms in waves");
            Assert.That(_levels.CallerOf(t.Room), Is.Null, "the chest's room holds no caller");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Shut));

            // She clears its room: past the line it wakes; every enemy of it is killed, its later waves too, until its doors open.
            LevelRoom room = t.Room;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            yield return Until(() => fight.On, 2f);
            Assert.That(fight.On, Is.True, "past the line: the room woke");
            for (int i = 0; i < 60 && (fight.On || level.EnemiesByRoom[room.Order].Any(e => e != null && !e.IsDead)); i++)
            {
                Kill(level.EnemiesByRoom[room.Order]);
                yield return GameTime(0.3f);
            }
            GeneratedLevels.RoomDoors doors = _levels.DoorsOf(room);
            yield return Until(() => doors == null || !doors.Shut, 3f);
            Assert.That(fight.On, Is.False, "the room's own fight cleared, its waves with it");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable), "then the chest can be opened");

            // Beside it, the interact key opens it: the doors shut, its wave comes; every one of it is killed as it stands.
            Vector3 middle = level.Middle(room);
            Vector3 toward = new Vector3(middle.x - t.Place.x, 0f, middle.z - t.Place.z);
            _player.Teleport(OnNavMesh(t.Place + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * 1.4f));
            yield return null;
            yield return null;
            Assert.That(_levels.Interact(), Is.True, "the interact key opens it");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Fighting));
            _player.Teleport(OnNavMesh(middle));
            yield return Until(() => t.Wave.Count > 0, 3f);
            Assert.That(t.Wave.Count, Is.GreaterThan(0), "its wave made");
            float until = Time.time + 30f;
            while (t.State == GeneratedLevels.TimedChestState.Fighting && Time.time < until)
            {
                Kill(t.Wave.Where(e => e != null && e.isActiveAndEnabled));
                yield return GameTime(0.25f);
            }
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Won), "its wave fought to its end");
            Assert.That(t.Arrived.Count, Is.GreaterThan(0), "its wave came by the arrival rule");
            yield return Until(() => doors == null || !doors.Shut, 3f);
            Assert.That(doors == null || !doors.Shut, Is.True, "the doors open again");
            _levels.BackToPracticeRoom();
        }
    }
}
