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
    /// Work order 137, a look's pictures: a caller beside a caster and a warder (left to right: the caster, the caller, the warder, 2.4 m
    /// apart) in a combat room of floor 5 (the deepest floor, its darkest palette) at the game's own camera with her 5 m south of them,
    /// the floor's light as he plays it (the default light step), taken without a window as work order 100's pictures are
    /// (<see cref="ShutDoorAndEntrancePicturesTests"/>): one with the three standing, one with the caller in its call's tell (its big
    /// cast, the wind-up's ring round it, and one of the waves' discs beside it). With each, the mean colour of each body on screen. It
    /// changes nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_137 names a folder outside the project.
    /// </summary>
    public class CallerPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_137";
        const int W = 1280, H = 720;
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run
        const float Apart = 2.4f;   // m between the three
        const float HerSouth = 5f;  // m south of them the camera's focus (her) stands
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 137)");
            Directory.CreateDirectory(_folder);
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
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
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

        /// <summary>The mean colour of a body on screen: a box over its middle (its feet to its head's height), in the picture's pixels.</summary>
        static string BodyColour(Camera cam, Color[] px, Enemy e, string name)
        {
            Vector3 foot = cam.WorldToScreenPoint(e.transform.position);
            Vector3 head = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 1.7f);
            int x0 = Mathf.Clamp((int)foot.x - 12, 0, W - 1), x1 = Mathf.Clamp((int)foot.x + 12, 0, W - 1);
            int y0 = Mathf.Clamp((int)foot.y, 0, H - 1), y1 = Mathf.Clamp((int)head.y, 0, H - 1);
            double r = 0, g = 0, b = 0;
            int n = 0;
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                for (int x = x0; x <= x1; x++)
                {
                    Color c = px[y * W + x];
                    r += c.r; g += c.g; b += c.b;
                    n++;
                }
            return n == 0 ? name + " off screen" : string.Format(Inv, "{0} at ({1:0},{2:0}) mean ({3:0.000}, {4:0.000}, {5:0.000})", name, foot.x, foot.y, r / n, g / n, b / n);
        }

        void Save(Camera cam, string name, string what, IReadOnlyList<(Enemy e, string name)> bodies)
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
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Color[] px = tex.GetPixels();
                _log.Add(name + ".png: " + what + "; " + string.Join("; ", bodies.Select(b => BodyColour(cam, px, b.e, b.name))));
            }
            catch (Exception ex) { _log.Add(name + " FAILED: " + ex.Message); }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheCaller_BesideACasterAndAWarder_StandingAndInItsTell_AtTheGamesCamera_OnFloor5()
        {
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 5), 5);
            Assert.That(level, Is.Not.Null);
            yield return null;
            // The floor's first combat room, emptied of its own enemies (they would stand in the picture).
            LevelRoom room = RoomCallerRules.CombatRooms(level.Layout)[0];
            foreach (Enemy e in level.EnemiesByRoom[room.Order].ToList()) level.Leave(e);
            EnemyBrute template = Object.FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID).First(b => b.ComesBack && b.GetComponentInParent<GeneratedLevel>() == null);
            Vector3 mid = OnNavMesh(level.Middle(room));
            var made = new List<(Enemy e, string name)>();
            foreach ((EnemyKind kind, float dx, string name) in new[] { (EnemyKind.Caster, -Apart, "caster"), (EnemyKind.Caller, 0f, "caller"), (EnemyKind.Warder, Apart, "warder") })
            {
                Enemy e = EnemyFactory.Make(kind, OnNavMesh(mid + new Vector3(dx, 0f, 0f)), level.transform, template, "Picture_" + name);
                e.HealthOverride = _levels.Values.HealthOf(kind == EnemyKind.Caller ? EnemyKind.Caster : kind);
                level.Join(e, room, roomsOwn: true); // the floor's light on enemies, as in the game
                e.Values.AggroRange = 0f; // asleep for the picture with her near (a picture only: its own copy of its values)
                made.Add((e, name));
            }
            var caller = (EnemyCaller)made[1].e;
            caller.UseValues(_levels.Values.CallerRules);
            // She stands 5 m south of them (none of them wakes: asleep for the picture), the room's light round her as she plays it.
            Vector3 focus = OnNavMesh(mid + new Vector3(0f, 0f, -HerSouth));
            _player.Teleport(focus);
            yield return GameTime(1.5f);
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            Pose(cam, focus);
            Save(cam, "137-a-caster-caller-warder", "floor 5 " + RoomThemes.Word(room.Theme) + " room " + room.Letter + room.Order + ", the three standing, left to right caster, caller, warder", made);

            // The caller in its call's tell: a disc of the waves' beside it, its ring and its big cast.
            LineRenderer disc = null;
            caller.UseValues(new CallerValues { FirstCallAfter = 0f });
            caller.BeginCall = c =>
            {
                disc = _levels.PlaceArrival(c, OnNavMesh(mid + new Vector3(0f, 0f, -2.2f)), _levels.Values.WaveRules);
                return 1;
            };
            caller.Provoke();
            float until = Time.time + 3f;
            while (!caller.Calling && Time.time < until) yield return null;
            Assert.That(caller.Calling, Is.True, "its tell began");
            yield return GameTime(0.6f);
            if (disc != null) disc.widthMultiplier = 0.2f;
            Pose(cam, focus);
            Save(cam, "137-b-the-caller-in-its-tell", "the same, the caller 0.6 s into its 1 s tell (CastBig), the ring round it and a waves' disc 2.2 m south of it", made);
        }
    }
}
