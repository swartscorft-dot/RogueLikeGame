using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// How the focus gauge works (decision 0005, his words of 2026-11-19: "Det kan vel kanskje drops/valg i runnet
    /// avgjøre?", drops or choices in the run could decide whether focus is a small, frequent spend or a rare, big
    /// window; work order 025, 2026-11-20). Today's is the gauge as it was before the choice: a run uses it until the choice
    /// after floor 1's reward, and the practice room uses it unless one of its keys picks another.
    /// </summary>
    public enum FocusMode
    {
        /// <summary>A full gauge spent at once: cooldowns cut by 40 % and a window in which basic shots mark (6 s), with the gains (work order 071).</summary>
        Today,
        /// <summary>The high-tempo way: three parts, Z spends one; each cuts cooldowns a little, primes the next skill and gives the gains for 2 s.</summary>
        HairTrigger,
        /// <summary>The big window: a full gauge, a longer window with the gains, and Triple Tap and Scatter Blast change shape in it.</summary>
        SteadyHand,
    }

    /// <summary>What a primed skill gains (work order 025): played as if she had done its timing right.</summary>
    public enum PrimeUse
    {
        /// <summary>Nothing it can use: it leaves the priming for the next skill (Slide, Snap Shot).</summary>
        None,
        /// <summary>A recast skill with a clean press (Triple Tap, Backstep): its last press is clean anywhere in the window.</summary>
        CleanPress,
        /// <summary>A Charge skill with a perfect band (Long Shot): a release at half charge or more is perfect.</summary>
        PerfectRelease,
        /// <summary>Any other skill that does not spend marks: one mark more than it would on each target it hits.</summary>
        ExtraMark,
        /// <summary>A spender: it counts one more mark than the target has, up to the cap.</summary>
        SpenderExtraMark,
    }

    /// <summary>The numbers of the two ways the gauge can work (work order 025, 2026-11-20). All first guesses.</summary>
    [Serializable]
    public sealed class FocusModeValues
    {
        /// <summary>The floor after whose reward the choice comes up, once a run.</summary>
        public int OfferAfterFloor = 1;

        // Hair Trigger.
        public int Parts = 3;                   // the gauge is in this many parts; Z spends one full part
        public float PartCooldownCut = 0.15f;   // a spent part cuts every running cooldown by this share of what is left
        public float PrimeSeconds = 4f;         // a part primes the next skill she starts within this many seconds
        public float PrimedChargeShare = 0.5f;  // a primed Charge skill's release is perfect from this share of its charge
        public int PrimedExtraMarks = 1;        // a primed skill adds this many marks more (a spender counts this many more)
        /// <summary>
        /// Work order 071: s a spent part gives the gains for (<see cref="FocusGains"/>), added to what is left of the one
        /// before: today's 6 s window over the three parts, so a full gauge spent part by part gives the same seconds. The run's
        /// Long focus lengthens it as it does the priming. 0: a part gives no gains, as before the order.
        /// </summary>
        public float PartBoostSeconds = 2f;

        // Steady Hand.
        public float WindowSeconds = 10f;       // the window a full gauge opens (6 s today)
        public string MoreTapsSkill = "Triple Tap";
        public int MoreTaps = 2;                // presses added to the run's Triple Tap in the window: five, its clean press the fifth
        public string TwiceSkill = "Scatter Blast";
        public int TwiceTimes = 2;              // it fires this many times in the window
        public float TwiceDelay = 0.25f;        // s between its blasts, the same way
    }

    /// <summary>The rules of the two ways (work order 025), with no engine in them.</summary>
    public static class FocusModeRules
    {
        /// <summary>The name the choice, the pause menu and the end screens show.</summary>
        public static string Name(FocusMode mode) =>
            mode == FocusMode.HairTrigger ? "Hair Trigger" : mode == FocusMode.SteadyHand ? "Steady Hand" : "Today's focus";

        /// <summary>The word the log uses: "today", "hair trigger", "steady hand".</summary>
        public static string Word(FocusMode mode) =>
            mode == FocusMode.HairTrigger ? "hair trigger" : mode == FocusMode.SteadyHand ? "steady hand" : "today";

        /// <summary>The two options of the choice, in the order shown (1, 2). Nothing is drawn: always these two.</summary>
        public static readonly FocusMode[] Options = { FocusMode.HairTrigger, FocusMode.SteadyHand };

        /// <summary>
        /// Three lines of plain words on what the Z key does from now on, for the choice on screen. Steady Hand's cut is
        /// today's (<see cref="IdentityValues.CooldownCut"/>), passed in. Work order 071: the lines say the gains too, from
        /// <paramref name="id"/> (the start values when none is given).
        /// </summary>
        public static string[] Lines(FocusMode mode, FocusModeValues v, float todayCut = 0.4f, IdentityValues id = null)
        {
            v = v ?? new FocusModeValues();
            id = id ?? new IdentityValues();
            if (mode == FocusMode.HairTrigger)
                return new[]
                {
                    string.Format(Inv, "The gauge is in {0} parts. Z spends one full part; you can hold all {0}.", v.Parts),
                    v.PartBoostSeconds > 0f && FocusGains.Any(id)
                        ? string.Format(Inv, "Each part cuts running cooldowns by {0:0}% of what is left and powers you up for {1:0.#} s.", v.PartCooldownCut * 100f, v.PartBoostSeconds)
                        : string.Format(Inv, "Each part cuts every running cooldown by {0:0}% of what is left.", v.PartCooldownCut * 100f),
                    string.Format(Inv, "And primes the next skill you start within {0:0} s: its timing counts as right.", v.PrimeSeconds),
                };
            if (mode == FocusMode.SteadyHand)
                return new[]
                {
                    string.Format(Inv, "Only a full gauge: Z cuts every running cooldown by {0:0}%, as today.", todayCut * 100f),
                    string.Format(Inv, FocusGains.Any(id) ? "For {0:0} s you are powered up and every basic shot that hits marks." : "For {0:0} s every basic shot that hits marks.", v.WindowSeconds),
                    string.Format(Inv, "In it {0} takes {1} more presses and {2} fires {3} times.", v.MoreTapsSkill, v.MoreTaps, v.TwiceSkill, v.TwiceTimes),
                };
            return new[] { "A full gauge: Z cuts every running cooldown.", "For a while every basic shot that hits marks.", FocusGains.Any(id) ? FocusGains.Words(id) : "" };
        }

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// What priming gives this skill: a clean last press for a recast skill with a clean press, a perfect release for
        /// a Charge skill with a perfect band, else a mark more (or, for a spender, a mark more counted). Slide hits
        /// nothing and Snap Shot is perfect already: they leave it.
        /// </summary>
        public static PrimeUse UseFor(SkillDefinition skill)
        {
            if (skill == null || skill.Input == SkillInput.KeyMove) return PrimeUse.None;
            if (skill.RecastCount > 1 && RecastChain.HasCleanPress(skill)) return PrimeUse.CleanPress;
            if (skill.Input == SkillInput.Charge && skill.NoCharge) return PrimeUse.None; // Snap Shot: always perfect already
            if (skill.Input == SkillInput.Charge && skill.PerfectTo > skill.PerfectFrom) return PrimeUse.PerfectRelease;
            return MarkRules.Spends(skill, false) ? PrimeUse.SpenderExtraMark : PrimeUse.ExtraMark;
        }

        /// <summary>A primed spender's count on a target that has <paramref name="has"/> marks: more, up to the cap.</summary>
        public static int SpenderCount(int has, int extra, int cap) => Math.Min(Math.Max(0, cap), Math.Max(0, has) + Math.Max(0, extra));

        /// <summary>Is a primed Charge skill's release at this charge (0 to 1) perfect? From the share on, both edges inside.</summary>
        public static bool PrimedPerfect(float charge, FocusModeValues v) => charge >= (v ?? new FocusModeValues()).PrimedChargeShare - 1e-4f;

        /// <summary>A primed recast press: a press of the last shot that the chain judged ordinary is clean.</summary>
        public static RecastPress Primed(RecastPress judged, bool nextIsLast, bool hasCleanPress) =>
            judged == RecastPress.Shot && nextIsLast && hasCleanPress ? RecastPress.Clean : judged;

        /// <summary>
        /// A skill as Steady Hand's window reads it: a copy of the run's skill, with the more-taps skill two presses
        /// longer (on top of whatever its upgrades made it) and the twice skill a burst of two, a quarter second apart.
        /// Any other skill is given back as it is.
        /// </summary>
        public static SkillDefinition InWindow(SkillDefinition skill, FocusModeValues v)
        {
            if (skill == null) return null;
            v = v ?? new FocusModeValues();
            bool taps = skill.Name == v.MoreTapsSkill && skill.RecastCount > 1 && v.MoreTaps > 0;
            bool twice = skill.Name == v.TwiceSkill && v.TwiceTimes > 1;
            if (!taps && !twice) return skill;
            SkillDefinition copy = skill.Copy();
            if (taps) copy.RecastCount += v.MoreTaps;
            if (twice)
            {
                copy.HitCount = Math.Max(1, skill.HitCount) * v.TwiceTimes;
                copy.HitInterval = Math.Max(0f, v.TwiceDelay);
            }
            return copy;
        }

        /// <summary>True once the floor's reward is the one after which the choice comes up.</summary>
        public static bool OfferedAfter(int floor, FocusModeValues v) => floor == (v ?? new FocusModeValues()).OfferAfterFloor;
    }

    /// <summary>
    /// Hair Trigger's priming (work order 025): a spent part primes the next skill she starts within a few seconds. The
    /// skill that uses it spends it; otherwise it lapses. Time is passed in, in seconds.
    /// </summary>
    public sealed class FocusPrime
    {
        float _until = float.NegativeInfinity;
        bool _on;

        public bool IsPrimed(float now) => _on && now < _until;
        public float Remaining(float now) => IsPrimed(now) ? _until - now : 0f;

        /// <summary>Primed for this long from now; a second part spent while primed starts it again.</summary>
        public void Prime(float now, float seconds)
        {
            _on = true;
            _until = now + Math.Max(0f, seconds);
        }

        /// <summary>A skill used it. False when there was none to use.</summary>
        public bool Spend(float now)
        {
            if (!IsPrimed(now)) return false;
            _on = false;
            return true;
        }

        /// <summary>True once, when a priming nobody used has run out by now.</summary>
        public bool TakeLapsed(float now)
        {
            if (!_on || now < _until) return false;
            _on = false;
            return true;
        }

        public void Clear()
        {
            _on = false;
            _until = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// Steady Hand's view of the skills (work order 025): while its window is open, the run's skills as the window
    /// changes them (<see cref="FocusModeRules.InWindow"/>), the way <see cref="RunSkillUpgrades.View"/> patches them:
    /// copies, made again only when the list changes, never written back.
    /// </summary>
    public sealed class SteadyHandView
    {
        IReadOnlyList<SkillDefinition> _of;
        SkillDefinition[] _view;

        public IReadOnlyList<SkillDefinition> View(IReadOnlyList<SkillDefinition> skills, FocusModeValues v)
        {
            if (skills == null) return null;
            if (_view != null && ReferenceEquals(_of, skills)) return _view;
            _of = skills;
            _view = new SkillDefinition[skills.Count];
            for (int i = 0; i < skills.Count; i++) _view[i] = FocusModeRules.InWindow(skills[i], v);
            return _view;
        }
    }

    /// <summary>
    /// A run's focus choice (work order 025): offered once, after floor 1's reward; until it is taken the gauge works as
    /// today. Cleared when a run starts or ends, like the run's upgrades. Nothing is written to the settings.
    /// </summary>
    public sealed class RunFocusChoice
    {
        public FocusMode Mode { get; private set; } = FocusMode.Today;
        public bool Offered { get; private set; }
        public bool Taken { get; private set; }

        public void Offer() => Offered = true;

        public void Take(FocusMode mode)
        {
            Mode = mode;
            Taken = true;
        }

        public void Clear()
        {
            Mode = FocusMode.Today;
            Offered = false;
            Taken = false;
        }
    }
}
