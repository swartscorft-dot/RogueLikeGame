using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Work order 042 (Kristian's answers of 2026-11-20), in the ControlRoom scene through the intents and, for the keys, a
    /// virtual keyboard the level controller reads (<see cref="GeneratedLevels.TestKeyboard"/>): killing the last enemy of
    /// the elite room opens no choice and puts a drop down, on walkable ground near where it fell; standing in reach and
    /// pressing interact opens it, out of reach it does not; a key held through the kill and the pick-up picks nothing
    /// until pressed fresh after the guard; the floor's reward cannot be lost by walking on, and the elite's lying drop
    /// comes first when the floor's is picked up; the old pick intents still work in one call; with the switch off the
    /// choice opens as before, with the guard; a chest's choice has the guard.
    ///
    /// The rooms are cleared by killing their enemies here, not with FinishEliteRoomForTests or FinishFloorForTests,
    /// which bring the choice up as before. Waits are in game time or real time with a limit, or frames while the game is
    /// paused; never frames of a supposed length.
    /// </summary>
    public class RewardDropTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        const int MostFrames = 600;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        Keyboard _keyboard;

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
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false; // work order 057: these count the skill upgrades taken, which a tiered floor reward adds to
        }

        [TearDown]
        public void CleanUp()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
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

        static IEnumerator RealTime(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        IEnumerator StartRun()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true; // nothing here is about her health
        }

        /// <summary>Kills a room's enemies, the last one a frame after the others; returns where the last stood.</summary>
        static IEnumerator Kill(IReadOnlyList<Enemy> enemies, Action<Vector3> last)
        {
            List<Enemy> alive = enemies.Where(e => e != null && !e.IsDead).ToList();
            Assume.That(alive.Count, Is.GreaterThan(0));
            for (int i = 0; i < alive.Count - 1; i++) alive[i].Body.TakeHit(100000000);
            yield return null;
            yield return null;
            Vector3 at = alive[alive.Count - 1].transform.position;
            alive[alive.Count - 1].Body.TakeHit(100000000);
            last?.Invoke(at);
        }

        IEnumerator KillTheEliteRoom(Action<Vector3> last = null)
        {
            yield return Kill(_levels.Level.EliteRoomEnemies, last);
            yield return Frames(() => _levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
        }

        IEnumerator KillTheExitRoom()
        {
            yield return Kill(_levels.Level.ExitEnemies, null);
            yield return Frames(() => _levels.FloorDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None);
        }

        /// <summary>Puts her on walkable ground about a metre from a point, and lets a frame or two go.</summary>
        IEnumerator StandBy(Vector3 point, float from = 1f)
        {
            Vector3 best = point;
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = point + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * from;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                best = new Vector3(hit.position.x, 0f, hit.position.z);
                break;
            }
            _player.Teleport(best);
            yield return null;
            yield return null;
        }

        void Keys(params Key[] down) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));

        IEnumerator Press(params Key[] down)
        {
            Keys(down);
            yield return Frames(() => down.All(k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed));
        }

        void UseKeyboard()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
        }

        // ---------------- The drop ----------------

        [UnityTest]
        public IEnumerator KillingTheLastEnemyOfTheEliteRoom_OpensNoChoice_AndPutsADropDown_OnWalkableGroundNearWhereItFell()
        {
            yield return StartRun();
            GeneratedLevel level = _levels.Level;
            Vector3 fell = default;
            yield return KillTheEliteRoom(at => fell = at);

            Assert.That(_levels.EliteRoomDone, Is.True, "the room is cleared");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no choice opens");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the game goes on");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(_levels.OfferedSkillUpgrades.Count, Is.Zero, "nothing offered on screen");
            RewardDrop drop = _levels.EliteDrop;
            Assert.That(drop, Is.Not.Null, "a drop is down");
            Assert.That(_levels.Drops.Count, Is.EqualTo(1));
            Assert.That(drop.Kind, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            Assert.That(drop.Color, Is.EqualTo(EnemyFactory.EliteTint), "the elite's gold");
            Assert.That(drop.transform.IsChildOf(level.transform), "it goes with its floor");

            // Where: from where the last fell (or the room's middle), on walkable ground of the room, within a couple of metres.
            Vector3 middle = level.Middle(level.Layout.EliteRoom);
            Assert.That(Flat(drop.Fell, fell) < 0.3f || Flat(drop.Fell, middle) < 0.3f, Is.True, "from where the last fell, or the middle");
            Assert.That(Flat(drop.Landing, drop.Fell), Is.LessThanOrEqualTo(_levels.DropValues.MostLandDistance + 0.05f), "within a couple of metres");
            Assert.That(NavMesh.SamplePosition(drop.Landing, out NavMeshHit hit, 0.3f, NavMesh.AllAreas), Is.True, "on the NavMesh");
            Assert.That(Flat(hit.position, drop.Landing), Is.LessThan(0.1f), "walkable ground, not in a wall or a prop");
            Assert.That(level.RoomAt(drop.Landing), Is.SameAs(level.Layout.EliteRoom), "in the room");
            LevelPoint p = level.ToLevel(drop.Landing);
            foreach (GeneratedLevel.PlacedProp prop in level.Props)
                Assert.That(prop.Prop.Footprint.DistanceTo(p), Is.GreaterThan(0.2), "clear of " + prop.Prop.Model);

            // It flies, then lies still after about 0.8 s.
            Assert.That(drop.Settled, Is.False, "in the air at first");
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.1f);
            Assert.That(drop.Settled, Is.True, "lying still");
            Assert.That(drop.Rang, Is.True, "it rang as it came to rest");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "and still no choice");
            Assert.That(_levels.SkillUpgradeOffers, Is.EqualTo(1), "the room's one offer, waiting in it");
        }

        [UnityTest]
        public IEnumerator StandingInReach_AndPressingInteract_OpensIt_AndOutOfReach_ItDoesNot()
        {
            yield return StartRun();
            yield return KillTheEliteRoom();
            RewardDrop drop = _levels.EliteDrop;
            Assume.That(drop, Is.Not.Null);
            yield return StandBy(drop.Landing);
            Assert.That(drop.Settled, Is.False);
            Assert.That(_levels.PickUpDrop(), Is.False, "not while it is in the air");
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.1f);

            _player.Teleport(_levels.Level.PlayerStart);
            yield return null;
            yield return null;
            Assert.That(_levels.DropInReach(), Is.Null);
            Assert.That(_levels.Interact(), Is.False, "out of reach: nothing");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(_levels.EliteDrop, Is.SameAs(drop), "it stays");

            yield return StandBy(drop.Landing);
            Assert.That(_levels.DropInReach(), Is.SameAs(drop), "in reach");
            Assert.That(_levels.DropPrompt, Is.SameAs(drop), "its prompt shows");
            Assert.That(_levels.Interact(), Is.True, "the interact key picks it up");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "its choice opens");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "paused");
            Assert.That(_levels.OfferedSkillUpgrades.Count, Is.EqualTo(3), "the offer it held");
            Assert.That(_levels.Drops.Count, Is.Zero, "picked up");
            Assert.That(_levels.ChoiceLive, Is.False, "guarded at first");
            yield return RealTime(() => _levels.ChoiceLive, 3f);
            Assert.That(_levels.ChoiceLive, Is.True, "live after the guard");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AKeyHeldThroughTheKill_AndThePickUp_PicksNothing_UntilPressedFreshAfterTheGuard()
        {
            yield return StartRun();
            UseKeyboard();
            yield return Press(Key.Digit1);
            Assert.That(_keyboard.digit1Key.isPressed, Is.True, "1 held");
            yield return KillTheEliteRoom();
            RewardDrop drop = _levels.EliteDrop;
            Assume.That(drop, Is.Not.Null);
            yield return StandBy(drop.Landing);
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.1f);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "held through the kill: nothing");

            // E, with 1 still held: the drop is picked up by the key.
            yield return Press(Key.Digit1, Key.E);
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the interact key opened it");
            yield return Press(Key.Digit1);
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "1 held through the pick-up picks nothing");

            // Let go and pressed again inside the guard: nothing.
            yield return Press();
            yield return Press(Key.Digit1);
            Assume.That(Time.unscaledTime, Is.LessThan(_levels.ChoiceLiveFrom), "still inside the guard");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "a fresh press before 0.35 s picks nothing");

            // Held on past the guard: still nothing.
            yield return RealTime(() => Time.unscaledTime > _levels.ChoiceLiveFrom + 0.05f, 3f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_levels.ChoiceLive, Is.True);
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "a press from inside the guard, held, picks nothing");

            // Let go and pressed fresh after it: it picks.
            SkillUpgradeDefinition first = _levels.OfferedSkillUpgrades[0];
            yield return Press();
            Keys(Key.Digit1);
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "a fresh press after the guard picks");
            Assert.That(_levels.SkillUpgrades.Taken.Select(u => u.FullName), Is.EqualTo(new[] { first.FullName }));
        }

        [UnityTest]
        public IEnumerator TheFloorsReward_CannotBeLostByWalkingOn()
        {
            yield return StartRun();
            GeneratedLevel level = _levels.Level;
            yield return KillTheExitRoom();
            RewardDrop drop = _levels.FloorDrop;
            Assert.That(drop, Is.Not.Null, "the floor's reward lies on the ground");
            Assert.That(drop.Kind, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(drop.Color, Is.EqualTo(GeneratedLevels.FloorRewardColor));
            Assert.That(_levels.Cleared, Is.True, "the floor is cleared");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            StringAssert.Contains("pick it up", _levels.WaitingLine, "the HUD says the way on waits for it");

            // She walks about the floor, room by room, and time goes by: she is still on floor 1 and it still lies there.
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                _player.Teleport(level.Middle(room));
                yield return GameTime(0.2f);
            }
            yield return GameTime(1f);
            Assert.That(_levels.Floor, Is.EqualTo(1), "nothing takes her on without it");
            Assert.That(_levels.Level, Is.SameAs(level));
            Assert.That(_levels.FloorDrop, Is.SameAs(drop), "it is still there");
            Assert.That(_levels.SkillsTaken, Is.Empty);

            yield return StandBy(drop.Landing);
            Assert.That(_levels.PickUpDrop(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.OfferedSkills, Is.EqualTo(FloorRewards.Offer(RunSeed, 1, Enumerable.Range(0, _player.Skills.Count).Select(_player.IsEquipped).ToList(), 3)),
                        "the offer of the floor, as before");
            Assert.That(_levels.WaitingLine, Is.Empty);
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "the focus choice follows with no drop of its own");
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PickingUpTheFloorsReward_WithTheElitesLying_BringsTheEliteUpgradeFirst_ThenTheReward()
        {
            yield return StartRun();
            yield return KillTheEliteRoom();
            yield return KillTheExitRoom();
            Assert.That(_levels.Drops.Count, Is.EqualTo(2), "two drops lie");
            RewardDrop floor = _levels.FloorDrop;
            yield return StandBy(floor.Landing);
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.1f);
            Assert.That(_levels.PickUpDrop(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the elite's first: it would be lost with the floor");
            Assert.That(_levels.Drops.Count, Is.Zero, "both taken up");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "then the floor's reward, at once");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "no frame of play between");
            Assert.That(_levels.ChoiceLive, Is.False, "and guarded again");
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1), "nothing was lost");
        }

        [UnityTest]
        public IEnumerator TheOldPickIntents_StillWork_InOneCall()
        {
            yield return StartRun();
            yield return KillTheEliteRoom();
            Assume.That(_levels.EliteDrop, Is.Not.Null);
            Assert.That(_levels.PickSkillUpgrade(0), Is.True, "picks the drop up and then picks, from wherever she is");
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1));
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(_levels.Drops.Count, Is.Zero);

            yield return KillTheExitRoom();
            Assume.That(_levels.FloorDrop, Is.Not.Null);
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice));
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.SkillsTaken.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FinishingARoomWithTheTestsIntent_BringsItsChoiceUpAsBefore()
        {
            yield return StartRun();
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            Assert.That(_levels.Drops.Count, Is.Zero);
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.Drops.Count, Is.Zero);
        }

        // ---------------- The guard ----------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheChoiceOpensAsBefore_WithTheGuard()
        {
            _levels.DropValues.On = false;
            yield return StartRun();
            UseKeyboard();
            yield return Kill(_levels.Level.EliteRoomEnemies, null);
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the moment the room is cleared, as before");
            Assert.That(_levels.Drops.Count, Is.Zero, "no drop");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(_levels.ChoiceLive, Is.False, "guarded");

            yield return Press(Key.Digit1);
            Assume.That(Time.unscaledTime, Is.LessThan(_levels.ChoiceLiveFrom));
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "a press inside the guard picks nothing");
            yield return Press();
            yield return RealTime(() => _levels.ChoiceLive, 3f);
            Keys(Key.Digit2);
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1), "a fresh press after it picks");
        }

        [UnityTest]
        public IEnumerator AChestsChoice_HasTheGuard()
        {
            yield return StartRun();
            UseKeyboard();
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            yield return StandBy(chest.Object.transform.position, 1.4f);
            Assume.That(_levels.ChestInReach(), Is.SameAs(chest));
            yield return Press(Key.E);
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest), "opened with the interact key, as before");
            Assert.That(_levels.ChoiceLive, Is.False, "guarded");
            yield return Press(Key.Digit1);
            Assume.That(Time.unscaledTime, Is.LessThan(_levels.ChoiceLiveFrom));
            Assert.That(_levels.Modifiers.Taken, Is.Empty, "a press inside the guard picks nothing");
            yield return Press();
            yield return RealTime(() => _levels.ChoiceLive, 3f);
            Keys(Key.Digit1);
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Modifiers.Taken.Count, Is.EqualTo(1), "a fresh press after it picks");
        }
    }
}
