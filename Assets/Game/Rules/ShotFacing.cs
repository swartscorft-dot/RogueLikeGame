using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where the body faces around basic shots that must turn it (the aim outside the front angle), so that shooting
    /// backward is a state she settles into and leaves smoothly (Kristian 2026-11-18: "It gets corny when she has to
    /// shoot backward, she snaps back and forth. I imagine it is natural that she jogs backward while she shoots then.").
    ///
    /// <list type="bullet">
    /// <item>A shot that must turn the body starts the hold: she turns toward the aim at a set rate and keeps facing the
    /// live aim. Any shot while held renews it.</item>
    /// <item>The hold ends when its time runs out after the last shot, or, while she is moving, when the aim comes back
    /// inside the front angle of her run direction. Moving, she then turns back to the run at the same rate; standing,
    /// she stays as she is.</item>
    /// <item>From outside: a move order ends it with the turn back at the rate; a dash or a skill ends it at once
    /// (they face their own way at once).</item>
    /// </list>
    /// Normal facing to the movement is not part of this: it stays as the turn time in the feel values has it.
    /// </summary>
    public sealed class ShotFacing
    {
        public enum Mode
        {
            /// <summary>Not held: the body faces the way she moves, as usual.</summary>
            Free,
            /// <summary>Turned to the aim after a shot that had to turn the body: facing the live aim, at the turn rate.</summary>
            Held,
            /// <summary>The hold is over: turning back to the run direction at the turn rate.</summary>
            TurningBack,
        }

        float _holdUntil = float.NegativeInfinity;

        public Mode State { get; private set; }

        /// <summary>When the hold runs out, if nothing renews it or ends it before.</summary>
        public float HoldUntil => _holdUntil;

        /// <summary>
        /// A basic shot starts. <paramref name="turnsBody"/>: its aim lies outside the front angle of where the body
        /// faces. Such a shot starts the hold; any shot while held renews it.
        /// </summary>
        public void ShotStarted(bool turnsBody, float now, float hold)
        {
            if (!turnsBody && State != Mode.Held) return;
            State = Mode.Held;
            _holdUntil = now + Math.Max(0f, hold);
        }

        /// <summary>
        /// Each frame. <paramref name="moving"/>: she walks on a move order. <paramref name="aimInsideRunFront"/>: the
        /// live aim lies within the front angle of her run direction. <paramref name="facingRun"/>: the body faces
        /// the run direction. Returns the mode for this frame.
        /// </summary>
        public Mode Update(float now, bool moving, bool aimInsideRunFront, bool facingRun)
        {
            if (State == Mode.Held && (now >= _holdUntil || (moving && aimInsideRunFront)))
                State = moving ? Mode.TurningBack : Mode.Free;
            if (State == Mode.TurningBack && (!moving || facingRun)) State = Mode.Free;
            return State;
        }

        /// <summary>Ends the hold from outside: with the turn back at the rate (a move order), or at once (a dash, a skill).</summary>
        public void End(bool turnBack)
        {
            if (State == Mode.Free) return;
            State = turnBack ? Mode.TurningBack : Mode.Free;
        }

        /// <summary>
        /// A facing angle turned toward a target angle by at most what the turn rate allows in <paramref name="dt"/>
        /// seconds, the shorter way round. <paramref name="halfTurnSeconds"/> is the time for 180 degrees; 0 or less
        /// turns at once. Degrees.
        /// </summary>
        public static float Turn(float current, float target, float halfTurnSeconds, float dt)
        {
            float delta = DeltaAngle(current, target);
            if (halfTurnSeconds <= 0f) return current + delta;
            float max = 180f / halfTurnSeconds * Math.Max(0f, dt);
            return current + Math.Max(-max, Math.Min(max, delta));
        }

        /// <summary>The shorter signed difference from one angle to another, -180 to 180 degrees.</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
