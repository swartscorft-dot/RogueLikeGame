using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>What a <see cref="PlayerReport"/> tells.</summary>
    public enum PlayerReportKind
    {
        SkillPressed,   // Slot, Result, CooldownLeft (when the cooldown rejected it)
        SkillReleased,  // Slot
        SkillStarted,   // Slot
        SkillFired,     // Slot, Landed, Charge: one hit of the skill went off; Clean, Held, At (work orders 015 and 019, below)
        SkillEnded,     // Slot, Ending, Held, Charge, Fired
        BufferDropped,  // Slot (-1 for a dash): a buffered press ran out before it could start
        ShotClicked,    // Result: Started, or Buffered for a click that waits
        ShotDropped,    // a waiting click was dropped by a dash or a skill
        ShotStarted,    // Moving
        ShotFired,      // Landed
        DashPressed,    // Result, CooldownLeft (when the cooldown rejected it), AlreadyDashing
        DashStarted,
        Hit,            // Slot (-1 for the basic shot), Target, Damage, Empowered, Focus (gained from the damage), Countered
        FocusPressed,   // Result
        // Decision 0005, the kit:
        MarkAdded,      // Slot (-1 for the basic shot), Target, Marks (added), MarksAfter
        MarksSpent,     // Slot, Target, Marks (taken), Damage (of that hit), Focus (gained from the marks)
        RecastPressed,  // Slot, Shot (which shot of the chain: 2, 3), Clean, Focus (gained from a clean tap), Late, Timing and Seconds (work order 069)
        ChainEnded,     // Slot, Shot (shots it fired), Early (short of its count)
        CooldownsCut,   // Cuts (seconds cut per skill slot), DashCut
        // Taking cover (Kristian 2026-11-19, an experiment):
        CoverEntered,   // Tall: whether what she took cover behind is tall cover
        CoverLeft,      // Leaving: why
        // The focus choice (work order 025, 2026-11-20). FocusPressed says the way the gauge worked (FocusMode).
        PrimeUsed,      // Slot, Prime: a skill started primed by Hair Trigger, and what it gained
        PrimeLapsed,    // a priming ran out with no skill to use it
        // Melee you read (work order 035, 2026-11-20): two things that happen to her.
        Stunned,        // Seconds, By: a stun began
        Pulled,         // Seconds, By: a pull began
        // The quick duck (work order 043, Kristian's answer of 2026-11-20 to the cover report).
        Ducked,         // By (what she ducked beside): a tap ducked her
        DuckEnded,      // DuckEnd (how), Shot (what it stopped)
        DuckNothing,    // a tap with no low prop in reach: nothing happened
        DuckStopped,    // At: the duck stopped a thing that flew at her there
        // The sprint (work order 092). Its stagger is a Stunned with By SprintRules.StaggerName.
        SprintStarted,  // a sprint began
        SprintEnded,    // Seconds (how long it ran, its jumps in it), By (why: released, stopped, dash, duck, held, other)
        SprintJumped,   // By (the prop), Metres (from the take-off to the landing): she jumped a low prop
        SprintRefused,  // Slot (-1 for the basic shot): a click or a skill key while sprinting did nothing
        // The three new skills (work order 109). Deadeye refused is a SkillPressed, Rejected, with NoTarget.
        RicochetJumped, // Slot, Target (the one it jumped to), Shot (which hit of the use: 2, 3)
        SmokeLanded,    // Slot, At (the cloud's middle), Seconds (how long it stands)
        DeadeyeFired,   // Slot, Shot (how many marked enemies it shot at)
    }

    public enum SkillEnding { RanOut, CancelledByDash, Cancelled }

    /// <summary>Why she left cover: the key went up, the dash, a skill that moves her, or anything else (the switch, the thing gone, a teleport, death).</summary>
    public enum CoverLeaving { Released, Dash, MovingSkill, Other }

    /// <summary>
    /// Something the player character did, as <see cref="PlayerCharacter.Reported"/> tells it: what became of a press,
    /// a skill or shot that started or went off, a hit. For the play session recorder; nothing in the game listens.
    /// Which fields are set depends on the kind.
    /// </summary>
    public struct PlayerReport
    {
        public PlayerReportKind Kind;
        public int Slot;
        public IntentResult Result;
        public float CooldownLeft;
        public bool AlreadyDashing;
        public bool Landed;
        public bool Moving;
        public SkillEnding Ending;
        public float Held;
        public float Charge;
        public bool Fired;
        public Damageable Target;
        public int Damage;
        public bool Empowered;
        public bool NotEquipped;   // SkillPressed: rejected because the slot holds no skill yet
        public int Marks;
        public int MarksAfter;
        public int Shot;
        public bool Clean;
        public bool Early;
        public float Focus;
        public float[] Cuts;
        public float DashCut;
        public bool Tall;             // CoverEntered
        public CoverLeaving Leaving;  // CoverLeft
        // Work order 019 (2026-11-20), for the timing feedback (TimingFeedback). SkillFired's Clean is a perfect release,
        // or for a keg's burst a keg set off by a shot (work order 015); its Held is the seconds the skill had charged
        // when it went off (a Charge skill; 0 for others), and a keg's burst says where the keg lay in At.
        public bool Countered;        // Hit: this hit countered the enemy's attack (Stopping Shot)
        public GroundPoint At;        // SkillFired of a keg's burst: where it lay
        // Work order 025.
        public FocusMode FocusMode;   // FocusPressed: the way the gauge worked when it was pressed
        public PrimeUse Prime;        // PrimeUsed: what the skill gained from the priming
        // Work order 035.
        public float Seconds;         // Stunned, Pulled: how long it holds her
        public string By;             // Stunned, Pulled: the attack that did it
        // Work order 040.
        public bool Late;             // RecastPressed: not clean, and after the clean part of its window (it comes early now)
        // Work order 043.
        public DuckEnd DuckEnd;       // DuckEnded: how the duck ended
        // Work order 050.
        public bool Crit;             // Hit: the hit was a crit (rolled, or a clean press's or a perfect release's)
        // Work order 069 (and Seconds above): RecastPressed's timing by its own window's clean part (a press before the last
        // in its clean part is Clean here, in rhythm, though not a clean tap), and Seconds its window's clock at the press.
        public RecastTiming Timing;
        // Work order 092.
        public float Metres;          // SprintJumped: from the take-off to the landing
        // Work order 109.
        public bool NoTarget;         // SkillPressed: rejected because Deadeye had no marked enemy in sight
    }

    /// <summary>
    /// What an enemy reports. Removed: it was taken out of the game alive (its level or the practice spawns went), and
    /// counts as giving up. StaggerTaken, Dazed and DazeEnded come from its stagger bar (work order 008). The boss's
    /// (work order 014): Charged, its charge began to run (Attack names it); CalledPack, it called a pack (Count how many
    /// came); Angered, it turned angry. The caster's (work order 027; its casts are AttackStarted and each circle's burst a
    /// Struck, as every enemy's): CirclePlaced, a circle of its cast was placed on the ground (Attack names the cast, Count
    /// which circle, from 1); CircleOffGround, one was not, its place being off the walkable ground; CircleFaded, one of a
    /// countered Grave Rain faded without bursting; Countered, Stopping Shot dropped its cast. Things that fly (work order
    /// 036): each one's end is a Struck with Flown set; that they left is told by <see cref="ThingsThatFly.Loosed"/>.
    /// The bomber's (work order 073): its fuse lit is an AttackStarted of its Blast (Count how: 0 at her, 1 dropped dead, 2
    /// set off by another's blast, <see cref="Roguelike.Rules.FuseCause"/>), and its blast on her a Struck, as every enemy's;
    /// Blasted, its blast went off (Count the enemies it hurt, Killed those of them it killed, Damage what it dealt them).
    /// The warder's (work order 079): WardRaising, a ward's raising began (its telegraph); WardRaised, the ward is on;
    /// WardBroken, a ward that was on ended (Count why, <see cref="Roguelike.Rules.WardEnd"/>; Damage what it took off hits).
    /// </summary>
    public enum EnemyReportKind { Woke, AttackStarted, Struck, GaveUp, Died, Removed, StaggerTaken, Dazed, DazeEnded, Charged, CalledPack, Angered,
                                  CirclePlaced, CircleOffGround, CircleFaded, Countered, Blasted, WardRaising, WardRaised, WardBroken }

    /// <summary>
    /// How a strike went. StoppedByCover: the shooter's shot was stopped by what she was in cover behind (an experiment,
    /// 2026-11-19). StoppedByWall: a thing that flew was stopped by a wall or a prop (work order 036).
    /// </summary>
    public enum StrikeOutcome { Landed, OutOfArea, AvoidedByDash, NoEffect, StoppedByCover, StoppedByWall }

    /// <summary>
    /// Something an enemy did, as <see cref="Enemy.Reported"/> tells it. For the play session recorder; nothing
    /// in the game listens. Attack is the attack's name for AttackStarted and Struck. Since work order 008 every report
    /// says which enemy it is (<see cref="Id"/>, unique in the run of the game) and its kind; Stagger and StaggerLeft
    /// are the points taken off its bar and the points left, for StaggerTaken.
    /// </summary>
    public struct EnemyReport
    {
        public EnemyReportKind Kind;
        public string Attack;
        public StrikeOutcome Outcome;
        public int Damage;
        public int Id;
        public EnemyKind EnemyKind;
        public float Stagger;
        public float StaggerLeft;
        public int Count;             // CalledPack: the enemies that came (work order 014); the caster's circle reports: which circle (work order 027)
        public bool Elite;            // the elite of an elite room (work order 017): the log names it "elite brute"
        public bool Flown;            // Struck: the end of a thing that flew (work order 036); it was heard as it left, not here
        public int Killed;            // Blasted: the enemies a bomber's blast killed (work order 073)
    }
}
