using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The Grave Ram (work order 149, 2026-11-27; <see cref="EliteValues.Unique"/>): floor 1's unique elite, a mini-boss of its own in
    /// grey boxes. Kristian: "Egentlig vil jeg ha unike elites, som har et eget sett med utfordringer slik som i hades". Its numbers are
    /// <see cref="GraveRamValues"/>, its choices <see cref="GraveRamTactics"/>, its rules <see cref="GraveRamRules"/>. Its one idea: bait
    /// its charge into stone, then punish the daze.
    ///
    /// Its body: the brute's model (it is of the brute's kind to the models, <see cref="Kind"/>; its own class to everything else) shown
    /// 1.6 times as large and tinted ash white, alone in floor 1's elite room. It wakes with its room's fight and walks to her; it
    /// begins nothing for <see cref="GraveRamValues.FirstAttackAfter"/> after the wake, nor while its whole body is not on his screen
    /// (<see cref="BodyOnScreen"/>: the box round its feet and its head as the camera shows them, inside the screen and off the top
    /// plate and the skill strip by <see cref="GraveRamValues.ScreenMargin"/>), nor within its gap after an attack or a daze.
    ///
    /// The Ram: its strip drawn on the floor (blue: Stopping Shot from its front drops it) toward where she stood as the tell began,
    /// its body leaning as the lancer's does. The strip is where it runs: its body is swept along the line once, as the tell begins
    /// (<see cref="RunFrom"/>: a capsule of its body's radius against walls, piers, shut doors and props, tall or low, and the end of
    /// the walkable ground), and the strip ends at its body's front where it will stop; nothing outside the strip is hit
    /// (<see cref="GraveRamRules.InStrip"/>). It begins no Ram that would end short of her where she stands
    /// (<see cref="GraveRamRules.RamReachesHer"/>): one cut by the NavMesh (work order 146's parked boss) or ending on a pier's corner
    /// with her behind it (a free daze). At the strike it runs, moved by its agent, never turning: it meets her (her damage, it stops,
    /// it stands its recovery), or its body meets the stone the sweep found (a crash: its stagger bar emptied at once, dazed
    /// <see cref="GraveRamValues.CrashDaze"/> taking 1.5 times, heard as the lancer's crash), or it runs out (a miss: its recovery).
    /// In phase 2 a Ram that met her or missed is followed at once by a second, aimed where she then stands, if that one too reaches her.
    ///
    /// The Stomp: a red circle round itself; struck as every enemy's circle. The Grave Stones: three red circles placed where she stands,
    /// one each <see cref="GraveRamValues.StonesApart"/>, each bursting <see cref="GraveRamValues.StoneBurstAfter"/> after it appears,
    /// drawn by a pool of three outlines made as it wakes. At half its health it roars (<see cref="GraveRamValues.RoarSeconds"/>,
    /// rearing back; it cannot be staggered then, her damage counts), and phase 2 begins.
    ///
    /// A hit of its gives her the hurt guard (<see cref="EliteHurtGuard"/>). Its bar is <see cref="GraveRamBar"/>; its line in the log
    /// is told once as its fight ends (<see cref="GraveRamEvents"/>). Its frames allocate nothing.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyGraveRam : Enemy
    {
        const float TelegraphHeight = 0.07f;
        const float TelegraphThin = 0.05f;
        const float TelegraphThick = 0.3f;
        const float SampleReach = 1f;
        const float BurstFxSeconds = 0.2f;
        const float LayoutHeight = 1080f;

        static readonly RaycastHit[] s_hits = new RaycastHit[32];
        static readonly Vector3[] s_points = new Vector3[8];

        /// <summary>One stone of a throw on the ground: its outline from the pool, where, when it appeared and bursts.</summary>
        public struct Stone
        {
            public bool Pending;
            public Vector3 Center;
            public float PlacedAt, BurstAt;
        }

        public GraveRamValues Tuning = new GraveRamValues();

        readonly GraveRamTactics _tactics = new GraveRamTactics();
        EnemyValues _calm, _angry;
        readonly Stone[] _stones = new Stone[3];
        LineRenderer[] _stoneLines;
        MaterialPropertyBlock _block;
        Transform _lean;

        // The Ram.
        float _ramRun, _ramStone;
        bool _ramCrash;
        LancerStopper _ramStopper;
        bool _running, _chaining;
        int _chainLeft;
        EnemyAttackDefinition _rushing;
        Vector3 _runFrom, _runDir;
        float _runStart;

        // The throw.
        bool _throwing;
        int _thrown;
        float _throwStart;

        // The fight.
        float _wokeAt = -1f, _readyAt, _roarUntil = -1f, _phaseTwoAt = -1f;
        bool _broken, _roarPending, _wasDazed, _wasAwake;
        StaggerBar _heldStagger;
        EnemyState _lastState;
        int _counteredSeen;
        float _lowered, _posedAt = -1f;
        float _crashDazeUntil = -1f, _lastHurtAt = -10f;

        // What the log is told.
        int _rams, _crashes, _missed, _met, _counters, _guarded, _ramDamage, _stompDamage, _stonesDamage, _crashDazeDamage;
        float _fightSeconds, _slowSeconds, _crashDazeSeconds, _crashDazeShot;
        readonly System.Collections.Generic.List<string> _crashedOn = new System.Collections.Generic.List<string>(8);

        /// <summary>To the models it is a brute (it wears the brute's skeleton warrior, <see cref="EnemyLook"/> by kind); to everything else its own class.</summary>
        public override EnemyKind Kind => EnemyKind.Brute;
        /// <summary>Its values in its phase: its own attacks, its health, its walk.</summary>
        public override EnemyValues Values => _broken ? Angry : Calm;
        EnemyValues Calm => _calm ?? (_calm = new EnemyValues
        {
            Health = Tuning.Health, MoveSpeed = Tuning.MoveSpeed, AggroRange = 9f, LeashRange = 40f, RespawnDelay = 4f, Attacks = Tuning.Attacks(false),
        });
        EnemyValues Angry => _angry ?? (_angry = new EnemyValues
        {
            Health = Calm.Health, MoveSpeed = Calm.MoveSpeed, AggroRange = Calm.AggroRange, LeashRange = Calm.LeashRange, RespawnDelay = Calm.RespawnDelay,
            Attacks = Tuning.Attacks(true), UseModels = Calm.UseModels,
        });

        /// <summary>Its tactics: the opening, the draws, the rests. For tests.</summary>
        public GraveRamTactics Tactics => _tactics;
        public StaggerBar Stagger => _heldStagger ?? (_self != null ? _self.Stagger : null);
        /// <summary>The share of its health left, 0 to 1.</summary>
        public float HealthShare => _self != null && _self.MaxHealth > 0 ? Mathf.Clamp01(_self.CurrentHealth / (float)_self.MaxHealth) : 0f;
        public bool PhaseTwo => _broken;
        public bool IsRoaring => _roarUntil >= 0f;
        /// <summary>True while its Ram runs down its line.</summary>
        public bool IsRamming => _running;
        /// <summary>The Ram wound up or run now: its middle's run along its line, as swept at the tell's start, and whether it ends in a crash, on what.</summary>
        public float RamRun => _ramRun;
        public bool RamCrashes => _ramCrash;
        public LancerStopper RamStopper => _ramStopper;
        /// <summary>The strip drawn and judged: its middle's run and its body's front.</summary>
        public float StripLength => GraveRamRules.StripLength(_ramRun, Tuning.BodyRadius);
        public Vector3 RunFromPoint => _runFrom;
        public Vector3 RunDirection => _runDir;
        /// <summary>Why the last second Ram of phase 2 was not begun (null: it was, or none was asked). For tests.</summary>
        public string ChainRefused { get; private set; }
        /// <summary>How its last Ram ended.</summary>
        public RamEnd LastRamEnd { get; private set; }
        public LancerStopper LastStopper { get; private set; }
        public int Rams => _rams;
        public int Crashes => _crashes;
        public int Missed => _missed;
        public int MetHer => _met;
        public int Counters => _counters;
        /// <summary>Hits of its the hurt guard spared her.</summary>
        public int Guarded => _guarded;
        /// <summary>Damage it dealt her, by attack.</summary>
        public int RamDamage => _ramDamage;
        public int StompDamage => _stompDamage;
        public int StonesDamage => _stonesDamage;
        /// <summary>Seconds of its fight (from the wake), and of crash dazes, and of those in which she shot it; her share of the fight standing.</summary>
        public float FightSeconds => _fightSeconds;
        public float CrashDazeSeconds => _crashDazeSeconds;
        public float CrashDazeShotSeconds => _crashDazeShot;
        public int CrashDazeDamage => _crashDazeDamage;
        public float SlowShare => _fightSeconds > 1e-4f ? _slowSeconds / _fightSeconds : 0f;
        /// <summary>The game time it woke, under 0 before.</summary>
        public float WokeAt => _wokeAt;
        /// <summary>The stones of the throw now and the last, and their outlines (null before it has woken).</summary>
        public Stone[] Stones => _stones;
        public LineRenderer[] StoneLines => _stoneLines;
        /// <summary>0 to 1 how far it leans now (negative: it rears back in its roar).</summary>
        public float Lowered => _lowered;
        public Transform Lean => _lean;
        /// <summary>True while a crash's daze holds it (not a daze of her skills or a counter).</summary>
        public bool CrashDazed => _crashDazeUntil >= 0f && Time.time < _crashDazeUntil && IsDazed;

        // ---------------- Made ----------------

        /// <summary>
        /// The Grave Ram standing at <paramref name="at"/>, under <paramref name="parent"/>, made from the practice brute as the factory
        /// makes its kinds (<see cref="EnemyFactory"/>: a capsule on a NavMeshAgent, no collider, the practice enemy's telegraph and strike
        /// material), shown <see cref="GraveRamValues.Shown"/> as large, tinted, an elite with its own health and its final damage
        /// (<see cref="Enemy.DamageScale"/> 1: not the floor's scale), its stagger bar over it. It stays dead once killed. Null without a template.
        /// </summary>
        public static EnemyGraveRam Make(Vector3 at, Transform parent, EnemyBrute template, string name, ulong seed = 0UL)
        {
            if (template == null) return null;
            var go = new GameObject(name);
            go.SetActive(false); // Awake waits until it is put together
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), Quaternion.LookRotation(Vector3.back));
            var ram = go.AddComponent<EnemyGraveRam>();
            GraveRamValues t = ram.Tuning;
            float shown = Mathf.Max(0.1f, t.Shown);
            go.transform.localScale = Vector3.one * shown;

            var body = go.GetComponent<Damageable>();
            body.Radius = t.BodyRadius;
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            agent.radius = t.BodyRadius / shown; // the agent is scaled with it
            agent.height = t.BodyHeight / shown;
            agent.baseOffset = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = 20;

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            UnityEngine.Object.Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(go.transform, false);
            capsule.transform.localPosition = new Vector3(0f, t.BodyHeight / shown * 0.5f, 0f);
            capsule.transform.localScale = new Vector3(t.BodyRadius / shown * 2f, t.BodyHeight / shown * 0.5f, t.BodyRadius / shown * 2f);
            var capsuleRenderer = capsule.GetComponent<Renderer>();
            Renderer templateBody = null;
            foreach (Renderer r in template.GetComponentsInChildren<Renderer>(true))
                if (!(r is LineRenderer) && r.GetComponent<MarkPips>() == null) { templateBody = r; break; }
            Material material = templateBody != null && templateBody.sharedMaterial != null
                ? new Material(templateBody.sharedMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = EnemyFactory.OwnMaterialPrefix + "GraveRam";
            Color tint = InterfaceStyle.C(t.Tint);
            EnemyFactory.Paint(material, tint);
            capsuleRenderer.sharedMaterial = material;
            var flash = go.AddComponent<HitFlash>();
            flash.Target = capsuleRenderer;

            LineRenderer telegraph = null;
            if (template.Telegraph != null)
            {
                GameObject tg = UnityEngine.Object.Instantiate(template.Telegraph.gameObject, parent, false);
                tg.name = name + "_Telegraph";
                tg.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                tg.transform.localScale = Vector3.one;
                telegraph = tg.GetComponent<LineRenderer>();
            }

            ram.Player = template.Player;
            ram.PlayerHealth = template.PlayerHealth;
            ram.Telegraph = telegraph;
            ram.StrikeMaterial = template.StrikeMaterial;
            ram.BodyRenderer = capsuleRenderer;
            ram.BodyColor = tint;
            ram.Calm.UseModels = template.Values.UseModels;
            ram.IsElite = true;
            ram.HealthOverride = t.Health;
            ram.DamageScale = 1f; // final: not the floor's scale (R7 5.1)
            ram.ComesBack = false;
            ram._tactics.Seed(seed);
            go.SetActive(true);
            if (template.Player != null) StaggerBarView.For(body, template.Player.ShotFxMaterial);
            return ram;
        }

        protected override void Awake()
        {
            base.Awake();
            _self.Stagger = new StaggerBar(Tuning.Stagger);
            if (Look != null && BodyRenderer != null)
            {
                BodyColor = InterfaceStyle.C(Tuning.Tint);
                EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
            }
            _lean = new GameObject("Lean").transform;
            _lean.gameObject.layer = gameObject.layer;
            _lean.SetParent(transform, false);
            if (Look != null) Look.Pivot.SetParent(_lean, false);
            MakeStoneLines();
            _self.Damaged += OnHurt;
            Reported += OnOwnReport;
            if (GetComponent<GraveRamBar>() == null) gameObject.AddComponent<GraveRamBar>();
        }

        void MakeStoneLines()
        {
            _stoneLines = new LineRenderer[_stones.Length];
            if (Telegraph == null) return;
            for (int i = 0; i < _stoneLines.Length; i++)
            {
                GameObject go = Instantiate(Telegraph.gameObject, transform.parent, false);
                go.name = name + "_Stone_" + (i + 1);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                LineRenderer line = go.GetComponent<LineRenderer>();
                line.loop = true;
                line.useWorldSpace = true;
                line.enabled = false;
                _stoneLines[i] = line;
            }
        }

        protected override void OnDestroy()
        {
            if (_wasAwake && (_self == null || _self.IsAlive)) NoteFight(GraveRamEvents.Removed);
            if (_self != null) _self.Damaged -= OnHurt;
            Reported -= OnOwnReport;
            if (_stoneLines != null)
                foreach (LineRenderer l in _stoneLines) if (l != null) Destroy(l.gameObject);
            if (Telegraph != null && Telegraph.transform.parent != transform) Destroy(Telegraph.gameObject);
            base.OnDestroy();
        }

        // ---------------- Every frame ----------------

        protected override void Update()
        {
            float now = Time.time;
            if (IsDead)
            {
                _running = false;
                _throwing = false;
                HideStones();
                EndRoar();
            }
            else
            {
                bool dazed = IsDazed;
                if (_wasDazed && !dazed) _readyAt = now + Tuning.GapOf(_broken); // a daze over: the gap, then its next tell
                _wasDazed = dazed;
                Temper(now);
            }
            base.Update();
            if (!IsDead)
            {
                EnemyState st = State;
                if (_lastState == EnemyState.Recover && st == EnemyState.Chase) _readyAt = now + Tuning.GapOf(_broken);
                _lastState = st;
                TickThrow(now);
            }
            TickStones(now);
            TellCounter();
            Pose(now);
            TintDazed();
            Follow(now);
        }

        /// <summary>One frame of it as the engine runs it, for the test of what a frame allocates.</summary>
        public void FrameNow() => Update();

        /// <summary>Phase 2 at half its health: the roar, once (as the boss's break, work order 106), when it is not dazed.</summary>
        void Temper(float now)
        {
            if (!_broken && _wokeAt >= 0f && HealthShare < Tuning.PhaseTwoBelow)
            {
                _broken = true;
                _phaseTwoAt = now;
                _roarPending = true;
                PlaceholderSounds.Current?.AskFor(PlaceholderSound.BossRoar);
                Report(EnemyReportKind.Angered);
            }
            if (_roarPending && !IsDazed) StartRoar(now);
            if (_roarUntil >= 0f && now >= _roarUntil)
            {
                EndRoar();
                _readyAt = now + Tuning.GapOf(true);
            }
        }

        void StartRoar(float now)
        {
            _roarPending = false;
            _brain.Cancel(now); // what it was winding up is dropped; it stands, held by the roar
            Turns.Ended(Id);
            _running = false;
            _throwing = false;
            _chainLeft = 0;
            if (_heldStagger == null && _self != null && _self.Stagger != null)
            {
                _heldStagger = _self.Stagger;
                _self.Stagger = null; // no hit takes stagger while it roars; her damage still counts (the boss's roar)
            }
            _roarUntil = now + Mathf.Max(0f, Tuning.RoarSeconds);
        }

        void EndRoar()
        {
            _roarPending = false;
            _roarUntil = -1f;
            if (_heldStagger != null && _self != null && _self.Stagger == null) _self.Stagger = _heldStagger;
            _heldStagger = null;
        }

        protected override void OnWoke()
        {
            _wokeAt = Time.time;
            _wasAwake = true;
            _readyAt = _wokeAt;
        }

        protected override bool MayAttack(Vector3 toPlayer, float distance) => !IsRoaring && !_roarPending;

        /// <summary>
        /// Between attacks: standing in its roar; holding its place facing her while she is in its Ram's band, its line clear and its Ram
        /// reaching her, its whole body on his screen (its next Ram comes when its rest and gap are over: it does not walk in under its
        /// band); coming in to her when she is within its Stomp's reach (and <see cref="GraveRamValues.StompComeIn"/>) and a Stomp may
        /// follow; else walking to a place for a Ram (<see cref="GraveRamValues.SeekNear"/>), or with none to her; never closer than
        /// <see cref="GraveRamValues.StopShortOfHer"/> (it does not stand on her).
        /// </summary>
        protected override void Chase(Vector3 toPlayer, float distance)
        {
            float now = Time.time;
            if (IsRoaring || (distance <= Tuning.StopShortOfHer && _tactics.StompMayFollow(now, Tuning)))
            {
                Halt(); // against her with its Stomp to come: it stands (it does not stand on her)
                Face(toPlayer);
                return;
            }
            GraveRamSituation s = Seen(distance, now);
            float dt = Time.deltaTime;
            if (s.OnScreen && s.LineClear && s.RamReaches)
            {
                HoldSeconds += dt;
                Halt();
                Face(toPlayer);
                return;
            }
            if (distance <= Tuning.StompAttack.MaxDistance + Tuning.StompComeIn && _tactics.StompMayFollow(now, Tuning))
            {
                ToHerSeconds += dt;
                Walk(Player.Position); // she is close, its Stomp ready: it comes in
                return;
            }
            bool spot = Seek(now);
            if (spot) ToSpotSeconds += dt;
            else ToHerSeconds += dt;
            if (spot) Walk(_spot);
            else if (distance > Tuning.StopShortOfHer) Walk(Player.Position);
            else
            {
                Halt(); // against her, no place for a Ram and no Stomp yet: it stands
                Face(toPlayer);
            }
        }

        Vector3 _spot;
        bool _spotFound;
        float _soughtAt = float.NegativeInfinity;

        /// <summary>Where it walks for a Ram (<see cref="GraveRamValues.SeekNear"/>); false with none. For tests.</summary>
        public bool HasSpot => _spotFound;
        public Vector3 Spot => _spot;
        /// <summary>
        /// For tests and its bots: its looks for a place for a Ram, those that found one, and the places turned down and why (0 no walkable
        /// ground there, 1 not walkable straight from her, 2 out of its band, 3 its sight cut, 4 off his screen, 5 its Ram short of her);
        /// and its seconds in the chase holding for a Ram, walking to a place for one, and walking to her.
        /// </summary>
        public int SeekLooks { get; private set; }
        public int SeekFinds { get; private set; }
        public readonly int[] SeekRejects = new int[6];
        public float HoldSeconds { get; private set; }
        public float ToSpotSeconds { get; private set; }
        public float ToHerSeconds { get; private set; }

        /// <summary>A place for a Ram round her (<see cref="GraveRamValues.SeekNear"/>), looked for every <see cref="GraveRamValues.SeekEvery"/> s; false with none.</summary>
        bool Seek(float now)
        {
            if (now - _soughtAt < Tuning.SeekEvery) return _spotFound;
            _soughtAt = now;
            _spotFound = false;
            SeekLooks++;
            if (Player == null) return false;
            Vector3 her = Flat(Player.Position), me = Flat(transform.position);
            if (!NavMesh.SamplePosition(her, out NavMeshHit herHit, SampleReach, NavMesh.AllAreas)) return false;
            float best = float.MaxValue;
            int ways = Mathf.Max(4, Tuning.SeekWays);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? Tuning.SeekNear : Tuning.SeekFar;
                for (int k = 0; k < ways; k++)
                {
                    float a = k * Mathf.PI * 2f / ways;
                    Vector3 p = her + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, SampleReach, NavMesh.AllAreas)) { SeekRejects[0]++; continue; }
                    Vector3 at = Flat(hit.position);
                    float cost = Flat(at - me).magnitude;
                    if (cost >= best) continue;
                    if (NavMesh.Raycast(herHit.position, hit.position, out NavMeshHit _, NavMesh.AllAreas)) { SeekRejects[1]++; continue; } // walkable straight from her: her open ground
                    Vector3 d = her - at;
                    float length = d.magnitude;
                    if (length < Tuning.RamAttack.MinDistance || length > Tuning.RamAttack.MaxDistance) { SeekRejects[2]++; continue; }
                    if (Cover.ClearDistance(at, d / length, length, Tuning.SightHeight) < length - 1e-3f) { SeekRejects[3]++; continue; }
                    if (!BodyOnScreen(at)) { SeekRejects[4]++; continue; }
                    float run = RunFrom(at, d / length, Tuning.RamAttack.Length, out _, out _, out _);
                    if (!GraveRamRules.RamReachesHer(run, Tuning.BodyRadius, length, Placeholders.PlayerRadius)) { SeekRejects[5]++; continue; }
                    best = cost;
                    _spot = at;
                    _spotFound = true;
                }
            }
            if (_spotFound) SeekFinds++;
            return _spotFound;
        }

        // ---------------- Its choice ----------------

        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values)
        {
            float now = Time.time;
            if (Player == null || now < _readyAt || _wokeAt < 0f || now < _wokeAt + Tuning.FirstAttackAfter) return null;
            return _tactics.Choose(Seen(distance, now), Tuning, values.Attacks);
        }

        int _seenFrame = -1;
        GraveRamSituation _seen;

        /// <summary>What it knows this frame (<see cref="Situation"/>), looked once a frame and kept for its choice and its walk.</summary>
        GraveRamSituation Seen(float distance, float now)
        {
            int frame = Time.frameCount;
            if (frame != _seenFrame)
            {
                _seenFrame = frame;
                _seen = Situation(distance, now);
            }
            return _seen;
        }

        /// <summary>What it knows now, for its tactics: his screen, its line, whether a Ram would reach her, the Stomp's circle seen.</summary>
        public GraveRamSituation Situation(float distance, float now)
        {
            var s = new GraveRamSituation { Now = now, WokeAt = _wokeAt, Distance = distance, PhaseTwo = _broken };
            s.OnScreen = BodyOnScreen();
            if (!s.OnScreen) return s;
            s.LineClear = LineClear();
            EnemyAttackDefinition ram = Tuning.RamAttack;
            if (s.LineClear && distance >= ram.MinDistance && distance <= ram.MaxDistance)
            {
                Vector3 d = Flat(Player.Position - transform.position);
                float run = d.sqrMagnitude > 1e-6f ? RunFrom(transform.position, d.normalized, ram.Length, out _, out _, out _) : 0f;
                s.RamReaches = GraveRamRules.RamReachesHer(run, Tuning.BodyRadius, distance, Placeholders.PlayerRadius);
            }
            EnemyAttackDefinition stomp = Tuning.StompAttack;
            s.StompSeen = distance <= stomp.MaxDistance && StompSeen(stomp.Radius);
            return s;
        }

        /// <summary>
        /// Is the Stomp's circle on his screen on her side: its edge toward her, at 45 degrees and at right angles to her both ways (the
        /// edge she walks out over), off the plates? Its far side, away from her, may be under the skill strip when it stands below her
        /// on his screen.
        /// </summary>
        public bool StompSeen(float radius)
        {
            Vector3 c = Flat(transform.position);
            Vector3 toHer = Player != null ? Flat(Player.Position) - c : Vector3.zero;
            if (toHer.sqrMagnitude < 0.09f)
            {
                Camera cam = Camera.main;
                toHer = cam != null ? Flat(cam.transform.forward) : Vector3.forward; // on her: up his screen
            }
            toHer.Normalize();
            for (int i = 0; i < 5; i++) s_points[i] = c + Quaternion.Euler(0f, (i - 2) * 45f, 0f) * toHer * radius;
            return PointsOnScreen(5);
        }

        /// <summary>Its line to her clear: nothing at its sight height (walls, shut doors, tall props), and the walkable ground unbroken (a low prop is a hole in it).</summary>
        public bool LineClear()
        {
            if (Player == null) return false;
            Vector3 from = Flat(transform.position), d = Flat(Player.Position) - from;
            float length = d.magnitude;
            if (length < 1e-3f) return true;
            if (Cover.ClearDistance(from, d / length, length, Tuning.SightHeight) < length - 1e-3f) return false;
            return !_agent.isOnNavMesh || !_agent.Raycast(Player.Position, out NavMeshHit _);
        }

        // ---------------- His screen ----------------

        /// <summary>
        /// Is its whole body on his screen: the box round its feet and the top of its head, as the game's camera shows them, inside
        /// the view and off the top plate and the skill strip by <see cref="GraveRamValues.ScreenMargin"/>? True with no camera.
        /// </summary>
        public bool BodyOnScreen() => BodyOnScreen(transform.position);

        /// <summary>Would its whole body be on his screen standing at <paramref name="at"/>?</summary>
        public bool BodyOnScreen(Vector3 at)
        {
            Vector3 c = Flat(at);
            float r = Tuning.BodyRadius, h = ModelHeight > 0f ? ModelHeight : Tuning.BodyHeight;
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = i == 0 ? Vector3.right : i == 1 ? Vector3.left : i == 2 ? Vector3.forward : Vector3.back;
                s_points[i] = c + side * r;
                s_points[i + 4] = c + side * r + Vector3.up * h;
            }
            return PointsOnScreen(8);
        }

        /// <summary>Is a circle on the ground on his screen, wholly (its eight points round it), off the plates?</summary>
        public bool CircleOnScreen(Vector3 center, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                s_points[i] = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            }
            return PointsOnScreen(8);
        }

        bool PointsOnScreen(int n)
        {
            Camera cam = Camera.main;
            if (cam == null) return true;
            float viewWidth = LayoutHeight * Mathf.Max(0.1f, cam.aspect);
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 v = cam.WorldToViewportPoint(s_points[i]);
                if (v.z <= 0f) return false;
                float x = v.x * viewWidth, y = (1f - v.y) * LayoutHeight;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
            InterfaceValues ui = InterfaceStyle.V;
            UiBox plate = ui.TopPlate ? InterfacePlate.Whole(ui) : default;
            UiBox strip = GraveRamRules.StripKeepOut(InterfaceRules.WholeStrip(SkillLoadout.KeyCount, viewWidth, ui), LayoutHeight, Tuning);
            return GraveRamRules.BoxOnScreen(left, top, right, bottom, viewWidth, LayoutHeight, plate, strip, Tuning.ScreenMargin);
        }

        // ---------------- The Ram ----------------

        /// <summary>
        /// How far its middle runs from <paramref name="from"/> along the flat unit <paramref name="dir"/> on a Ram of
        /// <paramref name="length"/> (<see cref="GraveRamRules.RunLength"/>): to where its body (a capsule of its radius, from just over the
        /// ground to <see cref="GraveRamValues.ProbeTop"/>) first meets a wall, a pier, a shut door or a prop tall or low (<paramref name="stone"/>
        /// m along, what it is <paramref name="stopper"/>), or to the end of the walkable ground, or its length. <paramref name="crash"/>:
        /// it ends on that stone.
        /// </summary>
        public float RunFrom(Vector3 from, Vector3 dir, float length, out bool crash, out float stone, out LancerStopper stopper)
        {
            from = Flat(from);
            float r = Mathf.Max(0.05f, Tuning.BodyRadius);
            Vector3 low = from + Vector3.up * (Tuning.ProbeLift + r), high = from + Vector3.up * Mathf.Max(Tuning.ProbeLift + r, Tuning.ProbeTop);
            stone = float.PositiveInfinity;
            stopper = LancerStopper.None;
            int n = Physics.CapsuleCastNonAlloc(low, high, r, dir, s_hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = s_hits[i];
                if (h.collider == null || h.distance <= 0f || h.distance >= stone) continue; // a cast that starts inside a thing does not see it
                LancerStopper s = StopperOf(h.collider);
                if (s == LancerStopper.None) continue;
                stone = h.distance;
                stopper = s;
            }
            float walkable = length;
            if (NavMesh.SamplePosition(from, out NavMeshHit start, SampleReach, NavMesh.AllAreas)
                && NavMesh.Raycast(start.position, start.position + dir * length, out NavMeshHit edge, NavMesh.AllAreas))
                walkable = Flat(edge.position - start.position).magnitude;
            float run = GraveRamRules.RunLength(length, stone, walkable, Tuning.EdgeSlack, out crash);
            if (!crash) stopper = LancerStopper.None;
            return run;
        }

        /// <summary>What a collider is to a Ram (the lancer's rule): a shut door, a prop tall or low, a wall or a pier; nothing for anything else.</summary>
        static LancerStopper StopperOf(Collider c)
        {
            RoomDoor door = c.GetComponentInParent<RoomDoor>();
            if (door != null) return door.Shut ? LancerStopper.Door : LancerStopper.None;
            Shelter shelter = c.GetComponentInParent<Shelter>();
            Cover cover = c.GetComponentInParent<Cover>();
            if (shelter == null && cover == null) return LancerStopper.None;
            GameObject owner = shelter != null ? shelter.gameObject : cover.gameObject;
            if (!owner.name.StartsWith("Prop", StringComparison.Ordinal)) return LancerStopper.Wall;
            return cover != null && cover.Stops && (shelter == null || !shelter.LowProp) ? LancerStopper.TallProp : LancerStopper.LowProp;
        }

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            float now = Time.time;
            if (!_chaining) _tactics.Began(attack, now);
            if (attack == null) return;
            if (attack.Rush > 0f)
            {
                _rams++;
                _ramRun = RunFrom(transform.position, AimDirection(), attack.Length, out _ramCrash, out _ramStone, out _ramStopper);
                if (!_chaining) _chainLeft = _broken ? Mathf.Max(0, Tuning.PhaseTwoRams - 1) : 0;
            }
            else if (attack.Name == GraveRamValues.Stones)
            {
                _throwing = true;
                _thrown = 0;
                _throwStart = now;
                TickThrow(now); // the first stone now, at her feet
            }
        }

        /// <summary>The Ram's strip reaches to its body's front where it stops; the others their own.</summary>
        protected override float Reach(EnemyAttackDefinition attack) => attack.Rush > 0f ? StripLength : attack.Length;

        /// <summary>The Grave Stones' tell is their circles, not one round its feet: nothing of its own on the floor while it throws.</summary>
        public override bool ReadOnBody => Attack != null && Attack.Name == GraveRamValues.Stones;

        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (attack == null) return false;
            if (attack.Name == GraveRamValues.Stones) return true; // its stones burst on their own clock
            if (attack.Rush <= 0f) return false;
            _rushing = attack;
            _running = true;
            _runFrom = Flat(transform.position);
            _runDir = AimDirection();
            _runStart = Time.time;
            Face(_runDir);
            Report(EnemyReportKind.Charged, attack);
            return true;
        }

        protected override void AfterMove()
        {
            if (_running) Run();
        }

        /// <summary>One frame of the run: along its line as far as it should be by now; then whether its strip so far met her, or it reached its end.</summary>
        void Run()
        {
            HoldRecovery(); // it stands its recovery where it stops, from then
            float now = Time.time;
            float s = Mathf.Min(_ramRun, (now - _runStart) * _rushing.Rush);
            Vector3 target = _runFrom + _runDir * s;
            Vector3 before = transform.position;
            if (_agent.isOnNavMesh) _agent.Move(Flat(target - before));
            Vector3 after = transform.position;
            float ran = Vector3.Dot(Flat(after) - _runFrom, _runDir);
            bool herThere = Player != null && (PlayerHealth == null || PlayerHealth.IsAlive);
            if (herThere && GraveRamRules.InStrip(Ground(_runFrom), Ground(_runDir), GraveRamRules.StripLength(ran, Tuning.BodyRadius), _rushing.HalfWidth,
                                                  Player.GroundPosition, Placeholders.PlayerRadius))
            {
                EndRam(RamEnd.MetHer);
                return;
            }
            bool blocked = Flat(after - target).magnitude > Tuning.BlockedSlack;
            if (blocked)
            {
                // The walkable ground ended under it short of its end: stone just ahead is a crash there, else a miss.
                _ramRun = Mathf.Max(0f, ran);
                EndRam(_ramCrash && _ramStone - ran <= Tuning.EdgeSlack + Tuning.BlockedSlack ? RamEnd.Crashed : RamEnd.Missed);
                return;
            }
            if (s >= _ramRun - 1e-4f) EndRam(_ramCrash ? RamEnd.Crashed : RamEnd.Missed);
        }

        void EndRam(RamEnd end)
        {
            _running = false;
            LastRamEnd = end;
            if (_agent.isOnNavMesh) _agent.velocity = Vector3.zero;
            float now = Time.time;
            _tactics.RamEnded(now);
            EnemyAttackDefinition attack = _rushing;
            switch (end)
            {
                case RamEnd.MetHer:
                    _met++;
                    Land(attack, true);
                    break;
                case RamEnd.Missed:
                    _missed++;
                    Land(attack, false);
                    break;
                case RamEnd.Crashed:
                {
                    _crashes++;
                    LastStopper = _ramStopper;
                    string on = LancerRules.StopperWord(_ramStopper);
                    if (_crashedOn.Count < 8) _crashedOn.Add(on);
                    bool cover = Player != null && LancerRules.CoverStoppedIt(Ground(_runFrom), Ground(_runDir), _ramRun, attack.HalfWidth,
                                                                             Player.GroundPosition, Placeholders.PlayerRadius);
                    Report(EnemyReportKind.Struck, attack, cover ? StrikeOutcome.StoppedByWall : StrikeOutcome.OutOfArea);
                    PlaceholderSounds.Current?.AskFor(SoundMoment.LancerCrashed);
                    Crash(now);
                    return;
                }
            }
            // Phase 2: a second Ram at once, aimed where she stands now, when that one too reaches her and it is on his screen.
            if (_chainLeft <= 0 || Player == null || (PlayerHealth != null && !PlayerHealth.IsAlive)) return;
            Vector3 d = Flat(Player.Position - transform.position);
            float distance = d.magnitude;
            EnemyAttackDefinition next = Values.Attacks[0];
            ChainRefused = distance < 1e-3f || distance < next.MinDistance || distance > next.MaxDistance ? "her distance"
                         : !BodyOnScreen() ? "off his screen" : !LineClear() ? "no clear line" : null;
            if (ChainRefused != null) return;
            float run = RunFrom(transform.position, d / distance, next.Length, out _, out _, out _);
            if (!GraveRamRules.RamReachesHer(run, Tuning.BodyRadius, distance, Placeholders.PlayerRadius))
            {
                ChainRefused = "its line ends short of her";
                return;
            }
            _chainLeft--;
            _chaining = true;
            BeginWindup(next, Player.Position);
            _chaining = false;
        }

        /// <summary>
        /// The crash dazes it by its stagger bar emptied at once, for <see cref="GraveRamValues.CrashDaze"/> (its bar's own daze, from her
        /// stagger skills, is its elite's 4 s), so it takes the bar's dazed damage, 1.5 times. Without a bar (held aside in its roar), the
        /// counter's daze of that long.
        /// </summary>
        void Crash(float now)
        {
            StaggerBar bar = _self.Stagger;
            _crashDazeUntil = now + Tuning.CrashDaze;
            if (bar != null && !bar.IsDazed(now))
            {
                float own = Tuning.Stagger.DazeSeconds;
                Tuning.Stagger.DazeSeconds = Tuning.CrashDaze; // the bar reads its values as it empties: the crash's daze, then its own again
                bar.Take(Mathf.Max(bar.Current, 1e-3f), now, out bool dazed);
                Tuning.Stagger.DazeSeconds = own;
                if (dazed)
                {
                    _self.DazeFor(0f); // raises the daze: its bar holds it for its time
                    return;
                }
            }
            _self.DazeFor(Tuning.CrashDaze);
        }

        protected override void OnDaze()
        {
            // Dazed mid-run (her stagger skills, a counter): it stops where it is; a throw stops too.
            if (_running)
            {
                _running = false;
                _tactics.RamEnded(Time.time);
            }
            _throwing = false;
            _chainLeft = 0;
        }

        protected override Vector3 LookVelocity => _running && _rushing != null ? _runDir * _rushing.Rush : base.LookVelocity;

        // ---------------- The Grave Stones ----------------

        /// <summary>While it throws: each stone where she stands when it appears (no lead), on walkable ground.</summary>
        void TickThrow(float now)
        {
            if (!_throwing || Player == null) return;
            // Round 2: it throws only while its whole body is on his screen, for the whole throw (it stands still as it throws; she
            // walking can take it off his screen): seen no more, the stones not yet thrown are not thrown.
            if (_thrown > 0 && !BodyOnScreen())
            {
                _throwing = false;
                ThrowsCutShort++;
                return;
            }
            int count = Mathf.Min(_stones.Length, Mathf.Max(1, Tuning.StonesCount));
            while (_thrown < count && now >= GraveRamRules.StoneAppears(_thrown, _throwStart, Tuning) - 1e-5f)
            {
                int i = _thrown++;
                Vector3 at = Flat(Player.Position);
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, SampleReach, NavMesh.AllAreas)) at = Flat(hit.position);
                _stones[i] = new Stone { Pending = true, Center = at, PlacedAt = now, BurstAt = now + Tuning.StoneBurst(_broken) };
                LineRenderer line = _stoneLines != null ? _stoneLines[i] : null;
                if (line != null)
                {
                    StoneOutline(line, at);
                    line.widthMultiplier = TelegraphThin;
                    line.enabled = true;
                }
                Report(EnemyReportKind.CirclePlaced, Tuning.StonesAttack, count: i + 1);
            }
            if (_thrown >= count) _throwing = false;
        }

        static readonly Vector3[] s_ring = new Vector3[32];

        /// <summary>Times a throw was cut short because it was no longer on his screen (round 2). For tests.</summary>
        public int ThrowsCutShort { get; private set; }

        /// <summary>
        /// A stone's outline, clipped at the wall (round 2): round it, as far as its radius or, where the walkable ground ends nearer,
        /// that end and <see cref="GraveRamValues.StoneWallSlack"/> (the wall's face), never under a wall. Its hit is the same circle:
        /// her middle never stands past the walkable ground's end.
        /// </summary>
        void StoneOutline(LineRenderer line, Vector3 center)
        {
            float r = Tuning.StonesAttack.Radius;
            bool sampled = NavMesh.SamplePosition(center, out NavMeshHit start, SampleReach, NavMesh.AllAreas);
            for (int i = 0; i < s_ring.Length; i++)
            {
                float a = i / (float)s_ring.Length * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = r;
                if (sampled && NavMesh.Raycast(start.position, start.position + dir * r, out NavMeshHit edge, NavMesh.AllAreas))
                    reach = Mathf.Min(r, Flat(edge.position - start.position).magnitude + Tuning.StoneWallSlack);
                Vector3 p = center + dir * reach;
                s_ring[i] = new Vector3(p.x, TelegraphHeight, p.z);
            }
            line.positionCount = s_ring.Length;
            line.SetPositions(s_ring);
        }

        /// <summary>Its stones' outlines' points now (for tests): the one of stone <paramref name="i"/>.</summary>
        public LineRenderer StoneLine(int i) => _stoneLines != null && i >= 0 && i < _stoneLines.Length ? _stoneLines[i] : null;

        void TickStones(float now)
        {
            for (int i = 0; i < _stones.Length; i++)
            {
                if (!_stones[i].Pending) continue;
                LineRenderer line = _stoneLines != null ? _stoneLines[i] : null;
                if (now >= _stones[i].BurstAt)
                {
                    _stones[i].Pending = false;
                    if (line != null) line.enabled = false;
                    if (IsDead) continue;
                    SpawnBurstFx(_stones[i].Center);
                    if (Player != null)
                        Land(Tuning.StonesAttack, SkillRules.InCircle(Ground(_stones[i].Center), Tuning.StonesAttack.Radius, Player.GroundPosition, Placeholders.PlayerRadius));
                }
                else if (line != null)
                {
                    float span = Mathf.Max(1e-4f, _stones[i].BurstAt - _stones[i].PlacedAt);
                    line.widthMultiplier = Mathf.Lerp(TelegraphThin, TelegraphThick, Mathf.Clamp01((now - _stones[i].PlacedAt) / span));
                }
            }
        }

        void HideStones()
        {
            for (int i = 0; i < _stones.Length; i++)
            {
                _stones[i].Pending = false;
                if (_stoneLines != null && _stoneLines[i] != null && _stoneLines[i].enabled) _stoneLines[i].enabled = false;
            }
        }

        /// <summary>Drops whatever it was doing and its stones on the ground, as a daze would and more (for tests that put it somewhere new).</summary>
        public void DropAllForTests()
        {
            HideStones();
            _throwing = false;
            _running = false;
            _self.DazeFor(0.001f);
        }

        void SpawnBurstFx(Vector3 center)
        {
            // Drawn as the effects draw a circle strike (as the caster's circles, work order 034); the flat stand-in when they are not running.
            if (StrikeAtDrawer != null && StrikeAtDrawer(this, Tuning.StonesAttack, center)) return;
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fx.name = "StrikeFx";
            Destroy(fx.GetComponent<Collider>());
            fx.transform.SetParent(transform.parent, false);
            fx.transform.position = center + Vector3.up * 0.04f;
            fx.transform.localScale = new Vector3(Tuning.StonesAttack.Radius * 2f, 0.02f, Tuning.StonesAttack.Radius * 2f);
            if (StrikeMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = StrikeMaterial;
            Destroy(fx, BurstFxSeconds);
        }

        // ---------------- What it tells ----------------

        /// <summary>A counter dropped its Ram's wind-up (Stopping Shot): told to the log as the lancer's is.</summary>
        void TellCounter()
        {
            if (Countered == _counteredSeen) return;
            _counteredSeen = Countered;
            _counters++;
            _chainLeft = 0;
            Report(EnemyReportKind.Countered, Tuning.RamAttack);
        }

        /// <summary>Its own reports: a hit of its that hurt her gives her the hurt guard; a hit the guard spared is counted; her damage by attack.</summary>
        void OnOwnReport(EnemyReport e)
        {
            if (e.Kind != EnemyReportKind.Struck) return;
            if (e.Outcome == StrikeOutcome.Landed && e.Damage > 0)
            {
                if (e.Attack == GraveRamValues.Ram) _ramDamage += e.Damage;
                else if (e.Attack == GraveRamValues.Stomp) _stompDamage += e.Damage;
                else if (e.Attack == GraveRamValues.Stones) _stonesDamage += e.Damage;
                EliteHurtGuard.Begin(PlayerHealth, Tuning);
            }
            else if (e.Outcome == StrikeOutcome.NoEffect && EliteHurtGuard.Spared(PlayerHealth)) _guarded++;
        }

        void OnHurt(int amount)
        {
            _lastHurtAt = Time.time;
            if (CrashDazed) _crashDazeDamage += amount;
        }

        /// <summary>The fight's measures, a frame at a time; and its line once it is over.</summary>
        void Follow(float now)
        {
            if (_wasAwake && !IsDead && IsAwake)
            {
                float dt = Time.deltaTime;
                _fightSeconds += dt;
                if (Player != null && Flat(Player.Velocity).magnitude < Tuning.SlowBelow) _slowSeconds += dt;
                if (CrashDazed)
                {
                    _crashDazeSeconds += dt;
                    if (now - _lastHurtAt <= Tuning.ShotWindow) _crashDazeShot += dt;
                }
            }
            if (_wasAwake && !IsAwake) NoteFight(IsDead ? GraveRamEvents.Died : GraveRamEvents.HerDeath);
        }

        void NoteFight(string how)
        {
            _wasAwake = false;
            if (_wokeAt < 0f) return;
            float end = Time.time;
            PlaySessionRecorder.Current?.NoteGraveRamFight(end - _wokeAt, how, _ramDamage, _stompDamage, _stonesDamage, _rams, _crashes, _missed, _met,
                                                          _counters, CrashWords(), _crashDazeSeconds, _crashDazeShot, _crashDazeDamage, SlowShare, _guarded,
                                                          _phaseTwoAt >= 0f ? end - _phaseTwoAt : 0f);
            _wokeAt = -1f;
        }

        string CrashWords()
        {
            if (_crashedOn.Count == 0) return "";
            return string.Join(";", _crashedOn);
        }

        // ---------------- Its look ----------------

        bool _dazedTinted;
        float _dazeFrom = -1f, _dazeUntil = -1f;

        /// <summary>
        /// Round 2: while dazed its body is <see cref="GraveRamValues.DazedTint"/>, a deep blue, in place of the game's pale daze blue that
        /// did not show on ash white; back to ash white as the daze ends (the base's own tint at the daze's end does that).
        /// </summary>
        void TintDazed()
        {
            bool dazed = !IsDead && IsDazed;
            if (dazed && !_dazedTinted)
            {
                _dazeFrom = Time.time;
                StaggerBar bar = _self != null ? _self.Stagger : null;
                _dazeUntil = CrashDazed ? _crashDazeUntil : bar != null && bar.IsDazed(Time.time) ? bar.DazedUntil : -1f;
            }
            Renderer[] parts = Look != null ? Look.Renderers : null;
            if (parts != null && (_partColours == null || _partColours.Length != parts.Length))
            {
                _partColours = new Color[parts.Length];
                for (int i = 0; i < parts.Length; i++) _partColours[i] = parts[i] != null && parts[i].sharedMaterial != null ? parts[i].sharedMaterial.color : Color.white;
            }
            if (dazed)
            {
                // Every part of its body (the skeleton's bones, its cloth, its shield, not only its largest mesh), painted over again while the
                // base's pale daze tint, a frame after the daze begins (Enemy.TintForDaze), shows.
                Color deep = InterfaceStyle.C(Tuning.DazedTint);
                if (BodyRenderer != null && BodyRenderer.sharedMaterial != null && BodyRenderer.sharedMaterial.color != deep)
                    EnemyFactory.Paint(BodyRenderer.sharedMaterial, deep);
                if (parts != null)
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i] != null && parts[i] != BodyRenderer && parts[i].sharedMaterial != null && parts[i].sharedMaterial.color != deep)
                            EnemyFactory.Paint(parts[i].sharedMaterial, deep);
            }
            else if (_dazedTinted && parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i] != null && parts[i] != BodyRenderer && parts[i].sharedMaterial != null) EnemyFactory.Paint(parts[i].sharedMaterial, _partColours[i]);
            }
            _dazedTinted = dazed;
        }

        Color[] _partColours;

        /// <summary>
        /// The share of the running daze still to come, 1 to 0, for its bar (round 2: "it is open now, for this long"); negative when it
        /// is not dazed or the daze's end is not known (Stopping Shot's).
        /// </summary>
        public float DazeLeftShare
        {
            get
            {
                if (!IsDazed || _dazeUntil <= _dazeFrom) return -1f;
                return Mathf.Clamp01((_dazeUntil - Time.time) / (_dazeUntil - _dazeFrom));
            }
        }

        /// <summary>True while its body shows its dazed tint. For tests.</summary>
        public bool ShowsDazedTint => _dazedTinted;

        /// <summary>Its lean: forward over the first part of a Ram's wind-up and held through the run, back while it roars, up again after.</summary>
        void Pose(float now)
        {
            float target = 0f;
            if (!IsDead)
            {
                if (IsRoaring) target = Tuning.LeanDegrees != 0f ? Tuning.RoarDegrees / Tuning.LeanDegrees : 0f;
                else if (_running) target = 1f;
                else if (!IsDazed && State == EnemyState.Windup && Attack != null && Attack.Rush > 0f)
                    target = GraveRamRules.Lean(Attack.Windup > 0f ? 1f - WindupRemaining / Attack.Windup : 1f, Tuning);
            }
            float dt = _posedAt < 0f ? 0f : Mathf.Max(0f, now - _posedAt);
            _posedAt = now;
            float step = dt / Mathf.Max(1e-3f, Tuning.RaiseSeconds);
            if (target > _lowered && target > 0f) _lowered = target; // the lean follows the wind-up
            else _lowered = Mathf.MoveTowards(_lowered, target, step); // up again, or back into the roar, over its time
            if (_lean != null) _lean.localRotation = Quaternion.Euler(Tuning.LeanDegrees * _lowered, 0f, 0f);
        }
    }
}
