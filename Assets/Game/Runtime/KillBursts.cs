using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The burst of a kill (work order 080, part 2: "a kill pays"): a small star where the enemy stood, popping out and
    /// gone in a fifth of a second, larger and longer for the kill that empties its room. In the style of the shot effects
    /// Kristian approved on 2026-11-18 (<see cref="ShotFx"/>, <see cref="CombatFx"/>): a flat mesh made in code, an outer
    /// star in a rim colour and an inner one in a core colour lifted above white so the camera's bloom lights it, with the
    /// shot effects' material, facing the camera. No particle system, texture or shader.
    ///
    /// Pooled: <see cref="KillWeightValues.BurstPool"/> pieces and one mesh, made at the first kill and kept; a burst
    /// takes a free piece, or the oldest's. While none lives it does not run at all (its Update is off), so nothing is
    /// made or done in a frame while nothing dies. On game time, as the shot effects, so the hit stop holds it.
    /// </summary>
    public sealed class KillBursts : MonoBehaviour
    {
        // The colours and the shape, first guesses: the shot effects' warm rim and a near-white core over it.
        static readonly Color s_Rim = new Color(1f, 0.55f, 0.18f, 1f);
        static readonly Color s_Core = new Color(1f, 0.95f, 0.85f, 1f);
        const float RimGlow = 1.6f;          // the rim a little over its colour, as the shot effects' rims
        const float CoreGlow = 3f;           // the core well over white: bloom lights it
        const float CoreShare = 0.5f;        // the inner star's size over the outer one's
        const float CoreLift = 0.01f;        // m the inner star lies in front of the outer one
        const float PopShare = 0.2f;         // of its life spent growing to its full size; the rest shrinking away
        const float Overshoot = 1.15f;       // its size at the top of the pop, over its own
        const float TurnDegrees = 70f;       // degrees it turns over its life, every other one the other way
        const float TowardCamera = 0.5f;     // m drawn nearer the camera, so the body it stands on does not hide it
        static readonly float[] s_Points = { 1f, 0.4f, 0.75f, 0.4f, 1f, 0.4f, 0.75f, 0.4f, 1f, 0.4f, 0.75f, 0.4f }; // six long and six short spikes

        struct Piece
        {
            public GameObject Go;
            public Transform T;
            public float Age, Seconds, Size;
            public bool Live;
            public Vector3 At;
            public float Turn;
        }

        Piece[] _pieces = new Piece[0];
        Mesh _mesh;
        Material _material;
        int _turns;

        /// <summary>For tests: the pieces made (never more than the pool), the bursts living now, and the bursts shown.</summary>
        public int Made { get; private set; }
        public int Live { get; private set; }
        public int Shown { get; private set; }
        /// <summary>For tests: the size of the latest burst, at its largest, in metres.</summary>
        public float LastSize { get; private set; }

        public static KillBursts Make(Transform parent)
        {
            var go = new GameObject("KillBursts");
            go.transform.SetParent(parent, false);
            var bursts = go.AddComponent<KillBursts>();
            bursts.enabled = false;
            return bursts;
        }

        /// <summary>A burst at an enemy's ground point <paramref name="feet"/>; <paramref name="roomWon"/>: its kill emptied its room.</summary>
        public void Burst(Vector3 feet, bool roomWon, KillWeightValues v, Material material)
        {
            if (v == null || material == null || v.BurstSize <= 0f || v.BurstSeconds <= 0f) return;
            Fill(Mathf.Max(1, v.BurstPool), material);
            int i = Free();
            float scale = roomWon ? Mathf.Max(1f, v.RoomWonScale) : 1f;
            ref Piece p = ref _pieces[i];
            if (!p.Live) Live++;
            p.Live = true;
            p.Age = 0f;
            p.Seconds = v.BurstSeconds * scale;
            p.Size = v.BurstSize * scale;
            p.At = feet + Vector3.up * v.BurstHeight;
            p.Turn = (_turns++ & 1) == 0 ? TurnDegrees : -TurnDegrees;
            p.Go.SetActive(true);
            Place(ref p, 0f);
            LastSize = p.Size;
            Shown++;
            enabled = true;
        }

        /// <summary>The pool, made once (and again only if its material or its size changed).</summary>
        void Fill(int count, Material material)
        {
            if (_mesh == null) _mesh = BuildMesh();
            if (material != _material)
            {
                _material = material;
                for (int i = 0; i < _pieces.Length; i++)
                    if (_pieces[i].Go != null) _pieces[i].Go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            if (_pieces.Length >= count) return;
            int had = _pieces.Length;
            System.Array.Resize(ref _pieces, count);
            for (int i = had; i < count; i++)
            {
                var go = new GameObject("KillBurst");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _pieces[i] = new Piece { Go = go, T = go.transform };
                Made++;
            }
        }

        /// <summary>A piece that is not live, else the one nearest its end.</summary>
        int Free()
        {
            int best = 0;
            float most = -1f;
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (!_pieces[i].Live) return i;
                float done = _pieces[i].Seconds > 0f ? _pieces[i].Age / _pieces[i].Seconds : 1f;
                if (done > most)
                {
                    most = done;
                    best = i;
                }
            }
            return best;
        }

        void Update()
        {
            if (Live == 0)
            {
                enabled = false;
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref Piece p = ref _pieces[i];
                if (!p.Live) continue;
                p.Age += dt;
                float t = p.Seconds > 0f ? p.Age / p.Seconds : 1f;
                if (t >= 1f)
                {
                    p.Live = false;
                    p.Go.SetActive(false);
                    Live--;
                    continue;
                }
                Place(ref p, t);
            }
        }

        /// <summary>Its place, its turn and its size at <paramref name="t"/> of its life, facing the camera.</summary>
        void Place(ref Piece p, float t)
        {
            Camera cam = Camera.main;
            Vector3 toCamera = cam != null ? (cam.transform.position - p.At).normalized : Vector3.up;
            p.T.position = p.At + toCamera * TowardCamera;
            p.T.rotation = Quaternion.LookRotation(-toCamera, Vector3.up) * Quaternion.Euler(0f, 0f, p.Turn * t);
            p.T.localScale = Vector3.one * (p.Size * SizeAt(t));
        }

        /// <summary>Its size over its own at <paramref name="t"/> of its life: a quick pop past its size, then down to nothing.</summary>
        public static float SizeAt(float t)
        {
            if (t <= 0f) return 0.35f;
            if (t < PopShare) return Mathf.Lerp(0.35f, Overshoot, t / PopShare);
            float k = Mathf.Clamp01((t - PopShare) / (1f - PopShare));
            return Overshoot * (1f - k * k);
        }

        /// <summary>The star: an outer one in the rim colour, an inner one in the core's in front of it, both faces drawn.</summary>
        static Mesh BuildMesh()
        {
            int n = s_Points.Length;
            var vertices = new Vector3[2 * (n + 1)];
            var colors = new Color[vertices.Length];
            var triangles = new int[2 * n * 6];
            Color rim = Drawn(s_Rim, RimGlow), core = Drawn(s_Core, CoreGlow);
            int tri = 0;
            for (int layer = 0; layer < 2; layer++)
            {
                int o = layer * (n + 1);
                float scale = layer == 0 ? 0.5f : 0.5f * CoreShare;
                float z = layer == 0 ? 0f : -CoreLift; // the camera looks down local +Z: in front is toward -Z
                Color c = layer == 0 ? rim : core;
                vertices[o] = new Vector3(0f, 0f, z);
                colors[o] = c;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    vertices[o + 1 + i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (s_Points[i] * scale) + new Vector3(0f, 0f, z);
                    colors[o + 1 + i] = c;
                }
                for (int i = 0; i < n; i++)
                {
                    int a = o + 1 + i, b = o + 1 + (i + 1) % n;
                    triangles[tri++] = o; triangles[tri++] = a; triangles[tri++] = b;
                    triangles[tri++] = o; triangles[tri++] = b; triangles[tri++] = a; // the other face too
                }
            }
            var mesh = new Mesh { name = "KillBurst" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The vertex colour for a picked colour, as the shot effects make it: in linear light, brighter by its glow.</summary>
        static Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
