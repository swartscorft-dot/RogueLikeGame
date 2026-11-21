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
        public float Speed => _inCover ? _coverVelocity.magnitude : _agent.velocity.magnitude;
        public Vector3 Velocity => _inCover ? _coverVelocity : _agent.velocity;

        /// <summary>The direction the input holds now: flat, of length 1, or zero when no move key is held.</summary>
        public Vector3 MoveInput => _moveInput;
        /// <summary>
        /// True while a move key is held and she is free to move: not dashing and not in a skill, or in a light one. Never
        /// in cover, where the keys slide her along it instead, nor down in a quick duck (work order 043).
        /// </summary>
        public bool IsMoving => _moveInput != Vector3.zero && !_dashing && !_inCover && !_duck.IsDown && !_held.IsHeld && (_actionKind != ActionKind.Skill || IsInLightSkill);

        // ---------------- Taking cover (Kristian 2026-11-19, an experiment) ----------------

        /// <summary>True while the cover key is held (<see cref="HoldCover"/>), whether or not she is in cover.</summary>
        public bool IsCoverHeld => _coverHeld;
        /// <summary>True while she is in cover against a wall or a prop: moving there, ducked, or stood up by a shot or a skill.</summary>
        public bool IsInCover => _inCover;
        /// <summary>
        /// True while she is ducked in cover: there, and no shot or skill or its peek time stands her up; or down in a quick
        /// duck (work order 043).
        /// </summary>
        public bool IsDucked => (_inCover && !_snapping && !StoodUpInCover) || _duck.IsDown;
        bool StoodUpInCover => _actionKind != ActionKind.None || Time.time < _peekUntil;
        /// <summary>True when what she is in cover against is tall cover (it stops shots for anyone).</summary>
        public bool CoverIsTall => _inCover && _coverTall;
        /// <summary>What she is in cover against, or null.</summary>
        public Shelter CoverObject => _inCover ? _shelter : null;
        /// <summary>
        /// The way from what she is in cover against (or down beside in a quick duck) to her, flat and of length 1; zero
        /// out of cover.
        /// </summary>
        public Vector3 CoverOutward
        {
            get
            {
                if (!_inCover && !_duck.IsDown) return Vector3.zero;
                GroundPoint n = (_inCover ? _coverShape : _duckShape).Outward(GroundPosition);
                return new Vector3(n.X, 0f, n.Z);
            }
        }
        /// <summary>Times she took cover.</summary>
        public int CoversTaken { get; private set; }
        /// <summary>Enemy shots that what she was in cover against stopped (<see cref="NoteEnemyShotStoppedByCover"/>).</summary>
        public int EnemyShotsStoppedByCover { get; private set; }

        /// <summary>
        /// Is she covered against a shot from this point by what she is in cover against? Only in cover, only when it lies
        /// between them, and a low prop only while she is ducked (<see cref="TakeCoverRules.Shields"/>). Walls and tall
        /// props stop shots for anyone as well, through <see cref="Cover"/>; this is the part that is hers. The shot is
        /// an arrow (the shooter's sight and its shot): down in a quick duck it is covered too (work order 043).
        /// </summary>
        public bool ShieldsFrom(Vector3 shooter) => ShieldsFrom(shooter, ProjectileKind.Arrow);

        /// <summary>
        /// The same for a thing of <paramref name="kind"/> loosed from <paramref name="shooter"/>: in cover as above,
        /// whatever its kind; down in a quick duck (work order 043) only an arrow, and only with the prop she ducked at
        /// between them (<see cref="QuickDuckRules.Shields"/>): an orb passes over a low prop.
        /// </summary>
        public bool ShieldsFrom(Vector3 shooter, ProjectileKind kind)
        {
            var from = new GroundPoint(shooter.x, shooter.z);
            if (_inCover) return TakeCoverRules.Shields(_coverShape, _coverTall, IsDucked, GroundPosition, from);
            return _duck.IsDown && QuickDuckRules.Shields(_duckShape, GroundPosition, from, kind);
        }

        /// <summary>
        /// Metres from a point along a flat direction before what she is in cover against (or down beside in a quick duck);
        /// <paramref name="max"/> when not.
        /// </summary>
        public float CoverEntry(Vector3 from, Vector3 direction, float max)
        {
            if (!_inCover && !_duck.IsDown) return max;
            return (_inCover ? _coverShape : _duckShape).Entry(new GroundPoint(from.x, from.z), new GroundPoint(direction.x, direction.z), max);
        }

        /// <summary>
        /// An enemy shot was stopped by what she is in cover against: counted, for the readout and tests. Down in a quick
        /// duck (work order 043) it is the duck's: counted, and a spark, a sound and the word.
        /// </summary>
        public void NoteEnemyShotStoppedByCover()
        {
            EnemyShotsStoppedByCover++;
            if (_duck.IsDown) DuckStoppedOne();
        }

        /// <summary>
        /// Where a thing that flew ended on what she is down beside (work order 043), told just before its end is: the
        /// duck's spark goes there. Without it (the shooter's old line) the spark goes on the prop's far side.
        /// </summary>
        public void NoteCoverStopAt(Vector3 at)
        {
            if (!_duck.IsDown) return;
            _duckStopAt = at;
            _duckStopAtKnown = true;
        }

        // ---------------- The quick duck (work order 043), for the readout and tests ----------------

        /// <summary>True while the quick duck's switch is on (and taking cover's): the cover key ducks her instead of taking cover.</summary>
        public bool QuickDuckOn => Feel.TakeCover && Feel.QuickDuck;
        /// <summary>True while she is down in a quick duck.</summary>
        public bool IsQuickDucking => _duck.IsDown;
        /// <summary>What she is down beside, or null.</summary>
        public Shelter DuckObject => _duck.IsDown ? _duckShelter : null;
        /// <summary>Seconds until a tap ducks her again; 0 when it would (or while she is down).</summary>
        public float DuckWaitLeft => _duck.WaitLeft(Time.time);
        /// <summary>When the duck going on (or the last) began, when she rises by herself, and when she last rose.</summary>
        public float DuckDownAt => _duck.DownAt;
        public float DuckUpAt => _duck.UpAt;
        public float DuckRoseAt => _duck.RoseAt;
        /// <summary>What the duck going on has stopped so far.</summary>
        public int DuckStoppedNow => _duck.Stopped;
        /// <summary>Ducks taken, of them those that stopped something; taps with no low prop in reach; things ducks stopped.</summary>
        public int DucksTaken { get; private set; }
        public int DucksThatStopped { get; private set; }
        public int DuckTapsWithNothing { get; private set; }
        public int ShotsStoppedByDuck { get; private set; }
        /// <summary>Basic shots that were the timing's reward: the first soon after a duck that stopped something.</summary>
        public int PeekShots { get; private set; }
        /// <summary>The low prop the rim light is on now, or null.</summary>
        public Shelter DuckRimOn => _rim.Shown ? _rim.On : null;
        /// <summary>The rim light itself, for tests.</summary>
        public QuickDuckRim DuckRim => _rim;
        /// <summary>True while something of hers may stop a thing loosed at her: she is in cover, or down in a quick duck.</summary>
        public bool HidesBehindSomething => _inCover || _duck.IsDown;
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

        /// <summary>
        /// The skills as the game reads them, in their list order: <see cref="SettingsSkills"/>, or in a run with skill
        /// upgrades taken the run's own copies of them, patched by the upgrades (work order 017,
        /// <see cref="RunSkillUpgrades.View"/>). While Steady Hand's window is open, copies of those with the window's two
        /// changes (work order 025, <see cref="SteadyHandView"/>). Nothing is written to the settings or to the skills in memory.
        /// </summary>
        public IReadOnlyList<SkillDefinition> Skills
        {
            get
            {
                IReadOnlyList<SkillDefinition> skills = SkillUpgrades != null ? SkillUpgrades.View(SettingsSkills) : SettingsSkills;
                return IsSteadyHandWindow ? _steadyView.View(skills, FocusModeValues) : skills;
            }
        }

        /// <summary>
        /// The skills of the settings, in their list order: the settings asset's, and after them, from the start values in
        /// memory, any skill the code knows that the asset lacks by name (work order 015: an asset from before the pool
        /// still has all twelve). Nothing is written to the asset. The practice room reads these as they are.
        /// </summary>
        public IReadOnlyList<SkillDefinition> SettingsSkills
        {
            get
            {
                SkillDefinition[] own = SkillSet != null ? SkillSet.Skills : null;
                if (own == null || own.Length == 0) return _fallbackSkills;
                if (!ReferenceEquals(own, _completedFrom))
                {
                    _completedFrom = own;
                    _completed = own;
                    var missing = new List<SkillDefinition>();
                    foreach (SkillDefinition s in _fallbackSkills)
                    {
                        bool have = false;
                        foreach (SkillDefinition o in own) have |= o != null && o.Name == s.Name;
                        if (!have) missing.Add(s);
                    }
                    if (missing.Count > 0)
                    {
                        var all = new List<SkillDefinition>(own);
                        all.AddRange(missing);
                        _completed = all.ToArray();
                    }
                }
                return _completed;
            }
        }
        SkillDefinition[] _completedFrom, _completed;

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
        /// <summary>Charges of the slot's skill ready now (work order 015): 1 or 0 for a skill with one charge.</summary>
        public int SkillChargesReady(int slot) => slot >= 0 && slot < Skills.Count ? CooldownFor(slot).Charges(Time.time) : 0;
        /// <summary>Seconds until the slot's skill gets its next charge back; 0 when all are ready.</summary>
        public float SkillNextCharge(int slot) => slot >= 0 && slot < Skills.Count ? CooldownFor(slot).NextCharge(Time.time) : 0f;
        /// <summary>Seconds the Charge skill in use has charged (work order 015: the perfect band is judged on it); 0 otherwise.</summary>
        public float SkillChargedSeconds => _actionKind == ActionKind.Skill ? _cast.ChargedSeconds(Time.time) : 0f;
        /// <summary>True when the Charge skill in use, let go now, would be a perfect release (work order 015).</summary>
        public bool InPerfectBand => _actionKind == ActionKind.Skill && _cast.Phase(Time.time) == SkillPhase.Channel
                                     && (PoolRules.IsPerfect(_cast.Skill, _cast.ChargedSeconds(Time.time))
                                         || (_castPrime == PrimeUse.PerfectRelease && FocusModeRules.PrimedPerfect(_cast.Charge(Time.time), FocusModeValues)));
        /// <summary>Perfect releases so far (work order 015).</summary>
        public int PerfectReleases { get; private set; }
        /// <summary>True while Slide moves her (work order 015).</summary>
        public bool IsKeyMoving => _keyMoving;
        /// <summary>Kegs lying on their fuse now (work order 015).</summary>
        public int KegsLying => _kegs.Count;
        /// <summary>Where the latest keg lies, or lay; for tests.</summary>
        public Vector3 LastKegPoint { get; private set; }
        /// <summary>Kegs that burst, by their fuse or by a shot.</summary>
        public int KegsBurst { get; private set; }
        public int KegsSetOffByShots { get; private set; }
        /// <summary>Counters that dropped an attack (work order 015, Stopping Shot).</summary>
        public int Counters { get; private set; }
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

        // ---------------- The focus choice (work order 025, 2026-11-20) ----------------

        /// <summary>
        /// The run's focus choice (<see cref="RunFocusChoice"/>), put on her by the run while it goes on, as its upgrades
        /// are; null outside a run. In a run before the choice it is today's gauge.
        /// </summary>
        [System.NonSerialized] public RunFocusChoice RunFocus;
        /// <summary>The practice room's own way, set only by its keys (F2, F3, F4); today's unless one was pressed. Not saved anywhere.</summary>
        public FocusMode PracticeFocusMode { get; set; } = FocusMode.Today;
        /// <summary>How the gauge works now: the run's choice in a run, else the practice room's.</summary>
        public FocusMode FocusMode => RunFocus != null ? RunFocus.Mode : PracticeFocusMode;
        /// <summary>The numbers of the two ways, defaults in code; not serialized, so no scene or asset carries them.</summary>
        public FocusModeValues FocusModeValues { get; set; } = new FocusModeValues();
        /// <summary>Hair Trigger's parts; 1 for the whole gauge of the other two.</summary>
        public int FocusParts => FocusMode == FocusMode.HairTrigger ? Mathf.Max(1, FocusModeValues.Parts) : 1;
        /// <summary>Full parts of the gauge: Hair Trigger's, 0 to 3; else 1 when it is full.</summary>
        public int FocusPartsFull => _focus.PartsFull(Identity, FocusParts);
        /// <summary>True while Hair Trigger's priming waits for the next skill.</summary>
        public bool IsPrimed => _prime.IsPrimed(Time.time);
        public float PrimeRemaining => _prime.Remaining(Time.time);
        /// <summary>True while Steady Hand's window is open: Triple Tap and Scatter Blast read as the window changes them.</summary>
        public bool IsSteadyHandWindow => FocusMode == FocusMode.SteadyHand && _focus.IsEmpowered(Time.time);
        /// <summary>What the skill in use gained from the priming; None when it was not primed.</summary>
        public PrimeUse ActivePrime => _actionKind == ActionKind.Skill ? _castPrime : PrimeUse.None;
        /// <summary>Primed skills used, and primings that lapsed (for tests and the readout).</summary>
        public int PrimesUsed { get; private set; }
        public int PrimesLapsed { get; private set; }

        /// <summary>The share of focus she gains: 1, more with the run's Keen focus (work order 025).</summary>
        float FocusGainShare => Modifiers != null ? Modifiers.FocusGainShare : 1f;

        /// <summary>Focus from anything, through the run's Keen focus. Returns the focus gained.</summary>
        float GainFocus(float amount) => _focus.Add(amount * FocusGainShare, Identity, Time.time);

        /// <summary>How far the basic shot reaches: the feel's, and the run's Longer barrel (work order 025).</summary>
        public float BasicShotRange => Feel.BasicShotRange + (Modifiers != null ? Modifiers.BasicShotRangeBonus : 0f);

        /// <summary>
        /// Sets the practice room's way (its keys, F2, F3 and F4). Nothing changes in a run, where the run's choice holds.
        /// A priming waiting is dropped. False in a run.
        /// </summary>
        public bool SetPracticeFocusMode(FocusMode mode)
        {
            if (RunFocus != null) return false;
            PracticeFocusMode = mode;
            _prime.Clear();
            return true;
        }

        // ---------------- The kit (decision 0005) ----------------

        public KitValues Kit => SkillSet != null && SkillSet.Kit != null ? SkillSet.Kit : _fallbackKit;

        /// <summary>
        /// The skills she has and the keys they sit on (work order 015, 2026-11-20). A "slot" everywhere in this class is
        /// a skill's place in <see cref="Skills"/>, which says which skill it is; a key is a place on the bar
        /// (<see cref="SkillOnKey"/>), which the input, the skill bar and the reward screen go through. Made from the
        /// skill list the first time it is asked for, and again by <see cref="EquipStartSkills"/>.
        /// </summary>
        SkillLoadout Loadout
        {
            get
            {
                bool all = Kit.StartWithAllSkills;
                if (!_loadoutReady || _loadoutAll != all)
                {
                    _loadout.Start(Skills, all);
                    _loadoutReady = true;
                    _loadoutAll = all;
                }
                return _loadout;
            }
        }

        /// <summary>The keys of the bar: eight, in the order of the bindings' skill keys.</summary>
        public int KeyCount => SkillLoadout.KeyCount;
        /// <summary>The slot of the skill on a key, or -1 for an empty key.</summary>
        public int SkillOnKey(int key) => Loadout.SkillOn(key);
        /// <summary>The key a slot's skill sits on, or -1 when it is on none.</summary>
        public int KeyOfSkill(int slot) => Loadout.KeyOf(slot);
        /// <summary>The key a skill taken now would go on: the first free one in key order, or -1.</summary>
        public int FirstFreeKey => Loadout.FirstFreeKey;

        /// <summary>True when the slot holds a skill she can use: one she started the run with, or was given since.</summary>
        public bool IsEquipped(int slot) => slot >= 0 && slot < Skills.Count && (Kit.StartWithAllSkills || Loadout.Has(slot));

        /// <summary>
        /// She has the slot's skill from now on, on the first free key if it is on none (work order 015); with no key free
        /// she has it all the same, for the intents. For the floor reward, tests and tools. Returns its key, or -1.
        /// </summary>
        public int EquipSkill(int slot)
        {
            if (slot < 0 || slot >= Skills.Count) return -1;
            return Loadout.Take(slot);
        }

        /// <summary>Back to the skills a run starts with, on their own keys. For a new run, tests and tools.</summary>
        public void EquipStartSkills() => _loadoutReady = false;

        /// <summary>
        /// The run's upgrades (work order 012, <see cref="RunModifiers"/>), read where the values are used: damage dealt,
        /// a skill's cooldown, run speed, Quick Shot's first mark (and maximum health, by <see cref="PlayerHealth"/>).
        /// Null outside a run: the settings' values as they are. Nothing here writes to the settings.
        /// </summary>
        [System.NonSerialized] public RunModifiers Modifiers;

        /// <summary>
        /// The run's skill upgrades (work order 017, <see cref="RunSkillUpgrades"/>): <see cref="Skills"/> reads the skills
        /// through them. Null outside a run: the settings' skills as they are.
        /// </summary>
        [System.NonSerialized] public RunSkillUpgrades SkillUpgrades;

        /// <summary>How many skill upgrades the slot's skill has this run (work order 017): the pips on its box.</summary>
        public int UpgradesOn(int slot) =>
            SkillUpgrades != null && slot >= 0 && slot < Skills.Count && Skills[slot] != null ? SkillUpgrades.CountFor(Skills[slot].Name) : 0;

        /// <summary>The slot of the skill of this name, or -1.</summary>
        public int SlotOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            IReadOnlyList<SkillDefinition> skills = Skills;
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null && skills[i].Name == name) return i;
            return -1;
        }

        /// <summary>Tags that jumped from a tagged target that died to the nearest enemy (work order 017, Spreading Tag).</summary>
        public int TagsSpread { get; private set; }

        /// <summary>The slot's cooldown as it runs now: the skill's, less the run's cuts to it.</summary>
        public float CooldownOf(int slot)
        {
            if (slot < 0 || slot >= Skills.Count || Skills[slot] == null) return 0f;
            SkillDefinition skill = Skills[slot];
            return Modifiers != null ? skill.Cooldown * Modifiers.CooldownShare(skill.Name) : skill.Cooldown;
        }

        /// <summary>The share of the feel's run speed she runs at: 1, more with the run's upgrades.</summary>
        public float RunSpeedShare => Modifiers != null ? Modifiers.RunSpeedShare : 1f;

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
        /// The share of the open window in which the next press is clean (the clean part's length over the window's); 0
        /// when the next press cannot be clean (not the last, or the skill has neither a clean tap nor a clean spend).
        /// Before work order 040 the clean part was always that share at the window's end; where it lies now is
        /// <see cref="RecastCleanFrom"/> to <see cref="RecastCleanTo"/>.
        /// </summary>
        public float RecastCleanShare
        {
            get
            {
                CleanPart part = RecastCleanPart;
                float window = _chain.Window;
                return part.Any && window > 0f ? Mathf.Clamp01((part.To - part.From) / window) : 0f;
            }
        }
        // Work order 040 (Kristian's second session of 2026-11-20): the clean part comes early in the window, and the cast
        // bar shows the chain from the press before to the end of the window. They read only.
        /// <summary>
        /// The clean part of the open window, in seconds after it opened (<see cref="CleanPart.Of"/>: early, or with
        /// <see cref="KitValues.CleanEarly"/> off the last share); primed (work order 025) the whole window; none when the next
        /// press cannot be clean.
        /// </summary>
        public CleanPart RecastCleanPart =>
            _chain.Slot >= 0 && _chain.NextIsLast && _chain.Slot < Skills.Count && RecastChain.HasCleanPress(Skills[_chain.Slot])
                ? (_chainPrimed ? CleanPart.Whole(_chain.Window) : CleanPart.Of(Kit, _chain.Window)) : default;
        /// <summary>Seconds after the window opened that its clean part begins, and ends; both 0 when the next press cannot be clean.</summary>
        public float RecastCleanFrom => RecastCleanPart.From;
        public float RecastCleanTo => RecastCleanPart.To;
        /// <summary>Seconds since the press of the chain's current shot (or its opening move), on game time; -1 with no chain.</summary>
        public float RecastSincePress => _chain.Slot >= 0 ? Time.time - _chainPressAt : -1f;
        /// <summary>Seconds from that press to the window opening: the startup of the shot, or of Backstep's hop; 0 with no chain.</summary>
        public float RecastLead => _chain.Slot >= 0 && _chain.Slot < Skills.Count && Skills[_chain.Slot] != null ? Mathf.Max(0f, Skills[_chain.Slot].Startup) : 0f;
        float _chainPressAt;
        /// <summary>Presses of the recast chain running, or 0 (work order 025: five for Triple Tap in Steady Hand's window).</summary>
        public int RecastCount => _chain.Slot >= 0 ? _chain.Count : 0;
        // Work order 037 (Kristian's session of 2026-11-20): what the signs of the clean press read (PlayerSigns). They
        // read only; nothing here changes when a press is clean.
        /// <summary>The recast window's length in seconds, of the chain running; 0 with none.</summary>
        public float RecastWindowSeconds => _chain.Window;
        /// <summary>Seconds since the window for the next press opened, past its length once it has run out; -1 before it opens or with no chain.</summary>
        public float RecastSinceOpened => _chain.SinceOpened(Time.time);
        /// <summary>
        /// True when a press of the chain's key now would be clean: judged exactly as that press itself would be. Stunned,
        /// pulled or down in a quick duck a press waits and is judged as one that waited (the third review of 2026-11-20,
        /// work order 047), so the signs and the tick follow that.
        /// </summary>
        public bool RecastPressWouldBeClean =>
            _chain.Slot >= 0 && _chain.Slot < Skills.Count && !_chain.Expired(Time.time)
            && JudgeRecast(_chain.Slot, Time.time, buffered: _held.IsHeld || _duck.IsDown) == RecastPress.Clean;

        /// <summary>
        /// True while the character cannot start anything new: in a dash or a skill. A basic shot does not lock, and
        /// neither does a free recovery (decision 0004), which holds back only the next skill (<see cref="SkillGateRemaining"/>).
        /// </summary>
        public bool IsLocked => _dashing || _actionKind == ActionKind.Skill || _held.IsHeld; // stunned or pulled too (work order 035)
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
        /// <summary>Line shots, basic and skill, that cover stopped short of their range (Kristian, 2026-11-19).</summary>
        public int ShotsStoppedByCover { get; private set; }
        /// <summary>Where cover stopped the last line shot it stopped, on the ground.</summary>
        public Vector3 LastCoverStop { get; private set; }
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

        // The quick duck's rim goes with her, its object and its mesh (the third review of 2026-11-20, work order 047).
        void OnDestroy() => _rim.Dispose();

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
        /// The move keys as the input gives them (the third review of 2026-11-20, work order 047): <see cref="Steer(Vector3)"/>,
        /// and whether a key went down this frame (<see cref="QuickDuckRules.MoveKeyWentDown"/>). A quick duck ends on a key
        /// going down, never on one let go; steered by <see cref="Steer(Vector3)"/> alone (tests, tools), on a direction not
        /// held at the tap, as before.
        /// </summary>
        public void Steer(Vector3 direction, bool keyWentDown)
        {
            Steer(direction);
            _steeredByKeys = true;
            _moveKeyWentDown |= keyWentDown;
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

        /// <summary>
        /// The cover key (Left Ctrl to start with; Kristian 2026-11-19, an experiment): held or not. Given every frame by
        /// the input; it holds until it is changed. Held, she takes cover against a wall or prop in reach when she is free;
        /// let go, she leaves cover at once and the move keys work at full speed from this frame on. With the quick duck on
        /// (work order 043) the key going down is a tap (<see cref="TapCover"/>) and holding it does nothing more.
        /// </summary>
        public void HoldCover(bool held)
        {
            bool pressed = held && !_coverHeld;
            _coverHeld = held;
            if (!held && _inCover) LeaveCover(CoverLeaving.Released);
            if (pressed && QuickDuckOn) TapCover();
        }

        /// <summary>
        /// Press: a tap of the cover key with the quick duck on (work order 043, <see cref="QuickDuck"/>). With a low prop
        /// within <see cref="FeelValues.DuckReach"/> she drops where she stands for <see cref="FeelValues.DuckSeconds"/>; with
        /// none, nothing happens but a small dull sound, and no wait starts. Down already, the key waiting, or she cannot
        /// duck now (a skill, a dash, Slide, stunned or pulled, dead, a choice, paused): nothing. A basic shot that has not
        /// gone off is dropped by the duck, as a dash drops it. For the input and tests.
        /// </summary>
        public DuckTap TapCover()
        {
            FeelValues feel = Feel;
            if (!feel.TakeCover || !feel.QuickDuck) return DuckTap.Off;
            float now = Time.time;
            bool may = MayDuckNow;
            Shelter at = null;
            CoverShape shape = default;
            bool found = _duck.Ready(now) && may && FindLowProp(feel, out at, out shape);
            DuckTap tap = _duck.Tap(now, may, found, feel.DuckSeconds);
            if (tap == DuckTap.Ducked) StartDuck(at, shape);
            else if (tap == DuckTap.NothingInReach)
            {
                DuckTapsWithNothing++;
                if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.EnemyHit, DuckNothingVolume); // its dull tick
                Report(new PlayerReport { Kind = PlayerReportKind.DuckNothing });
            }
            return tap;
        }

        /// <summary>True while she cannot start a basic shot now: a shot, a skill or a dash is going, or she is down in a quick duck (work order 043).</summary>
        bool Busy => _actionKind != ActionKind.None || _dashing || _held.IsHeld || _duck.IsDown;

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
            RefreshDashCharges(); // work order 029: Spare Step; work order 065: a second dash
            if (_dashing || !_dash.CanStart(Time.time))
                return DashPressed(IntentResult.Rejected, _dashing ? 0f : _dash.CooldownRemaining(Time.time));
            if (_held.IsHeld) return DashPressed(BufferPress(PressKind.Dash, point), 0f); // stunned or pulled: it waits, as in a lock (work order 035)

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
            RecastPress recast = JudgeRecast(slot, Time.time);
            if ((_held.IsHeld || _duck.IsDown) && recast != RecastPress.None)
            {
                // Stunned or pulled (work order 035), or down in a quick duck (work order 043): even a recast waits, as in a lock.
                _shotCursor = point;
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
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
            // In a lock, or in a free recovery: the next skill waits out the recovery, through the buffer. Slide (work
            // order 015) is its own move: it waits only for a dash, a committed skill or a Slide still moving her.
            bool waits = Skills[slot].Input == SkillInput.KeyMove
                ? _dashing || _keyMoving || _held.IsHeld || (_actionKind == ActionKind.Skill && !IsInLightSkill)
                : IsLocked || Time.time < _skillGateUntil;
            waits |= _duck.IsDown; // down in a quick duck no skill starts (work order 043): it waits, as in a lock
            if (waits)
            {
                _bufferedSkillReleased = false;
                return SkillPressed(slot, BufferPress(PressKind.Skill, point, slot), 0f);
            }
            SkillPressed(slot, IntentResult.Started, 0f);
            StartSkill(slot, point);
            return IntentResult.Started;
        }

        /// <summary>
        /// What a press of this slot's key is to the recast chain now (decision 0005), its clean part as the kit places it
        /// (work order 040). A press that waited in the buffer from before the window opened (<paramref name="buffered"/>) is
        /// a shot, never clean. A primed chain's press of its last shot is clean wherever in the window it falls, a buffered
        /// one too, as before (Hair Trigger, work order 025: its timing counts as right).
        /// </summary>
        RecastPress JudgeRecast(int slot, float now, bool buffered = false)
        {
            bool hasClean = RecastChain.HasCleanPress(Skills[slot]);
            RecastPress judged = _chain.Judge(slot, now, CleanPart.Of(Kit, _chain.Window), hasClean, buffered);
            return _chainPrimed && _chain.Slot == slot ? FocusModeRules.Primed(judged, _chain.NextIsLast, hasClean) : judged;
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
            // Work order 040: a press of the last shot after the clean part, in the window, was late (the word says so).
            bool late = !clean && _chain.Slot == slot && RecastCleanPart.IsAfter(_chain.SinceOpened(now));
            _chain.Recast();
            float focus = clean ? GainFocus(Identity.FocusPerCleanTap) : 0f;
            Report(new PlayerReport { Kind = PlayerReportKind.RecastPressed, Slot = slot, Shot = _chain.Shots, Clean = clean, Focus = focus, Late = late });
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
            RunChainOnThroughHitStop();
            if (_chain.Expired(now)) EndChain();
        }

        // Work order 038 (Kristian's answer of 2026-11-20: the recast window runs on real time). A hit stop froze the
        // window's game-time clock, so the clean part moved from one use to the next. Once a frame, before anything in it
        // judges a press (every press, the buffer's and the frame's own update pass here first), the open window runs on
        // by the real seconds a hit stop froze (RecastClock); a pause, a choice or a screen still stops it.
        // Off (KitValues.RecastOnRealTime), nothing runs on and the window is as it was.
        int _chainRanOnFrame = -1;

        void RunChainOnThroughHitStop()
        {
            if (_chainRanOnFrame == Time.frameCount) return;
            _chainRanOnFrame = Time.frameCount;
            _chain.RunOn(RecastClock.HitStopSeconds(Kit.RecastOnRealTime, Time.deltaTime, Time.unscaledDeltaTime, HitStop.HoldsThisFrame,
                                                    PlayerInputDriver.HeldByChoice || PlayerInputDriver.HeldByScreen, HitStop.ScaleFound));
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
            RecastPress recast = JudgeRecast(press.Slot, now, buffered: true); // work order 040: a press that waited is never clean
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
        /// so it can be pressed in the middle of a skill. Rejected unless the gauge is full. With Hair Trigger (work order
        /// 025) it spends one full part instead, rejected with none full, and primes the next skill.
        /// </summary>
        public IntentResult ActivateIdentity()
        {
            float now = Time.time;
            FocusMode mode = FocusMode;
            FocusModeValues ways = FocusModeValues;
            // Work order 025: Hair Trigger spends one full part, with no window; Steady Hand a full gauge on a longer window.
            bool started = mode == FocusMode.HairTrigger ? _focus.TrySpendPart(Identity, FocusParts)
                         : _focus.TryActivate(now, Identity, (mode == FocusMode.SteadyHand ? ways.WindowSeconds : Identity.EmpoweredDuration) * LongFocusNow); // work order 065: Long focus
            IntentResult result = started ? IntentResult.Started : IntentResult.Rejected;
            Report(new PlayerReport { Kind = PlayerReportKind.FocusPressed, Result = result, FocusMode = mode });
            if (result == IntentResult.Started)
            {
                // Decision 0005: every running cooldown, the dash's too, is cut by a share of what it has left; a part's
                // share is smaller (work order 025).
                float share = mode == FocusMode.HairTrigger ? ways.PartCooldownCut : Identity.CooldownCut;
                var cuts = new float[Skills.Count];
                for (int slot = 0; slot < cuts.Length; slot++) cuts[slot] = CooldownFor(slot).Cut(now, share);
                float dashCut = _dash.CutCooldown(now, share);
                Report(new PlayerReport { Kind = PlayerReportKind.CooldownsCut, Cuts = cuts, DashCut = dashCut });
                if (mode == FocusMode.HairTrigger) _prime.Prime(now, ways.PrimeSeconds * LongFocusNow); // work order 065: Long focus
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
            EndKeyMove();
            ClearKegs();
            if (_inCover) LeaveCover(CoverLeaving.Other);
            // The cover key stays as it is held (the third review of 2026-11-20, work order 047): a key held through a new
            // floor is no tap on arrival.
            _peekUntil = float.NegativeInfinity;
            EndDuck(DuckEnd.Other); // a quick duck ends, with no wait left and no reward (work order 043): death, a restart, a new floor
            _duck.Clear();
            _rim.Hide();
            _held.Clear(); // a stun or a pull ends (work order 035): death, a restart, a new floor
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
            _focus.Clear();
            _prime.Clear(); // and no priming (work order 025)
            _held.Clear(); // and no stun or pull (work order 035)
            _dash.Clear();
            _cooldowns.Clear(); // made again, ready, when next asked for
            // The basic shot too (the second reviews of 2026-11-20, work order 033): no hits counted toward its next mark,
            // no wait for its next shot, the first from the right hand, and no slow of a shot on the move or turn held
            // from the last one.
            _basicMarks.Reset();
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

        /// <summary>
        /// The dashes she holds, read again (work order 029's Spare Step; work order 065: her Second dash, <see cref="HerStats.DashCharges"/>):
        /// at every press, when a run starts and when a chest's upgrade is taken. A change makes every charge ready.
        /// </summary>
        public void RefreshDashCharges() => _dash.MaxCharges = Modifiers != null ? Modifiers.Stats.DashCharges : 1;

        /// <summary>
        /// Ends a focus window that runs, and any priming, keeping the gauge's focus (the second reviews of 2026-11-20,
        /// work order 033): called when the run's focus choice is taken.
        /// </summary>
        public void EndFocusWindow()
        {
            _focus.EndWindow();
            _prime.Clear();
        }

        /// <summary>Dashes ready now (work order 029: two with Spare Step); 1 or 0 otherwise.</summary>
        public int DashChargesReady => _dash.ChargesReady(Time.time);
        /// <summary>The dashes the dash holds now (work order 029).</summary>
        public int DashMaxCharges => _dash.MaxCharges;

        // ---------------- Stunned and pulled (work order 035, 2026-11-20; decision 0010) ----------------
        // Two things the melee kinds' blue skills do to her (MeleeEnemy): the brute's Shield bash stuns her, the guard's
        // Drag pulls her in. Stunned: for its time she cannot move, shoot, use a skill or dash; a press meanwhile waits in
        // the input buffer as in a lock and keeps only what the buffer allows at its end; it never stacks or chains
        // (HeldState: none while she is held, and none for HeldState.StunGrace after one ends). Pulled: she is moved along
        // the ground toward the puller on the NavMesh, stopping at walls and props as her own movement does, and cannot
        // act meanwhile; afterwards she acts at once. Neither starts while she is dead, a run's result is in, the game is
        // paused or a choice is open; both end at once on death, a restart and leaving a floor (Teleport, StartFresh).

        readonly HeldState _held = new HeldState();
        PlayerHealth _herHealth;

        /// <summary>True while a stun holds her (work order 035).</summary>
        public bool IsStunned => _held.IsStunned;
        /// <summary>True while a pull moves her (work order 035).</summary>
        public bool IsPulled => _held.IsPulled;
        /// <summary>True while she is stunned or pulled: she cannot act (work order 035).</summary>
        public bool IsHeld => _held.IsHeld;
        /// <summary>What holds her and since when; for the effects and tests.</summary>
        public HeldState HeldBy => _held;
        public int TimesStunned { get; private set; }
        public int TimesPulled { get; private set; }

        bool MayBeHeldNow
        {
            get
            {
                if (_herHealth == null) _herHealth = GetComponent<PlayerHealth>();
                GeneratedLevels levels = GeneratedLevels.Current;
                return MeleeYouReadRules.MayBeHeld(_herHealth == null || _herHealth.IsAlive, _herHealth != null && _herHealth.Untouchable,
                                                   levels != null && levels.Paused, levels != null && levels.Choice != GeneratedLevels.ChoiceKind.None);
            }
        }

        /// <summary>Stuns her for <paramref name="seconds"/> (work order 035), unless she may not be held now; <paramref name="by"/> names the attack. True if it did.</summary>
        public bool Stun(float seconds, string by = null)
        {
            if (!MayBeHeldNow || !_held.TryStun(Time.time, seconds)) return false;
            Interrupt();
            TimesStunned++;
            Report(new PlayerReport { Kind = PlayerReportKind.Stunned, Seconds = seconds, By = by });
            return true;
        }

        /// <summary>Pulls her to <paramref name="to"/> over <paramref name="seconds"/> (work order 035), unless she may not be held now. True if it did.</summary>
        public bool Pull(Vector3 to, float seconds, string by = null)
        {
            if (!MayBeHeldNow || !_held.TryPull(Time.time, GroundPosition, new GroundPoint(to.x, to.z), seconds)) return false;
            Interrupt();
            TimesPulled++;
            Report(new PlayerReport { Kind = PlayerReportKind.Pulled, Seconds = seconds, By = by });
            return true;
        }

        /// <summary>A stun or a pull takes her: what she was doing is dropped, as a teleport drops it, where she stands.</summary>
        void Interrupt()
        {
            if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.Cancelled, Time.time);
            _action.Cancel();
            _cast.Cancel();
            EndAction();
            // A recast chain whose shot (or opening move) is dropped before it went off would wait for a window that never
            // opens, and its key and every new skill with it (found merging work orders 035 and 038: the Ace stood so for
            // the rest of PilotTests' fight). It ends, as a teleport ends it; a window already open runs on, as before.
            if (_chain.SinceOpened(Time.time) < 0f) EndChain();
            _dashing = false;
            _buffer.Clear();
            DropWaitingClick();
            _shotFacing.End(turnBack: false);
            _easing = false;
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            EndKeyMove();
            if (_inCover) LeaveCover(CoverLeaving.Other);
            EndDuck(DuckEnd.Other); // and a quick duck (work order 043)
            _rim.Hide();
            Halt();
        }

        /// <summary>Each frame: a pull moves her along; true while she is still held, false once it is over (she acts at once).</summary>
        bool UpdateHeld(float now)
        {
            if (!_held.IsHeld) return false;
            if (_held.IsPulled)
            {
                GroundPoint p = _held.PullPoint(now);
                Vector3 step = Flat(new Vector3(p.X, 0f, p.Z) - transform.position);
                if (_agent.isOnNavMesh && step.sqrMagnitude > 1e-10f) _agent.Move(step); // on the NavMesh: it stops at walls and props
            }
            if (_held.Step(now) != HeldKind.None) return false;
            Halt();
            return true;
        }

        // ---------------- Simulation ----------------

        void Update()
        {
            FeelValues feel = Feel;
            float now = Time.time;
            ApplyMovementFeel(feel);
            ExpireChain(now);
            BurstKegsOnFuse(now);
            if (_prime.TakeLapsed(now))
            {
                PrimesLapsed++;
                Report(new PlayerReport { Kind = PlayerReportKind.PrimeLapsed }); // Hair Trigger's priming, unused (work order 025)
            }
            if (UpdateHeld(now)) return; // stunned or pulled: nothing else this frame (work order 035)

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
            if (_inCover)
            {
                CoverSlide();
                return;
            }
            if (_duck.IsDown)
            {
                // Down in a quick duck nothing moves her (work order 043); a move key not held at the tap ends the duck first.
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            if (_moveInput == Vector3.zero)
            {
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            MoveAlong(_moveInput, _agent.speed);
        }

        // ---------------- Taking cover (Kristian 2026-11-19, an experiment) ----------------

        /// <summary>
        /// Every frame she is not dashing: leaves cover when the key is up, the switch is off or the thing is gone; takes
        /// cover when the key is held, she is free and something is in reach; and moves her to stand against it.
        /// </summary>
        void UpdateCover(FeelValues feel, float now)
        {
            UpdateDuck(feel, now); // work order 043
            // With the quick duck on, taking cover is off (work order 043).
            if (_inCover && (!feel.TakeCover || feel.QuickDuck || !_coverHeld || _shelter == null || !_shelter.isActiveAndEnabled))
                LeaveCover(_coverHeld ? CoverLeaving.Other : CoverLeaving.Released);
            if (!_inCover && feel.TakeCover && !feel.QuickDuck && _coverHeld && !IsLocked && !_keyMoving) TryTakeCover(feel, now);
            if (!_inCover) return;

            Vector3 at = transform.position;
            float dt = Time.deltaTime;
            if (dt > 0f) _coverVelocity = Flat(at - _coverLastPos) / dt;
            _coverLastPos = at;
            if (_snapping)
            {
                float k = feel.CoverSnapTime > 0f ? Mathf.Clamp01((now - _snapStartedAt) / feel.CoverSnapTime) : 1f;
                MoveInCover(Vector3.Lerp(_snapFrom, _snapTo, k));
                if (k >= 1f) _snapping = false;
            }
        }

        /// <summary>Takes cover against the nearest wall or prop that blocks walking within reach, if there is one.</summary>
        void TryTakeCover(FeelValues feel, float now)
        {
            GroundPoint her = GroundPosition;
            Shelter best = null;
            CoverShape bestShape = default;
            float bestDistance = feel.CoverReach;
            IReadOnlyList<Shelter> all = Shelter.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || !all[i].TryShape(out CoverShape shape)) continue;
                float distance = shape.Distance(her);
                if (distance > bestDistance) continue;
                best = all[i];
                bestShape = shape;
                bestDistance = distance;
            }
            if (best == null) return;

            _inCover = true;
            _shelter = best;
            _coverShape = bestShape;
            _coverTall = best.IsTall;
            GroundPoint place = TakeCoverRules.StandPlace(bestShape, her, feel.CoverStandOff);
            _snapFrom = transform.position;
            _snapTo = new Vector3(place.X, _snapFrom.y, place.Z);
            _snapStartedAt = now;
            _snapping = true;
            _coverLastPos = transform.position;
            _coverVelocity = Vector3.zero;
            Halt();
            CoversTaken++;
            Report(new PlayerReport { Kind = PlayerReportKind.CoverEntered, Tall = _coverTall });
        }

        void LeaveCover(CoverLeaving why)
        {
            if (!_inCover) return;
            _inCover = false;
            _shelter = null;
            _snapping = false;
            _coverVelocity = Vector3.zero;
            // The key going up gives the move keys back at once, at full speed: no ease up from a skill used in cover.
            if (why == CoverLeaving.Released) _rising = false;
            Report(new PlayerReport { Kind = PlayerReportKind.CoverLeft, Leaving = why });
        }

        /// <summary>
        /// The move keys in cover: ducked, they slide her along it at the cover share of run speed, staying against it
        /// (<see cref="TakeCoverRules.Slide"/>); stood up by a shot or a skill, or on her way there, she stays where she is.
        /// </summary>
        void CoverSlide()
        {
            if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
            if (_snapping || StoodUpInCover || _moveInput == Vector3.zero) return;
            FeelValues feel = Feel;
            // Her run speed with the run's upgrades, as everywhere else she moves (work order 026, the second review of 2026-11-20).
            GroundPoint next = TakeCoverRules.Slide(_coverShape, GroundPosition, new GroundPoint(_moveInput.x, _moveInput.z),
                                                    feel.RunSpeed * RunSpeedShare * feel.CoverMoveShare * Time.deltaTime, feel.CoverStandOff, feel.CoverSlideAngle);
            MoveInCover(new Vector3(next.X, transform.position.y, next.Z));
        }

        /// <summary>Moves her to a point in cover by the NavMesh agent, so she stays on the NavMesh.</summary>
        void MoveInCover(Vector3 point)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 1e-10f && _agent.isOnNavMesh) _agent.Move(d);
        }

        // ---------------- The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043) ----------------

        /// <summary>How loud the duck's sounds play, as a share of their own volume (first guesses).</summary>
        const float DuckNothingVolume = 0.6f, DuckStopVolume = 0.7f;
        /// <summary>Metres over the ground of the spark where a duck stopped something: about where an arrow flies.</summary>
        const float DuckSparkHeight = 0.9f;
        /// <summary>The skill whose stagger the timing's reward deals: a strong hit that exists (no new strength).</summary>
        const string PeekStaggerOf = "Scatter Blast";

        /// <summary>
        /// May a duck start now? Not in a skill, a dash or Slide, not stunned or pulled, and never while she is dead, a run's
        /// result is in, the game is paused or a choice is open (as for a stun or a pull, <see cref="MayBeHeldNow"/>).
        /// </summary>
        bool MayDuckNow => !_dashing && _actionKind != ActionKind.Skill && !_keyMoving && !_held.IsHeld && MayBeHeldNow;

        /// <summary>
        /// May a duck going on go on? Not once she is dead or a run's result is in. A pause or a choice only holds it, as
        /// game time stands still; a stun, a pull, a restart and a new floor end it where they happen.
        /// </summary>
        bool DuckMayGoOn
        {
            get
            {
                if (_herHealth == null) _herHealth = GetComponent<PlayerHealth>();
                return _herHealth == null || (_herHealth.IsAlive && !_herHealth.Untouchable);
            }
        }

        /// <summary>The props' sort follows her switch, live (<see cref="Cover.QuickDuckSort"/>).</summary>
        bool DuckSortIsOn() => this != null && QuickDuckOn;

        /// <summary>
        /// Every frame she is not dashing (with taking cover): the props' sort follows the switch; a duck ends when the
        /// switch goes off, she may not be down any more or the prop is gone, rises by itself when its time is up, and ends
        /// at a move key that was not held at the tap; and the rim light goes on the low prop a tap would work at.
        /// </summary>
        void UpdateDuck(FeelValues feel, float now)
        {
            if (_duckSort == null) _duckSort = DuckSortIsOn;
            Cover.QuickDuckSort = _duckSort;
            if (_duck.IsDown)
            {
                // Let go: the next key ends it. With the keys known and none gone down, what is held now was held at the tap
                // (work order 047): letting go of one of two keys does not end it.
                if (_moveInput == Vector3.zero || (_steeredByKeys && !_moveKeyWentDown)) _duckHeldAtTap = _moveInput;
                if (!feel.TakeCover || !feel.QuickDuck || !DuckMayGoOn || _duckShelter == null || !_duckShelter.isActiveAndEnabled)
                    EndDuck(DuckEnd.Other);
                else if (_duck.RisesBy(now)) EndDuck(DuckEnd.Rose);
                // Steered by the keys, a key going down ends it, whatever the direction comes to (the fourth review of
                // 2026-11-21, work order 055: the opposite key, pressed with one held, gives no direction); steered by a
                // direction alone, a direction not held at the tap, as before.
                else if (_steeredByKeys ? _moveKeyWentDown
                         : QuickDuckRules.MoveEnds(new GroundPoint(_duckHeldAtTap.x, _duckHeldAtTap.z), new GroundPoint(_moveInput.x, _moveInput.z)))
                    EndDuck(DuckEnd.Moved);
            }
            _steeredByKeys = false; // looked at once a frame (work order 047)
            _moveKeyWentDown = false;
            if (feel.TakeCover && feel.QuickDuck && _duck.Ready(now) && MayDuckNow && FindLowProp(feel, out Shelter at, out CoverShape shape))
                _rim.Show(at, shape, ShotFxMaterial);
            else
                _rim.Hide();
        }

        /// <summary>
        /// The nearest low prop within <see cref="FeelValues.DuckReach"/> of her, her middle to its edge: a shelter that is not
        /// tall (<see cref="Shelter.IsTall"/>). A wall or a tall prop is never one. Nothing is allocated.
        /// </summary>
        bool FindLowProp(FeelValues feel, out Shelter best, out CoverShape bestShape)
        {
            GroundPoint her = GroundPosition;
            best = null;
            bestShape = default;
            float bestDistance = feel.DuckReach;
            IReadOnlyList<Shelter> all = Shelter.All;
            for (int i = 0; i < all.Count; i++)
            {
                Shelter s = all[i];
                if (s == null || !s.TryShape(out CoverShape shape)) continue;
                float distance = shape.Distance(her);
                if (distance > bestDistance || s.IsTall) continue; // in reach (QuickDuckRules.InReach) and nearer than the best so far
                best = s;
                bestShape = shape;
                bestDistance = distance;
            }
            return best != null;
        }

        /// <summary>
        /// She drops where she stands beside the prop: no snap, no slide. A basic shot that has not gone off is dropped, as a
        /// dash drops it, and one that has is over: she cannot shoot while down.
        /// </summary>
        void StartDuck(Shelter at, CoverShape shape)
        {
            if (_actionKind == ActionKind.BasicShot)
            {
                _action.Cancel();
                EndAction();
            }
            _duckShelter = at;
            _duckShape = shape;
            _duckHeldAtTap = _moveInput;
            _moveKeyWentDown = false; // a key that went down with the tap is held at it (work order 047)
            _duckStopAtKnown = false;
            _rim.Hide();
            Halt();
            DucksTaken++;
            Report(new PlayerReport { Kind = PlayerReportKind.Ducked, By = at != null ? at.name : null });
        }

        /// <summary>The duck going on ends, and the key's wait runs from the moment she is up: its own time when she rose by herself.</summary>
        void EndDuck(DuckEnd why)
        {
            if (!_duck.IsDown) return;
            _duck.End(why == DuckEnd.Rose ? _duck.UpAt : Time.time, Feel.DuckWait);
            int stopped = _duck.StoppedLast;
            if (stopped > 0) DucksThatStopped++;
            _duckShelter = null;
            _duckStopAtKnown = false;
            Report(new PlayerReport { Kind = PlayerReportKind.DuckEnded, DuckEnd = why, Shot = stopped });
        }

        /// <summary>
        /// The duck stopped a thing that flew at her (or the shooter's old line): counted, a spark at the prop where it
        /// ended (her shots' spark on cover), the placeholder sounds' heavy low impact, and the word DUCKED over her.
        /// </summary>
        void DuckStoppedOne()
        {
            _duck.StoppedOne();
            ShotsStoppedByDuck++;
            Vector3 at = _duckStopAtKnown ? _duckStopAt : FarSideOfDuck();
            _duckStopAtKnown = false;
            SpawnCoverHit(LookFor(null, 0f), new Vector3(at.x, DuckSparkHeight, at.z));
            if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.Strike, DuckStopVolume);
            Report(new PlayerReport { Kind = PlayerReportKind.DuckStopped, At = new GroundPoint(at.x, at.z) });
        }

        /// <summary>The middle of the far side of what she is down beside, seen from her: where a shot from beyond it meets it.</summary>
        Vector3 FarSideOfDuck()
        {
            GroundPoint c = _duckShape.Centre, away = c - GroundPosition;
            float length = away.Length;
            away = length > 1e-4f ? away * (1f / length) : new GroundPoint(transform.forward.x, transform.forward.z);
            GroundPoint p = _duckShape.Closest(c + away * 10f);
            return new Vector3(p.X, 0f, p.Z);
        }

        /// <summary>
        /// A basic shot starts: is it the timing's reward, her first started within <see cref="FeelValues.DuckPeekWindow"/>
        /// of rising from a duck that stopped something? Either way her first shot has had its turn.
        /// </summary>
        bool TakePeekShot(FeelValues feel)
        {
            bool peek = _duck.TakePeekShot(Time.time, feel.DuckPeekWindow) && feel.DuckPeekShot && QuickDuckOn;
            if (peek) PeekShots++;
            return peek;
        }

        /// <summary>The stagger the basic shot going off deals: Scatter Blast's for the timing's reward, else none (as always).</summary>
        float TakePeekStagger()
        {
            if (!_peekShot) return 0f;
            _peekShot = false;
            int slot = SlotOf(PeekStaggerOf);
            return slot >= 0 && Skills[slot] != null ? Mathf.Max(0f, Skills[slot].Stagger) : 0f;
        }

        /// <summary>Moves her along a flat direction of length 1 at this speed, steering the agent a little way ahead.</summary>
        void MoveAlong(Vector3 direction, float speed)
        {
            if (!_agent.isOnNavMesh) return;
            // While Slide moves her (work order 015) nothing else does: no walk, no slow of a shot, no skill's slide.
            if (_keyMoving)
            {
                Halt();
                return;
            }
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
            if (_duck.IsDown) return false; // waits out a quick duck, and is judged when she rises as at the end of a lock (work order 043)
            if (waiting.Kind == PressKind.Skill && now < _skillGateUntil) return false; // waits out a free recovery
            FeelValues feel = Feel;
            // A gate is set only by a free recovery, and a new skill, a dash or a teleport clears it.
            bool afterFreeRecovery = waiting.Kind == PressKind.Skill && feel.SkillRecoveryFree
                                     && _skillGateUntil != float.NegativeInfinity;
            // A press made while she was down counts from the moment she rose (the third review of 2026-11-20, work order
            // 047): kept through the duck, then the ordinary buffer from there, as a click waits and fires.
            float pressedAt = waiting.At >= _duck.DownAt && waiting.At <= _duck.RoseAt ? _duck.RoseAt : waiting.At;
            if (SkillGate.PressStillGood(pressedAt, now, feel.InputBuffer, afterFreeRecovery, _gateLetGoAt)) return true;
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
            _riseOfChain = skill != null && skill.RecastCount > 1; // work order 040: a recast chain's shot rises quicker (OffTheMark)
        }

        // Work order 040 (Kristian's second session of 2026-11-20: quicker off the mark after shooting). The three numbers
        // that hold her back after a shot are read through OffTheMark: with FeelValues.QuickOffTheMark on, the quicker ones
        // beside them; off, as before.
        bool _riseOfChain; // the rise running is after a recast chain's shot

        void ApplyMovementFeel(FeelValues feel)
        {
            if (Time.timeScale > 0f) _lastVelocity = _agent.velocity;
            if (_rising && SkillSlide.IsRisen(_riseFrom, Time.time - _riseStartedAt, OffTheMark.SkillSpeedRecovery(feel, _riseOfChain))) _rising = false;
            // A shot on the move that was cancelled before it went off ends the slow at once.
            if (_easing && _easeFiredAfter < 0f && !_movingShot) _easing = false;
            if (_easing && MovingShot.IsOver(Time.time - _easeStartedAt, _easeFiredAfter, feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel)))
                _easing = false;
            // The backpedal cap: held turned away from her movement by a shot, she moves at no more than a share of
            // run speed, eased in and out (decision 0003, a guess).
            _backpedalCap.Update(_shotFacing.State == ShotFacing.Mode.Held && IsMoving, Time.time, feel.BackpedalSpeedShare,
                                 feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel));
            _agent.speed = feel.RunSpeed * RunSpeedShare * CurrentSpeedShare(feel);
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
            return transform.position + new Vector3(dir.X, 0f, dir.Z) * BasicShotRange;
        }

        void Halt()
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        // ---------------- Slide: a skill that moves her along the move keys (work order 015, 2026-11-20) ----------------
        // Not the slowdown of decision 0004 (SkillSlide, _sliding): this is a skill of its own. She moves MoveDistance
        // along the held move keys (the way she faces with none held) over its Startup, on the NavMesh as the dash is, with
        // no invulnerability. It is no action: a basic shot starts and goes off during it as anywhere, and does not slow
        // it, for nothing else moves her while it runs. A dash ends it. Its charges are the cooldown's (Cooldown.MaxCharges).

        void StartKeyMove(int slot)
        {
            SkillDefinition skill = Skills[slot];
            float now = Time.time;
            Vector3 dir = _moveInput != Vector3.zero ? _moveInput : Flat(transform.forward);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            LeaveCover(CoverLeaving.MovingSkill); // a skill that moves her takes her out of cover, as the dash does
            CooldownFor(slot).Start(now, CooldownOf(slot));
            _keyMoving = true;
            _keyMoveSlot = slot;
            _keyMoveFrom = transform.position;
            _keyMoveTo = _keyMoveFrom + dir * skill.MoveDistance;
            _keyMoveStartedAt = now;
            _keyMoveSeconds = Mathf.Max(0f, skill.Startup);
            Halt();
            Report(new PlayerReport { Kind = PlayerReportKind.SkillStarted, Slot = slot });
            KeyMove(now);
        }

        void KeyMove(float now)
        {
            float k = _keyMoveSeconds > 0f ? Mathf.Clamp01((now - _keyMoveStartedAt) / _keyMoveSeconds) : 1f;
            Vector3 target = Vector3.Lerp(_keyMoveFrom, _keyMoveTo, k);
            if (_agent.isOnNavMesh)
            {
                _agent.velocity = Vector3.zero;
                // NavMeshAgent.Move keeps her on the NavMesh, so it stops at walls and edges, as the dash does.
                _agent.Move(Flat(target - transform.position));
            }
            if (k >= 1f) EndKeyMove();
        }

        void EndKeyMove()
        {
            if (!_keyMoving) return;
            _keyMoving = false;
            Report(new PlayerReport { Kind = PlayerReportKind.SkillEnded, Slot = _keyMoveSlot, Ending = SkillEnding.RanOut });
            _keyMoveSlot = -1;
        }

        // ---------------- Powder Keg: thrown, then set off (work order 015, 2026-11-20) ----------------
        // A skill with a Fuse does not burst where it lands: the keg lies there with its circle showing, and bursts when the
        // fuse runs out, or at once, for ShotBurstScale times the damage, when a shot of hers passes through it (a basic
        // shot, or a line skill; the shot goes on). Each keg is its own: two would lie and burst apart.

        void PlaceKeg(SkillDefinition skill, SkillArea area, Look look)
        {
            GroundPoint c = area.Centre;
            var centre = new Vector3(c.X, 0f, c.Z);
            LastKegPoint = centre;
            ShotFx circle = ShotFx.Outline(centre, skill.Radius, skill.Fuse, look.Paint, ShotFxMaterial);
            _kegs.Add(new LyingKeg { Skill = skill, Slot = _castSlot, Centre = c, BurstAt = Time.time + skill.Fuse, Circle = circle, Prime = _castPrime });
        }

        void BurstKegsOnFuse(float now)
        {
            for (int i = _kegs.Count - 1; i >= 0; i--)
                if (i < _kegs.Count && now >= _kegs[i].BurstAt) BurstKeg(i, 1f);
        }

        /// <summary>Sets off every lying keg a line from <paramref name="origin"/> toward <paramref name="aim"/>, this long and wide, passes through.</summary>
        void SetOffKegs(GroundPoint origin, GroundPoint aim, float range, float halfWidth, float length)
        {
            for (int i = _kegs.Count - 1; i >= 0; i--)
            {
                if (i >= _kegs.Count) continue;
                LyingKeg keg = _kegs[i];
                if (!LineShot.Hits(origin, aim, range, halfWidth, keg.Centre, KegRadius, length)) continue;
                KegsSetOffByShots++;
                BurstKeg(i, Mathf.Max(0f, keg.Skill.ShotBurstScale));
            }
        }

        void BurstKeg(int index, float damageScale)
        {
            LyingKeg keg = _kegs[index];
            _kegs.RemoveAt(index);
            if (keg.Circle != null) Destroy(keg.Circle.gameObject);
            KegsBurst++;
            var area = new SkillArea(keg.Skill, keg.Centre, keg.Centre, CoverQuery);
            Look look = LookFor(keg.Skill, 1f);
            Vector3 shotPoint = SpawnSkillFx(keg.Skill, look, area);
            PlaySound(look);
            int damage = Mathf.RoundToInt((SkillRules.Damage(keg.Skill, 1f) + FlatFor(keg.Skill)) * damageScale); // work order 050: her flat damage, by its weight
            bool landed = HitArea(keg.Skill, keg.Slot, area, look, shotPoint, damage, keg.Skill.Stagger, clean: false, out _, keg.Prime);
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = keg.Slot, Landed = landed, Charge = 1f, Clean = damageScale > 1f, At = keg.Centre });
            if (landed) HitStop.Trigger(Feel.HitStop);
        }

        void ClearKegs()
        {
            foreach (LyingKeg keg in _kegs)
                if (keg.Circle != null) Destroy(keg.Circle.gameObject);
            _kegs.Clear();
        }

        // ---------------- Dash ----------------

        bool StartDash(Vector3 point)
        {
            FeelValues feel = Feel;
            if (!_dash.TryStart(Time.time, feel, Modifiers != null ? Modifiers.DashCooldownShare : 1f)) return false; // work order 017: the chests' step

            Vector3 dir = Flat(point - transform.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Flat(transform.forward);
            dir.Normalize();

            LeaveCover(CoverLeaving.Dash); // the dash leaves cover at once (Kristian 2026-11-19, an experiment)
            EndDuck(DuckEnd.Dashed); // and ends a quick duck at once (work order 043)
            _rim.Hide();
            Halt();
            // A dash ends a skill's slide and the ease back up after one at once, and a free recovery with them:
            // a dash cancels a skill, recovery and all.
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            EndChain(); // and a recast chain (decision 0005)
            EndKeyMove(); // and Slide (work order 015): the dash takes over
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
            _peekShot = TakePeekShot(feel); // the quick duck's reward (work order 043)
            if (!onTheMove) Halt();
            _aimPoint = AimEnd(_shotCursor);
            _shotTurnsBody = BodyMustTurnToward(Flat(_aimPoint - transform.position), feel);
            // A shot that must turn the body starts the hold: she turns to the aim at the shot turn rate, and the
            // upper body and arms cover what the body has not turned yet. A shot while held renews it.
            _shotFacing.ShotStarted(_shotTurnsBody, Time.time, OffTheMark.ShotTurnHold(feel)); // work order 040
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
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, BasicShotRecoveryNow(feel)); // work order 050: attack speed
            Report(new PlayerReport { Kind = PlayerReportKind.ShotStarted, Moving = onTheMove });
        }

        /// <summary>
        /// Starts a skill. A recast (decision 0005) starts the next shot of the chain: no cooldown, and the chain goes on;
        /// any other start ends a chain that runs and begins this skill's own.
        /// </summary>
        void StartSkill(int slot, Vector3 point, bool recast = false)
        {
            SkillDefinition skill = Skills[slot];
            if (skill.Input == SkillInput.KeyMove)
            {
                StartKeyMove(slot);
                return;
            }
            bool light = skill.Lock == SkillLock.Light;
            // A skill moves her over its startup, but the shot after an opening move does not: Backstep hops on its
            // first press only (amended 2026-11-19).
            bool moves = skill.MoveDistance != 0f && !(recast && skill.OpeningMove);
            FeelValues feel = Feel;
            if (moves) LeaveCover(CoverLeaving.MovingSkill); // a skill that moves her takes her out of cover, as the dash does
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
                _shotFacing.ShotStarted(BodyMustTurnToward(Flat(point - transform.position), feel), Time.time, OffTheMark.ShotTurnHold(feel)); // work order 040
            }
            else
            {
                _shotFacing.End(turnBack: false); // a committed skill ends the backpedal at once: it faces the cursor
                // The slide (decision 0004): from the speed she actually has, along the way she actually moves. During a
                // hit stop the agent reads standing, so the last frame in which time ran is used.
                Vector3 velocity = Flat(Time.timeScale > 0f ? _agent.velocity : _lastVelocity);
                float share = feel.RunSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / feel.RunSpeed) : 0f;
                _rising = false;
                // In cover she does not leave her place (an experiment, 2026-11-19): no slide.
                _sliding = !moves && !_inCover && feel.SkillSlowdown > 0f && share > 0.01f;
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
            _chainPressAt = Time.time; // work order 040: the cast bar's chain runs from this press (read only while a chain runs)
            // Snap Shot (work order 017): a Charge skill with no charging is let go at its press, and fires as a perfect release.
            if (skill.Input == SkillInput.Charge && skill.NoCharge) _cast.Release(Time.time);
            _markedAt.Clear();
            _castClean = false;
            _castMoves = moves;
            if (!recast)
            {
                EndChain(); // another skill starting ends a chain (decision 0005)
                _chain.Start(slot, skill.RecastCount, skill.RecastWindow, skill.OpeningMove);
                CooldownFor(slot).Start(Time.time, CooldownOf(slot));
                TakePrime(slot, skill);
                _useKillCut = _useDazeCut = false; // a new use: its kill and daze cuts may come once (work order 033)
            }
            _castOpening = !recast && _chain.IsOpeningMove;
            Report(new PlayerReport { Kind = PlayerReportKind.SkillStarted, Slot = slot });
        }

        /// <summary>
        /// A skill starts (not a recast): if Hair Trigger's priming waits and the skill can use it, the skill spends it for
        /// all of this use (work order 025). One that cannot (Slide, Snap Shot) leaves it for the next.
        /// </summary>
        void TakePrime(int slot, SkillDefinition skill)
        {
            PrimeUse use = FocusModeRules.UseFor(skill);
            bool primed = use != PrimeUse.None && _prime.Spend(Time.time);
            _castPrime = primed ? use : PrimeUse.None;
            _chainPrimed = primed && use == PrimeUse.CleanPress;
            if (!primed) return;
            PrimesUsed++;
            Report(new PlayerReport { Kind = PlayerReportKind.PrimeUsed, Slot = slot, Prime = use });
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

        Cooldown CooldownFor(int slot)
        {
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            Cooldown cooldown = _cooldowns[slot];
            // Charges (work order 015): read live from the skill, so a change in the asset applies at once.
            if (slot < Skills.Count && Skills[slot] != null) cooldown.MaxCharges = Skills[slot].Charges;
            return cooldown;
        }

        /// <summary>
        /// The basic shot goes off along its aim: it hits the first living target its line touches
        /// (<see cref="LineShot.FirstHit"/>), or misses. Either way it fires: the streak runs to the target it hit,
        /// or out to the full range. Cover stops the line (Kristian, 2026-11-19): it hits nothing past the first cover
        /// on its centre line, and a shot that hits nothing before it ends there, its streak too, with a hit spark on
        /// the cover. True if it dealt damage.
        /// </summary>
        bool ResolveBasicShot()
        {
            BasicShotsFired++;
            float peekStagger = TakePeekStagger(); // the quick duck's reward (work order 043): 0 for every other basic shot
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
            float range = BasicShotRange; // the run's Longer barrel (work order 025)
            float stop = CoverRules.LineStop(origin, aim, range, CoverQuery);
            int index = LineShot.FirstHit(origin, aim, range, feel.BasicShotHalfWidth, _targetScratch, stop);
            Damageable hit = index >= 0 ? all[index] : null;

            Vector3 endPoint = hit != null ? hit.transform.position : _aimPoint;
            bool stopped = hit == null && stop < range;
            if (stopped) endPoint = NoteCoverStop(origin, aim, stop);
            Vector3 end = new Vector3(endPoint.x, muzzle.y, endPoint.z);
            SpawnFlash(look, muzzle, end - muzzle);
            SpawnStreak(look, muzzle, end, 1f);
            if (stopped) SpawnCoverHit(look, end);
            // A lying keg on the shot's way, up to where it ended, goes off at once (work order 015).
            if (_kegs.Count > 0)
            {
                float reach = hit != null ? hit.GroundPosition.DistanceTo(origin) : Mathf.Min(stop, range);
                SetOffKegs(origin, aim, range, feel.BasicShotHalfWidth, reach);
            }
            if (hit == null) return ShotFired(false);
            bool empowered = IsEmpowered;
            int applied = Deal(hit, Placeholders.BasicShotDamage + FlatFor(null), out float focus, peekStagger, marked: hit.MarkCount > 0); // work order 050: her flat damage; 065: Marked prey
            if (applied <= 0) return ShotFired(false);
            ReportHit(-1, hit, applied, empowered, focus);
            SpawnHit(look, hit, muzzle);
            BasicShotHits++;
            // Every few hits, whatever the target, the hit that completes the count marks the target it hit; in the
            // focus window every hit does (decision 0005).
            if (_basicMarks.Hit(Kit.BasicShotsPerMark, empowered)) AddMarks(hit, 1, -1);
            BasicShotHit(hit);
            return ShotFired(true);
        }

        /// <summary>
        /// A basic shot hit (work order 017, the one hook for it): each skill she has with a cut for it takes that off its
        /// cooldown (Quick Draw), and during Slide a skill that says so adds its marks (Slide Shot).
        /// </summary>
        void BasicShotHit(Damageable hit)
        {
            float now = Time.time;
            IReadOnlyList<SkillDefinition> skills = Skills;
            for (int slot = 0; slot < skills.Count; slot++)
                if (skills[slot] != null && skills[slot].BasicHitCooldownCut > 0f && IsEquipped(slot))
                    CooldownFor(slot).CutSeconds(now, skills[slot].BasicHitCooldownCut);
            if (_keyMoving && _keyMoveSlot >= 0 && _keyMoveSlot < skills.Count && skills[_keyMoveSlot].SlideShotMarks > 0)
                AddMarks(hit, skills[_keyMoveSlot].SlideShotMarks, _keyMoveSlot);
        }

        /// <summary>
        /// A use of a skill hit something (work order 017, the one hook for a hit, a kill, a daze and a counter): a cut to
        /// a cooldown for the hit (Tied Trigger), half its own cooldown off for a kill (Fresh Shells) or a daze (Follow
        /// Through), and a skill made ready by a counter (Riposte).
        /// </summary>
        void UseLanded(SkillDefinition skill, int slot, bool killed, bool dazed, bool countered)
        {
            float now = Time.time;
            if (skill.HitCooldownCut > 0f)
            {
                int cut = string.IsNullOrEmpty(skill.HitCutsSkill) ? slot : SlotOf(skill.HitCutsSkill);
                if (cut >= 0) CooldownFor(cut).CutSeconds(now, skill.HitCooldownCut);
            }
            // Once a use, however many blasts or hits it has (the second reviews of 2026-11-20, work order 033: Steady Hand's
            // second Scatter Blast with Fresh Shells made it ready again at once).
            if (slot >= 0 && killed && skill.KillCooldownCut > 0f && !_useKillCut)
            {
                _useKillCut = true;
                CooldownFor(slot).CutSeconds(now, CooldownOf(slot) * Mathf.Clamp01(skill.KillCooldownCut));
            }
            if (slot >= 0 && dazed && skill.DazeCooldownCut > 0f && !_useDazeCut)
            {
                _useDazeCut = true;
                CooldownFor(slot).CutSeconds(now, CooldownOf(slot) * Mathf.Clamp01(skill.DazeCooldownCut));
            }
            if (countered && !string.IsNullOrEmpty(skill.CounterReadies))
            {
                int ready = SlotOf(skill.CounterReadies);
                if (ready >= 0) CooldownFor(ready).MakeReady(now);
            }
        }

        /// <summary>
        /// A tagged target died (work order 017, Spreading Tag): the tag of the skill she has that spreads it jumps to the
        /// nearest other living target within its reach, with its time renewed.
        /// </summary>
        void SpreadTag(Damageable dead)
        {
            IReadOnlyList<SkillDefinition> skills = Skills;
            SkillDefinition spreads = null;
            for (int slot = 0; slot < skills.Count && spreads == null; slot++)
                if (skills[slot] != null && skills[slot].TagJump > 0f && skills[slot].TagSeconds > 0f && IsEquipped(slot)) spreads = skills[slot];
            if (spreads == null) return;
            Damageable nearest = null;
            float best = spreads.TagJump;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == dead || !d.IsAlive) continue;
                float distance = d.GroundPosition.DistanceTo(dead.GroundPosition);
                if (distance > best) continue;
                best = distance;
                nearest = d;
            }
            if (nearest == null) return;
            nearest.ApplyTag(spreads.TagSeconds, spreads.TagDamageShare);
            MarkPips.For(nearest, ShotFxMaterial);
            TagsSpread++;
        }

        /// <summary>Puts marks on a target, shows them over it and reports it. <paramref name="slot"/> is -1 for the basic shot.</summary>
        void AddMarks(Damageable target, int marks, int slot)
        {
            if (marks <= 0 || target == null || !target.IsAlive) return;
            // Work order 029 (Marked Target, a piece of the rack between runs): a hit that marks a target with none gives more.
            if (Modifiers != null && Modifiers.Meta.FirstMarkExtra > 0 && target.MarkCount == 0) marks += Modifiers.Meta.FirstMarkExtra;
            KitValues kit = Kit;
            // The run's Lasting marks (work order 025): they stay longer.
            int after = target.AddMarks(marks, kit.MaxMarks, kit.MarkLifetime + (Modifiers != null ? Modifiers.MarkLifetimeBonus : 0f));
            MarkPips pips = MarkPips.For(target, ShotFxMaterial);
            if (pips != null) pips.Max = kit.MaxMarks;
            Report(new PlayerReport { Kind = PlayerReportKind.MarkAdded, Slot = slot, Target = target, Marks = marks, MarksAfter = after });
        }

        /// <summary>
        /// The marks a builder's hit adds to a target: the skill's, or for a clean tap the clean tap's (a clean spend
        /// takes the marks and never comes here). A skill with a
        /// mark interval marks a target at most that often in one use (Fan Fire: once a second, not every tick).
        /// </summary>
        void MarkFromSkill(SkillDefinition skill, Damageable target, int primedExtra = 0)
        {
            // Primed (work order 025): a mark more than it would add, on each target it hits; a skill that adds none adds one.
            int marks = (_castClean ? skill.CleanTapMarks : skill.MarksAdded) + Mathf.Max(0, primedExtra);
            if (marks <= 0) return;
            // A run's upgrade (work order 012): more marks from this skill's hit on a target that has none.
            if (Modifiers != null && target.MarkCount == 0) marks += Modifiers.FirstMarkBonus(skill.Name);
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

        /// <summary>Reports damage she dealt: by a skill's slot, or -1 for the basic shot; and whether the hit countered (work order 019).</summary>
        void ReportHit(int slot, Damageable target, int damage, bool empowered, float focus, bool countered = false) =>
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.Hit, Slot = slot, Target = target, Damage = damage, Empowered = empowered, Focus = focus, Countered = countered,
                Crit = _dealtCrit, // work order 050: of the Deal just before
            });

        // ---------------- Her stats in a run (work order 050, decision 0012) ----------------

        /// <summary>The random stream crits come from: seeded by the run's seed when a run starts, and by tests (<see cref="SeedCrits"/>).</summary>
        LevelRandom _crits = LevelRandom.Stream(0UL, CritStage);
        bool _dealtCrit;
        public const string CritStage = "crit";

        /// <summary>The crits' stream starts again from this seed (a run's start, and tests).</summary>
        public void SeedCrits(ulong seed) => _crits = LevelRandom.Stream(seed, CritStage);
        /// <summary>Hits of hers that were crits, rolled or sure (a clean press, a perfect release).</summary>
        public int Crits { get; private set; }

        /// <summary>Her stats in the run (<see cref="HerStats"/>), or null outside a run.</summary>
        public HerStats Stats => Modifiers != null ? Modifiers.Stats : null;
        /// <summary>True in a run with the stats and tiers on: flat damage, attack speed and crits are read.</summary>
        bool StatsOn => Modifiers != null && Modifiers.StatsAndTiers;
        /// <summary>What her flat damage adds to a hit of this skill (null: the basic shot); 0 outside a run or with the switch off.</summary>
        int FlatFor(SkillDefinition skill) => StatsOn ? Modifiers.Stats.FlatFor(skill) : 0;
        /// <summary>Work order 065: her Clean press, Marked prey and Long focus (<see cref="HerStats"/>); 1 outside a run or with the switch off.</summary>
        float CleanPressNow => StatsOn ? Modifiers.Stats.CleanPress : 1f;
        float MarkedPreyNow => StatsOn ? Modifiers.Stats.MarkedPrey : 1f;
        float LongFocusNow => StatsOn ? Modifiers.Stats.LongFocus : 1f;
        /// <summary>The basic shot's recovery now: the feel's, or with the stats on shortened or lengthened by her attack speed.</summary>
        float BasicShotRecoveryNow(FeelValues f) =>
            Modifiers != null ? Modifiers.Stats.BasicShotRecovery(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery) : f.BasicShotRecovery;
        /// <summary>The time from one basic shot to the next now (work order 050): the feel's 0.225 s, or with the stats on her attack speed's.</summary>
        public float BasicShotInterval
        {
            get
            {
                FeelValues f = Feel;
                return f.BasicShotStartup + f.BasicShotActive + BasicShotRecoveryNow(f);
            }
        }

        /// <summary>
        /// Deals damage to a target: times the empowered multiplier while empowered (1 since decision 0005), and what
        /// was dealt trickles into the focus gauge. Returns the amount actually removed, and the focus gained.
        /// <paramref name="stagger"/> is what the hit takes off a stagger bar (work order 008); the basic shot's is 0.
        /// Work order 050: in a run with the stats on, the hit may crit (her crit chance, on the crits' stream), and one
        /// that is <paramref name="sureCrit"/> (a clean press, a perfect release) always does without a roll.
        /// </summary>
        int Deal(Damageable target, int damage, out float focus, float stagger = 0f, bool sureCrit = false, bool foldedCrit = false, bool marked = false)
        {
            IdentityValues identity = Identity;
            _dealtCrit = false;
            // Work order 065: her Marked prey on a target that carried marks when the hit came (1 off).
            if (marked && damage > 0 && StatsOn) damage = Mathf.RoundToInt(damage * MarkedPreyNow);
            if (StatsOn && damage > 0 && (sureCrit || CritRules.Rolls(_crits, Modifiers.Stats.CritChance)))
            {
                _dealtCrit = true;
                Crits++;
                // A clean spend's crit is folded (work order 055): at her start it deals what it dealt before the stats.
                damage = foldedCrit && sureCrit ? CritRules.ApplyFolded(damage, Modifiers.Stats.CritDamage, Modifiers.StatValues)
                                                : CritRules.Apply(damage, Modifiers.Stats.CritDamage);
            }
            if (Modifiers != null) damage = Modifiers.ScaleDamage(damage); // a run's upgrades (work order 012)
            damage = target.ScaleForTag(damage); // Tag Round's tag (work order 015)
            bool tagged = target.IsTagged;
            if (Modifiers != null) stagger *= Modifiers.StaggerShare; // the run's Heavy hits (work order 025)
            int applied = target.TakeHit(_focus.Scale(damage, Time.time, identity), stagger);
            focus = applied > 0 ? GainFocus(applied * identity.FocusPerDamage) : 0f; // through the run's Keen focus (work order 025)
            if (tagged && !target.IsAlive) SpreadTag(target); // work order 017
            return applied;
        }

        /// <summary>
        /// One hit of a skill: every living target its area touches takes the damage. True if any did. Cover takes its
        /// share away first (<see cref="SkillArea"/>, Kristian, 2026-11-19): a line ends at the first cover, a cone and
        /// a circle hit only what they see, and a circle lands short of cover in its way. A target cover saves is not
        /// hit at all: no damage, stagger, marks or focus.
        /// </summary>
        bool ResolveSkillHit(SkillDefinition skill, float charge)
        {
            SkillShotsFired++;
            GroundPoint origin = GroundPosition;
            Vector3 flatAim = Flat(_aimPoint - transform.position);
            // With nothing to aim at (the cursor on the character), a line or cone goes where the character faces.
            if (flatAim.sqrMagnitude < 1e-6f && skill.Shape != SkillShape.Circle) flatAim = Flat(transform.forward);
            var aim = new GroundPoint(origin.X + flatAim.x, origin.Z + flatAim.z);
            var area = new SkillArea(skill, origin, aim, CoverQuery);

            // Long Shot's perfect band (work order 015): let go by the key inside it, the shot deals the perfect scale
            // times the full-charge damage and stagger, gains focus as a clean tap does, and is drawn that much wider.
            // Snap Shot (work order 017): a skill with no charging is always a perfect release.
            // Primed by Hair Trigger (work order 025): let go by the key at half a charge or more is perfect too.
            bool perfect = skill.Input == SkillInput.Charge
                           && (skill.NoCharge || (_cast.ReleasedByKey && (PoolRules.IsPerfect(skill, _cast.ChargedSeconds(Time.time))
                                                                          || (_castPrime == PrimeUse.PerfectRelease && FocusModeRules.PrimedPerfect(charge, FocusModeValues)))));
            Look look = LookFor(skill, perfect ? 1f : charge);
            if (perfect) look.Width *= Mathf.Max(1f, skill.PerfectScale);
            // A keg (work order 015) lands and lies on its fuse: nothing is hit yet.
            if (skill.Fuse > 0f && skill.Shape == SkillShape.Circle)
            {
                PlaceKeg(skill, area, look);
                Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = false, Charge = charge });
                return false;
            }
            Vector3 shotPoint = SpawnSkillFx(skill, look, area);
            PlaySound(look); // once per hit of a burst, per tick of a hold, per landing

            int damage = perfect ? PoolRules.PerfectDamage(skill) : SkillRules.Damage(skill, charge);
            // Work order 050 (decision 0012): in a run with the stats on, her flat damage by the skill's weight is added, and
            // a perfect release and a clean press are crits (sureCrit): what their own scale gave beyond a crit at her start
            // stays (CritRules.Folded), the crit itself is dealt in Deal. A clean spend (Backstep) has no scale of its own:
            // its crit is folded instead (CritRules.ApplyFolded; the fourth review of 2026-11-21, work order 055: it was
            // paid 1.5 on top). Off, exactly as before.
            bool sureCrit = StatsOn && (perfect || _castClean);
            bool foldedCrit = sureCrit && !perfect && _castClean && MarkRules.HasCleanSpend(skill);
            if (StatsOn)
                damage = perfect ? Mathf.RoundToInt((SkillRules.Damage(skill, 1f) + FlatFor(skill)) * Mathf.Max(0f, skill.PerfectScale) * CritRules.Folded(1f, Modifiers.StatValues) * CleanPressNow) // work order 065
                                 : damage + FlatFor(skill);
            float stagger = perfect ? skill.Stagger * Mathf.Max(0f, skill.PerfectScale) : skill.Stagger;
            // A clean tap (decision 0005) hits harder by itself; a clean spend (Backstep, amended 2026-11-19) instead
            // takes the marks, as a spender does, for its own bonus per mark.
            // Work order 065: her Clean press (1 off) times whatever the clean bonus is, here where it enters the damage; a clean
            // spend (Backstep's) has no bonus of its own here, so its hit takes the stat on its own.
            if (MarkRules.IsCleanTap(skill, _castClean)) damage = Mathf.RoundToInt(damage * (StatsOn ? CritRules.Folded(Kit.CleanDamage, Modifiers.StatValues) * CleanPressNow : Kit.CleanDamage));
            else if (StatsOn && _castClean && !perfect) damage = Mathf.RoundToInt(damage * CleanPressNow);
            if (perfect)
            {
                PerfectReleases++;
                GainFocus(Identity.FocusPerCleanTap);
            }
            bool landed = HitArea(skill, _castSlot, area, look, shotPoint, damage, stagger, _castClean, out Damageable first, _castPrime, sureCrit, foldedCrit);

            // Tag Round (work order 015): the first target it hit is tagged; a tag on a tagged target starts it again.
            if (skill.TagSeconds > 0f && first != null && first.IsAlive)
            {
                first.ApplyTag(skill.TagSeconds, skill.TagDamageShare);
                MarkPips.For(first, ShotFxMaterial); // its sign shows beside the marks
            }
            // A line goes through every lying keg on it, and sets each off (work order 015).
            if (skill.Shape == SkillShape.Line && _kegs.Count > 0)
                SetOffKegs(area.Origin, area.Aim, skill.Range, skill.HalfWidth, area.Stop);
            // Held: the seconds charged, so the timing feedback can tell a release before the band from one after it (work order 019).
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = landed, Charge = charge, Clean = perfect,
                Held = _cast.ChargedSeconds(Time.time),
            });
            return landed;
        }

        /// <summary>
        /// Every living target the area touches takes the hit: damage, stagger, marks or the spending of them, a counter.
        /// True if any took damage. <paramref name="first"/> is the target nearest the area's origin that took damage.
        /// </summary>
        bool HitArea(SkillDefinition skill, int slot, SkillArea area, Look look, Vector3 shotPoint, int damage, float stagger,
                     bool clean, out Damageable first, PrimeUse prime = PrimeUse.None, bool sureCrit = false, bool foldedCrit = false)
        {
            first = null;
            float firstDistance = float.PositiveInfinity;
            bool spender = MarkRules.Spends(skill, clean);
            float bonusPerMark = MarkRules.BonusPerMark(skill, clean);
            bool landed = false, countered = false, killed = false, dazed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!d.IsAlive) continue;
                if (!area.Hits(d.GroundPosition, d.Radius)) continue;
                bool empowered = IsEmpowered;
                // Point Blank (work order 017): a spender with a close range spends, at its scaled bonus, only on a target
                // within it; one farther away takes the hit's own damage and keeps its marks.
                bool spends = spender && (skill.CloseRange <= 0f || d.GroundPosition.DistanceTo(area.Origin) - d.Radius <= skill.CloseRange);
                float bonus = skill.CloseRange > 0f ? bonusPerMark * skill.CloseMarkScale : bonusPerMark;
                // A spender takes every mark off the target and deals its bonus per mark on this hit (decision 0005);
                // Breaker Slug's bonus per mark is stagger, not damage (work order 015).
                bool marked = d.MarkCount > 0; // work order 065: before a spender takes them
                int taken = spends ? d.TakeMarks() : 0;
                int had = taken; // the marks it really had: only these give focus (the second reviews of 2026-11-20, work order 033)
                // Primed (work order 025): a spender counts a mark more than the target has, up to the cap.
                if (spends && prime == PrimeUse.SpenderExtraMark) taken = FocusModeRules.SpenderCount(taken, FocusModeValues.PrimedExtraMarks, Kit.MaxMarks);
                int dealt = spends ? MarkRules.SpenderDamage(damage, taken, bonus) : damage;
                // Execution (work order 017): more to a dazed target.
                if (skill.DazedDamageScale != 1f && d.IsDazed) dealt = Mathf.RoundToInt(dealt * Mathf.Max(0f, skill.DazedDamageScale));
                float hitStagger = skill.MarkStaggerBonus > 0f ? PoolRules.SpenderStagger(stagger, taken, skill.MarkStaggerBonus) : stagger;
                // Stopping Shot (work order 015): a hit on an enemy winding up a counterable attack, from its front. Tried
                // before the hit's stagger is dealt (the review of 2026-11-20, work order 026): a hit that empties a bar
                // dazes, the daze drops the windup, and the counter would then find nothing to counter.
                bool counteredThis = skill.CounterDaze > 0f && d.TryGetComponent(out Enemy enemy)
                                     && enemy.TryCounter(transform.position, skill.CounterHalfAngle, skill.CounterDaze);
                if (counteredThis) countered = true;
                bool wasDazed = d.IsDazed;
                int applied = Deal(d, dealt, out float focus, hitStagger, sureCrit, foldedCrit, marked); // work order 050: a clean press's or a perfect release's hit is a crit
                if (!d.IsAlive) killed = true;
                else if (!wasDazed && d.IsDazed) dazed = true;
                if (spends)
                {
                    // The primed extra mark counts for the damage (or Breaker Slug's stagger) only: a primed spender must
                    // not pay for its own part with focus (work order 033).
                    float markFocus = GainFocus(had * Identity.FocusPerMarkSpent);
                    Report(new PlayerReport
                    {
                        Kind = PlayerReportKind.MarksSpent, Slot = slot, Target = d, Marks = taken, Damage = applied, Focus = markFocus,
                    });
                }
                if (applied <= 0) continue;
                ReportHit(slot, d, applied, empowered, focus, counteredThis);
                SpawnHit(look, d, shotPoint);
                SkillShotHits++;
                landed = true;
                if (!spender) MarkFromSkill(skill, d, prime == PrimeUse.ExtraMark ? FocusModeValues.PrimedExtraMarks : 0);
                float distance = d.GroundPosition.DistanceTo(area.Origin);
                if (distance < firstDistance)
                {
                    firstDistance = distance;
                    first = d;
                }
            }
            if (countered)
            {
                Counters++;
                GainFocus(Identity.MaxFocus * Mathf.Clamp01(skill.CounterFocus));
            }
            if (landed || countered) UseLanded(skill, slot, killed, dazed, countered);
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
                : (ShotLooks != null ? ShotLooks.For(skill.Name) : null)
                  // Work order 024: a pool skill the asset has no look for takes its own from the defaults in memory; only
                  // with the effects switched off does it borrow its nearest's, sound and all, as before (work order 015).
                  ?? (CombatEffects.PoolLooksBorrowed && ShotLooks != null ? ShotLooks.For(ShotFxDefaults.Nearest(skill.Name)) : null)
                  ?? FallbackLook(skill.Name);
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
            bool borrowed = CombatEffects.PoolLooksBorrowed;
            Dictionary<string, ShotLook> looks = borrowed ? _borrowedLooks : _fallbackLooks;
            if (!looks.TryGetValue(skillName, out ShotLook look))
                looks[skillName] = look = borrowed ? ShotFxDefaults.Borrowed(skillName) : ShotFxDefaults.For(skillName);
            // Its own look keeps the sound of its nearest in the asset until the asset has a look of its own for it (work order 024).
            ShotLook nearest = !borrowed && ShotLooks != null ? ShotLooks.For(ShotFxDefaults.Nearest(skillName)) : null;
            if (nearest != null) ShotFxDefaults.TakeSound(look, nearest);
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
        Vector3 SpawnSkillFx(SkillDefinition skill, Look look, SkillArea area)
        {
            GroundPoint origin = area.Origin, aim = area.Aim;
            Vector3 dir = new Vector3(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

            if (skill.Shape == SkillShape.Circle)
            {
                // The area hit is a thin ring at the circle's edge. The bang is a big burst over the middle,
                // drawn in front of whatever stands in it, and a shockwave that runs out to the edge. It is where the
                // circle landed: short of cover in its way (Kristian, 2026-11-19).
                GroundPoint c = area.Centre;
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
                    // A streak that meets cover ends at it, as the hit does (Kristian, 2026-11-19).
                    Vector3 rayDir = Quaternion.Euler(0f, yaw, 0f) * dir;
                    float reach = skill.Range;
                    if (CoverQuery != null) reach = Cover.ClearDistance(level, rayDir, reach, Cover.RayHeight);
                    SpawnStreak(look, muzzle, level + rayDir * reach, 1f);
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

            // A line pierces everything in its strip, so its streaks run its whole range, or to the cover that stopped
            // it, with a hit spark there (Kristian, 2026-11-19). A wide one sends several side by side, spread evenly
            // across its width, all ending where its centre line ended.
            float width = skill.HalfWidth * 2f;
            int count = StreaksAcross(width);
            Vector3 across = new Vector3(dir.z, 0f, -dir.x);
            float length = area.Stop;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = across * ((i + 0.5f) / count - 0.5f) * width;
                SpawnStreak(look, muzzle + offset, level + dir * length + offset, 1f);
            }
            if (area.Stopped)
            {
                Vector3 stop = NoteCoverStop(origin, aim, length);
                SpawnCoverHit(look, new Vector3(stop.x, muzzle.y, stop.z));
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
                                                                feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel)) // work order 040
                                        : 1f);
            if (_rising) share = Mathf.Min(share, SkillSlide.RiseShare(_riseFrom, now - _riseStartedAt, OffTheMark.SkillSpeedRecovery(feel, _riseOfChain)));
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

        /// <summary>Counts a line shot that cover stopped and returns where, on the ground, <paramref name="stop"/> metres along it.</summary>
        Vector3 NoteCoverStop(GroundPoint origin, GroundPoint aim, float stop)
        {
            GroundPoint dir = LineShot.Direction(origin, aim, new GroundPoint(transform.forward.x, transform.forward.z), 1e-4f);
            GroundPoint at = origin + dir * stop;
            ShotsStoppedByCover++;
            LastCoverStop = new Vector3(at.X, 0f, at.Z);
            return LastCoverStop;
        }

        /// <summary>The hit spark, on the cover that stopped a line shot (Kristian, 2026-11-19): a blocked shot reads as blocked.</summary>
        void SpawnCoverHit(Look look, Vector3 at)
        {
            ShotFx.Spark(at, look.Values.HitSize * look.Scale, look.Values.HitSeconds, look.Paint, ShotFxMaterial, 0.2f);
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
