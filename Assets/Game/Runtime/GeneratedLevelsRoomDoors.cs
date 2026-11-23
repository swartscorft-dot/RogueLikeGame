using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The door closes behind her (work order 086; Kristian's answer of 2026-11-22: "Døra stenges bak deg, men vi bør balansere
    /// rommene med flere muligheter og, slik at man kan bruke miljøet til sin fordel"; the rules in <see cref="RoomDoorRules"/>,
    /// the values in <see cref="RoomFightValues"/> beside the fight's line).
    ///
    /// Every room that holds a fight (work order 070) gets its doors' state as the floor is set up; nothing is made then. Every
    /// frame, after the fights are stepped: a room whose fight is on, with her inside it (on its own floor, 1.5 m from each of its
    /// doors) and every living enemy of the fight inside too, shuts all its doors: the first time, a <see cref="RoomDoor"/> is made
    /// for each. While an enemy of it is not inside they wait; after <see cref="RoomFightValues.DoorPullAfter"/> those are pulled
    /// in. Shut doors open when the fight is over (cleared), she is dead, or she is off the room's floor. The recorder hears both,
    /// with the seconds between. With <see cref="RoomFightValues.Doors"/> off nothing here does anything; in the practice room and
    /// on a floor before the line there are no fights, and so no doors.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>One room's doors (work order 086).</summary>
        public sealed class RoomDoors
        {
            public LevelRoom Room;
            public RoomFight Fight;
            /// <summary>The doors of the layout that join the room: the way in and the way on.</summary>
            public readonly List<LevelDoor> Doors = new List<LevelDoor>();
            /// <summary>Their blocks, one a door in the same order, made the first time they close; empty before.</summary>
            public readonly List<RoomDoor> Blocks = new List<RoomDoor>();
            /// <summary>True while they are shut.</summary>
            public bool Shut;
            /// <summary>When they last closed and last opened (game time); minus infinity before.</summary>
            public float ClosedAt = float.NegativeInfinity, OpenedAt = float.NegativeInfinity;
            /// <summary>Since when she has been inside with the fight on and the doors waiting for an enemy; minus one when not.</summary>
            public float WaitingSince = -1f;
            /// <summary>Times they closed on this floor; enemies pulled in.</summary>
            public int Closings, PulledIn;
            /// <summary>True while a block is rising or sinking.</summary>
            public bool Moving;
        }

        readonly Dictionary<int, RoomDoors> _doors = new Dictionary<int, RoomDoors>();
        readonly List<RoomDoors> _doorList = new List<RoomDoors>();
        readonly List<Enemy> _notInside = new List<Enemy>();

        /// <summary>The doors of this floor's rooms that hold a fight (work order 086), in the order of the rooms; empty with the switch off.</summary>
        public IReadOnlyList<RoomDoors> RoomDoorList => _doorList;

        /// <summary>The doors of a room of this floor, or null (no fight, the switch off).</summary>
        public RoomDoors DoorsOf(LevelRoom room) => room != null && _doors.TryGetValue(room.Order, out RoomDoors d) ? d : null;

        /// <summary>True while the doors of the room she is in are shut.</summary>
        public bool ShutIn => CurrentRoom != null && _doors.TryGetValue(CurrentRoom.Order, out RoomDoors d) && d.Shut;

        /// <summary>After the fights are given out (<see cref="SetUpRoomFights"/>): each fight's room its doors' state, nothing made.</summary>
        void SetUpRoomDoors()
        {
            _doors.Clear();
            _doorList.Clear();
            if (_level == null || !RoomDoorRules.On(Values.Fights)) return;
            bool lineOrHurt = RoomDoorRules.WakesOnlyByLineOrHurt(Values.Fights);
            LevelLayout layout = _level.Layout;
            foreach (RoomFight f in _fightList)
            {
                f.WakesOnlyByLineOrHurt = lineOrHurt;
                LevelRoom room = f.Room >= 0 && f.Room < layout.Rooms.Count ? layout.Rooms[f.Room] : null;
                if (room == null) continue;
                var d = new RoomDoors { Room = room, Fight = f };
                RoomDoorRules.DoorsOf(layout, room, d.Doors);
                if (d.Doors.Count == 0) continue;
                _doors[room.Order] = d;
                _doorList.Add(d);
            }
        }

        /// <summary>The floor goes: its doors' blocks go with its root.</summary>
        void ForgetRoomDoors()
        {
            _doors.Clear();
            _doorList.Clear();
            _notInside.Clear();
        }

        /// <summary>
        /// Every frame after the fights are stepped (work order 086): shut doors that should open, open doors that should shut, and
        /// the blocks that rise or sink. Nothing is allocated, but the first time a room's doors close (their blocks are made).
        /// Public for the tests, which step it themselves to see that it allocates nothing.
        /// </summary>
        public void TickRoomDoors()
        {
            if (_doorList.Count == 0 || _level == null || _player == null) return;
            RoomFightValues v = Values.Fights;
            if (v == null) return;
            bool on = RoomDoorRules.On(v);
            float now = Time.time;
            bool herAlive = _health == null || _health.IsAlive;
            LevelRoom here = CurrentRoom;
            LevelLayout layout = _level.Layout;
            for (int i = 0; i < _doorList.Count; i++) // every frame: no LINQ, nothing made
            {
                RoomDoors d = _doorList[i];
                bool hereIsIt = here != null && here.Order == d.Room.Order;
                if (d.Shut)
                {
                    DoorOpened? why = RoomDoorRules.OpensNow(d.Fight.On, InPlayOf(d.Room.Order), herAlive, hereIsIt);
                    if (why.HasValue) OpenDoors(d, why.Value, now);
                }
                else
                {
                    // Only a fight with an enemy in play holds her: one switched off (a test's, out of her way) is not one.
                    bool fightOn = d.Fight.On && (hereIsIt ? InPlayOf(d.Room.Order) > 0 : false);
                    bool inside = hereIsIt && RoomDoorRules.IsInside(layout, d.Room, d.Doors, _level.ToLevel(_player.Position), v.DoorClear);
                    int notInside = on && fightOn && herAlive && inside ? EnemiesNotInside(d, v.DoorClear) : 0;
                    if (on && fightOn && herAlive && inside)
                    {
                        if (d.WaitingSince < 0f) d.WaitingSince = now;
                    }
                    else d.WaitingSince = -1f;
                    DoorStep step = RoomDoorRules.WhenOpen(on, fightOn, herAlive, inside, notInside, d.WaitingSince >= 0f ? now - d.WaitingSince : 0f, v.DoorPullAfter);
                    if (step == DoorStep.PullIn)
                    {
                        PullIn(d, v);
                        step = DoorStep.Close;
                    }
                    if (step == DoorStep.Close) CloseDoors(d, v, now);
                }
                if (d.Moving) MoveBlocks(d, v, now);
            }
        }

        /// <summary>The enemies of a room's fight in play: alive, and not switched off (its own, and the swarmers the boss called).</summary>
        int InPlayOf(int room) => InPlay(_level.EnemiesByRoom, room) + InPlay(_level.CalledByRoom, room);

        static int InPlay(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead && list[i].isActiveAndEnabled) n++;
            return n;
        }

        /// <summary>The living enemies of a room's fight (its own and the called) that are not inside it for its doors, into <see cref="_notInside"/>; how many.</summary>
        int EnemiesNotInside(RoomDoors d, float clear)
        {
            _notInside.Clear();
            AddNotInside(_level.EnemiesByRoom, d, clear);
            AddNotInside(_level.CalledByRoom, d, clear);
            return _notInside.Count;
        }

        void AddNotInside(IReadOnlyDictionary<int, List<Enemy>> byRoom, RoomDoors d, float clear)
        {
            if (!byRoom.TryGetValue(d.Room.Order, out List<Enemy> list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
                if (!RoomDoorRules.IsInside(_level.Layout, d.Room, d.Doors, _level.ToLevel(e.transform.position), clear)) _notInside.Add(e);
            }
        }

        /// <summary>
        /// The enemies of the fight not inside are put inside at once (work order 086): each 1 m past the doors' clearance, in from
        /// the door nearest it, a metre apart along it, on the NavMesh. One that cannot be put there (no NavMesh) stays: the doors
        /// close all the same, it is on the other side of no door she can reach.
        /// </summary>
        void PullIn(RoomDoors d, RoomFightValues v)
        {
            LevelLayout layout = _level.Layout;
            for (int i = 0; i < _notInside.Count; i++)
            {
                Enemy e = _notInside[i];
                LevelDoor door = RoomDoorRules.NearestOf(layout, d.Doors, _level.ToLevel(e.transform.position));
                if (door == null) continue;
                double along = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2);
                LevelPoint p = RoomDoorRules.PullInPlace(layout, d.Room, door, v.DoorClear + 1.0, along);
                if (layout.RoomAt(p.X, p.Y) != d.Room) p = RoomDoorRules.PullInPlace(layout, d.Room, door, v.DoorClear + 1.0, 0d);
                if (!NavMesh.SamplePosition(_level.ToWorld(p), out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                if (e.TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.Warp(hit.position))
                {
                    d.PulledIn++;
                    Debug.Log("Room doors: " + e.LogWord + " " + e.name + " pulled in to " + d.Room.Letter + d.Room.Order + " before its doors closed");
                }
            }
            _notInside.Clear();
        }

        /// <summary>A room's doors shut (work order 086): their blocks made the first time, shut at once, rising into view; heard and told.</summary>
        void CloseDoors(RoomDoors d, RoomFightValues v, float now)
        {
            if (d.Blocks.Count == 0)
                for (int k = 0; k < d.Doors.Count; k++) d.Blocks.Add(RoomDoor.Make(_level, d.Room.Order, d.Doors[k], v));
            for (int k = 0; k < d.Blocks.Count; k++) if (d.Blocks[k] != null) d.Blocks[k].SetShut(true);
            d.Shut = true;
            d.Moving = true;
            d.ClosedAt = now;
            d.WaitingSince = -1f;
            d.Closings++;
            Physics.SyncTransforms(); // the shots of this frame meet it
            if (v.DoorCloseGain > 0f) PlaceholderSounds.Current?.AskFor(v.DoorCloseSound, v.DoorCloseGain);
            PlaySessionRecorder.Current?.NoteRoomDoorsClosed(DoorFloor, LevelGeometry.RoomNumber(_level.Layout, d.Room) + 1, d.Room.Order);
        }

        /// <summary>A room's doors open (work order 086): off at once, sinking out of view; told with the seconds they stood.</summary>
        void OpenDoors(RoomDoors d, DoorOpened why, float now)
        {
            for (int k = 0; k < d.Blocks.Count; k++) if (d.Blocks[k] != null) d.Blocks[k].SetShut(false);
            d.Shut = false;
            d.Moving = true;
            d.OpenedAt = now;
            PlaySessionRecorder.Current?.NoteRoomDoorsOpened(DoorFloor, LevelGeometry.RoomNumber(_level.Layout, d.Room) + 1, d.Room.Order, now - d.ClosedAt, RoomDoorRules.Word(why));
        }

        int DoorFloor => Floor > 0 ? Floor : _level.Layout.Floor;

        /// <summary>The blocks rise or sink by the time since the doors closed or opened; done, they stop being moved.</summary>
        void MoveBlocks(RoomDoors d, RoomFightValues v, float now)
        {
            float raised = RoomDoorRules.Raised(d.Shut, now - (d.Shut ? d.ClosedAt : d.OpenedAt), v.DoorRiseSeconds, v.DoorSinkSeconds);
            for (int k = 0; k < d.Blocks.Count; k++) if (d.Blocks[k] != null) d.Blocks[k].Show(raised);
            if (d.Shut ? raised >= 1f : raised <= 0f) d.Moving = false;
        }

        /// <summary>For the tests: one step of the doors, as a frame takes it.</summary>
        public void TickRoomDoorsForTests() => TickRoomDoors();
    }
}
