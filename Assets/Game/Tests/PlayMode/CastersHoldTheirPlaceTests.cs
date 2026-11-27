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
    /// Casters that hold their place (work order 101; Kristian's choice of 2026-11-24 afternoon, "Behold, og kasterne også";
    /// <see cref="ShooterPostRules"/> with the caster's own values, <see cref="CasterValues.HoldsPlace"/>), in the ControlRoom scene through
    /// the intents of <see cref="GeneratedLevels"/>, as work order 098's <see cref="ShootersHoldTheirPlaceTests"/> for the shooters. A
    /// combat room of a fixed seed whose casters all stand out of her reach from the fight's line (the room's other enemies taken
    /// away): she stands at the line for 20 s and no caster comes nearer than its post less its circle, none leaves its circle and none
    /// begins a cast; a frame of their waiting allocates nothing. Then she walks in: the caster whose reach she stands in casts on its
    /// own timing, the wind-up and the recovery; she walks up to it and it backs off only inside its circle. The recorder's line of each,
    /// and the summary's. With the switch off, the same stand fails as it did before: a caster comes. The elite caster of an elite room:
    /// the same stand. A practice room's caster holds no place.
    ///
    /// She cannot be hurt. Frames without a window are short (about 0.3 ms): the waits count game seconds, sped up.
    /// </summary>
    public class CastersHoldTheirPlaceTests
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
            _levels.Values = new LevelValues { Waves = false }; // work order 136 (changed deliberately): its rooms wake whole, as the rule it measures was built on
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

        /// <summary>The places the generator stood the room's casters (the elite's with them), as <see cref="FightCover.FrameOf"/> reads its ranged ones.</summary>
        static List<LevelPoint> CasterPlaces(LevelRoom r, bool eliteOnly = false) =>
            Enumerable.Range(0, r.EnemyPoints.Count).Where(i => r.GroupAt(i).Kind == EnemyKind.Caster && (!eliteOnly || r.GroupAt(i).Elite))
                      .SelectMany(i => r.PlacesAt(i)).ToList();

        /// <summary>
        /// The first seed (floors 3 and 4, where the casters are) with a combat room (an elite one, when asked: with its elite a caster)
        /// whose every caster stands more than 12.5 m from the middle of the fight's line: she stands there out of every one's reach.
        /// </summary>
        static (ulong seed, int floor, int order) FindRoom(LevelValues v, bool elite)
        {
            for (ulong s = 1; s < 400; s++)
                foreach (int floor in new[] { 3, 4 })
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, v);
                    foreach (LevelRoom r in l.Rooms)
                    {
                        if (r.Kind != RoomKind.Combat || r.Elite != elite) continue;
                        FightFrame f = FightCover.FrameOf(l, r, v);
                        if (f == null || (elite && CasterPlaces(r, true).Count == 0)) continue;
                        List<LevelPoint> casters = CasterPlaces(r);
                        if (casters.Count < 1 || casters.Any(q => q.DistanceTo(f.LineMiddle) <= 12.5)) continue;
                        return (s, floor, r.Order);
                    }
                }
            Assert.Fail("no such room in the first 400 seeds");
            return (0UL, 0, -1);
        }

        /// <summary>Builds the floor, takes away every enemy but the room's casters, and stands her at the middle of its line, awake.</summary>
        IEnumerator AtTheLine(ulong seed, int floor, int order, bool holds, Action<GeneratedLevel, List<EnemyCaster>, Vector3> done)
        {
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelRoom room = level.Layout.Rooms[order];
            List<EnemyCaster> casters = level.EnemiesByRoom[order].OfType<EnemyCaster>().Where(e => e != null).ToList();
            Assert.That(casters.Count, Is.EqualTo(CasterPlaces(room).Count), "the room's casters, as its description says");
            foreach (List<Enemy> list in level.EnemiesByRoom.Values)
                foreach (Enemy e in list)
                    if (e != null && !(e is EnemyCaster c && casters.Contains(c))) Object.Destroy(e.gameObject);
            foreach (EnemyCaster c in casters) c.Tuning.HoldsPlace = holds;
            yield return Frames(2);
            FightFrame f = FightCover.FrameOf(level.Layout, room, _levels.Values);
            Vector3 line = level.ToWorld(f.LineMiddle);
            if (NavMesh.SamplePosition(line, out NavMeshHit hit, 1f, NavMesh.AllAreas)) line = hit.position;
            _player.Teleport(new Vector3(line.x, 0f, line.z));
            for (int i = 0; i < 30 && !casters.All(e => e.IsAwake); i++) yield return null;
            if (!casters.All(e => e.IsAwake))
            {
                // Not seen past the point from where she was put (the nearest door is another): any waking wakes the room (work order 070).
                casters[0].Provoke();
                for (int i = 0; i < 30 && !casters.All(e => e.IsAwake); i++) yield return null;
            }
            Assert.That(casters.All(e => e.IsAwake), "the room's casters awake, seed " + seed + " floor " + floor + " room " + order);
            Assert.That(casters.All(e => e.Fight != null && e.Fight.On), "in the room's fight");
            Debug.Log("CASTERSHOLD seed " + seed + " floor " + floor + " room " + room.Letter + order + ": " + string.Join(", ",
                      casters.Select(e => e.LogWord + " " + Flat(e.Post, _player.Position).ToString("0.0", Inv) + " m from her")));
            done(level, casters, line);
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

        /// <summary>A point on the NavMesh <paramref name="from"/> to <paramref name="to"/> m from the post that the post sees at a caster's sight height and that she can walk to, or null.</summary>
        Vector3? SpotSeenFrom(EnemyCaster c, float from, float to)
        {
            var path = new NavMeshPath();
            Physics.SyncTransforms();
            for (float r = from; r <= to; r += 0.5f)
                for (int k = 0; k < 24; k++)
                {
                    float a = k * Mathf.PI * 2f / 24f;
                    Vector3 p = c.Post + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    p = hit.position;
                    Vector3 d = new Vector3(p.x - c.Post.x, 0f, p.z - c.Post.z);
                    float length = d.magnitude;
                    if (Cover.ClearDistance(new Vector3(c.Post.x, 0f, c.Post.z), d / length, length, c.Tuning.SightHeight) < length - 0.05f) continue;
                    if (!NavMesh.CalculatePath(_player.Position, p, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    return p;
                }
            return null;
        }

        /// <summary>
        /// 20 s at the line: none comes nearer than its post less its circle, none leaves its circle, none begins a cast; the casts
        /// begun of each are listened to from here on (<paramref name="starts"/>).
        /// </summary>
        IEnumerator StandAtTheLine(List<EnemyCaster> casters, Dictionary<EnemyCaster, List<float>> starts)
        {
            foreach (EnemyCaster c in casters)
            {
                EnemyCaster me = c;
                starts[c] = new List<float>();
                c.Reported += r => { if (r.Kind == EnemyReportKind.AttackStarted) starts[me].Add(Time.time); };
            }
            float radius = casters[0].Tuning.HoldRadius;
            Assert.That(radius, Is.EqualTo(3f));
            Assert.That(casters.All(c => c.HoldsItsPlace), "every one holds its place: " + string.Join(", ", casters.Select(c => c.HoldsItsPlace)));
            var nearer = casters.ToDictionary(c => c, c => float.PositiveInfinity); // her distance to it, less (to its post less the circle)
            var off = casters.ToDictionary(c => c, c => 0f);
            Time.timeScale = 4f;
            float until = Time.time + 20f;
            while (Time.time < until)
            {
                yield return null;
                foreach (EnemyCaster c in casters)
                {
                    nearer[c] = Mathf.Min(nearer[c], Flat(c.transform.position, _player.Position) - (Flat(c.Post, _player.Position) - radius));
                    off[c] = Mathf.Max(off[c], Flat(c.transform.position, c.Post));
                }
            }
            Time.timeScale = 1f;
            foreach (EnemyCaster c in casters)
            {
                Debug.Log(string.Format(Inv, "CASTERSHOLD at the line, {0}: at most {1:0.00} m from its post, {2} casts begun, {3} circles placed", c.LogWord + " " + c.name,
                                        off[c], starts[c].Count, c.Circles.Count));
                Assert.That(nearer[c], Is.GreaterThanOrEqualTo(0f), c.name + " came no nearer than its post less " + radius + " m");
                Assert.That(off[c], Is.LessThanOrEqualTo(radius + ShooterPostRules.EdgeSlack), c.name + " stayed in its circle");
                Assert.That(Flat(c.transform.position, _player.Position), Is.GreaterThan(CasterValues.CastsFrom), c.name + ": she is out of its reach");
                Assert.That(starts[c], Is.Empty, c.name + " began no cast at her while she was out of its reach");
                Assert.That(c.CastsBegun, Is.EqualTo(0), c.name + ": none counted either");
                Assert.That(c.Circles, Is.Empty, c.name + ": nothing on the floor");
            }
        }

        // ---------------- The tests ----------------

        [UnityTest]
        public IEnumerator AtTheLineFor20s_NoCasterComes_NoneCasts_ThenSheWalksIn_TheOneInReachCastsOnItsTiming_AndBacksOffOnlyInsideItsCircle()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeCastersHold_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            (ulong seed, int floor, int order) = FindRoom(_levels.Values, false);
            List<EnemyCaster> casters = null;
            yield return AtTheLine(seed, floor, order, true, (l, c, at) => casters = c);
            var starts = new Dictionary<EnemyCaster, List<float>>();
            yield return StandAtTheLine(casters, starts);
            float radius = casters[0].Tuning.HoldRadius;

            // A frame of their waiting allocates nothing.
            foreach (EnemyCaster c in casters) c.ChaseForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) foreach (EnemyCaster c in casters) c.ChaseForTests(); }, AllocExt.AllocatingGCMemory(Is.Not),
                        "holding their posts, waiting: nothing");

            // She walks in, to a spot 8 m from one of them that it sees.
            EnemyCaster a = null;
            Vector3? spot = null;
            foreach (EnemyCaster c in casters.OrderBy(x => Flat(x.Post, _player.Position)))
            {
                spot = SpotSeenFrom(c, 7.5f, 9.5f);
                if (spot == null) continue;
                a = c;
                break;
            }
            Assert.That(a, Is.Not.Null, "a spot in a caster's reach that it sees");
            var arrived = new bool[1];
            Time.timeScale = 2f;
            yield return WalkTo(spot.Value, 30f, arrived);
            Assert.That(arrived[0], Is.True, "she walked in, to " + spot.Value);
            float stood = Time.time;
            float until = Time.time + 9f;
            while (Time.time < until) yield return null;
            Time.timeScale = 1f;
            List<float> mine = starts[a];
            Debug.Log("CASTERSHOLD in its reach, " + a.name + ": casts begun at " + string.Join(", ", mine.Select(t => (t - stood).ToString("0.00", Inv))) + " s from her stop");
            Assert.That(mine.Count(t => t >= stood), Is.GreaterThanOrEqualTo(2), "it casts at her in its reach");
            // Today's timing: a wind-up and a recovery between two casts, the shortest of its casts (Bone Spikes' or the orb's).
            float shortest = a.Tuning.Enemy.Attacks.Concat(a.Tuning.Orbs).Min(x => x.Windup + x.Recovery);
            float longest = a.Tuning.Enemy.Attacks.Concat(a.Tuning.Orbs).Max(x => x.Windup + x.Recovery);
            for (int i = 1; i < mine.Count; i++)
                Assert.That(mine[i] - mine[i - 1], Is.GreaterThanOrEqualTo(shortest - 0.1f), "its own timing: a wind-up and a recovery between two casts");
            Assert.That(mine.First(t => t >= stood - 0.01f) - stood, Is.LessThan(longest + 1f), "its first cast as she stood came on time");

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
            Debug.Log(string.Format(Inv, "CASTERSHOLD passed, {0}: she {1:0.0} m from its post, it at most {2:0.00} m from it, backed off {3}", a.name,
                                    Flat(_player.Position, a.Post), most, backed));
            Assert.That(Flat(_player.Position, a.Post), Is.LessThan(a.Tuning.BackOffUnder), "she came nearer than it backs off from");
            Assert.That(backed, Is.True, "it backed off");
            Assert.That(most, Is.LessThanOrEqualTo(radius + ShooterPostRules.EdgeSlack), "only inside its circle");

            // The recorder's line of each, as they die, and the summary's.
            foreach (EnemyCaster c in casters) c.Body.TakeHit(100000000);
            yield return Frames(5);
            List<SessionEvent> lines = _recorder.Events.Where(e => e.Type == CasterPostEvents.Post).ToList();
            Assert.That(lines.Count, Is.EqualTo(casters.Count), "one line a caster");
            Assert.That(lines.All(e => e.ExtraValue("held") == "1" && e.ExtraValue("died") != "-" && e.ExtraValue("order") == order.ToString(Inv)
                                       && e.ExtraValue("floor") == floor.ToString(Inv)), Is.True, string.Join(" | ", lines.Select(e => e.ToJson())));
            Assert.That(lines.Count(e => e.ExtraValue("reach") != "-"), Is.GreaterThanOrEqualTo(1), "she came within the reach of one");
            Assert.That(_recorder.Events.Any(e => e.Type == ShooterPostEvents.Post), Is.False, "no shooter's line: there was none");
            _levels.BackToPracticeRoom();
            yield return null;
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            string line = summary.Split('\n').FirstOrDefault(x => x.StartsWith("Casters ", StringComparison.Ordinal));
            Debug.Log("CASTERSHOLD summary: " + line);
            Assert.That(line, Does.StartWith("Casters " + casters.Count + " of " + casters.Count + " caster" + (casters.Count == 1 ? "" : "s") + " held a post, in 1 room"));
        }

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheSameStand_FailsAsBefore_ACasterComes()
        {
            (ulong seed, int floor, int order) = FindRoom(_levels.Values, false);
            List<EnemyCaster> casters = null;
            yield return AtTheLine(seed, floor, order, false, (l, c, at) => casters = c);
            Assert.That(casters.Any(c => c.HoldsItsPlace), Is.False, "the switch off: none holds a place");
            float radius = casters[0].Tuning.HoldRadius;
            bool came = false, cast = false;
            foreach (EnemyCaster c in casters) c.Reported += r => { if (r.Kind == EnemyReportKind.AttackStarted) cast = true; };
            Time.timeScale = 4f;
            float until = Time.time + 20f;
            while (Time.time < until && !(came && cast))
            {
                yield return null;
                came |= casters.Any(c => Flat(c.transform.position, _player.Position) < Flat(c.Post, _player.Position) - radius - 0.5f);
            }
            Time.timeScale = 1f;
            Debug.Log("CASTERSHOLD switch off: a caster came " + came + ", one cast " + cast);
            Assert.That(came, Is.True, "as before: a caster walks to her, nearer than its post less " + radius + " m");
            Assert.That(cast, Is.True, "and casts at her where she stands");
        }

        [UnityTest]
        public IEnumerator TheEliteCasterOfAnEliteRoom_AtTheLineFor20s_DoesNotCome_NorCasts()
        {
            (ulong seed, int floor, int order) = FindRoom(_levels.Values, true);
            List<EnemyCaster> casters = null;
            yield return AtTheLine(seed, floor, order, true, (l, c, at) => casters = c);
            Assert.That(casters.Count(c => c.IsElite), Is.EqualTo(1), "the elite room's elite caster");
            var starts = new Dictionary<EnemyCaster, List<float>>();
            yield return StandAtTheLine(casters, starts);
        }

        [UnityTest]
        public IEnumerator APracticeRoomsCaster_HoldsNoPlace()
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Caster);
            Assert.That(made.Count, Is.EqualTo(1));
            var practice = (EnemyCaster)made[0];
            yield return Frames(3);
            practice.Provoke();
            yield return Frames(3);
            Assert.That(practice.Fight, Is.Null);
            Assert.That(practice.IsAwake, Is.True);
            Assert.That(practice.HoldsItsPlace, Is.False, "the practice room's caster moves as before");
            _levels.ClearSpawned();
        }
    }
}
