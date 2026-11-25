using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Bones that fly (work order 122, a look order). Kristian, 2026-11-25, after two runs: "jeg har lyst at fiendene mister
    /// skjellet deler når de blir skutt på og at bein flyr rund etter slik fysikkens lover tilsier at det skal være. Gjerne knuse i
    /// biter." A skeleton loses a part as its health falls (the part is gone from its body and a loose piece of it flies off,
    /// thrown along her shot), and breaks into its pieces when it dies; the pieces are rigid bodies that fall, tumble, bounce and
    /// come to rest on the floor, the walls and the props, and a piece that lands hard enough breaks once more in two. Nothing in
    /// the fight changes: the pieces touch no one and block nothing. Every value is a first guess he tunes by playing.
    /// </summary>
    [Serializable]
    public sealed class BoneBreakValues
    {
        /// <summary>
        /// The order's switch (work order 122: "jeg har lyst at fiendene mister skjellet deler når de blir skutt på ... Gjerne knuse
        /// i biter"). On by default. Off, an enemy is hit and dies exactly as before the order: no part comes off, its death plays
        /// and it lies there as before, and no piece is made. Not kept between starts: every Play starts on.
        /// </summary>
        public static bool On = true;

        /// <summary>The values the game breaks bones with (one set, read at every hit and every frame of a piece).</summary>
        public static readonly BoneBreakValues Live = new BoneBreakValues();

        // ---------------- When a part comes off (the order's point 1) ----------------

        /// <summary>A part comes off for each this share of its health lost: a third, so the first at two thirds left, the next at one third.</summary>
        public double PartShare = 1.0 / 3.0;
        /// <summary>
        /// Parts it loses before it dies, at most: two (its skull, then the shin and foot on the side she shoots from), so it is visibly
        /// coming apart near its death and still has its body, its arms and what they carry to read until it dies. Its arms never go
        /// before its death: on every kind they carry its weapon or its wind-up (the order's point 1).
        /// </summary>
        public int PartsBeforeDeath = 2;

        // ---------------- How hard (the order's points 1 and 2) ----------------

        /// <summary>m/s a piece is thrown along her shot by a hit of no damage: the floor of the throw.</summary>
        public double ThrowBase = 2.5;
        /// <summary>m/s more for each point of the hit's damage: her basic shot (40) throws at 3.1 m/s, a 400 hit at 8.5.</summary>
        public double ThrowPerDamage = 0.015;
        /// <summary>m/s a piece is thrown at most, however hard the hit.</summary>
        public double ThrowMost = 10;
        /// <summary>Share of the throw each piece may be slower or faster by, at random, so they do not fly as one.</summary>
        public double ThrowSpread = 0.35;
        /// <summary>Share of the throw that goes upward, and m/s upward on top of it: a piece leaves in an arc, not along the floor.</summary>
        public double ThrowUp = 0.45;
        public double UpBase = 1.2;
        /// <summary>m/s each piece of a death also leaves the middle of the body by, so the body bursts apart as well as away.</summary>
        public double DeathScatter = 1.6;
        /// <summary>rad/s a piece tumbles at most as it leaves (each a share of it at random, about its own random axis).</summary>
        public double MostSpin = 14;

        // ---------------- How many and how long (the order's point 5) ----------------

        /// <summary>Pieces alive at once at most; when one more is needed the oldest is taken (and its old piece is gone at once).</summary>
        public int MostPieces = 150;
        /// <summary>s a piece lies still on the floor before it sinks away.</summary>
        public double LieSeconds = 5;
        /// <summary>s it takes to sink out of sight, and how deep it sinks: its own height times this, at least <see cref="SinkLeast"/> m.</summary>
        public double SinkSeconds = 1.2;
        public double SinkDepthShare = 1.2;
        public double SinkLeast = 0.25;
        /// <summary>s a piece may fly or roll before it sinks away anyway (one caught moving on a slope or in a corner).</summary>
        public double MostFlySeconds = 8;
        /// <summary>A piece under this speed (m/s) and this spin (rad/s) for <see cref="RestHold"/> s has come to rest.</summary>
        public double RestSpeed = 0.2;
        public double RestSpin = 0.8;
        public double RestHold = 0.25;
        /// <summary>m under the floor at which a piece that fell out of the world is gone at once.</summary>
        public double FallOutBelow = -8;

        // ---------------- Breaking again (the order's point 2: "gjerne knuse i biter") ----------------

        /// <summary>m/s of impact at which a whole piece breaks once more, in two; a half never breaks again.</summary>
        public double ShatterSpeed = 5.5;
        /// <summary>m/s the two halves part by along the cut.</summary>
        public double ShatterApart = 0.9;
        /// <summary>A piece breaks again only if it is at least this long (m, its longest side as made) and has this many triangles.</summary>
        public double ShatterLeastSize = 0.12;
        public int ShatterLeastTriangles = 24;

        // ---------------- Bone on stone (the order's point 3) ----------------

        /// <summary>kg/m³ of bone, and the share of a piece's box that is bone: a piece's mass is its box times both.</summary>
        public double BoneDensity = 1900;
        public double FillShare = 0.3;
        /// <summary>kg a piece weighs at least and at most.</summary>
        public double MassLeast = 0.03;
        public double MassMost = 25;
        /// <summary>Bone on stone: rubs and stops rather than slides, and bounces a little.</summary>
        public double DynamicFriction = 0.5;
        public double StaticFriction = 0.65;
        public double Bounce = 0.3;
        /// <summary>The air's slowing of a piece as it flies and of its tumble.</summary>
        public double Drag = 0.05;
        public double AngularDrag = 0.4;
    }

    /// <summary>The pieces a skeleton breaks into (work order 122): one for a group of its 24 bones.</summary>
    public enum BonePart
    {
        Pelvis,
        Belly,
        Chest,
        Skull,
        UpperArmLeft,
        UpperArmRight,
        ForearmLeft,
        ForearmRight,
        ThighLeft,
        ThighRight,
        ShinLeft,
        ShinRight,
    }

    /// <summary>
    /// Which piece each bone of an enemy's skeleton belongs to (work order 122), by the 24 bones the models have
    /// (<c>EnemyModelImportTests</c>): the hips the pelvis; the spine and the first spine bone the belly; the upper spine, the neck
    /// and the shoulder blades the chest; the head and its two end bones the skull; each upper arm; each forearm with its hand;
    /// each thigh; each shin with its foot and toes. A bone of another name belongs to its parent's piece (the code walks up).
    /// </summary>
    public static class BoneParts
    {
        public const int Count = 12;

        /// <summary>The piece of a bone by its name (the whole name, or the part after the last '|', ':' or '/'), or -1 when it is none of the 24.</summary>
        public static int Of(string bone)
        {
            if (string.IsNullOrEmpty(bone)) return -1;
            int cut = bone.LastIndexOfAny(new[] { '|', ':', '/' });
            string n = (cut < 0 ? bone : bone.Substring(cut + 1)).ToLowerInvariant();
            switch (n)
            {
                case "hips": return (int)BonePart.Pelvis;
                case "spine": case "spine01": return (int)BonePart.Belly;
                case "spine02": case "neck": case "leftshoulder": case "rightshoulder": return (int)BonePart.Chest;
                case "head": case "head_end": case "headfront": return (int)BonePart.Skull;
                case "leftarm": return (int)BonePart.UpperArmLeft;
                case "rightarm": return (int)BonePart.UpperArmRight;
                case "leftforearm": case "lefthand": return (int)BonePart.ForearmLeft;
                case "rightforearm": case "righthand": return (int)BonePart.ForearmRight;
                case "leftupleg": return (int)BonePart.ThighLeft;
                case "rightupleg": return (int)BonePart.ThighRight;
                case "leftleg": case "leftfoot": case "lefttoebase": return (int)BonePart.ShinLeft;
                case "rightleg": case "rightfoot": case "righttoebase": return (int)BonePart.ShinRight;
                default: return -1;
            }
        }

        /// <summary>
        /// The bone a part hangs from, nearest the hips: scaled to nothing, it takes the whole part off the body (its skinned mesh
        /// and everything under it), which is how a lost part is hidden.
        /// </summary>
        public static string RootBone(BonePart part)
        {
            switch (part)
            {
                case BonePart.Pelvis: return "Hips";
                case BonePart.Belly: return "Spine";
                case BonePart.Chest: return "Spine02";
                case BonePart.Skull: return "Head";
                case BonePart.UpperArmLeft: return "LeftArm";
                case BonePart.UpperArmRight: return "RightArm";
                case BonePart.ForearmLeft: return "LeftForeArm";
                case BonePart.ForearmRight: return "RightForeArm";
                case BonePart.ThighLeft: return "LeftUpLeg";
                case BonePart.ThighRight: return "RightUpLeg";
                case BonePart.ShinLeft: return "LeftLeg";
                default: return "RightLeg";
            }
        }
    }

    /// <summary>What a piece of bone is doing (work order 122).</summary>
    public enum BonePieceState
    {
        Free,
        Flying,
        Resting,
        Sinking,
    }

    /// <summary>The rules of bones that fly (work order 122), plain numbers for the code that shows them.</summary>
    public static class BoneBreakRules
    {
        /// <summary>
        /// Does this kind break? Every kind with a skeleton's body, the elites too: the skeleton warrior (the brute and the lancer),
        /// the archer (the shooter), the bone brute (the guard), the hooded skeleton (the caster and the warder) and the bony ghoul
        /// (the swarmer and the bomber). Not the act boss: an armoured knight, not a skeleton's body, whose death is the floor's
        /// finale with its own pictures (work order 106).
        /// </summary>
        public static bool Breaks(EnemyKind kind) => kind != EnemyKind.Boss;

        /// <summary>
        /// Does its body wait for its blast to break? The bomber's: dead, its body still swells and flashes as its fuse burns (the
        /// fuse is its blast's wind-up, read on its body), and it breaks as it blows.
        /// </summary>
        public static bool WaitsForItsBlast(EnemyKind kind) => kind == EnemyKind.Bomber;

        /// <summary>
        /// How many parts it should have lost with <paramref name="health"/> of <paramref name="max"/> left: one for each
        /// <see cref="BoneBreakValues.PartShare"/> lost, at most <see cref="BoneBreakValues.PartsBeforeDeath"/>. None at full health or
        /// without a maximum; at none left it is dead, and the death breaks the rest.
        /// </summary>
        public static int PartsDue(int health, int max, BoneBreakValues v)
        {
            if (v == null || max <= 0 || v.PartShare <= 0) return 0;
            double lost = 1.0 - Math.Max(0, Math.Min(health, max)) / (double)max;
            int n = (int)Math.Floor(lost / v.PartShare + 1e-9);
            return Math.Max(0, Math.Min(n, Math.Max(0, v.PartsBeforeDeath)));
        }

        /// <summary>
        /// The part it loses as its <paramref name="index"/>th (from 0): its skull first, then the shin and foot on the side she
        /// shoots from (<paramref name="nearIsLeft"/>: its left side faces her), then the other shin. Arms never: they carry its
        /// weapon or its wind-up on every kind.
        /// </summary>
        public static BonePart AlivePart(int index, bool nearIsLeft)
        {
            if (index <= 0) return BonePart.Skull;
            if (index == 1) return nearIsLeft ? BonePart.ShinLeft : BonePart.ShinRight;
            return nearIsLeft ? BonePart.ShinRight : BonePart.ShinLeft;
        }

        /// <summary>m/s a hit of <paramref name="damage"/> throws its pieces at: <see cref="BoneBreakValues.ThrowBase"/> and more for each point, at most <see cref="BoneBreakValues.ThrowMost"/>.</summary>
        public static double ThrowSpeed(int damage, BoneBreakValues v)
        {
            if (v == null) return 0;
            double s = v.ThrowBase + v.ThrowPerDamage * Math.Max(0, damage);
            return Math.Max(0, Math.Min(s, v.ThrowMost));
        }

        /// <summary>kg of a piece whose box (as shown) is these metres: bone's density times the share of the box that is bone, within bounds.</summary>
        public static double Mass(double x, double y, double z, BoneBreakValues v)
        {
            double m = Math.Abs(x * y * z) * v.BoneDensity * v.FillShare;
            return Math.Max(v.MassLeast, Math.Min(m, v.MassMost));
        }

        /// <summary>Does a piece break again on an impact of <paramref name="impact"/> m/s? Only a whole piece that can be cut in two, once, hard enough.</summary>
        public static bool Shatters(bool whole, bool hasHalves, double impact, BoneBreakValues v) =>
            whole && hasHalves && v != null && impact >= v.ShatterSpeed;

        /// <summary>Can a piece of this longest side (m, as made) and this many triangles be cut in two?</summary>
        public static bool CanBeCut(double longest, int triangles, BoneBreakValues v) =>
            v != null && longest >= v.ShatterLeastSize && triangles >= Math.Max(2, v.ShatterLeastTriangles);

        /// <summary>
        /// What a piece does next: one flying is gone at once when it fell out of the world, rests once it has been still for
        /// <see cref="BoneBreakValues.RestHold"/> s, and sinks anyway after <see cref="BoneBreakValues.MostFlySeconds"/>; one resting sinks
        /// after <see cref="BoneBreakValues.LieSeconds"/>; one sinking is gone after <see cref="BoneBreakValues.SinkSeconds"/>.
        /// </summary>
        public static BonePieceState Next(BonePieceState state, double age, double stillFor, double sinceState, double y, BoneBreakValues v)
        {
            switch (state)
            {
                case BonePieceState.Flying:
                    if (y < v.FallOutBelow) return BonePieceState.Free;
                    if (stillFor >= v.RestHold) return BonePieceState.Resting;
                    if (age >= v.MostFlySeconds) return BonePieceState.Sinking;
                    return state;
                case BonePieceState.Resting:
                    return sinceState >= v.LieSeconds ? BonePieceState.Sinking : state;
                case BonePieceState.Sinking:
                    return sinceState >= v.SinkSeconds ? BonePieceState.Free : state;
                default:
                    return state;
            }
        }

        /// <summary>Is a piece moving this slowly and turning this slowly still (m/s, rad/s)?</summary>
        public static bool Still(double speed, double spin, BoneBreakValues v) => speed < v.RestSpeed && spin < v.RestSpin;

        /// <summary>
        /// The slot a new piece takes, of the <paramref name="made"/> made so far: while fewer than
        /// <see cref="BoneBreakValues.MostPieces"/> live, the first free one, else a new one (<paramref name="made"/> itself); with as many
        /// living as the cap, the oldest (the earliest <paramref name="born"/>), whose piece is gone at once.
        /// </summary>
        public static int Slot(BonePieceState[] states, double[] born, int made, BoneBreakValues v)
        {
            int live = 0, free = -1, oldest = -1;
            for (int i = 0; i < made; i++)
            {
                if (states[i] == BonePieceState.Free)
                {
                    if (free < 0) free = i;
                    continue;
                }
                live++;
                if (oldest < 0 || born[i] < born[oldest]) oldest = i;
            }
            if (live >= Math.Max(1, v.MostPieces) && oldest >= 0) return oldest;
            return free >= 0 ? free : made;
        }
    }

    /// <summary>
    /// A small random stream of its own for the pieces' spread and tumble (work order 122), so that breaking bones takes nothing
    /// from any random stream of the game. The same seed gives the same numbers.
    /// </summary>
    public struct BoneBreakRandom
    {
        ulong _s;

        public BoneBreakRandom(ulong seed) => _s = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

        /// <summary>The next number from 0 up to 1.</summary>
        public double Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 7;
            _s ^= _s << 17;
            return (_s >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>The next number from -1 up to 1.</summary>
        public double Signed() => Next() * 2.0 - 1.0;
    }
}
