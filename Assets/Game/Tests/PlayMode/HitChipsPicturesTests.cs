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
    /// Work order 127, a look order's pictures and its cost, taken without a window as 122's were. It changes nothing in the game. Runs
    /// ONLY when the environment variable ROGUELIKE_CAPTURE_127 names a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_127 = 'C:\Users\kriss\.claude-automation\runner\captures\127'
    ///   run-tests.ps1 PlayMode -Filter "HitChipsPicturesTests"
    ///
    /// Every picture is drawn at OUR camera (55 degrees, 28 m, field of view 30; Kristian 2026-11-25 likes it best) at 1920 by 1080
    /// with the game's anti-aliasing (multisampled 4x), and cut out at 100 % (no scaling): one strip a kind (and an elite) of a plain
    /// hit (her basic shot, 40) the frame after it and 0.1, 0.3 and 0.7 s after, then the same for a crit (80, told as a crit); a fan of
    /// eight pellets into a pack of swarmers; the benchmark's floor 5 fight a few seconds in, the whole frame. The log measures each
    /// spray on screen: the box its living chips span and a chip's size, in pixels. And the cost: the benchmark's floor 5 fight with the
    /// switch off and on (her basic shots), the same with every enemy given a fan of pellets ten times a second (the most chips at
    /// once), and a full ring of chips flown in code beside as many of 122's kind of pooled rigid bodies.
    /// </summary>
    public class HitChipsPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_127";
        const int W = 1920, H = 1080, Cut = 420, Samples = 4;
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
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 127)");
            Directory.CreateDirectory(_folder);
            HitChipsValues.On = true;
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
            HitChipsValues.On = true;
            BoneBreakValues.On = true;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            if (BonePieces.Current != null) Object.Destroy(BonePieces.Current.gameObject);
            if (HitChips.Current != null) Object.Destroy(HitChips.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();

        /// <summary>OUR camera on a ground point: pitch, field of view and distance the feel's own (55, 30, 28).</summary>
        void Ours(Camera cam, Vector3 ground)
        {
            cam.fieldOfView = Feel.FieldOfView;
            cam.aspect = W / (float)H; // the picture's, not the batch screen's (4:3): the first measures read 1.33 times too wide
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            float d = GameSettings.CameraDistance(Feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(ground.x, 0f, ground.z) - rotation * Vector3.forward * d, rotation);
        }

        static Color32[] Draw(Camera cam)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = Samples };
            var plain = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                Graphics.Blit(rt, plain); // resolves the samples
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = plain;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                return tex.GetPixels32();
            }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                plain.Release();
                Object.Destroy(rt);
                Object.Destroy(plain);
                Object.Destroy(tex);
            }
        }

        static Color32[] Crop(Camera cam, Color32[] px, Vector3 at, int cw, int ch)
        {
            Vector3 s = cam.WorldToViewportPoint(at);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x * W) - cw / 2, 0, W - cw), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y * H) - ch / 2, 0, H - ch);
            var crop = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++) crop[y * cw + x] = px[(y0 + y) * W + x0 + x];
            return crop;
        }

        void Write(Color32[] px, int w, int h, string file)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_folder, file), tex.EncodeToPNG());
            }
            finally { Object.Destroy(tex); }
        }

        void WriteStrip(List<Color32[]> frames, int cw, int ch, string file)
        {
            int n = frames.Count, w = cw * n;
            var strip = new Color32[w * ch];
            for (int k = 0; k < n; k++)
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++) strip[y * w + k * cw + x] = frames[k][y * cw + x];
            Write(strip, w, ch, file);
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static void Hold(Enemy e)
        {
            e.enabled = false;
            NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
        }

        /// <summary>The living chips of these targets on screen: the box they span (px at 1920 by 1080) and a chip's mean size across (px), and how many.</summary>
        static string Measure(Camera cam, ICollection<Damageable> of)
        {
            HitChips c = HitChips.Current;
            if (c == null) return "no chips";
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, size = 0f;
            int n = 0;
            for (int i = 0; i < c.Made; i++)
            {
                if (c.StateOf(i) == ChipState.Free || !of.Contains(c.SourceOf(i))) continue;
                Vector3 p = c.RendererOf(i).transform.position;
                // In the picture's own pixels (the camera's screen in batch mode is not 1920 by 1080).
                Vector3 s = Vector3.Scale(cam.WorldToViewportPoint(p), new Vector3(W, H, 1f));
                Vector3 e = Vector3.Scale(cam.WorldToViewportPoint(p + cam.transform.right * c.RendererOf(i).transform.localScale.x), new Vector3(W, H, 1f));
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
                size += Mathf.Abs(e.x - s.x);
                n++;
            }
            if (n == 0) return "0 chips living";
            return string.Format(Inv, "{0} chips living, spanning {1:0} x {2:0} px, a chip about {3:0.0} px across", n, x1 - x0, y1 - y0, size / n);
        }

        string Values()
        {
            HitChipsValues v = HitChipsValues.Live;
            return string.Format(Inv, "the values: chips {0} + {1} a point of damage, at most {2} (a crit x{3} + {4}), +{5} a further hit in a frame, at most {6} a target, {7:0.##} of them under a part off; " +
                                      "speed {8} m/s + {9} a point, crit x{10}, at most {11}, spread {12}, up {13} + {14} of it; gravity {15}, bounce {16}, lie {17} s, shrink {18} s; size {19} m (+-{20}), crit x{21}; at most {22} at once",
                                 v.ChipsLeast, v.ChipsPerDamage, v.ChipsMost, v.CritCountScale, v.CritExtra, v.ChipsPerMoreHit, v.MostPerTarget, v.PartOffShare,
                                 v.SpeedBase, v.SpeedPerDamage, v.CritSpeedScale, v.SpeedMost, v.Spread, v.UpBase, v.UpShare, v.Gravity, v.Bounce, v.LieSeconds, v.ShrinkSeconds,
                                 v.Size, v.SizeJitter, v.CritSizeScale, v.MostChips);
        }

        IEnumerator SpawnHeld(EnemyKind kind, List<Enemy> into)
        {
            _template.enabled = true; // a brute is its copy: copied with its brain on, as in the game (see BoneBreakPlayTests.Spawn)
            List<Enemy> made = _levels.SpawnPractice(kind);
            _template.enabled = false;
            yield return null;
            yield return null;
            foreach (Enemy m in made) Hold(m);
            into.AddRange(made);
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator EveryKind_APlainHitAndACrit_AFanIntoAPack_AndAFloor5Fight_AtOurCamera()
        {
            Camera cam = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); {W}x{H} at OUR camera (pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}, distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m), MSAA {Samples}x; crops of {Cut}x{Cut} at 100 %; work order 127");
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
                var made = new List<Enemy>();
                yield return SpawnHeld(kind, made);
                if (made.Count == 0) { _log.Add(name + " NOT TAKEN: nothing spawned"); continue; }
                Enemy e = made[0];
                if (elite) EnemyFactory.MakeElite(e, 6000, 1.25f, 4f / 3f, 0.5f);
                for (int k = 1; k < made.Count; k++) made[k].GetComponent<NavMeshAgent>().Warp(e.transform.position + Vector3.back * (8f + k));
                e.Body.SetMaxHealth(1000000); // no part comes off, nothing dies: the chips alone
                _player.Teleport(new Vector3(e.transform.position.x - 5f, 0f, e.transform.position.z));
                _player.AimAt(e.transform.position);
                yield return null;
                yield return null;
                Vector3 focus = e.transform.position + Vector3.up * 0.6f;
                var frames = new List<Color32[]>();
                var said = new List<string>();
                var of = new HashSet<Damageable> { e.Body };
                foreach (bool crit in new[] { false, true })
                {
                    yield return For(2.5f); // the last spray gone
                    int damage = crit ? 2 * Placeholders.BasicShotDamage : Placeholders.BasicShotDamage;
                    e.Body.TakeHit(damage);
                    if (crit) HitChips.Crit(e.Body);
                    yield return null;
                    float hitAt = Time.time;
                    foreach (float after in new[] { 0f, 0.1f, 0.3f, 0.7f })
                    {
                        while (Time.time < hitAt + after) yield return null;
                        Ours(cam, e.transform.position);
                        frames.Add(Crop(cam, Draw(cam), focus, Cut, Cut));
                        said.Add(string.Format(Inv, "{0} {1:0.00} s after: {2}", crit ? "crit" : "plain", Time.time - hitAt, Measure(cam, of)));
                    }
                }
                WriteStrip(frames, Cut, Cut, name + ".png");
                _log.Add(string.Format(Inv, "{0}: a{1} {2}{3}, she 5 m west (the shots go east); left to right: {4}", name, elite ? "n elite" : "", e.LogWord, "",
                                       string.Join(" | ", said)));
            }

            // A fan of eight pellets (12 each) into a pack of swarmers, in one frame.
            {
                _levels.ClearSpawned();
                _player.Teleport(Vector3.zero);
                yield return null;
                var pack = new List<Enemy>();
                yield return SpawnHeld(EnemyKind.Swarmer, pack);
                pack.RemoveAll(x => x == null);
                Vector3 middle = pack.Aggregate(Vector3.zero, (s, x) => s + x.transform.position) / Mathf.Max(1, pack.Count);
                foreach (Enemy x in pack) x.Body.SetMaxHealth(1000000);
                _player.Teleport(new Vector3(middle.x - 5f, 0f, middle.z));
                yield return For(0.5f);
                var of = new HashSet<Damageable>(pack.Select(x => x.Body));
                int[] pellets = new int[pack.Count];
                for (int k = 0; k < 8; k++) pellets[(k * 3) % pack.Count]++;
                for (int k = 0; k < pack.Count; k++)
                    for (int p = 0; p < pellets[k]; p++) pack[k].Body.TakeHit(12);
                yield return null;
                float hitAt = Time.time;
                var frames = new List<Color32[]>();
                var said = new List<string>();
                foreach (float after in new[] { 0f, 0.1f, 0.3f, 0.7f })
                {
                    while (Time.time < hitAt + after) yield return null;
                    Ours(cam, middle);
                    frames.Add(Crop(cam, Draw(cam), middle + Vector3.up * 0.5f, 600, 450));
                    said.Add(string.Format(Inv, "{0:0.00} s after: {1}", Time.time - hitAt, Measure(cam, of)));
                }
                WriteStrip(frames, 600, 450, "11-fan-into-a-pack.png");
                _log.Add(string.Format(Inv, "11-fan-into-a-pack: {0} swarmers, eight pellets of 12 in one frame ({1} a swarmer), she 5 m west; crops of 600x450 at 100 %; left to right: {2}",
                                       pack.Count, string.Join("/", pellets), string.Join(" | ", said)));
            }

            // The benchmark's floor 5 fight a few seconds in, the whole frame at our camera (she shoots her basic shot at the nearest).
            _levels.ClearSpawned();
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            yield return null;
            yield return null;
            float started = Time.realtimeSinceStartup;
            int shot = 0;
            foreach (float at in new[] { 3f, 5f })
            {
                while (Time.realtimeSinceStartup < started + at) { stage.Keep(); yield return null; }
                Ours(cam, _player.Position);
                var all = new HashSet<Damageable>(stage.Fighting.Where(x => x != null).Select(x => x.Body));
                Write(Draw(cam), W, H, "12-floor5-fight-" + (++shot) + ".png");
                _log.Add(string.Format(Inv, "12-floor5-fight-{0}: the benchmark's floor 5 fight (seed BENCH032, its fullest combat room, {1} enemies kept alive), she shooting her basic shot at the nearest; {2:0.0} s in; {3}; the whole frame at our camera",
                                       shot, all.Count, Time.realtimeSinceStartup - started, Measure(cam, all)));
            }
            stage.End();
        }

        // ---------------- The cost ----------------

        /// <summary>The project's own measure of a frame (work order 032's benchmark), its floor 5 fight with the switch off and on, plain and with fans of pellets.</summary>
        [UnityTest, Timeout(2400000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_TheSwitchOffAndOn_PlainAndWithFans()
        {
            foreach (bool fans in new[] { false, true })
            foreach (bool on in new[] { false, true, false, true, false, true })
            {
                HitChipsValues.On = on;
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeChipCost-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", on ? "chips-on" : "chips-off", "-benchmarkScenarios", "3", "-benchmarkSeconds", "10",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                float until = Time.realtimeSinceStartup + 180f;
                while ((b.Stage == null || b.Stage.Level == null) && !b.Done && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(b.Stage, Is.Not.Null, "the benchmark staged its fight");
                BenchmarkStage stage = b.Stage;
                yield return null;
                yield return null;
                HitChips c = HitChips.Current;
                long thrownBefore = c != null ? c.Thrown : 0;
                int takenBefore = c != null ? c.TakenFromTheOldest : 0;
                if (c != null) c.ResetMost();
                List<Enemy> theirs = stage.Fighting.Where(x => x != null).ToList();
                float nextFan = 0f;
                while (!b.Done && Time.realtimeSinceStartup < until)
                {
                    if (fans && Time.time >= nextFan)
                    {
                        // Ten times a second every enemy of the fight takes a fan of six pellets (far past her real rate): the most chips at once.
                        nextFan = Time.time + 0.1f;
                        foreach (Enemy e in theirs)
                            if (e != null && !e.IsDead)
                                for (int p = 0; p < 6; p++) e.Body.TakeHit(12);
                    }
                    yield return null;
                }
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult s = r.Scenarios[0];
                c = HitChips.Current;
                string counters = string.Join(", ", s.Counters.Where(x => x.Works && (x.Name.Contains("Update") || x.Name.Contains("CPU") || x.Name.Contains("Render") || x.Name.Contains("heap")))
                                                         .Select(x => string.Format(Inv, "{0} mean {1:0.###} p99 {2:0.###} {3}", x.Name, x.Mean, x.P99, x.Unit)));
                string line = string.Format(Inv, "BENCHMARK {0} {1} chips {2}: {3} frames, mean {4:0.000} ms, p95 {5:0.000}, p99 {6:0.000}, worst {7:0.000}; {8} enemies; chips: most at once {9}, thrown {10}, taken from the oldest {11}; counters: {12}",
                                            s.Scenario, fans ? "WITH FANS (6 pellets x every enemy x 10 a second)" : "her basic shots", on ? "ON" : "OFF", s.Frames, s.FrameMs.Mean, s.FrameMs.P95, s.FrameMs.P99, s.FrameMs.Max,
                                            theirs.Count, c != null ? c.MostLive : 0, c != null ? c.Thrown - thrownBefore : 0, c != null ? c.TakenFromTheOldest - takenBefore : 0, counters);
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                if (HitChips.Current != null) Object.Destroy(HitChips.Current.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            HitChipsValues.On = true;
        }

        /// <summary>
        /// The order's point 4, by measure: a full ring of chips (the cap) flown in code, beside as many pooled rigid bodies of 122's
        /// kind (a box collider, continuous speculative, interpolated, bone on stone, on the Ignore Raycast layer), each thrown again
        /// as it has flown its life, in the practice room; and none, as the floor. Mean and p99 of the frame.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator Cost_AFullRingOfChips_InCode_BesideAsManyRigidBodies()
        {
            int n = HitChipsValues.Live.MostChips;
            Damageable dummy = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            dummy.SetMaxHealth(100000000);
            _player.Teleport(dummy.transform.position + Vector3.left * 5f);
            const float Seconds = 6f;

            IEnumerator Timed(string what, Action each, List<string> into)
            {
                var ms = new List<float>();
                float end = Time.realtimeSinceStartup + Seconds;
                yield return null;
                while (Time.realtimeSinceStartup < end)
                {
                    each?.Invoke();
                    yield return null;
                    ms.Add(Time.unscaledDeltaTime * 1000f);
                }
                ms.Sort();
                into.Add(string.Format(Inv, "{0}: {1} frames, mean {2:0.000} ms, p99 {3:0.000} ms", what, ms.Count, ms.Average(), ms[Mathf.Min(ms.Count - 1, (int)(ms.Count * 0.99f))]));
            }

            var lines = new List<string>();
            for (int round = 0; round < 2; round++)
            {
                HitChipsValues.On = false;
                yield return Timed("none", null, lines);

                // Chips in code: a crit's spray of the most a target throws into the dummy every frame keeps the ring full.
                HitChipsValues.On = true;
                HitChips chips = HitChips.Ensure();
                yield return Timed("a full ring of chips flown in code", () => { for (int k = 0; k < 4; k++) HitChips.Hit(dummy, 1000); HitChips.Crit(dummy); }, lines);
                lines[lines.Count - 1] += string.Format(Inv, " (chips living at the end {0}, most {1})", chips.Live, chips.MostLive);
                HitChipsValues.On = false;
                yield return For(3f);

                // As many pooled rigid bodies, thrown the same way and again after the same life.
                var bodies = new List<Rigidbody>();
                var born = new List<float>();
                var stone = new PhysicsMaterial("BoneOnStone") { dynamicFriction = 0.5f, staticFriction = 0.65f, bounciness = 0.3f };
                var root = new GameObject("RigidChips");
                Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                Material m = HitChips.Current != null ? HitChips.Current.MaterialOf(ChipColour.Bone) : null;
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("RigidChip") { layer = 2 };
                    go.transform.SetParent(root.transform, false);
                    go.transform.localScale = Vector3.one * (float)HitChipsValues.Live.Size;
                    go.AddComponent<MeshFilter>().sharedMesh = cube;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = m;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    go.AddComponent<BoxCollider>().sharedMaterial = stone;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.mass = 0.03f;
                    bodies.Add(rb);
                    born.Add(-100f);
                }
                Vector3 from = dummy.transform.position + Vector3.up * 1.1f;
                var rand = new BoneBreakRandom(7);
                float life = (float)(HitChipsValues.Live.LieSeconds + HitChipsValues.Live.ShrinkSeconds + 0.6);
                yield return Timed("as many pooled rigid bodies (122's kind)", () =>
                {
                    for (int i = 0; i < bodies.Count; i++)
                    {
                        if (Time.time - born[i] < life) continue;
                        born[i] = Time.time + (float)rand.Next() * 0.2f;
                        Rigidbody rb = bodies[i];
                        rb.position = from + new Vector3((float)rand.Signed(), (float)rand.Signed(), (float)rand.Signed()) * 0.12f;
                        rb.linearVelocity = new Vector3(3f * (float)(1 + 0.35 * rand.Signed()), (float)(1.4 + 1.5 * rand.Next()), 1.7f * (float)rand.Signed());
                        rb.angularVelocity = new Vector3((float)rand.Signed(), (float)rand.Signed(), (float)rand.Signed()) * 16f;
                        rb.WakeUp();
                    }
                }, lines);
                Object.Destroy(root);
                Object.Destroy(stone);
                yield return null;
            }
            foreach (string l in lines)
            {
                string line = "RING VS RIGID BODIES (" + n + " each, practice room): " + l;
                _log.Add(line);
                Debug.Log(line);
            }
            HitChipsValues.On = true;
        }
    }
}
