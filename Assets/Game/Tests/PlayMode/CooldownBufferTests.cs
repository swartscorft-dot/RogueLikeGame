using System;
using System.Collections;
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
    /// The cooldown buffer for every skill (work order 066, 2026-11-21, Kristian: "Ja, alle ferdigheter"), in the ControlRoom
    /// scene through the intents, for each of the twelve skills: a press 0.2 s before ready waits and goes the moment the
    /// skill is ready, aimed at the cursor then; 0.3 s before it is refused; with the value 0 every skill refuses as this
    /// morning. And, with two skills: the newer press wins; a waiting press is dropped by a stun, a pull, death, a choice
    /// and the pause menu; a skill with a charge left starts at once; Fan Fire and Long Shot buffered start as a press and
    /// hold only while the key is still down. The cooldown is put where a test wants it by
    /// <see cref="PlayerCharacter.SpendCooldownForTests"/>. Layout: Dummy_A at (4, 3), Dummy_B at (-4, 3).
    /// A frame without a window is about 0.3 ms: the waits count game time or real time, never frames.
    /// </summary>
    public class CooldownBufferTests
    {
        // The twelve, in the slots of SkillDefinition.Create (the settings asset's order).
        const int Quick = 0, Keg = 1, Fan = 2, Long = 3, Scatter = 4, Volley = 5, Backstep = 6, Triple = 7, Tag = 8, Breaker = 9, SlideSlot = 10, Stopping = 11;
        static readonly Vector3 Spot = new Vector3(0f, 0f, -2f);
        static readonly Vector3 AimFirst = new Vector3(4f, 0f, 3f);   // where the cursor is at the press: Dummy_A
        static readonly Vector3 AimThen = new Vector3(-4f, 0f, 3f);   // where it is when the skill is ready: Dummy_B

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        SkillSettings _copy;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _copy = Object.Instantiate(_player.SkillSet); // the settings asset is shared: a copy, for the value 0
            _player.SkillSet = _copy;
            // Changed deliberately (work order 109): fifteen, the three new skills after the twelve; the tests below are of the twelve.
            Assert.That(_player.Skills.Count, Is.EqualTo(15), "the twelve skills and the three of work order 109");
            Assert.That(_player.Skills.Take(12).Select(k => k.Name), Is.EqualTo(new[] { "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap", "Tag Round", "Breaker Slug", "Slide", "Stopping Shot" }), "in this order");
            for (int s = 0; s < _player.Skills.Count; s++) _player.EquipSkill(s);
            _player.Teleport(Spot);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_health != null) _health.Untouchable = false;
            if (_copy != null) Object.Destroy(_copy);
            _copy = null;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float realSeconds)
        {
            float deadline = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// Work order 154: until the condition holds, at most this many seconds of GAME time (and a minute of real time, however busy
        /// the machine is), for a test whose frames are fixed steps of game time (<see cref="FixedStep"/>).
        /// </summary>
        static IEnumerator UntilInGameTime(Func<bool> condition, float gameSeconds)
        {
            float end = Time.time + gameSeconds, deadline = Time.realtimeSinceStartup + 60f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// Work order 154: s of game time a frame (Time.captureDeltaTime) in the tests here that judge a rule of the game's time by
        /// its frames: beside a full EditMode run a frame carried up to a third of a second, and these tests failed by it on
        /// 2026-11-26 (alone, every one passed).
        /// </summary>
        const float FixedStep = 1f / 60f;

        /// <summary>True once the skill has started again: its cooldown runs from the start (Slide's second charge from its own).</summary>
        bool Started(int slot) => _player.SkillCooldownRemaining(slot) > 1f;

        /// <summary>The skill on cooldown with <paramref name="left"/> seconds to go, every charge spent; she free.</summary>
        IEnumerator OnCooldown(int slot, float left)
        {
            _player.Teleport(Spot);
            yield return null;
            _player.SpendCooldownForTests(slot, left);
            Assert.That(_player.SkillCooldownRemaining(slot), Is.EqualTo(left).Within(1e-3f));
            Assert.That(_player.SkillChargesReady(slot), Is.EqualTo(0));
        }

        // ---------------- The twelve ----------------

        [UnityTest]
        public IEnumerator EverySkill_APress02sBeforeReady_GoesAtReady_AimedAtTheCursorThen(
            [Values(Quick, Keg, Fan, Long, Scatter, Volley, Backstep, Triple, Tag, Breaker, SlideSlot, Stopping)] int slot)
        {
            string name = _player.Skills[slot].Name;
            yield return OnCooldown(slot, 0.2f);
            _player.AimAt(AimFirst);
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(slot, AimFirst), Is.EqualTo(IntentResult.Buffered), name + ": it waits");
            Assert.That(_player.HasBufferedPress, Is.True);
            if (_player.Skills[slot].Input != SkillInput.Holding && _player.Skills[slot].Input != SkillInput.Charge) _player.ReleaseSkill(slot);
            yield return null;
            _player.AimAt(AimThen); // the cursor moves on while it waits
            Assert.That(Started(slot), Is.False, name + ": not before ready");
            yield return Until(() => Started(slot), 3f);
            float took = Time.time - pressedAt;
            Assert.That(Started(slot), Is.True, name + ": it went");
            Assert.That(took, Is.EqualTo(0.2f).Within(0.03f), name + ": the moment the skill was ready");
            Assert.That(_player.HasBufferedPress, Is.False);
            if (_player.Skills[slot].Input != SkillInput.KeyMove) // Slide goes along the move keys, at no point
                Assert.That(Flat(_player.AimPoint - AimThen).magnitude, Is.LessThan(0.01f), name + ": aimed where the cursor was when it went");
            _player.ReleaseSkill(slot);
        }

        [UnityTest]
        public IEnumerator EverySkill_APress03sBeforeReady_IsRefused(
            [Values(Quick, Keg, Fan, Long, Scatter, Volley, Backstep, Triple, Tag, Breaker, SlideSlot, Stopping)] int slot)
        {
            yield return OnCooldown(slot, 0.3f);
            Assert.That(_player.PressSkill(slot, AimFirst), Is.EqualTo(IntentResult.Rejected), _player.Skills[slot].Name + ": refused, as before");
            Assert.That(_player.HasBufferedPress, Is.False);
            yield return Until(() => _player.SkillCooldownRemaining(slot) <= 0f, 3f);
            yield return null;
            Assert.That(Started(slot), Is.False, "nothing went at ready");
        }

        [UnityTest]
        public IEnumerator EverySkill_WithTheValue0_RefusesAsThisMorning(
            [Values(Quick, Keg, Fan, Long, Scatter, Volley, Backstep, Triple, Tag, Breaker, SlideSlot, Stopping)] int slot)
        {
            _player.Kit.CooldownBuffer = 0f;
            yield return OnCooldown(slot, 0.1f);
            Assert.That(_player.PressSkill(slot, AimFirst), Is.EqualTo(IntentResult.Rejected), _player.Skills[slot].Name);
            Assert.That(_player.HasBufferedPress, Is.False);
            yield return Until(() => _player.SkillCooldownRemaining(slot) <= 0f, 3f);
            yield return null;
            Assert.That(Started(slot), Is.False);
        }

        // ---------------- One at a time, and the drops ----------------

        [UnityTest]
        public IEnumerator TheNewerPress_Wins_AndADashThatStartsDropsIt()
        {
            yield return OnCooldown(Scatter, 0.2f);
            _player.SpendCooldownForTests(Breaker, 0.15f);
            Assert.That(_player.PressSkill(Scatter, AimFirst), Is.EqualTo(IntentResult.Buffered));
            _player.ReleaseSkill(Scatter);
            Assert.That(_player.PressSkill(Breaker, AimFirst), Is.EqualTo(IntentResult.Buffered), "a newer press of another skill");
            // Work order 082 (review 6, finding 30): the one waiting press is the newer one, seen in the buffer itself (the
            // older Scatter press, kept, would be dropped as too old anyway, so the end of this test cannot tell).
            Assert.That(_player.BufferedSkillSlot, Is.EqualTo(Breaker), "the newer press replaced the older");
            _player.ReleaseSkill(Breaker);
            yield return Until(() => Started(Breaker), 3f);
            Assert.That(Started(Breaker), Is.True, "the newer one went");
            yield return Until(() => _player.Action == ActionKind.None, 3f);
            yield return Until(() => _player.SkillCooldownRemaining(Scatter) <= 0f, 3f);
            yield return null;
            Assert.That(Started(Scatter), Is.False, "the older one was replaced");

            yield return OnCooldown(Scatter, 0.2f);
            Assert.That(_player.PressSkill(Scatter, AimFirst), Is.EqualTo(IntentResult.Buffered));
            _player.ReleaseSkill(Scatter);
            Assert.That(_player.DashToward(AimThen), Is.EqualTo(IntentResult.Started), "a dash that starts");
            Assert.That(_player.HasBufferedPress, Is.False, "it replaced the waiting press");
            yield return Until(() => _player.SkillCooldownRemaining(Scatter) <= 0f, 3f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Started(Scatter), Is.False);
        }

        [UnityTest]
        public IEnumerator AWaitingPress_IsDropped_ByAStunAPullDeathThePauseMenuAndAChoice(
            [Values("stun", "pull", "death", "pause", "choice")] string by)
        {
            if (by == "choice" || by == "pause")
            {
                _levels = GeneratedLevels.Ensure();
                _levels.RunValues.SkillTiers = false;
            }
            if (by == "choice")
            {
                _levels.StartRun(0x0660002UL);
                yield return null;
                _health.Untouchable = true;
                GeneratedLevel.PlacedProp chest = _levels.Chests().First();
                Vector3 c = chest.Object.transform.position, middle = _levels.Level.Middle(chest.Room);
                Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
                if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
                _player.Teleport(c + toward.normalized * 1.4f);
                yield return null;
                yield return null;
                _player.SpendCooldownForTests(Scatter, 0.2f);
            }
            else yield return OnCooldown(Scatter, 0.2f);
            Assert.That(_player.PressSkill(Scatter, _player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Buffered));
            _player.ReleaseSkill(Scatter);
            switch (by)
            {
                case "stun": Assert.That(_player.Stun(0.05f, "test"), Is.True); break;
                case "pull": Assert.That(_player.Pull(_player.Position + Vector3.right * 0.5f, 0.05f, "test"), Is.True); break;
                case "death": _health.TakeHit(100000); break;
                case "pause": Assert.That(_levels.Pause(), Is.True); break;
                case "choice": Assert.That(_levels.OpenChest(), Is.True); break;
            }
            yield return null;
            Assert.That(_player.HasBufferedPress, Is.False, by + ": the waiting press is dropped");
            if (by == "pause") _levels.Resume();
            if (by == "choice") _levels.PickUpgrade(0);
            yield return null;
            yield return Until(() => _player.SkillCooldownRemaining(Scatter) <= 0f, 3f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(Started(Scatter), Is.False, by + ": nothing went at ready");
        }

        [UnityTest]
        public IEnumerator Slide_WithAChargeLeft_StartsAtOnce_TheBufferUntouched()
        {
            _player.Teleport(Spot);
            yield return null;
            Assert.That(_player.PressSkill(SlideSlot, AimFirst), Is.EqualTo(IntentResult.Started), "the first charge");
            yield return Until(() => !_player.IsKeyMoving, 2f);
            Assert.That(_player.SkillChargesReady(SlideSlot), Is.EqualTo(1));
            Assert.That(_player.SkillCooldownRemaining(SlideSlot), Is.EqualTo(0f), "a charge left: not on cooldown");
            Assert.That(_player.PressSkill(SlideSlot, AimFirst), Is.EqualTo(IntentResult.Started), "the second, at once");
            Assert.That(_player.HasBufferedPress, Is.False);
        }

        // ---------------- Hold skills ----------------

        [UnityTest]
        public IEnumerator FanFire_Buffered_HoldsWhileTheKeyIsStillDown_AndStopsAtOnceIfItWentUp([Values(true, false)] bool stillDown)
        {
            // Work order 154: every frame 1/60 s of game time: beside a full run (2026-11-26) the frame the buffered press went in
            // was long enough to hold its whole use, and it had ended by the frame it was looked at ("it started as a press": null).
            Time.captureDeltaTime = FixedStep;
            yield return OnCooldown(Fan, 0.2f);
            Assert.That(_player.PressSkill(Fan, AimFirst), Is.EqualTo(IntentResult.Buffered));
            if (!stillDown) _player.ReleaseSkill(Fan); // let go while it waits
            yield return UntilInGameTime(() => Started(Fan), 3f);
            Assert.That(_player.ActiveSkill?.Name, Is.EqualTo("Fan Fire"), "it started as a press");
            float startedAt = Time.time;
            SkillDefinition fan = _player.Skills[Fan];
            yield return UntilInGameTime(() => _player.Action == ActionKind.None || Time.time - startedAt > fan.Startup + fan.Recovery + 0.4f, 4f);
            if (stillDown)
            {
                Assert.That(_player.ActiveSkill?.Name, Is.EqualTo("Fan Fire"), "held: still firing past a tap's length");
                Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel));
                _player.ReleaseSkill(Fan);
            }
            else Assert.That(Time.time - startedAt, Is.LessThanOrEqualTo(fan.Startup + fan.Recovery + fan.TickInterval + 0.05f), "let go: a tap's worth");
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator LongShot_Buffered_ChargesWhileTheKeyIsStillDown_AndFiresUnchargedIfItWentUp([Values(true, false)] bool stillDown)
        {
            Time.captureDeltaTime = FixedStep; // work order 154: as Fan Fire's above (2026-11-26: "Long Shot" was null)
            yield return OnCooldown(Long, 0.2f);
            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Long, AimFirst), Is.EqualTo(IntentResult.Buffered));
            if (!stillDown) _player.ReleaseSkill(Long);
            yield return UntilInGameTime(() => Started(Long), 3f);
            Assert.That(_player.ActiveSkill?.Name, Is.EqualTo("Long Shot"));
            float startedAt = Time.time;
            SkillDefinition shot = _player.Skills[Long];
            yield return UntilInGameTime(() => _player.SkillShotsFired > fired || Time.time - startedAt > shot.Startup + 0.5f, 4f);
            if (stillDown)
            {
                Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "held: it charges, nothing fired yet");
                _player.ReleaseSkill(Long);
                yield return UntilInGameTime(() => _player.SkillShotsFired > fired, 2f);
                Assert.That(_player.SkillShotsFired, Is.GreaterThan(fired), "and fires when let go");
            }
            else Assert.That(_player.SkillShotsFired, Is.GreaterThan(fired), "let go while it waited: it fires at once, uncharged");
            Time.captureDeltaTime = 0f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
