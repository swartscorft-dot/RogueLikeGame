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
    /// Work order 144, a look order's proof: the sight taken off screen in batch mode at our camera (55 degrees, 28 m), 1920 by
    /// 1080, the interface drawn into each picture by hand (<see cref="ImguiByHand"/>) as work order 134's pictures are, to be
    /// looked at at 100 % against part A's tiles (<c>codex\images\sight\round2\e_*_dimline.png</c>). Three grounds: a floor-5
    /// fight's dark floor (the benchmark's fullest room) with the cursor 10 m east of her, the practice room's light floor
    /// with the cursor 10 m east, and the floor-5 fight with the cursor on an enemy carrying Triple Tap's mark diamonds. On each:
    /// at rest closed, open at 12 and 24 degrees, the whole ring spread at 24 (<see cref="SightValues.PointerStays"/> off), and a
    /// real Triple Tap chain's last press started (0.25 s after the press before), now (the middle of its clean part) and too
    /// late, and Backstep's shot now, each closed and at 24 degrees. Every picture's numbers go to <c>144-pictures.csv</c>.
    /// Nothing here judges them: they are looked at.
    ///
    /// Runs ONLY when the environment variable ROGUELIKE_CAPTURE_144 names a folder outside the project (otherwise ignored):
    ///   $env:ROGUELIKE_CAPTURE_144 = 'C:\Users\kriss\.claude-automation\runner\captures\144'
    ///   run-tests.ps1 PlayMode -Filter "SightPicturesTests"
    /// </summary>
    public class SightPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_144";
        const int W = 1920, H = 1080;
        const float PictureStep = 1f / 60f; // s of game time a frame while pictures are staged
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Damageable _a;
        GeneratedLevels _levels;
        TimingAtTheSight _cue;
        PrecisionSight _precision;
        GameSight _sight;
        CastBar _cast;
        SkillBar _bar;
        EnemyBars _bars;
        TimingFeedback _feedback;
        PlayerSigns _signs;
        CombatEffects _combat;
        string _folder;
        RenderTexture _rt;
        Texture2D _read;
        Vector2 _cursorScreen;
        Func<Vector3> _aim;
        BenchmarkStage _stage;
        readonly List<string> _log = new List<string>();
        readonly List<string> _rows = new List<string>();
        bool _hadCue;
        int _cueWas;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 144)");
            Directory.CreateDirectory(_folder);
            _hadCue = PlayerPrefs.HasKey(TimingCueSettings.CueKey);
            _cueWas = PlayerPrefs.GetInt(TimingCueSettings.CueKey, 0);
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
            _precision = PrecisionSight.Ensure();
            _sight = GameSight.Ensure();
            TimingCueSettings.SetCue(TimingCue.Sight, keep: false);
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _read = new Texture2D(W, H, TextureFormat.RGB24, false);
            _driver.InputSource = Feed;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "144-log.txt"), _log);
            if (_folder != null && _rows.Count > 0)
            {
                string csv = Path.Combine(_folder, "144-pictures.csv");
                if (!File.Exists(csv)) File.WriteAllText(csv, "name,cx,cy,shift,pointer_stays,zone,zone_start,zone_end,head,now,late,press,since,lead,clean_from,clean_to,window,cone_deg,cone_px\n");
                File.AppendAllLines(csv, _rows);
            }
            _log.Clear();
            _rows.Clear();
            if (_stage != null) { _stage.End(); _stage = null; }
            Time.captureDeltaTime = 0f;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            GameSight.ConeForPictures = null;
            SightValues.On = true;
            SightValues.PointerStays = true;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_driver != null) _driver.InputSource = null;
            if (_levels != null)
            {
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _cue, _precision, _sight, _cast, _bars, _feedback, _signs, _combat })
                if (m != null) Object.Destroy(m.gameObject);
            if (FiringWeight.Current != null) Object.Destroy(FiringWeight.Current.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            if (_read != null) Object.Destroy(_read);
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            if (_hadCue) PlayerPrefs.SetInt(TimingCueSettings.CueKey, _cueWas); else PlayerPrefs.DeleteKey(TimingCueSettings.CueKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            Cursor.visible = true;
        }

        InputFrame? Feed() => new InputFrame { CursorScreen = _cursorScreen };

        /// <summary>The cursor on the aim's point this frame (the ground 10 m from her, or an enemy's chest), as the input driver will read it.</summary>
        void Aim()
        {
            Camera cam = Camera.main;
            if (cam != null && _aim != null) _cursorScreen = cam.WorldToScreenPoint(_aim());
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i] != null && _player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        /// <summary>Frames until the condition (at most <paramref name="timeoutSeconds"/> real seconds), the aim followed and the fight kept.</summary>
        IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                if (_stage != null) _stage.Keep();
                Aim();
                yield return null;
            }
        }

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (_stage != null) _stage.Keep();
                Aim();
                yield return null;
            }
        }

        // ---------------- Taking a picture ----------------

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
                _precision.Tick();
                _sight.Tick();
                cam.Render();
                if (ImguiByHand.Available) ImguiByHand.Draw(_rt);
            }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            }
        }

        void Save(string name)
        {
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            _read.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), _read.EncodeToPNG());
        }

        /// <summary>This frame as one picture: the cone (null: hers), the opening.</summary>
        void Shot(string name, float? cone, bool pointerStays = true)
        {
            GameSight.ConeForPictures = cone;
            SightValues.PointerStays = pointerStays;
            try
            {
                Draw();
                Save(name);
                GameSight s = _sight;
                _rows.Add(string.Format(Inv, "{0},{1:0.###},{2:0.###},{3:0.###},{4},{5},{6:0.###},{7:0.###},{8:0.###},{9},{10},{11},{12:0.####},{13:0.####},{14:0.####},{15:0.####},{16:0.####},{17:0.###},{18:0.###}",
                                        name, s.Centre.x, s.Centre.y, s.Shift, s.PointerStays ? 1 : 0, s.ZoneShown ? 1 : 0, s.ZoneStart, s.ZoneEnd, s.HeadAngle, s.Now ? 1 : 0, s.Late ? 1 : 0,
                                        _cue.Press, _cue.Since, _player.RecastLead, _cue.Clean.From, _cue.Clean.To, _cue.Window, s.ConeDegrees, s.ConePixels));
                _log.Add(string.Format(Inv, "{0}: sight ({1:0}, {2:0}), shown {3}, cone {4:0.0} deg = {5:0.0} px, brackets out {6:0.0} px, pointer stays {7}; press {8}, since {9:0.000} s, judge lit {10}; zone {11}: {12:0.0} to {13:0.0}, head {14:0.0}, now {15}, late {16}; mesh points {17}",
                                        name, s.Centre.x, s.Centre.y, s.Shown, s.ConeDegrees, s.ConePixels, s.Shift, s.PointerStays, _cue.Press, _cue.Since, _cue.Lit,
                                        s.ZoneShown, s.ZoneStart, s.ZoneEnd, s.HeadAngle, s.Now, s.Late, s.MeshPoints));
            }
            catch (Exception e) { _log.Add(name + " FAILED: " + e); }
            GameSight.ConeForPictures = null;
            SightValues.PointerStays = true;
            if (cone.HasValue || !pointerStays) return;
            // The same frame without the sight (its switch off for one picture): the ground part A's sight is drawn on for the comparison.
            SightValues.On = false;
            try
            {
                Draw();
                Save(name + "-bare");
            }
            catch (Exception e) { _log.Add(name + " bare FAILED: " + e); }
            finally { SightValues.On = true; }
        }

        /// <summary>The ground's set on one ground: at rest, opened, the whole ring, a Triple Tap chain's last press and Backstep's shot.</summary>
        IEnumerator Set(string ground, Func<Vector3> target)
        {
            int tap = Slot("Triple Tap"), back = Slot("Backstep");
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive && _player.RecastSlot < 0, 3f);
            yield return Frames(3);
            Shot(ground + "-01-rest", null);
            Shot(ground + "-02-open12", 12f);
            Shot(ground + "-03-open24", 24f);
            Shot(ground + "-04-wholering-open24", 24f, false);

            // Triple Tap: the second press in the middle of its clean part, then the last press's wait.
            _player.PressSkill(tap, target());
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 3f);
            _player.PressSkill(tap, target());
            yield return Until(() => _player.RecastShots == 2 && _player.IsRecastWindowOpen && _player.RecastSinceOpened >= 0.10f, 3f);
            Shot(ground + "-05-started", null);
            Shot(ground + "-05-started-open24", 24f);
            yield return Until(() => _player.RecastShots == 2 && _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 3f);
            Shot(ground + "-06-now-tt3", null);
            Shot(ground + "-06-now-tt3-open24", 24f);
            Shot(ground + "-06-now-tt3-wholering-open24", 24f, false);
            yield return Until(() => _player.RecastShots == 2 && _player.RecastSinceOpened >= _player.RecastCleanTo + 0.08f, 3f);
            Shot(ground + "-07-late", null);
            Shot(ground + "-07-late-open24", 24f);
            yield return Until(() => _player.RecastSlot < 0 && _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            yield return Frames(3);
            Shot(ground + "-08-rest-after", null);

            // Backstep: its shot in the middle of its clean part.
            _player.StartFresh();
            yield return Frames(2);
            _player.PressSkill(back, target());
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 3f);
            Shot(ground + "-09-now-backstep", null);
            Shot(ground + "-09-now-backstep-open24", 24f);
            yield return Until(() => _player.RecastSlot < 0 && _player.Action == ActionKind.None && !HitStop.IsActive, 3f);
            _player.StartFresh();
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Pictures_ThreeGrounds_RestOpenedTheWholeRing_TripleTapsLastPressAndBackstep()
        {
            Time.captureDeltaTime = PictureStep;
            Camera cam = Camera.main;
            _log.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}); work order 144; camera pitch {cam.transform.eulerAngles.x:0.0}, field of view {cam.fieldOfView:0}, {W}x{H}");
            int tap = Slot("Triple Tap"), back = Slot("Backstep");
            _player.EquipSkill(tap);
            _player.EquipSkill(back);

            // 1. The practice room's light floor, the cursor 10 m east of her.
            _player.StartFresh();
            _player.Teleport(NearA);
            for (int i = 0; i < 20; i++) yield return null;
            Func<Vector3> east = () => _player.Position + new Vector3(10f, 0f, 0f); // follows her (Backstep's hop moves her and the camera)
            _aim = east;
            yield return Set("light", east);

            // 2 and 3. The benchmark's floor-5 fight: the cursor 10 m east of her, then on an enemy.
            _stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assume.That(_stage, Is.Not.Null);
            _driver.InputSource = Feed;
            _aim = east; // 10 m east of her: north-east put the cursor 127 px from the top, the opened brackets off the screen
            yield return Frames(90); // a second and a half: the room awake and coming
            _player.StartFresh();
            yield return Set("dark", east);
            Enemy target = _stage.Target();
            Assume.That(target, Is.Not.Null, "an enemy to aim at");
            Vector3 last = target.transform.position + Vector3.up * 1.1f;
            Func<Vector3> chest = () =>
            {
                if (target != null && !target.IsDead) last = target.transform.position + Vector3.up * 1.1f;
                return last;
            };
            _aim = chest;
            yield return Frames(10);
            yield return Set("enemy", chest);
            _log.Add(string.Format(Inv, "the floor-5 fight: the benchmark's (seed BENCH032, its fullest combat room), {0} enemies, the cursor on a {1}", _stage.Fighting.Count, target != null ? target.Kind.ToString() : "gone enemy"));
            _stage.End();
            _stage = null;
            Time.captureDeltaTime = 0f;
        }
    }
}
