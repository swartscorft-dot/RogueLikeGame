using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 019 (2026-11-20), the timing feedback, through the intents in the ControlRoom scene: each moment that
    /// takes skill puts up its word once, at the right place, and no word when it did not happen (a clean tap against an
    /// early one, a perfect release against one outside the band, a counter from the front against one from behind, marks
    /// spent against none, a keg shot against one left to its fuse, a stagger bar emptied); EARLY and LATE in each case
    /// built; a burst on one target is one number with the sum; the setting off shows no numbers but still the words; each
    /// tone is asked for once on its moment and never on a miss (through <see cref="ShotSounds.Requested"/>, as the sound
    /// tests do); and a fight deals the same damage, cooldowns and timing with the feedback on and off.
    ///
    /// Waits are in game time or real time with a limit, never frames of a supposed length. The damage numbers key in
    /// PlayerPrefs is cleared before and after each test, and what this machine had under it is put back.
    /// </summary>
    public class TimingFeedbackTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        TimingFeedback _feedback;
        RunHud _hud;
        int? _numbersBefore;
        readonly List<FeedbackShown> _words = new List<FeedbackShown>();
        readonly List<FeedbackTone> _tones = new List<FeedbackTone>();
        readonly List<int> _damage = new List<int>();
        readonly List<Object> _copies = new List<Object>(); // settings copies a test made, so the asset itself is never touched

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _numbersBefore = PlayerPrefs.HasKey(GameSettings.DamageNumbersKey) ? PlayerPrefs.GetInt(GameSettings.DamageNumbersKey) : (int?)null;
            PlayerPrefs.DeleteKey(GameSettings.DamageNumbersKey);
            PlayerPrefs.Save();
            ShotSounds.Requested += Record;
            yield return Load();
            _feedback = TimingFeedback.Ensure();
            _feedback.WordShown += OnWord;
            yield return null;
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.MeleeYouRead = false; // work order 035: these tests are of the swarmer's, brute's and guard's attacks from before
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _damage.Clear();
            _a.Damaged += d => _damage.Add(d);
        }

        [TearDown]
        public void CleanUp()
        {
            ShotSounds.Requested -= Record;
            if (_feedback != null)
            {
                _feedback.WordShown -= OnWord;
                Object.Destroy(_feedback.gameObject);
            }
            _feedback = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            GameSettings.Unload();
            PlayerPrefs.DeleteKey(GameSettings.DamageNumbersKey);
            if (_numbersBefore.HasValue) PlayerPrefs.SetInt(GameSettings.DamageNumbersKey, _numbersBefore.Value);
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
            _words.Clear();
            _tones.Clear();
            _toneVolume = -1f;
            foreach (Object copy in _copies) if (copy != null) Object.Destroy(copy);
            _copies.Clear();
        }

        void OnWord(FeedbackShown w) => _words.Add(w);

        void Record(ShotSoundRequest r)
        {
            if (_feedback == null || r.Clip == null) return;
            if (r.Clip == _feedback.CleanTone) _tones.Add(FeedbackTone.Clean);
            else if (r.Clip == _feedback.PerfectTone) _tones.Add(FeedbackTone.Perfect);
            else if (r.Clip == _feedback.CounterTone) _tones.Add(FeedbackTone.Counter);
            else return;
            _toneVolume = r.Volume;
        }

        float _toneVolume = -1f;

        // ---------------- Helpers ----------------

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

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        List<FeedbackShown> Said(string text) => _words.Where(w => w.Text == text).ToList();

        /// <summary>The only word with this text: once, over this target (null: over her).</summary>
        void SaidOnce(string text, Damageable over)
        {
            List<FeedbackShown> said = Said(text);
            Assert.That(said.Count, Is.EqualTo(1), text + " once; words: " + string.Join(", ", _words.Select(w => w.Text)));
            if (over != null)
            {
                Assert.That(said[0].Target, Is.SameAs(over), text + " over its target");
                Assert.That(Flat(said[0].World, over.transform.position), Is.LessThan(0.05f), "anchored on it");
                Assert.That(said[0].World.y, Is.GreaterThan(over.transform.position.y + 1f), "over it, not in it");
            }
            else
            {
                Assert.That(said[0].OverHer, Is.True, text + " over her");
                Assert.That(Flat(said[0].World, _player.Position), Is.LessThan(0.5f));
            }
        }

        void NotSaid(params string[] texts)
        {
            foreach (string t in texts) Assert.That(Said(t), Is.Empty, "no " + t);
        }

        List<FeedbackShown> NumbersOver(Damageable target) => _feedback.Numbers.Where(n => n.Target == target).ToList();

        // ---------------- Triple Tap and Backstep: CLEAN and EARLY ----------------

        IEnumerator Chain(string skillName, bool clean)
        {
            int slot = Slot(skillName);
            SkillDefinition skill = _player.Skills[slot];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= skill.RecastCount; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                bool last = press == skill.RecastCount;
                if (last && clean) yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
                if (last && !clean) Assume.That(_player.RecastSinceOpened, Is.LessThan(_player.RecastCleanFrom), "early: not in the clean part"); // where it lies since work order 040
                Assume.That(_player.IsRecastWindowOpen, Is.True);
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), "press " + press);
            }
            int hits = skill.OpeningMove ? skill.RecastCount - 1 : skill.RecastCount;
            yield return Until(() => _a.HitsTaken >= hits, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            yield return null;
            Assert.That(_a.HitsTaken, Is.EqualTo(hits), "every shot hit");
        }

        [UnityTest]
        public IEnumerator TripleTap_ACleanLastPress_SaysCLEANOverTheTarget_WithItsToneOnce()
        {
            yield return Chain("Triple Tap", clean: true);
            SaidOnce("CLEAN", _a);
            Assert.That(Said("CLEAN")[0].Color, Is.EqualTo(new Color(0.4f, 1f, 0.5f, 1f)), "the skill bar's clean green");
            NotSaid("EARLY", "LATE");
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Clean }), "the clean tone, once");
        }

        [UnityTest]
        public IEnumerator TripleTap_AnEarlyLastPress_SaysEARLYOverHer_NoCleanAndNoTone()
        {
            yield return Chain("Triple Tap", clean: false);
            SaidOnce("EARLY", null);
            FeedbackShown early = Said("EARLY")[0];
            Assert.That(early.FontSize, Is.LessThan(_feedback.Values.WordSize), "small");
            Assert.That(early.Color.r, Is.EqualTo(early.Color.g).And.EqualTo(early.Color.b), "grey");
            NotSaid("CLEAN");
            Assert.That(_tones, Is.Empty, "a missed timing has no sound");
        }

        [UnityTest]
        public IEnumerator Backstep_ACleanSecondPress_SaysCLEANOverTheTarget_WithItsToneOnce()
        {
            yield return Chain("Backstep", clean: true);
            SaidOnce("CLEAN", _a);
            NotSaid("EARLY");
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Clean }));
        }

        [UnityTest]
        public IEnumerator Backstep_AnEarlyShot_SaysEARLYOverHer_NoCleanAndNoTone()
        {
            // Work order 142 opened Backstep's clean part with the hop's end (KitTimingChoices.BackstepFromHopsEnd), so its window
            // has no early part any more: this test of a press before it is given the 0.03 s before (on a copy of the settings).
            SkillSettings copy = Object.Instantiate(_player.SkillSet);
            _copies.Add(copy);
            copy.Kit.OpeningMoveCleanFrom = 0.03f;
            _player.SkillSet = copy;
            yield return Chain("Backstep", clean: false);
            SaidOnce("EARLY", null);
            NotSaid("CLEAN");
            Assert.That(_tones, Is.Empty);
        }

        // ---------------- Long Shot: PERFECT, EARLY, LATE ----------------

        IEnumerator LongShotReleasedAt(float seconds)
        {
            int slot = Slot("Long Shot");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillChargedSeconds >= seconds, 5f);
            _player.ReleaseSkill(slot);
            yield return Until(() => _damage.Count > 0, 3f);
            yield return null;
            Assert.That(_damage.Count, Is.EqualTo(1), "one hit");
        }

        [UnityTest]
        public IEnumerator LongShot_LetGoInTheBand_SaysPERFECTOverTheTarget_WithItsTone_AndABigGreenNumber()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            yield return LongShotReleasedAt(0.5f * (skill.PerfectFrom + skill.PerfectTo));
            Assume.That(_player.PerfectReleases, Is.EqualTo(1), "the game judged it perfect");
            SaidOnce("PERFECT", _a);
            Assert.That(Said("PERFECT")[0].FontSize, Is.EqualTo(_feedback.Values.BigWordSize), "larger");
            NotSaid("EARLY", "LATE");
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Perfect }));
            List<FeedbackShown> numbers = NumbersOver(_a);
            Assert.That(numbers.Count, Is.EqualTo(1));
            Assert.That(numbers[0].Sum, Is.EqualTo(_damage[0]));
            Assert.That(numbers[0].Kind, Is.EqualTo(NumberKind.Perfect));
            Assert.That(numbers[0].FontSize, Is.EqualTo(_feedback.Values.BigNumberSize));
            Assert.That(numbers[0].Color, Is.EqualTo(Said("PERFECT")[0].Color), "in its word's colour");
        }

        [UnityTest]
        public IEnumerator LongShot_LetGoAfterTheBand_SaysLATEOverHer_NoPerfectAndNoTone()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            yield return LongShotReleasedAt(skill.PerfectTo + 0.25f);
            SaidOnce("LATE", null);
            NotSaid("PERFECT", "EARLY");
            Assert.That(_tones, Is.Empty);
            Assert.That(NumbersOver(_a).Single().Kind, Is.EqualTo(NumberKind.Plain), "an ordinary number");
        }

        [UnityTest]
        public IEnumerator LongShot_LetGoBeforeTheBand_PastHalfACharge_SaysEARLYOverHer()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            float at = Mathf.Lerp(_feedback.Values.MissFromCharge * skill.ChargeTime, skill.PerfectFrom, 0.5f);
            yield return LongShotReleasedAt(at);
            SaidOnce("EARLY", null);
            NotSaid("PERFECT", "LATE");
            Assert.That(_tones, Is.Empty);
        }

        [UnityTest]
        public IEnumerator LongShot_Tapped_SaysNothing_ItWasAChoice()
        {
            yield return LongShotReleasedAt(0.1f);
            Assert.That(_words, Is.Empty, "no word for a tap");
            Assert.That(_tones, Is.Empty);
            Assert.That(NumbersOver(_a).Single().Sum, Is.EqualTo(_damage[0]), "its number still shows");
        }

        // ---------------- Stopping Shot: COUNTER ----------------

        IEnumerator BruteWindingUp(float distance, Action<EnemyBrute> got)
        {
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice brute");
            Vector3 home = brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * distance);
            yield return Until(() => brute.State == EnemyState.Windup, 6f);
            Assume.That(brute.State, Is.EqualTo(EnemyState.Windup), "the brute winds up");
            Assume.That(brute.Attack?.Name, Is.EqualTo("Shockwave"));
            got(brute);
        }

        [UnityTest]
        public IEnumerator StoppingShot_FromTheFront_SaysCOUNTEROverTheBrute_WithItsToneOnce()
        {
            EnemyBrute brute = null;
            yield return BruteWindingUp(7f, b => brute = b);
            Assert.That(_player.PressSkill(Slot("Stopping Shot"), brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Countered > 0 || brute.State != EnemyState.Windup, 3f);
            yield return null;
            Assume.That(brute.Countered, Is.EqualTo(1), "the game counted a counter");
            SaidOnce("COUNTER", brute.Body);
            Assert.That(Said("COUNTER")[0].Color, Is.EqualTo(new Color(0.15f, 0.45f, 1f, 1f)), "the blue of the counterable telegraph");
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Counter }));
        }

        [UnityTest]
        public IEnumerator StoppingShot_FromBehind_SaysNoCounter_AndNoTone()
        {
            EnemyBrute brute = null;
            yield return BruteWindingUp(7f, b => brute = b);
            _player.Teleport(brute.transform.position - brute.transform.forward * 4f);
            yield return null;
            Assume.That(brute.State, Is.EqualTo(EnemyState.Windup));
            int hits = brute.Body.HitsTaken;
            Assert.That(_player.PressSkill(Slot("Stopping Shot"), brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Body.HitsTaken > hits, 2f);
            yield return null;
            Assume.That(brute.Body.HitsTaken, Is.GreaterThan(hits), "the shot hit it");
            Assert.That(brute.Countered, Is.EqualTo(0));
            NotSaid("COUNTER");
            Assert.That(_tones, Is.Empty);
        }

        // ---------------- Scatter Blast: marks spent ----------------

        IEnumerator ScatterOnMarks(int marks)
        {
            _player.Teleport(NearA);
            yield return null;
            if (marks > 0) _a.AddMarks(marks, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(marks));
            Assert.That(_player.PressSkill(Slot("Scatter Blast"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            yield return null;
            Assert.That(_damage.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MarksSpent_Three_SaysX3OverTheTarget_InTheOrangeOfThePips_AndAnOrangeNumber()
        {
            yield return ScatterOnMarks(3);
            SaidOnce("x3", _a);
            Assert.That(Said("x3")[0].Color, Is.EqualTo(new Color(1f, 0.55f, 0.1f, 1f)));
            Assert.That(Said("x3")[0].FontSize, Is.EqualTo(_feedback.Values.WordSize));
            FeedbackShown number = NumbersOver(_a).Single();
            Assert.That(number.Kind, Is.EqualTo(NumberKind.Marks));
            Assert.That(number.Sum, Is.EqualTo(_damage[0]));
            Assert.That(_tones, Is.Empty, "marks have no tone");
        }

        [UnityTest]
        public IEnumerator MarksSpent_AtTheCap_SaysX5_RedAndLarger()
        {
            yield return ScatterOnMarks(_player.Kit.MaxMarks);
            string text = "x" + _player.Kit.MaxMarks;
            SaidOnce(text, _a);
            Assert.That(Said(text)[0].Color, Is.EqualTo(new Color(1f, 0.15f, 0.08f, 1f)), "the red of the pips at the cap");
            Assert.That(Said(text)[0].FontSize, Is.EqualTo(_feedback.Values.BigWordSize));
            Assert.That(NumbersOver(_a).Single().Kind, Is.EqualTo(NumberKind.MarksFull));
        }

        [UnityTest]
        public IEnumerator MarksSpent_NoneToSpend_SaysNothing()
        {
            yield return ScatterOnMarks(0);
            Assert.That(_words, Is.Empty);
            Assert.That(NumbersOver(_a).Single().Kind, Is.EqualTo(NumberKind.Plain));
        }

        // ---------------- Powder Keg: BOOM, and a burst is one number ----------------

        [UnityTest]
        public IEnumerator AKegSetOffByAShot_SaysBOOMOverTheKeg_AndTheBurstAndTheShotAreOneNumber()
        {
            int keg = Slot("Powder Keg");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsLying > 0, 2f);
            yield return UntilFree();
            int fired = _player.BasicShotsFired;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            yield return null;
            Assume.That(_player.KegsSetOffByShots, Is.EqualTo(1), "the game set it off by the shot");
            Assume.That(_damage.Count, Is.EqualTo(2), "the burst and the shot both hit A");

            List<FeedbackShown> boom = Said("BOOM");
            Assert.That(boom.Count, Is.EqualTo(1), "BOOM once");
            Assert.That(Flat(boom[0].World, _player.LastKegPoint), Is.LessThan(0.05f), "over the keg");
            Assert.That(boom[0].Target, Is.Null);
            Assert.That(boom[0].OverHer, Is.False);

            List<FeedbackShown> numbers = NumbersOver(_a);
            Assert.That(numbers.Count, Is.EqualTo(1), "one number for the burst and the shot");
            Assert.That(numbers[0].Sum, Is.EqualTo(_damage.Sum()), "their sum");
            Assert.That(numbers[0].Hits, Is.EqualTo(2));
            Assert.That(_tones, Is.Empty);
        }

        [UnityTest]
        public IEnumerator APerfectLongShot_ThroughALyingKeg_IsStillPerfect_OverItsTarget_AndItsNumberIsPerfect()
        {
            // Work order 026 (the second review of 2026-11-20): the keg's burst, reported in the middle of the Long Shot's
            // hit, must not take the Long Shot's hit with it.
            int keg = Slot("Powder Keg"), longShot = Slot("Long Shot");
            SkillDefinition kegSkill = _player.Skills[keg], skill = _player.Skills[longShot];
            _player.Teleport(NearA);
            yield return null;
            float fuse = kegSkill.Fuse;
            kegSkill.Fuse = 6f; // long enough to charge the Long Shot into its band (the skills live in an asset: put back below)
            try
            {
                Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
                yield return Until(() => _player.KegsLying > 0, 2f);
            }
            finally
            {
                kegSkill.Fuse = fuse;
            }
            Assume.That(_player.KegsLying, Is.EqualTo(1), "a keg lies at A");
            yield return UntilFree();
            Assert.That(_player.PressSkill(longShot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillChargedSeconds >= 0.5f * (skill.PerfectFrom + skill.PerfectTo), 5f);
            _player.ReleaseSkill(longShot);
            yield return Until(() => _damage.Count >= 2, 3f);
            yield return null;
            Assume.That(_player.PerfectReleases, Is.EqualTo(1), "the game judged it perfect");
            Assume.That(_player.KegsSetOffByShots, Is.EqualTo(1), "the Long Shot set the keg off");
            Assume.That(_damage.Count, Is.EqualTo(2), "the shot and the burst both hit A");

            SaidOnce("PERFECT", _a);
            Assert.That(Said("BOOM").Count, Is.EqualTo(1), "and the keg's BOOM");
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Perfect }));
            List<FeedbackShown> numbers = NumbersOver(_a);
            Assert.That(numbers.Count, Is.EqualTo(1), "one number for the shot and the burst");
            Assert.That(numbers[0].Sum, Is.EqualTo(_damage.Sum()));
            Assert.That(numbers[0].Kind, Is.EqualTo(NumberKind.Perfect), "the perfect shot's hit counted as perfect");
        }

        [UnityTest]
        public IEnumerator AKegLeftToItsFuse_SaysNoBoom()
        {
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Slot("Powder Keg"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsBurst > 0, 4f);
            yield return null;
            Assume.That(_damage.Count, Is.EqualTo(1), "it burst on A by itself");
            NotSaid("BOOM");
            Assert.That(NumbersOver(_a).Single().Sum, Is.EqualTo(_damage[0]));
        }

        [UnityTest]
        public IEnumerator AHeldSkill_FanFire_IsOneNumber_TheSumOfItsTicks()
        {
            int fan = Slot("Fan Fire");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(fan, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return For(0.6f);
            _player.ReleaseSkill(fan);
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            yield return null;
            Assume.That(_damage.Count, Is.GreaterThan(2), "several ticks hit A");
            List<FeedbackShown> numbers = NumbersOver(_a);
            Assert.That(numbers.Count, Is.EqualTo(1), "one number, not one a tick: " + string.Join(", ", numbers.Select(n => n.Text)));
            Assert.That(numbers[0].Sum, Is.EqualTo(_damage.Sum()));
            Assert.That(numbers[0].Hits, Is.EqualTo(_damage.Count));
        }

        // ---------------- A stagger bar emptied: DAZED ----------------

        [UnityTest]
        public IEnumerator AStaggerBarEmptied_SaysDAZEDOverTheTarget_AndHitsOnItAfterShowLargerInItsBlue()
        {
            _a.Stagger = new StaggerBar(new StaggerValues { Max = 10f, DazeSeconds = 5f });
            _player.Teleport(NearA);
            yield return null;

            // Quick Shot takes some of the bar: no daze, no word.
            Assume.That(_player.Skills[Slot("Quick Shot")].Stagger, Is.LessThan(10f));
            Assert.That(_player.PressSkill(Slot("Quick Shot"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count >= 1, 2f);
            yield return null;
            Assume.That(_a.IsDazed, Is.False);
            NotSaid("DAZED");
            yield return UntilFree();

            // Scatter Blast empties it (and spends Quick Shot's mark in the same moment: DAZED outranks x1).
            Assume.That(_player.Skills[Slot("Scatter Blast")].Stagger, Is.GreaterThanOrEqualTo(10f));
            Assert.That(_player.PressSkill(Slot("Scatter Blast"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count >= 2, 2f);
            yield return null;
            Assume.That(_a.IsDazed, Is.True, "the bar is empty");
            SaidOnce("DAZED", _a);
            Assert.That(Said("DAZED")[0].Color, Is.EqualTo(new Color(0.55f, 0.75f, 1f, 1f)), "the light blue of the daze tint");
            NotSaid("x1");
            yield return UntilFree();

            // A basic shot on it while dazed: a larger number in its blue.
            yield return For(_feedback.Values.NumberSeconds + 0.05f); // the numbers before have gone
            int fired = _player.BasicShotsFired;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            yield return null;
            Assume.That(_damage.Count, Is.EqualTo(3));
            FeedbackShown number = NumbersOver(_a).Single();
            Assert.That(number.Kind, Is.EqualTo(NumberKind.Dazed));
            Assert.That(number.Sum, Is.EqualTo(_damage[2]));
            Assert.That(number.FontSize, Is.EqualTo(_feedback.Values.BigNumberSize));
            Assert.That(Said("DAZED").Count, Is.EqualTo(1), "still once");
        }

        // ---------------- Damage numbers ----------------

        [UnityTest]
        public IEnumerator AHit_ShowsItsDamageOverTheTarget_AndWhatSheTakesIsRedOverHer()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _damage.Count > 0, 2f);
            yield return null;
            FeedbackShown n = NumbersOver(_a).Single();
            Assert.That(n.Sum, Is.EqualTo(Placeholders.BasicShotDamage));
            Assert.That(n.Text, Is.EqualTo(Placeholders.BasicShotDamage.ToString()));
            Assert.That(n.Kind, Is.EqualTo(NumberKind.Plain));
            Assert.That(n.Color, Is.EqualTo(Color.white));
            Assert.That(n.FontSize, Is.EqualTo(_feedback.Values.NumberSize));
            Assert.That(n.World.y, Is.GreaterThan(_a.transform.position.y + 1f), "over it");
            Assert.That(_words, Is.Empty, "a plain hit has no word");

            Assert.That(_health.TakeHit(7), Is.EqualTo(7));
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            yield return null;
            List<FeedbackShown> hers = _feedback.Numbers.Where(x => x.OverHer).ToList();
            Assert.That(hers.Count, Is.EqualTo(1), "two hits at once: one number");
            Assert.That(hers[0].Sum, Is.EqualTo(12));
            Assert.That(hers[0].Kind, Is.EqualTo(NumberKind.Hurt));
            Assert.That(hers[0].Color.r, Is.GreaterThan(0.9f));
            Assert.That(hers[0].Color.g, Is.LessThan(0.4f), "red");

            yield return For(_feedback.Values.NumberSeconds + 0.05f);
            Assert.That(_feedback.Numbers, Is.Empty, "gone after their time");
        }

        [UnityTest]
        public IEnumerator TheSettingOff_ShowsNoNumbers_ButStillTheWords()
        {
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(TimingFeedback.NumbersOn, Is.True, "on by default");
            Assert.That(_hud.MenuText(RunHud.MenuLine.DamageNumbers), Does.EndWith(":  on"));
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.DamageNumbers, 1), Is.True);
            Assert.That(TimingFeedback.NumbersOn, Is.False);
            Assert.That(PlayerPrefs.GetInt(GameSettings.DamageNumbersKey, -1), Is.EqualTo(FeedbackRules.NumbersOff), "kept in PlayerPrefs");
            Assert.That(_hud.MenuText(RunHud.MenuLine.DamageNumbers), Does.EndWith(":  off"));

            // The chain ends a frame or two after its last hit: a number for it would still show.
            yield return Chain("Triple Tap", clean: true);
            Assert.That(_feedback.Numbers, Is.Empty, "no numbers for her hits");
            _health.TakeHit(5);
            yield return null;
            Assert.That(_feedback.Numbers, Is.Empty, "nor for what she takes");
            SaidOnce("CLEAN", _a);
            Assert.That(_tones, Is.EqualTo(new[] { FeedbackTone.Clean }), "the tone too");

            // Made again: read back from PlayerPrefs; and on again.
            Object.Destroy(_hud.gameObject);
            yield return null;
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(TimingFeedback.NumbersOn, Is.False, "kept");
            _hud.ChangeSetting(RunHud.MenuLine.DamageNumbers, -1);
            Assert.That(TimingFeedback.NumbersOn, Is.True, "either arrow flips it");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.DamageNumbers), Is.False, "not paused: no menu");
        }

        [UnityTest]
        public IEnumerator TheTones_AreOff_WhenTheirValueIsOff_ButTheWordsStay()
        {
            _feedback.Values.Tones = false;
            yield return Chain("Triple Tap", clean: true);
            SaidOnce("CLEAN", _a);
            Assert.That(_tones, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TheTones_AreShort_AndPlayAtAThirdOfTheBasicShotsVolume()
        {
            yield return Chain("Triple Tap", clean: true);
            Assert.That(_tones.Count, Is.EqualTo(1));
            float basic = _player.ShotLooks != null && _player.ShotLooks.Basic != null
                ? _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume : 1f;
            Assert.That(_feedback.Values.ToneVolume, Is.EqualTo(0.33f).Within(0.01f), "about a third");
            Assert.That(_toneVolume, Is.EqualTo(Mathf.Clamp01(_feedback.Values.ToneVolume * basic)).Within(1e-4f));
            Assert.That(_feedback.CleanTone.length, Is.LessThan(0.2f));
            Assert.That(_feedback.PerfectTone.length, Is.LessThan(0.2f));
            Assert.That(_feedback.CounterTone.length, Is.LessThan(0.2f));
        }

        // ---------------- Nothing changes in the fight ----------------

        struct Fight
        {
            public List<int> Damage;
            public List<float> Cooldowns;
            public List<float> Leads;
        }

        IEnumerator FightOnce(Action<Fight> got)
        {
            var fight = new Fight { Damage = new List<int>(), Cooldowns = new List<float>(), Leads = new List<float>() };
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(3, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            foreach (string name in new[] { "Quick Shot", "Scatter Blast", "Long Shot" })
            {
                int slot = Slot(name);
                int before = _damage.Count;
                float pressed = Time.time;
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), name);
                fight.Cooldowns.Add(_player.SkillCooldownRemaining(slot));
                if (_player.Skills[slot].Input == SkillInput.Charge)
                {
                    SkillDefinition s = _player.Skills[slot];
                    yield return Until(() => _player.SkillChargedSeconds >= 0.5f * (s.PerfectFrom + s.PerfectTo), 5f);
                    pressed = Time.time;
                    _player.ReleaseSkill(slot);
                }
                yield return Until(() => _damage.Count > before, 3f);
                fight.Leads.Add(Time.time - pressed);
                yield return UntilFree();
            }
            fight.Damage.AddRange(_damage);
            got(fight);
        }

        [UnityTest]
        public IEnumerator AFight_DealsTheSameDamage_WithTheSameCooldownsAndTiming_WithTheFeedbackOnAndOff()
        {
            Fight on = default, off = default;
            yield return FightOnce(f => on = f);
            Assert.That(_words.Count + _feedback.Numbers.Count, Is.GreaterThan(0), "the feedback was at work");

            _feedback.WordShown -= OnWord;
            Object.Destroy(_feedback.gameObject);
            _feedback = null;
            yield return null;
            yield return Load();
            Assert.That(TimingFeedback.Current, Is.Null, "off");
            yield return FightOnce(f => off = f);

            Debug.Log($"[019] on: {string.Join(", ", on.Damage)}  off: {string.Join(", ", off.Damage)}");
            Assert.That(off.Damage, Is.EqualTo(on.Damage), "the same damage, hit for hit");
            Assert.That(off.Cooldowns, Is.EqualTo(on.Cooldowns).Within(1e-4f), "the same cooldowns");
            for (int i = 0; i < on.Leads.Count; i++)
                Assert.That(off.Leads[i], Is.EqualTo(on.Leads[i]).Within(0.05f), "the same time from press to hit, skill " + i);
        }
    }
}
