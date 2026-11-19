using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The eleven Crypt props as Unity imports them (work order 009): each loads by name from Resources, is as high as
    /// it was made, stands on the floor with its pivot in the middle of its footprint, has no turned or scaled root,
    /// and has a material with its painted texture. Writes a line per model with what Unity made of it ("PROPIMPORT").
    /// </summary>
    public class CryptPropImportTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The heights the models were made to, in metres (from the work order).</summary>
        static readonly Dictionary<PropModel, double> s_madeHeight = new Dictionary<PropModel, double>
        {
            [PropModel.Altar] = 1.3, [PropModel.BonePile] = 0.6, [PropModel.Brazier] = 1.1, [PropModel.Chest] = 0.9,
            [PropModel.GraveMarker] = 1.3, [PropModel.Pillar] = 3.0, [PropModel.PillarBroken] = 1.2, [PropModel.Rubble] = 0.8,
            [PropModel.Sarcophagus] = 1.0, [PropModel.Statue] = 2.6, [PropModel.Urns] = 1.1,
        };

        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (Object o in _made)
                if (o != null) Object.Destroy(o);
            _made.Clear();
            PlayerInputDriver.ReadRealDevices = true;
        }

        static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static string V(Vector3 v) => string.Format(s_Inv, "({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);

        [Test]
        public void EveryModel_Loads_IsItsHeight_StandsOnTheFloor_AndHasItsTexture()
        {
            var report = new StringBuilder();
            var faults = new List<string>();
            foreach (PropSize size in PropSize.Crypt)
            {
                string name = size.Name;
                GameObject source = Resources.Load<GameObject>(size.ResourcePath);
                if (source == null)
                {
                    faults.Add(name + " does not load from Resources/" + size.ResourcePath);
                    continue;
                }

                // As Unity imported it: the root's own transform, the meshes, the materials.
                Vector3 rootEuler = source.transform.localEulerAngles, rootScale = source.transform.localScale;
                MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
                Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
                long triangles = 0;
                foreach (MeshFilter f in filters)
                    if (f.sharedMesh != null)
                        for (int s = 0; s < f.sharedMesh.subMeshCount; s++) triangles += f.sharedMesh.GetIndexCount(s) / 3;
                Material imported = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
                Texture importedTexture = imported == null ? null
                    : imported.HasProperty("_BaseMap") && imported.GetTexture("_BaseMap") != null ? imported.GetTexture("_BaseMap") : imported.mainTexture;
                Texture2D fileTexture = Resources.Load<Texture2D>(size.TexturePath);

                // Placed as it comes, at the origin, unturned.
                GameObject raw = Object.Instantiate(source, Vector3.zero, source.transform.rotation);
                _made.Add(raw);
                Bounds b = WorldBounds(raw);
                Transform[] nodes = source.GetComponentsInChildren<Transform>(true);

                report.AppendFormat(s_Inv,
                    "PROPIMPORT {0}: bounds size {1} min y {2:0.###} centre {3}; triangles {4}; meshes {5}, renderers {6}, nodes {7}; " +
                    "root rotation {8} scale {9}; child rotations [{10}]; material {11} shader {12}, its texture {13}; texture file {14} {15}x{16}\n",
                    name, V(b.size), b.min.y, V(b.center), triangles, filters.Length, renderers.Length, nodes.Length,
                    V(rootEuler), V(rootScale),
                    string.Join("; ", nodes.Skip(1).Select(t => t.name + " " + V(t.localEulerAngles) + " x" + V(t.localScale))),
                    imported != null ? imported.name : "none", imported != null ? imported.shader.name : "-",
                    importedTexture != null ? importedTexture.name : "none",
                    fileTexture != null ? fileTexture.name : "none", fileTexture != null ? fileTexture.width : 0, fileTexture != null ? fileTexture.height : 0);

                double made = s_madeHeight[size.Model];
                if (System.Math.Abs(b.size.y - made) > 0.05 * made) faults.Add($"{name} is {b.size.y:0.###} m high, made {made} m");
                if (System.Math.Abs(b.min.y) > 0.03f) faults.Add($"{name}'s lowest point is {b.min.y:0.###} m from its pivot's height");
                float slack = Mathf.Max(0.05f, 0.1f * Mathf.Max(b.size.x, b.size.z));
                if (new Vector2(b.center.x, b.center.z).magnitude > slack) faults.Add($"{name}'s pivot is {V(b.center)} from its footprint's middle");
                if (Quaternion.Angle(source.transform.localRotation, Quaternion.identity) > 0.5f) faults.Add($"{name}'s root is turned {V(rootEuler)}");
                if ((rootScale - Vector3.one).magnitude > 1e-3f) faults.Add($"{name}'s root is scaled {V(rootScale)}");
                if (filters.Length != 1 || renderers.Length != 1) faults.Add($"{name} has {filters.Length} meshes and {renderers.Length} renderers, not one");
                if (fileTexture == null) faults.Add($"{name}'s texture does not load from Resources/{size.TexturePath}");

                // The footprint of the rules' table holds the model's bounds; its height is the model's.
                double footW = size.Shape == PropShape.Circle ? 2 * size.Radius : size.Width;
                double footD = size.Shape == PropShape.Circle ? 2 * size.Radius : size.Depth;
                if (size.Shape == PropShape.Circle)
                {
                    float r = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x), Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                    if (r > size.Radius + 0.1) faults.Add($"{name}'s bounds reach {r:0.###} m from its pivot, its footprint's radius is {size.Radius}");
                }
                else if (b.size.x > footW + 0.1 || b.size.z > footD + 0.1)
                    faults.Add($"{name}'s bounds are {b.size.x:0.###} x {b.size.z:0.###} m, its footprint {footW} x {footD}");
                if (System.Math.Abs(size.Height - b.size.y) > 0.1) faults.Add($"{name}'s height in the table is {size.Height}, its bounds {b.size.y:0.###}");

                // Made as a level makes it: its material has the painted texture, it stands on the ground where put.
                Material material = PropFactory.MakeMaterial(size.Model);
                _made.Add(material);
                var prop = LevelProp.Make(size.Model, new LevelPoint(0, 0), 90, PropPlace.Set);
                GameObject built = PropFactory.Make(prop, new Vector3(-200f, 0f, 40f), null, 0, material, "Test_" + name);
                _made.Add(built);
                Renderer builtRenderer = built.GetComponentInChildren<Renderer>();
                Assert.That(builtRenderer, Is.Not.Null, name + " has a renderer as built");
                Texture baseMap = builtRenderer.sharedMaterial.HasProperty("_BaseMap") ? builtRenderer.sharedMaterial.GetTexture("_BaseMap") : builtRenderer.sharedMaterial.mainTexture;
                if (baseMap == null) faults.Add($"{name}'s material as built has no base colour texture");
                Bounds bb = WorldBounds(built);
                if (System.Math.Abs(bb.min.y) > 0.03f) faults.Add($"{name} as built stands {bb.min.y:0.###} m off the ground");
                if (System.Math.Abs(bb.size.y - made) > 0.05 * made) faults.Add($"{name} as built is {bb.size.y:0.###} m high");
                Assert.That(built.GetComponents<Collider>().Length, Is.EqualTo(1), name + ": one collider, on the prop");
                Assert.That(built.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1), name + ": none on the model");
                Assert.That(built.GetComponentInChildren<MeshCollider>(), Is.Null, name + ": no mesh collider");
            }
            TestContext.WriteLine(report.ToString());
            Debug.Log(report.ToString());
            Assert.That(faults, Is.Empty, string.Join("\n", faults) + "\n" + report);
        }
    }
}
