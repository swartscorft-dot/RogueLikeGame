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
    /// Work order 123, a look order's pictures and its cost. Every one of her fifteen skills (and the basic shot) played in the
    /// practice room, BEFORE (<see cref="SkillLookValues.EffectsNotShapes"/> off: today's drawings) and AFTER (on), each moment taken
    /// in both: as its key is pressed (in its startup, its charge, its hold or its cast), the frame it fires, mid-effect, and just
    /// after; Powder Keg and Smoke Round at more moments (in the air, landed, lying, the burst). Game time runs at 1/60 s a frame,
    /// so the BEFORE and AFTER of a moment are the same time after the press (two uses of the skill, the same layout: not one frame).
    /// Two cameras: the game's own (its distance, pitch and field of view, a 960 by 540 cut of its 1920 by 1080 frame round the action)
    /// and one close (the game's pitch, 13 m from the action, its whole 960 by 540 frame). Each sheet is one skill and one camera:
    /// its rows the moments in order, BEFORE on the left and AFTER on the right. And the cost: the project's benchmark's floor 5 fight
    /// with her skills fired as their cooldowns allow, the switch off and on, twice each.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_123 names a folder outside the project (otherwise it is ignored):
    ///   $env:ROGUELIKE_CAPTURE_123 = 'C:\Users\kriss\.claude-automation\runner\captures\123'
    ///   run-tests.ps1 PlayMode -Filter "SkillLookPicturesTests"
    /// </summary>
    public class SkillLookPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_123";
        const int W = 1920, H = 1080, CellW = 960, CellH = 540;
        const float Frame = 1f / 60f;
        const float CloseDistance = 13f; // m: the close camera from the action (9 m cut Powder Keg's circle off in the first pictures)
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        CombatEffects _combat;
        string _folder;
        Vector3 _stand, _ahead, _side;
        readonly Dictionary<string, Damageable> _dummies = new Dictionary<string, Damageable>();
        readonly List<string> _log = new List<string>();
        readonly List<int> _fired = new List<int>();
        // The moments of one use: label, the game camera's cut, the close camera's frame.
        readonly List<(string label, Color32[] game, Color32[] close)> _taken = new List<(string, Color32[], Color32[])>();
        Vector3 _focus;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 123)");
            Directory.CreateDirectory(_folder);
            SkillLookValues.EffectsNotShapes = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _combat = CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _levels.Values = new LevelValues();
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.enabled = false;
            _player.Reported += OnReport;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            SkillLookValues.EffectsNotShapes = true;
            if (_player != null)
            {
                _player.Reported -= OnReport;
                _player.GetComponent<PlayerHealth>().Untouchable = false;
            }
            GameSettings.Unload();
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (_combat != null) Object.Destroy(_combat.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            SmokeClouds.Clear();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.SkillFired || r.Kind == PlayerReportKind.ShotFired) _fired.Add(Time.frameCount);
        }

        FeelValues Feel => _player.Feel ?? new FeelValues();
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);
        Vector3 Ahead(float metres, float across = 0f) => _stand + _ahead * metres + _side * across;

        static Vector3 MostRoom(Vector3 from, float max)
        {
            Vector3 best = Vector3.forward;
            float room = -1f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                float r = max;
                if (NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)
                    && NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas))
                    r = Vector3.Distance(Flat(hit.position), Flat(start.position));
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        void PoseGame(Camera cam, Vector3 focus)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            float d = GameSettings.CameraDistance(Feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        void PoseClose(Camera cam, Vector3 focus)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0.6f, focus.z) - rotation * Vector3.forward * CloseDistance, rotation);
        }

        Color32[] Draw(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
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

        /// <summary>The moment now, from both cameras: the game's cut round the action, the close one whole.</summary>
        void Take(string label)
        {
            try
            {
                Camera cam = Camera.main;
                Vector3 keepPos = cam.transform.position;
                Quaternion keepRot = cam.transform.rotation;
                float keepFov = cam.fieldOfView;
                PoseGame(cam, _player.Position);
                Color32[] full = Draw(cam, W, H);
                Vector3 s = cam.WorldToViewportPoint(_focus);
                int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x * W) - CellW / 2, 0, W - CellW), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y * H) - CellH / 2, 0, H - CellH);
                var game = new Color32[CellW * CellH];
                for (int y = 0; y < CellH; y++)
                    Array.Copy(full, (y0 + y) * W + x0, game, y * CellW, CellW);
                PoseClose(cam, _focus);
                Color32[] close = Draw(cam, CellW, CellH);
                cam.transform.SetPositionAndRotation(keepPos, keepRot);
                cam.fieldOfView = keepFov;
                _taken.Add((label, game, close));
            }
            catch (Exception e) { _log.Add(label + " FAILED: " + e.Message); }
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

        /// <summary>A sheet: one row per moment (the first at the top), BEFORE left, AFTER right.</summary>
        void Sheet(string file, List<(string label, Color32[] game, Color32[] close)> before, List<(string label, Color32[] game, Color32[] close)> after, bool close)
        {
            int rows = Mathf.Min(before.Count, after.Count);
            if (rows == 0) return;
            int sw = CellW * 2 + 8, sh = rows * CellH + (rows - 1) * 8;
            var px = new Color32[sw * sh];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            for (int r = 0; r < rows; r++)
            {
                // Texture rows go up from the bottom: the first moment at the top of the picture.
                int top = sh - (r + 1) * CellH - r * 8;
                Color32[] b = close ? before[r].close : before[r].game, a = close ? after[r].close : after[r].game;
                for (int y = 0; y < CellH; y++)
                {
                    Array.Copy(b, y * CellW, px, (top + y) * sw, CellW);
                    Array.Copy(a, y * CellW, px, (top + y) * sw + CellW + 8, CellW);
                }
            }
            Write(px, sw, sh, file);
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds * 6f + 20f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds) => Until(() => false, seconds);

        /// <summary>The layout for a skill: where the dummies stand, and where she aims.</summary>
        Vector3 Lay(string name)
        {
            int k = 0;
            foreach (Damageable d in _dummies.Values) Put(d.transform, _stand - _ahead * 25f + _side * (k++ * 7f - 17f));
            Damageable a = _dummies["Dummy_A"], b = _dummies["Dummy_B"], c = _dummies["Dummy_C"];
            Vector3 aim;
            switch (name)
            {
                case "Scatter Blast":
                case "Fan Fire":
                    Put(a.transform, Ahead(3f, 0.6f));
                    Put(b.transform, Ahead(4f, -1.2f));
                    aim = Ahead(6f);
                    break;
                case "Powder Keg":
                case "Smoke Round":
                    Put(a.transform, Ahead(6.3f, 1.8f));
                    Put(b.transform, Ahead(6.9f, -1.7f));
                    aim = Ahead(6f);
                    break;
                case "Ricochet":
                    Put(a.transform, Ahead(4f));
                    Put(b.transform, Ahead(4f, 3f));
                    Put(c.transform, Ahead(7f, 3f));
                    aim = a.transform.position;
                    break;
                case "Deadeye":
                    Put(a.transform, Ahead(4f, -2f));
                    Put(b.transform, Ahead(6f, 2f));
                    Physics.SyncTransforms();
                    a.AddMarks(2, _player.Kit.MaxMarks, 30f);
                    b.AddMarks(3, _player.Kit.MaxMarks, 30f);
                    aim = Ahead(6f);
                    break;
                case "Slide":
                    aim = Ahead(4f);
                    break;
                case "Breaker Slug":
                    Put(a.transform, Ahead(4f));
                    aim = a.transform.position;
                    break;
                default:
                    Put(a.transform, Ahead(7f));
                    aim = a.transform.position;
                    break;
            }
            Physics.SyncTransforms();
            return aim;
        }

        /// <summary>One use of the skill (null: the basic shot), its moments taken as it goes.</summary>
        IEnumerator Use(string name)
        {
            _taken.Clear();
            _fired.Clear();
            _player.Teleport(_stand);
            _player.StartFresh();
            _player.AimAt(Ahead(6f));
            Vector3 aim = Lay(name ?? "");
            yield return For(0.3f);
            _focus = Vector3.Lerp(_stand, Flat(aim), name == "Powder Keg" || name == "Smoke Round" ? 0.75f : 0.55f);
            if (name == null)
            {
                _player.Shoot(aim);
                yield return Until(() => _fired.Count > 0, 2f);
                Take("fires");
                yield return For(0.05f);
                Take("mid-effect (0.05 s)");
                yield return For(0.2f);
                Take("just after (0.25 s)");
                yield break;
            }
            int slot = Slot(name);
            _player.EquipSkill(slot);
            SkillDefinition s = _player.Skills[slot];
            _player.StartFresh();
            yield return null;
            switch (s.Input)
            {
                case SkillInput.KeyMove:
                    _player.Steer(_side);
                    _player.PressSkill(slot, aim);
                    yield return For(0.05f);
                    Take("sliding (0.05 s)");
                    yield return For(0.15f);
                    Take("mid-slide (0.2 s)");
                    _player.Stop();
                    yield return Until(() => !_player.IsKeyMoving, 2f);
                    Take("ends");
                    yield return For(0.3f);
                    Take("just after (0.3 s)");
                    yield break;
                case SkillInput.Holding:
                    _player.PressSkill(slot, aim);
                    yield return For(0.5f * s.Startup);
                    Take("pressed (in its startup)");
                    yield return Until(() => _fired.Count > 0, 2f);
                    Take("its first tick");
                    yield return For(0.5f);
                    Take("held (0.5 s in)");
                    _player.ReleaseSkill(slot);
                    yield return Until(() => _player.Action == ActionKind.None, 2f);
                    yield return For(0.25f);
                    Take("just after it is let go (0.25 s)");
                    yield break;
                case SkillInput.Charge:
                    _player.PressSkill(slot, aim);
                    yield return Until(() => _player.SkillChargedSeconds >= 0.5f * s.ChargeTime, 3f);
                    Take("held to charge (key down: the aim stays)");
                    yield return Until(() => _player.SkillChargedSeconds >= 0.5f * (s.PerfectFrom + s.PerfectTo), 3f);
                    _player.ReleaseSkill(slot);
                    yield return Until(() => _fired.Count > 0, 2f);
                    Take("fires (perfect)");
                    yield return For(0.15f);
                    Take("mid-effect (0.15 s)");
                    yield return For(0.4f);
                    Take("just after (0.55 s)");
                    yield break;
                case SkillInput.Point:
                    _player.PressSkill(slot, aim);
                    _player.ReleaseSkill(slot);
                    yield return For(0.5f * s.Startup);
                    Take("pressed (thrown, in its startup)");
                    yield return Until(() => _fired.Count > 0, 2f);
                    yield return null;
                    Take("lands");
                    if (s.Fuse > 0f)
                    {
                        yield return For(0.5f * s.Fuse);
                        Take("lying (mid-fuse)");
                        int burst = _player.KegsBurst;
                        yield return Until(() => _player.KegsBurst > burst, s.Fuse + 1f);
                        Take("bursts");
                        yield return For(0.12f);
                        Take("mid-blast (0.12 s)");
                        yield return For(0.33f);
                        Take("just after the blast (0.45 s)");
                        yield return For(0.6f);
                        Take("after (1.05 s)");
                    }
                    else
                    {
                        yield return For(0.3f);
                        Take("standing (0.3 s)");
                        yield return For(1.2f);
                        Take("mid (1.5 s)");
                        yield return For(Mathf.Max(0f, s.SmokeSeconds - 1.5f - 0.15f));
                        Take("near its end (0.15 s left)");
                        yield return Until(() => SmokeClouds.Up == 0, 2f);
                        yield return For(0.1f);
                        Take("just after it clears (0.1 s)");
                    }
                    yield break;
                default:
                    _player.PressSkill(slot, aim);
                    _player.ReleaseSkill(slot);
                    if (s.OpeningMove)
                    {
                        yield return For(0.5f * s.Startup);
                        Take("pressed (the hop)");
                        yield return Until(() => _player.Action == ActionKind.None, 2f);
                        yield return For(0.1f);
                        _player.PressSkill(slot, aim);
                        _player.ReleaseSkill(slot);
                        yield return For(0.05f);
                    }
                    else
                    {
                        yield return For(0.5f * Mathf.Max(Frame, s.Startup));
                        Take(s.Input == SkillInput.Casting ? "pressed (mid-cast)" : "pressed (in its startup)");
                    }
                    yield return Until(() => _fired.Count > 0, 3f);
                    Take("fires");
                    yield return For(0.08f);
                    Take("mid-effect (0.08 s)");
                    yield return For(0.27f);
                    Take("just after (0.35 s)");
                    yield break;
            }
        }

        static readonly string[] Everything =
        {
            null, "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap",
            "Tag Round", "Breaker Slug", "Slide", "Stopping Shot", "Ricochet", "Smoke Round", "Deadeye",
        };

        [UnityTest, Timeout(3600000)]
        public IEnumerator EverySkill_BeforeAndAfter_PressedFiresMidAndJustAfter_AtTheGamesCameraAndClose()
        {
            Time.captureDeltaTime = Frame;
            _stand = _player.Position;
            _ahead = MostRoom(_stand, 20f);
            _side = Vector3.Cross(Vector3.up, _ahead);
            foreach (string n in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(n);
                if (d == null) continue;
                Damageable body = d.GetComponent<Damageable>();
                body.SetMaxHealth(10000000);
                _dummies[n] = body;
            }
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); work order 123; " +
                     $"the game's camera {GameSettings.CameraDistance(Feel.CameraDistance):0.#} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}; the close camera {CloseDistance:0} m; " +
                     "each sheet: rows the moments in order from the top, BEFORE (switch off) left, AFTER (on) right; game time at 1/60 s a frame");
            int n2 = 0;
            foreach (string name in Everything)
            {
                string file = (++n2).ToString("00", Inv) + "-" + (name ?? "Basic shot").Replace(' ', '-').ToLowerInvariant();
                SkillLookValues.EffectsNotShapes = false;
                yield return Use(name);
                var before = new List<(string, Color32[], Color32[])>(_taken);
                SkillLookValues.EffectsNotShapes = true;
                yield return Use(name);
                var after = new List<(string, Color32[], Color32[])>(_taken);
                Sheet(file + "-game.png", before, after, close: false);
                Sheet(file + "-close.png", before, after, close: true);
                _log.Add(string.Format(Inv, "{0}-game.png / -close.png: {1}: rows {2}", file, name ?? "the basic shot", string.Join(" | ", after.Select(x => x.Item1))));
                if (before.Count != after.Count) _log.Add("  (BEFORE had " + before.Count + " moments, AFTER " + after.Count + ")");
                yield return null;
            }
            Time.captureDeltaTime = 0f;
        }

        /// <summary>
        /// The project's own measure of a frame (work order 032's benchmark), its floor 5 fight, with her fifteen skills fired as their
        /// cooldowns let her (the benchmark itself fires only the basic shot), the switch off and on, twice each.
        /// </summary>
        [UnityTest, Timeout(1200000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_HerSkillsFiring_SwitchOffAndOn()
        {
            Time.captureDeltaTime = 0f;
            foreach (bool on in new[] { false, true, false, true })
            {
                SkillLookValues.EffectsNotShapes = on;
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeSkillLookCost-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", on ? "skill-look-on" : "skill-look-off", "-benchmarkScenarios", "3", "-benchmarkSeconds", "8",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                int fired = 0, presses = 0, next = 0;
                void Count(PlayerReport r) { if (r.Kind == PlayerReportKind.SkillFired) fired++; }
                PlayerCharacter her = null;
                int holding = -1;
                float letGoAt = 0f;
                float until = Time.realtimeSinceStartup + 150f;
                while (!b.Done && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    PlayerCharacter p = b.Stage != null ? b.Stage.Player : null;
                    if (p == null) continue;
                    if (her != p)
                    {
                        if (her != null) her.Reported -= Count;
                        her = p;
                        her.Reported += Count;
                        for (int i = 0; i < her.Skills.Count; i++) her.EquipSkill(i);
                    }
                    if (holding >= 0)
                    {
                        if (Time.time >= letGoAt) { her.ReleaseSkill(holding); holding = -1; }
                        continue;
                    }
                    if (her.IsLocked || her.Action == ActionKind.Skill) continue;
                    Enemy target = null;
                    float best = float.PositiveInfinity;
                    foreach (Enemy e in b.Stage.Fighting)
                    {
                        if (e == null || e.IsDead) continue;
                        float d = (e.transform.position - her.Position).sqrMagnitude;
                        if (d < best) { best = d; target = e; }
                    }
                    if (target == null) continue;
                    int count = her.Skills.Count;
                    for (int k = 0; k < count; k++)
                    {
                        int slot = (next + k) % count;
                        if (her.SkillChargesReady(slot) <= 0) continue;
                        SkillDefinition s = her.Skills[slot];
                        if (her.PressSkill(slot, target.transform.position) != IntentResult.Started) continue;
                        presses++;
                        next = slot + 1;
                        // Every skill ready again at once: she fires one after another as fast as each lets her, far above his 990 in 25 minutes.
                        for (int j = 0; j < count; j++) if (j != slot) her.SpendCooldownForTests(j, 0f);
                        if (s.Input == SkillInput.Holding) { holding = slot; letGoAt = Time.time + 0.6f; }
                        else if (s.Input == SkillInput.Charge) { holding = slot; letGoAt = Time.time + 0.9f; }
                        else her.ReleaseSkill(slot);
                        break;
                    }
                }
                if (her != null) her.Reported -= Count;
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult sc = r.Scenarios[0];
                SkillEffects fx = SkillEffects.Current;
                string line = string.Format(Inv, "BENCHMARK {0} switch {1}: {2} frames, mean {3:0.000} ms, p95 {4:0.000}, worst {5:0.000}; {6} fighting; her skills: {7} presses started, {8} hits fired (all of the run, settle included); this order's pieces: {9}",
                                            sc.Scenario, on ? "ON" : "OFF", sc.Frames, sc.FrameMs.Mean, sc.FrameMs.P95, sc.FrameMs.Max, sc.EnemiesFighting, presses, fired,
                                            fx != null ? string.Format(Inv, "{0} pooled, {1} made, {2} capped, {3} drawn", fx.Pooled, fx.Made, fx.Capped, fx.DrawnBy.Sum()) : "none made");
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }
    }
}
