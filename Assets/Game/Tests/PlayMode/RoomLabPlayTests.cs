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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The room lab (work order 084; Kristian ticked "Gråboks-bryter og romlab" on 2026-11-22), in the ControlRoom scene through
    /// the intents of <see cref="GeneratedLevels"/> and a virtual keyboard: every room of the library built alone by the floor's
    /// level builder without a fault, she in its passage ("LABBUILD": how long a build takes); crossing the fight's line wakes
    /// the room; clearing it gives its reward as a floor's room does, and she stays in the lab; the act's last exit room shows
    /// no end screen; a death builds the same room again; the keys (the picker, a new seed, the next and the previous room, F6
    /// again, F7 back); the greybox view in the lab; the recorder's events and the summary's table; leaving the lab leaves
    /// nothing behind; its frame allocates nothing.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames, never a frame's length.
    /// </summary>
    public class RoomLabPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong Seed = 0x0840B0B0UL;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        Keyboard _keyboard;
        PlaySessionRecorder _recorder;
        string _folder;
        bool _hadPref;
        int _pref;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            // The greybox view's setting as this machine keeps it (the developer's G keeps what it turns), put back after the test.
            _hadPref = PlayerPrefs.HasKey(GameSettings.GreyboxKey);
            _pref = PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.GreyboxKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.GreyboxKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        LabEntry Entry(LabKind kind, bool template = true) =>
            RoomLab.Entries(_levels.Values).First(e => e.Kind == kind && (e.Template != null) == template);

        GeneratedLevel Lab(LabEntry entry, int mix = 0, ulong seed = Seed)
        {
            GeneratedLevel level = _levels.StartLab(entry, mix, seed);
            Assert.That(level, Is.Not.Null, entry + " was built");
            return level;
        }

        LevelRoom RoomOf(GeneratedLevel level) => level.Layout.Rooms[1];

        /// <summary>A ground point of the room on its open floor at <paramref name="depth"/> m in from its door in, nearest the door's line.</summary>
        static Vector3 AtDepth(GeneratedLevel level, double depth)
        {
            LevelLayout l = level.Layout;
            LevelRoom room = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = room.Rect.Y * cs + depth;
            for (int k = 0; k < room.Rect.Width && l.RoomAt(x, y) != room; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            Assert.That(l.RoomAt(x, y), Is.SameAs(room), "an open point at that depth");
            return level.ToWorld(new LevelPoint(x, y));
        }

        /// <summary>A walk on the NavMesh from her start in the passage to an open cell of the room (a prop may stand on one: the first that has NavMesh).</summary>
        static bool Reachable(GeneratedLevel level)
        {
            if (!NavMesh.SamplePosition(level.PlayerStart, out NavMeshHit start, 1f, NavMesh.AllAreas)) return false;
            LevelRoom room = level.Layout.Rooms[1];
            double cs = level.Layout.CellSize;
            var path = new NavMeshPath();
            for (int y = room.Rect.Top - 1; y >= room.Rect.Y; y--)
                for (int x = room.Rect.X; x < room.Rect.Right; x++)
                {
                    if (!room.IsOpen(x, y)) continue;
                    if (!NavMesh.SamplePosition(level.ToWorld(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs)), out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
                    return NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                }
            return false;
        }

        void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        // ---------------- Every room ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryRoomOfTheLibrary_IsBuiltAlone_WithoutAFault_AndSheStartsInItsPassage()
        {
            var times = new List<float>();
            var faults = new List<string>();
            List<LabEntry> entries = RoomLab.Entries(_levels.Values);
            Assert.That(entries.Count(e => e.Template != null), Is.EqualTo(RoomTemplateLibrary.Usable(_levels.Values).Count));
            foreach (LabEntry entry in entries)
            {
                int mix = entry.Kind == LabKind.Elite ? 2 : entry.Kind == LabKind.Exit ? 1 : 0;
                GeneratedLevel level = Lab(entry, mix);
                yield return Frames(2);
                times.Add(_levels.LabBuiltMs);
                foreach (string f in RoomLab.Faults(level.Layout, RoomLab.Mixes[mix], _levels.Values)) faults.Add(entry + ": " + f);
                Assert.That(_levels.InLab && _levels.InRun, Is.True);
                Assert.That(_levels.CurrentRoom, Is.SameAs(level.Layout.Rooms[0]), entry + ": she starts in the passage");
                Assert.That(level.Layout.Rooms[0].Kind, Is.EqualTo(RoomKind.Passage));
                LevelRoom room = RoomOf(level);
                if (entry.Template != null) Assert.That(room.Template.Template, Is.SameAs(entry.Template), entry.ToString());
                Assert.That(Reachable(level), Is.True, entry + ": the room has its NavMesh, and she can walk into it from the passage");
                int enemies = level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count : 0;
                Assert.That(enemies, Is.EqualTo(room.EnemyCount), entry + ": its enemies stand, every one");
                if (entry.Kind != LabKind.Treasure) Assert.That(_levels.FightOf(room), Is.Not.Null, entry + ": it holds its fight");
                if (entry.Kind == LabKind.Elite) Assert.That(level.Elite, Is.Not.Null, entry + ": its elite");
                if (entry.Kind == LabKind.Treasure) Assert.That(_levels.Chests(), Has.Count.EqualTo(1), entry + ": its chest");
                Assert.That(level.KitRoot != null && level.Mood != null && level.Cave != null, Is.True, entry + ": built with the kit, the mood and the cave, as a floor");
            }
            times.Sort();
            Debug.Log(string.Format(Inv, "LABBUILD {0} lab rooms built in the Editor's player: fastest {1:0} ms, median {2:0} ms, slowest {3:0} ms",
                                    times.Count, times[0], times[times.Count / 2], times[times.Count - 1]));
            Assert.That(faults, Is.Empty, "every lab room is a room of a floor");
        }

        // ---------------- It plays as a floor's room ----------------

        [UnityTest]
        public IEnumerator CrossingTheLine_WakesTheRoom_AndClearingIt_HealsHer_AndSheStaysInTheLab()
        {
            GeneratedLevel level = Lab(Entry(LabKind.Ordinary));
            LevelRoom room = RoomOf(level);
            yield return Frames(2);
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null);
            Assert.That(fight.On, Is.False, "asleep while she is in the passage");
            LevelDoor door = level.Layout.Doors[0];
            double point = RoomFightRules.PointDepth(level.Layout, room, door, _levels.Values.Fights);
            Assert.That(point, Is.EqualTo(_levels.Values.Fights.PointIn).Within(1e-4), "the line 5 m in from the passage's door, as in a floor");
            Assert.That(RoomFightRules.PastThePoint(level.Layout, room, door, level.ToLevel(AtDepth(level, point - 1.0)), _levels.Values.Fights), Is.False, "short of it: not past");
            Vector3 past = AtDepth(level, point + 1.0);
            Assert.That(RoomFightRules.PastThePoint(level.Layout, room, door, level.ToLevel(past), _levels.Values.Fights), Is.True);
            _player.Teleport(past);
            yield return Until(() => fight.On, 30);
            Assert.That(fight.On, Is.True, "past the line: the room wakes");
            Assert.That(fight.StartedBy, Is.EqualTo(RoomFightStart.Point).Or.EqualTo(RoomFightStart.Seen), "by its point (or one of them saw her the same frame)");
            Assert.That(_levels.FightDoor, Is.SameAs(door), "measured from the passage's door");
            Assert.That(level.EnemiesByRoom[room.Order].All(e => e.Fight == fight), Is.True, "every enemy of it in the fight");

            _health.Untouchable = false;
            _health.TakeHit(_health.Max / 2);
            _health.Untouchable = true;
            int hurt = _health.Current;
            Kill(level.EnemiesByRoom[room.Order]);
            yield return Until(() => _levels.LabCleared, 60);
            yield return Frames(2);
            Assert.That(_levels.LabCleared, Is.True, "cleared");
            Assert.That(_health.Current, Is.GreaterThan(hurt), "the room's heal, as a floor's room gives it");
            Assert.That(_levels.InLab && _levels.Level == level, Is.True, "she stays in the lab");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
        }

        [UnityTest]
        public IEnumerator TheEliteRoomsUpgrade_TheExitRoomsReward_AndTheTreasureRoomsChest_AsAFloorsRoomsGiveThem()
        {
            // The elite room: its upgrade.
            GeneratedLevel elite = Lab(Entry(LabKind.Elite), mix: 2);
            yield return Frames(2);
            _levels.FinishEliteRoomForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade, 60);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite room's upgrade");
            yield return new WaitForSecondsRealtime(0.5f); // past the choice's guard
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(_levels.InLab && _levels.Level == elite && _levels.Choice == GeneratedLevels.ChoiceKind.None, Is.True, "taken: she stays in the lab");

            // The exit room of floor 3: the floor's reward, and no next floor.
            GeneratedLevel exit = Lab(Entry(LabKind.Exit), mix: 1);
            yield return Frames(2);
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 60);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor's reward");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_levels.PickReward(0), Is.True);
            yield return Frames(2);
            Assert.That(_levels.InLab && _levels.Level == exit && _levels.Floor == 3, Is.True, "no next floor: the lab room still");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no focus choice");

            // The act's last exit room: no end screen.
            GeneratedLevel last = Lab(Entry(LabKind.Exit), mix: 2);
            yield return Frames(2);
            Assert.That(last.Boss, Is.Not.Null, "the act boss in floor 5's exit room");
            _levels.FinishFloorForTests();
            yield return Frames(5);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.None), "no act-cleared screen in the lab");
            Assert.That(_levels.InLab, Is.True);
            Assert.That(_levels.Notice, Does.Contain("act's last exit room"));

            // A treasure room: its chest opens.
            Lab(Entry(LabKind.Treasure, template: false), mix: 1);
            yield return Frames(2);
            GeneratedLevel.PlacedProp chest = _levels.Chests().Single();
            Assert.That(_levels.OpenChest(chest), Is.True, "its chest opens, as in a run");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(_levels.LabCleared, Is.True, "a treasure room is cleared by its chest");
        }

        [UnityTest]
        public IEnumerator ADeathInTheLab_BuildsTheSameRoomAgain_HerHealthFull()
        {
            GeneratedLevel level = Lab(Entry(LabKind.Ordinary));
            yield return Frames(2);
            int builds = _levels.LabBuilds;
            _health.Untouchable = false;
            _health.TakeHit(1000000);
            yield return Until(() => _levels.LabBuilds > builds, 60);
            _health.Untouchable = true;
            Assert.That(_levels.LabBuilds, Is.EqualTo(builds + 1), "built again");
            Assert.That(_levels.Level, Is.Not.SameAs(level));
            Assert.That(_levels.LabSeed, Is.EqualTo(Seed), "the same room: its seed");
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.None), "no death screen");
            Assert.That(_health.IsAlive && _health.Current == _health.Max, Is.True, "alive, her health full");
            Assert.That(_levels.InLab, Is.True);
        }

        // ---------------- The keys ----------------

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return Until(() => _keyboard[key].isPressed, 60);
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return Until(() => !_keyboard.anyKey.isPressed, 60);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheKeys_ThePicker_ANewSeed_TheNextAndThePreviousRoom_F6Again_AndF7Back()
        {
            Assume.That(DeveloperKeys.On, Is.True, "the Editor's developer keys");
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            yield return Tap(GeneratedLevels.LabKey);
            Assert.That(_levels.PickerOpen, Is.True, "L opens the picker in the practice room");
            StringAssert.Contains("ROOM LAB", _levels.PickerText);
            yield return Tap(Key.RightArrow);
            Assert.That(_levels.PickerKind, Is.EqualTo(LabKind.Elite), "right: the next kind");
            yield return Tap(Key.DownArrow);
            yield return Tap(Key.RightArrow);
            Assert.That(_levels.PickerRoom, Is.EqualTo(1), "the second elite room");
            yield return Tap(Key.DownArrow);
            yield return Tap(Key.RightArrow);
            Assert.That(_levels.PickerMix, Is.EqualTo(1), "floor 3's mix");
            yield return Tap(Key.Enter);
            Assert.That(_levels.InLab, Is.True, "Enter builds it");
            Assert.That(_levels.PickerOpen, Is.False);
            Assert.That(_levels.LabEntry.Kind, Is.EqualTo(LabKind.Elite));
            Assert.That(_levels.LabEntry.Template, Is.SameAs(RoomLab.EntriesOf(LabKind.Elite, _levels.Values)[1].Template));
            Assert.That(_levels.Floor, Is.EqualTo(3));
            ulong seed = _levels.LabSeed;
            LabEntry entry = _levels.LabEntry;

            yield return Tap(GeneratedLevels.LabNewSeedKey);
            Assert.That(_levels.LabSeed, Is.Not.EqualTo(seed), "N: a new seed");
            Assert.That(_levels.LabEntry.Template, Is.SameAs(entry.Template), "the same room");
            seed = _levels.LabSeed;

            List<LabEntry> entries = RoomLab.Entries(_levels.Values);
            int at = entries.FindIndex(e => e.Template == entry.Template && e.Kind == entry.Kind);
            yield return Tap(GeneratedLevels.LabNextKey);
            Assert.That(_levels.LabEntry.Template, Is.SameAs(entries[(at + 1) % entries.Count].Template), ". : the next room of the list");
            Assert.That(_levels.LabSeed, Is.EqualTo(seed), "with the same seed");
            yield return Tap(GeneratedLevels.LabPreviousKey);
            Assert.That(_levels.LabEntry.Template, Is.SameAs(entry.Template), ", : back");

            GeneratedLevel before = _levels.Level;
            yield return Tap(GeneratedLevels.SameLevelKey);
            Assert.That(_levels.Level, Is.Not.SameAs(before), "F6: built again");
            Assert.That(_levels.LabSeed, Is.EqualTo(seed), "the same seed");
            Assert.That(_levels.InLab, Is.True, "F6 is the lab's again, not a run of the lab's seed");

            yield return Tap(GeneratedLevels.GreyboxKey);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True, "the greybox view works in the lab");
            StringAssert.Contains("ROOM LAB", _levels.StatusText());
            StringAssert.Contains("GREYBOX", _levels.StatusText());
            yield return Tap(GeneratedLevels.LabNewSeedKey);
            yield return Frames(2);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True, "and on the room built again");
            GameSettings.SetGreybox(false, keep: false);

            yield return Tap(GeneratedLevels.PracticeRoomKey);
            Assert.That(_levels.InLab || _levels.InRun || _levels.IsInLevel, Is.False, "F7: back in the practice room");
            StringAssert.StartsWith("Left the room lab", _levels.LastRunLine);
        }

        // ---------------- The recorder, and what is left ----------------

        [UnityTest]
        public IEnumerator TheRecorder_NotesEachLabRoom_AndTheSummaryHasItsTable()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeRoomLabTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return Frames(2);
            GeneratedLevel level = Lab(Entry(LabKind.Ordinary));
            yield return Frames(2);
            _player.Teleport(AtDepth(level, 7.0));
            yield return Frames(3);
            _health.Untouchable = false;
            _health.TakeHit(7);
            _health.Untouchable = true;
            Kill(level.EnemiesByRoom[RoomOf(level).Order]);
            yield return Until(() => _levels.LabCleared, 60);
            _levels.LabNewSeed();
            yield return Frames(2);
            _levels.BackToPracticeRoom();
            yield return Frames(2);

            List<SessionEvent> events = _recorder.Events.ToList();
            List<SessionEvent> rooms = events.Where(e => e.Type == LabRoomEvents.Room).ToList();
            Assert.That(rooms, Has.Count.EqualTo(2), "each lab room noted");
            Assert.That(rooms[0].Name, Is.EqualTo(Entry(LabKind.Ordinary).Name));
            Assert.That(rooms[0].ExtraValue("kind"), Is.EqualTo("ordinary"));
            Assert.That(rooms[0].ExtraValue("mix"), Is.EqualTo("floor 1"));
            Assert.That(rooms[0].ExtraValue("seed"), Is.EqualTo(LevelSeed.ToText(Seed)));
            Assert.That(events.Count(e => e.Type == LabRoomEvents.Clear), Is.EqualTo(1));
            Assert.That(events.Where(e => e.Type == LabRoomEvents.End).Select(e => e.Result), Is.EqualTo(new[] { "new seed", "left" }));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.RunStart), Is.Zero, "no run told: the lab is not a run in the summary");
            List<LabRoomRow> rows = LabRoomsSummary.Compute(events);
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].Clears, Is.EqualTo(1));
            Assert.That(rows[0].Damage, Is.GreaterThanOrEqualTo(7), "her damage in it");
            Assert.That(rows[0].ClearSeconds[0], Is.GreaterThanOrEqualTo(0f));
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs, Is.Empty, "and no run in the summary's runs");
            StringAssert.Contains("Room lab (work order 084)", SessionSummary.Format(stats));
        }

        static (int objects, int materials, int meshes, int textures) Count() =>
            (Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Resources.FindObjectsOfTypeAll<Material>().Length,
             Resources.FindObjectsOfTypeAll<Mesh>().Length, Resources.FindObjectsOfTypeAll<Texture2D>().Length);

        [UnityTest]
        public IEnumerator LeavingTheLab_LeavesNothingBehind_AndItsFrameAllocatesNothing()
        {
            // The same round twice: the first loads whatever loads once (the props' models and textures of these rooms), the
            // second must leave the counts as the first left them.
            IEnumerator Round(bool measure)
            {
                Lab(Entry(LabKind.Ordinary), 0, Seed);
                yield return Frames(2);
                if (measure)
                {
                    for (int i = 0; i < 10; i++) _levels.TickToolsForTests();
                    Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickToolsForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "the lab's frame allocates nothing");
                }
                GameSettings.SetGreybox(true, keep: false);
                yield return Frames(2);
                if (measure)
                {
                    for (int i = 0; i < 10; i++) _levels.TickToolsForTests();
                    Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickToolsForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "with the greybox view on: nothing");
                }
                Lab(Entry(LabKind.Ordinary), 0, Seed + 1); // a new seed
                yield return Frames(2);
                _levels.LabStep(1); // the next room
                yield return Frames(2);
                Lab(Entry(LabKind.Elite), 2, Seed);
                yield return Frames(2);
                GameSettings.SetGreybox(false, keep: false);
                yield return Frames(2);
                _levels.BackToPracticeRoom();
                yield return Frames(3);
            }
            yield return Round(false);
            (int objects, int materials, int meshes, int textures) before = Count();
            var texturesBefore = new HashSet<Texture2D>(Resources.FindObjectsOfTypeAll<Texture2D>());
            yield return Round(true);
            (int objects, int materials, int meshes, int textures) after = Count();
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (!texturesBefore.Contains(t)) Debug.Log("LABLEFT a texture more: \"" + t.name + "\"");
            Debug.Log("LABLEFT before " + before + ", after " + after);
            Assert.That(after.objects, Is.EqualTo(before.objects), "no object left behind");
            Assert.That(after.materials, Is.EqualTo(before.materials), "no material");
            Assert.That(after.meshes, Is.EqualTo(before.meshes), "no mesh");
            Assert.That(after.textures, Is.EqualTo(before.textures), "no texture");
            Assert.That(_levels.InLab || _levels.InRun, Is.False);
        }
    }
}
