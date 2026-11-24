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
    /// Work order 105, a look order's pictures: the same combat room (the nave of the room lab, 085's style room, seed KCT7B24U,
    /// the mix of each floor) and the entrance (work order 091's run seed 226K2223, as work order 100 pictured it) on the five
    /// floors, each with its palette, singly and side by side in one contact sheet; a fight in the nave on floors 1, 3 and 5,
    /// and on floor 5 in the throne room with the boss. Lamplit C (the default), the game's own camera at its default distance,
    /// taken without a window the way work orders 091 and 100 took theirs. It changes nothing in the game. Runs ONLY when the
    /// environment variable ROGUELIKE_CAPTURE_105 names a folder outside the project (otherwise it is ignored);
    /// ROGUELIKE_CAPTURE_105_TAG goes in front of every file's name:
    ///
    ///   $env:ROGUELIKE_CAPTURE_105 = 'C:\Users\kriss\.claude-automation\runner\captures\105'
    ///   run-tests.ps1 PlayMode -Filter "FloorPalettePicturesTests"
    ///
    /// With each picture its mean colour; with the floors the palette's values and the darkest walkable point by the order's
    /// measure (<see cref="FloorPaletteRules.LightAt"/>), today and with the palette.
    /// </summary>
    public class FloorPalettePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_105";
        const string TagVariable = "ROGUELIKE_CAPTURE_105_TAG";
        const int W = 1280, H = 720;
        /// <summary>The contact sheet's tiles: a picture at two fifths of its size, a gap between.</summary>
        const int TileW = 512, TileH = 288, Gap = 6;
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
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 105)");
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
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, _tag + "pictures-log.txt"), _log);
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

        /// <summary>Renders the camera into a picture, saves it, and gives its pixels back (bottom row first).</summary>
        Color32[] Save(Camera cam, string name, string what)
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
                Color32[] px = tex.GetPixels32();
                _log.Add(_tag + name + ".png: " + what + "; " + MeanColour(px) + "; " + Counts());
                return px;
            }
            catch (Exception e)
            {
                _log.Add(_tag + name + " FAILED: " + e.Message);
                return null;
            }
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
            GeneratedLevel level = _levels.Level;
            FloorPaletteView p = level != null ? level.GetComponent<FloorPaletteView>() : null;
            return (v != null && v.On != null ? string.Format(Inv, "{0} lamps lit, level {1}", v.LampLightsOn, v.On.Name) : "LAMPLIT OFF")
                   + "; palette " + (p != null ? p.Palette.Name + string.Format(Inv, ", stone tint {0}", p.StoneTint) : "none (as today)");
        }

        static string MeanColour(Color32[] px)
        {
            double r = 0, g = 0, b = 0;
            foreach (Color32 c in px) { r += c.r; g += c.g; b += c.b; }
            int n = Math.Max(1, px.Length);
            r /= n; g /= n; b /= n;
            return string.Format(Inv, "the picture's mean colour (0-255) R {0:0.0} G {1:0.0} B {2:0.0}, grey {3:0.0}, blue over red {4:+0.0;-0.0}", r, g, b, 0.2126 * r + 0.7152 * g + 0.0722 * b, b - r);
        }

        /// <summary>A contact sheet: the pictures in rows (null for none), each cut down to a tile by averaging, a gap between.</summary>
        void Sheet(string name, List<Color32[]> pictures, int columns, string what)
        {
            int rows = (pictures.Count + columns - 1) / columns;
            int sw = columns * TileW + (columns + 1) * Gap, sh = rows * TileH + (rows + 1) * Gap;
            var sheet = new Color32[sw * sh];
            for (int i = 0; i < sheet.Length; i++) sheet[i] = new Color32(24, 24, 24, 255);
            for (int k = 0; k < pictures.Count; k++)
            {
                Color32[] px = pictures[k];
                if (px == null) continue;
                int col = k % columns, row = k / columns;
                int x0 = Gap + col * (TileW + Gap), y0 = sh - (row + 1) * (TileH + Gap); // the first row at the top (the pixels run from the bottom)
                for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                    {
                        int sx0 = x * W / TileW, sx1 = Math.Max(sx0 + 1, (x + 1) * W / TileW), sy0 = y * H / TileH, sy1 = Math.Max(sy0 + 1, (y + 1) * H / TileH);
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int sy = sy0; sy < sy1; sy++)
                            for (int sx = sx0; sx < sx1; sx++)
                            {
                                Color32 c = px[sy * W + sx];
                                r += c.r; g += c.g; b += c.b; n++;
                            }
                        sheet[(y0 + y) * sw + x0 + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                    }
            }
            var tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(sheet);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(_folder, _tag + name + ".png"), tex.EncodeToPNG());
                _log.Add(_tag + name + ".png: " + what);
            }
            finally { Object.Destroy(tex); }
        }

        GeneratedLevel Nave(int floor)
        {
            _levels.Values = new LevelValues();
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            int mix = Array.FindIndex(RoomLab.Mixes, m => m.Floor == floor);
            GeneratedLevel level = _levels.StartLab(nave, mix, seed);
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

        Enemy Nearest(IEnumerable<Enemy> enemies) =>
            enemies.Where(e => e != null && e.isActiveAndEnabled && !e.IsDead).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).FirstOrDefault();

        /// <summary>085's third moment: she shoots at the nearest enemy every 1.5 s until a telegraph is on the floor in view and something flies (an enemy winding up after 25 s), at most 40 s.</summary>
        IEnumerator Fight(Camera game, GeneratedLevel level, Action<string> done)
        {
            float started = Time.realtimeSinceStartup, nextShot = 0f;
            string what = null;
            while (Time.realtimeSinceStartup < started + 40f)
            {
                if (Time.time >= nextShot)
                {
                    Enemy e = Nearest(level.Enemies);
                    if (e != null) _player.Shoot(e.transform.position);
                    nextShot = Time.time + 1.5f;
                }
                yield return null;
                bool telegraph = level.Enemies.Any(e => e != null && !e.IsDead && e.IsTelegraphing && e.Telegraph.positionCount > 0 && InView(game, e.Telegraph.GetPosition(0)));
                bool windup = level.Enemies.Any(e => e != null && !e.IsDead && e.State == EnemyState.Windup && InView(game, e.transform.position));
                bool flying = ThingsThatFly.Current != null && ThingsThatFly.Current.Live > 0;
                if (Time.realtimeSinceStartup < started + 1f) continue;
                if (telegraph && flying) { what = "a telegraph on the floor in view and something in flight"; break; }
                if (Time.realtimeSinceStartup > started + 25f && (telegraph || windup)) { what = telegraph ? "a telegraph on the floor in view (nothing in flight in 25 s)" : "an enemy winding up in view (no line on the floor in 25 s)"; break; }
            }
            done((what ?? "NO MOMENT WITH A TELEGRAPH FOUND IN 40 s") + string.Format(Inv, " after {0:0.0} s", Time.realtimeSinceStartup - started));
        }

        static bool InView(Camera c, Vector3 p)
        {
            Vector3 v = c.WorldToViewportPoint(p);
            return v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > 0.03f && v.y < 0.97f;
        }

        /// <summary>The palette's row in words, and the darkest walkable point of the floor by the order's measure at lamplit C.</summary>
        string Measure(GeneratedLevel level)
        {
            FloorPalette p = FloorPaletteView.Values.On ? FloorPaletteView.Values.Floor1 : null;
            FloorPaletteView view = level.GetComponent<FloorPaletteView>();
            if (view != null) p = view.Palette;
            LevelLayout layout = level.Layout;
            LamplitView lamplit = _levels.Lamplit;
            if (lamplit == null || lamplit.Plan == null) return "LAMPLIT OFF: no measure";
            MoodValues mood = level.Values.Mood;
            var shown = new bool[layout.Rooms.Count];
            float darkToday = float.PositiveInfinity, darkNow = 0f;
            int points = 0;
            foreach (LevelRoom room in layout.Rooms)
                foreach (LevelPoint pt in LampPlan.FloorPoints(layout, room))
                {
                    Vector3 w = level.ToWorld(pt);
                    if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) || level.RoomAt(hit.position) != room) continue;
                    FloorPaletteRules.LightAt(layout, lamplit.Plan, LamplitView.Lamps, lamplit.On, mood, view != null ? view.Palette : null, room, pt, shown, out float today, out float now);
                    points++;
                    if (today < darkToday) { darkToday = today; darkNow = now; }
                }
            return string.Format(Inv, "{0}: stone {1} (lifted to {2}), lamps {3}, base {4} x {5}, fog {6} x {7}; the darkest of {8} walkable points by the order's measure at {9}: today {10:0.0000}, with the palette {11:0.0000}",
                                 p != null ? p.Name : "switch off", p?.Stone, view != null ? view.StoneTint.ToString() : "(1, 1, 1)", p?.Lamps, p?.Base, p?.BaseLevel, p?.Fog, p?.FogShare,
                                 points, lamplit.On.Name, darkToday, darkNow);
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator TheNaveAndTheEntrance_OnTheFiveFloors_SinglyAndInOneContactSheet()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 105, tag '{_tag.TrimEnd('-')}'; switch {(FloorPaletteView.Values.On ? "on" : "OFF")}; camera distance {GameSettings.CameraDistance(28f):0} m (the default); light step {LightSteps.Names[GameSettings.LightStep]}");
            var naves = new List<Color32[]>();
            var entrances = new List<Color32[]>();
            double line = _levels.Values.Fights.PointIn;
            for (int f = 1; f <= 5; f++)
            {
                GeneratedLevel level = Nave(f);
                _player.Teleport(AtDepth(level, line - 0.6));
                yield return new WaitForSecondsRealtime(0.6f);
                LightC();
                yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                naves.Add(Save(game, "nave-floor" + f, "the nave (room lab, ordinary, mix floor " + f + ", seed KCT7B24U), standing still 0.6 m short of the fight's line, the room asleep"));
                _log.Add("  " + Measure(level));
            }
            for (int f = 1; f <= 5; f++)
            {
                _levels.Values = new LevelValues();
                GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, f), f);
                Assert.That(level, Is.Not.Null);
                yield return null;
                LightC();
                yield return new WaitForSecondsRealtime(1.5f);
                entrances.Add(Save(game, "entrance-floor" + f, "the entrance at the start of floor " + f + " of run 226K2223 (floor seed " + level.Layout.SeedText + "), where the floor puts her, 1.5 s after it was built"));
                _log.Add("  " + Measure(level));
            }
            Sheet("sheet-nave-and-entrance-floors1to5", naves.Concat(entrances).ToList(), 5, "the contact sheet: top row the nave, bottom row the entrance, floors 1 to 5 from left to right");
        }

        /// <summary>
        /// The same nave with each floor's palette (round 2: the lab's nave of each floor's mix has its own lanterns and sconces, the
        /// kit's draws being the floor's, so floor 2's picture came out warmer than floor 1's by its lamps alone): the nave of the
        /// mix "floor 1", the same lamps in the same places, built five times, floor 1's row of the table standing in for each
        /// floor's in turn (put back after).
        /// </summary>
        [UnityTest, Timeout(1800000)]
        public IEnumerator TheSameNave_TheSameLamps_WithEachFloorsPalette_SinglyAndInOneContactSheet()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode}; work order 105, the same nave with each floor's palette, tag '{_tag.TrimEnd('-')}'");
            FloorPaletteValues table = FloorPaletteView.Values;
            FloorPalette[] rows = table.Rows;
            var pictures = new List<Color32[]>();
            try
            {
                for (int f = 1; f <= 5; f++)
                {
                    table.Floor1 = rows[f - 1];
                    GeneratedLevel level = Nave(1);
                    _player.Teleport(AtDepth(level, _levels.Values.Fights.PointIn - 0.6));
                    yield return new WaitForSecondsRealtime(0.6f);
                    LightC();
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.3f);
                    pictures.Add(Save(game, "same-nave-palette" + f, "the nave of the mix floor 1 (its lamps), with floor " + f + "'s palette, standing still 0.6 m short of the fight's line"));
                }
            }
            finally { table.Floor1 = rows[0]; }
            Sheet("sheet-same-nave-palettes1to5", pictures, 5, "the same nave, the same lamps, with the palettes of floors 1 to 5 from left to right");
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator AFight_InTheNave_OnFloorsOneThreeAndFive_AndTheBossOnFloorFive()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode}; work order 105's fights, tag '{_tag.TrimEnd('-')}'; switch {(FloorPaletteView.Values.On ? "on" : "OFF")}");
            var fights = new List<Color32[]>();
            foreach (int f in new[] { 1, 3, 5 })
            {
                GeneratedLevel level = Nave(f);
                _player.Teleport(AtDepth(level, _levels.Values.Fights.PointIn + 2.0));
                yield return null;
                LightC();
                string moment = "";
                yield return Fight(game, level, s => moment = s);
                fights.Add(Save(game, "fight-nave-floor" + f, "mid-fight in the nave (room lab, mix floor " + f + "), she 2 m past the line, shooting at the nearest enemy every 1.5 s: " + moment));
            }

            // Floor 5's throne room: the boss, mid-fight.
            _levels.Values = new LevelValues();
            GeneratedLevel five = _levels.Build(RunSeeds.FloorSeed(RunSeed, 5), 5);
            yield return null;
            LightC();
            LevelRoom throne = five.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(five.Layout, throne);
            Vector3 spot = five.ToWorld(RoomDoorRules.PullInPlace(five.Layout, throne, door, RoomFightRules.PointDepth(five.Layout, throne, door, _levels.Values.Fights) + 2.0, 0d));
            for (float r = 0.3f; r <= 4f; r += 0.3f)
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, r, NavMesh.AllAreas) && five.RoomAt(hit.position) == throne) { spot = hit.position; break; }
            _player.Teleport(spot);
            string bossMoment = "";
            yield return Fight(game, five, s => bossMoment = s);
            fights.Add(Save(game, "fight-throne-floor5", "mid-fight in floor 5's throne room with the boss (run 226K2223), she 2 m past the line: " + bossMoment
                                                         + "; the boss in view: " + (five.Boss != null && InView(game, five.Boss.transform.position) ? "yes" : "NO")));
            Sheet("sheet-fights", fights, 4, "the fights side by side: the nave on floors 1, 3, 5, then floor 5's throne room");
        }
    }
}
