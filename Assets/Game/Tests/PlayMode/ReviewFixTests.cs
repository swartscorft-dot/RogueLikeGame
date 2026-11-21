using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The faults the review of 2026-11-20 found where the day's work orders meet (work order 026), in the ControlRoom
    /// scene through the intents: starting a run clears what was spawned in the practice room; a press on the frame a
    /// choice lets go of the input is dropped, and so is the click that picked it; back to the practice room after a
    /// death she is alive at once; a run starts (and ends) with a clean slate of focus and cooldowns; the boss's called
    /// swarmers count as kills and into the room's count, and die when the act is cleared; a Stopping Shot that empties
    /// a stagger bar is still a counter; a hit stop never leaves the game slow; the chest prompt is found once a frame;
    /// the floor textures' filtering; the pause menu's two run lines.
    ///
    /// A paused or stopped game has a time scale of 0, and a frame without a window is about 0.3 ms: the waits here count
    /// frames or real time, or game time only where the game runs.
    /// </summary>
    public class ReviewFixTests
    {
        const ulong RunSeed = 0x0260A026UL;
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f; // real seconds until the end screen surely shows
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);        // where the boss tests stand: the boss comes 12 m north
        static readonly Vector3 Practice = new Vector3(-8f, 0f, -8f);    // open ground in the practice room, away from her start

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        GeneratedLevels _levels;
        RunHud _hud;
        readonly List<(PlayerReportKind Kind, int Frame)> _reports = new List<(PlayerReportKind, int)>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _reports.Clear();
            _player.Reported += r => _reports.Add((r.Kind, Time.frameCount));
        }

        [TearDown]
        public void CleanUp()
        {
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        /// <summary>Frames until the condition holds or <see cref="MostFrames"/> have gone: frames, so a paused game still counts.</summary>
        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        Vector2 ScreenMiddle => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        int ReportsSince(int frame, params PlayerReportKind[] kinds) => _reports.Count(r => r.Frame >= frame && kinds.Contains(r.Kind));

        IEnumerator FinishFloorAndWaitForTheReward()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
        }

        IEnumerator Die()
        {
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen is coming");
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
        }

        // ---------------- 1. What was spawned in the practice room ----------------

        [UnityTest]
        public IEnumerator StartingARun_F5OrF6_RemovesEverythingSpawnedInThePracticeRoom_TheBossAndItsBarToo()
        {
            foreach (bool sameRun in new[] { false, true })
            {
                _player.Teleport(Stand);
                yield return null;
                var made = new List<Enemy>();
                foreach (EnemyKind kind in new[] { EnemyKind.Boss, EnemyKind.Swarmer, EnemyKind.Shooter, EnemyKind.Guard })
                    made.AddRange(_levels.SpawnPractice(kind));
                var boss = (EnemyBoss)made.Single(e => e is EnemyBoss);
                yield return null;
                // Hurt: awake, and its first call brings six swarmers of its own.
                int max = boss.Body.MaxHealth;
                boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f));
                yield return null;
                yield return null;
                Assume.That(boss.Called.Count, Is.GreaterThan(0), "it called a pack");
                made.AddRange(boss.Called);
                BossHealthBar bar = boss.GetComponent<BossHealthBar>();
                Assert.That(bar.Showing, Is.True, "its bar shows in the practice room");

                if (sameRun) _levels.RestartRun();
                else _levels.StartNewRun();
                Assert.That(_levels.InRun, Is.True);
                Assert.That(bar.Showing, Is.False, "no bar from the moment the run starts");
                Assert.That(_levels.Spawned, Is.Empty);
                yield return null;
                Assert.That(made.All(e => e == null), "every one is gone, of every kind, the called ones too (" + (sameRun ? "F6" : "F5") + ")");
                _levels.BackToPracticeRoom();
                yield return null;
            }
        }

        // ---------------- 2. A press on the frame a choice lets go ----------------

        [UnityTest]
        public IEnumerator APress_OnTheFrameAChoiceLetsGo_IsDropped_TheHeldKeysComeThrough()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            int key = 0;
            Assume.That(_player.SkillOnKey(key), Is.GreaterThanOrEqualTo(0), "a skill on the first key");
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            _player.FillFocus(); // so a focus press would be taken

            var picker = new GameObject("PicksTheRewardFirst").AddComponent<PicksTheRewardFirst>();
            int read = 0;
            // On the frame the choice lets go: every press at once, the move keys and the cover key held.
            _driver.InputSource = () =>
            {
                if (Time.frameCount != picker.PickedAt) return null;
                read++;
                return new InputFrame
                {
                    CursorScreen = ScreenMiddle, Move = new Vector2(1f, 0f), CoverHeld = true,
                    SkillsPressed = 1 << key, SkillsHeld = 1 << key, ShootPressed = true, ShootHeld = true,
                    DashPressed = true, IdentityPressed = true,
                };
            };
            picker.Levels = _levels;
            yield return Frames(() => picker.PickedAt >= 0);
            for (int i = 0; i < 3; i++) yield return null;
            Object.Destroy(picker.gameObject);

            Assert.That(picker.PickedAt, Is.GreaterThan(0), "picked");
            Assert.That(read, Is.EqualTo(1), "the driver read the frame the choice let go in");
            Assert.That(ReportsSince(picker.PickedAt, PlayerReportKind.SkillPressed), Is.EqualTo(0), "no skill");
            Assert.That(ReportsSince(picker.PickedAt, PlayerReportKind.ShotClicked), Is.EqualTo(0), "no shot");
            Assert.That(ReportsSince(picker.PickedAt, PlayerReportKind.DashPressed), Is.EqualTo(0), "no dash");
            Assert.That(ReportsSince(picker.PickedAt, PlayerReportKind.FocusPressed), Is.EqualTo(0), "no focus");
            Assert.That(_player.IsEmpowered, Is.False);
            Assert.That(_player.MoveInput.sqrMagnitude, Is.GreaterThan(0.5f), "the move keys come through as they are");
            Assert.That(_player.IsCoverHeld, Is.True, "and the cover key");
        }

        [UnityTest]
        public IEnumerator TheClickThatPickedAChoice_FiresNoShot_TheNextClickDoes()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            int key = 0;
            Assume.That(_player.SkillOnKey(key), Is.GreaterThanOrEqualTo(0), "a skill on the first key");
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));

            // The click that picks, and a skill key with it, reaching the driver only on the frame after the choice let
            // go (as a click through the GUI may), the button still down; then let go; then a click of her own.
            Vector2 screen = ScreenMiddle;
            var frames = new Queue<InputFrame>();
            frames.Enqueue(new InputFrame { CursorScreen = screen, ShootPressed = true, ShootHeld = true, SkillsPressed = 1 << key, SkillsHeld = 1 << key });
            frames.Enqueue(new InputFrame { CursorScreen = screen, ShootHeld = true, SkillsHeld = 1 << key });
            frames.Enqueue(new InputFrame { CursorScreen = screen });
            frames.Enqueue(new InputFrame { CursorScreen = screen, ShootPressed = true, ShootHeld = true });
            frames.Enqueue(new InputFrame { CursorScreen = screen });
            int[] readAt = new int[frames.Count];
            int reads = 0;
            _driver.InputSource = () =>
            {
                if (frames.Count == 0) return null;
                readAt[reads++] = Time.frameCount;
                return frames.Dequeue();
            };
            Assert.That(_levels.PickReward(0), Is.True); // after the driver's Update of this frame: it was held
            Assert.That(_levels.PickFocusChoice(0), Is.True); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2. The let-go is here, as it was.
            yield return Frames(() => frames.Count == 0);
            yield return null;
            Assert.That(reads, Is.EqualTo(5));
            Assert.That(ReportsSince(readAt[0], PlayerReportKind.SkillPressed), Is.EqualTo(0), "the key that picked fires no skill");
            Assert.That(ReportsSince(readAt[0], PlayerReportKind.ShotClicked), Is.EqualTo(1), "one shot");
            Assert.That(ReportsSince(readAt[3], PlayerReportKind.ShotClicked), Is.EqualTo(1), "from the click after the button was up");
        }

        // ---------------- 3. Back to the practice room after a death ----------------

        [UnityTest]
        public IEnumerator FromTheDeathScreen_ToThePracticeRoom_SheIsAliveAtOnce_WhereItPutsHer_AndStaysThere()
        {
            _player.Teleport(Practice);
            yield return null;
            Vector3 spot = _player.Position;
            _levels.StartRun(RunSeed);
            yield return null;
            yield return Die();
            Assert.That(_health.IsAlive, Is.False);

            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(_health.IsAlive, Is.True, "alive at once");
            Assert.That(_health.Current, Is.EqualTo(_health.Max), "with full health");
            Assert.That(_health.Max, Is.EqualTo(_health.MaxHealth));
            Assert.That(Flat(_player.Position, spot), Is.LessThan(0.3f), "where the practice room puts her");
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            if (practice != null) practice.enabled = false; // nothing to hurt her while she waits
            yield return new WaitForSeconds(Placeholders.PlayerRespawnDelay + 0.5f);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(Flat(_player.Position, spot), Is.LessThan(0.3f), "no jump to the scene's start a moment later");

            // A new run from the death screen restores her too, as before.
            _levels.StartRun(RunSeed);
            yield return null;
            yield return Die();
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.Current, Is.EqualTo(_health.Max));
        }

        // ---------------- 4. A clean slate when a run starts and ends ----------------

        /// <summary>A skill and the dash cooling down, the focus window running and the gauge full again.</summary>
        IEnumerator UseEverything()
        {
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None, 3f);
            Assert.That(_player.DashToward(_player.Position + Vector3.right * 4f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            _player.FillFocus();
            int slot = _player.SkillOnKey(0);
            yield return Until(() => !_player.IsLocked && _player.SkillGateRemaining <= 0f, 3f);
            Assert.That(_player.PressSkill(slot, _player.Position + Vector3.forward * 5f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.SkillChargesReady(slot), Is.LessThan(_player.Skills[slot].Charges), "its cooldown runs");
            Assert.That(_player.DashCooldownRemaining, Is.GreaterThan(0f));
            Assert.That(_player.IsEmpowered, Is.True);
            Assert.That(_player.Focus, Is.GreaterThan(0f));
        }

        void AssertCleanSlate(string when)
        {
            Assert.That(_player.Focus, Is.EqualTo(0f), when + ": an empty gauge");
            Assert.That(_player.IsEmpowered, Is.False, when + ": no focus window");
            Assert.That(_player.DashCooldownRemaining, Is.EqualTo(0f), when + ": the dash ready");
            for (int s = 0; s < _player.Skills.Count; s++)
            {
                Assert.That(_player.SkillCooldownRemaining(s), Is.EqualTo(0f), when + ": " + _player.Skills[s].Name + " ready");
                Assert.That(_player.SkillChargesReady(s), Is.EqualTo(Mathf.Max(1, _player.Skills[s].Charges)), when + ": " + _player.Skills[s].Name + " charges");
            }
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None), when + ": no skill in progress");
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), when + ": no recast chain");
            Assert.That(_player.HasBufferedPress, Is.False, when + ": no buffered press");
            Assert.That(_player.KegsLying, Is.EqualTo(0), when + ": no keg lying");
        }

        [UnityTest]
        public IEnumerator ARun_StartsAndEnds_WithAnEmptyGauge_NoWindow_AndEverythingReady()
        {
            _player.Teleport(Practice);
            yield return null;
            yield return UseEverything();
            _levels.StartRun(RunSeed);
            AssertCleanSlate("the run started");
            yield return null;
            AssertCleanSlate("a frame later");

            yield return UseEverything();
            _levels.BackToPracticeRoom();
            AssertCleanSlate("back in the practice room");
        }

        // ---------------- 5. The swarmers the boss calls ----------------

        [UnityTest]
        public IEnumerator TheCalledSwarmers_CountAsKills_AndInTheRoomsCount_AndDieWhenTheActIsCleared_NotAsHerKills()
        {
            _health.MaxHealth = 1000000; // nothing in this test may kill her
            _levels.StartRun(RunSeed);
            yield return null;
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return FinishFloorAndWaitForTheReward();
                Assert.That(_levels.PickReward(0), Is.True, "floor " + floor);
                if (floor == 1) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice"); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
                yield return null;
            }
            Assert.That(_levels.Floor, Is.EqualTo(5));
            GeneratedLevel level = _levels.Level;
            var boss = level.Boss as EnemyBoss;
            Assert.That(boss, Is.Not.Null, "the act boss");
            LevelRoom exit = level.Layout.ExitRoom;
            double cs = level.Layout.CellSize;
            // Changed deliberately (work order 051): the south-west corner 2.5 m in, or, when the room's outline is carved there,
            // the middle of its open cell nearest that corner.
            var corner = new LevelPoint(exit.Rect.X * cs + 2.5, exit.Rect.Y * cs + 2.5);
            if (level.Layout.RoomAt(corner.X, corner.Y) != exit)
            {
                double best = double.MaxValue;
                for (int x = exit.Rect.X; x < exit.Rect.Right; x++)
                    for (int y = exit.Rect.Y; y < exit.Rect.Top; y++)
                    {
                        var mid = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs);
                        if (exit.IsOpen(x, y) && mid.DistanceTo(corner) < best)
                        {
                            best = mid.DistanceTo(corner);
                            corner = mid;
                        }
                    }
            }
            _player.Teleport(level.ToWorld(corner));
            yield return Frames(() => _levels.CurrentRoom == exit);
            Assert.That(_levels.CurrentRoom, Is.SameAs(exit), "in the boss's room");

            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f));
            yield return Frames(() => level.Called.Count >= 6);
            Assert.That(level.Called.Count, Is.EqualTo(6), "its first call");
            yield return null;
            int own = level.ExitEnemies.Count(e => e != null && !e.IsDead);
            Assert.That(_levels.EnemiesLeftInRoom, Is.EqualTo(own + 6), "the called ones are in the room's count");

            int kills = _levels.Record.Kills;
            level.Called[0].Body.TakeHit(1000000);
            yield return Frames(() => _levels.Record.Kills > kills);
            Assert.That(_levels.Record.Kills, Is.EqualTo(kills + 1), "a called swarmer she kills is a kill");
            Assert.That(_levels.EnemiesLeftInRoom, Is.EqualTo(own + 5));

            // The act is cleared with five of them alive.
            kills = _levels.Record.Kills;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.ActCleared);
            Assert.That(_levels.ActCleared, Is.True);
            Assert.That(level.Called.All(e => e.IsDead), "every called swarmer still alive dies on the spot");
            int health = _health.Current;
            yield return new WaitForSecondsRealtime(Stopped);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_health.Current, Is.EqualTo(health), "no attack of theirs lands after that");
            Assert.That(_levels.Record.Kills, Is.EqualTo(kills + own), "the room's own count as her kills, the called ones that died with the act do not");

            // 10. From the moment the act is cleared no damage reaches her at all: a strike that lands now goes through
            // the same PlayerHealth.TakeHit as every enemy's (Enemy.Land), and changes neither her health nor the record.
            int recorded = _levels.Record.HealthLeft;
            Assert.That(_health.TakeHit(25), Is.EqualTo(0), "a strike during the slow-down lands on nothing");
            Assert.That(_health.Current, Is.EqualTo(health));
            Assert.That(_levels.Record.HealthLeft, Is.EqualTo(recorded));
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(_health.TakeHit(25), Is.EqualTo(25), "and hits count again once the screen is answered");
        }

        // ---------------- 9. One screen at a time ----------------

        [UnityTest]
        public IEnumerator EscOnTheFrameTheFloorIsCleared_ThePauseComesFirst_AndTheRewardWaitsUntilResume()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests(); // the level controller sees the floor cleared in its next Update
            Assert.That(_levels.Pause(), Is.True, "Esc, handled first on that frame");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no reward under the pause menu");
            Assert.That(_levels.Paused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True);
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(_levels.PickReward(0), Is.False, "nothing hidden to pick");

            Assert.That(_levels.Resume(), Is.True);
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the reward, once resumed");
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the choice holds the pause");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.True);
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            Assert.That(_levels.PickReward(0), Is.True);
            // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "still paused");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.True);
            Assert.That(_levels.PickFocusChoice(1), Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "and the next floor runs");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
        }

        [UnityTest]
        public IEnumerator ALethalHit_OnTheFrameAChestOpens_TheDeathScreenComes_AndClosesTheChest()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
            Assert.That(_levels.OpenChest(), Is.True, "E beside the chest");
            _health.TakeHit(100000); // and the strike that kills her, on the same frame
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen comes");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "and the chest's choice is closed");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True);
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "stopped");
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
        }

        // ---------------- 13b. A hidden corpse is not animated ----------------

        [UnityTest]
        public IEnumerator ADeadEnemy_OnceHidden_IsNoLongerAnimated_ALiveOneIs()
        {
            _player.Teleport(Stand);
            yield return null;
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            made.AddRange(_levels.SpawnPractice(EnemyKind.Guard));
            yield return null;
            Enemy dead = made[0], alive = made[1];
            Assume.That(dead.Look, Is.Not.Null, "the shooter wears its model");
            Assume.That(alive.Look, Is.Not.Null, "the guard wears its model");
            dead.Body.TakeHit(1000000);
            yield return Until(() => !dead.Look.Skin.enabled, dead.ShownDeadFor(dead.Values) + 3f);
            Assert.That(dead.Look.Skin.enabled, Is.False, "its corpse is hidden");
            yield return null;
            int deadTicks = dead.Look.Ticks, aliveTicks = alive.Look.Ticks;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(dead.Look.Ticks, Is.EqualTo(deadTicks), "a hidden corpse's graph is not evaluated");
            Assert.That(alive.Look.Ticks, Is.GreaterThanOrEqualTo(aliveTicks + 5), "a live one's is, every frame");
            _levels.ClearSpawned();
        }

        // ---------------- 6. A Stopping Shot that empties a stagger bar ----------------

        static IEnumerator NextWindup(EnemyBoss boss, string name, Action<float> began, float timeout = 8f)
        {
            float at = -1f;
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (boss.State == EnemyState.Windup && boss.Attack != null && boss.Attack.Name == name)
                {
                    at = Time.time;
                    break;
                }
                yield return null;
            }
            began(at);
        }

        [UnityTest]
        public IEnumerator AStoppingShot_ThatEmptiesTheBossesBar_IsStillACounter_ItsDamageStaggerAndMarkDealtOnce()
        {
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(practice, Is.Not.Null, "the practice enemy, to copy");
            practice.enabled = false;
            _player.Teleport(Stand);
            yield return null;
            var boss = (EnemyBoss)_levels.SpawnPractice(EnemyKind.Boss).Single();
            yield return null;
            int stop = Slot("Stopping Shot");
            SkillDefinition skill = _player.Skills[stop];
            _player.EquipSkill(stop);
            Assume.That(skill.Stagger, Is.GreaterThan(0f));
            Vector3 from = boss.transform.position;
            Vector3 dir = (new Vector3(Stand.x, 0f, Stand.z) - new Vector3(from.x, 0f, from.z)).normalized;
            // Awake, with half the shot's stagger left in its bar.
            boss.Body.TakeHit(1, boss.Stagger.Max - skill.Stagger * 0.5f);
            Assume.That(boss.Stagger.Current, Is.EqualTo(skill.Stagger * 0.5f).Within(1e-3f));

            float began = -1f;
            yield return NextWindup(boss, BossValues.Charge, t => began = t);
            Assume.That(began, Is.GreaterThan(0f), "a charge at her 12 m away");
            Assume.That(boss.IsDazed, Is.False);
            _player.Teleport(from + dir * 9f);
            _health.RestoreFull();
            int hits = boss.Body.HitsTaken, hp = boss.Body.CurrentHealth, marks = boss.Body.MarkCount, counters = _player.Counters;
            float focus = _player.Focus;
            Assert.That(_player.PressSkill(stop, boss.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => boss.Body.HitsTaken > hits, 3f);
            Assert.That(boss.Body.HitsTaken, Is.EqualTo(hits + 1), "one hit");
            Assert.That(boss.Countered, Is.EqualTo(1), "a counter");
            Assert.That(_player.Counters, Is.EqualTo(counters + 1));
            Assert.That(_player.Focus - focus, Is.GreaterThanOrEqualTo(_player.Identity.MaxFocus * skill.CounterFocus - 1e-2f), "a third of the gauge");
            Assert.That(hp - boss.Body.CurrentHealth, Is.EqualTo(skill.Damage), "its damage, once");
            Assert.That(boss.Stagger.Current, Is.EqualTo(0f), "its stagger: the bar is empty");
            Assert.That(boss.Stagger.IsDazed(Time.time), Is.True, "and the bar's daze runs");
            Assert.That(boss.Body.MarkCount, Is.EqualTo(marks + skill.MarksAdded), "its mark, once");
            Assert.That(boss.IsCharging, Is.False, "the charge never starts");
            _player.EquipStartSkills();
            _levels.ClearSpawned();
        }

        // ---------------- 7. A hit stop during the end's slow-down ----------------

        [UnityTest]
        public IEnumerator AHitStop_StartedInTheEndsSlowDown_DoesNotLeaveTheNextRunSlow()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            yield return new WaitForSecondsRealtime(HudRules.EndSlowSeconds * 0.4f);
            Assume.That(Time.timeScale, Is.GreaterThan(0f).And.LessThan(1f), "slowing");
            HitStop.Trigger(0.3f);
            Assert.That(HitStop.IsActive, Is.True);
            yield return null;
            _levels.StartNewRun();
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the new run gives the time back");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            Assert.That(HitStop.IsActive, Is.False, "the hit stop is over");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "and it left the game at full speed");

            // And a hit stop that ends during the end's slow-down leaves it to the slow-down.
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            HitStop.Trigger(0.05f);
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "stopped, as the end screen wants");
        }

        // ---------------- 8. The chest in reach, once a frame ----------------

        [UnityTest]
        public IEnumerator TheChestPrompt_IsTheChestInReach_FoundOnceAFrame()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Assert.That(_levels.ChestPrompt, Is.Null, "none from the entrance");
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
            Assert.That(_levels.ChestPrompt, Is.SameAs(chest), "within reach");
            Assert.That(_levels.ChestPrompt, Is.SameAs(_levels.ChestInReach()));
        }

        // ---------------- The floor textures; the pause menu ----------------

        [Test]
        public void TheFloorTextures_AreFilteredForAFlatAngle()
        {
            var values = new CryptKitValues();
            Assume.That(values.FloorAnisoLevel, Is.GreaterThan(1));
            foreach (RoomKind kind in new[] { RoomKind.Entrance, RoomKind.Combat, RoomKind.Treasure, RoomKind.Exit })
            {
                Material m = CryptKit.MakeFloorMaterial(kind, values);
                try
                {
                    var t = m.mainTexture as Texture2D;
                    Assert.That(t, Is.Not.Null, kind + "'s floor texture");
                    Assert.That(t.anisoLevel, Is.EqualTo(values.FloorAnisoLevel), kind.ToString());
                    Assert.That(t.filterMode, Is.EqualTo(FilterMode.Trilinear), kind.ToString());
                }
                finally
                {
                    Object.Destroy(m);
                }
            }
        }

        [UnityTest]
        public IEnumerator ThePauseMenu_SaysNewRun_AndInARun_SameRunAgain_WhichDoesWhatF6Does()
        {
            _hud = RunHud.Ensure();
            yield return null;
            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuLines, Has.No.Member(RunHud.MenuLine.SameRun), "no run to do again in the practice room");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            ulong seed = _levels.RunSeed.Value;
            yield return null;

            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuText(RunHud.MenuLine.Restart), Does.StartWith("New run").And.Contains("(F5)"));
            Assert.That(_hud.MenuLines, Has.Member(RunHud.MenuLine.SameRun));
            List<RunHud.MenuLine> lines = _hud.MenuLines.ToList();
            Assert.That(lines.IndexOf(RunHud.MenuLine.SameRun), Is.EqualTo(lines.IndexOf(RunHud.MenuLine.Restart) + 1), "its own line, under it");
            Assert.That(_hud.MenuText(RunHud.MenuLine.SameRun), Does.StartWith("Same run again").And.Contains("(F6)"));
            string described = _levels.Level.Layout.Describe();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.SameRun), Is.True);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.RunSeed, Is.EqualTo(seed), "the same run");
            Assert.That(_levels.Floor, Is.EqualTo(1), "from floor 1");
            Assert.That(_levels.Level.Layout.Describe(), Is.EqualTo(described));
        }
    }
}
