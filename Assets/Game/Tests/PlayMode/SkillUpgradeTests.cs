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
    /// Work order 017 (2026-11-20), through the intents in the ControlRoom scene. In a run: clearing the elite room offers
    /// three skill upgrades with the game paused, and picking one changes the skill the character reads; a new run has
    /// none and the practice room's skills are untouched; an elite room left uncleared offers nothing; with nothing left
    /// to offer she is healed instead; the elite is larger, tougher and shown as one; a whole act gives five offers. In
    /// the practice room, through the same mechanism a run uses (<see cref="PlayerCharacter.SkillUpgrades"/>): every
    /// upgrade of the kind "change" by its effect, and three of the kind "numbers".
    ///
    /// Waits are in game time or real time with a limit, or frames while the game is paused; never frames of a supposed length.
    /// </summary>
    public class SkillUpgradeTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        const int MostFrames = 600;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here
        static readonly Vector3 OpenGround = new Vector3(-8f, 0f, -8f);

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<int> _damage = new List<int>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.MeleeYouRead = false; // work order 035: these tests are of the swarmer's, brute's and guard's attacks from before
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _damage.Clear();
            _a.Damaged += d => _damage.Add(d);
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
            if (_player != null) _player.SkillUpgrades = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        int Slot(string name)
        {
            int slot = _player.SlotOf(name);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no skill " + name);
            return slot;
        }

        void Give(string skill, string name)
        {
            SkillUpgradeDefinition row = SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, name);
            Assert.That(row, Is.Not.Null, skill + ": " + name);
            Give(row);
        }

        void Give(SkillUpgradeDefinition row)
        {
            if (_player.SkillUpgrades == null) _player.SkillUpgrades = new RunSkillUpgrades();
            _player.SkillUpgrades.Take(row);
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Frames until the condition holds or <see cref="MostFrames"/> have gone: frames, so a paused game still counts.</summary>
        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        static string Fields(SkillDefinition s) =>
            string.Join(";", typeof(SkillDefinition).GetFields().Select(f => f.Name + "=" + f.GetValue(s)));

        // ---------------- The elite room in a run ----------------

        IEnumerator ClearTheEliteRoom()
        {
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
        }

        [UnityTest]
        public IEnumerator ClearingTheEliteRoom_OffersThreeUpgradesOfHerSkills_Paused_AndPickingOne_ChangesTheSkill()
        {
            _levels = GeneratedLevels.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_player.SkillUpgrades, Is.SameAs(_levels.SkillUpgrades));
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero);
            yield return ClearTheEliteRoom();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the choice comes up");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "paused");
            Assert.That(PlayerInputDriver.HeldByChoice, Is.True);
            IReadOnlyList<SkillUpgradeDefinition> offered = _levels.OfferedSkillUpgrades;
            Assert.That(offered.Count, Is.EqualTo(3));
            var had = new HashSet<string>(Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).Select(s => _player.Skills[s].Name));
            Assert.That(offered.All(u => had.Contains(u.Skill)), "skills she has");
            Assert.That(offered.Select(u => u.Skill).Distinct().Count(), Is.EqualTo(3));
            Assert.That(offered.Any(u => u.Kind == SkillUpgradeKind.Change));
            Assert.That(offered.Select(u => u.FullName),
                        Is.EqualTo(SkillUpgradeOffer.Offer(RunSeed, 1, SkillUpgradeTable.Create(), had, Array.Empty<SkillUpgradeDefinition>(), 3).Select(u => u.FullName)),
                        "drawn from the run's seed and the floor");
            float t = Time.time;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Time.time, Is.EqualTo(t), "no game time passes while the choice is up");

            SkillUpgradeDefinition pick = offered[1];
            int slot = Slot(pick.Skill);
            string before = Fields(_player.Skills[slot]);
            Assert.That(_levels.PickSkillUpgrade(1), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the game goes on");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(Fields(_player.Skills[slot]), Is.Not.EqualTo(before), "the skill she reads has changed");
            Assert.That(Fields(_player.Skills[slot]), Is.EqualTo(Fields(RunSkillUpgrades.Patched(_player.SettingsSkills[slot], new[] { pick }))));
            Assert.That(_player.UpgradesOn(slot), Is.EqualTo(1), "a pip on its box");
            Assert.That(_levels.Record.SkillUpgrades, Is.EqualTo(new[] { pick.FullName }));
            Assert.That(_levels.Record.SkillUpgradesText, Is.EqualTo(pick.FullName));

            // Once: the room stays cleared, and nothing more comes up on this floor.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None));
            Assert.That(_levels.SkillUpgradeOffers, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ANewRun_HasNone_AndThePracticeRoomsSkillsAreUntouched_AfterARun()
        {
            string settingsBefore = string.Join("|", _player.SettingsSkills.Select(Fields));
            _levels = GeneratedLevels.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            yield return ClearTheEliteRoom();
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            Assert.That(_levels.SkillUpgrades.Count, Is.EqualTo(1));
            Assert.That(string.Join("|", _player.SettingsSkills.Select(Fields)), Is.EqualTo(settingsBefore), "the settings are never written");

            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_player.SkillUpgrades, Is.Null, "out of a run: the settings' skills");
            Assert.That(_player.Skills, Is.SameAs(_player.SettingsSkills));
            Assert.That(string.Join("|", _player.Skills.Select(Fields)), Is.EqualTo(settingsBefore), "the practice room's skills as they were");
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero);

            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "a new run starts with none");
            Assert.That(_player.Skills, Is.SameAs(_player.SettingsSkills));
        }

        [UnityTest]
        public IEnumerator AnEliteRoomLeftUncleared_OffersNothing_AndNothingLeft_HealsHerInstead()
        {
            _levels = GeneratedLevels.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor's reward, not an upgrade");
            Assert.That(_levels.SkillUpgradeOffers, Is.Zero);
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.PickFocusChoice(0), Is.True); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.SkillUpgrades.Count, Is.Zero, "the room walked past gave nothing");

            // Floor 2, with no upgrade left in the table: a full heal instead, said on screen.
            _levels.UpgradeValues.Table = Array.Empty<SkillUpgradeDefinition>();
            _health.TakeHit(30);
            Assume.That(_health.Current, Is.LessThan(_health.Max));
            yield return ClearTheEliteRoom();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "no choice");
            Assert.That(_levels.EliteRoomDone, Is.True);
            Assert.That(_health.Current, Is.EqualTo(_health.Max), "healed in full");
            StringAssert.Contains("healed", _levels.Notice);
        }

        [UnityTest]
        public IEnumerator TheElite_IsLarger_Tougher_AndShownAsOne()
        {
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            Enemy elite = level.Elite;
            Assert.That(elite, Is.Not.Null, "an elite on floor 1");
            Assert.That(elite.IsElite, Is.True);
            Assert.That(elite.Kind, Is.EqualTo(EnemyKind.Brute), "a brute on floor 1");
            Assert.That(level.EliteRoomEnemies, Does.Contain(elite));
            Assert.That(level.RoomAt(elite.transform.position), Is.SameAs(level.Layout.EliteRoom));
            Enemy plain = level.Enemies.First(e => e.Kind == EnemyKind.Brute && !e.IsElite);
            LevelValues v = _levels.Values;
            Assert.That(elite.Body.MaxHealth, Is.EqualTo(3 * v.EnemyHealth), "three times a brute's health (work order 030, lever C; four before)");
            Assert.That(elite.Body.CurrentHealth, Is.EqualTo(elite.Body.MaxHealth));
            Assert.That(plain.Body.MaxHealth, Is.EqualTo(v.EnemyHealth));
            // Work order 030, lever B: floor 1's enemies deal 0.7 of their attacks, and an elite's quarter more multiplies with it.
            float floorScale = v.DamageScaleOf(1);
            Assert.That(floorScale, Is.EqualTo(0.7f));
            Assert.That(elite.DamageScale, Is.EqualTo(1.25f * floorScale).Within(1e-5f), "a quarter more damage, times floor 1's");
            Assert.That(plain.DamageScale, Is.EqualTo(floorScale).Within(1e-6f));
            Assert.That(elite.transform.localScale.x / plain.transform.localScale.x, Is.EqualTo(4f / 3f).Within(1e-4f), "a third larger");
            Assert.That(elite.Body.Radius / plain.Body.Radius, Is.EqualTo(4f / 3f).Within(1e-4f));
            Assert.That(elite.Body.Stagger, Is.Not.Null, "a stagger bar");
            Assert.That(elite.Body.Stagger.Max, Is.EqualTo(new GuardValues().Stagger.Max * 0.5f).Within(1e-4f), "half the guard's");
            Assert.That(plain.Body.Stagger, Is.Null);
            Assert.That(elite.BodyColor, Is.EqualTo(EnemyFactory.EliteTint));
            Assert.That(elite.LogWord, Is.EqualTo("elite brute"));
            Assert.That(plain.LogWord, Is.EqualTo("brute"));
            Assert.That(level.Boss, Is.Null);

            // Its strikes: a quarter more than the attack's damage. Stand in its reach and let one land.
            int took = -1;
            elite.Reported += r => { if (r.Kind == EnemyReportKind.Struck && r.Outcome == StrikeOutcome.Landed && took < 0) took = r.Damage; };
            _player.Teleport(elite.transform.position + Vector3.back * 1.5f);
            yield return Until(() => took >= 0 || !_health.IsAlive, 8f);
            Assume.That(took, Is.GreaterThan(0), "it struck her");
            int[] scaled = elite.Values.Attacks.Select(a => Mathf.RoundToInt(a.Damage * elite.DamageScale)).ToArray();
            Assert.That(scaled, Does.Contain(took), "a quarter more than one of its attacks, times floor 1's 0.7");
        }

        [UnityTest]
        public IEnumerator AWholeAct_ThroughTheIntents_GivesFiveOffers_AndTheRecorderWritesThem()
        {
            _folder = Path.Combine(Path.GetTempPath(), "SkillUpgradeTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            // Into the elite room once, so the recorder sees her enter it.
            Vector3 middle = level.Middle(level.Layout.EliteRoom);
            if (NavMesh.SamplePosition(middle, out NavMeshHit onMesh, 4f, NavMesh.AllAreas)) middle = onMesh.position;
            _player.Teleport(middle);
            yield return Frames(() => _levels.EliteRoomEntered);
            Assert.That(_levels.EliteRoomEntered, Is.True);
            for (int floor = 1; floor <= 5; floor++)
            {
                Assert.That(_levels.Floor, Is.EqualTo(floor));
                yield return ClearTheEliteRoom();
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "floor " + floor);
                Assert.That(_levels.PickSkillUpgrade(floor % 3), Is.True);
                yield return null;
                _levels.FinishFloorForTests();
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
                if (floor < 5)
                {
                    Assert.That(_levels.PickReward(0), Is.True);
                    if (floor == 1) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice"); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
                    yield return null;
                }
            }
            Assert.That(_levels.ActCleared, Is.True);
            Assert.That(_levels.SkillUpgradeOffers, Is.EqualTo(5), "five offers in an act");
            Assert.That(_levels.Record.SkillUpgrades.Count, Is.EqualTo(5));
            Assert.That(_levels.Record.SkillUpgrades.Distinct().Count(), Is.EqualTo(5), "never one taken twice");

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EliteEnter), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EliteClear), Is.EqualTo(5));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.SkillUpgradeOffer), Is.EqualTo(5));
            List<string> taken = events.Where(e => e.Type == SessionEventTypes.SkillUpgradeTake).Select(e => e.Name).ToList();
            Assert.That(taken, Is.EqualTo(_levels.Record.SkillUpgrades), "in order");
            SessionStats stats = SessionSummary.Compute(events.ToList());
            Assert.That(stats.Runs.Last().SkillUpgrades, Is.EqualTo(taken));
            Assert.That(events.Any(e => e.Enemy == "elite brute"), Is.True, "the log names the elite");
        }

        // ---------------- The start skills' upgrades, by their effect ----------------

        [UnityTest]
        public IEnumerator QuickShot_DoubleTap_FiresTwiceInARow()
        {
            int q = Slot("Quick Shot");
            _player.Teleport(NearA);
            yield return null;
            Give("Quick Shot", "Double Tap");
            Assert.That(_player.SkillChargesReady(q), Is.EqualTo(2));
            Assert.That(_player.PressSkill(q, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            yield return UntilFree();
            Assert.That(_player.SkillChargesReady(q), Is.EqualTo(1));
            Assert.That(_player.PressSkill(q, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second charge");
            yield return Until(() => _damage.Count > 1, 2f);
            Assert.That(_damage.Count, Is.EqualTo(2));
            yield return UntilFree();
            Assert.That(_player.PressSkill(q, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "not a third");
        }

        [UnityTest]
        public IEnumerator QuickShot_QuickDraw_EveryBasicShotThatHits_Takes03SecondsOffItsCooldown()
        {
            int q = Slot("Quick Shot");
            Give("Quick Shot", "Quick Draw");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(q, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            yield return UntilFree();
            float r0 = _player.SkillCooldownRemaining(q), t0 = Time.time;
            int hits = _player.BasicShotHits;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotHits > hits, 2f);
            Assert.That(_player.BasicShotHits, Is.EqualTo(hits + 1));
            float r1 = _player.SkillCooldownRemaining(q);
            Assert.That(r1, Is.EqualTo(r0 - (Time.time - t0) - 0.3f).Within(0.02f), "0.3 s off besides the time gone");
        }

        [UnityTest]
        public IEnumerator TripleTap_FourthTap_FourPresses_AndTheCleanOneIsTheFourth()
        {
            int t = Slot("Triple Tap");
            Give("Triple Tap", "Fourth Tap");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(t, _a.transform.position), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= 4; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen, 2f);
                Assert.That(_player.IsRecastWindowOpen, Is.True, "a window for press " + press);
                if (press < 4) Assert.That(_player.RecastCleanShare, Is.EqualTo(0f), "press " + press + " is not the clean one");
                else Assert.That(_player.RecastCleanShare, Is.GreaterThan(0f), "the fourth is the clean one");
                Assert.That(_player.PressSkill(t, _a.transform.position), Is.EqualTo(IntentResult.Started));
            }
            yield return Until(() => _damage.Count >= 4, 2f);
            Assert.That(_damage.Count, Is.EqualTo(4), "four shots");
        }

        [UnityTest]
        public IEnumerator TripleTap_TiedTrigger_EveryTapThatHits_Takes05SecondsOffScatterBlast()
        {
            int sb = Slot("Scatter Blast"), t = Slot("Triple Tap");
            Give("Triple Tap", "Tied Trigger");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            yield return UntilFree();
            float r0 = _player.SkillCooldownRemaining(sb), t0 = Time.time;
            Assert.That(_player.PressSkill(t, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 1, 2f);
            float r1 = _player.SkillCooldownRemaining(sb);
            Assert.That(r1, Is.EqualTo(r0 - (Time.time - t0) - 0.5f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ScatterBlast_Slug_ReachesATargetTenMetresAway()
        {
            int sb = Slot("Scatter Blast");
            Give("Scatter Blast", "Slug");
            Assert.That(_player.Skills[sb].Shape, Is.EqualTo(SkillShape.Line));
            _player.Teleport(_a.transform.position + Vector3.back * 8f);
            yield return null;
            Assume.That(_player.SettingsSkills[sb].Range, Is.LessThan(7.5f), "the cone would not reach it");
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage, Is.EqualTo(new[] { _player.Skills[sb].Damage }), "8 m away, hit");
        }

        [UnityTest]
        public IEnumerator ScatterBlast_PointBlank_Close_TwiceTheBonusPerMark()
        {
            int sb = Slot("Scatter Blast");
            Give("Scatter Blast", "Point Blank");
            SkillDefinition skill = _player.Skills[sb];
            _player.Teleport(_a.transform.position + Vector3.back * 2f);
            yield return null;
            _a.AddMarks(2, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage, Is.EqualTo(new[] { MarkRules.SpenderDamage(skill.Damage, 2, skill.MarkDamageBonus * 2f) }));
            Assert.That(_a.MarkCount, Is.Zero, "it took them");
        }

        [UnityTest]
        public IEnumerator ScatterBlast_PointBlank_Far_NoBonus_AndTheMarksStay()
        {
            int sb = Slot("Scatter Blast");
            Give("Scatter Blast", "Point Blank");
            SkillDefinition skill = _player.Skills[sb];
            _player.Teleport(_a.transform.position + Vector3.back * 4.5f);
            yield return null;
            _a.AddMarks(2, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage, Is.EqualTo(new[] { skill.Damage }), "its own damage");
            Assert.That(_a.MarkCount, Is.EqualTo(2), "the marks stay");
        }

        [UnityTest]
        public IEnumerator ScatterBlast_FreshShells_AKill_HalvesItsCooldown()
        {
            int sb = Slot("Scatter Blast");
            Give("Scatter Blast", "Fresh Shells");
            _a.SetMaxHealth(50);
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_a.IsAlive, 2f);
            Assert.That(_a.IsAlive, Is.False, "killed");
            float full = _player.CooldownOf(sb);
            Assert.That(_player.SkillCooldownRemaining(sb), Is.LessThanOrEqualTo(full * 0.5f + 0.01f).And.GreaterThan(full * 0.5f - 0.5f));
        }

        [UnityTest]
        public IEnumerator Backstep_ForwardStep_HopsTowardTheCursor()
        {
            int bs = Slot("Backstep");
            Give("Backstep", "Forward Step");
            _player.Teleport(OpenGround);
            yield return null;
            Vector3 from = _player.Position;
            Assert.That(_player.PressSkill(bs, from + Vector3.forward * 6f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            float moved = Vector3.Dot(_player.Position - from, Vector3.forward);
            Assert.That(moved, Is.EqualTo(-_player.SettingsSkills[bs].MoveDistance).Within(0.3f), "5 m toward the cursor");
        }

        [UnityTest]
        public IEnumerator Backstep_SecondStep_HopsAgainAtOnce()
        {
            int bs = Slot("Backstep");
            Give("Backstep", "Second Step");
            _player.Teleport(OpenGround);
            yield return null;
            Vector3 aim = _player.Position + Vector3.back * 3f; // the hop goes away from it: north, onto open ground
            Assert.That(_player.PressSkill(bs, aim), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(bs, aim), Is.EqualTo(IntentResult.Started), "the shot");
            yield return UntilFree();
            yield return Until(() => _player.RecastSlot < 0, 2f);
            Assert.That(_player.SkillChargesReady(bs), Is.EqualTo(1));
            Assert.That(_player.PressSkill(bs, _player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started), "a second hop");
        }

        // ---------------- One upgrade of each pool skill, and the rest, by their effect ----------------

        [UnityTest]
        public IEnumerator LongShot_SnapShot_FiresAtOnce_AsAPerfectRelease()
        {
            int ls = Slot("Long Shot");
            Give("Long Shot", "Snap Shot");
            _player.Teleport(NearA);
            yield return null;
            int perfect = _player.PerfectReleases;
            float pressed = Time.time;
            Assert.That(_player.PressSkill(ls, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(Time.time - pressed, Is.LessThan(_player.Skills[ls].Startup + 0.1f), "no charging");
            Assert.That(_damage, Is.EqualTo(new[] { PoolRules.PerfectDamage(_player.Skills[ls]) }));
            Assert.That(_player.PerfectReleases, Is.EqualTo(perfect + 1));
            Assert.That(_player.CooldownOf(ls), Is.EqualTo(_player.SettingsSkills[ls].Cooldown + 6f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator LongShot_Overcharge_APerfectReleaseDealsThreeTimes()
        {
            int ls = Slot("Long Shot");
            Give("Long Shot", "Overcharge");
            SkillDefinition skill = _player.Skills[ls];
            _player.Teleport(NearA);
            yield return null;
            int perfect = _player.PerfectReleases;
            Assert.That(_player.PressSkill(ls, _a.transform.position), Is.EqualTo(IntentResult.Started));
            float middle = 0.5f * (skill.PerfectFrom + skill.PerfectTo);
            yield return Until(() => _player.SkillChargedSeconds >= middle, 5f);
            _player.ReleaseSkill(ls);
            yield return Until(() => _damage.Count > 0, 3f);
            Assert.That(_player.PerfectReleases, Is.EqualTo(perfect + 1));
            Assert.That(_damage, Is.EqualTo(new[] { 3 * skill.Damage }));
            Assert.That(skill.PerfectTo - skill.PerfectFrom, Is.EqualTo(0.5f * (_player.SettingsSkills[ls].PerfectTo - _player.SettingsSkills[ls].PerfectFrom)).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator PowderKeg_TwinKegs_TwoLieAtOnce()
        {
            int keg = Slot("Powder Keg");
            Give("Powder Keg", "Twin Kegs");
            _player.Teleport(NearA);
            yield return null;
            int most = 0;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsLying > 0, 2f);
            yield return UntilFree();
            Assert.That(_player.PressSkill(keg, _a.transform.position + Vector3.left), Is.EqualTo(IntentResult.Started), "the second charge");
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until && most < 2)
            {
                most = Mathf.Max(most, _player.KegsLying);
                yield return null;
            }
            Assert.That(most, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FanFire_WalkingFire_SheMovesAtFullSpeedWhileSheHoldsIt()
        {
            int ff = Slot("Fan Fire");
            Give("Fan Fire", "Walking Fire");
            _player.Teleport(OpenGround);
            yield return null;
            _player.Steer(Vector3.right);
            yield return Until(() => _player.Speed > _player.Feel.RunSpeed * 0.9f, 2f);
            Assert.That(_player.PressSkill(ff, _player.Position + Vector3.forward * 5f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsInLightSkill, Is.True);
            yield return Until(() => _player.SkillPhase == SkillPhase.Channel, 2f);
            Vector3 from = _player.Position;
            float t0 = Time.time;
            yield return For(0.4f);
            float moved = Vector3.Dot(_player.Position - from, Vector3.right);
            float elapsed = Time.time - t0;
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(ff), "still holding it");
            Assert.That(_player.IsSkillSliding, Is.False);
            Assert.That(moved, Is.GreaterThan(_player.Feel.RunSpeed * elapsed * 0.8f), "not slowed");
            _player.ReleaseSkill(ff);
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator HeavyVolley_QuickCast_ACastOf06Seconds_20PercentLess()
        {
            int hv = Slot("Heavy Volley");
            Give("Heavy Volley", "Quick Cast");
            _player.Teleport(NearA);
            yield return null;
            float pressed = Time.time;
            Assert.That(_player.PressSkill(hv, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 3f);
            Assert.That(Time.time - pressed, Is.EqualTo(0.6f).Within(0.05f));
            Assert.That(_damage, Is.EqualTo(new[] { Mathf.RoundToInt(_player.SettingsSkills[hv].Damage * 0.8f) }));
        }

        [UnityTest]
        public IEnumerator TagRound_SpreadingTag_TheTagJumpsToTheNearestWhenATaggedTargetDies()
        {
            int tag = Slot("Tag Round"), quick = Slot("Quick Shot");
            Give("Tag Round", "Spreading Tag");
            Damageable d = GameObject.Find("Dummy_D").GetComponent<Damageable>();
            Damageable e = GameObject.Find("Dummy_E").GetComponent<Damageable>();
            Damageable f = GameObject.Find("Dummy_F").GetComponent<Damageable>();
            d.SetMaxHealth(60);
            _player.Teleport(d.transform.position + Vector3.back * 5f);
            yield return null;
            Assert.That(_player.PressSkill(tag, d.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => d.IsTagged, 2f);
            Assert.That(d.IsTagged, Is.True);
            Assert.That(e.IsTagged || f.IsTagged, Is.False);
            yield return UntilFree();
            Assert.That(_player.PressSkill(quick, d.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !d.IsAlive, 2f);
            Assert.That(d.IsAlive, Is.False, "the tagged one died");
            Assert.That(_player.TagsSpread, Is.EqualTo(1));
            Assert.That(e.IsTagged || f.IsTagged, Is.True, "the tag jumped to the nearest");
            Damageable got = e.IsTagged ? e : f;
            Assert.That(got.TagRemaining, Is.GreaterThan(_player.Skills[tag].TagSeconds - 0.5f), "renewed");
        }

        [UnityTest]
        public IEnumerator BreakerSlug_FollowThrough_ADazeHalvesItsCooldown()
        {
            int slug = Slot("Breaker Slug");
            Give("Breaker Slug", "Follow Through");
            _levels = GeneratedLevels.Ensure();
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            if (practice != null) practice.enabled = false;
            _player.Teleport(new Vector3(8f, 0f, -6f));
            yield return null;
            var guard = (EnemyGuard)_levels.SpawnPractice(EnemyKind.Guard).Single();
            guard.Tuning.Enemy.MoveSpeed = 0f;
            guard.Tuning.Enemy.AggroRange = 0f;
            _player.Teleport(guard.transform.position + Vector3.back * 5f);
            yield return null;
            guard.Body.AddMarks(2, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(slug, guard.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => guard.IsDazed, 3f);
            Assert.That(guard.IsDazed, Is.True, "dazed by the slug");
            float full = _player.CooldownOf(slug);
            Assert.That(_player.SkillCooldownRemaining(slug), Is.LessThanOrEqualTo(full * 0.5f + 0.01f).And.GreaterThan(full * 0.5f - 0.6f));
        }

        [UnityTest]
        public IEnumerator Slide_SlideShot_ABasicShotThatHitsDuringTheSlide_AddsAMark()
        {
            int slide = Slot("Slide");
            Give("Slide", "Slide Shot");
            Assume.That(_player.Kit.BasicShotsPerMark, Is.GreaterThan(1), "the first basic hit marks nothing by itself");
            _player.Teleport(NearA);
            yield return null;
            _player.Steer(Vector3.left);
            Assert.That(_player.PressSkill(slide, _a.transform.position), Is.EqualTo(IntentResult.Started));
            _player.Stop();
            int hits = _player.BasicShotHits;
            _player.Shoot(_a.transform.position);
            bool duringSlide = false;
            float until = Time.realtimeSinceStartup + 2f;
            while (_player.BasicShotHits == hits && Time.realtimeSinceStartup < until)
            {
                duringSlide = _player.IsKeyMoving;
                yield return null;
            }
            Assert.That(_player.BasicShotHits, Is.EqualTo(hits + 1));
            Assume.That(duringSlide, Is.True, "the shot went off during the slide");
            Assert.That(_a.MarkCount, Is.EqualTo(1), "a mark from the slide");
        }

        [UnityTest]
        public IEnumerator StoppingShot_Riposte_ACounterMakesScatterBlastReady()
        {
            int stop = Slot("Stopping Shot"), sb = Slot("Scatter Blast");
            Give("Stopping Shot", "Riposte");
            // A long cooldown for Scatter Blast for this test, so it cannot come back by itself while the brute winds up.
            Give(new SkillUpgradeDefinition("Scatter Blast", "Test: slow", SkillUpgradeKind.Numbers, "for the test", null,
                                            SkillPatch.Set(nameof(SkillDefinition.Cooldown), 60f)));
            _player.Teleport(OpenGround);
            yield return null;
            Assert.That(_player.PressSkill(sb, _player.Position + Vector3.left * 3f), Is.EqualTo(IntentResult.Started));
            yield return UntilFree();
            Assert.That(_player.SkillCooldownRemaining(sb), Is.GreaterThan(30f));

            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            Vector3 home = brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up).normalized;
            _player.Teleport(home + away * 7f);
            yield return Until(() => brute.State == EnemyState.Windup && brute.Attack != null && brute.Attack.Name == "Shockwave", 8f);
            Assume.That(brute.Attack?.Name, Is.EqualTo("Shockwave"), "the brute winds up its counterable attack");
            Assert.That(_player.PressSkill(stop, brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Countered > 0, 3f);
            Assert.That(brute.Countered, Is.EqualTo(1));
            Assert.That(_player.SkillCooldownRemaining(sb), Is.EqualTo(0f), "Scatter Blast is ready");
        }

        // ---------------- Three of the kind "numbers" ----------------

        [UnityTest]
        public IEnumerator QuickShot_HeavyRound_60PercentMoreDamage()
        {
            int q = Slot("Quick Shot");
            Give("Quick Shot", "Heavy Round");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(q, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage, Is.EqualTo(new[] { Mathf.RoundToInt(_player.SettingsSkills[q].Damage * 1.6f) }));
        }

        [UnityTest]
        public IEnumerator TripleTap_SharpTaps_EachTap40PercentMore()
        {
            int t = Slot("Triple Tap");
            Give("Triple Tap", "Sharp Taps");
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(t, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage[0], Is.EqualTo(Mathf.RoundToInt(_player.SettingsSkills[t].Damage * 1.4f)));
        }

        [UnityTest]
        public IEnumerator PowderKeg_BigKeg_30PercentMore_AndAThirdWider()
        {
            int keg = Slot("Powder Keg");
            Give("Powder Keg", "Big Keg");
            Assert.That(_player.Skills[keg].Radius, Is.EqualTo(_player.SettingsSkills[keg].Radius * 4f / 3f).Within(1e-4f));
            _player.Teleport(NearA);
            yield return null;
            // Landed a little over the old radius from the dummy's edge: only the wider keg reaches it.
            Vector3 toA = (_a.transform.position - _player.Position);
            toA.y = 0f;
            Vector3 point = _a.transform.position - toA.normalized * (_player.SettingsSkills[keg].Radius + _a.Radius + 0.3f);
            Assert.That(_player.PressSkill(keg, point), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 4f);
            Assert.That(_damage, Is.EqualTo(new[] { Mathf.RoundToInt(_player.SettingsSkills[keg].Damage * 1.3f) }));
        }
    }
}
