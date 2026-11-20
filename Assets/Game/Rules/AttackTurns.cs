using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The sorts of enemy for the turns to attack (work order 023, 2026-11-20): heavy (the brute, the guard), ranged (the
    /// shooter, and any kind that attacks from afar), light (the swarmer). None takes no turn and is never held back:
    /// the act boss, and an elite if there are any.
    /// </summary>
    public enum TurnSort { None, Heavy, Ranged, Light }

    /// <summary>
    /// The numbers of the turns to attack (work order 023, 2026-11-20). Before it every enemy wound up whenever an attack
    /// fitted: the test pilot's flights of 2026-11-20 saw rooms of four shooters, four brutes and seven swarmers wind up
    /// overlapping attacks faster than one dash on a 10 s cooldown can answer (six shooter hits in 3.6 s), and the brute's
    /// Shockwave and the shooter's shot killed every pilot on floors 3 and 5 in 5 to 30 s. Pillar 4: a crowd must be
    /// readable, so not everyone may wind up at once. <see cref="On"/> off gives exactly the behaviour from before.
    /// </summary>
    [System.Serializable]
    public sealed class TurnValues
    {
        public bool On = true;
        public int HeavyAtOnce = 2;      // heavies winding up at the same moment, at most (none before)
        public int RangedAtOnce = 2;     // ranged winding up at the same moment, at most (none before)
        public float Gap = 0.5f;         // s from one wind-up of a sort beginning to the next of that sort (none before)
        /// <summary>
        /// s after its last ask that an enemy still counts as waiting: one that stops asking (she is out of its band, it
        /// lost sight of her, it died) no longer holds a turn back after this. Not a balance number.
        /// </summary>
        public float WaitingFor = 0.25f;

        /// <summary>
        /// One crowd, one queue (work order 030, lever D): heavy and ranged share ONE limit of wind-ups at once instead of
        /// a limit each, with its own gap between any two of them. The test pilot's flights of 2026-11-20 saw her die in
        /// rooms where two brutes and two shooters wound up together under the first pass's rule. Off: the first pass's
        /// rule exactly. Off by default: flown on (work order 030's fourth round) no pilot got further than with it off,
        /// and one flight ended with the Ace stepping sideways for good between two shooters out of its range that took
        /// turns winding up at it. Built and tested, so a later pass can turn it on.
        /// </summary>
        public bool Shared = false;
        public int SharedAtOnce = 2;            // heavy and ranged together winding up at once on floors 1 to 3 (two of each before)
        public int SharedAtOnceLate = 3;        // the same from SharedLateFromFloor on
        public int SharedLateFromFloor = 4;
        public float SharedGap = 0.7f;          // s from one heavy or ranged wind-up beginning to the next (0.5 within a sort before)
    }

    /// <summary>
    /// The turns to attack (work order 023, 2026-11-20; pillar 4, readable combat). Work order 030 (lever D): with
    /// <see cref="TurnValues.Shared"/> on, heavy and ranged wait in one queue under one limit by the floor
    /// (<see cref="SharedCap"/>) and one gap; what follows is the first pass's rule, which <see cref="TurnValues.Shared"/>
    /// off gives exactly. Among the enemies that take turns
    /// (those of a generated floor, awake and fighting her), at most <see cref="TurnValues.HeavyAtOnce"/> heavies and at
    /// most <see cref="TurnValues.RangedAtOnce"/> ranged wind up at the same moment, and a wind-up of a sort begins no
    /// sooner than <see cref="TurnValues.Gap"/> after the last one of that sort began. Light enemies and those of no sort
    /// are never held back.
    ///
    /// An enemy about to begin a wind-up asks (<see cref="Ask"/>) each frame it would; while it may not, it goes on doing
    /// what it does between attacks, and it counts as waiting from its first ask. When a turn is free the one of the sort
    /// that has waited longest goes first: another that asks is told no while an older waiter of its sort is still
    /// asking. It says when the wind-up begins (<see cref="Began"/>, with its length, after which it counts no more) and
    /// when one ends early (<see cref="Ended"/>: a daze, a death) or it is gone (<see cref="Forget"/>). Times are game
    /// seconds; it never reads a clock.
    /// </summary>
    public sealed class AttackTurns
    {
        struct Waiter
        {
            public TurnSort Sort;
            public float Since;
            public float LastAsk;
        }

        struct Windup
        {
            public TurnSort Sort;
            public float Until;
        }

        public TurnValues Values = new TurnValues();

        readonly Dictionary<int, Waiter> _waiting = new Dictionary<int, Waiter>();
        readonly Dictionary<int, Windup> _windups = new Dictionary<int, Windup>();
        readonly List<int> _gone = new List<int>();
        float _lastHeavy = float.NegativeInfinity;
        float _lastRanged = float.NegativeInfinity;

        /// <summary>The floor she fights on (work order 030): the shared limit is by floor. 1 until a floor says.</summary>
        public int Floor = 1;

        /// <summary>The shared limit of heavy and ranged wind-ups at once on this floor (work order 030, lever D).</summary>
        public int SharedCap(int floor) => floor >= Values.SharedLateFromFloor ? Values.SharedAtOnceLate : Values.SharedAtOnce;

        /// <summary>Do heavy and ranged share one queue now? With the rule and its shared form on.</summary>
        bool SharedNow => Values.On && Values.Shared;

        static bool Queued(TurnSort sort) => sort == TurnSort.Heavy || sort == TurnSort.Ranged;

        /// <summary>
        /// The sort of a kind: heavy for the brute and the guard, ranged for the shooter and the caster (work order 034: it
        /// attacks from afar, so it waits with the shooters), light for the swarmer, none for the boss.
        /// </summary>
        public static TurnSort SortOf(EnemyKind kind) =>
            kind == EnemyKind.Brute || kind == EnemyKind.Guard ? TurnSort.Heavy
            : kind == EnemyKind.Shooter || kind == EnemyKind.Caster ? TurnSort.Ranged
            : kind == EnemyKind.Swarmer ? TurnSort.Light
            : TurnSort.None;

        /// <summary>Is a sort ever held back? Heavy and ranged, with the rule on.</summary>
        public bool Holds(TurnSort sort) => Values.On && (sort == TurnSort.Heavy || sort == TurnSort.Ranged);

        /// <summary>The most of a sort winding up at once; no limit for the sorts not held.</summary>
        public int Cap(TurnSort sort) =>
            sort == TurnSort.Heavy ? Values.HeavyAtOnce : sort == TurnSort.Ranged ? Values.RangedAtOnce : int.MaxValue;

        /// <summary>
        /// May enemy <paramref name="id"/> of this sort begin a wind-up now? It counts as waiting from its first ask until
        /// it begins. Always yes for a sort not held, and with the rule off.
        /// </summary>
        public bool Ask(int id, TurnSort sort, float now)
        {
            if (!Holds(sort)) return true;
            if (_waiting.TryGetValue(id, out Waiter me) && me.Sort == sort && now - me.LastAsk <= Values.WaitingFor)
                me.LastAsk = now;
            else
                me = new Waiter { Sort = sort, Since = now, LastAsk = now };
            _waiting[id] = me;

            if (SharedNow)
            {
                // One queue for heavy and ranged (work order 030): one limit, one gap, the longest waiter of either first.
                if (WindingUp(TurnSort.Heavy, now) + WindingUp(TurnSort.Ranged, now) >= SharedCap(Floor)) return false;
                if (now - System.Math.Max(_lastHeavy, _lastRanged) < Values.SharedGap) return false;
            }
            else
            {
                if (WindingUp(sort, now) >= Cap(sort)) return false;
                if (now - LastBegun(sort) < Values.Gap) return false;
            }
            foreach (KeyValuePair<int, Waiter> w in _waiting)
            {
                bool sameQueue = SharedNow ? Queued(w.Value.Sort) : w.Value.Sort == sort;
                if (w.Key == id || !sameQueue || now - w.Value.LastAsk > Values.WaitingFor) continue;
                if (w.Value.Since < me.Since || (w.Value.Since == me.Since && w.Key < id)) return false; // an older one goes first
            }
            return true;
        }

        /// <summary>Enemy <paramref name="id"/> began a wind-up of <paramref name="seconds"/> now: it counts until then, or until it ends early.</summary>
        public void Began(int id, TurnSort sort, float now, float seconds)
        {
            _waiting.Remove(id);
            if (sort == TurnSort.None) return;
            _windups[id] = new Windup { Sort = sort, Until = now + (seconds > 0f ? seconds : 0f) };
            if (sort == TurnSort.Heavy) _lastHeavy = now;
            else if (sort == TurnSort.Ranged) _lastRanged = now;
        }

        /// <summary>Its wind-up ended before its time (struck, dazed, dead): it no longer counts.</summary>
        public void Ended(int id) => _windups.Remove(id);

        /// <summary>It is gone (dead, removed): neither waiting nor winding up.</summary>
        public void Forget(int id)
        {
            _waiting.Remove(id);
            _windups.Remove(id);
        }

        /// <summary>Is it waiting for a turn now (asked within <see cref="TurnValues.WaitingFor"/>)?</summary>
        public bool IsWaiting(int id, float now) => _waiting.TryGetValue(id, out Waiter w) && now - w.LastAsk <= Values.WaitingFor;

        /// <summary>Since when it has waited, or NaN when it is not waiting.</summary>
        public float WaitingSince(int id, float now) => IsWaiting(id, now) ? _waiting[id].Since : float.NaN;

        /// <summary>How many of a sort are winding up now.</summary>
        public int WindingUp(TurnSort sort, float now)
        {
            int n = 0;
            _gone.Clear();
            foreach (KeyValuePair<int, Windup> w in _windups)
            {
                if (now >= w.Value.Until) _gone.Add(w.Key);
                else if (w.Value.Sort == sort) n++;
            }
            foreach (int id in _gone) _windups.Remove(id);
            return n;
        }

        /// <summary>When the last wind-up of a sort began; minus infinity before the first.</summary>
        public float LastBegun(TurnSort sort) =>
            sort == TurnSort.Heavy ? _lastHeavy : sort == TurnSort.Ranged ? _lastRanged : float.NegativeInfinity;

        /// <summary>Forgets everything: a new scene, a new floor.</summary>
        public void Clear()
        {
            _waiting.Clear();
            _windups.Clear();
            _lastHeavy = _lastRanged = float.NegativeInfinity;
        }
    }
}
