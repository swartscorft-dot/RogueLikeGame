using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The Laser Sight's beams (work order 102; work order 060 drew a line from her middle along the cursor to where the shot
    /// would end, on an enemy's middle with a square there, which Kristian read as the aim snapping to the enemy: "det liker jeg
    /// ikke i det hele tatt ... bare en tyn rød laser stråle ... vil at de skal følge våpenets retning hele tiden"). One thin red
    /// beam from each pistol's muzzle along where its barrel points (the muzzle's +X, the pistol's barrel by the model's own
    /// rule: <see cref="CharacterAnimation.RightWeapon"/>), in three dimensions, read every frame after her arms are posed
    /// (<see cref="LaserSightLate"/>). It reads no cursor, no target and no shot. What it meets stops it: the floor, a wall, a
    /// prop, a shut door (a physics ray), an enemy's body (each living target taken as an upright body of its radius,
    /// <see cref="LaserBeamValues.BodyTop"/> high: they carry no collider). <see cref="PlayerCharacter.TickLaser"/> says when.
    ///
    /// The look is the research notes' (runner\research\laser-in-fog\notes.md), cheaply, all numbers in <see cref="Values"/>:
    /// strongest at the muzzle and fading to nothing at <see cref="LaserBeamValues.Length"/>; faint in clear air, brighter in
    /// the room's low fog along its walls (where <see cref="RoomMood"/> lays it: the edge of the room's open floor, low down),
    /// both uneven in slow wisps fixed in the air; a few dust motes that drift through it and wink out; the dot where it lands
    /// built but off (<see cref="LaserBeamValues.DotOn"/>). Drawn added onto the picture (light, never a dark band), on a
    /// Particles/Unlit material made here as the rooms' mood makes its own (<see cref="MoodShapes.MakeUnlit"/>), facing the
    /// camera about its own length so it never turns edge on.
    ///
    /// Code-made, no asset: one object for both beams in the scene (it goes with the scene) holding the late tick, a child and a
    /// mesh for each beam, and the material, made the first time she has the part; each frame only writes the meshes' few
    /// vertices into arrays made once, so nothing is allocated per frame. <see cref="Dispose"/> destroys all of it with her.
    /// </summary>
    public sealed class LaserSight
    {
        /// <summary>The beams: the right hand's, then the left's.</summary>
        public const int Right = 0, Left = 1, Count = 2;
        /// <summary>Rows of vertices along a beam; each row is three across, dark at both edges and lit in the middle, so it is soft across as a beam of light is.</summary>
        const int Rows = 13, Across = 3;
        /// <summary>Motes the mesh has room for on each beam; <see cref="LaserBeamValues.Specks"/> uses that many of them at most.</summary>
        public const int MostSpecks = 12;
        /// <summary>Vertices of a mote or the dot: a lit middle and four dark points round it, so it is a soft spot, not a square.</summary>
        const int Spot = 5;
        const int StripVerts = Rows * Across, SpeckBase = StripVerts, DotBase = SpeckBase + MostSpecks * Spot, Verts = DotBase + Spot;
        /// <summary>The material's place in the draw order: after the solid world and the floor's decals, with the other added light.</summary>
        public const int Queue = 3100;
        /// <summary>Metres the dot is drawn toward the camera off what it lands on, so it is not lost in that surface.</summary>
        const float DotLift = 0.02f;

        /// <summary>The beam's look; read every frame, so it can be tuned while playing.</summary>
        public readonly LaserBeamValues Values = new LaserBeamValues();

        GameObject _root;
        Material _material;
        readonly GameObject[] _go = new GameObject[Count];
        readonly Mesh[] _mesh = new Mesh[Count];
        readonly MeshRenderer[] _renderer = new MeshRenderer[Count];
        readonly Vector3[] _verts = new Vector3[Verts];
        readonly Color[] _colours = new Color[Verts];
        readonly Vector3[] _from = new Vector3[Count], _dir = new Vector3[Count], _dotAt = new Vector3[Count];
        readonly float[] _length = new float[Count], _fog = new float[Count];
        readonly bool[] _shown = new bool[Count], _landed = new bool[Count], _dotShown = new bool[Count];
        readonly Damageable[] _body = new Damageable[Count];
        readonly Collider[] _collider = new Collider[Count];
        static readonly RaycastHit[] s_hits = new RaycastHit[16];

        // The fog of her room, as RoomMood laid it (read again only when the room or its system changes).
        RoomMood _fogMood;
        int _fogRoom = int.MinValue;
        ParticleSystem _fogSystem;
        RoomMood.RoomParts _fogParts;
        Vector2 _fogHalf;

        /// <summary>True while a beam shows.</summary>
        public bool Shown => _shown[Right] || _shown[Left];
        /// <summary>The object both beams are under, or null before she first had the part; for tests.</summary>
        public GameObject Object => _root;
        /// <summary>The added-light material both beams are drawn with, or null before; for tests.</summary>
        public Material Material => _material;
        /// <summary>Frames it was drawn; for tests.</summary>
        public int Shows { get; private set; }

        /// <summary>True while that beam shows.</summary>
        public bool ShownOf(int beam) => _shown[beam];
        /// <summary>Where that beam starts, its muzzle, as it was last drawn.</summary>
        public Vector3 FromOf(int beam) => _from[beam];
        /// <summary>Its direction, of length 1: the barrel's, as last drawn.</summary>
        public Vector3 DirectionOf(int beam) => _dir[beam];
        /// <summary>Its length as drawn: <see cref="LaserBeamValues.Length"/>, or less where something stopped it.</summary>
        public float LengthOf(int beam) => _length[beam];
        /// <summary>Where it ends.</summary>
        public Vector3 EndOf(int beam) => _from[beam] + _dir[beam] * _length[beam];
        /// <summary>True when something stopped it within its length (it lands there).</summary>
        public bool LandedOf(int beam) => _landed[beam];
        /// <summary>The enemy's body that stopped it, or null.</summary>
        public Damageable BodyOf(int beam) => _body[beam];
        /// <summary>The collider that stopped it (a wall, a prop, the floor, a door), or null.</summary>
        public Collider ColliderOf(int beam) => _collider[beam];
        /// <summary>True while the dot shows where it lands (only with <see cref="LaserBeamValues.DotOn"/>).</summary>
        public bool DotShownOf(int beam) => _dotShown[beam];
        /// <summary>Where the dot is drawn.</summary>
        public Vector3 DotAt(int beam) => _dotAt[beam];
        /// <summary>The fog's mean share along that beam as last drawn, 0 to 1; for tests and pictures.</summary>
        public float FogOf(int beam) => _fog[beam];
        /// <summary>That beam's mesh, or null; for tests.</summary>
        public Mesh MeshOf(int beam) => _mesh[beam];

        /// <summary>Makes its objects the first time she has the part (from her LateUpdate), so its late tick runs from then on.</summary>
        public void Ensure(PlayerCharacter her)
        {
            if (_root == null) Make(her);
        }

        /// <summary>
        /// Draws both beams now from <paramref name="right"/> and <paramref name="left"/> (her muzzles; a missing or hidden one
        /// shows none), at <paramref name="time"/> for the wisps and motes; <paramref name="her"/>'s own colliders do not stop them.
        /// </summary>
        public void Draw(PlayerCharacter her, Transform right, Transform left, float time)
        {
            bool rightShown = right != null && right.gameObject.activeInHierarchy, leftShown = left != null && left.gameObject.activeInHierarchy;
            if (!rightShown && !leftShown)
            {
                Hide(); // no pistol to fix a beam to: nothing is made either
                return;
            }
            if (_root == null) Make(her);
            Camera cam = Camera.main;
            bool fog = FogRect(out Vector2 fogMiddle, out Vector2 fogHalf);
            bool any = false;
            for (int b = 0; b < Count; b++)
            {
                Transform muzzle = b == Right ? right : left;
                bool show = b == Right ? rightShown : leftShown;
                if (show) show = DrawBeam(b, her != null ? her.transform : null, muzzle, cam, fog, fogMiddle, fogHalf, time);
                if (!show) HideBeam(b);
                any |= show;
            }
            if (any) Shows++;
        }

        /// <summary>Hides both.</summary>
        public void Hide()
        {
            for (int b = 0; b < Count; b++) HideBeam(b);
        }

        /// <summary>Destroys its objects, meshes and material, from her teardown (as the quick duck's rim, work order 047).</summary>
        public void Dispose()
        {
            for (int b = 0; b < Count; b++)
            {
                if (_mesh[b] != null) UnityEngine.Object.Destroy(_mesh[b]);
                _mesh[b] = null;
                _go[b] = null;
                _renderer[b] = null;
                _shown[b] = _dotShown[b] = _landed[b] = false;
                _body[b] = null;
                _collider[b] = null;
            }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_material != null) UnityEngine.Object.Destroy(_material);
            _root = null;
            _material = null;
            _fogMood = null;
            _fogSystem = null;
            _fogParts = null;
        }

        void HideBeam(int b)
        {
            _shown[b] = _dotShown[b] = false;
            if (_go[b] != null && _go[b].activeSelf) _go[b].SetActive(false);
        }

        bool DrawBeam(int b, Transform her, Transform muzzle, Camera cam, bool fog, Vector2 fogMiddle, Vector2 fogHalf, float time)
        {
            LaserBeamValues v = Values;
            Vector3 from = muzzle.position;
            Vector3 dir = muzzle.right;
            if (dir.sqrMagnitude < 1e-8f || v.Length <= 0f) return false;
            dir.Normalize();
            float most = v.Length;
            float length = Stop(her, from, dir, most, v.BodyTop, out Damageable body, out Collider collider);
            _from[b] = from;
            _dir[b] = dir;
            _length[b] = length;
            _landed[b] = length < most;
            _body[b] = _landed[b] ? body : null;
            _collider[b] = _landed[b] ? collider : null;

            // Turned about its own length to face the camera: local z along the beam, local y toward the camera.
            Vector3 toCam = cam != null ? cam.transform.position - (from + dir * (length * 0.5f)) : Vector3.up;
            Vector3 side = Vector3.Cross(dir, toCam);
            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward);
            Quaternion turn = Quaternion.LookRotation(dir, Vector3.Cross(side, dir));
            Quaternion back = Quaternion.Inverse(turn);
            Vector3 camRight = cam != null ? back * cam.transform.right : Vector3.right;
            Vector3 camUp = cam != null ? back * cam.transform.up : Vector3.forward;
            float pixelPerMetre = 0f; // metres a pixel spans, per metre from the camera
            if (cam != null && !cam.orthographic && cam.pixelHeight > 0)
                pixelPerMetre = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
            Vector3 camAt = cam != null ? cam.transform.position : from + Vector3.up * 28f;

            Color red = Linear(v.Colour);
            float fogSum = 0f;
            for (int j = 0; j < Rows; j++)
            {
                float z = length * j / (Rows - 1);
                float s = z / most;
                Vector3 p = from + dir * z;
                float f = fog ? LaserBeam.FogAt(LaserBeam.FromEdge(p.x, p.z, fogMiddle.x, fogMiddle.y, fogHalf.x, fogHalf.y), p.y, v) : 0f;
                fogSum += f;
                float light = LaserBeam.Light(s, f, LaserBeam.Wisp(p.x, p.y, p.z, time, v), v);
                float pixel = pixelPerMetre * Vector3.Distance(camAt, p);
                // Lit in the middle, dark at the edges a whole width out: as bright and as much light as a solid band of the
                // width, its edges soft.
                float w = LaserBeam.Drawn(LaserBeam.WidthAt(s, v), pixel, v, out float share);
                Color c = new Color(red.r * light * share, red.g * light * share, red.b * light * share, 1f);
                int at = j * Across;
                _verts[at] = new Vector3(-w, 0f, z);
                _verts[at + 1] = new Vector3(0f, 0f, z);
                _verts[at + 2] = new Vector3(w, 0f, z);
                _colours[at] = Color.black;
                _colours[at + 1] = c;
                _colours[at + 2] = Color.black;
            }
            _fog[b] = fogSum / Rows;

            // The motes: lit only while one drifts through the beam within what it reaches.
            int specks = Mathf.Clamp(v.Specks, 0, MostSpecks);
            float half = v.SpeckSize * 0.5f;
            for (int i = 0; i < MostSpecks; i++)
            {
                int at = SpeckBase + i * Spot;
                float lit = 0f, along = 0f;
                if (i < specks) LaserBeam.Speck(i, b + 1, time, v, out along, out lit);
                float z = along * length; // somewhere along what it reaches
                if (lit <= 0f || z <= 0f || z >= length)
                {
                    Collapse(at, Spot);
                    continue;
                }
                float light = v.SpeckGlow * lit * LaserBeam.Fade(z / most, v);
                SoftSpot(at, new Vector3(0f, 0f, z), camRight * half, camUp * half, new Color(red.r * light, red.g * light, red.b * light, 1f));
            }

            // The dot where it lands, off unless asked for.
            bool dot = v.DotOn && _landed[b];
            _dotShown[b] = dot;
            if (dot)
            {
                Vector3 end = new Vector3(0f, 0f, length) + back * (toCam.normalized * DotLift);
                float light = LaserBeam.DotLight(length / most, v);
                float d = v.DotSize * 0.5f;
                SoftSpot(DotBase, end, camRight * d, camUp * d, new Color(red.r * light, red.g * light, red.b * light, 1f));
                _dotAt[b] = turn * end + from;
            }
            else
            {
                Collapse(DotBase, Spot);
                _dotAt[b] = from + dir * length;
            }

            Mesh mesh = _mesh[b];
            mesh.vertices = _verts;
            mesh.colors = _colours;
            mesh.bounds = new Bounds(new Vector3(0f, 0f, length * 0.5f), new Vector3(0.3f, 0.3f, length + 0.3f));
            _go[b].transform.SetPositionAndRotation(from, turn);
            if (!_go[b].activeSelf) _go[b].SetActive(true);
            _shown[b] = true;
            return true;
        }

        /// <summary>
        /// How far along the ray from <paramref name="from"/> it goes before the first thing it meets, at most <paramref name="most"/>:
        /// a collider that is not hers and not a trigger, the floor's level at height 0, or a living target's body.
        /// </summary>
        static float Stop(Transform her, Vector3 from, Vector3 dir, float most, float bodyTop, out Damageable body, out Collider collider)
        {
            body = null;
            collider = null;
            float nearest = most;
            int n = Physics.RaycastNonAlloc(from, dir, s_hits, most, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = s_hits[i].collider;
                if (c == null || s_hits[i].distance >= nearest) continue;
                if (her != null && c.transform.IsChildOf(her)) continue;
                nearest = s_hits[i].distance;
                collider = c;
            }
            if (dir.y < -1e-4f && from.y > 0f)
            {
                float floor = -from.y / dir.y; // the floors' tops lie at 0 (GeneratedLevel, the practice room)
                if (floor < nearest)
                {
                    nearest = floor;
                    collider = null;
                }
            }
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || !d.IsAlive || !d.isActiveAndEnabled) continue;
                Vector3 p = d.transform.position; // where it stands: its body rises from here
                if (LaserBeam.HitsBody(from.x, from.y - p.y, from.z, dir.x, dir.y, dir.z, p.x, p.z, d.Radius, bodyTop, nearest, out float t) && t < nearest)
                {
                    nearest = t;
                    body = d;
                    collider = null;
                }
            }
            return nearest;
        }

        /// <summary>A soft spot facing the camera: lit at <paramref name="middle"/>, dark at four points round it twice its half size out (about the light of a square of that size, softer).</summary>
        void SoftSpot(int at, Vector3 middle, Vector3 right, Vector3 up, Color c)
        {
            _verts[at] = middle;
            _verts[at + 1] = middle + right * 2f;
            _verts[at + 2] = middle + up * 2f;
            _verts[at + 3] = middle - right * 2f;
            _verts[at + 4] = middle - up * 2f;
            _colours[at] = c;
            for (int k = 1; k < Spot; k++) _colours[at + k] = Color.black;
        }

        void Collapse(int at, int count)
        {
            for (int k = 0; k < count; k++)
            {
                _verts[at + k] = Vector3.zero;
                _colours[at + k] = Color.clear;
            }
        }

        /// <summary>
        /// The fog of the room she is in, as <see cref="RoomMood"/> laid it: the middle and half sizes of the rectangle along
        /// whose edge its particles are emitted (the room's largest open rectangle less 0.8 m a side). False with no mood, no
        /// fog, or the room's air off.
        /// </summary>
        bool FogRect(out Vector2 middle, out Vector2 half)
        {
            middle = half = Vector2.zero;
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            RoomMood mood = level != null ? level.Mood : null;
            if (mood == null) return false;
            int here = mood.Here;
            if (mood != _fogMood || here != _fogRoom || (_fogParts != null && _fogParts.Fog != _fogSystem))
            {
                _fogMood = mood;
                _fogRoom = here;
                _fogParts = here >= 0 ? mood.Of(here) : null;
                _fogSystem = _fogParts != null ? _fogParts.Fog : null;
                if (_fogSystem != null)
                {
                    Vector3 scale = _fogSystem.shape.scale;
                    _fogHalf = new Vector2(scale.x * 0.5f, scale.z * 0.5f);
                }
            }
            if (_fogSystem == null || _fogParts == null || !_fogParts.AirOn || !_fogSystem.gameObject.activeInHierarchy) return false;
            Vector3 p = _fogSystem.transform.position;
            middle = new Vector2(p.x, p.z);
            half = _fogHalf;
            return true;
        }

        void Make(PlayerCharacter her)
        {
            _material = MoodShapes.MakeUnlit("LaserSight", MoodShapes.Blend.Add, null, Queue);
            _root = new GameObject("LaserSight");
            LaserSightLate late = _root.AddComponent<LaserSightLate>();
            late.Owner = her;
            var triangles = new int[(Rows - 1) * 12 + (MostSpecks + 1) * 12];
            int t = 0;
            for (int j = 0; j < Rows - 1; j++)
            {
                int a = j * Across, n = a + Across; // this row's left, middle, right; the next row's
                triangles[t++] = a; triangles[t++] = n; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = n; triangles[t++] = n + 1;
                triangles[t++] = a + 1; triangles[t++] = n + 1; triangles[t++] = a + 2;
                triangles[t++] = a + 2; triangles[t++] = n + 1; triangles[t++] = n + 2;
            }
            for (int q = 0; q <= MostSpecks; q++)
            {
                int a = SpeckBase + q * Spot; // the last one is the dot's (DotBase)
                for (int k = 1; k < Spot; k++)
                {
                    triangles[t++] = a; triangles[t++] = a + k; triangles[t++] = a + (k % (Spot - 1)) + 1;
                }
            }
            for (int b = 0; b < Count; b++)
            {
                var mesh = new Mesh { name = b == Right ? "LaserSight_R" : "LaserSight_L" };
                mesh.MarkDynamic();
                mesh.vertices = _verts;
                mesh.colors = _colours;
                mesh.triangles = triangles;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                _go[b] = go;
                _mesh[b] = mesh;
                _renderer[b] = r;
            }
        }

        static Color Linear(Rgb c)
        {
            var p = new Color(c.R, c.G, c.B, 1f);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
        }
    }
}
