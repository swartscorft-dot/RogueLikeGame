using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Line-ups (work order 135; Kristian, 2026-11-25: "man kommer inn, skyter de små først, så cahrgerne, og så ranged. det er veldig
    /// likt hver gang", and his tick "Ja" to a room of two kinds with the full mix kept for one big room a floor), the generator's
    /// version 25: the switch, the data (each floor's kinds no sooner than the act's plan brings them), how a room's counts spend its
    /// weight, the fill of a floor's fights, the validator's rules, a thousand seeds a floor, and the pins: with the switch off every floor
    /// is version 24's, field for field (BossAloneTests' version 24 pins, their OLD numbers); version 25 has two pins, the lancers named
    /// on and named off, and golden maps. The numbers for the producer's report are <see cref="LineUpsFloorsTests"/>.
    /// </summary>
    public class LineUpsTests
    {
        static LevelValues On => new LevelValues();
        static LevelValues Off => new LevelValues { LineUps = false };
        static LevelValues LancersOn => new LevelValues { Lancers = true };
        static LevelValues LancersOff => new LevelValues { Lancers = false };

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // BossAloneTests' fold (RangedAroundTheRoomTests' with the posts): the very fold of version 24's pins.
        static ulong Fold(LevelLayout level, ulong h)
        {
            h = FloorPrint.Of(level, h);
            foreach (LevelRoom r in level.Rooms)
            {
                h = Mix(h, (ulong)r.Shape);
                h = Mix(h, (ulong)r.SolidCount);
                foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                foreach (char ch in r.Template?.ToString() ?? "-") h = Mix(h, ch);
                h = Mix(h, (ulong)r.CoverTallAdded);
                h = Mix(h, (ulong)r.CoverLowAdded);
                foreach (EnemyGroup g in r.EnemyGroups)
                {
                    RangedPlace p = g.Ranged;
                    if (p == null) continue;
                    h = Mix(h, (ulong)p.Post);
                    h = Mix(h, (ulong)p.Rung);
                    h = Mix(h, (ulong)p.Cluster);
                    h = Mix(h, (ulong)p.ClusterSize);
                }
            }
            return h;
        }

        // Version 25: the line-up each room was filled from, besides.
        static ulong FoldLineUps(LevelLayout level, ulong h)
        {
            h = Fold(level, h);
            foreach (LevelRoom r in level.Rooms)
                foreach (char ch in r.LineUp?.Name ?? "-") h = Mix(h, ch);
            return h;
        }

        static List<LevelLayout> Run(ulong runSeed, LevelValues v, int floors)
        {
            var list = new List<LevelLayout>();
            string before = null;
            for (int floor = 1; floor <= floors; floor++)
            {
                LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(runSeed, floor), floor, v, exitBefore: before);
                list.Add(l);
                before = WayDowns.Shown(l);
            }
            return list;
        }

        /// <summary>BossAloneTests' two thousand floors and a hundred runs of five, folded, as its version 24 pins.</summary>
        static ulong Print(LevelValues v, Func<LevelLayout, ulong, ulong> fold)
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = fold(LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v), h);
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5)) h = fold(l, h);
            return h;
        }

        static List<LevelRoom> Chain(LevelLayout l) => LevelGeometry.MainPath(l).Where(r => r.Kind == RoomKind.Combat).ToList();

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion25_AndOnlyOnAFloorInARowWithTheActsPlan()
        {
            Assert.That(new LevelValues().LineUps, Is.True, "on by default (work order 135, his tick)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(25), "work order 135's version");
            Assert.That(LevelLineUps.On(On), Is.True);
            Assert.That(LevelLineUps.On(Off), Is.False);
            Assert.That(LevelLineUps.On(new LevelValues { InALine = false }), Is.False, "the floors before the line are as they were");
            Assert.That(LevelLineUps.On(new LevelValues { ActPlan = false }), Is.False, "the floors before the act's plan are as they were");
            Assert.That(LevelGenerator.Generate(0x135UL, 3).Version, Is.EqualTo(25));
            Assert.That(LevelGenerator.Generate(0x135UL, 3, Off).Version, Is.EqualTo(25), "off: version 24's floor, but for the number");
            Assert.That(new LineUpValues().SwarmersMost, Is.EqualTo(14), "the second round: fourteen swarmers at most");
            Assert.That(new LineUpValues().OneARoomMost, Is.EqualTo(2), "the second round: two of a one-a-room kind where the floor allows two");
            Assert.That(new LineUpValues().WeightMore, Is.EqualTo(0.15f), "the third round: his \"Ca. 15 % tyngre\"");
            Assert.That(new LineUpValues().TargetOf(10, 1), Is.EqualTo(10).Within(1e-9), "floor 1 at today's weight");
            Assert.That(new LineUpValues().TargetOf(10, 2), Is.EqualTo(11.5).Within(1e-5), "floors 2 to 5 15 % heavier");
            Assert.That(new LineUpValues().WeightTolerance, Is.EqualTo(0.2f), "the named tolerance");
        }

        /// <summary>The Trials page's line (he ticked the rooms' switches there on 2026-11-26): last on the page, its name and words.</summary>
        [Test]
        public void TheTrialsPage_EndsWithTheLineUpsLine()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines[lines.Count - 1], Is.EqualTo(SettingLine.LineUps));
            Assert.That(SettingsPages.PageOf(SettingLine.LineUps), Is.EqualTo(SettingsPage.Trials));
            Assert.That(RoomTrialLines.Is(SettingLine.LineUps), Is.True);
            Assert.That(RoomTrialLines.Is(SettingLine.TimingClick), Is.False);
            Assert.That(RoomTrialLines.Name(SettingLine.LineUps), Is.EqualTo("Line-ups"));
            Assert.That(RoomTrialLines.Words(true), Is.EqualTo("on"));
            Assert.That(RoomTrialLines.Words(false), Is.EqualTo("off"));
        }

        // ---------------- The data ----------------

        /// <summary>
        /// Each floor's line-ups: four to six with the full room (the research's table counts it), two kinds each, names their own; and no
        /// line-up brings a kind to a floor sooner than today's generator does (a kind the room may hold none of on that floor).
        /// </summary>
        [Test]
        public void EachFloor_HasFourToSixLineUps_OfTwoKinds_NoKindSoonerThanTheActsPlan()
        {
            var v = new LevelValues();
            for (int floor = 1; floor <= v.FloorsPerAct; floor++)
            {
                LineUp[] table = v.LineUpRules.Of(floor);
                Assert.That(table.Length + 1, Is.InRange(4, 6), "floor " + floor + ": with the full room");
                Assert.That(table.Select(u => u.Name).Distinct().Count(), Is.EqualTo(table.Length), "floor " + floor + ": names");
                foreach (LineUp u in table)
                {
                    Assert.That(u.Full, Is.False);
                    Assert.That(u.Lead, Is.Not.EqualTo(u.Partner), u.Name);
                    Assert.That(u.LeadFewest, Is.InRange(1, u.LeadMost), u.Name);
                    Assert.That(u.PartnerFewest, Is.InRange(1, u.PartnerMost), u.Name);
                    Assert.That(u.Name, Is.Not.EqualTo(v.LineUpRules.FullName));
                    // A room past the first may hold both kinds there (else the line-up could never be drawn on its floor).
                    Assert.That(LevelLineUps.Counts(u, floor, false, v, out _, out _, out _, out _), Is.True, "floor " + floor + ": " + u.Name);
                    // Second round: the swarmers fourteen at most, the one-a-room kinds two at most.
                    foreach ((EnemyKind k, int most) in new[] { (u.Lead, u.LeadMost), (u.Partner, u.PartnerMost) })
                    {
                        if (k == EnemyKind.Swarmer) Assert.That(most, Is.LessThanOrEqualTo(v.LineUpRules.SwarmersMost), "floor " + floor + ": " + u.Name);
                        if (k != EnemyKind.Swarmer && k != EnemyKind.Brute && k != EnemyKind.Shooter)
                            Assert.That(most, Is.LessThanOrEqualTo(v.LineUpRules.OneARoomMost), "floor " + floor + ": " + u.Name);
                    }
                }
            }
            // No kind sooner than today: the guard from floor 2, the caster and the warder from 3, the bomber and the lancer from 4.
            var first = new Dictionary<EnemyKind, int>();
            for (int floor = 1; floor <= v.FloorsPerAct; floor++)
                foreach (LineUp u in v.LineUpRules.Of(floor))
                    foreach (EnemyKind k in new[] { u.Lead, u.Partner })
                        if (!first.ContainsKey(k)) first[k] = floor;
            int Earliest(EnemyKind k) => k == EnemyKind.Guard ? v.GuardsFromFloor : k == EnemyKind.Caster ? v.CastersFromFloor : k == EnemyKind.Warder ? v.WarderFloors.FromFloor
                                       : k == EnemyKind.Bomber ? v.BomberFloors.FromFloor : k == EnemyKind.Lancer ? v.LancerFloors.FromFloor : 1;
            foreach (KeyValuePair<EnemyKind, int> kv in first)
                Assert.That(kv.Value, Is.GreaterThanOrEqualTo(Earliest(kv.Key)), EnemyKinds.Word(kv.Key));
            // And a room may hold none of them before then, so a table moved by hand still cannot bring one sooner.
            foreach (EnemyKind k in new[] { EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Warder, EnemyKind.Bomber, EnemyKind.Lancer })
                for (int floor = 1; floor < Earliest(k); floor++)
                    Assert.That(LevelLineUps.Most(k, floor, false, v), Is.EqualTo(0), EnemyKinds.Word(k) + " on floor " + floor);
            // The bomber, the warder and the lancer never in a floor's first combat room, as their own rules have it.
            foreach (EnemyKind k in new[] { EnemyKind.Warder, EnemyKind.Bomber, EnemyKind.Lancer })
                Assert.That(LevelLineUps.Most(k, 5, true, v), Is.EqualTo(0), EnemyKinds.Word(k) + " in the first combat room");
        }

        [Test]
        public void TheWeights_AreTheSquareLawsWorth_OfHealthAndHisSessionsDamage()
        {
            var w = new LineUpValues();
            var v = new LevelValues();
            // play-data.md's damage per enemy present (his sessions of 2026-11-25).
            var damage = new Dictionary<EnemyKind, double> { { EnemyKind.Brute, 2.0 }, { EnemyKind.Shooter, 2.6 }, { EnemyKind.Guard, 1.3 }, { EnemyKind.Caster, 4.4 }, { EnemyKind.Bomber, 3.3 } };
            foreach (KeyValuePair<EnemyKind, double> kv in damage)
            {
                double worth = Math.Sqrt(v.HealthOf(kv.Key) / (double)v.SwarmerHealth * kv.Value / 0.4);
                Assert.That(w.WeightOf(kv.Key), Is.EqualTo(worth).Within(0.051), EnemyKinds.Word(kv.Key));
            }
            Assert.That(w.WeightOf(EnemyKind.Swarmer), Is.EqualTo(1f));
            Assert.That(w.WeightOf(EnemyKind.Warder), Is.EqualTo(1f), "as the swarmer whose place it takes");
            Assert.That(w.WeightOf(EnemyKind.Lancer), Is.EqualTo(Math.Sqrt(v.LancerHealth / (double)v.SwarmerHealth * 2.0 / 0.4)).Within(0.051), "a brute's damage, his sessions have none");
            Assert.That(w.WeightOf(EnemyKind.Boss), Is.EqualTo(0f));
            var groups = new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 4), new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Guard, 1, elite: true), new EnemyGroup(EnemyKind.Boss, 1, boss: true) };
            Assert.That(w.WeightOf(groups), Is.EqualTo(4 * 1.0 + 2 * 6.3).Within(1e-5), "the elite and the boss apart");
        }

        // ---------------- Spending a room's weight ----------------

        [Test]
        public void Spend_DrawsAmongTheCountsWithinTheTolerance_ElseTheNearest()
        {
            var w = new LineUpValues();
            var u = new LineUp("brute line", EnemyKind.Brute, 2, 3, EnemyKind.Shooter, 1, 3);
            var seen = new HashSet<(int, int)>();
            for (ulong s = 0; s < 200; s++)
            {
                LevelLineUps.Spend(u, 20.0, 2, 3, 1, 3, w, LevelRandom.Stream(s, "test"), out int a, out int b);
                double weight = a * 6.3 + b * 4.2;
                Assert.That(Math.Abs(weight - 20.0), Is.LessThanOrEqualTo(0.2 * 20.0 + 1e-9), a + " and " + b);
                seen.Add((a, b));
            }
            // 2+1 16.8, 2+2 21.0, 3+0 no, 3+1 23.1: all three within 4 of 20, so all three are drawn.
            Assert.That(seen, Is.EquivalentTo(new[] { (2, 1), (2, 2), (3, 1) }));
            // A weight past what the counts can reach: the nearest, the most of each.
            LevelLineUps.Spend(u, 60.0, 2, 3, 1, 3, w, LevelRandom.Stream(1, "test"), out int ma, out int mb);
            Assert.That((ma, mb), Is.EqualTo((3, 3)));
            LevelLineUps.Spend(u, 1.0, 2, 3, 1, 3, w, LevelRandom.Stream(1, "test"), out int la, out int lb);
            Assert.That((la, lb), Is.EqualTo((2, 1)), "below: the fewest");
        }

        [Test]
        public void Counts_AreTheLineUpsOwn_PastTheMixedRoomsCaps_CutToTheirOwnMost_NeverTurnedIntoSwarmers()
        {
            var v = new LevelValues();
            // Second round (the weight wins): the brutes and shooters by the line-up's own counts, past the mixed room's three.
            var many = new LineUp("many", EnemyKind.Brute, 2, 6, EnemyKind.Shooter, 4, 5);
            Assert.That(LevelLineUps.Counts(many, 3, false, v, out int al, out int ah, out int bl, out int bh), Is.True);
            Assert.That((al, ah, bl, bh), Is.EqualTo((2, 6, 4, 5)), "the line-up's own counts");
            var horde = new LineUp("horde", EnemyKind.Swarmer, 10, 20, EnemyKind.Brute, 1, 1);
            Assert.That(LevelLineUps.Counts(horde, 3, false, v, out al, out ah, out _, out _), Is.True);
            Assert.That((al, ah), Is.EqualTo((10, 14)), "fourteen swarmers at most");
            var guards = new LineUp("guards", EnemyKind.Guard, 2, 3, EnemyKind.Swarmer, 4, 4);
            Assert.That(LevelLineUps.Counts(guards, 3, false, v, out al, out ah, out _, out _), Is.True);
            Assert.That((al, ah), Is.EqualTo((2, 2)), "two guards: floor 3's exit room holds two");
            Assert.That(LevelLineUps.Counts(guards, 5, false, v, out al, out ah, out _, out _), Is.True);
            Assert.That((al, ah), Is.EqualTo((1, 1)), "one on floor 5: its exit room's two are the act boss and one guard");
            Assert.That(LevelLineUps.Counts(guards, 1, false, v, out _, out _, out _, out _), Is.False, "no guard on floor 1");
            var casters = new LineUp("casters", EnemyKind.Caster, 1, 3, EnemyKind.Shooter, 2, 2);
            Assert.That(LevelLineUps.Counts(casters, 3, false, v, out _, out ah, out _, out _), Is.True);
            Assert.That(ah, Is.EqualTo(2), "two casters: floor 3's exit room holds two");
            Assert.That(LevelLineUps.Counts(casters, 2, false, v, out _, out _, out _, out _), Is.False, "no caster before floor 3");
            var bombers = new LineUp("bombers", EnemyKind.Brute, 4, 4, EnemyKind.Bomber, 1, 2);
            Assert.That(LevelLineUps.Counts(bombers, 4, false, v, out _, out _, out _, out bh), Is.True);
            Assert.That(bh, Is.EqualTo(1), "the bomber's own rule: one in a floor-4 combat room");
            Assert.That(LevelLineUps.Counts(bombers, 5, false, v, out _, out _, out _, out bh), Is.True);
            Assert.That(bh, Is.EqualTo(2), "two on floor 5");
            var warded = new LineUp("warded", EnemyKind.Swarmer, 1, 8, EnemyKind.Warder, 1, 1);
            Assert.That(LevelLineUps.Counts(warded, 3, false, v, out al, out _, out _, out _), Is.True);
            Assert.That(al, Is.EqualTo(v.WarderFloors.OthersAtLeast), "a warder stands only with three others");
            Assert.That(LevelLineUps.Counts(warded, 3, true, v, out _, out _, out _, out _), Is.False, "never in the first combat room");
        }

        // ---------------- A floor's fights filled ----------------

        static List<List<EnemyGroup>> TodaysFights() => new List<List<EnemyGroup>>
        {
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Shooter, 2), new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 4), new EnemyGroup(EnemyKind.Swarmer, 3) },
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Guard), new EnemyGroup(EnemyKind.Shooter, 2), new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 4) },
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Shooter, 2), new EnemyGroup(EnemyKind.Brute, 3), new EnemyGroup(EnemyKind.Swarmer, 5), new EnemyGroup(EnemyKind.Brute, 1, elite: true) },
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Shooter, 3), new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 5) },
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Shooter, 2), new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 5) },
            new List<EnemyGroup> { new EnemyGroup(EnemyKind.Guard), new EnemyGroup(EnemyKind.Shooter, 3), new EnemyGroup(EnemyKind.Brute, 3), new EnemyGroup(EnemyKind.Swarmer, 5), new EnemyGroup(EnemyKind.Swarmer, 4) },
        };

        static string Text(List<EnemyGroup> room) => string.Join(", ", room.Select(g => g.ToString()));

        [Test]
        public void Put_FillsEveryCombatRoomButOne_FromOneLineUp_TheElite_AndTheExitRoom_AsTheyWere()
        {
            Assert.That(LevelLineUps.Put(TodaysFights(), 1UL, 2, Off), Is.Null, "off: nothing");
            for (ulong s = 0; s < 300; s++)
            {
                List<List<EnemyGroup>> fights = TodaysFights(), was = TodaysFights();
                List<LineUp> drawn = LevelLineUps.Put(fights, s, 2, On);
                Assert.That(drawn.Count, Is.EqualTo(fights.Count));
                Assert.That(drawn[5], Is.Null, "the exit room has none");
                Assert.That(Text(fights[5]), Is.EqualTo(Text(was[5])), "the exit room as it was");
                Assert.That(drawn.Count(u => u != null && u.Full), Is.EqualTo(1), "one full room");
                int full = drawn.FindIndex(u => u != null && u.Full);
                Assert.That(full, Is.Not.EqualTo(0), "never the first combat room");
                Assert.That(full, Is.Not.EqualTo(2), "never the elite room");
                Assert.That(Text(fights[full]), Is.EqualTo(Text(was[full])), "the full room keeps today's groups");
                Assert.That(fights[2].Count(g => g.Elite), Is.EqualTo(1), "the elite stays");
                for (int i = 0; i < 5; i++)
                {
                    if (i > 0) Assert.That(drawn[i].Name, Is.Not.EqualTo(drawn[i - 1].Name), "seed " + s + ": neighbours never one line-up");
                    if (drawn[i].Full) continue;
                    var kinds = fights[i].Where(g => !g.Elite).Select(g => g.Kind).Distinct().ToList();
                    Assert.That(kinds, Is.EquivalentTo(new[] { drawn[i].Lead, drawn[i].Partner }), "seed " + s + " room " + i + ": " + Text(fights[i]));
                    Assert.That(LevelLineUps.Counts(drawn[i], 2, i == 0, On, out int leadLo, out int leadHi, out int partnerLo, out int partnerHi), Is.True);
                    int lead = fights[i].Where(g => g.Kind == drawn[i].Lead && !g.Elite).Sum(g => g.Count), partner = fights[i].Where(g => g.Kind == drawn[i].Partner && !g.Elite).Sum(g => g.Count);
                    Assert.That(lead, Is.InRange(leadLo, leadHi), "the lead's counts");
                    Assert.That(partner, Is.InRange(partnerLo, partnerHi), "the partner's counts");
                    Assert.That(fights[i].Where(g => g.Kind == EnemyKind.Swarmer).Sum(g => g.Count), Is.LessThanOrEqualTo(14), "fourteen swarmers at most");
                    Assert.That(fights[i].Where(g => g.Kind == EnemyKind.Guard).Sum(g => g.Count), Is.LessThanOrEqualTo(2), "two guards at most");
                }
            }
        }

        // ---------------- The validator ----------------

        [Test]
        public void TheValidator_HoldsTheLineUpsRules()
        {
            LevelLayout l = LevelGenerator.Generate(0x135UL, 2);
            Assert.That(LevelValidator.Validate(l, On), Is.Empty);
            List<LevelRoom> chain = Chain(l);
            Assert.That(chain.All(r => r.LineUp != null), Is.True, "every combat room carries its line-up (or the full room's mark)");
            Assert.That(l.Rooms.Single(r => r.Kind == RoomKind.Exit).LineUp, Is.Null);
            LevelRoom room = chain.First(r => LevelLineUps.Fills(r) && !r.Elite);
            // A third kind put in: a fault.
            EnemyKind other = new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter }.First(k => k != room.LineUp.Lead && k != room.LineUp.Partner);
            room.EnemyGroups.Add(new EnemyGroup(other));
            Assert.That(LevelValidator.Validate(l, On).Any(f => f.Contains("holds only")), Is.True, string.Join("\n", LevelValidator.Validate(l, On)));
            room.EnemyGroups.RemoveAt(room.EnemyGroups.Count - 1);
            // Two neighbours of one line-up: a fault.
            LineUp kept = chain[1].LineUp;
            chain[1].LineUp = chain[0].LineUp;
            Assert.That(LevelValidator.Validate(l, On).Any(f => f.Contains("next to each other on the row")), Is.True);
            chain[1].LineUp = kept;
            // The full room first: a fault.
            LineUp firstWas = chain[0].LineUp;
            chain[0].LineUp = LineUp.FullRoom("full");
            Assert.That(LevelValidator.Validate(l, On).Any(f => f.Contains("first combat room, is its full room")), Is.True);
            chain[0].LineUp = firstWas;
            Assert.That(LevelValidator.Validate(l, On), Is.Empty);
        }

        // ---------------- A thousand seeds a floor ----------------

        /// <summary>
        /// A thousand seeds a floor: every floor valid; one full room, never the first nor the elite room, holding what version 24 gives
        /// that room; the other combat rooms only their line-up's two kinds (the elite apart); neighbours never one line-up; the exit room
        /// and every room's elite as version 24 has them.
        /// </summary>
        [Test, Timeout(3600000)] // the long sweeps' own time limit (as BossAloneTests')
        public void AThousandSeedsAFloor_KeepTheLineUpsRules()
        {
            var w = new LineUpValues();
            for (int floor = 1; floor <= 5; floor++)
            {
                double offSum = 0, heaviest = 0;
                int lineUpRooms = 0, within = 0;
                for (ulong s = 0; s < 1000; s++)
                {
                    LevelLayout on = LevelGenerator.Generate(s, floor, On), off = LevelGenerator.Generate(s, floor, Off);
                    List<string> faults = LevelValidator.Validate(on, On);
                    if (faults.Count > 0) Assert.Fail("seed " + s + " floor " + floor + ": " + string.Join("; ", faults));
                    List<LevelRoom> chain = Chain(on), was = Chain(off);
                    Assert.That(chain.Count, Is.EqualTo(was.Count));
                    Assert.That(chain.Count(r => r.LineUp != null && r.LineUp.Full), Is.EqualTo(1), "seed " + s + " floor " + floor + ": one full room");
                    for (int i = 0; i < chain.Count; i++)
                    {
                        LevelRoom r = chain[i];
                        Assert.That(r.LineUp, Is.Not.Null);
                        string at = "seed " + s + " floor " + floor + " room " + i;
                        if (i > 0) Assert.That(r.LineUp.Name, Is.Not.EqualTo(chain[i - 1].LineUp.Name), at + ": neighbours");
                        string Elites(LevelRoom x) => string.Join(",", x.EnemyGroups.Where(g => g.Elite).Select(g => g.ToString()));
                        Assert.That(Elites(r), Is.EqualTo(Elites(was[i])), at + ": the elite as before");
                        string Kinds(LevelRoom x) => string.Join(",", x.EnemyGroups.Select(g => g.ToString()).OrderBy(t => t));
                        if (r.LineUp.Full)
                        {
                            Assert.That(i, Is.Not.EqualTo(0), at);
                            Assert.That(r.Elite, Is.False, at);
                            Assert.That(Kinds(r), Is.EqualTo(Kinds(was[i])), at + ": the full room holds version 24's groups");
                            continue;
                        }
                        Assert.That(r.EnemyGroups.Where(g => !g.Elite).Select(g => g.Kind).Distinct().Count(), Is.EqualTo(2), at + ": two kinds");
                        // Second round (the weight wins): the room's weight against the same room's of version 24.
                        double target = w.TargetOf(w.WeightOf(was[i].EnemyGroups), floor), weight = w.WeightOf(r.EnemyGroups); // third round: his 15 % from floor 2
                        offSum += (weight - target) / target;
                        lineUpRooms++;
                        if (Math.Abs(weight - target) <= w.WeightTolerance * target + 1e-9) within++;
                    }
                    foreach (LevelRoom r in was) heaviest = Math.Max(heaviest, w.WeightOf(r.EnemyGroups));
                    LevelRoom exit = on.Rooms.Single(r => r.Kind == RoomKind.Exit), exitWas = off.Rooms.Single(r => r.Kind == RoomKind.Exit);
                    Assert.That(string.Join(",", exit.EnemyGroups.Select(g => g.ToString()).OrderBy(t => t)), Is.EqualTo(string.Join(",", exitWas.EnemyGroups.Select(g => g.ToString()).OrderBy(t => t))),
                                "seed " + s + " floor " + floor + ": the exit room as before");
                }
                // The producer's second round: a mean within 5 % a floor, at least 80 % of the rooms within the tolerance.
                double mean = offSum / lineUpRooms, share = within / (double)lineUpRooms;
                TestContext.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "LINEUPWEIGHT floor {0}: off by {1:+0.0%;-0.0%} on average, {2:P1} within the tolerance; the heaviest room of version 24 {3:0.0}", floor, mean, share, heaviest));
                Assert.That(Math.Abs(mean), Is.LessThanOrEqualTo(0.05), "floor " + floor + ": the line-up rooms' weight on average");
                Assert.That(share, Is.GreaterThanOrEqualTo(0.8), "floor " + floor + ": the rooms within the tolerance");
                // The wave 1 (work order 136: the lead wakes at once): the lead's top count no heavier than the floor's heaviest room of version 24 and the tolerance.
                foreach (LineUp u in On.LineUpRules.Of(floor))
                {
                    LevelLineUps.Counts(u, floor, false, On, out _, out int leadHi, out _, out _);
                    Assert.That(leadHi * w.WeightOf(u.Lead), Is.LessThanOrEqualTo(w.TargetOf(heaviest, floor) * (1 + w.WeightTolerance)), "floor " + floor + ": " + u.Name + "'s wave 1");
                }
            }
        }

        // ---------------- The pins ----------------

        /// <summary>
        /// With the line-ups off every floor is version 24's, field for field: the same floors and runs as version 24's own pins
        /// (BossAloneTests.Version24_...), held to those pins' OLD numbers, taken by work order 130.
        /// </summary>
        [Test, Timeout(3600000)] // the long sweeps' own time limit (as BossAloneTests')
        public void SwitchOff_TwoThousandFloorsAndAHundredRuns_AreVersion24s_FieldForField()
        {
            ulong h = Print(Off, Fold);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x5F07D15C837EAF2CUL; // version 24's, Unity's EditMode run (work order 130's filtered run, 2026-11-25)
#else
            const ulong pinned = 0x4B58BEAA19D7D624UL; // version 24's, .NET (the .NET rules run of work order 130, 2026-11-25)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("LINEUPSOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the line-ups off: version 24's floors, found " + found);
        }

        /// <summary>Version 25's pin with the lancers named on (the default). Each runtime has its own number: .NET's from the .NET rules run of this code, Unity's from a filtered Unity EditMode run of it.</summary>
        [Test, Timeout(3600000)]
        public void Version25_LancersOn_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOn, FoldLineUps);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x7E1247AA85BE5824UL; // Unity's EditMode run of this code (work order 135's third round, its filtered run, 2026-11-27)
