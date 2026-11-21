using System;

namespace Roguelike.Rules
{
    /// <summary>How a skill is played with its key.</summary>
    public enum SkillInput
    {
        /// <summary>Press: it fires after its startup.</summary>
        Normal,
        /// <summary>Press: it lands on the ground point under the cursor, no farther away than its range.</summary>
        Point,
        /// <summary>Hold: it hits again and again while the key is down, up to a longest time.</summary>
        Holding,
        /// <summary>Hold to charge, release to fire. A longer charge hits harder.</summary>
        Charge,
        /// <summary>Press: a long cast with a bar, then one heavy hit.</summary>
        Casting,
        /// <summary>
        /// Press: she moves MoveDistance along the move keys (the way she faces with none held) over Startup, on the NavMesh,
        /// and hits nothing (work order 015, 2026-11-20: Slide). It is not a skill in use: she can shoot basic shots
        /// through it and they do not slow it. Not the slowdown of decision 0004 (<see cref="SkillSlide"/>).
        /// </summary>
        KeyMove,
    }

    /// <summary>The area a skill hits.</summary>
    public enum SkillShape
    {
        /// <summary>A straight strip from the character toward the aim: Range long, 2 * HalfWidth wide.</summary>
        Line,
        /// <summary>A circle of Radius around the aimed ground point, placed at most Range away.</summary>
        Circle,
        /// <summary>A wedge from the character toward the aim: Range long, Angle degrees wide.</summary>
        Cone,
    }

    /// <summary>
    /// One skill, as numbers. The instances live in the skill settings asset, where Kristian tunes them.
    /// Which fields a skill uses depends on its input type and shape; the rest are ignored.
    /// </summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public string Name = "Skill";
        public SkillInput Input = SkillInput.Normal;
        public SkillShape Shape = SkillShape.Line;

        public float Cooldown = 4f;        // s, counted from the moment the skill starts
        public int Damage = 100;           // per hit, per target. Charge: at full charge

        // Timing
        public float Startup = 0.15f;      // s before the first hit. Casting: the cast time. Charge: from release to the shot
        public float Recovery = 0.3f;      // s locked after the last hit
        public int HitCount = 1;           // hits in a burst, for every input type but Holding
        public float HitInterval = 0.1f;   // s between the hits of a burst

        // Area
        public float Range = 12f;          // m
        public float HalfWidth = 0.3f;     // m, Line
        public float Radius = 2.5f;        // m, Circle
        public float Angle = 50f;          // degrees, Cone

        // Movement
        public float MoveDistance = 0f;    // m the character moves during the startup: forward along the aim, or backward if negative

        // Holding and Charge
        public float HoldMax = 2f;         // s the key can be held before the skill ends (Holding) or fires by itself (Charge)
        public float TickInterval = 0.15f; // s between hits, Holding
        public float ChargeTime = 1f;      // s to full charge, Charge
        public float MinChargeDamage = 0.3f; // share of Damage dealt with no charge at all, Charge

        // The kit (decision 0005, 2026-11-19)
        public bool StartEquipped = false; // on the bar from the start of a run; the others are filled in later
        public SkillLock Lock = SkillLock.Committed;
        public int MarksAdded = 0;         // builder: marks added per hit, on each target it hits
        public float MarkInterval = 0f;    // s: a target gains marks from one use at most this often. 0: every hit
        public float MarkDamageBonus = 0f; // spender, above 0: takes all marks off each target it hits, this much more damage per mark
        public int RecastCount = 1;        // presses of the key, one shot each. 1: no recast
        public float RecastWindow = 0.6f;  // s after a shot has gone off in which the next press fires the next shot
        public int CleanTapMarks = 0;      // marks the last press adds when it is clean. 0: no clean tap
        // Amended 2026-11-19 after Kristian played: a second, harder way to spend marks (Backstep).
        public bool OpeningMove = false;   // recast: the first press only moves her (MoveDistance, over the startup) and fires nothing; the window opens when the move ends
        public float CleanMarkDamageBonus = 0f; // above 0: a clean last press takes all marks off each target it hits, this much more damage per mark, instead of a clean tap
        // Work order 008: what it takes off a stagger bar (StaggerBar), per hit, on each target it hits. The basic shot takes none.
        public float Stagger = 0f;

