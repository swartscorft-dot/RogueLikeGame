using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The act boss as a finale (work order 106, 2026-11-24), its numbers: all first guesses, the producer's where the order
    /// gives them ("about 1.5 s", "4 m", "about 1 s from crouch to landing", "about one attack in four", "Knight's Resolve",
    /// "2 points", "within 4 m"), the rest the agent's. Held by <see cref="BossValues.Phases"/>; on only with
    /// <see cref="BossValues.Finale"/>.
    ///
    /// The break: when its health first falls under <see cref="BossValues.AngerBelow"/> (half), the moment it has turned angry
    /// since work order 014, it stops and roars for <see cref="RoarSeconds"/>: it stands, it cannot be staggered, and her
    /// damage still counts. The room answers: the throne room's lamps take <see cref="LampColour"/> for the rest of the fight.
    /// Then phase two: the angry numbers as before, and the Ground Slam.
    /// </summary>
    [Serializable]
    public sealed class BossFinaleValues
    {
        /// <summary>s it stands and roars at the break (the producer's "about 1.5 s").</summary>
        public float RoarSeconds = 1.5f;
        /// <summary>
        /// The colour the throne room's lamps take at the break, for the rest of the fight (the agent's first guess): a cold
        /// grave-green, picked to stay apart from the red of its telegraphs and the blue of the counterable charge on the floor.
        /// Only the colour of the lights the room already has changes; their brightness stays theirs.
        /// </summary>
        public Rgb LampColour = new Rgb(0.45f, 0.95f, 0.55f);

        /// <summary>
        /// The Ground Slam, phase two only: it leaps to where she stood when the wind-up (its crouch) began and lands with a ring
        /// of damage, told as its other attacks are: on its body (it crouches, then rises in an arc) and by the Spin's ring on the
        /// floor, round the place it will land. Not counterable (red). The windup is the whole of it, from the crouch to the
        /// landing (the producer's "about 1 s"); the recovery is her opening where it landed.
        /// </summary>
        public EnemyAttackDefinition Slam = new EnemyAttackDefinition
        {
            // MaxDistance: the farthest it leaps for; the damage is the agent's, between the Spin's 35 and the Charge's 45.
            Name = BossFinale.Slam, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 14f,
            Windup = 1.0f, Recovery = 1.2f, Damage = 40, Radius = 4f, Counterable = false,
        };
        /// <summary>In phase two one attack chosen in this many is the slam, from the fourth on (the producer's "about one attack in four").</summary>
        public int SlamEvery = 4;
        /// <summary>m: farther than this it leaps only at her when it sees her (no wall or tall prop between), as its charge does.</summary>
        public float SlamSightFrom = 5f;
        /// <summary>s at the start of the windup it crouches where it stands before it leaves the ground.</summary>
        public float SlamCrouch = 0.35f;
        /// <summary>Its height while it crouches, as a share of its own: the body's tell (drawn only).</summary>
        public float SlamSquash = 0.85f;
        /// <summary>m over the ground at the top of the leap's arc (drawn only: its body on the ground goes straight to the landing).</summary>
        public float SlamHeight = 1.6f;
        /// <summary>m round the landing it looks for walkable ground to stand on.</summary>
        public float SlamFindGround = 2f;
    }

    /// <summary>
    /// The finale's rules with no engine in them (work order 106): the break, once; phase two's choice of attack, with the Ground
    /// Slam one in <see cref="BossFinaleValues.SlamEvery"/>; and where the slam lands. The rest (the roar, the leap, the lamps) is
    /// <c>EnemyBoss</c>'s.
    /// </summary>
    public sealed class BossPhases
    {
        /// <summary>True from the break on (phase two); it stays so.</summary>
        public bool Broken { get; private set; }
        /// <summary>Attacks chosen in phase two (the angry second charge is the first's, not a choice).</summary>
        public int Choices { get; private set; }
        /// <summary>Ground Slams chosen.</summary>
        public int Slams { get; private set; }

        /// <summary>
        /// The break, once: true on the one call in which its health share first falls under <see cref="BossValues.AngerBelow"/>,
        /// whichever blow takes it there (one that takes it from full to a sliver too). False with the finale off.
        /// </summary>
        public bool Breaks(float healthShare, BossValues v)
        {
            if (Broken || !BossValues.Finale || v == null || healthShare >= v.AngerBelow) return false;
            Broken = true;
            return true;
        }

        /// <summary>
        /// Is a slam due at the next choice? When fewer slams have come than one for every <see cref="BossFinaleValues.SlamEvery"/>
        /// choices counting the next: the 4th, the 8th, the 12th... A slam that does not fit when due waits for the next choice that it fits.
        /// </summary>
        public bool SlamDue(BossFinaleValues f) => Broken && f != null && f.SlamEvery > 0 && Slams < (Choices + 1) / f.SlamEvery;

        /// <summary>Does the slam reach her: within its band, and seen when she is farther than <see cref="BossFinaleValues.SlamSightFrom"/>?</summary>
        public static bool SlamFits(float distance, bool seesHer, BossFinaleValues f) =>
            f != null && f.Slam != null && distance >= f.Slam.MinDistance && distance <= f.Slam.MaxDistance && (seesHer || distance <= f.SlamSightFrom);

        /// <summary>
        /// The attack for this distance: before the break the tactics' own (work order 014's choice, untouched); in phase two the
        /// slam when it is due and fits, else the tactics' choice; each attack given counts as a choice.
        /// </summary>
        public EnemyAttackDefinition Choose(BossTactics tactics, float distance, bool seesHer, EnemyValues values, BossFinaleValues f)
        {
            if (!Broken) return tactics.Choose(distance, seesHer, values);
            if (SlamDue(f) && SlamFits(distance, seesHer, f))
            {
                Choices++;
                Slams++;
                return f.Slam;
            }
            EnemyAttackDefinition a = tactics.Choose(distance, seesHer, values);
            if (a != null) Choices++;
            return a;
        }
    }

    /// <summary>The finale's names and its plain geometry (work order 106).</summary>
    public static class BossFinale
    {
        public const string Slam = "Ground Slam";

        /// <summary>
        /// Where a leap from <paramref name="from"/> at <paramref name="aim"/> lands: at the aim, unless a wall or tall prop on the
        /// line is nearer (<paramref name="wallClear"/>, the clear distance along it), then <paramref name="stopShort"/> before it,
        /// as its charge stops; never behind where it started.
        /// </summary>
        public static GroundPoint Landing(GroundPoint from, GroundPoint aim, float wallClear, float stopShort)
        {
            GroundPoint d = aim - from;
            float length = d.Length;
            if (length < 1e-4f || wallClear >= length - 1e-3f) return aim;
            return from + d * (Math.Max(0f, wallClear - stopShort) / length);
        }

        /// <summary>
        /// How far through the leap it is, 0 to 1, at <paramref name="progress"/> (0 to 1) of a windup of
        /// <paramref name="windup"/> s of which the first <paramref name="crouch"/> s are the crouch: 0 through the crouch.
        /// </summary>
        public static float LeapShare(float progress, float windup, float crouch)
        {
            float c = windup > 0f ? Clamp01(crouch / windup) : 0f;
            if (c >= 1f) return progress >= 1f ? 1f : 0f;
            return Clamp01((progress - c) / (1f - c));
        }

        /// <summary>The arc's height over the ground at that share of the leap: 0 at both ends, <paramref name="top"/> halfway.</summary>
        public static float LeapHeight(float share, float top)
        {
            float s = Clamp01(share);
            return 4f * Math.Max(0f, top) * s * (1f - s);
        }

        /// <summary>Does the ring of the landing touch her? The Spin's own test: a circle of the slam's radius round the landing.</summary>
        public static bool RingHits(GroundPoint landing, float radius, GroundPoint her, float herRadius) =>
            SkillRules.InCircle(landing, radius, her, herRadius);

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Knight's Resolve (work order 106): the named piece the first kill of the act boss opens on the rack between runs. Not sold:
    /// owned once the feat <see cref="MetaFeat.ActBoss"/> is done, as Knight's Trophy is. 2 points to equip (the producer's).
    /// Once a run, a blow that would kill her leaves her at 1 health and staggers (dazes) the enemies within 4 m of her.
    /// Only with the between-runs trial on, as everything of the rack, and only with <see cref="BossValues.Finale"/>: off, the
    /// rack is the ten pieces as before.
    /// </summary>
    public static class KnightsResolve
    {
        public const string Id = "knights-resolve";
        public const string Name = "Knight's Resolve";
        /// <summary>Points to equip (the producer's start).</summary>
        public const int Points = 2;
        /// <summary>Times a run it holds (the producer's "once a run").</summary>
        public const float TimesARun = 1f;
        /// <summary>m from her within which the enemies are staggered (the producer's "within 4 m").</summary>
        public const float Reach = 4f;
        /// <summary>s the staggered enemies are dazed (the agent's first guess: a short daze, no bar emptied).</summary>
        public const float DazeSeconds = 1.5f;

        /// <summary>The piece as a line of the rack: at its end, after every piece of before.</summary>
        public static MetaPiece Piece() =>
            new MetaPiece(Id, Name, MetaPieceKind.Rule, Points, 0, MetaEffect.LastStand, TimesARun,
                          "Once a run, a blow that would kill her leaves her at 1 health and staggers the enemies within 4 m.")
            { Sold = false, FromFeat = MetaFeat.ActBoss, Amount2 = Reach };

        /// <summary>
        /// The rack's values with Knight's Resolve at the end of its pieces when the finale is on (and it is not there yet);
        /// as they were with it off. The same object, for a field initialiser.
        /// </summary>
        public static MetaValues WithPiece(MetaValues v)
        {
            if (v == null || !BossValues.Finale || MetaRack.Find(v, Id) != null) return v;
            MetaPiece[] had = v.Pieces ?? new MetaPiece[0];
            var pieces = new MetaPiece[had.Length + 1];
            Array.Copy(had, pieces, had.Length);
            pieces[had.Length] = Piece();
            v.Pieces = pieces;
            return v;
        }

        /// <summary>
        /// A blow of <paramref name="damage"/> on her <paramref name="current"/> health: when it would kill her and the run still
        /// has a resolve left, one is spent and the damage is cut so that she keeps 1 health (<paramref name="spared"/> true); else
        /// the damage as it is. No meta (outside a run, or the trial off): as it is.
        /// </summary>
        public static int Spare(int damage, int current, RunMetaEffects meta, out bool spared)
        {
            spared = false;
            if (meta == null || meta.ResolvesLeft <= 0 || current <= 0 || damage < current) return damage;
            meta.ResolvesLeft--;
            spared = true;
            return current - 1;
        }

        /// <summary>
        /// The end screen's line for a kill of the act boss after the first (work order 106): "Knight's Resolve is already yours." Null
        /// when the boss was not killed, when this run's kill was the first (the screen's "New piece" line says what it opened), or
        /// when the rack has no such piece (the finale off).
        /// </summary>
        public static string EndLine(RunEarnings run, MetaValues v)
        {
            if (run == null || !run.BossKilled || !run.Committed || run.NewFeats.Contains(MetaFeat.ActBoss)) return null;
            MetaPiece p = MetaRack.Find(v, Id);
            return p == null ? null : p.Name + " is already yours.";
        }
    }

    /// <summary>
    /// The recorder's line of a boss fight (work order 106), told by the boss once as its fight ends (it died, she died, or it was
    /// taken away awake), only with the finale on: one (s from its waking to the break, or to the end without one), two (s from the
    /// break to the end; 0 without one), slams, landed (on her), avoided (out of the ring or in the dash), ended (died, her death,
    /// removed).
    /// </summary>
    public static class BossFightEvents
    {
        public const string Fight = "boss_fight";
        public const string Died = "died";
        public const string HerDeath = "her death";
        public const string Removed = "removed";

        public static string Seconds(float s) => Math.Max(0f, s).ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>The recorder's line when Knight's Resolve keeps her alive (work order 106): blow (the damage it cut), dazed (the enemies it staggered).</summary>
    public static class KnightsResolveEvents
    {
        public const string Spent = "knights_resolve";
    }
}
