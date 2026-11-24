using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The music (work order 116) in the log: one line a change of its state (<see cref="MusicEvents.Change"/>), as it lands: from,
    /// to, and the bar of the music's clock it landed on. The summary reads them into one line (SessionSummaryMusic.cs). Nothing
    /// is written a frame.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>The music changed state: from, to, and the bar it landed on (from 0, the music's start).</summary>
        public void NoteMusic(MusicState from, MusicState to, long bar) =>
            Note(MusicEvents.Change, MusicRules.Word(to), "from", MusicRules.Word(from), "to", MusicRules.Word(to),
                 "bar", bar.ToString(CultureInfo.InvariantCulture));
    }
}
