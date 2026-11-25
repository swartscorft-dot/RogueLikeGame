using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// What work order 132 asks of <see cref="PlayerCharacter"/> (2026-11-25): for hold to fire, whether a click now would start a
    /// basic shot this very frame (<see cref="HoldToFireRules"/>); and the quick duck's reach in use, a little larger with the
    /// duck's key sign on (<see cref="DuckKeySignRules.Reach(float)"/>). A file of its own, so the shared files change by a line.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// True when a click now would start a basic shot in this frame (work order 132, <see cref="HoldToFireRules.ClickGoesThisFrame"/>):
        /// no skill, dash, stun or pull, jump or duck keeps her, the last shot's whole shot time is over, and a shot going on has
        /// gone off (it gives way in this frame's Update, and a click given before it waits and is taken there). The input driver
        /// reads it before her Update. Nothing is allocated, nothing changes.
        /// </summary>
        public bool ClickGoesThisFrame
        {
            get
            {
                bool otherBusy = _dashing || _held.IsHeld || _duck.IsDown || _jumping || _actionKind == ActionKind.Skill;
                bool shotGoing = _actionKind == ActionKind.BasicShot;
                return HoldToFireRules.ClickGoesThisFrame(otherBusy, shotGoing, shotGoing && _action.HitResolved, Time.time, _nextShotAt);
            }
        }

        /// <summary>The quick duck's reach in use now, in metres from her middle to a low prop's edge (work order 132).</summary>
        public float DuckReachInUse => DuckReachNow(Feel);

        /// <summary>The feel's <see cref="FeelValues.DuckReach"/>, times the key sign's factor while its switch is on (work order 132).</summary>
        static float DuckReachNow(FeelValues feel) => DuckKeySignRules.Reach(feel.DuckReach);
    }
}
