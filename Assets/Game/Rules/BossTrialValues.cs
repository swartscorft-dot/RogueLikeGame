using System;

namespace Roguelike.Rules
{
    /// <summary>The trial fight's seven attacks (work order 147), as its chooser and its log name them.</summary>
    public enum BossTrialAttack { None, Cleave, Charge, GraveMarks, Spokes, Slam, Seeds, Judgement }

    /// <summary>
    /// The hard boss, stage 1 in grey boxes (work order 147, 2026-11-27): the numbers of the trial fight, all first guesses for him
    /// to tune by playing. Kristian, 2026-11-27, after a whole run on main 4482587: "bossen stod fast mellom en trang path. så det var
    /// grise enkelt. bossen skal være veldig vanskelig. masse mechanics og ting å dodge. nå sto jeg basicly bare stille og høyre
    /// cklikket." He ticked "Research, så bygg i gråboks" and "Flere forsøk forventet" (a fight of two to three minutes he dies at
    /// a few times and learns). Their source is section 7 of research R6 (runner\research\r6-boss-fights\report.md) with the
    /// producer's rulings of work order 147 laid over it; where this agent changed one of section 7's numbers for fairness, its comment
    /// says so and why. On only with <see cref="BossValues.Trial"/> (and the Trials page's "Boss" line on "Trial").
    /// </summary>
    [Serializable]
    public sealed class BossTrialValues
    {
        // ---------------- Each attack's own switch (the order: "Each attack has its own static switch ... (ON)") ----------------

        /// <summary>Attack 1, Cleave and Wave. Off: never chosen (nor taught, nor in a combo).</summary>
        public static bool CleaveOn = true;
        /// <summary>Attack 2, Charge, with its crash daze.</summary>
        public static bool ChargeOn = true;
        /// <summary>Attack 3, Grave Marks (also the one mark of phase 3's Spokes combo and Judgement's marks).</summary>
        public static bool GraveMarksOn = true;
        /// <summary>Attack 4, Spokes, phase 2 on.</summary>
        public static bool SpokesOn = true;
        /// <summary>Attack 5, Slam and Shockwave, phase 2 on.</summary>
        public static bool SlamOn = true;
        /// <summary>Attack 6, Seeds, phase 2 on.</summary>
        public static bool SeedsOn = true;
        /// <summary>Attack 7, Judgement and Ruin, at 30 % and 15 % of its health.</summary>
        public static bool JudgementOn = true;

        /// <summary>
        /// Grave Marks LEAD her motion, her velocity times <see cref="MarkLeadSeconds"/>, at most <see cref="MarkLeadMost"/>, as the
        /// caster's Grave Rain already does (work order 034). Kristian, 2026-11-27, asked whether the four circles should be laid a
        /// little ahead of her so that running straight is punished too, ticked "Ja, de leder henne". On. Off: each circle where she
        /// stands as it is placed.
        /// </summary>
        public static bool GraveMarksLead = true;
        /// <summary>
        /// Seeds CAN BE SHOT, <see cref="SeedShots"/> basic shots each; they take no marks, give no focus, draw no bounce and are not
        /// enemies. Kristian, 2026-11-27, asked with his own ruling in the question (the boss calls nobody; the seeds are not enemies, do
        /// not move or attack, take no marks), ticked "Ja, de kan skytes". On. Off: delayed circles she must leave.
        /// </summary>
        public static bool SeedsCanBeShot = true;

        /// <summary>True when the attack's own switch is on.</summary>
        public static bool IsOn(BossTrialAttack a)
        {
            switch (a)
            {
                case BossTrialAttack.Cleave: return CleaveOn;
                case BossTrialAttack.Charge: return ChargeOn;
                case BossTrialAttack.GraveMarks: return GraveMarksOn;
                case BossTrialAttack.Spokes: return SpokesOn;
                case BossTrialAttack.Slam: return SlamOn;
                case BossTrialAttack.Seeds: return SeedsOn;
                case BossTrialAttack.Judgement: return JudgementOn;
                default: return false;
            }
        }

