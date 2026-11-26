using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Roguelike
{
    /// <summary>
    /// The WEAPON tab's stage (work order 139; Kristian, 2026-11-25: "Weapon upgrade som en egen fane, der man ser modellen til
    /// våpenet i høy res, kan dra og se rundt. Så kan man legge til attachments som gir buff"): her pistol on a small stage of its
    /// own, far below the floor on a layer of its own (<see cref="Layer"/>), with a camera and three lights of its own, drawn into a
    /// texture the tab shows (<see cref="Texture"/>); nothing of the run is behind it. The camera is never on by itself: the stage
    /// draws by hand in its LateUpdate, and only when something changed (the turn, the parts, its size). Around that one draw
    /// its lights are on and the scene's lights and fog are off, and put back at once; the game's camera and its look are never
    /// touched, nor any shared material.
    ///
    /// The model: whatever is at <see cref="WeaponStageValues.ModelPath"/> in Resources (a prefab or a model file, or a mesh
    /// with a material of the same name), read without making its scripts; until something is there, her pistol copied from her
    /// character model (her right hand's, as she wears it, in its own materials and not the toon look's), and if she has none a
    /// simple pistol made of boxes. It is laid by <see cref="WeaponStageValues.ModelTurn"/>, scaled to
    /// <see cref="WeaponStageValues.ModelLength"/> and centred; the three mount points are places in its box
    /// (<see cref="WeaponStageValues.MountOf"/>). Each fitted part is a simple shape made in code at its mount, in the model's
    /// greys with a teal accent, turning with the model; a part previewed and not owned is drawn ghosted.
    ///
    /// It lives while the menu keeps it (<see cref="Keep"/>, every frame the WEAPON tab is up) and goes the frame after it is not
    /// kept, freeing its texture and everything it made. It is made by the menu, never in Awake, and reads nothing in Awake.
    /// </summary>
    public sealed class WeaponStage : MonoBehaviour
    {
        /// <summary>The layer the stage and its camera are on: the last one, unnamed and unused by the game.</summary>
        public const int Layer = 31;
        /// <summary>Where the stage stands: far below every floor and the practice room, out of the game's camera's sight.</summary>
        public static readonly Vector3 Place = new Vector3(0f, -2000f, 0f);

        /// <summary>Where the model on the stage came from.</summary>
        public enum Source { NamedPlace, HerPistol, CodeMade }

        /// <summary>The one standing now, or null.</summary>
        public static WeaponStage Current { get; private set; }

        /// <summary>
        /// A test's model in place of Resources' (it returns what <see cref="WeaponStageValues.ModelPath"/> would hold: a
        /// GameObject, or a Mesh; null as if nothing were there). Null: Resources.
        /// </summary>
        public static Func<Object> LoadHook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            LoadHook = null;
        }

        /// <summary>The one standing, made if there is none.</summary>
        public static WeaponStage Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("WeaponStage") { hideFlags = HideFlags.DontSave, layer = Layer };
            go.transform.position = Place;
            Current = go.AddComponent<WeaponStage>();
            return Current;
        }

        /// <summary>How the model is turned and how near the camera is.</summary>
        public readonly WeaponTurn Turn = new WeaponTurn();
        /// <summary>The picture of the stage, made at its size (null until it first draws).</summary>
        public RenderTexture Texture => _rt;
        public Source ModelSource { get; private set; }
        /// <summary>How many times it drew (for tests: once for each change, none while nothing changes).</summary>
        public int Draws { get; private set; }
        /// <summary>The model's half size along each axis on the stage, after it is laid and scaled.</summary>
        public Vector3 Half => _half;
        /// <summary>The meshes the model is drawn with (for tests: the stage shows what is at the named place).</summary>
        public IReadOnlyList<Mesh> ModelMeshes => _modelMeshes;
        /// <summary>The camera (for tests: where a mount falls in the picture).</summary>
        public Camera Camera => _cam;

        // ---------------- Kept by the menu ----------------

        int _keptFrame = -10;

        /// <summary>The menu wants it this frame: it is built if it is not, and it stays.</summary>
        public void Keep()
        {
            _keptFrame = Time.frameCount;
            if (!_built) Build();
        }

        /// <summary>The size the tab shows it at, in layout pixels of the 1080-high layout (its texture and the camera's shape follow).</summary>
        public void SetView(float layoutWidth, float layoutHeight)
        {
            float scale = WeaponStageValues.Supersample * Mathf.Max(1f, Screen.height / 1080f);
            int w = Mathf.Clamp(Mathf.RoundToInt(layoutWidth * scale), 16, WeaponStageValues.MostTexture);
            int h = Mathf.Clamp(Mathf.RoundToInt(layoutHeight * scale), 16, WeaponStageValues.MostTexture);
            if (w == _wantW && h == _wantH) return;
            _wantW = w;
            _wantH = h;
            _dirty = true;
        }

        int _wantW = 1936, _wantH = 1292;

        // ---------------- What is on it ----------------

        readonly string[] _shown = new string[3];
        readonly bool[] _ghost = new bool[3];
        readonly Transform[] _mount = new Transform[3];
        readonly Transform[] _part = new Transform[3];

        /// <summary>The part on a slot (null: none), drawn ghosted when <paramref name="ghost"/>; made again only when it changed.</summary>
        public void Show(WeaponSlot slot, string partId, bool ghost)
        {
            int i = (int)slot;
            if (string.Equals(_shown[i], partId, StringComparison.Ordinal) && _ghost[i] == ghost && (_part[i] != null) == (partId != null)) return;
            _shown[i] = partId;
            _ghost[i] = ghost;
            if (_built) MakePart(slot);
            _dirty = true;
        }

        /// <summary>The part shown on a slot now (null: none), and whether it is ghosted. For tests.</summary>
        public string Shown(WeaponSlot slot) => _shown[(int)slot];
        public bool Ghosted(WeaponSlot slot) => _ghost[(int)slot];
        /// <summary>The shape of the part on a slot (null: none). For tests.</summary>
        public Transform PartOn(WeaponSlot slot) => _part[(int)slot];
        /// <summary>The mount of a slot on the turning model. For tests.</summary>
        public Transform MountOn(WeaponSlot slot) => _mount[(int)slot];

        /// <summary>Where a slot's mount falls in the stage's picture: 0 to 1 across from the left and down from the top; false when behind the camera or not built.</summary>
        public bool MountInPicture(WeaponSlot slot, out Vector2 at)
        {
            at = Vector2.zero;
            Transform m = _mount[(int)slot];
            if (m == null || _cam == null) return false;
            PoseNow();
            Vector3 p = _cam.WorldToViewportPoint(m.position);
            at = new Vector2(p.x, 1f - p.y);
            return p.z > 0f;
        }

        // ---------------- Built once ----------------

        bool _built, _dirty = true;
        Transform _pivot, _laid;
        Camera _cam;
        readonly List<Light> _lights = new List<Light>();
        readonly List<Light> _sceneLights = new List<Light>(), _turnedOff = new List<Light>();
        readonly List<Mesh> _modelMeshes = new List<Mesh>();
        readonly List<Object> _made = new List<Object>(); // materials and meshes made here, freed with it
        RenderTexture _rt;
        Vector3 _half = new Vector3(0.5f, 0.25f, 0.05f);
        Mesh _cube, _cylinder, _sphere, _ring;
        Material _body, _accent, _dot, _ghostBody, _ghostAccent, _ghostDot;

        // The stage's lights and backdrop (named values: first guesses, set from the pictures).
        /// <summary>The backdrop the model stands on: a dark slate, a step lighter than the menu's panels, so her dark steel reads against it.</summary>
        static readonly Color Backdrop = new Color(0.115f, 0.125f, 0.14f, 1f);
        /// <summary>The key light from above at her left and in front, warm white; the fill from the right, cool and weaker; the rim from behind, teal-white, to cut the model's edge from the backdrop.</summary>
        static readonly Vector3 KeyFrom = new Vector3(-0.55f, 0.75f, -0.6f), FillFrom = new Vector3(0.8f, 0.15f, -0.45f), RimFrom = new Vector3(0.2f, 0.45f, 0.95f);
        static readonly Color KeyColor = new Color(1f, 0.96f, 0.9f), FillColor = new Color(0.95f, 0.95f, 1f), RimColor = new Color(0.85f, 1f, 0.97f);
        /// <summary>Set from the pictures: at 1.6, 0.55 and 1.1 her pistol's faces came out a quarter lighter and pinker than her picture's rose-brown.</summary>
        const float KeyIntensity = 1.2f, FillIntensity = 0.35f, RimIntensity = 0.7f;
        /// <summary>
        /// The parts' colours: the model's own (a darker shade of her pistol's rose-brown: a steel grey read cold beside it in the
        /// pictures), the menu's teal for the accent, the laser's red dot.
        /// </summary>
        static readonly Color PartBody = new Color(0.27f, 0.19f, 0.2f), PartAccent = new Color(0.05f, 0.95f, 0.88f), PartDot = new Color(0.96f, 0.14f, 0.24f);
        const float PartMetallic = 0.35f, PartSmoothness = 0.5f, AccentGlow = 1f, DotGlow = 1f; // more glow ran the red dot to pink (no bloom on the stage)
        /// <summary>A part previewed and not owned: drawn see-through and pale (a dark see-through part read as a solid one), its accents as they are, a little see-through.</summary>
        static readonly Color GhostBody = new Color(0.78f, 0.9f, 0.9f);
        const float GhostAlpha = 0.38f;

        void Build()
        {
            _built = true;
            gameObject.layer = Layer;
            _pivot = Child("Turn", transform);
            var camGo = new GameObject("StageCamera") { hideFlags = HideFlags.DontSave, layer = Layer };
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.cullingMask = 1 << Layer;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Backdrop;
            _cam.fieldOfView = WeaponStageValues.FieldOfView;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 20f;
            _cam.allowHDR = false;
            _cam.allowMSAA = true;
            _cam.useOcclusionCulling = false;
            AddLight("Key", KeyFrom, KeyColor, KeyIntensity);
            AddLight("Fill", FillFrom, FillColor, FillIntensity);
            AddLight("Rim", RimFrom, RimColor, RimIntensity);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (!_lights.Contains(l)) _sceneLights.Add(l);
            MakeMaterials();
            BuildModel();
            for (int s = 0; s < 3; s++) MakePart((WeaponSlot)s);
            _dirty = true;
        }

        Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = Layer };
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        void AddLight(string name, Vector3 from, Color color, float intensity)
        {
            Transform t = Child(name, transform);
            t.rotation = Quaternion.LookRotation(-from.normalized, Vector3.up);
            Light l = t.gameObject.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << Layer;
            l.enabled = false; // only around the stage's own draw
            _lights.Add(l);
        }

        // ---------------- The model ----------------

        void BuildModel()
        {
            _laid = Child("Model", _pivot);
            _modelMeshes.Clear();
            Object found = LoadHook != null ? LoadHook() : LoadNamed();
            Bounds box = default;
            bool any = false;
            Quaternion turn = Quaternion.Euler(WeaponStageValues.ModelTurn.X, WeaponStageValues.ModelTurn.Y, WeaponStageValues.ModelTurn.Z);
            Matrix4x4 lay = Matrix4x4.Rotate(turn);
            // A prefab's or a model file's renderers as they lie in it, its root's own turn kept (a model file's import puts its
            // axes' turn on the root: undoing it laid the new pistol on its side); its place and size are undone below.
            if (found is GameObject prefab && CopyRenderers(prefab.transform, Matrix4x4.identity, lay, ref box, ref any, null))
                ModelSource = Source.NamedPlace;
            else if (found is Mesh mesh)
            {
                Material mat = LoadHook == null ? Resources.Load<Material>(WeaponStageValues.ModelPath) : null;
                AddCopy(mesh, mat != null ? new[] { mat } : new[] { _body }, lay, ref box, ref any);
                ModelSource = Source.NamedPlace;
            }
            else
            {
                CharacterAnimation her = FindAnyObjectByType<CharacterAnimation>();
                Transform holder = her != null ? her.RightWeapon : null;
                ToonLook toon = her != null ? her.GetComponentInParent<ToonLook>() ?? FindAnyObjectByType<ToonLook>() : null;
                if (holder != null && CopyRenderers(holder, holder.worldToLocalMatrix, lay, ref box, ref any, toon)) ModelSource = Source.HerPistol;
                else
                {
                    CodeMadePistol(lay, ref box, ref any);
                    ModelSource = Source.CodeMade;
                }
            }
            // Scaled so its longest side is the named length, and centred on the turn's middle.
            float longest = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
            float scale = longest > 1e-5f ? WeaponStageValues.ModelLength / longest : 1f;
            _laid.localScale = Vector3.one * scale;
            _laid.localPosition = -box.center * scale;
            _half = box.extents * scale;
            for (int s = 0; s < 3; s++) PlaceMount((WeaponSlot)s);
        }

        static Object LoadNamed()
        {
            Object o = Resources.Load<GameObject>(WeaponStageValues.ModelPath);
            return o != null ? o : Resources.Load<Mesh>(WeaponStageValues.ModelPath);
        }

        /// <summary>Copies of the renderers under <paramref name="root"/> (a prefab's, read without making it, or her holder's), in its space; their materials as they are (today's, not the toon look's).</summary>
        bool CopyRenderers(Transform root, Matrix4x4 rootInverse, Matrix4x4 lay, ref Bounds box, ref bool any, ToonLook toon)
        {
            bool copied = false;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || r == null) continue;
                AddCopy(f.sharedMesh, TodaysMaterials(r, toon), lay * rootInverse * f.transform.localToWorldMatrix, ref box, ref any);
                copied = true;
            }
            foreach (SkinnedMeshRenderer sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sk.sharedMesh == null) continue;
                AddCopy(sk.sharedMesh, TodaysMaterials(sk, toon), lay * rootInverse * sk.transform.localToWorldMatrix, ref box, ref any);
                copied = true;
            }
            return copied;
        }

        static Material[] TodaysMaterials(Renderer r, ToonLook toon)
        {
            if (toon != null)
                for (int i = 0; i < toon.TargetCount; i++)
                    if (toon.TargetRenderer(i) == r)
                    {
                        IReadOnlyList<Material> before = toon.TodaysOf(i);
                        var m = new Material[before.Count];
                        for (int k = 0; k < m.Length; k++) m[k] = before[k];
                        return m;
                    }
            return r.sharedMaterials;
        }

        void AddCopy(Mesh mesh, Material[] materials, Matrix4x4 m, ref Bounds box, ref bool any)
        {
            Transform t = Child(mesh.name, _laid);
            t.localPosition = m.GetColumn(3);
            Vector3 fwd = m.GetColumn(2), up = m.GetColumn(1);
            if (fwd.sqrMagnitude > 1e-12f && up.sqrMagnitude > 1e-12f) t.localRotation = Quaternion.LookRotation(fwd, up);
            t.localScale = m.lossyScale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _modelMeshes.Add(mesh);
            Bounds b = mesh.bounds;
            Matrix4x4 placed = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                Vector3 p = placed.MultiplyPoint3x4(corner);
                if (!any)
                {
                    box = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else box.Encapsulate(p);
            }
        }

        /// <summary>Her pistol made of boxes when there is no model at all: a slide, a barrel, a handle leaning back, a trigger guard.</summary>
        void CodeMadePistol(Matrix4x4 lay, ref Bounds box, ref bool any)
        {
            Mesh cube = Cube;
            Material[] m = { _body };
            AddCopy(cube, m, lay * Matrix4x4.TRS(new Vector3(0f, 0.07f, 0f), Quaternion.identity, new Vector3(1f, 0.16f, 0.12f)), ref box, ref any);
            AddCopy(cube, m, lay * Matrix4x4.TRS(new Vector3(-0.3f, -0.2f, 0f), Quaternion.Euler(0f, 0f, -14f), new Vector3(0.2f, 0.42f, 0.11f)), ref box, ref any);
            AddCopy(cube, m, lay * Matrix4x4.TRS(new Vector3(-0.06f, -0.08f, 0f), Quaternion.identity, new Vector3(0.16f, 0.03f, 0.05f)), ref box, ref any);
        }

        void PlaceMount(WeaponSlot slot)
        {
            int i = (int)slot;
            if (_mount[i] == null) _mount[i] = Child("Mount " + slot, _pivot);
            WeaponMount w = WeaponStageValues.MountOf(slot);
            _mount[i].localPosition = new Vector3(w.At.X * _half.x, w.At.Y * _half.y, w.At.Z * _half.z);
            Vector3 f = new Vector3(w.Forward.X, w.Forward.Y, w.Forward.Z), u = new Vector3(w.Up.X, w.Up.Y, w.Up.Z);
            if (f.sqrMagnitude < 1e-8f) f = Vector3.right;
            f.Normalize();
            u = Vector3.ProjectOnPlane(u, f);
            if (u.sqrMagnitude < 1e-8f) u = Vector3.up;
            _mount[i].localRotation = Quaternion.LookRotation(Vector3.Cross(f, u.normalized), u.normalized); // its +X along Forward, +Y along Up
        }

        /// <summary>The mounts and the parts again from the values (a test or the producer changed a mount).</summary>
        public void Remount()
        {
            if (!_built) return;
            for (int s = 0; s < 3; s++)
            {
                PlaceMount((WeaponSlot)s);
                MakePart((WeaponSlot)s);
            }
            _dirty = true;
        }

        // ---------------- The parts' shapes ----------------

        /// <summary>The handle's depth from front to back and its thickness, as shares of the model's length and of its whole thickness (the grip's shapes wrap it); from the new pistol's pictures.</summary>
        const float HandleDepth = 0.15f, HandleThickness = 0.75f;
        /// <summary>From the grip's mount (the handle's middle) to the handle's foot, down the handle, as a share of the model's length.</summary>
        const float HandleFoot = 0.11f;

        void MakePart(WeaponSlot slot)
        {
            int i = (int)slot;
            if (_part[i] != null) Destroy(_part[i].gameObject);
            _part[i] = null;
            string id = _shown[i];
            if (id == null || _mount[i] == null) return;
            Transform p = Child(id, _mount[i]);
            _part[i] = p;
            bool g = _ghost[i];
            Material body = g ? _ghostBody : _body, accent = g ? _ghostAccent : _accent, dot = g ? _ghostDot : _dot;
            float thick = Mathf.Max(0.04f, _half.z * 2f * HandleThickness); // the handle's thickness: the grip's wraps go round it
            float depth = HandleDepth, foot = HandleFoot;
            switch (id)
            {
                // The sights, standing on the slide (their +X the barrel's way, +Y up).
                case WeaponAttachments.LaserSight: // a short tube with a red dot at its front, on a small block
                    Box(p, body, new Vector3(0f, 0.012f, 0f), new Vector3(0.1f, 0.024f, 0.036f));
                    Tube(p, body, new Vector3(0f, 0.05f, 0f), 0.16f, 0.026f);
                    Ring(p, accent, new Vector3(-0.055f, 0.05f, 0f), 0.026f);
                    Ball(p, dot, new Vector3(0.082f, 0.05f, 0f), 0.034f);
                    break;
                case WeaponAttachments.GhostRing: // a ring on a post, a teal bead on its top
                    Box(p, body, new Vector3(0f, 0.028f, 0f), new Vector3(0.03f, 0.056f, 0.03f));
                    Ring(p, body, new Vector3(0f, 0.1f, 0f), 0.044f);
                    Ball(p, accent, new Vector3(0f, 0.148f, 0f), 0.016f);
                    break;
                case WeaponAttachments.ReflexSight: // a small window on a base: two posts, a bar, a teal pane
                    Box(p, body, new Vector3(0f, 0.01f, 0f), new Vector3(0.11f, 0.02f, 0.056f));
                    Box(p, body, new Vector3(0.025f, 0.055f, 0.027f), new Vector3(0.022f, 0.074f, 0.008f));
                    Box(p, body, new Vector3(0.025f, 0.055f, -0.027f), new Vector3(0.022f, 0.074f, 0.008f));
                    Box(p, body, new Vector3(0.025f, 0.094f, 0f), new Vector3(0.022f, 0.01f, 0.062f));
                    Box(p, accent, new Vector3(0.025f, 0.054f, 0f), new Vector3(0.006f, 0.068f, 0.046f));
                    break;
                // The barrels, out of the muzzle (+X out of it).
                case WeaponAttachments.BullBarrel: // a thick sleeve over the muzzle, a teal band at its mouth
                    Tube(p, body, new Vector3(0.04f, 0f, 0f), 0.15f, 0.07f);
                    Ring(p, accent, new Vector3(0.112f, 0f, 0f), 0.066f);
                    break;
                case WeaponAttachments.LongBarrel: // a long thin barrel, a teal band at its end
                    Tube(p, body, new Vector3(0.17f, 0f, 0f), 0.34f, 0.036f);
                    Ring(p, accent, new Vector3(0.335f, 0f, 0f), 0.034f);
                    break;
                case WeaponAttachments.Compensator: // a block on the muzzle a little wider than the barrel, two teal slits on its top
                    Box(p, body, new Vector3(0.065f, 0f, 0f), new Vector3(0.12f, 0.14f, 0.12f));
                    Box(p, accent, new Vector3(0.045f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f));
                    Box(p, accent, new Vector3(0.088f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f));
                    break;
                // The grips, on the handle (+X down the handle, +Y toward its front).
                case WeaponAttachments.RunnersGrip: // three rings round the handle, the middle one teal (flat plates read as plates: the pictures)
                    Band(p, body, -0.065f, depth, thick);
                    Band(p, accent, 0f, depth, thick);
                    Band(p, body, 0.065f, depth, thick);
                    break;
                case WeaponAttachments.WeightedGrip: // a heavy block at the handle's foot, a teal plate on its side
                    Box(p, body, new Vector3(foot + 0.02f, 0f, 0f), new Vector3(0.05f, depth * 1.12f, thick * 1.25f));
                    Box(p, accent, new Vector3(foot + 0.02f, 0f, -thick * 0.63f), new Vector3(0.026f, depth * 0.7f, 0.006f));
                    Box(p, accent, new Vector3(foot + 0.02f, 0f, thick * 0.63f), new Vector3(0.026f, depth * 0.7f, 0.006f));
                    break;
                case WeaponAttachments.QuickDrawGrip: // a flared well at the handle's foot, a thin teal stripe up its back
                    Box(p, body, new Vector3(foot, 0f, 0f), new Vector3(0.03f, depth * 1.18f, thick * 1.3f));
                    Box(p, accent, new Vector3(0f, -depth * 0.52f, 0f), new Vector3(foot * 1.6f, 0.01f, 0.018f));
                    break;
                default: // a part this drawing does not know: a plain block, so it still shows
                    Box(p, body, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f));
                    break;
            }
        }

        void Box(Transform parent, Material m, Vector3 at, Vector3 size) => Shape(parent, Cube, m, at, Quaternion.identity, size);
        /// <summary>A tube along the part's +X, this long and this thick (radius).</summary>
        void Tube(Transform parent, Material m, Vector3 at, float length, float radius) =>
            Shape(parent, Cylinder, m, at, Quaternion.Euler(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        /// <summary>A ring round the handle (its hole along the part's +X, down the handle) this far down from the mount, wrapping a handle this deep and this thick.</summary>
        void Band(Transform parent, Material m, float along, float depth, float thick) =>
            Shape(parent, RingMesh, m, new Vector3(along, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(thick * 0.46f, depth * 0.46f, 0.08f));
        void Ball(Transform parent, Material m, Vector3 at, float size) => Shape(parent, Sphere, m, at, Quaternion.identity, Vector3.one * size);
        /// <summary>A ring standing across the barrel's way (its hole along +X), this wide (its middle's radius).</summary>
        void Ring(Transform parent, Material m, Vector3 at, float radius) => Shape(parent, RingMesh, m, at, Quaternion.Euler(0f, 90f, 0f), Vector3.one * radius);

        void Shape(Transform parent, Mesh mesh, Material m, Vector3 at, Quaternion turn, Vector3 size)
        {
            Transform t = Child(mesh.name, parent);
            t.localPosition = at;
            t.localRotation = turn;
            t.localScale = size;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        Mesh Cube => _cube != null ? _cube : (_cube = Primitive(PrimitiveType.Cube));
        Mesh Cylinder => _cylinder != null ? _cylinder : (_cylinder = Primitive(PrimitiveType.Cylinder));
        Mesh Sphere => _sphere != null ? _sphere : (_sphere = Primitive(PrimitiveType.Sphere));

        /// <summary>The engine's own shape's mesh (shared, never changed); the object made to get it goes at once.</summary>
        static Mesh Primitive(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
            return mesh;
        }

        /// <summary>A ring of radius 1 in the XY plane, its tube this share of it thick (made once, in code).</summary>
        Mesh RingMesh
        {
            get
            {
                if (_ring != null) return _ring;
                const int Around = 40, Tube = 12;
                const float Thick = 0.2f;
                var vertices = new Vector3[(Around + 1) * (Tube + 1)];
                var normals = new Vector3[vertices.Length];
                var triangles = new int[Around * Tube * 6];
                int t = 0;
                for (int a = 0; a <= Around; a++)
                {
                    float u = a * Mathf.PI * 2f / Around;
                    var c = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                    for (int b = 0; b <= Tube; b++)
                    {
                        float v = b * Mathf.PI * 2f / Tube;
                        Vector3 n = c * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                        vertices[a * (Tube + 1) + b] = c + n * Thick;
                        normals[a * (Tube + 1) + b] = n;
                    }
                }
                for (int a = 0; a < Around; a++)
                    for (int b = 0; b < Tube; b++)
                    {
                        int i0 = a * (Tube + 1) + b, i1 = (a + 1) * (Tube + 1) + b;
                        triangles[t++] = i0; triangles[t++] = i0 + 1; triangles[t++] = i1;
                        triangles[t++] = i1; triangles[t++] = i0 + 1; triangles[t++] = i1 + 1;
                    }
                _ring = new Mesh { name = "WeaponStageRing", vertices = vertices, normals = normals, triangles = triangles };
                _made.Add(_ring);
                return _ring;
            }
        }

        void MakeMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            _body = Lit(lit, "Body", PartBody, 0f, false);
            _accent = Lit(lit, "Accent", PartAccent, AccentGlow, false);
            _dot = Lit(lit, "Dot", PartDot, DotGlow, false);
            _ghostBody = Lit(lit, "GhostBody", GhostBody, 0f, true);
            _ghostAccent = Lit(lit, "GhostAccent", PartAccent, AccentGlow, true);
            _ghostDot = Lit(lit, "GhostDot", PartDot, DotGlow, true);
        }

        Material Lit(Shader shader, string name, Color color, float glow, bool ghost)
        {
            var m = new Material(shader) { name = "WeaponStage_" + name };
            if (ghost) color.a = GhostAlpha;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", glow > 0f ? 0f : PartMetallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", PartSmoothness);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * glow);
            }
            if (ghost)
            {
                // See-through, as the code-made smoke is (work order 126's way: the shader blends by its keyword).
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            _made.Add(m);
            return m;
        }

        // ---------------- Every frame ----------------

        float _drawnYaw = float.NaN, _drawnPitch, _drawnDistance;

        void LateUpdate()
        {
            if (Time.frameCount - _keptFrame > 1)
            {
                Destroy(gameObject); // the tab is gone: so is the stage
                return;
            }
            if (!_built) return;
            Turn.Step(Time.unscaledDeltaTime);
            DrawIfChanged();
        }

        /// <summary>Draws the stage when anything it shows changed since it last drew; else nothing (and nothing allocated by it).</summary>
        public void DrawIfChanged()
        {
            if (!_built) return;
            bool changed = _dirty || _rt == null || _rt.width != _wantW || _rt.height != _wantH
                           || Turn.Yaw != _drawnYaw || Turn.Pitch != _drawnPitch || Turn.Distance != _drawnDistance;
            if (!changed) return;
            Draw();
        }

        void PoseNow()
        {
            _pivot.localRotation = Quaternion.AngleAxis(-Turn.Pitch, Vector3.right) * Quaternion.AngleAxis(-Turn.Yaw, Vector3.up);
            _cam.transform.localPosition = new Vector3(0f, 0f, -Turn.Distance);
            _cam.transform.localRotation = Quaternion.identity;
        }

        void Draw()
        {
            if (_rt == null || _rt.width != _wantW || _rt.height != _wantH)
            {
                FreeTexture();
                _rt = new RenderTexture(_wantW, _wantH, 24, RenderTextureFormat.ARGB32)
                {
                    name = "WeaponStage",
                    antiAliasing = WeaponStageValues.Samples,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                _rt.Create();
                _cam.targetTexture = _rt;
                _cam.aspect = _wantW / (float)_wantH;
            }
            PoseNow();
            bool fog = RenderSettings.fog;
            _turnedOff.Clear();
            try
            {
                RenderSettings.fog = false;
                for (int i = 0; i < _sceneLights.Count; i++)
                {
                    Light l = _sceneLights[i];
                    if (l != null && l.enabled)
                    {
                        l.enabled = false;
                        _turnedOff.Add(l);
                    }
                }
                for (int i = 0; i < _lights.Count; i++) _lights[i].enabled = true;
                _cam.Render();
            }
            finally
            {
                for (int i = 0; i < _lights.Count; i++) _lights[i].enabled = false;
                for (int i = 0; i < _turnedOff.Count; i++)
                    if (_turnedOff[i] != null) _turnedOff[i].enabled = true;
                _turnedOff.Clear();
                RenderSettings.fog = fog;
            }
            _drawnYaw = Turn.Yaw;
            _drawnPitch = Turn.Pitch;
            _drawnDistance = Turn.Distance;
            _dirty = false;
            Draws++;
        }

        void FreeTexture()
        {
            if (_rt == null) return;
            if (_cam != null) _cam.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        void OnDestroy()
        {
            FreeTexture();
            foreach (Object o in _made)
                if (o != null) Destroy(o);
            _made.Clear();
            if (Current == this) Current = null;
        }
    }
}
