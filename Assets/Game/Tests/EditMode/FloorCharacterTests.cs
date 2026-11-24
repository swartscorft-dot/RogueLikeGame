using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// A dominant thing a floor (work order 104; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "én ny ting å lære per
    /// etasje, en dominerende fiendemiks og tema per etasje"), the generator's version 21: the switch (<see cref="LevelValues.FloorCharacter"/>)
    /// on by default; version 20 with it off and version 21's own pins; the act's table, row by row; the dominant mix drawn twice as
    /// often and the dominant theme three times as likely, over a thousand seeds a floor within named tolerances; no elite of a kind on
    /// the kind's own floor; no more than three new combat templates a floor; the floors' names. The table before and after and the
    /// share of rooms that show each floor's new thing are printed ("ACTTABLE", "NEWTHING").
    /// </summary>
    public class FloorCharacterTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static LevelValues On => new LevelValues();
        static LevelValues Off => new LevelValues { FloorCharacter = false, Lancers = false };

        /// <summary>How far a measured share may lie from the one its weights give (a thousand seeds a floor, five to eight rooms each: about 1.5 points of noise).</summary>
        const double MixShareTolerance = 0.05;
        /// <summary>
        /// How much the floor's theme's share of the combat rooms rises at least, in points of the share: its weight is three, but the
        /// themes' rule that no theme stands in two combat rooms in a row (RoomThemes.Assign) caps it at three rooms of five, and the
        /// rooms' size weights and templates' suits pull it about; measured 8.9 (floor 1) to 12.1 (floor 4) points over a thousand seeds.
        /// </summary>
        const double ThemeShareRiseAtLeast = 0.08;

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

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
            }
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

        /// <summary>Two thousand floors (seeds s * 7919 + 13, floors 1 to 5 in turn) and a hundred runs of five, folded (as ExitRoomsInTheFloorsTests folds version 20's).</summary>
        static ulong Print(LevelValues v)
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = Fold(LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v), h);
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5)) h = Fold(l, h);
            return h;
        }

        // ---------------- The switch and the pins ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion21()
        {
            Assert.That(new LevelValues().FloorCharacter, Is.True, "on by default (work order 104)");
            Assert.That(LevelGenerator.Version, Is.GreaterThanOrEqualTo(21), "21, and 22 since work order 118");
            Assert.That(FloorCharacterRules.NameOf(2, On), Is.EqualTo("The Guard Halls"));
            Assert.That(FloorCharacterRules.NameOf(2, Off), Is.EqualTo(""), "off: no name");
        }

        /// <summary>Version 21's own pin: the same floors and runs with version 21's defaults (since work order 118 the lancers named off). Each runtime has its own number: .NET's from the .NET rules run of this code, Unity's from a filtered Unity EditMode run.</summary>
        [Test]
        public void Version21_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(new LevelValues { Lancers = false }); // work order 118 (changed deliberately): version 21 is the lancers off
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x524BEFDC904EAF79UL; // Unity's EditMode run of this code (work order 104's filtered run, 2026-11-24)
#else
            const ulong pinned = 0x7220A3733724AE81UL; // .NET, this code (the .NET rules run of work order 104)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("CHARACTERPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 21's floors, found " + found);
        }

        // ---------------- The act's table ----------------

        static readonly string[] CombatTemplates = RoomTemplateLibrary.All.Where(t => t.For == RoomTemplateFor.Combat && t.Since < RoomTemplateLibrary.MoreSince).Select(t => t.Name).ToArray();

        static string NewTemplates(int floor, LevelValues v, RoomTemplateFor kind) =>
            string.Join(", ", RoomTemplateLibrary.Usable(v).Where(t => t.For == kind && ExitRooms.Allowed(t, floor, v) && FloorCharacterRules.FirstFloorOf(t, v) == floor
                                                                       || (t.For == kind && kind == RoomTemplateFor.Exit && ExitRooms.Allowed(t, floor, v) && !ExitRooms.Allowed(t, floor - 1, v) && floor > 1 && FloorCharacterRules.FirstFloorOf(t, v) <= floor))
                                                         .Select(t => t.Name));

        static string Kinds(EnemyKind[] kinds) => string.Join(" ", kinds.Select(EnemyKinds.Word));

        static string Row(int floor, LevelValues v)
        {
            FloorEnemies f = v.EnemiesOf(floor);
            v.EnemyRange(RoomKind.Combat, floor, out int cf, out int cm);
            v.EnemyRange(RoomKind.Exit, floor, out int xf, out int xm);
            string theme = FloorCharacterRules.DominantTheme(floor, v) == RoomTheme.None ? "-" : RoomThemes.Word(FloorCharacterRules.DominantTheme(floor, v));
            return string.Format(Inv, "floor {0} | {1} | guards {2}, casters {3} | elite {4} | new combat {5} | new elite {6} | new exit {7} | {8}-{9} / {10}-{11} | mixes {12} | dominant mix {13}, theme {14}",
                                 floor, FloorCharacterRules.NameOf(floor, v), v.GuardsMost(RoomKind.Combat, floor), v.CastersMost(RoomKind.Combat, floor), Kinds(v.EliteKinds(floor)),
                                 NewTemplates(floor, v, RoomTemplateFor.Combat), NewTemplates(floor, v, RoomTemplateFor.Elite), NewTemplates(floor, v, RoomTemplateFor.Exit),
                                 cf, cm, xf, xm, string.Join(", ", f.Mixes.Select(m => m.Name + " " + FloorCharacterRules.MixWeight(m, floor, v).ToString("0", Inv))),
                                 FloorCharacterRules.DominantMix(floor, v).Length == 0 ? "-" : FloorCharacterRules.DominantMix(floor, v), theme);
        }

        /// <summary>The act's table, a row a floor, before (the switch off) and after (on), printed; and each row of after as the order says it.</summary>
        [Test]
        public void TheActsTable_RowByRow()
        {
            var lines = new List<string> { "ACTTABLE before (the switch off)" };
            for (int floor = 1; floor <= 5; floor++) lines.Add("ACTTABLE   " + Row(floor, Off));
            lines.Add("ACTTABLE after (on)");
            for (int floor = 1; floor <= 5; floor++) lines.Add("ACTTABLE   " + Row(floor, On));
            TestContext.WriteLine(string.Join("\n", lines));

            LevelValues v = On;
            string[] names = { "The Upper Tombs", "The Guard Halls", "The Shrine of Wards", "The Ossuary", "The Knight's Hall" };
            string[] mixes = { "swarm", "guarded swarm", "crossfire", "crossfire", "guarded" };
            RoomTheme[] themes = { RoomTheme.TombHall, RoomTheme.Archive, RoomTheme.Shrine, RoomTheme.Ossuary, RoomTheme.TombHall };
            var elites = new[]
            {
                new[] { EnemyKind.Brute },
                new[] { EnemyKind.Brute, EnemyKind.Shooter },
                new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard },
                new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster },
                new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster },
            };
            string[][] newCombat = { new[] { "nave", "corner", "colonnade" }, new[] { "corner-long", "twin", "transept" }, new[] { "nave-apse" }, new string[0], new string[0] };
            for (int floor = 1; floor <= 5; floor++)
            {
                string where = "floor " + floor;
                Assert.That(FloorCharacterRules.NameOf(floor, v), Is.EqualTo(names[floor - 1]), where);
                Assert.That(FloorCharacterRules.DominantMix(floor, v), Is.EqualTo(mixes[floor - 1]), where);
                Assert.That(v.EnemiesOf(floor).Mixes.Count(m => m.Name == mixes[floor - 1]), Is.EqualTo(1), where + ": the dominant mix is one of the floor's three");
                Assert.That(v.EnemiesOf(floor).Mixes.Length, Is.EqualTo(3), where);
                Assert.That(FloorCharacterRules.DominantTheme(floor, v), Is.EqualTo(themes[floor - 1]), where);
                Assert.That(v.EliteKinds(floor), Is.EqualTo(elites[floor - 1]), where);
                var fresh = RoomTemplateLibrary.Usable(v).Where(t => t.For == RoomTemplateFor.Combat && FloorCharacterRules.FirstFloorOf(t, v) == floor).Select(t => t.Name).ToList();
                Assert.That(fresh, Is.EquivalentTo(newCombat[floor - 1]), where + ": its new combat templates");
                Assert.That(fresh.Count, Is.LessThanOrEqualTo(3), where + ": no more than three new combat templates");
                // The counts of enemies and what they deal are not this order's: as with the switch off.
                v.EnemyRange(RoomKind.Combat, floor, out int a, out int b);
                Off.EnemyRange(RoomKind.Combat, floor, out int c, out int d);
                Assert.That((a, b), Is.EqualTo((c, d)), where + ": the counts unchanged");
                Assert.That(v.DamageScaleOf(floor), Is.EqualTo(Off.DamageScaleOf(floor)), where + ": what they deal unchanged");
                Assert.That(v.CastersMost(RoomKind.Combat, floor), Is.EqualTo(Off.CastersMost(RoomKind.Combat, floor)), where);
                Assert.That(v.GuardsMost(RoomKind.Combat, floor), Is.EqualTo(Off.GuardsMost(RoomKind.Combat, floor)), where);
            }
            // The kinds' floors, as the act's plan put them (work order 087): the guard 2, the caster and the warder 3, the bomber and the fan 4.
            Assert.That(v.GuardsFromFloor, Is.EqualTo(2));
            Assert.That(v.CastersFromFloor, Is.EqualTo(3));
            Assert.That(v.WarderFloors.FromFloor, Is.EqualTo(3));
            Assert.That(v.BomberFloors.FromFloor, Is.EqualTo(4));
            Assert.That(new ShooterValues().FanFromFloor, Is.EqualTo(4));
            // Every template of the first set has a first floor, and none was changed: the templates' own are as the library writes them.
            Assert.That(RoomTemplateLibrary.All.First(t => t.Name == "corner-long").FromFloor, Is.EqualTo(1), "the template itself unchanged");
            Assert.That(RoomTemplateLibrary.All.First(t => t.Name == "nave-apse").FromFloor, Is.EqualTo(1));
            Assert.That(CombatTemplates.Length, Is.EqualTo(7));
        }

        // ---------------- The weights ----------------

        /// <summary>The draw alone: a hundred thousand draws of each floor's mix, the dominant one half of them (2 of 4), each other a quarter.</summary>
        [Test]
        public void TheMixDraw_TheDominantMixHalfTheDraws_EachOtherAQuarter()
        {
            LevelValues v = On;
            for (int floor = 1; floor <= 5; floor++)
            {
                EnemyMix[] mixes = v.EnemiesOf(floor).Mixes;
                var count = mixes.ToDictionary(m => m, m => 0);
                var rng = LevelRandom.Stream(0x104UL, "test mixes", floor);
                const int draws = 100000;
                for (int i = 0; i < draws; i++) count[FloorCharacterRules.PickMix(mixes, floor, rng, v)]++;
                foreach (EnemyMix m in mixes)
                {
                    double want = m.Name == FloorCharacterRules.DominantMix(floor, v) ? 0.5 : 0.25;
                    Assert.That(count[m] / (double)draws, Is.EqualTo(want).Within(0.01), "floor " + floor + " " + m.Name);
                }
            }
            // Off: as the generator always drew it, one in three each, from the same draw.
            var off = LevelRandom.Stream(0x104UL, "test mixes off");
            var same = LevelRandom.Stream(0x104UL, "test mixes off");
            EnemyMix[] two = Off.EnemiesOf(2).Mixes;
            for (int i = 0; i < 100; i++) Assert.That(FloorCharacterRules.PickMix(two, 2, off, Off), Is.SameAs(two[same.Below(two.Length)]));
        }

        sealed class Share
        {
            public int Rooms, Shows, Themed, Dominant;
            public readonly Dictionary<RoomTheme, int> Themes = new Dictionary<RoomTheme, int>();
        }

        /// <summary>Does a room show its floor's new thing? Floor 1: a swarmer, a brute and a shooter; 2: a guard; 3: a caster or a warder; 4: a bomber (the fan: every shooter of floor 4 has it, counted apart); 5: the act boss.</summary>
        static bool ShowsTheNewThing(LevelRoom r, int floor)
        {
            bool Has(EnemyKind k) => r.EnemyGroups.Any(g => g.Kind == k && g.Count > 0);
            switch (floor)
            {
                case 1: return Has(EnemyKind.Swarmer) && Has(EnemyKind.Brute) && Has(EnemyKind.Shooter);
                case 2: return Has(EnemyKind.Guard);
                case 3: return Has(EnemyKind.Caster) || Has(EnemyKind.Warder);
                case 4: return Has(EnemyKind.Bomber);
                default: return r.EnemyGroups.Any(g => g.Boss);
            }
        }

        static Share[] Count(LevelValues v, int seeds)
        {
            var shares = new Share[6];
            for (int floor = 1; floor <= 5; floor++)
            {
                var s = shares[floor] = new Share();
                RoomTheme dominant = FloorCharacterRules.DominantTheme(floor, On);
                for (ulong k = 0; k < (ulong)seeds; k++)
                {
                    LevelLayout l = LevelGenerator.Generate(k * 6151UL + 104UL, floor, v);
                    foreach (LevelRoom r in l.Rooms.Where(x => x.Kind == RoomKind.Combat || x.Kind == RoomKind.Exit))
                    {
                        s.Rooms++;
                        if (ShowsTheNewThing(r, floor)) s.Shows++;
                        if (r.Kind != RoomKind.Combat) continue;
                        s.Themed++;
                        if (r.Theme == dominant) s.Dominant++;
                        s.Themes[r.Theme] = s.Themes.TryGetValue(r.Theme, out int n) ? n + 1 : 1;
                    }
                }
            }
            return shares;
        }

        /// <summary>
        /// A thousand seeds a floor, before and after: the share of the fights' rooms (combat and exit) that show the floor's new thing,
        /// and of the combat rooms that are the floor's theme; floor 2's rooms with a guard are the guarded swarm's (no other mix of floor
        /// 2 holds one), a third of them before and half after, within <see cref="MixShareTolerance"/>; the floor's theme's share at least
        /// <see cref="ThemeShareRiseAtLeast"/> higher than it was, and the floor's most common theme after. Prints "NEWTHING".
        /// </summary>
        [Test, Timeout(3600000)]
        public void AThousandSeedsAFloor_TheDominantMixAndTheme_WithinTheirTolerances_AndTheShareThatShowsTheNewThing()
        {
            Share[] before = Count(Off, 1000), after = Count(On, 1000);
            var lines = new List<string> { "NEWTHING a thousand seeds a floor: the fights' rooms that show the floor's new thing, and the combat rooms of its theme; before -> after" };
            for (int floor = 1; floor <= 5; floor++)
            {
                Share b = before[floor], a = after[floor];
                lines.Add(string.Format(Inv, "NEWTHING floor {0} ({1}): new thing {2:P1} -> {3:P1} of {4} rooms; {5} {6:P1} -> {7:P1} of {8} combat rooms; themes after {9}",
                                        floor, FloorCharacterRules.NameOf(floor, On), b.Shows / (double)b.Rooms, a.Shows / (double)a.Rooms, a.Rooms,
                                        RoomThemes.Word(FloorCharacterRules.DominantTheme(floor, On)), b.Dominant / (double)b.Themed, a.Dominant / (double)a.Themed, a.Themed,
                                        string.Join(", ", a.Themes.OrderByDescending(kv => kv.Value).Select(kv => RoomThemes.Word(kv.Key) + " " + (kv.Value / (double)a.Themed).ToString("P1", Inv)))));
            }
            TestContext.WriteLine(string.Join("\n", lines));

            // Floor 2: the guard's rooms are the guarded swarm's, which was one of three and is now half the draws. The elite guard is
            // floor 3's now, so with the switch on a guard on floor 2 is the mix's; before, an elite guard counts too: it is measured on
            // the combat rooms' groups that are not elite.
            double Guarded(LevelValues v)
            {
                int rooms = 0, guarded = 0;
                for (ulong k = 0; k < 1000; k++)
                    foreach (LevelRoom r in LevelGenerator.Generate(k * 6151UL + 104UL, 2, v).Rooms.Where(x => x.Kind == RoomKind.Combat))
                    {
                        rooms++;
                        if (r.EnemyGroups.Any(g => g.Kind == EnemyKind.Guard && !g.Elite)) guarded++;
                    }
                return guarded / (double)rooms;
            }
            Assert.That(Guarded(Off), Is.EqualTo(1.0 / 3).Within(MixShareTolerance), "floor 2 before: the guarded swarm one of three");
            Assert.That(Guarded(On), Is.EqualTo(0.5).Within(MixShareTolerance), "floor 2 after: the guarded swarm half the draws");
            for (int floor = 1; floor <= 5; floor++)
            {
                Share b = before[floor], a = after[floor];
                double pb = b.Dominant / (double)b.Themed, pa = a.Dominant / (double)a.Themed;
                Assert.That(pa - pb, Is.GreaterThanOrEqualTo(ThemeShareRiseAtLeast), "floor " + floor + ": its theme's share rises");
                Assert.That(a.Themes.OrderByDescending(kv => kv.Value).First().Key, Is.EqualTo(FloorCharacterRules.DominantTheme(floor, On)), "floor " + floor + ": its theme the most common");
            }
        }

        // ---------------- Elites and templates over the floors ----------------

        /// <summary>
        /// A thousand seeds a floor: no elite guard on floor 2 nor an elite caster on floor 3 (a kind's elite never on the kind's own
        /// floor, but the brute's on floor 1); no combat room draws a template before its first floor; every floor is made.
        /// </summary>
        [Test, Timeout(3600000)]
        public void AThousandSeedsAFloor_NoEliteOnItsKindsOwnFloor_NoTemplateBeforeItsFloor()
        {
            LevelValues v = On;
            for (int floor = 1; floor <= 5; floor++)
                for (ulong k = 0; k < 1000; k++)
                {
                    LevelLayout l = LevelGenerator.Generate(k * 3571UL + 21UL, floor, v);
                    string where = l.SeedText + " floor " + floor;
                    EnemyGroup elite = l.Rooms.SelectMany(r => r.EnemyGroups).FirstOrDefault(g => g.Elite);
                    Assert.That(elite, Is.Not.Null, where + ": the elite room's elite");
                    Assert.That(v.EliteKinds(floor), Does.Contain(elite.Kind), where);
                    if (floor == 2) Assert.That(elite.Kind, Is.Not.EqualTo(EnemyKind.Guard), where + ": no elite guard on the guard's floor");
                    if (floor == 3) Assert.That(elite.Kind, Is.Not.EqualTo(EnemyKind.Caster), where + ": no elite caster on the caster's floor");
                    foreach (LevelRoom r in l.Rooms.Where(x => x.Template != null))
                        Assert.That(FloorCharacterRules.FirstFloorOf(r.Template.Template, v), Is.LessThanOrEqualTo(floor), where + " " + r.Template.Template.Name);
                }
        }

        // ---------------- The names ----------------

        [Test]
        public void TheRunRecord_NamesTheFloor_UnderItsNumber()
        {
            var r = new RunRecord("ABCDEFGH", 5);
            r.EnterFloor(2, 8);
            Assert.That(r.FloorName, Is.EqualTo(""), "no name until it is named");
            r.NameFloor(FloorCharacterRules.NameOf(2, On));
            Assert.That(r.FloorText, Is.EqualTo("Floor 2 of 5"), "the number as before");
            Assert.That(r.FloorName, Is.EqualTo("The Guard Halls"));
            r.EnterFloor(3, 8);
            Assert.That(r.FloorName, Is.EqualTo(""), "a new floor: named anew");
            r.NameFloor(null);
            Assert.That(r.FloorName, Is.EqualTo(""));
        }

        // ---------------- The golden maps of version 20 ----------------

        // Work order 104: with the floors' character off the floors are version 20's, so the golden seeds keep version 20's maps
        // (LevelGeneratorTests' maps of version 20, as work order 103 left them), but for the version number.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_WithTheFloorsCharacterOff_KeepVersion20sMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor, Off)).Replace("version " + LevelGenerator.Version + " ", "version 20 ");
            string key = seedText + "/" + floor;
            Assert.That(map, Is.EqualTo(GoldenVersion20[key].Replace("\r\n", "\n")), "map of seed " + key + " with the floors' character off\n" + map);
        }

        // The maps of version 20 (LevelGeneratorTests' Golden after work order 103), copied unchanged.
        static readonly Dictionary<string, string> GoldenVersion20 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 20  17 rooms  57 enemies  172 x 68 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 shrine, T12 archive, C14 archive, X16 exit hall  templates C2 nave-apse, C4 corner-long, L8 knights-apse, C10 colonnade, C14 nave, X16 stair-head
