using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The lancer's numbers (work order 108, 2026-11-24; all the producer's first guesses, his to move after meeting it). He
    /// ticked "Fiender og boss" and, asked by name, said yes to the producer's "Lansereren (ny fiende)": "Etasje 4: stormer i
    /// rett linje langveisfra, varslet på kroppen, og blir ør når den treffer en vegg eller rekvisitt. Lages av
    /// skjelettmodellene som finnes." It answers his "skulle ønske man fikk brukt mer av rommet. nå blir det bare at man kiter
    /// det rundt seg" (2026-11-23): standing in the open has a price, and cover is something she uses on purpose.
    ///
    /// It keeps far from her (<see cref="KeepNear"/> to <see cref="KeepFar"/>), where it has a clear straight line to her. Then
    /// the wind-up of its <see cref="ChargeAttack"/>, read on its BODY (decision 0010: it lowers its lance and leans, and the
    /// glint of melee you read grows on it, blue, for Stopping Shot counters it; nothing on the floor); through the wind-up it
    /// keeps turning to her, and the line it charges down is the one it faces as the wind-up ENDS (<see cref="LancerRules.ChargeLine"/>).
    /// It runs that line at <see cref="EnemyAttackDefinition.Rush"/> up to its <see cref="EnemyAttackDefinition.Length"/>, never
    /// turning (<see cref="LancerCharge"/>). It ends in one of three ways (<see cref="LancerEnd"/>): it meets her (her damage,
    /// then it stops and recovers), it meets a wall, a shut door or a prop tall or low (it stops there and is dazed for
    /// <see cref="CrashDaze"/>, by its stagger bar emptied at once, so it takes the stagger's extra damage while dazed), or it
    /// runs its whole length and meets nothing (it stops and recovers). Other enemies neither stop it nor are hurt by it.
    /// </summary>
    [Serializable]
    public sealed class LancerValues
    {
        /// <summary>Its charge's name, as the log, the sounds and the summary write it.</summary>
        public const string Charge = "Lance Charge";

        /// <summary>
        /// Its body and its one attack. Health 1600 in the practice room (between the practice shooter's 800 and the practice
        /// brute's 12000 of the enemy settings asset; a floor's is <see cref="LevelValues.LancerHealth"/>: 800, between a floor
        /// shooter's 400 and a floor brute's 1200). Speed 3.2 m/s walking (a shooter's 3, a little more, as it walks to its
        /// distance). It wakes from farther than most (16 m: it fights from 12 to 16 m) and gives up at 28 m.
        /// </summary>
        public EnemyValues Enemy = new EnemyValues
        {
            Health = 1600, MoveSpeed = 3.2f, AggroRange = 16f, LeashRange = 28f, RespawnDelay = 4f,
            Attacks = new[] { ChargeAttack() },
        };

        /// <summary>
        /// The charge (work order 108): chosen when she is 10 to 17 m from it and its line to her is clear; a wind-up of 1.1 s on
        /// its body; then it runs at 16 m/s up to 18 m. 15 to her when it meets her: a brute's Shield bash
        /// (<see cref="MeleeAttacks.Brute"/>), times the floor's scale on a floor as every floor enemy's strike. The line it hits
        /// along is 0.6 m to a side of its middle (her footprint is met within that and her radius), narrow, so a step aside
        /// takes her out of it. Blue: Stopping Shot from its front during the wind-up drops it and dazes it, as it does the
        /// boss's Charge (<see cref="BossValues.Charge"/>). Recovery 1 s: where it stopped, after it met her or ran its length.
        /// </summary>
        public static EnemyAttackDefinition ChargeAttack() => new EnemyAttackDefinition
        {
            Name = Charge, Shape = EnemyAttackShape.Line, MinDistance = 10f, MaxDistance = 17f,
            Windup = 1.1f, Recovery = 1f, Damage = 15, Length = 18f, HalfWidth = 0.6f, Rush = 16f, Counterable = true,
        };

        // ---------------- Where it stands (the shooter's way of keeping its distance, farther) ----------------
        public float KeepNear = 12f;        // m: it backs off to at least this far from her...
        public float KeepFar = 16f;         // m: ...and comes no nearer than needed: it walks to her when she is farther than this
        public float BackOffUnder = 10f;    // m: it starts to back off when she is nearer than this
        public float SightHeight = 0.5f;    // m over the ground of the line it sees her along (the shooter's), for walls, shut doors and tall props
        public float LineCheckEvery = 0.2f; // s between two looks along its line to her while it keeps its distance (not every frame)

        // ---------------- The charge's end (work order 108) ----------------
        /// <summary>
        /// s it is dazed when its charge meets a wall, a shut door or a prop (the order: about 1.5 s). The daze is its stagger
        /// bar's (<see cref="Stagger"/>'s <see cref="StaggerValues.DazeSeconds"/>, the same number): the crash empties the bar at
        /// once, so it is staggered as the game's stagger is and takes <see cref="StaggerValues.DazedDamageMultiplier"/> while dazed.
        /// </summary>
        public float CrashDaze
        {
            get => Stagger != null ? Stagger.DazeSeconds : 0f;
            set { if (Stagger != null) Stagger.DazeSeconds = value; }
        }
        /// <summary>
        /// Its stagger bar: 60 points (a guard's 100, less; the warder's 40, more), her stagger skills empty it as any bar's;
        /// dazed 1.5 s (the crash's daze, <see cref="CrashDaze"/>), its damage while dazed as the guard's (1.5 times).
        /// </summary>
        public StaggerValues Stagger = new StaggerValues { Max = 60f, DazeSeconds = 1.5f, DazedDamageMultiplier = 1.5f };
        public float StopShort = 0.5f;      // m short of what stops it that its middle stops: its body's front, and the lance's point, at the thing
        public float StopProbeHeight = 0.35f; // m over the ground of the middle of the ball it looks ahead with for a prop: under the low props' top
        public float StopProbeRadius = 0.3f;  // m, that ball: a little under its body's half width, so a prop beside its line lets it pass

        // ---------------- Its look (work order 108: the skeleton warrior's model, tinted and larger, with a lance made here) ----------------
        public float BodyRadius = 0.35f;    // m, the capsule: a brute's
        public float BodyHeight = 2.1f;     // m
        public float Shown = 1.15f;         // it is shown this times its model's size (the skeleton warrior's 1.8 m: 2.07 m)
        public float LanceLength = 2.8f;    // m, the lance as shown (scaled with the body)
        public float LanceThickness = 0.07f; // m across its shaft
        public float LanceHeight = 1.05f;   // m over the ground where it is held (in the model's size before Shown)
        public float LanceSide = 0.28f;     // m to its right of its middle where it is held
        public float LanceGrip = 0.6f;      // m of the lance behind the hands
        public float LanceRestDegrees = 72f; // degrees over level the lance points at rest: held upright, a little forward
        public float LanceDownDegrees = 2f;  // degrees over level it points lowered: level at her, the tip a little up
        public float LeanDegrees = 16f;     // degrees it leans forward lowered and through the charge
        public float LowerShare = 0.6f;     // the share of the wind-up over which it lowers and leans; the rest it holds, lowered
        public float RaiseSeconds = 0.35f;  // s it takes to stand up again and raise the lance after the charge or a daze
    }

    /// <summary>How a lancer's charge ended (work order 108).</summary>
    public enum LancerEnd
    {
        /// <summary>Not ended: still running, or never begun.</summary>
        None,
        /// <summary>It met her: her damage (none in the dash's protected part), then it stops and recovers.</summary>
        MetHer,
        /// <summary>It met a wall, a shut door or a prop, tall or low: it stops there, dazed (<see cref="LancerValues.CrashDaze"/>).</summary>
        Crashed,
        /// <summary>It ran its whole length and met nothing: it stops and recovers.</summary>
        RanOut,
    }

    /// <summary>What stopped a lancer's charge (work order 108), as the log writes it.</summary>
    public enum LancerStopper
    {
        /// <summary>Nothing stops it within its length.</summary>
        None,
        Wall,
        /// <summary>A room's door, shut (work order 086).</summary>
        Door,
        TallProp,
        LowProp,
        /// <summary>The end of the walkable ground with nothing the physics sees (the practice room's edge).</summary>
        Edge,
    }

    /// <summary>What a lancer does between its charges, keeping its distance (work order 108).</summary>
    public enum LancerMove
    {
        /// <summary>Stand, facing her: in its distance, with a clear line.</summary>
        Hold,
        /// <summary>Walk toward her: she is farther than <see cref="LancerValues.KeepFar"/>, or something stands in its line.</summary>
        Closer,
        /// <summary>Walk away from her: she is nearer than <see cref="LancerValues.BackOffUnder"/>, until <see cref="LancerValues.KeepNear"/>.</summary>
        Away,
    }

    /// <summary>The lancer's rules (work order 108), with no engine in them.</summary>
    public static class LancerRules
    {
        /// <summary>
        /// Where it goes between charges: away when she is nearer than <see cref="LancerValues.BackOffUnder"/> (and on until
        /// <see cref="LancerValues.KeepNear"/> once backing off), toward her when she is farther than <see cref="LancerValues.KeepFar"/>
        /// or its line to her is not clear (the way round what stands between opens it), else it holds.
        /// </summary>
        public static LancerMove Move(float distance, bool lineClear, bool backingOff, LancerValues v)
        {
            if (distance < v.BackOffUnder || (backingOff && distance < v.KeepNear)) return LancerMove.Away;
            if (distance > v.KeepFar || !lineClear) return LancerMove.Closer;
            return LancerMove.Hold;
        }

        /// <summary>
        /// May it start its charge: its line to her clear, and she in the charge's band (the brain asks the band too, and that
        /// she and it are alive). Backing off to its distance it may charge as soon as she is in the band.
        /// </summary>
        public static bool MayCharge(float distance, bool lineClear, EnemyAttackDefinition charge) =>
            charge != null && lineClear && distance >= charge.MinDistance && distance <= charge.MaxDistance;

        /// <summary>
        /// The way it faces through the wind-up: at her, wherever she goes (the line is not fixed until the wind-up ends); the way
        /// it faced when she stands on it.
        /// </summary>
        public static GroundPoint Facing(GroundPoint lancer, GroundPoint her, GroundPoint faced)
        {
            GroundPoint d = her - lancer;
            float length = d.Length;
            if (length < 1e-4f)
            {
                float f = faced.Length;
                return f < 1e-6f ? new GroundPoint(0f, 1f) : faced * (1f / f);
            }
            return d * (1f / length);
        }

        /// <summary>The line it charges down: the way it faces as the wind-up ends (<see cref="Facing"/>, at her where she is then).</summary>
        public static GroundPoint ChargeLine(GroundPoint lancer, GroundPoint herAtWindupEnd, GroundPoint faced) => Facing(lancer, herAtWindupEnd, faced);

        /// <summary>
        /// How far it may run (the boss's rule, <see cref="BossTactics.ChargeLength"/>): its length, or, when something stops it
        /// nearer (<paramref name="stopAt"/>, the first wall, shut door, prop or end of the walkable ground on its line),
        /// <paramref name="stopShort"/> short of it; <paramref name="cut"/> is then true, and the charge ends in a crash.
        /// </summary>
        public static float ChargeLength(float length, float stopAt, float stopShort, out bool cut)
        {
            cut = stopAt < length;
            return BossTactics.ChargeLength(length, stopAt, stopAt, stopShort);
        }

        /// <summary>
        /// Was what stopped it between it and her: is she on its line (within its half width and her radius) and farther along
        /// it than where it crashed? Then cover stopped it (the summary's "stopped by a wall or prop"); else she had stepped out
        /// of its way before it crashed (avoided).
        /// </summary>
        public static bool CoverStoppedIt(GroundPoint from, GroundPoint dir, float crashedAt, float halfWidth, GroundPoint her, float herRadius)
        {
            GroundPoint to = her - from;
            float along = to.Dot(dir);
            if (along <= crashedAt) return false;
            GroundPoint side = to - dir * along;
            return side.Length <= halfWidth + herRadius;
        }

        /// <summary>
        /// How long it stands after a charge's end: dazed <see cref="LancerValues.CrashDaze"/> after a crash; its recovery
        /// (<see cref="EnemyAttackDefinition.Recovery"/>) after meeting her or running its length; nothing for none.
        /// </summary>
        public static float StandsAfter(LancerEnd end, LancerValues v, EnemyAttackDefinition charge) =>
            end == LancerEnd.Crashed ? v.CrashDaze : end == LancerEnd.MetHer || end == LancerEnd.RanOut ? (charge != null ? charge.Recovery : 0f) : 0f;

        /// <summary>
        /// 0 to 1, how far the lance is lowered and the body leaned: over the first <see cref="LancerValues.LowerShare"/> of the
        /// wind-up from nothing to all of it, held through the rest and the charge.
        /// </summary>
        public static float Lowered(float windupProgress, LancerValues v)
        {
            float share = v.LowerShare <= 1e-4f ? 1e-4f : v.LowerShare;
            float p = windupProgress / share;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        /// <summary>The lance's angle over level at this share lowered: from <see cref="LancerValues.LanceRestDegrees"/> to <see cref="LancerValues.LanceDownDegrees"/>.</summary>
        public static float LanceDegrees(float lowered, LancerValues v) => v.LanceRestDegrees + (v.LanceDownDegrees - v.LanceRestDegrees) * Clamp01(lowered);

        /// <summary>The body's lean forward at this share lowered.</summary>
        public static float LeanDegrees(float lowered, LancerValues v) => v.LeanDegrees * Clamp01(lowered);

        /// <summary>The log's word for what stopped it ("wall", "door", "tall prop", "low prop", "edge"; "" for none).</summary>
        public static string StopperWord(LancerStopper s) =>
            s == LancerStopper.Wall ? "wall" : s == LancerStopper.Door ? "door" : s == LancerStopper.TallProp ? "tall prop"
            : s == LancerStopper.LowProp ? "low prop" : s == LancerStopper.Edge ? "edge" : "";

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>
    /// One lancer's charge (work order 108), with no engine in it: begun at the wind-up's end from where it stands down the line
    /// it then faced (<see cref="LancerRules.ChargeLine"/>), at its speed, as far as it may run (<see cref="LancerRules.ChargeLength"/>).
    /// It never turns. Each step says where it is now and whether it ended: it met her when its body passed over her footprint
    /// on the way (the boss's rule, <see cref="BossTactics.RushPasses"/>), else it crashed or ran out when it reached its end.
    /// Times are game seconds; it never reads a clock.
    /// </summary>
    public sealed class LancerCharge
    {
        public bool Running { get; private set; }
        public GroundPoint From { get; private set; }
        public GroundPoint Direction { get; private set; }
        /// <summary>m it may run: its length, or short of what stops it.</summary>
        public float Length { get; private set; }
        /// <summary>Something stops it at its end: it ends in a crash, not by running out.</summary>
        public bool Cut { get; private set; }
        public LancerStopper Stopper { get; private set; }
        public float Speed { get; private set; }
        public float StartedAt { get; private set; }
        public LancerEnd End { get; private set; }
        /// <summary>m it had run when it ended.</summary>
        public float EndedAfter { get; private set; }

        /// <summary>
        /// Begins it now: from <paramref name="from"/> down <paramref name="direction"/> (made a unit), at <paramref name="speed"/>,
        /// up to <paramref name="length"/>, cut <paramref name="stopShort"/> short of a stop <paramref name="stopAt"/> m along its
        /// line when that is nearer (infinity or more than its length for none).
        /// </summary>
        public void Begin(GroundPoint from, GroundPoint direction, float speed, float length, float stopAt, float stopShort, LancerStopper stopper, float now)
        {
            float d = direction.Length;
            Direction = d < 1e-6f ? new GroundPoint(0f, 1f) : direction * (1f / d);
            From = from;
            Speed = Math.Max(0f, speed);
            Length = LancerRules.ChargeLength(Math.Max(0f, length), stopAt, stopShort, out bool cut);
            Cut = cut;
            Stopper = cut ? stopper : LancerStopper.None;
            StartedAt = now;
            End = LancerEnd.None;
            EndedAfter = 0f;
            Running = true;
        }

        /// <summary>m along its line it should have run by now.</summary>
        public float Run(float now) => Math.Min(Length, Math.Max(0f, now - StartedAt) * Speed);

        /// <summary>Where it should be now on its line.</summary>
        public GroundPoint At(float now) => From + Direction * Run(now);

        /// <summary>
        /// One step, from where it was (<paramref name="before"/>) to where it is now (<paramref name="after"/>), with her where she
        /// is: <see cref="LancerEnd.MetHer"/> when its line of <paramref name="halfWidth"/> passed over her footprint on the way and
        /// she may be met (alive), else at its end <see cref="LancerEnd.Crashed"/> when it was cut and
        /// <see cref="LancerEnd.RanOut"/> when not, else <see cref="LancerEnd.None"/>. <paramref name="blocked"/>: it could not
        /// move as far as it should (the walkable ground ended under it): it crashed there.
        /// </summary>
        public LancerEnd Step(GroundPoint before, GroundPoint after, float now, GroundPoint her, float herRadius, float halfWidth, bool herThere, bool blocked = false)
        {
            if (!Running) return LancerEnd.None;
            if (herThere && BossTactics.RushPasses(before, after, halfWidth, her, herRadius)) return Finish(LancerEnd.MetHer, after);
            if (blocked)
            {
                Cut = true;
                if (Stopper == LancerStopper.None) Stopper = LancerStopper.Edge;
                return Finish(LancerEnd.Crashed, after);
            }
            if (Run(now) >= Length - 1e-4f) return Finish(Cut ? LancerEnd.Crashed : LancerEnd.RanOut, after);
            return LancerEnd.None;
        }

        /// <summary>Stops it where it is with no end of its own (it was dazed or killed mid-run).</summary>
        public void Stop()
        {
            Running = false;
        }

        LancerEnd Finish(LancerEnd end, GroundPoint at)
        {
            Running = false;
            End = end;
            EndedAfter = (at - From).Dot(Direction);
            return end;
        }
    }
}
