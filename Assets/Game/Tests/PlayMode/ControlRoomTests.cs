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
        public void RestoreTime() => Time.timeScale = 1f;

        // ---------------- C2 Move ----------------

        [UnityTest]
        public IEnumerator Move_ArrivesAtRunSpeed_AndTheOrderEnds()
        {
            var target = new Vector3(-6f, 0f, -5f);
            float distance = FlatDistance(_player.Position, target);
            float start = Time.time;

            _player.MoveTo(target);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move));
            yield return Until(() => FlatDistance(_player.Position, target) < 0.15f, 10f);

            float elapsed = Time.time - start;
            float expected = distance / _feel.RunSpeed;
            Assert.That(FlatDistance(_player.Position, target), Is.LessThan(0.15f), $"arrived (at {_player.Position})");
            Assert.That(elapsed, Is.InRange(expected - 0.2f, expected + 0.5f + _feel.TimeToFullSpeed),
                        $"took {elapsed:0.00}s for {distance:0.00}m");

            yield return Until(() => _player.Order == OrderKind.None, 2f);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "the order is done on arrival");
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
            if (_feel.FaceCursorOnCast)
            {
                Vector3 want = Flat(aim - _player.Position).normalized;
                Assert.That(Vector3.Dot(_player.transform.forward, want), Is.GreaterThan(0.99f), "faces the aim point");
            }

            yield return Until(() => _player.Action == ActionKind.None, 5f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(1));
            Assert.That(_player.SkillShotHits, Is.EqualTo(1));
            Assert.That(before - _a.CurrentHealth, Is.EqualTo(Placeholders.SkillDamage));
            Assert.That(_b.HitsTaken, Is.EqualTo(0), "the dummy off the line is untouched");
        }

        // ---------------- C4 Attack-move ----------------

        // Both sides are tested, so the nearest target must win whichever dummy the game happens to list first.
        [UnityTest]
        public IEnumerator AttackMove_NearA_ShootsARepeatedly_WithoutWalking() => AttackMoveShootsTheNearest(NearA, _a, _b);

        [UnityTest]
        public IEnumerator AttackMove_NearB_ShootsBRepeatedly_WithoutWalking() => AttackMoveShootsTheNearest(NearB, _b, _a);

        IEnumerator AttackMoveShootsTheNearest(Vector3 from, Damageable near, Damageable far)
        {
            Assume.That(Targeting.InRange(new GroundPoint(from.x, from.z), far.GroundPosition, far.Radius, _feel.BasicAttackRange),
                        Is.True, "the farther dummy is in range too, so there is a choice to make");
            _player.Teleport(from);
            yield return null;
            Vector3 start = _player.Position;

            _player.AttackMoveTo(from + Vector3.back * 9f);
            yield return Until(() => near.HitsTaken + far.HitsTaken >= 2, 5f);

            Assert.That(near.HitsTaken, Is.EqualTo(2), "keeps shooting the nearest target");
            Assert.That(near.CurrentHealth, Is.EqualTo(near.MaxHealth - 2 * Placeholders.BasicShotDamage));
            Assert.That(far.HitsTaken, Is.EqualTo(0), "not the farther one");
            Assert.That(FlatDistance(_player.Position, start), Is.LessThan(0.05f), "does not walk while a target is in range");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackMove), "the order stays");
        }

        [UnityTest]
        public IEnumerator AttackMove_TargetOutOfRange_WalksUntilInRange_ThenShoots()
        {
            float range = _feel.BasicAttackRange;
            Vector3 a = _a.transform.position;
            _player.Teleport(new Vector3(a.x, 0f, a.z - (range + 4f)));
            yield return null;
            Assert.That(Targeting.InRange(_player.GroundPosition, _a.GroundPosition, _a.Radius, range), Is.False, "starts out of range");

            _player.AttackMoveTo(a);
            yield return Until(() => _a.HitsTaken >= 1, 10f);

            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            float gap = Targeting.Gap(_player.GroundPosition, _a.GroundPosition, _a.Radius);
            Assert.That(gap, Is.LessThanOrEqualTo(range + 0.05f), "shot from inside the range");
            Assert.That(gap, Is.GreaterThan(range - 0.6f), "stopped as soon as the target came into range");
            Assert.That(_b.HitsTaken, Is.EqualTo(0));
        }

        // The click picks the target: a dummy near the click wins over one nearer the character.

        [UnityTest]
        public IEnumerator AttackMove_ClickNearTheFartherDummy_ShootsThatOne_FromNearA() => AttackMoveShootsTheOneNearTheClick(NearA, _b, _a);

        [UnityTest]
        public IEnumerator AttackMove_ClickNearTheFartherDummy_ShootsThatOne_FromNearB() => AttackMoveShootsTheOneNearTheClick(NearB, _a, _b);

        IEnumerator AttackMoveShootsTheOneNearTheClick(Vector3 from, Damageable clicked, Damageable nearer)
        {
            var origin = new GroundPoint(from.x, from.z);
            Assume.That(Targeting.InRange(origin, clicked.GroundPosition, clicked.Radius, _feel.BasicAttackRange), Is.True, "the clicked dummy is in range");
            Assume.That(Targeting.Gap(origin, nearer.GroundPosition, nearer.Radius),
                        Is.LessThan(Targeting.Gap(origin, clicked.GroundPosition, clicked.Radius)), "and the other one is nearer the character");
            _player.Teleport(from);
            yield return null;
            Vector3 start = _player.Position;

            _player.AttackMoveTo(GroundBeside(clicked));
            Assert.That(_player.OrderTarget, Is.SameAs(clicked), "the click picked the dummy near it");
            yield return Until(() => clicked.HitsTaken + nearer.HitsTaken >= 2, 5f);

            Assert.That(clicked.HitsTaken, Is.EqualTo(2), "shoots the dummy near the click");
            Assert.That(nearer.HitsTaken, Is.EqualTo(0), "not the one nearer the character");
            Assert.That(FlatDistance(_player.Position, start), Is.LessThan(0.05f), "does not walk while that target is in range");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackMove));
        }

        [UnityTest]
        public IEnumerator AttackMove_ClickNearADummyOutOfRange_WalksToIt_AndLeavesTheNearerOneAlone()
        {
            float range = _feel.BasicAttackRange;
            _player.Teleport(NearA);
            yield return null;
            Assume.That(Targeting.InRange(_player.GroundPosition, _a.GroundPosition, _a.Radius, range), Is.True, "A is in range");
            Assume.That(Targeting.InRange(_player.GroundPosition, _c.GroundPosition, _c.Radius, range), Is.False, "C is not");

            _player.AttackMoveTo(GroundBeside(_c));
            Assert.That(_player.OrderTarget, Is.SameAs(_c));
            yield return Until(() => _c.HitsTaken >= 1 || _a.HitsTaken >= 1, 10f);

            Assert.That(_c.HitsTaken, Is.EqualTo(1), "walked to the dummy near the click and shot it");
            Assert.That(_a.HitsTaken, Is.EqualTo(0), "the dummy that was in range from the start is left alone");
            float gap = Targeting.Gap(_player.GroundPosition, _c.GroundPosition, _c.Radius);
            Assert.That(gap, Is.LessThanOrEqualTo(range + 0.05f), "shot from inside the range");
            Assert.That(gap, Is.GreaterThan(range - 0.6f), "stopped as soon as it came into range");
        }

        [UnityTest]
        public IEnumerator AttackMove_WhenTheClickedDummyDies_GoesOnWithTheNearest()
        {
            _player.Teleport(NearA);
            yield return null;
            _b.TakeHit(_b.MaxHealth - 1); // one shot left in it
            Assume.That(_b.IsAlive, Is.True);

            _player.AttackMoveTo(GroundBeside(_b));
            Assert.That(_player.OrderTarget, Is.SameAs(_b));
            yield return Until(() => !_b.IsAlive, 3f);
            Assert.That(_b.IsAlive, Is.False, "the clicked dummy is shot first");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));

            yield return Until(() => _a.HitsTaken >= 2, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(2), "then the nearest in range");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackMove), "the order stays");
            Assert.That(_player.OrderTarget, Is.Null, "with no picked target left");
        }

        // ---------------- C5 Attack-target ----------------

        [UnityTest]
        public IEnumerator AttackTarget_ShootsOnlyTheChosenTarget()
        {
            _player.Teleport(NearA);
            yield return null;

            _player.AttackTarget(_b); // A is nearer, B is the order
            yield return Until(() => _b.HitsTaken >= 2, 10f);

            Assert.That(_b.HitsTaken, Is.EqualTo(2));
            Assert.That(_a.HitsTaken, Is.EqualTo(0), "the nearer dummy is left alone");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackTarget));
            Assert.That(_player.OrderTarget, Is.SameAs(_b));
        }

        [UnityTest]
        public IEnumerator AttackTarget_EndsWhenTheTargetDies()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);

            _a.TakeHit(_a.MaxHealth);
            Assert.That(_a.IsAlive, Is.False);
            yield return Until(() => _player.Order == OrderKind.None && _player.Action == ActionKind.None, 3f);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "nothing left to attack");

            int shots = _player.BasicShotsFired;
            yield return new WaitForSeconds(ShotSeconds * 2f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(shots), "and it stops shooting");
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

        // ---------------- C6 Commitment ----------------

        [UnityTest]
        public IEnumerator MoveOrderDuringAShot_WaitsForRecovery_ThenMoves()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.AttackTarget(_a);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 2f);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot));
            Vector3 lockedAt = _player.Position;

            _player.MoveTo(new Vector3(2f, 0f, -6f));
            float deadline = Time.realtimeSinceStartup + 5f;
            int lockedFrames = 0;
            while (_player.Action == ActionKind.BasicShot && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(FlatDistance(_player.Position, lockedAt), Is.LessThan(0.01f), "locked until the shot is over");
                lockedFrames++;
                yield return null;
            }
            Assert.That(lockedFrames, Is.GreaterThan(0));
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the shot still landed");

            yield return Until(() => FlatDistance(_player.Position, lockedAt) > 1f, 3f);
            Assert.That(FlatDistance(_player.Position, lockedAt), Is.GreaterThan(1f), "then the move order runs");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "and it does not shoot again");
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
            Assume.That(_feel.HitStop, Is.GreaterThan(0f));
            _player.Teleport(NearA);
            yield return null;

            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "frozen on the frame of the hit");
            Assert.That(HitStop.IsActive, Is.True);

            yield return new WaitForSecondsRealtime(_feel.HitStop + 0.1f);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time runs again");
            Assert.That(HitStop.IsActive, Is.False);
        }

        // ---------------- C10 Input driver, fed frames ----------------

        [UnityTest]
        public IEnumerator Input_HeldRightClick_FollowsTheCursor_AndStopsAfterRelease()
        {
            UseFakeInput();
            Vector3 start = _player.Position;
            var offset = new Vector3(-5f, 0f, -4f);
            Vector2 screen = ScreenOf(start + offset);

            _idle = new InputFrame { CursorScreen = screen, MoveHeld = true };
            yield return new WaitForSeconds(0.5f);

            Vector3 moved = Flat(_player.Position - start);
            Assert.That(moved.magnitude, Is.EqualTo(_feel.RunSpeed * 0.5f).Within(0.6f + _feel.RunSpeed * _feel.TimeToFullSpeed),
                        "runs at run speed for as long as the button is held");
            Assert.That(Vector3.Dot(moved.normalized, offset.normalized), Is.GreaterThan(0.98f), "toward the cursor");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move));

            _idle = new InputFrame { CursorScreen = screen };
            yield return Until(() => _player.Order == OrderKind.None, 5f);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "walks to the last cursor point and stops");
        }

        [UnityTest]
        public IEnumerator Input_A_ShowsTheRangeRing_AndAGroundClickOrdersAttackMove()
        {
            UseFakeInput();
            _player.Teleport(new Vector3(0f, 0f, -12f)); // nothing in range here
            yield return null;
            yield return null;
            _idle = new InputFrame { CursorScreen = ScreenOf(_player.Position + new Vector3(-3f, 0f, 2f)) };
            Assert.That(_driver.RangeRing, Is.Not.Null);
            Assert.That(_driver.RangeRing.IsVisible, Is.False);

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, AttackMovePressed = true });
            Assert.That(_driver.IsAimingAttackMove, Is.True);
            Assert.That(_driver.RangeRing.IsVisible, Is.True, "A shows the basic-attack range");
            Assert.That(_driver.RangeRing.Radius, Is.EqualTo(_feel.BasicAttackRange));
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "A alone orders nothing");

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, ConfirmPressed = true });
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackMove), "left-click confirms");
            Assert.That(_driver.IsAimingAttackMove, Is.False);
            Assert.That(_driver.RangeRing.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Input_AThenClickOnADummy_OrdersAttackOnThatDummy()
        {
            UseFakeInput();
            _idle = new InputFrame { CursorScreen = ScreenOf(_b.transform.position) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, AttackMovePressed = true });
            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, ConfirmPressed = true });

            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackTarget));
            Assert.That(_player.OrderTarget, Is.SameAs(_b));
        }

        [UnityTest]
        public IEnumerator Input_RightClickWhileAiming_CancelsTheAim_AndMoves()
        {
            UseFakeInput();
            _idle = new InputFrame { CursorScreen = ScreenOf(_player.Position + new Vector3(0f, 0f, -3f)) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, AttackMovePressed = true });
            Assert.That(_driver.IsAimingAttackMove, Is.True);

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, MoveHeld = true });
            Assert.That(_driver.IsAimingAttackMove, Is.False);
            Assert.That(_driver.RangeRing.IsVisible, Is.False);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move));
        }

        [UnityTest]
        public IEnumerator Input_Esc_CancelsTheAim_AndALeftClickAloneDoesNothing()
        {
            UseFakeInput();
            _idle = new InputFrame { CursorScreen = ScreenOf(_player.Position + new Vector3(0f, 0f, -3f)) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, AttackMovePressed = true });
            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, CancelPressed = true });
            Assert.That(_driver.IsAimingAttackMove, Is.False);
            Assert.That(_driver.RangeRing.IsVisible, Is.False);

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, ConfirmPressed = true });
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "a left-click without A orders nothing");
        }

        [UnityTest]
        public IEnumerator Input_SpaceDashesTowardTheCursor_AndQCastsAtIt()
        {
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
            yield return Feed(new InputFrame { CursorScreen = onA, SkillPressed = true });
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
            yield return Until(() => _player.Action == ActionKind.None, 5f);
            Assert.That(_player.SkillShotHits, Is.EqualTo(1), "the cast went to the cursor, where Dummy_A stands");
        }

        [UnityTest]
        public IEnumerator Input_AThenClickOnTheGroundBesideADummy_OrdersAttackMove_GoingForThatDummy()
        {
            UseFakeInput();
            _player.Teleport(NearA); // A is the nearer one; the click is beside B
            yield return null;
            yield return null; // let the camera settle on the new position
            _idle = new InputFrame { CursorScreen = ScreenOf(GroundBeside(_b)) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, AttackMovePressed = true });
            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, ConfirmPressed = true });

            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackMove), "a click off the dummy is an attack-move");
            Assert.That(_player.OrderTarget, Is.SameAs(_b), "that goes for the dummy nearest the click");
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

            _player.MoveTo(new Vector3(-6f, 0f, -5f));
            yield return Until(() => view.Pose == CharacterPose.Run, 1f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Run), "moving shows Run");
            yield return new WaitForSeconds(0.3f);
            Assert.That(InState(view, CharacterAnimation.RunState), Is.True, "the Animator is in Run");

            _player.Teleport(NearA);
            yield return null;
            _player.AttackTarget(_a);
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
            _player.Teleport(NearA);
            yield return null;

            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));

            AnimatorStateInfo state = view.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.IsName(CharacterAnimation.ShootState), Is.True, "the Animator is in Shoot at the hit");
            float clipTime = state.normalizedTime * state.length;
            Assert.That(clipTime, Is.EqualTo(view.ShootFireTime).Within(0.08f),
                        "the clip is at its firing moment when the shot lands");
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
        Vector3 GroundBeside(Damageable d)
        {
            float gap = (Placeholders.CursorPickSlack + _feel.AttackMoveClickRadius) * 0.5f;
            Assume.That(gap, Is.GreaterThan(Placeholders.CursorPickSlack).And.LessThan(_feel.AttackMoveClickRadius),
                        "the click radius is wider than the cursor pick slack");
            Vector3 p = d.transform.position;
            return new Vector3(p.x, 0f, p.z - (d.Radius + gap));
        }

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

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