#else
            const ulong pinned = 0x4B40316555E1456AUL; // .NET, this code (the .NET rules run of work order 135's third round, 2026-11-27)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("LINEUPSPRINT LANCERS ON " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 25's floors, the lancers on, found " + found);
        }

        /// <summary>
        /// With <see cref="LineUpValues.WeightMore"/> 0 the line-up rooms weigh what the second round's did: the same room's version-24 weight,
        /// a mean within 5 % a floor and at least 80 % of the rooms within the tolerance (three hundred seeds a floor). Not the second round's
        /// floors field for field: the third round widened some line-ups' counts to reach his 15 %, and the draw among the counts within the
        /// tolerance draws among more of them.
        /// </summary>
        [Test, Timeout(3600000)]
        public void WeightMoreZero_TheLineUpRooms_WeighWhatTodaysRoomsWeigh()
        {
            var v = new LevelValues();
            v.LineUpRules.WeightMore = 0f;
            var w = v.LineUpRules;
            for (int floor = 1; floor <= 5; floor++)
            {
                double off = 0;
                int rooms = 0, within = 0;
                for (ulong s = 0; s < 300; s++)
                {
                    List<LevelRoom> on = Chain(LevelGenerator.Generate(s, floor, v)), was = Chain(LevelGenerator.Generate(s, floor, Off));
                    for (int i = 0; i < on.Count; i++)
                    {
                        if (!LevelLineUps.Fills(on[i])) continue;
                        double target = w.WeightOf(was[i].EnemyGroups), weight = w.WeightOf(on[i].EnemyGroups);
                        Assert.That(w.TargetOf(target, floor), Is.EqualTo(target).Within(1e-9), "WeightMore 0: the target is today's weight");
                        off += (weight - target) / target;
                        rooms++;
                        if (Math.Abs(weight - target) <= w.WeightTolerance * target + 1e-9) within++;
                    }
                }
                TestContext.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "LINEUPWEIGHTZERO floor {0}: off by {1:+0.0%;-0.0%}, {2:P1} within", floor, off / rooms, within / (double)rooms));
                Assert.That(Math.Abs(off / rooms), Is.LessThanOrEqualTo(0.05), "floor " + floor);
                Assert.That(within / (double)rooms, Is.GreaterThanOrEqualTo(0.8), "floor " + floor);
            }
        }

        /// <summary>Version 25's pin with the lancers named off.</summary>
        [Test, Timeout(3600000)]
        public void Version25_LancersOff_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOff, FoldLineUps);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x3684204A10B0062AUL; // Unity's EditMode run of this code (work order 135's third round, its filtered run, 2026-11-27)
