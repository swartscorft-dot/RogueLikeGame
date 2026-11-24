using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The boss as a finale (work order 106) in the log: one line a boss fight (<see cref="BossFightEvents"/>), told by the boss as
    /// the fight ends, only with the finale on; and one line when Knight's Resolve keeps her alive. Nothing is written a frame.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A boss fight ended: the seconds in each phase, whether it broke, its Ground Slams, landed and avoided, and how it ended.</summary>
        public void NoteBossFight(float one, float two, bool broke, int slams, int landed, int avoided, string ended) =>
            Note(BossFightEvents.Fight, null, "one", BossFightEvents.Seconds(one), "two", BossFightEvents.Seconds(two), "broke", broke ? "1" : "0",
                 "slams", Int(slams), "landed", Int(landed), "avoided", Int(avoided), "ended", ended ?? "");

        /// <summary>Knight's Resolve kept her alive (work order 106): the blow it cut, and the enemies it dazed.</summary>
        public void NoteKnightsResolve(int blow, int dazed) =>
            Note(KnightsResolveEvents.Spent, KnightsResolve.Name, "blow", Int(blow), "dazed", Int(dazed));
    }
}
