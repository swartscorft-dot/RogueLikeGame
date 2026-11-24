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
    /// Work order 112, a look order's pictures: the light check's three faults (work order 091's list) before and after, at
    /// lamplit C, from the game's own camera, taken without a window the way 091 and 100 took theirs
    /// (<see cref="LightRolloutPicturesTests"/>, <see cref="ShutDoorAndEntrancePicturesTests"/>): 091's run seed (226K2223), its
    /// floors and spots. "before" is the order's switch off (<see cref="LookListValues.On"/>: every plan and light as before,
    /// value for value, which LookListTests and LookListPlayTests prove), "after" on, in the same run. The floor's enemies are
    /// switched off in every picture, so that the two are of the same still moment. It changes nothing in the game. Runs ONLY when
    /// ROGUELIKE_CAPTURE_112 names a folder outside the project (otherwise it is ignored):
    ///
    ///   $env:ROGUELIKE_CAPTURE_112 = 'C:\Users\kriss\.claude-automation\runner\captures\112'
    ///   run-tests.ps1 PlayMode -Filter "LookListPicturesTests"
    ///
    /// The pictures: floor 5's throne room at 091's spot and over its carved blocks; floor 1's nave C2 at its door (100's 04b:
    /// the piers and the door's posts); the ossuary of floor 3 (091's 06) and of floor 5; the first passage of floors 1 and 3
    /// (091's 04a); and the nave of 085's room lab (the style room), to show its light has not changed. With each, the numbers:
    /// the lids' and the floor's grey on screen, 091's measure in the ossuaries and the passages, the lamps of the room.
    /// ROGUELIKE_CAPTURE_112_VARIANTS ("top:share:base,lantern:share") takes the blocks and the passage again with other values.
    /// </summary>
    public class LookListPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_112";
        const int W = 1280, H = 720;
        const ulong RunSeed = 0x09100001UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 112)");
            Directory.CreateDirectory(_folder);
            LookListValues.Reset();
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
            LookListValues.Reset();
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

        void Pose(Camera cam, Vector3 focus)
        {
            FeelValues feel = Feel;
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * GameSettings.CameraDistance(feel.CameraDistance), rotation);
        }

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

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
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                string extra = "";
                if (measure != null)
                {
                    try { extra = "; " + measure(cam, tex.GetPixels()); }
                    catch (Exception e) { extra = "; MEASURE FAILED: " + e.Message; }
                }
                _log.Add(name + ".png: " + what + extra + "; " + Counts());
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

        string Counts()
        {
            LamplitView v = _levels.Lamplit;
            int enabled = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.enabled);
            string tops = v != null && v.Tops != null ? string.Format(Inv, "; lids: {0} block cells, {1} piers, {2} posts, {3} drawn", v.Tops.Blocks, v.Tops.Piers, v.Tops.Posts, v.Tops.Renderers.Count(r => r.enabled)) : "; no lids";
            return string.Format(Inv, "{0} lights on in the scene{1}{2}", enabled, v != null && v.On != null ? string.Format(Inv, " ({0} lamps, level {1})", v.LampLightsOn, v.On.Name) : " (LAMPLIT OFF)", tops);
        }

        static string RoomName(LevelLayout layout, LevelRoom room)
        {
            string kind = room.Elite ? "elite room" : room.Kind == RoomKind.Combat ? "combat room" : room.Kind.ToString().ToLowerInvariant() + (room.Kind == RoomKind.Passage ? "" : " room");
            string template = room.Template != null && room.Template.Template != null ? "template " + room.Template.Template.Name : room.Kind == RoomKind.Passage ? "" : "drawn shape " + room.Shape.ToString().ToLowerInvariant();
            string theme = room.Theme != RoomTheme.None ? "theme " + RoomThemes.Word(room.Theme) : "no theme";
            return string.Format(Inv, "{0}{1} ({2}{3}{4}, {5})", room.Letter, room.Order, kind, template.Length > 0 ? ", " + template : "", ", " + theme, GreyboxRules.RoomWords(room, layout.CellSize));
        }

        static string Where(GeneratedLevel level, LevelRoom room) =>
            string.Format(Inv, "{0}, floor {1}, floor seed {2} (run seed {3})", RoomName(level.Layout, room), level.Layout.Floor, level.Layout.SeedText, LevelSeed.ToText(RunSeed));

        IEnumerator Floor(int floor, Action<GeneratedLevel> done)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null, "floor " + floor);
            foreach (Enemy e in level.Enemies) if (e != null) e.gameObject.SetActive(false);
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

        static bool InView(Camera c, Vector3 p, float margin = 0.03f)
        {
            Vector3 v = c.WorldToViewportPoint(p);
            return v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
        }

        static bool SouthDoor(LevelLayout layout, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            return door != null && LevelGeometry.WallOf(room.Rect, door) == LevelGenerator.South;
        }

        /// <summary>The mean grey (0-255) of a world rectangle (x0..x1, z0..z1 at height y) on screen, its middle 60 %.</summary>
        static float RectGrey(Camera cam, Color[] px, float x0, float z0, float x1, float z1, float y)
        {
            float mx = (x1 - x0) * 0.2f, mz = (z1 - z0) * 0.2f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 corner in new[] { new Vector3(x0 + mx, y, z0 + mz), new Vector3(x1 - mx, y, z0 + mz), new Vector3(x0 + mx, y, z1 - mz), new Vector3(x1 - mx, y, z1 - mz) })
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            int ax = Mathf.Clamp(Mathf.RoundToInt(minX * W), 0, W - 1), bx = Mathf.Clamp(Mathf.RoundToInt(maxX * W), 0, W - 1);
            int ay = Mathf.Clamp(Mathf.RoundToInt(minY * H), 0, H - 1), by = Mathf.Clamp(Mathf.RoundToInt(maxY * H), 0, H - 1);
            double sum = 0;
            int n = 0;
            for (int yy = ay; yy <= by; yy++)
                for (int xx = ax; xx <= bx; xx++) { sum += Luma(px[yy * W + xx]); n++; }
            return n == 0 ? -1f : (float)(sum / n * 255);
        }

        /// <summary>Each carved block cell of the room in view: its top's grey on screen and the floor's grey in the open cell south of it (toward the camera).</summary>
        static string BlockGreys(Camera cam, Color[] px, GeneratedLevel level, LevelRoom room)
        {
            float cs = level.Layout.CellSize;
            var parts = new List<string>();
            foreach (GameObject cap in level.SolidCaps)
            {
                Bounds b = cap.GetComponent<Renderer>().bounds;
                LevelPoint p = level.ToLevel(b.center);
                int x = Mathf.FloorToInt((float)(p.X / cs)), y = Mathf.FloorToInt((float)(p.Y / cs));
                if (level.Layout.RoomRectAtCell(x, y) != room || !InView(cam, b.center)) continue;
                float top = RectGrey(cam, px, b.min.x, b.min.z, b.max.x, b.max.z, b.max.y + 0.03f);
                string floor = "no open cell south";
                if (room.IsOpen(x, y - 1))
                {
                    Vector3 lo = level.ToWorld(new LevelPoint(x * cs, (y - 1) * cs)), hi = level.ToWorld(new LevelPoint((x + 1) * cs, y * cs - 1.5));
                    floor = RectGrey(cam, px, lo.x, lo.z, hi.x, hi.z, 0f).ToString("0.0", Inv);
                }
                parts.Add(string.Format(Inv, "block cell {0},{1}: top grey {2:0.0}, the floor south of it {3}", x, y, top, floor));
            }
            return parts.Count == 0 ? "no carved block in view" : string.Join("; ", parts);
        }

        /// <summary>091's measure on the NavMesh's points of a room (<see cref="LightRolloutPicturesTests.LampLight"/>): its darkest walkable point, its mean, and its middle.</summary>
        string Measure(GeneratedLevel level, LevelRoom room)
        {
            LamplitView view = _levels.Lamplit;
            if (view == null || view.Plan == null || view.On == null) return "LAMPLIT OFF: no measure";
            var shown = new bool[level.Layout.Rooms.Count];
            float darkest = float.PositiveInfinity, darkNearest = 0f;
            double sum = 0;
            int points = 0;
            foreach (LevelPoint p in LampPlan.FloorPoints(level.Layout, room))
            {
                Vector3 w = level.ToWorld(p);
                if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                if (new Vector2(hit.position.x - w.x, hit.position.z - w.z).magnitude > 0.3f || level.RoomAt(hit.position) != room) continue;
                LightRolloutPicturesTests.LampLight(level, view.Plan, view.On, room, hit.position, shown, out float light, out float nearest);
                points++;
                sum += light;
                bool darker = light < 0.005f && darkest < 0.005f ? nearest > darkNearest : light < darkest;
                if (darker) { darkest = light; darkNearest = nearest; }
            }
            LightRolloutPicturesTests.LampLight(level, view.Plan, view.On, room, level.Middle(room), shown, out float middle, out float midNearest);
            var kinds = view.Plan.Lamps.Where(l => l.Room == room.Order).GroupBy(l => l.Kind).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key.ToString().ToLowerInvariant());
            return string.Format(Inv, "091's measure in {0}{1}: darkest walkable point {2:0.000} (nearest lamp {3:0.0} m), mean {4:0.000} over {5} points, the middle {6:0.000} (nearest lamp {7:0.0} m); its lamps {8} ({9} real lights)",
                                 room.Letter, room.Order, darkest, darkNearest, sum / Math.Max(1, points), points, middle, midNearest, string.Join(", ", kinds), view.Plan.LightsOf(room.Order));
        }

        static string MeanColour(Color[] px)
        {
            double r = 0, g = 0, b = 0;
            foreach (Color c in px) { r += c.r; g += c.g; b += c.b; }
            int n = Math.Max(1, px.Length);
            r /= n; g /= n; b /= n;
            return string.Format(Inv, "the picture's mean colour (0-255) R {0:0.0} G {1:0.0} B {2:0.0}, green-blue over red {3:+0.0;-0.0}", r * 255, g * 255, b * 255, ((g + b) * 0.5 - r) * 255);
        }

        // ---------------- The pictures ----------------

        IEnumerator ThroneRoom(Camera game, string tag)
        {
            GeneratedLevel level = null;
            yield return Floor(5, l => level = l);
            LevelRoom room = level.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            Vector3 spot = InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, new LevelValues().Fights) + 2.0);
            _player.Teleport(spot);
            yield return new WaitForSecondsRealtime(1.5f);
            Save(game, tag + "-03-throne-floor5", "she 2 m past the fight's line (091's 03 spot; the room still, its enemies off): " + Where(level, room),
                 (c, px) => BlockGreys(c, px, level, room) + "; " + MeanColour(px));
            // Over the carved blocks walled in on every side: the camera where the game's would stand with her between them.
            var caps = level.SolidCaps.Where(c => level.RoomAt(c.transform.position) == null && level.Layout.RoomRectAtCell(Mathf.FloorToInt((float)(level.ToLevel(c.transform.position).X / level.Layout.CellSize)), Mathf.FloorToInt((float)(level.ToLevel(c.transform.position).Y / level.Layout.CellSize))) == room).ToList();
            if (caps.Count > 0)
            {
                Vector3 centre = caps.Aggregate(Vector3.zero, (s, c) => s + c.transform.position) / caps.Count;
                Vector3 her = OnFloor(level, room, new Vector3(centre.x, 0f, centre.z - 2f));
                _player.Teleport(her);
                yield return new WaitForSecondsRealtime(1.5f);
                Save(game, tag + "-03b-throne-blocks", string.Format(Inv, "she between the room's {0} carved block cells walled in on every side, the game's camera on her: ", caps.Count) + Where(level, room),
                     (c, px) => BlockGreys(c, px, level, room));
            }
        }

        IEnumerator NavePiers(Camera game, string tag)
        {
            GeneratedLevel level = null;
            yield return Floor(1, l => level = l);
            LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite && r.EnemyCount > 0 && SouthDoor(level.Layout, r));
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, 2.6));
            yield return new WaitForSecondsRealtime(1.5f);
            Save(game, tag + "-04b-room-piers", "she 2.6 m in from the room's door (100's 04b spot; the door open, the room's enemies off): the piers and the door's posts: " + Where(level, room), (c, px) => MeanColour(px));
        }

        IEnumerator Ossuary(Camera game, string tag, int floor)
        {
            GeneratedLevel level = null;
            yield return Floor(floor, l => level = l);
            LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite && r.Theme == RoomTheme.Ossuary);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, new LevelValues().Fights) - 0.6));
            yield return new WaitForSecondsRealtime(1.5f);
            Save(game, tag + "-06-ossuary-floor" + floor, "at the fight's line (0.6 m short of it; 091's 06 spot), the room asleep: " + Where(level, room), (c, px) => MeanColour(px));
            _log.Add(tag + " floor " + floor + " ossuary: " + Measure(level, room));
            // Its middle in the picture: she 2 m south of it (the camera shows about 7 m behind her and 6 ahead).
            _player.Teleport(OnFloor(level, room, level.Middle(room) + new Vector3(0f, 0f, -2f)));
            yield return new WaitForSecondsRealtime(1.5f);
            int sets = level.Mood != null && level.Mood.Plan != null && level.Mood.Plan.Of(room.Order) != null ? level.Mood.Plan.Of(room.Order).Sources.Count(s => s.ByLookList) : 0;
            Save(game, tag + "-06b-ossuary-floor" + floor + "-middle", string.Format(Inv, "she 2 m south of the room's middle, the middle in view; the order's sets of candles: {0}: ", sets) + Where(level, room), (c, px) => MeanColour(px));
        }

        IEnumerator Passage(Camera game, string tag, int floor, string name = null)
        {
            GeneratedLevel level = null;
            yield return Floor(floor, l => level = l);
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
            Save(game, name ?? tag + "-04a-passage-floor" + floor, "her in the middle of the first passage whose two doors the camera holds (091's 04a spot): " + Where(level, chosen), (c, px) => MeanColour(px));
            _log.Add((name ?? tag) + " floor " + floor + " passage: " + Measure(level, chosen));
        }

        IEnumerator Nave(Camera game, string tag)
        {
            LabEntry entry = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartLab(entry, 0, seed);
            foreach (Enemy e in level.Enemies) if (e != null) e.gameObject.SetActive(false);
            LevelLayout l = level.Layout;
            LevelRoom nave = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = nave.Rect.Y * cs + _levels.Values.Fights.PointIn - 0.6;
            for (int k = 0; k < nave.Rect.Width && l.RoomAt(x, y) != nave; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            _player.Teleport(level.ToWorld(new LevelPoint(x, y)));
            yield return null;
            LightC();
            yield return new WaitForSecondsRealtime(1.5f);
            Save(game, tag + "-085-nave", "the style room of 085 (the nave of the room lab, ordinary, floor 1, seed KCT7B24U) at 085's spot (0.6 m short of the fight's line), its enemies off: " + RoomName(l, nave), (c, px) => MeanColour(px));
            _log.Add(tag + " the nave: " + Measure(level, nave));
        }

        [UnityTest, Timeout(2400000)]
        public IEnumerator BeforeAndAfter_TheThroneRoomsBlocks_ARoomsPiers_TheOssuary_TheFirstPassages_AndTheNave()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 112; camera distance {GameSettings.CameraDistance(Feel.CameraDistance):0} m (the default), pitch {Feel.CameraPitch:0}, field of view {Feel.FieldOfView:0}; light step {LightSteps.Names[GameSettings.LightStep]}; run seed {LevelSeed.ToText(RunSeed)}; before = LookListValues.On false, after = true");
            string only = Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_112_ONLY"); // "before" or "after": one side only
            foreach (string tag in new[] { "before", "after" })
            {
                if (!string.IsNullOrEmpty(only) && only != tag) continue;
                LookListValues.Reset();
                LookListValues.On = tag == "after";
                yield return ThroneRoom(game, tag);
                yield return NavePiers(game, tag);
                yield return Ossuary(game, tag, 3);
                yield return Ossuary(game, tag, 5);
                yield return Passage(game, tag, 1);
                yield return Passage(game, tag, 3);
            }

            // Variants (after only): "top:share:base" (the lids' share of the floor's light and their base) or "lantern:share" (a passage's lanterns).
            string variants = Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_112_VARIANTS");
            foreach (string v in string.IsNullOrEmpty(variants) ? new string[0] : variants.Split(','))
            {
                string[] p = v.Split(':');
                LookListValues.Reset();
                if (p[0] == "top")
                {
                    LookListValues.TopShare = float.Parse(p[1], Inv);
                    LookListValues.TopBase = float.Parse(p[2], Inv);
                    yield return ThroneRoom(game, "variant-top" + p[1] + "-base" + p[2]);
                }
                else if (p[0] == "lantern")
                {
                    LookListValues.LanternShare = float.Parse(p[1], Inv);
                    yield return Passage(game, "variant", 1, "variant-lantern" + p[1] + "-04a-passage-floor1");
                }
            }

            // The nave last (the room lab's layout stays the floors' builder's after it).
            foreach (string tag in new[] { "before", "after" })
            {
                if (!string.IsNullOrEmpty(only) && only != tag) continue;
                LookListValues.Reset();
                LookListValues.On = tag == "after";
                yield return Nave(game, tag);
            }
        }
    }
}
