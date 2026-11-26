using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The lancer (work order 108, 2026-11-24: he ticked "Fiender og boss" and, asked by name, said yes to the producer's
    /// "Lansereren (ny fiende)": "Etasje 4: stormer i rett linje langveisfra, varslet på kroppen, og blir ør når den treffer en
    /// vegg eller rekvisitt. Lages av skjelettmodellene som finnes."). Its numbers are <see cref="LancerValues"/>, its charge
    /// <see cref="LancerCharge"/>, its rules <see cref="LancerRules"/>.
    ///
    /// It wakes, gives up and comes at her through the brain as every kind does (its range, a hurt, its room's fight). Between
    /// charges it keeps its distance (<see cref="LancerRules.Move"/>): it backs off when she is near, walks to her when she is far
    /// or when something stands in its line to her (a wall, a shut door, a tall prop by the cover query; a low prop by the
    /// walkable ground's hole round it), and else stands facing her. In its band with a clear line it winds up its charge: on its
    /// BODY (decision 0010; <see cref="Enemy.ReadOnBody"/>: no telegraph on the floor), it lowers its lance and leans forward
    /// over the first part of the wind-up and holds it, and the glint of melee you read grows on it, blue (Stopping Shot from its
    /// front drops the wind-up and dazes it, as it does the boss's Charge, <see cref="Enemy.TryCounter"/>). Through the wind-up it
    /// keeps turning to her. As the wind-up ends the line is fixed (<see cref="LancerRules.ChargeLine"/>), what stops it on that
    /// line is looked for once (<see cref="StopDistance"/>: walls, shut doors and props, tall or low, and the end of the walkable
    /// ground), and it runs, moved by its agent so it never leaves the NavMesh, never turning.
    ///
    /// The end (<see cref="LancerEnd"/>): it meets her (her damage, none in the dash's protected part; then it stops and stands
    /// its recovery); it reaches what stops it (it stops there, its stagger bar emptied at once: dazed for
    /// <see cref="LancerValues.CrashDaze"/>, taking the stagger's extra damage; heard as a crash); or it runs its whole length
    /// (it stops and stands its recovery). Other enemies neither stop it nor are hurt by it. Dazed (her stagger skills, a counter)
    /// or killed mid-run, it stops where it is. Heard: its wind-up (<see cref="PlaceholderSound.LancerWindup"/>), its charge
    /// setting off and its crash, by the placeholder sounds; the end as a strike as every enemy's.
    ///
    /// Its body (work order 108: "Lages av skjelettmodellene som finnes"): the skeleton warrior's model, the brute's, shown
    /// <see cref="LancerValues.Shown"/> times as large and tinted <see cref="EnemyFactory.LancerTint"/>, its sword hidden and a
    /// lance made here in code (a shaft and a point, no asset) held at its right side; the lean is a turn of the whole model
    /// about its feet. Its frame allocates nothing.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyLancer : Enemy
    {
        /// <summary>The lance's shaft: dark iron and old wood.</summary>
        public static readonly Color ShaftColor = new Color(0.32f, 0.27f, 0.22f);
        /// <summary>The lance's point: pale bone.</summary>
        public static readonly Color PointColor = new Color(0.86f, 0.84f, 0.76f);
        /// <summary>The start of the name of its lance's two materials, made here (work order 119's toon look finds them by it).</summary>
        public const string MaterialPrefix = "Lancer_";
        const float PointLength = 0.35f;   // m of the lance that is its point, of LanceLength
        const float PointWidth = 2.2f;      // times the shaft's thickness the point is at its base
        const float BlockedSlack = 0.5f;    // m short of where it should be that means the walkable ground ended under it
        const float EdgeSlack = 1f;         // m past the walkable ground's end within which a thing the physics saw is what ended it
        const float SampleReach = 1f;       // m round it the walkable ground is looked for

        static readonly RaycastHit[] s_hits = new RaycastHit[32];

        public LancerValues Tuning = new LancerValues();

        readonly LancerCharge _charge = new LancerCharge();
        EnemyAttackDefinition _running;
        Transform _lean, _lance;
        Renderer _shaft, _point;
        Material _shaftMaterial, _pointMaterial;
        bool _lineClear;
        float _lineCheckedAt = float.NegativeInfinity;
        bool _backingOff;
        float _lowered;
        float _posedAt = -1f;
        int _counteredSeen;
        bool _lanceShown = true;

        public override EnemyKind Kind => EnemyKind.Lancer;
        public override EnemyValues Values => Tuning.Enemy;
        /// <summary>What it is about to do is read on its body (decision 0010): its lance, its lean and the glint; nothing on the floor.</summary>
        public override bool ReadOnBody => true;
        /// <summary>It waits for its turn with the ranged kinds (work order 023): it attacks from afar.</summary>
        public override TurnSort AttackSort => TurnSort.Ranged;

        /// <summary>True while its charge runs down its line.</summary>
        public bool IsCharging => _charge.Running;
        /// <summary>Its charge: the one running, or the last.</summary>
        public LancerCharge Charge => _charge;
        /// <summary>How its last charge ended.</summary>
        public LancerEnd LastEnd { get; private set; }
        /// <summary>What stopped its last crash.</summary>
        public LancerStopper LastStopper { get; private set; }
        /// <summary>Its charges begun, those that met her, crashed and ran out.</summary>
        public int Charges { get; private set; }
        public int MetHer { get; private set; }
        public int Crashes { get; private set; }
        public int RanOut { get; private set; }
        /// <summary>Its stagger bar.</summary>
        public StaggerBar Stagger => _self != null ? _self.Stagger : null;
        /// <summary>0 to 1 how far its lance is lowered and its body leaned now.</summary>
        public float Lowered => _lowered;
        /// <summary>Its line to her was clear at its last look.</summary>
        public bool LineClear => _lineClear;
        /// <summary>The lance (its pivot at the hands), and the turn the whole model leans by; null before it has woken.</summary>
        public Transform Lance => _lance;
        public Transform Lean => _lean;
        /// <summary>Degrees over level the lance points now, as shown.</summary>
        public float LanceAngle => _lance != null ? -NormalisedPitch(_lance.localEulerAngles.x) : 0f;
        /// <summary>The charge attack of its values.</summary>
        public EnemyAttackDefinition ChargeAttack
        {
            get
            {
                EnemyAttackDefinition[] all = Tuning?.Enemy?.Attacks;
                if (all == null) return null;
                for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].Rush > 0f) return all[i];
                return null;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _self.Stagger = new StaggerBar(Tuning.Stagger);
            // Its model is the brute's, tinted (work order 108): the tint is its body's colour, which the daze and the flashes go back to.
            if (Look != null && BodyRenderer != null)
            {
                BodyColor = EnemyFactory.LancerTint;
                EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
                foreach (Renderer r in Look.Renderers)
                    if (r != null && r != Look.Skin && r.name.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0) r.enabled = false; // its lance, not the sword
            }
            BuildLance();
        }

        /// <summary>
        /// The lean (a turn of the whole model about its feet: its pivot under a transform of its own) and the lance: a shaft and
        /// a point made of the engine's primitives with no collider, at its right side at the hands' height, its pivot at the hands.
        /// </summary>
        void BuildLance()
        {
            _lean = new GameObject("Lean").transform;
            _lean.gameObject.layer = gameObject.layer;
            _lean.SetParent(transform, false);
            if (Look != null) Look.Pivot.SetParent(_lean, false);

            _lance = new GameObject("Lance").transform;
            _lance.gameObject.layer = gameObject.layer;
            _lance.SetParent(_lean, false);
            _lance.localPosition = new Vector3(Tuning.LanceSide, Tuning.LanceHeight, 0f);
            Material source = BodyRenderer != null ? BodyRenderer.sharedMaterial : null;
            _shaftMaterial = MakeMaterial(source, ShaftColor, "Shaft");
            _pointMaterial = MakeMaterial(source, PointColor, "Point");
            float length = Mathf.Max(0.1f, Tuning.LanceLength), thick = Mathf.Max(0.01f, Tuning.LanceThickness);
            float shaft = length * (1f - PointLength / Mathf.Max(PointLength, length));
            _shaft = Part(PrimitiveType.Cylinder, "Shaft", new Vector3(0f, 0f, shaft * 0.5f - Tuning.LanceGrip),
                          new Vector3(thick, shaft * 0.5f, thick), Quaternion.Euler(90f, 0f, 0f), _shaftMaterial);
            _point = Part(PrimitiveType.Capsule, "Point", new Vector3(0f, 0f, shaft - Tuning.LanceGrip + PointLength * 0.5f),
                          new Vector3(thick * PointWidth, PointLength * 0.5f, thick * PointWidth), Quaternion.Euler(90f, 0f, 0f), _pointMaterial);
            Pose(0f);
        }

        Renderer Part(PrimitiveType type, string partName, Vector3 at, Vector3 scale, Quaternion turn, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            Collider c = go.GetComponent<Collider>();
            if (c != null) { c.enabled = false; Destroy(c); } // no obstacle and no target: the enemies carry no collider
            go.layer = gameObject.layer;
            go.transform.SetParent(_lance, false);
            go.transform.localPosition = at;
            go.transform.localRotation = turn;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return r;
        }

        static Material MakeMaterial(Material source, Color color, string partName)
        {
            Material m = source != null ? new Material(source) : null;
            if (m == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return null;
                m = new Material(lit);
            }
            m.name = MaterialPrefix + partName;
            m.mainTexture = null;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            EnemyFactory.Paint(m, color);
            return m;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_shaftMaterial != null) Destroy(_shaftMaterial);
            if (_pointMaterial != null) Destroy(_pointMaterial);
        }

        protected override void Update()
        {
            if (IsDead && _charge.Running) _charge.Stop(); // killed mid-run: it stops where it fell
            base.Update();
            TellCounter();
            Pose(Time.time);
            ShowLance(!IsDead || (BodyRenderer != null && BodyRenderer.enabled)); // gone with its body once its corpse is hidden
        }

        /// <summary>One frame of it as the engine runs it, for the test of what a frame allocates.</summary>
        public void FrameNow() => Update();

        /// <summary>A counter dropped its wind-up (work order 015's Stopping Shot): told to the log as the caster's is.</summary>
        void TellCounter()
        {
            if (Countered == _counteredSeen) return;
            _counteredSeen = Countered;
            Report(EnemyReportKind.Countered, ChargeAttack);
        }

        // ---------------- Between charges: its distance and its line ----------------

        protected override bool MayAttack(Vector3 toPlayer, float distance)
        {
            float now = Time.time;
            if (now - _lineCheckedAt >= Tuning.LineCheckEvery)
            {
                _lineCheckedAt = now;
                _lineClear = LooksClear();
            }
            return LancerRules.MayCharge(distance, _lineClear, ChargeAttack) && Opening.AllowsSeen(this, RangedOpeningRules.LancerToo); // work order 140: its charge begins only on his screen ("Ja, begge")
        }

        /// <summary>Work order 140: when it may begin its charge's wind-up in a room's fight, on his screen (<see cref="RangedOpening.AllowsSeen"/>).</summary>
        public readonly RangedOpening Opening = new RangedOpening();

        /// <summary>
        /// Is its line to her clear: no wall, shut door or tall prop on it (the cover query, at its sight height) and the walkable
        /// ground unbroken along it (a low prop is a hole in it)? Clear with nothing to ask.
        /// </summary>
        bool LooksClear()
        {
            if (Player == null) return false;
            Vector3 from = Flat(transform.position), to = Flat(Player.Position);
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 1e-3f) return true;
            if (Cover.ClearDistance(from, d / length, length, Tuning.SightHeight) < length - 1e-3f) return false;
            return !_agent.isOnNavMesh || !_agent.Raycast(Player.Position, out NavMeshHit _);
        }

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            LancerMove move = LancerRules.Move(distance, _lineClear, _backingOff, Tuning);
            _backingOff = move == LancerMove.Away;
            switch (move)
            {
                case LancerMove.Away:
                {
                    Vector3 away = distance > 1e-3f ? -toPlayer / distance : -transform.forward;
                    Vector3 spot = Player.Position + away * ((Tuning.KeepNear + Tuning.KeepFar) * 0.5f);
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, SampleReach * 3f, NavMesh.AllAreas)) Walk(hit.position);
                    else Walk(transform.position + away * Mathf.Max(1f, Tuning.KeepNear - distance));
                    break;
                }
                case LancerMove.Closer:
                    Walk(Player.Position);
                    break;
                default:
                    Halt();
                    Face(toPlayer);
                    break;
            }
        }

        // ---------------- The wind-up and the charge ----------------

        protected override void AfterMove()
        {
            if (_charge.Running)
            {
                Run();
                return;
            }
            // Through the wind-up it keeps turning to her: the line is fixed only as it ends.
            if (State == EnemyState.Windup && Player != null)
            {
                Vector3 f = transform.forward;
                GroundPoint facing = RageTurn(LancerRules.Facing(Ground(transform.position), Player.GroundPosition, new GroundPoint(f.x, f.z))); // work order 129
                Face(new Vector3(facing.X, 0f, facing.Z));
            }
        }

        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (attack == null || attack.Rush <= 0f) return false;
            Vector3 at = Flat(transform.position);
            Vector3 f = transform.forward;
            GroundPoint dir = RageTurn(LancerRules.ChargeLine(Ground(at), Player != null ? Player.GroundPosition : Ground(at + f), new GroundPoint(f.x, f.z))); // work order 129: down the line its lance showed
            var dir3 = new Vector3(dir.X, 0f, dir.Z);
            float stopAt = StopDistance(at, dir3, attack.Length, out LancerStopper stopper);
            _running = attack;
            _charge.Begin(Ground(at), dir, attack.Rush, attack.Length, stopAt, Tuning.StopShort, stopper, Time.time);
            Charges++;
            Face(dir3);
            if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(SoundMoment.LancerCharged);
            return true;
        }

        /// <summary>One frame of the run: along its line as far as it should be by now, then whether it met her or reached its end.</summary>
        void Run()
        {
            HoldRecovery(); // it stands its recovery where it stops, from then
            float now = Time.time;
            GroundPoint target = _charge.At(now);
            Vector3 before = transform.position;
            var to = new Vector3(target.X, before.y, target.Z);
            if (_agent.isOnNavMesh) _agent.Move(Flat(to - before));
            Vector3 after = transform.position;
            bool blocked = Flat(after - to).magnitude > BlockedSlack;
            bool herThere = Player != null && (PlayerHealth == null || PlayerHealth.IsAlive);
            LancerEnd end = _charge.Step(Ground(before), Ground(after), now, herThere ? Player.GroundPosition : default,
                                         Placeholders.PlayerRadius, _running.HalfWidth, herThere, blocked);
            if (end != LancerEnd.None) EndCharge(end);
        }

        void EndCharge(LancerEnd end)
        {
            LastEnd = end;
            if (_agent.isOnNavMesh) _agent.velocity = Vector3.zero;
            EnemyAttackDefinition attack = _running;
            switch (end)
            {
                case LancerEnd.MetHer:
                    MetHer++;
                    Land(attack, true);
                    break;
                case LancerEnd.RanOut:
                    RanOut++;
                    Land(attack, false);
                    break;
                case LancerEnd.Crashed:
                {
                    Crashes++;
                    LastStopper = _charge.Stopper;
                    bool cover = Player != null && LancerRules.CoverStoppedIt(_charge.From, _charge.Direction, _charge.EndedAfter, attack.HalfWidth,
                                                                             Player.GroundPosition, Placeholders.PlayerRadius);
                    // A strike stopped by a wall when what stopped it stood between them (the log's "stopped by a wall"); out of
                    // its area when she had stepped aside before it crashed. Count: what stopped it, for the log's "by".
                    Report(EnemyReportKind.Struck, attack, cover ? StrikeOutcome.StoppedByWall : StrikeOutcome.OutOfArea, count: (int)_charge.Stopper);
                    if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(SoundMoment.LancerCrashed);
                    Crash();
                    break;
                }
            }
        }

        /// <summary>
        /// The crash dazes it as the game's stagger does: its bar emptied at once (dazed for its <see cref="StaggerValues.DazeSeconds"/>,
        /// <see cref="LancerValues.CrashDaze"/>, taking its dazed damage), and the daze told as every daze is (its wind-up dropped,
        /// it stands). Without a bar, the counter's daze of that long.
        /// </summary>
        void Crash()
        {
            float now = Time.time;
            StaggerBar bar = _self.Stagger;
            if (bar != null && !bar.IsDazed(now))
            {
                bar.Take(Mathf.Max(bar.Current, 1e-3f), now, out bool dazed);
                if (dazed)
                {
                    _self.DazeFor(0f); // raises the daze: its bar holds it for its time
                    return;
                }
            }
            _self.DazeFor(Tuning.CrashDaze);
        }

        protected override void OnDaze()
        {
            // Dazed mid-run (her stagger skills): it stops where it is.
            if (_charge.Running) _charge.Stop();
        }

        protected override Vector3 LookVelocity =>
            _charge.Running ? new Vector3(_charge.Direction.X, 0f, _charge.Direction.Z) * _charge.Speed : base.LookVelocity;

        /// <summary>
        /// m along a flat direction from a point on the ground, up to <paramref name="max"/>, before the first thing that stops a
        /// charge (<paramref name="stopper"/> says what): a ball low over the ground (<see cref="LancerValues.StopProbeHeight"/>,
        /// <see cref="LancerValues.StopProbeRadius"/>) against every collider that is a wall, a shut door or a prop (what carries
        /// <see cref="Cover"/> or <see cref="Shelter"/>, and a room's door while it is shut), and the end of the walkable ground on
        /// the line; infinity when nothing stops it within <paramref name="max"/>. Asked once a charge, at the wind-up's end.
        /// </summary>
        public float StopDistance(Vector3 from, Vector3 dir, float max, out LancerStopper stopper)
        {
            stopper = LancerStopper.None;
            from = Flat(from);
            float thing = float.PositiveInfinity;
            LancerStopper what = LancerStopper.None;
            Vector3 origin = from + Vector3.up * Tuning.StopProbeHeight;
            int n = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.01f, Tuning.StopProbeRadius), dir, s_hits, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = s_hits[i];
                if (h.collider == null || h.distance <= 0f || h.distance >= thing) continue; // a cast that starts inside a thing does not see it
                LancerStopper s = StopperOf(h.collider);
                if (s == LancerStopper.None) continue;
                thing = h.distance;
                what = s;
            }
            float edge = float.PositiveInfinity;
            if (NavMesh.SamplePosition(from, out NavMeshHit start, SampleReach, NavMesh.AllAreas)
                && NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit end, NavMesh.AllAreas))
                edge = Flat(end.position - start.position).magnitude;
            if (thing <= edge + EdgeSlack && what != LancerStopper.None)
            {
                stopper = what;
                return Mathf.Min(thing, edge);
            }
            if (!float.IsPositiveInfinity(edge)) stopper = LancerStopper.Edge;
            return edge;
        }

        /// <summary>What a collider is to a charge: a shut door, a prop tall or low, a wall; nothing for anything else (the floor, her).</summary>
        static LancerStopper StopperOf(Collider c)
        {
            RoomDoor door = c.GetComponentInParent<RoomDoor>();
            if (door != null) return door.Shut ? LancerStopper.Door : LancerStopper.None;
            Shelter shelter = c.GetComponentInParent<Shelter>();
            Cover cover = c.GetComponentInParent<Cover>();
            if (shelter == null && cover == null) return LancerStopper.None;
            GameObject owner = shelter != null ? shelter.gameObject : cover.gameObject;
            bool prop = owner.name.StartsWith("Prop", StringComparison.Ordinal);
            if (!prop) return LancerStopper.Wall;
            return cover != null && cover.Stops && (shelter == null || !shelter.LowProp) ? LancerStopper.TallProp : LancerStopper.LowProp;
        }

        // ---------------- Its look ----------------

        /// <summary>
        /// Its lance and lean as it is now: lowered over the first part of the wind-up and held (<see cref="LancerRules.Lowered"/>),
        /// lowered through the charge, raised again over <see cref="LancerValues.RaiseSeconds"/> after it (and through a daze).
        /// </summary>
        void Pose(float now)
        {
            float target = 0f;
            if (!IsDead && !IsDazed)
            {
                EnemyAttackDefinition a = Attack;
                if (_charge.Running) target = 1f;
                else if (State == EnemyState.Windup && a != null && a.Rush > 0f)
                    target = LancerRules.Lowered(a.Windup > 0f ? 1f - WindupRemaining / a.Windup : 1f, Tuning);
            }
            float dt = _posedAt < 0f ? 0f : Mathf.Max(0f, now - _posedAt);
            _posedAt = now;
            _lowered = target >= _lowered ? target : Mathf.Max(target, _lowered - dt / Mathf.Max(1e-3f, Tuning.RaiseSeconds));
            if (_lean != null) _lean.localRotation = Quaternion.Euler(LancerRules.LeanDegrees(_lowered, Tuning), 0f, 0f);
            if (_lance != null) _lance.localRotation = Quaternion.Euler(-LancerRules.LanceDegrees(_lowered, Tuning), 0f, 0f);
        }

        void ShowLance(bool on)
        {
            if (on == _lanceShown) return;
            _lanceShown = on;
            if (_shaft != null) _shaft.enabled = on;
            if (_point != null) _point.enabled = on;
        }

        static float NormalisedPitch(float degrees) => degrees > 180f ? degrees - 360f : degrees;
    }
}
