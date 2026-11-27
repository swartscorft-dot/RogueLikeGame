using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The caller (work order 137; Kristian's tick of 2026-11-25, "Ja, fra etasje 2"; the rules in <see cref="RoomCallerRules"/>, the numbers
    /// in <see cref="LevelValues.CallerRules"/>, the switch <see cref="LevelValues.Callers"/> with the Trials page's "Callers").
    ///
    /// As a floor's fights are given out (<see cref="SetUpRoomFights"/>, before its waves are split), each room the rules give a caller
    /// (<see cref="RoomCallerRules.Rooms"/>) loses the partners that give way for it (<see cref="RoomCallerRules.GiveWay"/>, the last the floor
    /// placed, taken out of the floor before anything woke) and gets its caller at its place (the first of its drawn places the NavMesh holds):
    /// one of the room's own enemies, so it holds the doors shut, counts in "enemies left" and in wave 1 of a room with waves, and the room is
    /// cleared only once it is dead too.
    ///
    /// Its calls: when one is due (<see cref="EnemyCaller.BeginCall"/>) as many as may come (<see cref="RoomCallerRules.CallSize"/>: three, never
    /// past six alive) are given points by work order 136's arrival rule (<see cref="RoomWaveRules.Pick"/>: on his screen inside the ranged
    /// rule's margin, 7 to 11 m from her, never nearer than 3, never below her on his screen, apart from the others, on the NavMesh with a path
    /// to her) and a disc each (<see cref="PlaceArrival"/>, the waves' own); its tell runs; at its end a swarmer stands awake on each disc
    /// (<see cref="EnemyCaller.EndCall"/>), of the floor's health and damage, one of the floor's called (<see cref="GeneratedLevel.Join"/>:
    /// not of its room's own, so no door, no clearing, no "enemies left", no reward and no wave's "half dead" waits for it or counts it).
    /// A tell cut short takes its discs away and brings none.
    ///
    /// When it dies (<see cref="EnemyCaller.Fell"/>, at the killing hit) every one of its called still alive falls at once, the game's
    /// ordinary death (a hit past its health from no one), not counted as her kill in the run's record; their marks go with them as at any
    /// death (Pass It On passes marks only on her own kills). The recorder hears each call and the caller's end.
    ///
    /// Off (the switch, the page's line, the room fights off): nothing here does anything, and every floor is as on work order 136.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>A hit past any health: a called swarmer falling with its caller (as the act's end makes the boss's called fall).</summary>
        const int FallHit = 1000000;

        /// <summary>One room's caller and what it called (work order 137).</summary>
        public sealed class RoomCaller
        {
            public LevelRoom Room;
            public RoomFight Fight;
            public EnemyCaller Caller;
            /// <summary>Where it stands (on the NavMesh).</summary>
            public Vector3 Place;
            /// <summary>The room's partners taken out for it, and their kind.</summary>
            public int GaveWay;
            public EnemyKind GaveWayKind;
            /// <summary>Every swarmer it called, dead ones too.</summary>
            public readonly List<Enemy> Called = new List<Enemy>();
            /// <summary>The discs of the call being told.</summary>
            public readonly List<WaveArrival> Discs = new List<WaveArrival>();
            /// <summary>Every disc it put down, for the tests (its point, when it began).</summary>
            public readonly List<WaveArrival> AllDiscs = new List<WaveArrival>();
            /// <summary>Calls due so far (with a tell or brought by none), and the swarmers that stood.</summary>
            public int Calls, Came;
            /// <summary>Of its called, those that fell with it.</summary>
            public int Fell;
            /// <summary>True once its end is told (its death, or its fight over with it alive).</summary>
            public bool Ended;
            internal float CallBegan;
            internal int CallAlive;
            internal bool FightSeenOn;
            internal List<LevelPoint> Points;
            internal Vector3[] OnMesh;
            internal sbyte[] OnMeshKnown; // 0 not asked, 1 on it, -1 not
            internal LevelRandom Rng;

            /// <summary>Its called alive now.</summary>
            public int CalledAlive
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Called.Count; i++) if (Called[i] != null && !Called[i].IsDead) n++;
                    return n;
                }
            }
        }

        readonly List<RoomCaller> _roomCallers = new List<RoomCaller>();
        readonly List<LevelPoint> _callTaken = new List<LevelPoint>();
        NavMeshPath _callPath;
        EnemyBrute _callTemplate;
        bool _callersOn;

        /// <summary>True when this floor was set up with callers (work order 137): its switch and the Trials page's line both on.</summary>
        public bool CallersOnThisFloor => _callersOn;
        /// <summary>The rooms of this floor that hold a caller, in the order of the rooms.</summary>
        public IReadOnlyList<RoomCaller> RoomCallerList => _roomCallers;
        /// <summary>A room's caller, or null.</summary>
        public RoomCaller CallerOf(LevelRoom room)
        {
            if (room == null) return null;
            for (int i = 0; i < _roomCallers.Count; i++) if (_roomCallers[i].Room == room) return _roomCallers[i];
            return null;
        }

        RoomCaller Of(EnemyCaller caller)
        {
            for (int i = 0; i < _roomCallers.Count; i++) if (_roomCallers[i].Caller == caller) return _roomCallers[i];
            return null;
        }

        /// <summary>From <see cref="SetUpRoomFights"/>, the fights given out and before the waves: each caller's room gives way for it, and it stands.</summary>
        void SetUpRoomCallers()
        {
            ForgetRoomCallers();
            _callersOn = _level != null && _fightList.Count > 0 && RoomTrialSettings.CallersInUse(Values);
            if (!_callersOn) return;
            LevelLayout layout = _level.Layout;
            List<LevelRoom> rooms = RoomCallerRules.Rooms(layout, Values);
            if (rooms.Count == 0) return;
            _callTemplate = PracticeEnemy();
            if (_callTemplate == null) return;
            CallerValues c = Values.CallerRules;
            float within = Values.WaveRules != null ? Values.WaveRules.OnNavMeshWithin : 0.3f;
            var partners = new List<Enemy>();
            foreach (LevelRoom room in rooms)
            {
                RoomFight fight = FightOf(room);
                if (fight == null || !_level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) continue;
                bool found = false;
                Vector3 at = default;
                foreach (LevelPoint p in RoomCallerRules.PlacesInOrder(layout, room, Values))
                {
                    if (!NavMesh.SamplePosition(_level.ToWorld(p), out NavMeshHit hit, within, NavMesh.AllAreas) || _level.RoomAt(hit.position) != room) continue;
                    at = hit.position;
                    found = true;
                    break;
                }
                if (!found) continue;
                // The partners that give way: the last the floor placed of the line-up's partner kind (never the elite).
                EnemyKind partner = room.LineUp.Partner;
                partners.Clear();
                foreach (Enemy e in list) if (e != null && !e.IsElite && e.Kind == partner) partners.Add(e);
                int give = RoomCallerRules.GiveWay(partners.Count, partner, c, Values.LineUpRules);
                for (int i = 0; i < give; i++) _level.Leave(partners[partners.Count - 1 - i]);
                var caller = EnemyFactory.Make(EnemyKind.Caller, at, _level.transform, _callTemplate, "Caller_" + room.Letter + room.Order) as EnemyCaller;
                if (caller == null) continue;
                caller.UseValues(c);
                caller.HealthOverride = Values.HealthOf(EnemyKind.Caster); // the caster's health for the floor (the order)
                _level.Join(caller, room, roomsOwn: true);
                caller.Fight = fight;
                caller.BeginCall = BeginCall;
                caller.EndCall = EndCall;
                caller.CancelCall = CancelCall;
                caller.Fell += CallerFell;
                _roomCallers.Add(new RoomCaller { Room = room, Fight = fight, Caller = caller, Place = at, GaveWay = give, GaveWayKind = partner });
            }
        }

        /// <summary>The floor goes: its callers with it (their discs and their called go with its root).</summary>
        void ForgetRoomCallers()
        {
            _roomCallers.Clear();
            _callTaken.Clear();
            _callTemplate = null;
            _callersOn = false;
        }

        /// <summary>
        /// A call is due (work order 137): as many as may come (three, never past six alive) are given points by the waves' arrival rule and
        /// their discs. Returns how many discs went down (0: no call now, told to the recorder as "none").
        /// </summary>
        int BeginCall(EnemyCaller caller)
        {
            RoomCaller rc = Of(caller);
            if (rc == null || _level == null || _player == null || rc.Ended) return 0;
            float now = Time.time;
            rc.Calls++;
            int alive = rc.CalledAlive;
            int want = RoomCallerRules.CallSize(alive, Values.CallerRules);
            int put = want > 0 ? PutDiscs(rc, caller, want, now) : 0;
            if (put <= 0)
            {
                NoteCall(rc, "none", now, 0, 0, alive, "");
                return 0;
            }
            rc.CallBegan = now;
            rc.CallAlive = alive;
            return put;
        }

        /// <summary>The discs of a call at as many good points of the room as there are, at most <paramref name="want"/> (136's rule, <see cref="RoomWaveRules.Pick"/>).</summary>
        int PutDiscs(RoomCaller rc, EnemyCaller caller, int want, float now)
        {
            LevelLayout layout = _level.Layout;
            RoomWaveValues rules = Values.WaveRules ?? new RoomWaveValues();
            Vector3 herWorld = _player.Position;
            LevelPoint her = _level.ToLevel(herWorld);
            Camera cam = Camera.main;
            GroundQuad view = InLevel(ViewOfTheGround(cam, 0f)); // the camera's view in the floor's own metres, as the room's points are
            double upX = 0, upZ = 1; // with no camera, the follow camera's: looking north
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                if (f.x * f.x + f.z * f.z > 1e-6f)
                {
                    upX = f.x;
                    upZ = f.z;
                }
            }
            float margin = Values.RangedAround != null ? Values.RangedAround.ScreenMargin : 0f;
            if (rc.Points == null)
            {
                rc.Points = RoomWaveRules.RoomPoints(layout, rc.Room, Values, rules.PointStep);
                rc.OnMesh = new Vector3[rc.Points.Count];
                rc.OnMeshKnown = new sbyte[rc.Points.Count];
                rc.Rng = LevelRandom.Stream(layout.Seed, RoomCallerRules.Stage, 2000 + rc.Room.Order);
            }
            // Apart from every living enemy of the room in play, its called, and the waves' arrivals of the moment.
            _callTaken.Clear();
            AddTaken(_level.EnemiesByRoom, rc.Room.Order);
            AddTaken(_level.CalledByRoom, rc.Room.Order);
            for (int i = 0; i < rc.Called.Count; i++)
                if (rc.Called[i] != null && !rc.Called[i].IsDead && rc.Called[i].isActiveAndEnabled) _callTaken.Add(_level.ToLevel(rc.Called[i].transform.position));
            RoomWaves w = WavesOf(rc.Room);
            if (w != null) for (int i = 0; i < w.Arriving.Count; i++) _callTaken.Add(_level.ToLevel(w.Arriving[i].At));
            bool herOnMesh = NavMesh.SamplePosition(herWorld, out NavMeshHit herHit, HerOnNavMeshWithin, NavMesh.AllAreas);
            if (_callPath == null) _callPath = new NavMeshPath();
            bool Reachable(int i)
            {
                if (rc.OnMeshKnown[i] == 0)
                {
                    bool on = NavMesh.SamplePosition(_level.ToWorld(rc.Points[i]), out NavMeshHit hit, rules.OnNavMeshWithin, NavMesh.AllAreas)
                              && _level.RoomAt(hit.position) == rc.Room;
                    rc.OnMeshKnown[i] = (sbyte)(on ? 1 : -1);
                    if (on) rc.OnMesh[i] = hit.position;
                }
                if (rc.OnMeshKnown[i] < 0) return false;
                return !herOnMesh || (NavMesh.CalculatePath(herHit.position, rc.OnMesh[i], NavMesh.AllAreas, _callPath) && _callPath.status == NavMeshPathStatus.PathComplete);
            }
            List<int> kept = RoomWaveRules.Pick(rc.Points, want, her, view, margin, upX, upZ, _callTaken, rules, rc.Rng, Reachable);
            for (int k = 0; k < kept.Count; k++)
            {
                Vector3 at = rc.OnMesh[kept[k]];
                var a = new WaveArrival { Enemy = null, Wave = rc.Calls, At = at, Since = now };
                a.Disc = PlaceArrival(caller, at, rules);
                rc.Discs.Add(a);
                rc.AllDiscs.Add(a);
            }
            return kept.Count;
        }

        void AddTaken(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list)) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead && list[i].isActiveAndEnabled) _callTaken.Add(_level.ToLevel(list[i].transform.position));
        }

        /// <summary>The call's tell is over: a swarmer stands awake on each of its discs, one of the floor's called.</summary>
        void EndCall(EnemyCaller caller)
        {
            RoomCaller rc = Of(caller);
            if (rc == null || _level == null) return;
            int asked = rc.Discs.Count, came = 0;
            int floor = Floor > 0 ? Floor : _level.Layout.Floor;
            var ids = new StringBuilder();
            for (int i = 0; i < rc.Discs.Count; i++)
            {
                WaveArrival a = rc.Discs[i];
                if (a.Disc != null) Destroy(a.Disc.gameObject);
                a.Disc = null;
                if (_callTemplate == null) continue;
                Enemy e = EnemyFactory.Make(EnemyKind.Swarmer, a.At, _level.transform, _callTemplate,
                                            "Called_" + rc.Room.Letter + rc.Room.Order + "_" + rc.Calls + "_" + i);
                if (e == null) continue;
                e.HealthOverride = Values.HealthOf(EnemyKind.Swarmer);
                e.TakesTurns = true; // light: never held back, but one of the floor's (work order 023), as the boss's called
                e.DamageScale = Values.DamageScaleOf(floor); // work order 030, lever B: as the floor's own
                _level.Join(e, rc.Room, roomsOwn: false);
                e.Fight = rc.Fight; // its caller's room's fight (after Join: the boss's called are given the boss's)
                e.Provoke();
                a.Enemy = e;
                rc.Called.Add(e);
                came++;
                if (ids.Length > 0) ids.Append(';');
                ids.Append(e.Id.ToString(CultureInfo.InvariantCulture));
            }
            rc.Discs.Clear();
            rc.Came += came;
            NoteCall(rc, "came", rc.CallBegan, asked, came, rc.CallAlive, ids.ToString());
        }

        /// <summary>The call's tell was cut short: its discs go and none comes.</summary>
        void CancelCall(EnemyCaller caller)
        {
            RoomCaller rc = Of(caller);
            if (rc == null || rc.Discs.Count == 0) return;
            int asked = rc.Discs.Count;
            ClearDiscs(rc);
            NoteCall(rc, "cut", rc.CallBegan, asked, 0, rc.CallAlive, "");
        }

        static void ClearDiscs(RoomCaller rc)
        {
            for (int i = 0; i < rc.Discs.Count; i++)
                if (rc.Discs[i].Disc != null) Destroy(rc.Discs[i].Disc.gameObject);
            rc.Discs.Clear();
        }

        /// <summary>At the hit that killed it: its call cut, and every one of its called still alive falls at once.</summary>
        void CallerFell(EnemyCaller caller) => Fall(Of(caller));

        void Fall(RoomCaller rc)
        {
            if (rc == null || rc.Ended) return;
            CancelCall(rc.Caller);
            rc.Ended = true;
            int fell = 0;
            for (int i = 0; i < rc.Called.Count; i++)
            {
                Enemy e = rc.Called[i];
                if (e == null || e.IsDead) continue;
                _diedWithTheAct.Add(e); // not her kill (the run's record), as the boss's called at the act's end
                e.Body.TakeHit(FallHit);
                fell++;
            }
            rc.Fell = fell;
            NoteEnd(rc, "died");
        }

        /// <summary>Every frame, after the fights are stepped and before the waves and the doors (work order 137). Allocates nothing while nothing changes.</summary>
        void TickRoomCallers()
        {
            if (_roomCallers.Count == 0 || _level == null) return;
            float now = Time.time;
            float tell = Values.CallerRules != null ? Values.CallerRules.TellSeconds : 1f;
            for (int r = 0; r < _roomCallers.Count; r++) // every frame: no LINQ, nothing made
            {
                RoomCaller rc = _roomCallers[r];
                for (int i = 0; i < rc.Discs.Count; i++)
                {
                    WaveArrival a = rc.Discs[i];
                    float share = tell > 0f ? Mathf.Clamp01((now - a.Since) / tell) : 1f;
                    if (a.Disc != null) a.Disc.widthMultiplier = Mathf.Lerp(DiscThin, DiscThick, share);
                }
                if (rc.Ended) continue;
                if (rc.Caller == null || rc.Caller.IsDead)
                {
                    Fall(rc); // a death its hit did not tell (a backup)
                    continue;
                }
                if (rc.Fight.On) rc.FightSeenOn = true;
                else if (rc.FightSeenOn)
                {
                    // Its fight ended with it alive (she died): told once.
                    rc.Ended = true;
                    NoteEnd(rc, "left");
                }
            }
        }

        void NoteCall(RoomCaller rc, string name, float began, int asked, int came, int alive, string ids)
        {
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null || _level == null) return;
            int floor = Floor > 0 ? Floor : _level.Layout.Floor;
            recorder.NoteCallerCall(name, floor, LevelGeometry.RoomNumber(_level.Layout, rc.Room) + 1, rc.Room.Order, rc.Caller != null ? rc.Caller.Id : 0,
                                    rc.Calls, began - rc.Fight.StartedAt, asked, came, alive, rc.Called.Count, ids);
        }

        void NoteEnd(RoomCaller rc, string name)
        {
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null || _level == null) return;
            int floor = Floor > 0 ? Floor : _level.Layout.Floor;
            EnemyCaller c = rc.Caller;
            float woke = rc.Fight.StartedAt;
            float firstHit = c != null && c.FirstHitAt >= 0f && woke > float.NegativeInfinity ? c.FirstHitAt - woke : -1f;
            float died = c != null && c.DiedAt >= 0f && woke > float.NegativeInfinity ? c.DiedAt - woke : -1f;
            recorder.NoteCallerEnd(name, floor, LevelGeometry.RoomNumber(_level.Layout, rc.Room) + 1, rc.Room.Order, c != null ? c.Id : 0, firstHit, died,
                                   c != null ? c.CallsBegun : 0, rc.Came, rc.Fell, rc.GaveWay, EnemyKinds.Word(rc.GaveWayKind));
        }

        /// <summary>For the tests: one step of the callers, as a frame takes it.</summary>
        public void TickRoomCallersForTests() => TickRoomCallers();
    }
}
