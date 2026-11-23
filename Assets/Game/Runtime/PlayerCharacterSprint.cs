using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The sprint of <see cref="PlayerCharacter"/> (work order 092; Kristian 2026-11-23: a sprint on Shift, no shooting meanwhile, a
    /// jump over the blue props, a small stagger when hit; the rules and their numbers in <see cref="SprintRules"/> and
    /// <see cref="FeelValues"/>).
    ///
    /// - The key (<see cref="HoldSprint"/>) is given every frame by the input, as the cover key is. Held with a move key, and she is
    ///   free to run, she sprints (<see cref="IsSprinting"/>): her speed is raised by <see cref="FeelValues.SprintSpeedShare"/> over
    ///   <see cref="FeelValues.SprintRampTime"/>, and drops back at once when it ends. A basic shot that has not gone off yet when the
    ///   key goes down goes off first (a few hundredths of a second); one that has gone off has its recovery cut. When it starts, a
    ///   waiting click and a waiting press are dropped, the slow of a shot on the move and the turn to its aim end: she runs and turns
    ///   freely.
    /// - While she sprints or is in a jump a click or a skill key does nothing and is not kept (<see cref="RefusedWhileSprinting"/>).
    ///   The key up, she acts at once: nothing is left to wind down (a shot pressed then winds up as any shot does).
    /// - The jump: each frame she sprints, a low prop her way meets within the look ahead, with free floor beyond, is crossed in a
    ///   straight line at her sprint speed (<see cref="SprintRules.Decide"/>). In the air she cannot act or turn, the dash key does
    ///   nothing, and it cannot be cancelled; the NavMesh agent waits at the take-off (it does not move her) and is put down at the
    ///   landing. She is where she is in the air: a thing that flies at her, or a blow on the ground under her, meets her as always.
    ///   A stun or a pull that takes her in the air takes her at the landing.
    /// - A hit that hurt her while she sprints or jumps (<see cref="TookHit"/>) staggers her: the stun of work order 035 for
    ///   <see cref="FeelValues.SprintStagger"/>, named <see cref="SprintRules.StaggerName"/>, with its telling (the stars over her,
    ///   the stun's sound and word) and no grace after it; in the air, at the landing.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        bool _sprintHeld;                                // the key, as the input gives it every frame
        bool _sprintSpan;                                // a sprint (or a jump in one) was going at the last look
        float _sprintStartedAt = float.NegativeInfinity;
        bool _jumping;
        Vector3 _jumpFrom, _jumpTo;
        float _jumpStartedAt, _jumpSeconds, _jumpSpeed;
        bool _staggerOnLanding;
        // What took her in the air, at the landing (a stun of an enemy, a pull): kept, and done then.
        bool _airStun, _airPull;
        float _airStunSeconds, _airStunGrace, _airPullSeconds;
        string _airStunBy, _airPullBy;
        Vector3 _airPullTo;
        readonly List<JumpShape> _jumpShapes = new List<JumpShape>();
        readonly List<Shelter> _jumpShelters = new List<Shelter>();
        readonly List<TargetInfo> _jumpEnemies = new List<TargetInfo>();
        Func<GroundPoint, bool> _walkable;

        /// <summary>Metres the NavMesh is looked for under a landing, and how far from it the NavMesh's point may lie.</summary>
        const float LandingSample = 0.3f, LandingOff = 0.15f;

        // ---------------- For the readout and the tests ----------------

        /// <summary>True while the sprint key is held (<see cref="HoldSprint"/>), whether or not she sprints.</summary>
        public bool IsSprintHeld => _sprintHeld;
        /// <summary>True while she sprints: the switch, the key and a move key held, and free to run (<see cref="SprintRules.On"/>). Not in a jump.</summary>
        public bool IsSprinting => _sprintHeld && _moveInput != Vector3.zero && SprintRules.On(Feel, true, true, SprintFree); // the key and a move first: nothing else is asked while she does not try to sprint (not in her Awake, before the others have woken)
        /// <summary>True while she is in a jump over a low prop.</summary>
        public bool IsJumping => _jumping;
        /// <summary>Where the jump going on (or the last) took off and lands, and when it took off and lands.</summary>
        public Vector3 JumpFrom => _jumpFrom;
        public Vector3 JumpTo => _jumpTo;
        public float JumpLandsAt => _jumpStartedAt + _jumpSeconds;
        /// <summary>The metres the model is lifted now: in a jump, its arc (<see cref="SprintRules.Lift"/>); else 0.</summary>
        public float JumpLift => _jumping && _jumpSeconds > 0f ? SprintRules.Lift(Feel, (Time.time - _jumpStartedAt) / _jumpSeconds) : 0f;
        /// <summary>Why the last look for a jump did not jump (the readout), and the counts.</summary>
        public JumpRefusal LastJumpRefusal { get; private set; } = JumpRefusal.NothingAhead;
        public int SprintsStarted { get; private set; }
        public int Jumps { get; private set; }
        public int SprintStaggers { get; private set; }
        public int RefusedWhileSprinting { get; private set; }
        /// <summary>Seconds of the sprints that have ended.</summary>
        public float SprintSeconds { get; private set; }

        /// <summary>
        /// Free to run, for the sprint: not dashing, in a jump, a skill, a stun or a pull, down in a duck, in cover or Slide; no basic
        /// shot that has not gone off; the duck key not held (it ends the sprint, and ducks by the duck's own rule); alive, and no
        /// pause or choice.
        /// </summary>
        bool SprintFree => !_dashing && !_jumping && _actionKind != ActionKind.Skill && !_held.IsHeld && !_duck.IsDown && !_inCover && !_keyMoving
                           && !_coverHeld && !(_actionKind == ActionKind.BasicShot && !_action.HitResolved) && MayBeHeldNow;

        /// <summary>The sprint key: held or not. Given every frame by the input; it holds until it is changed.</summary>
        public void HoldSprint(bool held) => _sprintHeld = held;

        /// <summary>The share of her run speed the sprint gives now: 1, or up to <see cref="FeelValues.SprintSpeedShare"/>.</summary>
        float SprintShareNow(FeelValues feel) => SprintRules.SpeedShare(feel, IsSprinting, Time.time - _sprintStartedAt);

        /// <summary>
        /// A click (<paramref name="slot"/> -1) or a skill key while she sprints or is in a jump: nothing, not kept, counted and told.
        /// True when it was refused.
        /// </summary>
        bool RefuseWhileSprinting(int slot)
        {
            if (!SprintRules.Refuses(Feel, _jumping || IsSprinting)) return false;
            RefusedWhileSprinting++;
            Report(new PlayerReport { Kind = PlayerReportKind.SprintRefused, Slot = slot });
            return true;
        }

        // ---------------- Every frame ----------------

        /// <summary>
        /// At the top of each frame: a sprint that begins drops what waits and frees her to run; one that ends is told with its
        /// seconds. A jump belongs to the sprint it was taken in.
        /// </summary>
        void SprintTick(float now)
        {
            bool on = IsSprinting;
            if (on && !_sprintSpan) BeginSprint(now);
            else if (!on && !_jumping && _sprintSpan) EndSprintSpan(now);
        }

        void BeginSprint(float now)
        {
            _sprintSpan = true;
            _sprintStartedAt = now;
            SprintsStarted++;
            DropWaitingClick(); // nothing waits through a sprint: a click is not kept
            if (_buffer.HasItem)
            {
                Report(new PlayerReport { Kind = PlayerReportKind.BufferDropped, Slot = BufferedSkillSlot });
                _buffer.Clear();
            }
            if (_actionKind == ActionKind.BasicShot) // gone off (SprintFree): its recovery is cut, as a move key cuts it
            {
                _action.Cancel();
                EndAction();
            }
            _easing = false;
            _rising = false;
            _backpedalCap.Reset();
            _shotFacing.End(turnBack: false);
            Report(new PlayerReport { Kind = PlayerReportKind.SprintStarted });
        }

        void EndSprintSpan(float now)
        {
            _sprintSpan = false;
            float seconds = Mathf.Max(0f, now - _sprintStartedAt);
            SprintSeconds += seconds;
            string why = !_sprintHeld ? "released" : _moveInput == Vector3.zero ? "stopped" : _dashing ? "dash"
                       : _coverHeld || _duck.IsDown ? "duck" : _held.IsHeld ? "held" : "other";
            Report(new PlayerReport { Kind = PlayerReportKind.SprintEnded, Seconds = seconds, By = why });
        }

        /// <summary>
        /// While she sprints, before she walks: a jump over the low prop her way meets, if <see cref="SprintRules.Decide"/> says so.
        /// True when one began. Nothing is allocated (the lists are kept).
        /// </summary>
        bool TryJump(FeelValues feel, float now)
        {
            if (!IsSprinting || !_agent.isOnNavMesh) return false;
            _jumpShapes.Clear();
            _jumpShelters.Clear();
            IReadOnlyList<Shelter> all = Shelter.All;
            bool anyLow = false;
            for (int i = 0; i < all.Count; i++)
            {
                Shelter s = all[i];
                if (s == null || !s.TryShape(out CoverShape shape)) continue;
                bool low = s.LowProp && !s.IsTall;
                anyLow |= low;
                _jumpShapes.Add(new JumpShape(shape, low));
                _jumpShelters.Add(s);
            }
            if (!anyLow)
            {
                LastJumpRefusal = JumpRefusal.NothingAhead;
                return false;
            }
            _jumpEnemies.Clear();
            IReadOnlyList<Damageable> targets = Damageable.All;
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null && targets[i].IsAlive) _jumpEnemies.Add(targets[i].Info);
            if (_walkable == null) _walkable = Walkable;
            JumpPlan plan = SprintRules.Decide(feel, GroundPosition, new GroundPoint(_moveInput.x, _moveInput.z), _jumpShapes, _jumpEnemies,
                                               _walkable, Cover.Any ? Cover.Line : null);
            LastJumpRefusal = plan.Refusal;
            if (!plan.Jumps) return false;
            StartJump(feel, now, plan, _jumpShelters[plan.Prop]);
            return true;
        }

        /// <summary>Is there walkable floor (the NavMesh) right at this point?</summary>
        static bool Walkable(GroundPoint p)
        {
            var at = new Vector3(p.X, 0f, p.Z);
            if (!NavMesh.SamplePosition(at, out NavMeshHit hit, LandingSample, NavMesh.AllAreas)) return false;
            Vector3 off = hit.position - at;
            off.y = 0f;
            return off.sqrMagnitude <= LandingOff * LandingOff;
        }

        void StartJump(FeelValues feel, float now, JumpPlan plan, Shelter over)
        {
            _jumping = true;
            _jumpFrom = transform.position;
            _jumpTo = new Vector3(plan.To.X, _jumpFrom.y, plan.To.Z);
            _jumpStartedAt = now;
            _jumpSpeed = feel.RunSpeed * RunSpeedShare * Mathf.Max(1f, feel.SprintSpeedShare); // her sprint speed, full
            _jumpSeconds = SprintRules.JumpSeconds(plan.Length, _jumpSpeed);
            _staggerOnLanding = false;
            Jumps++;
            Halt();
            // The agent waits at the take-off and is put down at the landing (Land): it would not go over the prop's hole.
            _agent.updatePosition = false;
            Face(Flat(_jumpTo - _jumpFrom));
            Report(new PlayerReport { Kind = PlayerReportKind.SprintJumped, By = over != null ? over.name : null, Metres = plan.Length });
        }

        /// <summary>One frame of a jump: she moves along it; true while she is still in the air (nothing else this frame).</summary>
        bool JumpStep(float now)
        {
            if (!_jumping) return false;
            float k = _jumpSeconds > 0f ? Mathf.Clamp01((now - _jumpStartedAt) / _jumpSeconds) : 1f;
            transform.position = Vector3.Lerp(_jumpFrom, _jumpTo, k);
            if (k < 1f) return true;
            Land();
            return false;
        }

        /// <summary>She lands, running: the agent put down where she is, and what took her in the air takes her now.</summary>
        void Land()
        {
            if (!_jumping) return;
            EndJump();
            _agent.Warp(_jumpTo);
            FeelValues feel = Feel;
            if (_staggerOnLanding)
            {
                _staggerOnLanding = false;
                if (Stun(feel.SprintStagger, SprintRules.StaggerName, 0f)) SprintStaggers++;
            }
            if (_airStun)
            {
                _airStun = false;
                Stun(_airStunSeconds, _airStunBy, _airStunGrace);
            }
            if (_airPull)
            {
                _airPull = false;
                Pull(_airPullTo, _airPullSeconds, _airPullBy);
            }
        }

        /// <summary>The jump is over (landed, or a teleport): the agent moves her again. What waited for the landing is dropped here.</summary>
        void EndJump()
        {
            if (!_jumping) return;
            _jumping = false;
            _agent.updatePosition = true;
        }

        /// <summary>A teleport, death or a new floor in the air: no landing, nothing kept for it.</summary>
        void DropJump()
        {
            EndJump();
            _staggerOnLanding = _airStun = _airPull = false;
        }

        /// <summary>A stun that takes her in the air: kept for the landing. True when kept.</summary>
        bool KeepStunForLanding(float seconds, string by, float grace)
        {
            if (!_jumping || !MayBeHeldNow) return false;
            _airStun = true;
            _airStunSeconds = seconds;
            _airStunBy = by;
            _airStunGrace = grace;
            return true;
        }

        /// <summary>A pull that takes her in the air: kept for the landing, toward the same point for its whole time. True when kept.</summary>
        bool KeepPullForLanding(Vector3 to, float seconds, string by)
        {
            if (!_jumping || !MayBeHeldNow) return false;
            _airPull = true;
            _airPullTo = to;
            _airPullSeconds = seconds;
            _airPullBy = by;
            return true;
        }

        /// <summary>
        /// A hit took <paramref name="applied"/> health (<see cref="PlayerHealth.TakeHit"/>, she lives): while she sprints, a stagger
        /// now; in a jump, at the landing; else nothing (<see cref="SprintRules.Staggers"/>).
        /// </summary>
        public void TookHit(int applied)
        {
            FeelValues feel = Feel;
            if (!SprintRules.Staggers(feel, _jumping || IsSprinting, applied)) return;
            if (_jumping)
            {
                _staggerOnLanding = true;
                return;
            }
            if (Stun(feel.SprintStagger, SprintRules.StaggerName, 0f)) SprintStaggers++;
        }
    }
}
