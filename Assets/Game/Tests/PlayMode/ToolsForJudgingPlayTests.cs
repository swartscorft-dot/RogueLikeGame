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
    /// Work order 090, the tools for judging the floors, in the ControlRoom scene: the room lab builds a room with each of its five
    /// presets, one a floor (the "floor 3" room with a warder standing in it, the "floor 4" room with a bomber), each a room a floor's
    /// rules find nothing in; and a short recorded session (the first room woken from the passage, one of it pulled in from there, the
    /// room cleared, the pause menu up a moment, the floor's reward and the focus choice) whose summary has the new lines: where the
    /// time went, adding up to the floor's time, the screens beside it, and why the doors opened, with the pull.
    /// </summary>
    public class ToolsForJudgingPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL; // RoomDoorsPlayTests' run: a passage, then the first combat room

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;

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
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null)
            {
                _player.Stop();
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
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

        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d));

        // ---------------- The lab's presets ----------------

        /// <summary>The first of these seeds whose lab room (rules only) holds a kind: the seed the lab is then built with, so the room holds it.</summary>
        ulong SeedWith(LabEntry entry, LabMix mix, EnemyKind kind)
        {
            for (ulong s = 1; s <= 200; s++)
            {
                ulong seed = 0x0900000UL + s;
                if (RoomLab.Build(entry, mix, seed, _levels.Values).Rooms[1].EnemyGroups.Any(g => g.Kind == kind)) return seed;
            }
            Assert.Fail("no seed of two hundred gives " + mix + " a " + EnemyKinds.Word(kind));
            return 0;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheLab_BuildsARoomWithEachPreset_OneAFloor_Floor3HoldsAWarder_Floor4ABomber()
        {
            // Changed deliberately (work order 108): the floors' own five, then "floor 4 with a lancer" (the PlayMode LancerTests builds it).
            Assert.That(RoomLab.Mixes.Select(m => m.Name), Is.EqualTo(new[] { "floor 1", "floor 2", "floor 3", "floor 4", "floor 5", "floor 4 with a lancer" }), "one a floor, in order");
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Template != null && e.Kind == LabKind.Ordinary);
            for (int mix = 0; mix < RoomLab.Mixes.Length; mix++)
            {
                LabMix m = RoomLab.Mixes[mix];
                if (m.Lancers > 0) continue; // work order 108: not a floor's own preset
                ulong seed = m.Floor == 3 ? SeedWith(nave, m, EnemyKind.Warder) : m.Floor == 4 ? SeedWith(nave, m, EnemyKind.Bomber) : 0x0900090UL;
                GeneratedLevel level = _levels.StartLab(nave, mix, seed);
                Assert.That(level, Is.Not.Null, m + " was built");
                yield return Frames(2);
                Assert.That(_levels.Floor, Is.EqualTo(m.Floor), m + ": on its floor");
                LevelRoom room = level.Layout.Rooms[1];
                Assert.That(RoomLab.Faults(level.Layout, m, _levels.Values), Is.Empty, m + ": a room of a floor");
                List<Enemy> own = level.EnemiesByRoom[room.Order];
                Assert.That(own.Count, Is.EqualTo(room.EnemyCount), m + ": its enemies stand, every one");
                Assert.That(room.EnemyGroups.Select(g => g.ToString()),
                            Is.EqualTo(RoomLab.Build(nave, m, seed, _levels.Values).Rooms[1].EnemyGroups.Select(g => g.ToString())), m + ": the same seed, the same room");
                if (m.Floor == 3) Assert.That(own.Any(e => e is EnemyWarder), Is.True, "floor 3: a warder stands in it");
                if (m.Floor == 4) Assert.That(own.Any(e => e is EnemyBomber), Is.True, "floor 4: a bomber stands in it");
                if (m.Floor < 3) Assert.That(own.Any(e => e is EnemyWarder || e is EnemyBomber || e is EnemyCaster), Is.False, m + ": none of the later floors' kinds");
                TestContext.WriteLine("LABPRESETPLAY " + m + " seed " + LevelSeed.ToText(seed) + ": " + string.Join(", ", own.GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key)));
            }
            _levels.BackToPracticeRoom();
        }

        // ---------------- The summary's new lines ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator AShortRecordedSession_ItsSummary_SaysWhereTheTimeWent_AndWhyTheDoorsOpened()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeToolsForJudging_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            LevelRoom passage = layout.Rooms[1], room = layout.Rooms[2];
            Assume.That((passage.Kind, room.Kind), Is.EqualTo((RoomKind.Passage, RoomKind.Combat)));
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);

            // In the passage a while; a hit wakes the room; one of it dazed in the passage; she steps in: it is pulled in, they shut.
            _player.Teleport(InFrom(level, room, door, -3.0));
            yield return Until(() => _levels.CurrentRoom == passage, 30);
            float t = Time.time + 0.5f;
            while (Time.time < t) yield return null;
            Enemy hit = own.OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).First();
            hit.Body.TakeHit(1);
            yield return Frames(3);
            Enemy left = own.First(e => e != null && !e.IsDead && e != hit);
            Assert.That(left.GetComponent<NavMeshAgent>().Warp(InFrom(level, room, door, -3.5)), Is.True);
            left.Body.DazeFor(8f);
            _player.Teleport(InFrom(level, room, door, 2.2));
            yield return Until(() => d.Shut, 3000);
            Assert.That(d.Shut, Is.True, "shut after the pull");

            // The pause menu up a moment (real time, the game's clock standing still).
            Assert.That(_levels.Pause(), Is.True);
            float real = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < real) yield return null;
            Assert.That(_recorder.ScreenSecondsSoFar, Is.GreaterThan(0.3), "the screen's seconds counted");
            Assert.That(_levels.Resume(), Is.True);
            yield return Frames(2);

            // Cleared: they open; the floor's reward and the focus choice; floor 2.
            foreach (Enemy e in own.ToList()) if (e != null && !e.IsDead) e.Body.TakeHit(100000000);
            yield return Until(() => !d.Shut, 60);
            Assert.That(d.Shut, Is.False);
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 120);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            yield return new WaitForSecondsRealtime(0.5f); // past the choice's guard
            Assert.That(_levels.PickReward(0), Is.True);
            yield return Frames(2);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(_levels.PickFocusChoice(0), Is.True);
            }
            yield return Until(() => _levels.Floor == 2, 60);
            Assert.That(_levels.Floor, Is.EqualTo(2));
            yield return Frames(10);

            List<SessionEvent> pulls = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomDoor && e.Name == "pull").ToList();
            Assert.That(pulls.Count, Is.GreaterThanOrEqualTo(1), "the pull told");
            Assert.That(pulls.Sum(e => int.Parse(e.ExtraValue("in"), Inv)), Is.GreaterThanOrEqualTo(1), "it put one inside");
            Assert.That(pulls.All(e => e.ExtraValue("shut") == "false"), Is.True, "before they shut");
            List<SessionEvent> screens = _recorder.Events.Where(e => e.Type == SessionEventTypes.Screen).ToList();
            Assert.That(screens.Select(e => e.Name), Is.SupersetOf(new[] { "pause menu", "floor reward" }), "the screens told as they went");
            Assert.That(screens.All(e => e.ExtraValue("run") == "1" && e.ExtraValue("floor") == "1"), Is.True, "each with its run and floor");

            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            TestContext.WriteLine(summary);
            StringAssert.Contains("  where time went  run ", summary);
            StringAssert.Contains("floor 1 ", summary);
            StringAssert.Contains("screens and menus ", summary);
            StringAssert.Contains("  doors opened     cleared 1, her death 0, she left 0, an enemy outside 0;  pulls ", summary);
            StringAssert.Contains("the rare lock case: no", summary);
            SessionStats stats = SessionSummary.Compute(_recorder.Events);
            RunRow run = stats.Runs.Single();
            Assert.That(run.FloorTimes.Keys, Is.EquivalentTo(new[] { 1, 2 }));
            FloorTimeRow one = run.FloorTimes[1];
            foreach (FloorTimeRow f in run.FloorTimes.Values)
                Assert.That(f.Fights + f.Rooms + f.Passages, Is.EqualTo(f.Seconds).Within(1.0), "floor " + f.Floor + ": the parts add up to its time");
            Assert.That(one.Fights, Is.GreaterThan(0.0), "a fight on floor 1");
            Assert.That(one.Passages, Is.GreaterThan(0.0), "the passage before it");
            Assert.That(one.Screens, Is.GreaterThan(0.3), "the pause and the choices, real time");
            Assert.That(one.Seconds, Is.EqualTo(run.FloorSeconds[1]).Within(30.0), "the floor's time, to its next floor");
            Assert.That(run.Doors.PullsThatPutIn, Is.GreaterThanOrEqualTo(1));
            Assert.That(run.Doors.Cleared, Is.EqualTo(1));
            _recorder = null;
        }
    }
}
