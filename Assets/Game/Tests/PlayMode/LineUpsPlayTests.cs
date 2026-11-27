using System;
using System.Collections;
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
    /// Line-ups in the game (work order 135; Kristian's tick "Ja" to a room of two kinds, the full mix kept for one big room a floor), in
    /// the ControlRoom scene through <see cref="GeneratedLevels"/>: a run's floor is the generator's version 25 floor, each combat room
    /// with its line-up and only its two kinds standing in it; the recorder writes whether line-ups were on in the session's start and,
    /// on each room's fight, which line-up the room drew; the Trials page's line off builds the next floor as version 24's, the values'
    /// own switch kept. Nothing is kept on this machine (the settings are set without keeping).
    /// </summary>
    public class LineUpsPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run: a passage, then the first combat room

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
            if (_health != null) _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            RoomTrialSettings.SetLineUps(true, keep: false);
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                if (_health != null) _health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel StartRun()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        [UnityTest]
        public IEnumerator ARunsFloor_IsTheGeneratorsVersion25Floor_EachCombatRoomOfItsLineUp()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout made = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, 1), 1, new LevelValues());
            Assert.That(LevelMap.Draw(level.Layout), Is.EqualTo(LevelMap.Draw(made)), "the generator's floor");
            var chain = LevelGeometry.MainPath(level.Layout).Where(r => r.Kind == RoomKind.Combat).ToList();
            Assert.That(chain.All(r => r.LineUp != null), Is.True, "every combat room carries its line-up");
            Assert.That(chain.Count(r => r.LineUp.Full), Is.EqualTo(1), "one full room");
            foreach (LevelRoom r in chain.Where(LevelLineUps.Fills))
            {
                var kinds = level.EnemiesByRoom[r.Order].Where(e => e != null && !e.IsElite).Select(e => e.Kind).Distinct().ToList();
                Assert.That(kinds, Is.EquivalentTo(new[] { r.LineUp.Lead, r.LineUp.Partner }), r.Letter + "" + r.Order + " (" + r.LineUp.Name + ")");
            }
        }

        [UnityTest]
        public IEnumerator TheRecorder_WritesTheLineUpsSwitch_InTheStart_AndTheLineUp_OnTheRoomsFight()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeLineUpsTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            SessionEvent start = _recorder.Events.First(e => e.Type == SessionEventTypes.SessionStart);
            Assert.That(start.ExtraValue("line_ups"), Is.EqualTo("true"), "the session's start says they are on");
            LevelRoom room = level.Layout.Rooms[2];
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat), "the first combat room, after its passage");
            Assert.That(room.LineUp, Is.Not.Null);
            // Into the room past its line: it wakes.
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5, depth = 5.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            _player.Teleport(level.ToWorld(p));
            for (int i = 0; i < 60 && !_recorder.Events.Any(e => e.Type == SessionEventTypes.RoomFight); i++) yield return null;
            SessionEvent f = _recorder.Events.FirstOrDefault(e => e.Type == SessionEventTypes.RoomFight);
            Assert.That(f, Is.Not.Null, "the room's fight started");
            Assert.That(f.ExtraValue("line_up"), Is.EqualTo(room.LineUp.Name), "the line-up it drew");
        }

        [UnityTest]
        public IEnumerator TheTrialsLineOff_BuildsTheNextFloorAsVersion24s_TheValuesOwnSwitchKept()
        {
            RoomTrialSettings.SetLineUps(false, keep: false);
            Assert.That(RoomTrialSettings.LineUpsChosen, Is.False);
            Assert.That(RoomTrialSettings.LineWords(SettingLine.LineUps), Is.EqualTo("off"));
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            Assert.That(_levels.Values.LineUps, Is.True, "the values' own switch is kept");
            LevelLayout off = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, 1), 1, new LevelValues { LineUps = false });
            Assert.That(LevelMap.Draw(level.Layout), Is.EqualTo(LevelMap.Draw(off)), "version 24's floor");
            Assert.That(level.Layout.Rooms.All(r => r.LineUp == null), Is.True, "no room has a line-up");
            RoomTrialSettings.SetLineUps(true, keep: false);
            Assert.That(RoomTrialSettings.LineWords(SettingLine.LineUps), Is.EqualTo("on"));
        }
    }
}
