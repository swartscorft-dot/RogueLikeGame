using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 125, the toon look made clean (Kristian, 2026-11-25, of work order 119's pictures: "man ser mange fragmenterte
    /// pixler rundt omkring, det ser veldig dårlig ut. Kan en agent gå over det å få det se bra ut."): with the clean line on, her
    /// model and an enemy's own meshes are drawn round by the camera's line and their materials' hull is off; off, the hull is
    /// on again and nothing is drawn round (work order 119's line, exactly); with the enemies' look off an enemy's materials are
    /// as they were, the hull's pass on; a thing that wears an enemy's material and its rendering layers (as a loose bone piece
    /// does, work order 122) gets no line; the fragmentation measure passes the clean line on a swarmer at the game's camera and
    /// fails work order 119's on the same frame; the anti-aliasing follows its switch on the game's camera; the pause menu's line
    /// flips it; and a frame with the switches standing allocates nothing.
    /// </summary>
    public class CleanToonLookTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);
        const int W = 1920, H = 1080;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        float _ink;
        readonly List<GameObject> _made = new List<GameObject>();

        IEnumerator Load()
        {
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            ToonLookValues.CleanInk = true;
            AntiAliasingValues.On = true;
            _ink = ToonLookValues.Live.InkWidth;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            ToonLookValues.CleanInk = true;
            AntiAliasingValues.On = true;
            if (_player != null)
            {
                ToonLookValues.Live.InkWidth = _ink;
            }
            foreach (GameObject g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
            if (_player != null && _player.GetComponent<PlayerHealth>() != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        List<Renderer> HerRenderers()
        {
            ToonLook look = _player.GetComponent<ToonLook>();
            var list = new List<Renderer>();
            for (int i = 0; i < look.TargetCount; i++) if (look.TargetRenderer(i) != null) list.Add(look.TargetRenderer(i));
            return list;
        }

        static Color32[] Draw(Camera cam, int samples)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
            var plain = samples > 1 ? new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) : null;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture read = rt;
                if (plain != null) { Graphics.Blit(rt, plain); read = plain; }
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
                if (plain != null) { plain.Release(); Object.Destroy(plain); }
                Object.Destroy(tex);
            }
        }

        void Pose(Camera cam, Vector3 focus)
        {
            FeelValues feel = _player.Feel ?? new FeelValues();
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * GameSettings.CameraDistance(feel.CameraDistance), rotation);
        }

        static void TickAll(PlayerCharacter player)
        {
            player.GetComponent<ToonLook>().Tick();
            foreach (Enemy e in Enemy.All) if (e != null && e.Look != null && e.Look.Toon != null) e.Look.Toon.Tick();
        }

        // ---------------- The switch ----------------

        static bool CleanSet(Material m) => m.GetFloat("_OutlineAlongRay") == 1f && m.GetFloat("_InkNeedsMark") == 1f;
        static bool Set119(Material m) => m.GetFloat("_OutlineAlongRay") == 0f && m.GetFloat("_InkNeedsMark") == 0f;

        [UnityTest]
        public IEnumerator TheCleanLineOn_MarksHerAndAnEnemysOwnRenderers_AndSetsTheirHull_AndOff_IsWorkOrder119sLineExactly()
        {
            yield return Load();
            Enemy brute = _levels.SpawnPractice(EnemyKind.Brute)[0];
            yield return Frames(2);
            List<Renderer> her = HerRenderers();
            List<Renderer> its = brute.GetComponentsInChildren<Renderer>(true).Where(ToonInkFigures.Has).ToList();
            ToonLook herLook = _player.GetComponent<ToonLook>();
            Assert.That(her, Is.Not.Empty);
            Assert.That(her.All(ToonInkFigures.Has), Is.True, "her renderers are marked for the line");
            Assert.That(its, Is.Not.Empty, "the brute's own renderers are marked for the line");
            Assert.That(brute.Look.Toon.Materials, Is.Not.Empty);
            Assert.That(brute.Look.Toon.Materials.All(CleanSet), Is.True, "the brute's hull pushed along the line of sight, only round a marked renderer");
            Assert.That(herLook.Made.All(CleanSet), Is.True, "hers too");

            ToonLookValues.CleanInk = false;
            herLook.Tick();
            brute.Look.Toon.Tick();
            Assert.That(her.Any(ToonInkFigures.Has) || its.Any(ToonInkFigures.Has), Is.False, "off: nothing marked (every renderer's user value as before)");
            Assert.That(brute.Look.Toon.Materials.All(Set119), Is.True, "off: work order 119's hull on the brute");
            Assert.That(herLook.Made.All(Set119), Is.True, "off: work order 119's hull on her");
            Assert.That(herLook.Made.All(m => m.GetFloat("_OutlineWidth") == ToonLookValues.Live.InkWidth && m.GetFloat("_OutlineDepthPush") == ToonLookValues.Live.InkDepthPush), Is.True, "119's width and push");

            ToonLookValues.CleanInk = true;
            herLook.Tick();
            brute.Look.Toon.Tick();
            Assert.That(her.All(ToonInkFigures.Has) && its.All(ToonInkFigures.Has), Is.True, "on again");
            EnemyToonLook itsLook = brute.Look.Toon;
            herLook.Tick();
            itsLook.Tick();
            Assert.That(() => { herLook.Tick(); itsLook.Tick(); }, NotAllocating, "a frame with the switch standing");
        }

        [UnityTest]
        public IEnumerator TheEnemiesLookOff_UnmarksTheirRenderers_AndTheirMaterialsAreSimpleLitAgain()
        {
            yield return Load();
            Enemy guard = _levels.SpawnPractice(EnemyKind.Guard)[0];
            yield return Frames(2);
            var mats = guard.Look.Toon.Materials.ToList();
            Assert.That(mats, Is.Not.Empty);
            Assert.That(guard.GetComponentsInChildren<Renderer>(true).Any(ToonInkFigures.Has), Is.True);
            ToonLookValues.Enemies = false;
            guard.Look.Toon.Tick();
            Assert.That(guard.Look.Toon.Shown, Is.False);
            Assert.That(mats.All(m => m.shader.name == PropFactory.ShaderName), Is.True, "Simple Lit again");
            Assert.That(guard.GetComponentsInChildren<Renderer>(true).All(r => ToonInkFigures.UserValue(r) == 0u), Is.True, "every renderer's user value as before");
        }

        // ---------------- A loose piece wearing an enemy's material (work order 122) ----------------

        [UnityTest]
        public IEnumerator AThingWearingAnEnemysMaterialAndItsLayers_ButNotOneOfItsOwnRenderers_GetsNoLine()
        {
            yield return Load();
            Enemy sw = _levels.SpawnPractice(EnemyKind.Swarmer)[0];
            yield return Frames(3);
            Assert.That(sw.Look.Toon.Shown, Is.True, "its look on");
            Renderer own = sw.Look.Skin;
            Assert.That(own, Is.Not.Null);
            // A piece as work order 122 makes one: a small mesh of its own with every material of the enemy's renderer and its
            // rendering layers, and no component of the enemy's.
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(piece);
            Object.Destroy(piece.GetComponent<Collider>());
            piece.transform.position = _player.Position + new Vector3(4f, 0.3f, 0f);
            piece.transform.localScale = Vector3.one * 0.6f;
            var r = piece.GetComponent<MeshRenderer>();
            r.sharedMaterials = own.sharedMaterials;
            r.renderingLayerMask = own.renderingLayerMask;
            r.shadowCastingMode = ShadowCastingMode.Off;
            yield return Frames(2);
            Assert.That(ToonInkFigures.Has(r), Is.False, "not marked");
            Camera cam = Camera.main;
            Pose(cam, piece.transform.position);
            Color32[] unmarked = Draw(cam, 1);
            ToonInkFigures.Mark(r, true);
            Color32[] marked = Draw(cam, 1);
            ToonInkFigures.Mark(r, false);
            r.enabled = false;
            Color32[] gone = Draw(cam, 1);
            r.enabled = true;
            // The piece's own pixels, and the ring of 2 px round them.
            Vector3 s = cam.WorldToViewportPoint(piece.transform.position);
            int cx = Mathf.RoundToInt(s.x * W), cy = Mathf.RoundToInt(s.y * H), ring = 0, ringInkedUnmarked = 0, ringInkedMarked = 0;
            bool Covers(int x, int y)
            {
                Color32 p = unmarked[y * W + x], q = gone[y * W + x];
                return System.Math.Abs(p.r - q.r) + System.Math.Abs(p.g - q.g) + System.Math.Abs(p.b - q.b) > 30;
            }
            for (int y = Mathf.Max(2, cy - 80); y < Mathf.Min(H - 2, cy + 80); y++)
                for (int x = Mathf.Max(2, cx - 80); x < Mathf.Min(W - 2, cx + 80); x++)
                {
                    if (Covers(x, y)) continue;
                    bool near = false;
                    for (int dy = -2; dy <= 2 && !near; dy++) for (int dx = -2; dx <= 2 && !near; dx++) near = Covers(x + dx, y + dy);
                    if (!near) continue;
                    ring++;
                    int i = y * W + x;
                    float lg = gone[i].g, lu = unmarked[i].g, lm = marked[i].g;
                    if (lu < lg * 0.7f) ringInkedUnmarked++;
                    if (lm < lg * 0.7f) ringInkedMarked++;
                }
            string line = string.Format(Inv, "CLEANTOON a piece wearing the swarmer's materials: {0} px round it, {1} inked unmarked, {2} inked if it were marked", ring, ringInkedUnmarked, ringInkedMarked);
            TestContext.WriteLine(line);
            Assert.That(ring, Is.GreaterThan(50), "the piece is in the picture");
            Assert.That(ringInkedMarked, Is.GreaterThan(ring / 4), "marked, it would have its line (the measure sees a line): " + line);
            Assert.That(ringInkedUnmarked, Is.EqualTo(0), "unmarked, no ink round it: " + line);
        }

        // ---------------- The measure ----------------

        static float[] Luma(Color32[] px, int x0, int y0, int w, int h)
        {
            var l = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[(y0 + y) * W + x0 + x];
                    l[y * w + x] = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                }
            return l;
        }

        [UnityTest]
        public IEnumerator TheMeasure_PassesTheCleanLineOnASwarmerCloseUpAndAtTheGamesCamera_AndFailsWorkOrder119sCloseUpOnTheSameFrame()
        {
            yield return Load();
            _player.Teleport(new Vector3(20f, 0f, 6f));
            yield return Frames(2);
            List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
            Enemy sw = pack[0];
            // Work order 154: the measured frame the same whenever the test runs. Where it was made, and the way it faced.
            Vector3 placed = sw.transform.position;
            Quaternion facing = sw.transform.rotation;
            yield return Frames(3);
            string found = FrameFound(sw, pack, placed, facing);
            List<Renderer> figure = sw.GetComponentsInChildren<Renderer>(true).Where(ToonInkFigures.Has).ToList();
            foreach (Renderer r in figure) r.shadowCastingMode = ShadowCastingMode.Off;
            FeelValues feel = _player.Feel ?? new FeelValues();
            void Line(bool clean, bool on)
            {
                ToonLookValues.CleanInk = clean;
                ToonLookValues.Live.InkWidth = on ? _ink : 0f;
                TickAll(_player);
                sw.Look.Toon.Tick();
                foreach (Material m in sw.Look.Toon.Materials) m.SetFloat("_OutlineWidth", ToonLookValues.Live.InkWidth); // its own width now (pushed only when its look goes on)
            }
            foreach (Enemy e in pack)
            {
                e.enabled = false; // they stand as they are (and leave Enemy.All: its look is ticked here)
                NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false; // and nothing walks them on to where they were going
            }
            // Work order 154: the pack's others out of the picture, and the swarmer where it was made, facing as it was made, standing
            // in the first frame of its Idle. Before, the others were moved 30 m off, and their agents put them back on the NavMesh:
            // they stood 0.5 to 0.8 m from it, in its box, where their spawn's first frames had pushed them (in the class run two on
            // one point and two 0.7 m off: 93.2 % of the ink in large pieces; the test alone, other places: 94.7 %), and it had
            // walked 0.4 m and run its Idle on by those frames' length. Now the frame is the same whenever the test runs.
            for (int i = 1; i < pack.Count; i++) pack[i].gameObject.SetActive(false);
            sw.transform.SetPositionAndRotation(placed, facing);
            StandInIdle(sw, 0.0);
            yield return Frames(2);
            StandInIdle(sw, 0.0);
            TestContext.WriteLine(found + " | measured at " + Where(sw));
            var results = new List<(bool close, InkFragments.Result clean, InkFragments.Result hull, string line, int covered)>();
            foreach (bool close in new[] { true, false })
            {
                (InkFragments.Result rClean, InkFragments.Result rHull, int covered) = MeasureOnce(sw, figure, close, Line, feel);
                string line = "CLEANTOON swarmer " + (close ? "close up" : "at the game's camera") + ": the clean line multisampled " + rClean + " | 119's line " + rHull;
                TestContext.WriteLine(line);
                Debug.Log(line);
                results.Add((close, rClean, rHull, line, covered));
            }
            // Work order 154: the same measure in other moments of its Idle, written down only (the asserted frame is the first):
            // how near the clean line stands to the 94 % in the poses a standing swarmer goes through.
            AnimationClip idle = EnemyLook.FindClip(sw.Look.Spec, "Idle");
            double length = idle != null ? idle.length : 0.0;
            foreach (double share in IdleShares)
            {
                if (length <= 0.0) break;
                StandInIdle(sw, share * length);
                foreach (bool close in new[] { true, false })
                {
                    var (c, h, _) = MeasureOnce(sw, figure, close, Line, feel);
                    TestContext.WriteLine(string.Format(Inv, "CLEANTOON [154] Idle at {0:0.00} of its {1:0.00} s, {2}: the clean line {3} | 119's line {4}",
                                                        share, length, close ? "close up" : "at the game's camera", c, h));
                }
            }
            StandInIdle(sw, 0.0);
            foreach ((bool close, InkFragments.Result rClean, InkFragments.Result rHull, string line, int covered) in results)
            {
                Assert.That(covered, Is.GreaterThan(200), "the swarmer is in the picture");
                Assert.That(rClean.Passes, Is.True, line);
                // At the game's camera a lone swarmer standing still is small and 119's line on it is whole too (its push moves it
                // by under a pixel there): the fault it shows close up, and in the fights among many.
                if (close) Assert.That(rHull.Passes, Is.False, "the measure sees 119's fault: " + line);
            }
        }

        /// <summary>Work order 154: the moments of its Idle the measure is also written down for (shares of the clip).</summary>
        static readonly double[] IdleShares = { 0.25, 0.5, 0.75 };

        /// <summary>
        /// Work order 154: the swarmer standing in its Idle at <paramref name="seconds"/> into the clip, every other clip out,
        /// posed now (its look's own Reset, then one tick of exactly that time, then its pose made whether or not it is in sight).
        /// </summary>
        static void StandInIdle(Enemy sw, double seconds)
        {
            EnemyLook look = sw.Look;
            float now = Time.time;
            look.Tick(sw, Vector3.zero, now); // its clock set to now
            look.Reset();                     // the first frame of Idle, all of it
            look.Tick(sw, Vector3.zero, now + (float)seconds);
            look.PoseNow();
        }

        static string Where(Enemy sw) => string.Format(Inv, "({0:0.000}, {1:0.000}) facing {2:0.0}, {3} at {4:0.000} s, skin box {5} {6}",
            sw.transform.position.x, sw.transform.position.z, sw.transform.eulerAngles.y, sw.Look.Playing, sw.Look.PlayingTime,
            sw.Look.Skin.bounds.center.ToString("F3"), sw.Look.Skin.bounds.size.ToString("F3"));

        /// <summary>Work order 154: what the swarmer's first five frames made of it, as found before it is put to the measured frame.</summary>
        static string FrameFound(Enemy sw, List<Enemy> pack, Vector3 placed, Quaternion facing) => string.Format(Inv,
            "CLEANTOON [154] found after its first frames (clock {0:0.000} s, frame {1}, last step {2:0.0000} s): Id {3}, {4}, moved {5:0.000} m, turned {6:0.0} degrees, state {7}, in sight {8}, posed {9}; its pack {10}; the camera at {11}",
            Time.time, Time.frameCount, Time.deltaTime, sw.Id, Where(sw), Vector3.Distance(sw.transform.position, placed),
            Quaternion.Angle(sw.transform.rotation, facing), sw.State, sw.Look.InSight, sw.Look.Posed, pack.Count,
            Camera.main != null ? Camera.main.transform.position.ToString("F2") : "none");

        (InkFragments.Result clean, InkFragments.Result hull, int covered) MeasureOnce(Enemy sw, List<Renderer> figure, bool close, System.Action<bool, bool> setLine, FeelValues feel)
        {
            Camera cam = Camera.main;
            {
                Vector3 middle = sw.Look.Skin.bounds.center;
                if (close)
                {
                    cam.fieldOfView = feel.FieldOfView;
                    Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
                    cam.transform.SetPositionAndRotation(middle - rotation * Vector3.forward * (3.2f + 2.2f * Mathf.Max(1.5f, sw.ModelHeight)), rotation);
                }
                else Pose(cam, middle);
                setLine(true, false);
                Color32[] none = Draw(cam, 1), noneAa = Draw(cam, AntiAliasingValues.MsaaSamples);
                setLine(true, true);
                Color32[] clean = Draw(cam, AntiAliasingValues.MsaaSamples);
                setLine(false, true);
                Color32[] hull = Draw(cam, 1);
                setLine(true, true);
                foreach (Renderer r in figure) r.enabled = false;
                Color32[] empty = Draw(cam, 1);
                foreach (Renderer r in figure) r.enabled = true;
                // The box round what the swarmer covers, grown by 8 px.
                int minX = W, minY = H, maxX = -1, maxY = -1;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color32 p = none[y * W + x], q = empty[y * W + x];
                        if (System.Math.Abs(p.r - q.r) + System.Math.Abs(p.g - q.g) + System.Math.Abs(p.b - q.b) <= 30) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                Assert.That(maxX, Is.GreaterThan(0), "the swarmer is in the picture");
                int x0 = Mathf.Max(0, minX - 8), y0 = Mathf.Max(0, minY - 8), bw = Mathf.Min(W - 1, maxX + 8) - x0 + 1, bh = Mathf.Min(H - 1, maxY + 8) - y0 + 1;
                var fig = new bool[bw * bh];
                for (int y = 0; y < bh; y++)
                    for (int x = 0; x < bw; x++)
                    {
                        Color32 p = none[(y0 + y) * W + x0 + x], q = empty[(y0 + y) * W + x0 + x];
                        fig[y * bw + x] = System.Math.Abs(p.r - q.r) + System.Math.Abs(p.g - q.g) + System.Math.Abs(p.b - q.b) > 30;
                    }
                InkFragments.Result rClean = InkFragments.Measure(InkFragments.InkOf(Luma(clean, x0, y0, bw, bh), Luma(noneAa, x0, y0, bw, bh)), bw, bh, fig);
                InkFragments.Result rHull = InkFragments.Measure(InkFragments.InkOf(Luma(hull, x0, y0, bw, bh), Luma(none, x0, y0, bw, bh)), bw, bh, fig);
                Pictures(close, clean, hull, x0, y0, bw, bh);
                return (rClean, rHull, fig.Count(b => b));
            }
        }

        /// <summary>
        /// Work order 154: with ROGUELIKE_CLEANTOON_PICTURES set to a folder, the measured box of the clean line and of 119's is
        /// written there as pictures (for the producer, when the measure is near its line); unset, nothing is written.
        /// </summary>
        static int s_pictures;
        static void Pictures(bool close, Color32[] clean, Color32[] hull, int x0, int y0, int bw, int bh)
        {
            string folder = System.Environment.GetEnvironmentVariable("ROGUELIKE_CLEANTOON_PICTURES");
            if (string.IsNullOrEmpty(folder)) return;
            System.IO.Directory.CreateDirectory(folder);
            s_pictures++;
            foreach ((string name, Color32[] px) in new[] { ("clean", clean), ("hull119", hull) })
            {
                var tex = new Texture2D(bw, bh, TextureFormat.RGBA32, false);
                var box = new Color32[bw * bh];
                for (int y = 0; y < bh; y++) for (int x = 0; x < bw; x++) box[y * bw + x] = px[(y0 + y) * W + x0 + x];
                tex.SetPixels32(box);
                tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, string.Format(Inv, "cleantoon-{0:00}-{1}-{2}.png", s_pictures, close ? "close" : "game", name)), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
        }

        // ---------------- The anti-aliasing and its line ----------------

        [UnityTest]
        public IEnumerator TheAntiAliasing_FollowsItsSwitchOnTheGamesCamera_AndThePauseMenusLineFlipsIt()
        {
            yield return Load();
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            AntiAliasingValues.Kind = AntiAliasingKind.Msaa4x;
            Draw(cam, 1);
            Assert.That(cam.allowMSAA, Is.True, "on: multisampling allowed (the asset asks for 4 samples)");
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            RunHud hud = RunHud.Ensure();
            yield return null;
            Assert.That(hud.Escape(), Is.True, "paused");
            yield return null;
            List<RunHud.MenuLine> lines = hud.MenuLines.ToList();
            Assert.That(lines.IndexOf(RunHud.MenuLine.AntiAliasing), Is.EqualTo(lines.IndexOf(RunHud.MenuLine.ToonLook) - 1), "just above the toon look's line");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(hud.MenuText(RunHud.MenuLine.AntiAliasing), Is.EqualTo("Anti-aliasing:  on"));
            Assert.That(hud.PickMenuLine(RunHud.MenuLine.AntiAliasing), Is.True);
            Assert.That(AntiAliasingValues.On, Is.False);
            Assert.That(hud.MenuText(RunHud.MenuLine.AntiAliasing), Is.EqualTo("Anti-aliasing:  off"));
            Draw(cam, 1);
            Assert.That(cam.allowMSAA, Is.False, "off: no multisampling, as before work order 125");
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.AntiAliasing, -1), Is.True, "the left arrow flips it too");
            Assert.That(AntiAliasingValues.On, Is.True);
            string a = hud.MenuText(RunHud.MenuLine.AntiAliasing);
            TestDelegate words = () => { string _ = hud.MenuText(RunHud.MenuLine.AntiAliasing); };
            Assert.That(words, NotAllocating, "the line's words are fixed strings");
            Assert.That(hud.MenuText(RunHud.MenuLine.AntiAliasing), Is.SameAs(a));
            hud.Escape();
        }
    }
}
