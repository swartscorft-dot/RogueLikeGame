using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>What the sprint's jump looks at: the footprint of a thing that stands on the floor, and whether it is a low prop.</summary>
    public readonly struct JumpShape
    {
        public readonly CoverShape Shape;
        /// <summary>A low prop (<see cref="PropHeights.Low"/>, the greybox's blue): it can be jumped. Anything else (a wall, a tall prop, a slender one) never.</summary>
        public readonly bool Low;

        public JumpShape(CoverShape shape, bool low)
        {
            Shape = shape;
            Low = low;
        }
    }

    /// <summary>Why the sprint's jump does not go (work order 092): for tests and the readout.</summary>
    public enum JumpRefusal
    {
        /// <summary>It jumps.</summary>
        None,
        /// <summary>No low prop on her way within <see cref="FeelValues.SprintJumpLookAhead"/>.</summary>
        NothingAhead,
        /// <summary>From where she is to where she would land is more than <see cref="FeelValues.SprintJumpMost"/>.</summary>
        TooFar,
        /// <summary>Another prop, a wall, or (seen by <see cref="CoverLine"/>) a tall prop or a shut door's block on the way or at the landing.</summary>
        Blocked,
        /// <summary>The landing is not walkable floor (no NavMesh there).</summary>
        NoFloor,
        /// <summary>A living enemy stands at the landing.</summary>
        EnemyThere,
    }

    /// <summary>The jump the sprint takes, or why it does not: from <see cref="From"/> to <see cref="To"/> in a straight line, <see cref="Length"/> metres, over <see cref="Prop"/> (its place in the list).</summary>
    public readonly struct JumpPlan
    {
        public readonly JumpRefusal Refusal;
        public readonly int Prop;
        public readonly GroundPoint From;
        public readonly GroundPoint To;
        public readonly float Length;

        public JumpPlan(JumpRefusal refusal, int prop, GroundPoint from, GroundPoint to, float length)
        {
            Refusal = refusal;
            Prop = prop;
            From = from;
            To = to;
            Length = length;
        }

        public bool Jumps => Refusal == JumpRefusal.None;

        public static JumpPlan No(JumpRefusal why, int prop = -1) => new JumpPlan(why, prop, default, default, 0f);
    }

    /// <summary>
    /// The sprint (work order 092; Kristian, 2026-11-23, playing main fa61ce7: "syntes også vi skal innføre en sprint funksjon med
    /// shift knappen. da kan man ikke skyte samtidig. men også hoppe over blå props. ved å sprinte og ta skade får man en liten
    /// stagger, sånn at det skal straffe seg litt": a sprint on Shift; she cannot shoot meanwhile; she can jump the blue props; a hit
    /// taken while sprinting gives a small stagger, so that it costs a little). No engine; the numbers are in <see cref="FeelValues"/>,
    /// the producer's starting values. With <see cref="FeelValues.Sprint"/> off nothing here is on and she moves exactly as before.
    ///
    /// - When (<see cref="On"/>): the sprint key held and a move key held, and she is free to run (no dash, skill, stun or pull, duck,
    ///   cover, Slide; the duck key not held). Standing with the key held does nothing. She turns freely. No stamina, no longest time,
    ///   no wait before the next: the price is that she cannot shoot, and the stagger.
    /// - How fast (<see cref="SpeedShare"/>): <see cref="FeelValues.SprintSpeedShare"/> times her run speed, reached
    ///   <see cref="FeelValues.SprintRampTime"/> after it starts; it ends at once (the key up, or she stops).
    /// - No shot or skill while it is on (<see cref="Refuses"/>): a click or a skill key does nothing and is not kept; a click or a
    ///   press waiting from before is dropped when it starts. The dash still works (and ends it for the dash's length); the duck key
    ///   ends it, and ducks by the duck's own rule; interact as always.
    /// - The jump (<see cref="Decide"/>): sprinting into a low prop with free floor on its far side, she goes over it by herself.
    /// - The stagger (<see cref="Staggers"/>): a hit that hurt her while she sprints or jumps holds her
    ///   <see cref="FeelValues.SprintStagger"/> (in the air, from the landing).
    /// </summary>
    public static class SprintRules
    {
        /// <summary>The name the stagger is told by (the stun's telling and the log's "player_stunned").</summary>
        public const string StaggerName = "sprint stagger";

        /// <summary>
        /// Is she sprinting now? The switch, the key held, a move key held, and free to run (<paramref name="free"/>: the caller's
        /// list: not dashing, not in a skill, not held, not down in a duck, not in cover, no Slide, the duck key not held, alive).
        /// </summary>
        public static bool On(FeelValues f, bool keyHeld, bool moving, bool free) =>
            f != null && f.Sprint && keyHeld && moving && free;

        /// <summary>
        /// The share of her run speed she may move at: 1 when not sprinting; sprinting, from 1 up to
        /// <see cref="FeelValues.SprintSpeedShare"/> over <see cref="FeelValues.SprintRampTime"/> from its start, and that after.
        /// </summary>
        public static float SpeedShare(FeelValues f, bool sprinting, float sinceStart)
        {
            if (f == null || !f.Sprint || !sprinting) return 1f;
            float most = Math.Max(1f, f.SprintSpeedShare);
            if (f.SprintRampTime <= 0f || sinceStart >= f.SprintRampTime) return most;
            float k = Math.Max(0f, sinceStart) / f.SprintRampTime;
            return 1f + (most - 1f) * k;
        }

        /// <summary>Does a click or a skill key do nothing now? While she sprints or is in a jump (with the switch on).</summary>
        public static bool Refuses(FeelValues f, bool sprintingOrJumping) => f != null && f.Sprint && sprintingOrJumping;

        /// <summary>
        /// Does a hit stagger her? One that took health (<paramref name="applied"/> above 0: a hit the dash's protected part let
        /// through unharmed gives none) while she sprints or jumps, the switch on and the stagger longer than 0.
        /// </summary>
        public static bool Staggers(FeelValues f, bool sprintingOrJumping, int applied) =>
            f != null && f.Sprint && sprintingOrJumping && applied > 0 && f.SprintStagger > 0f;

        /// <summary>The seconds a jump of <paramref name="length"/> metres takes at <paramref name="speed"/> m/s (her sprint speed); 0 for none.</summary>
        public static float JumpSeconds(float length, float speed) => length > 0f && speed > 0f ? length / speed : 0f;

        /// <summary>
        /// The height the model is lifted at <paramref name="progress"/> (0 at the take-off, 1 at the landing) of a jump: a half sine
        /// up to <see cref="FeelValues.SprintJumpLift"/> at its middle. A pose of the moment until a look order gives it a clip.
        /// </summary>
        public static float Lift(FeelValues f, float progress)
        {
            if (f == null || progress <= 0f || progress >= 1f) return 0f;
            return Math.Max(0f, f.SprintJumpLift) * (float)Math.Sin(Math.PI * progress);
        }

        /// <summary>
        /// The jump, sprinting from <paramref name="her"/> along <paramref name="way"/> (any length but zero). The nearest low prop
        /// of <paramref name="shapes"/> whose edge her way meets within <see cref="FeelValues.SprintJumpLookAhead"/> of her middle is
        /// the one; she lands where her way has left it and her middle is <see cref="FeelValues.SprintJumpRoom"/> from it (straight on,
        /// just past its far face), if all of these hold:
        /// - from her to the landing is at most <see cref="FeelValues.SprintJumpMost"/>;
        /// - no other shape of the list (another prop, low or tall; a wall) lies on the straight way from her to the landing or within
        ///   her body's radius of the landing (a wall she runs along does not stop it), and <paramref name="cover"/> (the physics ray of walls, tall props and shut doors' blocks; null
        ///   for none) is clear all the way;
        /// - the landing is walkable floor (<paramref name="walkable"/>: the NavMesh; null counts as walkable);
        /// - no living enemy of <paramref name="enemies"/> (null for none) stands within the room and its own radius of the landing.
        /// Otherwise she does not jump, and the prop stops her as before. Nothing is allocated.
        /// </summary>
        public static JumpPlan Decide(FeelValues f, GroundPoint her, GroundPoint way, IReadOnlyList<JumpShape> shapes,
                                      IReadOnlyList<TargetInfo> enemies, Func<GroundPoint, bool> walkable, CoverLine cover)
        {
            if (f == null || !f.Sprint || shapes == null) return JumpPlan.No(JumpRefusal.NothingAhead);
            float length = way.Length;
            if (length < 1e-6f) return JumpPlan.No(JumpRefusal.NothingAhead);
            GroundPoint d = way * (1f / length);
            float ahead = Math.Max(0f, f.SprintJumpLookAhead);

            // The low prop her way meets first, within the look ahead (and never one she stands in: the NavMesh keeps her out).
            int best = -1;
            float bestEntry = float.PositiveInfinity;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (!shapes[i].Low) continue;
                float entry = shapes[i].Shape.Entry(her, d, ahead + 1e-3f);
                if (entry > ahead || (entry <= 0f && shapes[i].Shape.Distance(her) <= 0f)) continue;
                if (entry < bestEntry)
                {
                    bestEntry = entry;
                    best = i;
                }
            }
            if (best < 0) return JumpPlan.No(JumpRefusal.NothingAhead);
            // Something else nearer on her way than the prop (another prop, a wall) stops her first.
            for (int i = 0; i < shapes.Count; i++)
                if (i != best && shapes[i].Shape.Entry(her, d, bestEntry) < bestEntry - 1e-3f) return JumpPlan.No(JumpRefusal.Blocked, best);

            CoverShape prop = shapes[best].Shape;
            float room = Math.Max(0f, f.SprintJumpRoom);
            float most = Math.Max(0f, f.SprintJumpMost);
            float exit = prop.Exit(her, d);
            if (exit < 0f) return JumpPlan.No(JumpRefusal.NothingAhead);
            // Straight on until her middle is the room from the prop (just past its far face; a little farther across a corner).
            float at = exit + room;
            const float Step = 0.05f;
            while (at <= most + 1e-4f && prop.Distance(her + d * at) < room - 1e-3f) at += Step;
            if (at > most + 1e-4f) return JumpPlan.No(JumpRefusal.TooFar, best);
            GroundPoint to = her + d * at;

            // Anything else only needs to leave her body room (she runs 0.5 m from a wall, as the NavMesh keeps her).
            float body = Math.Min(room, Placeholders.PlayerRadius) - 0.05f;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (i == best) continue;
                CoverShape s = shapes[i].Shape;
                if (s.Crosses(her, to) || s.Distance(to) < body) return JumpPlan.No(JumpRefusal.Blocked, best);
            }
            if (cover != null && cover(her, to) < at - 1e-3f) return JumpPlan.No(JumpRefusal.Blocked, best);
            if (walkable != null && !walkable(to)) return JumpPlan.No(JumpRefusal.NoFloor, best);
            if (enemies != null)
                for (int i = 0; i < enemies.Count; i++)
                {
                    TargetInfo e = enemies[i];
                    if (e.Alive && e.Position.DistanceTo(to) < room + Math.Max(0f, e.Radius)) return JumpPlan.No(JumpRefusal.EnemyThere, best);
                }
            return new JumpPlan(JumpRefusal.None, best, her, to, at);
        }
    }
}
