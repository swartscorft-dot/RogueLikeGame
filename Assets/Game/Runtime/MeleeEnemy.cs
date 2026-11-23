using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Melee you read (decision 0010, Kristian 2026-11-20: "Vi kan gå bort i fra så mye telegrafing på fiender. Heller en
    /// animasjon eller signalisering på dem at de angriper"; work order 035): what the swarmer, the brute and the guard
    /// share. With the switch on (<see cref="PlayerCharacter.MeleeYouRead"/>, read live) each fights with its own set
    /// (<see cref="MeleeAttacks"/>): an ordinary attack in reach on a short cycle, which takes no turn, and one or two skills
    /// on their own cooldowns, which take turns as the heavy attacks did (<see cref="MeleeTactics"/>). Nothing of them is
    /// drawn on the floor (<see cref="Enemy.ReadOnBody"/>): the area a strike hits is still its shape in the rules, and
    /// hitting, missing, the dash and cover work on it as before; what it is about to do is read on its body, from its
    /// attack clip, the glint the effects draw on it (<see cref="CombatEffects"/>) and the windup sounds of its skills.
    /// With the switch off the kind fights exactly as before work order 035, with the attacks of its values, floor shapes
    /// and all.
    ///
    /// The skills' moves, each in the last <see cref="EnemyAttackDefinition.MoveSeconds"/> of the windup so that the strike
    /// still lands as the windup ends, on the clip's strike: a Leap hops to where she stood as it leaves the ground (a
    /// sidestep or the dash makes it land beside her) and bites on landing; a Lunge runs along a line fixed as it starts,
    /// stopping at walls, at the end of the walkable ground and where it meets her, and strikes where it stops, stunning
    /// her when it lands (<see cref="PlayerCharacter.Stun"/>). A Drag goes off as its windup ends: unless a wall or a tall
    /// prop is between them or she is in the dash's protected part, she is pulled to a point in front of it
    /// (<see cref="PlayerCharacter.Pull"/>). A daze, a counter or a death stops a move where it is.
    ///
    /// One guard drags at a time (work order 093, <see cref="Drags"/>): while another's Drag runs and for
    /// <see cref="MeleeAttacks.DragOneAtATime"/> after it ended, its Drag is not allowed, so the choice goes on to its other
    /// attacks by the same rules (<see cref="MeleeTactics.Choose"/>), or it keeps coming in the chase; its Drag's cooldown is
    /// untouched, so it is ready when the hold ends as its own cooldown says.
    /// </summary>
    public abstract class MeleeEnemy : Enemy
    {
        readonly MeleeTactics _tactics = new MeleeTactics();
        System.Func<EnemyAttackDefinition, bool> _allows;

        /// <summary>
        /// The one hold on the guards' Drags for the whole level (work order 093, <see cref="DragHold"/>): she is one target.
        /// Cleared by a new floor (<see cref="GeneratedLevel"/> as it places its enemies, which a new run does too) and by her
        /// death (<see cref="GeneratedLevels"/>); forgotten with the guard that holds it when that one is taken out of the game.
        /// </summary>
        public static readonly DragHold Drags = new DragHold();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDrags()
        {
            Drags.Clear();
            Drags.Gap = MeleeAttacks.DragOneAtATime; // which a test may have changed
        }

        /// <summary>True from its Drag's commit until that Drag ended (work order 093): it holds <see cref="Drags"/>.</summary>
        bool _holdsDrag;
        /// <summary>The hold (<see cref="DragHold.Number"/>) it was last noted held back by: once per guard per hold.</summary>
        int _heldBackBy = -1;

        /// <summary>Times its Drag was held back by another's (work order 093), once per hold. For tests.</summary>
        public int DragsHeldBack { get; private set; }

        bool _moving;
        bool _moveDone;
        EnemyAttackDefinition _move;
        Vector3 _moveFrom, _moveTo;
        float _moveStart, _moveEnd;

        /// <summary>Its attacks with the switch on (work order 035).</summary>
        protected abstract EnemyAttackDefinition[] MeleeSet { get; }

        /// <summary>Does it keep moving with her through its ordinary attack (the swarmer)? No, unless a kind says so.</summary>
        protected virtual bool BitesOnTheMove => false;

        /// <summary>The switch as it stands: melee you read on (the default), read live from her.</summary>
        public bool MeleeOn => Player == null || Player.MeleeYouRead;

        public override bool ReadOnBody => MeleeOn;

        /// <summary>Its tactics: which skill is ready when. For tests.</summary>
        public MeleeTactics Tactics => _tactics;
        /// <summary>The attacks it fights with now: its melee set with the switch on, its values' with it off.</summary>
        public EnemyAttackDefinition[] AttacksNow => MeleeOn ? MeleeSet : Values.Attacks;
        /// <summary>True while a hop or a lunge moves it.</summary>
        public bool IsMoving => _moving;
        /// <summary>Where the hop or lunge going on, or the last one, ends.</summary>
        public Vector3 MoveTarget => _moveTo;

        protected override void Awake()
        {
            base.Awake();
            _allows = Allows;
        }

        // ---------------- Which attack ----------------

        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values) =>
            MeleeOn ? _tactics.Choose(Time.time, distance, MeleeSet, _allows) : base.ChooseAttack(distance, values);

        /// <summary>
        /// The Drag needs to see her; a skill needs a turn (an ordinary attack none), and when it gets none the ordinary
        /// attack is chosen instead if she is in its reach. Asked only of an attack that would otherwise be chosen. Work order
        /// 093: a Drag also needs the hold (<see cref="Drags"/>) free of another guard's, asked before its turn so that a guard
        /// held back by it asks for no turn for a Drag it may not begin.
        /// </summary>
        bool Allows(EnemyAttackDefinition attack)
        {
            if (attack.Move == MeleeMove.Drag && !SeesHer()) return false;
            if (!Drags.Allows(Id, attack, Time.time))
            {
                HeldBack(); // work order 093: another guard's Drag holds it; the choice goes on to its other attacks
                return false;
            }
            if (!AttackTurns.TakesTurn(attack)) return true;
            TurnSort sort = TakesTurns && !IsElite ? AttackSort : TurnSort.None;
            return !Turns.Holds(sort) || Turns.Ask(Id, sort, Time.time);
        }

        /// <summary>Does it see her, with no wall or tall prop between at the height her shots are tested at?</summary>
        public bool SeesHer()
        {
            if (Player == null) return false;
            Vector3 from = Flat(transform.position), d = Flat(Player.Position) - from;
            float length = d.magnitude;
            return length < 1e-3f || Cover.ClearDistance(from, d / length, length, Cover.RayHeight) >= length - 1e-3f;
        }

        protected override void OnWoke() => _tactics.Clear(); // a fresh fight: every skill ready

        /// <summary>
        /// The ordinary attacks it has begun (work order 039): the effects draw the flick of a rapid one on every n-th only
        /// (<see cref="MeleeYouReadRules.FlickShows"/>).
        /// </summary>
        public int OrdinaryBegun { get; private set; }

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            if (attack != null && attack.Ordinary) OrdinaryBegun++;
            _moving = false;
            _moveDone = false;
            _move = null;
            if (MeleeOn) _tactics.Began(attack, Time.time);
            // Work order 093: a Drag committed takes the hold, the first to commit on any frame; at the latest it ends as its
            // wind-up and her pull would.
            if (MeleeOn && attack != null && attack.Move == MeleeMove.Drag)
            {
                Drags.Began(Id, Time.time, attack.Windup + attack.MoveSeconds);
                _holdsDrag = true;
            }
        }

        /// <summary>
        /// Its Drag was held back by another's (work order 093): noted once per hold, with the guard that holds it, for the
        /// session's log.
        /// </summary>
        void HeldBack()
        {
            if (_heldBackBy == Drags.Number) return;
            _heldBackBy = Drags.Number;
            DragsHeldBack++;
            PlaySessionRecorder.Current?.NoteDragHeldBack(LogWord, Id, Drags.Holder, Drags.Gap);
        }

        /// <summary>Its Drag ended at <paramref name="at"/>: the hold runs <see cref="MeleeAttacks.DragOneAtATime"/> from there (work order 093).</summary>
        void EndDrag(float at)
        {
            if (!_holdsDrag) return;
            _holdsDrag = false;
            Drags.Ended(Id, at);
        }

        /// <summary>
        /// After its step (work order 093): a Drag it held that is no longer wound up (a daze dropped it, it died, its room's
        /// fight sent it home) ended now. One that went off ended at its strike, in <see cref="StrikesItself"/>.
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (_holdsDrag && !(State == EnemyState.Windup && Attack != null && Attack.Move == MeleeMove.Drag)) EndDrag(Time.time);
        }

        protected override void OnDestroy()
        {
            Drags.Forget(Id); // work order 093: taken out of the game with its level, the hold it held goes with it
            base.OnDestroy();
        }

        protected override bool ChasesThrough(EnemyAttackDefinition attack) => BitesOnTheMove && attack != null && attack.Ordinary;

        // ---------------- The moves ----------------

        protected override void AfterMove()
        {
            EnemyAttackDefinition attack = Attack;
            if (State != EnemyState.Windup || attack == null || (attack.Move != MeleeMove.Leap && attack.Move != MeleeMove.Lunge))
            {
                _moving = false;
                return;
            }
            float now = Time.time, left = WindupRemaining;
            if (!_moveDone && !_moving && left <= attack.MoveSeconds) BeginMove(attack, now, left);
            if (_moving) RunMove(now);
        }

        void BeginMove(EnemyAttackDefinition attack, float now, float left)
        {
            _moveDone = true;
            if (Player == null) return;
            Vector3 from = Flat(transform.position), her = Flat(Player.Position);
            Vector3 d = her - from;
            float toHer = d.magnitude;
            Vector3 dir = toHer > 1e-4f ? d / toHer : Flat(transform.forward).normalized;
            Vector3 to;
            if (attack.Move == MeleeMove.Leap)
            {
                // Where she stands as it leaves the ground: a sidestep or the dash after this makes it land beside her.
                GroundPoint landing = MeleeYouReadRules.LeapLanding(Ground(from), Ground(her));
                to = WalkableTo(from, new Vector3(landing.X, 0f, landing.Z));
            }
            else
            {
                // Along a line fixed now, no farther than walls, the walkable ground and the shield touching her.
                float most = Mathf.Max(0f, attack.Length);
                float clear = Mathf.Min(Cover.ClearDistance(from, dir, most, Cover.RayHeight), Flat(WalkableTo(from, from + dir * most) - from).magnitude);
                float length = MeleeYouReadRules.LungeLength(toHer, most, Contact, clear);
                to = from + dir * length;
            }
            Face(dir);
            _move = attack;
            _moveFrom = from;
            _moveTo = to;
            _moveStart = now;
            _moveEnd = now + Mathf.Max(1e-3f, left);
            _moving = true;
        }

        void RunMove(float now)
        {
            float k = Mathf.Clamp01((now - _moveStart) / (_moveEnd - _moveStart));
            Vector3 target = Vector3.Lerp(_moveFrom, _moveTo, k);
            Vector3 step = Flat(target - transform.position);
            if (_agent.enabled && _agent.isOnNavMesh && step.sqrMagnitude > 1e-10f) _agent.Move(step);
            // The lunge stops where its shield meets her.
            bool met = _move != null && _move.Move == MeleeMove.Lunge && Player != null
                       && Flat(Player.Position - transform.position).magnitude <= Contact;
            if (k >= 1f || met) StopMove();
        }

        void StopMove()
        {
            _moving = false;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.velocity = Vector3.zero;
        }

        /// <summary>m between its middle and hers when its body touches hers.</summary>
        float Contact => (Body != null ? Body.Radius : 0.5f) + Placeholders.PlayerRadius;

        /// <summary>The point toward <paramref name="to"/> as far as the walkable ground goes from <paramref name="from"/>.</summary>
        static Vector3 WalkableTo(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1f, NavMesh.AllAreas)) return to;
            Vector3 end = new Vector3(to.x, start.position.y, to.z);
            return NavMesh.Raycast(start.position, end, out NavMeshHit edge, NavMesh.AllAreas) ? Flat(edge.position) : Flat(to);
        }

        protected override void OnDaze() => StopMove();

        protected override Vector3 LookVelocity => _moving ? Vector3.zero : base.LookVelocity;

        // ---------------- The strikes that do more than hurt ----------------

        /// <summary>
        /// At the strike, before its hit is tested (the third review of 2026-11-20, work order 047): a hop or a lunge still
        /// under way is carried to its end, and one a long frame skipped (it crossed both the move's start and the strike)
        /// is made now, from where she stands, so the strike is tested where the move arrives. On the strike's frame the
        /// brain is already in its recovery, and the move after it would have stopped where it was.
        /// </summary>
        void FinishMove(EnemyAttackDefinition attack)
        {
            if (attack == null || (attack.Move != MeleeMove.Leap && attack.Move != MeleeMove.Lunge)) return;
            if (!_moveDone && !_moving) BeginMove(attack, Time.time, 0f);
            if (_moving) RunMove(_moveEnd);
        }

        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            FinishMove(attack); // work order 047: the strike is tested where its move arrives
            if (attack.Move == MeleeMove.Drag)
            {
                // Work order 093: it ended here, or with her pull when it landed.
                EndDrag(Drag(attack) ? Time.time + attack.MoveSeconds : Time.time);
                return true;
            }
            if (attack.Stun <= 0f) return false;
            // The Shield bash: its strike as any other, and a stun when it landed.
            bool hit = Player != null && EnemyBrain.Hits(attack, Ground(transform.position), Ground(AimPoint), Player.GroundPosition,
                                                         Placeholders.PlayerRadius, Reach(attack));
            int landed = StrikesLanded;
            Land(attack, hit);
            if (StrikesLanded > landed && Player != null) Player.Stun(attack.Stun, attack.Name);
            return true;
        }

        /// <summary>
        /// The Drag goes off: broken by a wall or a tall prop between them now, or the dash's protected part; out of reach
        /// past its length; else she is pulled to its pull distance in front of it (none when she cannot be held now). True
        /// when she was pulled.
        /// </summary>
        bool Drag(EnemyAttackDefinition attack)
        {
            if (Player == null) return false;
            Vector3 me = Flat(transform.position), her = Flat(Player.Position);
            float distance = (her - me).magnitude;
            if (distance > attack.Length)
            {
                Report(EnemyReportKind.Struck, attack, StrikeOutcome.OutOfArea);
                return false;
            }
            bool clear = SeesHer();
            bool protectedByDash = Player.IsInvulnerable;
            if (MeleeYouReadRules.DragBreaks(clear, protectedByDash))
            {
                // Stepping behind cover is getting out of its way: out of the area.
                Report(EnemyReportKind.Struck, attack, protectedByDash && clear ? StrikeOutcome.AvoidedByDash : StrikeOutcome.OutOfArea);
                return false;
            }
            GroundPoint to = MeleeYouReadRules.PullTarget(Ground(me), Ground(her), attack.PullTo);
            bool pulled = Player.Pull(new Vector3(to.X, 0f, to.Z), attack.MoveSeconds, attack.Name);
            Report(EnemyReportKind.Struck, attack, pulled ? StrikeOutcome.Landed : StrikeOutcome.NoEffect);
            return pulled;
        }
    }
}
