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
    /// Work order 091, a look check: pictures of lamplit C (the default since Kristian played it, 2026-11-22: "C er best") in the
    /// room kinds nobody has looked at on purpose, from the game's own camera at its default distance, taken without a window
    /// the way work order 085 took its pictures (<see cref="LamplitPicturesTests"/>). It changes nothing in the game: it builds
    /// floors of one run seed by the game's own intents (<see cref="GeneratedLevels.Build"/>), puts her where a picture wants
    /// her, and draws the camera into a texture. Runs ONLY when the environment variable ROGUELIKE_CAPTURE_091 names a folder
    /// outside the project (otherwise it is ignored: no ordinary suite takes a picture or leaves a file):
    ///
    ///   $env:ROGUELIKE_CAPTURE_091 = 'C:\Users\kriss\.claude-automation\runner\captures\091'
    ///   run-tests.ps1 PlayMode -Filter "LightRolloutPicturesTests"
    ///
    /// The pictures: a treasure room with its chest; the elite room mid-fight; floor 5's throne room mid-fight with the boss's
    /// telegraph; a passage with her in its middle; a room's door shut behind her (work order 086); the entrance at a floor's
    /// start; one room of each theme that is not the nave's, at the fight's line with the room asleep; and the two darkest
    /// walkable places of the run's five floors, found by measuring (<see cref="LampLight"/>), with an enemy 6 m from her.
    /// A fight is taken as 085's third moment was: she past the line, shooting at the nearest enemy every 1.5 s, until the
    /// moment is there. The interface is not drawn (IMGUI needs a window). The second test measures the nave's fight line
    /// (085's style room and spot) by the same measure, for the number beside the darkest places.
    /// </summary>
    public class LightRolloutPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_091";
        const int W = 1280, H = 720;
        /// <summary>The run seeds tried, from this one up, until the run's five floors hold every room the pictures want.</summary>
        const ulong FirstRunSeed = 0x09100001UL;
        const int SeedsTried = 400;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 091)");
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
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures.txt"), _log);
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

        /// <summary>Lamplit C now, in this frame: the floor follows, the mood and the lamps look at it at once (as 085's LightNow).</summary>
        void LightC()
        {
            GameSettings.SetLightStep(3, keep: false);
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
                _log.Add(name + ".png: " + what + "; " + Counts());
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
            return string.Format(Inv, "{0} lights on in the scene{1}", enabled,
                                 v != null && v.On != null ? string.Format(Inv, " ({0} lamps, {1} flashes lit, level {2})", v.LampLightsOn, v.FlashesLit, v.On.Name) : " (LAMPLIT OFF)");
        }

        static bool InView(Camera c, Vector3 p)
        {
            Vector3 v = c.WorldToViewportPoint(p);
            return v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > 0.03f && v.y < 0.97f;
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

        string Where(GeneratedLevel level, LevelRoom room, ulong run) =>
            string.Format(Inv, "{0}, floor {1}, floor seed {2} (run seed {3})", RoomName(level.Layout, room), level.Layout.Floor, level.Layout.SeedText, LevelSeed.ToText(run));

        /// <summary>The floor of the run, built anew (her at its entrance), lamplit C on.</summary>
        IEnumerator Floor(ulong run, int floor, Action<GeneratedLevel> done)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(run, floor), floor);
            Assert.That(level, Is.Not.Null, "floor " + floor);
            yield return null;
            LightC();
            yield return null;
            done(level);
        }

        /// <summary>A ground point <paramref name="depth"/> m straight in from the middle of the door, on the NavMesh, in the room.</summary>
        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            OnFloor(level, room, level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d)));

        /// <summary>The NavMesh point nearest <paramref name="p"/> in the room (searched out to 4 m); the point itself if there is none.</summary>
        static Vector3 OnFloor(GeneratedLevel level, LevelRoom room, Vector3 p)
        {
            for (float r = 0.3f; r <= 4f; r += 0.3f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return hit.position;
            return p;
        }

        Enemy Nearest(IEnumerable<Enemy> enemies) =>
            enemies.Where(e => e != null && e.isActiveAndEnabled && !e.IsDead).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).FirstOrDefault();

        // ---------------- The measure of the light ----------------

        static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        /// <summary>
        /// The lamps' light on the floor at a point, as the game draws it at a level: the sum over every lamp with a light of its
        /// own that is lit while she stands in <paramref name="room"/> (the lamps of the rooms the cave shows from there,
        /// <see cref="CaveSight.Shown"/>), of its intensity at the level (no flicker) times its colour's luminance times the URP
        /// point light's fall-off (1/d², times (1 - (d²/r²)²)², d from the light to the floor point, r its range). Her own light,
        /// the shots' flashes, the even base (ambient and key, the same everywhere on a floor) and the mood's painted glow are not
        /// in it. Also the distance on the ground to the nearest lamp of those rooms (carried ones too).
        /// </summary>
        public static void LampLight(GeneratedLevel level, LampPlan plan, LamplitValues at, LevelRoom room, Vector3 point, bool[] shown, out float light, out float nearest)
        {
            LampValues v = LamplitView.Lamps;
            CaveSight.Shown(level.Layout, room.Order, shown);
            light = 0f;
            nearest = float.PositiveInfinity;
            foreach (Lamp l in plan.Lamps)
            {
                if (l.Room < 0 || l.Room >= shown.Length || !shown[l.Room]) continue;
                Vector3 pos = level.ToWorld(l.At) + Vector3.up * (float)l.Height;
                float flat = new Vector2(pos.x - point.x, pos.z - point.z).magnitude;
                if (flat < nearest) nearest = flat;
                if (l.CarriedBy != null) continue;
                v.Of(l.Kind, out _, out float range);
                float d2 = (pos - new Vector3(point.x, 0f, point.z)).sqrMagnitude;
                float f = d2 / Mathf.Max(1e-4f, range * range);
                float smooth = Mathf.Clamp01(1f - f * f);
                light += l.Intensity(v) * Mathf.Max(0f, at.LampLevel) * Luma(v.ColourOf(l.Kind)) * smooth * smooth / Mathf.Max(d2, 1e-4f);
            }
        }

        sealed class Dark
        {
            public int Floor;
            public LevelRoom Room;
            public Vector3 At;
            public float Light = float.PositiveInfinity, Nearest;
            public int Points, Unlit;
        }

        /// <summary>Lamp light below this is none (a lamp's light ends at its range, so much of a floor has exactly none).</summary>
        const float NoLampLight = 0.005f;

        /// <summary>Is a point with this light and nearest lamp darker than the darkest so far? Less light; with no lamp light at both, farther from every lamp.</summary>
        static bool Darker(float light, float nearest, Dark than) =>
            light < NoLampLight && than.Light < NoLampLight ? nearest > than.Nearest : light < than.Light;

        /// <summary>
        /// The darkest walkable point of the floor by <see cref="LampLight"/>: every square metre of every room's open floor (a
        /// point at its middle) that is on the NavMesh within 0.3 m and in the room; the least lamp light, and among points with
        /// none (<see cref="NoLampLight"/>) the one farthest from every lamp. Also each room's, and how many points have none.
        /// </summary>
        static Dark DarkestOf(GeneratedLevel level, LampPlan plan, LamplitValues at, out Dictionary<int, Dark> perRoom)
        {
            LevelLayout layout = level.Layout;
            var shown = new bool[layout.Rooms.Count];
            var best = new Dark { Floor = layout.Floor };
            perRoom = new Dictionary<int, Dark>();
            foreach (LevelRoom room in layout.Rooms)
                foreach (LevelPoint p in LampPlan.FloorPoints(layout, room))
                {
                    Vector3 w = level.ToWorld(p);
                    if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    if (new Vector2(hit.position.x - w.x, hit.position.z - w.z).magnitude > 0.3f || level.RoomAt(hit.position) != room) continue;
                    LampLight(level, plan, at, room, hit.position, shown, out float light, out float nearest);
                    if (!perRoom.TryGetValue(room.Order, out Dark r)) perRoom[room.Order] = r = new Dark { Floor = layout.Floor, Room = room };
                    r.Points++;
                    best.Points++;
                    if (light < NoLampLight) { r.Unlit++; best.Unlit++; }
                    if (Darker(light, nearest, r)) { r.Light = light; r.Nearest = nearest; r.At = hit.position; }
                    if (Darker(light, nearest, best)) { best.Light = light; best.Nearest = nearest; best.At = hit.position; best.Room = room; }
                }
            return best;
        }

        /// <summary>
        /// Round two (work order 091: "a second only for a picture that failed to show what it should"): with
        /// ROGUELIKE_CAPTURE_091_ONLY set to picture numbers ("02,07"), only those are taken; the others are not taken again.
        /// </summary>
        static bool Take(string number)
        {
            string only = Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_091_ONLY");
            return string.IsNullOrEmpty(only) || only.Split(',').Any(s => s.Trim() == number);
        }

        // ---------------- The run ----------------

        sealed class Wanted
        {
            public ulong Run;
            public LevelLayout[] Floors = new LevelLayout[6];
        }

        static bool IsNave(LevelRoom r) => r.Template != null && r.Template.Template != null && r.Template.Template.Name == "nave";

        static readonly RoomTheme[] ThemesWanted = { RoomTheme.TombHall, RoomTheme.Ossuary, RoomTheme.Archive, RoomTheme.Shrine };

        /// <summary>A combat room of the theme (a shrine that is not the nave), the first in the run's order; floor and room.</summary>
        static (int floor, LevelRoom room) ThemeRoom(Wanted w, RoomTheme theme)
        {
            for (int f = 1; f <= 5; f++)
                foreach (LevelRoom r in w.Floors[f].Rooms)
                    if (r.Kind == RoomKind.Combat && !r.Elite && r.Theme == theme && !(theme == RoomTheme.Shrine && IsNave(r))) return (f, r);
            return (0, null);
        }

        static (int floor, LevelRoom room) VaultRoom(Wanted w)
        {
            for (int f = 1; f <= 5; f++)
                foreach (LevelRoom r in w.Floors[f].Rooms)
                    if (r.Kind == RoomKind.Treasure && r.Theme == RoomTheme.Vault && r.Props.Any(p => p.Model == PropModel.Chest)) return (f, r);
            return (0, null);
        }

        /// <summary>
        /// The elite room, the first in the run's order whose elite draws its telegraph on the floor (a shooter or a caster:
        /// the brute and the guard are read on their bodies since work order 035, and draw nothing there); else the first with
        /// any enemy that draws one (a shooter, a caster, a bomber, a warder); else the first (round one took floor 1's, whose
        /// brute and swarmers drew none in 45 s).
        /// </summary>
        static (int floor, LevelRoom room) EliteRoom(Wanted w)
        {
            bool Draws(EnemyKind k) => k == EnemyKind.Shooter || k == EnemyKind.Caster || k == EnemyKind.Bomber || k == EnemyKind.Warder;
            for (int pass = 0; pass < 3; pass++)
                for (int f = 1; f <= 5; f++)
                {
                    LevelRoom r = w.Floors[f].EliteRoom;
                    if (r == null) continue;
                    if (pass == 0 && !r.EnemyGroups.Any(g => g != null && g.Elite && (g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster))) continue;
                    if (pass == 1 && !r.EnemyGroups.Any(g => g != null && Draws(g.Kind))) continue;
                    return (f, r);
                }
            return (0, null);
        }

        /// <summary>The first run seed from <see cref="FirstRunSeed"/> whose five floors hold a vault with a chest, an elite room and a combat room of each theme wanted.</summary>
        static Wanted FindRun(LevelValues values)
        {
            for (int s = 0; s < SeedsTried; s++)
            {
                var w = new Wanted { Run = FirstRunSeed + (ulong)s };
                for (int f = 1; f <= 5; f++) w.Floors[f] = LevelGenerator.Generate(RunSeeds.FloorSeed(w.Run, f), f, values);
                if (VaultRoom(w).room == null || EliteRoom(w).room == null) continue;
                if (ThemesWanted.Any(t => ThemeRoom(w, t).room == null)) continue;
                return w;
            }
            return null;
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(2400000)]
        public IEnumerator LamplitC_InTheOtherRoomKinds_ThePassage_TheShutDoor_TheThemes_AndTheTwoDarkestPlaces()
        {
            Camera game = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType}; {W}x{H}; work order 091's round{(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_091_ONLY")) ? "" : " two, only " + Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_091_ONLY"))}; camera distance {GameSettings.CameraDistance(28f):0} m (the default); light step {LightSteps.Names[GameSettings.LightStep]}");
            Wanted w = FindRun(new LevelValues());
            Assert.That(w, Is.Not.Null, "a run seed whose floors hold every room wanted");
            _log.Add("run seed " + LevelSeed.ToText(w.Run) + string.Format(Inv, " (0x{0:X}), the first from 0x{1:X} whose five floors hold a vault with a chest, an elite room and a combat room of each theme wanted", w.Run, FirstRunSeed));
            for (int f = 1; f <= 5; f++) _log.Add("floor " + f + " (seed " + w.Floors[f].SeedText + "): " + string.Join("; ", w.Floors[f].Rooms.Select(r => RoomName(w.Floors[f], r))));
            GeneratedLevel level = null;
            RoomFightValues fights = new LevelValues().Fights;

            // 5. The entrance at the start of floor 1.
            if (Take("05"))
            {
                yield return Floor(w.Run, 1, l => level = l);
                yield return new WaitForSecondsRealtime(1.5f);
                Save(game, "05-entrance", "the entrance room at the start of the floor, where the floor puts her, 1.5 s after it was built: " + Where(level, level.Layout.Rooms[0], w.Run));
            }

            // 1. The treasure room (a vault): standing inside, the chest in view.
            if (Take("01"))
            {
                (int vf, LevelRoom vaultPlan) = VaultRoom(w);
                yield return Floor(w.Run, vf, l => level = l);
                LevelRoom vault = level.Layout.Rooms[vaultPlan.Order];
                GeneratedLevel.PlacedProp chest = level.Props.FirstOrDefault(p => p.Room == vault && p.Prop.Model == PropModel.Chest);
                Vector3 c = chest != null ? chest.Object.transform.position : level.Middle(vault);
                Vector3 spot = c;
                foreach (Vector3 off in new[] { new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, -2.5f), new Vector3(-3f, 0f, -2f), new Vector3(3f, 0f, -2f), new Vector3(0f, 0f, 3f) })
                {
                    Vector3 p = c + off;
                    if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas) && level.RoomAt(hit.position) == vault) { spot = hit.position; break; }
                }
                _player.Teleport(spot);
                yield return new WaitForSecondsRealtime(1.5f);
                Save(game, "01-treasure", string.Format(Inv, "standing inside, {0:0.0} m from the chest, the chest in view: {1}: ", Vector3.Distance(spot, c), Yes(chest != null && InView(game, c))) + Where(level, vault, w.Run));
            }

            // 2. The elite room mid-fight: the elite and at least one telegraph on the floor in view.
            if (Take("02"))
            {
                (int ef, LevelRoom elitePlan) = EliteRoom(w);
                yield return Floor(w.Run, ef, l => level = l);
                LevelRoom room = level.Layout.Rooms[elitePlan.Order];
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                Enemy elite = level.Elite;
                List<Enemy> own = level.EnemiesByRoom[room.Order];
                _player.Teleport(InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, fights) + 2.0));
                string moment = "";
                yield return Fight(game, own, () => elite, 45f, 20f, s => moment = s);
                Save(game, "02-elite", "mid-fight, she 2 m past the line, " + moment + ": " + Where(level, room, w.Run));
            }

            // 3. Floor 5's throne room: the boss in view, mid-fight, one of its telegraphs on the floor.
            if (Take("03"))
            {
                yield return Floor(w.Run, 5, l => level = l);
                LevelRoom room = level.Layout.ExitRoom;
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                Enemy boss = level.Boss;
                Assert.That(boss, Is.Not.Null, "the act boss on floor 5");
                _player.Teleport(InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, fights) + 2.0));
                string moment = "";
                yield return Fight(game, new List<Enemy> { boss }, () => boss, 60f, 60f, s => moment = s);
                Save(game, "03-throne-floor5", "mid-fight, she 2 m past the line, " + moment + ": " + Where(level, room, w.Run));
            }

            // 4a. A passage: her in its middle, both doors' ends in view if the camera holds them (the first of floor 1's passages that shows both; else the shortest).
            if (Take("04"))
            {
                yield return Floor(w.Run, 1, l => level = l);
                LevelLayout layout = level.Layout;
                LevelRoom chosen = null;
                Vector3 chosenSpot = Vector3.zero;
                string seen = "";
                float shortest = float.PositiveInfinity;
                foreach (LevelRoom passage in layout.Rooms.Where(r => r.Kind == RoomKind.Passage))
                {
                    List<LevelDoor> doors = layout.Doors.Where(d => d.Joins(passage.Order)).ToList();
                    if (doors.Count < 2) continue;
                    Vector3 a = DoorMiddle(level, doors[0]), b = DoorMiddle(level, doors[1]);
                    Vector3 spot = OnFloor(level, passage, (a + b) * 0.5f);
                    _player.Teleport(spot);
                    yield return null;
                    yield return null;
                    bool both = InView(game, a) && InView(game, b);
                    float length = Vector3.Distance(a, b);
                    if (both || chosen == null || (!seen.StartsWith("both") && length < shortest))
                    {
                        chosen = passage;
                        chosenSpot = spot;
                        shortest = length;
                        seen = (both ? "both doors' ends in view" : "NOT both doors' ends in view (the camera does not hold this passage's length)") + string.Format(Inv, ", its doors {0:0.0} m apart", length);
                    }
                    if (both) break;
                }
                Assert.That(chosen, Is.Not.Null, "a passage on floor 1");
                _player.Teleport(chosenSpot);
                yield return new WaitForSecondsRealtime(1.5f);
                Save(game, "04a-passage", "her in the middle of the passage, " + seen + ": " + Where(level, chosen, w.Run));
            }

            // 4b. A room's door shut behind her (work order 086), the block risen: the first ordinary combat room of floor 1 (else 2)
            // whose door in is on its south wall (the camera looks north, so the door behind her faces it), she 2.6 m in from it.
            if (Take("04"))
            {
                LevelRoom room = null;
                int df = 0;
                for (int f = 1; f <= 2 && room == null; f++)
                {
                    LevelLayout lay = w.Floors[f];
                    room = lay.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Combat && !r.Elite && r.EnemyCount > 0 && SouthDoor(lay, r));
                    df = f;
                }
                if (room == null)
                {
                    df = 1;
                    room = w.Floors[1].Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite && r.EnemyCount > 0);
                }
                yield return Floor(w.Run, df, l => level = l);
                room = level.Layout.Rooms[room.Order];
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
                bool shut = doors != null && doors.Shut;
                RoomDoor block = shut ? doors.Blocks.FirstOrDefault(b => b != null && b.Door == door) : null;
                _player.Teleport(InFrom(level, room, door, 2.6));
                yield return new WaitForSecondsRealtime(0.3f);
                shut = doors != null && doors.Shut;
                Save(game, "04b-door-shut", string.Format(Inv, "at the room's door, she 2.6 m in from it, the door SHUT behind her: {0}, its block risen {1:0.00}, in view: {2}; the room's fight on (its enemies come at her): ",
                                                          Yes(shut), block != null ? block.Raised : -1f, Yes(block != null && InView(game, block.transform.position))) + Where(level, room, w.Run));
            }

            // 6. One room of each theme that is not the nave's (the nave of 085 is a shrine: a shrine of another template stands here
            // too), at the fight's line (0.6 m short of it, as 085's still), the room asleep.
            foreach (RoomTheme theme in Take("06") ? ThemesWanted : new RoomTheme[0])
            {
                (int tf, LevelRoom plan) = ThemeRoom(w, theme);
                yield return Floor(w.Run, tf, l => level = l);
                LevelRoom room = level.Layout.Rooms[plan.Order];
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                _player.Teleport(InFrom(level, room, door, RoomFightRules.PointDepth(level.Layout, room, door, fights) - 0.6));
                yield return new WaitForSecondsRealtime(1.5f);
                bool asleep = !level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> es) || es.All(e => e == null || !e.IsAwake);
                Save(game, "06-theme-" + RoomThemes.Word(theme).Replace(' ', '-'), "at the fight's line (0.6 m short of it), the room asleep: " + Yes(asleep) + ": " + Where(level, room, w.Run));
            }

            // 7. The two darkest walkable places of the run's five floors, by the measure (LampLight): the darkest of each floor, the two darkest of those.
            var darkest = new List<Dark>();
            for (int f = 1; f <= 5 && Take("07"); f++)
            {
                yield return Floor(w.Run, f, l => level = l);
                LamplitView view = _levels.Lamplit;
                Assert.That(view != null && view.Plan != null, "lamplit on, its plan made");
                Dark d = DarkestOf(level, view.Plan, view.On, out Dictionary<int, Dark> perRoom);
                darkest.Add(d);
                _log.Add(string.Format(Inv, "the measure, floor {0}: {1} walkable points, {2:0} % of them with no lamp light (under {3}); the darkest in {4}: lamps' light {5:0.000}, nearest lamp {6:0.0} m; each room's darkest (light, nearest lamp, share with none): {7}",
                                       f, d.Points, 100.0 * d.Unlit / Math.Max(1, d.Points), NoLampLight, RoomName(level.Layout, d.Room), d.Light, d.Nearest,
                                       string.Join("; ", perRoom.Values.OrderBy(r => r.Room.Order).Select(r => string.Format(Inv, "{0}{1} {2:0.000} {3:0.0} m {4:0} %", r.Room.Letter, r.Room.Order, r.Light, r.Nearest, 100.0 * r.Unlit / Math.Max(1, r.Points))))));
            }
            int n = 0;
            foreach (Dark d in darkest.OrderBy(x => x.Light < NoLampLight ? 0f : x.Light).ThenByDescending(x => x.Nearest).Take(2))
            {
                n++;
                yield return Floor(w.Run, d.Floor, l => level = l);
                LevelRoom room = level.Layout.Rooms[d.Room.Order];
                // The floor's enemies are switched off for the moment, so that the room stays still while the fog fills the
                // room round her; then one (of another room's fight, asleep) is put 6 m from her, in view.
                List<Enemy> all = level.Enemies.Where(e => e != null).ToList();
                foreach (Enemy e in all) e.gameObject.SetActive(false);
                _player.Teleport(d.At);
                yield return new WaitForSecondsRealtime(1.5f);
                Enemy one = all.Where(e => !e.IsElite && e != level.Boss).OrderBy(e => level.RoomAt(e.transform.position) == room ? 1 : 0).FirstOrDefault() ?? all.FirstOrDefault();
                string enemyWords = "NO ENEMY PUT";
                if (one != null)
                {
                    one.gameObject.SetActive(true);
                    Vector3 put = Vector3.zero;
                    bool found = false;
                    for (int k = 0; k < 16 && !found; k++)
                    {
                        float a = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 22.5f * Mathf.Deg2Rad; // north first, then either side
                        Vector3 p = d.At + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 6f;
                        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.4f, NavMesh.AllAreas) && InView(game, hit.position)) { put = hit.position; found = true; }
                    }
                    if (found && one.TryGetComponent(out NavMeshAgent agent) && agent.enabled) agent.Warp(put);
                    else if (found) one.transform.position = put;
                    yield return null;
                    yield return null;
                    enemyWords = string.Format(Inv, "a {0} {1:0.0} m from her ({2}), in view: {3}", one.LogWord, Vector3.Distance(one.transform.position, _player.Position),
                                               level.RoomAt(one.transform.position) == room ? "in the same room" : "outside her room", Yes(InView(game, one.transform.position)));
                }
                Save(game, "07-darkest-" + n, string.Format(Inv, "the darkest walkable place no. {0} of the run's five floors by the measure: lamps' light {1:0.000}, nearest lamp {2:0.0} m; she standing on it, {3}; the floor's other enemies switched off for the moment: ",
                                                             n, d.Light, d.Nearest, enemyWords) + Where(level, room, w.Run));
            }
        }

        static bool SouthDoor(LevelLayout layout, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            return door != null && LevelGeometry.WallOf(room.Rect, door) == LevelGenerator.South;
        }

        static Vector3 DoorMiddle(GeneratedLevel level, LevelDoor door)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            return level.ToWorld(new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5));
        }

        /// <summary>
        /// 085's third moment: she shoots at the nearest enemy every 1.5 s until <paramref name="main"/> telegraphs in view (or,
        /// after <paramref name="anyAfter"/> s, any of <paramref name="own"/> does with the main one in view), at most
        /// <paramref name="most"/> real seconds. The moment's words.
        /// </summary>
        IEnumerator Fight(Camera game, List<Enemy> own, Func<Enemy> main, float most, float anyAfter, Action<string> done)
        {
            float started = Time.realtimeSinceStartup, nextShot = 0f;
            string what = null;
            while (Time.realtimeSinceStartup < started + most)
            {
                if (Time.time >= nextShot)
                {
                    Enemy e = Nearest(own);
                    if (e != null) _player.Shoot(e.transform.position);
                    nextShot = Time.time + 1.5f; // not too often: the room must stay full (085)
                }
                yield return null;
                Enemy m = main();
                bool mainIn = m != null && !m.IsDead && InView(game, m.transform.position);
                if (Time.realtimeSinceStartup < started + 1f || !mainIn) continue;
                if (m.IsTelegraphing && m.Telegraph.positionCount > 0 && InView(game, m.Telegraph.GetPosition(0)))
                {
                    what = m.LogWord + " in view and its own telegraph on the floor in view";
                    break;
                }
                if (Time.realtimeSinceStartup > started + anyAfter)
                {
                    Enemy t = own.FirstOrDefault(e => e != null && !e.IsDead && e.IsTelegraphing && InView(game, e.transform.position));
                    if (t != null)
                    {
                        what = m.LogWord + " in view and a " + t.LogWord + "'s telegraph on the floor in view (none of its own in " + anyAfter.ToString("0", Inv) + " s)";
                        break;
                    }
                    // Round two: one read on its body (work order 035) draws nothing on the floor; its wind-up is its telegraph.
                    if (Time.realtimeSinceStartup > started + anyAfter + 10f && m.State == EnemyState.Windup)
                    {
                        what = m.LogWord + " in view winding up (read on its body: no line on the floor) and no telegraph on the floor in view in " + (anyAfter + 10f).ToString("0", Inv) + " s";
                        break;
                    }
                }
            }
            bool arrow = ThingsThatFly.Current != null && ThingsThatFly.Current.Live > 0;
            done((what ?? "NO MOMENT WITH ITS TELEGRAPH FOUND IN " + most.ToString("0", Inv) + " s") + string.Format(Inv, " after {0:0.0} s{1}", Time.realtimeSinceStartup - started, arrow ? ", something in flight" : ""));
        }

        // ---------------- The nave, by the same measure ----------------

        /// <summary>
        /// The style room of 085 (the nave of the room lab, ordinary, floor 1, seed KCT7B24U) at lamplit C: the lamps' light where
        /// she stood for 085's still (0.6 m short of the fight's line), and the nave's own darkest walkable point, by
        /// <see cref="LampLight"/>, for the number beside the darkest places. No picture.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator TheNavesFightLine_ByTheSameMeasure()
        {
            LabEntry entry = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            GeneratedLevel level = _levels.StartLab(entry, 0, seed);
            Assert.That(level, Is.Not.Null);
            LevelLayout l = level.Layout;
            LevelRoom nave = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            // 085's spot (LamplitPicturesTests.AtDepth): straight in from the door's cell, the fight's line less 0.6 m.
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = nave.Rect.Y * cs + _levels.Values.Fights.PointIn - 0.6;
            for (int k = 0; k < nave.Rect.Width && l.RoomAt(x, y) != nave; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            Vector3 spot = level.ToWorld(new LevelPoint(x, y));
            _player.Teleport(spot);
            yield return new WaitForSecondsRealtime(0.5f);
            LightC();
            yield return null;
            LamplitView view = _levels.Lamplit;
            Assert.That(view != null && view.Plan != null, "lamplit on, its plan made");
            var shown = new bool[l.Rooms.Count];
            LampLight(level, view.Plan, view.On, nave, spot, shown, out float light, out float nearest);
            DarkestOf(level, view.Plan, view.On, out Dictionary<int, Dark> perRoom);
            Dark d = perRoom[nave.Order];
            string line = string.Format(Inv, "the measure, the nave of 085 (room lab, ordinary, floor 1, seed KCT7B24U) at {0}: at 085's fight line (0.6 m short of it, where she stood for its stills): lamps' light {1:0.000}, nearest lamp {2:0.0} m; " +
                                        "the nave's darkest walkable point: {3:0.000}, nearest lamp {4:0.0} m; {5:0} % of the nave's {6} walkable points with no lamp light (under {7}); the base at C: ambient {8} and key {9} of the scene's own, her own light {10} (range {11} m)",
                                        view.On.Name, light, nearest, d.Light, d.Nearest, 100.0 * d.Unlit / Math.Max(1, d.Points), d.Points, NoLampLight, view.On.AmbientLevel, view.On.KeyLevel, view.On.HerLightIntensity, view.On.HerLightRange);
            _log.Add(line);
            TestContext.WriteLine(line);
            Debug.Log(line);
        }
    }
}
