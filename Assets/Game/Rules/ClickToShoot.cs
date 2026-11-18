namespace Roguelike.Rules
{
    /// <summary>
    /// Click to shoot (Kristian 2026-11-18): every basic shot answers one click, and there is no auto-attack.
    /// A click that comes while the character cannot shoot yet (a shot is still in its cycle, or a dash or a skill
    /// is going) is not lost: it waits, and fires as the next shot the moment the cycle allows. Only one click
    /// waits; a newer one takes its place, which changes nothing, because a shot aims at the live cursor when it
    /// fires. A move order, a dash or a skill drops it.
    /// </summary>
    public sealed class ClickToShoot
    {
        /// <summary>True while a click waits for its shot.</summary>
        public bool Waiting { get; private set; }

        /// <summary>A click. True: the shot starts now. False: the click waits (one at most).</summary>
        public bool Click(bool busy, float now, float nextShotAt)
        {
            if (MustWait(busy, now, nextShotAt))
            {
                Waiting = true;
                return false;
            }
            Waiting = false;
            return true;
        }

        /// <summary>Asked when the character is free: true, once, when the waiting click's shot may start now.</summary>
        public bool TakeWhenAllowed(bool busy, float now, float nextShotAt)
        {
            if (!Waiting || MustWait(busy, now, nextShotAt)) return false;
            Waiting = false;
            return true;
        }

        /// <summary>The waiting click is dropped (a move order, a dash or a skill took over).</summary>
        public void Clear() => Waiting = false;

        /// <summary>
        /// True while a click has to wait: the character is busy (a shot, a skill or a dash is going), or the last
        /// shot's whole shot time is not over. That wait is what keeps clicking from raising the fire rate.
        /// </summary>
        public static bool MustWait(bool busy, float now, float nextShotAt) => busy || now < nextShotAt;
    }
}
