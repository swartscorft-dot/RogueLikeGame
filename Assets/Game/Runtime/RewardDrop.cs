using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// A reward lying on the ground (work order 042; Kristian's words of 2026-11-20: "a drop on the ground that one picks up
    /// with E"): what an elite room or a floor's exit room gave her, until she picks it up (<see cref="GeneratedLevels.PickUpDrop"/>).
    /// Made from code, in the look of the shot effects (vertex colours that glow under the bloom), in the colour of what it
    /// is: a gem that bursts up from where the last enemy fell, arcs and bounces to where it lands and lies still after
    /// <see cref="RewardDropValues.SettleSeconds"/> (<see cref="RewardDropRules.Pose"/>), then floats, turns and pulses; a
    /// tall thin beam and a ring on the ground where it lies, and a light of its colour, so it is seen from across the room.
    /// It rings once as it comes to rest (the chest's chime). While she can pick it up a prompt shows over it, as over a
    /// chest; while the floor's reward lies, a line at the top of the screen says the way on waits for it. It goes with
    /// its floor. A picture and a place only: what it gives is the run's (<see cref="GeneratedLevels"/>).
    /// </summary>
    public sealed class RewardDrop : MonoBehaviour
    {
        const float GemSize = 0.32f;     // m from the middle to a tip
        const float BeamHeight = 3.4f;   // m
        const float BeamWidth = 0.16f;   // m at its foot; it narrows to a point
        const float RingInner = 0.55f;   // m
        const float RingOuter = 0.72f;
        const float LightRange = 5f;     // m
        const float LightIntensity = 2f;
        const float Glow = 3f;
        const float TurnPerSecond = 90f; // degrees

        GeneratedLevels _owner;
        RewardDropValues _values;
        float _startedAt;
        Transform _gem;
        GameObject _mark;   // the beam and the ring, where it lands
        Light _light;
        readonly List<Mesh> _meshes = new List<Mesh>();
        GUIStyle _promptStyle;
        GUIStyle _lineStyle;

        /// <summary>The choice it opens.</summary>
        public GeneratedLevels.ChoiceKind Kind { get; private set; }
        /// <summary>Its colour, as picked (before the glow).</summary>
        public Color Color { get; private set; }
        /// <summary>Where the last enemy fell (or the room's middle), on the ground; and where it lands.</summary>
        public Vector3 Fell { get; private set; }
        public Vector3 Landing { get; private set; }
        /// <summary>True once it lies still: it can be picked up.</summary>
        public bool Settled => Pose.Settled;
        /// <summary>True once it has rung as it came to rest.</summary>
        public bool Rang { get; private set; }
        /// <summary>Where it is drawn now: in the air, or floating over where it lies.</summary>
        public DropPose Pose => RewardDropRules.Pose(Time.time - _startedAt, _values);
        /// <summary>What it gives, in a word for the prompt.</summary>
        public string What => Kind == GeneratedLevels.ChoiceKind.SkillUpgrade ? "skill upgrade"
                            : Kind == GeneratedLevels.ChoiceKind.FocusChoice ? "focus choice" : "new skill";

        internal object Owed { get; private set; }

        internal static RewardDrop Make(GeneratedLevels owner, object owed, GeneratedLevels.ChoiceKind kind, Color color,
                                        Vector3 fell, Vector3 landing, Transform parent, Material material, RewardDropValues values)
        {
            var go = new GameObject("RewardDrop_" + kind);
            go.transform.SetParent(parent, true);
            go.transform.position = landing;
            RewardDrop drop = go.AddComponent<RewardDrop>();
            drop._owner = owner;
            drop.Owed = owed;
            drop.Kind = kind;
            drop.Color = color;
            drop.Fell = fell;
            drop.Landing = landing;
            drop._values = values;
            drop._startedAt = Time.time;
            drop.Build(material);
            drop.Place();
            return drop;
        }

        void Build(Material material)
        {
            Color drawn = Drawn(Color, Glow);
            Color core = Drawn(Color.Lerp(Color, Color.white, 0.6f), Glow);

            var gem = new GameObject("Gem");
            gem.transform.SetParent(transform, false);
            _gem = gem.transform;
            AddRenderer(gem, GemMesh(drawn, core), material);
            _light = gem.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Color;
            _light.range = LightRange;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;

            _mark = new GameObject("BeamAndRing");
            _mark.transform.SetParent(transform, false);
            AddRenderer(_mark, MarkMesh(drawn, core), material);
            _mark.SetActive(false);
        }

        void AddRenderer(GameObject go, Mesh mesh, Material material)
        {
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Update()
        {
            Place();
            if (!Rang && Settled)
            {
                Rang = true;
                PlaceholderSounds.Current?.AskFor(PlaceholderSound.ChestOpens);
            }
        }

        /// <summary>The gem where its flight has it now; the beam, the ring and the light once it has landed.</summary>
        void Place()
        {
            DropPose pose = Pose;
            Vector3 ground = Vector3.Lerp(Fell, Landing, pose.Along);
            _gem.position = new Vector3(ground.x, Landing.y + pose.Height, ground.z);
            _gem.localScale = Vector3.one * pose.Scale;
            _gem.rotation = Quaternion.Euler(0f, (Time.time - _startedAt) * TurnPerSecond, 0f);
            bool landed = pose.Along >= 1f;
            if (_mark.activeSelf != landed) _mark.SetActive(landed);
            if (landed) _mark.transform.localScale = new Vector3(pose.Scale, 1f, pose.Scale);
            _light.intensity = landed ? LightIntensity * pose.Scale : LightIntensity * 0.5f;
        }

        void OnGUI()
        {
            if (_owner == null || Event.current.type != EventType.Repaint) return;
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;

            if (_owner.DropPrompt == this && Camera.main is Camera cam)
            {
                Vector3 screen = cam.WorldToScreenPoint(Landing + Vector3.up * 1.6f);
                if (screen.z > 0f)
                {
                    if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                    float px = screen.x / scale, py = (Screen.height - screen.y) / scale;
                    string key = PromptKey();
                    if (_prompt == null || key != _promptKey)
                    {
                        _promptKey = key;
                        _prompt = key + "  pick up " + What;
                    }
                    GUI.Box(new Rect(px - 80f, py - 14f, 160f, 26f), _prompt, _promptStyle);
                }
            }
            // The floor's reward: the way on waits for it (one line, drawn by the floor's drop).
            string line = Kind != GeneratedLevels.ChoiceKind.SkillUpgrade ? _owner.WaitingLine : "";
            if (line.Length > 0)
            {
                if (_lineStyle == null) _lineStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect((viewWidth - 480f) * 0.5f, 64f, 480f, 26f), line, _lineStyle);
            }
            GUI.matrix = matrixBefore;
        }

        string _prompt;
        string _promptKey;
        PlayerInputDriver _driver;

        /// <summary>The interact key as bound now, as the chest's prompt names it.</summary>
        string PromptKey()
        {
            if (_driver == null)
            {
                PlayerCharacter player = FindAnyObjectByType<PlayerCharacter>();
                _driver = player != null ? player.GetComponent<PlayerInputDriver>() : null;
            }
            if (_driver == null) return "E";
            UnityEngine.InputSystem.Key key = _driver.ActiveBindings.Interact;
            if (key != _boundKey || _boundKeyText == null)
            {
                _boundKey = key;
                _boundKeyText = key.ToString();
            }
            return _boundKeyText;
        }

        UnityEngine.InputSystem.Key _boundKey;
        string _boundKeyText;

        void OnDestroy()
        {
            foreach (Mesh m in _meshes)
                if (m != null) Destroy(m);
        }

        // ---------------- The meshes ----------------

        /// <summary>A gem: two four-sided pyramids base to base, the upper half lighter, made of vertex colours.</summary>
        static Mesh GemMesh(Color rim, Color core)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            Vector3 top = Vector3.up * GemSize * 1.3f, bottom = Vector3.down * GemSize;
            for (int i = 0; i < 4; i++)
            {
                float a0 = i * Mathf.PI * 0.5f, a1 = (i + 1) * Mathf.PI * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * GemSize;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * GemSize;
                // Both windings: drawn from either side whatever the material culls.
                Triangle(verts, colors, tris, top, p1, p0, core);
                Triangle(verts, colors, tris, top, p0, p1, core);
                Triangle(verts, colors, tris, bottom, p0, p1, rim);
                Triangle(verts, colors, tris, bottom, p1, p0, rim);
            }
            return Made("RewardDropGem", verts, colors, tris);
        }

        /// <summary>The beam (two crossed tapering blades, drawn from both sides) and the ring on the ground.</summary>
        static Mesh MarkMesh(Color rim, Color core)
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int blade = 0; blade < 2; blade++)
            {
                Vector3 side = (blade == 0 ? Vector3.right : Vector3.forward) * BeamWidth * 0.5f;
                Vector3 tip = Vector3.up * BeamHeight;
                Triangle(verts, colors, tris, -side, tip, side, core);
                Triangle(verts, colors, tris, side, tip, -side, core);
            }
            const int segments = 28;
            const float y = 0.03f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 i0 = d0 * RingInner + Vector3.up * y, i1 = d1 * RingInner + Vector3.up * y;
                Vector3 o0 = d0 * RingOuter + Vector3.up * y, o1 = d1 * RingOuter + Vector3.up * y;
                Triangle(verts, colors, tris, i0, o1, o0, rim);
                Triangle(verts, colors, tris, i0, i1, o1, rim);
            }
            return Made("RewardDropMark", verts, colors, tris);
        }

        static void Triangle(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int n = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            tris.Add(n);
            tris.Add(n + 1);
            tris.Add(n + 2);
        }

        static Mesh Made(string name, List<Vector3> verts, List<Color> colors, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The vertex colour for a picked colour, made brighter by its glow, as the shot effects do it.</summary>
        static Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            return new Color(c.r * glow, c.g * glow, c.b * glow, 1f);
        }
    }
}
