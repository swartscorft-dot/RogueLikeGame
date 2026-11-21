using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The rooms' mood on a generated floor (work order 044; Kristian's words and choice of 2026-11-20: "The rooms must also
    /// feel more alive and create atmosphere like Diablo and Path of Exile", and of the five concept pictures in
    /// docs/art/concepts/rooms, "build toward them"). With <see cref="LevelValues.RoomMood"/> on it sits on the level's root;
    /// off, it is not there and the floor is today's. What goes where is <see cref="RoomMoodPlan"/>; its numbers are
    /// <see cref="MoodValues"/>, read live while playing (the floor's values are GeneratedLevels' own), and a change to a
    /// switch or to a number that shapes what is built builds the mood again.
    ///
    /// Everything it makes is under its own root object beside the level ("RoomMood"), so the level's own counts are as they
    /// were; that root is shown and hidden with the level, and destroyed with it. Nothing has a collider; everything is on
    /// <see cref="CryptKit.Layer"/>. Its frame allocates nothing.
    ///
    /// 1. Light and darkness (<see cref="MoodValues.Darkness"/>): while a floor is up, the scene's ambient light and its key
    /// light are turned down to <see cref="MoodValues.AmbientLevel"/> and <see cref="MoodValues.KeyLevel"/> and a little cold,
    /// and put back as they were when the floor goes (the practice room keeps today's light). The sources
    /// (<see cref="MoodValues.Sources"/>): a soft pool of added light on the floor under each candle, brazier, basin and the
    /// vault's gold, a flame on each candle and brazier, all flickering slowly by a few percent, the pools and flames of her
    /// room and the next one; and a real light at the first sources of her room as far as the kit's budget goes
    /// (<see cref="CryptKitValues.LightsPerRoom"/>, less the room's lantern lights when those are on). A faint cold pool over
    /// each room's middle keeps the middle from being the darkest part. Her own soft light goes with her
    /// (<see cref="MoodValues.HerLight"/>). The fight stays readable: every enemy of the floor is drawn once more with its own
    /// texture, added at <see cref="MoodValues.EnemyLightFloor"/>, a floor on how dark it can be (its hit flash brightens that
    /// too); shots, sparks and the ground's warnings are unlit and as bright as ever.
    ///
    /// 2. Air: low fog along the walls of her room and the next (<see cref="MoodValues.Fog"/>), dust motes round her
    /// (<see cref="MoodValues.Dust"/>), embers rising from the braziers of her room and the next (<see cref="MoodValues.Embers"/>),
    /// each with a hard cap on its particles.
    ///
    /// 3. A worn floor (<see cref="MoodValues.Wear"/>): rubble, bones and skulls along the walls and in the corners, faceted and
    /// flat-shaded in the kit's stone colours; stains, cracks, missing tiles, wax pools and patches of lighter and darker stone
    /// flat on the floor.
    ///
    /// 4. The eye-catchers (<see cref="MoodValues.Compose"/>): the dais, the carpet, the coin heaps and the candles of the
    /// plan, and an empty object, "LargePropPlace", where a large prop is to stand later.
    ///
    /// 5. A room tone (<see cref="MoodValues.Tone"/>): one quiet loop (<see cref="RoomMoodPlan.RoomTone"/>), following the
    /// placeholder sounds' switch and master volume (<see cref="PlaceholderSounds"/>) and the game's volume.
    /// </summary>
    public sealed class RoomMood : MonoBehaviour
    {
        public const string RootName = "RoomMood";
        public const string LargePropPlaceName = "LargePropPlace";

        /// <summary>One room's parts.</summary>
        public sealed class RoomParts
        {
            public int Order;
            public GameObject Root;
            public MeshRenderer Lit, Decals, Glow;
            public Mesh GlowMesh;
            public Color[] GlowBase, GlowNow;
            public int[] VertexSource;
            public float[] Factor;
            public readonly List<MoodSource> Sources = new List<MoodSource>();
            public readonly List<Light> Lights = new List<Light>();
            public readonly List<int> LightSource = new List<int>();
            public ParticleSystem Fog;
            public readonly List<ParticleSystem> Embers = new List<ParticleSystem>();
            public Transform LargePropPlace;
            public bool AirOn;
        }

        sealed class Floored
        {
            public Renderer Renderer;
            public Material[] Before;
        }

        GeneratedLevel _level;
        LevelValues _values;
        GameObject _root;
        RoomMoodPlan _plan;
        readonly List<RoomParts> _rooms = new List<RoomParts>();
        readonly List<Object> _owned = new List<Object>();
        readonly List<Floored> _floored = new List<Floored>();
        readonly Dictionary<(Texture, Color), Material> _overlays = new Dictionary<(Texture, Color), Material>();
        Light _herLight;
        ParticleSystem _dust;
        AudioSource _tone;
        Material _particleMaterial;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;
        bool _built, _claimed;
        int _signature;
        float _overlayLevel = -1f;
        float _fogLevel = -1f, _dustLevel = -1f;
        MaterialPropertyBlock _block;

        static AudioClip s_tone;

        /// <summary>The plan built from, or null with the master off.</summary>
        public RoomMoodPlan Plan => _plan;
        /// <summary>The object everything is under.</summary>
        public GameObject Root => _root;
        public IReadOnlyList<RoomParts> Rooms => _rooms;
        public Light HerLight => _herLight;
        public ParticleSystem Dust => _dust;
        public AudioSource Tone => _tone;
        /// <summary>The room she was in at the last tick, by its place in the run; -1 for none.</summary>
        public int Here { get; private set; } = -1;
        /// <summary>Enemy renderers drawn with the light floor.</summary>
        public int FlooredRenderers => _floored.Count;
        public bool Built => _built;
        /// <summary>For tests: how many times it was built.</summary>
        public int Builds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_tone = null;
            s_claims = 0;
            s_saved = false;
            s_key = null;
        }

        /// <summary>The mood of a generated floor, on its root, built at once.</summary>
        public static RoomMood Add(GeneratedLevel level)
        {
            var mood = level.gameObject.AddComponent<RoomMood>();
            mood._level = level;
            mood._values = level.Values;
            mood.BuildAll();
            level.EnemyCalled += mood.OnEnemyCalled;
            return mood;
        }

        void OnEnemyCalled(Enemy e)
        {
            if (_built) FloorEnemy(e);
        }

        MoodValues V => _values?.Mood ?? s_defaults;
        static readonly MoodValues s_defaults = new MoodValues();

        // ---------------- The scene's light (shared by every floor's mood: the first takes it down, the last puts it back) ----------------

        static int s_claims;
        static bool s_saved;
        static SphericalHarmonicsL2 s_probe;
        static AmbientMode s_mode;
        static Color s_ambient, s_sky, s_equator, s_ground;
        static Light s_key;
        static float s_keyIntensity;
        static Color s_keyColour;
        static float s_appliedAmbient = -1f, s_appliedKey = -1f;
        static Rgb s_appliedTint;

        /// <summary>The scene's light as it was before any floor took it down; for tests.</summary>
        public static bool SceneLightTaken => s_saved;

        static Light FindKey()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            Light best = null;
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (best == null || l.intensity > best.intensity)) best = l;
            return best;
        }

        void Claim()
        {
            if (_claimed) return;
            _claimed = true;
            if (s_claims++ == 0)
            {
                s_probe = RenderSettings.ambientProbe;
                s_mode = RenderSettings.ambientMode;
                s_ambient = RenderSettings.ambientLight;
                s_sky = RenderSettings.ambientSkyColor;
                s_equator = RenderSettings.ambientEquatorColor;
                s_ground = RenderSettings.ambientGroundColor;
                s_key = FindKey();
                if (s_key != null)
                {
                    s_keyIntensity = s_key.intensity;
                    s_keyColour = s_key.color;
                }
                s_saved = true;
                s_appliedAmbient = s_appliedKey = -1f;
            }
        }

        void Release()
        {
            if (!_claimed) return;
            _claimed = false;
            if (--s_claims > 0 || !s_saved) return;
            s_claims = 0;
            if (s_mode != AmbientMode.Skybox)
            {
                RenderSettings.ambientMode = s_mode;
                RenderSettings.ambientLight = s_ambient;
                RenderSettings.ambientSkyColor = s_sky;
                RenderSettings.ambientEquatorColor = s_equator;
                RenderSettings.ambientGroundColor = s_ground;
            }
            RenderSettings.ambientProbe = s_probe;
            if (s_key != null)
            {
                s_key.intensity = s_keyIntensity;
                s_key.color = s_keyColour;
            }
            s_saved = false;
            s_key = null;
        }

        /// <summary>The darkness at its numbers now; the engine is told only when they changed.</summary>
        void ApplyDark()
        {
            MoodValues v = V;
            if (!s_saved) return;
            if (Mathf.Approximately(s_appliedAmbient, v.AmbientLevel) && Mathf.Approximately(s_appliedKey, v.KeyLevel)
                && s_appliedTint.R == v.DarkTint.R && s_appliedTint.G == v.DarkTint.G && s_appliedTint.B == v.DarkTint.B) return;
            s_appliedAmbient = v.AmbientLevel;
            s_appliedKey = v.KeyLevel;
            s_appliedTint = v.DarkTint;
            float a = Mathf.Max(0f, v.AmbientLevel), k = Mathf.Max(0f, v.KeyLevel);
            var tint = new Color(v.DarkTint.R, v.DarkTint.G, v.DarkTint.B, 1f);
            SphericalHarmonicsL2 probe = s_probe;
            for (int c = 0; c < 9; c++)
            {
                probe[0, c] *= a * tint.r;
                probe[1, c] *= a * tint.g;
                probe[2, c] *= a * tint.b;
            }
            if (s_mode != AmbientMode.Skybox)
            {
                Color Scaled(Color x) => new Color(x.r * a * tint.r, x.g * a * tint.g, x.b * a * tint.b, x.a);
                RenderSettings.ambientLight = Scaled(s_ambient);
                RenderSettings.ambientSkyColor = Scaled(s_sky);
                RenderSettings.ambientEquatorColor = Scaled(s_equator);
                RenderSettings.ambientGroundColor = Scaled(s_ground);
            }
            RenderSettings.ambientProbe = probe;
            if (s_key != null)
            {
                s_key.intensity = s_keyIntensity * k;
                s_key.color = new Color(s_keyColour.r * tint.r, s_keyColour.g * tint.g, s_keyColour.b * tint.b, s_keyColour.a);
            }
        }

        // ---------------- Building ----------------

        /// <summary>What a change to rebuilds the mood: every switch, and the numbers that shape what is built.</summary>
        int Signature()
        {
            MoodValues v = V;
            unchecked
            {
                int h = 17;
                void B(bool b) => h = h * 31 + (b ? 1 : 2);
                void F(float f) => h = h * 31 + f.GetHashCode();
                void I(int i) => h = h * 31 + i;
                B(v.Darkness); B(v.Sources); B(v.HerLight); B(v.Fog); B(v.Dust); B(v.Embers); B(v.Wear); B(v.Compose); B(v.Tone);
                B(v.EnemyLightFloor > 0f);
                F(v.CandlePool); F(v.CandleReach); F(v.BrazierPool); F(v.BrazierReach); F(v.GoldPool); F(v.GoldReach); F(v.FlameGlow);
                F(v.MiddleFill); F(v.MiddleFillShare); F(v.WearDensity); F(v.MiddleClear); F(v.DoorClear); F(v.PlaceClear); F(v.TintVariation);
                F(v.DaisMargin); F(v.DaisHeight); F(v.CarpetWidth); F(v.FogSize); F(v.FogHeight);
                I(v.FogMostPerRoom); I(v.DustMost); I(v.EmbersPerBrazier);
                F(v.CandleColour.R + v.CandleColour.G * 3f + v.CandleColour.B * 7f); F(v.Teal.R + v.Teal.G * 3f + v.Teal.B * 7f);
                F(v.GoldColour.R + v.GoldColour.G * 3f + v.GoldColour.B * 7f); F(v.FogColour.R + v.FogColour.G * 3f + v.FogColour.B * 7f);
                return h;
            }
        }

        void BuildAll()
        {
            if (_level == null || _values == null) return;
            MoodValues v = V;
            _signature = Signature();
            _built = true;
            Builds++;
            _block = _block ?? new MaterialPropertyBlock();
            _root = new GameObject(RootName + "_" + _level.Layout.SeedText) { layer = CryptKit.Layer };
            SceneManager.MoveGameObjectToScene(_root, _level.gameObject.scene);
            _root.SetActive(_level.gameObject.activeInHierarchy);
            _plan = RoomMoodPlan.Make(_level.Layout, _values);

            Texture2D palette = Own(MoodShapes.MakePalette());
            Material lit = Own(MoodShapes.MakeLit(palette));
            Material decal = Own(MoodShapes.MakeUnlit("Mood_Decals", MoodShapes.Blend.Multiply2x, null, 3000));
            Material glow = Own(MoodShapes.MakeUnlit("Mood_Glow", MoodShapes.Blend.Add, null, 3001));
            Texture2D spot = Own(MoodShapes.MakeSoftSpot());
            _particleMaterial = Own(MoodShapes.MakeUnlit("Mood_Air", MoodShapes.Blend.Add, spot, 3002));

            for (int i = 0; i < _plan.Rooms.Count; i++) _rooms.Add(BuildRoom(_plan.Rooms[i], lit, decal, glow));

            if (v.Darkness) Claim();
            if (v.HerLight)
            {
                var go = new GameObject("Mood_HerLight") { layer = CryptKit.Layer };
                go.transform.SetParent(_root.transform, false);
                _herLight = go.AddComponent<Light>();
                _herLight.type = LightType.Point;
                _herLight.shadows = LightShadows.None;
            }
            if (v.Dust)
            {
                _dust = MakeSystem("Mood_Dust", _root.transform, Vector3.zero, Mathf.Max(0, v.DustMost));
                ParticleSystem.MainModule main = _dust.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                ParticleSystem.EmissionModule emission = _dust.emission;
                emission.rateOverTime = Mathf.Max(0, v.DustMost) / 7.5f;
                ParticleSystem.ShapeModule shape = _dust.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(9f, 2f, 9f);
                shape.position = new Vector3(0f, 1.4f, 0f);
                ParticleSystem.NoiseModule noise = _dust.noise;
                noise.enabled = true;
                noise.strength = 0.08f;
                noise.frequency = 0.3f;
                FadeInOut(_dust);
                _dustLevel = -1f;
                _dust.Play();
            }
            if (v.Tone)
            {
                if (s_tone == null)
                {
                    float[] samples = Synth.Render(RoomMoodPlan.RoomTone());
                    s_tone = AudioClip.Create("Mood_RoomTone", samples.Length, 1, Synth.SampleRate, false);
                    s_tone.SetData(samples, 0);
                }
                _tone = _root.AddComponent<AudioSource>();
                _tone.clip = s_tone;
                _tone.loop = true;
                _tone.spatialBlend = 0f;
                _tone.playOnAwake = false;
                _tone.volume = 0f;
                if (_root.activeInHierarchy) _tone.Play();
            }
            if (v.EnemyLightFloor > 0f)
            {
                _overlayLevel = -1f;
                foreach (Enemy e in _level.Enemies) FloorEnemy(e);
            }
            Here = -2;
            _fogLevel = -1f;
        }

        T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        void Teardown()
        {
            foreach (Floored f in _floored)
                if (f.Renderer != null) f.Renderer.sharedMaterials = f.Before;
            _floored.Clear();
            _overlays.Clear();
            foreach (Object o in _owned)
                if (o != null) Destroy(o);
            _owned.Clear();
            _rooms.Clear();
            if (_root != null)
            {
                _root.SetActive(false);
                Destroy(_root);
            }
            _root = null;
            _herLight = null;
            _dust = null;
            _tone = null;
            _plan = null;
            _built = false;
            Release();
        }

        static Vector3 Up(float y) => new Vector3(0f, y, 0f);

        Vector3 W(LevelPoint p, float y = 0f) => _level.ToWorld(p) + Up(y);

        static Color KindColour(MoodValues v, MoodSourceKind k) =>
            k == MoodSourceKind.Candle ? MoodShapes.Linear(v.CandleColour, 1f) : k == MoodSourceKind.Gold ? MoodShapes.Linear(v.GoldColour, 1f) : MoodShapes.Linear(v.Teal, 1f);

        static float PoolOf(MoodValues v, MoodSourceKind k) => k == MoodSourceKind.Candle ? v.CandlePool : k == MoodSourceKind.Gold ? v.GoldPool : v.BrazierPool;
        static float ReachOf(MoodValues v, MoodSourceKind k) => k == MoodSourceKind.Candle ? v.CandleReach : k == MoodSourceKind.Gold ? v.GoldReach : v.BrazierReach;

        /// <summary>A set of candles' candles, from its number: where each stands from the middle and how tall it is.</summary>
        static int CandleLayout(uint variant, Vector3[] offsets, float[] heights)
        {
            uint s = variant | 1u;
            float J()
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return (s & 0xFFFF) / 65535f;
            }
            int n = 3 + (int)(J() * 2.99f);
            for (int i = 0; i < n; i++)
            {
                float a = (i * 137.5f + J() * 40f) * Mathf.Deg2Rad, r = i == 0 ? 0f : 0.07f + 0.06f * J();
                offsets[i] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                heights[i] = 0.1f + 0.2f * J() * (i == 0 ? 1.2f : 0.8f);
            }
            return n;
        }

        static readonly Vector3[] s_offsets = new Vector3[8];
        static readonly float[] s_heights = new float[8];

        RoomParts BuildRoom(MoodRoom mr, Material lit, Material decal, Material glow)
        {
            MoodValues v = V;
            LevelRoom room = mr.Room;
            var parts = new RoomParts { Order = room.Order };
            parts.Root = new GameObject("Mood_" + room.Letter + room.Order) { layer = CryptKit.Layer };
            parts.Root.transform.SetParent(_root.transform, false);
            parts.Sources.AddRange(v.Sources ? mr.Sources : new List<MoodSource>());

            var litB = new MoodShapes.Build();
            var decB = new MoodShapes.Build();
            var glowB = new MoodShapes.Build();
            var vertexSource = new List<int>();
            void Tag(int count, int source)
            {
                for (int i = 0; i < count; i++) vertexSource.Add(source);
            }

            // The middle never the darkest part.
            if (v.Darkness && v.MiddleFill > 0f)
            {
                float r = v.MiddleFillShare * Mathf.Min(room.Rect.Width, room.Rect.Height) * _level.Layout.CellSize;
                MoodShapes.Pool(glowB, W(room.Middle(_level.Layout.CellSize), 0.028f), r, MoodShapes.Linear(new Rgb(0.55f, 0.62f, 0.78f), v.MiddleFill), 20, out _, out int n);
                Tag(n, -1);
            }

            // Wear.
            if (v.Wear)
                foreach (WearPiece w in mr.Wear)
                {
                    uint seed = w.Variant | 1u;
                    Vector3 at = W(w.At);
                    switch (w.Kind)
                    {
                        case WearKind.Rubble:
                        {
                            int chunks = 3 + (int)(seed % 3u);
                            for (int c = 0; c < chunks; c++)
                            {
                                float a = (w.Turn + c * 97f) * Mathf.Deg2Rad, d = (float)w.Size * 0.45f * ((c * 0.37f) % 1f);
                                MoodShapes.Chunk(litB, at + new Vector3(Mathf.Sin(a) * d, 0f, Mathf.Cos(a) * d), Mathf.Lerp(0.14f, 0.36f, ((c * 0.61f) + 0.2f) % 1f),
                                                 w.Turn + c * 53f, ref seed, c % 2 == 0 ? MoodShapes.Paint.StoneDark : MoodShapes.Paint.StoneLight);
                            }
                            break;
                        }
                        case WearKind.Bone: MoodShapes.Bone(litB, at, (float)w.Size, w.Turn, ref seed); break;
                        case WearKind.Skull: MoodShapes.Chunk(litB, at, 0.22f, w.Turn, ref seed, MoodShapes.Paint.Bone); break;
                        case WearKind.Wax: MoodShapes.FlatBlob(litB, at + Up(0.008f), (float)w.Size, 7, ref seed, MoodShapes.Paint.Wax); break;
                        case WearKind.Stain: MoodShapes.Blob(decB, at + Up(0.012f), (float)w.Size, MoodShapes.Mul(0.3f + 0.1f * ((seed >> 4) % 100u) / 100f), ref seed); break;
                        case WearKind.Crack: MoodShapes.Crack(decB, at + Up(0.012f), (float)w.Size, w.Turn, ref seed); break;
                        case WearKind.Patch:
                        {
                            float t = v.TintVariation * ((seed & 1u) == 0 ? -1f : 1f) * (0.6f + 0.4f * ((seed >> 8) % 100u) / 100f);
                            MoodShapes.Blob(decB, at + Up(0.012f), (float)w.Size, MoodShapes.Mul(0.5f * (1f + t)), ref seed, 12);
                            break;
                        }
                        case WearKind.Hole:
                            MoodShapes.Blob(decB, at + Up(0.012f), (float)w.Size, MoodShapes.Mul(0.18f), ref seed);
                            for (int c = 0; c < 3; c++)
                                MoodShapes.Chunk(litB, at + new Vector3((c - 1) * (float)w.Size * 0.4f, -0.04f, ((c * 0.7f) % 1f - 0.5f) * (float)w.Size * 0.5f), 0.2f, w.Turn + c * 70f, ref seed, MoodShapes.Paint.StoneDark);
                            break;
                    }
                }

            // The eye-catcher.
            if (v.Compose)
            {
                foreach (CompositionPiece c in mr.Pieces)
                {
                    uint seed = c.Variant | 1u;
                    Vector3 at = W(c.At);
                    float h = Mathf.Max(0.02f, v.DaisHeight);
                    switch (c.Kind)
                    {
                        case CompositionKind.Dais:
                            MoodShapes.Slab(litB, at, (float)c.Width, (float)c.Depth, h * 0.5f, c.Turn, MoodShapes.Paint.DaisStone, MoodShapes.Paint.StoneLight);
                            MoodShapes.Slab(litB, at, Mathf.Max(0.1f, (float)c.Width - 0.4f), Mathf.Max(0.1f, (float)c.Depth - 0.4f), h, c.Turn, MoodShapes.Paint.DaisStone, MoodShapes.Paint.DaisStone);
                            break;
                        case CompositionKind.RoundDais:
                            MoodShapes.Prism(litB, at, (float)c.Width, h * 0.5f, 10, c.Turn, MoodShapes.Paint.DaisStone, MoodShapes.Paint.StoneLight);
                            MoodShapes.Prism(litB, at, Mathf.Max(0.1f, (float)c.Width - 0.2f), h, 10, c.Turn + 18f, MoodShapes.Paint.DaisStone, MoodShapes.Paint.DaisStone);
                            break;
                        case CompositionKind.Carpet:
                            MoodShapes.Carpet(litB, at + Up(0.015f), W(c.To, 0.015f), (float)c.Width, ref seed);
                            break;
                        case CompositionKind.CoinHeap:
                            MoodShapes.Cone(litB, at + Up(h), (float)c.Width, (float)c.Depth, 7, c.Turn, MoodShapes.Paint.Gold);
                            break;
                        case CompositionKind.Coins:
                            for (int k = 0; k < 22; k++)
                            {
                                float a = (k * 47.3f + (seed % 360u)) * Mathf.Deg2Rad, r = (float)c.Width - 0.5f * (((k * 0.618f) % 1f));
                                MoodShapes.FlatBlob(litB, at + new Vector3(Mathf.Sin(a) * r, 0.01f, Mathf.Cos(a) * r), 0.05f, 6, ref seed, MoodShapes.Paint.Gold);
                            }
                            break;
                        case CompositionKind.Candles:
                        {
                            Vector3 foot = at + Up((float)c.Height);
                            MoodShapes.FlatBlob(litB, foot + Up(0.008f), 0.2f, 8, ref seed, MoodShapes.Paint.Wax);
                            int n = CandleLayout(c.Variant, s_offsets, s_heights);
                            for (int k = 0; k < n; k++) MoodShapes.Prism(litB, foot + s_offsets[k], 0.035f, s_heights[k], 6, k * 20f, MoodShapes.Paint.Wax, MoodShapes.Paint.Wax);
                            break;
                        }
                    }
                }
                if (mr.LargePropPlace.HasValue)
                {
                    var place = new GameObject(LargePropPlaceName + "_" + RoomThemes.Word(room.Theme).Replace(' ', '_')) { layer = CryptKit.Layer };
                    place.transform.SetParent(parts.Root.transform, false);
                    place.transform.SetPositionAndRotation(W(mr.LargePropPlace.Value), Quaternion.Euler(0f, mr.LargePropTurn, 0f));
                    parts.LargePropPlace = place.transform;
                }
            }

            // Sources: pools and flames.
            for (int i = 0; i < parts.Sources.Count; i++)
            {
                MoodSource s = parts.Sources[i];
                Color colour = KindColour(v, s.Kind);
                MoodShapes.Pool(glowB, W(s.At, 0.03f), ReachOf(v, s.Kind), colour * PoolOf(v, s.Kind), 16, out _, out int n);
                Tag(n, i);
                if (!s.Flame) continue;
                Color core = colour * v.FlameGlow, tip = colour * (v.FlameGlow * 0.35f);
                if (s.Kind == MoodSourceKind.Brazier)
                {
                    MoodShapes.Flame(glowB, W(s.At, (float)s.Height), 0.55f, 0.18f, core, tip, out _, out n);
                    Tag(n, i);
                    continue;
                }
                CompositionPiece set = null;
                foreach (CompositionPiece c in mr.Pieces)
                    if (c.Kind == CompositionKind.Candles && c.At.DistanceTo(s.At) < 1e-3) set = c;
                if (set == null) continue;
                int count = CandleLayout(set.Variant, s_offsets, s_heights);
                for (int k = 0; k < count; k++)
                {
                    MoodShapes.Flame(glowB, W(set.At, (float)set.Height + s_heights[k] + 0.01f) + s_offsets[k], 0.09f, 0.025f, core, tip, out _, out n);
                    Tag(n, i);
                }
            }

            if (litB.Count > 0) parts.Lit = MakeRenderer(parts.Root, "Mood_Lit", litB.ToMesh("Mood_Lit_" + room.Order, true), lit, true);
            if (decB.Count > 0) parts.Decals = MakeRenderer(parts.Root, "Mood_Decals", decB.ToMesh("Mood_Decals_" + room.Order, false), decal, false);
            if (glowB.Count > 0)
            {
                parts.GlowMesh = glowB.ToMesh("Mood_Glow_" + room.Order, false);
                parts.GlowMesh.MarkDynamic();
                parts.Glow = MakeRenderer(parts.Root, "Mood_Glow", parts.GlowMesh, glow, false);
                parts.GlowBase = glowB.C.ToArray();
                parts.GlowNow = glowB.C.ToArray();
                parts.VertexSource = vertexSource.ToArray();
            }
            parts.Factor = new float[parts.Sources.Count];
            for (int i = 0; i < parts.Factor.Length; i++) parts.Factor[i] = 1f;

            // Real lights, as far as the kit's budget goes in this room.
            int lanterns = 0;
            if (_level.KitLights != null && _level.KitLights.ByRoom.TryGetValue(room.Order, out List<Light> had)) lanterns = had.Count;
            int perRoom = (_values.Crypt ?? new CryptKitValues()).LightsPerRoom;
            int slots = Mathf.Max(0, perRoom - lanterns);
            for (int i = 0; i < parts.Sources.Count && parts.Lights.Count < slots; i++)
            {
                MoodSource s = parts.Sources[i];
                var go = new GameObject("Mood_Light_" + room.Order + "_" + parts.Lights.Count) { layer = CryptKit.Layer };
                go.transform.SetParent(parts.Root.transform, false);
                go.transform.position = W(s.At, (float)s.Height + 0.35f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.color = new Color(s.Kind == MoodSourceKind.Candle ? v.CandleColour.R : s.Kind == MoodSourceKind.Gold ? v.GoldColour.R : v.Teal.R,
                                    s.Kind == MoodSourceKind.Candle ? v.CandleColour.G : s.Kind == MoodSourceKind.Gold ? v.GoldColour.G : v.Teal.G,
                                    s.Kind == MoodSourceKind.Candle ? v.CandleColour.B : s.Kind == MoodSourceKind.Gold ? v.GoldColour.B : v.Teal.B, 1f);
                l.enabled = false;
                parts.Lights.Add(l);
                parts.LightSource.Add(i);
            }

            // Air: the fog of the room, the embers of its braziers; playing only in her room and the next.
            float cs = _level.Layout.CellSize;
            if (v.Fog && v.FogMostPerRoom > 0)
            {
                float w = room.Rect.Width * cs, d = room.Rect.Height * cs;
                parts.Fog = MakeSystem("Mood_Fog", parts.Root.transform, W(room.Middle(cs), v.FogHeight), v.FogMostPerRoom);
                ParticleSystem.MainModule main = parts.Fog.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(v.FogSize * 0.7f, v.FogSize * 1.3f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.prewarm = true;
                ParticleSystem.EmissionModule emission = parts.Fog.emission;
                emission.rateOverTime = v.FogMostPerRoom / 11f;
                ParticleSystem.ShapeModule shape = parts.Fog.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.BoxEdge;
                shape.scale = new Vector3(Mathf.Max(1f, w - 1.6f), 0.01f, Mathf.Max(1f, d - 1.6f));
                shape.randomDirectionAmount = 1f;
                FadeInOut(parts.Fog);
                ParticleSystemRenderer r = parts.Fog.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }
            if (v.Embers && v.EmbersPerBrazier > 0)
                foreach (MoodSource s in mr.Sources)
                {
                    if (s.Kind != MoodSourceKind.Brazier || !s.Flame) continue;
                    ParticleSystem e = MakeSystem("Mood_Embers", parts.Root.transform, W(s.At, (float)s.Height + 0.1f), v.EmbersPerBrazier);
                    ParticleSystem.MainModule main = e.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                    main.startColor = new Color(1f, 0.55f, 0.2f, 1f);
                    main.gravityModifier = -0.05f;
                    ParticleSystem.EmissionModule emission = e.emission;
                    emission.rateOverTime = v.EmbersPerBrazier / 2.1f * 0.6f;
                    ParticleSystem.ShapeModule shape = e.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15f;
                    shape.radius = 0.25f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    ParticleSystem.NoiseModule noise = e.noise;
                    noise.enabled = true;
                    noise.strength = 0.3f;
                    noise.frequency = 0.8f;
                    FadeInOut(e);
                    parts.Embers.Add(e);
                }
            return parts;
        }

        MeshRenderer MakeRenderer(GameObject parent, string name, Mesh mesh, Material material, bool lit)
        {
            Own(mesh);
            var go = new GameObject(name) { layer = CryptKit.Layer };
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; // low and flat: they would only cost a shadow pass
            r.receiveShadows = lit;
            return r;
        }

        ParticleSystem MakeSystem(string name, Transform parent, Vector3 at, int most)
        {
            var go = new GameObject(name) { layer = CryptKit.Layer };
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.maxParticles = Mathf.Max(0, most);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _particleMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void FadeInOut(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white, 0.25f), new GradientColorKey(Color.white, 0.7f), new GradientColorKey(Color.black, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            col.color = g;
        }

        // ---------------- The fight readable ----------------

        /// <summary>
        /// Draws each of the enemy's own meshes once more, its texture (a capsule's colour) added at the light floor: however
        /// dark the room, it is never drawn darker than that. The material slot is put after its own and taken off again when
        /// the mood goes.
        /// </summary>
        void FloorEnemy(Enemy e)
        {
            if (e == null || V.EnemyLightFloor <= 0f) return;
            foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!r.gameObject.activeSelf) continue; // the capsule a model enemy no longer shows
                Material own = r.sharedMaterial;
                if (own == null) continue;
                if (!own.name.StartsWith(EnemyLook.MaterialPrefix, System.StringComparison.Ordinal) && !own.name.StartsWith(EnemyFactory.OwnMaterialPrefix, System.StringComparison.Ordinal)) continue;
                Texture tex = own.HasProperty("_BaseMap") ? own.GetTexture("_BaseMap") : own.mainTexture;
                Color tint = tex == null ? own.color : Color.white;
                if (!_overlays.TryGetValue((tex, tint), out Material overlay))
                {
                    overlay = Own(MoodShapes.MakeUnlit("Mood_EnemyFloor", MoodShapes.Blend.Add, tex, 3000));
                    overlay.SetFloat("_ZWrite", 0f);
                    overlay.SetFloat("_Cull", (float)CullMode.Back);
                    overlay.renderQueue = (int)RenderQueue.GeometryLast + 1;
                    _overlays[(tex, tint)] = overlay;
                    SetOverlay(overlay, tint, V.EnemyLightFloor);
                }
                Material[] before = r.sharedMaterials;
                var after = new Material[before.Length + 1];
                for (int i = 0; i < before.Length; i++) after[i] = before[i];
                after[before.Length] = overlay;
                r.sharedMaterials = after;
                _floored.Add(new Floored { Renderer = r, Before = before });
            }
        }

        static void SetOverlay(Material overlay, Color tint, float level)
        {
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color t = linear ? tint.linear : tint;
            var c = new Color(t.r * level, t.g * level, t.b * level, 1f);
            // SetColor takes a colour as picked and turns it into linear light itself.
            overlay.SetColor("_BaseColor", linear ? c.gamma : c);
        }

        // ---------------- Every frame ----------------

        void OnEnable()
        {
            if (_root != null) _root.SetActive(true);
            if (_built && V.Darkness) Claim();
            if (_tone != null && !_tone.isPlaying) _tone.Play();
        }

        void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
            Release();
        }

        void OnDestroy()
        {
            if (_level != null) _level.EnemyCalled -= OnEnemyCalled;
            Teardown();
        }

        void Update() => Tick();

        static float Wave(float t, float speed, float phase) =>
            0.6f * Mathf.Sin(t * speed * 6.2831853f + phase) + 0.4f * Mathf.Sin(t * speed * 1.73f * 6.2831853f + phase * 2.1f);

        /// <summary>
        /// Looks at where she is and at the numbers, and keeps every layer as they say: the darkness, her light, the real
        /// lights of her room, the air of her room and the next, the flicker, the tone's volume. Allocates nothing (but when a
        /// switch or a number that shapes the mood changed: then it builds it again). For tests too.
        /// </summary>
        public void Tick()
        {
            if (_level == null || _values == null) return;
            if (!_values.RoomMood)
            {
                if (_built) Teardown();
                return;
            }
            if (!_built || Signature() != _signature)
            {
                if (_built) Teardown();
                BuildAll();
            }
            MoodValues v = V;
            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            if (_claimed) ApplyDark();

            Vector3 her = _player != null ? _player.Position : Vector3.zero;
            LevelRoom room = _player != null ? _level.RoomAt(her) : null;
            int here = room != null ? room.Order : -1;

            if (_herLight != null)
            {
                _herLight.enabled = _player != null;
                _herLight.transform.position = her + Up(v.HerLightHeight);
                _herLight.intensity = v.HerLightIntensity;
                _herLight.range = v.HerLightRange;
                _herLight.color = new Color(v.HerLightColour.R, v.HerLightColour.G, v.HerLightColour.B, 1f);
            }
            if (_dust != null)
            {
                _dust.transform.position = her;
                if (!Mathf.Approximately(_dustLevel, v.DustLevel))
                {
                    _dustLevel = v.DustLevel;
                    ParticleSystem.MainModule main = _dust.main;
                    float d = Mathf.Clamp01(v.DustLevel);
                    main.startColor = new Color(0.62f * d, 0.56f * d, 0.45f * d, 1f);
                }
            }
            if (!Mathf.Approximately(_fogLevel, v.FogLevel))
            {
                _fogLevel = v.FogLevel;
                Color fog = new Color(v.FogColour.R * v.FogLevel, v.FogColour.G * v.FogLevel, v.FogColour.B * v.FogLevel, 1f);
                for (int i = 0; i < _rooms.Count; i++)
                    if (_rooms[i].Fog != null)
                    {
                        ParticleSystem.MainModule main = _rooms[i].Fog.main;
                        main.startColor = fog;
                    }
            }
            if (!Mathf.Approximately(_overlayLevel, v.EnemyLightFloor))
            {
                _overlayLevel = v.EnemyLightFloor;
                foreach (KeyValuePair<(Texture, Color), Material> pair in _overlays) SetOverlay(pair.Value, pair.Key.Item2, Mathf.Max(0f, v.EnemyLightFloor));
            }

            if (here != Here)
            {
                Here = here;
                for (int i = 0; i < _rooms.Count; i++)
                {
                    RoomParts p = _rooms[i];
                    for (int k = 0; k < p.Lights.Count; k++) p.Lights[k].enabled = p.Order == here;
                    bool air = here >= 0 && (p.Order == here || p.Order == here + 1);
                    if (air == p.AirOn) continue;
                    p.AirOn = air;
                    if (p.Fog != null)
                    {
                        if (air) p.Fog.Play();
                        else p.Fog.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                    for (int k = 0; k < p.Embers.Count; k++)
                    {
                        if (air) p.Embers[k].Play();
                        else p.Embers[k].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            // The flicker of her room and the next.
            float t = Time.time;
            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomParts p = _rooms[i];
                if (here < 0 || (p.Order != here && p.Order != here + 1)) continue;
                for (int s = 0; s < p.Sources.Count; s++)
                {
                    MoodSource src = p.Sources[s];
                    bool candle = src.Kind == MoodSourceKind.Candle;
                    float phase = s * 1.37f + p.Order * 0.53f;
                    p.Factor[s] = 1f + (candle ? v.CandleFlicker : v.BrazierFlicker) * Wave(t, candle ? v.CandleFlickerSpeed : v.BrazierFlickerSpeed, phase);
                }
                if (p.GlowMesh != null)
                {
                    float level = Mathf.Max(0f, v.PoolLevel);
                    for (int k = 0; k < p.GlowNow.Length; k++)
                    {
                        int src = p.VertexSource[k];
                        float f = src < 0 ? 1f : p.Factor[src] * level;
                        Color c = p.GlowBase[k];
                        p.GlowNow[k] = new Color(c.r * f, c.g * f, c.b * f, 1f);
                    }
                    p.GlowMesh.SetColors(p.GlowNow);
                }
                if (p.Order == here)
                    for (int k = 0; k < p.Lights.Count; k++)
                    {
                        MoodSource src = p.Sources[p.LightSource[k]];
                        bool candle = src.Kind == MoodSourceKind.Candle;
                        p.Lights[k].intensity = (candle ? v.CandleLight : v.BrazierLight) * p.Factor[p.LightSource[k]];
                        p.Lights[k].range = candle ? v.CandleLightRange : v.BrazierLightRange;
                    }
            }

            if (_tone != null)
            {
                PlaceholderSounds sounds = PlaceholderSounds.Current;
                float master = sounds == null ? 1f : sounds.Values.On ? sounds.Values.Master : 0f;
                _tone.volume = Mathf.Clamp01(v.ToneVolume * master);
            }
        }

        // ---------------- What it costs ----------------

        /// <summary>The triangles of every mesh it made, and how many meshes.</summary>
        public long Triangles(out int meshes)
        {
            long n = 0;
            meshes = 0;
            foreach (Object o in _owned)
                if (o is Mesh m)
                {
                    meshes++;
                    for (int s = 0; s < m.subMeshCount; s++) n += m.GetIndexCount(s) / 3;
                }
            return n;
        }
    }
}
