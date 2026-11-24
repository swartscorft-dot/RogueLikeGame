using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The warder's own body (work order 107, the warder that reads; Kristian's tick of 2026-11-24, "vokteren som ikke leses"):
    /// at a glance from the game's camera at lamplit C it is not a caster. It wears the caster's model (work order 079), so what
    /// sets it apart is made here from what the project has, in the ward's green (<see cref="EnemyWarder.WardColor"/>):
    ///
    /// - a staff it carries upright at its right side, taller than any of them, with the ward's light on top: a glowing sphere
    ///   (a caster carries nothing). No real light: a floor's lights are counted against its budget (the lamplit orders);
    /// - a halo over its head, a ring of the ward's light: the same ring the enemies it wards wear round them, so the two read
    ///   as one thing;
    /// - the model's own glows (eyes, palms, amulet) in the ward's green, where a caster's are cyan.
    ///
    /// While a ward of its is on, its light and its halo pulse together (<see cref="WarderReadRules.Pulse"/>). Dead, it shows none
    /// of it. Made as the warder wakes when its switch is on (<see cref="WarderValues.Reads"/>), so that what hides or swaps an
    /// enemy's renderers (the cave's rooms out of sight, the greybox view) finds it with the rest; made later if the switch is
    /// turned on in the Inspector, and kept: the switch turned off hides it all and gives the model back its own glows. Its two materials are its own and go with it; the
    /// primitives' meshes are the engine's own. Nothing is allocated in a frame.
    /// </summary>
    public sealed class WarderBody
    {
        public const string RootName = "WarderBody";
        public const string StaffName = "WarderStaff";
        public const string LightName = "WarderLight"; // the glowing sphere on top of the staff
        public const string HaloName = "WarderHalo";
        /// <summary>The start of the name of a material made here; it goes with the warder.</summary>
        public const string MaterialPrefix = "WarderBody_";
        const int HaloSegments = 32;
        // The staff's own colour: dark weathered wood, so its green light is the bright thing on it.
        static readonly Color StaffColor = new Color(0.16f, 0.13f, 0.1f);

        static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_Color = Shader.PropertyToID("_Color");

        readonly EnemyWarder _warder;
        readonly GameObject _root;
        readonly Transform _staff, _orb;
        readonly Renderer _staffRenderer, _orbRenderer;
        readonly LineRenderer _halo;
        readonly Material _staffMaterial, _orbMaterial;
        readonly List<Material> _glowing = new List<Material>();
        readonly List<Color> _glowBefore = new List<Color>();
        readonly Color _orbRest;
        readonly float _haloWidth;
        bool _shown;
        bool _glowsGreen;
        float _swellShown = -1f;

        /// <summary>Is it shown now (its switch on, the warder alive)?</summary>
        public bool Shown => _shown;
        /// <summary>The middle of the ward's light on its staff, in the world: its threads run from here.</summary>
        public Vector3 LightPoint => _orb.position;
        /// <summary>The parts, for tests: the staff, the light's sphere, the halo.</summary>
        public Transform Staff => _staff;
        public Transform Orb => _orb;
        public Renderer OrbRenderer => _orbRenderer;
        public Renderer StaffRenderer => _staffRenderer;
        public LineRenderer Halo => _halo;
        /// <summary>The model's materials whose glow it made green, for tests.</summary>
        public int GlowsMadeGreen => _glowsGreen ? _glowing.Count : 0;
        /// <summary>The swell it shows now (1 at rest), for tests.</summary>
        public float Swell => _swellShown < 0f ? 1f : _swellShown;

        WarderBody(EnemyWarder warder, LineRenderer lineLook)
        {
            _warder = warder;
            WarderReadValues r = warder.Tuning.Reading ?? new WarderReadValues();
            Transform t = warder.transform;
            float scale = Mathf.Max(1e-3f, Mathf.Abs(t.lossyScale.x));
            int layer = warder.gameObject.layer;
            Color ward = EnemyWarder.WardColor;

            // One child holds every part, scaled back to the world's metres: the parts are placed in metres beside it.
            _root = new GameObject(RootName) { layer = layer };
            _root.transform.SetParent(t, false);
            _root.transform.localScale = Vector3.one / scale;

            Shader shader = Shader.Find(PropFactory.ShaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            float orbMiddle = Mathf.Max(r.OrbRadius, r.StaffTop - r.OrbRadius);
            if (r.Staff)
            {
                // The staff: a thin cylinder from the floor up to its light, at its right side and a little ahead.
                GameObject staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                staff.name = StaffName;
                staff.layer = layer;
                Object.Destroy(staff.GetComponent<Collider>());
                _staff = staff.transform;
                _staff.SetParent(_root.transform, false);
                float length = Mathf.Max(0.1f, orbMiddle);
                _staff.localPosition = new Vector3(r.StaffSide, length * 0.5f, r.StaffAhead);
                _staff.localScale = new Vector3(r.StaffRadius * 2f, length * 0.5f, r.StaffRadius * 2f); // the cylinder is 2 m high
                _staffMaterial = new Material(shader) { name = MaterialPrefix + "Staff" };
                EnemyFactory.Paint(_staffMaterial, StaffColor);
                _staffRenderer = staff.GetComponent<Renderer>();
                _staffRenderer.sharedMaterial = _staffMaterial;
                _staffRenderer.shadowCastingMode = ShadowCastingMode.On;

                // The ward's light on top: a glowing sphere of the ward's colour.
                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = LightName;
                orb.layer = layer;
                Object.Destroy(orb.GetComponent<Collider>());
                _orb = orb.transform;
                _orb.SetParent(_root.transform, false);
                _orb.localPosition = new Vector3(r.StaffSide, orbMiddle, r.StaffAhead);
                _orb.localScale = Vector3.one * (r.OrbRadius * 2f);
                _orbMaterial = new Material(shader) { name = MaterialPrefix + "Light" };
                EnemyFactory.Paint(_orbMaterial, ward);
                _orbRest = Glow(ward, r.OrbGlow);
                if (_orbMaterial.HasProperty(s_EmissionColor))
                {
                    _orbMaterial.EnableKeyword("_EMISSION");
                    _orbMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    _orbMaterial.SetColor(s_EmissionColor, _orbRest);
                }
                _orbRenderer = orb.GetComponent<Renderer>();
                _orbRenderer.sharedMaterial = _orbMaterial;
                _orbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                // No staff: its threads run from where they always did, and there is no light to pulse.
                var point = new GameObject(LightName) { layer = layer };
                _orb = point.transform;
                _orb.SetParent(_root.transform, false);
                _orb.localPosition = Vector3.up * warder.Tuning.ThreadHeight;
            }

            if (r.Halo && lineLook != null)
            {
                // The halo: a ring of the ward's light over its head, in its own space, so it goes where it goes for nothing.
                GameObject halo = Object.Instantiate(lineLook.gameObject, _root.transform, false);
                halo.name = HaloName;
                halo.layer = layer;
                _halo = halo.GetComponent<LineRenderer>();
                _halo.useWorldSpace = false;
                _halo.loop = true;
                _halo.positionCount = HaloSegments;
                float head = warder.ModelHeight > 0f ? warder.ModelHeight : warder.Tuning.BodyHeight * scale;
                float y = head + r.HaloOver;
                for (int i = 0; i < HaloSegments; i++)
                {
                    float a = i / (float)HaloSegments * Mathf.PI * 2f;
                    _halo.SetPosition(i, new Vector3(Mathf.Cos(a) * r.HaloRadius, y, r.HaloAhead + Mathf.Sin(a) * r.HaloRadius));
                }
                _haloWidth = r.HaloWidth;
                _halo.widthMultiplier = _haloWidth;
                var block = new MaterialPropertyBlock();
                block.SetColor(s_BaseColor, ward);
                block.SetColor(s_Color, ward);
                _halo.SetPropertyBlock(block);
                _halo.enabled = true;
            }

            // The model's own glows, which the caster has in cyan: in the ward's green. With a body glow its whole body glows
            // faintly in the green instead (its own painted texture as its glow), a green figure where the caster is a dark one.
            _bodyGlows = r.BodyGlow > 0f;
            if ((r.GreenGlows || _bodyGlows) && warder.Look != null && warder.Look.Renderers != null)
            {
                foreach (Renderer renderer in warder.Look.Renderers)
                {
                    Material m = renderer != null ? renderer.sharedMaterial : null;
                    if (m == null || _glowing.Contains(m) || !m.HasProperty(s_EmissionColor) || !m.IsKeywordEnabled("_EMISSION")) continue;
                    _glowing.Add(m);
                    _glowBefore.Add(m.GetColor(s_EmissionColor));
                    _mapBefore.Add(m.HasProperty(s_EmissionMap) ? m.GetTexture(s_EmissionMap) : null);
                    _paint.Add(m.HasProperty(s_BaseMap) && m.GetTexture(s_BaseMap) != null ? m.GetTexture(s_BaseMap) : m.mainTexture);
                }
            }
            _greenGlow = _bodyGlows ? Glow(ward, r.BodyGlow) : Glow(ward, r.GreenGlowStrength);
            _shown = false;
            Show(true);
        }

        readonly Color _greenGlow;
        readonly bool _bodyGlows;
        readonly List<Texture> _mapBefore = new List<Texture>();
        readonly List<Texture> _paint = new List<Texture>();
        static readonly int s_EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");

        /// <summary>The emission colour for <paramref name="c"/> glowing <paramref name="k"/> times, as the model's glows are set (<see cref="EnemyLook"/>).</summary>
        static Color Glow(Color c, float k)
        {
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color l = linear ? c.linear : c;
            var lit = new Color(l.r * k, l.g * k, l.b * k, 1f);
            return linear ? lit.gamma : lit;
        }

        /// <summary>Its body for this warder, made now: its threads' look is copied for its halo.</summary>
        public static WarderBody Make(EnemyWarder warder, LineRenderer lineLook) => warder == null ? null : new WarderBody(warder, lineLook);

        /// <summary>One frame: shown while the warder is alive; its light pulsing while <paramref name="working"/> (a ward of its on).</summary>
        public void Tick(float now, bool working, bool alive)
        {
            if (alive != _shown) Show(alive);
            if (!_shown) return;
            float swell = working ? WarderReadRules.Swell(_warder.Tuning, now) : 1f;
            if (Mathf.Abs(swell - _swellShown) < 1e-4f) return;
            _swellShown = swell;
            if (_orbMaterial != null && _orbMaterial.HasProperty(s_EmissionColor))
                _orbMaterial.SetColor(s_EmissionColor, new Color(_orbRest.r * swell, _orbRest.g * swell, _orbRest.b * swell, 1f));
            if (_halo != null) _halo.widthMultiplier = _haloWidth * swell;
        }

        /// <summary>Shows or hides every part; the model's glows green while shown, its own otherwise.</summary>
        public void Show(bool on)
        {
            if (on == _shown && on == _glowsGreen) return;
            _shown = on;
            if (_staffRenderer != null) _staffRenderer.enabled = on;
            if (_orbRenderer != null) _orbRenderer.enabled = on;
            if (_halo != null) _halo.enabled = on;
            _swellShown = -1f;
            for (int i = 0; i < _glowing.Count; i++)
            {
                if (_glowing[i] == null) continue;
                _glowing[i].SetColor(s_EmissionColor, on ? _greenGlow : _glowBefore[i]);
                if (_bodyGlows && _glowing[i].HasProperty(s_EmissionMap)) _glowing[i].SetTexture(s_EmissionMap, on ? _paint[i] : _mapBefore[i]);
            }
            _glowsGreen = on;
        }

        /// <summary>Its materials go with it (its parts go with the warder's object).</summary>
        public void Dispose()
        {
            if (_staffMaterial != null) Object.Destroy(_staffMaterial);
            if (_orbMaterial != null) Object.Destroy(_orbMaterial);
            _glowing.Clear();
            _glowBefore.Clear();
            _mapBefore.Clear();
            _paint.Clear();
        }
    }
}