        // Work order 015 (2026-11-20): the pool of eight. All first guesses.
        public int Charges = 1;            // uses that can be stored; each recharges in Cooldown, one at a time (Slide: 2)
        public float PerfectFrom = 0f;     // s after a charge began: from here to PerfectTo a release is perfect (Long Shot). Both 0: no band
        public float PerfectTo = 0f;
        public float PerfectScale = 2f;    // a perfect release deals this times the full-charge damage and stagger
        public float MarkStaggerBonus = 0f; // spender of stagger, above 0: takes all marks off each target it hits, this much more stagger per mark (Breaker Slug)
        public float TagSeconds = 0f;      // above 0: the first target it hits is tagged this long (Tag Round); a new tag starts it again
        public float TagDamageShare = 0f;  // a tagged target takes this share more damage from everything she does
        public float CounterDaze = 0f;     // above 0: a counter (Stopping Shot). A hit on an enemy winding up a counterable attack, from its front, drops it and dazes it this long
        public float CounterHalfAngle = 60f; // degrees either side of where the enemy faces that count as its front
        public float CounterFocus = 0f;    // share of the focus gauge a counter fills
        public float Fuse = 0f;            // above 0: a circle that lies this long after it lands, then bursts (Powder Keg); a shot of hers through it sets it off at once
        public float ShotBurstScale = 1.5f; // a keg set off by a shot deals this times the damage

        // Work order 017 (2026-11-20): what the run's skill upgrades change (SkillUpgrades). Every skill starts with them
        // off; an upgrade sets them on the run's own copy of the skill, never here in the settings.
        public float BasicHitCooldownCut = 0f; // s off this skill's cooldown for every basic shot that hits (Quick Draw)
        public float HitCooldownCut = 0f;      // s off a cooldown for every use of this skill that hits (Tied Trigger)
        public string HitCutsSkill = "";       // whose cooldown HitCooldownCut cuts, by name; "": its own
        public float CloseRange = 0f;          // above 0, a spender: within this many m its bonus per mark is CloseMarkScale times; farther, no bonus and it takes no marks (Point Blank)
        public float CloseMarkScale = 1f;
        public float KillCooldownCut = 0f;     // share of its cooldown taken off when a use kills something (Fresh Shells: 0.5)
        public float DazeCooldownCut = 0f;     // share of its cooldown taken off when a use dazes its target (Follow Through: 0.5)
        public bool NoCharge = false;          // a Charge skill that fires at once, always as a perfect release (Snap Shot)
        public float DazedDamageScale = 1f;    // its damage to a dazed target, times this (Execution: 2)
        public float TagJump = 0f;             // above 0: when a target tagged by it dies, the tag jumps to the nearest enemy this near, renewed (Spreading Tag)
        public int SlideShotMarks = 0;         // KeyMove: marks each basic shot that hits during the move adds (Slide Shot)
        public string CounterReadies = "";     // a counter by this skill makes the named skill ready at once (Riposte)

        // Work order 050 (decision 0012, 2026-11-20): how many times her flat damage a hit of this skill adds. Below 0, which
        // the settings asset holds for every skill (it has no such field), the skill's own by name or kind (FlatDamage.WeightOf).
        public float FlatDamageWeight = -1f;

        /// <summary>A copy, field for field: a run's own view of the skill, patched by its upgrades (work order 017).</summary>
        public SkillDefinition Copy() => (SkillDefinition)MemberwiseClone();

