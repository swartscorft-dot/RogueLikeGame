using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 134, a look order's proof: pictures and clips of the three timing cues, taken off screen in batch mode at our
    /// camera (55 degrees, 28 m), 1920 by 1080, the interface drawn into each by hand (<see cref="ImguiByHand"/>) as work order 068's
    /// pictures are, and a stand-in for the mouse's arrow drawn where the cursor is (a batch run draws no cursor of the system's).
    /// For each moment the same frame is taken four times: the bar only, the cylinder, the ring, the hammer. Triple Tap's chain at
    /// six moments (before the window, entering the clean part, the second press made clean, the wait before the third, the third
    /// pressed clean, a miss), Backstep's at three (the hop, its clean part, its clean press), and a floor-5 fight (the benchmark's
    /// fullest room) with the cursor on an enemy. Then a clip a cue, every frame at a steady 30 a second of game time (work order
    /// 126's way): two chains of Triple Tap (pressed clean; then the second pressed early and the third missed) and Backstep's
    /// clean press. Nothing here judges them: they are looked at.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_134 names a folder outside the project (otherwise ignored):
    ///   $env:ROGUELIKE_CAPTURE_134 = 'C:\Users\kriss\.claude-automation\runner\captures\134'
    ///   run-tests.ps1 PlayMode -Filter "TimingAtTheSightPicturesTests"
    /// </summary>
    public class TimingAtTheSightPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_134";
        const int W = 1920, H = 1080;
        const float PictureStep = 1f / 60f; // s of game time a frame while pictures are staged: a long frame (a picture written) is no jump
        const float ClipStep = 1f / 30f;    // s of game time a frame of a clip: a steady 30 a second
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly TimingCue[] All = { TimingCue.BarOnly, TimingCue.Cylinder, TimingCue.Ring, TimingCue.Hammer };

        /// <summary>A stand-in for the system's arrow at the cursor, drawn over everything (the system draws its own over the game).</summary>
        sealed class ArrowStandIn : MonoBehaviour
        {
            public TimingAtTheSight Cue;
            public Vector2 At;
            Texture2D _arrow;

            // The system's arrow at 100 %, 12 by 19: X its black edge, . its white inside.
            static readonly string[] Rows =
            {
                "X           ", "XX          ", "X.X         ", "X..X        ", "X...X       ", "X....X      ", "X.....X     ",
                "X......X    ", "X.......X   ", "X........X  ", "X.........X ", "X......XXXXX", "X...X..X    ", "X..XX..X    ",
                "X.X  X..X   ", "XX   X..X   ", "X     X..X  ", "      X..X  ", "       XX   ",
            };

            void Awake()
            {
                _arrow = new Texture2D(12, Rows.Length, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < Rows.Length; y++)
                    for (int x = 0; x < 12; x++)
                    {
                        char c = x < Rows[y].Length ? Rows[y][x] : ' ';
                        _arrow.SetPixel(x, Rows.Length - 1 - y, c == 'X' ? Color.black : c == '.' ? Color.white : Color.clear);
                    }
                _arrow.Apply();
            }

            void OnDestroy() { if (_arrow != null) Destroy(_arrow); }

            void OnGUI()
            {
                if (Event.current.type != EventType.Repaint) return;
                GUI.depth = -100;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(Mathf.Round(At.x), Mathf.Round(At.y), 12f, Rows.Length), _arrow);
            }
        }

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Damageable _a;
        GeneratedLevels _levels;
        TimingAtTheSight _cue;
        CastBar _cast;
        SkillBar _bar;
        EnemyBars _bars;
        TimingFeedback _feedback;
        PlayerSigns _signs;
        CombatEffects _combat;
        ArrowStandIn _arrow;
        string _folder;
        RenderTexture _rt;
        Texture2D _read;
        Vector2 _cursorScreen;
        readonly List<string> _log = new List<string>();
        bool _hadCue, _hadSound;
        int _cueWas, _soundWas;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 134)");
            Directory.CreateDirectory(_folder);
            _hadCue = PlayerPrefs.HasKey(TimingCueSettings.CueKey);
            _cueWas = PlayerPrefs.GetInt(TimingCueSettings.CueKey, 0);
            _hadSound = PlayerPrefs.HasKey(TimingCueSettings.SoundKey);
            _soundWas = PlayerPrefs.GetInt(TimingCueSettings.SoundKey, 0);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = _player.GetComponent<PlayerInputDriver>();
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(10000000);
            _levels = GeneratedLevels.Ensure();
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _cast = CastBar.Ensure();
            _bars = EnemyBars.Ensure();
            _feedback = TimingFeedback.Ensure();
            _signs = PlayerSigns.Ensure();
            _combat = CombatEffects.Ensure();
            FiringWeight.Ensure();
            _cue = TimingAtTheSight.Ensure();
            _arrow = new GameObject("ArrowStandIn").AddComponent<ArrowStandIn>();
            _arrow.Cue = _cue;
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
            _driver.InputSource = Feed;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "134-log.txt"), _log);
            _log.Clear();
            Time.captureDeltaTime = 0f;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_driver != null) _driver.InputSource = null;
            if (_levels != null)
            {
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _cue, _cast, _bars, _feedback, _signs, _combat, _arrow })
                if (m != null) Object.Destroy(m.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_read != null) Object.Destroy(_read);
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            if (_hadCue) PlayerPrefs.SetInt(TimingCueSettings.CueKey, _cueWas); else PlayerPrefs.DeleteKey(TimingCueSettings.CueKey);
            if (_hadSound) PlayerPrefs.SetInt(TimingCueSettings.SoundKey, _soundWas); else PlayerPrefs.DeleteKey(TimingCueSettings.SoundKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        InputFrame? Feed() => new InputFrame { CursorScreen = _cursorScreen };

        /// <summary>The cursor on this point of the world (an enemy's chest), as the input driver will read it.</summary>
        void CursorOn(Vector3 world)
        {
            Camera cam = Camera.main;
            if (cam != null) _cursorScreen = cam.WorldToScreenPoint(world);
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator GameSeconds(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator Ready(Vector3 stand)
        {
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            _player.StartFresh();
            _player.Teleport(stand);
            for (int i = 0; i < 20; i++) yield return null; // the camera settles on her
        }

        // ---------------- Taking a picture ----------------

        /// <summary>The game's camera and the interface into the texture, every piece's frame worked out for it first.</summary>
        void Draw()
        {
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = _rt;
                if (_bar != null) _bar.Tick();
                _cast.Tick();
                _bars.Tick();
                _feedback.Tick();
                _signs.Tick();
                _cue.Tick();
                _arrow.At = _cue.Sight; // where the cue puts the cursor (her cursor's ray through this camera)
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(_rt);
            }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
        }

        void Save(string path, bool png)
        {
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            _read.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(path, png ? _read.EncodeToPNG() : _read.EncodeToJPG(92));
        }

        /// <summary>This frame four times: the bar only and each cue.</summary>
        void Moment(string name)
        {
            foreach (TimingCue cue in All)
            {
                TimingCueSettings.SetCue(cue, keep: false);
                try
                {
                    Draw();
                    Save(Path.Combine(_folder, name + "-" + cue.ToString().ToLowerInvariant() + ".png"), true);
                }
                catch (Exception e) { _log.Add(name + " " + cue + " FAILED: " + e); }
            }
            _log.Add(string.Format(Inv,
                "{0}: sight ({1:0}, {2:0}); press {3}, since opened {4:0.000} s, window {5:0.00}, clean {6:0.000} to {7:0.000}; judge lit {8}; cylinder says {9} (turn {10:0.0}, chamber {11} at {12:0.0} deg, notch {13:0.0}), ring says {14} (radius {15:0.0} px), hammer says {16} (level {17:0.00}); alpha {18:0.00}; recast timings 2 {19} 3 {20}",
                name, _cue.Sight.x, _cue.Sight.y, _cue.Press, _cue.SinceOpened, _cue.Window, _cue.Clean.From, _cue.Clean.To, _cue.Lit, _cue.CylinderSays, _cue.Turn,
                _cue.LiveChamber, _cue.LiveAngle, _cue.Notch, _cue.RingSays, _cue.RingRadius, _cue.HammerSays, _cue.HammerLevel, _cue.Alpha,
                _player.RecastTimingOf(2), _player.RecastTimingOf(3)));
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
        }

        // ---------------- The pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator Pictures_TripleTapAtSixMoments_BackstepAtThree_AFloor5Fight_EachCueAndTheBarOnly()
        {
            Time.captureDeltaTime = PictureStep;
            Camera cam = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); work order 134; camera pitch {cam.transform.eulerAngles.x:0.0}, field of view {cam.fieldOfView:0}, {W}x{H}; game time {PictureStep:0.0000} s a frame");
            int tap = Slot("Triple Tap"), back = Slot("Backstep");
            _player.EquipSkill(tap);
            _player.EquipSkill(back);
            Vector3 chest = _a.transform.position + Vector3.up * 1.1f;

            // Triple Tap, pressed clean twice.
            yield return Ready(NearA);
            CursorOn(chest);
            yield return null;
            yield return null;
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started), "the first press");
            yield return null;
            Moment("tt1-before");
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastPressWouldBeClean, 3f);
            Moment("tt2-entering");
            _player.PressSkill(tap, _a.transform.position);
            yield return null;
            Moment("tt3-second-press");
            yield return Until(() => _player.RecastShots == 2 && _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.25f, 3f);
            Moment("tt4-wait-third");
            yield return Until(() => _player.RecastShots == 2 && _player.RecastSinceOpened >= 0.52f, 3f);
            _player.PressSkill(tap, _a.transform.position);
            yield return null;
            Moment("tt5-clean-press");

            // Triple Tap, the third missed.
            yield return Ready(NearA);
            CursorOn(chest);
            _player.PressSkill(tap, _a.transform.position);
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastPressWouldBeClean, 3f);
            yield return GameSeconds(0.05f);
            _player.PressSkill(tap, _a.transform.position);
            yield return Until(() => _player.RecastSlot < 0, 3f);
            yield return GameSeconds(0.08f);
            Moment("tt6-miss");

            // Backstep.
            yield return Ready(NearA);
            CursorOn(chest);
            Assert.That(_player.PressSkill(back, _a.transform.position), Is.EqualTo(IntentResult.Started), "the hop");
            yield return GameSeconds(0.05f);
            Moment("bs1-before");
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.12f, 3f);
            Moment("bs2-clean");
            _player.PressSkill(back, _a.transform.position);
            yield return null;
            Moment("bs3-clean-press");

            // A floor-5 fight: the benchmark's fullest room, the cursor on the nearest enemy, Triple Tap at it.
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assume.That(stage, Is.Not.Null);
            _driver.InputSource = Feed;
            for (int i = 0; i < 90; i++) { stage.Keep(); yield return null; } // a second and a half: the room awake and coming
            Enemy target = stage.Target();
            Assume.That(target, Is.Not.Null, "an enemy to aim at");
            _player.StartFresh();
            int tap5 = Slot("Triple Tap");
            CursorOn(target.transform.position + Vector3.up * 1.1f);
            yield return null;
            CursorOn(target.transform.position + Vector3.up * 1.1f);
            _player.PressSkill(tap5, target.transform.position);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!(_player.IsRecastWindowOpen && _player.RecastPressWouldBeClean) && Time.realtimeSinceStartup < deadline)
            {
                stage.Keep();
                if (target != null) CursorOn(target.transform.position + Vector3.up * 1.1f);
                yield return null;
            }
            Moment("f5-lit");
            _player.PressSkill(tap5, target != null ? target.transform.position : _player.AimPoint);
            deadline = Time.realtimeSinceStartup + 5f;
            while (!(_player.RecastShots == 2 && _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.25f) && Time.realtimeSinceStartup < deadline)
            {
                stage.Keep();
                if (target != null) CursorOn(target.transform.position + Vector3.up * 1.1f);
                yield return null;
            }
            Moment("f5-wait-third");
            _log.Add(string.Format(Inv, "f5: the benchmark's floor 5 fight (seed BENCH032, its fullest combat room), {0} enemies, the cursor on a {1}", stage.Fighting.Count, target != null ? target.Kind.ToString() : "gone enemy"));
            stage.End();
            Time.captureDeltaTime = 0f;
        }

        // ---------------- The clips ----------------

        int _frame;
        string _seq;

        IEnumerator Frame()
        {
            yield return null;
            try
            {
                Draw();
                Save(Path.Combine(_folder, "frames", _seq, _frame.ToString("0000", Inv) + ".jpg"), false);
            }
            catch (Exception e) { _log.Add(_seq + " frame " + _frame + " FAILED: " + e.Message); }
            _frame++;
        }

        IEnumerator Frames(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return Frame();
        }

        IEnumerator FramesUntil(Func<bool> condition, float most)
        {
            float end = Time.time + most;
            while (!condition() && Time.time < end) yield return Frame();
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Clips_ACueEach_TwoTripleTapChainsAndBackstep_At30AFrame()
        {
            int tap = Slot("Triple Tap"), back = Slot("Backstep");
            _player.EquipSkill(tap);
            _player.EquipSkill(back);
            Vector3 chest = _a.transform.position + Vector3.up * 1.1f;
            _log.Add($"clips: {DateTime.Now:yyyy-MM-dd HH:mm}, game time {ClipStep:0.0000} s a frame, {W}x{H}, the game's camera");
            foreach (TimingCue cue in new[] { TimingCue.Cylinder, TimingCue.Ring, TimingCue.Hammer })
            {
                Time.captureDeltaTime = ClipStep;
                yield return Ready(NearA);
                CursorOn(chest);
                TimingCueSettings.SetCue(cue, keep: false);
                _seq = cue.ToString().ToLowerInvariant();
                _frame = 0;
                Directory.CreateDirectory(Path.Combine(_folder, "frames", _seq));
                yield return Frames(0.3f);
                // 1. Pressed clean, twice (in the middle of each clean part).
                _player.PressSkill(tap, _a.transform.position);
                yield return FramesUntil(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.20f, 2f);
                _player.PressSkill(tap, _a.transform.position);
                yield return FramesUntil(() => _player.RecastShots == 2 && _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.54f, 2f);
                _player.PressSkill(tap, _a.transform.position);
                yield return Frames(0.8f);
                // 2. The second pressed early (in its window, before its clean part), the third missed.
                _player.StartFresh();
                _player.PressSkill(tap, _a.transform.position);
                yield return FramesUntil(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.04f, 2f);
                _player.PressSkill(tap, _a.transform.position);
                yield return FramesUntil(() => _player.RecastSlot < 0, 3f);
                yield return Frames(0.8f);
                // 3. Backstep, its shot pressed clean.
                _player.StartFresh();
                _player.PressSkill(back, _a.transform.position);
                yield return FramesUntil(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.13f, 2f);
                _player.PressSkill(back, _a.transform.position);
                yield return Frames(0.8f);
                _log.Add(string.Format(Inv, "clip {0}: {1} frames; the sight at ({2:0}, {3:0}); Triple Tap's timings of the second chain: 2 {4}, 3 {5}",
                                       _seq, _frame, _cue.Sight.x, _cue.Sight.y, _player.RecastTimingOf(2), _player.RecastTimingOf(3)));
                Time.captureDeltaTime = 0f;
            }
        }
    }
}
