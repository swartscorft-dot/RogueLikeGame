namespace Roguelike.Rules
{
    /// <summary>
    /// Dash timing: cooldown, duration and i-frames. Time is passed in, in seconds.
    /// The feel values are read when a dash starts, so tuning them changes the next dash, not the one in flight.
    /// </summary>
    public sealed class Dash
    {
        float _startedAt = float.NegativeInfinity;
        float _readyAt = float.NegativeInfinity;
        float _duration;
        float _invulnerability;

        public bool CanStart(float now) => now >= _readyAt;

        public float CooldownRemaining(float now) => now >= _readyAt ? 0f : _readyAt - now;

        /// <summary>Cuts a running cooldown by a share of what is left, as <see cref="Cooldown.Cut"/> does. Returns the seconds cut.</summary>
        public float CutCooldown(float now, float share)
        {
            float left = CooldownRemaining(now);
            if (left <= 0f) return 0f;
            float cut = left * (share < 0f ? 0f : share > 1f ? 1f : share);
            _readyAt -= cut;
            return cut;
        }

        public bool TryStart(float now, FeelValues feel)
        {
            if (!CanStart(now)) return false;
            _startedAt = now;
            _readyAt = now + feel.DashCooldown;
            _duration = feel.DashDuration;
            _invulnerability = feel.DashInvulnerability;
            return true;
        }

        public bool IsDashing(float now) => now >= _startedAt && now < _startedAt + _duration;

        public bool IsInvulnerable(float now) => now >= _startedAt && now < _startedAt + _invulnerability;

        /// <summary>0 at the start of the dash, 1 at its end (clamped).</summary>
        public float Progress(float now)
        {
            if (_duration <= 0f) return 1f;
            float p = (now - _startedAt) / _duration;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }
    }
}
