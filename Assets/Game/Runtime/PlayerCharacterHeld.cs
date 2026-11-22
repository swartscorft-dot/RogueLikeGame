using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// What is done to <see cref="PlayerCharacter"/>: stunned and pulled (work order 081 moved it here from PlayerCharacter.cs,
    /// code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- Stunned and pulled (work order 035, 2026-11-20; decision 0010) ----------------
        // Two things the melee kinds' blue skills do to her (MeleeEnemy): the brute's Shield bash stuns her, the guard's
        // Drag pulls her in. Stunned: for its time she cannot move, shoot, use a skill or dash; a press meanwhile waits in
        // the input buffer as in a lock and keeps only what the buffer allows at its end; it never stacks or chains
        // (HeldState: none while she is held, and none for HeldState.StunGrace after one ends). Pulled: she is moved along
        // the ground toward the puller on the NavMesh, stopping at walls and props as her own movement does, and cannot
        // act meanwhile; afterwards she acts at once. Neither starts while she is dead, a run's result is in, the game is
        // paused or a choice is open; both end at once on death, a restart and leaving a floor (Teleport, StartFresh).

        readonly HeldState _held = new HeldState();
        PlayerHealth _herHealth;

        /// <summary>True while a stun holds her (work order 035).</summary>
        public bool IsStunned => _held.IsStunned;
        /// <summary>True while a pull moves her (work order 035).</summary>
        public bool IsPulled => _held.IsPulled;
        /// <summary>True while she is stunned or pulled: she cannot act (work order 035).</summary>
        public bool IsHeld => _held.IsHeld;
        /// <summary>What holds her and since when; for the effects and tests.</summary>
        public HeldState HeldBy => _held;
        public int TimesStunned { get; private set; }
        public int TimesPulled { get; private set; }

        bool MayBeHeldNow
        {
            get
            {
                if (_herHealth == null) _herHealth = GetComponent<PlayerHealth>();
                GeneratedLevels levels = GeneratedLevels.Current;
                return MeleeYouReadRules.MayBeHeld(_herHealth == null || _herHealth.IsAlive, _herHealth != null && _herHealth.Untouchable,
                                                   levels != null && levels.Paused, levels != null && levels.Choice != GeneratedLevels.ChoiceKind.None);
            }
        }

        /// <summary>Stuns her for <paramref name="seconds"/> (work order 035), unless she may not be held now; <paramref name="by"/> names the attack. True if it did.</summary>
        public bool Stun(float seconds, string by = null)
        {
            if (!MayBeHeldNow || !_held.TryStun(Time.time, seconds)) return false;
            Interrupt();
            TimesStunned++;
            Report(new PlayerReport { Kind = PlayerReportKind.Stunned, Seconds = seconds, By = by });
            return true;
        }

        /// <summary>Pulls her to <paramref name="to"/> over <paramref name="seconds"/> (work order 035), unless she may not be held now. True if it did.</summary>
        public bool Pull(Vector3 to, float seconds, string by = null)
        {
            if (!MayBeHeldNow || !_held.TryPull(Time.time, GroundPosition, new GroundPoint(to.x, to.z), seconds)) return false;
            Interrupt();
            TimesPulled++;
            Report(new PlayerReport { Kind = PlayerReportKind.Pulled, Seconds = seconds, By = by });
            return true;
        }

        /// <summary>A stun or a pull takes her: what she was doing is dropped, as a teleport drops it, where she stands.</summary>
        void Interrupt()
        {
            if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.Cancelled, Time.time);
            _action.Cancel();
            _cast.Cancel();
            EndAction();
            // A recast chain whose shot (or opening move) is dropped before it went off would wait for a window that never
            // opens, and its key and every new skill with it (found merging work orders 035 and 038: the Ace stood so for
            // the rest of PilotTests' fight). It ends, as a teleport ends it; a window already open runs on, as before.
            if (_chain.SinceOpened(Time.time) < 0f) EndChain();
            _dashing = false;
            _buffer.Clear();
            DropWaitingClick();
            _shotFacing.End(turnBack: false);
            _easing = false;
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            EndKeyMove();
            if (_inCover) LeaveCover(CoverLeaving.Other);
            EndDuck(DuckEnd.Other); // and a quick duck (work order 043)
            _rim.Hide();
            Halt();
        }

        /// <summary>Each frame: a pull moves her along; true while she is still held, false once it is over (she acts at once).</summary>
        bool UpdateHeld(float now)
        {
            if (!_held.IsHeld) return false;
            if (_held.IsPulled)
            {
                GroundPoint p = _held.PullPoint(now);
                Vector3 step = Flat(new Vector3(p.X, 0f, p.Z) - transform.position);
                if (_agent.isOnNavMesh && step.sqrMagnitude > 1e-10f) _agent.Move(step); // on the NavMesh: it stops at walls and props
            }
            if (_held.Step(now) != HeldKind.None) return false;
            Halt();
            return true;
        }
    }
}
