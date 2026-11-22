using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The room as a cave (work order 076; Kristian, 2026-11-21 in the evening: "jeg liker heller ikke at vi ser andre zoner når
    /// vi er i et rom. veggene må gå helt opp og føles at vi er i en hule"), built, in the ControlRoom scene through the intents
    /// of <see cref="GeneratedLevels"/>: in every room and passage only it and those a door away are drawn, every other
    /// renderer of the floor and its mood and every enemy standing there not, and no light of theirs is on; walking from the
    /// entrance through a passage the room ahead is drawn before she is in it; the hidden enemies' logic is what it is with the
    /// cave off; the rock by the values, without collider or shadow, the kit's pieces, the lights, the colliders and the NavMesh
    /// as with the cave off; the vault without its large coin heaps and its chest opened as before; the earth's black behind
    /// the floor and the scene's colour back when it goes; with the switch off nothing of it; its frame allocates nothing.
    /// Writes what it costs ("CAVECOST").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames or game seconds, never a frame's length.
    /// </summary>
    public class CaveTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;

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
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
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

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        GeneratedLevel Build(ulong seed, int floor, Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        GeneratedLevel StartRun(Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static Vector3 CellMiddle(GeneratedLevel level, int x, int y) =>
            level.ToWorld(new LevelPoint((x + 0.5) * level.Layout.CellSize, (y + 0.5) * level.Layout.CellSize));

        static Vector3 InPassage(GeneratedLevel level, LevelRoom passage)
        {
            List<(int x, int y)> path = Passages.PathOf(level.Layout, passage);
            (int x, int y) c = path[path.Count / 2];
            return CellMiddle(level, c.x, c.y);
        }

        /// <summary>An open cell's middle of the room, the nearest to its middle (a template's middle may be carved).</summary>
        static Vector3 InRoom(GeneratedLevel level, LevelRoom room)
        {
            if (room.Kind == RoomKind.Passage) return InPassage(level, room);
            double cs = level.Layout.CellSize;
            LevelPoint m = room.Middle(cs);
            if (level.Layout.RoomAt(m.X, m.Y) == room) return level.ToWorld(m);
            Vector3 best = level.ToWorld(m);
            double most = double.MaxValue;
            for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                for (int x = room.Rect.X; x < room.Rect.Right; x++)
                {
                    if (!room.IsOpen(x, y)) continue;
                    double d = Math.Abs((x + 0.5) * cs - m.X) + Math.Abs((y + 0.5) * cs - m.Y);
                    if (d < most) { most = d; best = CellMiddle(level, x, y); }
                }
            return best;
        }

        IEnumerator Into(GeneratedLevel level, LevelRoom room)
        {
            _player.Teleport(InRoom(level, room));
            yield return Until(() => _levels.CurrentRoom == room, 30);
            yield return Frames(2);
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        static IEnumerable<Renderer> FloorRenderers(GeneratedLevel level)
        {
            foreach (Renderer r in level.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponentInParent<Enemy>(true) == null) yield return r;
            if (level.Mood != null && level.Mood.Root != null)
                foreach (RoomMood.RoomParts p in level.Mood.Rooms)
                    if (p.Root != null)
                        foreach (Renderer r in p.Root.GetComponentsInChildren<Renderer>(true)) yield return r;
        }

        static bool[] ShownFor(GeneratedLevel level, int here)
        {
            var shown = new bool[level.Layout.Rooms.Count];
            CaveSight.Shown(level.Layout, here, shown);
            return shown;
        }

        // ---------------- What is shown ----------------

        [UnityTest]
        public IEnumerator InEveryRoomAndPassage_OnlyItAndThoseADoorAway_AreDrawn_TheirEnemiesToo_AndNoOtherLightIsOn()
        {
            GeneratedLevel level = StartRun(v => v.Crypt.LanternLights = true);
            yield return Frames(2);
            CaveView cave = level.Cave;
            Assert.That(cave, Is.Not.Null, "on by default");
            int budget = level.Values.Crypt.LightsPerRoom + level.Values.Signs.LightsMost + 1;
            int mostDrawn = 0, total = 0;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                yield return Into(level, room);
                cave.Tick(); // where every enemy stands now
                Assert.That(cave.Here, Is.EqualTo(room.Order));
                bool[] shown = ShownFor(level, room.Order);
                int drawn = 0;
                total = 0;
                foreach (Renderer r in FloorRenderers(level))
                {
                    List<int> owners = cave.OwnersOf(r);
                    Assert.That(owners, Is.Not.Empty, "every renderer of the floor and its rooms' mood belongs to a room or passage: " + r.name);
                    total++;
                    bool want = owners.Any(o => shown[o]);
                    Assert.That(r.forceRenderingOff, Is.EqualTo(!want), r.name + " of " + string.Join(",", owners) + " in " + room.Letter + room.Order);
                    if (want) drawn++;
                }
                mostDrawn = Math.Max(mostDrawn, drawn);
                for (int i = 0; i < level.Enemies.Count; i++)
                {
                    Enemy e = level.Enemies[i];
                    LevelRoom at = level.RoomAt(e.transform.position);
                    if (at == null) continue;
                    bool want = shown[at.Order];
                    foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
                        Assert.That(r.forceRenderingOff, Is.EqualTo(!want), e.name + " in " + at.Letter + at.Order + ", she in " + room.Letter + room.Order);
                }
                var lights = Object.FindObjectsByType<Light>().Where(l => l.isActiveAndEnabled && l.type != LightType.Directional
                    && (l.transform.root == level.transform || (level.Mood != null && level.Mood.Root != null && l.transform.root == level.Mood.Root.transform))).ToList();
                Assert.That(lights.Count, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
                foreach (Light l in lights)
                {
                    LevelRoom at = level.RoomAt(l.transform.position);
                    Assert.That(at == null || shown[at.Order], l.name + " is on in a room not drawn");
                }
            }
            string line = string.Format(Inv, "CAVECOST run {0} floor 1: {1} renderers of the floor and its mood, at most {2} drawn at once ({3:0%}); {4} rock renderers, {5} rock faces; {7} rock-top renderers over {8} cells; kit pieces {6}",
                                        level.Layout.SeedText, total, mostDrawn, mostDrawn / (double)Math.Max(1, total), cave.Rock.Count, cave.RockFaces, level.KitPieces.Count, cave.Lids.Count, cave.LidCells);
            Debug.Log(line);
            TestContext.WriteLine(line);
        }

        /// <summary>Walks her along the NavMesh to a point, by the move keys as the screen has them; true once she is within a metre.</summary>
        IEnumerator WalkTo(Vector3 goal, float seconds, Action<bool> done, Action eachFrame)
        {
            Camera cam = Camera.main;
            var path = new NavMeshPath();
            float until = Time.time + seconds;
            bool reached = false;
            while (Time.time < until)
            {
                eachFrame();
                Vector3 here = _player.Position;
                if (Vector3.Distance(new Vector3(here.x, 0f, here.z), new Vector3(goal.x, 0f, goal.z)) < 1f)
                {
                    reached = true;
                    break;
                }
                Vector3 next = goal;
                if (NavMesh.SamplePosition(here, out NavMeshHit a, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(goal, out NavMeshHit b, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length >= 2)
                    next = path.corners.Skip(1).FirstOrDefault(c => Vector3.Distance(new Vector3(c.x, 0f, c.z), new Vector3(here.x, 0f, here.z)) > 0.3f);
                if (next == Vector3.zero) next = goal;
                Vector3 sa = cam.WorldToScreenPoint(here), sb = cam.WorldToScreenPoint(here + (new Vector3(next.x, here.y, next.z) - here).normalized * 2f);
                Vector2 move = new Vector2(sb.x - sa.x, sb.y - sa.y).normalized;
                move = new Vector2(Mathf.Abs(move.x) < 0.38f ? 0f : Mathf.Sign(move.x), Mathf.Abs(move.y) < 0.38f ? 0f : Mathf.Sign(move.y));
                _player.GetComponent<PlayerInputDriver>().InputSource = () => new InputFrame { Move = move, CursorScreen = sb };
                yield return null;
            }
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            done(reached);
        }

        [UnityTest]
        public IEnumerator WalkingTheFloor_TheRoomAheadIsDrawnWhileSheIsInThePassage_BeforeSheIsInIt()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            CaveView cave = level.Cave;
            foreach (Enemy e in level.Enemies) e.gameObject.SetActive(false); // nothing in her way while she walks
            var faults = new List<string>();
            int entered = 0;
            LevelRoom last = layout.Rooms[0];
            var wasShown = new bool[layout.Rooms.Count];
            void Note()
            {
                LevelRoom here = level.RoomAt(_player.Position);
                if (here != null && here != last)
                {
                    // The frame she is first in it: was it drawn the frame before, and is every floor box of it drawn now?
                    if (!wasShown[here.Order]) faults.Add(here.Letter + "" + here.Order + " was not drawn the frame before she stepped in");
                    for (int i = 0; i < level.FloorBoxes.Count; i++)
                        if (level.FloorRoomOf(i) == here && level.FloorBoxes[i].GetComponent<Renderer>().forceRenderingOff) faults.Add(here.Letter + "" + here.Order + "'s floor not drawn");
                    if (here.Kind != RoomKind.Passage) entered++;
                    last = here;
                }
                for (int o = 0; o < wasShown.Length; o++) wasShown[o] = cave.IsShown(o);
            }
            // From the entrance through the rooms in their order to the fourth room past the entrance (two passages and two rooms).
            for (int k = 1; k <= Math.Min(4, layout.Rooms.Count - 1); k++)
            {
                LevelRoom room = layout.Rooms[k];
                Vector3 goal = InRoom(level, room);
                bool reached = false;
                IEnumerator walk = WalkTo(goal, 20f, r => reached = r, Note);
                while (walk.MoveNext()) yield return walk.Current;
                Note();
                Assert.That(reached, "she walked into " + room.Letter + room.Order);
                if (room.Kind == RoomKind.Passage)
                {
                    LevelRoom ahead = layout.Rooms[k + 1];
                    Assert.That(cave.IsShown(ahead.Order), "in the passage the room ahead, " + ahead.Letter + ahead.Order + ", is drawn");
                    Assert.That(cave.IsShown(layout.Rooms[k - 1].Order), "and the room behind until she leaves the passage");
                }
            }
            Assert.That(entered, Is.GreaterThanOrEqualTo(2), "she entered rooms on the way");
            Assert.That(faults, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheHiddenEnemies_AreNotDrawn_AndTheirLogicIsAsWithTheCaveOff()
        {
            var seen = new Dictionary<bool, List<string>>();
            foreach (bool on in new[] { false, true })
            {
                GeneratedLevel level = Build(42UL, 1, v => v.Cave = on);
                yield return Frames(2);
                LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat);
                yield return Into(level, room);
                yield return Frames(30);
                bool[] shown = ShownFor(level, room.Order);
                var states = new List<string>();
                int hidden = 0;
                foreach (Enemy e in level.Enemies)
                {
                    LevelRoom at = level.RoomAt(e.transform.position);
                    if (at == null || shown[at.Order]) continue;
                    hidden++;
                    NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
                    states.Add(e.name + " " + e.State + " " + e.transform.position.ToString("F2") + " " + e.gameObject.activeSelf + " " + e.enabled + " " + (agent != null && agent.enabled));
                    foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
                        Assert.That(r.forceRenderingOff, Is.EqualTo(on), e.name + (on ? " not drawn" : " drawn with the cave off"));
                }
                Assert.That(hidden, Is.GreaterThan(10), "enemies in rooms not drawn");
                seen[on] = states;
            }
            Assert.That(seen[true], Is.EqualTo(seen[false]), "their state, place, objects and components as with the cave off");
        }

        // ---------------- The walls ----------------

        [UnityTest]
        public IEnumerator TheRock_ByTheValues_NoColliderNorShadow_TheKitTheCollidersAndTheNavMesh_AsWithTheCaveOff()
        {
            var pieces = new Dictionary<bool, int>();
            var colliders = new Dictionary<bool, string>();
            var navmesh = new Dictionary<bool, int>();
            foreach (bool on in new[] { false, true })
            {
                // The heaps kept for this comparison: their going changes the vault's colliders on purpose (the chest's test).
                GeneratedLevel level = Build(42UL, 5, v => { v.Cave = on; v.CaveRules.NoGoldHeaps = false; });
                yield return Frames(3);
                pieces[on] = level.KitPieces.Count;
                colliders[on] = string.Join(";", level.GetComponentsInChildren<Collider>(true).Where(c => c.GetComponentInParent<Enemy>(true) == null)
                                                     .Select(c => c.name + " " + c.bounds.center.ToString("F2") + " " + c.bounds.size.ToString("F2")).OrderBy(s => s, StringComparer.Ordinal));
                navmesh[on] = NavMesh.CalculateTriangulation().vertices.Length;
                if (!on) continue;
                CaveView cave = level.Cave;
                Assert.That(cave.Rise, Is.Not.Null);
                Assert.That(cave.Rock.Count, Is.GreaterThan(0), "rock");
                CaveValues v2 = level.Values.CaveRules;
                foreach (MeshRenderer r in cave.Rock)
                {
                    Assert.That(r.GetComponent<Collider>(), Is.Null);
                    Assert.That(r.gameObject.layer, Is.EqualTo(CryptKit.Layer));
                    Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                    Bounds b = r.bounds;
                    Assert.That(b.max.y, Is.LessThanOrEqualTo(v2.RiseHeight + 0.5f), "no higher than the values with its facets: " + r.name);
                    Assert.That(b.min.y, Is.GreaterThanOrEqualTo(cave.WallTop - 0.25f - 0.5f), "from the wall's top: " + r.name);
                }
                Assert.That(cave.Rock.Max(r => r.bounds.max.y), Is.GreaterThan(v2.RiseHeight - 0.5f), "some reaches the most");
                Assert.That(cave.Lids.Count, Is.GreaterThan(0), "rock tops round the rooms");
                foreach (MeshRenderer r in cave.Lids)
                {
                    Assert.That(r.GetComponent<Collider>(), Is.Null);
                    Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                    Assert.That(r.bounds.max.y, Is.LessThanOrEqualTo(cave.WallTop + 0.05f), "a top no higher than the walls: " + r.name);
                }
                // Every rock face over a full wall the rule lets rise, none over a low wall.
                int faces = level.KitPlan.Sections.Where(s => !s.Low && cave.Rise.RiseOf(s.Vertical, s.Line, s.Cell) >= cave.WallTop + v2.MinRise)
                                 .Sum(s => (s.RoomMinus >= 0 ? 1 : 0) + (s.RoomPlus >= 0 ? 1 : 0));
                Assert.That(cave.RockFaces, Is.EqualTo(faces));
                Assert.That(level.KitPlan.Sections.Where(s => s.Low).All(s => !cave.Rise.HasRock(s.Vertical, s.Line, s.Cell)), "the low near walls stay low");
                TestContext.WriteLine(string.Format(Inv, "CAVECOST seed 42 floor 5: {0} rock faces in {1} renderers, {2} triangles; the kit's wall top {3:0.00} m",
                                                    cave.RockFaces, cave.Rock.Count, cave.Rock.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3), cave.WallTop));
            }
            Assert.That(pieces[true], Is.EqualTo(pieces[false]), "no kit piece more: the rock is its own mesh");
            Assert.That(colliders[true], Is.EqualTo(colliders[false]), "the colliders as with the cave off");
            Assert.That(navmesh[true], Is.EqualTo(navmesh[false]), "the NavMesh as with the cave off");
        }

        // ---------------- The treasure room ----------------

        [UnityTest]
        public IEnumerator TheVault_HasNoLargeCoinHeap_ItsChestOnTheDais_AndTheChestOpensAsBefore()
        {
            // Seed 42's floor 1 has a vault (T6), its large coin heap in front of the chest with the cave off (round 0's picture).
            GeneratedLevel off = Build(42UL, 1, v => v.Cave = false);
            yield return Frames(2);
            LevelRoom vaultOff = off.Layout.Rooms.First(r => r.Kind == RoomKind.Treasure && r.Theme == RoomTheme.Vault);
            Assert.That(off.LargeProps.Count(p => p.Room == vaultOff && p.Prop.Model == PropModel.CoinHeap), Is.GreaterThan(0), "the heaps with the cave off");

            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelRoom treasure = level.Layout.Rooms.First(r => r.Kind == RoomKind.Treasure);
            Assert.That(level.LargeProps.Any(p => p.Prop.Model == PropModel.CoinHeap), Is.False, "no large coin heap on the floor");
            yield return Into(level, treasure);
            GeneratedLevel.PlacedProp chest = _levels.Chests().First(c => c.Room == treasure);
            Assert.That(chest.Object.GetComponentInChildren<Renderer>().forceRenderingOff, Is.False, "the chest drawn");
            Assert.That(_levels.OpenChest(chest), Is.True, "opened");
            yield return null;
            Assert.That(_levels.PickUpgrade(0), Is.True, "and its offer taken");
        }

        // ---------------- The switch, the earth, the frame ----------------

        [UnityTest]
        public IEnumerator SwitchOff_NothingOfIt_TheEarthOnlyWhileAFloorIsUp()
        {
            Camera cam = Camera.main;
            Color scene = cam.backgroundColor;
            GeneratedLevel off = Build(42UL, 1, v => v.Cave = false);
            yield return Frames(2);
            Assert.That(off.Cave, Is.Null);
            Assert.That(off.GetComponent<CaveView>(), Is.Null);
            Assert.That(off.transform.Find(CaveView.RockRootName), Is.Null);
            Assert.That(FloorRenderers(off).Any(r => r.forceRenderingOff), Is.False, "every renderer drawn");
            Assert.That(cam.backgroundColor, Is.EqualTo(scene), "the scene's background");
            int offComponents = off.GetComponentsInChildren<Component>(true).Length;
            int offObjects = off.GetComponentsInChildren<Transform>(true).Length;

            GeneratedLevel on = Build(42UL, 1, v => v.CaveRules.NoGoldHeaps = false);
            yield return Frames(2);
            CaveView cave = on.Cave;
            Rgb earth = on.Values.CaveRules.Earth;
            Assert.That(cam.backgroundColor, Is.EqualTo(new Color(earth.R, earth.G, earth.B, 1f)), "the earth's black behind the floor");
            int rock = cave.Rock.Count + cave.Lids.Count; // the faces over the walls and the tops round the rooms
            Assert.That(cave.Lids.Count, Is.GreaterThan(0), "rock tops round the rooms");
            Assert.That(on.GetComponentsInChildren<Transform>(true).Length - offObjects, Is.EqualTo(rock + 1), "the rock's objects and their root, nothing else");
            Assert.That(on.GetComponentsInChildren<Component>(true).Length - offComponents, Is.EqualTo(1 + 1 + 3 * rock), "the cave, the rock's root and each rock's transform, filter and renderer");

            _levels.BackToPracticeRoom();
            yield return Frames(2);
            Assert.That(cam.backgroundColor, Is.EqualTo(scene), "the scene's background back when the floor goes");
        }

        [UnityTest]
        public IEnumerator ItsFrame_AllocatesNothing()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            CaveView cave = level.Cave;
            LevelRoom a = level.Layout.Rooms[2], b = level.Layout.Rooms[3];
            yield return Into(level, a);
            cave.Tick();
            cave.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) cave.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the cave's frame allocates nothing");
            // Changing room too: what is drawn is worked out again without allocating.
            Assert.That(() => { for (int i = 0; i < 50; i++) cave.Look(i % 2 == 0 ? b.Order : a.Order); }, AllocExt.AllocatingGCMemory(Is.Not), "a change of room allocates nothing");
        }
    }
}
