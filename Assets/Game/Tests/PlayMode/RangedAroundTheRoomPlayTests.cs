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
    /// Ranged enemies around the room (work order 124, the generator's version 23 and the fight's rule of parts 1 and 2; Kristian ticked
    /// "Ja, bare når den synes" and "Ja, med ett sekunds pause"), in the ControlRoom scene through the intents of
    /// <see cref="GeneratedLevels"/>, with the defaults: a room with a near flank is built, wakes as she crosses its line and can be
    /// ended; the near one begins no draw in the first second after the waking. A shooter that stands in her reach but off what the
    /// real camera shows begins no draw until the camera shows it, and then does; with the switch off the same shooter draws off screen,
    /// as before. She cannot be hurt. Nobody has played it: these are numbers. Writes what it found ("RANGEDPLAY").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count game seconds, never a frame's length.
    /// </summary>
    public class RangedAroundTheRoomPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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

        /// <summary>The game times each of these enemies began a draw or a cast at (its AttackStarted reports), from now on.</summary>
        static Dictionary<Enemy, List<float>> Began(IEnumerable<Enemy> enemies)
        {
            var began = new Dictionary<Enemy, List<float>>();
            foreach (Enemy e in enemies)
            {
                var list = new List<float>();
                began[e] = list;
                e.Reported += r =>
                {
                    if (r.Kind == EnemyReportKind.AttackStarted) list.Add(Time.time);
                };
            }
            return began;
        }

        /// <summary>The first seed (floors 1 to 3) whose ordinary combat room holds a near flank shooter.</summary>
        static (ulong seed, int floor, int order) FindNearFlank(LevelValues v)
        {
            for (ulong s = 1; s < 400; s++)
                foreach (int floor in new[] { 1, 2, 3 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, v);
                    foreach (LevelRoom r in l.Rooms)
                        if (r.Kind == RoomKind.Combat && !r.Elite && r.EnemyGroups.Any(g => g.Kind == EnemyKind.Shooter && g.Ranged?.Post == RangedPost.NearFlank))
                            return (s, floor, r.Order);
                }
            return (0UL, 0, -1);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ARoomWithANearFlank_Wakes_TheNearOneWaitsASecond_AndTheFightEnds()
        {
            Assert.That(_levels.Values.RangedAroundTheRoom, Is.True, "the defaults (work order 124)");
            (ulong seed, int floor, int order) = FindNearFlank(_levels.Values);
            Assert.That(order, Is.GreaterThanOrEqualTo(0), "a room with a near flank in the first 400 seeds");
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            LevelRoom room = layout.Rooms[order];
            string where = layout.SeedText + " floor " + floor + " room " + room.Letter + order + " (" + LevelRanged.PatternsOf(room) + ")";
            List<Enemy> own = level.EnemiesByRoom[order].Where(e => e != null).ToList();
            Assert.That(own.Count, Is.EqualTo(room.EnemyCount), where + ": every enemy stands");
            int near = room.EnemyGroups.FindIndex(g => g.Ranged?.Post == RangedPost.NearFlank && g.Kind == EnemyKind.Shooter);
            Vector3 post = level.ToWorld(room.EnemyPoints[near]);
            EnemyShooter nearOne = own.OfType<EnemyShooter>().OrderBy(s => (s.Home - post).sqrMagnitude).First();
            Assert.That(new Vector2(nearOne.Home.x - post.x, nearOne.Home.z - post.z).magnitude, Is.LessThan(0.5f), where + ": the near one stands at its post");
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, where + ": it holds its fight");
            Assert.That(fight.Ranged, Is.SameAs(_levels.Values.RangedAround), where + ": the fight has the rule's values");
            Dictionary<Enemy, List<float>> began = Began(own);

            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            Vector3 inside = level.ToWorld(RoomDoorRules.PullInPlace(layout, room, door, _levels.Values.Fights.PointIn + 1.5, 0d));
            _player.Teleport(inside);
            yield return Until(() => fight.On, 60);
            Assert.That(fight.On, Is.True, where + ": past the line, the room wakes");
            float woke = fight.StartedAt;
            float grace = _levels.Values.RangedAround.GraceSeconds;
            float distance = new Vector2(nearOne.transform.position.x - _player.Position.x, nearOne.transform.position.z - _player.Position.z).magnitude;
            // The first second: no ranged enemy within its reach at the waking begins.
            while (Time.time < woke + grace) yield return null;
            Assert.That(began[nearOne].Where(t => t < woke + grace - 1e-3f), Is.Empty, where + ": the near one began no draw in the first second");
            foreach (Enemy e in own.Where(x => (x is EnemyShooter s && s.Opening.InReachAtWaking) || (x is EnemyCaster c && c.Opening.InReachAtWaking)))
                Assert.That(began[e].Where(t => t < woke + grace - 1e-3f), Is.Empty, where + ": " + e.LogWord + " in reach at the waking began nothing in the first second");
            // Then it may: within a few game seconds, if the camera shows it and it sees her.
            float until = Time.time + 6f;
            while (Time.time < until && began[nearOne].Count == 0) yield return null;
            bool shown = RangedOpening.OnScreen(nearOne.transform.position, _levels.Values.RangedAround.ScreenMargin);
            string first = began[nearOne].Count > 0 ? (began[nearOne][0] - woke).ToString("0.00", Inv) + " s after the waking" : "none in 6 s (on screen " + shown + ", sees her " + nearOne.SeesHer() + ")";

            Kill(own);
            yield return Until(() => !fight.On, 240);
            Assert.That(fight.On, Is.False, where + ": cleared, the fight ends");
            string line = string.Format(Inv, "RANGEDPLAY near flank {0}: {1:0.0} m from her at the waking; its first draw {2}", where, distance, first);
            Debug.Log(line);
            TestContext.WriteLine(line);
            if (shown && nearOne.SeesHer()) Assert.That(began[nearOne], Is.Not.Empty, where + ": on screen and seeing her, it draws after its second");
        }

        /// <summary>
        /// A combat room's shooter (floors 1 to 3) with open floor 9.5 m and 6.5 m north of its post that it sees: she stands at the far
        /// spot (in its reach, below the camera's view of the ground, off screen), then at the near one (on screen).
        /// </summary>
        IEnumerator OffScreenThenOn(LevelValues v, Action<string, EnemyShooter, List<float>, bool[]> done)
        {
            _levels.Values = v;
            for (ulong s = 1; s < 60; s++)
                foreach (int floor in new[] { 1, 2, 3 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, v);
                    foreach (LevelRoom r in l.Rooms)
                    {
                        if (r.Kind != RoomKind.Combat) continue;
                        for (int i = 0; i < r.EnemyGroups.Count; i++)
                        {
                            if (r.EnemyGroups[i].Kind != EnemyKind.Shooter || r.EnemyGroups[i].Elite) continue;
                            LevelPoint p = r.EnemyPoints[i];
                            var far = new LevelPoint(p.X, p.Y + 9.5);
                            var close = new LevelPoint(p.X, p.Y + 6.5);
                            bool Open(LevelPoint q) => l.RoomAt(q.X, q.Y) == r && RoomShapes.DistanceToWalls(r, q, l.CellSize) > 1.0 && r.Props.All(x => x.Footprint.DistanceTo(q) > 1.0);
                            if (!Open(far) || !Open(close)) continue;
                            GeneratedLevel level = _levels.Build(s, floor);
                            yield return Frames(2);
                            List<Enemy> own = level.EnemiesByRoom[r.Order].Where(e => e != null).ToList();
                            Vector3 post = level.ToWorld(p);
                            EnemyShooter shooter = own.OfType<EnemyShooter>().OrderBy(e => (e.Home - post).sqrMagnitude).First();
                            Vector3 wFar = level.ToWorld(far), wClose = level.ToWorld(close);
                            if (!NavMesh.SamplePosition(wFar, out NavMeshHit h1, 0.3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(wClose, out NavMeshHit h2, 0.3f, NavMesh.AllAreas)
                                || !shooter.Sees(wFar) || !shooter.Sees(wClose))
                                continue;
                            foreach (List<Enemy> list in level.EnemiesByRoom.Values)
                                foreach (Enemy e in list)
                                    if (e != null && e != shooter) Object.Destroy(e.gameObject);
                            yield return Frames(2);
                            List<float> began = Began(new Enemy[] { shooter })[shooter];
                            string where = l.SeedText + " floor " + floor + " room " + r.Letter + r.Order + " shooter " + i;
                            _player.Teleport(new Vector3(h1.position.x, 0f, h1.position.z));
                            yield return Frames(3);
                            shooter.Provoke();
                            RoomFight fight = shooter.Fight;
                            yield return Until(() => fight != null && fight.On && shooter.IsAwake, 60);
                            Assert.That(shooter.IsAwake && fight.On, Is.True, where + ": awake in its fight");
                            // Off screen in her reach for three game seconds (the grace over after the first).
                            bool offAll = true, inReach = true;
                            float until = Time.time + 3f;
                            while (Time.time < until)
                            {
                                offAll &= !RangedOpening.OnScreen(shooter.transform.position, v.RangedAround.ScreenMargin);
                                inReach &= new Vector2(shooter.transform.position.x - _player.Position.x, shooter.transform.position.z - _player.Position.z).magnitude <= ShooterValues.ShootsFrom;
                                yield return null;
                            }
                            int offScreenDraws = began.Count;
                            // Then on screen: she steps to the near spot.
                            _player.Teleport(new Vector3(h2.position.x, 0f, h2.position.z));
                            yield return Frames(3);
                            bool shown = RangedOpening.OnScreen(shooter.transform.position, v.RangedAround.ScreenMargin);
                            until = Time.time + 5f;
                            while (Time.time < until && began.Count == offScreenDraws) yield return null;
                            done(where, shooter, began, new[] { offAll, inReach, shown, began.Count > offScreenDraws, offScreenDraws > 0 });
                            Kill(new Enemy[] { shooter });
                            yield break;
                        }
                    }
                }
            Assert.Fail("no shooter with open floor 9.5 m and 6.5 m north of its post that it sees, in the first 60 seeds");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator AShooterInReach_OffScreen_BeginsNoDraw_UntilTheCameraShowsIt()
        {
            string line = null;
            yield return OffScreenThenOn(new LevelValues { Waves = false }, (where, shooter, began, f) =>
            {
                Assert.That(f[0], Is.True, where + ": off the camera's view the whole first part");
                Assert.That(f[1], Is.True, where + ": and in its reach");
                Assert.That(f[4], Is.False, where + ": it began no draw off screen");
                Assert.That(f[2], Is.True, where + ": then on screen");
                Assert.That(f[3], Is.True, where + ": then it draws");
                var e = new SessionEvent(0, SessionEventTypes.EnemyLoose, ShooterValues.Arrow);
                RangedOpening.Note(e, shooter);
                Assert.That(e.Extra.Any(kv => kv.Key == "drew_on_screen" && kv.Value == "yes"), Is.True, where + ": the log line says it drew on screen");
                line = "RANGEDPLAY off screen " + where + ": no draw in 3 s off screen; on screen, a draw " + (began.Count > 0 ? "yes" : "no");
            });
            Debug.Log(line);
            TestContext.WriteLine(line);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator WithTheSwitchOff_TheSameShooterDrawsOffScreen_AsBefore()
        {
            string line = null;
            var off = new LevelValues { RangedAroundTheRoom = false, BossAlone = false, LineUps = false };
            yield return OffScreenThenOn(off, (where, shooter, began, f) =>
            {
                Assert.That(shooter.Fight.Ranged, Is.Null, where + ": the fight has no such rule");
                Assert.That(f[0] && f[1], Is.True, where + ": off screen in its reach");
                Assert.That(f[4], Is.True, where + ": with the switch off it draws off screen, as before");
                line = "RANGEDPLAY switch off " + where + ": drew off screen " + began.Count + " time(s)";
            });
            Debug.Log(line);
            TestContext.WriteLine(line);
        }
    }
}
