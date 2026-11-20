using System;

namespace Roguelike.Rules
{
    /// <summary>The three things that fly at her (work order 036, decision 0010): the shooter's arrow, the caster's orb, the boss's wave.</summary>
    public enum ProjectileKind { Arrow, Orb, Wave }

    /// <summary>What one step of a projectile came to (<see cref="ProjectileRules.Step"/>).</summary>
    public enum ProjectileHit
    {
        /// <summary>It flew on.</summary>
        None,
        /// <summary>It met her: her footprint and its own touched somewhere along the step.</summary>
        MetHer,
        /// <summary>A wall or a tall prop (the cover query it was given) stopped it.</summary>
        Wall,
        /// <summary>It entered what she is in cover against, while that covers her from where it left.</summary>
        HerCover,
        /// <summary>It reached its longest flight.</summary>
        FlightOver,
    }

    /// <summary>How one projectile ended, as its owner reports it (<c>Enemy.ProjectileEnded</c>).</summary>
    public enum ProjectileEnd { MetHer, StoppedByCover, StoppedByWall, FlightOver, Spared }

    /// <summary>
    /// One thing that flies (work order 036, 2026-11-20; decision 0010: "what is shot or thrown at her flies: it has a
    /// speed and a size, she can step out of its way, and walls and tall props stop it"). It has a place, a direction, a
    /// speed, a size and a longest flight; who threw it and what it deals are the game's. Its footprint on the ground is a
    /// strip <see cref="HalfWidth"/> to each side of its middle, across the way it flies, grown by <see cref="Radius"/>
    /// all round: a circle of <see cref="Radius"/> for an arrow and an orb (no half width), a low wide band for the wave.
    /// Metres and metres a second.
    /// </summary>
    public struct Projectile
    {
        public ProjectileKind Kind;
        /// <summary>Where it left.</summary>
        public GroundPoint Origin;
        /// <summary>Where it is now.</summary>
        public GroundPoint At;
        /// <summary>The way it flies, of length 1.</summary>
        public GroundPoint Dir;
        public float Speed;
        public float Radius;
        public float HalfWidth;
        public float MaxFlight;
        /// <summary>Metres flown so far.</summary>
        public float Flown;
        /// <summary>It met her and passed (the dash's protected part, or spared): it meets her no more.</summary>
        public bool PassedHer;

        /// <summary>Across the way it flies, of length 1.</summary>
        public GroundPoint Side => new GroundPoint(-Dir.Z, Dir.X);
        public float FlightLeft => Math.Max(0f, MaxFlight - Flown);

        public static Projectile Make(ProjectileKind kind, GroundPoint from, GroundPoint dir, float speed, float radius, float halfWidth, float maxFlight)
        {
            float length = dir.Length;
            return new Projectile
            {
                Kind = kind, Origin = from, At = from, Dir = length > 1e-6f ? dir * (1f / length) : new GroundPoint(0f, 1f),
                Speed = Math.Max(0f, speed), Radius = Math.Max(0f, radius), HalfWidth = Math.Max(0f, halfWidth), MaxFlight = Math.Max(0f, maxFlight),
            };
        }
    }

    /// <summary>What one step came to: what happened, how far through the step (0 to 1), and where it then is.</summary>
    public struct ProjectileStep
    {
        public ProjectileHit Hit;
        public float Fraction;
        public GroundPoint At;
        /// <summary>For <see cref="ProjectileHit.HerCover"/>: would its way on have met her? Only then is it her cover that stopped it.</summary>
        public bool WouldHaveMetHer;
    }

    /// <summary>
    /// The rules of the things that fly (work order 036, 2026-11-20), with no engine in them.
    ///
    /// A step (<see cref="Step"/>) moves it its speed times the time, no farther than its flight has left. The step is
    /// swept, so a fast arrow passes neither through her nor through a thin wall between two frames: the wall query is
    /// asked along the whole step (the game's is <c>Cover</c>: walls, and props tall enough to stop shots); and she is met
    /// if her footprint (<see cref="Placeholders.PlayerRadius"/>, as every enemy attack takes her) and its own touch at any
    /// moment of the step, the two moving in straight lines over it. Whatever of the three (a wall, what she ducked behind,
    /// her) comes first along the step is what happens. A wave (one with a half width) meets her only where the part of it
    /// that reaches her has a clear line from where it left: a wall stops the part of the wave behind it.
    /// </summary>
    public static class ProjectileRules
    {
        const float Slack = 1e-3f;
        const int Search = 40;

