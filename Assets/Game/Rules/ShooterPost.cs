using System;

namespace Roguelike.Rules
{
    /// <summary>What a shooter in the chase does with its feet this frame (work order 098, <see cref="ShooterPostRules.Choose"/>).</summary>
    public enum ShooterGo
    {
        /// <summary>It stands where it is (and, holding its place, faces her).</summary>
        Stand,
        /// <summary>It walks toward her: as before work order 098, when she is far or it does not see her.</summary>
        WalkToHer,
        /// <summary>It backs off straight away from her, to <see cref="ShooterMove.To"/> (cut short by the NavMesh where it stands).</summary>
        BackOff,
        /// <summary>Holding its place: it walks back to its post (she left its reach, or it was put out of its circle).</summary>
        ToPost,
        /// <summary>Holding its place, she is in its reach and it does not see her: it looks for a spot inside its circle that sees her.</summary>
        SeekLine,
    }

    /// <summary>A shooter's choice for the frame (work order 098): what it does, where to for a back-off, and its approach flag for the next frame.</summary>
    public struct ShooterMove
    {
        public ShooterGo Go;
        /// <summary>The back-off's end on the ground, before the NavMesh cuts it; nothing for the others.</summary>
        public GroundPoint To;
        /// <summary>Its approach flag after this frame (before 098: set when she is farther than KeepFar, cleared at the middle of its band).</summary>
        public bool Approaching;
    }

    /// <summary>
    /// Shooters that hold their place (work order 098; Kristian's choice of 2026-11-24, <see cref="ShooterValues.HoldsPlace"/>): where a
    /// shooter in the chase stands, as a rule with no engine. The shooter asks it every frame of the chase; what it cannot know
    /// here (whether it sees her, where the NavMesh ends, which spot of its circle sees her) it asks the engine itself.
    ///
    /// As before (the switch off, or no post known: the practice room, a shooter of no room's fight): it backs off straight away from
    /// her while it backs off; it walks toward her when she is farther than <see cref="ShooterValues.KeepFar"/> until she is at the
    /// middle of its band, and whenever it does not see her; otherwise it stands.
    ///
    /// Holding its place (a shooter of a room's fight, whose post is the place the generator stood it): it never walks toward her
    /// because she is far. Out of its circle (<see cref="ShooterValues.HoldRadius"/> round the post, and a little slack for the
    /// agent's arrival) it walks back to its post. Backing off, it backs off as before, cut short at the circle's edge, so that with
    /// its back to the edge it stands and shoots as it does with its back to a wall. She out of its reach
    /// (<see cref="ShooterValues.ShootsFrom"/>), it walks back to its post and stands there. She in its reach: it stands where it is
    /// when it sees her (and shoots by its own rules), and when it does not it looks for a spot inside its circle that does.
    /// </summary>
    public static class ShooterPostRules
    {
        /// <summary>m from its post that count as at it: the enemies' own slack for being at home (<c>Enemy.HomeSlack</c>).</summary>
        public const float AtPost = 0.3f;
        /// <summary>
        /// m past its circle's edge before a shooter counts as put out of it and walks back: an agent arrives within a few centimetres
        /// of where it was sent and is nudged by the others' avoidance, and a quarter metre keeps that from reading as leaving.
        /// </summary>
        public const float EdgeSlack = 0.25f;
        /// <summary>m of a back-off under which it counts as cornered (as before work order 098: it stands and shoots).</summary>
        public const float CorneredUnder = 0.5f;
        /// <summary>s between two looks for a spot that sees her, while it holds its place, she is in its reach and it does not see her.</summary>
        public const float SeekEvery = 0.5f;
        /// <summary>s a shooter walks to a spot it chose before it looks again (a spot it cannot reach is given up).</summary>
        public const float SeekMost = 2f;
        /// <summary>Directions round the post the spots it looks at stand in: every 45 degrees.</summary>
        public const int SpotDirections = 8;
        /// <summary>The rings of spots, as shares of <see cref="ShooterValues.HoldRadius"/>: half way out, and just inside the edge.</summary>
        public static readonly float[] SpotRings = { 0.5f, 0.95f };
        /// <summary>The spots looked at: the post and each ring's directions.</summary>
        public static int SpotCount => 1 + SpotDirections * SpotRings.Length;

        /// <summary>Does a shooter with these values hold its place, its post known?</summary>
        public static bool Holds(ShooterValues t, bool postKnown) => t != null && t.HoldsPlace && postKnown && t.HoldRadius >= 0f;

        /// <summary>Is the point inside the circle of <paramref name="radius"/> round <paramref name="post"/>, with <paramref name="slack"/> more?</summary>
        public static bool Inside(GroundPoint p, GroundPoint post, float radius, float slack = 0f) => p.DistanceTo(post) <= Math.Max(0f, radius) + slack;

