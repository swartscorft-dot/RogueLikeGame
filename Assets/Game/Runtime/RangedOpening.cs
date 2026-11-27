using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// When a shooter or a caster of a room's fight may begin a draw or a cast (work order 124, parts 1 and 2;
    /// <see cref="RangedOpeningRules"/> is the rule): each one holds one of these and asks it as the last thing its <c>MayAttack</c>
    /// asks. It reads the values its room's fight was given (<see cref="RoomFight.Ranged"/>: null with
    /// <see cref="LevelValues.RangedAroundTheRoom"/> off, in the practice room, and with no fight, when it lets everything through as
    /// before), notes once a waking whether it was within its reach of her then (part 2), and asks the real camera whether it stands on
    /// his screen (part 1: <see cref="OnScreen"/>, the camera's place, axes and angle handed to <see cref="ScreenGround"/> as plain numbers,
    /// so it is right at any camera angle). Nothing is allocated: a few numbers a frame.
    ///
    /// For the recorder (part 10): <see cref="Note"/> adds to each arrow's or orb's line whether its shooter was on screen at the release,
    /// and whether it was when it began the draw.
    /// </summary>
    public sealed class RangedOpening
    {
        RoomFight _fight;
        int _starts = -1;
        bool _inReachAtWaking;
        bool _drewOnScreen = true;

        /// <summary>Was it on his screen (no margin) the last time it was let begin? True before the first.</summary>
        public bool DrewOnScreen => _drewOnScreen;

        /// <summary>May <paramref name="enemy"/>, <paramref name="distance"/> from her, with a reach of <paramref name="reach"/>, begin a draw or a cast now?</summary>
        public bool Allows(Enemy enemy, float distance, float reach) => Asks(enemy, distance, reach, true);

        /// <summary>
        /// Work order 140 (<see cref="RangedOpeningRules.WarderToo"/>, <see cref="RangedOpeningRules.LancerToo"/>, handed in as
        /// <paramref name="on"/>): may the warder begin its bolt, or the lancer the wind-up of its charge, now? Part 1 alone, his screen,
        /// asked as <see cref="Allows"/> asks it; no grace at the waking. True with its switch off, as before.
        /// </summary>
        public bool AllowsSeen(Enemy enemy, bool on) => !on || Asks(enemy, 0f, 0f, false);

        bool Asks(Enemy enemy, float distance, float reach, bool grace)
        {
            RoomFight f = enemy != null ? enemy.Fight : null;
            RangedAroundValues r = f?.Ranged;
            if (r == null || !f.On) return true;
            Vector3 at = enemy.transform.position;
            if (!grace)
            {
                bool seen = RangedOpeningRules.MayBeginSeen(r, !r.OnScreenOnly || OnScreen(at, r.ScreenMargin));
                if (seen) _drewOnScreen = OnScreen(at, 0f);
                return seen;
            }
            if (f != _fight || f.Starts != _starts)
            {
                // The first ask of this waking: was it within its reach of her as the room woke?
                _fight = f;
                _starts = f.Starts;
                _inReachAtWaking = distance <= reach;
            }
            bool onScreen = !r.OnScreenOnly || OnScreen(at, r.ScreenMargin);
            bool may = RangedOpeningRules.MayBegin(r, onScreen, Mathf.Max(f.StartedAt, _arrivedAt), Time.time, _inReachAtWaking); // work order 136: or from its arrival
            if (may) _drewOnScreen = OnScreen(at, 0f);
            return may;
        }

        /// <summary>
        /// Is the point at least <paramref name="margin"/> inside what the game's camera (<see cref="Camera.main"/>) shows of the ground
        /// at its own height? True with no camera (nothing to ask).
        /// </summary>
        public static bool OnScreen(Vector3 point, float margin)
        {
            Camera c = Camera.main;
            if (c == null) return true;
            Transform t = c.transform;
            Vector3 p = t.position, f = t.forward, u = t.up, r = t.right;
            GroundQuad q = ScreenGround.FromCamera(p.x, p.y, p.z, f.x, f.y, f.z, u.x, u.y, u.z, r.x, r.y, r.z, c.fieldOfView, c.aspect, point.y);
            return ScreenGround.Shows(q, point.x, point.z, margin);
        }

        /// <summary>Part 10: on an arrow's or an orb's line (<see cref="SessionEventTypes.EnemyLoose"/>) of a shooter or caster of a fight with the rule, whether it was on screen at the release and as it drew.</summary>
        public static void Note(SessionEvent e, Enemy enemy)
        {
            if (e == null || enemy == null || enemy.Fight?.Ranged == null || !enemy.Fight.Ranged.TellsTheLog) return;
            RangedOpening o = enemy is EnemyShooter s ? s.Opening : enemy is EnemyCaster c ? c.Opening
                : enemy is EnemyWarder w && RangedOpeningRules.WarderToo ? w.Opening : null; // work order 140: the warder's bolt too
            if (o == null) return;
            e.AddExtra("on_screen", OnScreen(enemy.transform.position, 0f) ? "yes" : "no");
            e.AddExtra("drew_on_screen", o.DrewOnScreen ? "yes" : "no");
        }

        /// <summary>The grace's state for tests: within its reach at the waking it last saw.</summary>
        public bool InReachAtWaking => _inReachAtWaking;

        float _arrivedAt = float.NegativeInfinity;

        /// <summary>
        /// Work order 136 (two waves): it arrived in its wave at <paramref name="at"/> (game time) in <paramref name="fight"/>, already on:
        /// its second's grace runs from its arrival, whatever its distance from her ("a 1 s grace from their arrival before a first shot").
        /// </summary>
        public void Arrived(RoomFight fight, float at)
        {
            _arrivedAt = at;
            if (fight == null) return;
            _fight = fight;
            _starts = fight.Starts;
            _inReachAtWaking = true;
        }

        /// <summary>The game time it arrived in its wave (work order 136); minus infinity for one that stood in the room from the start.</summary>
        public float ArrivedAt => _arrivedAt;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "starts {0} in reach {1}", _starts, _inReachAtWaking);
    }
}
