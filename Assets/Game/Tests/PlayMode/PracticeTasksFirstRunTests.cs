using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 133's follow-up (Kristian, 2026-11-25: "Første start går ditt, men et tilpasset øvingsrom som ser ut som et run, sånn
    /// at neste rom blir første vanlige rom"), in the ControlRoom scene with a run of a fixed seed: a first run opens in floor 1's own
    /// entrance room dressed as the teaching room (statues that count as dummies, its doors shut, the tasks on the plate); she cannot
    /// walk out while they run; the run's clock is held; the floor laid out is the generator's, field for field, the same as a run that
    /// does not teach; H ends them and opens the door, and the next room is floor 1's first ordinary room with its ordinary fight;
    /// skipping to the last plate opens the door and walking out finishes them; the clean press's skill is ready when its task begins;
    /// leaving the run midway saves nothing and the next run teaches again; a second run starts as today; the switches off teach
    /// nothing; his saved flag is saved and restored round the one test that writes it.
    /// </summary>
    public class PracticeTasksFirstRunTests
    {
        const ulong Seed = 0x5EED1330UL;
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        GeneratedLevels _levels;
        PracticeTasks _tasks;
        Keyboard _keyboard;
        string _saves;
        bool _hadFlag;
        int _flagBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _hadFlag = PlayerPrefs.HasKey(PracticeTasks.DoneKey); // his value, put back in the tear down whatever a test does
            _flagBefore = PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0);
            Time.timeScale = 1f;
            _saves = Path.Combine(Path.GetTempPath(), "PracticeTasksFirstRunTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_tasks != null) Object.Destroy(_tasks.gameObject);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            RestoreFlag();
        }

        void RestoreFlag()
        {
            if (_hadFlag) PlayerPrefs.SetInt(PracticeTasks.DoneKey, _flagBefore);
            else PlayerPrefs.DeleteKey(PracticeTasks.DoneKey);
            PlayerPrefs.Save();
        }

        // ---------------- Helpers ----------------

        PracticeTasks Make(bool doneBefore = false)
        {
            _tasks = PracticeTasks.Ensure();
            _tasks.TestKeyboard = _keyboard;
            _tasks.UsesSavedFlag = false;
            _tasks.DoneBefore = doneBefore;
            return _tasks;
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return Until(() => _keyboard[key].isPressed, 2f);
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return Until(() => !_keyboard[key].isPressed, 2f);
            yield return null;
        }

        IEnumerator StartTheRun()
        {
            Assert.That(_levels.StartRun(Seed), Is.Not.Null, "a run of the fixed seed");
            yield return null;
            yield return null;
        }

        PracticeTask Now => _tasks.Run?.Current;

        void SkipTo(PracticeTaskKind kind, int slot = -1)
        {
            for (int i = 0; i < 40 && _tasks.Run != null && !(Now.Kind == kind && (slot < 0 || Now.Slot == slot)); i++) _tasks.Skip();
            Assert.That(Now, Is.Not.Null, "on " + kind);
            Assert.That(Now.Kind, Is.EqualTo(kind));
        }

        /// <summary>The floor as words: its map, and every room's kind, cells, props and enemies, for a field-for-field comparison.</summary>
        static string Floor(LevelLayout l)
        {
            var parts = new List<string> { l.SeedText, l.Version.ToString(), LevelMap.Draw(l) };
            foreach (LevelRoom r in l.Rooms)
            {
                parts.Add(r.Order + " " + r.Kind + " " + r.Rect.X + "," + r.Rect.Y + "," + r.Rect.Width + "," + r.Rect.Height + " theme " + r.Theme + " start " + r.PlayerStart);
                foreach (LevelProp p in r.Props) parts.Add("  prop " + p.Model + " " + p.Position.X.ToString("0.###") + "," + p.Position.Y.ToString("0.###") + " " + p.Rotation);
                for (int i = 0; i < r.EnemyPoints.Count; i++) parts.Add("  enemy " + r.GroupAt(i) + " at " + r.EnemyPoints[i].X.ToString("0.###") + "," + r.EnemyPoints[i].Y.ToString("0.###"));
            }
            return string.Join("\n", parts);
        }

        // ---------------- A first run ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator AFirstRun_OpensInTheTeachingRoom_ShutIn_TheClockHeld_TheFloorTheGenerators()
        {
            Make();
            yield return StartTheRun();
            GeneratedLevel level = _levels.Level;
            Assert.That(_tasks.Teaching, Is.True, "a first run teaches");
            Assert.That(_levels.Floor, Is.EqualTo(1));
            Assert.That(_levels.CurrentRoom.Kind, Is.EqualTo(RoomKind.Entrance), "in floor 1's own entrance room");
            Assert.That(_tasks.TeachingRoom.Order, Is.EqualTo(_levels.CurrentRoom.Order));
            Assert.That(_tasks.Run, Is.Not.Null);
            Assert.That(_tasks.Run.Tasks.Count, Is.EqualTo(13));
            Assert.That(_tasks.Run.Tasks.Last().Kind, Is.EqualTo(PracticeTaskKind.GoOn), "the last plate says the door leads on");
            Assert.That(_tasks.Run.Tasks.Where(t => t.Kind == PracticeTaskKind.UseSkill).Select(t => _player.Skills[t.Slot].Name),
                        Is.EqualTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }), "the four a run starts with");
            Assert.That(_tasks.Targets.Count, Is.EqualTo(3), "three statues to shoot");
            foreach (GameObject t in _tasks.Targets)
            {
                Assert.That(t.GetComponent<TrainingDummy>(), Is.Not.Null, "a target that does not fight back");
                Assert.That(t.GetComponent<Enemy>(), Is.Null);
                Assert.That(t.GetComponent<Cover>(), Is.Null, "it stops none of her shots");
            }
            Assert.That(_tasks.TeachingDoors.Count, Is.GreaterThan(0), "its doors");
            Assert.That(_tasks.TeachingDoors.All(d => d.Shut), Is.True, "shut");
            Assert.That(_tasks.PlateShown, Is.True);

            // She cannot walk out while they run.
            RoomDoor door = _tasks.TeachingDoors[0];
            Vector3 to = door.transform.position - _player.Position;
            _player.Steer(new Vector3(to.x, 0f, to.z).normalized);
            yield return Seconds(3f);
            _player.Steer(Vector3.zero);
            Assert.That(_levels.CurrentRoom, Is.Not.Null);
            Assert.That(_levels.CurrentRoom.Order, Is.EqualTo(_tasks.TeachingRoom.Order), "the door holds her in");
            Assert.That(_levels.RunClockSeconds, Is.LessThan(0.5f), "the run's clock held while she is taught");
            Assert.That(_levels.RoomFights.All(f => f.Starts == 0), Is.True, "no fight woke while she was taught");

            // The floor laid out is the generator's, field for field.
            string taught = Floor(level.Layout);
            Assert.That(taught, Is.EqualTo(Floor(LevelGenerator.Generate(RunSeeds.FloorSeed(Seed, 1), 1, _levels.Values))), "the generator's own floor");
            int props = level.Props.Count, enemies = level.Enemies.Count;

            // A second run (the flag set) starts as today: the same floor, nothing added, nothing shut, the clock running.
            _levels.BackToPracticeRoom();
            yield return null;
            _tasks.DoneBefore = true;
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.False, "the flag set: no teaching");
            Assert.That(Floor(_levels.Level.Layout), Is.EqualTo(taught), "the same floor field for field");
            Assert.That(_levels.Level.Props.Count, Is.EqualTo(props));
            Assert.That(_levels.Level.Enemies.Count, Is.EqualTo(enemies));
            Assert.That(GameObject.Find(PracticeTasks.TargetName + "1"), Is.Null, "no statue");
            Assert.That(Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None).Length, Is.Zero, "no door block");
            yield return Seconds(1f);
            Assert.That(_levels.RunClockSeconds, Is.GreaterThan(0.5f), "the clock runs as today");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HEndsThem_TheDoorOpens_TheNextRoomIsTheFirstOrdinaryRoomWithItsFight_TheFlagSavedAndRestored()
        {
            PlayerPrefs.DeleteKey(PracticeTasks.DoneKey); // put back in the tear down
            Make();
            _tasks.UsesSavedFlag = true;
            _tasks.DoneBefore = false;
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.True);
            yield return Tap(PracticeTasks.AgainKey);
            Assert.That(_tasks.Run, Is.Null, "H ends them");
            Assert.That(_tasks.LastRun.Ended, Is.True);
            Assert.That(PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0), Is.EqualTo(1), "ended: the flag saved");
            Assert.That(_tasks.TeachingDoorOpen, Is.True, "the door opens");
            Assert.That(_tasks.TeachingDoors.All(d => !d.Shut), Is.True);
            Assert.That(_levels.RunClockSeconds, Is.LessThan(0.5f), "still held while she is in the room");

            // Out to the first room with a fight: its ordinary fight wakes as she comes in.
            RoomFight first = _levels.RoomFights.OrderBy(f => f.Room).FirstOrDefault();
            Assert.That(first, Is.Not.Null, "floor 1 has fights");
            LevelRoom room = _levels.Level.Layout.Rooms[first.Room];
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat), "an ordinary room");
            for (int o = _tasks.TeachingRoom.Order + 1; o < room.Order; o++)
                Assert.That(_levels.Level.Layout.Rooms[o].Kind, Is.EqualTo(RoomKind.Passage), "only passages between: it is the first ordinary room");
            _player.Teleport(_levels.Level.Middle(room));
            yield return Until(() => !_tasks.Teaching, 3f);
            Assert.That(_tasks.Teaching, Is.False, "out of the room: the teaching is over");
            Assert.That(GameObject.Find(PracticeTasks.TargetName + "1"), Is.Null, "the statues went");
            yield return null;
            Assert.That(Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None).Count(d => d.Room == 0), Is.Zero, "the teaching's blocks went");
            yield return Until(() => first.On, 4f);
            Assert.That(first.On, Is.True, "its ordinary fight");
            Assert.That(_levels.RunClockSeconds, Is.GreaterThan(0f), "the run's clock runs from here");
            RestoreFlag();
            Assert.That(PlayerPrefs.HasKey(PracticeTasks.DoneKey), Is.EqualTo(_hadFlag), "his value restored");
            Assert.That(PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0), Is.EqualTo(_flagBefore));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheStatuesCountAsTargets_TheCleanPressSkillIsReady_TheLastPlateOpensTheDoor_WalkingOutFinishes()
        {
            Make();
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.True);
            SkipTo(PracticeTaskKind.Shoot);
            Vector3 target = _tasks.Targets[0].transform.position;
            Vector3 her = _player.Position, toHer = (her - target);
            toHer.y = 0f;
            _player.Teleport(target + toHer.normalized * 3f);
            yield return Seconds(0.2f);
            int index = _tasks.Run.Index;
            float end = Time.realtimeSinceStartup + 6f;
            while (_tasks.Run.Index == index && !_tasks.Run.Ticked && Time.realtimeSinceStartup < end)
            {
                _player.AimAt(target);
                _player.Shoot(target);
                yield return Seconds(0.12f);
            }
            Assert.That(_tasks.Run.Index != index || _tasks.Run.Ticked, Is.True, "basic shots on a statue tick the shooting task");

            int backstep = _player.SlotOf("Backstep");
            SkipTo(PracticeTaskKind.UseSkill, backstep);
            _player.SpendCooldownForTests(backstep, 8f);
            yield return null;
            _tasks.Skip();
            Assert.That(Now.Kind, Is.EqualTo(PracticeTaskKind.CleanPress));
            yield return null;
            yield return null;
            Assert.That(_player.PressSkill(backstep, target), Is.EqualTo(IntentResult.Started), "its skill ready when the clean press's task begins");

            SkipTo(PracticeTaskKind.GoOn);
            yield return null;
            Assert.That(_tasks.TeachingDoorOpen, Is.True, "the last plate up: the door open");
            Assert.That(_tasks.PlateShown, Is.True, "the last plate shows");
            LevelRoom next = _levels.Level.Layout.Rooms[_tasks.TeachingRoom.Order + 1];
            _player.Teleport(_levels.Level.Middle(next));
            yield return Until(() => !_tasks.Teaching, 3f);
            Assert.That(_tasks.LastRun.Finished, Is.True, "walking out finishes them");
            Assert.That(_tasks.DoneBefore, Is.True, "the flag (in memory for a test's)");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LeavingTheRunMidway_SavesNothing_AndTheNextRunTeachesAgain()
        {
            Make();
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.True);
            _levels.BackToPracticeRoom();
            yield return null;
            yield return null;
            Assert.That(_tasks.Teaching, Is.False);
            Assert.That(_tasks.Run, Is.Null);
            Assert.That(_tasks.DoneBefore, Is.False, "nothing saved");
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.True, "the next run teaches again");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheSwitchesOff_ARunStartsAsToday()
        {
            Make();
            _tasks.Values.InTheFirstRun = false;
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.False, "the first run's own bool off");
            Assert.That(_tasks.Run, Is.Null);
            Assert.That(Object.FindObjectsByType<RoomDoor>(FindObjectsSortMode.None).Length, Is.Zero);
            _levels.BackToPracticeRoom();
            yield return null;
            _tasks.Values.InTheFirstRun = true;
            _tasks.Values.On = false;
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.False, "the order's switch off");
            Assert.That(GameObject.Find(PracticeTasks.TargetName + "1"), Is.Null);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AFrameOfTheTeachingRoom_WithNothingChanging_AllocatesNothing()
        {
            Make();
            yield return StartTheRun();
            Assert.That(_tasks.Teaching, Is.True);
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    _tasks.Tick(_keyboard);
                    _tasks.LayOutForTests();
                    yield return null;
                }
                Assert.That(() => { _tasks.Tick(_keyboard); _tasks.LayOutForTests(); }, NotAllocating);
                Assert.That(_tasks.TextOutside(), Is.Empty, "every line inside its plate, clear of the run box");
            }
            finally { InterfaceStyle.ScreenForPictures = Vector2Int.zero; }
        }
    }
}