        /// <summary>Every switch of this file on again (tests that took one out put them back).</summary>
        public static void AllOn()
        {
            CleaveOn = ChargeOn = GraveMarksOn = SpokesOn = SlamOn = SeedsOn = JudgementOn = true;
            GraveMarksLead = SeedsCanBeShot = true;
        }

        // ---------------- The names its lines and its strikes carry ----------------

        public const string Cleave = BossValues.Cleave;      // "Cleave", as today's
        public const string Wave = BossValues.Wave;          // "Cleave Wave", as today's
        public const string Charge = BossValues.Charge;      // "Charge", as today's
        public const string GraveMark = "Grave Mark";
        public const string GraveMarks = "Grave Marks";
        public const string Spokes = "Spokes";
        public const string Slam = BossFinale.Slam;          // "Ground Slam", as the finale's
        public const string Shockwave = "Shockwave";
        public const string Seeds = "Seeds";
        public const string Seed = "Seed";
        public const string Judgement = "Judgement";
        public const string Ruin = "Ruin";

        // ---------------- 7.1, the numbers that frame it ----------------

        /// <summary>Its health (section 7.1: 18,000 today). 45,000 / (700 a second x 0.55 of the time) is about 117 s of shooting.</summary>
        public int Health = 45000;
        /// <summary>Phase 2 begins when its health share falls under this (7.1: 100 to 65 %, 65 to 30 %, 30 to 0 %).</summary>
        public float PhaseTwoBelow = 0.65f;
        /// <summary>Phase 3 begins under this share.</summary>
        public float PhaseThreeBelow = 0.30f;
        /// <summary>
        /// s it stands invulnerable at each phase's line (7.1: "the existing roar plus 1 s"), told by a grey tint of its body and the
        /// bar's notch; the hit that crosses the line counts, nothing after it does.
        /// </summary>
        public float TransitionSeconds = 2.5f;
        /// <summary>The grey of its body through a transition.</summary>
        public Rgb TransitionTint = new Rgb(0.55f, 0.55f, 0.58f);
        /// <summary>The colour its room's lamps take at phase 2: the finale's own grave-green (BossFinaleValues.LampColour).</summary>
        public Rgb PhaseTwoLamps = new Rgb(0.45f, 0.95f, 0.55f);
        /// <summary>The colour they take at phase 3 (the agent's: a cold violet, apart from the red telegraphs and the blue counter).</summary>
        public Rgb PhaseThreeLamps = new Rgb(0.62f, 0.42f, 0.95f);

        /// <summary>Damage of a light hit (7.1: 11 % of her 140): the Wave, a Grave Mark, the Shockwave.</summary>
        public int DamageLight = 15;
        /// <summary>A standard hit: the Cleave, Spokes, a Seed.</summary>
        public int DamageStandard = 25;
        /// <summary>A heavy hit: the Charge and the Slam's landing.</summary>
        public int DamageHeavy = 35;
        /// <summary>A failed check: Ruin.</summary>
        public int DamageFailedCheck = 50;

