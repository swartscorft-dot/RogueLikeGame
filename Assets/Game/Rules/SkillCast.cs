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
        /// <summary>Locked after the last hit.</summary>
        Recovery,
    }

    /// <summary>
    /// One use of a skill, from the press to the end of its recovery. Time is passed in, in seconds.
    /// The character is locked for as long as <see cref="IsRunning"/>.
    ///
    /// Normal, Point, Casting: Startup, one hit, Recovery.
    /// Holding: Startup, then a hit at once and one every TickInterval while the key is down
    ///          (at most HoldMax), then Recovery. Released during the startup, it still hits once.
    /// Charge:  charges while the key is down (it fires by itself after HoldMax), then Startup,
    ///          one hit, Recovery.
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

        float ChannelStart => _skill.Input == SkillInput.Holding ? _start + _skill.Startup : _start;

        float ChannelEnd
        {
            get
            {
                float latest = ChannelStart + Math.Max(0f, _skill.HoldMax);
                float end = Math.Min(_release, latest);
                return end < ChannelStart ? ChannelStart : end;
            }
        }

        float FirstHit
        {
            get
            {
                switch (_skill.Input)
                {
                    case SkillInput.Holding: return ChannelStart;
                    case SkillInput.Charge: return ChannelEnd + _skill.Startup;
                    default: return _start + _skill.Startup;
                }
            }
        }

        float End => (_skill.Input == SkillInput.Holding ? ChannelEnd : FirstHit) + _skill.Recovery;

        public SkillPhase Phase(float now)
        {
            if (!_running || now < _start) return SkillPhase.None;
            switch (_skill.Input)
            {
                case SkillInput.Holding:
                    if (now < ChannelStart) return SkillPhase.Startup;
                    if (now < ChannelEnd) return SkillPhase.Channel;
                    break;
                case SkillInput.Charge:
                    if (now < ChannelEnd) return SkillPhase.Channel;
                    if (now < FirstHit) return SkillPhase.Startup;
                    break;
                default:
                    if (now < FirstHit) return SkillPhase.Startup;
                    break;
            }
            return now < End ? SkillPhase.Recovery : SkillPhase.None;
        }

        public bool IsRunning(float now) => Phase(now) != SkillPhase.None;

        /// <summary>How many hits have come due by now, counted from the start of this use.</summary>
        public int HitsDue(float now)
        {
            if (!_running || now < FirstHit) return 0;
            if (_skill.Input != SkillInput.Holding || _skill.TickInterval <= 0f) return 1;
            float held = Math.Min(now, ChannelEnd) - ChannelStart;
            return 1 + (int)Math.Floor(held / _skill.TickInterval + 1e-4f);
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

        /// <summary>
        /// For a bar on screen, 0 to 1: how far the startup has come (Startup phase), how much of the
        /// longest hold is used (Holding), or the charge (Charge). 0 in recovery and when nothing runs.
        /// </summary>
        public float Progress(float now)
        {
            switch (Phase(now))
            {
                case SkillPhase.Startup:
                    float begin = _skill.Input == SkillInput.Charge ? ChannelEnd : _start;
                    return _skill.Startup <= 0f ? 1f : Clamp01((now - begin) / _skill.Startup);
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
