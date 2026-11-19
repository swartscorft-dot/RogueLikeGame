using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The speed cap while she backpedals: held turned away from her movement by a shot (<see cref="ShotFacing"/>),
    /// she may move at no more than a share of run speed, so the backward jog plays at its own speed. The cap eases
    /// in and out with the moving-shot curve's shape (smoothstep), never in a jump. Decision 0003 (on trial): a guess,
    /// 0.5 of run speed, the 3 m/s the backward jog was built for.
    /// </summary>
    public sealed class BackpedalCap
    {
        bool _on;
        float _changedAt = float.NegativeInfinity;
        float _from = 1f;

        /// <summary>The most share of run speed the cap allows now: 1 when it is off and has eased out.</summary>
        public float Share { get; private set; } = 1f;

        /// <summary>
        /// Once a frame: <paramref name="on"/> is whether she is backpedalling now. Eases toward <paramref name="cap"/>
        /// over <paramref name="easeIn"/> seconds when it comes on, and back to 1 over <paramref name="easeOut"/> when
        /// it goes off, from wherever it was. Returns <see cref="Share"/>.
        /// </summary>
        public float Update(bool on, float now, float cap, float easeIn, float easeOut)
        {
            if (on != _on)
            {
                _on = on;
                _changedAt = now;
                _from = Share;
            }
            float target = on ? Clamp01(cap) : 1f;
            float time = on ? easeIn : easeOut;
            float t = time > 0f ? Clamp01((now - _changedAt) / time) : 1f;
            Share = _from + (target - _from) * t * t * (3f - 2f * t);
            return Share;
        }

        /// <summary>Off at once, as after a teleport.</summary>
        public void Reset()
        {
            _on = false;
            _from = 1f;
            Share = 1f;
            _changedAt = float.NegativeInfinity;
        }

        static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }
}
