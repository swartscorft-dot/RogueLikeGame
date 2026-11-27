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
    /// Work order 153, a look's pictures: floor 2's timed chest (of RoomFightsPlayTests' run) at the game's own camera, 1920 by 1080, the
    /// interface drawn into each by hand (<see cref="ImguiByHand"/>), the floor's light as he plays it: (a) the chest shut and dim during
    /// its room's own fight, she in the room's middle; (b) the room cleared, she beside it, the chest aglow with its prompt; (c) opened,
    /// she back in the middle, its wave's discs on the floor and its number over it and on the HUD; (d) the same 13 s after the opening,
    /// the number fallen. With each, the chest's mean colour on screen. It changes nothing in the game. Runs ONLY when the environment
    /// variable ROGUELIKE_CAPTURE_153 names a folder outside the project.
    /// </summary>
    public class TimedChestPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_153";
        const int W = 1920, H = 1080;
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        string _folder;
        RenderTexture _rt;
        Texture2D _read;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 153)");
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_folder, "between-runs-of-the-pictures.json"); // never the game's own save
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            CombatEffects.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.ShowDevBox = false; // the developer's box is not his screen
            _hud = RunHud.Ensure();
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "153-log.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_levels != null)
            {
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            if (_hud != null) Object.Destroy(_hud.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_read != null) Object.Destroy(_read);
            GeneratedLevels.BetweenRunsSavePath = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 30f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 30f;
            while (!condition() && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        /// <summary>The camera where the game's rig would put it with her at <paramref name="focus"/>.</summary>
        void Pose(Camera cam, Vector3 focus)
        {
            FeelValues feel = _player.Feel ?? new FeelValues();
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            float d = GameSettings.CameraDistance(feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        /// <summary>The chest's mean colour on screen: a box round its middle, in the picture's pixels.</summary>
        string ChestColour(Camera cam, Vector3 at)
        {
            Vector3 a = cam.WorldToScreenPoint(at + new Vector3(-0.45f, 0.05f, 0f)), b = cam.WorldToScreenPoint(at + new Vector3(0.45f, 0.7f, 0f));
            int x0 = Mathf.Clamp((int)Mathf.Min(a.x, b.x), 0, W - 1), x1 = Mathf.Clamp((int)Mathf.Max(a.x, b.x), 0, W - 1);
            int y0 = Mathf.Clamp((int)Mathf.Min(a.y, b.y), 0, H - 1), y1 = Mathf.Clamp((int)Mathf.Max(a.y, b.y), 0, H - 1);
            double r = 0, g = 0, bl = 0;
            int n = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Color c = _read.GetPixel(x, y);
                    r += c.r; g += c.g; bl += c.b;
                    n++;
                }
            return n == 0 ? "the chest off screen" : string.Format(Inv, "the chest at ({0:0},{1:0}) px, mean ({2:0.000}, {3:0.000}, {4:0.000}); {5}", (a.x + b.x) * 0.5f, H - (a.y + b.y) * 0.5f, r / n, g / n, bl / n,
                                                                    FloorColour(cam, at));
        }

        /// <summary>The floor's mean colour beside the chest, 1.6 to 2.6 m from its middle on its four sides (its pool reaches 1.8 m): what its shape reads against.</summary>
        string FloorColour(Camera cam, Vector3 at)
        {
            double r = 0, g = 0, bl = 0;
            int n = 0;
            foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                for (float d = 1.6f; d <= 2.61f; d += 0.25f)
                {
                    Vector3 s = cam.WorldToScreenPoint(at + dir * d + Vector3.up * 0.02f);
                    int x = (int)s.x, y = (int)s.y;
                    if (s.z <= 0f || x < 0 || y < 0 || x >= W || y >= H) continue;
                    Color c = _read.GetPixel(x, y);
                    r += c.r; g += c.g; bl += c.b;
                    n++;
                }
            return n == 0 ? "the floor beside it off screen" : string.Format(Inv, "the floor beside it mean ({0:0.000}, {1:0.000}, {2:0.000})", r / n, g / n, bl / n);
        }

        void Save(string name, string what, Vector3 focus)
        {
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                Pose(cam, focus);
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = _rt;
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(_rt);
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _rt;
                _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                _read.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), _read.EncodeToPNG());
                GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
                _log.Add(name + ".png: " + what + "; " + (t != null ? ChestColour(cam, t.Place) + string.Format(Inv, "; state {0}, Echoes {1} of {2}", t.State, _levels.TimedChestEchoes, t.Whole) : "no chest")
                         + (ImguiByHand.Available ? "" : "; the interface could not be drawn by hand"));
            }
            catch (Exception ex) { _log.Add(name + " FAILED: " + ex.Message); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheTimedChest_ShutInItsFight_OpenableWithItsPrompt_AndOpenedWithItsDiscsAndNumber_OnFloor2()
        {
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 2), 2);
            Assert.That(level, Is.Not.Null);
            yield return null;
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            Assert.That(t, Is.Not.Null, "floor 2 holds one");
            LevelRoom room = t.Room;
            Vector3 middle = OnNavMesh(level.Middle(room));
            string where = "floor 2 " + RoomThemes.Word(room.Theme) + " room " + room.Letter + room.Order + " (" + (room.LineUp != null ? room.LineUp.Name : "") + ")";

            // (a) Its room's own fight on, she in its middle: the chest shut and dim.
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            yield return Until(() => fight.On, 2f);
            _player.Teleport(middle);
            yield return GameTime(1.2f);
            Save("153-a-shut-in-its-rooms-fight", where + ", its own fight on, she in its middle, the chest shut and dim", _player.Position);

            // (b) The room cleared, the doors open, she beside the chest: it glows faintly, its prompt over it.
            for (int i = 0; i < 40 && (fight.On || level.EnemiesByRoom[room.Order].Any(e => e != null && !e.IsDead)); i++)
            {
                foreach (Enemy e in level.EnemiesByRoom[room.Order].ToList())
                    if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
                yield return GameTime(0.3f);
            }
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            yield return Until(() => (d == null || !d.Shut) && t.State == GeneratedLevels.TimedChestState.Openable, 4f);

            // (e) The room cleared, she in its middle: the chest in its pool of warm light, seen from there.
            _player.Teleport(middle);
            yield return GameTime(2.5f); // the corpses gone, the doors sunk
            Save("153-e-openable-seen-from-the-middle", where + ", the room cleared, she in its middle: the chest openable in its pool of light", _player.Position);

            Vector3 toward = new Vector3(middle.x - t.Place.x, 0f, middle.z - t.Place.z);
            _player.Teleport(OnNavMesh(t.Place + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * 1.4f));
            yield return GameTime(2.5f); // the corpses gone, the doors sunk
            Save("153-b-openable-with-its-prompt", where + ", the room cleared, she beside the chest: aglow, its prompt", _player.Position);

            // (c) Opened: she steps to the middle, its wave's discs on the floor, its number over it and on the HUD.
            Assert.That(_levels.UseTimedChest(), Is.True, "it opens");
            _player.Teleport(middle);
            yield return Until(() => t.Arriving.Count > 0, 3f);
            yield return GameTime(0.5f);
            Save("153-c-opened-its-discs-and-number", where + ", opened 0.5 s after its first discs, she in the room's middle, " + t.Arriving.Count + " discs, its wave " + t.Kinds, _player.Position);

            // (d) 13 s after the opening, some of its wave alive: the number fallen (whole until 9 s, 1 % a second).
            yield return Until(() => Time.time - t.OpenedAt >= 13f, 20f);
            List<Enemy> alive = t.Wave.Where(e => e != null && !e.IsDead && e.isActiveAndEnabled).ToList();
            foreach (Enemy e in alive.Skip(2)) e.GetComponent<Damageable>().TakeHit(100000000); // two left, so the chest and the HUD line show
            yield return GameTime(0.4f);
            Save("153-d-its-number-fallen", where + ", 13 s after the opening, two of its wave left, she in the room's middle", _player.Position);
        }
    }
}
