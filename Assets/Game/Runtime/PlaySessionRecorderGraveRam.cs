using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The Grave Ram in the log (work order 149): one line a fight (<see cref="GraveRamEvents.Fight"/>), told by the Ram as its fight
    /// ends. Nothing is written a frame.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A Grave Ram fight ended: its seconds from the wake and how it ended; her damage taken by attack; the Rams begun, crashed,
        /// missed and met her, and what the crashes were on; Stopping Shot's counters; the crash dazes' seconds, those she shot in and
        /// her damage in them; her share of the fight below the standing speed; the hits the hurt guard spared; the seconds of phase 2.
        /// </summary>
        public void NoteGraveRamFight(float seconds, string ended, int ram, int stomp, int stones, int rams, int crashes, int missed, int met,
                                      int countered, string crashedOn, float dazeSeconds, float dazeShot, int dazeDamage, float slowShare, int guarded,
                                      float phaseTwo) =>
            Note(GraveRamEvents.Fight, GraveRamValues.Title, "seconds", GraveRamEvents.Seconds(seconds), "ended", ended ?? "",
                 "ram", Int(ram), "stomp", Int(stomp), "stones", Int(stones), "rams", Int(rams), "crashes", Int(crashes),
                 "missed", Int(missed), "met", Int(met), "countered", Int(countered), "crashed_on", crashedOn ?? "",
                 "daze", GraveRamEvents.Seconds(dazeSeconds), "daze_shot", GraveRamEvents.Seconds(dazeShot), "daze_damage", Int(dazeDamage),
                 "slow", GraveRamEvents.Share(slowShare), "guarded", Int(guarded), "two", GraveRamEvents.Seconds(phaseTwo));
    }
}
