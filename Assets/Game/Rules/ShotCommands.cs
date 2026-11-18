namespace Roguelike.Rules
{
    /// <summary>
    /// Click to shoot (Kristian 2026-11-18: "auto attack is not a thing any more, but click to shoot"):
    /// every basic shot answers one attack command, and an attack order is done when its own shot has gone off.
    ///
    /// A command that comes while a shot is still in its cycle is not lost: it stays as the one waiting command
    /// and is answered by the next shot, the moment the cycle allows. There is only ever one waiting command:
    /// a newer one replaces it, so ten clicks in one cycle are still one shot, at the latest target.
    /// A shot that started for an earlier command does not answer a newer one.
    /// </summary>
    public sealed class ShotCommands
    {
        int _command;       // counts attack commands
        int _shotFor = -1;  // the command the shot in progress was started for, or -1

        /// <summary>True while the latest attack command still waits for its shot to go off.</summary>
        public bool Owed { get; private set; }

        /// <summary>An attack command came. It replaces any command still waiting.</summary>
        public void Command()
        {
            _command++;
            Owed = true;
        }

        /// <summary>The waiting command is dropped (a move order, a dash or a skill took over).</summary>
        public void Clear()
        {
            Owed = false;
            _shotFor = -1;
        }

        /// <summary>A basic shot starts. It answers the waiting command if one waits and the shot was started for it.</summary>
        public void ShotStarted(bool forTheCommand) => _shotFor = forTheCommand && Owed ? _command : -1;

        /// <summary>The shot in progress was cancelled before it went off: it answered nothing.</summary>
        public void ShotCancelled() => _shotFor = -1;

        /// <summary>
        /// The shot in progress went off. True if it answered the waiting command, which is then done;
        /// false if a newer command came while it was going, which still waits for a shot of its own.
        /// </summary>
        public bool ShotWentOff()
        {
            bool done = Owed && _shotFor == _command;
            if (done) Owed = false;
            _shotFor = -1;
            return done;
        }

        /// <summary>
        /// True while a command has to wait for the next shot: a basic shot is going, or the last one's shot
        /// time is not over. That wait is what keeps clicking from raising the fire rate.
        /// </summary>
        public static bool MustWait(bool shotGoing, float now, float nextShotAt) => shotGoing || now < nextShotAt;
    }

    /// <summary>At most one waiting item, with no expiry: a newer one replaces it.</summary>
    public sealed class PendingOne<T> where T : class
    {
        T _item;

        public bool HasItem => _item != null;
        public T Item => _item;

        public void Store(T item) => _item = item;
        public void Clear() => _item = null;

        /// <summary>Takes the item, emptying the slot. Null if there was none.</summary>
        public T Take()
        {
            T item = _item;
            _item = null;
            return item;
        }
    }
}
