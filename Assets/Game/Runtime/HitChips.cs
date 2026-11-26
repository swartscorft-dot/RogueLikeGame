using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// Chips, not flashes (work order 127, a look order; Kristian, 2026-11-25: "kan vi drope at de flasher sånn når de blir truffet,
    /// heller at litt bein eller ligende spruter av"). At every hit on an enemy (or the practice room's dummy) a small spray of chips
    /// leaves it at about chest height from the side of its body turned to the camera and to her, so it is seen from the first frame:
    /// thrown mostly along her shot (from her to it)
    /// with some spread and some upward, falling by gravity, bouncing once on its floor and lying there a moment before they shrink
    /// away. More and harder for a harder hit, and a crit's spray larger and harder still, since its white frame is gone
    /// (<see cref="HitChipsRules"/>). The hit flash is told not to flash while the switch is on (<see cref="HitFlash"/>,
    /// <see cref="TrainingDummy"/>), and they hand their hits here (<see cref="Hit"/>, and the crit from her stats, <see cref="Crit"/>);
    /// a target's hits of one frame are gathered into one spray, thrown at the end of the frame (a fan of pellets is one larger spray,
    /// not six).
    ///
    /// Cheap by choice (the order's point 4): no physics. Each chip's flight is computed here (gravity, one bounce or rest on the
    /// floor it was thrown above), so they touch nothing at all: no collider, no rigid body, no query of the game sees them, and
    /// nothing in the fight reads them. Pooled in a ring of <see cref="HitChipsValues.MostChips"/> slots, every slot made as the first
    /// enemy appears (<see cref="Prewarm"/>); each new chip takes the next slot in turn, so a full ring gives up its oldest chip. While
    /// no chip lives and no hit waits, a frame of it is one test: nothing is made or done.
    ///
    /// Their look (the order's point 3, and the producer's second round): small solid splinters made here in code (a long splinter, a
    /// short knuckle, a curved rib bit, a wedge), flat-shaded so the light breaks on their faces, in two tones (their sides and their
    /// darker broken ends), on materials of their own (one a colour: bone, robe, steel...) wearing the shader the enemy's own body wears at that moment (the
    /// toon look when it is on, Simple Lit when it is off), with the toon values; drawn with no ink line (their materials ask for the
    /// line's mark, which no chip carries: no line is better than a broken line), and casting no shadow. The dummy's chips wear the
    /// dummy's own material. With <see cref="HitChipsValues.On"/> off it does nothing at all.
    /// </summary>
    public sealed class HitChips : MonoBehaviour
    {
        /// <summary>Unity's Ignore Raycast layer, as the bone pieces' (<see cref="BonePieces.PieceLayer"/>): they have no collider, but no query could see one.</summary>
        public const int ChipLayer = 2;
        /// <summary>Targets whose hits can wait in one frame for their spray; more in one frame throw none.</summary>
        const int MostWaiting = 64;
        /// <summary>The shapes of chips made in code (a long splinter, a short knuckle, a curved rib bit, a wedge), used in turn.</summary>
        const int Shapes = 4;
        /// <summary>Share of an enemy's height a shot strikes it at (its chest, as the effects place a hit's middle: <c>CombatEffects.Middle</c>).</summary>
        const float StrikeHeight = 0.55f;
        /// <summary>
        /// Share of its body's radius out from its middle where the chips leave from: at its skin, not inside it (at 0.7 the first
        /// pictures hid a guard's chips inside its wide body).
        /// </summary>
        const float StrikeFront = 1.1f;
        /// <summary>
        /// Which side of the body they leave from: the side turned to the camera, weighted this much, and the side turned to her this
        /// much (the second round: from her side alone, much of the spray flew behind the body at the very frames that must show the
        /// hit; from the camera's side it is seen from the first frame and flies past the body, not behind it).
        /// </summary>
        const float CameraSide = 1f, HerSide = 0.6f;
        /// <summary>m round the strike point the chips of one spray start within, so the spray is a small burst from the first frame, not one dot.</summary>
        const float StartRound = 0.12f;
        /// <summary>The height of a lying chip's middle over its floor, as a share of its long side: lying on its side.</summary>
        const float LyingHeight = 0.12f;
        /// <summary>The height of a tumbling chip's middle over its floor when it meets it, as a share of its long side.</summary>
        const float MeetsTheFloor = 0.25f;
        /// <summary>Share of its tumble a chip keeps at its bounce.</summary>
        const float SpinKeptAtBounce = 0.5f;
        /// <summary>m, an enemy's height when its model gives none.</summary>
        const float HeightWithoutModel = 1.8f;
        /// <summary>The toon shader's ask for the line's mark (work order 125): set on the chips' materials always.</summary>
        static readonly int s_InkNeedsMark = Shader.PropertyToID("_InkNeedsMark");
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");

        static HitChips s_current;
        static Mesh[] s_shapes;

        /// <summary>The one in the scene now, or null.</summary>
        public static HitChips Current => s_current != null ? s_current : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            HitChipsValues.On = true; // every Play starts on
            HitChipsValues.RingWhenABodyGoes = false;
            s_current = null;
        }

        /// <summary>Made ready as each enemy appears (after the enemies' own reset of their statics, which clears that event's listeners).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            Enemy.Appeared -= OnAppeared;
            Enemy.Appeared += OnAppeared;
        }

        static void OnAppeared(Enemy e)
        {
            if (HitChipsValues.On && e != null) Ensure();
        }

        /// <summary>The one in the scene, made (and every slot with it) if there is none.</summary>
        public static HitChips Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("HitChips");
            HitChips h = go.AddComponent<HitChips>();
            h.Prewarm();
            return h;
        }

        /// <summary>A hit on <paramref name="target"/> of <paramref name="amount"/> (its health already lowered): its spray is thrown at the end of the frame. Nothing while the switch is off.</summary>
        public static void Hit(Damageable target, int amount)
        {
            if (!HitChipsValues.On || target == null) return;
            Ensure().Wait(target, amount);
        }

        /// <summary>The hit just taken by <paramref name="target"/> was a crit of hers: its spray is a crit's. Nothing while the switch is off.</summary>
        public static void Crit(Damageable target)
        {
            if (!HitChipsValues.On || target == null || Current == null) return;
            Current.MarkCrit(target);
        }

        struct Waiting
        {
            public Damageable Target;
            public int Hits, Hardest, HealthBefore;
            public bool Crit;
        }

        readonly Waiting[] _waiting = new Waiting[MostWaiting];
        int _waitingCount;

        GameObject[] _go = new GameObject[0];
        Transform[] _t = new Transform[0];
        MeshFilter[] _f = new MeshFilter[0];
        MeshRenderer[] _r = new MeshRenderer[0];
        ChipState[] _states = new ChipState[0];
        Vector3[] _pos = new Vector3[0], _vel = new Vector3[0], _axis = new Vector3[0];
        Quaternion[] _turn = new Quaternion[0];
        float[] _spin = new float[0], _size = new float[0], _age = new float[0], _lying = new float[0], _floor = new float[0];
        bool[] _bounced = new bool[0];
        Damageable[] _source = new Damageable[0];
        Material[] _main = new Material[0], _end = new Material[0]; // what each chip wears now: its sides and its broken ends
        int[] _shape = new int[0];
        readonly System.Collections.Generic.List<Material> _two = new System.Collections.Generic.List<Material>(2);
        int _made;
        long _thrown;
        readonly Material[] _materials = new Material[HitChipsRules.ColourCount];
        BoneBreakRandom _random = new BoneBreakRandom(127);
        PlayerCharacter _her;

        /// <summary>For tests and the report: chips living now, the most at once since counted, slots made, chips thrown, chips taken from the oldest, sprays thrown.</summary>
        public int Live { get; private set; }
        public int MostLive { get; private set; }
        public int Made => _made;
        public long Thrown => _thrown;
        public int TakenFromTheOldest { get; private set; }
        public int Sprays { get; private set; }
        /// <summary>The last spray thrown: how many chips, whether a crit's, its target, the way it was thrown (flat), the point it left from, its chips' speed along the shot.</summary>
        public int LastCount { get; private set; }
        public bool LastCrit { get; private set; }
        public Damageable LastTarget { get; private set; }
        public Vector3 LastAlong { get; private set; }
        public Vector3 LastFrom { get; private set; }
        public float LastSpeed { get; private set; }

        /// <summary>Starts counting the most at once again (the benchmark's measure).</summary>
        public void ResetMost() => MostLive = Live;
        /// <summary>True while a chip lives or a hit waits: the pool has a frame's work to do.</summary>
        public bool Running => Live > 0 || _waitingCount > 0;

        public ChipState StateOf(int i) => i >= 0 && i < _made ? _states[i] : ChipState.Free;
        public Vector3 PositionOf(int i) => i >= 0 && i < _made ? _pos[i] : Vector3.zero;
        public Vector3 VelocityOf(int i) => i >= 0 && i < _made ? _vel[i] : Vector3.zero;
        public float SizeOf(int i) => i >= 0 && i < _made ? _size[i] : 0f;
        public Renderer RendererOf(int i) => i >= 0 && i < _made ? _r[i] : null;
        public Damageable SourceOf(int i) => i >= 0 && i < _made ? _source[i] : null;
        /// <summary>m a chip measures along its long side as shown (the second round's measure).</summary>
        public float LongSideOf(int i) => i >= 0 && i < _made ? _size[i] * s_long[_shape[i]] : 0f;
        /// <summary>The material chips of this colour wear (made with the pool).</summary>
        public Material MaterialOf(ChipColour c) => _materials[(int)c];

        void Awake()
        {
            s_current = this;
            // The component stays ON while it has nothing to do, as the bone pieces' pool (the play start check reads a component
            // that is off at the start as a fault in its Awake). Idle, a frame of it is the one test in Frame.
        }

        void OnDestroy()
        {
            if (s_current == this) s_current = null;
            foreach (Material m in _materials)
                if (m != null) Destroy(m);
        }

        /// <summary>Every slot made now, up to the cap, and the chips' shapes and materials (as an enemy appears, while a floor is put together): nothing is made in a fight.</summary>
        public void Prewarm()
        {
            if (s_shapes == null || s_shapes[0] == null) s_shapes = MakeShapes();
            for (int c = 0; c < _materials.Length; c++)
                if (_materials[c] == null) _materials[c] = MakeMaterial((ChipColour)c);
            int most = Mathf.Max(1, HitChipsValues.Live.MostChips);
            Grow(most);
        }

        // ---------------- A hit ----------------

        void Wait(Damageable target, int amount)
        {
            for (int k = 0; k < _waitingCount; k++)
            {
                if (_waiting[k].Target != target) continue;
                _waiting[k].Hits++;
                if (amount > _waiting[k].Hardest) _waiting[k].Hardest = amount;
                return;
            }
            if (_waitingCount >= MostWaiting) return;
            _waiting[_waitingCount++] = new Waiting { Target = target, Hits = 1, Hardest = amount, HealthBefore = target.CurrentHealth + amount };
        }

        void MarkCrit(Damageable target)
        {
            for (int k = 0; k < _waitingCount; k++)
                if (_waiting[k].Target == target) _waiting[k].Crit = true;
        }

        void LateUpdate() => Frame();

        /// <summary>Its frame (its LateUpdate; public for the test of what a frame allocates): each chip's flight moved on, then this frame's sprays thrown.</summary>
        public void Frame()
        {
            if (!Running) return;
            if (Live > 0) Fly(Time.deltaTime);
            if (_waitingCount > 0)
            {
                int n = _waitingCount;
                _waitingCount = 0;
                for (int k = 0; k < n; k++)
                {
                    Waiting w = _waiting[k];
                    _waiting[k].Target = null;
                    if (w.Target != null && HitChipsValues.On) Spray(w);
                }
            }
        }

        void Spray(Waiting w)
        {
            HitChipsValues v = HitChipsValues.Live;
            Damageable target = w.Target;
            bool isEnemy = target.TryGetComponent(out Enemy e);
            bool partOff = false, bodyBreaks = false;
            if (isEnemy && BoneBreakValues.On && target.TryGetComponent(out BoneBreak b) && b.Model != null)
            {
                // Together with work order 122: its killing hit breaks its body into its pieces (no chips over them); a hit that
                // took a part off throws a share of its chips under the flying part.
                if (!target.IsAlive) bodyBreaks = true;
                else partOff = BoneBreakRules.PartsDue(target.CurrentHealth, target.MaxHealth, BoneBreakValues.Live)
                               > BoneBreakRules.PartsDue(w.HealthBefore, target.MaxHealth, BoneBreakValues.Live);
            }
            int count = HitChipsRules.ForFrame(w.Hits, w.Hardest, w.Crit, partOff, bodyBreaks, v);
            if (count <= 0) return;

            // Where her shot came from, and where it struck.
            Transform at = target.transform;
            if (_her == null) _her = isEnemy && e.Player != null ? e.Player : FindAnyObjectByType<PlayerCharacter>();
            Vector3 from = _her != null ? _her.Position : at.position - at.forward;
            Vector3 along = at.position - from;
            along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : -Flat(at.forward);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.forward;

            // The side they leave from: turned to the camera and to her, so the camera sees them leave.
            Camera cam = Camera.main;
            Vector3 toCamera = cam != null ? Flat(cam.transform.position - at.position) : Vector3.zero;
            toCamera = toCamera.sqrMagnitude > 1e-6f ? toCamera.normalized : Vector3.zero;
            Vector3 face = toCamera * CameraSide - along * HerSide;
            face = face.sqrMagnitude > 1e-6f ? face.normalized : -along;

            Vector3 strike;
            float floor, shown;
            Material own = null;
            Shader shader = null;
            ChipStuff stuff = ChipStuff.Dummy;
            if (isEnemy)
            {
                float h = e.ModelHeight > 0f ? e.ModelHeight : HeightWithoutModel * Mathf.Abs(at.lossyScale.y);
                floor = at.position.y;
                strike = at.position + Vector3.up * (h * StrikeHeight) + face * (target.Radius * StrikeFront);
                shown = e.ModelHeight > 0f ? e.ModelHeight / (float)v.HeightForSize : Mathf.Abs(at.lossyScale.y);
                stuff = HitChipsRules.StuffOf(e.Kind);
                Renderer body = e.Look != null && e.Look.Skin != null ? e.Look.Skin : e.BodyRenderer;
                if (body != null && body.sharedMaterial != null) shader = body.sharedMaterial.shader;
            }
            else
            {
                // The practice room's dummy (or any other target): from its drawn body, in its own material.
                Renderer r = target.GetComponent<Renderer>();
                if (r == null) r = target.GetComponentInChildren<Renderer>();
                Bounds box = r != null ? r.bounds : new Bounds(at.position + Vector3.up, Vector3.one);
                floor = box.min.y;
                strike = box.center + face * (Mathf.Min(box.extents.x, box.extents.z) * StrikeFront);
                shown = 1f;
                own = r != null ? r.sharedMaterial : null;
            }

            float speed = (float)HitChipsRules.Speed(w.Hardest, w.Crit, v);
            float size = (float)HitChipsRules.SizeOf(w.Crit, shown, v);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 carried = isEnemy && target.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled
                ? Flat(agent.velocity) * (float)v.BodySpeedKept : Vector3.zero; // a moving body's chips move with it
            ChipColour first = HitChipsRules.First(stuff);
            if (own == null)
            {
                Look(first, shader);
                Look(HitChipsRules.EndOf(first), shader);
                if (stuff == ChipStuff.Robe)
                {
                    Look(ChipColour.Bone, shader);
                    Look(HitChipsRules.EndOf(ChipColour.Bone), shader);
                }
            }
            for (int k = 0; k < count; k++)
            {
                float s = speed * (float)(1.0 + v.SpeedJitter * _random.Signed());
                Vector3 vel = carried + along * s + side * (float)(speed * v.Spread * _random.Signed())
                              + Vector3.up * (float)(v.UpBase + v.UpShare * speed * (0.5 + 0.5 * _random.Next()));
                Vector3 start = strike + new Vector3((float)_random.Signed(), (float)_random.Signed(), (float)_random.Signed()) * StartRound;
                if (start.y < floor + size) start.y = floor + size;
                Vector3 axis = new Vector3((float)_random.Signed(), (float)_random.Signed(), (float)_random.Signed());
                if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
                float spin = (float)(v.MostSpin * (0.3 + 0.7 * _random.Next()));
                float chip = size * (float)(1.0 + v.SizeJitter * _random.Signed());
                ChipColour main = HitChipsRules.MainOf(stuff, _random.Next(), v);
                Material sides = own != null ? own : _materials[(int)main];
                Material ends = own != null ? own : _materials[(int)HitChipsRules.EndOf(main)];
                Throw(target, sides, ends, start, vel, axis.normalized, spin, chip, floor);
            }
            Sprays++;
            LastCount = count;
            LastCrit = w.Crit;
            LastTarget = target;
            LastAlong = along;
            LastFrom = strike;
            LastSpeed = speed;
        }

        void Throw(Damageable source, Material sides, Material ends, Vector3 at, Vector3 velocity, Vector3 axis, float spin, float size, float floor)
        {
            HitChipsValues v = HitChipsValues.Live;
            int cap = Mathf.Max(1, v.MostChips);
            if (cap > _made) Grow(cap);
            int i = HitChipsRules.Slot(_thrown, cap);
            _thrown++;
            if (_states[i] != ChipState.Free)
            {
                TakenFromTheOldest++;
                Live--;
            }
            _states[i] = ChipState.Flying;
            _pos[i] = at;
            _vel[i] = velocity;
            _axis[i] = axis;
            _spin[i] = spin;
            _size[i] = size;
            _age[i] = 0f;
            _lying[i] = 0f;
            _floor[i] = floor;
            _bounced[i] = false;
            _source[i] = source;
            _turn[i] = Quaternion.AngleAxis((float)(_random.Next() * 360.0), axis);
            int shape = (int)(_thrown % Shapes);
            _shape[i] = shape;
            _f[i].sharedMesh = s_shapes[shape];
            if (_main[i] != sides || _end[i] != ends)
            {
                _main[i] = sides;
                _end[i] = ends;
                _two.Clear();
                _two.Add(sides);
                _two.Add(ends);
                _r[i].SetSharedMaterials(_two);
            }
            _t[i].SetPositionAndRotation(at, _turn[i]);
            _t[i].localScale = Vector3.one * size;
            if (!_go[i].activeSelf) _go[i].SetActive(true);
            Live++;
            if (Live > MostLive) MostLive = Live;
        }

        // ---------------- Every frame, only while a chip lives ----------------

        void Fly(float dt)
        {
            HitChipsValues v = HitChipsValues.Live;
            float g = (float)v.Gravity;
            for (int i = 0; i < _made; i++)
            {
                ChipState state = _states[i];
                if (state == ChipState.Free) continue;
                _age[i] += dt;
                bool landed = false;
                if (state == ChipState.Flying)
                {
                    Vector3 vel = _vel[i];
                    vel.y -= g * dt;
                    Vector3 p = _pos[i] + vel * dt;
                    float rest = _floor[i] + _size[i] * MeetsTheFloor;
                    if (p.y <= rest && vel.y < 0f)
                    {
                        float up = (float)HitChipsRules.BounceUp(-vel.y, _bounced[i], v);
                        if (up > 0f)
                        {
                            _bounced[i] = true;
                            vel = new Vector3(vel.x * (float)v.SlideKeep, up, vel.z * (float)v.SlideKeep);
                            _spin[i] *= SpinKeptAtBounce;
                            p.y = rest;
                        }
                        else
                        {
                            landed = true;
                            vel = Vector3.zero;
                            p.y = _floor[i] + _size[i] * LyingHeight;
                        }
                    }
                    _vel[i] = vel;
                    _pos[i] = p;
                    if (!landed) _turn[i] = Quaternion.AngleAxis(_spin[i] * dt * Mathf.Rad2Deg, _axis[i]) * _turn[i];
                    _t[i].SetPositionAndRotation(p, _turn[i]);
                }
                else _lying[i] += dt;

                ChipState next = HitChipsRules.Next(state, _age[i], _lying[i], landed, v);
                if (next == ChipState.Free)
                {
                    Free(i);
                    continue;
                }
                if (next != state)
                {
                    _states[i] = next;
                    // One that never found its floor goes at once into its shrink.
                    _lying[i] = landed ? 0f : (float)v.LieSeconds;
                }
                if (next == ChipState.Lying && _lying[i] > v.LieSeconds)
                    _t[i].localScale = Vector3.one * (_size[i] * (float)HitChipsRules.Shown(_lying[i], v));
            }
        }

        void Free(int i)
        {
            if (_states[i] == ChipState.Free) return;
            _states[i] = ChipState.Free;
            _source[i] = null;
            if (_go[i] != null) _go[i].SetActive(false);
            Live--;
        }

        // ---------------- Made once ----------------

        void Grow(int most)
        {
            if (_made >= most) return;
            System.Array.Resize(ref _go, most);
            System.Array.Resize(ref _t, most);
            System.Array.Resize(ref _f, most);
            System.Array.Resize(ref _r, most);
            System.Array.Resize(ref _states, most);
            System.Array.Resize(ref _pos, most);
            System.Array.Resize(ref _vel, most);
            System.Array.Resize(ref _axis, most);
            System.Array.Resize(ref _turn, most);
            System.Array.Resize(ref _spin, most);
            System.Array.Resize(ref _size, most);
            System.Array.Resize(ref _age, most);
            System.Array.Resize(ref _lying, most);
            System.Array.Resize(ref _floor, most);
            System.Array.Resize(ref _bounced, most);
            System.Array.Resize(ref _source, most);
            System.Array.Resize(ref _main, most);
            System.Array.Resize(ref _end, most);
            System.Array.Resize(ref _shape, most);
            if (s_shapes == null || s_shapes[0] == null) s_shapes = MakeShapes();
            while (_made < most)
            {
                int i = _made++;
                var go = new GameObject("Chip") { layer = ChipLayer };
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                _go[i] = go;
                _t[i] = go.transform;
                _f[i] = go.AddComponent<MeshFilter>();
                _f[i].sharedMesh = s_shapes[i % Shapes];
                _r[i] = go.AddComponent<MeshRenderer>();
                _r[i].shadowCastingMode = ShadowCastingMode.Off; // a few pixels' shadow under each chip would be noise
                _r[i].receiveShadows = true;
                _r[i].lightProbeUsage = LightProbeUsage.Off;
                _r[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
                _main[i] = _materials[0];
                _end[i] = _materials[(int)HitChipsRules.EndOf(ChipColour.Bone)];
                _two.Clear();
                _two.Add(_main[i]);
                _two.Add(_end[i]);
                _r[i].SetSharedMaterials(_two);
                _states[i] = ChipState.Free;
            }
        }

        /// <summary>A colour's material, on Simple Lit until a chip of it is thrown from a body wearing another shader.</summary>
        Material MakeMaterial(ChipColour c)
        {
            Shader s = Shader.Find(PropFactory.ShaderName);
            var m = new Material(s) { name = "HitChip_" + c };
            Paint(m, c);
            return m;
        }

        static void Paint(Material m, ChipColour c)
        {
            Rgb rgb = HitChipsRules.ColourOf(c, HitChipsValues.Live);
            if (m.HasProperty(s_BaseColor)) m.SetColor(s_BaseColor, new Color(rgb.R, rgb.G, rgb.B, 1f));
            if (m.HasProperty(s_BaseMap)) m.SetTexture(s_BaseMap, Texture2D.whiteTexture);
        }

        /// <summary>A colour's material put on the shader the enemy's own body wears now (the toon look on or off), with the look's values and no ink line.</summary>
        void Look(ChipColour c, Shader shader)
        {
            Material m = _materials[(int)c];
            if (m == null || shader == null || m.shader == shader) return;
            m.shader = shader;
            Paint(m, c);
            if (shader.name == ToonLook.ShaderName) ToonLook.Push(m, false, ToonLookValues.Live);
            if (m.HasProperty(s_InkNeedsMark)) m.SetFloat(s_InkNeedsMark, 1f); // no chip is marked: no line round a chip
        }

        /// <summary>The long side of each shape as a share of its size (its scale): the splinter, the knuckle, the rib bit, the wedge.</summary>
        static readonly float[] s_long = { 1f, 0.6f, 1f, 0.75f };

        /// <summary>
        /// The chips' shapes (the second round: "no quads and no squares ... small solid splinters"), made once in code, each with its
        /// long side along x and real thickness, flat-shaded so the light breaks on its faces as it tumbles, and in two tones: its
        /// sides (sub-mesh 0, its colour) and its broken ends (sub-mesh 1, the darker tone, <see cref="HitChipsRules.EndOf"/>).
        /// Before it is scaled: a long splinter 1 long and about 0.3 thick (three times as long as wide), a short knuckle 0.6 long
        /// and about 0.45 thick, a curved rib bit 1 long and 0.15 by 0.1 in section, bowed 0.2, and a tapering wedge 0.75 long.
        /// </summary>
        static Mesh[] MakeShapes()
        {
            var rand = new BoneBreakRandom(1270);
            var meshes = new Mesh[Shapes];
            meshes[0] = Splinter(ref rand);
            meshes[1] = Knuckle(ref rand);
            meshes[2] = Rib(ref rand);
            meshes[3] = Wedge(ref rand);
            for (int k = 0; k < Shapes; k++) meshes[k].name = "HitChip" + k;
            return meshes;
        }

        /// <summary>Faces gathered into a mesh of two sub-meshes, each face with vertices of its own and wound to face away from a point inside it.</summary>
        sealed class Faces
        {
            readonly System.Collections.Generic.List<Vector3> _v = new System.Collections.Generic.List<Vector3>();
            readonly System.Collections.Generic.List<int>[] _t = { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };

            public void Add(int sub, Vector3 inside, params Vector3[] poly)
            {
                Vector3 mid = Vector3.zero;
                foreach (Vector3 p in poly) mid += p;
                mid /= poly.Length;
                Vector3 n = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]);
                bool turn = Vector3.Dot(n, mid - inside) < 0f;
                int first = _v.Count;
                for (int k = 0; k < poly.Length; k++) _v.Add(poly[turn ? poly.Length - 1 - k : k]);
                for (int k = 1; k + 1 < poly.Length; k++)
                {
                    _t[sub].Add(first);
                    _t[sub].Add(first + k);
                    _t[sub].Add(first + k + 1);
                }
            }

            public Mesh Mesh()
            {
                var m = new Mesh { hideFlags = HideFlags.DontSave, subMeshCount = 2 };
                m.SetVertices(_v);
                m.SetTriangles(_t[0], 0);
                m.SetTriangles(_t[1], 1);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static Vector3 Jitter(ref BoneBreakRandom r, float by) => new Vector3((float)r.Signed(), (float)r.Signed(), (float)r.Signed()) * by;

        /// <summary>A ring of <paramref name="n"/> corners round the x axis at <paramref name="x"/>, of these half-widths in y and z, a little uneven.</summary>
        static Vector3[] Ring(ref BoneBreakRandom r, float x, int n, float ry, float rz, float uneven)
        {
            var ring = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float a = (k + 0.5f) / n * Mathf.PI * 2f;
                float s = 1f + uneven * (float)r.Signed();
                ring[k] = new Vector3(x, Mathf.Cos(a) * ry * s, Mathf.Sin(a) * rz * s);
            }
            return ring;
        }

        static void Sides(Faces f, Vector3[] a, Vector3[] b)
        {
            Vector3 inside = new Vector3((a[0].x + b[0].x) * 0.5f, 0f, 0f);
            for (int k = 0; k < a.Length; k++)
            {
                int j = (k + 1) % a.Length;
                f.Add(0, inside, a[k], a[j], b[j], b[k]);
            }
        }

        static Mesh Splinter(ref BoneBreakRandom r)
        {
            var f = new Faces();
            Vector3[] a = Ring(ref r, -0.28f, 5, 0.13f, 0.16f, 0.2f), b = Ring(ref r, 0.24f, 5, 0.11f, 0.14f, 0.2f);
            Sides(f, a, b);
            Vector3 tipA = new Vector3(-0.5f, 0f, 0f) + Jitter(ref r, 0.05f), tipB = new Vector3(0.5f, 0f, 0f) + Jitter(ref r, 0.05f);
            for (int k = 0; k < a.Length; k++)
            {
                int j = (k + 1) % a.Length;
                f.Add(1, new Vector3(-0.28f, 0f, 0f), a[k], a[j], tipA); // its broken ends, the darker tone
                f.Add(1, new Vector3(0.24f, 0f, 0f), b[k], b[j], tipB);
            }
            return f.Mesh();
        }

        static Mesh Knuckle(ref BoneBreakRandom r)
        {
            var f = new Faces();
            Vector3[][] rings =
            {
                Ring(ref r, -0.3f, 6, 0.16f, 0.18f, 0.12f), Ring(ref r, -0.1f, 6, 0.21f, 0.23f, 0.12f),
                Ring(ref r, 0.1f, 6, 0.22f, 0.22f, 0.12f), Ring(ref r, 0.3f, 6, 0.15f, 0.17f, 0.12f),
            };
            for (int k = 0; k + 1 < rings.Length; k++) Sides(f, rings[k], rings[k + 1]);
            f.Add(1, new Vector3(0f, 0f, 0f), rings[0]);
            f.Add(1, new Vector3(0f, 0f, 0f), rings[rings.Length - 1]);
            return f.Mesh();
        }

        static Mesh Rib(ref BoneBreakRandom r)
        {
            var f = new Faces();
            const int n = 5;
            const float bow = 0.2f, w = 0.075f, h = 0.05f;
            var centre = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float x = -0.5f + k / (float)(n - 1);
                centre[k] = new Vector3(x, 0f, bow * (1f - 4f * x * x));
            }
            var sections = new Vector3[n][];
            for (int k = 0; k < n; k++)
            {
                Vector3 tangent = (centre[Mathf.Min(n - 1, k + 1)] - centre[Mathf.Max(0, k - 1)]).normalized;
                Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
                float s = 1f + 0.15f * (float)r.Signed();
                sections[k] = new[]
                {
                    centre[k] + (side * w + Vector3.up * h) * s, centre[k] + (-side * w + Vector3.up * h) * s,
                    centre[k] + (-side * w - Vector3.up * h) * s, centre[k] + (side * w - Vector3.up * h) * s,
                };
            }
            for (int k = 0; k + 1 < n; k++)
            {
                Vector3 inside = (centre[k] + centre[k + 1]) * 0.5f;
                for (int c = 0; c < 4; c++)
                {
                    int d = (c + 1) % 4;
                    f.Add(0, inside, sections[k][c], sections[k][d], sections[k + 1][d], sections[k + 1][c]);
                }
            }
            f.Add(1, centre[1], sections[0]);
            f.Add(1, centre[n - 2], sections[n - 1]);
            return f.Mesh();
        }

        static Mesh Wedge(ref BoneBreakRandom r)
        {
            var f = new Faces();
            Vector3[] a = Ring(ref r, -0.375f, 3, 0.17f, 0.19f, 0.15f), b = Ring(ref r, 0.375f, 3, 0.06f, 0.07f, 0.15f);
            Sides(f, a, b);
            f.Add(1, Vector3.zero, a);
            f.Add(1, Vector3.zero, b);
            return f.Mesh();
        }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