#else
            const ulong pinned = 0x780E5ADCFDF901EAUL; // .NET, this code (the .NET rules run of work order 135's third round, 2026-11-27)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("LINEUPSPRINT LANCERS OFF " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 25's floors, the lancers off, found " + found);
        }

        // ---------------- The golden maps of version 25 ----------------

        // Work order 135: what the generator of version 25 makes with its defaults, version 23's and 24's golden seeds, floors 1 and 5.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_KeepVersion25sMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(25), "the version was raised: update the golden maps and this number together");
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor, On));
            string key = seedText + "/" + floor;
            TestContext.WriteLine("GOLDEN25 " + key + "\n" + map);
            Assert.That(GoldenVersion25.ContainsKey(key), "no golden map for " + key + "\n" + map);
            Assert.That(map, Is.EqualTo(GoldenVersion25[key].Replace("\r\n", "\n")), "map of seed " + key + "\n" + map);
        }

        static readonly Dictionary<string, string> GoldenVersion25 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 25  17 rooms  53 enemies  148 x 88 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
                                                                +-+-+-+
                                                                |L8!  |
                                          +-+-+-+-+-+-+-+     +-+   p +-+
                                          |C1Bik k     u|     |    e    |
                                          +             +   +-+ [       +-+
                                          | :           |   | #       p   |
                                          +             +-+-+       #     +
                                          |      %       P      [     B  !|
                                          +         # +-+-+-+ +-+     +-+ +
                                          |           |     | | |     | | |
                                          +       h +-+     + +-+     +-+ +
                                          |      #  |       |   [   # =   |
                                          +       +-+       +             +
                                          |   h   |         |!            |
                                          +       +         +-+-+ +-+-+-+-+
                                          |       |             |P|
+-+-+-+-+-+-+-+-+-+-+       +-+-+-+-+-+   +-+-+ +-+             + +
|X16   :            |       |C1h      |       |P|               | |
+  h+-+   +-+#      +   +-+-+    h    +       + +               + +
|   | |   | |       |   |i    p       |       | |               | |
+-+ +-+   +-+h+-+   +   +Ao       #   +     +-+ +-+           +-+ +-+
  |           | |   |   |i h          |     |T12  |           |T6   |
+-+           +-+   +-+-+   #     :   +-+-+-+     +           +  $  +
|            B       P                 P         k|           |    k|
+                   +-+-+-+-+   #     +-+-+-+ i$i +           +    i+
|      p            |       |         |     |     |           |u   k|
+-+  :        +-+   +       +-+-+-+-+-+     +-+-+-+           +-+ +-+
  |        p  | |   |                                           |P|
+-+ +-+   +-+ +-+   +               +-+-+-+ +-+-+ +-+-+         + +
|   | |   | |       |               |C2   | |   | |k  |         | |
+   +-+   +-+       +               +     +-+   +-+   +   +-+-+-+ +-+-+
|             b  %  |               |k                |   |C4         |
+-+-+-+-+-+-+-+-+-+-+     +-+-+     +                 +   +   +-+ +-+ +-+
                          |E0*|     |      %      p   |   |   | | | |   |
                      +-+-+   +-+-+-+  :              +-+-+  #+-+ +-+#  +
                      |        P           b  p        P       # : # h *|
                      +   @   +-+-+-+     +-+   +-+   +-+-+           oA+
                      |       |     |     | | u | |   |   |     b      *|
                      +-+    *+     +-+-+-+ +-+-+ +-+-+   +        p  p +
                        |     |                           |    #        |
                        +-+-+-+                           +   +-+ +-+   +
                                                          |   | | | |   |
                                                          +   +-+ +-+ +-+
                                                          |           |
                                                          +-+-+-+-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 25  17 rooms  63 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2 u         |
        +-+-+   +-+-+ +          b  +
        |        P  | | %           |
        +   @   +-+ +-+    B    B   +
        |       | |          B     k|
        +-+    *+ +-+-+-+ #        i+
          |     |       |           |
          +-+-+-+       +-+  h      +
                          |h #:    k|
                          +-+  h  hi+
                            |h      |
                            +   :  k+
                            |       |
                            + +-+-+-+
                            |P|
                          +-+ +
                          |   |
                          + +-+
                          | |
                    +-+-+-+ +-+-+-+
                    |C4           |
                    + h         h +
                    |    #   #    |
                    + h         h +
                    |  h       h  |
                    +-+      h+-+-+
                      | h h   |
                      +-+     +
                        |h  h |
                    +-+-+     +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       |   g o    h  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| #           # |   |    *A       |   |Lh !h   &|
+ +-+       +-+ +   +-+-+-+-+ +-+-+   +   +-+h  +-+
| | |       | | |           |P|       |!  | |     |
+ +-+       +-+ +           + +       + h +-+h [ =+-+
|               |           | |       |             |
+       G       +         +-+ +-+     + #  h h     &+
|               |         |T6   |     |   #     e   |
+ +-+       +-+ +         +i    +-+-+-+      h      +
| | |       | | |         |k     P        h         |
+ +-+       +-+ +         +  $  +-+-+-+  [+-+  [ l+-+
|               |         |  u  |     |   | |h    |
+ #           # +         +-+-+-+     + # +-+   +-+
|               |                     |  h  h h |
+-+-+-+-+-+ +-+-+                     +-+-+-+ +-+
          |P|                               |P|
          + +-+                             + +-+
          |   |                             |   |
          +-+ +                             +-+ +
            | |                               | |
            + +-+-+ +-+-+-+             +-+-+-+ +-+-+-+-+-+
            |C14  | |     |             |C10            ki|
        +-+-+b B  +-+     +             +i             h B+
        |           # h   |             |   #             |
        +*  #             +   +-+-+-+   +# h    :        k+
        |A o  g      p    |   |T12 u|   |    %      B     |
        +*   c  p         +-+-+ i   +-+-+                 +
        |  #     #   : #   P    $    P     h h  h         |
        +-+-+     +-+     +-+-+ i   +-+-+   +-+     +-+   +
            |   h | |     |   |     |   |   | |   u | |   |
            +-+-+-+ +-+-+-+   +-+-+-+   +-+-+ +-+-+-+ +-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 25  17 rooms  49 enemies  136 x 136 m  themes C2 shrine, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
        +-+-+
        |E0 |
      +-+   +
      |     |
      +   @ +-+
      |       |
      +       +
      |*    * |
      +-+ +-+-+
        |P|
        + +
        | |
      +-+ +-+-+-+
      |C2       |
      +         +
      |  #      |
      +      #  +                                   +-+-+ +-+-+ +-+-+
      |         |                                   |X16| |   | |   |
      +-+     +-+                                   +   +-+   +-+h  +
        |     |                                     |               |
      +-+     +-+                                   + +-+     : +-+ +
      |         |                                   | | |       | | |
      +         +                                   + +-+      :+-+ +
      |  #   #  |                                   |       p       |
      +-+     +-+                                   +    p          +
        |  B  |                                     |               |
      +-+    #+-+                                   + +-+      b+-+ +
      |   h o   |                                   | | |       | | |
      +    *    +                                   + +-+       +-+ +
      |     A*  |                                   |               |
      +-+ +-+-+-+                                   +   +-+   +-+   +
        |P|                                         |   | |   | |   |
        + +                                         +   +-+   +-+   +
        | |                                         |           :   |
        + +                   +-+-+-+-+-+           +   %           +
        | |                   |L8       |           |               |
+-+-+-+-+ +-+-+               + # +-+   +-+         +-+-+-+-+ +-+-+-+
|C4           |               |   | |B    |                 |P|
+   p         +               +  [+-+     +-+             +-+ +
|             |               |         p   |             |   |
+ +-+    q+-+ +     +-+-+-+   +            #+             + +-+
| | |     | | |     |T6k k|   |     #       |             | |
+ +-+     +-+ +-+-+-+     +-+-+           e&+       +-+-+-+ +-+
|              P        $  P            p  &|       |C14Ai    |
+ +-+  p  +-+ +-+-+-+     +-+-+  =+-+[    +-+     +-+         +-+
| | |:    | | |     |   u |   |   | |     |       |   # o       |
+ +-+     +-+ +     +-+-+-+   +   +-+   +-+       +        p #  +
|      b      |               |         |         |  b          |
+-+  h      +-+               +-+-+-+ +-+         +             +
  |   ~~~^  |                       |P|           |             |
  +-+-+-+-+-+                     +-+ +           + #    #    # +
                                  |   |           |          p  |
                                  + +-+           +           +-+
                                  | |             |           |
                                +-+ +-+           +-+-+ +-+-+-+
                                |C10  |               |P|
                                +     +-+             + +
                                |       |             | |
                                +    # #+           +-+ +-+
                                |       |           |T12 u|
                                +   B   +-+-+-+-+-+-+   i +
                                |    %         P        $ |
                                +       :     +-+-+-+   i +
                                |          :  |     |     |
                                +             +     +-+-+-+
                                |          h i|
                                +            k+
                                |k      u     |
                                +-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 25  17 rooms  69 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
                                        +-+-+
                                        |E0 |
                                      +-+   +
                                      |     |
                                      +   @ +-+
                                      |       |
                                      +       +
                                      |*    * |
                                      +-+ +-+-+
                                        |P|
                                        + +
                                        | |
                                      +-+ +-+-+-+
                                      |C2    k  |
                                      +i       b+
                                      |         |
                                      +    #  # +
                                      |      h  |
                                      +-+B  # +-+
                                        |     |
                                      +-+     +-+
                                      |  %   : k|
                +-+-+-+   +-+-+-+     +       h +
                |C4  !|   |h h  |     |   B     |
    +-+-+-+     +&    +   +     + +-+-+      h  +
    |T6k  |     |     |   |   # | |P            |
    +     +-+-+-+    [+-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   |     |
    +     +-+-+-+    g   h      +-+-+   +     +
    |     |     |  =         #  |       |     |
    +-+ +-+     +        h      +       +-+-+-+
      |P|       |      h   h    |
      + +       +  h  +-+= # h h+
      | |       |   #h| |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |h  h |
    +     +     +-+-+-+   +-+-+-+
    |   : |
+-+-+b    +-+-+     +-+-+-+-+-+-+-+
| h    B  q h |     |C10!!      &!|
+       : h   +-+-+ +     B   [  B+
|h h  :        P  | |   =         |
+       e     +-+ +-+       B   # +
|     B    B  | |        p #  [   |
+-+-+     +-+-+ +-+-+-+           +
    |h    |           |           |
    +     +           +-+-+B  [  !+
    | ^~~ |               |       |
    +-+-+-+               +B   p !+
                          |       |
                          +  p    +
                          |     # |
                          +   =   +                 +-+-+-+-+-+-+-+
                          |       |                 |X16          |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     +   +-+   +-+ +-+
                          | |     |C14  w     |     |               |
                        +-+ +-+   +%  #   +-+ +     +               +
                        |T12  |   |     c | | |     |               |
                        +     +-+-+       +-+ +-+-+ +       G      o+
                        |      P        :   b  P  | |              Y|
                        + i$i +-+-+       +-+ +-+ +-+              o+
                        |     |   |     g | | | |                   |
                        +-+-+-+   +   B   +-+ + +-+-+   +-+   +-+ +-+
                                  |         hi|     |   | |   | | |
                                  +   : p h   +     + # +-+   +-+#+
                                  |u         l|     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };
    }
}
