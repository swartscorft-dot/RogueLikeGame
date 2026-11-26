namespace Roguelike.Rules
{
    /// <summary>
    /// Hold to fire (work order 132, 2026-11-25). A friend's note from a first run, passed on by Kristian: "Hadde vært chill med å
    /// holde LMB for auto attack". Asked whether the basic shot should go on while the button is held, Kristian ticked "Ja, hold =
    /// skyt" (the option: holding the button, she fires at the basic shot's own pace; clicking works as before; behind a switch).
    /// </summary>
    public static class HoldToFireValues
    {
        /// <summary>
        /// The order's switch: on, the shoot button held keeps firing basic shots at the shot's own pace (<see cref="HoldToFireRules"/>);
        /// off, one click is one shot as before work order 132 (holding never repeats). On by default: his tick "Ja, hold = skyt".
        /// </summary>
        public static bool On = true;
    }

    /// <summary>
    /// The rules of hold to fire (work order 132; see <see cref="HoldToFireValues"/>). No engine; the input driver asks them each frame.
    ///
    /// - While the button is held, a basic shot is asked for on exactly the frames on which a click would start one at once
    ///   (<see cref="ClickGoesThisFrame"/>): nothing else is busy, the last shot's whole shot time is over
    ///   (<see cref="ClickToShoot.MustWait"/>'s wait: what keeps clicking from raising the fire rate), and the shot going on, if
    ///   any, has gone off. So holding is exactly as fast as perfect clicking and never faster, and each shot aims at the cursor
    ///   as it is then (a shot follows the live cursor until it goes off, as a clicked one does). No click is left waiting by
    ///   holding: let go, nothing more starts.
    /// - It asks for nothing while the button must first go up after a choice or a screen (work order 026), while she sprints or
    ///   is in a jump (a click is refused there, work order 092; held, it is not even counted), while she is in a skill, a dash,
    ///   stunned or pulled, or down in a duck (a click waits there; held, the first shot comes on the first frame she is free:
    ///   at most a frame after a click that waited would have gone), nor while a skill on the same button holds or waits for its
    ///   recast on it. A press of the button is a click as before, whatever is held.
    /// - Down in a held duck a click brings her up and fires (work order 086); holding never does: she stays down until the
    ///   duck key goes up, and the held button fires from the frame she is up.
    /// </summary>
    public static class HoldToFireRules
    {
        /// <summary>
        /// Would a click now start a basic shot this very frame? Not while anything else keeps her (<paramref name="otherBusy"/>: a
        /// skill, a dash, a stun or a pull, a jump, down in a duck), not before <paramref name="nextShotAt"/> (the last shot's
        /// whole shot time), and not while a shot going on (<paramref name="shotGoing"/>) has not gone off yet
        /// (<paramref name="shotWentOff"/>): one that has, at that moment, is at the end of its cycle and gives way this frame.
        /// </summary>
        public static bool ClickGoesThisFrame(bool otherBusy, bool shotGoing, bool shotWentOff, float now, float nextShotAt) =>
            !otherBusy && now >= nextShotAt && (!shotGoing || shotWentOff);

        /// <summary>
        /// Does the held button (no press this frame) ask for a basic shot now? The switch on, the button held and not waiting to
        /// be seen up (<paramref name="waitsForUp"/>), a click now would go at once (<paramref name="clickGoesNow"/>,
        /// <see cref="ClickGoesThisFrame"/>), a click is not refused now (<paramref name="refusedNow"/>: sprinting or in a jump),
        /// and no skill on the same button holds or waits for its recast on it (<paramref name="buttonTakenBySkill"/>).
        /// </summary>
        public static bool Fires(bool on, bool held, bool waitsForUp, bool clickGoesNow, bool refusedNow, bool buttonTakenBySkill) =>
            on && held && !waitsForUp && clickGoesNow && !refusedNow && !buttonTakenBySkill;
    }
}