+-+-+-+-+-+-+-+-+-+-+
|X16           H    |
+   +-+-+  :+-+-+   +                       +-+-+-+-+-+
|   |   |   |   |   |                       |L8   [   |
+   +   +   +   +:  +                     +-+   +-+   +               +-+-+-+-+-+-+-+-+
|   |   |   |   |   |                     |     | |  #|               |C4    iAi      |
+   +-+-+   +-+-+   +                   +-+     +-+   +               +               +
| p            B    |                   |   =         |               |   h   o       |
+                   +                   + #   B       +   +-+-+-+     +           #   +
|             #  %  |                   |  e      =   |   |T6k k|     |   #  :b       |
+                   +                   +      p      +-+-+     +-+-+-+    p          +
|  p                |                   |   p          P    $    P               #    |
+         #         +                   +-+     +-+   +-+-+     +-+-+-+-+-+-+-+ p     +
|                   |                     | h # | |   |   |  u  |             |       |
+  :                +                     +-+   +-+ # +   +-+-+-+             +#   #  +
|     u             |                       |     [   |                       |       |
+-+-+-+-+ +-+-+-+-+-+                       +-+ +-+-+-+                       +-+     +
        |P|                                   |P|                               |     |
      +-+ +                                   + +                               + +-+-+
      |   |                                   | |                               |P|
      + +-+                           +-+-+-+-+ +-+-+                           + +
      | |                             |C10          |                           | |
    +-+ + +-+-+ +-+-+-+               +             +               +-+-+-+ +-+-+ +
    |C14| |   | |     |               |          #  |               |C2i  | |     |
    +   +-+   +-+    u+     +-+-+-+   + +-+     +-+ +               +     +-+     +-+-+
    |k                |     |T12ik|   | | |# p #| | |               |                 |
    +                 +-+-+-+     +-+-+ +-+     +-+ +     +-+-+     +                 +
    |     B    : p     P      $    P     B          |     |E0*|     |  :      :   p   |
    +i                +-+-+-+     +-+-+ +-+     +-+ + +-+-+   +-+-+-+                 +
    |k     %     #    |     |    u|   | | |# : #| | | |        P         # B %   h    |
    +  h+-+   +-+     +     +-+-+-+   + +-+     +-+ + +   @   +-+-+-+     +-+ #   +-+-+
    |   | |   | |    u|               |      o      | |       |     |     | |    u|
    +-+-+ +-+-+ +-+-+-+               +-+ h     p +-+ +-+    *+     +-+-+-+ +-+-+-+
                                        |   iAi   |     |     |
                                        +-+-+-+-+-+     +-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 20  17 rooms  94 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p  +
        |        P  | | %           |
        +   @   +-+ +-+    p  # B   +
        |       | |          g      |
        +-+    *+ +-+-+-+ #         +
          |     |       |      :    |
          +-+-+-+       +-+  :      +
                          |     b  k|
                          +-+     Hi+
                            |       |
                            +   c  k+
                            |       |
                            + +-+-+-+
                            |P|
                          +-+ +
                          |   |
                          + +-+
                          | |
                    +-+-+-+ +-+-+-+
                    |C4           |
                    +             +
                    |    #   #    |
                    +           B +
                    |          #  |
                    +-+       +-+-+
                      |       |
                      +-+     +
                        |   p |
                    +-+-+     +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       |   c o    H  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| w c     c   # |   | p  *A      c|   |L8 !x   &|
+ +-+ H     +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |    p  | | |           |P|       |b  | |     |
+ +-+b      +-+ +           + +       +  [+-+=c  x+-+
|  :          # |           | |       |             |
+       G   p   +         +-+ +-+     +    #  #    &+
|c            x |         |T6   |     |         e   |
+ +-+x     B+-+ +         +i    +-+-+-+     p  #   w+
| | |   p   | | |         |k     P           =      |
+ +-+       +-+ +         +  $  +-+-+-+  [+-+    B+-+
|               |         |  u  |     |   | | H   |
+ #           # +         +-+-+-+     +   +-+   +-+
|               |                     |     g   |
+-+-+-+-+-+ +-+-+                     +-+-+-+ +-+
          |P|                               |P|
          + +-+                             + +-+
          |   |                             |   |
          +-+ +                             +-+ +
            | |                               | |
            + +-+-+ +-+-+-+             +-+-+-+ +-+-+-+-+-+
            |C14  | |     |             |C10            ki|
        +-+-+c B  +-+     +             +i                +
        |             #   |             |   :             |
        +*  #             +   +-+-+-+   +%      :        k+
        |A o  g      p    |   |T12 u|   | # g#      b     |
        +*   H  p         +-+-+ i   +-+-+              c  +
        |  #      #    #   P    $    P     H B  p p       |
        +-+-+    b+-+     +-+-+ i   +-+-+   +-+     +-+   +
            |     | |     |   |     |   |   | |   u | |   |
            +-+-+-+ +-+-+-+   +-+-+-+   +-+-+ +-+-+-+ +-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 20  17 rooms  58 enemies  140 x 100 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave-apse, C4 corner-long, L8 knights-apse, C10 corner, X16 gate-hall
                                            +-+-+-+
                                            |L8^~~|
                                          +-+ e   +-+
                                          |     p   |
                      +-+-+-+-+-+-+-+   +-+         +-+
                      |C10   k   u  |   |       :   h |
                      +k            +-+-+             +
                      |i  :    B     P           :   u|
                      +             +-+-+ +-+     +-+ +
                      |k     %  #   |   | | | p   | | |
                      +   p     # +-+   + +-+    q+-+ +
                      |           |     |             |
                      +       +-+-+     +             +       +-+-+
                      |    p  |         |q          b |       |E0 |
                      + h     +         +-+-+-+ +-+-+-+     +-+   +
                      |       |               |P|           |     |
                      +       +               + +           +   @ +-+
                      |u      |               | |           |       |
                      +-+-+-+ +             +-+ +-+         +       +
                            |P|             |T6  u|         |*    * |
                            + +             +  $ k+         +-+ +-+-+
                            | |             |     |           |P|
                          +-+ +-+           +     +           + +
                          |T12  |           |     |           | |
                          +     +           +-+ +-+         +-+ +-+-+-+
                          |     |             |P|           |C2    k  |
                          +ki$i +             + +           +i       b+
                          |     |             | |           |         |
                          +-+ +-+       +-+-+-+ +           +         +
                            |P|         |C4 !   |           |   #  p  |
                            + +         +       +           +-+     +-+
                            | |         |    #  |             |     |
                          +-+ +-+-+-+   +! h   =+           +-+     +-+
                          |C14      |   |       |           |# %   : k|
+-+-+-+-+-+-+-+-+-+-+     +         +-+ +   [ = +           +         +
|X16     u          |     |     #     | |  p    |           |   p     |
+   +-+   +-+       +     +           + +       +-+-+   +-+-+      h  +
|   | | % | |       |     |        #  | |   [ b   = |   |P            |
+-+ +-+   +-+ +-+   +     +  #  b   p + +           +-+-+ +-+-+     +-+
  |           | |   |     |           | |      =  [       |   |     |
+-+           +-+   + +-+-+     p :   + +             +-+-+   +     +
|       B           | |P              | |    #        |       |     |
+ p   p             +-+ +-+   #   #   + +! p     # # &+       +-+-+-+
|                       | |  : o      | |             |
+-+           +-+   +-+-+ +-+    H  +-+ +-+-+-+-+-+-+-+
  |    :      | |   |       | iAi   |
+-+h+-+   +-+ +-+   +       +-+-+-+-+
|   | |   | |       |
+   +-+   +-+:      +
|           b       |
+-+-+-+-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 20  17 rooms  96 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 shrine, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
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
                                      +i        +
                                      |         |
                                      +  #      +
                                      |      p  |
                                      +-+B    +-+
                                        |     |
                                      +-+     +-+
                                      |# %   : k|
                +-+-+-+   +-+-+-+     +         +
                |C4  !|   |   ! |     |         |
    +-+-+-+     +&    +   +#    + +-+-+   c  H  +
    |T6k  |     |  c  |   |   # | |P            |
    +     +-+-+-+    [+-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    g          +-+-+   +    c+
    |     |     |  =       p    |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |          p    |
      + +       +  H  +-+=   B [+
      | |       |    w| |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |   b |
    +     +     +-+-+-+   +-+-+-+
    | q : |
+-+-+     +-+-+     +-+-+-+-+-+-+-+
|      x  q x |     |C10          |
+         b   +-+-+ +     g      c+
| p p :        P  | |             |
+       e     +-+ +-+     # b#    +
|     B    g  | |        p #      |
+-+-+     +-+-+ +-+-+-+           +
    |H    |           |          *|
    +   c +           +-+-+B   o A+
    | ^~~ |               |      *|
    +-+-+-+               +x      +
                          |   #  x|
                          +  p    +
                          |       |
                          +    H  +                 +-+-+-+-+-+-+-+
                          |       |                 |X16  c       |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     + b +-+   +-+ +-+
                          | |     |C14       k|     |         w     |
                        +-+ +-+   +   :   +-+ +     +  p        c   +
                        |T12  |   |     p | | |     |         p   B |
                        +     +-+-+       +-+ +-+-+ +       G       +
                        |      P            c  P  | |              Y|
                        + i$i +-+-+  #    +-+ +-+ +-+         p    o+
                        |     |   |     B | | | |                H  |
                        +-+-+-+   +   p   +-+ + +-+-+   +-+  g+-+ +-+
                                  |u     %  H |     |   | | # | | |
                                  +    #  c   +     + # +-+   +-+#+
                                  |      :   w|     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };
    }
}
