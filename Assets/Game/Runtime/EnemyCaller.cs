using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The caller (work order 137; Kristian's tick of 2026-11-25, "Ja, fra etasje 2"; its numbers <see cref="CallerValues"/>, where it stands
    /// and what gives way for it <see cref="RoomCallerRules"/>, its calls brought by the floor, <see cref="GeneratedLevels"/>' room callers):
    /// the one far enemy she must go to first. It wears the caster's model and clips in its own tint (<see cref="CallerValues.Tint"/>), has a
    /// caster's health (the floor gives it <see cref="LevelValues.HealthOf"/> of a caster) and casts none of the caster's attacks.
    ///
    /// It keeps its place: it wakes, gives up and goes home through the brain as every kind does (its room's fight holds it awake), but in
    /// the chase it never walks, not at her and not away from her (the caster's backing off is not kept): it stands where the floor put it,
    /// facing her.
    ///
    /// Its one attack is its call (<see cref="CallerValues.CallAttack"/>): <see cref="CallerValues.FirstCallAfter"/> after it wakes and then
    /// every <see cref="CallerValues.CallEvery"/> from the start of the call before, it asks the floor for the call (<see cref="BeginCall"/>,
    /// which puts down the discs and says how many; none, and there is no tell and the next is due a call's time later). The call's wind-up
    /// is its tell, <see cref="CallerValues.TellSeconds"/>: the caster's big cast (CastBig) on its body and the game's wind-up ring round it,
    /// blue as every counterable wind-up. At its end the swarmers stand at the discs (<see cref="EndCall"/>). A daze of any kind drops it
    /// as every wind-up is dropped, and Stopping Shot from its front counters it as it counters the caster's big casts (with
    /// <see cref="CallerValues.Counterable"/>): the discs go and none comes (<see cref="CancelCall"/>); so does its death or its fight's end
    /// in the tell. It has no stagger bar, as the caster has none.
    ///
    /// It tells the floor the hit that kills it, at that hit (<see cref="Fell"/>), so its called swarmers fall at once, before a Chain Kill
    /// bounce or a Pass It On looks for the next living enemy.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyCaller : Enemy
    {
        /// <summary>Its numbers: the floor's (<see cref="UseValues"/>), else the defaults.</summary>
        public CallerValues Calls = new CallerValues();
        /// <summary>Its brain's numbers: the caster's health, speeds and ranges, with its call as its one attack.</summary>
        public EnemyValues Tuning = CasterBrain(new CallerValues().CallAttack());
        /// <summary>m, its capsule and agent: the caster's.</summary>
        public float BodyRadius = new CasterValues().BodyRadius, BodyHeight = new CasterValues().BodyHeight;

        /// <summary>Asked as a call is due, with the call's wind-up about to begin: puts down its discs and says how many (0: no call now). Set by the floor.</summary>
        [NonSerialized] public Func<EnemyCaller, int> BeginCall;
        /// <summary>The call's tell is over: its swarmers stand at the discs. Set by the floor.</summary>
        [NonSerialized] public Action<EnemyCaller> EndCall;
        /// <summary>The call's tell was cut short (a daze, a counter, its death, its fight over): its discs go, none comes. Set by the floor.</summary>
        [NonSerialized] public Action<EnemyCaller> CancelCall;
        /// <summary>Raised once, at the hit that kills it (in that hit, before anything after it looks for a living enemy).</summary>
        public event Action<EnemyCaller> Fell;

        float _nextCallAt = float.PositiveInfinity;
        bool _callPending, _fellTold;

        public override EnemyKind Kind => EnemyKind.Caller;
        public override EnemyValues Values => Tuning;
        /// <summary>It never waits for a turn to attack: its clock is its own (<see cref="CallerValues.CallEvery"/>).</summary>
        public override TurnSort AttackSort => TurnSort.None;

        /// <summary>Calls begun (each with its tell), brought by none (due and nothing could come), and cut short.</summary>
        public int CallsBegun { get; private set; }
        public int CallsNone { get; private set; }
        public int CallsCut { get; private set; }
        /// <summary>Calls whose tell ended and whose swarmers stood.</summary>
        public int CallsMade { get; private set; }
        /// <summary>True while a call's tell runs (its discs on the ground).</summary>
        public bool Calling => _callPending;
        /// <summary>The game time its next call is due; infinite before it wakes.</summary>
        public float NextCallAt => _nextCallAt;
        /// <summary>The game time of the first hit it took, or a negative number.</summary>
        public float FirstHitAt { get; private set; } = -1f;
        /// <summary>The game time it died, or a negative number.</summary>
        public float DiedAt { get; private set; } = -1f;
        /// <summary>Its call as its brain has it.</summary>
        public EnemyAttackDefinition CallAttack => _call;
        EnemyAttackDefinition _call => Tuning.Attacks[0];

        /// <summary>The caster's brain numbers (its health, speeds, ranges) with this call as the one attack.</summary>
        static EnemyValues CasterBrain(EnemyAttackDefinition call)
        {
            EnemyValues c = new CasterValues().Enemy;
            return new EnemyValues
            {
                Health = c.Health, MoveSpeed = c.MoveSpeed, AggroRange = c.AggroRange, LeashRange = c.LeashRange, RespawnDelay = c.RespawnDelay,
                Attacks = new[] { call }, UseModels = c.UseModels,
            };
        }

        protected override void Awake()
        {
            base.Awake();
            _self.Damaged += OnHurt;
            Paint();
        }

        protected override void OnDestroy()
        {
            if (_self != null) _self.Damaged -= OnHurt;
            if (_callPending)
            {
                _callPending = false;
                CancelCall?.Invoke(this);
            }
            base.OnDestroy();
        }

        /// <summary>The floor's numbers for it, before its first frame: its call and its tint from them.</summary>
        public void UseValues(CallerValues values)
        {
            if (values == null) return;
            Calls = values;
            Tuning.Attacks = new[] { values.CallAttack() };
            Paint();
        }

        /// <summary>Its tint on its body (the caster's model, or its capsule): its body's colour, which the daze goes back to.</summary>
        void Paint()
        {
            BodyColor = EnemyFactory.ColorOf(Calls.Tint);
            if (BodyRenderer != null) EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
        }

        void OnHurt(int amount)
        {
            if (FirstHitAt < 0f) FirstHitAt = Time.time;
            if (!IsDead || _fellTold) return;
            _fellTold = true;
            DiedAt = Time.time;
            Fell?.Invoke(this);
        }

        protected override void Update()
        {
            base.Update();
            // A tell that ended with no call made (a daze or a counter dropped it, it died, its fight ended): its discs go.
            if (_callPending && (IsDead || State != EnemyState.Windup || Attack != _call))
            {
                _callPending = false;
                CallsCut++;
                CancelCall?.Invoke(this);
            }
        }

        protected override void OnWoke() => _nextCallAt = Time.time + Mathf.Max(0f, Calls.FirstCallAfter);

        /// <summary>It keeps its place: in the chase it stands where it is, facing her.</summary>
        protected override void Chase(Vector3 toPlayer, float distance)
        {
            Halt();
            Face(toPlayer);
        }

        /// <summary>Its call, when one is due and the floor puts down at least one disc; else nothing (it waits).</summary>
        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values)
        {
            float now = Time.time;
            if (now < _nextCallAt) return null;
            _nextCallAt = now + Mathf.Max(0.05f, Calls.CallEvery);
            int discs = BeginCall != null ? BeginCall(this) : 0;
            if (discs <= 0)
            {
                CallsNone++;
                return null;
            }
            CallsBegun++;
            _callPending = true;
            return _call;
        }

        /// <summary>The turns' question: never asked (it takes none), and never a call counted for it.</summary>
        protected override EnemyAttackDefinition WouldChoose(float distance, EnemyValues values) => null;

        /// <summary>The call's strike is the end of its tell: its swarmers stand; nothing is hit.</summary>
        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (attack != _call || !_callPending) return true;
            _callPending = false;
            CallsMade++;
            EndCall?.Invoke(this);
            return true;
        }
    }
}
