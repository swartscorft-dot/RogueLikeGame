using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// Work order 112, the first of the light check's faults (<see cref="LookListValues.LitTops"/>; 091's picture of the throne
    /// room: "the room's solid blocks ... are flat black tops that read as holes in the floor rather than masses"; the same on the
    /// walls' piers in work order 100's pictures). What the tops are today, and why they are black at a lamplit level:
    /// - a carved block walled in on every side (work order 051's caps, <see cref="GeneratedLevel.SolidCaps"/>) has a top at the
    ///   walls' height (3 m) in the kit's stone at half its colour (<see cref="CaveValues.BlockTop"/>), and round it the black caps
    ///   of its walls; every lamp hangs lower than 3 m (candles 0.7, braziers 1.3 to 1.8, sconces 2.3, lanterns 2.4), and a lamp
    ///   lights no face that looks up from above it, so with C's base (ambient 0.07, key 0.04) the top is black;
    /// - a pier's top is the dark look's flat cap in the earth's black (work order 083, <see cref="CaveValues.Earth"/>), black in
    ///   any light; a door's post that stands has its own top, over every lamp, unlit.
    ///
    /// The cheapest means that holds in every room kind: a lid over each such top, drawn without the engine's light, whose
    /// colour is worked out once from the floor's lamps (<see cref="LampPlan"/>): at each of its points the light the floor under
    /// it gets from the lamps of its rooms at the level, as the engine lights the floor (intensity, colour, the URP point light's
    /// fall-off and the angle), plus a base, times <see cref="LookListValues.TopShare"/> (a step darker than the floor), rising to
    /// <see cref="LookListValues.RimShare"/> at its outer edge (a lighter rim). Its stone is the floor's texture, a little greyer.
    /// A lid is worked out again only when the level changes; the lamps' flicker does not move it. Lids belong to the rooms round
    /// them and are drawn when one of those rooms is (as the lamps); with the light as before, or the greybox on, none is drawn.
    /// Everything it makes is the lamplit view's, under its root, destroyed with it; nothing has a collider.
    /// </summary>
    public sealed class LookListTops
    {
        public const string Name = "LookList_Tops";

        sealed class Lid
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public int[] Owners;
            public Vector3[] Points;
            public float[] Share;
            public Color[] Tint;
            public Color[] Colours;
        }

        readonly List<Lid> _lids = new List<Lid>();
        readonly List<Renderer> _renderers = new List<Renderer>();
        GeneratedLevel _level;
        LampPlan _plan;

        /// <summary>The lids' renderers, one for each set of rooms they belong to.</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;
        /// <summary>How many tops have a lid: carved blocks (a cell each), piers, posts.</summary>
        public int Blocks { get; private set; }
        public int Piers { get; private set; }
        public int Posts { get; private set; }
        /// <summary>The level the lids are coloured for now; null when they are off.</summary>
        public LamplitValues LitFor { get; private set; }
        /// <summary>How many times they were coloured (for tests: only when the level changes).</summary>
        public int Colourings { get; private set; }

        struct Top
        {
            public float X0, Z0, X1, Z1, Y;
            public int[] Owners;
            public Color Tint;
        }

        /// <summary>The lids of a built floor, off (null with the switch off or nothing to put a lid on). Owned objects go into <paramref name="owned"/>.</summary>
        public static LookListTops Make(GeneratedLevel level, LampPlan plan, Transform root, List<Object> owned)
        {
            if (!LookListValues.Tops || level == null || plan == null || root == null) return null;
            var tops = new LookListTops { _level = level, _plan = plan };
            var found = new List<Top>();
            tops.Find(found);
            if (found.Count == 0) return null;
            tops.BuildMeshes(found, root, owned);
            return tops;
        }

        static int[] RoomsOf(params LevelRoom[] rooms)
        {
            var list = new List<int>(4);
            foreach (LevelRoom r in rooms)
                if (r != null && !list.Contains(r.Order)) list.Add(r.Order);
            list.Sort();
            return list.ToArray();
        }

        static Color Linear(Rgb c)
        {
            var p = new Color(c.R, c.G, c.B, 1f);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
        }

        static Bounds BoundsOf(GameObject go)
        {
            bool first = true;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) b = r.bounds;
                else b.Encapsulate(r.bounds);
                first = false;
            }
            return b;
        }

        void Find(List<Top> into)
        {
            LevelLayout layout = _level.Layout;
            LevelValues values = _level.Values;
            float cs = layout.CellSize;
            CaveView cave = _level.Cave;
            float wallTop = cave != null && cave.WallTop > 0f ? cave.WallTop : values.WallHeight;
            CryptKit.Model wall = CryptKit.Load(CryptPiece.Wall);
            float reach = (wall != null ? Mathf.Max(wall.Bounds.max.z, -wall.Bounds.min.z) : values.WallThickness * 0.5f) + 0.03f + 0.01f; // past the walls' black caps (CaveView: their face plus 0.03)
            float lift = Mathf.Max(0.001f, LookListValues.LidLift);
            Color stone = Linear(LookListValues.TopTint), pale = Linear(LookListValues.PierTint);

            // The carved blocks walled in on every side: a lid over each cell, past the walls on its open sides.
            var capped = new HashSet<(int, int)>();
            var caps = new List<(int x, int y, float top)>();
            foreach (GameObject cap in _level.SolidCaps)
            {
                if (cap == null) continue;
                Bounds b = cap.GetComponent<Renderer>().bounds;
                LevelPoint p = _level.ToLevel(b.center);
                int x = Mathf.FloorToInt((float)(p.X / cs)), y = Mathf.FloorToInt((float)(p.Y / cs));
                if (capped.Add((x, y))) caps.Add((x, y, b.max.y));
            }
            foreach ((int x, int y, float top) in caps)
            {
                float w = capped.Contains((x - 1, y)) ? 0f : reach, e = capped.Contains((x + 1, y)) ? 0f : reach;
                float s = capped.Contains((x, y - 1)) ? 0f : reach, n = capped.Contains((x, y + 1)) ? 0f : reach;
                Vector3 lo = _level.ToWorld(new LevelPoint(x * cs - w, y * cs - s)), hi = _level.ToWorld(new LevelPoint((x + 1) * cs + e, (y + 1) * cs + n));
                into.Add(new Top { X0 = lo.x, Z0 = lo.z, X1 = hi.x, Z1 = hi.z, Y = Mathf.Max(top, wallTop + 0.01f) + lift, Owners = RoomsOf(layout.RoomRectAtCell(x, y)), Tint = stone });
                Blocks++;
            }

            // The piers at the kit's height (one stacked up a tall far wall goes into the dark with it), and the posts that stand by a door.
            var stacked = new Dictionary<string, GameObject>();
            if (cave != null)
                foreach (GameObject go in cave.Stacked)
                    if (go != null) stacked[go.name] = go;
            CryptKitPlan kit = _level.KitPlan;
            bool dark = cave != null && cave.LookBuilt == CaveLook.Dark;
            foreach (GeneratedLevel.KitPiece piece in _level.KitPieces)
            {
                if (kit == null || piece.Object == null || (piece.Kind != CryptPiece.Pier && piece.Kind != CryptPiece.Post)) continue;
                if (piece.Index < 0 || piece.Index >= kit.Points.Count) continue;
                CryptKitPoint point = kit.Points[piece.Index];
                Bounds b = BoundsOf(piece.Object);
                float top = b.max.y;
                if (piece.Kind == CryptPiece.Pier)
                {
                    if (!dark) continue; // without the dark look a pier has its own top, not a black cap
                    // Stacked up a tall far wall (the dark look's raised walls), it goes into the dark with it: no lid.
                    if (stacked.TryGetValue("CaveUpperPier_" + point.X + "_" + point.Y, out GameObject up))
                    {
                        float upTop = BoundsOf(up).max.y;
                        if (upTop > top + LookListValues.RaisedMargin) continue;
                        top = Mathf.Max(top, upTop);
                    }
                }
                else
                {
                    if (piece.Object.transform.localScale.y < 0.99f) continue; // a post cut to the lip has the lip's cap
                    if (top < LookListValues.PostsFrom) continue;               // a low post is under the lamps: they light it
                }
                float half = Mathf.Max(b.extents.x, b.extents.z) + (piece.Kind == CryptPiece.Pier ? 0.03f + 0.01f : 0.01f);
                Vector3 c = _level.ToWorld(new LevelPoint(point.X * cs, point.Y * cs));
                float y = piece.Kind == CryptPiece.Pier ? Mathf.Max(top + 0.012f, wallTop + 0.01f) + lift : top + lift;
                into.Add(new Top
                {
                    X0 = c.x - half, Z0 = c.z - half, X1 = c.x + half, Z1 = c.z + half, Y = y, Tint = pale,
                    Owners = RoomsOf(layout.RoomAtCell(point.X - 1, point.Y - 1), layout.RoomAtCell(point.X, point.Y - 1), layout.RoomAtCell(point.X - 1, point.Y), layout.RoomAtCell(point.X, point.Y)),
                });
                if (piece.Kind == CryptPiece.Pier) Piers++;
                else Posts++;
            }
        }

        /// <summary>The grid lines across [a, b]: its ends, the rim's inner line a rim in from each, and between them every TopGrid or less.</summary>
        static void Lines(float a, float b, float rim, List<float> into)
        {
            into.Clear();
            float r = Mathf.Min(rim, (b - a) * 0.3f);
            into.Add(a);
            float inA = a + r, inB = b - r;
            int n = Mathf.Max(1, Mathf.CeilToInt((inB - inA) / Mathf.Max(0.05f, LookListValues.TopGrid)));
            for (int i = 0; i <= n; i++) into.Add(Mathf.Lerp(inA, inB, i / (float)n));
            into.Add(b);
        }

        void BuildMeshes(List<Top> found, Transform root, List<Object> owned)
        {
            var groups = new Dictionary<string, List<Top>>();
            foreach (Top t in found)
            {
                string key = string.Join("_", t.Owners);
                if (!groups.TryGetValue(key, out List<Top> list)) groups[key] = list = new List<Top>();
                list.Add(t);
            }
            Material material = MakeMaterial();
            owned.Add(material);
            var xs = new List<float>();
            var zs = new List<float>();
            foreach (KeyValuePair<string, List<Top>> pair in groups)
            {
                var v = new List<Vector3>();
                var uv = new List<Vector2>();
                var share = new List<float>();
                var tint = new List<Color>();
                var tris = new List<int>();
                float tile = Mathf.Max(0.1f, LookListValues.TopTile);
                foreach (Top t in pair.Value)
                {
                    Lines(t.X0, t.X1, LookListValues.RimWidth, xs);
                    Lines(t.Z0, t.Z1, LookListValues.RimWidth, zs);
                    int first = v.Count;
                    for (int j = 0; j < zs.Count; j++)
                        for (int i = 0; i < xs.Count; i++)
                        {
                            v.Add(new Vector3(xs[i], t.Y, zs[j]));
                            uv.Add(new Vector2(xs[i] / tile, zs[j] / tile));
                            bool edge = i == 0 || j == 0 || i == xs.Count - 1 || j == zs.Count - 1;
                            share.Add(edge ? LookListValues.RimShare : LookListValues.TopShare);
                            tint.Add(t.Tint);
                        }
                    for (int j = 0; j < zs.Count - 1; j++)
                        for (int i = 0; i < xs.Count - 1; i++)
                        {
                            int a = first + j * xs.Count + i, b = a + xs.Count, c = b + 1, d = a + 1; // clockwise from above: facing up
                            tris.Add(a); tris.Add(b); tris.Add(c);
                            tris.Add(a); tris.Add(c); tris.Add(d);
                        }
                }
                var mesh = new Mesh { name = Name + "_" + pair.Key };
                if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetUVs(0, uv);
                var colours = new Color[v.Count];
                for (int i = 0; i < colours.Length; i++) colours[i] = Color.black;
                mesh.colors = colours;
                mesh.SetTriangles(tris, 0);
                var normals = new Vector3[v.Count];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                owned.Add(mesh);
                var go = new GameObject(Name + "_" + pair.Key) { layer = CryptKit.Layer };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                _renderers.Add(r);
                _lids.Add(new Lid { Renderer = r, Mesh = mesh, Owners = pair.Value[0].Owners, Points = v.ToArray(), Share = share.ToArray(), Tint = tint.ToArray(), Colours = colours });
            }
        }

        /// <summary>The lid's material: the floor's stone drawn as its vertices' colour says, without the engine's light, opaque (the caller destroys it).</summary>
        static Material MakeMaterial()
        {
            Shader shader = Shader.Find(MoodShapes.UnlitShaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = Name };
            Texture2D stone = Resources.Load<Texture2D>(CryptKit.FloorTexturePath(RoomKind.Combat));
            if (stone != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", stone);
                m.mainTexture = stone;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry + 1;
            return m;
        }

        /// <summary>
        /// Colours the lids for a level (null: off, none drawn). Worked out only when the level is another than the last; allocates
        /// nothing per lid point. At each point: the base (<see cref="LookListValues.TopBase"/> times the level's ambient share) plus,
        /// for every lamp of the lid's rooms with a light of its own, its colour times its intensity at the level times the URP
        /// point light's fall-off at the floor point under it and the angle it meets the floor at; never over
        /// <see cref="LookListValues.TopMost"/>; times the point's share and the stone's tint.
        /// </summary>
        public void Light(LamplitValues level)
        {
            if (level == LitFor) return;
            LitFor = level;
            if (level == null)
            {
                foreach (Renderer r in _renderers) if (r != null) r.enabled = false;
                return;
            }
            Colourings++;
            LampValues v = LamplitView.Lamps;
            float lampLevel = Mathf.Max(0f, level.LampLevel), baseLight = Mathf.Max(0f, LookListValues.TopBase * level.AmbientLevel);
            var lamps = new List<(Vector3 at, Color colour, float range, int room)>();
            foreach (Lamp l in _plan.Lamps)
            {
                if (l.CarriedBy != null) continue;
                v.Of(l.Kind, out _, out float range);
                Color c = Linear(v.ColourOf(l)) * (l.Intensity(v) * lampLevel);
                lamps.Add((_level.ToWorld(l.At) + Vector3.up * (float)l.Height, c, range, l.Room));
            }
            foreach (Lid lid in _lids)
            {
                for (int i = 0; i < lid.Points.Length; i++)
                {
                    Vector3 p = lid.Points[i];
                    float r = baseLight, g = baseLight, b = baseLight;
                    foreach ((Vector3 at, Color colour, float range, int room) in lamps)
                    {
                        if (System.Array.IndexOf(lid.Owners, room) < 0) continue;
                        float dx = at.x - p.x, dz = at.z - p.z, h = Mathf.Max(0.05f, at.y);
                        float d2 = dx * dx + dz * dz + h * h;
                        float f = d2 / Mathf.Max(1e-4f, range * range);
                        float smooth = Mathf.Clamp01(1f - f * f);
                        float k = smooth * smooth / d2 * (h / Mathf.Sqrt(d2));
                        r += colour.r * k;
                        g += colour.g * k;
                        b += colour.b * k;
                    }
                    float most = Mathf.Max(0.01f, LookListValues.TopMost), s = lid.Share[i];
                    Color t = lid.Tint[i];
                    lid.Colours[i] = new Color(Mathf.Min(most, r) * s * t.r, Mathf.Min(most, g) * s * t.g, Mathf.Min(most, b) * s * t.b, 1f);
                }
                lid.Mesh.colors = lid.Colours;
            }
        }

        /// <summary>The lids of the rooms drawn now (<see cref="CaveSight.Shown"/>) on, the others off; all off while the light is as before.</summary>
        public void Shown(bool[] shown)
        {
            foreach (Lid lid in _lids)
            {
                if (lid.Renderer == null) continue;
                bool on = false;
                if (LitFor != null)
                    foreach (int o in lid.Owners)
                        if (o >= 0 && o < shown.Length && shown[o]) { on = true; break; }
                lid.Renderer.enabled = on;
            }
        }

        /// <summary>The colour of a lid's point by its place in the lids' order (for tests): the first lid of a set of rooms, its i-th point.</summary>
        public Color ColourAt(int lid, int point) => _lids[lid].Colours[point];
        public int PointsOf(int lid) => _lids[lid].Points.Length;
        public int[] OwnersOf(int lid) => _lids[lid].Owners;
        public int LidCount => _lids.Count;
    }
}
