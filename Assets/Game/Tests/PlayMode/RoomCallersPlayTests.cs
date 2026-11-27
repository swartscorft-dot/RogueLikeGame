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
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The caller in the game (work order 137; Kristian's tick of 2026-11-25, "Ja, fra etasje 2"), in the ControlRoom scene through
    /// <see cref="GeneratedLevels"/> on a floor of a run seed built alone: the caller stands far in its room and on his screen from the
    /// room's line, in the caster's model in its own tint, with a caster's health, one of its room's own (in wave 1, the doors and "enemies
    /// left" count it) in place of some of its partners; after its first wait it tells a call for a second while three discs stand by
    /// 136's arrival rule, then three swarmers stand there awake, every 6 s, never more than six alive; its called hold no door and are no
    /// room's; a daze or a counter cuts a call; its death fells every one of its called in the same hit, and the room is cleared; the
    /// recorder writes the calls and its end; off, by the switch or the Trials line, there is no caller and every room is as on 136; its
    /// step allocates nothing while nothing changes. Nobody has played it: these are the rule's own checks.
    /// </summary>
    public class RoomCallersPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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
            RoomTrialSettings.SetCallers(true, keep: false);
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
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds = 5f)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 30f;
            while (!condition() && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;

        /// <summary>The first floor of the run (3, 4, 5, then 2) whose rules give it a caller.</summary>
        static int FloorWithACaller()
        {
            foreach (int floor in new[] { 3, 4, 5, 2 })
            {
                LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, floor), floor, new LevelValues());
                if (RoomCallerRules.Rooms(l, new LevelValues()).Count > 0) return floor;
            }
            return -1;
        }

        GeneratedLevel Build(int floor, Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        IEnumerator AtTheLine(GeneratedLevel level, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            yield return Until(() => fight.On, 2f);
            Assert.That(fight.On, Is.True, "past the line: the room woke");
        }

        /// <summary>She stands past the room's line (it wakes), the doors shut behind her; then in the room's middle, the camera with her.</summary>
        IEnumerator InAndToTheMiddle(GeneratedLevel level, LevelRoom room)
        {
            yield return AtTheLine(level, room);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            yield return Until(() => d == null || d.Shut, 3f);
            _player.Teleport(OnNavMesh(level.Middle(room)));
            yield return GameTime(0.6f); // the camera follows her
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ACallersRoom_ItStandsFarIn_OnHisScreenFromTheLine_ItsPartnersGaveWay_OneOfTheRoomsOwn_InWave1()
        {
            int floor = FloorWithACaller();
            Assert.That(floor, Is.GreaterThan(0), "a floor of the run holds a caller");
            GeneratedLevel level = Build(floor);
            yield return Frames(2);
            Assert.That(_levels.CallersOnThisFloor, Is.True, "the switch on");
            List<LevelRoom> ruled = RoomCallerRules.Rooms(level.Layout, _levels.Values);
            Assert.That(_levels.RoomCallerList.Select(c => c.Room), Is.EqualTo(ruled), "the rules' rooms");
            GeneratedLevels.RoomCaller rc = _levels.RoomCallerList[0];
            LevelRoom room = rc.Room;
            EnemyCaller caller = rc.Caller;
            Assert.That(caller, Is.Not.Null);
            Assert.That(caller.Kind, Is.EqualTo(EnemyKind.Caller));
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            Assert.That(own, Has.Member(caller), "one of its room's own");
            Assert.That(level.Called, Has.No.Member(caller));
            Assert.That(own.Count(e => e is EnemyCaller), Is.EqualTo(1), "one a room");
            Assert.That(level.RoomAt(caller.transform.position), Is.SameAs(room));
            int partnersPlaced = RoomCallerRules.Partners(room);
            int gave = RoomCallerRules.GiveWay(partnersPlaced, room.LineUp.Partner, _levels.Values.CallerRules, _levels.Values.LineUpRules);
            Assert.That(rc.GaveWay, Is.EqualTo(gave));
            Assert.That(own.Count(e => e != null && !e.IsElite && e.Kind == room.LineUp.Partner), Is.EqualTo(partnersPlaced - gave), "its partners gave way");
            Assert.That(own.Count, Is.EqualTo(room.EnemyCount - gave + 1), "the room: less the partners, and the caller");
            Assert.That(level.Enemies.Count(e => e == null), Is.EqualTo(0), "the partners that gave way are gone from the floor's list");
            Assert.That(caller.Body.MaxHealth, Is.EqualTo(_levels.Values.HealthOf(EnemyKind.Caster)), "a caster's health");
            Assert.That(caller.BodyColor, Is.EqualTo(EnemyFactory.ColorOf(_levels.Values.CallerRules.Tint)), "its tint");
            if (caller.Look != null) Assert.That(caller.Look.Spec.Name, Is.EqualTo(EnemyModelSpec.Caster.Name), "the caster's model");
            GeneratedLevels.RoomWaves w = _levels.WavesOf(room);
            if (w != null) Assert.That(w.Waves[0], Has.Member(caller), "it stands in wave 1");
            LevelDoor entry = LevelGeometry.EntryDoor(level.Layout, room);
            double depth = LevelLancers.Depth(level.Layout, room, entry, _levels.Values);
            Assert.That(LevelGeometry.DistanceToDoor(level.ToLevel(caller.transform.position), entry, level.Layout.CellSize) / depth,
                        Is.GreaterThanOrEqualTo(Math.Min(_levels.Values.CallerRules.FarShare, _levels.Values.CallerRules.InViewShareLeast) - _levels.Values.CallerRules.InViewBand - 0.05), "far in (the far band, else the deepest in view)");

            yield return AtTheLine(level, room);
            yield return GameTime(0.6f); // the camera follows her
            Assert.That(RangedOpening.OnScreen(caller.transform.position, 0f), Is.True, "he sees it from the line");
            Assert.That(caller.IsAwake, Is.True, "awake with its room");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ItCallsThree_AfterItsTell_EverySixSeconds_NeverPastSix_ItsCalledHoldNoDoor_AndAllFallInTheHitThatKillsIt()
        {
            int floor = FloorWithACaller();
            GeneratedLevel level = Build(floor);
            yield return Frames(2);
            GeneratedLevels.RoomCaller rc = _levels.RoomCallerList[0];
            LevelRoom room = rc.Room;
            EnemyCaller caller = rc.Caller;
            CallerValues c = _levels.Values.CallerRules;
            RoomWaveValues rules = _levels.Values.WaveRules;
            yield return InAndToTheMiddle(level, room);
            float woke = _levels.FightOf(room).StartedAt;

            // Its first call: after its wait, a tell of a second with three discs by 136's rule.
            yield return Until(() => caller.Calling, c.FirstCallAfter + 1f);
            Assert.That(caller.Calling, Is.True, "a call begins");
            Assert.That(Time.time - woke, Is.InRange(c.FirstCallAfter - 0.1f, c.FirstCallAfter + 0.5f), "its first call " + c.FirstCallAfter + " s after the waking");
            Assert.That(caller.State, Is.EqualTo(EnemyState.Windup), "its tell is its wind-up");
            Assert.That(caller.IsTelegraphing, Is.True, "the ring round it");
            Assert.That(rc.Discs.Count, Is.InRange(1, c.CallCount), "its discs");
            Vector3 her = _player.Position;
            Camera cam = Camera.main;
            Vector3 up = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            foreach (GeneratedLevels.WaveArrival a in rc.Discs)
            {
                Vector3 to = a.At - her;
                to.y = 0f;
                Assert.That(to.magnitude, Is.InRange(rules.Nearest - 0.35f, rules.Farthest + 0.35f), "7 to 11 m from her");
                Assert.That(Vector3.Dot(to, up), Is.GreaterThan(-0.35f), "never below her on his screen");
                Assert.That(RangedOpening.OnScreen(a.At, 0f), Is.True, "on his screen");
                Assert.That(a.Disc != null && a.Disc.enabled, Is.True, "its disc on the ground");
                Assert.That(level.RoomAt(a.At), Is.SameAs(room));
            }
            List<GeneratedLevels.WaveArrival> discs = rc.Discs.ToList();
            yield return Until(() => !caller.Calling, c.TellSeconds + 0.5f);
            Assert.That(caller.CallsMade, Is.EqualTo(1));
            Assert.That(rc.Called.Count, Is.EqualTo(discs.Count), "a swarmer for each disc");
            foreach (GeneratedLevels.WaveArrival a in discs)
            {
                Assert.That(a.Disc, Is.Null, "the disc gone");
                Assert.That(a.Enemy, Is.Not.Null);
                Assert.That(a.Enemy.Kind, Is.EqualTo(EnemyKind.Swarmer));
                Assert.That(Vector3.Distance(a.Enemy.Home, a.At), Is.LessThan(0.05f), "it stood on its disc");
            }
            yield return Frames(2);
            foreach (Enemy e in rc.Called)
            {
                Assert.That(e.IsAwake, Is.True, "awake");
                Assert.That(e.Fight, Is.SameAs(_levels.FightOf(room)), "its caller's room's fight");
                Assert.That(level.EnemiesByRoom[room.Order], Has.No.Member(e), "no room's own");
                Assert.That(level.CalledByRoom.TryGetValue(room.Order, out List<Enemy> boss) && boss.Contains(e), Is.False, "not the boss's packs' count");
                Assert.That(level.Called, Has.Member(e), "one of the floor's called");
                Assert.That(e.Body.MaxHealth, Is.EqualTo(_levels.Values.HealthOf(EnemyKind.Swarmer)), "the floor's swarmer");
            }

            // Every other enemy of the room dead, every wave come: the doors are held by the caller alone, "enemies left" is 1.
            GeneratedLevels.RoomWaves w = _levels.WavesOf(room);
            for (int i = 0; i < 40; i++)
            {
                Kill(level.EnemiesByRoom[room.Order].Where(e => e != caller));
                if (w == null || (w.Started >= w.Count && !w.StillArriving)) break;
                yield return GameTime(0.5f);
            }
            Kill(level.EnemiesByRoom[room.Order].Where(e => e != caller));
            yield return Frames(3);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            if (d != null) Assert.That(d.Shut, Is.True, "the caller alive: shut");
            Assert.That(_levels.EnemiesLeftInRoom, Is.EqualTo(1), "\"enemies left\": the caller, not its called");
            Assert.That(_levels.FightOf(room).On, Is.True);

            // Its calls go on every 6 s; never past six alive.
            Time.timeScale = 3f;
            yield return Until(() => caller.CallsMade >= 2, c.CallEvery + 1f);
            Assert.That(caller.CallsMade, Is.EqualTo(2), "a call every " + c.CallEvery + " s");
            int alive = rc.CalledAlive;
            Assert.That(alive, Is.LessThanOrEqualTo(c.MostAlive));
            yield return Until(() => rc.Calls >= 3 && !caller.Calling, c.CallEvery + 1f);
            Time.timeScale = 1f;
            Assert.That(rc.Calls, Is.GreaterThanOrEqualTo(3));
            Assert.That(rc.CalledAlive, Is.LessThanOrEqualTo(c.MostAlive), "never more than " + c.MostAlive + " alive");
            if (alive == c.MostAlive) Assert.That(caller.CallsNone, Is.EqualTo(1), "with six alive its call brings none, and tells nothing");
            else Assert.That(rc.CalledAlive, Is.LessThanOrEqualTo(alive + c.CallCount));

            // It dies: every one of its called falls in that hit, the room is cleared, the doors open.
            List<Enemy> calledAlive = rc.Called.Where(e => !e.IsDead).ToList();
            Assert.That(calledAlive, Is.Not.Empty);
            caller.Body.TakeHit(100000000);
            Assert.That(calledAlive.All(e => e.IsDead), Is.True, "they fall in the hit that kills it");
            Assert.That(rc.Fell, Is.EqualTo(calledAlive.Count));
            Assert.That(rc.Ended, Is.True);
            if (d != null)
            {
                yield return Until(() => !d.Shut, 2f);
                Assert.That(d.Shut, Is.False, "the doors open");
            }
            yield return Until(() => !_levels.FightOf(room).On, 2f);
            Assert.That(_levels.FightOf(room).On, Is.False, "the fight over");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ACallCut_ByADaze_OrStoppingShotsCounter_TakesItsDiscs_AndBringsNone()
        {
            int floor = FloorWithACaller();
            GeneratedLevel level = Build(floor);
            yield return Frames(2);
            GeneratedLevels.RoomCaller rc = _levels.RoomCallerList[0];
            EnemyCaller caller = rc.Caller;
            CallerValues c = _levels.Values.CallerRules;
            yield return InAndToTheMiddle(level, rc.Room);
            yield return Until(() => caller.Calling, c.FirstCallAfter + 1f);
            Assert.That(caller.Calling, Is.True);
            List<GeneratedLevels.WaveArrival> discs = rc.Discs.ToList();
            Assert.That(discs, Is.Not.Empty);
            caller.Body.DazeFor(0.5f);
            yield return Frames(2);
            Assert.That(caller.Calling, Is.False, "the daze dropped it");
            Assert.That(caller.CallsCut, Is.EqualTo(1));
            Assert.That(rc.Discs, Is.Empty);
            Assert.That(discs.All(a => a.Disc == null), Is.True, "its discs gone");
            yield return GameTime(c.TellSeconds + 0.2f);
            Assert.That(rc.Called, Is.Empty, "none came");

            // The next call, countered from its front as the caster's big cast is.
            Time.timeScale = 3f;
            yield return Until(() => caller.Calling, c.CallEvery + 1f);
            Time.timeScale = 1f;
            Assert.That(caller.Calling, Is.True, "its next call, a call's time after the one before began");
            Vector3 front = caller.transform.position + caller.transform.forward * 5f;
            Assert.That(caller.TryCounter(front, 60f, 0.5f), Is.True, "Stopping Shot from its front counters it");
            yield return Frames(2);
            Assert.That(caller.Calling, Is.False);
            Assert.That(caller.CallsCut, Is.EqualTo(2));
            yield return GameTime(c.TellSeconds + 0.2f);
            Assert.That(rc.Called, Is.Empty, "none came");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheSwitchOff_AndTheTrialsLineOff_NoCaller_EveryRoomAsOn136()
        {
            int floor = FloorWithACaller();
            foreach (bool byTheLine in new[] { false, true })
            {
                if (byTheLine) RoomTrialSettings.SetCallers(false, keep: false);
                GeneratedLevel level = Build(floor, v => { if (!byTheLine) v.Callers = false; });
                yield return Frames(2);
                Assert.That(_levels.CallersOnThisFloor, Is.False);
                Assert.That(_levels.RoomCallerList, Is.Empty);
                Assert.That(level.Enemies.Any(e => e != null && e.Kind == EnemyKind.Caller), Is.False, "no caller");
                foreach (LevelRoom r in level.Layout.Rooms)
                    if (level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> list))
                        Assert.That(list.Count, Is.EqualTo(r.EnemyCount), r.Letter + "" + r.Order + ": every enemy the floor placed");
                if (byTheLine) Assert.That(_levels.Values.Callers, Is.True, "the values' own switch kept");
                RoomTrialSettings.SetCallers(true, keep: false);
            }
            Assert.That(RoomTrialSettings.LineWords(SettingLine.Callers), Is.EqualTo("on"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheRecorder_WritesEachCall_AndTheCallersEnd_TheKindItsOwn()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeCallersTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            int floor = FloorWithACaller();
            GeneratedLevel level = Build(floor);
            yield return Frames(2);
            GeneratedLevels.RoomCaller rc = _levels.RoomCallerList[0];
            EnemyCaller caller = rc.Caller;
            CallerValues c = _levels.Values.CallerRules;
            yield return InAndToTheMiddle(level, rc.Room);
            yield return Until(() => caller.CallsMade >= 1, c.FirstCallAfter + c.TellSeconds + 1f);
            Assert.That(caller.CallsMade, Is.EqualTo(1));
            caller.Body.TakeHit(1);
            yield return Frames(2);
            caller.Body.TakeHit(100000000);
            yield return Frames(3);
            List<SessionEvent> calls = _recorder.Events.Where(e => e.Type == SessionEventTypes.CallerCall).ToList();
            Assert.That(calls, Is.Not.Empty);
            SessionEvent came = calls.First(e => e.Name == "came");
            Assert.That(int.Parse(came.ExtraValue("came"), Inv), Is.EqualTo(rc.Called.Count));
            Assert.That(came.ExtraValue("alive"), Is.EqualTo("0"));
            Assert.That(came.ExtraValue("call"), Is.EqualTo("1"));
            Assert.That(float.Parse(came.ExtraValue("since"), Inv), Is.InRange(c.FirstCallAfter - 0.1f, c.FirstCallAfter + 0.5f));
            Assert.That(came.ExtraValue("ids").Split(';').Length, Is.EqualTo(rc.Called.Count));
            SessionEvent end = _recorder.Events.Single(e => e.Type == SessionEventTypes.CallerEnd);
            Assert.That(end.Name, Is.EqualTo("died"));
            Assert.That(int.Parse(end.ExtraValue("fell"), Inv), Is.EqualTo(rc.Called.Count), "every one of its called fell with it");
            Assert.That(end.ExtraValue("first_hit"), Is.Not.Empty);
            Assert.That(float.Parse(end.ExtraValue("died"), Inv), Is.GreaterThan(float.Parse(end.ExtraValue("first_hit"), Inv)));
            Assert.That(end.ExtraValue("gave_way"), Is.EqualTo(rc.GaveWay.ToString(Inv)));
            Assert.That(_recorder.Events.Any(e => e.Type == SessionEventTypes.CallerCall && e.ExtraValue("caller") == caller.Id.ToString(Inv)), Is.True);
            SessionEvent start = _recorder.Events.First(e => e.Type == SessionEventTypes.SessionStart);
            Assert.That(start.ExtraValue("callers"), Is.EqualTo("true"));
            GeneratedLevels.RoomWaves w = _levels.WavesOf(rc.Room);
            if (w != null)
            {
                SessionEvent one = _recorder.Events.FirstOrDefault(e => e.Type == SessionEventTypes.RoomWave && e.ExtraValue("wave") == "1" && e.ExtraValue("order") == rc.Room.Order.ToString(Inv));
                Assert.That(one, Is.Not.Null);
                Assert.That(one.ExtraValue("kinds"), Does.Contain("caller 1"), "the caller as its own kind in wave 1's kinds");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheCallersStep_AllocatesNothing_WhileNothingChanges()
        {
            int floor = FloorWithACaller();
            GeneratedLevel level = Build(floor);
            yield return Frames(2);
            for (int i = 0; i < 10; i++) _levels.TickRoomCallersForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomCallersForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "asleep: nothing");
            GeneratedLevels.RoomCaller rc = _levels.RoomCallerList[0];
            yield return InAndToTheMiddle(level, rc.Room);
            for (int i = 0; i < 10; i++) _levels.TickRoomCallersForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomCallersForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "fighting, no call: nothing");
        }
    }
}