        /// <summary>
        /// The way from <paramref name="from"/> to <paramref name="to"/>, cut short where it would leave the circle round
        /// <paramref name="post"/>: <paramref name="to"/> when it is inside, else the point where the straight way meets the edge, and
        /// <paramref name="from"/> itself when that way leaves at once (from on the edge going out, or outside).
        /// </summary>
        public static GroundPoint Clip(GroundPoint from, GroundPoint to, GroundPoint post, float radius)
        {
            radius = Math.Max(0f, radius);
            if (to.DistanceTo(post) <= radius) return to;
            GroundPoint d = to - from;
            float length = d.Length;
            if (length < 1e-6f) return from;
            GroundPoint u = d * (1f / length);
            // |from + u t - post| = radius: t² + 2 b t + c = 0 with b = u·(from - post), c = |from - post|² - radius².
            GroundPoint f = from - post;
            float b = u.Dot(f), c = f.Dot(f) - radius * radius;
            float disc = b * b - c;
            if (disc < 0f) return from;
            float t = -b + (float)Math.Sqrt(disc); // the far meeting: where the way leaves the circle
            if (t <= 0f) return from;
            return t >= length ? to : from + u * t;
        }

        /// <summary>
        /// Spot <paramref name="i"/> (0 to <see cref="SpotCount"/> - 1) a shooter holding its place looks at for its line to her: 0 the
        /// post, then each ring of <see cref="SpotRings"/> in <see cref="SpotDirections"/> directions. Always inside the circle.
        /// </summary>
        public static GroundPoint Spot(int i, GroundPoint post, float radius)
        {
            if (i <= 0) return post;
            int ring = (i - 1) / SpotDirections, k = (i - 1) % SpotDirections;
            if (ring >= SpotRings.Length) return post;
            double a = k * (2.0 * Math.PI / SpotDirections);
            float r = Math.Max(0f, radius) * SpotRings[ring];
            return new GroundPoint(post.X + (float)Math.Cos(a) * r, post.Z + (float)Math.Sin(a) * r);
        }

        /// <summary>
        /// The choice of the frame (see the class) for a shooter at <paramref name="me"/> facing <paramref name="forward"/> (a unit
        /// direction, its back-off away from her when she stands on it), she at <paramref name="her"/>; <paramref name="seesHer"/> from
        /// where it stands; <paramref name="backingOff"/> and <paramref name="approaching"/> its flags; <paramref name="holds"/> when it
        /// holds its place (<see cref="Holds"/>), round <paramref name="post"/>.
        /// </summary>
        public static ShooterMove Choose(ShooterValues t, GroundPoint me, GroundPoint forward, GroundPoint her, bool seesHer, bool backingOff,
                                         bool approaching, bool holds, GroundPoint post)
        {
            GroundPoint toHer = her - me;
            float distance = toHer.Length;
            if (!holds)
            {
                // As before work order 098, line for line of the shooter's chase then.
                if (backingOff) return new ShooterMove { Go = ShooterGo.BackOff, To = AwayFrom(t, me, forward, toHer, distance), Approaching = approaching };
                if (distance > t.KeepFar) approaching = true;
                if (distance <= (t.KeepNear + t.KeepFar) * 0.5f) approaching = false;
                return new ShooterMove { Go = approaching || !seesHer ? ShooterGo.WalkToHer : ShooterGo.Stand, Approaching = approaching };
            }

            // Holding its place: never an approach because she is far.
            float radius = Math.Max(0f, t.HoldRadius);
            if (!Inside(me, post, radius, EdgeSlack)) return new ShooterMove { Go = ShooterGo.ToPost };
            if (backingOff) return new ShooterMove { Go = ShooterGo.BackOff, To = Clip(me, AwayFrom(t, me, forward, toHer, distance), post, radius) };
            if (distance > ShooterValues.ShootsFrom)
                return new ShooterMove { Go = me.DistanceTo(post) > AtPost ? ShooterGo.ToPost : ShooterGo.Stand };
            return new ShooterMove { Go = seesHer ? ShooterGo.Stand : ShooterGo.SeekLine };
        }

        /// <summary>The back-off's end as before work order 098: straight away from her, far enough to be KeepNear from her plus a metre.</summary>
        static GroundPoint AwayFrom(ShooterValues t, GroundPoint me, GroundPoint forward, GroundPoint toHer, float distance)
        {
            GroundPoint away = distance > 1e-3f ? new GroundPoint(-toHer.X / distance, -toHer.Z / distance) : new GroundPoint(-forward.X, -forward.Z);
            return me + away * (t.KeepNear - distance + 1f);
        }
    }

    /// <summary>
    /// The recorder's line of a shooter that held its post (work order 098), one a shooter a room's fight at most: written as it dies,
    /// or as its room's fight ends with it alive, or as it is taken out of the game in the fight. floor, order (the room's place in the
    /// floor), woke (the session's seconds the room's fight woke), held (1 when it held a post, 0 when it moved as before), reach (s
    /// from the waking until she first came within its reach, "-" never), died (s from the waking to its death, "-" alive).
    /// </summary>
    public static class ShooterPostEvents
    {
        public const string Post = "shooter_post";
    }
}
