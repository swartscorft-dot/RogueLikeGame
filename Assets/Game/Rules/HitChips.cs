using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Chips, not flashes (work order 127, a look order). Kristian, 2026-11-25 at about 18:40, after playing the build with the toon
    /// look and the bones: "toon upgraden er virkelig inne på noe. jeg likte bein og lignende som flyr. dog, så kan vi drope at de
    /// flasher sånn når de blir truffet, heller at litt bein eller ligende spruter av. [...] du kan droppe den sirkelen når døde
    /// enemies despawner". An enemy that is hit no longer turns white (neither a plain hit's flash nor a crit's hard white frame);
    /// instead a small spray of chips leaves it from where the shot struck, thrown along the shot with some spread and some upward,
    /// falling, bouncing once and lying a moment on the floor before they shrink away. Bone on the skeletons, cloth and bone on the
    /// hooded ones, steel on the boss, the dummy's own stuff on the dummy. A crit reads by its spray alone: more chips, thrown
    /// harder, a little larger. And the pale ring drawn where a dead body lay when it is hidden is gone. Nothing in the fight
    /// changes: the chips touch no one. Every value is a first guess he tunes by playing; judged at OUR camera (55 degrees, 28 m),
    /// where a metre is about 72 pixels at 1920 by 1080.
    /// </summary>
    [Serializable]
    public sealed class HitChipsValues
    {
        /// <summary>
        /// The order's switch (Kristian, 2026-11-25: "dog, så kan vi drope at de flasher sånn når de blir truffet, heller at litt bein
        /// eller ligende spruter av. [...] du kan droppe den sirkelen når døde enemies despawner"). On by default. Off, an enemy (and
        /// the dummy) flashes when hit and the ring comes when a body is hidden exactly as before the order, and no chip is made.
        /// Not kept between starts: every Play starts on.
        /// </summary>
        public static bool On = true;

        /// <summary>
        /// With <see cref="On"/>: is the thin pale ring still drawn where a dead body lay when it is hidden? No ("du kan droppe den
        /// sirkelen når døde enemies despawner"). The boss's own ring at its death is not this ring and stays.
        /// </summary>
        public static bool RingWhenABodyGoes = false;

        /// <summary>The values the game throws chips with (one set, read at every hit and every frame of a chip).</summary>
        public static readonly HitChipsValues Live = new HitChipsValues();

        // ---------------- How many (the order's point 2) ----------------

        /// <summary>
        /// Chips a plain hit of no damage throws: the floor of a spray. Few (the producer's second round: "a basic shot throws about
        /// 3 or 4"; the first round's 6 a shot made a cloud of flakes behind a pack in the floor 5 fight).
        /// </summary>
        public int ChipsLeast = 3;
        /// <summary>Chips more for each point of the hit's damage: her basic shot (40) throws 3, an 80 hit 4, a 200 hit 5.</summary>
        public double ChipsPerDamage = 0.0125;
        /// <summary>Chips a plain hit throws at most, however hard.</summary>
        public int ChipsMost = 6;
        /// <summary>A crit's spray: the plain count times this, and this many more (a crit of about 80 throws 4 × 1.6 + 3 = 9; the hardest crit 6 × 1.6 + 3 = 12).</summary>
        public double CritCountScale = 1.6;
        public int CritExtra = 3;
        /// <summary>Chips more for each further hit on the same target in the same frame (a fan of pellets): the spray grows, it does not repeat.</summary>
        public int ChipsPerMoreHit = 1;
        /// <summary>Chips one target throws in one frame at most, all its hits together (a fan of pellets into one body).</summary>
        public int MostPerTarget = 14;
        /// <summary>
        /// Share of the spray thrown on a hit that also takes a part off a skeleton (work order 122): its part flying off is that
        /// hit's sign, the chips under it are half as many so the two do not double up into noise. A killing hit on a body that
        /// breaks into its pieces throws none (its own pieces and the death's shards are the sign).
        /// </summary>
        public double PartOffShare = 0.5;

        // ---------------- How hard (the order's point 2) ----------------

        /// <summary>
        /// m/s a chip leaves along the shot after a hit of no damage. Slow, so that the spray stays on its enemy: a plain hit's chips
        /// land within about 0.6 m of the body, a crit's within about 1 m (the producer's second round; at 1.6 m/s the first round's
        /// lay a metre or two behind a pack, "litter from her gun", and at 2.6 m/s a crit's left a 5 m crop in 0.7 s).
        /// </summary>
        public double SpeedBase = 0.7;
        /// <summary>m/s more for each point of damage: her basic shot (40) throws at 0.86 m/s.</summary>
        public double SpeedPerDamage = 0.004;
        /// <summary>m/s a chip leaves at most along the shot, a crit's included.</summary>
        public double SpeedMost = 2.2;
        /// <summary>A crit throws this many times harder.</summary>
        public double CritSpeedScale = 1.35;
        /// <summary>Share of the speed each chip may be slower or faster by, at random.</summary>
        public double SpeedJitter = 0.3;
        /// <summary>Share of the speed a chip may go to either side of the shot, at random: the spray's fan.</summary>
        public double Spread = 0.6;
        /// <summary>
        /// m/s upward each chip leaves with, and the share of its speed that goes upward on top of it, at random from 0.5 to 1 of it:
        /// they pop up over the body's outline before they fall, where the camera sees them.
        /// </summary>
        public double UpBase = 1.6;
        public double UpShare = 0.3;
        /// <summary>
        /// Share of its body's own speed along the floor a chip carries as it leaves, as anything knocked off a moving body does: so the
        /// spray of a swarmer running at her moves with it and lands where it is, not in a trail behind it (the second round's floor 5
        /// fight left a line of chips where a charging swarmer had been hit).
        /// </summary>
        public double BodySpeedKept = 1.0;
        /// <summary>m/s² a chip falls by: the world's own.</summary>
        public double Gravity = 9.81;
        /// <summary>rad/s a chip tumbles at most as it leaves (each a share of it at random, about its own random axis).</summary>
        public double MostSpin = 16;

        // ---------------- The floor (the order's point 4: one bounce, then rest) ----------------

        /// <summary>Share of its falling speed a chip keeps upward at its one bounce, and share of its speed along the floor it keeps.</summary>
        public double Bounce = 0.25;
        public double SlideKeep = 0.3;
        /// <summary>m/s of falling under which a chip that meets the floor does not bounce but lies at once.</summary>
        public double RestSpeed = 0.6;

        // ---------------- How long (the order's point 4) ----------------

        /// <summary>
        /// s a chip lies on the floor before it shrinks away, and s it takes to shrink to nothing. Short: a spray is the sign of one
        /// hit, not litter (lying 0.9 s, the first guess, her basic shots into a pack left a heap of forty chips in the floor 5
        /// fight, away from any body).
        /// </summary>
        public double LieSeconds = 0.35;
        public double ShrinkSeconds = 0.25;
        /// <summary>s a chip may fly before it shrinks away anyway (one thrown off a ledge: it never finds its floor).</summary>
        public double MostFlySeconds = 2.0;

        // ---------------- How big (the order's point 3) ----------------

        /// <summary>
        /// m a chip measures along its long side (a splinter's length; a knuckle is about 0.6 of it, a rib bit as long, a wedge 0.75):
        /// 0.26 m is about 19 pixels at our camera at 1920 by 1080 (a metre is about 72 pixels there), a splinter about 6 pixels thick
        /// (0.14 m, the first guess, measured 10 pixels and read as confetti; the first round's flat 0.2 m flakes read as paper);
        /// each chip this share larger or smaller at random.
        /// </summary>
        public double Size = 0.26;
        public double SizeJitter = 0.3;
        /// <summary>A crit's chips are this much larger.</summary>
        public double CritSizeScale = 1.15;
        /// <summary>
        /// Chips are as much larger as their enemy is shown taller than this (m: a skeleton's height; an elite, a lancer, the boss),
        /// never smaller, up to this many times.
        /// </summary>
        public double HeightForSize = 1.9;
        public double MostSizeScale = 1.6;

        // ---------------- How many at once (the order's point 4) ----------------

        /// <summary>Chips alive at once at most: when one more is needed the oldest is taken (and its old chip is gone at once).</summary>
        public int MostChips = 240;

        // ---------------- The colours (the order's point 2: "bein eller lignende") ----------------

        /// <summary>
        /// Bone, warm, and its shade: the skeleton warrior, the archer, the ghoul (a paler first guess, 0.95/0.90/0.76, lit to white
        /// and read as paper in the pictures).
        /// </summary>
        public Rgb Bone = new Rgb(0.90f, 0.82f, 0.64f);
        /// <summary>A chip's second tone, on its broken ends (<see cref="HitChipsRules.EndOf"/>): bone's darker inside.</summary>
        public Rgb BoneShade = new Rgb(0.62f, 0.52f, 0.37f);
        /// <summary>The bone brute's grey bone plates, and their broken ends.</summary>
        public Rgb GreyBone = new Rgb(0.82f, 0.80f, 0.75f);
        public Rgb GreyBoneShade = new Rgb(0.55f, 0.53f, 0.49f);
        /// <summary>The lancer's blood-red bone, light and dark.</summary>
        public Rgb RedBone = new Rgb(0.85f, 0.30f, 0.24f);
        public Rgb RedBoneShade = new Rgb(0.55f, 0.13f, 0.11f);
        /// <summary>
        /// The hooded skeleton's robe, as scraps of dusty cloth, and their darker torn edge: light enough to be seen on its dark robe
        /// and the crypt's dark floor (the first round's 0.46/0.33/0.26, then 0.64/0.50/0.40, were lost on both).
        /// </summary>
        public Rgb Robe = new Rgb(0.84f, 0.74f, 0.60f);
        public Rgb RobeShade = new Rgb(0.58f, 0.48f, 0.38f);
        /// <summary>The boss's armour: bright steel and its darker plate.</summary>
        public Rgb Steel = new Rgb(0.86f, 0.86f, 0.88f);
        public Rgb SteelShade = new Rgb(0.52f, 0.51f, 0.55f);
        /// <summary>Share of a hooded one's chips that are bone, not robe.</summary>
        public double SecondShare = 0.4;
    }

    /// <summary>What a body's chips are (work order 127): taken from what its body is.</summary>
    public enum ChipStuff
    {
        /// <summary>Bone: the skeleton warrior (the brute), the archer (the shooter), the bony ghoul (the swarmer and the bomber).</summary>
        Bone,
        /// <summary>The bone brute's (the guard's) grey bone plates.</summary>
        GreyBone,
        /// <summary>The lancer's blood-red bone.</summary>
        RedBone,
        /// <summary>The hooded skeleton (the caster and the warder): scraps of its robe, and bone.</summary>
        Robe,
        /// <summary>The act boss, an armoured knight: chips of its steel.</summary>
        Armour,
        /// <summary>The practice room's dummy: chips of its own material.</summary>
        Dummy,
    }

    /// <summary>The colours chips are made in (work order 127): one material each.</summary>
    public enum ChipColour
    {
        Bone,
        BoneShade,
        GreyBone,
        RedBone,
        RedBoneShade,
        Robe,
        Steel,
        SteelShade,
        GreyBoneShade, // the second round: every chip's broken ends in a second tone
        RobeShade,
    }

    /// <summary>What a chip is doing (work order 127).</summary>
    public enum ChipState
    {
        Free,
        Flying,
        Lying,
    }

    /// <summary>The rules of chips, not flashes (work order 127), plain numbers for the code that shows them.</summary>
    public static class HitChipsRules
    {
        public const int ColourCount = 10;

        /// <summary>Does a hit flash its body white? Only with the switch off (as before the order).</summary>
        public static bool Flashes(bool on) => !on;

        /// <summary>Is the pale ring drawn where a dead body lay when it is hidden? With the switch off as before; on, only by its own bool.</summary>
        public static bool RingWhenABodyGoes(bool on, bool ring) => !on || ring;

        /// <summary>The stuff an enemy's chips are, by its kind (its body: <see cref="ChipStuff"/>).</summary>
        public static ChipStuff StuffOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Guard: return ChipStuff.GreyBone;
                case EnemyKind.Lancer: return ChipStuff.RedBone;
                case EnemyKind.Caster:
                case EnemyKind.Warder:
                case EnemyKind.Caller: return ChipStuff.Robe; // work order 137: the caster's robe it wears
                case EnemyKind.Boss: return ChipStuff.Armour;
                default: return ChipStuff.Bone;
            }
        }

        /// <summary>The first and the second colour of a stuff (the dummy's are its own material's: <see cref="ChipColour.Bone"/> stands in).</summary>
        public static ChipColour First(ChipStuff stuff)
        {
            switch (stuff)
            {
                case ChipStuff.GreyBone: return ChipColour.GreyBone;
                case ChipStuff.RedBone: return ChipColour.RedBone;
                case ChipStuff.Robe: return ChipColour.Robe;
                case ChipStuff.Armour: return ChipColour.Steel;
                default: return ChipColour.Bone;
            }
        }

        public static ChipColour Second(ChipStuff stuff)
        {
            switch (stuff)
            {
                case ChipStuff.GreyBone: return ChipColour.BoneShade;
                case ChipStuff.RedBone: return ChipColour.RedBoneShade;
                case ChipStuff.Robe: return ChipColour.Bone;
                case ChipStuff.Armour: return ChipColour.SteelShade;
                default: return ChipColour.BoneShade;
            }
        }

        /// <summary>
        /// A chip's colour (its sides): its stuff's first colour, but bone for <see cref="HitChipsValues.SecondShare"/> of a hooded
        /// one's (<paramref name="roll"/> from 0 up to 1: cloth and bone come off it).
        /// </summary>
        public static ChipColour MainOf(ChipStuff stuff, double roll, HitChipsValues v) =>
            stuff == ChipStuff.Robe && v != null && roll < v.SecondShare ? ChipColour.Bone : First(stuff);

        /// <summary>A chip's second tone, on its broken ends (the second round: a solid thing, not a flake): the darker of its colour.</summary>
        public static ChipColour EndOf(ChipColour main)
        {
            switch (main)
            {
                case ChipColour.GreyBone: return ChipColour.GreyBoneShade;
                case ChipColour.RedBone:
                case ChipColour.RedBoneShade: return ChipColour.RedBoneShade;
                case ChipColour.Robe: return ChipColour.RobeShade;
                case ChipColour.Steel:
                case ChipColour.SteelShade: return ChipColour.SteelShade;
                default: return ChipColour.BoneShade;
            }
        }

        /// <summary>The colour's value.</summary>
        public static Rgb ColourOf(ChipColour c, HitChipsValues v)
        {
            switch (c)
            {
                case ChipColour.GreyBoneShade: return v.GreyBoneShade;
                case ChipColour.RobeShade: return v.RobeShade;
                case ChipColour.BoneShade: return v.BoneShade;
                case ChipColour.GreyBone: return v.GreyBone;
                case ChipColour.RedBone: return v.RedBone;
                case ChipColour.RedBoneShade: return v.RedBoneShade;
                case ChipColour.Robe: return v.Robe;
                case ChipColour.Steel: return v.Steel;
                case ChipColour.SteelShade: return v.SteelShade;
                default: return v.Bone;
            }
        }

        /// <summary>Chips one hit of <paramref name="damage"/> throws: the plain count by damage, within its bounds; a crit's more.</summary>
        public static int Count(int damage, bool crit, HitChipsValues v)
        {
            if (v == null) return 0;
            double plain = v.ChipsLeast + v.ChipsPerDamage * Math.Max(0, damage);
            int n = (int)Math.Floor(Math.Min(plain, Math.Max(v.ChipsLeast, v.ChipsMost)) + 1e-9);
            if (crit) n = (int)Math.Floor(n * v.CritCountScale + 1e-9) + v.CritExtra;
            return Math.Max(0, n);
        }

        /// <summary>
        /// Chips a target throws in one frame for all its hits: the count of its hardest hit (a crit's if one was), more for each
        /// further hit, at most <see cref="HitChipsValues.MostPerTarget"/>; a share of it on a hit that took a part off; none on a
        /// killing hit that breaks the body into its pieces.
        /// </summary>
        public static int ForFrame(int hits, int hardest, bool crit, bool partOff, bool bodyBreaks, HitChipsValues v)
        {
            if (v == null || hits <= 0 || bodyBreaks) return 0;
            int n = Count(hardest, crit, v) + Math.Max(0, hits - 1) * Math.Max(0, v.ChipsPerMoreHit);
            n = Math.Min(n, Math.Max(0, v.MostPerTarget));
            if (partOff) n = (int)Math.Floor(n * Math.Max(0, Math.Min(1, v.PartOffShare)) + 1e-9);
            return Math.Max(0, n);
        }

        /// <summary>m/s a hit of <paramref name="damage"/> throws its chips along the shot at: more for more damage, a crit's harder, at most <see cref="HitChipsValues.SpeedMost"/>.</summary>
        public static double Speed(int damage, bool crit, HitChipsValues v)
        {
            if (v == null) return 0;
            double s = v.SpeedBase + v.SpeedPerDamage * Math.Max(0, damage);
            if (crit) s *= v.CritSpeedScale;
            return Math.Max(0, Math.Min(s, v.SpeedMost));
        }

        /// <summary>
        /// m a chip leaving at <paramref name="along"/> m/s along the floor and <paramref name="up"/> m/s upward from
        /// <paramref name="height"/> m over its floor flies before it first meets the floor (the second round's measure: the spray
        /// must land on its enemy, not behind it).
        /// </summary>
        public static double Reach(double along, double up, double height, HitChipsValues v)
        {
            if (v == null || v.Gravity <= 0) return 0;
            double t = (up + Math.Sqrt(Math.Max(0, up * up + 2 * v.Gravity * Math.Max(0, height)))) / v.Gravity;
            return Math.Abs(along) * t;
        }

        /// <summary>m a chip measures across: its size, a crit's larger, times its enemy's shown scale within bounds.</summary>
        public static double SizeOf(bool crit, double shownScale, HitChipsValues v)
        {
            if (v == null) return 0;
            double k = Math.Max(1.0, Math.Min(shownScale, Math.Max(1.0, v.MostSizeScale)));
            return v.Size * (crit ? v.CritSizeScale : 1.0) * k;
        }

        /// <summary>
        /// A chip that meets the floor falling at <paramref name="fall"/> m/s (positive down): does it bounce (once, and only if it
        /// falls fast enough), or lie? Returns the speed it leaves upward with, or 0 when it lies.
        /// </summary>
        public static double BounceUp(double fall, bool bouncedBefore, HitChipsValues v)
        {
            if (v == null || bouncedBefore || fall < v.RestSpeed) return 0;
            return fall * Math.Max(0, Math.Min(1, v.Bounce));
        }

        /// <summary>
        /// What a chip does next: one flying lies when it has met its floor (<paramref name="landed"/>) and shrinks away anyway after
        /// <see cref="HitChipsValues.MostFlySeconds"/> (it is then counted as lying at its shrink); one lying is gone after
        /// <see cref="HitChipsValues.LieSeconds"/> and <see cref="HitChipsValues.ShrinkSeconds"/>.
        /// </summary>
        public static ChipState Next(ChipState state, double age, double lyingFor, bool landed, HitChipsValues v)
        {
            switch (state)
            {
                case ChipState.Flying:
                    return landed || age >= v.MostFlySeconds ? ChipState.Lying : state;
                case ChipState.Lying:
                    return lyingFor >= v.LieSeconds + v.ShrinkSeconds ? ChipState.Free : state;
                default:
                    return state;
            }
        }

        /// <summary>Share of its size a chip is shown at, <paramref name="lyingFor"/> s after it came to lie: whole, then shrinking to nothing.</summary>
        public static double Shown(double lyingFor, HitChipsValues v)
        {
            if (lyingFor <= v.LieSeconds) return 1;
            if (v.ShrinkSeconds <= 0) return 0;
            return Math.Max(0, 1 - (lyingFor - v.LieSeconds) / v.ShrinkSeconds);
        }

        /// <summary>
        /// The slot the next chip takes in a ring of <paramref name="cap"/> slots, after <paramref name="thrown"/> chips: each in turn,
        /// so that when the ring is full the slot taken is always the oldest chip's (the one thrown <paramref name="cap"/> chips ago).
        /// </summary>
        public static int Slot(long thrown, int cap) => cap <= 0 ? 0 : (int)(thrown % cap);
    }
}
