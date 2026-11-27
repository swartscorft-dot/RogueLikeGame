namespace Roguelike
{
    /// <summary>
    /// The rehearsal's sessions (work order 148): a rehearsal is written as a session of its own, with "rehearsal":"true" in its
    /// start line, so his sessions can be told apart. As one begins, the session going on ends (its summary written as at Play's
    /// end) and a new one begins in the same folder, the same way it began (by itself or on purpose); "Again" goes on in it;
    /// leaving the rehearsals ends it and an ordinary session begins. With no recorder running (a test that started none, the
    /// recorder failed) nothing happens.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>True for the session's start line written while a rehearsal's session begins.</summary>
        static bool s_rehearsalStart;

        /// <summary>True when this session is a rehearsal's: its start line says "rehearsal":"true".</summary>
        public bool IsRehearsalSession { get; private set; }

        /// <summary>The word in the start line (work order 148).</summary>
        public const string RehearsalField = "rehearsal";

        /// <summary>For the start line (PlaySessionRecorderFile.cs): is it a rehearsal's?</summary>
        bool WritesRehearsal => s_rehearsalStart || IsRehearsalSession;

        /// <summary>
        /// The session going on becomes a rehearsal's (<paramref name="rehearsal"/> true) or an ordinary one: when it is not that
        /// already, it ends and a new one begins. Returns the recorder running after, or null with none.
        /// </summary>
        public static PlaySessionRecorder SwitchSession(bool rehearsal)
        {
            PlaySessionRecorder now = Current;
            if (now == null || !now.IsRecording || now.IsRehearsalSession == rehearsal) return now;
            string folder = now.Folder;
            bool byItself = now.StartedByItself;
            now.StopRecording();
            s_rehearsalStart = rehearsal;
            try
            {
                PlaySessionRecorder next = Begin(folder, byItself);
                next.IsRehearsalSession = rehearsal;
                return next;
            }
            finally
            {
                s_rehearsalStart = false;
            }
        }
    }
}
