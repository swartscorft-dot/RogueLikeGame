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
    /// Work order 119, the enemies in the toon look (Kristian, 2026-11-25: "Samme på fiendene."), in the ControlRoom scene with
    /// every kind spawned through the practice room's intents: off from the start (<see cref="ToonLookValues.Enemies"/> false),
    /// nothing of any enemy is looked at or changed; on, every kind's own materials (the model's meshes, the lancer's lance, the
    /// warder's staff, an elite's) wear her shader, the same objects, with nothing made; off again, every one is today's value for
    /// value; the look's own switch (the pause menu's line) turns them off with her; what the game paints on them (the hit's flash,
    /// the daze's tint, the elite's tint, the caster's and the warder's glows, the ward) still lands on what is drawn; the ink
    /// line on an enemy is its width on the screen at the camera's nearest and farthest steps; a frame with the switch standing
    /// allocates nothing; and the look asked before its enemy has woken does nothing.
    /// </summary>
    public class EnemyToonLookTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>The ink line's measured width may be off its named width by this many pixels (as for hers, work order 115).</summary>
        const float InkTolerancePixels = 1f;
        /// <summary>A layer nothing in the scene is on: the enemy's model is put on it alone for the measure.</summary>
        const int MeasureLayer = 31;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        static readonly EnemyKind[] Kinds =
        {
            EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Bomber,
            EnemyKind.Warder, EnemyKind.Lancer, EnemyKind.Boss,
        };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        GameObject _go;
        readonly List<GameObject> _made = new List<GameObject>();

        IEnumerator Load(bool enemies)
        {
            ToonLookValues.On = true;
            ToonLookValues.Enemies = enemies;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Roguelike.Rules.HitChipsValues.On = true; // work order 127: back to its default
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            foreach (GameObject g in _made) if (g != null) Object.Destroy(g);
            _made.Clear();
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            if (_player != null && _player.GetComponent<PlayerHealth>() != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (RunHud.Current != null) Object.Destroy(RunHud.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>One of every kind (a swarmers' pack), a brute made an elite before its first frame, and the practice brute.</summary>
        List<Enemy> SpawnEveryKind(out Enemy elite)
        {
            var all = new List<Enemy>();
            foreach (EnemyKind kind in Kinds)
            {
                List<Enemy> made = _levels.SpawnPractice(kind);
                Assert.That(made.Count, Is.GreaterThanOrEqualTo(1), kind + " spawned");
                all.AddRange(made);
            }
            List<Enemy> e = _levels.SpawnPractice(EnemyKind.Brute);
            elite = e[0];
            LevelValues v = _levels.Values;
            EnemyFactory.MakeElite(elite, v.EliteHealthOf(EnemyKind.Brute), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
            all.Add(elite);
            EnemyBrute practice = Object.FindObjectsByType<EnemyBrute>(FindObjectsSortMode.None).FirstOrDefault(b => !_levels.Spawned.Contains(b));
            if (practice != null && practice.Look != null) all.Add(practice);
            foreach (Enemy x in all)
            {
                Assert.That(x.Look, Is.Not.Null, x.name + " wears a model");
                if (x.Body != null) x.Body.SetMaxHealth(1000000);
            }
            return all;
        }

        /// <summary>Every mesh renderer under an enemy and its materials, by renderer.</summary>
        static Dictionary<Renderer, Material[]> MaterialsOf(IEnumerable<Enemy> enemies) =>
            enemies.SelectMany(e => e.GetComponentsInChildren<Renderer>(true)).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer)
                   .Distinct().ToDictionary(r => r, r => r.sharedMaterials);

        static bool MadeForIt(Material m) =>
            m != null && (m.name.StartsWith(EnemyLook.MaterialPrefix, StringComparison.Ordinal) || m.name.StartsWith(EnemyFactory.OwnMaterialPrefix, StringComparison.Ordinal)
                          || m.name.StartsWith(EnemyLancer.MaterialPrefix, StringComparison.Ordinal) || m.name.StartsWith(WarderBody.MaterialPrefix, StringComparison.Ordinal));

        /// <summary>Everything a material holds, as text: its name, shader, queue, keywords and every property of its shader (as work order 115's).</summary>
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

        /// <summary>The fields of two fingerprints that differ, as "was -> now".</summary>
        static string Differ(string was, string now)
        {
            string[] a = was.Split('|'), b = now.Split('|');
            var d = new List<string>();
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "(none)", y = i < b.Length ? b[i] : "(none)";
                if (x != y) d.Add(x + " -> " + y);
            }
            return string.Join("; ", d);
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        static Dictionary<Material, string> Prints(Dictionary<Renderer, Material[]> materials) =>
            materials.Values.SelectMany(a => a).Where(m => m != null).Distinct().ToDictionary(m => m, Fingerprint);

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator Off_FromTheStart_NothingOfAnyEnemyIsTouched_EveryMaterialToday()
        {
            yield return Load(enemies: false);
            List<Enemy> all = SpawnEveryKind(out _);
            yield return null; // every enemy has had its first frame
            Time.timeScale = 0f; // held still, so that nothing the game does while it fights (a fuse's flash) changes a value meanwhile
            yield return null;
            Dictionary<Renderer, Material[]> first = MaterialsOf(all);
            Dictionary<Material, string> prints = Prints(first);
            Assert.That(prints.Keys.Count(MadeForIt), Is.GreaterThanOrEqualTo(all.Count), "every enemy's own materials are there");
            yield return Frames(10);
            Shader toon = Shader.Find(ToonLook.ShaderName);
            foreach (Enemy e in all)
            {
                Assert.That(e.Look.Toon, Is.Not.Null, e.name);
                Assert.That(e.Look.Toon.Shown, Is.False, e.name + ": not in the look");
                Assert.That(e.Look.Toon.Materials.Count, Is.EqualTo(0), e.name + ": not one of its materials even looked at");
                foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        Assert.That(m == null || m.shader != toon, Is.True, e.name + " " + r.name + ": nothing on her shader");
            }
            foreach (KeyValuePair<Renderer, Material[]> kv in first)
            {
                Material[] now = kv.Key.sharedMaterials;
                Assert.That(now.Length, Is.EqualTo(kv.Value.Length), kv.Key.name);
                for (int k = 0; k < now.Length; k++)
                {
                    Assert.That(now[k], Is.SameAs(kv.Value[k]), kv.Key.name + " slot " + k + ": the very material it had");
                    if (now[k] != null && MadeForIt(now[k])) Assert.That(now[k].shader.name, Is.EqualTo(PropFactory.ShaderName), kv.Key.name + ": today's Simple Lit");
                }
            }
            foreach (KeyValuePair<Material, string> kv in prints)
                if (kv.Key != null) Assert.That(Fingerprint(kv.Key), Is.EqualTo(kv.Value), kv.Key.name + ": every value as it was");
        }

        [UnityTest]
        public IEnumerator On_EveryKindsOwnMaterials_WearHerShader_TheSameObjects_AndOff_TheyAreTodays_ValueForValue()
        {
            yield return Load(enemies: false);
            List<Enemy> all = SpawnEveryKind(out Enemy elite);
            yield return Frames(2);
            // Held still, so that nothing the game does while it fights changes a value between the two snapshots.
            Time.timeScale = 0f;
            yield return null;
            Dictionary<Renderer, Material[]> before = MaterialsOf(all);
            Dictionary<Material, string> prints = Prints(before);
            Shader toon = Shader.Find(ToonLook.ShaderName);
            Assert.That(toon, Is.Not.Null);

            ToonLookValues.Enemies = true;
            yield return Frames(2);
            var kinds = new HashSet<EnemyKind>();
            var lines = new List<string>();
            int ours = 0;
            foreach (Enemy e in all)
            {
                Assert.That(e.Look.Toon.Shown, Is.True, e.name + ": in the look within a frame");
                foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true).Where(r => r is SkinnedMeshRenderer || r is MeshRenderer))
                {
                    if (!before.ContainsKey(r)) continue; // (nothing is made meanwhile: time stands)
                    Material[] now = r.sharedMaterials;
                    Assert.That(now.Length, Is.EqualTo(before[r].Length), r.name);
                    for (int k = 0; k < now.Length; k++)
                    {
                        Material m = now[k];
                        Assert.That(m, Is.SameAs(before[r][k]), e.name + " " + r.name + " slot " + k + ": the same material object, none made");
                        if (m == null) continue;
                        bool light = r.gameObject.name == WarderBody.LightName;
                        if (MadeForIt(m) && !light)
                        {
                            Assert.That(m.shader, Is.SameAs(toon), e.name + " " + r.name + ": on her shader");
                            ours++;
                            if (r == e.Look.Skin) kinds.Add(e.Kind);
                            lines.Add(e.LogWord + " " + r.name + ": " + m.name);
                        }
                        else Assert.That(m.shader, Is.Not.SameAs(toon), e.name + " " + r.name + " (" + m.name + "): not made for the enemy, or the ward's light: left as it was");
                    }
                }
            }
            TestContext.WriteLine("ENEMYTOON wearing it: " + string.Join("; ", lines));
            Assert.That(kinds, Is.SupersetOf(Kinds), "every kind's body");
            Assert.That(elite.BodyRenderer.sharedMaterial.shader, Is.SameAs(toon), "the elite's");
            EnemyLancer lancer = all.OfType<EnemyLancer>().First();
            Assert.That(lancer.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith(EnemyLancer.MaterialPrefix, StringComparison.Ordinal))
                              .All(r => r.sharedMaterial.shader == toon), Is.True, "the lancer's lance");
            EnemyWarder warder = all.OfType<EnemyWarder>().First();
            Assert.That(warder.ReadBody, Is.Not.Null, "the warder's own body (its staff), made in its Awake");
            if (warder.ReadBody.StaffRenderer != null) Assert.That(warder.ReadBody.StaffRenderer.sharedMaterial.shader, Is.SameAs(toon), "the warder's staff");
            if (warder.ReadBody.OrbRenderer != null) Assert.That(warder.ReadBody.OrbRenderer.sharedMaterial.shader.name, Is.EqualTo(PropFactory.ShaderName), "its light left a glow");
            // Their own texture, colour and glow, read by her shader from the very same material.
            Enemy caster = all.First(x => x.Kind == EnemyKind.Caster);
            Assert.That(EnemyLook.EmissionOf(caster.Look.Skin), Is.Not.Null, "the caster's glow map, its glow still on");
            foreach (Enemy e in all) Assert.That(EnemyLook.TextureOf(e.Look.Skin), Is.Not.Null, e.name + ": its painted texture");

            ToonLookValues.Enemies = false;
            yield return Frames(2);
            foreach (Enemy e in all) Assert.That(e.Look.Toon.Shown, Is.False, e.name);
            foreach (KeyValuePair<Renderer, Material[]> kv in before)
            {
                Material[] now = kv.Key.sharedMaterials;
                for (int k = 0; k < now.Length; k++)
                {
                    Assert.That(now[k], Is.SameAs(kv.Value[k]), kv.Key.name + " slot " + k + ": the very material it had");
                    if (now[k] != null) Assert.That(Fingerprint(now[k]), Is.EqualTo(prints[now[k]]), kv.Key.name + " slot " + k + ": every value as it was; differing: " + Differ(prints[now[k]], Fingerprint(now[k])));
                }
            }
            TestContext.WriteLine("ENEMYTOON " + ours + " materials on her shader and back, of " + all.Count + " enemies");

            // The look's own switch (the pause menu's line) takes them off with her, and gives them back with her.
            ToonLookValues.Enemies = true;
            yield return Frames(2);
            Assert.That(all.All(e => e.Look.Toon.Shown), Is.True);
            ToonLookValues.On = false;
            yield return Frames(2);
            Assert.That(all.All(e => !e.Look.Toon.Shown), Is.True, "the look off: the enemies too");
            Assert.That(elite.BodyRenderer.sharedMaterial.shader.name, Is.EqualTo(PropFactory.ShaderName));
            ToonLookValues.On = true;
            yield return Frames(2);
            Assert.That(all.All(e => e.Look.Toon.Shown), Is.True, "on again with her");
        }

        [UnityTest]
        public IEnumerator ThePauseMenusLine_TurnsTheEnemiesOffWithHer_AndOnAgain_UnderThePause()
        {
            yield return Load(enemies: true);
            RunHud hud = RunHud.Ensure();
            Enemy brute = _levels.SpawnPractice(EnemyKind.Brute)[0];
            yield return Frames(2);
            Assert.That(brute.Look.Toon.Shown, Is.True, "on by default");
            Assert.That(hud.Escape(), Is.True, "paused");
            yield return null;
            Assert.That(hud.PickMenuLine(RunHud.MenuLine.ToonLook), Is.True);
            Assert.That(ToonLookValues.On, Is.False);
            yield return Frames(2);
            Assert.That(brute.Look.Toon.Shown, Is.False, "off with her, under the pause");
            Assert.That(brute.Look.Skin.sharedMaterial.shader.name, Is.EqualTo(PropFactory.ShaderName));
            Assert.That(hud.PickMenuLine(RunHud.MenuLine.ToonLook), Is.True);
            yield return Frames(2);
            Assert.That(brute.Look.Toon.Shown, Is.True, "and on again");
            Assert.That(brute.Look.Skin.sharedMaterial.shader.name, Is.EqualTo(ToonLook.ShaderName));
            hud.Escape();
        }

        // ---------------- What the game paints on them ----------------

        [UnityTest]
        public IEnumerator WithTheLookOn_TheFlash_TheDaze_TheElitesTint_TheGlows_AndTheWard_StillLandOnWhatIsDrawn()
        {
            yield return Load(enemies: true);
            Shader toon = Shader.Find(ToonLook.ShaderName);
            Enemy guard = _levels.SpawnPractice(EnemyKind.Guard)[0];
            Enemy caster = _levels.SpawnPractice(EnemyKind.Caster)[0];
            Enemy elite = _levels.SpawnPractice(EnemyKind.Brute)[0];
            LevelValues v = _levels.Values;
            EnemyFactory.MakeElite(elite, v.EliteHealthOf(EnemyKind.Brute), v.EliteDamageScale, v.EliteSizeScale, v.EliteStaggerShare);
            foreach (Enemy e in new[] { guard, caster, elite }) e.Body.SetMaxHealth(1000000);
            yield return Frames(2);

            // The elite's tint, on its body as drawn.
            Assert.That(elite.BodyRenderer.sharedMaterial.shader, Is.SameAs(toon));
            Assert.That(Near(elite.BodyRenderer.sharedMaterial.color, EnemyFactory.EliteTint), Is.True, "the elite's tint: " + elite.BodyRenderer.sharedMaterial.color);

            // A hit's flash over every mesh of the model, and the daze's tint on its body, both on what is drawn.
            Roguelike.Rules.HitChipsValues.On = false; // work order 127: this pins the flash from before its order; on, a hit throws chips (HitChipsPlayTests)
            guard.Body.TakeHit(1, 1e6f);
            Assert.That(guard.IsDazed, Is.True);
            yield return null;
            Assert.That(guard.Look.Skin.sharedMaterial.shader, Is.SameAs(toon));
            Assert.That(guard.Look.Skin.sharedMaterial.color, Is.Not.EqualTo(Color.white), "tinted while dazed");
            Assert.That(guard.GetComponent<HitFlash>().IsFlashing, Is.True);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in guard.Look.Renderers)
            {
                r.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(EnemyLook.FlashColor), r.name + ": the flash brightens the model");
            }

            // The caster's glow, by its own map, still on.
            Assert.That(caster.Look.Skin.sharedMaterial.shader, Is.SameAs(toon));
            Assert.That(EnemyLook.EmissionOf(caster.Look.Skin), Is.Not.Null, "its glow map");
            Assert.That(caster.Look.Skin.sharedMaterial.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(0.01f), "its glow's colour");

            // A warder and its pack: its model's glows made green with the look on, its staff in the look, its light not, and a ward.
            _levels.ClearSpawned();
            yield return null;
            List<Enemy> pack = _levels.SpawnWarderPack();
            EnemyWarder warder = pack.OfType<EnemyWarder>().First();
            yield return Until(() => pack.Any(EnemyWarder.IsWarded), 8f);
            Assert.That(pack.Any(EnemyWarder.IsWarded), Is.True, "one of the pack warded");
            Assert.That(warder.ReadBody, Is.Not.Null);
            Assert.That(warder.ReadBody.GlowsMadeGreen, Is.GreaterThan(0), "its model's glows made green, on her shader");
            Assert.That(warder.Look.Skin.sharedMaterial.shader, Is.SameAs(toon));
            if (warder.ReadBody.StaffRenderer != null) Assert.That(warder.ReadBody.StaffRenderer.sharedMaterial.shader, Is.SameAs(toon), "its staff");
            if (warder.ReadBody.OrbRenderer != null) Assert.That(warder.ReadBody.OrbRenderer.sharedMaterial.shader, Is.Not.SameAs(toon), "its light, a glow, as it was");
        }

        // ---------------- The ink line ----------------

        Camera MeasureCamera(Vector3 focus, float distance, int mask)
        {
            FeelValues feel = _player.Feel ?? new FeelValues();
            var go = new GameObject("EnemyToonMeasureCamera");
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
            cam.transform.SetPositionAndRotation(new Vector3(focus.x, 0f, focus.z) - rotation * Vector3.forward * distance, rotation);
            return cam;
        }

        static bool[] Mask(Camera cam, int w, int h)
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
                Color32[] px = tex.GetPixels32();
                var m = new bool[px.Length];
                for (int i = 0; i < px.Length; i++) m[i] = px[i].r > 40 || px[i].g < 215 || px[i].b > 40; // anything that is not the green
                return m;
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
        }

        /// <summary>How far the ink line widens the silhouette, in pixels, along every row and column that crosses it (as work order 115 measures hers).</summary>
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
        public IEnumerator TheInkLineOnAnEnemy_IsItsWidthOnTheScreen_AtTheCamerasNearestAndFarthestSteps()
        {
            yield return Load(enemies: false);
            Enemy brute = _levels.SpawnPractice(EnemyKind.Brute)[0];
            brute.enabled = false; // it stands as it is: the two pictures of a frame are of one pose
            yield return Frames(2);
            FeelValues feel = _player.Feel ?? new FeelValues();
            float nearest = CameraDistanceSteps.Closer.Min(), farthest = Mathf.Max(feel.CameraDistance, CameraDistanceSteps.Closer.Max());
            var layers = brute.Look.Renderers.ToDictionary(r => r, r => r.gameObject.layer);
            const int W = 1920, H = 1080;
            float want = ToonLookValues.Live.InkPixels(H);
            try
            {
                foreach (Renderer r in brute.Look.Renderers) r.gameObject.layer = MeasureLayer;
                foreach (float d in new[] { nearest, farthest })
                {
                    Camera cam = MeasureCamera(brute.transform.position, d, 1 << MeasureLayer);
                    ToonLookValues.Enemies = false;
                    brute.Look.Toon.Tick();
                    bool[] off = Mask(cam, W, H);
                    ToonLookValues.Enemies = true;
                    brute.Look.Toon.Tick();
                    Assert.That(brute.Look.Toon.Shown, Is.True);
                    bool[] on = Mask(cam, W, H);
                    float width = Widening(off, on, W, H, out int samples, out float quartile);
                    int tall = 0;
                    for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (off[y * W + x]) { tall++; break; }
                    string line = string.Format(Inv, "ENEMYTOON ink on a brute at {0:0} m: {1:0.0} px measured (median of {2} edge samples; lower quartile {3:0.0}), {4:0.00} px wanted at {5} rows; it is {6} px tall",
                                                d, width, samples, quartile, want, H, tall);
                    TestContext.WriteLine(line);
                    Debug.Log(line);
                    Assert.That(samples, Is.GreaterThan(200), "the brute is in the picture at " + d + " m");
                    Assert.That(width, Is.EqualTo(want).Within(InkTolerancePixels), line);
                }
            }
            finally
            {
                foreach (KeyValuePair<Renderer, int> kv in layers) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;
            }
        }

        // ---------------- Every frame, and before it has woken ----------------

        [UnityTest]
        public IEnumerator AFrameWithTheSwitchStanding_AllocatesNothing()
        {
            yield return Load(enemies: true);
            Enemy brute = _levels.SpawnPractice(EnemyKind.Brute)[0];
            yield return Frames(2);
            EnemyToonLook look = brute.Look.Toon;
            look.Tick();
            Assert.That(look.Shown, Is.True);
            Assert.That(() => look.Tick(), NotAllocating, "on, standing");
            ToonLookValues.Enemies = false;
            look.Tick();
            Assert.That(look.Shown, Is.False);
            Assert.That(() => look.Tick(), NotAllocating, "off, standing");
            ToonLookValues.Enemies = true;
            ToonLookValues.On = false;
            look.Tick();
            Assert.That(() => look.Tick(), NotAllocating, "the look off, standing");
        }

        [Test]
        public void TheLook_AskedBeforeItsEnemyHasWoken_OrWithoutOne_DoesNothing_AndThrowsNothing()
        {
            var none = new EnemyToonLook(null);
            none.Tick();
            Assert.That(none.Shown, Is.False);
            Assert.That(none.Materials.Count, Is.EqualTo(0));
            _go = new GameObject("EnemyToonLookBeforeAwake");
            _go.SetActive(false); // nothing on it wakes
            var empty = new EnemyToonLook(_go.transform);
            Assert.That(empty.Shown, Is.False);
            empty.Tick(); // nothing under it: finds nothing, changes nothing, throws nothing
            Assert.That(empty.Materials.Count, Is.EqualTo(0));
        }
    }
}
