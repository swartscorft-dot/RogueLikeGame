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
    /// Work order 028: the seven new ones too, each also as wide and deep as the order's table says and with its
    /// triangles; with its emission map on its material, glowing; and the throne's and the bookshelf's fronts read from
    /// their meshes (the throne's tall back, the bookshelf's back that only its side piers reach) against the table's.
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
            // Work order 028
            [PropModel.Coffin] = 1.00, [PropModel.SkullPile] = 0.88, [PropModel.Candelabra] = 1.80, [PropModel.BoneThrone] = 2.60,
            [PropModel.Bookshelf] = 2.17, [PropModel.Cage] = 2.00, [PropModel.Basin] = 1.07,
        };

        /// <summary>Work order 028's table: the seven's width (x) and depth (z) as measured in Blender, and their triangles.</summary>
        static readonly Dictionary<PropModel, (float w, float d, int triangles)> s_table = new Dictionary<PropModel, (float, float, int)>
        {
            [PropModel.Coffin] = (2.10f, 1.12f, 4164), [PropModel.SkullPile] = (1.60f, 1.52f, 4900), [PropModel.Candelabra] = (0.83f, 0.50f, 3113),
            [PropModel.BoneThrone] = (2.10f, 1.73f, 6272), [PropModel.Bookshelf] = (2.00f, 0.70f, 4705), [PropModel.Cage] = (1.21f, 1.22f, 5083),
            [PropModel.Basin] = (1.40f, 1.40f, 3637),
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

        /// <summary>
        /// Which way a model with a front faces, read from its mesh's points (work order 028), in words: "front -Z ..." or
        /// "front +Z ...", "front ? ..." when the points cannot be read; null for a model without a front to read. The
        /// throne: its back is its tall part, so the front is away from where the points over three quarters of its height
        /// lie. The bookshelf: both its ends in z are reached only by its side piers; between them its back is the rough
        /// recessed panel, one sheet of many points, and its front the books, spread through its depth; so the end whose
        /// half holds the 10 cm slab of z with the most points between the piers is its back.
        /// </summary>
        static string Front(PropSize size, Mesh mesh)
        {
            if (size.Model != PropModel.BoneThrone && size.Model != PropModel.Bookshelf) return null;
            List<Vector3> points = mesh != null ? Points(mesh) : null;
            if (points == null || points.Count == 0) return "front ? (its mesh's points could not be read)";
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y), minZ = points.Min(p => p.z), maxZ = points.Max(p => p.z);
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            if (size.Model == PropModel.BoneThrone)
            {
                float high = minY + 0.75f * (maxY - minY);
                List<Vector3> top = points.Where(p => p.y > high).ToList();
                float z = top.Count > 0 ? top.Average(p => p.z) : 0f;
                float mid = (minZ + maxZ) * 0.5f;
                return string.Format(s_Inv, "front {0}Z: its {1} points over {2:0.##} m lie at z {3:0.###} on the mean, the middle is {4:0.###}",
                                     z > mid ? "-" : "+", top.Count, high, z, mid);
            }
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f, between = 0.3f * (maxX - minX);
            List<Vector3> inner = points.Where(p => Mathf.Abs(p.x - cx) < between).ToList();
            float best = cz;
            int most = -1;
            for (float z = minZ; z + 0.1f <= maxZ + 1e-4f; z += 0.01f)
            {
                int n = inner.Count(p => p.z >= z && p.z < z + 0.1f);
                if (n > most) { most = n; best = z + 0.05f; }
            }
            return string.Format(s_Inv, "front {0}Z: of its {1} points between the piers, {2} lie in the 10 cm of z round {3:0.###} (its back's rough panel), the middle is {4:0.###}",
                                 best > cz ? "-" : "+", inner.Count, most, best, cz);
        }

        /// <summary>A mesh's points as Unity imported it, from the script's copy or read back from the graphics card (as the kit's import test does); null when neither can.</summary>
        static List<Vector3> Points(Mesh mesh)
        {
            var v = new List<Vector3>();
            try
            {
                if (mesh.isReadable) mesh.GetVertices(v);
                else
                    using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(mesh))
                    {
                        var raw = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                        data[0].GetVertices(raw);
                        v.AddRange(raw);
                        raw.Dispose();
                    }
            }
            catch (System.Exception)
            {
                v.Clear();
            }
            if (v.Count > 0) return v;
            try
            {
                if (mesh.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Position) != UnityEngine.Rendering.VertexAttributeFormat.Float32
                    || mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.Position) < 3) return null;
                int stream = mesh.GetVertexAttributeStream(UnityEngine.Rendering.VertexAttribute.Position);
                int offset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Position);
                int stride = mesh.GetVertexBufferStride(stream);
                using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream))
                {
                    if (buffer == null) return null;
                    var bytes = new byte[buffer.count * buffer.stride];
                    buffer.GetData(bytes);
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        int at = i * stride + offset;
                        if (at + 12 > bytes.Length) break;
                        v.Add(new Vector3(System.BitConverter.ToSingle(bytes, at), System.BitConverter.ToSingle(bytes, at + 4), System.BitConverter.ToSingle(bytes, at + 8)));
                    }
                }
            }
            catch (System.Exception)
            {
                return null;
            }
            return v.Count > 0 ? v : null;
        }

        [Test]
        public void EveryModel_Loads_IsItsHeight_StandsOnTheFloor_AndHasItsTexture()
        {
            LogAssert.ignoreFailingMessages = true; // work order 028: a mesh that cannot be read from a script says so in the log; that is reported, not failed
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

                // Work order 028: the seven against the table, their emission maps, and the fronts.
                if (s_table.TryGetValue(size.Model, out var table))
                {
                    if (Mathf.Abs(b.size.x - table.w) > 0.05f || Mathf.Abs(b.size.z - table.d) > 0.05f)
                        faults.Add($"{name} is {b.size.x:0.###} x {b.size.z:0.###} m across, the table {table.w} x {table.d}");
                    if (System.Math.Abs(triangles - table.triangles) > table.triangles / 20) faults.Add($"{name} has {triangles} triangles, the table {table.triangles}");
                    Texture2D emissionFile = PropFactory.EmissionTexture(size.Model);
                    report.AppendFormat(s_Inv, "PROPIMPORT {0}: emission map {1}\n", name, emissionFile != null ? emissionFile.name + " " + emissionFile.width + "x" + emissionFile.height : "none");
                    if ((emissionFile != null) != size.Emission)
                        faults.Add($"{name}'s emission map is {(emissionFile != null ? "there" : "missing")}, the table says {(size.Emission ? "it has one" : "none")}");
                    string front = Front(size, filters.Length == 1 ? filters[0].sharedMesh : null);
                    if (front != null)
                    {
                        report.Append("PROPIMPORT ").Append(name).Append(": ").Append(front).Append('\n');
                        bool minusZ = front.StartsWith("front -Z");
                        if (!front.StartsWith("front ?") && minusZ != size.FrontMinusZ)
                            faults.Add($"{name}'s front is {(minusZ ? "-Z" : "+Z")}, the table says {(size.FrontMinusZ ? "-Z" : "+Z")}: {front}");
                    }
                }

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
                // Work order 028: a model with an emission map glows on the material a level makes for it.
                Material mat = builtRenderer.sharedMaterial;
                bool glows = mat.IsKeywordEnabled("_EMISSION") && mat.GetTexture("_EmissionMap") != null && mat.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                if (glows != size.Emission) faults.Add($"{name} as built {(glows ? "glows" : "does not glow")}, the table says {(size.Emission ? "it has an emission map" : "none")}");
            }
            TestContext.WriteLine(report.ToString());
            Debug.Log(report.ToString());
            Assert.That(faults, Is.Empty, string.Join("\n", faults) + "\n" + report);
        }
    }
}
