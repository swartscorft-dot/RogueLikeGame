using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The engine's assert "Converting invalid MinMaxAABB" (work order 061), seen since work order 044 (rooms with a mood) now
    /// and then in the first frames after a floor was built, on whichever floor-building test was unlucky: about one floor in
    /// 600, and one mood made again in 500, always in the engine's particle update (PreLateUpdate.ParticleSystemBeginUpdateAll,
    /// on the main thread) of the frame of her first tick. The mood's particle systems were added to active objects, so each
    /// woke with the engine's defaults (playing on awake, in local space) before it was stopped and set up; the dust and the
    /// fog were both needed. <see cref="RoomMood"/> now makes each system on an inactive object and turns it on once set up.
    ///
    /// Here floors are built, or the mood made again from the test's coroutine as a floor's build makes it, again and again
    /// in one run, the mood's particle systems on seeds of the test's own (<see cref="RoomMood.ParticleSeedForTests"/>), and the
    /// builds whose frames logged the assert are counted, not failed at the first. The suite's test is the small form, a few
    /// seconds: the seeds that asserted before the fix, and a handful of floors. The large counts are the [Explicit] proofs.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames, and the frame after a build is made
    /// 0.13 s long (Time.captureDeltaTime, put back after), as the frame after a floor's build is.
    /// </summary>
    public class InvalidBoundsTests
    {
        const string Message = "Converting invalid MinMaxAABB";
        const ulong Seed = 0x5EED5UL;
        const float BuildFrame = 0.13f;
        const int Frames = 4;
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        // The proofs' counts (work order 061), for one's own runs: before the fix the mood made again asserted 26 times in
        // 15000 (counted seeds), floors twice in 1190.
        const int ProofRebuilds = 15000;
        const int ProofAutomaticRebuilds = 10000;
        const int ProofFloors = 3000;
        const int ProofAutomaticFloors = 1500;

        // The particle seeds whose build asserted before the fix (counted seeds 1 + n): floors (5 and 1 in turn) ...
        static readonly (int floor, uint seed)[] s_floorSeeds = { (5, 723u), (1, 920u) };
        // ... and the mood made again on floor 5.
        static readonly uint[] s_rebuildSeeds =
        {
            27, 288, 308, 365, 392, 431, 508, 586, 667, 1185, 1191, 1199, 1211, 1262, 1349, 1387, 1490, 1584, 1620, 1687, 1750,
            1797, 1868, 1884, 1971, 2071, 2135, 2235, 2333, 2338, 2557, 2618, 2732, 2756,
        };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        PlayerLoopSystem _savedLoop;
        static int s_asserts;
        static int s_mainThread;
        static volatile string s_phase = "";
        static readonly List<string> s_phases = new List<string>();

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
            _levels = GeneratedLevels.Ensure();
            s_asserts = 0;
            s_mainThread = Thread.CurrentThread.ManagedThreadId;
            lock (s_phases) s_phases.Clear();
            Application.logMessageReceivedThreaded += OnLog;
            _savedLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(Marked(_savedLoop));
        }

        [TearDown]
        public void CleanUp()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            if (_savedLoop.subSystemList != null) PlayerLoop.SetPlayerLoop(_savedLoop);
            LogAssert.ignoreFailingMessages = false;
            RoomMood.ParticleSeedForTests = 0;
            Time.captureDeltaTime = 0f;
            if (_levels != null)
            {
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (message == null || !message.Contains(Message)) return;
            bool main = Thread.CurrentThread.ManagedThreadId == s_mainThread;
            lock (s_phases) s_phases.Add(s_phase + (main ? "" : " (another thread)"));
            Interlocked.Increment(ref s_asserts);
        }

        // Where in the frame the assert is logged: a marker before each step of the player loop names the step that runs.
        static PlayerLoopSystem Marked(PlayerLoopSystem root)
        {
            if (root.subSystemList == null) return root;
            var top = new PlayerLoopSystem[root.subSystemList.Length];
            for (int i = 0; i < top.Length; i++)
            {
                PlayerLoopSystem phase = root.subSystemList[i];
                if (phase.subSystemList != null)
                {
                    var subs = new List<PlayerLoopSystem>();
                    string outer = phase.type != null ? phase.type.Name : "?";
                    foreach (PlayerLoopSystem s in phase.subSystemList)
                    {
                        string name = outer + "." + (s.type != null ? s.type.Name : "?");
                        subs.Add(new PlayerLoopSystem { type = typeof(InvalidBoundsTests), updateDelegate = () => s_phase = name });
                        subs.Add(s);
                    }
                    string end = outer + ".(after the last)";
                    subs.Add(new PlayerLoopSystem { type = typeof(InvalidBoundsTests), updateDelegate = () => s_phase = end });
                    phase.subSystemList = subs.ToArray();
                }
                top[i] = phase;
            }
            root.subSystemList = top;
            return root;
        }

        static string Phases()
        {
            lock (s_phases) return string.Join("; ", s_phases.GroupBy(p => p).Select(g => g.Key + " x" + g.Count()));
        }

        static string F(float f) => f.ToString("0.###", s_Inv);
        static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

        /// <summary>What the mood's air is doing now.</summary>
        static string Dump(RoomMood mood)
        {
            var sb = new StringBuilder();
            if (mood == null) return sb.ToString();
            void Ps(string what, ParticleSystem ps)
            {
                if (ps == null) return;
                sb.Append("  ").Append(what).Append(" seed ").Append(ps.randomSeed).Append(" n ").Append(ps.particleCount)
                  .Append(" playing ").Append(ps.isPlaying).Append(" t ").Append(F(ps.time)).Append(" at ").Append(V(ps.transform.position)).Append('\n');
            }
            Ps("dust", mood.Dust);
            foreach (RoomMood.RoomParts p in mood.Rooms)
            {
                Ps("fog " + p.Order, p.Fog);
                for (int k = 0; k < p.Embers.Count; k++) Ps("embers " + p.Order + "." + k, p.Embers[k]);
            }
            return sb.ToString();
        }

        static string Memory() =>
            string.Format(s_Inv, "allocated {0} MB, meshes {1}, materials {2}, particle systems {3}",
                          Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024), Resources.FindObjectsOfTypeAll<Mesh>().Length,
                          Resources.FindObjectsOfTypeAll<Material>().Length, Resources.FindObjectsOfTypeAll<ParticleSystem>().Length);

        sealed class Tally
        {
            public int Builds, Asserted;
            public readonly List<string> Hits = new List<string>();
            public override string ToString() => Builds + " builds, " + Asserted + " asserted" + (Hits.Count > 0 ? ": " + string.Join("; ", Hits.Take(20)) + "; where: " + Phases() : "");
        }

        /// <summary>
        /// Counts the asserts of the next <see cref="Frames"/> frames, the first <see cref="BuildFrame"/> long; a build whose
        /// frames logged one is a hit.
        /// </summary>
        IEnumerator Watch(string label, string what, int before, RoomMood mood, Tally t)
        {
            Time.captureDeltaTime = BuildFrame;
            for (int f = 1; f <= Frames; f++)
            {
                yield return null;
                Time.captureDeltaTime = 0f;
            }
            t.Builds++;
            if (s_asserts > before)
            {
                t.Asserted++;
                t.Hits.Add(what);
                Debug.Log("INVALIDBOUNDS hit " + label + " " + what + "; where: " + Phases() + "\n" + Dump(mood));
            }
        }

        /// <summary>Floors built again and again, each with the particle seed and floor given (seed 0: automatic).</summary>
        IEnumerator Floors(string label, int builds, Func<int, uint> seedOf, Func<int, int> floorOf, Tally t)
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.Log("INVALIDBOUNDS " + label + " starts: " + Memory());
            for (int i = 0; i < builds; i++)
            {
                uint seed = seedOf(i);
                int floor = floorOf(i);
                RoomMood.ParticleSeedForTests = seed;
                _levels.Values = new LevelValues();
                int before = s_asserts;
                GeneratedLevel level = _levels.Build(Seed, floor);
                Assert.That(level, Is.Not.Null);
                yield return Watch(label, "build " + i + " floor " + floor + " seed " + seed, before, level.Mood, t);
                if (i % 500 == 499)
                {
                    Debug.Log("INVALIDBOUNDS " + label + " after " + (i + 1) + ": " + t.Asserted + " asserted; " + Memory());
                    Assert.That(Profiler.GetTotalAllocatedMemoryLong(), Is.LessThan(3L * 1024 * 1024 * 1024), "the hunt must not grow in memory");
                }
            }
            RoomMood.ParticleSeedForTests = 0;
            LogAssert.ignoreFailingMessages = false;
            Debug.Log("INVALIDBOUNDS " + label + " SUMMARY: " + t + "\n" + Memory());
        }

        /// <summary>
        /// The mood alone made again and again on floor 5 from the test's coroutine, as a floor's build makes it (after the
        /// frame's updates, so its systems meet the engine's particle update once before her first tick), each with the
        /// particle seed given (0: automatic).
        /// </summary>
        IEnumerator Rebuilds(string label, int builds, Func<int, uint> seedOf, Tally t, Action<MoodValues> change = null)
        {
            LogAssert.ignoreFailingMessages = true;
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values.Mood);
            GeneratedLevel level = _levels.Build(Seed, 5);
            yield return null;
            yield return null;
            RoomMood mood = level.Mood;
            Debug.Log("INVALIDBOUNDS " + label + " starts: " + Memory());
            for (int i = 0; i < builds; i++)
            {
                uint seed = seedOf(i);
                RoomMood.ParticleSeedForTests = seed;
                int before = s_asserts;
                Object.Destroy(mood);
                mood = RoomMood.Add(level);
                yield return Watch(label, "rebuild " + i + " seed " + seed, before, mood, t);
                if (i % 2500 == 2499) Debug.Log("INVALIDBOUNDS " + label + " after " + (i + 1) + ": " + t.Asserted + " asserted; " + Memory());
            }
            RoomMood.ParticleSeedForTests = 0;
            LogAssert.ignoreFailingMessages = false;
            Debug.Log("INVALIDBOUNDS " + label + " SUMMARY: " + t + "\n" + Memory());
        }

        // ---------------- In the suite ----------------

        [UnityTest]
        public IEnumerator TheAir_InAFloorsFirstFrames_NeverAssertsInvalidBounds_AndPlaysAsBefore()
        {
            var rebuilt = new Tally();
            yield return Rebuilds("the seeds that asserted, rebuilt", s_rebuildSeeds.Length, i => s_rebuildSeeds[i], rebuilt);
            var floors = new Tally();
            yield return Floors("the seeds that asserted, floors", s_floorSeeds.Length, i => s_floorSeeds[i].seed, i => s_floorSeeds[i].floor, floors);
            var automatic = new Tally();
            yield return Floors("automatic seeds, floors", 4, i => 0u, i => i % 2 == 0 ? 5 : 1, automatic);
            Assert.That(rebuilt.Asserted + floors.Asserted + automatic.Asserted, Is.Zero, "the mood made again: " + rebuilt + "\nfloors: " + floors + "\nautomatic: " + automatic);

            // What the player sees is as it was: every system made, set up and turned on with the floor; the dust playing from the
            // build and round her from her first tick; the fog and embers of her room playing, the fog prewarmed.
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(Seed, 5);
            RoomMood mood = level.Mood;
            Assert.That(mood.Rooms.All(r => (r.Fog == null || r.Fog.gameObject.activeInHierarchy) && r.Embers.All(e => e.gameObject.activeInHierarchy)), "turned on once set up");
            Assert.That(mood.Dust.gameObject.activeInHierarchy && mood.Dust.isPlaying, "the dust plays from the build");
            yield return null;
            Assert.That(Vector3.Distance(mood.Dust.transform.position, _player.Position), Is.LessThan(0.5f), "where she is");
            yield return null;
            LevelRoom room = level.RoomAt(_player.Position);
            Assert.That(room, Is.Not.Null);
            RoomMood.RoomParts parts = mood.Rooms.First(r => r.Order == room.Order);
            Assert.That(parts.Fog.isPlaying && parts.Fog.gameObject.activeInHierarchy, "her room's fog");
            Assert.That(parts.Fog.particleCount, Is.GreaterThan(0), "prewarmed: there when she enters");
            Assert.That(parts.Embers.All(e => e.isPlaying && e.gameObject.activeInHierarchy), "her room's embers");
            // Work order 059: the next room is past the passage ahead (RoomMood.Next); the far rooms are those beyond it.
            Assert.That(mood.Rooms.Where(r => r.Order > Math.Max(room.Order + 1, mood.Next)).All(r => r.Fog == null || (!r.Fog.isPlaying && r.Fog.gameObject.activeInHierarchy)), "the far rooms' fog made, not playing");
        }

        /// <summary>
        /// Work order 082 (review 6, finding 12): the mechanism of work order 061's fix pinned, not the luck of seeds found on
        /// generator 14 (on today's floors they may no longer land on a system that asserts): every particle system of the air
        /// is added to an object not yet active in the hierarchy, in a floor's build and in the mood made again.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAirsSystems_AreAddedToAnInactiveObject_InAFloorsBuild_AndInTheMoodMadeAgain()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(Seed, 5);
            RoomMood mood = level.Mood;
            Assert.That(mood, Is.Not.Null);
            Assert.That(mood.Dust != null && mood.Rooms.Any(r => r.Fog != null), "the air's systems made");
            Assert.That(mood.SystemsAddedActive, Is.Zero, "a floor's build: every system added to an inactive object");
            yield return null;
            Object.Destroy(mood);
            mood = RoomMood.Add(level);
            Assert.That(mood.Dust != null && mood.Rooms.Any(r => r.Fog != null), "made again");
            Assert.That(mood.SystemsAddedActive, Is.Zero, "the mood made again: every system added to an inactive object");
            yield return null;
        }

        // ---------------- The proofs (minutes long) ----------------

        [UnityTest, Explicit("work order 061: a proof, minutes long"), Timeout(3600000)]
        public IEnumerator Proof_TheMoodMadeAgain_CountedSeeds()
        {
            var t = new Tally();
            yield return Rebuilds("proof rebuilt, counted seeds", ProofRebuilds, i => (uint)(1 + i), t);
            Assert.That(t.Asserted, Is.Zero, t.ToString());
        }

        [UnityTest, Explicit("work order 061: a proof, minutes long"), Timeout(3600000)]
        public IEnumerator Proof_TheMoodMadeAgain_AutomaticSeeds()
        {
            var t = new Tally();
            yield return Rebuilds("proof rebuilt, automatic seeds", ProofAutomaticRebuilds, i => 0u, t);
            Assert.That(t.Asserted, Is.Zero, t.ToString());
        }

        [UnityTest, Explicit("work order 061: a proof, minutes long"), Timeout(3600000)]
        public IEnumerator Proof_Floors_CountedSeeds()
        {
            var t = new Tally();
            yield return Floors("proof floors, counted seeds", ProofFloors, i => (uint)(1 + i), i => i % 2 == 0 ? 5 : 1, t);
            Assert.That(t.Asserted, Is.Zero, t.ToString());
        }

        [UnityTest, Explicit("work order 061: a proof, minutes long"), Timeout(3600000)]
        public IEnumerator Proof_Floors_AutomaticSeeds()
        {
            var t = new Tally();
            yield return Floors("proof floors, automatic seeds", ProofAutomaticFloors, i => 0u, i => i % 2 == 0 ? 5 : 1, t);
            Assert.That(t.Asserted, Is.Zero, t.ToString());
        }
    }
}
