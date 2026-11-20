using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>What a <see cref="PlayerReport"/> tells.</summary>
    public enum PlayerReportKind
    {
        SkillPressed,   // Slot, Result, CooldownLeft (when the cooldown rejected it)
        SkillReleased,  // Slot
        SkillStarted,   // Slot
        SkillFired,     // Slot, Landed, Charge: one hit of the skill went off
        SkillEnded,     // Slot, Ending, Held, Charge, Fired
        BufferDropped,  // Slot (-1 for a dash): a buffered press ran out before it could start
        ShotClicked,    // Result: Started, or Buffered for a click that waits
        ShotDropped,    // a waiting click was dropped by a dash or a skill
        ShotStarted,    // Moving
        ShotFired,      // Landed
        DashPressed,    // Result, CooldownLeft (when the cooldown rejected it), AlreadyDashing
        DashStarted,
        Hit,            // Slot (-1 for the basic shot), Target, Damage, Empowered, Focus (gained from the damage)
        FocusPressed,   // Result
        // Decision 0005, the kit:
        MarkAdded,      // Slot (-1 for the basic shot), Target, Marks (added), MarksAfter
        MarksSpent,     // Slot, Target, Marks (taken), Damage (of that hit), Focus (gained from the marks)
        RecastPressed,  // Slot, Shot (which shot of the chain: 2, 3), Clean, Focus (gained from a clean tap)
        ChainEnded,     // Slot, Shot (shots it fired), Early (short of its count)
        CooldownsCut,   // Cuts (seconds cut per skill slot), DashCut
        // Taking cover (Kristian 2026-11-19, an experiment):
        CoverEntered,   // Tall: whether what she took cover behind is tall cover
        CoverLeft,      // Leaving: why
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
    }

    /// <summary>
    /// What an enemy reports. Removed: it was taken out of the game alive (its level or the practice spawns went), and
    /// counts as giving up. StaggerTaken, Dazed and DazeEnded come from its stagger bar (work order 008). The boss's
    /// (work order 014): Charged, its charge began to run (Attack names it); CalledPack, it called a pack (Count how many
    /// came); Angered, it turned angry.
    /// </summary>
    public enum EnemyReportKind { Woke, AttackStarted, Struck, GaveUp, Died, Removed, StaggerTaken, Dazed, DazeEnded, Charged, CalledPack, Angered }

    /// <summary>How a strike went. StoppedByCover: the shooter's shot was stopped by what she was in cover behind (an experiment, 2026-11-19).</summary>
    public enum StrikeOutcome { Landed, OutOfArea, AvoidedByDash, NoEffect, StoppedByCover }

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
        public int Count;             // CalledPack: the enemies that came (work order 014)
    }
}
