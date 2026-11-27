using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>How marks were lost without a spender taking them.</summary>
    public enum MarkLoss { FellOff, Died, Reset }

    /// <summary>
    /// Something that can be targeted and hit: health and a round footprint on the ground. It carries the marks the
    /// player's builders put on it (decision 0005, <see cref="Marks"/>); a dead or reset target has none.
    /// </summary>
    public sealed class Damageable : MonoBehaviour
    {
        static readonly List<Damageable> s_All = new List<Damageable>();

        /// <summary>Every enabled Damageable, in the order they were enabled.</summary>
        public static IReadOnlyList<Damageable> All => s_All;

        /// <summary>
        /// Raised when marks are lost unused: they fell off, or the target died or was reset. With the target and how
        /// many. For the play session recorder; nothing in the game listens.
        /// </summary>
        public static event Action<Damageable, int, MarkLoss> MarksLost;

        public int MaxHealth = Placeholders.DummyHealth;
        public float Radius = 0.5f;

        Health _health;
        readonly Marks _marks = new Marks();

        // Work order 096 (as 92a5c71 made her health): safe to ask before this component has woken, from another's Awake or
        // OnEnable on the same object (an enemy's IsDead asks IsAlive), with the values it wakes with: full, alive.
        public int CurrentHealth => _health != null ? _health.Current : MaxHealth;
        public bool IsAlive => _health == null || !_health.IsDead;
        public int HitsTaken { get; private set; }
        public GroundPoint GroundPosition => new GroundPoint(transform.position.x, transform.position.z);
        public TargetInfo Info => new TargetInfo(GroundPosition, Radius, IsAlive);

        /// <summary>The marks it carries now.</summary>
        public int MarkCount => _marks.Count(Time.time);
        /// <summary>Seconds until the marks it carries fall off; 0 when it carries none.</summary>
        public float MarksFallOffIn => MarkCount > 0 ? Mathf.Max(0f, _marks.FallOffAt - Time.time) : 0f;

        /// <summary>Raised after a hit that removed health, with the amount removed.</summary>
        public event Action<int> Damaged;

        /// <summary>
        /// Work order 141, Hunter's Count: her basic hits on it toward its next mark (the count is the enemy's own); 0 again when it
        /// dies or is reset.
        /// </summary>
        [NonSerialized] public int HunterHits;
        /// <summary>
        /// Work order 141, Pass It On: set by her hit while it lasts when the marks it carries go on to another if the hit kills it:
        /// then its death keeps them for her (<see cref="TakePassedMarks"/>) and they are not lost (no <see cref="MarksLost"/>).
        /// </summary>
        [NonSerialized] public bool PassMarksOnDeath;
        int _passedMarks;

        /// <summary>Work order 141: the marks its death kept for Pass It On, taken once; 0 when none.</summary>
        public int TakePassedMarks()
        {
            int marks = _passedMarks;
            _passedMarks = 0;
            return marks;
        }

        /// <summary>
        /// Its stagger bar, or null for a target without one (work order 008: the guard has one). With a bar, hits take
        /// stagger off it, and while it is dazed every hit deals more (<see cref="StaggerBar"/>).
        /// </summary>
        public StaggerBar Stagger { get; set; }
        /// <summary>Raised when a hit took stagger off its bar: the points taken and the points left.</summary>
        public event Action<float, float> StaggerTaken;
        /// <summary>Raised when its bar was emptied and the daze began.</summary>
        public event Action Dazed;
        /// <summary>Raised when the daze ended and the bar was full again.</summary>
        public event Action DazeEnded;
        public bool IsDazed => (Stagger != null && Stagger.IsDazed(Time.time)) || Time.time < _dazedUntil;

        /// <summary>
        /// The share of every hit a warder's ward takes off while it is on (work order 079, <see cref="WarderRules.Lessened"/>):
        /// 0, no ward. Set and cleared by the warder; one rule for every hit, hers and a bomber's blast.
        /// </summary>
        [NonSerialized] public float WardShare;
        /// <summary>Raised after a hit the ward lessened, with the damage it took off (work order 079): the shell flashes, the log counts.</summary>
        public event Action<int> Warded;
        /// <summary>
        /// While true no hit does anything to it (work order 147: the trial boss through each phase's transition, "the hit that crosses
        /// the line counts; nothing after it does"). Set and cleared by that boss; false for everything else.
        /// </summary>
        [NonSerialized] public bool Untouchable;

        // Work order 015 (2026-11-20).
        readonly Tag _tag = new Tag();
        float _dazedUntil = float.NegativeInfinity;

        /// <summary>True while Tag Round's tag is on it: her hits deal more (<see cref="ScaleForTag"/>).</summary>
        public bool IsTagged => IsAlive && _tag.IsOn(Time.time);
        public float TagRemaining => IsAlive ? _tag.Remaining(Time.time) : 0f;

        /// <summary>Tags it for this long, this share more damage from her; a new tag starts it again. A dead target takes none.</summary>
        public void ApplyTag(float seconds, float share)
        {
            if (IsAlive) _tag.Apply(Time.time, seconds, share);
        }

        /// <summary>One of her hits on it, raised while it is tagged.</summary>
        public int ScaleForTag(int damage) => _tag.Scale(damage, Time.time);

        /// <summary>
        /// Dazed for this long whether or not it has a stagger bar (a counter, Stopping Shot): <see cref="IsDazed"/> is true
        /// and an enemy stands still. It does not raise the damage it takes, as an emptied bar's daze does.
        /// </summary>
        public void DazeFor(float seconds)
        {
            _dazedUntil = Mathf.Max(_dazedUntil, Time.time + Mathf.Max(0f, seconds));
            Dazed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_All.Clear();
            MarksLost = null;
            Destroyed = 0;
        }

        /// <summary>
        /// Targets destroyed since the game started (with their level or scene, or by themselves): a listener that keeps
        /// targets as keys forgets the gone ones only when this has moved (work order 026, the second review of 2026-11-20).
        /// </summary>
        public static int Destroyed { get; private set; }

        void Awake() => _health = new Health(MaxHealth);
        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);
        void OnDestroy() => Destroyed++;

        void Update()
        {
            int fallen = _marks.TakeFallenOff(Time.time);
            if (fallen > 0) MarksLost?.Invoke(this, fallen, MarkLoss.FellOff);
            if (Stagger != null && Stagger.Tick(Time.time)) DazeEnded?.Invoke();
        }

        /// <summary>
        /// Applies damage and returns the amount actually removed. A hit that kills it takes its marks with it. With a
        /// stagger bar, the damage is raised while it is dazed, and a hit that leaves it alive takes
        /// <paramref name="stagger"/> off the bar.
        /// </summary>
        public int TakeHit(int damage, float stagger = 0f)
        {
            if (Untouchable) return 0; // work order 147
            float now = Time.time;
            if (Stagger != null)
            {
                if (Stagger.Tick(now)) DazeEnded?.Invoke();
                damage = Stagger.Scale(damage, now);
            }
            int full = damage;
            if (WardShare > 0f) damage = WarderRules.Lessened(damage, WardShare); // work order 079: a ward on it
            int applied = _health.TakeDamage(damage);
            if (full > damage && applied > 0) Warded?.Invoke(full - damage);
            if (applied > 0)
            {
                HitsTaken++;
                Damaged?.Invoke(applied);
                if (_health.IsDead && PassMarksOnDeath) // work order 141: Pass It On takes them on
                {
                    _passedMarks = _marks.Clear(now);
                    HunterHits = 0;
                }
                else if (_health.IsDead) ClearMarks(MarkLoss.Died);
            }
            if (applied > 0 && !_health.IsDead && Stagger != null && stagger > 0f)
            {
                float taken = Stagger.Take(stagger, now, out bool dazed);
                if (taken > 0f) StaggerTaken?.Invoke(taken, Stagger.Current);
                if (dazed) Dazed?.Invoke();
            }
            return applied;
        }

        /// <summary>
        /// Adds marks, no more than <paramref name="max"/> in all, and starts their lifetime again. A dead target takes
        /// none. Returns the count after.
        /// </summary>
        public int AddMarks(int marks, int max, float lifetime)
        {
            if (!IsAlive) return 0;
            return _marks.Add(marks, Time.time, max, lifetime);
        }

        /// <summary>A spender takes every mark it carries. Returns how many.</summary>
        public int TakeMarks() => _marks.TakeAll(Time.time);

        void ClearMarks(MarkLoss why)
        {
            int lost = _marks.Clear(Time.time);
            HunterHits = 0; // work order 141: Hunter's Count's count goes with them (a death or a reset)
            if (lost > 0) MarksLost?.Invoke(this, lost, why);
        }

        public void Refill()
        {
            _tag.Clear();
            _dazedUntil = float.NegativeInfinity;
            _health.Reset();
            ClearMarks(MarkLoss.Reset);
            Stagger?.Reset();
        }

        /// <summary>Gives it a new maximum and fills it to that, and its stagger bar too.</summary>
        public void SetMaxHealth(int max)
        {
            MaxHealth = max;
            _health = new Health(max);
            _tag.Clear();
            _dazedUntil = float.NegativeInfinity;
            ClearMarks(MarkLoss.Reset);
            Stagger?.Reset();
        }
    }
}
