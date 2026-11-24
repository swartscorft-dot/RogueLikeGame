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
    /// G3: the five skills, one per input type, in the built ControlRoom scene. The character is driven
    /// through its intent API (PressSkill, ReleaseSkill, AimAt), or by feeding the input driver frames.
    /// Values come from the skill settings asset in the scene, so the tests still hold after tuning;
    /// where a test needs the layout to fit the values, it says so with an Assume.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3), and a tight group of
    /// Dummy_D, E and F around (-10.5, 6.7).
    /// </summary>
    public class SkillTests
    {
        const int Quick = 0, Keg = 1, Fan = 2, Long = 3, Volley = 5; // key order: Q W E R, S D F G
        const int Breaker = 9; // work order 015: in the pool, on no key of its own

        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here, B 5.8 m

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        FeelValues _feel;
        Damageable _a, _b, _d, _e, _f;

        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

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
            Assert.That(_player.SkillSet, Is.Not.Null, "the player uses the skill settings asset");
            Assert.That(_player.Skills.Count, Is.EqualTo(15)); // work order 015: eight start and pool skills grew to twelve; changed deliberately (work order 109): fifteen
            _feel = _player.Feel;
            _a = Dummy("Dummy_A");
            _b = Dummy("Dummy_B");
            _d = Dummy("Dummy_D");
            _e = Dummy("Dummy_E");
            _f = Dummy("Dummy_F");

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

        // ---------------- The settings ----------------

        [UnityTest]
        public IEnumerator TheFiveSkillsCoverTheFiveInputTypes()
        {
            yield return null;
            Assert.That(Skill(Quick).Input, Is.EqualTo(SkillInput.Normal));
            Assert.That(Skill(Keg).Input, Is.EqualTo(SkillInput.Point));
            Assert.That(Skill(Fan).Input, Is.EqualTo(SkillInput.Holding));
            Assert.That(Skill(Long).Input, Is.EqualTo(SkillInput.Charge));
            Assert.That(Skill(Volley).Input, Is.EqualTo(SkillInput.Casting));
        }

        // ---------------- Normal ----------------

        [UnityTest]
        public IEnumerator Normal_HitsOnce_ThenWaitsForItsCooldown()
        {
            SkillDefinition skill = Skill(Quick);
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(Quick));
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(Lost(_a), Is.EqualTo(skill.Damage));
            Assert.That(Lost(_b), Is.EqualTo(0), "the dummy off the line is untouched");
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Rejected), "on cooldown");
            Assert.That(_player.SkillCooldownRemaining(Quick), Is.GreaterThan(0f).And.LessThan(skill.Cooldown));

            yield return Until(() => _player.SkillCooldownRemaining(Quick) <= 0f, skill.Cooldown + 2f);
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started), "ready again");
        }

        // ---------------- Point ----------------

        [UnityTest]
        public IEnumerator Point_HitsEveryTargetInItsCircle()
        {
            SkillDefinition skill = Skill(Keg);
            Vector3 center = (_d.transform.position + _e.transform.position + _f.transform.position) / 3f;
            _player.Teleport(new Vector3(center.x, 0f, center.z - skill.Range * 0.7f));
            yield return null;
            foreach (Damageable d in new[] { _d, _e, _f })
                Assume.That(FlatDistance(d.transform.position, center), Is.LessThan(skill.Radius), "the group fits in the circle");

            Assert.That(_player.PressSkill(Keg, center), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.Startup * 0.5f);
            Assert.That(Lost(_d) + Lost(_e) + Lost(_f), Is.EqualTo(0), "nothing lands before the startup is over");
            // Work order 015: the keg lands, lies on its fuse, then bursts.
            yield return Until(() => _player.KegsBurst > 0, 4f);

            Assert.That(Lost(_d), Is.EqualTo(skill.Damage));
            Assert.That(Lost(_e), Is.EqualTo(skill.Damage));
            Assert.That(Lost(_f), Is.EqualTo(skill.Damage));
            Assert.That(Lost(_a) + Lost(_b), Is.EqualTo(0), "the dummies outside the circle are untouched");
        }

        [UnityTest]
        public IEnumerator Point_LandsNoFartherAwayThanItsRange()
        {
            SkillDefinition skill = Skill(Keg);
            Vector3 center = (_d.transform.position + _e.transform.position + _f.transform.position) / 3f;
            // Stand so far away that a circle at full range cannot touch the group.
            _player.Teleport(new Vector3(center.x, 0f, center.z - (skill.Range + skill.Radius + 3f)));
            yield return null;

            Assert.That(_player.PressSkill(Keg, center), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsBurst > 0, 4f); // work order 015: after its fuse

            Assert.That(_player.SkillShotsFired, Is.EqualTo(1), "it went off");
            Assert.That(Lost(_d) + Lost(_e) + Lost(_f), Is.EqualTo(0), "but short of the point that was aimed at");
        }

        // ---------------- Holding ----------------

        [UnityTest]
        public IEnumerator Holding_HitsWhileTheKeyIsDown_AndStopsAtTheRelease()
        {
            SkillDefinition skill = Skill(Fan);
            _player.Teleport(NearA);
            yield return null;
            Assume.That(skill.HoldMax, Is.GreaterThan(skill.TickInterval * 5f));

            Assert.That(_player.PressSkill(Fan, _a.transform.position), Is.EqualTo(IntentResult.Started));
            // Hold through three and a half ticks of the channel: hits at 0, 1, 2 and 3 intervals.
            yield return new WaitForSeconds(skill.Startup + skill.TickInterval * 3.5f);
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel));
            _player.ReleaseSkill(Fan);
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(_a.HitsTaken, Is.EqualTo(4), "one hit at once, then one per tick while held");
            Assert.That(Lost(_a), Is.EqualTo(4 * skill.Damage));
            Assert.That(Lost(_b), Is.EqualTo(0), "the dummy outside the cone is untouched");

            yield return new WaitForSeconds(skill.TickInterval * 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(4), "nothing after the release");
        }

        [UnityTest]
        public IEnumerator Holding_EndsByItselfAfterItsLongestHold()
        {
            SkillDefinition skill = Skill(Fan);
            _player.Teleport(NearA);
            yield return null;
            int expected = 1 + Mathf.FloorToInt(skill.HoldMax / skill.TickInterval + 1e-4f);
            Assume.That(expected * skill.Damage, Is.LessThan(_a.MaxHealth), "the dummy outlives the whole hold");

            Assert.That(_player.PressSkill(Fan, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None, 15f);

            Assert.That(_player.Action, Is.EqualTo(ActionKind.None), "it ended without a release");
            Assert.That(_a.HitsTaken, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator Holding_FollowsTheCursor()
        {
            SkillDefinition skill = Skill(Fan);
            _player.Teleport(NearA);
            yield return null;
            Assume.That(InReach(_player.GroundPosition, _b.GroundPosition, _b.Radius, skill.Range), Is.True, "B is within the cone's reach");

            _player.AimAt(_a.transform.position);
            Assert.That(_player.PressSkill(Fan, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.Startup + skill.TickInterval * 1.5f);
            Assert.That(_a.HitsTaken, Is.GreaterThan(0));
            Assert.That(_b.HitsTaken, Is.EqualTo(0));

            _player.AimAt(_b.transform.position);
            int aHits = 0;
            yield return null;
            yield return null;
            aHits = _a.HitsTaken;
            yield return new WaitForSeconds(skill.TickInterval * 2.5f);
            _player.ReleaseSkill(Fan);

            Assert.That(_b.HitsTaken, Is.GreaterThan(0), "the cone turned to the cursor");
            Assert.That(_a.HitsTaken, Is.EqualTo(aHits), "and off the first dummy");
            Vector3 toB = Flat(_b.transform.position - _player.Position).normalized;
            Assert.That(Vector3.Dot(_player.transform.forward, toB), Is.GreaterThan(0.99f), "the character faces the cursor");
        }

        // ---------------- Charge ----------------

        [UnityTest]
        public IEnumerator Charge_ReleasedEarly_HitsWeakly()
        {
            SkillDefinition skill = Skill(Long);
            Assume.That(skill.ChargeTime, Is.GreaterThan(0.5f));

            Assert.That(_player.PressSkill(Long, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.ChargeTime * 0.3f);
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel), "charging");
            Assert.That(Lost(_a), Is.EqualTo(0), "nothing fires while the key is down");
            _player.ReleaseSkill(Long);
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(Lost(_a), Is.InRange(SkillRules.Damage(skill, 0.25f), SkillRules.Damage(skill, 0.45f)),
                        "about a third charged");
            Assert.That(Lost(_a), Is.LessThan(skill.Damage));
        }

        [UnityTest]
        public IEnumerator Charge_HeldToFull_HitsWithAllItsDamage()
        {
            SkillDefinition skill = Skill(Long);
            Assume.That(skill.HoldMax, Is.GreaterThan(skill.ChargeTime + 0.3f));

            Assert.That(_player.PressSkill(Long, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.ChargeTime + 0.15f);
            Assert.That(_player.SkillCharge, Is.EqualTo(1f));
            _player.ReleaseSkill(Long);
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(Lost(_a), Is.EqualTo(skill.Damage));
        }

        [UnityTest]
        public IEnumerator Charge_NeverReleased_FiresByItself()
        {
            SkillDefinition skill = Skill(Long);
            float start = Time.time;

            Assert.That(_player.PressSkill(Long, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 1, skill.HoldMax + 3f);

            Assert.That(Lost(_a), Is.EqualTo(skill.Damage));
            Assert.That(Time.time - start, Is.EqualTo(skill.HoldMax + skill.Startup).Within(0.1f));
        }

        // ---------------- Casting ----------------

        [UnityTest]
        public IEnumerator Casting_HitsOnlyWhenTheCastIsOver()
        {
            SkillDefinition skill = Skill(Volley);
            Assume.That(skill.Startup, Is.GreaterThan(0.4f));

            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.Startup * 0.5f);
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Startup));
            Assert.That(_player.SkillProgress, Is.EqualTo(0.5f).Within(0.1f), "the cast bar is half full");
            Assert.That(Lost(_a), Is.EqualTo(0));

            yield return Until(() => _player.Action == ActionKind.None, 3f);
            Assert.That(Lost(_a), Is.EqualTo(skill.Damage));
        }

        [UnityTest]
        public IEnumerator Casting_ADashCancelsIt_AndTheSkillIsSpent()
        {
            SkillDefinition skill = Skill(Volley);
            Assume.That(_feel.DashCancelsSkill, Is.True);
            Assume.That(skill.Startup, Is.GreaterThan(0.4f));

            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(skill.Startup * 0.5f);
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 10f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(-1));

            yield return new WaitForSeconds(_feel.DashDuration + skill.Startup + 0.3f);
            Assert.That(Lost(_a), Is.EqualTo(0), "the cancelled cast never lands");
            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Rejected),
                        "and its cooldown is running");
        }

        // ---------------- Buffer ----------------

        [UnityTest]
        public IEnumerator Buffer_ASkillPressedLateInAnothersRecovery_FiresWhenItEnds()
        {
            // Powder Keg since decision 0005 (2026-11-19): Quick Shot's recovery became too short to press in. Breaker Slug
            // since work order 015, when the keg became light with a short recovery.
            SkillDefinition first = Skill(Breaker);
            Assume.That(first.Recovery, Is.GreaterThan(_feel.InputBuffer * 0.5f + 0.03f));

            Assert.That(_player.PressSkill(Breaker, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(first.Startup + first.Recovery - _feel.InputBuffer * 0.5f);
            // Decision 0004: with the free recovery the first skill is over for everything but the next skill.
            Assume.That(_player.ActiveSkillSlot == Breaker || _player.FreeRecoverySlot == Breaker, Is.True, "the press lands in the first skill's recovery");

            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Buffered));
            yield return Until(() => _player.ActiveSkillSlot == Volley, 1f);
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(Volley), "the buffered skill is the one that starts");
        }

        [UnityTest]
        public IEnumerator Buffer_AChargeSkillReleasedWhileItWaits_FiresWithNoCharge()
        {
            // Powder Keg, on B, since decision 0005 (2026-11-19): Quick Shot's recovery became too short to press in.
            // Breaker Slug since work order 015, when the keg became light with a short recovery.
            SkillDefinition first = Skill(Breaker);
            SkillDefinition charge = Skill(Long);
            Assume.That(first.Recovery, Is.GreaterThan(_feel.InputBuffer * 0.5f + 0.03f));

            Assert.That(_player.PressSkill(Breaker, _b.transform.position), Is.EqualTo(IntentResult.Started));
            yield return new WaitForSeconds(first.Startup + first.Recovery - _feel.InputBuffer * 0.5f);
            Assert.That(_player.PressSkill(Long, _a.transform.position), Is.EqualTo(IntentResult.Buffered));
            _player.ReleaseSkill(Long);

            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(Lost(_a), Is.EqualTo(SkillRules.Damage(charge, 0f)), "it started as already released");
        }

        // ---------------- Input driver, fed frames ----------------

        [UnityTest]
        public IEnumerator Input_ASkillKeyHeld_ThenLetGo_ReachesTheCharacter()
        {
            SkillDefinition skill = Skill(Fan);
            var indicator = Object.FindAnyObjectByType<SkillIndicator>();
            Assert.That(indicator, Is.Not.Null, "SkillIndicator in scene");
            UseFakeInput();
            _player.Teleport(NearA);
            yield return null;
            yield return null; // let the camera settle on the new position
            Vector2 onA = ScreenAtShotHeight(_a.transform.position); // work order 094: on the dummy as she sees it
            int key = 1 << Fan;

            _idle = new InputFrame { CursorScreen = onA, SkillsHeld = key };
            yield return Feed(new InputFrame { CursorScreen = onA, SkillsPressed = key, SkillsHeld = key });
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(Fan), "the press started the skill on that key");

            yield return new WaitForSeconds(skill.Startup + skill.TickInterval * 1.5f);
            Assert.That(_player.SkillPhase, Is.EqualTo(SkillPhase.Channel), "still going while the key is down");
            Assert.That(indicator.IsVisible, Is.True, "the aim outline is shown");
            Assert.That(_a.HitsTaken, Is.GreaterThan(0));
            // Kristian, 2026-11-23: a line or a wedge is outlined at the gun's height, so that it passes through the cursor on the
            // screen as the shot does (on the floor it lay under the cursor).
            var outline = indicator.GetComponent<LineRenderer>();
            Vector3 tip = outline.GetPosition(0), far = outline.GetPosition(1 + (outline.positionCount - 2) / 2);
            Assert.That(tip.y, Is.EqualTo(SkillIndicator.LineHeight).Within(1e-3f), "the wedge's outline is lifted off the floor, to the one height of her pistols");
            Assert.That(Mathf.Abs(_player.ShotHeight - SkillIndicator.LineHeight), Is.LessThan(0.35f), "which is near where the gun is now");
            Vector2 s0 = Camera.main.WorldToScreenPoint(tip), s1 = Camera.main.WorldToScreenPoint(far);
            Vector2 along = (s1 - s0).normalized;
            Vector2 toCursor = onA - s0;
            float off = Mathf.Abs(toCursor.x * along.y - toCursor.y * along.x);
            Assert.That(off, Is.LessThan(Screen.height * 0.03f), "the outline's middle passes by the cursor on the screen (on the floor it lay a metre and more under it)");

            _idle = new InputFrame { CursorScreen = ScreenAtShotHeight(_b.transform.position), SkillsHeld = key };
            yield return null;
            yield return null;
            yield return null;
            Assert.That(FlatDistance(_player.AimPoint, _b.transform.position), Is.LessThan(0.2f), "the aim follows the cursor");

            _idle = new InputFrame { CursorScreen = _idle.CursorScreen };
            yield return Until(() => _player.SkillPhase != SkillPhase.Channel, 0.5f);
            Assert.That(_player.SkillPhase, Is.Not.EqualTo(SkillPhase.Channel), "letting go ends the hold");
            yield return Until(() => _player.Action == ActionKind.None, 2f);
            Assert.That(indicator.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Input_EachSkillKey_StartsItsOwnSkill()
        {
            UseFakeInput();
            Vector2 onA = ScreenOf(_a.transform.position);
            _idle = new InputFrame { CursorScreen = onA };

            // Work order 015: eight keys, each starting the skill on it (with every skill had, the skill of its own place).
            for (int key = 0; key < _player.KeyCount; key++)
            {
                int slot = _player.SkillOnKey(key);
                Assert.That(slot, Is.EqualTo(key));
                yield return Feed(new InputFrame { CursorScreen = onA, SkillsPressed = 1 << key });
                Assert.That(_player.ActiveSkillSlot, Is.EqualTo(slot), $"skill key {key} starts skill {slot}");
                Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
                _player.Teleport(_player.Position); // clear it, so the next key is not buffered
                yield return null;
            }
        }

        // ---------------- Helpers ----------------

        SkillDefinition Skill(int slot) => _player.Skills[slot];

        static Damageable Dummy(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.That(go, Is.Not.Null, name + " in scene");
            return go.GetComponent<Damageable>();
        }

        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;

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

        /// <summary>
        /// Work order 094: the cursor over a point as she sees what she shoots at, at the height the shot is drawn and aimed at:
        /// the aim meets the cursor's ray there, so the shot goes to the point itself. On the point's ground spot, the aim would
        /// pass that spot's screen point at the gun's height, nearer the camera.
        /// </summary>
        Vector2 ScreenAtShotHeight(Vector3 world) => Camera.main.WorldToScreenPoint(new Vector3(world.x, _player.ShotHeight, world.z));

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;

        /// <summary>True when a target's edge is within a reach of a point on the ground.</summary>
        static bool InReach(GroundPoint from, GroundPoint target, float radius, float reach) => from.DistanceTo(target) - radius <= reach;
    }
}
