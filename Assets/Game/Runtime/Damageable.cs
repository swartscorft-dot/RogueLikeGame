using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>Something that can be targeted and hit: health and a round footprint on the ground.</summary>
    public sealed class Damageable : MonoBehaviour
    {
        static readonly List<Damageable> s_All = new List<Damageable>();

        /// <summary>Every enabled Damageable, in the order they were enabled.</summary>
        public static IReadOnlyList<Damageable> All => s_All;

        public int MaxHealth = Placeholders.DummyHealth;
        public float Radius = 0.5f;

        Health _health;

        public int CurrentHealth => _health.Current;
        public bool IsAlive => !_health.IsDead;
        public int HitsTaken { get; private set; }
        public GroundPoint GroundPosition => new GroundPoint(transform.position.x, transform.position.z);
        public TargetInfo Info => new TargetInfo(GroundPosition, Radius, IsAlive);

        /// <summary>Raised after a hit that removed health, with the amount removed.</summary>
        public event Action<int> Damaged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_All.Clear();

        void Awake() => _health = new Health(MaxHealth);
        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        /// <summary>Applies damage and returns the amount actually removed.</summary>
        public int TakeHit(int damage)
        {
            int applied = _health.TakeDamage(damage);
            if (applied > 0)
            {
                HitsTaken++;
                Damaged?.Invoke(applied);
            }
            return applied;
        }

        public void Refill() => _health.Reset();
    }
}