        // Work order 067: the skill as her run's upgrades made it before her stats' layer (Blast, Wide window) was laid on it
        // (SkillStatLayer). Not a number of the skill: never serialized, never patched, null on every skill of the settings.
        [NonSerialized] SkillDefinition _beforeStats;
        /// <summary>The skill before her stats' layer (work order 067): itself when no layer is on it. The cards reckon an upgrade on this.</summary>
        public SkillDefinition BeforeStats => _beforeStats ?? this;
        internal void SetBeforeStats(SkillDefinition before) => _beforeStats = ReferenceEquals(before, this) ? null : before;
    }

    /// <summary>
    /// How a skill holds her (decision 0005, 2026-11-19, Kristian: "the light skills may be cut short by moving; only
    /// the heavy ones lock her").
    /// </summary>
    public enum SkillLock
    {
        /// <summary>Decision 0004: committed until the last hit; a press on the move slides her to a stand.</summary>
        Committed,
        /// <summary>
        /// The skill does not stop, slow, slide or turn her more than a basic shot does; the move keys work all through
        /// it. Nothing else starts while it runs, and a dash still cancels it.
        /// </summary>
        Light,
    }

    /// <summary>
    /// The twelve skills of the first class. The first eight are in the key order they had when each had its own key;
    /// since work order 015 (2026-11-20) the place in the list is only which skill it is, and the key it sits on is kept
    /// apart (<see cref="SkillLoadout"/>): the four start skills on the keys they had, a skill taken later on the first
    /// free key. The pool is the eight that are not start skills; four were added by that order.
    /// The first five came in G3, one per input type. Scatter Blast, Backstep and Triple Tap came in G4.
    /// See docs/slices/g3-skills.md and docs/slices/g4-kit.md.
    /// Decision 0005 (2026-11-19) made them a kit: the pistols set up (marks), the shotgun cashes in. A run starts with
    /// Quick Shot, Scatter Blast, Backstep and Triple Tap; the other four wait for later and have their new cooldowns
    /// and mark roles ready.
    /// </summary>
    public static class SkillDefaults
    {
        /// <summary>
        /// What each skill does, in one line of plain words, for the floor reward's choice (work order 012). By name, so a
        /// skill tuned in the settings asset keeps its line; "" for a name not here.
        /// </summary>
        public static string Line(string name)
        {
            switch (name)
            {
                case "Quick Shot": return "A fast pistol shot down a line that marks what it hits. You keep moving.";
                case "Powder Keg": return "Throw a keg that bursts after a short fuse; shoot it to set it off at once, harder.";
                case "Fan Fire": return "Hold to spray a cone of shots; marks what it hits, once a second.";
                case "Long Shot": return "Hold to charge, let go for one long shot; let go in the green band for double damage.";
                case "Scatter Blast": return "A shotgun blast in a cone that takes every mark off what it hits for more damage.";
                case "Heavy Volley": return "A long cast, then a wide, heavy line of fire that spends marks for more damage.";
                case "Backstep": return "Hop back, then press again to shoot; late in the window the shot spends the marks.";
                case "Triple Tap": return "Three quick shots, one a press; each marks, and a clean last press marks twice.";
                case "Tag Round": return "A light shot that marks and tags what it hits first: it takes 10% more damage for 8 s.";
                case "Breaker Slug": return "A short, wide slug that breaks guard: it spends marks for more stagger, not damage.";
                case "Slide": return "Slide 6 m the way you move, shooting as you go. Two charges.";
                case "Stopping Shot": return "A shot that stops a blue attack from the front: the enemy is dazed, focus fills.";
                default: return "";
            }
        }

        /// <summary>Whether a skill builds marks, spends them, or both, in a few words (work order 012).</summary>
        public static string MarkRole(SkillDefinition s)
        {
            if (s == null) return "";
            bool builds = s.MarksAdded > 0 || s.CleanTapMarks > 0;
            bool spends = s.MarkDamageBonus > 0f || s.CleanMarkDamageBonus > 0f;
            return builds && spends ? "builds marks, spends them on a clean press"
                 : builds ? "builds marks"
                 : spends ? "spends marks"
                 : s.MarkStaggerBonus > 0f ? "spends marks for stagger" // work order 015
                 : "no marks";
        }

        public static SkillDefinition[] Create() => new[]
        {
            new SkillDefinition
            {
                Name = "Quick Shot", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 3f, Damage = 100, Startup = 0.15f, Recovery = 0.1f, Range = 12f, HalfWidth = 0.3f,
                StartEquipped = true, Lock = SkillLock.Light, MarksAdded = 1,
                Stagger = 3f,
            },
            new SkillDefinition
            {
                // A two-step since work order 015 (2026-11-20): thrown light, 0.2 s to land, it lies 1.5 s and bursts;
                // a shot of hers through it sets it off at once for one and a half times the damage. Startup 0.5 s and
                // committed before.
                Name = "Powder Keg", Input = SkillInput.Point, Shape = SkillShape.Circle,
                Cooldown = 14f, Damage = 180, Startup = 0.2f, Recovery = 0.1f, Range = 8f, Radius = 2.5f,
                Lock = SkillLock.Light, Fuse = 1.5f, ShotBurstScale = 1.5f,
                Stagger = 20f,
            },
            new SkillDefinition
            {
                Name = "Fan Fire", Input = SkillInput.Holding, Shape = SkillShape.Cone,
                Cooldown = 16f, Damage = 30, Startup = 0.2f, Recovery = 0.3f, Range = 7f, Angle = 50f,
                HoldMax = 2f, TickInterval = 0.15f,
                MarksAdded = 1, MarkInterval = 1f,
                Stagger = 4f, // per tick
            },
            new SkillDefinition
            {
                Name = "Long Shot", Input = SkillInput.Charge, Shape = SkillShape.Line,
                Cooldown = 12f, Damage = 300, Startup = 0.1f, Recovery = 0.4f, Range = 16f, HalfWidth = 0.25f,
                HoldMax = 2.5f, ChargeTime = 1f, MinChargeDamage = 0.3f,
                PerfectFrom = 0.8f, PerfectTo = 1f, PerfectScale = 2f, // the perfect band (work order 015)
                Stagger = 15f,
            },
            new SkillDefinition
            {
                // Cooldown 8 s until 2026-11-19: with one spender only, the target sat at five marks (Kristian).
                Name = "Scatter Blast", Input = SkillInput.Normal, Shape = SkillShape.Cone,
                Cooldown = 6f, Damage = 220, Startup = 0.2f, Recovery = 0.35f, Range = 5f, Angle = 70f,
                StartEquipped = true, MarkDamageBonus = 0.25f,
                Stagger = 30f,
            },
            new SkillDefinition
            {
                Name = "Heavy Volley", Input = SkillInput.Casting, Shape = SkillShape.Line,
                Cooldown = 24f, Damage = 400, Startup = 1f, Recovery = 0.5f, Range = 14f, HalfWidth = 1f,
                MarkDamageBonus = 0.4f,
                Stagger = 40f,
            },
            new SkillDefinition
            {
                // Two presses (decision 0005, amended 2026-11-19 after Kristian played: "a second way to spend marks, in
                // a hard way"): the first is the hop and fires nothing; the second, in the window that opens when the
                // hop ends, fires the shot. Early in the window it adds a mark; in its last third it spends them all.
                Name = "Backstep", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 10f, Damage = 80, Startup = 0.2f, Recovery = 0.2f, Range = 10f, HalfWidth = 0.4f,
                MoveDistance = -5f,
                StartEquipped = true, MarksAdded = 1,
                RecastCount = 2, RecastWindow = 0.6f, OpeningMove = true, CleanMarkDamageBonus = 0.5f,
                Stagger = 5f,
            },
            new SkillDefinition
            {
                // Three presses, one shot each (decision 0005); the burst of three it was before is gone.
                Name = "Triple Tap", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 5f, Damage = 70, Startup = 0.15f, Recovery = 0.1f, Range = 12f, HalfWidth = 0.3f,
                HitCount = 1, HitInterval = 0.12f,
                StartEquipped = true, Lock = SkillLock.Light, MarksAdded = 1,
                RecastCount = 3, RecastWindow = 0.6f, CleanTapMarks = 2,
                Stagger = 2f, // per shot
            },
            // The four that came with the pool (work order 015, 2026-11-20). None is on a key at the start.
            new SkillDefinition
            {
                // Keep it up on the big target: the first target hit is tagged for 8 s and takes 10 % more from her.
                Name = "Tag Round", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 6f, Damage = 40, Startup = 0.15f, Recovery = 0.1f, Range = 12f, HalfWidth = 0.3f,
                Lock = SkillLock.Light, MarksAdded = 1,
                TagSeconds = 8f, TagDamageShare = 0.1f,
                Stagger = 2f,
            },
            new SkillDefinition
            {
                // Stagger on demand: a spender of stagger, 30 % more per mark taken, no more damage.
                Name = "Breaker Slug", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 20f, Damage = 150, Startup = 0.25f, Recovery = 0.4f, Range = 7f, HalfWidth = 0.6f,
                MarkStaggerBonus = 0.3f,
                Stagger = 80f,
            },
            new SkillDefinition
            {
                // A second way to move: 6 m over 0.35 s along the move keys, no invulnerability, two charges.
                Name = "Slide", Input = SkillInput.KeyMove, Shape = SkillShape.Line,
                Cooldown = 12f, Damage = 0, Startup = 0.35f, Recovery = 0f, MoveDistance = 6f,
                Lock = SkillLock.Light, Charges = 2,
            },
            new SkillDefinition
            {
                // The counter: on an enemy winding up a blue attack, from its front, the attack is dropped, it is dazed
                // 2 s and her focus fills by a third. Otherwise an ordinary shot that marks.
                Name = "Stopping Shot", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 10f, Damage = 60, Startup = 0.15f, Recovery = 0.1f, Range = 12f, HalfWidth = 0.3f,
                Lock = SkillLock.Light, MarksAdded = 1,
                CounterDaze = 2f, CounterHalfAngle = 60f, CounterFocus = 1f / 3f,
                Stagger = 3f,
            },
        };
    }
}
