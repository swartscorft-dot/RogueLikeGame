using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// A generated floor dressed in the crypt kit (work order 016, 2026-11-20), in the ControlRoom scene through the
    /// intents of <see cref="GeneratedLevels"/>: every full-height wall cell has exactly one wall section on the box's
    /// line and every low one a low section; every door on a full line has an arch and none on a low line; piers and
    /// posts stand on grid points, one a point; no piece has a collider or stands on a layer anything asks; the wall boxes
    /// keep their colliders, cover and shelter and stop showing; the NavMesh is the same as without the kit and every
    /// room can be reached; the floors show their kind's texture a copy a cell; the same seed gives the same niches and
    /// lanterns; the lights, when on, light only her room; and with the switch off the boxes show as before. Writes what
    /// a floor costs ("KITCOST").
    /// </summary>
    public class CryptKitLevelTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly ulong[] Seeds = { 42UL, 7UL };
        static readonly int[] FloorNumbers = { 1, 5 };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        readonly List<GameObject> _made = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject go in _made)
                if (go != null) Object.Destroy(go);
            _made.Clear();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        GeneratedLevel Build(ulong seed, int floor, Action<LevelValues> change = null)
        {
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        static bool Near(float a, float b, float within = 1e-3f) => Mathf.Abs(a - b) <= within;

        /// <summary>A wall box's line and cells, read from where it stands and how long it is.</summary>
        struct BoxCells
        {
            public bool Vertical, Low;
            public int Line, From, Count;
            public GameObject Box;
        }

        static BoxCells CellsOf(GeneratedLevel level, GameObject box)
        {
            float cs = level.Layout.CellSize, t = level.Values.WallThickness;
            Vector3 s = box.transform.localScale;
            bool vertical = s.x < s.z;
            float along = vertical ? s.z : s.x;
            LevelPoint c = level.ToLevel(box.transform.position);
            int count = Mathf.RoundToInt((along - t) / cs);
            double mid = vertical ? c.Y : c.X;
            return new BoxCells
            {
                Vertical = vertical, Low = s.y < level.Values.WallHeight - 1e-3f, Box = box, Count = count,
                Line = (int)Math.Round((vertical ? c.X : c.Y) / cs), From = (int)Math.Round((mid - count * cs * 0.5) / cs),
            };
        }

        static string Where(GeneratedLevel level) => "seed " + level.Layout.SeedText + " floor " + level.Layout.Floor;

        int _archesSeen, _lowDoorsSeen;

        [UnityTest]
        public IEnumerator KitOn_EveryWallCellHasOneSection_EveryFullDoorAnArch_PiersAndPostsOnePerPoint()
        {
            _archesSeen = _lowDoorsSeen = 0;
            foreach (ulong seed in Seeds)
                foreach (int floor in FloorNumbers)
                {
                    GeneratedLevel level = Build(seed, floor);
                    yield return null;
                    AssertDressed(level);
                }
            Assert.That(_archesSeen, Is.GreaterThan(0), "doors on vertical lines, with arches");
            Assert.That(_lowDoorsSeen, Is.GreaterThan(0), "and on horizontal ones, without");
        }

        void AssertDressed(GeneratedLevel level)
        {
            LevelLayout layout = level.Layout;
            string where = Where(level);
            float cs = layout.CellSize;
            Assert.That(level.KitRoot, Is.Not.Null, where);
            Assert.That(level.KitPlan, Is.Not.Null);
            Assert.That(level.KitPieces.All(p => p.Object != null), "every piece of the plan was built: " + where);

            // Sections: one per wall cell, on the box's line, low for low.
            var sections = new Dictionary<(bool, int, int), List<GeneratedLevel.KitPiece>>();
            foreach (GeneratedLevel.KitPiece p in level.KitPieces.Where(p => p.Kind == CryptPiece.Wall || p.Kind == CryptPiece.WallNiche || p.Kind == CryptPiece.LowWall))
            {
                CryptKitSection s = level.KitPlan.Sections[p.Index];
                var key = (s.Vertical, s.Line, s.Cell);
                if (!sections.TryGetValue(key, out List<GeneratedLevel.KitPiece> list)) sections[key] = list = new List<GeneratedLevel.KitPiece>();
                list.Add(p);
            }
            int cells = 0;
            var fullEnds = new HashSet<(int, int)>();
            var lowEnds = new HashSet<(int, int)>();
            foreach (GameObject box in level.WallBoxes)
            {
                BoxCells b = CellsOf(level, box);
                for (int k = 0; k < b.Count; k++)
                {
                    int cell = b.From + k;
                    cells++;
                    string what = (b.Vertical ? "V" : "H") + b.Line + "_" + cell + " of " + box.name + " " + where;
                    Assert.That(sections.TryGetValue((b.Vertical, b.Line, cell), out List<GeneratedLevel.KitPiece> here) ? here.Count : 0, Is.EqualTo(1), "one section at " + what);
                    GeneratedLevel.KitPiece piece = here[0];
                    Assert.That(piece.Kind == CryptPiece.LowWall, Is.EqualTo(b.Low), "a low section on a low box, a full one on a full box: " + what);
                    Vector3 at = piece.Object.transform.position;
                    if (b.Vertical) Assert.That(Near(at.x, box.transform.position.x), "on the box's line: " + what);
                    else Assert.That(Near(at.z, box.transform.position.z), "on the box's line: " + what);
                    LevelPoint l = level.ToLevel(at);
                    Assert.That(Near((float)(b.Vertical ? l.Y : l.X), (cell + 0.5f) * cs), "in the middle of its cell: " + what);
                    Assert.That(Near(at.y, 0f), "on the floor: " + what);
                    var ends = b.Low ? lowEnds : fullEnds;
                    ends.Add(b.Vertical ? (b.Line, cell) : (cell, b.Line));
                    ends.Add(b.Vertical ? (b.Line, cell + 1) : (cell + 1, b.Line));
                }
            }
            Assert.That(sections.Values.Sum(x => x.Count), Is.EqualTo(cells), "no section where no box is: " + where);
            Assert.That(level.KitPieces.Count(p => p.Kind == CryptPiece.WallNiche), Is.EqualTo(level.KitPlan.Niches));

            // Arches: in every door cell of a full line, none on a low one.
            List<GeneratedLevel.KitPiece> arches = level.KitPieces.Where(p => p.Kind == CryptPiece.Arch).ToList();
            int wanted = 0;
            foreach (LevelDoor d in layout.Doors)
            {
                // An arch is full height: its two ends are a full piece's ends, a pier's points. Work order 056 (the first
                // Unity run of the night's combination, 2026-11-21): before work order 051 a full wall box always went on
                // along the line past a door's end, so the boxes alone found them; now the cells either side of that line
                // past it may both be carved (a gate's and the next room's corner), no box goes on, only low faces meet the
                // arch there (seed 2222223C floor 1, door 1-2 on line x = 32, its end at (32, 21)).
                if (d.Vertical || layout.RoomAtCell(d.From, d.Line) == null)
                {
                    fullEnds.Add(d.Vertical ? (d.Line, d.From) : (d.From, d.Line));
                    fullEnds.Add(d.Vertical ? (d.Line, d.From + d.Width) : (d.From + d.Width, d.Line));
                }
                for (int cell = d.From; cell < d.From + d.Width; cell++)
                {
                    LevelPoint middle = d.Vertical ? new LevelPoint(d.Line * cs, (cell + 0.5) * cs) : new LevelPoint((cell + 0.5) * cs, d.Line * cs);
                    Vector3 w = level.ToWorld(middle);
                    int here = arches.Count(a => Vector3.Distance(a.Object.transform.position, w) < 1e-3f);
                    int near = arches.Count(a => Vector3.Distance(a.Object.transform.position, w) < 1f);
                    // A door's line is full when the cell north of a horizontal door has no room: never, since a door joins two rooms.
                    bool full = d.Vertical || layout.RoomAtCell(cell, d.Line) == null;
                    Assert.That(here, Is.EqualTo(full ? 1 : 0), (full ? "an arch" : "no arch") + " in door " + d.RoomA + "-" + d.RoomB + " " + where);
                    Assert.That(near, Is.EqualTo(full ? 1 : 0));
                    if (full) wanted++;
                }
            }
            Assert.That(arches.Count, Is.EqualTo(wanted));
            _archesSeen += arches.Count;
            _lowDoorsSeen += layout.Doors.Count(d => !d.Vertical);

            // Piers and posts: on grid points, one a point; a pier where a full box or an arch ends or meets, a post where only low ones do.
            var points = new Dictionary<(int, int), GeneratedLevel.KitPiece>();
            foreach (GeneratedLevel.KitPiece p in level.KitPieces.Where(p => p.Kind == CryptPiece.Pier || p.Kind == CryptPiece.Post))
            {
                LevelPoint l = level.ToLevel(p.Object.transform.position);
                int gx = (int)Math.Round(l.X / cs), gy = (int)Math.Round(l.Y / cs);
                Assert.That(Near((float)l.X, gx * cs) && Near((float)l.Y, gy * cs), "on a grid point: " + p.Object.name + " " + where);
                Assert.That(points.ContainsKey((gx, gy)), Is.False, "one a point: " + p.Object.name + " " + where);
                points[(gx, gy)] = p;
            }
            foreach ((int, int) e in fullEnds)
                Assert.That(points.TryGetValue(e, out GeneratedLevel.KitPiece q) && q.Kind == CryptPiece.Pier, "a pier at " + e + " " + where);
            foreach ((int, int) e in lowEnds)
            {
                Assert.That(points.TryGetValue(e, out GeneratedLevel.KitPiece q), "a piece at the end of a low wall " + e + " " + where);
                if (!fullEnds.Contains(e)) Assert.That(q.Kind, Is.EqualTo(CryptPiece.Post), "a post where only low walls are: " + e + " " + where);
            }

            // Lanterns: at their height on a plain full section's face toward a room, never at a niche or next to a door.
            foreach (GeneratedLevel.KitPiece p in level.KitPieces.Where(p => p.Kind == CryptPiece.Lantern))
            {
                CryptKitLantern l = level.KitPlan.Lanterns[p.Index];
                CryptKitSection s = level.KitPlan.Sections[l.Section];
                Assert.That(s.Low || s.Niche || s.AtDoor, Is.False, p.Object.name);
                Vector3 at = p.Object.transform.position;
                Assert.That(at.y, Is.EqualTo(level.Values.Crypt.LanternHeight).Within(1e-3f));
                Vector3 into = s.Vertical ? new Vector3(l.Side, 0f, 0f) : new Vector3(0f, 0f, l.Side);
                LevelRoom room = level.RoomAt(at + into * 0.3f);
                Assert.That(room, Is.Not.Null, "a lantern hangs in a room: " + p.Object.name + " " + where);
                Assert.That(room.Order, Is.EqualTo(l.Room));
                Renderer r = p.Object.GetComponentInChildren<Renderer>();
                Assert.That(Vector3.Dot(r.bounds.center - at, into), Is.GreaterThan(0.1f), "it reaches into the room: " + p.Object.name);
            }
            Assert.That(level.KitPieces.Count(p => p.Kind == CryptPiece.Lantern), Is.GreaterThan(0), where);

            // Nothing of the kit can be hit, walked on or hidden behind.
            Assert.That(level.KitRoot.GetComponentsInChildren<Collider>(true), Is.Empty, "no kit piece has a collider: " + where);
            Assert.That(level.KitRoot.GetComponentsInChildren<Cover>(true), Is.Empty);
            Assert.That(level.KitRoot.GetComponentsInChildren<Shelter>(true), Is.Empty);
            Assert.That(level.KitRoot.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == CryptKit.Layer), "every piece on Ignore Raycast");
            Assert.That((Physics.DefaultRaycastLayers & (1 << CryptKit.Layer)) == 0, "which the default raycasts leave out");
            Assert.That(CryptKit.Layer, Is.Not.EqualTo(level.WallLayer), "and no cover stands on");
            // Changed deliberately (work order 052): and the large props that block walking.
            Assert.That(level.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(level.Floors + level.Walls + level.Props.Count + level.LargeProps.Count(p => !PropSize.Of(p.Prop.Model).Walkable)),
                        "floors, walls and props have colliders, nothing else");

            // The boxes are as they were and do not show.
            foreach (GameObject box in level.WallBoxes)
            {
                Assert.That(box.GetComponent<BoxCollider>() != null && box.GetComponent<BoxCollider>().enabled, box.name);
                Assert.That(box.GetComponent<Cover>(), Is.Not.Null, box.name);
                Assert.That(box.GetComponent<Shelter>(), Is.Not.Null, box.name);
                Assert.That(box.layer, Is.EqualTo(level.WallLayer));
                Assert.That(box.GetComponent<Renderer>().enabled, Is.False, "a box does not show: " + box.name);
            }
            Assert.That(level.WallBoxes.Count, Is.EqualTo(level.Walls));

            // Every room can still be reached.
            Vector3 Ground(Vector3 p)
            {
                Assert.That(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas), "NavMesh at " + p + " " + where + "\n" + LevelMap.Draw(layout));
                return hit.position;
            }
            Vector3 start = Ground(level.PlayerStart);
            var path = new NavMeshPath();
            foreach (LevelRoom r in layout.Rooms)
                Assert.That(NavMesh.CalculatePath(start, Ground(level.Middle(r)), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                            "the whole way to " + r.Letter + r.Order + " " + where);
        }

        [UnityTest]
        public IEnumerator TheNavMesh_IsTheSame_WithTheKitOnAndOff()
        {
            foreach (int floor in FloorNumbers)
            {
                Build(42UL, floor, v => v.CryptKit = false);
                yield return null;
                NavMeshTriangulation off = NavMesh.CalculateTriangulation();
                Build(42UL, floor);
                yield return null;
                NavMeshTriangulation on = NavMesh.CalculateTriangulation();
                Assert.That(on.vertices.Length, Is.GreaterThan(0));
                Assert.That(on.vertices, Is.EqualTo(off.vertices), "the same NavMesh, point for point, floor " + floor);
                Assert.That(on.indices, Is.EqualTo(off.indices));
                Assert.That(on.areas, Is.EqualTo(off.areas));
            }
        }

        [UnityTest]
        public IEnumerator TheFloors_ShowTheirKindsTexture_ACopyACell_FaintlyTinted()
        {
            GeneratedLevel level = Build(42UL, 1);
            yield return null;
            LevelLayout layout = level.Layout;
            Assert.That(layout.Rooms.Select(r => r.Kind).Distinct().Count(), Is.EqualTo(5), "every kind of room on this floor (work order 059: the passages too, on the combat rooms' floor)");
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                LevelRoom room = layout.Rooms[i];
                GameObject floor = level.FloorBoxes[i];
                string what = floor.name;
                Renderer r = floor.GetComponent<Renderer>();
                Assert.That(r.enabled, what);
                Material m = r.sharedMaterial;
                Assert.That(m.GetTexture("_BaseMap").name, Is.EqualTo("Crypt_Floor_" + CryptKit.FloorLetter(room.Kind) + "_base_color"), what);
                Assert.That(CryptKit.FloorLetter(room.Kind), Is.EqualTo(room.Kind == RoomKind.Treasure ? 'C' : room.Kind == RoomKind.Exit ? 'B' : 'A'));
                Color tint = m.GetColor("_BaseColor");
                Assert.That(Mathf.Min(tint.r, tint.g, tint.b), Is.GreaterThan(0.85f), "a faint tint: " + what);
                Assert.That(floor.GetComponent<BoxCollider>().enabled, what);

                // The top face: its texture coordinates, tiled, change by one every 4 m in the world and are whole at its corners.
                r.GetPropertyBlock(block);
                Vector4 st = block.GetVector("_BaseMap_ST");
                Mesh mesh = floor.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.isReadable, "the cube's mesh can be read");
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                Vector2[] uv = mesh.uv;
                var top = Enumerable.Range(0, v.Length).Where(k => n[k].y > 0.9f).ToList();
                Assert.That(top.Count, Is.EqualTo(4));
                foreach (int a in top)
                    foreach (int b in top)
                    {
                        Vector3 wa = floor.transform.TransformPoint(v[a]), wb = floor.transform.TransformPoint(v[b]);
                        Vector2 ua = new Vector2(uv[a].x * st.x + st.z, uv[a].y * st.y + st.w), ub = new Vector2(uv[b].x * st.x + st.z, uv[b].y * st.y + st.w);
                        Vector2 du = ub - ua;
                        Vector2 dw = new Vector2(wb.x - wa.x, wb.z - wa.z) / level.Values.Crypt.FloorTile;
                        Assert.That(du.magnitude, Is.EqualTo(dw.magnitude).Within(1e-3f), "a copy every 4 m: " + what);
                        Assert.That(Mathf.Abs(ua.x - Mathf.Round(ua.x)) + Mathf.Abs(ua.y - Mathf.Round(ua.y)), Is.LessThan(1e-3f), "whole at the corners, on the grid: " + what);
                    }
            }
        }

        [UnityTest]
        public IEnumerator TheSameSeed_GivesTheSameNichesAndLanterns()
        {
            GeneratedLevel first = Build(7UL, 3);
            yield return null;
            string Pieces(GeneratedLevel l) => string.Join(";", l.KitPieces.Select(p => p.Kind + " " + p.Object.name + " " + p.Object.transform.position.ToString("F3") + " "
                                                                                          + p.Object.transform.rotation.eulerAngles.ToString("F1")));
            string plan = first.KitPlan.Describe(), pieces = Pieces(first);
            int niches = first.KitPlan.Niches, lanterns = first.KitPlan.Lanterns.Count;
            Assert.That(niches, Is.GreaterThan(0));
            Assert.That(lanterns, Is.GreaterThan(0));
            List<Material> materials = first.KitPieces.Select(p => p.Object.GetComponentInChildren<Renderer>().sharedMaterial).Distinct().ToList();
            Assert.That(materials.Count, Is.EqualTo(first.KitPieces.Select(p => p.Kind).Distinct().Count()), "one material a model");
            Assert.That(first.KitPieces.GroupBy(p => p.Kind).All(g => g.Select(p => p.Object.GetComponentInChildren<MeshFilter>().sharedMesh).Distinct().Count() == 1), "one mesh a model");

            GeneratedLevel again = _levels.Rebuild();
            yield return null;
            Assert.That(again.KitPlan.Describe(), Is.EqualTo(plan));
            Assert.That(Pieces(again), Is.EqualTo(pieces), "the same pieces in the same places");
            Assert.That(materials.All(m => m == null), "the old floor's kit materials went with it");

            GeneratedLevel other = Build(8UL, 3);
            yield return null;
            Assert.That(other.KitPlan.Describe(), Is.Not.EqualTo(plan));
        }

        [UnityTest]
        public IEnumerator SwitchOff_NoKit_AndTheBoxesShowAsBefore()
        {
            GeneratedLevel level = Build(42UL, 1, v => v.CryptKit = false);
            yield return null;
            Assert.That(level.KitRoot, Is.Null);
            Assert.That(level.KitPlan, Is.Null);
            Assert.That(level.KitPieces, Is.Empty);
            Assert.That(level.transform.Find("CryptKit"), Is.Null);
            Assert.That(level.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Kit_")), Is.EqualTo(0));
            Assert.That(level.WallBoxes.All(b => b.GetComponent<Renderer>().enabled), "every box shows");
            Color[] tints = { GeneratedLevel.StartTint, GeneratedLevel.CombatTint, GeneratedLevel.RewardTint, GeneratedLevel.BossTint,
                              GeneratedLevel.EliteTint }; // the elite room's since work order 017
            foreach (GameObject f in level.FloorBoxes)
            {
                Color c = f.GetComponent<Renderer>().sharedMaterial.color;
                Assert.That(tints.Any(t => Mathf.Abs(t.r - c.r) + Mathf.Abs(t.g - c.g) + Mathf.Abs(t.b - c.b) < 1e-3f), "a grey-box tint: " + f.name);
            }
            Assert.That(level.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(level.Floors + level.Walls + level.Props.Count + level.LargeProps.Count(p => !PropSize.Of(p.Prop.Model).Walkable))); // changed deliberately (work order 052)
        }

        [UnityTest]
        public IEnumerator LanternLights_WhenOn_AtMostTwoARoom_NoShadows_OnlyHerRoomLit()
        {
            GeneratedLevel level = Build(42UL, 1, v => v.Crypt.LanternLights = true);
            yield return null;
            yield return null;
            CryptKitLights lights = level.KitLights;
            Assert.That(lights, Is.Not.Null);
            Assert.That(lights.ByRoom.Count, Is.GreaterThan(1));
            foreach (KeyValuePair<int, List<Light>> pair in lights.ByRoom)
            {
                Assert.That(pair.Value.Count, Is.InRange(1, 2), "room " + pair.Key);
                foreach (Light l in pair.Value)
                {
                    Assert.That(l.type, Is.EqualTo(LightType.Point));
                    Assert.That(l.shadows, Is.EqualTo(LightShadows.None));
                    Assert.That(l.color.g, Is.GreaterThan(l.color.r), "cyan");
                    Assert.That(l.GetComponent<Collider>(), Is.Null);
                    Assert.That(level.RoomAt(l.transform.position)?.Order, Is.EqualTo(pair.Key), "in its room");
                }
            }
            void AssertLit(int room)
            {
                Assert.That(lights.LitRoom, Is.EqualTo(room));
                foreach (KeyValuePair<int, List<Light>> pair in lights.ByRoom)
                    foreach (Light l in pair.Value)
                        Assert.That(l.enabled, Is.EqualTo(pair.Key == room), "room " + pair.Key + "'s light, she is in " + room);
            }
            AssertLit(0);
            LevelRoom combat = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && lights.ByRoom.ContainsKey(r.Order));
            _player.Teleport(level.Middle(combat));
            yield return null;
            yield return null;
            AssertLit(combat.Order);

            // Off by default: none.
            GeneratedLevel plain = Build(42UL, 1);
            yield return null;
            Assert.That(plain.KitLights, Is.Null);
            Assert.That(plain.GetComponentsInChildren<Light>(true), Is.Empty);
        }

        /// <summary>
        /// What a floor costs with the kit on and off, floors 1 and 5 of one seed, the level alone (no enemies): objects
        /// under its root, triangles of what shows, and the build's time (the middle of three, after one to load the models).
        /// </summary>
        [UnityTest]
        public IEnumerator Cost_OfAFloor_WithTheKitOnAndOff()
        {
            var report = new StringBuilder();
            foreach (int floor in FloorNumbers)
            {
                LevelLayout layout = LevelGenerator.Generate(42UL, floor, new LevelValues());
                var costs = new Dictionary<bool, (int objects, long triangles, double ms, int pieces)>();
                foreach (bool kit in new[] { true, false })
                {
                    var values = new LevelValues { CryptKit = kit };
                    var times = new List<double>();
                    int objects = 0, pieces = 0;
                    long triangles = 0;
                    for (int run = 0; run < 4; run++)
                    {
                        var watch = Stopwatch.StartNew();
                        GeneratedLevel level = GeneratedLevel.Build(layout, values, null, null);
                        watch.Stop();
                        if (run > 0) times.Add(watch.Elapsed.TotalMilliseconds);
                        objects = level.GetComponentsInChildren<Transform>(true).Length;
                        pieces = level.KitPieces.Count;
                        triangles = 0;
                        foreach (MeshRenderer r in level.GetComponentsInChildren<MeshRenderer>())
                        {
                            if (!r.enabled) continue;
                            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                            for (int s = 0; s < mesh.subMeshCount; s++) triangles += mesh.GetIndexCount(s) / 3;
                        }
                        level.gameObject.SetActive(false);
                        Object.Destroy(level.gameObject);
                        yield return null;
                    }
                    times.Sort();
                    costs[kit] = (objects, triangles, times[times.Count / 2], pieces);
                    report.AppendFormat(s_Inv, "KITCOST seed {0} floor {1} kit {2}: objects {3} (kit pieces {4}), triangles {5}, build {6:0.0} ms (runs {7})\n",
                                        layout.SeedText, floor, kit ? "on" : "off", objects, pieces, triangles, times[times.Count / 2],
                                        string.Join(", ", times.Select(t => t.ToString("0.0", s_Inv))));
                }
                Assert.That(costs[true].objects, Is.GreaterThan(costs[false].objects));
                Assert.That(costs[false].pieces, Is.EqualTo(0));
            }
            Debug.Log(report.ToString());
            TestContext.WriteLine(report.ToString());
        }
    }
}
