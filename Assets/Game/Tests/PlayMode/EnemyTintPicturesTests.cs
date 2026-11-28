using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 151, a look order's pictures, taken without a window as work order 137's (<see cref="CallerPicturesTests"/>): each
    /// tinted kind beside its untinted twin at the game's own camera (its height, pitch and field of view, as the rig puts it with her
    /// standing just south of them), on floor 5's first combat room (the deepest floor, its darkest palette, the rooms' mood on) and in
    /// the practice room (its light floor, no mood). Each line-up drawn four times in the same frame: in the default look (the toon look,
    /// on for a new player) and in the plain look, each with <see cref="EnemyTintValues.Show"/> off (BEFORE: today's) and on (AFTER).
    /// With each, the mean colour of each body on screen (its own pixels: those that change when only it is taken out of the picture,
    /// its shadow kept) and how much of it there is. And the cost: the benchmark's floor 5 fight with the switch on and off.
    /// It changes nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_151 names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE_151 = 'C:\Users\kriss\.claude-automation\runner\captures\151'
    ///   run-tests.ps1 PlayMode -Filter "EnemyTintPicturesTests"
    /// </summary>
    public class EnemyTintPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_151";
        const int W = 1920, H = 1080;
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run, work order 137's pictures
        const float Apart = 3.2f;            // m between the bodies of a line-up (no two overlap on the screen)
        const float HerSouth = 3f;           // m south of a line-up she stands
        const int CutW = 640, CutH = 300;    // px of the 1920 by 1080 frame cut round a line-up
        const int Zoom = 2;                  // the cut drawn this many times larger on the sheets (nearest pixel)
        const int Gap = 8;                   // px between the cuts of a sheet
        const float DiffAtLeast = 6f / 255f; // a pixel is a body's when it changes by this much in a channel as only that body is taken out
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        bool _chipsWere;
        Transform _practiceRoot;
        readonly List<string> _log = new List<string>();
        readonly List<Color32[]> _sheets = new List<Color32[]>();
        int _sheetW, _sheetH;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 151)");
            Directory.CreateDirectory(_folder);
            EnemyTintValues.Show = true;
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            _chipsWere = HitChipsValues.On;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            CombatEffects.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "151-log.txt"), _log);
            _log.Clear();
            _sheets.Clear();
            EnemyTintValues.Show = true;
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            HitChipsValues.On = _chipsWere;
            if (_practiceRoot != null) Object.Destroy(_practiceRoot.gameObject);
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (Benchmark.Current != null) Object.Destroy(Benchmark.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        /// <summary>The walkable point within <paramref name="search"/> m of <paramref name="around"/> farthest from any edge of the walkable ground (a wall, a prop): the open floor for a line-up.</summary>
        static Vector3 OpenSpot(Vector3 around, float search)
        {
            Vector3 best = OnNavMesh(around);
            float most = -1f;
            for (float dx = -search; dx <= search; dx += 0.5f)
                for (float dz = -search; dz <= search; dz += 0.5f)
                {
                    if (!NavMesh.SamplePosition(around + new Vector3(dx, 0f, dz), out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas)) continue;
                    if (edge.distance > most) { most = edge.distance; best = hit.position; }
                }
            return best;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>The camera where the game's rig would put it with her at <paramref name="focus"/>.</summary>
        void Pose(Camera cam, Vector3 focus)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            float d = GameSettings.CameraDistance(Feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        Color32[] Draw(Camera cam)
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

        static IEnumerable<Renderer> BodyRenderers(Enemy e) =>
            e.Look != null ? e.Look.Renderers.Where(r => r != null) : e.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer));

        /// <summary>A body's rectangle on the screen (pixels, rows from the bottom), from its meshes' bounds.</summary>
        static RectInt ScreenRect(Camera cam, Enemy e)
        {
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in BodyRenderers(e))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) b = r.bounds; else b.Encapsulate(r.bounds);
                any = true;
            }
            if (!any) return new RectInt(0, 0, 0, 0);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 s = cam.WorldToScreenPoint(c);
                s.x *= W / (float)cam.pixelWidth;
                s.y *= H / (float)cam.pixelHeight;
                x0 = Mathf.Min(x0, s.x); x1 = Mathf.Max(x1, s.x); y0 = Mathf.Min(y0, s.y); y1 = Mathf.Max(y1, s.y);
            }
            int ix0 = Mathf.Clamp(Mathf.FloorToInt(x0), 0, W - 1), iy0 = Mathf.Clamp(Mathf.FloorToInt(y0), 0, H - 1);
            int ix1 = Mathf.Clamp(Mathf.CeilToInt(x1), 0, W - 1), iy1 = Mathf.Clamp(Mathf.CeilToInt(y1), 0, H - 1);
            return new RectInt(ix0, iy0, ix1 - ix0 + 1, iy1 - iy0 + 1);
        }

        /// <summary>The frame with <paramref name="e"/> drawn as its shadow only (its body out of the picture, its shadow kept).</summary>
        Color32[] Without(Camera cam, Enemy e)
        {
            var was = new List<(Renderer r, ShadowCastingMode m)>();
            foreach (Renderer r in e.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.GetComponentInParent<StaggerBarView>() != null || r.GetComponent<MarkPips>() != null) continue;
                was.Add((r, r.shadowCastingMode));
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            try { return Draw(cam); }
            finally { foreach ((Renderer r, ShadowCastingMode m) in was) r.shadowCastingMode = m; }
        }

        /// <summary>A body's own pixels: in its rectangle, those that change as it alone is taken out.</summary>
        static bool[] MaskOf(Color32[] with, Color32[] without, RectInt rect)
        {
            var mask = new bool[W * H];
            int d = Mathf.RoundToInt(DiffAtLeast * 255f);
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    int i = y * W + x;
                    Color32 a = with[i], b = without[i];
                    mask[i] = Math.Abs(a.r - b.r) >= d || Math.Abs(a.g - b.g) >= d || Math.Abs(a.b - b.b) >= d;
                }
            return mask;
        }

        static string MeanOf(Color32[] px, bool[] mask, RectInt rect)
        {
            double r = 0, g = 0, b = 0;
            int n = 0;
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    int i = y * W + x;
                    if (!mask[i]) continue;
                    r += px[i].r; g += px[i].g; b += px[i].b;
                    n++;
                }
            if (n == 0) return "no pixels";
            return string.Format(Inv, "mean ({0:0}, {1:0}, {2:0}) of 255 over {3} px", r / n, g / n, b / n, n);
        }

        static Color32[] Cut(Color32[] frame, Vector2Int centre)
        {
            int x0 = Mathf.Clamp(centre.x - CutW / 2, 0, W - CutW), y0 = Mathf.Clamp(centre.y - CutH / 2, 0, H - CutH);
            var cut = new Color32[CutW * Zoom * CutH * Zoom];
            for (int y = 0; y < CutH * Zoom; y++)
                for (int x = 0; x < CutW * Zoom; x++)
                    cut[y * CutW * Zoom + x] = frame[(y0 + y / Zoom) * W + x0 + x / Zoom];
            return cut;
        }

        /// <summary>Four cuts on a sheet: the default look on top, the plain look under it; BEFORE left, AFTER right.</summary>
        Color32[] Sheet(Color32[] toonBefore, Color32[] toonAfter, Color32[] plainBefore, Color32[] plainAfter)
        {
            int cw = CutW * Zoom, ch = CutH * Zoom;
            _sheetW = cw * 2 + Gap;
            _sheetH = ch * 2 + Gap;
            var sheet = new Color32[_sheetW * _sheetH];
            for (int i = 0; i < sheet.Length; i++) sheet[i] = new Color32(128, 128, 128, 255);
            void Put(Color32[] c, int ox, int oy)
            {
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        sheet[(oy + y) * _sheetW + ox + x] = c[y * cw + x];
            }
            // Rows run from the bottom: the plain look at the bottom, the default look at the top.
            Put(plainBefore, 0, 0);
            Put(plainAfter, cw + Gap, 0);
            Put(toonBefore, 0, ch + Gap);
            Put(toonAfter, cw + Gap, ch + Gap);
            return sheet;
        }

        static void TickEnemies()
        {
            foreach (Enemy e in Enemy.All)
                if (e != null && e.Look != null && e.Look.Toon != null) e.Look.Toon.Tick();
        }

        static readonly MethodInfo s_lateUpdate = typeof(HitFlash).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>Every enemy's tint follows the switch now (as each does in its LateUpdate).</summary>
        static void TickTints()
        {
            foreach (Enemy e in Enemy.All)
                if (e != null && e.TryGetComponent(out HitFlash f)) s_lateUpdate.Invoke(f, null);
        }

        void Look(bool toon)
        {
            ToonLookValues.On = toon;
            ToonLookValues.Enemies = true;
            ToonLook her = _player.GetComponent<ToonLook>();
            if (her != null) her.Tick();
            TickEnemies();
        }

        static void Tints(bool show)
        {
            EnemyTintValues.Show = show;
            TickTints();
        }

        /// <summary>What a body is made of now: each mesh's own colour and shader, its added picture's colour, and this order's block on it.</summary>
        static string Diagnose(Enemy e, string name)
        {
            var parts = new List<string>();
            foreach (Renderer r in BodyRenderers(e))
            {
                Material[] m = r.sharedMaterials;
                if (m.Length == 0 || m[0] == null) continue;
                int slot = -1;
                for (int k = m.Length - 1; k >= 1 && slot < 0; k--) if (m[k] != null && m[k] != m[0]) slot = k;
                string added = "no added picture";
                if (slot >= 0)
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, slot);
                    added = string.Format(Inv, "added picture {0} colour {1}, block on it {2}", m[slot].shader.name, m[slot].GetColor(s_BaseColor),
                                          block.isEmpty ? "none" : block.GetVector(s_BaseColor).ToString("F3"));
                }
                var whole = new MaterialPropertyBlock();
                r.GetPropertyBlock(whole);
                parts.Add(string.Format(Inv, "{0}: {1} colour {2}{3}; {4}", r.name, m[0].shader.name, m[0].GetColor(s_BaseColor),
                                        whole.isEmpty ? "" : ", a block over it " + whole.GetColor(s_BaseColor), added));
            }
            return "  " + name + " (" + e.LogWord + (e.IsDazed ? ", dazed" : "") + (EnemyWarder.IsWarded(e) ? ", warded" : "") + "): " + string.Join(" | ", parts);
        }

        /// <summary>A line-up drawn four ways in this frame, its sheet saved, the bodies measured.</summary>
        void Shoot(Camera cam, string file, string what, IReadOnlyList<(Enemy e, string name)> bodies, Vector3 focus)
        {
            try
            {
                Pose(cam, focus);
                var rects = bodies.Select(b => ScreenRect(cam, b.e)).ToList();
                // The cut round the line-up's middle a metre up, as the camera sees it.
                Vector3 c = cam.WorldToScreenPoint(focus + Vector3.up);
                var centre = new Vector2Int(Mathf.RoundToInt(c.x * W / cam.pixelWidth), Mathf.RoundToInt(c.y * H / cam.pixelHeight));
                _log.Add(file + ".png: " + what + "; top the default look (toon), bottom the plain look; left BEFORE (the switch off: today's), right AFTER (the switch on)");
                var cuts = new Dictionary<string, Color32[]>();
                foreach (bool toon in new[] { true, false })
                {
                    Look(toon);
                    Tints(false);
                    var without = bodies.Select(b => Without(cam, b.e)).ToList();
                    foreach (bool show in new[] { false, true })
                    {
                        Tints(show);
                        Color32[] frame = Draw(cam);
                        string key = (toon ? "toon" : "plain") + (show ? "-after" : "-before");
                        cuts[key] = Cut(frame, centre);
                        var means = new List<string>();
                        for (int i = 0; i < bodies.Count; i++)
                            means.Add(bodies[i].name + " " + MeanOf(frame, MaskOf(frame, without[i], rects[i]), rects[i]));
                        _log.Add("  " + key + ": " + string.Join("; ", means));
                        if (toon && show)
                            foreach ((Enemy e, string name) in bodies) _log.Add(Diagnose(e, name));
                    }
                }
                Color32[] sheet = Sheet(cuts["toon-before"], cuts["toon-after"], cuts["plain-before"], cuts["plain-after"]);
                Write(sheet, _sheetW, _sheetH, file + ".png");
                _sheets.Add(sheet);
            }
            catch (Exception ex) { _log.Add(file + " FAILED: " + ex); }
            finally
            {
                Look(true);
                Tints(true);
            }
        }

        /// <summary>Every sheet of the test one under the other: the contact sheet.</summary>
        void ContactSheet(string file)
        {
            if (_sheets.Count == 0) return;
            int h = _sheets.Count * (_sheetH + Gap * 3);
            var all = new Color32[_sheetW * h];
            for (int i = 0; i < all.Length; i++) all[i] = new Color32(40, 40, 40, 255);
            for (int s = 0; s < _sheets.Count; s++)
            {
                int oy = h - (s + 1) * (_sheetH + Gap * 3); // the first at the top
                for (int y = 0; y < _sheetH; y++)
                    Array.Copy(_sheets[s], y * _sheetW, all, (oy + y) * _sheetW, _sheetW);
            }
            Write(all, _sheetW, h, file + ".png");
            _log.Add(file + ".png: the contact sheet, every sheet of " + file + " one under the other in the order above");
        }

        /// <summary>
        /// Asleep for the picture with her near: its own copy of its values with no range to wake in (a brute's values are the practice
        /// brute's asset otherwise, never to be changed: it is given a floor brute's copy of them first, as the generator gives it).
        /// </summary>
        void Asleep(Enemy e, EnemyBrute template)
        {
            if (e is EnemyBrute brute) brute.FloorValues = _levels.Values.FloorBruteValues(template.Values);
            e.Values.AggroRange = 0f;
        }

        // ---------------- The line-ups ----------------

        /// <summary>Where the line-ups stand and how an enemy is made there: on a floor (joined to it, the mood's added picture on it) or in the practice room.</summary>
        sealed class Place
        {
            public string Name;
            public Vector3 Middle;
            public Func<EnemyKind, Vector3, bool, Enemy> Make;
            public Func<Vector3, EnemyGraveRam> MakeRam;
            public Action<Enemy> Remove;
        }

        IEnumerator LineUps(Place p, string prefix)
        {
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; work order 151; {W}x{H}; {p.Name}; the camera {GameSettings.CameraDistance(Feel.CameraDistance):0} m at {Feel.CameraPitch:0} degrees, field of view {Feel.FieldOfView:0}; cuts {CutW}x{CutH} of the frame drawn x{Zoom}; the toon look's default {ToonLookValues.On}, its enemies {ToonLookValues.Enemies}; the hit chips {HitChipsValues.On}");
            Vector3 mid = p.Middle;
            Vector3 At(float dx) => OnNavMesh(mid + new Vector3(dx, 0f, 0f));
            var made = new List<Enemy>();
            Enemy Make(EnemyKind kind, float dx, bool elite = false)
            {
                Enemy e = p.Make(kind, At(dx), elite);
                made.Add(e);
                return e;
            }
            IEnumerator Settle(float herSouth = HerSouth)
            {
                _player.Teleport(OnNavMesh(mid + new Vector3(0f, 0f, -herSouth)));
                yield return GameTime(1.2f);
            }
            void Clear()
            {
                foreach (Enemy e in made) if (e != null) p.Remove(e);
                made.Clear();
            }

            // 1. A brute beside an elite brute.
            {
                Enemy brute = Make(EnemyKind.Brute, -Apart * 0.5f), elite = Make(EnemyKind.Brute, Apart * 0.5f, elite: true);
                yield return Settle();
                Shoot(cam, prefix + "-1-brute-elite", "a brute (left) and an elite brute (right), asleep", new[] { (brute, "brute"), (elite, "elite brute") }, mid);
                Clear();
                yield return null;
            }
            // 2. A caster beside a warder.
            {
                Enemy caster = Make(EnemyKind.Caster, -Apart * 0.5f), warder = Make(EnemyKind.Warder, Apart * 0.5f);
                yield return Settle();
                Shoot(cam, prefix + "-2-caster-warder", "a caster (left) and a warder (right), asleep", new[] { (caster, "caster"), (warder, "warder") }, mid);
                Clear();
                yield return null;
            }
            // 4. A lancer beside a dazed lancer.
            {
                Enemy lancer = Make(EnemyKind.Lancer, -Apart * 0.5f), dazed = Make(EnemyKind.Lancer, Apart * 0.5f);
                yield return Settle();
                dazed.Body.DazeFor(60f);
                yield return GameTime(0.4f);
                Shoot(cam, prefix + "-4-lancer-dazed-lancer", "a lancer (left) and a lancer dazed (right, " + dazed.IsDazed + ")", new[] { (lancer, "lancer"), (dazed, "dazed lancer") }, mid);
                Clear();
                yield return null;
            }
            // 5. The Grave Ram beside a brute.
            {
                Enemy brute = Make(EnemyKind.Brute, -Apart * 0.6f);
                EnemyGraveRam ram = p.MakeRam(At(Apart * 0.6f));
                made.Add(ram);
                ram.Values.AggroRange = 0f; // its own values (its calm ones), not an asset's
                yield return Settle();
                Shoot(cam, prefix + "-5-brute-graveram", "a brute (left) and the Grave Ram (right), asleep", new[] { (brute, "brute"), ((Enemy)ram, "Grave Ram") }, mid);
                Clear();
                yield return null;
            }
            // 6. The caller beside a caster and a warder.
            {
                Enemy caster = Make(EnemyKind.Caster, -Apart), caller = Make(EnemyKind.Caller, 0f), warder = Make(EnemyKind.Warder, Apart);
                yield return Settle();
                Shoot(cam, prefix + "-6-caster-caller-warder", "a caster (left), the caller (middle) and a warder (right), asleep", new[] { (caster, "caster"), (caller, "caller"), (warder, "warder") }, mid);
                Clear();
                yield return null;
            }
            // 7. A swarmer beside a bomber (its ember tint, laid the same way).
            {
                Enemy swarmer = Make(EnemyKind.Swarmer, -Apart * 0.5f), bomber = Make(EnemyKind.Bomber, Apart * 0.5f);
                yield return Settle();
                Shoot(cam, prefix + "-7-swarmer-bomber", "a swarmer (left) and a bomber (right), asleep", new[] { (swarmer, "swarmer"), (bomber, "bomber") }, mid);
                Clear();
                yield return null;
            }
            // 8. An elite brute beside an elite brute in a hit's flash (the chips off, as they were before work order 127).
            {
                HitChipsValues.On = false;
                Enemy still = Make(EnemyKind.Brute, -Apart * 0.5f, elite: true), hit = Make(EnemyKind.Brute, Apart * 0.5f, elite: true);
                foreach (Enemy e in new[] { still, hit }) e.Values.MoveSpeed = 0f; // the hit wakes them: they stay on their spot for the second picture
                yield return Settle();
                HitFlash flash = hit.GetComponent<HitFlash>();
                for (int i = 0; i < 10 && !flash.IsFlashing; i++)
                {
                    hit.Body.TakeHit(1);
                    yield return null;
                }
                Shoot(cam, prefix + "-8-elite-elite-in-a-flash", "an elite brute (left) and an elite brute in a hit's flash (right, " + flash.IsFlashing + "; the hit chips off for this picture)", new[] { (still, "elite brute"), (hit, "elite brute flashing") }, mid);
                yield return Until(() => !flash.IsFlashing, 2f);
                yield return null;
                Shoot(cam, prefix + "-9-elite-elite-after-the-flash", "the same a moment after the flash", new[] { (still, "elite brute"), (hit, "elite brute after its flash") }, mid);
                HitChipsValues.On = _chipsWere;
                Clear();
                yield return null;
            }
            // 3. A warded enemy beside an unwarded one, the warder by them (awake, warding; the two brutes kept on their spot; taken last, as the warder's waking wakes the room).
            {
                Enemy a = Make(EnemyKind.Brute, -Apart * 0.5f), b = Make(EnemyKind.Brute, Apart * 0.5f);
                var warder = (EnemyWarder)Make(EnemyKind.Warder, Apart * 1.5f);
                foreach (Enemy e in new[] { a, b }) e.Values.MoveSpeed = 0f; // their own copies: they stand where they are, awake or not
                _player.Teleport(OnNavMesh(mid + new Vector3(0f, 0f, -9f))); // the warder's spot is 8 to 10 m from her: about by them
                yield return null;
                warder.Provoke();
                yield return Until(() => EnemyWarder.IsWarded(a) || EnemyWarder.IsWarded(b), 15f);
                yield return GameTime(0.5f);
                Enemy warded = EnemyWarder.IsWarded(a) ? a : EnemyWarder.IsWarded(b) ? b : null;
                if (warded == null) _log.Add(prefix + "-3 NOT TAKEN: no ward came on in 15 s");
                else
                {
                    Enemy plain = warded == a ? b : a;
                    Vector3 focus = (a.transform.position + b.transform.position + warder.transform.position) / 3f;
                    Shoot(cam, prefix + "-3-unwarded-warded", string.Format(Inv, "an unwarded brute ({0}) and a warded brute ({1}), its warder {2:0.0} m from it, warding", warded == a ? "right" : "left", warded == a ? "left" : "right",
                          Vector3.Distance(warded.transform.position, warder.transform.position)), new[] { (plain, "unwarded brute"), (warded, "warded brute"), ((Enemy)warder, "warder") }, focus);
                }
                Clear();
                yield return null;
            }
            ContactSheet(prefix + "-contact-sheet");
            _sheets.Clear();
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheTintedKinds_BesideTheirUntintedTwins_OnFloor5sDarkFloor_BeforeAndAfter_InBothLooks()
        {
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 5), 5);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LevelRoom room = RoomCallerRules.CombatRooms(level.Layout)[0];
            foreach (Enemy e in level.EnemiesByRoom[room.Order].ToList()) level.Leave(e);
            EnemyBrute template = Object.FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID).First(b => b.ComesBack && b.GetComponentInParent<GeneratedLevel>() == null);
            int n = 0;
            var place = new Place
            {
                Name = "floor 5 " + RoomThemes.Word(room.Theme) + " room " + room.Letter + room.Order + " (its mood on: the floor's light floor for enemies " + level.Mood?.GetType().Name + ")",
                Middle = OpenSpot(level.Middle(room), 8f),
                Make = (kind, at, elite) =>
                {
                    Enemy e = EnemyFactory.Make(kind, at, level.transform, template, "Picture151_" + kind + "_" + (++n));
                    if (elite)
                    {
                        LevelValues v = _levels.Values;
                        EnemyFactory.MakeElite(e, v.EliteHealthOf(kind), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
                    }
                    if (e is EnemyCaller caller) caller.UseValues(_levels.Values.CallerRules);
                    e.HealthOverride = 1000000;
                    level.Join(e, room, roomsOwn: true); // the floor's light on enemies, as in the game
                    Asleep(e, template);
                    return e;
                },
                MakeRam = at =>
                {
                    EnemyGraveRam ram = EnemyGraveRam.Make(at, level.transform, template, "Picture151_GraveRam_" + (++n), 151UL);
                    level.Join(ram, room, roomsOwn: true);
                    return ram;
                },
                Remove = e => level.Leave(e),
            };
            yield return LineUps(place, "151-dark");
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheTintedKinds_BesideTheirUntintedTwins_InThePracticeRoomsLightFloor_BeforeAndAfter_InBothLooks()
        {
            _levels.ClearSpawned();
            EnemyBrute template = Object.FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID).First(b => b.ComesBack && b.GetComponentInParent<GeneratedLevel>() == null);
            _practiceRoot = new GameObject("Pictures151").transform;
            int n = 0;
            // The practice room's open floor north of where she starts.
            Vector3 start = OnNavMesh(_player.Position);
            var place = new Place
            {
                Name = "the practice room (its light floor, no mood)",
                Middle = OpenSpot(start + new Vector3(0f, 0f, 7f), 6f),
                Make = (kind, at, elite) =>
                {
                    Enemy e = EnemyFactory.Make(kind, at, _practiceRoot, template, "Picture151_" + kind + "_" + (++n));
                    if (elite)
                    {
                        LevelValues v = _levels.Values;
                        EnemyFactory.MakeElite(e, v.EliteHealthOf(kind), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
                    }
                    if (e is EnemyCaller caller) caller.UseValues(_levels.Values.CallerRules);
                    e.HealthOverride = 1000000;
                    Asleep(e, template);
                    return e;
                },
                MakeRam = at => EnemyGraveRam.Make(at, _practiceRoot, template, "Picture151_GraveRam_" + (++n), 151UL),
                Remove = e => Object.Destroy(e.gameObject),
            };
            yield return LineUps(place, "151-light");
        }

        /// <summary>The project's own measure of a frame (work order 032's benchmark), its floor 5 fight: the switch on, off, on, off.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_WithTheSwitchOnAndOff()
        {
            foreach (bool show in new[] { true, false, true, false })
            {
                EnemyTintValues.Show = show;
                string label = show ? "tints-on" : "tints-off";
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeTint151-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", label, "-benchmarkScenarios", "3", "-benchmarkSeconds", "5",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                float until = Time.realtimeSinceStartup + 120f;
                int mostLaid = 0, mostEnemies = 0;
                while (!b.Done && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    int laid = 0, enemies = 0;
                    foreach (Enemy e in Enemy.All)
                    {
                        if (e == null || e.IsDead) continue;
                        enemies++;
                        if (e.TryGetComponent(out HitFlash f)) laid += f.Tint.LaidCount;
                    }
                    mostLaid = Math.Max(mostLaid, laid);
                    mostEnemies = Math.Max(mostEnemies, enemies);
                }
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult s = r.Scenarios[0];
                string counters = string.Join(", ", s.Counters.Where(c => c.Works).Select(c => string.Format(Inv, "{0} {1:0.###} {2}", c.Name, c.Mean, c.Unit)));
                string line = string.Format(Inv, "BENCHMARK {0} {1}: {2} frames, mean {3:0.000} ms, p95 {4:0.000}, worst {5:0.000}; {6} fighting; at most {7} meshes with a tint laid among {8} enemies alive; counters: {9}",
                                            s.Scenario, label, s.Frames, s.FrameMs.Mean, s.FrameMs.P95, s.FrameMs.Max, s.EnemiesFighting, mostLaid, mostEnemies, counters);
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }
    }
}
