using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The practice room teaches a new player her kit, one task at a time (work order 133, 2026-11-25; a friend's first run: "Tutorial
    /// på charactern?"; Kristian ticked "Øvingsrom med oppgaver"), behind <see cref="PracticeTasksValues.On"/> (on; off, nothing of
    /// it is installed and the practice room is as before).
    ///
    /// The list is made from her kit as she has it when it begins (<see cref="PracticeTaskList"/>): move, shoot the dummy, dash, each
    /// starting skill by its own name and key (and its clean press, for one that has one), marks put on and spent, the duck behind a
    /// low prop of the project's own that is put down for that task only, focus, and the last plate, how to start a run. Each task is
    /// a line on a plate at the top of the screen, its keys as keys, done only by what came of her doing it, as the game reports it
    /// (<see cref="PlayerCharacter.Reported"/>, her walking measured on the ground): a key pressed while its skill cools down counts
    /// for nothing. A task done ticks; after a short beat the next comes. T skips a task, H ends them; with none running H starts them
    /// again, shown small in the room. Nothing pauses the game, nothing takes her keys or aims for her, no arrow, no sound.
    ///
    /// By itself only the first time she is in the practice room with nothing else up: once they were finished or ended a flag is
    /// saved in PlayerPrefs under <see cref="DoneKey"/> and they do not come back by themselves. The flag is read and written only by
    /// the one that installed itself at the start of Play (<see cref="UsesSavedFlag"/>): a test's keeps it in memory unless it says so.
    /// </summary>
    public sealed class PracticeTasks : MonoBehaviour
    {
        /// <summary>The saved flag: 1 once the tasks were finished or ended (work order 133).</summary>
        public const string DoneKey = "Roguelike.Practice.TasksDone";
        /// <summary>The key that skips the task on the plate (work order 133; free in every build).</summary>
        public const Key SkipKey = Key.T;
        /// <summary>The key that ends the tasks while they run, and starts them again while they do not (work order 133; free in every build).</summary>
        public const Key AgainKey = Key.H;
        /// <summary>The name of the low prop put down for the duck task.</summary>
        public const string PropName = "PracticeTasks_LowProp";
        /// <summary>The low prop of the duck task: a sarcophagus, the project's own low prop (a long low stone box, 1.9 by 1.1 m, 1 m high).</summary>
        public const PropModel DuckProp = PropModel.Sarcophagus;
        /// <summary>
        /// The basic shot keeps firing while its button is held: no such rule in the build work order 133 was cut from (the driver: "holding
        /// it never repeats"), so the task says click. Work order 132 adds hold to fire: at the merge this reads its rule.
        /// </summary>
        public static bool ShootKeepsFiring => false;

        /// <summary>The one running now, or null.</summary>
        public static PracticeTasks Current { get; private set; }

        /// <summary>The switch and the numbers (work order 133), in the Inspector while playing.</summary>
        public PracticeTasksValues Values = new PracticeTasksValues();
        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }
        /// <summary>The saved flag is read from and written to PlayerPrefs: true for the one that installed itself; a test sets it only round a save and restore.</summary>
        public bool UsesSavedFlag;
        /// <summary>A test's keyboard, read in place of the real one while <see cref="PlayerInputDriver.ReadRealDevices"/> is false.</summary>
        public Keyboard TestKeyboard { get; set; }

        /// <summary>The tasks running now, or null.</summary>
        public PracticeTaskRun Run { get; private set; }
        /// <summary>The last run of the tasks, kept after it is over (for tests and the readout).</summary>
        public PracticeTaskRun LastRun { get; private set; }
        /// <summary>The low prop of the duck task while it stands, or null.</summary>
        public GameObject PracticeProp => _prop;

        PlayerCharacter _player, _listening;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        GameObject _prop;
        Material _propMaterial;
        float _propTriedAt = float.NegativeInfinity;
        Vector3 _lastPos;
        bool _lastPosKnown;
        float _roomSeconds;
        bool _flagRead, _doneBefore;
        bool _wasAway;
        readonly Dictionary<Damageable, bool> _dummies = new Dictionary<Damageable, bool>();
        System.Action<PlayerReport> _onReport;

        /// <summary>Seconds between tries to put the low prop down when no place was found.</summary>
        const float PropRetrySeconds = 1f;
        /// <summary>Metres a frame's walk may be at most to count (a teleport or a new floor is not walking).</summary>
        const float MostWalkAFrame = 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            if (!new PracticeTasksValues().On) return; // the switch off: the practice room as before
            PracticeTasks t = Ensure();
            t.StartedByItself = true;
            t.UsesSavedFlag = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static PracticeTasks Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("PracticeTasks");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<PracticeTasks>();
            return Current;
        }

        void OnDestroy()
        {
            Listen(null);
            RemoveProp();
            if (Current == this) Current = null;
        }

        // ---------------- The flag ----------------

        /// <summary>The tasks were finished or ended before (the saved flag, read once when first asked; in memory for a test's).</summary>
        public bool DoneBefore
        {
            get
            {
                if (!_flagRead)
                {
                    _flagRead = true;
                    _doneBefore = UsesSavedFlag && PracticeTaskFlag.Read(PlayerPrefs.GetInt(DoneKey, 0));
                }
                return _doneBefore;
            }
            set
            {
                _flagRead = true;
                _doneBefore = value;
            }
        }

        void SaveFlag()
        {
            DoneBefore = true;
            if (!UsesSavedFlag) return;
            PlayerPrefs.SetInt(DoneKey, PracticeTaskFlag.Stored);
            PlayerPrefs.Save();
        }

        // ---------------- Where she is ----------------

        bool HasHer()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _driver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
                _lastPosKnown = false;
            }
            if (_listening != _player) Listen(_player);
            return _player != null;
        }

        void Listen(PlayerCharacter p)
        {
            if (_onReport == null) _onReport = OnReport;
            if (_listening != null) _listening.Reported -= _onReport;
            _listening = p;
            if (_listening != null) _listening.Reported += _onReport;
        }

        /// <summary>
        /// She is in the practice room with nothing else up: no floor, no run, no pause, no end screen, no choice, not the screen between
        /// runs or the lab's picker, not the start menu, not her sheet, not the benchmark; alive; no screen holding the input.
        /// </summary>
        public bool InPracticeRoomNow
        {
            get
            {
                if (!HasHer() || !_player.isActiveAndEnabled) return false;
                if (_health != null && !_health.IsAlive) return false;
                GeneratedLevels levels = GeneratedLevels.Current;
                if (levels != null && (levels.IsInLevel || levels.InRun || levels.Paused || levels.Ending != GeneratedLevels.EndKind.None
                                       || levels.Choice != GeneratedLevels.ChoiceKind.None || levels.BetweenRunsUp || levels.PickerOpen || levels.InLab)) return false;
                if (StartMenu.Holds || (RunHud.Current != null && RunHud.Current.SheetUp) || Benchmark.Current != null) return false;
                // Not the time scale: a hit's hit stop sets it to 0 for a moment (HitStop), and a press made in it must count.
                return !PlayerInputDriver.HeldByScreen && !PlayerInputDriver.HeldByChoice;
            }
        }

        /// <summary>The start menu is up, or a run or a floor began: she left the room for a run (the last plate's done).</summary>
        static bool LeftForARun()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            return StartMenu.Holds || (levels != null && (levels.InRun || levels.IsInLevel));
        }

        // ---------------- Starting, skipping, ending ----------------

        /// <summary>The tasks from the first, made from her kit and keys as they are now. False without her or with the switch off.</summary>
        public bool Begin()
        {
            if (!Values.On || !HasHer()) return false;
            RemoveProp();
            Run = new PracticeTaskRun(PracticeTaskList.Make(Values, KeysNow(), KitNow()));
            LastRun = Run;
            _lastPosKnown = false;
            _laid = false;
            return true;
        }

        /// <summary>The skip key: the next task at once. False with none running.</summary>
        public bool Skip()
        {
            if (Run == null || Run.Over) return false;
            Run.Skip();
            _laid = false;
            if (Run.Over) Over();
            return true;
        }

        /// <summary>The end key while they run: no more tasks, the flag saved. False with none running.</summary>
        public bool EndTasks()
        {
            if (Run == null || Run.Over) return false;
            Run.End();
            Over();
            return true;
        }

        void Over()
        {
            if (PracticeTaskFlag.Saves(Run)) SaveFlag();
            Run = null;
            RemoveProp();
            _laid = false;
        }

        /// <summary>The keys and ways as the bindings and the rules have them now.</summary>
        PracticeKeys KeysNow()
        {
            InputBindings b = _driver != null ? _driver.ActiveBindings : null;
            if (b == null) b = _fallback != null ? _fallback : (_fallback = ScriptableObject.CreateInstance<InputBindings>());
            FeelValues feel = _player != null ? _player.Feel : new FeelValues();
            PracticeDuck duck = b.TakeCover == Key.None || !feel.TakeCover ? PracticeDuck.None
                              : !feel.QuickDuck ? PracticeDuck.TakeCover
                              : HeldDuckRules.Held(feel) ? PracticeDuck.Hold : PracticeDuck.Tap;
            bool menu = StartMenu.Current != null;
            return new PracticeKeys
            {
                Up = Name(b.MoveUp), Left = Name(b.MoveLeft), Down = Name(b.MoveDown), Right = Name(b.MoveRight),
                Shoot = PracticeWords.KeyName(b.Shoot.Label), ShootHeld = ShootKeepsFiring,
                Dash = Name(b.Dash), DashToCursor = GameSettings.Dash(b.DashDirection) != DashDirection.TowardMovement,
                Cover = Name(b.TakeCover), Duck = duck,
                Focus = b.Focus != Key.None ? Name(b.Focus) : "",
                Start = menu ? StartKeyWithMenu : Name(GeneratedLevels.NewLevelKey), StartMenu = menu,
            };
        }

        InputBindings _fallback;

        /// <summary>The key that brings the start menu up in the practice room (<see cref="StartMenu"/>: Enter).</summary>
        public const string StartKeyWithMenu = "Enter";

        static string Name(Key k) => k == Key.None ? "" : PracticeWords.KeyName(k.ToString());

        /// <summary>Her starting skills on their keys, in key order (the ones a run starts with; all she has when none is marked so).</summary>
        List<PracticeSkill> KitNow()
        {
            var kit = new List<PracticeSkill>();
            if (_player == null) return kit;
            InputBindings b = _driver != null ? _driver.ActiveBindings : _fallback;
            IReadOnlyList<SkillDefinition> skills = _player.Skills;
            for (int pass = 0; pass < 2 && kit.Count == 0; pass++)
                for (int key = 0; key < _player.KeyCount; key++)
                {
                    int slot = _player.SkillOnKey(key);
                    if (slot < 0 || slot >= skills.Count || skills[slot] == null || !_player.IsEquipped(slot)) continue;
                    if (pass == 0 && !skills[slot].StartEquipped) continue;
                    kit.Add(PracticeSkill.Of(slot, skills[slot], PracticeWords.KeyName(b != null ? b.SkillLabel(key) : "")));
                }
            return kit;
        }

        // ---------------- Every frame ----------------

        void Update()
        {
            Keyboard keys = PlayerInputDriver.ReadRealDevices ? Keyboard.current : TestKeyboard;
            Tick(keys);
        }

        /// <summary>A frame's work (public for tests: a frame with nothing changing allocates nothing).</summary>
        public void Tick(Keyboard keys)
        {
            if (!Values.On)
            {
                if (Run != null) { Run = null; RemoveProp(); _laid = false; }
                return;
            }
            if (!HasHer()) return;
            // The last plate is done when she leaves for a run: the menu opened from the room, or a run begun.
            if (Run != null && !Run.Over && Run.Current != null && Run.Current.Kind == PracticeTaskKind.StartRun && !_wasAway && LeftForARun())
            {
                Run.Note(PracticeEvent.Of(PracticeEventKind.LeftForARun), Time.time);
                if (Run.Over) Over();
            }
            bool room = InPracticeRoomNow;
            _wasAway = !room && LeftForARun();
            _roomSeconds = room ? _roomSeconds + Time.deltaTime : 0f;
            if (!room)
            {
                _lastPosKnown = false;
                // Under the menu or a pause the duck task's prop stays where it is; it goes with a run or any other task.
                GeneratedLevels levels = GeneratedLevels.Current;
                bool inRun = levels != null && (levels.InRun || levels.IsInLevel);
                if (inRun || Run == null || Run.Current == null || Run.Current.Kind != PracticeTaskKind.Duck) RemoveProp();
                return;
            }
            if (Run == null && PracticeTaskFlag.BeginsByItself(Values, DoneBefore, false, true, _roomSeconds)) Begin();
            if (keys != null)
            {
                if (Run != null && keys[SkipKey].wasPressedThisFrame) Skip();
                else if (Run != null && keys[AgainKey].wasPressedThisFrame) EndTasks();
                else if (Run == null && keys[AgainKey].wasPressedThisFrame) Begin();
            }
            if (Run == null) return;
            Walk();
            Run.Step(Time.time, Values.TickBeat);
            if (Run.Over)
            {
                Over();
                return;
            }
            if (Run.Index != _laidIndex || Run.Ticked != _laidTicked) _laid = false;
            if (Run.Current != null && Run.Current.Kind == PracticeTaskKind.Duck) EnsureProp();
            else RemoveProp();
        }

        /// <summary>Her walk by the move keys, measured on the ground while she moves (a dash, a hop, a slide or cover is not walking).</summary>
        void Walk()
        {
            Vector3 at = _player.Position;
            PracticeTask t = Run.Current;
            if (_lastPosKnown && t != null && t.Kind == PracticeTaskKind.Move && !Run.Ticked && _player.IsMoving)
            {
                float d = new Vector2(at.x - _lastPos.x, at.z - _lastPos.z).magnitude;
                if (d > 0f && d <= MostWalkAFrame)
                {
                    PracticeEvent e = PracticeEvent.Of(PracticeEventKind.Walked);
                    e.Metres = d;
                    Run.Note(e, Time.time);
                }
            }
            _lastPos = at;
            _lastPosKnown = true;
        }

        // ---------------- What she did, as the game reports it ----------------

        bool OnDummy(Damageable target)
        {
            if (target == null) return false;
            if (_dummies.TryGetValue(target, out bool dummy)) return dummy;
            if (_dummies.Count > 64) _dummies.Clear();
            dummy = target.GetComponent<TrainingDummy>() != null;
            _dummies[target] = dummy;
            return dummy;
        }

        void OnReport(PlayerReport r)
        {
            if (Run == null || Run.Over || Run.Ticked || !InPracticeRoomNow) return;
            PracticeEvent e;
            switch (r.Kind)
            {
                case PlayerReportKind.Hit:
                    e = PracticeEvent.Of(r.Slot < 0 ? PracticeEventKind.ShotHit : PracticeEventKind.SkillHit);
                    e.Slot = r.Slot;
                    e.OnDummy = OnDummy(r.Target);
                    break;
                case PlayerReportKind.DashStarted:
                    e = PracticeEvent.Of(PracticeEventKind.Dashed);
                    break;
                case PlayerReportKind.RecastPressed:
                    e = PracticeEvent.Of(PracticeEventKind.Recast);
                    e.Slot = r.Slot;
                    e.Shot = r.Shot;
                    e.Clean = r.Clean;
                    break;
                case PlayerReportKind.MarksSpent:
                    e = PracticeEvent.Of(PracticeEventKind.MarksSpent);
                    e.Slot = r.Slot;
                    e.Marks = r.Marks;
                    e.OnDummy = OnDummy(r.Target);
                    break;
                case PlayerReportKind.Ducked:
                    e = PracticeEvent.Of(PracticeEventKind.Ducked);
                    e.AtPracticeProp = _prop != null && (_player.DuckObject != null ? _player.DuckObject.gameObject == _prop : r.By == PropName);
                    break;
                case PlayerReportKind.CoverEntered:
                    e = PracticeEvent.Of(PracticeEventKind.TookCover);
                    e.AtPracticeProp = _prop != null && _player.CoverObject != null && _player.CoverObject.gameObject == _prop;
                    break;
                case PlayerReportKind.FocusPressed:
                    if (r.Result != IntentResult.Started) return;
                    e = PracticeEvent.Of(PracticeEventKind.FocusSpent);
                    break;
                default:
                    return;
            }
            if (Run.Note(e, Time.time))
            {
                _laid = false;
                if (Run.Over) Over();
            }
        }

        // ---------------- The low prop of the duck task ----------------

        void EnsureProp()
        {
            if (_prop != null || Time.time - _propTriedAt < PropRetrySeconds) return;
            _propTriedAt = Time.time;
            if (!FindPropPlace(out Vector3 at, out float yaw)) return;
            LevelProp p = LevelProp.Make(DuckProp, new LevelPoint(at.x, at.z), Mathf.RoundToInt(yaw), Rules.PropPlace.Floor);
            _propMaterial = PropFactory.MakeMaterial(DuckProp);
            _prop = PropFactory.Make(p, at, null, 0, _propMaterial, PropName);
            SceneManager.MoveGameObjectToScene(_prop, _player.gameObject.scene);
            _prop.AddComponent<Shelter>().LowProp = PropHeights.Low(DuckProp); // the duck works beside it; the sprint jumps it, as a level's
            // She is moved on the NavMesh, which the practice room has baked without it: it is cut out of it while it stands.
            if (_prop.TryGetComponent(out BoxCollider box))
            {
                var obstacle = _prop.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = box.center;
                obstacle.size = box.size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
        }

        void RemoveProp()
        {
            if (_prop != null)
            {
                _prop.SetActive(false);
                Destroy(_prop);
            }
            _prop = null;
            if (_propMaterial != null) Destroy(_propMaterial);
            _propMaterial = null;
            _propTriedAt = float.NegativeInfinity;
        }

        /// <summary>
        /// Where the low prop goes: <see cref="PracticeTasksValues.PropDistance"/> from her, up the screen first, then round her, on
        /// the NavMesh with a clear way from her, clear of every dummy; its long side across the way to her.
        /// </summary>
        bool FindPropPlace(out Vector3 at, out float yaw)
        {
            at = default;
            yaw = 0f;
            Vector3 her = _player.Position;
            if (!NavMesh.SamplePosition(new Vector3(her.x, 0f, her.z), out NavMeshHit start, 2f, NavMesh.AllAreas)) return false;
            Camera cam = Camera.main;
            Vector3 up = cam != null ? new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z) : Vector3.forward;
            if (up.sqrMagnitude < 1e-6f) up = Vector3.forward;
            up.Normalize();
            TrainingDummy[] dummies = FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None);
            for (int i = 0; i < 8; i++)
            {
                float turn = (i + 1) / 2 * 45f * (i % 2 == 1 ? 1f : -1f); // 0, 45, -45, 90, -90, 135, -135, 180
                Vector3 dir = Quaternion.Euler(0f, turn, 0f) * up;
                Vector3 p = start.position + dir * Mathf.Max(1f, Values.PropDistance);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas)) continue;
                if (NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas)) continue;
                bool clear = true;
                foreach (TrainingDummy d in dummies)
                {
                    if (d == null) continue;
                    Vector3 dp = d.transform.position;
                    if (new Vector2(dp.x - hit.position.x, dp.z - hit.position.z).magnitude < Values.PropClearOfDummies) clear = false;
                }
                if (!clear) continue;
                at = new Vector3(hit.position.x, 0f, hit.position.z);
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        // ---------------- The plate ----------------

        /// <summary>The GUI depth it draws at: over the world's signs, under the screens (which it does not draw beside).</summary>
        public const int Depth = 2;
        /// <summary>The plate's top and height in the 1080 layout: a fixed quiet place at the top, clear of her and of the skill strip.</summary>
        public const float PlateTop = 92f, PlateHeight = 70f;
        /// <summary>The plate's least and most width in the layout.</summary>
        public const float PlateLeast = 560f, PlateMost = 1180f;
        /// <summary>The plate's padding left and right of what is on it, the keys' height and gap, the line's and the small words' sizes.</summary>
        const float Pad = 26f, CapHeight = 32f, CapGap = 7f, WordsGap = 16f, FootGap = 9f, FootCap = 22f, CountWidth = 70f, DoneWidth = 96f;
        const int WordsSize = 24, HeadSize = 17, FootSize = 16, CapSize = 19, FootCapSize = 15, CountSize = 21, DoneSize = 19;

        bool _laid;
        int _laidIndex = -1;
        bool _laidTicked;
        Vector2Int _laidAt;
        float _viewWidth;
        string _step = "", _count = "";
        float _countShown = -1f;
        readonly List<float> _capW = new List<float>();
        float _wordsW, _footSkipW, _footEndW, _againW;

        /// <summary>The plate's rectangle in the 1080 layout while it shows (empty when not), and the small line's in the room.</summary>
        public Rect PlateRect { get; private set; }
        public Rect AgainRect { get; private set; }
        /// <summary>The plate shows now, and the small line that starts the tasks again.</summary>
        public bool PlateShown => Values.On && Run != null && !Run.Over && InPracticeRoomNow;
        public bool AgainShown => Values.On && Run == null && InPracticeRoomNow;

        static float ViewWidth => InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);

        void LayOut()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_laid && _laidAt == at) return;
            _laid = true;
            _laidAt = at;
            _viewWidth = ViewWidth;
            GUIStyle cap = InterfaceStyle.Text(true, CapSize, TextAnchor.MiddleCenter), footCap = InterfaceStyle.Text(true, FootCapSize, TextAnchor.MiddleCenter);
            GUIStyle foot = InterfaceStyle.Text(false, FootSize, TextAnchor.MiddleLeft);
            _footSkipW = Mathf.Max(FootCap * 1.1f, InterfaceStyle.Measure(SkipKey.ToString(), footCap).x + 12f) + 5f + InterfaceStyle.Measure(PracticeWords.Skip, foot).x;
            _footEndW = Mathf.Max(FootCap * 1.1f, InterfaceStyle.Measure(AgainKey.ToString(), footCap).x + 12f) + 5f + InterfaceStyle.Measure(PracticeWords.End, foot).x;
            _againW = Mathf.Max(FootCap * 1.1f, InterfaceStyle.Measure(AgainKey.ToString(), footCap).x + 12f) + 7f + InterfaceStyle.Measure(PracticeWords.Again, foot).x;
            AgainRect = new Rect((_viewWidth - _againW) * 0.5f, PlateTop, _againW, FootCap);
            _capW.Clear();
            PracticeTask t = Run != null ? Run.Current : null;
            _laidIndex = Run != null ? Run.Index : -1;
            _laidTicked = Run != null && Run.Ticked;
            _countShown = -1f; // the count's words made again for this task's need
            if (t == null)
            {
                PlateRect = Rect.zero;
                return;
            }
            _step = PracticeWords.Head + "   " + InterfaceStyle.Number(Run.Index + 1) + " / " + InterfaceStyle.Number(Run.Tasks.Count);
            float keys = 0f;
            foreach (string k in t.Keys)
            {
                float w = Mathf.Max(CapHeight * 1.15f, InterfaceStyle.Measure(k, cap).x + 18f);
                _capW.Add(w);
                keys += w + CapGap;
            }
            _wordsW = InterfaceStyle.Measure(t.Words, InterfaceStyle.Text(false, WordsSize, TextAnchor.MiddleLeft)).x;
            float width = Pad + keys + WordsGap + _wordsW + WordsGap + Mathf.Max(t.ShowsCount ? CountWidth : 0f, DoneWidth) + Pad;
            float left = LeftmostX(InterfaceStyle.V);
            width = Mathf.Clamp(width, PlateLeast, Mathf.Min(PlateMost, _viewWidth - left - Pad));
            // In the middle of the screen, or as far right as it must be to stay clear of her health and focus at the top left.
            PlateRect = new Rect(Mathf.Max((_viewWidth - width) * 0.5f, left), PlateTop, width, PlateHeight);
        }

        /// <summary>Layout pixels the plate keeps from the top left plate (her health and focus), its own lean and outline included.</summary>
        const float ClearOfTopPlate = 44f;

        /// <summary>The leftmost the plate may stand: right of the top left plate while it is drawn (the skin and it on), else the padding.</summary>
        static float LeftmostX(InterfaceValues v) =>
            InterfaceStyle.Kinetic && v.TopPlate ? InterfacePlate.Whole(v).Right + ClearOfTopPlate : Pad;

        void OnGUI()
        {
            if (!Values.On || Event.current == null) return;
            bool plate = PlateShown, again = !plate && AgainShown;
            if (!plate && !again) return;
            GUI.depth = Depth;
            Color colorBefore = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 before);
            LayOut();
            InterfaceValues v = InterfaceStyle.V;
            if (plate) DrawPlate(v);
            else DrawAgain(v);
            GUI.matrix = before;
            GUI.color = colorBefore;
        }

        void DrawPlate(InterfaceValues v)
        {
            PracticeTask t = Run.Current;
            if (t == null) return;
            bool ticked = Run.Ticked;
            Rect r = PlateRect;
            float pivot = r.center.y, slant = v.Slant;
            // The menus' plate: the inked outline (white, teal once done), a black face, a teal cut down its left edge, dots toward its right.
            InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, ticked ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White, 0.85f), 0.8f);
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.92f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, 6f, r.height), pivot, slant, InterfaceStyle.C(v.Teal));
            InterfaceStyle.DotsAlong(new Rect(r.x + r.width * 0.72f, r.y, r.width * 0.28f, r.height), pivot, slant, 0.15f, InterfaceStyle.C(v.Teal, 0.16f));
            // The head over its top left: the word and which task of how many, small.
            Label(new Rect(r.x + Pad, r.y - HeadSize * 1.45f - 2f, r.width, HeadSize * 1.45f), _step, InterfaceStyle.Text(true, HeadSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal), true);
            // The keys as keys, then the line.
            float x = r.x + Pad, mid = r.y + r.height * 0.5f;
            GUIStyle cap = InterfaceStyle.Text(true, CapSize, TextAnchor.MiddleCenter);
            for (int i = 0; i < t.Keys.Length && i < _capW.Count; i++)
            {
                KeyCap(new Rect(x, mid - CapHeight * 0.5f, _capW[i], CapHeight), t.Keys[i], cap, v, ticked);
                x += _capW[i] + CapGap;
            }
            x += WordsGap - CapGap;
            Label(new Rect(x, r.y, _wordsW + 8f, r.height), t.Words, InterfaceStyle.Text(false, WordsSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White, ticked ? 0.6f : 1f), true);
            if (ticked)
            {
                // Done: a teal stamp at its right end, black words, the red double slash after it.
                var stamp = new Rect(r.xMax - Pad - DoneWidth + 8f, mid - 15f, DoneWidth - 12f, 30f);
                InterfaceStyle.HeavyPlate(stamp, stamp.center.y, slant, 2f, 0f);
                InterfaceStyle.Slanted(stamp, InterfaceStyle.C(v.Teal));
                Label(stamp, PracticeWords.Done, InterfaceStyle.Text(true, DoneSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black), false);
            }
            else if (t.ShowsCount)
            {
                int done = Mathf.FloorToInt(Run.Progress + 1e-4f);
                if (done != (int)_countShown)
                {
                    _countShown = done;
                    _count = InterfaceStyle.Number(done) + " / " + InterfaceStyle.Number(Mathf.RoundToInt(t.Need));
                }
                Label(new Rect(r.xMax - Pad - CountWidth, r.y, CountWidth, r.height), _count, InterfaceStyle.Text(true, CountSize, TextAnchor.MiddleRight), InterfaceStyle.C(v.Teal), true);
            }
            // The two keys small under its right end: skip, end.
            float fy = r.yMax + FootGap + v.HeavyOutline;
            float fx = r.xMax - _footSkipW - 18f - _footEndW;
            QuietBack(new Rect(fx, fy, _footSkipW + 18f + _footEndW, FootCap), v);
            FootKey(fx, fy, SkipKey.ToString(), PracticeWords.Skip, v);
            FootKey(fx + _footSkipW + 18f, fy, AgainKey.ToString(), PracticeWords.End, v);
        }

        void DrawAgain(InterfaceValues v)
        {
            Rect r = AgainRect;
            QuietBack(r, v);
            FootKey(r.x, r.y, AgainKey.ToString(), PracticeWords.Again, v, 0.8f);
        }

        /// <summary>The faint slanted back of the prompts (<see cref="InterfaceStyle.Prompt"/>'s) behind a small line, so it reads on a light floor.</summary>
        static void QuietBack(Rect r, InterfaceValues v) =>
            InterfaceStyle.Slanted(new Rect(r.x - 6f, r.y - 4f, r.width + 16f, r.height + 8f), InterfaceStyle.C(v.Back));

        /// <summary>A small key and its word, quiet: the menus' key cap, smaller.</summary>
        static void FootKey(float x, float y, string key, string word, InterfaceValues v, float alpha = 0.85f)
        {
            GUIStyle cap = InterfaceStyle.Text(true, FootCapSize, TextAnchor.MiddleCenter);
            float w = Mathf.Max(FootCap * 1.1f, InterfaceStyle.Measure(key, cap).x + 12f);
            var c = new Rect(x, y, w, FootCap);
            InterfaceStyle.HeavyPlate(c, c.center.y, v.Slant, 1.5f, 0f);
            InterfaceStyle.Slanted(c, InterfaceStyle.C(v.White, alpha));
            Label(c, key, cap, InterfaceStyle.C(v.Black), false);
            Label(new Rect(x + w + 5f, y - 2f, 200f, FootCap + 4f), word, InterfaceStyle.Text(false, FootSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White, alpha), true);
        }

        /// <summary>A key cap: white with black letters, the menus' own (teal once the task is done).</summary>
        static void KeyCap(Rect cap, string key, GUIStyle style, InterfaceValues v, bool done)
        {
            InterfaceStyle.HeavyPlate(cap, cap.center.y, v.Slant, 2f, 0f);
            InterfaceStyle.Slanted(cap, done ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
            Label(cap, key, style, InterfaceStyle.C(v.Black), false);
        }

        // Words drawn with a content each (as the menus' are): made once per text, none in a frame that changes nothing.
        static readonly Dictionary<string, GUIContent> s_Words = new Dictionary<string, GUIContent>(System.StringComparer.Ordinal);

        static void Label(Rect r, string text, GUIStyle style, Color c, bool shadow)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!s_Words.TryGetValue(text, out GUIContent content))
            {
                if (s_Words.Count >= 256) s_Words.Clear();
                content = new GUIContent(text);
                s_Words[text] = content;
            }
            InterfaceStyle.Label(r, content, style, c, shadow);
        }

        // ---------------- Measured, for tests ----------------

        /// <summary>The layout's work for a frame with the plate up (for the tests of a frame with nothing changing: it allocates nothing).</summary>
        public void LayOutForTests() => LayOut();

        /// <summary>
        /// What of the plate's words does not fit inside it at the screen now, as words; empty when everything fits (the order: "text cut
        /// off"). Every task of the list, not only the one on the plate.
        /// </summary>
        public string TextOutside()
        {
            var problems = new List<string>();
            PracticeTaskRun run = Run ?? LastRun;
            if (run == null) return "no tasks";
            float view = ViewWidth, most = Mathf.Min(PlateMost, view - LeftmostX(InterfaceStyle.V) - Pad);
            GUIStyle cap = InterfaceStyle.Text(true, CapSize, TextAnchor.MiddleCenter), words = InterfaceStyle.Text(false, WordsSize, TextAnchor.MiddleLeft);
            foreach (PracticeTask t in run.Tasks)
            {
                float keys = 0f;
                foreach (string k in t.Keys)
                {
                    Vector2 s = InterfaceStyle.Measure(k, cap);
                    float w = Mathf.Max(CapHeight * 1.15f, s.x + 18f);
                    if (s.y > CapHeight * 1.1f) problems.Add("the key " + k + " is taller than its cap");
                    keys += w + CapGap;
                }
                Vector2 line = InterfaceStyle.Measure(t.Words, words);
                float need = Pad + keys + WordsGap + line.x + WordsGap + Mathf.Max(t.ShowsCount ? CountWidth : 0f, DoneWidth) + Pad;
                if (need > most) problems.Add("\"" + t.Words + "\" (" + need.ToString("0") + " in " + most.ToString("0") + ")");
                if (line.y > PlateHeight) problems.Add("\"" + t.Words + "\" is taller than the plate");
            }
            return string.Join("; ", problems);
        }
    }
}
