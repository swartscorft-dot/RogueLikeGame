using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The pieces an enemy's model breaks into (work order 122), made once per model the first time an enemy wearing it appears,
    /// never in a frame of the fight: its own skinned mesh split by its bones. The import keeps no bone weights to read (the meshes
    /// are not readable in a player), so each vertex's bone is found by moving one bone at a time and baking the skin: the vertices
    /// that move most with a bone are that bone's (each bone moved a metre, its children held where they were, then put back, all
    /// within one call, so nothing is ever seen moved). The vertices of the bones of a piece (<see cref="BoneParts"/>) make that
    /// piece's mesh, held in the frame of the piece's root bone, so that a piece put at its bone's place and turn now sits where
    /// that part of the body is in the pose of this moment. A piece long enough is cut in two along its longest side as well, for
    /// breaking again. A rigid mesh hung on a bone (a sword, a shield, a bow) is a piece as it is, its own mesh.
    ///
    /// When the skin cannot be baked (a player build in which the mesh cannot be read), each piece is a box made in code from its
    /// root bone to the next part's, in the body's own material: the order's last choice, "shapes made in code".
    /// </summary>
    public sealed class BoneBreakModel
    {
        /// <summary>Metres a bone is moved by to find the vertices it carries.</summary>
        public const float ProbeMetres = 1f;
        /// <summary>A vertex must move at least this far (m) with a bone to be counted as moving with it.</summary>
        const float LeastMove = 1e-4f;
        /// <summary>A box made in code is this share of its bone's length thick, and at least <see cref="BoxLeast"/> m.</summary>
        const float BoxThickness = 0.35f;
        const float BoxLeast = 0.05f;

        /// <summary>For tests: the boxes made in code even where the skin can be baked, to see the last choice working.</summary>
        public static bool ForceShapesMadeInCode;

        /// <summary>One piece of the model.</summary>
        public sealed class Shape
        {
            /// <summary>The model's renderer whose material the piece wears: an index into <see cref="EnemyLook.Renderers"/>.</summary>
            public int Renderer;
            /// <summary>The piece of the skeleton it is (<see cref="BonePart"/>).</summary>
            public int Part;
            /// <summary>The bone whose frame its mesh is in; for a rigid mesh, null: its own renderer's frame.</summary>
            public string Bone;
            /// <summary>A rigid mesh's own mesh, used as it is (not made here, not destroyed here).</summary>
            public bool OwnMesh;
            public Mesh Whole;
            /// <summary>The two halves it breaks into again, or null when it does not.</summary>
            public Mesh[] Halves;
            /// <summary>The box of the whole and of each half, in its frame, in the model's metres (as made, before the enemy's scale).</summary>
            public Vector3 Centre, Size;
            public Vector3[] HalfCentre, HalfSize;
            /// <summary>The way the cut runs across, in its frame: the halves part along it.</summary>
            public Vector3 CutAxis;
            public int Triangles;
        }

        public readonly string Name;
        public readonly List<Shape> Shapes = new List<Shape>();
        /// <summary>True when the pieces are the model's own mesh split by its bones; false, boxes made in code.</summary>
        public bool Split { get; private set; }
        /// <summary>The milliseconds it took to make, for the report.</summary>
        public double MadeInMs { get; private set; }

        static readonly Dictionary<string, BoneBreakModel> s_models = new Dictionary<string, BoneBreakModel>();

        BoneBreakModel(string name) => Name = name;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_models.Clear();
            ForceShapesMadeInCode = false;
        }

        /// <summary>The pieces of the model this enemy wears, made from it if they are not made yet; null without a model.</summary>
        public static BoneBreakModel Of(Enemy enemy)
        {
            EnemyLook look = enemy != null ? enemy.Look : null;
            if (look == null || look.Model == null) return null;
            string key = look.Spec.Name + (ForceShapesMadeInCode ? "#code" : "");
            if (s_models.TryGetValue(key, out BoneBreakModel m) && m != null) return m;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            m = new BoneBreakModel(key);
            m.Make(enemy, look);
            m.MadeInMs = clock.Elapsed.TotalMilliseconds;
            s_models[key] = m;
            return m;
        }

        /// <summary>The models made so far, for tests and the report.</summary>
        public static IEnumerable<BoneBreakModel> Made => s_models.Values;

        void Make(Enemy enemy, EnemyLook look)
        {
            float scale = Mathf.Abs(enemy.transform.lossyScale.x);
            if (scale < 1e-4f) scale = 1f;
            Renderer[] renderers = look.Renderers;
            bool skinSplit = false;
            for (int ri = 0; ri < renderers.Length; ri++)
            {
                Renderer r = renderers[ri];
                if (r is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    if (!ForceShapesMadeInCode && SplitSkin(skin, ri, scale)) skinSplit |= skin == look.Skin;
                }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null)
                {
                    Bounds b = f.sharedMesh.bounds;
                    Shapes.Add(new Shape
                    {
                        Renderer = ri, Part = PartOf(r.transform), Bone = null, OwnMesh = true, Whole = f.sharedMesh,
                        Centre = b.center, Size = b.size, Triangles = 0,
                    });
                }
            }
            Split = skinSplit;
            if (!skinSplit) MakeBoxes(look, scale);
        }

        /// <summary>The piece a transform of the model belongs to: its own name's, or its nearest parent's that is one of the 24 bones; the pelvis when none is.</summary>
        static int PartOf(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                int part = BoneParts.Of(p.name);
                if (part >= 0) return part;
            }
            return (int)BonePart.Pelvis;
        }

        /// <summary>Is a bone of this name its part's root bone (<see cref="BoneParts.RootBone"/>), by its whole name or the part after the last '|', ':' or '/'?</summary>
        static bool IsRoot(string bone, int part)
        {
            string root = BoneParts.RootBone((BonePart)part);
            int cut = bone.LastIndexOfAny(new[] { '|', ':', '/' });
            string n = cut < 0 ? bone : bone.Substring(cut + 1);
            return string.Equals(n, root, System.StringComparison.OrdinalIgnoreCase);
        }

        // ---------------- The skin split by its bones ----------------

        bool SplitSkin(SkinnedMeshRenderer skin, int rendererIndex, float scale)
        {
            Transform[] bones = skin.bones;
            if (bones == null || bones.Length == 0) return false;
            var baked = new Mesh { name = "BoneBreakBake" };
            var probe = new Mesh { name = "BoneBreakProbe" };
            try
            {
                skin.BakeMesh(baked, true);
                var rest = new List<Vector3>();
                baked.GetVertices(rest);
                int vc = rest.Count;
                if (vc == 0) return false;

                // Each vertex's bone: the one it moves with most.
                var owner = new int[vc];
                var most = new float[vc];
                for (int v = 0; v < vc; v++) { owner[v] = -1; most[v] = LeastMove * LeastMove; }
                var moved = new List<Vector3>(vc);
                var seen = new HashSet<Transform>();
                for (int b = 0; b < bones.Length; b++)
                {
                    Transform bone = bones[b];
                    if (bone == null || !seen.Add(bone)) continue;
                    Vector3 at = bone.localPosition;
                    int cc = bone.childCount;
                    var childAt = new Vector3[cc];
                    for (int c = 0; c < cc; c++) childAt[c] = bone.GetChild(c).localPosition;
                    try
                    {
                        Vector3 d = Vector3.up * ProbeMetres;
                        bone.position += d;
                        for (int c = 0; c < cc; c++) bone.GetChild(c).position -= d; // its children stay where they were
                        skin.BakeMesh(probe, true);
                        probe.GetVertices(moved);
                    }
                    finally
                    {
                        bone.localPosition = at;
                        for (int c = 0; c < cc; c++) bone.GetChild(c).localPosition = childAt[c];
                    }
                    if (moved.Count != vc) continue;
                    for (int v = 0; v < vc; v++)
                    {
                        float m = (moved[v] - rest[v]).sqrMagnitude;
                        if (m > most[v]) { most[v] = m; owner[v] = b; }
                    }
                }

                // Each bone's piece, and each piece's frame: its root bone, else its first bone in the skin's list.
                var partOfBone = new int[bones.Length];
                var frame = new Transform[BoneParts.Count];
                for (int b = 0; b < bones.Length; b++)
                {
                    partOfBone[b] = bones[b] != null ? PartOf(bones[b]) : (int)BonePart.Pelvis;
                    if (bones[b] != null && IsRoot(bones[b].name, partOfBone[b])) frame[partOfBone[b]] = bones[b];
                }
                for (int b = 0; b < bones.Length; b++)
                    if (bones[b] != null && frame[partOfBone[b]] == null) frame[partOfBone[b]] = bones[b];
                var partOfVertex = new int[vc];
                for (int v = 0; v < vc; v++) partOfVertex[v] = owner[v] >= 0 ? partOfBone[owner[v]] : (int)BonePart.Pelvis;

                // Every vertex in each piece's frame, in the model's metres.
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                baked.GetNormals(normals);
                baked.GetUVs(0, uvs);
                bool hasNormals = normals.Count == vc, hasUvs = uvs.Count == vc;
                Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                Quaternion skinTurn = skin.transform.rotation;

                int subs = baked.subMeshCount;
                var tris = new List<int>();
                // The triangles of each piece, per submesh: a triangle is the piece most of its corners are in.
                var byPart = new List<int>[BoneParts.Count, subs];
                for (int s = 0; s < subs; s++)
                {
                    baked.GetTriangles(tris, s);
                    for (int t = 0; t + 2 < tris.Count; t += 3)
                    {
                        int a = partOfVertex[tris[t]], b = partOfVertex[tris[t + 1]], c = partOfVertex[tris[t + 2]];
                        int p = b == c ? b : a;
                        if (p < 0 || p >= BoneParts.Count) p = (int)BonePart.Pelvis;
                        if (byPart[p, s] == null) byPart[p, s] = new List<int>();
                        byPart[p, s].Add(tris[t]);
                        byPart[p, s].Add(tris[t + 1]);
                        byPart[p, s].Add(tris[t + 2]);
                    }
                }

                var local = new Vector3[vc];
                var localNormal = new Vector3[vc];
                bool any = false;
                for (int p = 0; p < BoneParts.Count; p++)
                {
                    int count = 0;
                    for (int s = 0; s < subs; s++) count += byPart[p, s] != null ? byPart[p, s].Count : 0;
                    if (count == 0) continue;
                    Transform f = frame[p] != null ? frame[p] : skin.rootBone != null ? skin.rootBone : skin.transform;
                    Quaternion back = Quaternion.Inverse(f.rotation);
                    Vector3 origin = f.position;
                    for (int s = 0; s < subs; s++)
                    {
                        if (byPart[p, s] == null) continue;
                        foreach (int v in byPart[p, s])
                        {
                            local[v] = back * (toWorld.MultiplyPoint3x4(rest[v]) - origin) / scale;
                            localNormal[v] = hasNormals ? back * (skinTurn * normals[v]) : Vector3.up;
                        }
                    }
                    var lists = new List<int>[subs];
                    for (int s = 0; s < subs; s++) lists[s] = byPart[p, s];
                    string name = Name + "_" + skin.name + "_" + (BonePart)p;
                    Mesh whole = Build(name, lists, subs, local, localNormal, hasUvs ? uvs : null, out Bounds wb);
                    var shape = new Shape
                    {
                        Renderer = rendererIndex, Part = p, Bone = f.name, Whole = whole, Centre = wb.center, Size = wb.size, Triangles = count / 3,
                    };
                    Cut(shape, name, lists, subs, local, localNormal, hasUvs ? uvs : null);
                    Shapes.Add(shape);
                    any = true;
                }
                return any;
            }
            finally
            {
                Object.Destroy(baked);
                Object.Destroy(probe);
            }
        }

        /// <summary>
        /// The two halves of a piece long enough (<see cref="BoneBreakRules.CanBeCut"/>): its triangles parted at the middle one of
        /// them along its longest side.
        /// </summary>
        void Cut(Shape shape, string name, List<int>[] lists, int subs, Vector3[] local, Vector3[] normal, List<Vector2> uvs)
        {
            BoneBreakValues v = BoneBreakValues.Live;
            Vector3 size = shape.Size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (!BoneBreakRules.CanBeCut(longest, shape.Triangles, v)) return;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var along = new List<float>();
            for (int s = 0; s < subs; s++)
                if (lists[s] != null)
                    for (int t = 0; t + 2 < lists[s].Count; t += 3)
                        along.Add((local[lists[s][t]][axis] + local[lists[s][t + 1]][axis] + local[lists[s][t + 2]][axis]) / 3f);
            along.Sort();
            float middle = along[along.Count / 2];
            var lo = new List<int>[subs];
            var hi = new List<int>[subs];
            for (int s = 0; s < subs; s++)
            {
                if (lists[s] == null) continue;
                for (int t = 0; t + 2 < lists[s].Count; t += 3)
                {
                    float c = (local[lists[s][t]][axis] + local[lists[s][t + 1]][axis] + local[lists[s][t + 2]][axis]) / 3f;
                    List<int>[] side = c < middle ? lo : hi;
                    if (side[s] == null) side[s] = new List<int>();
                    side[s].Add(lists[s][t]);
                    side[s].Add(lists[s][t + 1]);
                    side[s].Add(lists[s][t + 2]);
                }
            }
            bool loAny = false, hiAny = false;
            for (int s = 0; s < subs; s++) { loAny |= lo[s] != null; hiAny |= hi[s] != null; }
            if (!loAny || !hiAny) return;
            Mesh a = Build(name + "_a", lo, subs, local, normal, uvs, out Bounds ab);
            Mesh b = Build(name + "_b", hi, subs, local, normal, uvs, out Bounds bb);
            shape.Halves = new[] { a, b };
            shape.HalfCentre = new[] { ab.center, bb.center };
            shape.HalfSize = new[] { ab.size, bb.size };
            var cut = Vector3.zero;
            cut[axis] = 1f;
            shape.CutAxis = cut;
        }

        /// <summary>A mesh of these triangles (indices into the vertex arrays), its vertices taken out and numbered anew, a submesh for each of the model's.</summary>
        static Mesh Build(string name, List<int>[] lists, int subs, Vector3[] local, Vector3[] normal, List<Vector2> uvs, out Bounds bounds)
        {
            var map = new Dictionary<int, int>();
            var pos = new List<Vector3>();
            var nor = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>[subs];
            for (int s = 0; s < subs; s++)
            {
                indices[s] = new List<int>();
                if (lists[s] == null) continue;
                foreach (int v in lists[s])
                {
                    if (!map.TryGetValue(v, out int n))
                    {
                        n = pos.Count;
                        map[v] = n;
                        pos.Add(local[v]);
                        nor.Add(normal[v]);
                        if (uvs != null) uv.Add(uvs[v]);
                    }
                    indices[s].Add(n);
                }
            }
            var mesh = new Mesh { name = name };
            if (pos.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nor);
            if (uvs != null) mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs;
            for (int s = 0; s < subs; s++) mesh.SetTriangles(indices[s], s, false);
            mesh.RecalculateBounds();
            bounds = mesh.bounds;
            return mesh;
        }

        // ---------------- The last choice: boxes made in code ----------------

        /// <summary>Where each piece's box runs to from its root bone: the next part's root, or the end bone of a limb.</summary>
        static readonly string[] s_BoxEnd =
        {
            "Spine", "Spine02", "Head", "head_end", "LeftForeArm", "RightForeArm", "LeftHand", "RightHand", "LeftLeg", "RightLeg", "LeftToeBase", "RightToeBase",
        };

        void MakeBoxes(EnemyLook look, float scale)
        {
            int skinIndex = System.Array.IndexOf(look.Renderers, look.Skin);
            if (skinIndex < 0) return;
            for (int p = 0; p < BoneParts.Count; p++)
            {
                Transform root = EnemyLook.Find(look.Model.transform, BoneParts.RootBone((BonePart)p));
                if (root == null) continue;
                Transform end = EnemyLook.Find(look.Model.transform, s_BoxEnd[p]);
                Vector3 to = end != null ? Quaternion.Inverse(root.rotation) * (end.position - root.position) / scale : Vector3.up * 0.2f;
                if (p == (int)BonePart.ForearmLeft || p == (int)BonePart.ForearmRight) to *= 1.25f; // the hand past the wrist
                float length = Mathf.Max(BoxLeast, to.magnitude);
                float thick = Mathf.Max(BoxLeast, length * BoxThickness);
                Quaternion turn = Quaternion.FromToRotation(Vector3.up, to.sqrMagnitude > 1e-8f ? to.normalized : Vector3.up);
                Mesh box = Box(Name + "_box_" + (BonePart)p, to * 0.5f, turn, new Vector3(thick, length, thick), look.Skin.sharedMesh != null ? look.Skin.sharedMesh.subMeshCount : 1);
                Shapes.Add(new Shape
                {
                    Renderer = skinIndex, Part = p, Bone = root.name, Whole = box, Centre = box.bounds.center, Size = box.bounds.size, Triangles = 12,
                });
            }
        }

        /// <summary>A box of this size, turned and centred so, with a face's own normals; its triangles in the first submesh of <paramref name="subs"/>.</summary>
        static Mesh Box(string name, Vector3 centre, Quaternion turn, Vector3 size, int subs)
        {
            var pos = new List<Vector3>(24);
            var nor = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var tris = new List<int>(36);
            Vector3 h = size * 0.5f;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            for (int a = 0; a < 3; a++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = axes[a] * sign, u = axes[(a + 1) % 3], w = axes[(a + 2) % 3];
                    int o = pos.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float su = (k & 1) == 0 ? -1f : 1f, sw = (k & 2) == 0 ? -1f : 1f;
                        Vector3 p = Vector3.Scale(n + u * su + w * sw, h);
                        pos.Add(centre + turn * p);
                        nor.Add(turn * n);
                        uv.Add(new Vector2(0.5f, 0.5f));
                    }
                    if (sign > 0) { tris.Add(o); tris.Add(o + 1); tris.Add(o + 3); tris.Add(o); tris.Add(o + 3); tris.Add(o + 2); }
                    else { tris.Add(o); tris.Add(o + 3); tris.Add(o + 1); tris.Add(o); tris.Add(o + 2); tris.Add(o + 3); }
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(pos);
            mesh.SetNormals(nor);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = Mathf.Max(1, subs);
            mesh.SetTriangles(tris, 0, false);
            for (int s = 1; s < mesh.subMeshCount; s++) mesh.SetTriangles(new int[0], s, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
