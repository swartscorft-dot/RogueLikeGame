using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 149's tests' shared room: floor 1 built alone with the switch on, its elite room's Grave Ram awake in its fight, every
    /// other enemy of the floor switched off; places in the room's own metres (d in from the door she came by, s to her right). Time in
    /// fixed steps of <see cref="Step"/>, so the measures do not depend on the machine.
    /// </summary>
    public sealed class GraveRamTestRoom
    {
        /// <summary>s of game time a frame in these tests.</summary>
        public const float Step = 1f / 60f;
        /// <summary>The template the tests are written for (R7 5.2: its two free piers 8 m to each side, 8 to 12 m in).</summary>
        public const string KnightsApse = "knights-apse";

        public GeneratedLevels Levels;
        public GeneratedLevel Level;
        public LevelRoom R;
        public LevelDoor Door;
        public RoomFrame Frame;
        public RoomSpace Space;
        public RoomFight Fight;
        public EnemyGraveRam Ram;
        public PlayerCharacter Player;
        public PlayerHealth Health;
        public ulong Seed;

        /// <summary>Strikes of the Ram's that reached her (any but out of its area or stopped), by attack; and each strike's report.</summary>
        public int Reached;
        public readonly Dictionary<string, int> ReachedBy = new Dictionary<string, int>();
        public readonly List<EnemyReport> Reports = new List<EnemyReport>();

        public Vector3 World(double d, double s) => Level.ToWorld(Frame.FromFrame(d, s));

        public string At(Vector3 w)
        {
            LevelPoint p = Level.ToLevel(w);
            Frame.ToFrame(p.X, p.Y, out double d, out double s);
            return "(d " + d.ToString("0.0", CultureInfo.InvariantCulture) + ", s " + s.ToString("0.0", CultureInfo.InvariantCulture) + ")";
        }

        public void ToFrame(Vector3 w, out double d, out double s)
        {
            LevelPoint p = Level.ToLevel(w);
            Frame.ToFrame(p.X, p.Y, out d, out s);
        }

        /// <summary>
        /// The first floor-1 seed from <paramref name="from"/> whose elite room is knights-apse, entered by a door on its south (so the
        /// room's depth runs up his screen and its side across it), with the values as given.
        /// </summary>
        public static ulong ApseSeed(LevelValues values, ulong from)
        {
            for (ulong seed = from; seed < from + 400UL; seed++)
            {
                LevelLayout layout = LevelGenerator.Generate(seed, 1, values);
                LevelRoom elite = layout.EliteRoom;
                if (elite == null || elite.Template?.Template?.Name != KnightsApse) continue;
                LevelDoor door = LevelGeometry.EntryDoor(layout, elite);
                if (door == null) continue;
                RoomFrame f = RoomUseRules.FrameOf(layout, elite, door);
                if (f.InY > 0.99) return seed;
            }
            Assert.Fail("no floor-1 seed with knights-apse entered from the south near " + from);
            return 0UL;
        }

        /// <summary>Floor 1 of the seed alone, the Ram's fight woken by her walk in past its point (she untouchable unless told).</summary>
        public static IEnumerator Build(GeneratedLevels levels, EnemyBrute practice, PlayerCharacter player, ulong seed, Action<GraveRamTestRoom> got, bool wake = true)
        {
            practice.enabled = true; // the floor's enemies are copies of it
            GeneratedLevel level = levels.Build(seed, 1);
            practice.enabled = false;
            yield return null;
            yield return null;
            var r = new GraveRamTestRoom { Levels = levels, Level = level, R = level.Layout.EliteRoom, Player = player, Seed = seed };
            r.Health = player.GetComponent<PlayerHealth>();
            Assert.That(r.R, Is.Not.Null, "floor 1's elite room");
            r.Ram = level.Elite as EnemyGraveRam;
            Assert.That(r.Ram, Is.Not.Null, "floor 1's elite is the Grave Ram");
            r.Door = LevelGeometry.EntryDoor(level.Layout, r.R);
            r.Fight = levels.FightOf(r.R);
            Assert.That(r.Fight, Is.Not.Null, "the elite room's fight");
            r.Frame = RoomUseRules.FrameOf(level.Layout, r.R, r.Door);
            r.Space = RoomSpace.Make(level.Layout, r.R, r.Door, r.Frame, r.R.Props);
            foreach (Enemy e in new List<Enemy>(Enemy.All))
                if (e != r.Ram) e.gameObject.SetActive(false);
            r.Ram.Reported += e =>
            {
                if (e.Kind != EnemyReportKind.Struck) return;
                r.Reports.Add(e);
                if (e.Outcome == StrikeOutcome.OutOfArea || e.Outcome == StrikeOutcome.StoppedByWall || e.Outcome == StrikeOutcome.StoppedByCover) return;
                r.Reached++;
                r.ReachedBy.TryGetValue(e.Attack ?? "", out int n);
                r.ReachedBy[e.Attack ?? ""] = n + 1;
            };
            if (wake) yield return r.Wake();
            got(r);
        }

        /// <summary>She walks in past the room's point: its fight is on and the Ram awake.</summary>
        public IEnumerator Wake()
        {
            Health.Untouchable = true;
            Player.Teleport(World(1.0, 0.0));
            for (int i = 0; i < 30 && Levels.CurrentRoom != R; i++) yield return null;
            Player.Teleport(World(6.0, 0.0));
            for (int i = 0; i < 120 && !(Fight.On && Ram.IsAwake); i++) yield return null;
            Assert.That(Fight.On && Ram.IsAwake, Is.True, "the fight is on and the Ram awake");
        }

        /// <summary>The walkable floor nearest a point, within <paramref name="within"/> m; never a wall's or a pier's top.</summary>
        public static bool OnFloor(Vector3 p, float within, out Vector3 at)
        {
            bool found = NavMesh.SamplePosition(new Vector3(p.x, 0f, p.z), out NavMeshHit hit, within, NavMesh.AllAreas) && Mathf.Abs(hit.position.y) < 0.5f;
            at = found ? hit.position : p;
            return found;
        }

        /// <summary>m along the walkable ground from one point to another; negative when no whole path joins them.</summary>
        public static float WalkLength(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1f;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        /// <summary>Puts the Ram at <paramref name="at"/>, whatever it was doing and its stones dropped, with its rests over and its gap waited.</summary>
        public IEnumerator Put(Vector3 at)
        {
            Ram.GetComponent<NavMeshAgent>().Warp(at);
            Ram.DropAllForTests();
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Holds the Ram at <paramref name="at"/> (put there again each frame, so it does nothing) and her at <paramref name="her"/> until its
        /// fight is <see cref="GraveRamValues.FirstAttackAfter"/> old, its opening's wait over; then it is let go (its gap after the last put
        /// still to run).
        /// </summary>
        public IEnumerator HoldPastTheStart(Vector3 at, Vector3 her)
        {
            Time.captureDeltaTime = Step;
            while (Ram.WokeAt >= 0f && Time.time < Ram.WokeAt + Ram.Tuning.FirstAttackAfter + 0.05f)
            {
                Player.Teleport(her);
                Ram.GetComponent<NavMeshAgent>().Warp(at);
                Ram.DropAllForTests();
                yield return null;
            }
            Player.Teleport(her);
            yield return Put(at);
        }

        /// <summary>
        /// Over the room's walkable 2 m grid (and its half steps): a place of hers and of the Ram's, about across his screen from each other
        /// (their depths in the room within <paramref name="across"/> m, so both stand on his screen), <paramref name="least"/> to
        /// <paramref name="most"/> m apart, the Ram's line to her clear and its Ram reaching her, with a walkable place
        /// <paramref name="aside"/> m to her side, out of its strip; and the Ram's run toward her as <paramref name="want"/> asks (its
        /// length, whether it crashes, on what, where its middle stops). The first that fits; false when none.
        /// </summary>
        public bool FindRam(Func<float, bool, LancerStopper, Vector3, Vector3, bool> want, out Vector3 her, out Vector3 ram, out Vector3 step,
                            float least = 6.5f, float most = 12f, float across = 2.5f, float aside = 2.6f)
        {
            her = ram = step = Vector3.zero;
            var cells = new List<Vector3>();
            for (double d = 1.0; d < Space.Depth; d += 1.0)
                for (double s = Space.Left + 1.0; s < Space.Left + Space.Cols * RoomUseValues.CellMetres; s += 1.0)
                    if (OnFloor(World(d, s), 0.3f, out Vector3 p) && Level.RoomAt(p) == R) cells.Add(p);
            foreach (Vector3 h in cells)
                foreach (Vector3 m in cells)
                {
                    ToFrame(h, out double hd, out _);
                    ToFrame(m, out double md, out _);
                    if (Math.Abs(hd - md) > across) continue;
                    Vector3 d = Flat(h - m);
                    float length = d.magnitude;
                    if (length < least || length > most) continue;
                    Vector3 dir = d / length;
                    if (Cover.ClearDistance(Flat(m), dir, length, Ram.Tuning.SightHeight) < length - 1e-3f) continue;
                    if (NavMesh.Raycast(m, h, out NavMeshHit _, NavMesh.AllAreas)) continue;
                    float run = Ram.RunFrom(m, dir, Ram.Tuning.RamAttack.Length, out bool crash, out _, out LancerStopper stopper);
                    if (!GraveRamRules.RamReachesHer(run, Ram.Tuning.BodyRadius, length, Placeholders.PlayerRadius)) continue;
                    if (!want(run, crash, stopper, Flat(m) + dir * run, Flat(h))) continue;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    foreach (float sign in new[] { 1f, -1f })
                    {
                        Vector3 to = h + side * (sign * aside);
                        if (!OnFloor(to, 0.3f, out Vector3 at) || NavMesh.Raycast(h, at, out NavMeshHit _, NavMesh.AllAreas)) continue;
                        her = h;
                        ram = m;
                        step = at;
                        return true;
                    }
                }
            return false;
        }

        /// <summary>Is a point within <paramref name="within"/> m of a place of the room's frame?</summary>
        public bool Near(Vector3 p, double d, double s, float within)
        {
            Vector3 w = World(d, s);
            return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(w.x, w.z)) <= within;
        }

        /// <summary>Runs game time in fixed steps until <paramref name="until"/> holds or <paramref name="most"/> s passed; <paramref name="each"/> each frame first.</summary>
        public static IEnumerator Run(float most, Func<bool> until, Action each = null)
        {
            Time.captureDeltaTime = Step;
            float start = Time.time;
            while (Time.time - start < most)
            {
                each?.Invoke();
                yield return null;
                if (until != null && until()) break;
            }
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
