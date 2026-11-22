using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The warder (work order 079, 2026-11-22: Kristian ticked "Enda en fiendetype"; the producer's proposal, for him to
    /// correct after he has met it): a slow support that keeps to the back of its pack and wards its allies. Its numbers are
    /// <see cref="WarderValues"/>, its wards <see cref="WardKeeper"/>, its rules <see cref="WarderRules"/>.
    ///
    /// It wakes, gives up and comes at her through the brain exactly as every kind does (its range, a hurt, its room's fight,
    /// work order 070), with no waking rule of its own. In the chase it walks to its spot: on the line from her through the
    /// middle of its allies, a little past them, 8 to 10 m from her (<see cref="WarderRules.Spot"/>, the shooter's and the
    /// caster's way of keeping a distance); it does not flee.
    ///
    /// Awake, alive and not dazed, it raises a ward on the nearest ally within <see cref="WarderValues.WardRange"/> that has
    /// none (never itself, another warder or the boss): for <see cref="WarderValues.RaiseSeconds"/> its body glows and a thread
    /// of light grows from it to the ally (heard as a wind-up); then the ward is on: a ring of light round the ally's body
    /// (its shell) and the thread between the two, seen the whole time. A warded enemy takes <see cref="WarderValues.WardShare"/>
    /// less of every hit (<see cref="Damageable.WardShare"/>: hers and a bomber's blast alike), and the shell flashes at each.
    /// At most <see cref="WarderValues.MostWards"/> at a time, one no sooner than <see cref="WarderValues.WardEvery"/> after the last.
    ///
    /// A ward breaks when the warder dies, when it is staggered (its stagger bar emptied) or stunned (every one of its wards),
    /// or when the warded is farther than <see cref="WarderValues.BreakRange"/> from it: the shell bursts outward and fades
    /// (the shell itself, kept for the next ward: pooled, no particle system), and the enemy that lost it is dazed for
    /// <see cref="WarderValues.BreakDaze"/> (the counter's daze, <see cref="Damageable.DazeFor"/>). A raising is cancelled by
    /// the same things, with no daze. A ward whose ally died is simply gone; so are its wards when it goes home.
    ///
    /// Alone (no ally fit for a ward left alive in its room) it throws the caster's bolt, the bone orb of things that fly, at
    /// half the caster's rate (<see cref="WarderValues.Bolt"/>): the caster's attack whole, with its telegraph (the wind-up's
    /// star and sound, and the caster's Cast clip on the caster's model it wears), its sight and its release.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyWarder : Enemy
    {
        /// <summary>The ward's light (work order 079): a clear green, far from the telegraph's red, the counter's blue, the daze's pale blue and the elite's gold.</summary>
        public static readonly Color WardColor = new Color(0.3f, 1f, 0.55f);
        const float ArriveSlack = 0.75f;    // m from its spot where it stops walking
        const float SpotSearch = 2f;        // m round its spot it looks for walkable ground

        static readonly List<EnemyWarder> s_Warders = new List<EnemyWarder>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarders() => s_Warders.Clear();

        /// <summary>One ward's look: its thread and its shell, made once and kept (pooled), and whom it is on.</summary>
        sealed class Slot
        {
            public LineRenderer Thread, Shell;
            public Enemy Ally;
            public Damageable Body;
            public int AllyId = -1;
            public bool On;
            public float StartedAt;
            public float FlashUntil = -1f;
            public int WardedAway;
            public bool Bursting;
            public float BurstAt;
            public Vector3 BurstCentre;
            public float BurstRadius;
            public Action<int> OnWarded;
            public bool InUse => AllyId >= 0;
        }

        public WarderValues Tuning = new WarderValues();

        readonly WardKeeper _keeper = new WardKeeper();
        readonly List<WardAlly> _allies = new List<WardAlly>(16);
        readonly List<Enemy> _allyEnemies = new List<Enemy>(16);
        readonly List<WardChange> _changes = new List<WardChange>(8);
        Slot[] _slots = new Slot[0];
        MaterialPropertyBlock _block;
        bool _alone = true;
        bool _backingOff;
        bool _glowShown;
        bool _broken; // its wards were broken by its death

        public override EnemyKind Kind => EnemyKind.Warder;
        public override EnemyValues Values => Tuning.Enemy;

        /// <summary>Its wards now, being raised and on.</summary>
        public WardKeeper Keeper => _keeper;
        /// <summary>Alone: no ally fit for a ward alive in its room. Then it throws its bolt.</summary>
        public bool IsAlone => _alone;
        /// <summary>Its stagger bar.</summary>
        public StaggerBar Stagger => _self != null ? _self.Stagger : null;
        /// <summary>Hits a shell of its flashed at.</summary>
        public int ShellFlashes { get; private set; }
        /// <summary>Shells of its that burst (wards broken while on, their ally alive).</summary>
        public int Bursts { get; private set; }
        /// <summary>Damage its wards took off hits, all told.</summary>
        public int WardedAway { get; private set; }
        /// <summary>Its body glows now (a ward is being raised).</summary>
        public bool GlowShown => _glowShown;

        /// <summary>Is this enemy warded now (by any warder)?</summary>
        public static bool IsWarded(Enemy e) => e != null && e.Body != null && e.Body.WardShare > 0f;

        /// <summary>Is a ward of this warder on this enemy (on, not being raised)?</summary>
        public bool Wards(Enemy e)
        {
            Slot s = SlotOf(e);
            return s != null && s.On;
        }

        /// <summary>Is a ward of this warder being raised on this enemy now?</summary>
        public bool Raises(Enemy e)
        {
            Slot s = SlotOf(e);
            return s != null && !s.On;
        }

        /// <summary>The thread between it and this enemy is drawn now.</summary>
        public bool ThreadShown(Enemy e)
        {
            Slot s = SlotOf(e);
            return s != null && s.Thread != null && s.Thread.enabled;
        }

        /// <summary>The shell round this enemy is drawn now.</summary>
        public bool ShellShown(Enemy e)
        {
            Slot s = SlotOf(e);
            return s != null && s.Shell != null && s.Shell.enabled;
        }

        /// <summary>The line renderers of its threads and shells, made once (for the tests' count of what is left behind).</summary>
        public int LinesMade => _slots.Length * 2;

        Slot SlotOf(Enemy e)
        {
            if (e == null) return null;
            foreach (Slot s in _slots) if (s.InUse && s.AllyId == e.Id) return s;
            return null;
        }

        protected override void Awake()
        {
            base.Awake();
            _self.Stagger = new StaggerBar(Tuning.Stagger);
            s_Warders.Add(this);
            // The telegraph the factory gave it is the look of its threads and shells: it draws nothing round itself (its bolt's
            // telegraph is the caster's, on its body).
            LineRenderer look = Telegraph;
            Telegraph = null;
            int most = Mathf.Max(0, Tuning.MostWards);
            _slots = new Slot[most];
            _block = new MaterialPropertyBlock();
            _block.SetColor("_BaseColor", WardColor);
            _block.SetColor("_Color", WardColor);
            for (int i = 0; i < most; i++)
            {
                var s = new Slot();
                if (look != null)
                {
                    s.Thread = Line(look, "WardThread_" + i, false);
                    s.Shell = Line(look, "WardShell_" + i, true);
                }
                s.OnWarded = amount =>
                {
                    s.FlashUntil = Time.time + Tuning.ShellHitSeconds;
                    s.WardedAway += amount;
                    WardedAway += amount;
                    ShellFlashes++;
                };
                _slots[i] = s;
            }
            if (look != null) Destroy(look.gameObject);
            // Its model is the caster's, tinted (work order 079): the tint is its body's colour, which the daze and the glow go back to.
            if (Look != null && BodyRenderer != null)
            {
                BodyColor = EnemyFactory.WarderTint;
                EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
            }
        }

        LineRenderer Line(LineRenderer look, string name, bool loop)
        {
            GameObject go = Instantiate(look.gameObject, transform, false);
            go.name = name;
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.loop = loop;
            line.useWorldSpace = true;
            line.positionCount = loop ? line.positionCount : 2;
            line.widthMultiplier = loop ? Tuning.ShellWidth : Tuning.ThreadWidth;
            line.SetPropertyBlock(_block);
            line.enabled = false;
            return line;
        }

        protected override void OnDestroy()
        {
            // Taken away: its wards go with it, the warded no longer lessened, and nothing listens to them any more.
            if (_keeper.Wards.Count > 0) EndAll(WardEnd.WentHome, Time.time);
            foreach (Slot s in _slots) Unbind(s);
            s_Warders.Remove(this);
            base.OnDestroy();
        }

        protected override void Update()
        {
            base.Update();
            float now = Time.time;
            StepWards(now);
            DrawWards(now);
        }

        // ---------------- Its wards ----------------

        void StepWards(float now)
        {
            if (IsDead)
            {
                if (!_broken && _keeper.Wards.Count > 0) EndAll(WardEnd.WarderKilled, now);
                _broken = true;
                return;
            }
            _broken = false;
            if (IsDazed)
            {
                if (_keeper.Wards.Count > 0) EndAll(WardEnd.WarderStaggered, now);
                return;
            }
            Gather();
            _alone = WarderRules.Alone(_allies);
            if (State == EnemyState.Idle)
            {
                if (_keeper.Wards.Count > 0) EndAll(WardEnd.WentHome, now);
                return;
            }
            _changes.Clear();
            _keeper.Step(now, Ground(transform.position), _allies, Tuning, _changes);
            Apply(now);
        }

        /// <summary>Staggered or stunned (its bar emptied, a counter's daze): every ward of its breaks at once.</summary>
        protected override void OnDaze()
        {
            if (_keeper.Wards.Count > 0) EndAll(WardEnd.WarderStaggered, Time.time);
        }

        /// <summary>Its allies as it sees them: the enemies of its room (its fight's; in the practice room, those within its leash).</summary>
        void Gather()
        {
            _allies.Clear();
            _allyEnemies.Clear();
            IReadOnlyList<Enemy> all = All;
            Vector3 me = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || e == this || e.Body == null || e.Fight != Fight) continue;
                if (Fight == null && Flat(e.transform.position - me).magnitude > Values.LeashRange) continue;
                _allies.Add(new WardAlly
                {
                    Id = e.Id, At = Ground(e.transform.position), Alive = !e.IsDead,
                    NeverWarded = WarderRules.NeverWarded(e.Kind), WardedByAnother = WardedByAnother(e.Id),
                });
                _allyEnemies.Add(e);
            }
        }

        bool WardedByAnother(int id)
        {
            for (int i = 0; i < s_Warders.Count; i++)
            {
                EnemyWarder w = s_Warders[i];
                if (w != null && w != this && w._keeper.Holds(id)) return true;
            }
            return false;
        }

        Enemy AllyOf(int id)
        {
            for (int i = 0; i < _allyEnemies.Count; i++)
                if (_allyEnemies[i] != null && _allyEnemies[i].Id == id) return _allyEnemies[i];
            return null;
        }

        void EndAll(WardEnd why, float now)
        {
            _changes.Clear();
            _keeper.BreakAll(why, _changes);
            Apply(now);
        }

        void Apply(float now)
        {
            for (int i = 0; i < _changes.Count; i++)
            {
                WardChange c = _changes[i];
                switch (c.Kind)
                {
                    case WardChangeKind.Raising:
                    {
                        Slot s = FreeSlot();
                        if (s == null) break;
                        s.AllyId = c.AllyId;
                        s.Ally = AllyOf(c.AllyId);
                        s.Body = s.Ally != null ? s.Ally.Body : null;
                        s.On = false;
                        s.StartedAt = now;
                        s.WardedAway = 0;
                        s.Bursting = false;
                        Report(EnemyReportKind.WardRaising);
                        break;
                    }
                    case WardChangeKind.On:
                    {
                        Slot s = Find(c.AllyId);
                        if (s == null || s.Body == null) break;
                        s.On = true;
                        s.Body.WardShare = Tuning.WardShare;
                        s.Body.Warded += s.OnWarded;
                        Report(EnemyReportKind.WardRaised);
                        break;
                    }
                    case WardChangeKind.Broken:
                    case WardChangeKind.Cancelled:
                    {
                        Slot s = Find(c.AllyId);
                        if (s == null) break;
                        bool wasOn = s.On;
                        bool allyAlive = s.Ally != null && !s.Ally.IsDead;
                        int warded = s.WardedAway;
                        if (wasOn && allyAlive && c.Why != WardEnd.WentHome)
                        {
                            // The shell bursts outward from where it was, and fades.
                            s.Bursting = true;
                            s.BurstAt = now;
                            s.BurstCentre = Flat(s.Ally.transform.position);
                            s.BurstRadius = ShellRadius(s);
                            Bursts++;
                        }
                        if (c.Dazes && allyAlive && s.Body != null) s.Body.DazeFor(Tuning.BreakDaze); // the counter's daze
                        Unbind(s);
                        if (wasOn) Report(EnemyReportKind.WardBroken, damage: warded, count: (int)c.Why);
                        break;
                    }
                }
            }
            _changes.Clear();
        }

        Slot FreeSlot()
        {
            foreach (Slot s in _slots) if (!s.InUse) return s;
            return null;
        }

        Slot Find(int allyId)
        {
            foreach (Slot s in _slots) if (s.InUse && s.AllyId == allyId) return s;
            return null;
        }

        /// <summary>The slot lets its ally go: no ward share on it any more, and its hits no longer heard.</summary>
        static void Unbind(Slot s)
        {
            if (!ReferenceEquals(s.Body, null))
            {
                if (s.On) s.Body.WardShare = 0f;
                s.Body.Warded -= s.OnWarded;
            }
            s.Body = null;
            s.Ally = null;
            s.AllyId = -1;
            s.On = false;
            if (s.Thread != null && s.Thread.enabled) s.Thread.enabled = false;
            if (!s.Bursting && s.Shell != null && s.Shell.enabled) s.Shell.enabled = false;
        }

        float ShellRadius(Slot s) =>
            (s.Body != null ? s.Body.Radius : 0.4f) + Tuning.ShellGap;

        // ---------------- What she sees ----------------

        void DrawWards(float now)
        {
            bool raising = false;
            Vector3 from = Flat(transform.position) + Vector3.up * Tuning.ThreadHeight;
            foreach (Slot s in _slots)
            {
                if (s.InUse && s.Ally != null)
                {
                    Vector3 at = Flat(s.Ally.transform.position);
                    Vector3 to = at + Vector3.up * Tuning.ThreadHeight;
                    if (s.Thread != null)
                    {
                        if (!s.Thread.enabled) s.Thread.enabled = true;
                        float p = s.On ? 1f : Mathf.Clamp01((now - s.StartedAt) / Mathf.Max(1e-4f, Tuning.RaiseSeconds));
                        s.Thread.SetPosition(0, from);
                        s.Thread.SetPosition(1, Vector3.Lerp(from, to, p));
                    }
                    if (!s.On) raising = true;
                    if (s.Shell != null)
                    {
                        if (s.On)
                        {
                            s.Bursting = false;
                            if (!s.Shell.enabled) s.Shell.enabled = true;
                            GroundOutline.Circle(s.Shell, at, ShellRadius(s), Tuning.ShellHeight);
                            s.Shell.widthMultiplier = now < s.FlashUntil ? Tuning.ShellHitWidth : Tuning.ShellWidth;
                        }
                        else if (!s.Bursting && s.Shell.enabled) s.Shell.enabled = false;
                    }
                }
                if (s.Bursting && s.Shell != null && !s.On)
                {
                    float k = Tuning.BurstSeconds > 0f ? (now - s.BurstAt) / Tuning.BurstSeconds : 1f;
                    if (k >= 1f)
                    {
                        s.Bursting = false;
                        if (s.Shell.enabled) s.Shell.enabled = false;
                    }
                    else
                    {
                        if (!s.Shell.enabled) s.Shell.enabled = true;
                        GroundOutline.Circle(s.Shell, s.BurstCentre, s.BurstRadius * (1f + (Tuning.BurstGrow - 1f) * k), Tuning.ShellHeight);
                        s.Shell.widthMultiplier = Mathf.Lerp(Tuning.ShellHitWidth, 0f, k);
                    }
                }
            }
            // Its body glows while it raises a ward, on and off every GlowPulse.
            bool glow = raising && !IsDead && Tuning.GlowPulse > 0f && Mathf.FloorToInt(now / Tuning.GlowPulse) % 2 == 0;
            if (glow != _glowShown)
            {
                _glowShown = glow;
                if (BodyRenderer != null) EnemyFactory.Paint(BodyRenderer.sharedMaterial, glow ? WardColor : BodyColor);
            }
        }

        /// <summary>Draws its wards as they are now, as every frame does: for the test of what a frame allocates.</summary>
        public void DrawWardsNow() => DrawWards(Time.time);

        // ---------------- Where it stands, and its bolt alone ----------------

        protected override bool MayAttack(Vector3 toPlayer, float distance)
        {
            if (distance < Tuning.BackOffUnder) _backingOff = true;
            if (distance >= Tuning.KeepNear) _backingOff = false;
            return _alone && !_backingOff && SeesHer();
        }

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            // Its spot: behind the middle of its allies, on the line from her through them, 8 to 10 m from her.
            float x = 0f, z = 0f;
            int n = 0;
            for (int i = 0; i < _allies.Count; i++)
            {
                if (!_allies[i].Alive || _allies[i].NeverWarded) continue;
                x += _allies[i].At.X;
                z += _allies[i].At.Z;
                n++;
            }
            GroundPoint middle = n > 0 ? new GroundPoint(x / n, z / n) : Ground(transform.position);
            GroundPoint spot = WarderRules.Spot(Player.GroundPosition, Ground(transform.position), n > 0, middle, Tuning);
            Vector3 to = new Vector3(spot.X, transform.position.y, spot.Z);
            if (NavMesh.SamplePosition(to, out NavMeshHit hit, SpotSearch, NavMesh.AllAreas)) to = hit.position;
            if (Flat(to - transform.position).magnitude <= ArriveSlack) Halt();
            else Walk(to);
        }

        /// <summary>Does it see her: no wall or tall prop between at its sight height, and not covered by what she is in cover against (the caster's sight)?</summary>
        public bool SeesHer()
        {
            if (Player == null) return false;
            Vector3 from = Flat(transform.position), dir = Flat(Player.Position) - from;
            float length = dir.magnitude;
            bool clear = length < 1e-3f || Cover.ClearDistance(from, dir / length, length, Tuning.SightHeight) >= length - 1e-3f;
            return clear && !Player.ShieldsFrom(transform.position, ProjectileKind.Orb);
        }

        /// <summary>Its bolt leaves its hand at the release (the caster's orb, aimed at where she is then).</summary>
        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (Player == null) return true;
            GroundPoint me = Ground(transform.position);
            GroundPoint dir = Player.GroundPosition - me;
            if (dir.Length < 1e-3f) dir = new GroundPoint(transform.forward.x, transform.forward.z);
            dir = dir * (1f / Mathf.Max(1e-6f, dir.Length));
            Face(new Vector3(dir.X, 0f, dir.Z));
            GroundPoint hand = me + dir * (Tuning.BodyRadius * transform.lossyScale.x + 0.3f);
            ThingsThatFly.Ensure().Fire(this, attack, ProjectileRules.Orb(Tuning.Orbs, hand, dir));
            ThingsThatFly.TellLoosed(this, attack, 1);
            return true;
        }
    }
}
