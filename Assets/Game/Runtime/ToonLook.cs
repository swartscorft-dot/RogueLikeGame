using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Her model in the toon look, a trial (work order 115; Kristian, 2026-11-24, of the key art drawn of her: "Jeg liker også
    /// denne tegnestilen veldig godt. Klarer vi å få modellen i spillet til å ha mer av det?"). On her (her character adds it in
    /// its Awake); it touches only the renderers of her model, the object her animator is on and what hangs under it (her body,
    /// her pistols, the shotgun on her back): each of their materials gets a copy on the project's own shader
    /// (<see cref="ShaderName"/>, <c>Assets/Game/Shaders/ToonHer.shader</c>) with that material's own texture and colour and the
    /// toon values (<see cref="ToonLookValues.Live"/>): two tones, the edge light the room's own lights give her (the fixed teal
    /// edge is off since the follow-up), the teal lights (on her weapons), the ink line.
    ///
    /// <see cref="ToonLookValues.On"/> is read every frame (the pause menu's "Toon look (trial)" flips it): on, the copies are put
    /// on her renderers; off, her renderers get back the very material objects they had, which were never changed. Nothing is
    /// allocated while the switch stands. The copies are made the first time the look is put on and destroyed with her
    /// (<see cref="OnDestroy"/>); no mesh is made (the ink line is drawn from her own mesh by a second pass of the shader).
    /// Nothing else in the game changes what her model's renderers draw with, so the flashes, rims and glows around her (all
    /// drawn by objects of their own) are as they were.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToonLook : MonoBehaviour
    {
        /// <summary>The shader's name, as code finds it (kept in the build by the code-made shaders' list, work order 032's way).</summary>
        public const string ShaderName = "Roguelike/Toon Her";
        /// <summary>What every material made here is called before the name of the material it copies.</summary>
        public const string MaterialPrefix = "ToonLook_";

        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_LitThreshold = Shader.PropertyToID("_LitThreshold");
        static readonly int s_LitSoftness = Shader.PropertyToID("_LitSoftness");
        static readonly int s_LitLevel = Shader.PropertyToID("_LitLevel");
        static readonly int s_ShadowTint = Shader.PropertyToID("_ShadowTint");
        static readonly int s_ShadowEdge = Shader.PropertyToID("_ShadowEdge");
        static readonly int s_AmbientFlat = Shader.PropertyToID("_AmbientFlat");
        static readonly int s_AmbientLevel = Shader.PropertyToID("_AmbientLevel");
        static readonly int s_DetailCalm = Shader.PropertyToID("_DetailCalm");
        static readonly int s_RimColor = Shader.PropertyToID("_RimColor");
        static readonly int s_RimStrength = Shader.PropertyToID("_RimStrength");
        static readonly int s_RimStart = Shader.PropertyToID("_RimStart");
        static readonly int s_RimSoftness = Shader.PropertyToID("_RimSoftness");
        static readonly int s_RimAway = Shader.PropertyToID("_RimAway");
        static readonly int s_RimUnder = Shader.PropertyToID("_RimUnder");
        static readonly int s_LampEdge = Shader.PropertyToID("_LampEdge");
        static readonly int s_LampEdgeStrength = Shader.PropertyToID("_LampEdgeStrength");
        static readonly int s_LampEdgeStart = Shader.PropertyToID("_LampEdgeStart");
        static readonly int s_LampEdgeSoftness = Shader.PropertyToID("_LampEdgeSoftness");
        static readonly int s_TealGlow = Shader.PropertyToID("_TealGlow");
        static readonly int s_TealStart = Shader.PropertyToID("_TealStart");
        static readonly int s_TealSoftness = Shader.PropertyToID("_TealSoftness");
        static readonly int s_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int s_OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int s_OutlineDepthPush = Shader.PropertyToID("_OutlineDepthPush");

        sealed class Target
        {
            public Renderer Renderer;
            public Material[] Before; // today's, exactly as they were on the renderer
            public Material[] Toon;   // the copies, made the first time the look goes on
            public bool Kit;          // a weapon on her, not her skinned body: its teal lights glow
        }

        readonly List<Target> _targets = new List<Target>();
        readonly List<Material> _made = new List<Material>();
        bool _found;
        bool _failed; // the shader is not there: she stays as she is, and it is not looked for again
        bool _applied;

        /// <summary>Every Play starts with the look on (the switch is not kept between starts; work order 115).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ToonLookValues.On = true;

        /// <summary>True while her model wears the toon materials.</summary>
        public bool Applied => _applied;
        /// <summary>The materials made here (destroyed with her); for tests.</summary>
        public IReadOnlyList<Material> Made => _made;
        /// <summary>How many of her renderers it looks after; for tests.</summary>
        public int TargetCount => _targets.Count;
        /// <summary>The renderer of target <paramref name="i"/>; for tests.</summary>
        public Renderer TargetRenderer(int i) => _targets[i].Renderer;
        /// <summary>Its materials as they were before the look (the ones it puts back); for tests.</summary>
        public IReadOnlyList<Material> TodaysOf(int i) => _targets[i].Before;
        /// <summary>Whether target <paramref name="i"/> is a weapon on her (its teal lights glow); for tests.</summary>
        public bool IsKit(int i) => _targets[i].Kit;

        void LateUpdate() => Tick();

        /// <summary>Follows the switch: puts the look on or takes it off when it has changed; else nothing (and nothing allocated).</summary>
        public void Tick()
        {
            bool want = ToonLookValues.On;
            if (want == _applied || (want && _failed)) return;
            if (want) PutOn();
            else TakeOff();
        }

        /// <summary>Writes the values (<see cref="ToonLookValues.Live"/>) into every material made here again, after a change to them.</summary>
        public void Refresh()
        {
            foreach (Target t in _targets)
                if (t.Toon != null)
                    foreach (Material m in t.Toon)
                        if (m != null) Push(m, t.Kit, ToonLookValues.Live);
        }

        void Find()
        {
            _found = true;
            Transform model = null;
            CharacterAnimation anim = GetComponentInChildren<CharacterAnimation>(true);
            if (anim != null) model = anim.Animator != null ? anim.Animator.transform : anim.transform;
            if (model == null)
            {
                Animator animator = GetComponentInChildren<Animator>(true);
                if (animator != null) model = animator.transform;
            }
            if (model == null) return;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                if (r.GetComponent<CombatFx>() != null) continue;
                Material[] before = r.sharedMaterials;
                if (before.Length == 0) continue;
                bool all = true;
                foreach (Material m in before) all &= m != null && IsTodaysLit(m.shader);
                if (!all) continue;
                _targets.Add(new Target { Renderer = r, Before = before, Kit = !(r is SkinnedMeshRenderer) });
            }
        }

        /// <summary>Her model's materials today: URP's Lit or Simple Lit (an effect's own material is neither).</summary>
        static bool IsTodaysLit(Shader s) =>
            s != null && (s.name == "Universal Render Pipeline/Lit" || s.name == PropFactory.ShaderName);

        void PutOn()
        {
            if (!_found) Find();
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
            {
                _failed = true;
                Debug.LogWarning("[ToonLook] the shader " + ShaderName + " is not in this build or not supported here: she keeps today's look");
                return;
            }
            foreach (Target t in _targets)
            {
                if (t.Renderer == null) continue;
                if (t.Toon == null)
                {
                    t.Toon = new Material[t.Before.Length];
                    for (int i = 0; i < t.Before.Length; i++) t.Toon[i] = Make(shader, t.Before[i], t.Kit);
                }
                t.Renderer.sharedMaterials = t.Toon;
            }
            _applied = true;
        }

        void TakeOff()
        {
            foreach (Target t in _targets)
                if (t.Renderer != null) t.Renderer.sharedMaterials = t.Before;
            _applied = false;
        }

        Material Make(Shader shader, Material today, bool kit)
        {
            var m = new Material(shader) { name = MaterialPrefix + today.name };
            if (today.HasProperty(s_BaseMap))
            {
                m.SetTexture(s_BaseMap, today.GetTexture(s_BaseMap));
                m.SetTextureScale(s_BaseMap, today.GetTextureScale(s_BaseMap));
                m.SetTextureOffset(s_BaseMap, today.GetTextureOffset(s_BaseMap));
            }
            if (today.HasProperty(s_BaseColor)) m.SetColor(s_BaseColor, today.GetColor(s_BaseColor));
            m.enableInstancing = today.enableInstancing;
            Push(m, kit, ToonLookValues.Live);
            _made.Add(m);
            return m;
        }

        static Color C(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        /// <summary>Writes the look's values into a material on its shader (her copies; work order 119: an enemy's own materials too, not a kit).</summary>
        internal static void Push(Material m, bool kit, ToonLookValues v)
        {
            m.SetFloat(s_LitThreshold, v.LitThreshold);
            m.SetFloat(s_LitSoftness, Mathf.Max(0.001f, v.LitSoftness));
            m.SetFloat(s_LitLevel, v.LitLevel);
            m.SetColor(s_ShadowTint, C(v.ShadowTint));
            m.SetFloat(s_ShadowEdge, v.ShadowEdge);
            m.SetFloat(s_AmbientFlat, v.AmbientFlat);
            m.SetFloat(s_AmbientLevel, v.AmbientLevel);
            m.SetFloat(s_DetailCalm, v.DetailCalm);
            m.SetColor(s_RimColor, C(v.RimColour));
            m.SetFloat(s_RimStrength, v.RimStrength);
            m.SetFloat(s_RimStart, v.RimStart);
            m.SetFloat(s_RimSoftness, Mathf.Max(0.001f, v.RimSoftness));
            m.SetFloat(s_RimAway, v.RimAway);
            m.SetFloat(s_RimUnder, Mathf.Max(0.01f, v.RimUnder));
            m.SetFloat(s_LampEdge, v.LampEdgeOn ? 1f : 0f); // the follow-up: the edge light from the room's own lights
            m.SetFloat(s_LampEdgeStrength, Mathf.Max(0f, v.LampEdgeStrength));
            m.SetFloat(s_LampEdgeStart, v.LampEdgeStart);
            m.SetFloat(s_LampEdgeSoftness, Mathf.Max(0.001f, v.LampEdgeSoftness));
            m.SetFloat(s_TealGlow, kit ? v.KitTealGlow : v.BodyTealGlow);
            m.SetFloat(s_TealStart, v.TealStart);
            m.SetFloat(s_TealSoftness, Mathf.Max(0.01f, v.TealSoftness));
            m.SetColor(s_OutlineColor, C(v.InkColour));
            m.SetFloat(s_OutlineWidth, Mathf.Max(0f, v.InkWidth));
            m.SetFloat(s_OutlineDepthPush, Mathf.Max(0f, v.InkDepthPush));
        }

        // Her renderers get back today's materials if they outlive this, and every material made here goes with her.
        void OnDestroy()
        {
            TakeOff();
            foreach (Material m in _made)
                if (m != null) Destroy(m);
            _made.Clear();
            foreach (Target t in _targets) t.Toon = null;
        }
    }
}
