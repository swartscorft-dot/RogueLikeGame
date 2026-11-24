using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// A room is a fight (work order 070, 2026-11-21, Kristian after his evening session: "jeg vil også at nå rman kommer i et
    /// rom, så er man i en combat. det skal ikke være sånn at man skal lete og agrroe folk på avstand ... så man trenger et
    /// punkt in i rommet der alle flyr på"; <see cref="RoomFightRules"/>, <see cref="LevelValues.RoomFights"/>).
    ///
    /// When a floor in a line is built, each room that holds enemies gets its <see cref="RoomFight"/>, and each enemy of it
    /// is told its room's fight (<see cref="Enemy.Fight"/>); a swarmer the boss calls belongs to the boss's room's fight as
    /// it is made. Every frame, after her room is tracked: as she comes into a room, the door she came by is taken (the
    /// room's door nearest her); once she is at or past the room's point measured from it, its fight starts. Any waking of
    /// one of its enemies starts it too (the enemy stirs the fight itself: noticed by its range, hurt, provoked). A fight
    /// that starts is heard once (<see cref="RoomFightValues.CueSound"/>) and told to the recorder: the floor, the room by
    /// the run record's count (the passages not counted), how it started, how many of its enemies woke. A fight on ends
    /// (<see cref="RoomFightRules.Ends"/>) when she is dead, the room is cleared, or she is outside the room and farther than
    /// its own leash from every one of its enemies still alive; every one of them then goes home at once.
    ///
    /// With the switch off, on a floor before the line, and in the practice room, no enemy has a fight: as before.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        readonly Dictionary<int, RoomFight> _fights = new Dictionary<int, RoomFight>();
        readonly List<RoomFight> _fightList = new List<RoomFight>();
        GeneratedLevel _fightsOf;
        LevelRoom _fightRoomIn;
        LevelDoor _fightDoor;

        /// <summary>The fights of this floor's rooms (work order 070), in the order of the rooms; empty with the switch off.</summary>
        public IReadOnlyList<RoomFight> RoomFights => _fightList;

        /// <summary>The fight of a room of this floor, or null (no enemies, a passage, the switch off).</summary>
        public RoomFight FightOf(LevelRoom room) => room != null && _fights.TryGetValue(room.Order, out RoomFight f) ? f : null;

        /// <summary>The door she came into the room she is in by, from which its point is measured; null outside a fight's room.</summary>
        public LevelDoor FightDoor => _fightDoor;

        /// <summary>Gives each room of the floor just built its fight, and each of its enemies its room's (work order 070).</summary>
        void SetUpRoomFights()
        {
            ForgetRoomFights();
            if (_level == null || !Values.RoomFights || Values.Fights == null) return;
            LevelLayout layout = _level.Layout;
            foreach (LevelRoom room in layout.Rooms)
            {
                if (!_level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) continue;
                if (!RoomFightRules.HoldsAFight(layout, room, list.Count)) continue;
                var fight = new RoomFight(room.Order);
                _fights[room.Order] = fight;
                _fightList.Add(fight);
                foreach (Enemy e in list) if (e != null) e.Fight = fight;
            }
            SetUpRoomDoors(); // work order 086: the doors that close behind her (GeneratedLevelsRoomDoors.cs)
            if (_fightList.Count == 0) return;
            _fightsOf = _level;
            _level.EnemyCalled += OnCalledIntoAFight;
        }

        void ForgetRoomFights()
        {
            ForgetRoomUse(); // work order 097: a fight followed ends with the floor (GeneratedLevelsRoomUse.cs)
            if (_fightsOf != null) _fightsOf.EnemyCalled -= OnCalledIntoAFight;
            _fightsOf = null;
            _fights.Clear();
            _fightList.Clear();
            _fightRoomIn = null;
            _fightDoor = null;
            ForgetRoomDoors(); // work order 086
        }

        /// <summary>A swarmer the boss calls belongs to the boss's room's fight at once (work order 070): it comes for her awake.</summary>
        void OnCalledIntoAFight(Enemy called)
        {
            if (called == null || _level == null || _level.Boss == null) return;
            called.Fight = _level.Boss.Fight;
        }

        /// <summary>Every frame, after her room is tracked (work order 070): the point, the fights that started, the fights that end.</summary>
        void TickRoomFights()
        {
            if (_fightList.Count == 0 || _level == null || _player == null) return;
            float now = Time.time;
            LevelLayout layout = _level.Layout;
            LevelPoint her = _level.ToLevel(_player.Position);
            LevelRoom here = CurrentRoom;
            if (here != _fightRoomIn)
            {
                _fightRoomIn = here;
                _fightDoor = here != null && _fights.ContainsKey(here.Order) ? RoomFightRules.DoorSheCameBy(layout, here, her) : null;
            }
            bool herAlive = _health == null || _health.IsAlive;
            if (herAlive && here != null && _fights.TryGetValue(here.Order, out RoomFight hereFight) && !hereFight.On
                && AliveOf(here.Order) > 0 && RoomFightRules.PastThePoint(layout, here, _fightDoor, her, Values.Fights))
                hereFight.Stir(RoomFightStart.Point, now);

            for (int i = 0; i < _fightList.Count; i++) // every frame: no LINQ, nothing made
            {
                RoomFight f = _fightList[i];
                if (f.TakeStarted()) RoomFightStarted(f);
                if (!f.On) continue;
                int alive = AliveOf(f.Room);
                bool within = alive > 0 && AnyWithinLeash(f.Room);
                if (!RoomFightRules.Ends(herAlive, alive, here != null && here.Order == f.Room, within)) continue;
                f.End();
                NoteRoomUseEnded(f, herAlive, alive); // work order 097 (GeneratedLevelsRoomUse.cs)
            }
            TickRoomDoors(); // work order 086: after the fights, the doors that close behind her and open as the room is cleared
        }

        /// <summary>A room's fight started: one cue for the room, and the recorder told (work order 070).</summary>
        void RoomFightStarted(RoomFight f)
        {
            LevelRoom room = f.Room >= 0 && f.Room < _level.Layout.Rooms.Count ? _level.Layout.Rooms[f.Room] : null;
            int woke = AliveOf(f.Room);
            RoomFightValues values = Values.Fights;
            if (values != null && values.Cue && values.CueGain > 0f && !PlaceholderSounds.RoomsOn) PlaceholderSounds.Current?.AskFor(values.CueSound, values.CueGain); // work order 113: on, the room's own swell, once a room
            if (room == null) return;
            string kind = room.Elite ? "elite" : room.Kind.ToString().ToLowerInvariant();
            bool inside = CurrentRoom != null && CurrentRoom.Order == f.Room;
            PlaySessionRecorder.Current?.NoteRoomFight(Floor > 0 ? Floor : _level.Layout.Floor, LevelGeometry.RoomNumber(_level.Layout, room) + 1, room.Order,
                                                       kind, RoomFightRules.Word(f.StartedBy), inside, woke);
            NoteRoomUseBegan(f, room, Floor > 0 ? Floor : _level.Layout.Floor, kind); // work order 097: the room as she meets it (GeneratedLevelsRoomUse.cs)
        }

        /// <summary>The enemies of a room's fight still alive: the room's own, and the swarmers the boss called into it.</summary>
        int AliveOf(int room)
        {
            int n = 0;
            if (_level.EnemiesByRoom.TryGetValue(room, out List<Enemy> own))
                for (int i = 0; i < own.Count; i++) if (own[i] != null && !own[i].IsDead) n++;
            if (_level.CalledByRoom.TryGetValue(room, out List<Enemy> called))
                for (int i = 0; i < called.Count; i++) if (called[i] != null && !called[i].IsDead) n++;
            return n;
        }

        /// <summary>Is any living enemy of the room's fight within its own leash of her (straight-line, as the leash is)?</summary>
        bool AnyWithinLeash(int room)
        {
            Vector3 her = _player.Position;
            return WithinLeash(_level.EnemiesByRoom, room, her) || WithinLeash(_level.CalledByRoom, room, her);
        }

        static bool WithinLeash(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room, Vector3 her)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if (e == null || e.IsDead) continue;
                Vector3 d = e.transform.position - her;
                float leash = e.Values.LeashRange;
                if (d.x * d.x + d.z * d.z <= leash * leash) return true;
            }
            return false;
        }
    }
}
