using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// How much of the room she uses (work order 097), as the floor tells the recorder: as a room's fight begins, the room as she meets
    /// it (<see cref="RoomSpace"/>: its walkable floor on a 2 m grid and its covers, in its own metres from the door she came in by, or
    /// from its door nearest her when it woke before she was inside), and as that fight ends, why. The recorder writes her places
    /// between (<see cref="PlaySessionRecorder"/>, PlaySessionRecorderRoomUse.cs). One fight is followed at a time, the last that began.
    /// Nothing changes for her, the room or its enemies; with no recorder nothing is done.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        RoomFight _usedFight; // the fight whose places the recorder writes, or null
        readonly List<LevelProp> _useProps = new List<LevelProp>();

        /// <summary>A room's fight began (from <see cref="RoomFightStarted"/>): the recorder is told its room and follows it.</summary>
        void NoteRoomUseBegan(RoomFight f, LevelRoom room, int floor, string kind)
        {
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null || !recorder.IsRecording || room == null || _level == null || _player == null) return;
            LevelLayout layout = _level.Layout;
            LevelDoor door = CurrentRoom == room && _fightDoor != null ? _fightDoor : RoomFightRules.DoorSheCameBy(layout, room, _level.ToLevel(_player.Position));
            RoomFrame frame = RoomUseRules.FrameOf(layout, room, door);
            _useProps.Clear();
            AddStanding(_level.Props, room);
            AddStanding(_level.LargeProps, room);
            RoomSpace space = RoomSpace.Make(layout, room, door, frame, _useProps);
            _useProps.Clear();
            Vector3 origin = _level.ToWorld(new LevelPoint(frame.OriginX, frame.OriginY));
            string template = room.Template != null && room.Template.Template != null ? room.Template.Template.Name : "";
            recorder.NoteFightRoom(floor, LevelGeometry.RoomNumber(layout, room) + 1, room.Order, kind, template, space, frame.MovedTo(origin.x, origin.z));
            _usedFight = f;
        }

        /// <summary>The room's props that stand (built), as work order 088 counts them: its own and the mood's large props.</summary>
        void AddStanding(IReadOnlyList<GeneratedLevel.PlacedProp> props, LevelRoom room)
        {
            if (props == null) return;
            foreach (GeneratedLevel.PlacedProp p in props)
                if (p != null && p.Room == room && p.Prop != null && p.Object != null) _useProps.Add(p.Prop);
        }

        /// <summary>A fight ended (from <see cref="TickRoomFights"/>): if it is the one followed, the recorder is told why.</summary>
        void NoteRoomUseEnded(RoomFight f, bool herAlive, int alive)
        {
            if (f == null || f != _usedFight) return;
            _usedFight = null;
            PlaySessionRecorder.Current?.NoteFightEnd(!herAlive ? "her death" : alive <= 0 ? "cleared" : "she left");
        }

        /// <summary>The floor's fights are forgotten (a new floor, the run's end): a fight still followed ends.</summary>
        void ForgetRoomUse()
        {
            if (_usedFight == null) return;
            _usedFight = null;
            PlaySessionRecorder.Current?.NoteFightEnd(PlaySessionRecorder.FloorGone);
        }
    }
}
