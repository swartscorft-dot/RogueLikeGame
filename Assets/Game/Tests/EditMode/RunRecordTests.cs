using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 018 (2026-11-20): the run record the HUD and the end screens read (after a floor, a reward, a chest,
    /// a kill, death and a cleared act), the room wording, the clock, the red edge's numbers and the end's slow-down,
    /// and the settings' override rule.
    /// </summary>
    public class RunRecordTests
    {
        // ---------------- The run record ----------------

        [Test]
        public void ANewRecord_HasTheSeed_AndNothingYet()
        {
            var r = new RunRecord("1A2B3C4D", 5);
            Assert.That(r.Seed, Is.EqualTo("1A2B3C4D"));
            Assert.That(r.FloorsPerAct, Is.EqualTo(5));
            Assert.That(r.Floor, Is.EqualTo(0));
            Assert.That(r.Room, Is.EqualTo(-1));
            Assert.That(r.RoomText, Is.EqualTo(""));
            Assert.That(r.Kills, Is.EqualTo(0));
            Assert.That(r.Skills, Is.Empty);
            Assert.That(r.Upgrades, Is.Empty);
            Assert.That(r.SkillsText, Is.EqualTo("none"));
            Assert.That(r.UpgradesText, Is.EqualTo("none"));
            Assert.That(r.Outcome, Is.EqualTo(RunOutcome.Going));
            Assert.That(r.IsOver, Is.False);
        }

        [Test]
        public void AfterAFloorAndARoom_ItHoldsWhereSheIs_AndANewFloorForgetsTheRoom()
        {
            var r = new RunRecord("SEED0001", 5);
            r.EnterFloor(1, 6);
            Assert.That(r.FloorText, Is.EqualTo("Floor 1 of 5"));
            Assert.That(r.RoomText, Is.EqualTo(""), "no room yet");
            r.EnterRoom(0, RoomKind.Entrance);
            Assert.That(r.RoomText, Is.EqualTo("Entrance"));
            r.EnterRoom(2, RoomKind.Combat);
            Assert.That(r.Room, Is.EqualTo(2));
            Assert.That(r.RoomText, Is.EqualTo("Room 3 of 6"));

            r.EnterFloor(2, 7);
            Assert.That(r.Floor, Is.EqualTo(2));
            Assert.That(r.Rooms, Is.EqualTo(7));
            Assert.That(r.Room, Is.EqualTo(-1), "a new floor: the room is not known until she is in one");
            r.EnterRoom(6, RoomKind.Exit);
            Assert.That(r.RoomText, Is.EqualTo("Exit room"));
        }

        [Test]
        public void RewardsAndChests_AreKeptInOrder_TheSameUpgradesCountedTogether()
        {
            var r = new RunRecord("S", 5);
            r.TakeSkill("Fan Fire");
            r.TakeSkill("Long Shot");
            r.TakeUpgrade("+10 hp");
            r.TakeUpgrade("+5% dmg");
            r.TakeUpgrade("+10 hp");
            Assert.That(r.Skills, Is.EqualTo(new[] { "Fan Fire", "Long Shot" }));
            Assert.That(r.SkillsText, Is.EqualTo("Fan Fire, Long Shot"));
            Assert.That(r.Upgrades, Is.EqualTo(new[] { "+10 hp", "+5% dmg", "+10 hp" }));
            Assert.That(r.ChestsOpened, Is.EqualTo(3));
            Assert.That(r.UpgradesText, Is.EqualTo("+10 hp x2, +5% dmg"));
        }

        [Test]
        public void Kills_AreCounted_AlsoJustAfterTheEnd()
        {
            var r = new RunRecord("S", 5);
            r.Kill();
            r.Kill();
            Assert.That(r.Kills, Is.EqualTo(2));
            r.End(RunOutcome.ActCleared, 100f, 50, 100);
            r.Kill(); // the exit room's last enemies report their deaths on the frames after
            Assert.That(r.Kills, Is.EqualTo(3));
        }

        [Test]
        public void Death_FreezesTheRecord_WhereAndWhenSheDied()
        {
            var r = new RunRecord("S", 5);
            r.EnterFloor(3, 6);
            r.EnterRoom(3, RoomKind.Treasure);
            r.SetTime(61.5f);
            Assert.That(r.Seconds, Is.EqualTo(61.5f));
            r.End(RunOutcome.Died, 62f, 0, 110);
            Assert.That(r.Outcome, Is.EqualTo(RunOutcome.Died));
            Assert.That(r.IsOver, Is.True);
            Assert.That(r.Seconds, Is.EqualTo(62f));
            Assert.That(r.HealthLeft, Is.EqualTo(0));
            Assert.That(r.MaxHealth, Is.EqualTo(110));

            // Nothing changes it after: not the time, not a floor, not a reward, not a second end.
            r.SetTime(99f);
            r.EnterFloor(4, 6);
            r.EnterRoom(1, RoomKind.Combat);
            r.TakeSkill("Fan Fire");
            r.TakeUpgrade("+10 hp");
            r.End(RunOutcome.Left, 120f, 50, 100);
            Assert.That(r.Seconds, Is.EqualTo(62f));
            Assert.That(r.FloorText, Is.EqualTo("Floor 3 of 5"));
            Assert.That(r.RoomText, Is.EqualTo("Treasure room"));
            Assert.That(r.Skills, Is.Empty);
            Assert.That(r.Upgrades, Is.Empty);
            Assert.That(r.Outcome, Is.EqualTo(RunOutcome.Died));
        }

        [Test]
        public void AClearedAct_KeepsTheHealthLeft_AndTheTime()
        {
            var r = new RunRecord("S", 5);
            r.EnterFloor(5, 8);
            r.EnterRoom(7, RoomKind.Exit);
            r.End(RunOutcome.ActCleared, 754.25f, 64, 120);
            Assert.That(r.Outcome, Is.EqualTo(RunOutcome.ActCleared));
            Assert.That(r.HealthLeft, Is.EqualTo(64));
            Assert.That(r.MaxHealth, Is.EqualTo(120));
            Assert.That(RunWords.Clock(r.Seconds), Is.EqualTo("12:34"));
            Assert.That(r.FloorText, Is.EqualTo("Floor 5 of 5"));
        }

        [Test]
        public void GoingOn_IsNotAnEnd()
        {
            var r = new RunRecord("S", 5);
            r.End(RunOutcome.Going, 10f, 1, 1);
            Assert.That(r.IsOver, Is.False);
        }

        // ---------------- Words ----------------

        [TestCase(RoomKind.Entrance, 0, 6, "Entrance")]
        [TestCase(RoomKind.Combat, 1, 6, "Room 2 of 6")]
        [TestCase(RoomKind.Combat, 2, 6, "Room 3 of 6")]
        [TestCase(RoomKind.Treasure, 3, 6, "Treasure room")]
        [TestCase(RoomKind.Exit, 5, 6, "Exit room")]
        public void TheRoom_InWords(RoomKind kind, int order, int rooms, string words) =>
            Assert.That(RunWords.Room(kind, order, rooms), Is.EqualTo(words));

        [TestCase(0, "Room clear")]
        [TestCase(1, "1 enemy left")]
        [TestCase(4, "4 enemies left")]
        public void TheEnemiesLeft_InWords(int alive, string words) => Assert.That(RunWords.EnemiesLeft(alive), Is.EqualTo(words));

        [Test]
        public void TheClock()
        {
            Assert.That(RunWords.Clock(0f), Is.EqualTo("0:00"));
            Assert.That(RunWords.Clock(7.9f), Is.EqualTo("0:07"));
            Assert.That(RunWords.Clock(754.25f), Is.EqualTo("12:34"));
            Assert.That(RunWords.Clock(7.35f, tenths: true), Is.EqualTo("0:07.3"));
            Assert.That(RunWords.Clock(59.99f, tenths: true), Is.EqualTo("0:59.9"));
            Assert.That(RunWords.Clock(-3f), Is.EqualTo("0:00"));
            Assert.That(RunWords.Floor(2, 5), Is.EqualTo("Floor 2 of 5"));
        }

        // ---------------- The red edge and the end's slow-down ----------------

        [Test]
        public void TheHurtFlash_IsStrongerForABiggerHit_AndFades()
        {
            Assert.That(HudRules.HurtFlash(0, 100), Is.EqualTo(0f), "no damage, no flash");
            float small = HudRules.HurtFlash(5, 100), big = HudRules.HurtFlash(25, 100);
            Assert.That(small, Is.GreaterThanOrEqualTo(HudRules.WeakestFlash), "even a small hit is seen");
            Assert.That(big, Is.GreaterThan(small));
            Assert.That(HudRules.HurtFlash(1000, 100), Is.EqualTo(HudRules.StrongestFlash), "capped");
            Assert.That(HudRules.FlashAfter(big, 0f), Is.EqualTo(big));
            Assert.That(HudRules.FlashAfter(big, HudRules.FlashSeconds * 0.5f), Is.EqualTo(big * 0.5f).Within(1e-5f));
            Assert.That(HudRules.FlashAfter(big, HudRules.FlashSeconds), Is.EqualTo(0f));
            Assert.That(HudRules.Combine(0.2f, 0.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void UnderAQuarterOfHerHealth_TheEdgeStays()
        {
            Assert.That(HudRules.IsLow(24, 100), Is.True);
            Assert.That(HudRules.IsLow(25, 100), Is.False);
            Assert.That(HudRules.IsLow(0, 100), Is.False, "dead: the death screen says it");
            Assert.That(HudRules.IsLow(29, 120), Is.True, "a quarter of a raised maximum");
        }

        [Test]
        public void TheEnd_SlowsToAStop_OverHalfASecond()
        {
            Assert.That(HudRules.EndSlowSeconds, Is.EqualTo(0.5f).Within(0.1f));
            Assert.That(HudRules.EndSlowdown(0f), Is.EqualTo(1f));
            Assert.That(HudRules.EndSlowdown(HudRules.EndSlowSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(HudRules.EndSlowdown(HudRules.EndSlowSeconds), Is.EqualTo(0f));
            Assert.That(HudRules.EndScreenShown(HudRules.EndSlowSeconds * 0.9f), Is.False);
            Assert.That(HudRules.EndScreenShown(HudRules.EndSlowSeconds), Is.True);
        }

        // ---------------- The settings' override rule ----------------

        [TestCase(SettingsRules.NoOverride, false, false)]
        [TestCase(SettingsRules.NoOverride, true, true)]
        [TestCase(SettingsRules.DashTowardCursor, true, false)]
        [TestCase(SettingsRules.DashTowardMovement, false, true)]
        [TestCase(7, true, true)]
        [TestCase(7, false, false)]
        public void TheDashDirection_IsTheStoredOverride_ElseTheBindings(int stored, bool bindingsSay, bool movement) =>
            Assert.That(SettingsRules.DashGoesTheWaySheMoves(stored, bindingsSay), Is.EqualTo(movement));

        [Test]
        public void TheDashDirection_IsStoredAsAWholeNumber()
        {
            Assert.That(SettingsRules.StoreDash(true), Is.EqualTo(SettingsRules.DashTowardMovement));
            Assert.That(SettingsRules.StoreDash(false), Is.EqualTo(SettingsRules.DashTowardCursor));
            Assert.That(SettingsRules.DashGoesTheWaySheMoves(SettingsRules.StoreDash(true), false), Is.True);
        }

        [Test]
        public void TheVolume_GoesInTenths_From0To100Percent()
        {
            Assert.That(SettingsRules.DefaultVolumeStep, Is.EqualTo(10));
            Assert.That(SettingsRules.VolumeStep(-3), Is.EqualTo(0));
            Assert.That(SettingsRules.VolumeStep(14), Is.EqualTo(10));
            Assert.That(SettingsRules.StepVolume(10, 1), Is.EqualTo(10), "stops at 100 %");
            Assert.That(SettingsRules.StepVolume(0, -1), Is.EqualTo(0), "stops at 0 %");
            Assert.That(SettingsRules.StepVolume(7, -1), Is.EqualTo(6));
            Assert.That(SettingsRules.StepVolume(7, 5), Is.EqualTo(8), "one step at a time");
            Assert.That(SettingsRules.Volume(7), Is.EqualTo(0.7f).Within(1e-6f));
            Assert.That(SettingsRules.Volume(0), Is.EqualTo(0f));
            Assert.That(SettingsRules.Percent(7), Is.EqualTo(70));
        }
    }
}
