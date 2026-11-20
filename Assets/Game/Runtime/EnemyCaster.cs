using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The caster (work order 027, Kristian's request for a fifth kind, 2026-11-20): a hooded skeleton that stands off and
    /// calls up bursts under her feet that go off after a moment. Do you keep moving to somewhere new, and can you reach it
    /// or silence it? Weak, and the first enemy whose attack does not come from where it stands. Its numbers are
    /// <see cref="CasterValues"/>, which cast comes next <see cref="CasterTactics"/>, where its circles go <see cref="CasterRules"/>.
    ///
    /// In the chase it keeps its distance as the shooter does: nearer than <see cref="CasterValues.BackOffUnder"/> it backs
    /// off to at least <see cref="CasterValues.KeepNear"/> (with no room for that it casts from where it stands), farther
    /// than <see cref="CasterValues.KeepFar"/> it comes closer, and it begins no cast while it backs off or does not see her
    /// (the shooter's sight: a wall or a tall prop between them, at <see cref="CasterValues.SightHeight"/>, or what she is
    /// in cover against), walking toward her until it does.
    ///
    /// Its casts place circles on the ground, each a thing of its own once placed: its outline (a copy of the telegraph the
    /// factory gave it, which it draws nothing round itself with) shows for the circle's whole wind-up, thickening as every
    /// telegraph does, red, or blue for the counterable Grave Rain; then it bursts, and she takes the cast's damage if she
    /// touches it (not in the dash's i-frame). A circle is placed only where the walkable ground is (the NavMesh, within
    /// <see cref="CasterValues.GroundSearch"/>); one whose place is off it is not placed. Bone Spikes: one circle where she
    /// stands when the wind-up begins, bursting as it ends. Grave Rain: the first circle so, bursting at the end of the
    /// attack's wind-up; then one every <see cref="CasterValues.RainEvery"/> where she stands then, moved
    /// <see cref="CasterValues.RainLead"/> along the way she is moving. A circle placed stays: the caster dying, dazed or
    /// countered after does not take it away (a dead caster's last spike still goes off), but nothing more is placed once
    /// it is dazed or dead. Stopping Shot from its front during Grave Rain's wind-up (before the first burst) drops it, and
    /// the circles of it on the ground fade without bursting.
    ///
    /// Each circle belongs to it for the log (<see cref="Enemy.Reported"/>): placed, off the ground, faded, and its burst
    /// as a strike that landed or missed; and the counter.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyCaster : Enemy
    {
        const float CorneredFor = 1.5f;     // s it casts from where it stands when it cannot back off, as the shooter
        // As Enemy's telegraph and strike.
        const float TelegraphHeight = 0.07f;
        const float TelegraphThin = 0.05f;
        const float TelegraphThick = 0.3f;
        const float BurstFxSeconds = 0.2f;
        static readonly Color s_CounterBlue = new Color(0.15f, 0.45f, 1f);

        /// <summary>One circle a cast placed on the ground.</summary>
        public sealed class Circle
        {
            public EnemyAttackDefinition Attack;
            public int Number;              // which circle of its cast, from 1
            public Vector3 Center;          // on the ground
            public float PlacedAt;          // game time
            public float BurstAt;
            public bool HasBurst;
            public bool Hit;                // its burst hurt her
            public bool Faded;
            public float FadedAt = -1f;
            public LineRenderer Line;       // its outline; null once it is gone
            public float Radius => Attack != null ? Attack.Radius : 0f;
            public bool IsBlue => PoolRules.IsCounterable(Attack);
            /// <summary>Waiting to burst: placed, and neither burst nor faded.</summary>
            public bool IsPending => !HasBurst && !Faded;
            /// <summary>Its outline is on the ground now.</summary>
            public bool IsShown => Line != null && Line.enabled;
            public float Width => Line != null ? Line.widthMultiplier : 0f;
        }

        public CasterValues Tuning = new CasterValues();

        readonly CasterTactics _tactics = new CasterTactics();
        readonly List<Circle> _circles = new List<Circle>();
        readonly List<Circle> _rain = new List<Circle>();   // the circles of the Grave Rain being cast
        LineRenderer _circleLook;
        MaterialPropertyBlock _block;
        EnemyAttackDefinition _cast;                         // the cast whose circles are still to be placed, else null
        EnemyAttackDefinition _lastCast;
        float _castAt;
        int _castPlaced;
        int _counteredSeen;
        bool _backingOff;
        bool _approaching;
        float _corneredUntil = -1f;

        public override EnemyKind Kind => EnemyKind.Caster;
        public override EnemyValues Values => Tuning.Enemy;
        /// <summary>True while it walks away from her to win back its distance.</summary>
        public bool IsBackingOff => _backingOff;
        /// <summary>The casts it has begun.</summary>
        public int CastsBegun => _tactics.Casts;
        /// <summary>The name of the cast it begins next.</summary>
        public string NextCast => _tactics.Next(Tuning);
        /// <summary>Every circle it has placed, in order, those burst and faded too.</summary>
        public IReadOnlyList<Circle> Circles => _circles;
        /// <summary>Circles not placed because their place was off the walkable ground.</summary>
        public int CirclesOffGround { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            // The telegraph the factory gave it is the look of its circles: it draws none round itself.
            _circleLook = Telegraph;
            Telegraph = null;
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) _cast = null; // dead, nothing more is placed
            if (_circleLook != null && _circleLook.enabled) _circleLook.enabled = false;
            TickCircles(Time.time);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (Circle c in _circles)
                if (c.Line != null) Destroy(c.Line.gameObject);
        }

        // ---------------- Its distance and its sight, as the shooter's ----------------

        protected override bool MayAttack(Vector3 toPlayer, float distance)
        {
            if (distance < Tuning.BackOffUnder && Time.time >= _corneredUntil) _backingOff = true;
            if (distance >= Tuning.KeepNear) _backingOff = false;
            return !_backingOff && SeesHer();
        }

        /// <summary>Does it see her: no wall or tall prop between at its sight height, and not covered by what she is in cover against?</summary>
        public bool SeesHer() => Player != null && Sees(Player.Position) && !Player.ShieldsFrom(transform.position);

        /// <summary>Does it see that point, with no wall or tall prop between at its sight height (as the shooter and the boss)?</summary>
        public bool Sees(Vector3 point)
        {
            Vector3 from = Flat(transform.position), dir = Flat(point) - from;
            float length = dir.magnitude;
            return length < 1e-3f || Cover.ClearDistance(from, dir / length, length, Tuning.SightHeight) >= length - 1e-3f;
        }

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            if (_backingOff)
            {
                // Straight away from her, as far as the NavMesh allows; with no room for that it stops and casts.
                Vector3 away = distance > 1e-3f ? -toPlayer / distance : -transform.forward;
                Vector3 from = transform.position;
                Vector3 to = from + away * (Tuning.KeepNear - distance + 1f);
                if (NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas)) to = hit.position;
                if (Flat(to - from).magnitude < 0.5f)
                {
                    _backingOff = false;
                    _corneredUntil = Time.time + CorneredFor;
                    Halt();
                    return;
                }
                Walk(to);
                return;
            }
            if (distance > Tuning.KeepFar) _approaching = true;
            if (distance <= (Tuning.KeepNear + Tuning.KeepFar) * 0.5f) _approaching = false;
            if (_approaching || !SeesHer()) Walk(Player.Position);
            else Halt();
        }

        // ---------------- Its casts ----------------

        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values) => _tactics.Choose(distance, Tuning);

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            // Its circles are placed after it has walked this frame (AfterMove), so the cast is reported before them.
            _cast = attack;
            _lastCast = attack;
            _castAt = Time.time;
            _castPlaced = 0;
            _rain.Clear();
        }

        /// <summary>Its strikes are its circles' bursts, each on its own time: the brain's strike hits nothing itself.</summary>
        protected override bool StrikesItself(EnemyAttackDefinition attack) => true;

        protected override void AfterMove() => PlaceDue(Time.time);

        protected override void OnDaze()
        {
            // Dazed, nothing more is placed; countered (Stopping Shot from its front in Grave Rain's wind-up), what is on the
            // ground of it fades without bursting.
            _cast = null;
            if (Countered == _counteredSeen) return;
            _counteredSeen = Countered;
            Report(EnemyReportKind.Countered, _lastCast);
            foreach (Circle c in _rain)
                if (c.IsPending) Fade(c, Time.time);
            _rain.Clear();
        }

        bool IsRain(EnemyAttackDefinition attack) => attack != null && attack.Name == CasterValues.GraveRain;

        /// <summary>Places every circle of the cast whose time has come.</summary>
        void PlaceDue(float now)
        {
            if (_cast == null || Player == null) return;
            bool rain = IsRain(_cast);
            int total = rain ? Mathf.Max(1, Tuning.RainCircles) : 1;
            while (_castPlaced < total)
            {
                int i = _castPlaced;
                float placedAt = _castAt + (rain ? CasterRules.RainPlacedAt(i, Tuning) : 0f);
                if (now < placedAt - 1e-5f) break;
                _castPlaced++;
                // The first where she stood when the wind-up began (its aim); a later one of Grave Rain where she stands now, led.
                GroundPoint her = i == 0 ? Ground(AimPoint) : Player.GroundPosition;
                Vector3 v = Player.Velocity;
                GroundPoint at = rain ? CasterRules.RainPlace(i, her, new GroundPoint(v.x, v.z), Tuning) : her;
                float windup = rain ? CasterRules.RainWindup(i, Tuning) : _cast.Windup;
                Place(_cast, i + 1, new Vector3(at.X, 0f, at.Z), placedAt, windup, rain);
            }
            if (_castPlaced >= total) _cast = null;
        }

        void Place(EnemyAttackDefinition attack, int number, Vector3 at, float placedAt, float windup, bool rain)
        {
            if (!OnWalkableGround(at))
            {
                CirclesOffGround++;
                Report(EnemyReportKind.CircleOffGround, attack, count: number);
                return;
            }
            var c = new Circle { Attack = attack, Number = number, Center = at, PlacedAt = placedAt, BurstAt = placedAt + windup };
            if (_circleLook != null)
            {
                GameObject go = Instantiate(_circleLook.gameObject, transform.parent, false);
                go.name = "CasterCircle_" + name + "_" + number;
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                c.Line = go.GetComponent<LineRenderer>();
                c.Line.loop = true;
                c.Line.useWorldSpace = true;
                // Red as every telegraph; blue for the counterable Grave Rain (work order 015), by a block over the material.
                if (_block == null) _block = new MaterialPropertyBlock();
                _block.Clear();
                if (c.IsBlue)
                {
                    _block.SetColor("_BaseColor", s_CounterBlue);
                    _block.SetColor("_Color", s_CounterBlue);
                }
                c.Line.SetPropertyBlock(_block);
                GroundOutline.Circle(c.Line, at, c.Radius, TelegraphHeight);
                c.Line.widthMultiplier = TelegraphThin;
                c.Line.enabled = true;
            }
            _circles.Add(c);
            if (rain) _rain.Add(c);
            Report(EnemyReportKind.CirclePlaced, attack, count: number);
        }

        /// <summary>Is there ground she can stand on there: the NavMesh within <see cref="CasterValues.GroundSearch"/> of it, flat?</summary>
        public bool OnWalkableGround(Vector3 at)
        {
            Vector3 flat = Flat(at);
            if (!NavMesh.SamplePosition(flat, out NavMeshHit hit, Mathf.Max(1f, Tuning.GroundSearch), NavMesh.AllAreas)) return false;
            return Flat(hit.position - flat).magnitude <= Tuning.GroundSearch + 1e-4f;
        }

        void TickCircles(float now)
        {
            foreach (Circle c in _circles)
            {
                if (c.IsPending)
                {
                    if (now >= c.BurstAt) Burst(c);
                    else if (c.Line != null)
                    {
                        float span = Mathf.Max(1e-4f, c.BurstAt - c.PlacedAt);
                        c.Line.widthMultiplier = Mathf.Lerp(TelegraphThin, TelegraphThick, Mathf.Clamp01((now - c.PlacedAt) / span));
                    }
                }
                else if (c.Faded && c.Line != null)
                {
                    float k = Tuning.FadeSeconds > 0f ? 1f - (now - c.FadedAt) / Tuning.FadeSeconds : 0f;
                    if (k <= 0f) Gone(c);
                    else c.Line.widthMultiplier = Mathf.Min(c.Line.widthMultiplier, TelegraphThick * k);
                }
            }
        }

        void Burst(Circle c)
        {
            c.HasBurst = true;
            Gone(c);
            SpawnBurstFx(c);
            if (Player == null) return;
            int landed = StrikesLanded;
            Land(c.Attack, CasterRules.Hits(Ground(c.Center), c.Radius, Player.GroundPosition, Placeholders.PlayerRadius));
            c.Hit = StrikesLanded > landed;
        }

        void Fade(Circle c, float now)
        {
            c.Faded = true;
            c.FadedAt = now;
            Report(EnemyReportKind.CircleFaded, c.Attack, count: c.Number);
            if (Tuning.FadeSeconds <= 0f) Gone(c);
        }

        static void Gone(Circle c)
        {
            if (c.Line == null) return;
            c.Line.enabled = false;
            Destroy(c.Line.gameObject);
            c.Line = null;
        }

        void SpawnBurstFx(Circle c)
        {
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fx.name = "StrikeFx";
            Destroy(fx.GetComponent<Collider>());
            fx.transform.SetParent(transform.parent, false);
            fx.transform.position = Flat(c.Center) + Vector3.up * 0.04f;
            fx.transform.localScale = new Vector3(c.Radius * 2f, 0.02f, c.Radius * 2f);
            if (StrikeMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = StrikeMaterial;
            Destroy(fx, BurstFxSeconds);
        }
    }
}
