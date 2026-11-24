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
    /// Work order 100, a look order's pictures: the block that shuts a combat room's door (dark stone with the door orange along
    /// its edges only) and the entrance room's light (warm, as the combat rooms' lamps), at lamplit C, from the game's own camera,
    /// taken without a window the way work order 091 took its own (<see cref="LightRolloutPicturesTests"/>): the same run seed
    /// (226K2223, 091's), the same floors, the same spots. It changes nothing in the game. Runs ONLY when the environment variable
    /// ROGUELIKE_CAPTURE_100 names a folder outside the project (otherwise it is ignored); ROGUELIKE_CAPTURE_100_TAG ("before",
    /// "after") goes in front of every file's name, so that the base commit's pictures and the order's lie side by side:
    ///
    ///   $env:ROGUELIKE_CAPTURE_100 = 'C:\Users\kriss\.claude-automation\runner\captures\100'
    ///   $env:ROGUELIKE_CAPTURE_100_TAG = 'after'
    ///   run-tests.ps1 PlayMode -Filter "ShutDoorAndEntrancePicturesTests"
    ///
    /// The pictures: the entrance at the start of floor 1 and of floor 2 (091's 05); the first passage (091's 04a); the shut door
    /// of floor 1's nave C2 (20 x 36 m, the longest combat room) with her 2.6 m in (091's 04b); the same door half risen (set by
    /// hand, 0.5); in the greybox view; seen from the passage side (the camera put where it would stand with her 3.5 m out in the
    /// passage); with her as far into the room as the game's camera still holds the door; and the whole room in one picture from
    /// the game's angle, pulled back (not the game's view: the camera follows her and never holds a 36 m room). With each, the
    /// mean grey of the block on screen against the picture's, and for the entrance the mean colour of the picture and the
    /// darkest walkable point of the entrance by 091's measure (<see cref="LightRolloutPicturesTests.LampLight"/>) on floors 1 to 5.
    /// </summary>
    public class ShutDoorAndEntrancePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_100";
        const string TagVariable = "ROGUELIKE_CAPTURE_100_TAG";
        const int W = 1280, H = 720;
        /// <summary>Work order 091's run seed (226K2223), the first from 0x9100001 that held every room its pictures wanted.</summary>
        const ulong RunSeed = 0x09100001UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder, _tag;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 100)");
            _tag = Environment.GetEnvironmentVariable(TagVariable);
            _tag = string.IsNullOrEmpty(_tag) ? "" : _tag + "-";
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
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (ThingsThatFly.Current != null) Object.Destroy(ThingsThatFly.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        void LightC()
        {
            GameSettings.SetLightStep(3, keep: false);
            _levels.FollowLamplit();
            GeneratedLevel level = _levels.Level;
            if (level != null && level.Mood != null) level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
        }

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

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Renders the camera as it stands now into a picture; <paramref name="measure"/> may read the pixels and add words.</summary>
        void Save(Camera cam, string name, string what, Func<Camera, Color[], string> measure = null)
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
                File.WriteAllBytes(Path.Combine(_folder, _tag + name + ".png"), tex.EncodeToPNG());
                string extra = "";
                if (measure != null)
                {
                    try { extra = "; " + measure(cam, tex.GetPixels()); }
                    catch (Exception e) { extra = "; MEASURE FAILED: " + e.Message; }
                }
                _log.Add(_tag + name + ".png: " + what + extra + "; " + Counts());
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

        string Counts()
        {
            LamplitView v = _levels.Lamplit;
            int enabled = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.enabled);
            return string.Format(Inv, "{0} lights on in the scene{1}", enabled,
                                 v != null && v.On != null ? string.Format(Inv, " ({0} lamps, level {1})", v.LampLightsOn, v.On.Name) : " (LAMPLIT OFF)");
        }

        /// <summary>The mean colour of the whole picture, and the share of its grey that is green-blue over red (the teal cast).</summary>
        static string MeanColour(Color[] px)
        {
            double r = 0, g = 0, b = 0;
            foreach (Color c in px) { r += c.r; g += c.g; b += c.b; }
            int n = Math.Max(1, px.Length);
            r /= n; g /= n; b /= n;
            return string.Format(Inv, "the picture's mean colour (0-255) R {0:0.0} G {1:0.0} B {2:0.0}, green-blue over red {3:+0.0;-0.0}", r * 255, g * 255, b * 255, ((g + b) * 0.5 - r) * 255);
        }

        /// <summary>
        /// The block's top face on screen (its four top corners projected, a rectangle inside them), its mean grey and its brightest
        /// tenth against the whole picture's mean grey and its brightest hundredth (0-255): the door is no longer the brightest
        /// surface when its brightest tenth is under the picture's brightest hundredth.
        /// </summary>
        static string BlockGrey(Camera cam, Color[] px, RoomDoor block)
        {
            if (block == null || block.BlockRenderer == null) return "no block";
            Bounds b = block.BlockRenderer.bounds;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 corner in new[] { new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.min.x, b.max.y, b.max.z), new Vector3(b.max.x, b.max.y, b.max.z),
                                               new Vector3(b.min.x, b.min.y + 0.05f, b.min.z), new Vector3(b.max.x, b.min.y + 0.05f, b.min.z) })
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            int x0 = Mathf.Clamp(Mathf.RoundToInt(minX * W), 0, W - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(maxX * W), 0, W - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(minY * H), 0, H - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(maxY * H), 0, H - 1);
            var inside = new List<float>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) inside.Add(Luma(px[y * W + x]));
            var all = px.Select(Luma).OrderBy(l => l).ToArray();
            if (inside.Count == 0) return "block off screen";
            inside.Sort();
            float mean = inside.Average(), top = inside[(int)(inside.Count * 0.9f)];
            return string.Format(Inv, "the block on screen ({0}x{1} px from x {2}, y {3} from the top): mean grey {4:0.0}, its brightest tenth from {5:0.0}; the picture's mean grey {6:0.0}, its brightest hundredth from {7:0.0}, brightest thousandth from {8:0.0}",
                                 x1 - x0 + 1, y1 - y0 + 1, x0, H - 1 - y1, mean * 255, top * 255, all.Average() * 255, all[(int)(all.Length * 0.99f)] * 255, all[(int)(all.Length * 0.999f)] * 255);
        }

        static bool InView(Camera c, Vector3 p, float margin = 0.03f)
        {
            Vector3 v = c.WorldToViewportPoint(p);
            return v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
        }

        static string Yes(bool b) => b ? "yes" : "NO";

        static string RoomName(LevelLayout layout, LevelRoom room)
        {
            string kind = room.Elite ? "elite room" : room.Kind == RoomKind.Combat ? "combat room" : room.Kind.ToString().ToLowerInvariant() + (room.Kind == RoomKind.Passage ? "" : " room");
            string template = room.Template != null && room.Template.Template != null ? "template " + room.Template.Template.Name : room.Kind == RoomKind.Passage ? "" : "drawn shape " + room.Shape.ToString().ToLowerInvariant();
            string theme = room.Theme != RoomTheme.None ? "theme " + RoomThemes.Word(room.Theme) : "no theme";
            return string.Format(Inv, "{0}{1} ({2}{3}{4}, {5})", room.Letter, room.Order, kind, template.Length > 0 ? ", " + template : "", ", " + theme,
                                 GreyboxRules.RoomWords(room, layout.CellSize));
        }

        string Where(GeneratedLevel level, LevelRoom room) =>
            string.Format(Inv, "{0}, floor {1}, floor seed {2} (run seed {3})", RoomName(level.Layout, room), level.Layout.Floor, level.Layout.SeedText, LevelSeed.ToText(RunSeed));

        IEnumerator Floor(int floor, Action<GeneratedLevel> done)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null, "floor " + floor);
            yield return null;
            LightC();
            yield return null;
            done(level);
        }

        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            OnFloor(level, room, level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d)));

        static Vector3 OnFloor(GeneratedLevel level, LevelRoom room, Vector3 p)
        {
            for (float r = 0.3f; r <= 4f; r += 0.3f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return hit.position;
            return p;
        }

        static Vector3 DoorMiddle(GeneratedLevel level, LevelDoor door)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            return level.ToWorld(new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5));
        }

        static bool SouthDoor(LevelLayout layout, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            return door != null && LevelGeometry.WallOf(room.Rect, door) == LevelGenerator.South;
        }

        // ---------------- The entrance's light, by 091's measure ----------------

        const float NoLampLight = 0.005f;

        /// <summary>The entrance's darkest walkable point by 091's measure (the least lamp light; among points with none, the farthest from every lamp), its mean, and its lamps.</summary>
        string EntranceMeasure(GeneratedLevel level)
        {
            LamplitView view = _levels.Lamplit;
            if (view == null || view.Plan == null || view.On == null) return "LAMPLIT OFF: no measure";
            LevelLayout layout = level.Layout;
            LevelRoom room = layout.Rooms.First(r => r.Kind == RoomKind.Entrance);
            var shown = new bool[layout.Rooms.Count];
            float darkest = float.PositiveInfinity, darkNearest = 0f;
            double sum = 0;
            int points = 0, unlit = 0;
            foreach (LevelPoint p in LampPlan.FloorPoints(layout, room))
            {
                Vector3 w = level.ToWorld(p);
                if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                if (new Vector2(hit.position.x - w.x, hit.position.z - w.z).magnitude > 0.3f || level.RoomAt(hit.position) != room) continue;
                LightRolloutPicturesTests.LampLight(level, view.Plan, view.On, room, hit.position, shown, out float light, out float nearest);
                points++;
                sum += light;
                if (light < NoLampLight) unlit++;
                bool darker = light < NoLampLight && darkest < NoLampLight ? nearest > darkNearest : light < darkest;
                if (darker) { darkest = light; darkNearest = nearest; }
            }
            var lamps = new List<string>();
            foreach (Light l in view.Root.GetComponentsInChildren<Light>(true))
            {
                string[] parts = l.name.Split('_');
                if (parts.Length < 4 || parts[0] != "Lamp" || parts[2] != room.Order.ToString(Inv)) continue;
                lamps.Add(string.Format(Inv, "{0} colour ({1:0.00}, {2:0.00}, {3:0.00}) range {4:0.0}", parts[1], l.color.r, l.color.g, l.color.b, l.range));
            }
            var kinds = view.Plan.Lamps.Where(l => l.Room == room.Order).GroupBy(l => l.Kind).Select(g => g.Count() + " " + g.Key.ToString().ToLowerInvariant());
            return string.Format(Inv, "the entrance {0}{1} by 091's measure: darkest walkable point {2:0.000} (nearest lamp {3:0.0} m), mean {4:0.000} over {5} points, {6:0} % with none; its lamps {7}; their lights: {8}",
                                 room.Letter, room.Order, darkest, darkNearest, sum / Math.Max(1, points), points, 100.0 * unlit / Math.Max(1, points), string.Join(", ", kinds), string.Join("; ", lamps));
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheShutDoor_NearFarHalfRisenGreyboxAndFromThePassage_TheEntranceOnTwoFloors_AndThePassage()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 100, tag '{_tag.TrimEnd('-')}'; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m (the default), pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}; light step {LightSteps.Names[GameSettings.LightStep]}; run seed {LevelSeed.ToText(RunSeed)}");
            GeneratedLevel level = null;

            // The entrance at the start of floors 1 and 2 (091's 05), and its measure on floors 1 to 5.
            for (int f = 1; f <= 5; f++)
            {
                yield return Floor(f, l => level = l);
                if (f <= 2)
                {
                    yield return new WaitForSecondsRealtime(1.5f);
                    Save(game, f == 1 ? "05-entrance-floor1" : "05-entrance-floor2", "the entrance room at the start of the floor, where the floor puts her, 1.5 s after it was built: " + Where(level, level.Layout.Rooms[0]),
                         (c, px) => MeanColour(px));
                }
                _log.Add(string.Format(Inv, "floor {0} (seed {1}): {2}", f, level.Layout.SeedText, EntranceMeasure(level)));
            }

            // The first passage (091's 04a): her in its middle, both doors' ends in view.
            yield return Floor(1, l => level = l);
            {
                LevelLayout layout = level.Layout;
                LevelRoom chosen = null;
                Vector3 chosenSpot = Vector3.zero;
                foreach (LevelRoom passage in layout.Rooms.Where(r => r.Kind == RoomKind.Passage))
                {
                    List<LevelDoor> ends = layout.Doors.Where(d => d.Joins(passage.Order)).ToList();
                    if (ends.Count < 2) continue;
                    Vector3 a = DoorMiddle(level, ends[0]), b = DoorMiddle(level, ends[1]);
                    Vector3 spot = OnFloor(level, passage, (a + b) * 0.5f);
                    Pose(game, spot);
                    if (chosen == null || (InView(game, a) && InView(game, b)))
                    {
                        chosen = passage;
                        chosenSpot = spot;
                    }
                    if (InView(game, a) && InView(game, b)) break;
                }
                _player.Teleport(chosenSpot);
                yield return new WaitForSecondsRealtime(1.5f);
                Save(game, "04a-passage", "her in the middle of the passage: " + Where(level, chosen), (c, px) => MeanColour(px));
            }

            // The shut door: the first ordinary combat room of floor 1 whose door in is on its south wall (091's 04b: the nave C2, 20 x 36 m).
            LevelRoom room = level.Layout.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Combat && !r.Elite && r.EnemyCount > 0 && SouthDoor(level.Layout, r));
            Assert.That(room, Is.Not.Null, "a combat room with its door in on its south wall on floor 1");
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, 5.5));
            GeneratedLevels.RoomDoors doors = null;
            float until = Time.time + 6f, real = Time.realtimeSinceStartup + 30f;
            while (Time.time < until && Time.realtimeSinceStartup < real)
            {
                doors = _levels.DoorsOf(room);
                if (doors != null && doors.Shut && !doors.Moving) break;
                yield return null;
            }
            Assert.That(doors != null && doors.Shut, "the room's doors shut");
            RoomDoor block = doors.Blocks.FirstOrDefault(b => b != null && b.Door == door);
            Assert.That(block, Is.Not.Null, "the door in's block");
            Vector3 near = InFrom(level, room, door, 2.6);
            _player.Teleport(near);
            yield return new WaitForSecondsRealtime(0.3f);
            Save(game, "04b-door-shut", string.Format(Inv, "at the room's door, she 2.6 m in from it, the door SHUT behind her: {0}, its block risen {1:0.00}: ", Yes(doors.Shut), block.Raised) + Where(level, room),
                 (c, px) => BlockGrey(c, px, block));

            // Half risen, set by hand (the blocks stand still once risen: nothing moves them back meanwhile).
            foreach (RoomDoor b in doors.Blocks) if (b != null) b.Show(0.5f);
            Save(game, "04f-door-half-risen", "the same, the blocks set by hand to half risen (0.5): " + Where(level, room), (c, px) => BlockGrey(c, px, block));
            foreach (RoomDoor b in doors.Blocks) if (b != null) b.Show(1f);

            // From the passage side: the camera where it would stand with her 3.5 m out in the passage (she stays inside, at the door).
            Vector3 outside = level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, -3.5, 0d));
            Pose(game, outside);
            Save(game, "04e-door-from-passage", "from the passage side: the camera put where the game's would stand with her 3.5 m out in the passage, at its distance (she herself stays inside, 2.6 m in): " + Where(level, room),
                 (c, px) => BlockGrey(c, px, block));

            // The whole room from the game's angle, pulled back until the door and the room's far wall are both in the picture (not the game's view).
            Vector3 doorAt = DoorMiddle(level, door);
            Vector3 middle = level.Middle(room);
            Vector3 farWall = new Vector3(doorAt.x, 0f, doorAt.z + (middle.z - doorAt.z) * 2f);
            float distance = 28f;
            for (; distance < 120f; distance += 2f)
            {
                Pose(game, middle, distance);
                if (InView(game, doorAt, 0.04f) && InView(game, farWall + Vector3.up * 3f, 0.04f)) break;
            }
            Pose(game, middle, distance);
            Save(game, "04d-door-whole-room", string.Format(Inv, "the whole room in one picture: the camera at the game's angle over the room's middle, pulled back to {0:0} m (the game's is 28; NOT the game's view), the door {1:0.0} m from the far wall: ", distance, Vector3.Distance(doorAt, farWall)) + Where(level, room),
                 (c, px) => BlockGrey(c, px, block));

            // As far into the room as the game's camera still holds the door (its foot in the lower 5 % of the picture's edge).
            float far = 2.6f;
            Bounds bb = block.BlockRenderer.bounds;
            Vector3 foot = new Vector3(bb.center.x, bb.min.y, bb.min.z);
            for (float depth = 3f; depth < 40f; depth += 0.5f)
            {
                Vector3 spot = InFrom(level, room, door, depth);
                if (level.RoomAt(spot) != room) break;
                Pose(game, spot);
                if (!InView(game, foot, 0.02f)) break;
                far = depth;
            }
            _player.Teleport(InFrom(level, room, door, far));
            yield return new WaitForSecondsRealtime(0.4f);
            Save(game, "04c-door-far-in-game", string.Format(Inv, "she {0:0.0} m in from the door, as far as the game's camera still holds the door's foot: ", far) + Where(level, room),
                 (c, px) => BlockGrey(c, px, block));

            // The greybox view, she 2.6 m in again.
            _player.Teleport(near);
            yield return new WaitForSecondsRealtime(0.3f);
            GameSettings.SetGreybox(true, keep: false);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            Save(game, "04g-door-greybox", "the same door in the greybox view (on: " + Yes(_levels.Greybox != null && _levels.Greybox.On) + "), she 2.6 m in: " + Where(level, room)
                 + string.Format(Inv, "; the block's materials: {0}", string.Join(", ", block.BlockRenderer.sharedMaterials.Select(m => m == null ? "none" : m.name + " " + m.color))),
                 (c, px) => BlockGrey(c, px, block));
            GameSettings.SetGreybox(false, keep: false);
            yield return null;
            yield return null;

            // Variants of the edge (work order 100: "if dark with a thin edge does not read from far, widen the band or strengthen the
            // glow"): with ROGUELIKE_CAPTURE_100_VARIANTS = "width:glow,width:glow" (metres, share), the floor built again with each,
            // the door shut again, and the near and the whole-room pictures taken again.
            string variants = Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_100_VARIANTS");
            foreach (string pair in string.IsNullOrEmpty(variants) ? new string[0] : variants.Split(','))
            {
                string[] wg = pair.Split(':');
                float width = float.Parse(wg[0], Inv), glow = float.Parse(wg[1], Inv);
                _levels.Values = new LevelValues();
                _levels.Values.Fights.DoorEdgeWidth = width;
                _levels.Values.Fights.DoorEdgeGlow = glow;
                level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 1), 1);
                yield return null;
                LightC();
                yield return null;
                LevelRoom vr = level.Layout.Rooms[room.Order];
                LevelDoor vd = LevelGeometry.EntryDoor(level.Layout, vr);
                _player.Teleport(InFrom(level, vr, vd, 5.5));
                GeneratedLevels.RoomDoors vdoors = null;
                float vuntil = Time.time + 6f, vreal = Time.realtimeSinceStartup + 30f;
                while (Time.time < vuntil && Time.realtimeSinceStartup < vreal)
                {
                    vdoors = _levels.DoorsOf(vr);
                    if (vdoors != null && vdoors.Shut && !vdoors.Moving) break;
                    yield return null;
                }
                RoomDoor vb = vdoors != null ? vdoors.Blocks.FirstOrDefault(b => b != null && b.Door == vd) : null;
                _player.Teleport(InFrom(level, vr, vd, 2.6));
                yield return new WaitForSecondsRealtime(0.3f);
                string name = string.Format(Inv, "w{0:0.00}-g{1:0.0}", width, glow);
                Save(game, "04b-door-shut-variant-" + name, string.Format(Inv, "variant: edge {0:0.00} m, glow {1:0.0}; as 04b: ", width, glow) + Where(level, vr), (c, px) => BlockGrey(c, px, vb));
                Pose(game, level.Middle(vr), distance);
                Save(game, "04d-door-whole-room-variant-" + name, string.Format(Inv, "variant: edge {0:0.00} m, glow {1:0.0}; as 04d (pulled back to {2:0} m, NOT the game's view): ", width, glow, distance) + Where(level, vr), (c, px) => BlockGrey(c, px, vb));
            }
        }
    }
}
