using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// A room is a fight (work order 070, 2026-11-21, Kristian after his evening session: "nå rman kommer i et rom, så er man i
    /// en combat ... man trenger et punkt in i rommet der alle flyr på"; <see cref="RoomFightRules"/>), in the ControlRoom scene
    /// through the intents of <see cref="GeneratedLevels"/>, on floors with passages and templates (the defaults). She stands in
    /// the passage: nobody is awake. She crosses the point: every enemy of the room is awake within a few frames, each kind doing
    /// its fight, and the next rooms' enemies sleep on. One hit from the passage wakes all of them; one killed by its first hit
    /// wakes the others. She gets away: all of them go home in the same frame, and the next waking is all of them again. The
    /// elite room, the exit room and the boss's room. The recorder's event. The switch off is today.
    ///
    /// She cannot be hurt. Frames without a window are short (about 0.3 ms): the waits count frames or game seconds.
    /// </summary>
    public class RoomFightsPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;
        /// <summary>Frames within which a room's every enemy is awake once it wakes: the floor sees her, and each enemy steps once.</summary>
        const int WakeFrames = 3;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;

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
            if (_health != null) _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                if (_health != null) _health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel StartRun(Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            Assert.That(level.Layout.Passages && level.Layout.Templates, "a floor with passages and templates");
            return level;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A ground point <paramref name="depth"/> m straight in from the middle of <paramref name="door"/>, into <paramref name="room"/>.</summary>
        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            return level.ToWorld(p);
        }

        /// <summary>The middle of the passage's cell farthest, past their ranges, from the room's enemies: where she stands "in the passage".</summary>
        static Vector3 FarInPassage(GeneratedLevel level, LevelRoom passage, List<Enemy> enemies)
        {
            float cs = level.Layout.CellSize;
            Vector3 best = Vector3.zero;
            float bestMargin = float.NegativeInfinity;
            foreach ((int x, int y) in Passages.PathOf(level.Layout, passage))
            {
                Vector3 c = level.ToWorld(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs));
                float margin = enemies.Min(e => Flat(e.transform.position, c) - e.Values.AggroRange);
                if (margin <= bestMargin) continue;
                bestMargin = margin;
                best = c;
            }
            return best;
        }

        /// <summary>She stands at a point, and the run has seen her in the room there.</summary>
        IEnumerator At(Vector3 point, LevelRoom room)
        {
            _player.Teleport(point);
            for (int i = 0; i < 30 && _levels.CurrentRoom != room; i++) yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        static string Names(IEnumerable<Enemy> enemies) => string.Join(", ", enemies.Select(e => e.LogWord + " " + e.State));

        /// <summary>The first combat room after the entrance, its passage before it, and its enemies.</summary>
        static (LevelRoom room, LevelRoom passage, List<Enemy> own) FirstRoom(GeneratedLevel level)
        {
            LevelLayout layout = level.Layout;
            LevelRoom passage = layout.Rooms[1], room = layout.Rooms[2];
            Assert.That((passage.Kind, room.Kind), Is.EqualTo((RoomKind.Passage, RoomKind.Combat)));
            return (room, passage, level.EnemiesByRoom[room.Order]);
        }

        /// <summary>Stands her in the passage before the room, past every enemy's range, and sees that nobody is awake there.</summary>
        IEnumerator InThePassage_NobodyAwake(GeneratedLevel level, LevelRoom passage, List<Enemy> own)
        {
            Vector3 stand = FarInPassage(level, passage, own);
            yield return At(stand, passage);
            Enemy seer = own.FirstOrDefault(e => Flat(e.transform.position, stand) <= e.Values.AggroRange);
            Assert.That(seer, Is.Null, "the test's stand in the passage is past every enemy's range");
            yield return Frames(10);
            Assert.That(level.Enemies.Where(e => e != null && e.IsAwake), Is.Empty, "nobody awake while she stands in the passage");
        }

        IEnumerator UntilAllAwake(List<Enemy> own, Action<int> frames)
        {
            int n = 0;
            while (n < WakeFrames + 2 && !own.All(e => e == null || e.IsDead || e.IsAwake))
            {
                yield return null;
                n++;
            }
            frames(n);
        }

        // ---------------- She steps in ----------------

        [UnityTest]
        public IEnumerator InThePassage_NobodyIsAwake_PastThePoint_EveryEnemyOfTheRoomIs_EachDoingItsFight_AndTheNextRoomsSleepOn()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "the room has its fight");
            Assert.That(own.All(e => e.Fight == fight), "each of its enemies knows it");
            Assert.That(level.Enemies.Where(e => !own.Contains(e)).All(e => e.Fight != fight), "and no other");
            Assert.That(_levels.RoomFights.Count, Is.EqualTo(level.EnemiesByRoom.Count(p => p.Value.Count > 0)), "a fight for every room with enemies");
            yield return InThePassage_NobodyAwake(level, passage, own);

            // A step short of the point (4 m in): still nobody, unless one of them is within its own range there.
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            Vector3 shortOf = InFrom(level, room, door, 4.0);
            yield return At(shortOf, room);
            yield return Frames(2);
            bool seen = own.Any(e => Flat(e.transform.position, shortOf) <= e.Values.AggroRange);
            if (!seen) Assert.That(fight.On, Is.False, "4 m in, short of the point: the room still sleeps");

            // Past the point: every one of them within a few frames.
            _player.Teleport(InFrom(level, room, door, 5.5));
            int took = -1;
            yield return UntilAllAwake(own, n => took = n);
            Assert.That(own.All(e => e.IsAwake), "every enemy of the room is awake: " + Names(own.Where(e => !e.IsAwake)));
            Assert.That(took, Is.LessThanOrEqualTo(WakeFrames), "within a frame or two");
            Assert.That(fight.On, Is.True);
            Assert.That(fight.StartedBy, Is.EqualTo(seen ? fight.StartedBy : RoomFightStart.Point));
            Assert.That(level.Enemies.Where(e => !own.Contains(e) && e.IsAwake), Is.Empty, "no enemy of another room is awake");

            // Each doing its fight: the melee kinds run at her, the shooters and casters close in or shoot.
            var start = own.ToDictionary(e => e, e => Flat(e.transform.position, _player.Position));
            var nearest = new Dictionary<Enemy, float>(start);
            var attacked = new HashSet<Enemy>();
            var listeners = new List<(Enemy, Action<EnemyReport>)>();
            foreach (Enemy e in own)
            {
                Enemy me = e;
                Action<EnemyReport> heard = r => { if (r.Kind == EnemyReportKind.AttackStarted || r.Kind == EnemyReportKind.Struck) attacked.Add(me); };
                e.Reported += heard;
                listeners.Add((e, heard));
            }
            // Work order 098: a shooter that holds its place, she out of its reach, does its fight by standing at its post.
            var offPost = own.OfType<EnemyShooter>().ToDictionary(s => s, s => 0f);
            Time.timeScale = 4f;
            float until = Time.time + 8f;
            while (Time.time < until)
            {
                yield return null;
                foreach (Enemy e in own) nearest[e] = Mathf.Min(nearest[e], Flat(e.transform.position, _player.Position));
                foreach (EnemyShooter s in offPost.Keys.ToList()) offPost[s] = Mathf.Max(offPost[s], Flat(s.transform.position, s.Post));
                Assert.That(own.All(e => e.IsAwake), "none gives up: " + Names(own.Where(e => !e.IsAwake)));
            }
            Time.timeScale = 1f;
            foreach ((Enemy e, Action<EnemyReport> heard) in listeners) e.Reported -= heard;
            foreach (Enemy e in own)
            {
                bool came = nearest[e] <= start[e] - 1f || nearest[e] <= 3f;
                bool held = e is EnemyShooter s && s.HoldsItsPlace && start[e] > ShooterValues.ShootsFrom
                            && offPost[s] <= s.Tuning.HoldRadius + ShooterPostRules.EdgeSlack;
                Assert.That(came || attacked.Contains(e) || held, e.LogWord + " " + e.name + " did its fight: it came at her (from " + start[e].ToString("0.0", Inv)
                                                            + " m to " + nearest[e].ToString("0.0", Inv) + " m) or attacked, or (a shooter) held its post");
            }
            Assert.That(level.Enemies.Where(e => !own.Contains(e) && e.IsAwake), Is.Empty, "the next rooms sleep on");
        }

        [UnityTest]
        public IEnumerator AHitFromThePassage_WakesTheWholeRoom_AndOneKilledByItsFirstHit_WakesTheOthers()
        {
            foreach (bool kill in new[] { false, true })
            {
                GeneratedLevel level = StartRun();
                yield return Frames(2);
                (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
                yield return InThePassage_NobodyAwake(level, passage, own);
                Enemy hit = own.OrderBy(e => Flat(e.transform.position, _player.Position)).First();
                // What her shot does to it from the passage: a hit, of 1, or of more than it has.
                hit.Body.TakeHit(kill ? 100000000 : 1);
                int took = -1;
                yield return UntilAllAwake(own, n => took = n);
                string what = kill ? "killed by its first hit" : "hit once";
                Assert.That(hit.IsDead, Is.EqualTo(kill), what);
                Assert.That(own.Where(e => e != hit).All(e => e.IsAwake), "the room woke, one " + what + ": " + Names(own.Where(e => !e.IsAwake && e != hit)));
                Assert.That(took, Is.LessThanOrEqualTo(WakeFrames), what);
                Assert.That(_levels.FightOf(room).StartedBy, Is.EqualTo(RoomFightStart.Hit), what);
                Assert.That(level.Enemies.Where(e => !own.Contains(e) && e.IsAwake), Is.Empty, "the next rooms sleep on, " + what);
            }
        }

        // ---------------- She gets away, and comes back ----------------

        [UnityTest]
        public IEnumerator SheGetsAway_TheyAllGoHomeTogether_AndTheNextWakingIsAllOfThemAgain()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            RoomFight fight = _levels.FightOf(room);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(InFrom(level, room, door, 5.5));
            yield return UntilAllAwake(own, _ => { });
            Assert.That(own.All(e => e.IsAwake));
            Time.timeScale = 4f;
            float until = Time.time + 2f;
            while (Time.time < until) yield return null; // they come at her
            Time.timeScale = 1f;

            // Far away (the exit room, its enemies switched off so they take no part): all of them give up in one frame.
            LevelRoom exit = level.Layout.ExitRoom;
            foreach (Enemy e in level.EnemiesByRoom[exit.Order]) e.gameObject.SetActive(false);
            _player.Teleport(level.Middle(exit));
            var wentHome = new Dictionary<Enemy, int>();
            for (int i = 0; i < 60 && wentHome.Count < own.Count; i++)
            {
                yield return null;
                foreach (Enemy e in own) if (!e.IsAwake && !wentHome.ContainsKey(e)) wentHome[e] = Time.frameCount;
            }
            Assert.That(wentHome.Count, Is.EqualTo(own.Count), "every one of them gave up: " + Names(own.Where(e => e.IsAwake)));
            Assert.That(wentHome.Values.Max() - wentHome.Values.Min(), Is.LessThanOrEqualTo(1), "together, in the same frame or the next");
            Assert.That(fight.On, Is.False);
            Assert.That(own.All(e => e.State == EnemyState.Idle));

            // They walk home; she comes back to the passage; one wakes: all of them again.
            Time.timeScale = 4f;
            until = Time.time + 6f;
            while (Time.time < until) yield return null;
            Time.timeScale = 1f;
            yield return InThePassage_NobodyAwake(level, passage, own);
            own.OrderBy(e => Flat(e.transform.position, _player.Position)).First().Body.TakeHit(1);
            yield return UntilAllAwake(own, _ => { });
            Assert.That(own.All(e => e.IsAwake), "all of them again: " + Names(own.Where(e => !e.IsAwake)));
            Assert.That(fight.Starts, Is.EqualTo(2));
        }

        // ---------------- The elite room, the exit room, the boss's ----------------

        [UnityTest]
        public IEnumerator TheEliteRoom_AndTheExitRoom_WakeWhole_AtTheirPoints()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            foreach (LevelRoom room in new[] { level.Layout.EliteRoom, level.Layout.ExitRoom })
            {
                Assert.That(room, Is.Not.Null);
                List<Enemy> own = level.EnemiesByRoom[room.Order];
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                yield return At(InFrom(level, room, door, 5.5), room);
                yield return UntilAllAwake(own, _ => { });
                string name = room.Elite ? "the elite room" : "the exit room";
                Assert.That(own.All(e => e.IsAwake), name + ": " + Names(own.Where(e => !e.IsAwake)));
                if (room.Elite) Assert.That(own.Contains(level.Elite) && level.Elite.IsAwake, "the elite among them");
                Assert.That(_levels.FightOf(room).On, Is.True, name);
            }
        }

        [UnityTest]
        public IEnumerator TheBossRoom_WakesWhole_AtItsPoint_NotAtTheDoor()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, 5), 5);
            yield return Frames(2);
            LevelRoom exit = level.Layout.ExitRoom;
            Assert.That(level.Boss, Is.Not.Null, "floor 5's exit room holds the act boss");
            List<Enemy> own = level.EnemiesByRoom[exit.Order];
            Assert.That(own.Contains(level.Boss));
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, exit);
            // A metre inside the door: the boss woke here before work order 070; now the room waits for its point.
            Vector3 inside = InFrom(level, exit, door, 1.0);
            yield return At(inside, exit);
            yield return Frames(4);
            if (!own.Any(e => Flat(e.transform.position, inside) <= e.Values.AggroRange))
                Assert.That(own.Where(e => e.IsAwake), Is.Empty, "a metre inside the door, short of the point: the boss's room still sleeps");
            _player.Teleport(InFrom(level, exit, door, 5.5));
            yield return UntilAllAwake(own, _ => { });
            Assert.That(own.All(e => e.IsAwake), "the boss and every enemy of its room: " + Names(own.Where(e => !e.IsAwake)));
            Assert.That(level.Boss.HeldByFight, Is.True);
        }

        // ---------------- The recorder ----------------

        [UnityTest]
        public IEnumerator TheRecorder_IsToldOfTheFight_WhereItIs_HowItStarted_AndHowManyWoke()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeRoomFightsTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            yield return InThePassage_NobodyAwake(level, passage, own);
            _player.Teleport(InFrom(level, room, LevelGeometry.EntryDoor(level.Layout, room), 5.5));
            yield return UntilAllAwake(own, _ => { });
            yield return Frames(2);
            List<SessionEvent> fights = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomFight).ToList();
            Assert.That(fights.Count, Is.EqualTo(1), "one fight started");
            SessionEvent f = fights[0];
            Assert.That(f.Name, Is.EqualTo("point"));
            Assert.That(f.ExtraValue("floor"), Is.EqualTo("1"));
            Assert.That(f.ExtraValue("room"), Is.EqualTo((LevelGeometry.RoomNumber(level.Layout, room) + 1).ToString(Inv)), "the room as the run box counts it");
            Assert.That(f.ExtraValue("room"), Is.EqualTo("2"), "the second room, the passage not counted");
            Assert.That(f.ExtraValue("order"), Is.EqualTo(room.Order.ToString(Inv)));
            Assert.That(f.ExtraValue("kind"), Is.EqualTo("combat"));
            Assert.That(f.ExtraValue("inside"), Is.EqualTo("true"), "she was in the room");
            Assert.That(f.ExtraValue("woke"), Is.EqualTo(own.Count.ToString(Inv)), "every one of them");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.EnemyWake && own.Any(o => o.Id == e.EnemyId)), Is.EqualTo(own.Count), "and each enemy's own waking");
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_IsToday_OneHitWakesOneEnemy_AndNoRoomHasAFight()
        {
            GeneratedLevel level = StartRun(v => v.RoomFights = false);
            yield return Frames(2);
            Assert.That(_levels.RoomFights, Is.Empty);
            Assert.That(level.Enemies.All(e => e.Fight == null), "no enemy has a fight");
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            // A stand in the passage or the room past every enemy's range, with one within its leash that wakes no pack (not a
            // swarmer, or a swarmer with no other swarmer within its pack's range): a hurt wakes it there today, and nothing else.
            float cs = level.Layout.CellSize;
            bool WakesNoPack(Enemy e) => !(e is EnemySwarmer s)
                || !level.Enemies.Any(o => o != null && o != e && o is EnemySwarmer && Flat(o.transform.position, e.transform.position) <= s.Tuning.PackWakeRange + 1f);
            Enemy lone = null;
            Vector3 stand = Vector3.zero;
            LevelRoom standIn = null;
            foreach (LevelRoom r in new[] { passage, room })
                for (int x = r.Rect.X; x < r.Rect.Right && lone == null; x++)
                    for (int y = r.Rect.Y; y < r.Rect.Top && lone == null; y++)
                    {
                        if (level.Layout.RoomAtCell(x, y) != r) continue;
                        Vector3 c = level.ToWorld(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs));
                        if (level.Enemies.Any(e => e != null && Flat(e.transform.position, c) <= e.Values.AggroRange + 1f)) continue;
                        Enemy near = own.Where(e => WakesNoPack(e) && Flat(e.transform.position, c) <= e.Values.LeashRange - 2f)
                                        .OrderBy(e => Flat(e.transform.position, c)).FirstOrDefault();
                        if (near == null) continue;
                        lone = near;
                        stand = c;
                        standIn = r;
                    }
            Assert.That(lone, Is.Not.Null, "a stand past every enemy's range with one that wakes no pack within its leash");
            yield return At(stand, standIn);
            yield return Frames(10);
            Assert.That(level.Enemies.Where(e => e != null && e.IsAwake), Is.Empty, "nobody awake where she stands");
            lone.Body.TakeHit(1);
            yield return Frames(6);
            Assert.That(lone.IsAwake, "the one hit wakes");
            Assert.That(own.Where(e => e != lone && e.IsAwake), Is.Empty, "and only it, as before");
        }
    }
}
