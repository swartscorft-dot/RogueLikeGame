using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043), in the ControlRoom
    /// scene through the intents (<see cref="PlayerCharacter.TapCover"/> and the key, <see cref="PlayerCharacter.HoldCover"/>):
    /// a tap beside a low prop ducks her on the spot and she rises by herself; an arrow from the far side is stopped at the
    /// prop while she is down, one from her side or one that comes after she rose hits her, and an orb hits her through a
    /// duck; a tap with nothing in reach, or beside a tall prop or a wall, does nothing and starts no wait; the key waits
    /// after a duck; a move key or the dash ends it, and a stun ends it and keeps one from starting; she cannot shoot or use
    /// a skill while down, and what she pressed goes off as she rises; the rim light is on the nearest low prop in reach and
    /// nowhere else; the recorder counts it all; and with the switch off the key takes cover as before.
    ///
    /// The practice room has nothing to duck behind, so each test builds its props as a level builds them (with the duck's
    /// sort: <see cref="Cover.LowUnderQuickDuck"/>). She stands at its middle and everything is laid out along <c>_ahead</c>,
    /// the axis that points most away from the practice brute (switched off). Enemies are spawned as the practice room
    /// spawns them and cannot walk. Every number is read from the values (the arrow's speed is another order's to tune); no
    /// assertion counts frames.
    /// </summary>
    public class QuickDuckTests
    {
        static readonly Vector3 Stand = Vector3.zero;

        PlayerCharacter _player;
        PlayerHealth _health;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas, _quickDuckWas, _duckHoldWas;
        Vector3 _ahead, _side;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        PlaySessionRecorder _recorder;
        string _folder;
        readonly List<Object> _made = new List<Object>();
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

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
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _quickDuckWas = _feel.QuickDuck;
            _duckHoldWas = _feel.DuckHold;
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
            _feel.QuickDuck = true;
            _feel.DuckHold = false; // work order 086: these are the tap's tests (its switch off); the held duck's are QuickDuckHeldTests
            _reports.Clear();
            _player.Reported += r => _reports.Add(r);
            _folder = Path.Combine(Path.GetTempPath(), "QuickDuckTests-" + Guid.NewGuid().ToString("N"));

            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice enemy");
            brute.enabled = false;
            Vector3 away = Flat(Stand - brute.transform.position);
            Vector3[] axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            _ahead = axes.OrderByDescending(a => Vector3.Dot(a, away.normalized)).First();
            _side = Vector3.Cross(Vector3.up, _ahead);
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Put(d.transform, Stand - _ahead * 13f + _side * (k++ * 3f - 7.5f));
            }
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            _player.Teleport(Stand);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
                _feel.QuickDuck = _quickDuckWas;
                _feel.DuckHold = _duckHoldWas;
            }
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null)
            {
                _levels.ClearSpawned();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The duck ----------------

        [UnityTest]
        public IEnumerator ATap_BesideALowProp_DucksHerOnTheSpot_AndSheRisesAfterTheDuckSeconds()
        {
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            Assert.That(sarcophagus.GetComponent<Cover>(), Is.Null, "a low prop");
            yield return Synced();
            Vector3 at = _player.Position;

            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            Assert.That(_player.IsQuickDucking, Is.True, "down");
            Assert.That(_player.IsDucked, Is.True, "and ducked, for the pose");
            Assert.That(_player.DuckObject, Is.SameAs(sarcophagus.GetComponent<Shelter>()));
            Assert.That(_player.IsInCover, Is.False, "no cover taken");
            float down = _player.DuckDownAt;
            float worst = 0f;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_player.IsQuickDucking && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                worst = Mathf.Max(worst, FlatDistance(_player.Position, at));
            }
            float seen = Time.time;
            Assert.That(_player.IsQuickDucking, Is.False, "she rose");
            Assert.That(worst, Is.LessThan(0.01f), "nothing moved her: no snap, no slide");
            Assert.That(_player.DuckRoseAt - down, Is.EqualTo(_feel.DuckSeconds).Within(1e-4f), "after the duck seconds");
            Assert.That(seen, Is.GreaterThanOrEqualTo(down + _feel.DuckSeconds - 1e-4f));
            Assert.That(seen - (down + _feel.DuckSeconds), Is.LessThan(0.1f), "by herself, at its time");
            Assert.That(_player.DucksTaken, Is.EqualTo(1));
            PlayerReport ended = _reports.Last(r => r.Kind == PlayerReportKind.DuckEnded);
            Assert.That(ended.DuckEnd, Is.EqualTo(DuckEnd.Rose));
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator TheKey_IsATap_AndWaitsTheWaitAfterADuck()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            _player.HoldCover(true); // the key going down is the tap
            Assert.That(_player.IsQuickDucking, Is.True, "the key ducked her");
            _player.HoldCover(true); // held on: nothing more
            _player.HoldCover(false);
            Assert.That(_player.IsQuickDucking, Is.True, "letting go does not stand her up");
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Down));
            yield return Until(() => !_player.IsQuickDucking, 2f);
            float rose = _player.DuckRoseAt;
            Assert.That(_player.DuckWaitLeft, Is.GreaterThan(0f));

            yield return Until(() => Time.time >= rose + _feel.DuckWait - 0.05f, 3f);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Waiting), "the key waits");
            Assert.That(_player.IsQuickDucking, Is.False);
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.False, "the key too");
            _player.HoldCover(false);
            yield return Until(() => Time.time >= rose + _feel.DuckWait, 1f);
            Assert.That(_player.DuckWaitLeft, Is.EqualTo(0f));
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "it works again after the wait");
            Assert.That(_player.DucksTaken, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ATap_WithNothingInReach_DoesNothing_AndStartsNoWait()
        {
            GameObject far = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, _feel.DuckReach + 0.3f));
            yield return Synced();
            Vector3 at = _player.Position;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.NothingInReach));
            Assert.That(_player.IsQuickDucking, Is.False);
            Assert.That(_player.DuckWaitLeft, Is.EqualTo(0f), "no wait");
            Assert.That(_player.DuckTapsWithNothing, Is.EqualTo(1));
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.DuckNothing), Is.EqualTo(1));
            Assert.That(_reports.Any(r => r.Kind == PlayerReportKind.Ducked), Is.False);
            yield return null;
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.01f), "she did not move");

            // In reach at once: a tap ducks her, there was no wait.
            Object.Destroy(far);
            Prop(PropModel.Coffin, FaceAt(PropModel.Coffin, _ahead, 0.5f));
            yield return Synced();
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
        }

        [UnityTest]
        public IEnumerator ATap_BesideATallProp_OrAWall_DoesNothing()
        {
            GameObject pillar = Prop(PropModel.Pillar, FaceAt(PropModel.Pillar, _ahead, 0.5f));
            Assert.That(pillar.GetComponent<Shelter>().IsTall, Is.True, "a tall prop");
            yield return Synced();
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.NothingInReach), "a pillar stops what flies already");
            Assert.That(_player.IsQuickDucking, Is.False);
            Object.Destroy(pillar);
            Wall(Stand + _ahead * (0.5f + 0.15f), 6f, 0.3f);
            yield return Synced();
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.NothingInReach), "so does a wall");
            Assert.That(_player.DucksTaken, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ABrokenPillar_BuiltAsALevelBuildsIt_IsLowWithTheSwitchOn_AndTallWithItOff()
        {
            // Work order 043: 1.2 m, cover by its height, but low by its look under the duck's sort.
            GameObject stump = Prop(PropModel.PillarBroken, FaceAt(PropModel.PillarBroken, _ahead, 0.5f));
            Assert.That(stump.GetComponent<Cover>(), Is.Not.Null, "cover by its height, as a level builds it");
            Assert.That(stump.GetComponent<Cover>().LowUnderQuickDuck, Is.True);
            yield return Synced();
            float clear = Cover.ClearDistance(Flat(Stand), _ahead, 6f, Cover.RayHeight);
            Assert.That(clear, Is.EqualTo(6f).Within(1e-3f), "with the switch on a shot passes over it");
            Assert.That(stump.GetComponent<Shelter>().IsTall, Is.False);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "and she ducks behind it");
            yield return Until(() => !_player.IsQuickDucking, 2f);

            _feel.QuickDuck = false;
            Assert.That(Cover.ClearDistance(Flat(Stand), _ahead, 6f, Cover.RayHeight), Is.LessThan(1f), "off, it stops a shot as before");
            Assert.That(stump.GetComponent<Shelter>().IsTall, Is.True);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Off));
        }

        // ---------------- What ends it ----------------

        [UnityTest]
        public IEnumerator AMoveKey_EndsTheDuckAtOnce_ButOneHeldThroughTheTapDoesNot_AndTheDashEndsItToo()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            Vector3 at = _player.Position;
            yield return null;
            _player.Steer(_side);
            yield return Until(() => !_player.IsQuickDucking, 0.5f);
            Assert.That(_player.IsQuickDucking, Is.False, "told to move, she is up");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.DuckEnded).DuckEnd, Is.EqualTo(DuckEnd.Moved));
            Assert.That(Time.time - _player.DuckDownAt, Is.LessThan(_feel.DuckSeconds), "before its time");
            yield return Until(() => _player.Speed >= _feel.RunSpeed - 0.3f, 1f);
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.3f), "and moves at once");
            _player.Stop();

            // A key held through the tap: she stops and stays down; let go and pressed again, she moves.
            _player.Teleport(at);
            yield return Until(() => Time.time >= _player.DuckRoseAt + _feel.DuckWait, 2f);
            _player.Steer(_side);
            yield return null;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "Teleport cleared the wait");
            Vector3 down = _player.Position;
            float until = Time.time + 0.2f;
            while (Time.time < until) yield return null;
            Assert.That(_player.IsQuickDucking, Is.True, "the key she held at the tap does not end it");
            Assert.That(FlatDistance(_player.Position, down), Is.LessThan(0.05f), "nor move her");
            Assert.That(_player.IsMoving, Is.False);
            _player.Stop();
            yield return null;
            _player.Steer(_side);
            yield return Until(() => !_player.IsQuickDucking, 0.5f);
            Assert.That(_player.IsQuickDucking, Is.False, "pressed again, it does");
            _player.Stop();

            // The dash.
            _player.Teleport(at);
            yield return null;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            Assert.That(_player.DashToward(at - _ahead * 6f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsQuickDucking, Is.False, "the dash ends it at once");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.DuckEnded).DuckEnd, Is.EqualTo(DuckEnd.Dashed));
        }

        [UnityTest]
        public IEnumerator NoDuckStartsWhileSheIsStunned_AndAStunOrAPullEndsOne()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            Assert.That(_player.Stun(0.3f, "test"), Is.True);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Busy), "stunned, no duck");
            Assert.That(_player.IsQuickDucking, Is.False);
            Assert.That(_player.DuckWaitLeft, Is.EqualTo(0f), "and no wait");
            yield return Until(() => !_player.IsStunned, 2f);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "free again, she ducks");
            Assert.That(_player.Pull(_player.Position - _ahead * 1f, 0.2f, "test"), Is.True);
            Assert.That(_player.IsQuickDucking, Is.False, "a pull ends it");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.DuckEnded).DuckEnd, Is.EqualTo(DuckEnd.Other));
            // Past the first stun's grace (a stun never chains), and back in reach (the pull moved her; a teleport also ends the wait).
            yield return Until(() => !_player.IsPulled && Time.time >= _player.HeldBy.StunFreeAt, 4f);
            _player.Teleport(Stand);
            yield return null;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            Assert.That(_player.Stun(0.2f, "test"), Is.True);
            Assert.That(_player.IsQuickDucking, Is.False, "a stun ends it too");
        }

        [UnityTest]
        public IEnumerator WhileDown_SheCannotShootOrUseASkill_AndWhatSheClickedOrPressedGoesOffAsSheRises()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            Vector3 aim = Stand + _side * 5f;

            // A click while down waits for her to rise.
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            _player.Shoot(aim);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None), "no shot while down");
            Assert.That(_player.HasPendingShot, Is.True, "the click waits");
            float startedAt = -1f;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (startedAt < 0f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_player.IsQuickDucking) Assert.That(_player.Action, Is.EqualTo(ActionKind.None), "none while she is down");
                else if (_player.Action == ActionKind.BasicShot) startedAt = Time.time;
            }
            Assert.That(startedAt, Is.GreaterThanOrEqualTo(_player.DuckRoseAt - 1e-4f), "it starts as she rises");
            Assert.That(startedAt - _player.DuckRoseAt, Is.LessThan(0.05f));
            yield return Until(() => _player.Action == ActionKind.None, 2f);

            // A skill pressed late in a duck waits as at the end of a lock, and starts as she rises.
            yield return Until(() => Time.time >= _player.DuckRoseAt + _feel.DuckWait, 2f);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            int slot = FirstReadySkill();
            yield return Until(() => Time.time >= _player.DuckUpAt - _feel.InputBuffer * 0.5f, 2f);
            Assert.That(_player.IsQuickDucking, Is.True);
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Buffered), "no skill while down: it waits");
            _player.ReleaseSkill(slot);
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(-1));
            yield return Until(() => !_player.IsQuickDucking, 2f);
            yield return null;
            Assert.That(_reports.Any(r => r.Kind == PlayerReportKind.SkillStarted && r.Slot == slot), Is.True, "it starts as she rises");
        }

        // ---------------- What it stops ----------------

        [UnityTest]
        public IEnumerator AnArrow_FromTheFarSide_IsStoppedAtTheProp_SheTakesNothing_TheFirstShotAfterIsTheReward_AndTheRecorderCountsIt()
        {
            var feedback = TimingFeedback.Ensure();
            var words = new List<string>();
            feedback.WordShown += w => words.Add(w.Text);
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            // A tap with nothing in reach first, for the recorder: beside it, but out of reach.
            _player.Teleport(Stand - _ahead * 2f);
            yield return null;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.NothingInReach));
            _player.Teleport(Stand);
            yield return null;

            EnemyShooter shooter = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _ahead * 9f);
            List<EnemyReport> strikes = Strikes(shooter);
            int health = _health.Current;
            shooter.Provoke();
            float speed = shooter.Tuning.ArrowSpeed;
            yield return Until(() => Eta(shooter, speed, shooter.Tuning.ArrowRadius) < 0.25f, 6f);
            Assert.That(Eta(shooter, speed, shooter.Tuning.ArrowRadius), Is.LessThan(0.25f), "an arrow on its way, about to arrive");
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            _player.Shoot(Stand + _side * 5f); // clicked while down: it goes off as she rises
            yield return Until(() => strikes.Count > 0, 2f);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.StoppedByCover), "the prop she ducked at stopped it");
            Assert.That(_player.IsQuickDucking, Is.True, "while she was down");
            Assert.That(_health.Current, Is.EqualTo(health), "she took nothing");
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(1));
            Assert.That(_player.DuckStoppedNow, Is.EqualTo(1));
            Assert.That(_reports.Any(r => r.Kind == PlayerReportKind.DuckStopped), Is.True);
            Assert.That(words, Does.Contain("DUCKED"), "the word over her");
            _fly.ListFlying(_shown);
            List<ThingsThatFly.Shown> left = _shown.Where(s => s.Owner == shooter).ToList();
            if (left.Count > 0)
                Assert.That(FlatDistance(left[0].Position, sarcophagus.transform.position), Is.LessThan(1.2f), "it stopped at the prop");
            _levels.ClearSpawned();

            yield return Until(() => !_player.IsQuickDucking, 2f);
            Assert.That(_player.DucksThatStopped, Is.EqualTo(1));
            yield return Until(() => _player.BasicShotsFired > 0, 1f);
            Assert.That(_player.PeekShots, Is.EqualTo(1), "her first shot as she rose is the timing's reward");

            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Count(e => e.Type == SessionEventTypes.Duck), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.DuckNothing), Is.EqualTo(1));
            SessionEvent end = events.Last(e => e.Type == SessionEventTypes.DuckEnd);
            Assert.That(end.Result, Is.EqualTo(SessionWords.Rose));
            Assert.That(end.ExtraValue("stopped"), Is.EqualTo("1"));
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.HasDuck, Is.True);
            Assert.That(stats.Session.Ducks, Is.EqualTo(1));
            Assert.That(stats.Session.DucksThatStopped, Is.EqualTo(1));
            Assert.That(stats.Session.DuckTapsWithNothing, Is.EqualTo(1));
            Assert.That(stats.Session.StoppedByDuck, Is.EqualTo(1));
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Quick ducks / that stopped something", summary);
            StringAssert.Contains("stopped by a quick duck", summary);
            Assert.That(summary, Does.Not.Contain("Time in cover"));
            _recorder = null;
        }

        [UnityTest]
        public IEnumerator AnArrow_FromHerSide_HitsHerThroughADuck()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            EnemyShooter shooter = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand - _ahead * 8f);
            List<EnemyReport> strikes = Strikes(shooter);
            int health = _health.Current;
            shooter.Provoke();
            float speed = shooter.Tuning.ArrowSpeed;
            yield return Until(() => Eta(shooter, speed, shooter.Tuning.ArrowRadius) < 0.25f, 6f);
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            yield return Until(() => strikes.Count > 0, 2f);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.Landed), "the prop is not between them");
            Assert.That(_health.Current, Is.LessThan(health));
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator AnArrow_ThatArrivesAfterSheRose_HitsHer()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            EnemyShooter shooter = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _ahead * 9f);
            List<EnemyReport> strikes = Strikes(shooter);
            int health = _health.Current;
            shooter.Provoke();
            // She ducks so early that the arrow, at its own speed, comes after the duck is over.
            float flight = 9f / shooter.Tuning.ArrowSpeed;
            float early = Mathf.Max(0f, _feel.DuckSeconds + 0.25f - flight);
            yield return Until(() => (shooter.State == EnemyState.Windup && shooter.WindupRemaining <= early) || shooter.Strikes > 0, 6f);
            float tapped = Time.time;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            yield return Until(() => strikes.Count > 0, 3f);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(Time.time, Is.GreaterThan(tapped + _feel.DuckSeconds), "it came after she rose");
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.Landed), "and hit her: a low prop does not stop it by itself");
            Assert.That(_health.Current, Is.LessThan(health));
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator AnOrb_HitsHerThroughADuck()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            EnemyCaster caster = Spawn<EnemyCaster>(EnemyKind.Caster, Stand + _ahead * 10f);
            List<EnemyReport> strikes = Strikes(caster);
            int health = _health.Current;
            caster.Provoke();
            float speed = caster.Tuning.OrbSpeed;
            yield return Until(() => Eta(caster, speed, caster.Tuning.OrbRadius) < 0.25f, 8f);
            Assert.That(Eta(caster, speed, caster.Tuning.OrbRadius), Is.LessThan(0.25f), "an orb about to arrive");
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            Assert.That(caster.SeesHer(), Is.True, "a duck does not hide her from the caster");
            yield return Until(() => strikes.Count > 0, 2f);
            Assert.That(strikes.Any(s => s.Outcome == StrikeOutcome.Landed), Is.True, "the orb passed over the prop and hit her");
            Assert.That(_health.Current, Is.LessThan(health));
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(0));
        }

        // ---------------- The rim light ----------------

        [UnityTest]
        public IEnumerator TheRimLight_IsOnTheNearestLowPropInReach_AndNowhereElse()
        {
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.8f));
            GameObject bones = Prop(PropModel.BonePile, FaceAt(PropModel.BonePile, _side, 0.4f));
            GameObject pillar = Prop(PropModel.Pillar, FaceAt(PropModel.Pillar, -_ahead, 0.3f));
            GameObject basin = Prop(PropModel.Basin, FaceAt(PropModel.Basin, -_side, _feel.DuckReach + 0.4f));
            yield return Synced();
            yield return null;
            Shelter nearest = bones.GetComponent<Shelter>();
            Assert.That(_player.DuckRimOn, Is.SameAs(nearest), "the nearest low prop in reach");
            Assert.That(_player.DuckRim.Shown, Is.True);
            Assert.That(FlatDistance(_player.DuckRim.Centre, bones.transform.position), Is.LessThan(0.01f), "round it");
            Assert.That(_player.DuckRim.Height, Is.GreaterThan(0.2f), "on the prop, not on the floor");
            Assert.That(_player.DuckRimOn, Is.Not.SameAs(pillar.GetComponent<Shelter>()), "never on a tall one, nearer as it is");
            Assert.That(_player.DuckRimOn, Is.Not.SameAs(basin.GetComponent<Shelter>()));

            Object.Destroy(bones);
            yield return null;
            yield return null;
            Assert.That(_player.DuckRimOn, Is.SameAs(sarcophagus.GetComponent<Shelter>()), "the next nearest");

            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Null, "down, a tap would not work");
            yield return Until(() => !_player.IsQuickDucking, 2f);
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Null, "nor while the key waits");
            yield return Until(() => Time.time >= _player.DuckRoseAt + _feel.DuckWait, 2f);
            yield return null;
            Assert.That(_player.DuckRimOn, Is.SameAs(sarcophagus.GetComponent<Shelter>()), "on again when it would");

            Object.Destroy(sarcophagus);
            yield return null;
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Null, "with only a tall prop and one out of reach: none");
            Prop(PropModel.Coffin, FaceAt(PropModel.Coffin, _ahead, 0.5f));
            yield return Synced();
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Not.Null);
            _feel.QuickDuck = false;
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Null, "the switch off: none");
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheKeyTakesCoverAsBefore()
        {
            _feel.QuickDuck = false;
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 1.2f));
            yield return Synced();
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Off));
            _player.HoldCover(true);
            yield return Until(() => _player.IsDucked, 1f);
            Assert.That(_player.IsInCover, Is.True, "held, she takes cover as before");
            Assert.That(_player.IsQuickDucking, Is.False);
            Assert.That(Along(_player.Position), Is.EqualTo(1.2f - _feel.CoverStandOff).Within(0.03f), "against its face");
            Assert.That(_player.DucksTaken, Is.EqualTo(0));
            Assert.That(_player.DuckRimOn, Is.Null);
            _player.HoldCover(false);
            Assert.That(_player.IsInCover, Is.False);
        }

        // ---------------- Helpers ----------------

        /// <summary>The slot of the first skill she has that is ready.</summary>
        int FirstReadySkill()
        {
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot) && _player.SkillCooldownRemaining(slot) <= 0f && _player.Skills[slot].Input == SkillInput.Normal
                    && _player.Skills[slot].RecastCount <= 1 && _player.Skills[slot].MoveDistance == 0f)
                    return slot;
            Assert.Fail("no plain skill ready");
            return -1;
        }

        /// <summary>
        /// Seconds until the first thing <paramref name="owner"/> loosed that still flies meets her, at its speed: its
        /// distance to her, less its size and hers. Infinity with none.
        /// </summary>
        float Eta(Enemy owner, float speed, float radius)
        {
            _fly.ListFlying(_shown);
            float best = float.PositiveInfinity;
            foreach (ThingsThatFly.Shown s in _shown)
            {
                if (s.Owner != owner || s.Ending || s.Passed) continue;
                float d = FlatDistance(s.Position, _player.Position) - radius - Placeholders.PlayerRadius;
                best = Mathf.Min(best, Mathf.Max(0f, d) / Mathf.Max(0.1f, speed));
            }
            return best;
        }

        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            _levels = _levels != null ? _levels : GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        static List<EnemyReport> Strikes(Enemy e)
        {
            var list = new List<EnemyReport>();
            e.Reported += r => { if (r.Kind == EnemyReportKind.Struck) list.Add(r); };
            return list;
        }

        /// <summary>Where a prop's centre goes for its near face to stand <paramref name="face"/> metres from her along <paramref name="way"/>.</summary>
        Vector3 FaceAt(PropModel model, Vector3 way, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            return Stand + way * (face + half);
        }

        float Along(Vector3 p) => Vector3.Dot(Flat(p - Stand), _ahead);

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>
        /// A prop as a level builds it: cover when it is tall enough (and low under the duck's sort for the three between
        /// the lines), something to duck behind unless it is slender. A box is turned to face her.
        /// </summary>
        GameObject Prop(PropModel model, Vector3 at)
        {
            Vector3 toHer = Flat(Stand - at);
            int rotation = toHer.sqrMagnitude > 1e-6f ? (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360 : 0;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        GameObject Wall(Vector3 centre, float length, float thickness)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(centre.x, 1.5f, centre.z), Quaternion.LookRotation(_ahead));
            wall.transform.localScale = new Vector3(length, 3f, thickness);
            wall.AddComponent<Cover>();
            wall.AddComponent<Shelter>();
            _made.Add(wall);
            return wall;
        }

        static IEnumerator Synced()
        {
            Physics.SyncTransforms();
            yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
