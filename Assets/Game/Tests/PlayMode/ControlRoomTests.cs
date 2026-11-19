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
    /// Loads the built ControlRoom scene. The character is driven through its intent API, or by feeding
    /// the input driver frames; never by OS input. Values come from the settings asset in the scene,
    /// so the tests still hold after the feel values are tuned.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3), Dummy_C at (0, 10).
    /// </summary>
    public class ControlRoomTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);  // A is 2.3 m away here, B 5.8 m
        static readonly Vector3 NearB = new Vector3(-2f, 0f, 1f); // B is 2.3 m away here, A 5.8 m

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        FeelValues _feel;
        Damageable _a;
        Damageable _b;
        Damageable _c;

        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

        float ShotSeconds => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Assert.That(_driver, Is.Not.Null, "PlayerInputDriver in scene");
            Assert.That(_player.Settings, Is.Not.Null, "the player uses the settings asset");
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();

            // The real mouse and keyboard must not steer a test.
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null;
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- C2 Move ----------------

        [UnityTest]
        public IEnumerator Steer_MovesAtRunSpeed_InTheHeldDirection_AndStopsWhenLetGo()
        {
            var target = new Vector3(-6f, 0f, -5f);
            float distance = FlatDistance(_player.Position, target);
            float start = Time.time;

            yield return SteerTo(target, 0.15f, 10f);

            float elapsed = Time.time - start;
            float expected = distance / _feel.RunSpeed;
            Assert.That(FlatDistance(_player.Position, target), Is.LessThan(0.3f), $"got there (at {_player.Position})");
            Assert.That(elapsed, Is.InRange(expected - 0.2f, expected + 0.5f + _feel.TimeToFullSpeed),
                        $"took {elapsed:0.00}s for {distance:0.00}m");
            Vector3 stoppedAt = _player.Position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(FlatDistance(_player.Position, stoppedAt), Is.LessThan(0.05f), "let go, she stands");
            Assert.That(_player.IsMoving, Is.False);
        }

        // ---------------- C3 Dash ----------------

        [UnityTest]
        public IEnumerator Dash_CoversDashDistance_ThenIsRefusedOnCooldown()
        {
            Assume.That(_feel.DashCooldown, Is.GreaterThan(_feel.DashDuration + 0.5f));
            Vector3 from = _player.Position;

            Assert.That(_player.DashToward(from + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsDashing, Is.True);
            yield return new WaitForSeconds(_feel.DashDuration + 0.1f);

            Assert.That(_player.IsDashing, Is.False);
            Assert.That(FlatDistance(_player.Position, from), Is.EqualTo(_feel.DashDistance).Within(0.3f));
            Assert.That(_player.DashToward(from), Is.EqualTo(IntentResult.Rejected), "second dash blocked by cooldown");
            Assert.That(_player.DashCooldownRemaining, Is.GreaterThan(0f));
        }

        // ---------------- Placeholder skill ----------------

        [UnityTest]
        public IEnumerator Skill_FacesTheAimPoint_AndDamagesTheDummyInLine()
        {
            int before = _a.CurrentHealth;
            Vector3 aim = _a.transform.position;

            Assert.That(_player.CastAt(aim), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
            // A committed skill turns her to the aim point. The first skill, Quick Shot, is light since decision 0005
            // (2026-11-19): it aims at the point and turns the body only as a basic shot would (BackpedalTests).
            Assert.That(Flat(_player.AimPoint - aim).magnitude, Is.LessThan(0.01f), "aimed at the point");
            if (_feel.FaceCursorOnCast && _player.Skills[0].Lock == SkillLock.Committed)
            {
                Vector3 want = Flat(aim - _player.Position).normalized;
                Assert.That(Vector3.Dot(_player.transform.forward, want), Is.GreaterThan(0.99f), "faces the aim point");
            }

            yield return Until(() => _player.Action == ActionKind.None, 5f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(1));
            Assert.That(_player.SkillShotHits, Is.EqualTo(1));
            Assert.That(before - _a.CurrentHealth, Is.EqualTo(_player.Skills[0].Damage));
            Assert.That(_b.HitsTaken, Is.EqualTo(0), "the dummy off the line is untouched");
        }

        // ---------------- Training dummy ----------------

        [UnityTest]
        public IEnumerator Dummy_FlashesWhenHit_AndRefillsAfterItDies()
        {
            var dummy = _a.GetComponent<TrainingDummy>();
            _a.TakeHit(10);
            yield return null;
            Assert.That(dummy.IsFlashing, Is.True);
            yield return new WaitForSeconds(0.3f);
            Assert.That(dummy.IsFlashing, Is.False);

            _a.TakeHit(_a.MaxHealth);
            Assert.That(_a.IsAlive, Is.False);
            yield return new WaitForSeconds(Placeholders.DummyRefillDelay + 0.3f);
            Assert.That(_a.CurrentHealth, Is.EqualTo(_a.MaxHealth), "refilled");
        }

        // ---------------- C6 The basic shot never locks, as in League ----------------

        [UnityTest]
        public IEnumerator AShotCancelledInItsStartup_DoesNotDelayTheNextOne()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 2f);
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup));

            // A dash cancels a shot in its startup (movement no longer does).
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 2f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 1f);
            yield return null;
            Assert.That(_player.BasicShotsFired, Is.EqualTo(0), "the cancelled shot never went off");
            float orderedAt = Time.time;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);

            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            Assert.That(Time.time - orderedAt, Is.LessThan(_feel.BasicShotStartup + 0.1f),
                        "the new shot starts at once: nothing went off, so there is nothing to wait for");
        }

        [UnityTest]
        public IEnumerator AMoveKeyInAShotsRecovery_MovesAtOnce()
        {
            Assume.That(_feel.BasicShotRecovery, Is.GreaterThan(0.08f), "a recovery long enough to leave during");
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_player.IsInShotRecovery, Is.True, "the shot has gone off and its recovery is running");
            Assert.That(_player.IsLocked, Is.False, "which does not lock");
            Vector3 from = _player.Position;
            float orderedAt = Time.time;

            _player.Steer(Vector3.back);
            yield return Until(() => FlatDistance(_player.Position, from) > 0.15f, 2f);
            _player.Stop();
            Assert.That(FlatDistance(_player.Position, from), Is.GreaterThan(0.15f));
            Assert.That(Time.time - orderedAt, Is.LessThan(_feel.BasicShotRecovery * 0.8f), "it left during what would have been the recovery");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
        }

        [UnityTest]
        public IEnumerator CuttingTheRecoveryShort_DoesNotRaiseTheFireRate()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            float firstHit = Time.time;

            // Step away and order the attack again every frame: the fastest a player could try to shoot.
            // (A command during the shot's cycle waits for the next shot; repeating it every frame is what a
            // player hammering the button would do, and still gives one shot per cycle.)
            _player.Steer(Vector3.back);
            yield return null;
            yield return Until(() =>
            {
                if (_a.HitsTaken < 2) _player.Shoot(_a.transform.position);
                return _a.HitsTaken >= 2;
            }, 3f);
            _player.Stop();

            Assert.That(_a.HitsTaken, Is.EqualTo(2));
            Assert.That(Time.time - firstHit, Is.GreaterThanOrEqualTo(ShotSeconds - 0.03f),
                        "the second shot lands a whole shot time after the first, not sooner");
        }

        [UnityTest]
        public IEnumerator SkillPressedInAShotsRecovery_StartsAtOnce()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assume.That(_player.IsInShotRecovery, Is.True);

            Assert.That(_player.PressSkill(0, _a.transform.position), Is.EqualTo(IntentResult.Started), "not buffered");
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SkillPressedInAShotsStartup_CancelsTheShot_AndStartsAtOnce()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 2f);
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup));

            Assert.That(_player.PressSkill(0, _a.transform.position), Is.EqualTo(IntentResult.Started), "not buffered");
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(0));
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(0), "the basic shot never went off");
            Assert.That(Lost(_a), Is.EqualTo(_player.Skills[0].Damage), "what landed is the skill");
        }

        // ---------------- C7 Dash-cancel ----------------

        [UnityTest]
        public IEnumerator Dash_CancelsACastBeforeItHits()
        {
            Assume.That(_feel.DashCancelsSkill, Is.True);
            int before = _a.CurrentHealth;
            Vector3 from = _player.Position;

            Assert.That(_player.CastAt(_a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return null;
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "the frame was short enough to still be in startup");

            Assert.That(_player.DashToward(from + Vector3.left * 10f), Is.EqualTo(IntentResult.Started), "the dash starts at once");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
            Assert.That(_player.IsDashing, Is.True);

            yield return new WaitForSeconds(_feel.DashDuration + ShotSeconds + 0.2f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(0), "the cancelled cast never fires");
            Assert.That(_a.CurrentHealth, Is.EqualTo(before));
            Assume.That(_player.Skills[0].Cooldown, Is.GreaterThan(_feel.DashDuration + ShotSeconds + 0.5f));
            Assert.That(_player.CastAt(_a.transform.position), Is.EqualTo(IntentResult.Rejected),
                        "the cancelled skill is still on cooldown");
            Assert.That(FlatDistance(_player.Position, from), Is.EqualTo(_feel.DashDistance).Within(0.3f));
        }

        // ---------------- C8 Input buffer ----------------

        [UnityTest]
        public IEnumerator Buffer_SkillPressedLateInADash_FiresWhenTheDashEnds()
        {
            Assume.That(_feel.InputBuffer, Is.GreaterThan(0.04f));
            float late = Mathf.Max(0f, _feel.DashDuration - _feel.InputBuffer * 0.5f);

            Assert.That(_player.DashToward(_player.Position + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));
            if (late > 0f) yield return new WaitForSeconds(late);
            Assume.That(_player.IsDashing, Is.True, "the press lands during the dash");

            Assert.That(_player.CastAt(_a.transform.position), Is.EqualTo(IntentResult.Buffered));
            Assert.That(_player.HasBufferedPress, Is.True);
            yield return Until(() => _player.SkillShotsFired >= 1, 3f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(1), "the buffered skill fires after the dash");
        }

        [UnityTest]
        public IEnumerator Buffer_SkillPressedEarlierThanTheBufferTime_IsDropped()
        {
            Assume.That(_feel.InputBuffer, Is.LessThan(_feel.DashDuration - 0.03f), "needs a dash longer than the buffer");

            Assert.That(_player.DashToward(_player.Position + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.CastAt(_a.transform.position), Is.EqualTo(IntentResult.Buffered));

            yield return new WaitForSeconds(_feel.DashDuration + ShotSeconds + 0.3f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(0), "a press older than the buffer time is dropped");
            Assert.That(_player.HasBufferedPress, Is.False);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
        }

        // ---------------- C9 Hit-stop ----------------

        [UnityTest]
        public IEnumerator HitStop_FreezesTimeAtTheHit_ThenReleases()
        {
            Assume.That(_feel.BasicShotHitStop, Is.GreaterThan(0f), "a basic shot hit has a hit stop of its own");
            _player.Teleport(NearA);
            yield return null;

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "frozen on the frame of the hit");
            Assert.That(HitStop.IsActive, Is.True);

            yield return new WaitForSecondsRealtime(_feel.BasicShotHitStop + 0.1f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time runs again");
            Assert.That(HitStop.IsActive, Is.False);
        }

        // ---------------- C10 Input driver, fed frames ----------------

        [UnityTest]
        public IEnumerator Input_LeftClicksInRhythm_EveryClickIsAShot()
        {
            UseFakeInput();
            _player.Teleport(NearA);
            yield return null;
            yield return null;
            _idle = new InputFrame { CursorScreen = ScreenOf(_a.transform.position) };
            var click = new InputFrame { CursorScreen = ScreenOf(_a.transform.position), ShootPressed = true };

            // Three left clicks on the dummy, one every 0.6 shot times: faster than she can shoot.
            float start = Time.time;
            int given = 0;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_a.HitsTaken < 3 && Time.realtimeSinceStartup < deadline)
            {
                if (given < 3 && Time.time >= start + given * ShotSeconds * 0.6f) { _frames.Enqueue(click); given++; }
                yield return null;
            }
            Assert.That(_a.HitsTaken, Is.EqualTo(3), "three clicks, three shots: a click during a shot's cycle is not lost");
            yield return new WaitForSeconds(ShotSeconds * 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(3), "and no more");
        }

        [UnityTest]
        public IEnumerator Input_SpaceDashesTowardTheCursor_AndQCastsAtIt()
        {
            // The dash direction is a setting since decision 0004: test the cursor one whatever the asset says.
            InputBindings bindings = Object.Instantiate(_driver.ActiveBindings);
            bindings.DashDirection = DashDirection.TowardCursor;
            _driver.Bindings = bindings;
            UseFakeInput();
            Vector3 start = _player.Position;
            _idle = new InputFrame { CursorScreen = ScreenOf(start + Vector3.left * 5f) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, DashPressed = true });
            Assert.That(_player.DashesStarted, Is.EqualTo(1));
            yield return new WaitForSeconds(_feel.DashDuration + 0.1f);
            Vector3 moved = Flat(_player.Position - start);
            Assert.That(Vector3.Dot(moved.normalized, Vector3.left), Is.GreaterThan(0.98f), "dashes toward the cursor");

            yield return null; // let the camera settle on the new position
            Vector2 onA = ScreenOf(_a.transform.position);
            yield return Feed(new InputFrame { CursorScreen = onA, SkillsPressed = 1 });
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
            yield return Until(() => _player.Action == ActionKind.None, 5f);
            Assert.That(_player.SkillShotHits, Is.EqualTo(1), "the cast went to the cursor, where Dummy_A stands");
        }

        // ---------------- The rigged model ----------------
        // These run only when the scene was built with a rigged player model.

        [UnityTest]
        public IEnumerator Animation_ShowsIdle_Run_Shoot_AndDash()
        {
            CharacterAnimation view = RiggedView();
            yield return null;
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Idle));
            Assert.That(InState(view, CharacterAnimation.IdleState), Is.True, "the Animator is in Idle");

            _player.Steer(new Vector3(-6f, 0f, -5f) - _player.Position);
            yield return Until(() => view.Pose == CharacterPose.Run, 1f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Run), "moving shows Run");
            yield return new WaitForSeconds(0.3f);
            Assert.That(InState(view, CharacterAnimation.RunState), Is.True, "the Animator is in Run");

            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => view.Pose == CharacterPose.Shoot, 1f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Shoot), "a shot shows Shoot");

            Assert.That(_player.DashToward(_player.Position + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => view.Pose == CharacterPose.Dash, 1f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Dash), "a dash shows Dash, and cuts the shot's animation");
            yield return new WaitForSeconds(_feel.DashDuration * 0.5f);
            Assert.That(InState(view, CharacterAnimation.DashState), Is.True, "the Animator is in Dash");

            _player.Stop();
            yield return new WaitForSeconds(_feel.DashDuration + view.DashRecovery + 0.3f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Idle), "and back to Idle when all is over");
        }

        [UnityTest]
        public IEnumerator Animation_TheShotGoesOffAtTheClipsFiringMoment()
        {
            CharacterAnimation view = RiggedView();
            Assume.That(view.ShootFireTime, Is.GreaterThan(0f), "this model's shoot clip has a firing moment");
            _player.Teleport(NearA);
            yield return null;

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));

            AnimatorStateInfo state = view.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.IsName(CharacterAnimation.ShootState), Is.True, "the Animator is in Shoot at the hit");
            float clipTime = state.normalizedTime * state.length;
            Assert.That(clipTime, Is.EqualTo(view.ShootFireTime).Within(0.08f),
                        "the clip is at its firing moment when the shot lands");
        }

        [UnityTest]
        public IEnumerator Animation_Shooting_PointsBothArmsAndBothWeaponsAtTheTarget()
        {
            CharacterAnimation view = RiggedView();
            Assume.That(view.RightWeapon != null && view.LeftWeapon != null, Is.True, "this model holds a weapon in each hand");
            _player.Teleport(NearA);
            yield return null;
            yield return null;
            Assert.That(view.AimWeight, Is.EqualTo(0f), "at rest the arms are as the clip has them");

            _player.Shoot(_a.transform.position);
            yield return Until(() => view.AimWeight >= 1f, 2f);
            Assert.That(view.AimWeight, Is.EqualTo(1f), "the arms come up for the shot");
            yield return null; // read the pose after it was applied

            Vector3 toTarget = Flat(_a.transform.position - _player.Position).normalized;
            foreach (Transform weapon in new[] { view.RightWeapon, view.LeftWeapon })
                Assert.That(Vector3.Dot(weapon.right, toTarget), Is.GreaterThan(0.97f), weapon.name + " points at the target");
            foreach (Transform hand in new[] { view.RightHand, view.LeftHand })
                Assert.That(Vector3.Dot(hand.position - _player.Position, toTarget), Is.GreaterThan(0.35f), hand.name + " is held out in front");

            _player.Stop();
            yield return Until(() => view.AimWeight <= 0f, 2f);
            Assert.That(view.AimWeight, Is.EqualTo(0f), "and go down again when the shooting stops");
        }

        [UnityTest]
        public IEnumerator Animation_ThePistolsSitInTheHands_StandingRunningAndShooting()
        {
            CharacterAnimation view = RiggedView();
            Assume.That(view.RightWeapon != null && view.LeftWeapon != null, Is.True, "this model holds a pistol in each hand");
            yield return null;
            yield return null;

            // With grip settings the pistols are where the settings say. Without, they are where the model
            // had them when the scene was built. Either way they must stay there, in every pose.
            float rightReach = Vector3.Distance(view.RightWeapon.position, view.RightHand.position);
            float leftReach = Vector3.Distance(view.LeftWeapon.position, view.LeftHand.position);
            if (view.Grip != null)
            {
                float set = new Vector3(view.Grip.Forward, view.Grip.Up, view.Grip.Outward).magnitude;
                Assert.That(rightReach, Is.EqualTo(set).Within(0.01f), "right pistol, as the grip settings say");
                Assert.That(leftReach, Is.EqualTo(set).Within(0.01f), "left pistol, as the grip settings say");
            }
            Assert.That(rightReach, Is.InRange(0.03f, 0.35f), "the right pistol is at the hand");
            Assert.That(leftReach, Is.InRange(0.03f, 0.35f), "the left pistol is at the hand");
            System.Action<string> check = when =>
            {
                Assert.That(Vector3.Distance(view.RightWeapon.position, view.RightHand.position), Is.EqualTo(rightReach).Within(0.01f), "right pistol, " + when);
                Assert.That(Vector3.Distance(view.LeftWeapon.position, view.LeftHand.position), Is.EqualTo(leftReach).Within(0.01f), "left pistol, " + when);
            };

            _player.Steer(new Vector3(-6f, 0f, -5f) - _player.Position);
            yield return new WaitForSeconds(0.4f);
            check("running");
            _player.Stop();

            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => view.AimWeight >= 1f, 2f);
            yield return null;
            check("shooting");
        }

        [UnityTest]
        public IEnumerator Animation_TheModelStaysOnTheCharacter_ThroughADash()
        {
            CharacterAnimation view = RiggedView();
            yield return null;
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));

            float worst = 0f;
            float until = Time.time + _feel.DashDuration + view.DashRecovery + 0.2f;
            while (Time.time < until)
            {
                yield return null;
                worst = Mathf.Max(worst, FlatDistance(view.Hips.position, _player.Position));
            }
            Assert.That(worst, Is.LessThan(0.3f), "the roll clip travels, but the model must not leave the character");
        }

        // ---------------- Helpers ----------------

        [UnityTest]
        public IEnumerator Animation_BothShootingGaitsAreBakedForThisRig()
        {
            CharacterAnimation view = RiggedView();
            yield return null;
            foreach (string state in new[] { CharacterAnimation.ShootMoveState, CharacterAnimation.ShootBackState })
                Assert.That(view.Animator.HasState(0, Animator.StringToHash(state)), Is.True, "the Animator has a state " + state);
            AnimationClip forward = null, backward = null;
            foreach (AnimationClip clip in view.Animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                if (clip.name == "Outrider_grip_ShootRun") forward = clip;
                if (clip.name == CharacterAnimation.ShootBackState) backward = clip;
            }
            Assert.That(forward, Is.Not.Null, "the forward gait (Run and Shoot) baked for this rig");
            Assert.That(backward, Is.Not.Null, "the backward gait: Kristian's backward jog, made on his rig");
            Assert.That(backward.length, Is.EqualTo(0.533f).Within(0.01f).Or.EqualTo(0.667f).Within(0.01f), "one of the two v2 jogs (3.0 or 2.4 m/s)");
            Assert.That(view.Animator.HasState(0, Animator.StringToHash(CharacterAnimation.ShootBackState)), Is.True);
            foreach (AnimationClip clip in new[] { forward, backward })
            {
                Assert.That(clip.length, Is.GreaterThan(0.3f), clip.name + ": a whole cycle");
                Assert.That(clip.isLooping, Is.True, clip.name + ": looping");
            }
            Assert.That(view.ShootRunClipSpeed, Is.GreaterThan(0f), "the speeds their steps match are set");
            Assert.That(view.ShootBackClipSpeed, Is.GreaterThan(0f));
        }

        CharacterAnimation RiggedView()
        {
            var view = Object.FindAnyObjectByType<CharacterAnimation>();
            if (view == null) Assert.Ignore("the scene was built without a rigged player model");
            Assert.That(view.Player, Is.SameAs(_player));
            Assert.That(view.Animator, Is.Not.Null);
            Assert.That(view.Hips, Is.Not.Null);
            return view;
        }

        /// <summary>True when the Animator is in the state, or cross-fading into it.</summary>
        static bool InState(CharacterAnimation view, string stateName)
        {
            Animator animator = view.Animator;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName));
        }

        /// <summary>
        /// A point on the ground next to a dummy: outside the cursor pick slack, so it is a ground click,
        /// and inside the attack-move click radius, so the click still picks that dummy.
        /// </summary>
        void UseFakeInput() => _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;

        /// <summary>Gives the driver one frame, and waits until it has been read and a second, idle frame has passed.</summary>
        IEnumerator Feed(InputFrame frame)
        {
            _frames.Enqueue(frame);
            yield return null;
            yield return null;
            Assert.That(_frames.Count, Is.EqualTo(0), "the driver read the frame");
        }

        static Vector2 ScreenOf(Vector3 world) =>
            Camera.main.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));

        /// <summary>Holds the move direction toward a point, as a held key would, until she is within a distance of it; then lets go.</summary>
        IEnumerator SteerTo(Vector3 point, float within, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (FlatDistance(_player.Position, point) > within && Time.realtimeSinceStartup < deadline)
            {
                _player.Steer(point - _player.Position);
                yield return null;
            }
            _player.Stop();
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;
    }
}
