using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Decision 0004, skills on the move: the free recovery, the slide, the pose in the slide, the arms held up after a
    /// shot, and the dash direction. The character is driven through its intent API or by frames fed to the input
    /// driver; the real mouse and keyboard never are. Values come from the settings assets; a test that needs a value
    /// changed uses a copy of the asset, never the asset itself. The measured numbers go to the console, "[0004]".
    /// </summary>
    public class SkillsOnTheMoveTests
    {
        const int Quick = 0, Keg = 1, Scatter = 4, Volley = 5, Backstep = 6, Triple = 7;

        // An open stretch of floor: no dummy within reach of the shots, the enemy beyond its aggro range.
        static readonly Vector3 RunFrom = new Vector3(-14f, 0f, -5f);
        static readonly Vector3 StandAt = new Vector3(-8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        CharacterAnimation _view;
        FeelValues _feel;
        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;
        readonly List<Object> _copies = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            Assert.That(_player.Settings, Is.Not.Null, "the player uses the settings asset");
            _feel = _player.Feel;
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null;
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
            foreach (Object copy in _copies) if (copy != null) Object.Destroy(copy);
            _copies.Clear();
        }

        // ---------------- The free recovery ----------------

        [UnityTest] public IEnumerator FreeRecovery_QuickShot_AClickDuringIt_ShootsAtItsLastHit() => ShotAfterSkill(Quick, free: true);
        [UnityTest] public IEnumerator FreeRecovery_PowderKeg_AClickDuringIt_ShootsAtItsLastHit() => ShotAfterSkill(Keg, free: true);
        [UnityTest] public IEnumerator FreeRecovery_HeavyVolley_AClickDuringIt_ShootsAtItsLastHit() => ShotAfterSkill(Volley, free: true);
        [UnityTest] public IEnumerator SwitchOff_QuickShot_AClickDuringIt_WaitsOutTheRecovery() => ShotAfterSkill(Quick, free: false);
        [UnityTest] public IEnumerator SwitchOff_PowderKeg_AClickDuringIt_WaitsOutTheRecovery() => ShotAfterSkill(Keg, free: false);
        [UnityTest] public IEnumerator SwitchOff_HeavyVolley_AClickDuringIt_WaitsOutTheRecovery() => ShotAfterSkill(Volley, free: false);

        /// <summary>
        /// A click made while the skill commits her: with the free recovery its shot starts at the skill's last hit and
        /// goes off a basic shot's startup later; with the switch off it waits out the recovery first. Measured in game
        /// time from the skill's last hit (nothing is hit, so no hit stop comes in between).
        /// </summary>
        IEnumerator ShotAfterSkill(int slot, bool free)
        {
            if (!free) OwnFeel(f => f.SkillRecoveryFree = false);
            Assume.That(_feel.SkillRecoveryFree, Is.EqualTo(free));
            SkillDefinition skill = _player.Skills[slot];
            Assume.That(skill.Input, Is.Not.EqualTo(SkillInput.Holding).And.Not.EqualTo(SkillInput.Charge), "a skill that fires by itself");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f; // open ground, down the screen
            int skillShots = _player.SkillShotsFired, shots = _player.BasicShotsFired;

            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(slot);
            float lastHit = pressedAt + skill.Startup + (Mathf.Max(1, skill.HitCount) - 1) * skill.HitInterval;
            yield return null;
            Assume.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Startup), "the click comes while the skill commits her");
            _player.Shoot(aim);
            Assert.That(_player.HasPendingShot, Is.True, "the click waits");

            float firedAt = float.NaN, maxDt = 0f;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (float.IsNaN(firedAt) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                maxDt = Mathf.Max(maxDt, Time.deltaTime);
                if (_player.BasicShotsFired > shots) firedAt = Time.time;
            }
            Assert.That(firedAt, Is.Not.NaN, "the shot went off");
            Assert.That(_player.SkillShotsFired - skillShots, Is.EqualTo(Mathf.Max(1, skill.HitCount)), "after the skill's hits");
            float delay = firedAt - lastHit;
            Debug.Log($"[0004] {skill.Name}, free recovery {(free ? "on" : "off")}: first basic shot went off {delay:0.000} s after the last hit "
                    + $"(startup {_feel.BasicShotStartup}, recovery {skill.Recovery}, longest frame {maxDt:0.000} s)");
            if (free)
                Assert.That(delay, Is.LessThanOrEqualTo(_feel.BasicShotStartup + 2f * maxDt + 0.001f),
                            "the shot starts at the last hit and goes off one startup later");
            else
            {
                Assert.That(delay, Is.GreaterThanOrEqualTo(skill.Recovery + _feel.BasicShotStartup - 0.001f), "it waited out the recovery");
                Assert.That(delay, Is.LessThanOrEqualTo(skill.Recovery + _feel.BasicShotStartup + 2f * maxDt + 0.001f));
            }
        }

        [UnityTest]
        public IEnumerator FreeRecovery_ASecondSkill_WaitsUntilTheRecoveryWouldHaveEnded()
        {
            CommittedQuickShot();
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(quick.Recovery, Is.GreaterThan(0.12f));
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float gate = pressedAt + quick.Startup + quick.Recovery;
            yield return Until(() => _player.FreeRecoverySlot == Quick, 2f);
            Assert.That(_player.FreeRecoverySlot, Is.EqualTo(Quick), "in Quick Shot's free recovery");
            Assert.That(_player.IsLocked, Is.False, "which does not lock");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(-1));

            yield return Until(() => Time.time >= gate - _feel.InputBuffer * 0.5f, 2f);
            Assume.That(Time.time, Is.LessThan(gate), "the press comes inside the recovery");
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Buffered), "the next skill waits");
            yield return StartedAt(Volley, gate);
        }

        [UnityTest]
        public IEnumerator FreeRecovery_ASkillPressedDuringAShotFiredInIt_StillWaitsOutTheRecovery()
        {
            CommittedQuickShot();
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            float cycle = _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;
            Assume.That(quick.Recovery, Is.GreaterThan(cycle + 0.04f), "room for a second shot to start inside the recovery");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            int shots = _player.BasicShotsFired;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float lastHit = pressedAt + quick.Startup, gate = lastHit + quick.Recovery;
            yield return Until(() => _player.FreeRecoverySlot == Quick, 2f);

            // A shot in the free recovery: it does not lock, so it must not open a way past the recovery.
            _player.Shoot(aim);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "the shot starts at once");
            yield return Until(() => _player.BasicShotsFired > shots, 1f);
            // Pressed late enough to be still fresh in the buffer when the recovery ends, as during a lock.
            yield return Until(() => Time.time >= gate - _feel.InputBuffer * 0.66f, 1f);
            Assume.That(_player.IsInShotRecovery, Is.True, "in the shot's recovery");
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Buffered), "during the shot's recovery, the skill still waits");

            // A second shot, winding up as the recovery runs out: the waiting skill takes over at the recovery's end.
            yield return Until(() => Time.time >= lastHit + cycle + 0.005f, 1f);
            Assume.That(Time.time, Is.LessThan(gate - 0.02f));
            _player.Shoot(aim);
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "the second shot winds up");
            Assert.That(_player.HasBufferedPress, Is.True, "the skill still waits");
            yield return StartedAt(Volley, gate);
            Assert.That(_player.BasicShotsFired - shots, Is.EqualTo(1), "the skill cancelled the second shot before it went off");
        }

        /// <summary>Waits for a skill to start and checks it started at the gate: not before, and within two frames after.</summary>
        IEnumerator StartedAt(int slot, float gate)
        {
            float startedAt = float.NaN, maxDt = 0f, earliest = float.PositiveInfinity;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (float.IsNaN(startedAt) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                maxDt = Mathf.Max(maxDt, Time.deltaTime);
                if (_player.ActiveSkillSlot == slot) startedAt = Time.time;
                else if (_player.Action == ActionKind.Skill) earliest = Mathf.Min(earliest, Time.time);
            }
            Assert.That(startedAt, Is.Not.NaN, "the waiting skill started");
            Assert.That(earliest, Is.EqualTo(float.PositiveInfinity), "no other skill started first");
            Debug.Log($"[0004] the waiting skill started {startedAt - gate:+0.000;-0.000} s from the end of the recovery");
            Assert.That(startedAt, Is.GreaterThanOrEqualTo(gate - 0.001f), "not before the recovery would have ended");
            Assert.That(startedAt, Is.LessThanOrEqualTo(gate + 2f * maxDt + 0.001f), "and at once when it did");
        }

        // ---------------- A skill key pressed in the free recovery (0004, amended 2026-11-19) ----------------

        /// <summary>
        /// A skill key pressed early in a free recovery, earlier than the buffer time before its end: it waits, and the
        /// skill starts when the recovery ends, not before. Under the old rule this press was dropped as stale.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeRecovery_ASkillPressedEarlyInIt_StartsWhenItEnds_AndNotBefore()
        {
            CommittedQuickShot();
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(quick.Recovery, Is.GreaterThan(_feel.InputBuffer + 0.06f), "room for a press earlier than the buffer time");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float gate = pressedAt + quick.Startup + quick.Recovery;
            yield return Until(() => _player.FreeRecoverySlot == Quick, 2f);
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Buffered), "the next skill waits");
            float early = gate - Time.time;
            Assume.That(early, Is.GreaterThan(_feel.InputBuffer + 0.02f), "pressed earlier than the buffer time before the end");
            Debug.Log($"[0004] a skill pressed {early:0.000} s before the end of Quick Shot's free recovery");
            yield return StartedAt(Volley, gate);
        }

        /// <summary>
        /// A skill key pressed while she is visibly committed, earlier than the buffer time before the last hit, is
        /// dropped as before. It waits through the free recovery without holding anything back, and when it is dropped
        /// at the recovery's end it must not cancel a shot winding up across that moment: the shot goes off, and the
        /// press starts nothing. (Before 2026-11-19 this test pressed early in the free recovery, which now fires.)
        /// </summary>
        [UnityTest]
        public IEnumerator FreeRecovery_ASkillPressedEarlierThanTheBufferTimeBeforeTheLastHit_IsDropped_AndCancelsNoShotAtTheGate()
        {
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition volley = _player.Skills[Volley];
            Assume.That(volley.Startup, Is.GreaterThan(_feel.InputBuffer + 0.1f), "a cast long enough to press early in it");
            Assume.That(volley.Recovery, Is.GreaterThan(_feel.BasicShotStartup + 0.05f), "room for a click in the recovery");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            int shots = _player.BasicShotsFired;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Started));
            float lastHit = pressedAt + volley.Startup, gate = lastHit + volley.Recovery;
            yield return null;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Buffered), "pressed in the cast");
            Assume.That(lastHit - Time.time, Is.GreaterThan(_feel.InputBuffer + 0.02f), "earlier than the buffer time before the last hit");

            yield return Until(() => _player.FreeRecoverySlot == Volley, 2f);
            Assert.That(_player.HasBufferedPress, Is.True, "it waits through the free recovery");

            // A shot that is still winding up when the recovery ends.
            yield return Until(() => Time.time >= gate - _feel.BasicShotStartup * 0.5f, 2f);
            Assume.That(Time.time, Is.LessThan(gate), "clicked before the gate");
            _player.Shoot(aim);
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "winding up across the gate");

            yield return Until(() => _player.BasicShotsFired > shots, 1f);
            Assert.That(_player.BasicShotsFired - shots, Is.EqualTo(1), "the shot went off");
            yield return new WaitForSeconds(0.2f);
            Assert.That(_player.Action, Is.Not.EqualTo(ActionKind.Skill), "the early press started nothing");
            Assert.That(_player.HasBufferedPress, Is.False, "and is gone");
            Assert.That(_player.SkillCooldownRemaining(Quick), Is.EqualTo(0f), "Quick Shot was never used");
        }

        /// <summary>A skill key pressed within the buffer time before the last hit fires when the free recovery ends.</summary>
        [UnityTest]
        public IEnumerator FreeRecovery_ASkillPressedJustBeforeTheLastHit_StartsWhenTheRecoveryEnds()
        {
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition volley = _player.Skills[Volley];
            Assume.That(volley.Startup, Is.GreaterThan(_feel.InputBuffer), "a cast longer than the buffer time");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Started));
            float lastHit = pressedAt + volley.Startup, gate = lastHit + volley.Recovery;
            yield return Until(() => Time.time >= lastHit - _feel.InputBuffer * 0.5f, 2f);
            Assume.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Startup), "still in the cast");
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Buffered));
            yield return Until(() => _player.FreeRecoverySlot == Volley, 1f);
            Assert.That(_player.FreeRecoverySlot, Is.EqualTo(Volley), "Heavy Volley let her go");
            Assert.That(_player.HasBufferedPress, Is.True, "and the press waits through its free recovery");
            yield return StartedAt(Quick, gate);
        }

        /// <summary>With the free recovery off the old lock and the old freshness rule hold: an early press is dropped.</summary>
        [UnityTest]
        public IEnumerator SwitchOff_ASkillPressedEarlyInTheRecovery_IsDropped()
        {
            CommittedQuickShot();
            OwnFeel(f => f.SkillRecoveryFree = false);
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(quick.Recovery, Is.GreaterThan(_feel.InputBuffer + 0.06f), "room for a press earlier than the buffer time");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float end = pressedAt + quick.Startup + quick.Recovery;
            yield return Until(() => _player.SkillPhase == SkillPhase.Recovery, 2f);
            Assert.That(_player.IsLocked, Is.True, "the recovery locks");
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Buffered));
            Assume.That(end - Time.time, Is.GreaterThan(_feel.InputBuffer + 0.02f), "earlier than the buffer time before the end");

            bool started = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.time < end + 0.3f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_player.ActiveSkillSlot == Volley) started = true;
            }
            Assert.That(started, Is.False, "the early press is dropped, as before");
            Assert.That(_player.HasBufferedPress, Is.False);
            Assert.That(_player.SkillCooldownRemaining(Volley), Is.EqualTo(0f), "Heavy Volley was never used");
        }

        /// <summary>
        /// While a skill press waits out a free recovery, a click still shoots and a move key still moves her; the
        /// skill then starts at the recovery's end.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeRecovery_WhileASkillPressWaits_AClickShoots_AndAMoveKeyMovesHer()
        {
            CommittedQuickShot();
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(quick.Recovery, Is.GreaterThan(_feel.BasicShotStartup + _feel.BasicShotActive + 0.05f), "room for a shot to go off");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            int shots = _player.BasicShotsFired;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float gate = pressedAt + quick.Startup + quick.Recovery;
            yield return Until(() => _player.FreeRecoverySlot == Quick, 2f);
            Assert.That(_player.PressSkill(Volley, aim), Is.EqualTo(IntentResult.Buffered));

            _player.Steer(Vector3.right);
            _player.Shoot(aim);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "the click shoots at once");
            Vector3 from = _player.Position;
            float fastest = 0f;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.time < gate - 0.01f && _player.Action != ActionKind.Skill && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                fastest = Mathf.Max(fastest, _player.Speed);
            }
            Assume.That(_player.Action, Is.Not.EqualTo(ActionKind.Skill), "measured before the skill started");
            Vector3 moved = Flat(_player.Position - from);
            Debug.Log($"[0004] while a skill press waited: {_player.BasicShotsFired - shots} shot(s), moved {moved.magnitude:0.00} m, fastest {fastest:0.00} m/s");
            Assert.That(_player.BasicShotsFired - shots, Is.EqualTo(1), "the shot went off while the press waited");
            Assert.That(Vector3.Dot(moved, Vector3.right), Is.GreaterThan(0.1f), "she moved the held way");
            Assert.That(_player.HasBufferedPress, Is.True, "the press still waits");
            yield return StartedAt(Volley, gate);
            _player.Stop();
        }

        /// <summary>
        /// Keys are rolled: the next skill's key goes down before the last one's comes up. While a Charge skill waits
        /// out a free recovery, the key of the skill before it going up must not count as its own release: it starts
        /// charging and keeps charging. Before 2026-11-19 any skill key going up marked the waiting press as released,
        /// and the Charge skill fired at once with no charge.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeRecovery_WhileAChargeSkillWaits_TheEarlierSkillsKeyGoingUp_DoesNotReleaseIt()
        {
            const int Long = 3;
            Assume.That(_feel.SkillRecoveryFree, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            SkillDefinition longShot = _player.Skills[Long];
            Assume.That(longShot.Input, Is.EqualTo(SkillInput.Charge));
            Assume.That(longShot.ChargeTime, Is.GreaterThan(0.3f), "room to see it still charging");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 aim = StandAt + Vector3.back * 6f;
            float pressedAt = Time.time;
            Assert.That(_player.PressSkill(Quick, aim), Is.EqualTo(IntentResult.Started));
            float gate = pressedAt + quick.Startup + quick.Recovery;
            yield return Until(() => _player.FreeRecoverySlot == Quick, 2f);
            int fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(Long, aim), Is.EqualTo(IntentResult.Buffered), "the charge skill waits");
            _player.ReleaseSkill(Quick); // the first key comes up while the second is held
            yield return StartedAt(Long, gate);
            // Held: well past its startup it is still the skill in use, still charging, and has not fired.
            yield return new WaitForSeconds(longShot.Startup + longShot.ChargeTime * 0.5f);
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(Long), "still held");
            Assert.That(_player.SkillCharge, Is.GreaterThan(0.2f), "and charging");
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired), "it has not fired");
            _player.ReleaseSkill(Long);
            yield return Until(() => _player.SkillShotsFired > fired, 2f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(fired + 1), "its own key going up fires it");
            yield return Until(() => !_player.IsLocked, 2f);
        }

        /// <summary>
        /// A skill pressed late in a dash starts when the dash ends. She did not run into it, the dash moved her: no
        /// slide out of it, whether or not a move key is held.
        /// </summary>
        [UnityTest] public IEnumerator Slide_ASkillBufferedDuringADash_DoesNotSlideOutOfIt_MoveKeyHeld() => SkillOutOfADash(Volley, held: true);
        [UnityTest] public IEnumerator Slide_ASkillBufferedDuringADash_DoesNotSlideOutOfIt_NoMoveKey() => SkillOutOfADash(Keg, held: false);

        IEnumerator SkillOutOfADash(int slot, bool held)
        {
            {
                _player.Teleport(RunFrom);
                yield return null;
                if (held)
                {
                    _player.Steer(Vector3.right);
                    yield return new WaitForSeconds(0.35f);
                    Assume.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.2f), "running into the dash");
                }
                Assert.That(_player.DashToward(_player.Position + Vector3.right * 10f), Is.EqualTo(IntentResult.Started));
                float late = Mathf.Max(0f, _feel.DashDuration - _feel.InputBuffer * 0.5f);
                if (late > 0f) yield return new WaitForSeconds(late);
                Assume.That(_player.IsDashing, Is.True, "the press lands during the dash");
                Assert.That(_player.PressSkill(slot, _player.Position + Vector3.right * 6f), Is.EqualTo(IntentResult.Buffered));
                yield return Until(() => _player.ActiveSkillSlot == slot, 1f);
                Assert.That(_player.ActiveSkillSlot, Is.EqualTo(slot), "the buffered skill started when the dash ended");
                string name = _player.Skills[slot].Name + (held ? ", move key held" : ", no move key");
                Debug.Log($"[0004] {name}: started out of a dash with velocity {_player.Velocity.magnitude:0.00} m/s, "
                        + $"sliding {_player.IsSkillSliding}, slide start share {_player.SlideStartShare:0.00}");
                Assert.That(_player.IsSkillSliding, Is.False, name + ": no slide out of a dash");
                Vector3 at = _player.Position;
                float fastest = 0f;
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                    fastest = Mathf.Max(fastest, _player.Speed);
                }
                Assert.That(fastest, Is.LessThan(0.1f), name + ": she stands in the skill");
                Assert.That(Flat(_player.Position - at).magnitude, Is.LessThan(0.05f), name + ": and does not move");
                _player.Stop();
            }
        }

        // ---------------- The slide ----------------

        struct Sample { public float T, Speed; public Vector3 Position, Velocity; }

        /// <summary>Runs along +X at full speed, presses a skill aimed ahead, and records every frame for a while.</summary>
        IEnumerator RunAndPress(int slot, Vector3 aimOffset, float seconds, List<Sample> samples, Action<Vector3, float> pressed,
                                Vector3? steerAfter = null, Action<float> beforeFrame = null)
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right);
            yield return new WaitForSeconds(0.35f);
            Assume.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.2f), "at run speed");
            Vector3 from = _player.Position;
            float t0 = Time.time;
            Assert.That(_player.PressSkill(slot, from + aimOffset), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(slot);
            if (steerAfter.HasValue) _player.Steer(steerAfter.Value);
            pressed(from, t0);
            float deadline = Time.realtimeSinceStartup + seconds + 3f;
            while (Time.time < t0 + seconds && Time.realtimeSinceStartup < deadline)
            {
                beforeFrame?.Invoke(Time.time - t0);
                yield return null;
                samples.Add(new Sample { T = Time.time - t0, Speed = _player.Speed, Position = _player.Position, Velocity = _player.Velocity });
            }
        }

        [UnityTest]
        public IEnumerator Slide_QuickShot_OnlyDipsHerSpeed_AndShesBackAtRunSpeedAfter()
        {
            CommittedQuickShot();
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(_feel.SkillSlowdown, Is.GreaterThan(quick.Startup), "a short skill: it ends before she could stop");
            var samples = new List<Sample>();
            Vector3 from = default;
            yield return RunAndPress(Quick, Vector3.right * 8f, 0.8f, samples, (p, t) => from = p);
            _player.Stop();

            float minSpeed = float.PositiveInfinity, backAt = float.NaN, maxDrop = 0f, worstDeviation = 0f, biggestStep = 0f;
            Vector3 atHit = default;
            float before = _feel.RunSpeed, beforeT = 0f;
            foreach (Sample s in samples)
            {
                float dt = s.T - beforeT;
                if (s.T <= quick.Startup)
                {
                    maxDrop = Mathf.Max(maxDrop, (before - s.Speed) / Mathf.Max(dt, 1e-4f));
                    biggestStep = Mathf.Max(biggestStep, before - s.Speed - SteepestFall() * dt);
                    worstDeviation = Mathf.Max(worstDeviation,
                        Mathf.Abs(s.Speed - _feel.RunSpeed * SkillSlide.SlideShare(_player.SlideStartShare, s.T, _feel.SkillSlowdown)));
                    atHit = s.Position;
                }
                minSpeed = Mathf.Min(minSpeed, s.Speed);
                if (float.IsNaN(backAt) && s.T > quick.Startup && s.Speed >= _feel.RunSpeed - 0.05f) backAt = s.T;
                before = s.Speed;
                beforeT = s.T;
            }
            float slid = Vector3.Dot(Flat(atHit - from), Vector3.right);
            float predicted = SkillSlide.SlideDistance(_player.SlideStartShare, quick.Startup, _feel.SkillSlowdown, _feel.RunSpeed);
            Debug.Log($"[0004] Quick Shot from {_player.SlideStartShare * _feel.RunSpeed:0.00} m/s: slowest {minSpeed:0.00} m/s, slid {slid:0.00} m "
                    + $"to its hit (curve {predicted:0.00}), back at run speed {backAt:0.000} s after the press, steepest fall {maxDrop:0.0} m/s per s");
            Assert.That(minSpeed, Is.GreaterThan(_feel.RunSpeed * 0.3f), "she never comes near a stand");
            Assert.That(worstDeviation, Is.LessThan(0.4f), "the speed follows the slide, frame by frame");
            Assert.That(biggestStep, Is.LessThan(0.2f), "no frame drops more than the curve's steepest fall allows (m/s beyond it)");
            Assert.That(slid, Is.EqualTo(predicted).Within(0.2f), "the distance the curve predicts");
            Assert.That(backAt, Is.Not.NaN, "back at run speed with the key held");
            Assert.That(backAt, Is.LessThan(quick.Startup + _feel.SkillSpeedRecovery + 0.1f));
        }

        /// <summary>
        /// Heavy Volley pressed at a run, with the move key turned to the side after the press. Changed deliberately on
        /// 2026-11-19 (0004 amended: the move keys steer the slide): with <see cref="FeelValues.SlideSteering"/> off it
        /// checks the old rule, that the key does not steer; on, that the slide turns the held way at once. Either way
        /// the speed follows the slide's curve frame by frame, she stands after the same time and the same distance,
        /// and she stands until the hit.
        /// </summary>
        [UnityTest] public IEnumerator Slide_HeavyVolley_StopsByTheSlowdown_AndStandsUntilItsHit_SteeringOff_TheKeysDoNotSteerIt() => VolleySlide(steering: false);
        [UnityTest] public IEnumerator Slide_HeavyVolley_SteeredToTheSide_TurnsAtOnce_AtTheCurvesSpeed_AndStandsAsBefore() => VolleySlide(steering: true);

        IEnumerator VolleySlide(bool steering)
        {
            if (!steering) OwnFeel(f => f.SlideSteering = false);
            Assume.That(_feel.SlideSteering, Is.EqualTo(steering));
            SkillDefinition volley = _player.Skills[Volley];
            Assume.That(volley.Startup, Is.GreaterThan(_feel.SkillSlowdown + 0.2f), "a long cast");
            var samples = new List<Sample>();
            Vector3 from = default;
            int hitsBefore = _player.SkillShotsFired;
            // The move key is turned to the side after the press: the slide follows it only with steering on.
            yield return RunAndPress(Volley, Vector3.right * 8f, volley.Startup + 0.02f, samples, (p, t) => from = p, steerAfter: Vector3.forward);
            Assert.That(_player.SkillShotsFired - hitsBefore, Is.EqualTo(1), "the cast fired");
            Vector3 way = steering ? Vector3.forward : Vector3.right;
            Vector3 across = steering ? Vector3.right : Vector3.forward;

            float stoppedAt = float.NaN, movedAfterStop = 0f, maxFall = 0f, worstSideways = 0f, biggestStep = 0f, worstDeviation = 0f;
            float before = _feel.RunSpeed, beforeT = 0f;
            Vector3 stopPos = default;
            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                if (s.T >= volley.Startup) break;
                float dt = Mathf.Max(s.T - beforeT, 1e-4f);
                maxFall = Mathf.Max(maxFall, (before - s.Speed) / dt);
                biggestStep = Mathf.Max(biggestStep, before - s.Speed - SteepestFall() * dt);
                worstDeviation = Mathf.Max(worstDeviation,
                    Mathf.Abs(s.Speed - _feel.RunSpeed * SkillSlide.SlideShare(_player.SlideStartShare, s.T, _feel.SkillSlowdown)));
                if (s.Velocity.sqrMagnitude > 0.01f && i >= (steering ? FirstTurnedSample : 0))
                {
                    worstSideways = Mathf.Max(worstSideways, Mathf.Abs(Vector3.Dot(s.Velocity.normalized, across)));
                    Assert.That(Vector3.Dot(s.Velocity, way), Is.GreaterThan(0f), $"sliding the {(steering ? "held" : "run")} way at {s.T:0.000} s");
                }
                // Standing counts from well under the limit checked afterwards: at very short frames (no window) the
                // speed wavers by a few mm/s from frame to frame, and a first sample just under 0.05 could be followed
                // by one just over it.
                if (float.IsNaN(stoppedAt) && s.Speed < 0.01f) { stoppedAt = s.T; stopPos = s.Position; }
                else if (!float.IsNaN(stoppedAt)) movedAfterStop = Mathf.Max(movedAfterStop, Flat(s.Position - stopPos).magnitude, s.Speed);
                before = s.Speed;
                beforeT = s.T;
            }
            Vector3 slidBy = Flat(stopPos - from);
            float slid = Vector3.Dot(slidBy, way);
            float predicted = SkillSlide.SlideDistance(_player.SlideStartShare, 10f, _feel.SkillSlowdown, _feel.RunSpeed);
            Debug.Log($"[0004] Heavy Volley from {_player.SlideStartShare * _feel.RunSpeed:0.00} m/s, steering {(steering ? "on, key to the side" : "off")}: "
                    + $"standing {stoppedAt:0.000} s after the press, slid {slid:0.00} m the {(steering ? "held" : "run")} way and {Vector3.Dot(slidBy, across):0.00} m across "
                    + $"(curve {predicted:0.00}), ends at {slidBy.x:+0.00;-0.00} m along the run and {slidBy.z:+0.00;-0.00} m to the side of the press, "
                    + $"speed off the curve by at most {worstDeviation:0.000} m/s, steepest fall {maxFall:0.0} m/s per s");
            Assert.That(stoppedAt, Is.Not.NaN, "she comes to a stand");
            Assert.That(stoppedAt, Is.EqualTo(_feel.SkillSlowdown * _player.SlideStartShare).Within(0.06f), "by about the slowdown");
            Assert.That(worstDeviation, Is.LessThan(0.4f), "the speed follows the slide, frame by frame");
            Assert.That(biggestStep, Is.LessThan(0.2f), "falling frame by frame, never more in a frame than the curve allows (m/s beyond it)");
            Assert.That(movedAfterStop, Is.LessThan(0.05f), "and stands until the hit");
            Assert.That(slid, Is.EqualTo(predicted).Within(0.15f), "the distance the curve predicts");
            if (steering)
            {
                Assert.That(Mathf.Abs(Vector3.Dot(slidBy, across)), Is.LessThan(0.15f), "nothing along the run after the press");
                Assert.That(worstSideways, Is.LessThan(0.05f), "the held way in every frame the agent reports it (FirstTurnedSample): steered at once");
            }
            else
            {
                Assert.That(Vector3.Dot(slidBy.normalized, Vector3.right), Is.GreaterThan(0.99f), "along the way she was running");
                Assert.That(worstSideways, Is.LessThan(0.05f), "the move key turned to the side did not steer the slide");
            }
            _player.Stop();
        }

        /// <summary>
        /// Quick Shot pressed at a run, the key turned straight back after the press: the slide turns round at the
        /// speed it has and does not brake her; the speed follows the curve to the hit. With the key to the side, the
        /// same to the side. (One skill per test: Quick Shot's cooldown is longer than a test.)
        /// </summary>
        [UnityTest] public IEnumerator Slide_QuickShot_SteeredBack_TurnsRoundAtTheSpeedItHas_TheOppositeKeyDoesNotBrake() => QuickSteered(Vector3.left);
        [UnityTest] public IEnumerator Slide_QuickShot_SteeredToTheSide_SlidesThatWay_AtTheCurvesSpeed() => QuickSteered(Vector3.forward);

        IEnumerator QuickSteered(Vector3 steer)
        {
            CommittedQuickShot();
            Assume.That(_feel.SlideSteering, Is.True);
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(_feel.SkillSlowdown, Is.GreaterThan(quick.Startup), "a short skill: it ends before she could stop");
            {
                var samples = new List<Sample>();
                Vector3 from = default;
                yield return RunAndPress(Quick, Vector3.right * 8f, quick.Startup, samples, (p, t) => from = p, steerAfter: steer);
                float worstDeviation = 0f, worstOffWay = 0f;
                Vector3 atHit = from;
                for (int i = 0; i < samples.Count; i++)
                {
                    Sample s = samples[i];
                    if (s.T > quick.Startup) break;
                    worstDeviation = Mathf.Max(worstDeviation,
                        Mathf.Abs(s.Speed - _feel.RunSpeed * SkillSlide.SlideShare(_player.SlideStartShare, s.T, _feel.SkillSlowdown)));
                    if (s.Velocity.sqrMagnitude > 0.01f && i >= FirstTurnedSample) worstOffWay = Mathf.Max(worstOffWay, 1f - Vector3.Dot(s.Velocity.normalized, steer));
                    atHit = s.Position;
                }
                _player.Stop();
                Vector3 slidBy = Flat(atHit - from);
                float predicted = SkillSlide.SlideDistance(_player.SlideStartShare, quick.Startup, _feel.SkillSlowdown, _feel.RunSpeed);
                string name = steer == Vector3.left ? "key turned back" : "key to the side";
                Debug.Log($"[0004] Quick Shot from {_player.SlideStartShare * _feel.RunSpeed:0.00} m/s, {name}: at its hit {slidBy.x:+0.00;-0.00} m along the run "
                        + $"and {slidBy.z:+0.00;-0.00} m to the side of the press (unsteered: {predicted:+0.00} m along the run), "
                        + $"speed off the curve by at most {worstDeviation:0.000} m/s");
                Assert.That(worstDeviation, Is.LessThan(0.4f), name + ": the speed follows the slide, frame by frame");
                Assert.That(worstOffWay, Is.LessThan(0.01f), name + ": the held way in every frame the agent reports it (FirstTurnedSample)");
                Assert.That(Vector3.Dot(slidBy, steer), Is.EqualTo(predicted).Within(0.2f), name + ": the curve's distance, the held way");
                yield return Until(() => _player.Action == ActionKind.None, 2f);
            }
        }

        /// <summary>A steered slide whose key is let go keeps the way it last slid, at the curve's speed, to a stand.</summary>
        [UnityTest]
        public IEnumerator Slide_Steered_ThenTheKeyLetGo_KeepsItsWay()
        {
            Assume.That(_feel.SlideSteering, Is.True);
            SkillDefinition volley = _player.Skills[Volley];
            Assume.That(volley.Startup, Is.GreaterThan(_feel.SkillSlowdown + 0.2f), "a long cast");
            const float LetGoAt = 0.08f;
            Assume.That(LetGoAt, Is.LessThan(_feel.SkillSlowdown * 0.5f), "let go well before the stand");
            var samples = new List<Sample>();
            Vector3 from = default;
            yield return RunAndPress(Volley, Vector3.right * 8f, _feel.SkillSlowdown + 0.15f, samples, (p, t) => from = p, steerAfter: Vector3.forward,
                                     beforeFrame: t => { if (t >= LetGoAt) _player.Steer(Vector3.zero); });
            float worstOffWay = 0f, worstDeviation = 0f, stoppedAt = float.NaN;
            int afterLetGo = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                worstDeviation = Mathf.Max(worstDeviation,
                    Mathf.Abs(s.Speed - _feel.RunSpeed * SkillSlide.SlideShare(_player.SlideStartShare, s.T, _feel.SkillSlowdown)));
                if (s.Velocity.sqrMagnitude > 0.01f && i >= FirstTurnedSample)
                {
                    worstOffWay = Mathf.Max(worstOffWay, 1f - Vector3.Dot(s.Velocity.normalized, Vector3.forward));
                    if (s.T > LetGoAt) afterLetGo++;
                }
                if (float.IsNaN(stoppedAt) && s.Speed < 0.01f) stoppedAt = s.T;
            }
            Vector3 slidBy = Flat(_player.Position - from);
            Debug.Log($"[0004] Heavy Volley steered to the side, key let go at {LetGoAt} s: {afterLetGo} sliding frames after it, "
                    + $"standing {stoppedAt:0.000} s after the press, {slidBy.x:+0.00;-0.00} m along the run and {slidBy.z:+0.00;-0.00} m to the side");
            Assert.That(afterLetGo, Is.GreaterThan(0), "she still slid after the key was let go");
            Assert.That(worstOffWay, Is.LessThan(0.01f), "the way she last slid, to the stand");
            Assert.That(worstDeviation, Is.LessThan(0.4f), "at the curve's speed");
            Assert.That(stoppedAt, Is.EqualTo(_feel.SkillSlowdown * _player.SlideStartShare).Within(0.06f), "standing after the same time");
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator Slide_ASlowdownOfZero_StopsHerDead()
        {
            CommittedQuickShot();
            OwnFeel(f => f.SkillSlowdown = 0f);
            var samples = new List<Sample>();
            yield return RunAndPress(Quick, Vector3.right * 8f, 0.1f, samples, (p, t) => { });
            _player.Stop();
            Assert.That(samples.Count, Is.GreaterThan(0));
            Assert.That(samples[0].Speed, Is.LessThan(0.2f), "standing from the first frame, as before decision 0004");
        }

        [UnityTest]
        public IEnumerator Slide_Backstep_DoesNotSlide_ItMovesItsOwnDistance()
        {
            SkillDefinition step = _player.Skills[Backstep];
            Assume.That(step.MoveDistance, Is.LessThan(-1f));
            var samples = new List<Sample>();
            Vector3 from = default;
            yield return RunAndPress(Backstep, Vector3.right * 8f, step.Startup + 0.02f, samples, (p, t) => from = p);
            _player.Stop();
            Vector3 moved = Flat(_player.Position - from);
            Assert.That(_player.IsSkillSliding, Is.False);
            Assert.That(moved.magnitude, Is.EqualTo(-step.MoveDistance).Within(0.3f), "its move distance, no slide on top");
            Assert.That(Vector3.Dot(moved.normalized, Vector3.left), Is.GreaterThan(0.98f), "straight away from the aim");
        }

        /// <summary>A shot straight after a skill, ahead or behind: no jump in speed beyond what one frame of easing gives.</summary>
        [UnityTest]
        public IEnumerator Slide_AShotAfterASkill_TheSpeedEasesWithoutAJump_AheadAndBehind()
        {
            float maxRate = 0f;
            foreach (var (slot, shotAim, name) in new[] { (Quick, Vector3.right * 8f, "Quick Shot, shot ahead"), (Triple, Vector3.left * 8f, "Triple Tap, shot behind") })
            {
                var samples = new List<Sample>();
                Vector3 from = default;
                float t0 = 0f;
                yield return RunAndPress(slot, Vector3.right * 8f, 0.05f, samples, (p, t) => { from = p; t0 = t; });
                _player.Shoot(_player.Position + shotAim); // waits for the skill's last hit, then goes on the move
                float before = _player.Speed, beforeT = Time.time, worst = 0f;
                int shotsBefore = _player.BasicShotsFired;
                float deadline = Time.realtimeSinceStartup + 4f;
                while (Time.time < t0 + 1.4f && Time.realtimeSinceStartup < deadline)
                {
                    _player.AimAt(_player.Position + shotAim);
                    yield return null;
                    float dt = Mathf.Max(Time.time - beforeT, 1e-4f);
                    float rate = Mathf.Abs(_player.Speed - before) / dt;
                    worst = Mathf.Max(worst, rate);
                    // The steepest of the curves: the slide, the ease back up, a shot on the move and the backpedal cap.
                    float allowed = 1.6f * _feel.RunSpeed * Mathf.Max(1f / _feel.SkillSlowdown, 1f / _feel.SkillSpeedRecovery,
                                                                   0.5f / _feel.MovingShotSlowdown, 0.5f / _feel.MovingShotRecovery);
                    Assert.That(Mathf.Abs(_player.Speed - before), Is.LessThan(allowed * dt + 0.15f),
                                $"{name}: no jump at {Time.time - t0:0.000} s ({before:0.00} to {_player.Speed:0.00} m/s)");
                    before = _player.Speed;
                    beforeT = Time.time;
                }
                Assert.That(_player.BasicShotsFired, Is.GreaterThan(shotsBefore), name + ": the shot went off");
                maxRate = Mathf.Max(maxRate, worst);
                Debug.Log($"[0004] {name}: steepest change in speed {worst:0.0} m/s per s; speed at the end {_player.Speed:0.00} m/s");
                _player.Stop();
                yield return null;
            }
        }

        /// <summary>
        /// The first sample whose velocity shows a turn made at the press. The character turns in the first frame after
        /// it, as free movement does (her position moves the new way in that frame), but the NavMesh agent reports the
        /// velocity it had before for that one frame: measured 2026-11-19, identical for free movement and the slide.
        /// </summary>
        const int FirstTurnedSample = 1;

        /// <summary>The steepest fall of the slide from full speed, m/s per s: a smoothstep is steepest half way, at 1.5 times the average.</summary>
        float SteepestFall() => 1.5f * _feel.RunSpeed / _feel.SkillSlowdown;

        // ---------------- The pose ----------------

        [UnityTest]
        public IEnumerator Pose_SlidingInASkill_ShowsTheShootingGait_ThenTheShootStanceWhenStanding()
        {
            Assume.That(_view != null, Is.True, "a rigged model");
            // Aimed ahead (Scatter Blast): the gait forward. Aimed behind (Powder Keg, which then stands for the rest of
            // its cast): the gait backward, then the Shoot stance.
            var poses = new List<(float t, float speed, CharacterPose pose, float weight)>();
            foreach (var (slot, aim, gait) in new[] { (Scatter, Vector3.right * 4f, CharacterPose.ShootMove), (Keg, Vector3.left * 5f, CharacterPose.ShootBack) })
            {
                poses.Clear();
                _player.Teleport(RunFrom);
                yield return null;
                _player.Steer(Vector3.right);
                yield return new WaitForSeconds(0.35f);
                float t0 = Time.time;
                Assert.That(_player.PressSkill(slot, _player.Position + aim), Is.EqualTo(IntentResult.Started));
                SkillDefinition skill = _player.Skills[slot];
                float deadline = Time.realtimeSinceStartup + 2f;
                while (_player.ActiveSkillSlot == slot && _player.SkillPhase == SkillPhase.Startup && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    poses.Add((Time.time - t0, _player.Speed, _view.Pose, _view.AimWeight));
                }
                _player.Stop();
                bool sawGait = false, wrong = false, standingShoot = true;
                foreach (var p in poses)
                {
                    if (p.speed > PoseRule.MoveThreshold && p.t > 0.05f)
                    {
                        if (p.pose == gait) sawGait = true; else wrong = true;
                        if (p.t > _view.AimBlend + 0.05f) Assert.That(p.weight, Is.EqualTo(1f), skill.Name + ": the arms aim while she slides");
                    }
                    if (p.speed <= PoseRule.MoveThreshold && p.t > 0.1f && p.pose != CharacterPose.Shoot) standingShoot = false;
                }
                Assert.That(sawGait, Is.True, $"{skill.Name}: the {gait} gait while sliding");
                Assert.That(wrong, Is.False, $"{skill.Name}: and no other pose while sliding");
                Assert.That(standingShoot, Is.True, $"{skill.Name}: the Shoot stance once standing");
                yield return Until(() => _player.Action == ActionKind.None, 2f);
            }
        }

        /// <summary>
        /// The gait follows the way she actually slides, frame by frame (CharacterAnimation reads her velocity against
        /// the body's facing): a slide aimed ahead and steered back behind her shows the backward shooting gait.
        /// </summary>
        [UnityTest]
        public IEnumerator Pose_ASlideSteeredBehindHer_ShowsTheBackwardGait()
        {
            Assume.That(_view != null, Is.True, "a rigged model");
            Assume.That(_feel.SlideSteering, Is.True);
            var poses = new List<(float t, float speed, CharacterPose pose)>();
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right);
            yield return new WaitForSeconds(0.35f);
            float t0 = Time.time;
            Assert.That(_player.PressSkill(Volley, _player.Position + Vector3.right * 8f), Is.EqualTo(IntentResult.Started)); // she faces ahead
            _player.Steer(Vector3.left);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_player.IsSkillSliding && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                poses.Add((Time.time - t0, _player.Speed, _view.Pose));
            }
            _player.Stop();
            bool sawBack = false, wrong = false;
            foreach (var p in poses)
            {
                if (p.speed <= PoseRule.MoveThreshold || p.t <= 0.05f) continue;
                if (p.pose == CharacterPose.ShootBack) sawBack = true; else wrong = true;
            }
            Assert.That(sawBack, Is.True, "the backward shooting gait while she slides behind her");
            Assert.That(wrong, Is.False, "and no other pose while sliding");
        }

        // ---------------- The arms held up ----------------

        [UnityTest]
        public IEnumerator ArmHold_ClicksSlowerThanTheFireRate_KeepTheArmsUpAndTheShootStance_ThenTheyComeDown()
        {
            Assume.That(_view != null, Is.True, "a rigged model");
            float cycle = _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;
            const float Every = 0.3f;
            Assume.That(Every, Is.GreaterThan(cycle), "slower than the fire rate");
            Assume.That(Every - cycle, Is.LessThan(_feel.AimHold), "the gap is shorter than the hold");
            _player.Teleport(StandAt);
            yield return null;
            yield return null;
            Vector3 aim = _player.Position + Flat(_player.transform.forward) * 5f;
            float start = Time.time, lowest = 1f, maxDt = 0f;
            int clicks = 0;
            bool up = false, shoot = false, leftShoot = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.time < start + 2f && Time.realtimeSinceStartup < deadline)
            {
                _player.AimAt(aim);
                if (Time.time >= start + clicks * Every) { _player.Shoot(aim); clicks++; }
                yield return null;
                maxDt = Mathf.Max(maxDt, Time.deltaTime);
                if (_view.AimWeight >= 1f) up = true;
                if (up) lowest = Mathf.Min(lowest, _view.AimWeight);
                if (_view.Pose == CharacterPose.Shoot) shoot = true;
                else if (shoot) leftShoot = true;
            }
            Assert.That(clicks, Is.GreaterThanOrEqualTo(6));
            Assert.That(up, Is.True, "the arms came up");
            Assert.That(lowest, Is.EqualTo(1f), "and never came down between the shots");
            Assert.That(leftShoot, Is.False, "the body stayed in the Shoot stance");

            yield return Until(() => _player.Action == ActionKind.None, 1f);
            float endedAt = Time.time;
            yield return null; // the view may have read the character before it ended the shot in that frame
            Assert.That(_view.ArmsHeld, Is.True, "held after the last shot");
            yield return Until(() => _view.AimWeight <= 0f, 2f);
            float downAfter = Time.time - endedAt;
            Debug.Log($"[0004] arm hold: lowest weight {lowest:0.00} over {clicks} clicks; down {downAfter:0.000} s after the last shot ended "
                    + $"(hold {_feel.AimHold}, blend {_view.AimBlend}); pose then {_view.Pose}");
            Assert.That(downAfter, Is.GreaterThanOrEqualTo(_feel.AimHold - maxDt), "held for the hold");
            Assert.That(downAfter, Is.LessThanOrEqualTo(_feel.AimHold + _view.AimBlend + 2f * maxDt + 0.01f), "and down after it and the blend");
            yield return new WaitForSeconds(0.2f);
            Assert.That(_view.Pose, Is.EqualTo(CharacterPose.Idle), "the body goes back to Idle");
        }

        [UnityTest]
        public IEnumerator ArmHold_EndsWhenTheCursorLeavesTheFront_OrSheDashes()
        {
            Assume.That(_view != null, Is.True, "a rigged model");
            _player.Teleport(StandAt);
            yield return null;
            Vector3 ahead = _player.Position + Flat(_player.transform.forward) * 5f;
            _player.AimAt(ahead);
            _player.Shoot(ahead);
            yield return Until(() => _player.Action == ActionKind.None, 1f);
            yield return null; // the view may have read the character before it ended the shot in that frame
            Assert.That(_view.ArmsHeld, Is.True);
            _player.AimAt(_player.Position - Flat(_player.transform.forward) * 5f); // straight behind: out of the front angle
            yield return null;
            yield return null;
            Assert.That(_view.ArmsHeld, Is.False, "the cursor behind her ends the hold");

            _player.AimAt(ahead);
            _player.Shoot(ahead);
            yield return Until(() => _player.Action == ActionKind.None, 1f);
            yield return null; // the view may have read the character before it ended the shot in that frame
            Assert.That(_view.ArmsHeld, Is.True);
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 6f), Is.EqualTo(IntentResult.Started));
            yield return null;
            Assert.That(_view.ArmsHeld, Is.False, "a dash ends the hold");
        }

        // ---------------- The dash direction ----------------

        [UnityTest]
        public IEnumerator Dash_TowardMovement_GoesTheHeldWay_NotToTheCursor()
        {
            OwnBindings(DashDirection.TowardMovement);
            UseFakeInput();
            _player.Teleport(new Vector3(0f, 0f, -4f));
            yield return null;
            yield return null;
            Vector3 up = _driver.ScreenToGround(new Vector2(0f, 1f));
            Vector2 cursor = ScreenOf(_player.Position + Vector3.left * 6f);
            _idle = new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f) };
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f), DashPressed = true });
            yield return Until(() => _player.IsDashing, 1f);
            Vector3 dashFrom = _player.Position;
            yield return Until(() => !_player.IsDashing, 1f);
            _idle = new InputFrame { CursorScreen = cursor };
            Vector3 dashed = Flat(_player.Position - dashFrom);
            Assert.That(Vector3.Dot(dashed.normalized, up), Is.GreaterThan(0.95f), "up the screen, the way W moves her");
            Assert.That(dashed.magnitude, Is.GreaterThan(_feel.DashDistance * 0.7f));
            StringAssert.Contains("dash the way you move", _driver.ActiveBindings.HelpLine());
        }

        [UnityTest]
        public IEnumerator Dash_TowardMovement_WithNoKeyHeld_GoesTheWaySheFaces()
        {
            OwnBindings(DashDirection.TowardMovement);
            UseFakeInput();
            _player.Teleport(new Vector3(0f, 0f, -4f));
            yield return null;
            Vector3 right = _driver.ScreenToGround(new Vector2(1f, 0f));
            Vector2 cursor = ScreenOf(_player.Position + Vector3.back * 5f);
            // A tap of D turns her to the right of the screen; then nothing is held.
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, Move = new Vector2(1f, 0f) });
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, Move = new Vector2(1f, 0f) });
            _idle = new InputFrame { CursorScreen = cursor };
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Vector3 facing = Flat(_player.transform.forward).normalized;
            Assume.That(Vector3.Dot(facing, right), Is.GreaterThan(0.95f), "she faces the right of the screen");
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, DashPressed = true });
            yield return Until(() => _player.IsDashing, 1f);
            Vector3 dashFrom = _player.Position;
            yield return Until(() => !_player.IsDashing, 1f);
            Vector3 dashed = Flat(_player.Position - dashFrom);
            Assert.That(Vector3.Dot(dashed.normalized, facing), Is.GreaterThan(0.95f), "the way she faces, not toward the cursor");
        }

        [UnityTest]
        public IEnumerator Dash_TowardCursor_GoesToTheCursor_WhateverKeyIsHeld()
        {
            OwnBindings(DashDirection.TowardCursor);
            UseFakeInput();
            _player.Teleport(new Vector3(0f, 0f, -4f));
            yield return null;
            yield return null;
            Vector2 cursor = ScreenOf(_player.Position + Vector3.left * 6f);
            _idle = new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f) };
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f), DashPressed = true });
            yield return Until(() => _player.IsDashing, 1f);
            Vector3 dashFrom = _player.Position;
            yield return Until(() => !_player.IsDashing, 1f);
            _idle = new InputFrame { CursorScreen = cursor };
            Vector3 dashed = Flat(_player.Position - dashFrom);
            Assert.That(Vector3.Dot(dashed.normalized, Vector3.left), Is.GreaterThan(0.95f), "toward the cursor");
            StringAssert.Contains("dash to the cursor", _driver.ActiveBindings.HelpLine());
        }

        // ---------------- Helpers ----------------

        /// <summary>Gives the player a copy of the feel settings with a change, so the asset itself is never touched.</summary>
        void OwnFeel(Action<FeelValues> change)
        {
            FeelSettings copy = Object.Instantiate(_player.Settings);
            _copies.Add(copy);
            change(copy.Values);
            _player.Settings = copy;
            _feel = copy.Values;
        }

        /// <summary>
        /// Decision 0005 (2026-11-19) made Quick Shot light: it no longer stops or slides her, and its recovery is 0.1 s.
        /// The checks of decision 0004 that used it as the short committed skill keep it so: Quick Shot as it was before,
        /// committed with a 0.3 s recovery, on a copy of the skill settings, so the asset itself is never touched.
        /// </summary>
        void CommittedQuickShot()
        {
            SkillSettings copy = Object.Instantiate(_player.SkillSet);
            _copies.Add(copy);
            copy.Skills[Quick].Lock = SkillLock.Committed;
            copy.Skills[Quick].Recovery = 0.3f;
            _player.SkillSet = copy;
        }

        /// <summary>Gives the driver a copy of the bindings with this dash direction.</summary>
        void OwnBindings(DashDirection direction)
        {
            InputBindings copy = Object.Instantiate(_driver.ActiveBindings);
            _copies.Add(copy);
            copy.DashDirection = direction;
            _driver.Bindings = copy;
        }

        void UseFakeInput() => _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;

        static Vector2 ScreenOf(Vector3 world) => Camera.main.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
