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
    /// Work order 121's pictures: the same room at the same frozen moment of a fight, through the game's own camera (placed by
    /// its rig from the pause menu's angle, not posed by the test) at each of the four angles, Ours, Diablo, League, PoE; and a
    /// second moment, she by the room's north wall (the far walls that stand tall), at each. Floor 1 of work order 091's run,
    /// lamplit C, 1920 by 1080, taken without a window the way work order 107 took its own (<see cref="WarderReadsPicturesTests"/>).
    /// The camera's kick is off for them. The damage numbers, the bars and the rest of the interface are drawn by IMGUI and are
    /// not in a camera's picture. It changes nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_121
    /// names a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_121 = 'C:\Users\kriss\.claude-automation\runner\captures\121'
    ///   run-tests.ps1 PlayMode -Filter "CameraAnglesPicturesTests"
    /// </summary>
    public class CameraAnglesPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_121";
        const int W = 1920, H = 1080;
        /// <summary>Work order 091's run seed (226K2223), as 100, 102 and 107 took their pictures in.</summary>
        const ulong RunSeed = 0x09100001UL;
        const int Unkillable = 1000000;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly float HerHeight = new CaveValues().HerHeight;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        EnemyBrute _practiceEnemy;
        Transform _root;
        string _folder;
        int _made;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 121)");
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            CombatEffects.Ensure();
            FiringWeight.Ensure();
            ThingsThatFly.Ensure();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _levels.Values = new LevelValues();
            CameraAngles.On = true;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetCameraAngleStep(CameraAngles.Ours, keep: false);
            GameSettings.SetCameraKick(false, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        FeelValues Feel => _player.Feel ?? new FeelValues();

        void Save(Camera cam, string name, string what)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                // Her height in the picture, through the camera as it now stands (her feet, and 1.8 m over them).
                Vector3 feet = cam.WorldToScreenPoint(new Vector3(_player.Position.x, 0f, _player.Position.z));
                Vector3 head = cam.WorldToScreenPoint(new Vector3(_player.Position.x, HerHeight, _player.Position.z));
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                _log.Add(name + ".png: " + what + string.Format(Inv, "; her 1.8 m {0:0} px of {1} rows ({2:0.0} %), her feet at ({3:0}, {4:0})", head.y - feet.y, H, 100f * (head.y - feet.y) / H, feet.x, feet.y));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e.Message); }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static Vector3 OnFloor(GeneratedLevel level, LevelRoom room, Vector3 p)
        {
            for (float r = 0.3f; r <= 4f; r += 0.3f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return hit.position;
            return p;
        }

        void LightC()
        {
            GameSettings.SetLightStep(3, keep: false);
            _levels.FollowLamplit();
            GeneratedLevel level = _levels.Level;
            if (level != null && level.Mood != null) level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
        }

        T Make<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            if (_root == null) _root = new GameObject("CameraAnglesPictureSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, at, _root, _practiceEnemy, "Pictures121_" + kind + "_" + (++_made));
            e.enabled = true; // a brute is a copy of the practice one, switched off here
            e.HealthOverride = Unkillable;
            return (T)e;
        }

        string Header() => string.Format(Inv, "{0:yyyy-MM-dd HH:mm} batch {1} device {2}; {3}x{4}; work order 121; feel's camera pitch {5:0}, field of view {6:0}, distance {7:0} m; lamplit C; the camera's kick off",
                                         DateTime.Now, Application.isBatchMode, SystemInfo.graphicsDeviceType, W, H, Feel.CameraPitch, Feel.FieldOfView, Feel.CameraDistance);

        /// <summary>The four pictures of this moment: the game's own rig placing the camera at each angle, time stood still.</summary>
        IEnumerator FourAngles(Camera game, string number, string what)
        {
            for (int a = 0; a < CameraAngles.Count; a++)
            {
                GameSettings.SetCameraAngleStep(a, keep: false);
                yield return null; // the rig's LateUpdate places the camera (it runs with the time stood still)
                CameraAngle now = GameSettings.CameraNow(Feel);
                Assert.That(game.fieldOfView, Is.EqualTo(now.FieldOfView).Within(1e-4f), "the rig followed the angle");
                Save(game, number + (char)('a' + a) + "-" + CameraAngles.Names[a].ToLowerInvariant(),
                     string.Format(Inv, "{0} at {1}: pitch {2:0.#}, field of view {3:0.#}, distance {4:0.#} m", what, now.Name, now.Pitch, now.FieldOfView, now.Distance));
            }
            GameSettings.SetCameraAngleStep(CameraAngles.Ours, keep: false);
            yield return null;
        }

        /// <summary>Floor 1 of the run at lamplit C, she in the middle of its entrance. The practice brute switched off after: the template.</summary>
        IEnumerator Entrance(Action<GeneratedLevel, LevelRoom, Vector3> then)
        {
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 1), 1);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LightC();
            yield return null;
            _practiceEnemy.enabled = false;
            LevelRoom entrance = level.Layout.Rooms[0];
            Vector3 middle = OnFloor(level, entrance, level.Middle(entrance));
            _player.Teleport(middle);
            _player.AimAt(middle + Vector3.forward * 6f);
            yield return Frames(3);
            then(level, entrance, middle);
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheSameMomentOfAFight_AndBesideTheFarWall_AtTheFourAngles()
        {
            Camera game = Camera.main;
            Assert.That(game.GetComponent<CameraRig>(), Is.Not.Null, "the game's camera, with its rig");
            _log.Add(Header());
            GeneratedLevel level = null;
            LevelRoom entrance = null;
            Vector3 her = Vector3.zero;
            yield return Entrance((l, r, m) => { level = l; entrance = r; her = m; });

            // 1. A fight in the entrance: a brute 3.5 m up the picture, a caster up and to the left, a guard to the right, six
            // swarmers round her lower half; all awake and all but unkillable. She shoots at the brute every 0.4 s for 2.5 s; then
            // time stands still and the four pictures are taken of that one moment.
            var brute = Make<EnemyBrute>(EnemyKind.Brute, OnFloor(level, entrance, her + new Vector3(0.3f, 0f, 3.5f)));
            Make<EnemyCaster>(EnemyKind.Caster, OnFloor(level, entrance, her + new Vector3(-4.5f, 0f, 5f)));
            Make<EnemyGuard>(EnemyKind.Guard, OnFloor(level, entrance, her + new Vector3(4f, 0f, 1.5f)));
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * (1.05f + 0.9f * i / 5f);
                Make<EnemySwarmer>(EnemyKind.Swarmer, OnFloor(level, entrance, her + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (2.5f + 1.5f * (i % 2))));
            }
            float started = Time.time, nextShot = 0f;
            while (Time.time < started + 2.5f)
            {
                if (Time.time >= nextShot)
                {
                    _player.Shoot(brute.transform.position);
                    nextShot = Time.time + 0.4f;
                }
                yield return null;
            }
            Time.timeScale = 0f;
            yield return null;
            _log.Add(string.Format(Inv, "the fight: {0} enemies round her at ({1:0.0}, {2:0.0}), the room {3} by {4} cells", Enemy.All.Count(e => e != null && !e.IsDead && e.name.StartsWith("Pictures121_", StringComparison.Ordinal)),
                                    _player.Position.x, _player.Position.z, entrance.Rect.Width, entrance.Rect.Height));
            yield return FourAngles(game, "01", "The same frozen moment of a fight in the entrance of floor 1 (run " + LevelSeed.ToText(RunSeed) + "), lamplit C");
            Time.timeScale = 1f;

            // 2. She by the room's north wall (a far wall: it stands tall), the fight left behind her.
            Object.Destroy(_root.gameObject);
            _root = null;
            yield return null;
            double cs = level.Layout.CellSize;
            Vector3 north = OnFloor(level, entrance, level.ToWorld(new LevelPoint(level.ToLevel(her).X, (entrance.Rect.Y + entrance.Rect.Height) * cs - 1.2)));
            _player.Teleport(north);
            _player.AimAt(north + Vector3.back * 6f);
            yield return Frames(5);
            Time.timeScale = 0f;
            yield return null;
            yield return FourAngles(game, "02", string.Format(Inv, "She 1.2 m from the entrance's north wall at ({0:0.0}, {1:0.0}), alone", north.x, north.z));
            Time.timeScale = 1f;
        }
    }
}
