using System;

namespace Roguelike.Rules
{
    /// <summary>The numbers of the identity gauge. They live in the skill settings asset.</summary>
    [Serializable]
    public sealed class IdentityValues
    {
        public float MaxFocus = 100f;          // the gauge is full at this much focus
        public float FocusPerDamage = 0.005f;  // focus gained per point of damage dealt: a trickle (decision 0005)
        public float EmpoweredDuration = 6f;   // s the empowered mode lasts: the window in which every basic shot hit marks
        public float EmpoweredDamage = 1f;     // damage is multiplied by this while empowered (decision 0005: none extra)
        public float FocusPerMarkSpent = 5f;   // focus gained per mark a spender takes
        public float FocusPerCleanTap = 5f;    // focus gained per clean tap
        public float CooldownCut = 0.4f;       // spending a full gauge cuts every running cooldown by this share of what is left

        // Work order 071 (Kristian, 2026-11-21: "videre, så må vi reworke focus, det føles ræva. glemmer å bruke den heller,
        // fordi man ikke føler det. føler man må få mere attackdmg, attackspeed, cooldown osv."): the gains, what spending
        // focus makes her for its seconds (<see cref="FocusGains"/>). First guesses of the order. New fields with their defaults
        // here in code: the settings asset holds none of them, and EmpoweredDamage above stays 1 there. Set to none (1, 1, 1,
        // 0) focus is exactly what it was before the order.
        public float EmpoweredDamageScale = 1.35f;  // her basic shot's and her skills' damage times this while the gains run (+35 %)
        public float EmpoweredFireRate = 1.4f;      // her basic shot fires this many times as fast: the time from shot to shot over it
        public float EmpoweredCooldownRate = 1.6f;  // every running cooldown, the dash's too, runs this many times as fast (never past the run's floor)
        public float EmpoweredMoveShare = 0.1f;     // she runs this share faster (+10 %)
        /// <summary>
        /// s: the basic shot's recovery is never cut under this by the faster fire (the Shoot clip's 0.05 s fade in, as
        /// <see cref="StatValues.MostAttackSpeed"/> keeps it): each shot's pose, kick and sound still show apart from the next.
        /// </summary>
        public float EmpoweredLeastRecovery = 0.05f;
    }

    /// <summary>
    /// The identity gauge of the first class: focus. Decision 0005 (2026-11-19): spent marks and clean taps fill it,
    /// and damage a little. When it is full it can be spent on the empowered mode: every running cooldown is cut, and
    /// for a while every basic shot hit marks (the cut and the marks are the character's; this is the gauge).
    /// No focus is gained while empowered. Time is passed in, in seconds.
    ///
    /// The gains (work order 071): for as long as the window runs, and with Hair Trigger for a short while after each part
    /// spent (it has no window), she is stronger (<see cref="IsBoosted"/>, <see cref="FocusGains"/>). The window and the
    /// gains are kept apart: only the window makes basic shots mark and holds focus back, as before.
    /// </summary>
    public sealed class FocusGauge
    {
        float _focus;
        float _empoweredUntil = float.NegativeInfinity;
        float _windowSeconds; // how long the window running now was opened for (work order 025: Steady Hand's is longer)
        // Work order 071: the gains' own time, the window's, or a Hair Trigger part's.
        float _boostFrom = float.NegativeInfinity;
        float _boostUntil = float.NegativeInfinity;

        public float Focus => _focus;

        // ---------------- The gains' time (work order 071) ----------------

        /// <summary>True while the gains run: in the window, or in a Hair Trigger part's seconds.</summary>
        public bool IsBoosted(float now) => now >= _boostFrom && now < _boostUntil;
        public float BoostRemaining(float now) => IsBoosted(now) ? _boostUntil - now : 0f;
        /// <summary>How long the gains running now were given for, from their start (a part spent while they run adds on).</summary>
        public float BoostSeconds => _boostUntil > _boostFrom ? _boostUntil - _boostFrom : 0f;

        /// <summary>The seconds between <paramref name="from"/> and <paramref name="to"/> in which the gains ran.</summary>
        public float BoostedSeconds(float from, float to)
        {
            float a = Math.Max(from, _boostFrom), b = Math.Min(to, _boostUntil);
            return b > a ? b - a : 0f;
        }

        /// <summary>The gains from now for <paramref name="seconds"/>, or that much longer when they run already.</summary>
        void Boost(float now, float seconds)
        {
            if (seconds <= 0f) return;
            if (!IsBoosted(now)) _boostFrom = now;
            _boostUntil = Math.Max(now, _boostUntil) + seconds;
        }

        public bool IsEmpowered(float now) => now < _empoweredUntil;
        public float EmpoweredRemaining(float now) => now < _empoweredUntil ? _empoweredUntil - now : 0f;
        public bool IsFull(IdentityValues values) => _focus >= values.MaxFocus;

