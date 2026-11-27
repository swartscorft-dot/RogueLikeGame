using System.Collections;
using System.Collections.Generic;
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
    /// Work order 149: the Grave Ram is never parked (work order 146's measure, written for it; the owner killed the boss in 27 s
    /// standing still because it parked itself behind a pier's corner). On floor 1's elite room, built alone with the switch on and its
    /// fight woken: she stands still (untouchable, so a strike that reaches her is counted and does not end the measure) at every walkable
    /// 2 m cell of the room (every other one, 4 m, in the second and third rooms), the doorway among them, and the Ram is put at each of
    /// three start points (its own place at the apse mouth, 80 % in and a quarter across, 30 % in and 80 % across). A strike must reach
    /// her within the walk to her at its speed and <see cref="MostSlack"/>; nor may it crash while she stands still (a free daze is a
    /// cheese). The camera's picture is his 16:9; time runs in fixed steps. The results are written to
    /// <c>captures\149\never-parked.txt</c> when that folder exists.
    /// </summary>
    public class GraveRamNeverParkedTests
    {
        /// <summary>
        /// s more than the walk to her at its speed it may take to reach her: its gap after a put (1 s), a Ram's tell, run and recovery
        /// (1.2 + 1.3 + 1.0), and the stones' 1 s burst, with a second to spare. One that stands short of her longer than this is parked.
        /// </summary>
        const float MostSlack = 6f;
        const ulong From = 0x149A0UL;
        const string Captures = @"C:\Users\kriss\.claude-automation\runner\captures\149";

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        LevelValues _values;
        bool _uniqueBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _uniqueBefore = EliteValues.Unique;
            EliteValues.Unique = true;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            _levels = GeneratedLevels.Ensure();
            _values = new LevelValues();
            _levels.Values = _values;
            _levels.RunValues.SkillTiers = false;
            if (Camera.main != null) Camera.main.aspect = 16f / 9f;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            EliteValues.Unique = _uniqueBefore;
            EliteHurtGuard.Clear();
            if (_health != null) _health.Untouchable = false;
            if (Camera.main != null) Camera.main.ResetAspect();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        /// <summary>Floor-1 seeds whose elite rooms are entered from three different sides, the first from the south.</summary>
        List<ulong> Seeds()
        {
            var seeds = new List<ulong> { GraveRamTestRoom.ApseSeed(_values, From) };
            var turns = new HashSet<int> { 0 };
            for (ulong seed = From + 1000UL; seed < From + 1400UL && seeds.Count < 3; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, 1, _values);
                LevelRoom elite = layout.EliteRoom;
                if (elite == null) continue;
                LevelDoor door = LevelGeometry.EntryDoor(layout, elite);
                if (door == null) continue;
                RoomFrame f = RoomUseRules.FrameOf(layout, elite, door);
                int turn = f.InY > 0.5 ? 0 : f.InX > 0.5 ? 1 : f.InY < -0.5 ? 2 : 3;
                if (turns.Add(turn)) seeds.Add(seed);
            }
            return seeds;
        }

        [UnityTest, Timeout(3000000)]
        public IEnumerator EveryStart_EveryPlaceSheStands_ItReachesHer_AndNeverCrashesWhileSheStandsStill()
        {
            var faults = new List<string>();
            var lines = new List<string>();
            int cases = 0;
            var templates = new HashSet<string>();
            List<ulong> seeds = Seeds();
            for (int k = 0; k < seeds.Count; k++)
            {
                GraveRamTestRoom r = null;
                yield return GraveRamTestRoom.Build(_levels, _practiceEnemy, _player, seeds[k], x => r = x);
                templates.Add(r.R.Template?.Template?.Name ?? "none");
                Assert.That(GraveRamTestRoom.OnFloor(r.World(1.0, 0.0), 1f, out Vector3 door), Is.True, "inside the door");
                double depth = r.Space.Depth, width = r.Space.Cols * RoomUseValues.CellMetres, left = r.Space.Left;
                var starts = new List<Vector3> { r.Ram.Home };
                if (GraveRamTestRoom.OnFloor(r.World(depth * 0.8, left + width * 0.25), 3f, out Vector3 a)) starts.Add(a);
                if (GraveRamTestRoom.OnFloor(r.World(depth * 0.3, left + width * 0.8), 3f, out Vector3 b)) starts.Add(b);
                starts.RemoveAll(st => GraveRamTestRoom.WalkLength(st, door) < 0f);
                int every = k == 0 ? 1 : 2;
                var places = new List<Vector3>();
                if (GraveRamTestRoom.OnFloor(r.World(0.6, 0.0), 0.5f, out Vector3 doorway)) places.Add(doorway);
                for (int row = 0; row < r.Space.Rows; row += every)
                    for (int col = 0; col < r.Space.Cols; col += every)
                        if (r.Space.IsWalkable(row, col)
                            && GraveRamTestRoom.OnFloor(r.World((row + 0.5) * RoomUseValues.CellMetres, left + (col + 0.5) * RoomUseValues.CellMetres), 1f, out Vector3 p))
                            places.Add(p);
                foreach (Vector3 herAt in places)
                {
                    if (GraveRamTestRoom.WalkLength(door, herAt) < 0f) continue;
                    foreach (Vector3 ramAt in starts)
                    {
                        float walk = GraveRamTestRoom.WalkLength(ramAt, herAt);
                        if (walk < 0f)
                        {
                            faults.Add(LevelSeed.ToText(seeds[k]) + ": she at " + r.At(herAt) + ", which it cannot walk to from " + r.At(ramAt));
                            continue;
                        }
                        float most = walk / r.Ram.Values.MoveSpeed + MostSlack;
                        cases++;
                        _player.Teleport(herAt);
                        yield return r.Put(ramAt);
                        r.Reached = 0;
                        r.ReachedBy.Clear();
                        int crashes = r.Ram.Crashes;
                        float start = Time.time, first = -1f;
                        yield return GraveRamTestRoom.Run(most, () => r.Reached > 0, () => _player.Teleport(herAt));
                        if (r.Reached > 0) first = Time.time - start;
                        string by = string.Join(",", r.ReachedBy.Keys);
                        lines.Add(LevelSeed.ToText(seeds[k]) + "\t" + r.At(herAt) + "\t" + r.At(ramAt) + "\t" + (first < 0f ? "none" : first.ToString("0.0")) + "\t" + by
                                  + "\t" + (r.Ram.Crashes - crashes));
                        if (first < 0f)
                            faults.Add(LevelSeed.ToText(seeds[k]) + ": she at " + r.At(herAt) + ", it from " + r.At(ramAt) + ": nothing reached her in "
                                       + most.ToString("0.0") + " s; it ended at " + r.At(r.Ram.transform.position) + ", on screen " + r.Ram.BodyOnScreen()
                                       + ", line clear " + r.Ram.LineClear());
                        if (r.Ram.Crashes > crashes)
                            faults.Add(LevelSeed.ToText(seeds[k]) + ": she at " + r.At(herAt) + ", it from " + r.At(ramAt) + ": it crashed while she stood still");
                    }
                }
            }
            Time.captureDeltaTime = 0f;
            string head = "Work order 149's measure: " + cases + " places and starts in " + seeds.Count + " elite rooms (" + string.Join(", ", templates) + "); "
                          + faults.Count + " faults";
            Debug.Log(head);
            if (Directory.Exists(Captures))
                File.WriteAllLines(Path.Combine(Captures, "never-parked.txt"), new[] { head }.Concat(faults).Concat(new[] { "", "seed\ther\tram from\tfirst strike s\tby\tcrashes" }).Concat(lines));
            Assert.That(seeds.Count, Is.GreaterThanOrEqualTo(2), "rooms entered from more than one side");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }
    }
}
