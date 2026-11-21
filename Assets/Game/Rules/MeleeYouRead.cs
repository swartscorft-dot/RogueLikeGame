using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// What a melee attack does besides striking where the enemy stands (decision 0010; work order 035, 2026-11-20).
    /// Leap: the swarmer hops to where she was as it leaves the ground, in the last part of the windup, and bites on
    /// landing. Lunge: the brute runs at her along a line fixed as the lunge starts, in the last part of the windup, and
    /// strikes where it stops. Drag: as the windup ends the guard pulls her to a point in front of it.
    /// </summary>
    public enum MeleeMove { None, Leap, Lunge, Drag }

    /// <summary>
    /// The new attacks of the swarmer, the brute and the guard (decision 0010, Kristian 2026-11-20: "Melees bør ha vanlig
    /// auto attack, komme i range og angripe på lav cd. Ikke mye dmg, men kanskje ha en-to skills som gjør noe spesifikt for
    /// den typen de er"; work order 035). Each kind has an ordinary attack, in reach on a short cycle for little damage with
    /// a wind-up so short that its animation is the warning, and one or two skills on their own cooldowns. The order's
    /// first guesses, tuned so that an enemy left to attack her freely deals about what it dealt before, or up to a third
    /// more (work order 035, "Keeping it hard"): the bite 5 (6 asked), the stroke 13 (10 asked), the fist 24 (18 asked).
    /// The bite is 2, three times a second, since work order 039 (<see cref="Swarmer"/>).
    /// The attacks from before stay as they were, in the kinds' values, for the switch's off position
    /// (<see cref="MeleeYouReadRules"/>).
    /// </summary>
    public static class MeleeAttacks
    {
        public const string Bite = "Bite";
        public const string Leap = "Leap";
        public const string Stroke = "Stroke";
        public const string ShieldBash = "Shield bash";
        public const string Fist = "Fist";
        public const string Drag = "Drag";
        public const string Crush = "Crush";

        /// <summary>
        /// The swarmer: it bites as it clings, about three times a second for 2, never stopping to wind up (it keeps moving
        /// with her); and, only when it <see cref="SwarmerValues.Leaps"/> (off by default since work order 039), from 4 to
        /// 7 m it crouches and hops to her, aimed where she was as it leaves the ground, biting on landing.
        ///
        /// The bite was 5, twice a second (work order 035). Kristian's second session of 2026-11-20 had the Bite land 41
        /// of 47 times, since it lands whenever one is in reach as it chases; and he asked that the small ones "just run fast
        /// and hammer away when they are close" (work order 039): 2, three times a second. Standing still before one she
        /// takes about 60 in ten seconds at floor 5's damage scale; at floor 1's (0.7) a bite of 2 comes to 1, about 30.
        /// </summary>
        public static EnemyAttackDefinition[] Swarmer() => new[]
        {
            new EnemyAttackDefinition
            {
                Name = Bite, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = 1.6f,
                Windup = 0.11f, Recovery = 0.2f, Damage = 2, Length = 1.6f, Angle = 140f, Ordinary = true,
            },
            new EnemyAttackDefinition
            {
                // A crouch of 0.25 s, then the hop of 0.35 s: the windup is both, so the bite lands on the clip's strike.
                Name = Leap, Shape = EnemyAttackShape.Circle, MinDistance = 4f, MaxDistance = 7f,
                Windup = 0.6f, Recovery = 0.4f, Damage = 10, Radius = 1.2f, Cooldown = 4f,
                Move = MeleeMove.Leap, MoveSeconds = 0.35f,
            },
        };

        /// <summary>
        /// The brute (the sword and shield): a stroke in an arc in front of it about once a second; and from 3 to 6 m a
        /// Shield bash, blue (Stopping Shot counters it): the shield raised for 0.6 s, then a lunge of up to 5 m in 0.3 s
        /// along a line fixed as it starts, which stuns her for half a second when it lands. Nothing that reaches her from
        /// afar: its Shockwave is gone with the switch on.
        /// </summary>
        public static EnemyAttackDefinition[] Brute() => new[]
        {
            new EnemyAttackDefinition
            {
                Name = Stroke, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = 2.2f,
                // Damage 10 (13 before): the test pilot's flights of the night of 2026-11-20 (work order 046) had the
                // brute the first source of her damage (a third of it), its Stroke a sixth, the pilots dying on floor 2.
                Windup = 0.35f, Recovery = 0.75f, Damage = 10, Length = 2.2f, Angle = 100f, Ordinary = true,
            },
            new EnemyAttackDefinition
            {
                Name = ShieldBash, Shape = EnemyAttackShape.Circle, MinDistance = 3f, MaxDistance = 6f,
                // Cooldown 9 s (6 before): the same flights (work order 046) had the bash a fifth of her damage, and its
                // stun the start of most strokes that landed (two to seven stuns a run); Kristian's third session, ten
                // stuns in ten minutes.
                Windup = 0.9f, Recovery = 0.8f, Damage = 15, Radius = 1.3f, Length = 5f, HalfWidth = 0.6f,
                Counterable = true, Cooldown = 9f, Move = MeleeMove.Lunge, MoveSeconds = 0.3f, Stun = 0.5f,
            },
        };

        /// <summary>
        /// The guard: a heavy fist, slow; from 4 to 9 m, seeing her, a Drag, blue: an arm raised for 0.9 s, then she is
        /// pulled to 2 m in front of it over 0.3 s, unless a wall or a tall prop is between them or she is in the dash's
        /// protected part; and a Crush, both arms overhead for 1.1 s and then 40 in an arc in front, which it likes to
        /// follow a Drag with. Its stagger bar and its daze are as before.
        /// </summary>
        public static EnemyAttackDefinition[] Guard() => new[]
        {
            new EnemyAttackDefinition
            {
                Name = Fist, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = 2.6f,
                // Damage 18 (24 before; 18 is what work order 035 first asked): the test pilot's flights of the night of
                // 2026-11-20 (work order 046) had the guard's Fist land at every floor-2 death in a guard's room.
                Windup = 0.5f, Recovery = 1.3f, Damage = 18, Length = 2.6f, Angle = 90f, Ordinary = true,
            },
            new EnemyAttackDefinition
            {
                // Length: as far as she may have got from it when it goes off and still be pulled.
                Name = Drag, Shape = EnemyAttackShape.Line, MinDistance = 4f, MaxDistance = 9f,
                Windup = 0.9f, Recovery = 0.4f, Damage = 0, Length = 12f, HalfWidth = 0.5f,
                Counterable = true, Cooldown = 9f, Move = MeleeMove.Drag, MoveSeconds = 0.3f, PullTo = 2f,
            },
            new EnemyAttackDefinition
            {
                Name = Crush, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = 3.2f,
                Windup = 1.1f, Recovery = 1.2f, Damage = 40, Length = 3.2f, Angle = 70f, Cooldown = 6f, Follows = Drag,
            },
        };

        /// <summary>The set of a kind (the three melee kinds); null for any other.</summary>
        public static EnemyAttackDefinition[] For(EnemyKind kind) =>
            kind == EnemyKind.Swarmer ? Swarmer() : kind == EnemyKind.Brute ? Brute() : kind == EnemyKind.Guard ? Guard() : null;

        /// <summary>The attack of that name in a set, or null.</summary>
        public static EnemyAttackDefinition Find(EnemyAttackDefinition[] attacks, string name)
        {
            if (attacks == null) return null;
            for (int i = 0; i < attacks.Length; i++)
                if (attacks[i] != null && attacks[i].Name == name) return attacks[i];
            return null;
        }
    }

    /// <summary>
    /// Which attack a melee enemy starts, and when its skills are ready again (work order 035): first the skill that
    /// follows the last skill it began, if that was within <see cref="FollowWindow"/> (the guard's Crush after a Drag,
    /// its own cooldown or not); then any other skill whose band holds her and whose cooldown is over, in the set's order;
    /// then the ordinary attack, if she is in its reach. A cooldown runs from the start of the windup. Times are game
    /// seconds; it never reads a clock. <paramref name="allows"/> in <see cref="Choose"/> is asked last, only of an attack
    /// that would otherwise be chosen: whether the enemy sees her (the Drag), and whether it has a turn (the skills).
    /// </summary>
    public sealed class MeleeTactics
    {
        /// <summary>s after a skill began within which the attack that follows it is chosen first.</summary>
        public float FollowWindow = 3f;

        readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
        string _last;
        float _lastAt = float.NegativeInfinity;

        /// <summary>When the attack may start again; minus infinity when it has no cooldown running.</summary>
        public float ReadyAt(EnemyAttackDefinition attack) =>
            attack != null && _readyAt.TryGetValue(attack.Name, out float at) ? at : float.NegativeInfinity;

        public bool IsReady(EnemyAttackDefinition attack, float now) => now >= ReadyAt(attack);

        /// <summary>The name of the last skill it began, or null.</summary>
        public string LastSkill => _last;

        static bool InBand(EnemyAttackDefinition a, float distance) => distance >= a.MinDistance && distance <= a.MaxDistance;

        public EnemyAttackDefinition Choose(float now, float distance, EnemyAttackDefinition[] attacks, Func<EnemyAttackDefinition, bool> allows = null)
        {
            if (attacks == null) return null;
            if (_last != null && now - _lastAt <= FollowWindow)
                for (int i = 0; i < attacks.Length; i++)
                {
                    EnemyAttackDefinition a = attacks[i];
                    if (a == null || string.IsNullOrEmpty(a.Follows) || a.Follows != _last || !InBand(a, distance)) continue;
                    if (allows == null || allows(a)) return a;
                }
            for (int i = 0; i < attacks.Length; i++)
            {
                EnemyAttackDefinition a = attacks[i];
                if (a == null || a.Ordinary || !InBand(a, distance) || !IsReady(a, now)) continue;
                if (allows == null || allows(a)) return a;
            }
            for (int i = 0; i < attacks.Length; i++)
            {
                EnemyAttackDefinition a = attacks[i];
                if (a == null || !a.Ordinary || !InBand(a, distance)) continue;
                if (allows == null || allows(a)) return a;
            }
            return null;
        }

        /// <summary>Its windup began now: its cooldown runs from here, and a skill is the one the next may follow.</summary>
        public void Began(EnemyAttackDefinition attack, float now)
        {
            if (attack == null) return;
            if (attack.Cooldown > 0f) _readyAt[attack.Name] = now + attack.Cooldown;
            if (attack.Ordinary) return;
            _last = attack.Name;
            _lastAt = now;
        }

        /// <summary>A fresh fight: every skill ready, nothing to follow.</summary>
        public void Clear()
        {
            _readyAt.Clear();
            _last = null;
            _lastAt = float.NegativeInfinity;
        }
    }

    /// <summary>The rules of melee you read (decision 0010; work order 035, 2026-11-20), with no engine in them.</summary>
    public static class MeleeYouReadRules
    {
        /// <summary>m short of where she stood that a leap lands, so that it lands in front of her, not on her.</summary>
        public const float LeapShort = 0.7f;

        /// <summary>The glint on an enemy is blue for an attack Stopping Shot can counter, red for one that only hurts.</summary>
        public static bool GlintIsBlue(EnemyAttackDefinition attack) => PoolRules.IsCounterable(attack);

        /// <summary>A skill's glint grows through its windup and snaps at the strike; an ordinary attack's is one small flick.</summary>
        public static bool GlintGrows(EnemyAttackDefinition attack) => attack != null && !attack.Ordinary;

        /// <summary>
        /// s: the least time between two flicks on one enemy (work order 039). The swarmer's bite comes three times a second,
        /// and eight swarmers on her flicking at every bite would strobe: a flick shows on every bite of an attack whose cycle
        /// is this long or longer, and on every n-th of a faster one (every other bite).
        /// </summary>
        public const float FlickGap = 0.5f;

        /// <summary>Is it an ordinary attack faster than <see cref="FlickGap"/> a cycle (the swarmer's bite)?</summary>
        public static bool IsRapid(EnemyAttackDefinition attack) =>
            attack != null && attack.Ordinary && attack.Windup + attack.Recovery < FlickGap - 1e-4f;

        /// <summary>The flick shows on every this-many-th of an attack: 1 for a skill and an attack at least <see cref="FlickGap"/> a cycle.</summary>
        public static int FlickEvery(EnemyAttackDefinition attack)
        {
            if (!IsRapid(attack)) return 1;
            float cycle = Math.Max(1e-3f, attack.Windup + attack.Recovery);
            return Math.Max(1, (int)Math.Ceiling(FlickGap / cycle - 1e-4f));
        }

        /// <summary>Does the flick of the <paramref name="number"/>-th (from 1) ordinary attack an enemy begins show? The first always does.</summary>
        public static bool FlickShows(EnemyAttackDefinition attack, int number) => number <= 1 || (number - 1) % FlickEvery(attack) == 0;

        /// <summary>Where a leap from <paramref name="from"/> lands: <paramref name="shortBy"/> short of where she stands as it leaves the ground.</summary>
        public static GroundPoint LeapLanding(GroundPoint from, GroundPoint her, float shortBy = LeapShort)
        {
            GroundPoint d = her - from;
            float length = d.Length;
            if (length <= shortBy || length < 1e-4f) return from;
            return from + d * ((length - shortBy) / length);
        }

        /// <summary>
        /// How far a lunge runs: to <paramref name="shortBy"/> short of where she stands as it starts, never more than
        /// <paramref name="most"/>, and no farther than the way is clear (<paramref name="clear"/>, a wall or the end of the
        /// walkable ground). Never less than nothing.
        /// </summary>
        public static float LungeLength(float toHer, float most, float shortBy, float clear) =>
            Math.Max(0f, Math.Min(Math.Min(toHer - shortBy, most), clear));

        /// <summary>Where a pull takes her: <paramref name="pullTo"/> in front of the puller, toward where she is; where she is when she is nearer already.</summary>
        public static GroundPoint PullTarget(GroundPoint puller, GroundPoint her, float pullTo)
        {
            GroundPoint d = her - puller;
            float length = d.Length;
            if (length <= pullTo || length < 1e-4f) return her;
            return puller + d * (pullTo / length);
        }

        /// <summary>A Drag that goes off is broken by a wall or a tall prop between them at that moment, or by the dash's protected part.</summary>
        public static bool DragBreaks(bool lineClear, bool inDashProtection) => !lineClear || inDashProtection;

        /// <summary>
        /// May she be stunned or pulled now? Never while she is dead or a run's result is in, the game is paused, or a
        /// choice is open.
        /// </summary>
        public static bool MayBeHeld(bool alive, bool untouchable, bool paused, bool choiceOpen) => alive && !untouchable && !paused && !choiceOpen;
    }

    /// <summary>What holds her: nothing, a stun, or a pull (work order 035).</summary>
    public enum HeldKind { None, Stunned, Pulled }

    /// <summary>
    /// Stunned and pulled, for her (work order 035, 2026-11-20; decision 0010, open question 1: "is being stunned, even for
    /// half a second, acceptable in a game about tempo?"). Stunned: for its time she cannot move, shoot, use a skill or
    /// dash; it never stacks or chains: none starts while she is held, and after a stun ends none starts for
    /// <see cref="StunGrace"/>. Pulled: she is moved along the ground from where she was to a point over its time and
    /// cannot act meanwhile; none starts while she is held. Both end at their time (<see cref="Step"/>), or at once
    /// (<see cref="Clear"/>: death, a restart, leaving a floor). Times are game seconds; it never reads a clock.
    /// </summary>
    public sealed class HeldState
    {
        /// <summary>s after a stun ends in which no stun may start.</summary>
        public const float StunGrace = 1.5f;

        float _grace = StunGrace;
        float _stunFreeAt = float.NegativeInfinity;

        public HeldKind Kind { get; private set; }
        public float Since { get; private set; }
        public float Until { get; private set; }
        /// <summary>A pull's start and end on the ground.</summary>
        public GroundPoint From { get; private set; }
        public GroundPoint To { get; private set; }
        /// <summary>The earliest a new stun may start.</summary>
        public float StunFreeAt => _stunFreeAt;

        public bool IsHeld => Kind != HeldKind.None;
        public bool IsStunned => Kind == HeldKind.Stunned;
        public bool IsPulled => Kind == HeldKind.Pulled;

        public bool TryStun(float now, float seconds, float grace = StunGrace)
        {
            if (seconds <= 0f || Kind != HeldKind.None || now < _stunFreeAt) return false;
            Kind = HeldKind.Stunned;
            Since = now;
            Until = now + seconds;
            _grace = Math.Max(0f, grace);
            return true;
        }

        public bool TryPull(float now, GroundPoint from, GroundPoint to, float seconds)
        {
            if (seconds <= 0f || Kind != HeldKind.None) return false;
            Kind = HeldKind.Pulled;
            Since = now;
            Until = now + seconds;
            From = from;
            To = to;
            return true;
        }

        /// <summary>0 to 1 through what holds her; 1 when nothing does.</summary>
        public float Progress(float now)
        {
            if (Kind == HeldKind.None || Until <= Since) return 1f;
            float p = (now - Since) / (Until - Since);
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        /// <summary>Where a pull has her now: from its start to its end, evenly over its time.</summary>
        public GroundPoint PullPoint(float now) => From + (To - From) * Progress(now);

        /// <summary>Advances to now: what ended at this step (its time is up), or None.</summary>
        public HeldKind Step(float now)
        {
            if (Kind == HeldKind.None || now < Until) return HeldKind.None;
            HeldKind ended = Kind;
            if (ended == HeldKind.Stunned) _stunFreeAt = Until + _grace;
            Kind = HeldKind.None;
            return ended;
        }

        /// <summary>Ends whatever holds her at once, and forgets the stun's grace: death, a restart, leaving a floor.</summary>
        public void Clear()
        {
            Kind = HeldKind.None;
            _stunFreeAt = float.NegativeInfinity;
        }
    }
}
