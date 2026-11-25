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
    /// <see cref="ReleaseSkill"/>, <see cref="AimAt"/>, <see cref="ActivateIdentity"/>, <see cref="HoldCover"/> and <see cref="Stop"/>.
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
    ///   clean part (<see cref="CleanPart"/>: early in it since work order 040, before that its last share), never a press
    ///   that waited in the buffer. A dash or another skill ends the chain; the cooldown runs from the first press.
    /// - Backstep (amended 2026-11-19 after Kristian played: a second, harder way to spend marks) is a chain of two that
    ///   opens with a move (<see cref="SkillDefinition.OpeningMove"/>): the first press is the hop and fires nothing;
    ///   when it ends she is free and the window opens; the second press fires the shot at the cursor. A clean second
    ///   press (<see cref="SkillDefinition.CleanMarkDamageBonus"/>) spends the marks of each target it hits, as a
    ///   spender does; an earlier one is an ordinary shot that adds its mark. The shot itself does not move her.
    /// - Focus fills from spent marks, clean taps and a trickle of damage; spending it cuts every running cooldown and,
    ///   for its window, makes every basic shot hit mark.
    /// - The focus choice (decision 0005, his words of 2026-11-19; work order 025, 2026-11-20): how the gauge works is
    ///   <see cref="FocusMode"/>, the run's choice (<see cref="RunFocus"/>) or in the practice room its own
    ///   (<see cref="PracticeFocusMode"/>); today's until one is picked. Hair Trigger: the gauge in parts, Z spends one,
    ///   cuts every running cooldown by a share and primes the next skill started in a few seconds (<see cref="FocusPrime"/>,
    ///   <see cref="FocusModeRules.UseFor"/>): a clean last press anywhere in the window, a perfect release from half a
    ///   charge, or a mark more; no window. Steady Hand: a full gauge, a longer window, and in it <see cref="Skills"/> reads
    ///   Triple Tap two presses longer and Scatter Blast as two blasts (<see cref="SteadyHandView"/>), copies as the
    ///   upgrades are. Nothing is written to the settings.
    /// - Skill upgrades (work order 017, 2026-11-20): in a run, <see cref="Skills"/> are the run's own copies of the
    ///   skills, patched by the upgrades taken from elite rooms (<see cref="SkillUpgrades"/>); the settings' skills
    ///   (<see cref="SettingsSkills"/>) are never written. What an upgrade changes beyond numbers is a field of the skill
    ///   read in one hook each: a basic shot that hits (<see cref="SkillDefinition.BasicHitCooldownCut"/>,
    ///   <see cref="SkillDefinition.SlideShotMarks"/>), a use that hits, kills, dazes or counters
    ///   (<see cref="SkillDefinition.HitCooldownCut"/>, <see cref="SkillDefinition.KillCooldownCut"/>,
    ///   <see cref="SkillDefinition.DazeCooldownCut"/>, <see cref="SkillDefinition.CounterReadies"/>), a tagged target that
    ///   dies (<see cref="SkillDefinition.TagJump"/>), a spender's distance (<see cref="SkillDefinition.CloseRange"/>), a
    ///   dazed target (<see cref="SkillDefinition.DazedDamageScale"/>) and a charge with none (<see cref="SkillDefinition.NoCharge"/>).
    ///
    /// Cover (Kristian, 2026-11-19: "Skudd skal stoppes av vegg og søyler", shots are to be stopped by walls and
    /// pillars): a generated level's walls and tall props (<see cref="Cover"/>) stop her shots and skills
    /// (<see cref="CoverRules"/>). A line shot, basic or skill, ends at the first cover on its centre line, its streak
    /// too, with a hit spark there; a cone hits only what it sees from her; a circle lands short of cover in its way and
    /// hits only what it sees from its centre. What cover saves takes no damage, stagger, marks or focus.
    /// <see cref="FeelValues.CoverStopsShots"/> off, nothing stops them.
    ///
    /// Taking cover (Kristian, 2026-11-19, an experiment: "med control-knappen, lene seg inntil vegger/props med litt
    /// høyde for å dukke unna skudd som kommer på avstand", with the control key, lean against walls and props to duck
    /// shots from afar; something she shoots from in bursts, not a place to sit). <see cref="HoldCover"/> is held:
    /// - While it is held and she is free (not dashing, not in a skill) and within <see cref="FeelValues.CoverReach"/> of a
    ///   wall or a prop that blocks walking (<see cref="Shelter"/>), she moves to stand against it on her side over
    ///   <see cref="FeelValues.CoverSnapTime"/> (<see cref="TakeCoverRules.StandPlace"/>) and ducks. Nothing in reach,
    ///   nothing happens.
    /// - Ducked, the thing she is against covers her against the shooter's sight and shot when it lies between them, a
    ///   low prop too (<see cref="ShieldsFrom"/>, <see cref="TakeCoverRules.Shields"/>). Strikes on the ground ignore it.
    /// - The move keys slide her along it at <see cref="FeelValues.CoverMoveShare"/> of run speed, staying against it; a
    ///   key more than <see cref="FeelValues.CoverSlideAngle"/> off its face does nothing (<see cref="TakeCoverRules.Slide"/>).
    ///   In cover she is never <see cref="IsMoving"/>: a shot from cover is a standing shot.
    /// - A basic shot or a skill stands her up, where she is, for as long as it lasts and
    ///   <see cref="FeelValues.CoverPeekTime"/> after; then she ducks again. Standing, a low prop does not cover her.
    /// - The key going up stands her up and gives the move keys back at once, at full speed. The dash, and a skill that
    ///   moves her itself (Backstep's hop), leave cover at once. With the key still held she takes cover again when she
    ///   is free and something is in reach.
    /// <see cref="FeelValues.TakeCover"/> off, the key does nothing.
    ///
    /// The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043): with
    /// <see cref="FeelValues.QuickDuck"/> on, all of taking cover above is off and the key going down is a tap
    /// (<see cref="TapCover"/>, the rules in <see cref="QuickDuck"/>): beside a low prop she drops where she stands for a
    /// moment and rises by herself; nothing moves her. While she is down she cannot shoot or use a skill (a click waits,
    /// a skill press waits as at the end of a lock), a move key not held at the tap or the dash ends it at once, and the
    /// prop she ducked at stops arrows loosed from its far side (<see cref="ShieldsFrom(Vector3, ProjectileKind)"/>). The
    /// nearest low prop a tap would work at shows a rim light (<see cref="QuickDuckRim"/>); a stop shows a spark, a sound
    /// and the word DUCKED. With it on, the altar, the grave marker and the broken pillar stop no shot (<see cref="PropSort"/>).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class PlayerCharacter : MonoBehaviour
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
        [Header("Melee you read (work order 035)")]
        [Tooltip("On: the swarmer, the brute and the guard show no shapes on the floor and fight with their ordinary attacks and skills, " +
                 "read on their bodies (decision 0010). Off: they fight exactly as before work order 035, floor shapes and all. Read live.")]
        public bool MeleeYouRead = true;

        enum PressKind { Dash, Skill }

        struct Press
        {
            public PressKind Kind;
            public Vector3 Point;
            public int Slot; // which skill, for a skill press
            public float At; // when it was pressed
            public bool ForCooldown; // work order 064: a first press of Backstep just before its cooldown ends, waiting for it; 066: of any skill
            public float ReadyAt;    // work order 066: when its cooldown was to end, as it stood at the press
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
        // The focus choice (work order 025).
        readonly FocusPrime _prime = new FocusPrime();
        readonly SteadyHandView _steadyView = new SteadyHandView();
        PrimeUse _castPrime;   // what the skill in use gained from Hair Trigger's priming
        bool _chainPrimed;     // the recast chain running was primed: its last press is clean anywhere in the window
        readonly ShotLook _fallbackBasicLook = ShotFxDefaults.Basic();
        readonly Dictionary<string, ShotLook> _fallbackLooks = new Dictionary<string, ShotLook>();
        readonly Dictionary<string, ShotLook> _borrowedLooks = new Dictionary<string, ShotLook>(); // work order 024: switched off
        // The kit (decision 0005).
        readonly KitValues _fallbackKit = new KitValues();
        readonly BasicShotMarks _basicMarks = new BasicShotMarks();
        readonly RecastChain _chain = new RecastChain();
        // The pool (work order 015): the skills she has and the keys they sit on, made from the skill list when first asked.
        readonly SkillLoadout _loadout = new SkillLoadout();
        bool _loadoutReady;
        bool _loadoutAll;
        bool _castPerfect; // the Charge skill in use was let go inside its perfect band
        // Slide (work order 015): a skill that moves her along the move keys, beside whatever else she does.
        bool _keyMoving;
        int _keyMoveSlot = -1;
        Vector3 _keyMoveFrom, _keyMoveTo;
        float _keyMoveStartedAt, _keyMoveSeconds;
        // Powder Keg's kegs that lie on their fuse (work order 015).
        struct LyingKeg
        {
            public SkillDefinition Skill;
            public int Slot;
            public GroundPoint Centre;
            public float BurstAt;
            public ShotFx Circle;
            public PrimeUse Prime; // thrown primed (work order 025): its burst adds a mark more
        }
        readonly List<LyingKeg> _kegs = new List<LyingKeg>();
        /// <summary>How near a shot's line must pass a lying keg's middle to set it off, in m (work order 015, a guess).</summary>
        public const float KegRadius = 0.5f;
        readonly Dictionary<Damageable, float> _markedAt = new Dictionary<Damageable, float>(); // by the skill in use, for its mark interval
        bool _castClean; // the shot of the skill in use came from a clean press: a clean tap or a clean spend
        bool _castMoves;   // the skill in use moves her over its startup (MoveDistance): not the shot after an opening move
        bool _castOpening; // the skill in use is a chain's opening move: it fires nothing (Backstep's hop)
        bool _useKillCut, _useDazeCut; // the use going on has had its cut for a kill, for a daze (work order 033)

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

        // Taking cover (Kristian 2026-11-19, an experiment).
        bool _coverHeld;        // the key is held
        bool _inCover;
        Shelter _shelter;       // what she is in cover against
        CoverShape _coverShape; // its footprint when she took cover there
        bool _coverTall;
        bool _snapping;         // moving to stand against it
        Vector3 _snapFrom, _snapTo;
        float _snapStartedAt;
        float _peekUntil = float.NegativeInfinity; // stood up by a shot or a skill: ducked again from this moment
        Vector3 _coverLastPos;
        Vector3 _coverVelocity; // how fast she moved in cover over the last frame: the agent's own velocity stays 0 there
        // The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043).
        readonly QuickDuck _duck = new QuickDuck();
        readonly QuickDuckRim _rim = new QuickDuckRim();
        readonly LaserSight _laser = new LaserSight(); // work order 060: the Laser Sight's line; 102: its beams, made the first time she has the part
        Shelter _duckShelter;   // what she ducked beside
        CoverShape _duckShape;  // its footprint at the tap
        Vector3 _duckHeldAtTap; // the move keys held at the tap, until they are let go
        bool _steeredByKeys;    // work order 047: the keys steered since the duck last looked, and whether one went down
        bool _moveKeyWentDown;
        bool _peekShot;         // the basic shot going on is her first soon after a duck that stopped something
        Vector3 _duckStopAt;    // where the thing a duck stopped ended, told by what flies just before its end is told
        bool _duckStopAtKnown;
        System.Func<bool> _duckSort;

        // ---------------- State, for the readout and for tests ----------------

        public FeelValues Feel => Settings != null ? Settings.Values : _fallbackFeel;
        /// <summary>
        /// What her shots and skills test for cover (Kristian, 2026-11-19: shots are stopped by walls and pillars): the
        /// physics query of <see cref="Cover"/>, or null, so nothing stops them, when <see cref="FeelValues.CoverStopsShots"/>
        /// is off or no cover stands anywhere (the practice room).
        /// </summary>
        public CoverLine CoverQuery => Feel.CoverStopsShots && Cover.Any ? Cover.Line : null;
        public Vector3 Position => transform.position;
        public GroundPoint GroundPosition => new GroundPoint(transform.position.x, transform.position.z);
        public float Speed => _jumping ? _jumpSpeed : _inCover ? _coverVelocity.magnitude : _agent.velocity.magnitude; // work order 092: in a jump, its own
        public Vector3 Velocity => _jumping ? Flat(_jumpTo - _jumpFrom).normalized * _jumpSpeed : _inCover ? _coverVelocity : _agent.velocity;

        /// <summary>The direction the input holds now: flat, of length 1, or zero when no move key is held.</summary>
        public Vector3 MoveInput => _moveInput;
        /// <summary>
        /// True while a move key is held and she is free to move: not dashing and not in a skill, or in a light one. Never
        /// in cover, where the keys slide her along it instead, nor down in a quick duck (work order 043).
        /// </summary>
        public bool IsMoving => _moveInput != Vector3.zero && !_dashing && !_inCover && !_duck.IsDown && !_held.IsHeld && (_actionKind != ActionKind.Skill || IsInLightSkill);

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

        /// <summary>
        /// True while the character cannot start anything new: in a dash or a skill. A basic shot does not lock, and
        /// neither does a free recovery (decision 0004), which holds back only the next skill (<see cref="SkillGateRemaining"/>).
        /// </summary>
        public bool IsLocked => _dashing || _actionKind == ActionKind.Skill || _held.IsHeld || _jumping; // stunned or pulled too (work order 035); in the sprint's jump (092)
        /// <summary>True from the moment a basic shot has gone off until its recovery is over or is cut short.</summary>
        public bool IsInShotRecovery => _actionKind == ActionKind.BasicShot && _action.HitResolved;
        public bool IsInvulnerable => _dash.IsInvulnerable(Time.time);
        public float DashCooldownRemaining => _dash.CooldownRemaining(Time.time);
        public bool HasBufferedPress => _buffer.HasItem;
        /// <summary>For tests (work order 082): the skill whose press waits in the buffer now, or -1 (a dash's, or none).</summary>
        public int BufferedSkillSlot => _buffer.TryPeek(out Press waiting) && waiting.Kind == PressKind.Skill ? waiting.Slot : -1;
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
        /// <summary>Line shots, basic and skill, that cover stopped short of their range (Kristian, 2026-11-19).</summary>
        public int ShotsStoppedByCover { get; private set; }
        /// <summary>Where cover stopped the last line shot it stopped, on the ground.</summary>
        public Vector3 LastCoverStop { get; private set; }
        public int SkillShotHits { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.autoBraking = false; // she is steered a little way ahead every frame: no braking toward that point
            _agent.stoppingDistance = 0f;
            ApplyMovementFeel(Feel);
            if (!TryGetComponent(out ToonLook _)) gameObject.AddComponent<ToonLook>(); // work order 115: her model's toon look (ToonLookValues.On)
        }

        // The quick duck's rim goes with her, its object and its mesh (the third review of 2026-11-20, work order 047); the
        // Laser Sight's beams too, their object, meshes and material (work order 060; 102).
        void OnDestroy()
        {
            _rim.Dispose();
            _laser.Dispose();
        }

        // ---------------- Intent API ----------------

        /// <summary>True while she cannot start a basic shot now: a shot, a skill or a dash is going, or she is down in a quick duck (work order 043).</summary>
        bool Busy => _actionKind != ActionKind.None || _dashing || _held.IsHeld || _duck.IsDown;

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
            EndKeyMove();
            ClearKegs();
            SmokeClouds.Clear(); // work order 109: a smoke cloud stays where it was thrown, never on the next floor
            if (_inCover) LeaveCover(CoverLeaving.Other);
            // The cover key stays as it is held (the third review of 2026-11-20, work order 047): a key held through a new
            // floor is no tap on arrival.
            _peekUntil = float.NegativeInfinity;
            EndDuck(DuckEnd.Other); // a quick duck ends, with no wait left and no reward (work order 043): death, a restart, a new floor
            _duck.Clear();
            _rim.Hide();
            _held.Clear(); // a stun or a pull ends (work order 035): death, a restart, a new floor
            DropJump(); // and a jump of the sprint, with nothing kept for its landing (work order 092)
            _agent.Warp(new Vector3(point.x, 0f, point.z));
            Halt();
        }

        /// <summary>
        /// A clean slate (the review of 2026-11-20, work order 026): an empty focus gauge and no focus window, every
        /// skill's cooldown and charges and the dash's cooldown ready, and any skill in progress, recast chain, buffered
        /// press, waiting click, Slide and lying keg ended. Where she stands and the keys held stay. Called when a run
        /// starts and when a run ends back to the practice room; nothing else calls it.
        /// </summary>
        public void StartFresh()
        {
            if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.Cancelled, Time.time);
            _action.Cancel();
            _cast.Cancel();
            EndAction();
            _dashing = false;
            _buffer.Clear();
            _clicks.Clear();
            _skillGateUntil = float.NegativeInfinity;
            EndChain();
            EndKeyMove();
            ClearKegs();
            SmokeClouds.Clear(); // work order 109
            _focus.Clear();
            _prime.Clear(); // and no priming (work order 025)
            _held.Clear(); // and no stun or pull (work order 035)
            if (_jumping) // a jump of the sprint is put down where it was going, with nothing kept for its landing (work order 092)
            {
                DropJump();
                PutDownAtLanding();
            }
            _dash.Clear();
            _cooldowns.Clear(); // made again, ready, when next asked for
            // The basic shot too (the second reviews of 2026-11-20, work order 033): no hits counted toward its next mark,
            // no wait for its next shot, the first from the right hand, and no slow of a shot on the move or turn held
            // from the last one.
            _basicMarks.Reset();
            ResetShotUpgrades(); // work order 138: Hot Barrel's heat and what it counts
            _nextShotAt = float.NegativeInfinity;
            _leftHandNext = false;
            _easing = false;
            _shotFacing.End(turnBack: false);
            _backpedalCap.Reset();
            // Work order 029 (the rack between runs, only when it is switched on and a piece says so): Spare Step's dash
            // charges, and Steady Start's focus.
            RefreshDashCharges();
            if (Modifiers != null && Modifiers.Meta.StartFocusShare > 0f) _focus.Add(Identity.MaxFocus * Modifiers.Meta.StartFocusShare, Identity, Time.time);
        }

        // ---------------- Simulation ----------------

        void Update()
        {
            FeelValues feel = Feel;
            float now = Time.time;
            SprintTick(now); // work order 092: a sprint that begins or ends, before the speed is set from it
            ApplyMovementFeel(feel);
            DropCooldownPressWhenHeldBack(); // work order 066
            TickFocusGains(now); // work order 071: the cooldowns run faster while spent focus's gains run
            ExpireChain(now);
            BurstKegsOnFuse(now);
            if (_prime.TakeLapsed(now))
            {
                PrimesLapsed++;
                Report(new PlayerReport { Kind = PlayerReportKind.PrimeLapsed }); // Hair Trigger's priming, unused (work order 025)
            }
            if (UpdateHeld(now)) return; // stunned or pulled: nothing else this frame (work order 035)
            if (JumpStep(now)) return; // in the sprint's jump: she moves along it and does nothing else (work order 092)

            if (_dashing)
            {
                Vector3 target = Vector3.Lerp(_dashFrom, _dashTo, _dash.Progress(now));
                // NavMeshAgent.Move keeps the character on the NavMesh, so a dash stops at walls and edges.
                _agent.Move(Flat(target - transform.position));
                if (_dash.IsDashing(now)) return;
                _dashing = false;
            }
            if (_keyMoving) KeyMove(now);
            UpdateCover(feel, now);

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
                        _agent.speed = feel.RunSpeed * RunSpeedShare * CurrentSpeedShare(feel);
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
                    if (landed) HitStop.Trigger(FiringWeightRules.HitStop(LastShotWeight, feel.HitStop, FiringWeight.ValuesNow)); // work order 064: the heavy ones longer
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

            if (TryJump(feel, now)) return; // sprinting into a low prop with free floor beyond: over it (work order 092)
            Walk();
            UpdateFacing(feel);
        }

        void EndAction()
        {
            // In cover, a shot or a skill that ends keeps her up for the peek time (an experiment, 2026-11-19).
            if (_inCover && _actionKind != ActionKind.None) _peekUntil = Time.time + Feel.CoverPeekTime;
            _actionKind = ActionKind.None;
            _castSlot = -1;
            _movingShot = false;
            _shotTurnsBody = false;
        }
    }
}
