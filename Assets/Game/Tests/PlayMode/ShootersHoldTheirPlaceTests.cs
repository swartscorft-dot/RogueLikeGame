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
    /// Shooters that hold their place (work order 098; Kristian's choice of 2026-11-24, "Skyttere som holder plassen";
    /// <see cref="ShooterPostRules"/>, <see cref="ShooterValues.HoldsPlace"/>), in the ControlRoom scene through the intents of
    /// <see cref="GeneratedLevels"/>. A combat room of a fixed seed whose shooters all stand out of her reach from the fight's line
    /// (the room's other enemies taken away): she stands at the line for 20 s and no shooter comes nearer than its post less its
    /// circle, none leaves its circle and none begins a shot; a frame of their waiting allocates nothing. Then she walks in: the
    /// shooter whose reach she stands in shoots on its own timing, the wind-up and the recovery; she walks up to it and it backs
    /// off only inside its circle. The recorder's line of each, and the summary's. With the switch off, the same stand fails as it
    /// did before: a shooter comes. A practice room's shooter holds no place; a lab room's shooters do.
    ///
    /// She cannot be hurt. Frames without a window are short (about 0.3 ms): the waits count game seconds, sped up.
    /// </summary>
    public class ShootersHoldTheirPlaceTests
    {
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
            _levels.Values = new LevelValues();
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
                _player.Stop();
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

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>
        /// The first seed (floors 2 and 3: single arrows) with an ordinary combat room of at least two shooters, every one of them
        /// more than 12.5 m from the middle of the fight's line: she stands there out of every one's reach.
        /// </summary>
        static (ulong seed, int floor, int order) FindRoom(LevelValues v)
        {
            for (ulong s = 1; s < 400; s++)
                foreach (int floor in new[] { 2, 3 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, v);
                    foreach (LevelRoom r in l.Rooms)
                    {
                        if (r.Kind != RoomKind.Combat || r.Elite) continue;
                        FightFrame f = FightCover.FrameOf(l, r, v);
                        if (f == null || f.Shooters.Count < 2 || f.Shooters.Any(q => q.DistanceTo(f.LineMiddle) <= 12.5)) continue;
                        return (s, floor, r.Order);
                    }
                }
            Assert.Fail("no such room in the first 400 seeds");
            return (0UL, 0, -1);
        }

        /// <summary>Builds the floor, takes away every enemy but the room's shooters, and stands her at the middle of its line, awake.</summary>
        IEnumerator AtTheLine(ulong seed, int floor, int order, bool holds, Action<GeneratedLevel, List<EnemyShooter>, Vector3> done)
        {
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelRoom room = level.Layout.Rooms[order];
            List<EnemyShooter> shooters = level.EnemiesByRoom[order].OfType<EnemyShooter>().Where(e => e != null).ToList();
            foreach (List<Enemy> list in level.EnemiesByRoom.Values)
                foreach (Enemy e in list)
                    if (e != null && !(e is EnemyShooter s && shooters.Contains(s))) Object.Destroy(e.gameObject);
            foreach (EnemyShooter s in shooters) s.Tuning.HoldsPlace = holds;
            yield return Frames(2);
            FightFrame f = FightCover.FrameOf(level.Layout, room, _levels.Values);
            Vector3 line = level.ToWorld(f.LineMiddle);
            if (NavMesh.SamplePosition(line, out NavMeshHit hit, 1f, NavMesh.AllAreas)) line = hit.position;
            _player.Teleport(new Vector3(line.x, 0f, line.z));
            for (int i = 0; i < 30 && !shooters.All(e => e.IsAwake); i++) yield return null;
            if (!shooters.All(e => e.IsAwake))
            {
                // Not seen past the point from where she was put (the nearest door is another): any waking wakes the room (work order 070).
                shooters[0].Provoke();
                for (int i = 0; i < 30 && !shooters.All(e => e.IsAwake); i++) yield return null;
            }
            Assert.That(shooters.All(e => e.IsAwake), "the room's shooters awake, seed " + seed + " floor " + floor + " room " + order);
            Assert.That(shooters.All(e => e.Fight != null && e.Fight.On), "in the room's fight");
            Debug.Log("SHOOTERSHOLD seed " + seed + " floor " + floor + " room " + room.Letter + order + ": " + string.Join(", ",
                      shooters.Select(e => e.LogWord + " " + Flat(e.Post, _player.Position).ToString("0.0", Inv) + " m from her")));
            done(level, shooters, line);
        }

        /// <summary>Walks her along the NavMesh to a point by her move intent (<see cref="PlayerCharacter.Steer(Vector3)"/>); true when she got within half a metre.</summary>
        IEnumerator WalkTo(Vector3 to, float seconds, bool[] arrived, Action each = null)
        {
            var path = new NavMeshPath();
            arrived[0] = false;
            if (!NavMesh.CalculatePath(_player.Position, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) yield break;
            Vector3[] corners = path.corners;
            int next = 1;
            float until = Time.time + seconds;
            while (Time.time < until && next < corners.Length)
            {
                if (Flat(_player.Position, corners[next]) < 0.3f) next++;
                if (next >= corners.Length) break;
                _player.Steer(corners[next] - _player.Position);
                yield return null;
                each?.Invoke();
            }
            _player.Stop();
            arrived[0] = Flat(_player.Position, to) < 0.5f;
        }

        /// <summary>A point on the NavMesh <paramref name="from"/> to <paramref name="to"/> m from the post that the post sees at a shooter's sight height and that she can walk to, or null.</summary>
        Vector3? SpotSeenFrom(EnemyShooter s, float from, float to)
        {
            var path = new NavMeshPath();
            Physics.SyncTransforms();
            for (float r = from; r <= to; r += 0.5f)
                for (int k = 0; k < 24; k++)
                {
                    float a = k * Mathf.PI * 2f / 24f;
                    Vector3 p = s.Post + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    p = hit.position;
                    Vector3 d = new Vector3(p.x - s.Post.x, 0f, p.z - s.Post.z);
                    float length = d.magnitude;
                    if (Cover.ClearDistance(new Vector3(s.Post.x, 0f, s.Post.z), d / length, length, s.Tuning.SightHeight) < length - 0.05f) continue;
                    if (!NavMesh.CalculatePath(_player.Position, p, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    return p;
                }
            return null;
        }

        // ---------------- The tests ----------------

        [UnityTest]
        public IEnumerator AtTheLineFor20s_NoShooterComes_NoneShoots_ThenSheWalksIn_TheOneInReachShootsOnItsTiming_AndBacksOffOnlyInsideItsCircle()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeShootersHold_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            (ulong seed, int floor, int order) = FindRoom(_levels.Values);
            List<EnemyShooter> shooters = null;
            yield return AtTheLine(seed, floor, order, true, (l, s, at) => shooters = s);
            var starts = new Dictionary<EnemyShooter, List<float>>();
            foreach (EnemyShooter s in shooters)
            {
                EnemyShooter me = s;
                starts[s] = new List<float>();
                s.Reported += r => { if (r.Kind == EnemyReportKind.AttackStarted) starts[me].Add(Time.time); };
            }
            float radius = shooters[0].Tuning.HoldRadius;
            Assert.That(radius, Is.EqualTo(3f));
            Assert.That(shooters.All(s => s.HoldsItsPlace), "every one holds its place: " + string.Join(", ", shooters.Select(s => s.HoldsItsPlace)));

            // 20 s at the line: none comes nearer than its post less its circle, none leaves its circle, none begins a shot.
            var nearer = shooters.ToDictionary(s => s, s => float.PositiveInfinity); // her distance to it, less (to its post less the circle)
            var off = shooters.ToDictionary(s => s, s => 0f);
            Time.timeScale = 4f;
            float until = Time.time + 20f;
            while (Time.time < until)
            {
                yield return null;
                foreach (EnemyShooter s in shooters)
                {
                    nearer[s] = Mathf.Min(nearer[s], Flat(s.transform.position, _player.Position) - (Flat(s.Post, _player.Position) - radius));
                    off[s] = Mathf.Max(off[s], Flat(s.transform.position, s.Post));
                }
            }
            Time.timeScale = 1f;
            foreach (EnemyShooter s in shooters)
            {
                Debug.Log(string.Format(Inv, "SHOOTERSHOLD at the line, {0}: at most {1:0.00} m from its post, {2} shots begun", s.name, off[s], starts[s].Count));
                Assert.That(nearer[s], Is.GreaterThanOrEqualTo(0f), s.name + " came no nearer than its post less " + radius + " m");
                Assert.That(off[s], Is.LessThanOrEqualTo(radius + ShooterPostRules.EdgeSlack), s.name + " stayed in its circle");
                Assert.That(Flat(s.transform.position, _player.Position), Is.GreaterThan(ShooterValues.ShootsFrom), s.name + ": she is out of its reach");
                Assert.That(starts[s], Is.Empty, s.name + " began no shot at her while she was out of its reach");
            }

            // A frame of their waiting allocates nothing.
            foreach (EnemyShooter s in shooters) s.ChaseForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) foreach (EnemyShooter s in shooters) s.ChaseForTests(); }, AllocExt.AllocatingGCMemory(Is.Not),
                        "holding their posts, waiting: nothing");

            // She walks in, to a spot 8 m from one of them that it sees.
            EnemyShooter a = null;
            Vector3? spot = null;
            foreach (EnemyShooter s in shooters.OrderBy(x => Flat(x.Post, _player.Position)))
            {
                spot = SpotSeenFrom(s, 7.5f, 9.5f);
                if (spot == null) continue;
                a = s;
                break;
            }
            Assert.That(a, Is.Not.Null, "a spot in a shooter's reach that it sees");
            var arrived = new bool[1];
            Time.timeScale = 2f;
            yield return WalkTo(spot.Value, 30f, arrived);
            Assert.That(arrived[0], Is.True, "she walked in, to " + spot.Value);
            float stood = Time.time;
            until = Time.time + 9f;
            while (Time.time < until) yield return null;
            Time.timeScale = 1f;
            List<float> mine = starts[a];
            Debug.Log("SHOOTERSHOLD in its reach, " + a.name + ": shots begun at " + string.Join(", ", mine.Select(t => (t - stood).ToString("0.00", Inv))) + " s from her stop");
            Assert.That(mine.Count(t => t >= stood), Is.GreaterThanOrEqualTo(2), "it shoots at her in its reach");
            EnemyAttackDefinition arrow = a.Tuning.Arrows[0];
            for (int i = 1; i < mine.Count; i++)
                Assert.That(mine[i] - mine[i - 1], Is.GreaterThanOrEqualTo(arrow.Windup + arrow.Recovery - 0.1f), "its own timing: a wind-up and a recovery between two shots");
            Assert.That(mine.First(t => t >= stood - 0.01f) - stood, Is.LessThan(arrow.Windup + arrow.Recovery + 1f), "its first shot as she stood came on time");

            // She walks up to it: it backs off, never out of its circle.
            Vector3 toward = a.Post + (_player.Position - a.Post).normalized * 3f;
            if (NavMesh.SamplePosition(toward, out NavMeshHit near, 1f, NavMesh.AllAreas)) toward = near.position;
            float most = 0f;
            bool backed = false;
            Time.timeScale = 2f;
            yield return WalkTo(toward, 15f, arrived, () =>
            {
                most = Mathf.Max(most, Flat(a.transform.position, a.Post));
                backed |= a.IsBackingOff;
            });
            until = Time.time + 6f;
            while (Time.time < until)
            {
                yield return null;
                most = Mathf.Max(most, Flat(a.transform.position, a.Post));
                backed |= a.IsBackingOff;
            }
            Time.timeScale = 1f;
            Debug.Log(string.Format(Inv, "SHOOTERSHOLD passed, {0}: she {1:0.0} m from its post, it at most {2:0.00} m from it, backed off {3}", a.name,
                                    Flat(_player.Position, a.Post), most, backed));
            Assert.That(Flat(_player.Position, a.Post), Is.LessThan(a.Tuning.BackOffUnder), "she came nearer than it backs off from");
            Assert.That(backed, Is.True, "it backed off");
            Assert.That(most, Is.LessThanOrEqualTo(radius + ShooterPostRules.EdgeSlack), "only inside its circle");

            // The recorder's line of each, as they die, and the summary's.
            foreach (EnemyShooter s in shooters) s.Body.TakeHit(100000000);
            yield return Frames(5);
            List<SessionEvent> lines = _recorder.Events.Where(e => e.Type == ShooterPostEvents.Post).ToList();
            Assert.That(lines.Count, Is.EqualTo(shooters.Count), "one line a shooter");
            Assert.That(lines.All(e => e.ExtraValue("held") == "1" && e.ExtraValue("died") != "-" && e.ExtraValue("order") == order.ToString(Inv)), Is.True,
                        string.Join(" | ", lines.Select(e => e.ToJson())));
            Assert.That(lines.Count(e => e.ExtraValue("reach") != "-"), Is.GreaterThanOrEqualTo(1), "she came within the reach of one");
            _levels.BackToPracticeRoom();
            yield return null;
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            string line = summary.Split('\n').FirstOrDefault(x => x.StartsWith("Shooters ", StringComparison.Ordinal));
            Debug.Log("SHOOTERSHOLD summary: " + line);
            Assert.That(line, Does.StartWith("Shooters " + shooters.Count + " of " + shooters.Count + " shooters held a post, in 1 room"));
        }

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheSameStand_FailsAsBefore_AShooterComes()
        {
            (ulong seed, int floor, int order) = FindRoom(_levels.Values);
            List<EnemyShooter> shooters = null;
            yield return AtTheLine(seed, floor, order, false, (l, s, at) => shooters = s);
            Assert.That(shooters.Any(s => s.HoldsItsPlace), Is.False, "the switch off: none holds a place");
            float radius = shooters[0].Tuning.HoldRadius;
            bool came = false, shot = false;
            foreach (EnemyShooter s in shooters) s.Reported += r => { if (r.Kind == EnemyReportKind.AttackStarted) shot = true; };
            Time.timeScale = 4f;
            float until = Time.time + 20f;
            while (Time.time < until && !(came && shot))
            {
                yield return null;
                came |= shooters.Any(s => Flat(s.transform.position, _player.Position) < Flat(s.Post, _player.Position) - radius - 0.5f);
            }
            Time.timeScale = 1f;
            Debug.Log("SHOOTERSHOLD switch off: a shooter came " + came + ", one shot " + shot);
            Assert.That(came, Is.True, "as before: a shooter walks to her, nearer than its post less " + radius + " m");
            Assert.That(shot, Is.True, "and shoots at her where she stands");
        }

        [UnityTest]
        public IEnumerator APracticeRoomsShooter_HoldsNoPlace_ALabRoomsShooters_Do()
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(made.Count, Is.EqualTo(1));
            var practice = (EnemyShooter)made[0];
            yield return Frames(3);
            practice.Provoke();
            yield return Frames(3);
            Assert.That(practice.Fight, Is.Null);
            Assert.That(practice.IsAwake, Is.True);
            Assert.That(practice.HoldsItsPlace, Is.False, "the practice room's shooter moves as before");
            _levels.ClearSpawned();

            int found = 0;
            foreach (LabEntry entry in RoomLab.Entries(_levels.Values).Where(e => e.Kind == LabKind.Ordinary && e.Template != null).Take(12))
            {
                GeneratedLevel level = _levels.StartLab(entry, 2, 0x0980B0B0UL);
                Assert.That(level, Is.Not.Null);
                yield return Frames(2);
                LevelRoom room = level.Layout.Rooms[1];
                List<EnemyShooter> shooters = level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> own) ? own.OfType<EnemyShooter>().ToList() : new List<EnemyShooter>();
                if (shooters.Count == 0) continue;
                shooters[0].Provoke();
                yield return Frames(3);
                Assert.That(shooters.All(s => s.IsAwake && s.Fight != null && s.Fight.On), "the lab room's fight is on: " + entry.Name);
                Assert.That(shooters.All(s => s.HoldsItsPlace), "and its shooters hold their places: " + entry.Name);
                found++;
                if (found >= 2) break;
            }
            Debug.Log("SHOOTERSHOLD lab rooms with shooters, holding their places: " + found);
            Assert.That(found, Is.GreaterThan(0), "a lab room with shooters");
        }
    }
}
