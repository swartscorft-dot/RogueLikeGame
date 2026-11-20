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

        public bool Winding;
        public string Attack;
        public EnemyAttackShape Shape;
        public Vector3 AimPoint;
        public float Reach, Radius, HalfWidth, Angle;
        public float WindupLeft;
        /// <summary>The telegraph is blue: Stopping Shot from its front drops it.</summary>
        public bool Counterable;
        public float CounterHalfAngle;

        /// <summary>The boss's charge running down its line now.</summary>
        public bool Charging;
        public Vector3 ChargeFrom, ChargeDirection;
        public float ChargeReach, ChargeHalfWidth;

        public bool Melee => Kind != EnemyKind.Shooter;
        /// <summary>A kind with an attack Stopping Shot can drop: the brute's shockwave, the guard's sweep, the boss's charge.</summary>
        public bool HasCounterable => Kind == EnemyKind.Brute || Kind == EnemyKind.Guard || Kind == EnemyKind.Boss;
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
    /// </summary>
    public sealed class PilotEyes
    {
        public const float SightRange = 30f;

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
                EnemyAttackDefinition a = e.Attack;
                if (e.State == EnemyState.Windup && a != null && e.IsTelegraphing)
                {
                    seen.Winding = true;
                    seen.Attack = a.Name;
                    seen.Shape = a.Shape;
                    seen.AimPoint = e.AimPoint;
                    seen.Radius = a.Radius;
                    seen.HalfWidth = a.HalfWidth;
                    seen.Angle = a.Angle;
                    seen.WindupLeft = e.WindupRemaining;
                    seen.Counterable = e.TelegraphIsCounterable;
                    seen.Reach = e is EnemyShooter shooter ? shooter.LineReach
                               : e is EnemyBoss b && a.Rush > 0f ? b.ChargeReach
                               : a.Length;
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
