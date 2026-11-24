using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The sprint (work order 092; Kristian 2026-11-23: a sprint on Shift, no shots meanwhile, a jump over the blue props, a small
    /// stagger when hit; <see cref="SprintRules"/>): its values and switch; when it is on and how fast; no shot or skill while it is on;
    /// the jump's decision (a low prop yes; a tall prop, a wall, a shut door's block, an enemy and no free far side no; where she lands);
    /// the stagger only for a hit that hurt her while sprinting or jumping; the held duck's new timing; and the summary's lines.
    /// </summary>
    public class SprintRulesTests
    {
        static readonly FeelValues Feel = new FeelValues();
        static readonly GroundPoint Her = new GroundPoint(0f, 0f);
        static readonly GroundPoint North = new GroundPoint(0f, 1f);

        // A sarcophagus as a level stands it (1.9 m along x, 1.1 m along z), its near face 0.7 m north of her middle: where the
        // NavMesh lets her stand, her 0.5 m radius and a little.
        static CoverShape Sarcophagus(float face = 0.7f) => CoverShape.Box(new GroundPoint(0f, face + 0.55f), 1.9f, 1.1f);

        static JumpPlan Decide(IEnumerable<JumpShape> shapes, IEnumerable<TargetInfo> enemies = null, System.Func<GroundPoint, bool> walkable = null,
                               CoverLine cover = null, FeelValues f = null, GroundPoint? way = null) =>
            SprintRules.Decide(f ?? Feel, Her, way ?? North, shapes.ToList(), enemies?.ToList(), walkable, cover);

        // ---------------- The values ----------------

        [Test]
        public void TheStartValues_AreTheOrdersNumbers()
        {
            Assert.That(Feel.Sprint, Is.True, "on by default");
            Assert.That(Feel.SprintSpeedShare, Is.EqualTo(1.3f), "7.8 m/s against 6 (Kristian 2026-11-24; 1.5 before)");
            Assert.That(Feel.RunSpeed * Feel.SprintSpeedShare, Is.EqualTo(7.8f).Within(1e-4f));
            Assert.That(Feel.SprintRampTime, Is.LessThanOrEqualTo(0.1f), "full speed within 0.1 s");
            Assert.That(Feel.SprintJumpLookAhead, Is.EqualTo(1f), "her path over the next 1 m");
            Assert.That(Feel.SprintJumpMost, Is.EqualTo(4f), "at most 4 m in all");
            Assert.That(Feel.SprintJumpRoom, Is.GreaterThanOrEqualTo(Placeholders.PlayerRadius), "her own room past the prop");
            Assert.That(Feel.SprintStagger, Is.EqualTo(0.35f));
        }

        // ---------------- When, and how fast ----------------

        [Test]
        public void On_OnlyWithTheSwitchTheKeyAMoveKeyAndFree()
        {
            Assert.That(SprintRules.On(Feel, keyHeld: true, moving: true, free: true), Is.True);
            Assert.That(SprintRules.On(Feel, keyHeld: false, moving: true, free: true), Is.False, "the key up");
            Assert.That(SprintRules.On(Feel, keyHeld: true, moving: false, free: true), Is.False, "standing with the key held does nothing");
            Assert.That(SprintRules.On(Feel, keyHeld: true, moving: true, free: false), Is.False, "in a dash, a skill, a duck, a stun");
            Assert.That(SprintRules.On(new FeelValues { Sprint = false }, true, true, true), Is.False, "the switch off");
            Assert.That(SprintRules.On(null, true, true, true), Is.False);
        }

        [Test]
        public void TheSpeed_RisesToItsShareWithinItsRamp_AndIsOneOtherwise()
        {
            Assert.That(SprintRules.SpeedShare(Feel, false, 5f), Is.EqualTo(1f), "not sprinting: her run speed");
            Assert.That(SprintRules.SpeedShare(Feel, true, 0f), Is.EqualTo(1f).Within(1e-4f), "from her run speed");
            Assert.That(SprintRules.SpeedShare(Feel, true, Feel.SprintRampTime * 0.5f), Is.EqualTo(1.15f).Within(1e-4f));
            Assert.That(SprintRules.SpeedShare(Feel, true, 0.1f), Is.EqualTo(1.3f).Within(1e-4f), "full within 0.1 s");
            Assert.That(SprintRules.SpeedShare(Feel, true, 30f), Is.EqualTo(1.3f), "no longest time: no stamina");
            Assert.That(SprintRules.SpeedShare(new FeelValues { Sprint = false }, true, 1f), Is.EqualTo(1f), "the switch off: today's speed");
            Assert.That(SprintRules.SpeedShare(new FeelValues { SprintRampTime = 0f }, true, 0f), Is.EqualTo(1.3f), "no ramp: at once");
            // 15.6 m in 2 s where she ran 12, short of it by the ramp's 0.09 m (half of the 1.8 m/s more, for 0.1 s).
            float metres = 0f;
            for (int i = 0; i < 2000; i++) metres += Feel.RunSpeed * SprintRules.SpeedShare(Feel, true, (i + 0.5f) * 0.001f) * 0.001f;
            Assert.That(metres, Is.EqualTo(15.6f - 0.09f).Within(0.01f));
        }

        [Test]
        public void NoShotOrSkill_WhileSheSprintsOrJumps()
        {
            Assert.That(SprintRules.Refuses(Feel, true), Is.True);
            Assert.That(SprintRules.Refuses(Feel, false), Is.False, "the key up: she acts at once");
            Assert.That(SprintRules.Refuses(new FeelValues { Sprint = false }, true), Is.False);
        }

        [Test]
        public void TheStagger_OnlyForAHitThatHurtHerWhileSprintingOrJumping()
        {
            Assert.That(SprintRules.Staggers(Feel, true, 9), Is.True);
            Assert.That(SprintRules.Staggers(Feel, false, 9), Is.False, "hit while running or standing: as today");
            Assert.That(SprintRules.Staggers(Feel, true, 0), Is.False, "let through unharmed by the dash's protected part");
            Assert.That(SprintRules.Staggers(new FeelValues { Sprint = false }, true, 9), Is.False);
            Assert.That(SprintRules.Staggers(new FeelValues { SprintStagger = 0f }, true, 9), Is.False);
            Assert.That(SprintRules.StaggerName, Is.EqualTo("sprint stagger"));
        }

        // ---------------- The jump ----------------

        [Test]
        public void ALowPropAhead_WithFreeFloorBeyond_IsJumped_AndSheLandsHerRoomPastItsFarFace()
        {
            JumpPlan plan = Decide(new[] { new JumpShape(Sarcophagus(), true) });
            Assert.That(plan.Jumps, Is.True, plan.Refusal.ToString());
            Assert.That(plan.Prop, Is.EqualTo(0));
            float farFace = 0.7f + 1.1f;
            Assert.That(plan.To.Z, Is.EqualTo(farFace + Feel.SprintJumpRoom).Within(1e-3f), "straight on, her room past its far face");
            Assert.That(plan.To.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(plan.Length, Is.EqualTo(farFace + Feel.SprintJumpRoom).Within(1e-3f));
            Assert.That(SprintRules.JumpSeconds(plan.Length, 9f), Is.EqualTo(plan.Length / 9f).Within(1e-5f), "crossed at her sprint speed");
            Assert.That(SprintRules.Lift(Feel, 0.5f), Is.EqualTo(Feel.SprintJumpLift).Within(1e-4f), "lifted most at its middle");
            Assert.That(SprintRules.Lift(Feel, 0f) + SprintRules.Lift(Feel, 1f), Is.EqualTo(0f), "on the ground at both ends");
        }

        [Test]
        public void ACircleProp_AndOneCrossedOnTheSlant_LandClearOfIt()
        {
            var basin = CoverShape.Circle(new GroundPoint(0f, 1.2f), 0.5f);
            JumpPlan plan = Decide(new[] { new JumpShape(basin, true) });
            Assert.That(plan.Jumps, Is.True, plan.Refusal.ToString());
            Assert.That(basin.Distance(plan.To), Is.GreaterThanOrEqualTo(Feel.SprintJumpRoom - 1e-3f));
            var slant = new GroundPoint(0.5f, 1f);
            plan = Decide(new[] { new JumpShape(Sarcophagus(), true) }, way: slant);
            Assert.That(plan.Jumps, Is.True, plan.Refusal.ToString());
            Assert.That(Sarcophagus().Distance(plan.To), Is.GreaterThanOrEqualTo(Feel.SprintJumpRoom - 1e-3f), "her room from the prop at the landing");
        }

        [Test]
        public void NoJump_AtATallProp_AWall_OrNothingWithinTheLookAhead()
        {
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(), false) }).Refusal, Is.EqualTo(JumpRefusal.NothingAhead), "a tall prop or a wall is never jumped");
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(1.2f), true) }).Refusal, Is.EqualTo(JumpRefusal.NothingAhead), "1.2 m off: not yet");
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(), true) }, way: new GroundPoint(1f, 0f)).Refusal, Is.EqualTo(JumpRefusal.NothingAhead),
                        "running past it, not into it");
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(), true) }, way: new GroundPoint(0f, 0f)).Jumps, Is.False, "standing");
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(), true) }, f: new FeelValues { Sprint = false }).Jumps, Is.False, "the switch off");
        }

        [Test]
        public void NoJump_WhenTheFarSideIsNotFree()
        {
            JumpShape prop = new JumpShape(Sarcophagus(), true);
            // Another prop just behind it, a wall behind it.
            var behind = new JumpShape(CoverShape.Box(new GroundPoint(0f, 2.3f), 1f, 0.6f), true);
            Assert.That(Decide(new[] { prop, behind }).Refusal, Is.EqualTo(JumpRefusal.Blocked), "another prop on the far side");
            var wall = new JumpShape(CoverShape.Box(new GroundPoint(0f, 2.4f), 10f, 0.4f), false);
            Assert.That(Decide(new[] { prop, wall }).Refusal, Is.EqualTo(JumpRefusal.Blocked), "a wall right behind it");
            // A shut door's block, a tall prop beyond: seen by the cover's ray, which stops 1.5 m out.
            Assert.That(Decide(new[] { prop }, cover: (a, b) => 1.5f).Refusal, Is.EqualTo(JumpRefusal.Blocked), "a shut door's block or a tall prop on the way");
            Assert.That(Decide(new[] { prop }, cover: (a, b) => a.DistanceTo(b)).Jumps, Is.True, "the cover's ray clear");
            Assert.That(Decide(new[] { prop }, walkable: p => false).Refusal, Is.EqualTo(JumpRefusal.NoFloor), "no NavMesh there");
            var enemy = new TargetInfo(new GroundPoint(0f, 2.6f), 0.5f, true);
            Assert.That(Decide(new[] { prop }, new[] { enemy }).Refusal, Is.EqualTo(JumpRefusal.EnemyThere), "an enemy standing there");
            Assert.That(Decide(new[] { prop }, new[] { new TargetInfo(enemy.Position, 0.5f, false) }).Jumps, Is.True, "a dead one is not");
            Assert.That(Decide(new[] { prop }, new[] { new TargetInfo(new GroundPoint(4f, 2.6f), 0.5f, true) }).Jumps, Is.True, "one off to the side is not");
        }

        [Test]
        public void NoJump_OverAPropTooDeep_ForTheLongestJump()
        {
            var deep = new JumpShape(CoverShape.Box(new GroundPoint(0f, 0.7f + 1.5f), 1.9f, 3f), true);
            JumpPlan plan = Decide(new[] { deep });
            Assert.That(plan.Refusal, Is.EqualTo(JumpRefusal.TooFar), "0.7 + 3 + 0.6 m is past 4 m");
        }

        [Test]
        public void TheProp_MustBeTheFirstThingOnHerWay()
        {
            var wallFirst = new JumpShape(CoverShape.Box(new GroundPoint(0f, 0.75f), 10f, 0.1f), false);
            Assert.That(Decide(new[] { new JumpShape(Sarcophagus(0.9f), true), wallFirst }).Refusal, Is.EqualTo(JumpRefusal.Blocked));
        }

        [Test]
        public void TheShapesExit_IsWhereTheLineLeavesIt()
        {
            CoverShape box = Sarcophagus();
            Assert.That(box.Exit(Her, North), Is.EqualTo(1.8f).Within(1e-4f));
            Assert.That(box.Exit(Her, new GroundPoint(0f, -1f)), Is.EqualTo(-1f), "behind her");
            Assert.That(box.Exit(Her, new GroundPoint(1f, 0f)), Is.EqualTo(-1f), "beside her");
        }

        // ---------------- The held duck's new timing ----------------

        [Test]
        public void TheHeldDuck_WaitsOnlyAFrameAfterRising_AndKeepsAPressWhileBusy_AndGoesDownAgainWhenTheShotHasLeft()
        {
            Assert.That(Feel.DuckHoldWait, Is.EqualTo(0.05f), "it only has to stop a flicker within one frame");
            Assert.That(HeldDuckRules.Wait(Feel), Is.EqualTo(0.05f));
            Assert.That(Feel.DuckReach, Is.EqualTo(1f), "his answer: the reach stays");
            Assert.That(Feel.DuckPressKeptWhenBusy, Is.True);
            Assert.That(Feel.DuckDownWhenShotLeft, Is.True);
            Assert.That(Feel.BasicShotStartup, Is.GreaterThan(Feel.DuckHoldWait), "down again after a shot waits for the shot, not the wait");
            var duck = new QuickDuck();
            duck.Tap(1f, true, true, HeldDuckRules.Seconds(Feel));
            duck.End(2f, HeldDuckRules.Wait(Feel));
            Assert.That(duck.Tap(2.03f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Waiting), "two frames after rising");
            Assert.That(duck.Tap(2.05f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Ducked), "0.05 s after: down");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_GivesTheSecondsSprinting_TheJumps_TheStaggers_AndThePressesThatDidNothing()
        {
            var start = new SessionEvent(0, SessionEventTypes.SessionStart);
            start.AddExtra("feel.Sprint", "true");
            var events = new List<SessionEvent> { start };
            var wake = new SessionEvent(10, SessionEventTypes.EnemyWake) { EnemyId = 1 };
            events.Add(wake);
            var end1 = new SessionEvent(14, SessionEventTypes.SprintEnd) { Result = "released" };
            end1.AddExtra("seconds", "2");
            events.Add(end1);
            var jump = new SessionEvent(13, SessionEventTypes.SprintJump, "Prop_C2_0_Sarcophagus");
            jump.AddExtra("metres", "2.4");
            events.Add(jump);
            events.Add(new SessionEvent(12.5, SessionEventTypes.SprintRefused, SessionWords.BasicShot));
            events.Add(new SessionEvent(12.6, SessionEventTypes.SprintRefused, "Quick Shot") { Slot = 0 });
            var stagger = new SessionEvent(13.5, SessionEventTypes.PlayerStunned, SprintRules.StaggerName);
            stagger.AddExtra("seconds", "0.35");
            events.Add(stagger);
            var bash = new SessionEvent(15, SessionEventTypes.PlayerStunned, "Shield bash");
            bash.AddExtra("seconds", "0.8");
            events.Add(bash);
            events.Add(new SessionEvent(20, SessionEventTypes.EnemyDeath) { EnemyId = 1 });
            var end2 = new SessionEvent(40, SessionEventTypes.SprintEnd) { Result = "stopped" };
            end2.AddExtra("seconds", "3");
            events.Add(end2);
            events.Add(new SessionEvent(50, SessionEventTypes.SessionEnd));

            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.HasSprint, Is.True);
            Assert.That(stats.Session.SprintSeconds, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(stats.Session.SprintShare, Is.EqualTo(0.1f).Within(1e-3f), "5 s of 50");
            Assert.That(stats.InFights.SprintSeconds, Is.EqualTo(2f).Within(1e-3f), "the sprint in the fight of 10 s");
            Assert.That(stats.InFights.SprintShare, Is.EqualTo(0.2f).Within(1e-3f), "a share of the fights' time");
            Assert.That(stats.InFights.SprintJumps, Is.EqualTo(1));
            Assert.That(stats.InFights.SprintStaggers, Is.EqualTo(1));
            Assert.That(stats.InFights.SprintRefused, Is.EqualTo(2));
            Assert.That(stats.Session.Stuns, Is.EqualTo(1), "the sprint's stagger is not among the stuns");
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Sprinting: time, share of the time", text);
            StringAssert.Contains("jumps over low props", text);
            StringAssert.Contains("staggers from a hit while sprinting", text);
            StringAssert.Contains("shots and skills pressed that did nothing", text);

            var old = new List<SessionEvent> { new SessionEvent(0, SessionEventTypes.SessionStart), new SessionEvent(5, SessionEventTypes.SessionEnd) };
            Assert.That(SessionSummary.Compute(old).HasSprint, Is.False, "a log from before has no sprint lines");
            StringAssert.DoesNotContain("Sprinting", SessionSummary.Format(SessionSummary.Compute(old)));
        }
    }
}
