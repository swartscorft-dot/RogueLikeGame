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
    /// Decision 0005 (2026-11-19), the core of the new kit, in the built ControlRoom scene through the intent API: marks,
    /// the four start skills, light skills, Triple Tap's three presses, and focus that cuts cooldowns. Values come from
    /// the settings assets in the scene. Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3).
    /// The test set-up equips all eight skills (PlayModeTestSetUp); a test about the start four puts them back.
    /// </summary>
    public class KitCoreTests
    {
        const int Quick = 0, Keg = 1, Scatter = 4, Volley = 5, Backstep = 6, Triple = 7;

        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here
        static readonly Vector3 RunFrom = new Vector3(-8f, 0f, -8f); // open ground to run right along

        PlayerCharacter _player;
        FeelValues _feel;
        Damageable _a;
        readonly List<Object> _copies = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Assert.That(_player.SkillSet, Is.Not.Null, "the player uses the skill settings asset");
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
            foreach (Object copy in _copies) if (copy != null) Object.Destroy(copy);
            _copies.Clear();
        }

        // ---------------- The settings asset ----------------

        /// <summary>The tuned copy in Skills.asset was set by hand for this order: it must match the code's start values.</summary>
        [UnityTest]
        public IEnumerator TheSettingsAsset_MatchesTheStartValues_InEveryFieldThisKitSets()
        {
            yield return null;
            SkillSettings asset = _player.SkillSet;
            SkillDefinition[] start = SkillDefaults.Create();
            Assert.That(asset.Skills.Select(s => s.Name), Is.EqualTo(start.Select(s => s.Name)), "the same skills in the same order");
            for (int i = 0; i < start.Length; i++)
            {
                SkillDefinition have = asset.Skills[i], want = start[i];
                string n = want.Name + ": ";
                Assert.That(have.Cooldown, Is.EqualTo(want.Cooldown), n + "Cooldown");
                Assert.That(have.Damage, Is.EqualTo(want.Damage), n + "Damage");
                Assert.That(have.Startup, Is.EqualTo(want.Startup), n + "Startup");
                Assert.That(have.Recovery, Is.EqualTo(want.Recovery), n + "Recovery");
                Assert.That(have.HitCount, Is.EqualTo(want.HitCount), n + "HitCount");
                Assert.That(have.StartEquipped, Is.EqualTo(want.StartEquipped), n + "StartEquipped");
                Assert.That(have.Lock, Is.EqualTo(want.Lock), n + "Lock");
                Assert.That(have.MarksAdded, Is.EqualTo(want.MarksAdded), n + "MarksAdded");
                Assert.That(have.MarkInterval, Is.EqualTo(want.MarkInterval), n + "MarkInterval");
                Assert.That(have.MarkDamageBonus, Is.EqualTo(want.MarkDamageBonus), n + "MarkDamageBonus");
                Assert.That(have.RecastCount, Is.EqualTo(want.RecastCount), n + "RecastCount");
                Assert.That(have.RecastWindow, Is.EqualTo(want.RecastWindow), n + "RecastWindow");
                Assert.That(have.CleanTapMarks, Is.EqualTo(want.CleanTapMarks), n + "CleanTapMarks");
                // Amended 2026-11-19: Backstep's two presses and its clean spend.
                Assert.That(have.MoveDistance, Is.EqualTo(want.MoveDistance), n + "MoveDistance");
                Assert.That(have.OpeningMove, Is.EqualTo(want.OpeningMove), n + "OpeningMove");
                Assert.That(have.CleanMarkDamageBonus, Is.EqualTo(want.CleanMarkDamageBonus), n + "CleanMarkDamageBonus");
                // Work order 008: what it takes off a stagger bar.
                Assert.That(have.Stagger, Is.EqualTo(want.Stagger), n + "Stagger");
                // Work order 015: the pool's fields.
                Assert.That(have.Input, Is.EqualTo(want.Input), n + "Input");
                Assert.That(have.Charges, Is.EqualTo(want.Charges), n + "Charges");
                Assert.That(have.PerfectFrom, Is.EqualTo(want.PerfectFrom), n + "PerfectFrom");
                Assert.That(have.PerfectTo, Is.EqualTo(want.PerfectTo), n + "PerfectTo");
                Assert.That(have.PerfectScale, Is.EqualTo(want.PerfectScale), n + "PerfectScale");
                Assert.That(have.MarkStaggerBonus, Is.EqualTo(want.MarkStaggerBonus), n + "MarkStaggerBonus");
                Assert.That(have.TagSeconds, Is.EqualTo(want.TagSeconds), n + "TagSeconds");
                Assert.That(have.TagDamageShare, Is.EqualTo(want.TagDamageShare), n + "TagDamageShare");
                Assert.That(have.CounterDaze, Is.EqualTo(want.CounterDaze), n + "CounterDaze");
                Assert.That(have.CounterHalfAngle, Is.EqualTo(want.CounterHalfAngle), n + "CounterHalfAngle");
                Assert.That(have.CounterFocus, Is.EqualTo(want.CounterFocus), n + "CounterFocus");
                Assert.That(have.Fuse, Is.EqualTo(want.Fuse), n + "Fuse");
                Assert.That(have.ShotBurstScale, Is.EqualTo(want.ShotBurstScale), n + "ShotBurstScale");
                Assert.That(have.Range, Is.EqualTo(want.Range), n + "Range");
                Assert.That(have.HalfWidth, Is.EqualTo(want.HalfWidth), n + "HalfWidth");
            }
            IdentityValues id = asset.Identity, idStart = new IdentityValues();
            Assert.That(id.MaxFocus, Is.EqualTo(idStart.MaxFocus), "MaxFocus");
            Assert.That(id.FocusPerDamage, Is.EqualTo(idStart.FocusPerDamage), "FocusPerDamage");
            Assert.That(id.EmpoweredDuration, Is.EqualTo(idStart.EmpoweredDuration), "EmpoweredDuration");
            Assert.That(id.EmpoweredDamage, Is.EqualTo(idStart.EmpoweredDamage), "EmpoweredDamage");
            Assert.That(id.FocusPerMarkSpent, Is.EqualTo(idStart.FocusPerMarkSpent), "FocusPerMarkSpent");
            Assert.That(id.FocusPerCleanTap, Is.EqualTo(idStart.FocusPerCleanTap), "FocusPerCleanTap");
            Assert.That(id.CooldownCut, Is.EqualTo(idStart.CooldownCut), "CooldownCut");
            KitValues kit = asset.Kit, kitStart = new KitValues();
            Assert.That(kit, Is.Not.Null, "the kit block");
            Assert.That(kit.MaxMarks, Is.EqualTo(kitStart.MaxMarks), "MaxMarks");
            Assert.That(kit.MarkLifetime, Is.EqualTo(kitStart.MarkLifetime), "MarkLifetime");
            Assert.That(kit.BasicShotsPerMark, Is.EqualTo(kitStart.BasicShotsPerMark), "BasicShotsPerMark");
            Assert.That(kit.StartWithAllSkills, Is.EqualTo(kitStart.StartWithAllSkills), "StartWithAllSkills");
            Assert.That(kit.CleanShare, Is.EqualTo(kitStart.CleanShare), "CleanShare");
            Assert.That(kit.CleanDamage, Is.EqualTo(kitStart.CleanDamage), "CleanDamage");
        }

        // ---------------- Equipped slots ----------------

        [UnityTest]
        public IEnumerator ARun_StartsWithFourSkills_TheOthersAreEmptyUntilEquipped()
        {
            _player.EquipStartSkills();
            yield return null;
            var equipped = Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).ToArray();
            Assert.That(equipped, Is.EqualTo(new[] { Quick, Scatter, Backstep, Triple }), "Quick Shot, Scatter Blast, Backstep, Triple Tap");

            Assert.That(_player.PressSkill(Keg, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "an empty slot does nothing");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
            Assert.That(_player.SkillCooldownRemaining(Keg), Is.EqualTo(0f));

            _player.EquipSkill(Keg);
            Assert.That(_player.IsEquipped(Keg), Is.True);
            Assert.That(_player.PressSkill(Keg, _a.transform.position), Is.EqualTo(IntentResult.Started), "equipped, it works");
        }

        [UnityTest]
        public IEnumerator StartWithAllSkills_EquipsTheOthers()
        {
            _player.EquipStartSkills();
            OwnSkills(s => s.Kit.StartWithAllSkills = true);
            yield return null;
            for (int slot = 0; slot < _player.Skills.Count; slot++) Assert.That(_player.IsEquipped(slot), Is.True, "slot " + slot);
        }

        // ---------------- Marks ----------------

        /// <summary>
        /// Changed deliberately 2026-11-19 after Kristian played: the basic shot marks every fifth hit (it was every
        /// fourth, and the test was named for it and assumed 4). The steps are the same, read from the settings.
        /// </summary>
        [UnityTest]
        public IEnumerator Builders_MarkTheTarget_AndTheBasicShotEveryFifthHit_AndThePipsShowThem()
        {
            KitValues kit = _player.Kit;
            Assume.That(kit.BasicShotsPerMark, Is.EqualTo(5));
            _player.Teleport(NearA);
            yield return null;

            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(_a.MarkCount, Is.EqualTo(_player.Skills[Quick].MarksAdded), "Quick Shot marks what it hits");
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 2f);

            for (int hit = 1; hit <= kit.BasicShotsPerMark; hit++)
            {
                int before = _a.HitsTaken;
                _player.Shoot(_a.transform.position);
                yield return Until(() => _a.HitsTaken > before, 2f);
                int want = 1 + (hit == kit.BasicShotsPerMark ? 1 : 0);
                Assert.That(_a.MarkCount, Is.EqualTo(want), "after basic shot hit " + hit);
            }
            yield return null;
            MarkPips pips = _a.GetComponentInChildren<MarkPips>();
            Assert.That(pips, Is.Not.Null, "the pips were made at the first mark");
            Assert.That(pips.Visible, Is.True);
            Assert.That(pips.Shown, Is.EqualTo(2), "two marks shown");
        }

        [UnityTest]
        public IEnumerator Marks_FallOffTogether_ALifetimeAfterTheLast_AndThePipsGo()
        {
            OwnSkills(s => s.Kit.MarkLifetime = 0.5f);
            _player.Teleport(NearA);
            yield return null;
            _player.PressSkill(Quick, _a.transform.position);
            yield return Until(() => _a.MarkCount >= 1, 2f);
            float markedAt = Time.time;
            Assert.That(_a.MarkCount, Is.EqualTo(1));
            yield return Until(() => _a.MarkCount == 0, 2f);
            Assert.That(Time.time - markedAt, Is.EqualTo(0.5f).Within(0.1f), "they fall off after the lifetime");
            yield return null;
            Assert.That(_a.GetComponentInChildren<MarkPips>().Visible, Is.False, "and nothing shows");
        }

        [UnityTest]
        public IEnumerator ScatterBlast_TakesEveryMark_ForItsBonus_AndFillsFocus()
        {
            SkillDefinition blast = _player.Skills[Scatter];
            IdentityValues id = _player.Identity;
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(5, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(5));

            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            int want = MarkRules.SpenderDamage(blast.Damage, 5, blast.MarkDamageBonus);
            Debug.Log($"[0005] Scatter Blast: {blast.Damage} with no marks, {want} with five");
            Assert.That(Lost(_a), Is.EqualTo(want), "+25 % per mark: +125 % at five");
            Assert.That(_a.MarkCount, Is.EqualTo(0), "it took them all");
            Assert.That(_player.Focus, Is.EqualTo(5 * id.FocusPerMarkSpent + want * id.FocusPerDamage).Within(1e-3f),
                        "focus for each mark taken, and the trickle from the damage");
        }

        // ---------------- Light skills ----------------

        [UnityTest]
        public IEnumerator QuickShot_IsLight_SheRunsThroughIt_AndNothingElseStartsMeanwhile()
        {
            Assume.That(_player.Skills[Quick].Lock, Is.EqualTo(SkillLock.Light));
            yield return RunRight();
            float minSpeed = float.PositiveInfinity;
            Vector3 from = _player.Position;
            Assert.That(_player.PressSkill(Quick, _player.Position + Vector3.right * 8f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsMoving, Is.True, "the move keys work in it");
            _player.Shoot(_player.Position + Vector3.right * 8f);
            Assert.That(_player.HasPendingShot, Is.True, "a click waits");
            Assert.That(_player.PressSkill(Scatter, _player.Position + Vector3.right * 3f), Is.EqualTo(IntentResult.Buffered), "another skill waits");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_player.ActiveSkillSlot == Quick && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_player.ActiveSkillSlot == Quick) minSpeed = Mathf.Min(minSpeed, _player.Speed);
                Assert.That(_player.IsSkillSliding, Is.False, "no slide");
            }
            float moved = Vector3.Dot(Flat(_player.Position - from), Vector3.right);
            Debug.Log($"[0005] Quick Shot on the run: slowest {minSpeed:0.00} m/s of {_feel.RunSpeed}, moved {moved:0.00} m through it");
            Assert.That(minSpeed, Is.GreaterThan(_feel.RunSpeed * 0.95f), "not stopped, slowed or slid");
            Assert.That(_player.SkillShotsFired, Is.GreaterThanOrEqualTo(1), "and it fired");
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator ALightSkill_IsCancelledByADash_BeforeItFires()
        {
            _player.Teleport(NearA);
            yield return null;
            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 5f), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(_player.Skills[Quick].Startup + 0.2f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "it never fired");
        }

        // ---------------- Triple Tap ----------------

        [UnityTest] public IEnumerator TripleTap_AThirdPressInTheCleanPart_AddsTwoMarks_AndHitsHarder() => TripleChain(clean: true);
        [UnityTest] public IEnumerator TripleTap_AThirdPressEarlyInItsWindow_IsAnOrdinaryShot() => TripleChain(clean: false);

        IEnumerator TripleChain(bool clean)
        {
            SkillDefinition tap = _player.Skills[Triple];
            KitValues kit = _player.Kit;
            Assume.That(tap.RecastCount, Is.EqualTo(3));
            _player.Teleport(NearA);
            yield return null;
            float focusBefore = _player.Focus;

            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            // Work order 069: the second press has its own clean part, 0.12 to 0.30 s of its 0.45 s window (until then none).
            Assert.That(_player.RecastCleanShare, Is.EqualTo((kit.QuickPressCleanTo - kit.QuickPressCleanFrom) / kit.QuickPressWindow).Within(1e-5f), "the second press's own clean part");
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastCleanShare, Is.EqualTo((kit.LastPressCleanTo - kit.LastPressCleanFrom) / kit.LastPressWindow).Within(1e-5f), "the third's: 0.42 to 0.66 s of 0.80 s (work order 069; until then a third of 0.6 s)");
            if (clean) yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assume.That(_player.IsRecastWindowOpen, Is.True);
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the third press");
            yield return Until(() => _a.HitsTaken >= 3, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);

            int third = clean ? Mathf.RoundToInt(tap.Damage * kit.CleanDamage) : tap.Damage;
            Assert.That(_a.HitsTaken, Is.EqualTo(3), "three shots");
            Assert.That(Lost(_a), Is.EqualTo(2 * tap.Damage + third));
            Assert.That(_a.MarkCount, Is.EqualTo(clean ? 2 * tap.MarksAdded + tap.CleanTapMarks : 3 * tap.MarksAdded),
                        clean ? "four marks' worth" : "three marks");
            float focusFromDamage = Lost(_a) * _player.Identity.FocusPerDamage;
            Assert.That(_player.Focus - focusBefore, Is.EqualTo(focusFromDamage + (clean ? _player.Identity.FocusPerCleanTap : 0f)).Within(1e-3f));
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the chain is over");
        }

        [UnityTest]
        public IEnumerator TripleTap_APressAfterTheWindowRanOut_GivesNothing()
        {
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSlot == -1, 2f);
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the window ran out and the chain is over");
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "a late press: the cooldown runs");
            yield return new WaitForSeconds(0.4f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "no second shot");
        }

        [UnityTest]
        public IEnumerator TripleTap_TheCooldownRunsFromTheFirstPress_AndSheWalksThroughAllOfIt()
        {
            SkillDefinition tap = _player.Skills[Triple];
            yield return RunRight();
            float firstPress = Time.time, minSpeed = float.PositiveInfinity;
            Vector3 aim() => _player.Position + Vector3.right * 6f;
            Assert.That(_player.PressSkill(Triple, aim()), Is.EqualTo(IntentResult.Started));
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_player.RecastSlot == Triple && Time.realtimeSinceStartup < deadline)
            {
                if (_player.IsRecastWindowOpen) Assert.That(_player.PressSkill(Triple, aim()), Is.EqualTo(IntentResult.Started));
                yield return null;
                minSpeed = Mathf.Min(minSpeed, _player.Speed);
                Assert.That(_player.IsMoving, Is.True, "the move keys work all through it");
            }
            Assert.That(_player.SkillShotsFired, Is.GreaterThanOrEqualTo(2));
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            Debug.Log($"[0005] Triple Tap on the run: slowest {minSpeed:0.00} m/s of {_feel.RunSpeed}, chain over {Time.time - firstPress:0.00} s after the first press");
            Assert.That(minSpeed, Is.GreaterThan(_feel.RunSpeed * 0.95f), "she ran through all of it");
            Assert.That(_player.SkillCooldownRemaining(Triple), Is.EqualTo(tap.Cooldown - (Time.time - firstPress)).Within(0.02f),
                        "the cooldown counts from the first press");
            _player.Stop();
        }

        // ---------------- Backstep: two presses, the second a clean spend (amended 2026-11-19 after Kristian played) ----------------

        [UnityTest]
        public IEnumerator Backstep_OnePress_SheHops_NoShotIsFired_AndTheCooldownRunsFromThatPress()
        {
            SkillDefinition step = _player.Skills[Backstep];
            Assume.That(step.RecastCount, Is.EqualTo(2));
            Assume.That(step.OpeningMove, Is.True);
            Assume.That(step.MoveDistance, Is.LessThan(-1f), "it moves backward");
            _player.Teleport(NearA);
            yield return null;
            Vector3 from = _player.Position;
            Vector3 toA = Flat(_a.transform.position - from);
            int fired = _player.SkillShotsFired;
            float pressedAt = Time.time;

            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.RecastSlot, Is.EqualTo(Backstep), "a chain of two");
            Assert.That(_player.IsRecastWindowOpen, Is.False, "no window while she hops");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            float openedAt = Time.time;
            Vector3 moved = Flat(_player.Position - from);
            Debug.Log($"[0005] Backstep's hop: {moved.magnitude:0.00} m, window open {openedAt - pressedAt:0.000} s after the press");
            Assert.That(_player.IsRecastWindowOpen, Is.True, "the window opens when the hop has ended");
            Assert.That(openedAt - pressedAt, Is.EqualTo(step.Startup).Within(0.05f), "at the end of the hop");
            Assert.That(moved.magnitude, Is.EqualTo(-step.MoveDistance).Within(0.3f), "the hop as before: its move distance");
            Assert.That(Vector3.Dot(moved.normalized, toA.normalized), Is.LessThan(-0.98f), "straight away from the aim");
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "the hop fires nothing");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));
            Assert.That(_player.RecastCleanShare, Is.EqualTo(_player.Kit.CleanShare).Within(1e-5f), "the window has its clean part");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None), "she is free in the window");
            Assert.That(_player.IsLocked, Is.False);
            Vector3 hopEnd = _player.Position;
            _player.Steer(Vector3.right);
            yield return null;
            Assert.That(_player.IsMoving, Is.True, "and the move keys work at once");
            yield return new WaitForSeconds(0.1f);
            Assert.That(Vector3.Dot(Flat(_player.Position - hopEnd), Vector3.right), Is.GreaterThan(0.1f), "she walks in the window");
            _player.Stop();

            yield return Until(() => _player.RecastSlot == -1, 2f);
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "no second press: the skill is over");
            Assert.That(Time.time - openedAt, Is.EqualTo(step.RecastWindow).Within(0.05f), "when the window ran out");
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "no shot, ever");
            Assert.That(Lost(_a), Is.EqualTo(0));
            Assert.That(_player.SkillCooldownRemaining(Backstep), Is.EqualTo(step.Cooldown - (Time.time - pressedAt)).Within(0.02f),
                        "the cooldown counts from the first press");
        }

        [UnityTest]
        public IEnumerator Backstep_ASecondPressEarlyInTheWindow_IsAnOrdinaryShot_ThatAddsOneMark_AndTakesNone()
        {
            SkillDefinition step = _player.Skills[Backstep];
            KitValues kit = _player.Kit;
            IdentityValues id = _player.Identity;
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(2, kit.MaxMarks, kit.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(2));
            float focusBefore = _player.Focus;

            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assume.That(_player.RecastSinceOpened, Is.LessThan(_player.RecastCleanFrom), "early: not in the clean part"); // where it lies since work order 040
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);

            Assert.That(_a.HitsTaken, Is.EqualTo(1), "one shot");
            Assert.That(Lost(_a), Is.EqualTo(step.Damage), "the ordinary damage");
            Assert.That(_a.MarkCount, Is.EqualTo(2 + step.MarksAdded), "one mark added, none taken");
            Assert.That(_player.Focus - focusBefore, Is.EqualTo(step.Damage * id.FocusPerDamage).Within(1e-3f), "only the trickle from the damage");
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the chain is over");
        }

        [UnityTest]
        public IEnumerator Backstep_ASecondPressInTheCleanPart_TakesFiveMarks_ForTheCleanBonus_AndFillsFocus()
        {
            SkillDefinition step = _player.Skills[Backstep];
            KitValues kit = _player.Kit;
            IdentityValues id = _player.Identity;
            Assume.That(step.CleanMarkDamageBonus, Is.GreaterThan(0f));
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(5, kit.MaxMarks, kit.MarkLifetime);
            Assume.That(_a.MarkCount, Is.EqualTo(5));
            float focusBefore = _player.Focus;

            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f, 2f);
            Assume.That(_player.IsRecastWindowOpen, Is.True, "still in the window");
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started), "the clean press");
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            yield return Until(() => _player.Action == ActionKind.None, 2f);

            int want = Mathf.RoundToInt(step.Damage * (1f + step.CleanMarkDamageBonus * 5));
            Debug.Log($"[0005] Backstep's shot: {step.Damage} ordinary, {want} clean at five marks");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "one shot");
            Assert.That(Lost(_a), Is.EqualTo(want), "the base damage times (1 + bonus times 5)");
            Assert.That(_a.MarkCount, Is.EqualTo(0), "the marks are gone");
            Assert.That(_player.Focus - focusBefore,
                        Is.EqualTo(5 * id.FocusPerMarkSpent + id.FocusPerCleanTap + want * id.FocusPerDamage).Within(1e-3f),
                        "focus for five marks spent, the clean press and the trickle from the damage");
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the chain is over");
        }

        [UnityTest]
        public IEnumerator Backstep_ASecondPressAfterTheWindow_GivesNothing()
        {
            _player.Teleport(NearA);
            yield return null;
            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            yield return Until(() => _player.RecastSlot == -1, 2f);
            Assert.That(_player.RecastSlot, Is.EqualTo(-1), "the window ran out and the chain is over");
            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "a late press: the cooldown runs");
            yield return new WaitForSeconds(0.4f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "no shot");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
        }

        // ---------------- Focus ----------------

        [UnityTest]
        public IEnumerator SpendingFocus_CutsEveryRunningCooldown_AndEveryBasicShotHitMarks()
        {
            IdentityValues id = _player.Identity;
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 2f);
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            float scatterLeft = _player.SkillCooldownRemaining(Scatter), dashLeft = _player.DashCooldownRemaining;
            Assume.That(scatterLeft, Is.GreaterThan(1f));
            Assume.That(dashLeft, Is.GreaterThan(0.05f));

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.SkillCooldownRemaining(Scatter), Is.EqualTo(scatterLeft * (1f - id.CooldownCut)).Within(1e-3f), "Scatter Blast's cooldown is cut");
            Assert.That(_player.DashCooldownRemaining, Is.EqualTo(dashLeft * (1f - id.CooldownCut)).Within(1e-3f), "and the dash's");
            Assert.That(_player.SkillCooldownRemaining(Keg), Is.EqualTo(0f), "a ready skill stays ready");

            int marks = _a.MarkCount;
            for (int hit = 1; hit <= 2; hit++)
            {
                int before = _a.HitsTaken;
                _player.Shoot(_a.transform.position);
                yield return Until(() => _a.HitsTaken > before, 2f);
                Assert.That(_a.MarkCount, Is.EqualTo(marks + hit), "every basic shot hit marks while it runs");
            }
        }

        // ---------------- The numbers Kristian will meet ----------------

        /// <summary>
        /// The start four used on cooldown, at Dummy_A from up close, with a basic shot whenever nothing else may start,
        /// for three Scatter Blasts after the first. Logs the marks on the target when Scatter Blast comes off cooldown
        /// and at each blast, what the blast dealt against what it deals with none, how long after each blast the target
        /// is back at five marks, each Backstep shot, and when the focus gauge was first full. Backstep hops her 5 m
        /// away, out of Scatter Blast's reach; she shoots from there and is put back where she stood after the chain.
        /// The dummy is given a health it cannot lose in the time, so it never dies and drops its marks. Focus is never
        /// spent: the gauge fills once.
        /// Changed deliberately 2026-11-19 after Kristian played: Backstep is two presses, the second early in its window
        /// here (the ordinary shot that adds a mark, as its one press did before), and the rotation reads the new values
        /// (five basic shot hits a mark, Scatter Blast 6 s). The second test is the same with every second press of
        /// Backstep clean: the hard way, which spends the marks.
        /// </summary>
        [UnityTest]
        public IEnumerator Measure_TheStartFourOnCooldown_MarksAtEachBlast_AndTheTimeToFillFocus() => MeasureStartFour(cleanBackstep: false);

        [UnityTest]
        public IEnumerator Measure_TheStartFourOnCooldown_WithEveryBackstepClean() => MeasureStartFour(cleanBackstep: true);

        IEnumerator MeasureStartFour(bool cleanBackstep)
        {
            _player.EquipStartSkills();
            _a.SetMaxHealth(1000000);
            _player.Teleport(NearA);
            yield return null;
            SkillDefinition blast = _player.Skills[Scatter], step = _player.Skills[Backstep];
            KitValues kit = _player.Kit;
            var marksAtBlast = new List<int>();
            var marksWhenReady = new List<int>();  // when Scatter Blast came off cooldown
            var dealtAtBlast = new List<int>();
            var backAtFive = new List<string>();   // after each blast: how long until the target carried the cap again
            var steps = new List<string>();        // each Backstep shot: the marks before it and what it dealt
            var stepSpends = new List<KeyValuePair<int, int>>();
            float start = Time.time, fullAt = float.NaN, lastBlastAt = float.NaN;
            bool stepping = false, blastWasReady = true;
            float deadline = Time.realtimeSinceStartup + 90f;
            while ((marksAtBlast.Count < 4 || (float.IsNaN(fullAt) && Time.time - start < 40f)) && Time.realtimeSinceStartup < deadline)
            {
                Vector3 at = _a.transform.position;
                if (float.IsNaN(fullAt) && _player.IsFocusFull) fullAt = Time.time - start;
                bool blastReady = _player.SkillCooldownRemaining(Scatter) <= 0f;
                if (blastReady && !blastWasReady) marksWhenReady.Add(_a.MarkCount);
                blastWasReady = blastReady;
                if (!float.IsNaN(lastBlastAt) && _a.MarkCount >= kit.MaxMarks)
                {
                    backAtFive.Add($"{Time.time - lastBlastAt:0.0} s");
                    lastBlastAt = float.NaN;
                }
                // Not Action None: a click always waits, so a basic shot is nearly always going.
                if (stepping && _player.RecastSlot != Backstep && _player.Action != ActionKind.Skill && !_player.IsDashing)
                {
                    _player.Teleport(NearA);
                    stepping = false;
                }
                bool free = !_player.IsLocked && _player.SkillGateRemaining <= 0f;
                bool inCleanPart = _player.RecastSinceOpened >= (_player.RecastCleanFrom + _player.RecastCleanTo) * 0.5f;
                if (_player.RecastSlot == Triple && _player.IsRecastWindowOpen && (_player.RecastCleanShare <= 0f || inCleanPart))
                    _player.PressSkill(Triple, at);
                else if (_player.RecastSlot == Backstep && _player.IsRecastWindowOpen && (!cleanBackstep || inCleanPart))
                {
                    int marksBefore = _a.MarkCount, lostBefore = Lost(_a), firedBefore = _player.SkillShotsFired;
                    _player.PressSkill(Backstep, at);
                    yield return Until(() => _player.SkillShotsFired > firedBefore, 2f);
                    int dealt = Lost(_a) - lostBefore;
                    steps.Add($"{marksBefore} marks: {dealt}");
                    if (cleanBackstep) stepSpends.Add(new KeyValuePair<int, int>(marksBefore, dealt));
                    continue;
                }
                else if (free && _player.RecastSlot < 0)
                {
                    if (blastReady)
                    {
                        if (!float.IsNaN(lastBlastAt)) backAtFive.Add("not before the next blast");
                        marksAtBlast.Add(_a.MarkCount);
                        int before = Lost(_a), firedBefore = _player.SkillShotsFired;
                        float distance = Flat(at - _player.Position).magnitude;
                        IntentResult pressed = _player.PressSkill(Scatter, at);
                        yield return Until(() => _player.SkillShotsFired > firedBefore, 2f);
                        int dealt = Lost(_a) - before;
                        dealtAtBlast.Add(dealt);
                        lastBlastAt = Time.time;
                        blastWasReady = false;
                        if (dealt <= 0)
                            Debug.Log($"[0005] a blast dealt nothing: {pressed}, {distance:0.00} m from A, A alive {_a.IsAlive}, health {_a.CurrentHealth}/{_a.MaxHealth}, "
                                    + $"fired {_player.SkillShotsFired - firedBefore}, marks now {_a.MarkCount}");
                        continue;
                    }
                    if (_player.SkillCooldownRemaining(Triple) <= 0f) _player.PressSkill(Triple, at);
                    else if (_player.SkillCooldownRemaining(Quick) <= 0f) _player.PressSkill(Quick, at);
                    else if (_player.SkillCooldownRemaining(Backstep) <= 0f) { _player.PressSkill(Backstep, at); stepping = true; }
                    else _player.Shoot(at);
                }
                else _player.Shoot(at);
                yield return null;
            }
            Assert.That(marksAtBlast.Count, Is.GreaterThanOrEqualTo(4), "four blasts in the time");
            string blasts = string.Join(", ", marksAtBlast.Select((m, i) => $"{m} marks: {dealtAtBlast.ElementAtOrDefault(i)}"));
            Debug.Log($"[0005] start four on cooldown, Backstep's second press {(cleanBackstep ? "clean" : "early")}: "
                    + $"marks when Scatter Blast came off cooldown {string.Join(", ", marksWhenReady)}; "
                    + $"at each Scatter Blast {blasts} (with no marks {blast.Damage}); "
                    + $"back at {kit.MaxMarks} marks after each blast: {string.Join(", ", backAtFive)}; "
                    + $"Backstep shots {string.Join(", ", steps)} (ordinary {step.Damage}, clean at five "
                    + $"{MarkRules.SpenderDamage(step.Damage, 5, step.CleanMarkDamageBonus)}); "
                    + $"focus first full {fullAt:0.0} s after the start; marks fall off {kit.MarkLifetime} s after the last");
            Assert.That(steps.Count, Is.GreaterThanOrEqualTo(1), "Backstep fired its shot at least once");
            if (cleanBackstep)
            {
                foreach (KeyValuePair<int, int> spend in stepSpends)
                    Assert.That(spend.Value, Is.EqualTo(MarkRules.SpenderDamage(step.Damage, spend.Key, step.CleanMarkDamageBonus)),
                                $"a clean Backstep on {spend.Key} marks");
            }
            else
            {
                Assert.That(marksAtBlast.Skip(1).Min(), Is.GreaterThanOrEqualTo(3), "the rotation builds marks for the blast");
            }
        }

        // ---------------- Helpers ----------------

        /// <summary>Gives the player a copy of the skill settings with a change, so the asset itself is never touched.</summary>
        void OwnSkills(Action<SkillSettings> change)
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet);
            _copies.Add(copy);
            change(copy);
            _player.SkillSet = copy;
        }

        /// <summary>From open ground, runs right until she is at full run speed.</summary>
        IEnumerator RunRight()
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right);
            yield return Until(() => _player.Speed >= _feel.RunSpeed - 0.01f, 2f);
            yield return new WaitForSeconds(0.1f);
            Assume.That(_player.Speed, Is.GreaterThan(_feel.RunSpeed - 0.05f), "at full run speed");
        }

        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
