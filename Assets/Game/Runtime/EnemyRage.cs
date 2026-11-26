using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Rage when a part is lost (work order 129; Kristian 2026-11-25, asked what a lost part should do: "De sikter dårligere, men
    /// fyrer av mye oftere. en slags rage mode."). What every kind of enemy shares of it; the rules are <see cref="PartRageRules"/>,
    /// the values <see cref="PartRageValues"/>, the switch <see cref="PartRageValues.On"/>.
    ///
    /// From the first step at which its health is at or under work order 122's first part's share
    /// (<see cref="PartRageRules.Due"/>: the rules value, not the picture, so the same with 122's look off or with no model) it is
    /// enraged until it dies; standing up again (the practice brute) it is whole and calm. Enraged, with the switch on:
    ///
    /// - its brain stands <see cref="PartRageValues.RestShare"/> of each attack's rest after the strike (<see cref="EnemyBrain.RestShare"/>);
    ///   the wind-up is never touched;
    /// - at the start of each wind-up, where its aim is fixed today, it draws one angle from its own seeded stream
    ///   (<see cref="PartRageRules.Seed"/>: the floor's seed, the floor and its place's name), within its kind's width
    ///   (<see cref="PartRageRules.Degrees"/>); its aim point is turned about it by that angle and it faces it, so its body, its draw
    ///   and its lance show the way the attack will go. A kind that looses at the release (the archer's arrow, the caster's orb, the
    ///   warder's bolt) turns the way it would loose today by the same angle (<see cref="RageTurn"/>); the lancer turns to her by it
    ///   through its wind-up and charges down that line.
    ///
    /// No new sign: no glow, sound, marker or text (the missing part is the sign). Its frame allocates nothing: the stream is
    /// seeded once, at its first enraged wind-up (one name read).
    /// </summary>
    public abstract partial class Enemy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRageSwitch() => PartRageValues.On = true; // every Play starts on

        bool _enraged;
        float _enragedAt = -1f;
        float _rageAngle;
        bool _rageSeeded;
        BoneBreakRandom _rageStream;

        /// <summary>True while it is enraged (a part lost by 122's rule, alive) and the switch is on (work order 129).</summary>
        public bool IsEnraged => PartRageValues.On && _enraged;
        /// <summary>The game time its rage began; negative while it has none (work order 129).</summary>
        public float EnragedSince => _enraged ? _enragedAt : -1f;
        /// <summary>
        /// Degrees (+ to the left seen from above) the attack being wound up, or the last, is thrown off by: drawn at its wind-up's
        /// start; 0 for one begun calm or with the switch off (work order 129).
        /// </summary>
        public float RageAngle => _rageAngle;

        /// <summary>
        /// Once a frame at the top of its step (work order 129): its rage begins when its health is at the first part's share, ends
        /// with its death; the share of its rest it stands now (exactly 1 when calm or with the switch off).
        /// </summary>
        float RageRest(float now)
        {
            if (_self == null || !_self.IsAlive)
            {
                _enraged = false;
                _enragedAt = -1f;
                return 1f;
            }
            if (!_enraged && PartRageRules.Due(Kind, _self.CurrentHealth, _self.MaxHealth, BoneBreakValues.Live))
            {
                _enraged = true;
                _enragedAt = now;
            }
            return PartRageRules.RestShare(IsEnraged, PartRageValues.Live);
        }

        /// <summary>
        /// Where a wind-up beginning now is aimed (work order 129): <paramref name="her"/> itself when calm, with the switch off or for a
        /// kind whose aim is left alone; enraged, <paramref name="her"/> turned about it by this attack's angle, drawn now.
        /// </summary>
        Vector3 RageAim(Vector3 her)
        {
            _rageAngle = 0f;
            if (!IsEnraged || PartRageRules.Degrees(Kind, PartRageValues.Live) <= 0f) return her;
            if (!_rageSeeded)
            {
                _rageSeeded = true;
                GeneratedLevel level = GetComponentInParent<GeneratedLevel>();
                bool onFloor = level != null && level.Layout != null;
                _rageStream = new BoneBreakRandom(PartRageRules.Seed(onFloor ? level.Layout.Seed : 0UL, onFloor ? level.Layout.Floor : 0, name,
                                                                     onFloor ? 0 : Id));
            }
            _rageAngle = PartRageRules.Angle(Kind, true, _rageStream.Signed(), PartRageValues.Live);
            GroundPoint me = Ground(transform.position);
            GroundPoint d = PartRageRules.Turn(Ground(her) - me, _rageAngle);
            return new Vector3(me.X + d.X, her.y, me.Z + d.Z);
        }

        /// <summary>The way it faces as a wind-up begins (work order 129): toward her, turned by this attack's angle (itself at 0).</summary>
        Vector3 RageFacing(Vector3 toPlayer)
        {
            if (_rageAngle == 0f) return toPlayer;
            GroundPoint d = PartRageRules.Turn(Ground(toPlayer), _rageAngle);
            return new Vector3(d.X, 0f, d.Z);
        }

        /// <summary>
        /// A way it looses or runs, turned by the attack's angle (work order 129): itself, exactly, for an attack begun calm or with
        /// the switch off. The archer, the caster, the warder and the lancer turn the way they would go today by it.
        /// </summary>
        protected GroundPoint RageTurn(GroundPoint dir) => _rageAngle == 0f ? dir : PartRageRules.Turn(dir, _rageAngle);
    }
}
