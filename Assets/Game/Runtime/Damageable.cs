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

        public int CurrentHealth => _health.Current;
        public bool IsAlive => !_health.IsDead;
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
            float now = Time.time;
            if (Stagger != null)
            {
                if (Stagger.Tick(now)) DazeEnded?.Invoke();
                damage = Stagger.Scale(damage, now);
            }
            int applied = _health.TakeDamage(damage);
            if (applied > 0)
            {
                HitsTaken++;
                Damaged?.Invoke(applied);
                if (_health.IsDead) ClearMarks(MarkLoss.Died);
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
