using System;
using UnityEditor;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The benchmark in the Editor's Play mode (work order 032, 2026-11-20): the menu item sets the environment variable
    /// the game reads as it starts (<see cref="Benchmark.EnvironmentSwitch"/>) and enters Play mode; when the benchmark has
    /// written its results, Play mode ends (only for a run the menu started: a test that runs the benchmark keeps Play mode,
    /// SessionState remembering across the domain reload which it is). The numbers of an Editor run are the Editor's, not a player's.
    /// </summary>
    [InitializeOnLoad]
    public static class BenchmarkInEditor
    {
        const string FromMenu = "Roguelike.BenchmarkFromMenu";

        static BenchmarkInEditor() => EditorApplication.update += StopWhenDone;

        [MenuItem("Roguelike/Benchmark/Run in Play mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(FromMenu, true);
            Environment.SetEnvironmentVariable(Benchmark.EnvironmentSwitch, "-benchmarkLabel editor");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Roguelike/Benchmark/Run the heavy scenarios in Play mode")]
        public static void RunHeavy()
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(FromMenu, true);
            Environment.SetEnvironmentVariable(Benchmark.EnvironmentSwitch, "-benchmarkLabel editor-heavy -benchmarkScenarios 3,4");
            EditorApplication.EnterPlaymode();
        }

        static void StopWhenDone()
        {
            if (!EditorApplication.isPlaying || Benchmark.Current == null || !Benchmark.Current.Done || !SessionState.GetBool(FromMenu, false)) return;
            SessionState.SetBool(FromMenu, false);
            EditorApplication.ExitPlaymode();
        }
    }
}
