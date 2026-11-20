using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The switch of the things that fly and how they look (work order 036, 2026-11-20; decision 0010), defaults here in
    /// code: no asset holds them. Their speeds, sizes, flights and damage are their kinds' (<see cref="ShooterValues"/>,
    /// <see cref="CasterValues"/>, <see cref="BossValues"/>). All first guesses; nobody has seen them yet.
    /// </summary>
    [Serializable]
    public sealed class FlyValues
    {
        /// <summary>
        /// Off: the shooter, the caster and the boss fight exactly as before work order 036 (the shooter's line and the
        /// caster's circles on the floor, no wave). Read live, at each wind-up, by the floors' enemies, their elites and the
        /// practice room's alike.
        /// </summary>
        public bool On = true;

        [Header("The pool: made once, then lent out")]
        public int PoolStart = 32;
        public int PoolGrow = 16;

        [Header("How they look, seen from 28 m up")]
        // Work order 039 (Kristian's second session of 2026-11-20: in a room of eight he did not see the release, and half a
        // second later it was there): the arrow larger and brighter than anything near it that is not hers, with a long
        // fading tail, and a short bright flash at the bow as it leaves, so the eye goes to where it starts. Its hit size
        // is its kind's (ShooterValues.ArrowRadius, 0.35 m), as before. It was 1.6 m by 0.16 m, at the shot effects' glow.
        public float ArrowLength = 2.4f;      // m, head to the end of its tail
        public float ArrowWidth = 0.3f;       // m
        public float ArrowTail = 0.75f;       // share of its length that is the tail, fading to nearly nothing at its end
        public float ArrowRimGlow = 2.6f;     // its rim's and core's glow: above the shot effects' (1.8 and 4), the orb's and
        public float ArrowCoreGlow = 5.5f;    // the wave's; under the counter's brightest moment (7)
        public float ArrowHeight = 0.9f;      // m over the ground, about the bow
        public float OrbHeight = 1f;          // m over the ground
        public float OrbPulse = 0.08f;        // share its size swells and shrinks by
        public float OrbPulseSeconds = 0.7f;  // s for one swell and shrink
        public float OrbRingWidth = 0.1f;     // m, the faint ring on the floor under it
        public float WaveHeight = 0.1f;       // m over the ground: low
        public float WaveBulge = 0.8f;        // m its middle runs ahead of its ends
        public float PuffSize = 0.55f;        // m, the puff where a wall stops one
        public float PuffSeconds = 0.18f;
        public float FadeSeconds = 0.12f;     // s an orb or a wave takes to go at the end of its flight
        public float RimGlow = ShotFxSettings.StartRimGlow;
        public float CoreGlow = ShotFxSettings.StartCoreGlow;
        public float RingGlow = 1.1f;

        [Header("The flash at the bow as an arrow leaves (work order 039)")]
        public float FlashSize = 0.9f;        // m, its radius at its largest
        public float FlashSeconds = 0.14f;    // s, up quickly, then down to nothing
        public float FlashGlow = 6f;          // its core's glow; its rim at the arrow's rim glow
        public int FlashPoolStart = 8;
        public int FlashPoolGrow = 4;

        [Header("Colours: rim, then core")]
        public Color ArrowRim = new Color(0.9f, 0.88f, 0.8f);     // as the effects' arrow (CombatFxValues)
        public Color ArrowCore = new Color(1f, 1f, 0.97f);
        public Color OrbRim = new Color(0.86f, 0.8f, 0.66f);      // bone, as the effects' shards
        public Color OrbCore = new Color(1f, 0.97f, 0.9f);
        public Color WaveRim = new Color(1f, 0.36f, 0.12f);       // the boss's warm red-orange
        public Color WaveCore = new Color(1f, 0.85f, 0.65f);
        public Color PuffRim = new Color(0.62f, 0.55f, 0.45f);    // dust
        public Color PuffCore = new Color(0.86f, 0.8f, 0.7f);
    }

    /// <summary>
    /// The things that fly at her (work order 036, 2026-11-20; decision 0010, Kristian: "more projectiles", "the game is
    /// about dodging colours on yourself, not dodging what comes at you"): the shooter's arrows, the caster's orbs and the
    /// boss's wave, one kind of thing in the game (<see cref="Projectile"/>, its rules <see cref="ProjectileRules"/>).
    ///
    /// Each frame each moves on game time, so a pause and a hit stop hold it and a slowed game slows it. The step is swept:
    /// it meets her (her footprint, as every enemy attack takes her) once, and is gone; walls and props tall enough to stop
    /// shots stop it (<see cref="Cover"/>), the wave only by walls; what she has ducked behind stops it as it stops a shot
    /// today (<see cref="PlayerCharacter.ShieldsFrom"/>); in the dash's protected part it passes her and flies on; it passes
    /// through enemies. Its owner tells how it ended (<see cref="Enemy.ProjectileEnded"/>), also after the owner died; it
    /// is gone with its owner (a floor removed, the practice room cleared), when another floor is built, and when the run
    /// it was loosed in ends.
    ///
    /// Drawn in the shot effects' style (a flat mesh made here, rim and core in colours lifted above white for the bloom,
    /// the shot effects' material), no asset: an arrow a bright streak with a long fading tail, pointing where it flies,
    /// larger and brighter than anything near it that is not hers, and a short bright flash at the bow as it leaves
    /// (work order 039: Kristian could not see one coming; <see cref="FlashAt"/>, pooled as they are);
    /// an orb a pale bone ball, pulsing, and a faint ring on the floor right under it, the only thing drawn on the floor;
    /// the wave a low bright crescent as wide as it hits. A wall stops one with a small puff of dust and no sound.
    ///
    /// Pooled: <see cref="FlyValues.PoolStart"/> are made when it starts and lent out; past that it grows by
    /// <see cref="FlyValues.PoolGrow"/>. Nothing is made per shot or per frame once warm.
    ///
    /// It lives in the scene (a new scene takes it and all that flies with it) and holds the switch,
    /// <see cref="FlyValues.On"/>; it installs itself when Play starts under the same rule as the play session recorder,
    /// and anything that looses one makes it if there is none (<see cref="Ensure"/>). With none, the switch is on.
    /// </summary>
    public sealed class ThingsThatFly : MonoBehaviour
    {
        static ThingsThatFly s_current;

        /// <summary>The one in the scene now, or null.</summary>
        public static ThingsThatFly Current => s_current != null ? s_current : null;

        /// <summary>The switch as it stands: the one in the scene's, or on when there is none.</summary>
        public static bool On => Current != null ? Current.Values.On : true;

        /// <summary>
        /// Raised as things leave an enemy at its release: the enemy, its attack, how many at once. The placeholder sounds
        /// play its kind's strike at it, and the recorder logs it; each one's end is a strike of its own
        /// (<see cref="Enemy.ProjectileEnded"/>).
        /// </summary>
        public static event Action<Enemy, EnemyAttackDefinition, int> Loosed;

        /// <summary>Tells everyone listening that <paramref name="count"/> of an attack left <paramref name="enemy"/> now.</summary>
        public static void TellLoosed(Enemy enemy, EnemyAttackDefinition attack, int count) => Loosed?.Invoke(enemy, attack, count);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_current = null;
            Loosed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure();
        }

        /// <summary>The one in the scene, made if there is none.</summary>
        public static ThingsThatFly Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("ThingsThatFly");
            return go.AddComponent<ThingsThatFly>();
        }

        public FlyValues Values = new FlyValues();

        /// <summary>What one looks like now, for tests and tools.</summary>
        public struct Shown
        {
            public ProjectileKind Kind;
            public Enemy Owner;
            public string Attack;
            public int Number;          // the how-manieth loosed, from 1
            public Vector3 Position;    // on the ground
            public Vector3 Direction;
            public float Flown;
            public bool Passed;         // it met her and passed (the dash, or spared)
            public bool Ending;         // stopped or out of flight, its puff or fade still showing
            public bool Drawn;          // its picture is on
            public bool RingDrawn;      // an orb's ring on the floor is on
            public Vector3 Scale;       // its picture's scale: an arrow's width, 1, its length (work order 039)
        }

        sealed class Slot
        {
            public Projectile P;
            public bool Live;
            public int Number;
            public Enemy Owner;
            public EnemyAttackDefinition Attack;
            public GeneratedLevel Level;
            public bool InRun;
            public bool Spares;        // the boss's wave whose Cleave hit her: it passes her
            public bool Told;          // its end is told: it only flies on to where it stops
            public float Age;
            public float EndingAt = -1f;
            public bool Puff;
            public GameObject Go;
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public GameObject RingGo;
            public Transform RingT;
            public MeshRenderer RingRenderer;
        }

        /// <summary>A flash at the bow (work order 039): it does not fly, and is not one of what flies.</summary>
        sealed class Flash
        {
            public bool Live;
            public float Age;
            public int Frame;           // the frame it began: it is not aged in that frame, so even a long one shows it
            public Vector3 At;
            public float Yaw;
            public GameObject Go;
            public Transform T;
            public MeshRenderer Renderer;
        }

        /// <summary>What a flash looks like now, for tests and tools.</summary>
        public struct FlashShown
        {
            public Vector3 Position;    // in the world, at the bow's height
            public float Size;          // m, its radius now
            public bool Drawn;
        }

        readonly List<Slot> _slots = new List<Slot>();
        readonly List<Flash> _flashes = new List<Flash>();
        CoverLine _wallsOnly;
        PlayerCharacter _player;
        Material _material;
        Mesh _arrow, _orb, _ring, _wave, _puff, _flash;
        GroundPoint _herLast;
        bool _herKnown;
        static readonly RaycastHit[] s_hits = new RaycastHit[32];

        /// <summary>Live now: flying, or showing their puff or fade.</summary>
        public int Live { get; private set; }
        /// <summary>The pool's size: every slot made so far, free or lent.</summary>
        public int Pooled => _slots.Count;
        /// <summary>
        /// Objects made for the pools since it started (two a slot, one a flash): for the test that nothing is made per shot
        /// once warm.
        /// </summary>
        public int Made { get; private set; }
        /// <summary>Loosed since it started.</summary>
        public int Fired { get; private set; }
        /// <summary>Flashes at the bow shown since it started, those showing now, and the flash pool's size (work order 039).</summary>
        public int Flashed { get; private set; }
        public int FlashesLive { get; private set; }
        public int FlashesPooled => _flashes.Count;
        /// <summary>The arrow's mesh, for tests.</summary>
        public Mesh ArrowMesh => _arrow;

        void Awake()
        {
            if (s_current != null && s_current != this)
            {
                Destroy(this);
                return;
            }
            s_current = this;
            _wallsOnly = WallsOnly;
            BuildMeshes();
            Grow(Mathf.Max(1, Values.PoolStart));
            GrowFlashes(Mathf.Max(1, Values.FlashPoolStart));
        }

        void OnDestroy()
        {
            if (s_current == this) s_current = null;
            foreach (Mesh m in new[] { _arrow, _orb, _ring, _wave, _puff, _flash })
                if (m != null) Destroy(m);
        }

        // ---------------- Loosing ----------------

        /// <summary>
        /// Looses one for <paramref name="owner"/>, of its <paramref name="attack"/>, as <paramref name="p"/> says. It
        /// belongs to the floor and the run there are now. <paramref name="sparesHer"/>: it passes her without a hit (the
        /// boss's wave when its Cleave had hit her). Returns its number.
        /// </summary>
        public int Fire(Enemy owner, EnemyAttackDefinition attack, Projectile p, bool sparesHer = false)
        {
            if (owner == null || attack == null) return 0;
            if (_player == null) _player = owner.Player;
            if (_material == null && owner.Player != null) _material = owner.Player.ShotFxMaterial;
            Slot s = Free();
            GeneratedLevels levels = GeneratedLevels.Current;
            s.P = p;
            s.Live = true;
            s.Number = ++Fired;
            s.Owner = owner;
            s.Attack = attack;
            s.Level = levels != null ? levels.Level : null;
            s.InRun = levels != null && levels.InRun;
            s.Spares = sparesHer;
            s.Told = false;
            s.Age = 0f;
            s.EndingAt = -1f;
            s.Puff = false;
            s.Filter.sharedMesh = p.Kind == ProjectileKind.Arrow ? _arrow : p.Kind == ProjectileKind.Orb ? _orb : _wave;
            if (_material != null && s.Renderer.sharedMaterial != _material)
            {
                s.Renderer.sharedMaterial = _material;
                s.RingRenderer.sharedMaterial = _material;
            }
            s.Renderer.enabled = _material != null;
            s.RingGo.SetActive(p.Kind == ProjectileKind.Orb);
            s.RingRenderer.enabled = _material != null;
            s.Go.SetActive(true);
            Live++;
            Draw(s, Camera.main);
            return s.Number;
        }

        /// <summary>Everything that flies is gone at once, without a word to its owners; and every flash at a bow.</summary>
        public void Clear()
        {
            foreach (Slot s in _slots)
                if (s.Live) Return(s);
            foreach (Flash f in _flashes)
                if (f.Live) Return(f);
        }

        /// <summary>
        /// A short bright flash at the bow as arrows leave it (work order 039: in a room of eight the eye must be drawn to
        /// where one starts), at <paramref name="ground"/> on the ground, at the arrow's height; one for a fan's three.
        /// </summary>
        public void FlashAt(Vector3 ground, Enemy owner = null)
        {
            if (_material == null && owner != null && owner.Player != null) _material = owner.Player.ShotFxMaterial;
            Flash f = null;
            foreach (Flash x in _flashes)
                if (!x.Live) { f = x; break; }
            if (f == null)
            {
                int before = _flashes.Count;
                GrowFlashes(Mathf.Max(1, Values.FlashPoolGrow));
                f = _flashes[before];
            }
            f.Live = true;
            f.Age = 0f;
            f.Frame = Time.frameCount;
            f.At = new Vector3(ground.x, Values.ArrowHeight + 0.02f, ground.z);
            f.Yaw = (Flashed * 37) % 360;
            Flashed++;
            FlashesLive++;
            if (_material != null && f.Renderer.sharedMaterial != _material) f.Renderer.sharedMaterial = _material;
            f.Renderer.enabled = _material != null;
            f.Go.SetActive(true);
            DrawFlash(f);
        }

        /// <summary>The flashes showing now, into a list the caller owns: for tests and tools.</summary>
        public void ListFlashes(List<FlashShown> into)
        {
            into.Clear();
            foreach (Flash f in _flashes)
            {
                if (!f.Live) continue;
                into.Add(new FlashShown { Position = f.T.position, Size = f.T.localScale.x, Drawn = f.Go.activeSelf && f.Renderer.enabled });
            }
        }

        void GrowFlashes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var f = new Flash();
                f.Go = new GameObject("Flash_" + (_flashes.Count + 1));
                f.T = f.Go.transform;
                f.T.SetParent(transform, false);
                f.Go.AddComponent<MeshFilter>().sharedMesh = _flash;
                f.Renderer = f.Go.AddComponent<MeshRenderer>();
                f.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                f.Renderer.receiveShadows = false;
                f.Go.SetActive(false);
                Made++;
                _flashes.Add(f);
            }
        }

        void Return(Flash f)
        {
            if (!f.Live) return;
            f.Live = false;
            f.Go.SetActive(false);
            FlashesLive = Mathf.Max(0, FlashesLive - 1);
        }

        void TickFlashes(float dt)
        {
            if (FlashesLive == 0) return;
            foreach (Flash f in _flashes)
            {
                if (!f.Live || f.Frame == Time.frameCount) continue;
                f.Age += dt;
                if (f.Age >= Values.FlashSeconds) Return(f);
                else DrawFlash(f);
            }
        }

        /// <summary>Up to its full size in the first quarter of its time, then down to nothing.</summary>
        void DrawFlash(Flash f)
        {
            float t = Mathf.Clamp01(f.Age / Mathf.Max(1e-3f, Values.FlashSeconds));
            float k = t < 0.25f ? Mathf.Lerp(0.5f, 1f, t / 0.25f) : 1f - (t - 0.25f) / 0.75f;
            f.T.SetPositionAndRotation(f.At, Quaternion.Euler(0f, f.Yaw, 0f));
            f.T.localScale = Vector3.one * Mathf.Max(1e-3f, Values.FlashSize * k);
        }

        /// <summary>What flies now, into a list the caller owns: for tests and tools.</summary>
        public void ListFlying(List<Shown> into)
        {
            into.Clear();
            foreach (Slot s in _slots)
            {
                if (!s.Live) continue;
                into.Add(new Shown
                {
                    Kind = s.P.Kind, Owner = s.Owner, Attack = s.Attack != null ? s.Attack.Name : null, Number = s.Number,
                    Position = new Vector3(s.P.At.X, 0f, s.P.At.Z), Direction = new Vector3(s.P.Dir.X, 0f, s.P.Dir.Z),
                    Flown = s.P.Flown, Passed = s.P.PassedHer, Ending = s.EndingAt >= 0f,
                    Drawn = s.Go.activeSelf && s.Renderer.enabled, RingDrawn = s.RingGo.activeSelf && s.RingRenderer.enabled,
                    Scale = s.T.localScale,
                });
            }
        }

        Slot Free()
        {
            foreach (Slot s in _slots)
                if (!s.Live) return s;
            int before = _slots.Count;
            Grow(Mathf.Max(1, Values.PoolGrow));
            return _slots[before];
        }

        void Grow(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var s = new Slot();
                s.Go = new GameObject("Flying_" + (_slots.Count + 1));
                s.T = s.Go.transform;
                s.T.SetParent(transform, false);
                s.Filter = s.Go.AddComponent<MeshFilter>();
                s.Renderer = s.Go.AddComponent<MeshRenderer>();
                s.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                s.Renderer.receiveShadows = false;
                // The orb's ring on the floor: its own object beside it, so it lies flat while the orb faces the camera.
                s.RingGo = new GameObject("Flying_" + (_slots.Count + 1) + "_Ring");
                s.RingT = s.RingGo.transform;
                s.RingT.SetParent(s.T, false);
                s.RingGo.AddComponent<MeshFilter>().sharedMesh = _ring;
                s.RingRenderer = s.RingGo.AddComponent<MeshRenderer>();
                s.RingRenderer.shadowCastingMode = ShadowCastingMode.Off;
                s.RingRenderer.receiveShadows = false;
                s.Go.SetActive(false);
                Made += 2;
                _slots.Add(s);
            }
        }

        void Return(Slot s)
        {
            if (!s.Live) return;
            s.Live = false;
            s.Owner = null;
            s.Attack = null;
            s.Level = null;
            s.Go.SetActive(false);
            Live = Mathf.Max(0, Live - 1);
        }

        // ---------------- Every frame ----------------

        void Update()
        {
            TickFlashes(Time.deltaTime);
            if (_player == null) _herKnown = false;
            GroundPoint herTo = _player != null ? _player.GroundPosition : default;
            if (Live == 0)
            {
                _herLast = herTo;
                _herKnown = _player != null;
                return;
            }
            float dt = Time.deltaTime;
            // Where she was at the last frame: the step sweeps her from there. A jump farther than a dash in one frame is
            // a teleport, not a run.
            GroundPoint herFrom = _herKnown && _herLast.DistanceTo(herTo) <= 8f ? _herLast : herTo;
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            bool inRun = levels != null && levels.InRun;
            Camera cam = Camera.main;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (!s.Live) continue;
                // Gone with its owner, with the floor it was loosed on (by reference: a destroyed floor is not "no floor"),
                // and with the run it was loosed in.
                if (s.Owner == null || !ReferenceEquals(s.Level, level) || (s.InRun && !inRun))
                {
                    Return(s);
                    continue;
                }
                Tick(s, dt, herFrom, herTo, cam);
            }
            _herLast = herTo;
            _herKnown = _player != null;
        }

        void Tick(Slot s, float dt, GroundPoint herFrom, GroundPoint herTo, Camera cam)
        {
            s.Age += dt;
            if (s.EndingAt >= 0f)
            {
                float span = s.Puff ? Values.PuffSeconds : Values.FadeSeconds;
                if (s.Age - s.EndingAt >= span) Return(s);
                else Draw(s, cam);
                return;
            }
            if (dt <= 0f)
            {
                Draw(s, cam);
                return;
            }
            float left = dt;
            for (int pass = 0; pass < 3 && left > 0f; pass++)
            {
                // What she is in cover against stops it as it stops a shot today, when it covers her from where it left.
                // The wave runs along the ground under it: props do not stop it.
                float coverEntry = float.PositiveInfinity;
                if (!s.Told && s.P.Kind != ProjectileKind.Wave && _player != null && _player.IsInCover)
                {
                    var origin = new Vector3(s.P.Origin.X, 0f, s.P.Origin.Z);
                    if (_player.ShieldsFrom(origin))
                        coverEntry = _player.CoverEntry(new Vector3(s.P.At.X, 0f, s.P.At.Z), new Vector3(s.P.Dir.X, 0f, s.P.Dir.Z),
                                                        s.P.Speed * left + 1f);
                }
                CoverLine walls = s.P.Kind == ProjectileKind.Wave ? _wallsOnly : (CoverLine)Cover.Line;
                ProjectileStep step = ProjectileRules.Step(ref s.P, left, herFrom, herTo, Placeholders.PlayerRadius, walls, coverEntry, s.Told);
                switch (step.Hit)
                {
                    case ProjectileHit.None:
                        left = 0f;
                        break;
                    case ProjectileHit.MetHer:
                    {
                        left *= 1f - step.Fraction;
                        herFrom = herFrom + (herTo - herFrom) * step.Fraction;
                        s.P.PassedHer = true;
                        if (s.Spares)
                        {
                            // Its swing's Cleave hurt her already: the wave passes her (work order 036).
                            Tell(s, ProjectileEnd.Spared);
                            break;
                        }
                        if (_player != null && _player.IsInvulnerable)
                        {
                            // The dash's protected part lets it pass, as with every strike: it flies on.
                            Tell(s, ProjectileEnd.MetHer);
                            break;
                        }
                        Tell(s, ProjectileEnd.MetHer);
                        Return(s); // its hit is the strike's effect on her, and her hurt's sound
                        return;
                    }
                    case ProjectileHit.Wall:
                        Tell(s, ProjectileEnd.StoppedByWall);
                        End(s, puff: true, cam);
                        return;
                    case ProjectileHit.HerCover:
                        Tell(s, step.WouldHaveMetHer ? ProjectileEnd.StoppedByCover : ProjectileEnd.StoppedByWall);
                        End(s, puff: true, cam);
                        return;
                    case ProjectileHit.FlightOver:
                        Tell(s, ProjectileEnd.FlightOver);
                        if (s.P.Kind == ProjectileKind.Arrow) Return(s);
                        else End(s, puff: false, cam);
                        return;
                }
            }
            Draw(s, cam);
        }

        /// <summary>Its end, told to its owner once; whatever it does after is only seen.</summary>
        void Tell(Slot s, ProjectileEnd end)
        {
            if (s.Told) return;
            s.Told = true;
            if (s.Owner != null) s.Owner.ProjectileEnded(s.Attack, end);
        }

        void End(Slot s, bool puff, Camera cam)
        {
            s.EndingAt = s.Age;
            s.Puff = puff;
            if (puff)
            {
                s.Filter.sharedMesh = _puff;
                s.RingGo.SetActive(false);
            }
            Draw(s, cam);
        }

        /// <summary>
        /// The wave's walls (work order 036: walls stop it, props do not): the cover query, counting only what is not a
        /// prop. A level's wall is a primitive box with its mesh on it; a prop carries its footprint's collider on an object
        /// of its own, its model under it (<see cref="PropFactory"/>).
        /// </summary>
        float WallsOnly(GroundPoint from, GroundPoint to)
        {
            Vector3 d = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
            float length = d.magnitude;
            if (length < 1e-5f || !Cover.Any) return length;
            Vector3 start = new Vector3(from.X, Cover.RayHeight, from.Z);
            int n = Physics.RaycastNonAlloc(start, d / length, s_hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = length;
            for (int i = 0; i < n; i++)
            {
                Collider c = s_hits[i].collider;
                if (c == null || s_hits[i].distance >= nearest) continue;
                Cover cover = c.GetComponentInParent<Cover>();
                if (cover == null || !cover.enabled || !c.TryGetComponent(out MeshFilter _)) continue;
                nearest = s_hits[i].distance;
            }
            return nearest;
        }

        // ---------------- The picture ----------------

        void Draw(Slot s, Camera cam)
        {
            Vector3 ground = new Vector3(s.P.At.X, 0f, s.P.At.Z);
            Vector3 dir = new Vector3(s.P.Dir.X, 0f, s.P.Dir.Z);
            float end = s.EndingAt >= 0f ? Mathf.Clamp01((s.Age - s.EndingAt) / Mathf.Max(1e-3f, s.Puff ? Values.PuffSeconds : Values.FadeSeconds)) : 0f;
            if (s.EndingAt >= 0f && s.Puff)
            {
                // A small puff of dust where it stopped: up quickly, then down to nothing.
                float pop = end < 0.3f ? Mathf.Lerp(0.5f, 1.1f, end / 0.3f) : 1.1f * (1f - (end - 0.3f) / 0.7f);
                float height = s.P.Kind == ProjectileKind.Arrow ? Values.ArrowHeight : s.P.Kind == ProjectileKind.Orb ? Values.OrbHeight : Values.WaveHeight;
                s.T.SetPositionAndRotation(ground + Vector3.up * height, Quaternion.Euler(0f, s.Age * 400f, 0f));
                s.T.localScale = Vector3.one * Mathf.Max(1e-3f, Values.PuffSize * pop * (s.P.Kind == ProjectileKind.Arrow ? 1f : 1.6f));
                return;
            }
            float shrink = 1f - end;
            switch (s.P.Kind)
            {
                case ProjectileKind.Arrow:
                    s.T.SetPositionAndRotation(ground + Vector3.up * Values.ArrowHeight, Quaternion.LookRotation(dir, Vector3.up));
                    s.T.localScale = new Vector3(Values.ArrowWidth, 1f, Values.ArrowLength);
                    break;
                case ProjectileKind.Orb:
                {
                    float pulse = 1f + Values.OrbPulse * Mathf.Sin(s.Age * Mathf.PI * 2f / Mathf.Max(0.05f, Values.OrbPulseSeconds));
                    Vector3 at = ground + Vector3.up * Values.OrbHeight;
                    Vector3 toCamera = cam != null ? cam.transform.position - at : Vector3.up;
                    s.T.SetPositionAndRotation(at, toCamera.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toCamera.normalized, Vector3.up) : Quaternion.identity);
                    s.T.localScale = Vector3.one * Mathf.Max(1e-3f, s.P.Radius * pulse * shrink);
                    // The ring lies on the floor right under it, of its hit size: where it is on the ground is certain.
                    s.RingT.SetPositionAndRotation(ground + Vector3.up * 0.05f, Quaternion.identity);
                    Vector3 parent = s.T.localScale;
                    float r = s.P.Radius * shrink;
                    s.RingT.localScale = new Vector3(r / Mathf.Max(1e-3f, parent.x), 1f / Mathf.Max(1e-3f, parent.y), r / Mathf.Max(1e-3f, parent.z));
                    break;
                }
                default:
                    s.T.SetPositionAndRotation(ground + Vector3.up * Values.WaveHeight, Quaternion.LookRotation(dir, Vector3.up));
                    s.T.localScale = new Vector3(Mathf.Max(1e-3f, (s.P.HalfWidth + s.P.Radius) * shrink), 1f, 1f);
                    break;
            }
        }

        // ---------------- Meshes, made once ----------------

        Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        void BuildMeshes()
        {
            FlyValues v = Values;
            _arrow = BuildArrow(Drawn(v.ArrowRim, v.ArrowRimGlow), Drawn(v.ArrowCore, v.ArrowCoreGlow), Drawn(v.ArrowRim, v.ArrowRimGlow * 0.06f),
                                Mathf.Clamp(v.ArrowTail, 0.2f, 0.9f));
            _orb = BuildDisc(Drawn(v.OrbRim, v.RimGlow), Drawn(v.OrbCore, v.CoreGlow * 0.8f));
            _ring = BuildRing(1f - Mathf.Clamp01(v.OrbRingWidth / 0.9f), Drawn(v.OrbRim, v.RingGlow));
            _wave = BuildWave(v.WaveBulge, Drawn(v.WaveRim, v.RimGlow), Drawn(v.WaveCore, v.CoreGlow));
            _puff = BuildPuff("Flying_Puff", Drawn(v.PuffRim, v.RimGlow * 0.8f), Drawn(v.PuffCore, v.CoreGlow * 0.5f));
            _flash = BuildPuff("Flying_Flash", Drawn(v.ArrowRim, v.ArrowRimGlow), Drawn(v.ArrowCore, v.FlashGlow));
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            // Both sides: what it lies on or faces is seen from above at any turn.
            int n = triangles.Count;
            for (int i = 0; i < n; i += 3)
            {
                triangles.Add(triangles[i]);
                triangles.Add(triangles[i + 2]);
                triangles.Add(triangles[i + 1]);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The arrow: level, its head at the origin, pointing along +Z, its tail to -1 (the piece is scaled to its length),
        /// half a unit wide each way (scaled to its width). A rim and a narrower core over it: a short bright head, then a
        /// long tail (<paramref name="tailShare"/> of its length, work order 039) that narrows to a point and fades, the rim
        /// to <paramref name="tail"/> at its end, the core to the rim halfway down it.
        /// </summary>
        static Mesh BuildArrow(Color rim, Color core, Color tail, float tailShare)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            float begins = -(1f - tailShare);
            for (int layer = 0; layer < 2; layer++)
            {
                float w = layer == 0 ? 0.5f : 0.22f;
                float y = layer == 0 ? 0f : 0.01f;
                float end = layer == 0 ? -1f : begins - tailShare * 0.5f;
                float mid = (begins + end) * 0.5f;
                Color head = layer == 0 ? rim : core, back = layer == 0 ? tail : rim;
                Color between = Color.Lerp(head, back, 0.5f);
                int b = v.Count;
                v.Add(new Vector3(0f, y, 0.02f));            // the point
                v.Add(new Vector3(-w, y, -0.1f));            // the shoulders
                v.Add(new Vector3(w, y, -0.1f));
                v.Add(new Vector3(-w * 0.6f, y, begins));    // where the tail begins
                v.Add(new Vector3(w * 0.6f, y, begins));
                v.Add(new Vector3(-w * 0.3f, y, mid));       // halfway down it, half faded
                v.Add(new Vector3(w * 0.3f, y, mid));
                v.Add(new Vector3(0f, y, end));              // the tail's end
                c.Add(head); c.Add(head); c.Add(head); c.Add(head); c.Add(head); c.Add(between); c.Add(between); c.Add(back);
                t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3, b + 3, b + 2, b + 4, b + 3, b + 4, b + 5, b + 5, b + 4, b + 6, b + 5, b + 6, b + 7 });
            }
            return Finish("Flying_Arrow", v, c, t);
        }

        /// <summary>The orb: a disc of radius 1 in the local XY plane, facing the camera along +Z, its brighter core in front.</summary>
        static Mesh BuildDisc(Color rim, Color core)
        {
            const int segments = 24;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                float r = layer == 0 ? 1f : 0.62f;
                float z = layer == 0 ? 0f : 0.02f;
                Color col = layer == 0 ? rim : core;
                int b = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                c.Add(col);
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
                    c.Add(col);
                    t.Add(b);
                    t.Add(b + 1 + i);
                    t.Add(b + 1 + (i + 1) % segments);
                }
            }
            return Finish("Flying_Orb", v, c, t);
        }

        /// <summary>A level ring from <paramref name="inner"/> to 1, for the orb's place on the floor.</summary>
        static Mesh BuildRing(float inner, Color col)
        {
            const int segments = 32;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * inner);
                v.Add(d);
                c.Add(col);
                c.Add(col);
                if (i == segments) break;
                int b = i * 2;
                t.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }
            return Finish("Flying_OrbRing", v, c, t);
        }

        /// <summary>
        /// The wave: a level crescent from local x -1 to 1 (scaled to its half width), its middle <paramref name="bulge"/>
        /// metres ahead of its ends along +Z, a rim band and a brighter core band at its front.
        /// </summary>
        static Mesh BuildWave(float bulge, Color rim, Color core)
        {
            const int segments = 16;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                float y = layer == 0 ? 0f : 0.01f;
                Color col = layer == 0 ? rim : core;
                int b = v.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float x = -1f + 2f * i / segments;
                    float front = bulge * (1f - x * x) + 0.3f;
                    float depth = (layer == 0 ? 0.6f : 0.22f) * (1f - 0.6f * x * x);
                    v.Add(new Vector3(x, y, front));
                    v.Add(new Vector3(x, y, front - depth));
                    c.Add(col);
                    c.Add(col);
                    if (i == segments) break;
                    int k = b + i * 2;
                    t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                }
            }
            return Finish("Flying_Wave", v, c, t);
        }

        /// <summary>The puff, and the flash at the bow (work order 039): a small level star of eight points, radius 1.</summary>
        static Mesh BuildPuff(string name, Color rim, Color core)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                float k = layer == 0 ? 1f : 0.5f;
                float y = layer == 0 ? 0f : 0.01f;
                Color col = layer == 0 ? rim : core;
                int b = v.Count;
                v.Add(new Vector3(0f, y, 0f));
                c.Add(col);
                const int points = 8;
                for (int i = 0; i < points * 2; i++)
                {
                    float a = i * Mathf.PI / points;
                    float r = (i % 2 == 0 ? 1f : 0.4f) * k;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    c.Add(col);
                    t.Add(b);
                    t.Add(b + 1 + i);
                    t.Add(b + 1 + (i + 1) % (points * 2));
                }
            }
            return Finish(name, v, c, t);
        }
    }
}
