using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The eight large props in the rooms' compositions (work order 052; the concept pictures, Kristian's choice of 2026-11-20,
    /// "build toward them"): each is in the props' table at the order's size; over a thousand seeds per floor number each
    /// theme's large prop is placed by its rule or left out for a stated reason, and nothing of them stands on a doorway, the
    /// lane, the middle, an enemy's place or her start, or overlaps another prop; banners hang on a plain wall, clear of
    /// doorways and their signs; the sources are the great braziers, the great altar and the candle clusters; with the switch
    /// off the plan is work order 044's; generation is as quick as it was. The numbers are written as "LARGESTATS".
    /// </summary>
    public class LargePropsPlanTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The order's table: across (x), deep (z) and high, in metres, as made in Blender.</summary>
        static readonly Dictionary<PropModel, (double x, double z, double h)> s_table = new Dictionary<PropModel, (double, double, double)>
        {
            [PropModel.KnightStatue] = (1.38, 1.40, 3.00), [PropModel.GreatAltar] = (3.00, 1.50, 1.82), [PropModel.Dais] = (3.98, 4.00, 0.60),
            [PropModel.Banner] = (1.48, 0.22, 2.50), [PropModel.GreatBrazier] = (1.24, 1.23, 1.50), [PropModel.CoinHeap] = (2.00, 1.84, 0.63),
            [PropModel.BoneWall] = (3.00, 0.50, 2.50), [PropModel.CandleCluster] = (0.64, 0.65, 1.00),
        };

        static LevelValues Off() => new LevelValues { LargeProps = false };

        /// <summary>The large prop each theme is about (the banner for a room that has none of its own).</summary>
        static PropModel MainOf(LevelRoom r) =>
            r.Theme == RoomTheme.ThroneRoom ? PropModel.Dais : r.Theme == RoomTheme.TombHall ? PropModel.KnightStatue : r.Theme == RoomTheme.Shrine ? PropModel.GreatAltar
            : r.Theme == RoomTheme.Vault || (r.Theme == RoomTheme.Archive && r.Kind == RoomKind.Treasure) ? PropModel.Dais
            : r.Theme == RoomTheme.Ossuary ? PropModel.BoneWall : PropModel.Banner;

        static string Key(LevelRoom r) => RoomThemes.Word(r.Theme) + (r.Theme == RoomTheme.Archive ? (r.Kind == RoomKind.Treasure ? " (treasure)" : " (combat)") : "");

        static void Count<T>(Dictionary<T, int> d, T key, int by = 1)
        {
            d.TryGetValue(key, out int n);
            d[key] = n + by;
        }

        // ---------------- The table ----------------

        [Test]
        public void TheEight_AreInTheTable_AtTheOrdersSizes_AndNoThemesRecipeNamesThem()
        {
            PropModel[] all = Enum.GetValues(typeof(PropModel)).Cast<PropModel>().ToArray();
            Assert.That(all.Where(m => PropSize.Of(m).Large), Is.EquivalentTo(s_table.Keys), "the eight, and only they, are large");
            Assert.That(all.Take(18).All(m => !PropSize.Of(m).Large), "the eighteen before them keep their numbers");
            Assert.That(PropSize.Crypt.Select(s => s.Letter).Distinct().Count(), Is.EqualTo(PropSize.Crypt.Length), "each its own letter");
            foreach (KeyValuePair<PropModel, (double x, double z, double h)> t in s_table)
            {
                PropSize s = PropSize.Of(t.Key);
                string what = t.Key + " " + s.Name;
                Assert.That(s.Model, Is.EqualTo(t.Key), what);
                Assert.That(s.Name, Is.EqualTo("Crypt_" + t.Key + "_01"), what);
                Assert.That(s.Height, Is.EqualTo(t.Value.h).Within(0.011), "its height: " + what);
                double w = s.Shape == PropShape.Circle ? 2 * s.Radius : s.Width, d = s.Shape == PropShape.Circle ? 2 * s.Radius : s.Depth;
                Assert.That(w, Is.InRange(t.Value.x - 1e-9, t.Value.x + 0.1), "its footprint holds it across: " + what);
                Assert.That(d, Is.InRange(t.Value.z - 1e-9, t.Value.z + 0.1), "and deep: " + what);
            }
            Assert.That(all.Where(m => PropSize.Of(m).Walkable), Is.EquivalentTo(new[] { PropModel.Dais, PropModel.Banner }), "walked over or under: the dais and the banner");
            Assert.That(all.Where(m => PropSize.Of(m).Walkable).All(m => PropSize.Of(m).Slender), "and so they stop nothing");
            Assert.That(all.Where(m => PropSize.Of(m).PivotAtBack), Is.EquivalentTo(new[] { PropModel.Banner, PropModel.BoneWall }), "their backs go against a wall");
            Assert.That(all.Where(m => PropSize.Of(m).Large && PropSize.Of(m).WallPiece), Is.EquivalentTo(new[] { PropModel.Banner, PropModel.BoneWall }));
            Assert.That(PropSize.Of(PropModel.Banner).Emission, Is.False, "the banner does not glow");
            Assert.That(s_table.Keys.Where(m => m != PropModel.Banner).All(m => PropSize.Of(m).Emission), "the other seven do");
            Assert.That(PropSize.Of(PropModel.Dais).ModelUp, Is.EqualTo(PropSize.DaisShows - 0.6).Within(1e-9), "sunk so 0.15 m shows");
            Assert.That(PropSize.DaisShows, Is.EqualTo(0.15).Within(1e-9));
            foreach (ThemeRecipe r in new ThemeValues().Recipes)
                Assert.That(RoomThemes.ModelsOf(r).Any(m => PropSize.Of(m).Large), Is.False, "no recipe of the props stage names one: " + r.Theme);
        }

        // ---------------- A thousand seeds per floor ----------------

        /// <summary>The stretches of wall a piece at a wall keeps clear of, as the plan finds them: the room's niches and lanterns.</summary>
        static List<PropRules.WallSpan> SpansOf(LevelLayout l, LevelRoom r, CryptKitPlan kit)
        {
            List<PropRules.WallSpan> spans = PropRules.NichesOf(l, r, kit);
            foreach (CryptKitLantern lantern in kit.Lanterns)
            {
                if (lantern.Room != r.Order) continue;
                CryptKitSection s = kit.Sections[lantern.Section];
                int side = s.Vertical ? (s.Line == r.Rect.X ? LevelGenerator.West : s.Line == r.Rect.Right ? LevelGenerator.East : -1)
                                      : (s.Line == r.Rect.Y ? LevelGenerator.South : s.Line == r.Rect.Top ? LevelGenerator.North : -1);
                if (side >= 0) spans.Add(new PropRules.WallSpan { Side = side, From = s.Cell * l.CellSize, To = (s.Cell + 1) * l.CellSize });
            }
            return spans;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_EachThemesLargeProp_IsPlacedByItsRule_OrLeftOutForAStatedReason_AndKeepsEveryRule(int floor)
        {
            var values = new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false }; // work order 103 (changed deliberately): the throne room on floors 1 to 4 too, as this was measured
            PropValues pv = values.PropRules;
            Assert.That(values.LargeProps && values.RoomMood && values.Mood.Compose, "on by default");
            var rooms = new Dictionary<string, int>();
            var placed = new Dictionary<string, int>();
            var reasons = new Dictionary<string, int>();
            var models = new Dictionary<PropModel, int>();
            int combat = 0, banners = 0, throneBanners = 0, thrones = 0, vaults = 0, heaps = 0, daisThrones = 0, greatBraziers = 0, clusters = 0;
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, floor, values);
                RoomMoodPlan plan = RoomMoodPlan.Make(l, values);
                CryptKitPlan kit = PropRules.KitPlanForProps(l);
                double cs = l.CellSize;
                foreach (MoodRoom mr in plan.Rooms)
                {
                    LevelRoom r = mr.Room;
                    string where = "seed " + s + " floor " + floor + " " + r.Letter + r.Order + " " + Key(r);
                    if (r.Kind == RoomKind.Entrance)
                    {
                        Assert.That(mr.LargeProps, Is.Empty, "nothing in the entrance: " + where);
                        continue;
                    }
                    Count(rooms, Key(r));
                    PropModel main = MainOf(r);
                    if (mr.LargeProps.Any(p => p.Model == main)) Count(placed, Key(r));
                    else if (main != PropModel.Banner)
                    {
                        Assert.That(mr.LargeLeftOut, Is.Not.Null.And.Not.Empty, "left out for a stated reason: " + where);
                        string why = mr.LargeLeftOut;
                        int cut = why.IndexOfAny("0123456789(".ToCharArray());
                        Count(reasons, Key(r) + ": " + (cut > 0 ? why.Substring(0, cut).TrimEnd() : why));
                    }
                    foreach (LevelProp p in mr.LargeProps) Count(models, p.Model);
                    if (r.Kind == RoomKind.Combat)
                    {
                        combat++;
                        banners += mr.LargeProps.Count(p => p.Model == PropModel.Banner);
                    }
                    clusters += mr.LargeProps.Count(p => p.Model == PropModel.CandleCluster);

                    // Every large prop: in its room, off the doorways, her start and every enemy's place, overlapping nothing.
                    List<LevelProp> standing = r.Props.Where(p => !mr.Replaced.Contains(p)).ToList();
                    List<PropRules.WallSpan> spans = SpansOf(l, r, kit);
                    foreach (LevelProp p in mr.LargeProps)
                    {
                        PropSize size = PropSize.Of(p.Model);
                        string what = where + ": " + p;
                        Assert.That(size.Large, what);
                        Assert.That(l.RoomAt(p.Position.X, p.Position.Y), Is.SameAs(r), "in its room: " + what);
                        Assert.That(p.Footprint.DistanceToWalls(r.Rect, cs), Is.GreaterThanOrEqualTo(-1e-9), "inside its walls: " + what);
                        bool dais = p.Model == PropModel.Dais;
                        double fromDoor = dais ? Math.Min(pv.FromDoor, 1.0) : pv.FromDoor, fromPoints = dais ? Math.Min(pv.FromPoints, pv.WalkRadius) : pv.FromPoints;
                        foreach (LevelDoor d in l.DoorsOf(r.Order))
                        {
                            d.Opening(cs, out LevelPoint a, out LevelPoint b);
                            Assert.That(p.Footprint.DistanceTo(a, b), Is.GreaterThanOrEqualTo(fromDoor - 1e-9), "off the doorway and its sign: " + what);
                        }
                        if (r.PlayerStart.HasValue) Assert.That(p.Footprint.DistanceTo(r.PlayerStart.Value), Is.GreaterThanOrEqualTo(fromPoints - 1e-9), "off her start: " + what);
                        for (int i = 0; i < r.EnemyPoints.Count; i++)
                            foreach (LevelPoint m in r.PlacesAt(i).Concat(new[] { r.EnemyPoints[i] }))
                                Assert.That(p.Footprint.DistanceTo(m), Is.GreaterThanOrEqualTo(fromPoints - 1e-9), "off an enemy's place: " + what);
                        foreach (LevelProp o in standing.Concat(mr.LargeProps))
                        {
                            if (o == p || (dais && mr.Raised.Contains(o)) || (o.Model == PropModel.Dais && mr.Raised.Contains(p))) continue;
                            Assert.That(p.Footprint.DistanceTo(o.Footprint), Is.GreaterThan(1e-9), "overlaps the " + o.Model + ": " + what);
                        }
                        Assert.That(PropRules.RotationAllowed(p.Model, p.Rotation), what);
                        if (!size.Walkable)
                        {
                            // A prop that blocks walking: off the middle as a themed room's props are.
                            ThemeRecipe recipe = r.Theme == RoomTheme.None ? null : values.ThemeRules.Of(r.Theme, r.Kind);
                            double fromMiddle = p.Footprint.DistanceTo(r.Middle(cs));
                            if (r.Kind != RoomKind.Treasure) Assert.That(fromMiddle, Is.GreaterThanOrEqualTo(pv.LaneWidth * 0.5 - pv.LaneGrid - 1e-9), "off the middle: " + what);
                            if (recipe != null && recipe.ClearMiddle > 0) Assert.That(fromMiddle, Is.GreaterThanOrEqualTo(recipe.ClearMiddle - 1e-9), "off the recipe's clear middle: " + what);
                        }
                        if (size.WallPiece)
                        {
                            // The banner and the bone wall: their backs at the wall's face, facing into the room, never at the low
                            // south wall, in front of no niche and no lantern, every prop a little away from a banner.
                            Assert.That(PropRules.Fault(l, r, p, standing.Concat(mr.LargeProps).Where(o => o != p).ToList(), new PropValues { FloorSpacing = 0f }, null, spans), Is.Null, "a wall piece's rules: " + what);
                            int side = p.Footprint.NearestSide(r.Rect, cs, out double back);
                            Assert.That(side, Is.Not.EqualTo(LevelGenerator.South), "never at the low south wall: " + what);
                            Assert.That(back, Is.InRange(pv.WallFace - 1e-9, pv.WallFace + pv.HandWidth + 1e-9), "its back at the wall's face: " + what);
                            if (p.Model == PropModel.Banner)
                                foreach (LevelProp o in standing.Concat(mr.LargeProps).Where(o => o != p))
                                    Assert.That(o.Footprint.DistanceTo(p.Footprint), Is.GreaterThanOrEqualTo(RoomMoodPlan.BannerClear - 1e-9), "free wall round a banner: " + what);
                        }
                        if (p.Model == PropModel.KnightStatue || p.Model == PropModel.GreatAltar)
                        {
                            int side = p.Model == PropModel.KnightStatue ? p.Footprint.NearestSide(r.Rect, cs, out _) : mr.FarSide;
                            LevelPoint into = PropRules.Inward(side), front = p.Front;
                            Assert.That(front.X * into.X + front.Y * into.Y, Is.GreaterThan(0.99), "facing into the room: " + what);
                            if (p.Model == PropModel.KnightStatue) Assert.That(side, Is.Not.EqualTo(LevelGenerator.South), "never at the low south wall: " + what);
                        }
                    }

                    // The lane, with every prop that stands and every large prop that blocks walking.
                    var lane = new PropRules.Lane(l, r, pv);
                    foreach (LevelProp p in standing) lane.Block(p.Footprint);
                    foreach (LevelProp p in mr.LargeProps.Where(p => !PropSize.Of(p.Model).Walkable)) lane.Block(p.Footprint);
                    Assert.That(lane.Clear(), "the lane from door to door and to the middle is clear: " + where);

                    // The dais carries what stands wholly on it; replaced props are the throne's pair and the shrine's basin.
                    LevelProp daisProp = mr.LargeProps.FirstOrDefault(p => p.Model == PropModel.Dais);
                    if (daisProp != null)
                    {
                        foreach (LevelProp o in standing.Where(o => o.Footprint.DistanceTo(daisProp.Footprint) <= 1e-9))
                            Assert.That(mr.Raised, Does.Contain(o), "what the dais touches stands on it: " + where);
                        LevelProp carriedOne = r.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone || p.Model == PropModel.Chest);
                        Assert.That(mr.Raised, Does.Contain(carriedOne), "the throne or the chest on it: " + where);
                    }
                    foreach (LevelProp o in mr.Replaced)
                        Assert.That(r.Theme == RoomTheme.ThroneRoom ? o.Model == PropModel.Brazier || o.Model == PropModel.Basin : r.Theme == RoomTheme.Shrine && o.Model == PropModel.Basin,
                                    "only the throne's pair and the shrine's basin are replaced: " + where + " " + o);
                    if (r.Theme == RoomTheme.ThroneRoom)
                    {
                        thrones++;
                        throneBanners += mr.LargeProps.Count(p => p.Model == PropModel.Banner);
                        if (daisProp != null)
                        {
                            daisThrones++;
                            greatBraziers += mr.LargeProps.Count(p => p.Model == PropModel.GreatBrazier);
                            Assert.That(mr.Pieces.Any(c => c.Kind == CompositionKind.Dais), Is.False, "the model in place of the code-made dais: " + where);
                        }
                        else Assert.That(mr.Replaced, Is.Empty, "no dais model, the pair stays: " + where);
                    }
                    if (r.Theme == RoomTheme.Vault)
                    {
                        vaults++;
                        int h = mr.LargeProps.Count(p => p.Model == PropModel.CoinHeap);
                        heaps += h;
                        if (h > 0) Assert.That(mr.Pieces.Any(c => c.Kind == CompositionKind.CoinHeap), Is.False, "the models in place of the code-made heaps: " + where);
                    }
                }
            }
            string Share(string key) => (rooms.TryGetValue(key, out int n) && n > 0 ? (placed.TryGetValue(key, out int p) ? p : 0) / (double)n : 1).ToString("0.0%", s_Inv);
            TestContext.WriteLine(string.Format(s_Inv,
                "LARGESTATS floor {0}, seeds 0-999: {1}; left out: {2}; models {3}; banners {4} in {5} combat rooms ({6:0.00} a room), {7} in {8} throne rooms; " +
                "great braziers {9} by {10} thrones on the dais; coin heaps {11} in {12} vaults; candle clusters {13}",
                floor, string.Join(", ", rooms.OrderBy(k => k.Key).Select(k => k.Key + " " + (placed.TryGetValue(k.Key, out int p) ? p : 0) + " of " + k.Value + " (" + Share(k.Key) + ")")),
                string.Join(", ", reasons.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)),
                string.Join(", ", models.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)),
                banners, combat, combat > 0 ? banners / (double)combat : 0, throneBanners, thrones, greatBraziers, daisThrones, heaps, vaults, clusters));

            // Each theme's large prop is placed in most of its rooms (the rest: reasons above).
            double Placed(string key) => rooms.TryGetValue(key, out int n) && n > 0 ? (placed.TryGetValue(key, out int p) ? p : 0) / (double)n : 1;
            Assert.That(Placed("throne room"), Is.GreaterThan(0.8), "the throne on the dais model");
            Assert.That(Placed("tomb hall"), Is.GreaterThan(0.9), "the knight");
            Assert.That(Placed("vault"), Is.GreaterThan(0.9), "the vault's chest on the dais model");
            Assert.That(Placed("archive (treasure)"), Is.GreaterThan(0.9), "the treasure room's archive's chest on the dais model");
            Assert.That(Placed("shrine"), Is.GreaterThan(0.55), "the great altar");
            // Changed deliberately (work order 054): with the casters on in the floors, standing at the far side as the shooters
            // do, the bone wall finds its free section in 79.2 % of floor 4's ossuaries and 79.0 % of floor 5's (80.4 and 80.5 %
            // with them off); 0.8 before.
            Assert.That(Placed("ossuary"), Is.GreaterThan(0.75), "the bone wall");
            Assert.That(banners, Is.GreaterThan(combat), "banners in the combat rooms");
            Assert.That(greatBraziers, Is.GreaterThan(daisThrones), "great braziers by the dais");
            // Changed deliberately (work order 076, Kristian of the treasure room's gold: "litt unødvendig"): with the cave on, the
            // vault's large coin heaps are not placed (CaveValues.NoGoldHeaps; CavePlanTests counts them gone and the chest seen);
            // with it off, as before, at least nine vaults in ten have them.
            if (values.Cave && values.CaveRules.NoGoldHeaps) Assert.That(heaps, Is.EqualTo(0), "no large coin heap in the vaults");
            else Assert.That(heaps, Is.GreaterThanOrEqualTo(vaults * 9 / 10), "coin heaps in the vaults");
            Assert.That(clusters, Is.GreaterThan(0));
        }

        // ---------------- Off: work order 044's ----------------

        /// <summary>The plan's text, room by room.</summary>
        static List<string> RoomsOf(RoomMoodPlan plan)
        {
            var list = new List<string>();
            foreach (string line in plan.Describe().Split('\n'))
            {
                if (line.Length == 0) continue;
                if (!line.StartsWith(" ", StringComparison.Ordinal)) list.Add(line + "\n");
                else list[list.Count - 1] += line + "\n";
            }
            return list;
        }

        [Test]
        public void TheSwitchOff_GivesWorkOrder044sPlan_TheGeneratorNeverReadsIt_AndARoomItLeftAloneIsAsItWas()
        {
            int same = 0, changed = 0;
            for (ulong s = 0; s < 300; s++)
                foreach (int floor in new[] { 1, 5 })
                {
                    LevelLayout on = LevelGenerator.Generate(s, floor), off = LevelGenerator.Generate(s, floor, Off());
                    string where = "seed " + s + " floor " + floor;
                    Assert.That(off.Describe(), Is.EqualTo(on.Describe()), "the generator never reads it: " + where);
                    RoomMoodPlan planOff = RoomMoodPlan.Make(off, Off()), planOn = RoomMoodPlan.Make(on, new LevelValues());
                    Assert.That(planOff.Rooms.All(r => r.LargeProps.Count == 0 && r.Replaced.Count == 0 && r.Raised.Count == 0 && r.LargeLeftOut == null), "off: none, " + where);
                    Assert.That(planOff.Describe(), Does.Not.Contain("  large ").And.Not.Contain("  replaced ").And.Not.Contain("  raised "), where);
                    List<string> a = RoomsOf(planOff), b = RoomsOf(planOn);
                    Assert.That(b.Count, Is.EqualTo(a.Count), where);
                    for (int i = 0; i < a.Count; i++)
                    {
                        MoodRoom r = planOn.Rooms[i];
                        if (r.LargeProps.Count > 0 || r.Replaced.Count > 0) { changed++; continue; }
                        same++;
                        Assert.That(b[i], Is.EqualTo(a[i]), "a room with no large prop is work order 044's to the letter: " + where + " room " + i);
                    }
                    if (s % 10 == 0)
                    {
                        // The rooms' mood or its compositions off: no large prop either.
                        var noCompose = new LevelValues();
                        noCompose.Mood.Compose = false;
                        Assert.That(RoomMoodPlan.Make(on, noCompose).Rooms.All(r => r.LargeProps.Count == 0), where);
                        Assert.That(RoomMoodPlan.Make(on, new LevelValues { Props = false }).Rooms.All(r => r.LargeProps.Count == 0), where);
                    }
                }
            TestContext.WriteLine("LARGESTATS seeds 0-299, floors 1 and 5: " + same + " rooms as work order 044 left them to the letter, " + changed + " with large props");
            Assert.That(same, Is.GreaterThan(0));
            Assert.That(changed, Is.GreaterThan(0));
        }

        // ---------------- The sources ----------------

        [Test]
        public void TheSources_AreTheGreatBraziers_TheGreatAltar_AndTheCandleClusters_AndWhatTheyReplaceIsNotOne()
        {
            var values = new LevelValues();
            int braziers = 0, altars = 0, clusters = 0;
            for (ulong s = 0; s < 120; s++)
                foreach (int floor in new[] { 1, 5 })
                    foreach (MoodRoom mr in RoomMoodPlan.Make(LevelGenerator.Generate(s, floor, values), values).Rooms)
                    {
                        string where = "seed " + s + " floor " + floor + " " + mr.Room.Letter + mr.Room.Order;
                        foreach (LevelProp p in mr.LargeProps)
                        {
                            MoodSource src = mr.Sources.FirstOrDefault(x => x.OfProp && x.At.DistanceTo(p.Position) < 1e-6);
                            switch (p.Model)
                            {
                                case PropModel.GreatBrazier:
                                    braziers++;
                                    Assert.That(src != null && src.Kind == MoodSourceKind.Brazier && src.Flame, "a great brazier's coals: " + where);
                                    break;
                                case PropModel.GreatAltar:
                                    altars++;
                                    Assert.That(src != null && src.Kind == MoodSourceKind.Brazier, "the great altar's basin: " + where);
                                    break;
                                case PropModel.CandleCluster:
                                    clusters++;
                                    Assert.That(src != null && src.Kind == MoodSourceKind.Candle, "a candle cluster: " + where);
                                    break;
                                case PropModel.Dais: break; // its middle is the chest's, whose gold it carries
                                default:
                                    Assert.That(src, Is.Null, "no source of a " + p.Model + ": " + where);
                                    break;
                            }
                        }
                        foreach (LevelProp p in mr.Replaced.Where(o => !mr.LargeProps.Any(q => q.Position.DistanceTo(o.Position) < 1e-6))) // the great altar may stand where the basin did
                            Assert.That(mr.Sources.Any(x => x.OfProp && x.At.DistanceTo(p.Position) < 1e-6), Is.False, "a replaced prop is no source: " + where);
                        // Every candle source of the composition still has its candles.
                        foreach (MoodSource x in mr.Sources.Where(x => !x.OfProp && x.Kind == MoodSourceKind.Candle))
                            Assert.That(mr.Pieces.Any(c => c.Kind == CompositionKind.Candles && c.At.DistanceTo(x.At) < 1e-6), "candles for every candle source: " + where);
                        for (int i = 1; i < mr.Sources.Count; i++)
                            Assert.That(mr.Sources[i - 1].Kind != MoodSourceKind.Candle || mr.Sources[i].Kind == MoodSourceKind.Candle, "braziers first, for the real lights: " + where);
                    }
            TestContext.WriteLine("LARGESTATS sources (seeds 0-119, floors 1 and 5): great braziers " + braziers + ", great altars " + altars + ", candle clusters " + clusters);
            Assert.That(braziers, Is.GreaterThan(0));
            Assert.That(altars, Is.GreaterThan(0));
            Assert.That(clusters, Is.GreaterThan(0));
        }

        // ---------------- The chest on the dais ----------------

        [Test]
        public void TheChestOnTheDais_CanBeReachedFromTheFloor_TheDaisBeingWalkedOver()
        {
            var values = new LevelValues();
            float reach = new RunValues().ChestReach;
            int chests = 0;
            for (ulong s = 0; s < 200; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, 3, values);
                foreach (MoodRoom mr in RoomMoodPlan.Make(l, values).Rooms)
                {
                    LevelProp dais = mr.LargeProps.FirstOrDefault(p => p.Model == PropModel.Dais);
                    LevelProp chest = mr.Room.Props.FirstOrDefault(p => p.Model == PropModel.Chest);
                    if (dais == null || chest == null) continue;
                    chests++;
                    Assert.That(PropSize.Of(PropModel.Dais).Walkable, "walked over");
                    // Half a metre before the chest's front: on the dais, in no prop that blocks walking, within its reach.
                    LevelPoint f = chest.Front;
                    var stand = new LevelPoint(chest.Position.X + f.X * (chest.Depth * 0.5 + 0.5), chest.Position.Y + f.Y * (chest.Depth * 0.5 + 0.5));
                    Assert.That(dais.Footprint.DistanceTo(stand), Is.EqualTo(0).Within(1e-9), "seed " + s + ": she stands on the dais");
                    foreach (LevelProp o in mr.Room.Props.Concat(mr.LargeProps).Where(o => !PropSize.Of(o.Model).Walkable && !mr.Replaced.Contains(o)))
                        Assert.That(o.Footprint.DistanceTo(stand), Is.GreaterThan(0.3), "seed " + s + ": clear of the " + o.Model);
                    Assert.That(stand.DistanceTo(chest.Position), Is.LessThan(reach), "seed " + s + ": within the chest's reach");
                }
            }
            Assert.That(chests, Is.GreaterThan(100));
        }

        // ---------------- Quick, the same ----------------

        static double Median(List<double> xs)
        {
            xs.Sort();
            return xs.Count == 0 ? 0 : xs[xs.Count / 2];
        }

        [Test]
        public void Generation_IsAsQuickAsItWas_ByTheMedian_AndThePlanStaysQuick()
        {
            var on = new LevelValues();
            LevelGenerator.Generate(424242UL, 3, on);
            RoomMoodPlan.Make(LevelGenerator.Generate(424242UL, 3, Off()), Off());
            var gOn = new List<double>();
            var gOff = new List<double>();
            var pOn = new List<double>();
            var pOff = new List<double>();
            var timer = new Stopwatch();
            for (ulong s = 0; s < 300; s++)
            {
                timer.Restart();
                LevelLayout l = LevelGenerator.Generate(s, 3, on);
                gOn.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                LevelGenerator.Generate(s, 3, Off());
                gOff.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                RoomMoodPlan.Make(l, on);
                pOn.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                RoomMoodPlan.Make(l, Off());
                pOff.Add(timer.Elapsed.TotalMilliseconds);
            }
            double slowest = pOn.Max();
            double mgOn = Median(gOn), mgOff = Median(gOff), mpOn = Median(pOn), mpOff = Median(pOff);
            TestContext.WriteLine(string.Format(s_Inv, "LARGESTATS generation, floor 3, seeds 0-299: median {0:0.000} ms with the large props on, {1:0.000} ms off (the generator never reads the switch); " +
                                                "the mood's plan (made when a floor is built) median {2:0.000} ms on, {3:0.000} ms off, slowest {4:0.000} ms on", mgOn, mgOff, mpOn, mpOff, slowest));
            Assert.That(mgOn, Is.LessThan(mgOff * 1.25 + 0.2), "generation as quick as it was, by the median");
            Assert.That(mpOn, Is.LessThan(20), "the plan is quick, as work order 044's bound");
        }

        [Test]
        public void ThePlan_IsTheSameForTheSameSeed()
        {
            var values = new LevelValues();
            foreach (ulong s in new[] { 7UL, 42UL, 4242UL })
            {
                string one = RoomMoodPlan.Make(LevelGenerator.Generate(s, 5, values), values).Describe();
                Assert.That(one, Does.Contain("  large "), "seed " + s + " holds large props");
                Assert.That(RoomMoodPlan.Make(LevelGenerator.Generate(s, 5, values), values).Describe(), Is.EqualTo(one), "seed " + s);
            }
        }
    }
}
