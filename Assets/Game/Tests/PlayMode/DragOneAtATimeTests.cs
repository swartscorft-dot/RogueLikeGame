using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// One guard drags at a time (work order 093; Kristian 2026-11-23: "Én drar om gangen"), through the ControlRoom scene: three
    /// guards made round her, each 6 m off (every one in its Drag's reach, all ready, all seeing her), for 30 s of game time, her
    /// health held up. She stands where she is put or pulled; the guards walk in and strike her in reach, so every 3.5 s (not
    /// while she is held) she is put back 6 m from where she stood, from one of two spots to the other, which puts the guards
    /// that came in round her back in their Drags' reach (standing still, one pull would have been all: the first run of this
    /// test had one in 30 s). Then: no two of her player_pulled events closer than
    /// <see cref="MeleeAttacks.DragOneAtATime"/>; the guards held back attack her with their Fist or Crush meanwhile; the
    /// recorder's drag_held_back lines and the summary's lines are there. And the hold clears on a new floor and on her death.
    ///
    /// The practice brute is switched off: it stays in the scene as the template every enemy here is made from. Made here, the
    /// guards take no turns (as the practice room's enemies), so only the hold keeps their Drags apart. Game time goes in fixed
    /// steps of a sixtieth (<see cref="Time.captureDeltaTime"/>), so the run is the same each time.
    /// </summary>
    public class DragOneAtATimeTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        const float Round = 6f; // m from her to each guard: inside the Drag's 4 to 9 m
        /// <summary>The other spot she is put on, 6 m south of the first: the guards in reach round one are 3.4 to 8.6 m from the other.</summary>
        static readonly Vector3 Back = Stand + Vector3.back * Round;
        const float PutBackEvery = 3.5f; // s

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;
        readonly List<Object> _made = new List<Object>();

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
            Assert.That(_player.MeleeYouRead, Is.True, "on by default");
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 1000000;
            _health.RestoreFull();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            MeleeEnemy.Drags.Clear();
            MeleeEnemy.Drags.Gap = MeleeAttacks.DragOneAtATime;
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            MeleeEnemy.Drags.Clear();
            MeleeEnemy.Drags.Gap = MeleeAttacks.DragOneAtATime;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        EnemyGuard Guard(Vector3 at, int n)
        {
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
            Enemy e = EnemyFactory.Make(EnemyKind.Guard, at, null, _practiceEnemy, "DragOneAtATime_Guard" + n);
            Assert.That(e, Is.Not.Null, "guard made");
            _made.Add(e.gameObject);
            return (EnemyGuard)e;
        }

        static string S(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        [UnityTest]
        public IEnumerator ThreeGuardsRoundHer_For30s_NoTwoPullsCloserThanTheValue_TheHeldBackAttackOtherwise_AndTheLogSaysSo()
        {
            _folder = Path.Combine(Path.GetTempPath(), "DragOneAtATimeTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            var guards = new List<EnemyGuard>();
            for (int i = 0; i < 3; i++)
                guards.Add(Guard(Stand + Quaternion.Euler(0f, -40f + 40f * i, 0f) * Vector3.forward * Round, i + 1));
            yield return null;
            foreach (EnemyGuard g in guards)
                Assume.That(g.SeesHer(), Is.True, g.name + " sees her where it stands: nothing of the room between");

            // What each began, and when: to see what the held-back ones did while the hold was on.
            var begun = new List<(float T, int Id, string Attack, bool Held)>();
            foreach (EnemyGuard g in guards)
            {
                EnemyGuard me = g;
                me.Reported += r =>
                {
                    if (r.Kind != EnemyReportKind.AttackStarted) return;
                    bool held = MeleeEnemy.Drags.Holder != 0 && MeleeEnemy.Drags.Holder != me.Id && !MeleeEnemy.Drags.MayBegin(me.Id, Time.time);
                    begun.Add((Time.time, me.Id, r.Attack, held));
                };
            }
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (EnemyGuard g in guards) g.Provoke();
                float t0 = Time.time, putAt = t0 + PutBackEvery;
                bool onStand = true;
                while (Time.time - t0 < 30f)
                {
                    if (_health.Current < _health.Max / 2) _health.RestoreFull(); // her health held up: none of it ends in her death
                    if (Time.time >= putAt && !_player.IsHeld)
                    {
                        onStand = !onStand;
                        _player.Teleport(onStand ? Stand : Back);
                        putAt = Time.time + PutBackEvery;
                    }
                    yield return null;
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            List<double> pulls = events.Where(e => e.Type == SessionEventTypes.PlayerPulled && e.Name == MeleeAttacks.Drag).Select(e => e.T).OrderBy(t => t).ToList();
            List<double> gaps = pulls.Zip(pulls.Skip(1), (a, b) => b - a).ToList();
            List<SessionEvent> heldBack = events.Where(e => e.Type == SessionEventTypes.DragHeldBack).ToList();
            var otherwise = begun.Where(b => b.Held && b.Attack != MeleeAttacks.Drag).ToList();
            string line = "[093] three guards for 30 s: " + pulls.Count + " pulls at " + string.Join(", ", pulls.Select(S))
                          + "; shortest gap " + (gaps.Count > 0 ? S(gaps.Min()) : "-") + " s; held back " + heldBack.Count
                          + " (" + string.Join(", ", guards.Select(g => g.Id + ": " + g.DragsHeldBack)) + "); while held back they began "
                          + string.Join(", ", otherwise.GroupBy(b => b.Attack).Select(g => g.Key + " " + g.Count()))
                          + "; all began " + string.Join(", ", begun.GroupBy(b => b.Attack).Select(g => g.Key + " " + g.Count()));
            Debug.Log(line);
            TestContext.WriteLine(line);

            Assert.That(pulls.Count, Is.GreaterThanOrEqualTo(2), "she was pulled more than once: " + line);
            Assert.That(gaps.Min(), Is.GreaterThanOrEqualTo(MeleeAttacks.DragOneAtATime), "no two pulls closer than the value: " + line);
            // No Drag began while another's held it: with no daze here, each ran its whole wind-up, so one began no sooner than
            // the last one's wind-up and the value after it.
            List<float> drags = begun.Where(b => b.Attack == MeleeAttacks.Drag).Select(b => b.T).ToList();
            EnemyAttackDefinition drag = MeleeAttacks.Find(guards[0].Tuning.Melee, MeleeAttacks.Drag);
            Assert.That(drags.Zip(drags.Skip(1), (a, b) => b - a).Min(), Is.GreaterThanOrEqualTo(drag.Windup + MeleeAttacks.DragOneAtATime - 0.02f),
                        "no Drag began while another's held it: " + string.Join(", ", drags.Select(t => S(t))));
            Assert.That(heldBack.Count, Is.GreaterThanOrEqualTo(2), "the two not first were held back at once, and noted: " + line);
            Assert.That(otherwise.Count, Is.GreaterThan(0), "the held back attacked her with something else meanwhile: " + line);
            Assert.That(otherwise.All(b => b.Attack == MeleeAttacks.Fist || b.Attack == MeleeAttacks.Crush), Is.True, "its Fist or its Crush: " + line);
            // Once per guard per hold: never two notes of one guard for one hold.
            Assert.That(heldBack.GroupBy(e => e.EnemyId).All(g => g.Count() <= drags.Count), Is.True, "not every frame: " + heldBack.Count);
            foreach (SessionEvent e in heldBack)
            {
                Assert.That(e.Enemy, Is.EqualTo("guard"));
                Assert.That(e.Name, Is.EqualTo(MeleeAttacks.Drag));
                Assert.That(e.ExtraValue("held_by"), Is.Not.Null.And.Not.EqualTo(e.EnemyId?.ToString(CultureInfo.InvariantCulture)));
                Assert.That(e.ExtraValue("seconds"), Is.EqualTo("4"));
            }
            Assert.That(guards.Sum(g => g.DragsHeldBack), Is.EqualTo(heldBack.Count), "each held back noted, once");

            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Stunned / pulled", summary);
            StringAssert.Contains("Drags landed / held back by the rule", summary);
            StringAssert.Contains("shortest between two Drags landing", summary);
            Debug.Log("[093] summary\n" + summary);
        }

        [UnityTest]
        public IEnumerator TheHold_ClearsOnANewFloor_AndOnHerDeath()
        {
            MeleeEnemy.Drags.Began(123456, Time.time, 1.2f);
            Assert.That(MeleeEnemy.Drags.MayBegin(1, Time.time), Is.False);
            GeneratedLevel level = _levels.Build(0x0930093UL, 3);
            Assert.That(level, Is.Not.Null);
            Assert.That(MeleeEnemy.Drags.Holder, Is.EqualTo(0), "a new floor: no hold");
            Assert.That(MeleeEnemy.Drags.MayBegin(1, Time.time), Is.True);
            yield return null;

            MeleeEnemy.Drags.Began(123456, Time.time, 1.2f);
            Assert.That(MeleeEnemy.Drags.MayBegin(1, Time.time), Is.False);
            _health.TakeHit(_health.Current + 1);
            Assert.That(_health.IsAlive, Is.False);
            Assert.That(MeleeEnemy.Drags.Holder, Is.EqualTo(0), "her death: no hold");
            yield return null;
        }
    }
}
