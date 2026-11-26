using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
            // Changed deliberately (work order 138): these pin the floor reward of before (three skills, the focus screen
            // after floor 1) on eight slots, so its six slots and its reward of three kinds are off here.
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            KitSwitchesForTests.EightSlots(_player);
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
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
            yield return null;
            _levels.SkipEntrance(); // work order 130 (changed deliberately): where that spot is past the room's point, its entrance began; skipped at once

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
            BoneBreakValues.On = false; // work order 122: the corpse as it was (its Death played, then hidden); broken, its body is hidden at its death
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
            BoneBreakValues.On = true; // work order 122
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

    /// <summary>
    /// The faults the third pair of reviews of 2026-11-20 found (work order 047), in the ControlRoom scene through the
    /// intents: the clean signs while a press would wait; a skill pressed early in a quick duck; a move key let go in a
    /// duck; a projectile in the frame it was loosed, whichever Update runs first; a leap and a Shield bash over a long
    /// frame; an elite room's drop at a floor with nothing to offer and at the act's end; Second Look's key and the guard;
    /// a bow's flash after its floor and run; the duck rim's mesh; the small bars after the camera; the pause menu's cover
    /// line; the cover key held through a teleport; the turns' values; the caster's circles.
    ///
    /// The practice brute is switched off (the template the enemies here are made from). Props are built as a level
    /// builds them. A frame without a window is about 0.3 ms: the waits count frames or real time, or game time only
    /// where the game runs; a long frame is made with <see cref="Time.captureDeltaTime"/>, never assumed.
    /// </summary>
    public class ReviewFix3Tests
    {
        const ulong RunSeed = 0x0470A047UL;
        const int MostFrames = 600;
        static readonly Vector3 Practice = new Vector3(-8f, 0f, -8f); // open ground in the practice room, away from her start
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);      // Dummy_A (4, 3) is 2.8 m away here
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        static readonly Vector3 EnemyAt = new Vector3(8f, 0f, 4f);

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        GeneratedLevels _levels;
        EnemyBrute _practiceEnemy;
        RunHud _hud;
        FeelValues _feel;
        bool _takeCoverWas, _quickDuckWas, _duckHoldWas;
        Keyboard _keyboard;
        ActsFirstInFrame _first;
        ActsLastInFrame _last;
        readonly List<Object> _made = new List<Object>();
        readonly List<(PlayerReport Report, int Frame, float Time)> _reports = new List<(PlayerReport, int, float)>();
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // nothing here is about her health
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _quickDuckWas = _feel.QuickDuck;
            _duckHoldWas = _feel.DuckHold;
            _feel.TakeCover = true;
            _feel.QuickDuck = true;
            _feel.DuckHold = false; // work order 086: the duck's tests here are the tap's (its switch off); the held duck's are QuickDuckHeldTests
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            // Changed deliberately (work order 138): these pin the floor reward of before (three skills, the focus screen
            // after floor 1) on eight slots, so its six slots and its reward of three kinds are off here.
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            KitSwitchesForTests.EightSlots(_player);
            _levels.RunValues.SkillTiers = false; // work order 057: one test counts the skill upgrades taken, which a tiered floor reward adds to
            _reports.Clear();
            _player.Reported += r => _reports.Add((r, Time.frameCount, Time.time));
            _player.Teleport(Practice);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.QuickDuck = _quickDuckWas;
                _feel.DuckHold = _duckHoldWas;
            }
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_first != null) Object.Destroy(_first.gameObject);
            if (_last != null) Object.Destroy(_last.gameObject);
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
        Vector2 ScreenMiddle => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        /// <summary>The slot of the first plain skill she has that is ready (as QuickDuckTests picks it).</summary>
        int FirstReadySkill()
        {
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot) && _player.SkillCooldownRemaining(slot) <= 0f && _player.Skills[slot].Input == SkillInput.Normal
                    && _player.Skills[slot].RecastCount <= 1 && _player.Skills[slot].MoveDistance == 0f)
                    return slot;
            Assert.Fail("no plain skill ready");
            return -1;
        }

        /// <summary>A low prop (a sarcophagus) as a level builds it, its near face <paramref name="face"/> m from <paramref name="her"/> along <paramref name="way"/>.</summary>
        GameObject LowProp(Vector3 her, Vector3 way, float face = 0.5f)
        {
            const PropModel model = PropModel.Sarcophagus;
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            Vector3 at = Flat(her) + way * (face + half);
            int rotation = (Mathf.RoundToInt(Quaternion.LookRotation(-way).eulerAngles.y / 90f) * 90) % 360;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, at, null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>One melee enemy made where it stands, from the practice enemy, awake once provoked; it does not walk.</summary>
        T Make<T>(EnemyKind kind, Vector3 at) where T : MeleeEnemy
        {
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
            Enemy e = EnemyFactory.Make(kind, at, null, _practiceEnemy, "Melee_" + kind);
            Assert.That(e, Is.Not.Null, kind + " made");
            _made.Add(e.gameObject);
            if (e is EnemyBrute brute)
            {
                brute.FloorValues = new LevelValues().FloorBruteValues(brute.Settings != null ? brute.Settings.Values : null);
                brute.enabled = true;
            }
            e.Values.MoveSpeed = 0f;
            return (T)e;
        }

        bool ShownNumber(ThingsThatFly fly, int number, out ThingsThatFly.Shown shown)
        {
            fly.ListFlying(_shown);
            foreach (ThingsThatFly.Shown s in _shown)
                if (s.Number == number)
                {
                    shown = s;
                    return true;
                }
            shown = default;
            return false;
        }

        void UseKeyboard()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
        }

        void Keys(params Key[] down) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));

        IEnumerator Press(params Key[] down)
        {
            Keys(down);
            yield return Frames(() => down.All(k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed));
        }

        /// <summary>Floor 1's reward and the focus choice taken through the intents: she is on floor 2.</summary>
        IEnumerator OnToTheNextFloor()
        {
            int floor = _levels.Floor;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.PickReward(0), Is.True, "floor " + floor + "'s reward");
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) Assert.That(_levels.PickFocusChoice(0), Is.True);
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(floor + 1));
        }

        /// <summary>Kills the elite room's enemies: its drop lies.</summary>
        IEnumerator KillTheEliteRoom()
        {
            List<Enemy> alive = _levels.Level.EliteRoomEnemies.Where(e => e != null && !e.IsDead).ToList();
            Assume.That(alive.Count, Is.GreaterThan(0), "an elite room on floor " + _levels.Floor);
            foreach (Enemy e in alive) e.Body.TakeHit(100000000);
            yield return Frames(() => _levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.EliteDrop, Is.Not.Null, "the elite's drop lies");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
        }

        // ---------------- 1. The signs while a press would wait ----------------

        [UnityTest]
        public IEnumerator TheCleanSigns_SayNotCleanWhileStunned_ForAPressThenWaitsAndIsJudgedAsOneThatWaited()
        {
            int slot = Slot("Triple Tap");
            SkillDefinition skill = _player.Skills[slot];
            Vector3 aim = GameObject.Find("Dummy_A").transform.position;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), "the first press");
            for (int press = 2; press < skill.RecastCount; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), "press " + press);
            }
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastShots == skill.RecastCount - 1
                                     && _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 3f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "free, in the middle of the clean part: a press would be clean");

            Assert.That(_player.Stun(0.05f, "test"), Is.True);
            Assert.That(_player.RecastPressWouldBeClean, Is.False, "stunned, a press waits, and one that waited is never clean");
            float stunnedAt = Time.time;
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Buffered), "the press waits");
            yield return Until(() => !_player.IsStunned, 2f);
            yield return null;
            Assert.That(_reports.Any(r => r.Report.Kind == PlayerReportKind.RecastPressed && r.Time >= stunnedAt && r.Report.Clean), Is.False,
                        "and it was not clean: the sign told the truth");
        }

        // ---------------- 2. A skill pressed early in a quick duck ----------------

        [UnityTest]
        public IEnumerator ASkill_PressedEarlyInAQuickDuck_GoesOffAsSheRises()
        {
            LowProp(Practice, Vector3.right);
            yield return null;
            int slot = FirstReadySkill();
            Assume.That(_feel.DuckSeconds - 0.1f, Is.GreaterThan(_feel.InputBuffer + 0.05f), "a press 0.1 s in is older than the buffer when she rises");
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            float down = _player.DuckDownAt;
            yield return GameTime(0.1f);
            Assume.That(_player.IsQuickDucking, Is.True);
            Assume.That(Time.time - down, Is.LessThan(_feel.DuckSeconds - _feel.InputBuffer - 0.05f), "early in the duck");
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(slot, Practice + Vector3.left * 5f), Is.EqualTo(IntentResult.Buffered), "no skill while down: it waits");
            _player.ReleaseSkill(slot);
            yield return Until(() => !_player.IsQuickDucking, 2f);
            yield return null;
            yield return null;
            Assert.That(_reports.Any(r => r.Report.Kind == PlayerReportKind.BufferDropped && r.Time >= pressedAt), Is.False, "it is not dropped");
            Assert.That(_reports.Any(r => r.Report.Kind == PlayerReportKind.SkillStarted && r.Report.Slot == slot && r.Time >= pressedAt), Is.True,
                        "it goes off as she rises, as a click does");
        }

        // ---------------- 3. A move key let go in a duck ----------------

        [UnityTest]
        public IEnumerator LettingGoOfOneOfTwoMoveKeys_DoesNotEndADuck_PressingOneAgainDoes()
        {
            LowProp(Practice, Vector3.forward);
            yield return null;
            Vector2 move = new Vector2(1f, 1f); // W and D
            bool cover = false;
            _driver.InputSource = () => new InputFrame { CursorScreen = ScreenMiddle, Move = move, CoverHeld = cover };
            yield return null;
            yield return null;
            cover = true; // the tap, W and D held
            yield return Frames(() => _player.IsQuickDucking);
            Assert.That(_player.IsQuickDucking, Is.True, "ducked by the key");
            move = new Vector2(0f, 1f); // D let go, W still down
            for (int i = 0; i < 6; i++) yield return null;
            Assume.That(Time.time, Is.LessThan(_player.DuckUpAt), "before she would rise by herself");
            Assert.That(_player.IsQuickDucking, Is.True, "a key let go is no new press: still down");
            move = new Vector2(1f, 1f); // D down again
            yield return Frames(() => !_player.IsQuickDucking);
            Assert.That(_player.IsQuickDucking, Is.False, "a key going down ends it");
            Assert.That(_reports.Last(r => r.Report.Kind == PlayerReportKind.DuckEnded).Report.DuckEnd, Is.EqualTo(DuckEnd.Moved));
        }

        // ---------------- 4. A projectile in the frame it was loosed ----------------

        [UnityTest]
        public IEnumerator AProjectile_DoesNotFlyInTheFrameItWasLoosed_WhicheverUpdateRunsFirst()
        {
            var shooter = (EnemyShooter)_levels.SpawnPractice(EnemyKind.Shooter).Single();
            shooter.Values.MoveSpeed = 0f;
            ThingsThatFly fly = ThingsThatFly.Ensure();
            fly.Values.On = true;
            _first = new GameObject("ActsFirstInFrame").AddComponent<ActsFirstInFrame>();
            _last = new GameObject("ActsLastInFrame").AddComponent<ActsLastInFrame>();
            yield return null;
            EnemyAttackDefinition arrow = shooter.Tuning.Arrows[0];
            foreach (bool beforeThePool in new[] { true, false })
            {
                int number = 0;
                Action loose = () =>
                {
                    // Away from her, so that it meets nothing for a while.
                    GroundPoint me = new GroundPoint(shooter.transform.position.x, shooter.transform.position.z);
                    GroundPoint away = me - _player.GroundPosition;
                    number = fly.Fire(shooter, arrow, ProjectileRules.Arrow(shooter.Tuning, me, away));
                };
                if (beforeThePool) _first.Act = loose;
                else _last.Act = loose;
                yield return null; // the frame it is loosed in
                string order = beforeThePool ? "loosed before the pool's Update" : "loosed after it";
                Assert.That(number, Is.GreaterThan(0), order);
                Assert.That(ShownNumber(fly, number, out ThingsThatFly.Shown s), Is.True, order);
                Assert.That(s.Flown, Is.EqualTo(0f), order + ": it waits at the bow in the frame it was loosed");
                yield return null;
                Assert.That(ShownNumber(fly, number, out s), Is.True, order);
                Assert.That(s.Flown, Is.GreaterThan(0f), order + ": and flies from the next");
            }
            _levels.ClearSpawned();
        }

        // ---------------- 5. A leap and a Shield bash over a long frame ----------------

        IEnumerator StrikesWhereItArrives(MeleeEnemy e, string attack, float standOff)
        {
            EnemyAttackDefinition a = MeleeAttacks.Find(e.AttacksNow, attack);
            yield return null;
            _player.Teleport(Flat(e.transform.position) + Vector3.back * standOff);
            yield return null;
            e.Provoke();
            yield return Until(() => e.State == EnemyState.Windup && e.Attack != null && e.Attack.Name == attack, 6f);
            Assert.That(e.Attack != null && e.Attack.Name == attack, Is.True, e.Kind + " winds up " + attack);
            Vector3 from = e.transform.position;
            int strikes = e.Strikes, landed = e.StrikesLanded, hp = _health.Current;
            try
            {
                // Frames of 0.31 s (not 0.3, whose sums fall on the windups' ends): one frame carries the windup across the move's start, the next past the strike.
                Time.captureDeltaTime = 0.31f;
                yield return Until(() => e.Strikes > strikes, 5f);
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
            Assert.That(e.Strikes, Is.EqualTo(strikes + 1), "it struck");
            Assert.That(FlatDistance(e.transform.position, from), Is.GreaterThan(standOff - 2.5f), "it arrived before the strike was tested");
            Assert.That(e.StrikesLanded, Is.EqualTo(landed + 1), "and the strike landed on her, standing where it went");
            Assert.That(hp - _health.Current, Is.GreaterThanOrEqualTo(a.Damage));
        }

        [UnityTest]
        public IEnumerator AShieldBash_OverLongFrames_StrikesWhereItsLungeArrives()
        {
            var brute = Make<EnemyBrute>(EnemyKind.Brute, EnemyAt);
            yield return StrikesWhereItArrives(brute, MeleeAttacks.ShieldBash, 4.5f);
            Assert.That(_player.IsStunned, Is.True, "and stunned her");
        }

        [UnityTest]
        public IEnumerator ALeap_WithItsFlagOn_OverLongFrames_BitesWhereItLands()
        {
            var swarmer = Make<EnemySwarmer>(EnemyKind.Swarmer, EnemyAt);
            swarmer.Tuning.Leaps = true; // the Leap as 035 built it, kept behind its flag (work order 039)
            yield return StrikesWhereItArrives(swarmer, MeleeAttacks.Leap, 5.5f);
        }

        // ---------------- 6. An elite room's drop when the floor goes on by itself ----------------

        [UnityTest]
        public IEnumerator TheElitesDrop_LeftLying_ComesFirst_WhenTheFloorHasNothingToOffer_ThenTheNextFloor()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            yield return OnToTheNextFloor(); // floor 2: no focus choice is due any more
            for (int slot = 0; slot < _player.Skills.Count; slot++) _player.EquipSkill(slot); // nothing left to offer
            yield return KillTheEliteRoom();
            foreach (Enemy e in _levels.Level.ExitEnemies.Where(x => x != null && !x.IsDead)) e.Body.TakeHit(100000000);
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || _levels.Floor != 2);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's upgrade first: it would go with the floor");
            Assert.That(_levels.Floor, Is.EqualTo(2), "still on floor 2");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1), "nothing was lost");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(_levels.Floor, Is.EqualTo(3), "then the floor goes on, as it would have");
        }

        [UnityTest]
        public IEnumerator TheElitesDrop_LeftLying_ComesFirst_AtTheActsEnd_ThenTheActIsCleared()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            for (int floor = 1; floor < _levels.Values.FloorsPerAct; floor++) yield return OnToTheNextFloor();
            yield return KillTheEliteRoom();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || _levels.ActCleared);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's upgrade first: it would be left lying");
            Assert.That(_levels.ActCleared, Is.False, "the act's end waits for it");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.ActCleared, Is.True, "then the act is cleared");
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.ActCleared));
            Assert.That(_levels.Drops.Count, Is.Zero, "nothing left lying");
        }

        // ---------------- 8. Second Look's key ----------------

        [UnityTest]
        public IEnumerator SecondLook_HasAKeyOfItsOwn_NotTheMaps_AndWaitsForTheGuard()
        {
            Assert.That(GeneratedLevels.RedrawKey, Is.Not.EqualTo(FloorMap.LargeKey), "a key of its own");
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Meta.RedrawsLeft = 1; // Second Look, as the trial's piece gives it
            UseKeyboard();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.CanRedraw, Is.True);

            yield return Press(GeneratedLevels.RedrawKey);
            Assume.That(Time.unscaledTime, Is.LessThan(_levels.ChoiceLiveFrom), "inside the guard");
            Assert.That(_levels.Modifiers.Meta.RedrawsLeft, Is.EqualTo(1), "a press inside the guard draws nothing");
            yield return Press();
            yield return Until(() => _levels.ChoiceLive, 3f);
            yield return Press(FloorMap.LargeKey);
            yield return Press();
            Assert.That(_levels.Modifiers.Meta.RedrawsLeft, Is.EqualTo(1), "the map's key spends nothing");
            Keys(GeneratedLevels.RedrawKey);
            yield return Frames(() => _levels.Modifiers.Meta.RedrawsLeft == 0);
            Assert.That(_levels.Modifiers.Meta.RedrawsLeft, Is.EqualTo(0), "its own key after the guard draws again");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the offer is still up");
        }

        // ---------------- 9. A bow's flash after its floor and run ----------------

        [UnityTest]
        public IEnumerator ABowsFlash_GoesWithItsRun_AndWithItsFloor()
        {
            ThingsThatFly fly = ThingsThatFly.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            fly.FlashAt(_player.Position + Vector3.forward * 3f);
            Assert.That(fly.FlashesLive, Is.EqualTo(1));
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(fly.FlashesLive, Is.EqualTo(0), "gone with the run");

            _levels.StartRun(RunSeed);
            yield return null;
            fly.FlashAt(_player.Position + Vector3.forward * 3f);
            Assert.That(fly.FlashesLive, Is.EqualTo(1));
            yield return OnToTheNextFloor(); // the choice holds the game: the flash does not age
            Assert.That(fly.FlashesLive, Is.EqualTo(0), "gone with the floor");
        }

        // ---------------- 10. The duck rim's mesh ----------------

        [UnityTest]
        public IEnumerator TheDuckRim_ItsObjectAndMesh_GoWithHer()
        {
            LowProp(Practice, Vector3.right);
            yield return null;
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Not.Null, "the rim shows");
            GameObject rim = _player.DuckRim.Object;
            Mesh mesh = rim.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single); // she goes with her scene
            yield return null;
            Assert.That(rim == null, Is.True, "its object is gone");
            Assert.That(mesh == null, Is.True, "and its mesh");
            _player = null;
        }

        // ---------------- 11. The small bars after the camera ----------------

        [Test]
        public void TheSmallBars_WorkOutTheirPlaces_AfterTheCameraHasMoved()
        {
            int Order(Type t) => t.GetCustomAttribute<DefaultExecutionOrder>()?.order ?? 0;
            Assert.That(Order(typeof(EnemyBars)), Is.GreaterThan(Order(typeof(CameraRig))), "the bars' LateUpdate runs after the camera's");
        }

        // ---------------- 12. The pause menu's cover line ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusKeyList_SaysTheCoverKeyIsATap_WithTheQuickDuck()
        {
            _hud = RunHud.Ensure();
            yield return null;
            string CoverKeyLine() => _hud.KeyLines().Single(l => l.StartsWith("Cover"));
            Assert.That(CoverKeyLine(), Does.Contain("tap to duck by a low prop").And.Not.Contain("(hold)"));
            // Work order 086: with the held duck on (the default since; this class turns it off for its tap tests), the line says so.
            _feel.DuckHold = true;
            Assert.That(CoverKeyLine(), Does.Contain("(hold: duck by a low prop)").And.Not.Contain("tap"));
            _feel.DuckHold = false;
            _feel.QuickDuck = false;
            Assert.That(CoverKeyLine(), Does.Contain("(hold)"), "with it off, held as before");
        }

        // ---------------- 13. The cover key held through a teleport ----------------

        [UnityTest]
        public IEnumerator TheCoverKey_HeldThroughAFloorChange_GivesNoDuckOnArrival()
        {
            Vector3 beside = Practice + Vector3.back * 6f;
            LowProp(beside, Vector3.right);
            _player.HoldCover(true); // held, with nothing in reach
            yield return null;
            Assume.That(_player.IsQuickDucking, Is.False);
            _player.Teleport(beside); // as a new floor puts her at its start
            yield return null;
            _player.HoldCover(true); // the next frame, the key as it is
            yield return null;
            Assert.That(_player.IsQuickDucking, Is.False, "a key held all along is no tap");
            Assert.That(_player.DucksTaken, Is.EqualTo(0));
            _player.HoldCover(false);
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True, "pressed again there, it ducks");
        }

        // ---------------- 14. To check ----------------

        [Test]
        public void TheTurnsValuesAndFloor_AreResetWithTheOtherStatics()
        {
            const BindingFlags Statics = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo reset = typeof(Enemy).GetMethod("ResetStatics", Statics);
            FieldInfo all = typeof(Enemy).GetField("s_All", Statics), nextId = typeof(Enemy).GetField("s_NextId", Statics);
            FieldInfo appeared = typeof(Enemy).GetField("Appeared", Statics);
            Assert.That(reset, Is.Not.Null);
            var enemies = ((IEnumerable<Enemy>)all.GetValue(null)).ToList();
            object id = nextId.GetValue(null), listeners = appeared.GetValue(null);
            var drawer = Enemy.StrikeDrawer;
            var drawerAt = Enemy.StrikeAtDrawer;
            TurnValues values = Enemy.Turns.Values;
            int floor = Enemy.Turns.Floor;
            try
            {
                Enemy.Turns.Values = new TurnValues { On = false, Shared = true };
                Enemy.Turns.Floor = 4;
                reset.Invoke(null, null); // as a new Play session begins, with domain reload off
                var fresh = new TurnValues();
                Assert.That(Enemy.Turns.Values.On, Is.EqualTo(fresh.On), "the turns' values as they start");
                Assert.That(Enemy.Turns.Values.Shared, Is.EqualTo(fresh.Shared));
                Assert.That(Enemy.Turns.Floor, Is.EqualTo(1), "and its floor");
            }
            finally
            {
                var list = (List<Enemy>)all.GetValue(null);
                list.Clear();
                list.AddRange(enemies);
                nextId.SetValue(null, id);
                appeared.SetValue(null, listeners);
                Enemy.StrikeDrawer = drawer;
                Enemy.StrikeAtDrawer = drawerAt;
                Enemy.Turns.Values = values;
                Enemy.Turns.Floor = floor;
            }
        }

        [UnityTest]
        public IEnumerator TheCastersCircles_AreNotKeptForever()
        {
            var caster = (EnemyCaster)_levels.SpawnPractice(EnemyKind.Caster).Single();
            caster.Values.MoveSpeed = 0f;
            yield return null;
            MethodInfo place = typeof(EnemyCaster).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(place, Is.Not.Null);
            EnemyAttackDefinition spikes = caster.Tuning.Enemy.Attacks.First(a => a.Name == CasterValues.BoneSpikes);
            Vector3 at = Flat(caster.transform.position); // its own feet: walkable, and 12 m from her
            for (int i = 0; i < 100; i++) place.Invoke(caster, new object[] { spikes, 1, at, Time.time, 0f, false });
            yield return Frames(() => caster.Circles.All(c => !c.IsPending));
            Assume.That(caster.Circles.All(c => !c.IsPending), Is.True, "all burst");
            place.Invoke(caster, new object[] { spikes, 1, at, Time.time, 0f, false });
            Assert.That(caster.Circles.Count, Is.LessThanOrEqualTo(EnemyCaster.KeptCircles), "the burst ones are let go of in a long session");
            Assert.That(caster.Circles.Last().IsPending, Is.True, "the new one is kept");
            _levels.ClearSpawned();
        }
    }
}
