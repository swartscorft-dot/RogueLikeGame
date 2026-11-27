using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 144, the sight in the game: the Windows arrow hidden while she plays and back for every screen, her death, the
    /// game's focus lost and the piece gone (a hidden arrow left in his Editor is a fault he meets at once); the dot on the
    /// cursor's point; the brackets at work order 143's cone; the arm frame by frame against the judge, in the manner of
    /// <see cref="CleanPressJudgedTests"/> (the game stepped at 1/60 s of game time, each frame's drawing worked out and compared
    /// with whether a press now would be clean); the switch off, the game as before; nothing allocated in a frame.
    /// </summary>
    public class SightPlayTests
    {
        const float Frame = 1f / 60f;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        Damageable _a;
        TimingAtTheSight _cue;
        PrecisionSight _precision;
        GameSight _sight;
        readonly List<GameObject> _made = new List<GameObject>();
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        bool _hadCue, _hadSound;
        int _cueWas, _soundWas;
        Vector2 _cursorScreen, _move;
        int _pressBits;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _hadCue = PlayerPrefs.HasKey(TimingCueSettings.CueKey);
            _cueWas = PlayerPrefs.GetInt(TimingCueSettings.CueKey, 0);
            _hadSound = PlayerPrefs.HasKey(TimingCueSettings.SoundKey);
            _soundWas = PlayerPrefs.GetInt(TimingCueSettings.SoundKey, 0);
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = Feed;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(10000000);
            _cue = TimingAtTheSight.Ensure();
            _precision = PrecisionSight.Ensure();
            _sight = GameSight.Ensure();
            _made.Add(GeneratedLevels.Ensure().gameObject); // the run's screens as in the game (its offers' setting among them)
            _made.Add(_cue.gameObject);
            _made.Add(_precision.gameObject);
            _made.Add(_sight.gameObject);
            _player.Reported += OnReport;
            TimingCueSettings.SetCue(TimingCue.Sight, keep: false);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Reported -= OnReport;
            if (_health != null) _health.Untouchable = false;
            if (_driver != null) _driver.InputSource = null;
            foreach (GameObject o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            SightValues.On = true;
            SightValues.PointerStays = true;
            GameSight.ConeForPictures = null;
            PrecisionSettings.ForTests = null;
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            if (_hadCue) PlayerPrefs.SetInt(TimingCueSettings.CueKey, _cueWas); else PlayerPrefs.DeleteKey(TimingCueSettings.CueKey);
            if (_hadSound) PlayerPrefs.SetInt(TimingCueSettings.SoundKey, _soundWas); else PlayerPrefs.DeleteKey(TimingCueSettings.SoundKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            Cursor.visible = true;
        }

        void OnReport(PlayerReport r) { if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r); }

        InputFrame? Feed()
        {
            var f = new InputFrame { CursorScreen = _cursorScreen, Move = _move, SkillsPressed = _pressBits, SkillsHeld = _pressBits };
            _pressBits = 0;
            return f;
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i] != null && _player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator Ready()
        {
            Time.captureDeltaTime = 0f;
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive && _player.RecastSlot < 0, 4f);
            _player.StartFresh();
            _player.Teleport(NearA);
            _move = Vector2.zero;
            yield return null;
            yield return null;
            _cursorScreen = Camera.main.WorldToScreenPoint(_a.transform.position + Vector3.up);
            yield return null;
            yield return null;
            Time.captureDeltaTime = Frame;
            _recasts.Clear();
        }

        IEnumerator Press(int slot)
        {
            int key = _player.KeyOfSkill(slot);
            if (key >= 0)
            {
                _pressBits = 1 << key;
                yield return null;
            }
            else
            {
                yield return null;
                _player.PressSkill(slot, _player.AimPoint);
            }
        }

        bool Waiting(int press) => _player.RecastSlot >= 0 && _player.RecastShots == press - 1;

        void Tick()
        {
            _cue.Tick();
            _precision.Tick();
            _sight.Tick();
        }

        // ---------------- 1. The Windows arrow ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheArrow_HiddenWhileShePlays_BackForAScreen_AChoice_LostFocus_AndWhenThePieceGoes()
        {
            yield return null;
            Tick();
            Assert.That(_sight.Shown, Is.True, "the sight drawn while she plays");
            Assert.That(GameSight.ArrowHidden, Is.True, "the Windows arrow hidden");
            Debug.Log("[144] Cursor.visible while the sight is drawn (batch mode): " + Cursor.visible);

            PlayerInputDriver.HeldByScreen = true; // the pause menu, an end screen, the start menu (work order 018, 114)
            yield return null;
            Tick();
            Assert.That(_sight.Shown, Is.False, "a screen up: no sight");
            Assert.That(GameSight.ArrowHidden, Is.False, "and the arrow back");
            Assert.That(Cursor.visible, Is.True);
            PlayerInputDriver.HeldByScreen = false;
            yield return null;
            Tick();
            Assert.That(GameSight.ArrowHidden, Is.True, "the screen down: hidden again");

            PlayerInputDriver.HeldByChoice = true; // an offer, a chest
            yield return null;
            Tick();
            Assert.That(GameSight.ArrowHidden, Is.False, "a choice up: the arrow");
            PlayerInputDriver.HeldByChoice = false;
            yield return null;
            Tick();
            Assert.That(GameSight.ArrowHidden, Is.True);

            _sight.FocusChanged(false); // the game without focus (alt-tab, a click in another window of the Editor)
            Assert.That(GameSight.ArrowHidden, Is.False, "focus lost: the arrow at once");
            Tick();
            Assert.That(GameSight.ArrowHidden, Is.False, "and while it is lost");
            _sight.FocusChanged(true);
            Tick();
            Assert.That(GameSight.ArrowHidden, Is.True, "focus back: hidden");

            _sight.enabled = false; // a scene left, Play ended: the piece stops
            Assert.That(GameSight.ArrowHidden, Is.False, "the piece off: the arrow");
            Assert.That(Cursor.visible, Is.True);
            _sight.enabled = true;
            Object.Destroy(_sight.gameObject);
            yield return null;
            Assert.That(GameSight.ArrowHidden, Is.False, "the piece gone: the arrow");
            Assert.That(Cursor.visible, Is.True);
        }

        // ---------------- 2. The dot and the opening ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDot_IsOnTheCursorsPoint_TheBracketsAtTheCone_SnugWithPrecisionOff()
        {
            yield return Ready();
            Time.captureDeltaTime = 0f;
            _cursorScreen = new Vector2(1000f, 400f);
            yield return null;
            yield return null;
            Tick();
            Vector2 c = _sight.Centre;
            float height = InterfaceStyle.ScreenHeight;
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[144] cursor fed at ({0}, {1}) from the bottom; the sight's middle at ({2:0.000}, {3:0.000}) from the top, screen {4}x{5}",
                                    _cursorScreen.x, _cursorScreen.y, c.x, c.y, InterfaceStyle.ScreenWidth, height));
            Assert.That(c.x, Is.EqualTo(_cursorScreen.x).Within(0.5f), "the dot on the cursor");
            Assert.That(c.y, Is.EqualTo(height - _cursorScreen.y).Within(0.5f));
            Assert.That(_sight.Shift, Is.EqualTo(0f), "precision Off (nobody at the keys): snug");
            Assert.That(GameSight.TakesTheBrackets, Is.True, "143's own brackets give way");

            // Precision in use and her running: the brackets at the cone's edges, as work order 143 measures them.
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            _cursorScreen = Camera.main.WorldToScreenPoint(_player.Position + new Vector3(8f, 0f, 0f));
            _move = new Vector2(0f, 1f);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Tick();
                if (_precision.Cone.Shown && _precision.Cone.Degrees > 6f) break;
            }
            ConeAtTheCursor cone = _precision.Cone;
            float scale = InterfaceStyle.ScreenHeight / 1080f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[144] running: cone {0:0.0} degrees at {1:0.0} m, {2:0.0} px across; the brackets out {3:0.0} layout px",
                                    cone.Degrees, cone.Distance, cone.Pixels, _sight.Shift));
            Assert.That(cone.Shown && cone.Degrees > 0f, Is.True, "a cone while she runs");
            Assert.That(_sight.ConePixels, Is.EqualTo(cone.Pixels), "the sight reads 143's one entry");
            Assert.That(_sight.Shift, Is.EqualTo(Mathf.Max(0f, cone.Pixels * 0.5f / scale - 24.5f)).Within(1e-3f), "the brackets' inner ink at the cone's edge");
            Assert.That(GameSight.PixelsAcross(Camera.main, _player.Position, _player.CursorOnItsRay, cone.Degrees), Is.EqualTo(cone.Pixels).Within(0.5f),
                        "the pictures' own measure is 143's");
            _move = Vector2.zero;
        }

        // ---------------- 3. The arm against the judge, frame by frame ----------------

        static IEnumerable<TestCaseData> Cases()
        {
            yield return new TestCaseData("Triple Tap", 2).SetName("Arm_TripleTap_Press2").Returns(null);
            yield return new TestCaseData("Triple Tap", 3).SetName("Arm_TripleTap_Press3").Returns(null);
            yield return new TestCaseData("Backstep", 2).SetName("Arm_Backstep_Press2").Returns(null);
        }

        struct Seen
        {
            public float Since, CleanFrom, CleanTo, Lead, Head, Start, End;
            public bool Open, Judge, Catch, Now, Late, Says;
            public bool OnEdge => Open && (Math.Abs(Since - CleanFrom) < 1e-4f || Math.Abs(Since - CleanTo) < 1e-4f);
        }

        [UnityTest, Timeout(120000)]
        [TestCaseSource(nameof(Cases))]
        public IEnumerator TheArmsCatch_IsInTheTeethExactlyWhileAPressWouldBeJudgedClean(string skill, int press)
        {
            int slot = Slot(skill);
            yield return Ready();
            yield return Press(slot);
            for (int p = 2; p < press; p++)
            {
                int q = p;
                yield return Until(() => !Waiting(q) || _player.IsRecastWindowOpen, 3f);
                CleanPart c = _player.RecastCleanPart;
                float mid = (c.From + c.To) * 0.5f;
                yield return Until(() => !Waiting(q) || _player.RecastSinceOpened >= mid - Frame, 3f);
                yield return Press(slot);
            }
            var seen = new List<Seen>();
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Waiting(press) && Time.realtimeSinceStartup < deadline)
            {
                Tick();
                CleanPart part = _player.RecastCleanPart;
                seen.Add(new Seen
                {
                    Since = _player.RecastSinceOpened, Open = _player.IsRecastWindowOpen, Judge = _player.IsRecastWindowOpen && _player.RecastPressWouldBeClean,
                    CleanFrom = part.From, CleanTo = part.To, Lead = _player.RecastLead, Head = _sight.HeadAngle, Start = _sight.ZoneStart, End = _sight.ZoneEnd,
                    Catch = _sight.CatchInZone, Now = _sight.Now, Late = _sight.Late, Says = _cue.SaysClean,
                });
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            Assert.That(seen.Count(s => s.Judge), Is.GreaterThan(0), "the judge saw clean frames");
            Assert.That(seen.All(s => s.Start > s.End), Is.True, "the zone drawn every frame of the wait");
            int judgeFirst = seen.FindIndex(s => s.Judge), judgeLast = seen.FindLastIndex(s => s.Judge);
            int nowFirst = seen.FindIndex(s => s.Now), nowLast = seen.FindLastIndex(s => s.Now);
            int wrong = seen.Count(s => !s.OnEdge && s.Now != s.Judge);
            int saysWrong = seen.Count(s => !s.OnEdge && s.Says != s.Judge);
            int catchBeforeOpen = seen.Count(s => !s.Open && s.Catch);
            int lateBack = seen.Count(s => s.Late && s.Catch);
            int lateWrong = seen.Count(s => s.Open && !s.OnEdge && s.Late != (s.Since > s.CleanTo));
            var log = new StringBuilder();
            log.AppendLine(string.Format(CultureInfo.InvariantCulture, "[144] {0} press {1}: {2} frames of 1/60 s from the press before to the window's end; zone {3:0.0} to {4:0.0} degrees; lead {5:0.000} s",
                                         skill, press, seen.Count, seen[judgeFirst].Start, seen[judgeFirst].End, seen[judgeFirst].Lead));
            log.AppendLine(string.Format(CultureInfo.InvariantCulture, "[144]   judge: frames {0} to {1} (since {2:0.0} to {3:0.0} ms); the sight's 'now': frames {4} to {5} ({6} and {7} frames against the judge); frames it disagrees: {8}; the cue's SaysClean disagrees: {9}; catch before the window opens: {10}; too late with the catch in the zone: {11}; too late misjudged: {12}",
                                         judgeFirst, judgeLast, seen[judgeFirst].Since * 1000f, seen[judgeLast].Since * 1000f, nowFirst, nowLast, nowFirst - judgeFirst, nowLast - judgeLast, wrong, saysWrong, catchBeforeOpen, lateBack, lateWrong));
            for (int i = Math.Max(0, judgeFirst - 2); i <= Math.Min(seen.Count - 1, judgeLast + 2); i++)
                if (i <= judgeFirst + 1 || i >= judgeLast - 1)
                    log.AppendLine(string.Format(CultureInfo.InvariantCulture, "[144]     frame {0}: since {1:0.0} ms, head {2:0.00}, catch's lead {3:0.00}, judge {4}, now {5}, late {6}",
                                                 i, seen[i].Since * 1000f, seen[i].Head, seen[i].Head - 6f, seen[i].Judge, seen[i].Now, seen[i].Late));
            Debug.Log(log.ToString());
            Assert.That(wrong, Is.EqualTo(0), "the sight's 'now' on exactly the judge's frames");
            Assert.That(saysWrong, Is.EqualTo(0), "the cue's SaysClean with the sight in use");
            Assert.That(nowFirst, Is.EqualTo(judgeFirst));
            Assert.That(nowLast, Is.EqualTo(judgeLast));
            Assert.That(catchBeforeOpen, Is.EqualTo(0), "never in the zone before the window opens");
            Assert.That(lateBack, Is.EqualTo(0), "too late: the arm back at its pin, never back into the zone");
            Assert.That(lateWrong, Is.EqualTo(0), "too late exactly after the clean part");
        }

        // ---------------- 4. The switch off ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSwitchOff_NoSight_TheArrowKept_143sBracketsDrawn_TheFourCues()
        {
            SightValues.On = false;
            yield return Ready();
            Time.captureDeltaTime = 0f;
            Tick();
            Assert.That(_sight.Shown, Is.False, "off: nothing drawn");
            Assert.That(GameSight.ArrowHidden, Is.False, "off: the Windows arrow");
            Assert.That(GameSight.TakesTheBrackets, Is.False, "off: 143's brackets as 143 made them");
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly), "a stored sight cue is the default with the switch off");
            Assert.That(TimingCueSettings.CueNow, Is.EqualTo(TimingCue.BarOnly));
            Assert.That(TimingCues.Next(TimingCue.Hammer), Is.EqualTo(TimingCue.BarOnly), "Y steps through the four of work order 134");
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines, Has.No.Member(SettingLine.SightOpening));
            int slot = Slot("Triple Tap");
            yield return Press(slot);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            _cue.Tick();
            Assert.That(_cue.SightSays, Is.True, "the rule is worked out, as the other cues' are");
            Assert.That(_cue.SaysClean, Is.False, "but the cue in use is the bar only");
            Tick();
            Assert.That(_sight.Timing || _sight.Shown, Is.False);
        }

        // ---------------- 5. Nothing allocated in a frame ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator AFrame_AllocatesNothing_StillOrWhileTheSightChanges()
        {
            yield return Ready();
            Time.captureDeltaTime = 0f;
            float scale = InterfaceStyle.ScreenHeight / 1080f;
            Tick();
            _sight.Build(scale);
            int builds = _sight.Builds;
            for (int i = 0; i < 5; i++) { Tick(); _sight.Build(scale); }
            Assert.That(_sight.Builds, Is.EqualTo(builds), "nothing changed: the mesh not filled again");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) { Tick(); _sight.Build(scale); }
            long still = GC.GetAllocatedBytesForCurrentThread() - before;
            // The opening changing every frame: the mesh filled again each time.
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                GameSight.ConeForPictures = 2f + (i % 40) * 0.5f;
                Tick();
                _sight.Build(scale);
            }
            long changing = GC.GetAllocatedBytesForCurrentThread() - before;
            int points = _sight.MeshPoints;
            GameSight.ConeForPictures = null;
            Debug.Log("[144] bytes allocated over 200 frames: still " + still + ", the opening changing every frame " + changing + "; the mesh's points " + points + "; fills " + _sight.Builds);
            Assert.That(still, Is.EqualTo(0L), "still: nothing allocated");
            Assert.That(changing, Is.EqualTo(0L), "changing: nothing allocated either");
            Assert.That(points, Is.GreaterThan(0).And.LessThan(65000), "one mesh of 16-bit indices");
        }
    }
}