        /// <summary>The shooter's arrow from a point along a direction (<see cref="ShooterValues"/>).</summary>
        public static Projectile Arrow(ShooterValues v, GroundPoint from, GroundPoint dir) =>
            Projectile.Make(ProjectileKind.Arrow, from, dir, v.ArrowSpeed, v.ArrowRadius, 0f, v.ArrowFlight);

        /// <summary>The caster's bone orb from a point along a direction (<see cref="CasterValues"/>).</summary>
        public static Projectile Orb(CasterValues v, GroundPoint from, GroundPoint dir) =>
            Projectile.Make(ProjectileKind.Orb, from, dir, v.OrbSpeed, v.OrbRadius, 0f, v.OrbFlight);

        /// <summary>
        /// The boss's wave from a point along a direction (<see cref="BossValues"/>): <see cref="BossValues.WaveWidth"/> wide
        /// from edge to edge, <see cref="BossValues.WaveDepth"/> from its middle line to its front and back.
        /// </summary>
        public static Projectile Wave(BossValues v, GroundPoint from, GroundPoint dir) =>
            Projectile.Make(ProjectileKind.Wave, from, dir, v.WaveSpeed, v.WaveDepth, Math.Max(0f, v.WaveWidth * 0.5f - v.WaveDepth), v.WaveFlight);

        /// <summary>
        /// One step of <paramref name="dt"/> seconds; <paramref name="p"/> is moved to where the step leaves it. She moves
        /// from <paramref name="herFrom"/> to <paramref name="herTo"/> over the step. <paramref name="walls"/> answers how far
        /// a line runs before a wall (null: nothing stops it). <paramref name="coverEntry"/>: metres along its way from where
        /// it is before it enters what she is in cover against, when that covers her from where it left; infinity when
        /// nothing does. <paramref name="ignoreHer"/>: it no longer looks for her (its strike is already told).
        /// </summary>
        public static ProjectileStep Step(ref Projectile p, float dt, GroundPoint herFrom, GroundPoint herTo, float herRadius,
                                          CoverLine walls, float coverEntry = float.PositiveInfinity, bool ignoreHer = false)
        {
            var result = new ProjectileStep { Hit = ProjectileHit.None, Fraction = 1f, At = p.At };
            float s = Math.Min(Math.Max(0f, p.Speed * dt), p.FlightLeft);
            if (s <= 1e-6f)
            {
                if (p.FlightLeft <= 1e-4f) result.Hit = ProjectileHit.FlightOver;
                return result;
            }
            GroundPoint p0 = p.At, p1 = p.At + p.Dir * s;

            float tWall = float.PositiveInfinity;
            if (walls != null)
            {
                // Asked a hair past the step's end, and a wall up to its very end counts (work order 039): a wall whose face
                // fell within that hair of the end was passed over, and the next step, starting inside it, never saw it. At
                // 13 m/s and frames of 0.3 ms a step is 4 mm, so that was a fair share of arrows through a wall.
                float clear = walls(p0, p0 + p.Dir * (s + Slack));
                if (clear <= s) tWall = Math.Max(0f, clear) / s;
            }
            float tCover = coverEntry < s ? Math.Max(0f, coverEntry) / s : float.PositiveInfinity;
            float tStop = Math.Min(1f, Math.Min(tWall, tCover));

            if (!ignoreHer && !p.PassedHer)
            {
                float tMeet = FirstMeeting(p, p0, p1, herFrom, herTo, herRadius, tStop);
                if (tMeet >= 0f && (p.HalfWidth <= 0f || ColumnClear(p, Lerp(herFrom, herTo, tMeet), walls)))
                {
                    p.At = p0 + p.Dir * (s * tMeet);
                    p.Flown += s * tMeet;
                    result.Hit = ProjectileHit.MetHer;
                    result.Fraction = tMeet;
                    result.At = p.At;
                    return result;
                }
            }
            if (tWall <= 1f && tWall <= tCover)
            {
                p.At = p0 + p.Dir * (s * tWall);
                p.Flown += s * tWall;
                result.Hit = ProjectileHit.Wall;
                result.Fraction = tWall;
                result.At = p.At;
                return result;
            }
            if (tCover <= 1f)
            {
                // Would it have met her had what she is behind not been there: its way on, as the old shot's line.
                result.WouldHaveMetHer = LineShot.Hits(p0, p0 + p.Dir, p.FlightLeft, p.HalfWidth + p.Radius, herTo, herRadius);
                p.At = p0 + p.Dir * (s * tCover);
                p.Flown += s * tCover;
                result.Hit = ProjectileHit.HerCover;
                result.Fraction = tCover;
                result.At = p.At;
                return result;
            }
            p.At = p1;
            p.Flown += s;
            result.At = p1;
            if (p.Flown >= p.MaxFlight - 1e-4f) result.Hit = ProjectileHit.FlightOver;
            return result;
        }

