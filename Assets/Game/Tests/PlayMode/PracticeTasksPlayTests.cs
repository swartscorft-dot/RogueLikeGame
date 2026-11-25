using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 133 (the practice room teaches her kit, his tick of 2026-11-25 "Øvingsrom med oppgaver"), in the ControlRoom scene
    /// through her intents: the whole list run to its end by really doing each thing (walking, shooting the dummy, the dash, each
    /// starting skill and its clean press, marks put on and spent, the duck behind the low prop put down for it, focus, the start
    /// menu opened); each task done by nothing else (a skill pressed on its cooldown, a miss, a press that was not clean, a tap with
    /// no prop, focus pressed with an empty gauge, a shot on the dash's task); the keys T and H on a test keyboard; ending it early
    /// leaves the room as today; the switch off does nothing at all; the saved flag (his PlayerPrefs value saved and restored round
    /// the one test that writes it); the plate clear of her and of the skill strip with every word inside it at 1920 by 1080 and 1280
    /// by 720; a frame with nothing changing allocates nothing.
    /// </summary>
    public class PracticeTasksPlayTests
    {
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        GeneratedLevels _levels;
        StartMenu _menu;
        PracticeTasks _tasks;
        Keyboard _keyboard;
        string _saves;
        bool _hadFlag;
        int _flagBefore;

        static readonly Vector3 DummyA = new Vector3(4f, 0f, 3f), DummyB = new Vector3(-4f, 0f, 3f), DummyC = new Vector3(0f, 0f, 10f);
        static readonly Vector3 ByA = new Vector3(2f, 0f, 1.5f), ByB = new Vector3(-2f, 0f, 1.5f), SouthOfC = new Vector3(0f, 0f, 6.5f);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // His saved flag is read here and put back in the tear down, whatever a test does (the Editor's tests share his values).
            _hadFlag = PlayerPrefs.HasKey(PracticeTasks.DoneKey);
            _flagBefore = PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0);
            Time.timeScale = 1f;
            _saves = Path.Combine(Path.GetTempPath(), "PracticeTasksPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _menu = StartMenu.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _menu.TestKeyboard = _keyboard;
            _levels.TestKeyboard = _keyboard;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_tasks != null) Object.Destroy(_tasks.gameObject);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
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

        /// <summary>A test's own: nothing read or written in PlayerPrefs, the flag in memory, not done before.</summary>
        PracticeTasks Make()
        {
            _tasks = PracticeTasks.Ensure();
            _tasks.TestKeyboard = _keyboard;
            _tasks.UsesSavedFlag = false;
            _tasks.DoneBefore = false;
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

        int Slot(string name) => _player.SlotOf(name);
        PracticeTask Now => _tasks.Run?.Current;
        bool OnKind(PracticeTaskKind kind) => Now != null && Now.Kind == kind;

        /// <summary>Done with the task at <paramref name="index"/>: ticked, or gone on past it.</summary>
        bool DoneWith(int index) => _tasks.Run == null || _tasks.Run.Index != index || _tasks.Run.Ticked;

        /// <summary>Skip on to the first task of this kind (and skill).</summary>
        void SkipTo(PracticeTaskKind kind, int slot = -1)
        {
            for (int i = 0; i < 40 && _tasks.Run != null && !(Now.Kind == kind && (slot < 0 || Now.Slot == slot)); i++) _tasks.Skip();
            Assert.That(Now, Is.Not.Null, "on " + kind);
            Assert.That(Now.Kind, Is.EqualTo(kind));
        }

        IEnumerator NextTask(int index)
        {
            yield return Until(() => _tasks.Run == null || _tasks.Run.Index != index, 4f);
        }

        void Ready(int slot) => _player.SpendCooldownForTests(slot, 0f);

        IEnumerator Shots(Vector3 at, Func<bool> until, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!until() && Time.realtimeSinceStartup < end)
            {
                _player.AimAt(at);
                _player.Shoot(at);
                yield return Seconds(0.12f);
            }
        }

        /// <summary>A whole chain of a recast skill at a point: each press in its window, the last one clean when asked, else at once.</summary>
        IEnumerator Chain(int slot, Vector3 at, bool cleanLast)
        {
            int presses = _player.Skills[slot].RecastCount;
            Ready(slot);
            _player.AimAt(at);
            yield return Until(() => !_player.IsDashing && _player.RecastSlot < 0, 2f); // free: no chain running
            Assert.That(_player.PressSkill(slot, at), Is.Not.EqualTo(IntentResult.Rejected), "the chain's first press");
            yield return Until(() => _player.RecastSlot == slot, 2f);
            for (int press = 2; press <= presses; press++)
            {
                bool last = press == presses;
                int before = press - 1;
                // The window of THIS press: the press before it has fired (a press made while the one before still waits is lost).
                yield return Until(() => _player.RecastSlot == slot && _player.RecastShots >= before && _player.RecastWindowOpenFor(slot)
                                         && (!(last && cleanLast) || _player.RecastPressWouldBeClean), 3f);
                _player.AimAt(at);
                _player.PressSkill(slot, at);
                yield return Until(() => _player.RecastShots >= press || _player.RecastSlot != slot, 2f);
            }
        }

        /// <summary>Does the task on the plate by really doing it, and waits for its tick.</summary>
        IEnumerator DoTheTask()
        {
            PracticeTask t = Now;
            int index = _tasks.Run.Index;
            switch (t.Kind)
            {
                case PracticeTaskKind.Move:
                    _player.Steer(Vector3.right);
                    yield return Until(() => DoneWith(index), 6f);
                    _player.Steer(Vector3.zero);
                    break;
                case PracticeTaskKind.Shoot:
                    _player.Teleport(ByA);
                    yield return Shots(DummyA, () => DoneWith(index), 8f);
                    break;
                case PracticeTaskKind.Dash:
                    _player.Teleport(Vector3.zero);
                    yield return null;
                    _player.DashToward(new Vector3(-4f, 0f, -2f));
                    yield return Until(() => DoneWith(index), 3f);
                    break;
                case PracticeTaskKind.UseSkill:
                case PracticeTaskKind.CleanPress:
                    if (t.Presses > 1)
                    {
                        _player.Teleport(SouthOfC);
                        yield return Seconds(0.2f);
                        yield return Chain(t.Slot, DummyC, t.Kind == PracticeTaskKind.CleanPress);
                        yield return Until(() => DoneWith(index), 2f);
                    }
                    else
                    {
                        _player.Teleport(ByA);
                        yield return Seconds(0.2f);
                        Ready(t.Slot);
                        _player.AimAt(DummyA);
                        _player.PressSkill(t.Slot, DummyA);
                        yield return Until(() => DoneWith(index), 3f);
                    }
                    break;
                case PracticeTaskKind.SpendMarks:
                    _player.Teleport(ByB);
                    yield return Seconds(0.2f);
                    yield return Chain(Slot("Triple Tap"), DummyB, false); // three marks on dummy B
                    yield return Seconds(0.5f);
                    Ready(t.Slot);
                    _player.AimAt(DummyB);
                    _player.PressSkill(t.Slot, DummyB);
                    yield return Until(() => DoneWith(index), 3f);
                    break;
                case PracticeTaskKind.Duck:
                    yield return Until(() => _tasks.PracticeProp != null, 3f);
                    Assert.That(_tasks.PracticeProp, Is.Not.Null, "the low prop is put down for the duck's task");
                    yield return Seconds(0.2f);
                    Transform prop = _tasks.PracticeProp.transform;
                    _player.Teleport(prop.position - prop.forward * 1.3f); // 0.75 m from its long side: within the duck's reach of 1 m
                    yield return Seconds(0.2f);
                    _player.HoldCover(true);
                    yield return Until(() => DoneWith(index), 3f);
                    _player.HoldCover(false);
                    break;
                case PracticeTaskKind.Focus:
                    _player.FillFocus();
                    Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
                    yield return Until(() => DoneWith(index), 2f);
                    break;
                case PracticeTaskKind.StartRun:
                    Assert.That(_menu.Open(MenuScreen.Main), Is.True, "Enter's opening of the menu from the room");
                    yield return Until(() => DoneWith(index), 2f);
                    break;
            }
        }

        // ---------------- The whole list ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheListRunsToItsEnd_EachTaskDoneByReallyDoingIt()
        {
            Make();
            yield return Until(() => _tasks.Run != null, 3f);
            Assert.That(_tasks.Run, Is.Not.Null, "the first time in the practice room the tasks begin by themselves");
            PracticeTaskRun run = _tasks.Run;
            var kinds = run.Tasks.Select(t => t.Kind).ToList();
            Assert.That(kinds.First(), Is.EqualTo(PracticeTaskKind.Move));
            Assert.That(kinds.Last(), Is.EqualTo(PracticeTaskKind.StartRun));
            Assert.That(run.Tasks.Where(t => t.Kind == PracticeTaskKind.UseSkill).Select(t => _player.Skills[t.Slot].Name),
                        Is.EqualTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }), "her four starting skills in key order");
            Assert.That(run.Tasks.Last().Keys, Is.EqualTo(new[] { "Enter" }), "the start menu is there: its key");
            var log = new List<string>();
            var reports = new List<string>();
            Action<PlayerReport> listen = r =>
            {
                if (r.Kind == PlayerReportKind.RecastPressed) reports.Add("recast " + r.Slot + " shot " + r.Shot + (r.Clean ? " clean" : ""));
                else if (r.Kind == PlayerReportKind.SkillPressed) reports.Add("press " + r.Slot + " " + r.Result);
                else if (r.Kind == PlayerReportKind.ChainEnded) reports.Add("chain end " + r.Slot + " shots " + r.Shot + (r.Early ? " early" : ""));
            };
            _player.Reported += listen;
            for (int guard = 0; guard < 30 && _tasks.Run != null; guard++)
            {
                int index = _tasks.Run.Index;
                PracticeTask t = Now;
                reports.Clear();
                yield return DoTheTask();
                log.Add(t.Kind + (t.Slot >= 0 ? " " + _player.Skills[t.Slot].Name : "") + (run.Index != index || run.Ticked || run.Over ? " done" : " NOT DONE [" + string.Join("; ", reports) + "]"));
                Assert.That(run.Index != index || run.Ticked || run.Over, Is.True, "done by doing it: " + string.Join(", ", log));
                if (_tasks.Run != null) yield return NextTask(index);
            }
            _player.Reported -= listen;
            Assert.That(run.Finished, Is.True, string.Join(", ", log));
            Assert.That(run.Done, Is.EqualTo(run.Tasks.Count), "every one done, none skipped");
            Assert.That(_tasks.Run, Is.Null);
            Assert.That(_tasks.DoneBefore, Is.True, "finished: the flag (in memory for a test's)");
            Assert.That(_tasks.PracticeProp, Is.Null, "the low prop went with its task");
        }

        // ---------------- Done by nothing else ----------------

        [UnityTest]
        public IEnumerator ASkillPressedOnItsCooldown_DoesNotTickIt_AndNeitherDoesAShotOrAMiss()
        {
            Make();
            Assert.That(_tasks.Begin(), Is.True);
            int quick = Slot("Quick Shot");
            SkipTo(PracticeTaskKind.UseSkill, quick);
            _player.Teleport(ByA);
            yield return Seconds(0.2f);
            _player.SpendCooldownForTests(quick, 8f);
            _player.AimAt(DummyA);
            Assert.That(_player.PressSkill(quick, DummyA), Is.Not.EqualTo(IntentResult.Started), "on its cooldown");
            yield return Shots(DummyA, () => false, 1f); // basic shots on the dummy
            Assert.That(_tasks.Run.Ticked, Is.False, "neither the press on the cooldown nor the basic shots");
            Ready(quick);
            _player.AimAt(new Vector3(-6f, 0f, -6f));
            _player.PressSkill(quick, new Vector3(-6f, 0f, -6f)); // away from every dummy
            yield return Seconds(0.6f);
            Assert.That(_tasks.Run.Ticked, Is.False, "a miss");
            Ready(quick);
            _player.AimAt(DummyA);
            Assert.That(_player.PressSkill(quick, DummyA), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _tasks.Run.Ticked, 2f);
            Assert.That(_tasks.Run.Ticked, Is.True, "a hit on the dummy");
        }

        [UnityTest]
        public IEnumerator ACleanPressTask_IsNotTickedByAPressThatWasNotClean()
        {
            Make();
            _tasks.Begin();
            int tap = Slot("Triple Tap");
            SkipTo(PracticeTaskKind.CleanPress, tap);
            _player.Teleport(SouthOfC);
            yield return Seconds(0.2f);
            // The last press late: at the window's end, past its clean part.
            Ready(tap);
            _player.AimAt(DummyC);
            _player.PressSkill(tap, DummyC);
            yield return Until(() => _player.RecastWindowOpenFor(tap), 2f);
            _player.PressSkill(tap, DummyC);
            yield return null;
            yield return Until(() => _player.RecastWindowOpenFor(tap) && !_player.RecastPressWouldBeClean && _player.RecastWindowProgress > 0.85f, 3f);
            _player.PressSkill(tap, DummyC);
            yield return Seconds(0.3f);
            Assert.That(_tasks.Run.Ticked, Is.False, "a late last press is not clean");
            yield return Chain(tap, DummyC, true);
            yield return Until(() => _tasks.Run.Ticked, 2f);
            Assert.That(_tasks.Run.Ticked, Is.True, "a clean last press");
        }

        [UnityTest]
        public IEnumerator TheDashTask_IsNotTickedByAShot_TheFocusTaskNotByAnEmptyGauge_TheDuckNotAwayFromItsProp()
        {
            Make();
            _tasks.Begin();
            SkipTo(PracticeTaskKind.Dash);
            _player.Teleport(ByA);
            yield return Shots(DummyA, () => false, 0.8f);
            Assert.That(_tasks.Run.Ticked, Is.False, "a shot on the dash's task");
            yield return Seconds(0.6f); // her last shot over
            var results = new List<string>();
            for (int i = 0; i < 3 && !_tasks.Run.Ticked; i++)
            {
                results.Add(_player.DashToward(new Vector3(-3f, 0f, -3f)) + (_player.IsDashing ? " dashing" : ""));
                yield return Until(() => _tasks.Run.Ticked, 1.5f);
            }
            Assert.That(_tasks.Run.Ticked, Is.True, "the dash: " + string.Join(", ", results) + " on task " + Now.Kind);

            SkipTo(PracticeTaskKind.Duck);
            yield return Until(() => _tasks.PracticeProp != null, 3f);
            Assert.That(_tasks.PracticeProp, Is.Not.Null);
            Assert.That(_tasks.PracticeProp.GetComponent<Shelter>(), Is.Not.Null, "a low prop she can duck at");
            Assert.That(_tasks.PracticeProp.GetComponent<Shelter>().IsTall, Is.False, "low: she shoots over it");
            Assert.That(_tasks.PracticeProp.GetComponent<Cover>(), Is.Null, "it stops none of her shots");
            _player.Teleport(_tasks.PracticeProp.transform.position + new Vector3(8f, 0f, -6f));
            yield return Seconds(0.2f);
            _player.HoldCover(true);
            yield return Seconds(0.3f);
            _player.HoldCover(false);
            Assert.That(_tasks.Run.Ticked, Is.False, "a tap of the key with no low prop in reach");

            SkipTo(PracticeTaskKind.Focus);
            Assert.That(_player.ActivateIdentity(), Is.Not.EqualTo(IntentResult.Started), "an empty gauge");
            yield return Seconds(0.3f);
            Assert.That(_tasks.Run.Ticked, Is.False, "focus pressed with an empty gauge");
        }

        // ---------------- The keys, ending early, the switch ----------------

        [UnityTest]
        public IEnumerator TSkips_HEnds_AndHStartsThemAgain()
        {
            Make();
            yield return Until(() => _tasks.Run != null, 3f);
            Assert.That(_tasks.Run.Index, Is.Zero);
            yield return Tap(PracticeTasks.SkipKey);
            Assert.That(_tasks.Run.Index, Is.EqualTo(1), "T skips the task");
            yield return Tap(PracticeTasks.AgainKey);
            Assert.That(_tasks.Run, Is.Null, "H ends them");
            Assert.That(_tasks.LastRun.Ended, Is.True);
            Assert.That(_tasks.DoneBefore, Is.True, "ended: the flag");
            yield return Seconds(1.5f);
            Assert.That(_tasks.Run, Is.Null, "ended, they do not come back by themselves");
            Assert.That(_tasks.AgainShown, Is.True, "the key that starts them again shows small in the room");
            yield return Tap(PracticeTasks.AgainKey);
            Assert.That(_tasks.Run, Is.Not.Null, "H starts them again");
            Assert.That(_tasks.Run.Index, Is.Zero, "from the first");
        }

        [UnityTest]
        public IEnumerator EndingEarly_LeavesTheRoomAsToday()
        {
            int sheltersBefore = Shelter.All.Count;
            int obstaclesBefore = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None).Length;
            int rootsBefore = SceneManager.GetActiveScene().rootCount;
            Make();
            _tasks.Begin();
            SkipTo(PracticeTaskKind.Duck);
            yield return Until(() => _tasks.PracticeProp != null, 3f);
            Assert.That(_tasks.PracticeProp, Is.Not.Null);
            Assert.That(Shelter.All.Count, Is.EqualTo(sheltersBefore + 1), "the duck's prop stands while its task is up");
            Assert.That(_tasks.EndTasks(), Is.True);
            yield return null;
            yield return null;
            Assert.That(_tasks.PracticeProp, Is.Null);
            Assert.That(GameObject.Find(PracticeTasks.PropName), Is.Null, "the prop is gone");
            Assert.That(Shelter.All.Count, Is.EqualTo(sheltersBefore), "no shelter left: the practice room has none, as before");
            Assert.That(Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None).Length, Is.EqualTo(obstaclesBefore));
            Assert.That(SceneManager.GetActiveScene().rootCount, Is.EqualTo(rootsBefore), "nothing of it left in the scene");
            Assert.That(_tasks.PlateShown, Is.False, "no plate");
            Assert.That(_player.IsDucked, Is.False);
            Assert.That(PlayerPrefs.HasKey(PracticeTasks.DoneKey) == _hadFlag && PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0) == _flagBefore, Is.True,
                        "a test's tasks never write his saved flag");
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_NothingBegins_NothingShows_NoKeyDoesAnything()
        {
            Make();
            _tasks.Values.On = false;
            yield return Seconds(1.6f);
            Assert.That(_tasks.Run, Is.Null, "nothing begins by itself");
            Assert.That(_tasks.Begin(), Is.False);
            yield return Tap(PracticeTasks.AgainKey);
            Assert.That(_tasks.Run, Is.Null, "H does nothing");
            Assert.That(_tasks.PlateShown, Is.False);
            Assert.That(_tasks.AgainShown, Is.False, "not even the small line");
            Assert.That(Shelter.All.Count, Is.Zero, "the practice room as before: no prop");
            // On again in the middle: the tasks as the switch says, and off in the middle stops them, the prop gone.
            _tasks.Values.On = true;
            Assert.That(_tasks.Begin(), Is.True);
            SkipTo(PracticeTaskKind.Duck);
            yield return Until(() => _tasks.PracticeProp != null, 3f);
            _tasks.Values.On = false;
            yield return null;
            yield return null;
            Assert.That(_tasks.Run, Is.Null);
            Assert.That(GameObject.Find(PracticeTasks.PropName), Is.Null);
        }

        [UnityTest]
        public IEnumerator TheTasks_WaitUnderTheMenuAndInARun_AndGoOnInTheRoom()
        {
            Make();
            _tasks.Begin();
            _tasks.Skip();
            int index = _tasks.Run.Index;
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_tasks.PlateShown, Is.False, "no plate under the menu");
            Assert.That(_tasks.Run, Is.Not.Null, "not finished: the last plate is not up");
            _menu.Escape();
            yield return null;
            yield return null;
            Assert.That(_tasks.PlateShown, Is.True, "back in the room");
            Assert.That(_tasks.Run.Index, Is.EqualTo(index), "where she was");
        }

        // ---------------- The saved flag ----------------

        [UnityTest]
        public IEnumerator TheSavedFlag_IsWrittenOnlyWhenTheyAreOver_ReadByTheNext_AndHisValueIsRestored()
        {
            PlayerPrefs.DeleteKey(PracticeTasks.DoneKey); // put back in the tear down
            _tasks = PracticeTasks.Ensure();
            _tasks.TestKeyboard = _keyboard;
            _tasks.UsesSavedFlag = true;
            yield return Until(() => _tasks.Run != null, 3f);
            Assert.That(_tasks.Run, Is.Not.Null, "no flag stored: they begin by themselves");
            Assert.That(PlayerPrefs.HasKey(PracticeTasks.DoneKey), Is.False, "nothing written while they run");
            _tasks.EndTasks();
            Assert.That(PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0), Is.EqualTo(1), "ended: the flag saved");
            Object.Destroy(_tasks.gameObject);
            yield return null;
            _tasks = PracticeTasks.Ensure();
            _tasks.TestKeyboard = _keyboard;
            _tasks.UsesSavedFlag = true;
            yield return Seconds(1.6f);
            Assert.That(_tasks.DoneBefore, Is.True, "the next game reads it");
            Assert.That(_tasks.Run, Is.Null, "and they do not begin by themselves");
            RestoreFlag();
            Assert.That(PlayerPrefs.HasKey(PracticeTasks.DoneKey), Is.EqualTo(_hadFlag), "his value restored");
            Assert.That(PlayerPrefs.GetInt(PracticeTasks.DoneKey, 0), Is.EqualTo(_flagBefore));
        }

        // ---------------- The plate on screen ----------------

        [UnityTest]
        public IEnumerator ThePlate_IsClearOfHerAndOfTheStrip_AndEveryWordIsInside_AtBothSizes()
        {
            Make();
            _tasks.Begin();
            yield return null;
            InterfaceValues v = InterfaceStyle.V;
            Camera cam = Camera.main;
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                InterfaceStyle.ScreenForPictures = size;
                cam.aspect = size.x / (float)size.y;
                float view = size.x / InterfaceRules.Scale(size.y, v);
                float stripTop = InterfaceRules.Slot(0, SkillLoadout.KeyCount, view, v).Y;
                // Her head and feet on the screen, in the layout.
                Vector3 feet = cam.WorldToViewportPoint(new Vector3(_player.Position.x, 0f, _player.Position.z));
                Vector3 head = cam.WorldToViewportPoint(new Vector3(_player.Position.x, 2.2f, _player.Position.z));
                float headY = (1f - head.y) * v.LayoutHeight;
                Assert.That(_tasks.TextOutside(), Is.Empty, size + ": every line inside its plate");
                _tasks.Begin();
                for (int i = 0; i < 30 && _tasks.Run != null; i++)
                {
                    _tasks.LayOutForTests();
                    Rect r = _tasks.PlateRect;
                    Assert.That(r.width, Is.GreaterThan(0f));
                    Assert.That(r.x, Is.GreaterThan(0f), size + " " + Now.Kind);
                    Assert.That(r.xMax, Is.LessThan(view), size + " " + Now.Kind);
                    Assert.That(r.y, Is.GreaterThan(20f), "under the top edge, the head above it inside the screen");
                    Assert.That(r.yMax + 60f, Is.LessThan(headY), size + ": the plate and its small keys over her head, never over her (" + Now.Kind + ")");
                    Assert.That(r.yMax + 60f, Is.LessThan(stripTop), size + ": never over the skill strip");
                    if (v.TopPlate)
                        Assert.That(r.x - r.height * v.Slant * 0.5f - v.HeavyOutline - v.OuterStroke, Is.GreaterThan(InterfacePlate.Whole(v).Right),
                                    size + ": clear of her health and focus at the top left (" + Now.Kind + ")");
                    _tasks.Skip();
                }
                Assert.That(feet.y, Is.InRange(0.1f, 0.9f), "she is on the screen");
            }
            cam.ResetAspect();
        }

        [UnityTest]
        public IEnumerator AFrameWithNothingChanging_AllocatesNothing()
        {
            Make();
            _tasks.Begin();
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            for (int i = 0; i < 5; i++)
            {
                _tasks.Tick(_keyboard);
                _tasks.LayOutForTests();
                yield return null;
            }
            Assert.That(() => { _tasks.Tick(_keyboard); _tasks.LayOutForTests(); }, NotAllocating, "a frame with the plate up");
            SkipTo(PracticeTaskKind.Duck);
            yield return Until(() => _tasks.PracticeProp != null, 3f);
            for (int i = 0; i < 5; i++)
            {
                _tasks.Tick(_keyboard);
                _tasks.LayOutForTests();
                yield return null;
            }
            Assert.That(() => { _tasks.Tick(_keyboard); _tasks.LayOutForTests(); }, NotAllocating, "a frame with the duck's prop standing");
            _tasks.EndTasks();
            yield return null;
            _tasks.Tick(_keyboard);
            Assert.That(() => _tasks.Tick(_keyboard), NotAllocating, "a frame with none running");
        }
    }
}
