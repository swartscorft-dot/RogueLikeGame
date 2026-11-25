using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The eight large props in the game (work order 052; the concept pictures, Kristian's choice of 2026-11-20, "build toward
    /// them"), in the ControlRoom scene through <see cref="GeneratedLevels"/>: each loads from Resources with its material and
    /// glow, at the order's size, standing on the floor (the banner on a wall, the dais sunk to a low platform); on a floor the
    /// ones that block walking have colliders and no NavMesh where they stand, the dais and the banner none; the chest on a dais
    /// opens from the floor; a path goes from each door to each door of every room holding one; the lights stay inside the
    /// budget; with the switch off none of the eight is in the scene. Writes what Unity made of each ("LARGEIMPORT") and what a
    /// floor holds ("LARGEFLOOR"). Nothing here looks at the picture: the look is Kristian's to judge by playing.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames, and nothing hangs on a frame's length.
    /// </summary>
    public class LargePropsTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        const ulong Seed = 0x5EED5UL;

        /// <summary>The order's table: across (x), deep (Blender's y, the game's z) and high, in metres.</summary>
        static readonly Dictionary<PropModel, Vector3> s_table = new Dictionary<PropModel, Vector3>
        {
            [PropModel.KnightStatue] = new Vector3(1.38f, 3.00f, 1.40f), [PropModel.GreatAltar] = new Vector3(3.00f, 1.82f, 1.50f),
            [PropModel.Dais] = new Vector3(3.98f, 0.60f, 4.00f), [PropModel.Banner] = new Vector3(1.48f, 2.50f, 0.22f),
            [PropModel.GreatBrazier] = new Vector3(1.24f, 1.50f, 1.23f), [PropModel.CoinHeap] = new Vector3(2.00f, 0.63f, 1.84f),
            [PropModel.BoneWall] = new Vector3(3.00f, 2.50f, 0.50f), [PropModel.CandleCluster] = new Vector3(0.64f, 1.00f, 0.65f),
        };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        readonly List<Object> _made = new List<Object>();

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
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (Object o in _made)
                if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null)
            {
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel Build(int floor = 5, Action<LevelValues> change = null, ulong seed = Seed)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static string V(Vector3 v) => string.Format(s_Inv, "({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);

        static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static bool Glows(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m != null && m.IsKeywordEnabled("_EMISSION") && m.GetTexture("_EmissionMap") != null && m.GetColor("_EmissionColor").maxColorComponent > 0.01f;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ---------------- The eight as Unity imports them ----------------

        [Test]
        public void EachOfTheEight_Loads_WithItsMaterialAndGlow_AtTheTablesSize_StandingOnTheFloor_TheBannerOnAWall_TheDaisSunk()
        {
            var report = new StringBuilder();
            var faults = new List<string>();
            foreach (PropSize size in PropSize.Crypt.Where(s => s.Large))
            {
                string name = size.Name;
                GameObject source = Resources.Load<GameObject>(size.ResourcePath);
                if (source == null)
                {
                    faults.Add(name + " does not load from Resources/" + size.ResourcePath);
                    continue;
                }
                MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
                long triangles = 0;
                foreach (MeshFilter f in filters)
                    if (f.sharedMesh != null)
                        for (int s = 0; s < f.sharedMesh.subMeshCount; s++) triangles += f.sharedMesh.GetIndexCount(s) / 3;
                if (filters.Length != 1) faults.Add($"{name} has {filters.Length} meshes, not one");
                if (Resources.Load<Texture2D>(size.TexturePath) == null) faults.Add($"{name}'s texture does not load");
                if (Quaternion.Angle(source.transform.localRotation, Quaternion.identity) > 0.5f) faults.Add($"{name}'s root is turned");
                if ((source.transform.localScale - Vector3.one).magnitude > 1e-3f) faults.Add($"{name}'s root is scaled");

                // As it comes, at the origin: the order's size.
                GameObject raw = Object.Instantiate(source, Vector3.zero, source.transform.rotation);
                _made.Add(raw);
                Bounds b = WorldBounds(raw);
                Vector3 t = s_table[size.Model];
                if (Mathf.Abs(b.size.x - t.x) > 0.05f || Mathf.Abs(b.size.y - t.y) > 0.05f || Mathf.Abs(b.size.z - t.z) > 0.05f)
                    faults.Add($"{name} is {V(b.size)} as imported, the table {V(t)}");

                // As a level makes it, at a place, unturned.
                Material material = PropFactory.MakeMaterial(size.Model);
                _made.Add(material);
                var prop = LevelProp.Make(size.Model, new LevelPoint(0, 0), 0, PropPlace.Set);
                Vector3 at = new Vector3(-220f, 0f, 60f);
                GameObject built = PropFactory.Make(prop, at, null, 0, material, "Test_" + name);
                _made.Add(built);
                Renderer renderer = built.GetComponentInChildren<Renderer>();
                Bounds bb = WorldBounds(built);
                Vector3 local = bb.center - at;
                report.AppendFormat(s_Inv, "LARGEIMPORT {0}: bounds size {1} as imported (table {2}); as built min {3} max {4}; triangles {5}; footprint {6}; glows {7}; colliders {8}\n",
                                    name, V(b.size), V(t), V(bb.min - at), V(bb.max - at), triangles,
                                    size.Shape == PropShape.Circle ? "circle r " + size.Radius.ToString(s_Inv) : size.Width.ToString(s_Inv) + " x " + size.Depth.ToString(s_Inv),
                                    Glows(renderer), built.GetComponentsInChildren<Collider>().Length);

                if (renderer.sharedMaterial.GetTexture("_BaseMap") == null) faults.Add($"{name} as built has no painted texture");
                if (Glows(renderer) != size.Emission) faults.Add($"{name} as built {(Glows(renderer) ? "glows" : "does not glow")}, the table says {(size.Emission ? "it glows" : "not")}");
                if (size.Walkable)
                {
                    if (built.GetComponentsInChildren<Collider>().Length != 0) faults.Add($"{name} is walked over or under and has a collider");
                }
                else
                {
                    if (built.GetComponents<Collider>().Length != 1 || built.GetComponentsInChildren<Collider>().Length != 1) faults.Add($"{name} has not one collider, on the prop");
                    if (built.GetComponentInChildren<MeshCollider>() != null) faults.Add($"{name} has a mesh collider");
                }
                // Where it stands: on the floor, the banner hung from its top, the dais sunk so 0.15 m shows.
                float bottom = bb.min.y - at.y, top = bb.max.y - at.y;
                if (size.Model == PropModel.Banner)
                {
                    if (Mathf.Abs(top - (float)PropSize.BannerTop) > 0.03f) faults.Add($"the banner's top is at {top:0.###} m, not {PropSize.BannerTop}");
                    if (bottom < 0.3f) faults.Add($"the banner hangs down to {bottom:0.###} m");
                }
                else if (size.Model == PropModel.Dais)
                {
                    if (Mathf.Abs(top - (float)PropSize.DaisShows) > 0.03f) faults.Add($"the dais shows {top:0.###} m over the floor, not {PropSize.DaisShows}");
                }
                else if (Mathf.Abs(bottom) > 0.03f) faults.Add($"{name} as built stands {bottom:0.###} m off the floor");
                // Its footprint holds it; one whose back goes against a wall has its back at the footprint's back.
                double halfW = size.Shape == PropShape.Circle ? size.Radius : size.Width * 0.5, halfD = size.Shape == PropShape.Circle ? size.Radius : size.Depth * 0.5;
                if (Mathf.Abs(local.x) + bb.extents.x > halfW + 0.05 || Mathf.Abs(local.z) + bb.extents.z > halfD + 0.05)
                    faults.Add($"{name} as built reaches past its footprint: centre {V(local)} extents {V(bb.extents)}");
                if (size.PivotAtBack && Mathf.Abs((bb.min.z - at.z) + (float)halfD) > 0.03f) faults.Add($"{name}'s back is at {bb.min.z - at.z:0.###}, its footprint's at {-halfD}");
            }
            TestContext.WriteLine(report.ToString());
            Debug.Log(report.ToString());
            Assert.That(faults, Is.Empty, string.Join("\n", faults) + "\n" + report);
        }

        // ---------------- On a floor ----------------

        [UnityTest]
        public IEnumerator OnAFloor_TheBlockingOnesHaveColliders_AndNoNavMesh_TheDaisIsWalkedOver_AndTheBannerHangs()
        {
            // The eight include the vault's large coin heap, which the cave leaves out (work order 076, CaveValues.NoGoldHeaps:
            // Kristian, of the heap by the chest: "litt unødvendig"). The heap is still a prop of the game with that off, and
            // this test is about all eight on a floor: so the heaps are placed here. (Found by the full PlayMode run on the
            // morning's combination of 2026-11-22: with the heaps gone no floor holds all eight.)
            Action<LevelValues> heaps = v => v.CaveRules.NoGoldHeaps = false;
            heaps(_levels.Values);
            var seen = new Dictionary<PropModel, int>();
            // Two floors of the mood's seed, and the first floor 5 from seed 1 on that holds every one of the eight.
            // 2026-11-25: floor 5, not floor 3 as before. Work order 103 (generator version 20, LevelValues.ExitRoomsOfTheirOwn,
            // asked for: "tronrommet bare i etasje 5") ends floors 1 to 4 in an exit room of their own, so the throne room, the
            // only room that holds the great braziers (RoomMoodPlan's Throne), stands on floor 5 alone: no floor 3 of seeds 1 to
            // 199 holds a great brazier now (it was 161 of them at version 19), and the search found none. Floor 5 still holds
            // all eight (seed 3 the first under the .NET rules runner), so the test proves what it did.
            const int searchFloor = 5;
            var floors = new List<(int floor, ulong seed)> { (5, Seed), (1, Seed) };
            for (ulong s = 1; s < 200; s++)
            {
                RoomMoodPlan plan = RoomMoodPlan.Make(LevelGenerator.Generate(s, searchFloor, _levels.Values), _levels.Values);
                if (s_table.Keys.All(m => plan.Rooms.Any(r => r.LargeProps.Any(p => p.Model == m))))
                {
                    floors.Add((searchFloor, s));
                    break;
                }
            }
            Assert.That(floors.Count, Is.EqualTo(3), "a floor holding all eight");
            foreach ((int floor, ulong seed) in floors)
            {
                GeneratedLevel level = Build(floor, heaps, seed);
                yield return Frames(2);
                string where = "seed " + level.Layout.SeedText + " floor " + floor;
                Assert.That(level.LargePropsPlan, Is.Not.Null, where);
                Assert.That(level.LargeProps.Count, Is.EqualTo(level.LargePropsPlan.Rooms.Sum(r => r.LargeProps.Count)), "each large prop of the plan is built: " + where);
                Assert.That(level.Props.Any(p => level.Replaced.Contains(p.Prop)), Is.False, "what one stands in place of is not built: " + where);
                foreach (GeneratedLevel.PlacedProp placed in level.LargeProps)
                {
                    LevelProp p = placed.Prop;
                    PropSize size = PropSize.Of(p.Model);
                    string what = where + " " + placed.Object.name + " " + p;
                    seen.TryGetValue(p.Model, out int n);
                    seen[p.Model] = n + 1;
                    Transform t = placed.Object.transform;
                    Assert.That(t.IsChildOf(level.transform), what);
                    Assert.That(placed.Object.name, Does.StartWith("Prop_"), what);
                    Assert.That(Flat(t.position, level.ToWorld(p.Position)), Is.LessThan(0.001f), "at its place: " + what);
                    Assert.That(Mathf.DeltaAngle(t.eulerAngles.y, p.Rotation), Is.EqualTo(0f).Within(0.01f), "turned: " + what);
                    Assert.That(placed.Object.layer, Is.EqualTo(level.WallLayer), "on the walls' layer: " + what);
                    Renderer renderer = placed.Object.GetComponentInChildren<Renderer>();
                    Assert.That(renderer, Is.Not.Null, "the model is there: " + what);
                    Assert.That(Glows(renderer), Is.EqualTo(size.Emission), "glows when the table says: " + what);
                    Collider[] colliders = placed.Object.GetComponentsInChildren<Collider>();
                    if (size.Walkable)
                    {
                        Assert.That(colliders, Is.Empty, "walked over or under, no collider: " + what);
                        Assert.That(placed.Object.GetComponentInChildren<Cover>(), Is.Null, what);
                        Assert.That(placed.Object.GetComponentInChildren<Shelter>(), Is.Null, what);
                    }
                    else
                    {
                        Assert.That(colliders.Length, Is.EqualTo(1), "one collider: " + what);
                        Assert.That(placed.Object.GetComponent<Cover>() != null, Is.EqualTo(p.IsCover(level.Values.PropRules)), "cover when tall: " + what);
                        Assert.That(placed.Object.GetComponent<Shelter>(), Is.Not.Null, "shelter: " + what);
                        Assert.That(NavMesh.SamplePosition(level.ToWorld(p.Position), out _, 0.3f, NavMesh.AllAreas), Is.False, "no NavMesh where it stands: " + what);
                        Assert.That(renderer.bounds.min.y, Is.EqualTo(0f).Within(0.03f), "standing on the floor: " + what);
                    }
                    if (p.Model == PropModel.Banner)
                    {
                        Assert.That(renderer.bounds.max.y, Is.EqualTo((float)PropSize.BannerTop).Within(0.03f), "hung from its top: " + what);
                        Assert.That(renderer.bounds.min.y, Is.GreaterThan(0.3f), "off the floor: " + what);
                    }
                    if (p.Model == PropModel.Dais)
                    {
                        Assert.That(renderer.bounds.max.y, Is.EqualTo((float)PropSize.DaisShows).Within(0.03f), "0.15 m of it shows: " + what);
                        // Walked over: the NavMesh goes over it before what it carries.
                        LevelPoint f = p.Front;
                        var onTop = new LevelPoint(p.Position.X + f.X * 1.6, p.Position.Y + f.Y * 1.6);
                        Assert.That(NavMesh.SamplePosition(level.ToWorld(onTop), out NavMeshHit hit, 0.3f, NavMesh.AllAreas), "NavMesh on the dais: " + what);
                        Assert.That(hit.position.y, Is.EqualTo(0f).Within(0.1f), "at the floor's height: " + what);
                        // What it carries is drawn on its top, its collider where it was.
                        MoodRoom mr = level.LargePropsPlan.Of(placed.Room.Order); // work order 059: by order, a passage having no plan
                        foreach (LevelProp carried in mr.Raised)
                        {
                            GeneratedLevel.PlacedProp c = level.Props.First(x => x.Prop == carried);
                            Transform model = c.Object.transform.Find("Model");
                            Assert.That(model, Is.Not.Null, what);
                            Assert.That(model.localPosition.y, Is.EqualTo((float)PropSize.DaisShows).Within(1e-4f), "the " + carried.Model + " on its top: " + what);
                            Collider cc = c.Object.GetComponent<Collider>();
                            Assert.That(cc.bounds.min.y, Is.EqualTo(0f).Within(0.01f), "its collider on the floor: " + what);
                        }
                    }
                }
            }
            TestContext.WriteLine("LARGEFLOOR models seen: " + string.Join(", ", seen.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            foreach (PropModel m in s_table.Keys) Assert.That(seen.ContainsKey(m), m + " stands on one of the floors");
        }

        /// <summary>A point a metre into a room from a door's middle, on the NavMesh (as RoomMoodTests finds it).</summary>
        static Vector3 InsideDoor(GeneratedLevel level, LevelRoom room, LevelDoor d)
        {
            d.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            var mid = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            LevelPoint m = room.Middle(level.Layout.CellSize);
            double dx = m.X - mid.X, dy = m.Y - mid.Y;
            Vector3 p = level.ToWorld(new LevelPoint(mid.X + (d.Vertical ? Math.Sign(dx) : 0), mid.Y + (d.Vertical ? 0 : Math.Sign(dy))));
            return NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position : p;
        }

        [UnityTest]
        public IEnumerator APath_GoesFromEachDoorToEachDoor_OfEveryRoomHoldingALargeProp_AndToItsMiddle()
        {
            int rooms = 0, paths = 0;
            var path = new NavMeshPath();
            foreach (int floor in new[] { 1, 3, 5 })
            {
                GeneratedLevel level = Build(floor);
                yield return Frames(2);
                foreach (LevelRoom room in level.Layout.Rooms)
                {
                    if (!level.LargeProps.Any(p => p.Room == room)) continue;
                    rooms++;
                    List<LevelDoor> doors = level.Layout.DoorsOf(room.Order).ToList();
                    var ends = doors.Select(d => InsideDoor(level, room, d)).ToList();
                    if (room.Kind != RoomKind.Treasure && NavMesh.SamplePosition(level.Middle(room), out NavMeshHit middle, 1f, NavMesh.AllAreas)) ends.Add(middle.position);
                    for (int i = 0; i < ends.Count; i++)
                        for (int j = i + 1; j < ends.Count; j++)
                        {
                            paths++;
                            Assert.That(NavMesh.CalculatePath(ends[i], ends[j], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                        "floor " + floor + " " + room.Letter + room.Order + " (" + RoomThemes.Word(room.Theme) + "): a whole path " + i + "-" + j + "\n" + LevelMap.Draw(level.Layout));
                        }
                }
            }
            TestContext.WriteLine("LARGEFLOOR paths: " + paths + " whole paths in " + rooms + " rooms holding large props (floors 1, 3, 5 of seed " + LevelSeed.ToText(Seed) + ")");
            Assert.That(rooms, Is.GreaterThan(5));
        }

        // ---------------- The chest ----------------

        [UnityTest]
        public IEnumerator TheChestOnADais_OpensFromTheFloor()
        {
            // A run whose floor 1 holds a treasure room with the chest on the dais model.
            ulong run = 0;
            LevelRoom found = null;
            for (ulong r = 0x12345678UL; r < 0x12345678UL + 40 && found == null; r++)
            {
                LevelLayout layout = LevelGenerator.Generate(RunSeeds.FloorSeed(r, 1), 1, _levels.Values);
                RoomMoodPlan plan = RoomMoodPlan.Make(layout, _levels.Values);
                MoodRoom mr = plan.Rooms.FirstOrDefault(m => m.Room.Kind == RoomKind.Treasure && m.LargeProps.Any(p => p.Model == PropModel.Dais));
                if (mr == null) continue;
                run = r;
                found = mr.Room;
            }
            Assert.That(found, Is.Not.Null, "a run with a chest on a dais on floor 1");
            GeneratedLevel level = _levels.StartRun(run);
            yield return Frames(2);
            GeneratedLevel.PlacedProp chest = _levels.Chests().First(c => c.Room.Order == found.Order);
            LevelProp dais = level.LargeProps.First(p => p.Room.Order == found.Order && p.Prop.Model == PropModel.Dais).Prop;
            Assert.That(level.LargePropsPlan.Of(found.Order).Raised, Does.Contain(chest.Prop), "the chest stands on the dais");

            // She walks up to its front over the dais: from there it is in reach and opens.
            LevelPoint f = chest.Prop.Front;
            double off = chest.Prop.Depth * 0.5 + 0.6;
            var stand = new LevelPoint(chest.Prop.Position.X + f.X * off, chest.Prop.Position.Y + f.Y * off);
            Assert.That(dais.Footprint.DistanceTo(stand), Is.EqualTo(0).Within(1e-9), "the place before it is on the dais");
            Assert.That(NavMesh.SamplePosition(level.ToWorld(stand), out NavMeshHit hit, 0.3f, NavMesh.AllAreas), "and walkable");
            var path = new NavMeshPath();
            Assert.That(NavMesh.SamplePosition(level.PlayerStart, out NavMeshHit start, 1f, NavMesh.AllAreas), "NavMesh at the start");
            Assert.That(NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "she can walk there from the entrance");
            Assert.That(_levels.ChestInReach(), Is.Null, "not from the entrance");
            _player.Teleport(hit.position);
            yield return Frames(2);
            Assert.That(_levels.ChestInReach(), Is.SameAs(chest), "within reach from the floor before it");
            Assert.That(_levels.OpenChest(), Is.True, "it opens");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
        }

        // ---------------- The lights ----------------

        static int LightsOn(GeneratedLevel level) =>
            Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled && l.type != LightType.Directional
                && (l.transform.root == level.transform || (level.Mood != null && level.Mood.Root != null && l.transform.root == level.Mood.Root.transform)));

        [UnityTest]
        public IEnumerator TheSourcesLights_StayInsideTheBudget_TheLargePropsAmongTheSources()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            RoomMood mood = level.Mood;
            Assert.That(mood, Is.Not.Null);
            Assert.That(mood.Plan, Is.SameAs(level.LargePropsPlan), "the mood is built from the plan the large props came from");
            int budget = level.Values.Crypt.LightsPerRoom + level.Values.Signs.LightsMost + 1;
            int large = 0, mostOn = 0;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                RoomMood.RoomParts parts = mood.Of(room.Order); // work order 059: a passage has no parts of its own
                if (parts == null)
                {
                    Assert.That(room.Kind, Is.EqualTo(RoomKind.Passage));
                    continue;
                }
                foreach (GeneratedLevel.PlacedProp p in level.LargeProps.Where(p => p.Room == room))
                {
                    if (p.Prop.Model == PropModel.Dais) continue; // its middle is the chest's, whose gold it carries
                    bool source = p.Prop.Model == PropModel.GreatBrazier || p.Prop.Model == PropModel.GreatAltar || p.Prop.Model == PropModel.CandleCluster;
                    Assert.That(parts.Sources.Any(s => s.OfProp && s.At.DistanceTo(p.Prop.Position) < 1e-6), Is.EqualTo(source), "a source when it glows warm or teal: " + p.Prop);
                    if (source) large++;
                }
                _player.Teleport(level.Middle(room));
                yield return Frames(3);
                int on = LightsOn(level);
                mostOn = Mathf.Max(mostOn, on);
                Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
                Assert.That(parts.Lights.Count, Is.LessThanOrEqualTo(level.Values.Crypt.LightsPerRoom), "the room's real lights inside the kit's budget");
            }
            TestContext.WriteLine("LARGEFLOOR lights floor 5: " + large + " large props among the sources; at most " + mostOn + " lights on at once (budget " + budget + ")");
            Assert.That(large, Is.GreaterThan(0));
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_NoneOfTheEightIsInTheScene_AndEveryPropOfTheDescriptionIsBuilt()
        {
            var meshes = new HashSet<Mesh>();
            foreach (PropSize s in PropSize.Crypt.Where(s => s.Large))
            {
                GameObject source = Resources.Load<GameObject>(s.ResourcePath);
                Assert.That(source, Is.Not.Null, s.Name);
                meshes.Add(source.GetComponentInChildren<MeshFilter>().sharedMesh);
            }
            foreach (int floor in new[] { 1, 5 })
            {
                GeneratedLevel off = Build(floor, v => v.LargeProps = false);
                yield return Frames(2);
                string where = "floor " + floor;
                Assert.That(off.LargePropsPlan, Is.Null, where);
                Assert.That(off.LargeProps, Is.Empty, where);
                Assert.That(off.Replaced, Is.Empty, where);
                Assert.That(off.Props.Count, Is.EqualTo(off.Layout.Rooms.Sum(r => r.Props.Count)), "every prop of the description: " + where);
                Assert.That(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(f => meshes.Contains(f.sharedMesh)), Is.False, "none of the eight in the scene: " + where);
                Assert.That(off.Mood.Plan.Rooms.All(r => r.LargeProps.Count == 0), where);
                Assert.That(off.Mood.Plan.Rooms.Any(r => r.Pieces.Any(c => c.Kind == CompositionKind.Dais)), "work order 044's code-made dais: " + where);
                Assert.That(off.GetComponentsInChildren<Collider>().Length, Is.EqualTo(off.Floors + off.Walls + off.Props.Count), where);

                GeneratedLevel on = Build(floor);
                yield return Frames(2);
                Assert.That(on.LargeProps.Count, Is.GreaterThan(0), "on, they are: " + where);
                Assert.That(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Any(f => meshes.Contains(f.sharedMesh)), where);
            }
        }
    }
}