        /// <summary>
        /// The earliest moment (0 to <paramref name="until"/> of the step) at which its footprint and hers touch, the two
        /// moving in straight lines from where they are at the start of the step to where they are at its end; -1 when they
        /// do not. The square of the distance between them is convex in time, so its least is searched and then the first
        /// moment it comes within reach.
        /// </summary>
        public static float FirstMeeting(Projectile p, GroundPoint p0, GroundPoint p1, GroundPoint herFrom, GroundPoint herTo,
                                         float herRadius, float until = 1f)
        {
            if (until < 0f) return -1f;
            float reach = p.Radius + herRadius;
            float reach2 = reach * reach;
            GroundPoint a = p0 - herFrom;
            GroundPoint b = (p1 - herTo) - a;
            GroundPoint dir = p.Dir, side = p.Side;
            float w = p.HalfWidth;
            float Gap2(float t)
            {
                GroundPoint q = a + b * t;
                float along = q.Dot(dir);
                float across = Math.Max(0f, Math.Abs(q.Dot(side)) - w);
                return along * along + across * across;
            }
            if (Gap2(0f) <= reach2) return 0f;
            float lo = 0f, hi = until;
            for (int i = 0; i < Search; i++)
            {
                float m1 = lo + (hi - lo) / 3f, m2 = hi - (hi - lo) / 3f;
                if (Gap2(m1) <= Gap2(m2)) hi = m2;
                else lo = m1;
            }
            float least = (lo + hi) * 0.5f;
            if (Gap2(least) > reach2 && Gap2(until) > reach2) return -1f;
            if (Gap2(until) <= reach2 && Gap2(least) > reach2) least = until;
            // The first moment within reach: between the start (out of reach) and the least.
            float outT = 0f, inT = least;
            for (int i = 0; i < Search; i++)
            {
                float m = (outT + inT) * 0.5f;
                if (Gap2(m) <= reach2) inT = m;
                else outT = m;
            }
            return inT;
        }

        /// <summary>
        /// A wave's part that reaches her has a clear line, through the walls it was given, from where that part left to
        /// her: a wall stops the part of a wave behind it. Always clear with no walls.
        /// </summary>
        public static bool ColumnClear(Projectile p, GroundPoint her, CoverLine walls)
        {
            if (walls == null) return true;
            float across = (her - p.Origin).Dot(p.Side);
            float w = p.HalfWidth + p.Radius;
            across = across < -w ? -w : across > w ? w : across;
            return CoverRules.Clear(p.Origin + p.Side * across, her, walls);
        }

        /// <summary>
        /// Where an arrow is aimed (work order 036): at where she will be if she keeps her velocity for the arrow's flight
        /// time to her, times <paramref name="share"/>, the lead no longer than <paramref name="most"/>. The flight time is
        /// the true meeting time of an arrow of this speed with her keeping her velocity, or her distance over its speed
        /// when there is none (she outruns it). Standing, it is aimed at her.
        /// </summary>
        public static GroundPoint LeadAim(GroundPoint from, GroundPoint her, GroundPoint velocity, float speed, float share, float most)
        {
            if (share <= 0f || speed <= 0f || velocity.Dot(velocity) < 1e-8f || most <= 0f) return her;
            float t = MeetingTime(her - from, velocity, speed);
            GroundPoint lead = velocity * (t * share);
            float length = lead.Length;
            if (length > most) lead = lead * (most / length);
            return her + lead;
        }

