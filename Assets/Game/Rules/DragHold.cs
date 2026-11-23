using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// One guard drags at a time (work order 093; Kristian 2026-11-23, of floor 3's rooms of up to three guards: "Én drar om
    /// gangen"). She is one target, so there is one hold for the whole level. From the moment a guard's Drag is committed
    /// (its wind-up begins, <see cref="Began"/>) until <see cref="Gap"/> seconds after that Drag has ended
    /// (<see cref="Ended"/>: it landed and her pull is over, it missed, it was broken by cover or the dash, or the guard was
    /// staggered out of it or died), no OTHER guard may begin a Drag (<see cref="MayBegin"/>). The guard that holds it is not
    /// held back by it: its own cooldown (9 s from its wind-up's start) already keeps it from dragging sooner. A Drag that is
    /// never said to have ended ends at the latest its wind-up and its pull could last, given at <see cref="Began"/>.
    /// The first guard to commit takes the hold: guards are stepped one after another, and each asks and commits in its own
    /// step, so two can never begin together on the frame the hold ends. <see cref="Gap"/> 0 switches it off: every guard
    /// may drag whenever its own cooldown says, as before. Times are game seconds; it never reads a clock, and it never
    /// allocates.
    /// </summary>
    public sealed class DragHold
    {
        /// <summary>s after a Drag has ended before another guard may begin one; 0: the rule off (<see cref="MeleeAttacks.DragOneAtATime"/>).</summary>
        public float Gap = MeleeAttacks.DragOneAtATime;

        int _holder;
        bool _running;
        float _endsAt = float.NegativeInfinity;

        /// <summary>The enemy whose Drag is the last one committed (its id), or 0 when there is none since the hold was cleared.</summary>
        public int Holder => _holder;

        /// <summary>
        /// The Drags committed since the game started: each new hold has its own number, so a guard held back can be noted
        /// once per hold, not every frame. Never reset.
        /// </summary>
        public int Number { get; private set; }

        /// <summary>True while the holder's Drag is under way (its wind-up or her pull): it has not been said to have ended, and its latest end is not past.</summary>
        public bool Running(float now) => _holder != 0 && _running && now < _endsAt;

        /// <summary>When the last Drag ended, or will end at the latest while it runs; minus infinity with none.</summary>
        public float EndsAt => _holder != 0 ? _endsAt : float.NegativeInfinity;

        /// <summary>The earliest another guard may begin a Drag; minus infinity when any may now (none held, or the rule off).</summary>
        public float FreeAt => Gap <= 0f || _holder == 0 ? float.NegativeInfinity : (float)(_endsAt + Gap); // as a float: Unity's runtime adds floats wider than a float is, and a time that IS the sum must compare equal to it

        /// <summary>May enemy <paramref name="id"/> begin a Drag now? Always with the rule off, and always for the holder itself.</summary>
        public bool MayBegin(int id, float now) => Gap <= 0f || _holder == 0 || id == _holder || (!Running(now) && now >= FreeAt);

        /// <summary>
        /// May enemy <paramref name="id"/> begin this attack now, as far as the hold goes? Every attack but a Drag always; a
        /// Drag as <see cref="MayBegin"/> says. Asked by a melee enemy's choice (<see cref="MeleeTactics.Choose"/>'s allows):
        /// refused, its choice goes on to its other attacks by the same rules.
        /// </summary>
        public bool Allows(int id, EnemyAttackDefinition attack, float now) =>
            attack == null || attack.Move != MeleeMove.Drag || MayBegin(id, now);

        /// <summary>
        /// Enemy <paramref name="id"/> committed a Drag now (its wind-up began): it holds the hold until the Drag ends, and
        /// for <see cref="Gap"/> after. <paramref name="longest"/>: the most the Drag can last from now (its wind-up and its
        /// pull), when it ends at the latest if nothing says so sooner.
        /// </summary>
        public void Began(int id, float now, float longest)
        {
            _holder = id;
            _running = true;
            _endsAt = now + Math.Max(0f, longest);
            Number++;
        }

        /// <summary>
        /// Enemy <paramref name="id"/>'s Drag ended at <paramref name="at"/> (its strike's moment, or the end of her pull when
        /// it landed; the moment it was dropped). Nothing when it is not the holder's, or it ended already; never later than
        /// the latest end given at <see cref="Began"/>.
        /// </summary>
        public void Ended(int id, float at)
        {
            if (id == 0 || id != _holder || !_running) return;
            _running = false;
            if (at < _endsAt) _endsAt = at;
        }

        /// <summary>Enemy <paramref name="id"/> is gone (taken out of the game with its level): the hold it held goes with it.</summary>
        public void Forget(int id)
        {
            if (id != 0 && id == _holder) Clear();
        }

        /// <summary>Forgets the hold: a new floor, her death, a new run. <see cref="Number"/> runs on.</summary>
        public void Clear()
        {
            _holder = 0;
            _running = false;
            _endsAt = float.NegativeInfinity;
        }
    }
}
