using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the Windows player from the command line with no hand steps (work order 032, 2026-11-20):
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;this folder&gt; -executeMethod Roguelike.EditorTools.PlayerBuild.BuildWindows -logFile &lt;log&gt;
    ///
    /// into <see cref="Folder"/> inside the project folder (ignored by git), with the scenes the build settings list, as
    /// the game starts today (the ControlRoom). <see cref="BuildWindowsDevelopment"/> makes a development build beside it
    /// in <see cref="DevelopmentFolder"/>, whose profiler markers the benchmark can read.
    ///
    /// Before it builds it keeps the code's shaders in the build (<see cref="CodeMadeShaders.Ensure"/>), unless the
    /// command line has -withoutCodeMadeShaders (to show what a build without them lacks). For the build only, the
    /// player's frame timing stats are switched on, so that the benchmark can read the <see cref="FrameTimingManager"/>'s
    /// CPU and GPU times in a release build; the setting is put back as it was afterwards. In batch mode a build that
    /// fails ends Unity with exit code 1.
    /// </summary>
    public static class PlayerBuild
    {
        public const string Folder = "Build/Windows";
        public const string DevelopmentFolder = "Build/WindowsDevelopment";
        public const string ExeName = "Roguelike.exe";

        [MenuItem("Roguelike/Build/Windows player")]
        public static void BuildWindows() => Build(Folder, false);

        [MenuItem("Roguelike/Build/Windows player (development)")]
        public static void BuildWindowsDevelopment() => Build(DevelopmentFolder, true);

        static void Build(string folder, bool development)
        {
            bool withoutShaders = Environment.GetCommandLineArgs().Contains("-withoutCodeMadeShaders");
            if (!withoutShaders) CodeMadeShaders.Ensure();
            else Debug.Log("PlayerBuild: building WITHOUT the code's shaders kept (-withoutCodeMadeShaders)");

            string project = Path.GetDirectoryName(Application.dataPath);
            string exe = Path.Combine(project, folder, ExeName);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            bool timingBefore = PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timingBefore;
            }

            BuildSummary s = report.summary;
            long folderBytes = Directory.Exists(Path.GetDirectoryName(exe))
                ? new DirectoryInfo(Path.GetDirectoryName(exe)).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                    "PlayerBuild: {0} {1} in {2:0} s, {3} errors, {4} warnings, report size {5:0.0} MB, folder {6:0.0} MB, scenes [{7}] -> {8}",
                                    development ? "development" : "release", s.result, s.totalTime.TotalSeconds, s.totalErrors, s.totalWarnings,
                                    s.totalSize / (1024.0 * 1024.0), folderBytes / (1024.0 * 1024.0), string.Join(", ", scenes), exe));
            if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
