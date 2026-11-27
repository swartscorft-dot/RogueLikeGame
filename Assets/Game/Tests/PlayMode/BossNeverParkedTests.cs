using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// Work order 146 (Kristian, 2026-11-27, after his run of commit 4482587: "bossen stod fast mellom en trang path. så det var grise
    /// enkelt. [...] nå sto jeg basicly bare stille og høyre cklikket"; <see cref="BossValues.NeverParked"/>). On floor 5's throne room,
    /// built alone, with the fight woken at its point: she stands still (untouchable, so a strike that reaches her is counted and does
    /// not end the test) and the boss is put somewhere in its room. A strike that reaches her is one that would have hurt her: any but
    /// one out of its area, stopped by a wall or by cover; the Cleave's wave is not counted (it is the Cleave's). Time runs in fixed
    /// steps of <see cref="Step"/>, so the measure does not depend on the machine.
    ///
    /// His fight, from his log (2026-11-27_10-35-01.jsonl): she stood at the mouth of the nook beside the left pier from 811.1 s to 814.2 s,
    /// and the boss charged twice from beyond the pier's corner; the two charges ran 0.10 s and 0.01 s.
    /// </summary>
    public class BossNeverParkedTests
    {
        /// <summary>s of game time a frame of these tests: fixed, so the measure is the same on a busy machine.</summary>
        const float Step = 1f / 60f;
        /// <summary>
        /// s more than the walk to her at its speed, along the NavMesh, that it may take to reach her with a strike: a windup and a
        /// recovery of its slowest attack (the charge's 0.9 and 1.6 s) twice over. A boss that stands short of her longer than this is parked.
        /// </summary>
        const float MostSlack = 5f;
        /// <summary>His floor 5 (the run KSRLMZJG's), and three more floor 5s: two of the other throne template, one more of his.</summary>
        const string HisFloor = "XFBMUJHT";
        static readonly ulong[] OtherFloors = { 0x13C6EF362UL, 0x3B54CDA26UL, 0x9E3779B1UL };
        /// <summary>His room as his log has it (the "fight_room" line): the floor is the same with this order's code.</summary>
        const string HisCells = "######....######/######....######/##............##/##............##/....##....##..../....##....##..../................/................/................/................/................/................/##............##/##............##/####........####/####...#....####";

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        bool _entranceBefore, _parkedBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _entranceBefore = BossEntranceValues.On;
            _parkedBefore = BossValues.NeverParked;
            BossEntranceValues.On = false; // the fight wakes at the point, as the entrance's end wakes it
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            BossValues.NeverParked = _parkedBefore;
            BossEntranceValues.On = _entranceBefore;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- His fight ----------------

        /// <summary>Where the boss stood in his fight from 797.4 s to 811.3 s, in its room's metres from the door (d in, s to her right): in the band between the front wall and the left pier, its body filling it.</summary>
        const double HisBossD = 6.11, HisBossS = -3.97;
        /// <summary>Where she stood from 811.1 s to 814.2 s, at the mouth of the nook beside the left pier.</summary>
        const double HisD = 10.0, HisS = -10.2;
        /// <summary>
        /// s within which, angry, it reaches her standing there: the walk from 7.3 m to the Cleave's 5 m and its angry windup (0.88 s)
        /// take 1.7 s. In his fight it charged twice instead (0.72 s each and a recovery of 1.6 s, 3 s at the least) and ran 0.10 s and 0.01 s.
        /// </summary>
        const float HisSpotSeconds = 2.5f;

        [UnityTest, Timeout(240000)]
        public IEnumerator HisFight_TheBossBesideThePier_SheAtHisSpot_ItWalksToHerAndStrikes_WithTheSwitchOffItChargesNowhere()
        {
            Assert.That(LevelSeed.TryParse(HisFloor, out ulong seed), Is.True);
            Room r = null;
            yield return Build(seed, x => r = x);
            Assert.That(Cells(r.Space), Is.EqualTo(HisCells), "his throne room, as his log has it");
            // Phase two, as from 805.5 s: below half its health, its roar over.
            r.Boss.Body.TakeHit((int)(r.Boss.Body.CurrentHealth * 0.6f));
            Time.captureDeltaTime = Step;
            float roarEnd = Time.time + r.Boss.Tuning.Phases.RoarSeconds + 0.2f;
            while (Time.time < roarEnd) yield return null;
            Assert.That(r.Boss.IsAngry && r.Boss.HasBroken && !r.Boss.IsRoaring, Is.True, "angry, broken, its roar over");
            Assert.That(OnFloor(r.World(HisBossD, HisBossS), 0.5f, out Vector3 bossAt), Is.True, "where the boss stood is walkable floor");
            Assert.That(OnFloor(r.World(HisD, HisS), 0.5f, out Vector3 herAt), Is.True, "where she stood is walkable floor");

            BossValues.NeverParked = false;
            float first = 0f;
            yield return Stand(r, herAt, bossAt, HisSpotSeconds, (f, _) => first = f);
            Assert.That(first, Is.LessThan(0f), "switch off: nothing reaches her for " + HisSpotSeconds + " s");
            Assert.That(r.Charges, Is.GreaterThanOrEqualTo(1), "it charges");
            Assert.That(r.ShortestCharge, Is.LessThan(2f), "a charge cut to almost nothing at the pier's corner, as his (0.10 s and 0.01 s at 12 m/s)");

            BossValues.NeverParked = true;
            yield return Stand(r, herAt, bossAt, HisSpotSeconds, (f, _) => first = f);
            Assert.That(first, Is.GreaterThanOrEqualTo(0f), "switch on: a strike reaches her within " + HisSpotSeconds + " s");
            Assert.That(r.ChargesShort, Is.EqualTo(0), "no charge cut short of her");
        }

        /// <summary>
        /// The same room, the boss beside the right pier (the left one's mirror; work order 146's measure put it there from the room's
        /// point 30 % in and 80 % across), she standing where she came in: with the switch off it charges the pier's corner over and over
        /// and never moves; on, it walks round the pier and strikes.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator ThePierCorner_SheStandsWhereSheCameIn_ParkedForTenSecondsWithTheSwitchOff_ReachedWithItOn()
        {
            Assert.That(LevelSeed.TryParse(HisFloor, out ulong seed), Is.True);
            Room r = null;
            yield return Build(seed, x => r = x);
            Assert.That(OnFloor(r.World(9.6, 11.6), 3f, out Vector3 bossAt), Is.True);
            Assert.That(OnFloor(r.World(5.0, -1.0), 0.5f, out Vector3 herAt), Is.True);
            const float Parked = 10f;
            const float ParkedWithin = 3f; // m: its cut charges move it a metre or two at the most, then not at all

            BossValues.NeverParked = false;
            float first = 0f, moved = 0f;
            yield return Stand(r, herAt, bossAt, Parked, (f, m) => { first = f; moved = m; });
            Assert.That(first, Is.LessThan(0f), "switch off: nothing reaches her for " + Parked + " s");
            Assert.That(r.ChargesShort, Is.GreaterThanOrEqualTo(2), "it charges again and again, cut short of her");
            Assert.That(r.ShortestCharge, Is.LessThan(2f));
            Assert.That(moved, Is.LessThan(ParkedWithin), "and stands by where it was put");

            BossValues.NeverParked = true;
            float most = WalkLength(bossAt, herAt) / r.Boss.Values.MoveSpeed + MostSlack;
            yield return Stand(r, herAt, bossAt, most, (f, m) => first = f);
            Assert.That(first, Is.GreaterThanOrEqualTo(0f), "switch on: a strike reaches her within " + most.ToString("0.0") + " s");
            Assert.That(r.ChargesShort, Is.EqualTo(0), "no charge cut short of her");
        }

        // ---------------- The whole room ----------------

        /// <summary>
        /// Four floor 5s (his and three more, both throne templates): from three start points (its own point, 80 % in and a quarter across,
        /// 30 % in and 80 % across), she standing still at every walkable 2 m cell of his room and every other one of the others (4 m)
        /// that she can walk to from the door, the doorway itself among them: a strike reaches her within the walk to her at its speed
        /// and <see cref="MostSlack"/>. His room also in phase two, on the 4 m grid.
        /// </summary>
        [UnityTest, Timeout(1500000)]
        public IEnumerator EveryStart_EveryPlaceSheStands_FourThroneRooms_ItReachesHer()
        {
            Assert.That(LevelSeed.TryParse(HisFloor, out ulong his), Is.True);
            var seeds = new List<ulong> { his };
            seeds.AddRange(OtherFloors);
            var faults = new List<string>();
            int cases = 0;
            var templates = new HashSet<string>();
            foreach (ulong seed in seeds)
            {
                Room r = null;
                yield return Build(seed, x => r = x);
                templates.Add(r.R.Template?.Template?.Name ?? "none");
                Assert.That(OnFloor(r.World(1.0, 0.0), 1f, out Vector3 door), Is.True, "inside the door");
                double depth = r.Space.Depth, width = r.Space.Cols * RoomUseValues.CellMetres, left = r.Space.Left;
                var starts = new List<Vector3> { r.Boss.Home };
                if (OnFloor(r.World(depth * 0.8, left + width * 0.25), 3f, out Vector3 a)) starts.Add(a);
                if (OnFloor(r.World(depth * 0.3, left + width * 0.8), 3f, out Vector3 b)) starts.Add(b);
                starts.RemoveAll(st => WalkLength(st, door) < 0f); // a start in a pocket it could never be in
                for (int phase = 1; phase <= (seed == his ? 2 : 1); phase++)
                {
                    if (phase == 2)
                    {
                        r.Boss.Body.TakeHit((int)(r.Boss.Body.CurrentHealth * 0.6f));
                        Time.captureDeltaTime = Step;
                        float roarEnd = Time.time + r.Boss.Tuning.Phases.RoarSeconds + 0.2f;
                        while (Time.time < roarEnd) yield return null;
                    }
                    int every = seed == his && phase == 1 ? 1 : 2;
                    var places = new List<Vector3>();
                    if (OnFloor(r.World(0.6, 0.0), 0.5f, out Vector3 doorway)) places.Add(doorway); // the doorway itself
                    for (int row = 0; row < r.Space.Rows; row += every)
                        for (int col = 0; col < r.Space.Cols; col += every)
                            if (r.Space.IsWalkable(row, col)
                                && OnFloor(r.World((row + 0.5) * RoomUseValues.CellMetres, left + (col + 0.5) * RoomUseValues.CellMetres), 1f, out Vector3 p))
                                places.Add(p);
                    foreach (Vector3 herAt in places)
                    {
                        if (WalkLength(door, herAt) < 0f) continue; // nowhere she can walk to from the door
                        foreach (Vector3 bossAt in starts)
                        {
                            float walk = WalkLength(bossAt, herAt);
                            if (walk < 0f)
                            {
                                faults.Add(LevelSeed.ToText(seed) + " phase " + phase + ": she stands at " + r.At(herAt) + ", which it cannot walk to from " + r.At(bossAt));
                                continue;
                            }
                            float most = walk / r.Boss.Values.MoveSpeed + MostSlack, first = 0f;
                            cases++;
                            yield return Stand(r, herAt, bossAt, most, (f, m) => first = f);
                            if (first < 0f)
                                faults.Add(LevelSeed.ToText(seed) + " phase " + phase + ": she at " + r.At(herAt) + ", it from " + r.At(bossAt) + ": nothing reached her in "
                                           + most.ToString("0.0") + " s; it ended at " + r.At(r.Boss.transform.position) + ", " + r.Charges + " charges, " + r.ChargesShort + " cut short of her");
                        }
                    }
                }
            }
            Debug.Log("Work order 146's measure: " + cases + " places and starts in " + string.Join(", ", templates) + "; " + faults.Count + " not reached");
            Assert.That(templates.Count, Is.EqualTo(2), "both throne templates");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        // ---------------- Helpers ----------------

        sealed class Room
        {
            public GeneratedLevel Level;
            public LevelRoom R;
            public LevelDoor Door;
            public RoomFrame Frame;
            public EnemyBoss Boss;
            public RoomFight Fight;
            public RoomSpace Space;
            public int Reached;
            public int Charges, ChargesCut, ChargesShort;
            public float ShortestCharge = float.PositiveInfinity;
            public Vector3 World(double d, double s) => Level.ToWorld(Frame.FromFrame(d, s));
            public string At(Vector3 w)
            {
                LevelPoint p = Level.ToLevel(w);
                Frame.ToFrame(p.X, p.Y, out double d, out double s);
                return "(d " + d.ToString("0.0", CultureInfo.InvariantCulture) + ", s " + s.ToString("0.0", CultureInfo.InvariantCulture) + ")";
            }
        }

        static string Cells(RoomSpace sp)
        {
            var c = new StringBuilder();
            for (int row = 0; row < sp.Rows; row++)
            {
                if (row > 0) c.Append('/');
                for (int col = 0; col < sp.Cols; col++) c.Append(sp.Walkable[row * sp.Cols + col] ? '.' : '#');
            }
            return c.ToString();
        }

        /// <summary>Floor 5 alone, the boss alone awake in its fight; the floor's other enemies switched off (they sleep in other rooms).</summary>
        IEnumerator Build(ulong seed, Action<Room> got)
        {
            _practiceEnemy.enabled = true; // the floor's enemies are copies of it
            GeneratedLevel level = _levels.Build(seed, 5);
            _practiceEnemy.enabled = false;
            yield return null;
            yield return null;
            var r = new Room { Level = level, R = level.Layout.ExitRoom, Boss = level.Boss as EnemyBoss };
            Assert.That(r.Boss, Is.Not.Null, "floor 5's act boss");
            r.Door = LevelGeometry.EntryDoor(level.Layout, r.R);
            r.Fight = _levels.FightOf(r.R);
            Assert.That(r.Fight, Is.Not.Null, "the boss room's fight");
            r.Frame = RoomUseRules.FrameOf(level.Layout, r.R, r.Door);
            r.Space = RoomSpace.Make(level.Layout, r.R, r.Door, r.Frame, r.R.Props);
            foreach (Enemy e in new List<Enemy>(Enemy.All))
                if (e != r.Boss) e.gameObject.SetActive(false);
            r.Boss.Reported += e =>
            {
                if (e.Kind == EnemyReportKind.Struck && !e.Flown && e.Outcome != StrikeOutcome.OutOfArea
                    && e.Outcome != StrikeOutcome.StoppedByWall && e.Outcome != StrikeOutcome.StoppedByCover) r.Reached++;
                if (e.Kind == EnemyReportKind.AttackStarted && e.Attack == BossValues.Charge)
                {
                    r.Charges++;
                    r.ShortestCharge = Mathf.Min(r.ShortestCharge, r.Boss.ChargeReach);
                    EnemyAttackDefinition charge = Array.Find(r.Boss.Values.Attacks, a => a.Name == BossValues.Charge);
                    if (r.Boss.ChargeReach < charge.Length - 1e-3f) r.ChargesCut++;
                    Vector3 toHer = _player.Position - r.Boss.transform.position;
                    toHer.y = 0f;
                    if (!BossTactics.ChargeReachesHer(charge.Length, r.Boss.ChargeReach, toHer.magnitude, charge.HalfWidth, Placeholders.PlayerRadius)) r.ChargesShort++;
                }
            };
            // She walks in past the point (untouchable: a strike that reaches her is counted, and the test goes on).
            _health.Untouchable = true;
            _player.Teleport(r.World(1.0, 0.0));
            for (int i = 0; i < 30 && _levels.CurrentRoom != r.R; i++) yield return null;
            _player.Teleport(r.World(6.0, 0.0));
            for (int i = 0; i < 120 && !(r.Fight.On && r.Boss.IsAwake); i++) yield return null;
            Assert.That(r.Fight.On && r.Boss.IsAwake, Is.True, "the fight is on and the boss awake");
            got(r);
        }

        /// <summary>
        /// The boss put at <paramref name="bossAt"/> (whatever it was doing dropped, as a daze drops it), she standing still at
        /// <paramref name="herAt"/>; time runs until a strike reaches her or <paramref name="most"/> s have passed. Gives the seconds to
        /// the first strike that reached her (negative: none), and how far it ended from where it was put.
        /// </summary>
        IEnumerator Stand(Room r, Vector3 herAt, Vector3 bossAt, float most, Action<float, float> got)
        {
            Time.captureDeltaTime = Step;
            _player.Teleport(herAt);
            r.Boss.GetComponent<NavMeshAgent>().Warp(bossAt);
            // Its windup, its charge or its slam dropped and its walk stopped, as a daze does: a warp clears the agent's path, and the
            // walk asks for a new one only when it has stopped or she has moved (Enemy.Walk).
            r.Boss.Body.DazeFor(0.001f);
            r.Reached = r.Charges = r.ChargesCut = r.ChargesShort = 0;
            r.ShortestCharge = float.PositiveInfinity;
            float start = Time.time, first = -1f;
            while (Time.time - start < most)
            {
                _player.Teleport(herAt); // she stands
                yield return null;
                if (r.Reached > 0)
                {
                    first = Time.time - start;
                    break;
                }
            }
            Time.captureDeltaTime = 0f;
            got(first, Vector3.Distance(r.Boss.transform.position, bossAt));
        }

        /// <summary>m along the walkable ground from one point to another; negative when no whole path joins them.</summary>
        static float WalkLength(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1f;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        /// <summary>The walkable floor nearest a point, within <paramref name="within"/> m; never the top of a wall or a pier (the NavMesh has pieces up there that nothing walks to).</summary>
        static bool OnFloor(Vector3 p, float within, out Vector3 at)
        {
            bool found = NavMesh.SamplePosition(new Vector3(p.x, 0f, p.z), out NavMeshHit hit, within, NavMesh.AllAreas) && Mathf.Abs(hit.position.y) < FloorHeight;
            at = found ? hit.position : p;
            return found;
        }

        /// <summary>m over the ground under which a NavMesh point is the floor (a wall's top stands 3 m up).</summary>
        const float FloorHeight = 0.5f;
    }
}
