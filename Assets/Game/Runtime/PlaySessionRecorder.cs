using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Records what Kristian does in a play session, so that after he has played we can say in numbers what he pressed:
    /// which skills he used and which he ignored, how fast he played, where the pauses were, what did the damage.
    ///
    /// It installs itself from code when Play starts, in the Editor and in a built game, and records the character it
    /// finds in the scene; it touches no scene, prefab or asset. It does not start by itself in a test run
    /// (<see cref="SessionRecordingRule"/>): not in batch mode, not in the test runner's own start scene, and not for a
    /// character shut off from the real devices. Tests start it on purpose with <see cref="StartRecording"/>.
    ///
    /// It writes one log per session: one JSON object per line (<see cref="SessionEvent"/>), into
    /// &lt;project folder&gt;/PlaySessions/ in the Editor and &lt;persistentDataPath&gt;/PlaySessions/ in a built game,
    /// named for the date and time the session started. Lines are kept in memory and written every few seconds, not
    /// per event. When the session ends (Play stops, the game quits) it writes the summary next to the log
    /// (<see cref="SessionSummary"/>).
    ///
    /// It listens to events the game raises anyway (<see cref="PlayerCharacter.Reported"/>,
    /// <see cref="Enemy.Reported"/> of every kind of enemy, <see cref="PlayerHealth"/>) and reads a few properties once a frame; it never
    /// changes what the game does. Anything that goes wrong (a full disk, a locked file) stops the recording and is said
    /// once in the console, and nothing is thrown into the game.
    /// </summary>
    public sealed partial class PlaySessionRecorder : MonoBehaviour
    {
        public const string FolderName = "PlaySessions";
        /// <summary>How often she is sampled for whether she is moving, in seconds of game time.</summary>
        public const float MoveSampleSeconds = 0.25f;
        const float FlushSeconds = 5f;            // real seconds between writes to disk
        const int FlushChars = 32 * 1024;         // or sooner, when this much waits
        const float LookForPlayerSeconds = 1f;    // real seconds between looks for a character, while it has none

        /// <summary>The recorder running now, or null.</summary>
        public static PlaySessionRecorder Current { get; private set; }

        public string Folder { get; private set; }
        /// <summary>The log's path, once a character has been found and the file opened; else null.</summary>
        public string LogPath { get; private set; }
        /// <summary>The summary's path, once the session has ended and it was written; else null.</summary>
        public string SummaryPath { get; private set; }
        public bool IsRecording => _recording;
        public bool HasFailed => _failed;
        /// <summary>True when it installed itself at the start of Play, rather than being started on purpose.</summary>
        public bool StartedByItself { get; private set; }
        public PlayerCharacter Player => _player;
        /// <summary>Every event of the session so far, in the order written.</summary>
        public IReadOnlyList<SessionEvent> Events => _events;

        readonly List<SessionEvent> _events = new List<SessionEvent>();
        readonly StringBuilder _pending = new StringBuilder(FlushChars + 1024);
        readonly Dictionary<Damageable, string> _targetKinds = new Dictionary<Damageable, string>();
        readonly List<Enemy> _enemies = new List<Enemy>();
        StreamWriter _writer;
        bool _recording;
        bool _failed;
        bool _ended;
        bool _destroying;
        bool _onlyRealDevices;
        float _startTime;
        DateTime _startedAt;
        float _nextSample;
        float _nextFlush;
        float _nextLook;
        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerCharacter _refused;
        bool _attached;
        bool _wasFull;
        bool _wasEmpowered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartByItself()
        {
            try
            {
                if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
                Begin(DefaultFolder(), byItself: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Play session recorder could not start: " + e.Message);
            }
        }

        /// <summary>Where sessions go: &lt;project folder&gt;/PlaySessions in the Editor, under persistentDataPath in a build.</summary>
        public static string DefaultFolder() =>
            Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FolderName)
                : Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>
        /// Starts a session on purpose, writing into this folder, whatever the rule for starting by itself says. It
        /// records the character in the scene, or the first one that appears. A recorder already running is stopped
        /// first. For tests and tools.
        /// </summary>
        public static PlaySessionRecorder StartRecording(string folder) => Begin(folder, byItself: false);

        static PlaySessionRecorder Begin(string folder, bool byItself)
        {
            if (Current != null) Current.StopRecording();
            var go = new GameObject("PlaySessionRecorder");
            DontDestroyOnLoad(go);
            var recorder = go.AddComponent<PlaySessionRecorder>();
            recorder.Folder = folder;
            recorder.StartedByItself = byItself;
            recorder._onlyRealDevices = byItself;
            recorder._startTime = Time.time;
            recorder._startedAt = DateTime.Now;
            recorder._recording = true;
            Current = recorder;
            SceneManager.sceneLoaded += recorder.OnSceneLoaded;
            try
            {
                recorder.LookForPlayer();
            }
            catch (Exception e)
            {
                recorder.Fail(e);
            }
            return recorder;
        }

        /// <summary>
        /// Ends the session: writes its last lines and the summary, and removes the recorder. Safe to call twice.
        /// Play stopping and the game quitting call it.
        /// </summary>
        public void StopRecording()
        {
            if (_ended) return;
            _ended = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_recording && _writer != null)
            {
                try
                {
                    EndFramePart(FrameTimeEvents.PartPracticeRoom, 0, 0); // work order 074: the last part's frames
                    Add(new SessionEvent(Now(), SessionEventTypes.SessionEnd));
                    Flush();
                    _writer.Dispose();
                    _writer = null;
                    string text = SessionSummary.Format(SessionSummary.Compute(_events), Path.GetFileName(LogPath));
                    string summaryPath = SessionSummary.SummaryPathFor(LogPath);
                    File.WriteAllText(summaryPath, text, new UTF8Encoding(false));
                    SummaryPath = summaryPath;
                    Debug.Log("Play session recorded: " + LogPath + "\nSummary: " + summaryPath);
                }
                catch (Exception e)
                {
                    Fail(e);
                }
            }
            _recording = false;
            Detach();
            CloseQuietly();
            if (Current == this) Current = null;
            if (!_destroying && this != null) Destroy(gameObject);
        }

        void OnApplicationQuit() => StopRecording();

        void OnDestroy()
        {
            _destroying = true;
            StopRecording();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_recording) return;
            try
            {
                if (_player == null) LookForPlayer();
                else FindEnemies();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void Update()
        {
            if (!_recording) return;
            try
            {
                if (_player == null)
                {
                    if (_attached) Detach(); // the character was destroyed with its scene
                    if (Time.unscaledTime >= _nextLook) LookForPlayer();
                    if (_player == null) return;
                }
                MeasureFrame(); // work order 074: the frame's time, while in play

                float now = Time.time;
                // The gauge, read once a frame: when it becomes full, and when the empowered mode ends.
                bool full = _player.IsFocusFull;
                if (full && !_wasFull) Add(new SessionEvent(Now(), SessionEventTypes.FocusFull));
                _wasFull = full;
                bool empowered = _player.IsEmpowered;
                if (!empowered && _wasEmpowered) Add(new SessionEvent(Now(), SessionEventTypes.EmpoweredEnd));
                _wasEmpowered = empowered;
                NoteShotInterval();

                if (now >= _nextSample)
                {
                    _nextSample = now + MoveSampleSeconds;
                    Add(new SessionEvent(Now(), SessionEventTypes.Move) { Moving = _player.IsMoving });
                }

                if (_pending.Length >= FlushChars || Time.unscaledTime >= _nextFlush) Flush();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------- Finding what to record ----------------

        void LookForPlayer()
        {
            _nextLook = Time.unscaledTime + LookForPlayerSeconds;
            PlayerCharacter found = FindAnyObjectByType<PlayerCharacter>();
            if (found == null || found == _refused) return;
            if (_onlyRealDevices && !SessionRecordingRule.MayRecordCharacter(PlayerInputDriver.ReadRealDevices))
            {
                _refused = found; // a test's character: never this one
                return;
            }
            Attach(found);
        }

        void Attach(PlayerCharacter player)
        {
            _player = player;
            _health = player.GetComponent<PlayerHealth>();
            _shotInterval = -1f; // work order 055: written again for her
            _wasFull = player.IsFocusFull;
            _wasEmpowered = player.IsEmpowered;
            _nextSample = Time.time;
            _nextFlush = Time.unscaledTime + FlushSeconds;
            player.Reported += OnPlayer;
            Damageable.MarksLost += OnMarksLost;
            Enemy.Appeared += Listen; // those spawned later: a generated level's, the practice room's spawns
            ThingsThatFly.Loosed += OnLoosed; // work order 036
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
            _attached = true;
            FindEnemies();
            if (_writer == null) OpenLog(player);
        }

        void FindEnemies()
        {
            _enemies.RemoveAll(e => e == null); // those of a generated level that was removed
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.InstanceID)) Listen(enemy);
        }

        void Listen(Enemy enemy)
        {
            if (enemy == null || _enemies.Contains(enemy)) return;
            _enemies.Add(enemy);
            enemy.Reported += OnEnemy;
        }

        void Detach()
        {
            if (_player != null) _player.Reported -= OnPlayer;
            Damageable.MarksLost -= OnMarksLost;
            Enemy.Appeared -= Listen;
            ThingsThatFly.Loosed -= OnLoosed;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
            foreach (Enemy enemy in _enemies)
                if (enemy != null) enemy.Reported -= OnEnemy;
            _enemies.Clear();
            _targetKinds.Clear();
            _player = null;
            _health = null;
            _attached = false;
        }

        // ---------------- The file ----------------

        void OpenLog(PlayerCharacter player)
        {
            Directory.CreateDirectory(Folder);
            string stem = _startedAt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string path = Path.Combine(Folder, stem + ".jsonl");
            for (int n = 2; File.Exists(path) || File.Exists(SessionSummary.SummaryPathFor(path)); n++)
                path = Path.Combine(Folder, stem + "_" + n + ".jsonl");
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), 16 * 1024);
            LogPath = path;
            WriteStart(player);
            Flush(); // the start is on disk at once
        }

        void WriteStart(PlayerCharacter player)
        {
            var start = new SessionEvent(Now(), SessionEventTypes.SessionStart);
            start.AddExtra("date", _startedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            string commit = "", branch = "";
            if (Application.isEditor) ReadGitHead(Path.GetDirectoryName(Application.dataPath), out commit, out branch);
            start.AddExtra("commit", commit);
            start.AddExtra("branch", branch);
            start.AddExtra("unity", Application.unityVersion);
            start.AddExtra("scene", player.gameObject.scene.name);
            start.AddExtra("editor", Application.isEditor ? "true" : "false");
            AddFields(start, "feel.", player.Feel);
            AddFields(start, "identity.", player.Identity);
            AddFields(start, "kit.", player.Kit);
            EnemyBrute enemy = null;
            foreach (Enemy e in _enemies)
                if (e is EnemyBrute brute) { enemy = brute; break; }
            if (enemy != null) AddEnemyFields(start, "enemy.", enemy.Values);
            // The other kinds (work order 008), as their values start in code.
            var swarmer = new SwarmerValues();
            AddFields(start, "swarmer.", swarmer);
            AddEnemyFields(start, "swarmer.", swarmer.Enemy);
            var shooter = new ShooterValues();
            AddFields(start, "shooter.", shooter);
            AddEnemyFields(start, "shooter.", shooter.Enemy);
            var guard = new GuardValues();
            AddFields(start, "guard.", guard);
            AddEnemyFields(start, "guard.", guard.Enemy);
            AddFields(start, "guard.stagger.", guard.Stagger);
            start.AddExtra("things_that_fly", ThingsThatFly.On ? "true" : "false"); // work order 036: the switch as it stood
            Add(start);

            IReadOnlyList<SkillDefinition> skills = player.Skills;
            for (int slot = 0; slot < skills.Count; slot++)
            {
                SkillDefinition s = skills[slot];
                if (s == null) continue;
                var e = new SessionEvent(Now(), SessionEventTypes.Skill, s.Name)
                {
                    Slot = slot, Input = s.Input.ToString(), Cooldown = s.Cooldown, Damage = s.Damage,
                };
                e.AddExtra("startup", Text(s.Startup));
                e.AddExtra("recovery", Text(s.Recovery));
                e.AddExtra("hit_count", s.HitCount.ToString(CultureInfo.InvariantCulture));
                if (s.Input == SkillInput.Holding || s.Input == SkillInput.Charge) e.AddExtra("hold_max", Text(s.HoldMax));
                if (s.Input == SkillInput.Holding) e.AddExtra("tick_interval", Text(s.TickInterval));
                if (s.Input == SkillInput.Charge) e.AddExtra("charge_time", Text(s.ChargeTime));
                // The kit (decision 0005).
                e.AddExtra("equipped", player.IsEquipped(slot) ? "true" : "false");
                // The key it sits on (work order 015: the place in the list is which skill it is, not its key).
                int key = player.KeyOfSkill(slot);
                InputBindings bindings = player.GetComponent<PlayerInputDriver>() is PlayerInputDriver driver ? driver.ActiveBindings : null;
                e.AddExtra("key", key < 0 ? "none" : bindings != null ? bindings.SkillLabel(key) : key.ToString(CultureInfo.InvariantCulture));
                e.AddExtra("lock", s.Lock.ToString());
                if (s.MarksAdded > 0) e.AddExtra("marks_added", s.MarksAdded.ToString(CultureInfo.InvariantCulture));
                if (s.MarkDamageBonus > 0f) e.AddExtra("mark_bonus", Text(s.MarkDamageBonus));
                if (s.RecastCount > 1) e.AddExtra("recast_count", s.RecastCount.ToString(CultureInfo.InvariantCulture));
                // Amended 2026-11-19: a chain may open with a move that fires nothing (Backstep's hop: its first press
                // is a skill start and end with no shot), and its clean press may spend the marks instead of a clean tap.
                if (s.RecastCount > 1 && s.OpeningMove) e.AddExtra("opening_move", "true");
                if (s.RecastCount > 1 && s.CleanTapMarks > 0) e.AddExtra("clean_tap_marks", s.CleanTapMarks.ToString(CultureInfo.InvariantCulture));
                if (s.RecastCount > 1 && s.CleanMarkDamageBonus > 0f) e.AddExtra("clean_mark_bonus", Text(s.CleanMarkDamageBonus));
                Add(e);
            }
        }

        static void AddEnemyFields(SessionEvent e, string prefix, EnemyValues values)
        {
            AddFields(e, prefix, values);
            if (values?.Attacks == null) return;
            foreach (EnemyAttackDefinition attack in values.Attacks)
                if (attack != null) AddFields(e, prefix + attack.Name + ".", attack);
        }

        /// <summary>Every public number, switch and word of a settings object, under a prefix: the tuning in use.</summary>
        static void AddFields(SessionEvent e, string prefix, object values)
        {
            if (values == null) return;
            foreach (FieldInfo field in values.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = field.GetValue(values);
                switch (v)
                {
                    case float f: e.AddExtra(prefix + field.Name, Text(f)); break;
                    case int i: e.AddExtra(prefix + field.Name, i.ToString(CultureInfo.InvariantCulture)); break;
                    case bool b: e.AddExtra(prefix + field.Name, b ? "true" : "false"); break;
                    case string s when field.Name != "Name": e.AddExtra(prefix + field.Name, s); break;
                    case Enum en: e.AddExtra(prefix + field.Name, en.ToString()); break;
                }
            }
        }

        static string Text(float f) => f.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// The commit and branch checked out, read from .git/HEAD in the project folder or a folder above it, without
        /// starting a process. A worktree's .git file and packed refs are followed. Empty when it cannot be read.
        /// </summary>
        public static void ReadGitHead(string projectFolder, out string commit, out string branch)
        {
            commit = "";
            branch = "";
            try
            {
                string gitDir = null;
                for (string dir = projectFolder; dir != null && gitDir == null; dir = Path.GetDirectoryName(dir))
                {
                    string git = Path.Combine(dir, ".git");
                    if (Directory.Exists(git)) gitDir = git;
                    else if (File.Exists(git))
                    {
                        string line = File.ReadAllText(git).Trim();
                        if (!line.StartsWith("gitdir:", StringComparison.Ordinal)) return;
                        gitDir = line.Substring(7).Trim();
                        if (!Path.IsPathRooted(gitDir)) gitDir = Path.GetFullPath(Path.Combine(dir, gitDir));
                    }
                }
                if (gitDir == null) return;
                string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    commit = head;
                    return;
                }
                string refName = head.Substring(4).Trim();
                branch = refName.StartsWith("refs/heads/", StringComparison.Ordinal) ? refName.Substring(11) : refName;
                string common = gitDir;
                string commonFile = Path.Combine(gitDir, "commondir");
                if (File.Exists(commonFile))
                {
                    common = File.ReadAllText(commonFile).Trim();
                    if (!Path.IsPathRooted(common)) common = Path.GetFullPath(Path.Combine(gitDir, common));
                }
                foreach (string dir in new[] { gitDir, common })
                {
                    string loose = Path.Combine(dir, refName.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(loose))
                    {
                        commit = File.ReadAllText(loose).Trim();
                        return;
                    }
                }
                string packed = Path.Combine(common, "packed-refs");
                if (!File.Exists(packed)) return;
                foreach (string line in File.ReadAllLines(packed))
                {
                    if (line.EndsWith(" " + refName, StringComparison.Ordinal))
                    {
                        commit = line.Substring(0, line.IndexOf(' '));
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Not knowing the commit is no reason to record nothing.
            }
        }

        // ---------------- Events ----------------

        /// <summary>
        /// Seconds since the session started, to the millisecond the log holds (<see cref="SessionEvent.AppendJson"/>):
        /// the summary written at the end and one recomputed from the log then see the very same times. Kept to the
        /// microsecond before 2026-11-19, a rate near a rounding edge could come out 0.1 apart in the two.
        /// </summary>
        double Now() => double.Parse(((double)(Time.time - _startTime)).ToString("0.000", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        void Add(SessionEvent e)
        {
            if (_writer == null) return;
            _events.Add(e);
            e.AppendJson(_pending);
            _pending.Append('\n');
        }

        void Flush()
        {
            _nextFlush = Time.unscaledTime + FlushSeconds;
            if (_writer == null || _pending.Length == 0) return;
            _writer.Write(_pending.ToString());
            _writer.Flush();
            _pending.Clear();
        }

        void Fail(Exception e)
        {
            if (_failed) return;
            _failed = true;
            _recording = false;
            Debug.LogWarning("Play session recorder stopped and records nothing more this session: " + e.Message
                             + (LogPath != null ? " (" + LogPath + ")" : ""));
            try { Detach(); } catch (Exception) { }
            CloseQuietly();
        }

        void CloseQuietly()
        {
            try { _writer?.Dispose(); }
            catch (Exception) { }
            _writer = null;
        }

        string SkillName(int slot)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            return skills != null && slot >= 0 && slot < skills.Count && skills[slot] != null
                ? skills[slot].Name
                : "slot " + slot.ToString(CultureInfo.InvariantCulture);
        }

        SkillInput? InputOf(int slot)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            return skills != null && slot >= 0 && slot < skills.Count && skills[slot] != null ? skills[slot].Input : (SkillInput?)null;
        }

        string TargetKind(Damageable target)
        {
            if (target == null) return "";
            if (_targetKinds.TryGetValue(target, out string kind)) return kind;
            kind = target.GetComponent<Enemy>() != null ? SessionWords.Enemy
                 : target.GetComponent<TrainingDummy>() != null ? SessionWords.Dummy
                 : target.name;
            _targetKinds[target] = kind;
            return kind;
        }

        static string Word(IntentResult result) =>
            result == IntentResult.Started ? SessionWords.Started
            : result == IntentResult.Buffered ? SessionWords.Buffered
            : SessionWords.Rejected;

        void OnPlayer(PlayerReport r)
        {
            if (!_recording) return;
            try
            {
                double t = Now();
                switch (r.Kind)
                {
                    case PlayerReportKind.SkillPressed:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillPress, SkillName(r.Slot)) { Slot = r.Slot, Result = Word(r.Result) };
                        if (r.Result == IntentResult.Rejected)
                        {
                            e.Reason = r.NotEquipped ? SessionWords.NotEquipped : r.CooldownLeft > 0f ? SessionWords.Cooldown : "no such skill";
                            if (r.CooldownLeft > 0f) e.CooldownLeft = r.CooldownLeft;
                        }
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.MarkAdded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.MarkAdd, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null, Target = TargetKind(r.Target), Marks = r.MarksAfter,
                        };
                        e.AddExtra("added", r.Marks.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.MarksSpent:
                        Add(new SessionEvent(t, SessionEventTypes.MarksSpent, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Target = TargetKind(r.Target), Marks = r.Marks, Damage = r.Damage, Focus = r.Focus,
                        });
                        break;
                    case PlayerReportKind.RecastPressed:
                    {
                        var press = new SessionEvent(t, SessionEventTypes.RecastPress, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Shot = r.Shot, Clean = r.Clean, Focus = r.Clean ? r.Focus : (float?)null,
                        };
                        // Work order 069: how it fell by its own window's clean part, and the window's clock at the press (s after the
                        // shot before went off; for a press that waited, when it fired), so that the clean parts can be set from his presses.
                        press.AddExtra("timing", r.Timing.ToString().ToLowerInvariant());
                        press.AddExtra("since", r.Seconds.ToString("0.000", CultureInfo.InvariantCulture));
                        Add(press);
                        break;
                    }
                    case PlayerReportKind.ChainEnded:
                        Add(new SessionEvent(t, SessionEventTypes.ChainEnd, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Shot = r.Shot, Result = r.Early ? SessionWords.Early : SessionWords.Complete,
                        });
                        break;
                    case PlayerReportKind.CooldownsCut:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.CooldownCut);
                        if (r.Cuts != null)
                            for (int slot = 0; slot < r.Cuts.Length; slot++)
                                if (r.Cuts[slot] > 0f) e.AddExtra("cut." + SkillName(slot), Text(r.Cuts[slot]));
                        if (r.DashCut > 0f) e.AddExtra("cut." + SessionWords.Dash, Text(r.DashCut));
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.SkillReleased:
                        Add(new SessionEvent(t, SessionEventTypes.SkillRelease, SkillName(r.Slot)) { Slot = r.Slot });
                        break;
                    case PlayerReportKind.SkillStarted:
                        Add(new SessionEvent(t, SessionEventTypes.SkillStart, SkillName(r.Slot)) { Slot = r.Slot });
                        break;
                    case PlayerReportKind.SkillFired:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillFired, SkillName(r.Slot)) { Slot = r.Slot, Hit = r.Landed };
                        if (InputOf(r.Slot) == SkillInput.Charge) e.Charge = r.Charge;
                        if (r.Clean) e.Clean = true; // work order 015: a perfect release, or a keg set off by a shot
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.SkillEnded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillEnd, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Hit = r.Fired,
                            Result = r.Ending == SkillEnding.RanOut ? SessionWords.RanOut
                                   : r.Ending == SkillEnding.CancelledByDash ? SessionWords.CancelledByDash
                                   : SessionWords.Cancelled,
                        };
                        SkillInput? input = InputOf(r.Slot);
                        if (input == SkillInput.Holding || input == SkillInput.Charge) e.Held = r.Held;
                        if (input == SkillInput.Charge) e.Charge = r.Charge;
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.BufferDropped:
                        Add(new SessionEvent(t, SessionEventTypes.BufferDropped, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.Dash)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                        });
                        break;
                    case PlayerReportKind.ShotClicked:
                        Add(new SessionEvent(t, SessionEventTypes.Click, SessionWords.BasicShot)
                        {
                            Result = r.Result == IntentResult.Started ? SessionWords.Started : SessionWords.Waiting,
                        });
                        break;
                    case PlayerReportKind.ShotDropped:
                        Add(new SessionEvent(t, SessionEventTypes.ClickDropped, SessionWords.BasicShot));
                        break;
                    case PlayerReportKind.ShotStarted:
                        Add(new SessionEvent(t, SessionEventTypes.ShotStart, SessionWords.BasicShot) { Moving = r.Moving });
                        break;
                    case PlayerReportKind.ShotFired:
                        Add(new SessionEvent(t, SessionEventTypes.ShotFired, SessionWords.BasicShot) { Hit = r.Landed });
                        break;
                    case PlayerReportKind.DashPressed:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.DashPress, SessionWords.Dash) { Result = Word(r.Result) };
                        if (r.Result == IntentResult.Rejected)
                        {
                            e.Reason = r.AlreadyDashing ? SessionWords.Dashing : SessionWords.Cooldown;
                            if (r.CooldownLeft > 0f) e.CooldownLeft = r.CooldownLeft;
                        }
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.DashStarted:
                        Add(new SessionEvent(t, SessionEventTypes.DashStart, SessionWords.Dash));
                        break;
                    case PlayerReportKind.Hit:
                    {
                        Enemy enemy = r.Target != null ? r.Target.GetComponent<Enemy>() : null;
                        var hit = new SessionEvent(t, SessionEventTypes.Hit, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                            Damage = r.Damage, Target = TargetKind(r.Target), Empowered = r.Empowered,
                            Focus = r.Focus > 0f ? r.Focus : (float?)null,
                            Enemy = enemy != null ? enemy.LogWord : null,
                            EnemyId = enemy != null ? enemy.Id : (int?)null,
                        };
                        if (r.Crit) hit.AddExtra("crit", "true"); // work order 050
                        Add(hit);
                        break;
                    }
                    case PlayerReportKind.FocusPressed:
                        Add(new SessionEvent(t, SessionEventTypes.FocusPress) { Result = Word(r.Result) });
                        if (r.Result == IntentResult.Started)
                        {
                            // What it was spent as (work order 025): today's full gauge, a part, or Steady Hand's window.
                            var spent = new SessionEvent(t, SessionEventTypes.FocusSpent);
                            spent.AddExtra("as", r.FocusMode == FocusMode.HairTrigger ? "part" : r.FocusMode == FocusMode.SteadyHand ? "window" : "today");
                            Add(spent);
                        }
                        break;
                    case PlayerReportKind.PrimeUsed:
                        Add(new SessionEvent(t, SessionEventTypes.PrimeUsed, SkillName(r.Slot))
                        {
                            Slot = r.Slot,
                            Result = r.Prime == PrimeUse.CleanPress ? "clean press" : r.Prime == PrimeUse.PerfectRelease ? "perfect release"
                                   : r.Prime == PrimeUse.SpenderExtraMark ? "a mark more counted" : "a mark more",
                        });
                        break;
                    case PlayerReportKind.PrimeLapsed:
                        Add(new SessionEvent(t, SessionEventTypes.PrimeLapsed));
                        break;
                    // Melee you read (work order 035): two things that happen to her.
                    case PlayerReportKind.Stunned:
                    case PlayerReportKind.Pulled:
                    {
                        var e = new SessionEvent(t, r.Kind == PlayerReportKind.Stunned ? SessionEventTypes.PlayerStunned : SessionEventTypes.PlayerPulled, r.By);
                        e.AddExtra("seconds", Text(r.Seconds));
                        Add(e);
                        break;
                    }
                    // Taking cover (2026-11-19, an experiment).
                    case PlayerReportKind.CoverEntered:
                        Add(new SessionEvent(t, SessionEventTypes.CoverEnter, r.Tall ? SessionWords.Tall : SessionWords.Low));
                        break;
                    case PlayerReportKind.CoverLeft:
                        Add(new SessionEvent(t, SessionEventTypes.CoverLeave)
                        {
                            Result = r.Leaving == CoverLeaving.Released ? SessionWords.Released
                                   : r.Leaving == CoverLeaving.Dash ? SessionWords.DashLeft
                                   : r.Leaving == CoverLeaving.MovingSkill ? SessionWords.MovingSkill
                                   : SessionWords.Other,
                        });
                        break;
                    // The quick duck (work order 043).
                    case PlayerReportKind.Ducked:
                        Add(new SessionEvent(t, SessionEventTypes.Duck, r.By));
                        break;
                    case PlayerReportKind.DuckEnded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.DuckEnd)
                        {
                            Result = r.DuckEnd == DuckEnd.Rose ? SessionWords.Rose : r.DuckEnd == DuckEnd.Moved ? SessionWords.Moved
                                   : r.DuckEnd == DuckEnd.Dashed ? SessionWords.DashLeft : SessionWords.Other,
                        };
                        e.AddExtra("stopped", r.Shot.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.DuckNothing:
                        Add(new SessionEvent(t, SessionEventTypes.DuckNothing));
                        break;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void OnEnemy(EnemyReport r)
        {
            if (!_recording) return;
            try
            {
                double t = Now();
                // Every enemy event says which enemy and its kind (work order 008).
                SessionEvent Of(string type, string name = null) =>
                    new SessionEvent(t, type, name) { Enemy = (r.Elite ? "elite " : "") + EnemyKinds.Word(r.EnemyKind), EnemyId = r.Id };
                switch (r.Kind)
                {
                    case EnemyReportKind.Woke:
                        Add(Of(SessionEventTypes.EnemyWake));
                        break;
                    case EnemyReportKind.AttackStarted:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyAttack, r.Attack);
                        if (r.EnemyKind == EnemyKind.Bomber) e.AddExtra("fuse", SessionWords.Fuse(r.Count)); // work order 073: how its fuse was lit
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Struck:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyStrike, r.Attack);
                        e.Result = r.Outcome == StrikeOutcome.Landed ? SessionWords.Landed
                                 : r.Outcome == StrikeOutcome.OutOfArea ? SessionWords.OutOfArea
                                 : r.Outcome == StrikeOutcome.AvoidedByDash ? SessionWords.AvoidedByDash
                                 : r.Outcome == StrikeOutcome.StoppedByCover ? SessionWords.StoppedByCover
                                 : r.Outcome == StrikeOutcome.StoppedByWall ? SessionWords.StoppedByWall
                                 : SessionWords.NoEffect;
                        e.Damage = r.Damage > 0 ? r.Damage : (int?)null;
                        if (r.Flown) e.AddExtra("flown", "true"); // work order 036: the end of a thing that flew
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.GaveUp:
                        Add(Of(SessionEventTypes.EnemyGaveUp));
                        break;
                    case EnemyReportKind.Removed:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyGaveUp);
                        e.Result = SessionWords.Removed;
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Died:
                        Add(Of(SessionEventTypes.EnemyDeath));
                        break;
                    case EnemyReportKind.StaggerTaken:
                    {
                        SessionEvent e = Of(SessionEventTypes.StaggerTaken);
                        e.Stagger = r.Stagger;
                        e.AddExtra("bar", Text(r.StaggerLeft));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Dazed:
                        Add(Of(SessionEventTypes.EnemyDazed));
                        break;
                    case EnemyReportKind.DazeEnded:
                        Add(Of(SessionEventTypes.DazeEnded));
                        break;
                    case EnemyReportKind.Charged:
                        Add(Of(SessionEventTypes.BossCharge, r.Attack));
                        break;
                    case EnemyReportKind.CalledPack:
                    {
                        SessionEvent e = Of(SessionEventTypes.BossCall);
                        e.AddExtra("called", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Angered:
                        Add(Of(SessionEventTypes.BossAnger));
                        break;
                    // The caster (work order 027).
                    case EnemyReportKind.CirclePlaced:
                    case EnemyReportKind.CircleOffGround:
                    {
                        SessionEvent e = Of(SessionEventTypes.CasterCircle, r.Attack);
                        e.Result = r.Kind == EnemyReportKind.CirclePlaced ? SessionWords.Placed : SessionWords.OffTheGround;
                        e.AddExtra("circle", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.CircleFaded:
                    {
                        SessionEvent e = Of(SessionEventTypes.CasterCircleFaded, r.Attack);
                        e.AddExtra("circle", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Countered:
                        Add(Of(SessionEventTypes.EnemyCountered, r.Attack));
                        break;
                    // The bomber's blast on the others (work order 073); on her it is an enemy_strike as every enemy's.
                    case EnemyReportKind.Blasted:
                    {
                        SessionEvent e = Of(SessionEventTypes.BomberBlast, r.Attack);
                        e.Damage = r.Damage > 0 ? r.Damage : (int?)null;
                        e.AddExtra("enemies", r.Count.ToString(CultureInfo.InvariantCulture));
                        e.AddExtra("killed", r.Killed.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Things that left an enemy at its release (work order 036): which attack, how many at once.</summary>
        void OnLoosed(Enemy enemy, EnemyAttackDefinition attack, int count)
        {
            if (!_recording || enemy == null) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.EnemyLoose, attack?.Name) { Enemy = enemy.LogWord, EnemyId = enemy.Id };
                e.AddExtra("count", count.ToString(CultureInfo.InvariantCulture));
                Add(e);
            }
            catch (Exception e) { Fail(e); }
        }

        /// <summary>Marks lost unused, on any target (decision 0005).</summary>
        void OnMarksLost(Damageable target, int marks, MarkLoss why)
        {
            if (!_recording) return;
            try
            {
                Add(new SessionEvent(Now(), SessionEventTypes.MarksLost)
                {
                    Target = TargetKind(target), Marks = marks,
                    Result = why == MarkLoss.FellOff ? SessionWords.FellOff : why == MarkLoss.Died ? SessionWords.Died : SessionWords.Reset,
                });
            }
            catch (Exception e) { Fail(e); }
        }

        /// <summary>
        /// A generated level was built (<see cref="GeneratedLevels"/>): writes its seed, version and number of rooms,
        /// and starts listening to the enemies it brought.
        /// </summary>
        public void NoteLevelBuilt(string seedText, int version, int rooms)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.LevelBuilt, seedText);
                e.AddExtra("version", version.ToString(CultureInfo.InvariantCulture));
                e.AddExtra("rooms", rooms.ToString(CultureInfo.InvariantCulture));
                Add(e);
                if (_player != null) FindEnemies();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>
        /// She entered a room of the generated level: its place in the run (from 0) and its kind, and (work order 028) its
        /// theme in words when it has one.
        /// </summary>
        public void NoteRoomEntered(int room, string kind, string theme = null)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.RoomEntered, kind);
                e.AddExtra("room", room.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(theme)) e.AddExtra("theme", theme);
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------- Runs (work order 012) ----------------

        void Note(string type, string name, params string[] extras)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), type, name);
                for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>A run started, with this seed; and her basic shot's interval in it (work order 055).</summary>
        public void NoteRunStarted(string runSeed)
        {
            Note(SessionEventTypes.RunStart, runSeed);
            _frameRuns++; // work order 074: the frame times' parts say which run
            NoteShotInterval();
        }

        float _shotInterval = -1f;

        /// <summary>
        /// Her basic shot's interval (<see cref="PlayerCharacter.BasicShotInterval"/>), written when it is not the one last
        /// written: when she is found, at a run's start, and when her attack speed changes it (the fourth review of
        /// 2026-11-21, work order 055: the summary measured the shot against the feel's 0.225 s).
        /// </summary>
        void NoteShotInterval()
        {
            if (!_recording || _player == null) return;
            float interval = _player.BasicShotInterval;
            if (Mathf.Abs(interval - _shotInterval) < 1e-5f) return;
            _shotInterval = interval;
            Note(SessionEventTypes.ShotInterval, null, "seconds", interval.ToString("0.#####", CultureInfo.InvariantCulture));
        }

        /// <summary>She entered floor <paramref name="floor"/> of the run, of this seed.</summary>
        public void NoteFloorEntered(int floor, string seed)
        {
            EndFramePart(FrameTimeEvents.PartFloor, _frameRuns, floor); // work order 074: the part before ends, this floor's begins
            Note(SessionEventTypes.FloorEnter, seed, "floor", Int(floor));
        }

        /// <summary>The floor's exit room was cleared: how long the floor took (game time: a choice's pause does not count) and the health she had left.</summary>
        public void NoteFloorFinished(int floor, float seconds, int health) =>
            Note(SessionEventTypes.FloorFinish, null, "floor", Int(floor), "seconds", Text(seconds), "health", Int(health));

        /// <summary>The floor reward offered: the skills, in the order shown; work order 057: with <paramref name="tiers"/> given (the skill tiers on), each card's tier too.</summary>
        public void NoteRewardOffered(int floor, IList<string> skills, IList<string> tiers = null)
        {
            if (tiers == null) Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", skills));
            else Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", skills), "tiers", string.Join(" | ", tiers));
        }

        /// <summary>A floor reward taken; work order 057: a rare or epic card's tier, and an epic's change by name ("" for a rare).</summary>
        public void NoteRewardTaken(int floor, string skill, string tier = null, string with = null)
        {
            if (string.IsNullOrEmpty(tier) || tier == UpgradeTiers.Word(UpgradeTier.Common)) Note(SessionEventTypes.RewardTake, skill, "floor", Int(floor));
            else Note(SessionEventTypes.RewardTake, skill, "floor", Int(floor), "tier", tier, "with", with ?? "");
        }

        /// <summary>Work order 023: she was healed as floor <paramref name="floor"/>'s reward was taken: what was given, and her health after.</summary>
        public void NoteFloorHeal(int floor, int healed, int health) =>
            Note(SessionEventTypes.FloorHeal, null, "floor", Int(floor), "healed", Int(healed), "health", Int(health));

        /// <summary>Every enemy of a combat room is dead and she was healed (work order 030): the room, whether it is the elite room, what was given and her health after.</summary>
        public void NoteRoomHeal(int floor, int room, bool elite, int healed, int health) =>
            Note(SessionEventTypes.RoomHeal, null, "floor", Int(floor), "room", Int(room), "elite", elite ? "true" : "false", "healed", Int(healed), "health", Int(health));

        /// <summary>
        /// A room's fight started (work order 070): every enemy of the room woke at once. The floor, the room counted from 1
        /// without the passages (as the run box counts), its place in the floor, its kind (combat, elite, exit), how it started
        /// (point, seen, hit, provoked), whether she was inside the room then, and how many woke.
        /// </summary>
        public void NoteRoomFight(int floor, int room, int order, string kind, string how, bool inside, int woke) =>
            Note(SessionEventTypes.RoomFight, how, "floor", Int(floor), "room", Int(room), "order", Int(order), "kind", kind,
                 "inside", inside ? "true" : "false", "woke", Int(woke));

        /// <summary>
        /// A chest opened: what it offered, and what was taken (short forms). Work order 050: for a tiered pick also its
        /// stat, tier and size (its amount, and its heal when it heals), and her stats after it in one line.
        /// </summary>
        public void NoteChestOpened(int floor, int room, IList<string> offered, string taken, Upgrade tiered = null, string stats = null)
        {
            if (tiered == null)
            {
                Note(SessionEventTypes.ChestOpen, taken, "floor", Int(floor), "room", Int(room), "offered", string.Join(" | ", offered));
                return;
            }
            Note(SessionEventTypes.ChestOpen, taken, "floor", Int(floor), "room", Int(room), "offered", string.Join(" | ", offered),
                 "stat", tiered.Stat, "tier", tiered.TierWord, "size", Text(tiered.Amount), "heal", Int(tiered.Heal), "stats", stats ?? "");
        }

        /// <summary>She entered the floor's elite room for the first time (work order 017).</summary>
        public void NoteEliteEntered(int floor, int room) => Note(SessionEventTypes.EliteEnter, null, "floor", Int(floor), "room", Int(room));

        /// <summary>The elite room was cleared (work order 017): the seconds since she first entered it (game time), and the health she had left.</summary>
        public void NoteEliteCleared(int floor, float seconds, int health) =>
            Note(SessionEventTypes.EliteClear, null, "floor", Int(floor), "seconds", Text(seconds), "health", Int(health));

        /// <summary>The skill upgrades offered (work order 017), "Skill: Name (kind)" in the order shown; none, and a heal instead.</summary>
        public void NoteSkillUpgradeOffered(int floor, IList<string> offered, bool heal) =>
            Note(SessionEventTypes.SkillUpgradeOffer, null, "floor", Int(floor), "offered", string.Join(" | ", offered), "heal", heal ? "true" : "false");

        /// <summary>A skill upgrade was taken (work order 017): its full name and kind.</summary>
        public void NoteSkillUpgradeTaken(int floor, string fullName, string kind) =>
            Note(SessionEventTypes.SkillUpgradeTake, fullName, "floor", Int(floor), "kind", kind);

        /// <summary>The focus choice offered (work order 025): the two ways, in the order shown.</summary>
        public void NoteFocusChoiceOffered(int floor, IList<string> offered) =>
            Note(SessionEventTypes.FocusChoiceOffer, null, "floor", Int(floor), "offered", string.Join(" | ", offered));

        /// <summary>The focus choice taken (work order 025), by its name.</summary>
        public void NoteFocusChoiceTaken(int floor, string name) => Note(SessionEventTypes.FocusChoiceTake, name, "floor", Int(floor));

        /// <summary>The run ended: how (death, act cleared, left), where (floor, room from 1 and its kind), and when.</summary>
        public void NoteRunEnded(string how, int floor, int room, string kind, float seconds, string stats = null)
        {
            EndFramePart(FrameTimeEvents.PartPracticeRoom, 0, 0); // work order 074: the floor's frames end with the run
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.RunEnd) { Result = how };
                e.AddExtra("floor", Int(floor));
                e.AddExtra("room", Int(room));
                if (!string.IsNullOrEmpty(kind)) e.AddExtra("kind", kind);
                e.AddExtra("seconds", Text(seconds));
                if (!string.IsNullOrEmpty(stats)) e.AddExtra("stats", stats); // work order 050: her final stats
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------- Between runs (work order 029; only with that trial switched on) ----------------

        /// <summary>
        /// A run started with the rack switched on: the pieces equipped, the currency then, the budget and the points used.
        /// Work order 060: <paramref name="weapon"/>, her pistols' parts fitted joined by " | " (or "stock"), only when the
        /// attachments are on; null, the event is as before.
        /// </summary>
        public void NoteBetweenRunsLoadout(IList<string> pieces, int currency, int budget, int used, string weapon = null)
        {
            if (weapon == null) Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used));
            else Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used), "weapon", weapon);
        }

        /// <summary>The run earned currency: how much, from what (floor, elite room, act boss), on which floor.</summary>
        public void NoteBetweenRunsEarned(int amount, string from, int floor) =>
            Note(SessionEventTypes.MetaEarn, from, "amount", Int(amount), "floor", Int(floor));

        /// <summary>A feat done for the first time, by its name in the save, when the run ended.</summary>
        public void NoteBetweenRunsFeat(string feat) => Note(SessionEventTypes.MetaFeat, feat);

        void OnDamaged(int amount)
        {
            if (!_recording) return;
            try { Add(new SessionEvent(Now(), SessionEventTypes.PlayerDamaged) { Damage = amount }); }
            catch (Exception e) { Fail(e); }
        }

        void OnDied()
        {
            if (!_recording) return;
            try { Add(new SessionEvent(Now(), SessionEventTypes.PlayerDeath)); }
            catch (Exception e) { Fail(e); }
        }
    }
}