        /// <summary>s after its waking before its first attack may begin (7.1: today it charged 0.01 s after it woke, at the door).</summary>
        public float FirstAttackAfter = 2.0f;
        /// <summary>
        /// m inside the edge of what his camera shows of the ground that its middle must stand for an attack to begin (his "only when
        /// seen", applied to the boss, asked of the real camera as RangedOpening asks it for the shooters): its body's radius, so its
        /// whole body is on his screen. It walks until it is.
        /// </summary>
        public float OnScreenMargin = 1.2f;
        /// <summary>s from the end of a recovery to the next wind-up, by phase (7.1: 0.9, 0.7, 0.45). Index 0 is unused.</summary>
        public float[] Gap = { 0.9f, 0.9f, 0.7f, 0.45f };
        /// <summary>Wind-ups are this many times as long, by phase (7.2: x0.9 in phase 2, x0.85 in phase 3). Index 0 is unused.</summary>
        public float[] WindupFactor = { 1f, 1f, 0.9f, 0.85f };
        /// <summary>s: at most one dash-only attack (the Slam) in this long (7.1: her dash's cooldown).</summary>
        public float DashOnlyEvery = 10f;
        /// <summary>s: no dash-only attack within this long of another big pattern's end (7.1).</summary>
        public float BigPatternApart = 3f;
        /// <summary>An attack may be chosen at most this many times in a row (7.3).</summary>
        public int InARowMost = 2;
        /// <summary>The Cleave: never twice in a row (7.2).</summary>
        public int CleaveInARowMost = 1;
        /// <summary>Spokes, Seeds and the Slam need this many other attacks since their last use (7.3).</summary>
        public int OthersBetween = 3;
        /// <summary>The seed of the draws in a real fight is taken from the clock; a test gives its own.</summary>
        public int DrawSeed = 147;

        /// <summary>Phase 1 opens with this fixed order (7.3), so the first minute teaches; then the draws begin.</summary>
        public BossTrialAttack[] Teaching = { BossTrialAttack.Charge, BossTrialAttack.Cleave, BossTrialAttack.GraveMarks, BossTrialAttack.Cleave, BossTrialAttack.Charge };
        /// <summary>
        /// s the next taught attack waits to fit (her distance, its sight) before it is passed over (the agent's: the first Charge
        /// needs her 7 m off; a player who stands at its feet would hold the order for ever).
        /// </summary>
        public float TeachWaitMost = 3f;
        /// <summary>
        /// Phase 2 opens with its new attacks, each alone, in this order (7.3 names Spokes, then Seeds; the agent added the Slam, the
        /// third new one, so that it too is first seen alone and not drawn by chance under another pattern).
        /// </summary>
        public BossTrialAttack[] PhaseTwoOpening = { BossTrialAttack.Spokes, BossTrialAttack.Seeds, BossTrialAttack.Slam };
        /// <summary>s with nothing new after each opening attack (7.3).</summary>
        public float NothingNewAfter = 3f;
        /// <summary>s from one opening attack's start after which the next may come even if the first's end was never seen (a safeguard).</summary>
        public float OpeningWaitMost = 10f;
        /// <summary>Its walking speed, as today's (3 m/s).</summary>
        public float MoveSpeed = 3f;

        /// <summary>s she is untouchable by it after a hit of it or its patterns (7.1, the hurt guard; Hades' 1.3 s after 25 %).</summary>
        public float HurtGuardSeconds = 0.8f;
        /// <summary>s of each half of her body's flicker through the guard: shown, hidden, shown.</summary>
        public float HurtFlickerHalf = 0.07f;

        // ---------------- 1. Cleave and Wave ----------------

        /// <summary>s of the Cleave's wind-up in phase 1 (7.2, as today's).</summary>
        public float CleaveWindup = 1.1f;
        /// <summary>s: the shortest the wind-up becomes in any phase (0.25 s to react and 0.6 s to step 3.5 m sideways out of the cone's edge).</summary>
        public float CleaveWindupFloor = 0.9f;
        public float CleaveRecovery = 1.3f;
        /// <summary>s of its recovery when phase 3's combo follows it with a Charge (7.3).</summary>
        public float ComboCleaveRecovery = 0.5f;
        public float CleaveLength = 6.5f;
        public float CleaveAngle = 110f;
        /// <summary>m: used when she is within this (7.2: 6 m; today's 5 m let her out-step all five of his).</summary>
        public float CleaveWithin = 6f;
        /// <summary>m across the Wave (7.2), from its middle line out to each side half.</summary>
        public float WaveWidth = 6f;
        /// <summary>m from the Wave's middle to its front and its back: the band that hits.</summary>
        public float WaveDepth = 0.3f;
        public float WaveSpeed = 10f;
        public float WaveFlight = 14f;
        /// <summary>s its whole path is drawn on the floor as it leaves (7.2: "drawn for 0.3 s", its path is not drawn today).</summary>
        public float WaveDrawn = 0.3f;

