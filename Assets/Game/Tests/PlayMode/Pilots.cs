using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike.PlayModeTests
{
    public enum PilotKind { Turret, Kiter, Ace }

    /// <summary>
    /// A pilot (work order 022, 2026-11-20): code that plays a run through the intents, well enough to give a lower and
    /// an upper bound on how a run goes. It decides once a frame, on game time (<see cref="PilotBody.Now"/>), from a
    /// <see cref="PilotView"/>, and acts only through its <see cref="PilotBody"/>. It changes no time, health or value.
    ///
    /// What all three share:
    /// - The way through a floor: the rooms from the entrance to the exit by the level's doors (the fewest doors), and a
    ///   treasure room one door off that way, for its chest; each room in turn is walked to by the NavMesh, cleared of
    ///   its enemies, and left. A chest within reach is opened (<see cref="WantsChest"/>; the harness presses E for it and
    ///   takes the first upgrade), and the first skill of a floor reward is taken (the harness picks it).
    /// - Its foes: the living enemies in the room she is in, and the awake ones on screen. With foes it fights, else it
    ///   walks on. A room's enemies that sleep are walked up to until one wakes.
    /// - The kit: the target is the nearest foe she can see (no wall between), else the nearest. Every skill on a key is
    ///   used when it is ready and the target is in its reach: a spender when the target has three marks or more, else a
    ///   builder, in key order; a charge skill let go at full charge, a held skill held to its end, a recast pressed as
    ///   soon as its window opens; the basic shot clicked whenever no click waits and the target is in its range.
    ///   Slide is a move, not a shot: only the Kiter and the Ace use it, to leave a telegraph.
    /// The kinds change how it moves and when it presses (<see cref="Turret"/>, <see cref="Kiter"/>, <see cref="Ace"/>).
    /// </summary>
    public abstract class Pilot
    {
        public const int SpendAt = 3;          // marks on the target before a spender is used (work order 022)
        protected const float PathEvery = 0.4f; // s between new NavMesh paths to the same goal

        protected readonly PilotBody Me;
        public abstract PilotKind Kind { get; }
        /// <summary>What it is doing, in a few words, for the harness's stuck report.</summary>
        public string Mode { get; protected set; } = "start";
        /// <summary>Where it is walking to, when it walks somewhere; for the harness's progress check.</summary>
        public Vector3 Goal { get; protected set; }
        public bool HasGoal { get; protected set; }
        /// <summary>A chest is within reach and unopened: the harness presses the interact key.</summary>
        public bool WantsChest { get; private set; }

        int _mapVersion = -1;
        readonly List<int> _route = new List<int>();
        readonly HashSet<int> _done = new HashSet<int>();
        int _routeAt;
        readonly NavMeshPath _path = new NavMeshPath();
        Vector3 _pathGoal;
        float _pathAt = float.NegativeInfinity;
        bool _pathOk;
        int _corner;
        int _charging = -1; // the Charge skill it holds, by slot
        protected Vector3 LastDir = Vector3.forward;

        protected Pilot(PilotBody me) { Me = me; }

        /// <summary>One decision, once a frame.</summary>
        public void Tick(PilotView view)
        {
            WantsChest = false;
            if (!Me.Alive)
            {
                Me.Stop();
                return;
            }
            if (view.MapVersion != _mapVersion) NewMap(view);
            List<EnemySeen> foes = Foes(view);
            if (foes.Count > 0)
            {
                EnemySeen target = Target(foes);
                Engage(view, foes, target);
                Move(view, foes, target);
                return;
            }
            Travel(view);
        }

        // ---------------- Foes and the target ----------------

        static List<EnemySeen> Foes(PilotView view)
        {
            var foes = new List<EnemySeen>();
            foreach (EnemySeen e in view.Enemies)
            {
                // Its own room's enemies, wherever they stand now (one that chased her out and went home still counts).
                bool here = view.HasMap ? view.HereRoom >= 0 && (e.Room == view.HereRoom || e.HomeRoom == view.HereRoom) : true;
                if (here || e.Awake) foes.Add(e);
            }
            return foes;
        }

        static EnemySeen Target(List<EnemySeen> foes)
        {
            EnemySeen best = foes[0];
            float bestScore = float.MaxValue;
            foreach (EnemySeen e in foes)
            {
                float score = e.Distance + (e.Visible ? 0f : 1000f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        protected static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        protected static Vector3 FlatDir(Vector3 from, Vector3 to)
        {
            Vector3 d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }
        protected static GroundPoint G(Vector3 v) => new GroundPoint(v.x, v.z);

        // ---------------- The kit ----------------

        protected virtual bool UsesFocus => false;

        /// <summary>The Charge skill in hand is let go now: at full charge, unless a kind says otherwise.</summary>
        protected virtual bool ReleaseCharge(PilotSkill skill) => Me.SkillChargedSeconds >= skill.ChargeTime;

        /// <summary>The next press of a recast chain whose window is open goes now: at once, unless a kind says otherwise.</summary>
        protected virtual bool RecastNow(PilotSkill skill) => true;

        /// <summary>May this skill be started now on this target? Every skill, unless a kind keeps one back.</summary>
        protected virtual bool MayStart(PilotSkill skill, List<EnemySeen> foes, EnemySeen target) => true;

        /// <summary>A shot or skill it must fire now, before the rest (the Ace's counter and keg); false when none.</summary>
        protected virtual bool Special(PilotView view, List<EnemySeen> foes, EnemySeen target) => false;

        static bool InReach(PilotSkill s, EnemySeen target)
        {
            float d = target.Distance;
            switch (s.Shape)
            {
                case SkillShape.Circle: return d <= s.Range + s.Radius * 0.5f;
                case SkillShape.Cone: return d <= s.Range + target.BodyRadius;
                default: return d <= s.Range + target.BodyRadius;
            }
        }

        void Engage(PilotView view, List<EnemySeen> foes, EnemySeen target)
        {
            Vector3 aim = target.Position;
            Me.AimAt(aim);

            // The skill in hand.
            int active = Me.ActiveSkillSlot;
            if (_charging >= 0 && active != _charging) _charging = -1;
            if (active >= 0 && Me.TrySlot(active, out PilotSkill inHand) && inHand.Input == SkillInput.Charge && ReleaseCharge(inHand))
            {
                Me.ReleaseSkill(active);
                _charging = -1;
            }

            if (UsesFocus && Me.IsFocusFull) Me.ActivateIdentity();

            // A recast chain waits for its next press.
            int chain = Me.RecastSlot;
            if (chain >= 0 && Me.IsRecastWindowOpen && Me.TrySlot(chain, out PilotSkill recast) && target.Visible && RecastNow(recast))
            {
                Me.PressSkill(chain, aim);
                Me.ReleaseSkill(chain);
            }

            if (Special(view, foes, target)) return;

            // A new skill.
            if (!Me.IsLocked && Me.SkillGateRemaining <= 0f && Me.RecastSlot < 0 && target.Visible)
            {
                PilotSkill? pick = null;
                List<PilotSkill> keys = Me.KeySkills();
                if (target.Marks >= SpendAt)
                    foreach (PilotSkill s in keys)
                        if (s.Spends && Ready(s) && InReach(s, target) && MayStart(s, foes, target)) { pick = s; break; }
                if (pick == null)
                    foreach (PilotSkill s in keys)
                        if (!s.Spends && s.Input != SkillInput.KeyMove && Ready(s) && InReach(s, target) && MayStart(s, foes, target)) { pick = s; break; }
                if (pick != null) Use(pick.Value, aim);
            }

            // The basic shot.
            if (target.Visible && target.Distance <= Me.BasicShotRange + target.BodyRadius && !Me.HasPendingShot && Me.ActiveSkillSlot < 0)
                Me.Shoot(aim);
        }

        protected bool Ready(PilotSkill s) => Me.ChargesReady(s.Slot) > 0;

        /// <summary>Presses a skill: let go at once, but a Charge skill (let go later) and a Holding one (held to its end).</summary>
        protected void Use(PilotSkill s, Vector3 aim)
        {
            IntentResult r = Me.PressSkill(s.Slot, aim);
            if (r == IntentResult.Rejected) return;
            if (s.Input == SkillInput.Charge) _charging = s.Slot;
            else if (s.Input != SkillInput.Holding) Me.ReleaseSkill(s.Slot);
        }

        protected bool TryKeySkill(System.Predicate<PilotSkill> which, out PilotSkill skill)
        {
            foreach (PilotSkill s in Me.KeySkills())
                if (which(s)) { skill = s; return true; }
            skill = default;
            return false;
        }

        // ---------------- Moving ----------------

        /// <summary>How it moves in a fight.</summary>
        protected abstract void Move(PilotView view, List<EnemySeen> foes, EnemySeen target);

        protected static bool AnyAwake(List<EnemySeen> foes)
        {
            foreach (EnemySeen e in foes)
                if (e.Awake) return true;
            return false;
        }

        /// <summary>Walks toward a point by the NavMesh: a new path now and then, steered toward its next corner.</summary>
        protected void WalkTo(Vector3 goal, string mode)
        {
            Mode = mode;
            Goal = goal;
            HasGoal = true;
            Vector3 here = Me.Position;
            float now = Me.Now;
            if (now - _pathAt >= PathEvery || Flat(goal, _pathGoal) > 1f)
            {
                _pathAt = now;
                _pathGoal = goal;
                _corner = 1;
                Vector3 from = NavMesh.SamplePosition(here, out NavMeshHit a, 1.5f, NavMesh.AllAreas) ? a.position : here;
                Vector3 to = NavMesh.SamplePosition(goal, out NavMeshHit b, 3f, NavMesh.AllAreas) ? b.position : goal;
                _pathOk = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path) && _path.corners.Length >= 2;
            }
            Vector3 next = goal;
            if (_pathOk)
            {
                Vector3[] corners = _path.corners;
                while (_corner < corners.Length - 1 && Flat(corners[_corner], here) < 0.4f) _corner++;
                next = corners[Mathf.Min(_corner, corners.Length - 1)];
            }
            Vector3 dir = FlatDir(here, next);
            if (dir == Vector3.zero) Me.Stop();
            else
            {
                LastDir = dir;
                Me.Steer(dir);
            }
        }

        // ---------------- The way through the floor ----------------

        void NewMap(PilotView view)
        {
            _mapVersion = view.MapVersion;
            _route.Clear();
            _done.Clear();
            _routeAt = 0;
            if (!view.HasMap || view.Rooms.Count == 0) return;
            int start = -1, exit = -1;
            foreach (RoomSeen r in view.Rooms)
            {
                if (r.Kind == RoomKind.Entrance && start < 0) start = r.Order;
                if (r.Kind == RoomKind.Exit) exit = r.Order;
            }
            if (start < 0) start = view.Rooms[0].Order;
            // The fewest doors from the entrance to the exit.
            var before = new Dictionary<int, int> { [start] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                foreach (DoorSeen d in view.Doors)
                {
                    if (d.A != r && d.B != r) continue;
                    int o = d.A == r ? d.B : d.A;
                    if (before.ContainsKey(o)) continue;
                    before[o] = r;
                    queue.Enqueue(o);
                }
            }
            var chain = new List<int>();
            if (exit >= 0 && before.ContainsKey(exit))
                for (int r = exit; r >= 0; r = before[r]) chain.Insert(0, r);
            else chain.Add(start);
            // A treasure room one door off the way goes in after its neighbour, and back.
            foreach (int r in chain)
            {
                _route.Add(r);
                foreach (DoorSeen d in view.Doors)
                {
                    if (d.A != r && d.B != r) continue;
                    int o = d.A == r ? d.B : d.A;
                    if (chain.Contains(o) || _route.Contains(o)) continue;
                    if (view.TryRoom(o, out RoomSeen side) && side.Kind == RoomKind.Treasure)
                    {
                        _route.Add(o);
                        _route.Add(r);
                    }
                }
            }
        }

        bool ChestOf(PilotView view, int room, out ChestSeen chest)
        {
            foreach (ChestSeen c in view.Chests)
                if (c.Room == room) { chest = c; return true; }
            chest = default;
            return false;
        }

        void Travel(PilotView view)
        {
            if (!view.HasMap || _route.Count == 0)
            {
                Mode = "nothing to do";
                HasGoal = false;
                Me.Stop();
                return;
            }
            // The room she is in is done when nothing of it lives (it is not a foe now) and its chest, if any, is open.
            int here = view.HereRoom;
            if (here >= 0 && (!ChestOf(view, here, out ChestSeen hc) || hc.Opened)) _done.Add(here);
            // A room met again later in the way (back out of a treasure room) is done already and passed at once.
            while (_routeAt < _route.Count - 1 && _done.Contains(_route[_routeAt])) _routeAt++;
            int room = _route[_routeAt];
            if (view.TryRoom(room, out RoomSeen r) && r.Kind == RoomKind.Treasure && ChestOf(view, room, out ChestSeen chest) && !chest.Opened)
            {
                if (Flat(Me.Position, chest.Position) <= view.ChestReach - 0.2f)
                {
                    WantsChest = true;
                    Me.Stop();
                    Mode = "opening the chest of room " + (room + 1);
                    return;
                }
                WalkTo(chest.Position, "to the chest of room " + (room + 1));
                return;
            }
            if (view.TryRoom(room, out RoomSeen target))
            {
                if (here == room && _done.Contains(room) && _routeAt >= _route.Count - 1)
                {
                    Mode = "at the end of the way";
                    Me.Stop();
                    return;
                }
                WalkTo(target.Middle, "to room " + (room + 1) + " (" + target.Kind.ToString().ToLowerInvariant() + ")");
            }
        }

        /// <summary>The rooms of the way through the floor, in order: for tests.</summary>
        public IReadOnlyList<int> Route => _route;
    }

    /// <summary>
    /// The Turret (work order 022), the lower bound: it walks into the room and, once anything there is awake, stands
    /// still and fights with the kit as <see cref="Pilot"/> says. It never dashes and never steers in a fight while
    /// anything is awake; with nothing awake it walks to the nearest sleeping enemy of the room. Backstep, being a skill
    /// on its key, still hops it back.
    /// </summary>
    public sealed class Turret : Pilot
    {
        public Turret(PilotBody me) : base(me) { }
        public override PilotKind Kind => PilotKind.Turret;

        protected override void Move(PilotView view, List<EnemySeen> foes, EnemySeen target)
        {
            if (AnyAwake(foes))
            {
                Mode = "standing and fighting";
                HasGoal = false;
                Me.Stop();
                return;
            }
            WalkTo(target.Position, "to the nearest sleeping enemy");
        }
    }

    /// <summary>
    /// The Kiter (work order 022), a competent player: the Turret's use of the kit, and it moves. In this order, once a
    /// frame:
    /// - In a telegraph (or the boss's charge line): to the nearest point outside every one, walking when it gets there
    ///   before the strike, else the dash when it is ready, else Slide when it has it, else walking anyway.
    /// - A spender ready and the target at three marks or more but out of its reach: it walks in.
    /// - The nearest melee enemy nearer than <see cref="KeepFrom"/> less a metre: it backs away, along the best of
    ///   sixteen ways a few metres ahead on the NavMesh (away from the melee enemies, away from walls, inside the room).
    /// - A shooter winding up at her: it steps sideways to its line.
    /// - The target out of the basic shot's range or not seen: it walks toward it.
    /// - Else it stands and shoots.
    /// </summary>
    public class Kiter : Pilot
    {
        public const float KeepFrom = 7f;
        const float HazardMargin = 0.35f;
        const float Probe = 3f;

        public Kiter(PilotBody me) : base(me) { }
        public override PilotKind Kind => PilotKind.Kiter;

        /// <summary>Is a point inside a telegraph or the charge's line? The soonest strike among those it is in.</summary>
        protected static bool InHazard(List<EnemySeen> foes, Vector3 p, out float soonest)
        {
            soonest = float.MaxValue;
            bool any = false;
            float r = Placeholders.PlayerRadius + HazardMargin;
            foreach (EnemySeen e in foes)
            {
                if (e.Winding)
                {
                    bool hit;
                    switch (e.Shape)
                    {
                        case EnemyAttackShape.Circle: hit = SkillRules.InCircle(G(e.Position), e.Radius, G(p), r); break;
                        case EnemyAttackShape.Cone: hit = SkillRules.InCone(G(e.Position), G(e.AimPoint), e.Reach, e.Angle, G(p), r); break;
                        default: hit = LineShot.Hits(G(e.Position), G(e.AimPoint), e.Reach, e.HalfWidth, G(p), r); break;
                    }
                    if (hit)
                    {
                        any = true;
                        soonest = Mathf.Min(soonest, e.WindupLeft);
                    }
                }
                if (e.Charging)
                {
                    Vector3 end = e.ChargeFrom + e.ChargeDirection * e.ChargeReach;
                    if (LineShot.Hits(G(e.Position), G(end), Flat(e.Position, end) + 0.01f, e.ChargeHalfWidth, G(p), r))
                    {
                        any = true;
                        soonest = Mathf.Min(soonest, Flat(e.Position, p) / 12f);
                    }
                }
            }
            return any;
        }

        static bool Clear(Vector3 from, Vector3 to) => !NavMesh.Raycast(from, to, out NavMeshHit _, NavMesh.AllAreas);

        /// <summary>The nearest point outside every telegraph that can be walked to in a straight line; false if none.</summary>
        protected static bool SafePoint(List<EnemySeen> foes, Vector3 here, out Vector3 safe)
        {
            safe = here;
            float best = float.MaxValue;
            for (float radius = 0.75f; radius <= 7.01f; radius += 0.5f)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                    Vector3 p = here + dir * radius;
                    if (InHazard(foes, p, out float _)) continue;
                    if (!Clear(here, p)) continue;
                    float cost = radius;
                    foreach (EnemySeen e in foes)
                        if (e.Melee && Flat(e.Position, p) < 2.5f) cost += 2f; // not into the arms of another
                    if (cost < best)
                    {
                        best = cost;
                        safe = p;
                    }
                }
                if (best < radius) break; // nothing farther can be nearer
            }
            return best < float.MaxValue;
        }

        protected virtual bool UsesSlide => true;

        protected override void Move(PilotView view, List<EnemySeen> foes, EnemySeen target)
        {
            Vector3 here = Me.Position;
            bool free = !Me.IsDashing && (!Me.IsLocked || Me.InLightSkill);

            // 1. Out of any telegraph before the strike.
            if (InHazard(foes, here, out float soonest) && SafePoint(foes, here, out Vector3 safe))
            {
                HasGoal = false;
                Vector3 dir = FlatDir(here, safe);
                float need = Flat(here, safe) / Mathf.Max(0.1f, Me.RunSpeed) + 0.05f;
                if ((!free || need > soonest) && Me.DashCooldownRemaining <= 0f && !Me.IsDashing)
                {
                    Mode = "dashing out of a telegraph";
                    Me.DashToward(here + dir * Me.DashDistance);
                    return;
                }
                if (!free) { Mode = "in a telegraph, locked"; return; }
                Mode = "stepping out of a telegraph";
                LastDir = dir;
                Me.Steer(dir);
                if (need > soonest && UsesSlide && TryKeySkill(s => s.Input == SkillInput.KeyMove && Ready(s), out PilotSkill slide))
                {
                    Me.PressSkill(slide.Slot, here + dir * 6f);
                    Me.ReleaseSkill(slide.Slot);
                }
                return;
            }
            if (!free) return;

            // The nearest melee enemy she can see: one behind a wall is walked round to, not backed away from (a Kiter
            // that backed from it and walked to it in turn stood still at the wall; work order 022's first flights).
            EnemySeen? melee = null;
            foreach (EnemySeen e in foes)
                if (e.Melee && e.Visible && (melee == null || e.Distance < melee.Value.Distance)) melee = e;

            // 2. In for a spender.
            if (target.Visible && target.Marks >= SpendAt
                && TryKeySkill(s => s.Spends && Ready(s), out PilotSkill spender) && target.Distance > spender.Range * 0.85f)
            {
                Mode = "walking in for " + spender.Name;
                HasGoal = false;
                Vector3 dir = FlatDir(here, target.Position);
                LastDir = dir;
                Me.Steer(dir);
                return;
            }

            // 3. Away from the nearest melee enemy.
            if (melee != null && melee.Value.Distance < KeepFrom - 1f)
            {
                Mode = "backing away";
                HasGoal = false;
                Vector3 dir = BestWay(view, foes, here, Vector3.zero);
                if (dir != Vector3.zero)
                {
                    LastDir = dir;
                    Me.Steer(dir);
                    return;
                }
            }

            // 4. Sideways while a shooter aims at her.
            foreach (EnemySeen e in foes)
            {
                if (e.Kind != EnemyKind.Shooter || !e.Winding) continue;
                Vector3 across = Vector3.Cross(Vector3.up, FlatDir(e.Position, here));
                Vector3 dir = BestWay(view, foes, here, across);
                if (Vector3.Dot(dir, across) < 0f) across = -across;
                Mode = "sideways to a shooter's aim";
                HasGoal = false;
                LastDir = across;
                Me.Steer(Vector3.Dot(dir, across) > 0.3f ? dir : across);
                return;
            }

            // 5. Toward a target it cannot reach or see.
            if (!target.Visible || target.Distance > Me.BasicShotRange - 1f)
            {
                if (melee == null || melee.Value.Distance > KeepFrom + 1f || !target.Visible)
                {
                    WalkTo(target.Position, "toward the target");
                    return;
                }
            }

            Mode = "standing and shooting";
            HasGoal = false;
            Me.Stop();
        }

        /// <summary>
        /// The best of sixteen ways a few metres ahead on the NavMesh: away from the melee enemies, away from the walls,
        /// inside the room she is in, a little along <paramref name="lean"/> and the way she went last. Zero when none is
        /// open.
        /// </summary>
        protected Vector3 BestWay(PilotView view, List<EnemySeen> foes, Vector3 here, Vector3 lean)
        {
            RoomSeen r = default;
            bool inRoom = view.HasMap && view.TryRoom(view.HereRoom, out r);
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Vector3 to = here + dir * Probe;
                float clear = Probe;
                if (NavMesh.Raycast(here, to, out NavMeshHit hit, NavMesh.AllAreas)) clear = Flat(here, hit.position);
                if (clear < 1f) continue;
                Vector3 p = here + dir * clear;
                float score = 0f;
                foreach (EnemySeen e in foes)
                {
                    if (!e.Melee || e.Distance > 14f) continue;
                    score += (Flat(p, e.Position) - e.Distance) * 2f / Mathf.Max(1f, e.Distance);
                }
                float edge = NavMesh.FindClosestEdge(p, out NavMeshHit edgeHit, NavMesh.AllAreas) ? edgeHit.distance : 3f;
                score += 0.6f * Mathf.Min(edge, 2.5f);
                score += clear * 0.3f;
                if (inRoom && !r.Contains(p, 0.5f)) score -= 3f;
                score += 0.5f * Vector3.Dot(dir, lean) + 0.3f * Vector3.Dot(dir, LastDir);
                if (InHazard(foes, p, out float _)) score -= 5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = dir;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// The Ace (work order 022), the upper bound: the Kiter, playing the timings right. Triple Tap's and Backstep's last
    /// press halfway into the clean part of the window (so Backstep's shot spends); Long Shot let go in the
    /// middle of its perfect band; Stopping Shot kept for a blue wind-up from the front while a kind with one is among the
    /// foes, and used then; focus spent the moment it is full; a keg thrown and then shot (a basic shot at the keg while
    /// an enemy stands within its burst).
    /// </summary>
    public sealed class Ace : Kiter
    {
        public Ace(PilotBody me) : base(me) { }
        public override PilotKind Kind => PilotKind.Ace;

        protected override bool UsesFocus => true;

        protected override bool ReleaseCharge(PilotSkill skill) =>
            skill.HasPerfectBand ? Me.SkillChargedSeconds >= (skill.PerfectFrom + skill.PerfectTo) * 0.5f : base.ReleaseCharge(skill);

        protected override bool RecastNow(PilotSkill skill)
        {
            float clean = Me.RecastCleanShare;
            return clean <= 0f || Me.RecastSinceOpened >= (Me.RecastCleanFrom + Me.RecastCleanTo) * 0.5f; // halfway into the clean part, wherever it lies (work order 040)
        }

        protected override bool MayStart(PilotSkill skill, List<EnemySeen> foes, EnemySeen target)
        {
            if (skill.CounterDaze <= 0f) return true;
            foreach (EnemySeen e in foes)
                if (e.HasCounterable) return false; // kept for the counter
            return true;
        }

        protected override bool Special(PilotView view, List<EnemySeen> foes, EnemySeen target)
        {
            Vector3 here = Me.Position;
            // The counter: a blue wind-up, she in its front, in reach.
            if (!Me.IsLocked && Me.SkillGateRemaining <= 0f && Me.RecastSlot < 0
                && TryKeySkill(s => s.CounterDaze > 0f && Ready(s), out PilotSkill stop))
            {
                foreach (EnemySeen e in foes)
                {
                    if (!e.Winding || !e.Counterable || !e.Visible || e.Distance > stop.Range + e.BodyRadius) continue;
                    if (!PoolRules.InFront(G(e.Position), G(e.Forward), G(here), stop.CounterHalfAngle)) continue;
                    Me.AimAt(e.Position);
                    Use(stop, e.Position);
                    return true;
                }
            }
            // The keg: shot while an enemy stands in its burst.
            if (Me.KegsLying > 0 && !Me.HasPendingShot && Me.ActiveSkillSlot < 0)
            {
                Vector3 keg = Me.LastKegPoint;
                if (Flat(here, keg) <= Me.BasicShotRange && TryKeySkill(s => s.Fuse > 0f, out PilotSkill kegSkill))
                {
                    foreach (EnemySeen e in foes)
                    {
                        if (Flat(e.Position, keg) > kegSkill.Radius) continue;
                        Me.AimAt(keg);
                        Me.Shoot(keg);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
