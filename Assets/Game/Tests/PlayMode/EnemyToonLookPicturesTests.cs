using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Work order 119, a look order's pictures (Kristian, 2026-11-25: "Samme på fiendene. Outline er for tyk. Kan vi fp henne i
    /// litt høyere oppløsning?"), taken without a window the way work order 115 took its own (<see cref="ToonLookPicturesTests"/>):
    /// each BEFORE and AFTER drawn in the same frame, so every pose is the same in both, at 1920 by 1080, saved whole (JPEG) and as
    /// a pair (PNG, BEFORE left, AFTER right) cut round what it is of. It changes nothing in the game. Runs ONLY when the
    /// environment variable ROGUELIKE_CAPTURE_119 names a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_119 = 'C:\Users\kriss\.claude-automation\runner\captures\119'
    ///   run-tests.ps1 PlayMode -Filter "EnemyToonLookPicturesTests"
    ///
    /// The pictures: her close up and at the game's distance, work order 115's line and detail calm (2.25 px, 1.5 levels) against
    /// this order's (1.25 px, 0.5 levels), and her line at four widths side by side; each enemy kind close up with the enemies'
    /// look off and on, mid wind-up where it has one (the bomber mid fuse), an elite, a hit's flash and a daze; a warded enemy
    /// beside its warder; and the benchmark's floor 1 and floor 5 fights at the game's camera, the enemies' look off and on. And
    /// the cost: the floor 5 fight's frame drawn with the enemies' look on and off by the clock round the camera's draw and its
    /// read back, and by the project's benchmark (its floor 5 fight), the enemies' look on and off.
    /// </summary>
    public class EnemyToonLookPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_119";
        const int W = 1920, H = 1080;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Work order 115's values, the BEFORE of her pictures.</summary>
        const float InkBefore = 2.25f, CalmBefore = 1.5f;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        float _ink, _calm;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 119)");
            Directory.CreateDirectory(_folder);
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            _ink = ToonLookValues.Live.InkWidth;
            _calm = ToonLookValues.Live.DetailCalm;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _levels.Values = new LevelValues();
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            if (_folder != null)
            {
                ToonLookValues.Live.InkWidth = _ink;
                ToonLookValues.Live.DetailCalm = _calm;
            }
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();
        ToonLook HerLook => _player.GetComponent<ToonLook>();

        /// <summary>The middle of her model as drawn.</summary>
        Vector3 Chest
        {
            get
            {
                ToonLook look = HerLook;
                Bounds b = new Bounds(_player.Position, Vector3.zero);
                bool any = false;
                for (int i = 0; look != null && i < look.TargetCount; i++)
                {
                    Renderer r = look.TargetRenderer(i);
                    if (r == null || !(r is SkinnedMeshRenderer)) continue;
                    if (!any) b = r.bounds; else b.Encapsulate(r.bounds);
                    any = true;
                }
                return b.center;
            }
        }

        static Vector3 MiddleOf(Enemy e) => e != null && e.Look != null && e.Look.Skin != null ? e.Look.Skin.bounds.center : e != null ? e.transform.position + Vector3.up : Vector3.zero;

        void Pose(Camera cam, Vector3 focus, float distance = 0f)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            float d = distance > 0f ? distance : GameSettings.CameraDistance(Feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        void PoseOn(Camera cam, Vector3 point, float distance)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(point - rotation * Vector3.forward * distance, rotation);
        }

        void PoseFront(Camera cam, float distance)
        {
            CharacterAnimation anim = _player.GetComponentInChildren<CharacterAnimation>(true);
            Vector3 forward = anim != null ? anim.transform.forward : _player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            cam.fieldOfView = Feel.FieldOfView;
            cam.transform.SetPositionAndRotation(Chest + forward * distance + Vector3.up * 0.15f, Quaternion.LookRotation(-forward, Vector3.up));
        }

        Color32[] Draw(Camera cam)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                if (_player != null) _player.TickLaser();
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

        void Write(Color32[] px, int w, int h, string file, bool jpeg)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_folder, file), jpeg ? tex.EncodeToJPG(92) : tex.EncodeToPNG());
            }
            finally { Object.Destroy(tex); }
        }

        /// <summary>A cut <paramref name="cw"/> by <paramref name="ch"/> of a whole frame round <paramref name="centre"/> on the screen of <paramref name="cam"/>.</summary>
        static Color32[] Cut(Camera cam, Color32[] frame, Vector3 centre, ref int cw, ref int ch)
        {
            Vector3 s = cam.WorldToViewportPoint(centre);
            s.x *= W;
            s.y *= H;
            cw = Mathf.Min(cw, W);
            ch = Mathf.Min(ch, H);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x) - cw / 2, 0, W - cw), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y) - ch / 2, 0, H - ch);
            var cut = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    cut[y * cw + x] = frame[(y0 + y) * W + x0 + x];
            return cut;
        }

        /// <summary>Cuts side by side, left to right.</summary>
        static Color32[] SideBySide(List<Color32[]> cuts, int cw, int ch)
        {
            var all = new Color32[cw * cuts.Count * ch];
            for (int i = 0; i < cuts.Count; i++)
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        all[y * cw * cuts.Count + i * cw + x] = cuts[i][y * cw + x];
            return all;
        }

        /// <summary>Every enemy's toon look follows the switch now (as each does at its next frame).</summary>
        static void TickEnemies()
        {
            foreach (Enemy e in Enemy.All)
                if (e != null && e.Look != null && e.Look.Toon != null) e.Look.Toon.Tick();
        }

        /// <summary>Her values, and her copies given them.</summary>
        void HerValues(float ink, float calm)
        {
            ToonLookValues.Live.InkWidth = ink;
            ToonLookValues.Live.DetailCalm = calm;
            HerLook.Refresh();
        }

        /// <summary>BEFORE and AFTER of the camera as it stands, in this frame: each whole, and the pair cut round <paramref name="centre"/>.</summary>
        void Pair(Camera cam, string name, string what, Action before, Action after, int cw, int ch, Vector3 centre)
        {
            try
            {
                before();
                Color32[] off = Draw(cam);
                after();
                Color32[] on = Draw(cam);
                Write(off, W, H, name + "-before.jpg", true);
                Write(on, W, H, name + "-after.jpg", true);
                int w = cw, h = ch;
                Color32[] a = Cut(cam, off, centre, ref w, ref h);
                Color32[] b = Cut(cam, on, centre, ref w, ref h);
                Write(SideBySide(new List<Color32[]> { a, b }, w, h), w * 2, h, name + "-pair.png", false);
                _log.Add(string.Format(Inv, "{0}: {1} (pair: BEFORE left, AFTER right, {2} by {3} px each cut from the 1920 by 1080 frame)", name, what, w, h));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e.Message); }
        }

        /// <summary>The enemies' look off (left) and on (right), her in the look in both.</summary>
        void EnemiesPair(Camera cam, string name, string what, int cw, int ch, Vector3 centre) =>
            Pair(cam, name, what + "; LEFT the enemies today, RIGHT the enemies in the look (her in it in both)",
                 () => { ToonLookValues.Enemies = false; TickEnemies(); },
                 () => { ToonLookValues.Enemies = true; TickEnemies(); }, cw, ch, centre);

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        string Values()
        {
            ToonLookValues v = ToonLookValues.Live;
            return string.Format(Inv, "the look's values: ink {0} px at 1080 rows (115: {1}), detail calm {2} mip levels (115: {3}), lit threshold {4}, lit tone {5}, shadow tone {6}, lamp edge {7} x{8} from {9}, ink pushed back {10} m; the enemies' switch {11}",
                                 v.InkWidth, InkBefore, v.DetailCalm, CalmBefore, v.LitThreshold, v.LitLevel, v.ShadowTint, v.LampEdgeOn, v.LampEdgeStrength, v.LampEdgeStart, v.InkDepthPush, ToonLookValues.Enemies);
        }

        // ---------------- Her ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator Her_CloseUpAndAtTheGamesDistance_115sLineAndCalm_AgainstThisOrders_AndHerLineAtFourWidths()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); {W}x{H}; work order 119; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");
            _log.Add(Values());
            HerLook.Tick();
            for (int i = 0; i < HerLook.TargetCount; i++)
            {
                Renderer r = HerLook.TargetRenderer(i);
                Texture t = HerLook.TodaysOf(i).Count > 0 && HerLook.TodaysOf(i)[0] != null ? HerLook.TodaysOf(i)[0].GetTexture("_BaseMap") : null;
                _log.Add(string.Format(Inv, "her {0} ({1}): texture {2}", r.name, r.GetType().Name,
                                       t != null ? t.name + " " + t.width + "x" + t.height + " " + (t is Texture2D t2 ? t2.format + ", " + t2.mipmapCount + " mips" : "") + ", filter " + t.filterMode + ", aniso " + t.anisoLevel + ", mip bias " + t.mipMapBias : "none"));
            }
            _log.Add("quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ", anisotropic " + QualitySettings.anisotropicFiltering + ", texture mip limit " + QualitySettings.globalTextureMipmapLimit);
            _levels.ClearSpawned();
            _player.Teleport(Vector3.zero);
            _player.AimAt(new Vector3(0f, 0f, -6f)); // facing the camera's side
            yield return new WaitForSecondsRealtime(1.5f);

            Action old = () => HerValues(InkBefore, CalmBefore), now = () => HerValues(_ink, _calm);
            PoseFront(game, 4.2f);
            Pair(game, "01-her-close-front", "her close up from the front, standing at rest in the practice room, the camera 4.2 m in front of her at her middle's height (NOT the game's view); LEFT work order 115's line 2.25 px and detail calm 1.5, RIGHT this order's 1.25 px and 0.5", old, now, 900, 1080, Chest);
            PoseOn(game, Chest, 6f);
            Pair(game, "02-her-close-game-angle", "her close up from the game's angle, the camera 6 m from her chest (NOT the game's distance); LEFT 2.25 px and 1.5, RIGHT 1.25 px and 0.5", old, now, 900, 1080, Chest);
            Pose(game, _player.Position);
            Pair(game, "03-her-game-distance", "her at the game's own distance and angle in the practice room; LEFT 2.25 px and 1.5, RIGHT 1.25 px and 0.5", old, now, 480, 480, Chest);
            Action calmOld = () => HerValues(_ink, CalmBefore);
            PoseFront(game, 2.6f);
            Pair(game, "04-her-detail-calm-only", "her face and coat close up from the front, the camera 2.6 m out (NOT the game's view): the line 1.25 px in both; LEFT detail calm 1.5, RIGHT 0.5", calmOld, now, 900, 1080, Chest + Vector3.up * 0.3f);

            // Her line at four widths side by side, at the game's distance and pulled in to 8 m.
            foreach (float d in new[] { 0f, 8f })
            {
                try
                {
                    if (d > 0f) PoseOn(game, Chest, d); else Pose(game, _player.Position);
                    var cuts = new List<Color32[]>();
                    int cw = d > 0f ? 520 : 320, ch = d > 0f ? 900 : 420;
                    foreach (float ink in new[] { InkBefore, _ink, 1f, 0.75f })
                    {
                        HerValues(ink, _calm);
                        int w = cw, h = ch;
                        cuts.Add(Cut(game, Draw(game), Chest, ref w, ref h));
                        cw = w;
                        ch = h;
                    }
                    string name = d > 0f ? "05b-her-line-widths-8m" : "05-her-line-widths";
                    Write(SideBySide(cuts, cw, ch), cw * 4, ch, name + ".png", false);
                    _log.Add(string.Format(Inv, "{0}: her ink line at 2.25, {1}, 1.0 and 0.75 px (left to right), detail calm {2}, {3} ({4} by {5} px each)", name, _ink, _calm,
                                           d > 0f ? "the camera at the game's angle pulled in to 8 m (NOT the game's view)" : "at the game's distance", cw, ch));
                }
                catch (Exception e) { _log.Add("05 FAILED: " + e.Message); }
            }
            HerValues(_ink, _calm);
            // Running: the line in motion, from one frame to the next.
            _player.Steer(Vector3.right);
            float runUntil = Time.time + 0.8f, until = Time.realtimeSinceStartup + 10f;
            while (Time.time < runUntil && Time.realtimeSinceStartup < until) yield return null;
            PoseOn(game, Chest, 7f);
            Pair(game, "06-her-running-close", string.Format(Inv, "running east at {0:0.0} m/s, the camera pulled in to 7 m (NOT the game's view); LEFT 2.25 px and 1.5, RIGHT 1.25 px and 0.5", _player.Speed), old, now, 900, 900, Chest);
            yield return Frames(3);
            PoseOn(game, Chest, 7f);
            Pair(game, "06b-her-running-close-later", "three frames later, the same", old, now, 900, 900, Chest);
            _player.Stop();
        }

        // ---------------- The enemies, kind by kind ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator EachKind_CloseUp_MidWindUp_TheEnemiesLookOffAndOn_AndAnElite_AFlash_ADaze_AndAWardBesideItsWarder()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} work order 119, each kind; " + Values());
            Vector3 home = new Vector3(8f, 0f, -6f);
            int n = 10;
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Bomber, EnemyKind.Warder, EnemyKind.Lancer, EnemyKind.Boss })
            {
                _levels.ClearSpawned();
                _player.Teleport(home);
                yield return Frames(2);
                List<Enemy> made = _levels.SpawnPractice(kind);
                if (made.Count == 0) { _log.Add(kind + " NOT SPAWNED"); continue; }
                Enemy e = made[0];
                foreach (Enemy m in made) if (m.Body != null) m.Body.SetMaxHealth(1000000);
                yield return Frames(2);
                // She stands near it, so that it starts an attack on her.
                Vector3 d = e.transform.position - _player.Position;
                d.y = 0f;
                float near = kind == EnemyKind.Shooter || kind == EnemyKind.Caster || kind == EnemyKind.Warder || kind == EnemyKind.Lancer ? 7f : 2.2f;
                _player.Teleport(e.transform.position - (d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward) * near);
                var bomber = e as EnemyBomber;
                float started = Time.realtimeSinceStartup;
                yield return Until(() => bomber != null ? bomber.FillRadius > 0.4f * bomber.Tuning.BlastRadius : e.State == EnemyState.Windup && e.Attack != null && e.WindupRemaining < e.Attack.Windup * 0.6f, 12f);
                string state = bomber != null ? string.Format(Inv, "its fuse {0:0}% burnt, swollen x{1:0.00}", 100f * bomber.FillRadius / Mathf.Max(0.01f, bomber.Tuning.BlastRadius), bomber.SwellNow)
                                              : e.State == EnemyState.Windup ? string.Format(Inv, "mid wind-up of its {0} ({1:0.00} s of {2:0.00} left)", e.Attack != null ? e.Attack.Name : "?", e.WindupRemaining, e.Attack != null ? e.Attack.Windup : 0f)
                                              : "NOT in a wind-up (" + e.State + ") after " + (Time.realtimeSinceStartup - started).ToString("0.0", Inv) + " s";
                Vector3 mid = MiddleOf(e);
                float h = Mathf.Max(1.5f, e.ModelHeight);
                PoseOn(game, mid, 3.2f + 2.2f * h);
                EnemiesPair(game, string.Format(Inv, "{0:00}-{1}", n++, EnemyKinds.Word(kind)), "the " + EnemyKinds.Word(kind) + " close up at the game's angle (NOT the game's distance), " + state + ", her near it", 900, 900, mid);
            }

            // An elite brute, a hit's flash, and a daze.
            _levels.ClearSpawned();
            _player.Teleport(home);
            yield return Frames(2);
            Enemy elite = _levels.SpawnPractice(EnemyKind.Brute)[0];
            LevelValues v = _levels.Values;
            EnemyFactory.MakeElite(elite, v.EliteHealthOf(EnemyKind.Brute), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
            Enemy guard = _levels.SpawnPractice(EnemyKind.Guard)[0];
            elite.Body.SetMaxHealth(1000000);
            guard.Body.SetMaxHealth(1000000);
            yield return Frames(3);
            PoseOn(game, MiddleOf(elite), 7f);
            EnemiesPair(game, "20-elite-brute", "an elite brute (its tint, 4/3 as large) close up at the game's angle (NOT the game's distance)", 900, 900, MiddleOf(elite));
            HitFlash flash = elite.GetComponent<HitFlash>();
            for (int i = 0; i < 10; i++)
            {
                elite.Body.TakeHit(1);
                yield return null; // a frame longer than the flash (a shader made the first time it is drawn) misses it: again
                if (flash.IsFlashing) break;
            }
            PoseOn(game, MiddleOf(elite), 7f);
            EnemiesPair(game, "21-hit-flash", "the elite the frame after a hit: its flash (" + elite.GetComponent<HitFlash>().IsFlashing + ")", 900, 900, MiddleOf(elite));
            guard.Body.TakeHit(1, 1e6f);
            yield return Frames(4);
            PoseOn(game, MiddleOf(guard), 7f);
            EnemiesPair(game, "22-dazed-guard", "a guard dazed (" + guard.IsDazed + "): its tint", 900, 900, MiddleOf(guard));

            // A warded enemy beside its warder.
            _levels.ClearSpawned();
            _player.Teleport(home);
            yield return Frames(2);
            List<Enemy> pack = _levels.SpawnWarderPack();
            foreach (Enemy m in pack) if (m.Body != null) m.Body.SetMaxHealth(1000000);
            yield return Until(() => pack.Any(EnemyWarder.IsWarded), 10f);
            EnemyWarder warder = pack.OfType<EnemyWarder>().FirstOrDefault();
            Enemy warded = pack.FirstOrDefault(EnemyWarder.IsWarded);
            if (warder != null && warded != null)
            {
                yield return Frames(10);
                Vector3 between = (MiddleOf(warder) + MiddleOf(warded)) * 0.5f;
                float apart = Vector3.Distance(warder.transform.position, warded.transform.position);
                PoseOn(game, between, 6f + apart);
                EnemiesPair(game, "23-warded-beside-its-warder", string.Format(Inv, "a warded {0} {1:0.0} m from its warder, the ward's thread, shell and the warder's staff, light and halo, at the game's angle (NOT the game's distance)", warded.LogWord, apart), 1400, 1000, between);
                Pose(game, _player.Position);
                EnemiesPair(game, "23b-warded-game-distance", "the same at the game's own distance, her where she stands", 900, 700, between);
            }
            else _log.Add("23 NOT TAKEN: no ward came on in 10 s");
            _levels.ClearSpawned();
        }

        // ---------------- The floors' fights, and the cost ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheBenchmarksFloor1AndFloor5Fights_AtTheGamesCamera_TheEnemiesLookOffAndOn_AndTheFrameDrawn()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} work order 119, the floors' fights; " + Values());
            foreach (BenchmarkScenario scenario in new[] { BenchmarkScenario.Floor1Combat, BenchmarkScenario.Floor5Combat })
            {
                BenchmarkStage stage = BenchmarkStage.Begin(scenario);
                Assert.That(stage, Is.Not.Null);
                Assert.That(stage.Level, Is.Not.Null, scenario + ": its floor");
                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    stage.Keep();
                }
                IReadOnlyList<Enemy> fighting = stage.Fighting;
                int alive = fighting.Count(e => e != null && !e.IsDead);
                string kinds = string.Join(", ", fighting.Where(e => e != null && !e.IsDead).GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key));
                string f = scenario == BenchmarkScenario.Floor1Combat ? "31-floor1-fight" : "32-floor5-fight";
                Pose(game, _player.Position);
                EnemiesPair(game, f, string.Format(Inv, "the benchmark's {0} (floor {1}'s fullest combat room), {2} enemies coming for her ({3}), she shooting the nearest, 3 s in, the game's distance", scenario, stage.Level.Layout.Floor, alive, kinds), 1100, 800, Chest);
                Pose(game, _player.Position, 10f);
                EnemiesPair(game, f + "-close", "the same moment, the camera pulled in to 10 m (NOT the game's view)", 1500, 1000, Chest);
                if (scenario == BenchmarkScenario.Floor5Combat) yield return Cost(game, alive);
                stage.End();
            }
            _levels.BackToPracticeRoom();
        }

        /// <summary>The frame of this fight drawn with the enemies' look on and off, alternating: the camera's draw and its read back (the GPU's work in it).</summary>
        IEnumerator Cost(Camera cam, int enemies)
        {
            const int Rounds = 30;
            var on = new List<double>();
            var off = new List<double>();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                for (int i = 0; i < Rounds * 2 + 4; i++)
                {
                    bool lookOn = i % 2 == 0;
                    ToonLookValues.Enemies = lookOn;
                    TickEnemies();
                    var watch = Stopwatch.StartNew();
                    cam.Render();
                    RenderTexture was = RenderTexture.active;
                    RenderTexture.active = rt;
                    one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); // waits for the GPU to finish the frame
                    one.Apply();
                    RenderTexture.active = was;
                    watch.Stop();
                    if (i >= 4) (lookOn ? on : off).Add(watch.Elapsed.TotalMilliseconds);
                }
            }
            finally
            {
                cam.targetTexture = before;
                ToonLookValues.Enemies = true;
                TickEnemies();
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(one);
            }
            on.Sort();
            off.Sort();
            string line = string.Format(Inv, "COST the floor 5 fight's frame ({0} enemies coming for her) drawn at {1} by {2} and read back (the GPU's work in it), {3} times each, alternating: the enemies' look ON median {4:0.000} ms (lower quartile {5:0.000}), OFF median {6:0.000} ms (lower quartile {7:0.000}); the difference of the medians {8:+0.000;-0.000} ms",
                                        enemies, W, H, Rounds, on[on.Count / 2], on[on.Count / 4], off[off.Count / 2], off[off.Count / 4], on[on.Count / 2] - off[off.Count / 2]);
            _log.Add(line);
            Debug.Log(line);
            yield return null;
        }

        /// <summary>The project's own measure of a frame (work order 032's benchmark), its floor 5 fight: the enemies' look on, off, on, off, and the whole look off once.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_WithTheEnemiesLookOnAndOff()
        {
            foreach ((bool her, bool them) in new[] { (true, true), (true, false), (true, true), (true, false), (false, false) })
            {
                ToonLookValues.On = her;
                ToonLookValues.Enemies = them;
                string label = !her ? "toon-all-off" : them ? "toon-enemies-on" : "toon-enemies-off";
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeToonCost119-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", label, "-benchmarkScenarios", "3", "-benchmarkSeconds", "5",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                float until = Time.realtimeSinceStartup + 120f;
                while (!b.Done && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult s = r.Scenarios[0];
                string counters = string.Join(", ", s.Counters.Where(c => c.Works).Select(c => string.Format(Inv, "{0} {1:0.###} {2}", c.Name, c.Mean, c.Unit)));
                int toonMaterials = r.Materials.Where(m => m.Shader == ToonLook.ShaderName).Sum(m => m.Renderers);
                string line = string.Format(Inv, "BENCHMARK {0} {1}: {2} frames, mean {3:0.000} ms, p95 {4:0.000}, worst {5:0.000}; {6} fighting; renderers seen on the toon shader: {7}; counters: {8}",
                                            s.Scenario, label, s.Frames, s.FrameMs.Mean, s.FrameMs.P95, s.FrameMs.Max, s.EnemiesFighting, toonMaterials, counters);
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }
    }
}