        /// <summary>Focus for damage dealt. Nothing while empowered, and never more than a full gauge. Returns the focus gained.</summary>
        public float AddDamage(int damage, IdentityValues values, float now) =>
            damage <= 0 ? 0f : Add(damage * values.FocusPerDamage, values, now);

        /// <summary>Focus from any source. Nothing while empowered, and never more than a full gauge. Returns the focus gained.</summary>
        public float Add(float amount, IdentityValues values, float now)
        {
            if (amount <= 0f || IsEmpowered(now)) return 0f;
            float before = _focus;
            _focus = Math.Min(values.MaxFocus, _focus + amount);
            return _focus - before;
        }

        /// <summary>Spends a full gauge on the empowered mode. False, and nothing happens, if it is not full or already running.</summary>
        public bool TryActivate(float now, IdentityValues values) => TryActivate(now, values, values.EmpoweredDuration);

        /// <summary>
        /// Spends a full gauge on a window of <paramref name="seconds"/> (work order 025: Steady Hand's is longer than
        /// today's). False, and nothing happens, if it is not full or already running.
        /// </summary>
        public bool TryActivate(float now, IdentityValues values, float seconds)
        {
            if (IsEmpowered(now) || !IsFull(values)) return false;
            _focus = 0f;
            _windowSeconds = Math.Max(0f, seconds);
            _empoweredUntil = now + _windowSeconds;
            // Work order 071: the gains run with the window, exactly as long.
            _boostFrom = now;
            _boostUntil = _empoweredUntil;
            return true;
        }

        // ---------------- Hair Trigger (work order 025, 2026-11-20): the gauge in parts ----------------

        /// <summary>The focus one part holds when the gauge is in <paramref name="parts"/> parts.</summary>
        public static float PartSize(IdentityValues values, int parts) => parts <= 0 ? values.MaxFocus : values.MaxFocus / parts;

        /// <summary>How many whole parts are full, 0 to <paramref name="parts"/>.</summary>
        public int PartsFull(IdentityValues values, int parts)
        {
            float size = PartSize(values, parts);
            if (size <= 0f) return 0;
            return Math.Min(Math.Max(1, parts), (int)Math.Floor(_focus / size + 1e-4f));
        }

        /// <summary>
        /// Spends one full part: a part's worth of focus comes off, and what fills the next part stays. False, and
        /// nothing happens, with no part full. There is no window.
        /// </summary>
        public bool TrySpendPart(IdentityValues values, int parts)
        {
            if (PartsFull(values, parts) < 1) return false;
            _focus = Math.Max(0f, _focus - PartSize(values, parts));
            return true;
        }

        /// <summary>
        /// Work order 071: spends one full part as above, and gives the gains for <paramref name="boostSeconds"/> from now,
        /// added to what is left of a part spent before (three parts at once: three times as long). Still no window.
        /// </summary>
        public bool TrySpendPart(IdentityValues values, int parts, float now, float boostSeconds)
        {
            if (!TrySpendPart(values, parts)) return false;
            Boost(now, boostSeconds);
            return true;
        }

        /// <summary>Fills the gauge. For tests and tools.</summary>
        public void Fill(IdentityValues values) => _focus = values.MaxFocus;

        /// <summary>Back to its start: an empty gauge and no window (work order 026: a run starts with a clean slate).</summary>
        public void Clear()
        {
            _focus = 0f;
            EndWindow();
        }

        /// <summary>
        /// Ends a window that runs, and keeps the focus (the second reviews of 2026-11-20, work order 033: a window opened
        /// on floor 1 must not carry into the way of the gauge picked after it). The gains end with it (work order 071).
        /// </summary>
        public void EndWindow()
        {
            _empoweredUntil = float.NegativeInfinity;
            _windowSeconds = 0f;
            _boostFrom = float.NegativeInfinity;
            _boostUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Damage times this now: the window's own multiplier (1 since decision 0005), and since work order 071 the gains'
        /// (<see cref="IdentityValues.EmpoweredDamageScale"/>) while they run.
        /// </summary>
        public float DamageMultiplier(float now, IdentityValues values) =>
            (IsEmpowered(now) ? values.EmpoweredDamage : 1f) * FocusGains.DamageScale(IsBoosted(now), values);

        /// <summary>Damage after the empowered mode's multiplier, rounded to a whole number.</summary>
        public int Scale(int damage, float now, IdentityValues values) =>
            (int)Math.Round(damage * DamageMultiplier(now, values));

        /// <summary>
        /// For the bar, 0 to 1: how full the gauge is, or while empowered how much of the window is left, of the length it
        /// was opened for.
        /// </summary>
        public float BarFill(float now, IdentityValues values)
        {
            if (IsEmpowered(now))
                return _windowSeconds <= 0f ? 0f : EmpoweredRemaining(now) / _windowSeconds;
            return values.MaxFocus <= 0f ? 0f : Math.Min(1f, _focus / values.MaxFocus);
        }
    }
}
