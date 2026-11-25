using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// What shows her skills in the world in place of the flat shapes on the ground (work order 123, 2026-11-25; Kristian: "Jeg vil
    /// heller ikke at alle abilities får en cone/telegraf etter man har skutt de, prosjektiler og røyk/flammer skal heller vise
    /// hvordan spellsene utspiller seg"), with <see cref="SkillLookValues.EffectsNotShapes"/> on:
    /// - dust kicked up where each of a cone's tracers ends, and gun smoke at the muzzle after (Scatter Blast, Fan Fire);
    /// - a blast's ring of dust running out from its middle to exactly its radius, and smoke left inside it after (Powder Keg);
    /// - the keg or the smoke round in the air over the startup, from her hand to exactly where the rule lands it;
    /// - the keg itself while it lies on its fuse, sparks off its fuse, for exactly as long as the rule's keg lies;
    /// - Smoke Round's cloud as puffs of smoke whose outer side lies on its radius, drawn exactly while the rule's cloud stands;
    /// - Slide's dust kicked back where she starts.
    /// Every place and time is the rule's (<see cref="SkillLookRules"/>); nothing here changes what a skill does.
    ///
    /// Work order 126 (2026-11-25) draws them as the game is drawn (<see cref="SkillLookValues.ToonLook"/>, on): smoke and dust as
    /// rounded puffs with body in two or three flat tones and an ink line, born small, swelling, rolling and rising, eaten away
    /// from their edges at the end; Powder Keg's blast as a fire that runs out to exactly its radius in the blast's time (fire
    /// balls, tongues licking up, in hard bands from a pale core to a red edge), embers rising and dying, a short warm light on the
    /// floor and walls, its starburst kept with clean edges, and smoke rising where it was. The shapes are fields in a texture
    /// made in code, cut one screen pixel wide by the project's own shader (<see cref="SkillEffectArt"/>): clean with
    /// anti-aliasing on or off. Smoke Round's cloud is cleared in front of the figures in it, so she and the enemies are found.
    /// Without the shader (or with the look off) everything is drawn as 123 drew it.
    ///
    /// Made in code, no asset. Pooled: the pieces are made the first time one is wanted and lent out; past
    /// <see cref="SkillLookValues.MostPieces"/> live a new one is not drawn. Nothing is made or allocated per frame once warm,
    /// and with nothing live a frame does nothing but look.
    ///
    /// It lives in the scene, made by whatever first wants it (<see cref="Ensure"/>): a cone's or a blast's effects, a keg that lands,
    /// a smoke cloud, a throw seen by the aim outline. With the switch off nothing makes it, and nothing of it is drawn.
    /// </summary>
    public sealed class SkillEffects : MonoBehaviour
    {
        static SkillEffects s_current;

        /// <summary>The one in the scene now, or null.</summary>
        public static SkillEffects Current => s_current != null ? s_current : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_current = null;

        /// <summary>The one in the scene, made if there is none.</summary>
        public static SkillEffects Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("SkillEffects");
            return go.AddComponent<SkillEffects>();
        }

        /// <summary>What a piece is there for (work order 126 added the last four: the fire's balls, its tongues, its embers, the starburst).</summary>
        public enum Use { TracerKick, MuzzleSmoke, RingDust, RingSpark, AfterSmoke, FuseSpark, ThrowTrail, Smoke, SlideDust, Fire, Flame, Ember, Star }

        /// <summary>What one piece looks like now, for tests and tools.</summary>
        public struct Shown
        {
            public Use Use;
            public Vector3 Position;   // in the world
            public float Half;         // m: half its width (its mesh reaches this far from its middle)
            public float Age, Life;    // s
            public float Alpha;        // its alpha now (sparks: 1)
            public int Cloud;          // a smoke puff: the place of its cloud in SmokeClouds; -1 otherwise
            public Vector3 Centre;     // a blast's ring: its middle
            public float Reach;        // a blast's ring: how far its middle goes from the blast's
            public bool Drawn;
            // Work order 126:
            public Vector3 Foot;       // where it stands on the floor (a tongue's foot, a ball's or a puff's place under it)
            public float HalfTall;     // m: half its height (a tongue's; otherwise its half)
            public float Worn;         // how far it is eaten away (0 whole, 1 gone)
            public bool Toon;          // drawn in the drawn look
        }

        enum Style { Smoke, Dust, Soot, Fire, Ember, Star }

        sealed class Piece
        {
            public bool Live;
            public Use Use;
            public bool Spark;          // drawn as a spark (opaque), else a puff (blended)
            public int Cloud = -1;
            public float Born, Life;
            public Vector3 From, Velocity, Centre, Out;
            public float Gravity, Reach, Arrive, Height;
            public float Half, Alpha, AlphaSet = -1f, Spin;
            public float SmokeFrom, SmokeUntil;
            // Work order 126: the drawn look.
            public bool Toon;
            public Style Style;
            public float Seed, Grow, Share, Speed, Drag, Blast, Late, Wear, HalfTall;
            public bool Mirror;
            public Vector3 Foot;
            public MaterialPropertyBlock Block;
            public GameObject Go;
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        sealed class Barrel
        {
            public GameObject Go;
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public float NextSpark;
        }

        /// <summary>A fire's warm light on the floor and walls while it burns (work order 126).</summary>
        sealed class Glow
        {
            public Light Light;
            public bool Live;
            public float Born, Blast, Life, Peak, Seed;
        }

        readonly List<Piece> _pool = new List<Piece>();
        readonly List<Piece> _free = new List<Piece>();
        readonly List<Piece> _live = new List<Piece>();
        readonly List<Barrel> _kegs = new List<Barrel>();
        readonly List<Glow> _glows = new List<Glow>();
        Barrel _thrown;
        bool _throwing, _throwSmoke;
        Vector3 _throwFrom, _throwAt;
        float _nextTrail;
        readonly bool[] _cloudShown = new bool[SmokeValues.MostClouds];
        readonly float[] _cloudFrom = new float[SmokeValues.MostClouds];
        readonly float[] _cloudX = new float[SmokeValues.MostClouds];
        readonly float[] _cloudZ = new float[SmokeValues.MostClouds];
        PlayerCharacter _player;
        Material _puffMaterial, _ownSpark, _drawn;
        Mesh[] _puffs;
        Mesh _spark, _keg, _canister;
        MaterialPropertyBlock _block;
        int _colorId;
        GeneratedLevel _level;
        bool _levelKnown;
        int _seed = 1;
        bool _figuresSet;
        readonly Vector4[] _figures = new Vector4[MostFigures];

        const int MostFigures = 8;     // the shader's array: the figures the smoke is cleared round
        const int MostGlows = 4;       // fires' lights at once
        const float EmberDrag = 3f;    // a second: how fast the air holds an ember back (its farthest is its speed over this)

        static readonly int s_Tone0 = Shader.PropertyToID("_Tone0"), s_Tone1 = Shader.PropertyToID("_Tone1"), s_Tone2 = Shader.PropertyToID("_Tone2"),
                            s_Tone3 = Shader.PropertyToID("_Tone3"), s_Ink = Shader.PropertyToID("_Ink"), s_Bands = Shader.PropertyToID("_Bands"),
                            s_Shape = Shader.PropertyToID("_Shape"), s_Flow = Shader.PropertyToID("_Flow"), s_More = Shader.PropertyToID("_More"),
                            s_Cut = Shader.PropertyToID("_Cut"), s_Figures = Shader.PropertyToID("_SkillFxFigures"), s_FigureCount = Shader.PropertyToID("_SkillFxFigureCount"),
                            s_FigureHeight = Shader.PropertyToID("_SkillFxFigureHeight");

        /// <summary>Pieces live now.</summary>
        public int Live => _live.Count;
        /// <summary>The pool's size: every piece made so far.</summary>
        public int Pooled => _pool.Count;
        /// <summary>Objects made since it started (pieces, barrels, meshes, lights): for the test that nothing is made per use once warm.</summary>
        public int Made { get; private set; }
        /// <summary>Pieces asked for and not drawn because <see cref="SkillLookValues.MostPieces"/> were live.</summary>
        public int Capped { get; private set; }
        /// <summary>Pieces drawn since it started, by what for (indexed by <see cref="Use"/>).</summary>
        public readonly int[] DrawnBy = new int[13];
        /// <summary>Kegs drawn lying now.</summary>
        public int KegsShown { get; private set; }
        /// <summary>True while a throw is drawn; where the round is now.</summary>
        public bool ThrowShown => _throwing && _thrown != null && _thrown.Go.activeSelf;
        public Vector3 ThrowAt => _throwAt;
        /// <summary>Where a lying keg is drawn, for tests.</summary>
        public Vector3 KegAt(int i) => i >= 0 && i < _kegs.Count && _kegs[i].Go.activeSelf ? _kegs[i].T.position : Vector3.zero;
        /// <summary>Fires' lights lit now, and the brightest of them (work order 126).</summary>
        public int GlowsLit { get; private set; }
        public float GlowBrightest { get; private set; }
        /// <summary>The figures the smoke is cleared round now (work order 126).</summary>
        public int FiguresCleared { get; private set; }

        SkillLookValues Values => SkillLookValues.Live;

        /// <summary>True when the drawn look (work order 126) is drawn: its value on and the project's shader there.</summary>
        public bool DrawnLook => Values.ToonLook && SkillEffectArt.Ready;

        void Awake()
        {
            if (s_current != null && s_current != this)
            {
                Destroy(this);
                return;
            }
            s_current = this;
            _block = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID("_BaseColor");
        }

        void OnDestroy()
        {
            if (s_current == this) s_current = null;
            if (_puffs != null) foreach (Mesh m in _puffs) if (m != null) Destroy(m);
            if (_spark != null) Destroy(_spark);
            if (_keg != null) Destroy(_keg);
            if (_canister != null) Destroy(_canister);
            if (_puffMaterial != null) Destroy(_puffMaterial);
            if (_ownSpark != null) Destroy(_ownSpark);
            if (_drawn != null) Destroy(_drawn);
            if (_figuresSet) Shader.SetGlobalFloat(s_FigureCount, 0f);
        }

        /// <summary>The player whose skills it shows: the throw, the lying kegs and the smoke are read from her each frame.</summary>
        public SkillEffects Watch(PlayerCharacter player)
        {
            if (player != null) _player = player;
            return this;
        }

        // ---------------- Asked for by her skills (PlayerCharacterSkillLook.cs, CombatEffects) ----------------

        /// <summary>Dust kicked up under where a cone's tracer ends (its reach: the range, or the cover that stopped it).</summary>
        public void TracerEnd(Vector3 end)
        {
            SkillLookValues v = Values;
            Piece p = DrawnLook ? TakeDrawn(Use.TracerKick, Style.Dust, PuffShape(), v.KickSeconds, v.KickSize * 0.5f)
                            : Take(Use.TracerKick, spark: false, v.KickSeconds, v.KickSize * 0.5f, v.Dust.A);
            if (p == null) return;
            p.From = new Vector3(end.x, v.KickHeight, end.z);
            p.Velocity = Vector3.up * v.KickRise;
            Place(p);
        }

        /// <summary>Gun smoke at the muzzle after a cone's shot: <paramref name="puffs"/> grey puffs drifting up and along it.</summary>
        public void MuzzleSmoke(Vector3 muzzle, Vector3 dir, int puffs)
        {
            SkillLookValues v = Values;
            Vector3 along = new Vector3(dir.x, 0f, dir.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            for (int i = 0; i < puffs; i++)
            {
                float life = v.MuzzleSmokeSeconds * (0.8f + 0.4f * Next());
                Piece p = DrawnLook ? TakeDrawn(Use.MuzzleSmoke, Style.Smoke, PuffShape(), life, v.MuzzleSmokeSize * 0.5f)
                                : Take(Use.MuzzleSmoke, spark: false, life, v.MuzzleSmokeSize * 0.5f, v.SmokeAlpha * 1.2f);
                if (p == null) return;
                p.From = muzzle + along * (0.15f + 0.25f * i);
                p.Velocity = (Vector3.up + along * 0.6f + new Vector3(Next() - 0.5f, 0f, Next() - 0.5f) * 0.6f).normalized * v.MuzzleSmokeRise;
                Place(p);
            }
        }

        /// <summary>
        /// A blast of <paramref name="radius"/> at <paramref name="centre"/> over <paramref name="seconds"/>: a ring of dust running out
        /// from the middle, slowing, until its outer side lies on the radius; and smoke left inside it after. In the drawn look (work
        /// order 126) a fire too: balls of it running out along the floor to the radius (times <see cref="SkillLookValues.FireSize"/>,
        /// never past it) in exactly the blast's seconds, balls over its middle rising, tongues licking up from its ring, all burning
        /// out for <see cref="SkillLookValues.FireBurnOutSeconds"/> after; embers thrown up and out inside the radius, rising and
        /// dying; a warm light while it burns; the smoke after as puffs rising where it was.
        /// </summary>
        public void Blast(Vector3 centre, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            SkillLookValues v = Values;
            var c = new Vector3(centre.x, 0f, centre.z);
            float turn = Next() * 360f;
            if (DrawnLook)
            {
                DrawnBlast(c, radius, seconds, turn);
                return;
            }
            int dust = Mathf.Max(0, v.RingPuffs), sparks = Mathf.Max(0, v.RingSparks);
            for (int i = 0; i < dust; i++)
            {
                float half = Mathf.Min(v.RingPuffSize * 0.5f, radius * 0.5f);
                Piece p = Take(Use.RingDust, spark: false, seconds, half, v.Dust.A);
                if (p == null) break;
                Ring(p, c, turn + 360f * i / dust, radius - half, v.RingHeight * (0.7f + 0.6f * Next()), v.ArriveShare);
            }
            for (int i = 0; i < sparks; i++)
            {
                float half = Mathf.Min(v.RingSparkSize * 0.5f, radius * 0.5f);
                Piece p = Take(Use.RingSpark, spark: true, seconds, half, 1f);
                if (p == null) break;
                Ring(p, c, turn + 360f * (i + 0.5f) / sparks, radius - half, v.RingHeight * (0.8f + 0.8f * Next()), v.ArriveShare * 0.85f);
            }
            float smokeHalf = Mathf.Min(v.AfterSmokeSize * 0.5f, radius * v.AfterSmokeReach * 0.5f);
            float within = Mathf.Max(0f, radius * v.AfterSmokeReach - smokeHalf);
            for (int i = 0; i < v.AfterSmokePuffs; i++)
            {
                Piece p = Take(Use.AfterSmoke, spark: false, v.AfterSmokeSeconds * (0.85f + 0.3f * Next()), smokeHalf, v.SmokeAlpha * 1.3f);
                if (p == null) break;
                float a = Next() * Mathf.PI * 2f, r = within * Mathf.Sqrt(Next());
                p.From = c + new Vector3(Mathf.Cos(a) * r, 0.4f + 0.6f * Next(), Mathf.Sin(a) * r);
                p.Velocity = Vector3.up * v.MuzzleSmokeRise * 0.5f; // straight up: it never leaves the circle
                Place(p);
            }
        }

        /// <summary>The drawn look's blast (work order 126): see <see cref="Blast"/>.</summary>
        void DrawnBlast(Vector3 c, float radius, float seconds, float turn)
        {
            SkillLookValues v = Values;
            float share = Mathf.Clamp01(v.FireSize);
            float burn = Mathf.Max(0.01f, v.FireBurnOutSeconds), life = seconds + burn;
            // The fire's balls running out along the floor.
            int blobs = Mathf.Max(0, v.FireBlobs);
            for (int i = 0; i < blobs; i++)
            {
                float half = Mathf.Min(v.FireBlobSize * 0.5f, radius * share * 0.5f);
                Piece p = TakeDrawn(Use.Fire, Style.Fire, FireShape(), life, half);
                if (p == null) break;
                float a = (turn + 360f * (i + 0.3f * (Next() - 0.5f)) / Mathf.Max(1, blobs)) * Mathf.Deg2Rad;
                p.Centre = c;
                p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.Reach = radius;
                p.Share = share;
                p.Blast = seconds;
                p.Height = half * (0.55f + 0.25f * Next());
                p.Speed = 0.15f * Next();
                p.Grow = 0.35f;
                p.Arrive = OuterCool;
                Place(p);
            }
            // A second ring of them halfway out, so the fire is one fire and not a ring of balls.
            int mid = Mathf.Max(0, v.FireMidBlobs);
            for (int i = 0; i < mid; i++)
            {
                float half = Mathf.Min(v.FireBlobSize * 0.5f, radius * share * 0.5f);
                Piece p = TakeDrawn(Use.Fire, Style.Fire, FireShape(), life, half);
                if (p == null) break;
                float a = (turn + 360f * (i + 0.5f) / Mathf.Max(1, mid)) * Mathf.Deg2Rad;
                p.Centre = c;
                p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.Reach = radius;
                p.Share = share * FireMidShare;
                p.Blast = seconds;
                p.Height = half * (0.7f + 0.3f * Next());
                p.Speed = 0.3f * Next();
                p.Grow = 0.35f;
                p.Arrive = MidCool;
                Place(p);
            }
            // Its balls over the middle, rising.
            for (int i = 0; i < Mathf.Max(0, v.FireCore); i++)
            {
                float half = Mathf.Min(v.FireCoreSize * 0.5f, radius * share * 0.6f);
                Piece p = TakeDrawn(Use.Fire, Style.Fire, FireShape(), life, half);
                if (p == null) break;
                float a = Next() * Mathf.PI * 2f, off = 0.25f * half * Next();
                p.Centre = c + new Vector3(Mathf.Cos(a) * off, 0f, Mathf.Sin(a) * off);
                p.Out = Vector3.zero;
                p.Reach = 0f;
                p.Share = share;
                p.Blast = seconds;
                p.Height = 0.45f + 0.5f * i;
                p.Speed = v.FireRise * (0.7f + 0.5f * Next());
                p.Grow = 0.3f;
                p.Arrive = 0f;
                Place(p);
            }
            // Tongues licking up from the ring, between the balls.
            int tongues = Mathf.Max(0, v.FlameTongues);
            for (int i = 0; i < tongues; i++)
            {
                float half = Mathf.Min(v.FlameTongueSize * 0.5f, radius * share * 0.5f);
                Piece p = TakeDrawn(Use.Flame, Style.Fire, (_seed & 1) == 0 ? SkillEffectArt.Shape.Tongue0 : SkillEffectArt.Shape.Tongue1, life, half);
                if (p == null) break;
                float a = (turn + 360f * (i + 0.5f + 0.35f * (Next() - 0.5f)) / Mathf.Max(1, tongues)) * Mathf.Deg2Rad;
                p.Centre = c;
                p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.Reach = radius;
                p.Share = share * (0.6f + 0.4f * Next()); // some lick up inside the ring, not all on it
                p.Blast = seconds;
                p.HalfTall = v.FlameTongueTall * 0.5f * (0.7f + 0.3f * Next());
                p.Mirror = Next() < 0.5f;
                p.Grow = 0.2f;
                Place(p);
            }
            // The dust ring.
            int dust = Mathf.Max(0, v.RingDustPuffs);
            for (int i = 0; i < dust; i++)
            {
                float half = Mathf.Min(v.RingDustSize * 0.5f, radius * 0.5f);
                Piece p = TakeDrawn(Use.RingDust, Style.Dust, PuffShape(), seconds, half);
                if (p == null) break;
                Ring(p, c, turn + 360f * (i + 0.5f) / dust, radius - half, half * (0.45f + 0.3f * Next()), v.ArriveShare);
            }
            // Embers: thrown up and out from the middle, held back by the air, never past the radius.
            for (int i = 0; i < Mathf.Max(0, v.Embers); i++)
            {
                float half = v.EmberSize * 0.5f;
                Piece p = TakeDrawn(Use.Ember, Style.Ember, SkillEffectArt.Shape.Ember, v.EmberSeconds * (0.7f + 0.3f * Next()), half);
                if (p == null) break;
                float a = Next() * Mathf.PI * 2f, off = 0.3f * Next();
                p.Centre = c;
                p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.From = c + p.Out * off + Vector3.up * (0.4f + 0.4f * Next());
                float far = Mathf.Max(0f, radius * share - half * 3f - off) * (0.45f + 0.55f * Next());
                p.Drag = EmberDrag;
                p.Speed = far * EmberDrag;
                p.Velocity = Vector3.up * (2.2f + 2f * Next());
                Place(p);
            }
            // The smoke after: puffs born out of the fire as it dies, rising and swelling, eaten away.
            for (int i = 0; i < Mathf.Max(0, v.AfterSootPuffs); i++)
            {
                float half = Mathf.Min(v.AfterSootSize * 0.5f, radius * v.AfterSmokeReach * 0.5f);
                float grown = half * AfterSootSwell;
                Piece p = TakeDrawn(Use.AfterSmoke, Style.Soot, PuffShape(), v.AfterSootSeconds * (0.8f + 0.2f * Next()), half);
                if (p == null) break;
                float within = Mathf.Max(0f, radius * v.AfterSmokeReach - grown);
                float a = Next() * Mathf.PI * 2f, r = within * Mathf.Sqrt(Next());
                p.From = c + new Vector3(Mathf.Cos(a) * r, 0.5f + 0.4f * Next(), Mathf.Sin(a) * r);
                p.Born = Time.time + seconds * (0.8f + 0.4f * Next()); // out of the fire, as it burns out
                p.Speed = v.AfterSootRise * (0.7f + 0.5f * Next());
                Place(p);
            }
            LightUp(c, radius, seconds, life);
        }

        /// <summary>How much a puff draws in, and how far it lifts (m), as it is eaten away (work order 126).</summary>
        const float EatenShrink = 0.5f, EatenLift = 0.25f;

        /// <summary>How much cooler than the middle's the fire's outer and second rings of balls burn (work order 126): no white core in each.</summary>
        const float OuterCool = 0.36f, MidCool = 0.16f;

        /// <summary>The share of the fire's reach its second ring of balls runs out to (work order 126).</summary>
        const float FireMidShare = 0.55f;

        /// <summary>How much a puff of the smoke after a blast swells over its life, past its size (work order 126).</summary>
        const float AfterSootSwell = 1.15f;

        /// <summary>
        /// The keg's starburst in the drawn look (work order 126; Kristian liked 123's blast): the shot effects' own spark star (its
        /// spikes, its inner star, its pop and shrink over the blast's seconds, drawn <paramref name="towardCamera"/> nearer the
        /// camera), with clean edges. False when the drawn look is not drawn (the caller draws the shot effects' star, as before);
        /// true with <see cref="SkillLookValues.KegStarburst"/> off and nothing drawn.
        /// </summary>
        public bool Star(Vector3 at, float size, float seconds, ShotPaint paint, float towardCamera)
        {
            if (!SkillLookValues.EffectsNotShapes || !DrawnLook) return false;
            if (!Values.KegStarburst || size <= 0f || seconds <= 0f) return true;
            Piece p = TakeDrawn(Use.Star, Style.Star, SkillEffectArt.Shape.Star, seconds, size / SkillEffectArt.StarFill);
            if (p == null) return true;
            Vector4 rim = Lit(paint.Rim, paint.RimGlow), core = Lit(paint.Core, paint.CoreGlow);
            p.Block.SetVector(s_Tone0, core);
            p.Block.SetVector(s_Tone1, rim);
            p.Block.SetVector(s_Tone2, rim);
            p.Block.SetVector(s_Tone3, rim);
            p.From = at;
            p.Height = towardCamera;
            p.Spin = Next() * 360f;
            Place(p);
            return true;
        }

        /// <summary>Slide's dust, kicked back from where she starts along <paramref name="back"/>.</summary>
        public void SlideDust(Vector3 from, Vector3 back)
        {
            SkillLookValues v = Values;
            Vector3 b = new Vector3(back.x, 0f, back.z);
            b = b.sqrMagnitude > 1e-6f ? b.normalized : Vector3.back;
            int n = Mathf.Max(0, v.SlideDustPuffs);
            for (int i = 0; i < n; i++)
            {
                Piece p = DrawnLook ? TakeDrawn(Use.SlideDust, Style.Dust, PuffShape(), v.SlideDustSeconds, v.SlideDustSize * 0.5f)
                                : Take(Use.SlideDust, spark: false, v.SlideDustSeconds, v.SlideDustSize * 0.5f, v.Dust.A);
                if (p == null) return;
                float yaw = n > 1 ? Mathf.Lerp(-v.SlideDustSpread * 0.5f, v.SlideDustSpread * 0.5f, i / (n - 1f)) : 0f;
                p.From = new Vector3(from.x, 0.2f, from.z);
                p.Velocity = Quaternion.Euler(0f, yaw, 0f) * b * v.SlideDustSpeed + Vector3.up * 0.4f;
                Place(p);
            }
        }

        // ---------------- Every frame ----------------

        void Update() => Step();

        /// <summary>The frame's work: what is read from her and the clouds, and every live piece moved. Public for the allocation test.</summary>
        public void Step()
        {
            float now = Time.time;
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            // Gone with the floor it was drawn on (by reference: a destroyed floor is not "no floor"), as what flies is.
            if (_levelKnown && !ReferenceEquals(level, _level)) ClearAll();
            _level = level;
            _levelKnown = true;
            bool on = SkillLookValues.EffectsNotShapes && _player != null;
            Camera cam = Camera.main;
            ThrowNow(on, now, cam);
            KegsNow(on, now, cam);
            CloudsNow(SkillLookValues.EffectsNotShapes, now); // the clouds are read from SmokeClouds, not from her
            FiguresNow();
            for (int i = _live.Count - 1; i >= 0; i--) Tick(_live[i], i, now, cam);
            GlowsNow(now, cam);
        }

        void ThrowNow(bool on, float now, Camera cam)
        {
            SkillDefinition s = on ? _player.ActiveSkill : null;
            bool throwing = s != null && s.Input == SkillInput.Point && s.Shape == SkillShape.Circle && _player.SkillPhase == SkillPhase.Startup;
            if (!throwing)
            {
                if (_thrown != null && _thrown.Go.activeSelf) _thrown.Go.SetActive(false);
                _throwing = false;
                return;
            }
            if (!_throwing)
            {
                _throwing = true;
                _throwFrom = _player.ThrowHand;
                _throwSmoke = s.SmokeSeconds > 0f;
                _nextTrail = now;
                if (_thrown == null) _thrown = MakeBarrel("SkillEffects_Thrown");
                _thrown.Filter.sharedMesh = _throwSmoke ? _canister : _keg;
                _thrown.Renderer.sharedMaterial = SparkMaterial;
            }
            // Where the rule lands it: the circle's centre as it is placed at the hit (short of cover in its way), from where she
            // stands now and the point it was pressed at.
            GroundPoint c = CoverRules.CircleCenter(s, _player.GroundPosition, new GroundPoint(_player.AimPoint.x, _player.AimPoint.z), _player.CoverQuery);
            float k = Mathf.Clamp01(_player.SkillProgress);
            var to = new Vector3(c.X, 0f, c.Z);
            _throwAt = Vector3.Lerp(_throwFrom, to, k) + Vector3.up * SkillLookRules.ThrowLift(k, Values.ThrowPeak);
            _thrown.T.SetPositionAndRotation(_throwAt, Quaternion.Euler(k * 300f, k * 140f, 0f));
            if (!_thrown.Go.activeSelf) _thrown.Go.SetActive(true);
            if (now < _nextTrail) return;
            _nextTrail = now + Mathf.Max(0.005f, Values.ThrowTrailEvery);
            SkillLookValues v = Values;
            if (_throwSmoke)
            {
                Piece p = DrawnLook ? TakeDrawn(Use.ThrowTrail, Style.Smoke, PuffShape(), v.MuzzleSmokeSeconds * 0.6f, v.MuzzleSmokeSize * 0.3f)
                                : Take(Use.ThrowTrail, spark: false, v.MuzzleSmokeSeconds * 0.6f, v.MuzzleSmokeSize * 0.3f, v.SmokeAlpha);
                if (p == null) return;
                p.From = _throwAt;
                p.Velocity = Vector3.up * 0.3f;
                Place(p);
            }
            else
            {
                Piece p = DrawnLook ? TakeDrawn(Use.ThrowTrail, Style.Ember, SkillEffectArt.Shape.Ember, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f)
                                : Take(Use.ThrowTrail, spark: true, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f, 1f);
                if (p == null) return;
                p.From = _throwAt + Vector3.up * v.KegHeight * 0.5f;
                p.Velocity = new Vector3(Next() - 0.5f, 1f, Next() - 0.5f) * 0.8f;
                p.Gravity = 3f;
                Place(p);
            }
        }

        void KegsNow(bool on, float now, Camera cam)
        {
            int n = on ? _player.KegsLying : 0;
            while (_kegs.Count < n) _kegs.Add(MakeBarrel("SkillEffects_Keg_" + (_kegs.Count + 1)));
            KegsShown = 0;
            SkillLookValues v = Values;
            for (int i = 0; i < _kegs.Count; i++)
            {
                Barrel b = _kegs[i];
                if (i >= n || !_player.LyingKegAt(i, out Vector3 centre, out float burstAt, out _))
                {
                    if (b.Go.activeSelf) b.Go.SetActive(false);
                    continue;
                }
                if (!b.Go.activeSelf)
                {
                    b.Filter.sharedMesh = _keg;
                    b.Renderer.sharedMaterial = SparkMaterial;
                    b.Go.SetActive(true);
                    b.NextSpark = now;
                }
                b.T.SetPositionAndRotation(new Vector3(centre.x, 0f, centre.z), Quaternion.Euler(0f, (centre.x * 37f + centre.z * 53f) % 360f, 0f));
                KegsShown++;
                if (now < b.NextSpark || now >= burstAt) continue;
                b.NextSpark = now + Mathf.Max(0.01f, v.FuseEvery);
                Piece p = DrawnLook ? TakeDrawn(Use.FuseSpark, Style.Ember, SkillEffectArt.Shape.Ember, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f)
                                : Take(Use.FuseSpark, spark: true, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f, 1f);
                if (p == null) continue;
                p.From = new Vector3(centre.x, v.KegHeight + 0.08f, centre.z);
                p.Velocity = new Vector3((Next() - 0.5f) * 0.8f, v.FuseSparkRise, (Next() - 0.5f) * 0.8f);
                p.Gravity = 2.5f;
                Place(p);
            }
        }

        void CloudsNow(bool on, float now)
        {
            int count = SmokeClouds.Count;
            for (int i = 0; i < _cloudShown.Length; i++)
            {
                SmokeCloud cloud = i < count ? SmokeClouds.At(i) : default;
                bool up = on && i < count && cloud.IsUp(now) && SmokeClouds.ViewAt(i) != null;
                if (!up)
                {
                    if (_cloudShown[i]) ClearCloud(i);
                    continue;
                }
                if (_cloudShown[i] && _cloudFrom[i] == cloud.From && _cloudX[i] == cloud.Centre.X && _cloudZ[i] == cloud.Centre.Z) continue;
                if (_cloudShown[i]) ClearCloud(i);
                ShowCloud(i, cloud);
            }
        }

        void ShowCloud(int place, SmokeCloud cloud)
        {
            SkillLookValues v = Values;
            _cloudShown[place] = true;
            _cloudFrom[place] = cloud.From;
            _cloudX[place] = cloud.Centre.X;
            _cloudZ[place] = cloud.Centre.Z;
            int n = Mathf.Max(1, v.SmokePuffs);
            bool drawn = DrawnLook;
            float half = Mathf.Max(0.05f, v.SmokePuffShare * cloud.Radius * 0.5f);
            // The drawn look's sizes, never wider than the soft look's share of a small cloud allows.
            float edgeHalf = Mathf.Min(v.SmokeEdgeSize * 0.5f, cloud.Radius * 0.5f), innerHalf = Mathf.Min(v.SmokeInnerSize * 0.5f, cloud.Radius * 0.6f);
            for (int i = 0; i < n; i++)
            {
                float life = Mathf.Max(1e-3f, cloud.Until - cloud.From);
                Piece p;
                if (drawn)
                {
                    GroundPoint at = SkillLookRules.CloudPuffAt(i, n, v.SmokeEdgePuffs, cloud.Radius, edgeHalf, innerHalf, out float share);
                    bool edge = i < Mathf.Min(v.SmokeEdgePuffs, n);
                    p = TakeDrawn(Use.Smoke, Style.Smoke, (SkillEffectArt.Shape)((i * 3 + place) % SkillEffectArt.PuffShapes), life, edge ? edgeHalf : innerHalf);
                    if (p == null) return;
                    float low = (edge ? edgeHalf : innerHalf) * SmokeFootShare;
                    float h = SkillLookRules.CloudPuffHeight(share, low, Mathf.Max(low, v.SmokeTop)) + 0.08f * (Next() - 0.5f);
                    p.From = new Vector3(cloud.Centre.X + at.X, h, cloud.Centre.Z + at.Z);
                    p.Share = share;
                    p.Out = new Vector3(-at.Z, 0f, at.X).normalized; // along the edge: a puff rolls round, never out of the circle
                    p.Late = v.SmokeStagger * share;           // it bursts out from the middle
                    p.Grow = Mathf.Clamp01(v.SmokeBornShare);
                    // Each a little early, by its own share (the inner ones up to 0.12 s, the edge's at most 0.04 s, its area to the
                    // last): thirty puffs do not go in one frame.
                    p.Speed = Next() * (edge ? EdgeEarly : InnerEarly);
                }
                else
                {
                    p = Take(Use.Smoke, spark: false, life, half, v.SmokeAlpha);
                    if (p == null) return;
                    GroundPoint at = SkillLookRules.SmokePuffAt(i, n, v.SmokeEdgePuffs, cloud.Radius, half);
                    float h = Mathf.Lerp(v.SmokeLowest, v.SmokeHighest, ((i * 7) % n) / (float)n);
                    p.From = new Vector3(cloud.Centre.X + at.X, h, cloud.Centre.Z + at.Z);
                    p.Spin = (i % 2 == 0 ? 1f : -1f) * v.SmokeSpin;
                }
                p.Born = cloud.From;
                p.Cloud = place;
                p.SmokeFrom = cloud.From;
                p.SmokeUntil = cloud.Until;
                p.Velocity = Vector3.up * v.SmokeRise;
                Place(p);
            }
        }

        /// <summary>
        /// The share of a puff's half its middle stands over the floor on the cloud's edge (work order 126): its foot then meets the
        /// floor on the circle as the game's camera sees it (pitch 55: a puff facing the camera reaches down and toward it).
        /// </summary>
        const float SmokeFootShare = 0.32f;

        /// <summary>s at most a drawn cloud's puff on its edge, and one inside, is gone before the cloud's time is out (work order 126).</summary>
        const float EdgeEarly = 0.04f, InnerEarly = 0.12f;

        void ClearCloud(int place)
        {
            _cloudShown[place] = false;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].Cloud == place) Return(_live[i], i);
        }

        /// <summary>
        /// The figures the smoke is cleared round in front of them (work order 126): her and every enemy standing in or near a cloud
        /// drawn now, at most <see cref="MostFigures"/>, set for the shader. With no cloud drawn, none.
        /// </summary>
        void FiguresNow()
        {
            SkillLookValues v = Values;
            int n = 0;
            bool any = false;
            for (int i = 0; i < _cloudShown.Length; i++) any |= _cloudShown[i];
            if (any && v.SmokeClearAround > 0f && DrawnLook)
            {
                float r = v.SmokeClearAround;
                if (_player != null && NearACloud(_player.Position)) _figures[n++] = Figure(_player.Position, r);
                IReadOnlyList<Enemy> all = Enemy.All;
                for (int i = 0; i < all.Count && n < MostFigures; i++)
                {
                    Enemy e = all[i];
                    if (e == null || e.IsDead) continue;
                    Vector3 at = e.transform.position;
                    if (NearACloud(at)) _figures[n++] = Figure(at, r);
                }
            }
            FiguresCleared = n;
            if (n == 0 && !_figuresSet) return;
            Shader.SetGlobalVectorArray(s_Figures, _figures);
            Shader.SetGlobalFloat(s_FigureCount, n);
            Shader.SetGlobalFloat(s_FigureHeight, v.SmokeFigureHeight);
            _figuresSet = n > 0;
        }

        static Vector4 Figure(Vector3 at, float r) => new Vector4(at.x, 0f, at.z, r);

        bool NearACloud(Vector3 at)
        {
            int count = SmokeClouds.Count;
            for (int i = 0; i < count && i < _cloudShown.Length; i++)
            {
                if (!_cloudShown[i]) continue;
                SmokeCloud c = SmokeClouds.At(i);
                float dx = at.x - c.Centre.X, dz = at.z - c.Centre.Z, reach = c.Radius + 1f;
                if (dx * dx + dz * dz <= reach * reach) return true;
            }
            return false;
        }

        /// <summary>Everything drawn now is gone at once (a new floor; tests).</summary>
        public void ClearAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Return(_live[i], i);
            for (int i = 0; i < _cloudShown.Length; i++) _cloudShown[i] = false;
            foreach (Barrel b in _kegs) if (b.Go.activeSelf) b.Go.SetActive(false);
            if (_thrown != null && _thrown.Go.activeSelf) _thrown.Go.SetActive(false);
            foreach (Glow g in _glows)
            {
                g.Live = false;
                if (g.Light != null && g.Light.enabled) g.Light.enabled = false;
            }
            _throwing = false;
            KegsShown = 0;
        }

        void Tick(Piece p, int index, float now, Camera cam)
        {
            float age = now - p.Born;
            float t = age / Mathf.Max(1e-4f, p.Life);
            if (t >= 1f || (p.Use == Use.Smoke && now >= p.SmokeUntil))
            {
                Return(p, index);
                return;
            }
            if (p.Toon) PoseDrawn(p, age, t, now, cam);
            else Pose(p, age, t, now, cam);
        }

        void Pose(Piece p, float age, float t, float now, Camera cam)
        {
            Vector3 at;
            float size = 1f, alpha = p.Alpha;
            switch (p.Use)
            {
                case Use.RingDust:
                case Use.RingSpark:
                {
                    at = p.Centre + p.Out * SkillLookRules.RingReach(p.Reach, t, p.Arrive) + Vector3.up * p.Height;
                    float after = t <= p.Arrive ? 0f : (t - p.Arrive) / Mathf.Max(1e-3f, 1f - p.Arrive);
                    size = p.Spark ? (t < 0.15f ? Mathf.Lerp(0.6f, 1f, t / 0.15f) : 1f - after) : Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t / Mathf.Max(1e-3f, p.Arrive))) * (1f - 0.5f * after);
                    alpha = p.Alpha * (1f - after);
                    break;
                }
                case Use.Smoke:
                    at = p.From + p.Velocity * Mathf.Max(0f, age);
                    alpha = p.Alpha * SkillLookRules.SmokeShare(now, p.SmokeFrom, p.SmokeUntil, Values.SmokeFadeIn, Values.SmokeFadeOut);
                    break;
                default:
                    at = p.From + p.Velocity * age + Vector3.down * (0.5f * p.Gravity * age * age);
                    if (p.Spark) size = t < 0.2f ? Mathf.Lerp(0.6f, 1.1f, t / 0.2f) : 1.1f * (1f - (t - 0.2f) / 0.8f);
                    else
                    {
                        size = Mathf.Lerp(0.6f, 1f, 1f - (1f - t) * (1f - t));
                        alpha = p.Alpha * (1f - t) * (1f - t * 0.5f);
                    }
                    break;
            }
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
            p.T.SetPositionAndRotation(at, p.Spin != 0f ? face * Quaternion.Euler(0f, 0f, p.Spin * age) : face);
            p.T.localScale = Vector3.one * Mathf.Max(1e-3f, p.Half * Mathf.Max(0f, size));
            p.Foot = new Vector3(at.x, 0f, at.z);
            if (!p.Spark && Mathf.Abs(alpha - p.AlphaSet) > 0.004f)
            {
                p.AlphaSet = alpha;
                Color tint = p.Use == Use.Smoke || p.Use == Use.MuzzleSmoke || p.Use == Use.AfterSmoke || (p.Use == Use.ThrowTrail && _throwSmoke)
                    ? Picked(Values.Smoke) : Picked(Values.Dust);
                tint.a = Mathf.Clamp01(alpha);
                _block.SetColor(_colorId, tint);
                p.Renderer.SetPropertyBlock(_block);
            }
        }

        // ---------------- The drawn look (work order 126) ----------------

        /// <summary>Where a piece of the drawn look is, how big, how worn, how hot, now.</summary>
        void PoseDrawn(Piece p, float age, float t, float now, Camera cam)
        {
            SkillLookValues v = Values;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
            Vector3 camUp = face * Vector3.up, camRight = face * Vector3.right;
            Vector3 at;
            float sx = 1f, sy = 1f, wear = 0f, cool = 0f, roll = 0f, turn = 0f;
            bool hidden = false;
            switch (p.Use)
            {
                case Use.Smoke:
                {
                    float since = now - p.SmokeFrom - p.Late;
                    float k = Mathf.Clamp01(since / Mathf.Max(1e-3f, v.SmokeSwellSeconds));
                    float grow = Mathf.Lerp(p.Grow, 1f, EaseOut(k));
                    float phase = Mathf.PI * 2f * (now / Mathf.Max(0.1f, v.SmokeRollSeconds) + p.Seed);
                    float breathe = 1f + v.SmokeBreathe * Mathf.Sin(phase);
                    sx = sy = grow * breathe;
                    roll = v.SmokeRoll * Mathf.Sin(phase + 1.3f);
                    at = p.From + p.Velocity * Mathf.Max(0f, now - p.SmokeFrom) + p.Out * (0.07f * Mathf.Sin(phase * 0.7f));
                    // Born small and swelling (never eaten at its birth: an eaten edge breaks into specks), eaten at its end.
                    if (since < 0f) hidden = true;
                    wear = SkillLookRules.Worn(now, p.SmokeUntil - Mathf.Max(0f, p.Speed), v.SmokeEatSeconds);
                    // As it is eaten it draws in and lifts a little: a puff going, not a pebble left.
                    sx = sy = sx * (1f - EatenShrink * wear * wear);
                    at += Vector3.up * (EatenLift * wear);
                    break;
                }
                case Use.Fire:
                {
                    float tb = age / Mathf.Max(1e-3f, p.Blast);
                    float burn = Mathf.Clamp01((age - p.Blast) / Mathf.Max(1e-3f, p.Life - p.Blast));
                    float reach = SkillLookRules.FireReach(p.Reach, p.Share, p.Half, tb);
                    at = p.Centre + p.Out * reach + Vector3.up * (p.Height + p.Speed * age);
                    sx = sy = Mathf.Lerp(p.Grow, 1f, EaseOut(Mathf.Clamp01(tb)));
                    cool = p.Arrive + 0.08f * Mathf.Clamp01(tb) + 0.45f * burn; // the outer balls cooler than the middle's: one fireball, hottest in its middle
                    wear = Smooth(burn * 1.15f);
                    break;
                }
                case Use.Flame:
                {
                    float tb = age / Mathf.Max(1e-3f, p.Blast);
                    float burn = Mathf.Clamp01((age - p.Blast) / Mathf.Max(1e-3f, p.Life - p.Blast));
                    float reach = SkillLookRules.FireReach(p.Reach, p.Share, p.Half, tb);
                    Vector3 foot = p.Centre + p.Out * reach;
                    float tall = EaseOut(Mathf.Clamp01(tb * 1.6f)) * (1f - 0.5f * burn);
                    float lick = 1f + 0.16f * Mathf.Sin(age * 19f + p.Seed * 40f) + 0.09f * Mathf.Sin(age * 31f + p.Seed * 17f);
                    sx = Mathf.Lerp(p.Grow, 1f, EaseOut(Mathf.Clamp01(tb * 1.4f)));
                    float halfTall = Mathf.Max(1e-3f, p.HalfTall * Mathf.Max(p.Grow, tall) * lick);
                    sy = halfTall / Mathf.Max(1e-3f, p.Half);
                    at = foot + camUp * (halfTall * 0.9f); // its foot (the teardrop's round end) on the floor
                    p.Foot = foot;
                    cool = 0.12f + 0.1f * Mathf.Clamp01(tb) + 0.45f * burn;
                    wear = Smooth(burn * 1.15f);
                    break;
                }
                case Use.Ember:
                {
                    float o = SkillLookRules.EmberOut(p.Speed, p.Drag, age);
                    float rise = p.Velocity.y * (1f - Mathf.Exp(-2.5f * age)) / 2.5f + 0.35f * age;
                    at = new Vector3(p.From.x, p.From.y + rise, p.From.z) + p.Out * o;
                    Vector3 vel = p.Out * (p.Speed * Mathf.Exp(-p.Drag * age)) + Vector3.up * (p.Velocity.y * Mathf.Exp(-2.5f * age) + 0.35f);
                    turn = Mathf.Atan2(Vector3.Dot(vel, camUp), Vector3.Dot(vel, camRight)) * Mathf.Rad2Deg;
                    float size = (t < 0.1f ? Mathf.Lerp(0.6f, 1f, t / 0.1f) : 1f - 0.3f * t);
                    sx = size * (1.5f + v.EmberStretch * vel.magnitude); // a spark flying, never a round speck
                    sy = size;
                    cool = 0.35f * t;
                    wear = Smooth((t - 0.5f) / 0.5f);
                    break;
                }
                case Use.FuseSpark:
                case Use.ThrowTrail when p.Style == Style.Ember:
                {
                    at = p.From + p.Velocity * age + Vector3.down * (0.5f * p.Gravity * age * age);
                    Vector3 vel = p.Velocity + Vector3.down * (p.Gravity * age);
                    turn = Mathf.Atan2(Vector3.Dot(vel, camUp), Vector3.Dot(vel, camRight)) * Mathf.Rad2Deg;
                    float size = t < 0.2f ? Mathf.Lerp(0.6f, 1f, t / 0.2f) : 1f - 0.3f * (t - 0.2f);
                    sx = size * (1f + v.EmberStretch * vel.magnitude);
                    sy = size;
                    cool = 0.6f * t;
                    wear = Smooth((t - 0.5f) / 0.5f);
                    break;
                }
                case Use.Star:
                {
                    // The shot effects' spark's own pop and shrink (ShotFx.Animate), drawn as it was, nearer the camera.
                    float pop = t < 0.2f ? Mathf.Lerp(0.7f, 1.1f, t / 0.2f) : 1.1f * (1f - Sq((t - 0.2f) / 0.8f));
                    Vector3 toCam = cam != null ? (cam.transform.position - p.From).normalized : Vector3.up;
                    at = p.From + toCam * p.Height;
                    sx = sy = Mathf.Max(0f, pop);
                    turn = p.Spin;
                    break;
                }
                case Use.RingDust:
                {
                    at = p.Centre + p.Out * SkillLookRules.RingReach(p.Reach, t, p.Arrive) + Vector3.up * p.Height;
                    sx = sy = Mathf.Lerp(0.45f, 1f, EaseOut(Mathf.Clamp01(t / Mathf.Max(1e-3f, p.Arrive))));
                    roll = 0.04f * Mathf.Sin(age * 7f + p.Seed * 20f);
                    wear = Sq(Mathf.Clamp01((t - 0.45f) / 0.55f));
                    break;
                }
                case Use.AfterSmoke:
                {
                    if (age < 0f)
                    {
                        hidden = true;
                        at = p.From;
                        break;
                    }
                    const float settle = 0.9f; // s: how fast its rise slows
                    at = p.From + Vector3.up * (p.Speed * settle * (1f - Mathf.Exp(-age / settle)));
                    float grow = Mathf.Lerp(0.5f, 1f, EaseOut(Mathf.Clamp01(age / 0.4f))) * (1f + (AfterSootSwell - 1f) * t);
                    roll = v.SmokeRoll * Mathf.Sin(age * 2.4f + p.Seed * 20f);
                    wear = Sq(Mathf.Clamp01((t - 0.45f) / 0.55f)); // slowly, then quickly: no last bits lingering
                    sx = sy = grow * (1f - EatenShrink * wear * wear);
                    at += Vector3.up * (EatenLift * wear);
                    cool = 0f;
                    break;
                }
                default: // a tracer's dust, gun smoke, Slide's dust, a smoke round's trail
                {
                    at = p.From + p.Velocity * age + Vector3.down * (0.5f * p.Gravity * age * age);
                    wear = Sq(Mathf.Clamp01((t - 0.35f) / 0.65f));
                    sx = sy = Mathf.Lerp(0.5f, 1f, EaseOut(t)) * (1f - EatenShrink * wear * wear);
                    break;
                }
            }
            if (p.Use != Use.Flame) p.Foot = new Vector3(at.x, 0f, at.z);
            Quaternion rot = turn != 0f ? face * Quaternion.Euler(0f, 0f, turn) : face;
            p.T.SetPositionAndRotation(at, rot);
            float mirror = p.Mirror ? -1f : 1f;
            if (hidden) p.T.localScale = Vector3.one * 1e-4f;
            else p.T.localScale = new Vector3(mirror * Mathf.Max(1e-3f, p.Half * sx), Mathf.Max(1e-3f, p.Half * sy), 1f);
            p.Wear = hidden ? 1f : Mathf.Clamp01(wear);
            Vector4 shape = p.Block.GetVector(s_Shape);
            shape.x = p.Wear;
            shape.y = cool;
            p.Block.SetVector(s_Shape, shape);
            Vector4 bands = p.Block.GetVector(s_Bands);
            bands.w = roll;
            p.Block.SetVector(s_Bands, bands);
            Vector4 more = p.Block.GetVector(s_More);
            more.x = Mathf.Max(0f, age);
            // Its front's depth: a sphere of its half (a tongue: of its width), so it meets the floor and figures in a curve.
            more.z = p.Style == Style.Star || p.Style == Style.Ember ? 0f : p.Use == Use.Flame ? p.Half * sx : p.Half * Mathf.Max(sx, sy);
            p.Block.SetVector(s_More, more);
            p.Renderer.SetPropertyBlock(p.Block);
        }

        /// <summary>A piece of the drawn look, its style's tones and thresholds set; null when the look is not drawn or the cap is reached.</summary>
        Piece TakeDrawn(Use use, Style style, SkillEffectArt.Shape shape, float life, float half)
        {
            if (!SkillLookValues.EffectsNotShapes || life <= 0f || half <= 0f || !DrawnLook) return null;
            if (_live.Count >= Mathf.Max(1, Values.MostPieces))
            {
                Capped++;
                return null;
            }
            EnsureLooks();
            if (_free.Count == 0) Grow(_pool.Count == 0 ? Mathf.Max(1, Values.PoolStart) : Mathf.Max(1, Values.PoolGrow));
            Piece p = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            Begin(p, use, spark: false, life, half, 1f);
            p.Toon = true;
            p.Style = style;
            p.Seed = Next();
            p.Filter.sharedMesh = SkillEffectArt.Quad(shape);
            p.Renderer.sharedMaterial = _drawn;
            SetStyle(p, style);
            DrawnBy[(int)use]++;
            return p;
        }

        void SetStyle(Piece p, Style style)
        {
            SkillLookValues v = Values;
            MaterialPropertyBlock b = p.Block;
            float alpha = 1f, ink = v.InkPixels, clear = 0f;
            Vector4 flow = Vector4.zero;
            switch (style)
            {
                case Style.Smoke:
                case Style.Dust:
                case Style.Soot:
                {
                    Rgba lit = style == Style.Smoke ? v.SmokeLit : style == Style.Dust ? v.DustLit : v.SootLit;
                    Rgba mid = style == Style.Smoke ? v.SmokeMid : style == Style.Dust ? v.DustMid : v.SootMid;
                    Rgba shade = style == Style.Smoke ? v.SmokeShade : style == Style.Dust ? v.DustShade : v.SootShade;
                    Rgba inkTone = style == Style.Smoke ? v.SmokeInk : style == Style.Dust ? v.DustInk : v.SootInk;
                    b.SetVector(s_Tone0, Lit(lit));
                    b.SetVector(s_Tone1, Lit(mid));
                    b.SetVector(s_Tone2, Lit(shade));
                    b.SetVector(s_Tone3, Lit(shade));
                    b.SetVector(s_Ink, Lit(inkTone));
                    b.SetVector(s_Bands, new Vector4(v.LitBand, v.ShadeBand, -10f, 0f));
                    if (style == Style.Smoke && p.Use == Use.Smoke)
                    {
                        alpha = Mathf.Clamp01(v.SmokeThickness);
                        clear = 1f;
                    }
                    break;
                }
                case Style.Fire:
                    b.SetVector(s_Tone0, Lit(v.FireCoreTone));
                    b.SetVector(s_Tone1, Lit(v.FireYellow));
                    b.SetVector(s_Tone2, Lit(v.FireOrange));
                    b.SetVector(s_Tone3, Lit(v.FireRed));
                    b.SetVector(s_Ink, Lit(v.FireInk));
                    b.SetVector(s_Bands, new Vector4(0.7f, 0.47f, 0.25f, 0f));
                    ink = v.FireInkPixels;
                    flow = new Vector4(1.6f, 0.22f, 0.6f, 0.1f); // up the flame, its heat's bands wobbling, its edge licked
                    break;
                case Style.Ember:
                    b.SetVector(s_Tone0, Lit(v.FireCoreTone));
                    b.SetVector(s_Tone1, Lit(v.FireYellow));
                    b.SetVector(s_Tone2, Lit(v.FireOrange));
                    b.SetVector(s_Tone3, Lit(v.FireOrange));
                    b.SetVector(s_Ink, Lit(v.FireRed));
                    b.SetVector(s_Bands, new Vector4(0.55f, 0.28f, -10f, 0f));
                    ink = 1f;
                    break;
                case Style.Star:
                    b.SetVector(s_Ink, Vector4.zero);
                    b.SetVector(s_Bands, new Vector4(0.5f, -10f, -10f, 0f));
                    ink = 0f;
                    break;
            }
            b.SetVector(s_Shape, new Vector4(0f, 0f, ink, alpha));
            // Where it is eaten toward, and how raggedly: a fire ball a little under its middle, a tongue toward its foot.
            b.SetVector(s_Cut, style == Style.Star || style == Style.Ember ? Vector4.zero
                             : p.Use == Use.Flame ? new Vector4(0f, -0.7f, 0.25f, 0f)
                             : style == Style.Fire ? new Vector4(0f, -0.15f, 0.3f, 0f)
                             : new Vector4(0f, 0f, 0.35f, 0f));
            b.SetVector(s_Flow, flow);
            b.SetVector(s_More, new Vector4(0f, p.Seed * 7.3f, 0f, clear));
        }

        /// <summary>A fire's warm light (work order 126): up while the blast lasts, falling as the fire burns out, flickering.</summary>
        void LightUp(Vector3 c, float radius, float blast, float life)
        {
            SkillLookValues v = Values;
            if (v.FireLight <= 0f) return;
            Glow g = null;
            foreach (Glow x in _glows) if (!x.Live) { g = x; break; }
            if (g == null)
            {
                if (_glows.Count >= MostGlows) return;
                var go = new GameObject("SkillEffects_FireLight");
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                g = new Glow { Light = light };
                _glows.Add(g);
                Made++;
            }
            g.Live = true;
            g.Born = Time.time;
            g.Blast = blast;
            g.Life = life;
            g.Peak = v.FireLight;
            g.Seed = Next() * 10f;
            g.Light.transform.position = c + Vector3.up * v.FireLightHeight;
            g.Light.range = radius + Mathf.Max(0f, v.FireLightReach);
            Color warm = Picked(v.FireOrange) * 0.5f + Picked(v.FireCoreTone) * 0.5f;
            g.Light.color = new Color(warm.r, warm.g, warm.b, 1f);
            g.Light.intensity = 0f;
            g.Light.enabled = true;
        }

        void GlowsNow(float now, Camera cam)
        {
            GlowsLit = 0;
            GlowBrightest = 0f;
            SkillLookValues v = Values;
            for (int i = 0; i < _glows.Count; i++)
            {
                Glow g = _glows[i];
                if (!g.Live) continue;
                float age = now - g.Born;
                if (age >= g.Life || !SkillLookValues.EffectsNotShapes)
                {
                    g.Live = false;
                    g.Light.enabled = false;
                    continue;
                }
                float up = Mathf.Clamp01(age / 0.04f);
                float down = age <= g.Blast ? 1f : 1f - (age - g.Blast) / Mathf.Max(1e-3f, g.Life - g.Blast);
                float flicker = 1f - Mathf.Clamp01(v.FireLightFlicker) * (0.5f + 0.25f * Mathf.Sin(age * 43f + g.Seed) + 0.25f * Mathf.Sin(age * 71f + g.Seed * 2f));
                g.Light.intensity = g.Peak * up * down * down * flicker;
                if (cam != null) g.Light.transform.rotation = cam.transform.rotation; // a point light: facing changes nothing, kept as the pieces are
                GlowsLit++;
                GlowBrightest = Mathf.Max(GlowBrightest, g.Light.intensity);
            }
        }

        SkillEffectArt.Shape PuffShape() => (SkillEffectArt.Shape)((int)SkillEffectArt.Shape.Puff0 + (int)(Next() * SkillEffectArt.PuffShapes) % SkillEffectArt.PuffShapes);
        SkillEffectArt.Shape FireShape() => (SkillEffectArt.Shape)((int)SkillEffectArt.Shape.Fire0 + (int)(Next() * SkillEffectArt.FireShapes) % SkillEffectArt.FireShapes);

        static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);
        static float Smooth(float k)
        {
            float c = Mathf.Clamp01(k);
            return c * c * (3f - 2f * c);
        }
        static float Sq(float x) => x * x;

        /// <summary>A colour as picked, in the space the shader adds in, times its glow.</summary>
        static Vector4 Lit(Rgba c) => Lit(new Color(c.R, c.G, c.B, 1f), 1f);

        static Vector4 Lit(Color c, float glow)
        {
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            float k = Mathf.Max(0f, glow);
            return new Vector4(l.r * k, l.g * k, l.b * k, 1f);
        }

        // ---------------- The pool ----------------

        Piece Take(Use use, bool spark, float life, float half, float alpha)
        {
            if (!SkillLookValues.EffectsNotShapes || life <= 0f || half <= 0f) return null;
            if (_live.Count >= Mathf.Max(1, Values.MostPieces))
            {
                Capped++;
                return null;
            }
            EnsureLooks();
            if (_free.Count == 0) Grow(_pool.Count == 0 ? Mathf.Max(1, Values.PoolStart) : Mathf.Max(1, Values.PoolGrow));
            Piece p = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            Begin(p, use, spark, life, half, alpha);
            p.Filter.sharedMesh = spark ? _spark : _puffs[(Pooled + _live.Count + (int)use) % _puffs.Length];
            p.Renderer.sharedMaterial = spark ? SparkMaterial : _puffMaterial;
            if (spark) p.Renderer.SetPropertyBlock(null);
            DrawnBy[(int)use]++;
            return p;
        }

        static void Begin(Piece p, Use use, bool spark, float life, float half, float alpha)
        {
            p.Live = true;
            p.Use = use;
            p.Spark = spark;
            p.Toon = false;
            p.Cloud = -1;
            p.Born = Time.time;
            p.Life = life;
            p.Half = half;
            p.Alpha = alpha;
            p.AlphaSet = -1f;
            p.Spin = 0f;
            p.Gravity = 0f;
            p.Velocity = Vector3.zero;
            p.SmokeUntil = float.PositiveInfinity;
            p.Grow = 1f;
            p.Share = 1f;
            p.Speed = p.Drag = p.Blast = p.Late = p.Wear = 0f;
            p.HalfTall = half;
            p.Mirror = false;
            p.Out = Vector3.zero;
        }

        void Ring(Piece p, Vector3 centre, float degrees, float reach, float height, float arrive)
        {
            float a = degrees * Mathf.Deg2Rad;
            p.Centre = centre;
            p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            p.Reach = Mathf.Max(0f, reach);
            p.Height = height;
            p.Arrive = arrive;
            Place(p);
        }

        void Place(Piece p)
        {
            _live.Add(p);
            float now = Time.time;
            float age = now - p.Born;
            if (p.Toon) PoseDrawn(p, age, age / Mathf.Max(1e-4f, p.Life), now, Camera.main);
            else Pose(p, 0f, 0f, now, Camera.main);
            p.Go.SetActive(true);
        }

        void Return(Piece p, int index)
        {
            p.Live = false;
            p.Cloud = -1;
            p.Go.SetActive(false);
            int last = _live.Count - 1;
            _live[index] = _live[last];
            _live.RemoveAt(last);
            _free.Add(p);
        }

        void Grow(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new Piece();
                p.Go = new GameObject("SkillEffects_" + (_pool.Count + 1));
                p.T = p.Go.transform;
                p.T.SetParent(transform, false);
                p.Filter = p.Go.AddComponent<MeshFilter>();
                p.Renderer = p.Go.AddComponent<MeshRenderer>();
                p.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                p.Renderer.receiveShadows = false;
                p.Renderer.lightProbeUsage = LightProbeUsage.Off;
                p.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                p.Block = new MaterialPropertyBlock();
                p.Go.SetActive(false);
                Made++;
                _pool.Add(p);
                _free.Add(p);
            }
        }

        Barrel MakeBarrel(string name)
        {
            EnsureLooks();
            var b = new Barrel();
            b.Go = new GameObject(name);
            b.T = b.Go.transform;
            b.T.SetParent(transform, false);
            b.Filter = b.Go.AddComponent<MeshFilter>();
            b.Filter.sharedMesh = _keg;
            b.Renderer = b.Go.AddComponent<MeshRenderer>();
            b.Renderer.sharedMaterial = SparkMaterial;
            b.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            b.Renderer.receiveShadows = false;
            b.Go.SetActive(false);
            Made++;
            return b;
        }

        /// <summary>What every piece is drawn with, made the first time one is wanted.</summary>
        void EnsureLooks()
        {
            if (_puffs != null) return;
            SkillLookValues v = Values;
            _puffs = new[] { BuildPuff(0), BuildPuff(1), BuildPuff(2) };
            _spark = BuildSpark(Drawn(v.SparkRim, v.SparkGlow), Drawn(v.SparkCore, v.SparkGlow));
            _keg = BuildBarrel("SkillEffects_KegMesh", v.KegRadius, v.KegHeight, Drawn(v.KegWood, 1f), Drawn(v.KegHoop, 1f));
            _canister = BuildBarrel("SkillEffects_RoundMesh", v.KegRadius * 0.45f, v.KegHeight * 0.55f, Drawn(v.RoundShell, 1f), Drawn(v.KegHoop, 1f));
            Made += 6;
            Shader shader = Shader.Find(MoodShapes.UnlitShaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            // The puffs: unlit, blended by alpha, no depth written, both sides (as the smoke cloud's discs were, work order 109).
            _puffMaterial = new Material(shader) { name = "SkillEffects_Puff" };
            if (_puffMaterial.HasProperty("_BaseColor")) _puffMaterial.SetColor("_BaseColor", Color.white);
            _puffMaterial.SetFloat("_Surface", 1f);
            _puffMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _puffMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _puffMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            _puffMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            _puffMaterial.SetFloat("_ZWrite", 0f);
            _puffMaterial.SetFloat("_Cull", (float)CullMode.Off);
            _puffMaterial.renderQueue = (int)RenderQueue.Transparent;
            // Its pictures of 2026-11-25 drew the puffs solid with the floats alone: the shader blends by the keyword, as the
            // material inspector sets it, with no depth pass and the transparent tag.
            _puffMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _puffMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _puffMaterial.DisableKeyword("_ALPHAMODULATE_ON");
            _puffMaterial.SetOverrideTag("RenderType", "Transparent");
            _puffMaterial.SetShaderPassEnabled("DepthOnly", false);
            _puffMaterial.SetShaderPassEnabled("SHADOWCASTER", false);
            _ownSpark = new Material(shader) { name = "SkillEffects_Spark" };
            // Work order 126: the drawn look's one material (its textures made once for the run); each piece's values in its block.
            if (SkillEffectArt.Ready)
            {
                _drawn = SkillEffectArt.MakeMaterial();
                Made++;
            }
        }

        /// <summary>The sparks and the barrels: the shot effects' material (unlit, its vertices' colours), as every shot effect is drawn; one of its own without her.</summary>
        Material SparkMaterial => _player != null && _player.ShotFxMaterial != null ? _player.ShotFxMaterial : _ownSpark;

        // ---------------- For tests and tools ----------------

        /// <summary>The pieces live now, into a list the caller owns.</summary>
        public void ListPieces(List<Shown> into)
        {
            into.Clear();
            foreach (Piece p in _live)
                into.Add(new Shown
                {
                    Use = p.Use, Position = p.T.position, Half = Mathf.Abs(p.T.localScale.x), Age = Time.time - p.Born, Life = p.Life,
                    Alpha = p.Spark ? 1f : p.Toon ? 1f - p.Wear : p.AlphaSet, Cloud = p.Cloud, Centre = p.Centre, Reach = p.Reach,
                    Drawn = p.Go.activeSelf && p.Renderer.enabled,
                    Foot = p.Foot, HalfTall = Mathf.Abs(p.T.localScale.y), Worn = p.Wear, Toon = p.Toon,
                });
        }

        // ---------------- Meshes, made once ----------------

        /// <summary>The share of a puff's radius that is fully its colour; past it the alpha falls to nothing at its edge.</summary>
        const float PuffSolid = 0.55f;

        static Color Picked(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        static Color Drawn(Rgba picked, float glow)
        {
            Color p = Picked(picked);
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        float Next()
        {
            // A small fixed stream: the same pattern each run, no allocation, not Unity's own random (the game's draws stay as they were).
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            return (_seed % 10007) / 10007f;
        }

        /// <summary>
        /// A soft, lumpy puff: a disc of radius at most 1 in the local XY plane, white, its alpha full out to <see cref="PuffSolid"/> of
        /// its radius and falling to none at its edge.
        /// </summary>
        static Mesh BuildPuff(int variant)
        {
            const int edge = 18;
            var vertices = new Vector3[1 + edge * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[edge * 18];
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;
            int t = 0;
            for (int i = 0; i < edge; i++)
            {
                float a = i * Mathf.PI * 2f / edge;
                // Lumps no farther out than 1: the puff never reaches past the half it is scaled to.
                float r = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(a * (3 + variant) + variant * 1.7f));
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[1 + i] = d * (r * PuffSolid);
                colors[1 + i] = Color.white;
                vertices[1 + edge + i] = d * r;
                colors[1 + edge + i] = new Color(1f, 1f, 1f, 0f);
                int i0 = 1 + i, i1 = 1 + (i + 1) % edge, o0 = i0 + edge, o1 = i1 + edge;
                foreach (int k in new[] { 0, i0, i1, 0, i1, i0, i0, o0, o1, i0, o1, i1, i0, o1, o0, i0, i1, o1 }) triangles[t++] = k;
            }
            var mesh = new Mesh { name = "SkillEffects_Puff" + variant };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A small four-pointed star of radius 1, a rim and a core over it, both sides.</summary>
        static Mesh BuildSpark(Color rim, Color core)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                float k = layer == 0 ? 1f : 0.5f;
                float z = layer == 0 ? 0f : -0.01f;
                Color col = layer == 0 ? rim : core;
                int b = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                c.Add(col);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float r = (i % 2 == 0 ? 1f : 0.35f) * k;
                    v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
                    c.Add(col);
                    t.Add(b); t.Add(b + 1 + i); t.Add(b + 1 + (i + 1) % 8);
                    t.Add(b); t.Add(b + 1 + (i + 1) % 8); t.Add(b + 1 + i);
                }
            }
            var mesh = new Mesh { name = "SkillEffects_Spark" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A small barrel standing on its foot at the origin: ten staves, a little wider at its middle, two dark hoops, a lid. Unlit,
        /// so its sides are shaded in its colours (lighter toward the room's usual key light, from the upper left).
        /// </summary>
        static Mesh BuildBarrel(string name, float radius, float height, Color wood, Color hoop)
        {
            const int sides = 10;
            float[] heights = { 0f, 0.14f, 0.2f, 0.5f, 0.8f, 0.86f, 1f };
            float[] widths = { 0.9f, 0.96f, 0.98f, 1.06f, 0.98f, 0.96f, 0.9f };
            bool[] hoops = { true, true, false, false, true, true, false };
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            Vector3 light = new Vector3(-0.5f, 0.4f, 0.75f).normalized;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 normal = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                float shade = 0.6f + 0.4f * Mathf.Max(0f, Vector3.Dot(normal, light));
                for (int h = 0; h + 1 < heights.Length; h++)
                {
                    Color col = (hoops[h] ? hoop : wood) * shade;
                    col.a = 1f;
                    int b = v.Count;
                    float y0 = heights[h] * height, y1 = heights[h + 1] * height, r0 = widths[h] * radius, r1 = widths[h + 1] * radius;
                    v.Add(new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0));
                    v.Add(new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0));
                    v.Add(new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1));
                    v.Add(new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1));
                    c.Add(col); c.Add(col); c.Add(col); c.Add(col);
                    t.Add(b); t.Add(b + 2); t.Add(b + 1);
                    t.Add(b); t.Add(b + 3); t.Add(b + 2);
                }
            }
            // The lid: lighter wood, its end grain.
            int centre = v.Count;
            Color lid = wood * 1.15f;
            lid.a = 1f;
            v.Add(new Vector3(0f, height, 0f));
            c.Add(lid);
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Cos(a) * radius * 0.9f, height, Mathf.Sin(a) * radius * 0.9f));
                c.Add(lid);
            }
            for (int s = 0; s < sides; s++)
            {
                t.Add(centre); t.Add(centre + 1 + (s + 1) % sides); t.Add(centre + 1 + s);
            }
            // Both sides, as every shot effect's mesh is: it is seen right whichever way the material culls.
            int n = t.Count;
            for (int i = 0; i < n; i += 3)
            {
                t.Add(t[i]);
                t.Add(t[i + 2]);
                t.Add(t[i + 1]);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
