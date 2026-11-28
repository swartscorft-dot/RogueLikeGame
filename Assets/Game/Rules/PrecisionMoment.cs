using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// A basic shot's wind-up as work order 152 watches it on the law's own time line (<see cref="PrecisionMoment"/>): from the
    /// press to the moment the wind-up ends, the lowest speed share the accuracy reads, taken at every small step of the law and at
    /// that moment itself, wherever it falls inside a frame.
    /// </summary>
    public struct WindUpWatch
    {
        /// <summary>A wind-up is being watched (a basic shot was pressed and has not yet been read).</summary>
        public bool Open;
        /// <summary>Seconds of the law's time from the start of the next step to the moment the wind-up ends.</summary>
        public float Left;
        /// <summary>The lowest share since the press, the moment's own included once it is reached.</summary>
        public float Lowest;
        /// <summary>The moment the wind-up ends has been reached: <see cref="Lowest"/> is the share the shot reads.</summary>
        public bool Reached;

        /// <summary>A press: the share she has at it (the law's state at the frame's moment) and the wind-up's length in seconds.</summary>
        public void Start(float shareNow, float windUp)
        {
            Open = true;
            Left = Math.Max(0f, windUp);
            Lowest = shareNow;
            Reached = Left <= 0f;
        }

        /// <summary>The shot has been read (or cancelled): nothing more is watched.</summary>
        public void Stop() => Open = false;

        /// <summary>A share the law passed through inside the wind-up.</summary>
        public void Note(float share)
        {
            if (Open && !Reached && share < Lowest) Lowest = share;
        }

        /// <summary>True while the step about to run is inside the wind-up.</summary>
        public bool Watching => Open && !Reached;

        /// <summary>
        /// A step of <paramref name="dt"/> the law did not run (her body moved by something else: a dash, cover, a stun): the share
        /// at its end (<paramref name="share"/>) stands for it, and the wind-up's time runs on.
        /// </summary>
        public void Pass(float dt, float share)
        {
            if (dt <= 0f) return;
            if (Watching)
            {
                Note(share);
                if (Left <= dt) Reached = true;
            }
            Left -= dt;
        }
    }

    /// <summary>
    /// Work order 152: how the speed the law aims for changes inside a frame's step: its share, <c>seconds</c> into the step, of
    /// what it is at the step's start (1 there; the moving shot's ease falling from full speed toward the grip's share, a skill's
    /// slide, the rise after one). Her character gives it; a rule test gives its own. Nothing is allocated to ask it.
    /// </summary>
    public interface IPrecisionCurve
    {
        float ShareAt(float seconds);
    }

    /// <summary>
    /// Work order 152: the shot's cone the same at every frame rate (the grips' analyst's finding of 2026-11-27, proposal-grips
    /// point 2: held fire on the move with the stock gun read 16.3° then 13.9° at 60 frames a second and 14.5° then 12.1° at 144,
    /// against the law's own 13.9° then 11.2°; a player at 60 fps missed more than one at 144).
    ///
    /// A frame's work runs the law over the step that follows the frame's moment: what the frame read (the keys, the click, the
    /// start of a shot's ease) acts from that moment. Two things in work order 143's frame made the cone depend on the step's length:
    /// - the speed the law aims for (the moving shot's ease, which falls from full speed to the grip's share over 0.15 s) was held
    ///   at the frame's own value through the whole step, so at 60 fps her body followed the ease up to a frame late and was
    ///   faster when the shot went off;
    /// - the shot read the speed at the ends of frames only: the frame after the moment its wind-up ended, and the lowest at
    ///   those ends.
    /// Here the law runs as written, friction, brake and acceleration in its small steps of at most
    /// <see cref="PrecisionValues.InertiaStep"/>, with the speed it aims for taken at each small step's own middle
    /// (<c>capShare</c>), and a step that holds the moment the wind-up ends is cut there, so the share at that moment is the law's
    /// own and not an interpolation between the frame's two ends (the law is not straight inside a step: friction is a share of the
    /// speed and acceleration stops at the cap). The meter and the ghost had the same fault (they read the frame's ends and
    /// followed her speed as the frame held it) and get the same repair. Nothing is allocated.
    /// </summary>
    public static class PrecisionMoment
    {
        /// <summary>
        /// Inertia, one frame of <paramref name="dt"/>: as <see cref="PrecisionRules.InertiaFrame"/>, the speed it aims for being
        /// <paramref name="cap"/> times <paramref name="capShare"/> at each small step's middle (seconds into the frame; null: the
        /// cap throughout). Her share (speed over <paramref name="runSpeed"/>) is noted in <paramref name="watch"/>.
        /// </summary>
        public static void InertiaFrame(ref float vx, ref float vz, float wishX, float wishZ, float cap, IPrecisionCurve capShare,
                                        float dt, float runSpeed, PrecisionValues v, ref WindUpWatch watch)
        {
            if (dt <= 0f) return;
            float run = Math.Max(0.1f, runSpeed);
            float cut = Cut(ref watch, dt);
            if (cut > 0f) InertiaPiece(ref vx, ref vz, wishX, wishZ, cap, capShare, 0f, cut, run, v, ref watch);
            if (cut >= 0f)
            {
                watch.Note(Length(vx, vz) / run);
                watch.Reached = true;
            }
            float from = Math.Max(0f, cut);
            if (dt - from > 0f) InertiaPiece(ref vx, ref vz, wishX, wishZ, cap, capShare, from, dt - from, run, v, ref watch);
            watch.Left -= dt;
        }

        /// <summary>
        /// The ghost, one frame: as <see cref="PrecisionRules.GhostFrame"/>, following her real velocity (<paramref name="realX"/>,
        /// <paramref name="realZ"/>) times <paramref name="realShare"/> at each small step's middle (null: as the frame holds it).
        /// </summary>
        public static void GhostFrame(ref float gx, ref float gz, float realX, float realZ, IPrecisionCurve realShare, float dt,
                                      float runSpeed, PrecisionValues v, float settle, ref WindUpWatch watch)
        {
            if (dt <= 0f) return;
            float run = Math.Max(0.1f, runSpeed);
            float cut = Cut(ref watch, dt);
            if (cut > 0f) GhostPiece(ref gx, ref gz, realX, realZ, realShare, 0f, cut, run, v, settle, ref watch);
            if (cut >= 0f)
            {
                watch.Note(Length(gx, gz) / run);
                watch.Reached = true;
            }
            float from = Math.Max(0f, cut);
            if (dt - from > 0f) GhostPiece(ref gx, ref gz, realX, realZ, realShare, from, dt - from, run, v, settle, ref watch);
            watch.Left -= dt;
        }

        /// <summary>
        /// The meter, one frame: as <see cref="PrecisionRules.MeterStep"/> toward the target of her real share
        /// (<paramref name="realShare"/>) times <paramref name="realShareAt"/> at each small step's middle; returns the momentum. The
        /// share noted is the one it stands for (<see cref="PrecisionRules.MeterShare"/>).
        /// </summary>
        public static float MeterFrame(float momentum, float realShare, IPrecisionCurve realShareAt, float dt, PrecisionValues v,
                                       float accurateShare, float settle, ref WindUpWatch watch)
        {
            if (dt <= 0f) return momentum;
            float cut = Cut(ref watch, dt);
            if (cut > 0f) momentum = MeterPiece(momentum, realShare, realShareAt, 0f, cut, v, accurateShare, settle, ref watch);
            if (cut >= 0f)
            {
                watch.Note(PrecisionRules.MeterShare(momentum, accurateShare));
                watch.Reached = true;
            }
            float from = Math.Max(0f, cut);
            if (dt - from > 0f) momentum = MeterPiece(momentum, realShare, realShareAt, from, dt - from, v, accurateShare, settle, ref watch);
            watch.Left -= dt;
            return momentum;
        }

        /// <summary>Seconds into a step of <paramref name="dt"/> at which the watched wind-up ends; negative when it does not end in it.</summary>
        static float Cut(ref WindUpWatch watch, float dt)
        {
            if (!watch.Watching) return -1f;
            if (watch.Left > dt) return -1f;
            return Math.Max(0f, watch.Left);
        }

        /// <summary>The number of equal small steps a piece of <paramref name="length"/> seconds is run in (each at most the law's step).</summary>
        static int Steps(float length, PrecisionValues v) => Math.Max(1, (int)Math.Ceiling(length / Math.Max(1e-4f, v.InertiaStep) - 1e-6));

        static void InertiaPiece(ref float vx, ref float vz, float wishX, float wishZ, float cap, IPrecisionCurve capShare,
                                 float from, float length, float run, PrecisionValues v, ref WindUpWatch watch)
        {
            int steps = Steps(length, v);
            float h = length / steps;
            for (int i = 0; i < steps; i++)
            {
                float c = capShare != null ? cap * Math.Max(0f, capShare.ShareAt(from + (i + 0.5f) * h)) : cap;
                PrecisionRules.InertiaFrame(ref vx, ref vz, wishX, wishZ, c, h, v); // one small step of the law (h is at most its step)
                watch.Note(Length(vx, vz) / run);
            }
        }

        static void GhostPiece(ref float gx, ref float gz, float realX, float realZ, IPrecisionCurve realShare, float from, float length,
                               float run, PrecisionValues v, float settle, ref WindUpWatch watch)
        {
            int steps = Steps(length, v);
            float h = length / steps;
            for (int i = 0; i < steps; i++)
            {
                float k = realShare != null ? Math.Max(0f, realShare.ShareAt(from + (i + 0.5f) * h)) : 1f;
                PrecisionRules.GhostFrame(ref gx, ref gz, realX * k, realZ * k, h, v, settle);
                watch.Note(Length(gx, gz) / run);
            }
        }

        static float MeterPiece(float momentum, float realShare, IPrecisionCurve realShareAt, float from, float length,
                                PrecisionValues v, float accurateShare, float settle, ref WindUpWatch watch)
        {
            int steps = Steps(length, v);
            float h = length / steps;
            for (int i = 0; i < steps; i++)
            {
                float k = realShareAt != null ? Math.Max(0f, realShareAt.ShareAt(from + (i + 0.5f) * h)) : 1f;
                momentum = PrecisionRules.MeterStep(momentum, realShare * k, h, v, accurateShare, settle);
                watch.Note(PrecisionRules.MeterShare(momentum, accurateShare));
            }
            return momentum;
        }

        static float Length(float x, float z) => (float)Math.Sqrt(x * x + z * z);
    }
}
