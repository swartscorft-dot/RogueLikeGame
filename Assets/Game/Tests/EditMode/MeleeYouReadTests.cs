using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Melee you read (decision 0010; work order 035, 2026-11-20), the rules: the swarmer's, the brute's and the guard's
    /// new attacks, their areas, cycles and choices (which attack at which distance, the cooldowns, a Crush after a Drag);
    /// the turns; being stunned and pulled; what breaks a Drag; the glint's colour; the sounds, the clips, the words and
    /// what the session summary says of them.
    /// </summary>
    public class MeleeYouReadTests
    {
        const float Her = Placeholders.PlayerRadius;

        static EnemyAttackDefinition Of(EnemyAttackDefinition[] set, string name)
        {
            EnemyAttackDefinition a = MeleeAttacks.Find(set, name);
            Assert.That(a, Is.Not.Null, name);
            return a;
        }

        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);
        static readonly GroundPoint North = new GroundPoint(0f, 10f);

        static bool Hits(EnemyAttackDefinition a, float x, float z) => EnemyBrain.Hits(a, Origin, North, new GroundPoint(x, z), Her);

        // ---------------- The sets ----------------

        [Test]
        public void EachKind_HasOneOrdinaryAttack_AndOneOrTwoSkillsOnTheirOwnCooldowns()
        {
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard })
            {
                EnemyAttackDefinition[] set = MeleeAttacks.For(kind);
                Assert.That(set.Count(a => a.Ordinary), Is.EqualTo(1), kind + ": one ordinary attack");
                Assert.That(set.Count(a => !a.Ordinary), Is.InRange(1, 2), kind + ": one or two skills");
                foreach (EnemyAttackDefinition skill in set.Where(a => !a.Ordinary))
                    Assert.That(skill.Cooldown, Is.GreaterThan(0f), kind + " " + skill.Name + " has a cooldown");
                EnemyAttackDefinition ordinary = set.Single(a => a.Ordinary);
                Assert.That(ordinary.MinDistance, Is.EqualTo(0f), "in reach");
                Assert.That(ordinary.Cooldown, Is.EqualTo(0f), "a short cycle, no cooldown");
                Assert.That(ordinary.Counterable, Is.False, "red");
                Assert.That(ordinary.Damage, Is.LessThan(set.Where(a => !a.Ordinary && a.Damage > 0).Select(a => a.Damage).DefaultIfEmpty(int.MaxValue).Max()),
                            "less than the skill that hurts");
            }
            Assert.That(MeleeAttacks.For(EnemyKind.Shooter), Is.Null, "the shooter is another order's");
            Assert.That(MeleeAttacks.For(EnemyKind.Boss), Is.Null);
        }

        [Test]
        public void TheOrdinaryAttacks_StrikeOnAShortCycle_WithAShortTell()
        {
            EnemyAttackDefinition bite = Of(MeleeAttacks.Swarmer(), MeleeAttacks.Bite);
            EnemyAttackDefinition stroke = Of(MeleeAttacks.Brute(), MeleeAttacks.Stroke);
            EnemyAttackDefinition fist = Of(MeleeAttacks.Guard(), MeleeAttacks.Fist);
            Assert.That(bite.Windup + bite.Recovery, Is.EqualTo(0.5f).Within(1e-4f), "twice a second");
            Assert.That(bite.Windup, Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(stroke.Windup + stroke.Recovery, Is.EqualTo(1.1f).Within(1e-4f), "about every 1.1 s");
            Assert.That(stroke.Windup, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(fist.Windup + fist.Recovery, Is.EqualTo(1.8f).Within(1e-4f), "about every 1.8 s");
            Assert.That(fist.Windup, Is.EqualTo(0.5f).Within(1e-4f));
            foreach (EnemyAttackDefinition a in new[] { bite, stroke, fist })
                Assert.That(a.Windup, Is.LessThanOrEqualTo(0.5f), a.Name + ": the animation warns, not a pause");
        }

        [Test]
        public void TheBrute_HasNoAttackThatReachesHerFromAfar()
        {
            foreach (EnemyAttackDefinition a in MeleeAttacks.Brute())
            {
                Assert.That(a.MaxDistance, Is.LessThanOrEqualTo(6f), a.Name);
                Assert.That(a.Shape, Is.Not.EqualTo(EnemyAttackShape.Line), a.Name + ": no shockwave down a line");
            }
            Assert.That(MeleeAttacks.Find(MeleeAttacks.Brute(), "Shockwave"), Is.Null, "the Shockwave is gone with the switch on");
            Assert.That(new EnemyValues().Attacks.Any(a => a.Name == "Shockwave"), Is.True, "and kept for it off");
        }

        [Test]
        public void TheSkills_AreBlueWhereTheyStunOrPull_AndDoWhatTheirKindIsFor()
        {
            EnemyAttackDefinition leap = Of(MeleeAttacks.Swarmer(), MeleeAttacks.Leap);
            EnemyAttackDefinition bash = Of(MeleeAttacks.Brute(), MeleeAttacks.ShieldBash);
            EnemyAttackDefinition drag = Of(MeleeAttacks.Guard(), MeleeAttacks.Drag);
            EnemyAttackDefinition crush = Of(MeleeAttacks.Guard(), MeleeAttacks.Crush);
            Assert.That(leap.Move, Is.EqualTo(MeleeMove.Leap));
            Assert.That(leap.MinDistance, Is.EqualTo(4f));
            Assert.That(leap.MaxDistance, Is.EqualTo(7f));
            Assert.That(leap.Windup - leap.MoveSeconds, Is.EqualTo(0.25f).Within(1e-4f), "it crouches 0.25 s...");
            Assert.That(leap.MoveSeconds, Is.EqualTo(0.35f).Within(1e-4f), "...and hops in 0.35 s");
            Assert.That(leap.Cooldown, Is.EqualTo(4f));
            Assert.That(PoolRules.IsCounterable(leap), Is.False);

            Assert.That(bash.Move, Is.EqualTo(MeleeMove.Lunge));
            Assert.That(bash.Counterable, Is.True, "blue");
            Assert.That(bash.Stun, Is.EqualTo(0.5f));
            Assert.That(bash.Length, Is.EqualTo(5f), "a lunge of up to 5 m");
            Assert.That(bash.Windup - bash.MoveSeconds, Is.EqualTo(0.6f).Within(1e-4f), "the shield raised for 0.6 s");
            Assert.That(bash.MoveSeconds, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(bash.Cooldown, Is.EqualTo(6f));

            Assert.That(drag.Move, Is.EqualTo(MeleeMove.Drag));
            Assert.That(drag.Counterable, Is.True, "blue");
            Assert.That(drag.MinDistance, Is.EqualTo(4f));
            Assert.That(drag.MaxDistance, Is.EqualTo(9f));
            Assert.That(drag.Windup, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(drag.PullTo, Is.EqualTo(2f));
            Assert.That(drag.MoveSeconds, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(drag.Cooldown, Is.EqualTo(9f));

            Assert.That(crush.Follows, Is.EqualTo(MeleeAttacks.Drag));
            Assert.That(crush.Counterable, Is.False, "red: it only hurts");
            Assert.That(crush.Damage, Is.EqualTo(40));
            Assert.That(crush.Windup, Is.EqualTo(1.1f).Within(1e-4f));
        }

        [Test]
        public void EveryAttackFromBefore_KeepsTheNewFieldsAtTheirDefaults()
        {
            var before = new List<EnemyAttackDefinition>();
            before.AddRange(EnemyValues.DefaultAttacks());
            before.AddRange(new SwarmerValues().Enemy.Attacks);
            before.AddRange(new GuardValues().Enemy.Attacks);
            before.AddRange(new ShooterValues().Enemy.Attacks);
            foreach (EnemyAttackDefinition a in before)
            {
                Assert.That(a.Ordinary, Is.False, a.Name);
                Assert.That(a.Cooldown, Is.EqualTo(0f), a.Name);
                Assert.That(a.Move, Is.EqualTo(MeleeMove.None), a.Name);
                Assert.That(a.Stun, Is.EqualTo(0f), a.Name);
                Assert.That(a.Follows, Is.Empty, a.Name);
            }
            Assert.That(new SwarmerValues().Enemy.Attacks.Single().Name, Is.EqualTo("Lunge"), "the attacks from before stay as data");
            Assert.That(new GuardValues().Enemy.Attacks.Select(a => a.Name), Is.EqualTo(new[] { "Slam", "Sweep" }));
            Assert.That(new SwarmerValues().Melee.Select(a => a.Name), Is.EqualTo(new[] { MeleeAttacks.Bite, MeleeAttacks.Leap }));
            Assert.That(new GuardValues().Melee.Select(a => a.Name), Is.EqualTo(new[] { MeleeAttacks.Fist, MeleeAttacks.Drag, MeleeAttacks.Crush }));
        }

        // ---------------- The areas ----------------

        [Test]
        public void TheBite_ReachesAbout1Point6Metres_InFrontAndToItsSides_NotBehind()
        {
            EnemyAttackDefinition bite = Of(MeleeAttacks.Swarmer(), MeleeAttacks.Bite);
            Assert.That(Hits(bite, 0f, 1.5f), Is.True, "in front, in reach");
            Assert.That(Hits(bite, 1.2f, 0.6f), Is.True, "to its front side");
            Assert.That(Hits(bite, 0f, 1.6f + Her + 0.3f), Is.False, "out of reach");
            Assert.That(Hits(bite, 0f, -1.4f), Is.False, "behind it");
        }

        [Test]
        public void TheStroke_IsAnArcOf100Degrees_2Point2MetresLong()
        {
            EnemyAttackDefinition stroke = Of(MeleeAttacks.Brute(), MeleeAttacks.Stroke);
            Assert.That(stroke.Shape, Is.EqualTo(EnemyAttackShape.Cone));
            Assert.That(stroke.Angle, Is.EqualTo(100f));
            Assert.That(Hits(stroke, 0f, 2f), Is.True);
            Assert.That(Hits(stroke, 0f, 2.2f + Her + 0.3f), Is.False, "out of reach");
            Assert.That(Hits(stroke, 2f, -0.5f), Is.False, "out of its front");
        }

        [Test]
        public void TheFistAndTheCrush_AreArcsInFront_TheCrushLongerAndNarrower()
        {
            EnemyAttackDefinition fist = Of(MeleeAttacks.Guard(), MeleeAttacks.Fist);
            EnemyAttackDefinition crush = Of(MeleeAttacks.Guard(), MeleeAttacks.Crush);
            Assert.That(fist.Length, Is.EqualTo(2.6f));
            Assert.That(crush.Length, Is.EqualTo(3.2f));
            Assert.That(crush.Angle, Is.EqualTo(70f));
            Assert.That(Hits(fist, 0f, 2.4f), Is.True);
            Assert.That(Hits(fist, 0f, 3.5f), Is.False);
            Assert.That(Hits(crush, 0f, 3.2f), Is.True);
            Assert.That(Hits(crush, 0f, 4.2f), Is.False);
            Assert.That(Hits(crush, 2.2f, 1f), Is.False, "out of its narrow front");
            Assert.That(Hits(fist, 0f, -1.5f), Is.False, "behind");
        }

        [Test]
        public void TheLeapAndTheBash_StrikeACircleWhereTheyStop()
        {
            EnemyAttackDefinition leap = Of(MeleeAttacks.Swarmer(), MeleeAttacks.Leap);
            EnemyAttackDefinition bash = Of(MeleeAttacks.Brute(), MeleeAttacks.ShieldBash);
            Assert.That(leap.Shape, Is.EqualTo(EnemyAttackShape.Circle));
            Assert.That(bash.Shape, Is.EqualTo(EnemyAttackShape.Circle));
            // Landed 0.7 m short of her: she is bitten if she stood, missed if she stepped 2 m aside in the hop.
            Assert.That(Hits(leap, 0f, MeleeYouReadRules.LeapShort), Is.True, "she stood");
            Assert.That(Hits(leap, 2.1f, MeleeYouReadRules.LeapShort), Is.False, "she stepped aside in the 0.35 s hop");
            Assert.That(Hits(bash, 0f, 1f), Is.True);
            Assert.That(Hits(bash, 1.8f, 1f), Is.False, "she stepped aside in the 0.3 s lunge");
        }

        // ---------------- Where a move goes ----------------

        [Test]
        public void ALeap_LandsJustShortOfWhereSheStoodAsItLeftTheGround()
        {
            GroundPoint landing = MeleeYouReadRules.LeapLanding(new GroundPoint(0f, 0f), new GroundPoint(0f, 5f));
            Assert.That(landing.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(landing.Z, Is.EqualTo(5f - MeleeYouReadRules.LeapShort).Within(1e-4f));
            GroundPoint near = MeleeYouReadRules.LeapLanding(new GroundPoint(1f, 1f), new GroundPoint(1.3f, 1f));
            Assert.That(near.X, Is.EqualTo(1f), "already nearer than that: it stays");
        }

        [Test]
        public void ALunge_RunsToHer_NoFartherThan5Metres_NorThanTheWayIsClear()
        {
            Assert.That(MeleeYouReadRules.LungeLength(4f, 5f, 1f, 5f), Is.EqualTo(3f), "to her, its shield touching hers");
            Assert.That(MeleeYouReadRules.LungeLength(9f, 5f, 1f, 9f), Is.EqualTo(5f), "up to 5 m");
            Assert.That(MeleeYouReadRules.LungeLength(6f, 5f, 1f, 2.5f), Is.EqualTo(2.5f), "a wall in the way");
            Assert.That(MeleeYouReadRules.LungeLength(0.5f, 5f, 1f, 5f), Is.EqualTo(0f), "never backwards");
        }

        [Test]
        public void APull_TakesHerTo2MetresInFrontOfThePuller()
        {
            GroundPoint to = MeleeYouReadRules.PullTarget(new GroundPoint(0f, 0f), new GroundPoint(6f, 8f), 2f);
            Assert.That(to.X, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(to.Z, Is.EqualTo(1.6f).Within(1e-4f));
            GroundPoint stays = MeleeYouReadRules.PullTarget(new GroundPoint(0f, 0f), new GroundPoint(1f, 0f), 2f);
            Assert.That(stays.X, Is.EqualTo(1f), "nearer already: where she is");
        }

        [Test]
        public void ADrag_IsBrokenByAWallOrATallProp_OrTheDashsProtectedPart()
        {
            Assert.That(MeleeYouReadRules.DragBreaks(lineClear: true, inDashProtection: false), Is.False);
            Assert.That(MeleeYouReadRules.DragBreaks(lineClear: false, inDashProtection: false), Is.True, "a wall or a tall prop between");
            Assert.That(MeleeYouReadRules.DragBreaks(lineClear: true, inDashProtection: true), Is.True, "the dash's protected part");
        }

        // ---------------- Which attack, and when ----------------

        [Test]
        public void TheSwarmer_BitesInReach_LeapsFrom4To7Metres_EveryFourSeconds_AndWalksBetween()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Swarmer();
            var t = new MeleeTactics();
            Assert.That(t.Choose(0f, 1f, set).Name, Is.EqualTo(MeleeAttacks.Bite));
            Assert.That(t.Choose(0f, 3f, set), Is.Null, "between the two: it runs");
            EnemyAttackDefinition leap = t.Choose(0f, 5f, set);
            Assert.That(leap.Name, Is.EqualTo(MeleeAttacks.Leap));
            t.Began(leap, 10f);
            Assert.That(t.Choose(11f, 5f, set), Is.Null, "the leap cools down");
            Assert.That(t.Choose(13.9f, 5f, set), Is.Null);
            Assert.That(t.Choose(14f, 5f, set).Name, Is.EqualTo(MeleeAttacks.Leap), "about every 4 s");
            Assert.That(t.Choose(11f, 1f, set).Name, Is.EqualTo(MeleeAttacks.Bite), "the bite has no cooldown");
            Assert.That(t.Choose(0f, 8f, set), Is.Null, "too far for either");
        }

        [Test]
        public void TheBrute_StrokesInReach_BashesFrom3To6Metres_EverySixSeconds()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Brute();
            var t = new MeleeTactics();
            Assert.That(t.Choose(0f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Stroke));
            EnemyAttackDefinition bash = t.Choose(0f, 4.5f, set);
            Assert.That(bash.Name, Is.EqualTo(MeleeAttacks.ShieldBash));
            t.Began(bash, 0f);
            Assert.That(t.Choose(5.9f, 4.5f, set), Is.Null);
            Assert.That(t.Choose(6f, 4.5f, set).Name, Is.EqualTo(MeleeAttacks.ShieldBash));
            Assert.That(t.Choose(0f, 2.6f, set), Is.Null, "between them it walks");
            Assert.That(t.Choose(0f, 8f, set), Is.Null, "nothing from afar");
        }

        [Test]
        public void TheGuard_DragsFrom4To9Metres_FistsInReach_AndCrushes_AfterADragFirst()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Guard();
            var t = new MeleeTactics();
            EnemyAttackDefinition first = t.Choose(0f, 2f, set);
            Assert.That(first.Name, Is.EqualTo(MeleeAttacks.Crush), "a skill ready in reach comes before the fist");
            t.Began(first, 0f);
            Assert.That(t.Choose(2.3f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Fist), "the crush cooling down: the fist");
            Assert.That(t.Choose(2.3f, 2.9f, set), Is.Null, "past the fist's reach, the crush cooling down: it walks");

            EnemyAttackDefinition drag = t.Choose(3f, 6f, set);
            Assert.That(drag.Name, Is.EqualTo(MeleeAttacks.Drag));
            t.Began(drag, 3f);
            Assert.That(t.ReadyAt(first), Is.EqualTo(6f), "the crush's own cooldown still runs");
            Assert.That(t.Choose(4.3f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Crush), "it likes to follow a Drag, its cooldown or not");
            Assert.That(t.Choose(4.3f, 6f, set), Is.Null, "the drag cools down for 9 s");
            Assert.That(t.Choose(12f, 6f, set).Name, Is.EqualTo(MeleeAttacks.Drag));
        }

        [Test]
        public void AFollowUp_IsUsedOnce()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Guard();
            var t = new MeleeTactics();
            t.Began(MeleeAttacks.Find(set, MeleeAttacks.Drag), 0f);
            EnemyAttackDefinition crush = t.Choose(1.3f, 2f, set);
            Assert.That(crush.Name, Is.EqualTo(MeleeAttacks.Crush));
            t.Began(crush, 1.3f);
            Assert.That(t.Choose(2.5f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Fist), "the crush followed: no second one");

            // Long after a Drag the Crush waits for its own cooldown.
            var late = new MeleeTactics();
            late.Began(MeleeAttacks.Find(set, MeleeAttacks.Crush), 0f);
            late.Began(MeleeAttacks.Find(set, MeleeAttacks.Drag), 1f);
            Assert.That(late.Choose(1.5f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Crush), "right after the drag");
            Assert.That(late.Choose(1f + late.FollowWindow + 0.1f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Fist), "not long after");
        }

        [Test]
        public void ASkillItMayNotStart_LeavesTheOrdinaryAttack_AndAskedOnlyOfWhatWouldBeChosen()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Guard();
            var t = new MeleeTactics();
            var asked = new List<string>();
            EnemyAttackDefinition chosen = t.Choose(0f, 2f, set, a =>
            {
                asked.Add(a.Name);
                return a.Ordinary; // no turn for a skill, as a held turn says
            });
            Assert.That(chosen.Name, Is.EqualTo(MeleeAttacks.Fist), "no turn for the crush: the fist");
            Assert.That(asked, Is.EqualTo(new[] { MeleeAttacks.Crush, MeleeAttacks.Fist }), "the drag, out of its band, was not asked");
            Assert.That(t.Choose(0f, 6f, set, a => false), Is.Null, "it cannot see her: no drag");
        }

        [Test]
        public void AFreshFight_HasEverySkillReady()
        {
            EnemyAttackDefinition[] set = MeleeAttacks.Brute();
            var t = new MeleeTactics();
            t.Began(MeleeAttacks.Find(set, MeleeAttacks.ShieldBash), 0f);
            Assert.That(t.Choose(1f, 4f, set), Is.Null);
            t.Clear();
            Assert.That(t.Choose(1f, 4f, set).Name, Is.EqualTo(MeleeAttacks.ShieldBash));
            Assert.That(t.LastSkill, Is.Null);
        }

        // ---------------- The turns ----------------

        [Test]
        public void AnOrdinaryAttack_TakesNoTurn_ASkillTakesOne_AsEveryAttackFromBefore()
        {
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard })
                foreach (EnemyAttackDefinition a in MeleeAttacks.For(kind))
                    Assert.That(AttackTurns.TakesTurn(a), Is.EqualTo(!a.Ordinary), kind + " " + a.Name);
            foreach (EnemyAttackDefinition a in EnemyValues.DefaultAttacks())
                Assert.That(AttackTurns.TakesTurn(a), Is.True, "before: " + a.Name);
            Assert.That(AttackTurns.TakesTurn(null), Is.False, "no attack, no turn");

            // A heavy with a skill waits its turn; its ordinary attack never does.
            var turns = new AttackTurns();
            Assert.That(turns.Ask(1, TurnSort.Heavy, 0f), Is.True);
            turns.Began(1, TurnSort.Heavy, 0f, 2f);
            Assert.That(turns.Ask(2, TurnSort.Heavy, 0.5f), Is.True);
            turns.Began(2, TurnSort.Heavy, 0.5f, 2f);
            Assert.That(turns.Ask(3, TurnSort.Heavy, 1f), Is.False, "two heavies wind up: the third's skill waits");
        }

        // ---------------- Stunned and pulled ----------------

        [Test]
        public void AStun_LastsItsTime_ThenEnds()
        {
            var h = new HeldState();
            Assert.That(h.TryStun(10f, 0.5f), Is.True);
            Assert.That(h.IsStunned, Is.True);
            Assert.That(h.Step(10.49f), Is.EqualTo(HeldKind.None), "still stunned");
            Assert.That(h.IsHeld, Is.True);
            Assert.That(h.Step(10.5f), Is.EqualTo(HeldKind.Stunned), "it ends at its time");
            Assert.That(h.IsHeld, Is.False);
        }

        [Test]
        public void AStun_NeverStacksOrChains()
        {
            var h = new HeldState();
            Assert.That(h.TryStun(0f, 0.5f), Is.True);
            Assert.That(h.TryStun(0.2f, 0.5f), Is.False, "none while she is stunned");
            Assert.That(h.Until, Is.EqualTo(0.5f), "and it is not made longer");
            h.Step(0.5f);
            Assert.That(h.TryStun(0.6f, 0.5f), Is.False, "none just after");
            Assert.That(h.TryStun(0.5f + HeldState.StunGrace - 0.01f, 0.5f), Is.False);
            Assert.That(HeldState.StunGrace, Is.EqualTo(1.5f));
            Assert.That(h.TryStun(0.5f + HeldState.StunGrace, 0.5f), Is.True, "1.5 s after it ended, again");
            Assert.That(h.TryStun(5f, 0f), Is.False, "nothing for no time");
        }

        [Test]
        public void APull_MovesHerEvenlyOverItsTime_AndNothingElseStartsMeanwhile()
        {
            var h = new HeldState();
            Assert.That(h.TryPull(0f, new GroundPoint(0f, 8f), new GroundPoint(0f, 2f), 0.3f), Is.True);
            Assert.That(h.IsPulled, Is.True);
            Assert.That(h.PullPoint(0.15f).Z, Is.EqualTo(5f).Within(1e-4f), "half way at half its time");
            Assert.That(h.PullPoint(1f).Z, Is.EqualTo(2f).Within(1e-4f), "no farther than its end");
            Assert.That(h.TryStun(0.1f, 0.5f), Is.False, "not stunned while pulled");
            Assert.That(h.TryPull(0.1f, new GroundPoint(0f, 0f), new GroundPoint(1f, 0f), 0.3f), Is.False, "nor pulled again");
            Assert.That(h.Step(0.3f), Is.EqualTo(HeldKind.Pulled));
            Assert.That(h.TryStun(0.31f, 0.5f), Is.True, "a pull gives no grace: a stun after it may come");
        }

        [Test]
        public void DeathARestartOrANewFloor_EndWhateverHoldsHer_AndTheGrace()
        {
            var h = new HeldState();
            h.TryStun(0f, 0.5f);
            h.Clear();
            Assert.That(h.IsHeld, Is.False);
            Assert.That(h.TryStun(0.1f, 0.5f), Is.True, "a fresh start");
            h.Clear();
            h.TryPull(1f, new GroundPoint(0f, 0f), new GroundPoint(1f, 0f), 0.3f);
            h.Clear();
            Assert.That(h.IsPulled, Is.False);
        }

        [Test]
        public void NeitherHappens_DeadPausedInAChoiceOrAfterTheRunsResult()
        {
            Assert.That(MeleeYouReadRules.MayBeHeld(alive: true, untouchable: false, paused: false, choiceOpen: false), Is.True);
            Assert.That(MeleeYouReadRules.MayBeHeld(false, false, false, false), Is.False, "dead");
            Assert.That(MeleeYouReadRules.MayBeHeld(true, true, false, false), Is.False, "the run's result is in");
            Assert.That(MeleeYouReadRules.MayBeHeld(true, false, true, false), Is.False, "paused");
            Assert.That(MeleeYouReadRules.MayBeHeld(true, false, false, true), Is.False, "a choice is open");
        }

        // ---------------- The sign on the body ----------------

        [Test]
        public void TheGlint_IsBlueForWhatStoppingShotCounters_RedForWhatOnlyHurts_AndGrowsForASkill()
        {
            var blue = new HashSet<string> { MeleeAttacks.ShieldBash, MeleeAttacks.Drag };
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard })
                foreach (EnemyAttackDefinition a in MeleeAttacks.For(kind))
                {
                    Assert.That(MeleeYouReadRules.GlintIsBlue(a), Is.EqualTo(blue.Contains(a.Name)), a.Name);
                    Assert.That(MeleeYouReadRules.GlintGrows(a), Is.EqualTo(!a.Ordinary), a.Name + ": a skill's grows, an ordinary one flicks");
                }
            Assert.That(MeleeYouReadRules.GlintGrows(null), Is.False);
        }

        [Test]
        public void AnOrdinaryAttacksWindup_IsSilent_ASkillsIsRedOrBlueAsBefore()
        {
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Swarmer, false, ordinary: true), Is.Null);
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Brute, true), Is.EqualTo(PlaceholderSound.WindupBlue));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Guard, false), Is.EqualTo(PlaceholderSound.WindupRed));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.EnemyStruck, EnemyKind.Swarmer, false, ordinary: true), Is.EqualTo(PlaceholderSound.Strike),
                        "its strike has the strike's sound");
        }

        [Test]
        public void EveryNewAttack_PlaysAClipOfItsKind_WhoseStrikeLandsAsTheWindupEnds()
        {
            var expected = new Dictionary<string, string>
            {
                [MeleeAttacks.Bite] = "Attack", [MeleeAttacks.Leap] = "Attack", [MeleeAttacks.Stroke] = "Attack",
                [MeleeAttacks.ShieldBash] = "Slam", [MeleeAttacks.Fist] = "Sweep", [MeleeAttacks.Drag] = "Sweep", [MeleeAttacks.Crush] = "Slam",
            };
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard })
                foreach (EnemyAttackDefinition a in MeleeAttacks.For(kind))
                {
                    EnemyClipSpec clip = EnemyModelSpec.Of(kind).ClipForAttack(a.Name);
                    Assert.That(clip, Is.Not.Null, kind + " " + a.Name);
                    Assert.That(clip.Name, Is.EqualTo(expected[a.Name]), a.Name);
                    double most = a.Ordinary ? EnemyModelSpec.OrdinaryMaxAttackSpeed : EnemyModelSpec.MaxAttackSpeed;
                    AttackClipPlan plan = AttackClipPlan.For(a.Windup, a.Recovery, clip.Strike, clip.Length, maxSpeed: most);
                    Assert.That(plan.InWindup(a.Windup), Is.EqualTo(clip.Strike).Within(1e-9), a.Name + ": the clip's strike on the game's");
                    Assert.That(plan.WindupSpeed, Is.InRange(0.5, most), a.Name);
                }
            // The bite's whole clip plays for each bite at up to four times its speed.
            EnemyAttackDefinition bite = Of(MeleeAttacks.Swarmer(), MeleeAttacks.Bite);
            AttackClipPlan bitePlan = AttackClipPlan.For(bite.Windup, bite.Recovery, 1.233, 2.467, maxSpeed: EnemyModelSpec.OrdinaryMaxAttackSpeed);
            Assert.That(bitePlan.WindupSpeed, Is.EqualTo(4.0));
            Assert.That(bitePlan.RecoverySpeed, Is.LessThan(4.0), "the rest of the clip ends with the recovery");
        }

        [Test]
        public void StunnedAndPulled_AreWordsOverHer()
        {
            Assert.That(FeedbackRules.Word(FeedbackMoment.Stunned).Text, Is.EqualTo("STUNNED"));
            Assert.That(FeedbackRules.Word(FeedbackMoment.Pulled).Text, Is.EqualTo("PULLED"));
            Assert.That(FeedbackRules.Word(FeedbackMoment.Stunned).Tint, Is.EqualTo(FeedbackTint.Counter), "the blue of the attacks that do it");
        }

        // ---------------- What the session summary says ----------------

        static SessionEvent Ev(double t, string type, string name = null, string enemy = null, int? id = null, int? damage = null, string result = null) =>
            new SessionEvent(t, type, name) { Enemy = enemy, EnemyId = id, Damage = damage, Result = result };

        [Test]
        public void TheSummary_ListsTheNewAttacksByNameUnderEachKind_WithLandedAndAvoided_AndCountsStunsAndPulls()
        {
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake, enemy: "swarmer", id: 1),
                Ev(1.2, SessionEventTypes.EnemyAttack, MeleeAttacks.Bite, "swarmer", 1),
                Ev(1.35, SessionEventTypes.EnemyStrike, MeleeAttacks.Bite, "swarmer", 1, 5, SessionWords.Landed),
                Ev(1.7, SessionEventTypes.EnemyStrike, MeleeAttacks.Bite, "swarmer", 1, null, SessionWords.OutOfArea),
                Ev(2.2, SessionEventTypes.EnemyStrike, MeleeAttacks.Bite, "swarmer", 1, null, SessionWords.AvoidedByDash),
                Ev(2, SessionEventTypes.EnemyWake, enemy: "guard", id: 2),
                Ev(3, SessionEventTypes.EnemyStrike, MeleeAttacks.Drag, "guard", 2, null, SessionWords.Landed),
                Ev(3, SessionEventTypes.PlayerPulled, MeleeAttacks.Drag),
                Ev(4.5, SessionEventTypes.EnemyStrike, MeleeAttacks.Crush, "guard", 2, 40, SessionWords.Landed),
                Ev(5, SessionEventTypes.EnemyWake, enemy: "brute", id: 3),
                Ev(6, SessionEventTypes.EnemyStrike, MeleeAttacks.ShieldBash, "brute", 3, 15, SessionWords.Landed),
                Ev(6, SessionEventTypes.PlayerStunned, MeleeAttacks.ShieldBash),
                Ev(9, SessionEventTypes.EnemyDeath, enemy: "swarmer", id: 1),
                Ev(10, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            EnemyKindRow swarmer = stats.EnemyKinds.Single(r => r.Kind == "swarmer");
            AttackStrikes bite = swarmer.StrikesByAttack.Single(a => a.Name == MeleeAttacks.Bite);
            Assert.That(bite.Landed, Is.EqualTo(1));
            Assert.That(bite.Avoided, Is.EqualTo(2), "out of the area and by the dash");
            EnemyKindRow guard = stats.EnemyKinds.Single(r => r.Kind == "guard");
            Assert.That(guard.StrikesByAttack.Select(a => a.Name), Is.EqualTo(new[] { MeleeAttacks.Drag, MeleeAttacks.Crush }), "the drag too, though it does no damage");
            Assert.That(guard.DamageByAttack, Is.EqualTo(new[] { new KeyValuePair<string, int>(MeleeAttacks.Crush, 40) }), "the damage as before");
            Assert.That(stats.Session.Stuns, Is.EqualTo(1));
            Assert.That(stats.Session.Pulls, Is.EqualTo(1));
            Assert.That(stats.HasHeld, Is.True);
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("  " + MeleeAttacks.Bite, text);
            StringAssert.Contains("  " + MeleeAttacks.Drag, text);
            StringAssert.Contains("  " + MeleeAttacks.ShieldBash, text);
            StringAssert.Contains("Stunned / pulled", text);
            TestContext.WriteLine(text);
        }

        [Test]
        public void ASessionWithNoStunOrPull_HasNoLineForThem()
        {
            var events = new List<SessionEvent> { Ev(0, SessionEventTypes.SessionStart), Ev(5, SessionEventTypes.SessionEnd) };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.HasHeld, Is.False);
            StringAssert.DoesNotContain("Stunned / pulled", SessionSummary.Format(stats));
        }
    }
}
