using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Work order 143 in the log (<see cref="PrecisionLog"/>): precision's extras on each basic shot's shot_fired, and a
    /// counter_strafe line for every move key pressed against her motion above 35 %. With precision Off, nothing is added.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>One of her reports, if it is precision's own (a counter-strafe): logged, and true; anything else false.</summary>
        bool RecordPrecision(PlayerReport r, double t)
        {
            if (r.Kind != PlayerReportKind.CounterStrafed) return false;
            var e = new SessionEvent(t, PrecisionLog.CounterStrafe, SessionWords.BasicShot);
            e.AddExtra(PrecisionLog.Share, Text(r.Share));
            Add(e);
            return true;
        }

        /// <summary>A basic shot's precision, on its shot_fired (its precision was worked out as it went off).</summary>
        void AddPrecision(SessionEvent e)
        {
            if (_player == null) return;
            PrecisionShot s = _player.LastPrecisionShot;
            if (s.Mode == PrecisionMode.Off) return;
            e.AddExtra(PrecisionLog.Mode, PrecisionLines.WordsOf(s.Mode).ToLowerInvariant());
            e.AddExtra(PrecisionLog.ShareAtPress, Text(s.ShareAtPress));
            e.AddExtra(PrecisionLog.ShareLowest, Text(s.ShareLowest));
            if (s.Mode == PrecisionMode.Meter) e.AddExtra(PrecisionLog.Momentum, Text(s.Momentum));
            if (s.Mode == PrecisionMode.Ghost) e.AddExtra(PrecisionLog.HiddenSpeed, Text(s.HiddenSpeed));
            e.AddExtra(PrecisionLog.Cone, Text(s.Cone));
            e.AddExtra(PrecisionLog.Deflection, Text(s.Deflection));
            if (!float.IsNaN(s.NearestDistance)) e.AddExtra(PrecisionLog.Nearest, Text(s.NearestDistance));
            e.AddExtra(PrecisionLog.StraightHit, s.StraightHit ? "true" : "false");
            e.AddExtra(PrecisionLog.ConeMiss, s.ConeMiss ? "true" : "false");
            if (!float.IsNaN(s.SinceCounterStrafe)) e.AddExtra(PrecisionLog.SinceCounterStrafe, s.SinceCounterStrafe.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
