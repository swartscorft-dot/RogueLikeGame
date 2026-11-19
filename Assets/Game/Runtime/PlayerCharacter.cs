using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>The committed action the character is locked in, if any.</summary>
    public enum ActionKind { None, BasicShot, Skill }

    public enum IntentResult { Started, Buffered, Rejected }

    /// <summary>
    /// The player character. Everything that drives it goes through the intent API:
    /// <see cref="Steer"/>, <see cref="Shoot"/>, <see cref="DashToward"/>, <see cref="PressSkill"/>,
    /// <see cref="ReleaseSkill"/>, <see cref="AimAt"/>, <see cref="ActivateIdentity"/> and <see cref="Stop"/>.
    /// Input and tests both call these; nothing else moves the character.
    ///
    /// Movement is a direction the input gives every frame (W A S D, decision 0003): she moves that way at run speed
    /// while it is held, on the NavMesh and sliding along its edges, and stands when it is let go. There are no move
    /// orders and no destinations. While a skill or a dash locks her, the direction does nothing; when the lock ends,
    /// a direction still held moves her at once.
    /// Presses (dash, skill) made during a lock go through the input buffer and expire.
    /// A skill locks the character, and only the dash cancels it.
    ///
    /// The basic shot goes where the cursor points (decision 0002, Kristian 2026-11-18): a straight line toward the
    /// cursor's ground point, aimed at the live cursor until it goes off, hitting the first living target the line
    /// touches and only that one. With nothing in the line it is a miss that still fires. A click never moves her:
    /// standing she shoots from where she stands; moving she shoots on the move, still moving the held way,
    /// slowed while the shot winds up and back to full speed after it.
    ///
    /// Click to shoot: one click, one shot, no auto-attack. A click while she cannot shoot yet waits and fires the
    /// moment the cycle allows; only one waits (<see cref="ClickToShoot"/>). A dash or a skill drops it; movement
    /// never does. A shot that went off makes the next one wait out the whole shot time, so clicking never raises
    /// the fire rate.
    ///
    /// The basic shot never locks. Movement never cancels it (decision 0003; this retires the League rule that a move
    /// order cancels a standing shot): a move key during a standing shot's wind-up makes it a shot on the move, and
    /// one after it has gone off moves her at once. A skill or a dash cancels a shot that has not gone off, at no
    /// cost. A basic shot turns the whole body only if the aim lies outside the front angle; within it the body keeps
    /// its direction and CharacterAnimation turns the upper body.
    ///
    /// Skills on the move (decision 0004, on trial):
    /// - A skill commits her until its last hit (the end of the hold for a Holding skill). With
    ///   <see cref="FeelValues.SkillRecoveryFree"/> on, the recovery after it no longer holds back the basic shot or the
    ///   move keys: the skill ends there for everything but the next skill, which waits until the recovery would have
    ///   ended (a press in that time goes through the input buffer and fires when the recovery ends, however early in
    ///   it: amended 2026-11-19, <see cref="SkillGate"/>). During that free recovery
    ///   <see cref="Action"/> is None (or BasicShot), <see cref="IsLocked"/> is false, <see cref="ActiveSkill"/> is null,
    ///   <see cref="ActiveSkillSlot"/> -1 and <see cref="SkillPhase"/> None; <see cref="FreeRecoverySlot"/> and
    ///   <see cref="SkillGateRemaining"/> say which skill's recovery runs and for how long. A dash ends it, as a dash
    ///   cancels a skill. Off, the whole recovery locks as before.
    /// - A skill pressed on the move slides her to a stand (<see cref="SkillSlide"/>), from the speed she had, along the
    ///   way she was moving. The move keys steer it (amended 2026-11-19, <see cref="FeelValues.SlideSteering"/>): the
    ///   direction follows the held key, the speed still follows the slide. When the skill lets her go the speed she
    ///   may move at eases back up. A skill that moves her itself (Backstep) does neither, and a dash ends both at once.
    ///
    /// The kit (decision 0005, 2026-11-19: the pistols set up and the shotgun cashes in):
    /// - Marks. Builders (<see cref="SkillDefinition.MarksAdded"/>) put marks on each target they hit, and every
    ///   <see cref="KitValues.BasicShotsPerMark"/>-th basic shot hit marks the target it hit. A spender
    ///   (<see cref="SkillDefinition.MarkDamageBonus"/>) takes all marks off each target it hits and deals that share
    ///   more per mark. The marks live on the target (<see cref="Damageable"/>) and show over it (<see cref="MarkPips"/>).
    /// - Equipped slots. A run starts with the skills marked <see cref="SkillDefinition.StartEquipped"/>; a press on
    ///   any other slot is rejected until <see cref="EquipSkill"/> fills it (or <see cref="KitValues.StartWithAllSkills"/>).
    /// - Light skills (<see cref="SkillLock.Light"/>) do not lock her: no stop, slow or slide, the move keys work all
    ///   through, and the body turns no more than for a basic shot. Nothing else starts while one runs, and a dash
    ///   cancels it. Committed skills keep the rules of decision 0004 above.
    /// - Recast skills (<see cref="SkillDefinition.RecastCount"/>, Triple Tap): one shot per press, the next press in
    ///   the window that opens when a shot has gone off (<see cref="RecastChain"/>); the last one clean in the window's
    ///   last share. A dash or another skill ends the chain; the cooldown runs from the first press.
    /// - Backstep (amended 2026-11-19 after Kristian played: a second, harder way to spend marks) is a chain of two that
    ///   opens with a move (<see cref="SkillDefinition.OpeningMove"/>): the first press is the hop and fires nothing;
    ///   when it ends she is free and the window opens; the second press fires the shot at the cursor. A clean second
    ///   press (<see cref="SkillDefinition.CleanMarkDamageBonus"/>) spends the marks of each target it hits, as a
    ///   spender does; an earlier one is an ordinary shot that adds its mark. The shot itself does not move her.
    /// - Focus fills from spent marks, clean taps and a trickle of damage; spending it cuts every running cooldown and,
    ///   for its window, makes every basic shot hit mark.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        /// <summary>Metres ahead of her, along the held direction, that she is steered toward each frame.</summary>
        const float SteerLookAhead = 1.5f;
        /// <summary>Where a shot starts without a pistol to fire it from: this high over the character's ground point.</summary>
        public const float FallbackMuzzleHeight = 0.6f;
        /// <summary>Metres between streaks side by side across a line skill. A line narrower than twice this shows one streak.</summary>
        public const float LineStreakSpacing = 0.5f;

        public FeelSettings Settings;
        public SkillSettings SkillSet;
        [Tooltip("How each shot looks. Optional: without it the shots use the start values.")]
        public ShotFxSettings ShotLooks;
        [Tooltip("The material of the shot effects: unlit, drawn in the colours of their vertices.")]
        public Material ShotFxMaterial;
        [Tooltip("The barrel end of the pistol in the right hand. Optional: without a muzzle, shots start at the character.")]
        public Transform RightMuzzle;
        public Transform LeftMuzzle;
        [Tooltip("Plays the shot sounds. Optional: without it the shots are silent.")]
        public ShotSounds Sounds;

        enum PressKind { Dash, Skill }

        struct Press
        {
            public PressKind Kind;
            public Vector3 Point;
            public int Slot; // which skill, for a skill press
            public float At; // when it was pressed
        }

        readonly FeelValues _fallbackFeel = new FeelValues();
        readonly Dash _dash = new Dash();
        readonly ActionTimeline _action = new ActionTimeline();
        readonly InputBuffer<Press> _buffer = new InputBuffer<Press>();
        readonly List<TargetInfo> _targetScratch = new List<TargetInfo>();
        readonly ClickToShoot _clicks = new ClickToShoot();
        readonly ShotFacing _shotFacing = new ShotFacing();
        readonly BackpedalCap _backpedalCap = new BackpedalCap();
        Vector3 _runDirection = Vector3.forward;    // the way she last moved, kept through a hit stop
        Vector3 _moveInput;                         // the direction the input holds now, flat and of length 1, or zero
        readonly SkillCast _cast = new SkillCast();
        readonly List<Cooldown> _cooldowns = new List<Cooldown>();
        readonly SkillDefinition[] _fallbackSkills = SkillDefaults.Create();
        readonly FocusGauge _focus = new FocusGauge();
        readonly IdentityValues _fallbackIdentity = new IdentityValues();
        readonly ShotLook _fallbackBasicLook = ShotFxDefaults.Basic();
        readonly Dictionary<string, ShotLook> _fallbackLooks = new Dictionary<string, ShotLook>();
        // The kit (decision 0005).
        readonly KitValues _fallbackKit = new KitValues();
        readonly BasicShotMarks _basicMarks = new BasicShotMarks();
        readonly RecastChain _chain = new RecastChain();
        readonly List<bool> _equipped = new List<bool>();
        readonly Dictionary<Damageable, float> _markedAt = new Dictionary<Damageable, float>(); // by the skill in use, for its mark interval
        bool _castClean; // the shot of the skill in use came from a clean press: a clean tap or a clean spend
        bool _castMoves;   // the skill in use moves her over its startup (MoveDistance): not the shot after an opening move
        bool _castOpening; // the skill in use is a chain's opening move: it fires nothing (Backstep's hop)

        NavMeshAgent _agent;


        ActionKind _actionKind;
        Vector3 _aimPoint;
        Vector3 _shotCursor;                        // where the basic shot aims: the latest click or cursor report
        int _castSlot = -1;
        float _nextShotAt = float.NegativeInfinity; // the earliest the next basic shot may start
        float _shotStartedAt;
        bool _movingShot;                           // the basic shot in progress is a shot on the move
        bool _shotTurnsBody;                        // the basic shot in progress turned the whole body to its aim
        // The speed around shots on the move (MovingShot.SpeedShare): eased down from the share it had when the
        // latest such shot started, and back up once that shot has gone off and the slow-down is done.
        bool _easing;
        float _easeStartedAt;
        float _easeFrom = 1f;
        float _easeFiredAfter = -1f;
        Vector3 _cursorPoint;
        bool _hasCursor;     // a cursor report came after the skill in use started
        bool _cursorKnown;   // a cursor report came at all: where she looks
        bool _bufferedSkillReleased;
        float _castMoved;      // how far the skill in use has moved the character so far
        Vector3 _castMoveDir;  // along the aim it was pressed with

        bool _leftHandNext; // the bullets of one shot come from one hand, and the next shot from the other

        bool _dashing;
        Vector3 _dashFrom;
        Vector3 _dashTo;

        // Skills on the move (decision 0004).
        float _skillGateUntil = float.NegativeInfinity; // a free recovery: no new skill before this
        int _gateSlot = -1;                             // whose recovery it is
        float _gateLetGoAt;                             // when that skill let her go: its last hit, or the end of the hold
        bool _sliding;          // the skill in use slides her to a stand
        float _slideFrom;       // the share of run speed she had at the press
        float _slideStartedAt;
        Vector3 _slideDir;      // the way she slides: at the press the way she was moving, then the held way; flat, of length 1
        bool _rising;           // let go by a skill: the speed she may move at eases back up
        float _riseFrom;
        float _riseStartedAt;
        Vector3 _lastVelocity;  // her velocity in the last frame that time ran, for a press during a hit stop

        // ---------------- State, for the readout and for tests ----------------

        public FeelValues Feel => Settings != null ? Settings.Values : _fallbackFeel;
        public Vector3 Position => transform.position;
        public GroundPoint GroundPosition => new GroundPoint(transform.position.x, transform.position.z);
        public float Speed => _agent.velocity.magnitude;
        public Vector3 Velocity => _agent.velocity;

        /// <summary>The direction the input holds now: flat, of length 1, or zero when no move key is held.</summary>
        public Vector3 MoveInput => _moveInput;
        /// <summary>True while a move key is held and she is free to move: not dashing and not in a skill, or in a light one.</summary>
        public bool IsMoving => _moveInput != Vector3.zero && !_dashing && (_actionKind != ActionKind.Skill || IsInLightSkill);
        /// <summary>True while a light skill runs (decision 0005): she moves under it as under a basic shot.</summary>
        public bool IsInLightSkill => _actionKind == ActionKind.Skill && _cast.Skill != null && _cast.Skill.Lock == SkillLock.Light;
        /// <summary>True once the cursor has been reported; <see cref="LookPoint"/> is then where it is on the ground.</summary>
        public bool HasLookPoint => _cursorKnown;
        public Vector3 LookPoint => _cursorPoint;
        public ActionKind Action => _actionKind;
        /// <summary>The phase of the lock. For a skill, holding or charging counts as Active.</summary>
        public ActionPhase ActionPhase
        {
            get
            {
                if (_actionKind == ActionKind.BasicShot) return _action.Phase(Time.time);
                if (_actionKind != ActionKind.Skill) return ActionPhase.None;
                switch (_cast.Phase(Time.time))
                {
                    case SkillPhase.Startup: return ActionPhase.Startup;
                    case SkillPhase.Channel: return ActionPhase.Active;
                    case SkillPhase.Active: return ActionPhase.Active;
                    case SkillPhase.Recovery: return ActionPhase.Recovery;
                    default: return ActionPhase.None;
                }
            }
        }

        /// <summary>Seconds from the start of the current shot or skill to its first hit, as known when it started.</summary>
        public float ActionLeadTime { get; private set; }

        // ---------------- Skills, for the skill bar, the aim indicator and tests ----------------

        public IReadOnlyList<SkillDefinition> Skills =>
            SkillSet != null && SkillSet.Skills != null && SkillSet.Skills.Length > 0 ? SkillSet.Skills : _fallbackSkills;

        /// <summary>The slot of the skill in use, or -1.</summary>
        public int ActiveSkillSlot => _actionKind == ActionKind.Skill ? _castSlot : -1;
        public SkillDefinition ActiveSkill => _actionKind == ActionKind.Skill ? _cast.Skill : null;
        public SkillPhase SkillPhase => _actionKind == ActionKind.Skill ? _cast.Phase(Time.time) : SkillPhase.None;
        /// <summary>0 to 1, for a bar: the cast, the hold or the charge of the skill in use.</summary>
        public float SkillProgress => _actionKind == ActionKind.Skill ? _cast.Progress(Time.time) : 0f;
        public float SkillCharge => _actionKind == ActionKind.Skill ? _cast.Charge(Time.time) : 0f;
        /// <summary>Where the skill in use is aimed.</summary>
        public Vector3 AimPoint => _aimPoint;
        public float SkillCooldownRemaining(int slot) =>
            slot >= 0 && slot < Skills.Count ? CooldownFor(slot).Remaining(Time.time) : 0f;
        /// <summary>
        /// The slot whose recovery is running free (decision 0004): the skill is over for the basic shot and the move
        /// keys, but the next skill waits for it. -1 when none.
        /// </summary>
        public int FreeRecoverySlot => Time.time < _skillGateUntil ? _gateSlot : -1;
        /// <summary>Seconds until a new skill may start after a free recovery; 0 when it may start now (or a lock holds it).</summary>
        public float SkillGateRemaining => Mathf.Max(0f, _skillGateUntil - Time.time);
        /// <summary>True while a skill slides her to a stand (decision 0004).</summary>
        public bool IsSkillSliding => _actionKind == ActionKind.Skill && _sliding;
        /// <summary>The share of run speed the latest skill slide started from: what she had at the press.</summary>
        public float SlideStartShare => _slideFrom;
        /// <summary>
        /// The live cursor as the character knows it: the latest cursor report, click or skill press. The arms
        /// follow it while they are held up after a shot (CharacterAnimation).
        /// </summary>
        public Vector3 AimCursor => _shotCursor;

        // ---------------- The identity gauge: focus ----------------

        public IdentityValues Identity => SkillSet != null && SkillSet.Identity != null ? SkillSet.Identity : _fallbackIdentity;
        public float Focus => _focus.Focus;
        public bool IsFocusFull => _focus.IsFull(Identity);
        public bool IsEmpowered => _focus.IsEmpowered(Time.time);
        public float EmpoweredRemaining => _focus.EmpoweredRemaining(Time.time);
        /// <summary>For the bar, 0 to 1: how full the gauge is, or while empowered how much of the mode is left.</summary>
        public float FocusBarFill => _focus.BarFill(Time.time, Identity);
        public bool IsDashing => _dashing;

        // ---------------- The kit (decision 0005) ----------------

        public KitValues Kit => SkillSet != null && SkillSet.Kit != null ? SkillSet.Kit : _fallbackKit;

        /// <summary>True when the slot holds a skill she can use: one she started the run with, or was given since.</summary>
        public bool IsEquipped(int slot)
        {
            if (slot < 0 || slot >= Skills.Count) return false;
            if (Kit.StartWithAllSkills) return true;
            while (_equipped.Count <= slot) _equipped.Add(Skills[_equipped.Count].StartEquipped);
            return _equipped[slot];
        }

        /// <summary>Puts the slot's skill on the bar: its key works from now on. For tests and for the rewards to come.</summary>
        public void EquipSkill(int slot)
        {
            if (slot < 0 || slot >= Skills.Count) return;
            IsEquipped(slot);
            _equipped[slot] = true;
        }

        /// <summary>Back to the skills a run starts with. For tests and tools.</summary>
        public void EquipStartSkills() => _equipped.Clear();

        /// <summary>Basic shot hits counted toward the next mark.</summary>
        public int BasicShotsTowardMark => _basicMarks.Counted;
        /// <summary>The slot whose recast chain waits for its next press, or -1.</summary>
        public int RecastSlot => _chain.Slot;
        /// <summary>Shots of the recast chain pressed so far.</summary>
        public int RecastShots => _chain.Slot >= 0 ? _chain.Shots : 0;
        /// <summary>True while the window for the next press of a recast chain is open.</summary>
        public bool IsRecastWindowOpen => _chain.IsWindowOpen(Time.time);
        /// <summary>For a bar, 1 to 0: how much of the open recast window is left.</summary>
        public float RecastWindowLeft => _chain.WindowLeft(Time.time);
        /// <summary>
        /// The share of the open window, at its end, in which the next press is clean; 0 when the next press cannot be
        /// clean (not the last, or the skill has neither a clean tap nor a clean spend).
        /// </summary>
        public float RecastCleanShare =>
            _chain.Slot >= 0 && _chain.NextIsLast && _chain.Slot < Skills.Count && RecastChain.HasCleanPress(Skills[_chain.Slot])
                ? Mathf.Clamp01(Kit.CleanShare) : 0f;

        /// <summary>
        /// True while the character cannot start anything new: in a dash or a skill. A basic shot does not lock, and
        /// neither does a free recovery (decision 0004), which holds back only the next skill (<see cref="SkillGateRemaining"/>).
        /// </summary>
        public bool IsLocked => _dashing || _actionKind == ActionKind.Skill;
        /// <summary>True from the moment a basic shot has gone off until its recovery is over or is cut short.</summary>
        public bool IsInShotRecovery => _actionKind == ActionKind.BasicShot && _action.HitResolved;
        public bool IsInvulnerable => _dash.IsInvulnerable(Time.time);
        public float DashCooldownRemaining => _dash.CooldownRemaining(Time.time);
        public bool HasBufferedPress => _buffer.HasItem;
        /// <summary>True while a click waits for its shot.</summary>
        public bool HasPendingShot => _clicks.Waiting;
        /// <summary>True while a basic shot is fired on the move: the move order goes on under it.</summary>
        public bool IsShootingOnTheMove => _actionKind == ActionKind.BasicShot && _movingShot;
        /// <summary>True while the basic shot in progress turned the whole body to its aim (it lay outside the front angle).</summary>
        public bool ShotTurnsBody => _actionKind == ActionKind.BasicShot && _shotTurnsBody;
        /// <summary>The speed the character may walk at now: the run speed, or less while it shoots on the move.</summary>
        public float SpeedLimit => _agent.speed;
        /// <summary>
        /// True while the speed is eased down or back up around a shot on the move: the shooting gait, forward or
        /// (with the body turned to a target behind) backward.
        /// </summary>
        public bool InShootingGait => _easing || _shotFacing.State != ShotFacing.Mode.Free || IsSkillSliding;
        /// <summary>
        /// Where the body faces around shots that must turn it: free, held turned to the aim (backpedalling while she
        /// keeps shooting), or turning back to the run (<see cref="ShotFacing"/>).
        /// </summary>
        public ShotFacing.Mode FacingMode => _shotFacing.State;
        /// <summary>Counts the shots and skill hits fired from a pistol, for the recoil. Which hand and how hard is in the next two.</summary>
        public int KickCount { get; private set; }
        public bool KickLeftHand { get; private set; }
        public float KickStrength { get; private set; }
        /// <summary>Counts every basic shot that started on the move.</summary>
        public int ShotsOnTheMove { get; private set; }

        public int DashesStarted { get; private set; }
        /// <summary>Counts every basic shot and skill that started its startup, whether or not it went on to fire.</summary>
        public int ActionsStarted { get; private set; }
        public int BasicShotsFired { get; private set; }
        public int BasicShotHits { get; private set; }
        public int SkillShotsFired { get; private set; }
        public int SkillShotHits { get; private set; }

        /// <summary>
        /// Tells what she did as it happens: what became of each press, each skill and shot that started, went off or
        /// ended, each hit. For the play session recorder (<see cref="PlaySessionRecorder"/>); nothing in the game listens,
        /// and it changes nothing.
        /// </summary>
        public event System.Action<PlayerReport> Reported;

        void Report(PlayerReport report) => Reported?.Invoke(report);

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.autoBraking = false; // she is steered a little way ahead every frame: no braking toward that point
            _agent.stoppingDistance = 0f;
            ApplyMovementFeel(Feel);
        }

        // ---------------- Intent API ----------------

        /// <summary>
        /// The move keys: walk this way (any length; it is laid flat and made of length 1), or stand with zero. Given
        /// every frame by the input; it holds until it is changed. It never cancels a shot or drops a waiting click.
        /// </summary>
        public void Steer(Vector3 direction)
        {
            Vector3 flat = Flat(direction);
            _moveInput = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.zero;
        }

        /// <summary>
        /// Press: one basic shot toward a point on the ground, the cursor. It never moves her: standing she shoots
        /// from where she stands, moving she shoots on the move. The shot aims at the live cursor
        /// (<see cref="AimAt"/>) until it goes off. A click while she cannot shoot yet waits for the next shot.
        /// </summary>
        public void Shoot(Vector3 point)
        {
            _shotCursor = point;
            if (!_clicks.Click(Busy, Time.time, _nextShotAt))
            {
                Report(new PlayerReport { Kind = PlayerReportKind.ShotClicked, Result = IntentResult.Buffered });
                return;
            }
            Report(new PlayerReport { Kind = PlayerReportKind.ShotClicked, Result = IntentResult.Started });
            StartBasicShot(Feel, onTheMove: IsMoving);
        }

        /// <summary>True while she cannot start a basic shot now: a shot, a skill or a dash is going.</summary>
        bool Busy => _actionKind != ActionKind.None || _dashing;

        /// <summary>Let go of the move keys: she stands. A shot or dash in progress still finishes. For tests and tools.</summary>
        public void Stop()
        {
            _moveInput = Vector3.zero;
            if (!IsLocked) Halt();
        }

        /// <summary>
        /// Press: dash toward a point. Cancels a shot or skill in progress.
        /// Rejected while on cooldown or already dashing.
        /// </summary>
        public IntentResult DashToward(Vector3 point)
        {
            if (_dashing || !_dash.CanStart(Time.time))
                return DashPressed(IntentResult.Rejected, _dashing ? 0f : _dash.CooldownRemaining(Time.time));

            DropWaitingClick(); // a dash drops a waiting click
            _shotFacing.End(turnBack: false); // and ends the backpedal at once: the dash faces its own way
            if (_actionKind != ActionKind.None)
            {
                if (_actionKind == ActionKind.Skill && !Feel.DashCancelsSkill)
                    return DashPressed(BufferPress(PressKind.Dash, point), 0f);
                if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.CancelledByDash, Time.time);
                _action.Cancel();
                _cast.Cancel();
                EndAction();
            }
            _easing = false; // a dash ends the slow of a shot on the move at once
            return DashPressed(StartDash(point) ? IntentResult.Started : IntentResult.Rejected, 0f);
        }

        /// <summary>Reports what became of a dash press, and returns it.</summary>
        IntentResult DashPressed(IntentResult result, float cooldownLeft)
        {
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.DashPressed, Result = result, CooldownLeft = cooldownLeft,
                AlreadyDashing = result == IntentResult.Rejected && _dashing,
            });
            return result;
        }

        /// <summary>Drops a waiting click, as a dash or a skill does, and reports it if one was waiting.</summary>
        void DropWaitingClick()
        {
            if (_clicks.Waiting) Report(new PlayerReport { Kind = PlayerReportKind.ShotDropped });
            _clicks.Clear();
        }

        /// <summary>Reports that the skill in use ends now, and how. Called before the skill is cancelled or let go.</summary>
        void ReportSkillEnded(SkillEnding ending, float now)
        {
            if (Reported == null || _cast.Skill == null) return;
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.SkillEnded, Slot = _castSlot, Ending = ending,
                Held = _cast.HeldSeconds(now), Charge = _cast.Charge(now), Fired = _cast.HitsTaken > 0 && !_castOpening,
            });
        }

        /// <summary>
        /// Press: the key of a skill went down, with the cursor over this point.
        /// Rejected while the skill is on cooldown. Buffered while the character is locked.
        /// The cooldown starts when the skill starts, so a skill cancelled by a dash is still spent.
        /// </summary>
        public IntentResult PressSkill(int slot, Vector3 point)
        {
            if (slot < 0 || slot >= Skills.Count) return SkillPressed(slot, IntentResult.Rejected, 0f);
            if (!IsEquipped(slot))
            {
                Report(new PlayerReport { Kind = PlayerReportKind.SkillPressed, Slot = slot, Result = IntentResult.Rejected, NotEquipped = true });
                return IntentResult.Rejected;
            }
            // A recast (decision 0005): inside the window it fires the next shot at once, whatever the cooldown; before
            // the shot it follows has gone off it waits like any press during a skill.
            ExpireChain(Time.time);
            RecastPress recast = _chain.Judge(slot, Time.time, Kit.CleanShare, RecastChain.HasCleanPress(Skills[slot]));
            if (recast == RecastPress.Shot || recast == RecastPress.Clean)
            {
                _shotCursor = point;
                SkillPressed(slot, IntentResult.Started, 0f);
                Recast(slot, point, recast == RecastPress.Clean);
                return IntentResult.Started;
            }
            if (recast == RecastPress.Early)
            {
                _shotCursor = point;
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            if (!CooldownFor(slot).IsReady(Time.time))
                return SkillPressed(slot, IntentResult.Rejected, CooldownFor(slot).Remaining(Time.time));
            _shotCursor = point; // a press comes with the cursor: the arms held up after the skill follow it from here
            // In a lock, or in a free recovery: the next skill waits out the recovery, through the buffer.
            if (IsLocked || Time.time < _skillGateUntil)
            {
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            SkillPressed(slot, IntentResult.Started, 0f);
            StartSkill(slot, point);
            return IntentResult.Started;
        }

        /// <summary>Reports what became of a skill press, and returns it.</summary>
        IntentResult SkillPressed(int slot, IntentResult result, float cooldownLeft)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.SkillPressed, Slot = slot, Result = result, CooldownLeft = cooldownLeft });
            return result;
        }

        /// <summary>
        /// The next shot of a recast chain: the shot before it is over (it went off; its recovery, if it is still in
        /// it, is cut), and this one starts at once with the skill's startup. A clean tap gains its focus here.
        /// </summary>
        void Recast(int slot, Vector3 point, bool clean)
        {
            float now = Time.time;
            _chain.Recast();
            float focus = clean ? _focus.Add(Identity.FocusPerCleanTap, Identity, now) : 0f;
            Report(new PlayerReport { Kind = PlayerReportKind.RecastPressed, Slot = slot, Shot = _chain.Shots, Clean = clean, Focus = focus });
            if (_actionKind == ActionKind.Skill)
            {
                ReportSkillEnded(SkillEnding.RanOut, now);
                _cast.Cancel();
                EndAction();
            }
            StartSkill(slot, point, recast: true);
            _castClean = clean;
        }

        /// <summary>Ends a recast chain whose window has run out.</summary>
        void ExpireChain(float now)
        {
            if (_chain.Expired(now)) EndChain();
        }

        /// <summary>Ends the recast chain, if one runs, and reports how many shots it fired.</summary>
        void EndChain()
        {
            if (_chain.Slot < 0) return;
            int slot = _chain.Slot, count = _chain.Count;
            int shots = _chain.End();
            Report(new PlayerReport { Kind = PlayerReportKind.ChainEnded, Slot = slot, Shot = shots, Early = shots < count });
        }

        /// <summary>
        /// A skill press that waited in the buffer may start now: a recast whose window has opened, or an ordinary start.
        /// A press that is neither any more (its chain ran out and the skill is cooling down) is dropped.
        /// </summary>
        bool StartWaitingSkill(Press press, float now)
        {
            ExpireChain(now);
            RecastPress recast = _chain.Judge(press.Slot, now, Kit.CleanShare, RecastChain.HasCleanPress(Skills[press.Slot]));
            if (recast == RecastPress.Shot || recast == RecastPress.Clean)
            {
                Recast(press.Slot, press.Point, recast == RecastPress.Clean);
                return true;
            }
            if (recast == RecastPress.None && CooldownFor(press.Slot).IsReady(now))
            {
                StartSkill(press.Slot, press.Point);
                return true;
            }
            Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = press.Slot });
            return false;
        }

        /// <summary>
        /// The key of a skill went up. Ends a Holding skill and fires a Charge skill.
        /// If that skill's press is still waiting in the buffer, the skill starts as already released.
        /// </summary>
        public void ReleaseSkill(int slot)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.SkillReleased, Slot = slot });
            if (_actionKind == ActionKind.Skill && _castSlot == slot) _cast.Release(Time.time);
            // Only the waiting skill's own key: the key of the skill before it going up says nothing about this one.
            // A press can wait out a whole free recovery now, and keys are rolled: the next goes down before the last
            // comes up. Before 2026-11-19 any skill key going up did this, and a Charge skill that waited fired at
            // once with no charge.
            else if (_buffer.TryPeek(out Press waiting) && waiting.Kind == PressKind.Skill && waiting.Slot == slot)
                _bufferedSkillReleased = true;
        }

        /// <summary>
        /// Where the cursor is. A skill that is being held or charged follows it, from the first report
        /// after the skill started. Without a report it stays aimed at the point it was pressed with.
        /// A basic shot follows it until it goes off, and she looks toward it.
        /// </summary>
        public void AimAt(Vector3 point)
        {
            _cursorPoint = point;
            _hasCursor = true;
            _cursorKnown = true;
            _shotCursor = point;
        }

        /// <summary>
        /// Press: spend a full focus gauge on the empowered mode. It takes no time and locks nothing,
        /// so it can be pressed in the middle of a skill. Rejected unless the gauge is full.
        /// </summary>
        public IntentResult ActivateIdentity()
        {
            float now = Time.time;
            IntentResult result = _focus.TryActivate(now, Identity) ? IntentResult.Started : IntentResult.Rejected;
            Report(new PlayerReport { Kind = PlayerReportKind.FocusPressed, Result = result });
            if (result == IntentResult.Started)
            {
                // Decision 0005: every running cooldown, the dash's too, is cut by a share of what it has left.
                float share = Identity.CooldownCut;
                var cuts = new float[Skills.Count];
                for (int slot = 0; slot < cuts.Length; slot++) cuts[slot] = CooldownFor(slot).Cut(now, share);
                float dashCut = _dash.CutCooldown(now, share);
                Report(new PlayerReport { Kind = PlayerReportKind.CooldownsCut, Cuts = cuts, DashCut = dashCut });
            }
            return result;
        }

        /// <summary>Fills the focus gauge. For tests and tools.</summary>
        public void FillFocus() => _focus.Fill(Identity);

        /// <summary>Press and let go of the first skill at once. Shorthand for tests and tools.</summary>
        public IntentResult CastAt(Vector3 point)
        {
            IntentResult result = PressSkill(0, point);
            if (result != IntentResult.Rejected) ReleaseSkill(0);
            return result;
        }

        /// <summary>Put the character somewhere else at once and clear what it was doing. For tests and tools.</summary>
        public void Teleport(Vector3 point)
        {
            if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.Cancelled, Time.time);
            _action.Cancel();
            _cast.Cancel();
            EndAction();
            _dashing = false;
            _hasCursor = false;
            _cursorKnown = false;
            _buffer.Clear();
            _clicks.Clear();
            _shotFacing.End(turnBack: false);
            _backpedalCap.Reset();
            _easing = false;
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            _moveInput = Vector3.zero;
            EndChain();
            _agent.Warp(new Vector3(point.x, 0f, point.z));
            Halt();
        }

        // ---------------- Simulation ----------------

        void Update()
        {
            FeelValues feel = Feel;
            float now = Time.time;
            ApplyMovementFeel(feel);
            ExpireChain(now);

            if (_dashing)
            {
                Vector3 target = Vector3.Lerp(_dashFrom, _dashTo, _dash.Progress(now));
                // NavMeshAgent.Move keeps the character on the NavMesh, so a dash stops at walls and edges.
                _agent.Move(Flat(target - transform.position));
                if (_dash.IsDashing(now)) return;
                _dashing = false;
            }

            if (_actionKind == ActionKind.BasicShot)
            {
                // The aim follows the live cursor until the shot goes off, and stays where it went after.
                if (!_action.HitResolved) _aimPoint = AimEnd(_shotCursor);
                if (_action.ConsumeHit(now))
                {
                    // Only a shot that went off makes the next one wait.
                    _nextShotAt = _shotStartedAt + _action.Total;
                    if (_movingShot && _easing) _easeFiredAfter = now - _easeStartedAt;
                    if (ResolveBasicShot()) HitStop.Trigger(feel.BasicShotHitStop);
                }
                // A buffered press that may start now takes over at once, as the press itself would have: a skill that
                // waited out a free recovery (decision 0004) cancels a shot that has not gone off, like any skill.
                bool takenOver = PressReady(now);
                if (_movingShot)
                {
                    // On the move: she keeps moving the held way under the shot, at the speed ApplyMovementFeel sets.
                    // The body faces the aim if the shot turned it, else the way she moves.
                    Walk();
                    UpdateFacing(feel);
                    if (_action.IsRunning(now) && !takenOver) return;
                    _action.Cancel();
                    EndAction();
                }
                else
                {
                    // A move key never cancels a standing shot. Before it has gone off, the shot becomes a shot on the
                    // move, slowing from this moment; after, the recovery is cut short and she moves at once.
                    // A waiting click does not cut it short: the recovery runs out exactly when the next shot may
                    // start, so the arms stay up between shots clicked in rhythm.
                    if (IsMoving && !_action.HitResolved && !takenOver)
                    {
                        _movingShot = true;
                        ShotsOnTheMove++;
                        // From the speed she actually has, as a skill's slide does: from the stand of a standing shot,
                        // zero. The limit ApplyMovementFeel set at the top of this frame was for a stand with no slow, so
                        // it is set again from the curve before she moves.
                        StartEasing(feel, Mathf.Min(CurrentSpeedShare(feel), ActualSpeedShare(feel)));
                        _agent.speed = feel.RunSpeed * CurrentSpeedShare(feel);
                        Walk();
                        UpdateFacing(feel);
                        return;
                    }
                    bool cutShort = takenOver || (_action.HitResolved && IsMoving);
                    UpdateFacing(feel);
                    if (_action.IsRunning(now) && !cutShort) return;
                    _action.Cancel();
                    EndAction();
                }
            }
            else if (_actionKind == ActionKind.Skill)
            {
                SkillDefinition skill = _cast.Skill;
                // A skill that moves the character does so over its startup, along the aim it was pressed with.
                if (_castMoves)
                {
                    float moved = skill.MoveDistance * _cast.StartupProgress(now);
                    if (moved != _castMoved)
                    {
                        _agent.Move(_castMoveDir * (moved - _castMoved));
                        _castMoved = moved;
                    }
                }
                bool light = skill.Lock == SkillLock.Light;
                // A skill that is held or charged follows the cursor until the key goes up.
                if (_hasCursor && _cast.Phase(now) == SkillPhase.Channel)
                {
                    _aimPoint = _cursorPoint;
                    if (feel.FaceCursorOnCast && !light) Face(Flat(_aimPoint - transform.position));
                }

                // Pressed on the move, she slides to a stand along the way she was moving (decision 0004).
                if (_sliding) Slide(feel, now);
                // A light skill (decision 0005): she moves the held way under it, as under a basic shot, and the body
                // faces as for a basic shot.
                if (light)
                {
                    Walk();
                    UpdateFacing(feel);
                }

                // An opening move (Backstep's hop, amended 2026-11-19): it fires nothing. When the move has ended (its
                // startup is over: the moment its shot went off before) the window for the shot opens and she is free at
                // once, with no recovery and nothing that holds back the next skill.
                if (_castOpening)
                {
                    if (_cast.TakeHits(now) == 0) return;
                    _chain.ShotWentOff(now);
                    ReportSkillEnded(SkillEnding.RanOut, now);
                    LetGo(skill, now);
                    _cast.Cancel();
                    EndAction();
                }
                else
                {
                    float charge = _cast.Charge(now);
                    bool landed = false;
                    int fired = _cast.TakeHits(now);
                    for (int hits = fired; hits > 0; hits--) landed |= ResolveSkillHit(skill, charge);
                    if (landed) HitStop.Trigger(feel.HitStop);
                    // A shot of a recast chain went off: the window for the next press opens, or the chain is complete.
                    if (fired > 0 && _chain.Slot == _castSlot)
                    {
                        _chain.ShotWentOff(now);
                        if (_chain.IsComplete) EndChain();
                    }
                    // Committed until the last hit. With the free recovery, that is where the skill lets her go; the next
                    // skill still waits until the recovery would have ended. Without it, the recovery locks as before.
                    if (feel.SkillRecoveryFree ? _cast.IsCommitted(now) : _cast.IsRunning(now)) return;
                    if (feel.SkillRecoveryFree)
                    {
                        // Set even when a long frame has already carried her past the recovery's end: a press that waited
                        // is then judged from the moment the skill let her go, as at any other gate (SkillGate).
                        _skillGateUntil = _cast.RecoveryEndsAt;
                        _gateLetGoAt = _cast.RecoveryEndsAt - skill.Recovery;
                        _gateSlot = _castSlot;
                    }
                    ReportSkillEnded(SkillEnding.RanOut, now);
                    LetGo(skill, now);
                    _cast.Cancel();
                    EndAction();
                }
            }

            // Free from here on: first a buffered press that may start now, then a waiting click, then the order.
            // A skill press that waits out a free recovery does not hold back the click or the move keys meanwhile.
            bool pressReady = PressReady(now);
            if (!pressReady && _clicks.TakeWhenAllowed(Busy, now, _nextShotAt))
            {
                StartBasicShot(feel, onTheMove: IsMoving);
                return;
            }
            if (pressReady && _buffer.TryPeek(out Press press))
            {
                _buffer.Clear(); // PressReady has judged it good
                if (press.Kind == PressKind.Skill)
                {
                    if (StartWaitingSkill(press, now))
                    {
                        if (_bufferedSkillReleased) _cast.Release(now);
                        return;
                    }
                }
                else
                if (StartDash(press.Point)) return;
            }

            Walk();
            UpdateFacing(feel);
        }

        /// <summary>
        /// Moves her the held way at the speed the feel allows now, by steering the NavMesh agent a little way ahead:
        /// she stays on the NavMesh and slides along its edges. With no direction held she stops at once.
        /// </summary>
        void Walk()
        {
            if (!_agent.isOnNavMesh) return;
            if (_moveInput == Vector3.zero)
            {
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            MoveAlong(_moveInput, _agent.speed);
        }

        /// <summary>Moves her along a flat direction of length 1 at this speed, steering the agent a little way ahead.</summary>
        void MoveAlong(Vector3 direction, float speed)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            _agent.SetDestination(transform.position + direction * SteerLookAhead);
            // Start at once, as the movement always has: the agent would otherwise wait a frame for its path.
            _agent.velocity = direction * speed;
        }

        /// <summary>
        /// The body's facing, every frame she is free or in a basic shot. Normally it faces the way she moves, at the
        /// movement turn time. Around a shot that had to turn the body, <see cref="ShotFacing"/> holds it toward the
        /// live aim, then turns it back to the run, both at the shot turn rate, so she backpedals while she keeps
        /// shooting instead of flipping round and back on every shot.
        /// </summary>
        void UpdateFacing(FeelValues feel)
        {
            // Her run direction is the way she moves this frame: the held direction.
            bool moving = IsMoving;
            if (moving) _runDirection = _moveInput;
            Vector3 aim = Flat(AimEnd(_shotCursor) - transform.position);
            bool aimInsideRunFront = moving && !UpperBodyAim.BodyMustTurn(
                new GroundPoint(_runDirection.x, _runDirection.z), new GroundPoint(aim.x, aim.z), feel.ShotFrontAngle);
            bool facingRun = Vector3.Angle(Flat(transform.forward), _runDirection) < 1f;
            switch (_shotFacing.Update(Time.time, moving, aimInsideRunFront, facingRun))
            {
                case ShotFacing.Mode.Held:
                    TurnAtShotRate(aim, feel);
                    break;
                case ShotFacing.Mode.TurningBack:
                    TurnAtShotRate(_runDirection, feel);
                    break;
                default:
                    FaceMovement(feel);
                    break;
            }
        }

        /// <summary>Turns the body toward a direction at the shot turn rate (<see cref="FeelValues.ShotTurnTime"/> for a half turn).</summary>
        void TurnAtShotRate(Vector3 flatDir, FeelValues feel)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            float yaw = transform.eulerAngles.y;
            float want = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, ShotFacing.Turn(yaw, want, feel.ShotTurnTime, Time.deltaTime), 0f);
        }

        /// <summary>
        /// True when the buffer holds a press that may start now and will: a dash at once, a skill once no free
        /// recovery holds it back, and either only while it is still good. A press is good while it is no older than
        /// the buffer time; a skill press that waited out a free recovery is good if it was made no earlier than the
        /// buffer time before the skill let her go, however long ago that was (<see cref="SkillGate"/>, decision 0004
        /// amended 2026-11-19). A press that is due but not good is dropped here, so it never cancels or holds back
        /// anything (a shot winding up, a waiting click, the move keys).
        /// </summary>
        bool PressReady(float now)
        {
            if (!_buffer.TryPeek(out Press waiting)) return false;
            if (waiting.Kind == PressKind.Skill && now < _skillGateUntil) return false; // waits out a free recovery
            FeelValues feel = Feel;
            // A gate is set only by a free recovery, and a new skill, a dash or a teleport clears it.
            bool afterFreeRecovery = waiting.Kind == PressKind.Skill && feel.SkillRecoveryFree
                                     && _skillGateUntil != float.NegativeInfinity;
            if (SkillGate.PressStillGood(waiting.At, now, feel.InputBuffer, afterFreeRecovery, _gateLetGoAt)) return true;
            _buffer.Clear(); // not good: dropped
            Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = waiting.Kind == PressKind.Skill ? waiting.Slot : -1 });
            return false;
        }

        /// <summary>
        /// One frame of a skill's slide, at the share of run speed the slide allows now (ApplyMovementFeel has set it),
        /// on the NavMesh. At a stand the slide is over. The way is the held move direction, changed at once as free
        /// movement does; with no move key held, the way she last slid (decision 0004, amended 2026-11-19: Kristian
        /// ruled that the move keys steer the slide). Steering never changes the speed: an opposite key turns the slide
        /// round at the speed it has. With <see cref="FeelValues.SlideSteering"/> off, the way she was moving at the press.
        /// </summary>
        void Slide(FeelValues feel, float now)
        {
            if (SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, feel.SkillSlowdown) <= 0f)
            {
                _sliding = false;
                Halt();
                return;
            }
            if (feel.SlideSteering && _moveInput != Vector3.zero) _slideDir = _moveInput;
            MoveAlong(_slideDir, _agent.speed);
        }

        /// <summary>
        /// A skill lets her go (at its last hit with the free recovery, else at the end of its recovery): the speed she
        /// may move at eases back up from what the slide has left. A skill that moved her itself leaves her free at once.
        /// </summary>
        void LetGo(SkillDefinition skill, float now)
        {
            // A light skill never held her back, so there is nothing to ease back up from (decision 0005).
            if (skill != null && skill.Lock == SkillLock.Light)
            {
                _sliding = false;
                return;
            }
            float share = _sliding ? SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, Feel.SkillSlowdown) : 0f;
            _sliding = false;
            _rising = skill == null || !_castMoves;
            _riseFrom = share;
            _riseStartedAt = now;
        }

        void ApplyMovementFeel(FeelValues feel)
        {
            if (Time.timeScale > 0f) _lastVelocity = _agent.velocity;
            if (_rising && SkillSlide.IsRisen(_riseFrom, Time.time - _riseStartedAt, feel.SkillSpeedRecovery)) _rising = false;
            // A shot on the move that was cancelled before it went off ends the slow at once.
            if (_easing && _easeFiredAfter < 0f && !_movingShot) _easing = false;
            if (_easing && MovingShot.IsOver(Time.time - _easeStartedAt, _easeFiredAfter, feel.MovingShotSlowdown, feel.MovingShotRecovery))
                _easing = false;
            // The backpedal cap: held turned away from her movement by a shot, she moves at no more than a share of
            // run speed, eased in and out (decision 0003, a guess).
            _backpedalCap.Update(_shotFacing.State == ShotFacing.Mode.Held && IsMoving, Time.time, feel.BackpedalSpeedShare,
                                 feel.MovingShotSlowdown, feel.MovingShotRecovery);
            _agent.speed = feel.RunSpeed * CurrentSpeedShare(feel);
            // The agent does not slow down by itself when its speed limit drops below how fast it is going.
            if (_agent.velocity.sqrMagnitude > _agent.speed * _agent.speed && !_dashing)
                _agent.velocity = _agent.velocity.normalized * _agent.speed;
            _agent.acceleration = feel.TimeToFullSpeed > 0f ? feel.RunSpeed / feel.TimeToFullSpeed : 10000f;
        }

        // ---------------- Movement ----------------

        /// <summary>True when an aim direction lies outside the front angle, so a basic shot along it turns the whole body.</summary>
        bool BodyMustTurnToward(Vector3 direction, FeelValues feel)
        {
            Vector3 facing = Flat(transform.forward);
            return UpperBodyAim.BodyMustTurn(new GroundPoint(facing.x, facing.z), new GroundPoint(direction.x, direction.z), feel.ShotFrontAngle);
        }

        /// <summary>
        /// Where a basic shot aimed at the cursor point ends: the full range out, along the line toward the point,
        /// or along where she faces when the point is on her.
        /// </summary>
        Vector3 AimEnd(Vector3 cursor)
        {
            Vector3 facing = Flat(transform.forward);
            GroundPoint dir = LineShot.Direction(GroundPosition, new GroundPoint(cursor.x, cursor.z), new GroundPoint(facing.x, facing.z));
            return transform.position + new Vector3(dir.X, 0f, dir.Z) * Feel.BasicShotRange;
        }

        void Halt()
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        // ---------------- Dash ----------------

        bool StartDash(Vector3 point)
        {
            FeelValues feel = Feel;
            if (!_dash.TryStart(Time.time, feel)) return false;

            Vector3 dir = Flat(point - transform.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Flat(transform.forward);
            dir.Normalize();

            Halt();
            // A dash ends a skill's slide and the ease back up after one at once, and a free recovery with them:
            // a dash cancels a skill, recovery and all.
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            EndChain(); // and a recast chain (decision 0005)
            _dashFrom = transform.position;
            _dashTo = _dashFrom + dir * feel.DashDistance;
            _dashing = true;
            DashesStarted++;
            Face(dir);
            Report(new PlayerReport { Kind = PlayerReportKind.DashStarted });
            return true;
        }

        // ---------------- Shots ----------------

        IntentResult BufferPress(PressKind kind, Vector3 point, int slot = -1)
        {
            _buffer.Store(new Press { Kind = kind, Point = point, Slot = slot, At = Time.time }, Time.time);
            return IntentResult.Buffered;
        }

        /// <summary>
        /// Starts a basic shot toward the cursor. Standing, the character stops; on the move it walks on. Either way
        /// the whole body turns to the aim only if it lies outside the front angle; within it, the upper body aims.
        /// </summary>
        void StartBasicShot(FeelValues feel, bool onTheMove)
        {
            if (!onTheMove) Halt();
            _aimPoint = AimEnd(_shotCursor);
            _shotTurnsBody = BodyMustTurnToward(Flat(_aimPoint - transform.position), feel);
            // A shot that must turn the body starts the hold: she turns to the aim at the shot turn rate, and the
            // upper body and arms cover what the body has not turned yet. A shot while held renews it.
            _shotFacing.ShotStarted(_shotTurnsBody, Time.time, feel.ShotTurnHold);
            _actionKind = ActionKind.BasicShot;
            _movingShot = onTheMove;
            if (onTheMove)
            {
                ShotsOnTheMove++;
                StartEasing(feel);
            }
            ActionsStarted++;
            ActionLeadTime = feel.BasicShotStartup;
            _shotStartedAt = Time.time;
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
            Report(new PlayerReport { Kind = PlayerReportKind.ShotStarted, Moving = onTheMove });
        }

        /// <summary>
        /// Starts a skill. A recast (decision 0005) starts the next shot of the chain: no cooldown, and the chain goes on;
        /// any other start ends a chain that runs and begins this skill's own.
        /// </summary>
        void StartSkill(int slot, Vector3 point, bool recast = false)
        {
            SkillDefinition skill = Skills[slot];
            bool light = skill.Lock == SkillLock.Light;
            // A skill moves her over its startup, but the shot after an opening move does not: Backstep hops on its
            // first press only (amended 2026-11-19).
            bool moves = skill.MoveDistance != 0f && !(recast && skill.OpeningMove);
            FeelValues feel = Feel;
            DropWaitingClick(); // a skill drops a waiting click
            _action.Cancel(); // a skill may start during a basic shot, and ends it
            _movingShot = false;
            _shotTurnsBody = false;
            _easing = false; // a skill ends the slow of a shot on the move at once
            _skillGateUntil = float.NegativeInfinity; // the recovery it waited for is over
            _aimPoint = point;
            if (light)
            {
                // A light skill (decision 0005) turns the body as a basic shot does: only for an aim outside the front
                // angle, held turned while she backpedals; within it the upper body aims. No stop, no slide.
                _sliding = false;
                _slideFrom = 0f;
                _shotFacing.ShotStarted(BodyMustTurnToward(Flat(point - transform.position), feel), Time.time, feel.ShotTurnHold);
            }
            else
            {
                _shotFacing.End(turnBack: false); // a committed skill ends the backpedal at once: it faces the cursor
                // The slide (decision 0004): from the speed she actually has, along the way she actually moves. During a
                // hit stop the agent reads standing, so the last frame in which time ran is used.
                Vector3 velocity = Flat(Time.timeScale > 0f ? _agent.velocity : _lastVelocity);
                float share = feel.RunSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / feel.RunSpeed) : 0f;
                _rising = false;
                _sliding = !moves && feel.SkillSlowdown > 0f && share > 0.01f;
                if (_sliding)
                {
                    _slideFrom = share;
                    _slideStartedAt = Time.time;
                    _slideDir = velocity / velocity.magnitude;
                }
                else
                {
                    _slideFrom = 0f;
                    Halt();
                }
            }
            _castSlot = slot;
            _hasCursor = false; // follow only a cursor that is reported after this skill started, never a stale one
            _castMoved = 0f;
            Vector3 toAim = Flat(point - transform.position);
            _castMoveDir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Flat(transform.forward).normalized;
            if (feel.FaceCursorOnCast && !light) Face(Flat(point - transform.position));

            _actionKind = ActionKind.Skill;
            ActionsStarted++;
            ActionLeadTime = skill.Input == SkillInput.Charge ? 0f : skill.Startup;
            _cast.Start(skill, Time.time);
            _markedAt.Clear();
            _castClean = false;
            _castMoves = moves;
            if (!recast)
            {
                EndChain(); // another skill starting ends a chain (decision 0005)
                _chain.Start(slot, skill.RecastCount, skill.RecastWindow, skill.OpeningMove);
                CooldownFor(slot).Start(Time.time, skill.Cooldown);
            }
            _castOpening = !recast && _chain.IsOpeningMove;
            Report(new PlayerReport { Kind = PlayerReportKind.SkillStarted, Slot = slot });
        }

        void EndAction()
        {
            _actionKind = ActionKind.None;
            _castSlot = -1;
            _movingShot = false;
            _shotTurnsBody = false;
        }

        Cooldown CooldownFor(int slot)
        {
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            return _cooldowns[slot];
        }

        /// <summary>
        /// The basic shot goes off along its aim: it hits the first living target its line touches
        /// (<see cref="LineShot.FirstHit"/>), or misses. Either way it fires: the streak runs to the target it hit,
        /// or out to the full range. True if it dealt damage.
        /// </summary>
        bool ResolveBasicShot()
        {
            BasicShotsFired++;
            FeelValues feel = Feel;
            Look look = LookFor(null, 0f);
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            PlaySound(look); // on a hit or a miss alike

            GroundPoint origin = GroundPosition;
            var aim = new GroundPoint(_aimPoint.x, _aimPoint.z);
            IReadOnlyList<Damageable> all = Damageable.All;
            _targetScratch.Clear();
            for (int i = 0; i < all.Count; i++) _targetScratch.Add(all[i].Info);
            int index = LineShot.FirstHit(origin, aim, feel.BasicShotRange, feel.BasicShotHalfWidth, _targetScratch);
            Damageable hit = index >= 0 ? all[index] : null;

            Vector3 endPoint = hit != null ? hit.transform.position : _aimPoint;
            Vector3 end = new Vector3(endPoint.x, muzzle.y, endPoint.z);
            SpawnFlash(look, muzzle, end - muzzle);
            SpawnStreak(look, muzzle, end, 1f);
            if (hit == null) return ShotFired(false);
            bool empowered = IsEmpowered;
            int applied = Deal(hit, Placeholders.BasicShotDamage, out float focus);
            if (applied <= 0) return ShotFired(false);
            ReportHit(-1, hit, applied, empowered, focus);
            SpawnHit(look, hit, muzzle);
            BasicShotHits++;
            // Every few hits, whatever the target, the hit that completes the count marks the target it hit; in the
            // focus window every hit does (decision 0005).
            if (_basicMarks.Hit(Kit.BasicShotsPerMark, empowered)) AddMarks(hit, 1, -1);
            return ShotFired(true);
        }

        /// <summary>Puts marks on a target, shows them over it and reports it. <paramref name="slot"/> is -1 for the basic shot.</summary>
        void AddMarks(Damageable target, int marks, int slot)
        {
            if (marks <= 0 || target == null || !target.IsAlive) return;
            KitValues kit = Kit;
            int after = target.AddMarks(marks, kit.MaxMarks, kit.MarkLifetime);
            MarkPips pips = MarkPips.For(target, ShotFxMaterial);
            if (pips != null) pips.Max = kit.MaxMarks;
            Report(new PlayerReport { Kind = PlayerReportKind.MarkAdded, Slot = slot, Target = target, Marks = marks, MarksAfter = after });
        }

        /// <summary>
        /// The marks a builder's hit adds to a target: the skill's, or for a clean tap the clean tap's (a clean spend
        /// takes the marks and never comes here). A skill with a
        /// mark interval marks a target at most that often in one use (Fan Fire: once a second, not every tick).
        /// </summary>
        void MarkFromSkill(SkillDefinition skill, Damageable target)
        {
            int marks = _castClean ? skill.CleanTapMarks : skill.MarksAdded;
            if (marks <= 0) return;
            if (skill.MarkInterval > 0f)
            {
                float now = Time.time;
                if (_markedAt.TryGetValue(target, out float at) && now - at < skill.MarkInterval - 1e-4f) return;
                _markedAt[target] = now;
            }
            AddMarks(target, marks, _castSlot);
        }

        /// <summary>Reports a basic shot that went off, and whether it hit; returns that.</summary>
        bool ShotFired(bool landed)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.ShotFired, Landed = landed });
            return landed;
        }

        /// <summary>Reports damage she dealt: by a skill's slot, or -1 for the basic shot.</summary>
        void ReportHit(int slot, Damageable target, int damage, bool empowered, float focus) =>
            Report(new PlayerReport { Kind = PlayerReportKind.Hit, Slot = slot, Target = target, Damage = damage, Empowered = empowered, Focus = focus });

        /// <summary>
        /// Deals damage to a target: times the empowered multiplier while empowered (1 since decision 0005), and what
        /// was dealt trickles into the focus gauge. Returns the amount actually removed, and the focus gained.
        /// <paramref name="stagger"/> is what the hit takes off a stagger bar (work order 008); the basic shot's is 0.
        /// </summary>
        int Deal(Damageable target, int damage, out float focus, float stagger = 0f)
        {
            IdentityValues identity = Identity;
            int applied = target.TakeHit(_focus.Scale(damage, Time.time, identity), stagger);
            focus = _focus.AddDamage(applied, identity, Time.time);
            return applied;
        }

        /// <summary>One hit of a skill: every living target its area touches takes the damage. True if any did.</summary>
        bool ResolveSkillHit(SkillDefinition skill, float charge)
        {
            SkillShotsFired++;
            GroundPoint origin = GroundPosition;
            Vector3 flatAim = Flat(_aimPoint - transform.position);
            // With nothing to aim at (the cursor on the character), a line or cone goes where the character faces.
            if (flatAim.sqrMagnitude < 1e-6f && skill.Shape != SkillShape.Circle) flatAim = Flat(transform.forward);
            var aim = new GroundPoint(origin.X + flatAim.x, origin.Z + flatAim.z);

            Look look = LookFor(skill, charge);
            Vector3 shotPoint = SpawnSkillFx(skill, look, origin, aim);
            PlaySound(look); // once per hit of a burst, per tick of a hold, per landing

            int damage = SkillRules.Damage(skill, charge);
            // A clean tap (decision 0005) hits harder by itself; a clean spend (Backstep, amended 2026-11-19) instead
            // takes the marks, as a spender does, for its own bonus per mark.
            if (MarkRules.IsCleanTap(skill, _castClean)) damage = Mathf.RoundToInt(damage * Kit.CleanDamage);
            bool spender = MarkRules.Spends(skill, _castClean);
            float bonusPerMark = MarkRules.BonusPerMark(skill, _castClean);
            bool landed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!SkillRules.Hits(skill, origin, aim, d.GroundPosition, d.Radius)) continue;
                if (!d.IsAlive) continue;
                bool empowered = IsEmpowered;
                // A spender takes every mark off the target and deals its bonus per mark on this hit (decision 0005).
                int taken = spender ? d.TakeMarks() : 0;
                int dealt = spender ? MarkRules.SpenderDamage(damage, taken, bonusPerMark) : damage;
                int applied = Deal(d, dealt, out float focus, skill.Stagger);
                if (spender)
                {
                    float markFocus = _focus.Add(taken * Identity.FocusPerMarkSpent, Identity, Time.time);
                    Report(new PlayerReport
                    {
                        Kind = PlayerReportKind.MarksSpent, Slot = _castSlot, Target = d, Marks = taken, Damage = applied, Focus = markFocus,
                    });
                }
                if (applied <= 0) continue;
                ReportHit(_castSlot, d, applied, empowered, focus);
                SpawnHit(look, d, shotPoint);
                SkillShotHits++;
                landed = true;
                if (!spender) MarkFromSkill(skill, d);
            }
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = landed, Charge = charge });
            return landed;
        }

        // ---------------- Effects: flat, bright shapes that show a shot, and a disc for an area ----------------
        // They are pictures only. The hit has landed when they appear, and nothing they do changes it.
        // How each shot looks is in the ShotFx settings asset, read at the moment of the shot.

        /// <summary>A look as it is used for one shot: scaled by the charge and by the empowered mode, with its glow.</summary>
        struct Look
        {
            public ShotLook Values;
            public float Scale;     // every size
            public float Width;     // streak width and flash size on top of Scale: the charge
            public ShotPaint Paint;
        }

        /// <summary>The look's sound, once, in this frame: its volume times the master, its pitch spread at random.</summary>
        void PlaySound(Look look)
        {
            ShotLook values = look.Values;
            if (Sounds == null || values == null || values.Sound == null) return;
            float master = ShotLooks != null ? ShotLooks.SoundVolume : 1f;
            float spread = Mathf.Max(0f, values.SoundPitchSpread);
            float pitch = values.SoundPitch * (1f + Random.Range(-spread, spread));
            if (IsEmpowered && ShotLooks != null) pitch *= ShotLooks.EmpoweredPitch;
            Sounds.Play(values.Sound, values.SoundVolume * master, pitch, values.SoundStart);
        }

        Look LookFor(SkillDefinition skill, float charge)
        {
            ShotLook values = skill == null
                ? (ShotLooks != null && ShotLooks.Basic != null ? ShotLooks.Basic : _fallbackBasicLook)
                : (ShotLooks != null ? ShotLooks.For(skill.Name) : null) ?? FallbackLook(skill.Name);
            bool empowered = IsEmpowered;
            float empoweredScale = ShotLooks != null ? ShotLooks.EmpoweredScale : ShotFxSettings.StartEmpoweredScale;
            Color rim = empowered ? (ShotLooks != null ? ShotLooks.EmpoweredRim : ShotFxSettings.StartEmpoweredRim) : values.Rim;
            return new Look
            {
                Values = values,
                Scale = empowered ? empoweredScale : 1f,
                Width = skill != null && skill.Input == SkillInput.Charge ? Mathf.Lerp(1f, values.FullChargeScale, charge) : 1f,
                Paint = new ShotPaint(rim, values.Core,
                                      ShotLooks != null ? ShotLooks.RimGlow : ShotFxSettings.StartRimGlow,
                                      ShotLooks != null ? ShotLooks.CoreGlow : ShotFxSettings.StartCoreGlow),
            };
        }

        ShotLook FallbackLook(string skillName)
        {
            if (!_fallbackLooks.TryGetValue(skillName, out ShotLook look))
                _fallbackLooks[skillName] = look = ShotFxDefaults.For(skillName);
            return look;
        }

        /// <summary>How many streaks go side by side for a line of this width: one, or one per spacing across it.</summary>
        public static int StreaksAcross(float width) => Mathf.Max(1, Mathf.FloorToInt(width / LineStreakSpacing + 1e-4f));

        /// <summary>
        /// The barrel end the next shot fires from, taking turns between the hands. Without pistols, a point
        /// over the character's ground point at the height the old beams were drawn at.
        /// </summary>
        Vector3 NextMuzzle(out bool leftHand)
        {
            Transform muzzle = _leftHandNext ? LeftMuzzle : RightMuzzle;
            if (muzzle == null) muzzle = _leftHandNext ? RightMuzzle : LeftMuzzle;
            leftHand = muzzle != null && muzzle == LeftMuzzle;
            _leftHandNext = !_leftHandNext;
            if (muzzle != null) return muzzle.position;
            Vector3 p = transform.position;
            return new Vector3(p.x, FallbackMuzzleHeight, p.z);
        }

        /// <summary>The effects of one hit of a skill. Returns the height and place the hit sparks go from.</summary>
        Vector3 SpawnSkillFx(SkillDefinition skill, Look look, GroundPoint origin, GroundPoint aim)
        {
            Vector3 dir = new Vector3(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

            if (skill.Shape == SkillShape.Circle)
            {
                // The area hit is a thin ring at the circle's edge. The bang is a big burst over the middle,
                // drawn in front of whatever stands in it, and a shockwave that runs out to the edge.
                GroundPoint c = SkillRules.CircleCenter(skill, origin, aim);
                var centre = new Vector3(c.X, 0f, c.Z);
                float blast = look.Values.BlastSeconds;
                ShotFx.Outline(centre, skill.Radius, blast * 1.6f, look.Paint, ShotFxMaterial);
                ShotFx.Ring(centre, skill.Radius, blast, look.Paint, ShotFxMaterial);
                ShotFx.Spark(centre + Vector3.up * 1f, skill.Radius * 1.1f * look.Scale, blast, look.Paint, ShotFxMaterial,
                             towardCamera: skill.Radius + 1f);
                return centre + Vector3.up * 1f;
            }

            // Streaks run level, at the height of the muzzle, and end where the area ends: the range is
            // measured from the character, as the hit is.
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            Vector3 level = new Vector3(origin.X, muzzle.y, origin.Z);
            SpawnFlash(look, muzzle, dir);
            if (skill.Shape == SkillShape.Cone)
            {
                int rays = Mathf.Max(1, look.Values.Rays);
                for (int i = 0; i < rays; i++)
                {
                    // Five or more spread from edge to edge; fewer, as in rapid fire, go anywhere in the cone.
                    float yaw = rays >= 5
                        ? Mathf.Lerp(-skill.Angle * 0.5f, skill.Angle * 0.5f, i / (rays - 1f))
                        : Random.Range(-skill.Angle * 0.5f, skill.Angle * 0.5f);
                    SpawnStreak(look, muzzle, level + Quaternion.Euler(0f, yaw, 0f) * dir * skill.Range, 1f);
                }
                // The blast comes out of the gun, level with the streaks, so it reads as one with them from the
                // camera. It reaches as far as the cone does.
                Vector3 apex = level + dir * Mathf.Max(0f, Vector3.Dot(muzzle - level, dir));
                // A wide flat shape glowing as hard as a thin streak would bloom into a white blob: its edge glows less.
                ShotPaint blast = look.Paint;
                blast.CoreGlow = Mathf.Lerp(1f, blast.CoreGlow, 0.4f);
                ShotFx.Wedge(apex, dir, skill.Angle, skill.Range - Vector3.Distance(apex, level), look.Values.BlastSeconds,
                             blast, ShotFxMaterial);
                return muzzle;
            }

            // A line pierces everything in its strip, so its streaks run its whole range. A wide one sends
            // several side by side, spread evenly across its width.
            float width = skill.HalfWidth * 2f;
            int count = StreaksAcross(width);
            Vector3 across = new Vector3(dir.z, 0f, -dir.x);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = across * ((i + 0.5f) / count - 0.5f) * width;
                SpawnStreak(look, muzzle + offset, level + dir * skill.Range + offset, 1f);
            }
            return muzzle;
        }

        /// <summary>Tells the animation that a pistol fired, which hand and how hard it kicks (the look's recoil, scaled like its flash).</summary>
        void Kick(bool leftHand, Look look)
        {
            KickLeftHand = leftHand;
            KickStrength = look.Values.Recoil * look.Scale * look.Width;
            KickCount++;
        }

        /// <summary>
        /// The share of run speed it may walk at now: 1, or the lowest of what limits it. Each limit eases without a
        /// jump (a shot on the move, the backpedal cap, a skill's slide, the ease back up after a skill), so the lowest
        /// of them does too, whichever follows which.
        /// </summary>
        float CurrentSpeedShare(FeelValues feel)
        {
            float now = Time.time;
            float share = Mathf.Min(_backpedalCap.Share,
                                    _easing
                                        ? MovingShot.SpeedShare(_easeFrom, now - _easeStartedAt, _easeFiredAfter, feel.MovingShotSpeedShare,
                                                                feel.MovingShotSlowdown, feel.MovingShotRecovery)
                                        : 1f);
            if (_rising) share = Mathf.Min(share, SkillSlide.RiseShare(_riseFrom, now - _riseStartedAt, feel.SkillSpeedRecovery));
            if (IsSkillSliding) share = Mathf.Min(share, SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, feel.SkillSlowdown));
            return share;
        }

        /// <summary>Starts the ease around a shot on the move: down from the speed she has now (full, or wherever a chained shot left it).</summary>
        void StartEasing(FeelValues feel) => StartEasing(feel, CurrentSpeedShare(feel));

        /// <summary>Starts the ease around a shot on the move from a given share of run speed.</summary>
        void StartEasing(FeelValues feel, float fromShare)
        {
            _easeFrom = fromShare;
            _easeStartedAt = Time.time;
            _easeFiredAfter = _actionKind == ActionKind.BasicShot && _action.HitResolved ? 0f : -1f;
            _easing = true;
        }

        /// <summary>
        /// The share of run speed she actually moves at, 0 to 1. During a hit stop the agent reads standing, so the last
        /// frame in which time ran is used, as for a skill's slide.
        /// </summary>
        float ActualSpeedShare(FeelValues feel)
        {
            Vector3 velocity = Flat(Time.timeScale > 0f ? _agent.velocity : _lastVelocity);
            return feel.RunSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / feel.RunSpeed) : 0f;
        }

        void SpawnFlash(Look look, Vector3 muzzle, Vector3 direction)
        {
            ShotFx.Flash(muzzle, Flat(direction).sqrMagnitude > 1e-8f ? Flat(direction).normalized : Flat(transform.forward).normalized,
                         look.Values.FlashSize * look.Scale * look.Width, look.Values.FlashSeconds, look.Paint, ShotFxMaterial);
        }

        void SpawnStreak(Look look, Vector3 from, Vector3 to, float widthScale)
        {
            ShotFx.Streak(from, to, look.Values.StreakWidth * look.Scale * look.Width * widthScale, look.Values.StreakSeconds,
                          look.Paint, ShotFxMaterial);
        }

        /// <summary>A spark on the side of a hit target that faces where the shot came from, at the shot's height.</summary>
        void SpawnHit(Look look, Damageable target, Vector3 from)
        {
            Vector3 centre = new Vector3(target.transform.position.x, from.y, target.transform.position.z);
            Vector3 back = Flat(from - centre);
            Vector3 at = back.sqrMagnitude > 1e-6f ? centre + back.normalized * Mathf.Min(target.Radius, back.magnitude) : centre;
            ShotFx.Spark(at, look.Values.HitSize * look.Scale, look.Values.HitSeconds, look.Paint, ShotFxMaterial, target.Radius + 0.2f);
        }

        // ---------------- Facing ----------------

        void FaceMovement(FeelValues feel)
        {
            Vector3 v = IsMoving ? _moveInput : Flat(_agent.velocity);
            if (v.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(v.normalized, Vector3.up);
            transform.rotation = feel.TurnTime <= 0f
                ? want
                : Quaternion.RotateTowards(transform.rotation, want, 180f / feel.TurnTime * Time.deltaTime);
        }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