        // ---------------- 2. Charge ----------------

        public float ChargeWindup = 1.0f;
        /// <summary>s: its shortest wind-up (0.25 s to react and 0.35 s to sidestep 2 m out of its 3 m strip).</summary>
        public float ChargeWindupFloor = 0.8f;
        public float ChargeRecovery = 1.6f;
        public float ChargeLength = 14f;
        public float ChargeHalfWidth = 1.5f;
        public float ChargeRush = 12f;
        /// <summary>m: used when she is at least this far (7.2), and it sees her.</summary>
        public float ChargeFrom = 7f;
        public float ChargeTo = 16f;
        /// <summary>s at least from one Charge's start to the next (7.2).</summary>
        public float ChargeEvery = 6f;
        /// <summary>s it is dazed when its charge ends at a pillar or a wall (7.2, as the lancer is), taking <see cref="CrashTaken"/>.</summary>
        public float CrashDaze = 2.0f;
        /// <summary>Her damage while it is dazed by a crash, against the hit's own (its stagger bar's daze, as the lancer's).</summary>
        public float CrashTaken = 1.5f;
        /// <summary>
        /// m: a charge cut short (by the first wall or tall prop on its line, or the end of the walkable ground) is a crash when a wall
        /// or tall prop stands within its body's radius and this much in front of where it stopped, along its middle or either side
        /// of its body (the NavMesh keeps its middle half a metre off a wall and the charge stops 0.7 m short of that, so its 1.2 m body
        /// just meets the wall). A low prop (a brazier) that ends the walkable ground under it is not a crash.
        /// </summary>
        public float CrashSlack = 0.4f;
        /// <summary>Of its body's radius: the two side lines a crash is looked for along, besides its middle.</summary>
        public float CrashSideShare = 0.8f;

        // ---------------- 3. Grave Marks ----------------

        public int Marks = 4;
        /// <summary>s between two circles' placing (7.2).</summary>
        public float MarkEvery = 0.45f;
        /// <summary>s from a circle's placing to its burst, phase 1 (7.2).</summary>
        public float MarkBurst = 1.0f;
        /// <summary>s: the shortest it becomes (0.25 s to react and 2.5 m out of a 2 m circle at 6 m/s, 0.67 s, and a margin).</summary>
        public float MarkBurstFloor = 0.8f;
        public float MarkRadius = 2.0f;
        /// <summary>s of her velocity a circle is moved ahead of her (7.2, with <see cref="GraveMarksLead"/>; the caster's is 0.8 s).</summary>
        public float MarkLeadSeconds = 0.6f;
        public float MarkLeadMost = 3f;
        /// <summary>m/s: slower than this she counts as standing, and no lead is given.</summary>
        public float MarkMovingOver = 0.3f;
        /// <summary>s at least from one Grave Marks' start to the next (7.2: "every 7 s at most").</summary>
        public float MarksEvery = 7f;
        /// <summary>It walks at this share of its speed while it places them (7.2: half speed).</summary>
        public float MarksWalkShare = 0.5f;
        /// <summary>s it stands after the last burst.</summary>
        public float MarksRecovery = 0.3f;
        /// <summary>m round a circle's place it looks for walkable ground to put it on; none there, the circle is not placed.</summary>
        public float MarkFindGround = 1.5f;

        // ---------------- 4. Spokes ----------------

