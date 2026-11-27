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
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Two waves in the game (work order 136; Kristian's ticks of 2026-11-25: waves "Ja, i de fleste rom", the next wave "Når halve første
    /// er død"), in the ControlRoom scene through <see cref="GeneratedLevels"/> on floor 2 of a run seed built alone: a room of two waves
    /// holds its partner out of the game until half of its lead is dead, counted alive (the doors stay shut, the run box counts it); then
    /// each partner gets a disc for a second at a point 7 to 11 m from her, on his screen, never below her, and stands there awake (a
    /// shooter's or a caster's grace from its arrival); a wave that finds no point waits and comes later; the recorder writes the waves;
    /// with the switch off, or the Trials line off, the room wakes whole as before; the waves' step allocates nothing while nothing changes.
    /// Nobody has played it: these are the rule's own checks.
    /// </summary>
    public class RoomWavesPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run
        const int FloorNumber = 2;

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
            RoomTrialSettings.SetWaves(true, keep: false);
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
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds = 5f)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (!condition() && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        GeneratedLevel Build(Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, FloorNumber), FloorNumber);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;

        /// <summary>The first room of the floor with two waves of a line-up (not the full room).</summary>
        GeneratedLevels.RoomWaves TwoWaveRoom() =>
            _levels.RoomWaveList.FirstOrDefault(w => w.Count == 2 && w.Room.LineUp != null && !w.Room.LineUp.Full);

        /// <summary>She stands past the room's line (it wakes), the doors shut behind her; then in the room's middle, the camera with her.</summary>
        IEnumerator InAndToTheMiddle(GeneratedLevel level, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            yield return Until(() => fight.On, 2f);
            Assert.That(fight.On, Is.True, "past the line: the room woke");
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            yield return Until(() => d == null || d.Shut, 3f);
            _player.Teleport(OnNavMesh(level.Middle(room)));
            yield return GameTime(0.6f); // the camera follows her
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ARoomOfTwoWaves_ThePartnerOutUntilHalfTheLeadIsDead_ThenADiscAPoint_AndTheDoorsShutUntilTheLastIsDead()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            Assert.That(_levels.WavesOnThisFloor, Is.True, "the switch on");
            GeneratedLevels.RoomWaves w = TwoWaveRoom();
            Assert.That(w, Is.Not.Null, "floor " + FloorNumber + " has a room of two waves");
            LevelRoom room = w.Room;
            List<Enemy> lead = w.Waves[0], partner = w.Waves[1];
            Assert.That(lead.All(e => e.Kind == room.LineUp.Lead), Is.True, "wave 1 is the lead (" + room.LineUp.Name + ")");
            Assert.That(partner.All(e => e.Kind == room.LineUp.Partner), Is.True, "wave 2 is the partner");
            Assert.That(lead.Count + partner.Count, Is.EqualTo(level.EnemiesByRoom[room.Order].Count), "the waves divide the room, none added");
            Assert.That(partner.All(e => !e.gameObject.activeSelf && e.WaitsForItsWave && !e.IsDead), Is.True, "the partner out of the game, alive");
            Assert.That(lead.All(e => e.gameObject.activeSelf), Is.True, "the lead stands where the floor put it");

            yield return InAndToTheMiddle(level, room);
            Assert.That(w.Started, Is.EqualTo(1), "the room woke: wave 1");
            Assert.That(lead.All(e => e.IsAwake), Is.True, "the lead woke at the line, all at once");
            Assert.That(partner.All(e => !e.gameObject.activeSelf), Is.True, "the partner not yet");
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            Assert.That(d != null && d.Shut, Is.True, "the doors shut");
            Assert.That(_levels.EnemiesLeftInRoom, Is.EqualTo(lead.Count + partner.Count), "the run box counts the wave to come");

            int needed = RoomWaveRules.DeadFor(lead.Count, _levels.Values.WaveRules);
            Kill(lead.Take(needed - 1));
            yield return Frames(3);
            Assert.That(w.Started, Is.EqualTo(1), "one short of half: not yet");
            Kill(lead.Skip(needed - 1).Take(1));
            yield return Until(() => w.Started == 2, 1f);
            Assert.That(w.Started, Is.EqualTo(2), "half of the lead dead: wave 2 starts");
            float startedAt = Time.time;
            Vector3 her = _player.Position;
            Assert.That(w.Arriving.Count, Is.GreaterThan(0), "its first enemies have points");
            Camera cam = Camera.main;
            Vector3 up = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            RoomWaveValues rules = _levels.Values.WaveRules;
            foreach (GeneratedLevels.WaveArrival a in w.Arriving.ToList())
            {
                Vector3 to = a.At - her;
                to.y = 0f;
                Assert.That(to.magnitude, Is.InRange(rules.Nearest - 0.35f, rules.Farthest + 0.35f), "7 to 11 m from her (the NavMesh's sample a hair off)");
                Assert.That(Vector3.Dot(to, up), Is.GreaterThan(-0.35f), "never below her on his screen");
                Assert.That(RangedOpening.OnScreen(a.At, 0f), Is.True, "on his screen");
                Assert.That(a.Disc != null && a.Disc.enabled, Is.True, "its disc on the ground");
                Assert.That(a.Enemy.gameObject.activeSelf, Is.False, "it cannot be hit and does nothing during the disc's second");
                Assert.That(level.RoomAt(a.At), Is.SameAs(room), "in the room");
            }
            List<GeneratedLevels.WaveArrival> first = w.Arriving.ToList();
            yield return GameTime(rules.DiscSeconds + 0.1f);
            foreach (GeneratedLevels.WaveArrival a in first)
            {
                Assert.That(a.Disc, Is.Null, "the disc gone");
                Assert.That(a.Enemy.gameObject.activeSelf && !a.Enemy.WaitsForItsWave, Is.True, "it stands there");
                Assert.That(a.Enemy.IsAwake, Is.True, "awake");
                Assert.That(Vector3.Distance(a.Enemy.Home, a.At), Is.LessThan(0.01f), "its home is where it arrived");
                RangedOpening o = a.Enemy is EnemyShooter s ? s.Opening : a.Enemy is EnemyCaster c ? c.Opening : null;
                if (o != null) Assert.That(o.ArrivedAt, Is.InRange(startedAt + rules.DiscSeconds - 0.2f, startedAt + rules.DiscSeconds + 0.2f), "a ranged arrival's grace from its arrival");
            }
            yield return Until(() => !w.StillArriving, 5f);
            Assert.That(w.StillArriving, Is.False, "every one of the wave arrived");

            Kill(lead);
            yield return Frames(5);
            Assert.That(d.Shut, Is.True, "the lead all dead, the partner alive: shut");
            Kill(partner);
            yield return Until(() => !d.Shut, 2f);
            Assert.That(d.Shut, Is.False, "the last wave's last dead: the doors open");
            Assert.That(_levels.FightOf(room).On, Is.False, "the fight over");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AWaveWithNoGoodPoint_Waits_AndComesWhenOneIs_TheDoorsShutMeanwhile()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            GeneratedLevels.RoomWaves w = TwoWaveRoom();
            Assert.That(w, Is.Not.Null);
            yield return InAndToTheMiddle(level, w.Room);
            RoomWaveValues rules = _levels.Values.WaveRules;
            rules.Nearest = 100f; // no point of the room is that far from her
            rules.Farthest = 101f;
            Kill(w.Waves[0].Take(RoomWaveRules.DeadFor(w.Waves[0].Count, rules)));
            yield return Until(() => w.Started == 2, 1f);
            Assert.That(w.Started, Is.EqualTo(2));
            Assert.That(w.Arriving, Is.Empty, "nobody arrives off the band");
            Assert.That(w.Waiting.Count, Is.EqualTo(w.Waves[1].Count), "all of it waits");
            Kill(w.Waves[0]);
            yield return GameTime(rules.WaitSeconds * 3f);
            Assert.That(w.WaitTries, Is.GreaterThanOrEqualTo(2), "tried again every " + rules.WaitSeconds + " s");
            Assert.That(_levels.DoorsOf(w.Room).Shut, Is.True, "the doors stay shut while a wave waits");
            Assert.That(_levels.FightOf(w.Room).On, Is.True, "the fight goes on");
            rules.Nearest = 7f;
            rules.Farthest = 11f;
            yield return Until(() => w.Arriving.Count > 0, rules.WaitSeconds + 0.5f);
            Assert.That(w.Arriving.Count, Is.GreaterThan(0), "at the next try, points");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheRecorder_WritesEachWave_AndTheRoomsFightItsWaves()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeWavesTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = Build();
            yield return Frames(2);
            GeneratedLevels.RoomWaves w = TwoWaveRoom();
            yield return InAndToTheMiddle(level, w.Room);
            SessionEvent fight = _recorder.Events.LastOrDefault(e => e.Type == SessionEventTypes.RoomFight);
            Assert.That(fight, Is.Not.Null);
            Assert.That(fight.ExtraValue("waves"), Is.EqualTo("2"));
            Assert.That(fight.ExtraValue("woke"), Is.EqualTo(w.Waves[0].Count.ToString()), "only the lead woke");
            Kill(w.Waves[0].Take(RoomWaveRules.DeadFor(w.Waves[0].Count, _levels.Values.WaveRules)));
            yield return Until(() => w.Started == 2, 1f);
            List<SessionEvent> waves = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomWave).ToList();
            Assert.That(waves.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(waves[0].Name, Is.EqualTo("start"));
            Assert.That(waves[0].ExtraValue("wave"), Is.EqualTo("1"));
            Assert.That(waves[0].ExtraValue("before_alive"), Is.EqualTo(""));
            SessionEvent two = waves.First(e => e.ExtraValue("wave") == "2");
            Assert.That(two.ExtraValue("waves"), Is.EqualTo("2"));
            Assert.That(two.ExtraValue("kinds"), Does.StartWith(EnemyKinds.Word(w.Room.LineUp.Partner)));
            Assert.That(int.Parse(two.ExtraValue("before_alive")), Is.EqualTo(w.Waves[0].Count - RoomWaveRules.DeadFor(w.Waves[0].Count, _levels.Values.WaveRules)));
            Assert.That(float.Parse(two.ExtraValue("since"), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThan(0f));
            string[] arrivals = two.ExtraValue("arrivals").Split(';');
            Assert.That(arrivals.Length, Is.EqualTo(w.Arriving.Count));
            foreach (string a in arrivals) Assert.That(a.Split('|').Length, Is.EqualTo(3), "metres|x|y: " + a);
            SessionEvent start = _recorder.Events.First(e => e.Type == SessionEventTypes.SessionStart);
            Assert.That(start.ExtraValue("waves"), Is.EqualTo("true"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSwitchOff_AndTheTrialsLineOff_TheRoomWakesWhole_AsBefore()
        {
            foreach (bool byTheLine in new[] { false, true })
            {
                if (byTheLine) RoomTrialSettings.SetWaves(false, keep: false);
                GeneratedLevel level = Build(v => { if (!byTheLine) v.Waves = false; });
                yield return Frames(2);
                Assert.That(_levels.WavesOnThisFloor, Is.False);
                Assert.That(_levels.RoomWaveList, Is.Empty);
                Assert.That(level.Enemies.All(e => e == null || e.gameObject.activeSelf), Is.True, "every enemy stands in its room");
                if (byTheLine) Assert.That(_levels.Values.Waves, Is.True, "the values' own switch kept");
                LevelRoom room = level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat && LevelLineUps.Fills(r) && !r.Elite).Skip(1).First();
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
                yield return Until(() => _levels.FightOf(room).On, 2f);
                yield return Frames(3);
                Assert.That(level.EnemiesByRoom[room.Order].All(e => e.IsAwake), Is.True, "every enemy of the room woke at once");
                RoomTrialSettings.SetWaves(true, keep: false);
            }
            Assert.That(RoomTrialSettings.LineWords(SettingLine.Waves), Is.EqualTo("on"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheThreeWaveRoom_TheLeadInTwoHalves_ThePartnerBetween()
        {
            int found = 0;
            foreach (ulong run in new[] { RunSeed, 0x13613613UL, 0x5A5A5A5AUL })
            {
                for (int floor = 2; floor <= 4; floor++)
                {
                    _levels.Values = new LevelValues();
                    GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(run, floor), floor);
                    yield return Frames(2);
                    foreach (GeneratedLevels.RoomWaves w in _levels.RoomWaveList.Where(x => x.Count == 3))
                    {
                        found++;
                        LineUp u = w.Room.LineUp;
                        Assert.That(w.Waves[0].All(e => e.Kind == u.Lead) && w.Waves[2].All(e => e.Kind == u.Lead), Is.True);
                        Assert.That(w.Waves[1].All(e => e.Kind == u.Partner), Is.True);
                        Assert.That(w.Waves[0].Count - w.Waves[2].Count, Is.InRange(0, 1), "the larger half first");
                        LevelRoom last = level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat).OrderBy(r => r.Order).Last();
                        Assert.That(w.Room, Is.SameAs(last), "the floor's last combat room before its exit");
                        Assert.That(w.Waves[2].All(e => !e.gameObject.activeSelf), Is.True);
                    }
                }
            }
            Assert.That(found, Is.GreaterThan(0), "a room of three waves on these floors");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheWavesStep_AllocatesNothing_WhileNoWaveStartsOrArrives()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            for (int i = 0; i < 10; i++) _levels.TickRoomWavesForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomWavesForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "asleep: nothing");
            GeneratedLevels.RoomWaves w = TwoWaveRoom();
            yield return InAndToTheMiddle(level, w.Room);
            for (int i = 0; i < 10; i++) _levels.TickRoomWavesForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomWavesForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "wave 1 fighting: nothing");
        }
    }
}
