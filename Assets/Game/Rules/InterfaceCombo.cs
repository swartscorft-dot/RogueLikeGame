using System;

namespace Roguelike.Rules
{
    /// <summary>One press of a recast chain as the combo bar draws it ahead of time (work order 068).</summary>
    public struct ComboPress
    {
        /// <summary>Seconds from the press before to this press's window opening: the startup of the shot before (or Backstep's hop).</summary>
        public float Lead;
        /// <summary>The press's window, in seconds after the shot before it went off (work order 069's <see cref="RecastBeat.Window"/>).</summary>
        public float Window;
        /// <summary>Its clean part, in seconds after the shot before it went off, as judged (<see cref="RecastBeat.Clean"/>).</summary>
        public CleanPart Clean;
        /// <summary>The chain's last press (its clean press a clean tap); before it, the clean part is the rhythm a full clean chain asks for.</summary>
        public bool Last;
    }

    /// <summary>How a zone of the combo bar shows (work order 068), from how its press fell (work order 069's <see cref="RecastTiming"/>).</summary>
    public enum ComboZoneState
    {
        /// <summary>Still to come: drawn ahead, dim.</summary>
        Coming,
        /// <summary>Its window open now: lit.</summary>
        Open,
        /// <summary>Pressed in its clean part: flashes, then stays bright.</summary>
        Hit,
        /// <summary>Pressed in its window before its clean part (or it waited from before the window opened): EARLY.</summary>
        Early,
        /// <summary>Pressed in its window after its clean part: LATE.</summary>
        Late,
        /// <summary>A press of a skill with nothing to judge by.</summary>
        Plain,
        /// <summary>Its window ran out unpressed, or the chain ended before it: grey.</summary>
        Missed,
    }

    /// <summary>
    /// The combo bar's line (work order 068, item 3: "de første er veldig utydlig når skal skje. må tydliggjøre den combo
    /// baren"): every press of a recast chain on one line from its first press, each press's window and clean part drawn
    /// ahead of time. Every window and clean part is work order 069's (<see cref="RecastLine.Beat"/>, from
    /// <see cref="RecastRhythm"/>: the one function the judging reads too); this only places them on one line: a window not
    /// yet open after the press before it (pressed, or else aimed at the middle of its clean part) by the startup of the shot
    /// between (<see cref="Lead"/>), and says how a press fell (<see cref="Of(RecastTiming)"/>).
    /// </summary>
    public static class ComboLine
    {
        /// <summary>The most presses a line shows (Triple Tap with Fourth Tap and Steady Hand has six).</summary>
        public const int Most = 8;

        /// <summary>
        /// The seconds from press <paramref name="press"/> - 1 to press <paramref name="press"/>'s window opening: the startup of
        /// the shot between, as <c>PlayerCharacter.RecastLead</c> reads it (the skill's for the first press; after an opening
        /// move, the kit's shot startup for the presses after).
        /// </summary>
        public static float Lead(SkillDefinition skill, KitValues kit, int press)
        {
            if (skill == null) return 0f;
            kit = kit ?? new KitValues();
            bool afterShot = press > 2;
            return Math.Max(0f, afterShot && skill.OpeningMove && kit.OpeningMoveShotStartup >= 0f ? kit.OpeningMoveShotStartup : skill.Startup);
        }

        /// <summary>A press on the line from its beat (work order 069) and its lead.</summary>
        public static ComboPress Of(RecastBeat beat, float lead) =>
            beat.Any ? new ComboPress { Lead = Math.Max(0f, lead), Window = beat.Window, Clean = beat.Clean, Last = beat.Last } : default;

        /// <summary>
        /// Where a press is aimed on the line: the middle of its clean part, or the middle of its window when it has none. The
        /// presses after an open one are drawn from here: as if each were pressed at its aim. Seconds after its window opens.
        /// </summary>
        public static float Aim(float window, CleanPart clean) => clean.Any ? (clean.From + clean.To) * 0.5f : window * 0.5f;

        /// <summary>
        /// The line's place of every press's window, in seconds after the chain's first press. <paramref name="opened"/> holds
        /// for each press (index 0 is the first press, which has no window) when its window opened if it has, else NaN;
        /// <paramref name="pressed"/> when it was pressed if it was, else NaN. A window not yet open is placed after the press
        /// before it (pressed, or else aimed) by its lead. Writes each window's start into <paramref name="start"/> and returns
        /// the line's end: the last window's end.
        /// </summary>
        public static float Lay(ComboPress[] presses, int count, float[] opened, float[] pressed, float[] start)
        {
            count = Math.Min(count, Math.Min(presses.Length, Math.Min(opened.Length, Math.Min(pressed.Length, start.Length))));
            float press = 0f, end = 0f;
            for (int i = 1; i < count; i++)
            {
                ComboPress p = presses[i];
                float s = !float.IsNaN(opened[i]) ? opened[i] : press + p.Lead;
                start[i] = s;
                end = Math.Max(end, s + p.Window);
                press = !float.IsNaN(pressed[i]) ? pressed[i] : s + Aim(p.Window, p.Clean);
            }
            return end;
        }

        /// <summary>A time on the line as a share of the bar (0 to 1) for a line <paramref name="length"/> seconds long.</summary>
        public static float Share(float seconds, float length) => length <= 0f ? 0f : Math.Max(0f, Math.Min(1f, seconds / length));

        /// <summary>A zone's state from how its press fell (work order 069): <see cref="RecastTiming.None"/> leaves it as it is (null).</summary>
        public static ComboZoneState? Of(RecastTiming timing)
        {
            switch (timing)
            {
                case RecastTiming.Clean: return ComboZoneState.Hit;
                case RecastTiming.Early:
                case RecastTiming.Waited: return ComboZoneState.Early;
                case RecastTiming.Late: return ComboZoneState.Late;
                case RecastTiming.Plain: return ComboZoneState.Plain;
                case RecastTiming.Missed: return ComboZoneState.Missed;
                default: return null;
            }
        }
    }
}