        public int SpokeCount = 6;
        /// <summary>Degrees the second set is turned from the first (7.2: half a gap).</summary>
        public float SpokeTurn = 30f;
        public float SpokeHalfWidth = 1.5f;
        /// <summary>s the first set fills before it bursts, phase 1 (7.2: 1.4 s).</summary>
        public float SpokeFirstFill = 1.4f;
        /// <summary>s the second set fills from the first's burst (7.2: 1.0 s).</summary>
        public float SpokeSecondFill = 1.0f;
        /// <summary>s: the shortest either becomes (0.25 s to react and 2 m sideways out of a strip, 0.33 s, and a margin).</summary>
        public float SpokeFillFloor = 0.8f;
        /// <summary>m: the farthest a spoke reaches when nothing stops it first (walls and tall props stop it, and so it is drawn).</summary>
        public float SpokeReach = 30f;
        public float SpokesRecovery = 0.4f;
        /// <summary>m from it within which Spokes are begun: anywhere it attacks from (they reach the walls).</summary>
        public float SpokesWithin = 16f;

        // ---------------- 5. Slam and Shockwave ----------------

        /// <summary>
        /// s from the crouch to the landing, phase 1. Section 7.2 says 1.0 s (the finale's). Changed for fairness: the landing circle is
        /// 4 m round where she stood, so from its middle she must go 4.5 m (its radius and hers), 0.25 s to react and 0.75 s at 6 m/s:
        /// exactly 1.0 s, no margin at all, and then the ring is on her at once. At 1.3 s she is 1.8 m outside the circle when it lands
        /// (1.0 m in phase 2, 0.6 m in phase 3), and the ring, growing at 7 m/s against her 6, reaches her only 1 s and more later: time
        /// to turn and dash through it.
        /// </summary>
        public float SlamWindup = 1.3f;
        /// <summary>s: the shortest it becomes.</summary>
        public float SlamWindupFloor = 1.1f;
        public float SlamRadius = 4f;
        public float SlamRecovery = 1.2f;
        /// <summary>m: the farthest it leaps for (the finale's 14 m), seen as its charge sees her when farther than the finale's 5 m.</summary>
        public float SlamWithin = 14f;
        /// <summary>m: the ring's middle radius as it is born, at the landing circle's edge.</summary>
        public float RingFrom = 4f;
        /// <summary>m: where it ends.</summary>
        public float RingTo = 16f;
        public float RingSpeed = 7f;
        /// <summary>m across its band (7.2: 0.6 m), drawn as two circles moving.</summary>
        public float RingThickness = 0.6f;
        /// <summary>Directions round the landing in which walls and tall props are looked for once: the ring stops at them, and is drawn so.</summary>
        public int RingSamples = 128;

        // ---------------- 6. Seeds ----------------

        public int SeedCount = 3;
        /// <summary>m from her that each seed is thrown to (7.2: 4 to 7 m, in view).</summary>
        public float SeedNear = 4f;
        public float SeedFar = 7f;
        /// <summary>m round a seed that its burst covers, drawn as a circle that thickens.</summary>
        public float SeedCircle = 2.5f;
        /// <summary>s from its landing to its burst, phase 1 (7.2: 4.0 s).</summary>
        public float SeedFill = 4.0f;
        public float SeedFillFloor = 3.2f;
        /// <summary>m across the grey sphere (7.2: 0.5 m). Her shot's line must touch it.</summary>
        public float SeedSize = 0.5f;
        /// <summary>m over the ground of the sphere's middle.</summary>
        public float SeedHeight = 0.45f;
        /// <summary>Basic shots that break one (7.2: three).</summary>
        public int SeedShots = 3;
        /// <summary>s of the throw's wind-up (the agent's: a short tell on its body; the seeds' own circles are the long one).</summary>
        public float SeedThrowWindup = 0.6f;
        public float SeedsRecovery = 0.6f;
        /// <summary>m apart at the least that two seeds land (so their circles read as three).</summary>
        public float SeedApart = 3f;
        public float SeedFindGround = 1.5f;

        // ---------------- 7. Judgement and Ruin ----------------

