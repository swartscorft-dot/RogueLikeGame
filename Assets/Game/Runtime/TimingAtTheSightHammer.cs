using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The hammer cue's pieces and the click (work order 134). Her pistols are one mesh each (the Desert Eagle model: no hammer, no
    /// cylinder that can be turned), so the weapon shows the timing by its muzzles' light, as work order 064's muzzle light lights a
    /// shot: a soft glow at each muzzle, facing the camera, added light on the effects' own unlit material
    /// (<see cref="MoodShapes.MakeUnlit"/>), and one small point light between her hands so the floor round her shows it too. Made
    /// the first time the hammer is chosen; each frame only moves them and writes a few colours into arrays made once.
    /// The click: one clip made in code once (<see cref="TimingClick.Recipe"/>) on a plain 2D voice of its own, at its own level
    /// (<see cref="TimingAtTheSightValues.ClickVolume"/>), never through the shots' voices.
    /// </summary>
    public sealed partial class TimingAtTheSight
    {
        const int GlowRim = 16;                         // the glow's rim points round its lit middle
        static readonly Color s_Warm = new Color(1f, 0.6f, 0.15f);   // her colour, the effects' warm orange (CombatFxValues.HerRim)
        static readonly Color s_Hot = new Color(1f, 0.97f, 0.85f);   // white-hot, at the brightest (CombatFxValues.HerCore, a little warmer)

        GameObject _hammerRoot;
        Material _glowMaterial;
        readonly GameObject[] _glow = new GameObject[2];
        readonly Mesh[] _glowMesh = new Mesh[2];
        readonly Color[] _glowColours = new Color[GlowRim + 1];
        Light _hammerLight;

        AudioSource _clickVoice;
        AudioClip _clickClip;

        /// <summary>The hammer's pieces (for tests): made, and shown now.</summary>
        public bool HammerMade => _hammerRoot != null;
        public bool HammerShown => _hammerRoot != null && _hammerRoot.activeSelf;
        /// <summary>The glow at the right muzzle (0) or the left (1), or null; and the point light. For tests and pictures.</summary>
        public GameObject GlowOf(int hand) => hand >= 0 && hand < 2 ? _glow[hand] : null;
        public Light HammerLight => _hammerLight;

        void DrawHammer(PlayerCharacter p, float level, TimingAtTheSightValues v)
        {
            if (p == null) { HideHammer(); return; }
            if (_hammerRoot == null) MakeHammer();
            Camera cam = Camera.main;
            Quaternion facing = cam != null ? cam.transform.rotation : Quaternion.Euler(55f, 0f, 0f);
            bool hot = level >= HammerCue.Brightest;
            Color tint = hot ? s_Hot : s_Warm;
            float glow = v.GlowPeak * level;
            Color mid = new Color(tint.r * glow, tint.g * glow, tint.b * glow, 1f);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) mid = mid.linear;
            _glowColours[0] = mid;
            for (int k = 1; k <= GlowRim; k++) _glowColours[k] = Color.black;
            float size = v.GlowSize * Mathf.Lerp(v.GlowLeast, 1f, level);
            Vector3 middle = Vector3.zero;
            int shown = 0;
            for (int hand = 0; hand < 2; hand++)
            {
                Transform muzzle = hand == 0 ? p.RightMuzzle : p.LeftMuzzle;
                bool show = muzzle != null && muzzle.gameObject.activeInHierarchy;
                GameObject go = _glow[hand];
                if (!show)
                {
                    if (go.activeSelf) go.SetActive(false);
                    continue;
                }
                Vector3 at = muzzle.position;
                Vector3 toCam = cam != null ? (cam.transform.position - at).normalized : Vector3.up;
                go.transform.SetPositionAndRotation(at + toCam * 0.1f, facing); // a hand's breadth toward the camera: never inside the gun
                go.transform.localScale = new Vector3(size, size, size);
                _glowMesh[hand].colors = _glowColours;
                if (!go.activeSelf) go.SetActive(true);
                middle += at;
                shown++;
            }
            if (_hammerLight != null)
            {
                bool light = v.LightPeak > 0f && shown > 0;
                if (light)
                {
                    _hammerLight.transform.position = middle / shown;
                    _hammerLight.color = tint;
                    _hammerLight.range = v.LightRange;
                    _hammerLight.intensity = v.LightPeak * level;
                }
                if (_hammerLight.enabled != light) _hammerLight.enabled = light;
            }
            if (!_hammerRoot.activeSelf) _hammerRoot.SetActive(true);
        }

        void HideHammer()
        {
            if (_hammerRoot != null && _hammerRoot.activeSelf) _hammerRoot.SetActive(false);
        }

        void MakeHammer()
        {
            _glowMaterial = MoodShapes.MakeUnlit("TimingHammerGlow", MoodShapes.Blend.Add, null, LaserSight.Queue);
            _hammerRoot = new GameObject("TimingHammer");
            _hammerRoot.transform.SetParent(transform, false);
            // A disc: a lit middle and a dark rim round it, a unit across, facing its own -z (toward the camera when turned as it).
            var verts = new Vector3[GlowRim + 1];
            var tris = new int[GlowRim * 3];
            for (int k = 0; k < GlowRim; k++)
            {
                float a = k * Mathf.PI * 2f / GlowRim;
                verts[k + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                tris[k * 3] = 0;
                tris[k * 3 + 1] = k + 1;
                tris[k * 3 + 2] = (k + 1) % GlowRim + 1;
            }
            for (int hand = 0; hand < 2; hand++)
            {
                var mesh = new Mesh { name = hand == 0 ? "TimingHammerGlow_R" : "TimingHammerGlow_L" };
                mesh.MarkDynamic();
                mesh.vertices = verts;
                mesh.colors = _glowColours;
                mesh.triangles = tris;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(_hammerRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _glowMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                _glow[hand] = go;
                _glowMesh[hand] = mesh;
            }
            var lightGo = new GameObject("TimingHammerLight");
            lightGo.transform.SetParent(_hammerRoot.transform, false);
            _hammerLight = lightGo.AddComponent<Light>();
            _hammerLight.type = LightType.Point;
            _hammerLight.shadows = LightShadows.None;
            _hammerLight.enabled = false;
            _hammerRoot.SetActive(false);
        }

        void DisposeHammer()
        {
            for (int hand = 0; hand < 2; hand++)
            {
                if (_glowMesh[hand] != null) Destroy(_glowMesh[hand]);
                _glowMesh[hand] = null;
                _glow[hand] = null;
            }
            if (_hammerRoot != null) Destroy(_hammerRoot);
            if (_glowMaterial != null) Destroy(_glowMaterial);
            _hammerRoot = null;
            _glowMaterial = null;
            _hammerLight = null;
        }

        // ---------------- The click ----------------

        void Click(int press, TimingAtTheSightValues v)
        {
            if (_clickVoice == null) MakeClick();
            float pitch = TimingClick.Pitch(press, v);
            Clicks++;
            LastClickPitch = pitch;
            Clicked?.Invoke(pitch);
            if (_clickVoice == null || _clickClip == null || v.ClickVolume <= 0f) return;
            _clickVoice.pitch = pitch;
            _clickVoice.PlayOneShot(_clickClip, Mathf.Clamp01(v.ClickVolume));
        }

        void MakeClick()
        {
            float[] samples = Synth.Render(TimingClick.Recipe(), Synth.SampleRate);
            _clickClip = AudioClip.Create("TimingClick", samples.Length, 1, Synth.SampleRate, false);
            _clickClip.SetData(samples, 0);
            _clickVoice = gameObject.AddComponent<AudioSource>();
            _clickVoice.playOnAwake = false;
            _clickVoice.spatialBlend = 0f; // plain 2D, as every sound of the game (the camera is 28 m up)
            _clickVoice.loop = false;
        }

        void DisposeClick()
        {
            if (_clickClip != null) Destroy(_clickClip);
            _clickClip = null;
        }
    }
}
