using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The act boss as a finale (work order 106, 2026-11-24; <see cref="BossValues.Finale"/>, on; its numbers
    /// <see cref="BossFinaleValues"/>, its rules <see cref="BossPhases"/>). Off, none of this does anything: every override here
    /// gives what the boss did before.
    ///
    /// The break: the frame its health first falls under half (whichever blow takes it there), as it turns angry, it roars: the
    /// roar's sound (<see cref="PlaceholderSound.BossRoar"/>) over the anger's growl, the throne room's lamps take
    /// <see cref="BossFinaleValues.LampColour"/> for the rest of the fight (<see cref="BossRoomLight"/>), and for
    /// <see cref="BossFinaleValues.RoarSeconds"/> it stands, facing her: what it was winding up or running is dropped, it starts
    /// nothing, it walks nowhere, and it cannot be staggered (its bar is held aside: hits take none off it and cannot daze it);
    /// her damage counts as ever. Dazed as it breaks, it roars when the daze is over. Its bar on screen shows the two halves
    /// (<see cref="BossHealthBar"/>). Then phase two: its angry numbers as before, and one attack in four the Ground Slam.
    ///
    /// The Ground Slam: its ring shows round the place it will land, where she stood as the windup began (the Spin's red ring),
    /// cut short of a wall on the line as its charge is. It crouches (its body squashed a little) for
    /// <see cref="BossFinaleValues.SlamCrouch"/>, then leaps along the ground to the landing in the rest of the windup, its body
    /// drawn up in an arc; it lands as the windup ends and strikes the ring there: she walks or dashes out of it. Dazed or
    /// stopped in the air, it comes down where it is.
    ///
    /// The fight's line for the recorder (<see cref="BossFightEvents"/>), once as it ends: the seconds in each phase, the slams,
    /// landed and avoided.
    /// </summary>
    public sealed partial class EnemyBoss
    {
        readonly BossPhases _phases = new BossPhases();
        float _roarUntil = -1f;
        bool _roarPending;
        StaggerBar _heldStagger;
        bool _slamming, _slamLeft;
        Vector3 _slamFrom, _slamTo;
        float _wokeAt = -1f, _brokeAt = -1f;
        int _slamsStruck, _slamsLanded, _slamsAvoided;
        bool _wasAwake, _listening;

        /// <summary>The finale's phases (work order 106): the break, the choices and slams of phase two.</summary>
        public BossPhases Phases => _phases;
        /// <summary>True from the break on (work order 106); never with the finale off.</summary>
        public bool HasBroken => _phases.Broken;
        /// <summary>True while it stands and roars at the break.</summary>
        public bool IsRoaring => _roarUntil >= 0f;
        /// <summary>When the roar ends (game time); under 0 when it is not roaring.</summary>
        public float RoarEndsAt => _roarUntil;
        /// <summary>The game time of the break; under 0 before it.</summary>
        public float BrokeAt => _brokeAt;
        /// <summary>True from the windup of a Ground Slam until it lands (or is stopped).</summary>
        public bool IsSlamming => _slamming;
        /// <summary>True while it is in the air, between the crouch and the landing.</summary>
        public bool IsInTheAir => _slamming && _slamLeft;
        /// <summary>Where the slam being wound up started, and where it lands: the ring's middle.</summary>
        public Vector3 SlamFrom => _slamFrom;
        public Vector3 SlamLanding => _slamTo;
        /// <summary>Ground Slams struck this fight, and of them those that landed on her and those she avoided (out of the ring, or in the dash).</summary>
        public int SlamsStruck => _slamsStruck;
        public int SlamsLanded => _slamsLanded;
        public int SlamsAvoided => _slamsAvoided;
        /// <summary>The lights of its room the break gave the lamps' colour.</summary>
        public int LampsAnswered { get; private set; }

        /// <summary>The bar its stagger is kept in: aside while it roars, so no hit takes from it.</summary>
        StaggerBar StaggerNow => _heldStagger ?? (_self != null ? _self.Stagger : null);

        /// <summary>The break, once (from <see cref="Temper"/>, as it turns angry): the roar's sound and the room's lamps now, the roar itself when it is not dazed.</summary>
        void Break()
        {
            _brokeAt = Time.time;
            _roarPending = true;
            PlaceholderSounds.Current?.AskFor(PlaceholderSound.BossRoar);
            LampsAnswered = BossRoomLight.Answer(this, Tuning.Phases.LampColour);
            Debug.Log("Boss: the break at " + Mathf.RoundToInt(HealthShare * 100f) + " % of its health; " + LampsAnswered + " lights of its room answer");
        }

        /// <summary>Every frame alive, before it thinks: the roar begins (once it is not dazed) and ends; a slam the brain dropped comes down.</summary>
        void FinaleTick()
        {
            if (_roarPending && !IsDazed) StartRoar();
            if (_roarUntil >= 0f && Time.time >= _roarUntil) EndRoar();
            if (_slamming && (State != EnemyState.Windup || Attack != Tuning.Phases.Slam)) EndSlam(transform.position);
            NoteFightIfOver();
        }

        /// <summary>Every frame dead: a slam in the air comes down, a roar ends, and the fight is told.</summary>
        void FinaleDead()
        {
            if (_slamming) EndSlam(transform.position);
            if (_roarUntil >= 0f || _roarPending) EndRoar();
            NoteFightIfOver();
        }

        void StartRoar()
        {
            _roarPending = false;
            _brain.Cancel(Time.time); // what it was winding up is dropped; it stands in the chase, held by the roar
            Turns.Ended(Id); // as a daze ends a turn (it takes none as the act boss; a safeguard)
            _rushing = false;
            _chargesLeft = 0;
            if (_heldStagger == null && _self != null)
            {
                _heldStagger = _self.Stagger;
                _self.Stagger = null; // no hit takes stagger while it roars; its damage still counts
            }
            _roarUntil = Time.time + Mathf.Max(0f, Tuning.Phases.RoarSeconds);
        }

        void EndRoar()
        {
            _roarPending = false;
            _roarUntil = -1f;
            if (_heldStagger != null && _self != null && _self.Stagger == null) _self.Stagger = _heldStagger;
            _heldStagger = null;
        }

        protected override bool MayAttack(Vector3 toPlayer, float distance) => !IsRoaring && base.MayAttack(toPlayer, distance);

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            if (IsRoaring)
            {
                Halt();
                Face(toPlayer); // it roars at her
                return;
            }
            base.Chase(toPlayer, distance);
        }

        // ---------------- The Ground Slam ----------------

        /// <summary>A slam's windup began: where it lands is fixed now, where she stands, cut short of a wall on the line.</summary>
        void BeginSlam()
        {
            BossFinaleValues f = Tuning.Phases;
            _slamFrom = transform.position;
            Vector3 from = Flat(_slamFrom), d = Flat(AimPoint) - from;
            float length = d.magnitude;
            float wall = length > 1e-3f ? Cover.ClearDistance(from, d / length, length, Tuning.SightHeight) : 0f;
            GroundPoint land = BossFinale.Landing(Ground(_slamFrom), Ground(AimPoint), wall, Tuning.ChargeStopShort);
            Vector3 to = new Vector3(land.X, _slamFrom.y, land.Z);
            _slamTo = NavMesh.SamplePosition(to, out NavMeshHit hit, Mathf.Max(0.1f, f.SlamFindGround), NavMesh.AllAreas) ? hit.position : _slamFrom;
            _slamming = true;
            _slamLeft = false;
        }

        /// <summary>After it has thought this frame: the crouch, then the leap along the ground, its body drawn up in the arc.</summary>
        void SlamStep()
        {
            if (!_slamming) return;
            EnemyAttackDefinition a = Attack;
            if (State != EnemyState.Windup || a != Tuning.Phases.Slam)
            {
                EndSlam(transform.position);
                return;
            }
            float progress = a.Windup > 0f ? 1f - WindupRemaining / a.Windup : 1f;
            float share = BossFinale.LeapShare(progress, a.Windup, Tuning.Phases.SlamCrouch);
            if (share > 0f && !_slamLeft)
            {
                _slamLeft = true;
                if (_agent.enabled) _agent.updatePosition = false; // in the air it is moved by hand, over what it leaps
            }
            if (_slamLeft) transform.position = Vector3.Lerp(_slamFrom, _slamTo, share);
            PoseSlam(share, !_slamLeft);
        }

        /// <summary>The strike of a slam: it lands, and the ring is struck round the landing (by <see cref="Enemy"/>'s own strike, from where it now stands).</summary>
        void LandSlam() => EndSlam(_slamTo, landed: true);

        void EndSlam(Vector3 at, bool landed = false)
        {
            bool left = _slamLeft;
            _slamming = false;
            _slamLeft = false;
            if (left || landed)
            {
                if (_agent.enabled && NavMesh.SamplePosition(at, out NavMeshHit hit, Mathf.Max(0.1f, Tuning.Phases.SlamFindGround), NavMesh.AllAreas)) at = hit.position;
                transform.position = at;
                if (_agent.enabled) _agent.Warp(at);
            }
            if (_agent.enabled) _agent.updatePosition = true;
            PoseSlam(0f, false);
        }

        /// <summary>Its body for the slam: squashed in the crouch, lifted along the arc in the air; as it was otherwise.</summary>
        void PoseSlam(float share, bool crouching)
        {
            if (Look == null || Look.Pivot == null) return;
            BossFinaleValues f = Tuning.Phases;
            float scale = Look.Measured.Scale;
            Look.Pivot.localPosition = Vector3.up * (Look.Measured.Lift + BossFinale.LeapHeight(share, f.SlamHeight));
            Look.Pivot.localScale = new Vector3(scale, scale * (crouching ? Mathf.Clamp(f.SlamSquash, 0.1f, 1f) : 1f), scale);
        }

        /// <summary>Work order 106: a slam's ring is drawn round where it lands, not round where it stands.</summary>
        protected override Vector3 TelegraphOrigin => _slamming ? Flat(_slamTo) : base.TelegraphOrigin;

        // ---------------- The fight's line ----------------

        protected override void OnWoke()
        {
            base.OnWoke();
            if (!_listening)
            {
                _listening = true;
                Reported += OnOwnReport;
            }
            _wokeAt = Time.time;
            _slamsStruck = _slamsLanded = _slamsAvoided = 0;
            _wasAwake = true;
        }

        void OnOwnReport(EnemyReport r)
        {
            if (r.Kind != EnemyReportKind.Struck || r.Attack != BossFinale.Slam) return;
            _slamsStruck++;
            if (r.Outcome == StrikeOutcome.Landed) _slamsLanded++;
            else if (r.Outcome == StrikeOutcome.OutOfArea || r.Outcome == StrikeOutcome.AvoidedByDash) _slamsAvoided++;
        }

        void NoteFightIfOver()
        {
            if (!_wasAwake || IsAwake) return;
            NoteFight(IsDead ? BossFightEvents.Died : BossFightEvents.HerDeath);
        }

        /// <summary>The fight is over: told once, with the finale on (work order 106).</summary>
        void NoteFight(string how)
        {
            _wasAwake = false;
            if (!BossValues.Finale || _wokeAt < 0f) return;
            float end = Time.time;
            bool broke = _phases.Broken;
            float breakAt = broke ? Mathf.Max(_brokeAt, _wokeAt) : end; // broken in an earlier fight: phase two from its waking
            PlaySessionRecorder.Current?.NoteBossFight(breakAt - _wokeAt, broke ? end - breakAt : 0f, broke, _slamsStruck, _slamsLanded, _slamsAvoided, how);
            _wokeAt = -1f;
        }

        protected override void OnDestroy()
        {
            // Taken away awake (the floor left, the practice spawns cleared): its body may be gone first, so only a death seen says otherwise.
            if (_wasAwake && (_self == null || _self.IsAlive)) NoteFight(BossFightEvents.Removed);
            if (_listening) Reported -= OnOwnReport;
            base.OnDestroy();
        }
    }
}
