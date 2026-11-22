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
    /// Work order 076 (the room is a cave): pictures of the rooms from the game's camera, taken without a window. Runs ONLY
    /// when the environment variable ROGUELIKE_CAPTURE_076 names a folder outside the project (otherwise it is ignored, so no
    /// ordinary suite takes a picture or leaves a file). For one floor (seed 42, floor 1, built as the game builds it) it
    /// stands her in a small room, a large room, a room at the floor's north edge, the first passage, the treasure room and
    /// the exit room, at the door she comes in by, in the middle and in two corners, and renders the game's camera into a
    /// texture (960 by 540 a picture, four to a sheet of 1920 by 1080). With ROGUELIKE_CAPTURE_076_SWITCH set to "off" or
    /// "on", the cave's switch (<c>LevelValues.Cave</c>, looked up by name) is set so before the floor is built; "both"
    /// takes every picture twice, off then on. Beside the pictures a text file lists where she stood and how many renderers
    /// of other rooms and passages the camera's view held.
    ///
    /// Work order 083 (the cave, second attempt): with ROGUELIKE_CAPTURE_LOOKS a list of looks, comma between them, from
    /// "before" (the cave off), "rock" (076's look), "dark" (083's) and "dark25" or "dark22" (083's with the camera at that
    /// distance step, the pause menu's setting, not kept), every picture is taken once in each look, its file and sheet named
    /// with the look; it overrides the switch above. The text file adds each look's counts and how long the camera took to
    /// draw a picture in this batch run (the CPU's side only: no GPU timing without a window).
    /// </summary>
    public class CavePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_076";
        const string SwitchVariable = "ROGUELIKE_CAPTURE_076_SWITCH";
        const string LooksVariable = "ROGUELIKE_CAPTURE_LOOKS"; // work order 083
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int W = 960, H = 540;

        PlayerCharacter _player;
        GeneratedLevels _levels;

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 076)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FolderVariable))) yield break;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            GameSettings.SetCameraDistanceStep(0, false); // work order 083: a look at a closer step is never kept
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>The cave's switch by name, so that this test compiles before and after it exists; false when there is none.</summary>
        static bool SetCave(LevelValues v, bool on)
        {
            var f = typeof(LevelValues).GetField("Cave");
            if (f == null || f.FieldType != typeof(bool)) return false;
            f.SetValue(v, on);
            return true;
        }

        sealed class Spot
        {
            public string Name;
            public LevelRoom Room;
            public Vector3 At;
        }

        static Vector3 Snap(GeneratedLevel level, Vector3 p, LevelRoom room)
        {
            for (float r = 0.5f; r <= 6f; r += 0.5f)
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, r, NavMesh.AllAreas) && level.RoomAt(hit.position) == room) return new Vector3(hit.position.x, 0f, hit.position.z);
            return p;
        }

        static List<Spot> SpotsOf(GeneratedLevel level, LevelRoom room, string label)
        {
            LevelLayout layout = level.Layout;
            float cs = layout.CellSize;
            var spots = new List<Spot>();
            string tag = label + "-" + room.Letter + room.Order.ToString(Inv);
            if (room.Kind == RoomKind.Passage)
            {
                List<(int x, int y)> path = Passages.PathOf(layout, room);
                Vector3 Cell((int x, int y) c) => level.ToWorld(new LevelPoint((c.x + 0.5) * cs, (c.y + 0.5) * cs));
                spots.Add(new Spot { Name = tag + "-first", Room = room, At = Cell(path[0]) });
                spots.Add(new Spot { Name = tag + "-middle", Room = room, At = Cell(path[path.Count / 2]) });
                spots.Add(new Spot { Name = tag + "-last", Room = room, At = Cell(path[path.Count - 1]) });
                return spots;
            }
            LevelPoint middle = room.Middle(cs);
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            Vector3 doorSpot;
            if (door == null) doorSpot = level.PlayerStart;
            else
            {
                door.Opening(cs, out LevelPoint a, out LevelPoint b);
                var dm = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                double dx = door.Vertical ? Math.Sign(middle.X - dm.X) * 1.5 : 0, dy = door.Vertical ? 0 : Math.Sign(middle.Y - dm.Y) * 1.5;
                doorSpot = level.ToWorld(new LevelPoint(dm.X + dx, dm.Y + dy));
            }
            spots.Add(new Spot { Name = tag + "-door", Room = room, At = Snap(level, doorSpot, room) });
            spots.Add(new Spot { Name = tag + "-middle", Room = room, At = Snap(level, level.ToWorld(middle), room) });
            spots.Add(new Spot { Name = tag + "-cornerNE", Room = room, At = Snap(level, level.ToWorld(new LevelPoint(room.Rect.Right * cs - 2, room.Rect.Top * cs - 2)), room) });
            spots.Add(new Spot { Name = tag + "-cornerSW", Room = room, At = Snap(level, level.ToWorld(new LevelPoint(room.Rect.X * cs + 2, room.Rect.Y * cs + 2)), room) });
            return spots;
        }

        /// <summary>The rooms the order names: small, large, at the north edge, the first passage, the treasure room, the exit room.</summary>
        static List<(string label, LevelRoom room)> Chosen(LevelLayout layout)
        {
            var rooms = layout.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList();
            var combat = rooms.Where(r => r.Kind == RoomKind.Combat).ToList();
            CellRect box = layout.Bounds();
            var picks = new List<(string, LevelRoom)>();
            void Add(string label, LevelRoom r)
            {
                if (r != null && picks.All(p => p.Item2 != r)) picks.Add((label, r));
            }
            Add("small", combat.OrderBy(r => r.Rect.Width * r.Rect.Height - r.SolidCount).FirstOrDefault());
            Add("large", rooms.OrderByDescending(r => r.Rect.Width * r.Rect.Height - r.SolidCount).FirstOrDefault());
            Add("edge", rooms.OrderBy(r => box.Top - r.Rect.Top).ThenBy(r => r.Order).FirstOrDefault());
            Add("passage", layout.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Passage));
            Add("treasure", layout.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Treasure));
            Add("exit", layout.ExitRoom);
            return picks;
        }

        /// <summary>Renderers drawn now under the level and the mood, outside the room she is in, whose bounds the camera's view holds.</summary>
        static (int other, int total) InView(GeneratedLevel level, Camera cam, LevelRoom here)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var shown = new bool[level.Layout.Rooms.Count];
            CaveSight.Shown(level.Layout, here != null ? here.Order : -1, shown);
            int other = 0, total = 0;
            var roots = new List<Transform> { level.transform };
            if (level.Mood != null && level.Mood.Root != null) roots.Add(level.Mood.Root.transform);
            foreach (Transform root in roots)
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) continue;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    total++;
                    Vector3 c = r.bounds.center;
                    LevelRoom at = level.RoomAt(c);
                    if (at != null && here != null && !shown[at.Order]) other++;
                }
            return (other, total);
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheRooms_FromTheGamesCamera()
        {
            string folder = Folder();
            string which = (Environment.GetEnvironmentVariable(SwitchVariable) ?? "").Trim().ToLowerInvariant();
            var settings = which == "both" ? new bool?[] { false, true } : which == "off" ? new bool?[] { false } : which == "on" ? new bool?[] { true } : new bool?[] { null };
            // Work order 083: the looks, if named.
            string[] looks = (Environment.GetEnvironmentVariable(LooksVariable) ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().ToLowerInvariant()).ToArray();
            if (looks.Length > 0) settings = looks.Select(l => (bool?)(l != "before")).ToArray();
            var log = new List<string>();
            log.Add($"batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} screen {Screen.width}x{Screen.height}");
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var sheet = new Texture2D(2 * W, 2 * H, TextureFormat.RGBA32, false);
            for (int lookIndex = 0; lookIndex < settings.Length; lookIndex++)
            {
                bool? cave = settings[lookIndex];
                _levels.Values = new LevelValues();
                string suffix = "";
                if (cave.HasValue)
                {
                    bool had = SetCave(_levels.Values, cave.Value);
                    suffix = cave.Value ? "-on" : "-off";
                    log.Add("switch Cave " + (had ? (cave.Value ? "on" : "off") : "NOT FOUND"));
                }
                if (looks.Length > 0)
                {
                    string look = looks[lookIndex];
                    suffix = "-" + look;
                    _levels.Values.CaveRules.Look = look == "rock" ? CaveLook.Rock : look.StartsWith("dark") ? CaveLook.Dark : CaveLook.Before;
                    int step = look == "dark25" ? 1 : look == "dark22" ? 2 : 0;
                    if (look == "darktall") _levels.Values.CaveRules.FarHeight = 4f * _levels.Values.CaveRules.HerHeight; // the order's "try 2.5 to 4"
                    if (look == "darklow") _levels.Values.CaveRules.FarHeight = 2.5f * _levels.Values.CaveRules.HerHeight;
                    GameSettings.SetCameraDistanceStep(step, false);
                    log.Add("look " + look + ": Cave " + _levels.Values.Cave + ", Look " + _levels.Values.CaveRules.Look + ", camera distance "
                            + GameSettings.CameraDistance(new FeelValues().CameraDistance).ToString(Inv) + " m");
                }
                // A vault (the treasure room with the chest on the dais and the large coin heaps, work order 052): the first seed from 1 whose floor 1 has one.
                ulong vaultSeed = 0;
                for (ulong s = 1; s < 400 && vaultSeed == 0; s++)
                {
                    LevelLayout l = LevelGenerator.Generate(s, 1, _levels.Values);
                    LevelRoom v = l.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Treasure && r.Theme == RoomTheme.Vault);
                    if (v != null) vaultSeed = s;
                }
                foreach (ulong seed in new[] { 42UL, vaultSeed })
                {
                if (seed == 0) continue;
                GeneratedLevel level = _levels.Build(seed, 1);
                Assert.That(level, Is.Not.Null);
                yield return Frames(3);
                Camera cam = Camera.main;
                CaveView cv = level.Cave;
                if (cv != null)
                    log.Add(string.Format(Inv, "cave {0}: far sections raised {1}, held at the kit's height {2}, near sections cut to the lip {3} (scale {4:0.00}); stacked pieces {5}, veil and cap meshes {6}; 076's rock faces {7}, tops {8}; falloff {9}",
                                          cv.LookBuilt, cv.Raised, cv.Held, cv.Lips, cv.LipScale, cv.Stacked.Count, cv.DarkMeshes.Count, cv.Rock.Count, cv.Lids.Count, cv.Falloff != null ? "on" : "off"));
                else log.Add("cave: none (before 076)");
                log.Add($"floor seed {level.Layout.SeedText} ({seed}) floor 1; camera {cam.name} clear {cam.clearFlags} background {cam.backgroundColor} fov {cam.fieldOfView} far {cam.farClipPlane}");
                log.Add(LevelMap.Draw(level.Layout));
                var picks = seed == 42UL ? Chosen(level.Layout)
                    : level.Layout.Rooms.Where(r => r.Kind == RoomKind.Treasure && r.Theme == RoomTheme.Vault).Take(1).Select(r => ("vault", r)).ToList();
                foreach ((string label, LevelRoom room) in picks)
                {
                    List<Spot> spots = SpotsOf(level, room, label);
                    for (int i = 0; i < spots.Count; i++)
                    {
                        Spot s = spots[i];
                        _player.Teleport(s.At);
                        yield return Frames(4); // (WaitForEndOfFrame is never evoked in batch mode: the camera is rendered by hand)
                        RenderTexture before = cam.targetTexture;
                        cam.targetTexture = rt;
                        cam.Render();
                        // Work order 083: how long the camera takes to draw it here (CPU, batch run, ten draws).
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        for (int k = 0; k < 10; k++) cam.Render();
                        watch.Stop();
                        double renderMs = watch.Elapsed.TotalMilliseconds / 10.0;
                        cam.targetTexture = before;
                        RenderTexture was = RenderTexture.active;
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                        tex.Apply();
                        RenderTexture.active = was;
                        File.WriteAllBytes(Path.Combine(folder, s.Name + suffix + ".png"), tex.EncodeToPNG());
                        int tx = (i % 2) * W, ty = (1 - i / 2) * H;
                        sheet.SetPixels(tx, ty, W, H, tex.GetPixels());
                        (int other, int total) = InView(level, cam, level.RoomAt(_player.Position));
                        log.Add(string.Format(Inv, "{0}{1}: {2} {3} rect {4} at ({5:0.0}, {6:0.0}) in {7}; renderers drawn in view {8}, of rooms and passages more than a door away {9}; a draw {10:0.00} ms",
                                              s.Name, suffix, room.Kind, room.Letter + room.Order.ToString(Inv), room.Rect, s.At.x, s.At.z,
                                              level.RoomAt(_player.Position)?.Letter + "" + level.RoomAt(_player.Position)?.Order, total, other, renderMs));
                    }
                    for (int i = spots.Count; i < 4; i++) sheet.SetPixels((i % 2) * W, (1 - i / 2) * H, W, H, new Color[W * H]);
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "sheet-" + label + suffix + ".png"), sheet.EncodeToPNG());
                }
                }
            }
            File.WriteAllLines(Path.Combine(folder, "pictures.txt"), log);
            foreach (string l in log) Debug.Log("[cave pictures] " + l);
            Object.Destroy(tex);
            Object.Destroy(sheet);
            rt.Release();
        }
    }
}
