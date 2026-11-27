using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// One line-up a combat room may be filled from (work order 135): a lead kind and a partner kind, each with a count of its own
    /// (<see cref="LeadFewest"/> to <see cref="LeadMost"/>, <see cref="PartnerFewest"/> to <see cref="PartnerMost"/>), and how often it
    /// is drawn beside the floor's others (<see cref="Weight"/>). Only those two kinds stand in a room filled from it (the elite room's
    /// elite apart). Work order 136 (two waves) reads the lead as wave 1 and the partner as wave 2: a room's line-up is
    /// <see cref="LevelRoom.LineUp"/>, and its groups of the lead's kind are the lead's (the elite apart, <see cref="EnemyGroup.Elite"/>).
    /// </summary>
    [Serializable]
    public sealed class LineUp
    {
        public string Name = "";
        public EnemyKind Lead;
        public int LeadFewest = 1;
        public int LeadMost = 1;
        public EnemyKind Partner;
        public int PartnerFewest = 1;
        public int PartnerMost = 1;
        public float Weight = 1f;
        /// <summary>The floor's full room (<see cref="LineUpValues.FullName"/>): today's mix of three kinds, kept as the generator drew it; no lead, no partner.</summary>
        public bool Full;

        public LineUp() { }

        public LineUp(string name, EnemyKind lead, int leadFewest, int leadMost, EnemyKind partner, int partnerFewest, int partnerMost, float weight = 1f)
        {
            Name = name;
            Lead = lead;
            LeadFewest = leadFewest;
            LeadMost = leadMost;
            Partner = partner;
            PartnerFewest = partnerFewest;
            PartnerMost = partnerMost;
            Weight = weight;
        }

        /// <summary>The full room's mark (one a floor, today's mix).</summary>
        public static LineUp FullRoom(string name) => new LineUp { Name = name, Full = true, Weight = 0f };

        public override string ToString() =>
            Full ? Name : string.Format(CultureInfo.InvariantCulture, "{0} ({1} {2}-{3}, {4} {5}-{6})", Name, EnemyKinds.Word(Lead), LeadFewest, LeadMost,
                                        EnemyKinds.Word(Partner), PartnerFewest, PartnerMost);
    }

    /// <summary>The line-ups one floor draws from (work order 135).</summary>
    [Serializable]
    public sealed class LineUpFloor
    {
        public LineUp[] LineUps = Array.Empty<LineUp>();

        public LineUpFloor() { }

        public LineUpFloor(params LineUp[] lineUps) => LineUps = lineUps;
    }

    /// <summary>
    /// The line-ups' numbers (work order 135; Kristian, 2026-11-25: "og hvert rom er slik: man kommer inn, skyter de små først, så
    /// cahrgerne, og så ranged. det er veldig likt hver gang ... vi trenger en mye større variasjon av utfordirnger i rommene", and his
    /// tick the same evening to "may a room hold only two kinds, the full mix kept for one big room a floor?": "Ja"). Data, all first
    /// guesses (the research digest R3's table "Mix by floor", turned into the kinds and counts the game has), his to change by playing.
    /// Read only with <see cref="LevelValues.LineUps"/> on.
    /// </summary>
    [Serializable]
    public sealed class LineUpValues
    {
        // ---- A room's weight (the order: "the room's budget ... is spent on the two kinds, so a line-up room is about as hard as
        // today's room on that floor"). The generator had no measure of a room's threat: it draws a count from the floor's range and
        // caps brutes and shooters. So a kind's weight here, counted in swarmers, is Lanchester's square law's worth of a unit, the
        // square root of its staying power times its hitting power, each against the swarmer's: its health on a generated floor
        // (LevelValues: swarmer 150, brute 1200, shooter 400, guard 3500, caster 500, bomber 100, lancer 800) times the damage one
        // of it dealt her per fight it stood in, in his sessions of 2026-11-25 (research R3, play-data.md, "Per enemy present":
        // swarmer 0.4, brute 2.0, shooter 2.6, guard 1.3, caster 4.4, bomber 3.3). Rounded to a tenth.
        public float SwarmerWeight = 1f;
        public float BruteWeight = 6.3f;      // sqrt(1200/150 x 2.0/0.4) = 6.32
        public float ShooterWeight = 4.2f;    // sqrt(400/150 x 2.6/0.4) = 4.16
        public float GuardWeight = 8.7f;      // sqrt(3500/150 x 1.3/0.4) = 8.71
        public float CasterWeight = 6.1f;     // sqrt(500/150 x 4.4/0.4) = 6.06
        public float BomberWeight = 2.3f;     // sqrt(100/150 x 3.3/0.4) = 2.35
        // The warder deals nothing (it wards the others): counted as the swarmer whose place it takes today (LevelWarders).
        public float WarderWeight = 1f;
        // The lancer is in none of his sessions: its health's share (800/150) times a brute's damage share (2.0/0.4), sqrt = 5.16.
        public float LancerWeight = 5.2f;

        // ---- The counts (work order 135's second round, the producer's ruling of 2026-11-27: "THE WEIGHT WINS, the caps give way"; the
        // owner finds the game easy already, "klarte det ganske enkelt nå", and work order 136 brings a line-up room in two waves, the lead
        // then the partner, so its enemies no longer all stand there at once). A line-up's own count ranges, per floor (each floor's table
        // holds its own line-ups), are its caps for the brutes and the shooters: the mixed room's three of each (work order 023,
        // LevelValues.BrutesMostInRoom and ShootersMostInRoom) are not read in a line-up room. Per line-up and floor rather than one number
        // for all: the line-ups that need more are those without swarmers, on the deep floors, and each only by as much as it needs.
        /// <summary>The most swarmers any line-up room holds (the producer: "swarmers stay at 14 or under"; "the small ones first" is what he is tired of).</summary>
        public int SwarmersMost = 14;

        /// <summary>
        /// The most a line-up room holds of a kind that is one a room by its nature today (the guard, the caster), where the floor already
        /// lets some room of it hold that many (an exit room's two guards from floor 2, its two casters from floor 3): 2 (the producer's
        /// ruling). The warder stays one (no room holds two), and the bomber and the lancer keep their own rules for a combat room
        /// (LevelBombers, LevelLancers: one on floor 4, two on floor 5).
        /// </summary>
        public int OneARoomMost = 2;

        /// <summary>
        /// How far a line-up room's weight may lie from the weight today's generator gives that very room (its groups as version 24
        /// draws them, the elite apart), as a share of it: the counts are drawn among those within it, else the nearest the line-up's
        /// counts and the room's caps allow (the order: "within a named tolerance"; the producer's first guess, 20 %).
        /// </summary>
        public float WeightTolerance = 0.2f;

        /// <summary>
        /// How much heavier than the same room of version 24 a line-up room is on floors 2 to 5 (<see cref="WeightMoreFromFloor"/> on), as a
        /// share of it: its target is that room's weight times (1 + this). Kristian, 2026-11-27, asked whether the new rooms should weigh
        /// the same as today's, about 15 % more or about 30 % more (he had found the day's run easy, "klarte det ganske enkelt nå"), ticked
        /// "Ca. 15 % tyngre": floors 2 to 5 get rooms about 15 % heavier, more of the heavy kinds, not more small ones. Floor 1 stays at
        /// today's weight (his tick named floors 2 to 5); the full room and the exit room are mixed rooms and stay as they are. 0: the
        /// floors of the second round, the same weight as today's rooms.
        /// </summary>
        public float WeightMore = 0.15f;

        /// <summary>The first floor <see cref="WeightMore"/> is read on: 2 (his tick: "floors 2 to 5").</summary>
        public const int WeightMoreFromFloor = 2;

        /// <summary>A line-up room's target on this floor: the same room's version-24 weight, times (1 + <see cref="WeightMore"/>) from <see cref="WeightMoreFromFloor"/>.</summary>
        public double TargetOf(double today, int floor) => floor >= WeightMoreFromFloor ? today * (1 + Math.Max(0f, WeightMore)) : today;

        /// <summary>The full room's name in the log and on the room (one a floor, today's mix of three kinds).</summary>
        public string FullName = "full";

        /// <summary>The line-ups by floor number from 1, past the end its last (<see cref="DefaultFloors"/>).</summary>
        public LineUpFloor[] Floors = DefaultFloors();

        /// <summary>A kind's weight in swarmers; the act boss none (it is no line-up's).</summary>
        public float WeightOf(EnemyKind kind) =>
            kind == EnemyKind.Swarmer ? SwarmerWeight : kind == EnemyKind.Brute ? BruteWeight : kind == EnemyKind.Shooter ? ShooterWeight
            : kind == EnemyKind.Guard ? GuardWeight : kind == EnemyKind.Caster ? CasterWeight : kind == EnemyKind.Bomber ? BomberWeight
            : kind == EnemyKind.Warder ? WarderWeight : kind == EnemyKind.Lancer ? LancerWeight : 0f;

        /// <summary>A room's weight: every enemy of its groups by its kind's, the act boss and the elite apart.</summary>
        public double WeightOf(IEnumerable<EnemyGroup> groups)
        {
            double w = 0;
            if (groups == null) return w;
            foreach (EnemyGroup g in groups)
                if (g != null && !g.Boss && !g.Elite) w += WeightOf(g.Kind) * Math.Max(0, g.Count);
            return w;
        }

        /// <summary>The line-ups of this floor number.</summary>
        public LineUp[] Of(int floor)
        {
            if (Floors == null || Floors.Length == 0) return Array.Empty<LineUp>();
            LineUpFloor f = Floors[Math.Max(0, Math.Min(Floors.Length - 1, floor - 1))];
            return f?.LineUps ?? Array.Empty<LineUp>();
        }

        /// <summary>
        /// The first guesses, from research R3's table "Mix by floor" (stage 1), each floor's new kind kept to its floor (the act's plan:
        /// the guard from floor 2, the caster and the warder from 3, the bomber and the lancer from 4; a line-up never brings one
        /// sooner, and the bomber, the warder and the lancer never into a floor's first combat room, as their own rules have it).
        /// The counts are ranges; the room's weight picks among them. Floor 1: the pack (swarmers and a brute or two), the gallery
        /// (shooters and swarmers), the brute line (brutes and shooters). Floor 2 adds the guard's two: the guard and its pack, the
        /// guard and shooters. Floor 3 the warded brutes (the research's three-kind "warder + brutes + swarmers" as two: a warder and
        /// fourteen swarmers at most cannot carry a floor-3 room's weight) and casters with shooters. Floor 4 bombers with brutes (the
        /// research's "bombers in the pack", the brutes carrying the weight the swarmers cannot) and lancers with shooters. Floor 5
        /// ("three-kind rooms" in the research; two kinds by his tick) draws from the act's kinds, its guard with shooters. Second round
        /// (the weight wins): the counts on floors 2 to 5 reach the weight of the floor's rooms of version 24, and the lead's top count
        /// stays under the heaviest such room of its floor (it is what wakes at once with work order 136's waves).
        /// </summary>
        public static LineUpFloor[] DefaultFloors() => new[]
        {
            new LineUpFloor(
                new LineUp("pack", EnemyKind.Swarmer, 6, 12, EnemyKind.Brute, 1, 2),
                new LineUp("gallery", EnemyKind.Shooter, 2, 3, EnemyKind.Swarmer, 4, 10),
                new LineUp("brute line", EnemyKind.Brute, 2, 3, EnemyKind.Shooter, 1, 3)),
            new LineUpFloor(
                new LineUp("pack", EnemyKind.Swarmer, 8, 14, EnemyKind.Brute, 2, 6),
                new LineUp("gallery", EnemyKind.Shooter, 3, 8, EnemyKind.Swarmer, 6, 12),
                new LineUp("brute line", EnemyKind.Brute, 2, 5, EnemyKind.Shooter, 2, 4),
                new LineUp("guard and pack", EnemyKind.Guard, 1, 2, EnemyKind.Swarmer, 8, 14),
                new LineUp("guard and shooters", EnemyKind.Guard, 1, 2, EnemyKind.Shooter, 2, 7)),
            new LineUpFloor(
                new LineUp("pack", EnemyKind.Swarmer, 8, 14, EnemyKind.Brute, 3, 7),
                new LineUp("gallery", EnemyKind.Shooter, 4, 10, EnemyKind.Swarmer, 6, 12),
                new LineUp("brute line", EnemyKind.Brute, 3, 6, EnemyKind.Shooter, 2, 6),
                new LineUp("warded brutes", EnemyKind.Brute, 3, 8, EnemyKind.Warder, 1, 1),
                new LineUp("casters and shooters", EnemyKind.Caster, 1, 2, EnemyKind.Shooter, 3, 10)),
            new LineUpFloor(
                new LineUp("pack", EnemyKind.Swarmer, 8, 14, EnemyKind.Brute, 3, 8),
                new LineUp("gallery", EnemyKind.Shooter, 4, 11, EnemyKind.Swarmer, 6, 12),
                new LineUp("brute line", EnemyKind.Brute, 3, 7, EnemyKind.Shooter, 2, 6),
                new LineUp("bombers and brutes", EnemyKind.Brute, 4, 9, EnemyKind.Bomber, 1, 1),
                new LineUp("lancers and shooters", EnemyKind.Lancer, 1, 1, EnemyKind.Shooter, 4, 13)),
            new LineUpFloor(
                new LineUp("pack", EnemyKind.Swarmer, 8, 14, EnemyKind.Brute, 4, 10),
                new LineUp("brute line", EnemyKind.Brute, 4, 8, EnemyKind.Shooter, 3, 7),
                new LineUp("guard and shooters", EnemyKind.Guard, 1, 2, EnemyKind.Shooter, 4, 14),
                new LineUp("casters and shooters", EnemyKind.Caster, 1, 2, EnemyKind.Shooter, 4, 13),
                new LineUp("lancers and shooters", EnemyKind.Lancer, 1, 2, EnemyKind.Shooter, 4, 13)),
        };
    }

    /// <summary>
    /// Line-ups (work order 135, the generator's version 25; Kristian, 2026-11-25: "man kommer inn, skyter de små først, så cahrgerne,
    /// og så ranged. det er veldig likt hver gang", and his tick "Ja" to a room of two kinds with the full mix kept for one big room a
    /// floor). With <see cref="LevelValues.LineUps"/> on (on a floor in one row) every combat room of the floor but one is filled from
    /// ONE line-up of its floor (<see cref="LineUpValues.Of"/>): only its lead kind and its partner kind stand in it, the elite room's
    /// elite beside them as before. One combat room a floor, never the first nor the elite room, is the full room: what today's
    /// generator gives it, three kinds and more. Two combat rooms next to each other on the floor's row never draw the same line-up.
    /// The exit room, floor 5's throne room with the act boss alone, and the elite are as before.
    ///
    /// Each room's counts spend the weight today's generator gives that very room (<see cref="LineUpValues.WeightOf(IEnumerable{EnemyGroup})"/>
    /// of its groups as version 24 draws them, the elite apart) on the two kinds: among the line-up's counts, within the room's most
    /// (<see cref="Most"/>; second round, the weight wins: the brutes and shooters by the line-up's own counts past the mixed room's
    /// three, fourteen swarmers at most, the guard and the caster up to two where the floor already lets a room hold two, the bombers',
    /// warders' and lancers' own rules, so no kind comes to a floor or a first room sooner than today), those whose weight lies within
    /// <see cref="LineUpValues.WeightTolerance"/> of it, one drawn; else the nearest. A count past a most is cut, never turned into
    /// swarmers. Every draw is on a stream of its own (<see cref="Stage"/>), after the bombers, the warders and the lancers, before the
    /// shooters stand singly (work order 124) and the boss room is cleared (130), so those rules hold for the line-up's enemies too.
    /// Off: nothing here is read, and every floor is version 24's, field for field, but for the version.
    /// </summary>
    public static class LevelLineUps
    {
        /// <summary>The generator's stream for the line-ups' draws.</summary>
        public const string Stage = "line-ups";

        /// <summary>
        /// Does the rule hold for floors made with these values: its switch on, on a floor in one row, with the act's plan on (its
        /// line-ups are each floor's kinds as the plan brings them, as the bombers', warders' and lancers' rules are read only with it).
        /// </summary>
        public static bool On(LevelValues v) => v != null && v.LineUps && v.InALine && v.ActPlan && v.LineUpRules != null;

        /// <summary>
        /// The most of a kind a line-up room may hold (a combat room of this floor; the floor's first combat room when
        /// <paramref name="firstCombatRoom"/>), second round (the weight wins): the swarmers <see cref="LineUpValues.SwarmersMost"/>; the
        /// brutes and shooters no cap but the line-up's own counts (the mixed room's three of each are not read); the guards and the
        /// casters as many as any room of the floor may hold (a combat room or the exit room, the act boss not counted), at most
        /// <see cref="LineUpValues.OneARoomMost"/>, none before their floor; the bombers, warders and lancers what their rules let a combat
        /// room hold there (none before their floor, none in the first combat room); the act boss none.
        /// </summary>
        public static int Most(EnemyKind kind, int floor, bool firstCombatRoom, LevelValues v)
        {
            int oneARoom = Math.Max(1, v.LineUpRules?.OneARoomMost ?? 1);
            switch (kind)
            {
                case EnemyKind.Swarmer: return Math.Max(1, v.LineUpRules?.SwarmersMost ?? int.MaxValue);
                case EnemyKind.Brute: return int.MaxValue;
                case EnemyKind.Shooter: return int.MaxValue;
                case EnemyKind.Guard:
                {
                    int combat = v.GuardsMost(RoomKind.Combat, floor);
                    int exit = v.GuardsMost(RoomKind.Exit, floor) - (floor >= v.FloorsPerAct ? 1 : 0); // the exit room's, the act boss counted in it
                    return combat <= 0 ? 0 : Math.Min(oneARoom, Math.Max(combat, exit));
                }
                case EnemyKind.Caster:
                {
                    int combat = v.CastersMost(RoomKind.Combat, floor);
                    return combat <= 0 ? 0 : Math.Min(oneARoom, Math.Max(combat, v.CastersMost(RoomKind.Exit, floor)));
                }
                case EnemyKind.Bomber: return LevelBombers.Most(v, RoomKind.Combat, floor, firstCombatRoom);
                case EnemyKind.Warder: return LevelWarders.Most(v, RoomKind.Combat, floor, firstCombatRoom, int.MaxValue);
                case EnemyKind.Lancer: return LevelLancers.Most(v, RoomKind.Combat, floor, firstCombatRoom);
                default: return 0;
            }
        }

        /// <summary>
        /// The counts a line-up may take in a room: each kind's own range, cut to the room's most (<see cref="Most"/>); with a warder,
        /// its partner at least <see cref="WarderFloorValues.OthersAtLeast"/> (it shields, and stands only with so many). False when
        /// the room may hold none of one of its kinds.
        /// </summary>
        public static bool Counts(LineUp u, int floor, bool firstCombatRoom, LevelValues v, out int leadLo, out int leadHi, out int partnerLo, out int partnerHi)
        {
            leadLo = leadHi = partnerLo = partnerHi = 0;
            if (u == null || u.Full || u.Lead == u.Partner) return false;
            int leadMost = Most(u.Lead, floor, firstCombatRoom, v), partnerMost = Most(u.Partner, floor, firstCombatRoom, v);
            if (leadMost <= 0 || partnerMost <= 0) return false;
            leadHi = Math.Min(Math.Max(1, u.LeadMost), leadMost);
            leadLo = Math.Min(Math.Max(1, u.LeadFewest), leadHi);
            partnerHi = Math.Min(Math.Max(1, u.PartnerMost), partnerMost);
            partnerLo = Math.Min(Math.Max(1, u.PartnerFewest), partnerHi);
            int others = v.WarderFloors != null ? v.WarderFloors.OthersAtLeast : 0;
            if (u.Lead == EnemyKind.Warder) partnerLo = Math.Max(partnerLo, others);
            if (u.Partner == EnemyKind.Warder) leadLo = Math.Max(leadLo, others);
            return leadLo <= leadHi && partnerLo <= partnerHi;
        }

        /// <summary>
        /// The room's counts, spending <paramref name="target"/> (today's room's weight): every pair of counts the line-up may take
        /// (<see cref="Counts"/>), those within <see cref="LineUpValues.WeightTolerance"/> of it, one drawn evenly from
        /// <paramref name="rng"/>; with none, the nearest (the fewest enemies of the nearest, no draw).
        /// </summary>
        public static void Spend(LineUp u, double target, int leadLo, int leadHi, int partnerLo, int partnerHi, LineUpValues w, LevelRandom rng,
                                 out int lead, out int partner)
        {
            double wl = w.WeightOf(u.Lead), wp = w.WeightOf(u.Partner), tol = Math.Max(0f, w.WeightTolerance) * target;
            int within = 0;
            for (int a = leadLo; a <= leadHi; a++)
                for (int b = partnerLo; b <= partnerHi; b++)
                    if (Math.Abs(a * wl + b * wp - target) <= tol + 1e-9) within++;
            lead = leadLo;
            partner = partnerLo;
            if (within > 0)
            {
                int pick = rng.Below(within);
                for (int a = leadLo; a <= leadHi; a++)
                    for (int b = partnerLo; b <= partnerHi; b++)
                        if (Math.Abs(a * wl + b * wp - target) <= tol + 1e-9 && pick-- == 0)
                        {
                            lead = a;
                            partner = b;
                            return;
                        }
                return;
            }
            double best = double.MaxValue;
            for (int n = leadLo + partnerLo; n <= leadHi + partnerHi; n++)
                for (int a = leadLo; a <= leadHi; a++)
                {
                    int b = n - a;
                    if (b < partnerLo || b > partnerHi) continue;
                    double off = Math.Abs(a * wl + b * wp - target);
                    if (off < best - 1e-9)
                    {
                        best = off;
                        lead = a;
                        partner = b;
                    }
                }
        }

        /// <summary>A line-up's groups: the lead's, then the partner's, split as the generator splits a kind (packs, lines, the rest alone).</summary>
        public static List<EnemyGroup> GroupsOf(LineUp u, int lead, int partner, LevelValues v)
        {
            var groups = new List<EnemyGroup>();
            AddKind(groups, u.Lead, lead, v);
            AddKind(groups, u.Partner, partner, v);
            return groups;
        }

        static void AddKind(List<EnemyGroup> groups, EnemyKind kind, int count, LevelValues v)
        {
            if (count <= 0) return;
            int most = kind == EnemyKind.Swarmer ? Math.Max(1, v.PackMost) : kind == EnemyKind.Shooter ? Math.Max(1, v.ShooterLineMost)
                     : kind == EnemyKind.Brute ? Math.Max(1, v.BruteLineMost) : 1;
            int n = (count + most - 1) / most;
            for (int i = 0; i < n; i++) groups.Add(new EnemyGroup(kind, count / n + (i < count % n ? 1 : 0)));
        }

        static bool HoldsTheElite(List<EnemyGroup> groups) => groups != null && groups.Any(g => g != null && g.Elite);

        /// <summary>
        /// Fills a floor's fights from its line-ups (<paramref name="fights"/>: its combat rooms in order and then its exit room, each as
        /// its groups, as the generator lays them into the row), in place, and returns each fight's line-up: the full room's mark for
        /// the one that keeps today's mix, null for the exit room; null (nothing changed) with the rule off.
        /// </summary>
        public static List<LineUp> Put(List<List<EnemyGroup>> fights, ulong seed, int floor, LevelValues v)
        {
            if (fights == null || !On(v)) return null;
            LineUpValues w = v.LineUpRules;
            var drawn = new List<LineUp>();
            for (int i = 0; i < fights.Count; i++) drawn.Add(null);
            int combats = Math.Max(0, fights.Count - 1); // the last is the exit room
            LevelRandom rng = LevelRandom.Stream(seed, Stage, floor);

            // The full room: one combat room, never the first nor the elite room.
            var could = new List<int>();
            for (int i = 1; i < combats; i++)
                if (!HoldsTheElite(fights[i])) could.Add(i);
            int full = could.Count > 0 ? could[rng.Below(could.Count)] : -1;

            LineUp[] table = w.Of(floor);
            LineUp before = null;
            for (int i = 0; i < combats; i++)
            {
                if (i == full)
                {
                    drawn[i] = before = LineUp.FullRoom(w.FullName);
                    continue;
                }
                bool first = i == 0;
                var choices = new List<LineUp>();
                double total = 0;
                foreach (LineUp u in table)
                {
                    if (u == null || u.Full || u.Weight <= 0f || (before != null && u.Name == before.Name)) continue;
                    if (!Counts(u, floor, first, v, out _, out _, out _, out _)) continue;
                    choices.Add(u);
                    total += u.Weight;
                }
                if (choices.Count == 0)
                {
                    drawn[i] = before = null; // none fits: the room keeps what today's generator gives it
                    continue;
                }
                double pick = (rng.NextBits() >> 11) * (1.0 / (1UL << 53)) * total;
                LineUp chosen = choices[choices.Count - 1];
                foreach (LineUp u in choices)
                {
                    if (pick < u.Weight)
                    {
                        chosen = u;
                        break;
                    }
                    pick -= u.Weight;
                }
                Counts(chosen, floor, first, v, out int leadLo, out int leadHi, out int partnerLo, out int partnerHi);
                double target = w.TargetOf(w.WeightOf(fights[i]), floor); // work order 135's third round: his "Ca. 15 % tyngre" from floor 2
                Spend(chosen, target, leadLo, leadHi, partnerLo, partnerHi, w, rng, out int lead, out int partner);
                List<EnemyGroup> elite = fights[i].Where(g => g != null && g.Elite).ToList();
                fights[i].Clear();
                fights[i].AddRange(GroupsOf(chosen, lead, partner, v));
                fights[i].AddRange(elite);
                drawn[i] = before = chosen;
            }
            return drawn;
        }

        /// <summary>
        /// Tells each combat room and the exit room of a floor in a line its line-up (<see cref="LevelRoom.LineUp"/>), in the order
        /// <see cref="Put"/> gave them (the fights' order: the rooms that hold a fight, in the order of the run).
        /// </summary>
        public static void Mark(LevelLayout level, List<LineUp> lineUps)
        {
            if (level == null || lineUps == null) return;
            int next = 0;
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                room.LineUp = next < lineUps.Count ? lineUps[next] : null;
                next++;
            }
        }

        /// <summary>Is this room filled from a line-up of two kinds (not the full room, not a room without one)?</summary>
        public static bool Fills(LevelRoom r) => r != null && r.LineUp != null && !r.LineUp.Full;

        /// <summary>
        /// The validator's rules for them, on rooms that carry a line-up: only its two kinds (the elite apart), each at least one and
        /// within its counts cut to the room's caps; two combat rooms next to each other on the row never of one line-up; the full
        /// room never the floor's first combat room nor its elite room, and at most one a floor.
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues v, List<string> faults)
        {
            if (level == null || v == null || faults == null) return;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            List<LevelRoom> chain = LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();
            int fulls = 0;
            for (int i = 0; i < chain.Count; i++)
            {
                LevelRoom r = chain[i];
                if (r.LineUp == null) continue;
                if (i > 0 && chain[i - 1].LineUp != null && chain[i - 1].LineUp.Name == r.LineUp.Name)
                    faults.Add($"{Name(chain[i - 1])} and {Name(r)}, next to each other on the row, are both the line-up \"{r.LineUp.Name}\"");
                if (r.LineUp.Full)
                {
                    fulls++;
                    if (i == 0) faults.Add($"{Name(r)}, the floor's first combat room, is its full room");
                    if (r.Elite) faults.Add($"{Name(r)}, the elite room, is the floor's full room");
                    continue;
                }
                List<EnemyGroup> others = r.EnemyGroups.Where(g => g != null && !g.Elite && !g.Boss).ToList();
                foreach (EnemyGroup g in others)
                    if (g.Kind != r.LineUp.Lead && g.Kind != r.LineUp.Partner)
                        faults.Add($"{Name(r)} holds a {EnemyKinds.Word(g.Kind)}, and its line-up \"{r.LineUp.Name}\" holds only {EnemyKinds.Word(r.LineUp.Lead)}s and {EnemyKinds.Word(r.LineUp.Partner)}s");
                int lead = others.Where(g => g.Kind == r.LineUp.Lead).Sum(g => g.Count), partner = others.Where(g => g.Kind == r.LineUp.Partner).Sum(g => g.Count);
                if (!Counts(r.LineUp, level.Floor, i == 0, v, out int leadLo, out int leadHi, out int partnerLo, out int partnerHi))
                    faults.Add($"{Name(r)}'s line-up \"{r.LineUp.Name}\" may not stand in it on floor {level.Floor}");
                else if (lead < leadLo || lead > leadHi || partner < partnerLo || partner > partnerHi)
                    faults.Add($"{Name(r)} holds {lead} {EnemyKinds.Word(r.LineUp.Lead)}s and {partner} {EnemyKinds.Word(r.LineUp.Partner)}s, its line-up \"{r.LineUp.Name}\" {leadLo} to {leadHi} and {partnerLo} to {partnerHi} there");
            }
            if (fulls > 1) faults.Add($"the floor has {fulls} full rooms, and one at most");
        }
    }
}
