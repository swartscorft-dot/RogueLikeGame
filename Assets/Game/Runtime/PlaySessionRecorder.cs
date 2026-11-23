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
                    EndScreen(); // work order 090: a screen still up, with its seconds
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
    }
}
