using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The pause menu's "Light" line (work order 085; Kristian, 2026-11-22, having played work order 083's dark cave: "en ting
    /// jeg ser for meg vi bør ha, er at det er mørkere i rommet. sånn at vi får sett mere flashing når man skyter, ikke standar
    /// lys over alt, men at lysilider rundt er hovedkilden til sol. så får vi mere hule følelse. vi må seff ha en grunnlys
    /// styurke da som gjør at det ikke blir helt bekmørkt."): four steps, "as before" (the floors lit as on main
    /// 6adfc56) and three lamplit levels from mild to dark (<see cref="LamplitValues"/>). Kept in PlayerPrefs by the game's
    /// settings; switchable in the middle of a run; the greybox view wins over it. Since he played it the default is lamplit C (<see cref="Default"/>). Plain data: no engine.
    /// </summary>
    public static class LightSteps
    {
        /// <summary>The steps: 0 as before, 1 lamplit A, 2 lamplit B, 3 lamplit C.</summary>
        public const int Count = 4;
        public const int AsBefore = 0;
        /// <summary>
        /// The step a player gets who has none stored: lamplit C (Kristian, 2026-11-22, having played the four: "C er best"). As
        /// before stays a step of the line; a test that judges today's light chooses it itself.
        /// </summary>
        public const int Default = 3;

        /// <summary>The words of each step on the menu's line.</summary>
        public static readonly string[] Names = { "as before", "lamplit A", "lamplit B", "lamplit C" };

        /// <summary>A stored step made safe: anything not a step is the default (lamplit C).</summary>
        public static int Step(int stored) => stored < 0 || stored >= Count ? Default : stored;

        /// <summary>One step on, round to "as before" after C (Enter, a click, the right arrow).</summary>
        public static int Next(int step) => (Step(step) + 1) % Count;

        /// <summary>One step back, round to C before "as before" (the left arrow).</summary>
        public static int Previous(int step) => (Step(step) + Count - 1) % Count;

        /// <summary>The three lamplit levels, A, B, C, one object each for the whole game (the floors compare them by reference).</summary>
        public static readonly LamplitValues[] Presets = { LamplitValues.A(), LamplitValues.B(), LamplitValues.C() };

        /// <summary>The lamplit values of a step; null for "as before".</summary>
        public static LamplitValues Of(int step)
        {
            step = Step(step);
            return step == AsBefore ? null : Presets[step - 1];
        }
    }

    /// <summary>
    /// One lamplit level (work order 085): how much of the even light is left (the base: the scene's ambient and its key light,
    /// which the rooms' mood turns down on a floor, <see cref="MoodValues.AmbientLevel"/> 0.35 and <see cref="MoodValues.KeyLevel"/>
    /// 0.4 as before), her own small light, how strong the lamps and the shots' flashes are, and the least light an enemy is drawn
    /// with. The lamps' places, their reach and the flashes' lengths are the same in every level (<see cref="LampValues"/>). All
    /// first guesses, judged from the pictures of the nave in the room lab; he tunes by playing.
    /// </summary>
    [Serializable]
    public sealed class LamplitValues
    {
        public string Name;

        // 1. The base: what is left of the even light (shares of the scene's own, as MoodValues.AmbientLevel and KeyLevel are)
        public float AmbientLevel;          // the scene's ambient light kept (as before 0.35)
        public float KeyLevel;              // the scene's key (directional) light kept (as before 0.4)
        public float EnemyLightFloor;       // the least light an enemy is drawn with, its own texture times this, added (as before 0.3): its outline reads in the dark

        // Her own light: a small warm light over her and a little toward the camera, so she is always seen (the order: "a small light of her own, or a rim")
        public float HerLightIntensity;     // as before 1.4 (a wide soft light, range 9 m, 3 m up); round 1's pictures: 1.6 to 2 left her a dark figure among the swarmers
        public float HerLightRange;         // m
        public float HerLightHeight;        // m over the floor
        public float HerLightBack;          // m toward the camera (south), so the side of her the camera sees is lit

        // 2. The lamps: times every lamp's own intensity (LampValues)
        public float LampLevel;

        // 3. Her shots: times every flash's intensity (LampValues: the muzzle, a hit, a blast)
        public float FlashLevel;

        // 4. The fog (the mood's low fog along the walls, MoodValues.Fog). Kristian, 2026-11-22, having played C: "noe rart med
        // tåken når man går til den. liker den, men den må være litt mer subtil", and asked what: "det blir som et rart lag man
        // kommer inn i, det skal jo være litt gjennomsiktig". Its particles ADD a fixed grey (MoodValues.FogColour, unlit): on
        // the floor as before that is a faint haze, on C's dark floor it is brighter than the floor itself, a milky sheet over
        // the floor, her feet and the telegraphs. At a lamplit level it adds only this share of that grey, and nothing round her
        // or round an enemy's telegraph, so what lies under it reads through it. Its place, amount and movement are as before.
        public float FogLevel = 1f;         // times the fog's grey (as before 1)
        public float FogClearNear = 0f;     // m on the ground round her (and an enemy that telegraphs) with no fog added at all
        public float FogClearFar = 0f;      // m from her where the fog is back to its share; between, it rises smoothly

        /// <summary>Does this level change the fog at all? (Level 1 with no clearing leaves it exactly as before.)</summary>
        public bool ChangesFog => FogLevel != 1f || FogClearFar > 0f;

        /// <summary>Level A: clearly darker than as before, the lamps' pools plain.</summary>
        public static LamplitValues A() => new LamplitValues
        {
            Name = "lamplit A",
            AmbientLevel = 0.2f, KeyLevel = 0.2f, EnemyLightFloor = 0.3f,
            HerLightIntensity = 2.8f, HerLightRange = 5f, HerLightHeight = 2.2f, HerLightBack = 0.8f,
            LampLevel = 1f, FlashLevel = 1f,
            FogLevel = 0.75f, FogClearNear = 1.5f, FogClearFar = 4f,
        };

        /// <summary>Level B: the producer's best guess at what he means: the lamps carry the room, dim floor between them.</summary>
        public static LamplitValues B() => new LamplitValues
        {
            Name = "lamplit B",
            AmbientLevel = 0.12f, KeyLevel = 0.09f, EnemyLightFloor = 0.36f,
            HerLightIntensity = 3.2f, HerLightRange = 4.8f, HerLightHeight = 2.2f, HerLightBack = 0.8f,
            LampLevel = 1.15f, FlashLevel = 1.2f,
            FogLevel = 0.6f, FogClearNear = 1.5f, FogClearFar = 4.5f,
        };

        /// <summary>Level C: as dark as the base allows while she, every telegraph and every enemy still read.</summary>
        public static LamplitValues C() => new LamplitValues
        {
            Name = "lamplit C",
            AmbientLevel = 0.07f, KeyLevel = 0.04f, EnemyLightFloor = 0.42f,
            HerLightIntensity = 3.6f, HerLightRange = 4.5f, HerLightHeight = 2.2f, HerLightBack = 0.8f,
            LampLevel = 1.3f, FlashLevel = 1.4f,
            FogLevel = 0.5f, FogClearNear = 1.5f, FogClearFar = 5f,
        };
    }

    /// <summary>What a lamp stands on: a candle (a candelabra or the mood's candles), a brazier or a basin, the vault's gold, a lantern of the crypt kit, a sconce added by the rule, a chest.</summary>
    public enum LampKind { Candle, Brazier, Gold, Lantern, Sconce, Chest }

    /// <summary>
    /// The lamps' places, reach and colours, and the shots' flashes' reach and length (work order 085): the same in every lamplit
    /// level, only the levels' strengths change (<see cref="LamplitValues"/>). Lengths in metres, intensities as the engine's
    /// point lights take them, colours as picked.
    /// </summary>
    [Serializable]
    public sealed class LampValues
    {
        // A lamp's light: its intensity at level 1 and its range
        public float CandleLight = 2.4f, CandleRange = 6f;     // a candelabra, the mood's candles: warm
        public float BrazierLight = 4f, BrazierRange = 9f;     // a brazier or the shrine's basin: its flame is the game's teal, and so is its light
        public float GoldLight = 3f, GoldRange = 7f;           // the vault's chest
        public float LanternLight = 0.8f, LanternRange = 4.5f; // a crypt kit lantern on a wall: its crystal is cyan (round 1's pictures: 1.6 and 6 m, nine of them in the nave, read as a green aquarium: the warm lamps must carry the room)
        public float SconceLight = 4.5f, SconceRange = 9f;     // a sconce the rule adds: a warm flame on an iron bracket (round 1: 3.4 and 8.5 m)
        public float ChestLight = 1.6f, ChestRange = 4.5f;     // a soft warm light over every chest, so it reads from across the room
        public float SourceLift = 0.35f;                        // m a source's light stands over its own height (as the mood's real lights)
        public float LanternLightHeight = 2.4f;                 // m over the floor: a lantern hangs at 2.2 (CryptKitValues.LanternHeight)
        public float LanternOut = 0.7f;                         // m out from the wall's face
        public float ChestLightHeight = 1.4f;
        public Rgb Warm = new Rgb(1f, 0.66f, 0.36f);            // the candles', the sconces' and the chests' light (the mood's candle colour, a little paler)
        public Rgb Teal = new Rgb(0.25f, 0.85f, 0.85f);         // the braziers' and the lanterns' (the game's cyan)
        public Rgb Gold = new Rgb(1f, 0.78f, 0.32f);
        public float Flicker = 0.07f;                           // a lantern's or a sconce's brightness moves by up to this share (the mood's sources move by their own)
        public float FlickerSpeed = 0.9f;                       // about this many times a second
        // Lamps that stand close together (the candles of an eye-catcher, a candelabra beside them) share one light: the nave's
        // seven candles stand within two metres of each other, and seven lights there would be one white spot and seven lights' cost
        public float JoinDistance = 1.6f;   // m on the ground: a lamp this near an earlier one of the same colour gives its light to it
        public float JoinShare = 0.25f;     // the light that carries others is this share stronger for each it carries
        public float JoinMost = 2f;         // up to this many times its own

        // The rule that adds sconces where a room has too few lamps for its size
        public float PoolShare = 0.55f;     // a point of the floor is "in a pool" within this share of a lamp's range (measured on the ground)
        public float WantedCover = 0.5f;    // a room with less of its floor in a pool than this gets sconces
        public int MostSconces = 8;         // at most this many in one room
        public float SconceSpacing = 5f;    // m on the ground from every other lamp of the room
        public float SconceHeight = 2.3f;   // m: the flame's foot over the floor (her height 1.8, a lantern at 2.2)
        public float WallFace = 0.3f;       // m from a wall's line to its face (the kit's wall, CaveValues.FarReach 0.35 with its facets)
        public float SconceOut = 0.45f;     // m its light stands out from the face (over the flame)

        // Her shots' flashes (times the level's FlashLevel): short real lights, pooled
        public float MuzzleLight = 5f, MuzzleRange = 6f, MuzzleSeconds = 0.07f;    // at the muzzle, every shot of hers (FiringWeight's warm light stands aside while one is lit)
        public float MuzzleSizeRef = 0.6f;                                          // the basic shot's flash size (ShotFxSettings.FlashSize): a larger flash lights more, up to MuzzleMost times
        public float MuzzleMost = 2f;
        public float HitLight = 3.5f, HitRange = 4.5f, HitSeconds = 0.09f;          // where a shot lands
        public float BlastLight = 9f, BlastReach = 3f, BlastSeconds = 0.22f;        // a blast (Powder Keg's circle, Scatter Blast's cone, a bomber): its range is the blast's radius plus BlastReach
        public float BlastSparkSize = 2f;                                           // m: a spark this large is a blast's (a hit's is about 0.8)
        public Rgb FlashColour = new Rgb(1f, 0.82f, 0.55f);                         // warm white, as the muzzle flash's own
        public float PaintShare = 0.35f;                                            // a hit's and a blast's light takes this share of the shot's own colour
        public int MostFlashes = 8;                                                 // flash lights at once; the oldest is taken for a new one

        /// <summary>A lamp's intensity at level 1 and its range, by what it stands on.</summary>
        public void Of(LampKind kind, out float intensity, out float range)
        {
            switch (kind)
            {
                case LampKind.Candle: intensity = CandleLight; range = CandleRange; return;
                case LampKind.Brazier: intensity = BrazierLight; range = BrazierRange; return;
                case LampKind.Gold: intensity = GoldLight; range = GoldRange; return;
                case LampKind.Lantern: intensity = LanternLight; range = LanternRange; return;
                case LampKind.Chest: intensity = ChestLight; range = ChestRange; return;
                default: intensity = SconceLight; range = SconceRange; return;
            }
        }

        /// <summary>A lamp's colour, by what it stands on.</summary>
        public Rgb ColourOf(LampKind kind) =>
            kind == LampKind.Brazier || kind == LampKind.Lantern ? Teal : kind == LampKind.Gold ? Gold : Warm;
    }

    /// <summary>One lamp of a floor: its room, what it stands on, where its light stands (on the ground and how high), and what it belongs to.</summary>
    public sealed class Lamp
    {
        public int Room;
        public LampKind Kind;
        public LevelPoint At;
        public double Height;
        /// <summary>A source of the mood: its place in the room's <see cref="MoodRoom.Sources"/>; -1 for none.</summary>
        public int Source = -1;
        /// <summary>A lantern: its place in the kit plan's lanterns; -1 for none.</summary>
        public int Lantern = -1;
        /// <summary>A sconce: the kit section it hangs on (its place in the plan) and the side of it toward the room; -1 for none.</summary>
        public int Section = -1, Side;
        /// <summary>A chest: its place in the room's props; -1 for none.</summary>
        public int Prop = -1;
        /// <summary>The lamp whose light this one shares (it has none of its own); null for one with its own light.</summary>
        public Lamp CarriedBy;
        /// <summary>How many lamps share this one's light.</summary>
        public int Carries;

        /// <summary>Its light's intensity at level 1: its kind's, stronger for each lamp it carries; 0 for a carried one.</summary>
        public float Intensity(LampValues v)
        {
            if (CarriedBy != null) return 0f;
            v.Of(Kind, out float intensity, out _);
            return intensity * Math.Min(Math.Max(1f, v.JoinMost), 1f + v.JoinShare * Carries);
        }
    }

    /// <summary>
    /// Where the lamps of a floor stand in the lamplit light (work order 085), from its layout, its crypt kit plan and its mood plan
    /// alone: the things that burn (every source of the mood: the braziers, basins, candelabras and candles; every lantern of the
    /// kit), a soft light over every chest, and, where a room has too little of its floor within a pool for its size
    /// (<see cref="LampValues.WantedCover"/>), sconces added by a rule: on the face toward the room of a full wall section of the
    /// kit that is far for the game's camera (never a near lip, never a niche, never by a door, never where a lantern hangs),
    /// each where it adds the most floor in a pool, at least <see cref="LampValues.SconceSpacing"/> from every other lamp, until
    /// the room is covered or has <see cref="LampValues.MostSconces"/>. A sconce has no collider and stands in no one's way: it
    /// is a flame on a wall. Plain data: no engine; the same plan whatever the level (only the strengths differ).
    /// </summary>
    public sealed class LampPlan
    {
        public readonly List<Lamp> Lamps = new List<Lamp>();
        /// <summary>The share of each room's floor within a pool, by its place in the run: with the things that burn alone, and with the sconces added.</summary>
        public readonly Dictionary<int, double> CoverBefore = new Dictionary<int, double>();
        public readonly Dictionary<int, double> CoverAfter = new Dictionary<int, double>();

        /// <summary>The lamps of a room.</summary>
        public IEnumerable<Lamp> Of(int room)
        {
            foreach (Lamp l in Lamps)
                if (l.Room == room) yield return l;
        }

        public static LampPlan Make(LevelLayout layout, CryptKitPlan kit, RoomMoodPlan mood, LampValues v)
        {
            v = v ?? new LampValues();
            var plan = new LampPlan();
            double cs = layout.CellSize;

            // The things that burn: the mood's sources, the kit's lanterns, the chests.
            if (mood != null)
                foreach (MoodRoom mr in mood.Rooms)
                    for (int i = 0; i < mr.Sources.Count; i++)
                    {
                        MoodSource s = mr.Sources[i];
                        LampKind kind = s.Kind == MoodSourceKind.Candle ? LampKind.Candle : s.Kind == MoodSourceKind.Gold ? LampKind.Gold : LampKind.Brazier;
                        plan.Lamps.Add(new Lamp { Room = mr.Room.Order, Kind = kind, At = s.At, Height = s.Height + v.SourceLift, Source = i });
                    }
            if (kit != null)
                for (int i = 0; i < kit.Lanterns.Count; i++)
                {
                    CryptKitLantern l = kit.Lanterns[i];
                    CryptKitSection s = kit.Sections[l.Section];
                    plan.Lamps.Add(new Lamp { Room = l.Room, Kind = LampKind.Lantern, At = OnFace(s, l.Side, cs, v.WallFace + v.LanternOut), Height = v.LanternLightHeight, Lantern = i });
                }
            foreach (LevelRoom room in layout.Rooms)
                for (int i = 0; i < room.Props.Count; i++)
                {
                    LevelProp p = room.Props[i];
                    if (p.Model != PropModel.Chest) continue;
                    bool gold = false;
                    foreach (Lamp l in plan.Lamps)
                        if (l.Room == room.Order && l.Kind == LampKind.Gold && l.At.DistanceTo(p.Position) < 0.5) gold = true;
                    if (!gold) plan.Lamps.Add(new Lamp { Room = room.Order, Kind = LampKind.Chest, At = p.Position, Height = v.ChestLightHeight, Prop = i });
                }
            // Close ones of the same colour share a light.
            for (int i = 0; i < plan.Lamps.Count; i++)
            {
                Lamp l = plan.Lamps[i];
                for (int k = 0; k < i; k++)
                {
                    Lamp e = plan.Lamps[k];
                    if (e.CarriedBy != null || e.Room != l.Room || !SameColour(v, e.Kind, l.Kind) || e.At.DistanceTo(l.At) > v.JoinDistance) continue;
                    l.CarriedBy = e;
                    e.Carries++;
                    break;
                }
            }

            // Each room's cover, and sconces where it is too little.
            foreach (LevelRoom room in layout.Rooms)
            {
                var own = new List<Lamp>();
                foreach (Lamp l in plan.Lamps)
                    if (l.Room == room.Order) own.Add(l);
                List<LevelPoint> floor = FloorPoints(layout, room);
                bool[] lit = new bool[floor.Count];
                foreach (Lamp l in own) Light(floor, lit, l, v);
                double cover = Share(lit);
                plan.CoverBefore[room.Order] = cover;
                if (kit != null && cover < v.WantedCover && floor.Count > 0)
                {
                    var places = new List<Lamp>();
                    for (int i = 0; i < kit.Sections.Count; i++)
                    {
                        CryptKitSection s = kit.Sections[i];
                        if (s.Low || s.Niche || s.AtDoor || CaveWalls.IsNear(s, CaveWalls.GameYaw)) continue;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (s.RoomOn(side) != room.Order || HasLantern(kit, i, side)) continue;
                            places.Add(new Lamp { Room = room.Order, Kind = LampKind.Sconce, At = OnFace(s, side, cs, v.WallFace + v.SconceOut), Height = v.SconceHeight, Section = i, Side = side });
                        }
                    }
                    int added = 0;
                    var trial = new bool[lit.Length];
                    while (cover < v.WantedCover && added < v.MostSconces)
                    {
                        Lamp best = null;
                        int bestGain = 0;
                        foreach (Lamp p in places)
                        {
                            if (TooClose(p, own, v.SconceSpacing)) continue;
                            Array.Copy(lit, trial, lit.Length);
                            int gain = Light(floor, trial, p, v);
                            if (gain > bestGain)
                            {
                                bestGain = gain;
                                best = p;
                            }
                        }
                        if (best == null) break;
                        Light(floor, lit, best, v);
                        own.Add(best);
                        plan.Lamps.Add(best);
                        places.Remove(best);
                        added++;
                        cover = Share(lit);
                    }
                }
                plan.CoverAfter[room.Order] = cover;
            }
            return plan;
        }

        /// <summary>The ground point <paramref name="out"/> metres from a section's line, at its middle, on the side toward the room.</summary>
        public static LevelPoint OnFace(CryptKitSection s, int side, double cs, double @out)
        {
            LevelPoint mid = s.Vertical ? new LevelPoint(s.Line * cs, (s.Cell + 0.5) * cs) : new LevelPoint((s.Cell + 0.5) * cs, s.Line * cs);
            return s.Vertical ? new LevelPoint(mid.X + side * @out, mid.Y) : new LevelPoint(mid.X, mid.Y + side * @out);
        }

        static bool SameColour(LampValues v, LampKind a, LampKind b)
        {
            Rgb x = v.ColourOf(a), y = v.ColourOf(b);
            return x.R == y.R && x.G == y.G && x.B == y.B;
        }

        /// <summary>How many lamps have a light of their own (the others share one).</summary>
        public int LightsOf(int room)
        {
            int n = 0;
            foreach (Lamp l in Lamps)
                if (l.Room == room && l.CarriedBy == null) n++;
            return n;
        }

        static bool HasLantern(CryptKitPlan kit, int section, int side)
        {
            foreach (CryptKitLantern l in kit.Lanterns)
                if (l.Section == section && l.Side == side) return true;
            return false;
        }

        static bool TooClose(Lamp p, List<Lamp> own, double spacing)
        {
            foreach (Lamp l in own)
                if (l.At.DistanceTo(p.At) < spacing) return true;
            return false;
        }

        /// <summary>The room's open floor, a point a metre: the middle of each square metre of its open cells.</summary>
        public static List<LevelPoint> FloorPoints(LevelLayout layout, LevelRoom room)
        {
            var points = new List<LevelPoint>();
            double cs = layout.CellSize;
            int per = Math.Max(1, (int)Math.Round(cs));
            for (int cy = room.Rect.Y; cy < room.Rect.Top; cy++)
                for (int cx = room.Rect.X; cx < room.Rect.Right; cx++)
                {
                    if (!room.IsOpen(cx, cy)) continue;
                    for (int j = 0; j < per; j++)
                        for (int i = 0; i < per; i++)
                            points.Add(new LevelPoint((cx + (i + 0.5) / per) * cs, (cy + (j + 0.5) / per) * cs));
                }
            return points;
        }

        /// <summary>Marks the points within the lamp's pool; returns how many it marked that were not marked.</summary>
        static int Light(List<LevelPoint> floor, bool[] lit, Lamp l, LampValues v)
        {
            v.Of(l.Kind, out _, out float range);
            double r = range * v.PoolShare, r2 = r * r;
            int gained = 0;
            for (int i = 0; i < floor.Count; i++)
            {
                if (lit[i]) continue;
                double dx = floor[i].X - l.At.X, dy = floor[i].Y - l.At.Y;
                if (dx * dx + dy * dy > r2) continue;
                lit[i] = true;
                gained++;
            }
            return gained;
        }

        static double Share(bool[] lit)
        {
            if (lit.Length == 0) return 1.0;
            int n = 0;
            foreach (bool b in lit) if (b) n++;
            return n / (double)lit.Length;
        }

        /// <summary>A room's lamps and cover in one line, for the tests' logs and the report: "nave: 2 brazier, 4 lantern; 31 % in a pool, 6 sconces added: 58 %".</summary>
        public string Describe(LevelRoom room, string name = null)
        {
            var counts = new int[Enum.GetValues(typeof(LampKind)).Length];
            foreach (Lamp l in Of(room.Order)) counts[(int)l.Kind]++;
            var sb = new StringBuilder();
            sb.Append(name ?? room.Letter + room.Order.ToString(CultureInfo.InvariantCulture)).Append(": ");
            bool first = true;
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0 || (LampKind)k == LampKind.Sconce) continue;
                sb.Append(first ? "" : ", ").Append(counts[k]).Append(' ').Append(((LampKind)k).ToString().ToLowerInvariant());
                first = false;
            }
            if (first) sb.Append("nothing that burns");
            sb.Append(string.Format(CultureInfo.InvariantCulture, "; {0:0} % of its floor in a pool", 100.0 * CoverBefore[room.Order]));
            int sconces = counts[(int)LampKind.Sconce];
            if (sconces > 0) sb.Append(string.Format(CultureInfo.InvariantCulture, ", {0} sconces added: {1:0} %", sconces, 100.0 * CoverAfter[room.Order]));
            sb.Append(string.Format(CultureInfo.InvariantCulture, "; {0} real lights", LightsOf(room.Order)));
            return sb.ToString();
        }
    }
}
