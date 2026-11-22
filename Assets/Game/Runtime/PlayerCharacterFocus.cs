using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Focus, the identity gauge of <see cref="PlayerCharacter"/>: the gauge, the gains of spent focus, the focus choice and
    /// Hair Trigger's prime (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- The identity gauge: focus ----------------

        public IdentityValues Identity => SkillSet != null && SkillSet.Identity != null ? SkillSet.Identity : _fallbackIdentity;
        public float Focus => _focus.Focus;
        public bool IsFocusFull => _focus.IsFull(Identity);
        public bool IsEmpowered => _focus.IsEmpowered(Time.time);
        public float EmpoweredRemaining => _focus.EmpoweredRemaining(Time.time);
        /// <summary>For the bar, 0 to 1: how full the gauge is, or while empowered how much of the mode is left.</summary>
        public float FocusBarFill => _focus.BarFill(Time.time, Identity);
        public bool IsDashing => _dashing;

        // ---------------- The gains of spent focus (work order 071, 2026-11-21) ----------------

        /// <summary>
        /// True while spent focus makes her stronger (<see cref="FocusGains"/>): in the window of today's gauge and of Steady
        /// Hand, and with Hair Trigger for a part's seconds after each part spent. Her shots look and sound empowered for as long.
        /// </summary>
        public bool IsFocusBoosted => _focus.IsBoosted(Time.time);
        /// <summary>Seconds of the gains left, and how long those running were given for (for the effects' last second).</summary>
        public float FocusBoostRemaining => _focus.BoostRemaining(Time.time);
        public float FocusBoostSeconds => _focus.BoostSeconds;
        /// <summary>
        /// True while focus is ready and unspent (work order 071: "ready" on her, not only on the interface): a full gauge, or
        /// with Hair Trigger at least one full part; never while the gains run.
        /// </summary>
        public bool IsFocusReady => !IsFocusBoosted && !IsEmpowered && (FocusMode == FocusMode.HairTrigger ? FocusPartsFull >= 1 : IsFocusFull);
        /// <summary>Her damage times this now from focus: the window's multiplier and the gains'.</summary>
        public float FocusDamageMultiplier => _focus.DamageMultiplier(Time.time, Identity);
        /// <summary>Her basic shot's rate times this now from the gains; 1 outside them.</summary>
        public float FocusFireRate => FocusGains.FireRate(IsFocusBoosted, Identity);
        /// <summary>Her run speed times this now from the gains; 1 outside them.</summary>
        public float FocusMoveScale => FocusGains.MoveScale(IsFocusBoosted, Identity);
        /// <summary>How many times as fast the slot's cooldown runs now: the gains' rate, never past the run's floor (work order 065).</summary>
        public float FocusCooldownRate(int slot)
        {
            if (!IsFocusBoosted) return 1f;
            SkillDefinition skill = slot >= 0 && slot < Skills.Count ? Skills[slot] : null;
            float share = Modifiers != null && skill != null ? Modifiers.CooldownShare(skill.Name) : 1f;
            return FocusGains.CooldownRate(true, Identity, share, Modifiers != null ? Modifiers.CooldownFloor : 0f);
        }
        /// <summary>The same for the dash's cooldown (her dash steps and the rack's Spare Step make its share).</summary>
        float FocusDashCooldownRate =>
            FocusGains.CooldownRate(IsFocusBoosted, Identity, Modifiers != null ? Modifiers.DashCooldownShare : 1f, Modifiers != null ? Modifiers.CooldownFloor : 0f);

        float _gainsTickedAt = float.NaN; // the game time the gains last ran the cooldowns to

        /// <summary>
        /// Each frame: while the gains run, every running cooldown, the dash's too, runs faster (work order 071): the
        /// seconds the gains ran since the last frame, times the rate less one, come off what is left. Nothing is allocated.
        /// </summary>
        void TickFocusGains(float now)
        {
            float from = float.IsNaN(_gainsTickedAt) || _gainsTickedAt > now ? now : _gainsTickedAt;
            _gainsTickedAt = now;
            float boosted = _focus.BoostedSeconds(from, now);
            if (boosted <= 0f) return;
            for (int slot = 0; slot < _cooldowns.Count; slot++)
            {
                float extra = FocusGains.ExtraCooldownSeconds(boosted, FocusCooldownRate(slot));
                if (extra > 0f) CooldownFor(slot).CutSeconds(now, extra);
            }
            float dash = FocusGains.ExtraCooldownSeconds(boosted, FocusDashCooldownRate);
            if (dash > 0f) _dash.CutCooldownSeconds(now, dash);
        }

        // ---------------- The focus choice (work order 025, 2026-11-20) ----------------

        /// <summary>
        /// The run's focus choice (<see cref="RunFocusChoice"/>), put on her by the run while it goes on, as its upgrades
        /// are; null outside a run. In a run before the choice it is today's gauge.
        /// </summary>
        [System.NonSerialized] public RunFocusChoice RunFocus;
        /// <summary>The practice room's own way, set only by its keys (F2, F3, F4); today's unless one was pressed. Not saved anywhere.</summary>
        public FocusMode PracticeFocusMode { get; set; } = FocusMode.Today;
        /// <summary>How the gauge works now: the run's choice in a run, else the practice room's.</summary>
        public FocusMode FocusMode => RunFocus != null ? RunFocus.Mode : PracticeFocusMode;
        /// <summary>The numbers of the two ways, defaults in code; not serialized, so no scene or asset carries them.</summary>
        public FocusModeValues FocusModeValues { get; set; } = new FocusModeValues();
        /// <summary>Hair Trigger's parts; 1 for the whole gauge of the other two.</summary>
        public int FocusParts => FocusMode == FocusMode.HairTrigger ? Mathf.Max(1, FocusModeValues.Parts) : 1;
        /// <summary>Full parts of the gauge: Hair Trigger's, 0 to 3; else 1 when it is full.</summary>
        public int FocusPartsFull => _focus.PartsFull(Identity, FocusParts);
        /// <summary>True while Hair Trigger's priming waits for the next skill.</summary>
        public bool IsPrimed => _prime.IsPrimed(Time.time);
        public float PrimeRemaining => _prime.Remaining(Time.time);
        /// <summary>True while Steady Hand's window is open: Triple Tap and Scatter Blast read as the window changes them.</summary>
        public bool IsSteadyHandWindow => FocusMode == FocusMode.SteadyHand && _focus.IsEmpowered(Time.time);
        /// <summary>What the skill in use gained from the priming; None when it was not primed.</summary>
        public PrimeUse ActivePrime => _actionKind == ActionKind.Skill ? _castPrime : PrimeUse.None;
        /// <summary>Primed skills used, and primings that lapsed (for tests and the readout).</summary>
        public int PrimesUsed { get; private set; }
        public int PrimesLapsed { get; private set; }

        /// <summary>The share of focus she gains: 1, more with the run's Keen focus (work order 025).</summary>
        float FocusGainShare => Modifiers != null ? Modifiers.FocusGainShare : 1f;

        /// <summary>Focus from anything, through the run's Keen focus. Returns the focus gained.</summary>
        float GainFocus(float amount) => _focus.Add(amount * FocusGainShare, Identity, Time.time);

        /// <summary>
        /// Sets the practice room's way (its keys, F2, F3 and F4). Nothing changes in a run, where the run's choice holds.
        /// A priming waiting is dropped. False in a run.
        /// </summary>
        public bool SetPracticeFocusMode(FocusMode mode)
        {
            if (RunFocus != null) return false;
            PracticeFocusMode = mode;
            _prime.Clear();
            return true;
        }

        /// <summary>
        /// Press: spend a full focus gauge on the empowered mode. It takes no time and locks nothing,
        /// so it can be pressed in the middle of a skill. Rejected unless the gauge is full. With Hair Trigger (work order
        /// 025) it spends one full part instead, rejected with none full, and primes the next skill.
        /// </summary>
        public IntentResult ActivateIdentity()
        {
            float now = Time.time;
            FocusMode mode = FocusMode;
            FocusModeValues ways = FocusModeValues;
            // Work order 025: Hair Trigger spends one full part, with no window; Steady Hand a full gauge on a longer window.
            // Work order 071: the window brings the gains; a part brings them for its own seconds (Long focus lengthens them).
            bool started = mode == FocusMode.HairTrigger ? _focus.TrySpendPart(Identity, FocusParts, now, FocusGains.PartSeconds(ways, LongFocusNow))
                         : _focus.TryActivate(now, Identity, (mode == FocusMode.SteadyHand ? ways.WindowSeconds : Identity.EmpoweredDuration) * LongFocusNow); // work order 065: Long focus
            IntentResult result = started ? IntentResult.Started : IntentResult.Rejected;
            Report(new PlayerReport { Kind = PlayerReportKind.FocusPressed, Result = result, FocusMode = mode });
            if (result == IntentResult.Started)
            {
                // Decision 0005: every running cooldown, the dash's too, is cut by a share of what it has left; a part's
                // share is smaller (work order 025).
                float share = mode == FocusMode.HairTrigger ? ways.PartCooldownCut : Identity.CooldownCut;
                var cuts = new float[Skills.Count];
                for (int slot = 0; slot < cuts.Length; slot++) cuts[slot] = CooldownFor(slot).Cut(now, share);
                float dashCut = _dash.CutCooldown(now, share);
                Report(new PlayerReport { Kind = PlayerReportKind.CooldownsCut, Cuts = cuts, DashCut = dashCut });
                if (mode == FocusMode.HairTrigger) _prime.Prime(now, ways.PrimeSeconds * LongFocusNow); // work order 065: Long focus
            }
            return result;
        }

        /// <summary>Fills the focus gauge. For tests and tools.</summary>
        public void FillFocus() => _focus.Fill(Identity);

        /// <summary>
        /// Ends a focus window that runs, and any priming, keeping the gauge's focus (the second reviews of 2026-11-20,
        /// work order 033): called when the run's focus choice is taken.
        /// </summary>
        public void EndFocusWindow()
        {
            _focus.EndWindow();
            _prime.Clear();
        }

        /// <summary>
        /// A skill starts (not a recast): if Hair Trigger's priming waits and the skill can use it, the skill spends it for
        /// all of this use (work order 025). One that cannot (Slide, Snap Shot) leaves it for the next.
        /// </summary>
        void TakePrime(int slot, SkillDefinition skill)
        {
            PrimeUse use = FocusModeRules.UseFor(skill);
            bool primed = use != PrimeUse.None && _prime.Spend(Time.time);
            _castPrime = primed ? use : PrimeUse.None;
            _chainPrimed = primed && use == PrimeUse.CleanPress;
            if (!primed) return;
            PrimesUsed++;
            Report(new PlayerReport { Kind = PlayerReportKind.PrimeUsed, Slot = slot, Prime = use });
        }
    }
}
