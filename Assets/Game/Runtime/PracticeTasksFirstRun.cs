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
            PropSize size = PropSize.Of(Values.TargetModel);
            float radius = (float)(size.Shape == PropShape.Circle ? size.Radius : 0.5 * System.Math.Max(size.Width, size.Depth));
            _targetHeight = (float)size.Height;
            if (_targetMaterial == null) _targetMaterial = PropFactory.MakeMaterial(Values.TargetModel);
            // The second round: first where the room's own light falls (round its braziers, one statue a brazier in turn), in view from
            // where she starts and clear of the plate, the run box and the strip; then anywhere in view; then anywhere at all.
            List<GroundPoint> lit = LitPlaces(level);
            List<GroundPoint> anywhere = PracticeTaskPlaces.Candidates(new GroundPoint(her.x, her.z), new GroundPoint(ahead.x, ahead.z), Values.TargetDistance);
            for (int pass = 0; pass < 3 && _targets.Count < Values.Targets; pass++)
                foreach (GroundPoint g in pass == 0 ? lit : anywhere)
                {
                    if (_targets.Count >= Values.Targets) break;
                    TryTarget(g, pass < 2, her, start, radius, level);
                }
        }

        /// <summary>The statues' height (the model's), for the view check and the ring.</summary>
        float _targetHeight = 2.6f;

        /// <summary>Puts a statue at <paramref name="g"/> if it can stand there (and, with <paramref name="inView"/>, is seen well from her start).</summary>
        void TryTarget(GroundPoint g, bool inView, Vector3 her, NavMeshHit start, float radius, GeneratedLevel level)
        {
            {
                if (!NavMesh.SamplePosition(new Vector3(g.X, 0f, g.Z), out NavMeshHit hit, SampleReach, NavMesh.AllAreas)) return;
                Vector3 p = new Vector3(hit.position.x, 0f, hit.position.z);
                if (new Vector2(p.x - her.x, p.z - her.z).magnitude < TargetFromHer) return;
                if (NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas)) return;
                if (inView && !SeenWell(p, her)) return;
                if (!InTheTeachingRoomAndClear(p, radius + Values.ClearOfProps)) return;
                bool apart = true;
                foreach (GameObject t in _targets)
                    if (new Vector2(t.transform.position.x - p.x, t.transform.position.z - p.z).magnitude < Values.TargetsApart) apart = false;
                if (!apart) return;
                float yaw = Mathf.Atan2(her.x - p.x, her.z - p.z) * Mathf.Rad2Deg; // its front (+Z) toward her
                LevelProp prop = LevelProp.Make(Values.TargetModel, level.ToLevel(p), Mathf.RoundToInt(yaw), Rules.PropPlace.Floor);
                GameObject go = PropFactory.Make(prop, p, level.transform, level.WallLayer, _targetMaterial, TargetName + (_targets.Count + 1));
                var target = go.AddComponent<Damageable>();
                target.MaxHealth = Placeholders.DummyHealth;
                target.Radius = radius;
                go.AddComponent<TrainingDummy>(); // hit, it counts as the practice room's dummies; fallen, it comes back full
                if (go.TryGetComponent(out BoxCollider box)) Carve(go, box.center, box.size);
                if (Values.TargetsWearARing) MakeRing(go, radius);
                _targets.Add(go);
                Physics.SyncTransforms();
            }
        }

        /// <summary>The light sources of the entrance room by their models: its braziers (the entrance's own, the generator's), and lit candles.</summary>
        static bool GivesLight(PropModel m) => m == PropModel.Brazier || m == PropModel.GreatBrazier || m == PropModel.CandleCluster || m == PropModel.Candelabra;

        /// <summary>
        /// Places round each light of the teaching room, <see cref="PracticeTasksValues.NearTheLight"/> then
        /// <see cref="PracticeTasksValues.FarFromTheLight"/> out, every 30 degrees; taken a light at a time in turn (the first good
        /// place by each light before a second by any), so the statues stand by different lights.
        /// </summary>
        List<GroundPoint> LitPlaces(GeneratedLevel level)
        {
            var lights = new List<Vector3>();
            foreach (GeneratedLevel.PlacedProp p in level.Props)
                if (p != null && p.Object != null && p.Room != null && p.Room.Order == _teachRoom.Order && GivesLight(p.Prop.Model)) lights.Add(p.Object.transform.position);
            var rings = new List<List<GroundPoint>>();
            foreach (Vector3 l in lights)
            {
                var ring = new List<GroundPoint>();
                foreach (float r in new[] { Values.NearTheLight, Values.FarFromTheLight })
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        ring.Add(new GroundPoint(l.x + Mathf.Cos(a) * r, l.z + Mathf.Sin(a) * r));
                    }
                rings.Add(ring);
            }
            var list = new List<GroundPoint>();
            for (int k = 0; k < 24; k++)
                foreach (List<GroundPoint> ring in rings) list.Add(ring[k]);
            return list;
        }

        /// <summary>Layout pixels (1080 high) a statue keeps, feet and head, from the screen's sides, and its lowest and highest place up the screen (under the plate and its keys, over the strip).</summary>
        const float ViewSide = 140f, ViewTop = PlateTop + PlateHeight + 70f, ViewBottom = 820f;

        /// <summary>
        /// Is a statue here seen well from her start: its feet and its head on the screen as the camera will stand over her, clear of the
        /// plate and the run box at the top and of the strip at the foot.
        /// </summary>
        bool SeenWell(Vector3 p, Vector3 her)
        {
            Camera cam = Camera.main;
            if (cam == null || cam.transform.forward.y > -1e-3f) return true;
            Transform t = cam.transform;
            Vector3 looksAt = t.position + t.forward * (t.position.y / -t.forward.y); // where its view meets the ground now
            Vector3 shift = new Vector3(her.x - looksAt.x, 0f, her.z - looksAt.z);      // as it will stand once it has followed her
            float h = InterfaceStyle.V.LayoutHeight, w = h * cam.aspect;
            foreach (float up in new[] { 0f, _targetHeight })
            {
                Vector3 v = cam.WorldToViewportPoint(p + new Vector3(0f, up, 0f) - shift);
                if (v.z <= 0f) return false;
                float x = v.x * w, y = (1f - v.y) * h;
                if (x < ViewSide || x > w - ViewSide || y < ViewTop || y > ViewBottom) return false;
            }
            return true;
        }

        // ---------------- The rings (the producer's addition) ----------------

        /// <summary>Metres from the statue's footprint to the ring's inner edge, the ring's width, its height over the floor, its brightness; and its segments.</summary>
        const float RingOutset = 0.3f, RingWidth = 0.08f, RingLift = 0.03f, RingGlow = 0.85f;
        const int RingSegments = 48;
        public const string RingName = "Ring";

        readonly List<GameObject> _rings = new List<GameObject>();
        Mesh _ringMesh;
        bool _ringsShown;

        /// <summary>
        /// One thin flat band on the floor round a statue's feet, in the interface's teal (the duck's rim's way of drawing a band, with her
        /// shot effects' unlit material in the colours of its vertices); its mesh made once for all the statues; hidden until a task needs it.
        /// </summary>
        void MakeRing(GameObject statue, float radius)
        {
            if (_ringMesh == null) _ringMesh = RingMesh(radius + RingOutset, RingWidth);
            var go = new GameObject(RingName);
            go.transform.SetParent(statue.transform, false);
            go.transform.localPosition = new Vector3(0f, RingLift, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = _player != null ? _player.ShotFxMaterial : null;
            r.enabled = r.sharedMaterial != null;
            go.SetActive(false);
            _rings.Add(go);
        }

        static Mesh RingMesh(float inner, float width)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Rgba teal = InterfaceStyle.V.Teal;
            Color c = new Color(teal.R, teal.G, teal.B);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
            c = new Color(c.r * RingGlow, c.g * RingGlow, c.b * RingGlow, 1f);
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(d * inner);
                vertices.Add(d * (inner + width));
                colors.Add(c);
                colors.Add(c);
            }
            for (int i = 0; i < RingSegments; i++)
            {
                int a = i * 2, b = ((i + 1) % RingSegments) * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1); // both windings: seen from above at any turn
                triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
            }
            var mesh = new Mesh { name = "PracticeTasks_TargetRing" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The rings on while the task on the plate waits for a hit on a target (and their switch is on), off otherwise; set only when that changes.</summary>
        void ShowRings(bool on)
        {
            on &= Values.TargetsWearARing;
            if (on == _ringsShown) return;
            _ringsShown = on;
            foreach (GameObject r in _rings) if (r != null) r.SetActive(on);
        }

        /// <summary>The rings show now (for tests and pictures).</summary>
        public bool RingsShown => _ringsShown;

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
