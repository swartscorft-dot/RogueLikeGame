using System;

namespace Roguelike.Rules
{
    public enum SkillPhase
    {
        None,
        /// <summary>Locked before the first hit. For a Casting skill this is the cast.</summary>
        Startup,
        /// <summary>The key is being held: a Holding skill is hitting, a Charge skill is charging.</summary>
        Channel,
        /// <summary>A burst is between its first and its last hit.</summary>
        Active,
        /// <summary>Locked after the last hit.</summary>
        Recovery,
    }

    /// <summary>
    /// One use of a skill, from the press to the end of its recovery. Time is passed in, in seconds.
    /// The character is locked for as long as <see cref="IsRunning"/>.
    ///
    /// Normal, Point, Casting: Startup, the hits, Recovery.
    /// Holding: Startup, then a hit at once and one every TickInterval while the key is down
    ///          (at most HoldMax), then Recovery. Released during the startup, it still hits once.
    /// Charge:  charges while the key is down (it fires by itself after HoldMax), then Startup,
    ///          the hits, Recovery.
    /// "The hits" is one hit, or a burst of HitCount hits, HitInterval apart.
    /// </summary>
    public sealed class SkillCast
    {
        SkillDefinition _skill;
        float _start;
        float _release;
        bool _running;
        int _hitsTaken;

        /// <summary>The skill in use, or null when none has been started or it was cancelled.</summary>
        public SkillDefinition Skill => _running ? _skill : null;

        public void Start(SkillDefinition skill, float now)
        {
            _skill = skill ?? throw new ArgumentNullException(nameof(skill));
            _start = now;
            _release = float.PositiveInfinity;
            _hitsTaken = 0;
            _running = true;
        }

        /// <summary>The key was let go. Only the first release counts, and only Holding and Charge care.</summary>
        public void Release(float now)
        {
            if (_running && now < _release) _release = now;
        }

        /// <summary>Ends the skill at once. Hits that were not yet taken never happen.</summary>
        public void Cancel() => _running = false;

        bool IsHolding => _skill.Input == SkillInput.Holding;

        float ChannelStart => IsHolding ? _start + _skill.Startup : _start;

        float ChannelEnd
        {
            get
            {
                float latest = ChannelStart + Math.Max(0f, _skill.HoldMax);
                float end = Math.Min(_release, latest);
                return end < ChannelStart ? ChannelStart : end;
            }
        }

        /// <summary>When the startup begins: at the press, or for a Charge skill at the release.</summary>
        float StartupBegin => _skill.Input == SkillInput.Charge ? ChannelEnd : _start;

        float FirstHit => IsHolding ? ChannelStart : StartupBegin + _skill.Startup;

        int BurstHits => IsHolding ? 1 : Math.Max(1, _skill.HitCount);

        float LastHit => FirstHit + (BurstHits - 1) * Math.Max(0f, _skill.HitInterval);

        float End => (IsHolding ? ChannelEnd : LastHit) + _skill.Recovery;

        public SkillPhase Phase(float now)
        {
            if (!_running || now < _start) return SkillPhase.None;
            if (IsHolding)
            {
                if (now < ChannelStart) return SkillPhase.Startup;
                if (now < ChannelEnd) return SkillPhase.Channel;
            }
            else
            {
                if (_skill.Input == SkillInput.Charge && now < ChannelEnd) return SkillPhase.Channel;
                if (now < FirstHit) return SkillPhase.Startup;
                if (now < LastHit) return SkillPhase.Active;
            }
            return now < End ? SkillPhase.Recovery : SkillPhase.None;
        }

        public bool IsRunning(float now) => Phase(now) != SkillPhase.None;

        /// <summary>
        /// True up to the last hit (for a Holding skill, the end of the hold): the startup, the cast, the hold, the
        /// charge and the burst. False in the recovery and when nothing runs. Decision 0004: the commitment ends here.
        /// </summary>
        public bool IsCommitted(float now)
        {
            SkillPhase phase = Phase(now);
            return phase == SkillPhase.Startup || phase == SkillPhase.Channel || phase == SkillPhase.Active;
        }

        /// <summary>
        /// When the recovery ends, as known now. For a Holding or Charge skill not yet let go it moves with the
        /// release, so it is final only once the skill is in its recovery. Negative infinity when nothing runs.
        /// </summary>
        public float RecoveryEndsAt => _running ? End : float.NegativeInfinity;

        /// <summary>How many hits have come due by now, counted from the start of this use.</summary>
        public int HitsDue(float now)
        {
            if (!_running || now < FirstHit) return 0;
            if (IsHolding)
            {
                if (_skill.TickInterval <= 0f) return 1;
                float held = Math.Min(now, ChannelEnd) - ChannelStart;
                return 1 + (int)Math.Floor(held / _skill.TickInterval + 1e-4f);
            }
            int burst = BurstHits;
            if (burst == 1 || _skill.HitInterval <= 0f) return burst;
            return Math.Min(burst, 1 + (int)Math.Floor((now - FirstHit) / _skill.HitInterval + 1e-4f));
        }

        /// <summary>
        /// The hits that came due since the last call. A long frame returns several at once,
        /// and a cancelled skill returns none.
        /// </summary>
        public int TakeHits(float now)
        {
            int due = HitsDue(now);
            int fresh = due - _hitsTaken;
            if (fresh <= 0) return 0;
            _hitsTaken = due;
            return fresh;
        }

        /// <summary>For a Charge skill: 0 at the press, 1 at full charge; it stops growing at the release. 1 for other skills.</summary>
        public float Charge(float now)
        {
            if (!_running || _skill.Input != SkillInput.Charge) return 1f;
            if (_skill.ChargeTime <= 0f) return 1f;
            return Clamp01((Math.Min(now, ChannelEnd) - _start) / _skill.ChargeTime);
        }

        /// <summary>How far the startup has come, 0 to 1: 0 before it begins, 1 from the first hit on. 0 when nothing runs.</summary>
        public float StartupProgress(float now)
        {
            if (!_running) return 0f;
            if (_skill.Startup <= 0f) return now >= StartupBegin ? 1f : 0f;
            return Clamp01((now - StartupBegin) / _skill.Startup);
        }

        /// <summary>
        /// For a bar on screen, 0 to 1: how far the startup has come (Startup phase), how much of the
        /// longest hold is used (Holding), or the charge (Charge). 0 in a burst, in recovery and when nothing runs.
        /// </summary>
        public float Progress(float now)
        {
            switch (Phase(now))
            {
                case SkillPhase.Startup:
                    return StartupProgress(now);
                case SkillPhase.Channel:
                    if (_skill.Input == SkillInput.Charge) return Charge(now);
                    return _skill.HoldMax <= 0f ? 1f : Clamp01((now - ChannelStart) / _skill.HoldMax);
                default:
                    return 0f;
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
