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
    /// Work order 115, a look order's pictures: her model BEFORE (today's materials, the switch off) and AFTER (the toon look) in
    /// the same frame, so her pose and everything round her are the same in both; taken without a window the way 091, 100 and 102
    /// took theirs (<see cref="LaserBeamPicturesTests"/>). Every picture is drawn at 1920 by 1080, the screen the ink line's width
    /// is named at; each is saved whole BEFORE and AFTER (JPEG) and as a pair (PNG, BEFORE left, AFTER right) cut round her at
    /// the screen's own pixels. It changes nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_115 names
    /// a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_115 = 'C:\Users\kriss\.claude-automation\runner\captures\115'
    ///   run-tests.ps1 PlayMode -Filter "ToonLookPicturesTests"
    ///
    /// The pictures: close up from the front and from the game's angle (the practice room); the game's distance in the practice
    /// room; the nave of 085's style room (room lab, ordinary, floor 1, seed KCT7B24U) at lamplit C standing still, beside one
    /// skeleton, and in a crowded fight; one picture of that fight with the skeletons in the toon look too (the whole game's look,
    /// not left on); running and dashing. And the cost: the frame drawn with the look on and off in that fight, by the clock round
    /// the camera's draw and its read back (the GPU's work in it), and by the project's benchmark (its floor 5 fight).
    /// </summary>
    public class ToonLookPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_115";
        const int W = 1920, H = 1080;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();
        readonly List<GameObject> _putAway = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 115)");
            Directory.CreateDirectory(_folder);
            ToonLookValues.On = true;
            ToonLookValues.Enemies = false; // work order 119: these are her pictures beside skeletons in today's look, as taken for 115
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
        ToonLook Look => _player.GetComponent<ToonLook>();
        /// <summary>The middle of her model as drawn (her renderers' bounds; the character's own point stands over her feet by the agent's offset).</summary>
        Vector3 Chest
        {
            get
            {
                ToonLook look = Look;
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

        /// <summary>The camera in front of her face to face, at her chest's height, <paramref name="distance"/> m out.</summary>
        void PoseFront(Camera cam, float distance)
        {
            CharacterAnimation anim = _player.GetComponentInChildren<CharacterAnimation>(true);
            Vector3 forward = anim != null ? anim.transform.forward : _player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            cam.fieldOfView = Feel.FieldOfView;
            Vector3 at = Chest;
            cam.transform.SetPositionAndRotation(at + forward * distance + Vector3.up * 0.15f, Quaternion.LookRotation(-forward, Vector3.up));
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

        /// <summary>
        /// BEFORE and AFTER of the camera as it stands, in this frame: each whole, and the pair cut <paramref name="cw"/> by
        /// <paramref name="ch"/> round <paramref name="centre"/> (her chest unless said) at the screen's own pixels.
        /// </summary>
        void Shot(Camera cam, string name, string what, int cw, int ch, Vector3? centre = null)
        {
            try
            {
                ToonLookValues.On = false;
                Look.Tick();
                Color32[] off = Draw(cam);
                ToonLookValues.On = true;
                Look.Tick();
                Color32[] on = Draw(cam);
                Write(off, W, H, name + "-before.jpg", true);
                Write(on, W, H, name + "-after.jpg", true);
                Vector3 s = cam.WorldToViewportPoint(centre ?? Chest); // the viewport: the camera's own pixel size is the window's, not the picture's
                s.x *= W;
                s.y *= H;
                cw = Mathf.Min(cw, W);
                ch = Mathf.Min(ch, H);
                int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x) - cw / 2, 0, W - cw), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y) - ch / 2, 0, H - ch);
                var pair = new Color32[cw * 2 * ch];
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        pair[y * cw * 2 + x] = off[(y0 + y) * W + x0 + x];
                        pair[y * cw * 2 + cw + x] = on[(y0 + y) * W + x0 + x];
                    }
                Write(pair, cw * 2, ch, name + "-pair.png", false);
                _log.Add(string.Format(Inv, "{0}: {1} (pair: BEFORE left, AFTER right, {2} by {3} px each cut round her from the 1920 by 1080 frame)", name, what, cw, ch));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e.Message); }
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        void LightC()
        {
            GameSettings.SetLightStep(3, keep: false);
            _levels.FollowLamplit();
            GeneratedLevel level = _levels.Level;
            if (level != null && level.Mood != null) level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
        }

        static Vector3 OnFloor(GeneratedLevel level, LevelRoom room, Vector3 p)
        {
            for (float r = 0.3f; r <= 4f; r += 0.3f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return hit.position;
            return p;
        }

        Enemy Nearest(IEnumerable<Enemy> enemies) =>
            enemies.Where(e => e != null && e.isActiveAndEnabled && !e.IsDead).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).FirstOrDefault();

        string Values()
        {
            ToonLookValues v = ToonLookValues.Live;
            return string.Format(Inv, "the look's values: lit threshold {0} (softness {1}), lit tone {2}, shadow tone {3}, cast shadow edge {4}, ambient flat {5} x{6}, detail calm {7} mip levels, " +
                                      "edge light {8} x{9} from {10} (softness {11}, away {12}, gone under {19}), teal glow {13} on the kit and {14} on her body (from {15}, softness {16}), ink {17} px at 1080 rows in {18}, pushed back {20} m",
                                 v.LitThreshold, v.LitSoftness, v.LitLevel, v.ShadowTint, v.ShadowEdge, v.AmbientFlat, v.AmbientLevel, v.DetailCalm,
                                 v.RimColour, v.RimStrength, v.RimStart, v.RimSoftness, v.RimAway, v.KitTealGlow, v.BodyTealGlow, v.TealStart, v.TealSoftness, v.InkWidth, v.InkColour, v.RimUnder, v.InkDepthPush);
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator BeforeAndAfter_CloseUp_InThePracticeRoom_InTheNaveStillBesideASkeletonAndInAFight_Running_AndDashing()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); {W}x{H}; work order 115; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");
            _log.Add(Values());
            ToonLook look = Look;
            Assert.That(look, Is.Not.Null);
            look.Tick();
            var targets = new List<string>();
            for (int i = 0; i < look.TargetCount; i++)
                targets.Add(look.TargetRenderer(i).name + " (" + look.TargetRenderer(i).GetType().Name + ", " + string.Join("/", look.TodaysOf(i).Select(m => m.name + " on " + m.shader.name)) + (look.IsKit(i) ? ", kit" : "") + ")");
            _log.Add("her model's renderers it looks after: " + string.Join("; ", targets));

            // The practice room: she at the origin, the dummies where the scene has them.
            _player.Teleport(Vector3.zero);
            _player.AimAt(new Vector3(0f, 0f, -6f)); // facing the camera's side
            yield return new WaitForSecondsRealtime(1.5f);
            PoseFront(game, 4.2f);
            Shot(game, "01-close-front", "close up from the front, standing at rest in the practice room, the camera at her middle's height 4.2 m in front of her (NOT the game's view)", 900, 1080);
            PoseOn(game, Chest, 6f);
            Shot(game, "02-close-game-angle", "close up from the game's angle (its pitch), the camera 6 m from her chest (NOT the game's distance)", 900, 1080);
            Pose(game, _player.Position);
            Shot(game, "03-practice-game-distance", "the practice room at the game's own distance and angle, standing", 640, 480);

            // The nave of 085's style room at lamplit C.
            LabEntry entry = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            GeneratedLevel level = _levels.StartLab(entry, 0, seed);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LevelLayout l = level.Layout;
            LevelRoom nave = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = nave.Rect.Y * cs + _levels.Values.Fights.PointIn - 0.6;
            for (int k = 0; k < nave.Rect.Width && l.RoomAt(x, y) != nave; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            Vector3 spot = OnFloor(level, nave, level.ToWorld(new LevelPoint(x, y)));
            _player.Teleport(spot);
            yield return new WaitForSecondsRealtime(0.5f);
            LightC();
            yield return null;
            _player.AimAt(spot + new Vector3(0f, 0f, -6f));
            yield return new WaitForSecondsRealtime(1.2f);
            Pose(game, _player.Position);
            Shot(game, "04-nave-still", "the nave (room lab, ordinary, floor 1, seed KCT7B24U) at lamplit C, she standing 0.6 m short of its fight line, the game's distance", 640, 480);
            Pose(game, _player.Position, 8f);
            Shot(game, "04b-nave-still-close", "the same, the camera at the game's angle pulled in to 8 m (NOT the game's view)", 900, 900);

            // Beside one skeleton (today's look): the nave's nearest enemy put 1.6 m east of her, the moment after.
            var own = new List<Enemy>();
            if (level.EnemiesByRoom.TryGetValue(nave.Order, out List<Enemy> theirs)) own.AddRange(theirs.Where(e => e != null && !e.IsDead));
            _log.Add("the nave's enemies: " + string.Join(", ", own.GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key)));
            Enemy skeleton = own.FirstOrDefault(e => e.Look != null) ?? own.FirstOrDefault();
            if (skeleton != null)
            {
                Damageable d = skeleton.GetComponent<Damageable>();
                if (d != null) d.SetMaxHealth(1000000);
                NavMeshAgent agent = skeleton.GetComponent<NavMeshAgent>();
                if (agent != null && NavMesh.SamplePosition(_player.Position + Vector3.right * 1.6f, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
                yield return Frames(2);
                Pose(game, _player.Position);
                Shot(game, "05-beside-a-skeleton", "beside one " + skeleton.LogWord + " in today's look, put 1.6 m east of her in the nave, the game's distance", 640, 480);
                Pose(game, _player.Position + Vector3.right * 0.8f, 7f);
                Shot(game, "05b-beside-a-skeleton-close", "the same moment, the camera pulled in to 7 m (NOT the game's view)", 1100, 900, Chest + new Vector3(0.8f, 0f, 0f));
            }
            else _log.Add("05 NOT TAKEN: the nave had no enemy");

            // A crowded fight: the nave's enemies put round her 2.5 to 5 m out (made all but unkillable), she shooting at the nearest every 0.3 s.
            int put = 0;
            foreach (Enemy e in own.Take(8))
            {
                Damageable d = e.GetComponent<Damageable>();
                if (d != null) d.SetMaxHealth(1000000);
                float angle = put * Mathf.PI * 2f / Mathf.Max(1, Mathf.Min(8, own.Count)), r = 2.5f + 2.5f * (put % 2);
                Vector3 want = _player.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
                if (agent != null && NavMesh.SamplePosition(want, out NavMeshHit hit, 2f, NavMesh.AllAreas) && agent.Warp(hit.position)) put++;
            }
            float started = Time.realtimeSinceStartup, nextShot = 0f;
            while (Time.realtimeSinceStartup < started + 8f)
            {
                if (Time.time >= nextShot)
                {
                    Enemy e = Nearest(own);
                    if (e != null) _player.Shoot(e.transform.position);
                    nextShot = Time.time + 0.3f;
                }
                yield return null;
                if (Time.realtimeSinceStartup > started + 2.5f && _player.Action == ActionKind.BasicShot && _player.ActionPhase == ActionPhase.Recovery) break;
            }
            int close = own.Count(e => e != null && !e.IsDead && (e.transform.position - _player.Position).magnitude < 7f);
            string fight = string.Format(Inv, "a crowded fight in the nave at lamplit C: {0} of its enemies put round her 2.5 to 5 m out, she shooting at the nearest every 0.3 s, {1:0.0} s in, just after a shot went off, {2} alive within 7 m", put, Time.realtimeSinceStartup - started, close);
            Pose(game, _player.Position);
            Shot(game, "06-nave-fight", fight + ", the game's distance", 800, 600);
            Pose(game, _player.Position, 10f);
            Shot(game, "06b-nave-fight-close", "the same moment, the camera pulled in to 10 m (NOT the game's view)", 1400, 1000);

            // The frame's cost in this fight, the look on and off: the camera's draw and its read back timed, alternating.
            yield return Cost(game);

            // The whole game's look, once: the skeletons in it too (not left on), beside her in it alone.
            var enemyBefore = new List<(Renderer r, Material[] m)>();
            var enemyMade = new List<Material>();
            Shader toon = Shader.Find(ToonLook.ShaderName);
            foreach (Enemy e in own)
            {
                if (e == null || e.Look == null || e.Look.Renderers == null) continue;
                foreach (Renderer r in e.Look.Renderers)
                {
                    if (r == null || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                    Material[] was = r.sharedMaterials;
                    var now = new Material[was.Length];
                    for (int i = 0; i < was.Length; i++)
                    {
                        now[i] = new Material(toon) { name = "ToonLookPicture_" + (was[i] != null ? was[i].name : "none") };
                        if (was[i] != null && was[i].HasProperty("_BaseMap")) now[i].SetTexture("_BaseMap", was[i].GetTexture("_BaseMap"));
                        if (was[i] != null && was[i].HasProperty("_BaseColor")) now[i].SetColor("_BaseColor", was[i].GetColor("_BaseColor"));
                        enemyMade.Add(now[i]);
                    }
                    enemyBefore.Add((r, was));
                    r.sharedMaterials = now;
                }
            }
            Pose(game, _player.Position);
            Shot(game, "07-nave-fight-all-toon", "the same fight with the skeletons in the toon look too (pair: LEFT her today and the skeletons toon, RIGHT everyone toon; the skeletons wear the shader's start values, nothing tuned for them; not left on), the game's distance", 800, 600);
            Pose(game, _player.Position, 10f);
            Shot(game, "07b-nave-fight-all-toon-close", "the same, the camera pulled in to 10 m (NOT the game's view)", 1400, 1000);
            foreach ((Renderer r, Material[] m) in enemyBefore) if (r != null) r.sharedMaterials = m;
            foreach (Material m in enemyMade) Object.Destroy(m);

            // Running and dashing across the nave (the ink line in motion), the fight's enemies still about.
            _player.Teleport(OnFloor(level, nave, level.ToWorld(new LevelPoint((nave.Rect.X + 1.5) * cs, (nave.Rect.Y + nave.Rect.Height * 0.5) * cs))));
            yield return Frames(3);
            _player.Steer(Vector3.right);
            float runUntil = Time.time + 0.8f, until = Time.realtimeSinceStartup + 10f;
            while (Time.time < runUntil && Time.realtimeSinceStartup < until) yield return null;
            Pose(game, _player.Position);
            Shot(game, "08-running", string.Format(Inv, "running east across the nave at {0:0.0} m/s, the game's distance", _player.Speed), 640, 480);
            Pose(game, _player.Position, 7f);
            Shot(game, "08b-running-close", "the same moment, the camera pulled in to 7 m (NOT the game's view)", 900, 900);
            yield return Frames(4);
            Pose(game, _player.Position, 7f);
            Shot(game, "08c-running-close-later", "four frames later, the same (the line from one frame to the next)", 900, 900);
            _player.DashToward(_player.Position + Vector3.right * 5f);
            float dashAt = Time.time;
            until = Time.realtimeSinceStartup + 5f;
            while (Time.time < dashAt + 0.08f && Time.realtimeSinceStartup < until) yield return null;
            Pose(game, _player.Position);
            Shot(game, "09-dashing", string.Format(Inv, "in the dash, {0:0.00} s in (dashing: {1}), the game's distance", Time.time - dashAt, _player.IsDashing), 640, 480);
            Pose(game, _player.Position, 7f);
            Shot(game, "09b-dashing-close", "the same moment, the camera pulled in to 7 m (NOT the game's view)", 900, 900);
            _player.Stop();
        }

        IEnumerator Cost(Camera cam)
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
                    ToonLookValues.On = lookOn;
                    Look.Tick();
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
                ToonLookValues.On = true;
                Look.Tick();
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(one);
            }
            on.Sort();
            off.Sort();
            string line = string.Format(Inv, "COST the crowded fight's frame drawn at {0} by {1} and read back (the GPU's work in it), {2} times each, alternating: the look ON median {3:0.000} ms (lower quartile {4:0.000}), OFF median {5:0.000} ms (lower quartile {6:0.000}); the difference of the medians {7:+0.000;-0.000} ms",
                                        W, H, Rounds, on[on.Count / 2], on[on.Count / 4], off[off.Count / 2], off[off.Count / 4], on[on.Count / 2] - off[off.Count / 2]);
            _log.Add(line);
            Debug.Log(line);
            yield return null;
        }

        /// <summary>The project's own measure of a frame (work order 032's benchmark), its floor 5 fight, the look on and then off.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Cost_TheBenchmarksFloor5Fight_WithTheLookOnAndOff()
        {
            foreach (bool lookOn in new[] { true, false, true, false })
            {
                ToonLookValues.On = lookOn;
                string folder = Path.Combine(Path.GetTempPath(), "RoguelikeToonCost-" + Guid.NewGuid().ToString("N"));
                Benchmark b = Benchmark.Begin(new[]
                {
                    Benchmark.Argument, "-benchmarkLabel", lookOn ? "toon-on" : "toon-off", "-benchmarkScenarios", "3", "-benchmarkSeconds", "5",
                    "-benchmarkSettle", "1", "-benchmarkOut", folder,
                });
                b.QuitWhenDone = false;
                float until = Time.realtimeSinceStartup + 120f;
                while (!b.Done && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(b.Done, Is.True, "the benchmark finished");
                BenchmarkResults r = JsonUtility.FromJson<BenchmarkResults>(File.ReadAllText(b.ResultsPath));
                BenchmarkScenarioResult s = r.Scenarios[0];
                string counters = string.Join(", ", s.Counters.Where(c => c.Works).Select(c => string.Format(Inv, "{0} {1:0.###} {2}", c.Name, c.Mean, c.Unit)));
                bool wore = false;
                foreach (BenchmarkMaterial m in r.Materials) wore |= m.Shader == ToonLook.ShaderName;
                string line = string.Format(Inv, "BENCHMARK {0} look {1}: {2} frames, mean {3:0.000} ms, p95 {4:0.000}, worst {5:0.000}; {6} fighting; her toon materials seen by it: {7}; shaders looked up: {8}; counters: {9}",
                                            s.Scenario, lookOn ? "ON" : "OFF", s.Frames, s.FrameMs.Mean, s.FrameMs.P95, s.FrameMs.Max, s.EnemiesFighting, wore, string.Join(" | ", r.ShaderLookups), counters);
                _log.Add(line);
                Debug.Log(line);
                Object.Destroy(b.gameObject);
                yield return null;
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
        }
    }
}
