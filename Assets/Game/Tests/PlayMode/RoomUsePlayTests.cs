using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// How much of the room she uses (work order 097; <see cref="RoomUseRules"/>), in the ControlRoom scene on a fixed seed's first combat
    /// room, the recorder on. She is walked a known path (in from the passage, past the line to 4 m short of the far wall and back, and
    /// still a while), every frame put where the path says: the log's room is the room as built, her places are in its own metres from
    /// the door she came in by, and the summary's measures from the log are those of where she stood each frame, within a cell. The
    /// enemies' deaths are where they fell. A frame that writes no place allocates nothing. A five minute fight's places stay under the
    /// bound stated (<see cref="FiveMinuteBytes"/>), and nothing is written outside a fight.
    ///
    /// She cannot be hurt. Frames without a window are short (about 0.3 ms): the walk follows game seconds, not frames.
    /// </summary>
    public class RoomUsePlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;
        /// <summary>m/s she is walked at: about her run (the feel's run speed is near 6).</summary>
        const float WalkSpeed = 5f;
        /// <summary>
        /// Bytes a five minute fight's places may come to (work order 097's bound): four lines a second at most, each about 50
        /// characters ({"t":1234.567,"type":"fight_at","d":"12.3","s":"-4.1"} and its line break), 300 s: 60 KB; with a margin, 72 KB.
        /// </summary>
        const int FiveMinuteBytes = 72 * 1024;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;
        bool _hadPref;
        int _pref;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _hadPref = PlayerPrefs.HasKey(GameSettings.GreyboxKey);
            _pref = PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeRoomUseTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
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
            }
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.GreyboxKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.GreyboxKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        GeneratedLevel StartRun()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static (LevelRoom room, LevelRoom passage, List<Enemy> own) FirstRoom(GeneratedLevel level)
        {
            LevelLayout layout = level.Layout;
            LevelRoom passage = layout.Rooms[1], room = layout.Rooms[2];
            Assert.That((passage.Kind, room.Kind), Is.EqualTo((RoomKind.Passage, RoomKind.Combat)));
            return (room, passage, level.EnemiesByRoom[room.Order]);
        }

        /// <summary>The room's props that stand, as the floor gives them to the measure (its own and the large ones, built).</summary>
        static List<LevelProp> Standing(GeneratedLevel level, LevelRoom room) =>
            level.Props.Concat(level.LargeProps).Where(p => p != null && p.Room == room && p.Prop != null && p.Object != null).Select(p => p.Prop).ToList();

        IEnumerator At(Vector3 point, LevelRoom room)
        {
            _player.Teleport(point);
            for (int i = 0; i < 30 && _levels.CurrentRoom != room; i++) yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        /// <summary>Into the first room from its passage, past the line, until the recorder follows its fight. The room, its frame and the space the test sees.</summary>
        IEnumerator IntoTheFight(Action<GeneratedLevel, LevelRoom, RoomFrame, RoomSpace, List<Enemy>> done)
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            RoomFrame f = RoomUseRules.FrameOf(level.Layout, room, door);
            RoomSpace space = RoomSpace.Make(level.Layout, room, door, f, Standing(level, room));
            yield return At(level.ToWorld(f.FromFrame(-2, 1)), passage);
            yield return At(level.ToWorld(f.FromFrame(6, 1)), room);
            yield return Until(() => _recorder.FollowsAFight, 30);
            Assert.That(_recorder.FollowsAFight, Is.True, "past the line the recorder follows the room's fight");
            done(level, room, f, space, own);
        }

        // ---------------- A known path ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator AFixedSeedFight_SheIsWalkedAKnownPath_TheSummarysMeasuresAreWhereSheStood_WithinACell()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            RoomFrame f = RoomUseRules.FrameOf(level.Layout, room, door);
            RoomSpace space = RoomSpace.Make(level.Layout, room, door, f, Standing(level, room));
            double floorDepth = space.FloorDepth;
            // Her way: along the middle of the column of cells nearest the door's middle whose floor runs deepest unbroken from the door.
            int bestCol = -1, bestRows = -1;
            for (int c = 0; c < space.Cols; c++)
            {
                int rows = 0;
                while (rows < space.Rows && space.IsWalkable(rows, c)) rows++;
                double mid = space.Left + (c + 0.5) * 2;
                if (rows > bestRows || (rows == bestRows && Math.Abs(mid) < Math.Abs(space.Left + (bestCol + 0.5) * 2))) { bestRows = rows; bestCol = c; }
            }
            double side = space.Left + (bestCol + 0.5) * 2, far = Math.Min(bestRows * 2 - 3, floorDepth - 4);
            Assume.That(far, Is.GreaterThan(12), "a way deep enough into the room");
            TestContext.WriteLine(string.Format(Inv, "ROOMUSE the room: {0} by {1} m ({2} by {3} cells, {4} of floor), {5} covers; her way at side {6:0.0} m, from 1 m in to {7:0.0} m",
                                                space.Cols * 2, space.Depth, space.Cols, space.Rows, space.WalkableCount, space.Covers.Count, side, far));

            yield return At(level.ToWorld(f.FromFrame(-2, side)), passage);
            yield return At(level.ToWorld(f.FromFrame(1, side)), room);
            // The path in game seconds: in at WalkSpeed to `far`, half a second there, back to 6 m in, two seconds there.
            double outFor = (far - 1) / WalkSpeed, backFor = (far - 6) / WalkSpeed;
            double Depth(double tau) =>
                tau < outFor ? 1 + WalkSpeed * tau
                : tau < outFor + 0.5 ? far
                : tau < outFor + 0.5 + backFor ? far - WalkSpeed * (tau - outFor - 0.5)
                : 6;
            double total = outFor + 0.5 + backFor + 2;
            var truth = new List<RoomPlace>(); // where she stood each frame, from the fight's start, in the room's own metres
            float from = Time.time;
            bool following = false;
            double fightFrom = 0; // the session's seconds the fight began at (its line's), and the game's then
            float fightFrame = 0f;
            while (Time.time - from < total)
            {
                _player.Teleport(level.ToWorld(f.FromFrame(Depth(Time.time - from), side)));
                yield return null;
                if (!following && _recorder.FollowsAFight)
                {
                    following = true;
                    fightFrom = _recorder.Events.Last(e => e.Type == RoomUseEvents.Room).T;
                    fightFrame = Time.time;
                }
                if (!following) continue;
                LevelPoint at = level.ToLevel(_player.Position);
                f.ToFrame(at.X, at.Y, out double d, out double s);
                truth.Add(new RoomPlace(fightFrom + (Time.time - fightFrame), d, s));
            }
            Assert.That(following, Is.True, "the fight began as she crossed the line");
            Assert.That(_levels.FightDoor, Is.SameAs(door), "the door she came in by");
            // Where the enemies are as they die: all of the room's, at once.
            var fell = new List<RoomPlace>();
            foreach (Enemy e in own.Where(e => e != null && !e.IsDead).ToList())
            {
                LevelPoint p = level.ToLevel(e.transform.position);
                f.ToFrame(p.X, p.Y, out double d, out double s);
                fell.Add(new RoomPlace(0, d, s));
                e.GetComponent<Damageable>().TakeHit(100000000);
            }
            yield return Until(() => !_recorder.FollowsAFight, 30);
            Assert.That(_recorder.FollowsAFight, Is.False, "cleared: the fight ends");
            string log = _recorder.LogPath;
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            SessionStats stats = SessionSummary.Compute(events);
            RoomUseRow row = stats.RoomUse.Single();
            Assert.That(row.Ended, Is.EqualTo("cleared"));
            Assert.That((row.Run, row.Floor, row.Order, row.Kind), Is.EqualTo((1, 1, room.Order, "combat")));
            Assert.That(row.Space.Walkable, Is.EqualTo(space.Walkable), "the log's room is the room as built");
            Assert.That(row.Space.Covers.Count, Is.EqualTo(space.Covers.Count));
            SessionEvent cover = events.Single(e => e.Type == FightCoverEvents.Room && e.ExtraValue("order") == room.Order.ToString(Inv));
            Assert.That(space.Covers.Count, Is.EqualTo(int.Parse(cover.ExtraValue("tall"), Inv) + int.Parse(cover.ExtraValue("low"), Inv)), "the covers work order 088 counted");
            Assert.That(events.Count(e => e.Type == RoomUseEvents.Place && e.T < row.Start), Is.EqualTo(0), "nothing before the fight");
            Assert.That(events.Count(e => e.Type == RoomUseEvents.Place && e.T > row.End + 1e-6), Is.EqualTo(0), "nor after it");

            // The truth's measures, from every frame; the log's, from its places a quarter second apart at most.
            RoomUseMeasures want = RoomUseRules.Measure(space, truth, truth[0].T, truth[truth.Count - 1].T, fell);
            RoomUseMeasures got = row.Measures;
            const double cell = RoomUseValues.CellMetres;
            Assert.That(got.Seconds, Is.EqualTo(want.Seconds).Within(0.2), "the fight's seconds");
            Assert.That(got.Farthest, Is.EqualTo(far).Within(cell), "farthest, within a cell");
            Assert.That(got.FarthestShare, Is.EqualTo(want.FarthestShare).Within(cell / floorDepth));
            Assert.That(got.MedianDepth, Is.EqualTo(want.MedianDepth).Within(cell), "median depth, within a cell");
            Assert.That(got.Travel, Is.EqualTo(want.Travel).Within(cell), "travel, within a cell");
            Assert.That(got.Circle, Is.EqualTo(want.Circle).Within(cell), "the 90 % circle, within a cell");
            int column = (int)Math.Floor(far / cell) - (int)Math.Floor(5.0 / cell) + 1; // the cells of her column from the line to `far`
            Assert.That(got.CellsUsed, Is.InRange(column - 1, column + 1), "the cells of her column");
            Assert.That(got.CellsUsed, Is.InRange(want.CellsUsed - 1, want.CellsUsed + 1));
            Assert.That(got.CellsWalkable, Is.EqualTo(space.WalkableCount));
            Assert.That(got.Deaths, Is.EqualTo(fell.Count), "every death in the fight, with its place");
            Assert.That(got.DeathsDepth, Is.EqualTo(want.DeathsDepth).Within(0.1), "where they fell");
            Assert.That((got.SprintSeconds, got.Jumps), Is.EqualTo((0d, 0)));
            Assert.That(got.CoversUsed, Is.InRange(0, got.Covers));
            int places = events.Count(e => e.Type == RoomUseEvents.Place);
            Assert.That(places, Is.LessThanOrEqualTo((int)Math.Ceiling(want.Seconds / RoomUseValues.SampleSeconds) + 2), "four a second at most");

            string line = SessionSummary.OneLine(stats.RoomUse);
            StringAssert.Contains("Room use " + line, summary);
            StringAssert.Contains("Room use in fights (work order 097)", summary);
            TestContext.WriteLine(string.Format(Inv,
                "ROOMUSE the known path's fight: {0:0.0} s, {1} places ({2} frames); floor used {3:P0} ({4} of {5} cells; the frames' {6}), farthest {7:P0} ({8:0.0} m; the frames' {9:0.0}), " +
                "median depth {10:0.0} m (the frames' {11:0.0}), travel {12:0.0} m (the frames' {13:0.0}), circle {14:0.0} m (the frames' {15:0.0}), deaths' depth {16:0.0} m, covers used {17} of {18}",
                got.Seconds, places, truth.Count, got.UsedShare, got.CellsUsed, got.CellsWalkable, want.CellsUsed, got.FarthestShare, got.Farthest, want.Farthest,
                got.MedianDepth, want.MedianDepth, got.Travel, want.Travel, got.Circle, want.Circle, got.DeathsDepth, got.CoversUsed, got.Covers));
            TestContext.WriteLine("ROOMUSE the one line: " + line);
            TestContext.WriteLine(summary.Substring(summary.IndexOf("Room use in fights", StringComparison.Ordinal)));
        }

        // ---------------- Cheap ----------------

        [UnityTest]
        public IEnumerator WhileAFightIsFollowed_AFrameThatWritesNoPlaceAllocatesNothing_AndOutsideAFightNothingIsWritten()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            // Before any fight: nothing of the measure, however she moves.
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, _) = FirstRoom(level);
            Assert.That(_recorder.FollowsAFight, Is.False);
            Assert.That(_recorder.FollowFight(_player.Position + new Vector3(3f, 0f, 0f), 1000.0), Is.False, "no fight: nothing");
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            RoomFrame f = RoomUseRules.FrameOf(level.Layout, room, door);
            yield return At(level.ToWorld(f.FromFrame(-2, 1)), passage);
            Assert.That(_recorder.Events.Count(e => e.Type.StartsWith("fight_", StringComparison.Ordinal)), Is.EqualTo(0), "nothing outside a fight");
            yield return At(level.ToWorld(f.FromFrame(6, 1)), room);
            yield return Until(() => _recorder.FollowsAFight, 30);
            Assert.That(_recorder.FollowsAFight, Is.True);

            Vector3 here = _player.Position, away = here + new Vector3(4f, 0f, 0f);
            double t0 = 5000.0;
            _recorder.FollowFight(away, t0); // a place now, whatever came before: the next ones are measured from it
            int written = _recorder.FightPlacesWritten;
            Assert.That(() => { for (int i = 0; i < 300; i++) _recorder.FollowFight(here, t0 + 0.2 * i / 300.0); }, NotAllocating, "too soon: nothing written, nothing allocated");
            Assert.That(() => { for (int i = 0; i < 300; i++) _recorder.FollowFight(away + new Vector3(0.3f * (i % 2), 0f, 0f), t0 + 1.0 + i); }, NotAllocating, "too near: nothing written, nothing allocated");
            Assert.That(_recorder.FightPlacesWritten, Is.EqualTo(written), "and none was written");
            Assert.That(_recorder.FollowFight(here, t0 + 400.0), Is.True, "far enough and late enough: written");
        }

        static byte[] s_junk;

        [UnityTest, Timeout(120000)]
        public IEnumerator AFiveMinuteFight_HerPlacesStayUnderTheStatedBound()
        {
            GeneratedLevel level = null;
            RoomFrame f = default;
            RoomSpace space = null;
            yield return IntoTheFight((l, r, fr, s, o) => { level = l; f = fr; space = s; });
            // Five minutes of running a ring of 5 m round the room's middle at 6 m/s, sixty frames a second, the times given.
            int before = _recorder.Events.Count;
            double t0 = 1000.0, radius = 5, speed = 6; // a time of a session's length, as his (29 minutes)
            for (int i = 0; i <= 300 * 60; i++)
            {
                double t = i / 60.0, a = t * speed / radius;
                _recorder.FollowFight(level.ToWorld(f.FromFrame(space.Depth / 2 + radius * Math.Sin(a), radius * Math.Cos(a))), t0 + t);
            }
            List<SessionEvent> lines = _recorder.Events.Skip(before).ToList();
            Assert.That(lines.All(e => e.Type == RoomUseEvents.Place), Is.True, "only her places");
            long bytes = lines.Sum(e => (long)System.Text.Encoding.UTF8.GetByteCount(e.ToJson()) + 1);
            TestContext.WriteLine(string.Format(Inv, "ROOMUSE a five minute fight at a run: {0} places, {1:0.0} KB, {2:0.0} bytes a line (bound {3} KB)",
                                                lines.Count, bytes / 1024.0, bytes / (double)Math.Max(1, lines.Count), FiveMinuteBytes / 1024));
            Assert.That(lines.Count, Is.LessThanOrEqualTo(300 * 4 + 1), "four a second at most");
            Assert.That(lines.Count, Is.GreaterThan(300 * 3), "and about four while she runs");
            Assert.That(bytes, Is.LessThan(FiveMinuteBytes), "under the bound");
            yield return null;
        }
    }
}
