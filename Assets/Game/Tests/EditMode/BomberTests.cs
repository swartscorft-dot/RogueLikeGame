using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 073 (2026-11-21, "Ny fiendetype"): the bomber's rules. Its values beside the other kinds'; the fuse's
    /// time line (lit at the range, its circle on the ground the whole fuse, the blast at its end and never before); killed
    /// before it lit, the short fuse; killed while lit, the fuse goes on; whom the blast reaches (her, the other enemies, and
    /// another bomber that lights its own full fuse and does not blow at once); the dash's protected part; how it swells and
    /// flashes; its word, letter, model, turns, sounds and size for the effects; and the floors: off by default (and then
    /// nothing of it is read), on valid over 400 seeds a floor, within its rules, and the validator's rules for it; the
    /// summary with its blasts in it.
    /// </summary>
    public class BomberTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        const float Her = Placeholders.PlayerRadius;

        // ---------------- Its values ----------------

        [Test]
        public void ItsValues_AreTheOrdersFirstGuesses_BesideTheOtherKinds()
        {
            var v = new BomberValues();
            var swarmer = new SwarmerValues();
            EnemyAttackDefinition slam = EnemyValues.DefaultAttacks().First(a => a.Name == "Slam");
            Assert.That(v.Enemy.Health, Is.EqualTo(100));
            int shots = (int)Math.Ceiling(v.Enemy.Health / (double)Placeholders.BasicShotDamage);
            Assert.That(shots, Is.InRange(2, 3), "two or three basic shots");
            Assert.That(v.Enemy.MoveSpeed, Is.EqualTo(swarmer.Enemy.MoveSpeed * 1.2f).Within(1e-4f), "faster than a swarmer by a fifth");
            Assert.That(v.Enemy.Attacks, Is.Empty, "no attack but the blast");
            Assert.That(v.Enemy.AggroRange, Is.EqualTo(swarmer.Enemy.AggroRange), "it wakes as a swarmer");
            Assert.That(v.Enemy.LeashRange, Is.EqualTo(swarmer.Enemy.LeashRange));
            Assert.That(v.FuseRange, Is.EqualTo(2.5f), "it lights within 2.5 m of her");
            Assert.That(v.BlastRadius, Is.EqualTo(3f), "a circle of 3 m");
            Assert.That(v.FuseSeconds, Is.EqualTo(0.7f), "filling over 0.7 s");
            Assert.That(v.ShortFuseSeconds, Is.EqualTo(0.45f), "killed before it lit: 0.45 s");
            Assert.That(v.ShortFuseSeconds, Is.LessThan(v.FuseSeconds));
            Assert.That(v.BlastAttack.Damage, Is.EqualTo(slam.Damage), "about a brute's hit: its Slam, a circle round it as the blast is");
            Assert.That(slam.Radius, Is.EqualTo(v.BlastRadius));
            Assert.That(v.BlastAttack.Shape, Is.EqualTo(EnemyAttackShape.Circle));
            Assert.That(v.BlastAttack.Counterable, Is.False);
            Assert.That(v.BlastAttack.Name, Is.EqualTo(BomberValues.Blast));
            var floors = new LevelValues();
            Assert.That(v.EnemyDamage, Is.GreaterThanOrEqualTo(swarmer.Enemy.Health), "it kills a practice swarmer");
            Assert.That(v.EnemyDamage, Is.GreaterThanOrEqualTo(floors.SwarmerHealth), "and a floor's");
            Assert.That(v.EnemyDamage, Is.LessThan(floors.GuardHealth), "and hurts a guard");
            Assert.That(v.EnemyDamage, Is.LessThan(new GuardValues().Enemy.Health));
            Assert.That(v.BodyRadius, Is.LessThan(swarmer.BodyRadius), "small");
            Assert.That(v.Shown, Is.LessThan(1f), "shown smaller than a swarmer");
            Assert.That(floors.BomberHealth, Is.EqualTo(v.Enemy.Health));
            Assert.That(floors.HealthOf(EnemyKind.Bomber), Is.EqualTo(floors.BomberHealth));
        }

        [Test]
        public void ItsWord_Letter_Model_Turns_Sounds_AndSize()
        {
            Assert.That(EnemyKinds.Word(EnemyKind.Bomber), Is.EqualTo("bomber"));
            Assert.That(EnemyKinds.Letter(EnemyKind.Bomber, 1), Is.EqualTo('x'));
            var letters = new HashSet<char>();
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind))) Assert.That(letters.Add(EnemyKinds.Letter(k, 1)), Is.True, "a letter of its own: " + k);
            EnemyModelSpec spec = EnemyModelSpec.Of(EnemyKind.Bomber);
            Assert.That(spec, Is.SameAs(EnemyModelSpec.Bomber));
            Assert.That(EnemyModelSpec.All, Has.Member(spec));
            Assert.That(spec.Name, Is.EqualTo(EnemyModelSpec.Swarmer.Name), "the swarmer's model, no new art");
            Assert.That(spec.Height, Is.EqualTo(EnemyModelSpec.Swarmer.Height), "as made: the look measures it as the swarmer's; it is shown smaller by its scale");
            Assert.That(spec.Kind, Is.EqualTo(EnemyKind.Bomber));
            Assert.That(spec.HasStagger, Is.False);
            Assert.That(AttackTurns.SortOf(EnemyKind.Bomber), Is.EqualTo(TurnSort.None), "it takes no turn to blow");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Bomber, false), Is.EqualTo(PlaceholderSound.WindupRed), "its fuse lit: the red wind-up");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.EnemyStruck, EnemyKind.Bomber), Is.EqualTo(PlaceholderSound.Strike), "its blast: the strike");
            var sizes = new FxSizes();
            Assert.That(sizes.KindScale(EnemyKind.Bomber), Is.EqualTo(sizes.KindScale(EnemyKind.Swarmer)), "a swarmer's size for the effects");
            Assert.That(sizes.ShardCount(EnemyKind.Bomber), Is.EqualTo(sizes.ShardCount(EnemyKind.Swarmer)));
            Assert.That(CombatFxRules.StrikeShapes(new BomberValues().BlastAttack.Shape), Is.EqualTo(new[] { FxShape.Ring, FxShape.Spark }), "its blast is a circle strike: no new shape");
        }

        // ---------------- The fuse ----------------

        [Test]
        public void TheFuse_LitAtTheRange_TheCircleOnTheGroundTheWholeFuse_TheBlastAtItsEnd()
        {
            var v = new BomberValues();
            var fuse = new BomberFuse();
            const float dt = 0.01f;
            float distance = 10f, litAt = -1f, blewAt = -1f;
            int shownSteps = 0, stepsLit = 0;
            for (int i = 0; i < 400 && blewAt < 0f; i++)
            {
                float now = i * dt;
                if (!fuse.IsLit)
                {
                    distance -= v.Enemy.MoveSpeed * dt; // it runs at her...
                    if (BomberRules.Lights(true, true, true, distance, v) && fuse.Light(now, FuseCause.Range, v)) litAt = now;
                    else Assert.That(distance, Is.GreaterThan(v.FuseRange), "it lights the moment it is within the range");
                    continue;
                }
                stepsLit++;
                if (fuse.Due(now))
                {
                    Assert.That(fuse.Blow(now), Is.True);
                    blewAt = now;
                    continue;
                }
                Assert.That(fuse.IsBurning, Is.True, "its circle is on the ground at " + now.ToString("0.00", s_Inv));
                Assert.That(fuse.Progress(now), Is.EqualTo((now - litAt) / v.FuseSeconds).Within(1e-4f), "and fills evenly");
                shownSteps++;
            }
            TestContext.WriteLine(string.Format(s_Inv, "BOMBERFUSE lit at {0:0.00} s, {1:0.00} m from her; blew at {2:0.00} s", litAt, distance, blewAt));
            Assert.That(litAt, Is.GreaterThan(0f));
            Assert.That(distance, Is.LessThanOrEqualTo(v.FuseRange).And.GreaterThan(v.FuseRange - v.Enemy.MoveSpeed * dt - 1e-4f));
            Assert.That(blewAt - litAt, Is.GreaterThanOrEqualTo(v.FuseSeconds - 1e-4f), "never before its whole fuse");
            Assert.That(blewAt - litAt, Is.LessThan(v.FuseSeconds + dt + 1e-4f), "and at its end");
            Assert.That(shownSteps, Is.EqualTo(stepsLit - 1), "the circle on the ground every step from the lighting to the blast");
            Assert.That(fuse.IsBurning, Is.False);
            Assert.That(fuse.Progress(blewAt + 1f), Is.EqualTo(1f));
            Assert.That(fuse.Blow(blewAt + 1f), Is.False, "it blows once");
        }

        [Test]
        public void ItLightsOnly_Alive_Awake_SheAlive_AndWithinTheRange()
        {
            var v = new BomberValues();
            Assert.That(BomberRules.Lights(true, true, true, v.FuseRange, v), Is.True);
            Assert.That(BomberRules.Lights(true, true, true, v.FuseRange + 0.01f, v), Is.False, "farther");
            Assert.That(BomberRules.Lights(false, true, true, 1f, v), Is.False, "dead (it lights its short fuse then, by its death)");
            Assert.That(BomberRules.Lights(true, false, true, 1f, v), Is.False, "asleep at home");
            Assert.That(BomberRules.Lights(true, true, false, 1f, v), Is.False, "she is dead");
        }

        [Test]
        public void KilledBeforeItLit_TheShortFuse_AndKilledWhileLit_TheFuseGoesOn()
        {
            var v = new BomberValues();
            var dropped = new BomberFuse();
            Assert.That(dropped.Light(5f, FuseCause.Dropped, v), Is.True);
            Assert.That(dropped.Seconds, Is.EqualTo(v.ShortFuseSeconds));
            Assert.That(dropped.BlowAt, Is.EqualTo(5f + v.ShortFuseSeconds).Within(1e-5f));
            Assert.That(dropped.Due(5f + v.ShortFuseSeconds - 0.01f), Is.False, "the circle is on the ground its whole short fuse");
            Assert.That(dropped.Due(5f + v.ShortFuseSeconds), Is.True);

            var lit = new BomberFuse();
            Assert.That(lit.Light(2f, FuseCause.Range, v), Is.True);
            // Killed at 2.3 s: its death would light the short fuse, but it is lit already.
            Assert.That(lit.Light(2.3f, FuseCause.Dropped, v), Is.False, "the fuse goes on");
            Assert.That(lit.Cause, Is.EqualTo(FuseCause.Range));
            Assert.That(lit.BlowAt, Is.EqualTo(2f + v.FuseSeconds).Within(1e-5f), "at the time it had");
            Assert.That(lit.Light(2.4f, FuseCause.Chain, v), Is.False, "another blast changes nothing either");
            Assert.That(BomberRules.FuseOf(FuseCause.Chain, v), Is.EqualTo(v.FuseSeconds), "a chain's is a full fuse");
            Assert.That(BomberRules.FuseOf(FuseCause.Range, v), Is.EqualTo(v.FuseSeconds));
        }

        [Test]
        public void TheBlast_HurtsWhoIsInItsCircle_ItsOwnSideToo_AndLightsAnotherBomber_WhichDoesNotBlowAtOnce()
        {
            var v = new BomberValues();
            var centre = new GroundPoint(0f, 0f);
            // Her: inside the circle (touching it counts, her footprint in it), outside, and in the dash's protected part.
            Assert.That(BomberRules.InBlast(centre, v, new GroundPoint(v.BlastRadius + Her - 0.01f, 0f), Her), Is.True, "touching its edge");
            Assert.That(BomberRules.InBlast(centre, v, new GroundPoint(v.BlastRadius + Her + 0.01f, 0f), Her), Is.False, "stepped out");
            Assert.That(BomberRules.HurtsHer(true, false), Is.True);
            Assert.That(BomberRules.HurtsHer(true, true), Is.False, "the dash's frames: unhurt, as from any strike");
            Assert.That(BomberRules.HurtsHer(false, false), Is.False);
            Assert.That(BomberRules.HerDamage(v, 1f), Is.EqualTo(v.BlastAttack.Damage), "the practice room's: all of it");
            Assert.That(BomberRules.HerDamage(v, 0.6f), Is.EqualTo(12), "a floor 2's: 0.6 of 20");

            // The others.
            Assert.That(BomberRules.What(true, true, false, false), Is.EqualTo(BlastEffect.Hurts), "an enemy in it");
            Assert.That(BomberRules.What(false, true, false, false), Is.EqualTo(BlastEffect.Nothing), "outside");
            Assert.That(BomberRules.What(true, false, false, false), Is.EqualTo(BlastEffect.Nothing), "dead already");
            Assert.That(BomberRules.What(true, true, true, false), Is.EqualTo(BlastEffect.Lights), "another bomber: lit");
            Assert.That(BomberRules.What(true, false, true, false), Is.EqualTo(BlastEffect.Lights), "a dead one never lit cannot be (its death lights it), and would be lit");
            Assert.That(BomberRules.What(true, true, true, true), Is.EqualTo(BlastEffect.Nothing), "one lit already burns on");

            // A chain: the second lights at the first's blast, with its full fuse, and blows a whole fuse later, not at once.
            var first = new BomberFuse();
            var second = new BomberFuse();
            first.Light(0f, FuseCause.Range, v);
            float blast = first.BlowAt;
            Assert.That(first.Blow(blast), Is.True);
            Assert.That(second.Light(blast, FuseCause.Chain, v), Is.True);
            Assert.That(second.Due(blast), Is.False, "never an instant chain");
            Assert.That(second.Due(blast + v.FuseSeconds - 0.01f), Is.False);
            Assert.That(second.Due(blast + v.FuseSeconds), Is.True, "one fuse after the first");
        }

        [Test]
        public void WhileItsFuseBurns_ItSwells_AndFlashesFasterAndFaster()
        {
            var v = new BomberValues();
            Assert.That(BomberRules.Swell(0f, v), Is.EqualTo(1f));
            Assert.That(BomberRules.Swell(1f, v), Is.EqualTo(v.SwellMost).Within(1e-5f));
            float before = 1f;
            for (int i = 1; i <= 10; i++)
            {
                float s = BomberRules.Swell(i / 10f, v);
                Assert.That(s, Is.GreaterThan(before));
                before = s;
            }
            Assert.That(BomberRules.Swell(0.75f, v) - BomberRules.Swell(0.5f, v), Is.GreaterThan(BomberRules.Swell(0.25f, v) - BomberRules.Swell(0f, v)), "faster and faster");
            Assert.That(BomberRules.FlashPeriod(0f, v), Is.EqualTo(v.FlashFirst).Within(1e-5f));
            Assert.That(BomberRules.FlashPeriod(1f, v), Is.EqualTo(v.FlashLast).Within(1e-5f));
            float fuse = v.FuseSeconds;
            float firstHalf = BomberRules.Flashes(fuse / 2f, fuse, v), all = BomberRules.Flashes(fuse, fuse, v);
            Assert.That(all - firstHalf, Is.GreaterThan(firstHalf * 1.5f), "more flashes in the second half of the fuse than in the first");
            // Its derivative is one over the period: counted by small steps, the same.
            double counted = 0;
            const int steps = 7000;
            for (int i = 0; i < steps; i++) counted += (fuse / steps) / BomberRules.FlashPeriod((i + 0.5f) / steps, v);
            Assert.That(all, Is.EqualTo((float)counted).Within(0.01f));
            Assert.That(BomberRules.FlashOn(0f, fuse, v), Is.True, "lit up as it is lit");
            int changes = 0;
            bool last = true;
            for (int i = 0; i <= 700; i++)
            {
                bool on = BomberRules.FlashOn(i * fuse / 700f, fuse, v);
                if (on != last) changes++;
                last = on;
            }
            TestContext.WriteLine(string.Format(s_Inv, "BOMBERFLASH {0:0.0} flashes over a fuse of {1} s ({2:0.0} in its first half), {3} changes", all, fuse, firstHalf, changes));
            Assert.That(changes, Is.GreaterThanOrEqualTo(2 * (int)Math.Floor(all) - 1));
        }

        // ---------------- The floors ----------------

        /// <summary>
        /// Work order 073's rule with its own values: the bombers on from floor 2, and the warders off (work order 087 turned both
        /// on in the floors, the bomber from floor 4 and the warder from floor 3: ActPlanTests test the defaults).
        /// </summary>
        static LevelValues On() => new LevelValues { Bombers = true, Warders = false, BomberFloors = new BomberFloorValues { FromFloor = 2, MostInRoom = new[] { 1, 1, 2 } } }; // 087's follow-up: 073's caps counted from floor 2

        /// <summary>The same with the bombers off (work order 073's "off", the default until work order 087).</summary>
        static LevelValues Off() => new LevelValues { Bombers = false, Warders = false };

        [Test]
        public void Off_OrTheActsPlanOff_NoRoomMayHoldOne()
        {
            Assert.That(new LevelValues().Bombers, Is.True, "built off (work order 073), on in the floors since work order 087");
            foreach (LevelValues v in new[] { Off(), new LevelValues { ActPlan = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, UsableRooms = false } }) // work order 087: the plan off is version 17's floors, with none
            {
                for (int f = 1; f <= 5; f++)
                    foreach (RoomKind k in Enum.GetValues(typeof(RoomKind)))
                        foreach (bool first in new[] { false, true })
                            Assert.That(LevelBombers.Most(v, k, f, first), Is.EqualTo(0), "off: floor " + f + " " + k);
                Assert.That(v.EliteKinds(5), Has.No.Member(EnemyKind.Bomber), "never an elite");
                var fights = new List<List<EnemyGroup>> { new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 4) }, new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 4) } };
                LevelBombers.Put(fights, 1UL, 4, v);
                Assert.That(fights.SelectMany(g => g).All(g => g.Kind == EnemyKind.Swarmer && g.Count == 4), Is.True, "off, nothing is touched");
            }
        }

        [Test]
        public void On_TheMostARoom_FromFloor2_NeverInTheFirstRoom_OneMoreInTheExit()
        {
            var v = On();
            int[] combat = { 0, 1, 1, 2, 2 }, exit = { 0, 2, 2, 3, 3 };
            for (int f = 1; f <= 5; f++)
            {
                Assert.That(LevelBombers.Most(v, RoomKind.Combat, f, false), Is.EqualTo(combat[f - 1]), "a combat room on floor " + f);
                Assert.That(LevelBombers.Most(v, RoomKind.Exit, f, false), Is.EqualTo(exit[f - 1]), "the exit room on floor " + f);
                Assert.That(LevelBombers.Most(v, RoomKind.Combat, f, true), Is.EqualTo(0), "never in the floor's first combat room");
                Assert.That(LevelBombers.Most(v, RoomKind.Treasure, f, false), Is.EqualTo(0));
                Assert.That(LevelBombers.Most(v, RoomKind.Entrance, f, false), Is.EqualTo(0));
                Assert.That(LevelBombers.Most(v, RoomKind.Passage, f, false), Is.EqualTo(0));
            }
        }

        [Test]
        public void EachTakesThePlaceOfASwarmer_OfTheLargestPack_AndAloneAtItsPoint()
        {
            var groups = new List<EnemyGroup>
            {
                new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 3), new EnemyGroup(EnemyKind.Swarmer, 4),
            };
            Assert.That(LevelBombers.Convert(groups, 2), Is.EqualTo(2));
            Assert.That(groups.Select(g => g.ToString()), Is.EqualTo(new[] { "brute x2", "swarmer x2", "swarmer x3", "bomber", "bomber" }),
                        "the pack of four, then of two as large the first");
            var few = new List<EnemyGroup> { new EnemyGroup(EnemyKind.Shooter), new EnemyGroup(EnemyKind.Swarmer, 1) };
            Assert.That(LevelBombers.Convert(few, 3), Is.EqualTo(1), "no swarmer left, no more");
            Assert.That(few.Select(g => g.ToString()), Is.EqualTo(new[] { "shooter", "bomber" }));
            Assert.That(new EnemyGroup(EnemyKind.Bomber).Letter, Is.EqualTo('x'));
        }

        // With the switch on but no floor allowed to hold one, every floor is exactly the floor with it off: the bombers'
        // stream disturbs nothing else.
        [TestCase(2)]
        [TestCase(5)]
        public void On_WithNoneAllowed_IsTheFloorWithThemOff(int floor)
        {
            var off = Off();
            var none = new LevelValues { Bombers = true, Warders = false, BomberFloors = new BomberFloorValues { FromFloor = 99 } };
            for (ulong s = 0; s < 100; s++)
                Assert.That(LevelGenerator.Generate(s, floor, none).Describe(), Is.EqualTo(LevelGenerator.Generate(s, floor, off).Describe()), "seed " + s);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void On_FourHundredSeeds_EveryFloorIsValid_AndTheBombersKeepTheirRules(int floor)
        {
            LevelValues on = On(), off = Off();
            var perFloor = new List<int>();
            int rooms = 0, roomsWith = 0;
            for (ulong s = 0; s < 400; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, on); // valid under these values, or it throws
                List<string> faults = LevelValidator.Validate(level, on);
                Assert.That(faults, Is.Empty, "seed " + s + ": " + string.Join("; ", faults));
                LevelLayout without = LevelGenerator.Generate(s, floor, off);
                LevelRoom first = LevelGeometry.MainPath(level).First(r => r.Kind == RoomKind.Combat);
                List<LevelRoom> fights = level.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).OrderBy(r => r.Order).ToList();
                List<LevelRoom> fightsOff = without.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).OrderBy(r => r.Order).ToList();
                Assert.That(fights.Count, Is.EqualTo(fightsOff.Count));
                int onFloor = 0;
                for (int i = 0; i < fights.Count; i++)
                {
                    LevelRoom r = fights[i], o = fightsOff[i];
                    int bombers = r.EnemyGroups.Where(g => g.Kind == EnemyKind.Bomber).Sum(g => g.Count);
                    Assert.That(bombers, Is.LessThanOrEqualTo(LevelBombers.Most(on, r.Kind, floor, r == first)), "seed " + s + " room " + r.Order);
                    if (r == first) Assert.That(bombers, Is.EqualTo(0), "never in the first room");
                    Assert.That(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Bomber).All(g => g.Count == 1 && !g.Elite && !g.Boss), Is.True, "each alone");
                    // The very groups of the floor with them off, but for the swarmers they took the places of.
                    int Of(LevelRoom room, EnemyKind k) => room.EnemyGroups.Where(g => g.Kind == k).Sum(g => g.Count);
                    foreach (EnemyKind k in new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Boss })
                        Assert.That(Of(r, k), Is.EqualTo(Of(o, k)), "seed " + s + " room " + r.Order + ": its " + k + "s as with them off");
                    Assert.That(Of(r, EnemyKind.Swarmer) + bombers, Is.EqualTo(Of(o, EnemyKind.Swarmer)), "each in place of a swarmer, seed " + s + " room " + r.Order);
                    Assert.That(r.EnemyGroups.Count(g => g.Elite), Is.EqualTo(o.EnemyGroups.Count(g => g.Elite)));
                    rooms++;
                    if (bombers > 0) roomsWith++;
                    onFloor += bombers;
                }
                perFloor.Add(onFloor);
                if (s == 0 && floor == 4) TestContext.WriteLine(LevelMap.Draw(level));
            }
            perFloor.Sort();
            TestContext.WriteLine(string.Format(s_Inv,
                "BOMBERS floor {0} seeds 0-399: bombers on a floor lowest {1}, median {2}, highest {3}, mean {4:0.00}; rooms holding one {5} of {6}",
                floor, perFloor[0], perFloor[perFloor.Count / 2], perFloor[perFloor.Count - 1], perFloor.Average(), roomsWith, rooms));
            if (floor == 1) Assert.That(perFloor[perFloor.Count - 1], Is.EqualTo(0), "none on floor 1");
            else Assert.That(perFloor[perFloor.Count - 1], Is.GreaterThan(0), "they appear from floor 2");
        }

        [Test]
        public void TheValidator_ReportsABomberOnFloor1_InTheFirstRoom_TooMany_AndOneWithTheSwitchOff()
        {
            LevelValues on = On();
            LevelLayout one = LevelGenerator.Generate(7UL, 1, on);
            Assume.That(LevelValidator.Validate(one, on), Is.Empty);
            LevelRoom room = one.Rooms.Last(r => r.Kind == RoomKind.Combat && !r.Elite);
            int i = room.EnemyGroups.FindIndex(g => g.Count == 1 && g.Kind == EnemyKind.Swarmer);
            if (i < 0) i = room.EnemyGroups.FindIndex(g => g.Count == 1 && !g.Elite && !g.Boss);
            Assume.That(i, Is.GreaterThanOrEqualTo(0));
            room.EnemyGroups[i] = new EnemyGroup(EnemyKind.Bomber);
            List<string> faults = LevelValidator.Validate(one, on);
            Assert.That(faults.Any(f => f.Contains("1 bombers, floor 1 at most 0")), string.Join("\n", faults));

            LevelLayout three = LevelGenerator.Generate(7UL, 3, on);
            Assume.That(LevelValidator.Validate(three, on), Is.Empty);
            LevelRoom first = LevelGeometry.MainPath(three).First(r => r.Kind == RoomKind.Combat);
            int j = first.EnemyGroups.FindIndex(g => g.Count == 1 && !g.Elite && !g.Boss);
            Assume.That(j, Is.GreaterThanOrEqualTo(0));
            first.EnemyGroups[j] = new EnemyGroup(EnemyKind.Bomber);
            faults = LevelValidator.Validate(three, on);
            Assert.That(faults.Any(f => f.Contains("bombers, floor 3 at most 0") && f.Contains("first combat room")), string.Join("\n", faults));
            Assert.That(LevelValidator.Validate(three, Off()).Any(f => f.Contains("bombers, floor 3 at most 0")), Is.True, "with the switch off none may be");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_CountsTheBlasts_TheirHitsOnHer_AndTheEnemiesKilledByThem()
        {
            SessionEvent Ev(double t, string type, string name = null, int id = 7, string enemy = "bomber") =>
                new SessionEvent(t, type, name) { Enemy = enemy, EnemyId = id };
            SessionEvent Fuse(double t, int id, string how)
            {
                SessionEvent e = Ev(t, SessionEventTypes.EnemyAttack, BomberValues.Blast, id);
                e.AddExtra("fuse", how);
                return e;
            }
            SessionEvent Blast(double t, int id, int enemies, int killed)
            {
                SessionEvent e = Ev(t, SessionEventTypes.BomberBlast, BomberValues.Blast, id);
                e.Damage = enemies * 300;
                e.AddExtra("enemies", enemies.ToString(s_Inv));
                e.AddExtra("killed", killed.ToString(s_Inv));
                return e;
            }
            var landed = Ev(2.7, SessionEventTypes.EnemyStrike, BomberValues.Blast);
            landed.Result = SessionWords.Landed;
            landed.Damage = 20;
            var missed = Ev(4.2, SessionEventTypes.EnemyStrike, BomberValues.Blast, 8);
            missed.Result = SessionWords.OutOfArea;
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake), Ev(1, SessionEventTypes.EnemyWake, id: 8),
                Ev(1, SessionEventTypes.EnemyWake, id: 9, enemy: "swarmer"), Ev(1, SessionEventTypes.EnemyWake, id: 10, enemy: "swarmer"),
                Fuse(2, 7, SessionWords.FuseAtHer),
                landed, Blast(2.7, 7, 2, 2),
                Fuse(2.7, 8, SessionWords.FuseChain),
                Ev(2.71, SessionEventTypes.EnemyDeath), Ev(2.71, SessionEventTypes.EnemyDeath, id: 9, enemy: "swarmer"), Ev(2.71, SessionEventTypes.EnemyDeath, id: 10, enemy: "swarmer"),
                missed, Blast(3.4, 8, 0, 0),
                Ev(3.41, SessionEventTypes.EnemyDeath, id: 8),
                new SessionEvent(6, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events)
            {
                SessionEvent back = SessionEvent.Parse(e.ToJson());
                Assert.That(back.Type, Is.EqualTo(e.Type));
                Assert.That(back.ExtraValue("killed"), Is.EqualTo(e.ExtraValue("killed")));
            }
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.BomberBlasts, Is.EqualTo(2));
            Assert.That(stats.BomberBlastsOnHer, Is.EqualTo(1));
            Assert.That(stats.BlastEnemiesHurt, Is.EqualTo(2));
            Assert.That(stats.BlastKills, Is.EqualTo(2), "the enemies killed by a bomber's blast");
            EnemyKindRow bomber = stats.EnemyKinds.Single(r => r.Kind == "bomber");
            Assert.That(bomber.Kills, Is.EqualTo(2));
            Assert.That(bomber.DamageTaken, Is.EqualTo(20));
            Assert.That(bomber.StrikesByAttack.Single().Name, Is.EqualTo(BomberValues.Blast));
            Assert.That(bomber.StrikesByAttack.Single().Avoided, Is.EqualTo(1));
            string text = SessionSummary.Format(stats);
            TestContext.WriteLine(text);
            StringAssert.Contains("Bombers, whole session", text);
            StringAssert.Contains("Enemies killed by a blast", text);
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != SessionEventTypes.BomberBlast).ToList())), Does.Not.Contain("Bombers, whole session"),
                        "no lines for a session without a blast");
            Assert.That(SessionWords.Fuse((int)FuseCause.Range), Is.EqualTo(SessionWords.FuseAtHer));
            Assert.That(SessionWords.Fuse((int)FuseCause.Dropped), Is.EqualTo(SessionWords.FuseDropped));
            Assert.That(SessionWords.Fuse((int)FuseCause.Chain), Is.EqualTo(SessionWords.FuseChain));
        }
    }
}
