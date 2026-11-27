using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Work order 147's tests share this: the control room loaded with nobody at the keys, the trial fight asked for
    /// (<see cref="BossTrialSettings.ForTests"/>), a fixed seed for its draws, time in fixed steps of <see cref="Step"/> (so a measure does
    /// not depend on the machine), the trial boss in the practice room or in floor 5's throne room, and a stand-in for her that is
    /// steered through her intents.
    /// </summary>
    public abstract class BossTrialHarness
    {
        /// <summary>s of game time a frame: fixed.</summary>
        protected const float Step = 1f / 60f;
        /// <summary>Three floor 5s of work order 146's measure (both throne templates among them), and his.</summary>
        protected static readonly ulong[] Floors = { 0x13C6EF362UL, 0x3B54CDA26UL, 0x9E3779B1UL };
        protected const string HisFloor = "XFBMUJHT";

        protected PlayerCharacter _player;
        protected PlayerHealth _health;
        protected EnemyBrute _practiceEnemy;
        protected GeneratedLevels _levels;
        protected readonly List<EnemyReport> _reports = new List<EnemyReport>();
        bool _entranceBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _entranceBefore = BossEntranceValues.On;
            BossEntranceValues.On = false; // the fight wakes at its point
            BossTrialSettings.ForTests = true;
            BossTrialTesting.Seed = 147;
            BossTrialTesting.HoldChoices = false;
            BossTrialValues.AllOn();
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            ThingsThatFly.Ensure().Values.On = false; // the old boss's wave is not the trial's: nothing else here flies
            _reports.Clear();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            BossTrialSettings.ForTests = null;
            BossTrialTesting.Seed = null;
            BossTrialTesting.HoldChoices = false;
            BossTrialValues.AllOn();
            BossEntranceValues.On = _entranceBefore;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Time ----------------

        /// <summary>Game time in fixed steps for <paramref name="seconds"/>, doing <paramref name="each"/> before each frame.</summary>
        protected IEnumerator Run(float seconds, Action each = null, Func<bool> until = null)
        {
            Time.captureDeltaTime = Step;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                each?.Invoke();
                yield return null;
                if (until != null && until()) break;
            }
        }

        // ---------------- The boss ----------------

        /// <summary>The practice room's trial boss, awake, at <paramref name="distance"/> from her along the spawn's clear way, choosing nothing itself.</summary>
        protected IEnumerator PracticeBoss(Action<EnemyBoss> got, float distance = 6f, bool hold = true)
        {
            BossTrialTesting.HoldChoices = hold;
            _practiceEnemy.enabled = false;
            Vector3 her = _player.Position;
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Boss);
            Assert.That(made.Count, Is.EqualTo(1), "one boss");
            var boss = (EnemyBoss)made[0];
            yield return null;
            Assert.That(boss.InTrial, Is.True, "the trial fight");
            Vector3 dir = Flat(boss.transform.position - her).normalized;
            if (dir.sqrMagnitude < 0.5f) dir = Vector3.forward;
            Place(boss, her + dir * distance);
            boss.Reported += r => _reports.Add(r);
            boss.Provoke();
            yield return Run(0.1f);
            Assert.That(boss.IsAwake, Is.True, "awake");
            got(boss);
        }

        /// <summary>The boss put at a point (on the NavMesh), facing her.</summary>
        protected void Place(EnemyBoss boss, Vector3 at)
        {
            Assert.That(NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas), Is.True, "walkable ground for the boss at " + at);
            boss.GetComponent<NavMeshAgent>().Warp(hit.position);
            boss.transform.rotation = Quaternion.LookRotation(Flat(_player.Position - hit.position).normalized + new Vector3(0f, 0f, 1e-4f), Vector3.up);
        }

        /// <summary>A throne room of floor 5, built alone, the boss alone awake in its fight (as work order 146's tests).</summary>
        protected IEnumerator ThroneRoom(ulong seed, Action<GeneratedLevel, EnemyBoss> got, bool hold = true)
        {
            BossTrialTesting.HoldChoices = hold;
            _practiceEnemy.enabled = true; // the floor's enemies are copies of it
            GeneratedLevel level = _levels.Build(seed, 5);
            _practiceEnemy.enabled = false;
            yield return null;
            yield return null;
            var boss = level.Boss as EnemyBoss;
            Assert.That(boss, Is.Not.Null, "floor 5's act boss");
            Assert.That(boss.InTrial, Is.True, "the trial fight");
            LevelRoom room = level.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            RoomFrame frame = RoomUseRules.FrameOf(level.Layout, room, door);
            foreach (Enemy e in new List<Enemy>(Enemy.All))
                if (e != boss) e.gameObject.SetActive(false);
            boss.Reported += r => _reports.Add(r);
            _health.Untouchable = true;
            _player.Teleport(level.ToWorld(frame.FromFrame(1.0, 0.0)));
            for (int i = 0; i < 30 && _levels.CurrentRoom != room; i++) yield return null;
            _player.Teleport(level.ToWorld(frame.FromFrame(6.0, 0.0)));
            RoomFight fight = _levels.FightOf(room);
            for (int i = 0; i < 120 && !(fight != null && fight.On && boss.IsAwake); i++) yield return null;
            Assert.That(boss.IsAwake, Is.True, "the boss awake in its fight");
            _health.Untouchable = false;
            got(level, boss);
        }

        // ---------------- Her ----------------

        /// <summary>She stands where she is (her move keys let go).</summary>
        protected void Stand() => _player.Stop();

        /// <summary>She walks this way this frame.</summary>
        protected void Walk(Vector3 dir) => _player.Steer(Flat(dir));

        /// <summary>Her hits landed by the boss of this attack's name since the reports were cleared.</summary>
        protected int Landed(string attack)
        {
            int n = 0;
            foreach (EnemyReport r in _reports)
                if (r.Kind == EnemyReportKind.Struck && r.Outcome == StrikeOutcome.Landed && r.Attack == attack) n++;
            return n;
        }

        protected int Struck(string attack, StrikeOutcome outcome)
        {
            int n = 0;
            foreach (EnemyReport r in _reports)
                if (r.Kind == EnemyReportKind.Struck && r.Outcome == outcome && r.Attack == attack) n++;
            return n;
        }

        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        protected static Vector3 Side(Vector3 dir) => Vector3.Cross(Vector3.up, Flat(dir).normalized);

        /// <summary>A pier near the room's middle: the nearest point of a tall block (not the room's own wall) seen from the middle, and the way to it.</summary>
        protected static bool Pier(EnemyBoss boss, Vector3 from, out Vector3 face, out Vector3 dir, float within = 14f)
        {
            face = dir = default;
            float best = float.PositiveInfinity;
            for (int k = 0; k < 144; k++)
            {
                float a = k * Mathf.PI * 2f / 144f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float clear = Cover.ClearDistance(Flat(from), d, within, Cover.RayHeight);
                if (clear >= within - 1e-3f) continue;
                // A pier, not a wall: open ground beyond it on the same line (within a few metres past its far side).
                Vector3 beyond = Flat(from) + d * (clear + 5f);
                if (!NavMesh.SamplePosition(beyond, out NavMeshHit hit, 1f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > 0.5f) continue;
                if (clear < best)
                {
                    best = clear;
                    face = Flat(from) + d * clear;
                    dir = d;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        /// <summary>The walkable floor nearest a point, within <paramref name="within"/> m, never a wall's top.</summary>
        protected static bool OnFloor(Vector3 p, float within, out Vector3 at)
        {
            bool found = NavMesh.SamplePosition(new Vector3(p.x, 0f, p.z), out NavMeshHit hit, within, NavMesh.AllAreas) && Mathf.Abs(hit.position.y) < 0.5f;
            at = found ? hit.position : p;
            return found;
        }
    }
}
