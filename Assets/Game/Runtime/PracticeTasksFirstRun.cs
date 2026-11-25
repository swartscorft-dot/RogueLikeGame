using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The first run teaches (work order 133's follow-up; Kristian, 2026-11-25: "Første start går ditt, men et tilpasset øvingsrom som
    /// ser ut som et run, sånn at neste rom blir første vanlige rom"), behind <see cref="PracticeTasksValues.InTheFirstRun"/> under
    /// <see cref="PracticeTasksValues.On"/>.
    ///
    /// A player's first run (the saved flag not set) opens as every run does, in floor 1's own entrance room, with its kit, light and
    /// mood: nothing of the floor's layout is touched (the generator is not asked anything new; a floor is the same field for field).
    /// While she stands there, at run time and under the floor's root: the project's crypt statues as targets that do not fight back
    /// (a <see cref="Damageable"/> and a <see cref="TrainingDummy"/> each: hit, they count as the practice room's dummies do, and come
    /// back full a moment after they fall), and the room's doors held shut by the floor's own door blocks (<see cref="RoomDoor"/>, as a
    /// fight's doors are). The thirteen tasks run on the same plate; the last says the door is open and the run goes on. The doors open
    /// (sinking as a cleared room's do) once the last plate is up or the tasks are ended (H). Once she walks out of the room, the
    /// statues and the blocks go, and the next room is floor 1's first ordinary room with its ordinary fight.
    ///
    /// While she is taught, until she walks out with the door open, the run's clock is held (<see cref="GeneratedLevels.HoldRunClock"/>):
    /// the run's time and everything measured from it do not count the teaching; the session log says the run began with it
    /// (<see cref="PracticeTaskLog"/>). The skill a clean-press task is about is ready when that task begins (the producer's decision).
    /// Leaving the run, dying or a new floor before it is over stops it, saves nothing, and the next run teaches again.
    /// </summary>
    public sealed partial class PracticeTasks
    {
        GeneratedLevel _teachLevel, _taughtLevel;
        LevelRoom _teachRoom;
        readonly List<RoomDoor> _blocks = new List<RoomDoor>();
        readonly List<GameObject> _targets = new List<GameObject>();
        readonly List<LevelDoor> _doorsScratch = new List<LevelDoor>();
        Material _targetMaterial;
        bool _doorOpen;
        float _doorMovedAt = float.NegativeInfinity;
        float _teachBeganAt;
        int _readiedIndex = -1;
        bool _blocksMoving;

        /// <summary>
        /// The first run's teaching room is up now: the tasks run there and its clock is held. A plain flag, not the floor's reference:
        /// a floor destroyed reads as null by itself, and the stop must still run.
        /// </summary>
        public bool Teaching => _teaching;
        bool _teaching;
        /// <summary>The room taught in (floor 1's entrance), or null.</summary>
        public LevelRoom TeachingRoom => _teachRoom;
        /// <summary>The door blocks holding it, and the targets standing in it (for tests and pictures).</summary>
        public IReadOnlyList<RoomDoor> TeachingDoors => _blocks;
        public IReadOnlyList<GameObject> Targets => _targets;
        /// <summary>The teaching room's doors are open (or none were made).</summary>
        public bool TeachingDoorOpen => _doorOpen;

        /// <summary>Metres a target keeps from her start, the low prop's clearance radius, and the targets' colliders' clearance radius.</summary>
        const float TargetFromHer = 3f, PropClearRadius = 1.3f;
        /// <summary>Metres the NavMesh is searched round a target's or the prop's place.</summary>
        const float SampleReach = 0.4f;

        /// <summary>She is in the run being taught with nothing up: no pause, end screen, choice, menu or sheet; alive.</summary>
        public bool InTeachingRoomNow
        {
            get
            {
                if (!Teaching || !HasHer() || !_player.isActiveAndEnabled) return false;
                if (_health != null && !_health.IsAlive) return false;
                GeneratedLevels levels = GeneratedLevels.Current;
                if (levels == null || !levels.InRun || levels.Level != _teachLevel || levels.Paused || levels.Ending != GeneratedLevels.EndKind.None
                    || levels.Choice != GeneratedLevels.ChoiceKind.None || levels.BetweenRunsUp) return false;
                if (StartMenu.Holds || (RunHud.Current != null && RunHud.Current.SheetUp)) return false;
                return !PlayerInputDriver.HeldByScreen && !PlayerInputDriver.HeldByChoice;
            }
        }

        bool InTheRoomNow(GeneratedLevels levels) =>
            levels != null && levels.CurrentRoom != null && _teachRoom != null && levels.CurrentRoom.Order == _teachRoom.Order;

        /// <summary>
        /// The first run's frame: its beginning, and while it teaches, everything of it. True when the frame was its (the grey room's
        /// frame is then not taken). Allocates nothing in a frame that changes nothing.
        /// </summary>
        bool TickFirstRun(Keyboard keys)
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            if (!Teaching)
            {
                if (levels == null || levels.Level == null || levels.Level == _taughtLevel || !Values.InTheFirstRun) return false;
                bool entrance = levels.CurrentRoom != null && levels.CurrentRoom.Kind == RoomKind.Entrance;
                if (!PracticeTaskFlag.BeginsInTheRun(Values, DoneBefore, levels.InRun && !levels.InLab, levels.Floor, entrance, false)) return false;
                BeginTeaching(levels);
                if (!Teaching) return false;
            }
            // Gone from under it: the run left or ended, a new floor, the floor removed (or destroyed). Nothing is saved: the next run teaches again.
            if (levels == null || !levels.InRun || _teachLevel == null || levels.Level != _teachLevel)
            {
                StopTeaching(PracticeTaskLog.LeftTheRun);
                return false;
            }
            bool inRoom = InTheRoomNow(levels);
            if (PracticeTaskFlag.HoldsTheClock(true, _doorOpen, inRoom)) levels.HoldRunClock();
            if (_blocksMoving) MoveBlocks(levels);
            if (_doorOpen && !inRoom && levels.CurrentRoom != null)
            {
                // Out of the room with the door open: the last plate is done, the run goes on.
                if (Run != null && !Run.Over)
                {
                    Run.Note(PracticeEvent.Of(PracticeEventKind.LeftTheRoom), Time.time);
                    if (Run.Over) Over();
                    else { Run.End(); Over(); } // walked out before the last plate (the door opened by H): ended
                }
                FinishTeaching(levels);
                return true;
            }
            if (!InTeachingRoomNow) return true; // a pause, a choice, the sheet: the tasks wait (the clock is held all the same)
            if (Run != null && keys != null)
            {
                if (keys[SkipKey].wasPressedThisFrame) Skip();
                else if (keys[AgainKey].wasPressedThisFrame) EndTasks();
            }
            if (Run == null) return true;
            Walk();
            Run.Step(Time.time, Values.TickBeat);
            if (Run.Over)
            {
                Over();
                return true;
            }
            if (Run.Index != _laidIndex || Run.Ticked != _laidTicked) _laid = false;
            PracticeTask t = Run.Current;
            if (t != null && Run.Index != _readiedIndex)
            {
                _readiedIndex = Run.Index;
                if (PracticeTaskFlag.ReadyAtStart(t, true)) _player.MakeSkillReady(t.Slot);
            }
            if (!_doorOpen && PracticeTaskFlag.DoorOpens(Run)) OpenDoor();
            if (t != null && t.Kind == PracticeTaskKind.Duck) EnsureProp();
            else RemoveProp();
            return true;
        }

        // ---------------- Beginning, the room dressed ----------------

        void BeginTeaching(GeneratedLevels levels)
        {
            GeneratedLevel level = levels.Level;
            LevelRoom room = levels.CurrentRoom;
            _taughtLevel = level; // once a floor, whatever comes of it
            if (level == null || room == null) return;
            _teaching = true;
            _teachLevel = level;
            _teachRoom = room;
            _doorOpen = false;
            _readiedIndex = -1;
            _teachBeganAt = Time.time;
            RemoveProp();
            PlaceTargets(levels);
            ShutDoor(levels);
            levels.HoldRunClock();
            Begin(); // the thirteen, with the last plate's words for the run (KeysNow: InRun)
            PlaySessionRecorder.Current?.NotePracticeTasks(PracticeTaskLog.Began, 0, 0, Run != null ? Run.Tasks.Count : 0, 0f);
        }

        /// <summary>
        /// The targets: the project's crypt statue (<see cref="PracticeTasksValues.TargetModel"/>), facing her, on the first places of
        /// <see cref="PracticeTaskPlaces.Candidates"/> that are on the NavMesh with a clear way from her, inside the room, clear of its
        /// props and walls, <see cref="PracticeTasksValues.TargetsApart"/> from each other; each a target that does not fight back.
        /// </summary>
        void PlaceTargets(GeneratedLevels levels)
        {
            GeneratedLevel level = _teachLevel;
            Vector3 her = _player.Position;
            if (!NavMesh.SamplePosition(new Vector3(her.x, 0f, her.z), out NavMeshHit start, 2f, NavMesh.AllAreas)) return;
            Vector3 middle = level.Middle(_teachRoom);
            Vector3 ahead = new Vector3(middle.x - her.x, 0f, middle.z - her.z);
            if (ahead.magnitude < 2f && Camera.main != null) ahead = new Vector3(Camera.main.transform.forward.x, 0f, Camera.main.transform.forward.z);
            Physics.SyncTransforms();
            List<GroundPoint> places = PracticeTaskPlaces.Candidates(new GroundPoint(her.x, her.z), new GroundPoint(ahead.x, ahead.z), Values.TargetDistance);
            PropSize size = PropSize.Of(Values.TargetModel);
            float radius = (float)(size.Shape == PropShape.Circle ? size.Radius : 0.5 * System.Math.Max(size.Width, size.Depth));
            if (_targetMaterial == null) _targetMaterial = PropFactory.MakeMaterial(Values.TargetModel);
            foreach (GroundPoint g in places)
            {
                if (_targets.Count >= Values.Targets) break;
                if (!NavMesh.SamplePosition(new Vector3(g.X, 0f, g.Z), out NavMeshHit hit, SampleReach, NavMesh.AllAreas)) continue;
                Vector3 p = new Vector3(hit.position.x, 0f, hit.position.z);
                if (new Vector2(p.x - her.x, p.z - her.z).magnitude < TargetFromHer) continue;
                if (NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas)) continue;
                if (!InTheTeachingRoomAndClear(p, radius + Values.ClearOfProps)) continue;
                bool apart = true;
                foreach (GameObject t in _targets)
                    if (new Vector2(t.transform.position.x - p.x, t.transform.position.z - p.z).magnitude < Values.TargetsApart) apart = false;
                if (!apart) continue;
                float yaw = Mathf.Atan2(her.x - p.x, her.z - p.z) * Mathf.Rad2Deg; // its front (+Z) toward her
                LevelProp prop = LevelProp.Make(Values.TargetModel, level.ToLevel(p), Mathf.RoundToInt(yaw), Rules.PropPlace.Floor);
                GameObject go = PropFactory.Make(prop, p, level.transform, level.WallLayer, _targetMaterial, TargetName + (_targets.Count + 1));
                var target = go.AddComponent<Damageable>();
                target.MaxHealth = Placeholders.DummyHealth;
                target.Radius = radius;
                go.AddComponent<TrainingDummy>(); // hit, it counts as the practice room's dummies; fallen, it comes back full
                if (go.TryGetComponent(out BoxCollider box)) Carve(go, box.center, box.size);
                _targets.Add(go);
                Physics.SyncTransforms();
            }
        }

        /// <summary>The name of a target (with its number from 1).</summary>
        public const string TargetName = "PracticeTasks_Target_";

        static void Carve(GameObject go, Vector3 center, Vector3 size)
        {
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = center;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        /// <summary>
        /// A place inside the teaching room, <see cref="PracticeTasksValues.FromWalls"/> from its walls, with no collider of the floor's
        /// walls' layer (its walls, its props, the targets) within <paramref name="radius"/>; always true outside the teaching room.
        /// </summary>
        bool InTheTeachingRoomAndClear(Vector3 p, float radius)
        {
            if (!Teaching) return true;
            LevelLayout layout = _teachLevel.Layout;
            LevelPoint lp = _teachLevel.ToLevel(p);
            double cs = layout.CellSize, m = Values.FromWalls;
            CellRect r = _teachRoom.Rect;
            if (lp.X < r.X * cs + m || lp.X > (r.X + r.Width) * cs - m || lp.Y < r.Y * cs + m || lp.Y > (r.Y + r.Height) * cs - m) return false;
            // A box from ClearFrom to ClearTo over the ground: the floor's own collider under it is never met, a low prop's is.
            var centre = new Vector3(p.x, (ClearFrom + ClearTo) * 0.5f, p.z);
            var half = new Vector3(radius, (ClearTo - ClearFrom) * 0.5f, radius);
            return !Physics.CheckBox(centre, half, Quaternion.identity, 1 << _teachLevel.WallLayer, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Metres over the ground of the clearance box's bottom and top (the lowest prop is about 0.6 m high).</summary>
        const float ClearFrom = 0.25f, ClearTo = 1.5f;

        // ---------------- The door ----------------

        /// <summary>Every door of the room shut by a block of the floor's own (rising into view), and nobody walks or shoots through them.</summary>
        void ShutDoor(GeneratedLevels levels)
        {
            RoomFightValues v = levels.Values.Fights ?? new RoomFightValues();
            _doorsScratch.Clear();
            RoomDoorRules.DoorsOf(_teachLevel.Layout, _teachRoom, _doorsScratch);
            for (int i = 0; i < _doorsScratch.Count; i++)
            {
                RoomDoor d = RoomDoor.Make(_teachLevel, _teachRoom.Order, _doorsScratch[i], v);
                d.SetShut(true);
                _blocks.Add(d);
            }
            _doorsScratch.Clear();
            _doorOpen = _blocks.Count == 0;
            _doorMovedAt = Time.time;
            _blocksMoving = _blocks.Count > 0;
            Physics.SyncTransforms();
        }

        /// <summary>The doors open: off at once, sinking out of view as a cleared room's.</summary>
        void OpenDoor()
        {
            _doorOpen = true;
            for (int i = 0; i < _blocks.Count; i++) if (_blocks[i] != null) _blocks[i].SetShut(false);
            _doorMovedAt = Time.time;
            _blocksMoving = _blocks.Count > 0;
            Physics.SyncTransforms();
        }

        void MoveBlocks(GeneratedLevels levels)
        {
            RoomFightValues v = levels.Values.Fights ?? new RoomFightValues();
            float raised = RoomDoorRules.Raised(!_doorOpen, Time.time - _doorMovedAt, v.DoorRiseSeconds, v.DoorSinkSeconds);
            for (int i = 0; i < _blocks.Count; i++) if (_blocks[i] != null) _blocks[i].Show(raised);
            if (_doorOpen ? raised <= 0f : raised >= 1f) _blocksMoving = false;
        }

        // ---------------- The end ----------------

        /// <summary>The tasks over in the teaching room (finished or ended): its door opens; the log is told.</summary>
        void TaughtOver(PracticeTaskRun run)
        {
            if (!_doorOpen) OpenDoor();
            if (run != null)
                PlaySessionRecorder.Current?.NotePracticeTasks(run.Ended ? PracticeTaskLog.Ended : PracticeTaskLog.Finished, run.Done, run.Skipped, run.Tasks.Count, Time.time - _teachBeganAt);
        }

        /// <summary>She walked out with the door open: the statues and the blocks go, the run's clock runs from here.</summary>
        void FinishTeaching(GeneratedLevels levels)
        {
            PracticeTaskRun run = LastRun;
            PlaySessionRecorder.Current?.NotePracticeTasks(PracticeTaskLog.WalkedOn, run != null ? run.Done : 0, run != null ? run.Skipped : 0, run != null ? run.Tasks.Count : 0, Time.time - _teachBeganAt);
            ClearRoom();
        }

        /// <summary>
        /// Stopped before it was over (the run left or ended, the switch off, the component gone): nothing saved, the tasks dropped,
        /// the room's doors open and its statues gone if the floor is still there. Nothing when it does not teach.
        /// </summary>
        void StopTeaching(string why)
        {
            if (!Teaching) return;
            PracticeTaskRun run = Run;
            Run = null;
            RemoveProp();
            PlaySessionRecorder.Current?.NotePracticeTasks(why, run != null ? run.Done : 0, run != null ? run.Skipped : 0, run != null ? run.Tasks.Count : 0, Time.time - _teachBeganAt);
            ClearRoom();
        }

        void ClearRoom()
        {
            RemoveProp();
            foreach (RoomDoor d in _blocks)
                if (d != null)
                {
                    d.SetShut(false);
                    d.gameObject.SetActive(false);
                    Destroy(d.gameObject);
                }
            foreach (GameObject t in _targets)
                if (t != null)
                {
                    t.SetActive(false);
                    Destroy(t);
                }
            _blocks.Clear();
            _targets.Clear();
            if (_targetMaterial != null) Destroy(_targetMaterial);
            _targetMaterial = null;
            _blocksMoving = false;
            _doorOpen = false;
            _teaching = false;
            _teachLevel = null;
            _teachRoom = null;
            _laid = false;
        }
    }
}
