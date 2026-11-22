using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Taking cover and the quick duck of <see cref="PlayerCharacter"/>: their state for the readout and the tests, the key, and
    /// how each runs (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- Taking cover (Kristian 2026-11-19, an experiment) ----------------

        /// <summary>True while the cover key is held (<see cref="HoldCover"/>), whether or not she is in cover.</summary>
        public bool IsCoverHeld => _coverHeld;
        /// <summary>True while she is in cover against a wall or a prop: moving there, ducked, or stood up by a shot or a skill.</summary>
        public bool IsInCover => _inCover;
        /// <summary>
        /// True while she is ducked in cover: there, and no shot or skill or its peek time stands her up; or down in a quick
        /// duck (work order 043).
        /// </summary>
        public bool IsDucked => (_inCover && !_snapping && !StoodUpInCover) || _duck.IsDown;
        bool StoodUpInCover => _actionKind != ActionKind.None || Time.time < _peekUntil;
        /// <summary>True when what she is in cover against is tall cover (it stops shots for anyone).</summary>
        public bool CoverIsTall => _inCover && _coverTall;
        /// <summary>What she is in cover against, or null.</summary>
        public Shelter CoverObject => _inCover ? _shelter : null;
        /// <summary>
        /// The way from what she is in cover against (or down beside in a quick duck) to her, flat and of length 1; zero
        /// out of cover.
        /// </summary>
        public Vector3 CoverOutward
        {
            get
            {
                if (!_inCover && !_duck.IsDown) return Vector3.zero;
                GroundPoint n = (_inCover ? _coverShape : _duckShape).Outward(GroundPosition);
                return new Vector3(n.X, 0f, n.Z);
            }
        }
        /// <summary>Times she took cover.</summary>
        public int CoversTaken { get; private set; }
        /// <summary>Enemy shots that what she was in cover against stopped (<see cref="NoteEnemyShotStoppedByCover"/>).</summary>
        public int EnemyShotsStoppedByCover { get; private set; }

        /// <summary>
        /// Is she covered against a shot from this point by what she is in cover against? Only in cover, only when it lies
        /// between them, and a low prop only while she is ducked (<see cref="TakeCoverRules.Shields"/>). Walls and tall
        /// props stop shots for anyone as well, through <see cref="Cover"/>; this is the part that is hers. The shot is
        /// an arrow (the shooter's sight and its shot): down in a quick duck it is covered too (work order 043).
        /// </summary>
        public bool ShieldsFrom(Vector3 shooter) => ShieldsFrom(shooter, ProjectileKind.Arrow);

        /// <summary>
        /// The same for a thing of <paramref name="kind"/> loosed from <paramref name="shooter"/>: in cover as above,
        /// whatever its kind; down in a quick duck (work order 043) only an arrow, and only with the prop she ducked at
        /// between them (<see cref="QuickDuckRules.Shields"/>): an orb passes over a low prop.
        /// </summary>
        public bool ShieldsFrom(Vector3 shooter, ProjectileKind kind)
        {
            var from = new GroundPoint(shooter.x, shooter.z);
            if (_inCover) return TakeCoverRules.Shields(_coverShape, _coverTall, IsDucked, GroundPosition, from);
            return _duck.IsDown && QuickDuckRules.Shields(_duckShape, GroundPosition, from, kind);
        }

        /// <summary>
        /// Metres from a point along a flat direction before what she is in cover against (or down beside in a quick duck);
        /// <paramref name="max"/> when not.
        /// </summary>
        public float CoverEntry(Vector3 from, Vector3 direction, float max)
        {
            if (!_inCover && !_duck.IsDown) return max;
            return (_inCover ? _coverShape : _duckShape).Entry(new GroundPoint(from.x, from.z), new GroundPoint(direction.x, direction.z), max);
        }

        /// <summary>
        /// An enemy shot was stopped by what she is in cover against: counted, for the readout and tests. Down in a quick
        /// duck (work order 043) it is the duck's: counted, and a spark, a sound and the word.
        /// </summary>
        public void NoteEnemyShotStoppedByCover()
        {
            EnemyShotsStoppedByCover++;
            if (_duck.IsDown) DuckStoppedOne();
        }

        /// <summary>
        /// Where a thing that flew ended on what she is down beside (work order 043), told just before its end is: the
        /// duck's spark goes there. Without it (the shooter's old line) the spark goes on the prop's far side.
        /// </summary>
        public void NoteCoverStopAt(Vector3 at)
        {
            if (!_duck.IsDown) return;
            _duckStopAt = at;
            _duckStopAtKnown = true;
        }

        // ---------------- The quick duck (work order 043), for the readout and tests ----------------

        /// <summary>True while the quick duck's switch is on (and taking cover's): the cover key ducks her instead of taking cover.</summary>
        public bool QuickDuckOn => Feel.TakeCover && Feel.QuickDuck;
        /// <summary>True while she is down in a quick duck.</summary>
        public bool IsQuickDucking => _duck.IsDown;
        /// <summary>What she is down beside, or null.</summary>
        public Shelter DuckObject => _duck.IsDown ? _duckShelter : null;
        /// <summary>Seconds until a tap ducks her again; 0 when it would (or while she is down).</summary>
        public float DuckWaitLeft => _duck.WaitLeft(Time.time);
        /// <summary>When the duck going on (or the last) began, when she rises by herself, and when she last rose.</summary>
        public float DuckDownAt => _duck.DownAt;
        public float DuckUpAt => _duck.UpAt;
        public float DuckRoseAt => _duck.RoseAt;
        /// <summary>What the duck going on has stopped so far.</summary>
        public int DuckStoppedNow => _duck.Stopped;
        /// <summary>Ducks taken, of them those that stopped something; taps with no low prop in reach; things ducks stopped.</summary>
        public int DucksTaken { get; private set; }
        public int DucksThatStopped { get; private set; }
        public int DuckTapsWithNothing { get; private set; }
        public int ShotsStoppedByDuck { get; private set; }
        /// <summary>Basic shots that were the timing's reward: the first soon after a duck that stopped something.</summary>
        public int PeekShots { get; private set; }
        /// <summary>The low prop the rim light is on now, or null.</summary>
        public Shelter DuckRimOn => _rim.Shown ? _rim.On : null;
        /// <summary>The rim light itself, for tests.</summary>
        public QuickDuckRim DuckRim => _rim;
        /// <summary>True while something of hers may stop a thing loosed at her: she is in cover, or down in a quick duck.</summary>
        public bool HidesBehindSomething => _inCover || _duck.IsDown;
        /// <summary>
        /// The cover key (Left Ctrl to start with; Kristian 2026-11-19, an experiment): held or not. Given every frame by
        /// the input; it holds until it is changed. Held, she takes cover against a wall or prop in reach when she is free;
        /// let go, she leaves cover at once and the move keys work at full speed from this frame on. With the quick duck on
        /// (work order 043) the key going down is a tap (<see cref="TapCover"/>) and holding it does nothing more.
        /// </summary>
        public void HoldCover(bool held)
        {
            bool pressed = held && !_coverHeld;
            _coverHeld = held;
            if (!held && _inCover) LeaveCover(CoverLeaving.Released);
            if (pressed && QuickDuckOn) TapCover();
        }

        /// <summary>
        /// Press: a tap of the cover key with the quick duck on (work order 043, <see cref="QuickDuck"/>). With a low prop
        /// within <see cref="FeelValues.DuckReach"/> she drops where she stands for <see cref="FeelValues.DuckSeconds"/>; with
        /// none, nothing happens but a small dull sound, and no wait starts. Down already, the key waiting, or she cannot
        /// duck now (a skill, a dash, Slide, stunned or pulled, dead, a choice, paused): nothing. A basic shot that has not
        /// gone off is dropped by the duck, as a dash drops it. For the input and tests.
        /// </summary>
        public DuckTap TapCover()
        {
            FeelValues feel = Feel;
            if (!feel.TakeCover || !feel.QuickDuck) return DuckTap.Off;
            float now = Time.time;
            bool may = MayDuckNow;
            Shelter at = null;
            CoverShape shape = default;
            bool found = _duck.Ready(now) && may && FindLowProp(feel, out at, out shape);
            DuckTap tap = _duck.Tap(now, may, found, feel.DuckSeconds);
            if (tap == DuckTap.Ducked) StartDuck(at, shape);
            else if (tap == DuckTap.NothingInReach)
            {
                DuckTapsWithNothing++;
                if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.EnemyHit, DuckNothingVolume); // its dull tick
                Report(new PlayerReport { Kind = PlayerReportKind.DuckNothing });
            }
            return tap;
        }

        // ---------------- Taking cover (Kristian 2026-11-19, an experiment) ----------------

        /// <summary>
        /// Every frame she is not dashing: leaves cover when the key is up, the switch is off or the thing is gone; takes
        /// cover when the key is held, she is free and something is in reach; and moves her to stand against it.
        /// </summary>
        void UpdateCover(FeelValues feel, float now)
        {
            UpdateDuck(feel, now); // work order 043
            // With the quick duck on, taking cover is off (work order 043).
            if (_inCover && (!feel.TakeCover || feel.QuickDuck || !_coverHeld || _shelter == null || !_shelter.isActiveAndEnabled))
                LeaveCover(_coverHeld ? CoverLeaving.Other : CoverLeaving.Released);
            if (!_inCover && feel.TakeCover && !feel.QuickDuck && _coverHeld && !IsLocked && !_keyMoving) TryTakeCover(feel, now);
            if (!_inCover) return;

            Vector3 at = transform.position;
            float dt = Time.deltaTime;
            if (dt > 0f) _coverVelocity = Flat(at - _coverLastPos) / dt;
            _coverLastPos = at;
            if (_snapping)
            {
                float k = feel.CoverSnapTime > 0f ? Mathf.Clamp01((now - _snapStartedAt) / feel.CoverSnapTime) : 1f;
                MoveInCover(Vector3.Lerp(_snapFrom, _snapTo, k));
                if (k >= 1f) _snapping = false;
            }
        }

        /// <summary>Takes cover against the nearest wall or prop that blocks walking within reach, if there is one.</summary>
        void TryTakeCover(FeelValues feel, float now)
        {
            GroundPoint her = GroundPosition;
            Shelter best = null;
            CoverShape bestShape = default;
            float bestDistance = feel.CoverReach;
            IReadOnlyList<Shelter> all = Shelter.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || !all[i].TryShape(out CoverShape shape)) continue;
                float distance = shape.Distance(her);
                if (distance > bestDistance) continue;
                best = all[i];
                bestShape = shape;
                bestDistance = distance;
            }
            if (best == null) return;

            _inCover = true;
            _shelter = best;
            _coverShape = bestShape;
            _coverTall = best.IsTall;
            GroundPoint place = TakeCoverRules.StandPlace(bestShape, her, feel.CoverStandOff);
            _snapFrom = transform.position;
            _snapTo = new Vector3(place.X, _snapFrom.y, place.Z);
            _snapStartedAt = now;
            _snapping = true;
            _coverLastPos = transform.position;
            _coverVelocity = Vector3.zero;
            Halt();
            CoversTaken++;
            Report(new PlayerReport { Kind = PlayerReportKind.CoverEntered, Tall = _coverTall });
        }

        void LeaveCover(CoverLeaving why)
        {
            if (!_inCover) return;
            _inCover = false;
            _shelter = null;
            _snapping = false;
            _coverVelocity = Vector3.zero;
            // The key going up gives the move keys back at once, at full speed: no ease up from a skill used in cover.
            if (why == CoverLeaving.Released) _rising = false;
            Report(new PlayerReport { Kind = PlayerReportKind.CoverLeft, Leaving = why });
        }

        /// <summary>
        /// The move keys in cover: ducked, they slide her along it at the cover share of run speed, staying against it
        /// (<see cref="TakeCoverRules.Slide"/>); stood up by a shot or a skill, or on her way there, she stays where she is.
        /// </summary>
        void CoverSlide()
        {
            if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
            if (_snapping || StoodUpInCover || _moveInput == Vector3.zero) return;
            FeelValues feel = Feel;
            // Her run speed with the run's upgrades, as everywhere else she moves (work order 026, the second review of 2026-11-20).
            GroundPoint next = TakeCoverRules.Slide(_coverShape, GroundPosition, new GroundPoint(_moveInput.x, _moveInput.z),
                                                    feel.RunSpeed * RunSpeedShare * feel.CoverMoveShare * Time.deltaTime, feel.CoverStandOff, feel.CoverSlideAngle);
            MoveInCover(new Vector3(next.X, transform.position.y, next.Z));
        }

        /// <summary>Moves her to a point in cover by the NavMesh agent, so she stays on the NavMesh.</summary>
        void MoveInCover(Vector3 point)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 1e-10f && _agent.isOnNavMesh) _agent.Move(d);
        }

        // ---------------- The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043) ----------------

        /// <summary>How loud the duck's sounds play, as a share of their own volume (first guesses).</summary>
        const float DuckNothingVolume = 0.6f, DuckStopVolume = 0.7f;
        /// <summary>Metres over the ground of the spark where a duck stopped something: about where an arrow flies.</summary>
        const float DuckSparkHeight = 0.9f;
        /// <summary>The skill whose stagger the timing's reward deals: a strong hit that exists (no new strength).</summary>
        const string PeekStaggerOf = "Scatter Blast";

        /// <summary>
        /// May a duck start now? Not in a skill, a dash or Slide, not stunned or pulled, and never while she is dead, a run's
        /// result is in, the game is paused or a choice is open (as for a stun or a pull, <see cref="MayBeHeldNow"/>).
        /// </summary>
        bool MayDuckNow => !_dashing && _actionKind != ActionKind.Skill && !_keyMoving && !_held.IsHeld && MayBeHeldNow;

        /// <summary>
        /// May a duck going on go on? Not once she is dead or a run's result is in. A pause or a choice only holds it, as
        /// game time stands still; a stun, a pull, a restart and a new floor end it where they happen.
        /// </summary>
        bool DuckMayGoOn
        {
            get
            {
                if (_herHealth == null) _herHealth = GetComponent<PlayerHealth>();
                return _herHealth == null || (_herHealth.IsAlive && !_herHealth.Untouchable);
            }
        }

        /// <summary>The props' sort follows her switch, live (<see cref="Cover.QuickDuckSort"/>).</summary>
        bool DuckSortIsOn() => this != null && QuickDuckOn;

        /// <summary>
        /// Every frame she is not dashing (with taking cover): the props' sort follows the switch; a duck ends when the
        /// switch goes off, she may not be down any more or the prop is gone, rises by itself when its time is up, and ends
        /// at a move key that was not held at the tap; and the rim light goes on the low prop a tap would work at.
        /// </summary>
        void UpdateDuck(FeelValues feel, float now)
        {
            if (_duckSort == null) _duckSort = DuckSortIsOn;
            Cover.QuickDuckSort = _duckSort;
            if (_duck.IsDown)
            {
                // Let go: the next key ends it. With the keys known and none gone down, what is held now was held at the tap
                // (work order 047): letting go of one of two keys does not end it.
                if (_moveInput == Vector3.zero || (_steeredByKeys && !_moveKeyWentDown)) _duckHeldAtTap = _moveInput;
                if (!feel.TakeCover || !feel.QuickDuck || !DuckMayGoOn || _duckShelter == null || !_duckShelter.isActiveAndEnabled)
                    EndDuck(DuckEnd.Other);
                else if (_duck.RisesBy(now)) EndDuck(DuckEnd.Rose);
                // Steered by the keys, a key going down ends it, whatever the direction comes to (the fourth review of
                // 2026-11-21, work order 055: the opposite key, pressed with one held, gives no direction); steered by a
                // direction alone, a direction not held at the tap, as before.
                else if (_steeredByKeys ? _moveKeyWentDown
                         : QuickDuckRules.MoveEnds(new GroundPoint(_duckHeldAtTap.x, _duckHeldAtTap.z), new GroundPoint(_moveInput.x, _moveInput.z)))
                    EndDuck(DuckEnd.Moved);
            }
            _steeredByKeys = false; // looked at once a frame (work order 047)
            _moveKeyWentDown = false;
            if (feel.TakeCover && feel.QuickDuck && _duck.Ready(now) && MayDuckNow && FindLowProp(feel, out Shelter at, out CoverShape shape))
                _rim.Show(at, shape, ShotFxMaterial);
            else
                _rim.Hide();
        }

        /// <summary>
        /// The nearest low prop within <see cref="FeelValues.DuckReach"/> of her, her middle to its edge: a shelter that is not
        /// tall (<see cref="Shelter.IsTall"/>). A wall or a tall prop is never one. Nothing is allocated.
        /// </summary>
        bool FindLowProp(FeelValues feel, out Shelter best, out CoverShape bestShape)
        {
            GroundPoint her = GroundPosition;
            best = null;
            bestShape = default;
            float bestDistance = feel.DuckReach;
            IReadOnlyList<Shelter> all = Shelter.All;
            for (int i = 0; i < all.Count; i++)
            {
                Shelter s = all[i];
                if (s == null || !s.TryShape(out CoverShape shape)) continue;
                float distance = shape.Distance(her);
                if (distance > bestDistance || s.IsTall) continue; // in reach (QuickDuckRules.InReach) and nearer than the best so far
                best = s;
                bestShape = shape;
                bestDistance = distance;
            }
            return best != null;
        }

        /// <summary>
        /// She drops where she stands beside the prop: no snap, no slide. A basic shot that has not gone off is dropped, as a
        /// dash drops it, and one that has is over: she cannot shoot while down.
        /// </summary>
        void StartDuck(Shelter at, CoverShape shape)
        {
            if (_actionKind == ActionKind.BasicShot)
            {
                _action.Cancel();
                EndAction();
            }
            _duckShelter = at;
            _duckShape = shape;
            _duckHeldAtTap = _moveInput;
            _moveKeyWentDown = false; // a key that went down with the tap is held at it (work order 047)
            _duckStopAtKnown = false;
            _rim.Hide();
            Halt();
            DucksTaken++;
            Report(new PlayerReport { Kind = PlayerReportKind.Ducked, By = at != null ? at.name : null });
        }

        /// <summary>The duck going on ends, and the key's wait runs from the moment she is up: its own time when she rose by herself.</summary>
        void EndDuck(DuckEnd why)
        {
            if (!_duck.IsDown) return;
            _duck.End(why == DuckEnd.Rose ? _duck.UpAt : Time.time, Feel.DuckWait);
            int stopped = _duck.StoppedLast;
            if (stopped > 0) DucksThatStopped++;
            _duckShelter = null;
            _duckStopAtKnown = false;
            Report(new PlayerReport { Kind = PlayerReportKind.DuckEnded, DuckEnd = why, Shot = stopped });
        }

        /// <summary>
        /// The duck stopped a thing that flew at her (or the shooter's old line): counted, a spark at the prop where it
        /// ended (her shots' spark on cover), the placeholder sounds' heavy low impact, and the word DUCKED over her.
        /// </summary>
        void DuckStoppedOne()
        {
            _duck.StoppedOne();
            ShotsStoppedByDuck++;
            Vector3 at = _duckStopAtKnown ? _duckStopAt : FarSideOfDuck();
            _duckStopAtKnown = false;
            SpawnCoverHit(LookFor(null, 0f), new Vector3(at.x, DuckSparkHeight, at.z));
            if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.Strike, DuckStopVolume);
            Report(new PlayerReport { Kind = PlayerReportKind.DuckStopped, At = new GroundPoint(at.x, at.z) });
        }

        /// <summary>The middle of the far side of what she is down beside, seen from her: where a shot from beyond it meets it.</summary>
        Vector3 FarSideOfDuck()
        {
            GroundPoint c = _duckShape.Centre, away = c - GroundPosition;
            float length = away.Length;
            away = length > 1e-4f ? away * (1f / length) : new GroundPoint(transform.forward.x, transform.forward.z);
            GroundPoint p = _duckShape.Closest(c + away * 10f);
            return new Vector3(p.X, 0f, p.Z);
        }

        /// <summary>
        /// A basic shot starts: is it the timing's reward, her first started within <see cref="FeelValues.DuckPeekWindow"/>
        /// of rising from a duck that stopped something? Either way her first shot has had its turn.
        /// </summary>
        bool TakePeekShot(FeelValues feel)
        {
            bool peek = _duck.TakePeekShot(Time.time, feel.DuckPeekWindow) && feel.DuckPeekShot && QuickDuckOn;
            if (peek) PeekShots++;
            return peek;
        }

        /// <summary>The stagger the basic shot going off deals: Scatter Blast's for the timing's reward, else none (as always).</summary>
        float TakePeekStagger()
        {
            if (!_peekShot) return 0f;
            _peekShot = false;
            int slot = SlotOf(PeekStaggerOf);
            return slot >= 0 && Skills[slot] != null ? Mathf.Max(0f, Skills[slot].Stagger) : 0f;
        }
    }
}
