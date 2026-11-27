using System;
using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The hard boss, stage 1 in grey boxes (work order 147, 2026-11-27; <see cref="BossValues.Trial"/>, its numbers
    /// <see cref="BossTrialValues"/>, its choices <see cref="BossTrialChooser"/>). Kristian: "bossen skal være veldig vanskelig. masse
    /// mechanics og ting å dodge. nå sto jeg basicly bare stille og høyre cklikket." In use only when <see cref="BossTrialSettings.InUse"/>
    /// as it is made; else nothing here runs and the boss is work order 146's, field for field.
    ///
    /// The frame: 52,000 health (section 7.1 said 45,000; raised by the bots), three phases (100 to 65 %, 65 to 30 %, 30 to 0 %), each line crossed by a hit that counts, then 2.5 s
    /// it stands untouchable, grey, roaring, its room's lamps recoloured; nothing begins before 2 s after its waking, nor while its body
    /// is off his screen (it walks until it is in view), nor within the phase's gap after a recovery. Today's anger, its Spin and its
    /// packs are not part of it.
    ///
    /// The seven attacks, each behind its own switch: Cleave and Wave (the cone, then a band down its line whose path is drawn as it
    /// leaves); Charge (blue, counterable; a crash into a pillar or wall dazes it 2 s, taking 1.5 times); Grave Marks (four led circles
    /// on her, 0.45 s apart, while it walks at half speed); Spokes (it plants and stands: six strips fill and burst, then six more
    /// turned half a gap); Slam and Shockwave (the leap to where she stood, then a ring that grows to 16 m and stops at pillars and
    /// walls, to be dashed through); Seeds (three grey spheres round her whose circles fill 4 s, to be shot or left); Judgement and Ruin
    /// (at 30 % and 15 %: it walks to the room's middle and kneels behind a ward of 4,000 while a gold circle closes on it for 8 s;
    /// broken, it is dazed 4 s taking 1.5 times; not, Ruin: the floor outlined red to the walls and each pillar's shadow drawn pale, and
    /// she has the template's time to reach one).
    ///
    /// Every shape is drawn by <see cref="BossTrialLines"/> from the very numbers its hit is judged by.
    /// </summary>
    public sealed partial class EnemyBoss
    {
        // ---------------- The trial's state ----------------

        BossTrialValues _tv;
        EnemyValues[] _tvValues;                 // its brain's attacks by phase, 1 to 3 (each phase's wind-ups)
        BossTrialChooser _chooser;
        System.Random _tvRandom;                 // the seeds' places, from the same seed as the draws
        int _phase = 1;
        bool _trialOn;                           // its fight is on: woken, and not over
        float _trialWoke = -1f, _lastEnded, _lastBigEnded = float.NegativeInfinity, _lastDashOnly = float.NegativeInfinity;
        float _standUntil = -1f;                 // a transition runs until then
        int _judgementsCome;
        EnemyState _stateSeen;
        bool _dazedSeen;
        BossTrialAttack _running;                // the attack wound up or recovered from now
        bool _comboCharge, _comboSeeds;          // phase 3's combos, waiting
        StaggerBar _tvHeldStagger;
        BossTrialLines _lines;
        HurtGuard _guard;
        float _lastDashPress = float.NegativeInfinity;
        Damageable _lastShotTarget;
        Action<PlayerReport> _onHer;
        Action _onHerDeath;
        Action<int> _onWarded, _onDamagedInWard;
        bool _herListened;
        bool _diedPending;

        // The room.
        bool _roomKnown;
        Vector3 _roomMin, _roomMax, _roomMiddle;
        string _template = "";

        /// <summary>True when this boss is the trial fight (decided as it is made).</summary>
        public bool InTrial => _tv != null;
        /// <summary>The trial's numbers, or null.</summary>
        public BossTrialValues TrialValues => _tv;
        /// <summary>Its phase in the trial, 1 to 3; 0 when it is not the trial.</summary>
        public int TrialPhase => _tv != null ? _phase : 0;
        /// <summary>True while a phase's transition holds it.</summary>
        public bool InTransition => _standUntil >= 0f;
        /// <summary>The trial's chooser (for tests).</summary>
        public BossTrialChooser TrialChooser => _chooser;
        /// <summary>The trial attack being wound up or recovered from, or None.</summary>
        public BossTrialAttack TrialAttack => _tv != null && State != EnemyState.Chase && State != EnemyState.Idle ? _running : BossTrialAttack.None;
        /// <summary>The throne template its room was stamped from, or "" (the practice room).</summary>
        public string TrialTemplate => _template;
        /// <summary>s since her last dash press, or a negative number when there was none.</summary>
        public float TrialSecondsSinceDash => float.IsNegativeInfinity(_lastDashPress) ? -1f : Time.time - _lastDashPress;
        /// <summary>The trial's drawings (for tests: none is made in a frame once warm).</summary>
        public BossTrialLines TrialLines => _lines;
        /// <summary>Is its whole body seen on his screen: inside the view and clear of the interface's plates (<see cref="BossTrialScreen.Seen"/>)?</summary>
        public bool TrialSeen => _tv == null || BossTrialScreen.Seen(transform.position, Tuning.BodyRadius, Tuning.BodyHeight, _tv.SeenMarginPx);
        /// <summary>The middle of its room (the throne room's floor; round its home in the practice room).</summary>
        public Vector3 TrialRoomMiddle => _roomMiddle;
        /// <summary>When the last recovery (or transition, or Judgement) ended.</summary>
        public float TrialLastEnded => _lastEnded;

        // ---------------- Made, started, woken ----------------

        /// <summary>From Awake, before Enemy's (which reads <see cref="Values"/>): is it the trial, and its attacks for each phase.</summary>
        void TrialAwake()
        {
            if (!BossTrialSettings.InUse) return;
            _tv = new BossTrialValues();
            _tvValues = new EnemyValues[4];
            for (int p = 1; p <= 3; p++) _tvValues[p] = TrialAttacks(p);
            _tvValues[0] = _tvValues[1];
        }

        EnemyValues TrialAttacks(int phase)
        {
            BossTrialValues v = _tv;
            float W(float one, float floor) => BossTrialRules.Windup(one, floor, phase, v);
            return new EnemyValues
            {
                Health = v.Health, MoveSpeed = v.MoveSpeed, AggroRange = Tuning.Enemy.AggroRange, LeashRange = Tuning.Enemy.LeashRange,
                RespawnDelay = Tuning.Enemy.RespawnDelay, UseModels = Tuning.Enemy.UseModels,
                Attacks = new[]
                {
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.Cleave, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = v.CleaveWithin,
                        Windup = W(v.CleaveWindup, v.CleaveWindupFloor), Recovery = v.CleaveRecovery, Damage = v.DamageStandard,
                        Length = v.CleaveLength, Angle = v.CleaveAngle,
                    },
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.Charge, Shape = EnemyAttackShape.Line, MinDistance = v.ChargeFrom, MaxDistance = v.ChargeTo,
                        Windup = W(v.ChargeWindup, v.ChargeWindupFloor), Recovery = v.ChargeRecovery, Damage = v.DamageHeavy,
                        Length = v.ChargeLength, HalfWidth = v.ChargeHalfWidth, Rush = v.ChargeRush, Counterable = true,
                    },
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.GraveMarks, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 1000f,
                        Windup = BossTrialRules.MarksWhole(W(v.MarkBurst, v.MarkBurstFloor), v), Recovery = v.MarksRecovery,
                        Damage = v.DamageLight, Radius = v.MarkRadius,
                    },
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.Spokes, Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = v.SpokesWithin,
                        Windup = W(v.SpokeFirstFill, v.SpokeFillFloor) + W(v.SpokeSecondFill, v.SpokeFillFloor), Recovery = v.SpokesRecovery,
                        Damage = v.DamageStandard, Length = v.SpokeReach, HalfWidth = v.SpokeHalfWidth,
                    },
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.Slam, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = v.SlamWithin,
                        Windup = W(v.SlamWindup, v.SlamWindupFloor), Recovery = v.SlamRecovery, Damage = v.DamageHeavy, Radius = v.SlamRadius,
                    },
                    new EnemyAttackDefinition
                    {
                        Name = BossTrialValues.Seeds, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 1000f,
                        Windup = v.SeedThrowWindup, Recovery = v.SeedsRecovery, Damage = v.DamageStandard, Radius = v.SeedCircle,
                    },
                },
            };
        }

        /// <summary>The trial's attack of this kind, in its phase now.</summary>
        public EnemyAttackDefinition TrialDef(BossTrialAttack a)
        {
            if (_tv == null) return null;
            EnemyAttackDefinition[] all = _tvValues[Mathf.Clamp(_phase, 1, 3)].Attacks;
            switch (a)
            {
                case BossTrialAttack.Cleave: return all[0];
                case BossTrialAttack.Charge: return all[1];
                case BossTrialAttack.GraveMarks: return all[2];
                case BossTrialAttack.Spokes: return all[3];
                case BossTrialAttack.Slam: return all[4];
                case BossTrialAttack.Seeds: return all[5];
                default: return null;
            }
        }

        /// <summary>Which trial attack a definition is (by its name).</summary>
        public static BossTrialAttack TrialKindOf(EnemyAttackDefinition a)
        {
            if (a == null) return BossTrialAttack.None;
            switch (a.Name)
            {
                case BossTrialValues.Cleave: return BossTrialAttack.Cleave;
                case BossTrialValues.Charge: return BossTrialAttack.Charge;
                case BossTrialValues.GraveMarks: return BossTrialAttack.GraveMarks;
                case BossTrialValues.Spokes: return BossTrialAttack.Spokes;
                case BossTrialValues.Slam: return BossTrialAttack.Slam;
                case BossTrialValues.Seeds: return BossTrialAttack.Seeds;
                default: return BossTrialAttack.None;
            }
        }

        /// <summary>Is this the Ground Slam, the finale's or the trial's?</summary>
        bool IsSlam(EnemyAttackDefinition a) => a != null && (a == Tuning.Phases.Slam || (_tv != null && a.Name == BossTrialValues.Slam));

        // The definitions the patterns strike with (their names and damage, for her health and the log).
        EnemyAttackDefinition _waveDef, _markDef, _ringDef, _seedDef, _ruinDef;

        /// <summary>From Start: the trial's health, whatever the floor or the practice room set (the practice room's boss too, at full).</summary>
        void TrialStart()
        {
            if (_tv == null) return;
            _self.SetMaxHealth(_tv.Health);
            _waveDef = new EnemyAttackDefinition { Name = BossTrialValues.Wave, Shape = EnemyAttackShape.Line, Damage = _tv.DamageLight, Length = _tv.WaveFlight, HalfWidth = _tv.WaveWidth * 0.5f };
            _markDef = new EnemyAttackDefinition { Name = BossTrialValues.GraveMark, Shape = EnemyAttackShape.Circle, Damage = _tv.DamageLight, Radius = _tv.MarkRadius };
            _ringDef = new EnemyAttackDefinition { Name = BossTrialValues.Shockwave, Shape = EnemyAttackShape.Circle, Damage = _tv.DamageLight, Radius = _tv.RingTo };
            _seedDef = new EnemyAttackDefinition { Name = BossTrialValues.Seed, Shape = EnemyAttackShape.Circle, Damage = _tv.DamageStandard, Radius = _tv.SeedCircle };
            _ruinDef = new EnemyAttackDefinition { Name = BossTrialValues.Ruin, Shape = EnemyAttackShape.Circle, Damage = _tv.DamageFailedCheck, Radius = 100f };
            for (int i = 0; i < _marks.Length; i++) _marks[i] = new Mark();
            for (int i = 0; i < _seeds.Length; i++) _seeds[i] = new SeedThing();
        }

        /// <summary>From OnWoke: its fight begins.</summary>
        void TrialWoke()
        {
            if (_tv == null) return;
            float now = Time.time;
            _trialOn = true;
            _herDied = _diedPending = false;
            _trialWoke = now;
            _lastEnded = now;
            _phase = BossTrialRules.PhaseOf(HealthShare, _tv);
            int seed = BossTrialTesting.Seed ?? Environment.TickCount;
            _chooser = new BossTrialChooser(seed);
            _tvRandom = new System.Random(seed ^ 0x5EED);
            _chooser.EnterPhase(_phase);
            if (_lines == null) _lines = new BossTrialLines(Telegraph, transform.parent, 48);
            FindRoom();
            ListenToHer(true);
            if (PlayerHealth != null)
            {
                _guard = HurtGuard.On(PlayerHealth);
                _guard.SetActive(true);
            }
            BeginRecord(now);
        }

        /// <summary>Its room: the throne room's floor (its corners, its middle, its template), or the ground round its home in the practice room.</summary>
        void FindRoom()
        {
            if (_roomKnown) return;
            _roomKnown = true;
            GeneratedLevel level = InRoom != null ? GetComponentInParent<GeneratedLevel>() : null;
            LevelRoom room = level != null ? level.RoomAt(Home) : null;
            if (room != null)
            {
                double cs = level.Layout.CellSize;
                Vector3 a = level.ToWorld(new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs));
                Vector3 b = level.ToWorld(new LevelPoint((room.Rect.X + room.Rect.Width) * cs, (room.Rect.Y + room.Rect.Height) * cs));
                _roomMin = new Vector3(Mathf.Min(a.x, b.x), 0f, Mathf.Min(a.z, b.z));
                _roomMax = new Vector3(Mathf.Max(a.x, b.x), 0f, Mathf.Max(a.z, b.z));
                _template = room.Template?.Template?.Name ?? "";
            }
            else
            {
                Vector3 h = Flat(transform.position); // where it woke
                float half = PracticeHalf;
                _roomMin = h - new Vector3(half, 0f, half);
                _roomMax = h + new Vector3(half, 0f, half);
            }
            _roomMiddle = (_roomMin + _roomMax) * 0.5f;
            FloorCells();
        }

        /// <summary>m: in the practice room (no walls), the half side of the square round where it woke taken as its room.</summary>
        const float PracticeHalf = 16f;

        void ListenToHer(bool on)
        {
            if (Player == null) return;
            if (on && !_herListened)
            {
                _herListened = true;
                _onHer = OnHer;
                Player.Reported += _onHer;
                if (PlayerHealth != null)
                {
                    _onHerDeath = OnHerDeath;
                    PlayerHealth.Died += _onHerDeath;
                }
                Reported += OnTrialReport;
            }
            else if (!on && _herListened)
            {
                _herListened = false;
                Player.Reported -= _onHer;
                if (PlayerHealth != null && _onHerDeath != null) PlayerHealth.Died -= _onHerDeath;
                Reported -= OnTrialReport;
            }
        }

        void OnHer(PlayerReport r)
        {
            if (!_trialOn) return;
            if (r.Kind == PlayerReportKind.DashPressed) _lastDashPress = Time.time;
            else if (r.Kind == PlayerReportKind.Hit && r.Slot == -1) _lastShotTarget = r.Target;
            else if (r.Kind == PlayerReportKind.ShotFired)
            {
                ShotAtSeeds(r.Landed ? _lastShotTarget : null);
                _lastShotTarget = null;
            }
        }

        void OnHerDeath()
        {
            _diedPending = true;
            _herDied = true;
        }
        bool _herDied;

        // ---------------- Every frame ----------------

        /// <summary>Every frame alive, before it thinks (from Update): the transition, Judgement, every pattern, the record.</summary>
        void TrialTick()
        {
            if (_tv == null) return;
            float now = Time.time;
            if (_trialOn && (!IsAwake || _herDied)) EndTrialFight(BossFightEvents.HerDeath); // her death ends it (in the practice room she stands up again)
            if (!_trialOn) return;
            // The gap runs from the end of each recovery, and of each daze.
            EnemyState state = State;
            if (_stateSeen == EnemyState.Recover && state != EnemyState.Recover) AttackEnded(now);
            _stateSeen = state;
            bool dazed = IsDazed;
            if (_dazedSeen && !dazed) _lastEnded = Mathf.Max(_lastEnded, now);
            _dazedSeen = dazed;
            if (_standUntil >= 0f && now >= _standUntil) EndTransition(now);
            if (_standUntil < 0f && _judgement == JudgementStage.None && state == EnemyState.Chase && !dazed && BossTrialValues.JudgementOn && _phase >= 3
                && BossTrialRules.JudgementDue(HealthShare, _judgementsCome, _tv) >= 0 && !GroundLive())
                BeginJudgement(now);
            TrialFrame(now);
        }

        /// <summary>
        /// One frame of the trial's patterns and drawings, alive or dazed: the circles, the spokes, the wave, the ring, the seeds,
        /// Judgement, and the record. Public for the test that it allocates nothing.
        /// </summary>
        public void TrialFrame(float now)
        {
            if (_tv == null || Player == null) return;
            TickMarks(now);
            TickSpokes(now);
            TickWave(now);
            TickRing(now);
            TickSeeds(now);
            TickJudgement(now);
            Sample(now);
        }

        void AttackEnded(float now)
        {
            _lastEnded = now;
            if (BossTrialRules.IsBig(_running) && _running != BossTrialAttack.Seeds && _running != BossTrialAttack.Slam) _lastBigEnded = now;
            if (_running == BossTrialAttack.Spokes) EndSpokes();
            if (_running == BossTrialAttack.Slam && _phase >= 3 && BossTrialValues.SeedsOn) _comboSeeds = true;   // phase 3: Slam then Seeds
            _running = BossTrialAttack.None;
        }

        /// <summary>Is a pattern still on the ground: a circle, a seed, the ring, the wave, the spokes, Judgement?</summary>
        public bool GroundLive()
        {
            if (_ring.Live || _wave.Live || _spokes.Live || _judgement != JudgementStage.None) return true;
            for (int i = 0; i < _marks.Length; i++) if (_marks[i] != null && _marks[i].Live) return true;
            for (int i = 0; i < _seeds.Length; i++) if (_seeds[i] != null && _seeds[i].Live) return true;
            return false;
        }

        // ---------------- Its phases ----------------

        /// <summary>From Temper, in place of the anger: a line crossed begins a transition (the hit that crossed it counted).</summary>
        void TrialTemper()
        {
            if (!_trialOn) return;
            int phase = BossTrialRules.PhaseOf(HealthShare, _tv);
            if (phase > _phase) BeginTransition(phase, Time.time);
        }

        void BeginTransition(int phase, float now)
        {
            _phase = phase;
            _phaseStarted[Mathf.Clamp(phase, 1, 3)] = now;
            _standUntil = now + Mathf.Max(0f, _tv.TransitionSeconds);
            _self.Untouchable = true; // nothing after the crossing hit counts
            if (_tvHeldStagger == null && _self.Stagger != null)
            {
                _tvHeldStagger = _self.Stagger;
                _self.Stagger = null;
            }
            _brain.Cancel(now);
            _rushing = false;
            _chargesLeft = 0;
            if (_slamming) EndSlam(transform.position);
            ClearPatterns();
            _comboCharge = _comboSeeds = false;
            _running = BossTrialAttack.None;
            PlaceholderSounds.Current?.AskFor(PlaceholderSound.BossRoar);
            Rgb lamps = phase >= 3 ? _tv.PhaseThreeLamps : _tv.PhaseTwoLamps;
            LampsAnswered = BossRoomLight.Answer(this, lamps);
            Tint(new Color(_tv.TransitionTint.R, _tv.TransitionTint.G, _tv.TransitionTint.B));
            NoteTrial(BossTrialLog.Phase, null, "share", Text(HealthShare));
        }

        void EndTransition(float now)
        {
            _standUntil = -1f;
            _self.Untouchable = false;
            if (_tvHeldStagger != null && _self.Stagger == null) _self.Stagger = _tvHeldStagger;
            _tvHeldStagger = null;
            Tint(BodyColor);
            _chooser.EnterPhase(_phase);
            _lastEnded = now;
        }

        void Tint(Color c)
        {
            if (BodyRenderer != null && !IsDazed) EnemyFactory.Paint(BodyRenderer.sharedMaterial, c);
        }

        bool TrialMayAttack() => _standUntil < 0f && _judgement == JudgementStage.None;

        /// <summary>From Chase: where it walks in the trial, when not toward her. True when this decided.</summary>
        bool TrialChase(Vector3 toPlayer)
        {
            if (_standUntil >= 0f || _judgement == JudgementStage.Ward || _judgement == JudgementStage.Ruin || _judgement == JudgementStage.Dazed)
            {
                Halt();
                Face(toPlayer);
                return true;
            }
            if (_judgement == JudgementStage.Walk)
            {
                Walk(_kneel);
                return true;
            }
            return false;
        }

        // ---------------- Choosing ----------------

        EnemyAttackDefinition TrialChoose(float distance)
        {
            if (!_trialOn || Player == null || _standUntil >= 0f || _judgement != JudgementStage.None || BossTrialTesting.HoldChoices) return null;
            float now = Time.time;
            var s = new BossTrialSituation
            {
                Now = now, Phase = _phase, WokeAt = _trialWoke, LastEnded = _lastEnded, Distance = distance,
                OnScreen = TrialSeen, // its whole body seen: in the view and clear of her plates (the second round)
                LastBigEnded = _lastBigEnded, LastDashOnly = _lastDashOnly,
            };
            if (!BossTrialChooser.Ready(s, _tv)) return null;
            bool sees = Sees(Player.Position);
            EnemyAttackDefinition charge = TrialDef(BossTrialAttack.Charge);
            s.ChargeFits = sees && (distance < charge.MinDistance || distance > charge.MaxDistance || ChargeReachesHer(charge, distance));
            s.SlamFits = distance <= _tv.SlamWithin && (sees || distance <= Tuning.Phases.SlamSightFrom);
            s.GroundLive = GroundLive();
            // Phase 3's combos come first, each as soon as it may.
            if (_comboCharge)
            {
                _comboCharge = false;
                if (sees && now - _chooser.LastStart(BossTrialAttack.Charge) >= _tv.ChargeEvery - 1e-4f && ChargeReachesHer(charge, distance))
                    return Chosen(BossTrialAttack.Charge, now);
            }
            if (_comboSeeds && !_ring.Live)
            {
                _comboSeeds = false;
                if (!s.GroundLive) return Chosen(BossTrialAttack.Seeds, now);
            }
            return Chosen(_chooser.Choose(s, _tv), now, counted: true);
        }

        EnemyAttackDefinition Chosen(BossTrialAttack a, float now, bool counted = false)
        {
            if (a == BossTrialAttack.None) return null;
            if (!counted) _chooser.Began(a, now);
            _running = a;
            return TrialDef(a);
        }

        /// <summary>For tests: begins this attack's wind-up now, at her, whatever it was doing (awake, not dazed). Judgement too.</summary>
        public bool TrialBegin(BossTrialAttack a)
        {
            if (_tv == null || !_trialOn || IsDead || IsDazed || State == EnemyState.Idle || Player == null) return false;
            float now = Time.time;
            if (a == BossTrialAttack.Judgement)
            {
                BeginJudgement(now);
                return true;
            }
            EnemyAttackDefinition def = TrialDef(a);
            if (def == null) return false;
            _chooser.Began(a, now);
            _running = a;
            BeginWindup(def, Player.Position);
            return true;
        }

        /// <summary>Its wind-up began (the aim locked): each attack's own start.</summary>
        void TrialWindupStarted(EnemyAttackDefinition attack)
        {
            float now = Time.time;
            BossTrialAttack a = TrialKindOf(attack);
            _running = a;
            NoteTell(a, now);
            switch (a)
            {
                case BossTrialAttack.GraveMarks:
                    _marksStart = now;
                    _marksPlaced = 0;
                    _marksBurst = BossTrialRules.Windup(_tv.MarkBurst, _tv.MarkBurstFloor, _phase, _tv);
                    break;
                case BossTrialAttack.Spokes:
                    BeginSpokes(now);
                    break;
                case BossTrialAttack.Slam:
                    _lastDashOnly = now;
                    break;
            }
        }

        /// <summary>At the strike: true when the pattern strikes itself (Grave Marks, Spokes, Seeds); the Cleave sends its Wave; the Slam its ring.</summary>
        bool TrialStrikesItself(EnemyAttackDefinition attack)
        {
            switch (TrialKindOf(attack))
            {
                case BossTrialAttack.Cleave:
                {
                    SendTrialWave(attack);
                    // Phase 3: Cleave straight into Charge, its recovery cut short (when a Charge may come: its 6 s since the last).
                    float now = Time.time;
                    if (_phase >= 3 && BossTrialValues.ChargeOn && now - _chooser.LastStart(BossTrialAttack.Charge) >= _tv.ChargeEvery - 1e-4f)
                    {
                        _comboCharge = true;
                        _brain.HoldRecovery(now - Mathf.Max(0f, attack.Recovery - _tv.ComboCleaveRecovery));
                    }
                    return false;
                }
                case BossTrialAttack.Slam:
                    BeginRing(Flat(_slamTo), Time.time);
                    return false;
                case BossTrialAttack.GraveMarks:
                case BossTrialAttack.Spokes:
                    return true;
                case BossTrialAttack.Seeds:
                    ThrowSeeds(Time.time);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// From EndCharge: the trial's charge ended. A crash (7.2) dazes it <see cref="BossTrialValues.CrashDaze"/>, taking 1.5 times:
        /// its line was cut short, and a wall or tall prop stands within its body and a little in front of it where it stopped
        /// (<see cref="BossTrialRules.Crashed"/>). No angry second charge.
        /// </summary>
        void TrialChargeEnded()
        {
            if (_rush == null) return;
            Vector3 at = Flat(transform.position), dir = Flat(_rushDir).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (Tuning.BodyRadius * _tv.CrashSideShare);
            float look = Tuning.BodyRadius + _tv.CrashSlack + 0.5f;
            float ahead = Mathf.Min(Cover.ClearDistance(at, dir, look, Tuning.SightHeight),
                                    Mathf.Min(Cover.ClearDistance(at + side, dir, look, Tuning.SightHeight), Cover.ClearDistance(at - side, dir, look, Tuning.SightHeight)));
            bool crashed = BossTrialRules.Crashed(_chargeReach, _rush.Length, ahead, Tuning.BodyRadius, _tv);
            NoteTrial(BossTrialLog.ChargeEnd, BossTrialValues.Charge, "crash", crashed ? "yes" : "no", "ran", Text(_chargeReach), "ahead", Text(ahead));
            if (!crashed) return;
            Crashes++;
            DazeTrial(_tv.CrashDaze);
        }

        /// <summary>Charges that ended in a crash this fight.</summary>
        public int Crashes { get; private set; }

        /// <summary>Dazed now for this long, taking its stagger bar's 1.5 times (the crash, the broken ward).</summary>
        void DazeTrial(float seconds)
        {
            StaggerBar bar = _self.Stagger ?? _tvHeldStagger;
            if (bar == null) return;
            if (_self.Stagger == null) { _self.Stagger = bar; _tvHeldStagger = null; }
            bar.DazeFor(seconds, Time.time);
            _self.DazeFor(0f); // tells the body: the brain drops what it did, and it stands until the daze ends
        }

        /// <summary>From OnDaze: what was being wound up is dropped; what is on the ground stays.</summary>
        void TrialOnDaze()
        {
            if (_running == BossTrialAttack.Spokes) EndSpokes();
            _marksStart = -1f;
            _running = BossTrialAttack.None;
            _comboCharge = _comboSeeds = false;
        }

        /// <summary>The trial's patterns are read on the ground, drawn by this file: no telegraph of the brain's round it (and the effects' glint on its body).</summary>
        public override bool ReadOnBody => _tv != null && State == EnemyState.Windup
                                           && (_running == BossTrialAttack.GraveMarks || _running == BossTrialAttack.Spokes || _running == BossTrialAttack.Seeds);

        /// <summary>Grave Marks: it keeps walking after her through their wind-up (at half speed, <see cref="TrialAfterMove"/>).</summary>
        protected override bool ChasesThrough(EnemyAttackDefinition attack) => _tv != null && TrialKindOf(attack) == BossTrialAttack.GraveMarks;

        /// <summary>Grave Marks: it walks after her at half speed while it places them.</summary>
        void TrialAfterMove()
        {
            if (_running == BossTrialAttack.GraveMarks && State == EnemyState.Windup && _agent.enabled) _agent.speed = Values.MoveSpeed * _tv.MarksWalkShare;
        }

        // ---------------- 1. The Wave ----------------

        sealed class Band
        {
            public bool Live, Spared, Landed, Avoided, PathShown;
            public Vector3 From, Dir;
            public float Start, Flight, Before;
            public LineRenderer Path, Front;
        }
        readonly Band _wave = new Band();
        Color WaveColour => new Color(_tv.WaveColour.R, _tv.WaveColour.G, _tv.WaveColour.B);

        void SendTrialWave(EnemyAttackDefinition cleave)
        {
            if (Player == null) return;
            Vector3 dir = AimDirection();
            Vector3 from = Flat(transform.position) + dir * Tuning.BodyRadius;
            bool cleaved = EnemyBrain.Hits(cleave, Ground(transform.position), Ground(AimPoint), Player.GroundPosition, Placeholders.PlayerRadius, cleave.Length);
            EndWave();
            _wave.Live = true;
            _wave.Spared = cleaved; // one swing hurts her once (as today's wave)
            _wave.Landed = _wave.Avoided = false;
            _wave.From = from;
            _wave.Dir = dir;
            _wave.Start = Time.time;
            _wave.Before = 0f;
            _wave.Flight = Mathf.Min(_tv.WaveFlight, Cover.ClearDistance(from, dir, _tv.WaveFlight, Tuning.SightHeight));
            _wave.Path = null; // drawn a moment after the strike (TickWave)
            _wave.PathShown = false;
            _wave.Front = _lines.Take(WaveColour, false);
        }

        void TickWave(float now)
        {
            if (!_wave.Live) return;
            float since = now - _wave.Start;
            if (!_wave.PathShown && since >= _tv.WavePathAfter)
            {
                _wave.PathShown = true;
                _wave.Path = _lines.Take(WaveColour, true);
                _lines.Strip(_wave.Path, _wave.From, _wave.Dir, _wave.Flight, _tv.WaveWidth * 0.5f);
                if (_wave.Path != null) _wave.Path.widthMultiplier = BossTrialLines.Thick;
            }
            if (_wave.Path != null && since >= _tv.WavePathAfter + _tv.WaveDrawn) _wave.Path = _lines.Give(_wave.Path);
            float at = Mathf.Min(_wave.Flight, since * _tv.WaveSpeed);
            if (_wave.Front != null)
            {
                // Its front filled: a band across its whole width, as deep as the part that hits (a thing coming, not an outline).
                Vector3 side = Vector3.Cross(Vector3.up, _wave.Dir) * (_tv.WaveWidth * 0.5f), mid = _wave.From + _wave.Dir * at;
                _lines.Band(_wave.Front, mid - side, side.normalized, _tv.WaveWidth);
                _wave.Front.widthMultiplier = _tv.WaveDepth * 2f;
            }
            if (!_wave.Spared && !_wave.Landed)
            {
                GroundPoint her = Player.GroundPosition, o = Ground(_wave.From), d = new GroundPoint(_wave.Dir.x, _wave.Dir.z);
                GroundPoint rel = her - o;
                float along = rel.Dot(d), across = Mathf.Abs(rel.X * d.Z - rel.Z * d.X);
                bool touch = across <= _tv.WaveWidth * 0.5f + Placeholders.PlayerRadius && along <= _wave.Flight + Placeholders.PlayerRadius
                             && BossTrialRules.BandTouches(_wave.Before, at, _tv.WaveDepth, along, Placeholders.PlayerRadius);
                if (touch) Touch(_waveDef, ref _wave.Landed, ref _wave.Avoided);
            }
            _wave.Before = at;
            if (since * _tv.WaveSpeed >= _wave.Flight + _tv.WaveDepth) EndWave();
        }

        void EndWave()
        {
            if (!_wave.Live) return;
            _wave.Live = false;
            if (!_wave.Spared && !_wave.Landed) Report(EnemyReportKind.Struck, _waveDef, _wave.Avoided ? StrikeOutcome.AvoidedByDash : StrikeOutcome.OutOfArea);
            _wave.Path = _lines.Give(_wave.Path);
            _wave.Front = _lines.Give(_wave.Front);
        }

        /// <summary>
        /// A band that moves touched her this frame: it lands once, unless the dash's protected part covers her now, when it waits to
        /// see whether she comes out of it still in the band (reported avoided by the dash at its end if never landed).
        /// </summary>
        void Touch(EnemyAttackDefinition def, ref bool landed, ref bool avoided)
        {
            if (Player.IsInvulnerable)
            {
                avoided = true;
                return;
            }
            landed = true;
            Land(def, true);
        }

        // ---------------- 3. Grave Marks ----------------

        sealed class Mark
        {
            public bool Live;
            public Vector3 At;
            public float PlacedAt, BurstAt;
            public LineRenderer Line;
        }
        readonly Mark[] _marks = new Mark[12];
        float _marksStart = -1f, _marksBurst;
        int _marksPlaced;

        /// <summary>The middle of the Grave Mark placed last (for tests and the bot).</summary>
        public Vector3 NewestMark { get; private set; }

        /// <summary>Grave Marks on the ground now (for tests and the bot).</summary>
        public int MarksLive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _marks.Length; i++) if (_marks[i] != null && _marks[i].Live) n++;
                return n;
            }
        }

        /// <summary>The live circle nearest to bursting, its middle and burst time (false: none).</summary>
        public bool NextMark(out Vector3 at, out float burstAt, out float radius)
        {
            at = default;
            burstAt = float.PositiveInfinity;
            radius = _tv != null ? _tv.MarkRadius : 0f;
            bool any = false;
            for (int i = 0; i < _marks.Length; i++)
            {
                Mark m = _marks[i];
                if (m == null || !m.Live || m.BurstAt >= burstAt) continue;
                at = m.At;
                burstAt = m.BurstAt;
                any = true;
            }
            return any;
        }

        void TickMarks(float now)
        {
            if (_marksStart >= 0f && _running == BossTrialAttack.GraveMarks && State == EnemyState.Windup)
            {
                while (_marksPlaced < _tv.Marks && now >= _marksStart + BossTrialRules.MarkPlacedAt(_marksPlaced, _tv) - 1e-5f)
                {
                    _marksPlaced++;
                    PlaceMark(now, _marksBurst);
                }
                if (_marksPlaced >= _tv.Marks) _marksStart = -1f;
            }
            for (int i = 0; i < _marks.Length; i++)
            {
                Mark m = _marks[i];
                if (m == null || !m.Live) continue;
                if (now >= m.BurstAt)
                {
                    m.Live = false;
                    m.Line = _lines.Give(m.Line);
                    BurstAt(_markDef, m.At);
                    Land(_markDef, SkillRules.InCircle(Ground(m.At), _tv.MarkRadius, Player.GroundPosition, Placeholders.PlayerRadius));
                    continue;
                }
                if (m.Line != null) m.Line.widthMultiplier = BossTrialLines.Width((now - m.PlacedAt) / Mathf.Max(1e-4f, m.BurstAt - m.PlacedAt));
            }
        }

        /// <summary>One circle on her (led along her motion with <see cref="BossTrialValues.GraveMarksLead"/>), bursting <paramref name="burst"/> s from now.</summary>
        void PlaceMark(float now, float burst)
        {
            if (Player == null || !BossTrialValues.GraveMarksOn) return;
            Vector3 v = Player.Velocity;
            GroundPoint p = BossTrialRules.MarkPlace(Player.GroundPosition, new GroundPoint(v.x, v.z), BossTrialValues.GraveMarksLead, _tv);
            var at = new Vector3(p.X, 0f, p.Z);
            if (!NavMesh.SamplePosition(at, out NavMeshHit hit, _tv.MarkFindGround, NavMesh.AllAreas)) return; // off the walkable ground: none
            at = Flat(hit.position);
            Mark m = null;
            for (int i = 0; i < _marks.Length; i++) if (!_marks[i].Live) { m = _marks[i]; break; }
            if (m == null) return;
            m.Live = true;
            m.At = at;
            m.PlacedAt = now;
            m.BurstAt = now + burst;
            m.Line = _lines.Take(BossTrialLines.Red, true);
            _lines.Circle(m.Line, at, _tv.MarkRadius);
            NewestMark = at;
            Report(EnemyReportKind.CirclePlaced, _markDef);
        }

        /// <summary>A circle's burst drawn as the effects draw a caster's (work order 034's drawer); nothing when they are not running.</summary>
        void BurstAt(EnemyAttackDefinition def, Vector3 at)
        {
            if (StrikeAtDrawer != null) StrikeAtDrawer(this, def, at);
        }

        // ---------------- 4. Spokes ----------------

        sealed class SpokeSet
        {
            public bool Live, FirstBurst;
            public Vector3 Origin;
            public float Start, First, Second;
            public bool ComboMark;
            public readonly Vector3[] Dir = new Vector3[12];
            public readonly float[] Length = new float[12];
            public readonly LineRenderer[] Out = new LineRenderer[6], In = new LineRenderer[6];
        }
        readonly SpokeSet _spokes = new SpokeSet();

        /// <summary>The spokes now: live, which set shows (0 or 1), and a spoke's direction and length (for tests and the bot).</summary>
        public bool SpokesLive => _spokes.Live;
        public int SpokesSet => _spokes.FirstBurst ? 1 : 0;
        public Vector3 SpokeOrigin => _spokes.Origin;
        public Vector3 SpokeDir(int set, int i) => _spokes.Dir[set * 6 + i];
        public float SpokeLength(int set, int i) => _spokes.Length[set * 6 + i];
        public float SpokesBurstAt => _spokes.Live ? _spokes.Start + (_spokes.FirstBurst ? _spokes.First + _spokes.Second : _spokes.First) : float.PositiveInfinity;

        void BeginSpokes(float now)
        {
            EndSpokes();
            Vector3 aim = AimDirection();
            var a = new GroundPoint(aim.x, aim.z);
            _spokes.Live = true;
            _spokes.FirstBurst = false;
            _spokes.Origin = Flat(transform.position);
            _spokes.Start = now;
            _spokes.First = BossTrialRules.Windup(_tv.SpokeFirstFill, _tv.SpokeFillFloor, _phase, _tv);
            _spokes.Second = BossTrialRules.Windup(_tv.SpokeSecondFill, _tv.SpokeFillFloor, _phase, _tv);
            _spokes.ComboMark = _phase >= 3 && BossTrialValues.GraveMarksOn; // phase 3: one Grave Mark during set 2
            int n = Mathf.Min(6, _tv.SpokeCount);
            for (int set = 0; set < 2; set++)
                for (int i = 0; i < n; i++)
                {
                    GroundPoint d = BossTrialRules.SpokeDirection(a, set, i, _tv);
                    var dir = new Vector3(d.X, 0f, d.Z);
                    _spokes.Dir[set * 6 + i] = dir;
                    // Stopped where it first meets a wall or pier along its middle or either edge (the second round: a strip whose middle
                    // missed a pier was drawn over it), and drawn and judged so.
                    Vector3 edge = Vector3.Cross(Vector3.up, dir) * (_tv.SpokeHalfWidth * 0.95f);
                    _spokes.Length[set * 6 + i] = Mathf.Min(Cover.ClearDistance(_spokes.Origin, dir, _tv.SpokeReach, Tuning.SightHeight),
                        Mathf.Min(Cover.ClearDistance(_spokes.Origin + edge, dir, _tv.SpokeReach, Tuning.SightHeight),
                                  Cover.ClearDistance(_spokes.Origin - edge, dir, _tv.SpokeReach, Tuning.SightHeight)));
                }
            DrawSpokeSet(0);
            PoseSlam(0f, true); // it plants its weapon: the crouch of the slam, held
        }

        void DrawSpokeSet(int set)
        {
            int n = Mathf.Min(6, _tv.SpokeCount);
            for (int i = 0; i < n; i++)
            {
                _spokes.Out[i] = _lines.Give(_spokes.Out[i]);
                _spokes.In[i] = _lines.Give(_spokes.In[i]);
                _spokes.Out[i] = _lines.Take(BossTrialLines.Red, true);
                _spokes.In[i] = _lines.Take(BossTrialLines.Red, false);
                _lines.Strip(_spokes.Out[i], _spokes.Origin, _spokes.Dir[set * 6 + i], _spokes.Length[set * 6 + i], _tv.SpokeHalfWidth);
            }
        }

        void TickSpokes(float now)
        {
            if (!_spokes.Live) return;
            int n = Mathf.Min(6, _tv.SpokeCount);
            int set = _spokes.FirstBurst ? 1 : 0;
            float began = _spokes.Start + (set == 1 ? _spokes.First : 0f), fill = set == 1 ? _spokes.Second : _spokes.First;
            if (now >= began + fill)
            {
                // The set bursts: she is hit once if any of its strips touches her.
                bool hit = false;
                GroundPoint o = Ground(_spokes.Origin), her = Player.GroundPosition;
                for (int i = 0; i < n && !hit; i++)
                {
                    Vector3 d = _spokes.Dir[set * 6 + i];
                    hit = BossTrialRules.SpokeHits(o, new GroundPoint(d.x, d.z), _spokes.Length[set * 6 + i], _tv.SpokeHalfWidth, her, Placeholders.PlayerRadius);
                }
                Land(TrialDef(BossTrialAttack.Spokes), hit);
                if (set == 0)
                {
                    _spokes.FirstBurst = true;
                    DrawSpokeSet(1);
                    if (_spokes.ComboMark) PlaceMark(now, Mathf.Min(_spokes.Second, BossTrialRules.Windup(_tv.MarkBurst, _tv.MarkBurstFloor, _phase, _tv)));
                }
                else EndSpokes();
                return;
            }
            float share = BossTrialRules.Fill(now - began, fill);
            for (int i = 0; i < n; i++)
            {
                if (_spokes.Out[i] != null) _spokes.Out[i].widthMultiplier = BossTrialLines.Width(share);
                // The fill: the same line drawn down the strip's middle, as wide as the share of the fill, so the strip fills from its
                // middle out to its edges and is full, red, as it bursts (an outline alone, six of them crossing, read as a web).
                if (_spokes.In[i] != null)
                {
                    _lines.Band(_spokes.In[i], _spokes.Origin, _spokes.Dir[set * 6 + i], _spokes.Length[set * 6 + i]);
                    _spokes.In[i].widthMultiplier = Mathf.Max(BossTrialLines.Thin, 2f * _tv.SpokeHalfWidth * share);
                }
            }
        }

        void EndSpokes()
        {
            if (!_spokes.Live) return;
            _spokes.Live = false;
            for (int i = 0; i < 6; i++)
            {
                _spokes.Out[i] = _lines.Give(_spokes.Out[i]);
                _spokes.In[i] = _lines.Give(_spokes.In[i]);
            }
            if (_judgement == JudgementStage.None) PoseSlam(0f, false);
            _lastBigEnded = Time.time;
        }

        // ---------------- 5. The Shockwave ----------------

        sealed class Ring
        {
            public bool Live, Landed, Avoided;
            public Vector3 Middle;
            public float Start, Before;
            public float[] Clear;
            public readonly LineRenderer[] Arcs = new LineRenderer[16];
        }
        readonly Ring _ring = new Ring();

        /// <summary>The ring now: live, its middle and its radius (for tests and the bot).</summary>
        public bool RingLive => _ring.Live;
        public Vector3 RingMiddle => _ring.Middle;
        public float RingRadiusNow => _ring.Live ? BossTrialRules.RingRadius(Time.time - _ring.Start, _tv) : 0f;
        /// <summary>Is a point behind what stops the ring now?</summary>
        public bool RingSheltered(Vector3 p) => _ring.Live && BossTrialRules.Sheltered(_ring.Clear, Ground(_ring.Middle), Ground(p));

        void BeginRing(Vector3 middle, float now)
        {
            EndRing();
            if (_ring.Clear == null || _ring.Clear.Length != _tv.RingSamples) _ring.Clear = new float[Mathf.Max(8, _tv.RingSamples)];
            _ring.Live = true;
            _ring.Landed = _ring.Avoided = false;
            _ring.Middle = middle;
            _ring.Start = now;
            _ring.Before = _tv.RingFrom;
            // Walls and tall props round the landing, looked for once: the ring stops at them, and is drawn so.
            for (int i = 0; i < _ring.Clear.Length; i++)
            {
                GroundPoint d = BossTrialRules.SampleDirection(i, _ring.Clear.Length);
                _ring.Clear[i] = Cover.ClearDistance(middle, new Vector3(d.X, 0f, d.Z), _tv.RingTo + 1f, Tuning.SightHeight);
            }
            for (int i = 0; i < _ring.Arcs.Length; i++) _ring.Arcs[i] = _lines.Take(BossTrialLines.Red, false);
        }

        void TickRing(float now)
        {
            if (!_ring.Live) return;
            float r = BossTrialRules.RingRadius(now - _ring.Start, _tv), half = _tv.RingThickness * 0.5f;
            if (!_ring.Landed)
            {
                GroundPoint her = Player.GroundPosition, mid = Ground(_ring.Middle);
                float at = her.DistanceTo(mid);
                if (at - Placeholders.PlayerRadius <= _tv.RingTo + half && BossTrialRules.BandTouches(_ring.Before, r, half, at, Placeholders.PlayerRadius)
                    && !BossTrialRules.Sheltered(_ring.Clear, mid, her))
                    Touch(_ringDef, ref _ring.Landed, ref _ring.Avoided);
            }
            _ring.Before = r;
            if (r - half > _tv.RingTo)
            {
                EndRing();
                return;
            }
            // One filled band as wide as the part that hits, broken where a pillar or wall stops it (the second round: two thin circles
            // read like the landing's own flash; the ring is what still threatens, so it is the clearer of the two).
            int used = DrawRingCircle(r, 0);
            for (int i = used; i < _ring.Arcs.Length; i++) if (_ring.Arcs[i] != null) _ring.Arcs[i].enabled = false;
        }

        int DrawRingCircle(float radius, int used)
        {
            float[] clear = _ring.Clear;
            int n = clear.Length;
            int start = -1;
            for (int i = 0; i < n; i++) if (clear[i] < radius) { start = i; break; }
            if (start < 0)
            {
                // Nothing stops it all round: one whole circle.
                if (used < _ring.Arcs.Length) Arc(used++, radius, 0, n - 1, true);
                return used;
            }
            int runFrom = -1;
            for (int k = 1; k <= n; k++)
            {
                int i = (start + k) % n;
                bool open = clear[i] >= radius;
                if (open && runFrom < 0) runFrom = i;
                if ((!open || k == n) && runFrom >= 0)
                {
                    int to = open ? i : (i + n - 1) % n;
                    if (used < _ring.Arcs.Length) Arc(used++, radius, runFrom, to, false);
                    runFrom = -1;
                }
            }
            return used;
        }

        void Arc(int index, float radius, int from, int to, bool whole)
        {
            LineRenderer l = _ring.Arcs[index];
            if (l == null) return;
            l.enabled = true;
            l.loop = whole;
            l.widthMultiplier = _tv.RingThickness;
            _lines.Arc(l, _ring.Middle, radius, from, to, _ring.Clear.Length);
        }

        void EndRing()
        {
            if (!_ring.Live) return;
            _ring.Live = false;
            if (!_ring.Landed) Report(EnemyReportKind.Struck, _ringDef, _ring.Avoided ? StrikeOutcome.AvoidedByDash : StrikeOutcome.OutOfArea);
            for (int i = 0; i < _ring.Arcs.Length; i++) _ring.Arcs[i] = _lines.Give(_ring.Arcs[i]);
            _lastBigEnded = Time.time;
        }

        // ---------------- 6. Seeds ----------------

        sealed class SeedThing
        {
            public bool Live;
            public Vector3 At;
            public float LandedAt, BurstAt;
            public int Hits;
            public GameObject Sphere;
            public LineRenderer Line;
        }
        readonly SeedThing[] _seeds = new SeedThing[6];
        MaterialPropertyBlock _seedBlock;

        /// <summary>Seeds on the ground now, and seed <paramref name="i"/>'s place and burst (for tests and the bot).</summary>
        public int SeedsLive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _seeds.Length; i++) if (_seeds[i] != null && _seeds[i].Live) n++;
                return n;
            }
        }
        public bool Seed(int i, out Vector3 at, out float burstAt, out int hits)
        {
            SeedThing s = i >= 0 && i < _seeds.Length ? _seeds[i] : null;
            at = s != null ? s.At : default;
            burstAt = s != null ? s.BurstAt : 0f;
            hits = s != null ? s.Hits : 0;
            return s != null && s.Live;
        }
        public int SeedSlots => _seeds.Length;

        void ThrowSeeds(float now)
        {
            if (Player == null) return;
            float fill = BossTrialRules.Windup(_tv.SeedFill, _tv.SeedFillFloor, _phase, _tv);
            GroundPoint her = Player.GroundPosition; // where she is as they leave its hand
            int placed = 0;
            double turn = _tvRandom.NextDouble() * Math.PI * 2.0;
            for (int k = 0; k < _tv.SeedCount; k++)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    float distance = _tv.SeedNear + (float)_tvRandom.NextDouble() * Mathf.Max(0f, _tv.SeedFar - _tv.SeedNear);
                    GroundPoint p = BossTrialRules.SeedPlace(her, k, _tv.SeedCount, turn + tries * 0.37, distance);
                    var at = new Vector3(p.X, 0f, p.Z);
                    if (!NavMesh.SamplePosition(at, out NavMeshHit hit, _tv.SeedFindGround, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > 0.5f) continue;
                    at = Flat(hit.position);
                    if (Vector3.Distance(at, Flat(Player.Position)) < _tv.SeedNear - 0.5f) continue;
                    // Seen (the second round): its sphere and its whole circle in view and clear of her plates, not behind a pier from his
                    // camera, its circle on open floor (no wall or pier inside it).
                    if (!BossTrialScreen.Seen(at, _tv.SeedCircle, _tv.SeedHeight + _tv.SeedSize * 0.5f, _tv.SeenMarginPx)) continue;
                    if (!BossTrialScreen.Unhidden(at, _tv.SeedHeight)) continue;
                    if (!Clear(at, _tv.SeedCircle)) continue;
                    if (TooNearASeed(at)) continue;
                    if (PlaceSeed(at, now, fill)) placed++;
                    break;
                }
            }
            if (placed == 0) _lastBigEnded = now; // none found ground in view: the pattern is over as it began
            NoteTrial(BossTrialLog.SeedsThrown, BossTrialValues.Seeds, "count", Int(placed));
        }

        bool TooNearASeed(Vector3 at)
        {
            for (int i = 0; i < _seeds.Length; i++)
                if (_seeds[i].Live && Vector3.Distance(_seeds[i].At, at) < _tv.SeedApart) return true;
            return false;
        }

        bool PlaceSeed(Vector3 at, float now, float fill)
        {
            SeedThing s = null;
            for (int i = 0; i < _seeds.Length; i++) if (!_seeds[i].Live) { s = _seeds[i]; break; }
            if (s == null) return false;
            s.Live = true;
            s.At = at;
            s.LandedAt = now;
            s.BurstAt = now + fill;
            s.Hits = 0;
            if (s.Sphere == null)
            {
                s.Sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.Sphere.name = "BossTrialSeed";
                Collider c = s.Sphere.GetComponent<Collider>();
                if (c != null) Destroy(c); // nothing walks into it or is stopped by it
                s.Sphere.transform.SetParent(transform.parent, true);
                if (_seedBlock == null) _seedBlock = new MaterialPropertyBlock();
                _seedBlock.Clear();
                var grey = new Color(_tv.SeedColour.R, _tv.SeedColour.G, _tv.SeedColour.B);
                _seedBlock.SetColor("_BaseColor", grey);
                _seedBlock.SetColor("_Color", grey);
                s.Sphere.GetComponent<Renderer>().SetPropertyBlock(_seedBlock);
            }
            s.Sphere.transform.position = at + Vector3.up * _tv.SeedHeight;
            s.Sphere.transform.localScale = Vector3.one * _tv.SeedSize;
            s.Sphere.SetActive(true);
            s.Line = _lines.Take(BossTrialLines.Red, true);
            _lines.Circle(s.Line, at, _tv.SeedCircle);
            return true;
        }

        void TickSeeds(float now)
        {
            for (int i = 0; i < _seeds.Length; i++)
            {
                SeedThing s = _seeds[i];
                if (s == null || !s.Live) continue;
                if (now >= s.BurstAt)
                {
                    bool hit = SkillRules.InCircle(Ground(s.At), _tv.SeedCircle, Player.GroundPosition, Placeholders.PlayerRadius);
                    GoneSeed(s);
                    BurstAt(_seedDef, s.At);
                    Land(_seedDef, hit);
                    SeedsBurst++;
                    NoteTrial(BossTrialLog.Seed, BossTrialValues.Seed, "ended", "burst", "hit", hit ? "yes" : "no");
                    continue;
                }
                if (s.Line != null) s.Line.widthMultiplier = BossTrialLines.Width((now - s.LandedAt) / Mathf.Max(1e-4f, s.BurstAt - s.LandedAt));
            }
        }

        /// <summary>Her basic shot went off: the first live seed its line touches before it ends takes a hit (with <see cref="BossTrialValues.SeedsCanBeShot"/>).</summary>
        void ShotAtSeeds(Damageable hitTarget)
        {
            if (!BossTrialValues.SeedsCanBeShot || Player == null || SeedsLive == 0) return;
            GroundPoint origin = Player.GroundPosition;
            Vector3 aimPoint = Player.AimPoint;
            var aim = new GroundPoint(aimPoint.x, aimPoint.z);
            GroundPoint toAim = aim - origin;
            if (toAim.Length < 1e-3f) return;
            var dir = new Vector3(toAim.X, 0f, toAim.Z) / toAim.Length;
            float reach = Mathf.Min(Player.BasicShotRange, Cover.ClearDistance(Flat(Player.Position), dir, Player.BasicShotRange, Cover.RayHeight));
            if (hitTarget != null) reach = Mathf.Min(reach, hitTarget.GroundPosition.DistanceTo(origin));
            SeedThing best = null;
            float bestAlong = float.PositiveInfinity;
            for (int i = 0; i < _seeds.Length; i++)
            {
                SeedThing s = _seeds[i];
                if (s == null || !s.Live) continue;
                GroundPoint p = Ground(s.At);
                if (!BossTrialRules.ShotTouchesSeed(origin, aim, reach, Player.BasicShotHalfWidth, p, _tv.SeedSize * 0.5f)) continue;
                float along = (p - origin).Dot(toAim * (1f / toAim.Length));
                if (along < bestAlong) { bestAlong = along; best = s; }
            }
            if (best == null) return;
            best.Hits++;
            SeedHits++;
            if (best.Hits >= _tv.SeedShots)
            {
                GoneSeed(best);
                SeedsShot++;
                NoteTrial(BossTrialLog.Seed, BossTrialValues.Seed, "ended", "shot", "hit", "no");
                return;
            }
            // Each hit shrinks it a little: the shot told on the grey sphere.
            best.Sphere.transform.localScale = Vector3.one * (_tv.SeedSize * (1f - 0.2f * best.Hits));
        }

        /// <summary>Seeds broken by her shots, burst, and her shots that hit one, this fight.</summary>
        public int SeedsShot { get; private set; }
        public int SeedsBurst { get; private set; }
        public int SeedHits { get; private set; }

        void GoneSeed(SeedThing s)
        {
            s.Live = false;
            if (s.Sphere != null) s.Sphere.SetActive(false);
            s.Line = _lines.Give(s.Line);
            if (SeedsLive == 0) _lastBigEnded = Time.time; // the last of them gone: the big pattern is over
        }

        // ---------------- 7. Judgement and Ruin ----------------

        /// <summary>Where Judgement stands.</summary>
        public enum JudgementStage { None, Walk, Ward, Ruin, Dazed }

        JudgementStage _judgement;
        Vector3 _kneel;
        float _stageAt, _walkFrom;
        int _ward, _wardMarks;
        float[] _shadowClear, _shadowEdge;
        LineRenderer _wardLine, _ruinLine;
        readonly LineRenderer[] _shadowLines = new LineRenderer[12];
        int _shadowsDrawn;
        float _ruinSeconds;

        public JudgementStage Judgement => _judgement;
        /// <summary>s left to break the ward while it stands, else 0 (shown on its bar).</summary>
        public float WardTimeLeft => _judgement == JudgementStage.Ward && _tv != null ? Mathf.Max(0f, _tv.WardSeconds - (Time.time - _stageAt)) : 0f;
        /// <summary>The ward's damage taken so far, of <see cref="BossTrialValues.Ward"/>.</summary>
        public int WardTaken => _ward;
        public Vector3 KneelPlace => _kneel;
        /// <summary>When the stage now began.</summary>
        public float JudgementStageAt => _stageAt;
        /// <summary>s of Ruin in this room.</summary>
        public float RuinTime => _tv != null ? _tv.RuinSecondsFor(_template) : 0f;
        /// <summary>Is she (or any point) in a pillar's shadow from where it kneels?</summary>
        public bool InRuinShadow(Vector3 p) => _shadowClear != null && RuinShadows.InShadow(_shadowClear, Ground(_kneel), Ground(p));
        /// <summary>The shadow table (for the bot: the nearest shadow).</summary>
        public float[] ShadowClear => _shadowClear;
        public float[] ShadowEdge => _shadowEdge;
        public int WardsBroken { get; private set; }
        public int WardsFailed { get; private set; }

        void BeginJudgement(float now)
        {
            _judgementsCome++;
            _chooser.Began(BossTrialAttack.Judgement, now);
            _running = BossTrialAttack.None;
            _judgement = JudgementStage.Walk;
            _stageAt = _walkFrom = now;
            _kneel = KneelPlaceNear(_roomMiddle);
            _brain.Cancel(now);
            if (_tvHeldStagger == null && _self.Stagger != null)
            {
                _tvHeldStagger = _self.Stagger; // nothing staggers it through Judgement: the ward is the check
                _self.Stagger = null;
            }
            NoteTell(BossTrialAttack.Judgement, now);
            Report(EnemyReportKind.AttackStarted, _ruinDefJudgement ?? (_ruinDefJudgement = new EnemyAttackDefinition { Name = BossTrialValues.Judgement, Damage = 0 }));
        }
        EnemyAttackDefinition _ruinDefJudgement;

        /// <summary>
        /// The place nearest the room's middle where its whole body stands clear of every wall and pier (its body is wider than the
        /// agent the NavMesh is built for: a middle chosen on the NavMesh alone could put it half inside a pier).
        /// </summary>
        Vector3 KneelPlaceNear(Vector3 middle)
        {
            for (float r = 0f; r <= _tv.KneelSearch + 1e-3f; r += 1f)
            {
                int ways = r <= 0f ? 1 : 16;
                for (int k = 0; k < ways; k++)
                {
                    float a = k * Mathf.PI * 2f / ways;
                    Vector3 p = middle + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > 0.5f) continue;
                    if (Clear(Flat(hit.position), _tv.KneelClear)) return hit.position;
                }
            }
            return transform.position;
        }

        bool Clear(Vector3 p, float clear)
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                if (Cover.ClearDistance(p, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), clear, Tuning.SightHeight) < clear - 1e-3f) return false;
            }
            return true;
        }

        void TickJudgement(float now)
        {
            switch (_judgement)
            {
                case JudgementStage.Walk:
                {
                    bool there = Flat(transform.position - _kneel).magnitude <= _tv.KneelArrive || now - _walkFrom >= _tv.KneelWalkMost;
                    // It kneels once there, on his screen or not: the ward strikes nothing, and Ruin is 8 s and more away.
                    if (there && !IsDazed) BeginWard(now);
                    break;
                }
                case JudgementStage.Ward:
                {
                    float since = now - _stageAt;
                    if (_wardLine != null)
                    {
                        _lines.Circle(_wardLine, Flat(transform.position), BossTrialRules.WardRadius(since, _tv));
                        _wardLine.widthMultiplier = BossTrialLines.Width(since / Mathf.Max(0.01f, _tv.WardSeconds));
                    }
                    while (_wardMarks < BossTrialRules.WardMarks(_tv) && since >= (_wardMarks + 1) * _tv.WardMarkEvery)
                    {
                        _wardMarks++;
                        PlaceMark(now, BossTrialRules.Windup(_tv.MarkBurst, _tv.MarkBurstFloor, _phase, _tv));
                    }
                    if (_ward >= _tv.Ward) WardBroken(now, since);
                    else if (since >= _tv.WardSeconds) BeginRuin(now);
                    break;
                }
                case JudgementStage.Ruin:
                {
                    float since = now - _stageAt;
                    if (_ruinLine != null) _ruinLine.widthMultiplier = BossTrialLines.Width(since / Mathf.Max(0.01f, _ruinSeconds));
                    if (since >= _ruinSeconds)
                    {
                        bool hit = !InRuinShadow(Player.Position);
                        Land(_ruinDef, hit);
                        EndJudgement(now);
                    }
                    break;
                }
                case JudgementStage.Dazed:
                    if (!IsDazed) EndJudgement(now);
                    break;
            }
        }

        void BeginWard(float now)
        {
            _judgement = JudgementStage.Ward;
            _stageAt = now;
            _ward = 0;
            _wardMarks = 0;
            Halt();
            PoseSlam(0f, true); // it kneels: the crouch of the slam, held
            _self.WardShare = 1f; // her hits wear the ward, not its health (1 a hit goes through, as a warder's ward)
            if (_onWarded == null)
            {
                _onWarded = amount => { if (_judgement == JudgementStage.Ward) _ward += amount; };
                _onDamagedInWard = amount => { if (_judgement == JudgementStage.Ward) _ward += amount; };
            }
            _self.Warded += _onWarded;
            _self.Damaged += _onDamagedInWard;
            _wardLine = _lines.Take(BossTrialLines.Gold, true);
            // The shadows are found now, once, from where it kneels (it does not move again until Judgement is over).
            int n = Mathf.Max(8, _tv.ShadowSamples);
            if (_shadowClear == null || _shadowClear.Length != n)
            {
                _shadowClear = new float[n];
                _shadowEdge = new float[n];
            }
            GroundPoint k = Ground(transform.position);
            _kneel = Flat(transform.position);
            var room = new GroundRect(_roomMin.x, _roomMin.z, _roomMax.x, _roomMax.z);
            for (int i = 0; i < n; i++)
            {
                GroundPoint d = BossTrialRules.SampleDirection(i, n);
                float edge = RuinShadows.Exit(k, d, room);
                _shadowEdge[i] = edge;
                _shadowClear[i] = Cover.ClearDistance(_kneel, new Vector3(d.X, 0f, d.Z), edge + 1f, Tuning.SightHeight);
            }
            DrawShadows(_tv.ShadowDimWidth); // dim from the ward's rise: 8 s to see where shelter is
            NoteTrial(BossTrialLog.Ward, BossTrialValues.Judgement, "began", "yes", "ward", Int(_tv.Ward));
        }

        void WardBroken(float now, float since)
        {
            WardsBroken++;
            EndWard();
            NoteTrial(BossTrialLog.Ward, BossTrialValues.Judgement, "broken", "yes", "seconds", Text(since));
            _judgement = JudgementStage.Dazed;
            _stageAt = now;
            for (int i = 0; i < _shadowLines.Length; i++) _shadowLines[i] = _lines.Give(_shadowLines[i]); // no Ruin: no shelter to show
            PoseSlam(0f, false);
            DazeTrial(_tv.WardDaze);
        }

        void BeginRuin(float now)
        {
            WardsFailed++;
            EndWard();
            NoteTrial(BossTrialLog.Ward, BossTrialValues.Judgement, "broken", "no", "seconds", Text(_tv.WardSeconds), "taken", Int(_ward));
            _judgement = JudgementStage.Ruin;
            _stageAt = now;
            _ruinSeconds = _tv.RuinSecondsFor(_template);
            _ruinLine = _lines.Take(BossTrialLines.Red, true);
            _lines.Rect(_ruinLine, _roomMin, _roomMax);
            // The room's walls are mostly off his screen: the room itself tells it, its lamps red and its body red, until Ruin strikes.
            BossRoomLight.Answer(this, _tv.RuinLamps);
            Tint(new Color(_tv.RuinLamps.R, _tv.RuinLamps.G, _tv.RuinLamps.B));
            // The shadows, drawn dim since the ward rose, turn bright; the floor he can see is hatched red out to where each line is stopped.
            for (int i = 0; i < _shadowsDrawn; i++) if (_shadowLines[i] != null) _shadowLines[i].widthMultiplier = _tv.ShadowLineWidth;
            DrawRuinFan();
            int drawn = _shadowsDrawn;
            NoteTrial(BossTrialLog.Ruin, BossTrialValues.Ruin, "seconds", Text(_ruinSeconds), "shadows", Int(drawn), "in_shadow", InRuinShadow(Player.Position) ? "yes" : "no");
        }

        /// <summary>Each pillar's shadow away from it, pale: the runs of directions its line stops short of the room's edge.</summary>
        void DrawShadows(float width)
        {
            int n = _shadowClear.Length, drawn = 0, start = -1;
            bool Deep(int i) => _shadowEdge[i] - _shadowClear[i] > ShadowDepth;
            for (int i = 0; i < n; i++) if (!Deep(i)) { start = i; break; }
            if (start >= 0)
            {
                int from = -1;
                for (int k = 1; k <= n; k++)
                {
                    int i = (start + k) % n;
                    bool deep = Deep(i);
                    if (deep && from < 0) from = i;
                    if ((!deep || k == n) && from >= 0)
                    {
                        int to = deep ? i : (i + n - 1) % n;
                        if (drawn < _shadowLines.Length)
                        {
                            _shadowLines[drawn] = _lines.Take(BossTrialLines.Pale, true);
                            _lines.Shadow(_shadowLines[drawn], _kneel, _shadowClear, _shadowEdge, from, to);
                            if (_shadowLines[drawn] != null) _shadowLines[drawn].widthMultiplier = width;
                            drawn++;
                        }
                        from = -1;
                    }
                }
            }
            _shadowsDrawn = drawn;
        }

        BossTrialLines _fan;
        readonly LineRenderer[] _fanLines = new LineRenderer[256];

        /// <summary>
        /// Ruin over the floor he can see (the second round): red wedges from it out to where each wedge's lines are first stopped (the
        /// nearest stop within the wedge, so no wedge paints a shadow), hatched so the floor shows between them.
        /// </summary>
        void DrawRuinFan()
        {
            if (_fan == null) _fan = new BossTrialLines(Telegraph, transform.parent, 0, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            int wedges = Mathf.Clamp(_tv.RuinFanWedges, 8, _fanLines.Length), n = _shadowClear.Length;
            for (int w = 0; w < wedges; w++)
            {
                int from = w * n / wedges, to = (w + 1) * n / wedges;
                float reach = float.PositiveInfinity;
                for (int i = from; i < to; i++) reach = Mathf.Min(reach, _shadowClear[i]);
                if (float.IsInfinity(reach) || reach < 0.5f) continue;
                float angle = (from + to) * 0.5f / n * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                _fanLines[w] = _fan.Take(BossTrialLines.Red, false);
                _fan.Band(_fanLines[w], _kneel, dir, reach);
                if (_fanLines[w] != null) _fanLines[w].widthMultiplier = 2f * reach * Mathf.Tan(Mathf.PI / wedges) * _tv.RuinFanFill;
            }
        }

        void EndRuinFan()
        {
            if (_fan == null) return;
            for (int i = 0; i < _fanLines.Length; i++) _fanLines[i] = _fan.Give(_fanLines[i]);
        }

        /// <summary>m: a direction's line must stop this far short of the room's edge for the ground behind to be drawn as a shadow.</summary>
        const float ShadowDepth = 0.5f;
        /// <summary>Shadows drawn by the last Ruin.</summary>
        public int ShadowsDrawn => _shadowsDrawn;

        void EndWard()
        {
            _self.WardShare = 0f;
            if (_onWarded != null) _self.Warded -= _onWarded;
            if (_onDamagedInWard != null) _self.Damaged -= _onDamagedInWard;
            _wardLine = _lines.Give(_wardLine);
        }

        void EndJudgement(float now)
        {
            if (_judgement == JudgementStage.Ward) EndWard();
            if (_judgement == JudgementStage.Ruin)
            {
                BossRoomLight.Answer(this, _phase >= 3 ? _tv.PhaseThreeLamps : _tv.PhaseTwoLamps);
                Tint(BodyColor);
            }
            _judgement = JudgementStage.None;
            _ruinLine = _lines.Give(_ruinLine);
            EndRuinFan();
            for (int i = 0; i < _shadowLines.Length; i++) _shadowLines[i] = _lines.Give(_shadowLines[i]);
            if (_tvHeldStagger != null && _self.Stagger == null) _self.Stagger = _tvHeldStagger;
            _tvHeldStagger = null;
            PoseSlam(0f, false);
            _lastEnded = now;
            _lastBigEnded = now;
        }

        // ---------------- Clearing up ----------------

        /// <summary>Everything on the ground goes, unburst (a transition, its death, her death).</summary>
        void ClearPatterns()
        {
            for (int i = 0; i < _marks.Length; i++)
                if (_marks[i] != null && _marks[i].Live)
                {
                    _marks[i].Live = false;
                    _marks[i].Line = _lines?.Give(_marks[i].Line);
                }
            _marksStart = -1f;
            for (int i = 0; i < _seeds.Length; i++) if (_seeds[i] != null && _seeds[i].Live) GoneSeed(_seeds[i]);
            if (_lines != null)
            {
                EndSpokes();
                EndWave();
                EndRing();
            }
            if (_judgement != JudgementStage.None && _lines != null) EndJudgement(Time.time);
        }

        /// <summary>The fight is over (its death, her death, taken away): the patterns go, her guard ends, the line is written.</summary>
        void EndTrialFight(string how)
        {
            if (!_trialOn) return;
            _trialOn = false;
            ClearPatterns();
            if (_standUntil >= 0f)
            {
                _standUntil = -1f;
                if (_self != null) _self.Untouchable = false;
            }
            if (_guard != null) _guard.SetActive(false);
            ListenToHer(false);
            WriteFight(how);
        }

        /// <summary>Every frame dead (from Update).</summary>
        void TrialDead()
        {
            if (_tv != null && _trialOn) EndTrialFight(BossFightEvents.Died);
        }

        void TrialDestroyed()
        {
            if (_tv == null) return;
            if (_trialOn) EndTrialFight(BossFightEvents.Removed);
            for (int i = 0; i < _seeds.Length; i++) if (_seeds[i] != null && _seeds[i].Sphere != null) Destroy(_seeds[i].Sphere);
            _lines?.DestroyAll();
            _fan?.DestroyAll();
        }

        // ---------------- The record (7.9) ----------------

        readonly float[] _phaseStarted = new float[4];
        readonly Dictionary<string, int> _hitsBy = new Dictionary<string, int>();
        readonly Dictionary<string, int> _damageBy = new Dictionary<string, int>();
        readonly Dictionary<string, int> _tellsBy = new Dictionary<string, int>();
        float _nextSample, _sampled, _stood;
        bool[] _floorWalkable, _floorUsed;
        int _floorCols, _floorRows, _floorWalkableCount;
        string _killedBy = "";

        /// <summary>Her hits taken by attack this fight (for tests and the bot).</summary>
        public IReadOnlyDictionary<string, int> TrialHitsBy => _hitsBy;
        public IReadOnlyDictionary<string, int> TrialDamageBy => _damageBy;
        public IReadOnlyDictionary<string, int> TrialTellsBy => _tellsBy;
        public string TrialKilledBy => _killedBy;
        /// <summary>The share of the fight she stood (under <see cref="BossTrialValues.StandingUnder"/>), and of its floor she used.</summary>
        public float TrialStoodShare => _sampled > 0f ? _stood / _sampled : 0f;
        public float TrialFloorShare
        {
            get
            {
                if (_floorUsed == null || _floorWalkableCount == 0) return 0f;
                int n = 0;
                for (int i = 0; i < _floorUsed.Length; i++) if (_floorUsed[i]) n++;
                return n / (float)_floorWalkableCount;
            }
        }
        /// <summary>When a phase began (game time; 0: not reached).</summary>
        public float TrialPhaseStarted(int phase) => _phaseStarted[Mathf.Clamp(phase, 1, 3)];

        void BeginRecord(float now)
        {
            _hitsBy.Clear();
            _damageBy.Clear();
            _tellsBy.Clear();
            _killedBy = "";
            _sampled = _stood = 0f;
            _nextSample = now;
            for (int i = 0; i < _phaseStarted.Length; i++) _phaseStarted[i] = 0f;
            _phaseStarted[Mathf.Clamp(_phase, 1, 3)] = now;
            if (_floorUsed != null) Array.Clear(_floorUsed, 0, _floorUsed.Length);
            SeedsShot = SeedsBurst = SeedHits = 0;
            Crashes = 0;
            WardsBroken = WardsFailed = 0;
        }

        /// <summary>The floor's walkable 2 m cells, once (a NavMesh look at each cell's middle).</summary>
        void FloorCells()
        {
            float cell = Mathf.Max(0.5f, _tv.FloorCell);
            _floorCols = Mathf.Max(1, Mathf.CeilToInt((_roomMax.x - _roomMin.x) / cell));
            _floorRows = Mathf.Max(1, Mathf.CeilToInt((_roomMax.z - _roomMin.z) / cell));
            _floorWalkable = new bool[_floorCols * _floorRows];
            _floorUsed = new bool[_floorCols * _floorRows];
            _floorWalkableCount = 0;
            for (int z = 0; z < _floorRows; z++)
                for (int x = 0; x < _floorCols; x++)
                {
                    var p = new Vector3(_roomMin.x + (x + 0.5f) * cell, 0f, _roomMin.z + (z + 0.5f) * cell);
                    bool ok = NavMesh.SamplePosition(p, out NavMeshHit hit, cell * 0.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y) < 0.5f;
                    _floorWalkable[z * _floorCols + x] = ok;
                    if (ok) _floorWalkableCount++;
                }
        }

        void Sample(float now)
        {
            if (now < _nextSample || _floorUsed == null) return;
            _nextSample = now + _tv.SampleEvery;
            _sampled += _tv.SampleEvery;
            if (Player.Velocity.magnitude < _tv.StandingUnder) _stood += _tv.SampleEvery;
            float cell = Mathf.Max(0.5f, _tv.FloorCell);
            int x = Mathf.FloorToInt((Player.Position.x - _roomMin.x) / cell), z = Mathf.FloorToInt((Player.Position.z - _roomMin.z) / cell);
            if (x >= 0 && z >= 0 && x < _floorCols && z < _floorRows && _floorWalkable[z * _floorCols + x]) _floorUsed[z * _floorCols + x] = true;
        }

        void NoteTell(BossTrialAttack a, float now)
        {
            string name = a == BossTrialAttack.Judgement ? BossTrialValues.Judgement : TrialDef(a)?.Name;
            if (name == null) return;
            _tellsBy.TryGetValue(name, out int n);
            _tellsBy[name] = n + 1;
        }

        void OnTrialReport(EnemyReport r)
        {
            if (r.Kind != EnemyReportKind.Struck || r.Outcome != StrikeOutcome.Landed || string.IsNullOrEmpty(r.Attack)) return;
            _hitsBy.TryGetValue(r.Attack, out int n);
            _hitsBy[r.Attack] = n + 1;
            _damageBy.TryGetValue(r.Attack, out int d);
            _damageBy[r.Attack] = d + r.Damage;
            if (_diedPending)
            {
                _diedPending = false;
                _killedBy = r.Attack;
            }
        }

        void WriteFight(string how)
        {
            PlaySessionRecorder rec = PlaySessionRecorder.Current;
            if (rec == null) return;
            float now = Time.time;
            float Len(int p)
            {
                float a = _phaseStarted[p];
                if (a <= 0f) return 0f;
                float b = now;
                for (int q = p + 1; q <= 3; q++) if (_phaseStarted[q] > 0f) { b = _phaseStarted[q]; break; }
                return Mathf.Max(0f, b - a);
            }
            var extras = new List<string>
            {
                "length", Text(now - _trialWoke), "one", Text(Len(1)), "two", Text(Len(2)), "three", Text(Len(3)),
                "ended", how ?? "", "killed_by", _killedBy, "stood", Text(TrialStoodShare), "floor", Text(TrialFloorShare),
                "template", _template, "seeds_shot", Int(SeedsShot), "seeds_burst", Int(SeedsBurst), "crashes", Int(Crashes),
                "wards_broken", Int(WardsBroken), "wards_failed", Int(WardsFailed), "health_left", Text(HealthShare),
            };
            foreach (KeyValuePair<string, int> h in _hitsBy) { extras.Add("hits." + h.Key); extras.Add(Int(h.Value)); }
            foreach (KeyValuePair<string, int> h in _damageBy) { extras.Add("damage." + h.Key); extras.Add(Int(h.Value)); }
            foreach (KeyValuePair<string, int> h in _tellsBy) { extras.Add("tells." + h.Key); extras.Add(Int(h.Value)); }
            rec.NoteBossTrial(BossTrialLog.Fight, null, extras.ToArray());
        }

        void NoteTrial(string type, string name, params string[] extras)
        {
            PlaySessionRecorder rec = PlaySessionRecorder.Current;
            if (rec == null) return;
            var all = new string[extras.Length + 2];
            all[0] = "phase";
            all[1] = Int(_phase);
            Array.Copy(extras, 0, all, 2, extras.Length);
            rec.NoteBossTrial(type, name, all);
        }

        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
        static string Text(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What a test sets for the trial fight: the seed of its draws (null in a real fight: the clock's), and whether the boss chooses
    /// nothing itself (a test of one attack begins it by <see cref="EnemyBoss.TrialBegin"/>).
    /// </summary>
    public static class BossTrialTesting
    {
        public static int? Seed;
        public static bool HoldChoices;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Seed = null;
            HoldChoices = false;
        }
    }
}
