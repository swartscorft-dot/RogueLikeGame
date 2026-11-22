using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// A run of five floors (work order 012, decision 0008) in the ControlRoom scene, through the intents of
    /// <see cref="GeneratedLevels"/>: a run starts at floor 1 with the start four and full health; finishing a floor
    /// offers three skills she lacks with the game paused, and picking one equips it and puts her at the next floor's
    /// entrance with her health; a chest opens once and its upgrade changes the value it names; after four floors she
    /// has eight skills, floor 5's exit room holds the act boss and finishing it clears the act; death ends the run and
    /// clears the upgrades, and since work order 018 leaves her on the floor until the death screen is answered; the practice room's values are untouched after a run; she can walk from the entrance to
    /// every room of a floor 1 and a floor 5; and the recorder writes the run.
    ///
    /// A paused game has a time scale of 0: the waits here count frames, never game time.
    /// </summary>
    public class RunTests
    {
        const ulong RunSeed = 0x12345678UL;
        const int MostFrames = 600;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;

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
            _levels.RunValues.StatsAndTiers = false; // work order 050: these tests pin today's chest steps (+10 health, +5 % damage)
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false; // work order 018: a death or a cleared act leaves an end screen up
            PlayerInputDriver.ReadRealDevices = true;
        }

        /// <summary>Frames until the condition holds or <see cref="MostFrames"/> have gone: frames, so a paused game still counts.</summary>
        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        int EquippedCount() => Enumerable.Range(0, _player.Skills.Count).Count(_player.IsEquipped);

        IEnumerator FinishFloorAndWaitForTheReward()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
        }

        [UnityTest]
        public IEnumerator ARun_StartsAtFloor1_WithTheStartFour_AndFullHealth()
        {
            _health.TakeHit(20);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(1));
            Assert.That(level.Layout.Floor, Is.EqualTo(1));
            Assert.That(level.Layout.Seed, Is.EqualTo(RunSeeds.FloorSeed(RunSeed, 1)), "floor 1's seed comes from the run's");
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                Assert.That(_player.IsEquipped(slot), Is.EqualTo(_player.Skills[slot].StartEquipped), "slot " + slot);
            Assert.That(EquippedCount(), Is.EqualTo(4));
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth), "full health");
            Assert.That(_player.Modifiers, Is.SameAs(_levels.Modifiers));
            Assert.That(Flat(_player.Position, level.PlayerStart), Is.LessThan(0.3f));
            string status = _levels.StatusText();
            StringAssert.Contains("run " + LevelSeed.ToText(RunSeed), status);
            StringAssert.Contains("floor 1 of 5", status);
            StringAssert.Contains("room 1 of " + LevelGeometry.RoomCount(level.Layout), status); // changed deliberately (work order 077): the rooms without the passages
            StringAssert.Contains("F5 new run", status);

            // F6's intent: the same run again gives the same floor.
            string described = level.Layout.Describe();
            GeneratedLevel again = _levels.RestartRun();
            yield return null;
            Assert.That(again.Layout.Describe(), Is.EqualTo(described));
        }

        [UnityTest]
        public IEnumerator FinishingAFloor_OffersThreeSkillsSheLacks_Paused_AndPickingOne_EquipsIt_AtTheNextFloorsEntrance_WithHerHealth()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.TakeHit(60); // 25 before work order 023: with the floor heal she must still be under her maximum after
            yield return FinishFloorAndWaitForTheReward();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor reward is offered");
            Assert.That(_levels.Cleared, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the game is paused");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.True);
            Assert.That(_levels.OfferedSkills.Count, Is.EqualTo(3));
            foreach (int slot in _levels.OfferedSkills) Assert.That(_player.IsEquipped(slot), Is.False, "never one she has");
            Assert.That(_levels.OfferedSkills, Is.EqualTo(FloorRewards.Offer(RunSeed, 1, Enumerable.Range(0, _player.Skills.Count).Select(_player.IsEquipped).ToList(), 3)),
                        "drawn from the run's seed");

            // Paused: frames go by and game time does not.
            float t = Time.time;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Time.time, Is.EqualTo(t), "no game time passes while the choice is up");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));

            int health = _health.Current;
            int picked = _levels.OfferedSkills[1];
            Assert.That(_levels.PickReward(1), Is.True);
            // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice), "the focus choice, still paused");
            Assert.That(_levels.Floor, Is.EqualTo(1));
            Assert.That(_levels.PickFocusChoice(0), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the game goes on");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.False);
            Assert.That(_player.IsEquipped(picked), Is.True, "the picked skill is equipped");
            Assert.That(_player.KeyOfSkill(picked), Is.EqualTo(1), "on the first free key (work order 015)");
            Assert.That(EquippedCount(), Is.EqualTo(5));
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.Level.Layout.Floor, Is.EqualTo(2));
            Assert.That(_levels.Level.Layout.Seed, Is.EqualTo(RunSeeds.FloorSeed(RunSeed, 2)));
            // Work order 023: her health carries over, and the floor heal is added to it.
            Assert.That(_health.Current, Is.EqualTo(health + FloorHeals.Amount(_health.Max, _levels.RunValues.FloorHeal)), "her health carries over, healed");
            Assert.That(_health.Current, Is.LessThan(_health.Max));
            Assert.That(_levels.SkillsTaken, Is.EqualTo(new[] { _player.Skills[picked].Name }));
            yield return null;
            Assert.That(Flat(_player.Position, _levels.Level.PlayerStart), Is.LessThan(0.3f), "at floor 2's entrance");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Time.time, Is.GreaterThan(t), "time runs again");
            Assert.That(_levels.PickReward(0), Is.False, "nothing is offered now");
        }

        GeneratedLevel.PlacedProp FirstChest() => _levels.Chests().First();

        IEnumerator StandBy(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AChest_OpensOnce_AndItsUpgradeChangesTheValueItNames()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_levels.Chests().Count, Is.EqualTo(2), "a chest in each treasure room");
            GeneratedLevel.PlacedProp chest = FirstChest();
            Assert.That(_levels.ChestInReach(), Is.Null, "not from the entrance");
            yield return StandBy(chest);
            Assert.That(_levels.ChestInReach(), Is.SameAs(chest), "within reach");

            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "paused");
            Assert.That(_levels.OfferedUpgrades.Count, Is.EqualTo(2));
            Upgrade u = _levels.OfferedUpgrades[0];

            int maxBefore = _health.Max;
            float speedBefore = _player.RunSpeedShare;
            int damageBefore = _levels.Modifiers.ScaleDamage(1000);
            int slot = Enumerable.Range(0, _player.Skills.Count).FirstOrDefault(s => _player.Skills[s].Name == u.Skill);
            float cooldownBefore = _player.CooldownOf(slot);
            int marksBefore = _levels.Modifiers.FirstMarkBonus("Quick Shot");
            Assert.That(_levels.PickUpgrade(0), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_levels.Modifiers.Taken, Is.EqualTo(new[] { u }));

            switch (u.Kind)
            {
                case UpgradeKind.MaxHealth: Assert.That(_health.Max, Is.EqualTo(maxBefore + 10)); break;
                case UpgradeKind.Damage: Assert.That(_levels.Modifiers.ScaleDamage(1000), Is.EqualTo(damageBefore + 50)); break;
                case UpgradeKind.Cooldown: Assert.That(_player.CooldownOf(slot), Is.EqualTo(cooldownBefore * 0.95f).Within(1e-4f)); break;
                case UpgradeKind.RunSpeed: Assert.That(_player.RunSpeedShare, Is.EqualTo(speedBefore * 1.05f).Within(1e-4f)); break;
                case UpgradeKind.FirstMark: Assert.That(_levels.Modifiers.FirstMarkBonus("Quick Shot"), Is.EqualTo(marksBefore + 1)); break;
            }
            StringAssert.Contains(u.Short, _levels.StatusText(), "the box lists it");

            yield return null;
            Assert.That(_levels.ChestInReach(), Is.Null, "an opened chest shows no prompt");
            Assert.That(_levels.OpenChest(chest), Is.False, "once");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
        }

        [UnityTest]
        public IEnumerator TheDamageUpgrade_RaisesTheDamageOfAHit_WhereItIsDealt()
        {
            // On the practice dummy, with a run's upgrades put on her by hand: the damage is read where it is dealt.
            var dummy = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            var dealt = new List<int>();
            _player.Reported += r => { if (r.Kind == PlayerReportKind.Hit && r.Target == dummy) dealt.Add(r.Damage); };
            var modifiers = new RunModifiers();
            modifiers.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.5f });
            _player.Modifiers = modifiers;
            _player.Shoot(dummy.transform.position);
            yield return Frames(() => dealt.Count > 0);
            _player.Modifiers = null;
            Assert.That(dealt, Is.Not.Empty, "the shot hit the dummy");
            Assert.That(dealt[0], Is.EqualTo(Mathf.RoundToInt(Placeholders.BasicShotDamage * 1.5f)), "a basic shot, half as hard again");
        }

        [UnityTest]
        public IEnumerator AfterFourFloors_SheHasEightSkills_AndFloor5sExitRoomHoldsTheBoss_AndFinishingItClearsTheAct()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return FinishFloorAndWaitForTheReward();
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "floor " + floor);
                Assert.That(_levels.PickReward(0), Is.True);
                if (floor == 1) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice"); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
                yield return null;
            }
            Assert.That(_levels.Floor, Is.EqualTo(5));
            Assert.That(EquippedCount(), Is.EqualTo(8), "eight skills after four floors");
            // Work order 015: on eight keys, one each, the four taken on the four keys that were free, in key order.
            var onKeys = Enumerable.Range(0, _player.KeyCount).Select(_player.SkillOnKey).ToList();
            Assert.That(onKeys.All(s => s >= 0), Is.True, "no key is empty");
            Assert.That(onKeys.Distinct().Count(), Is.EqualTo(8), "eight skills on eight keys");
            Assert.That(new[] { 1, 2, 3, 5 }.Select(k => _player.Skills[onKeys[k]].Name), Is.EqualTo(_levels.SkillsTaken),
                        "taken in order onto the first free key each time");
            Assert.That(_player.FirstFreeKey, Is.EqualTo(-1));
            Assert.That(_levels.SkillsTaken.Count, Is.EqualTo(4));
            GeneratedLevel level = _levels.Level;
            Assert.That(level.Boss, Is.Not.Null, "the act boss");
            Assert.That(level.ExitEnemies, Does.Contain(level.Boss), "in the exit room");
            Assert.That(level.Boss.Kind, Is.EqualTo(EnemyKind.Boss)); // a guard until work order 014
            Assert.That(level.Boss.GetComponent<Damageable>().MaxHealth, Is.EqualTo(level.Values.BossHealth));
            Assert.That(level.ExitEnemies.Count, Is.GreaterThanOrEqualTo(16), "with a full room of others");

            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.ActCleared);
            Assert.That(_levels.ActCleared, Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no reward after the last floor");
            Assert.That(_levels.InRun, Is.False, "the run is over");
            StringAssert.Contains("ACT CLEARED", _levels.StatusText());
            StringAssert.Contains(LevelSeed.ToText(RunSeed), _levels.StatusText());
            StringAssert.Contains("time", _levels.LastRunLine);
            Assert.That(_levels.Modifiers.Count, Is.EqualTo(0));

            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.IsInLevel, Is.False);
        }

        [UnityTest]
        public IEnumerator Death_EndsTheRun_AndClearsTheUpgrades()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return StandBy(FirstChest());
            Assert.That(_levels.OpenChest(), Is.True);
            _levels.PickUpgrade(0);
            Assert.That(_levels.Modifiers.Count, Is.EqualTo(1));
            LevelRoom room = _levels.CurrentRoom;

            _health.TakeHit(100000);
            yield return Frames(() => !_levels.InRun);
            Assert.That(_levels.InRun, Is.False, "death ends the run");
            Assert.That(_levels.Modifiers.Count, Is.EqualTo(0), "the upgrades are cleared");
            Assert.That(_player.Modifiers, Is.Null);
            StringAssert.Contains("DIED on floor 1, room " + (LevelGeometry.RoomNumber(_levels.Level.Layout, room) + 1) + " (", _levels.LastRunLine); // changed deliberately (work order 077): counted without the passages
            StringAssert.Contains(LevelSeed.ToText(RunSeed), _levels.LastRunLine);

            // Work order 018: she stays on the floor, the game stopped, until the death screen is answered.
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died));
            yield return new WaitForSecondsRealtime(HudRules.EndSlowSeconds + 0.3f);
            Assert.That(_levels.IsInLevel, Is.True, "still on the floor");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the game has stopped");
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(_levels.IsInLevel, Is.False, "back in the practice room");
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            yield return new WaitForSeconds(Placeholders.PlayerRespawnDelay + 0.5f);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.Max, Is.EqualTo(_health.MaxHealth), "her maximum is her own again");
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
        }

        [UnityTest]
        public IEnumerator ThePracticeRoomsValues_AreUntouchedAfterARun()
        {
            float[] cooldowns = _player.Skills.Select(s => s.Cooldown).ToArray();
            int[] damages = _player.Skills.Select(s => s.Damage).ToArray();
            float runSpeed = _player.Feel.RunSpeed;
            int maxHealth = _health.MaxHealth;

            _levels.StartRun(RunSeed);
            yield return null;
            foreach (GeneratedLevel.PlacedProp chest in _levels.Chests())
            {
                yield return StandBy(chest);
                Assert.That(_levels.OpenChest(), Is.True);
                _levels.PickUpgrade(0);
            }
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.2f, Skill = "Quick Shot" });
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.RunSpeed, Amount = 0.2f });
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 30f });
            _health.RefreshMax();
            Assert.That(_player.CooldownOf(0), Is.LessThan(cooldowns[0]), "in the run, Quick Shot's cooldown is cut");
            _levels.BackToPracticeRoom();
            yield return null;

            Assert.That(_player.Skills.Select(s => s.Cooldown).ToArray(), Is.EqualTo(cooldowns), "the skills' settings");
            Assert.That(_player.Skills.Select(s => s.Damage).ToArray(), Is.EqualTo(damages));
            Assert.That(_player.Feel.RunSpeed, Is.EqualTo(runSpeed));
            Assert.That(_health.MaxHealth, Is.EqualTo(maxHealth));
            Assert.That(_health.Max, Is.EqualTo(maxHealth));
            Assert.That(_player.Modifiers, Is.Null);
            Assert.That(_player.CooldownOf(0), Is.EqualTo(cooldowns[0]));
            Assert.That(_player.RunSpeedShare, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SheCanWalk_FromTheEntrance_ToEveryRoom_OnAFloor1_AndAFloor5()
        {
            foreach (int floor in new[] { 1, 5 })
            {
                GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                yield return null;
                LevelLayout layout = level.Layout;
                Vector3 Ground(Vector3 p)
                {
                    Assert.That(NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas), "NavMesh at " + p + "\n" + LevelMap.Draw(layout));
                    return hit.position;
                }
                Vector3 start = Ground(level.PlayerStart);
                var path = new NavMeshPath();
                foreach (LevelRoom r in layout.Rooms)
                {
                    Assert.That(NavMesh.CalculatePath(start, Ground(level.Middle(r)), NavMesh.AllAreas, path), "a path to " + r.Letter + r.Order + " floor " + floor);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "the whole way to " + r.Letter + r.Order + " floor " + floor + "\n" + LevelMap.Draw(layout));
                }
            }
        }

        [UnityTest]
        public IEnumerator TheRecorder_WritesTheRun()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RunTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(RunSeed);
            yield return null;
            yield return StandBy(FirstChest());
            _levels.OpenChest();
            string taken = _levels.OfferedUpgrades[1].Short;
            _levels.PickUpgrade(1);
            yield return FinishFloorAndWaitForTheReward();
            string skill = _player.Skills[_levels.OfferedSkills[0]].Name;
            _levels.PickReward(0);
            Assert.That(_levels.PickFocusChoice(1), Is.True); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
            yield return null;
            _health.TakeHit(100000);
            yield return Frames(() => !_levels.InRun);

            List<SessionEvent> events = _recorder.Events.ToList();
            string seed = LevelSeed.ToText(RunSeed);
            Assert.That(events.Single(e => e.Type == SessionEventTypes.RunStart).Name, Is.EqualTo(seed));
            Assert.That(events.Where(e => e.Type == SessionEventTypes.FloorEnter).Select(e => e.ExtraValue("floor")), Is.EqualTo(new[] { "1", "2" }));
            SessionEvent finish = events.Single(e => e.Type == SessionEventTypes.FloorFinish);
            Assert.That(finish.ExtraValue("floor"), Is.EqualTo("1"));
            Assert.That(finish.ExtraValue("health"), Is.Not.Null);
            Assert.That(events.Single(e => e.Type == SessionEventTypes.RewardOffer).ExtraValue("offered").Split('|').Length, Is.EqualTo(3));
            Assert.That(events.Single(e => e.Type == SessionEventTypes.RewardTake).Name, Is.EqualTo(skill));
            SessionEvent chest = events.Single(e => e.Type == SessionEventTypes.ChestOpen);
            Assert.That(chest.Name, Is.EqualTo(taken));
            StringAssert.Contains(" | ", chest.ExtraValue("offered"));
            SessionEvent end = events.Single(e => e.Type == SessionEventTypes.RunEnd);
            Assert.That(end.Result, Is.EqualTo("death"));
            Assert.That(end.ExtraValue("floor"), Is.EqualTo("2"));

            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Run " + seed + "   floors reached 2   ended: death on floor 2", summary);
            StringAssert.Contains("  skills taken     " + skill, summary);
            StringAssert.Contains("  upgrades taken   " + taken, summary);
        }
    }
}
