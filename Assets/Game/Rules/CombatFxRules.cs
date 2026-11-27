using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The moments the effects of work order 024 (2026-11-20) answer, named for what happened, not for the picture: the
    /// game turns its own reports into these (as <see cref="SoundMoment"/> for the placeholder sounds), and
    /// <see cref="CombatFxRules.ShapesOf"/> says what each is drawn with. The numbers in the comments are the order's.
    /// </summary>
    public enum FxMoment
    {
        // The enemies
        EnemyStruck,       // 1: a strike went off from where the enemy stands: the shape of its attack
        StrikeHitHer,      // 1: ...and it hurt her: a spark on her in its colours
        WindupStarted,     // 2: a windup began
        EnemyDied,         // 3: its health reached 0 and its death clip starts
        BodyHidden,        // 3: the body is gone from sight: a ring where it lay
        EnemyAppeared,     // 4: an enemy came while a fight was on (the boss's called pack)
        EnemyDazed,        // 5: its stagger bar was emptied, or a counter dazed it
        ArrowLoosed,       // 6: the shooter let its shot go
        // The boss
        BossChargeStep,    // 7: every half metre of its charge's run
        BossChargeEnded,   // 7: the run ended
        BossAngered,       // 8: it turned angry
        BossCalled,        // 9: it called a pack
        BossDied,          // 10: it died
        // Her
        TagRoundTagged,    // 11: Tag Round tagged its target
        BreakerSlugHit,    // 11: Breaker Slug hit a target
        SlideStarted,      // 11: Slide began
        SlideEnded,        // 11: Slide ended
        Countered,         // 12: a hit of hers countered an attack
        PerfectRelease,    // 13: a charge skill let go in its perfect band
        MarksSpent,        // 14: a spender took the marks off a target
        MarkAdded,         // 15: a mark went on a target
        KegLying,          // 16: a keg landed and lies on its fuse
        KegSetOffByShot,   // 16: a shot of hers set a lying keg off
        Dashed,            // 17: her dash began
        HerHurt,           // 18: she took damage
        // The run
        ChestOpened,       // 19: a chest was opened
        ChoiceTaken,       // 20: a skill, an upgrade or a chest's upgrade was picked
    }

    /// <summary>
    /// The shapes the effects are drawn with (work order 024). The first six are the shot effects' own, approved by
    /// Kristian on 2026-11-18 ("love the effects, exactly the style I had in mind"); the rest are new, made the same way:
    /// a flat outer shape in a rim colour and an inner one in a core colour, the core glowing under the bloom.
    /// - Flash: a spiky star along a direction. Streak: a tapered bar. Spark: a round spiky star. Wedge: a jagged slice of
    ///   a disc blown outward. Ring: a shockwave on the ground. Outline: a thin ring at the edge of a circle.
    /// - Shards: small triangles flying out from a point. Converge: thin spikes pulling in to a point. Closing: a thin
    ///   ring on the ground closing in to its centre. Orbit: small stars turning in a ring over a point. Spike: a tall
    ///   spiky star pointing up. Diamond: a four-pointed star. SideSpikes: spikes off both sides of a strip on the ground.
    /// </summary>
    public enum FxShape { Flash, Streak, Spark, Wedge, Ring, Outline, Shards, Converge, Closing, Orbit, Spike, Diamond, SideSpikes }

    /// <summary>
    /// How much an effect matters when there are too many (work order 024, the limits): a minor one goes before a plain
    /// one, and a key one, the mark of a rare and important moment, is never dropped.
    /// </summary>
    public enum FxTier { Minor, Plain, Key }

    /// <summary>
    /// The limits of the effects (work order 024, 2026-11-20; pillar 4, readable combat: "a screen full of effects you
    /// cannot read" is what it must not be, and a room holds up to twenty-four enemies). First guesses.
    /// </summary>
    [Serializable]
    public sealed class FxLimits
    {
        /// <summary>At most this many pieces live at once; past it the least of them goes (<see cref="FxBudget"/>).</summary>
        public int MostLive = 160;
        // At most this many of a kind that can come many times in one frame in a crowd, per frame; the rest are not drawn.
        public int StrikesPerFrame = 8;
        public int HitsOnHerPerFrame = 2;
        public int WindupsPerFrame = 6;
        public int DeathsPerFrame = 8;
        public int AppearancesPerFrame = 12;
        public int MarksAddedPerFrame = 4;
        public int SlugHitsPerFrame = 6;

        /// <summary>The cap per frame of a moment; 0: none. A key moment is never capped.</summary>
        public int PerFrame(FxMoment moment)
        {
            switch (moment)
            {
                case FxMoment.EnemyStruck:
                case FxMoment.ArrowLoosed: return StrikesPerFrame;
                case FxMoment.StrikeHitHer:
                case FxMoment.HerHurt: return HitsOnHerPerFrame;
                case FxMoment.WindupStarted: return WindupsPerFrame;
                case FxMoment.EnemyDied:
                case FxMoment.BodyHidden: return DeathsPerFrame;
                case FxMoment.EnemyAppeared: return AppearancesPerFrame;
                case FxMoment.MarkAdded: return MarksAddedPerFrame;
                case FxMoment.BreakerSlugHit: return SlugHitsPerFrame;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// The sizes of the effects that depend on what they tell (work order 024): the body of the enemy, the marks spent,
    /// the hit she took. First guesses.
    /// </summary>
    [Serializable]
    public sealed class FxSizes
    {
        // Every effect on an enemy is this many times its size on a brute: small for a swarmer, larger for a guard.
        public float Swarmer = 0.6f;
        public float Brute = 1f;
        public float Shooter = 0.85f;
        public float Guard = 1.35f;
        public float Boss = 2.2f;
        // The shards of a death: five to eight, by size.
        public int ShardsSmall = 5;
        public int ShardsPlain = 6;
        public int ShardsLarge = 8;
        // Marks spent: a star from each pip, this large for one mark, this much larger for each more...
        public float MarkStar = 0.3f;
        public float MarkStarGrowth = 0.08f;
        // ...and at the cap this many times larger, and twice as many.
        public float MarkStarAtCap = 1.6f;
        // Her hurt spark: this large for the smallest hit, this large for a hit of HurtFullAt of her health or more.
        public float HurtSmallest = 0.55f;
        public float HurtLargest = 1.4f;
        public float HurtFullAt = 0.25f;

        public float KindScale(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Swarmer:
                case EnemyKind.Bomber: return Swarmer; // work order 073: a bomber's size for the effects is a swarmer's
                case EnemyKind.Shooter:
                case EnemyKind.Caster: return Shooter; // work order 034: a caster's size for the effects is a shooter's
                case EnemyKind.Warder: return Shooter; // work order 079: a warder's too, as the caster whose model it wears
                case EnemyKind.Caller: return Shooter; // work order 137: a caller's too, as the caster whose model it wears
                case EnemyKind.Guard: return Guard;
                case EnemyKind.Boss: return Boss;
                default: return Brute;
            }
        }

        public int ShardCount(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Swarmer:
                case EnemyKind.Bomber: return ShardsSmall; // work order 073
                case EnemyKind.Guard:
                case EnemyKind.Boss: return ShardsLarge;
                default: return ShardsPlain;
            }
        }

        /// <summary>How many stars burst over a target that had <paramref name="marks"/> taken: one a pip, twice as many at the cap.</summary>
        public int MarkStars(int marks, int cap)
        {
            if (marks <= 0) return 0;
            return marks >= Math.Max(1, cap) ? marks * 2 : marks;
        }

        /// <summary>The size of each of those stars: larger with more marks, and much larger at the cap.</summary>
        public float MarkStarSize(int marks, int cap)
        {
            if (marks <= 0) return 0f;
            float size = MarkStar + MarkStarGrowth * (marks - 1);
            return marks >= Math.Max(1, cap) ? size * MarkStarAtCap : size;
        }

        /// <summary>Her hurt spark for a hit of <paramref name="amount"/> on a maximum of <paramref name="max"/>.</summary>
        public float HurtSize(int amount, int max)
        {
            if (amount <= 0) return HurtSmallest;
            float full = Math.Max(1f, HurtFullAt * Math.Max(1, max));
            float k = Math.Min(1f, amount / full);
            return HurtSmallest + (HurtLargest - HurtSmallest) * k;
        }
    }

    /// <summary>The rules of the effects of work order 024 (2026-11-20), with no engine in them.</summary>
    public static class CombatFxRules
    {
        /// <summary>Every moment, in order.</summary>
        public static readonly FxMoment[] All = (FxMoment[])Enum.GetValues(typeof(FxMoment));

        /// <summary>
        /// What a moment is drawn with. A strike's shapes depend on its attack (<see cref="StrikeShapes"/>); those listed
        /// here for <see cref="FxMoment.EnemyStruck"/> are all it may use.
        /// </summary>
        public static FxShape[] ShapesOf(FxMoment moment)
        {
            switch (moment)
            {
                case FxMoment.EnemyStruck: return new[] { FxShape.Ring, FxShape.Spark, FxShape.Wedge, FxShape.Streak, FxShape.SideSpikes };
                case FxMoment.StrikeHitHer: return new[] { FxShape.Spark };
                case FxMoment.WindupStarted: return new[] { FxShape.Converge };
                case FxMoment.EnemyDied: return new[] { FxShape.Spark, FxShape.Shards };
                case FxMoment.BodyHidden: return new[] { FxShape.Outline };
                case FxMoment.EnemyAppeared: return new[] { FxShape.Closing, FxShape.Spike };
                case FxMoment.EnemyDazed: return new[] { FxShape.Orbit };
                case FxMoment.ArrowLoosed: return new[] { FxShape.Streak };
                case FxMoment.BossChargeStep: return new[] { FxShape.Wedge, FxShape.Streak };
                case FxMoment.BossChargeEnded: return new[] { FxShape.Ring };
                case FxMoment.BossAngered: return new[] { FxShape.Ring, FxShape.Spike };
                case FxMoment.BossCalled: return new[] { FxShape.Ring };
                case FxMoment.BossDied: return new[] { FxShape.Spark, FxShape.Shards, FxShape.Ring };
                case FxMoment.TagRoundTagged: return new[] { FxShape.Diamond };
                case FxMoment.BreakerSlugHit: return new[] { FxShape.Ring };
                case FxMoment.SlideStarted: return new[] { FxShape.Wedge };
                case FxMoment.SlideEnded: return new[] { FxShape.Streak };
                case FxMoment.Countered: return new[] { FxShape.Spark, FxShape.Ring };
                case FxMoment.PerfectRelease: return new[] { FxShape.Ring, FxShape.Spark };
                case FxMoment.MarksSpent: return new[] { FxShape.Spark, FxShape.Spike };
                case FxMoment.MarkAdded: return new[] { FxShape.Spark };
                case FxMoment.KegLying: return new[] { FxShape.Closing };
                case FxMoment.KegSetOffByShot: return new[] { FxShape.Spark, FxShape.Ring };
                case FxMoment.Dashed: return new[] { FxShape.Flash, FxShape.Streak };
                case FxMoment.HerHurt: return new[] { FxShape.Spark };
                case FxMoment.ChestOpened: return new[] { FxShape.Spark, FxShape.Ring };
                case FxMoment.ChoiceTaken: return new[] { FxShape.Ring, FxShape.Spark };
                default: return new FxShape[0];
            }
        }

        /// <summary>
        /// A strike's shapes by its attack (work order 024, 1): a circle bursts as a shockwave ring to its edge with a
        /// short spiky star at the centre; a cone as a jagged wedge blown outward; a line as a tapered streak down its
        /// length with spikes off its sides.
        /// </summary>
        public static FxShape[] StrikeShapes(EnemyAttackShape shape)
        {
            switch (shape)
            {
                case EnemyAttackShape.Circle: return new[] { FxShape.Ring, FxShape.Spark };
                case EnemyAttackShape.Cone: return new[] { FxShape.Wedge };
                default: return new[] { FxShape.Streak, FxShape.SideSpikes };
            }
        }

        /// <summary>
        /// How much a moment's effect matters: a counter, the boss's anger and its death, and marks spent at the cap are
        /// key, never dropped; the small ones that come constantly (a mark gained, a windup, the dust of a charge) are
        /// minor; the rest plain.
        /// </summary>
        public static FxTier TierOf(FxMoment moment, int marks = 0, int cap = 5)
        {
            switch (moment)
            {
                case FxMoment.Countered:
                case FxMoment.BossAngered:
                case FxMoment.BossDied: return FxTier.Key;
                case FxMoment.MarksSpent: return marks >= Math.Max(1, cap) ? FxTier.Key : FxTier.Plain;
                case FxMoment.MarkAdded:
                case FxMoment.WindupStarted:
                case FxMoment.BossChargeStep: return FxTier.Minor;
                default: return FxTier.Plain;
            }
        }

        /// <summary>An enemy that came is drawn appearing only while a fight is on: another enemy is awake (work order 024, 4).</summary>
        public static bool AppearsInAFight(bool anotherAwake) => anotherAwake;

        /// <summary>The boss's charge kicks up dust every this many metres it runs (work order 024, 7).</summary>
        public const float ChargeDustEvery = 0.5f;

        /// <summary>How many dust wedges are due after running <paramref name="run"/> metres when <paramref name="done"/> are out.</summary>
        public static int DustDue(float run, int done, float every = ChargeDustEvery) =>
            every <= 0f ? 0 : Math.Max(0, (int)Math.Floor(run / every + 1e-4) - done);
    }

    /// <summary>
    /// The limits rule (work order 024, 2026-11-20): at most <see cref="FxLimits.MostLive"/> pieces live at once, and when
    /// a new one comes past that the least of those live goes first: a minor one before a plain one, the smallest of
    /// them, and of those the oldest. A key piece is never dropped: it is let in past the cap when nothing else can go.
    /// A piece less than everything it would push out is not let in. A moment that can come many times in one frame is
    /// let through at most its cap a frame (<see cref="FxLimits.PerFrame"/>); a key one always.
    /// </summary>
    public sealed class FxBudget
    {
        struct Entry
        {
            public int Id;
            public FxTier Tier;
            public float Size;
            public long Born;
        }

        readonly List<Entry> _live = new List<Entry>();
        readonly Dictionary<FxMoment, int> _asked = new Dictionary<FxMoment, int>();
        int _frame = int.MinValue;
        long _born;

        public FxLimits Limits;

        public FxBudget(FxLimits limits) => Limits = limits ?? new FxLimits();

        /// <summary>The pieces live now.</summary>
        public int Live => _live.Count;

        public bool IsLive(int id)
        {
            foreach (Entry e in _live) if (e.Id == id) return true;
            return false;
        }

        /// <summary>May a moment's effect be drawn in frame <paramref name="frame"/>? Counts it when it may.</summary>
        public bool MayAsk(FxMoment moment, int frame, FxTier tier)
        {
            if (frame != _frame)
            {
                _frame = frame;
                _asked.Clear();
            }
            _asked.TryGetValue(moment, out int n);
            int cap = Limits.PerFrame(moment);
            if (tier != FxTier.Key && cap > 0 && n >= cap) return false;
            _asked[moment] = n + 1;
            return true;
        }

        /// <summary>
        /// Lets a piece in, or not. The ids of the pieces it pushes out are added to <paramref name="dropped"/>; the caller
        /// removes them. False when it is not let in.
        /// </summary>
        public bool Admit(int id, FxTier tier, float size, List<int> dropped)
        {
            int most = Math.Max(0, Limits.MostLive);
            while (_live.Count >= most)
            {
                int victim = Least();
                if (victim < 0)
                {
                    if (tier == FxTier.Key) break; // nothing but key pieces live: a key one comes in past the cap
                    return false;
                }
                Entry v = _live[victim];
                if (tier != FxTier.Key && Before(new Entry { Tier = tier, Size = size, Born = long.MaxValue }, v)) return false;
                _live.RemoveAt(victim);
                dropped?.Add(v.Id);
            }
            _live.Add(new Entry { Id = id, Tier = tier, Size = size, Born = _born++ });
            return true;
        }

        /// <summary>A piece ended by itself, or was removed.</summary>
        public void Gone(int id)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Id != id) continue;
                _live.RemoveAt(i);
                return;
            }
        }

        public void Clear()
        {
            _live.Clear();
            _asked.Clear();
        }

        /// <summary>The index of the live piece that goes first, or -1 when all are key.</summary>
        int Least()
        {
            int best = -1;
            for (int i = 0; i < _live.Count; i++)
            {
                Entry e = _live[i];
                if (e.Tier == FxTier.Key) continue;
                if (best < 0 || Before(e, _live[best])) best = i;
            }
            return best;
        }

        /// <summary>Does <paramref name="a"/> go before <paramref name="b"/>: a lower tier, then smaller, then older.</summary>
        static bool Before(Entry a, Entry b)
        {
            if (a.Tier != b.Tier) return a.Tier < b.Tier;
            if (Math.Abs(a.Size - b.Size) > 1e-4f) return a.Size < b.Size;
            return a.Born < b.Born;
        }
    }
}