        /// <summary>Health shares at which Judgement comes, each once (7.2: 30 % and 15 %).</summary>
        public float[] JudgementAt = { 0.30f, 0.15f };
        /// <summary>The ward bar it raises (7.2: 4,000, about 5.7 s of her 700 a second). While it stands, her hits wear the ward, not its health.</summary>
        public int Ward = 4000;
        /// <summary>s she has to break it; a circle closes round the boss through them (7.2: the closing is the tell).</summary>
        public float WardSeconds = 8f;
        /// <summary>m: the closing circle's radius as the ward rises.</summary>
        public float WardCircleFrom = 12f;
        /// <summary>m: and as the time runs out (its body).</summary>
        public float WardCircleTo = 1.2f;
        /// <summary>s between two Grave Marks on her through the ward (7.2: every 2.5 s).</summary>
        public float WardMarkEvery = 2.5f;
        /// <summary>
        /// s before the ward's end after which no mark is placed (the agent's, for fairness): a led circle bursting while she runs for a
        /// pillar's shadow would land on the way she runs. So of 7.2's marks at 2.5, 5.0 and 7.5 s only the first two come.
        /// </summary>
        public float WardMarksStopBefore = 2f;
        /// <summary>s it is dazed when she breaks the ward (7.2), taking its stagger bar's 1.5 times.</summary>
        public float WardDaze = 4f;
        /// <summary>
        /// s she has to reach a pillar's shadow once Ruin is told, in a room this order has not measured (the practice room); each
        /// throne template has its own (<see cref="RuinSecondsFor"/>).
        /// </summary>
        public float RuinSeconds = 2.0f;
        /// <summary>
        /// s of Ruin in throne-nave (four free piers). Section 7.2 says 2.0 s. Lengthened for fairness: measured
        /// (BossTrialRuinRulesTests), the worst place she can stand within 10 m of the room's middle (beside the boss's body) is 12.0 m on
        /// foot from the nearest shadow, round the piers: 0.25 s to react and 2.0 s at 6 m/s, 2.25 s.
        /// </summary>
        public float RuinSecondsNave = 2.3f;
        /// <summary>
        /// s of Ruin in throne-arcade (two piers flanking the approach; its corner masses stand in the room's corners, so their shadows
        /// fall almost all outside it). Section 7.2 says 2.0 s. Lengthened for fairness: both piers stand in the front half, and from
        /// the back of the circle within 10 m of the middle the nearest shadow is 17.8 m on foot (BossTrialRuinRulesTests): 0.25 s and
        /// 17.8 m at 6 m/s, 3.21 s.
        /// </summary>
        public float RuinSecondsArcade = 3.3f;
        /// <summary>Directions round it in which the pillars' shadows are found (and drawn, and judged).</summary>
        public int ShadowSamples = 720;
        /// <summary>m round the room's middle it looks for a place to kneel that keeps its body clear of every pier (its body is 1.2 m).</summary>
        public float KneelSearch = 6f;
        /// <summary>m clear of any wall or tall prop round the place it kneels: its body and a little.</summary>
        public float KneelClear = 1.5f;
        /// <summary>m: near enough to the place it walks to for Judgement.</summary>
        public float KneelArrive = 0.6f;
        /// <summary>s it walks to the middle at the most; then it kneels where it is.</summary>
        public float KneelWalkMost = 8f;

        // ---------------- The record (7.9) ----------------

        /// <summary>m/s: she counts as standing below this (7.9: 35 % of run speed, her accurate speed).</summary>
        public float StandingUnder = 2.1f;
        /// <summary>s: a dash pressed within this before a dash-only attack's tell is written on its line (7.9).</summary>
        public float DashBeforeWithin = 10f;
        /// <summary>m: the cells of the floor she used are counted on a grid of this.</summary>
        public float FloorCell = 2f;
        /// <summary>s between two looks at where she stands and how fast, for the fight's line.</summary>
        public float SampleEvery = 0.1f;

        /// <summary>Ruin's time for a throne room of this template (by its name); <see cref="RuinSeconds"/> for any other room.</summary>
        public float RuinSecondsFor(string template) =>
            template == "throne-nave" ? RuinSecondsNave : template == "throne-arcade" ? RuinSecondsArcade : RuinSeconds;
    }
}
