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
    /// The room themes as built (work order 028; Kristian, 2026-11-19 and 2026-11-20: "good variety", and more of the
    /// crypt), in the ControlRoom scene through <see cref="GeneratedLevels"/>: on a floor 1 and a floor 5 of two seeds,
    /// every prop of the plan stands where the plan says, turned as it says, the wall pieces' backs a hand from the wall,
    /// the props with an emission map glow, the slender ones carry no <see cref="Cover"/> and no <see cref="Shelter"/>,
    /// and she can walk from the entrance to every room's middle; a slender prop stops no shot where a tall one does; and
    /// entering a room tells the play session recorder its theme.
    /// </summary>
    public class ThemedLevelTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;

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
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        GeneratedLevel Build(ulong seed, int floor)
        {
            _levels = _levels != null ? _levels : GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null, "a level was built");
            Assert.That(level.Layout.Floor, Is.EqualTo(floor));
            return level;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static ulong Seed(string text)
        {
            Assert.That(LevelSeed.TryParse(text, out ulong seed), Is.True);
            return seed;
        }

        static bool Glows(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m != null && m.IsKeywordEnabled("_EMISSION") && m.GetTexture("_EmissionMap") != null && m.GetColor("_EmissionColor").maxColorComponent > 0.01f;
        }

        /// <summary>Metres from a wall's line to the nearest point of the model's drawn bounds, into the room.</summary>
        static float BackFromLine(GeneratedLevel level, LevelRoom room, int side, Bounds b)
        {
            float cs = level.Layout.CellSize;
            Vector3 low = level.ToWorld(new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs));
            Vector3 high = level.ToWorld(new LevelPoint(room.Rect.Right * cs, room.Rect.Top * cs));
            switch (side)
            {
                case LevelGenerator.North: return high.z - b.max.z;
                case LevelGenerator.South: return b.min.z - low.z;
                case LevelGenerator.East: return high.x - b.max.x;
                default: return b.min.x - low.x;
            }
        }

        /// <summary>How far a model's back is from a carved cell's face (work order 051), into the room.</summary>
        static float BackFromFace(GeneratedLevel level, WallFace face, Bounds b)
        {
            float cs = level.Layout.CellSize;
            Vector3 at = level.ToWorld(new LevelPoint(face.Line * cs, face.Line * cs));
            switch (face.Side)
            {
                case LevelGenerator.North: return at.z - b.max.z;
                case LevelGenerator.South: return b.min.z - at.z;
                case LevelGenerator.East: return at.x - b.max.x;
                default: return b.min.x - at.x;
            }
        }

        [UnityTest]
        public IEnumerator FloorsOneAndFive_OfTwoSeeds_EveryPropStandsAsThePlanSays_AndEveryRoomCanBeReached()
        {
            var models = new HashSet<PropModel>();
            var themes = new HashSet<RoomTheme>();
            int wallPieces = 0, glowing = 0, slender = 0;
            foreach (ulong seed in new[] { 42UL, Seed("WALKTHRU") })
                foreach (int floor in new[] { 1, 5 })
                {
                    GeneratedLevel level = Build(seed, floor);
                    yield return null;
                    LevelLayout layout = level.Layout;
                    string where = "seed " + layout.SeedText + " floor " + floor;
                    PropValues pv = level.Values.PropRules;
                    // Changed deliberately (work order 052): but those a large prop stands in place of (the throne's pair, a shrine's basin).
                    List<LevelProp> described = layout.Rooms.SelectMany(r => r.Props).Where(p => !level.Replaced.Contains(p)).ToList();
                    Assert.That(level.Props.Count, Is.EqualTo(described.Count), where + ": a prop built for each of the plan");
                    Assert.That(layout.Rooms.Where(r => r.Kind != RoomKind.Entrance).All(r => r.Theme != RoomTheme.None), where + ": every room but the entrance has a theme");
                    foreach (LevelRoom r in layout.Rooms) themes.Add(r.Theme);

                    for (int i = 0; i < described.Count; i++)
                    {
                        GeneratedLevel.PlacedProp placed = level.Props[i];
                        LevelProp p = described[i];
                        models.Add(p.Model);
                        string what = where + " " + placed.Object.name + " " + p;
                        Assert.That(placed.Prop, Is.SameAs(p), what);
                        Transform t = placed.Object.transform;
                        Assert.That(Flat(t.position, level.ToWorld(p.Position)), Is.LessThan(0.001f), "at its place: " + what);
                        Assert.That(t.position.y, Is.EqualTo(0f).Within(0.001f), "on the ground: " + what);
                        Assert.That(Mathf.DeltaAngle(t.eulerAngles.y, p.Rotation), Is.EqualTo(0f).Within(0.01f), "turned: " + what);
                        Assert.That(placed.Object.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1), "one collider: " + what);
                        Renderer renderer = placed.Object.GetComponentInChildren<Renderer>();
                        Assert.That(renderer, Is.Not.Null, "the model is there: " + what);
                        // Changed deliberately (work order 052): one that stands on the dais model stands on its top.
                        Assert.That(renderer.bounds.min.y, Is.EqualTo(level.Raised.Contains(p) ? (float)PropSize.DaisShows : 0f).Within(0.03f), "standing on the floor: " + what);

                        PropSize size = PropSize.Of(p.Model);
                        Assert.That(Glows(renderer), Is.EqualTo(size.Emission), "glows when it has an emission map: " + what);
                        if (size.Emission) glowing++;
                        if (p.Slender)
                        {
                            slender++;
                            Assert.That(placed.Object.GetComponentInChildren<Cover>(), Is.Null, "a slender prop is not cover: " + what);
                            Assert.That(placed.Object.GetComponentInChildren<Shelter>(), Is.Null, "nor shelter: " + what);
                            Assert.That(placed.IsCover, Is.False, what);
                        }
                        else
                        {
                            Assert.That(placed.Object.GetComponent<Shelter>(), Is.Not.Null, "every other prop is shelter: " + what);
                            Assert.That(placed.Object.GetComponent<Cover>() != null, Is.EqualTo(p.IsCover(pv)), "cover when tall: " + what);
                        }
                        if (size.WallPiece)
                        {
                            // As drawn: the model's back within a hand of the wall's face (work order 028), facing into the room.
                            wallPieces++;
                            LevelRoom room = placed.Room;
                            // Changed deliberately (work order 051): its wall may be a carved cell's face (an alcove's back).
                            WallFace face = RoomShapes.NearestFace(room, p.Footprint, layout.CellSize, out _);
                            int side = face.Side;
                            float back = (face.Carved ? BackFromFace(level, face, renderer.bounds) : BackFromLine(level, room, side, renderer.bounds)) - pv.WallFace;
                            Assert.That(back, Is.InRange(-0.03f, pv.HandWidth + 1e-3f), "its back within a hand of the wall's face: " + what);
                            LevelPoint into = PropRules.Inward(side), front = p.Front;
                            Assert.That(front.X * into.X + front.Y * into.Y, Is.GreaterThan(0.99), "facing into the room: " + what);
                            Vector3 frontWorld = t.rotation * (size.FrontMinusZ ? Vector3.back : Vector3.forward);
                            Assert.That(Vector3.Dot(frontWorld, new Vector3((float)into.X, 0f, (float)into.Y)), Is.GreaterThan(0.99f), "and so turned in the game: " + what);
                        }
                    }

                    // She can walk from the entrance to every room's middle.
                    Assert.That(NavMesh.SamplePosition(level.PlayerStart, out NavMeshHit start, 1f, NavMesh.AllAreas), where + ": NavMesh at the start");
                    var path = new NavMeshPath();
                    foreach (LevelRoom r in layout.Rooms)
                    {
                        Assert.That(NavMesh.SamplePosition(level.Middle(r), out NavMeshHit middle, 1f, NavMesh.AllAreas), where + ": NavMesh at the middle of " + r.Letter + r.Order);
                        Assert.That(NavMesh.CalculatePath(start.position, middle.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                    where + ": the whole way to " + r.Letter + r.Order + " (" + RoomThemes.Word(r.Theme) + ")\n" + LevelMap.Draw(layout));
                    }
                    TestContext.WriteLine("THEMEDLEVEL " + LevelMap.Draw(layout));
                }
            TestContext.WriteLine(string.Format(s_Inv, "THEMEDLEVEL models {0}; themes {1}; wall pieces {2}, glowing props {3}, slender {4}",
                                                string.Join(", ", models.OrderBy(m => m)), string.Join(", ", themes.OrderBy(t => t).Select(RoomThemes.Word)), wallPieces, glowing, slender));
            Assert.That(wallPieces, Is.GreaterThan(0), "wall pieces were seen");
            Assert.That(slender, Is.GreaterThan(0), "slender props were seen");
            Assert.That(themes, Does.Contain(RoomTheme.ThroneRoom));
        }

        /// <summary>Two points either side of a prop through its middle, the line between them clear of every other prop of its room and of the walls; false if none.</summary>
        static bool LineThrough(GeneratedLevel level, GeneratedLevel.PlacedProp placed, out Vector3 from, out Vector3 to)
        {
            LevelProp p = placed.Prop;
            double reach = p.Shape == PropShape.Circle ? p.Radius : 0.5 * Math.Sqrt(p.Width * p.Width + p.Depth * p.Depth);
            for (int k = 0; k < 16; k++)
            {
                double a = k * Math.PI / 8;
                var dir = new LevelPoint(Math.Sin(a), Math.Cos(a));
                var a0 = new LevelPoint(p.Position.X - dir.X * (reach + 0.6), p.Position.Y - dir.Y * (reach + 0.6));
                var a1 = new LevelPoint(p.Position.X + dir.X * (reach + 0.6), p.Position.Y + dir.Y * (reach + 0.6));
                if (LevelGeometry.DistanceToWalls(a0, placed.Room.Rect, level.Layout.CellSize) < 0.4 || LevelGeometry.DistanceToWalls(a1, placed.Room.Rect, level.Layout.CellSize) < 0.4) continue;
                // Changed deliberately (work order 052): clear of the room's large props too.
                if (!placed.Room.Props.Concat(level.LargeProps.Where(l => l.Room == placed.Room).Select(l => l.Prop)).All(o => o == p || o.Footprint.DistanceTo(a0, a1) > 0.1)) continue;
                from = level.ToWorld(a0);
                to = level.ToWorld(a1);
                return true;
            }
            from = to = default;
            return false;
        }

        [UnityTest]
        public IEnumerator ASlenderProp_StopsNoShot_WhereATallOneDoes()
        {
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = null;
            GeneratedLevel.PlacedProp candle = null, tall = null;
            Vector3 cFrom = default, cTo = default, tFrom = default, tTo = default;
            for (ulong s = 42; s < 62 && candle == null; s++)
            {
                LevelLayout plan = LevelGenerator.Generate(s, 1, _levels.Values);
                if (!plan.Rooms.SelectMany(r => r.Props).Any(p => p.Slender)) continue;
                level = Build(s, 1);
                yield return null;
                foreach (GeneratedLevel.PlacedProp placed in level.Props)
                {
                    if (candle == null && placed.Prop.Slender && LineThrough(level, placed, out cFrom, out cTo)) candle = placed;
                    // Changed deliberately (work order 058: seed 42's floor is another, and its first tall prop a grave marker,
                    // which the quick duck's sort of work order 043 makes stop nothing): a tall prop that stops shots.
                    if (tall == null && placed.IsCover && placed.Object.GetComponent<Cover>() is Cover c && c.Stops && LineThrough(level, placed, out tFrom, out tTo)) tall = placed;
                }
            }
            Assert.That(candle, Is.Not.Null, "a slender prop with a clear line through it in seeds 42 to 61");
            Assert.That(tall, Is.Not.Null, "and a tall one");
            yield return null;

            float ClearAlong(Vector3 a, Vector3 b)
            {
                Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);
                return Cover.ClearDistance(new Vector3(a.x, 0f, a.z), d.normalized, d.magnitude, Cover.RayHeight);
            }
            float candleLine = Flat(cFrom, cTo), tallLine = Flat(tFrom, tTo);
            Assert.That(ClearAlong(cFrom, cTo), Is.EqualTo(candleLine).Within(1e-3f), "a shot's line goes through the slender " + candle.Prop);
            Assert.That(ClearAlong(tFrom, tTo), Is.LessThan(tallLine - 0.5f), "and stops at the tall " + tall.Prop);
            Assert.That(Shelter.All.Any(sh => sh.gameObject == candle.Object), Is.False, "she cannot take cover against the slender one");
            Assert.That(Shelter.All.Any(sh => sh.gameObject == tall.Object), Is.True, "but can against the tall one");
            // And it still blocks walking.
            Assert.That(NavMesh.SamplePosition(level.ToWorld(candle.Prop.Position), out _, 0.3f, NavMesh.AllAreas), Is.False, "no floor to walk under " + candle.Prop);
        }

        [UnityTest]
        public IEnumerator EnteringARoom_TellsTheRecorderItsTheme()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ThemedLevelTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = Build(42UL, 1);
            yield return null;
            LevelRoom combat = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat);
            Assert.That(combat.Theme, Is.Not.EqualTo(RoomTheme.None));
            _player.Teleport(level.Middle(combat));
            yield return null;
            yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(combat));
            var entered = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomEntered).ToList();
            Assert.That(entered.Select(e => e.Name), Is.EqualTo(new[] { "entrance", "combat" }));
            Assert.That(entered[0].ExtraValue("theme"), Is.Null.Or.Empty, "the entrance has none");
            Assert.That(entered[1].ExtraValue("theme"), Is.EqualTo(RoomThemes.Word(combat.Theme)));
        }
    }
}
