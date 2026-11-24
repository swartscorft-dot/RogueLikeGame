using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Work order 025 (2026-11-20), in the ControlRoom scene through the intents: the focus choice comes up after floor
    /// 1's skill pick and before floor 2, paused, and never again, and the pause menu's list, the end screen, the record and
    /// the recorder name it; a new run has today's gauge until the choice, and the practice room's gauge is untouched after
    /// a run. In the practice room, through its keys' intent (<see cref="GeneratedLevels.SetPracticeFocus"/>): Hair
    /// Trigger's part cuts cooldowns by its share and primes the next skill (Triple Tap's last press clean when pressed
    /// early, Long Shot perfect at half a charge), and the priming lapses; Steady Hand's window is 10 s, Triple Tap takes
    /// five presses in it and three outside it, and Scatter Blast lands twice in it. And each of the chests' new rows
    /// changes what it names. Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3); the set-up equips every skill.
    ///
    /// A paused game has a time scale of 0: the waits around the choice count frames, never game time. Frames without a
    /// window are about 0.3 ms long, so no assertion here assumes a frame of about 1/60 s.
    /// </summary>
    public class FocusChoiceTests
    {
        const ulong RunSeed = 0x12345678UL;
        const int MostFrames = 600;
        const int Long = 3, Scatter = 4, Triple = 7, Quick = 0;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A is 2.8 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        Damageable _a;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<float> _reportTimes = new List<float>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _levels.RunValues.StatsAndTiers = false; // work order 050: the Mend test sets today's chest table, which the tiered offer does not read
            _reports.Clear();
            _reportTimes.Clear();
            _player.Reported += r => { _reports.Add(r); _reportTimes.Add(Time.time); };
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        IEnumerator FinishFloorAndWaitForTheReward()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
        }

        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;

        IEnumerable<PlayerReport> Reports(PlayerReportKind kind) => _reports.Where(r => r.Kind == kind);

        /// <summary>Triple Tap at Dummy_A from NearA: every press as soon as its window opens. Returns the presses made.</summary>
        IEnumerator TapEarly(List<int> presses)
        {
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the first press");
            presses.Add(1);
            while (true)
            {
                yield return Until(() => _player.IsRecastWindowOpen || _player.RecastSlot != Triple, 2f);
                if (_player.RecastSlot != Triple) yield break;
                Assume.That(_player.RecastSinceOpened, Is.LessThan(CleanPart.Of(_player.Kit, _player.RecastWindowSeconds).From), "early in the window: not in its clean part"); // where it lies since work order 040
                Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
                presses.Add(presses.Count + 1);
            }
        }

        // ---------------- The choice in a run ----------------

        [UnityTest]
        public IEnumerator TheChoice_ComesUpAfterFloor1sSkillPick_BeforeFloor2_Paused_AndNotAgain_AndIsNamed()
        {
            _hud = RunHud.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today), "a new run: today's gauge until the choice");
            Assert.That(_player.FocusParts, Is.EqualTo(1));
            yield return FinishFloorAndWaitForTheReward();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the skill first");
            Assert.That(_levels.PickFocusChoice(0), Is.False, "not before the skill is picked");
            Assert.That(_levels.PickReward(0), Is.True);

            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "then the focus choice");
            Assert.That(_levels.Floor, Is.EqualTo(1), "before floor 2");
            Assert.That(_levels.OfferedFocusModes, Is.EqualTo(new[] { FocusMode.HairTrigger, FocusMode.SteadyHand }));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "paused");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.True);
            float t = Time.time;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Time.time, Is.EqualTo(t), "no game time passes while it is up");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "it cannot be skipped: it waits");
            Assert.That(_hud.Escape(), Is.False, "Esc does nothing while it is up");
            Assert.That(_levels.PickReward(0), Is.False);
            Assert.That(_levels.PickFocusChoice(2), Is.False, "two options");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today), "still today's until it is taken");

            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(_levels.Floor, Is.EqualTo(2), "on to floor 2");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.HairTrigger));
            Assert.That(_player.FocusParts, Is.EqualTo(3));
            Assert.That(_levels.Record.FocusChoice, Is.EqualTo("Hair Trigger"));
            StringAssert.Contains("focus: Hair Trigger", _levels.StatusText());
            yield return null;
            StringAssert.Contains("Focus  Z  (Hair Trigger)", string.Join("\n", _hud.KeyLines()), "the pause menu's list names it");

            yield return FinishFloorAndWaitForTheReward();
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "not again after floor 2");
            Assert.That(_levels.Floor, Is.EqualTo(3));
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.HairTrigger), "it lasts for the run");

            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending != GeneratedLevels.EndKind.None);
            yield return null;
            StringAssert.Contains("Focus  Hair Trigger", _hud.EndScreenText, "the end screen names it");
            Assert.That(_player.RunFocus, Is.Null, "the run is over: the choice is off her");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today));

            // The same run seed again: today's gauge, and the choice at the same point.
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun), Is.True);
            yield return null;
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today));
            Assert.That(_levels.Record.FocusChoice, Is.Empty);
            yield return FinishFloorAndWaitForTheReward();
            Assert.That(_levels.PickReward(1), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "the same point");
            Assert.That(_levels.PickFocusChoice(1), Is.True);
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.SteadyHand));
            _levels.BackToPracticeRoom();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePracticeRoomsGauge_IsUntouchedAfterARun_AndItsKeysChangeOnlyIt()
        {
            IdentityValues id = _player.Identity;
            string identityBefore = JsonUtility.ToJson(id);
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today), "the practice room: today's");
            Assert.That(_levels.SetPracticeFocus(FocusMode.SteadyHand), Is.True, "F3");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.SteadyHand));

            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today), "a run starts with today's gauge, whatever the practice room had");
            Assert.That(_levels.SetPracticeFocus(FocusMode.HairTrigger), Is.False, "the practice keys do nothing in a run");
            yield return FinishFloorAndWaitForTheReward();
            _levels.PickReward(0);
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.HairTrigger));
            _levels.BackToPracticeRoom();
            yield return null;

            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.SteadyHand), "back to the practice room's own");
            Assert.That(_levels.SetPracticeFocus(FocusMode.Today), Is.True, "F4");
            Assert.That(_player.FocusMode, Is.EqualTo(FocusMode.Today));
            Assert.That(JsonUtility.ToJson(_player.Identity), Is.EqualTo(identityBefore), "nothing written to the settings");
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.EmpoweredRemaining, Is.EqualTo(id.EmpoweredDuration).Within(1e-3f), "today's window, as before");
            Assert.That(_player.IsSteadyHandWindow, Is.False);
            Assert.That(_player.Skills, Is.SameAs(_player.SettingsSkills), "the skills as they are");
        }

        [UnityTest]
        public IEnumerator TheRecorder_LearnsTheChoice_EachSpend_AndThePrimings()
        {
            _folder = Path.Combine(Path.GetTempPath(), "FocusChoiceTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(RunSeed);
            yield return null;
            yield return FinishFloorAndWaitForTheReward();
            _levels.PickReward(0);
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "a part");
            Assert.That(_player.PressSkill(Quick, _player.Position + Vector3.forward * 4f), Is.EqualTo(IntentResult.Started));
            yield return UntilFree();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "another part");
            yield return new WaitForSeconds(_player.FocusModeValues.PrimeSeconds + 0.1f);
            Assert.That(_player.PrimesLapsed, Is.EqualTo(1));
            _levels.BackToPracticeRoom();
            yield return null;

            List<SessionEvent> events = _recorder.Events.ToList();
            SessionEvent offer = events.Single(e => e.Type == SessionEventTypes.FocusChoiceOffer);
            Assert.That(offer.ExtraValue("offered"), Is.EqualTo("Hair Trigger | Steady Hand"));
            Assert.That(offer.ExtraValue("floor"), Is.EqualTo("1"));
            Assert.That(events.Single(e => e.Type == SessionEventTypes.FocusChoiceTake).Name, Is.EqualTo("Hair Trigger"));
            Assert.That(events.Where(e => e.Type == SessionEventTypes.FocusSpent).Select(e => e.ExtraValue("as")), Is.EqualTo(new[] { "part", "part" }));
            SessionEvent used = events.Single(e => e.Type == SessionEventTypes.PrimeUsed);
            Assert.That(used.Name, Is.EqualTo(_player.Skills[Quick].Name));
            Assert.That(used.Result, Is.EqualTo("a mark more"));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.PrimeLapsed), Is.EqualTo(1));
            string summary = SessionSummary.Format(SessionSummary.Compute(events));
            StringAssert.Contains("  focus choice     Hair Trigger   spent 2, primed skills used 1, lapsed 1", summary);
        }

        // ---------------- Hair Trigger ----------------

        [UnityTest]
        public IEnumerator HairTrigger_APartCutsCooldownsByItsShare_AndTheNextTripleTapsLastPress_IsCleanWhenPressedEarly()
        {
            Assert.That(_levels.SetPracticeFocus(FocusMode.HairTrigger), Is.True, "F2");
            FocusModeValues ways = _player.FocusModeValues;
            ways.PartBoostSeconds = 0f; // changed deliberately for work order 071: a part's gains off, this is the cut's and the priming's test (FocusFeltTests has the gains)
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return UntilFree();
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            _player.Teleport(NearA);
            float scatterLeft = _player.SkillCooldownRemaining(Scatter), dashLeft = _player.DashCooldownRemaining;
            Assume.That(scatterLeft, Is.GreaterThan(1f));

            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Rejected), "no part full");
            _player.FillFocus();
            Assert.That(_player.FocusPartsFull, Is.EqualTo(3), "she holds three");
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.FocusPartsFull, Is.EqualTo(2), "one part spent");
            Assert.That(_player.SkillCooldownRemaining(Scatter), Is.EqualTo(scatterLeft * (1f - ways.PartCooldownCut)).Within(1e-3f), "cut by 15 % of what is left");
            Assert.That(_player.DashCooldownRemaining, Is.EqualTo(dashLeft * (1f - ways.PartCooldownCut)).Within(1e-3f), "and the dash's");
            Assert.That(_player.IsEmpowered, Is.False, "no window");
            Assert.That(_player.IsPrimed, Is.True, "the next skill is primed");

            int marksBefore = _a.MarkCount, lostBefore = Lost(_a);
            SkillDefinition tap = _player.Skills[Triple];
            var presses = new List<int>();
            yield return TapEarly(presses);
            Assert.That(_player.IsPrimed, Is.False, "Triple Tap spent it");
            Assert.That(presses.Count, Is.EqualTo(3));
            yield return Until(() => _a.HitsTaken >= 3, 2f);
            yield return UntilFree();
            List<PlayerReport> recasts = Reports(PlayerReportKind.RecastPressed).ToList();
            Assert.That(recasts.Select(r => r.Clean), Is.EqualTo(new[] { false, true }), "the last press clean, pressed early; the second not");
            Assert.That(_a.MarkCount - marksBefore, Is.EqualTo(2 * tap.MarksAdded + tap.CleanTapMarks), "two marks from the clean tap");
            Assert.That(Lost(_a) - lostBefore, Is.EqualTo(2 * tap.Damage + Mathf.RoundToInt(tap.Damage * _player.Kit.CleanDamage)));
            Assert.That(Reports(PlayerReportKind.PrimeUsed).Single().Prime, Is.EqualTo(PrimeUse.CleanPress));

            // Unprimed, the same early last press is an ordinary shot.
            _player.StartFresh();
            _reports.Clear();
            presses.Clear();
            yield return TapEarly(presses);
            yield return UntilFree();
            Assert.That(Reports(PlayerReportKind.RecastPressed).Select(r => r.Clean), Is.EqualTo(new[] { false, false }));
        }

        [UnityTest]
        public IEnumerator HairTrigger_APrimedLongShot_LetGoAtHalfACharge_IsPerfect()
        {
            Assert.That(_levels.SetPracticeFocus(FocusMode.HairTrigger), Is.True);
            _player.FocusModeValues.PartBoostSeconds = 0f; // changed deliberately for work order 071: a part's gains off, this is the priming's test
            SkillDefinition shot = _player.Skills[Long];
            Assume.That(shot.PerfectFrom, Is.GreaterThan(0.6f), "half a charge is short of the band");
            _player.Teleport(NearA);
            yield return null;

            // Unprimed first: let go at 0.55 of a charge, it is no perfect release.
            yield return ChargeAndLetGo(0.55f);
            Assert.That(_player.PerfectReleases, Is.EqualTo(0), "unprimed: not perfect");

            _player.StartFresh();
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            int lostBefore = Lost(_a);
            yield return ChargeAndLetGo(0.55f);
            Assert.That(_player.PerfectReleases, Is.EqualTo(1), "primed: perfect at half a charge");
            Assert.That(Lost(_a) - lostBefore, Is.EqualTo(PoolRules.PerfectDamage(shot)), "the perfect damage");
            Assert.That(Reports(PlayerReportKind.SkillFired).Last(r => r.Slot == Long).Clean, Is.True, "shown as a perfect release");
            Assert.That(_player.IsPrimed, Is.False);
        }

        IEnumerator ChargeAndLetGo(float seconds)
        {
            int hits = _a.HitsTaken;
            Assert.That(_player.PressSkill(Long, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillChargedSeconds >= seconds, 3f);
            Assume.That(_player.SkillChargedSeconds, Is.LessThan(_player.Skills[Long].PerfectFrom), "let go before the band");
            _player.ReleaseSkill(Long);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            yield return UntilFree();
        }

        [UnityTest]
        public IEnumerator HairTrigger_APrimingNoSkillUses_LapsesAfterFourSeconds()
        {
            Assert.That(_levels.SetPracticeFocus(FocusMode.HairTrigger), Is.True);
            _player.Teleport(NearA);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            float spentAt = Time.time;
            Assert.That(_player.IsPrimed, Is.True);
            yield return Until(() => !_player.IsPrimed, 6f);
            Assert.That(Time.time - spentAt, Is.EqualTo(_player.FocusModeValues.PrimeSeconds).Within(0.05f), "four seconds");
            yield return null;
            Assert.That(_player.PrimesLapsed, Is.EqualTo(1));
            Assert.That(Reports(PlayerReportKind.PrimeLapsed).Count(), Is.EqualTo(1));
            var presses = new List<int>();
            yield return TapEarly(presses);
            yield return UntilFree();
            Assert.That(Reports(PlayerReportKind.RecastPressed).Select(r => r.Clean), Is.EqualTo(new[] { false, false }), "lapsed: nothing primed");
            Assert.That(Reports(PlayerReportKind.PrimeUsed), Is.Empty);
        }

        // ---------------- Steady Hand ----------------

        [UnityTest]
        public IEnumerator SteadyHand_TheWindowIsTenSeconds_TripleTapTakesFivePressesInIt_AndThreeOutside_AndScatterBlastLandsTwice()
        {
            Assert.That(_levels.SetPracticeFocus(FocusMode.SteadyHand), Is.True, "F3");
            FocusModeValues ways = _player.FocusModeValues;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Rejected), "only a full gauge");
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            float openedAt = Time.time;
            Assert.That(_player.EmpoweredRemaining, Is.EqualTo(ways.WindowSeconds).Within(1e-3f), "ten seconds");
            Assert.That(_player.IsSteadyHandWindow, Is.True);
            Assert.That(_player.Skills[Triple].RecastCount, Is.EqualTo(5));
            Assert.That(_player.SettingsSkills[Triple].RecastCount, Is.EqualTo(3), "the settings are never written");

            int hits = _a.HitsTaken;
            var presses = new List<int>();
            yield return TapEarly(presses);
            yield return UntilFree();
            Assert.That(presses.Count, Is.EqualTo(5), "five presses in the window");
            Assert.That(_a.HitsTaken - hits, Is.EqualTo(5), "five shots");
            Assert.That(Reports(PlayerReportKind.ChainEnded).Last().Shot, Is.EqualTo(5));

            // Scatter Blast: two blasts a quarter second apart, each spending the marks it finds.
            _a.AddMarks(5, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            int fired = Reports(PlayerReportKind.SkillFired).Count(r => r.Slot == Scatter);
            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => Reports(PlayerReportKind.SkillFired).Count(r => r.Slot == Scatter) >= fired + 2, 2f);
            var blasts = Enumerable.Range(0, _reports.Count).Where(i => _reports[i].Kind == PlayerReportKind.SkillFired && _reports[i].Slot == Scatter)
                                   .Skip(fired).Select(i => _reportTimes[i]).ToList();
            Assert.That(blasts.Count, Is.EqualTo(2), "it lands twice");
            Assert.That(blasts[1] - blasts[0], Is.EqualTo(ways.TwiceDelay).Within(0.02f), "the second 0.25 s after the first");
            List<PlayerReport> spent = Reports(PlayerReportKind.MarksSpent).Where(r => r.Slot == Scatter && r.Target == _a).ToList();
            Assert.That(spent.Count, Is.EqualTo(2), "each blast spends what it finds");
            Assert.That(spent[0].Marks, Is.EqualTo(5));
            Assert.That(spent[1].Marks, Is.EqualTo(0), "nothing left for the second");
            yield return UntilFree();

            // The window ends ten seconds after it opened; outside it Triple Tap takes three presses.
            yield return Until(() => !_player.IsEmpowered, 12f);
            Assert.That(Time.time - openedAt, Is.EqualTo(ways.WindowSeconds).Within(0.05f));
            Assert.That(_player.IsSteadyHandWindow, Is.False);
            Assert.That(_player.Skills[Triple].RecastCount, Is.EqualTo(3));
            _player.StartFresh(); // the cooldowns ready again
            hits = _a.HitsTaken;
            presses.Clear();
            yield return TapEarly(presses);
            yield return UntilFree();
            Assert.That(presses.Count, Is.EqualTo(3), "three outside the window");
            Assert.That(_a.HitsTaken - hits, Is.EqualTo(3));
        }

        // ---------------- The chests' new rows ----------------

        [UnityTest]
        public IEnumerator TheNewChestRows_EachChangesWhatItNames()
        {
            var modifiers = new RunModifiers();
            _player.Modifiers = modifiers;
            KitValues kit = _player.Kit;
            IdentityValues id = _player.Identity;
            SkillDefinition quick = _player.Skills[Quick];
            _a.Stagger = new StaggerBar(new StaggerValues { Max = 100000f });
            var stagger = new List<float>();
            _a.StaggerTaken += (taken, left) => stagger.Add(taken);
            _player.Teleport(NearA);
            yield return null;

            // Lasting marks, Keen focus and Heavy hits, on one Quick Shot.
            modifiers.Take(new Upgrade { Kind = UpgradeKind.LastingMarks, Amount = 2f });
            modifiers.Take(new Upgrade { Kind = UpgradeKind.KeenFocus, Amount = 0.1f });
            modifiers.Take(new Upgrade { Kind = UpgradeKind.HeavyHits, Amount = 0.1f });
            int lostBefore = Lost(_a);
            float focusBefore = _player.Focus;
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.MarkCount > 0, 2f);
            Assert.That(_a.MarksFallOffIn, Is.EqualTo(kit.MarkLifetime + 2f).Within(0.05f), "the marks stay 2 s longer");
            int dealt = Lost(_a) - lostBefore;
            Assert.That(_player.Focus - focusBefore, Is.EqualTo(dealt * id.FocusPerDamage * 1.1f).Within(1e-3f), "focus fills 10 % faster");
            Assert.That(stagger.Last(), Is.EqualTo(quick.Stagger * 1.1f).Within(1e-3f), "10 % more off a stagger bar");
            yield return UntilFree();

            // Longer barrel: a dummy just past the basic shot's reach (its range, its half width and the dummy's radius) is hit with a metre more.
            float range = _player.Feel.BasicShotRange;
            Assert.That(_player.BasicShotRange, Is.EqualTo(range));
            Vector3 at = _a.transform.position + Vector3.forward * (range + _player.Feel.BasicShotHalfWidth + _a.Radius + 0.4f); // from the north: no other dummy on the line, far from the brute
            _player.Teleport(new Vector3(at.x, 0f, at.z));
            yield return null;
            int hits = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => Reports(PlayerReportKind.ShotFired).Any(), 2f);
            yield return UntilFree();
            Assert.That(_a.HitsTaken, Is.EqualTo(hits), "out of reach");
            modifiers.Take(new Upgrade { Kind = UpgradeKind.LongerBarrel, Amount = 1f });
            Assert.That(_player.BasicShotRange, Is.EqualTo(range + 1f));
            yield return new WaitForSeconds(0.3f);
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > hits, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(hits + 1), "a metre farther: hit");
            _player.Modifiers = null;
            Assert.That(_player.BasicShotRange, Is.EqualTo(range), "out of a run: as it was");
        }

        [UnityTest]
        public IEnumerator Mend_HealsThirtyNow_WithoutRaisingHerMaximum_AndIsNotOfferedAtFullHealth()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.RunValues.Upgrades = new[]
            {
                new UpgradeDefinition(UpgradeKind.Mend, 0f, heal: 30),
                new UpgradeDefinition(UpgradeKind.LastingMarks, 2f),
                new UpgradeDefinition(UpgradeKind.KeenFocus, 0.1f),
            };
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assume.That(chests.Count, Is.GreaterThanOrEqualTo(2));

            yield return StandBy(chests[0]);
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.OfferedUpgrades.Any(u => u.Kind == UpgradeKind.Mend), Is.False, "at full health: no Mend");
            _levels.PickUpgrade(0);

            _health.TakeHit(50);
            int health = _health.Current, max = _health.Max;
            yield return StandBy(chests[1]);
            _levels.RunValues.Upgrades = new[] { new UpgradeDefinition(UpgradeKind.Mend, 0f, heal: 30), new UpgradeDefinition(UpgradeKind.HeavyHits, 0.1f) };
            Assert.That(_levels.OpenChest(), Is.True);
            int mend = _levels.OfferedUpgrades.ToList().FindIndex(u => u.Kind == UpgradeKind.Mend);
            Assert.That(mend, Is.GreaterThanOrEqualTo(0), "hurt: Mend is offered");
            StringAssert.Contains("heal 30", _levels.OfferedUpgrades[mend].Text);
            Assert.That(_levels.PickUpgrade(mend), Is.True);
            Assert.That(_health.Current, Is.EqualTo(health + 30), "healed 30 now");
            Assert.That(_health.Max, Is.EqualTo(max), "her maximum stays");
            StringAssert.Contains("heal 30", _levels.StatusText(), "the list shows it in short form");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.Modifiers.Count, Is.Zero, "cleared with the run");
        }

        IEnumerator StandBy(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }
    }
}
