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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_All.Clear();
            MarksLost = null;
        }

        void Awake() => _health = new Health(MaxHealth);
        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        void Update()
        {
            int fallen = _marks.TakeFallenOff(Time.time);
            if (fallen > 0) MarksLost?.Invoke(this, fallen, MarkLoss.FellOff);
        }

        /// <summary>Applies damage and returns the amount actually removed. A hit that kills it takes its marks with it.</summary>
        public int TakeHit(int damage)
        {
            int applied = _health.TakeDamage(damage);
            if (applied > 0)
            {
                HitsTaken++;
                Damaged?.Invoke(applied);
                if (_health.IsDead) ClearMarks(MarkLoss.Died);
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
            _health.Reset();
            ClearMarks(MarkLoss.Reset);
        }

        /// <summary>Gives it a new maximum and fills it to that.</summary>
        public void SetMaxHealth(int max)
        {
            MaxHealth = max;
            _health = new Health(max);
            ClearMarks(MarkLoss.Reset);
        }
    }
}
