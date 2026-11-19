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

        [UnityTest]
        public IEnumerator Builders_MarkTheTarget_AndTheBasicShotEveryFourthHit_AndThePipsShowThem()
        {
            KitValues kit = _player.Kit;
            Assume.That(kit.BasicShotsPerMark, Is.EqualTo(4));
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
            Assert.That(_player.RecastCleanShare, Is.EqualTo(0f), "the second press cannot be clean");
            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started), "the second press");
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.RecastCleanShare, Is.EqualTo(kit.CleanShare).Within(1e-5f), "the third can");
            if (clean) yield return Until(() => _player.RecastWindowLeft <= kit.CleanShare * 0.5f, 2f);
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
        /// for three Scatter Blasts after the first. Logs the marks on the target at each blast, what the blast dealt
        /// against what it deals with none, and when the focus gauge was first full. Backstep steps her 5 m away, out of
        /// Scatter Blast's reach, so she is put back where she stood after each one. The dummy is given a health it
        /// cannot lose in the time, so it never dies and drops its marks. Nothing is spent: the gauge fills once.
        /// </summary>
        [UnityTest]
        public IEnumerator Measure_TheStartFourOnCooldown_MarksAtEachBlast_AndTheTimeToFillFocus()
        {
            _player.EquipStartSkills();
            _a.SetMaxHealth(1000000);
            _player.Teleport(NearA);
            yield return null;
            SkillDefinition blast = _player.Skills[Scatter];
            KitValues kit = _player.Kit;
            var marksAtBlast = new List<int>();
            var dealtAtBlast = new List<int>();
            float start = Time.time, fullAt = float.NaN;
            bool stepping = false;
            float deadline = Time.realtimeSinceStartup + 90f;
            while ((marksAtBlast.Count < 4 || (float.IsNaN(fullAt) && Time.time - start < 40f)) && Time.realtimeSinceStartup < deadline)
            {
                Vector3 at = _a.transform.position;
                if (float.IsNaN(fullAt) && _player.IsFocusFull) fullAt = Time.time - start;
                // Not Action None: a click always waits, so a basic shot is nearly always going.
                if (stepping && _player.Action != ActionKind.Skill && !_player.IsDashing)
                {
                    _player.Teleport(NearA);
                    stepping = false;
                }
                bool free = !_player.IsLocked && _player.SkillGateRemaining <= 0f;
                if (_player.RecastSlot == Triple && _player.IsRecastWindowOpen
                    && (_player.RecastCleanShare <= 0f || _player.RecastWindowLeft <= _player.RecastCleanShare * 0.5f))
                    _player.PressSkill(Triple, at);
                else if (free && _player.RecastSlot < 0)
                {
                    if (_player.SkillCooldownRemaining(Scatter) <= 0f)
                    {
                        marksAtBlast.Add(_a.MarkCount);
                        int before = _a.MaxHealth - _a.CurrentHealth, firedBefore = _player.SkillShotsFired;
                        float distance = Flat(at - _player.Position).magnitude;
                        IntentResult pressed = _player.PressSkill(Scatter, at);
                        yield return Until(() => _player.SkillShotsFired > firedBefore, 2f);
                        int dealt = _a.MaxHealth - _a.CurrentHealth - before;
                        dealtAtBlast.Add(dealt);
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
            Debug.Log($"[0005] start four on cooldown: at each Scatter Blast {blasts} (with no marks {blast.Damage}); "
                    + $"focus first full {fullAt:0.0} s after the start; marks fall off {kit.MarkLifetime} s after the last");
            Assert.That(marksAtBlast.Skip(1).Min(), Is.GreaterThanOrEqualTo(3), "the rotation builds marks for the blast");
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
