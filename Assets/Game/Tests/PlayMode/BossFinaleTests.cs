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
    /// The boss as a finale (work order 106, 2026-11-24; <see cref="BossValues.Finale"/>), in the ControlRoom scene, fought by a
    /// scripted player: the boss spawned through the practice room's intent (F8) and hurt by hand, she moved with
    /// <see cref="PlayerCharacter.Teleport"/>, as <see cref="EnemyBossTests"/> do. The fight through the break (the roar: it
    /// stands, it cannot be staggered, her damage counts, once; the bar's halves; the recorder's line); the Ground Slam lands where
    /// she stood and hurts her there, and misses when she has left; the throne room's lights answer the break on floor 5; Knight's
    /// Resolve keeps her at 1 health once and dazes the enemies near her; and with the switch off, today's fight. Every number is
    /// read from the values. The recorder writes under a temporary folder of the test only.
    /// </summary>
    public class BossFinaleTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;
        bool _finaleBefore;
        readonly List<PlaceholderSoundRequest> _sounds = new List<PlaceholderSoundRequest>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _finaleBefore = BossValues.Finale;
            BossValues.Finale = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            PlaceholderSounds.Requested += Heard;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false; // the boss's cleave sends no wave here: these are of the finale
            PlaceholderSounds.Ensure();
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
            _sounds.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Heard;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null) _player.Modifiers = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            BossValues.Finale = _finaleBefore;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Heard(PlaceholderSoundRequest r) => _sounds.Add(r);

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

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        void Record()
        {
            _folder = Path.Combine(Path.GetTempPath(), "BossFinaleTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
        }

        EnemyBoss SpawnBoss()
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Boss);
            Assert.That(made.Count, Is.EqualTo(1), "one boss");
            var boss = (EnemyBoss)made[0];
            boss.Tuning.CallsAt = new float[0]; // no packs in the way here
            return boss;
        }

        /// <summary>A blow that takes it to just under half its health: the break's blow.</summary>
        static void ToJustUnderHalf(EnemyBoss boss)
        {
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(boss.Body.CurrentHealth - Mathf.FloorToInt(max * boss.Tuning.AngerBelow) + 1);
        }

        /// <summary>Waits for the next windup of an attack of that name; the time it began, or -1.</summary>
        static IEnumerator NextWindup(EnemyBoss boss, string name, Action<float> began, float timeout = 8f)
        {
            float at = -1f;
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (boss.State == EnemyState.Windup && boss.Attack != null && boss.Attack.Name == name)
                {
                    at = Time.time;
                    break;
                }
                yield return null;
            }
            began(at);
        }

        // ---------------- The break ----------------

        [UnityTest]
        public IEnumerator TheBreak_AtHalf_ItDropsItsAttack_StandsAndRoars_CannotBeStaggered_DamageCounts_Once_ThenPhaseTwo()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return null;
            Vector3 at = boss.transform.position;
            _player.Teleport(at + Vector3.back * 3f);
            var bar = boss.GetComponent<BossHealthBar>();
            Assert.That(bar.ShowsHalves, Is.True, "its bar shows the two halves");
            Assert.That(bar.HalfAt, Is.EqualTo(0.5f));
            Rect mark = bar.HalfMark(new Rect(100f, 30f, 560f, 16f));
            Assert.That(mark.center.x, Is.EqualTo(100f + 280f).Within(0.01f), "at its middle");
            Assert.That(mark.height, Is.GreaterThan(16f), "across the bar");
            boss.Body.TakeHit(1);
            float began = -1f;
            yield return NextWindup(boss, BossValues.Cleave, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "a cleave being wound up");
            int strikes = boss.Strikes;
            _health.RestoreFull();
            int hp = _health.Current;

            ToJustUnderHalf(boss);
            yield return null;
            float brokeAt = Time.time;
            Assert.That(boss.HasBroken, Is.True, "the break");
            Assert.That(boss.IsAngry, Is.True, "angry, as before");
            Assert.That(boss.IsRoaring, Is.True, "it roars");
            Assert.That(boss.RoarEndsAt - boss.BrokeAt, Is.EqualTo(boss.Tuning.Phases.RoarSeconds).Within(0.05f));
            Assert.That(boss.State, Is.Not.EqualTo(EnemyState.Windup), "the cleave it was winding up is dropped");
            Assert.That(boss.IsTelegraphing, Is.False);

            // It cannot be staggered in it: a hit with more than its whole bar of stagger dazes nothing; the damage counts.
            int before = boss.Body.CurrentHealth;
            float barBefore = boss.Stagger.Current;
            boss.Body.TakeHit(50, boss.Tuning.Stagger.Max * 2f);
            yield return null;
            Assert.That(boss.IsDazed, Is.False, "not staggered while it roars");
            Assert.That(boss.Body.CurrentHealth, Is.EqualTo(before - 50), "her damage counts");
            Assert.That(boss.Stagger, Is.Not.Null, "its bar is still shown");
            Assert.That(boss.Stagger.Current, Is.EqualTo(barBefore), "and nothing came off it");

            // It stands: nothing starts, nothing strikes, it goes nowhere, for the roar.
            Vector3 stood = boss.transform.position;
            bool started = false;
            while (boss.IsRoaring && Time.time - brokeAt < 3f)
            {
                if (boss.State == EnemyState.Windup) started = true;
                yield return null;
            }
            float roared = Time.time - brokeAt;
            Assert.That(boss.IsRoaring, Is.False, "the roar ends");
            Assert.That(roared, Is.EqualTo(boss.Tuning.Phases.RoarSeconds).Within(0.15f), "about 1.5 s");
            Assert.That(started, Is.False, "it started no attack in it");
            Assert.That(boss.Strikes, Is.EqualTo(strikes), "and struck nothing");
            Assert.That(_health.Current, Is.EqualTo(hp), "she was not hurt in it");
            Assert.That(Flat(boss.transform.position, stood), Is.LessThan(0.05f), "it stood");
            Assert.That(_sounds.Count(s => s.Sound == PlaceholderSound.BossRoar), Is.EqualTo(1), "its roar was heard, once");

            // Phase two: it can be staggered again, and it does not break again.
            boss.Body.TakeHit(1, boss.Tuning.Stagger.Max);
            yield return null;
            Assert.That(boss.IsDazed, Is.True, "after the roar its bar dazes it again");
            boss.Body.TakeHit(boss.Body.MaxHealth / 10);
            yield return null;
            Assert.That(boss.IsRoaring, Is.False, "once");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.BossAnger), Is.EqualTo(1));

            // Its death ends the fight: the recorder's line tells both phases.
            yield return For(0.5f);
            boss.Body.TakeHit(10000000);
            yield return null;
            yield return null;
            Assert.That(boss.IsDead, Is.True);
            SessionEvent fight = _recorder.Events.Single(e => e.Type == BossFightEvents.Fight);
            Assert.That(fight.ExtraValue("broke"), Is.EqualTo("1"));
            Assert.That(fight.ExtraValue("ended"), Is.EqualTo(BossFightEvents.Died));
            Assert.That(float.Parse(fight.ExtraValue("two"), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThan(1.5f), "phase two from the break to its death");
            Assert.That(float.Parse(fight.ExtraValue("one"), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(0f), "phase one: here only the frames from its waking to the blow");
            _levels.ClearSpawned();
        }

        // ---------------- The Ground Slam ----------------

        /// <summary>The boss broken and done roaring, its next choice a slam, she <paramref name="away"/> m from it toward where it faces her.</summary>
        IEnumerator BrokenWithASlamDue(EnemyBoss boss, float away, Action<Vector3> stood)
        {
            boss.Tuning.Phases.SlamEvery = 1; // every choice of phase two a slam: the test need not wait for the fourth
            yield return null;
            Vector3 from = boss.transform.position;
            Vector3 dir = (new Vector3(Stand.x, 0f, Stand.z) - new Vector3(from.x, 0f, from.z)).normalized;
            Vector3 her = from + dir * away;
            _player.Teleport(her);
            boss.Tuning.Enemy.MoveSpeed = 0f;
            ToJustUnderHalf(boss);
            yield return null;
            Assert.That(boss.IsRoaring, Is.True);
            yield return Until(() => !boss.IsRoaring, 3f);
            stood(_player.Position);
        }

        [UnityTest]
        public IEnumerator TheSlam_CrouchesLeapsAndLandsWhereSheStood_ItsRingShowsThere_SheStays_AndIsHit()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            Vector3 her = Vector3.zero;
            yield return BrokenWithASlamDue(boss, 8f, p => her = p);
            Vector3 from = boss.transform.position;
            float began = -1f;
            yield return NextWindup(boss, BossFinale.Slam, t => began = t, 4f);
            Assert.That(began, Is.GreaterThan(0f), "a Ground Slam at her 8 m away");
            EnemyAttackDefinition slam = boss.Attack;
            Assert.That(slam.Counterable, Is.False);
            Assert.That(boss.TelegraphIsCounterable, Is.False, "red");
            Assert.That(boss.IsSlamming, Is.True);
            Assert.That(Flat(boss.SlamLanding, her), Is.LessThan(0.3f), "it will land where she stood");
            Assert.That(boss.IsTelegraphing, Is.True, "its ring shows");
            yield return null;
            LineRenderer ring = boss.Telegraph;
            var points = new Vector3[ring.positionCount];
            ring.GetPositions(points);
            Assert.That(points.Length, Is.GreaterThan(8));
            Vector3 middle = points.Aggregate(Vector3.zero, (a, p) => a + p) / points.Length;
            Assert.That(Flat(middle, boss.SlamLanding), Is.LessThan(0.3f), "round the landing, not round where it stands");
            Assert.That(points.Average(p => Flat(p, boss.SlamLanding)), Is.EqualTo(slam.Radius).Within(0.2f), "the slam's 4 m");

            _health.RestoreFull();
            int hp = _health.Current;
            int strikes = boss.Strikes;
            bool crouchedInPlace = true, flew = false;
            float highest = 0f, lift = boss.Look != null ? boss.Look.Measured.Lift : 0f;
            while (boss.Strikes == strikes && Time.time - began < 3f)
            {
                float t = Time.time - began;
                if (t < boss.Tuning.Phases.SlamCrouch * 0.8f && Flat(boss.transform.position, from) > 0.05f) crouchedInPlace = false;
                if (boss.IsInTheAir) flew = true;
                if (boss.Look != null) highest = Mathf.Max(highest, boss.Look.Pivot.localPosition.y - lift);
                yield return null;
            }
            float struck = Time.time - began;
            Assert.That(boss.Strikes, Is.GreaterThan(strikes), "it struck");
            Assert.That(struck, Is.EqualTo(slam.Windup).Within(0.1f), "about 1 s from the crouch to the landing");
            Assert.That(crouchedInPlace, Is.True, "it crouched where it stood first");
            Assert.That(flew, Is.True, "then it was in the air");
            if (boss.Look != null) Assert.That(highest, Is.GreaterThan(boss.Tuning.Phases.SlamHeight * 0.5f), "its body drawn up in the arc");
            Assert.That(Flat(boss.transform.position, her), Is.LessThan(0.3f), "landed where she stood");
            Assert.That(NavMesh.SamplePosition(boss.transform.position, out NavMeshHit _, 0.3f, NavMesh.AllAreas), Is.True, "on the NavMesh");
            Assert.That(hp - _health.Current, Is.EqualTo(slam.Damage), "she stayed: the ring hit her");
            Assert.That(boss.SlamsLanded, Is.EqualTo(1));
            Assert.That(boss.IsSlamming, Is.False);
            Assert.That(boss.State, Is.EqualTo(EnemyState.Recover), "its recovery, where it landed: her opening");
            if (boss.Look != null) Assert.That(boss.Look.Pivot.localPosition.y, Is.EqualTo(lift).Within(1e-4f), "its body on its feet again");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == BossFinale.Slam && e.Result == SessionWords.Landed), Is.EqualTo(1));
            _levels.ClearSpawned();
            yield return null;
            SessionEvent fight = _recorder.Events.Single(e => e.Type == BossFightEvents.Fight);
            Assert.That(fight.ExtraValue("slams"), Is.EqualTo("1"));
            Assert.That(fight.ExtraValue("landed"), Is.EqualTo("1"));
            Assert.That(fight.ExtraValue("ended"), Is.EqualTo(BossFightEvents.Removed), "taken away awake");
        }

        [UnityTest]
        public IEnumerator TheSlam_MissesWhenSheHasLeftTheRing_ItStillLandsWhereSheStood()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            Vector3 her = Vector3.zero;
            yield return BrokenWithASlamDue(boss, 8f, p => her = p);
            Vector3 from = boss.transform.position;
            float began = -1f;
            yield return NextWindup(boss, BossFinale.Slam, t => began = t, 4f);
            Assert.That(began, Is.GreaterThan(0f), "a Ground Slam at her 8 m away");
            EnemyAttackDefinition slam = boss.Attack;
            // She walks out sideways in the crouch: past the ring's edge and her own.
            Vector3 dir = (new Vector3(her.x, 0f, her.z) - new Vector3(from.x, 0f, from.z)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            _player.Teleport(her + side * (slam.Radius + Placeholders.PlayerRadius + 1f));
            _health.RestoreFull();
            int hp = _health.Current;
            int strikes = boss.Strikes;
            yield return Until(() => boss.Strikes > strikes, 3f);
            Assert.That(boss.Strikes, Is.GreaterThan(strikes), "it struck");
            Assert.That(_health.Current, Is.EqualTo(hp), "out of the ring: missed");
            Assert.That(Flat(boss.transform.position, her), Is.LessThan(0.3f), "it landed where she stood, not where she went");
            Assert.That(boss.SlamsAvoided, Is.EqualTo(1));
            Assert.That(boss.SlamsLanded, Is.EqualTo(0));
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == BossFinale.Slam && e.Result == SessionWords.OutOfArea), Is.EqualTo(1));
            _levels.ClearSpawned();
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_TodaysFight_AngryAtHalf_NoRoar_NoSlam_NoHalves_NoLine()
        {
            BossValues.Finale = false;
            Record();
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Phases.SlamEvery = 1; // were it on, every choice would be a slam
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return null;
            Vector3 at = boss.transform.position;
            _player.Teleport(at + Vector3.back * 3f);
            Assert.That(boss.GetComponent<BossHealthBar>().ShowsHalves, Is.False, "its bar as before");
            float calm = boss.Values.Attacks.First(a => a.Name == BossValues.Cleave).Windup;
            ToJustUnderHalf(boss);
            yield return null;
            Assert.That(boss.IsAngry, Is.True, "angry at half, as before");
            Assert.That(boss.Values.Attacks.First(a => a.Name == BossValues.Cleave).Windup, Is.EqualTo(calm * boss.Tuning.AngerWindup).Within(1e-5f));
            Assert.That(boss.HasBroken, Is.False, "no break");
            Assert.That(boss.IsRoaring, Is.False, "no roar");
            boss.Body.TakeHit(1, boss.Tuning.Stagger.Max);
            yield return null;
            Assert.That(boss.IsDazed, Is.True, "staggered at once, as before");
            yield return Until(() => !boss.IsDazed, boss.Tuning.Stagger.DazeSeconds + 1f);
            var names = new List<string>();
            float until = Time.time + 20f, realUntil = Time.realtimeSinceStartup + 60f;
            EnemyAttackDefinition last = null;
            _health.Untouchable = true; // four of its blows would kill her, and it would go home
            while (names.Count < 4 && Time.time < until && Time.realtimeSinceStartup < realUntil)
            {
                if (boss.State == EnemyState.Windup && boss.Attack != last) names.Add(boss.Attack.Name);
                last = boss.State == EnemyState.Windup ? boss.Attack : null;
                yield return null;
            }
            _health.Untouchable = false;
            Assert.That(names.Count, Is.EqualTo(4), "four attacks: " + string.Join(", ", names));
            Assert.That(names, Has.None.EqualTo(BossFinale.Slam), "no slam");
            Assert.That(names.All(n => n == BossValues.Cleave || n == BossValues.Spin), Is.True, string.Join(", ", names));
            Assert.That(boss.LampsAnswered, Is.EqualTo(0));
            Assert.That(_sounds.Count(s => s.Sound == PlaceholderSound.BossRoar), Is.EqualTo(0), "no roar heard");
            boss.Body.TakeHit(10000000);
            yield return null;
            yield return null;
            Assert.That(_recorder.Events.Count(e => e.Type == BossFightEvents.Fight), Is.EqualTo(0), "no line of its fight");
            Assert.That(KnightsResolve.WithPiece(new MetaValues()).Pieces.Length, Is.EqualTo(new MetaValues().Pieces.Length), "the rack as before");
            _levels.ClearSpawned();
        }

        // ---------------- The room answers ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator InFloor5_TheThroneRoomsLights_TakeTheBreaksColour_TheOtherRoomsKeepTheirs()
        {
            _practiceEnemy.enabled = true;
            GeneratedLevel level = _levels.Build(42UL, 5);
            yield return For(0.5f); // the floor's light comes up as it is shown
            var boss = level.Boss as EnemyBoss;
            Assert.That(boss, Is.Not.Null, "the act boss");
            boss.Tuning.CallsAt = new float[0];
            LevelRoom room = level.Layout.ExitRoom;
            boss.Body.TakeHit(1); // its room's fight wakes
            yield return Until(() => boss.IsAwake, 2f);
            yield return null;
            List<Light> own = BossRoomLight.Of(level, room.Order);
            Assert.That(own.Count, Is.GreaterThan(0), "the throne room has lights");
            var others = new Dictionary<Light, Color>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (!own.Contains(l)) others[l] = l.color;
            int lightsBefore = others.Count + own.Count;
            float[] ranges = own.Select(l => l.range).ToArray();
            ToJustUnderHalf(boss);
            yield return null;
            Assert.That(boss.HasBroken, Is.True);
            Assert.That(boss.LampsAnswered, Is.EqualTo(own.Count));
            BossFinaleValues f = boss.Tuning.Phases;
            var want = new Color(f.LampColour.R, f.LampColour.G, f.LampColour.B, 1f);
            foreach (Light l in own) Assert.That(l.color, Is.EqualTo(want), l.name);
            Assert.That(own.Select(l => l.range).ToArray(), Is.EqualTo(ranges), "their reach their own");
            foreach (KeyValuePair<Light, Color> p in others)
                if (p.Key != null) Assert.That(p.Key.color, Is.EqualTo(p.Value), p.Key.name + " keeps its colour");
            yield return For(2f);
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length, Is.LessThanOrEqualTo(lightsBefore), "no light made");
            foreach (Light l in own) Assert.That(l.color, Is.EqualTo(want), "for the rest of the fight: " + l.name);
            Debug.Log("[106] floor 5 seed 42: " + own.Count + " lights of the throne room answered the break: " + string.Join(", ", own.Select(l => l.name)));
            _levels.BackToPracticeRoom();
            yield return null;
        }

        // ---------------- Knight's Resolve ----------------

        /// <summary>Stands an enemy at a point of the NavMesh near this one, at once.</summary>
        static void Put(Enemy e, Vector3 near)
        {
            Vector3 p = NavMesh.SamplePosition(near, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : near;
            var agent = e.GetComponent<NavMeshAgent>();
            if (agent.enabled) agent.Warp(p);
            e.transform.position = p;
        }

        [UnityTest]
        public IEnumerator KnightsResolve_ABlowThatWouldKillHer_LeavesHerAt1_DazesTheEnemiesWithin4m_OnceARun()
        {
            List<Enemy> near = _levels.SpawnPractice(EnemyKind.Brute), far = _levels.SpawnPractice(EnemyKind.Brute);
            yield return null;
            Enemy a = near[0], b = far[0];
            a.enabled = b.enabled = true; // copies of the practice brute, which these tests switch off
            var run = new RunModifiers();
            MetaValues v = KnightsResolve.WithPiece(new MetaValues());
            var s = new MetaState();
            s.Feats.Add(MetaFeats.Id(MetaFeat.ActBoss));
            MetaRack.Tidy(s, v);
            Assert.That(MetaRack.Equip(s, v, KnightsResolve.Id), Is.EqualTo(MetaEquip.Equipped));
            MetaRack.Apply(s, v, run.Meta);
            _player.Modifiers = run;
            _health.RestoreFull();
            yield return null;
            Vector3 her = _player.Position; // the two put near and far in the frame of the blow: neither has time to strike
            Put(a, her + Vector3.right * 2.5f);
            Put(b, her + Vector3.left * 9f);
            Assert.That(Flat(a.transform.position, her), Is.LessThan(KnightsResolve.Reach - 0.5f), "one near her");
            Assert.That(Flat(b.transform.position, her), Is.GreaterThan(KnightsResolve.Reach + 2f), "one far");
            Assert.That(Enemy.All.Contains(a) && Enemy.All.Contains(b), Is.True, "both in the fight");
            int full = _health.Current;
            Assert.That(_health.TakeHit(full + 30), Is.EqualTo(full - 1), "the killing blow takes all but 1");
            Assert.That(_health.Current, Is.EqualTo(1));
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(a.IsDazed, Is.True, "the brute 2.5 m from her is staggered");
            Assert.That(b.IsDazed, Is.False, "the one 9 m away is not");
            Assert.That(run.Meta.ResolvesLeft, Is.EqualTo(0));
            yield return null;
            Assert.That(_health.TakeHit(5), Is.EqualTo(1), "once a run: the next blow kills her");
            Assert.That(_health.IsAlive, Is.False);
            _levels.ClearSpawned();
        }
    }
}
