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
    /// The timed chest in the game (work order 153; Kristian's tick of 2026-11-25, "Tidskisten"), in the ControlRoom scene through
    /// <see cref="GeneratedLevels"/>: on a floor of a run seed built alone, the chest stands in the rules' room at one of its corner places,
    /// on the NavMesh; it is shut and dim, and no key opens it, until the room's own fight is cleared and its doors are open, then it glows
    /// and walking past it opens nothing; the interact key opens it: the doors shut, its wave of the room's own two kinds comes by work order
    /// 136's arrival rule after its discs, at most five at one moment; in a run, its reward is whole until 9 s, falls 1 % a second, freezes at
    /// the last kill, and the same key takes it into the run's Echoes, exactly; her death in its fight gives nothing; no chest in a rehearsal,
    /// in the benchmark, with the switch or the ROOMS line off (every floor then as before, field for field); its step allocates nothing
    /// while its number counts; the game's own saves are never touched. Nobody has played it: these are the rule's own checks.
    /// </summary>
    public class TimedChestPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // RoomFightsPlayTests' run
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string[] s_realPaths;
        static byte[][] s_realBytes;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;
        bool _rehearsalBefore;

        string BetweenRunsFile => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);
        string BuildFile => Path.Combine(_folder, RehearsalValues.FileName);
        string RecordsFile => Path.Combine(_folder, "records.json");

        [OneTimeSetUp]
        public void RememberTheRealSaves()
        {
            s_realPaths = new[] { GeneratedLevels.DefaultBetweenRunsSavePath, StartMenu.DefaultRecordsPath, GeneratedLevels.DefaultRehearsalSavePath };
            s_realBytes = s_realPaths.Select(p => File.Exists(p) ? File.ReadAllBytes(p) : null).ToArray();
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _rehearsalBefore = RehearsalValues.On;
            _folder = Path.Combine(Path.GetTempPath(), "TimedChestPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            GeneratedLevels.BetweenRunsSavePath = BetweenRunsFile; // never the game's own save
            GeneratedLevels.RehearsalSavePath = BuildFile;          // never his stored build
            StartMenu.RecordsPath = RecordsFile;                    // never his records
            BetweenRunsSwitch.Unload();
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
            LevelValues.TimedChests = true;
            TimedChestSettings.Set(true, keep: false);
            RehearsalValues.On = _rehearsalBefore;
            if (PlaySessionRecorder.Current != null) PlaySessionRecorder.Current.StopRecording();
            _recorder = null;
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                if (_health != null) _health.Untouchable = false;
            }
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (_player != null) _player.Modifiers = null;
            GameSettings.Unload();
            BetweenRunsSwitch.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            GeneratedLevels.RehearsalSavePath = null;
            StartMenu.RecordsPath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
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

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 30f;
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

        GeneratedLevel Build(int floor, Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        /// <summary>She stands past the room's line (it wakes); then every enemy of the room is killed, its waves too, until its doors are open.</summary>
        IEnumerator ClearTheRoom(GeneratedLevel level, LevelRoom room)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 0.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            yield return Until(() => fight.On, 2f);
            Assert.That(fight.On, Is.True, "past the line: the room woke");
            for (int i = 0; i < 40 && (fight.On || level.EnemiesByRoom[room.Order].Any(e => e != null && !e.IsDead)); i++)
            {
                Kill(level.EnemiesByRoom[room.Order]);
                yield return GameTime(0.3f);
            }
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            yield return Until(() => d == null || !d.Shut, 3f);
            yield return Frames(2);
            Assert.That(fight.On, Is.False, "the room's own fight cleared");
        }

        /// <summary>She stands beside the chest, on its room's side.</summary>
        IEnumerator BesideIt(GeneratedLevel level, GeneratedLevels.TimedChest t)
        {
            Vector3 middle = level.Middle(t.Room);
            Vector3 toward = new Vector3(middle.x - t.Place.x, 0f, middle.z - t.Place.z);
            _player.Teleport(OnNavMesh(t.Place + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * 1.4f));
            yield return Frames(2);
        }

        /// <summary>The chest's room cleared, she opens it beside it, and steps to the room's middle in the same frame (the camera with her), as she turns to fight.</summary>
        IEnumerator OpenItAndStepIn(GeneratedLevel level, GeneratedLevels.TimedChest t)
        {
            yield return ClearTheRoom(level, t.Room);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable));
            yield return BesideIt(level, t);
            Assert.That(_levels.Interact(), Is.True, "the interact key opens it");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Fighting));
            _player.Teleport(OnNavMesh(level.Middle(t.Room)));
        }

        /// <summary>Its wave, every one of it killed as it stands, until none is left.</summary>
        IEnumerator KillTheWave(GeneratedLevels.TimedChest t, float seconds = 20f)
        {
            float until = Time.time + seconds;
            while (t.State == GeneratedLevels.TimedChestState.Fighting && Time.time < until)
            {
                Kill(t.Wave.Where(e => e != null && e.isActiveAndEnabled));
                yield return GameTime(0.25f);
            }
        }

        /// <summary>A run from its first floor to floor 2: floor 1's reward taken (and the focus choice after it).</summary>
        IEnumerator ARunOnFloor2()
        {
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            _levels.FinishFloorForTests();
            for (int i = 0; i < 8 && _levels.Floor < 2; i++)
            {
                yield return Until(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || _levels.Floor >= 2, 3f);
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FloorReward) _levels.PickReward(0);
                else if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0);
                else if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
                yield return Frames(2);
            }
            Assert.That(_levels.Floor, Is.EqualTo(2), "on floor 2");
            Assert.That(_levels.Earnings, Is.Not.Null, "a run with what lasts between runs on (in memory and in this test's file only)");
            yield return Frames(2);
        }

        // ---------------- Where it stands, and when it can be opened ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheChest_StandsInTheRulesRoom_AtACorner_ShutAndDim_UntilTheRoomIsCleared_ThenItGlows_AndWalkingPastOpensNothing()
        {
            GeneratedLevel level = Build(2);
            yield return Frames(2);
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            Assert.That(t, Is.Not.Null, "floor 2 holds one");
            LevelRoom ruled = TimedChestRules.Room(level.Layout, _levels.Values, _levels.RoomCallerList.Select(c => c.Room).ToList());
            Assert.That(t.Room, Is.SameAs(ruled), "the rules' room");
            Assert.That(TimedChestRules.Places(level.Layout, t.Room, _levels.Values).Any(p => p.DistanceTo(t.At) < 1e-6), Is.True, "one of its corner places");
            Assert.That(Vector3.Distance(new Vector3(t.Place.x, 0f, t.Place.z), level.ToWorld(t.At)), Is.LessThan(0.35f), "where the rule put it");
            Assert.That(NavMesh.SamplePosition(t.Place, out NavMeshHit _, 0.35f, NavMesh.AllAreas) || t.Object.GetComponent<NavMeshObstacle>() != null, Is.True, "on the NavMesh (its own place carved out of it)");
            Assert.That(level.RoomAt(t.Place), Is.SameAs(t.Room));
            Assert.That(t.Object, Is.Not.Null);
            Assert.That(t.Object.GetComponent<Collider>(), Is.Not.Null, "it stands in her way as the treasure chest does");
            Assert.That(t.Whole, Is.EqualTo(TimedChestRules.Whole(2, _levels.Values.TimedChestRules)));
            Assert.That(level.EnemiesByRoom[t.Room.Order].Count, Is.EqualTo(t.Room.EnemyCount), "it changes nothing of its room's fight");
            Renderer r = t.Object.GetComponentInChildren<Renderer>();
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Shut));
            TimedChestValues cv = _levels.Values.TimedChestRules;
            if (r != null) Assert.That(r.sharedMaterial.color.r, Is.EqualTo(cv.DimShade).Within(1e-4), "shut: its shade");
            if (r != null && cv.ShutLight > 0f) Assert.That(r.sharedMaterial.GetColor("_EmissionColor").b, Is.GreaterThan(0f), "shut: its shape lit faintly, uncoloured");
            Assert.That(t.Pool, Is.Null, "shut: no pool of light");
            // The cave rule owns the chest: it belongs to its room (the doors' way for a renderer made after the floor).
            if (level.Cave != null)
                foreach (Renderer cr in t.Object.GetComponentsInChildren<Renderer>(true))
                    Assert.That(level.Cave.OwnersOf(cr), Is.EquivalentTo(new[] { t.Room.Order }), "the cave rule owns " + cr.name);

            // Beside it before the room is cleared: no key opens it.
            yield return BesideIt(level, t);
            Assert.That(_levels.TimedChestInReach(), Is.Null);
            Assert.That(_levels.UseTimedChest(), Is.False, "shut until the room's own fight is cleared");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Shut));

            yield return ClearTheRoom(level, t.Room);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable), "the room cleared and its doors open: it can be opened");
            if (r != null) Assert.That(r.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True, "it glows faintly");
            if (r != null) Assert.That(r.sharedMaterial.color.r, Is.EqualTo(1f).Within(1e-4));
            Assert.That(t.Pool, Is.Not.Null, "it stands in its pool of warm light");
            Assert.That(t.Pool.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x, Is.EqualTo(cv.PoolReach).Within(0.05f), "the pool's radius");
            Assert.That(t.Pool.GetComponentInParent<Light>(), Is.Null, "a pool of the mood's kind: no light component of its own");
            Assert.That(t.Object.GetComponentsInChildren<Light>(true), Is.Empty);
            if (level.Mood != null) Assert.That(t.Pool.transform.parent, Is.SameAs(level.Mood.Of(t.Room.Order).Root.transform), "the room mood owns it: under its room's root");
            if (level.Cave != null) Assert.That(level.Cave.OwnersOf(t.Pool), Is.EquivalentTo(new[] { t.Room.Order }), "the cave rule owns it");
            if (level.Cave != null) Assert.That(t.Pool.forceRenderingOff, Is.False, "drawn: she is in its room");

            // Walking past it opens nothing: she stands beside it, and goes on.
            yield return BesideIt(level, t);
            yield return GameTime(1f);
            Assert.That(_levels.TimedChestInReach(), Is.SameAs(t), "in her reach");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable), "never opened by walking past it");
            _player.Teleport(OnNavMesh(level.Middle(t.Room)));
            yield return GameTime(0.5f);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable), "she may walk on and leave it shut");
            Assert.That(_levels.FightOf(t.Room).On, Is.False);
        }

        // ---------------- Opening it ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator OpeningIt_ShutsTheDoors_AndBringsItsWave_OfTheRoomsOwnTwoKinds_ByTheArrivalRule()
        {
            GeneratedLevel level = Build(2);
            yield return Frames(2);
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            TimedChestRules.WaveCounts(level.Layout, t.Room, _levels.Values.TimedChestRules, out int leads, out int partners);
            TimedChestRules.WaveKinds(t.Room, out EnemyKind lead, out EnemyKind partner);
            yield return OpenItAndStepIn(level, t);
            Vector3 her = _player.Position;

            Assert.That(t.Wave.Count, Is.EqualTo(leads + partners), "its named count");
            Assert.That(t.Wave.Count(e => e.Kind == lead), Is.EqualTo(leads));
            if (partner != lead) Assert.That(t.Wave.Count(e => e.Kind == partner), Is.EqualTo(partners));
            Assert.That(t.Wave.All(e => e.Kind == lead || e.Kind == partner), Is.True, "the room's own two kinds");
            Assert.That(t.Wave.Any(e => e.IsElite || e.Kind == EnemyKind.Caller), Is.False, "no elite, no caller");
            foreach (Enemy e in t.Wave)
            {
                Assert.That(level.EnemiesByRoom[t.Room.Order], Has.Member(e), "one of the room's own");
                Assert.That(e.Fight, Is.SameAs(_levels.FightOf(t.Room)));
                Assert.That(e.Body.MaxHealth, Is.EqualTo(_levels.Values.HealthOf(e.Kind)), "the floor's health");
            }
            Assert.That(_levels.FightOf(t.Room).On, Is.True, "the room's fight wakes again");
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(t.Room);
            yield return Until(() => d == null || d.Shut, 2f);
            if (d != null) Assert.That(d.Shut, Is.True, "the doors shut behind her");

            // Its arrivals: each disc in view, 7 to 11 m from her, never below her on his screen, at most five at one moment.
            RoomWaveValues rules = _levels.Values.WaveRules;
            yield return Until(() => t.Arriving.Count > 0, 3f);
            Assert.That(t.Arriving.Count, Is.InRange(1, rules.MostAtOnce), "a group of at most " + rules.MostAtOnce);
            Assert.That(_levels.EnemiesLeftInRoom, Is.EqualTo(t.Wave.Count), "\"enemies left\" counts its wave, those to come too");
            Camera cam = Camera.main;
            Vector3 up = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            float most = 0;
            float until = Time.time + 15f, deadline = Time.realtimeSinceStartup + 60f;
            while ((t.Waiting.Count > 0 || t.Arriving.Count > 0) && Time.time < until && Time.realtimeSinceStartup < deadline)
            {
                most = Mathf.Max(most, t.Arriving.Count);
                yield return null;
            }
            Assert.That(t.Waiting, Is.Empty, "every one of its wave came");
            Assert.That(most, Is.LessThanOrEqualTo(rules.MostAtOnce));
            Assert.That(t.Arrived.Count, Is.EqualTo(t.Wave.Count));
            foreach (GeneratedLevels.WaveArrival a in t.Arrived)
            {
                Vector3 to = a.At - her;
                to.y = 0f;
                Assert.That(to.magnitude, Is.InRange(rules.Nearest - 0.35f, rules.Farthest + 0.35f), "7 to 11 m from her");
                Assert.That(Vector3.Dot(to, up), Is.GreaterThan(-0.35f), "never below her on his screen");
                Assert.That(RangedOpening.OnScreen(a.At, 0f), Is.True, "on his screen");
                Assert.That(level.RoomAt(a.At), Is.SameAs(t.Room));
                Assert.That(a.Disc, Is.Null, "its disc gone as it stood");
                Assert.That(Vector3.Distance(a.Enemy.Home, a.At), Is.LessThan(0.05f), "it stood on its disc");
            }
            yield return Frames(2);
            Assert.That(t.Wave.All(e => e.IsAwake), Is.True, "awake as they stand");
            if (d != null) Assert.That(d.Shut, Is.True, "shut while its wave lives");

            yield return KillTheWave(t);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Won));
            if (d != null)
            {
                yield return Until(() => !d.Shut, 3f);
                Assert.That(d.Shut, Is.False, "the doors open as its wave is dead");
            }
        }

        // ---------------- Its reward ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator ItsReward_CountsDown_AndFreezesAtTheLastKill_SheTakesIt_AndHerRunsEchoesRiseByExactlyThat()
        {
            _recorder = PlaySessionRecorder.StartRecording(Path.Combine(_folder, "sessions"));
            yield return ARunOnFloor2();
            GeneratedLevel level = _levels.Level;
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            Assert.That(t, Is.Not.Null, "the run's floor 2 holds one");
            TimedChestValues c = _levels.Values.TimedChestRules;
            yield return OpenItAndStepIn(level, t);
            int before = _levels.Earnings.Total(_levels.MetaValues); // after the room's own clearing paid its own (work order 060)
            Assert.That(_levels.TimedChestEchoes, Is.EqualTo(t.Whole), "whole at the opening");

            // Whole until 9 s; then 1 % a second.
            Time.timeScale = 3f;
            yield return Until(() => Time.time - t.OpenedAt >= c.DecayAfter - 0.5f, 20f);
            Assert.That(_levels.TimedChestShare, Is.EqualTo(1d), "whole until " + c.DecayAfter + " s");
            yield return Until(() => Time.time - t.OpenedAt >= c.DecayAfter + 4.2f, 20f);
            double share = _levels.TimedChestShare;
            Assert.That(share, Is.EqualTo(TimedChestRules.Share(Time.time - t.OpenedAt, c)).Within(1e-9));
            Assert.That(share, Is.InRange(0.95d, 0.97d), "4 s after it began to fall: 4 % less");
            Assert.That(TimedChestRules.Percent(share), Is.EqualTo((int)Math.Round(share * 100)), "the percent shown");
            Assert.That(_levels.TimedChestEchoes, Is.EqualTo(TimedChestRules.Left(t.Whole, TimedChestRules.Percent(share) / 100d, c)), "the Echoes shown are those of the percent shown");
            Assert.That(t.Pool, Is.Not.Null, "its pool through its fight: he sees where the reward is");
            Time.timeScale = 1f;

            // The last kill freezes it.
            yield return KillTheWave(t);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Won), "its wave dead");
            double frozen = _levels.TimedChestShare;
            int left = _levels.TimedChestEchoes;
            Time.timeScale = 3f;
            yield return GameTime(6f);
            Time.timeScale = 1f;
            Assert.That(_levels.TimedChestShare, Is.EqualTo(frozen), "frozen at the last kill");
            Assert.That(_levels.TimedChestEchoes, Is.EqualTo(left));
            Assert.That(frozen, Is.EqualTo(TimedChestRules.Share(t.WonAt - t.OpenedAt, c)).Within(1e-9));

            // She takes it by the same key: her run's Echoes rise by exactly what was left.
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(t.Room);
            yield return Until(() => d == null || !d.Shut, 3f);
            yield return BesideIt(level, t);
            Assert.That(_levels.Interact(), Is.True, "the same key takes it");
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Taken));
            Assert.That(t.Taken, Is.EqualTo(left));
            yield return null;
            Assert.That(t.Pool == null, Is.True, "its pool gone as its reward is taken");
            Assert.That(_levels.Earnings.FromTimedChests, Is.EqualTo(left));
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(before + left), "her run's Echoes rose by exactly that");
            Assert.That(_levels.UseTimedChest(), Is.False, "taken once");
            yield return GameTime(0.5f);
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(before + left));

            // The recorder's line.
            SessionEvent line = _recorder.Events.Single(e => e.Type == TimedChestEvents.Chest);
            Assert.That(line.Name, Is.EqualTo(TimedChestEvents.Taken));
            Assert.That(line.ExtraValue("floor"), Is.EqualTo("2"));
            Assert.That(line.ExtraValue("opened"), Is.EqualTo("true"));
            Assert.That(line.ExtraValue("echoes"), Is.EqualTo(left.ToString(Inv)));
            Assert.That(line.ExtraValue("whole"), Is.EqualTo(t.Whole.ToString(Inv)));
            Assert.That(line.ExtraValue("count"), Is.EqualTo(t.Wave.Count.ToString(Inv)));
            Assert.That(line.ExtraValue("kinds"), Is.EqualTo(t.Kinds));
            Assert.That(double.Parse(line.ExtraValue("share"), Inv), Is.EqualTo(frozen).Within(1e-3));
            Assert.That(float.Parse(line.ExtraValue("clear"), Inv), Is.EqualTo(t.WonAt - t.OpenedAt).Within(0.02f));
            Assert.That(float.Parse(line.ExtraValue("after_clear"), Inv), Is.GreaterThanOrEqualTo(0f));
            Assert.That(_recorder.Events.Any(e => e.Type == SessionEventTypes.MetaEarn && e.Name == "timed chest"), Is.True, "told as Echoes earned");
            Assert.That(SessionSummary.Format(SessionSummary.Compute(_recorder.Events)), Does.Contain("Timed chest (work order 153)"));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator HerDamage_InItsFight_IsCounted_AndHerDeathInIt_GivesNothing()
        {
            _recorder = PlaySessionRecorder.StartRecording(Path.Combine(_folder, "sessions"));
            yield return ARunOnFloor2();
            GeneratedLevel level = _levels.Level;
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            yield return OpenItAndStepIn(level, t);
            int before = _levels.Earnings.Total(_levels.MetaValues); // after the room's own clearing paid its own (work order 060)
            yield return Frames(2);
            _health.Untouchable = false;
            int hurt = _health.TakeHit(7);
            Assert.That(t.Damage, Is.EqualTo(hurt), "her damage taken in its fight");
            _health.TakeHit(100000);
            yield return Frames(3);
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Lost), "her death in its fight");
            Assert.That(t.Taken, Is.EqualTo(0), "nothing");
            Assert.That(_levels.UseTimedChest(), Is.False);
            Assert.That(_levels.Earnings.FromTimedChests, Is.EqualTo(0));
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(before), "nothing from it");
            SessionEvent line = _recorder.Events.Single(e => e.Type == TimedChestEvents.Chest);
            Assert.That(line.Name, Is.EqualTo(TimedChestEvents.Died));
            Assert.That(line.ExtraValue("echoes"), Is.EqualTo("0"));
            Assert.That(int.Parse(line.ExtraValue("damage"), Inv), Is.GreaterThanOrEqualTo(hurt));
            _health.Untouchable = true;
        }

        // ---------------- Where it is not ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator NoChest_InARehearsal()
        {
            RehearsalValues.On = true;
            yield return ARunOnFloor2();
            Assert.That(_levels.TimedChestOfFloor, Is.Not.Null, "the real run's floor 2 holds one");
            _levels.BackToPracticeRoom(); // the run ends: its build is kept (in this test's file)
            yield return Frames(2);
            Assert.That(_levels.StartRehearsal(), Is.Not.Null, "a rehearsal starts");
            yield return Frames(3);
            Assert.That(_levels.InRehearsal, Is.True);
            Assert.That(_levels.TimedChestOfFloor, Is.Null, "a rehearsal has no timed chest");
            Assert.That(_levels.Level.GetComponentsInChildren<Transform>().Any(x => x.name.StartsWith("TimedChest_", StringComparison.Ordinal)), Is.False);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator NoChest_WithTheSwitchOff_NorTheRoomsLineOff_EveryFloorAsBefore_FieldForField()
        {
            foreach (int floor in new[] { 2, 3, 5 })
            {
                GeneratedLevel on = Build(floor);
                yield return Frames(2);
                Assert.That(_levels.TimedChestOfFloor, Is.Not.Null, "floor " + floor + " holds one");
                string Print(GeneratedLevel l) => string.Join("\n", l.Enemies.Where(e => e != null).Select(e => string.Format(Inv, "{0} {1} {2:0.000} {3:0.000} {4}",
                                                         e.name, e.Kind, e.transform.position.x, e.transform.position.z, e.Body.MaxHealth)))
                                                  + "\nprops " + l.Props.Count + " " + l.LargeProps.Count + "\nfights " + _levels.RoomFights.Count;
                string withIt = Print(on);
                foreach (bool byTheLine in new[] { false, true })
                {
                    if (byTheLine) TimedChestSettings.Set(false, keep: false);
                    else LevelValues.TimedChests = false;
                    GeneratedLevel off = Build(floor);
                    yield return Frames(2);
                    Assert.That(_levels.TimedChestOfFloor, Is.Null, "off: no chest");
                    Assert.That(off.GetComponentsInChildren<Transform>().Any(x => x.name.StartsWith("TimedChest_", StringComparison.Ordinal)), Is.False);
                    Assert.That(Print(off), Is.EqualTo(withIt), "floor " + floor + ": every enemy, prop and fight as with it before it is opened, field for field");
                    LevelValues.TimedChests = true;
                    TimedChestSettings.Set(true, keep: false);
                }
            }
            Assert.That(TimedChestSettings.LineWords, Is.EqualTo("on"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NoChest_InTheBenchmark_AndItsSwitchPutBack()
        {
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            yield return Frames(3);
            stage.Keep();
            Assert.That(_levels.TimedChestOfFloor, Is.Null, "the benchmark's floors have none");
            Assert.That(LevelValues.TimedChests, Is.True, "its switch put back");
            stage.End();
        }

        // ---------------- The ROOMS page ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TheRoomsPage_HasItsLine_RightAfterTheElites_OnByDefault_AndItTurnsTheChestOff()
        {
            string stored = PlayerPrefs.HasKey(TimedChestSettings.Key) ? PlayerPrefs.GetInt(TimedChestSettings.Key).ToString(Inv) : null;
            SettingsScreen screen = SettingsScreen.Ensure();
            try
            {
                Assert.That(screen.Open(SettingsFrom.StartMenu), Is.True);
                Assert.That(screen.OpenPage(SettingsPage.Rooms), Is.True);
                List<SettingLine> lines = screen.Lines.ToList();
                int at = lines.IndexOf(SettingLine.TimedChest);
                Assert.That(at, Is.EqualTo(lines.IndexOf(SettingLine.Elites) + 1), "right after the elites, found by its line");
                Assert.That(screen.RowNames[at], Is.EqualTo("Timed chest"));
                Assert.That(screen.RowValues[at], Is.EqualTo("on"), "on by default");
                Assert.That(screen.Select(at) && screen.Change(1), Is.True);
                Assert.That(TimedChestSettings.Chosen, Is.False);
                Assert.That(screen.RowValues[at], Is.EqualTo("off"));
                Assert.That(PlayerPrefs.GetInt(TimedChestSettings.Key, -1), Is.EqualTo(0), "kept");
                Build(2);
                yield return Frames(2);
                Assert.That(_levels.TimedChestOfFloor, Is.Null, "off on the page: the floors set up after it hold none");
                TimedChestSettings.Set(true, keep: false);
                Assert.That(TimedChestSettings.LineWords, Is.EqualTo("on"), "and on again");
                screen.Close();
            }
            finally
            {
                if (stored == null) PlayerPrefs.DeleteKey(TimedChestSettings.Key);
                else PlayerPrefs.SetInt(TimedChestSettings.Key, int.Parse(stored, Inv));
                PlayerPrefs.Save();
                Object.Destroy(screen.gameObject);
            }
        }

        /// <summary>The producer's round 2: the HUD's line in the boss's name's size, under the top plate at its left, clear of it.</summary>
        [Test]
        public void TheHudLine_StandsUnderTheTopPlate_AtItsLeft_InTheHudsNameSize()
        {
            InterfaceValues v = InterfaceStyle.V;
            Rect line = _levels.TimedChestHudLineForTests();
            Assert.That(line.height, Is.EqualTo(v.BossNameSize * 1.6f).Within(1e-3f), "the boss's name's size, not a prompt's");
            Assert.That(v.BossNameSize, Is.GreaterThan(v.PromptSize));
            Assert.That(line.x, Is.EqualTo(v.HealthPlateX), "at the plate's left");
            if (v.TopPlate) Assert.That(line.y, Is.GreaterThanOrEqualTo(InterfacePlate.Whole(v).Bottom + 1f), "under the plate, clear of it");
            Assert.That(line.y + line.height, Is.LessThan(v.LayoutHeight * 0.25f), "at the top of the screen");
        }

        // ---------------- Nothing made a frame ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator ItsStep_AllocatesNothing_WhileItsNumberCounts()
        {
            GeneratedLevel level = Build(2);
            yield return Frames(2);
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            for (int i = 0; i < 10; i++) _levels.TickTimedChestForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickTimedChestForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "shut: nothing");
            yield return OpenItAndStepIn(level, t);
            yield return Until(() => t.Waiting.Count == 0 && t.Arriving.Count == 0, 15f);
            Assert.That(t.Waiting, Is.Empty);
            for (int i = 0; i < 10; i++) _levels.TickTimedChestForTests();
            int echoes = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    _levels.TickTimedChestForTests();
                    echoes += _levels.TimedChestEchoes;
                }
            }, AllocExt.AllocatingGCMemory(Is.Not), "its wave standing, its number counting: nothing");
            Assert.That(echoes, Is.GreaterThan(0));
        }

        /// <summary>Last by name: after every other test of the class, the game's own saves are as they were.</summary>
        [Test]
        public void Z_TheGamesOwnSaves_AreNeverTouchedByTheseTests()
        {
            for (int i = 0; i < s_realPaths.Length; i++)
            {
                byte[] now = File.Exists(s_realPaths[i]) ? File.ReadAllBytes(s_realPaths[i]) : null;
                Assert.That(now == null, Is.EqualTo(s_realBytes[i] == null), s_realPaths[i]);
                if (now != null) Assert.That(now, Is.EqualTo(s_realBytes[i]), s_realPaths[i]);
            }
        }
    }
}
