namespace Roguelike.Rules
{
    public enum ActionPhase { None, Startup, Active, Recovery }

    /// <summary>
    /// A committed action: startup, active, recovery. The hit resolves once, when the active phase is reached.
    /// Whether the character is locked for all three phases is up to the user of the timeline:
    /// <see cref="HitResolved"/> says when the hit is behind it.
    /// </summary>
    public sealed class ActionTimeline
    {
        float _startedAt;
        float _startup;
        float _active;
        float _recovery;
        bool _running;
        bool _hitResolved;

        public float Total => _startup + _active + _recovery;

        public void Start(float now, float startup, float active, float recovery)
        {
            _startedAt = now;
            _startup = startup;
            _active = active;
            _recovery = recovery;
            _running = true;
            _hitResolved = false;
        }

        public void Cancel() => _running = false;

        /// <summary>True from the moment the hit has been resolved until the action is cancelled or started again.</summary>
        public bool HitResolved => _running && _hitResolved;

        public ActionPhase Phase(float now)
        {
            if (!_running) return ActionPhase.None;
            float t = now - _startedAt;
            if (t < 0f) return ActionPhase.None;
            if (t < _startup) return ActionPhase.Startup;
            if (t < _startup + _active) return ActionPhase.Active;
            if (t < Total) return ActionPhase.Recovery;
            return ActionPhase.None;
        }

        public bool IsRunning(float now) => Phase(now) != ActionPhase.None;

        /// <summary>
        /// True exactly once per action, on the first call at or after the start of the active phase.
        /// A long frame that skips the active window still resolves the hit; a cancelled action never does.
        /// </summary>
        public bool ConsumeHit(float now)
        {
            if (!_running || _hitResolved) return false;
            if (now - _startedAt < _startup) return false;
            _hitResolved = true;
            return true;
        }
    }
}
