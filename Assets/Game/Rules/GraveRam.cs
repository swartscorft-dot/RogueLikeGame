using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The unique elites (work order 149, 2026-11-27). Kristian, asked about the elite room: "Egentlig vil jeg ha unike elites, som
    /// har et eget sett med utfordringer slik som i hades"; he ticked "Egen minibosse per etasje" and "Research nå, første elite hvis
    /// tid" (floor 1's elite built in grey boxes behind a switch, so he can feel the direction). Research R7 section 5 is the
    /// proposal; only floor 1's, the Grave Ram (<see cref="GraveRamValues"/>), is built.
    /// </summary>
    public static class EliteValues
    {
        /// <summary>
        /// Work order 149's switch, ON by default (his words above). On: floor 1's elite room holds the Grave Ram and nothing else
        /// (the room's other enemies are not made when the floor is built; the generator's output is the same). Off: today's elite
        /// and elite room, field for field. Floors 2 to 4 have today's elite either way. One switch for the whole game, so it is
        /// static; the Trials page's "Elites" line chooses it with a player at the keys; tests set it and put it back.
        /// </summary>
        public static bool Unique = true;

        /// <summary>The one floor whose elite is unique in this order: floor 1, the Grave Ram.</summary>
        public const int GraveRamFloor = 1;

        /// <summary>Does this room of this floor hold the Grave Ram alone, with the switch as given?</summary>
        public static bool HoldsTheRam(int floor, bool eliteRoom, bool unique) => unique && eliteRoom && floor == GraveRamFloor;
    }

    /// <summary>Work order 149's line on the Trials page: "Elites", "Unique" or "As before".</summary>
    public static class EliteLines
    {
        public const string Name = "Elites";
        public const string UniqueWord = "Unique", AsBefore = "As before";

        /// <summary>A stored number: 1 unique, 0 as before, anything else none stored (the switch's own).</summary>
        public static bool Unique(int stored) => stored == 1 || (stored != 0 && EliteValues.Unique);

        public static string WordsOf(bool unique) => unique ? UniqueWord : AsBefore;
    }

    /// <summary>
    /// The Grave Ram's numbers (work order 149; research R7 section 5.2, with the producer's rulings in the order over it). Every
    /// number a first guess, his to move after meeting it. Its one idea: bait its charge into stone, then punish the daze.
    ///
    /// Against her 100 health at the start of a run (R7 5.1), its damage is final (not passed through the floor's damage scale,
    /// as the boss's is not): the Ram 18 (18 %), the Stomp 14 (14 %), one Grave Stone 8 (8 %; three on one spot are 24, or 16 with
    /// the hurt guard, which spares the second, 0.5 s after the first).
    /// </summary>
    [Serializable]
    public sealed class GraveRamValues
    {
        /// <summary>Its name, on its bar at the top of the screen.</summary>
        public const string Title = "The Grave Ram";
        /// <summary>Its attacks' names, as the log and the summary write them.</summary>
        public const string Ram = "Ram", Stomp = "Stomp", Stones = "Grave Stones";

        // ---------------- Its body (R7 5.2: the brute's model x1.6, ash white, about 0.8 m across the middle) ----------------
        /// <summary>It is shown this times the brute's model (the skeleton warrior's 1.8 m: 2.88 m).</summary>
        public float Shown = 1.6f;
        /// <summary>m, its body's radius (the brute's 0.5 times 1.6): what her shots hit, and the ball its Ram looks ahead with.</summary>
        public float BodyRadius = 0.8f;
        /// <summary>m, its capsule's height (the brute's model shown 1.6 times).</summary>
        public float BodyHeight = 2.9f;
        /// <summary>Its tint: ash white, cool, clear of telegraph red, counter blue, anger orange, the lamps' green and the elite's gold (R7 5.1, a look choice for him).</summary>
        public Rgba Tint = new Rgba(0.86f, 0.86f, 0.84f);
        /// <summary>m/s it walks (a brute's 3.5, the settings asset's).</summary>
        public float MoveSpeed = 3.5f;
        /// <summary>m over the ground of the line it sees her along (the shooter's, the lancer's, the boss's): walls, shut doors and tall props.</summary>
        public float SightHeight = 0.5f;

        // ---------------- Health, stagger, phases ----------------
        /// <summary>Its health: 10,000 (R7 5.2: 10,000 / (450 x 0.55), about 40 s practised; / (450 x 0.35), about 63 s on a first try). Final, not scaled by the floor.</summary>
        public int Health = 10000;
        /// <summary>
        /// Its stagger bar, as today's elite brute's (half the guard's: <see cref="LevelValues.EliteStaggerShare"/> of 100 points, its daze
        /// 4 s at 1.5 times the damage). Her stagger skills empty it as any bar's.
        /// </summary>
        public StaggerValues Stagger = new StaggerValues { Max = 50f, DazeSeconds = 4f, DazedDamageMultiplier = 1.5f };
        /// <summary>The share of its health under which it is in phase 2 (R7 5.2: at 50 % it stops and roars).</summary>
        public float PhaseTwoBelow = 0.5f;
        /// <summary>
        /// s it stands and roars at the break (R7 5.2: 1.5 s). As the boss's roar (<see cref="BossFinaleValues.RoarSeconds"/>, work order 106):
        /// it starts nothing, walks nowhere and cannot be staggered (its bar held aside), and her damage counts as ever. R7 wrote
        /// "invulnerable"; the boss's roar in this build is not, so neither is the Ram's.
        /// </summary>
        public float RoarSeconds = 1.5f;
        /// <summary>Phase 2: every wind-up this many times as long (R7 5.2), never under its telegraph floor (<see cref="GraveRamRules.TellFloor"/>).</summary>
        public float PhaseTwoWindup = 0.85f;
        /// <summary>s from the end of one attack's recovery (or a daze) to the next wind-up (R7 5.2: 1.0 s in phase 1)...</summary>
        public float Gap = 1.0f;
        /// <summary>...and in phase 2 (Cult of the Lamb's cut, R6 O18).</summary>
        public float PhaseTwoGap = 0.6f;
        /// <summary>Phase 2: a Ram comes as this many, the second aimed where she then stands (the angry boss's double charge).</summary>
        public int PhaseTwoRams = 2;

        // ---------------- His screen and the start (R7 5.1, R6 7.1) ----------------
        /// <summary>s after the fight wakes before its first attack.</summary>
        public float FirstAttackAfter = 2.0f;
        /// <summary>
        /// Layout px (of a view 1080 high) its whole body's box must stand inside the screen's edges, and off the interface's plates,
        /// to count as on his screen (the order: "the Ram's WHOLE body inside the camera's view and clear of the interface's plates,
        /// with a named margin").
        /// </summary>
        public float ScreenMargin = 24f;
        /// <summary>
        /// Layout px over the screen's bottom the skill strip's keep-out reaches, across the strip's width: its boxes, the pips and the
        /// chamber row over them (work order 145's <see cref="LancerWarningMark.BottomInset"/>, the same 210).
        /// </summary>
        public float StripKeepOut = 210f;

        // ---------------- 1. Ram (its main move) ----------------
        /// <summary>
        /// The Ram (R7 5.2): when she is 6 to 18 m away and its line to her is clear; a strip 2.4 m wide drawn on the floor toward where
        /// she stood when the tell began, 1.2 s; it runs at 14 m/s up to 18 m; 18 to her. Blue: Stopping Shot from its front drops
        /// it and dazes it. Recovery 1.0 s after a miss with no crash (and after it met her, where it stops).
        /// </summary>
        public EnemyAttackDefinition RamAttack = new EnemyAttackDefinition
        {
            Name = Ram, Shape = EnemyAttackShape.Line, MinDistance = 6f, MaxDistance = 18f,
            Windup = 1.2f, Recovery = 1.0f, Damage = 18, Length = 18f, HalfWidth = 1.2f, Rush = 14f, Counterable = true,
        };
        /// <summary>s at least from the end of one Ram to the start of the next (R7 5.2: 2.5 s); the second Ram of phase 2 is the exception.</summary>
        public float RamRest = 2.5f;
        /// <summary>
        /// s it is dazed when its Ram meets stone (R7 5.2: 2.5 s, the lancer's rule, longer), by its stagger bar emptied at once, so it
        /// takes the bar's dazed damage, <see cref="Stagger"/>'s 1.5 times (R7 5.2: "dazed 2.5 s and takes x1.5").
        /// </summary>
        public float CrashDaze = 2.5f;
        /// <summary>m over the ground of the bottom of the body it looks ahead with (a capsule of <see cref="BodyRadius"/>): low props stop it.</summary>
        public float ProbeLift = 0.05f;
        /// <summary>m over the ground of the middle of that capsule's upper end.</summary>
        public float ProbeTop = 2.0f;
        /// <summary>m past the walkable ground's end within which stone the body met is what ended it (the lancer's).</summary>
        public float EdgeSlack = 1f;
        /// <summary>m short of where it should be that means the walkable ground ended under it (the lancer's).</summary>
        public float BlockedSlack = 0.5f;

        // ---------------- 2. Stomp (when she is close) ----------------
        /// <summary>
        /// The Stomp (R7 5.2): a circle of 3.5 m round itself, 0.9 s, 14 to her, within 4 m, never twice in a row, recovery 1.2 s.
        /// </summary>
        public EnemyAttackDefinition StompAttack = new EnemyAttackDefinition
        {
            Name = Stomp, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 4f,
            Windup = 0.9f, Recovery = 1.2f, Damage = 14, Radius = 3.5f,
        };
        /// <summary>
        /// s from one Stomp's start after which a second may follow it after all, when nothing else can be begun (she stays within its
        /// reach: no Ram under 6 m, no stones under 6 m). Work order 149's measure: with "never twice in a row" alone, she standing
        /// against it after a Stomp parked it, a free target. 6 s: its tell 0.9, recovery 1.2 and gap 1.0, and time to walk to a place for
        /// a Ram (its bots: at 4 s it came back in and Stomped again every 4 s and Rammed about once a fight; she follows it, it Stomps).
        /// </summary>
        public float StompAgain = 6f;
        /// <summary>m from her, its middle to hers, at which it stops walking up to her (its body's and hers and a little): it does not stand on her.</summary>
        public float StopShortOfHer = 1.6f;

        // ---------------- Where it goes between attacks (work order 149's bots: walking straight at her, it Stomped and hardly ever Rammed) ----------------
        /// <summary>
        /// Between attacks, unless she is in its Ram's band with a line that reaches her (it holds there) or close with its Stomp ready
        /// (it comes in), it walks to a place for a Ram: the nearest of <see cref="SeekWays"/> places round her at <see cref="SeekNear"/> and
        /// <see cref="SeekFar"/> m, walkable straight from her, on his screen, its line from there clear and reaching her; with none, to her.
        /// Looked for again every <see cref="SeekEvery"/> s. Its bots met a Ram in 4 of 9 fights before (the room's props cut its lines
        /// walking straight at her, and it Stomped instead).
        /// </summary>
        public float SeekNear = 8f;
        public float SeekFar = 11f;
        public int SeekWays = 16;
        public float SeekEvery = 0.5f;
        /// <summary>
        /// m past its Stomp's reach within which it comes in to her for a Stomp (0.5); farther, it goes to a place for a Ram. Its bots
        /// before: it came in from 6 m, Stomped, backed off for its rule of twice in a row, came in again 4 s on and Stomped again, and
        /// Rammed about once a fight.
        /// </summary>
        public float StompComeIn = 0.5f;

        // ---------------- 3. Grave Stones (when it cannot charge) ----------------
        /// <summary>
        /// The Grave Stones (R7 5.2): three circles of 1.8 m at her position (no lead), 0.5 s apart, each bursting 1.0 s after it
        /// appears, 8 each; when she is beyond 6 m and its line to her is not clear; at least 6 s apart. Its wind-up is the throwing,
        /// from the first stone to the last (<see cref="StonesApart"/> times two); its recovery stands until the last has burst.
        /// </summary>
        public EnemyAttackDefinition StonesAttack = new EnemyAttackDefinition
        {
            Name = Stones, Shape = EnemyAttackShape.Circle, MinDistance = 6f, MaxDistance = 60f,
            Windup = 1.0f, Recovery = 1.0f, Damage = 8, Radius = 1.8f,
        };
        /// <summary>Stones a throw.</summary>
        public int StonesCount = 3;
        /// <summary>s between two stones of a throw.</summary>
        public float StonesApart = 0.5f;
        /// <summary>s from a stone's circle appearing to its burst (phase 2: times <see cref="PhaseTwoWindup"/>).</summary>
        public float StoneBurstAfter = 1.0f;
        /// <summary>s at least from one throw's start to the next.</summary>
        public float StonesRest = 6f;
        /// <summary>m beyond which she must be (R7 5.2: "beyond 6 m").</summary>
        public float StonesBeyond = 6f;

        // ---------------- The telegraph floor (R6 section 5) ----------------
        /// <summary>s to react, then her walk at <see cref="HerSpeed"/>, then <see cref="TellSlack"/>: the shortest tell she can walk out of.</summary>
        public float ReactSeconds = 0.3f;
        /// <summary>m/s she walks out of a shape (her run speed, <see cref="FeelValues.RunSpeed"/>).</summary>
        public float HerSpeed = 6f;
        public float TellSlack = 0.1f;
        /// <summary>m: her footprint (<see cref="Placeholders.PlayerRadius"/>), for the floors.</summary>
        public float HerRadius = 0.5f;

        // ---------------- The recorder ----------------
        /// <summary>m/s under which she counts as standing (R7 5.2's 2.1: 35 % of her run speed of 6, work order 143's accurate share).</summary>
        public float SlowBelow = 2.1f;
        /// <summary>s after a hit of hers a daze's time counts as time she shot in.</summary>
        public float ShotWindow = 0.3f;

        // ---------------- Its look: the lean (the lancer's) and the roar ----------------
        /// <summary>Degrees it leans forward through a Ram's wind-up (over its first <see cref="LeanShare"/>) and its run.</summary>
        public float LeanDegrees = 14f;
        public float LeanShare = 0.5f;
        /// <summary>Degrees it rears back while it roars.</summary>
        public float RoarDegrees = -12f;
        /// <summary>s it takes to stand up again after a lean.</summary>
        public float RaiseSeconds = 0.3f;

        // ---------------- The hurt guard (R6 7.1; work order 147 builds the boss's) ----------------
        /// <summary>s after a hit of the Ram's in which nothing hurts her again (R6 7.1: 0.8 s).</summary>
        public float HurtGuardSeconds = 0.8f;
        /// <summary>s of one flicker of her body while it lasts: hidden for half of it, shown for the other half.</summary>
        public float HurtGuardFlicker = 0.1f;

        [NonSerialized] EnemyAttackDefinition[] _calm, _angry;

        /// <summary>Its three attacks as they are in phase 1, or in phase 2 (each wind-up shorter, never under its floor). Made once.</summary>
        public EnemyAttackDefinition[] Attacks(bool phaseTwo)
        {
            if (_calm == null)
            {
                _calm = new[] { RamAttack, StompAttack, StonesAttack };
                _angry = new EnemyAttackDefinition[_calm.Length];
                for (int i = 0; i < _calm.Length; i++)
                {
                    EnemyAttackDefinition a = _calm[i].Copy();
                    a.Windup = GraveRamRules.PhaseTwoWindupOf(_calm[i], this);
                    _angry[i] = a;
                }
            }
            return phaseTwo ? _angry : _calm;
        }

        /// <summary>A stone's burst delay in this phase (phase 2 shorter, never under its floor).</summary>
        public float StoneBurst(bool phaseTwo) =>
            phaseTwo ? Math.Max(StoneBurstAfter * PhaseTwoWindup, GraveRamRules.TellFloor(StonesAttack, this)) : StoneBurstAfter;

        /// <summary>The gap between attacks in this phase.</summary>
        public float GapOf(bool phaseTwo) => phaseTwo ? PhaseTwoGap : Gap;
    }

    /// <summary>How a Ram ended (work order 149).</summary>
    public enum RamEnd
    {
        /// <summary>Still running, or never begun.</summary>
        None,
        /// <summary>It met her: her damage (none in the dash's protected part or the hurt guard), then it stops and recovers.</summary>
        MetHer,
        /// <summary>Its body met stone (a pier, a wall, a shut door, a prop tall or low): it stops there, dazed.</summary>
        Crashed,
        /// <summary>It ran its length, or to the end of the walkable ground, with no stone: it recovers.</summary>
        Missed,
    }

    /// <summary>The Grave Ram's rules, with no engine in them (work order 149).</summary>
    public static class GraveRamRules
    {
        /// <summary>
        /// The shortest tell she can walk out of (R6 section 5): <see cref="GraveRamValues.ReactSeconds"/>, plus the walk out of the shape
        /// from the worst place she can stand at its tell's start at <see cref="GraveRamValues.HerSpeed"/>, plus
        /// <see cref="GraveRamValues.TellSlack"/>. The Ram's strip: from its middle line, its half width and her radius. The Stomp: from
        /// against its body (its radius and hers), out of its circle. A stone: from its middle, its radius and hers.
        /// </summary>
        public static float TellFloor(EnemyAttackDefinition a, GraveRamValues v)
        {
            if (a == null || v == null) return 0f;
            float walk;
            if (a.Name == GraveRamValues.Ram) walk = a.HalfWidth + v.HerRadius;
            else if (a.Name == GraveRamValues.Stomp) walk = Math.Max(0f, a.Radius + v.HerRadius - (v.BodyRadius + v.HerRadius));
            else walk = a.Radius + v.HerRadius;
            return v.ReactSeconds + walk / Math.Max(0.1f, v.HerSpeed) + v.TellSlack;
        }

        /// <summary>
        /// An attack's wind-up in phase 2: <see cref="GraveRamValues.PhaseTwoWindup"/> times its own, never under its floor (the order:
        /// never a tell shorter than walking out of its shape needs). The Grave Stones' wind-up is the throwing, the stones
        /// <see cref="GraveRamValues.StonesApart"/> apart, and is not shortened: each stone's own tell, its burst delay, is
        /// (<see cref="GraveRamValues.StoneBurst"/>).
        /// </summary>
        public static float PhaseTwoWindupOf(EnemyAttackDefinition a, GraveRamValues v) =>
            a == null ? 0f : a.Name == GraveRamValues.Stones ? a.Windup : Math.Max(a.Windup * v.PhaseTwoWindup, TellFloor(a, v));

        /// <summary>
        /// How far its middle runs on a Ram along its line: to where its body meets stone (<paramref name="stone"/> m along, infinity for
        /// none within its length), or to the end of the walkable ground (<paramref name="walkable"/>), or its whole length, the nearest.
        /// <paramref name="crash"/>: its body meets stone there (stone no farther than the ground's end and <paramref name="edgeSlack"/>):
        /// the run ends in a crash. The walkable ground's end with no stone is a miss.
        /// </summary>
        public static float RunLength(float length, float stone, float walkable, float edgeSlack, out bool crash)
        {
            crash = stone < length && stone <= walkable + edgeSlack;
            // Its body cannot pass the walkable ground's end: a crash on stone just past it stops it there.
            float end = crash ? Math.Min(stone, walkable) : Math.Min(length, walkable);
            return Math.Max(0f, end);
        }

        /// <summary>
        /// The Ram's strip as drawn and as judged (work order 142's rule: a drawn edge is the judged edge): from its middle as the run
        /// starts, along its line, to the front of its body where its middle stops (<paramref name="run"/> plus <paramref name="front"/>).
        /// </summary>
        public static float StripLength(float run, float front) => Math.Max(0f, run) + Math.Max(0f, front);

        /// <summary>
        /// Does her footprint (a circle of <paramref name="herRadius"/> at <paramref name="her"/>) touch the strip from
        /// <paramref name="from"/> along the unit <paramref name="dir"/>, <paramref name="length"/> long, <paramref name="halfWidth"/> to a
        /// side, cut square at both ends: the rectangle drawn, nothing past it.
        /// </summary>
        public static bool InStrip(GroundPoint from, GroundPoint dir, float length, float halfWidth, GroundPoint her, float herRadius)
        {
            GroundPoint to = her - from;
            float along = to.Dot(dir);
            float side = to.X * dir.Z - to.Z * dir.X;
            float ca = along < 0f ? 0f : along > length ? length : along;
            float cs = side < -halfWidth ? -halfWidth : side > halfWidth ? halfWidth : side;
            float da = along - ca, ds = side - cs;
            return da * da + ds * ds <= herRadius * herRadius + 1e-6f;
        }

        /// <summary>
        /// Would a Ram begun now reach her where she stands: does its strip, as its line is cut (its body's run <paramref name="run"/>
        /// and its front), touch her standing <paramref name="distance"/> straight ahead? A Ram whose line ends in stone or at the walkable
        /// ground's end short of her is not begun (work order 146's fault, a parked boss, must not come back as a parked Ram; nor a Ram
        /// that crashes on a pier's corner while she stands still behind it, a free daze).
        /// </summary>
        public static bool RamReachesHer(float run, float front, float distance, float herRadius) =>
            distance <= StripLength(run, front) + herRadius + 1e-4f;

        /// <summary>
        /// Does a box on the screen (layout px, y down: left, top, right, bottom) stand wholly inside a view <paramref name="viewWidth"/> by
        /// <paramref name="viewHeight"/>, <paramref name="margin"/> inside its edges, and clear by the margin of the two plates the interface
        /// draws over the game (<paramref name="plate"/>, the top left; <paramref name="strip"/>, the skill strip; none when its width or
        /// height is 0)?
        /// </summary>
        public static bool BoxOnScreen(float left, float top, float right, float bottom, float viewWidth, float viewHeight,
                                       UiBox plate, UiBox strip, float margin)
        {
            if (left < margin || top < margin || right > viewWidth - margin || bottom > viewHeight - margin) return false;
            return !Overlaps(left, top, right, bottom, plate, margin) && !Overlaps(left, top, right, bottom, strip, margin);
        }

        static bool Overlaps(float left, float top, float right, float bottom, UiBox b, float margin)
        {
            if (b.W <= 0f || b.H <= 0f) return false;
            return left < b.Right + margin && right > b.X - margin && top < b.Bottom + margin && bottom > b.Y - margin;
        }

        /// <summary>The skill strip's keep-out: across the strip's whole width (<paramref name="wholeStrip"/>), from <see cref="GraveRamValues.StripKeepOut"/> over the bottom to the bottom.</summary>
        public static UiBox StripKeepOut(UiBox wholeStrip, float viewHeight, GraveRamValues v)
        {
            float top = Math.Min(wholeStrip.Y, viewHeight - Math.Max(0f, v.StripKeepOut));
            return new UiBox(wholeStrip.X, top, wholeStrip.W, viewHeight - top);
        }

        /// <summary>When the stone <paramref name="i"/> (from 0) of a throw begun at <paramref name="start"/> appears.</summary>
        public static float StoneAppears(int i, float start, GraveRamValues v) => start + Math.Max(0, i) * v.StonesApart;

        /// <summary>0 to 1, how far it leans: over the first share of a Ram's wind-up to all of it, held through the rest and the run.</summary>
        public static float Lean(float windupProgress, GraveRamValues v)
        {
            float share = v.LeanShare <= 1e-4f ? 1e-4f : v.LeanShare;
            float p = windupProgress / share;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        /// <summary>Is the hurt guard on, <paramref name="since"/> s after the hit that began it (none: negative)?</summary>
        public static bool Guards(float since, GraveRamValues v) => since >= 0f && since < v.HurtGuardSeconds;

        /// <summary>Is her body shown at this moment of the guard (hidden for the first half of each flicker)?</summary>
        public static bool GuardShows(float since, GraveRamValues v)
        {
            if (!Guards(since, v) || v.HurtGuardFlicker <= 1e-4f) return true;
            float k = since / v.HurtGuardFlicker;
            return k - (float)Math.Floor(k) >= 0.5f;
        }
    }

    /// <summary>What the Grave Ram knows when it may choose an attack (work order 149): one frame of its chase.</summary>
    public struct GraveRamSituation
    {
        /// <summary>Game time now, and when its fight woke it.</summary>
        public float Now, WokeAt;
        /// <summary>m from her.</summary>
        public float Distance;
        /// <summary>Its whole body on his screen (<see cref="GraveRamRules.BoxOnScreen"/>).</summary>
        public bool OnScreen;
        /// <summary>Its line to her clear: no wall, shut door or tall prop at its sight height, and the walkable ground unbroken (a low prop is a hole in it).</summary>
        public bool LineClear;
        /// <summary>A Ram begun now would reach her (<see cref="GraveRamRules.RamReachesHer"/>).</summary>
        public bool RamReaches;
        /// <summary>The Stomp's circle on his screen on her side: its edge toward her and to her sides, the edge she walks out over.</summary>
        public bool StompSeen;
        /// <summary>It is in phase 2.</summary>
        public bool PhaseTwo;
    }

    /// <summary>
    /// What the Grave Ram decides, with no engine in it (work order 149): which attack, when. Nothing before <see cref="GraveRamValues.FirstAttackAfter"/>
    /// from the wake, nothing while it is off his screen, nothing within the gap after a recovery (the caller holds that). Eligible:
    /// the Ram when she is in its band, its line clear and a Ram would reach her, and <see cref="GraveRamValues.RamRest"/> since the last
    /// Ram ended; the Stomp within its reach, its circle on his screen, never twice in a row unless <see cref="GraveRamValues.StompAgain"/>
    /// has passed since the last (she stays against it, and nothing else can be begun); the Grave Stones beyond
    /// <see cref="GraveRamValues.StonesBeyond"/> with its line not clear, <see cref="GraveRamValues.StonesRest"/> since the last throw began.
    /// The opening teaches: Ram, Ram, then the Stomp if she is close, else the Stones; a slot whose attack is not eligible gives way to
    /// the draw. Then equal-weight draws among the eligible, from a seeded source (xorshift), so a fight can be replayed.
    /// </summary>
    public sealed class GraveRamTactics
    {
        uint _state = 0x9E3779B9u;
        int _opening;
        string _last;
        float _lastRamEnd = float.NegativeInfinity;
        float _lastStones = float.NegativeInfinity;
        float _lastStomp = float.NegativeInfinity;
        readonly EnemyAttackDefinition[] _eligible = new EnemyAttackDefinition[3];

        /// <summary>The opening's slots taken (0 to 3).</summary>
        public int Opening => _opening;
        /// <summary>The last attack begun, or null.</summary>
        public string Last => _last;
        public float LastRamEnd => _lastRamEnd;
        public float LastStones => _lastStones;
        /// <summary>Draws made from the seeded source.</summary>
        public int Draws { get; private set; }

        /// <summary>Seeds its source (the floor's seed): the same seed gives the same draws.</summary>
        public void Seed(ulong seed)
        {
            // splitmix64's finish, so near seeds start far apart
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            uint s = (uint)(z ^ (z >> 32));
            _state = s == 0u ? 0x9E3779B9u : s;
        }

        /// <summary>A Ram ended now (a crash, a miss, it met her): the rest to the next is counted from here.</summary>
        public void RamEnded(float now) => _lastRamEnd = now;

        /// <summary>An attack began now: the opening's slot is taken, it is the last, a throw's rest starts.</summary>
        public void Began(EnemyAttackDefinition a, float now)
        {
            if (a == null) return;
            if (_opening < 3) _opening++;
            _last = a.Name;
            if (a.Name == GraveRamValues.Stones) _lastStones = now;
            if (a.Name == GraveRamValues.Stomp) _lastStomp = now;
        }

        /// <summary>May a Stomp be begun next, as far as its rule of twice in a row goes.</summary>
        public bool StompMayFollow(float now, GraveRamValues v) => _last != GraveRamValues.Stomp || now - _lastStomp >= v.StompAgain;

        /// <summary>May it attack at all now: woken long enough ago and on his screen.</summary>
        public static bool MayStart(GraveRamSituation s, GraveRamValues v) => s.OnScreen && s.Now >= s.WokeAt + v.FirstAttackAfter;

        /// <summary>Is this attack eligible now, by the rules above?</summary>
        public bool Eligible(EnemyAttackDefinition a, GraveRamSituation s, GraveRamValues v)
        {
            if (a == null || !MayStart(s, v)) return false;
            switch (a.Name)
            {
                case GraveRamValues.Ram:
                    return s.Distance >= a.MinDistance && s.Distance <= a.MaxDistance && s.LineClear && s.RamReaches
                           && s.Now - _lastRamEnd >= v.RamRest;
                case GraveRamValues.Stomp:
                    return s.Distance >= a.MinDistance && s.Distance <= a.MaxDistance && s.StompSeen && StompMayFollow(s.Now, v);
                case GraveRamValues.Stones:
                    return s.Distance > v.StonesBeyond && s.Distance <= a.MaxDistance && !s.LineClear && s.Now - _lastStones >= v.StonesRest;
            }
            return false;
        }

        /// <summary>The attack to begin now from <paramref name="attacks"/> (Ram, Stomp, Stones of the phase), or null to keep walking. Draws only when it returns one.</summary>
        public EnemyAttackDefinition Choose(GraveRamSituation s, GraveRamValues v, EnemyAttackDefinition[] attacks)
        {
            if (attacks == null || attacks.Length < 3 || !MayStart(s, v)) return null;
            EnemyAttackDefinition ram = attacks[0], stomp = attacks[1], stones = attacks[2];
            if (_opening < 2 && Eligible(ram, s, v)) return ram;
            if (_opening == 2)
            {
                if (Eligible(stomp, s, v)) return stomp;
                if (s.Distance > stomp.MaxDistance && Eligible(stones, s, v)) return stones;
            }
            int n = 0;
            for (int i = 0; i < 3; i++) if (Eligible(attacks[i], s, v)) _eligible[n++] = attacks[i];
            if (n == 0) return null;
            if (n == 1) return _eligible[0];
            Draws++;
            return _eligible[(int)(Next() % (uint)n)];
        }

        uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }

    /// <summary>
    /// The Grave Ram's line in the log (work order 149), once a fight as it ends: <see cref="Fight"/>, its name the Ram's title, with the
    /// fight's seconds from the wake, how it ended, her damage taken by attack, the Rams begun and how each ended (crashed, missed, met
    /// her), what the crashes were on, the counters (Stopping Shot dropped a Ram), the crash dazes' seconds and those she shot in, the
    /// damage she dealt in them, her share of the fight below <see cref="GraveRamValues.SlowBelow"/>, the hits the hurt guard spared,
    /// and the phase-2 seconds.
    /// </summary>
    public static class GraveRamEvents
    {
        public const string Fight = "elite_fight";
        public const string Died = "died";
        public const string HerDeath = "her death";
        public const string Removed = "removed";

        public static string Seconds(float s) => Math.Max(0f, s).ToString("0.0", CultureInfo.InvariantCulture);
        public static string Share(float s) => Math.Max(0f, Math.Min(1f, s)).ToString("0.00", CultureInfo.InvariantCulture);
        public static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
