using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Tests
{
    /// <summary>
    /// Work order 149 (Kristian, 2026-11-27: "Egentlig vil jeg ha unike elites, som har et eget sett med utfordringer slik som i hades";
    /// <see cref="EliteValues.Unique"/>): the Grave Ram's rules. Its numbers against research R7 5.2 and the order's rulings; every tell no
    /// shorter than walking out of its shape needs (R6 section 5), in both phases; its chooser (the opening, the rests, his screen, the
    /// seeded equal draws); the Ram's run, its crash and its strip (drawn and judged the same); the box that is on his screen; the hurt
    /// guard; its line in the summary. Rules only; nobody has played it.
    /// </summary>
    public class GraveRamRulesTests
    {
        static GraveRamSituation Open(float distance, float now = 10f) => new GraveRamSituation
        {
            Now = now, WokeAt = 0f, Distance = distance, OnScreen = true, LineClear = true, RamReaches = true, StompSeen = true,
        };

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndHoldsTheRamOnlyInFloorOnesEliteRoom()
        {
            Assert.That(EliteValues.Unique, Is.True);
            Assert.That(EliteValues.HoldsTheRam(1, true, true), Is.True, "floor 1's elite room");
            Assert.That(EliteValues.HoldsTheRam(1, false, true), Is.False, "another room of floor 1");
            for (int floor = 2; floor <= 5; floor++) Assert.That(EliteValues.HoldsTheRam(floor, true, true), Is.False, "floor " + floor + ": today's elite");
            Assert.That(EliteValues.HoldsTheRam(1, true, false), Is.False, "the switch off: today's elite");
        }

        [Test]
        public void TheTrialsLine_ReadsUniqueOrAsBefore_AndNoneStoredIsTheSwitch()
        {
            Assert.That(EliteLines.Name, Is.EqualTo("Elites"));
            Assert.That(EliteLines.WordsOf(true), Is.EqualTo("Unique"));
            Assert.That(EliteLines.WordsOf(false), Is.EqualTo("As before"));
            Assert.That(EliteLines.Unique(1), Is.True);
            Assert.That(EliteLines.Unique(0), Is.False);
            bool before = EliteValues.Unique;
            try
            {
                EliteValues.Unique = false;
                Assert.That(EliteLines.Unique(-1), Is.False);
                EliteValues.Unique = true;
                Assert.That(EliteLines.Unique(-1), Is.True);
            }
            finally { EliteValues.Unique = before; }
            var lines = new List<SettingLine>();
            // The merge of 2026-11-27, W3 (changed deliberately): on the Rooms page, right after work order 147's boss line (137's
            // callers with the boss trial off); found by its line, not counted from the end.
            SettingsPages.Lines(SettingsPage.Rooms, true, lines);
            Assert.That(lines.IndexOf(SettingLine.Elites), Is.EqualTo(lines.IndexOf(BossValues.Trial ? SettingLine.BossFight : SettingLine.Callers) + 1), "after the boss's line");
            Assert.That(SettingsPages.PageOf(SettingLine.Elites), Is.EqualTo(SettingsPage.Rooms));
        }

        // ---------------- Its numbers ----------------

        [Test]
        public void ItsNumbers_AreResearchFiveTwos()
        {
            var v = new GraveRamValues();
            Assert.That(v.Health, Is.EqualTo(17000), "R7's 10,000 raised in the producer's round 2: standing still must not win");
            Assert.That(v.Shown, Is.EqualTo(1.6f));
            Assert.That(v.PhaseTwoBelow, Is.EqualTo(0.5f));
            Assert.That(v.RoarSeconds, Is.EqualTo(1.5f));
            Assert.That(v.FirstAttackAfter, Is.EqualTo(2f));
            Assert.That(v.Gap, Is.EqualTo(1f));
            Assert.That(v.PhaseTwoGap, Is.EqualTo(0.6f));
            Assert.That(v.PhaseTwoRams, Is.EqualTo(2));
            EnemyAttackDefinition ram = v.RamAttack, stomp = v.StompAttack, stones = v.StonesAttack;
            Assert.That(ram.HalfWidth * 2f, Is.EqualTo(2.4f).Within(1e-5f), "a strip 2.4 m wide");
            Assert.That(ram.Windup, Is.EqualTo(1.2f));
            Assert.That(ram.Rush, Is.EqualTo(14f));
            Assert.That(ram.Length, Is.EqualTo(18f));
            Assert.That(ram.Damage, Is.EqualTo(18));
            Assert.That(ram.MinDistance, Is.EqualTo(6f));
            Assert.That(ram.MaxDistance, Is.EqualTo(18f));
            Assert.That(ram.Recovery, Is.EqualTo(1f), "a miss with no crash recovers 1.0 s");
            Assert.That(ram.Counterable, Is.True, "blue: Stopping Shot from its front counters it");
            Assert.That(v.RamRest, Is.EqualTo(2.5f));
            Assert.That(v.CrashDaze, Is.EqualTo(2.5f));
            Assert.That(v.Stagger.DazedDamageMultiplier, Is.EqualTo(1.5f), "dazed it takes x1.5");
            Assert.That(stomp.Radius, Is.EqualTo(3.5f));
            Assert.That(stomp.Windup, Is.EqualTo(0.9f));
            Assert.That(stomp.Damage, Is.EqualTo(14));
            Assert.That(stomp.MaxDistance, Is.EqualTo(4f));
            Assert.That(stomp.Recovery, Is.EqualTo(1.2f));
            Assert.That(stones.Radius, Is.EqualTo(1.8f));
            Assert.That(stones.Damage, Is.EqualTo(8));
            Assert.That(v.StonesCount, Is.EqualTo(3));
            Assert.That(v.StonesApart, Is.EqualTo(0.5f));
            Assert.That(v.StoneBurstAfter, Is.EqualTo(1f));
            Assert.That(v.StonesRest, Is.EqualTo(6f));
            Assert.That(v.StonesBeyond, Is.EqualTo(6f));
            Assert.That(stones.Windup, Is.EqualTo((v.StonesCount - 1) * v.StonesApart).Within(1e-5f), "its wind-up is the throwing");
        }

        [Test]
        public void ItsDamage_AgainstHerStartingHundred()
        {
            var v = new GraveRamValues();
            Assert.That(Placeholders.PlayerHealth, Is.EqualTo(100), "her health at a run's start");
            Assert.That(v.RamAttack.Damage, Is.EqualTo(18), "18 %");
            Assert.That(v.StompAttack.Damage, Is.EqualTo(14), "14 %");
            Assert.That(v.StonesAttack.Damage * v.StonesCount, Is.EqualTo(24), "three stones on one spot 24 %");
        }

        [Test]
        public void PhaseTwo_EveryWindupShorter_NeverUnderItsFloor_TheStonesThrowTheSame()
        {
            var v = new GraveRamValues();
            EnemyAttackDefinition[] calm = v.Attacks(false), angry = v.Attacks(true);
            Assert.That(angry[0].Windup, Is.EqualTo(1.2f * 0.85f).Within(1e-5f), "the Ram: 1.02 s");
            Assert.That(angry[1].Windup, Is.EqualTo(GraveRamRules.TellFloor(v.StompAttack, v)).Within(1e-5f), "the Stomp: its floor, over 0.9 x 0.85");
            Assert.That(angry[1].Windup, Is.GreaterThan(0.9f * 0.85f));
            Assert.That(angry[2].Windup, Is.EqualTo(calm[2].Windup), "the stones' throw is not shortened");
            Assert.That(v.StoneBurst(true), Is.EqualTo(0.85f).Within(1e-5f), "each stone bursts sooner");
            Assert.That(v.GapOf(true), Is.EqualTo(0.6f));
            Assert.That(calm[0], Is.SameAs(v.RamAttack), "phase 1 is the values' own");
            Assert.That(v.Attacks(true), Is.SameAs(angry), "made once");
        }

        [Test]
        public void EveryTell_IsNoShorterThanWalkingOutOfItsShape_InBothPhases()
        {
            var v = new GraveRamValues();
            Assert.That(GraveRamRules.TellFloor(v.RamAttack, v), Is.EqualTo(0.3f + 1.7f / 6f + 0.1f).Within(1e-4f), "the strip: 1.2 + 0.5 m");
            Assert.That(GraveRamRules.TellFloor(v.StompAttack, v), Is.EqualTo(0.3f + 2.7f / 6f + 0.1f).Within(1e-4f), "the Stomp: 3.5 + 0.5 - 1.3 m from against its body");
            Assert.That(GraveRamRules.TellFloor(v.StonesAttack, v), Is.EqualTo(0.3f + 2.3f / 6f + 0.1f).Within(1e-4f), "a stone: 1.8 + 0.5 m");
            foreach (bool two in new[] { false, true })
            {
                EnemyAttackDefinition[] a = v.Attacks(two);
                Assert.That(a[0].Windup, Is.GreaterThanOrEqualTo(GraveRamRules.TellFloor(a[0], v)), "the Ram, phase two " + two);
                Assert.That(a[1].Windup, Is.GreaterThanOrEqualTo(GraveRamRules.TellFloor(a[1], v) - 1e-5f), "the Stomp, phase two " + two);
                Assert.That(v.StoneBurst(two), Is.GreaterThanOrEqualTo(GraveRamRules.TellFloor(v.StonesAttack, v)), "a stone, phase two " + two);
            }
        }

        [Test]
        public void ItsDazedTint_IsADeepBlue_FarFromItsAshWhite()
        {
            var v = new GraveRamValues();
            Rgba w = v.Tint, d = v.DazedTint;
            Assert.That(w.R - d.R, Is.GreaterThan(0.5f), "far less red than ash white");
            Assert.That(d.B - d.R, Is.GreaterThan(0.6f), "plainly blue");
            Assert.That(d.B, Is.GreaterThan(0.9f));
            Assert.That(v.StoneWallSlack, Is.EqualTo(Placeholders.PlayerRadius), "a stone's outline stops at the wall's face: the walkable ground keeps her middle her radius off it");
        }

        // ---------------- The Ram's run, crash and strip ----------------

        [Test]
        public void TheRun_EndsWhereItsBodyMeetsStone_ACrash_OrAtTheGroundsEnd_AMiss()
        {
            float run = GraveRamRules.RunLength(18f, 11f, 18f, 1f, out bool crash);
            Assert.That(crash, Is.True, "stone on its line");
            Assert.That(run, Is.EqualTo(11f), "its middle stops where its body meets it");
            run = GraveRamRules.RunLength(18f, float.PositiveInfinity, 18f, 1f, out crash);
            Assert.That(crash, Is.False);
            Assert.That(run, Is.EqualTo(18f), "its whole length: a miss");
            run = GraveRamRules.RunLength(18f, float.PositiveInfinity, 9f, 1f, out crash);
            Assert.That(crash, Is.False, "the walkable ground's end with no stone is no crash");
            Assert.That(run, Is.EqualTo(9f));
            run = GraveRamRules.RunLength(18f, 9.6f, 9f, 1f, out crash);
            Assert.That(crash, Is.True, "stone just past the ground's end ended it");
            Assert.That(run, Is.EqualTo(9f), "its body cannot pass the ground's end");
            run = GraveRamRules.RunLength(18f, 12f, 9f, 1f, out crash);
            Assert.That(crash, Is.False, "stone far past the ground's end did not end it");
        }

        [Test]
        public void TheStrip_IsJudgedAsDrawn_CutSquare_NothingPastIt()
        {
            var from = new GroundPoint(0f, 0f);
            var dir = new GroundPoint(0f, 1f);
            float length = GraveRamRules.StripLength(10f, 0.8f);
            Assert.That(length, Is.EqualTo(10.8f).Within(1e-5f), "to its body's front");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(0f, 5f), 0.5f), Is.True, "on its line");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(1.69f, 5f), 0.5f), Is.True, "her edge on its edge");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(1.71f, 5f), 0.5f), Is.False, "a step out");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(0f, 11.29f), 0.5f), Is.True, "her edge at its end");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(0f, 11.31f), 0.5f), Is.False, "past its end");
            Assert.That(GraveRamRules.InStrip(from, dir, length, 1.2f, new GroundPoint(1.6f, 11.2f), 0.5f), Is.False,
                        "beyond its end's corner: square, not rounded by its half width (the round shot's reach, LineShot)");
        }

        [Test]
        public void ARam_IsBegunOnlyWhenItsStripReachesHerWhereSheStands()
        {
            Assert.That(GraveRamRules.RamReachesHer(18f, 0.8f, 12f, 0.5f), Is.True, "its whole length");
            Assert.That(GraveRamRules.RamReachesHer(9f, 0.8f, 10.3f, 0.5f), Is.True, "a pier behind her: it reaches her, and crashes if she steps out");
            Assert.That(GraveRamRules.RamReachesHer(9f, 0.8f, 10.31f, 0.5f), Is.False, "a pier's corner before her: never begun (a free daze)");
            Assert.That(GraveRamRules.RamReachesHer(1.4f, 0.8f, 7f, 0.5f), Is.False, "cut by the NavMesh short of her: never begun (146's parked boss)");
        }

        // ---------------- His screen ----------------

        [Test]
        public void OnHisScreen_IsTheWholeBox_InsideTheEdgesByTheMargin_OffThePlates()
        {
            var v = new GraveRamValues();
            var plate = new UiBox(0f, 0f, 560f, 140f);
            UiBox strip = GraveRamRules.StripKeepOut(new UiBox(600f, 900f, 720f, 150f), 1080f, v);
            Assert.That(strip.Y, Is.EqualTo(1080f - 210f), "the strip's keep-out reaches 210 px up");
            Assert.That(strip.X, Is.EqualTo(600f));
            float m = v.ScreenMargin;
            Assert.That(GraveRamRules.BoxOnScreen(900f, 300f, 1000f, 500f, 1920f, 1080f, plate, strip, m), Is.True, "the middle");
            Assert.That(GraveRamRules.BoxOnScreen(m - 1f, 300f, 100f, 500f, 1920f, 1080f, plate, strip, m), Is.False, "inside the left edge by less than the margin");
            Assert.That(GraveRamRules.BoxOnScreen(900f, 300f, 1000f, 1080f - m + 1f, 1920f, 1080f, plate, strip, m), Is.False, "the bottom edge");
            Assert.That(GraveRamRules.BoxOnScreen(300f, 100f, 400f, 300f, 1920f, 1080f, plate, strip, m), Is.False, "the head under the top plate");
            Assert.That(GraveRamRules.BoxOnScreen(300f, 140f + m + 1f, 400f, 300f, 1920f, 1080f, plate, strip, m), Is.True, "clear under the top plate");
            Assert.That(GraveRamRules.BoxOnScreen(900f, 700f, 1000f, 880f, 1920f, 1080f, plate, strip, m), Is.False, "its feet in the strip's keep-out");
            Assert.That(GraveRamRules.BoxOnScreen(1400f, 700f, 1500f, 950f, 1920f, 1080f, plate, strip, m), Is.True, "beside the strip");
        }

        // ---------------- The chooser ----------------

        [Test]
        public void Nothing_BeforeTwoSecondsAfterTheWake_NorOffHisScreen()
        {
            var v = new GraveRamValues();
            var t = new GraveRamTactics();
            GraveRamSituation s = Open(10f, 1.99f);
            Assert.That(t.Choose(s, v, v.Attacks(false)), Is.Null, "1.99 s after the wake");
            s.Now = 2f;
            Assert.That(t.Choose(s, v, v.Attacks(false)), Is.Not.Null, "2.0 s after");
            s.OnScreen = false;
            Assert.That(t.Choose(s, v, v.Attacks(false)), Is.Null, "off his screen");
        }

        [Test]
        public void TheOpening_RamRam_ThenTheStompIfClose_ElseTheStones()
        {
            var v = new GraveRamValues();
            EnemyAttackDefinition[] a = v.Attacks(false);
            var t = new GraveRamTactics();
            float now = 10f;
            EnemyAttackDefinition first = t.Choose(Open(10f, now), v, a);
            Assert.That(first.Name, Is.EqualTo(GraveRamValues.Ram));
            t.Began(first, now);
            t.RamEnded(now + 2f);
            now += 5f;
            EnemyAttackDefinition second = t.Choose(Open(10f, now), v, a);
            Assert.That(second.Name, Is.EqualTo(GraveRamValues.Ram));
            t.Began(second, now);
            t.RamEnded(now + 2f);
            now += 5f;
            GraveRamSituation close = Open(3f, now);
            Assert.That(t.Choose(close, v, a).Name, Is.EqualTo(GraveRamValues.Stomp), "close: the Stomp");
            GraveRamSituation hidden = Open(10f, now);
            hidden.LineClear = false;
            hidden.RamReaches = false;
            Assert.That(t.Choose(hidden, v, a).Name, Is.EqualTo(GraveRamValues.Stones), "far, no line: the Stones");
        }

        [Test]
        public void AnOpeningSlotNotEligible_GivesWayToWhatIs_SoItIsNeverParked()
        {
            var v = new GraveRamValues();
            EnemyAttackDefinition[] a = v.Attacks(false);
            var t = new GraveRamTactics();
            Assert.That(t.Choose(Open(2f), v, a).Name, Is.EqualTo(GraveRamValues.Stomp), "she rushed in at the start: the Stomp, not a wait for a Ram");
            GraveRamSituation hidden = Open(10f);
            hidden.LineClear = false;
            hidden.RamReaches = false;
            Assert.That(t.Choose(hidden, v, a).Name, Is.EqualTo(GraveRamValues.Stones), "behind a pier at the start: the Stones");
        }

        [Test]
        public void TheRam_NeedsItsBand_ItsLine_ToReachHer_AndItsRest()
        {
            var v = new GraveRamValues();
            var t = new GraveRamTactics();
            EnemyAttackDefinition ram = v.RamAttack;
            Assert.That(t.Eligible(ram, Open(5.9f), v), Is.False, "under 6 m");
            Assert.That(t.Eligible(ram, Open(6f), v), Is.True);
            Assert.That(t.Eligible(ram, Open(18f), v), Is.True);
            Assert.That(t.Eligible(ram, Open(18.1f), v), Is.False, "over 18 m");
            GraveRamSituation s = Open(10f);
            s.RamReaches = false;
            Assert.That(t.Eligible(ram, s, v), Is.False, "its line ends short of her");
            s = Open(10f);
            s.LineClear = false;
            Assert.That(t.Eligible(ram, s, v), Is.False, "no clear line");
            t.RamEnded(8f);
            Assert.That(t.Eligible(ram, Open(10f, 10.49f), v), Is.False, "2.49 s after the last Ram ended");
            Assert.That(t.Eligible(ram, Open(10f, 10.5f), v), Is.True, "2.5 s after");
        }

        [Test]
        public void TheStomp_WithinFourMetres_ItsCircleSeen_NeverTwiceInARow_UnlessSixSecondsPassed()
        {
            var v = new GraveRamValues();
            var t = new GraveRamTactics();
            EnemyAttackDefinition stomp = v.StompAttack;
            Assert.That(t.Eligible(stomp, Open(4f), v), Is.True);
            Assert.That(t.Eligible(stomp, Open(4.1f), v), Is.False);
            GraveRamSituation s = Open(2f);
            s.StompSeen = false;
            Assert.That(t.Eligible(stomp, s, v), Is.False, "its circle not wholly on his screen");
            t.Began(stomp, 10f);
            Assert.That(v.StompAgain, Is.EqualTo(6f));
            Assert.That(t.Eligible(stomp, Open(2f, 12f), v), Is.False, "never twice in a row");
            Assert.That(t.Eligible(stomp, Open(2f, 10f + v.StompAgain - 0.01f), v), Is.False);
            Assert.That(t.Eligible(stomp, Open(2f, 10f + v.StompAgain), v), Is.True,
                        "unless she stays against it: 6 s after the last, a second (else it is parked, she shooting it for free)");
            t.Began(v.RamAttack, 13f);
            Assert.That(t.Eligible(stomp, Open(2f, 13.5f), v), Is.True, "after another attack, at once");
        }

        [Test]
        public void TheStones_BeyondSixMetres_NoClearLine_SixSecondsApart()
        {
            var v = new GraveRamValues();
            var t = new GraveRamTactics();
            EnemyAttackDefinition stones = v.StonesAttack;
            GraveRamSituation s = Open(10f);
            Assert.That(t.Eligible(stones, s, v), Is.False, "a clear line: it rams instead");
            s.LineClear = false;
            Assert.That(t.Eligible(stones, s, v), Is.True);
            s.Distance = 6f;
            Assert.That(t.Eligible(stones, s, v), Is.False, "beyond 6 m only");
            t.Began(stones, 10f);
            s = Open(10f, 15.99f);
            s.LineClear = false;
            Assert.That(t.Eligible(stones, s, v), Is.False, "5.99 s after the last throw began");
            s.Now = 16f;
            Assert.That(t.Eligible(stones, s, v), Is.True);
        }

        [Test]
        public void AfterTheOpening_EqualDrawsAmongTheEligible_FromASeededSource()
        {
            var v = new GraveRamValues();
            EnemyAttackDefinition[] a = v.Attacks(false);
            // At 4 m with a line that reaches her... the Ram's band starts at 6: only the Stomp, alone, eligible. Make two eligible: the
            // Stomp at 4 m and the Stones (no line); the Ram needs a line, so these two only.
            var counts = new Dictionary<string, int> { { GraveRamValues.Stomp, 0 }, { GraveRamValues.Stones, 0 } };
            var first = new List<string>();
            for (int seed = 1; seed <= 400; seed++)
            {
                var t = new GraveRamTactics();
                t.Seed((ulong)seed * 7919UL);
                t.Began(v.RamAttack, 0f);
                t.Began(v.RamAttack, 0f);
                t.Began(v.RamAttack, 0f); // the opening taken
                GraveRamSituation s = Open(4f, 100f);
                s.LineClear = false;
                s.RamReaches = false;
                v.StonesBeyond = 3f; // for this test: the Stones beyond 3 m, so both are eligible at 4 m
                EnemyAttackDefinition c = t.Choose(s, v, a);
                counts[c.Name]++;
                if (seed <= 20) first.Add(c.Name);
            }
            Assert.That(counts[GraveRamValues.Stomp], Is.InRange(160, 240), "about half each");
            Assert.That(counts[GraveRamValues.Stones], Is.InRange(160, 240));
            var again = new List<string>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var t = new GraveRamTactics();
                t.Seed((ulong)seed * 7919UL);
                t.Began(v.RamAttack, 0f);
                t.Began(v.RamAttack, 0f);
                t.Began(v.RamAttack, 0f);
                GraveRamSituation s = Open(4f, 100f);
                s.LineClear = false;
                s.RamReaches = false;
                again.Add(t.Choose(s, v, a).Name);
            }
            Assert.That(again, Is.EqualTo(first), "the same seed draws the same");
        }

        // ---------------- The hurt guard ----------------

        [Test]
        public void TheHurtGuard_HoldsPointEightSeconds_AndFlickers()
        {
            // The merge of 2026-11-27, W3 (changed deliberately): the Ram's guard is the one guard of work order 147's rule; a hit of
            // the Ram's begins it with no trial fight on; its flicker is the one flicker (shown first, then hidden, every 0.07 s).
            Assert.That(HurtGuardRule.Seconds, Is.EqualTo(0.8f));
            var g = new HurtGuardRule();
            Assert.That(g.Holds(0f), Is.False, "none begun");
            g.Begin(10f);
            Assert.That(g.Active, Is.False, "no trial fight: the Ram's own hit begins it");
            Assert.That(g.Holds(10f), Is.True);
            Assert.That(g.Holds(10.79f), Is.True);
            Assert.That(g.Holds(10.8f), Is.False);
            Assert.That(HurtGuardRule.FlickerHidden(10.1f, 10f), Is.True, "hidden in a flicker's second half");
            Assert.That(HurtGuardRule.FlickerHidden(10.02f, 10f), Is.False, "shown in its first");
            Assert.That(HurtGuardRule.FlickerHidden(10.9f, 10f), Is.False, "shown once it is over");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_HasAnEliteBlock_OnlyWithAFight()
        {
            var events = new List<SessionEvent> { new SessionEvent(1d, SessionEventTypes.SessionStart) };
            string without = SessionSummary.Format(SessionSummary.Compute(events));
            Assert.That(without, Does.Not.Contain("Elite (work order 149)"));
            var e = new SessionEvent(60d, GraveRamEvents.Fight, GraveRamValues.Title);
            foreach (var kv in new[]
            {
                ("seconds", "52.3"), ("ended", "died"), ("ram", "18"), ("stomp", "14"), ("stones", "16"), ("rams", "9"), ("crashes", "4"),
                ("missed", "4"), ("met", "1"), ("countered", "0"), ("crashed_on", "wall;wall;low prop;wall"), ("daze", "10.0"), ("daze_shot", "7.5"),
                ("daze_damage", "3900"), ("slow", "0.41"), ("guarded", "1"), ("two", "20.1"),
            }) e.AddExtra(kv.Item1, kv.Item2);
            events.Add(e);
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.GraveRam.Count, Is.EqualTo(1));
            GraveRamRow r = stats.GraveRam[0];
            Assert.That(r.Seconds, Is.EqualTo(52.3d).Within(1e-6));
            Assert.That(r.Crashes, Is.EqualTo(4));
            Assert.That(r.Stones, Is.EqualTo(16));
            string text = SessionSummary.Format(stats);
            Assert.That(text, Does.Contain("Elite (work order 149): The Grave Ram"));
            Assert.That(text, Does.Contain("9: 4 / 4 / 1"), "Rams: crashed / missed / met her");
            Assert.That(text, Does.Contain("wall, wall, low prop, wall"));
            Assert.That(text, Does.Contain("18 / 14 / 16"), "her damage by attack");
            Assert.That(text, Does.Contain("41 %"), "her time standing");
        }
    }
}
