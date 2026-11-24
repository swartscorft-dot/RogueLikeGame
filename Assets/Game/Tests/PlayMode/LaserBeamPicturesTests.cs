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
    /// Work order 102, a look order's pictures: the Laser Sight's line before (the base commit's: from her middle along the
    /// cursor to where the shot would end, on an enemy's middle with a square there) and after (a thin red beam from each
    /// pistol's muzzle along its barrel). From the game's own camera angle, taken without a window the way work order 091 took
    /// its own (<see cref="LightRolloutPicturesTests"/>, and 100's <see cref="ShutDoorAndEntrancePicturesTests"/>). It changes
    /// nothing in the game. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_102 names a folder outside the project
    /// (otherwise it is ignored); ROGUELIKE_CAPTURE_102_TAG ("before", "after") goes in front of every file's name:
    ///
    ///   $env:ROGUELIKE_CAPTURE_102 = 'C:\Users\kriss\.claude-automation\runner\captures\102'
    ///   $env:ROGUELIKE_CAPTURE_102_TAG = 'before'
    ///   run-tests.ps1 PlayMode -Filter "LaserBeamPicturesTests"
    ///
    /// The practice room's picture: she at (0, 0, 0) with the part fitted, Dummy_A 5 m east of her, the cursor 0.6 m beside the
    /// dummy (inside the shot's band), the game's camera angle and distance over her.
    /// </summary>
    public class LaserBeamPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_102";
        const string TagVariable = "ROGUELIKE_CAPTURE_102_TAG";
        const int W = 1280, H = 720;
        /// <summary>Work order 091's run seed (226K2223), as 100 took its pictures in.</summary>
        const ulong RunSeed = 0x09100001UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        GeneratedLevels _levels;
        string _folder, _tag;

        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 102)");
            _tag = Environment.GetEnvironmentVariable(TagVariable);
            _tag = string.IsNullOrEmpty(_tag) ? "" : _tag + "-";
            Directory.CreateDirectory(_folder);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = _player.GetComponent<PlayerInputDriver>();

            _driver.InputSource = () => null;
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
            GameSettings.Unload();
            if (_player != null)
            {
                _player.GetComponent<PlayerHealth>().Untouchable = false;
                _player.Modifiers = null;
            }
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

        /// <summary>The Laser Sight fitted, as a run puts it.</summary>
        void Fit()
        {
            var m = new RunModifiers();
            m.Weapon.Add(WeaponRack.Find(new MetaValues(), WeaponAttachments.LaserSight));
            _player.Modifiers = m;
        }

        /// <summary>The camera where the game's rig would put it with her at <paramref name="focus"/>, at <paramref name="distance"/> m (the game's own when 0).</summary>
        void Pose(Camera cam, Vector3 focus, float distance = 0f)
        {
            FeelValues feel = Feel;
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            float d = distance > 0f ? distance : GameSettings.CameraDistance(feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        /// <summary>The camera at the game's angle, <paramref name="distance"/> m from a point in the air (her right muzzle, for a close look at the beams).</summary>
        void PoseOn(Camera cam, Vector3 point, float distance)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(point - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>The middle of her right beam as last drawn (or her right muzzle).</summary>
        Vector3 BeamMiddle()
        {
            LaserSight l = _player.Laser;
            return l.ShownOf(LaserSight.Right) ? l.FromOf(LaserSight.Right) + l.DirectionOf(LaserSight.Right) * (l.LengthOf(LaserSight.Right) * 0.5f) : _player.RightMuzzle.position;
        }

        /// <summary>Renders the camera as it stands now into a picture.</summary>
        void Save(Camera cam, string name, string what)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture before = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                // The beams drawn again for this camera as it is posed now and this picture's pixels (they face the camera and are
                // never drawn thinner than a few of its pixels); in the game they are drawn each frame for the game's camera.
                if (_player != null) _player.TickLaser();
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, _tag + name + ".png"), tex.EncodeToPNG());
                _log.Add(_tag + name + ".png: " + what);
            }
            catch (Exception e) { _log.Add(_tag + name + " FAILED: " + e.Message); }
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

        readonly List<GameObject> _putAway = new List<GameObject>();

        /// <summary>The practice room: she at the origin, Dummy_A 5 m east, every other target put away (brought back before a floor is built).</summary>
        Damageable PracticeSpot()
        {
            Damageable a = null;
            foreach (Damageable d in Damageable.All.ToList())
            {
                if (d.name == "Dummy_A") a = d;
                else
                {
                    d.gameObject.SetActive(false);
                    _putAway.Add(d.gameObject);
                }
            }
            Assert.That(a, Is.Not.Null, "Dummy_A");
            a.SetMaxHealth(1000000);
            a.transform.position = new Vector3(5f, a.transform.position.y, 0f);
            _player.Teleport(Vector3.zero);
            Physics.SyncTransforms();
            return a;
        }

        // ---------------- BEFORE: the base commit's line ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Before_TheLine_WithTheCursorBesideAnEnemy_EndsOnIt()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 102, tag '{_tag.TrimEnd('-')}'; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");
            Damageable a = PracticeSpot();
            Fit();
            Vector3 beside = a.transform.position + new Vector3(0f, 0f, 0.6f);
            _player.AimAt(new Vector3(beside.x, 0f, beside.z));
            yield return Frames(30);
            Pose(game, _player.Position);
            Save(game, "01-practice-cursor-beside-enemy", string.Format(Inv, "the practice room, she at the origin, Dummy_A 5 m east, the cursor 0.6 m north of its middle, standing; the part fitted; the line shown: {0}", _player.Laser.Shown));
            Pose(game, _player.Position, 9f);
            Save(game, "01b-practice-cursor-beside-enemy-close", "the same, the camera at the game's angle pulled in to 9 m (NOT the game's view)");
        }

        // ---------------- AFTER: the beams ----------------

        /// <summary>Both beams as last drawn: where each points, how long it is, what stopped it, the fog's share along it.</summary>
        string Beams()
        {
            LaserSight l = _player.Laser;
            var parts = new List<string>();
            for (int b = 0; b < LaserSight.Count; b++)
            {
                if (!l.ShownOf(b)) { parts.Add((b == 0 ? "right" : "left") + " not shown"); continue; }
                Vector3 d = l.DirectionOf(b);
                float down = Mathf.Asin(Mathf.Clamp(-d.y, -1f, 1f)) * Mathf.Rad2Deg;
                string by = l.BodyOf(b) != null ? "an enemy's body (" + l.BodyOf(b).name + ")" : l.ColliderOf(b) != null ? l.ColliderOf(b).name : l.LandedOf(b) ? "the floor" : "nothing (its full length)";
                parts.Add(string.Format(Inv, "{0}: from {1:F2}, {2:0} degrees {3} the level, {4:0.00} m long, stopped by {5}, fog share {6:0.00}, dot {7}",
                                        b == 0 ? "right" : "left", l.FromOf(b), Mathf.Abs(down), down >= 0f ? "under" : "over", l.LengthOf(b), by, l.FogOf(b), l.DotShownOf(b) ? "shown" : "off"));
            }
            return "beams: " + string.Join("; ", parts);
        }

        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            OnFloor(level, room, level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d)));

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

        Enemy Nearest(IEnumerable<Enemy> enemies) =>
            enemies.Where(e => e != null && e.isActiveAndEnabled && !e.IsDead).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).FirstOrDefault();

        [UnityTest, Timeout(1200000)]
        public IEnumerator After_TheBeams_Standing_Shooting_Running_ByAWallAndInTheMiddle_InAFight_CloseUp_AndWithTheDot()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 102, tag '{_tag.TrimEnd('-')}'; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}");

            // 02: the practice room, as BEFORE's: the cursor beside the dummy, and a shot at that point.
            Damageable a = PracticeSpot();
            Fit();
            LaserBeamValues v = _player.Laser.Values;
            _log.Add(string.Format(Inv, "the beam's values: colour {0}, width {1} m (end {2} m, never under {3} px), length {4} m, fade power {5}, glow {6} (fog adds {7}), wisps {8}/{9}, {10} motes at {11}, dot {12}",
                                   v.Colour, v.Width, v.EndWidth, v.LeastPixels, v.Length, v.FadePower, v.Glow, v.FogGlow, v.WispDepth, v.WispAir, v.Specks, v.SpeckGlow, v.DotOn ? "on" : "off"));
            Vector3 beside = a.transform.position + new Vector3(0f, 0f, 0.6f);
            _player.AimAt(new Vector3(beside.x, 0f, beside.z));
            yield return Frames(30);
            Pose(game, _player.Position);
            Save(game, "02a-practice-cursor-beside-enemy", "as BEFORE's 01: the practice room, Dummy_A 5 m east, the cursor 0.6 m north of its middle, standing (no shot): " + Beams());
            Pose(game, _player.Position, 9f);
            Save(game, "02b-practice-cursor-beside-enemy-close", "the same, the camera pulled in to 9 m (NOT the game's view): " + Beams());
            int fired = _player.BasicShotsFired;
            _player.Shoot(new Vector3(beside.x, 0f, beside.z));
            float until = Time.realtimeSinceStartup + 5f;
            while (_player.BasicShotsFired == fired && Time.realtimeSinceStartup < until) yield return null;
            float firedAt = Time.time;
            while (Time.time < firedAt + 0.06f && Time.realtimeSinceStartup < until) yield return null;
            Pose(game, _player.Position);
            Save(game, "02c-practice-shooting-cursor-beside-enemy", string.Format(Inv, "a shot at the cursor beside the dummy, {0:0.00} s after it went off (her arms aimed, its streak): ", Time.time - firedAt) + Beams());
            Pose(game, _player.Position, 9f);
            Save(game, "02d-practice-shooting-cursor-beside-enemy-close", "the same moment, the camera pulled in to 9 m (NOT the game's view): " + Beams());

            // 03: standing, her arms at rest.
            yield return new WaitForSecondsRealtime(1.5f);
            Pose(game, _player.Position, 6f);
            Save(game, "03-practice-standing-close", "standing, her arms at rest (no shot for 1.5 s), the camera pulled in to 6 m (NOT the game's view): " + Beams());

            // A floor of the run at lamplit C: the entrance, nobody in it. (The practice brute back first: a floor's enemies are made from it.)
            foreach (GameObject g in _putAway) if (g != null) g.SetActive(true);
            _putAway.Clear();
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 1), 1);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LightC();
            Fit();
            yield return null;
            LevelRoom entrance = level.Layout.Rooms[0];
            float cs = level.Layout.CellSize;
            Vector3 middle = OnFloor(level, entrance, level.Middle(entrance));
            _player.Teleport(middle);
            _player.AimAt(middle + Vector3.right * 6f);
            yield return new WaitForSecondsRealtime(1.5f);
            Pose(game, _player.Position);
            Save(game, "04a-entrance-middle", "the entrance's clear middle, standing: " + level.Layout.Rooms[0].Letter + entrance.Order + ", floor 1 of run " + LevelSeed.ToText(RunSeed) + "; " + Beams());
            Pose(game, _player.Position, 7f);
            Save(game, "04b-entrance-middle-close", "the same, the camera pulled in to 7 m (NOT the game's view): " + Beams());

            Vector3 byWall = OnFloor(level, entrance, level.ToWorld(new LevelPoint((entrance.Rect.X + 0.4) * cs + 0.9, (entrance.Rect.Y + entrance.Rect.Height * 0.5) * cs)));
            _player.Teleport(byWall);
            _player.AimAt(byWall + Vector3.left * 6f); // toward the wall
            yield return new WaitForSecondsRealtime(1.5f);
            Pose(game, _player.Position);
            Save(game, "05a-entrance-by-wall", "by the entrance's west wall, where its fog lies, standing, the cursor toward the wall: " + Beams());
            Pose(game, _player.Position, 7f);
            Save(game, "05b-entrance-by-wall-close", "the same, the camera pulled in to 7 m (NOT the game's view): " + Beams());
            int shotsBefore = _player.BasicShotsFired;
            _player.Shoot(byWall + Vector3.left * 6f);
            until = Time.realtimeSinceStartup + 5f;
            while (_player.BasicShotsFired == shotsBefore && Time.realtimeSinceStartup < until) yield return null;
            firedAt = Time.time;
            while (Time.time < firedAt + 0.25f && Time.realtimeSinceStartup < until) yield return null;
            Pose(game, _player.Position, 7f);
            Save(game, "05c-entrance-by-wall-aimed-close", "a shot at the wall, 0.25 s after it went off (her arms aimed, the kick settled), the camera pulled in to 7 m (NOT the game's view): " + Beams());

            // 06: close on her, the motes (three moments 0.4 s apart).
            for (int k = 0; k < 3; k++)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                PoseOn(game, BeamMiddle(), 3f);
                Save(game, "06-close-motes-" + (k + 1), "close on her right beam by the wall, the camera at the game's angle 3 m from its middle (NOT the game's view), moment " + (k + 1) + ": " + Beams());
            }

            // 07: the dot on (one picture), standing by the wall.
            _player.Laser.Values.DotOn = true;
            yield return Frames(3);
            Pose(game, _player.Position);
            Save(game, "07a-dot-on", "the dot's switch ON (off by default), standing by the wall: " + Beams());
            Pose(game, _player.Position, 7f);
            Save(game, "07b-dot-on-close", "the same, the camera pulled in to 7 m (NOT the game's view): " + Beams());
            _player.Laser.Values.DotOn = false;

            // 08: running across the entrance.
            _player.Teleport(OnFloor(level, entrance, level.ToWorld(new LevelPoint((entrance.Rect.X + 2.0) * cs, (entrance.Rect.Y + entrance.Rect.Height * 0.5) * cs))));
            yield return Frames(3);
            _player.Steer(Vector3.right);
            float runUntil = Time.time + 0.7f;
            until = Time.realtimeSinceStartup + 10f;
            while (Time.time < runUntil && Time.realtimeSinceStartup < until) yield return null;
            Pose(game, _player.Position);
            Save(game, "08a-running", string.Format(Inv, "running east across the entrance at {0:0.0} m/s: ", _player.Speed) + Beams());
            Pose(game, _player.Position, 7f);
            Save(game, "08b-running-close", "the same moment, the camera pulled in to 7 m (NOT the game's view): " + Beams());
            _player.Stop();

            // 09: a crowded fight: the combat room with the most enemies, she 2 m past its fight line; its enemies (made all but
            // unkillable) put round her 2.5 to 5 m out, then she shoots at the nearest every 0.3 s for a few seconds.
            RoomFightValues fights = new LevelValues().Fights;
            LevelRoom room = level.Layout.Rooms.OrderByDescending(r => r.EnemyCount).FirstOrDefault(r => r.Kind == RoomKind.Combat && !r.Elite && r.EnemyCount > 0);
            Assert.That(room, Is.Not.Null, "a combat room with enemies");
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, fights) + 2.0));
            yield return new WaitForSecondsRealtime(1f);
            var own = new List<Enemy>();
            if (level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> theirs)) own.AddRange(theirs.Where(e => e != null && !e.IsDead));
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
            string kinds = string.Join(", ", own.Where(e => e != null).GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key));
            int close = own.Count(e => e != null && !e.IsDead && (e.transform.position - _player.Position).magnitude < 7f);
            Pose(game, _player.Position);
            Save(game, "09a-crowded-fight", string.Format(Inv, "a fight: the room {0}{1} ({4}; {5} of them put round her 2.5 to 5 m out and made all but unkillable), she 2 m past its fight line shooting at the nearest enemy every 0.3 s, {2:0.0} s in, just after a shot went off, {3} enemies alive within 7 m of her: ", room.Letter, room.Order, Time.realtimeSinceStartup - started, close, kinds, put) + Beams());
            Pose(game, _player.Position, 9f);
            Save(game, "09b-crowded-fight-close", "the same moment, the camera pulled in to 9 m (NOT the game's view): " + Beams());
        }
    }
}
