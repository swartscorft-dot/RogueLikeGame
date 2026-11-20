using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the cast bar (work order 040, Kristian's second session of 2026-11-20: "I do not find the F and
    /// right-click spells intuitive to time. Rather a casting bar that shows start to end, and where to press."), first
    /// guesses, with their defaults here in code: no asset holds them. Sizes in pixels of a view 720 high (the HUD's),
    /// times in real seconds.
    /// </summary>
    [Serializable]
    public sealed class CastBarValues
    {
        /// <summary>Off: no cast bar; the skill bar, the key's strip and the ring (if it is on) as they are.</summary>
        public bool On = true;
        public float WidthShare = 0.2f;      // of the screen's width: about a fifth
        public float LeastWidth = 180f;      // px: never narrower than this, on a narrow screen
        public float Height = 16f;           // px: thick enough to read at a glance
        public float BelowMiddle = 64f;      // px: its top this far under the middle of the screen, over her feet and clear of the skill bar
        public float LabelHeight = 20f;      // px: the skill's name and which press it is, above the bar
        public float FlashSeconds = 0.3f;    // s of real time the bar flashes in the colour of what the press was, then goes
    }

    /// <summary>What the cast bar shows: one of her timed things (work order 040), or nothing.</summary>
    public enum CastBarKind
    {
        None,
        /// <summary>A recast chain's next press (Triple Tap, Backstep): from the press before to the end of its window.</summary>
        Recast,
        /// <summary>A Charge skill's charge (Long Shot): from the start of the charge to where it fires by itself.</summary>
        Charge,
        /// <summary>A Holding skill's hold (Fan Fire): its startup, then how long it can be held.</summary>
        Hold,
        /// <summary>A Casting skill's cast (Heavy Volley): from the press to the hit.</summary>
        Cast,
    }

    /// <summary>What the bar flashes when the press comes (work order 040): the colour of what it was.</summary>
    public enum CastBarFlash
    {
        None,
        /// <summary>A clean press, a perfect release.</summary>
        Clean,
        /// <summary>Anything else: a plain shot (early or late), a release outside the band, the end of a hold or a cast.</summary>
        Plain,
    }

    /// <summary>
    /// The cast bar at one moment (work order 040), all along the bar from 0 (its left end) to 1 (its right end): what it
    /// spans in seconds, the dim part at its start (a press there only waits), the bright zone (the clean part of a recast
    /// window, a charge's perfect band; none when <see cref="HasZone"/> is false), and the marker, now.
    /// </summary>
    public struct CastBarView
    {
        public CastBarKind Kind;
        public float Seconds;
        public float DimTo;
        public float ZoneFrom, ZoneTo;
        public float Marker;

        public bool HasZone => ZoneTo > ZoneFrom;
        /// <summary>Is the marker inside the bright zone.</summary>
        public bool MarkerInZone => HasZone && Marker >= ZoneFrom && Marker <= ZoneTo;
    }

    /// <summary>
    /// Where everything on the cast bar is (work order 040), with no engine in it. Each timed thing of hers is laid out
    /// left to right from its start to its end, on the clock the game judges it on, so the marker is in the zone exactly
    /// when a press (a release) now would be clean (perfect):
    /// - a recast chain: from the press before (its shot's startup, or Backstep's hop, drawn dim: a press there waits in the
    ///   buffer and is never clean) to the end of the window, the clean part (<see cref="CleanPart"/>) the zone, the marker
    ///   on the chain's own clock (game time before the window opens, the window's clock after: it stands still in a pause
    ///   and runs on through a hit stop, work order 038);
    /// - a charge: from the press to <see cref="SkillDefinition.HoldMax"/>, where it fires by itself, the perfect band the
    ///   zone (primed by Hair Trigger, from <see cref="FocusModeValues.PrimedChargeShare"/> of the charge on);
    /// - a hold: its startup dim, then <see cref="SkillDefinition.HoldMax"/>, how long it can be held; no zone;
    /// - a cast: from the press to its hit; no zone.
    /// </summary>
    public static class CastBarRules
    {
        /// <summary>
        /// A recast chain. <paramref name="lead"/>: seconds from the press before to the window opening (the startup of
        /// the shot or the hop); <paramref name="sincePress"/>: seconds since that press, on game time;
        /// <paramref name="sinceOpened"/>: the window's clock, negative before it opens; <paramref name="clean"/>: the clean
        /// part, none when the next press cannot be clean.
        /// </summary>
        public static CastBarView Recast(float lead, float window, float sincePress, float sinceOpened, CleanPart clean)
        {
            lead = Math.Max(0f, lead);
            window = Math.Max(0f, window);
            float total = lead + window;
            if (total <= 0f) return default;
            var v = new CastBarView { Kind = CastBarKind.Recast, Seconds = total, DimTo = lead / total };
            if (clean.Any)
            {
                v.ZoneFrom = (lead + Math.Min(window, clean.From)) / total;
                v.ZoneTo = (lead + Math.Min(window, clean.To)) / total;
            }
            v.Marker = sinceOpened >= 0f
                ? (lead + Math.Min(window, sinceOpened)) / total
                : Math.Max(0f, Math.Min(lead, sincePress)) / total;
            return v;
        }

        /// <summary>
        /// A charge, <paramref name="charged"/> seconds in, that fires by itself at <paramref name="holdMax"/>; its perfect
        /// band from <paramref name="perfectFrom"/> to <paramref name="perfectTo"/> seconds charged (none when the second is
        /// not past the first).
        /// </summary>
        public static CastBarView Charge(float charged, float holdMax, float perfectFrom, float perfectTo)
        {
            float total = Math.Max(1e-3f, Math.Max(holdMax, perfectTo));
            var v = new CastBarView { Kind = CastBarKind.Charge, Seconds = total, Marker = Clamp01(charged / total) };
            if (perfectTo > perfectFrom)
            {
                v.ZoneFrom = Clamp01(perfectFrom / total);
                v.ZoneTo = Clamp01(perfectTo / total);
            }
            return v;
        }

        /// <summary>The perfect band a charge is judged on: the skill's own, or primed (Hair Trigger) from a share of its charge to its end.</summary>
        public static void PerfectBand(SkillDefinition skill, bool primed, FocusModeValues ways, out float from, out float to)
        {
            from = to = 0f;
            if (skill == null) return;
            if (skill.PerfectTo > skill.PerfectFrom)
            {
                from = skill.PerfectFrom;
                to = skill.PerfectTo;
            }
            if (!primed) return;
            float full = skill.ChargeTime > 0f ? skill.ChargeTime : 0f;
            float primedFrom = Clamp01((ways ?? new FocusModeValues()).PrimedChargeShare) * full;
            from = to > from ? Math.Min(from, primedFrom) : primedFrom;
            to = Math.Max(Math.Max(to, skill.HoldMax), full);
        }

        /// <summary>A hold: <paramref name="startup"/> dim, then <paramref name="holdMax"/>; <paramref name="sincePress"/> seconds in.</summary>
        public static CastBarView Hold(float sincePress, float startup, float holdMax)
        {
            startup = Math.Max(0f, startup);
            float total = startup + Math.Max(0f, holdMax);
            if (total <= 0f) return default;
            return new CastBarView { Kind = CastBarKind.Hold, Seconds = total, DimTo = startup / total, Marker = Clamp01(sincePress / total) };
        }

        /// <summary>A cast of <paramref name="castTime"/> seconds, <paramref name="sincePress"/> seconds in.</summary>
        public static CastBarView Cast(float sincePress, float castTime)
        {
            float total = Math.Max(1e-3f, castTime);
            return new CastBarView { Kind = CastBarKind.Cast, Seconds = total, Marker = Clamp01(sincePress / total) };
        }

        /// <summary>The flash's strength, 1 to 0, <paramref name="age"/> real seconds after the press; 0 when it is over.</summary>
        public static float FlashAlpha(float age, float seconds)
        {
            if (age < 0f || seconds <= 0f || age >= seconds) return 0f;
            return 1f - age / seconds;
        }

        /// <summary>What a recast press was, for the flash: clean, or plain (an early or a late shot).</summary>
        public static CastBarFlash OfPress(bool clean) => clean ? CastBarFlash.Clean : CastBarFlash.Plain;

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