        /// <summary>
        /// Seconds until something leaving the origin at <paramref name="speed"/> meets a point at <paramref name="offset"/>
        /// moving at <paramref name="velocity"/>: the least positive time with |offset + velocity t| = speed t; the distance
        /// over the speed when there is none.
        /// </summary>
        public static float MeetingTime(GroundPoint offset, GroundPoint velocity, float speed)
        {
            float fallback = speed > 0f ? offset.Length / speed : 0f;
            float a = velocity.Dot(velocity) - speed * speed;
            float b = 2f * offset.Dot(velocity);
            float c = offset.Dot(offset);
            if (Math.Abs(a) < 1e-6f)
            {
                if (Math.Abs(b) < 1e-6f) return fallback;
                float t = -c / b;
                return t > 0f ? t : fallback;
            }
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return fallback;
            float root = (float)Math.Sqrt(disc);
            float t1 = (-b - root) / (2f * a), t2 = (-b + root) / (2f * a);
            float best = float.PositiveInfinity;
            if (t1 > 0f) best = t1;
            if (t2 > 0f && t2 < best) best = t2;
            return float.IsPositiveInfinity(best) ? fallback : best;
        }

        /// <summary>
        /// The way of projectile <paramref name="index"/> (from 0) of a fan of <paramref name="count"/>,
        /// <paramref name="degreesApart"/> apart, the middle one along <paramref name="middle"/>. Of length 1.
        /// </summary>
        public static GroundPoint FanDirection(GroundPoint middle, int index, int count, float degreesApart)
        {
            float length = middle.Length;
            GroundPoint d = length > 1e-6f ? middle * (1f / length) : new GroundPoint(0f, 1f);
            if (count <= 1) return d;
            double a = (index - (count - 1) * 0.5) * degreesApart * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            return new GroundPoint(d.X * cos - d.Z * sin, d.X * sin + d.Z * cos);
        }

        static GroundPoint Lerp(GroundPoint a, GroundPoint b, float t) => a + (b - a) * t;
    }

    /// <summary>
    /// Which shot the shooter looses next, with things that fly on (work order 036): on <see cref="ShooterValues.FanFromFloor"/>
    /// and later, and for an elite on any floor, every <see cref="ShooterValues.FanEvery"/>th is a fan
    /// (<see cref="ShooterValues.ArrowFan"/>); an elite's other shots are volleys (<see cref="ShooterValues.Volley"/>); every
    /// other shot is one arrow (<see cref="ShooterValues.Arrow"/>).
    /// </summary>
    public sealed class ShooterTactics
    {
        /// <summary>The shots begun so far.</summary>
        public int Shots { get; private set; }

        /// <summary>Is shot number <paramref name="number"/> (counted from 1) a fan on this floor, for an elite or not?</summary>
        public static bool IsFan(int number, int floor, bool elite, ShooterValues v) =>
            v.FanEvery > 0 && number > 0 && number % v.FanEvery == 0 && (elite || floor >= v.FanFromFloor);

        /// <summary>The name of shot number <paramref name="number"/>.</summary>
        public static string NameOf(int number, int floor, bool elite, ShooterValues v) =>
            IsFan(number, floor, elite, v) ? ShooterValues.ArrowFan : elite ? ShooterValues.Volley : ShooterValues.Arrow;

        /// <summary>The name of the next shot.</summary>
        public string Next(int floor, bool elite, ShooterValues v) => NameOf(Shots + 1, floor, elite, v);

        /// <summary>The next shot when its band holds her, counting nothing (the turns to attack ask before the brain begins).</summary>
        public EnemyAttackDefinition Peek(float distance, int floor, bool elite, ShooterValues v)
        {
            EnemyAttackDefinition a = CasterTactics.Find(v.Arrows, Next(floor, elite, v));
            return a == null || distance < a.MinDistance || distance > a.MaxDistance ? null : a;
        }

        /// <summary>The next shot when its band holds her, counting it as begun; else null, counting nothing.</summary>
        public EnemyAttackDefinition Choose(float distance, int floor, bool elite, ShooterValues v)
        {
            EnemyAttackDefinition a = Peek(distance, floor, elite, v);
            if (a != null) Shots++;
            return a;
        }

        /// <summary>Arrows that leave together at the release of a shot of this name: the fan's, or one.</summary>
        public static int AtOnce(string name, ShooterValues v) => name == ShooterValues.ArrowFan ? Math.Max(1, v.FanArrows) : 1;

        /// <summary>Arrows a shot of this name looses in all: the fan's, the volley's one after the other, or one.</summary>
        public static int InAll(string name, ShooterValues v) =>
            name == ShooterValues.ArrowFan ? Math.Max(1, v.FanArrows) : name == ShooterValues.Volley ? Math.Max(1, v.VolleyArrows) : 1;
    }
}
