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
    /// Work order 015 (2026-11-20), through the intents in the ControlRoom scene: a pool skill taken in a run and fired
    /// from its key; Long Shot let go in its perfect band and outside it; Tag Round's tag; Breaker Slug's stagger per mark
    /// on a guard; Slide's two charges; Stopping Shot's counter on the practice brute; Powder Keg's fuse and its burst
    /// when shot. Every number is read from the skills. Waits are in game time or real time with a limit, never frames
    /// of a supposed length.
    /// </summary>
    public class SkillPoolTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here
        static readonly Vector3 OpenGround = new Vector3(-8f, 0f, -8f);

        PlayerCharacter _player;
        Damageable _a;
        GeneratedLevels _levels;
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
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _damage.Clear();
            _a.Damaged += d => _damage.Add(d);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
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

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ---------------- The bar in a run ----------------

        [UnityTest]
        public IEnumerator ARun_APoolSkillTaken_GoesOnTheFirstFreeKey_AndFiresFromIt()
        {
            _levels = GeneratedLevels.Ensure();
            // Changed deliberately (work order 138): these pin the floor reward of before (three skills, the focus screen
            // after floor 1) on eight slots, so its six slots and its reward of three kinds are off here.
            KitSwitchesForTests.SkillsOnlyReward(_levels);
            KitSwitchesForTests.EightSlots(_player);
            _levels.StartRun(0x0BADF00DUL);
            yield return null;
            Assert.That(_player.FirstFreeKey, Is.EqualTo(1), "the start four on Q, R, F and the right button: W's key is free");
            _levels.FinishFloorForTests();
            for (int i = 0; i < 600 && _levels.Choice != GeneratedLevels.ChoiceKind.FloorReward; i++) yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            foreach (int s in _levels.OfferedSkills) Assert.That(_player.Skills[s].StartEquipped, Is.False, "out of the pool");
            int picked = _levels.OfferedSkills[0];
            Assert.That(_levels.PickReward(0), Is.True);
            Assert.That(_levels.PickFocusChoice(0), Is.True); // changed deliberately (work order 025): the focus choice before floor 2
            Assert.That(_player.KeyOfSkill(picked), Is.EqualTo(1));
            Assert.That(_player.SkillOnKey(1), Is.EqualTo(picked));
            for (int i = 0; i < 5; i++) yield return null;

            // Its key (bit 1), through the input driver.
            int charges = _player.SkillChargesReady(picked);
            var frames = new Queue<InputFrame>();
            var screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            frames.Enqueue(new InputFrame { CursorScreen = screen, SkillsPressed = 1 << 1, SkillsHeld = 1 << 1 });
            frames.Enqueue(new InputFrame { CursorScreen = screen });
            _player.GetComponent<PlayerInputDriver>().InputSource = () => frames.Count > 0 ? frames.Dequeue() : (InputFrame?)null;
            yield return Until(() => frames.Count == 0, 2f);
            yield return null;
            Assert.That(_player.SkillChargesReady(picked), Is.LessThan(charges), _player.Skills[picked].Name + " fired from its key: its cooldown runs");
        }

        // ---------------- Long Shot's perfect band ----------------

        IEnumerator LongShotReleasedAt(float seconds, Action<int, bool> result)
        {
            int slot = Slot("Long Shot");
            _player.Teleport(NearA);
            yield return null;
            int perfect = _player.PerfectReleases;
            _damage.Clear();
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillChargedSeconds >= seconds, 5f);
            float charged = _player.SkillChargedSeconds;
            _player.ReleaseSkill(slot);
            yield return Until(() => _damage.Count > 0, 3f);
            Assert.That(_damage.Count, Is.EqualTo(1), "one hit, released at " + charged + " s");
            result(_damage[0], _player.PerfectReleases > perfect);
        }

        [UnityTest]
        public IEnumerator LongShot_ReleasedInTheBand_IsPerfect_TwiceTheFullChargeDamage_AndFocus()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            float focus = _player.Focus;
            int dealt = 0;
            bool perfect = false;
            yield return LongShotReleasedAt(0.5f * (skill.PerfectFrom + skill.PerfectTo), (d, p) => { dealt = d; perfect = p; });
            Assert.That(perfect, Is.True);
            Assert.That(dealt, Is.EqualTo(PoolRules.PerfectDamage(skill)));
            Assert.That(dealt, Is.EqualTo(2 * skill.Damage));
            Assert.That(_player.Focus - focus, Is.GreaterThanOrEqualTo(_player.Identity.FocusPerCleanTap - 1e-3f), "as a clean tap does");
        }

        [UnityTest]
        public IEnumerator LongShot_ReleasedBeforeTheBand_IsAnOrdinaryShot()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            int dealt = 0;
            bool perfect = true;
            yield return LongShotReleasedAt(0.4f, (d, p) => { dealt = d; perfect = p; });
            Assert.That(perfect, Is.False);
            Assert.That(dealt, Is.LessThan(skill.Damage), "by how long it was charged");
        }

        [UnityTest]
        public IEnumerator LongShot_ReleasedAfterTheBand_IsAnOrdinaryFullCharge()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            int dealt = 0;
            bool perfect = true;
            yield return LongShotReleasedAt(skill.PerfectTo + 0.25f, (d, p) => { dealt = d; perfect = p; });
            Assert.That(perfect, Is.False);
            Assert.That(dealt, Is.EqualTo(skill.Damage));
        }

        // ---------------- Tag Round ----------------

        [UnityTest]
        public IEnumerator TagRound_TheTargetTakesMoreFromLaterHits_For8Seconds()
        {
            int tag = Slot("Tag Round"), quick = Slot("Quick Shot");
            SkillDefinition tagRound = _player.Skills[tag], quickShot = _player.Skills[quick];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(tag, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage[0], Is.EqualTo(tagRound.Damage), "the tagging shot itself is not raised");
            Assert.That(_a.IsTagged, Is.True);
            yield return null;
            Assert.That(MarkPips.For(_a, null).TagShown, Is.True, "its sign shows over it");
            yield return UntilFree();

            _damage.Clear();
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage[0], Is.EqualTo(Mathf.RoundToInt(quickShot.Damage * (1f + tagRound.TagDamageShare))), "10 % more while tagged");

            yield return Until(() => !_a.IsTagged, tagRound.TagSeconds + 3f);
            Assert.That(_a.IsTagged, Is.False, "gone after its time");
            yield return Until(() => _player.SkillCooldownRemaining(quick) <= 0f, 5f);
            yield return UntilFree();
            _damage.Clear();
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(_damage[0], Is.EqualTo(quickShot.Damage), "no more than its own after 8 s");
        }

        // ---------------- Breaker Slug ----------------

        IEnumerator GuardHitByTheSlug(int marks, Action<EnemyGuard> got)
        {
            if (_levels == null) _levels = GeneratedLevels.Ensure();
            EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
            if (practice != null) practice.enabled = false;
            _player.Teleport(new Vector3(8f, 0f, -6f));
            yield return null;
            var guard = (EnemyGuard)_levels.SpawnPractice(EnemyKind.Guard).Single();
            guard.Tuning.Enemy.MoveSpeed = 0f;
            guard.Tuning.Enemy.AggroRange = 0f;
            _player.Teleport(guard.transform.position + Vector3.back * 5f);
            yield return null;
            if (marks > 0) guard.Body.AddMarks(marks, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(Slot("Breaker Slug"), guard.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => guard.Stagger.Current < guard.Stagger.Max || guard.IsDazed, 3f);
            Assert.That(guard.Body.HitsTaken, Is.EqualTo(1), "the slug hit it");
            got(guard);
            Object.Destroy(guard.gameObject);
            yield return null;
        }

        // Breaker Slug empties a guard's bar sooner with marks than without: one slug with none leaves some of the bar,
        // one slug on two marks empties it. Two tests, so neither waits out the 20 s cooldown.

        [UnityTest]
        public IEnumerator BreakerSlug_WithoutMarks_TakesItsOwnStagger_AndLeavesSomeOfTheBar()
        {
            SkillDefinition slug = _player.Skills[Slot("Breaker Slug")];
            EnemyGuard g = null;
            float max = 0f, current = 0f;
            bool dazed = true;
            yield return GuardHitByTheSlug(0, x => { g = x; max = x.Stagger.Max; current = x.Stagger.Current; dazed = x.IsDazed; });
            Assume.That(max, Is.GreaterThan(slug.Stagger), "one slug alone does not empty a bar");
            Assert.That(current, Is.EqualTo(max - slug.Stagger).Within(1e-3f), "no marks: its own stagger");
            Assert.That(dazed, Is.False);
        }

        [UnityTest]
        public IEnumerator BreakerSlug_OnTwoMarks_SixtyPercentMoreStagger_EmptiesTheBar_AndTakesTheMarks()
        {
            SkillDefinition slug = _player.Skills[Slot("Breaker Slug")];
            float max = 0f;
            bool dazed = false;
            int marks = -1, hpLost = 0;
            yield return GuardHitByTheSlug(2, x =>
            {
                max = x.Stagger.Max;
                dazed = x.IsDazed;
                marks = x.Body.MarkCount;
                hpLost = x.Body.MaxHealth - x.Body.CurrentHealth;
            });
            Assume.That(max, Is.LessThanOrEqualTo(PoolRules.SpenderStagger(slug.Stagger, 2, slug.MarkStaggerBonus)));
            Assert.That(dazed, Is.True, "two marks: 60 % more, and the bar is empty");
            Assert.That(marks, Is.EqualTo(0), "it took the marks");
            Assert.That(hpLost, Is.EqualTo(slug.Damage), "and no more damage");
        }

        // ---------------- Slide ----------------

        [UnityTest]
        public IEnumerator Slide_Moves6MetresTheWayTheKeysPoint_Twice_AndNotAThirdTime()
        {
            int slide = Slot("Slide");
            SkillDefinition skill = _player.Skills[slide];
            _player.Teleport(OpenGround);
            yield return null;
            Assert.That(_player.SkillChargesReady(slide), Is.EqualTo(2));

            foreach (Vector3 way in new[] { Vector3.right, Vector3.left })
            {
                Vector3 from = _player.Position;
                _player.Steer(way);
                Assert.That(_player.PressSkill(slide, from + Vector3.forward * 5f), Is.EqualTo(IntentResult.Started), "the way the keys point, not the cursor");
                _player.Stop();
                Assert.That(_player.IsKeyMoving, Is.True);
                // A basic shot during it goes off and does not slow it.
                int shots = _player.BasicShotsFired;
                _player.Shoot(from + Vector3.forward * 5f);
                yield return Until(() => !_player.IsKeyMoving, 2f);
                Vector3 moved = _player.Position - from;
                Assert.That(Vector3.Dot(moved, way), Is.EqualTo(skill.MoveDistance).Within(0.15f), "6 m along the keys");
                Assert.That(Mathf.Abs(moved.z), Is.LessThan(0.15f));
                yield return Until(() => _player.BasicShotsFired > shots, 1f);
                Assert.That(_player.BasicShotsFired, Is.GreaterThan(shots), "she shot during it");
                yield return null;
            }
            Assert.That(_player.SkillChargesReady(slide), Is.EqualTo(0));
            Vector3 at = _player.Position;
            Assert.That(_player.PressSkill(slide, at), Is.EqualTo(IntentResult.Rejected), "no third time");
            Assert.That(_player.SkillNextCharge(slide), Is.GreaterThan(0f).And.LessThanOrEqualTo(skill.Cooldown));
        }

        // ---------------- Stopping Shot ----------------

        IEnumerator BruteWindingUp(float distance, string attack, Action<EnemyBrute> got)
        {
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice brute");
            Vector3 home = brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * distance);
            yield return Until(() => brute.State == EnemyState.Windup, 6f);
            Assume.That(brute.State, Is.EqualTo(EnemyState.Windup), "the brute winds up");
            Assume.That(brute.Attack?.Name, Is.EqualTo(attack));
            got(brute);
        }

        [UnityTest]
        public IEnumerator StoppingShot_FromTheFront_OnTheShockwave_DropsIt_AndDazesTheBrute()
        {
            int stop = Slot("Stopping Shot");
            SkillDefinition skill = _player.Skills[stop];
            EnemyBrute brute = null;
            yield return BruteWindingUp(7f, "Shockwave", b => brute = b);
            Assert.That(brute.TelegraphIsCounterable, Is.True, "drawn blue");
            float focus = _player.Focus;
            int strikes = brute.Strikes;
            Assert.That(_player.PressSkill(stop, brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Countered > 0 || brute.State != EnemyState.Windup, 3f);
            Assert.That(brute.Countered, Is.EqualTo(1));
            Assert.That(brute.IsDazed, Is.True);
            Assert.That(brute.State, Is.Not.EqualTo(EnemyState.Windup));
            Assert.That(_player.Counters, Is.EqualTo(1));
            Assert.That(_player.Focus - focus, Is.GreaterThanOrEqualTo(_player.Identity.MaxFocus * skill.CounterFocus - 1e-2f), "a third of the gauge");
            yield return For(skill.CounterDaze - 0.3f);
            Assert.That(brute.IsDazed, Is.True, "for its time");
            Assert.That(brute.Strikes, Is.EqualTo(strikes), "no strike");
            yield return For(0.5f);
            Assert.That(brute.IsDazed, Is.False, "and then no more");
        }

        [UnityTest]
        public IEnumerator StoppingShot_FromBehind_DoesNotCounter()
        {
            int stop = Slot("Stopping Shot");
            EnemyBrute brute = null;
            yield return BruteWindingUp(7f, "Shockwave", b => brute = b);
            Vector3 behind = brute.transform.position - brute.transform.forward * 4f;
            _player.Teleport(behind);
            yield return null;
            Assume.That(brute.State, Is.EqualTo(EnemyState.Windup));
            int hits = brute.Body.HitsTaken;
            Assert.That(_player.PressSkill(stop, brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Body.HitsTaken > hits, 2f);
            Assert.That(brute.Body.HitsTaken, Is.GreaterThan(hits), "the shot hit it");
            Assert.That(brute.Countered, Is.EqualTo(0));
            Assert.That(brute.IsDazed, Is.False);
            Assert.That(brute.State, Is.EqualTo(EnemyState.Windup), "the attack goes on");
        }

        [UnityTest]
        public IEnumerator StoppingShot_OnARedAttack_IsAnOrdinaryHit()
        {
            int stop = Slot("Stopping Shot");
            SkillDefinition skill = _player.Skills[stop];
            EnemyBrute brute = null;
            yield return BruteWindingUp(2.5f, "Slam", b => brute = b);
            Assert.That(brute.TelegraphIsCounterable, Is.False, "the slam is red");
            int hp = brute.Body.CurrentHealth;
            Assert.That(_player.PressSkill(stop, brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Body.CurrentHealth < hp, 2f);
            Assert.That(hp - brute.Body.CurrentHealth, Is.EqualTo(skill.Damage));
            Assert.That(brute.Countered, Is.EqualTo(0));
            Assert.That(brute.State, Is.EqualTo(EnemyState.Windup), "the slam goes on");
        }

        // ---------------- Powder Keg ----------------

        [UnityTest]
        public IEnumerator PowderKeg_LiesOnItsFuse_ThenBurstsByItself()
        {
            int keg = Slot("Powder Keg");
            SkillDefinition skill = _player.Skills[keg];
            _player.Teleport(NearA);
            yield return null;
            float pressed = Time.time;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsInLightSkill, Is.True, "thrown without being locked");
            yield return Until(() => _player.KegsLying > 0, 2f);
            Assert.That(_player.KegsLying, Is.EqualTo(1), "it landed");
            Assert.That(Time.time - pressed, Is.EqualTo(skill.Startup).Within(0.1f));
            Assert.That(_damage, Is.Empty, "nothing burst yet");
            yield return For(skill.Fuse - 0.3f);
            Assert.That(_damage, Is.Empty, "still on its fuse");
            yield return Until(() => _damage.Count > 0, 2f);
            Assert.That(Time.time - pressed, Is.EqualTo(skill.Startup + skill.Fuse).Within(0.1f), "it burst after the fuse");
            Assert.That(_damage, Is.EqualTo(new[] { skill.Damage }));
            Assert.That(_player.KegsLying, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PowderKeg_Shot_BurstsAtOnce_ForOneAndAHalfTimesTheDamage()
        {
            int keg = Slot("Powder Keg");
            SkillDefinition skill = _player.Skills[keg];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsLying > 0, 2f);
            float landed = Time.time;
            yield return UntilFree();
            int fired = _player.BasicShotsFired;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            Assert.That(Time.time - landed, Is.LessThan(skill.Fuse), "before the fuse ran out");
            Assert.That(_player.KegsLying, Is.EqualTo(0));
            Assert.That(_player.KegsSetOffByShots, Is.EqualTo(1));
            Assert.That(_damage, Does.Contain(Mathf.RoundToInt(skill.Damage * skill.ShotBurstScale)), "the higher damage");
            Assert.That(_damage, Does.Contain(Placeholders.BasicShotDamage), "the shot hit too");
        }
    }
}
