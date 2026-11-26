using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 115, the toon look on her model (a trial; Kristian, 2026-11-24, of the key art drawn of her: "Klarer vi å få
    /// modellen i spillet til å ha mer av det?"): the switch off gives her renderers back today's material objects, every value in
    /// them as it was; the materials made are destroyed with her; a frame with the switch standing allocates nothing; the shader
    /// is kept for a built player the way work order 032 keeps the code's shaders (a material on it in the Resources folder
    /// <c>CodeMadeShaders</c>) and is found by name; the ink line is its width in pixels on the screen at the camera's nearest and
    /// farthest steps and is hidden where she is hidden; the flashes and glows around her still change what they changed with the
    /// look on; the pause menu's line flips it; and her scene object woken in both orders puts it on and logs nothing.
    /// </summary>
    public class ToonLookTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>The ink line's measured width may be off its named width by this many pixels (pixels are whole on the screen and nothing is antialiased).</summary>
        const float InkTolerancePixels = 1f;
        /// <summary>A layer nothing in the scene is on: her model is put on it alone for the measure.</summary>
        const int MeasureLayer = 31;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        GameObject _go, _original;
        readonly List<GameObject> _made = new List<GameObject>();

        IEnumerator Load(bool on)
        {
            ToonLookValues.On = on;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            Roguelike.Rules.HitChipsValues.On = true; // work order 127: back to its default
            ToonLookValues.On = true;
            foreach (GameObject g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            if (_original != null) _original.SetActive(true);
            _original = null;
            if (_player != null && _player.GetComponent<PlayerHealth>() != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject);
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        /// <summary>Her model's renderers: everything under the object her animator is on.</summary>
        static List<Renderer> ModelRenderers(PlayerCharacter p)
        {
            CharacterAnimation anim = p.GetComponentInChildren<CharacterAnimation>(true);
            Assert.That(anim, Is.Not.Null, "her model's animation");
            Transform model = anim.Animator != null ? anim.Animator.transform : anim.transform;
            return model.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToList();
        }

        /// <summary>Everything a material holds, as text: its name, shader, queue, keywords and every property of its shader.</summary>
        static string Fingerprint(Material m)
        {
            if (m == null) return "null";
            var sb = new StringBuilder();
            sb.Append(m.name).Append('|').Append(m.shader != null ? m.shader.name : "no shader").Append('|').Append(m.renderQueue).Append('|')
              .Append(string.Join(",", m.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal))).Append('|').Append(m.enableInstancing).Append('|').Append(m.globalIlluminationFlags);
            Shader s = m.shader;
            for (int i = 0; s != null && i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                sb.Append('|').Append(n).Append('=');
                switch (s.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: sb.Append(m.GetColor(n).ToString("F6")); break;
                    case ShaderPropertyType.Vector: sb.Append(m.GetVector(n).ToString("F6")); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: sb.Append(m.GetFloat(n).ToString("R", Inv)); break;
                    case ShaderPropertyType.Int: sb.Append(m.GetInteger(n)); break;
                    case ShaderPropertyType.Texture:
                        Texture t = m.GetTexture(n);
                        sb.Append(t != null ? t.name + "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(t) : "none").Append(m.GetTextureScale(n).ToString("F6")).Append(m.GetTextureOffset(n).ToString("F6"));
                        break;
                }
            }
            return sb.ToString();
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator TheSwitchOn_PutsTheToonOnHerModel_AndOff_GivesBackTodaysMaterials_ValueForValue()
        {
            yield return Load(on: false);
            ToonLook look = _player.GetComponent<ToonLook>();
            Assert.That(look, Is.Not.Null, "her character adds the look in its Awake");
            Assert.That(look.Applied, Is.False, "the switch off: nothing put on");
            List<Renderer> renderers = ModelRenderers(_player);
            Assert.That(renderers.Count, Is.GreaterThan(0));
            var before = renderers.ToDictionary(r => r, r => r.sharedMaterials);
            var prints = renderers.ToDictionary(r => r, r => r.sharedMaterials.Select(Fingerprint).ToArray());

            ToonLookValues.On = true;
            yield return null;
            yield return null;
            Assert.That(look.Applied, Is.True, "the switch on: put on within a frame");
            Assert.That(look.TargetCount, Is.GreaterThan(0));
            bool body = false, kit = false;
            var lines = new List<string>();
            for (int i = 0; i < look.TargetCount; i++)
            {
                Renderer r = look.TargetRenderer(i);
                Assert.That(renderers, Has.Member(r), r.name + ": a renderer of her model");
                body |= r is SkinnedMeshRenderer;
                kit |= look.IsKit(i);
                Material[] now = r.sharedMaterials;
                Assert.That(now.Length, Is.EqualTo(before[r].Length), r.name + ": as many materials as today");
                for (int k = 0; k < now.Length; k++)
                {
                    Assert.That(now[k].shader.name, Is.EqualTo(ToonLook.ShaderName), r.name + " slot " + k);
                    Assert.That(now[k].name, Does.StartWith(ToonLook.MaterialPrefix));
                    Assert.That(now[k].GetTexture("_BaseMap"), Is.SameAs(before[r][k].GetTexture("_BaseMap")), r.name + ": her own texture");
                    lines.Add(r.name + " (" + r.GetType().Name + (look.IsKit(i) ? ", kit" : ", body") + "): " + before[r][k].name + " -> " + now[k].name);
                }
            }
            TestContext.WriteLine("TOONLOOK targets: " + string.Join("; ", lines));
            Assert.That(body, Is.True, "her skinned body wears it");
            Assert.That(kit, Is.True, "and the weapons on her (their teal lights glow)");

            ToonLookValues.On = false;
            yield return null;
            Assert.That(look.Applied, Is.False);
            foreach (Renderer r in renderers)
            {
                Material[] now = r.sharedMaterials;
                Assert.That(now.Length, Is.EqualTo(before[r].Length), r.name);
                for (int k = 0; k < now.Length; k++)
                {
                    Assert.That(now[k], Is.SameAs(before[r][k]), r.name + " slot " + k + ": the very material it had");
                    Assert.That(Fingerprint(now[k]), Is.EqualTo(prints[r][k]), r.name + " slot " + k + ": every value as it was");
                }
            }

            // On and off again: the same copies, the same today's.
            ToonLookValues.On = true;
            yield return null;
            int made = look.Made.Count;
            ToonLookValues.On = false;
            yield return null;
            ToonLookValues.On = true;
            yield return null;
            Assert.That(look.Made.Count, Is.EqualTo(made), "the copies are made once");
        }

        [UnityTest]
        public IEnumerator EveryMaterialItMade_IsDestroyedWithHer()
        {
            // A copy of her is woken and destroyed (the scene's own her stays: the scene's enemies hold her).
            yield return Load(on: false);
            _original = _player.gameObject;
            _original.SetActive(false);
            _go = Object.Instantiate(_original);
            ToonLookValues.On = true;
            _go.SetActive(true);
            _go.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _go.GetComponent<PlayerHealth>().Untouchable = true;
            yield return null;
            yield return null;
            ToonLook look = _go.GetComponent<ToonLook>();
            Assert.That(look.Applied, Is.True);
            List<Material> made = look.Made.ToList();
            Assert.That(made.Count, Is.GreaterThan(0));
            Assert.That(made.All(m => m != null), Is.True);
            Object.Destroy(_go);
            _go = null;
            yield return null;
            yield return null;
            foreach (Material m in made) Assert.That(m == null, Is.True, "destroyed with her");
            _original.SetActive(true);
            _original = null;
        }

        [UnityTest]
        public IEnumerator AFrameWithTheSwitchStanding_AllocatesNothing()
        {
            yield return Load(on: true);
            ToonLook look = _player.GetComponent<ToonLook>();
            look.Tick();
            Assert.That(() => look.Tick(), NotAllocating, "on, standing");
            ToonLookValues.On = false;
            look.Tick();
            Assert.That(() => look.Tick(), NotAllocating, "off, standing");
        }

        // ---------------- The build ----------------

        [UnityTest]
        public IEnumerator TheShader_IsFound_AndKeptForABuiltPlayer_TheWayTheCodesShadersAre()
        {
            yield return null;
            Shader shader = Shader.Find(ToonLook.ShaderName);
            Assert.That(shader, Is.Not.Null, ToonLook.ShaderName);
            Assert.That(shader.isSupported, Is.True, "supported on this machine");
            bool Kept() => Resources.LoadAll<Material>("CodeMadeShaders").Any(m => m != null && m.shader != null && m.shader.name == ToonLook.ShaderName);
#if UNITY_EDITOR
            if (!Kept())
            {
                // The first time only: the kept material is made by the project's own step (CodeMadeShaders.Ensure, which every
                // build runs first), through Unity, as work order 032's three were.
                Type ensure = Type.GetType("Roguelike.EditorTools.CodeMadeShaders, Roguelike.Editor");
                Assert.That(ensure, Is.Not.Null, "CodeMadeShaders");
                ensure.GetMethod("Ensure").Invoke(null, null);
                Assert.That((bool)ensure.GetMethod("AllThere").Invoke(null, null), Is.True, "every kept material there with its shader");
            }
#endif
            Assert.That(Kept(), Is.True, "a material on it in Resources/CodeMadeShaders, which goes into every build whole");
        }

        // ---------------- The ink line ----------------

        /// <summary>The camera at the game's angle <paramref name="distance"/> m from her feet, drawing only <paramref name="mask"/>, onto plain green.</summary>
        Camera MeasureCamera(float distance, int mask)
        {
            FeelValues feel = _player.Feel ?? new FeelValues();
            var go = new GameObject("ToonLookMeasureCamera");
            _made.Add(go);
            Camera cam = go.AddComponent<Camera>(); // a camera of its own: no post-processing, no other camera's settings
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.green;
            cam.cullingMask = mask;
            cam.fieldOfView = feel.FieldOfView;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            Vector3 focus = _player.Position;
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * distance, rotation);
            return cam;
        }

        static Color32[] Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
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

        static bool[] Mask(Color32[] px)
        {
            var m = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) m[i] = px[i].r > 40 || px[i].g < 215 || px[i].b > 40; // anything that is not the green
            return m;
        }

        /// <summary>
        /// How far the ink line widens her silhouette, in pixels: along every row and every column that crosses her, how far the
        /// first and the last covered pixel moved out; the median of those, and the lower quartile beside it. Pixels are whole: a
        /// line of 2.25 px covers 2 pixels across an edge three times in four and 3 once. An edge slanting to a row or column widens
        /// it there by more; where the hull's vertex normals point mostly toward or away from the camera, their push on the screen
        /// runs partly along the edge and the line is thinner there (the lower quartile shows how much).
        /// </summary>
        static float Widening(bool[] off, bool[] on, int w, int h, out int samples, out float quartile)
        {
            var grow = new List<float>();
            for (int y = 0; y < h; y++) Scan(off, on, w, i => y * w + i, grow);
            for (int x = 0; x < w; x++) Scan(off, on, h, i => i * w + x, grow);
            samples = grow.Count;
            grow.Sort();
            quartile = grow.Count > 0 ? grow[grow.Count / 4] : float.NaN;
            return grow.Count > 0 ? grow[grow.Count / 2] : float.NaN;
        }

        static void Scan(bool[] off, bool[] on, int n, Func<int, int> at, List<float> grow)
        {
            int f0 = -1, l0 = -1, f1 = -1, l1 = -1;
            for (int i = 0; i < n; i++)
            {
                if (off[at(i)]) { if (f0 < 0) f0 = i; l0 = i; }
                if (on[at(i)]) { if (f1 < 0) f1 = i; l1 = i; }
            }
            if (f0 < 0 || f1 < 0) return;
            grow.Add(f0 - f1);
            grow.Add(l1 - l0);
        }

        [UnityTest]
        public IEnumerator TheInkLine_IsItsWidthOnTheScreen_AtTheCamerasNearestAndFarthestSteps_AndIsHiddenWhereSheIsHidden()
        {
            yield return Load(on: false);
            ToonLook look = _player.GetComponent<ToonLook>();
            _player.Teleport(Vector3.zero);
            yield return null;
            FeelValues feel = _player.Feel ?? new FeelValues();
            float nearest = CameraDistanceSteps.Closer.Min(), farthest = Mathf.Max(feel.CameraDistance, CameraDistanceSteps.Closer.Max());
            List<Renderer> renderers = ModelRenderers(_player);
            var layers = renderers.ToDictionary(r => r, r => r.gameObject.layer);
            const int W = 1920, H = 1080;
            float want = ToonLookValues.Live.InkPixels(H);
            try
            {
                foreach (Renderer r in renderers) r.gameObject.layer = MeasureLayer;
                foreach (float d in new[] { nearest, farthest })
                {
                    Camera cam = MeasureCamera(d, 1 << MeasureLayer);
                    // Both in the same frame, so that her pose is the same: today's materials, then the look.
                    ToonLookValues.On = false;
                    look.Tick();
                    bool[] off = Mask(Render(cam, W, H));
                    ToonLookValues.On = true;
                    look.Tick();
                    bool[] on = Mask(Render(cam, W, H));
                    float width = Widening(off, on, W, H, out int samples, out float quartile);
                    int tall = 0;
                    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (off[y * W + x]) { tall++; break; }
                    string line = string.Format(Inv, "TOONLOOK ink at {0:0} m: {1:0.0} px measured (median of {2} edge samples; lower quartile {3:0.0}), {4:0.00} px wanted at {5} rows; she is {6} px tall",
                                                d, width, samples, quartile, want, H, tall);
                    TestContext.WriteLine(line);
                    Debug.Log(line);
                    Assert.That(samples, Is.GreaterThan(200), "she is in the picture at " + d + " m");
                    Assert.That(width, Is.EqualTo(want).Within(InkTolerancePixels), line);
                }

                // Hidden where she is hidden: a wall between her and the camera, and the picture is the same with the look on and off.
                Camera wallCam = MeasureCamera(feel.CameraDistance, (1 << MeasureLayer) | (1 << 30));
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _made.Add(wall);
                wall.layer = 30;
                Object.Destroy(wall.GetComponent<Collider>());
                wall.transform.position = Vector3.Lerp(wallCam.transform.position, _player.Position + Vector3.up * 1f, 0.5f);
                wall.transform.rotation = wallCam.transform.rotation;
                wall.transform.localScale = new Vector3(8f, 8f, 0.2f);
                ToonLookValues.On = false;
                look.Tick();
                Color32[] hiddenOff = Render(wallCam, W / 2, H / 2);
                ToonLookValues.On = true;
                look.Tick();
                Color32[] hiddenOn = Render(wallCam, W / 2, H / 2);
                int differ = 0;
                for (int i = 0; i < hiddenOn.Length; i++)
                    if (!hiddenOn[i].Equals(hiddenOff[i])) differ++;
                Assert.That(differ, Is.EqualTo(0), "nothing of her or her ink line drawn through the wall");
            }
            finally
            {
                foreach (KeyValuePair<Renderer, int> kv in layers) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;
            }
        }

        // ---------------- What the game does around her ----------------

        [UnityTest]
        public IEnumerator WithTheLookOn_ADummysHitFlash_TheLaserSight_TheDash_AndHerDeath_StillDoWhatTheyDid()
        {
            yield return Load(on: true);
            ToonLook look = _player.GetComponent<ToonLook>();
            Assert.That(look.Applied, Is.True);
            List<Renderer> renderers = ModelRenderers(_player);
            var shown = renderers.ToDictionary(r => r, r => r.enabled);

            // A dummy's hit flash: it sets its colour on its own renderer, never on hers.
            TrainingDummy dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsInactive.Exclude).OrderBy(t => (t.transform.position - _player.Position).sqrMagnitude).First();
            Damageable target = dummy.GetComponent<Damageable>();
            target.SetMaxHealth(100000);
            Roguelike.Rules.HitChipsValues.On = false; // work order 127: this pins the flash from before its order; on, a hit throws chips (HitChipsPlayTests)
            target.TakeHit(1);
            yield return null;
            Assert.That(dummy.IsFlashing, Is.True, "the dummy flashes when hit");
            var block = new MaterialPropertyBlock();
            dummy.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.white), "its flash colour on its own renderer");
            foreach (Renderer r in renderers)
            {
                Assert.That(r.HasPropertyBlock(), Is.False, r.name + ": nothing laid over her materials");
            }

            // The Laser Sight's beams: shown with the part, drawn with their own material.
            var m = new RunModifiers();
            m.Weapon.Add(WeaponRack.Find(new MetaValues(), WeaponAttachments.LaserSight));
            _player.Modifiers = m;
            _player.AimAt(_player.Position + Vector3.right * 5f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_player.Laser.Shown, Is.True, "the beams shown with the look on");
            Assert.That(_player.Laser.Material.shader.name, Is.Not.EqualTo(ToonLook.ShaderName), "their own material");

            // The dash: she dashes, and still wears the look, every renderer shown as before.
            int dashes = _player.DashesStarted;
            _player.DashToward(_player.Position + Vector3.forward * 5f);
            yield return null;
            Assert.That(_player.DashesStarted, Is.GreaterThan(dashes), "a dash started");
            AssertWears(look, renderers, shown, "in the dash");
            float until = Time.realtimeSinceStartup + 3f;
            while (_player.IsDashing && Time.realtimeSinceStartup < until) yield return null;

            // Her death: she dies and still wears it; the switch still works on her dead.
            _player.Modifiers = null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            health.Untouchable = false;
            health.TakeHit(100000);
            yield return null;
            Assert.That(health.IsAlive, Is.False, "she died");
            AssertWears(look, renderers, shown, "dead");
            ToonLookValues.On = false;
            yield return null;
            Assert.That(look.Applied, Is.False, "off while dead");
            ToonLookValues.On = true;
            yield return null;
            AssertWears(look, renderers, shown, "dead, on again");
        }

        static void AssertWears(ToonLook look, List<Renderer> renderers, Dictionary<Renderer, bool> shown, string when)
        {
            Assert.That(look.Applied, Is.True, when);
            for (int i = 0; i < look.TargetCount; i++)
                foreach (Material mat in look.TargetRenderer(i).sharedMaterials)
                    Assert.That(mat.shader.name, Is.EqualTo(ToonLook.ShaderName), when + ": " + look.TargetRenderer(i).name);
            foreach (Renderer r in renderers) Assert.That(r.enabled, Is.EqualTo(shown[r]), when + ": " + r.name + " shown as before");
        }

        // ---------------- The pause menu ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusLine_FlipsTheLook_AndHerModelFollowsAtOnce()
        {
            yield return Load(on: true);
            GeneratedLevels.Ensure();
            RunHud hud = RunHud.Ensure();
            yield return null;
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            Assert.That(hud.Escape(), Is.True, "paused");
            yield return null;
            List<RunHud.MenuLine> lines = hud.MenuLines.ToList();
            Assert.That(lines, Has.Member(RunHud.MenuLine.ToonLook));
            Assert.That(lines.IndexOf(RunHud.MenuLine.ToonLook), Is.EqualTo(lines.Count - 2), "just above the attachments' line");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(hud.MenuText(RunHud.MenuLine.ToonLook), Is.EqualTo("Toon look (trial):  on"));
            ToonLook look = _player.GetComponent<ToonLook>();
            Assert.That(look.Applied, Is.True);

            Assert.That(hud.PickMenuLine(RunHud.MenuLine.ToonLook), Is.True);
            Assert.That(ToonLookValues.On, Is.False);
            Assert.That(hud.MenuText(RunHud.MenuLine.ToonLook), Is.EqualTo("Toon look (trial):  off"));
            yield return null;
            Assert.That(look.Applied, Is.False, "her model follows under the pause");
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.ToonLook, -1), Is.True, "the left arrow flips it too");
            Assert.That(ToonLookValues.On, Is.True);
            yield return null;
            Assert.That(look.Applied, Is.True);
            string a = hud.MenuText(RunHud.MenuLine.ToonLook);
            TestDelegate words = () => { string _ = hud.MenuText(RunHud.MenuLine.ToonLook); };
            Assert.That(words, NotAllocating, "the line's words are fixed strings");
            Assert.That(hud.MenuText(RunHud.MenuLine.ToonLook), Is.SameAs(a));
            hud.Escape();
        }

        // ---------------- Woken before and in both orders (in the manner of HerAwakeOrderTests) ----------------

        [Test]
        public void TheLook_AskedBeforeItHasWoken_DoesNothing_AndThrowsNothing()
        {
            _go = new GameObject("ToonLookBeforeAwake");
            _go.SetActive(false); // nothing on it wakes
            ToonLook look = _go.AddComponent<ToonLook>();
            Assert.That(look.Applied, Is.False);
            Assert.That(look.TargetCount, Is.EqualTo(0));
            Assert.That(look.Made.Count, Is.EqualTo(0));
            look.Refresh();
            look.Tick(); // no model under it: finds nothing, puts nothing on, throws nothing
            Assert.That(look.TargetCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheScenesHer_WokenWithTheLookFirst_PutsItOn_AndLogsNothing() => WokenInOrder(lookFirst: true);

        [UnityTest]
        public IEnumerator TheScenesHer_WokenWithHerCharacterFirst_PutsItOn_AndLogsNothing() => WokenInOrder(lookFirst: false);

        IEnumerator WokenInOrder(bool lookFirst)
        {
#if !UNITY_EDITOR
            Assert.Ignore("the components are reordered with the Editor's ComponentUtility");
            yield break;
#else
            yield return Load(on: false); // she is copied in today's materials, as the scene holds her
            _original = _player.gameObject;
            _original.SetActive(false);
            ToonLookValues.On = true;
            _go = Object.Instantiate(_original); // off, as the original is now: nothing on the copy has woken
            _go.name = _original.name;
            PlayerCharacter her = _go.GetComponent<PlayerCharacter>();
            ToonLook look = _go.GetComponent<ToonLook>();
            Assert.That(look, Is.Not.Null, "the copy carries the look her character added");
            int Place(Component c) => Array.IndexOf(_go.GetComponents<Component>(), c);
            for (int guard = 0; guard < 20 && (Place(look) < Place(her)) != lookFirst; guard++)
                UnityEditorInternal.ComponentUtility.MoveComponentUp(lookFirst ? look : (Component)her);
            string order = string.Join(", ", _go.GetComponents<Component>().Select(c => c.GetType().Name));
            Assert.That(Place(look) < Place(her), Is.EqualTo(lookFirst), "the order wanted: " + order);
            _go.SetActive(true); // an exception in an Awake fails the test as an unexpected log
            _go.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _go.GetComponent<PlayerHealth>().Untouchable = true;
            yield return null;
            yield return null;
            Assert.That(_go.GetComponents<ToonLook>().Length, Is.EqualTo(1), "one look, not a second added (" + order + ")");
            Assert.That(look.enabled && her.enabled, Is.True, order);
            Assert.That(look.Applied, Is.True, "put on (" + order + ")");
            Assert.That(look.TargetCount, Is.GreaterThan(0), "her model found (" + order + ")");
            foreach (Renderer r in ModelRenderers(her))
                foreach (Material mat in r.sharedMaterials)
                {
                    Assert.That(mat.shader.name, Is.EqualTo(ToonLook.ShaderName), r.name + " (" + order + ")");
                    Assert.That(look.Made, Has.Member(mat), r.name + ": its own copy (" + order + ")");
                }
#endif
        }
    }
}
