using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The recast chains of <see cref="PlayerCharacter"/> (Triple Tap, Backstep): the window, the clean part, the rhythm, a recast
    /// press and the chain's end (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>Basic shot hits counted toward the next mark.</summary>
        public int BasicShotsTowardMark => _basicMarks.Counted;
        /// <summary>The slot whose recast chain waits for its next press, or -1.</summary>
        public int RecastSlot => _chain.Slot;
        /// <summary>Shots of the recast chain pressed so far.</summary>
        public int RecastShots => _chain.Slot >= 0 ? _chain.Shots : 0;
        /// <summary>True while the window for the next press of a recast chain is open.</summary>
        public bool IsRecastWindowOpen => _chain.IsWindowOpen(Time.time);
        /// <summary>For a bar, 1 to 0: how much of the open recast window is left.</summary>
        public float RecastWindowLeft => _chain.WindowLeft(Time.time);
        /// <summary>
        /// The share of the open window in which the next press is clean (the clean part's length over the window's); 0
        /// when the next press cannot be clean (not the last, or the skill has neither a clean tap nor a clean spend).
        /// Before work order 040 the clean part was always that share at the window's end; where it lies now is
        /// <see cref="RecastCleanFrom"/> to <see cref="RecastCleanTo"/>.
        /// </summary>
        public float RecastCleanShare
        {
            get
            {
                CleanPart part = RecastCleanPart;
                float window = _chain.Window;
                return part.Any && window > 0f ? Mathf.Clamp01((part.To - part.From) / window) : 0f;
            }
        }
        // Work order 040 (Kristian's second session of 2026-11-20): the clean part comes early in the window, and the cast
        // bar shows the chain from the press before to the end of the window. They read only.
        /// <summary>
        /// The clean part of the open window, in seconds after it opened (<see cref="CleanPart.Of"/>: early, or with
        /// <see cref="KitValues.CleanEarly"/> off the last share); primed (work order 025) the whole window; none when the next
        /// press cannot be clean (the skill has no clean press, or the chain is complete).
        /// Work order 069: every press of the chain has its own (the quick presses' early, the last's a beat later), so this is
        /// the part of the press whose window is open now, or comes next, whichever press of the chain it is; before, only the
        /// last press's (the presses before it showed none, "veldig utydlig når skal skje"); with the rhythm's switch off
        /// (<see cref="KitValues.ChainRhythm"/>) still only the last's, as this morning.
        /// </summary>
        public CleanPart RecastCleanPart =>
            _chain.Slot >= 0 && !_chain.IsComplete && _chain.Slot < Skills.Count && RecastChain.HasCleanPress(Skills[_chain.Slot]) && ShowsThisPress
                ? (_chainPrimed && _chain.NextIsLast ? CleanPart.Whole(_chain.Window) : ChainCleanPart) : default; // work order 064: after a hop, its own

        /// <summary>
        /// The clean part of the running chain's next press as the kit places it (work order 064: after a hop, its own; work
        /// order 069: each press its own, <see cref="RecastRhythm"/>), widened by her Wide window (work order 067; 0 outside a run
        /// or with the stats off): the ONE place the judging (<see cref="JudgeRecast"/>, a press's rhythm in <see cref="Recast"/>)
        /// and everything that shows it (<see cref="RecastCleanPart"/>: the cast blade, the strip's box, the signs) read it.
        /// </summary>
        CleanPart ChainCleanPart => ChainBeat.Clean;

        /// <summary>True when the press open now is shown (work order 069): every press with the rhythm on, only the last with it off (this morning's).</summary>
        bool ShowsThisPress => _chain.NextIsLast || Kit.ChainRhythm;

        /// <summary>The running chain's next press: its window and clean part (work order 069, <see cref="RecastRhythm.Press(KitValues, float, bool, int, int, float)"/>); the default with no chain.</summary>
        RecastBeat ChainBeat =>
            _chain.Slot >= 0 && _chain.Slot < Skills.Count && Skills[_chain.Slot] != null
                ? RecastRhythm.Press(Kit, Skills[_chain.Slot].RecastWindow, _chain.OpenedWithMove, _chain.Shots + 1, _chain.Count, WideWindowNow) : default;

        // Work order 069: what work order 068's combo bar reads: the whole chain on one time line. They read only.
        /// <summary>
        /// The chain running on one time line (<see cref="Roguelike.Rules.RecastLine"/>): for each press 2 to
        /// <see cref="Roguelike.Rules.RecastLine.Count"/> its window and clean part from the shot before it (<see cref="Roguelike.Rules.RecastLine.Beat"/>, as
        /// judged: widened, primed), which press is next (<see cref="Roguelike.Rules.RecastLine.Next"/>), how long since the shot before it went
        /// off (<see cref="Roguelike.Rules.RecastLine.SinceShotBefore"/>), and how each press made fell (<see cref="Roguelike.Rules.RecastLine.Timing"/>). The
        /// default (<see cref="Roguelike.Rules.RecastLine.Any"/> false) with no chain running. Makes nothing.
        /// </summary>
        public RecastLine RecastLine =>
            _chain.Slot >= 0 && _chain.Slot < Skills.Count ? RecastLine.Of(_chain, Skills[_chain.Slot], Kit, WideWindowNow, _chainPrimed, Time.time) : default;

        /// <summary>The line of this slot's chain with none running (work order 069): every press's window and clean part as she has the skill now.</summary>
        public RecastLine RecastLineOf(int slot) =>
            slot >= 0 && slot < Skills.Count ? RecastLine.Of(Skills[slot], Kit, WideWindowNow) : default;

        /// <summary>How press <paramref name="press"/> (2 on) of the latest chain fell (work order 069): kept after the chain ended, until the next one starts.</summary>
        public RecastTiming RecastTimingOf(int press) => _chain.TimingOf(press);
        /// <summary>Seconds after the window opened that its clean part begins, and ends; both 0 when the next press cannot be clean.</summary>
        public float RecastCleanFrom => RecastCleanPart.From;
        public float RecastCleanTo => RecastCleanPart.To;
        /// <summary>Seconds since the press of the chain's current shot (or its opening move), on game time; -1 with no chain.</summary>
        public float RecastSincePress => _chain.Slot >= 0 ? Time.time - _chainPressAt : -1f;
        /// <summary>Seconds from that press to the window opening: the startup of the shot, or of Backstep's hop; 0 with no chain.</summary>
        public float RecastLead => _chain.Slot >= 0 && _chain.Slot < Skills.Count && Skills[_chain.Slot] != null ? Mathf.Max(0f, StartupOf(Skills[_chain.Slot], _chain.Shots > 1)) : 0f;

        // Work order 064: what order 063's skill strip reads to show an open recast window on the skill's own box. They read only.
        /// <summary>True while the recast window of this slot's chain is open: the next press of its key fires its next shot.</summary>
        public bool RecastWindowOpenFor(int slot) => slot >= 0 && _chain.Slot == slot && _chain.IsWindowOpen(Time.time);
        /// <summary>0 to 1: how much of the open recast window has gone by (1 less <see cref="RecastWindowLeft"/>); 0 when none is open.</summary>
        public float RecastWindowProgress => IsRecastWindowOpen ? Mathf.Clamp01(1f - _chain.WindowLeft(Time.time)) : 0f;
        /// <summary>Where the open window's clean part begins and ends, as shares of the window (0 to 1); both 0 when the next press cannot be clean.</summary>
        public float RecastCleanFromShare => _chain.Window > 0f ? Mathf.Clamp01(RecastCleanFrom / _chain.Window) : 0f;
        public float RecastCleanToShare => _chain.Window > 0f ? Mathf.Clamp01(RecastCleanTo / _chain.Window) : 0f;

        /// <summary>
        /// A use's startup (work order 064): the skill's, but the shot after an opening move (Backstep's second press) takes the
        /// kit's <see cref="KitValues.OpeningMoveShotStartup"/> when that is 0 or more, so the shot is felt at the press.
        /// </summary>
        float StartupOf(SkillDefinition skill, bool shotAfterOpeningMove) =>
            skill != null && shotAfterOpeningMove && skill.OpeningMove && Kit.OpeningMoveShotStartup >= 0f ? Kit.OpeningMoveShotStartup : skill != null ? skill.Startup : 0f;
        float _chainPressAt;
        /// <summary>Presses of the recast chain running, or 0 (work order 025: five for Triple Tap in Steady Hand's window).</summary>
        public int RecastCount => _chain.Slot >= 0 ? _chain.Count : 0;
        // Work order 037 (Kristian's session of 2026-11-20): what the signs of the clean press read (PlayerSigns). They
        // read only; nothing here changes when a press is clean.
        /// <summary>The recast window's length in seconds, of the chain running; 0 with none.</summary>
        public float RecastWindowSeconds => _chain.Window;
        /// <summary>Seconds since the window for the next press opened, past its length once it has run out; -1 before it opens or with no chain.</summary>
        public float RecastSinceOpened => _chain.SinceOpened(Time.time);
        /// <summary>
        /// True when a press of the chain's key now would be clean: judged exactly as that press itself would be. Stunned,
        /// pulled or down in a quick duck a press waits and is judged as one that waited (the third review of 2026-11-20,
        /// work order 047), so the signs and the tick follow that. Work order 069: for every press of the chain, the one open now:
        /// the last press clean (a clean tap or spend), a press before it in its clean part (in rhythm, toward a full clean
        /// chain), exactly as <see cref="Recast"/> records it (<see cref="RecastTiming.Clean"/>); before, only the last press.
        /// </summary>
        public bool RecastPressWouldBeClean =>
            _chain.Slot >= 0 && _chain.Slot < Skills.Count && !_chain.Expired(Time.time)
            && ShowsThisPress && TimingOfRecast(_chain.Slot, Time.time, _held.IsHeld || _duck.IsDown) == RecastTiming.Clean;

        /// <summary>
        /// How a press of this slot's key now falls in the running chain (work order 069): the ONE judgement of a press's timing
        /// that the record of the press (<see cref="Recast"/>) and what the interface is told (<see cref="RecastPressWouldBeClean"/>)
        /// both read. The last press: clean when <see cref="JudgeRecast"/> says so; a press before it: clean in its clean part,
        /// never one that waited. <paramref name="buffered"/>: the press waited from before the window opened.
        /// </summary>
        RecastTiming TimingOfRecast(int slot, float now, bool buffered)
        {
            if (slot < 0 || slot >= Skills.Count || _chain.Slot != slot) return RecastTiming.None;
            bool hasClean = RecastChain.HasCleanPress(Skills[slot]);
            bool clean = _chain.NextIsLast
                ? JudgeRecast(slot, now, buffered) == RecastPress.Clean
                : hasClean && _chain.InCleanPart(slot, now, ChainCleanPart, buffered);
            return RecastRhythm.TimingOf(_chain.SinceOpened(now), ChainCleanPart, clean, buffered, hasClean);
        }

        /// <summary>
        /// What a press of this slot's key is to the recast chain now (decision 0005), its clean part as the kit places it
        /// (work order 040). A press that waited in the buffer from before the window opened (<paramref name="buffered"/>) is
        /// a shot, never clean. A primed chain's press of its last shot is clean wherever in the window it falls, a buffered
        /// one too, as before (Hair Trigger, work order 025: its timing counts as right).
        /// </summary>
        RecastPress JudgeRecast(int slot, float now, bool buffered = false)
        {
            bool hasClean = RecastChain.HasCleanPress(Skills[slot]);
            RecastPress judged = _chain.Judge(slot, now, ChainCleanPart, hasClean, buffered); // work order 064: after a hop, its own; 067: widened
            return _chainPrimed && _chain.Slot == slot ? FocusModeRules.Primed(judged, _chain.NextIsLast, hasClean) : judged;
        }

        /// <summary>Reports what became of a skill press, and returns it.</summary>
        IntentResult SkillPressed(int slot, IntentResult result, float cooldownLeft)
        {
            Report(new PlayerReport { Kind = PlayerReportKind.SkillPressed, Slot = slot, Result = result, CooldownLeft = cooldownLeft });
            return result;
        }

        /// <summary>
        /// The next shot of a recast chain: the shot before it is over (it went off; its recovery, if it is still in
        /// it, is cut), and this one starts at once with the skill's startup. A clean tap gains its focus here.
        /// </summary>
        void Recast(int slot, Vector3 point, bool clean, bool buffered)
        {
            float now = Time.time;
            // Work order 069: how the press fell, by its own window's clean part (the one judgement the interface reads too).
            // Work order 040: a press after the clean part, in the window, was late (the word says so for the last).
            float since = _chain.SinceOpened(now);
            RecastTiming timing = clean ? RecastTiming.Clean : TimingOfRecast(slot, now, buffered);
            bool late = timing == RecastTiming.Late;
            // Work order 064: whether this press fell in its window's clean part, the last press or not (a full clean chain).
            bool inRhythm = timing == RecastTiming.Clean; // work order 067: the one clean part
            _chain.Recast(inRhythm, timing);
            if (!_chain.IsComplete) _chain.NextWindow(ChainBeat.Window); // work order 069: the next press's own window
            bool fullClean = MarkRules.IsFullCleanChain(Skills[slot], clean, _chain.Shots, _chain.AllInRhythm);
            float focus = clean ? GainFocus(Identity.FocusPerCleanTap) : 0f;
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.RecastPressed, Slot = slot, Shot = _chain.Shots, Clean = clean, Focus = focus, Late = late,
                Timing = timing, Seconds = since, // work order 069
            });
            if (_actionKind == ActionKind.Skill)
            {
                ReportSkillEnded(SkillEnding.RanOut, now);
                _cast.Cancel();
                EndAction();
            }
            StartSkill(slot, point, recast: true);
            _castClean = clean;
            _castFullClean = fullClean;
            if (fullClean) FullCleanChains++;
        }

        bool _castFullClean; // work order 064: the shot in use is the clean last tap of a full clean chain
        /// <summary>Full clean chains she finished (work order 064): every recast press in its clean part, the last clean.</summary>
        public int FullCleanChains { get; private set; }
        /// <summary>The weight of her latest shot (work order 064): for its hit stop, and for tests.</summary>
        public ShotWeight LastShotWeight { get; private set; }

        /// <summary>True when a press of this slot's skill is not held back by another skill's free recovery (work order 064: Backstep's hop is the escape).</summary>
        bool CutsRecovery(int slot) => Kit.OpeningMoveCutsRecovery && slot >= 0 && slot < Skills.Count && Skills[slot] != null && Skills[slot].OpeningMove;

        /// <summary>Ends a recast chain whose window has run out.</summary>
        void ExpireChain(float now)
        {
            RunChainOnThroughHitStop();
            if (!_chain.Expired(now)) return;
            _chain.MarkMissed(); // work order 069: the line of the chain shows the press missed
            EndChain();
        }

        // Work order 038 (Kristian's answer of 2026-11-20: the recast window runs on real time). A hit stop froze the
        // window's game-time clock, so the clean part moved from one use to the next. Once a frame, before anything in it
        // judges a press (every press, the buffer's and the frame's own update pass here first), the open window runs on
        // by the real seconds a hit stop froze (RecastClock); a pause, a choice or a screen still stops it.
        // Off (KitValues.RecastOnRealTime), nothing runs on and the window is as it was.
        int _chainRanOnFrame = -1;

        void RunChainOnThroughHitStop()
        {
            if (_chainRanOnFrame == Time.frameCount) return;
            _chainRanOnFrame = Time.frameCount;
            _chain.RunOn(RecastClock.HitStopSeconds(Kit.RecastOnRealTime, Time.deltaTime, Time.unscaledDeltaTime, HitStop.HoldsThisFrame,
                                                    PlayerInputDriver.HeldByChoice || PlayerInputDriver.HeldByScreen, HitStop.ScaleFound));
        }

        /// <summary>Ends the recast chain, if one runs, and reports how many shots it fired.</summary>
        void EndChain()
        {
            if (_chain.Slot < 0) return;
            int slot = _chain.Slot, count = _chain.Count;
            int shots = _chain.End();
            Report(new PlayerReport { Kind = PlayerReportKind.ChainEnded, Slot = slot, Shot = shots, Early = shots < count });
        }
    }
}
