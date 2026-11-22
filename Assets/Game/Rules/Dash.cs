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
        // Work order 029 (Spare Step, a piece of the rack between runs): more than one charge, counted as a skill's are.
        int _maxCharges = 1;
        Cooldown _charges = new Cooldown();

        /// <summary>
        /// The dashes it holds (work order 029): 1, the dash as it always was, unless Spare Step gives more; then each spent
        /// charge comes back one at a time, as a skill's charges do (<see cref="Cooldown"/>). Changing it starts the count
        /// again, all charges ready.
        /// </summary>
        public int MaxCharges
        {
            get => _maxCharges;
            set
            {
                int v = value < 1 ? 1 : value;
                if (v == _maxCharges) return;
                _maxCharges = v;
                _charges = new Cooldown { MaxCharges = v };
                _readyAt = float.NegativeInfinity;
            }
        }

        /// <summary>Dashes ready now: 1 or 0 with one charge.</summary>
        public int ChargesReady(float now) => _maxCharges > 1 ? _charges.Charges(now) : CanStart(now) ? 1 : 0;

        public bool CanStart(float now) => _maxCharges > 1 ? _charges.IsReady(now) : now >= _readyAt;

        public float CooldownRemaining(float now) => _maxCharges > 1 ? _charges.Remaining(now) : now >= _readyAt ? 0f : _readyAt - now;

        /// <summary>Cuts a running cooldown by a share of what is left, as <see cref="Cooldown.Cut"/> does. Returns the seconds cut.</summary>
        public float CutCooldown(float now, float share)
        {
            if (_maxCharges > 1) return _charges.Cut(now, share);
            float left = CooldownRemaining(now);
            if (left <= 0f) return 0f;
            float cut = left * (share < 0f ? 0f : share > 1f ? 1f : share);
            _readyAt -= cut;
            return cut;
        }

        /// <summary>
        /// Takes this many seconds off a running cooldown, no more than it has left, as <see cref="Cooldown.CutSeconds"/> does
        /// (work order 071: while spent focus's gains run, the dash's cooldown runs faster). Returns the seconds cut.
        /// </summary>
        public float CutCooldownSeconds(float now, float seconds)
        {
            if (_maxCharges > 1) return _charges.CutSeconds(now, seconds);
            float left = CooldownRemaining(now);
            if (left <= 0f || seconds <= 0f) return 0f;
            float cut = left < seconds ? left : seconds;
            _readyAt -= cut;
            return cut;
        }

        /// <summary>
        /// Starts a dash if it may. <paramref name="cooldownShare"/> is the share of the feel's cooldown it runs (work order
        /// 017: the run's chest steps cut it; 1 outside a run).
        /// </summary>
        public bool TryStart(float now, FeelValues feel, float cooldownShare = 1f)
        {
            if (!CanStart(now)) return false;
            _startedAt = now;
            if (_maxCharges > 1) _charges.Start(now, feel.DashCooldown * (cooldownShare < 0f ? 0f : cooldownShare)); // work order 029
            else _readyAt = now + feel.DashCooldown * (cooldownShare < 0f ? 0f : cooldownShare);
            _duration = feel.DashDuration;
            _invulnerability = feel.DashInvulnerability;
            return true;
        }

        /// <summary>Back to its start: no dash, and ready at once (work order 026: a run starts with a clean slate).</summary>
        public void Clear()
        {
            _startedAt = float.NegativeInfinity;
            _readyAt = float.NegativeInfinity;
            _duration = 0f;
            _invulnerability = 0f;
            _charges = new Cooldown { MaxCharges = _maxCharges };
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
