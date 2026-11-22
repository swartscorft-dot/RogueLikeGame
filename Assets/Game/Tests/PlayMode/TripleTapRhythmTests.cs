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
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 069 (Kristian, 2026-11-21 evening: "høyreklik er litt klunky. jeg vil at de to første skal kome relativt
    /// fort, og det 3. litt senere. men de første er veldig utydlig når skal skje"), in the built ControlRoom scene through
    /// the intents: Triple Tap's second press is quick (its window open to 0.45 s after the first shot, clean 0.12 to 0.30 s),
    /// its third waits a beat (open to 0.80 s, clean 0.42 to 0.66 s); tap-tap-beat-tap at the clean times is three clean
    /// presses and a full clean chain's pay; the old even beat is not; what a press at given times does, for the second and
    /// the third; a press a little before its window opens fires the instant it opens; what the interface is told equals what
    /// is judged in the frame of each press; Backstep as it was; the switch off gives the morning's; reading the chain's line
    /// allocates nothing. Nobody has seen or heard any of it: these are numbers.
    /// Layout: player at (0, 0), Dummy_A at (4, 3). Triple Tap is slot 7, Backstep slot 6.
    /// </summary>
    public class TripleTapRhythmTests
    {
        const int Backstep = 6, Triple = 7;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);

        PlayerCharacter _player;
        Damageable _a;
        readonly List<PlayerReport> _recasts = new List<PlayerReport>();
        readonly List<Object> _copies = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _recasts.Clear();
            _player.Reported += OnReport;
            _player.Teleport(NearA);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Reported -= OnReport;
            foreach (Object o in _copies) if (o != null) Object.Destroy(o);
            _copies.Clear();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.RecastPressed) _recasts.Add(r);
        }

        void OwnKit(Action<KitValues> change)
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet); // the settings asset is shared: a copy of it
            _copies.Add(copy);
            change(copy.Kit);
            _player.SkillSet = copy;
        }

        Vector3 At => _a.transform.position;

        /// <summary>Waits for the window of the next press, then until its clock reads <paramref name="since"/> (or the chain is over).</summary>
        IEnumerator UntilSince(float since)
        {
            yield return Until(() => _player.IsRecastWindowOpen || _player.RecastSlot != Triple, 2f);
            yield return Until(() => _player.RecastSlot != Triple || _player.RecastSinceOpened >= since, 2f);
        }

        /// <summary>
        /// Presses Triple Tap's key now, and checks that what the interface was told in this frame is what was judged: the
        /// clean part shown is the line's for the press, and "would be clean" is the press's own timing.
        /// </summary>
        IntentResult PressAndCompare(string what)
        {
            int had = _recasts.Count;
            bool told = _player.RecastPressWouldBeClean;
            CleanPart shown = _player.RecastCleanPart;
            RecastLine line = _player.RecastLine;
            if (line.Next > 0)
            {
                Assert.That(line.Beat(line.Next).Clean.From, Is.EqualTo(shown.From).Within(1e-6f), what + ": the line's clean part is the one shown");
                Assert.That(line.Beat(line.Next).Clean.To, Is.EqualTo(shown.To).Within(1e-6f));
                Assert.That(line.Beat(line.Next).Window, Is.EqualTo(_player.RecastWindowSeconds).Within(1e-6f), what + ": the line's window is the one that runs");
            }
            IntentResult result = _player.PressSkill(Triple, At);
            if (_recasts.Count > had)
                Assert.That(_recasts[_recasts.Count - 1].Timing == RecastTiming.Clean, Is.EqualTo(told), what + ": told " + told + ", judged " + _recasts[_recasts.Count - 1].Timing);
            else
                Assert.That(told, Is.False, what + ": no recast, and none was told clean");
            return result;
        }

        // ---------------- 1. Tap-tap, a beat, tap ----------------

        [UnityTest]
        public IEnumerator TapTapBeatTap_AtTheCleanTimes_IsThreeCleanPresses_AndAFullCleanChainsPay()
        {
            SkillDefinition tap = _player.Skills[Triple];
            KitValues kit = _player.Kit;
            int chainsBefore = _player.FullCleanChains;
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started), "the first press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastWindowSeconds, Is.EqualTo(0.45f).Within(1e-5f), "the second press's window: quick");
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(0.12f).Within(1e-5f));
            Assert.That(_player.RecastCleanTo, Is.EqualTo(0.30f).Within(1e-5f));
            Assert.That(_player.RecastLine.Next, Is.EqualTo(2));
            yield return UntilSince(0.21f);
            Assert.That(_player.RecastPressWouldBeClean, Is.True, "the second press is shown clean in its clean part (before work order 069: never)");
            Assert.That(PressAndCompare("second"), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastWindowSeconds, Is.EqualTo(0.80f).Within(1e-5f), "the third press's window: the beat");
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(0.42f).Within(1e-5f));
            Assert.That(_player.RecastCleanTo, Is.EqualTo(0.66f).Within(1e-5f));
            yield return UntilSince(0.54f);
            Assert.That(PressAndCompare("third"), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 3, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            Assert.That(_recasts.Select(r => r.Timing), Is.EqualTo(new[] { RecastTiming.Clean, RecastTiming.Clean }), "both recast presses in rhythm");
            Assert.That(_recasts.Select(r => r.Clean), Is.EqualTo(new[] { false, true }), "the third a clean tap; the second in rhythm, no clean tap of its own (as before)");
            Assert.That(_player.RecastTimingOf(2), Is.EqualTo(RecastTiming.Clean), "the line keeps them after the chain");
            Assert.That(_player.RecastTimingOf(3), Is.EqualTo(RecastTiming.Clean));
            Assert.That(_player.FullCleanChains - chainsBefore, Is.EqualTo(1), "a full clean chain");
            int third = Mathf.RoundToInt(tap.Damage * kit.CleanDamage * kit.FullChainScale); // not in a run: no stats, no crit
            Assert.That(_a.MaxHealth - _a.CurrentHealth, Is.EqualTo(2 * tap.Damage + third), "a full clean chain's pay, as work order 064 made it");
            Assert.That(_player.LastShotWeight, Is.EqualTo(ShotWeight.FullClean));
        }

        [UnityTest]
        public IEnumerator TheOldEvenBeat_05After05_IsNotTwoCleanPresses_TheChainIsOverAtOneShot()
        {
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return UntilSince(0.5f);
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the second press's window closed at 0.45 s: the chain is over");
            Assert.That(PressAndCompare("second at 0.5"), Is.EqualTo(IntentResult.Rejected), "a press after it is an ordinary press, on cooldown");
            Assert.That(_recasts.Count, Is.EqualTo(0), "no recast");
            Assert.That(_player.RecastTimingOf(2), Is.EqualTo(RecastTiming.Missed), "the line shows the press missed");
        }

        // ---------------- 2. A press at given times ----------------

        [UnityTest] public IEnumerator TheSecondPress_At005_IsAShot_Early_NotInRhythm() => Second(0.05f, RecastTiming.Early);
        [UnityTest] public IEnumerator TheSecondPress_At020_IsAShot_InRhythm() => Second(0.20f, RecastTiming.Clean);
        [UnityTest] public IEnumerator TheSecondPress_At040_IsAShot_Late() => Second(0.40f, RecastTiming.Late);
        [UnityTest] public IEnumerator TheSecondPress_At050_IsNoShot_TheWindowRanOut() => Second(0.50f, RecastTiming.Missed);

        IEnumerator Second(float since, RecastTiming want)
        {
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return UntilSince(since);
            IntentResult r = PressAndCompare("second at " + since);
            Assert.That(_player.RecastTimingOf(2), Is.EqualTo(want), "the second press at " + since + " s");
            if (want == RecastTiming.Missed)
            {
                Assert.That(r, Is.EqualTo(IntentResult.Rejected));
                yield break;
            }
            Assert.That(r, Is.EqualTo(IntentResult.Started), "a shot");
            Assert.That(_recasts.Single().Clean, Is.False, "never a clean tap: the second press");
            Assert.That(_recasts.Single().Late, Is.EqualTo(want == RecastTiming.Late));
            Assert.That(_recasts.Single().Seconds, Is.EqualTo(since).Within(0.02f), "the window's clock at the press, for the log");
            yield return Until(() => _player.IsRecastWindowOpen || _player.RecastSlot != Triple, 2f);
            Assert.That(_player.RecastSlot, Is.EqualTo(Triple), "the chain goes on to the third");
        }

        [UnityTest] public IEnumerator TheThirdPress_At005_IsAShot_Early() => Third(0.05f, RecastTiming.Early);
        [UnityTest] public IEnumerator TheThirdPress_At020_IsAShot_Early_TheMorningsCleanTimeIsNotCleanAnyMore() => Third(0.20f, RecastTiming.Early);
        [UnityTest] public IEnumerator TheThirdPress_At040_IsAShot_Early() => Third(0.40f, RecastTiming.Early);
        [UnityTest] public IEnumerator TheThirdPress_At050_IsClean() => Third(0.50f, RecastTiming.Clean);
        [UnityTest] public IEnumerator TheThirdPress_At070_IsAShot_Late() => Third(0.70f, RecastTiming.Late);
        [UnityTest] public IEnumerator TheThirdPress_At085_IsNoShot_TheWindowRanOut() => Third(0.85f, RecastTiming.Missed);

        IEnumerator Third(float since, RecastTiming want)
        {
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return UntilSince(0.21f);
            Assert.That(PressAndCompare("second"), Is.EqualTo(IntentResult.Started));
            yield return UntilSince(since);
            IntentResult r = PressAndCompare("third at " + since);
            Assert.That(_player.RecastTimingOf(3), Is.EqualTo(want), "the third press at " + since + " s");
            if (want == RecastTiming.Missed)
            {
                Assert.That(r, Is.EqualTo(IntentResult.Rejected));
                Assert.That(_recasts.Count, Is.EqualTo(1));
                yield break;
            }
            Assert.That(r, Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Count, Is.EqualTo(2));
            Assert.That(_recasts[1].Clean, Is.EqualTo(want == RecastTiming.Clean), "a clean tap only in its clean part");
            Assert.That(_recasts[1].Late, Is.EqualTo(want == RecastTiming.Late), "LATE after it (the word says so)");
            Assert.That(FeedbackRules.RecastMiss(_player.Skills[Triple], 3, _recasts[1].Clean, _recasts[1].Late),
                        Is.EqualTo(want == RecastTiming.Clean ? TimingMiss.None : want == RecastTiming.Late ? TimingMiss.Late : TimingMiss.Early), "the word over her");
        }

        // ---------------- 3. A press a little before its window opens ----------------

        [UnityTest]
        public IEnumerator APress005BeforeTheWindowOpens_IsNotLost_ItFiresTheInstantItOpens_NeverClean()
        {
            SkillDefinition tap = _player.Skills[Triple];
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.RecastSincePress >= tap.Startup - 0.05f, 2f);
            Assert.That(_player.IsRecastWindowOpen, Is.False, "the first shot has not gone off");
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Buffered), "it waits, as before");
            yield return Until(() => _recasts.Count > 0, 2f);
            Assert.That(_recasts.Count, Is.EqualTo(1), "it fired: not lost");
            Assert.That(_recasts[0].Timing, Is.EqualTo(RecastTiming.Waited));
            Assert.That(_recasts[0].Clean, Is.False, "a press that waited is never clean");
            Assert.That(_recasts[0].Seconds, Is.LessThan(0.02f), "the instant the window opened (until work order 069 it waited out the shot's free recovery: 0.1 s)");
            // The same for the third press, 0.05 s before the second shot goes off.
            yield return Until(() => _player.RecastSincePress >= tap.Startup - 0.05f, 2f);
            Assert.That(_player.IsRecastWindowOpen, Is.False, "the second shot has not gone off");
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Buffered));
            yield return Until(() => _recasts.Count > 1, 2f);
            Assert.That(_recasts.Count, Is.EqualTo(2), "the third went too");
            Assert.That(_recasts[1].Timing, Is.EqualTo(RecastTiming.Waited));
            Assert.That(_recasts[1].Clean, Is.False, "never a clean tap");
            Assert.That(_recasts[1].Seconds, Is.LessThan(0.02f));
            yield return Until(() => _a.HitsTaken >= 3, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(3), "three shots");
        }

        // ---------------- 4. What the interface is told, frame by frame ----------------

        [UnityTest]
        public IEnumerator WhatTheInterfaceIsTold_EqualsWhatIsJudged_EveryFrame_OfEachPress()
        {
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= 3; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                float aim = press == 2 ? 0.21f : 0.54f;
                int frames = 0, toldClean = 0;
                while (_player.IsRecastWindowOpen && _player.RecastSinceOpened < aim && frames < 100000)
                {
                    RecastLine line = _player.RecastLine;
                    CleanPart part = line.Beat(press).Clean;
                    float since = _player.RecastSinceOpened;
                    bool inPart = since >= part.From - 1e-5f && since <= part.To + 1e-5f;
                    bool nearEdge = Mathf.Abs(since - part.From) < 2e-3f;
                    Assert.That(line.Next, Is.EqualTo(press));
                    Assert.That(line.SinceShotBefore, Is.EqualTo(since).Within(1e-5f));
                    if (!nearEdge) Assert.That(_player.RecastPressWouldBeClean, Is.EqualTo(inPart), "press " + press + " at " + since);
                    if (_player.RecastPressWouldBeClean) toldClean++;
                    frames++;
                    yield return null;
                }
                Assert.That(toldClean, Is.GreaterThan(0), "press " + press + ": its clean part was shown");
                Assert.That(PressAndCompare("press " + press), Is.EqualTo(IntentResult.Started));
            }
            Assert.That(_recasts.All(r => r.Timing == RecastTiming.Clean), Is.True);
        }

        [UnityTest]
        public IEnumerator ReadingTheLine_AllocatesNothing()
        {
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            NUnit.Framework.Constraints.IResolveConstraint notAllocating = AllocExt.AllocatingGCMemory(Is.Not);
            TestDelegate reads = () =>
            {
                for (int i = 0; i < 300; i++)
                {
                    RecastLine line = _player.RecastLine;
                    for (int p = 2; p <= line.Count; p++)
                    {
                        RecastBeat b = line.Beat(p);
                        if (b.Window < 0f || line.Timing(p) == RecastTiming.Missed) Debug.Log("never");
                    }
                    if (_player.RecastPressWouldBeClean && _player.RecastCleanPart.Width < 0f) Debug.Log("never");
                }
            };
            reads(); // once first, as the interface's first frame would: only the frames after count
            yield return null;
            Assert.That(reads, notAllocating, "the combo bar's reads, every frame");
        }

        // ---------------- 5. As they were ----------------

        [UnityTest]
        public IEnumerator Backstep_ItsOnePress_AsItWas()
        {
            Assert.That(_player.PressSkill(Backstep, At), Is.EqualTo(IntentResult.Started), "the hop");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastWindowSeconds, Is.EqualTo(_player.Skills[Backstep].RecastWindow).Within(1e-5f), "0.6 s");
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(_player.Kit.OpeningMoveCleanFrom).Within(1e-5f), "0.03 s");
            Assert.That(_player.RecastCleanTo, Is.EqualTo(_player.Kit.OpeningMoveCleanTo).Within(1e-5f), "0.23 s");
            RecastLine line = _player.RecastLine;
            Assert.That(line.Count, Is.EqualTo(2));
            Assert.That(line.Next, Is.EqualTo(2));
            Assert.That(line.Beat(2).Last, Is.True);
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_GivesTheMorningsChain_TheSkillsWindowAndTheKitsCleanPart()
        {
            OwnKit(k => k.ChainRhythm = false);
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastWindowSeconds, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(_player.RecastCleanPart.Any, Is.False, "the second press shows no clean part, as this morning");
            yield return UntilSince(0.2f);
            Assert.That(_player.RecastPressWouldBeClean, Is.False, "nor a tick");
            Assert.That(_player.PressSkill(Triple, At), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Timing, Is.EqualTo(RecastTiming.Clean), "in rhythm by the kit's clean part, as this morning (toward a full clean chain)");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastWindowSeconds, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(_player.RecastCleanFrom, Is.EqualTo(0.10f).Within(1e-5f));
            Assert.That(_player.RecastCleanTo, Is.EqualTo(0.30f).Within(1e-5f));
            yield return UntilSince(0.2f);
            Assert.That(PressAndCompare("third"), Is.EqualTo(IntentResult.Started));
            Assert.That(_recasts.Last().Clean, Is.True, "clean at 0.2 s, as this morning");
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
