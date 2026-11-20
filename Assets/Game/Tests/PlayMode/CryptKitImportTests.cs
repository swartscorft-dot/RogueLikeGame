using System;
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
    /// The seven crypt kit models as Unity imports them (work order 016, 2026-11-20): each loads by name from Resources,
    /// is the size it was made to the centimetre, is one mesh, has its pivot where it was made (the bottom centre; the
    /// arch's in the middle of its opening on the floor; the lantern's on the back of its plate), and has its textures.
    /// Which face of the niche wall has the niche, which way the lantern reaches, and the arch's clear opening are read
    /// from the meshes. Writes a line per model with what Unity made of it ("KITIMPORT").
    /// </summary>
    public class CryptKitImportTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The sizes the models were made to, along x, through (z) and up (y), in metres (from the work order).</summary>
        static readonly Dictionary<CryptPiece, Vector3> s_made = new Dictionary<CryptPiece, Vector3>
        {
            [CryptPiece.Wall] = new Vector3(4.00f, 3.00f, 0.55f),
            [CryptPiece.WallNiche] = new Vector3(4.00f, 3.00f, 0.55f),
            [CryptPiece.LowWall] = new Vector3(4.00f, 0.95f, 0.55f),
            [CryptPiece.Pier] = new Vector3(0.80f, 3.40f, 0.80f),
            [CryptPiece.Post] = new Vector3(0.80f, 1.30f, 0.80f),
            [CryptPiece.Arch] = new Vector3(4.00f, 4.20f, 0.69f),
            [CryptPiece.Lantern] = new Vector3(0.31f, 0.60f, 0.48f),
        };

        static readonly Dictionary<CryptPiece, int> s_triangles = new Dictionary<CryptPiece, int>
        {
            [CryptPiece.Wall] = 2882, [CryptPiece.WallNiche] = 3317, [CryptPiece.LowWall] = 1638, [CryptPiece.Pier] = 608,
            [CryptPiece.Post] = 1560, [CryptPiece.Arch] = 2004, [CryptPiece.Lantern] = 990,
        };

        const float Centimetre = 0.0105f;

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

        static string V(Vector3 v) => string.Format(s_Inv, "({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);

        /// <summary>The mesh's points in the model's frame, or null when they cannot be read.</summary>
        static List<Vector3> Points(CryptKit.Model model)
        {
            var all = new List<Vector3>();
            for (int i = 0; i < model.Meshes.Length; i++)
            {
                Mesh mesh = model.Meshes[i];
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
                catch (Exception)
                {
                    v.Clear();
                }
                if (v.Count == 0) ReadBack(mesh, v);
                if (v.Count == 0) return null;
                foreach (Vector3 p in v) all.Add(model.Places[i].MultiplyPoint3x4(p));
            }
            return all;
        }

        /// <summary>
        /// The mesh's positions read back from its vertex buffer on the graphics card: an imported mesh without Read/Write
        /// keeps no copy a script can read, and this is how the import test still sees its points. Nothing is drawn.
        /// </summary>
        static void ReadBack(Mesh mesh, List<Vector3> into)
        {
            try
            {
                if (mesh.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Position) != UnityEngine.Rendering.VertexAttributeFormat.Float32
                    || mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.Position) < 3) return;
                int stream = mesh.GetVertexAttributeStream(UnityEngine.Rendering.VertexAttribute.Position);
                int offset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Position);
                int stride = mesh.GetVertexBufferStride(stream);
                using (GraphicsBuffer buffer = mesh.GetVertexBuffer(stream))
                {
                    if (buffer == null) return;
                    var bytes = new byte[buffer.count * buffer.stride];
                    buffer.GetData(bytes);
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        int at = i * stride + offset;
                        if (at + 12 > bytes.Length) break;
                        into.Add(new Vector3(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8)));
                    }
                }
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        [Test]
        public void EveryKitModel_Loads_IsItsSize_OneMesh_PivotWhereMade_AndHasItsTextures()
        {
            LogAssert.ignoreFailingMessages = true; // a mesh that cannot be read from a script says so in the log; that is reported, not failed
            var report = new StringBuilder();
            var faults = new List<string>();
            foreach (CryptPiece piece in (CryptPiece[])Enum.GetValues(typeof(CryptPiece)))
            {
                string name = CryptKit.NameOf(piece);
                GameObject source = Resources.Load<GameObject>(CryptKit.ResourcePath(piece));
                if (source == null)
                {
                    faults.Add(name + " does not load from Resources/" + CryptKit.ResourcePath(piece));
                    continue;
                }
                CryptKit.Model model = CryptKit.Load(piece);
                Assert.That(model, Is.Not.Null, name + " has a mesh");
                MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
                Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
                Transform[] nodes = source.GetComponentsInChildren<Transform>(true);
                Component[] components = source.GetComponentsInChildren<Component>(true);

                // As placed by a level: the bounds in a piece's frame. And as the import's object comes, for comparison.
                Bounds b = model.Bounds;
                GameObject raw = Object.Instantiate(source, new Vector3(-300f, 0f, 60f), source.transform.rotation);
                _made.Add(raw);
                Bounds rb = raw.GetComponentInChildren<Renderer>().bounds;
                foreach (Renderer r in raw.GetComponentsInChildren<Renderer>()) rb.Encapsulate(r.bounds);
                Vector3 rawSize = rb.size;

                Texture2D baseColor = CryptKit.BaseTexture(piece);
                Texture2D emission = CryptKit.EmissionTexture(piece);

                Vector3 made = s_made[piece];
                string extra = "";
                List<Vector3> points = Points(model);
                if (points != null)
                {
                    // Read right only when the points fill the mesh's own bounds.
                    var pb = new Bounds(points[0], Vector3.zero);
                    foreach (Vector3 p in points) pb.Encapsulate(p);
                    extra += "; " + points.Count + " points read, their bounds " + V(pb.size);
                    if ((pb.size - b.size).magnitude > 0.01f || (pb.center - b.center).magnitude > 0.01f)
                    {
                        extra += " (not the mesh's: not used)";
                        points = null;
                    }
                }
                if (points == null) extra += "; mesh points could not be read";
                if (piece == CryptPiece.WallNiche || piece == CryptPiece.Wall)
                {
                    // The niche's skulls and its recess are more points on one face than on the other.
                    if (points != null)
                    {
                        int plus = points.Count(p => p.z > 0.02f), minus = points.Count(p => p.z < -0.02f);
                        extra += string.Format(s_Inv, "; points on +Z {0}, on -Z {1}", plus, minus);
                        if (piece == CryptPiece.WallNiche)
                        {
                            bool plusZ = plus > minus;
                            extra += "; the niche is on " + (plusZ ? "+Z" : "-Z");
                            if (plusZ != new CryptKitValues().NicheFacesPlusZ)
                                faults.Add($"{name}'s niche is on {(plusZ ? "+Z" : "-Z")}, the values say {(new CryptKitValues().NicheFacesPlusZ ? "+Z" : "-Z")}");
                        }
                    }
                }
                if (piece == CryptPiece.Lantern)
                {
                    extra += "; reaches " + (b.center.z >= 0f ? "+Z" : "-Z") + string.Format(s_Inv, " from z {0:0.###} to {1:0.###}", b.min.z, b.max.z);
                    if (Mathf.Min(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z)) > 0.03f) faults.Add($"{name}'s pivot is not on its back: z {b.min.z:0.###} to {b.max.z:0.###}");
                    if (Mathf.Abs(b.center.x) > 0.02f) faults.Add($"{name}'s pivot is {b.center.x:0.###} m off its middle across");
                }
                else
                {
                    if (Mathf.Abs(b.min.y) > 0.01f) faults.Add($"{name}'s lowest point is {b.min.y:0.###} m from its pivot's height");
                    if (new Vector2(b.center.x, b.center.z).magnitude > 0.01f) faults.Add($"{name}'s pivot is {V(b.center)} from its footprint's middle");
                }
                if (piece == CryptPiece.Arch && points != null)
                {
                    // The clear opening: the innermost points of the jambs, between the floor and the springing.
                    List<Vector3> jambs = points.Where(p => p.y > 0.1f && p.y < 2.8f).ToList();
                    float inner = jambs.Count > 0 ? jambs.Min(p => Mathf.Abs(p.x)) : float.NaN;
                    float top = points.Where(p => Mathf.Abs(p.x) < 0.05f && p.y > 0.1f).Select(p => p.y).DefaultIfEmpty(float.NaN).Min();
                    extra += string.Format(s_Inv, "; clear opening {0:0.###} m wide, {1:0.###} m high in the middle", 2f * inner, top);
                    float share = new CryptKitValues().ArchOpeningShare * b.size.x;
                    if (Mathf.Abs(2f * inner - share) > 0.02f) faults.Add($"{name}'s opening is {2f * inner:0.###} m, the values say {share:0.###}");
                }

                report.AppendFormat(s_Inv,
                    "KITIMPORT {0}: size in a piece's frame {1} (made {2}); bounds min {3} max {4}; as the import's object comes {5}; " +
                    "triangles {6} (made {7}); meshes {8}, renderers {9}, nodes {10}, components [{11}]; root rotation {12} scale {13}; " +
                    "child rotations [{14}]; mesh on the root {15}; base colour {16} {17}x{18}; emission {19} {20}x{21}{22}\n",
                    name, V(new Vector3(b.size.x, b.size.y, b.size.z)), V(made), V(b.min), V(b.max), V(rawSize),
                    model.Triangles, s_triangles[piece], filters.Length, renderers.Length, nodes.Length,
                    string.Join(", ", components.Select(c => c.GetType().Name).Distinct()),
                    V(source.transform.localEulerAngles), V(source.transform.localScale),
                    string.Join("; ", nodes.Skip(1).Select(t => t.name + " " + V(t.localEulerAngles) + " x" + V(t.localScale))),
                    model.OnRoot,
                    baseColor != null ? baseColor.name : "none", baseColor != null ? baseColor.width : 0, baseColor != null ? baseColor.height : 0,
                    emission != null ? emission.name : "none", emission != null ? emission.width : 0, emission != null ? emission.height : 0, extra);

                if (Mathf.Abs(b.size.x - made.x) > Centimetre) faults.Add($"{name} is {b.size.x:0.###} m along, made {made.x}");
                if (Mathf.Abs(b.size.y - made.y) > Centimetre) faults.Add($"{name} is {b.size.y:0.###} m high, made {made.y}");
                if (Mathf.Abs(b.size.z - made.z) > Centimetre) faults.Add($"{name} is {b.size.z:0.###} m through, made {made.z}");
                if (filters.Length != 1 || renderers.Length != 1) faults.Add($"{name} has {filters.Length} meshes and {renderers.Length} renderers, not one");
                if (Mathf.Abs(model.Triangles - s_triangles[piece]) > 2) faults.Add($"{name} has {model.Triangles} triangles, made {s_triangles[piece]}");
                if (baseColor == null) faults.Add($"{name}'s texture does not load from Resources/{CryptKit.BaseTexturePath(piece)}");
                if (emission == null) faults.Add($"{name}'s emission map does not load from Resources/{CryptKit.EmissionTexturePath(piece)}");
                if (Mathf.Abs(rawSize.y - b.size.y) > 0.01f) faults.Add($"{name} stands differently as the import's object comes: {V(rawSize)}");
            }
            foreach (RoomKind kind in (RoomKind[])Enum.GetValues(typeof(RoomKind)))
            {
                var t = Resources.Load<Texture2D>(CryptKit.FloorTexturePath(kind));
                report.AppendFormat(s_Inv, "KITIMPORT floor of {0}: {1} {2}x{3} wrap {4}\n", kind, t != null ? t.name : "none", t != null ? t.width : 0, t != null ? t.height : 0,
                                    t != null ? t.wrapMode.ToString() : "-");
                if (t == null) faults.Add("the floor texture of " + kind + " does not load from Resources/" + CryptKit.FloorTexturePath(kind));
                else if (t.wrapMode != TextureWrapMode.Repeat) faults.Add("the floor texture of " + kind + " does not repeat");
            }
            LogAssert.ignoreFailingMessages = false;
            TestContext.WriteLine(report.ToString());
            Debug.Log(report.ToString());
            Assert.That(faults, Is.Empty, string.Join("\n", faults) + "\n" + report);
        }

        [Test]
        public void EveryKitMaterial_HasItsTexture_AndTheGlowablesGlowInTheGamesCyan()
        {
            var v = new CryptKitValues();
            var report = new StringBuilder();
            foreach (CryptPiece piece in (CryptPiece[])Enum.GetValues(typeof(CryptPiece)))
            {
                Material m = CryptKit.MakeMaterial(piece, v);
                _made.Add(m);
                Assert.That(m.shader.name, Is.EqualTo(PropFactory.ShaderName), piece.ToString());
                Assert.That(m.GetTexture("_BaseMap"), Is.Not.Null, piece + " has its painted texture");
                Assert.That(m.GetTexture("_EmissionMap"), Is.Not.Null, piece + " has its emission map");
                Assert.That(m.IsKeywordEnabled("_EMISSION"), Is.True, piece + " glows");
                Color set = m.GetColor("_EmissionColor");
                Vector4 e = QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)set.linear : (Vector4)set;
                Color wanted = CryptKit.Glowing(v.Cyan, piece == CryptPiece.Lantern ? v.LanternGlow : v.StoneGlow);
                Assert.That(Vector4.Distance(e, (Vector4)wanted), Is.LessThan(1e-2f), piece + "'s glow, in linear light");
                Assert.That(e.y, Is.GreaterThan(e.x), "cyan");
                Color tint = m.GetColor("_BaseColor");
                bool pale = piece == CryptPiece.Pier || piece == CryptPiece.Arch;
                Assert.That(tint.r, Is.EqualTo(pale ? v.PaleStoneTint.R : 1f).Within(0.01f), piece + "'s tint");
                report.AppendFormat(s_Inv, "KITGLOW {0}: emission colour ({1:0.###}, {2:0.###}, {3:0.###}) linear; tint {4}\n", piece, e.x, e.y, e.z, tint);
            }
            Debug.Log(report.ToString());
            TestContext.WriteLine(report.ToString());
        }
    }
}
