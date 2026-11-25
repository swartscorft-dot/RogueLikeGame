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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 122, a look order's pictures and its cost, taken without a window the way 091, 100, 102 and 115 took theirs. It
    /// changes nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_122 names a folder outside the project
    /// (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_122 = 'C:\Users\kriss\.claude-automation\runner\captures\122'
    ///   run-tests.ps1 PlayMode -Filter "BoneBreakPicturesTests"
    ///
    /// The pictures: one strip a kind (and an elite, and the boss to show it does not break), six moments in the practice room
    /// seen from the game's angle 10 m out (NOT the game's distance): standing, after losing its skull, after losing a shin, and
    /// 0.05, 0.6 and 2 s after the killing hit (her basic shot; the bomber's from its blast); she stands 5 m west of it, so its
    /// pieces fly east. Then the benchmark's floor 5 fight a few seconds in, at the game's own view. And the cost: the benchmark's
    /// floor 5 fight with the switch off and on (three times each, alternating), its enemies given back a share of their own health
    /// so that they die in it (the benchmark itself keeps every enemy alive, so nothing would break).
    /// </summary>
    public class BoneBreakPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_122";
        const int W = 1280, H = 720, Cut = 520;
        /// <summary>The share of their own health the fight's enemies are given back, so that a few seconds hold a fight's worth of kills.</summary>
        const float FightHealthShare = 0.25f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        EnemyBrute _template;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 122)");
            Directory.CreateDirectory(_folder);
            BoneBreakValues.On = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _template = Object.FindAnyObjectByType<EnemyBrute>();
            _template.enabled = false;
            CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            BoneBreakValues.On = true;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            if (BonePieces.Current != null) Object.Destroy(BonePieces.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();

        void PoseOn(Camera cam, Vector3 point, float distance)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(point - rotation * Vector3.forward * distance, rotation);
        }

        static Color32[] Draw(Camera cam)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                return tex.GetPixels32();
            }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        static Color32[] CropAround(Camera cam, Color32[] px, Vector3 at)
        {
            Vector3 s = cam.WorldToViewportPoint(at);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x * W) - Cut / 2, 0, W - Cut), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y * H) - Cut / 2, 0, H - Cut);
            var crop = new Color32[Cut * Cut];
            for (int y = 0; y < Cut; y++)
                for (int x = 0; x < Cut; x++) crop[y * Cut + x] = px[(y0 + y) * W + x0 + x];
            return crop;
        }

        void Write(Color32[] px, int w, int h, string file, bool jpeg)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_folder, file), jpeg ? tex.EncodeToJPG(90) : tex.EncodeToPNG());
            }
            finally { Object.Destroy(tex); }
        }

        void WriteStrip(List<Color32[]> frames, string file)
        {
            int n = frames.Count, w = Cut * n;
            var strip = new Color32[w * Cut];
            for (int k = 0; k < n; k++)
                for (int y = 0; y < Cut; y++)
                    for (int x = 0; x < Cut; x++) strip[y * w + k * Cut + x] = frames[k][y * Cut + x];
            Write(strip, w, Cut, file, false);
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static void HurtTo(Enemy e, float share)
        {
            int to = Mathf.Max(1, Mathf.FloorToInt(e.Body.MaxHealth * share));
            int hit = e.Body.CurrentHealth - to;
            if (hit > 0) e.Body.TakeHit(hit);
        }

        static void Hold(Enemy e)
        {
            e.enabled = false;
            NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
        }

        string Values()
        {
            BoneBreakValues v = BoneBreakValues.Live;
            return string.Format(Inv, "the values: a part each {0:0.###} of health lost, {1} before death; throw {2} m/s + {3} a point of damage, at most {4} (spread {5}, up {6} of it + {7}), death scatter {8}, spin up to {9} rad/s; " +
                                      "at most {10} pieces, lie {11} s, sink {12} s; break again at {13} m/s; bone {14} kg/m3 x {15}, friction {16}/{17}, bounce {18}",
                                 v.PartShare, v.PartsBeforeDeath, v.ThrowBase, v.ThrowPerDamage, v.ThrowMost, v.ThrowSpread, v.ThrowUp, v.UpBase, v.DeathScatter, v.MostSpin,
                                 v.MostPieces, v.LieSeconds, v.SinkSeconds, v.ShatterSpeed, v.BoneDensity, v.FillShare, v.DynamicFriction, v.StaticFriction, v.Bounce);
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator EveryKind_Standing_LosingItsParts_AndAtThreeMomentsOfItsDeath_ThenAFloor5Fight()
        {
            Camera cam = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); {W}x{H}, strips of {Cut}x{Cut} crops; work order 122; pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");
            _log.Add(Values());
            var kinds = new (EnemyKind kind, bool elite, string name)[]
            {
                (EnemyKind.Swarmer, false, "01-swarmer"), (EnemyKind.Shooter, false, "02-shooter"), (EnemyKind.Brute, false, "03-brute"),
                (EnemyKind.Guard, false, "04-guard"), (EnemyKind.Caster, false, "05-caster"), (EnemyKind.Warder, false, "06-warder"),
                (EnemyKind.Lancer, false, "07-lancer"), (EnemyKind.Bomber, false, "08-bomber"), (EnemyKind.Brute, true, "09-elite-brute"),
                (EnemyKind.Boss, false, "10-boss"),
            };
            foreach ((EnemyKind kind, bool elite, string name) in kinds)
            {
                _levels.ClearSpawned();
                _player.Teleport(Vector3.zero);
                yield return null;
                EnemyBrute template = _template;
                template.enabled = true; // a brute is its copy: copied with its brain on, as in the game (see BoneBreakPlayTests.Spawn)
                List<Enemy> made = _levels.SpawnPractice(kind);
                template.enabled = false;
                if (made.Count == 0) { _log.Add(name + " NOT TAKEN: nothing spawned"); continue; }
                yield return null;
                yield return null;
                foreach (Enemy m in made) Hold(m);
                Enemy e = made[0];
                if (elite) EnemyFactory.MakeElite(e, 6000, 1.25f, 4f / 3f, 0.5f);
                // The pack's others out of the picture (the swarmer's), she 5 m west of it.
                for (int k = 1; k < made.Count; k++) made[k].GetComponent<NavMeshAgent>().Warp(e.transform.position + Vector3.back * (6f + k));
                _player.Teleport(new Vector3(e.transform.position.x - 5f, 0f, e.transform.position.z));
                _player.AimAt(e.transform.position);
                yield return null;
                Vector3 focus = e.transform.position + Vector3.up * 0.8f;
                Color32[] Shot() { PoseOn(cam, focus, 10f); return CropAround(cam, Draw(cam), focus); } // posed each time: the camera rig follows her between frames
                BoneBreak b = e.GetComponent<BoneBreak>();
                var frames = new List<Color32[]>();
                var said = new List<string>();
                frames.Add(Shot()); said.Add("standing");
                HurtTo(e, 0.6f);
                yield return For(0.5f);
                frames.Add(Shot()); said.Add("0.5 s after the hit to 60 % (" + (b != null ? b.PartsLost + " parts off" : "it does not break") + ")");
                HurtTo(e, 0.3f);
                yield return For(0.5f);
                frames.Add(Shot()); said.Add("0.5 s after the hit to 30 % (" + (b != null ? b.PartsLost + " parts off" : "it does not break") + ")");
                // The killing hit: her basic shot; its brain on again, so a kind that does not break plays its death as ever.
                e.enabled = true;
                int left = e.Body.CurrentHealth;
                if (left > Placeholders.BasicShotDamage) e.Body.TakeHit(left - Placeholders.BasicShotDamage);
                e.Body.TakeHit(Placeholders.BasicShotDamage);
                string from = "the killing hit";
                if (kind == EnemyKind.Bomber && b != null)
                {
                    float until = Time.realtimeSinceStartup + 8f;
                    while (!b.Broken && Time.realtimeSinceStartup < until) yield return null;
                    from = "its blast";
                }
                yield return For(0.05f);
                frames.Add(Shot()); said.Add("0.05 s after " + from);
                yield return For(0.55f);
                frames.Add(Shot()); said.Add("0.6 s after");
                yield return For(1.4f);
                frames.Add(Shot()); said.Add("2 s after");
                WriteStrip(frames, name + ".png");
                BonePieces p = BonePieces.Current;
                _log.Add(string.Format(Inv, "{0}: {1}{2}, left to right: {3}; pieces living now {4}, thrown in all {5}, broken again in all {6}; the model's pieces: {7}",
                                       name, "a ", e.LogWord, string.Join(" | ", said), p != null ? p.Live : 0, p != null ? p.Thrown : 0, p != null ? p.BrokenAgain : 0,
                                       b != null && b.Model != null ? (b.Model.Split ? "its own mesh split by its bones, " : "boxes made in code, ") + b.Model.Shapes.Count : "none"));
            }

            // The benchmark's floor 5 fight, a few seconds in, at the game's own view.
            _levels.ClearSpawned();
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            yield return null;
            yield return null;
            GiveBackHealth(stage);
            float started = Time.realtimeSinceStartup;
            int shot = 0;
            foreach (float at in new[] { 3f, 6f, 9f })
            {
                while (Time.realtimeSinceStartup < started + at) { stage.Keep(); yield return null; }
                int alive = stage.Fighting.Count(x => x != null && !x.IsDead);
                Write(Draw(Camera.main), W, H, "11-floor5-fight-" + (++shot) + ".jpg", true);
                _log.Add(string.Format(Inv, "11-floor5-fight-{0}: the benchmark's floor 5 fight (seed BENCH032, its fullest combat room), its {1} enemies given back {2:0%} of their health, she shooting at the nearest; {3:0.0} s in, {4} alive, pieces living {5} (most at once {6}), the game's own camera",
                                       shot, stage.Fighting.Count, FightHealthShare, Time.realtimeSinceStartup - started, alive, BonePieces.Current != null ? BonePieces.Current.Live : 0,
                                       BonePieces.Current != null ? BonePieces.Current.MostLive : 0));
            }
            stage.End();
        }

        void GiveBackHealth(BenchmarkStage stage)
        {
            foreach (Enemy e in stage.Fighting)
                if (e != null && e.Body != null && !e.IsDead)
                    e.Body.SetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(stage.Level.Values.HealthOf(e.Kind) * FightHealthShare)));
        }

        // ---------------- The cost ----------------

        /// <summary>The project's own measure of a frame (work order 032's benchmark), its floor 5 fight with deaths in it, the switch off and on.</summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_WithDeaths_TheSwitchOffAndOn()
        {
            foreach (bool on in new[] { false, true, false, true, false, true })
            {
                BoneBreakValues.On = on;
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeBoneCost-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", on ? "bones-on" : "bones-off", "-benchmarkScenarios", "3", "-benchmarkSeconds", "12",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                float until = Time.realtimeSinceStartup + 180f;
                while ((b.Stage == null || b.Stage.Level == null) && !b.Done && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(b.Stage, Is.Not.Null, "the benchmark staged its fight");
                BenchmarkStage stage = b.Stage;
                yield return null;
                yield return null; // every enemy has had its Start (its health read)
                GiveBackHealth(stage);
                int thrownBefore = BonePieces.Current != null ? BonePieces.Current.Thrown : 0;
                int againBefore = BonePieces.Current != null ? BonePieces.Current.BrokenAgain : 0;
                int takenBefore = BonePieces.Current != null ? BonePieces.Current.TakenFromTheOldest : 0;
                if (BonePieces.Current != null) BonePieces.Current.ResetMost();
                int fighting = stage.Fighting.Count;
                var kinds = string.Join(", ", stage.Fighting.Where(x => x != null).GroupBy(x => x.LogWord).Select(g => g.Count() + " " + g.Key));
                List<Enemy> theirs = stage.Fighting.ToList();
                while (!b.Done && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult s = r.Scenarios[0];
                BonePieces p = BonePieces.Current;
                int killed = theirs.Count(x => x == null || x.IsDead);
                string counters = string.Join(", ", s.Counters.Where(c => c.Works).Select(c => string.Format(Inv, "{0} mean {1:0.###} p99 {2:0.###} max {3:0.###} {4}", c.Name, c.Mean, c.P99, c.Max, c.Unit)));
                string line = string.Format(Inv, "BENCHMARK {0} bones {1}: {2} frames, mean {3:0.000} ms, p95 {4:0.000}, p99 {5:0.000}, worst {6:0.000}; {7} enemies in the fight ({8}) at {9:0%} health, {10} killed by its end; pieces: most at once {11}, thrown {12}, broken again {13}, taken from the oldest {14}; counters: {15}",
                                            s.Scenario, on ? "ON" : "OFF", s.Frames, s.FrameMs.Mean, s.FrameMs.P95, s.FrameMs.P99, s.FrameMs.Max, fighting, kinds, FightHealthShare, killed,
                                            p != null ? p.MostLive : 0, p != null ? p.Thrown - thrownBefore : 0, p != null ? p.BrokenAgain - againBefore : 0, p != null ? p.TakenFromTheOldest - takenBefore : 0, counters);
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                if (BonePieces.Current != null) Object.Destroy(BonePieces.Current.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            BoneBreakValues.On = true;
        }
    }
}
