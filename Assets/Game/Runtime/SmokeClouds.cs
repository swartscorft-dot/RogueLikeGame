using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The clouds of Smoke Round that stand now (work order 109, 2026-11-24, the producer's "Røykskudd": "fiender i et lite område
    /// mister deg av syne et øyeblikk"), and the one question the enemies ask of them: does the smoke hide her from me
    /// (<see cref="Hides"/>, <see cref="NewSkillRules.Hides"/>)? A fixed row of <see cref="SmokeValues.MostClouds"/>: a cloud placed
    /// with the row full takes the oldest's place. Asking allocates nothing; with no cloud up it answers false at once, so with
    /// the new skills off nothing an enemy does is changed. Each cloud is drawn by a <see cref="SmokeCloudView"/>, made once when
    /// it lands and gone when its time is out; a cloud whose picture went with its scene (a floor or a test's scene unloaded) is
    /// up no more.
    /// </summary>
    public static class SmokeClouds
    {
        static readonly SmokeCloud[] s_Clouds = new SmokeCloud[SmokeValues.MostClouds];
        static readonly SmokeCloudView[] s_Views = new SmokeCloudView[SmokeValues.MostClouds];
        static int s_Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < s_Clouds.Length; i++)
            {
                s_Clouds[i] = default;
                s_Views[i] = null;
            }
            s_Count = 0;
        }

        /// <summary>How many clouds stand now.</summary>
        public static int Up
        {
            get
            {
                float now = Time.time;
                int n = 0;
                for (int i = 0; i < s_Count; i++)
                    if (IsUp(i, now)) n++;
                return n;
            }
        }

        /// <summary>The cloud in place <paramref name="i"/> (for tests and the view); default when there is none.</summary>
        public static SmokeCloud At(int i) => i >= 0 && i < s_Count ? s_Clouds[i] : default;

        /// <summary>The picture of the cloud in place <paramref name="i"/>, or null.</summary>
        public static SmokeCloudView ViewAt(int i) => i >= 0 && i < s_Count ? s_Views[i] : null;

        /// <summary>The places in the row that hold a cloud, up or not.</summary>
        public static int Count => s_Count;

        static bool IsUp(int i, float now) => s_Clouds[i].IsUp(now) && s_Views[i] != null;

        /// <summary>A cloud lands: it stands from now for its seconds, drawn, in the first free place, or the oldest's.</summary>
        public static void Place(SmokeCloud cloud)
        {
            float now = Time.time;
            int at = -1;
            for (int i = 0; i < s_Count && at < 0; i++)
                if (!IsUp(i, now)) at = i;
            if (at < 0 && s_Count < s_Clouds.Length) at = s_Count++;
            if (at < 0)
            {
                at = 0;
                for (int i = 1; i < s_Count; i++)
                    if (s_Clouds[i].From < s_Clouds[at].From) at = i;
            }
            if (s_Views[at] != null) Object.Destroy(s_Views[at].gameObject);
            s_Clouds[at] = cloud;
            s_Views[at] = SmokeCloudView.Make(cloud);
        }

        /// <summary>Every cloud gone at once (she is put somewhere else: a new floor, a run's start or end, a test).</summary>
        public static void Clear()
        {
            for (int i = 0; i < s_Count; i++)
            {
                if (s_Views[i] != null) Object.Destroy(s_Views[i].gameObject);
                s_Views[i] = null;
                s_Clouds[i] = default;
            }
            s_Count = 0;
        }

        /// <summary>
        /// Does the smoke hide her from an enemy at <paramref name="enemy"/>: it stands in a cloud that is up, or its line to her
        /// at <paramref name="her"/> passes through one? Nothing allocated; false at once with no cloud placed.
        /// </summary>
        public static bool Hides(Vector3 enemy, Vector3 her)
        {
            if (s_Count == 0) return false;
            float now = Time.time;
            var e = new GroundPoint(enemy.x, enemy.z);
            var h = new GroundPoint(her.x, her.z);
            for (int i = 0; i < s_Count; i++)
            {
                if (!IsUp(i, now)) continue;
                SmokeCloud c = s_Clouds[i];
                if (NewSkillRules.Inside(e, c) || NewSkillRules.Crosses(e, h, c)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The picture of one smoke cloud (work order 109): code-made, grey, see-through enough that she reads what is in it. A few
    /// flat discs stacked from the floor to about her height, each soft at its edge (its vertices' alpha falls to nothing there),
    /// in one mesh made once when the cloud lands; drawn alpha-blended, after the solid world, writing no depth. It changes
    /// nothing in a frame: no mesh, colour or material is touched while it stands, and it goes when its time is out.
    /// </summary>
    public sealed class SmokeCloudView : MonoBehaviour
    {
        /// <summary>The smoke's grey, as picked (a little warm, like the crypt's dust).</summary>
        public static readonly Color Grey = new Color(0.62f, 0.62f, 0.6f);
        /// <summary>The alpha at a disc's middle: low, so the stacked discs stay see-through (work order 109: "see-through enough that she reads what is in it").</summary>
        public const float MiddleAlpha = 0.22f;
        /// <summary>The discs, by height in m over the floor, and each one's share of the cloud's radius: wide low, narrower high.</summary>
        public static readonly float[] DiscHeights = { 0.15f, 0.7f, 1.3f, 1.9f };
        public static readonly float[] DiscShares = { 1f, 0.95f, 0.85f, 0.65f };
        /// <summary>Points round a disc's edge.</summary>
        public const int EdgePoints = 32;

        static Material s_Material;

        Mesh _mesh;
        float _until;

        /// <summary>The cloud it draws.</summary>
        public SmokeCloud Cloud { get; private set; }

        public static SmokeCloudView Make(SmokeCloud cloud)
        {
            var go = new GameObject("SmokeCloud");
            go.transform.position = new Vector3(cloud.Centre.X, 0f, cloud.Centre.Z);
            var view = go.AddComponent<SmokeCloudView>();
            view.Cloud = cloud;
            view._until = cloud.Until;
            view._mesh = BuildMesh(cloud.Radius);
            go.AddComponent<MeshFilter>().sharedMesh = view._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return view;
        }

        /// <summary>One material for every cloud, made the first time: unlit, blended by alpha, no depth written, both sides drawn.</summary>
        static Material Material
        {
            get
            {
                if (s_Material != null) return s_Material;
                Shader shader = Shader.Find(MoodShapes.UnlitShaderName);
                if (shader == null) shader = Shader.Find("Sprites/Default");
                s_Material = new Material(shader) { name = "SmokeCloud" };
                if (s_Material.HasProperty("_BaseColor")) s_Material.SetColor("_BaseColor", Color.white);
                s_Material.SetFloat("_Surface", 1f); // transparent
                s_Material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                s_Material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                s_Material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                s_Material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                s_Material.SetFloat("_ZWrite", 0f);
                s_Material.SetFloat("_Cull", (float)CullMode.Off);
                s_Material.renderQueue = (int)RenderQueue.Transparent;
                return s_Material;
            }
        }

        /// <summary>The stacked discs, each a fan from its middle (the grey at <see cref="MiddleAlpha"/>) to its edge (no alpha).</summary>
        static Mesh BuildMesh(float radius)
        {
            int discs = DiscHeights.Length, per = EdgePoints + 1;
            var vertices = new Vector3[discs * per];
            var colors = new Color[vertices.Length];
            var triangles = new int[discs * EdgePoints * 3];
            Color picked = QualitySettings.activeColorSpace == ColorSpace.Linear ? Grey.linear : Grey;
            Color middle = new Color(picked.r, picked.g, picked.b, MiddleAlpha), edge = new Color(picked.r, picked.g, picked.b, 0f);
            int t = 0;
            for (int d = 0; d < discs; d++)
            {
                int b = d * per;
                float r = radius * DiscShares[d], y = DiscHeights[d];
                vertices[b] = new Vector3(0f, y, 0f);
                colors[b] = middle;
                for (int i = 0; i < EdgePoints; i++)
                {
                    float a = 2f * Mathf.PI * i / EdgePoints;
                    vertices[b + 1 + i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    colors[b + 1 + i] = edge;
                    triangles[t++] = b;
                    triangles[t++] = b + 1 + (i + 1) % EdgePoints;
                    triangles[t++] = b + 1 + i;
                }
            }
            var mesh = new Mesh { name = "SmokeCloud" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Gone when its time is out (game time: a hit stop holds it, as it holds the cloud's own time).</summary>
        void Update()
        {
            if (Time.time >= _until) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
