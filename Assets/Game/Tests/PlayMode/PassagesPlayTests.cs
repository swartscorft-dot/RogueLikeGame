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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Passages between the rooms (work order 059; Kristian, 2026-11-20: "bedre bruk av mappet ... litt dungen crawler følelse
    /// trenger vi", and his choice of 2026-11-21, "Ganger mellom rommene"), built, in the ControlRoom scene through the intents of
    /// <see cref="GeneratedLevels"/>: the NavMesh joins her start to the exit room's middle and runs through every passage, a
    /// metre at least from its edges at every path cell's middle; no floor box lies under a solid cell or outside the rooms;
    /// every side of a passage cell with neither a door nor a path cell beyond it has a wall collider; the kit's pieces and the
    /// lights on floor 5 against the passages off; she walks from the entrance through the first passage into the first combat
    /// room and her room goes E, P, C; an enemy woken there reaches her in the passage; the signs at both ends of a passage to a
    /// treasure room, lit from the right room, dimmed by the chest; the map one step ahead past a passage; the mood's air ahead,
    /// not behind; and with the switch off the floor's objects are version 15's. Writes what the passages cost ("PASSAGECOST").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames or game seconds, never a frame's length.
    /// </summary>
    public class PassagesPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        FloorMap _map;

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
            _map = FloorMap.Ensure();
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
            if (_map != null) Object.Destroy(_map.gameObject);
            _map = null;
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

        /// <summary>A passage's middle cell of its path, on the ground: where she stands "in the passage".</summary>
        static Vector3 InPassage(GeneratedLevel level, LevelRoom passage)
        {
            List<(int x, int y)> path = Passages.PathOf(level.Layout, passage);
            (int x, int y) c = path[path.Count / 2];
            return CellMiddle(level, c.x, c.y);
        }

        /// <summary>She stands at a point, and the run has seen her in the room there.</summary>
        IEnumerator At(GeneratedLevel level, Vector3 point, LevelRoom room)
        {
            _player.Teleport(point);
            yield return Until(() => _levels.CurrentRoom == room, 30);
            yield return Frames(2);
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        IEnumerator Into(GeneratedLevel level, LevelRoom room) =>
            At(level, room.Kind == RoomKind.Passage ? InPassage(level, room) : level.Middle(room), room);

        static string Where(GeneratedLevel level) => "seed " + level.Layout.SeedText + " floor " + level.Layout.Floor;

        // ---------------- The built floor ----------------

        [UnityTest]
        public IEnumerator BuiltFloors_HaveAWayThroughEveryPassage_WallsOnTheirSides_AndNoFloorUnderASolidCell()
        {
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
                foreach (int floor in new[] { 1, 5 })
                {
                    GeneratedLevel level = Build(seed, floor);
                    yield return Frames(2);
                    LevelLayout layout = level.Layout;
                    string where = Where(level);
                    float cs = layout.CellSize;
                    Assert.That(layout.Passages, Is.True, "on by default");
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.SamplePosition(level.PlayerStart, out NavMeshHit a, 1.5f, NavMesh.AllAreas), where);
                    Assert.That(NavMesh.SamplePosition(level.Middle(layout.ExitRoom), out NavMeshHit b, 2f, NavMesh.AllAreas), where);
                    Assert.That(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                "a complete path from her start to the exit room's middle, " + where);
                    var walls = new HashSet<GameObject>(level.WallBoxes);
                    foreach (LevelRoom p in layout.Rooms.Where(r => r.Kind == RoomKind.Passage))
                    {
                        string name = p.Letter + p.Order.ToString(Inv) + " " + where;
                        List<(int x, int y)> cells = Passages.PathOf(layout, p);
                        Vector3 first = CellMiddle(level, cells[0].x, cells[0].y), last = CellMiddle(level, cells[cells.Count - 1].x, cells[cells.Count - 1].y);
                        Assert.That(NavMesh.SamplePosition(first, out NavMeshHit f, 0.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(last, out NavMeshHit l, 0.5f, NavMesh.AllAreas)
                                    && NavMesh.CalculatePath(f.position, l.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                    "a complete path through " + name);
                        foreach ((int x, int y) in cells)
                        {
                            Vector3 middle = CellMiddle(level, x, y);
                            Assert.That(NavMesh.SamplePosition(middle, out NavMeshHit on, 0.3f, NavMesh.AllAreas), "NavMesh at the middle of " + (x, y) + " of " + name);
                            Assert.That(NavMesh.FindClosestEdge(on.position, out NavMeshHit edge, NavMesh.AllAreas), name);
                            Assert.That(edge.distance, Is.GreaterThanOrEqualTo(1f), "a metre from the NavMesh's edge at " + (x, y) + " of " + name);
                            // A wall on every side with no door and no path cell beyond it.
                            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            {
                                if (cells.Contains((x + dx, y + dy))) continue;
                                bool door = layout.DoorsOf(p.Order).Any(d => d.CellPairs().Any(c => (c.Key == (x, y) && c.Value == (x + dx, y + dy)) || (c.Value == (x, y) && c.Key == (x + dx, y + dy))));
                                if (door) continue;
                                RaycastHit[] hits = Physics.RaycastAll(middle + Vector3.up * 0.5f, new Vector3(dx, 0f, dy), 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                                Assert.That(hits.Any(h => walls.Contains(h.collider.gameObject)), "a wall on the " + (dx, dy) + " side of " + (x, y) + " of " + name);
                            }
                        }
                    }
                    // No floor box under a solid cell or outside the rooms.
                    for (int i = 0; i < level.FloorBoxes.Count; i++)
                    {
                        CellRect c = level.FloorCellsOf(i);
                        for (int x = c.X; x < c.Right; x++)
                            for (int y = c.Y; y < c.Top; y++)
                                Assert.That(layout.RoomAtCell(x, y), Is.SameAs(level.FloorRoomOf(i)), "a floor box over an open cell of its room only, " + (x, y) + " " + where);
                    }
                }
        }

        [UnityTest]
        public IEnumerator Floor5_TheKitsPieces_AndTheLights_WithThePassagesOffAndOn()
        {
            var counts = new Dictionary<bool, (int pieces, int walls, int floors, int lights)>();
            foreach (bool passages in new[] { false, true })
            {
                GeneratedLevel level = Build(0x5EED5UL, 5, v => { v.Passages = passages; v.Crypt.LanternLights = true; });
                yield return Frames(2);
                int budget = level.Values.Crypt.LightsPerRoom + level.Values.Signs.LightsMost + 1;
                int mostOn = 0;
                foreach (LevelRoom room in level.Layout.Rooms)
                {
                    yield return Into(level, room);
                    int on = Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled && l.type != LightType.Directional
                        && (l.transform.root == level.transform || (level.Mood != null && level.Mood.Root != null && l.transform.root == level.Mood.Root.transform)));
                    mostOn = Mathf.Max(mostOn, on);
                    Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order + (passages ? " with" : " without") + " passages");
                }
                if (level.KitLights != null)
                    foreach (KeyValuePair<int, List<Light>> pair in level.KitLights.ByRoom)
                        Assert.That(pair.Value.Count, Is.LessThanOrEqualTo(level.Values.Crypt.LightsPerRoom), "the lanterns' lights of room " + pair.Key);
                counts[passages] = (level.KitPieces.Count, level.Walls, level.Floors, mostOn);
            }
            string line = string.Format(Inv, "PASSAGECOST seed 5EED5 floor 5: kit pieces {0} without the passages, {1} with ({2:0.00} times); wall boxes {3} and {4}; floor boxes {5} and {6}; " +
                                             "at most {7} and {8} lights on at once (the lanterns' lights on)",
                                        counts[false].pieces, counts[true].pieces, counts[true].pieces / (double)counts[false].pieces, counts[false].walls, counts[true].walls,
                                        counts[false].floors, counts[true].floors, counts[false].lights, counts[true].lights);
            Debug.Log(line);
            TestContext.WriteLine(line);
            Assert.That(counts[true].pieces, Is.LessThanOrEqualTo(counts[false].pieces * 1.6), "the kit's pieces at most 1.6 times");
        }

        // ---------------- Through the intents ----------------

        /// <summary>Walks her along the NavMesh to a point, by the move keys as the screen has them; true once she is within a metre.</summary>
        IEnumerator WalkTo(Vector3 goal, float seconds, Action<bool> done)
        {
            Camera cam = Camera.main;
            var path = new NavMeshPath();
            float until = Time.time + seconds;
            bool reached = false;
            while (Time.time < until)
            {
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
                move = new Vector2(Mathf.Abs(move.x) < 0.38f ? 0f : Mathf.Sign(move.x), Mathf.Abs(move.y) < 0.38f ? 0f : Mathf.Sign(move.y)); // the keys: each -1, 0 or 1
                _player.GetComponent<PlayerInputDriver>().InputSource = () => new InputFrame { Move = move, CursorScreen = sb };
                yield return null;
            }
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            done(reached);
        }

        [UnityTest]
        public IEnumerator SheWalks_FromTheEntrance_ThroughThePassage_IntoTheFirstCombatRoom_AndAnEnemyThereReachesHerInThePassage()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            LevelRoom entrance = layout.Rooms[0], passage = layout.Rooms[1], combat = layout.Rooms[2];
            Assert.That((entrance.Kind, passage.Kind, combat.Kind), Is.EqualTo((RoomKind.Entrance, RoomKind.Passage, RoomKind.Combat)));
            foreach (Enemy e in level.Enemies) e.gameObject.SetActive(false); // nothing in her way while she walks
            var seen = new List<int>();
            void Note()
            {
                int here = _levels.CurrentRoom != null ? _levels.CurrentRoom.Order : -1;
                if (seen.Count == 0 || seen[seen.Count - 1] != here) seen.Add(here);
            }
            Note();
            LevelDoor into = LevelGeometry.EntryDoor(layout, combat);
            into.Opening(layout.CellSize, out LevelPoint d0, out LevelPoint d1);
            // A few metres into the combat room past its door.
            LevelPoint doorMiddle = new LevelPoint((d0.X + d1.X) * 0.5, (d0.Y + d1.Y) * 0.5), m = combat.Middle(layout.CellSize);
            double dx = into.Vertical ? Math.Sign(m.X - doorMiddle.X) * 4 : 0, dy = into.Vertical ? 0 : Math.Sign(m.Y - doorMiddle.Y) * 4;
            Vector3 goal = level.ToWorld(new LevelPoint(doorMiddle.X + dx, doorMiddle.Y + dy));
            bool reached = false;
            IEnumerator walk = WalkTo(goal, 20f, r => reached = r);
            while (walk.MoveNext())
            {
                Note();
                yield return walk.Current;
            }
            yield return Frames(3);
            Note();
            Assert.That(reached, "she walked from the entrance into the first combat room");
            Assert.That(seen.Where(o => o >= 0), Is.EqualTo(new[] { 0, 1, 2 }), "her room went E, P, C");

            // An enemy of the combat room, woken, comes into the passage to her.
            foreach (Enemy e in level.Enemies) e.gameObject.SetActive(true);
            yield return At(level, InPassage(level, passage), passage);
            Enemy nearest = level.EnemiesByRoom[combat.Order].Where(e => e != null && !e.IsDead).OrderBy(e => Vector3.Distance(e.transform.position, _player.Position)).First();
            nearest.GetComponent<Damageable>().TakeHit(1); // provoked: it chases her within its leash
            float until = Time.time + 12f;
            bool came = false;
            while (Time.time < until && !came)
            {
                yield return null;
                came = level.RoomAt(nearest.transform.position) == passage || Vector3.Distance(nearest.transform.position, _player.Position) < 2.5f;
            }
            Assert.That(came, "the woken " + nearest.Kind + " came into the passage to her (it was " + Vector3.Distance(nearest.transform.position, _player.Position).ToString("0.0", Inv) + " m away)");

            // She goes far beyond its leash (the exit room): it gives up and walks home through the passage's door, not stuck in it.
            Vector3 home = nearest.Home;
            _player.Teleport(level.Middle(layout.ExitRoom));
            until = Time.time + 20f;
            bool backHome = false;
            while (Time.time < until && !backHome)
            {
                yield return null;
                backHome = nearest.State == EnemyState.Idle && Vector3.Distance(new Vector3(nearest.transform.position.x, 0f, nearest.transform.position.z), new Vector3(home.x, 0f, home.z)) < 1.5f;
            }
            Assert.That(backHome, "the " + nearest.Kind + " walked home through the passage's door (it is " + Vector3.Distance(nearest.transform.position, home).ToString("0.0", Inv)
                                  + " m from home, in " + (level.RoomAt(nearest.transform.position)?.Letter.ToString() ?? "no room") + ", " + nearest.State + ")");
        }

        // ---------------- The signs, the map, the mood ----------------

        [UnityTest]
        public IEnumerator APassageToATreasureRoom_ShowsTheChestAtBothEnds_LitFromTheRightRoom_AndTheChestDimsBoth()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            var signs = level.GetComponent<DoorSigns>();
            Assert.That(signs, Is.Not.Null);
            int signed = layout.Rooms.Count(r => DoorSignRules.SignOf(r, layout.Floor, level.Values.FloorsPerAct) != DoorSign.None);
            Assert.That(signs.Signs.Count, Is.EqualTo(2 * signed), "two signs for each signed room: one at each end of its passage");
            foreach (LevelRoom r in layout.Rooms.Where(x => DoorSignRules.SignOf(x, layout.Floor, level.Values.FloorsPerAct) != DoorSign.None))
                Assert.That(signs.Signs.Count(s => s.For == r.Order), Is.EqualTo(2), r.Letter + r.Order.ToString(Inv));

            LevelRoom treasure = layout.Rooms.First(r => r.Kind == RoomKind.Treasure);
            LevelRoom passage = layout.Rooms[treasure.Order - 1], before = layout.Rooms[treasure.Order - 2];
            Assert.That(passage.Kind, Is.EqualTo(RoomKind.Passage));
            DoorSigns.Sign mouth = signs.Signs.First(s => s.Beyond == passage.Order), far = signs.SignInto(treasure.Order);
            Assert.That(mouth.Kind, Is.EqualTo(DoorSign.Treasure));
            Assert.That(far.Kind, Is.EqualTo(DoorSign.Treasure));

            yield return Into(level, before);
            Assert.That(mouth.Lit, Is.True, "the mouth's sign, from the room before");
            Assert.That(far.Lit, Is.False);
            Assert.That(signs.Signs.Count(s => s.Lit), Is.EqualTo(1));
            Assert.That(signs.DoorLight != null && signs.DoorLight.enabled, "its light");
            Assert.That(level.RoomAt(signs.DoorLight.transform.position), Is.SameAs(before), "the light on her side of the mouth");
            // The map: from the room before, the passage and the treasure room show one step ahead.
            _map.Tick();
            Assert.That(_map.RoomShown(passage.Order), Is.EqualTo(MapShown.Ahead));
            Assert.That(_map.RoomShown(treasure.Order), Is.EqualTo(MapShown.Ahead));
            Assert.That(_map.RoomShown(treasure.Order + 1), Is.EqualTo(MapShown.Hidden), "never more");

            yield return Into(level, passage);
            Assert.That(far.Lit, Is.True, "the far end's sign, from inside the passage");
            Assert.That(mouth.Lit, Is.False);
            Assert.That(signs.Signs.Count(s => s.Lit), Is.EqualTo(1), "one door light at most");
            Assert.That(level.RoomAt(signs.DoorLight.transform.position), Is.SameAs(passage), "the light in the passage, on her side of the far end");

            yield return Into(level, treasure);
            GeneratedLevel.PlacedProp chest = _levels.Chests().First(c => c.Room == treasure);
            Assert.That(_levels.OpenChest(chest), Is.True);
            yield return null;
            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return Frames(2);
            yield return Into(level, before);
            Assert.That(mouth.Lit || far.Lit, Is.False, "both dim once the chest is opened");
            Assert.That(mouth.BeyondDone && far.BeyondDone, Is.True);
        }

        [UnityTest]
        public IEnumerator InAPassage_TheAirAheadIsOn_AndBehindOff_InARoom_TheRoomPastThePassage()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            RoomMood mood = level.Mood;
            Assert.That(mood, Is.Not.Null);
            Assert.That(mood.Rooms.All(p => layout.Rooms[p.Order].Kind != RoomKind.Passage), "no air, fog or embers of a passage's own");
            Assert.That(mood.Rooms.Count, Is.EqualTo(LevelGeometry.RoomCount(layout)));
            RoomMood.RoomParts Parts(int order) => mood.Rooms.First(p => p.Order == order);
            LevelRoom combat = layout.Rooms[2], passage = layout.Rooms[3], after = layout.Rooms[4];
            Assert.That(passage.Kind, Is.EqualTo(RoomKind.Passage));

            yield return Into(level, combat);
            mood.Tick();
            Assert.That(mood.Next, Is.EqualTo(after.Order), "in a room, the next room is past the passage");
            Assert.That(Parts(after.Order).AirOn, Is.True, "the room past the passage has its air on");
            Assert.That(Parts(combat.Order).AirOn, Is.True);
            Assert.That(Parts(layout.Rooms[0].Order).AirOn, Is.False);

            yield return Into(level, passage);
            mood.Tick();
            Assert.That(mood.Next, Is.EqualTo(after.Order));
            Assert.That(Parts(after.Order).AirOn, Is.True, "in a passage, the room ahead is on");
            Assert.That(Parts(combat.Order).AirOn, Is.False, "and the room behind off");
            Assert.That(Parts(combat.Order).Lights.All(l => !l.enabled) && Parts(after.Order).Lights.All(l => !l.enabled), "no room's real lights in a passage");
            Assert.That(mood.HerLight != null && mood.HerLight.enabled, "her own light");
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator SwitchOff_TheFloorsObjects_AreVersion15s()
        {
            GeneratedLevel off = Build(42UL, 1, v => { v.Passages = false; v.MoreTemplates = false; }); // work order 072: version 15 had none of the second set
            yield return Frames(2);
            Assert.That(off.Layout.Passages, Is.False);
            Assert.That(off.Layout.Rooms.Any(r => r.Kind == RoomKind.Passage), Is.False);
            Assert.That(off.Layout.GridCells, Is.EqualTo(48));
            // The floor's objects, name for name and place for place, as a floor built from the same seed's layout with the
            // passages off gives them (the generator with the passages off is version 15's: EditMode PassagesTests).
            LevelLayout plain = LevelGenerator.Generate(off.Layout.Seed, 1, new LevelValues { Passages = false, MoreTemplates = false });
            Assert.That(plain.Describe(), Is.EqualTo(off.Layout.Describe()), "the same floor");
            string Objects(GeneratedLevel l) => string.Join(";", l.FloorBoxes.Concat(l.WallBoxes).Concat(l.KitPieces.Select(p => p.Object))
                .Select(g => g.name + " " + g.transform.position.ToString("F3") + " " + g.transform.localScale.ToString("F3")));
            string first = Objects(off);
            GeneratedLevel again = GeneratedLevel.Build(plain, new LevelValues { Passages = false, MoreTemplates = false, RoomMood = false }, null, null);
            yield return null;
            Assert.That(Objects(again), Is.EqualTo(first), "the same objects");
            Assert.That(again.KitPlan.Describe(), Is.EqualTo(off.KitPlan.Describe()));
            Object.Destroy(again.gameObject);
            Assert.That(first, Does.Not.Contain("Floor_P"));
            var signs = off.GetComponent<DoorSigns>();
            Assert.That(signs.Signs.Count, Is.EqualTo(4), "a sign for each signed room, as before");
            Assert.That(signs.Signs.All(s => s.For == s.Beyond));
        }
    }
}
