using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The benchmark's runner in the Editor's Play mode (work order 032, 2026-11-20), cut short: two scenarios of half a
    /// second each, its results written as JSON and text into a folder of its own under the temp folder (never the
    /// project), every material it saw on a shader that is not the error shader, and she can be hurt again afterwards.
    /// </summary>
    public class BenchmarkRunTests
    {
        string _folder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            GeneratedLevels.Ensure();
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeBenchmarkTest-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void CleanUp()
        {
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [UnityTest]
        public IEnumerator ARunCutShort_WritesItsResults_AndEveryMaterialHasARealShader()
        {
            Benchmark b = Benchmark.Begin(new[]
            {
                Benchmark.Argument, "-benchmarkLabel", "test", "-benchmarkScenarios", "1,4", "-benchmarkSeconds", "0.5",
                "-benchmarkSettle", "0.2", "-benchmarkOut", _folder,
            });
            b.QuitWhenDone = false;
            float until = Time.realtimeSinceStartup + 30f;
            while (!b.Done && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(b.Done, Is.True, "it finished");
            Assert.That(File.Exists(b.ResultsPath), Is.True, b.ResultsPath);
            Assert.That(Path.GetFullPath(b.ResultsPath).StartsWith(Path.GetFullPath(Path.GetTempPath())), Is.True, "under the temp folder");
            Assert.That(File.Exists(Path.ChangeExtension(b.ResultsPath, ".txt")), Is.True);
            BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
            Assert.That(r.Scenarios.Count, Is.EqualTo(2));
            Assert.That(r.Scenarios[0].Scenario, Does.Contain("PracticeIdle"));
            Assert.That(r.Scenarios[1].Scenario, Does.Contain("Floor5Boss"));
            Assert.That(r.Scenarios[1].PacksCalled, Is.EqualTo(2));
            Assert.That(r.Scenarios[1].Frames, Is.GreaterThan(0));
            Assert.That(r.Scenarios[1].FrameMs.Mean, Is.GreaterThan(0));
            Assert.That(r.Materials.Count, Is.GreaterThan(0));
            foreach (BenchmarkMaterial m in r.Materials) Assert.That(m.ErrorShader, Is.False, m.Material);
            PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
            Assert.That(health.Untouchable, Is.False, "let go of her at the end");
        }
    }
}
