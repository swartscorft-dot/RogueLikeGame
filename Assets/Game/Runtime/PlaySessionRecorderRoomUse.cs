using System;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// How much of the room she uses (work order 097), as part of the play session recorder: during a room's fight, and only then,
    /// her place in the room's own metres from the door she came in by (<see cref="RoomUseEvents"/>). The floor tells it when a fight
    /// begins (with the room as she meets it) and when it ends; between, once a frame, her place is written when the sample rule says
    /// (<see cref="RoomUseRules.ShouldWrite"/>: a quarter of a second since the last and more than half a metre from it), and an enemy
    /// of the fight that dies is written with its place. A frame that writes no line allocates nothing: a struct turned, a few numbers
    /// compared. Outside a fight nothing is written.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        bool _useOn;
        RoomFrame _useFrame;      // the room's own metres, its origin in the scene's ground (x, z)
        int _useOrder;            // the fight's room by its place in the floor
        double _useT, _useD, _useS; // the last place written: the session's seconds (unrounded), depth, side

        /// <summary>True while a room's fight is being followed (work order 097).</summary>
        public bool FollowsAFight => _useOn;
        /// <summary>Her places written in the session so far (work order 097). For tests.</summary>
        public int FightPlacesWritten { get; private set; }

        static string Metre(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>Seconds to the millisecond as the log holds them (as <see cref="Now"/> does), for a time given.</summary>
        static double Millis(double t) => double.Parse(t.ToString("0.000", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        /// <summary>
        /// A room's fight began (work order 097): the room as she meets it, in <paramref name="frame"/> (its origin in the scene's ground),
        /// and her first place. A fight still followed ends first ("another fight").
        /// </summary>
        public void NoteFightRoom(int floor, int room, int order, string kind, string template, RoomSpace space, RoomFrame frame)
        {
            if (!_recording || _writer == null || space == null) return;
            try
            {
                if (_useOn) NoteFightEnd("another fight");
                double t = Now();
                var e = new SessionEvent(t, RoomUseEvents.Room);
                e.AddExtra("floor", Int(floor));
                e.AddExtra("room", Int(room));
                e.AddExtra("order", Int(order));
                e.AddExtra("kind", kind ?? "");
                e.AddExtra("template", template ?? "");
                space.WriteTo(e);
                Add(e);
                _useOn = true;
                _useFrame = frame;
                _useOrder = order;
                if (_player == null) return;
                Vector3 her = _player.Position;
                _useFrame.ToFrame(her.x, her.z, out double d, out double s);
                WritePlace(t, (double)(Time.time - _startTime), d, s);
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        /// <summary>Once a frame from <see cref="Update"/>: her place, if the sample rule says so.</summary>
        void FollowFight()
        {
            if (_useOn) FollowFight(_player.Position, Time.time - _startTime);
        }

        /// <summary>
        /// Her place at <paramref name="seconds"/> of the session (unrounded) while a fight is followed: written when the sample rule
        /// says so, and true then. Nothing is allocated when it writes nothing. Public for tests, which give the time themselves.
        /// </summary>
        public bool FollowFight(Vector3 her, double seconds)
        {
            if (!_useOn || !_recording) return false;
            _useFrame.ToFrame(her.x, her.z, out double d, out double s);
            if (!RoomUseRules.ShouldWrite(_useT, _useD, _useS, seconds, d, s)) return false;
            try
            {
                WritePlace(Millis(seconds), seconds, d, s);
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
            return true;
        }

        void WritePlace(double logT, double seconds, double d, double s)
        {
            var e = new SessionEvent(logT, RoomUseEvents.Place);
            e.AddExtra("d", Metre(d));
            e.AddExtra("s", Metre(s));
            Add(e);
            _useT = seconds;
            _useD = d;
            _useS = s;
            FightPlacesWritten++;
        }

        /// <summary>An enemy died (its report's id): with a fight followed and the enemy of its room's fight (or of none), its place.</summary>
        void NoteFightDeath(int id)
        {
            if (!_useOn) return;
            Enemy enemy = null;
            for (int i = 0; i < _enemies.Count; i++) // no LINQ: a death is a moment, but there are many
                if (_enemies[i] != null && _enemies[i].Id == id)
                {
                    enemy = _enemies[i];
                    break;
                }
            if (enemy == null || (enemy.Fight != null && enemy.Fight.Room != _useOrder)) return;
            Vector3 p = enemy.transform.position;
            _useFrame.ToFrame(p.x, p.z, out double d, out double s);
            var e = new SessionEvent(Now(), RoomUseEvents.Death) { EnemyId = id };
            e.AddExtra("d", Metre(d));
            e.AddExtra("s", Metre(s));
            Add(e);
        }

        /// <summary>The fight followed ended (work order 097): why, and her last place. Nothing when none is followed.</summary>
        public void NoteFightEnd(string why)
        {
            if (!_useOn) return;
            _useOn = false;
            if (!_recording || _writer == null) return;
            try
            {
                var e = new SessionEvent(Now(), RoomUseEvents.End) { Result = why ?? "" };
                if (_player != null)
                {
                    Vector3 her = _player.Position;
                    _useFrame.ToFrame(her.x, her.z, out double d, out double s);
                    e.AddExtra("d", Metre(d));
                    e.AddExtra("s", Metre(s));
                }
                Add(e);
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }
    }
}
