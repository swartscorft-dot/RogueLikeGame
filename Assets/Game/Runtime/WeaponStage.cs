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
    /// with a material of the same name), read without making its scripts: the prefab there is the high-detail original of the
    /// pistol in her hands (2026-11-26, the owner: "Vi lagde jo en egen model i meshy som karakteren holder i hånden. Hvorfor var
    /// ikke den igjenbrukt"; made by <c>Editor/WeaponModelImport</c>). Nothing of her figure in the scene is read: should the
    /// named place be empty (a broken import), a simple pistol made of boxes, with a warning in the log. It is laid by
    /// <see cref="WeaponStageValues.ModelTurn"/>, scaled to <see cref="WeaponStageValues.ModelLength"/> and centred; the three
    /// mount points are places in its box (<see cref="WeaponStageValues.MountOf"/>). Each fitted part is a simple shape made in
    /// code at its mount, in the model's own dark steel and bronze with a teal accent, turning with the model; a part previewed
    /// and not owned is drawn ghosted. Work order 150: while <see cref="WeaponStageValues.ModelledParts"/> is on, each part is
    /// its model from Blender instead (<see cref="WeaponStageValues.PartModelPath"/>), its shape made in code only when the
    /// model is missing.
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
        public enum Source { NamedPlace, CodeMade }

        /// <summary>The one standing now, or null.</summary>
        public static WeaponStage Current { get; private set; }

        /// <summary>
        /// A test's model in place of Resources' (it returns what <see cref="WeaponStageValues.ModelPath"/> would hold: a
        /// GameObject, or a Mesh; null as if nothing were there). Null: Resources.
        /// </summary>
        public static Func<Object> LoadHook;

        /// <summary>
        /// A test's part model in place of Resources' (work order 150: it returns what
        /// <see cref="WeaponStageValues.PartModelPath"/> would hold for the part's id: a GameObject; null as if nothing were
        /// there). Null: Resources.
        /// </summary>
        public static Func<string, Object> PartLoadHook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            LoadHook = null;
            PartLoadHook = null;
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
        /// <summary>
        /// How far the model and its mounts stand from the turn's middle: back along -X by a share of what the barrel part shown
        /// sticks out past the muzzle (<see cref="WeaponStageValues.FrameShiftShare"/>), so the pistol with its barrel stays in the
        /// frame at the nearer camera of 2026-11-26; zero with no barrel part.
        /// </summary>
        public Vector3 Shift => _shift;
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
            if (_built)
            {
                MakePart(slot);
                if (slot == WeaponSlot.Barrel) Reframe();
            }
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
        Material _body, _trim, _accent, _dot, _ghostBody, _ghostAccent, _ghostDot;
        readonly Mesh[] _bands = new Mesh[3]; // the grip's bands, made for the handle's section where each sits, once the model is built
        Vector3 _laidAt, _shift; // the model's place when centred, and the frame's shift for a barrel part

        // The stage's lights and backdrop (named values: first guesses, set from the pictures).
        /// <summary>The backdrop the model stands on: a dark slate, a step lighter than the menu's panels, so her dark steel reads against it.</summary>
        static readonly Color Backdrop = new Color(0.115f, 0.125f, 0.14f, 1f);
        /// <summary>The key light from above at her left and in front, warm white; the fill from the right, cool and weaker; the rim from behind, teal-white, to cut the model's edge from the backdrop.</summary>
        static readonly Vector3 KeyFrom = new Vector3(-0.55f, 0.75f, -0.6f), FillFrom = new Vector3(0.8f, 0.15f, -0.45f), RimFrom = new Vector3(0.2f, 0.45f, 0.95f);
        static readonly Color KeyColor = new Color(1f, 0.96f, 0.9f), FillColor = new Color(0.95f, 0.95f, 1f), RimColor = new Color(0.85f, 1f, 0.97f);
        /// <summary>
        /// Set from the pictures: at 1.6, 0.55 and 1.1 the first model's faces came out a quarter lighter than its picture; for the
        /// high-detail original (2026-11-26) the key a little softer again (1.2 before), its painted light grey on the slide read as
        /// chrome near the camera.
        /// </summary>
        const float KeyIntensity = 1.05f, FillIntensity = 0.35f, RimIntensity = 0.7f;
        /// <summary>
        /// The stage's own soft light from all round (in place of the scene's ambient around its draw, which a room's mood sets in
        /// a run): a little light from above and less from below, so the dark gunmetal keeps its shape and does not sink into the
        /// backdrop.
        /// </summary>
        static readonly Color AmbientAbove = new Color(0.36f, 0.37f, 0.4f), AmbientBelow = new Color(0.16f, 0.16f, 0.17f);
        SphericalHarmonicsL2 _stageAmbient;
        /// <summary>
        /// The parts' colours, the model's own (read off its base colour map, sRGB medians, 2026-11-26): its gunmetal for a part's
        /// body (a step lighter than the map's 0.21 and less metal: the plain shapes have none of its painted light edges and read
        /// near black beside it in the first pictures), its bronze trim for a part's rings and plates, its teal line for the accent;
        /// the laser's red dot.
        /// </summary>
        static readonly Color PartBody = new Color(0.32f, 0.315f, 0.33f), PartTrim = new Color(0.576f, 0.467f, 0.349f),
                              PartAccent = new Color(0.416f, 0.886f, 0.918f), PartDot = new Color(0.96f, 0.14f, 0.24f);
        /// <summary>The body's and the trim's metal and gloss (the model's own map: its gunmetal about 0.45 metallic, its bronze 0.7, both about half rough; less on the plain shapes, whose thin rings read as grey steel at 0.6 and 0.4 in the pictures of 2026-11-26).</summary>
        const float PartMetallic = 0.3f, TrimMetallic = 0.25f, PartSmoothness = 0.5f, AccentGlow = 1f, DotGlow = 1f; // more glow ran the red dot to pink (no bloom on the stage)
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
            _stageAmbient = new SphericalHarmonicsL2();
            _stageAmbient.AddAmbientLight((AmbientAbove + AmbientBelow) * 0.5f);
            _stageAmbient.AddDirectionalLight(Vector3.up, (AmbientAbove - AmbientBelow) * 0.5f, 1f);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (!_lights.Contains(l)) _sceneLights.Add(l);
            MakeMaterials();
            BuildModel();
            for (int s = 0; s < 3; s++) MakePart((WeaponSlot)s);
            Reframe();
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
            if (found is GameObject prefab && CopyRenderers(prefab.transform, lay, ref box, ref any))
                ModelSource = Source.NamedPlace;
            else if (found is Mesh mesh)
            {
                Material mat = LoadHook == null ? Resources.Load<Material>(WeaponStageValues.ModelPath) : null;
                AddCopy(mesh, mat != null ? new[] { mat } : new[] { _body }, lay, ref box, ref any);
                ModelSource = Source.NamedPlace;
            }
            else
            {
                // Nothing at the named place: never her figure's pistol (the stage does not depend on her being in the scene).
                if (LoadHook == null) Debug.LogWarning("[WeaponStage] nothing at Resources/" + WeaponStageValues.ModelPath + ": the stage shows a pistol made of boxes");
                CodeMadePistol(lay, ref box, ref any);
                ModelSource = Source.CodeMade;
            }
            // Scaled so its longest side is the named length, and centred on the turn's middle.
            float longest = Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
            float scale = longest > 1e-5f ? WeaponStageValues.ModelLength / longest : 1f;
            _laid.localScale = Vector3.one * scale;
            _laidAt = -box.center * scale;
            _laid.localPosition = _laidAt + _shift;
            _half = box.extents * scale;
            for (int s = 0; s < 3; s++) PlaceMount((WeaponSlot)s);
        }

        static Object LoadNamed()
        {
            Object o = Resources.Load<GameObject>(WeaponStageValues.ModelPath);
            return o != null ? o : Resources.Load<Mesh>(WeaponStageValues.ModelPath);
        }

        /// <summary>Copies of the renderers under <paramref name="root"/> (a prefab's or a model file's, read without making it), in its space, with their own materials.</summary>
        bool CopyRenderers(Transform root, Matrix4x4 lay, ref Bounds box, ref bool any)
        {
            bool copied = false;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || r == null) continue;
                AddCopy(f.sharedMesh, r.sharedMaterials, lay * f.transform.localToWorldMatrix, ref box, ref any, true);
                copied = true;
            }
            foreach (SkinnedMeshRenderer sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sk.sharedMesh == null) continue;
                AddCopy(sk.sharedMesh, sk.sharedMaterials, lay * sk.transform.localToWorldMatrix, ref box, ref any, true);
                copied = true;
            }
            return copied;
        }

        void AddCopy(Mesh mesh, Material[] materials, Matrix4x4 m, ref Bounds box, ref bool any, bool model = false)
        {
            Transform t = Child(mesh.name, _laid);
            t.localPosition = m.GetColumn(3);
            Vector3 fwd = m.GetColumn(2), up = m.GetColumn(1);
            if (fwd.sqrMagnitude > 1e-12f && up.sqrMagnitude > 1e-12f) t.localRotation = Quaternion.LookRotation(fwd, up);
            t.localScale = m.lossyScale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>();
            bool toon = model && WeaponStageValues.ToonOnStage;
            r.sharedMaterials = toon ? Toon(materials) : materials;
            if (toon) ToonInkFigures.Mark(r, true); // the ink line round it, as round her in a run (the clean line asks for the mark)
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

        /// <summary>
        /// The model's materials in the toon look her pistol wears in a run (<see cref="WeaponStageValues.ToonOnStage"/>): copies on
        /// her toon shader with the same base map and colour, the look's values as her kit has them (<see cref="ToonLook.Push"/>:
        /// its teal glows), made here and freed with the stage; without the shader the materials stay as they are.
        /// </summary>
        Material[] Toon(Material[] today)
        {
            Shader toon = Shader.Find(ToonLook.ShaderName);
            if (toon == null || today == null) return today;
            var made = new Material[today.Length];
            for (int i = 0; i < today.Length; i++)
            {
                Material t = today[i];
                if (t == null) continue;
                var m = new Material(toon) { name = "WeaponStage_Toon_" + t.name };
                if (t.HasProperty("_BaseMap"))
                {
                    m.SetTexture("_BaseMap", t.GetTexture("_BaseMap"));
                    m.SetTextureScale("_BaseMap", t.GetTextureScale("_BaseMap"));
                    m.SetTextureOffset("_BaseMap", t.GetTextureOffset("_BaseMap"));
                }
                if (t.HasProperty("_BaseColor")) m.SetColor("_BaseColor", t.GetColor("_BaseColor"));
                ToonLook.Push(m, true, ToonLookValues.Live);
                _made.Add(m);
                made[i] = m;
            }
            return made;
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
            _mount[i].localPosition = new Vector3(w.At.X * _half.x, w.At.Y * _half.y, w.At.Z * _half.z) + _shift;
            Vector3 f = new Vector3(w.Forward.X, w.Forward.Y, w.Forward.Z), u = new Vector3(w.Up.X, w.Up.Y, w.Up.Z);
            if (f.sqrMagnitude < 1e-8f) f = Vector3.right;
            f.Normalize();
            u = Vector3.ProjectOnPlane(u, f);
            if (u.sqrMagnitude < 1e-8f) u = Vector3.up;
            _mount[i].localRotation = Quaternion.LookRotation(Vector3.Cross(f, u.normalized), u.normalized); // its +X along Forward, +Y along Up
        }

        /// <summary>How far past the muzzle a barrel part sticks out (in the stage's units; its shapes' own lengths below, or the models' while <see cref="WeaponStageValues.ModelledParts"/> is on).</summary>
        static float BarrelReach(string id) =>
            WeaponStageValues.ModelledParts
                ? (id == WeaponAttachments.LongBarrel ? WeaponStageValues.LongBarrelReach : id == WeaponAttachments.BullBarrel ? WeaponStageValues.BullBarrelReach : id == WeaponAttachments.Compensator ? WeaponStageValues.CompensatorReach : 0f)
                : (id == WeaponAttachments.LongBarrel ? LongBarrelLength : id == WeaponAttachments.BullBarrel ? 0.115f : id == WeaponAttachments.Compensator ? 0.125f : 0f);

        /// <summary>The model and the mounts (the parts with them) moved back by the shown barrel's share (<see cref="Shift"/>).</summary>
        void Reframe()
        {
            Vector3 shift = Vector3.left * (BarrelReach(_shown[(int)WeaponSlot.Barrel]) * WeaponStageValues.FrameShiftShare);
            if (shift == _shift) return;
            _shift = shift;
            if (_laid != null) _laid.localPosition = _laidAt + _shift;
            for (int s = 0; s < 3; s++)
                if (_mount[s] != null) PlaceMount((WeaponSlot)s);
            _dirty = true;
        }

        /// <summary>The mounts and the parts again from the values (a test or the producer changed a mount).</summary>
        public void Remount()
        {
            if (!_built) return;
            Reframe(); // the barrel's reach follows WeaponStageValues.ModelledParts (work order 150)
            for (int s = 0; s < 3; s++)
            {
                PlaceMount((WeaponSlot)s);
                MakePart((WeaponSlot)s);
            }
            _dirty = true;
        }

        // ---------------- The parts' shapes ----------------

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
            Material body = g ? _ghostBody : _body, trim = g ? _ghostBody : _trim, accent = g ? _ghostAccent : _accent, dot = g ? _ghostDot : _dot;
            if (WeaponStageValues.ModelledParts && ModelledPart(p, i, id, g)) return; // work order 150: its model; without one, its shape below
            float spacing = WeaponStageValues.GripBandSpacing * WeaponStageValues.ModelLength;
            switch (id)
            {
                // The sights, standing on the barrel's top (their +X the barrel's way, +Y up).
                case WeaponAttachments.LaserSight: // a short tube with a red dot at its front, on a small bronze block, a teal collar
                    Box(p, trim, new Vector3(0f, 0.012f, 0f), new Vector3(0.1f, 0.024f, 0.036f));
                    Tube(p, body, new Vector3(0f, 0.05f, 0f), 0.16f, 0.026f);
                    Ring(p, accent, new Vector3(-0.055f, 0.05f, 0f), 0.026f);
                    Ball(p, dot, new Vector3(0.082f, 0.05f, 0f), 0.034f);
                    break;
                case WeaponAttachments.GhostRing: // a bronze ring on a post, a teal bead on its top
                    Box(p, body, new Vector3(0f, 0.028f, 0f), new Vector3(0.03f, 0.056f, 0.03f));
                    Ring(p, trim, new Vector3(0f, 0.1f, 0f), 0.044f);
                    Ball(p, accent, new Vector3(0f, 0.148f, 0f), 0.016f);
                    break;
                case WeaponAttachments.ReflexSight: // a small window on a base: two bronze posts, a bar, a teal pane
                    Box(p, body, new Vector3(0f, 0.01f, 0f), new Vector3(0.11f, 0.02f, 0.056f));
                    Box(p, trim, new Vector3(0.025f, 0.055f, 0.027f), new Vector3(0.022f, 0.074f, 0.008f));
                    Box(p, trim, new Vector3(0.025f, 0.055f, -0.027f), new Vector3(0.022f, 0.074f, 0.008f));
                    Box(p, trim, new Vector3(0.025f, 0.094f, 0f), new Vector3(0.022f, 0.01f, 0.062f));
                    Box(p, accent, new Vector3(0.025f, 0.054f, 0f), new Vector3(0.006f, 0.068f, 0.046f));
                    break;
                // The barrels, out of the muzzle (+X out of it).
                case WeaponAttachments.BullBarrel: // a thick sleeve over the muzzle, a bronze band at its back, a teal band at its mouth
                    Tube(p, body, new Vector3(0.04f, 0f, 0f), 0.15f, 0.07f);
                    Ring(p, trim, new Vector3(0.006f, 0f, 0f), 0.068f); // a collar on the muzzle's face
                    Ring(p, accent, new Vector3(0.112f, 0f, 0f), 0.066f);
                    break;
                case WeaponAttachments.LongBarrel: // a long thin barrel, a bronze collar at the muzzle, a teal band at its end
                    Tube(p, body, new Vector3(LongBarrelLength * 0.5f, 0f, 0f), LongBarrelLength, 0.036f);
                    Ring(p, trim, new Vector3(0.012f, 0f, 0f), 0.04f);
                    Ring(p, accent, new Vector3(LongBarrelLength - 0.005f, 0f, 0f), 0.034f);
                    break;
                case WeaponAttachments.Compensator: // a block on the muzzle a little wider than the barrel, two teal slits on its top
                    Box(p, body, new Vector3(0.065f, 0f, 0f), new Vector3(0.12f, 0.14f, 0.12f));
                    Box(p, accent, new Vector3(0.045f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f));
                    Box(p, accent, new Vector3(0.088f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f));
                    break;
                // The grips, on the handle (+X down the handle, +Y toward its front).
                case WeaponAttachments.RunnersGrip: // three bands close round the handle, bronze, the middle one teal
                    Band(p, trim, 0, -spacing);
                    Band(p, accent, 1, 0f);
                    Band(p, trim, 2, spacing);
                    break;
                case WeaponAttachments.WeightedGrip: // a heavy block under the handle's foot, a teal plate on each side
                {
                    Transform f = Foot(p);
                    float fd = FootDeep, ft = FootThick;
                    Box(f, body, new Vector3(0f, -0.024f, 0f), new Vector3(fd * 1.04f, 0.05f, ft * 1.16f));
                    Box(f, accent, new Vector3(0f, -0.024f, -ft * 0.58f - 0.003f), new Vector3(fd * 0.7f, 0.026f, 0.006f));
                    Box(f, accent, new Vector3(0f, -0.024f, ft * 0.58f + 0.003f), new Vector3(fd * 0.7f, 0.026f, 0.006f));
                    break;
                }
                case WeaponAttachments.QuickDrawGrip: // a flared bronze well round the handle's foot, a thin teal stripe up its back
                {
                    Transform f = Foot(p);
                    float fd = FootDeep, ft = FootThick;
                    Box(f, trim, new Vector3(0f, 0.012f, 0f), new Vector3(fd * 1.12f, 0.03f, ft * 1.22f));
                    float back = HandleDeep * 0.5f - 0.008f, stripe = 2.2f * spacing; // up the back of the grip's panels (below them the foot stands straight); a little in from the depth's extreme, where the rounded back's own surface is (it stood off at + 0.004 in the pictures)
                    Box(p, accent, new Vector3(0f, -back, 0f), new Vector3(stripe, 0.008f, 0.018f));
                    break;
                }
                default: // a part this drawing does not know: a plain block, so it still shows
                    Box(p, body, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f));
                    break;
            }
        }

        /// <summary>The long barrel's length past the muzzle (stage units): 2026-11-26 0.26, not the first 0.34, so the pistol with it fits its nearer frame; still twice the bull barrel's sleeve.</summary>
        const float LongBarrelLength = 0.26f;

        /// <summary>The handle's depth (front to back, square to it) on the stage, and its foot's depth and thickness, from the values.</summary>
        float HandleDeep => WeaponStageValues.HandleDepth * WeaponStageValues.ModelLength;
        float FootDeep => WeaponStageValues.FootDepth * WeaponStageValues.ModelLength;
        float FootThick => Mathf.Max(0.04f, _half.z * 2f * WeaponStageValues.FootThickness);

        /// <summary>
        /// A frame at the handle's foot (<see cref="WeaponStageValues.FootAt"/>: the middle of its bottom face), level with the model
        /// (+X toward the muzzle, +Y up), under a grip part: the handle leans back but its foot, the magazine's bronze base, is level.
        /// </summary>
        Transform Foot(Transform part)
        {
            Transform mount = part.parent;
            Vector3 at = new Vector3(WeaponStageValues.FootAt.X * _half.x, WeaponStageValues.FootAt.Y * _half.y, WeaponStageValues.FootAt.Z * _half.z) + _shift;
            Transform f = Child("Foot", part);
            Quaternion undo = Quaternion.Inverse(mount.localRotation);
            f.localPosition = undo * (at - mount.localPosition);
            f.localRotation = undo;
            return f;
        }

        void Box(Transform parent, Material m, Vector3 at, Vector3 size) => Shape(parent, Cube, m, at, Quaternion.identity, size);
        /// <summary>A tube along the part's +X, this long and this thick (radius).</summary>
        void Tube(Transform parent, Material m, Vector3 at, float length, float radius) =>
            Shape(parent, Cylinder, m, at, Quaternion.Euler(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        /// <summary>Band <paramref name="k"/> (0 the top) round the handle (its hole along the part's +X, down the handle) this far down from the mount: <see cref="BandMesh"/>.</summary>
        void Band(Transform parent, Material m, int k, float along) => Shape(parent, BandMesh(k), m, new Vector3(along, 0f, 0f), Quaternion.identity, Vector3.one);
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

        /// <summary>
        /// The grip's band: a flat strap (<see cref="WeaponStageValues.GripBandWidth"/> wide along the handle,
        /// <see cref="WeaponStageValues.GripBandTube"/> thick either side of its middle) along a rounded rectangle that goes round the
        /// handle's section (its depth along the part's +Y, its thickness along +Z) with a hair's gap
        /// (<see cref="WeaponStageValues.GripBandGap"/>), so it sits on the handle instead of standing off it as a coil (the last
        /// model's elliptic rings did, and a round wire still read as one in the pictures of 2026-11-26) or cutting into its corners. Band <paramref name="k"/> (0 the top) takes the handle's
        /// thickness where it sits (<see cref="WeaponStageValues.GripBandThickness"/>: the grip flares toward the slide). Made once
        /// each for the model's handle, in code.
        /// </summary>
        Mesh BandMesh(int k)
        {
            if (_bands[k] != null) return _bands[k];
            float len = WeaponStageValues.ModelLength;
            float tube = WeaponStageValues.GripBandTube * len, gap = WeaponStageValues.GripBandGap * len, halfWidth = WeaponStageValues.GripBandWidth * len * 0.5f;
            float thick = Mathf.Max(0.04f, _half.z * 2f * WeaponStageValues.GripBandThickness[k]);
            float a = HandleDeep * 0.5f + gap + tube, b = thick * 0.5f + gap + tube; // the tube's middle line: half its sides
            float corner = Mathf.Min(a, b) * WeaponStageValues.GripBandCorner;
            const int PerCorner = 10, Around = 16;
            int path = PerCorner * 4;
            var points = new Vector2[path];
            var outward = new Vector2[path];
            for (int c = 0; c < 4; c++)
            {
                // The corners' middles at (+-(a - r), +-(b - r)), in turn round the loop; each sweeps a quarter.
                float cy = (c == 0 || c == 3 ? 1f : -1f) * (a - corner), cz = (c < 2 ? 1f : -1f) * (b - corner);
                for (int j = 0; j < PerCorner; j++)
                {
                    float u = (c + j / (float)(PerCorner - 1)) * Mathf.PI * 0.5f;
                    var n = new Vector2(Mathf.Cos(u), Mathf.Sin(u));
                    points[c * PerCorner + j] = new Vector2(cy, cz) + n * corner;
                    outward[c * PerCorner + j] = n;
                }
            }
            var vertices = new Vector3[path * Around];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[path * Around * 6];
            for (int s = 0; s < path; s++)
                for (int q = 0; q < Around; q++)
                {
                    float v = q * Mathf.PI * 2f / Around;
                    // Round its flat section (an ellipse halfWidth along the handle, tube across): the point, and its normal.
                    float sv = Mathf.Sin(v), cv = Mathf.Cos(v);
                    vertices[s * Around + q] = new Vector3(sv * halfWidth, points[s].x + outward[s].x * cv * tube, points[s].y + outward[s].y * cv * tube);
                    normals[s * Around + q] = new Vector3(sv / halfWidth, outward[s].x * cv / tube, outward[s].y * cv / tube).normalized;
                }
            int t = 0;
            for (int s = 0; s < path; s++)
                for (int q = 0; q < Around; q++)
                {
                    int s1 = (s + 1) % path, q1 = (q + 1) % Around;
                    int i00 = s * Around + q, i01 = s * Around + q1, i10 = s1 * Around + q, i11 = s1 * Around + q1;
                    triangles[t++] = i00; triangles[t++] = i01; triangles[t++] = i10; // as the ring's: (here, round the tube, along)
                    triangles[t++] = i10; triangles[t++] = i01; triangles[t++] = i11;
                }
            var band = new Mesh { name = "WeaponStageBand" + k, vertices = vertices, normals = normals, triangles = triangles };
            _bands[k] = band;
            _made.Add(band);
            return band;
        }

        // ---------------- The parts as models (work order 150) ----------------

        /// <summary>
        /// The models' three shared materials (the names the Blender files give them: Gunmetal, Bronze, Teal), matched by eye to
        /// the pistol's painted map under the stage's three lights (sRGB; the map's medians, 2026-11-26: gunmetal about 0.21, bronze
        /// 0.58/0.47/0.35, teal 0.42/0.89/0.92). Their worn-edge layer (<c>Wear</c>) is not shown: URP Lit has no vertex colour.
        /// Set from the pictures of 2026-11-27: at the map's bronze, 0.6 metallic and half rough, the bronze read as flat tan plastic
        /// beside the pistol's (lighter in its shade, no highlight), and the gunmetal at 0.30 as flat grey beside its dark steel
        /// (81 against 45 of 255 on the same side): both darker now, more metal and glossier, so they shade and catch the key light
        /// as the pistol's painted metal does. The teal at the map's colour came out a sixth brighter than the pistol's teal lines
        /// (193 against 168 of 255 in green, its highlights at 255): darker by 0.85 and less glossy.
        /// </summary>
        static readonly Color ModelGunmetal = new Color(0.24f, 0.235f, 0.25f), ModelBronze = new Color(0.55f, 0.42f, 0.28f), ModelTeal = new Color(0.354f, 0.753f, 0.78f);
        const float ModelGunmetalMetallic = 0.55f, ModelBronzeMetallic = 0.75f, ModelSmoothness = 0.6f, ModelTealSmoothness = 0.35f;
        /// <summary>
        /// The laser's lens (the Blender file's LaserRed): a deep red lit from inside, with no light highlight and no reflection of
        /// its own (a grey shine on it read as pink). The code-made dot's colour and glow ran to pink on the model's lens in the
        /// first pictures, and at a glow of 0.9 red with a gloss its brightest pixels still came out salmon (255, 101, 124 of 255):
        /// the glow lower and the shine off, so its red is red.
        /// </summary>
        static readonly Color ModelDot = new Color(0.45f, 0.02f, 0.04f), ModelDotGlow = new Color(0.75f, 0.02f, 0.05f);
        /// <summary>The reflex sight's pane (the Blender file's Glass: a pale teal, about a third opaque) and how glossy it is.</summary>
        static readonly Color ModelGlass = new Color(0.55f, 0.85f, 0.9f, 0.3f);
        const float ModelGlassSmoothness = 0.9f;

        Material _modelGunmetal, _modelBronze, _modelTeal, _modelGlass, _modelDot;
        readonly HashSet<string> _partModelMissing = new HashSet<string>(); // ids already warned of (one warning a part)

        /// <summary>Whether the part on a slot is drawn as its model (false: its shape made in code, or none). For tests.</summary>
        public bool PartModelled(WeaponSlot slot) => _part[(int)slot] != null && _part[(int)slot].Find(PartModelChild) != null;
        /// <summary>The name of the child of a part that holds its model.</summary>
        public const string PartModelChild = "Model";

        /// <summary>
        /// The part's model (<see cref="WeaponStageValues.PartModelPath"/>) under <paramref name="p"/>, in the pistol's own frame:
        /// the models were made in Blender in the pistol's own units and axes, each with its origin at its mount (the sights' on the
        /// top rib, the barrels' in the muzzle's face on the bore, the grips' in the middle of the handle's panels: the stage's
        /// three mount points are the same places in the model's box), so each is laid as the pistol is (<see cref="WeaponStageValues.ModelTurn"/>
        /// and its file's own turn), at the pistol's scale on the stage, with its origin on the mount and no turn of the mount's own
        /// (the grip's lean is in its shape). Its materials by name: the three shared ones, the laser's red dot, the reflex's pane;
        /// a ghost (a part previewed and not owned) in the ghost materials. False, with one warning a part, when there is no model.
        /// </summary>
        bool ModelledPart(Transform p, int slot, string id, bool ghost)
        {
            string path = WeaponStageValues.PartModelPath(id);
            if (path == null) return false;
            var model = (PartLoadHook != null ? PartLoadHook(id) : Resources.Load<GameObject>(path)) as GameObject;
            bool any = false;
            MeshFilter[] filters = model != null ? model.GetComponentsInChildren<MeshFilter>(true) : null;
            if (filters != null)
                foreach (MeshFilter f in filters)
                    any |= f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null;
            if (!any)
            {
                if (_partModelMissing.Add(id)) Debug.LogWarning("[WeaponStage] no model at Resources/" + path + ": the " + id + " is drawn as its shape made in code");
                return false;
            }
            MakeModelMaterials();
            Transform holder = Child(PartModelChild, p);
            holder.localRotation = Quaternion.Inverse(_mount[slot].localRotation); // the pistol's frame, not the mount's
            holder.localScale = _laid.localScale;
            Matrix4x4 lay = Matrix4x4.Rotate(Quaternion.Euler(WeaponStageValues.ModelTurn.X, WeaponStageValues.ModelTurn.Y, WeaponStageValues.ModelTurn.Z));
            foreach (MeshFilter f in filters)
            {
                MeshRenderer source = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null || source == null) continue;
                Matrix4x4 m = lay * f.transform.localToWorldMatrix;
                Transform t = Child(f.sharedMesh.name, holder);
                t.localPosition = m.GetColumn(3);
                Vector3 fwd = m.GetColumn(2), up = m.GetColumn(1);
                if (fwd.sqrMagnitude > 1e-12f && up.sqrMagnitude > 1e-12f) t.localRotation = Quaternion.LookRotation(fwd, up);
                t.localScale = m.lossyScale;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>();
                Material[] from = source.sharedMaterials;
                var mats = new Material[Mathf.Max(1, from.Length)];
                for (int k = 0; k < mats.Length; k++) mats[k] = ModelMaterial(k < from.Length ? from[k] : null, ghost);
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return true;
        }

        /// <summary>The stage's material for a model's material, by the name the Blender file gives it (anything else: the gunmetal).</summary>
        Material ModelMaterial(Material from, bool ghost)
        {
            string n = from != null ? from.name : "";
            if (n.StartsWith("Bronze", StringComparison.Ordinal)) return ghost ? _ghostBody : _modelBronze;
            if (n.StartsWith("Teal", StringComparison.Ordinal)) return ghost ? _ghostAccent : _modelTeal;
            if (n.StartsWith("LaserRed", StringComparison.Ordinal)) return ghost ? _ghostDot : _modelDot;
            if (n.StartsWith("Glass", StringComparison.Ordinal)) return ghost ? _ghostBody : _modelGlass;
            return ghost ? _ghostBody : _modelGunmetal;
        }

        /// <summary>The models' materials, made the first time a model is shown (freed with the stage).</summary>
        void MakeModelMaterials()
        {
            if (_modelGunmetal != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            _modelGunmetal = Lit(lit, "ModelGunmetal", ModelGunmetal, 0f, false, ModelGunmetalMetallic);
            _modelBronze = Lit(lit, "ModelBronze", ModelBronze, 0f, false, ModelBronzeMetallic);
            _modelTeal = Lit(lit, "ModelTeal", ModelTeal, WeaponStageValues.PartTealGlow, false);
            _modelGlass = Lit(lit, "ModelGlass", ModelGlass, 0f, true);
            _modelDot = Lit(lit, "ModelDot", ModelDot, 1f, false);
            if (_modelDot.HasProperty("_EmissionColor")) _modelDot.SetColor("_EmissionColor", ModelDotGlow);
            if (_modelDot.HasProperty("_SpecularHighlights"))
            {
                _modelDot.SetFloat("_SpecularHighlights", 0f);
                _modelDot.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            if (_modelDot.HasProperty("_EnvironmentReflections"))
            {
                _modelDot.SetFloat("_EnvironmentReflections", 0f);
                _modelDot.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            foreach (Material m in new[] { _modelGunmetal, _modelBronze })
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ModelSmoothness);
            if (_modelTeal.HasProperty("_Smoothness")) _modelTeal.SetFloat("_Smoothness", ModelTealSmoothness);
            if (_modelGlass.HasProperty("_Smoothness")) _modelGlass.SetFloat("_Smoothness", ModelGlassSmoothness);
            if (_modelGlass.HasProperty("_BaseColor")) _modelGlass.SetColor("_BaseColor", ModelGlass); // its own alpha, not the ghost's
            _modelGlass.color = ModelGlass;
        }

        void MakeMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            _body = Lit(lit, "Body", PartBody, 0f, false, PartMetallic);
            _trim = Lit(lit, "Trim", PartTrim, 0f, false, TrimMetallic);
            _accent = Lit(lit, "Accent", PartAccent, AccentGlow, false);
            _dot = Lit(lit, "Dot", PartDot, DotGlow, false);
            _ghostBody = Lit(lit, "GhostBody", GhostBody, 0f, true, PartMetallic);
            _ghostAccent = Lit(lit, "GhostAccent", PartAccent, AccentGlow, true);
            _ghostDot = Lit(lit, "GhostDot", PartDot, DotGlow, true);
        }

        Material Lit(Shader shader, string name, Color color, float glow, bool ghost, float metallic = 0f)
        {
            var m = new Material(shader) { name = "WeaponStage_" + name };
            if (ghost) color.a = GhostAlpha;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", glow > 0f ? 0f : metallic);
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
            SphericalHarmonicsL2 ambient = RenderSettings.ambientProbe; // a room's mood may have left it dark or tinted
            _turnedOff.Clear();
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientProbe = _stageAmbient;
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
                RenderSettings.ambientProbe = ambient;
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
