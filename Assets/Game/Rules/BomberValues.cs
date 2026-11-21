using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The bomber's numbers (work order 073, 2026-11-21: Kristian ticked "Ny fiendetype", and the producer proposed the kind
    /// for him to correct after playing; all first guesses): a small, fast thing that runs at her and blows up. It is the
    /// kind a whole room rushing at once wants (work order 070 beside it: a room is a fight): it makes her move NOW, it
    /// rewards shooting the right one first, and its blast hurts its own side as much as her, so that a bomber shot dead in
    /// the middle of a pack is the best thing that can happen to her.
    ///
    /// Decision 0010 (an enemy is read from its body and its telegraph, never from a surprise): it never blows without the
    /// circle of its blast having been on the ground for the fuse's whole time (<see cref="BomberFuse"/>).
    ///
    /// <see cref="BlastAttack"/> is the blast as an attack of the game: the range it lights its fuse at
    /// (<see cref="EnemyAttackDefinition.MaxDistance"/>), the full fuse (<see cref="EnemyAttackDefinition.Windup"/>), the
    /// circle (<see cref="EnemyAttackDefinition.Radius"/>) and her damage (<see cref="EnemyAttackDefinition.Damage"/>).
    /// </summary>
    [Serializable]
    public sealed class BomberValues
    {
        /// <summary>The blast's name, as the log writes its strike.</summary>
        public const string Blast = "Blast";

        public EnemyValues Enemy = new EnemyValues
        {
            // Health 100: two or three of her basic shots (40 each, Placeholders.BasicShotDamage): three, two with her flat
            // damage or a crit. Speed 6 m/s: a swarmer's 5 and a fifth more. It wakes and gives up as a swarmer does (9 m,
            // 22 m). No attack in the brain's list: its one attack is the blast, which it times itself (EnemyBomber).
            Health = 100, MoveSpeed = 6f, AggroRange = 9f, LeashRange = 22f, RespawnDelay = 4f,
            Attacks = new EnemyAttackDefinition[0],
        };

        /// <summary>
        /// The blast (work order 073): within 2.5 m of her it stops and lights; the circle of 3 m fills over the fuse of 0.7 s,
        /// then it blows. 20 to her inside the circle: a brute's Slam, its hit of the same shape (a circle of 3 m round it, 20;
        /// <see cref="EnemyValues.DefaultAttacks"/>), between today's Shield bash (15) and the Shockwave (30). A floor's bomber
        /// deals it times the floor's scale, as every floor enemy's strike (<see cref="LevelValues.DamageScaleOf"/>).
        /// </summary>
        public EnemyAttackDefinition BlastAttack = new EnemyAttackDefinition
        {
            Name = Blast, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 2.5f,
            Windup = 0.7f, Recovery = 0f, Damage = 20, Radius = 3f,
        };
        /// <summary>
        /// s of the fuse it lights where it died when it is killed before it lit (work order 073): shorter than the full one,
        /// the same circle and the same blast, so killing it at range in a crowd is the play, and killing it at her feet still
        /// has to be stepped out of.
        /// </summary>
        public float ShortFuseSeconds = 0.45f;
        /// <summary>
        /// The blast's damage to every enemy in its circle (work order 073: enough to kill a swarmer and hurt a guard): a
        /// practice room's swarmer has 300, a floor's 150 (<see cref="LevelValues.SwarmerHealth"/>); a floor's guard 3500.
        /// Not scaled by the floor. Another bomber in it is not hurt but lit (<see cref="BomberRules.What"/>).
        /// </summary>
        public int EnemyDamage = 300;

        // Its body and its look (work order 073): the swarmer's model, the ghoul, shown 0.8 of a swarmer's size and tinted
        // ember orange (EnemyFactory.BomberTint); the capsule as small. While its fuse burns it swells and flashes faster
        // and faster (BomberRules.Swell, FlashOn).
        public float BodyRadius = 0.25f;    // m, the capsule: a swarmer's 0.3, smaller
        public float BodyHeight = 0.9f;     // m: a swarmer's 1.1, smaller
        public float Shown = 0.8f;          // it is shown this times its model's size (the ghoul's 1.1 m: 0.88 m)
        public float SwellMost = 1.4f;      // times its size at the blast; it swells faster and faster to it
        public float FlashFirst = 0.2f;     // s from flash to flash as the fuse is lit...
        public float FlashLast = 0.05f;     // ...and as it blows, closing in evenly over the fuse

        /// <summary>The range it lights its fuse at, in m (<see cref="BlastAttack"/>'s reach).</summary>
        public float FuseRange => BlastAttack != null ? BlastAttack.MaxDistance : 0f;
        /// <summary>The full fuse, in s (<see cref="BlastAttack"/>'s wind-up).</summary>
        public float FuseSeconds => BlastAttack != null ? BlastAttack.Windup : 0f;
        /// <summary>The blast's circle, in m.</summary>
        public float BlastRadius => BlastAttack != null ? BlastAttack.Radius : 0f;
    }

    /// <summary>How a bomber's fuse was lit (work order 073), as the log writes it.</summary>
    public enum FuseCause
    {
        /// <summary>It came within <see cref="BomberValues.FuseRange"/> of her, alive: the full fuse.</summary>
        Range,
        /// <summary>It was killed before it lit: the short fuse, where it fell.</summary>
        Dropped,
        /// <summary>Another bomber's blast caught it: its own full fuse, never at once.</summary>
        Chain,
    }

    /// <summary>
    /// One bomber's fuse (work order 073), with no engine in it: lit once, at a time and for a time; it burns, and it blows
    /// when that time is up, never before. Killed or dazed while it burns, it goes on (it is already lit): nothing here
    /// puts it out. Lit again (another blast, its death) while it burns or after it blew: nothing changes.
    /// </summary>
    public sealed class BomberFuse
    {
        public bool IsLit { get; private set; }
        public bool HasBlown { get; private set; }
        public FuseCause Cause { get; private set; }
        /// <summary>Game time it was lit; negative before.</summary>
        public float LitAt { get; private set; } = -1f;
        /// <summary>s it burns.</summary>
        public float Seconds { get; private set; }
        /// <summary>Game time it blows.</summary>
        public float BlowAt => LitAt + Seconds;
        /// <summary>Lit and not yet blown: its circle is on the ground.</summary>
        public bool IsBurning => IsLit && !HasBlown;

        /// <summary>Lights it now, for the fuse of its cause (<see cref="BomberRules.FuseOf"/>). False when it was lit already.</summary>
        public bool Light(float now, FuseCause cause, BomberValues v)
        {
            if (IsLit) return false;
            IsLit = true;
            Cause = cause;
            LitAt = now;
            Seconds = Math.Max(0f, BomberRules.FuseOf(cause, v));
            return true;
        }

        /// <summary>Is its time up: burning, and the whole fuse gone?</summary>
        public bool Due(float now) => IsBurning && now >= BlowAt;

        /// <summary>It blew (once its time was up). False, and nothing changes, when it was not due.</summary>
        public bool Blow(float now)
        {
            if (!Due(now)) return false;
            HasBlown = true;
            return true;
        }

        /// <summary>0 to 1 through the fuse: how far its circle has filled. 0 unlit, 1 blown.</summary>
        public float Progress(float now)
        {
            if (!IsLit) return 0f;
            if (HasBlown || Seconds <= 0f) return 1f;
            float p = (now - LitAt) / Seconds;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        /// <summary>s of the fuse left; 0 unlit or blown.</summary>
        public float Left(float now) => IsBurning ? Math.Max(0f, BlowAt - now) : 0f;
    }

    /// <summary>What a bomber's blast does to another enemy in its circle (work order 073).</summary>
    public enum BlastEffect
    {
        /// <summary>Nothing: outside the circle, dead, or a bomber already lit.</summary>
        Nothing,
        /// <summary>It takes <see cref="BomberValues.EnemyDamage"/>.</summary>
        Hurts,
        /// <summary>A bomber not yet lit: it lights its own full fuse (a chain, each with its full fuse, never an instant chain), and is not hurt.</summary>
        Lights,
    }

    /// <summary>The bomber's rules (work order 073): when it lights, how long it burns, whom its blast reaches, how it looks while it burns.</summary>
    public static class BomberRules
    {
        /// <summary>Does it light its fuse now: alive, awake, she alive, and within <see cref="BomberValues.FuseRange"/> of her?</summary>
        public static bool Lights(bool alive, bool awake, bool herAlive, float distance, BomberValues v) =>
            alive && awake && herAlive && distance <= v.FuseRange;

        /// <summary>The fuse for how it was lit: the short one when it was killed before it lit, else the full one (a chain's too).</summary>
        public static float FuseOf(FuseCause cause, BomberValues v) => cause == FuseCause.Dropped ? v.ShortFuseSeconds : v.FuseSeconds;

        /// <summary>Does a body of this radius at <paramref name="target"/> touch the blast's circle round <paramref name="centre"/>?</summary>
        public static bool InBlast(GroundPoint centre, BomberValues v, GroundPoint target, float targetRadius) =>
            SkillRules.InCircle(centre, v.BlastRadius, target, targetRadius);

        /// <summary>Is she hurt: inside the circle and not in the dash's protected part (its invulnerable frames, as for any strike)?</summary>
        public static bool HurtsHer(bool inside, bool invulnerable) => inside && !invulnerable;

        /// <summary>Her damage from a blast, by the bomber's damage scale (the floor's; 1 in the practice room), rounded as every strike's.</summary>
        public static int HerDamage(BomberValues v, float damageScale)
        {
            int d = v.BlastAttack != null ? v.BlastAttack.Damage : 0;
            return damageScale == 1f ? d : (int)Math.Round(d * Math.Max(0f, damageScale), MidpointRounding.ToEven);
        }

        /// <summary>What the blast does to another enemy: one in its circle and alive is hurt, a bomber not yet lit is lit instead.</summary>
        public static BlastEffect What(bool inside, bool alive, bool isBomber, bool bomberLit)
        {
            if (!inside) return BlastEffect.Nothing;
            if (isBomber) return bomberLit ? BlastEffect.Nothing : BlastEffect.Lights;
            return alive ? BlastEffect.Hurts : BlastEffect.Nothing;
        }

        /// <summary>Times its size at this share of its fuse: from 1 to <see cref="BomberValues.SwellMost"/>, faster and faster.</summary>
        public static float Swell(float progress, BomberValues v)
        {
            float p = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            return 1f + (v.SwellMost - 1f) * p * p;
        }

        /// <summary>s from one flash to the next at this share of its fuse: from <see cref="BomberValues.FlashFirst"/> to <see cref="BomberValues.FlashLast"/>.</summary>
        public static float FlashPeriod(float progress, BomberValues v)
        {
            float p = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            return Math.Max(1e-3f, v.FlashFirst + (v.FlashLast - v.FlashFirst) * p);
        }

        /// <summary>
        /// Flashes counted from the lighting, <paramref name="since"/> s into a fuse of <paramref name="fuse"/> s: the period
        /// closes in evenly from <see cref="BomberValues.FlashFirst"/> to <see cref="BomberValues.FlashLast"/>, so they come
        /// faster and faster.
        /// </summary>
        public static float Flashes(float since, float fuse, BomberValues v)
        {
            if (since <= 0f) return 0f;
            float a = Math.Max(1e-3f, v.FlashFirst), b = Math.Max(1e-3f, v.FlashLast);
            if (fuse <= 0f) return since / b;
            float t = Math.Min(since, fuse);
            double count = Math.Abs(b - a) < 1e-6f ? t / a : fuse / (b - a) * Math.Log((a + (b - a) * t / fuse) / a);
            if (since > fuse) count += (since - fuse) / b;
            return (float)count;
        }

        /// <summary>Is its body lit up now: the first half of each flash.</summary>
        public static bool FlashOn(float since, float fuse, BomberValues v)
        {
            float n = Flashes(since, fuse, v);
            return n - (float)Math.Floor(n) < 0.5f;
        }
    }
}
