using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
    /// Work order 142 (Kristian, 2026-11-26, playing main 13c1fe0 with the ring cue on: "selvom jeg for sirkelen innenfor
    /// strekene får jeg feil. det samme gjelder den opprinnelige baren og"): what each press of Triple Tap and Backstep is told,
    /// and whether every drawing of a timed press (the combo bar, the old cast bar, the skill box, the clean window's sign, and
    /// work order 134's cylinder, ring and hammer) enters and leaves its clean part on the very frame the judge does.
    ///
    /// Measured, not supposed: the game is stepped at a fixed 1/60 s of game time a frame (<see cref="Time.captureDeltaTime"/>),
    /// each frame's drawings worked out (their own Tick, as their LateUpdate does) and compared with the judgement of a press
    /// made now; then presses are made through the input driver on the frame AFTER a drawing says "now" (the earliest a player
    /// can answer a picture), and their judgement read. Once with the shot landing on Dummy_A (its hit stop of 0.05 s in the
    /// window's clock) and once aimed at nothing (no hit stop). Every number is written to the test's log with "[142]".
    /// </summary>
    public class CleanPressJudgedTests
    {
        const float Frame = 1f / 60f;          // s of game time a frame here: a person's 60 frames a second
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here (PlayerSignsTests')

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        Damageable _a;
        TimingAtTheSight _cue;
        CastBar _cast;
        PlayerSigns _signs;
        TimingFeedback _feedback;
        PlaceholderSounds _sounds;
        SkillBar _bar;
        readonly List<GameObject> _made = new List<GameObject>();
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<PlaceholderSound> _heard = new List<PlaceholderSound>();
        readonly List<string> _words = new List<string>();
        readonly List<FeedbackTone> _tones = new List<FeedbackTone>();
        readonly List<CastBarFlash> _flashes = new List<CastBarFlash>();

        bool _hadCue, _hadSound;
        int _cueWas, _soundWas;

        Vector2 _cursorScreen;
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
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.BladeWithCombo = false;
            CleanPressAnswers.On = true;
            PlaceholderSounds.Requested += OnSound;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(10000000);
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _cue = TimingAtTheSight.Ensure();
            _cast = CastBar.Ensure();
            _signs = PlayerSigns.Ensure();
            _feedback = TimingFeedback.Ensure();
            _sounds = PlaceholderSounds.Ensure();
            _made.Add(_cue.gameObject);
            _made.Add(_cast.gameObject);
            _made.Add(_signs.gameObject);
            _made.Add(_feedback.gameObject);
            _made.Add(_sounds.gameObject);
            _player.Reported += OnReport;
            _feedback.WordShown += OnWord;
            _feedback.TonePlayed += OnTone;
            _cast.Flashed += OnFlash;
            TimingCueSettings.SetCue(TimingCue.Ring, keep: false);
            TimingCueSettings.SetSound(true, keep: false);
            yield return null;
            ClearHeard();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= OnSound;
            if (_player != null) _player.Reported -= OnReport;
            if (_feedback != null) { _feedback.WordShown -= OnWord; _feedback.TonePlayed -= OnTone; }
            if (_cast != null) _cast.Flashed -= OnFlash;
            if (_health != null) _health.Untouchable = false;
            foreach (GameObject o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false);
            TimingCueSettings.ForgetSound(keep: false);
            if (_hadCue) PlayerPrefs.SetInt(TimingCueSettings.CueKey, _cueWas); else PlayerPrefs.DeleteKey(TimingCueSettings.CueKey);
            if (_hadSound) PlayerPrefs.SetInt(TimingCueSettings.SoundKey, _soundWas); else PlayerPrefs.DeleteKey(TimingCueSettings.SoundKey);
            PlayerPrefs.Save();
            if (GameSettings.IsLoaded) GameSettings.Unload();
            CleanPressAnswers.On = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnReport(PlayerReport r) { if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r); }
        void OnSound(PlaceholderSoundRequest r) => _heard.Add(r.Sound);
        void OnWord(FeedbackShown w) => _words.Add(w.Text);
        void OnTone(FeedbackTone t) => _tones.Add(t);
        void OnFlash(CastBarFlash f) => _flashes.Add(f);

        void ClearHeard()
        {
            _recasts.Clear();
            _heard.Clear();
            _words.Clear();
            _tones.Clear();
            _flashes.Clear();
        }

        // ---------------- Helpers ----------------

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i] != null && _player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        InputFrame? Feed()
        {
            var f = new InputFrame { CursorScreen = _cursorScreen, SkillsPressed = _pressBits, SkillsHeld = _pressBits };
            _pressBits = 0;
            return f;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Her fresh near Dummy_A, the cursor on it (<paramref name="hit"/>) or on the floor where no target stands.</summary>
        IEnumerator Ready(bool hit)
        {
            Time.captureDeltaTime = 0f;
            _driver.InputSource = () => null;
            yield return Until(() => _player.Action == ActionKind.None && !HitStop.IsActive && _player.RecastSlot < 0, 4f);
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            yield return null;
            Camera cam = Camera.main;
            Vector3 at = hit ? _a.transform.position + Vector3.up : _player.Position + AwayFromEveryTarget() * 6f;
            _cursorScreen = cam.WorldToScreenPoint(at);
            _driver.InputSource = Feed;
            yield return null;
            yield return null;
            Time.captureDeltaTime = Frame; // from here every frame is 1/60 s of game time
            ClearHeard();
        }

        /// <summary>The flat way from her that points farthest from every target there is (so a shot that way hits nothing).</summary>
        Vector3 AwayFromEveryTarget()
        {
            Vector3 best = Vector3.forward;
            float bestAngle = -1f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var way = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float least = 360f;
                foreach (Damageable d in Damageable.All)
                {
                    if (d == null) continue;
                    Vector3 to = d.transform.position - _player.Position;
                    to.y = 0f;
                    if (to.sqrMagnitude < 1e-4f) continue;
                    least = Mathf.Min(least, Vector3.Angle(way, to));
                }
                if (least > bestAngle) { bestAngle = least; best = way; }
            }
            return best;
        }

        /// <summary>A press of the slot's key, read by the input driver in the next frame (as a player's is), or straight in that frame when the skill is on no key.</summary>
        IEnumerator Press(int slot)
        {
            int key = _player.KeyOfSkill(slot);
            if (key >= 0)
            {
                _pressBits = 1 << key;
                yield return null; // the driver reads it in this frame's Update, before anything of the frame is drawn
            }
            else
            {
                yield return null;
                _player.PressSkill(slot, _player.AimPoint);
            }
        }

        bool Waiting(int press) => _player.RecastSlot >= 0 && _player.RecastShots == press - 1;

        /// <summary>Frames until press <paramref name="press"/>'s window is open and its clock has reached <paramref name="since"/> less a frame, then the press (it lands about there).</summary>
        IEnumerator PressAt(int slot, int press, float since)
        {
            yield return Until(() => !Waiting(press) || (_player.IsRecastWindowOpen && _player.RecastSinceOpened >= since - Frame), 3f);
            Assert.That(Waiting(press), Is.True, "press " + press + " still waited for");
            yield return Press(slot);
        }

        /// <summary>The presses before <paramref name="measured"/>, each at the middle of its clean part.</summary>
        IEnumerator PressesBefore(int slot, int measured)
        {
            yield return Press(slot);
            for (int p = 2; p < measured; p++)
            {
                yield return Until(() => !Waiting(p) || _player.IsRecastWindowOpen, 3f);
                CleanPart c = _player.RecastCleanPart;
                yield return PressAt(slot, p, (c.From + c.To) * 0.5f);
                Assert.That(_recasts.Count > 0 && _recasts.Last().Timing == RecastTiming.Clean, Is.True, "press " + p + " at its middle is clean");
            }
        }

        // ---------------- One frame's drawings ----------------

        struct Seen
        {
            public int Index;
            public float Since, Real;
            public bool Open, Judge, Part, ComboLit, ComboIn, CastLit, CastIn, BoxLit, BoxIn, Cylinder, Ring, Hammer, Signs, HitStop;
            public float RingRadius, CleanFrom, CleanTo;

            /// <summary>
            /// The clock stands within 0.1 ms of an edge of the clean part: there the judge allows its own 0.01 ms of rounding and a
            /// marker drawn as a share of its bar may round the other way (the fixed 1/60 s steps land exactly on 0.3 s).
            /// </summary>
            public bool OnEdge => Open && (Math.Abs(Since - CleanFrom) < OnEdgeSeconds || Math.Abs(Since - CleanTo) < OnEdgeSeconds);
        }

        const float OnEdgeSeconds = 1e-4f;

        static readonly string[] Signals = { "judge", "clean part", "combo bar lit", "combo blade in zone", "old cast bar lit", "old cast bar marker in zone",
                                             "skill box lit", "skill box marker in band", "cylinder in notch", "ring in band", "hammer brightest", "clean window sign" };

        static bool Signal(Seen s, int i)
        {
            switch (i)
            {
                case 0: return s.Judge;
                case 1: return s.Part;
                case 2: return s.ComboLit;
                case 3: return s.ComboIn;
                case 4: return s.CastLit;
                case 5: return s.CastIn;
                case 6: return s.BoxLit;
                case 7: return s.BoxIn;
                case 8: return s.Cylinder;
                case 9: return s.Ring;
                case 10: return s.Hammer;
                default: return s.Signs;
            }
        }

        /// <summary>This frame, after every Update: each drawing worked out as its LateUpdate would, and the judge.</summary>
        Seen Look(int slot, int index)
        {
            _cue.Tick();
            _cast.Tick();
            _signs.Tick();
            var s = new Seen
            {
                Index = index, Since = _player.RecastSinceOpened, Real = Time.unscaledDeltaTime, Open = _player.IsRecastWindowOpen,
                Judge = _player.RecastPressWouldBeClean, HitStop = HitStop.HoldsThisFrame,
            };
            CleanPart part = _player.RecastCleanPart;
            s.CleanFrom = part.From;
            s.CleanTo = part.To;
            s.Part = s.Open && part.Contains(s.Since);
            s.ComboLit = _cast.ComboShown && _cast.ComboLit;
            int open = _cast.ComboOpen;
            s.ComboIn = _cast.ComboShown && open >= 1 && _cast.ComboCleanTo(open) > _cast.ComboCleanFrom(open)
                        && _cast.ComboBlade >= _cast.ComboCleanFrom(open) && _cast.ComboBlade <= _cast.ComboCleanTo(open);
            s.CastLit = _cast.Up && _cast.Lit;
            s.CastIn = _cast.Up && _cast.View.MarkerInZone;
            if (_bar != null && _bar.RecastOnBox(slot, out float now, out float from, out float to, out bool cleanNow))
            {
                s.BoxLit = cleanNow;
                s.BoxIn = to > from && now >= from && now <= to;
            }
            s.Cylinder = _cue.CylinderSays && _cue.Lit;
            s.Ring = _cue.RingSays && _cue.Lit;
            s.Hammer = _cue.HammerSays && _cue.Lit;
            s.RingRadius = _cue.RingRadius;
            s.Signs = _signs.BandLit;
            return s;
        }

        /// <summary>Every frame of press <paramref name="press"/>'s wait, from the press before to its window's end; no press made.</summary>
        IEnumerator Follow(int slot, int press, List<Seen> seen)
        {
            int i = 0;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Waiting(press) && Time.realtimeSinceStartup < deadline)
            {
                seen.Add(Look(slot, i++));
                yield return null;
            }
        }

        static float Ms(float s) => s * 1000f;
        static string F(float x) => x.ToString("0.0", CultureInfo.InvariantCulture);

        // ---------------- 1. The drawn window against the judged window, frame by frame ----------------

        static IEnumerable<TestCaseData> Cases()
        {
            foreach (bool hit in new[] { true, false })
            {
                yield return new TestCaseData("Triple Tap", 2, hit).SetName("Edges_TripleTap_Press2_" + (hit ? "Hit" : "NoHit")).Returns(null);
                yield return new TestCaseData("Triple Tap", 3, hit).SetName("Edges_TripleTap_Press3_" + (hit ? "Hit" : "NoHit")).Returns(null);
                yield return new TestCaseData("Backstep", 2, hit).SetName("Edges_Backstep_Press2_" + (hit ? "Hit" : "NoHit")).Returns(null);
            }
        }

        [UnityTest, Timeout(120000)]
        [TestCaseSource(nameof(Cases))]
        public IEnumerator EveryDrawing_EntersAndLeavesTheCleanPart_OnTheJudgesFrame_AndAPressAfterItsFirstAndMiddleFramesIsClean(string skill, int press, bool hit)
        {
            int slot = Slot(skill);
            var seen = new List<Seen>();
            yield return Ready(hit);
            yield return PressesBefore(slot, press);
            yield return Follow(slot, press, seen);
            Time.captureDeltaTime = 0f;
            Assert.That(seen.Count(s => s.Judge), Is.GreaterThan(0), "the judge saw clean frames");

            var log = new StringBuilder();
            log.AppendLine("[142] " + skill + " press " + press + (hit ? ", the shot before it landing (hit stop)" : ", aimed at nothing (no hit stop)")
                           + ": frames of 1/60 s; " + seen.Count + " frames from the press before to the window's end; frames in a hit stop: "
                           + seen.Count(s => s.HitStop) + ", their real seconds " + string.Join(",", seen.Where(s => s.HitStop).Select(s => s.Real.ToString("0.0000", CultureInfo.InvariantCulture))));
            int judgeFirst = seen.FindIndex(s => s.Judge), judgeLast = seen.FindLastIndex(s => s.Judge);
            Seen lastOpen = seen.LastOrDefault(s => s.Open);
            log.AppendLine("[142]   judged clean part: since " + F(Ms(seen[judgeFirst].Since)) + " to " + F(Ms(seen[judgeLast].Since)) + " ms (frames "
                           + judgeFirst + " to " + judgeLast + "); window open through since " + F(Ms(lastOpen.Since)) + " ms");
            var faults = new List<string>();
            for (int k = 0; k < Signals.Length; k++)
            {
                int first = seen.FindIndex(s => Signal(s, k)), last = seen.FindLastIndex(s => Signal(s, k));
                if (first < 0)
                {
                    log.AppendLine("[142]   " + Signals[k] + ": never (not drawn here)");
                    continue;
                }
                int gaps = 0;
                for (int j = first; j <= last; j++) if (!Signal(seen[j], k)) gaps++;
                int mismatched = seen.Count(s => (s.Open || s.Since >= 0f) && !s.OnEdge && Signal(s, k) != s.Judge);
                int onEdge = seen.Count(s => s.OnEdge && Signal(s, k) != s.Judge);
                int beforeOpen = seen.Count(s => !s.Open && s.Since < 0f && Signal(s, k));
                log.AppendLine("[142]   " + Signals[k] + ": enters at frame " + first + " (since " + F(Ms(seen[first].Since)) + " ms), leaves after frame " + last
                               + " (since " + F(Ms(seen[last].Since)) + " ms); against the judge " + (first - judgeFirst) + " and " + (last - judgeLast)
                               + " frames; frames it disagrees with the judge: " + mismatched + (onEdge > 0 ? " (and " + onEdge + " within 0.1 ms of an edge, rounding)" : "")
                               + (gaps > 0 ? "; gaps " + gaps : "") + (beforeOpen > 0 ? "; says clean before the window opens on " + beforeOpen + " frames" : ""));
                // Every drawing a player reads as "now" must say so on exactly the judge's frames (the skill box's and the old bar's
                // marker included), and never before the window opens. The old cast bar is not drawn with the combo bar on, but it is
                // worked out: it must agree too. A frame within 0.1 ms of an edge is rounding, not a frame a person can tell.
                if (k >= 2 && (mismatched > 0 || beforeOpen > 0)) faults.Add(Signals[k] + " (" + (first - judgeFirst) + ", " + (last - judgeLast) + ", " + mismatched + ")");
            }
            Debug.Log(log.ToString());
            Assert.That(faults, Is.Empty, skill + " press " + press + ": drawings that do not enter or leave on the judge's frame: " + string.Join("; ", faults));
            float sinceFirst = seen[judgeFirst].Since, sinceLast = seen[judgeLast].Since;

            // Presses on the frame after a drawing says "now": its first frame, its middle, its last.
            float[] targets = { sinceFirst, (sinceFirst + sinceLast) * 0.5f, sinceLast };
            string[] names = { "first", "middle", "last" };
            var pressLog = new StringBuilder();
            for (int t = 0; t < targets.Length; t++)
            {
                yield return Ready(hit);
                yield return PressesBefore(slot, press);
                float target = targets[t];
                yield return Until(() => !Waiting(press) || (_player.IsRecastWindowOpen && _player.RecastSinceOpened >= target - 1e-4f), 3f);
                Assert.That(Waiting(press), Is.True);
                Seen saw = Look(slot, 0);
                int before = _recasts.Count;
                yield return Press(slot);
                Time.captureDeltaTime = 0f;
                Assert.That(_recasts.Count, Is.EqualTo(before + 1), "the press was made");
                PlayerReport r = _recasts.Last();
                pressLog.AppendLine("[142]   pressed on the frame after the drawings' " + names[t] + " clean frame (drawn at since " + F(Ms(saw.Since)) + " ms, ring "
                                    + F(saw.RingRadius) + " px, combo lit " + saw.ComboLit + "): judged at since " + F(Ms(r.Seconds)) + " ms, " + r.Timing + (r.Clean ? ", rewarded" : ""));
                if (t < 2) Assert.That(r.Timing, Is.EqualTo(RecastTiming.Clean), skill + " press " + press + ": a press on the frame after the drawings' " + names[t] + " clean frame is clean");
            }
            Debug.Log("[142] " + skill + " press " + press + (hit ? " (hit)" : " (no hit)") + ":\n" + pressLog);
        }

        // ---------------- 2. What each press is told ----------------

        struct Told
        {
            public RecastTiming Timing;
            public bool Rewarded;
            public float Since;
            public string Words, Tones, Flashes, Sounds;
            public ComboZoneState Combo;
            public bool RingClean;
        }

        const float WaitedAhead = 0.08f; // s before the window opens that a waited press is made: inside the input buffer's 0.15 s

        static readonly FieldInfo s_GoneClean = typeof(TimingAtTheSight).GetField("_goneClean", BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly PlaceholderSound[] Telling = { PlaceholderSound.CleanTick, PlaceholderSound.ChainTop, PlaceholderSound.OffTick, PlaceholderSound.HitCleanEdge, PlaceholderSound.Menu };

        /// <summary>One chain, press <paramref name="press"/> made <paramref name="kind"/> (early, clean, late or waited), the presses before it clean; what it was told until 0.4 s after.</summary>
        IEnumerator Tell(int slot, int press, RecastTiming kind, bool hit, List<Told> told)
        {
            yield return Ready(hit);
            yield return PressesBefore(slot, press);
            if (kind == RecastTiming.Waited)
            {
                // Inside the input buffer (0.15 s) before the window opens: earlier, the press is dropped (Backstep's hop is 0.2 s).
                yield return null; // the press before's own sounds out first
                yield return Until(() => !Waiting(press) || _player.IsRecastWindowOpen || _player.RecastSincePress >= _player.RecastLead - WaitedAhead, 3f);
                Assert.That(Waiting(press) && !_player.IsRecastWindowOpen, Is.True, "the shot before (or the hop) still in its startup");
            }
            else
            {
                yield return Until(() => !Waiting(press) || _player.IsRecastWindowOpen, 3f);
                CleanPart c = _player.RecastCleanPart;
                float window = _player.RecastWindowSeconds;
                float at = kind == RecastTiming.Early ? Mathf.Max(Frame, c.From - 2f * Frame)
                         : kind == RecastTiming.Late ? Mathf.Min(window - 2f * Frame, c.To + 3f * Frame) : (c.From + c.To) * 0.5f;
                yield return Until(() => !Waiting(press) || _player.RecastSinceOpened >= at - Frame, 3f);
            }
            int flashes = _flashes.Count, sounds = _heard.Count, words = _words.Count, tones = _tones.Count, recasts = _recasts.Count;
            // Backstep's early part is two frames (0 to 0.03 s after the hop's end): its early press is made on the frame the window
            // opens, straight through the intent (the driver reads it on that frame too, before anything of the frame is drawn).
            bool now = kind == RecastTiming.Early && _player.IsRecastWindowOpen && _player.RecastCleanPart.From < 3f * Frame;
            if (now) _player.PressSkill(slot, _player.AimPoint);
            else yield return Press(slot);
            yield return Until(() => _recasts.Count > recasts, 2f); // a press that waited is reported when its window opens
            Assert.That(_recasts.Count, Is.EqualTo(recasts + 1), "the press made");
            PlayerReport r = _recasts.Last();
            Assert.That(s_GoneClean, Is.Not.Null, "the ring cue's pressed ring is read");
            bool ringClean = (bool)s_GoneClean.GetValue(_cue);
            _cast.Tick();
            ComboZoneState combo = _cast.ComboState(press - 1);
            float end = Time.time + 0.4f;
            yield return Until(() => Time.time >= end, 3f);
            Time.captureDeltaTime = 0f;
            told.Add(new Told
            {
                Timing = r.Timing, Rewarded = r.Clean, Since = r.Seconds, Combo = combo, RingClean = ringClean,
                Flashes = string.Join(" ", _flashes.Skip(flashes)),
                Sounds = string.Join(" ", _heard.Skip(sounds).Where(s => Telling.Contains(s))),
                Words = string.Join(" ", _words.Skip(words)),
                Tones = string.Join(" ", _tones.Skip(tones)),
            });
        }

        static IEnumerable<TestCaseData> Presses()
        {
            yield return new TestCaseData("Triple Tap", 2).SetName("Told_TripleTap_Press2").Returns(null);
            yield return new TestCaseData("Triple Tap", 3).SetName("Told_TripleTap_Press3").Returns(null);
            yield return new TestCaseData("Backstep", 2).SetName("Told_Backstep_Press2").Returns(null);
        }

        [UnityTest, Timeout(120000)]
        [TestCaseSource(nameof(Presses))]
        public IEnumerator EachPress_IsToldWhatItWasJudged_ACleanOneTheCleanAnswerEverywhere_ANotCleanOneNowhere(string skill, int press)
        {
            int slot = Slot(skill);
            var told = new List<Told>();
            foreach (RecastTiming kind in new[] { RecastTiming.Early, RecastTiming.Clean, RecastTiming.Late, RecastTiming.Waited })
                yield return Tell(slot, press, kind, true, told);
            var log = new StringBuilder("[142] what " + skill + " press " + press + " is told (the presses before it clean; the shot landing on Dummy_A):\n");
            log.AppendLine("[142]   timing | rewarded | since ms | combo zone | ring cue | cast bar flash | sounds | words over her or the target | tones");
            foreach (Told t in told)
                log.AppendLine("[142]   " + t.Timing + " | " + t.Rewarded + " | " + F(Ms(t.Since)) + " | " + t.Combo + " | " + (t.RingClean ? "teal (clean)" : "grey") + " | "
                               + (t.Flashes.Length > 0 ? t.Flashes : "-") + " | " + (t.Sounds.Length > 0 ? t.Sounds : "-") + " | " + (t.Words.Length > 0 ? t.Words : "-") + " | " + (t.Tones.Length > 0 ? t.Tones : "-"));
            Debug.Log(log.ToString());
            Assert.That(told.Select(t => t.Timing), Is.EqualTo(new[] { RecastTiming.Early, RecastTiming.Clean, RecastTiming.Late, RecastTiming.Waited }), "the four presses fell as aimed");
            foreach (Told t in told)
            {
                bool clean = t.Timing == RecastTiming.Clean;
                string what = skill + " press " + press + ", " + t.Timing + ": ";
                Assert.That(t.Combo == ComboZoneState.Hit, Is.EqualTo(clean), what + "the combo bar's zone");
                Assert.That(t.RingClean, Is.EqualTo(clean), what + "the ring cue's pressed ring");
                Assert.That(t.Flashes.Contains("Clean"), Is.EqualTo(clean), what + "the cast bar's flash (" + t.Flashes + ")");
                Assert.That(t.Sounds.Contains("CleanTick") || t.Sounds.Contains("ChainTop"), Is.EqualTo(clean), what + "the clean press's tick (" + t.Sounds + ")");
                // The chain's rewarded press keeps its stronger answer, and only it.
                Assert.That(t.Words.Contains("CLEAN"), Is.EqualTo(t.Rewarded), what + "the CLEAN word only for the rewarded press");
            }
        }

        // ---------------- 3. The switch ----------------

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSwitchOff_TheCastBarFlashesAsBefore_PlainForAQuickPressInItsCleanPart()
        {
            int slot = Slot("Triple Tap");
            var told = new List<Told>();
            CleanPressAnswers.On = false;
            yield return Tell(slot, 2, RecastTiming.Clean, true, told);
            Assert.That(told[0].Timing, Is.EqualTo(RecastTiming.Clean));
            Assert.That(told[0].Flashes, Is.EqualTo("Plain"), "off: as before work order 142, the flash follows the rewarded press alone");
            CleanPressAnswers.On = true;
            told.Clear();
            yield return Tell(slot, 2, RecastTiming.Clean, true, told);
            Assert.That(told[0].Flashes, Is.EqualTo("Clean"), "on: the judged timing");
        }
    }
}
