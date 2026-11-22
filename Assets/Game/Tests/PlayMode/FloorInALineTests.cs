using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// A floor in a line, the doors that say what is behind them and the small map (work order 041; Kristian's second session
    /// of 2026-11-20), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>, <see cref="DoorSigns"/>
    /// and <see cref="FloorMap"/>: a built floor is one row with no side room; from the room before it, the doorway on shows
    /// the next room's sign in its colour and its light spills on her side, and the door she came in by is dim; opening a
    /// chest takes the gold off its door and off the map; a cleared room's door and map room go dim; the map shows the rooms
    /// she has been in and one step ahead, and nothing under a choice, the pause menu or an end screen; the lights stay
    /// inside the budget on floor 5; nothing is allocated in their frames; and each switch off removes its part.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames, and nothing hangs on a frame's length.
    ///
    /// Work order 082 (review 6, finding 2): the tests of the signs, the map and their frames run both with the passages
    /// off (the rooms touching, as they were built for) and on (the floor he plays, each signed room's sign at both ends of
    /// its passage). Kept one way, and why: the map's step-ahead (on: PassagesPlayTests), floor 5's light budget (on:
    /// PassagesPlayTests.Floor5_...) and the switches.
    /// </summary>
    public class FloorInALineTests
    {
        const ulong RunSeed = 0x41414141UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        FloorMap _map;
        RunHud _hud;

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
            _map = FloorMap.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
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

        GeneratedLevel StartRun(Action<LevelValues> change = null) => StartRun(false, change);

        GeneratedLevel StartRun(bool passages, Action<LevelValues> change = null)
        {
            // Work order 059: the rooms touching, as the row's doors, signs and map were built for them (each sign on the door
            // into its room, one room ahead on the map); work order 082: and with the passages on, the floor he plays.
            _levels.Values = new LevelValues { Passages = passages };
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        /// <summary>A passage's middle cell of its path, on the ground (as PassagesPlayTests): where she stands "in the passage".</summary>
        static Vector3 InPassage(GeneratedLevel level, LevelRoom passage)
        {
            List<(int x, int y)> path = Passages.PathOf(level.Layout, passage);
            (int x, int y) c = path[path.Count / 2];
            double cs = level.Layout.CellSize;
            return level.ToWorld(new LevelPoint((c.x + 0.5) * cs, (c.y + 0.5) * cs));
        }

        /// <summary>She stands in the middle of a room (of a passage's path), and the run has seen her there.</summary>
        IEnumerator Into(GeneratedLevel level, LevelRoom room)
        {
            _player.Teleport(room.Kind == RoomKind.Passage ? InPassage(level, room) : level.Middle(room));
            yield return Until(() => _levels.CurrentRoom == room, 30);
            yield return Frames(2);
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        static bool Warm(Color c) => c.r > c.b + 0.3f && c.g > c.b;
        static bool Red(Color c) => c.r > 0.8f && c.g < 0.35f && c.b < 0.35f;
        static bool Violet(Color c) => c.b > c.g + 0.3f && c.r > c.g;

        [UnityTest]
        public IEnumerator ABuiltFloor_IsOneRow_WithNoSideRoom([Values(false, true)] bool passages)
        {
            GeneratedLevel level = StartRun(passages);
            yield return null;
            LevelLayout layout = level.Layout;
            Assert.That(layout.InALine, Is.True);
            Assert.That(layout.Passages, Is.EqualTo(passages));
            Assert.That(layout.Rooms.Any(r => r.Kind == RoomKind.Passage), Is.EqualTo(passages));
            Assert.That(layout.Version, Is.EqualTo(LevelGenerator.Version));
            Assert.That(layout.Rooms.Count(r => r.Kind == RoomKind.Treasure), Is.EqualTo(2));
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var others = layout.DoorsOf(i).Select(d => d.Other(i)).OrderBy(x => x).ToList();
                var wanted = new List<int>();
                if (i > 0) wanted.Add(i - 1);
                if (i + 1 < layout.Rooms.Count) wanted.Add(i + 1);
                Assert.That(others, Is.EqualTo(wanted), "room " + i + " joins only the rooms before and after it\n" + LevelMap.Draw(layout));
            }
            Assert.That(_levels.Chests().Count, Is.EqualTo(2), "a chest in each treasure room");
        }

        [UnityTest]
        public IEnumerator TheDoorOn_ShowsTheNextRoomsSign_InItsColour_AndTheDoorBackIsDim([Values(false, true)] bool passages)
        {
            GeneratedLevel level = StartRun(passages);
            yield return null;
            LevelLayout layout = level.Layout;
            var signs = level.GetComponent<DoorSigns>();
            Assert.That(signs, Is.Not.Null, "on by default");
            Assert.That(signs.Signs.Count, Is.EqualTo(passages ? 8 : 4), "two treasure rooms, the elite room and the exit room (at both ends of a passage)");
            Assert.That(signs.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "a picture only");

            // In the entrance: its door on leads to a combat room, so nothing is lit and no light is made.
            yield return Frames(2);
            Assert.That(signs.Signs.All(s => !s.Lit), "nothing lit from the entrance");
            Assert.That(signs.DoorLight, Is.Null);

            foreach ((Func<LevelRoom, bool> which, DoorSign kind, Func<Color, bool> colour) in new (Func<LevelRoom, bool>, DoorSign, Func<Color, bool>)[]
                     {
                         (r => r.Kind == RoomKind.Treasure, DoorSign.Treasure, Warm),
                         (r => r.Elite, DoorSign.Elite, Red),
                         (r => r.Kind == RoomKind.Exit, DoorSign.Exit, Violet),
                     })
            {
                LevelRoom next = layout.Rooms.First(which);
                // Work order 082: each of its signs in the order she meets them (one; with the passages on, the passage's
                // mouth and then its far end), each lit from the room before its door and dim once she is through.
                List<DoorSigns.Sign> its = signs.Signs.Where(s => s.For == next.Order).OrderBy(s => s.Before).ToList();
                Assert.That(its.Count, Is.EqualTo(passages ? 2 : 1), kind + ": a sign at each end of its passage");
                Assert.That(its.Last(), Is.SameAs(signs.SignInto(next.Order)));
                foreach (DoorSigns.Sign sign in its)
                {
                    LevelRoom before = layout.Rooms[sign.Before];
                    yield return Into(level, before);
                    Assert.That(sign.Kind, Is.EqualTo(kind));
                    Assert.That(sign.Lit, Is.True, "the door on, from " + before.Letter + before.Order + ", says " + kind);
                    Assert.That(sign.Filter.sharedMesh, Is.SameAs(sign.LitMesh));
                    Assert.That(colour(sign.Colour), "its colour: " + sign.Colour);
                    Assert.That(signs.Signs.Count(s => s.Lit), Is.EqualTo(1), "only the door on");
                    Light light = signs.DoorLight;
                    Assert.That(light, Is.Not.Null);
                    Assert.That(light.enabled, Is.True, "its light spills from the doorway");
                    Assert.That(colour(light.color), "the light in its colour: " + light.color);
                    Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                    Assert.That(level.RoomAt(light.transform.position), Is.SameAs(before), "on her side of the doorway");
                    Assert.That(level.RoomAt(sign.Object.transform.position), Is.SameAs(before), "the sign over the arch, a little on her side");
                    Assert.That(sign.Object.transform.position.y, Is.GreaterThan(level.Values.WallHeight), "over the arch");

                    // Through the door: the way back is dim, and the light goes with the lit door.
                    yield return Into(level, layout.Rooms[sign.Beyond]);
                    Assert.That(sign.Lit, Is.False, "the door she came in by is dim");
                    Assert.That(sign.Filter.sharedMesh, Is.SameAs(sign.DimMesh));
                }
            }
            // On floor 5 the exit room's door says the boss.
            Assert.That(DoorSignRules.SignOf(layout.ExitRoom, 5, level.Values.FloorsPerAct), Is.EqualTo(DoorSign.Boss));
        }

        [UnityTest]
        public IEnumerator OpeningAChest_TakesTheGoldOffItsDoor_AndOffTheMap_AndNothingShowsUnderTheChoice([Values(false, true)] bool passages)
        {
            GeneratedLevel level = StartRun(passages);
            yield return null;
            var signs = level.GetComponent<DoorSigns>();
            LevelRoom treasure = level.Layout.Rooms.First(r => r.Kind == RoomKind.Treasure);
            DoorSigns.Sign sign = signs.SignInto(treasure.Order);
            LevelRoom before = level.Layout.Rooms[sign.Before]; // work order 082: with the passages on, the passage itself
            Assert.That(before.Kind == RoomKind.Passage, Is.EqualTo(passages));
            // With the passages on she comes into the passage from the room before it, as she walks (the map shows the room
            // past a passage one step ahead once the room before the passage is entered: FloorMapRules.Before).
            if (passages) yield return Into(level, level.Layout.Rooms[before.Order - 1]);
            yield return Into(level, before);
            Assert.That(sign.Lit, Is.True);
            Assert.That(_map.RoomShown(treasure.Order), Is.EqualTo(MapShown.Ahead));
            Assert.That(_map.SignOf(treasure.Order), Is.EqualTo(DoorSign.Treasure));
            Assert.That(_map.SignStrength(treasure.Order), Is.EqualTo(1f), "its gold on the map, seen ahead");

            GeneratedLevel.PlacedProp chest = _levels.Chests().First(c => c.Room == treasure);
            Assert.That(_levels.OpenChest(chest), Is.True);
            yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            _map.Tick();
            Assert.That(_map.Shown, Is.False, "no map under a choice");
            _map.HoldLarge = true;
            _map.Tick();
            Assert.That(_map.Large, Is.False, "nor large");
            _map.HoldLarge = false;
            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(_map.Shown, Is.True, "back once the choice is gone");

            Assert.That(sign.BeyondDone, Is.True, "the chest is opened: the room is done");
            Assert.That(sign.Lit, Is.False, "the door's gold goes dim");
            Assert.That(signs.Signs.Where(s => s.For == treasure.Order).All(s => s.BeyondDone && !s.Lit), "every sign of the room, at both ends of its passage");
            Assert.That(signs.DoorLight == null || !signs.DoorLight.enabled, "and its light goes out");
            Assert.That(_map.RoomDone(treasure.Order), Is.True);
            Assert.That(_map.SignStrength(treasure.Order), Is.EqualTo(0f), "the map loses its gold");
        }

        [UnityTest]
        public IEnumerator ClearingARoom_DimsItsDoor_AndItsRoomOnTheMap([Values(false, true)] bool passages)
        {
            GeneratedLevel level = StartRun(passages);
            yield return null;
            var signs = level.GetComponent<DoorSigns>();
            LevelLayout layout = level.Layout;
            LevelRoom elite = layout.EliteRoom;
            // Work order 082 (review 6, finding 2): every sign of the elite room, each seen from the room before its door
            // (with the passages on, the passage's mouth from the room before it and its far end from inside it).
            List<DoorSigns.Sign> its = signs.Signs.Where(s => s.For == elite.Order).OrderBy(s => s.Before).ToList();
            Assert.That(its.Count, Is.EqualTo(passages ? 2 : 1));
            LevelRoom before = layout.Rooms[its[0].Before];
            foreach (DoorSigns.Sign s in its)
            {
                yield return Into(level, layout.Rooms[s.Before]);
                Assert.That(s.Lit, Is.True, "lit from " + layout.Rooms[s.Before].Letter + s.Before + " before the room is cleared");
            }
            yield return Into(level, elite);
            _levels.FinishEliteRoomForTests();
            yield return Until(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || _levels.EliteRoomDone);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade)
            {
                _map.Tick();
                Assert.That(_map.Shown, Is.False, "no map under the elite room's choice");
                _levels.PickSkillUpgrade(0);
            }
            yield return Frames(2);
            Assert.That(_map.RoomDone(elite.Order), Is.True, "cleared");
            Assert.That(_map.RoomShown(elite.Order), Is.EqualTo(MapShown.Here));
            Assert.That(_map.RoomDim(elite.Order), Is.False, "lit while she is in it");

            foreach (DoorSigns.Sign s in its)
            {
                yield return Into(level, layout.Rooms[s.Before]);
                Assert.That(s.BeyondDone, Is.True, "cleared, seen from " + layout.Rooms[s.Before].Letter + s.Before);
                Assert.That(s.Lit, Is.False, "a cleared room's door is dim, even seen from the room before it (" + layout.Rooms[s.Before].Letter + s.Before + ")");
            }
            yield return Into(level, before);
            Assert.That(_map.RoomShown(elite.Order), Is.EqualTo(MapShown.Entered));
            Assert.That(_map.RoomDim(elite.Order), Is.True, "and it is filled dim on the map");
            Assert.That(_map.SignStrength(elite.Order), Is.EqualTo(level.Values.Signs.DimShare).Within(1e-6f), "its sign dim");

            // An ordinary combat room too (the first; with the passages on, past the entrance's passage).
            LevelRoom first = layout.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite);
            yield return Into(level, first);
            Assert.That(_map.RoomDone(first.Order), Is.False);
            foreach (Enemy e in level.EnemiesByRoom[first.Order]) e.GetComponent<Damageable>().TakeHit(100000000);
            yield return Frames(3);
            Assert.That(_map.RoomDone(first.Order), Is.True);
            LevelRoom after = layout.Rooms[first.Order + 1];
            yield return Into(level, after);
            Assert.That(_map.RoomDim(first.Order), Is.True, "entered and cleared: dim");
            Assert.That(_map.RoomDim(after.Order), Is.False, "the room she is in is not");
        }

        [UnityTest]
        public IEnumerator TheMap_ShowsTheRoomsSheHasBeenIn_AndOneStepAhead_AtTheTopRight()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            Assert.That(_map.Shown, Is.True, "in a floor, nothing over it");
            Assert.That(_map.Layout, Is.SameAs(layout));
            Assert.That(_map.RoomShown(0), Is.EqualTo(MapShown.Here));
            Assert.That(_map.Here, Is.EqualTo(0));
            Assert.That(_map.RoomShown(1), Is.EqualTo(MapShown.Ahead), "the room after the entrance, as an outline");
            for (int i = 2; i < layout.Rooms.Count; i++) Assert.That(_map.RoomShown(i), Is.EqualTo(MapShown.Hidden), "room " + i + " not yet");
            Assert.That(_map.DoorShown(0), Is.True);
            for (int d = 1; d < layout.Doors.Count; d++) Assert.That(_map.DoorShown(d), Is.False, "door " + d);
            Assert.That(Vector2.Distance(new Vector2((float)_map.Dot.X, (float)_map.Dot.Y), new Vector2((float)layout.Rooms[0].PlayerStart.Value.X, (float)layout.Rooms[0].PlayerStart.Value.Y)), Is.LessThan(0.5f), "the dot where she is");

            yield return Into(level, layout.Rooms[1]);
            Assert.That(_map.RoomShown(0), Is.EqualTo(MapShown.Entered));
            Assert.That(_map.RoomShown(1), Is.EqualTo(MapShown.Here));
            Assert.That(_map.RoomShown(2), Is.EqualTo(MapShown.Ahead));
            for (int i = 3; i < layout.Rooms.Count; i++) Assert.That(_map.RoomShown(i), Is.EqualTo(MapShown.Hidden), "room " + i + " not yet");
            Assert.That(_map.DoorShown(1), Is.True);
            Assert.That(_map.DoorShown(2), Is.False);
            // The room ahead is a treasure room: its gold and its sign, ahead.
            LevelRoom ahead = layout.Rooms[2];
            Assert.That(_map.SignStrength(2), Is.EqualTo(ahead.Kind == RoomKind.Treasure || ahead.Elite ? 1f : 0f));

            // Small, at the top right; large over the middle while held.
            Rect small = _map.SmallRect;
            Assert.That(small.height, Is.LessThanOrEqualTo(SkillBar.LayoutHeight / 6f + 0.01f), "about a sixth of the view's height");
            Assert.That(small.y, Is.LessThan(20f), "at the top");
            Assert.That(small.center.x, Is.GreaterThan(_map.LargeRect.center.x), "on the right");
            Assert.That(_map.Large, Is.False);
            _map.HoldLarge = true;
            _map.Tick();
            Assert.That(_map.Large, Is.True, "held, large");
            Assert.That(_map.LargeRect.height, Is.GreaterThan(small.height * 3f));
            _map.HoldLarge = false;
        }

        [UnityTest]
        public IEnumerator TheMap_IsNotDrawn_UnderThePauseMenu_OrAnEndScreen_AndThePauseMenuNamesItsKey([Values(false, true)] bool passages)
        {
            _hud = RunHud.Ensure();
            StartRun(passages);
            yield return Frames(2);
            Assert.That(_map.Shown, Is.True);
            Assert.That(_levels.Pause(), Is.True);
            _map.HoldLarge = true;
            _map.Tick();
            Assert.That(_map.Shown, Is.False, "not under the pause menu");
            Assert.That(_map.Large, Is.False);
            _map.HoldLarge = false;
            Assert.That(_hud.KeyLines(), Has.Member("Map  Tab  (hold)"), "the pause menu's list of keys names it");
            Assert.That(_levels.Resume(), Is.True);
            yield return Frames(2);
            Assert.That(_map.Shown, Is.True);

            _player.GetComponent<PlayerHealth>().TakeHit(100000);
            yield return Until(() => _levels.Ending != GeneratedLevels.EndKind.None, 60);
            _map.Tick();
            Assert.That(_map.Shown, Is.False, "not under an end screen");

            _levels.BackToPracticeRoom();
            yield return Frames(2);
            Assert.That(_map.Shown, Is.False, "nor outside a floor");
        }

        [UnityTest]
        public IEnumerator TheLights_StayInsideTheBudget_OnFloor5()
        {
            _levels.Values = new LevelValues { Passages = false }; // work order 059: as StartRun
            _levels.Values.Crypt.LanternLights = true;
            GeneratedLevel level = _levels.Build(0x5EED5UL, 5);
            yield return Frames(2);
            CryptKitValues kit = level.Values.Crypt;
            int budget = kit.LightsPerRoom + level.Values.Signs.LightsMost;
            var signs = level.GetComponent<DoorSigns>();
            Assert.That(signs.SignInto(level.Layout.ExitRoom.Order).Kind, Is.EqualTo(DoorSign.Boss), "floor 5's exit door says the boss");
            int mostOn = 0;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                yield return Into(level, room);
                int on = level.GetComponentsInChildren<Light>().Count(l => l.enabled);
                mostOn = Mathf.Max(mostOn, on);
                Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
            }
            int made = level.GetComponentsInChildren<Light>(true).Length;
            int lanterns = level.KitLights != null ? level.KitLights.ByRoom.Values.Sum(l => l.Count) : 0;
            Assert.That(made - lanterns, Is.LessThanOrEqualTo(1), "the door signs make one light for the floor at most");
            Assert.That(signs.DoorLight, Is.Not.Null, "lit at some doorway");
            Debug.Log("LIGHTBUDGET floor 5: " + made + " lights made (" + lanterns + " lanterns'), at most " + mostOn + " on at once, the budget " + budget);

            // With the lanterns' lights off (the default) the doorway's is the only one.
            _levels.Values = new LevelValues { Passages = false };
            GeneratedLevel plain = _levels.Build(0x5EED5UL, 5);
            yield return Frames(2);
            foreach (LevelRoom room in plain.Layout.Rooms)
            {
                yield return Into(plain, room);
                Assert.That(plain.GetComponentsInChildren<Light>().Count(l => l.enabled), Is.LessThanOrEqualTo(1), room.Letter + room.Order.ToString());
            }
        }

        [UnityTest]
        public IEnumerator TheirFrames_AllocateNothing([Values(false, true)] bool passages)
        {
            GeneratedLevel level = StartRun(passages);
            yield return null;
            LevelRoom treasure = level.Layout.Rooms.First(r => r.Kind == RoomKind.Treasure);
            var signs = level.GetComponent<DoorSigns>();
            yield return Into(level, level.Layout.Rooms[signs.SignInto(treasure.Order).Before]);
            Assert.That(signs.Signs.Count(x => x.Lit), Is.EqualTo(1), "a sign lit while the frames are measured");
            signs.Tick();
            _map.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) signs.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the signs' frame allocates nothing");
            Assert.That(() => { for (int i = 0; i < 300; i++) _map.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the map's frame allocates nothing");
        }

        [UnityTest]
        public IEnumerator EachSwitchOff_RemovesItsPart()
        {
            // The line off: version 9's floor, its treasure rooms off the sides.
            GeneratedLevel sides = StartRun(v => v.InALine = false);
            yield return Frames(2);
            Assert.That(sides.Layout.InALine, Is.False);
            Assert.That(sides.Layout.Rooms.Any(r => sides.Layout.DoorsOf(r.Order).Count() == 3), "a combat room with a side room");
            Assert.That(sides.Layout.Describe().Replace("version " + LevelGenerator.Version + " ", "version 9 "),
                        Is.EqualTo(LevelGenerator.Generate(sides.Layout.Seed, 1, new LevelValues { InALine = false }).Describe().Replace("version " + LevelGenerator.Version + " ", "version 9 ")));

            // The door signs off: none, and no light of theirs.
            GeneratedLevel plain = StartRun(v => v.DoorSigns = false);
            yield return Frames(2);
            Assert.That(plain.GetComponent<DoorSigns>(), Is.Null);
            Assert.That(plain.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("DoorSign")), Is.EqualTo(0));
            Assert.That(plain.GetComponentsInChildren<Light>(true), Is.Empty);

            // The map off: not drawn.
            StartRun(v => v.FloorMap = false);
            yield return Frames(2);
            Assert.That(_map.Shown, Is.False);
            _map.HoldLarge = true;
            _map.Tick();
            Assert.That(_map.Large, Is.False);
            _map.HoldLarge = false;

            // The kit off: the signs still show, over the gap.
            GeneratedLevel grey = StartRun(v => v.CryptKit = false);
            yield return Frames(2);
            var signs = grey.GetComponent<DoorSigns>();
            Assert.That(signs, Is.Not.Null);
            Assert.That(signs.Signs.Count, Is.EqualTo(4));
            Assert.That(signs.Signs.All(s => s.Renderer.enabled && s.Object.transform.position.y > grey.Values.WallHeight), "over the doorways, without the kit");
        }
    }
}
