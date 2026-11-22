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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 085: pictures of the style room (the nave of the room lab, ordinary, floor 1, seed KCT7B24U) from the game's own
    /// camera at its default distance, in the four lightings (as before, lamplit A, B, C), at three moments: standing still at
    /// the fight's line; the frame a shot of hers goes off; mid-fight with a telegraph on the floor and an arrow in flight. Taken
    /// without a window. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_085 names a folder outside the project
    /// (otherwise it is ignored: no ordinary suite takes a picture or leaves a file):
    ///
    ///   $env:ROGUELIKE_CAPTURE_085 = 'C:\Users\kriss\.claude-automation\runner\captures\085\round1'
    ///   run-tests.ps1 PlayMode -Filter "LamplitPicturesTests"
    ///
    /// The still and mid-fight moments are one frame drawn four times, the lighting switched between (nothing moves); the shot
    /// is the same room built again with the same seed for each lighting and the same shot at the same enemy, drawn the frame
    /// after it went off. The interface is not drawn (IMGUI needs a window). The effects, the shot's weight and the things that
    /// fly are installed as Play installs them. A second test measures the cost: the main thread's frame and the camera's
    /// drawing to completion (the CPU's side and the GPU's, made to finish by a read back) in the nave mid-fight, as before and
    /// at level B, alternating.
    /// </summary>
    public class LamplitPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_085";
        const string FogVariable = "ROGUELIKE_CAPTURE_085_FOG"; // the fog at lamplit C, before and after (the follow-up)
        const int W = 1280, H = 720;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // The fog's round (the follow-up of 2026-11-22) has its own folder: only the test of the variable set runs.
            bool fog = TestContext.CurrentContext.Test.Name.Contains("Fog");
            string variable = fog ? FogVariable : FolderVariable;
            _folder = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + variable + " names a folder (work order 085)");
            Directory.CreateDirectory(_folder);
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
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetCameraDistanceStep(0, keep: false);
            GameSettings.SetLightStep(LightSteps.AsBefore, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures.txt"), _log);
            _log.Clear();
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            if (_fogWas != null) { LamplitValues c = LightSteps.Of(3); c.FogLevel = _fogWas[0]; c.FogClearNear = _fogWas[1]; c.FogClearFar = _fogWas[2]; _fogWas = null; } // C's fog values back, whatever happened
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static readonly string[] Names = { "asbefore", "lamplitA", "lamplitB", "lamplitC" };

        GeneratedLevel StyleRoom()
        {
            _levels.Values = new LevelValues();
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            GeneratedLevel level = _levels.StartLab(nave, 0, seed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static Vector3 AtDepth(GeneratedLevel level, double depth)
        {
            LevelLayout l = level.Layout;
            LevelRoom room = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = room.Rect.Y * cs + depth;
            for (int k = 0; k < room.Rect.Width && l.RoomAt(x, y) != room; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            return level.ToWorld(new LevelPoint(x, y));
        }

        /// <summary>The lighting switched now, in this frame: the floor follows, the mood and the view look at it at once.</summary>
        void LightNow(int step)
        {
            GameSettings.SetLightStep(step, keep: false);
            _levels.FollowLamplit();
            GeneratedLevel level = _levels.Level;
            if (level != null && level.Mood != null) level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
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
            catch (Exception e) { _log.Add(name + " failed: " + e.Message); }
            finally
            {
                cam.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        string Counts()
        {
            LamplitView v = _levels.Lamplit;
            int enabled = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.enabled);
            return string.Format(Inv, "{0} lights on in the scene{1}", enabled,
                                 v != null && v.On != null ? string.Format(Inv, " ({0} lamps, {1} flashes lit)", v.LampLightsOn, v.FlashesLit) : "");
        }

        Enemy Nearest(GeneratedLevel level) =>
            level.Enemies.Where(e => e != null && e.isActiveAndEnabled).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).FirstOrDefault();

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheNave_InTheFourLightings_AtThreeMoments()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 085's round; camera distance {GameSettings.CameraDistance(28f):0} m (the default)");
            GeneratedLevel level = StyleRoom();
            LevelRoom nave = level.Layout.Rooms[1];
            double line = _levels.Values.Fights.PointIn;
            _log.Add("the style room: " + RoomLab.Describe(_levels.LabEntry, RoomLab.Mixes[0], _levels.LabSeed) + ", " + GreyboxRules.RoomWords(nave, level.Layout.CellSize));
            _log.Add(LevelMap.Draw(level.Layout));

            // 1. Standing still at the fight's line (just short of it: the room asleep).
            _player.Teleport(AtDepth(level, line - 0.6));
            yield return new WaitForSecondsRealtime(0.5f);
            LightNow(2);
            _log.Add("lamps: " + _levels.Lamplit.Plan.Describe(nave, "the nave") + "; " + _levels.Lamplit.Plan.Describe(level.Layout.Rooms[0], "its passage"));
            for (int s = 0; s < 4; s++)
            {
                LightNow(s);
                Save(game, "1-still-" + Names[s], LightSteps.Names[s] + ": standing still at the fight's line (0.6 m short of it), the room asleep; " + Counts());
            }

            // 2. The frame a shot goes off: the same room built again, the same shot at the same enemy, each lighting.
            for (int s = 0; s < 4; s++)
            {
                LightNow(LightSteps.AsBefore);
                level = _levels.LabSameAgain();
                Assert.That(level, Is.Not.Null);
                _player.Teleport(AtDepth(level, line - 0.6));
                yield return new WaitForSecondsRealtime(0.4f);
                LightNow(s);
                yield return null;
                Enemy target = Nearest(level);
                bool fired = false;
                Action<PlayerReport> heard = r => { if (r.Kind == PlayerReportKind.ShotFired) fired = true; };
                _player.Reported += heard;
                _player.Shoot(target != null ? target.transform.position : _player.Position + Vector3.forward * 10f);
                float until = Time.realtimeSinceStartup + 3f;
                while (!fired && Time.realtimeSinceStartup < until) yield return null;
                _player.Reported -= heard;
                Save(game, "2-shot-" + Names[s], LightSteps.Names[s] + ": the frame her basic shot goes off" + (fired ? "" : " (IT DID NOT GO OFF)") + ", at the nearest enemy" +
                                                  (target != null ? string.Format(Inv, " ({0:0.0} m away)", Vector3.Distance(target.transform.position, _player.Position)) : "") + "; " + Counts());
            }

            // 3. Mid-fight: past the line, she shooting at the nearest enemy, until a telegraph is on the floor and an arrow flies.
            LightNow(LightSteps.AsBefore);
            level = _levels.LabSameAgain();
            _player.Teleport(AtDepth(level, line + 2.0));
            yield return null;
            LightNow(2);
            float started = Time.realtimeSinceStartup, end = started + 40f, nextShot = 0f;
            bool found = false;
            string what = "";
            while (Time.realtimeSinceStartup < end)
            {
                if (Time.time >= nextShot)
                {
                    Enemy e = Nearest(level);
                    if (e != null) _player.Shoot(e.transform.position);
                    nextShot = Time.time + 1.5f; // not too often: the room must stay full
                }
                yield return null;
                bool telegraph = level.Enemies.Any(e => e != null && e.IsTelegraphing);
                bool windup = level.Enemies.Any(e => e != null && !e.IsDead && e.State == EnemyState.Windup);
                bool arrow = ThingsThatFly.Current != null && ThingsThatFly.Current.Live > 0;
                if (arrow && Time.realtimeSinceStartup > started + 1f && (telegraph || (windup && Time.realtimeSinceStartup > started + 25f)))
                {
                    found = true;
                    what = telegraph ? "a telegraph on the floor and an arrow in flight" : "an enemy winding up (no line on the floor found in 25 s) and an arrow in flight";
                    break;
                }
            }
            string moment = found ? what : "NO MOMENT WITH BOTH A TELEGRAPH AND AN ARROW FOUND IN 40 s";
            foreach (int s in new[] { 2, 1, 3, 0 })
            {
                LightNow(s);
                Save(game, "3-fight-" + Names[s], LightSteps.Names[s] + ": mid-fight, " + moment + ", she 2 m past the line (one frame drawn four times; the flashes lit are B's, so as before shows none of that frame's muzzle light); " + Counts());
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheCost_InTheNaveMidFight_AsBeforeAndLevelB()
        {
            Camera game = Camera.main;
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, _levels.Values.Fights.PointIn + 2.0));
            yield return new WaitForSecondsRealtime(1f);
            const int frames = 300, renders = 60, rounds = 4;
            int fw = 1920, fh = 1080;
            var rt = new RenderTexture(fw, fh, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 1 };
            RenderTexture before = game.targetTexture;
            var main = new double[2];
            var draw = new double[2];
            var counts = new int[2];
            var lights = new int[2];
            try
            {
                for (int round = 0; round < rounds; round++)
                    foreach (int s in new[] { 0, 2 })
                    {
                        int k = s == 0 ? 0 : 1;
                        LightNow(s);
                        for (int i = 0; i < 30; i++) yield return null;
                        // The main thread: real seconds a frame over many frames (no camera draws itself in batch mode).
                        float t0 = Time.realtimeSinceStartup;
                        for (int i = 0; i < frames; i++)
                        {
                            Enemy e = i % 40 == 0 ? level.Enemies.FirstOrDefault(x => x != null && x.isActiveAndEnabled) : null;
                            if (e != null) _player.Shoot(e.transform.position);
                            yield return null;
                        }
                        main[k] += (Time.realtimeSinceStartup - t0) * 1000.0 / frames;
                        // The camera's drawing, finished on the GPU before the clock stops (a read back of the picture).
                        game.targetTexture = rt;
                        game.Render();
                        AsyncGPUReadback.Request(rt).WaitForCompletion();
                        var watch = Stopwatch.StartNew();
                        for (int i = 0; i < renders; i++)
                        {
                            game.Render();
                            AsyncGPUReadback.Request(rt, 0, 0, 4, 0, 4, 0, 1).WaitForCompletion();
                        }
                        draw[k] += watch.Elapsed.TotalMilliseconds / renders;
                        game.targetTexture = before;
                        counts[k]++;
                        lights[k] = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.enabled);
                    }
            }
            finally
            {
                game.targetTexture = before;
                rt.Release();
                Object.Destroy(rt);
            }
            string line = string.Format(Inv,
                "COST nave mid-fight, {0} rounds alternating, device {1}, {2}x{3}: as before main thread {4:0.000} ms a frame, camera drawn to completion {5:0.000} ms ({6} lights on); lamplit B main thread {7:0.000} ms, camera {8:0.000} ms ({9} lights on); B minus as before: main {10:+0.000;-0.000} ms, camera {11:+0.000;-0.000} ms",
                rounds, SystemInfo.graphicsDeviceType, fw, fh, main[0] / counts[0], draw[0] / counts[0], lights[0], main[1] / counts[1], draw[1] / counts[1], lights[1],
                main[1] / counts[1] - main[0] / counts[0], draw[1] / counts[1] - draw[0] / counts[0]);
            _log.Add(line);
            TestContext.WriteLine(line);
            UnityEngine.Debug.Log(line);
        }

        float[] _fogWas;

        /// <summary>
        /// The fog at lamplit C (the follow-up of 2026-11-22: "det blir som et rart lag man kommer inn i, det skal jo være litt
        /// gjennomsiktig"): in the nave, a patch of fog (the particle with the most others within 3 m), she far from it, walking up
        /// to it and standing in it; each moment one frame (the game held still) drawn twice: the fog as it was in 3772aa7 (C's fog
        /// values set to change nothing) and as it is now.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TheFog_AtLamplitC_FarFromAPatch_WalkingUpToIt_StandingInIt_BeforeAndAfter()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 085's fog round, lamplit C");
            GeneratedLevel level = StyleRoom();
            LevelRoom nave = level.Layout.Rooms[1];
            _player.Teleport(AtDepth(level, 3.0));
            yield return new WaitForSecondsRealtime(1.5f);
            LightNow(3);
            RoomMood.RoomParts parts = level.Mood.Of(nave.Order);
            var buffer = new ParticleSystem.Particle[level.Values.Mood.FogMostPerRoom];
            int n = parts.Fog.GetParticles(buffer);
            Assert.That(n, Is.GreaterThan(0), "fog in the nave");
            Vector3 patch = buffer[0].position;
            int most = -1;
            // A patch the camera sees: not by the south wall (the near lip and its black cap stand between the camera and it).
            float south = level.ToWorld(new LevelPoint(0, nave.Rect.Y * level.Layout.CellSize)).z + 8f;
            for (int i = 0; i < n; i++)
            {
                if (buffer[i].position.z < south) continue;
                int near = 0;
                for (int k = 0; k < n; k++) if ((buffer[k].position - buffer[i].position).sqrMagnitude < 9f) near++;
                if (near > most) { most = near; patch = buffer[i].position; }
            }
            Vector3 middle = level.ToWorld(nave.Middle(level.Layout.CellSize));
            Vector3 toMiddle = new Vector3(middle.x - patch.x, 0f, middle.z - patch.z).normalized;
            Vector3 Spot(float metres)
            {
                Vector3 p = new Vector3(patch.x, 0f, patch.z) + toMiddle * metres;
                for (float r = 0.5f; r <= 6f; r += 0.5f)
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, r, UnityEngine.AI.NavMesh.AllAreas) && level.RoomAt(hit.position) == nave) return new Vector3(hit.position.x, 0f, hit.position.z);
                return p;
            }
            _log.Add(string.Format(Inv, "the patch: {0} of the nave's {1} fog particles within 3 m of ({2:0.0}, {3:0.0}); fog particles {4:0.0} to {5:0.0} m across, {6:0.00} m over the floor, added (unlit), the fog's grey {7}",
                                   most, n, patch.x, patch.z, level.Values.Mood.FogSize * 0.7f, level.Values.Mood.FogSize * 1.3f, level.Values.Mood.FogHeight, level.Values.Mood.FogColour));
            LamplitValues c = LightSteps.Of(3);
            _fogWas = new[] { c.FogLevel, c.FogClearNear, c.FogClearFar };
            Time.timeScale = 0f; // held still: the two pictures of a moment are the same frame
            foreach ((string name, float metres, string what) in new[] { ("1-far", 9f, "she far from the patch (9 m)"), ("2-walking-up", 3.5f, "she walking up to it (3.5 m)"), ("3-in-it", 0f, "she standing in it") })
            {
                _player.Teleport(Spot(metres));
                yield return null;
                yield return null;
                foreach (bool after in new[] { false, true })
                {
                    c.FogLevel = after ? _fogWas[0] : 1f;
                    c.FogClearNear = after ? _fogWas[1] : 0f;
                    c.FogClearFar = after ? _fogWas[2] : 0f;
                    level.Mood.Tick();
                    Save(game, name + (after ? "-after" : "-before"), "lamplit C, " + what + ", " + (after ? string.Format(Inv, "the fog now: {0:0.##} of its grey, none within {1:0.#} m of her, all of its share from {2:0.#} m", c.FogLevel, c.FogClearNear, c.FogClearFar) : "the fog as it was in 3772aa7"));
                }
            }
            // What is what (a check, not for him): at the last moment, the before's fog, then the fog's renderers off, then the
            // mood's glow (the pools, the flames and the cold pool over each room's middle) off.
            c.FogLevel = 1f; c.FogClearNear = 0f; c.FogClearFar = 0f;
            level.Mood.Tick();
            var fogRenderers = level.Mood.Rooms.Where(r => r.Fog != null).Select(r => r.Fog.GetComponent<ParticleSystemRenderer>()).ToList();
            foreach (ParticleSystemRenderer r in fogRenderers) r.enabled = false;
            Save(game, "check-in-it-fog-off", "a check: as 3-in-it-before, the fog not drawn at all");
            foreach (ParticleSystemRenderer r in fogRenderers) r.enabled = true;
            var glows = level.Mood.Rooms.Where(r => r.Glow != null).Select(r => r.Glow).ToList();
            foreach (MeshRenderer r in glows) r.enabled = false;
            Save(game, "check-in-it-glow-off", "a check: as 3-in-it-before, the mood's pools, flames and the cold pool over each room's middle not drawn");
            foreach (MeshRenderer r in glows) r.enabled = true;
            c.FogLevel = 5f; // where the fog lies, made five times as bright
            level.Mood.Tick();
            Save(game, "check-in-it-fog-x5", "a check: as 3-in-it-before, the fog five times as bright, to show where its particles lie");
            c.FogLevel = 1f;
            level.Mood.Tick();
            n = parts.Fog.GetParticles(buffer);
            int close = 0;
            for (int i = 0; i < n; i++) if (new Vector2(buffer[i].position.x - _player.Position.x, buffer[i].position.z - _player.Position.z).magnitude < 4f) close++;
            _log.Add(string.Format(Inv, "a check: {0} fog particles in the nave, {1} within 4 m of her where she stands in it; the fog's material {2}, queue {3}", n, close,
                                   parts.Fog.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name, parts.Fog.GetComponent<ParticleSystemRenderer>().sharedMaterial.renderQueue));
            Time.timeScale = 1f;
        }
    }
}