using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The pilots of work order 022 (2026-11-20) on tiny cases, in the ControlRoom scene, quick enough for the ordinary
    /// suite: the Turret stands still and kills a spawned pack; the Kiter leaves a telegraph it stands in before the
    /// strike and is not hit; the Ace gets a clean Triple Tap and a perfect Long Shot at least three times out of four; a
    /// pilot walks from a floor's entrance to its exit room with the floor's enemies out of the way; and a pilot holds
    /// nothing but its body and plain values. The practice brute is switched off (it stays as the template spawns copy).
    /// Waits are in game time; no assertion counts on the length of a frame.
    /// </summary>
    public class PilotTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

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
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            if (practice != null) practice.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Record()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikePilotTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
        }

        /// <summary>Ticks the pilot once a frame until the condition holds or that much game time has gone.</summary>
        IEnumerator Fly(Pilot pilot, Func<bool> done, float seconds)
        {
            var eyes = new PilotEyes(_player, _levels);
            var view = new PilotView();
            float until = Time.time + seconds;
            while (!done() && Time.time < until && _health.IsAlive)
            {
                if (_levels.Choice == GeneratedLevels.ChoiceKind.Chest) _levels.PickUpgrade(0);
                eyes.Look(view);
                pilot.Tick(view);
                if (pilot.WantsChest) _levels.OpenChest();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheTurret_StandsStill_AndKillsASpawnedPack()
        {
            _player.EquipStartSkills();
            List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
            // Three of the pack, with a floor's swarmer health (the practice room's own is twice that).
            for (int i = 3; i < pack.Count; i++) Object.Destroy(pack[i].gameObject);
            pack = pack.Take(3).ToList();
            foreach (Enemy e in pack) e.HealthOverride = new LevelValues().SwarmerHealth;
            var body = new PilotBody(_player, _health);
            var turret = new Turret(body);
            // It stands from the moment something is awake: the pack is shot at, and wakes, from where she stands.
            yield return Fly(turret, () => pack.Any(e => e.IsAwake), 5f);
            body.ResetCounts();
            yield return Fly(turret, () => pack.All(e => e.IsDead), 40f);
            Assert.That(pack.All(e => e.IsDead), Is.True, "the pack is dead");
            Assert.That(body.MovingSteers, Is.EqualTo(0), "it never walked while the pack was awake");
            Assert.That(body.DashPresses, Is.EqualTo(0), "it never dashed");
            Assert.That(body.Clicks, Is.GreaterThan(0), "it shot");
        }

        [UnityTest]
        public IEnumerator TheKiter_LeavesATelegraphItStandsIn_BeforeTheStrike_AndIsNotHit()
        {
            _player.EquipStartSkills();
            EnemyGuard guard = _levels.SpawnPractice(EnemyKind.Guard).OfType<EnemyGuard>().Single();
            yield return null;
            Vector3 g = guard.transform.position;
            Vector3 away = new Vector3(Stand.x - g.x, 0f, Stand.z - g.z).normalized;
            _player.Teleport(g + away * 2f);
            yield return null;
            // The guard wakes and winds up its slam with her inside it before the pilot moves at all.
            float until = Time.time + 3f;
            while (guard.State != EnemyState.Windup && Time.time < until) yield return null;
            Assert.That(guard.State, Is.EqualTo(EnemyState.Windup), "the guard winds up");
            EnemyAttackDefinition attack = guard.Attack;
            Vector3 p = _player.Position;
            Assert.That(EnemyBrain.Hits(attack, new GroundPoint(g.x, g.z), new GroundPoint(guard.AimPoint.x, guard.AimPoint.z),
                                        new GroundPoint(p.x, p.z), Placeholders.PlayerRadius), Is.True, "she stands in the telegraph");
            int health = _health.Current;
            var kiter = new Kiter(new PilotBody(_player, _health));
            yield return Fly(kiter, () => guard.Strikes >= 1, 5f);
            Assert.That(guard.Strikes, Is.GreaterThanOrEqualTo(1), "the strike came");
            Assert.That(guard.StrikesLanded, Is.EqualTo(0), "and missed her");
            Assert.That(_health.Current, Is.EqualTo(health), "she took nothing");
        }

        [UnityTest]
        public IEnumerator TheAce_GetsACleanTripleTap_AndAPerfectLongShot_AtLeastThreeTimesOutOfFour()
        {
            _player.EquipStartSkills();
            int longShot = Enumerable.Range(0, _player.Skills.Count).First(s => _player.Skills[s].Name == "Long Shot");
            _player.EquipSkill(longShot);
            Record();
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Guard);
            foreach (Enemy e in made) e.HealthOverride = 1000000; // a target that lasts the test
            yield return null;
            var ace = new Ace(new PilotBody(_player, _health));
            Time.timeScale = 4f; // the rules hold at it (work order 022's check flight); the waits are in game time
            int perfectBefore = _player.PerfectReleases;
            // A Long Shot let go: its skill ends having fired.
            int LongShots() => _recorder.Events.Count(e => e.Type == SessionEventTypes.SkillEnd && e.Name == "Long Shot" && e.Hit == true);
            List<SessionEvent> LastTaps() => _recorder.Events.Where(e => e.Type == SessionEventTypes.RecastPress && e.Name == "Triple Tap" && e.Shot == 3).ToList();
            int perfectAtFour = -1;
            yield return Fly(ace, () =>
            {
                if (perfectAtFour < 0 && LongShots() >= 4) perfectAtFour = _player.PerfectReleases - perfectBefore;
                return perfectAtFour >= 0 && LastTaps().Count >= 4;
            }, 90f);
            Time.timeScale = 1f;
            Assert.That(LongShots(), Is.GreaterThanOrEqualTo(4), "four Long Shots were let go");
            Assert.That(perfectAtFour, Is.GreaterThanOrEqualTo(3), "three of the first four perfect, or more");
            List<SessionEvent> taps = LastTaps().Take(4).ToList();
            Assert.That(taps.Count, Is.EqualTo(4), "four Triple Taps pressed to their last shot");
            Assert.That(taps.Count(e => e.Clean == true), Is.GreaterThanOrEqualTo(3), "three of them clean, or more");
        }

        [UnityTest]
        public IEnumerator APilot_WalksFromTheEntrance_ToTheExitRoom_OnAFloorWithItsEnemiesOutOfTheWay()
        {
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            // The exit room's enemies are switched off rather than removed, so the floor is not finished at once.
            LevelRoom exit = level.Layout.ExitRoom;
            foreach (Enemy e in level.Enemies.ToList())
            {
                if (level.RoomAt(e.transform.position) == exit || level.ExitEnemies.Contains(e)) e.gameObject.SetActive(false);
                else Object.Destroy(e.gameObject);
            }
            yield return null;
            Assert.That(Enemy.All.Count(e => e != null && !e.IsDead && e.GetComponentInParent<GeneratedLevel>() == level), Is.EqualTo(0), "no enemy left to see");
            var kiter = new Kiter(new PilotBody(_player, _health));
            Time.timeScale = 4f;
            yield return Fly(kiter, () => _levels.CurrentRoom == exit, 150f);
            Time.timeScale = 1f;
            Assert.That(_levels.CurrentRoom, Is.SameAs(exit), "she reached the exit room (the pilot was: " + kiter.Mode + ")");
            Assert.That(kiter.Route.First(), Is.EqualTo(0));
            Assert.That(kiter.Route.Last(), Is.EqualTo(exit.Order));
        }

        /// <summary>
        /// By construction (work order 022): a pilot holds its <see cref="PilotBody"/> and values: no field of a pilot is a
        /// Unity object or a part of the game, and the body hands out no reference into the game.
        /// </summary>
        [Test]
        public void APilot_HoldsNothingButItsBody_AndPlainValues()
        {
            foreach (Type t in new[] { typeof(Turret), typeof(Kiter), typeof(Ace) })
            {
                for (Type k = t; k != null && k != typeof(object); k = k.BaseType)
                    foreach (FieldInfo f in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        Assert.That(Allowed(f.FieldType), Is.True, t.Name + "." + f.Name + " is a " + f.FieldType.Name);
            }
            foreach (PropertyInfo p in typeof(PilotBody).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                Assert.That(Allowed(p.PropertyType), Is.True, "PilotBody." + p.Name);
            foreach (MethodInfo m in typeof(PilotBody).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                Assert.That(Allowed(m.ReturnType), Is.True, "PilotBody." + m.Name + " returns " + m.ReturnType.Name);
                foreach (ParameterInfo a in m.GetParameters())
                    Assert.That(Allowed(a.ParameterType.IsByRef ? a.ParameterType.GetElementType() : a.ParameterType), Is.True, "PilotBody." + m.Name);
            }
            foreach (Type t in new[] { typeof(PilotView), typeof(EnemySeen), typeof(RoomSeen), typeof(DoorSeen), typeof(ChestSeen), typeof(PilotSkill) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Assert.That(Allowed(f.FieldType), Is.True, t.Name + "." + f.Name);
        }

        /// <summary>A value, a string, the body, the view, a NavMesh path (a query's answer) or a list of those.</summary>
        static bool Allowed(Type t)
        {
            if (t == typeof(void) || t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(PilotBody) || t == typeof(PilotView)) return true;
            if (t == typeof(UnityEngine.AI.NavMeshPath)) return true;
            if (typeof(Object).IsAssignableFrom(t)) return false;
            if (t.IsGenericType) return t.GetGenericArguments().All(Allowed);
            if (t.IsArray) return Allowed(t.GetElementType());
            if (t.IsValueType) return t.Namespace == null || !t.Namespace.StartsWith("Roguelike", StringComparison.Ordinal) || t.Assembly == typeof(PilotTests).Assembly
                                      || t == typeof(GroundPoint);
            // Reference types of the game (the character, an enemy, the levels, a skill definition) are not allowed.
            return t.Namespace != null && t.Namespace.StartsWith("System", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The flights of work order 022 (2026-11-20): long, so they never run with the ordinary suite. They run only with
    /// the environment variable ROGUELIKE_PILOT_FLIGHTS set to 1 (each is skipped, ignored, without it), and are in the
    /// category PilotFlight. The standard set: each pilot through a whole act from three run seeds, started on floors 3
    /// and 5 with eight skills and full health, and against the act boss at its full health in the practice room. The
    /// check flights: a Kiter against a pack and a shooter at time scale 1 and at the flights' scale.
    /// Results go to <see cref="PilotFlight.Folder"/>: flights.tsv (a line a flight), table.txt and a text file a flight.
    /// </summary>
    [Category("PilotFlight")]
    public class PilotFlightTests
    {
        public const string Switch = "ROGUELIKE_PILOT_FLIGHTS";
        const int Long = 3600000;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            if (Environment.GetEnvironmentVariable(Switch) != "1")
                Assert.Ignore("Pilot flights run only with " + Switch + "=1 (work order 022).");
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            // The practice brute stays on here: a floor's brutes are copies of it, and a copy of a switched-off brute is
            // switched off too. The practice-room flights switch it off themselves (PilotFlight).
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(new Vector3(8f, 0f, -6f));
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (PlaySessionRecorder.Current != null) PlaySessionRecorder.Current.StopRecording();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        IEnumerator Fly(PilotKind pilot, FlightKind kind, string seed, float scale = -1f)
        {
            var flight = new PilotFlight(pilot, kind, seed, _player, _health, _levels, scale);
            yield return flight.Run();
            Assert.That(flight.Line, Is.Not.Empty);
        }

        // The check: the same fight at time scale 1 and at the flights' scale.
        [UnityTest, Timeout(Long)] public IEnumerator Check_Kiter_Scale1() => Fly(PilotKind.Kiter, FlightKind.Check, "pack", 1f);
        [UnityTest, Timeout(Long)] public IEnumerator Check_Kiter_ScaleOfFlights() => Fly(PilotKind.Kiter, FlightKind.Check, "pack");
        [UnityTest, Timeout(Long)] public IEnumerator Check_Turret_Scale1() => Fly(PilotKind.Turret, FlightKind.Check, "pack", 1f);
        [UnityTest, Timeout(Long)] public IEnumerator Check_Turret_ScaleOfFlights() => Fly(PilotKind.Turret, FlightKind.Check, "pack");

        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Act_WALKTHRU() => Fly(PilotKind.Kiter, FlightKind.Act, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Act_RUNNER42() => Fly(PilotKind.Kiter, FlightKind.Act, "RUNNER42");
        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Act_TESTFLY2() => Fly(PilotKind.Kiter, FlightKind.Act, "TESTFLY2");
        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Floor3_WALKTHRU() => Fly(PilotKind.Kiter, FlightKind.Floor3, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Floor5_WALKTHRU() => Fly(PilotKind.Kiter, FlightKind.Floor5, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Kiter_Boss_Practice() => Fly(PilotKind.Kiter, FlightKind.Boss, "practice");

        [UnityTest, Timeout(Long)] public IEnumerator Turret_Act_WALKTHRU() => Fly(PilotKind.Turret, FlightKind.Act, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Turret_Act_RUNNER42() => Fly(PilotKind.Turret, FlightKind.Act, "RUNNER42");
        [UnityTest, Timeout(Long)] public IEnumerator Turret_Act_TESTFLY2() => Fly(PilotKind.Turret, FlightKind.Act, "TESTFLY2");
        [UnityTest, Timeout(Long)] public IEnumerator Turret_Floor3_WALKTHRU() => Fly(PilotKind.Turret, FlightKind.Floor3, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Turret_Floor5_WALKTHRU() => Fly(PilotKind.Turret, FlightKind.Floor5, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Turret_Boss_Practice() => Fly(PilotKind.Turret, FlightKind.Boss, "practice");

        [UnityTest, Timeout(Long)] public IEnumerator Ace_Act_WALKTHRU() => Fly(PilotKind.Ace, FlightKind.Act, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Ace_Act_RUNNER42() => Fly(PilotKind.Ace, FlightKind.Act, "RUNNER42");
        [UnityTest, Timeout(Long)] public IEnumerator Ace_Act_TESTFLY2() => Fly(PilotKind.Ace, FlightKind.Act, "TESTFLY2");
        [UnityTest, Timeout(Long)] public IEnumerator Ace_Floor3_WALKTHRU() => Fly(PilotKind.Ace, FlightKind.Floor3, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Ace_Floor5_WALKTHRU() => Fly(PilotKind.Ace, FlightKind.Floor5, "WALKTHRU");
        [UnityTest, Timeout(Long)] public IEnumerator Ace_Boss_Practice() => Fly(PilotKind.Ace, FlightKind.Boss, "practice");
    }
}
