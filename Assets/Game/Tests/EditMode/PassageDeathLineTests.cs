using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 081 (noted by work order 077): a run that ends in a passage says so in the session summary. The recorder
    /// writes a passage's end with no room number (0) and the kind "passage"; a room's end reads as before.
    /// </summary>
    public class PassageDeathLineTests
    {
        static SessionEvent Ev(double t, string type, string name = null, string result = null, params string[] extras)
        {
            var e = new SessionEvent(t, type, name) { Result = result };
            for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
            return e;
        }

        static List<SessionEvent> Run(string room, string kind) => new List<SessionEvent>
        {
            Ev(0, SessionEventTypes.SessionStart),
            Ev(1, SessionEventTypes.RunStart, "ABCDEFGH"),
            Ev(1, SessionEventTypes.FloorEnter, "22222222", null, "floor", "1"),
            Ev(61, SessionEventTypes.FloorFinish, null, null, "floor", "1", "seconds", "60", "health", "80"),
            Ev(61, SessionEventTypes.FloorEnter, "33333333", null, "floor", "2"),
            Ev(100, SessionEventTypes.RunEnd, null, "death", "floor", "2", "room", room, "kind", kind, "seconds", "99"),
            Ev(120, SessionEventTypes.SessionEnd),
        };

        [Test]
        public void ADeathInAPassage_ReadsInAPassage_ADeathInARoom_AsBefore()
        {
            SessionStats passage = SessionSummary.Compute(Run("0", "passage"));
            Assert.That(passage.Runs[0].Ended, Is.EqualTo("death on floor 2, in a passage after 1:39.0"));
            StringAssert.Contains("ended: death on floor 2, in a passage after 1:39.0", SessionSummary.Format(passage));

            SessionStats room = SessionSummary.Compute(Run("4", "combat"));
            Assert.That(room.Runs[0].Ended, Is.EqualTo("death on floor 2, room 4 (combat) after 1:39.0"), "a room's, as before");
            SessionStats nowhere = SessionSummary.Compute(Run("0", ""));
            Assert.That(nowhere.Runs[0].Ended, Is.EqualTo("death on floor 2 after 1:39.0"), "outside every room and passage, as before");
        }
    }
}
