using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the rooms' mood (work order 044; Kristian's words and choice of 2026-11-20: "The rooms must also feel
    /// more alive and create atmosphere like Diablo and Path of Exile", and of the five concept pictures drawn for him,
    /// "build toward them": darkness between the light sources, warm candle pools against cold teal glow, low fog, dust and
    /// embers, a worn floor, one eye-catcher per room with the middle kept clear). All first guesses, with their defaults
    /// in code, for turning in the Inspector while playing (GeneratedLevels' Values, Mood). Every layer has its own switch;
    /// the master switch is <see cref="LevelValues.RoomMood"/>. Lengths in metres, colours as picked.
    /// </summary>
    [Serializable]
    public sealed class MoodValues
    {
        // 1. Light and darkness
        public bool Darkness = true;            // the scene's ambient and key light go down on a floor, back up when it goes
        public float AmbientLevel = 0.35f;      // share of the scene's ambient light kept on a floor (1: today's)
        public float KeyLevel = 0.4f;           // share of the scene's key (directional) light kept on a floor (1: today's)
        public Rgb DarkTint = new Rgb(0.84f, 0.88f, 1f); // the light that is left, a little cold
        public bool Sources = true;             // candles, braziers and the vault's gold: pools of light on the floor, flames, a few real lights
        public float PoolLevel = 1f;            // times every pool's brightness
        public float CandlePool = 0.32f;        // a candle's pool, brightest at its middle (additive, linear light)
        public float CandleReach = 1.9f;        // its radius
        public float BrazierPool = 0.3f;        // a brazier's or the basin's teal pool
        public float BrazierReach = 3.2f;
        public float GoldPool = 0.3f;           // the vault's chest
        public float GoldReach = 2.4f;
        public Rgb CandleColour = new Rgb(1f, 0.62f, 0.3f);
        public Rgb Teal = new Rgb(0.25f, 0.85f, 0.85f); // the game's cyan (CryptKitValues.Cyan)
        public Rgb GoldColour = new Rgb(1f, 0.78f, 0.32f);
        public float FlameGlow = 2.4f;          // a flame's core, over the bloom's threshold
        public float CandleFlicker = 0.05f;     // a candle's brightness moves by up to this share (slow, not a strobe)
        public float CandleFlickerSpeed = 1.3f; // about this many times a second
        public float BrazierFlicker = 0.08f;
        public float BrazierFlickerSpeed = 0.6f;
        public float CandleLight = 1.6f;        // a real light at a candle, when the budget gives it one
        public float CandleLightRange = 5f;
        public float BrazierLight = 2f;
        public float BrazierLightRange = 7f;
        public float MiddleFill = 0.05f;        // a faint cold pool over each room's middle, so the middle is never the darkest part
        public float MiddleFillShare = 0.5f;    // its radius, a share of the room's short side
        public bool HerLight = true;            // a soft light that goes with her
        public float HerLightIntensity = 1.4f;
        public float HerLightRange = 9f;
        public float HerLightHeight = 3f;
        public Rgb HerLightColour = new Rgb(1f, 0.93f, 0.84f);
        public float EnemyLightFloor = 0.3f;    // the least light an enemy is drawn with: its own texture times this, added (0: none)

        // 2. Air
        public bool Fog = true;                 // low fog along the walls and in the corners, never thick in the middle
        public float FogLevel = 1f;             // times the fog's brightness
        public int FogMostPerRoom = 24;         // particles at once, a room
        public float FogSize = 4f;
        public float FogHeight = 0.3f;
        public Rgb FogColour = new Rgb(0.11f, 0.12f, 0.14f);
        public bool Dust = true;                // motes drifting round her
        public int DustMost = 50;
        public float DustLevel = 1f;
        public bool Embers = true;              // embers rising from the braziers
        public int EmbersPerBrazier = 10;

        // 3. A worn floor
        public bool Wear = true;
        public float WearDensity = 3.5f;         // pieces of wear (rubble, bones, stains, cracks, tint patches) per 100 square metres
        public float MiddleClear = 4f;          // m round a room's middle with no wear (at most 0.3 of its short side)
        public float DoorClear = 2.5f;          // m from a door's opening
        public float PlaceClear = 1.5f;         // m from her start and from every enemy's place
        public float TintVariation = 0.06f;     // the floor's patches of lighter and darker stone, up to this share

        // 4. One eye-catcher per room theme
        public bool Compose = true;             // the dais, the carpet, the coins, the candles; and the ossuary's piles banked at its far wall (the generator)
        public float DaisMargin = 0.4f;         // a dais reaches this far past what stands on it (inside the ground she cannot reach)
        public float DaisHeight = 0.12f;
        public float CarpetWidth = 2f;

        // 5. A room tone
        public bool Tone = true;
        public float ToneVolume = 0.05f;        // times the sounds' master volume; the game's volume does the rest
    }

    /// <summary>What a light source of a room is: a candle (warm, small), a brazier or a basin (teal, larger), the vault's gold.</summary>
    public enum MoodSourceKind { Candle, Brazier, Gold }

    /// <summary>One light source: its room, where it stands, how high its light is, whether it has a flame drawn, and its rank for a real light.</summary>
    public sealed class MoodSource
    {
        public int Room;
        public MoodSourceKind Kind;
        public LevelPoint At;
        public double Height;
        public bool Flame;
        /// <summary>Of a prop of the description (a brazier, a basin, a candelabra, the chest), not of a composition.</summary>
        public bool OfProp;
    }

    /// <summary>The kinds of wear: rubble, bones and skulls along the walls and in the corners; stains, wax, cracks, patches and missing tiles flat on the floor.</summary>
    public enum WearKind { Rubble, Bone, Skull, Stain, Wax, Crack, Patch, Hole }

    /// <summary>One piece of wear: its kind, place, size, turn and a number for the shape's jitter.</summary>
    public sealed class WearPiece
    {
        public int Room;
        public WearKind Kind;
        public LevelPoint At;
        public double Size;
        public int Turn;
        public uint Variant;

        /// <summary>Flat on the floor: walking and shots pass over it as over the floor.</summary>
        public bool Flat => Kind == WearKind.Stain || Kind == WearKind.Wax || Kind == WearKind.Crack || Kind == WearKind.Patch || Kind == WearKind.Hole;

        /// <summary>How far it reaches from its middle, for the rules.</summary>
        public double Reach => ReachOf(Kind, Size);

        public static double ReachOf(WearKind kind, double size)
        {
            switch (kind)
            {
                case WearKind.Rubble: return size * 0.6;
                case WearKind.Bone: return size * 0.5;
                case WearKind.Skull: return 0.12;
                case WearKind.Crack: return size * 0.5;
                case WearKind.Hole: return size + 0.2;
                default: return size;
            }
        }
    }

    /// <summary>A piece of a room's eye-catcher made in code: a dais (square or round), the carpet, a coin heap, coins on the floor, candles.</summary>
    public enum CompositionKind { Dais, RoundDais, Carpet, CoinHeap, Coins, Candles }

    public sealed class CompositionPiece
    {
        public int Room;
        public CompositionKind Kind;
        /// <summary>Its middle; the carpet's start, at the door in.</summary>
        public LevelPoint At;
        /// <summary>The carpet's end, at the dais.</summary>
        public LevelPoint To;
        /// <summary>Across its own x and its own z (a round one: Width is its radius); the carpet's width.</summary>
        public double Width, Depth;
        /// <summary>Degrees, clockwise from above, as a prop's.</summary>
        public int Turn;
        /// <summary>Of a dais: the height of what stands on it, for the candles on its edge.</summary>
        public double Height;
        public uint Variant;

        /// <summary>How far it reaches from its middle.</summary>
        public double Reach =>
            Kind == CompositionKind.RoundDais ? Width
            : Kind == CompositionKind.Dais ? 0.5 * Math.Sqrt(Width * Width + Depth * Depth)
            : Kind == CompositionKind.Candles ? 0.2
            : Kind == CompositionKind.CoinHeap ? Width
            : Kind == CompositionKind.Coins ? Width
            : 0;
    }

    /// <summary>A room's mood: its far wall, its sources, its wear, its eye-catcher and the empty place left there for a large prop.</summary>
    public sealed class MoodRoom
    {
        public LevelRoom Room;
        /// <summary>The wall opposite the door she comes in by (north without one); the throne room's, the throne's wall.</summary>
        public int FarSide = LevelGenerator.North;
        public readonly List<MoodSource> Sources = new List<MoodSource>();
        public readonly List<WearPiece> Wear = new List<WearPiece>();
        public readonly List<CompositionPiece> Pieces = new List<CompositionPiece>();
        /// <summary>
        /// Where one new large prop (a statue, an altar; the producer has them modelled) can stand later: a place where the
        /// game's statue would keep every rule of the props (<see cref="PropRules.Fault"/> and the clear lane) with the
        /// room's props as they are, near the middle of the far wall, clear of the eye-catcher. Null when the wall has none.
        /// </summary>
        public LevelPoint? LargePropPlace;
        public int LargePropTurn;
        /// <summary>The wall the empty place is at: the far wall, else a side wall.</summary>
        public int LargePropSide;
        /// <summary>
        /// Work order 052 (<see cref="LevelValues.LargeProps"/>): the large props of the room's composition, props of the floor
        /// that it builds with the others (RoomMoodLargeProps.cs).
        /// </summary>
        public readonly List<LevelProp> LargeProps = new List<LevelProp>();
        /// <summary>Props of the description a large prop stands in place of (the throne's pair, the shrine's basin): not built.</summary>
        public readonly List<LevelProp> Replaced = new List<LevelProp>();
        /// <summary>Props of the description standing on the dais model: drawn <see cref="PropSize.DaisShows"/> higher, their colliders where they were.</summary>
        public readonly List<LevelProp> Raised = new List<LevelProp>();
        /// <summary>Why the theme's large prop is not there, when it is not; null when it is.</summary>
        public string LargeLeftOut;
    }

    /// <summary>
    /// Where a floor's mood goes (work order 044), from its layout alone: plain data, no engine, so the rules can be tested and
    /// the floor only places what this says (<c>RoomMood</c> in the game).
    ///
    /// From the seed: the stream "room mood" of the floor's seed and number (<see cref="LevelRandom"/>), which no other stage
    /// reads, so no seed's layout, enemies or props change by it. The props stand where the generator put them; the only
    /// change to them is the ossuary's (<see cref="MoodValues.Compose"/>, in the props stage itself: its skull and bone piles
    /// banked at its far wall).
    ///
    /// Sources: each brazier and basin (teal), candelabra (warm) and, in a vault, the chest (gold); each set of candles of an
    /// eye-catcher. Wear: about <see cref="MoodValues.WearDensity"/> pieces a hundred square metres, rubble, bones and skulls
    /// along the walls and in the corners, stains, cracks, missing tiles and patches of lighter or darker stone on the floor;
    /// wax by each candle, a stain by each coffin and sarcophagus. Every piece of wear keeps <see cref="WearFault"/>: off the
    /// room's middle, its doorways and every place an enemy or she stands at, and (one that is not flat) off every prop.
    ///
    /// The eye-catchers (the concept pictures): the throne room's throne on a low dais with a long carpet from the door in and
    /// candles on the dais's front corners, its braziers or basins already either side; the tomb hall's coffins and
    /// sarcophagi, already in two rows, with candles at their heads and feet; the vault's chest on a dais with coin heaps and
    /// a gold glow (the archive's chest on a dais); the shrine's basin on a round dais with candles round it and rows of
    /// candles along the altar's sides; the ossuary's piles banked against its far wall with bones and skulls at their feet;
    /// the archive's shelves with candles at their ends. Each with its <see cref="MoodRoom.LargePropPlace"/>.
    /// </summary>
    public sealed partial class RoomMoodPlan
    {
        public const string Stage = "room mood";

        public readonly List<MoodRoom> Rooms = new List<MoodRoom>();

        static double Unit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));
        static double Between(LevelRandom rng, double a, double b) => a + (b - a) * Unit(rng);

        /// <summary>The radius round a room's middle that no wear may reach into.</summary>
        public static double MiddleClearOf(LevelLayout layout, LevelRoom room, MoodValues mv)
        {
            double shortSide = Math.Min(room.Rect.Width, room.Rect.Height) * layout.CellSize;
            return Math.Max(0, Math.Min(mv.MiddleClear, 0.3 * shortSide));
        }

        /// <summary>
        /// The first rule a piece of wear at <paramref name="at"/> reaching <paramref name="reach"/> breaks, or null: inside its
        /// room clear of the walls' lines, off the middle (<see cref="MiddleClearOf"/>), at least <see cref="MoodValues.DoorClear"/>
        /// from each door's opening, <see cref="MoodValues.PlaceClear"/> from her start and every enemy's place, and, one that
        /// is not flat, off every prop's footprint.
        /// </summary>
        public static string WearFault(LevelLayout layout, LevelRoom room, LevelPoint at, double reach, bool flat, MoodValues mv)
        {
            double cs = layout.CellSize;
            if (RoomShapes.DistanceToWalls(room, at, cs) < reach + 0.3 || layout.RoomAt(at.X, at.Y) != room) return "reaches into a wall"; // work order 051: to the outline
            if (at.DistanceTo(room.Middle(cs)) - reach < MiddleClearOf(layout, room, mv)) return "lies in the room's middle";
            foreach (LevelDoor d in layout.DoorsOf(room.Order))
                if (LevelGeometry.DistanceToDoor(at, d, cs) - reach < mv.DoorClear) return "lies in a doorway";
            if (room.PlayerStart.HasValue && room.PlayerStart.Value.DistanceTo(at) - reach < mv.PlaceClear) return "lies on her start";
            for (int i = 0; i < room.EnemyPoints.Count; i++)
            {
                if (room.EnemyPoints[i].DistanceTo(at) - reach < mv.PlaceClear) return "lies on an enemy's place";
                foreach (LevelPoint m in room.PlacesAt(i))
                    if (m.DistanceTo(at) - reach < mv.PlaceClear) return "lies on an enemy's place";
            }
            if (!flat)
                foreach (LevelProp p in room.Props)
                    if (p.Footprint.DistanceTo(at) - reach < 0.05) return "lies on the " + p.Model;
            return null;
        }

        static bool InsideClear(LevelLayout layout, LevelRoom room, LevelPoint at, double reach)
        {
            if (RoomShapes.DistanceToWalls(room, at, layout.CellSize) < reach + 0.3) return false; // work order 051: to the outline
            foreach (LevelProp p in room.Props)
                if (p.Footprint.DistanceTo(at) - reach < 0.05) return false;
            return true;
        }

        public static RoomMoodPlan Make(LevelLayout layout, LevelValues values)
        {
            values = values ?? new LevelValues();
            MoodValues mv = values.Mood ?? new MoodValues();
            var plan = new RoomMoodPlan();
            LevelRandom rng = LevelRandom.Stream(layout.Seed, Stage, layout.Floor);
            CryptKitPlan kit = null; // work order 052: where the kit's niches and lanterns are, for the large props at a wall
            foreach (LevelRoom room in layout.Rooms)
            {
                var mr = new MoodRoom { Room = room };
                LevelDoor entry = LevelGeometry.EntryDoor(layout, room);
                int wayIn = entry != null ? LevelGeometry.WallOf(room.Rect, entry) : -1;
                mr.FarSide = wayIn >= 0 ? LevelGenerator.Opposite(wayIn) : LevelGenerator.North;
                if (mv.Compose) Compose(layout, room, mr, values, rng);
                // Work order 051: no piece of an eye-catcher on a carved cell (the carpet's runs are checked as they are laid).
                if (room.SolidCount > 0) mr.Pieces.RemoveAll(c => OnCarved(room, c, layout.CellSize));
                SourcesOf(layout, room, mr, mv.Compose);
                if (mv.Wear) WearOf(layout, room, mr, mv, rng);
                if (mv.Compose) mr.LargePropPlace = LargePlace(layout, room, mr, values, out mr.LargePropTurn, out mr.LargePropSide);
                if (mv.Compose && room.Theme == RoomTheme.TombHall && mr.LargePropPlace.HasValue)
                {
                    // The tomb hall's far wall, as the picture's knight: a set of candles either side of the empty place.
                    LevelPoint into = PropRules.Inward(mr.LargePropSide), along = new LevelPoint(-into.Y, into.X), c = mr.LargePropPlace.Value;
                    foreach (int s in new[] { -1, 1 })
                        AddCandles(layout, room, mr, new LevelPoint(c.X + along.X * 1.25 * s, c.Y + along.Y * 1.25 * s), rng);
                }
                // Work order 052: the large props, last, drawing nothing from the stream.
                if (mv.Compose && values.LargeProps && values.Props) PlaceLarge(layout, room, mr, values, ref kit);
                plan.Rooms.Add(mr);
            }
            return plan;
        }

        // ---------------- Sources ----------------

        static void SourcesOf(LevelLayout layout, LevelRoom room, MoodRoom mr, bool compose)
        {
            foreach (LevelProp p in room.Props)
            {
                switch (p.Model)
                {
                    case PropModel.Brazier: mr.Sources.Insert(0, new MoodSource { Room = room.Order, Kind = MoodSourceKind.Brazier, At = p.Position, Height = 1.05, Flame = true, OfProp = true }); break;
                    case PropModel.Basin: mr.Sources.Insert(0, new MoodSource { Room = room.Order, Kind = MoodSourceKind.Brazier, At = p.Position, Height = 1.0, OfProp = true }); break;
                    case PropModel.Candelabra: mr.Sources.Add(new MoodSource { Room = room.Order, Kind = MoodSourceKind.Candle, At = p.Position, Height = 1.75, OfProp = true }); break;
                    case PropModel.Chest:
                        if (compose && room.Theme == RoomTheme.Vault)
                            mr.Sources.Add(new MoodSource { Room = room.Order, Kind = MoodSourceKind.Gold, At = p.Position, Height = 1.2, OfProp = true });
                        break;
                }
            }
            // A real light goes to the first ones: braziers, the gold, then candles, nearest the middle first.
            LevelPoint m = room.Middle(layout.CellSize);
            List<MoodSource> ranked = mr.Sources.OrderBy(s => s.Kind == MoodSourceKind.Brazier ? 0 : s.Kind == MoodSourceKind.Gold ? 1 : 2)
                                                .ThenBy(s => s.At.DistanceTo(m)).ToList();
            mr.Sources.Clear();
            mr.Sources.AddRange(ranked);
        }

        // ---------------- Wear ----------------

        static readonly WearKind[] s_kinds = { WearKind.Rubble, WearKind.Bone, WearKind.Skull, WearKind.Stain, WearKind.Crack, WearKind.Patch };
        static readonly int[] s_weights = { 30, 15, 6, 18, 16, 15 };

        static double SizeOf(WearKind k, LevelRandom rng)
        {
            switch (k)
            {
                case WearKind.Rubble: return Between(rng, 0.5, 1.1);
                case WearKind.Bone: return Between(rng, 0.35, 0.5);
                case WearKind.Skull: return 0.2;
                case WearKind.Stain: return Between(rng, 0.5, 1.2);
                case WearKind.Wax: return Between(rng, 0.15, 0.3);
                case WearKind.Crack: return Between(rng, 1.0, 2.2);
                case WearKind.Patch: return Between(rng, 1.2, 2.2);
                default: return Between(rng, 0.5, 0.9);
            }
        }

        /// <summary>Puts a piece at the first of up to ten drawn places that keeps <see cref="WearFault"/>; false if none did.</summary>
        static bool TryWear(LevelLayout layout, LevelRoom room, MoodRoom mr, WearKind kind, Func<LevelPoint> where, MoodValues mv, LevelRandom rng)
        {
            double size = SizeOf(kind, rng);
            int turn = rng.Below(360);
            uint variant = (uint)rng.NextBits();
            var piece = new WearPiece { Room = room.Order, Kind = kind, Size = size, Turn = turn, Variant = variant };
            for (int t = 0; t < 10; t++)
            {
                LevelPoint at = where();
                if (WearFault(layout, room, at, piece.Reach, piece.Flat, mv) != null) continue;
                piece.At = at;
                mr.Wear.Add(piece);
                return true;
            }
            return false;
        }

        static void WearOf(LevelLayout layout, LevelRoom room, MoodRoom mr, MoodValues mv, LevelRandom rng)
        {
            double cs = layout.CellSize, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            double area = (x1 - x0) * (y1 - y0);
            int n = (int)Math.Round(area / 100.0 * Math.Max(0f, mv.WearDensity));
            int total = s_weights.Sum();
            LevelPoint AtWall()
            {
                int side = rng.Below(4);
                double depth = Between(rng, 0.45, 1.5);
                double along = AlongX(side) ? Between(rng, x0 + 0.5, x1 - 0.5) : Between(rng, y0 + 0.5, y1 - 0.5);
                return OnWall(side, along, depth, x0, x1, y0, y1);
            }
            LevelPoint InCorner()
            {
                int c = rng.Below(4);
                double dx = Between(rng, 0.45, 2.2), dy = Between(rng, 0.45, 2.2);
                return new LevelPoint((c & 1) == 0 ? x0 + dx : x1 - dx, (c & 2) == 0 ? y0 + dy : y1 - dy);
            }
            LevelPoint Anywhere() => new LevelPoint(Between(rng, x0 + 0.6, x1 - 0.6), Between(rng, y0 + 0.6, y1 - 0.6));

            for (int k = 0; k < n; k++)
            {
                int pick = rng.Below(total), i = 0;
                while (pick >= s_weights[i]) pick -= s_weights[i++];
                WearKind kind = s_kinds[i];
                bool lying = kind == WearKind.Rubble || kind == WearKind.Bone || kind == WearKind.Skull;
                bool corner = rng.Below(10) < 3;
                TryWear(layout, room, mr, kind, lying ? (corner ? (Func<LevelPoint>)InCorner : AtWall) : Anywhere, mv, rng);
            }
            // A missing tile or two in a larger room, with rubble in it.
            if (area > 150 && rng.Below(10) < 7) TryWear(layout, room, mr, WearKind.Hole, Anywhere, mv, rng);
            // Wax by each candle; a stain by each coffin and sarcophagus.
            foreach (MoodSource s in mr.Sources.Where(s => s.Kind == MoodSourceKind.Candle).ToList())
            {
                int pools = 1 + rng.Below(2);
                for (int k = 0; k < pools; k++)
                {
                    LevelPoint c = s.At;
                    TryWear(layout, room, mr, WearKind.Wax, () =>
                    {
                        double a = Unit(rng) * Math.PI * 2, r = Between(rng, 0.25, 0.6);
                        return new LevelPoint(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
                    }, mv, rng);
                }
            }
            foreach (LevelProp p in room.Props.Where(p => p.Model == PropModel.Coffin || p.Model == PropModel.Sarcophagus).ToList())
            {
                LevelPoint c = p.Position;
                double half = p.Depth * 0.5 + 0.5;
                TryWear(layout, room, mr, WearKind.Stain, () =>
                {
                    double a = (p.Rotation + (rng.Below(2) == 0 ? 0 : 180)) * Math.PI / 180.0; // beside it, along its own z
                    return new LevelPoint(c.X + Math.Sin(a) * half, c.Y + Math.Cos(a) * half);
                }, mv, rng);
            }
            // The ossuary's banked piles: bones and a skull at the feet of each.
            if (room.Theme == RoomTheme.Ossuary)
                foreach (LevelProp p in room.Props.Where(p => p.Model == PropModel.SkullPile || p.Model == PropModel.BonePile).ToList())
                    for (int k = 0; k < 3; k++)
                    {
                        LevelPoint c = p.Position;
                        double r = p.Radius + 0.3;
                        TryWear(layout, room, mr, k == 2 ? WearKind.Skull : WearKind.Bone, () =>
                        {
                            double a = Unit(rng) * Math.PI * 2;
                            return new LevelPoint(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
                        }, mv, rng);
                    }
        }

        static bool AlongX(int side) => side == LevelGenerator.North || side == LevelGenerator.South;

        static LevelPoint OnWall(int side, double along, double depth, double x0, double x1, double y0, double y1)
        {
            switch (side)
            {
                case LevelGenerator.North: return new LevelPoint(along, y1 - depth);
                case LevelGenerator.South: return new LevelPoint(along, y0 + depth);
                case LevelGenerator.East: return new LevelPoint(x1 - depth, along);
                default: return new LevelPoint(x0 + depth, along);
            }
        }

        // ---------------- The eye-catchers ----------------

        /// <summary>A prop's own x and z on the ground (x east, y north), as its footprint turns them.</summary>
        static void Axes(int rotation, out LevelPoint ux, out LevelPoint uz)
        {
            double a = rotation * Math.PI / 180.0;
            ux = new LevelPoint(Math.Cos(a), -Math.Sin(a));
            uz = new LevelPoint(Math.Sin(a), Math.Cos(a));
        }

        static LevelPoint Off(LevelPoint c, LevelPoint ux, double x, LevelPoint uz, double z) =>
            new LevelPoint(c.X + ux.X * x + uz.X * z, c.Y + ux.Y * x + uz.Y * z);

        static void AddCandles(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelPoint at, LevelRandom rng, double height = 0)
        {
            uint variant = (uint)rng.NextBits();
            if (height <= 0 && !InsideClear(layout, room, at, 0.2)) return;
            if (RoomShapes.DistanceToWalls(room, at, layout.CellSize) < 0.35) return; // work order 051: to the outline
            foreach (LevelDoor d in layout.DoorsOf(room.Order))
                if (LevelGeometry.DistanceToDoor(at, d, layout.CellSize) < 1.5) return;
            foreach (CompositionPiece c in mr.Pieces)
                if (c.Kind == CompositionKind.Candles && c.At.DistanceTo(at) < 0.5) return;
            mr.Pieces.Add(new CompositionPiece { Room = room.Order, Kind = CompositionKind.Candles, At = at, Height = height, Variant = variant });
            mr.Sources.Add(new MoodSource { Room = room.Order, Kind = MoodSourceKind.Candle, At = at, Height = height + 0.3, Flame = true });
        }

        /// <summary>
        /// A piece of an eye-catcher reaches a carved cell of its room (work order 051): a dais by its rectangle or circle, the
        /// others by their reach round their middle. The carpet's runs are checked as they are laid.
        /// </summary>
        public static bool OnCarved(LevelRoom room, CompositionPiece c, double cs)
        {
            if (room.SolidCount == 0 || c.Kind == CompositionKind.Carpet) return false;
            if (c.Kind == CompositionKind.Dais)
            {
                var f = new PropFootprint { Shape = PropShape.Rectangle, Centre = c.At, Rotation = c.Turn, HalfWidth = c.Width * 0.5, HalfDepth = c.Depth * 0.5 };
                return RoomShapes.DistanceToSolid(room, f, cs) < 0.05;
            }
            return RoomShapes.DistanceToSolid(room, c.At, cs) < c.Reach + 0.05;
        }

        /// <summary>
        /// The throne room's carpet in a carved room when a carved cell stands between its door and its dais (work order 051):
        /// in through the gate to the middle of the cell inside the door, along the middles of the open cells by the fewest
        /// cells to the dais's cell, and on to the dais's front; a run for each straight stretch. Nothing when no way is found.
        /// </summary>
        static void CarpetByTheCells(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelDoor entry, LevelPoint door, LevelPoint daisFront, MoodValues mv, LevelRandom rng)
        {
            double cs = layout.CellSize;
            if (!RoomShapes.InnerCell(room, entry, entry.From, out int sx, out int sy, out int ix, out int iy)) return;
            int tx = (int)Math.Floor(daisFront.X / cs), ty = (int)Math.Floor(daisFront.Y / cs);
            if (!room.IsOpen(tx, ty)) return;
            var before = new Dictionary<(int, int), (int, int)> { [(sx, sy)] = (sx, sy) };
            var queue = new Queue<(int, int)>();
            queue.Enqueue((sx, sy));
            while (queue.Count > 0 && !before.ContainsKey((tx, ty)))
            {
                (int x, int y) = queue.Dequeue();
                foreach ((int ddx, int ddy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (room.IsOpen(x + ddx, y + ddy) && !before.ContainsKey((x + ddx, y + ddy)))
                    {
                        before[(x + ddx, y + ddy)] = (x, y);
                        queue.Enqueue((x + ddx, y + ddy));
                    }
            }
            if (!before.ContainsKey((tx, ty))) return;
            var cells = new List<(int x, int y)>();
            for ((int, int) c = (tx, ty); ; c = before[c])
            {
                cells.Insert(0, c);
                if (c == (sx, sy)) break;
            }
            LevelPoint Mid((int x, int y) c) => new LevelPoint((c.x + 0.5) * cs, (c.y + 0.5) * cs);
            var points = new List<LevelPoint> { new LevelPoint(door.X + ix * 0.5, door.Y + iy * 0.5) };
            for (int k = 0; k < cells.Count; k++)
            {
                bool turn = k == 0 || k == cells.Count - 1
                            || (cells[k].x - cells[k - 1].x != cells[k + 1].x - cells[k].x) || (cells[k].y - cells[k - 1].y != cells[k + 1].y - cells[k].y);
                if (turn) points.Add(Mid(cells[k]));
            }
            points.Add(daisFront);
            for (int k = 1; k < points.Count; k++)
            {
                if (points[k - 1].DistanceTo(points[k]) < 1e-6) continue;
                uint variant = (uint)rng.NextBits();
                mr.Pieces.Add(new CompositionPiece { Room = room.Order, Kind = CompositionKind.Carpet, Width = mv.CarpetWidth, Variant = variant, At = points[k - 1], To = points[k] });
            }
        }

        /// <summary>A run of the carpet lies on a carved cell of its room (work order 051): nearer one than half its width.</summary>
        public static bool RunOnCarved(LevelRoom room, CompositionPiece run, double cs)
        {
            foreach ((int x, int y) in room.SolidList)
            {
                var cell = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5 };
                if (cell.DistanceTo(run.At, run.To) < run.Width * 0.5) return true;
            }
            return false;
        }

        /// <summary>A run of the throne room's carpet in a carved room (work order 051): only when it is longer than a step and lies on no carved cell.</summary>
        static bool AddCarpet(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelPoint from, LevelPoint to, MoodValues mv, LevelRandom rng)
        {
            uint variant = (uint)rng.NextBits();
            if (from.DistanceTo(to) < 1) return false;
            double cs = layout.CellSize, half = mv.CarpetWidth * 0.5;
            foreach ((int x, int y) in room.SolidList)
            {
                var cell = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5 };
                if (cell.DistanceTo(from, to) < half + 0.05) return false;
            }
            mr.Pieces.Add(new CompositionPiece { Room = room.Order, Kind = CompositionKind.Carpet, Width = mv.CarpetWidth, Variant = variant, At = from, To = to });
            return true;
        }

        static void Compose(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelValues values, LevelRandom rng)
        {
            MoodValues mv = values.Mood ?? new MoodValues();
            double m = Math.Max(0, mv.DaisMargin);
            switch (room.Theme)
            {
                case RoomTheme.ThroneRoom:
                {
                    LevelProp throne = room.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
                    if (throne == null) break;
                    mr.FarSide = throne.Footprint.NearestSide(room.Rect, layout.CellSize, out _);
                    Axes(throne.Rotation, out LevelPoint ux, out LevelPoint uz);
                    LevelPoint front = throne.Front;
                    // Its back stays at the wall's face; it reaches forward and to the sides.
                    var dais = new CompositionPiece
                    {
                        Room = room.Order, Kind = CompositionKind.Dais, Turn = throne.Rotation, Width = throne.Width + 2 * m, Depth = throne.Depth + m,
                        At = new LevelPoint(throne.Position.X + front.X * m * 0.5, throne.Position.Y + front.Y * m * 0.5), Variant = (uint)rng.NextBits(),
                    };
                    mr.Pieces.Add(dais);
                    LevelPoint daisFront = new LevelPoint(throne.Position.X + front.X * (throne.Depth * 0.5 + m), throne.Position.Y + front.Y * (throne.Depth * 0.5 + m));
                    LevelDoor entry = LevelGeometry.EntryDoor(layout, room);
                    if (entry != null)
                    {
                        entry.Opening(layout.CellSize, out LevelPoint a, out LevelPoint b);
                        LevelPoint door = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                        double dx = daisFront.X - door.X, dy = daisFront.Y - door.Y, len = Math.Sqrt(dx * dx + dy * dy);
                        if (room.SolidCount > 0)
                        {
                            // Work order 051: straight from the door to the dais when no carved cell is in the way; else in
                            // through the gate and on along the middles of the open cells (2 m from every carved cell), turning
                            // where the way turns, to the dais.
                            var straight = new LevelPoint(door.X + dx / len * 0.5, door.Y + dy / len * 0.5);
                            if (len <= 2 || !AddCarpet(layout, room, mr, straight, daisFront, mv, rng)) CarpetByTheCells(layout, room, mr, entry, door, daisFront, mv, rng);
                        }
                        else if (len > 2)
                            mr.Pieces.Add(new CompositionPiece
                            {
                                Room = room.Order, Kind = CompositionKind.Carpet, Width = mv.CarpetWidth, Variant = (uint)rng.NextBits(),
                                At = new LevelPoint(door.X + dx / len * 0.5, door.Y + dy / len * 0.5), To = daisFront,
                            });
                    }
                    // Candles on the dais's front corners.
                    double side = throne.Width * 0.5 + m * 0.5, ahead = throne.Depth * 0.5 + m * 0.5;
                    foreach (int s in new[] { -1, 1 })
                        AddCandles(layout, room, mr, Off(throne.Position, ux, side * s, front, ahead), rng, mv.DaisHeight * 0.5);
                    break;
                }
                case RoomTheme.TombHall:
                    foreach (LevelProp p in room.Props.Where(p => p.Model == PropModel.Coffin || p.Model == PropModel.Sarcophagus).ToList())
                    {
                        Axes(p.Rotation, out LevelPoint ux, out LevelPoint uz);
                        foreach (int s in new[] { -1, 1 }) AddCandles(layout, room, mr, Off(p.Position, ux, s * (p.Width * 0.5 + 0.3), uz, 0), rng);
                    }
                    break;
                case RoomTheme.Vault:
                case RoomTheme.Archive when room.Kind == RoomKind.Treasure:
                {
                    LevelProp chest = room.Props.FirstOrDefault(p => p.Model == PropModel.Chest);
                    if (chest == null) break;
                    mr.Pieces.Add(new CompositionPiece
                    {
                        Room = room.Order, Kind = CompositionKind.Dais, Turn = chest.Rotation, Width = chest.Width + 2 * m, Depth = chest.Depth + 2 * m,
                        At = chest.Position, Height = chest.Height, Variant = (uint)rng.NextBits(),
                    });
                    if (room.Theme != RoomTheme.Vault) break;
                    Axes(chest.Rotation, out LevelPoint ux, out LevelPoint uz);
                    // Coin heaps on the dais behind and beside the chest, and coins spilled round it.
                    var heaps = new[] { (-0.5, -0.5), (0.5, -0.5), (-0.5, 0.15), (0.5, 0.2) };
                    foreach ((double hx, double hz) in heaps)
                        mr.Pieces.Add(new CompositionPiece
                        {
                            Room = room.Order, Kind = CompositionKind.CoinHeap, Width = Between(rng, 0.16, 0.24), Depth = Between(rng, 0.1, 0.18),
                            At = Off(chest.Position, ux, hx * (chest.Width + m), uz, hz * (chest.Depth + m)), Variant = (uint)rng.NextBits(),
                        });
                    mr.Pieces.Add(new CompositionPiece
                    {
                        Room = room.Order, Kind = CompositionKind.Coins, At = chest.Position, Width = 0.5 * Math.Sqrt((chest.Width + 2 * m) * (chest.Width + 2 * m) + (chest.Depth + 2 * m) * (chest.Depth + 2 * m)) + 0.5,
                        Variant = (uint)rng.NextBits(),
                    });
                    break;
                }
                case RoomTheme.Shrine:
                {
                    LevelProp basin = room.Props.FirstOrDefault(p => p.Model == PropModel.Basin);
                    if (basin != null)
                    {
                        mr.Pieces.Add(new CompositionPiece { Room = room.Order, Kind = CompositionKind.RoundDais, At = basin.Position, Width = basin.Radius + m, Variant = (uint)rng.NextBits() });
                        for (int k = 0; k < 4; k++)
                        {
                            double a = (45 + 90 * k) * Math.PI / 180.0, r = basin.Radius + m + 0.25;
                            AddCandles(layout, room, mr, new LevelPoint(basin.Position.X + Math.Sin(a) * r, basin.Position.Y + Math.Cos(a) * r), rng);
                        }
                    }
                    LevelProp altar = room.Props.FirstOrDefault(p => p.Model == PropModel.Altar);
                    if (altar != null)
                    {
                        Axes(altar.Rotation, out LevelPoint ux, out LevelPoint uz);
                        foreach (int s in new[] { -1, 1 })
                            foreach (double z in new[] { -0.35, 0.35 })
                                AddCandles(layout, room, mr, Off(altar.Position, ux, s * (altar.Width * 0.5 + 0.28), uz, z), rng);
                    }
                    break;
                }
                case RoomTheme.Archive:
                    foreach (LevelProp p in room.Props.Where(p => p.Model == PropModel.Bookshelf).ToList())
                    {
                        Axes(p.Rotation, out LevelPoint ux, out LevelPoint uz);
                        foreach (int s in new[] { -1, 1 }) AddCandles(layout, room, mr, Off(p.Position, ux, s * (p.Width * 0.5 + 0.28), uz, 0), rng);
                    }
                    break;
            }
        }

        /// <summary>
        /// The empty place for a large prop to come: along the far wall (the throne's wall in the throne room), at a statue's
        /// depth, from its middle (the throne's place) outward every half metre, the first place where the game's statue,
        /// facing into the room, keeps every rule of the props and the clear lane with the room's props as they are, and
        /// stands clear of the eye-catcher's pieces; when the far wall has none (a door in its middle, a treasure room's or a
        /// room of the row's way on, or shelves along it), the two side walls the same way, the one farther from the door
        /// in first. <paramref name="side"/> is the wall it stands at. Null when there is none, and in the entrance.
        /// </summary>
        public static LevelPoint? LargePlace(LevelLayout layout, LevelRoom room, MoodRoom mr, LevelValues values, out int turn, out int side)
        {
            turn = 0;
            side = mr.FarSide;
            if (room.Kind == RoomKind.Entrance) return null;
            PropValues pv = values.PropRules ?? new PropValues();
            var lane = new PropRules.Lane(layout, room, pv);
            foreach (LevelProp p in room.Props) lane.Block(p.Footprint);
            LevelProp throne = room.Props.FirstOrDefault(p => p.Model == PropModel.BoneThrone);
            var walls = new List<int> { mr.FarSide };
            int a = (mr.FarSide + 1) % 4, b = (mr.FarSide + 3) % 4;
            LevelDoor entry = LevelGeometry.EntryDoor(layout, room);
            if (entry != null)
            {
                entry.Opening(layout.CellSize, out LevelPoint e0, out LevelPoint e1);
                var door = new LevelPoint((e0.X + e1.X) * 0.5, (e0.Y + e1.Y) * 0.5);
                double cs = layout.CellSize;
                double Dist(int w) => w == LevelGenerator.North ? room.Rect.Top * cs - door.Y : w == LevelGenerator.South ? door.Y - room.Rect.Y * cs
                                      : w == LevelGenerator.East ? room.Rect.Right * cs - door.X : door.X - room.Rect.X * cs;
                if (Dist(b) > Dist(a)) { int t = a; a = b; b = t; }
            }
            walls.Add(a);
            walls.Add(b);
            foreach (int w in walls)
            {
                double? from = room.Theme == RoomTheme.ThroneRoom && throne != null && w == mr.FarSide ? (AlongX(w) ? throne.Position.X : throne.Position.Y) : (double?)null;
                LevelPoint? at = AlongWall(layout, room, mr, pv, lane, w, from, out turn);
                if (!at.HasValue) continue;
                side = w;
                return at;
            }
            return null;
        }

        static LevelPoint? AlongWall(LevelLayout layout, LevelRoom room, MoodRoom mr, PropValues pv, PropRules.Lane lane, int side, double? start, out int turn)
        {
            double cs = layout.CellSize, x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            PropSize statue = PropSize.Of(PropModel.Statue);
            LevelPoint into = PropRules.Inward(side);
            turn = (PropRules.Facing(into.X, into.Y) + statue.FrontTurn) % 360;
            double depth = pv.WallClearance + Math.Max(statue.Width, statue.Depth) * 0.5 + 0.1;
            double lo = AlongX(side) ? x0 : y0, hi = AlongX(side) ? x1 : y1, from = start ?? (lo + hi) * 0.5;
            for (int s = 0; ; s++)
            {
                bool any = false;
                for (int k = 0; k < (s == 0 ? 1 : 2); k++)
                {
                    double along = from + (k == 0 ? s : -s) * 0.5;
                    if (along <= lo || along >= hi) continue;
                    any = true;
                    LevelPoint at = OnWall(side, along, depth, x0, x1, y0, y1);
                    LevelProp p = LevelProp.Make(PropModel.Statue, at, turn, PropPlace.Set);
                    if (PropRules.Fault(layout, room, p, room.Props, pv) != null) continue;
                    if (!lane.Clear(p.Footprint)) continue;
                    bool clear = true;
                    foreach (CompositionPiece c in mr.Pieces)
                    {
                        double gap = c.Kind == CompositionKind.Carpet
                            ? LevelGeometry.DistanceToSegment(at, c.At, c.To) - c.Width * 0.5
                            : at.DistanceTo(c.At) - c.Reach;
                        if (gap < 0.9) { clear = false; break; }
                    }
                    if (clear) return at;
                }
                if (!any) return null;
            }
        }

        // ---------------- Words ----------------

        /// <summary>Everything as text: two plans with the same text put the same mood in the same places.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            string N(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
            foreach (MoodRoom r in Rooms)
            {
                sb.Append(r.Room.Letter).Append(r.Room.Order).Append(" far ").Append(r.FarSide);
                if (r.LargePropPlace.HasValue) sb.Append(" large ").Append(N(r.LargePropPlace.Value.X)).Append(',').Append(N(r.LargePropPlace.Value.Y)).Append(" turned ").Append(r.LargePropTurn);
                sb.Append('\n');
                foreach (MoodSource s in r.Sources) sb.Append("  source ").Append(s.Kind).Append(' ').Append(s.At).Append(s.Flame ? " flame" : "").Append('\n');
                foreach (CompositionPiece c in r.Pieces) sb.Append("  piece ").Append(c.Kind).Append(' ').Append(c.At).Append(' ').Append(N(c.Width)).Append('x').Append(N(c.Depth)).Append(" turned ").Append(c.Turn).Append('\n');
                foreach (WearPiece w in r.Wear) sb.Append("  wear ").Append(w.Kind).Append(' ').Append(w.At).Append(' ').Append(N(w.Size)).Append('\n');
                foreach (LevelProp p in r.LargeProps) sb.Append("  large ").Append(p).Append('\n'); // work order 052
                foreach (LevelProp p in r.Replaced) sb.Append("  replaced ").Append(p).Append('\n');
                foreach (LevelProp p in r.Raised) sb.Append("  raised ").Append(p).Append('\n');
            }
            return sb.ToString();
        }

        public int WearCount => Rooms.Sum(r => r.Wear.Count);
        public int SourceCount => Rooms.Sum(r => r.Sources.Count);

        // ---------------- The room tone ----------------

        /// <summary>
        /// The room tone (work order 044, a placeholder until Kristian records his own): seven seconds that loop, a dull air of
        /// low-passed noise that swells and sinks once a loop, and two drips. Quiet: it peaks at half of full scale before the
        /// tone's volume (<see cref="MoodValues.ToneVolume"/>) and the sounds' master take it down.
        /// </summary>
        public static SynthRecipe RoomTone() => new SynthRecipe
        {
            Seconds = 7f, Peak = 0.5f, Seed = 44,
            Layers = new[]
            {
                new SynthLayer { Wave = SynthWave.Noise, Length = 7f, Gain = 1f, LowPass = 240f, Attack = 0.0005f, Release = 0.0005f, Tremolo = 1f / 7f, TremoloDepth = 0.35f },
                new SynthLayer { Wave = SynthWave.Noise, Length = 7f, Gain = 1.2f, LowPass = 80f, Attack = 0.0005f, Release = 0.0005f },
                new SynthLayer { Start = 2.1f, Length = 0.12f, From = 1500f, To = 700f, Gain = 0.3f, Attack = 0.002f, Decay = 0.025f, Release = 0.02f },
                new SynthLayer { Start = 2.27f, Length = 0.1f, From = 1300f, To = 650f, Gain = 0.08f, Attack = 0.002f, Decay = 0.03f, Release = 0.02f },
                new SynthLayer { Start = 5.35f, Length = 0.12f, From = 1150f, To = 600f, Gain = 0.22f, Attack = 0.002f, Decay = 0.025f, Release = 0.02f },
            },
        };
    }
}
