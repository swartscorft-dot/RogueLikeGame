using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// One enemy in the toon look (work order 119; Kristian, 2026-11-25, after two runs with the look on her: "Samme på
    /// fiendene."): two tones, the ink line and the edge the room's lamps light, by her shader (<see cref="ToonLook.ShaderName"/>)
    /// and her values (<see cref="ToonLookValues.Live"/>; its teal glow is her body's, none). Every enemy that wears a model has
    /// one (its <see cref="EnemyLook"/> makes it and ticks it each frame): the swarmer, the brute, the shooter, the guard, the
    /// caster, the bomber, the warder, the lancer, the elites and the boss, in the floors, the practice room and the room lab.
    ///
    /// How: no material is made. The enemy's own materials, made for it at run time on URP Simple Lit (its model's meshes,
    /// <see cref="EnemyLook.MaterialPrefix"/>; the lancer's lance, <see cref="EnemyLancer.MaterialPrefix"/>; the warder's staff,
    /// <see cref="WarderBody.MaterialPrefix"/>; an elite's copy, <see cref="EnemyFactory.OwnMaterialPrefix"/>), are given her
    /// shader while the look is on and Simple Lit again when it goes off. They stay the same objects, so everything the game
    /// writes into them still lands on what is drawn: the daze's tint, the wind-ups and flashes painted on a body (the bomber's
    /// fuse, the boss's anger), an elite's tint, the warded tint, the caster's and the warder's glows; and a hit's flash, laid
    /// over the renderer, is over these as over today's. The shader declares the Simple Lit values that code sets, so each is
    /// kept while it wears hers, and when it gets Simple Lit back every Simple Lit value it had is put back as it was (but for
    /// what the game wrote meanwhile: <see cref="Today"/>), as the Editor's own check of a Simple Lit material changes some. The warder's light on its staff is left as it is (a glow, not a body), and so is everything not made for
    /// the enemy (an asset's material is never touched: in the Editor a change to one would be written to its file).
    ///
    /// <see cref="ToonLookValues.EnemiesShown"/> (the look on, and its enemies' switch on) is read every frame. Off from the
    /// start, nothing is looked at or changed: every material is today's. Nothing is allocated in a frame while the switch stands;
    /// the enemy's renderers are looked through again only when a part is added under it (the warder's staff, made as it wakes).
    /// </summary>
    public sealed class EnemyToonLook
    {
        static readonly List<Renderer> s_renderers = new List<Renderer>();
        static readonly List<Material> s_slots = new List<Material>();
        static Shader s_toon, s_today;
        static bool s_looked, s_failed;

        /// <summary>Every Play starts with the enemies in the look (the switch is not kept between starts; work order 119).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ToonLookValues.Enemies = true;
            s_toon = null;
            s_today = null;
            s_looked = false;
            s_failed = false;
        }

        readonly Transform _root;
        readonly List<Material> _materials = new List<Material>();
        readonly List<Today> _today = new List<Today>(); // beside each of _materials: its Simple Lit values while it wears the look
        int _children = -1;
        bool _shown;

        /// <summary>The look for the enemy whose object is <paramref name="root"/>; nothing happens until its first <see cref="Tick"/>.</summary>
        public EnemyToonLook(Transform root) => _root = root;

        /// <summary>True while its materials wear the toon shader.</summary>
        public bool Shown => _shown;
        /// <summary>The enemy's own materials it looks after (found the first time the look goes on); for tests.</summary>
        public IReadOnlyList<Material> Materials => _materials;

        /// <summary>
        /// Follows the switch: puts the look on its materials or takes it off when it has changed, and on a part added under the
        /// enemy since it last looked; else nothing (and nothing allocated).
        /// </summary>
        public void Tick()
        {
            if (_root == null) return;
            bool want = ToonLookValues.EnemiesShown;
            if (want)
            {
                if (!Shaders()) return; // her shader is not in this build: the enemies keep today's look
                int children = _root.childCount;
                if (children != _children)
                {
                    _children = children;
                    Find();
                    if (_shown) Show(true); // what was found now goes on too
                }
            }
            if (want != _shown) Show(want);
        }

        void Show(bool on)
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                Material m = _materials[i];
                if (m == null) continue;
                if (on)
                {
                    if (m.shader != s_today) continue;
                    _today[i] ??= new Today();
                    _today[i].Take(m);
                    m.shader = s_toon;
                    ToonLook.Push(m, false, ToonLookValues.Live);
                }
                else if (m.shader == s_toon)
                {
                    m.shader = s_today;
                    _today[i]?.Give(m);
                }
            }
            _shown = on;
        }

        /// <summary>
        /// A material's Simple Lit values as they were when it was given her shader, put back when it gets Simple Lit again. In the
        /// Editor, giving a material URP's Simple Lit runs URP's own check of it (its ShaderGUI.ValidateMaterial), which sets its
        /// keywords and render values from its properties as its Inspector would: from the enemies' _SpecularHighlights 0 it turns
        /// on _SPECULAR_COLOR (a grey shine today's enemies never had). So everything is put back as it was, but for what the game
        /// itself writes into it while the look is on (its colour, its texture, its glow: <see cref="Live"/>), which stays as the
        /// game left it. Made once per material, the first time the look goes on; nothing in a frame.
        /// </summary>
        sealed class Today
        {
            Vector4[] _values;
            Texture[] _textures;
            string[] _keywords;
            int _queue;
            MaterialGlobalIlluminationFlags _gi;

            public void Take(Material m)
            {
                int n = s_ids.Length;
                _values ??= new Vector4[n];
                _textures ??= new Texture[n];
                for (int i = 0; i < n; i++)
                {
                    if (Live(s_ids[i])) continue;
                    switch (s_types[i])
                    {
                        case ShaderPropertyType.Color: _values[i] = m.GetColor(s_ids[i]); break;
                        case ShaderPropertyType.Vector: _values[i] = m.GetVector(s_ids[i]); break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range: _values[i].x = m.GetFloat(s_ids[i]); break;
                        case ShaderPropertyType.Int: _values[i].x = m.GetInteger(s_ids[i]); break;
                        case ShaderPropertyType.Texture: _textures[i] = m.GetTexture(s_ids[i]); break;
                    }
                }
                _keywords = m.shaderKeywords;
                _queue = m.renderQueue;
                _gi = m.globalIlluminationFlags;
            }

            public void Give(Material m)
            {
                if (_values == null) return;
                for (int i = 0; i < s_ids.Length; i++)
                {
                    if (Live(s_ids[i])) continue;
                    switch (s_types[i])
                    {
                        case ShaderPropertyType.Color: m.SetColor(s_ids[i], _values[i]); break;
                        case ShaderPropertyType.Vector: m.SetVector(s_ids[i], _values[i]); break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range: m.SetFloat(s_ids[i], _values[i].x); break;
                        case ShaderPropertyType.Int: m.SetInteger(s_ids[i], (int)_values[i].x); break;
                        case ShaderPropertyType.Texture: m.SetTexture(s_ids[i], _textures[i]); break;
                    }
                }
                m.shaderKeywords = _keywords;
                if (m.renderQueue != _queue) m.renderQueue = _queue;
                m.globalIlluminationFlags = _gi;
            }

            /// <summary>What the game writes into an enemy's material while it may wear the look (the daze, the wind-ups, the tints, the glows): never put back.</summary>
            static bool Live(int id) => id == s_BaseColor || id == s_BaseMap || id == s_EmissionColor || id == s_EmissionMap;
        }

        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int s_EmissionMap = Shader.PropertyToID("_EmissionMap");
        static int[] s_ids = new int[0];
        static ShaderPropertyType[] s_types = new ShaderPropertyType[0];

        /// <summary>The enemy's own materials on Simple Lit (or on her shader already), on its meshes but the warder's light.</summary>
        void Find()
        {
            _root.GetComponentsInChildren(true, s_renderers);
            foreach (Renderer r in s_renderers)
            {
                if (r == null || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                if (r.TryGetComponent(out MarkPips _) || r.TryGetComponent(out CombatFx _) || r.GetComponentInParent<StaggerBarView>(true) != null) continue;
                if (string.Equals(r.gameObject.name, WarderBody.LightName, StringComparison.Ordinal)) continue; // the ward's light: a glow
                r.GetSharedMaterials(s_slots);
                foreach (Material m in s_slots)
                    if (m != null && !_materials.Contains(m) && Ours(m))
                    {
                        _materials.Add(m);
                        _today.Add(null);
                    }
            }
            s_renderers.Clear();
            s_slots.Clear();
        }

        /// <summary>A material made for the enemy at run time, on Simple Lit or on her shader: one this look may change.</summary>
        static bool Ours(Material m)
        {
            if (m.shader != s_today && m.shader != s_toon) return false;
            string name = m.name;
            return name.StartsWith(EnemyLook.MaterialPrefix, StringComparison.Ordinal)
                || name.StartsWith(EnemyFactory.OwnMaterialPrefix, StringComparison.Ordinal)
                || name.StartsWith(EnemyLancer.MaterialPrefix, StringComparison.Ordinal)
                || name.StartsWith(WarderBody.MaterialPrefix, StringComparison.Ordinal);
        }

        /// <summary>Her shader and Simple Lit, looked up once; false (and a warning, once) when hers is not there.</summary>
        static bool Shaders()
        {
            if (s_looked) return !s_failed;
            s_looked = true;
            s_toon = Shader.Find(ToonLook.ShaderName);
            s_today = Shader.Find(PropFactory.ShaderName);
            s_failed = s_toon == null || !s_toon.isSupported || s_today == null;
            if (!s_failed)
            {
                int n = s_today.GetPropertyCount();
                s_ids = new int[n];
                s_types = new ShaderPropertyType[n];
                for (int i = 0; i < n; i++)
                {
                    s_ids[i] = s_today.GetPropertyNameId(i);
                    s_types[i] = s_today.GetPropertyType(i);
                }
            }
            if (s_failed) Debug.LogWarning("[EnemyToonLook] the shader " + ToonLook.ShaderName + " is not in this build or not supported here: the enemies keep today's look");
            return !s_failed;
        }
    }
}
