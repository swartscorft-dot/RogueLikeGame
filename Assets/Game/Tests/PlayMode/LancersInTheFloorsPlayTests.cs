using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// The lancer in real floors (work order 118, the generator's version 22; Kristian, 2026-11-24, said yes to "Lansereren (ny
    /// fiende)": "Etasje 4: stormer i rett linje langveisfra, varslet på kroppen, og blir ør når den treffer en vegg eller
    /// rekvisitt"), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>, with the defaults: a floor-4 combat
    /// room that holds a lancer is built, the lancer stands on the NavMesh on no prop with a floor's health; she past the room's line,
    /// its fight wakes and its doors shut; the lancer fights in it; cleared, the fight ends and the doors open. And a room with
    /// shooters and casters holding their posts (work orders 098, 101) beside a lancer: its fight wakes and ends as well. She cannot be
    /// hurt. Nobody has played it: these are numbers. Writes what it found ("LANCERFLOORPLAY").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames or game seconds, never a frame's length.
    /// </summary>
    public class LancersInTheFloorsPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float FightSeconds = 12f; // game seconds at most the room's fight runs before she clears it: time for the lancer to walk to its distance, wind up and charge (the wait ends at its first charge's end)

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;

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
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues { Waves = false }; // work order 136 (changed deliberately): its rooms wake whole, as the rule it measures was built on
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null)
            {
                _player.Steer(Vector3.zero);
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_health != null) _health.Untouchable = false;
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        /// <summary>The first seed of the floor whose combat room (not its first, not the elite room) holds a lancer and, if asked, a shooter and a caster too; order -1 when none of the first 600 seeds has one.</summary>
        static (ulong seed, int order) FindRoom(LevelValues v, int floor, bool withPosts)
        {
            for (ulong s = 1; s < 600; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, floor, v);
                foreach (LevelRoom r in l.Rooms)
                {
                    if (r.Kind != RoomKind.Combat || r.Elite || LevelLancers.Count(r.EnemyGroups) == 0) continue;
                    if (withPosts && !(r.EnemyGroups.Any(g => g.Kind == EnemyKind.Shooter) && r.EnemyGroups.Any(g => g.Kind == EnemyKind.Caster))) continue;
                    return (s, r.Order);
                }
            }
            return (0UL, -1);
        }

        /// <summary>Builds the floor, checks the room's lancers stand, wakes the room with her past its line, lets the fight run, clears it and checks it ends.</summary>
        IEnumerator WakeAndEnd(ulong seed, int floor, int order, List<string> lines, Action<GeneratedLevel, LevelRoom, List<Enemy>> awake = null)
        {
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            LevelRoom room = layout.Rooms[order];
            string where = layout.SeedText + " floor " + floor + " room " + room.Letter + order;
            List<Enemy> own = level.EnemiesByRoom[order].Where(e => e != null).ToList();
            Assert.That(own.Count, Is.EqualTo(room.EnemyCount), where + ": every enemy stands");
            List<EnemyLancer> lancers = own.OfType<EnemyLancer>().ToList();
            Assert.That(lancers.Count, Is.EqualTo(LevelLancers.Count(room.EnemyGroups)), where + ": its lancers stand");
            float body = new LancerValues().BodyRadius;
            foreach (EnemyLancer l in lancers)
            {
                Assert.That(l.Body.MaxHealth, Is.EqualTo(_levels.Values.LancerHealth), where + ": a floor's health");
                Assert.That(l.DamageScale, Is.EqualTo(_levels.Values.DamageScaleOf(floor)).Within(1e-5f), where + ": the floor's damage scale");
                LevelPoint at = level.ToLevel(l.transform.position);
                Assert.That(room.Props.All(p => p.Footprint.DistanceTo(at) >= body), Is.True, where + ": the lancer sits in no prop");
                Assert.That(NavMesh.SamplePosition(l.transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas), Is.True, where + ": on the NavMesh");
                Assert.That(Physics.OverlapSphere(l.transform.position + Vector3.up * 1f, body * 0.5f).Where(c => !c.isTrigger && c.GetComponentInParent<Enemy>() == null).ToList(),
                            Is.Empty, where + ": nothing solid in its body");
            }
            RoomFight fight = _levels.FightOf(room);
            GeneratedLevels.RoomDoors doors = _levels.DoorsOf(room);
            Assert.That(fight, Is.Not.Null, where + ": it holds its fight");
            Assert.That(fight.On, Is.False, where + ": asleep while she is away");

            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            Vector3 inside = level.ToWorld(RoomDoorRules.PullInPlace(layout, room, door, _levels.Values.Fights.PointIn + 1.5, 0d));
            _player.Teleport(inside);
            yield return Until(() => fight.On, 60);
            Assert.That(fight.On, Is.True, where + ": past the line, the room wakes");
            if (doors != null)
            {
                yield return Until(() => doors.Shut, 30);
                Assert.That(doors.Shut, Is.True, where + ": its doors shut");
            }
            yield return Until(() => own.All(e => e == null || e.IsDead || e.IsAwake), 60);
            Assert.That(own.All(e => e.IsAwake), where + ": every enemy of the room awake");
            awake?.Invoke(level, room, own);
            Time.timeScale = 4f;
            bool wound = false;
            float end = Time.time + FightSeconds, began = Time.time;
            while (Time.time < end && !lancers.Any(l => l != null && l.LastEnd != LancerEnd.None))
            {
                wound |= lancers.Any(l => l != null && (l.IsCharging || l.State == EnemyState.Windup));
                yield return null;
            }
            float waited = Time.time - began;
            Time.timeScale = 1f;
            bool charged = lancers.Any(l => l != null && l.LastEnd != LancerEnd.None);
            string ends = string.Join("/", lancers.Select(l => l != null ? l.LastEnd.ToString() : "gone"));

            Kill(own);
            yield return Until(() => !fight.On && (doors == null || !doors.Shut), 240);
            Assert.That(fight.On, Is.False, where + ": cleared, the fight ends");
            if (doors != null) Assert.That(doors.Shut, Is.False, where + ": and its doors open");
            lines.Add(string.Format(Inv, "{0}: {1} ({2} lancer(s); within {3:0.0} game s of {4}: wound up {5}, a charge ended {6} ({7}))", where,
                                    string.Join(", ", own.GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key)), lancers.Count, waited, FightSeconds, wound || charged, charged, ends));
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator AFloor4RoomWithALancer_IsBuilt_ItsFightWakes_AndCanBeEnded_TheLancerInNoProp()
        {
            Assert.That(_levels.Values.LancersInFloors, Is.True, "the defaults (work order 118)");
            (ulong seed, int order) = FindRoom(_levels.Values, 4, false);
            Assert.That(order, Is.GreaterThanOrEqualTo(0), "a floor 4 with a lancer in the first 600 seeds");
            var lines = new List<string>();
            yield return WakeAndEnd(seed, 4, order, lines);
            string line = "LANCERFLOORPLAY " + string.Join("; ", lines);
            Debug.Log(line);
            TestContext.WriteLine(line);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ARoomWithShootersAndCastersAtTheirPosts_AndALancer_ItsFightWakes_AndStillEnds()
        {
            var lines = new List<string>();
            int floor = 4;
            (ulong seed, int order) found = (0UL, -1);
            foreach (int f in new[] { 5, 4 }) // floor 5 first: two lancers may stand there, and the room is another than the floor-4 test's
            {
                found = FindRoom(_levels.Values, f, true);
                floor = f;
                if (found.order >= 0) break;
            }
            Assert.That(found.order, Is.GreaterThanOrEqualTo(0), "a room with a lancer, shooters and casters on floor 4 or 5 in the first 600 seeds");
            yield return WakeAndEnd(found.seed, floor, found.order, lines, (level, room, own) =>
            {
                List<EnemyShooter> shooters = own.OfType<EnemyShooter>().ToList();
                List<EnemyCaster> casters = own.OfType<EnemyCaster>().ToList();
                Assert.That(shooters, Is.Not.Empty, "shooters in it");
                Assert.That(casters, Is.Not.Empty, "casters in it");
                Assert.That(shooters.All(s => s.Tuning.HoldsPlace), Is.True, "the shooters hold their posts (work order 098)");
                Assert.That(casters.All(c => c.Tuning.HoldsPlace), Is.True, "the casters hold theirs (work order 101)");
            });
            string line = "LANCERFLOORPLAY posts " + string.Join("; ", lines);
            Debug.Log(line);
            TestContext.WriteLine(line);
        }
    }
}
