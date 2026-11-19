using System;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The player's health. A hit during the dash's i-frame does nothing.
    /// At zero the character drops what it was doing, and after a moment stands at the start again with
    /// full health: the control room is for practice. In a run, death will end the run.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        public int MaxHealth = Placeholders.PlayerHealth;

        Health _health;
        PlayerCharacter _player;
        Vector3 _spawn;
        float _deadSince = -1f;

        public int Current => _health.Current;
        public bool IsAlive => !_health.IsDead;
        public int HitsTaken { get; private set; }
        /// <summary>Hits that landed during the dash's i-frame and did nothing.</summary>
        public int HitsAvoided { get; private set; }
        public int Deaths { get; private set; }

        /// <summary>Raised after a hit that removed health, with the amount removed.</summary>
        public event Action<int> Damaged;

        /// <summary>Raised when she dies, before she drops what she was doing. For the play session recorder.</summary>
        public event Action Died;

        void Awake()
        {
            _health = new Health(MaxHealth);
            _player = GetComponent<PlayerCharacter>();
            _spawn = transform.position;
        }

        /// <summary>Applies damage and returns the amount actually removed.</summary>
        public int TakeHit(int damage)
        {
            if (!IsAlive || damage <= 0) return 0;
            if (_player.IsInvulnerable)
            {
                HitsAvoided++;
                return 0;
            }

            int applied = _health.TakeDamage(damage);
            if (applied > 0)
            {
                HitsTaken++;
                Damaged?.Invoke(applied);
            }
            if (_health.IsDead)
            {
                Deaths++;
                _deadSince = Time.time;
                Died?.Invoke();
                _player.Teleport(_player.Position); // drops the order, the skill and the buffered press
            }
            return applied;
        }

        void Update()
        {
            if (_deadSince < 0f || Time.time - _deadSince < Placeholders.PlayerRespawnDelay) return;
            _deadSince = -1f;
            _health.Reset();
            _player.Teleport(_spawn);
        }
    }
}
