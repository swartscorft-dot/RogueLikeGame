using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>What work order 143's precision made of one basic shot: for the log, the practice room's task and tests.</summary>
    public struct PrecisionShot
    {
        /// <summary>The mode it was fired under; Off: nothing below was worked out.</summary>
        public PrecisionMode Mode;
        /// <summary>The speed share the accuracy read at the press, and the lowest during the wind-up (the one that counts).</summary>
        public float ShareAtPress, ShareLowest;
        /// <summary>The meter's momentum at the shot (Meter), the hidden velocity's speed in m/s (Ghost), her own speed in m/s.</summary>
        public float Momentum, HiddenSpeed, Speed;
        /// <summary>The cone's full width in degrees (after her Precision's cut) and the deflection drawn in it (clockwise from above).</summary>
        public float Cone, Deflection;
        /// <summary>Metres from her to the target whose centre lies nearest the straight aim, within the reach; NaN for none.</summary>
        public float NearestDistance;
        /// <summary>The straight line would have hit something; the deflected one hits nothing: a miss the cone caused.</summary>
        public bool StraightHit, ConeMiss;
        /// <summary>Seconds since her last counter-strafe at the press (NaN: none yet).</summary>
        public float SinceCounterStrafe;
        /// <summary>She ran (<see cref="PrecisionValues.StopShotRunShare"/>) within <see cref="PrecisionValues.StopShotRunWithin"/> before the press and the shot went off accurate: the practice room's "Stop and shoot".</summary>
        public bool StoppedAfterARun;
    }

    /// <summary>
    /// Work order 143, precision and momentum (Kristian, 2026-11-26: "jo høyere den er, jo mer usikker skyter man (altså en cone blir
    /// bredere), og som i cs. så kan man counter strafe for å stoppe fortere"; "Ekte treghet, måleren som valg"; "Bare grunnskuddet"):
    /// her side of it. The mode is <see cref="PrecisionSettings.ModeNow"/>; Off, nothing here runs and she is as before, field for field.
    /// - Each frame, around the frame's own work (<see cref="PrecisionBefore"/>, <see cref="PrecisionAfter"/>): Inertia runs her body
    ///   by its law (the speed caps the frame set, a moving shot's, the backpedal's, a skill's slide, the sprint's, are the speed it
    ///   aims for; friction brings her down to them; a dash exits at run speed along it); the meter and the ghost follow her real
    ///   velocity; a counter-strafe is told; the speed share the accuracy reads is kept, and its lowest in a shot's wind-up.
    /// - A basic shot that goes off is deflected inside its cone (<see cref="DeflectBasicShot"/>): centre-weighted, from the run's
    ///   seeded bag; its line keeps its width. Only the basic shot (clicked, held, the heavy round); a pierce runs on along the
    ///   deflected line and a Chain Kill's bounce is not drawn again; no skill is touched.
    /// Nothing is allocated in a frame.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        PrecisionMode _pmode;
        Vector3 _bodyVelocity;          // Inertia: her body's velocity, flat
        bool _wishThisFrame;            // MoveAlong asked for a way this frame (Inertia)
        Vector3 _wishDir;
        float _wishSpeed;
        float _momentum;                // the meter
        Vector3 _ghostVelocity;         // the ghost
        Vector3 _motion;                // her velocity averaged over the last PrecisionValues.MotionSeconds
        Vector3 _heldBefore;            // the held direction last frame
        bool _wasDashing;
        float _precisionShare;          // the share the accuracy reads now
        float _lowestInWindUp = 1f, _shareAtPress;
        bool _windUp;
        float _lastCounterStrafeAt = float.NegativeInfinity;
        float _ranAt = float.NegativeInfinity, _ranAtPress = float.NegativeInfinity;
        PrecisionBag _bag;

        /// <summary>The mode in use this frame.</summary>
        public PrecisionMode PrecisionModeNow => _pmode;
        /// <summary>The share of her run speed the accuracy reads now (the meter's as the share it stands for).</summary>
        public float PrecisionShare => _precisionShare;
        /// <summary>The cone's full width in degrees a shot would have now, after her Precision's cut; 0 Off.</summary>
        public float PrecisionCone => _pmode == PrecisionMode.Off ? 0f : ConeFor(_precisionShare);
        /// <summary>The meter's momentum, 0 to 1 (Meter).</summary>
        public float Momentum => _momentum;
        /// <summary>The hidden velocity (Ghost), flat, m/s.</summary>
        public Vector3 GhostVelocity => _ghostVelocity;
        /// <summary>The last basic shot's precision, as worked out when it went off.</summary>
        public PrecisionShot LastPrecisionShot { get; private set; }
        /// <summary>Counts, since she was made: misses the cone caused, counter-strafes.</summary>
        public int ConeMisses { get; private set; }
        public int CounterStrafes { get; private set; }
        /// <summary>Her speed at full run now, m/s: the feel's, her stats' share, focus's.</summary>
        float RunSpeedNow => Mathf.Max(0.1f, Feel.RunSpeed * RunSpeedShare);
        PrecisionValues PV => PrecisionValues.Live;
        /// <summary>Her Precision's cut of the moving cone (the Laser Sight's), 0 outside a run.</summary>
        float PrecisionCut => Modifiers != null ? Modifiers.Stats.Precision : 0f;
        float AccurateShareNow => Modifiers != null ? Modifiers.Stats.PrecisionAccurateShare : PV.AccurateShare;

        float ConeFor(float share) => PrecisionRules.AfterCut(PrecisionRules.Cone(share, PV, AccurateShareNow), PrecisionCut, PV);

        /// <summary>For tests: the draw's bag from this seed (a run's own is set when the run starts).</summary>
        public void SeedPrecision(uint seed)
        {
            if (_bag == null) _bag = new PrecisionBag(seed, PV);
            else _bag.Seed(seed, PV);
        }

        /// <summary>A run starts or ends (<see cref="StartFresh"/>): the bag from the run's seed (the practice room's outside one), the meter and the ghost at rest.</summary>
        void FreshPrecision()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            ulong? run = levels != null && levels.InRun ? levels.RunSeed : null;
            SeedPrecision(run.HasValue ? (uint)(run.Value ^ (run.Value >> 32)) : PV.PracticeSeed);
            _momentum = 0f;
            _ghostVelocity = Vector3.zero;
            _motion = Vector3.zero;
        }

        /// <summary>MoveAlong's way and speed this frame (Inertia aims for them).</summary>
        void NoteWish(Vector3 direction, float speed)
        {
            _wishThisFrame = true;
            _wishDir = direction;
            _wishSpeed = speed;
        }

        /// <summary>Before the frame's work: the mode, and under Inertia her body's velocity as the agent moved it (walls and edges slow it).</summary>
        void PrecisionBefore()
        {
            _pmode = PrecisionSettings.ModeNow;
            _wishThisFrame = false;
            if (_pmode == PrecisionMode.Inertia && Time.deltaTime > 0f && !_dashing && !_jumping) _bodyVelocity = Flat(_agent.velocity);
        }

        /// <summary>
        /// After the frame's work: Inertia moves her body; the meter, the ghost and her motion follow her velocity; a counter-strafe is
        /// told; the share the accuracy reads is kept, and its lowest while a basic shot winds up.
        /// </summary>
        void PrecisionAfter()
        {
            if (_pmode == PrecisionMode.Off)
            {
                _wasDashing = _dashing;
                _heldBefore = _moveInput;
                return;
            }
            PrecisionValues v = PV;
            float dt = Time.deltaTime, now = Time.time, run = RunSpeedNow;
            bool dashEnded = _wasDashing && !_dashing && _dash.Progress(now) >= 1f; // its own end, not a teleport out of it
            Vector3 dashDir = Flat(_dashTo - _dashFrom);
            dashDir = dashDir.sqrMagnitude > 1e-6f ? dashDir.normalized : Vector3.zero;
            _wasDashing = _dashing;
            if (_pmode == PrecisionMode.Inertia) InertiaBody(dt, dashEnded ? dashDir * run : (Vector3?)null);

            // Her real velocity this frame; in a dash nothing follows it, and it exits at run speed along it.
            Vector3 real = Flat(Velocity);
            if (dt > 0f && !_dashing)
            {
                if (dashEnded) real = dashDir * run;
                _motion += (real - _motion) * (1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, v.MotionSeconds)));
                // A counter-strafe: a new held way against her motion of the last 0.1 s, that motion above 35 %.
                if (_moveInput != _heldBefore && _moveInput != Vector3.zero
                    && PrecisionRules.IsCounterStrafe(new GroundPoint(_moveInput.x, _moveInput.z), new GroundPoint(_motion.x, _motion.z), run, v))
                {
                    CounterStrafes++;
                    _lastCounterStrafeAt = now;
                    if (_pmode == PrecisionMode.Meter) _momentum = 0f; // a counter-tap empties it at once
                    Report(new PlayerReport { Kind = PlayerReportKind.CounterStrafed, Share = _motion.magnitude / run });
                }
                float realShare = real.magnitude / run;
                float settle = Modifiers != null ? Modifiers.Stats.PrecisionSettle : 1f;
                if (_pmode == PrecisionMode.Meter) _momentum = PrecisionRules.MeterStep(_momentum, realShare, dt, v, AccurateShareNow, settle);
                else if (_pmode == PrecisionMode.Ghost)
                {
                    if (dashEnded) _ghostVelocity = real;
                    float gx = _ghostVelocity.x, gz = _ghostVelocity.z;
                    PrecisionRules.GhostFrame(ref gx, ref gz, real.x, real.z, dt, v, settle);
                    _ghostVelocity = new Vector3(gx, 0f, gz);
                }
            }
            _heldBefore = _moveInput;
            _precisionShare = _pmode == PrecisionMode.Meter ? PrecisionRules.MeterShare(_momentum, AccurateShareNow)
                            : _pmode == PrecisionMode.Ghost ? _ghostVelocity.magnitude / run
                            : Flat(Velocity).magnitude / run;
            if (dt > 0f && _precisionShare >= v.StopShotRunShare) _ranAt = now;
            // Her lowest while a basic shot winds up (only frames in which time ran: a hit stop's frames read standing).
            if (_windUp && _actionKind == ActionKind.BasicShot && !_action.HitResolved && dt > 0f) _lowestInWindUp = Mathf.Min(_lowestInWindUp, _precisionShare);
        }

        /// <summary>Inertia: her body by the law, toward the way and cap the frame asked for, or coasting; handed to the agent.</summary>
        void InertiaBody(float dt, Vector3? dashExit)
        {
            if (dashExit.HasValue) _bodyVelocity = dashExit.Value;
            bool free = _agent.isOnNavMesh && !_dashing && !_jumping && !_held.IsHeld && !_keyMoving && !_inCover && !_duck.IsDown
                        && (_actionKind != ActionKind.Skill || IsInLightSkill || (_sliding && !_castMoves));
            if (!free)
            {
                if (!_dashing) _bodyVelocity = Flat(Velocity);
                return;
            }
            float vx = _bodyVelocity.x, vz = _bodyVelocity.z;
            if (_wishThisFrame) PrecisionRules.InertiaFrame(ref vx, ref vz, _wishDir.x, _wishDir.z, _wishSpeed, dt, PV);
            else PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 0f, 0f, dt, PV);
            _bodyVelocity = new Vector3(vx, 0f, vz);
            float speed = _bodyVelocity.magnitude;
            if (speed < InertiaRest)
            {
                _bodyVelocity = Vector3.zero;
                if (_agent.hasPath || _agent.velocity.sqrMagnitude > 0f) Halt();
                return;
            }
            // As MoveAlong steers her: a little way ahead, now along her body's own way at its own speed.
            Vector3 way = _bodyVelocity / speed;
            _agent.isStopped = false;
            _agent.SetDestination(transform.position + way * SteerLookAhead);
            _agent.speed = speed;
            _agent.velocity = _bodyVelocity;
        }

        /// <summary>m/s under which Inertia's body stands.</summary>
        const float InertiaRest = 0.01f;

        /// <summary>A basic shot starts: the share at the press, and its lowest from here until it goes off.</summary>
        void PrecisionShotStarted()
        {
            if (_pmode == PrecisionMode.Off) return;
            _windUp = true;
            _shareAtPress = _precisionShare;
            _lowestInWindUp = _precisionShare;
            _ranAtPress = _ranAt;
        }

        /// <summary>
        /// The basic shot goes off (from <see cref="ResolveBasicShot"/>, before anything is worked out from its aim): its aim turned by a
        /// draw inside its cone, the cone read at the lowest share of its wind-up. Off, nothing; an accurate shot is not drawn.
        /// </summary>
        void DeflectBasicShot()
        {
            if (_pmode == PrecisionMode.Off)
            {
                LastPrecisionShot = default;
                return;
            }
            float share = _windUp ? Mathf.Min(_lowestInWindUp, _precisionShare) : _precisionShare;
            _windUp = false;
            float cone = ConeFor(share);
            GroundPoint origin = GroundPosition;
            var straight = new GroundPoint(_aimPoint.x, _aimPoint.z);
            Damageable straightHit = BasicShotEnd(origin, straight, out float range, out _);
            float nearest = NearestToTheAim(origin, straight, range);
            float deflection = 0f;
            bool coneMiss = false;
            if (cone > 0f)
            {
                if (_bag == null) SeedPrecision(PV.PracticeSeed);
                deflection = _bag.Next() * cone * 0.5f;
                GroundPoint dir = PrecisionRules.Turn(LineShot.Direction(origin, straight, new GroundPoint(0f, 1f)), deflection);
                _aimPoint = transform.position + new Vector3(dir.X, 0f, dir.Z) * BasicShotRange;
                Damageable hit = BasicShotEnd(origin, new GroundPoint(_aimPoint.x, _aimPoint.z), out _, out _);
                coneMiss = straightHit != null && hit == null;
                if (coneMiss) ConeMisses++;
            }
            float now = Time.time;
            LastPrecisionShot = new PrecisionShot
            {
                Mode = _pmode, ShareAtPress = _shareAtPress, ShareLowest = share, Momentum = _momentum, HiddenSpeed = _ghostVelocity.magnitude,
                Speed = Flat(Velocity).magnitude, Cone = cone, Deflection = deflection, NearestDistance = nearest,
                StraightHit = straightHit != null, ConeMiss = coneMiss,
                SinceCounterStrafe = float.IsNegativeInfinity(_lastCounterStrafeAt) ? float.NaN : now - _lastCounterStrafeAt,
                StoppedAfterARun = cone <= 0f && _shotStartedAt - _ranAtPress <= PV.StopShotRunWithin,
            };
        }

        /// <summary>Metres from her to the living target whose centre lies nearest the straight aim's line within its reach; NaN for none.</summary>
        float NearestToTheAim(GroundPoint origin, GroundPoint aim, float range)
        {
            GroundPoint toAim = aim - origin;
            float length = toAim.Length;
            if (length < 1e-5f) return float.NaN;
            GroundPoint dir = toAim * (1f / length);
            IReadOnlyList<Damageable> all = Damageable.All;
            float best = float.PositiveInfinity, at = float.NaN;
            for (int i = 0; i < all.Count; i++)
            {
                TargetInfo t = all[i].Info;
                if (!t.Alive) continue;
                GroundPoint to = t.Position - origin;
                float along = to.Dot(dir);
                if (along < 0f || along > range + t.Radius) continue;
                float off = (to - dir * along).Length;
                if (off < best)
                {
                    best = off;
                    at = to.Length;
                }
            }
            return at;
        }
    }
}
