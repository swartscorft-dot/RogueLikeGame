using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The play start check (work order 096, 2026-11-23): starts the game the way Kristian starts it, and fails on what he met
    /// that morning (main <c>c7921e2</c>: her character's Awake threw, Unity switched her character off, she could not move or
    /// shoot and a dash never ended; no test and no build saw it, because the tests LOAD the scene by code and there the
    /// components wake in another order).
    ///
    ///   Unity.exe -batchmode -projectPath &lt;folder&gt; -executeMethod Roguelike.EditorTools.PlayStartCheck.Run -logFile &lt;log&gt; -playStartResult &lt;file&gt;
    ///
    /// (no -quit: the check ends Unity itself, with exit code 0 when it passed and 1 when it did not; the runner's
    /// run-playstart.ps1 does all this.) It OPENS <see cref="ScenePath"/> in the Editor, as he has it open, and enters Play mode
    /// with the project's own Enter Play Mode options as they are committed (it never changes them; it writes them down). For
    /// the length of Play, everything that installs itself at the start of Play in his Editor installs itself here too
    /// (<see cref="SessionRecordingRule.BatchModeAsAnEditor"/>: batch mode otherwise keeps them out, for the test suites).
    ///
    /// From the moment Play mode is asked for until it has ended, every log of type Exception, Error and Assert is collected,
    /// with its stack (Play is asked for once the batch Editor has settled, <see cref="SettleSeconds"/>; what its own start
    /// logs before that is written down apart and not counted). In Play: a moment in the practice room, then what F5 does
    /// (<see cref="GeneratedLevels.StartNewRun"/>; with what lasts between runs on, as on this machine, the screen between runs
    /// and its start, played on a copy of his save so nothing is earned or spent in his), and on floor 1 what a player does
    /// first, through the game's intents: runs four ways, shoots, dashes, sprints, presses each equipped skill, ducks at a low
    /// prop near her, walks through the door of the first room with a fight (so that it wakes and its doors shut), and opens and
    /// closes the pause menu. Her input driver is given no device frames (<see cref="PlayerInputDriver.InputSource"/>) once Play
    /// has started, so nothing at the computer steers her; she is made untouchable only from the room's fight on (a start check,
    /// not a fight; untouchable, she could not sprint, <see cref="MeleeYouReadRules.MayBeHeld"/>).
    ///
    /// It PASSES only if: no Exception, Error or Assert was logged; her <see cref="PlayerCharacter"/>,
    /// <see cref="PlayerHealth"/>, <see cref="PlayerInputDriver"/> and every other MonoBehaviour on her, and every MonoBehaviour
    /// on the scene's root objects and on what installed itself, is still enabled at the end (the ones the scene itself has
    /// switched off excepted); she moved more than <see cref="LeastMetresSteered"/> when steered; a basic shot went off when
    /// asked; the dash ended; the game's clock ran. It writes one result file: the verdict on its first line, what was done,
    /// each check and its value, and every log collected.
    ///
    /// Should domain reload ever be switched on, it still works: what it must remember across Play's start and end is kept in
    /// <see cref="SessionState"/>, as <see cref="BenchmarkInEditor"/> does.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayStartCheck
    {
        /// <summary>The scene he has open when he presses Play.</summary>
        public const string ScenePath = "Assets/Game/Scenes/ControlRoom.unity";
        /// <summary>The command line's argument naming the result file.</summary>
        public const string ResultArgument = "-playStartResult";
        /// <summary>Metres she must have gone while steered four ways (0.8 s each at her run speed is about 15 m in the open; walls take some).</summary>
        public const float LeastMetresSteered = 3f;
        /// <summary>Game seconds the clock must have run from Play's start to the check's end (the steps take about 20).</summary>
        public const float LeastGameSeconds = 10f;
        /// <summary>Metres within which a low prop counts as near her, to walk to and duck at.</summary>
        const float NearProp = 20f;
        /// <summary>Real seconds Play mode may take to start before the check gives up.</summary>
        const double EnterTimeout = 180.0;
        /// <summary>Real seconds the whole check may take, from Play asked for, before it gives up (the steps' own waits have real-time guards too).</summary>
        const double WholeTimeout = 420.0;

        // SessionState keys: it survives a domain reload, not an Editor restart.
        const string KPhase = "Roguelike.PlayStartCheck.Phase";       // 0 none, 4 the Editor settling, 1 Play asked for, 2 playing, 3 leaving Play
        const string KResult = "Roguelike.PlayStartCheck.Result";     // the result file's path
        const string KLogs = "Roguelike.PlayStartCheck.Logs";         // the logs collected before a domain reload
        const string KEnabled = "Roguelike.PlayStartCheck.Enabled";   // the components the scene has on, before Play
        const string KReport = "Roguelike.PlayStartCheck.Report";     // the steps' and checks' lines, made before Play ends
        const string KFaults = "Roguelike.PlayStartCheck.Faults";     // the faults of the steps' checks, in order, or empty
        const string KRoots = "Roguelike.PlayStartCheck.Roots";       // the scene's root objects' names, before Play
        const string KOptions = "Roguelike.PlayStartCheck.Options";   // the Enter Play Mode options in use
        const string KAsked = "Roguelike.PlayStartCheck.AskedAt";     // real time (ticks, UTC) Play was asked for
        const string KSessions = "Roguelike.PlayStartCheck.Sessions"; // the play session files there were before
        const string KBefore = "Roguelike.PlayStartCheck.Before";     // the logs of the Editor's own start, before Play was asked for (not counted)
        /// <summary>Real seconds the Editor is left to settle after the scene is opened, before Play is asked for: the batch Editor's own start-up work (its search index) runs in delayed calls in its first seconds and once threw while Play started (work order 096's first run); he presses Play in an Editor long started.</summary>
        const double SettleSeconds = 8.0;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly object s_lock = new object();
        static readonly List<string> s_logs = new List<string>();
        static readonly List<string> s_before = new List<string>();
        static volatile bool s_counting;
        static readonly Stopwatch s_clock = Stopwatch.StartNew();
        static IEnumerator s_steps;
        static bool s_stepping;

        static int Phase { get => SessionState.GetInt(KPhase, 0); set => SessionState.SetInt(KPhase, value); }

        static PlayStartCheck()
        {
            if (Phase == 0) return;
            // A domain reload in the middle of the check (only with domain reload switched on): take up again.
            SessionRecordingRule.BatchModeAsAnEditor = Phase == 1 || Phase == 2;
            s_counting = Phase != 4;
            string kept = SessionState.GetString(KLogs, "");
            lock (s_lock)
            {
                s_logs.Clear();
                if (kept.Length > 0) s_logs.AddRange(kept.Split(new[] { "\u001e" }, StringSplitOptions.None));
            }
            Listen();
        }

        static void Listen()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= KeepLogs;
            AssemblyReloadEvents.beforeAssemblyReload += KeepLogs;
        }

        static void KeepLogs()
        {
            lock (s_lock) SessionState.SetString(KLogs, string.Join("\u001e", s_logs));
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            string line = string.Format(Inv, "[{0} at {1:0.000} s real] {2}\n{3}", type, s_clock.Elapsed.TotalSeconds, message, (stack ?? "").TrimEnd());
            lock (s_lock) (s_counting ? s_logs : s_before).Add(line);
        }

        static string[] Logs()
        {
            lock (s_lock) return s_logs.ToArray();
        }

        static double RealSecondsSinceAsked() =>
            (DateTime.UtcNow - new DateTime(long.Parse(SessionState.GetString(KAsked, DateTime.UtcNow.Ticks.ToString(Inv)), Inv), DateTimeKind.Utc)).TotalSeconds;

        // ---------------- The start ----------------

        /// <summary>The command line's start (see the class). Also from the menu, in an Editor with a window: it then ends that Editor too.</summary>
        [MenuItem("Roguelike/Checks/Play start check (ends the Editor)")]
        public static void Run()
        {
            string project = Path.GetDirectoryName(Application.dataPath) ?? ".";
            string result = Path.Combine(project, "Temp", "PlayStart-result.txt");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == ResultArgument) result = args[i + 1];
            SessionState.SetString(KResult, result);
            SessionState.SetString(KReport, "");
            SessionState.SetString(KFaults, "");
            SessionState.SetString(KLogs, "");
            SessionState.SetString(KBefore, "");
            lock (s_lock)
            {
                s_logs.Clear();
                s_before.Clear();
            }
            s_counting = false;
            s_steps = null;

            // The options as committed: read, written down, never changed.
            string options = EditorSettings.enterPlayModeOptionsEnabled
                ? "Enter Play Mode options enabled: " + EditorSettings.enterPlayModeOptions + " (" + (int)EditorSettings.enterPlayModeOptions + ")"
                : "Enter Play Mode options disabled: domain and scene are reloaded";
            SessionState.SetString(KOptions, options);

            string sessions = PlaySessionRecorder.DefaultFolder();
            SessionState.SetString(KSessions, Directory.Exists(sessions) ? string.Join("|", Directory.GetFiles(sessions).Select(Path.GetFileName)) : "\u0000none");

            // The scene, open in the Editor as he has it (not loaded by code in Play).
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetString(KEnabled, string.Join("\n", EnabledInTheScene()));
            SessionState.SetString(KRoots, string.Join("\n", SceneManager.GetActiveScene().GetRootGameObjects().Select(r => r.name)));

            SessionState.SetString(KAsked, DateTime.UtcNow.Ticks.ToString(Inv)); // from here the settling's seconds; from Play asked for, Play's
            Phase = 4;
            Listen();
            Debug.Log("PlayStart: " + ScenePath + " open; " + options + "; Play is asked for once the Editor has settled");
        }

        /// <summary>The Editor has settled: Play is asked for, with the scene open, and from now every log counts.</summary>
        static void AskForPlay()
        {
            lock (s_lock) SessionState.SetString(KBefore, string.Join("\u001e", s_before));
            s_counting = true;
            SessionRecordingRule.BatchModeAsAnEditor = true;
            SessionState.SetString(KAsked, DateTime.UtcNow.Ticks.ToString(Inv));
            Phase = 1;
            Debug.Log("PlayStart: entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>The scene's MonoBehaviours that are on before Play, by their place: the hierarchy path, the type and its index among the same on the object.</summary>
        static List<string> EnabledInTheScene()
        {
            var keys = new List<string>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (b != null && b.enabled) keys.Add(KeyOf(b));
            return keys;
        }

        static string KeyOf(MonoBehaviour b)
        {
            var path = new StringBuilder(b.gameObject.name);
            for (Transform t = b.transform.parent; t != null; t = t.parent) path.Insert(0, t.name + "/");
            Type type = b.GetType();
            int index = Array.IndexOf(b.GetComponents(type), b);
            return path + ":" + type.Name + "#" + index;
        }

        // ---------------- Play ----------------

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (Phase == 0) return;
            if (change == PlayModeStateChange.EnteredPlayMode && Phase == 1)
            {
                Phase = 2;
                s_steps = Steps();
                s_stack.Clear();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && Phase >= 2) Finish();
        }

        static void Tick()
        {
            if (Phase == 0 || s_stepping) return;
            if (Phase == 4)
            {
                if (RealSecondsSinceAsked() >= SettleSeconds && !EditorApplication.isCompiling && !EditorApplication.isUpdating) AskForPlay();
                return;
            }
            if (Phase == 1 && RealSecondsSinceAsked() > EnterTimeout)
            {
                Fault("Play mode did not start within " + EnterTimeout.ToString("0", Inv) + " s");
                Phase = 3;
                Finish();
                return;
            }
            if (Phase == 3 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Finish(); // left Play without the state change reaching us (a domain reload in between)
                return;
            }
            if (Phase != 2 || !EditorApplication.isPlaying) return;
            if (s_steps == null)
            {
                // Play goes on but the steps were lost (a domain reload in the middle of Play): the check cannot go on honestly.
                Fault("the check's steps were lost in Play (a domain reload?)");
                Leave();
                return;
            }
            s_stepping = true;
            try
            {
                bool more;
                try { more = Step(); }
                catch (Exception e)
                {
                    Fault("the check's own step threw: " + e.GetType().Name + ": " + e.Message);
                    Note("the check's own step threw:\n" + e);
                    more = false;
                    try { Judge(); } catch (Exception e2) { Note("judging threw too: " + e2); }
                }
                if (!more) Leave();
                else if (RealSecondsSinceAsked() > WholeTimeout)
                {
                    Fault("the check took more than " + WholeTimeout.ToString("0", Inv) + " real seconds");
                    Leave();
                }
            }
            finally
            {
                s_stepping = false;
            }
        }

        /// <summary>One editor tick of the steps: as Unity runs a coroutine, a step that yields another enumerator runs that one to its end first.</summary>
        static bool Step()
        {
            if (s_stack.Count == 0) s_stack.Push(s_steps);
            while (s_stack.Count > 0)
            {
                IEnumerator top = s_stack.Peek();
                if (!top.MoveNext())
                {
                    s_stack.Pop();
                    continue; // the one below goes on in this same tick
                }
                if (top.Current is IEnumerator inner)
                {
                    s_stack.Push(inner);
                    continue;
                }
                return true; // a tick's wait
            }
            return false;
        }

        static readonly Stack<IEnumerator> s_stack = new Stack<IEnumerator>();

        static void Leave()
        {
            s_stack.Clear();
            s_steps = null;
            Phase = 3;
            EditorApplication.ExitPlaymode();
        }

        // ---------------- What it does in Play ----------------

        static PlayerCharacter s_her;
        static PlayerHealth s_health;
        static PlayerInputDriver s_driver;
        static GeneratedLevels s_levels;
        static float s_playStartedAt;
        static int s_framesAtStart;
        static float s_steered;          // metres gone while steered four ways
        static int s_shotsAsked, s_shotsBefore;
        static string s_dash = "";
        static bool s_dashEnded;

        static IEnumerator Steps()
        {
            s_playStartedAt = Time.time;
            s_framesAtStart = Time.frameCount;
            s_steered = 0f;
            s_shotsAsked = 0;
            s_dash = "not tried";
            s_dashEnded = false;

            s_her = Object.FindAnyObjectByType<PlayerCharacter>();
            if (s_her == null)
            {
                Fault("no PlayerCharacter in the scene");
                yield break;
            }
            s_health = s_her.GetComponent<PlayerHealth>();
            s_driver = s_her.GetComponent<PlayerInputDriver>();
            if (s_driver != null) s_driver.InputSource = () => null; // nothing at the computer steers her; the intents do
            Note(string.Format(Inv, "Play started: frame {0}, game time {1:0.000} s; her character {2}, health {3}, input driver {4}",
                               Time.frameCount, Time.time, OnOff(s_her), OnOff(s_health), OnOff(s_driver)));
            s_levels = GeneratedLevels.Current;
            Note("installed by itself at Play's start: " + string.Join(", ", InstalledByItself()));
            if (s_levels == null)
            {
                Fault("the game's floors (GeneratedLevels) did not install themselves: the check did not start the game as his Editor does");
                yield break;
            }

            // What lasts between runs is saved under the game's data folder, which his own Editor shares: the check plays on a copy
            // of his save beside the result file, so that it never earns or spends anything in his.
            string copy = Path.Combine(Path.GetDirectoryName(SessionState.GetString(KResult, "")) ?? ".", "PlayStart-between-runs.json");
            string his = GeneratedLevels.DefaultBetweenRunsSavePath;
            if (File.Exists(copy)) File.Delete(copy);
            if (File.Exists(his)) File.Copy(his, copy);
            GeneratedLevels.BetweenRunsSavePath = copy;
            Note("what lasts between runs: " + (s_levels.MetaOn ? "on" : "off") + "; played on a copy of the save (" + (File.Exists(his) ? "his, copied" : "he has none") + ") at " + copy);

            // A fresh Play: the practice room, no key.
            yield return GameSeconds(1f);
            Note(string.Format(Inv, "practice room: in a floor {0}, in a run {1}, she stands at {2}", s_levels.IsInLevel, s_levels.InRun, Flat(s_her.Position)));

            // One key: F5, a new run (GeneratedLevels.Update: NewLevelKey -> StartNewRun); with what lasts between runs on, that
            // opens the screen between runs, and its start begins the run (StartFromBetweenRuns).
            GeneratedLevel level = s_levels.StartNewRun();
            if (level == null && s_levels.BetweenRunsUp)
            {
                Note("F5's StartNewRun opened the screen between runs: its start is pressed");
                yield return Frames(2);
                level = s_levels.StartFromBetweenRuns();
            }
            if (s_health != null) s_health.Untouchable = false; // as a run has her (the screen between runs set it); she is made untouchable only for the room's fight below
            Note(level != null
                ? string.Format(Inv, "F5's StartNewRun: run {0}, floor {1} of {2} rooms, {3} enemies; she stands at {4}",
                                s_levels.RunSeedText, s_levels.Floor, level.Layout.Rooms.Count, level.Enemies.Count, Flat(s_her.Position))
                : "no floor after F5" + (s_levels.BetweenRunsUp ? " (the screen between runs is still up)" : ""));
            if (level == null)
            {
                Fault("F5 put her in no floor");
                yield break;
            }
            yield return GameSeconds(0.5f);

            // Runs four ways, as the screen has them (up, right, down, left), 0.8 s each.
            Camera cam = Camera.main;
            Vector3 up = cam != null ? Flatten(cam.transform.forward) : Vector3.forward;
            Vector3 right = cam != null ? Flatten(cam.transform.right) : Vector3.right;
            foreach (Vector3 way in new[] { up, right, -up, -right })
            {
                s_her.Steer(way, true);
                float until = Time.time + 0.8f;
                double guard = RealNow() + 10.0;
                Vector3 last = s_her.Position;
                while (Time.time < until && RealNow() < guard)
                {
                    yield return null;
                    s_steered += Flat(s_her.Position - last).magnitude;
                    last = s_her.Position;
                }
            }
            s_her.Stop();
            Note(string.Format(Inv, "steered four ways, 0.8 s each: {0:0.00} m gone", s_steered));
            yield return GameSeconds(0.2f);

            // Shoots three times toward a point ahead.
            s_shotsBefore = s_her.BasicShotsFired;
            for (int i = 0; i < 3; i++)
            {
                s_her.Shoot(s_her.Position + up * 6f);
                s_shotsAsked++;
                yield return GameSeconds(0.45f);
            }
            Note(string.Format(Inv, "shot asked {0} times: {1} basic shots went off", s_shotsAsked, s_her.BasicShotsFired - s_shotsBefore));

            // Dashes toward a point to her right, and waits for the dash to end.
            int dashesBefore = s_her.DashesStarted;
            IntentResult dash = s_her.DashToward(s_her.Position + right * 5f);
            float dashAsked = Time.time;
            yield return Frames(2);
            bool dashing = s_her.IsDashing;
            yield return Until(() => !s_her.IsDashing, 2f);
            s_dashEnded = s_her.DashesStarted > dashesBefore && !s_her.IsDashing;
            s_dash = string.Format(Inv, "{0}, started {1}, dashing right after {2}, still dashing {3} after {4:0.00} s", dash, s_her.DashesStarted - dashesBefore,
                                   dashing, s_her.IsDashing, Time.time - dashAsked);
            Note("dash: " + s_dash);
            yield return GameSeconds(0.3f);

            // Sprints: the key held and a move, 1.5 s.
            s_her.HoldSprint(true);
            s_her.Steer(-right, true);
            bool sprinted = false;
            {
                float until = Time.time + 1.5f;
                double guard = RealNow() + 10.0;
                while (Time.time < until && RealNow() < guard)
                {
                    yield return null;
                    sprinted |= s_her.IsSprinting;
                }
            }
            s_her.HoldSprint(false);
            s_her.Stop();
            Note("sprint held with a move for 1.5 s: sprinting seen " + sprinted);
            yield return GameSeconds(0.3f);

            // Presses each equipped skill toward a point ahead, holds it a moment, lets go, and waits for it to end.
            var skills = new List<string>();
            for (int slot = 0; slot < s_her.Skills.Count; slot++)
            {
                if (!s_her.IsEquipped(slot)) continue;
                int skillShotsBefore = s_her.SkillShotsFired;
                IntentResult r = s_her.PressSkill(slot, s_her.Position + up * 6f);
                yield return GameSeconds(0.3f);
                s_her.ReleaseSkill(slot);
                yield return Until(() => s_her.ActiveSkill == null && !s_her.IsLocked, 3f);
                skills.Add(string.Format(Inv, "{0} ({1}): {2}, skill shots {3}", slot, s_her.Skills[slot] != null ? s_her.Skills[slot].Name : "?", r, s_her.SkillShotsFired - skillShotsBefore));
                yield return GameSeconds(0.2f);
            }
            Note("skills pressed: " + (skills.Count > 0 ? string.Join("; ", skills) : "none equipped"));

            // Ducks at a low prop, if one is near: walks to it, holds the duck a second.
            Shelter prop = null;
            float nearest = NearProp;
            foreach (Shelter s in Shelter.All)
            {
                if (s == null || !s.LowProp || !s.isActiveAndEnabled) continue;
                float d = Flat(s.transform.position - s_her.Position).magnitude;
                if (d < nearest) { nearest = d; prop = s; }
            }
            if (prop != null)
            {
                yield return WalkTo(prop.transform.position, 1.2f, 6f);
                s_her.HoldCover(true);
                bool ducked = false;
                float until = Time.time + 1f;
                double guard = RealNow() + 10.0;
                while (Time.time < until && RealNow() < guard)
                {
                    yield return null;
                    ducked |= s_her.IsDucked;
                }
                s_her.HoldCover(false);
                Note(string.Format(Inv, "a low prop {0:0.0} m from her ({1}): walked to it, held the duck 1 s: ducked {2}", nearest, prop.name, ducked));
            }
            else Note(string.Format(Inv, "no low prop within {0:0} m of her: no duck tried", NearProp));

            // Walks through the door of the first room with a fight: it wakes and its doors shut.
            GeneratedLevels.RoomDoors doors = s_levels.RoomDoorList.Count > 0 ? s_levels.RoomDoorList[0] : null;
            if (doors != null && doors.Entry != null)
            {
                if (s_health != null) s_health.Untouchable = true; // the room's fight wakes: a start check, not a fight she may lose
                LevelRoom room = doors.Room;
                // Past the room's point, where its fight wakes (RoomFightRules.PastThePoint), by a metre and a half.
                double depth = RoomFightRules.PointDepth(level.Layout, room, doors.Entry, s_levels.Values.Fights) + 1.5;
                Vector3 inside = level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, doors.Entry, depth, 0.0));
                yield return WalkTo(inside, 0.6f, 15f, () => doors.Shut);
                s_her.Stop();
                yield return Until(() => doors.Shut, 5f);
                Note(string.Format(Inv, "walked {0:0.0} m into the room {1}{2} through its door (to {3:0.0} m of the point aimed at): she is in it {4}, its fight woke {5}, its doors shut {6} (closings {7})",
                                   depth, room.Letter, room.Order, Flat(inside - s_her.Position).magnitude, s_levels.CurrentRoom == room,
                                   doors.Fight != null && doors.Fight.On, doors.Shut, doors.Closings));
            }
            else Note("no room with doors on this floor: no door walked through");

            // Opens the pause menu and closes it.
            bool paused = s_levels.Pause();
            double pausedUntil = RealNow() + 0.5;
            while (RealNow() < pausedUntil) yield return null;
            bool stillPaused = s_levels.Paused;
            bool resumed = s_levels.Resume();
            yield return GameSeconds(0.5f);
            Note(string.Format(Inv, "pause menu: opened {0}, open after 0.5 real s {1}, closed {2}, time scale now {3}", paused, stillPaused, resumed, Time.timeScale));

            Judge();
        }

        /// <summary>The checks that are made in Play, at the end of the steps (the logs are judged once Play has ended).</summary>
        static void Judge()
        {
            float gameSeconds = Time.time - s_playStartedAt;
            int frames = Time.frameCount - s_framesAtStart;
            int shots = s_her != null ? s_her.BasicShotsFired - s_shotsBefore : 0;

            List<string> off = SwitchedOff();
            Check(off.Count == 0, "every MonoBehaviour on her, on the scene's roots and on what installed itself is on",
                  off.Count == 0 ? "all on" : off.Count + " off: " + string.Join(", ", off));
            Check(s_steered > LeastMetresSteered, "she moved when steered", string.Format(Inv, "{0:0.00} m (more than {1:0} m wanted)", s_steered, LeastMetresSteered));
            Check(s_shotsAsked > 0 && shots > 0, "a shot went off when asked", string.Format(Inv, "{0} of {1} asked", shots, s_shotsAsked));
            Check(s_dashEnded, "the dash ended", s_dash);
            Check(gameSeconds > LeastGameSeconds && frames > 0, "the game's clock ran",
                  string.Format(Inv, "{0:0.00} game seconds over {1} frames, time scale {2}", gameSeconds, frames, Time.timeScale));
        }

        /// <summary>
        /// The MonoBehaviours that are off at the end and should be on: her character, health and input driver always; any other
        /// that the scene had on before Play (on her, on its roots, anywhere in it); and every one on a root object made in Play
        /// (the generated floor, what installed itself). Not the one the game switches on and off itself: the developer's readout
        /// (<see cref="DebugReadout"/>, off while the developer's keys are off, <see cref="RunHud"/>).
        /// </summary>
        static List<string> SwitchedOff()
        {
            var off = new List<string>();
            var wasOn = new HashSet<string>(SessionState.GetString(KEnabled, "").Split('\n'));
            var sceneRoots = new HashSet<string>(SessionState.GetString(KRoots, "").Split('\n'));
            var seen = new HashSet<MonoBehaviour>();
            void Look(MonoBehaviour b, bool mustBeOn)
            {
                if (b == null || !seen.Add(b)) return;
                if (b is DebugReadout) return; // the game switches it itself: on only with the developer's keys (RunHud)
                if (!b.enabled && (mustBeOn || wasOn.Contains(KeyOf(b))))
                    off.Add(KeyOf(b) + (b.gameObject.activeInHierarchy ? "" : " (object inactive)"));
            }
            if (s_her != null)
            {
                Look(s_her, true);
                Look(s_health, true);
                Look(s_driver, true);
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                bool madeInPlay = !sceneRoots.Contains(root.name);
                foreach (MonoBehaviour b in root.GetComponents<MonoBehaviour>()) Look(b, madeInPlay);
                foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true)) Look(b, false);
            }
            foreach (GameObject root in DontDestroyRoots())
                foreach (MonoBehaviour b in root.GetComponents<MonoBehaviour>()) Look(b, true);
            return off;
        }

        static IEnumerable<GameObject> DontDestroyRoots()
        {
            var probe = new GameObject("PlayStartCheckProbe");
            Object.DontDestroyOnLoad(probe);
            GameObject[] roots = probe.scene.GetRootGameObjects();
            Object.DestroyImmediate(probe);
            return roots.Where(r => r != null && r != probe);
        }

        static IEnumerable<string> InstalledByItself()
        {
            foreach (GameObject root in DontDestroyRoots())
                yield return root.name + (root.GetComponents<MonoBehaviour>().Length > 0 ? " [" + string.Join(" ", root.GetComponents<MonoBehaviour>().Select(b => b.GetType().Name)) + "]" : "");
        }

        // ---------------- Helpers of the steps ----------------

        static double RealNow() => s_clock.Elapsed.TotalSeconds;

        static IEnumerator GameSeconds(float seconds)
        {
            float until = Time.time + seconds;
            double guard = RealNow() + seconds + 10.0;
            while (Time.time < until && RealNow() < guard) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            int until = Time.frameCount + n;
            double guard = RealNow() + 10.0;
            while (Time.frameCount < until && RealNow() < guard) yield return null;
        }

        static IEnumerator Until(Func<bool> done, float gameSeconds)
        {
            float until = Time.time + gameSeconds;
            double guard = RealNow() + gameSeconds + 10.0;
            while (!done() && Time.time < until && RealNow() < guard) yield return null;
        }

        /// <summary>Steers her along the NavMesh's way to a point, until she is within <paramref name="near"/> of it (or <paramref name="stop"/> holds), at most so many game seconds.</summary>
        static IEnumerator WalkTo(Vector3 target, float near, float gameSeconds, Func<bool> stop = null)
        {
            float until = Time.time + gameSeconds;
            double guard = RealNow() + gameSeconds + 20.0;
            var path = new NavMeshPath();
            Vector3[] corners = { target };
            int next = 0;
            float planAt = float.NegativeInfinity;
            while (Time.time < until && RealNow() < guard && Flat(target - s_her.Position).magnitude > near && (stop == null || !stop()))
            {
                if (Time.time >= planAt + 1f)
                {
                    planAt = Time.time;
                    corners = new[] { target };
                    next = 0;
                    if (NavMesh.SamplePosition(s_her.Position, out NavMeshHit from, 2f, NavMesh.AllAreas) &&
                        NavMesh.SamplePosition(target, out NavMeshHit to, 2f, NavMesh.AllAreas) &&
                        NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.corners.Length > 0)
                        corners = path.corners;
                }
                while (next < corners.Length - 1 && Flat(corners[next] - s_her.Position).magnitude < 0.4f) next++;
                Vector3 way = Flatten(corners[next] - s_her.Position);
                if (way == Vector3.zero) way = Flatten(target - s_her.Position);
                s_her.Steer(way);
                yield return null;
            }
            s_her.Stop();
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static string OnOff(Behaviour b) => b == null ? "missing" : b.enabled ? "on" : "OFF";

        // ---------------- The report ----------------

        static void Note(string line)
        {
            SessionState.SetString(KReport, SessionState.GetString(KReport, "") + "  " + line.Replace("\n", "\n    ") + "\n");
            Debug.Log("PlayStart: " + line);
        }

        static void Check(bool ok, string what, string value) =>
            Note((ok ? "PASS " : "FAIL ") + what + ": " + value + (ok ? "" : Fault(what + ": " + value)));

        static string Fault(string fault)
        {
            string had = SessionState.GetString(KFaults, "");
            SessionState.SetString(KFaults, had.Length == 0 ? fault : had + "\u001f" + fault);
            return "";
        }

        /// <summary>Play has ended: the logs are judged, the result file written, the play session files moved beside it, and Unity ended.</summary>
        static void Finish()
        {
            if (Phase == 0) return;
            Phase = 0;
            SessionRecordingRule.BatchModeAsAnEditor = false;
            GeneratedLevels.BetweenRunsSavePath = null;
            Application.logMessageReceivedThreaded -= OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= KeepLogs;

            string[] logs = Logs();
            string report = SessionState.GetString(KReport, "");
            var faults = new List<string>();
            if (logs.Length > 0)
            {
                // A throw is the likeliest root of what else failed: it goes first.
                string logFault = logs.Length + " Exception/Error/Assert logged, the first: " + logs[0].Split('\n')[0] + " | at " + FirstStackLine(logs[0]);
                report += "  FAIL nothing logged of type Exception, Error or Assert: " + logFault + "\n";
                faults.Add(logFault);
            }
            else report += "  PASS nothing logged of type Exception, Error or Assert: none\n";
            string kept = SessionState.GetString(KFaults, "");
            if (kept.Length > 0) faults.AddRange(kept.Split('\u001f'));
            if (report.IndexOf("the game's clock ran", StringComparison.Ordinal) < 0) faults.Add("the steps did not reach their checks");
            string fault = faults.Count == 0 ? "" : faults[0] + (faults.Count > 1 ? " (and " + (faults.Count - 1) + " more: " + string.Join("; ", faults.Skip(1)) + ")" : "");

            string result = SessionState.GetString(KResult, Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp", "PlayStart-result.txt"));
            string moved = MoveSessions(Path.GetDirectoryName(result));
            bool passed = fault.Length == 0;
            int checks = report.Split('\n').Count(l => l.TrimStart().StartsWith("PASS ", StringComparison.Ordinal));
            string verdict = passed
                ? string.Format(Inv, "PlayStart: PASSED ({0} checks, {1} logs, {2})", checks, logs.Length, SessionState.GetString(KOptions, ""))
                : "PlayStart: FAILED: " + fault;

            var text = new StringBuilder();
            text.AppendLine(verdict);
            text.AppendLine();
            text.AppendLine("Scene opened in the Editor: " + ScenePath);
            text.AppendLine(SessionState.GetString(KOptions, ""));
            text.AppendLine("Play session files: " + moved);
            text.AppendLine();
            text.AppendLine("What was done, and the checks:");
            text.Append(report);
            text.AppendLine();
            text.AppendLine("Logs of type Exception, Error and Assert from Play asked for to Play ended (" + logs.Length + "):");
            foreach (string l in logs) text.AppendLine(l).AppendLine();
            string before = SessionState.GetString(KBefore, "");
            string[] beforeLogs = before.Length > 0 ? before.Split('\u001e') : new string[0];
            text.AppendLine("Logs of the Editor's own start, before Play was asked for, NOT counted (" + beforeLogs.Length + "):");
            foreach (string l in beforeLogs) text.AppendLine(l).AppendLine();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(result) ?? ".");
                File.WriteAllText(result, text.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.Log("PlayStart: could not write " + result + ": " + e.Message);
            }
            Debug.Log(verdict + "\n(result file: " + result + ")");
            EditorApplication.Exit(passed ? 0 : 1);
        }

        static string FirstStackLine(string log)
        {
            string[] lines = log.Split('\n');
            return lines.Length > 1 ? lines[1].Trim() : "";
        }

        /// <summary>The play session files the recorder wrote during the check go beside the result file, so none is left in the project.</summary>
        static string MoveSessions(string toFolder)
        {
            try
            {
                string folder = PlaySessionRecorder.DefaultFolder();
                if (!Directory.Exists(folder)) return "none written";
                string before = SessionState.GetString(KSessions, "\u0000none");
                var had = new HashSet<string>(before == "\u0000none" ? new string[0] : before.Split('|'));
                string to = Path.Combine(toFolder ?? ".", "PlayStart-sessions");
                var movedNames = new List<string>();
                foreach (string f in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(f);
                    if (had.Contains(name)) continue;
                    Directory.CreateDirectory(to);
                    string dest = Path.Combine(to, name);
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(f, dest);
                    movedNames.Add(name);
                }
                if (before == "\u0000none" && Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
                return movedNames.Count == 0 ? "none written" : movedNames.Count + " moved to " + to;
            }
            catch (Exception e)
            {
                return "could not be moved: " + e.Message;
            }
        }
    }
}
