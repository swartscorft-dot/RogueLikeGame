using System;

namespace Roguelike.Rules
{
    /// <summary>The numbers of a stagger bar (work order 008). The guard has one; any enemy may carry one.</summary>
    [Serializable]
    public sealed class StaggerValues
    {
        public float Max = 100f;                    // points in a full bar
        public float DazeSeconds = 4f;              // s it stays dazed when the bar is empty
        public float DazedDamageMultiplier = 1.5f;  // damage it takes while dazed, against the hit's own
    }

    /// <summary>
    /// A stagger bar, with no engine in it (work order 008). Hits take stagger off it, each by its skill's
    /// <see cref="SkillDefinition.Stagger"/>; it does not refill by itself. When it is empty its bearer is dazed for
    /// <see cref="StaggerValues.DazeSeconds"/>: it takes <see cref="StaggerValues.DazedDamageMultiplier"/> times the
    /// damage, and stagger is not taken while it lasts. When the daze ends the bar is full again.
    ///
    /// What a daze does to the bearer (it stops, its windup is dropped) is the bearer's business: the rule only says
    /// when it starts and when it ends. Each step is told the time.
    /// </summary>
    public sealed class StaggerBar
    {
        readonly StaggerValues _values;
        float _dazedUntil = float.NegativeInfinity;
        bool _dazed;

        public StaggerBar(StaggerValues values)
        {
            _values = values ?? new StaggerValues();
            Current = _values.Max;
        }

        public StaggerValues Values => _values;
        /// <summary>The points left in the bar. 0 all through a daze.</summary>
        public float Current { get; private set; }
        public float Max => _values.Max;
        /// <summary>0 to 1: the share of the bar left.</summary>
        public float Fill => _values.Max > 0f ? Current / _values.Max : 0f;
        /// <summary>When the running daze ends; meaningless when it is not dazed.</summary>
        public float DazedUntil => _dazedUntil;

        /// <summary>Is it dazed at this time? A daze that has run out counts as over even before <see cref="Tick"/>.</summary>
        public bool IsDazed(float now) => _dazed && now < _dazedUntil;

        /// <summary>
        /// Takes stagger off the bar. Returns the points actually taken (no more than are left; none while dazed or
        /// for an amount of 0 or less). <paramref name="dazed"/> is true when this hit emptied the bar and started a daze.
        /// </summary>
        public float Take(float amount, float now, out bool dazed)
        {
            dazed = false;
            Tick(now);
            if (amount <= 0f || _dazed) return 0f;
            float taken = Math.Min(amount, Current);
            Current -= taken;
            if (Current <= 1e-4f)
            {
                Current = 0f;
                _dazed = true;
                _dazedUntil = now + _values.DazeSeconds;
                dazed = true;
            }
            return taken;
        }

        /// <summary>The damage a hit of this size deals at this time: more while dazed. Rounded to whole points.</summary>
        public int Scale(int damage, float now) =>
            IsDazed(now) ? (int)Math.Round(damage * (double)_values.DazedDamageMultiplier, MidpointRounding.AwayFromZero) : damage;

        /// <summary>Advances to now. Returns true on the one step in which a daze ends; the bar is then full.</summary>
        public bool Tick(float now)
        {
            if (!_dazed || now < _dazedUntil) return false;
            _dazed = false;
            Current = _values.Max;
            return true;
        }

        /// <summary>Full, and not dazed: as after a death, or going home.</summary>
        public void Reset()
        {
            _dazed = false;
            _dazedUntil = float.NegativeInfinity;
            Current = _values.Max;
        }

        /// <summary>
        /// Dazed now for <paramref name="seconds"/>, as an emptied bar is (its damage raised by <see cref="StaggerValues.DazedDamageMultiplier"/>,
        /// no stagger taken, full again at the end), whatever is left in it (work order 147: the trial boss's crash and its broken ward).
        /// A daze already running that lasts longer is kept.
        /// </summary>
        public void DazeFor(float seconds, float now)
        {
            if (seconds <= 0f) return;
            float until = now + seconds;
            if (_dazed && _dazedUntil >= until) return;
            Current = 0f;
            _dazed = true;
            _dazedUntil = until;
        }
    }
}
