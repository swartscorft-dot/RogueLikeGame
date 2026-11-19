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
    /// <see cref="EnemyBrute.Reported"/>, <see cref="PlayerHealth"/>) and reads a few properties once a frame; it never
    /// changes what the game does. Anything that goes wrong (a full disk, a locked file) stops the recording and is said
    /// once in the console, and nothing is thrown into the game.
    /// </summary>
    public sealed class PlaySessionRecorder : MonoBehaviour
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
        readonly List<EnemyBrute> _enemies = new List<EnemyBrute>();
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

                float now = Time.time;
                // The gauge, read once a frame: when it becomes full, and when the empowered mode ends.
                bool full = _player.IsFocusFull;
                if (full && !_wasFull) Add(new SessionEvent(Now(), SessionEventTypes.FocusFull));
                _wasFull = full;
                bool empowered = _player.IsEmpowered;
                if (!empowered && _wasEmpowered) Add(new SessionEvent(Now(), SessionEventTypes.EmpoweredEnd));
                _wasEmpowered = empowered;

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
            _wasFull = player.IsFocusFull;
            _wasEmpowered = player.IsEmpowered;
            _nextSample = Time.time;
            _nextFlush = Time.unscaledTime + FlushSeconds;
            player.Reported += OnPlayer;
            Damageable.MarksLost += OnMarksLost;
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
            foreach (EnemyBrute enemy in FindObjectsByType<EnemyBrute>(FindObjectsSortMode.None))
            {
                if (_enemies.Contains(enemy)) continue;
                _enemies.Add(enemy);
                enemy.Reported += OnEnemy;
            }
        }

        void Detach()
        {
            if (_player != null) _player.Reported -= OnPlayer;
            Damageable.MarksLost -= OnMarksLost;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
            foreach (EnemyBrute enemy in _enemies)
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
            EnemyBrute enemy = _enemies.Count > 0 ? _enemies[0] : null;
            if (enemy != null)
            {
                AddFields(start, "enemy.", enemy.Values);
                if (enemy.Values.Attacks != null)
                    foreach (EnemyAttackDefinition attack in enemy.Values.Attacks)
                        if (attack != null) AddFields(start, "enemy." + attack.Name + ".", attack);
            }
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
                e.AddExtra("lock", s.Lock.ToString());
                if (s.MarksAdded > 0) e.AddExtra("marks_added", s.MarksAdded.ToString(CultureInfo.InvariantCulture));
                if (s.MarkDamageBonus > 0f) e.AddExtra("mark_bonus", Text(s.MarkDamageBonus));
                if (s.RecastCount > 1) e.AddExtra("recast_count", s.RecastCount.ToString(CultureInfo.InvariantCulture));
                Add(e);
            }
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

        double Now() => Time.time - _startTime;

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
            kind = target.GetComponent<EnemyBrute>() != null ? SessionWords.Enemy
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
                        Add(new SessionEvent(t, SessionEventTypes.RecastPress, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Shot = r.Shot, Clean = r.Clean, Focus = r.Clean ? r.Focus : (float?)null,
                        });
                        break;
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
                        Add(new SessionEvent(t, SessionEventTypes.Hit, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                            Damage = r.Damage, Target = TargetKind(r.Target), Empowered = r.Empowered,
                            Focus = r.Focus > 0f ? r.Focus : (float?)null,
                        });
                        break;
                    case PlayerReportKind.FocusPressed:
                        Add(new SessionEvent(t, SessionEventTypes.FocusPress) { Result = Word(r.Result) });
                        if (r.Result == IntentResult.Started) Add(new SessionEvent(t, SessionEventTypes.FocusSpent));
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
                switch (r.Kind)
                {
                    case EnemyReportKind.Woke:
                        Add(new SessionEvent(t, SessionEventTypes.EnemyWake));
                        break;
                    case EnemyReportKind.AttackStarted:
                        Add(new SessionEvent(t, SessionEventTypes.EnemyAttack, r.Attack));
                        break;
                    case EnemyReportKind.Struck:
                        Add(new SessionEvent(t, SessionEventTypes.EnemyStrike, r.Attack)
                        {
                            Result = r.Outcome == StrikeOutcome.Landed ? SessionWords.Landed
                                   : r.Outcome == StrikeOutcome.OutOfArea ? SessionWords.OutOfArea
                                   : r.Outcome == StrikeOutcome.AvoidedByDash ? SessionWords.AvoidedByDash
                                   : SessionWords.NoEffect,
                            Damage = r.Damage > 0 ? r.Damage : (int?)null,
                        });
                        break;
                    case EnemyReportKind.GaveUp:
                        Add(new SessionEvent(t, SessionEventTypes.EnemyGaveUp));
                        break;
                    case EnemyReportKind.Died:
                        Add(new SessionEvent(t, SessionEventTypes.EnemyDeath));
                        break;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
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
