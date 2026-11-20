using System;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Every number and colour of the effects of work order 024 (2026-11-20), with their defaults here in code: no asset
    /// holds them. All first guesses; nobody has seen them yet. Sizes in metres, times in seconds. Every effect is short
    /// (most under 0.3 s), flat and sized to what it tells (pillar 4, readable combat). Her own effects stay warm (orange,
    /// white-hot) and the enemies' stay in theirs (red for a strike, blue for a counterable one, the boss's warm
    /// red-orange), so the two are never confused; the cyan-teal of the world's lanterns and runes is for the world and
    /// for rewards. <see cref="On"/> off leaves the game exactly as it looked before.
    /// </summary>
    [Serializable]
    public sealed class CombatFxValues
    {
        /// <summary>Off: none of these effects, the flat strike stand-in back, and the four pool skills borrowing their nearest's look again.</summary>
        public bool On = true;

        [Header("Glow, as the shot effects' start values")]
        public float RimGlow = ShotFxSettings.StartRimGlow;
        public float CoreGlow = ShotFxSettings.StartCoreGlow;
        [Tooltip("The counter's core: the brightest thing on screen for that moment.")]
        public float BrightestGlow = 7f;
        [Tooltip("Dust and trails: a faint glow.")]
        public float FaintRimGlow = 1.1f;
        public float FaintCoreGlow = 1.6f;

        public FxLimits Limits = new FxLimits();
        public FxSizes Sizes = new FxSizes();

        [Header("Colours: rim, then core")]
        public Color StrikeRim = new Color(1f, 0.16f, 0.1f);        // the telegraph's red
        public Color StrikeCore = new Color(1f, 0.86f, 0.8f);
        public Color CounterRim = new Color(0.15f, 0.45f, 1f);      // the counterable telegraph's blue (Enemy)
        public Color CounterCore = new Color(0.85f, 0.93f, 1f);
        public Color BoneRim = new Color(0.86f, 0.8f, 0.66f);
        public Color BoneCore = new Color(1f, 0.97f, 0.9f);
        public Color DazedRim = new Color(0.55f, 0.75f, 1f);        // the daze tint (Enemy)
        public Color DazedCore = new Color(0.95f, 0.98f, 1f);
        public Color BossRim = new Color(1f, 0.36f, 0.12f);
        public Color BossCore = new Color(1f, 0.85f, 0.65f);
        public Color AngryRim = new Color(1f, 0.55f, 0.32f);        // the angry boss's tint (EnemyBoss)
        public Color AngryCore = new Color(1f, 0.92f, 0.8f);
        public Color DustRim = new Color(0.62f, 0.55f, 0.45f);
        public Color DustCore = new Color(0.86f, 0.8f, 0.7f);
        public Color ArrowRim = new Color(0.9f, 0.88f, 0.8f);
        public Color ArrowCore = new Color(1f, 1f, 0.97f);
        public Color HerRim = new Color(1f, 0.6f, 0.15f);           // the shot looks' default rim
        public Color HerCore = new Color(1f, 1f, 0.92f);
        public Color HurtRim = new Color(1f, 0.12f, 0.08f);
        public Color HurtCore = new Color(1f, 0.8f, 0.75f);
        public Color MarkRim = new Color(1f, 0.55f, 0.1f);          // the mark pips'
        public Color MarkCore = new Color(1f, 0.95f, 0.65f);
        public Color MarkFullRim = new Color(1f, 0.15f, 0.08f);     // the mark pips' at the cap
        public Color MarkFullCore = new Color(1f, 0.75f, 0.55f);
        public Color TagRim = new Color(0.2f, 1f, 0.45f);           // the tag's sign over the pips
        public Color TagCore = new Color(0.8f, 1f, 0.85f);
        public Color WorldRim = new Color(0.15f, 0.9f, 0.85f);      // the cyan-teal of the lanterns and runes
        public Color WorldCore = new Color(0.85f, 1f, 1f);

        [Header("1. A strike")]
        public float StrikeRingSeconds = 0.25f;
        public float StrikeStar = 0.8f;
        public float StrikeStarSeconds = 0.16f;
        public float StrikeStarHeight = 0.6f;
        public float StrikeWedgeSeconds = 0.22f;
        public float StrikeLineSeconds = 0.2f;
        public float StrikeLineHeight = 0.35f;
        [Tooltip("The line's streak is this many times the line's half width.")]
        public float StrikeLineWidth = 0.8f;

        [Header("2. A windup")]
        public float WindupSize = 1f;
        public float WindupSeconds = 0.22f;
        public int WindupSpikes = 8;

        [Header("3. A death")]
        public float DeathStar = 0.9f;
        public float DeathStarSeconds = 0.2f;
        public float ShardReach = 1.4f;
        public float ShardSize = 0.28f;
        public float ShardSeconds = 0.38f;
        public float HiddenRing = 0.9f;
        public float HiddenRingSeconds = 0.45f;

        [Header("4. Appearing")]
        public float AppearRing = 1.4f;
        public float AppearSeconds = 0.3f;
        public float AppearSpike = 1.3f;
        public float AppearSpikeSeconds = 0.25f;

        [Header("5. Dazed")]
        public float DazedRadius = 0.5f;
        public float DazedOverHead = 0.35f;
        public float DazedSpin = 90f;     // degrees a second
        public float DazedFade = 0.2f;
        public float DazedMost = 20f;

        [Header("6. The shooter's arrow")]
        public float ArrowWidth = 0.12f;
        public float ArrowSeconds = 0.16f;

        [Header("7 to 10. The boss")]
        public float DustRadius = 1.1f;
        public float DustAngle = 70f;
        public float DustSeconds = 0.22f;
        public float TrailWidth = 0.9f;
        public float TrailLength = 4f;
        public float TrailHeight = 1.2f;
        public float TrailFade = 0.2f;
        public float ChargeEndRing = 2.5f;
        public float ChargeEndSeconds = 0.3f;
        public float AngerRing = 6f;
        public float AngerSeconds = 0.45f;
        public float AngerSpike = 4.5f;
        public float AngerSpikeSeconds = 0.4f;
        public float CallRing = 4.5f;
        public float CallSeconds = 0.35f;
        public float BossBurstGap = 0.25f;
        public float BossBurstStar = 2.4f;
        public float BossShardReach = 3f;
        public float BossShardSize = 0.5f;
        public float BossDeathRing = 6f;
        public float BossDeathSeconds = 0.5f;

        [Header("11. The four pool skills' own effects")]
        public float TagDiamond = 0.8f;
        public float TagSeconds = 0.25f;
        public float SlugRing = 1.6f;
        public float SlugSeconds = 0.22f;
        public float SlideDust = 1.3f;
        public float SlideDustAngle = 60f;
        public float SlideDustSeconds = 0.22f;
        public float SlideTrailWidth = 0.5f;
        public float SlideTrailSeconds = 0.2f;
        public float SlideHeight = 0.5f;

        [Header("12 to 18. Her")]
        public float CounterStar = 2f;
        public float CounterSeconds = 0.22f;
        public float CounterRing = 2.4f;
        public float CounterRingSeconds = 0.3f;
        public float PerfectRing = 1.4f;
        public float PerfectRingSeconds = 0.25f;
        [Tooltip("The perfect release's spark is this many times the look's hit spark.")]
        public float PerfectSparkScale = 1.8f;
        public float MarkBurstSpeed = 2.5f;
        public float MarkBurstSeconds = 0.3f;
        public float MarkCapSpike = 1.2f;
        public float MarkPop = 0.3f;
        public float MarkPopSeconds = 0.12f;
        [Tooltip("A keg set off by a shot bursts this many times larger than its fuse's burst, times its own burst scale.")]
        public float KegShotScale = 1.25f;
        public float KegShotRing = 1.5f;
        public float KegShotSeconds = 0.35f;
        public float DashFlash = 1f;
        public float DashFlashSeconds = 0.1f;
        public float DashWidth = 0.45f;
        public float DashSeconds = 0.2f;
        public float DashHeight = 0.9f;
        public float HerChest = 1.1f;
        public float HurtSeconds = 0.16f;

        [Header("21. Melee you read (work order 035): the glint on an enemy, and her stunned and pulled")]
        [Tooltip("A skill's glint at its full size, on a brute: it grows to this through the windup and snaps at the strike.")]
        public float GlintSize = 1.6f;
        public float GlintSnapSeconds = 0.2f;
        [Tooltip("An ordinary attack's glint: one small flick as its windup begins.")]
        public float FlickSize = 0.8f;
        public float FlickSeconds = 0.16f;
        [Tooltip("The glint's height, a share of the enemy's height: about its chest or weapon.")]
        public float GlintHeight = 0.6f;
        [Tooltip("The glint's place in front of the enemy, m on a brute.")]
        public float GlintForward = 0.55f;
        public float StunnedRadius = 0.45f;
        public float StunnedOverHead = 0.35f;
        public float StunnedSpin = 240f;   // degrees a second: faster than an enemy's daze
        public float PulledWidth = 0.55f;
        public float PulledHeight = 0.9f;

        [Header("19 and 20. The run")]
        public int ChestStars = 7;
        public float ChestStar = 0.38f;
        public float ChestStarSeconds = 0.55f;
        public float ChestRise = 4.5f;
        public float ChestSpread = 1.4f;
        public float ChestGravity = 12f;
        public float ChestRing = 1.8f;
        public float ChestRingSeconds = 0.4f;
        public float ChoiceRing = 1f;
        public float ChoiceRise = 3f;
        public float ChoiceSeconds = 0.5f;
        public float ChoiceStar = 0.35f;
        public float ChoiceStarSeconds = 0.45f;
    }
}
