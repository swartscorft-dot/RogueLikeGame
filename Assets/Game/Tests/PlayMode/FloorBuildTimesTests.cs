using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 081 (cleanup and speed), the build-time table: where the time of a floor's start goes, step by step
    /// (<see cref="FloorBuildClock"/>, the run's start and the build's own marks), for floors 1 to 5 of three fixed run seeds,
    /// reached as she reaches them (the run's start, then each floor's reward and, after floor 1, the focus choice), in the
    /// ControlRoom scene; and the frame the build is in and the one after it, by the engine's own clock. The first floor of the
    /// first run is the cold one (the kit's models, the materials and the icons loaded for the first time in this Unity) and is
    /// shown on its own. It prints the table and asserts nothing about milliseconds (machines differ).
    /// </summary>
    public class FloorBuildTimesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly ulong[] RunSeeds = { 0x0810081UL, 0x0810082UL, 0x0810083UL };
        const int Floors = 5;
        const int MostFrames = 600;

        PlayerCharacter _player;
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
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null; // the real mouse and keyboard must not steer a test
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        sealed class Start
        {
            public int Floor;
            public bool Cold;
            public readonly List<(string name, double ms)> Steps = new List<(string, double)>();
            public double Total, BuildFrame, NextFrame;
        }

        static Start Read(int floor, bool cold)
        {
            var s = new Start { Floor = floor, Cold = cold, Total = FloorBuildClock.TotalMs };
            for (int i = 0; i < FloorBuildClock.Count; i++) s.Steps.Add((FloorBuildClock.NameOf(i), FloorBuildClock.MsOf(i)));
            return s;
        }

        /// <summary>The frame the build was in (the next frame's delta) and the frame after it, in ms.</summary>
        static IEnumerator FrameTimes(Start s)
        {
            yield return null;
            s.BuildFrame = Time.unscaledDeltaTime * 1000.0;
            yield return null;
            s.NextFrame = Time.unscaledDeltaTime * 1000.0;
        }

        [UnityTest]
        public IEnumerator TheStartOfAFloor_StepByStep_Table()
        {
            var starts = new List<Start>();
            bool cold = true;
            foreach (ulong seed in RunSeeds)
            {
                Assert.That(_levels.StartRun(seed), Is.Not.Null, "floor 1");
                Start first = Read(1, cold);
                cold = false;
                starts.Add(first);
                yield return FrameTimes(first);
                while (_levels.Floor < Floors)
                {
                    int was = _levels.Floor;
                    _levels.FinishFloorForTests();
                    yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                    Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor reward on floor " + was);
                    Assert.That(_levels.PickReward(0), Is.True);
                    if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 (work order 025)
                    Assert.That(_levels.Floor, Is.EqualTo(was + 1), "the next floor, from the choice before it");
                    Start s = Read(_levels.Floor, false);
                    starts.Add(s);
                    yield return FrameTimes(s);
                }
                _levels.BackToPracticeRoom();
                yield return null;
            }

            // The table: the steps in the order first met, a column for the cold floor 1 and for each floor's mean over the seeds.
            var names = new List<string>();
            foreach (Start s in starts)
                foreach ((string name, double _) in s.Steps)
                    if (!names.Contains(name)) names.Add(name);
            Start coldStart = starts.First(s => s.Cold);
            List<Start> Warm(int floor) => starts.Where(s => !s.Cold && s.Floor == floor).ToList();
            double Mean(IEnumerable<Start> of, Func<Start, double> what) { List<Start> l = of.ToList(); return l.Count == 0 ? double.NaN : l.Average(what); }
            double StepOf(Start s, string name) => s.Steps.Where(x => x.name == name).Sum(x => x.ms);

            var t = new StringBuilder();
            t.AppendLine("BUILDTIMES the start of a floor, step by step, in ms (" + (Application.isEditor ? "Editor" : "player") + (Application.isBatchMode ? ", batch mode" : "") +
                         "); floor 1 cold: the first run of this Unity; the others: the mean over the warm starts (" + RunSeeds.Length + " run seeds, floor 1 of the first not among them)");
            t.AppendFormat(Inv, "BUILDTIMES {0,-44} {1,8}", "step", "1 cold");
            for (int f = 1; f <= Floors; f++) t.AppendFormat(Inv, " {0,8}", "floor " + f);
            t.AppendLine();
            foreach (string name in names)
            {
                t.AppendFormat(Inv, "BUILDTIMES {0,-44} {1,8:0.0}", name, StepOf(coldStart, name));
                for (int f = 1; f <= Floors; f++) t.AppendFormat(Inv, " {0,8:0.0}", Mean(Warm(f), s => StepOf(s, name)));
                t.AppendLine();
            }
            void Row(string label, Func<Start, double> what)
            {
                t.AppendFormat(Inv, "BUILDTIMES {0,-44} {1,8:0.0}", label, what(coldStart));
                for (int f = 1; f <= Floors; f++) t.AppendFormat(Inv, " {0,8:0.0}", Mean(Warm(f), what));
                t.AppendLine();
            }
            Row("= the start, begin to end", s => s.Total);
            Row("the frame it is in (the next frame's delta)", s => s.BuildFrame);
            Row("the frame after it", s => s.NextFrame);
            TestContext.WriteLine(t.ToString());
            Debug.Log(t.ToString());

            Assert.That(starts.Count, Is.EqualTo(RunSeeds.Length * Floors));
            Assert.That(starts.All(s => s.Steps.Count > 5), Is.True, "every start's steps were marked");
        }
    }
}
