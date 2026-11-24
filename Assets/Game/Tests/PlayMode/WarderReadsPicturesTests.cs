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
    /// Work order 107's pictures (the warder that reads): a warder beside a caster, and a ward on a brute in a crowded fight, each
    /// BEFORE (the switch <see cref="WarderValues.Reads"/> off: the warder as it was, field for field) and AFTER (on), at lamplit C
    /// on floor 1 of work order 091's run, from the game's own camera angle and distance (and pulled in, said so), taken without
    /// a window the way work order 102 took its own (<see cref="LaserBeamPicturesTests"/>). It changes nothing in the game. The
    /// damage numbers and the first time's line are drawn by the interface (IMGUI) and are not in a camera's picture: they are
    /// proven by <see cref="WarderReadsTests"/>. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_107 names a folder
    /// outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_107 = 'C:\Users\kriss\.claude-automation\runner\captures\107'
    ///   run-tests.ps1 PlayMode -Filter "WarderReadsPicturesTests"
    /// </summary>
    public class WarderReadsPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_107";
        const int W = 1280, H = 720;
        /// <summary>Work order 091's run seed (226K2223), as 100 and 102 took their pictures in.</summary>
        const ulong RunSeed = 0x09100001UL;
        const int Unkillable = 1000000;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 107)");
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
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
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

        /// <summary>The camera where the game's rig would put it with her at <paramref name="focus"/>, at <paramref name="distance"/> m (the game's own when 0).</summary>
        void Pose(Camera cam, Vector3 focus, float distance = 0f)
        {
            FeelValues feel = Feel;
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            float d = distance > 0f ? distance : GameSettings.CameraDistance(feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        void Save(Camera cam, string name, string what)
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
                _log.Add(name + ".png: " + what);
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

        /// <summary>One enemy of a kind standing at <paramref name="at"/>; asleep (an aggro range of half a metre) unless <paramref name="awake"/>; all but unkillable.</summary>
        T Make<T>(EnemyKind kind, Vector3 at, bool awake) where T : Enemy
        {
            if (_root == null) _root = new GameObject("WarderReadsPictureSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, at, _root, _practiceEnemy, "Pictures107_" + kind + "_" + (++_made));
            e.enabled = true; // a brute is a copy of the practice one, switched off here
            e.HealthOverride = Unkillable;
            if (!awake) e.Values.AggroRange = 0.5f;
            return (T)e;
        }

        string Widths(EnemyWarder w)
        {
            var parts = new List<string>();
            foreach (LineRenderer l in w.GetComponentsInChildren<LineRenderer>(true).Where(l => l.enabled).OrderBy(l => l.name))
                parts.Add(string.Format(Inv, "{0} {1:0.000} m", l.name, l.widthMultiplier));
            return "lines: " + (parts.Count > 0 ? string.Join(", ", parts) : "none drawn");
        }

        string Header() => string.Format(Inv, "{0:yyyy-MM-dd HH:mm} batch {1} device {2}; {3}x{4}; work order 107; camera distance {5:0} m, pitch {6:0}, field of view {7:0}; lamplit C",
                                         DateTime.Now, Application.isBatchMode, SystemInfo.graphicsDeviceType, W, H, GameSettings.CameraDistance(Feel.CameraDistance), Feel.CameraPitch, Feel.FieldOfView);

        /// <summary>Floor 1 of the run at lamplit C, she in the middle of its entrance (nobody in it). The practice brute switched off after: the template.</summary>
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

        // ---------------- 1. A warder beside a caster ----------------

        [UnityTest, Timeout(1200000)]
        public IEnumerator AWarderBesideACaster_BeforeAndAfter()
        {
            Camera game = Camera.main;
            _log.Add(Header());
            GeneratedLevel level = null;
            LevelRoom entrance = null;
            Vector3 her = Vector3.zero;
            yield return Entrance((l, r, m) => { level = l; entrance = r; her = m; });
            // The two side by side 3.5 m up the picture from her, 2.4 m apart, the caster on the left; both asleep, facing her.
            var caster = Make<EnemyCaster>(EnemyKind.Caster, OnFloor(level, entrance, her + new Vector3(-1.2f, 0f, 3.5f)), false);
            var warder = Make<EnemyWarder>(EnemyKind.Warder, OnFloor(level, entrance, her + new Vector3(1.2f, 0f, 3.5f)), false);
            warder.Tuning.Reads = false;
            yield return new WaitForSecondsRealtime(1.5f);
            Vector3 pair = (caster.transform.position + warder.transform.position) * 0.5f;
            Pose(game, her);
            Save(game, "01a-before-warder-beside-caster", "BEFORE (the switch off: the warder as it was): floor 1 of run " + LevelSeed.ToText(RunSeed) + ", the entrance at lamplit C, she in its middle; 3.5 m up the picture a caster (left) and a warder (right), 2.4 m apart, asleep, facing her. " + Widths(warder));
            Pose(game, pair, 9f);
            Save(game, "01b-before-warder-beside-caster-close", "BEFORE, the same, the camera at the game's angle pulled in to 9 m on the pair (NOT the game's view)");

            warder.Tuning.Reads = true;
            yield return Frames(3);
            WarderBody body = warder.ReadBody;
            Assert.That(body != null && body.Shown, Is.True, "its body, made at once when the switch is turned on");
            Pose(game, her);
            Save(game, "02a-after-warder-beside-caster", string.Format(Inv, "AFTER (the switch on), the same spot and pair: the warder with its staff (top {0:0.0} m), its light, its halo, its glows green; the caster as it is. ", warder.Tuning.Reading.StaffTop) + Widths(warder));
            Pose(game, pair, 9f);
            Save(game, "02b-after-warder-beside-caster-close", "AFTER, the same, pulled in to 9 m on the pair (NOT the game's view)");
        }

        // ---------------- 2. A ward on a brute in a crowded fight ----------------

        [UnityTest, Timeout(1200000)]
        public IEnumerator AWardOnABrute_InACrowdedFight_BeforeAndAfter()
        {
            Camera game = Camera.main;
            _log.Add(Header());
            GeneratedLevel level = null;
            LevelRoom entrance = null;
            Vector3 her = Vector3.zero;
            yield return Entrance((l, r, m) => { level = l; entrance = r; her = m; });

            // The crowd, all awake and all but unkillable: a brute 3.5 m up the picture (nearest the warder: its first ward), the
            // warder 4 m behind it, a caster up and to the left, a guard to the right, six swarmers round her and below her.
            var brute = Make<EnemyBrute>(EnemyKind.Brute, OnFloor(level, entrance, her + new Vector3(0.3f, 0f, 3.5f)), true);
            var warder = Make<EnemyWarder>(EnemyKind.Warder, OnFloor(level, entrance, her + new Vector3(0.6f, 0f, 7.5f)), true);
            var others = new List<Enemy>
            {
                Make<EnemyCaster>(EnemyKind.Caster, OnFloor(level, entrance, her + new Vector3(-4.5f, 0f, 5f)), true),
                Make<EnemyGuard>(EnemyKind.Guard, OnFloor(level, entrance, her + new Vector3(4f, 0f, 1.5f)), true),
            };
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * (1.05f + 0.9f * i / 5f); // round her lower half, 2.5 to 4 m out
                others.Add(Make<EnemySwarmer>(EnemyKind.Swarmer, OnFloor(level, entrance, her + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (2.5f + 1.5f * (i % 2))), true));
            }
            warder.Tuning.Reads = false;

            // The fight: she shoots at the brute every 0.4 s until the warder has two wards on (the brute's first).
            float started = Time.realtimeSinceStartup, nextShot = 0f;
            while (Time.realtimeSinceStartup < started + 14f && warder.Keeper.OnCount < 2)
            {
                if (Time.time >= nextShot)
                {
                    _player.Shoot(brute.transform.position);
                    nextShot = Time.time + 0.4f;
                }
                yield return null;
            }
            string warded = string.Join(", ", new Enemy[] { brute }.Concat(others).Where(e => warder.Wards(e)).Select(e => e.LogWord));
            _log.Add(string.Format(Inv, "the fight: {0:0.0} s in, the warder wards {1} ({2} wards on); the brute warded: {3}", Time.realtimeSinceStartup - started, warded, warder.Keeper.OnCount, warder.Wards(brute)));

            yield return Shots(game, brute, warder, her, "03", "BEFORE (the switch off: the ward as it was)");
            warder.Tuning.Reads = true;
            yield return Frames(3);
            yield return Shots(game, brute, warder, her, "04", "AFTER (the switch on)");
        }

        /// <summary>The crowded fight's pictures at this moment: the game's view, pulled in, and at a hit on the warded brute.</summary>
        IEnumerator Shots(Camera game, EnemyBrute brute, EnemyWarder warder, Vector3 her, string number, string what)
        {
            string kinds = string.Join(", ", Enemy.All.Where(e => e != null && !e.IsDead && e.name.StartsWith("Pictures107_", StringComparison.Ordinal)).GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key));
            string state = string.Format(Inv, "{0} wards on, the brute warded {1}, the brute {2:0.0} m from her, the warder {3:0.0} m; the crowd: {4}; ",
                                         warder.Keeper.OnCount, warder.Wards(brute), Vector3.Distance(brute.transform.position, _player.Position), Vector3.Distance(warder.transform.position, _player.Position), kinds);
            Pose(game, _player.Position);
            Save(game, number + "a-crowded-fight-ward-on-brute", what + ": the entrance at lamplit C, a crowd round her, the warder's ward on the brute. " + state + Widths(warder));
            Vector3 mid = (brute.transform.position + warder.transform.position) * 0.5f;
            Pose(game, mid, 12f);
            Save(game, number + "b-crowded-fight-ward-on-brute-close", what + ", the same moment pulled in to 12 m between the brute and the warder (NOT the game's view)");
            Pose(game, (_player.Position + warder.transform.position) * 0.5f);
            Save(game, number + "d-crowded-fight-centred-between-her-and-the-warder", what + ", the same moment at the game's angle and distance but centred halfway between her and the warder, so the warder at its spot is in the picture (NOT the game's view: the game centres her)");
            // A hit on the warded brute (a basic shot's damage put on its body: her own shots kept landing on the swarmers round
            // it): the picture in the frame after, while its ring flashes.
            int off = 0;
            void Off(int n) => off += n;
            yield return null; // the frame the pictures above took long is over: the flash is timed in game time
            brute.Body.Warded += Off;
            brute.Body.TakeHit(Placeholders.BasicShotDamage);
            brute.Body.Warded -= Off;
            yield return null;
            Pose(game, _player.Position);
            Save(game, number + "c-crowded-fight-hit-on-warded-brute", what + string.Format(Inv, ": the frame after a basic shot's damage landed on the warded brute (its ring flashing; the ward took {0} off). ", off) + Widths(warder));
        }
    }
}
