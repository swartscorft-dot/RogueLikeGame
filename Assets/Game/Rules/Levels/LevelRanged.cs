using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the ranged enemies around the room (work order 124; Kristian, 2026-11-25, after two runs: "det er litt kjedelig
    /// at alle range alltid er gruppert bakerst. De kan være litt mer strategisk plassert rundt i rommet", and of four questions he
    /// ticked "Ja, bare når den synes", "Ja, med ett sekunds pause", "Blanding" and "Fra etasje 4"; the research digest R2's patterns
    /// P1, P2, P3, P7 and P8 and its rules G1 to G3). Read only with <see cref="LevelValues.RangedAroundTheRoom"/> on; each part has a
    /// bool of its own so it can be turned off alone. Lengths in metres from the room's own floor; all first guesses.
    /// </summary>
    [Serializable]
    public sealed class RangedAroundValues
    {
        // ---- Part 1: a draw begins only while it is on his screen (a rule of the fight; his tick "Ja, bare når den synes") ----
        public bool OnScreenOnly = true;
        public float ScreenMargin = 0.5f;          // m inside the edge of what the camera shows of the ground that a shooter or caster must stand to begin (the digest's G2: half a metre)

        // ---- Part 2: a second's grace at the waking (his tick "Ja, med ett sekunds pause"; the digest's Hades patch note) ----
        public bool WakeGrace = true;
        public float GraceSeconds = 1.0f;          // s after its room's fight wakes before a ranged enemy within its reach of her may begin its first draw

        // ---- Part 3: the near flank (digest P3) ----
        public bool NearFlank = true;
        public double NearFlankShare = 0.6;        // of the combat and exit rooms with at least NearFlankRangedAtLeast ranged enemies, this share draws one (the order's first guess, so rooms differ)
        public int NearFlankRangedAtLeast = 2;     // ranged enemies a room must hold to draw a near flank
        public double NearFromLine = 8.0;          // m from the middle of the fight's line, at least: in reach at once (G3)
        public double NearToLine = 11.0;           // and at most: the shooter's and the caster's reach (11 m)
        public double NearSideLeast = 6.0;         // m to the side of the door's straight-in line, at least
        public double NearSideMost = 10.0;         // and at most
        // The view it must be in as the room wakes (G2), asked of the default camera's numbers (FeelValues' CameraDistance, CameraPitch,
        // FieldOfView at 16:9) from the middle of the fight's line: named here so the floors do not move when the camera is tried otherwise.
        public double ViewDistance = 28.0;         // m (FeelValues.CameraDistance)
        public double ViewPitch = 55.0;            // degrees (FeelValues.CameraPitch)
        public double ViewFieldOfView = 30.0;      // degrees, vertical (FeelValues.FieldOfView)
        public double ViewAspect = 16.0 / 9.0;     // 1920 by 1080
        public double ViewMargin = 0.5;            // m inside its edge (G2)

        // ---- Part 4: singles, a pair, or a nest: a mix (his tick "Blanding") ----
        public bool PairsAndNests = true;
        public double SinglesShare = 0.5;          // of the rooms with shooters: each stands alone (the order's first guess)
        public double PairShare = 0.3;             // one pair, the rest alone
        public double NestShare = 0.2;             // one nest of three where the room has three (with two, a pair)
        public double ClusterApartLeast = 4.0;     // m between two of a pair or a nest, at least (EnemySpacing)
        public double ClusterApartMost = 6.0;      // and at most (the digest's P6)

        // ---- Part 5: the ladder (digest P8) ----
        public bool Ladder = true;
        public int Rungs = 3;                      // rungs at most
        public double RungFrom = 8.0;              // m from the middle of the fight's line: rung 1 from 8 to 11, then 15 to 18, then 22 to 25
        public double RungTo = 11.0;
        public double RungStep = 7.0;              // m deeper each rung
        public double RungFromBefore = 11.0;       // m from the rung before at most: its reach begins about where the last one stood
        public double RungSideLeast = 1.5;         // m to its side of the door's straight-in line at least: the rungs alternate sides

        // ---- Part 6: at most one ranged group at the back (digest P1) ----
        public bool OneAtTheBack = true;           // only the first ranged group (a pair, else a caster, else a shooter) takes a template's h perch or the far band

        // ---- Part 7: the warder in the pack (digest P7; the producer's choice from the research; Kristian was asked on 2026-11-25 and said yes: "Ja, prøv det") ----
        public bool WarderInThePack = true;
        public double PackBandFrom = 0.35;         // share of the room's farthest place from the door in, at least
        public double PackBandTo = 0.6;            // and at most: the middle band
        public double PackWardRange = 9.0;         // m from the largest melee group's point at most (WarderValues.WardRange)
        public double PackBehindLeast = 3.0;       // m from it at least (WarderValues.BehindBy)
        public double PackBehindMost = 5.0;        // and at most
        public double PackBehindAngleMost = 60.0;  // degrees off the way from the door in through that point: behind it as seen from the door

        // ---- Part 8: crossfire from floor 4 (digest P2; his tick "Fra etasje 4") ----
        public bool Crossfire = true;
        public int CrossfireFromFloor = 4;
        public double CrossfireShare = 1.0 / 3.0;  // of the combat rooms of floors 4 and 5 with two ranged enemies to spare (about one in three)
        public double CrossFromMiddle = 9.0;       // m from the room's middle, at least
        public double CrossToMiddle = 13.0;        // and at most
        public double CrossNorthSouthMost = 5.0;   // m north or south of the middle at most: east and west on screen
        public double CrossApartLeast = 16.0;      // m between the two, at least
        public double CrossCoverFrom = 3.0;        // m in front of each, toward the other's side, where a tall cover is wanted: from
        public double CrossCoverTo = 6.0;          // to
        // Only in a room she enters heading north or south (the digest's P2: only then do east and west on screen lie across the room, not
        // one post by the door and one at the far wall). OFF since Kristian's tick of 2026-11-25, "Også i rom du går inn i fra siden": in a
        // room entered from the side one post may stand by the door and one at the far wall, and a post in the far band is then the room's
        // one group at the back. True: work order 124's first rule, north or south only.
        public bool CrossfireNorthOrSouthOnly = false;

        // ---- Part 10: the recorder says what he met ----
        public bool TellsTheLog = true;            // the fight_room line names the room's patterns; each arrow or orb let loose says whether its shooter was on screen
    }

    /// <summary>Where a ranged enemy was stood by work order 124's rules (<see cref="RangedPlace"/>).</summary>
    public enum RangedPost
    {
        None,
        Far,        // the room's one group at the back: a template's h perch or the far band (P1)
        NearFlank,  // close at her side as she steps in (P3)
        Rung,       // a rung of the ladder (P8)
        Crossfire,  // one of two east and west of the room's middle (P2)
        Pack,       // the warder among the melee (P7)
        Elsewhere,  // no pattern found room: short of the far band, or anywhere
    }

    /// <summary>The note on a ranged group of where work order 124's rules stood it, and with whom. Null on a group they did not place.</summary>
    public sealed class RangedPlace
    {
        public RangedPost Post;
        /// <summary>The rung, from 1, for <see cref="RangedPost.Rung"/>; 0 otherwise.</summary>
        public int Rung;
        /// <summary>The pair's or nest's number in its room, from 1; 0 for one alone.</summary>
        public int Cluster;
        /// <summary>1 alone, 2 a pair, 3 a nest (as many as stand together).</summary>
        public int ClusterSize = 1;
        /// <summary>True for the post its pattern's band was asked of (one alone, or a pair's or a nest's first); false for the others of a pair or a nest, each 4 to 6 m from those before it.</summary>
        public bool Anchor = true;

        public override string ToString() =>
            Post + (Rung > 0 ? " " + Rung.ToString(CultureInfo.InvariantCulture) : "") + (ClusterSize > 1 ? (ClusterSize == 2 ? " pair " : " nest ") + Cluster.ToString(CultureInfo.InvariantCulture) : "");
    }

    /// <summary>
    /// Ranged enemies around the room (work order 124, the generator's version 23), with <see cref="LevelValues.RangedAroundTheRoom"/>
    /// on. Its parts in the generator (each behind its own bool of <see cref="RangedAroundValues"/>):
    ///
    /// - The shooters stand singly (<see cref="Split"/>: every line of shooters of a room's groups becomes as many single shooters, so
    ///   the room's count is the same), and a room's own draw (<see cref="Stage"/>) says whether they stand each alone, or one pair
    ///   4 to 6 m apart and the rest alone, or one nest of three (part 4).
    /// - Only the first ranged group takes a template's h perch or the far band: a pair, else a caster, else a shooter (part 6).
    /// - On floors 4 and 5, in about one combat room in three, two stand east and west of the room's middle on screen (part 8).
    /// - In a share of the rooms with two or more, one stands close at her side as she comes in: 8 to 11 m from the middle of the
    ///   fight's line, 6 to 10 m to the side of the door's straight-in line, in view (part 3).
    /// - The rest stand on rungs staggered in depth, alternating sides (part 5); the warder stands among the melee (part 7).
    ///
    /// Every post keeps every rule a post kept (the generator's Fits: the walls, the doors, <see cref="LevelValues.EnemyFromEntry"/>,
    /// the spacing, the casters' spacing); a pattern that finds no room in a room is left out there and its enemies stand short of the
    /// far band, or anywhere. The validator holds each post to its pattern's rule (<see cref="Faults"/>); the fight's cover serves the
    /// near flank's line first and puts tall cover before the crossfire's posts where it can (the props stage).
    /// </summary>
    public static class LevelRanged
    {
        /// <summary>The generator's stream for each room's draws of where its ranged enemies stand.</summary>
        public const string Stage = "ranged";

        /// <summary>Is the switch on (and so the version-23 rules read)?</summary>
        public static bool On(LevelValues v) => v != null && v.RangedAroundTheRoom && v.RangedAround != null;

        /// <summary>A ranged group these rules stand (part 3 to 6, 8): a shooter or a caster that is not the elite.</summary>
        public static bool IsRanged(EnemyGroup g) => g != null && !g.Boss && !g.Elite && (g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster);

        /// <summary>
        /// Part 4: every line of shooters of these fights' groups (each room's, as the generator lays them into the row) becomes as many
        /// single shooters, in its place; the room's count of enemies is the same. Nothing with the switch or the part off.
        /// </summary>
        public static void Split(List<List<EnemyGroup>> fights, LevelValues v)
        {
            if (fights == null || !On(v) || !v.RangedAround.PairsAndNests) return;
            foreach (List<EnemyGroup> groups in fights) SplitGroups(groups, v);
        }

        /// <summary>Part 4 for one room's groups (the lab's room too).</summary>
        public static void SplitGroups(List<EnemyGroup> groups, LevelValues v)
        {
            if (groups == null || !On(v) || !v.RangedAround.PairsAndNests) return;
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                EnemyGroup g = groups[i];
                if (g == null || g.Kind != EnemyKind.Shooter || g.Elite || g.Boss || g.Count <= 1) continue;
                int n = g.Count;
                groups[i] = new EnemyGroup(EnemyKind.Shooter);
                for (int k = 1; k < n; k++) groups.Insert(i + 1, new EnemyGroup(EnemyKind.Shooter));
            }
        }

        // ---------------- The room's own measures ----------------

        /// <summary>The middle of the door she comes in by, and the unit way into the room from it.</summary>
        public static void DoorWay(LevelLayout level, LevelRoom room, LevelDoor entry, out LevelPoint middle, out LevelPoint into)
        {
            entry.Opening(level.CellSize, out LevelPoint a, out LevelPoint b);
            middle = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            into = PropRules.Inward(LevelGeometry.WallOf(room.Rect, entry));
        }

        /// <summary>The middle of the fight's line: straight in from the middle of the door in by its depth (<see cref="RoomFightRules.PointDepth"/>).</summary>
        public static LevelPoint LineMiddle(LevelLayout level, LevelRoom room, LevelDoor entry, LevelValues v)
        {
            DoorWay(level, room, entry, out LevelPoint d, out LevelPoint into);
            double depth = RoomFightRules.PointDepth(level, room, entry, v.Fights ?? new RoomFightValues());
            return new LevelPoint(d.X + into.X * depth, d.Y + into.Y * depth);
        }

        /// <summary>Metres to the side of the door's straight-in line, to her left as she comes in negative, to her right positive.</summary>
        public static double Side(LevelPoint doorMiddle, LevelPoint into, LevelPoint p)
        {
            double dx = p.X - doorMiddle.X, dy = p.Y - doorMiddle.Y;
            // Her right as she faces along into is (into.Y, -into.X).
            return dx * into.Y - dy * into.X;
        }

        /// <summary>The ground the default camera shows from her at <paramref name="her"/> (the numbers of <see cref="RangedAroundValues"/>, G2).</summary>
        public static GroundQuad DesignView(LevelPoint her, RangedAroundValues r) =>
            ScreenGround.Design(her.X, her.Y, r.ViewDistance, r.ViewPitch, r.ViewFieldOfView, r.ViewAspect);

        /// <summary>Is <paramref name="p"/> in the default camera's view from <paramref name="her"/>, <paramref name="margin"/> inside its edge?</summary>
        public static bool InDesignView(LevelPoint her, LevelPoint p, RangedAroundValues r, double margin) =>
            ScreenGround.Shows(DesignView(her, r), p.X, p.Y, margin);

        /// <summary>Is the point within the band from <paramref name="lo"/> to <paramref name="hi"/> m of <paramref name="from"/>?</summary>
        public static bool Within(LevelPoint from, LevelPoint p, double lo, double hi)
        {
            double d = from.DistanceTo(p);
            return d >= lo - 1e-9 && d <= hi + 1e-9;
        }

        /// <summary>The band of a rung, from 1 (part 5): from <see cref="RangedAroundValues.RungFrom"/> plus a step a rung on.</summary>
        public static void RungBand(RangedAroundValues r, int rung, out double lo, out double hi)
        {
            lo = r.RungFrom + r.RungStep * (rung - 1);
            hi = r.RungTo + r.RungStep * (rung - 1);
        }

        /// <summary>Is the room's door in on its north or south wall: she comes in heading south or north (part 8)?</summary>
        public static bool EnteredNorthOrSouth(LevelRoom room, LevelDoor entry)
        {
            int wall = entry != null ? LevelGeometry.WallOf(room.Rect, entry) : -1;
            return wall == LevelGenerator.North || wall == LevelGenerator.South;
        }

        /// <summary>Is the point where a crossfire's post may stand (part 8), east (<paramref name="east"/>) or west of the room's middle?</summary>
        public static bool CrossPlace(LevelPoint middle, LevelPoint p, bool east, RangedAroundValues r) =>
            Within(middle, p, r.CrossFromMiddle, r.CrossToMiddle) && Math.Abs(p.Y - middle.Y) <= r.CrossNorthSouthMost + 1e-9
            && (east ? p.X > middle.X + 1e-9 : p.X < middle.X - 1e-9);

        /// <summary>
        /// Is the warder's place among the melee (part 7): within its ward's reach of the melee group's point, 3 to 5 m from it, behind
        /// it as seen from the door in (the way from the door's middle through the group within <see cref="RangedAroundValues.PackBehindAngleMost"/>
        /// of the way from the group to it), in the middle band of the room's depth (<paramref name="share"/>)?
        /// </summary>
        public static bool PackPlace(LevelPoint doorMiddle, LevelPoint melee, LevelPoint p, double share, RangedAroundValues r)
        {
            if (share < r.PackBandFrom - 1e-9 || share > r.PackBandTo + 1e-9) return false;
            double d = melee.DistanceTo(p);
            if (d > r.PackWardRange + 1e-9 || d < r.PackBehindLeast - 1e-9 || d > r.PackBehindMost + 1e-9) return false;
            double ax = melee.X - doorMiddle.X, ay = melee.Y - doorMiddle.Y, al = Math.Sqrt(ax * ax + ay * ay);
            if (al < 1e-9 || d < 1e-9) return false;
            double cos = (ax * (p.X - melee.X) + ay * (p.Y - melee.Y)) / (al * d);
            return cos >= Math.Cos(r.PackBehindAngleMost * Math.PI / 180.0) - 1e-9;
        }

        /// <summary>
        /// Part 8: is there tall cover on the line from a crossfire's post <paramref name="q"/> toward the other's (<paramref name="other"/>):
        /// a tall prop (<see cref="FightCover.IsTall"/>; <paramref name="extra"/> too) or a carved cell of the room whose footprint meets the
        /// stretch of that line from <see cref="RangedAroundValues.CrossCoverFrom"/> to <see cref="RangedAroundValues.CrossCoverTo"/> in front of it?
        /// </summary>
        public static bool CrossCovered(LevelLayout level, LevelRoom room, LevelPoint q, LevelPoint other, RangedAroundValues r, LevelProp extra = null)
        {
            double dx = other.X - q.X, dy = other.Y - q.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-9) return false;
            var a = new LevelPoint(q.X + dx / l * r.CrossCoverFrom, q.Y + dy / l * r.CrossCoverFrom);
            var b = new LevelPoint(q.X + dx / l * Math.Min(l, r.CrossCoverTo), q.Y + dy / l * Math.Min(l, r.CrossCoverTo));
            foreach (LevelProp p in room.Props)
                if (FightCover.IsTall(p) && p.Footprint.DistanceTo(a, b) <= 1e-9) return true;
            if (extra != null && FightCover.IsTall(extra) && extra.Footprint.DistanceTo(a, b) <= 1e-9) return true;
            double cs = level.CellSize;
            foreach ((int x, int y) in room.SolidList)
            {
                var cell = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5 };
                if (cell.DistanceTo(a, b) <= 1e-9) return true;
            }
            return false;
        }

        /// <summary>A melee group a warder may stand among (part 7): a pack of swarmers, brutes or a guard (not the act boss).</summary>
        public static bool IsMelee(EnemyGroup g) => g != null && !g.Boss && (g.Kind == EnemyKind.Swarmer || g.Kind == EnemyKind.Brute || g.Kind == EnemyKind.Guard);

        /// <summary>The melee group a warder stands among: the largest of the room's swarmer packs, brutes and guards (the first of the largest); -1 with none.</summary>
        public static int LargestMelee(LevelRoom room)
        {
            int best = -1;
            for (int i = 0; i < room.EnemyGroups.Count && i < room.EnemyPoints.Count; i++)
            {
                EnemyGroup g = room.EnemyGroups[i];
                if (!IsMelee(g)) continue;
                if (best < 0 || g.Count > room.EnemyGroups[best].Count) best = i;
            }
            return best;
        }

        // ---------------- What a room shows ----------------

        /// <summary>The pattern names, in the order the log writes them.</summary>
        public const string FarWord = "far", NearFlankWord = "near-flank", PairWord = "pair", NestWord = "nest", LadderWord = "ladder",
                            CrossfireWord = "crossfire", PackWord = "warder-in-pack", SinglesWord = "singles", ElsewhereWord = "elsewhere";

        /// <summary>Does the room hold a ladder: rungs at two depths at least (the near flank counted as rung 1)?</summary>
        public static bool HasLadder(LevelRoom room)
        {
            var rungs = new HashSet<int>();
            foreach (EnemyGroup g in room.EnemyGroups)
            {
                if (g?.Ranged == null) continue;
                if (g.Ranged.Post == RangedPost.Rung) rungs.Add(g.Ranged.Rung);
                if (g.Ranged.Post == RangedPost.NearFlank) rungs.Add(1);
            }
            return rungs.Count >= 2;
        }

        /// <summary>The patterns its ranged enemies stand in (part 10), joined by "+"; empty for a room these rules did not stand.</summary>
        public static string PatternsOf(LevelRoom room)
        {
            if (room == null) return "";
            List<RangedPlace> all = room.EnemyGroups.Where(g => g?.Ranged != null).Select(g => g.Ranged).ToList();
            if (all.Count == 0) return "";
            var words = new List<string>();
            if (all.Any(p => p.Post == RangedPost.Far)) words.Add(FarWord);
            if (all.Any(p => p.Post == RangedPost.NearFlank)) words.Add(NearFlankWord);
            if (all.Any(p => p.ClusterSize == 2)) words.Add(PairWord);
            if (all.Any(p => p.ClusterSize == 3)) words.Add(NestWord);
            if (HasLadder(room)) words.Add(LadderWord);
            if (all.Any(p => p.Post == RangedPost.Crossfire)) words.Add(CrossfireWord);
            if (all.Any(p => p.Post == RangedPost.Pack)) words.Add(PackWord);
            if (all.Any(p => p.Post == RangedPost.Elsewhere)) words.Add(ElsewhereWord);
            if (words.Count == 0 || (!all.Any(p => p.ClusterSize > 1) && room.EnemyGroups.Count(g => g?.Ranged != null && g.Kind == EnemyKind.Shooter) > 1))
                words.Add(SinglesWord);
            return string.Join("+", words);
        }

        // ---------------- The validator's rules ----------------

        /// <summary>
        /// Work order 124's rules (part 9), added to <paramref name="faults"/> for a floor in a line made with the switch on: every
        /// ranged post its pattern's rule (the near flank 8 to 11 m from the line's middle, 6 to 10 m to the side, in view; a rung in
        /// its band; the crossfire's two east and west of the middle, 9 to 13 m from it, within 5 m north or south, 16 m apart, never
        /// before floor 4 nor outside a combat room; a pair's and a nest's members 4 to 6 m apart; the warder among the melee), at most
        /// one group in the room's far band by the pattern (with part 6 on), and none of these marks on a group the rules do not stand.
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues v, List<string> faults)
        {
            if (level == null || v == null || faults == null) return;
            bool on = On(v) && level.InALine;
            foreach (LevelRoom room in level.Rooms) RoomFaults(level, room, v, faults, on);
        }

        /// <summary>The rules of <see cref="Faults"/> for one room (the room lab's check asks it too).</summary>
        public static void RoomFaults(LevelLayout level, LevelRoom room, LevelValues v, List<string> faults, bool on)
        {
            string name = room.Letter + room.Order.ToString(CultureInfo.InvariantCulture);
            List<int> marked = Enumerable.Range(0, Math.Min(room.EnemyGroups.Count, room.EnemyPoints.Count)).Where(i => room.EnemyGroups[i]?.Ranged != null).ToList();
            if (!on)
            {
                if (marked.Count > 0) faults.Add($"{name} has ranged posts of work order 124 and its switch is off");
                return;
            }
            if (marked.Count == 0) return;
            RangedAroundValues r = v.RangedAround;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            if (entry == null || LevelGeometry.WallOf(room.Rect, entry) < 0)
            {
                faults.Add($"{name} has ranged posts of work order 124 and no door in");
                return;
            }
            DoorWay(level, room, entry, out LevelPoint door, out LevelPoint into);
            LevelPoint line = LineMiddle(level, room, entry, v);
            LevelPoint middle = room.Middle(level.CellSize);
            double depth = LevelLancers.Depth(level, room, entry, v);
            double Share(LevelPoint p) => LevelGeometry.DistanceToDoor(p, entry, level.CellSize) / Math.Max(1e-9, depth);
            int far = 0, cross = 0, crossEast = 0;
            var clusters = new Dictionary<int, List<LevelPoint>>();
            var farClusters = new HashSet<int>();
            foreach (int i in marked)
            {
                EnemyGroup g = room.EnemyGroups[i];
                RangedPlace rp = g.Ranged;
                LevelPoint p = room.EnemyPoints[i];
                string label = $"{name}'s {EnemyKinds.Word(g.Kind)} {i} at {p} ({rp})";
                bool kindFits = rp.Post == RangedPost.Pack ? g.Kind == EnemyKind.Warder
                              : rp.Post == RangedPost.Elsewhere ? IsRanged(g) || g.Kind == EnemyKind.Warder : IsRanged(g);
                if (!kindFits) faults.Add(label + " is not of a kind its post is for");
                switch (rp.Post)
                {
                    case RangedPost.Far:
                        if (rp.Cluster > 0) farClusters.Add(rp.Cluster);
                        else far++;
                        break;
                    case RangedPost.NearFlank:
                        if (!Within(line, p, r.NearFromLine, r.NearToLine)) faults.Add($"{label} is {line.DistanceTo(p):0.00} m from the fight's line's middle, not {r.NearFromLine} to {r.NearToLine}");
                        double side = Math.Abs(Side(door, into, p));
                        if (side < r.NearSideLeast - 1e-9 || side > r.NearSideMost + 1e-9) faults.Add($"{label} is {side:0.00} m to the side of the door's line, not {r.NearSideLeast} to {r.NearSideMost}");
                        if (!InDesignView(line, p, r, r.ViewMargin)) faults.Add($"{label} is not in view from the fight's line's middle");
                        break;
                    case RangedPost.Rung:
                        RungBand(r, rp.Rung, out double lo, out double hi);
                        if (rp.Rung < 1 || rp.Rung > r.Rungs) faults.Add($"{label} is on no rung of 1 to {r.Rungs}");
                        else if (rp.Anchor && !Within(line, p, lo, hi)) faults.Add($"{label} is {line.DistanceTo(p):0.00} m from the fight's line's middle, not its rung's {lo} to {hi}");
                        break;
                    case RangedPost.Crossfire:
                        cross++;
                        if (Share(p) >= v.FarBand - 1e-9) far++; // in a room entered from the side, a post at the far wall is the room's group at the back
                        if (p.X > middle.X) crossEast++;
                        if (!CrossPlace(middle, p, p.X > middle.X, r)) faults.Add($"{label} is not 9 to 13 m east or west of the room's middle within 5 m north or south");
                        break;
                    case RangedPost.Pack:
                        int m = LargestMelee(room);
                        bool among = false;
                        for (int k = 0; m >= 0 && k < room.EnemyGroups.Count && k < room.EnemyPoints.Count && !among; k++)
                            among = IsMelee(room.EnemyGroups[k]) && room.EnemyGroups[k].Count == room.EnemyGroups[m].Count && PackPlace(door, room.EnemyPoints[k], p, Share(p), r);
                        if (!among) faults.Add($"{label} does not stand behind one of the room's largest melee groups in the middle band");
                        break;
                }
                if (rp.Cluster > 0)
                {
                    if (!clusters.TryGetValue(rp.Cluster, out List<LevelPoint> list)) clusters[rp.Cluster] = list = new List<LevelPoint>();
                    list.Add(p);
                }
            }
            far += farClusters.Count;
            if (r.OneAtTheBack && far > 1) faults.Add($"{name} has {far} ranged groups at the back, and one at most");
            if (cross != 0)
            {
                if (cross != 2 || crossEast != 1) faults.Add($"{name}'s crossfire is {cross} posts, {crossEast} east: two, one each side");
                if (level.Floor < r.CrossfireFromFloor) faults.Add($"{name} has a crossfire on floor {level.Floor}, before floor {r.CrossfireFromFloor}");
                if (room.Kind != RoomKind.Combat) faults.Add($"{name} has a crossfire and is not a combat room");
                if (r.CrossfireNorthOrSouthOnly && !EnteredNorthOrSouth(room, entry)) faults.Add($"{name} has a crossfire and is entered from the east or west");
                List<LevelPoint> posts = marked.Where(i => room.EnemyGroups[i].Ranged.Post == RangedPost.Crossfire).Select(i => room.EnemyPoints[i]).ToList();
                if (posts.Count == 2 && posts[0].DistanceTo(posts[1]) < r.CrossApartLeast - 1e-9) faults.Add($"{name}'s crossfire's two stand {posts[0].DistanceTo(posts[1]):0.00} m apart, under {r.CrossApartLeast}");
            }
            foreach (KeyValuePair<int, List<LevelPoint>> c in clusters)
            {
                if (c.Value.Count > 3) faults.Add($"{name}'s cluster {c.Key} is {c.Value.Count} together, three at most");
                for (int a = 0; a < c.Value.Count; a++)
                    for (int b = a + 1; b < c.Value.Count; b++)
                        if (!Within(c.Value[a], c.Value[b], r.ClusterApartLeast, r.ClusterApartMost))
                            faults.Add($"{name}'s cluster {c.Key} has two {c.Value[a].DistanceTo(c.Value[b]):0.00} m apart, not {r.ClusterApartLeast} to {r.ClusterApartMost}");
            }
        }
    }
}
