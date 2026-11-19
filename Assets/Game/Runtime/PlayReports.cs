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
    }

    public enum SkillEnding { RanOut, CancelledByDash, Cancelled }

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
    }

    public enum EnemyReportKind { Woke, AttackStarted, Struck, GaveUp, Died }

    public enum StrikeOutcome { Landed, OutOfArea, AvoidedByDash, NoEffect }

    /// <summary>
    /// Something the enemy did, as <see cref="EnemyBrute.Reported"/> tells it. For the play session recorder; nothing
    /// in the game listens. Attack is the attack's name for AttackStarted and Struck.
    /// </summary>
    public struct EnemyReport
    {
        public EnemyReportKind Kind;
        public string Attack;
        public StrikeOutcome Outcome;
        public int Damage;
    }
}
