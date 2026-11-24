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
    /// Work order 115's follow-up, pictures of her edge light (Kristian, 2026-11-24: "Ja, men det lyset er en refleksjon? Det er
    /// ikke \"på\" karakteren"): the fixed teal edge of commit 8a49e44 (its strength 0.4, no edge from the room's lights) against
    /// the edge the room's own lights give her (the fixed one at 0), in the same frame. From the game's camera and close: beside
    /// a teal brazier or lantern, beside a warm sconce or candles, in the dark middle of a room, between a warm and a teal lamp,
    /// and in the practice room. Floors of run 0x09100001 at lamplit C, the lamps found in the floor's own lamp plan. Runs ONLY
    /// when the environment variable ROGUELIKE_CAPTURE_115_EDGE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE_115_EDGE = 'C:\Users\kriss\.claude-automation\runner\captures\115\edge'
    ///   run-tests.ps1 PlayMode -Filter "ToonLookEdgePicturesTests"
    /// </summary>
    public class ToonLookEdgePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_115_EDGE";
        const int W = 1920, H = 1080;
        const ulong RunSeed = 0x09100001UL;
        /// <summary>The fixed edge's strength in commit 8a49e44, for the LEFT of each pair.</summary>
        const float OldRim = 0.4f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        float _rim, _edgeStrength;
        bool _edgeOn;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 115)");
            Directory.CreateDirectory(_folder);
            ToonLookValues.On = true;
            ToonLookValues v = ToonLookValues.Live;
            _rim = v.RimStrength;
            _edgeStrength = v.LampEdgeStrength;
            _edgeOn = v.LampEdgeOn;
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
            ToonLookValues v = ToonLookValues.Live;
            v.RimStrength = _rim;
            v.LampEdgeStrength = _edgeStrength;
            v.LampEdgeOn = _edgeOn;
            ToonLookValues.On = true;
            GameSettings.Unload();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
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

        Vector3 Chest
        {
            get
            {
                ToonLook look = Look;
                for (int i = 0; look != null && i < look.TargetCount; i++)
                    if (look.TargetRenderer(i) is SkinnedMeshRenderer s) return s.bounds.center;
                return _player.Position;
            }
        }

        void Pose(Camera cam, Vector3 focus)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            float d = GameSettings.CameraDistance(Feel.CameraDistance);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * d, rotation);
        }

        void PoseOn(Camera cam, Vector3 point, float distance)
        {
            cam.fieldOfView = Feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(Feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(point - rotation * Vector3.forward * distance, rotation);
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

        void Set(bool old)
        {
            ToonLookValues v = ToonLookValues.Live;
            v.RimStrength = old ? OldRim : _rim;
            v.LampEdgeOn = old ? false : _edgeOn;
            v.LampEdgeStrength = _edgeStrength;
            Look.Refresh();
        }

        /// <summary>The fixed teal edge of 8a49e44 (LEFT) and the edge from the room's lights (RIGHT), this frame; whole and cut round her.</summary>
        void Shot(Camera cam, string name, string what, int cw, int ch)
        {
            try
            {
                Set(old: true);
                Color32[] a = Draw(cam);
                Set(old: false);
                Color32[] b = Draw(cam);
                Write(a, W, H, name + "-fixed-teal.jpg", true);
                Write(b, W, H, name + "-room-light.jpg", true);
                Vector3 s = cam.WorldToViewportPoint(Chest);
                s.x *= W;
                s.y *= H;
                int x0 = Mathf.Clamp(Mathf.RoundToInt(s.x) - cw / 2, 0, W - cw), y0 = Mathf.Clamp(Mathf.RoundToInt(s.y) - ch / 2, 0, H - ch);
                var pair = new Color32[cw * 2 * ch];
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        pair[y * cw * 2 + x] = a[(y0 + y) * W + x0 + x];
                        pair[y * cw * 2 + cw + x] = b[(y0 + y) * W + x0 + x];
                    }
                Write(pair, cw * 2, ch, name + "-pair.png", false);
                _log.Add(string.Format(Inv, "{0}: {1} (pair: LEFT the fixed teal edge of 8a49e44, RIGHT the edge from the room's lights; {2} by {3} px each cut round her)", name, what, cw, ch));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e.Message); }
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
            for (float r = 0.3f; r <= 2f; r += 0.3f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return hit.position;
            return new Vector3(float.NaN, 0f, float.NaN);
        }

        static bool Teal(Rgb c) => c.G > c.R * 1.5f && c.B > c.R * 1.5f;
        static bool Warm(Rgb c) => c.R > c.B * 1.5f;

        static Vector3 LampAt(GeneratedLevel level, Lamp l) => level.ToWorld(l.At);

        /// <summary>A place 0.9 to 2 m from the lamp in its room, on the camera's side of it first (so the lamp stands behind her).</summary>
        static bool Beside(GeneratedLevel level, Lamp l, out Vector3 at)
        {
            LevelRoom room = level.Layout.Rooms.FirstOrDefault(r => r.Order == l.Room);
            at = Vector3.zero;
            if (room == null) return false;
            Vector3 p = LampAt(level, l);
            Vector3 toMiddle = level.Middle(room) - p;
            toMiddle.y = 0f;
            var ways = new List<Vector3> { Vector3.back, (Vector3.back + Vector3.left).normalized, (Vector3.back + Vector3.right).normalized, Vector3.left, Vector3.right };
            if (toMiddle.sqrMagnitude > 0.01f) ways.Add(toMiddle.normalized);
            foreach (Vector3 way in ways)
                foreach (float d in new[] { 1.3f, 1.0f, 1.8f })
                {
                    Vector3 q = OnFloor(level, room, p + way * d);
                    if (float.IsNaN(q.x)) continue;
                    float flat = new Vector2(q.x - p.x, q.z - p.z).magnitude;
                    if (flat < 0.8f || flat > 2.2f) continue;
                    at = q;
                    return true;
                }
            return false;
        }

        string Near(GeneratedLevel level, LampPlan plan, Vector3 at)
        {
            LampValues v = LamplitView.Lamps;
            var near = plan.Lamps.Where(l => l.CarriedBy == null).Select(l => (l, d: new Vector2(LampAt(level, l).x - at.x, LampAt(level, l).z - at.z).magnitude))
                             .OrderBy(t => t.d).Take(3).Select(t => string.Format(Inv, "{0} {1} at {2:0.0} m", t.l.Kind, v.ColourOf(t.l), t.d));
            return "nearest lamps: " + string.Join(", ", near);
        }

        IEnumerator PutHer(Vector3 at, Vector3 facing)
        {
            _player.Teleport(at);
            yield return new WaitForSecondsRealtime(0.5f);
            LightC();
            _player.AimAt(at + facing * 6f);
            yield return new WaitForSecondsRealtime(1.0f);
            LightC();
            yield return null;
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator FixedTealAgainstTheRoomsLight_BesideTealAndWarmLamps_InTheDark_BetweenTwo_AndInThePracticeRoom()
        {
            Camera game = Camera.main;
            ToonLookValues v = ToonLookValues.Live;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 115's follow-up; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m, pitch {Feel.CameraPitch:0}");
            _log.Add(string.Format(Inv, "the edge from the room's lights: on {0}, strength {1} of the light reaching her (times N·L), from {2} in 1 - N·V, softness {3}; the fixed teal edge {4} (LEFT shows it at {5}, as in 8a49e44)",
                                   v.LampEdgeOn, v.LampEdgeStrength, v.LampEdgeStart, v.LampEdgeSoftness, v.RimStrength, OldRim));

            // The practice room first.
            _player.Teleport(Vector3.zero);
            _player.AimAt(new Vector3(0f, 0f, -6f));
            yield return new WaitForSecondsRealtime(1.2f);
            Pose(game, _player.Position);
            Shot(game, "05-practice-game", "the practice room (its one key light, no lamps), the game's view", 480, 400);
            PoseOn(game, Chest, 6f);
            Shot(game, "05b-practice-close", "the same, the camera at the game's angle 6 m from her (NOT the game's view)", 900, 1000);

            bool teal = false, warm = false, dark = false, between = false;
            for (int floor = 1; floor <= 5 && !(teal && warm && dark && between); floor++)
            {
                _levels.Values = new LevelValues();
                GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                Assert.That(level, Is.Not.Null);
                yield return null;
                LightC();
                yield return null;
                LamplitView view = _levels.Lamplit;
                if (view == null || view.Plan == null) { _log.Add("floor " + floor + ": no lamp plan"); continue; }
                LampPlan plan = view.Plan;
                LampValues lv = LamplitView.Lamps;
                var lit = plan.Lamps.Where(l => l.CarriedBy == null).ToList();
                string where = string.Format(Inv, "floor {0} of run {1}", floor, LevelSeed.ToText(RunSeed));

                if (!teal)
                {
                    Lamp t = lit.FirstOrDefault(l => (l.Kind == LampKind.Brazier || l.Kind == LampKind.Lantern) && Teal(lv.ColourOf(l)) && Beside(level, l, out _));
                    if (t != null && Beside(level, t, out Vector3 at))
                    {
                        yield return PutHer(at, Vector3.back);
                        Pose(game, _player.Position);
                        Shot(game, "01-teal-lamp-game", "beside a teal " + t.Kind + " (" + where + "), the game's view; " + Near(level, plan, at), 480, 400);
                        PoseOn(game, Chest, 6f);
                        Shot(game, "01b-teal-lamp-close", "the same, 6 m (NOT the game's view)", 900, 1000);
                        teal = true;
                    }
                }
                if (!warm)
                {
                    Lamp w = lit.Where(l => Warm(lv.ColourOf(l)) && l.Kind != LampKind.Chest && Beside(level, l, out _)).OrderBy(l => l.Kind == LampKind.Sconce ? 0 : 1).FirstOrDefault();
                    if (w != null && Beside(level, w, out Vector3 at))
                    {
                        yield return PutHer(at, Vector3.back);
                        Pose(game, _player.Position);
                        Shot(game, "02-warm-lamp-game", "beside a warm " + w.Kind + (w.Warm ? " (the entrance's warm brazier)" : "") + " (" + where + "), the game's view; " + Near(level, plan, at), 480, 400);
                        PoseOn(game, Chest, 6f);
                        Shot(game, "02b-warm-lamp-close", "the same, 6 m (NOT the game's view)", 900, 1000);
                        warm = true;
                    }
                }
                if (!dark)
                {
                    // The walkable floor point of a combat room farthest from every lamp of the floor.
                    Vector3 best = Vector3.zero;
                    float far = -1f;
                    foreach (LevelRoom room in level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat))
                        foreach (LevelPoint p in LampPlan.FloorPoints(level.Layout, room))
                        {
                            Vector3 q = OnFloor(level, room, level.ToWorld(p));
                            if (float.IsNaN(q.x)) continue;
                            float nearest = lit.Count == 0 ? 99f : lit.Min(l => new Vector2(LampAt(level, l).x - q.x, LampAt(level, l).z - q.z).magnitude);
                            if (nearest > far) { far = nearest; best = q; }
                        }
                    if (far > 4f)
                    {
                        yield return PutHer(best, Vector3.back);
                        Pose(game, _player.Position);
                        Shot(game, "03-dark-middle-game", string.Format(Inv, "a combat room's place farthest from every lamp ({0:0.0} m to the nearest; {1}), the game's view; her own small light over her still lights her", far, where) + "; " + Near(level, plan, best), 480, 400);
                        PoseOn(game, Chest, 6f);
                        Shot(game, "03b-dark-middle-close", "the same, 6 m (NOT the game's view)", 900, 1000);
                        dark = true;
                    }
                }
                if (!between)
                {
                    bool done = false;
                    foreach (Lamp w in lit.Where(l => Warm(lv.ColourOf(l)) && l.Kind != LampKind.Chest))
                    {
                        foreach (Lamp t in lit.Where(l => Teal(lv.ColourOf(l)) && l.Room == w.Room))
                        {
                            Vector3 a = LampAt(level, w), b = LampAt(level, t);
                            float apart = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                            if (apart < 2.5f || apart > 8f) continue;
                            LevelRoom room = level.Layout.Rooms.First(r => r.Order == w.Room);
                            Vector3 q = OnFloor(level, room, (a + b) * 0.5f);
                            if (float.IsNaN(q.x)) continue;
                            yield return PutHer(q, Vector3.back);
                            Pose(game, _player.Position);
                            Shot(game, "04-between-warm-and-teal-game", string.Format(Inv, "between a warm {0} and a teal {1} {2:0.0} m apart ({3}), the game's view; {4}", w.Kind, t.Kind, apart, where, Near(level, plan, q)), 480, 400);
                            PoseOn(game, Chest, 6f);
                            Shot(game, "04b-between-warm-and-teal-close", "the same, 6 m (NOT the game's view)", 900, 1000);
                            done = true;
                            break;
                        }
                        if (done) break;
                    }
                    between = done;
                }
            }
            if (!teal) _log.Add("01 NOT TAKEN: no teal brazier or lantern with a free place beside it on floors 1 to 5");
            if (!warm) _log.Add("02 NOT TAKEN: no warm lamp with a free place beside it on floors 1 to 5");
            if (!dark) _log.Add("03 NOT TAKEN: no combat room place 4 m from every lamp on floors 1 to 5");
            if (!between) _log.Add("04 NOT TAKEN: no warm and teal lamp 2.5 to 8 m apart in one room on floors 1 to 5");
        }
    }
}
