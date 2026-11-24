using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The shooter (work order 008): it stands off and shoots, a thin tall yellow capsule. Do you keep moving?
    ///
    /// In the chase it keeps <see cref="ShooterValues.KeepNear"/> to <see cref="ShooterValues.KeepFar"/> from her: it
    /// walks closer when she is farther, backs off when she comes nearer than <see cref="ShooterValues.BackOffUnder"/>
    /// (until she is KeepNear away again, or it has its back to a wall), and otherwise stands. It does not shoot while
    /// it backs off.
    ///
    /// Its one attack is a line aimed at where she stands when the windup begins, shown for the windup and then landing
    /// along its whole length at once; it does not follow her. It needs to see her: it starts no windup while a wall
    /// stands between them (a ray at <see cref="ShooterValues.SightHeight"/> over the ground against cover, the same
    /// cover that stops her shots: the level's walls, the low ones too, and its tall props, so it sees and shoots over
    /// a low prop as she does; Kristian, 2026-11-19), and walks toward her until it does. The line stops at the first
    /// wall on its way, so stepping behind one avoids it. With <see cref="FeelValues.CoverStopsShots"/> off, a wall is
    /// every collider that is not her, an enemy or a target, as before.
    ///
    /// Taking cover (Kristian, 2026-11-19, an experiment): what she is in cover against stops its sight of her and its
    /// shot when it lies between them, a low prop too while she is ducked (<see cref="PlayerCharacter.ShieldsFrom"/>).
    /// It then starts no windup and walks toward her, which may take it round to her side. A windup already going when
    /// she ducks is stopped at the strike, its line and telegraph ending at the thing she is behind. Asked live, so a
    /// shot that winds up while she peeks and strikes after she ducks again is stopped, and one that strikes while she
    /// is up is not.
    ///
    /// Things that fly (work order 036, 2026-11-20; decision 0010; <see cref="ThingsThatFly"/>, on by default): no line is
    /// drawn on the floor; what it is about to do is read on its body, the draw of its bow, the wind-up effect and sound.
    /// At the release an arrow leaves the bow and flies (<see cref="ShooterValues.Arrows"/>), aimed where she will be if she
    /// keeps her velocity for its flight, times <see cref="ShooterValues.LeadShare"/>, the lead at most
    /// <see cref="ShooterValues.LeadMost"/> (<see cref="ProjectileRules.LeadAim"/>). Since work order 039 the arrow is slow
    /// enough to see and its lead only partial, with a flash at the bow as it leaves: standing still, drifting, or running
    /// at it or away from it she is hit; running across she is missed behind; a sidestep after the release, a wall or a
    /// tall prop, what she ducked behind, or the dash avoids it. On
    /// floor <see cref="ShooterValues.FanFromFloor"/> and later, and for an elite on any floor, every third shot is a fan
    /// of three, drawn a little longer with a larger wind-up effect; an elite's other shots are volleys of two, each aimed
    /// as it leaves (<see cref="ShooterTactics"/>). Its distance, its sight and its turns are as before.
    ///
    /// Shooters that hold their place (work order 098, Kristian's choice of 2026-11-24, <see cref="ShooterValues.HoldsPlace"/>, on by
    /// default; the rule is <see cref="ShooterPostRules"/>): a shooter of a room's fight has a post, the place the generator stood it
    /// (its home, found on the NavMesh). Awake, it does not walk toward her because she is far: out of its reach it stands at its
    /// post facing her and waits; in its reach it shoots as above. It steps only inside <see cref="ShooterValues.HoldRadius"/> of its
    /// post: to back off, cut short at the circle's edge (with its back to the edge it stands and shoots, as with its back to a wall),
    /// and, when it does not see her, to the nearest spot of its circle that does and that it can walk to without leaving the circle;
    /// with none, it stands and waits. Put out of its circle (pushed, pulled in by the doors) it walks back to its post. One of no
    /// room's fight (the practice room), one whose home is not on the NavMesh, and every one with the switch off move as before. As
    /// each one's part in a fight ends it tells the recorder once (<see cref="ShooterPostEvents"/>).
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyShooter : Enemy
    {
        const float CorneredFor = 1.5f; // s it shoots from where it stands when it cannot back off
        static readonly RaycastHit[] s_Hits = new RaycastHit[32];

        public ShooterValues Tuning = new ShooterValues();

        bool _backingOff;
        bool _approaching;
        float _corneredUntil = -1f;
        float _reach;

        // Things that fly (work order 036).
        readonly ShooterTactics _arrows = new ShooterTactics();
        LineRenderer _line;             // its aim line, put away while things fly
        EnemyAttackDefinition _volley;  // a volley's arrows still to leave, and when the next does
        int _volleyLeft;
        float _volleyAt;
        int _levelFloor = -1;

        // Shooters that hold their place (work order 098).
        bool _postAsked, _postKnown;
        Vector3 _post;
        NavMeshPath _path;                                  // made at the first look for a spot, kept
        static readonly Vector3[] s_Corners = new Vector3[16];
        static readonly float[] s_SpotDistances = new float[ShooterPostRules.SpotCount];
        bool _seeking;
        Vector3 _spot;
        float _seekUntil, _nextSeek;
        RoomFight _postFight;                               // the fight its part in is followed for the recorder, or null
        bool _postHeld;
        float _postWoke, _postReach;

        /// <summary>
        /// The floor it fights on, for its fans (work order 036): set it, or under 0 it is read from the floor it stands
        /// in, and 0 in the practice room.
        /// </summary>
        [NonSerialized] public int Floor = -1;

        public override EnemyKind Kind => EnemyKind.Shooter;
        public override EnemyValues Values => Tuning.Enemy;
        /// <summary>True while it walks away from her to win back its distance.</summary>
        public bool IsBackingOff => _backingOff;
        /// <summary>
        /// How far the line being wound up reaches: its length, or less when a wall cuts it, or what she is in cover
        /// against (an experiment, 2026-11-19), asked now.
        /// </summary>
        public float LineReach => Attack != null ? Reach(Attack) : _reach;

        // ---------------- Shooters that hold their place (work order 098) ----------------

        /// <summary>
        /// Does it hold its place now: the switch on, it belongs to a room's fight, and its post is known (its home on the NavMesh,
        /// found the first time it is asked in a fight). False in the practice room and with the switch off: it moves as before.
        /// </summary>
        public bool HoldsItsPlace => Fight != null && ShooterPostRules.Holds(Tuning, KnowsPost());
        /// <summary>Its post: its home on the NavMesh (the place the generator stood it); its home while none is known.</summary>
        public Vector3 Post => KnowsPost() ? _post : Home;
        /// <summary>True while it walks to a spot of its circle it chose to see her from.</summary>
        public bool IsSeekingLine => _seeking;

        /// <summary>For tests: its choice of where to stand and its part for the recorder, as a frame of its chase runs them (work order 098).</summary>
        public void ChaseForTests()
        {
            if (Player == null) return;
            Vector3 toPlayer = Flat(Player.Position - transform.position);
            Chase(toPlayer, toPlayer.magnitude);
            FollowPost();
        }

        bool KnowsPost()
        {
            if (_postAsked) return _postKnown;
            if (_agent == null || !_agent.isOnNavMesh) return false; // asked again later: the floor's NavMesh may not be built yet
            _postAsked = true;
            _postKnown = NavMesh.SamplePosition(Home, out NavMeshHit hit, 1f, NavMesh.AllAreas);
            if (_postKnown) _post = hit.position;
            return _postKnown;
        }

        // ---------------- Things that fly (work order 036) ----------------

        /// <summary>The shots it has begun with things that fly on.</summary>
        public int ArrowShots => _arrows.Shots;
        /// <summary>The name of its next shot with things that fly on.</summary>
        public string NextArrowShot => _arrows.Next(FloorNow, IsElite, Tuning);
        /// <summary>The floor its fans go by: <see cref="Floor"/>, or the floor it stands in, or 0.</summary>
        public int FloorNow
        {
            get
            {
                if (Floor >= 0) return Floor;
                if (_levelFloor < 0)
                {
                    GeneratedLevel level = GetComponentInParent<GeneratedLevel>();
                    _levelFloor = level != null && level.Layout != null ? level.Layout.Floor : 0;
                }
                return _levelFloor;
            }
        }
        /// <summary>The size of its wind-up effect on the body, times the others': a fan's draw shows larger, so it can be told apart.</summary>
        public float WindupLook => State == EnemyState.Windup && Attack != null && Attack.Name == ShooterValues.ArrowFan ? Tuning.FanWindupLook : 1f;

        static bool IsArrow(EnemyAttackDefinition a) =>
            a != null && (a.Name == ShooterValues.Arrow || a.Name == ShooterValues.ArrowFan || a.Name == ShooterValues.Volley);

        protected override void Update()
        {
            PutAwayTheLine();
            if (IsDead) _volleyLeft = 0;
            base.Update();
            FollowPost();
        }

        protected override void OnDestroy()
        {
            if (_postFight != null) TellPost(-1f); // taken out of the game in its fight (the floor gone)
            base.OnDestroy();
        }

        /// <summary>
        /// Its part in a room's fight, for the recorder (work order 098): from the frame it is seen awake in the fight on, when she
        /// first comes within its reach; told once, as it dies or the fight ends. A frame that tells nothing allocates nothing.
        /// </summary>
        void FollowPost()
        {
            RoomFight f = Fight;
            if (_postFight == null)
            {
                if (f == null || !f.On || IsDead || !IsAwake) return;
                _postFight = f;
                _postWoke = f.StartedAt;
                _postReach = -1f;
                _postHeld = HoldsItsPlace;
            }
            if (IsDead)
            {
                TellPost(Time.time);
                return;
            }
            if (_postReach < 0f && Player != null && Flat(Player.Position - transform.position).sqrMagnitude <= ShooterValues.ShootsFrom * ShooterValues.ShootsFrom)
                _postReach = Time.time;
            if (f != _postFight || !f.On) TellPost(-1f);
        }

        void TellPost(float died)
        {
            RoomFight f = _postFight;
            _postFight = null;
            PlaySessionRecorder.Current?.NoteShooterPost(FloorNow, f.Room, _postWoke, _postHeld,
                                                         _postReach >= 0f ? _postReach - _postWoke : -1f, died >= 0f ? died - _postWoke : -1f);
        }

        /// <summary>With things that fly on (or an arrow being drawn), no line on the floor: its telegraph is put away.</summary>
        void PutAwayTheLine()
        {
            bool hide = ThingsThatFly.On || IsArrow(Attack);
            if (hide && Telegraph != null)
            {
                _line = Telegraph;
                Telegraph = null;
            }
            else if (!hide && Telegraph == null && _line != null)
            {
                Telegraph = _line;
                _line = null;
            }
            if (_line != null && _line.enabled) _line.enabled = false;
        }

        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values) =>
            ThingsThatFly.On ? _arrows.Choose(distance, FloorNow, IsElite, Tuning) : base.ChooseAttack(distance, values);

        protected override EnemyAttackDefinition WouldChoose(float distance, EnemyValues values) =>
            ThingsThatFly.On ? _arrows.Peek(distance, FloorNow, IsElite, Tuning) : base.ChooseAttack(distance, values);

        /// <summary>An arrow is loosed by the shooter itself at the release: the strike hits nothing of its own.</summary>
        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (!IsArrow(attack)) return false;
            Loose(attack, ShooterTactics.AtOnce(attack.Name, Tuning));
            if (attack.Name == ShooterValues.Volley && Tuning.VolleyArrows > 1)
            {
                _volley = attack;
                _volleyLeft = Tuning.VolleyArrows - 1;
                _volleyAt = Time.time + Tuning.VolleyGap;
            }
            return true;
        }

        protected override void AfterMove()
        {
            // A volley's next arrow, aimed as it leaves; a daze or a death drops what is left of it.
            if (_volleyLeft <= 0 || Time.time < _volleyAt) return;
            _volleyLeft--;
            _volleyAt = Time.time + Tuning.VolleyGap;
            Loose(_volley, 1);
        }

        protected override void OnDaze() => _volleyLeft = 0;

        /// <summary>
        /// Looses <paramref name="count"/> arrows from the bow, the middle one aimed where she will be (the lead), the
        /// others <see cref="ShooterValues.FanDegrees"/> apart, with one flash at the bow (work order 039), and says so (the
        /// release's sound and the log).
        /// </summary>
        void Loose(EnemyAttackDefinition attack, int count)
        {
            if (attack == null || Player == null) return;
            GroundPoint me = Ground(transform.position);
            Vector3 v = Player.Velocity;
            GroundPoint aim = ProjectileRules.LeadAim(me, Player.GroundPosition, new GroundPoint(v.x, v.z), Tuning.ArrowSpeed,
                                                      Tuning.LeadShare, Tuning.LeadMost);
            GroundPoint dir = aim - me;
            if (dir.Length < 1e-3f) dir = new GroundPoint(transform.forward.x, transform.forward.z);
            dir = dir * (1f / Mathf.Max(1e-6f, dir.Length));
            Face(new Vector3(dir.X, 0f, dir.Z));
            GroundPoint bow = me + dir * (Tuning.BodyRadius * transform.lossyScale.x + 0.2f);
            ThingsThatFly fly = ThingsThatFly.Ensure();
            for (int i = 0; i < count; i++)
                fly.Fire(this, attack, ProjectileRules.Arrow(Tuning, bow, ProjectileRules.FanDirection(dir, i, count, Tuning.FanDegrees)));
            fly.FlashAt(new Vector3(bow.X, 0f, bow.Z), this);
            ThingsThatFly.TellLoosed(this, attack, count);
        }

        protected override bool MayAttack(Vector3 toPlayer, float distance)
        {
            float now = Time.time;
            if (distance < Tuning.BackOffUnder && now >= _corneredUntil) _backingOff = true;
            if (distance >= Tuning.KeepNear) _backingOff = false;
            return !_backingOff && SeesHer();
        }

        /// <summary>
        /// Does it see her: no wall between at its sight height (<see cref="Sees"/>), and not covered by what she is in
        /// cover against (taking cover, 2026-11-19, an experiment)?
        /// </summary>
        public bool SeesHer() => Player != null && Sees(Player.Position) && !Player.ShieldsFrom(transform.position);

        /// <summary>
        /// Where it goes in the chase: <see cref="ShooterPostRules.Choose"/> says what (as before work order 098, or holding its
        /// place); the NavMesh, its sight and its circle's spots are asked here.
        /// </summary>
        protected override void Chase(Vector3 toPlayer, float distance)
        {
            bool holds = HoldsItsPlace;
            Vector3 from = transform.position;
            Vector3 f = transform.forward;
            // Its sight is asked only where the choice reads it: as before, not while it backs off.
            bool sees = !_backingOff && SeesHer();
            ShooterMove m = ShooterPostRules.Choose(Tuning, Ground(from), Ground(f), Player.GroundPosition, sees, _backingOff, _approaching,
                                                    holds, Ground(_post));
            _approaching = m.Approaching;
            if (m.Go != ShooterGo.SeekLine) _seeking = false;
            switch (m.Go)
            {
                case ShooterGo.BackOff:
                {
                    // Straight away from her, as far as the NavMesh allows (and, holding its place, its circle); with no room for that
                    // it stops and shoots.
                    Vector3 to = new Vector3(m.To.X, from.y, m.To.Z);
                    if (NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas)) to = hit.position;
                    if (Flat(to - from).magnitude < ShooterPostRules.CorneredUnder)
                    {
                        _backingOff = false;
                        _corneredUntil = Time.time + CorneredFor;
                        Halt();
                        return;
                    }
                    Walk(to);
                    return;
                }
                case ShooterGo.WalkToHer:
                    Walk(Player.Position);
                    return;
                case ShooterGo.ToPost:
                    Walk(_post);
                    return;
                case ShooterGo.SeekLine:
                    SeekLine(toPlayer);
                    return;
                default:
                    Halt();
                    if (holds) Face(toPlayer); // holding its place it waits facing her
                    return;
            }
        }

        /// <summary>
        /// Holding its place, she in its reach and not seen (work order 098): on to the spot it chose while it is not there; else, every
        /// <see cref="ShooterPostRules.SeekEvery"/>, the nearest spot of its circle that sees her and that it can walk to without leaving
        /// the circle; with none it stands, facing her, and waits.
        /// </summary>
        void SeekLine(Vector3 toPlayer)
        {
            float now = Time.time;
            if (_seeking && now < _seekUntil && Flat(_spot - transform.position).magnitude > ShooterPostRules.AtPost)
            {
                Walk(_spot);
                return;
            }
            _seeking = false;
            if (now >= _nextSeek)
            {
                _nextSeek = now + ShooterPostRules.SeekEvery;
                if (FindSpot(out Vector3 spot))
                {
                    _spot = spot;
                    _seeking = true;
                    _seekUntil = now + ShooterPostRules.SeekMost;
                    Walk(spot);
                    return;
                }
            }
            Halt();
            Face(toPlayer);
        }

        /// <summary>The nearest spot of its circle (<see cref="ShooterPostRules.Spot"/>) that sees her and that it walks to inside the circle. Allocates nothing once its path is made.</summary>
        bool FindSpot(out Vector3 spot)
        {
            spot = default;
            if (Player == null) return false;
            Vector3 me = transform.position;
            GroundPoint post = Ground(_post);
            float radius = Mathf.Max(0f, Tuning.HoldRadius);
            int n = ShooterPostRules.SpotCount;
            for (int i = 0; i < n; i++) s_SpotDistances[i] = ShooterPostRules.Spot(i, post, radius).DistanceTo(Ground(me));
            if (_path == null) _path = new NavMeshPath();
            for (int tried = 0; tried < n; tried++)
            {
                // The nearest not tried yet.
                int best = -1;
                for (int i = 0; i < n; i++)
                    if (s_SpotDistances[i] >= 0f && (best < 0 || s_SpotDistances[i] < s_SpotDistances[best])) best = i;
                if (best < 0) return false;
                s_SpotDistances[best] = -1f;
                GroundPoint g = ShooterPostRules.Spot(best, post, radius);
                if (!NavMesh.SamplePosition(new Vector3(g.X, _post.y, g.Z), out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                if (!ShooterPostRules.Inside(Ground(p), post, radius)) continue;
                if (!SeesHerFrom(p)) continue;
                if (!NavMesh.CalculatePath(me, p, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;
                int corners = _path.GetCornersNonAlloc(s_Corners);
                if (corners >= s_Corners.Length) continue; // a way that long is not a step inside the circle
                bool inside = true;
                for (int c = 0; c < corners && inside; c++) inside = ShooterPostRules.Inside(Ground(s_Corners[c]), post, radius, ShooterPostRules.EdgeSlack);
                if (!inside) continue;
                spot = p;
                return true;
            }
            return false;
        }

        /// <summary>Would it see her from that point: no wall between at its sight height, and not covered by what she is in cover against?</summary>
        bool SeesHerFrom(Vector3 point)
        {
            if (Player == null || Player.ShieldsFrom(point)) return false;
            Vector3 from = Flat(point);
            Vector3 dir = Flat(Player.Position) - from;
            float length = dir.magnitude;
            return length < 1e-3f || ClearDistanceFrom(from, dir / length, length) >= length - 1e-3f;
        }

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            _reach = ClearDistance(AimDirection(), attack.Length);
        }

        /// <summary>How far its line reaches for the walls: its length, or less when a wall cut it at the windup's start.</summary>
        float WallReach(EnemyAttackDefinition attack) => _reach > 0f ? Mathf.Min(_reach, attack.Length) : attack.Length;

        /// <summary>The walls' reach, ended at what she is in cover against while that covers her (an experiment, 2026-11-19).</summary>
        protected override float Reach(EnemyAttackDefinition attack)
        {
            float reach = WallReach(attack);
            if (Player != null && Player.ShieldsFrom(transform.position))
                reach = Player.CoverEntry(Flat(transform.position), AimDirection(), reach);
            return reach;
        }

        /// <summary>The shot would have reached her past the walls, and what she is in cover against covers her from here.</summary>
        protected override bool StoppedByHerCover(EnemyAttackDefinition attack, GroundPoint origin, GroundPoint aim) =>
            Player != null && Player.ShieldsFrom(transform.position)
            && EnemyBrain.Hits(attack, origin, aim, Player.GroundPosition, Placeholders.PlayerRadius, WallReach(attack));

        /// <summary>Does it see that point, with no wall between at its sight height?</summary>
        public bool Sees(Vector3 point)
        {
            Vector3 from = Flat(transform.position) + Vector3.up * Tuning.SightHeight;
            Vector3 to = Flat(point) + Vector3.up * Tuning.SightHeight;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            return length < 1e-3f || ClearDistance(dir / length, length) >= length - 1e-3f;
        }

        /// <summary>
        /// Metres along a flat direction, up to <paramref name="max"/>, before the first wall. With
        /// <see cref="FeelValues.CoverStopsShots"/> on, a wall is cover as her shots take it (<see cref="Cover"/>, Kristian,
        /// 2026-11-19), so it sees and shoots over a low prop as she does; off, it is any collider that is not her, an
        /// enemy or a target, as before.
        /// </summary>
        float ClearDistance(Vector3 flatDir, float max) => ClearDistanceFrom(Flat(transform.position), flatDir, max);

        /// <summary>The same from another point on the ground (work order 098: a spot of its circle it might step to).</summary>
        float ClearDistanceFrom(Vector3 ground, Vector3 flatDir, float max)
        {
            if (Player == null || Player.Feel.CoverStopsShots)
                return Cover.ClearDistance(ground, flatDir, max, Tuning.SightHeight);
            Vector3 from = ground + Vector3.up * Tuning.SightHeight;
            int n = Physics.RaycastNonAlloc(from, flatDir, s_Hits, max, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = max;
            for (int i = 0; i < n; i++)
            {
                Collider c = s_Hits[i].collider;
                if (c == null || IsNotAWall(c)) continue;
                if (s_Hits[i].distance < nearest) nearest = s_Hits[i].distance;
            }
            return nearest;
        }

        bool IsNotAWall(Collider c) =>
            (Player != null && c.transform.IsChildOf(Player.transform))
            || c.GetComponentInParent<Enemy>() != null
            || c.GetComponentInParent<Damageable>() != null;
    }
}
