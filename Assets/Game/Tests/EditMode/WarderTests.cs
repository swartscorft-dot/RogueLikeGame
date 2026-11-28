using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 079 (2026-11-22, "Enda en fiendetype"): the warder's rules. Its values beside the other kinds'; whom it wards
    /// (the nearest without a ward, never itself, another warder or the boss, one ward an enemy); the raising's time before a
    /// ward is on; two at most; the pause between raisings; the lessening (her hit, a bomber's blast); what breaks a ward and
    /// that the daze follows; a cancelled raising gives no daze; alone it attacks; where it stands; its word, letter, model,
    /// turns, sounds and size for the effects; the floors: off by default and then today's floors field for field, on valid
    /// over 400 seeds a floor within its rules, and the validator's rules for it; the summary with its wards in it.
    /// </summary>
    public class WarderTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static WardAlly Ally(int id, float x, float z, bool alive = true, bool never = false, bool other = false) =>
            new WardAlly { Id = id, At = new GroundPoint(x, z), Alive = alive, NeverWarded = never, WardedByAnother = other };

        static readonly GroundPoint Me = new GroundPoint(0f, 0f);

        // ---------------- Its values ----------------

        [Test]
        public void ItsValues_AreTheOrdersFirstGuesses_BesideTheOtherKinds()
        {
            var v = new WarderValues();
            var swarmer = new SwarmerValues();
            var caster = new CasterValues();
            int shots = (int)Math.Ceiling(v.Enemy.Health / (double)Placeholders.BasicShotDamage);
            Assert.That(shots, Is.InRange(5, 6), "five or six basic shots");
            Assert.That(v.Enemy.MoveSpeed, Is.EqualTo(swarmer.Enemy.MoveSpeed * 2f / 3f).Within(1e-4f), "two thirds of a swarmer's speed");
            Assert.That(v.KeepNear, Is.InRange(8f, 10f));
            Assert.That(v.KeepFar, Is.InRange(8f, 10f));
            Assert.That(v.WardRange, Is.EqualTo(9f));
            Assert.That(v.RaiseSeconds, Is.EqualTo(1f));
            Assert.That(v.WardShare, Is.EqualTo(0.6f), "60 % less");
            Assert.That(v.MostWards, Is.EqualTo(2));
            Assert.That(v.WardEvery, Is.EqualTo(4f));
            Assert.That(v.BreakRange, Is.EqualTo(14f));
            Assert.That(v.BreakDaze, Is.EqualTo(0.6f));
            Assert.That(v.Stagger.Max, Is.LessThan(new GuardValues().Stagger.Max), "a bar, smaller than a guard's");
            // Alone: the caster's bolt, whole, at half the caster's rate.
            EnemyAttackDefinition orb = CasterTactics.Find(caster.Orbs, CasterValues.BoneOrb);
            EnemyAttackDefinition bolt = v.Enemy.Attacks.Single();
            Assert.That(bolt.Name, Is.EqualTo(CasterValues.BoneOrb), "the caster's orb, so its clip, sound and effects are the orb's");
            Assert.That(bolt.Damage, Is.EqualTo(orb.Damage));
            Assert.That(bolt.Windup, Is.EqualTo(orb.Windup), "with its telegraph's time");
            Assert.That(bolt.MinDistance, Is.EqualTo(orb.MinDistance));
            Assert.That(bolt.MaxDistance, Is.EqualTo(orb.MaxDistance));
            Assert.That(bolt.Windup + bolt.Recovery, Is.EqualTo(2f * (orb.Windup + orb.Recovery)).Within(1e-4f), "half the caster's rate");
            Assert.That(bolt.Counterable, Is.False);
            var floors = new LevelValues();
            Assert.That(floors.WarderHealth, Is.EqualTo(v.Enemy.Health));
            Assert.That(floors.HealthOf(EnemyKind.Warder), Is.EqualTo(floors.WarderHealth));
        }

        [Test]
        public void ItsWord_Letter_Model_Turns_Sounds_AndSize()
        {
            Assert.That(EnemyKinds.Word(EnemyKind.Warder), Is.EqualTo("warder"));
            Assert.That(EnemyKinds.Letter(EnemyKind.Warder, 1), Is.EqualTo('w'));
            var letters = new HashSet<char>();
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind))) Assert.That(letters.Add(EnemyKinds.Letter(k, 1)), Is.True, "a letter of its own: " + k);
            EnemyModelSpec spec = EnemyModelSpec.Of(EnemyKind.Warder);
            Assert.That(spec, Is.SameAs(EnemyModelSpec.Warder));
            Assert.That(EnemyModelSpec.All, Has.Member(spec));
            Assert.That(spec.Name, Is.EqualTo(EnemyModelSpec.Caster.Name), "the caster's model, no new art");
            Assert.That(spec.Height, Is.EqualTo(EnemyModelSpec.Caster.Height));
            Assert.That(spec.Kind, Is.EqualTo(EnemyKind.Warder));
            Assert.That(AttackTurns.SortOf(EnemyKind.Warder), Is.EqualTo(TurnSort.Ranged), "its bolt waits with the shooters'");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.Loosed, EnemyKind.Warder), Is.EqualTo(PlaceholderSound.OrbLoosed), "its bolt is the caster's orb");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Warder, false), Is.EqualTo(PlaceholderSound.WindupRed));
            var sizes = new FxSizes();
            Assert.That(sizes.KindScale(EnemyKind.Warder), Is.EqualTo(sizes.KindScale(EnemyKind.Caster)));
        }

        // ---------------- Whom it wards ----------------

        [Test]
        public void ItPicks_TheNearestWithoutAWard_NeverAWarderOrTheBoss_NorOneWardedByAnother_WithinItsRange()
        {
            var v = new WarderValues();
            var allies = new List<WardAlly>
            {
                Ally(1, 7f, 0f), Ally(2, 3f, 0f, never: true), Ally(3, 4f, 0f, other: true), Ally(4, 5f, 0f), Ally(5, 2f, 0f, alive: false),
                Ally(6, 9.5f, 0f),
            };
            Assert.That(allies[WarderRules.Pick(Me, allies, null, v)].Id, Is.EqualTo(4), "the nearest alive, not a warder or the boss, not warded by another");
            Assert.That(WarderRules.NeverWarded(EnemyKind.Warder), Is.True);
            Assert.That(WarderRules.NeverWarded(EnemyKind.Boss), Is.True);
            foreach (EnemyKind k in new[] { EnemyKind.Brute, EnemyKind.Swarmer, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Bomber })
                Assert.That(WarderRules.NeverWarded(k), Is.False, k.ToString());
            var keeper = new WardKeeper();
            var changes = new List<WardChange>();
            keeper.Step(0f, Me, allies, v, changes);
            Assert.That(changes.Single().AllyId, Is.EqualTo(4));
            Assert.That(WarderRules.Pick(Me, allies, keeper, v), Is.EqualTo(0), "then the next nearest it does not hold");
            Assert.That(WarderRules.Pick(Me, new List<WardAlly> { Ally(6, 9.5f, 0f) }, null, v), Is.EqualTo(-1), "none beyond 9 m");
            Assert.That(WarderRules.Pick(Me, new List<WardAlly>(), null, v), Is.EqualTo(-1));
        }

        // ---------------- The raising and the ward on ----------------

        [Test]
        public void AWard_IsOnOnlyAfterTheRaisingsSecond_TwoAtMost_TheNextNoSoonerThanFourSecondsAfterTheLast()
        {
            var v = new WarderValues();
            var allies = new List<WardAlly> { Ally(1, 3f, 0f), Ally(2, 4f, 0f), Ally(3, 5f, 0f) };
            var keeper = new WardKeeper();
            var changes = new List<WardChange>();
            keeper.Step(10f, Me, allies, v, changes);
            Assert.That(changes.Select(c => c.Kind + " " + c.AllyId), Is.EqualTo(new[] { "Raising 1" }));
            changes.Clear();
            keeper.Step(10f + v.RaiseSeconds - 0.01f, Me, allies, v, changes);
            Assert.That(changes, Is.Empty, "not on before its second");
            Assert.That(keeper.OnCount, Is.EqualTo(0));
            keeper.Step(10f + v.RaiseSeconds, Me, allies, v, changes);
            Assert.That(changes.Select(c => c.Kind + " " + c.AllyId), Is.EqualTo(new[] { "On 1" }));
            changes.Clear();
            keeper.Step(10f + v.WardEvery - 0.01f, Me, allies, v, changes);
            Assert.That(changes, Is.Empty, "no new raising within the pause");
            keeper.Step(10f + v.WardEvery, Me, allies, v, changes);
            Assert.That(changes.Select(c => c.Kind + " " + c.AllyId), Is.EqualTo(new[] { "Raising 2" }), "the next nearest, 4 s after the last began");
            changes.Clear();
            keeper.Step(10f + v.WardEvery + v.RaiseSeconds, Me, allies, v, changes);
            Assert.That(keeper.OnCount, Is.EqualTo(2));
            changes.Clear();
            keeper.Step(100f, Me, allies, v, changes);
            Assert.That(changes, Is.Empty, "two at most");
            Assert.That(keeper.Wards.Count, Is.EqualTo(2));
            Assert.That(keeper.Holds(3), Is.False);
        }

        // ---------------- The lessening ----------------

        [Test]
        public void AWard_TakesSixtyPercentOffEveryHit_HersAndABombersBlast()
        {
            var v = new WarderValues();
            Assert.That(WarderRules.Lessened(Placeholders.BasicShotDamage, v.WardShare), Is.EqualTo(16), "her basic shot of 40: 16");
            Assert.That(WarderRules.Lessened(new BomberValues().EnemyDamage, v.WardShare), Is.EqualTo(120), "a bomber's blast of 300: 120");
            Assert.That(WarderRules.Lessened(100, 0f), Is.EqualTo(100), "no ward: the whole hit");
            Assert.That(WarderRules.Lessened(1, v.WardShare), Is.EqualTo(1), "a hit that did any does at least 1");
            Assert.That(WarderRules.Lessened(0, v.WardShare), Is.EqualTo(0));
            Assert.That(WarderRules.Lessened(5, v.WardShare), Is.EqualTo(2), "2 of 5: halves up");
        }

        // ---------------- What breaks a ward ----------------

        static (WardKeeper keeper, List<WardAlly> allies) TwoOnAndOneRaising(WarderValues v)
        {
            var allies = new List<WardAlly> { Ally(1, 3f, 0f), Ally(2, 4f, 0f), Ally(3, 5f, 0f) };
            var keeper = new WardKeeper();
            v.MostWards = 3;
            keeper.Step(0f, Me, allies, v, null);
            keeper.Step(v.WardEvery, Me, allies, v, null);
            keeper.Step(2f * v.WardEvery, Me, allies, v, null); // the third begins: being raised
            Assert.That(keeper.OnCount, Is.EqualTo(2));
            Assert.That(keeper.Wards.Count, Is.EqualTo(3));
            return (keeper, allies);
        }

        [TestCase(WardEnd.WarderKilled)]
        [TestCase(WardEnd.WarderStaggered)]
        public void ItsDeathOrItsStagger_BreaksEveryWard_TheWardedDazed_AndCancelsARaising_WithNoDaze(WardEnd why)
        {
            var v = new WarderValues();
            (WardKeeper keeper, _) = TwoOnAndOneRaising(v);
            var changes = new List<WardChange>();
            keeper.BreakAll(why, changes);
            Assert.That(keeper.Wards, Is.Empty);
            Assert.That(changes.Where(c => c.Kind == WardChangeKind.Broken).Select(c => c.AllyId).OrderBy(i => i), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(changes.Where(c => c.Kind == WardChangeKind.Broken).All(c => c.Dazes && c.Why == why), Is.True, "both dazed");
            WardChange cancelled = changes.Single(c => c.Kind == WardChangeKind.Cancelled);
            Assert.That(cancelled.AllyId, Is.EqualTo(3));
            Assert.That(cancelled.Dazes, Is.False, "a cancelled raising gives no daze");
        }

        [Test]
        public void TheWardedFartherThan14m_LosesItsWard_AndIsDazed_TheOtherKeepsIts()
        {
            var v = new WarderValues();
            var allies = new List<WardAlly> { Ally(1, 3f, 0f), Ally(2, 4f, 0f) };
            var keeper = new WardKeeper();
            keeper.Step(0f, Me, allies, v, null);
            keeper.Step(v.WardEvery, Me, allies, v, null);
            keeper.Step(v.WardEvery + v.RaiseSeconds, Me, allies, v, null);
            Assert.That(keeper.OnCount, Is.EqualTo(2));
            var changes = new List<WardChange>();
            allies[0] = Ally(1, 13.9f, 0f);
            keeper.Step(20f, Me, allies, v, changes);
            Assert.That(changes.Where(c => c.Kind != WardChangeKind.Raising), Is.Empty, "13.9 m: kept");
            allies[0] = Ally(1, 14.1f, 0f);
            changes.Clear();
            keeper.Step(21f, Me, allies, v, changes);
            WardChange broken = changes.First();
            Assert.That(broken.Kind, Is.EqualTo(WardChangeKind.Broken));
            Assert.That(broken.Why, Is.EqualTo(WardEnd.TooFar));
            Assert.That(broken.Dazes, Is.True);
            Assert.That(keeper.Holds(2), Is.True, "the other keeps its ward");
            Assert.That(keeper.Holds(1), Is.False);
        }

        [Test]
        public void AWardWhoseAllyDied_IsGone_WithNoDaze_AndARaisingTooFarIsCancelled()
        {
            var v = new WarderValues();
            var allies = new List<WardAlly> { Ally(1, 3f, 0f) };
            var keeper = new WardKeeper();
            keeper.Step(0f, Me, allies, v, null);
            keeper.Step(v.RaiseSeconds, Me, allies, v, null);
            allies[0] = Ally(1, 3f, 0f, alive: false);
            var changes = new List<WardChange>();
            keeper.Step(2f, Me, allies, v, changes);
            Assert.That(changes.Single().Kind, Is.EqualTo(WardChangeKind.Broken));
            Assert.That(changes.Single().Why, Is.EqualTo(WardEnd.AllyGone));
            Assert.That(changes.Single().Dazes, Is.False);
            Assert.That(WarderRules.Dazes(WardEnd.WentHome), Is.False);

            var other = new WardKeeper();
            var near = new List<WardAlly> { Ally(5, 3f, 0f) };
            other.Step(0f, Me, near, v, null);
            near[0] = Ally(5, 20f, 0f);
            changes.Clear();
            other.Step(0.5f, Me, near, v, changes);
            Assert.That(changes.Single().Kind, Is.EqualTo(WardChangeKind.Cancelled), "a raising whose ally went too far is cancelled");
            Assert.That(changes.Single().Dazes, Is.False);
        }

        // ---------------- Alone, and where it stands ----------------

        [Test]
        public void Alone_WithNoAllyFitForAWardAlive_ItAttacks()
        {
            Assert.That(WarderRules.Alone(new List<WardAlly>()), Is.True);
            Assert.That(WarderRules.Alone(new List<WardAlly> { Ally(1, 3f, 0f, alive: false), Ally(2, 3f, 0f, never: true) }), Is.True,
                        "a dead ally, another warder or the boss: nothing to shield");
            Assert.That(WarderRules.Alone(new List<WardAlly> { Ally(1, 3f, 0f, other: true) }), Is.False, "one warded by another is still its side");
            Assert.That(WarderRules.Alone(new List<WardAlly> { Ally(1, 30f, 0f) }), Is.False);
        }

        [Test]
        public void ItStands_BehindTheMiddleOfItsAllies_EightToTenMetresFromHer()
        {
            var v = new WarderValues();
            var her = new GroundPoint(0f, 0f);
            GroundPoint spot = WarderRules.Spot(her, new GroundPoint(0f, 20f), true, new GroundPoint(0f, 6f), v);
            Assert.That(spot.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(spot.Z, Is.EqualTo(9f).Within(1e-4f), "3 m behind allies 6 m from her");
            spot = WarderRules.Spot(her, new GroundPoint(0f, 20f), true, new GroundPoint(0f, 1f), v);
            Assert.That(spot.Z, Is.EqualTo(v.KeepNear).Within(1e-4f), "never nearer her than 8 m");
            spot = WarderRules.Spot(her, new GroundPoint(0f, 20f), true, new GroundPoint(12f, 0f), v);
            Assert.That(spot.X, Is.EqualTo(v.KeepFar).Within(1e-4f), "nor farther than 10 m: it does not flee");
            spot = WarderRules.Spot(her, new GroundPoint(3f, 4f), false, default, v);
            Assert.That(spot.DistanceTo(her), Is.EqualTo(v.KeepNear).Within(1e-4f), "with none: its distance, on its own line");
            Assert.That(spot.X / spot.Z, Is.EqualTo(0.75f).Within(1e-4f));
        }

        // ---------------- The floors ----------------

        /// <summary>
        /// Work order 079's rule with its own values: the warders on from floor 2, and the bombers off (work order 087 turned both
        /// on in the floors, the warder from floor 3 and the bomber from floor 4: ActPlanTests test the defaults).
        /// </summary>
        static LevelValues On() => new LevelValues { Warders = true, Bombers = false, LineUps = false, WarderFloors = new WarderFloorValues { FromFloor = 2 } };

        /// <summary>The same with the warders off (work order 079's "off", the default until work order 087).</summary>
        static LevelValues Off() => new LevelValues { Warders = false, Bombers = false, LineUps = false }; // work order 135 (changed deliberately): the warders against their own off on version 24's rooms, the line-ups named off

        [Test]
        public void Off_OrTheActsPlanOff_NoRoomMayHoldOne()
        {
            Assert.That(new LevelValues().Warders, Is.True, "built off (work order 079), on in the floors since work order 087");
            foreach (LevelValues v in new[] { Off(), new LevelValues { ActPlan = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, LineUps = false, UsableRooms = false } }) // work order 087: the plan off is version 17's floors, with none
            {
                for (int f = 1; f <= 5; f++)
                    foreach (RoomKind k in Enum.GetValues(typeof(RoomKind)))
                        foreach (bool first in new[] { false, true })
                            Assert.That(LevelWarders.Most(v, k, f, first, 10), Is.EqualTo(0), "off: floor " + f + " " + k);
                Assert.That(v.EliteKinds(5), Has.No.Member(EnemyKind.Warder), "never an elite");
                var fights = new List<List<EnemyGroup>> { new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 5) }, new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 5) } };
                LevelWarders.Put(fights, 1UL, 4, v);
                Assert.That(fights.SelectMany(g => g).All(g => g.Kind == EnemyKind.Swarmer && g.Count == 5), Is.True, "off, nothing is touched");
            }
        }

        [Test]
        public void On_OneARoom_FromFloor2_NeverInTheFirstRoom_OnlyWithThreeOthers()
        {
            var v = On();
            for (int f = 1; f <= 5; f++)
            {
                int want = f >= 2 ? 1 : 0;
                Assert.That(LevelWarders.Most(v, RoomKind.Combat, f, false, 3), Is.EqualTo(want), "a combat room on floor " + f);
                Assert.That(LevelWarders.Most(v, RoomKind.Exit, f, false, 3), Is.EqualTo(want), "the exit room on floor " + f);
                Assert.That(LevelWarders.Most(v, RoomKind.Combat, f, false, 2), Is.EqualTo(0), "two others: none");
                Assert.That(LevelWarders.Most(v, RoomKind.Combat, f, true, 9), Is.EqualTo(0), "never in the floor's first combat room");
                Assert.That(LevelWarders.Most(v, RoomKind.Treasure, f, false, 9), Is.EqualTo(0));
                Assert.That(LevelWarders.Most(v, RoomKind.Entrance, f, false, 9), Is.EqualTo(0));
                Assert.That(LevelWarders.Most(v, RoomKind.Passage, f, false, 9), Is.EqualTo(0));
            }
            var groups = new List<EnemyGroup> { new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 3), new EnemyGroup(EnemyKind.Swarmer, 4) };
            Assert.That(LevelWarders.Convert(groups, 1), Is.EqualTo(1));
            Assert.That(groups.Select(g => g.ToString()), Is.EqualTo(new[] { "brute x2", "swarmer x3", "swarmer x3", "warder" }), "in place of a swarmer of the largest pack");
            Assert.That(new EnemyGroup(EnemyKind.Warder).Letter, Is.EqualTo('w'));
        }

        /// <summary>
        /// The switch off is today's floors (the brief: checked under .NET with the same seeds, described and compared): the
        /// default values, and values that name the switch off, against values built with no warder field set at all (as they
        /// were before this order), every floor, 200 seeds a floor. The fingerprint classes pin the same under Unity.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Off_EveryFloorIsTheFloorWithoutTheField_FieldForField(int floor)
        {
            var before = new LevelValues { Warders = false }; // no warder number named (work order 087: the switch named off, now that it is on by default)
            var off = new LevelValues { Warders = false, WarderFloors = new WarderFloorValues { FromFloor = 1, OthersAtLeast = 0, MostInRoom = 5 } };
            for (ulong s = 0; s < 200; s++)
            {
                LevelLayout a = SharedFloors.Once(s, floor, before), b = SharedFloors.Once(s, floor, off); // work order 155: from the run's set (a batch at a time), read in this order
                Assert.That(b.Describe(), Is.EqualTo(a.Describe()), "seed " + s);
                Assert.That(a.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Warder), Is.False);
            }
        }

        [TestCase(2)]
        [TestCase(5)]
        public void On_WithNoneAllowed_IsTheFloorWithThemOff(int floor)
        {
            var off = Off();
            var none = new LevelValues { Warders = true, Bombers = false, LineUps = false, WarderFloors = new WarderFloorValues { FromFloor = 99 } }; // work order 135 (changed deliberately): against Off(), the line-ups named off
            for (ulong s = 0; s < 100; s++)
                Assert.That(SharedFloors.Once(s, floor, none).Describe(), Is.EqualTo(SharedFloors.Once(s, floor, off).Describe()), "seed " + s); // work order 155: from the run's set (a batch at a time), read in this order
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void On_FourHundredSeeds_EveryFloorIsValid_AndTheWardersKeepTheirRules(int floor)
        {
            LevelValues on = On(), off = Off();
            var perFloor = new List<int>();
            int rooms = 0, roomsWith = 0;
            for (ulong s = 0; s < 400; s++)
            {
                LevelLayout level = SharedFloors.Once(s, floor, on); // valid under these values, or it throws // work order 155: from the run's set (a batch at a time), read in this order
                List<string> faults = LevelValidator.Validate(level, on);
                Assert.That(faults, Is.Empty, "seed " + s + ": " + string.Join("; ", faults));
                LevelLayout without = SharedFloors.Once(s, floor, off); // work order 155: from the run's set (a batch at a time), read in this order
                LevelRoom first = LevelGeometry.MainPath(level).First(r => r.Kind == RoomKind.Combat);
                List<LevelRoom> fights = level.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).OrderBy(r => r.Order).ToList();
                List<LevelRoom> fightsOff = without.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).OrderBy(r => r.Order).ToList();
                Assert.That(fights.Count, Is.EqualTo(fightsOff.Count));
                int onFloor = 0;
                for (int i = 0; i < fights.Count; i++)
                {
                    LevelRoom r = fights[i], o = fightsOff[i];
                    int Of(LevelRoom room, EnemyKind k) => room.EnemyGroups.Where(g => g.Kind == k).Sum(g => g.Count);
                    int warders = Of(r, EnemyKind.Warder);
                    Assert.That(warders, Is.LessThanOrEqualTo(1), "one a room at most, seed " + s);
                    if (r == first) Assert.That(warders, Is.EqualTo(0), "never in the first room");
                    if (warders > 0) Assert.That(LevelWarders.Count(r.EnemyGroups) - warders, Is.GreaterThanOrEqualTo(3), "three others at least, seed " + s);
                    Assert.That(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Warder).All(g => g.Count == 1 && !g.Elite && !g.Boss), Is.True, "each alone");
                    foreach (EnemyKind k in new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Boss, EnemyKind.Bomber })
                        Assert.That(Of(r, k), Is.EqualTo(Of(o, k)), "seed " + s + " room " + r.Order + ": its " + k + "s as with them off");
                    Assert.That(Of(r, EnemyKind.Swarmer) + warders, Is.EqualTo(Of(o, EnemyKind.Swarmer)), "in place of a swarmer, seed " + s + " room " + r.Order);
                    rooms++;
                    if (warders > 0) roomsWith++;
                    onFloor += warders;
                }
                perFloor.Add(onFloor);
            }
            perFloor.Sort();
            TestContext.WriteLine(string.Format(s_Inv,
                "WARDERS floor {0} seeds 0-399: warders on a floor lowest {1}, median {2}, highest {3}, mean {4:0.00}; rooms holding one {5} of {6}",
                floor, perFloor[0], perFloor[perFloor.Count / 2], perFloor[perFloor.Count - 1], perFloor.Average(), roomsWith, rooms));
            if (floor == 1) Assert.That(perFloor[perFloor.Count - 1], Is.EqualTo(0), "none on floor 1");
            else Assert.That(perFloor[perFloor.Count - 1], Is.GreaterThan(0), "they appear from floor 2");
        }

        [Test]
        public void On_WithTheBombersOnToo_EveryFloorIsValid()
        {
            var both = new LevelValues { Warders = true, Bombers = true };
            for (int floor = 2; floor <= 5; floor++)
                for (ulong s = 0; s < 100; s++)
                    Assert.That(LevelValidator.Validate(SharedFloors.Once(s, floor, both), both), Is.Empty, "floor " + floor + " seed " + s); // work order 155: from the run's set (a batch at a time), read in this order
        }

        [Test]
        public void TheValidator_ReportsAWarderOnFloor1_AndOneWithTheSwitchOff()
        {
            LevelValues on = On();
            LevelLayout one = LevelGenerator.Generate(7UL, 1, on);
            Assume.That(LevelValidator.Validate(one, on), Is.Empty);
            LevelRoom room = one.Rooms.Last(r => r.Kind == RoomKind.Combat && !r.Elite);
            int i = room.EnemyGroups.FindIndex(g => g.Count == 1 && !g.Elite && !g.Boss);
            Assume.That(i, Is.GreaterThanOrEqualTo(0));
            room.EnemyGroups[i] = new EnemyGroup(EnemyKind.Warder);
            List<string> faults = LevelValidator.Validate(one, on);
            Assert.That(faults.Any(f => f.Contains("1 warders") && f.Contains("floor 1 at most 0")), string.Join("\n", faults));
            Assert.That(LevelValidator.Validate(one, Off()).Any(f => f.Contains("warders") && f.Contains("at most 0")), Is.True, "with the switch off none may be");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_CountsWardsRaisedAndBroken_ByWhy_TheDamageWardedAway_AndWardersKilled()
        {
            SessionEvent Ev(double t, string type, string name = null, int id = 7, string enemy = "warder") =>
                new SessionEvent(t, type, name) { Enemy = enemy, EnemyId = id };
            SessionEvent Broken(double t, string why, int warded)
            {
                SessionEvent e = Ev(t, SessionEventTypes.WardBroken, WarderValues.Ward);
                e.Damage = warded;
                e.AddExtra("why", why);
                return e;
            }
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake),
                Ev(2, SessionEventTypes.WardRaised, WarderValues.Ward), Ev(6, SessionEventTypes.WardRaised, WarderValues.Ward),
                Broken(7, SessionWords.WardTooFar, 24), Ev(8, SessionEventTypes.WardRaised, WarderValues.Ward),
                Broken(9, SessionWords.WarderStaggered, 48), Broken(9, SessionWords.WarderStaggered, 0),
                Ev(12, SessionEventTypes.WardRaised, WarderValues.Ward), Broken(13, SessionWords.WardAllyGone, 10),
                Ev(14, SessionEventTypes.EnemyDeath),
                new SessionEvent(20, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events)
            {
                SessionEvent back = SessionEvent.Parse(e.ToJson());
                Assert.That(back.Type, Is.EqualTo(e.Type));
                Assert.That(back.ExtraValue("why"), Is.EqualTo(e.ExtraValue("why")));
                Assert.That(back.Damage, Is.EqualTo(e.Damage));
            }
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.WardsRaised, Is.EqualTo(4));
            Assert.That(stats.WardsBroken, Is.EqualTo(3), "one whose ally fell is not broken");
            Assert.That(stats.WardsBrokenByDistance, Is.EqualTo(1));
            Assert.That(stats.WardsBrokenByStagger, Is.EqualTo(2));
            Assert.That(stats.WardsBrokenByKill, Is.EqualTo(0));
            Assert.That(stats.WardedAway, Is.EqualTo(82), "every ward's warded away, its ally's fall too");
            Assert.That(stats.WardersKilled, Is.EqualTo(1));
            string text = SessionSummary.Format(stats);
            TestContext.WriteLine(text);
            StringAssert.Contains("Warders, whole session", text);
            StringAssert.Contains("Warded away", text);
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != SessionEventTypes.WardRaised).ToList())), Does.Not.Contain("Warders, whole session"),
                        "no lines for a session without a ward");
            Assert.That(SessionWords.WardWhy((int)WardEnd.WarderKilled), Is.EqualTo(SessionWords.WarderKilled));
            Assert.That(SessionWords.WardWhy((int)WardEnd.WarderStaggered), Is.EqualTo(SessionWords.WarderStaggered));
            Assert.That(SessionWords.WardWhy((int)WardEnd.TooFar), Is.EqualTo(SessionWords.WardTooFar));
            Assert.That(SessionWords.WardWhy((int)WardEnd.AllyGone), Is.EqualTo(SessionWords.WardAllyGone));
            Assert.That(SessionWords.WardWhy((int)WardEnd.WentHome), Is.EqualTo(SessionWords.WardWentHome));
        }
    }
}
