using System.Collections;
using System.Collections.Generic;
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
    /// The props of a generated level as built (work order 009), in the ControlRoom scene through the intents of
    /// <see cref="GeneratedLevels"/>: every prop of the description stands at its place, turned, with a collider of its
    /// footprint on the walls' layer; the NavMesh goes round them, so she cannot walk through a pillar or a low prop;
    /// every room can still be reached; a line at the shooter's sight height stops at them as at a wall; a level built
    /// with props off has none; and they go when the level goes.
    /// </summary>
    public class GeneratedLevelPropsTests
    {
        const ulong Seed = 42UL;

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
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        GeneratedLevel Build(ulong seed = Seed, bool props = true)
        {
            _levels = GeneratedLevels.Ensure();
            _levels.Values.Props = props;
            GeneratedLevel level = _levels.Build(seed);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static Vector3 Ground(Vector3 p, string what, LevelLayout layout)
        {
            Assert.That(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas), "NavMesh at " + what + " " + p + "\n" + LevelMap.Draw(layout));
            return hit.position;
        }

        static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        [UnityTest]
        public IEnumerator EveryProp_StandsAtItsPlace_Turned_WithAColliderOfItsFootprint_OnTheWallsLayer()
        {
            GeneratedLevel level = Build();
            yield return null;
            LevelLayout layout = level.Layout;
            List<LevelProp> described = layout.Rooms.SelectMany(r => r.Props).ToList();
            Assert.That(described.Count, Is.GreaterThan(10));
            Assert.That(level.Props.Count, Is.EqualTo(described.Count), "a prop built for each of the description");
            Assert.That(level.LowestPropCollider, Is.GreaterThan(0.75f), "over the agent's step");

            int wallLayer = level.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Wall_")).gameObject.layer;
            Assert.That(level.WallLayer, Is.EqualTo(wallLayer));
            for (int i = 0; i < described.Count; i++)
            {
                GeneratedLevel.PlacedProp placed = level.Props[i];
                LevelProp p = described[i];
                string what = placed.Object.name + " " + p;
                Assert.That(placed.Prop, Is.SameAs(p), what);
                Transform t = placed.Object.transform;
                Assert.That(t.IsChildOf(level.transform), what);
                Assert.That(Flat(t.position, level.ToWorld(p.Position)), Is.LessThan(0.001f), "at its place: " + what);
                Assert.That(t.position.y, Is.EqualTo(0f).Within(0.001f), "on the ground: " + what);
                Assert.That(Mathf.DeltaAngle(t.eulerAngles.y, p.Rotation), Is.EqualTo(0f).Within(0.01f), "turned: " + what);
                Assert.That(placed.Object.layer, Is.EqualTo(wallLayer), "on the walls' layer: " + what);

                Collider[] colliders = placed.Object.GetComponentsInChildren<Collider>();
                Assert.That(colliders.Length, Is.EqualTo(1), "one collider: " + what);
                Assert.That(colliders[0], Is.Not.InstanceOf<MeshCollider>(), what);
                float height = Mathf.Max((float)p.Height, level.LowestPropCollider);
                if (colliders[0] is BoxCollider box)
                {
                    float w = p.Shape == PropShape.Circle ? 2f * (float)p.Radius : (float)p.Width;
                    float d = p.Shape == PropShape.Circle ? 2f * (float)p.Radius : (float)p.Depth;
                    Assert.That(box.size.x, Is.EqualTo(w).Within(1e-4f), what);
                    Assert.That(box.size.z, Is.EqualTo(d).Within(1e-4f), what);
                    Assert.That(box.size.y, Is.EqualTo(height).Within(1e-4f), what);
                    Assert.That(box.center.y, Is.EqualTo(height * 0.5f).Within(1e-4f), what);
                }
                else
                {
                    var capsule = (CapsuleCollider)colliders[0];
                    Assert.That(p.Shape, Is.EqualTo(PropShape.Circle), what);
                    Assert.That(capsule.radius, Is.EqualTo((float)p.Radius).Within(1e-4f), what);
                    Assert.That(capsule.height, Is.EqualTo(height).Within(1e-4f), what);
                    Assert.That(capsule.direction, Is.EqualTo(1), what);
                }

                Renderer r = placed.Object.GetComponentInChildren<Renderer>();
                Assert.That(r, Is.Not.Null, "the model is there: " + what);
                Assert.That(r.sharedMaterial.shader.name, Is.EqualTo(PropFactory.ShaderName), what);
                Assert.That(r.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null, "its painted texture: " + what);
                Assert.That(r.bounds.min.y, Is.EqualTo(0f).Within(0.03f), "standing on the floor: " + what);

                // No NavMesh where it stands.
                Assert.That(NavMesh.SamplePosition(level.ToWorld(p.Position), out _, 0.3f, NavMesh.AllAreas), Is.False, "no floor to walk under " + what);
            }
        }

        /// <summary>Two points on the NavMesh either side of a prop; the straight line between them is blocked and the way round is longer.</summary>
        static void AssertGoesRound(GeneratedLevel level, LevelProp p)
        {
            LevelLayout layout = level.Layout;
            PropFootprint f = p.Footprint;
            double reach = p.Shape == PropShape.Circle ? p.Radius : 0.5 * System.Math.Sqrt(p.Width * p.Width + p.Depth * p.Depth);
            float off = (float)reach + 1.2f;
            Vector3 centre = level.ToWorld(p.Position);
            var path = new NavMeshPath();
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                Vector3 a = centre - dir * off, b = centre + dir * off;
                if (!NavMesh.SamplePosition(a, out NavMeshHit ha, 0.2f, NavMesh.AllAreas)) continue;
                if (!NavMesh.SamplePosition(b, out NavMeshHit hb, 0.2f, NavMesh.AllAreas)) continue;
                Assert.That(NavMesh.Raycast(ha.position, hb.position, out _, NavMesh.AllAreas), Is.True, "the straight line through " + p + " is blocked");
                Assert.That(NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                            "a way round " + p + "\n" + LevelMap.Draw(layout));
                float straight = Flat(ha.position, hb.position);
                Assert.That(PathLength(path), Is.GreaterThan(straight + 0.3f), "the way round " + p + " is longer than the straight line");
                Assert.That(f.DistanceTo(level.ToLevel(path.corners[path.corners.Length / 2])), Is.GreaterThan(0.0), "and stays off it");
                return;
            }
            Assert.Fail("no two points on the NavMesh either side of " + p + "\n" + LevelMap.Draw(layout));
        }

        static (GeneratedLevel level, LevelProp prop) FindIn(GeneratedLevels levels, PropModel model, PropPlace place)
        {
            for (ulong s = Seed; s < Seed + 20; s++)
            {
                LevelLayout layout = LevelGenerator.Generate(s, levels.Values);
                LevelProp p = layout.Rooms.SelectMany(r => r.Props).FirstOrDefault(x => x.Model == model && x.Place == place);
                if (p == null) continue;
                GeneratedLevel level = levels.Build(s);
                return (level, level.Layout.Rooms.SelectMany(r => r.Props).First(x => x.Model == model && x.Place == place));
            }
            Assert.Fail("no " + model + " in seeds " + Seed + " to " + (Seed + 19));
            return (null, null);
        }

        [UnityTest]
        public IEnumerator SheCannotWalkThroughAPillar_TheWayGoesRoundIt()
        {
            _levels = GeneratedLevels.Ensure();
            (GeneratedLevel level, LevelProp pillar) = FindIn(_levels, PropModel.Pillar, PropPlace.Floor);
            yield return null;
            AssertGoesRound(level, pillar);

            // And walking at it: she stops at it, not through it.
            Vector3 centre = level.ToWorld(pillar.Position);
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                Vector3 from = centre - dir * ((float)pillar.Radius + 1.5f);
                if (!NavMesh.SamplePosition(from, out NavMeshHit hit, 0.2f, NavMesh.AllAreas)) continue;
                Assert.That(NavMesh.Raycast(hit.position, centre + dir * ((float)pillar.Radius + 1.5f), out NavMeshHit stop, NavMesh.AllAreas), Is.True);
                Assert.That(Flat(stop.position, centre), Is.GreaterThan((float)pillar.Radius), "a straight walk at it stops outside it");
                break;
            }
        }

        [UnityTest]
        public IEnumerator LowProps_BlockMovement_LikeTheRest()
        {
            _levels = GeneratedLevels.Ensure();
            foreach (PropModel model in new[] { PropModel.Chest, PropModel.Rubble, PropModel.BonePile })
            {
                PropPlace place = model == PropModel.Chest ? PropPlace.Set : model == PropModel.Rubble ? PropPlace.Floor : PropPlace.Wall;
                (GeneratedLevel level, LevelProp p) = FindIn(_levels, model, place);
                yield return null;
                Assert.That(NavMesh.SamplePosition(level.ToWorld(p.Position), out _, 0.3f, NavMesh.AllAreas), Is.False, "no NavMesh on " + p);
                if (place != PropPlace.Wall) AssertGoesRound(level, p);
            }
        }

        [UnityTest]
        public IEnumerator EveryRoom_IsStillReachable_WithProps()
        {
            foreach (ulong seed in new[] { 1UL, 7UL, 12345UL, 0xDEADBEEFUL })
            {
                GeneratedLevel level = Build(seed);
                yield return null;
                LevelLayout layout = level.Layout;
                Assert.That(level.Props.Count, Is.GreaterThan(0));
                Vector3 start = Ground(level.PlayerStart, "the start", layout);
                var path = new NavMeshPath();
                foreach (LevelRoom r in layout.Rooms)
                {
                    Vector3 to = Ground(level.Middle(r), "the middle of " + r.Letter + r.Order, layout);
                    Assert.That(NavMesh.CalculatePath(start, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                "seed " + seed + ": the whole way to " + r.Letter + r.Order + "\n" + LevelMap.Draw(layout));
                }
                // And to every enemy, which stands where its point is.
                foreach (Enemy e in level.Enemies)
                {
                    Vector3 to = Ground(e.Home, "an enemy's home", layout);
                    Assert.That(NavMesh.CalculatePath(start, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                "seed " + seed + ": the way to " + e.name);
                }
            }
        }

        /// <summary>
        /// Where a prop's collider stands on the ground (PropFactory): its footprint, but the square round a circle lower than
        /// it is wide. Work order 044: an ossuary's piles stand side by side at its far wall, where a line can clear one's
        /// circle and still meet the corner of its square.
        /// </summary>
        static PropFootprint ColliderOf(LevelProp o, float lowest)
        {
            PropFootprint f = o.Footprint;
            if (o.Shape == PropShape.Circle && System.Math.Max(o.Height, lowest) < 2 * o.Radius)
            {
                f.Shape = PropShape.Rectangle;
                f.HalfWidth = f.HalfDepth = o.Radius;
            }
            return f;
        }

        [UnityTest]
        public IEnumerator AShootersLineOfSight_StopsAtAProp_AsAtAWall()
        {
            GeneratedLevel level = Build();
            yield return null;
            float sight = new ShooterValues().SightHeight;
            LevelLayout layout = level.Layout;
            foreach (GeneratedLevel.PlacedProp placed in level.Props)
            {
                // From a point in its room, clear of the walls and of every other prop, with nothing else on the line.
                LevelProp p = placed.Prop;
                double reach = p.Shape == PropShape.Circle ? p.Radius : 0.5 * System.Math.Sqrt(p.Width * p.Width + p.Depth * p.Depth);
                LevelPoint start = default;
                bool found = false;
                for (int k = 0; k < 16 && !found; k++)
                {
                    double a = k * System.Math.PI / 8;
                    start = new LevelPoint(p.Position.X + System.Math.Sin(a) * (reach + 1.0), p.Position.Y + System.Math.Cos(a) * (reach + 1.0));
                    found = LevelGeometry.DistanceToWalls(start, placed.Room.Rect, layout.CellSize) > 0.3
                            && placed.Room.Props.All(o => o == p || ColliderOf(o, level.LowestPropCollider).DistanceTo(start, p.Position) > 0.05);
                }
                Assert.That(found, "a clear place beside " + p);
                Vector3 centre = level.ToWorld(p.Position) + Vector3.up * sight;
                Vector3 from = level.ToWorld(start) + Vector3.up * sight;
                Vector3 dir = (centre - from).normalized;
                Assert.That(Physics.Raycast(from, dir, out RaycastHit hit, Vector3.Distance(from, centre), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                            "a line at the shooter's sight height meets " + placed.Prop);
                Assert.That(hit.collider.transform.IsChildOf(placed.Object.transform) || hit.collider.gameObject == placed.Object, Is.True,
                            "it stops at the prop: " + placed.Prop + ", hit " + hit.collider.name);
                Assert.That(hit.collider.GetComponentInParent<Damageable>(), Is.Null);
                Assert.That(hit.collider.GetComponentInParent<Enemy>(), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator PropsOff_BuildsTheLevelWithout()
        {
            GeneratedLevel level = Build(Seed, props: false);
            yield return null;
            Assert.That(level.Props, Is.Empty);
            Assert.That(level.Layout.Rooms.Sum(r => r.Props.Count), Is.EqualTo(0));
            Assert.That(level.GetComponentsInChildren<Collider>().Length, Is.EqualTo(level.Floors + level.Walls));
            Assert.That(level.Layout.Describe(props: false), Is.EqualTo(LevelGenerator.Generate(Seed, new LevelValues()).Describe(props: false)),
                        "the same rooms, doors and enemies as with props");
        }

        [UnityTest]
        public IEnumerator Props_GoWithTheLevel()
        {
            GeneratedLevel level = Build();
            yield return null;
            List<GameObject> props = level.Props.Select(p => p.Object).ToList();
            Assert.That(props.Count, Is.GreaterThan(0));
            List<Material> materials = props.Select(p => p.GetComponentInChildren<Renderer>().sharedMaterial).Distinct().ToList();

            GeneratedLevel again = _levels.Rebuild();
            yield return null;
            Assert.That(props.All(p => p == null), "a rebuild removes the old level's props");
            Assert.That(materials.All(m => m == null), "and their materials");
            Assert.That(again.Props.Count, Is.EqualTo(props.Count));

            props = again.Props.Select(p => p.Object).ToList();
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(props.All(p => p == null), "going back removes them");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("Prop_")), Is.EqualTo(0), "none left anywhere");
        }
    }
}
