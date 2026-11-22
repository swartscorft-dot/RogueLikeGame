using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Movement and the dash of <see cref="PlayerCharacter"/>: the move keys, the walk, the speed eased round shots and
    /// skills, the dash and its charges, and facing (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// The move keys: walk this way (any length; it is laid flat and made of length 1), or stand with zero. Given
        /// every frame by the input; it holds until it is changed. It never cancels a shot or drops a waiting click.
        /// </summary>
        public void Steer(Vector3 direction)
        {
            Vector3 flat = Flat(direction);
            _moveInput = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.zero;
        }

        /// <summary>
        /// The move keys as the input gives them (the third review of 2026-11-20, work order 047): <see cref="Steer(Vector3)"/>,
        /// and whether a key went down this frame (<see cref="QuickDuckRules.MoveKeyWentDown"/>). A quick duck ends on a key
        /// going down, never on one let go; steered by <see cref="Steer(Vector3)"/> alone (tests, tools), on a direction not
        /// held at the tap, as before.
        /// </summary>
        public void Steer(Vector3 direction, bool keyWentDown)
        {
            Steer(direction);
            _steeredByKeys = true;
            _moveKeyWentDown |= keyWentDown;
        }

        /// <summary>Let go of the move keys: she stands. A shot or dash in progress still finishes. For tests and tools.</summary>
        public void Stop()
        {
            _moveInput = Vector3.zero;
            if (!IsLocked) Halt();
        }

        /// <summary>
        /// Press: dash toward a point. Cancels a shot or skill in progress.
        /// Rejected while on cooldown or already dashing.
        /// </summary>
        public IntentResult DashToward(Vector3 point)
        {
            RefreshDashCharges(); // work order 029: Spare Step; work order 065: a second dash
            if (_dashing || !_dash.CanStart(Time.time))
                return DashPressed(IntentResult.Rejected, _dashing ? 0f : _dash.CooldownRemaining(Time.time));
            if (_held.IsHeld) return DashPressed(BufferPress(PressKind.Dash, point), 0f); // stunned or pulled: it waits, as in a lock (work order 035)

            DropWaitingClick(); // a dash drops a waiting click
            _shotFacing.End(turnBack: false); // and ends the backpedal at once: the dash faces its own way
            if (_actionKind != ActionKind.None)
            {
                if (_actionKind == ActionKind.Skill && !Feel.DashCancelsSkill)
                    return DashPressed(BufferPress(PressKind.Dash, point), 0f);
                if (_actionKind == ActionKind.Skill) ReportSkillEnded(SkillEnding.CancelledByDash, Time.time);
                _action.Cancel();
                _cast.Cancel();
                EndAction();
            }
            _easing = false; // a dash ends the slow of a shot on the move at once
            DropCooldownPress(); // work order 066: a newer press replaces one waiting for its cooldown
            return DashPressed(StartDash(point) ? IntentResult.Started : IntentResult.Rejected, 0f);
        }

        /// <summary>Reports what became of a dash press, and returns it.</summary>
        IntentResult DashPressed(IntentResult result, float cooldownLeft)
        {
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.DashPressed, Result = result, CooldownLeft = cooldownLeft,
                AlreadyDashing = result == IntentResult.Rejected && _dashing,
            });
            return result;
        }

        /// <summary>
        /// The dashes she holds, read again (work order 029's Spare Step; work order 065: her Second dash, <see cref="HerStats.DashCharges"/>):
        /// at every press, when a run starts and when a chest's upgrade is taken. A change makes every charge ready.
        /// </summary>
        public void RefreshDashCharges() => _dash.MaxCharges = Modifiers != null ? Modifiers.Stats.DashCharges : 1;

        /// <summary>Dashes ready now (work order 029: two with Spare Step); 1 or 0 otherwise.</summary>
        public int DashChargesReady => _dash.ChargesReady(Time.time);
        /// <summary>The dashes the dash holds now (work order 029).</summary>
        public int DashMaxCharges => _dash.MaxCharges;

        /// <summary>
        /// Moves her the held way at the speed the feel allows now, by steering the NavMesh agent a little way ahead:
        /// she stays on the NavMesh and slides along its edges. With no direction held she stops at once.
        /// </summary>
        void Walk()
        {
            if (!_agent.isOnNavMesh) return;
            if (_inCover)
            {
                CoverSlide();
                return;
            }
            if (_duck.IsDown)
            {
                // Down in a quick duck nothing moves her (work order 043); a move key not held at the tap ends the duck first.
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            if (_moveInput == Vector3.zero)
            {
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            MoveAlong(_moveInput, _agent.speed);
        }

        /// <summary>Moves her along a flat direction of length 1 at this speed, steering the agent a little way ahead.</summary>
        void MoveAlong(Vector3 direction, float speed)
        {
            if (!_agent.isOnNavMesh) return;
            // While Slide moves her (work order 015) nothing else does: no walk, no slow of a shot, no skill's slide.
            if (_keyMoving)
            {
                Halt();
                return;
            }
            _agent.isStopped = false;
            _agent.SetDestination(transform.position + direction * SteerLookAhead);
            // Start at once, as the movement always has: the agent would otherwise wait a frame for its path.
            _agent.velocity = direction * speed;
        }

        /// <summary>
        /// The body's facing, every frame she is free or in a basic shot. Normally it faces the way she moves, at the
        /// movement turn time. Around a shot that had to turn the body, <see cref="ShotFacing"/> holds it toward the
        /// live aim, then turns it back to the run, both at the shot turn rate, so she backpedals while she keeps
        /// shooting instead of flipping round and back on every shot.
        /// </summary>
        void UpdateFacing(FeelValues feel)
        {
            // Her run direction is the way she moves this frame: the held direction.
            bool moving = IsMoving;
            if (moving) _runDirection = _moveInput;
            Vector3 aim = Flat(AimEnd(_shotCursor) - transform.position);
            bool aimInsideRunFront = moving && !UpperBodyAim.BodyMustTurn(
                new GroundPoint(_runDirection.x, _runDirection.z), new GroundPoint(aim.x, aim.z), feel.ShotFrontAngle);
            bool facingRun = Vector3.Angle(Flat(transform.forward), _runDirection) < 1f;
            switch (_shotFacing.Update(Time.time, moving, aimInsideRunFront, facingRun))
            {
                case ShotFacing.Mode.Held:
                    TurnAtShotRate(aim, feel);
                    break;
                case ShotFacing.Mode.TurningBack:
                    TurnAtShotRate(_runDirection, feel);
                    break;
                default:
                    FaceMovement(feel);
                    break;
            }
        }

        /// <summary>Turns the body toward a direction at the shot turn rate (<see cref="FeelValues.ShotTurnTime"/> for a half turn).</summary>
        void TurnAtShotRate(Vector3 flatDir, FeelValues feel)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            float yaw = transform.eulerAngles.y;
            float want = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, ShotFacing.Turn(yaw, want, feel.ShotTurnTime, Time.deltaTime), 0f);
        }

        /// <summary>
        /// True when the buffer holds a press that may start now and will: a dash at once, a skill once no free
        /// recovery holds it back, and either only while it is still good. A press is good while it is no older than
        /// the buffer time; a skill press that waited out a free recovery is good if it was made no earlier than the
        /// buffer time before the skill let her go, however long ago that was (<see cref="SkillGate"/>, decision 0004
        /// amended 2026-11-19). A press that is due but not good is dropped here, so it never cancels or holds back
        /// anything (a shot winding up, a waiting click, the move keys).
        /// </summary>
        bool PressReady(float now)
        {
            if (!_buffer.TryPeek(out Press waiting)) return false;
            if (_duck.IsDown) return false; // waits out a quick duck, and is judged when she rises as at the end of a lock (work order 043)
            // Work order 069: nor a recast press whose window has opened: it fires the instant it opens, as a press in the window
            // does (PressSkill), not when the shot before's free recovery would have ended (0.1 s later for Triple Tap until then).
            if (waiting.Kind == PressKind.Skill && now < _skillGateUntil && !CutsRecovery(waiting.Slot) && !RecastWindowOpenFor(waiting.Slot)) return false; // waits out a free recovery (not Backstep's hop: work order 064)
            FeelValues feel = Feel;
            // Work order 064: a press that waits for its cooldown waits while it is no older than the cooldown buffer (and the
            // ordinary buffer: a guard), and once the cooldown has ended it goes, judged good for the ordinary buffer from
            // there. Work order 066: for every skill, and judged as a press made the moment the skill became ready.
            bool waitedForCooldown = waiting.Kind == PressKind.Skill && waiting.ForCooldown;
            if (waitedForCooldown && !CooldownFor(waiting.Slot).IsReady(now))
            {
                if (CooldownBufferRules.StillInTime(waiting.At, now, Kit.CooldownBuffer, feel.InputBuffer)) return false; // it waits
                _buffer.Clear(); // a cooldown that did not end in time (none does: a guard)
                Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = waiting.Slot });
                return false;
            }
            // A gate is set only by a free recovery, and a new skill, a dash or a teleport clears it.
            bool afterFreeRecovery = waiting.Kind == PressKind.Skill && feel.SkillRecoveryFree
                                     && _skillGateUntil != float.NegativeInfinity;
            // A press made while she was down counts from the moment she rose (the third review of 2026-11-20, work order
            // 047): kept through the duck, then the ordinary buffer from there, as a click waits and fires.
            float pressedAt = waiting.At >= _duck.DownAt && waiting.At <= _duck.RoseAt ? _duck.RoseAt : waiting.At;
            if (waitedForCooldown) pressedAt = CooldownBufferRules.CountsFrom(pressedAt, waiting.ReadyAt); // work order 066
            if (SkillGate.PressStillGood(pressedAt, now, feel.InputBuffer, afterFreeRecovery, _gateLetGoAt)) return true;
            _buffer.Clear(); // not good: dropped
            Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = waiting.Kind == PressKind.Skill ? waiting.Slot : -1 });
            return false;
        }

        /// <summary>
        /// One frame of a skill's slide, at the share of run speed the slide allows now (ApplyMovementFeel has set it),
        /// on the NavMesh. At a stand the slide is over. The way is the held move direction, changed at once as free
        /// movement does; with no move key held, the way she last slid (decision 0004, amended 2026-11-19: Kristian
        /// ruled that the move keys steer the slide). Steering never changes the speed: an opposite key turns the slide
        /// round at the speed it has. With <see cref="FeelValues.SlideSteering"/> off, the way she was moving at the press.
        /// </summary>
        void Slide(FeelValues feel, float now)
        {
            if (SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, feel.SkillSlowdown) <= 0f)
            {
                _sliding = false;
                Halt();
                return;
            }
            if (feel.SlideSteering && _moveInput != Vector3.zero) _slideDir = _moveInput;
            MoveAlong(_slideDir, _agent.speed);
        }

        /// <summary>
        /// A skill lets her go (at its last hit with the free recovery, else at the end of its recovery): the speed she
        /// may move at eases back up from what the slide has left. A skill that moved her itself leaves her free at once.
        /// </summary>
        void LetGo(SkillDefinition skill, float now)
        {
            // A light skill never held her back, so there is nothing to ease back up from (decision 0005).
            if (skill != null && skill.Lock == SkillLock.Light)
            {
                _sliding = false;
                return;
            }
            float share = _sliding ? SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, Feel.SkillSlowdown) : 0f;
            _sliding = false;
            _rising = skill == null || !_castMoves;
            _riseFrom = share;
            _riseStartedAt = now;
            _riseOfChain = skill != null && skill.RecastCount > 1; // work order 040: a recast chain's shot rises quicker (OffTheMark)
        }

        // Work order 040 (Kristian's second session of 2026-11-20: quicker off the mark after shooting). The three numbers
        // that hold her back after a shot are read through OffTheMark: with FeelValues.QuickOffTheMark on, the quicker ones
        // beside them; off, as before.
        bool _riseOfChain; // the rise running is after a recast chain's shot

        void ApplyMovementFeel(FeelValues feel)
        {
            if (Time.timeScale > 0f) _lastVelocity = _agent.velocity;
            if (_rising && SkillSlide.IsRisen(_riseFrom, Time.time - _riseStartedAt, OffTheMark.SkillSpeedRecovery(feel, _riseOfChain))) _rising = false;
            // A shot on the move that was cancelled before it went off ends the slow at once.
            if (_easing && _easeFiredAfter < 0f && !_movingShot) _easing = false;
            if (_easing && MovingShot.IsOver(Time.time - _easeStartedAt, _easeFiredAfter, feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel)))
                _easing = false;
            // The backpedal cap: held turned away from her movement by a shot, she moves at no more than a share of
            // run speed, eased in and out (decision 0003, a guess).
            _backpedalCap.Update(_shotFacing.State == ShotFacing.Mode.Held && IsMoving, Time.time,
                                 Modifiers != null ? Modifiers.Weapon.BackpedalShare(feel.BackpedalSpeedShare) : feel.BackpedalSpeedShare, // work order 060: a grip
                                 feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel));
            _agent.speed = feel.RunSpeed * RunSpeedShare * CurrentSpeedShare(feel);
            // The agent does not slow down by itself when its speed limit drops below how fast it is going.
            if (_agent.velocity.sqrMagnitude > _agent.speed * _agent.speed && !_dashing)
                _agent.velocity = _agent.velocity.normalized * _agent.speed;
            _agent.acceleration = feel.TimeToFullSpeed > 0f ? feel.RunSpeed / feel.TimeToFullSpeed : 10000f;
        }

        // ---------------- Movement ----------------

        /// <summary>True when an aim direction lies outside the front angle, so a basic shot along it turns the whole body.</summary>
        bool BodyMustTurnToward(Vector3 direction, FeelValues feel)
        {
            Vector3 facing = Flat(transform.forward);
            return UpperBodyAim.BodyMustTurn(new GroundPoint(facing.x, facing.z), new GroundPoint(direction.x, direction.z), feel.ShotFrontAngle);
        }

        /// <summary>
        /// Where a basic shot aimed at the cursor point ends: the full range out, along the line toward the point,
        /// or along where she faces when the point is on her.
        /// </summary>
        Vector3 AimEnd(Vector3 cursor)
        {
            Vector3 facing = Flat(transform.forward);
            GroundPoint dir = LineShot.Direction(GroundPosition, new GroundPoint(cursor.x, cursor.z), new GroundPoint(facing.x, facing.z));
            return transform.position + new Vector3(dir.X, 0f, dir.Z) * BasicShotRange;
        }

        void Halt()
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        // ---------------- Dash ----------------

        bool StartDash(Vector3 point)
        {
            FeelValues feel = Feel;
            if (!_dash.TryStart(Time.time, feel, Modifiers != null ? Modifiers.DashCooldownShare : 1f)) return false; // work order 017: the chests' step

            Vector3 dir = Flat(point - transform.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Flat(transform.forward);
            dir.Normalize();

            LeaveCover(CoverLeaving.Dash); // the dash leaves cover at once (Kristian 2026-11-19, an experiment)
            EndDuck(DuckEnd.Dashed); // and ends a quick duck at once (work order 043)
            _rim.Hide();
            Halt();
            // A dash ends a skill's slide and the ease back up after one at once, and a free recovery with them:
            // a dash cancels a skill, recovery and all.
            _sliding = false;
            _rising = false;
            _skillGateUntil = float.NegativeInfinity;
            EndChain(); // and a recast chain (decision 0005)
            EndKeyMove(); // and Slide (work order 015): the dash takes over
            _dashFrom = transform.position;
            _dashTo = _dashFrom + dir * feel.DashDistance;
            _dashing = true;
            DashesStarted++;
            Face(dir);
            Report(new PlayerReport { Kind = PlayerReportKind.DashStarted });
            return true;
        }

        /// <summary>
        /// The share of run speed it may walk at now: 1, or the lowest of what limits it. Each limit eases without a
        /// jump (a shot on the move, the backpedal cap, a skill's slide, the ease back up after a skill), so the lowest
        /// of them does too, whichever follows which.
        /// </summary>
        float CurrentSpeedShare(FeelValues feel)
        {
            float now = Time.time;
            float share = Mathf.Min(_backpedalCap.Share,
                                    _easing
                                        ? MovingShot.SpeedShare(_easeFrom, now - _easeStartedAt, _easeFiredAfter,
                                                                Modifiers != null ? Modifiers.Weapon.MovingShare(feel.MovingShotSpeedShare) : feel.MovingShotSpeedShare, // work order 060: a grip
                                                                feel.MovingShotSlowdown, OffTheMark.MovingShotRecovery(feel)) // work order 040
                                        : 1f);
            if (_rising) share = Mathf.Min(share, SkillSlide.RiseShare(_riseFrom, now - _riseStartedAt, OffTheMark.SkillSpeedRecovery(feel, _riseOfChain)));
            if (IsSkillSliding) share = Mathf.Min(share, SkillSlide.SlideShare(_slideFrom, now - _slideStartedAt, feel.SkillSlowdown));
            return share;
        }

        /// <summary>Starts the ease around a shot on the move: down from the speed she has now (full, or wherever a chained shot left it).</summary>
        void StartEasing(FeelValues feel) => StartEasing(feel, CurrentSpeedShare(feel));

        /// <summary>Starts the ease around a shot on the move from a given share of run speed.</summary>
        void StartEasing(FeelValues feel, float fromShare)
        {
            _easeFrom = fromShare;
            _easeStartedAt = Time.time;
            _easeFiredAfter = _actionKind == ActionKind.BasicShot && _action.HitResolved ? 0f : -1f;
            _easing = true;
        }

        /// <summary>
        /// The share of run speed she actually moves at, 0 to 1. During a hit stop the agent reads standing, so the last
        /// frame in which time ran is used, as for a skill's slide.
        /// </summary>
        float ActualSpeedShare(FeelValues feel)
        {
            Vector3 velocity = Flat(Time.timeScale > 0f ? _agent.velocity : _lastVelocity);
            return feel.RunSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / feel.RunSpeed) : 0f;
        }

        // ---------------- Facing ----------------

        void FaceMovement(FeelValues feel)
        {
            Vector3 v = IsMoving ? _moveInput : Flat(_agent.velocity);
            if (v.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(v.normalized, Vector3.up);
            transform.rotation = feel.TurnTime <= 0f
                ? want
                : Quaternion.RotateTowards(transform.rotation, want, 180f / feel.TurnTime * Time.deltaTime);
        }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
