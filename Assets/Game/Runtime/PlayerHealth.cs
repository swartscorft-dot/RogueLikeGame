using System;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The player's health. A hit during the dash's i-frame does nothing.
    /// At zero the character drops what it was doing, and after a moment stands at the start again with
    /// full health: the control room is for practice. In a run, death ends the run (work order 012, <see cref="GeneratedLevels"/>),
    /// and the run's upgrades raise her maximum (<see cref="RefreshMax"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        public int MaxHealth = Placeholders.PlayerHealth;

        Health _health;
        PlayerCharacter _player;
        Vector3 _spawn;
        float _deadSince = -1f;

        /// <summary>Her health now; before this component has woken, the full <see cref="MaxHealth"/> it wakes with (work order 096: safe to ask from another's Awake, as <see cref="IsAlive"/>).</summary>
        public int Current => _health != null ? _health.Current : MaxHealth;
        /// <summary>
        /// Alive: also before this component has woken (in a scene her character wakes first and asks at once; the morning of
        /// 2026-11-23 that threw in her Awake, which switched her off: she could not move or shoot).
        /// </summary>
        public bool IsAlive => _health == null || !_health.IsDead;
        /// <summary>The maximum now: <see cref="MaxHealth"/>, and in a run the run's upgrades on top (work order 012); before this component has woken, <see cref="MaxHealth"/> (work order 096).</summary>
        public int Max => _health != null ? _health.Max : MaxHealth;

        /// <summary>
        /// Reads the maximum again from <see cref="MaxHealth"/> and the run's upgrades on the character
        /// (<see cref="PlayerCharacter.Modifiers"/>), keeping what she has (cut to the new maximum). Called when an
        /// upgrade is taken and when a run starts or ends (work order 012).
        /// </summary>
        public void RefreshMax()
        {
            RunModifiers m = _player != null ? _player.Modifiers : null;
            _health.SetMax(m != null ? m.MaxHealthFor(MaxHealth) : MaxHealth); // the rack's share too (work order 029), then the chests'
        }

        /// <summary>Gives back health, up to the maximum; returns what was given.</summary>
        public int Heal(int amount) => _health.Heal(amount);

        /// <summary>Full health, alive: the start of a run. Cancels a respawn that was waiting.</summary>
        public void RestoreFull()
        {
            RefreshMax();
            _health.Reset();
            _deadSince = -1f;
        }
        public int HitsTaken { get; private set; }
        /// <summary>Hits that landed during the dash's i-frame and did nothing.</summary>
        public int HitsAvoided { get; private set; }
        public int Deaths { get; private set; }

        /// <summary>
        /// True from the moment a run's result is in (the act cleared, or her death) until its end screen is answered
        /// (work order 026, the second review of 2026-11-20, set by <see cref="GeneratedLevels"/>): no hit removes health,
        /// so a strike already winding up plays out and lands on nothing.
        /// </summary>
        public bool Untouchable { get; set; }

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
            if (!IsAlive || damage <= 0 || Untouchable) return 0;
            if (HurtGuard.Holds(this)) return 0; // work order 147: the trial boss's hurt guard (HurtGuard.cs)
            if (_player.IsInvulnerable)
            {
                HitsAvoided++;
                return 0;
            }

            damage = KnightsResolveBlow.Spare(_player, damage, _health.Current); // work order 106: Knight's Resolve, once a run with the piece
            int applied = _health.TakeDamage(damage);
            if (applied > 0)
            {
                HitsTaken++;
                HurtGuard.Took(this); // work order 147
                Damaged?.Invoke(applied);
                if (!_health.IsDead) _player.TookHit(applied); // work order 092: hurt while sprinting, a small stagger
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
