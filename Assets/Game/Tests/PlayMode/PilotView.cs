using System.Collections.Generic;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// An enemy as a player sees it on screen (work order 022, 2026-11-20): where it is and faces, whether it is awake,
    /// dazed or winding up, the marks over it, and the telegraph on the ground while it winds up (its shape, where it is
    /// aimed, how far it reaches, how long until it lands: the telegraph thickens to the strike). Plain values.
    /// </summary>
    public struct EnemySeen
    {
        public int Id;
        public EnemyKind Kind;
        public Vector3 Position;
        public Vector3 Forward;
        public float BodyRadius;
        /// <summary>The room it stands in now, by its place in the run; -1 outside every room or in the practice room.</summary>
        public int Room;
        /// <summary>The room it stands in at home, where she met it: a room is cleared when none of its own lives.</summary>
        public int HomeRoom;
        public bool Awake, Dazed;
        /// <summary>No wall or tall prop between her and it, at the height shots fly.</summary>
        public bool Visible;
        public int Marks;
        public float Distance;

        /// <summary>
        /// It winds up an attack she knows of: a telegraph on the floor, a tell on its body (work order 046), or a draw
        /// that ends in things that fly; each only from <see cref="PilotEyes.ReactionTime"/> after the wind-up began.
        /// </summary>
        public bool Winding;
        public string Attack;
        public EnemyAttackShape Shape;
        /// <summary>
        /// Where the strike is struck from: the circle's middle, the line's or the cone's start. The enemy's place, but for
        /// a leap (where it lands) and a lunge (where it runs from; its line toward <see cref="AimPoint"/>).
        /// </summary>
        public Vector3 StrikeFrom;
        public Vector3 AimPoint;
        public float Reach, Radius, HalfWidth, Angle;
        public float WindupLeft;
        /// <summary>The telegraph is blue, or the glint on its body (work order 046): Stopping Shot from its front drops it.</summary>
        public bool Counterable;
        public float CounterHalfAngle;
        /// <summary>Work order 046: the attack is read on its body (decision 0010), not a shape on the floor.</summary>
        public bool ReadOnBody;
        /// <summary>The attack wound up is an ordinary one (work order 035): a short cycle, little damage.</summary>
        public bool Ordinary;
        /// <summary>
        /// The guard's Drag (work order 035): it pulls her in when it goes off from anywhere within <see cref="Radius"/>
        /// with no wall or tall prop between, so it is not stepped out of; the dash's protected part breaks it.
        /// </summary>
        public bool Pulls;
        /// <summary>A draw that ends in things that fly (the shooter's bow, the caster's orb): no area of its own on the ground.</summary>
        public bool Throws;
        /// <summary>m from its middle that its ordinary attack reaches (work order 046); 0 for a kind with none.</summary>
        public float OrdinaryReach;

        /// <summary>The boss's charge running down its line now.</summary>
        public bool Charging;
        public Vector3 ChargeFrom, ChargeDirection;
        public float ChargeReach, ChargeHalfWidth;

        public bool Melee => Kind != EnemyKind.Shooter;
        /// <summary>A kind with an attack Stopping Shot can drop: the brute's shockwave, the guard's sweep, the boss's charge.</summary>
        public bool HasCounterable => Kind == EnemyKind.Brute || Kind == EnemyKind.Guard || Kind == EnemyKind.Boss;
    }

    /// <summary>
    /// A thing that flies at her as a player sees it (work order 046, 2026-11-20): the arrow, the orb, the wave
    /// (<see cref="Projectile"/>). Where it is, the way it flies, its speed, its size, the wave's half width, the flight it
    /// has left. Seen only from <see cref="PilotEyes.ReactionTime"/> after it left.
    /// </summary>
    public struct FlyingSeen
    {
        public ProjectileKind Kind;
        public string Attack;
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed, Radius, HalfWidth, FlightLeft;
        /// <summary>s since it left.</summary>
        public float Age;
        public float Distance;
    }

    public struct RoomSeen
    {
        public int Order;
        public RoomKind Kind;
        public Vector3 Min, Max, Middle;

        public bool Contains(Vector3 p, float inset = 0f) =>
            p.x >= Min.x + inset && p.x <= Max.x - inset && p.z >= Min.z + inset && p.z <= Max.z - inset;
    }

    public struct DoorSeen
    {
        public int A, B;
        public Vector3 Middle;
    }

    public struct ChestSeen
    {
        public int Room;
        public Vector3 Position;
        public bool Opened;
    }

    /// <summary>
    /// What a pilot sees once a frame (work order 022): the floor's map (its rooms, doors and chests: a player sees them
    /// as she walks, and the floor is small), the room she is in, and the living enemies near enough to be on screen. Made
    /// by <see cref="PilotEyes"/>; nothing in it is a reference into the game.
    /// </summary>
    public sealed class PilotView
    {
        /// <summary>Changes when a new floor is built (or the practice room has no floor: 0).</summary>
        public int MapVersion;
        public bool HasMap;
        public readonly List<RoomSeen> Rooms = new List<RoomSeen>();
        public readonly List<DoorSeen> Doors = new List<DoorSeen>();
        public readonly List<ChestSeen> Chests = new List<ChestSeen>();
        public int HereRoom = -1;
        public float ChestReach = 2f;
        public readonly List<EnemySeen> Enemies = new List<EnemySeen>();
        /// <summary>The things that fly she can see (work order 046).</summary>
        public readonly List<FlyingSeen> Flying = new List<FlyingSeen>();

        public bool TryRoom(int order, out RoomSeen room)
        {
            foreach (RoomSeen r in Rooms)
                if (r.Order == order) { room = r; return true; }
            room = default;
            return false;
        }
    }

    /// <summary>
    /// Fills a <see cref="PilotView"/> from the game, for the harness (work order 022): the pilots never see the objects.
    /// Enemies are those alive within <see cref="SightRange"/> of her.
    ///
    /// Work order 046 (the night of 2026-11-20): it sees what a person sees of the enemies rebuilt in work orders 035 and
    /// 036 (decision 0010), and no sooner than a person could. An attack is known from the start of its wind-up plus
    /// <see cref="ReactionTime"/>, whether its tell is a shape on the floor, a tell on the body (the swarmer, the brute and
    /// the guard: the area the strike hits as the rules have it, when it lands, whether it can be countered) or the draw
    /// of a bow or an orb; an attack whose wind-up is shorter than that is never seen coming (the swarmer's bite). A thing
    /// that flies is seen from its release plus the same time, within <see cref="SightRange"/>. Nothing of an enemy's
    /// choice is read before its wind-up starts, and where a lunge or a leap will go is read as the rules aim it now
    /// (at her, until it leaves), never ahead.
    /// </summary>
    public sealed class PilotEyes
    {
        public const float SightRange = 30f;
        /// <summary>
        /// s from the start of a wind-up, or a release, until the pilot knows of it (work order 046). Before, the pilot
        /// had none: it saw a telegraph the frame it was drawn. A person's reaction to a seen cue is about a quarter of a
        /// second; it holds for the floor shapes too now.
        /// </summary>
        public const float ReactionTime = 0.25f;
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

        readonly PlayerCharacter _player;
        readonly GeneratedLevels _levels;
        GeneratedLevel _mapOf;
        int _version;

        public PilotEyes(PlayerCharacter player, GeneratedLevels levels)
        {
            _player = player;
            _levels = levels;
        }

        public void Look(PilotView view)
        {
            GeneratedLevel level = _levels != null ? _levels.Level : null;
            if (level != _mapOf)
            {
                _mapOf = level;
                _version++;
                FillMap(view, level);
            }
            view.MapVersion = _version;
            view.HasMap = level != null;
            view.ChestReach = _levels != null ? _levels.RunValues.ChestReach : 2f;
            view.HereRoom = _levels != null && _levels.CurrentRoom != null ? _levels.CurrentRoom.Order : -1;
            if (level != null)
            {
                view.Chests.Clear();
                foreach (GeneratedLevel.PlacedProp chest in _levels.Chests())
                {
                    if (chest.Object == null) continue;
                    view.Chests.Add(new ChestSeen
                    {
                        Room = chest.Room.Order, Position = chest.Object.transform.position,
                        Opened = _levels.ChestsOpened.Contains(chest.Room.Order),
                    });
                }
            }

            view.Enemies.Clear();
            Vector3 her = _player.Position;
            var herGround = new GroundPoint(her.x, her.z);
            foreach (Enemy e in Enemy.All)
            {
                if (e == null || e.IsDead) continue;
                Vector3 p = e.transform.position;
                float distance = new Vector2(p.x - her.x, p.z - her.z).magnitude;
                LevelRoom homeRoom = level != null ? level.RoomAt(e.Home) : null;
                int home = homeRoom != null ? homeRoom.Order : -1;
                // Out of sight, but one of the room she is in: the HUD's box counts the enemies left in it.
                if (distance > SightRange && !(home >= 0 && home == view.HereRoom)) continue;
                float radius = e.Body != null ? e.Body.Radius : 0.5f;
                LevelRoom inRoom = level != null ? level.RoomAt(p) : null;
                var seen = new EnemySeen
                {
                    Id = e.Id, Kind = e.Kind, Position = new Vector3(p.x, 0f, p.z), Forward = e.transform.forward,
                    BodyRadius = radius, Room = inRoom != null ? inRoom.Order : -1, HomeRoom = home,
                    Awake = e.IsAwake, Dazed = e.IsDazed, Marks = e.Body != null ? e.Body.MarkCount : 0, Distance = distance,
                    Visible = Cover.ClearDistance(herGround, new GroundPoint(p.x, p.z)) >= distance - radius - 0.05f,
                };
                MeleeEnemy melee = e as MeleeEnemy;
                if (melee != null && melee.MeleeOn) seen.OrdinaryReach = OrdinaryReachOf(melee.AttacksNow);
                EnemyAttackDefinition a = e.Attack;
                if (e.State == EnemyState.Windup && a != null && a.Windup - e.WindupRemaining >= ReactionTime)
                {
                    bool body = !e.IsTelegraphing && e.ReadOnBody;
                    bool throws = !e.IsTelegraphing && !body && (e is EnemyShooter || e is EnemyCaster);
                    if (e.IsTelegraphing || body || throws)
                    {
                        seen.Winding = true;
                        seen.Attack = a.Name;
                        seen.Shape = a.Shape;
                        seen.StrikeFrom = seen.Position;
                        seen.AimPoint = e.AimPoint;
                        seen.Radius = a.Radius;
                        seen.HalfWidth = a.HalfWidth;
                        seen.Angle = a.Angle;
                        seen.WindupLeft = e.WindupRemaining;
                        seen.Ordinary = a.Ordinary;
                        seen.Counterable = e.IsTelegraphing ? e.TelegraphIsCounterable : PoolRules.IsCounterable(a);
                        seen.Reach = e is EnemyShooter shooter ? shooter.LineReach
                                   : e is EnemyBoss b && a.Rush > 0f ? b.ChargeReach
                                   : a.Length;
                        seen.ReadOnBody = body;
                        seen.Throws = throws;
                        if (body && melee != null) ReadBody(ref seen, melee, a, her);
                    }
                }
                if (e is EnemyBoss boss && boss.IsCharging)
                {
                    seen.Charging = true;
                    seen.ChargeFrom = boss.ChargeFrom;
                    seen.ChargeDirection = boss.ChargeDirection;
                    seen.ChargeReach = boss.ChargeReach;
                    seen.ChargeHalfWidth = ChargeHalfWidth(boss);
                }
                view.Enemies.Add(seen);
            }

            // The things that fly (work order 036), each from its release plus the reaction time (work order 046).
            view.Flying.Clear();
            ThingsThatFly fly = ThingsThatFly.Current;
            if (fly == null) return;
            fly.ListFlying(_shown);
            foreach (ThingsThatFly.Shown s in _shown)
            {
                if (s.Passed || s.Ending || s.Owner == null) continue;
                if (!Make(s, out Projectile p)) continue;
                float age = p.Speed > 0f ? s.Flown / p.Speed : 0f;
                if (age < ReactionTime) continue;
                float distance = new Vector2(s.Position.x - her.x, s.Position.z - her.z).magnitude;
                if (distance > SightRange) continue;
                view.Flying.Add(new FlyingSeen
                {
                    Kind = s.Kind, Attack = s.Attack, Position = s.Position, Direction = s.Direction,
                    Speed = p.Speed, Radius = p.Radius, HalfWidth = p.HalfWidth, FlightLeft = Mathf.Max(0f, p.MaxFlight - s.Flown),
                    Age = age, Distance = distance,
                });
            }
        }

        /// <summary>Its speed and size as the game makes one of its kind from its owner's values (<see cref="ProjectileRules"/>).</summary>
        static bool Make(ThingsThatFly.Shown s, out Projectile p)
        {
            var from = new GroundPoint(s.Position.x, s.Position.z);
            var dir = new GroundPoint(s.Direction.x, s.Direction.z);
            p = default;
            if (s.Kind == ProjectileKind.Arrow && s.Owner is EnemyShooter shooter) p = ProjectileRules.Arrow(shooter.Tuning, from, dir);
            else if (s.Kind == ProjectileKind.Orb && s.Owner is EnemyCaster caster) p = ProjectileRules.Orb(caster.Tuning, from, dir);
            else if (s.Kind == ProjectileKind.Wave && s.Owner is EnemyBoss boss) p = ProjectileRules.Wave(boss.Tuning, from, dir);
            else return false;
            return true;
        }

        /// <summary>The reach of the ordinary attack in a set (work order 035), 0 when it has none.</summary>
        static float OrdinaryReachOf(EnemyAttackDefinition[] attacks)
        {
            float reach = 0f;
            if (attacks == null) return reach;
            foreach (EnemyAttackDefinition a in attacks)
                if (a != null && a.Ordinary) reach = Mathf.Max(reach, Mathf.Max(a.Length, a.MaxDistance));
            return reach;
        }

        /// <summary>
        /// The area of a melee skill read on the body, as the rules strike it (work order 046; <see cref="MeleeEnemy"/>).
        /// A Leap lands short of where she stands as it leaves the ground, so until then where she stands now, then where
        /// it is going. A Lunge runs at her along a line fixed as it starts and strikes in a circle where it stops: until
        /// then a strip toward her as long as the lunge and as wide as the circle, then toward where it runs, then the
        /// circle where it stopped. A Drag pulls her from anywhere within its length with a clear line: a circle that
        /// long, marked so. Every other attack is struck from where it stands, as a telegraph is.
        /// </summary>
        static void ReadBody(ref EnemySeen seen, MeleeEnemy melee, EnemyAttackDefinition a, Vector3 her)
        {
            Vector3 me = seen.Position;
            Vector3 herFlat = new Vector3(her.x, 0f, her.z);
            bool moveOver = !melee.IsMoving && seen.WindupLeft <= a.MoveSeconds;
            Vector3 to = new Vector3(melee.MoveTarget.x, 0f, melee.MoveTarget.z);
            switch (a.Move)
            {
                case MeleeMove.Leap:
                {
                    seen.Shape = EnemyAttackShape.Circle;
                    if (melee.IsMoving) seen.StrikeFrom = to;
                    else if (moveOver) seen.StrikeFrom = me;
                    else
                    {
                        GroundPoint land = MeleeYouReadRules.LeapLanding(new GroundPoint(me.x, me.z), new GroundPoint(her.x, her.z));
                        seen.StrikeFrom = new Vector3(land.X, 0f, land.Z);
                    }
                    break;
                }
                case MeleeMove.Lunge:
                    if (moveOver)
                    {
                        seen.Shape = EnemyAttackShape.Circle;
                        seen.StrikeFrom = me;
                        break;
                    }
                    seen.Shape = EnemyAttackShape.Line;
                    seen.StrikeFrom = me;
                    seen.AimPoint = melee.IsMoving ? to : herFlat;
                    seen.Reach = (melee.IsMoving ? new Vector2(to.x - me.x, to.z - me.z).magnitude : a.Length) + a.Radius;
                    seen.HalfWidth = a.Radius;
                    break;
                case MeleeMove.Drag:
                    seen.Shape = EnemyAttackShape.Circle;
                    seen.Radius = a.Length;
                    seen.Pulls = true;
                    break;
            }
        }

        static float ChargeHalfWidth(EnemyBoss boss)
        {
            foreach (EnemyAttackDefinition a in boss.Values.Attacks)
                if (a != null && a.Rush > 0f) return a.HalfWidth;
            return 1.5f;
        }

        static void FillMap(PilotView view, GeneratedLevel level)
        {
            view.Rooms.Clear();
            view.Doors.Clear();
            view.Chests.Clear();
            if (level == null) return;
            float cs = level.Layout.CellSize;
            foreach (LevelRoom r in level.Layout.Rooms)
            {
                Vector3 a = level.ToWorld(new LevelPoint(r.Rect.X * cs, r.Rect.Y * cs));
                Vector3 b = level.ToWorld(new LevelPoint(r.Rect.Right * cs, r.Rect.Top * cs));
                view.Rooms.Add(new RoomSeen
                {
                    Order = r.Order, Kind = r.Kind, Min = Vector3.Min(a, b), Max = Vector3.Max(a, b), Middle = level.Middle(r),
                });
            }
            foreach (LevelDoor d in level.Layout.Doors)
            {
                d.Opening(cs, out LevelPoint a, out LevelPoint b);
                view.Doors.Add(new DoorSeen { A = d.RoomA, B = d.RoomB, Middle = level.ToWorld(new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5)) });
            }
        }
    }
}
