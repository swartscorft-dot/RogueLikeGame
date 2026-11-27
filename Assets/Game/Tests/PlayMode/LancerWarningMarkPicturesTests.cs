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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 145, a look order's proof: two pictures of the lancer's warning mark, taken off screen in batch mode at the game's
    /// own camera, 1920 by 1080, the interface drawn into each by hand (<see cref="ImguiByHand"/>) as work order 134's pictures are:
    /// two lancers winding up their charges from off his screen, one 13 m below her and one 15 m to her side, each with its "!" at
    /// that edge of the screen. One on the practice room's light floor, one on a dark floor (floor 4 of the benchmark's run, its
    /// art and its lamps, the floor's own enemies taken away). With each, a close crop round each mark at three times its size (each
    /// pixel a block of 3 by 3) for looking at it at full size, and in the log where each mark stands and the floor's brightness
    /// beside it. Nothing here judges them: they are looked at.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_145 names a folder outside the project (otherwise ignored):
    ///   $env:ROGUELIKE_CAPTURE_145 = 'C:\Users\kriss\.claude-automation\runner\captures\145'
    ///   run-tests.ps1 PlayMode -Filter "LancerWarningMarkPicturesTests"
    /// </summary>
    public class LancerWarningMarkPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_145";
        const int W = 1920, H = 1080;
        const float Step = 1f / 60f;   // s of game time a frame while staged: a long frame (a picture written) is no jump
        const float Below = 13f, Side = 15f; // m from her, off screen, in the lancer's band
        const int Crop = 160, Zoom = 3;      // px round a mark, and its scale in the crop
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        EnemyBrute _template;
        GeneratedLevels _levels;
        LancerWarningMarks _marks;
        CastBar _cast;
        SkillBar _bar;
        EnemyBars _bars;
        CombatEffects _combat;
        string _folder;
        RenderTexture _rt;
        Texture2D _read;
        Transform _root;
        int _made;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 145)");
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _template = Object.FindAnyObjectByType<EnemyBrute>();
            _template.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            GameSettings.SetGreybox(false, keep: false);
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _cast = CastBar.Ensure();
            _bars = EnemyBars.Ensure();
            _combat = CombatEffects.Ensure();
            FiringWeight.Ensure();
            _marks = LancerWarningMarks.Ensure();
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "145-log.txt"), _log);
            _log.Clear();
            Time.captureDeltaTime = 0f;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_root != null) Object.Destroy(_root.gameObject);
            if (_levels != null)
            {
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _marks, _cast, _bars, _combat })
                if (m != null) Object.Destroy(m.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_read != null) Object.Destroy(_read);
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator GameSeconds(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float Room(Vector3 from, Vector3 dir, float max)
        {
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 0.5f, NavMesh.AllAreas)) return 0f;
            if (NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas))
                return Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(start.position.x, start.position.z));
            return Mathf.Min(max, Cover.ClearDistance(Flat(start.position), dir, max, new LancerValues().SightHeight));
        }

        static bool Fits(Vector3 p, Vector3 side) => Room(p, Vector3.back, Below + 1.5f) >= Below + 1.5f && Room(p, side, Side + 1.5f) >= Side + 1.5f;

        EnemyLancer Lancer(Vector3 at)
        {
            if (_root == null) _root = new GameObject("LancerMarkPictureSpawns").transform;
            Enemy e = EnemyFactory.Make(EnemyKind.Lancer, Flat(at), _root, _template, "LancerMarkPicture_" + (++_made));
            return (EnemyLancer)e;
        }

        static bool InWindup(EnemyLancer l) => l != null && l.State == EnemyState.Windup && l.Attack != null && l.Attack.Name == LancerValues.Charge;

        /// <summary>The game's camera and the interface into the texture, the pieces' frames worked out for it first.</summary>
        void Draw()
        {
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = _rt;
                if (_bar != null) _bar.Tick();
                _cast.Tick();
                _bars.Tick();
                _marks.Tick();
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(_rt);
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _rt;
                _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                _read.Apply();
                RenderTexture.active = was;
            }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>The picture, a crop round each mark at three times its size, and the log's line of each mark.</summary>
        void Save(string name, string what, EnemyLancer[] lancers)
        {
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), _read.EncodeToPNG());
            _log.Add(string.Format(Inv, "{0}.png: {1}; {2} marks up, {3} drawn; camera pitch {4:0.0}, field of view {5:0}", name, what, _marks.Shown, _marks.LastDrawn,
                Camera.main.transform.eulerAngles.x, Camera.main.fieldOfView));
            foreach (EnemyLancer l in lancers)
            {
                int i = _marks.MarkOf(l);
                string who = string.Format(Inv, "  {0} at ({1:0.0}, {2:0.0}), she at ({3:0.0}, {4:0.0}), in its wind-up {5}, charging {6}, on screen {7}",
                    l.name, l.transform.position.x, l.transform.position.z, _player.Position.x, _player.Position.z, InWindup(l), l.IsCharging,
                    RangedOpening.OnScreen(l.transform.position, 0f));
                if (i < 0) { _log.Add(who + ": NO MARK"); continue; }
                Vector2 at = _marks.At(i); // layout px of a view 1080 high: here the picture's own px
                int cx = Mathf.RoundToInt(at.x), cy = H - Mathf.RoundToInt(at.y); // the texture's rows run from the bottom
                Color floor = _read.GetPixel(Mathf.Clamp(cx - 30, 0, W - 1), cy), mark = _read.GetPixel(cx, cy + 8);
                _log.Add(who + string.Format(Inv, ": mark at ({0:0.0}, {1:0.0}) px; the floor 30 px left of it luma {2:0.00}, the mark's bar luma {3:0.00} ({4})",
                    at.x, at.y, Luma(floor), Luma(mark), mark));
                SaveCrop(Path.Combine(_folder, name + "-crop-" + (i + 1) + ".png"), cx, cy);
            }
        }

        void SaveCrop(string path, int cx, int cy)
        {
            int x0 = Mathf.Clamp(cx - Crop / 2, 0, W - Crop), y0 = Mathf.Clamp(cy - Crop / 2, 0, H - Crop);
            var crop = new Texture2D(Crop * Zoom, Crop * Zoom, TextureFormat.RGB24, false);
            try
            {
                var px = new Color32[Crop * Zoom * Crop * Zoom];
                Color32[] src = _read.GetPixels32();
                for (int y = 0; y < Crop * Zoom; y++)
                    for (int x = 0; x < Crop * Zoom; x++)
                        px[y * Crop * Zoom + x] = src[(y0 + y / Zoom) * W + x0 + x / Zoom];
                crop.SetPixels32(px);
                crop.Apply();
                File.WriteAllBytes(path, crop.EncodeToPNG());
            }
            finally { Object.Destroy(crop); }
        }

        /// <summary>Two lancers made off screen, below her and to her side; waits until both wind up and their lances are lowered, then the picture.</summary>
        IEnumerator Stage(Vector3 her, Vector3 side, string name, string what)
        {
            _player.Teleport(her);
            for (int i = 0; i < 20; i++) yield return null; // the camera settles on her
            var lancers = new[] { Lancer(her + Vector3.back * Below), Lancer(her + side * Side) };
            yield return Until(() => lancers.All(InWindup), 6f);
            yield return GameSeconds(0.6f); // the lance lowered and the glint grown, the wind-up not yet over (1.1 s)
            Draw();
            Save(name, what, lancers);
            foreach (EnemyLancer l in lancers) if (l != null) Object.Destroy(l.gameObject);
            yield return null;
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator Pictures_TwoLancersChargingFromOffScreen_BelowAndToTheSide_OnALightFloorAndADarkOne()
        {
            Time.captureDeltaTime = Step;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); work order 145; {W}x{H}; " +
                     string.Format(Inv, "mark {0} px high, {1} wide, outline {2}, inset {3}, bottom inset {4}, colour {5}", LancerWarningMark.Height, LancerWarningMark.Width,
                         LancerWarningMark.Outline, LancerWarningMark.Inset, LancerWarningMark.BottomInset, LancerWarningMark.Colour));

            // 1. The practice room's light floor.
            Vector3 stand = Vector3.zero, side = Vector3.right;
            bool found = false;
            for (int z = 0; z <= 12 && !found; z += 2)
                for (int x = -10; x <= 4 && !found; x += 2)
                    if (Fits(new Vector3(x, 0f, z), Vector3.right)) { stand = new Vector3(x, 0f, z); found = true; }
            Assert.That(found, Is.True, "room on the practice floor");
            yield return Stage(stand, side, "145-light-practice-room", "the practice room's floor, a lancer 13 m below her and one 15 m to her right");

            // 2. A dark floor: floor 4 of the benchmark's run, its own enemies taken away.
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(BenchmarkStage.RunSeed, 4), 4);
            Assert.That(level, Is.Not.Null);
            yield return null;
            yield return null;
            foreach (KeyValuePair<int, List<Enemy>> kv in level.EnemiesByRoom)
                foreach (Enemy e in kv.Value)
                    if (e != null) Object.Destroy(e.gameObject);
            yield return null;
            found = false;
            double cs = level.Layout.CellSize;
            string roomName = "";
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if (found || room.Kind != RoomKind.Combat) continue;
                Vector3 lo = level.ToWorld(new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs));
                Vector3 hi = level.ToWorld(new LevelPoint(room.Rect.Right * cs, room.Rect.Top * cs));
                for (float z = Mathf.Min(lo.z, hi.z) + 1f; z < Mathf.Max(lo.z, hi.z) && !found; z += 1f)
                    for (float x = Mathf.Min(lo.x, hi.x) + 1f; x < Mathf.Max(lo.x, hi.x) && !found; x += 1f)
                    {
                        var p = new Vector3(x, 0f, z);
                        if (level.RoomAt(p) != room || !NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                        foreach (Vector3 way in new[] { Vector3.right, Vector3.left })
                        {
                            if (level.RoomAt(p + Vector3.back * Below) != room || level.RoomAt(p + way * Side) != room || !Fits(p, way)) continue;
                            stand = Flat(hit.position);
                            side = way;
                            found = true;
                            roomName = "" + room.Letter + room.Order;
                            break;
                        }
                    }
            }
            Assume.That(found, Is.True, "a combat room of floor 4 with room below and beside her");
            yield return Stage(stand, side, "145-dark-floor-4", "floor 4 of the benchmark's run (room " + roomName + "), its art and lamps, a lancer 13 m below her and one 15 m to her " + (side.x > 0 ? "right" : "left"));
            Time.captureDeltaTime = 0f;
        }
    }
}
