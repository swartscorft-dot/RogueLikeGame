using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 115's follow-up, the edge light from the room's own lights (Kristian, 2026-11-24, of the fixed teal edge: "Ja,
    /// men det lyset er en refleksjon? Det er ikke \"på\" karakteren. Slik man ser det på bildene fra codex også"). Her model alone
    /// is drawn (on a layer of its own, onto plain magenta, every light of the scene switched off) by a camera at the game's angle,
    /// with the toon look's values changed between two pictures of the same frame: with no light near her the edge adds nothing;
    /// with one coloured light behind her to one side, the edge on that side takes its hue and the other side does not; and the
    /// edge light's switch off draws her exactly as the look with no edge light at all.
    /// </summary>
    public class ToonLookEdgeTests
    {
        const int MeasureLayer = 31;
        const int W = 960, H = 540;
        /// <summary>The camera's distance for these pictures: close, so her edge is many pixels long (m).</summary>
        const float Distance = 7f;
        /// <summary>A channel may differ by this much between two pictures and count as the same (8-bit rounding).</summary>
        const int Same = 1;
        static readonly Color Background = new Color(1f, 0f, 1f, 1f);

        PlayerCharacter _player;
        ToonLook _look;
        readonly List<Light> _switchedOff = new List<Light>();
        readonly Dictionary<Renderer, int> _layers = new Dictionary<Renderer, int>();
        readonly List<GameObject> _made = new List<GameObject>();
        float _rim, _edgeStrength;
        bool _edgeOn;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            ToonLookValues.On = true;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _player.Teleport(Vector3.zero);
            yield return null;
            _look = _player.GetComponent<ToonLook>();
            Assert.That(_look.Applied, Is.True);
            ToonLookValues v = ToonLookValues.Live;
            _rim = v.RimStrength;
            _edgeStrength = v.LampEdgeStrength;
            _edgeOn = v.LampEdgeOn;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.enabled) { l.enabled = false; _switchedOff.Add(l); }
            CharacterAnimation anim = _player.GetComponentInChildren<CharacterAnimation>(true);
            Transform model = anim.Animator != null ? anim.Animator.transform : anim.transform;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                _layers[r] = r.gameObject.layer;
                r.gameObject.layer = MeasureLayer;
            }
        }

        [TearDown]
        public void CleanUp()
        {
            ToonLookValues v = ToonLookValues.Live;
            v.RimStrength = _rim;
            v.LampEdgeStrength = _edgeStrength;
            v.LampEdgeOn = _edgeOn;
            if (_look != null) _look.Refresh();
            foreach (Light l in _switchedOff) if (l != null) l.enabled = true;
            _switchedOff.Clear();
            foreach (KeyValuePair<Renderer, int> kv in _layers) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;
            _layers.Clear();
            foreach (GameObject g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            ToonLookValues.On = true;
            PlayerInputDriver.ReadRealDevices = true;
        }

        Camera MeasureCamera()
        {
            FeelValues feel = _player.Feel ?? new FeelValues();
            var go = new GameObject("ToonLookEdgeCamera");
            _made.Add(go);
            Camera cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.cullingMask = 1 << MeasureLayer;
            cam.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            Vector3 middle = _player.Position;
            cam.transform.SetPositionAndRotation(new Vector3(middle.x, 0.9f, middle.z) - rotation * Vector3.forward * Distance, rotation);
            return cam;
        }

        static Color32[] Render(Camera cam)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                return tex.GetPixels32();
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        /// <summary>The picture with the edge light set so: its switch, its strength, the fixed edge's strength.</summary>
        Color32[] With(Camera cam, bool edgeOn, float edgeStrength, float rim)
        {
            ToonLookValues v = ToonLookValues.Live;
            v.LampEdgeOn = edgeOn;
            v.LampEdgeStrength = edgeStrength;
            v.RimStrength = rim;
            _look.Refresh();
            return Render(cam);
        }

        static bool Hers(Color32 c) => !(c.r > 230 && c.g < 25 && c.b > 230);

        Light ColouredLight(Color colour, Vector3 offset)
        {
            var go = new GameObject("ToonLookEdgeTestLight");
            _made.Add(go);
            go.transform.position = new Vector3(_player.Position.x, 0f, _player.Position.z) + offset;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = colour;
            l.intensity = 4f;
            l.range = 8f;
            l.shadows = LightShadows.None;
            return l;
        }

        [UnityTest]
        public IEnumerator WithTheFixedEdgeOff_AndNoRealLightNearHer_NoPixelOfHerIsLighterWithTheEdgeLightOn()
        {
            yield return null;
            Camera cam = MeasureCamera();
            float strength = ToonLookValues.Live.LampEdgeStrength;
            Assert.That(ToonLookValues.Live.RimStrength, Is.EqualTo(0f), "the fixed edge off by default");
            Color32[] off = With(cam, false, strength, 0f);
            Color32[] on = With(cam, true, strength, 0f);
            int hers = 0, lighter = 0;
            for (int i = 0; i < on.Length; i++)
            {
                if (!Hers(off[i])) continue;
                hers++;
                if (on[i].r > off[i].r + Same || on[i].g > off[i].g + Same || on[i].b > off[i].b + Same) lighter++;
            }
            TestContext.WriteLine("TOONEDGE no light: " + hers + " pixels of her, " + lighter + " lighter with the edge light on");
            Assert.That(hers, Is.GreaterThan(1000), "she is in the picture");
            Assert.That(lighter, Is.EqualTo(0), "no light near her: no edge");
        }

        [UnityTest]
        public IEnumerator AColouredLightBehindHerToOneSide_LightsTheEdgeOnThatSideInItsHue_AndNotTheOther()
        {
            ColouredLight(new Color(0f, 1f, 0f), new Vector3(2f, 1.3f, 1.6f)); // green, to her right (the picture's right) and behind her (away from the camera)
            yield return null;
            Camera cam = MeasureCamera();
            float strength = ToonLookValues.Live.LampEdgeStrength;
            Color32[] off = With(cam, false, strength, 0f);
            Color32[] on = With(cam, true, strength, 0f);
            // Her middle across the picture: the mean column of her pixels.
            double sumX = 0;
            int n = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Hers(off[y * W + x])) { sumX += x; n++; }
            Assert.That(n, Is.GreaterThan(1000), "she is in the picture");
            double middle = sumX / n;
            long rightG = 0, leftG = 0, rightRB = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!Hers(off[i])) continue;
                    int dg = Mathf.Max(0, on[i].g - off[i].g), drb = Mathf.Max(0, on[i].r - off[i].r) + Mathf.Max(0, on[i].b - off[i].b);
                    if (x > middle) { rightG += dg; rightRB += drb; }
                    else leftG += dg;
                }
            string line = "TOONEDGE green light behind her to the right: green added on her right side " + rightG + ", on her left " + leftG + ", red and blue added on the right " + rightRB;
            TestContext.WriteLine(line);
            Debug.Log(line);
            Assert.That(rightG, Is.GreaterThan(2000), "her right edge lit: " + line);
            Assert.That(rightG, Is.GreaterThan(leftG * 4), "the side toward the light, not the other: " + line);
            Assert.That(rightG, Is.GreaterThan(rightRB * 4), "in the light's hue: " + line);
        }

        [UnityTest]
        public IEnumerator TheEdgeLightsSwitchOff_DrawsHerAsTheLookWithNoEdgeLightAtAll()
        {
            ColouredLight(new Color(0f, 1f, 0.8f), new Vector3(-2f, 1.3f, 1.6f));
            ColouredLight(new Color(1f, 0.66f, 0.36f), new Vector3(2f, 1.3f, 1.6f));
            yield return null;
            Camera cam = MeasureCamera();
            Color32[] switchedOff = With(cam, false, ToonLookValues.Live.LampEdgeStrength, 0f);
            Color32[] none = With(cam, true, 0f, 0f);
            Color32[] lit = With(cam, true, _edgeStrength, 0f);
            int differ = 0, litDiffer = 0;
            for (int i = 0; i < none.Length; i++)
            {
                if (!switchedOff[i].Equals(none[i])) differ++;
                if (!lit[i].Equals(none[i])) litDiffer++;
            }
            TestContext.WriteLine("TOONEDGE switch off against no edge light: " + differ + " pixels differ; with the edge light on, " + litDiffer + " differ");
            Assert.That(differ, Is.EqualTo(0), "the switch off is the look with no edge light");
            Assert.That(litDiffer, Is.GreaterThan(0), "and the edge light does show with these two lights");
        }
    }
}
