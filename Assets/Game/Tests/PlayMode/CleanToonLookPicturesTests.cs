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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 125, a look order's pictures and its measures (Kristian, 2026-11-25, of work order 119's pictures: "man ser
    /// mange fragmenterte pixler rundt omkring, det ser veldig dårlig ut. Kan en agent gå over det å få det se bra ut."), taken
    /// without a window as work order 119 took its own (<see cref="EnemyToonLookPicturesTests"/>): every picture 1920 by 1080,
    /// nothing scaled, cut at 100 %; each comparison drawn in one frame, so the pose is the same in all. Runs ONLY when the
    /// environment variable ROGUELIKE_CAPTURE_125 names a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_125 = 'C:\Users\kriss\.claude-automation\runner\captures\125'
    ///   run-tests.ps1 PlayMode -Filter "CleanToonLookPicturesTests"
    ///
    /// What: why work order 119's hull broke up (its meshes' split normals and loose shells, its push away from the camera seen
    /// on and off, the figures' thin parts); her and every kind alone on the practice room's plain floor, at the game's camera
    /// and close up, 119's line (no anti-aliasing) LEFT and this order's (anti-aliased) RIGHT, with the fragmentation measure of
    /// each; each kind mid wind-up beside her, an elite, a flash, a daze and a ward; a walking figure frame by frame; the floor 1
    /// and floor 5 fights; the anti-aliasing off, multisampled and SMAA side by side; and the cost of each in the floor 5 fight.
    /// </summary>
    public class CleanToonLookPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_125";
        const int W = 1920, H = 1080;
        const int Msaa = AntiAliasingValues.MsaaSamples;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        float _ink;
        AntiAliasingKind _kind;
        readonly List<string> _log = new List<string>();
        readonly List<Enemy> _frozen = new List<Enemy>(); // enemies stood still (their component off: they leave Enemy.All), ticked here

        /// <summary>The ways a frame is drawn here.</summary>
        enum Way { NoLine, Hull119, Clean, CleanMsaa, CleanSmaa, NoLineMsaa, NoLineSmaa, Hull119Msaa }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 125)");
            Directory.CreateDirectory(_folder);
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            ToonLookValues.CleanInk = true;
            AntiAliasingValues.On = true;
            _ink = ToonLookValues.Live.InkWidth;
            _kind = AntiAliasingValues.Kind;
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
            ToonLookValues.CleanInk = true;
            AntiAliasingValues.On = true;
            _frozen.Clear();
            if (_folder != null)
            {
                ToonLookValues.Live.InkWidth = _ink;
                AntiAliasingValues.Kind = _kind;
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

        // ---------------- Drawing ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();
        ToonLook HerLook => _player.GetComponent<ToonLook>();

        Vector3 Chest
        {
            get
            {
                ToonLook look = HerLook;
                Bounds b = new Bounds(_player.Position + Vector3.up, Vector3.zero);
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

        List<Renderer> HerRenderers()
        {
            var list = new List<Renderer>();
            for (int i = 0; i < HerLook.TargetCount; i++) if (HerLook.TargetRenderer(i) != null) list.Add(HerLook.TargetRenderer(i));
            return list;
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

        static void TickEnemies()
        {
            foreach (Enemy e in Enemy.All)
                if (e != null && e.Look != null && e.Look.Toon != null) e.Look.Toon.Tick();
        }

        void TickAll()
        {
            TickEnemies();
            foreach (Enemy e in _frozen)
                if (e != null && e.Look != null && e.Look.Toon != null) e.Look.Toon.Tick();
        }

        /// <summary>Stands it still once its look is on: its component off (it then leaves Enemy.All, so it is ticked from here).</summary>
        void Freeze(Enemy e)
        {
            e.enabled = false;
            _frozen.Add(e);
        }

        /// <summary>Sets the line and the anti-aliasing for <paramref name="way"/>, her and every enemy following at once; returns the samples a pixel to draw with.</summary>
        int Set(Way way)
        {
            bool line = way != Way.NoLine && way != Way.NoLineMsaa && way != Way.NoLineSmaa;
            bool hull119 = way == Way.Hull119 || way == Way.Hull119Msaa;
            ToonLookValues.CleanInk = !hull119;
            ToonLookValues.Live.InkWidth = line ? _ink : 0f;
            bool msaa = way == Way.CleanMsaa || way == Way.NoLineMsaa || way == Way.Hull119Msaa;
            bool smaa = way == Way.CleanSmaa || way == Way.NoLineSmaa;
            AntiAliasingValues.On = msaa || smaa;
            AntiAliasingValues.Kind = smaa ? AntiAliasingKind.Smaa : AntiAliasingKind.Msaa4x;
            HerLook.Tick();
            HerLook.Refresh();
            TickAll();
            // An enemy's line width is written when its look goes on: here, so that "no line" is no line on it too.
            foreach (Enemy e in Enemy.All.Concat(_frozen))
                if (e != null && e.Look != null && e.Look.Toon != null)
                    foreach (Material m in e.Look.Toon.Materials)
                        if (m != null && m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", ToonLookValues.Live.InkWidth);
            return msaa ? Msaa : 1;
        }

        Color32[] Draw(Camera cam, int samples)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
            var plain = samples > 1 ? new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1 } : null;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                if (_player != null) _player.TickLaser();
                cam.Render();
                RenderTexture read = rt;
                if (plain != null)
                {
                    Graphics.Blit(rt, plain); // resolves the samples
                    read = plain;
                }
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = read;
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
                if (plain != null)
                {
                    plain.Release();
                    Object.Destroy(plain);
                }
                Object.Destroy(tex);
            }
        }

        Color32[] DrawWay(Camera cam, Way way) => Draw(cam, Set(way));

        void Write(Color32[] px, int w, int h, string file)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_folder, file), file.EndsWith(".jpg", StringComparison.Ordinal) ? tex.EncodeToJPG(92) : tex.EncodeToPNG());
            }
            finally { Object.Destroy(tex); }
        }

        struct Box
        {
            public int X, Y, W, H;
        }

        static Box Around(Camera cam, Vector3 centre, int cw, int ch)
        {
            Vector3 s = cam.WorldToViewportPoint(centre);
            cw = Mathf.Min(cw, W);
            ch = Mathf.Min(ch, H);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x * W) - cw / 2, 0, W - cw), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y * H) - ch / 2, 0, H - ch);
            return new Box { X = x0, Y = y0, W = cw, H = ch };
        }

        static Color32[] Cut(Color32[] frame, Box b)
        {
            var cut = new Color32[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                    cut[y * b.W + x] = frame[(b.Y + y) * W + b.X + x];
            return cut;
        }

        static Color32[] SideBySide(IList<Color32[]> cuts, int cw, int ch)
        {
            const int Gap = 4;
            int total = cw * cuts.Count + Gap * (cuts.Count - 1);
            var all = new Color32[total * ch];
            for (int i = 0; i < all.Length; i++) all[i] = new Color32(255, 255, 255, 255);
            for (int i = 0; i < cuts.Count; i++)
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        all[y * total + i * (cw + Gap) + x] = cuts[i][y * cw + x];
            return all;
        }

        /// <summary>Saves the cuts side by side at 100 % (PNG), and, when <paramref name="enlarge"/> is over 1, again with each pixel made that many (for looking at single pixels).</summary>
        void SaveRow(string name, IList<Color32[]> frames, Box b, int enlarge = 1)
        {
            var cuts = frames.Select(f => Cut(f, b)).ToList();
            int total = b.W * cuts.Count + 4 * (cuts.Count - 1);
            Color32[] row = SideBySide(cuts, b.W, b.H);
            Write(row, total, b.H, name + ".png");
            if (enlarge > 1)
            {
                int ew = total * enlarge, eh = b.H * enlarge;
                var big = new Color32[ew * eh];
                for (int y = 0; y < eh; y++)
                    for (int x = 0; x < ew; x++)
                        big[y * ew + x] = row[(y / enlarge) * total + x / enlarge];
                Write(big, ew, eh, name + "-x" + enlarge + ".png");
            }
        }

        static float[] Luma(Color32[] px, Box b)
        {
            var l = new float[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    Color32 c = px[(b.Y + y) * W + b.X + x];
                    l[y * b.W + x] = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                }
            return l;
        }

        /// <summary>The figure's own pixels: where the frame with it (and no line) differs from the frame without it.</summary>
        static bool[] FigureOf(Color32[] with, Color32[] without, Box b)
        {
            var f = new bool[b.W * b.H];
            for (int y = 0; y < b.H; y++)
                for (int x = 0; x < b.W; x++)
                {
                    int i = (b.Y + y) * W + b.X + x;
                    Color32 p = with[i], q = without[i];
                    f[y * b.W + x] = Math.Abs(p.r - q.r) + Math.Abs(p.g - q.g) + Math.Abs(p.b - q.b) > 30;
                }
            return f;
        }

        /// <summary>The bounding box of a figure in a frame, grown by <paramref name="margin"/>.</summary>
        static Box Bounds(Color32[] with, Color32[] without, int margin)
        {
            int x0 = W, y0 = H, x1 = -1, y1 = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Color32 p = with[i], q = without[i];
                    if (Math.Abs(p.r - q.r) + Math.Abs(p.g - q.g) + Math.Abs(p.b - q.b) <= 30) continue;
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
            if (x1 < 0) return new Box { X = 0, Y = 0, W = 8, H = 8 };
            x0 = Math.Max(0, x0 - margin);
            y0 = Math.Max(0, y0 - margin);
            x1 = Math.Min(W - 1, x1 + margin);
            y1 = Math.Min(H - 1, y1 + margin);
            return new Box { X = x0, Y = y0, W = x1 - x0 + 1, H = y1 - y0 + 1 };
        }

        /// <summary>Share of a figure's pixels in parts under 3 px across (taken away by an opening with a 3 by 3 square).</summary>
        static float ThinShare(bool[] f, int w, int h)
        {
            var eroded = new bool[f.Length];
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    bool all = true;
                    for (int dy = -1; dy <= 1 && all; dy++) for (int dx = -1; dx <= 1 && all; dx++) all &= f[(y + dy) * w + x + dx];
                    eroded[y * w + x] = all;
                }
            int figure = 0, kept = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!f[y * w + x]) continue;
                    figure++;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h && eroded[ny * w + nx]) near = true;
                        }
                    if (near) kept++;
                }
            return figure > 0 ? 1f - (float)kept / figure : 0f;
        }

        /// <summary>
        /// One figure alone: the frames this order compares, drawn in this frame, measured, and saved as LEFT 119's line (no
        /// anti-aliasing) and RIGHT this order's (anti-aliased by <see cref="AntiAliasingValues.Kind"/>, as the game draws it).
        /// </summary>
        void MeasureFigure(Camera cam, List<Renderer> figure, string name, string what, int enlarge)
        {
            try
            {
                var shadows = figure.ToDictionary(r => r, r => r.shadowCastingMode);
                Color32[] empty, none, hull, clean, noneAa, cleanAa, hullAa;
                float[] lMsaa;
                try
                {
                    foreach (Renderer r in figure) r.shadowCastingMode = ShadowCastingMode.Off; // its shadow on the floor is not the figure
                    Way noLineAa = _kind == AntiAliasingKind.Smaa ? Way.NoLineSmaa : Way.NoLineMsaa, cleanAaWay = _kind == AntiAliasingKind.Smaa ? Way.CleanSmaa : Way.CleanMsaa;
                    none = DrawWay(cam, Way.NoLine);
                    hull = DrawWay(cam, Way.Hull119);
                    clean = DrawWay(cam, Way.Clean);
                    noneAa = DrawWay(cam, noLineAa);
                    cleanAa = DrawWay(cam, cleanAaWay);
                    hullAa = DrawWay(cam, Way.Hull119Msaa);
                    Color32[] noneMsaa = _kind == AntiAliasingKind.Smaa ? DrawWay(cam, Way.NoLineMsaa) : noneAa;
                    lMsaa = Luma(noneMsaa, new Box { X = 0, Y = 0, W = W, H = H });
                    foreach (Renderer r in figure) r.enabled = false;
                    empty = DrawWay(cam, Way.NoLine);
                }
                finally
                {
                    foreach (Renderer r in figure) if (r != null) { r.enabled = true; r.shadowCastingMode = shadows[r]; }
                    Set(Way.CleanMsaa);
                    AntiAliasingValues.Kind = _kind;
                    AntiAliasingValues.On = true;
                }
                Box b = Bounds(none, empty, 8);
                bool[] fig = FigureOf(none, empty, b);
                float[] lNone = Luma(none, b), lNoneAa = Luma(noneAa, b);
                InkFragments.Result rHull = InkFragments.Measure(InkFragments.InkOf(Luma(hull, b), lNone), b.W, b.H, fig);
                InkFragments.Result rClean = InkFragments.Measure(InkFragments.InkOf(Luma(clean, b), lNone), b.W, b.H, fig);
                InkFragments.Result rCleanAa = InkFragments.Measure(InkFragments.InkOf(Luma(cleanAa, b), lNoneAa), b.W, b.H, fig);
                var lNoneMsaa = new float[b.W * b.H];
                for (int y = 0; y < b.H; y++) for (int x = 0; x < b.W; x++) lNoneMsaa[y * b.W + x] = lMsaa[(b.Y + y) * W + b.X + x];
                InkFragments.Result rHullAa = InkFragments.Measure(InkFragments.InkOf(Luma(hullAa, b), lNoneMsaa), b.W, b.H, fig);
                _log.Add(string.Format(Inv, "MEASURE {0} ({1}; the figure {2} by {3} px, {4:0.0} % of its pixels in parts under 3 px across): 119's hull: {5} | 119's hull multisampled (the anti-aliasing alone): {9} | this order's line, no anti-aliasing: {6} | this order's line, {7} (as the game draws it): {8}",
                                       name, what, b.W, b.H, 100f * ThinShare(fig, b.W, b.H), rHull, rClean, _kind, rCleanAa, rHullAa));
                // The pair: a box round the figure (at least a fixed size, so that the pictures line up).
                int cw = Math.Max(b.W + 40, enlarge > 1 ? 240 : 600), ch = Math.Max(b.H + 40, enlarge > 1 ? 240 : 600);
                Box cut = new Box { X = Mathf.Clamp(b.X + b.W / 2 - cw / 2, 0, W - Math.Min(cw, W)), Y = Mathf.Clamp(b.Y + b.H / 2 - ch / 2, 0, H - Math.Min(ch, H)), W = Math.Min(cw, W), H = Math.Min(ch, H) };
                SaveRow(name, new List<Color32[]> { hull, cleanAa }, cut, enlarge);
                SaveRow(name + "-steps", new List<Color32[]> { none, hull, hullAa, clean, cleanAa }, cut, enlarge);
                _log.Add(string.Format(Inv, "{0}: {1}; LEFT work order 119's hull line, no anti-aliasing; RIGHT this order's line, {2} ({3} by {4} px each at 100 %{5}); and {0}-steps, LEFT TO RIGHT: no line, 119's line, 119's line multisampled (the anti-aliasing alone), this order's line without anti-aliasing (the line alone), this order's line multisampled",
                                       name, what, _kind, cut.W, cut.H, enlarge > 1 ? ", and -x" + enlarge + " each pixel made " + enlarge + " by " + enlarge : ""));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
        }

        string Values()
        {
            ToonLookValues v = ToonLookValues.Live;
            return string.Format(Inv, "work order 125: clean line {0} (the hull pushed {1} m back along the line of sight, only round marked renderers), ink {2} px at 1080 rows; anti-aliasing {3} ({4}); 119's line: the same width, pushed {1} m along the camera's axis, round every renderer wearing the shader, no anti-aliasing",
                                 ToonLookValues.CleanInk, v.InkDepthPush, _ink, AntiAliasingValues.On, _kind);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static readonly EnemyKind[] Kinds = { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Bomber, EnemyKind.Warder, EnemyKind.Lancer, EnemyKind.Boss };

        // ---------------- Why 119's line broke up ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator A_WhyTheHullBrokeUp_TheMeshes_ThePush_AndTheThinParts()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); {W}x{H}; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");
            _log.Add(Values());
            // The meshes: her body and each kind's.
            var meshes = new List<(string, Mesh)>();
            foreach (Renderer r in HerRenderers())
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m != null) meshes.Add(("her " + r.name, m));
            }
            Vector3 home = new Vector3(8f, 0f, -6f);
            foreach (EnemyKind kind in Kinds)
            {
                _levels.ClearSpawned();
                List<Enemy> made = _levels.SpawnPractice(kind);
                yield return Frames(1);
                if (made.Count == 0 || made[0].Look == null) continue;
                foreach (Renderer r in made[0].Look.Renderers)
                {
                    Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r != null ? r.GetComponent<MeshFilter>()?.sharedMesh : null;
                    if (m != null && meshes.All(x => x.Item2 != m)) meshes.Add((EnemyKinds.Word(kind) + " " + r.name, m));
                }
            }
            foreach ((string label, Mesh m) in meshes) _log.Add("MESH " + label + ": " + MeshStats(m));

            // The push: the swarmer alone, close up and at the game's camera, its hull as 119 draws it, pushed along the line of
            // sight instead, and not pushed at all.
            _levels.ClearSpawned();
            _player.Teleport(home + new Vector3(14f, 0f, 0f));
            yield return Frames(2);
            Enemy sw = _levels.SpawnPractice(EnemyKind.Swarmer)[0];
            yield return Frames(3);
            Freeze(sw);
            yield return Frames(1);
            foreach (float d in new[] { 0f, 3.2f + 2.2f * Mathf.Max(1.5f, sw.ModelHeight) })
            {
                try
                {
                    if (d > 0f) PoseOn(game, MiddleOf(sw), d); else Pose(game, MiddleOf(sw));
                    var frames = new List<Color32[]>();
                    var results = new List<string>();
                    var figure = sw.Look.Renderers.Where(r => r != null).ToList();
                    foreach (Renderer r in figure) r.shadowCastingMode = ShadowCastingMode.Off;
                    Color32[] none = DrawWay(game, Way.NoLine);
                    foreach (Renderer r in figure) r.enabled = false;
                    Color32[] empty = DrawWay(game, Way.NoLine);
                    foreach (Renderer r in figure) r.enabled = true;
                    Box b = Bounds(none, empty, 8);
                    bool[] fig = FigureOf(none, empty, b);
                    foreach ((string label, float ray, float push) in new[] { ("119 as it is (pushed 0.06 m along the camera's axis)", 0f, -1f), ("pushed 0.06 m along the line of sight", 1f, -1f), ("not pushed", 0f, 0f) })
                    {
                        Set(Way.Hull119);
                        foreach (Material m in sw.Look.Toon.Materials)
                        {
                            m.SetFloat("_OutlineAlongRay", ray);
                            m.SetFloat("_OutlineDepthPush", push >= 0f ? push : ToonLookValues.Live.InkDepthPush);
                        }
                        Color32[] f = Draw(game, 1);
                        frames.Add(f);
                        results.Add(label + ": " + InkFragments.Measure(InkFragments.InkOf(Luma(f, b), Luma(none, b)), b.W, b.H, fig));
                    }
                    foreach (Material m in sw.Look.Toon.Materials)
                    {
                        m.SetFloat("_OutlineAlongRay", 0f);
                        m.SetFloat("_OutlineDepthPush", ToonLookValues.Live.InkDepthPush);
                    }
                    foreach (Renderer r in figure) r.shadowCastingMode = ShadowCastingMode.On;
                    Set(Way.CleanMsaa);
                    Vector3 s = game.WorldToViewportPoint(MiddleOf(sw));
                    string name = d > 0f ? "02-hull-push-close" : "01-hull-push-game";
                    int cw = Math.Max(b.W + 40, d > 0f ? 600 : 200), ch = Math.Max(b.H + 40, d > 0f ? 600 : 200);
                    Box cut = new Box { X = Mathf.Clamp(b.X + b.W / 2 - cw / 2, 0, W - cw), Y = Mathf.Clamp(b.Y + b.H / 2 - ch / 2, 0, H - ch), W = cw, H = ch };
                    SaveRow(name, frames, cut, d > 0f ? 2 : 4);
                    _log.Add(string.Format(Inv, "{0}: the swarmer alone {1}, its middle {2:0} px right and {3:0} px up of the screen's middle; work order 119's hull (no anti-aliasing) LEFT as it is, MIDDLE pushed along the line of sight, RIGHT not pushed ({4} by {5} px each); the figure {6:0.0} % in parts under 3 px across. {7}",
                                           name, d > 0f ? "close up (" + d.ToString("0.0", Inv) + " m)" : "at the game's camera", (s.x - 0.5f) * W, (s.y - 0.5f) * H, cw, ch, 100f * ThinShare(fig, b.W, b.H), string.Join(" | ", results)));
                }
                catch (Exception e) { _log.Add("hull push FAILED: " + e); }
            }

            _levels.ClearSpawned();
        }

        /// <summary>A mesh's vertices that share a place with others, how many of those places have normals that differ (the hull tears there), and its loose shells.</summary>
        static string MeshStats(Mesh m)
        {
            try
            {
                // Read off the GPU: the models are imported without Read/Write, so their data is not on the CPU.
                Vector3[] v = Attribute(m, VertexAttribute.Position), n = Attribute(m, VertexAttribute.Normal);
                int[] t = Indices(m);
                if (v == null || n == null || t == null) return "not readable here (" + m.vertexCount + " vertices)";
                var at = new Dictionary<Vector3Int, List<int>>();
                for (int i = 0; i < v.Length; i++)
                {
                    var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
                    if (!at.TryGetValue(k, out List<int> list)) at[k] = list = new List<int>();
                    list.Add(i);
                }
                int shared = 0, split = 0;
                float worst = 0f;
                var place = new int[v.Length];
                int p = 0;
                foreach (List<int> list in at.Values)
                {
                    foreach (int i in list) place[i] = p;
                    p++;
                    if (list.Count < 2) continue;
                    shared++;
                    float most = 0f;
                    for (int a = 0; a < list.Count; a++)
                        for (int b = a + 1; b < list.Count; b++)
                            most = Mathf.Max(most, Vector3.Angle(n[list[a]], n[list[b]]));
                    if (most > 1f) split++;
                    worst = Mathf.Max(worst, most);
                }
                int shells = Shells(t, place, p), rawShells = Shells(t, Enumerable.Range(0, v.Length).ToArray(), v.Length);
                return string.Format(Inv, "{0} vertices at {1} places; {2} places hold more than one vertex, {3} of them ({4:0.0} % of all places) with normals apart by more than 1 degree (at most {5:0} degrees): the hull tears open there; {6} loose shells (welded by place), {7} by its own vertices",
                                     v.Length, p, shared, split, 100f * split / Mathf.Max(1, p), worst, shells, rawShells);
            }
            catch (Exception e) { return "not readable here: " + e.Message; }
        }

        static uint[] Words(GraphicsBuffer b)
        {
            var words = new uint[b.count * b.stride / 4];
            b.GetData(words);
            return words;
        }

        static Vector3[] Attribute(Mesh m, VertexAttribute a)
        {
            if (!m.HasVertexAttribute(a)) return null;
            m.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            int stream = m.GetVertexAttributeStream(a), offset = m.GetVertexAttributeOffset(a), stride = m.GetVertexBufferStride(stream);
            VertexAttributeFormat format = m.GetVertexAttributeFormat(a);
            if (format != VertexAttributeFormat.Float32 && format != VertexAttributeFormat.Float16) return null;
            uint[] w;
            using (GraphicsBuffer b = m.GetVertexBuffer(stream)) w = Words(b);
            var bytes = new byte[w.Length * 4];
            Buffer.BlockCopy(w, 0, bytes, 0, bytes.Length);
            var o = new Vector3[m.vertexCount];
            for (int i = 0; i < o.Length; i++)
            {
                int at = i * stride + offset;
                if (format == VertexAttributeFormat.Float32)
                    o[i] = new Vector3(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8));
                else
                    o[i] = new Vector3(Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, at)), Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, at + 2)), Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, at + 4)));
            }
            return o;
        }

        static int[] Indices(Mesh m)
        {
            m.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            uint[] w;
            using (GraphicsBuffer b = m.GetIndexBuffer()) w = Words(b);
            bool wide = m.indexFormat == IndexFormat.UInt32;
            int Index(int i) => wide ? (int)w[i] : (int)((w[i / 2] >> (16 * (i % 2))) & 0xFFFF);
            var list = new List<int>();
            for (int sub = 0; sub < m.subMeshCount; sub++)
            {
                if (m.GetTopology(sub) != MeshTopology.Triangles) continue;
                int start = (int)m.GetIndexStart(sub), count = (int)m.GetIndexCount(sub), baseVertex = (int)m.GetBaseVertex(sub);
                for (int i = 0; i < count; i++) list.Add(Index(start + i) + baseVertex);
            }
            return list.ToArray();
        }

        static int Shells(int[] tris, int[] place, int count)
        {
            var parent = Enumerable.Range(0, count).ToArray();
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Join(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            var used = new bool[count];
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = place[tris[i]], b = place[tris[i + 1]], c = place[tris[i + 2]];
                used[a] = used[b] = used[c] = true;
                Join(a, b);
                Join(b, c);
            }
            var roots = new HashSet<int>();
            for (int i = 0; i < count; i++) if (used[i]) roots.Add(Find(i));
            return roots.Count;
        }

        // ---------------- Her and each kind alone, measured ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator B_HerAndEachKindAlone_AtTheGamesCameraAndCloseUp_119sLineAgainstThisOrders_Measured()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} " + Values());
            _levels.ClearSpawned();
            _player.Teleport(Vector3.zero);
            _player.AimAt(new Vector3(0f, 0f, -6f));
            yield return new WaitForSecondsRealtime(1.5f);
            List<Renderer> her = HerRenderers();
            Pose(game, _player.Position);
            MeasureFigure(game, her, "10-her-game", "her alone at the game's camera (28 m), standing", 3);
            PoseOn(game, Chest, 6f);
            MeasureFigure(game, her, "11-her-close", "her alone at the game's angle, the camera 6 m from her chest (NOT the game's distance)", 1);
            PoseOn(game, Chest + Vector3.up * 0.2f, 3.2f);
            MeasureFigure(game, her, "12-her-closer", "her hat's brim, hands and guns, the camera 3.2 m from her chest at the game's angle (NOT the game's view)", 1);
            // Running.
            _player.Steer(Vector3.right);
            float runUntil = Time.time + 0.8f, until = Time.realtimeSinceStartup + 10f;
            while (Time.time < runUntil && Time.realtimeSinceStartup < until) yield return null;
            PoseOn(game, Chest, 7f);
            MeasureFigure(game, her, "13-her-running-close", string.Format(Inv, "her running east at {0:0.0} m/s, the camera 7 m out (NOT the game's view)", _player.Speed), 1);
            Pose(game, _player.Position);
            MeasureFigure(game, her, "14-her-running-game", "her running, at the game's camera", 3);
            _player.Stop();

            int n = 20;
            Vector3 home = new Vector3(8f, 0f, -6f);
            foreach (EnemyKind kind in Kinds)
            {
                _levels.ClearSpawned();
                _player.Teleport(home + new Vector3(16f, 0f, 6f)); // far off: the figure alone
                yield return Frames(2);
                List<Enemy> made = _levels.SpawnPractice(kind);
                if (made.Count == 0) { _log.Add(kind + " NOT SPAWNED"); continue; }
                Enemy e = made[0];
                foreach (Enemy m in made) if (m.Body != null) m.Body.SetMaxHealth(1000000);
                yield return Frames(3);
                foreach (Enemy m in made) Freeze(m);
                for (int i = 1; i < made.Count; i++) made[i].transform.position += new Vector3(30f, 0f, 30f); // a pack's others away
                yield return Frames(2);
                var figure = e.GetComponentsInChildren<Renderer>(true).Where(r => ToonInkFigures.Has(r)).ToList();
                string word = EnemyKinds.Word(kind);
                Pose(game, MiddleOf(e));
                MeasureFigure(game, figure, string.Format(Inv, "{0:00}-{1}-game", n, word), "the " + word + " alone at the game's camera (28 m), standing", 3);
                PoseOn(game, MiddleOf(e), 3.2f + 2.2f * Mathf.Max(1.5f, e.ModelHeight));
                MeasureFigure(game, figure, string.Format(Inv, "{0:00}-{1}-close", n + 1, word), "the " + word + " alone close up at the game's angle (NOT the game's distance), standing", 1);
                n += 2;
            }
            _levels.ClearSpawned();
        }

        // ---------------- The telegraphs, beside her ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator C_EachKindMidWindUp_AnElite_AFlash_ADaze_AndAWard_119sLineAgainstThisOrders()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} the telegraphs; " + Values());
            Vector3 home = new Vector3(8f, 0f, -6f);
            int n = 40;
            foreach (EnemyKind kind in Kinds)
            {
                _levels.ClearSpawned();
                _player.Teleport(home);
                yield return Frames(2);
                List<Enemy> made = _levels.SpawnPractice(kind);
                if (made.Count == 0) continue;
                Enemy e = made[0];
                foreach (Enemy m in made) if (m.Body != null) m.Body.SetMaxHealth(1000000);
                yield return Frames(2);
                Vector3 d = e.transform.position - _player.Position;
                d.y = 0f;
                float near = kind == EnemyKind.Shooter || kind == EnemyKind.Caster || kind == EnemyKind.Warder || kind == EnemyKind.Lancer ? 7f : 2.2f;
                _player.Teleport(e.transform.position - (d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward) * near);
                var bomber = e as EnemyBomber;
                yield return Until(() => bomber != null ? bomber.FillRadius > 0.4f * bomber.Tuning.BlastRadius : e.State == EnemyState.Windup && e.Attack != null && e.WindupRemaining < e.Attack.Windup * 0.6f, 12f);
                string state = bomber != null ? string.Format(Inv, "its fuse {0:0}% burnt", 100f * bomber.FillRadius / Mathf.Max(0.01f, bomber.Tuning.BlastRadius))
                                              : e.State == EnemyState.Windup ? "mid wind-up of its " + (e.Attack != null ? e.Attack.Name : "?") : "NOT in a wind-up (" + e.State + ")";
                PoseOn(game, MiddleOf(e), 3.2f + 2.2f * Mathf.Max(1.5f, e.ModelHeight));
                Pair(game, string.Format(Inv, "{0:00}-{1}-telegraph", n++, EnemyKinds.Word(kind)), "the " + EnemyKinds.Word(kind) + " close up, " + state + ", her near it", MiddleOf(e), 900, 900);
            }
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
            Pair(game, "50-elite-brute", "an elite brute close up", MiddleOf(elite), 900, 900);
            HitFlash flash = elite.GetComponent<HitFlash>();
            for (int i = 0; i < 10; i++)
            {
                elite.Body.TakeHit(1);
                yield return null;
                if (flash.IsFlashing) break;
            }
            PoseOn(game, MiddleOf(elite), 7f);
            Pair(game, "51-hit-flash", "the elite the frame after a hit: its flash (" + flash.IsFlashing + ")", MiddleOf(elite), 900, 900);
            guard.Body.TakeHit(1, 1e6f);
            yield return Frames(4);
            PoseOn(game, MiddleOf(guard), 7f);
            Pair(game, "52-dazed-guard", "a guard dazed (" + guard.IsDazed + ")", MiddleOf(guard), 900, 900);

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
                PoseOn(game, between, 6f + Vector3.Distance(warder.transform.position, warded.transform.position));
                Pair(game, "53-warded-beside-its-warder", "a warded " + warded.LogWord + " beside its warder: the thread, the shell, the staff's light and halo", between, 1400, 1000);
                AaRow(game, "54-warded-aa", "the same: this order's line with the anti-aliasing LEFT off, MIDDLE multisampled 4x, RIGHT SMAA (the thin teal thread and the glows)", between, 600, 500, 2);
                Pose(game, _player.Position);
                Pair(game, "55-warded-game", "the same at the game's own distance", between, 900, 700);
            }
            else _log.Add("53 NOT TAKEN: no ward came on in 10 s");
            _levels.ClearSpawned();
        }

        /// <summary>LEFT 119's line without anti-aliasing, RIGHT this order's line anti-aliased as the game draws it; this frame.</summary>
        void Pair(Camera cam, string name, string what, Vector3 centre, int cw, int ch)
        {
            try
            {
                Way aa = _kind == AntiAliasingKind.Smaa ? Way.CleanSmaa : Way.CleanMsaa;
                Color32[] a = DrawWay(cam, Way.Hull119);
                Color32[] b = DrawWay(cam, aa);
                Set(Way.CleanMsaa);
                AntiAliasingValues.Kind = _kind;
                Write(b, W, H, name + "-after.jpg");
                SaveRow(name, new List<Color32[]> { a, b }, Around(cam, centre, cw, ch));
                _log.Add(string.Format(Inv, "{0}: {1}; LEFT work order 119 (its hull, no anti-aliasing), RIGHT this order ({2}) ({3} by {4} px each at 100 %)", name, what, _kind, Math.Min(cw, W), Math.Min(ch, H)));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
        }

        void ThreeRow(Camera cam, string name, string what, Vector3 centre, int cw, int ch, int enlarge)
        {
            try
            {
                Color32[] a = DrawWay(cam, Way.Hull119), b = DrawWay(cam, Way.Hull119Msaa), c = DrawWay(cam, Way.CleanMsaa);
                Set(Way.CleanMsaa);
                AntiAliasingValues.Kind = _kind;
                SaveRow(name, new List<Color32[]> { a, b, c }, Around(cam, centre, cw, ch), enlarge);
                _log.Add(string.Format(Inv, "{0}: {1} ({2} by {3} px each at 100 %{4})", name, what, Math.Min(cw, W), Math.Min(ch, H), enlarge > 1 ? ", and -x" + enlarge : ""));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
        }

        /// <summary>This order's line with the anti-aliasing off, multisampled and SMAA, side by side; this frame.</summary>
        void AaRow(Camera cam, string name, string what, Vector3 centre, int cw, int ch, int enlarge)
        {
            try
            {
                Color32[] off = DrawWay(cam, Way.Clean), msaa = DrawWay(cam, Way.CleanMsaa), smaa = DrawWay(cam, Way.CleanSmaa);
                Set(Way.CleanMsaa);
                AntiAliasingValues.Kind = _kind;
                SaveRow(name, new List<Color32[]> { off, msaa, smaa }, Around(cam, centre, cw, ch), enlarge);
                _log.Add(string.Format(Inv, "{0}: {1} ({2} by {3} px each at 100 %{4})", name, what, Math.Min(cw, W), Math.Min(ch, H), enlarge > 1 ? ", and -x" + enlarge : ""));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
        }

        // ---------------- Frame to frame ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator D_AWalkingFigure_FrameByFrame_TheInkPixelsCount_AndTheMeasure()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} frame to frame; " + Values());
            _levels.ClearSpawned();
            _player.Teleport(new Vector3(8f, 0f, -6f));
            yield return Frames(2);
            Enemy sw = _levels.SpawnPractice(EnemyKind.Swarmer)[0];
            if (sw.Body != null) sw.Body.SetMaxHealth(1000000);
            sw.transform.position = _player.Position + new Vector3(-9f, 0f, 4f);
            yield return Frames(20);
            foreach ((string label, Func<Vector3> focus, float distance, Func<List<Renderer>> figure) in new (string, Func<Vector3>, float, Func<List<Renderer>>)[]
            {
                ("a swarmer walking at her, at the game's camera", () => MiddleOf(sw), 0f, () => sw.GetComponentsInChildren<Renderer>(true).Where(ToonInkFigures.Has).ToList()),
                ("a swarmer walking at her, the camera 8 m out", () => MiddleOf(sw), 8f, () => sw.GetComponentsInChildren<Renderer>(true).Where(ToonInkFigures.Has).ToList()),
            })
            {
                var hull = new List<int>();
                var clean = new List<int>();
                var hullPer = new List<float>();
                var cleanPer = new List<float>();
                var passes = new List<string>();
                int clean119Pass = 0, cleanPass = 0;
                for (int f = 0; f < 16; f++)
                {
                    yield return null;
                    if (sw == null || sw.IsDead) break;
                    if (distance > 0f) PoseOn(game, focus(), distance); else Pose(game, focus());
                    List<Renderer> fig = figure();
                    var shadows = fig.ToDictionary(r => r, r => r.shadowCastingMode);
                    foreach (Renderer r in fig) r.shadowCastingMode = ShadowCastingMode.Off;
                    Color32[] none = DrawWay(game, Way.NoLine), h = DrawWay(game, Way.Hull119), noneAa = DrawWay(game, Way.NoLineMsaa), c = DrawWay(game, Way.CleanMsaa);
                    foreach (Renderer r in fig) r.enabled = false;
                    Color32[] empty = DrawWay(game, Way.NoLine);
                    foreach (Renderer r in fig) { r.enabled = true; r.shadowCastingMode = shadows[r]; }
                    Set(Way.CleanMsaa);
                    Box b = Bounds(none, empty, 8);
                    bool[] figure2 = FigureOf(none, empty, b);
                    InkFragments.Result rh = InkFragments.Measure(InkFragments.InkOf(Luma(h, b), Luma(none, b)), b.W, b.H, figure2);
                    InkFragments.Result rc = InkFragments.Measure(InkFragments.InkOf(Luma(c, b), Luma(noneAa, b)), b.W, b.H, figure2);
                    hull.Add(rh.InkPixels);
                    clean.Add(rc.InkPixels);
                    hullPer.Add((float)rh.InkPixels / Math.Max(1, rh.OutlinePixels));
                    cleanPer.Add((float)rc.InkPixels / Math.Max(1, rc.OutlinePixels));
                    if (rh.Passes) clean119Pass++;
                    if (rc.Passes) cleanPass++;
                    if (f == 0 || f == 8)
                    {
                        string name = string.Format(Inv, "60-walking-{0}-frame{1:00}", distance > 0f ? "8m" : "game", f);
                        int cw = Math.Max(b.W + 30, 200), ch = Math.Max(b.H + 30, 200);
                        SaveRow(name, new List<Color32[]> { h, c }, new Box { X = Mathf.Clamp(b.X + b.W / 2 - cw / 2, 0, W - cw), Y = Mathf.Clamp(b.Y + b.H / 2 - ch / 2, 0, H - ch), W = cw, H = ch }, distance > 0f ? 2 : 4);
                    }
                }
                _log.Add(string.Format(Inv, "FRAMES {0}: 119's hull ink px per frame [{1}], largest jump {2:0.0} % of the mean, {3} of {4} frames pass the measure; this order's line (multisampled) [{5}], largest jump {6:0.0} %, {7} of {4} pass (pass mark: a jump of at most {8:0} %); the ink pixels for every pixel of the figure's outline (the figure's own turning taken out): 119's [{9}] largest jump {10:0.0} %, this order's [{11}] largest jump {12:0.0} %; pictures 60-walking-* frames 0 and 8, LEFT 119, RIGHT this order",
                                       label, string.Join(" ", hull), 100f * InkFragments.FrameJump(hull), clean119Pass, hull.Count, string.Join(" ", clean), 100f * InkFragments.FrameJump(clean), cleanPass, 100f * InkFragments.PassFrameJump,
                                       string.Join(" ", hullPer.Select(x => x.ToString("0.00", Inv))), 100f * InkFragments.FrameJump(hullPer), string.Join(" ", cleanPer.Select(x => x.ToString("0.00", Inv))), 100f * InkFragments.FrameJump(cleanPer)));
            }
            _levels.ClearSpawned();
        }

        // ---------------- The fights, the anti-aliasing and the cost ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator E_TheFloor1AndFloor5Fights_119AgainstThisOrder_TheAntiAliasingSideBySide_AndTheCost()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} the floors' fights; " + Values());
            foreach (BenchmarkScenario scenario in new[] { BenchmarkScenario.Floor1Combat, BenchmarkScenario.Floor5Combat })
            {
                BenchmarkStage stage = BenchmarkStage.Begin(scenario);
                Assert.That(stage, Is.Not.Null);
                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    stage.Keep();
                }
                IReadOnlyList<Enemy> fighting = stage.Fighting;
                int alive = fighting.Count(e => e != null && !e.IsDead);
                string kinds = string.Join(", ", fighting.Where(e => e != null && !e.IsDead).GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key));
                string f = scenario == BenchmarkScenario.Floor1Combat ? "71-floor1-fight" : "72-floor5-fight";
                Pose(game, _player.Position);
                Pair(game, f, string.Format(Inv, "the benchmark's {0}, {1} enemies coming for her ({2}), 3 s in, the game's camera", scenario, alive, kinds), Chest, 1100, 800);
                ThreeRow(game, f + "-three", "the same moment: LEFT 119's line, MIDDLE 119's line multisampled (the anti-aliasing alone), RIGHT this order's line multisampled", Chest, 560, 560, 2);
                Pose(game, _player.Position, 10f);
                ThreeRow(game, f + "-close-three", "the same, 10 m: LEFT 119's line, MIDDLE 119's line multisampled, RIGHT this order's line multisampled", Chest, 600, 600, 2);
                Pose(game, _player.Position);
                AaRow(game, f + "-aa", "the same moment: this order's line with the anti-aliasing LEFT off, MIDDLE multisampled 4x, RIGHT SMAA", Chest, 560, 560, 2);
                Pose(game, _player.Position, 10f);
                Pair(game, f + "-close", "the same moment, the camera pulled in to 10 m (NOT the game's view)", Chest, 1500, 1000);
                AaRow(game, f + "-close-aa", "the same, 10 m: the anti-aliasing LEFT off, MIDDLE multisampled 4x, RIGHT SMAA", Chest, 600, 600, 2);
                if (scenario == BenchmarkScenario.Floor5Combat)
                {
                    Pose(game, _player.Position);
                    yield return Cost(game, alive);
                }
                stage.End();
            }
            _levels.BackToPracticeRoom();
        }

        /// <summary>The floor 5 fight's frame drawn each way in turn, the camera's draw and its read back (the GPU's work in it).</summary>
        IEnumerator Cost(Camera cam, int enemies)
        {
            const int Rounds = 30;
            Way[] ways = { Way.NoLine, Way.Hull119, Way.Clean, Way.CleanMsaa, Way.CleanSmaa, Way.NoLineMsaa, Way.Hull119Msaa };
            var times = ways.ToDictionary(w => w, w => new List<double>());
            var targets = new Dictionary<int, RenderTexture>
            {
                { 1, new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 } },
                { Msaa, new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = Msaa } },
            };
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var plain = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
            RenderTexture before = cam.targetTexture;
            try
            {
                for (int i = 0; i < Rounds + 2; i++)
                    foreach (Way w in ways)
                    {
                        int samples = Set(w);
                        RenderTexture rt = targets[samples];
                        cam.targetTexture = rt;
                        var watch = Stopwatch.StartNew();
                        cam.Render();
                        RenderTexture was = RenderTexture.active;
                        if (samples > 1) Graphics.Blit(rt, plain); // resolves, as the screen's would be
                        RenderTexture.active = samples > 1 ? plain : rt;
                        one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); // waits for the GPU to finish the frame
                        one.Apply();
                        RenderTexture.active = was;
                        watch.Stop();
                        if (i >= 2) times[w].Add(watch.Elapsed.TotalMilliseconds);
                    }
            }
            finally
            {
                cam.targetTexture = before;
                Set(Way.CleanMsaa);
                AntiAliasingValues.Kind = _kind;
                foreach (RenderTexture rt in targets.Values) { rt.Release(); Object.Destroy(rt); }
                plain.Release();
                Object.Destroy(plain);
                Object.Destroy(one);
            }
            var parts = new List<string>();
            foreach (Way w in ways)
            {
                List<double> t = times[w];
                t.Sort();
                parts.Add(string.Format(Inv, "{0} median {1:0.000} ms (lower quartile {2:0.000})", w, t[t.Count / 2], t[t.Count / 4]));
            }
            string line = string.Format(Inv, "COST the floor 5 fight's frame ({0} enemies coming for her) drawn at {1} by {2} and read back (the GPU's work in it), {3} times each way in turn: {4}",
                                        enemies, W, H, Rounds, string.Join("; ", parts));
            _log.Add(line);
            Debug.Log(line);
            yield return null;
        }
    }
}
