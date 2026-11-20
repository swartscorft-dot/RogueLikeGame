using System;

namespace Roguelike.Rules
{
    /// <summary>One animation take of an enemy's model (work order 011), as it was made: 30 frames a second.</summary>
    public sealed class EnemyClipSpec
    {
        public string Name;
        public double Length;          // s
        public bool Loop;
        public double Strike = -1;     // s into the clip where the blow lands or the arrow leaves; under 0 for none

        public EnemyClipSpec(string name, double length, bool loop, double strike = -1)
        {
            Name = name;
            Length = length;
            Loop = loop;
            Strike = strike;
        }
    }

    /// <summary>
    /// The model of each kind of enemy (work order 011): the file under Resources, the height it was made to and is
    /// shown at, its clips, and which clip plays for which attack. The stride speeds are those of the models as
    /// imported, measured from the feet by the import test (each foot sweeps a step back under the hips, two steps a
    /// cycle), rounded; the look scales them with the model.
    /// </summary>
    public sealed class EnemyModelSpec
    {
        public const double FramesPerSecond = 30;
        public const double FadeSeconds = 0.15;      // s a clip blends into the next
        public const double MinAttackSpeed = 0.5;    // the attack clips play no slower than half speed
        public const double MaxAttackSpeed = 2;      // nor faster than double
        /// <summary>
        /// An ordinary attack's clip (work order 035: a tell of 0.15 to 0.5 s, the animation the warning) may play up to
        /// four times as fast, so the whole clip plays for every blow instead of only its last part.
        /// </summary>
        public const double OrdinaryMaxAttackSpeed = 4;

        public EnemyKind Kind;
        public string Name;
        public double Height;                        // m, the top of the head standing
        public string Walk = "Walk";
        public double WalkStride;                    // m/s the walk clip covers at its own speed, the model as imported
        public double RunStride;                     // m/s the run clip covers at its own speed, the model as imported
        public bool HasStagger;
        /// <summary>
        /// Share of its height its lowest point may lie off the floor before the model is lifted or lowered to it (work
        /// order 014: the knight's cape hangs under its feet, which stand on the floor).
        /// </summary>
        public double FloorShare = 0.05;
        /// <summary>
        /// Degrees to the left of where the body faces that the attack clip's shot leaves at its release, as measured in
        /// Blender: the archer's Shoot fires along its left arm. The body is turned to the right while it plays, by the
        /// angle the look measures on the clip, or by this when it cannot.
        /// </summary>
        public double ShotLeftOfFacing;
        /// <summary>The attack's name in the game, then the clip that plays for it.</summary>
        public string[] AttackClips = new string[0];
        public EnemyClipSpec[] Clips = new EnemyClipSpec[0];

        /// <summary>
        /// Work order 027: over 0, its "_emission" map goes on its material in the game's cyan times this, as the crypt
        /// kit's (<see cref="CryptKitValues.StoneGlow"/>); 0, no glow.
        /// </summary>
        public double Glow;

        public string ResourcePath => "Enemies/" + Name + "/" + Name;
        /// <summary>The base colour texture of a mesh in the model's folder, by the mesh's name.</summary>
        public string TexturePath(string mesh) => "Enemies/" + Name + "/" + mesh + "_base_color";
        /// <summary>The emission map beside the model (work order 027), by the model's name.</summary>
        public string EmissionPath => "Enemies/" + Name + "/" + Name + "_emission";

        public EnemyClipSpec Clip(string name)
        {
            foreach (EnemyClipSpec c in Clips)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        /// <summary>The clip that plays for an attack of this name, or null when none does.</summary>
        public EnemyClipSpec ClipForAttack(string attack)
        {
            for (int i = 0; i + 1 < AttackClips.Length; i += 2)
                if (string.Equals(AttackClips[i], attack, StringComparison.OrdinalIgnoreCase)) return Clip(AttackClips[i + 1]);
            return null;
        }

        static EnemyClipSpec Idle => new EnemyClipSpec("Idle", 4.000, true);
        static EnemyClipSpec WalkClip => new EnemyClipSpec("Walk", 1.033, true);
        static EnemyClipSpec RunClip => new EnemyClipSpec("Run", 0.633, true);
        static EnemyClipSpec Death => new EnemyClipSpec("Death", 2.967, false);

        public static readonly EnemyModelSpec Swarmer = new EnemyModelSpec
        {
            Kind = EnemyKind.Swarmer, Name = "Crypt_Ghoul_01", Height = 1.1, Walk = "CrouchWalk",
            WalkStride = 0.77, RunStride = 1.77,
            // Work order 035: the Bite and the Leap play its one attack clip, at the speed each needs.
            AttackClips = new[] { "Lunge", "Attack", MeleeAttacks.Bite, "Attack", MeleeAttacks.Leap, "Attack" },
            Clips = new[]
            {
                Idle, new EnemyClipSpec("CrouchWalk", 1.133, true), WalkClip, RunClip,
                new EnemyClipSpec("Attack", 2.467, false, 1.233), Death,
            },
        };

        public static readonly EnemyModelSpec Shooter = new EnemyModelSpec
        {
            Kind = EnemyKind.Shooter, Name = "Crypt_SkeletonArcher_01", Height = 1.9,
            WalkStride = 1.67, RunStride = 3.08,
            ShotLeftOfFacing = 98,
            AttackClips = new[] { "Shot", "Shoot" },
            Clips = new[]
            {
                Idle, WalkClip, RunClip, new EnemyClipSpec("Shoot", 2.833, false, 2.333),
                new EnemyClipSpec("ShootFull", 5.000, false), Death,
            },
        };

        public static readonly EnemyModelSpec Brute = new EnemyModelSpec
        {
            Kind = EnemyKind.Brute, Name = "Crypt_SkeletonWarrior_01", Height = 1.8,
            WalkStride = 1.63, RunStride = 3.0,
            // Work order 035: the Stroke plays the sword's Attack, the Shield bash the Slam.
            AttackClips = new[] { "Slam", "Slam", "Shockwave", "Attack", MeleeAttacks.Stroke, "Attack", MeleeAttacks.ShieldBash, "Slam" },
            Clips = new[]
            {
                Idle, WalkClip, RunClip, new EnemyClipSpec("Attack", 2.800, false, 1.067),
                new EnemyClipSpec("Slam", 3.000, false, 1.667), Death,
            },
        };

        public static readonly EnemyModelSpec Guard = new EnemyModelSpec
        {
            Kind = EnemyKind.Guard, Name = "Crypt_BoneBrute_01", Height = 2.6, HasStagger = true,
            WalkStride = 2.14, RunStride = 3.91,
            // Work order 035: the Fist and the Drag play the one-armed Sweep, the Crush the two-armed Slam.
            AttackClips = new[] { "Slam", "Slam", "Sweep", "Sweep", MeleeAttacks.Fist, "Sweep", MeleeAttacks.Drag, "Sweep", MeleeAttacks.Crush, "Slam" },
            Clips = new[]
            {
                Idle, WalkClip, RunClip, new EnemyClipSpec("Slam", 3.000, false, 1.633),
                new EnemyClipSpec("Sweep", 3.467, false, 1.467), new EnemyClipSpec("Stagger", 2.833, false), Death,
            },
        };

        /// <summary>
        /// The act boss (work order 014, 2026-11-20): the horned knight with its cleaver on a pole, a separate mesh on the
        /// right hand. Its Slam is in the file and not played: the cleaver points straight up in it. The charge has no
        /// clip of its own: it stands in Idle through the windup and runs in Run.
        /// </summary>
        public static readonly EnemyModelSpec Boss = new EnemyModelSpec
        {
            // As Unity imports it (the Blender report said 3.0 m to the helmet, takes a frame longer): 3.317 m as bound,
            // the horns' tips; the cape hangs 0.27 m under the feet in the file's pose, so it is not lifted for that.
            Kind = EnemyKind.Boss, Name = "Crypt_BossKnight_01", Height = 3.317, HasStagger = true, FloorShare = 0.1,
            WalkStride = 2.97, RunStride = 5.47,
            AttackClips = new[] { BossValues.Cleave, "Attack", BossValues.Spin, "Spin" },
            Clips = new[]
            {
                Idle, WalkClip, RunClip,
                new EnemyClipSpec("Attack", 2.800, false, 1.067), new EnemyClipSpec("Spin", 2.500, false, 0.967),
                new EnemyClipSpec("Stagger", 2.833, false), Death,
            },
        };

        /// <summary>
        /// The caster (work order 027, 2026-11-20): the hooded skeleton in a tattered robe, its eyes, palms and amulet
        /// glowing in the game's cyan. Neither cast fires along the body (the quick one throws the right hand up and out,
        /// the big one both hands overhead): the spell appears on the ground where it is aimed, so nothing is turned. Each
        /// cast's release lands on the game's strike: Bone Spikes' burst, and Grave Rain's first burst.
        /// </summary>
        public static readonly EnemyModelSpec Caster = new EnemyModelSpec
        {
            // As Unity imports it: 1.9 m as bound, 1.83 m standing in the file's pose, 24 bones, the takes as the Blender
            // report said; the strides measured from the feet, 1.79 and 3.38 m/s (the report: about 1.7 and 5.7).
            Kind = EnemyKind.Caster, Name = "Crypt_BoneCaster_01", Height = 1.9, HasStagger = true, Glow = 3,
            WalkStride = 1.79, RunStride = 3.38,
            AttackClips = new[] { CasterValues.BoneSpikes, "Cast", CasterValues.GraveRain, "CastBig" },
            Clips = new[]
            {
                new EnemyClipSpec("Idle", 3.967, true), new EnemyClipSpec("Walk", 1.000, true), new EnemyClipSpec("Run", 0.600, true),
                new EnemyClipSpec("Cast", 1.967, false, 0.600), new EnemyClipSpec("CastBig", 2.667, false, 1.733),
                new EnemyClipSpec("Stagger", 1.500, false), new EnemyClipSpec("Death", 1.533, false),
            },
        };

        public static readonly EnemyModelSpec[] All = { Brute, Swarmer, Shooter, Guard, Boss, Caster };

        public static EnemyModelSpec Of(EnemyKind kind) =>
            kind == EnemyKind.Swarmer ? Swarmer : kind == EnemyKind.Shooter ? Shooter : kind == EnemyKind.Guard ? Guard
            : kind == EnemyKind.Boss ? Boss : kind == EnemyKind.Caster ? Caster : Brute;

        /// <summary>
        /// The clip's own name in a take name as an exporter writes it: "Armature|Idle" is "Idle". The part after the
        /// last '|', ':', '/', '.' or '_'.
        /// </summary>
        public static string TakeName(string take)
        {
            if (take == null) return "";
            int cut = take.LastIndexOfAny(new[] { '|', ':', '/', '.', '_' });
            return cut < 0 ? take : take.Substring(cut + 1);
        }

        /// <summary>Is this take the clip of that name? Matched by the end of the take's name, the whole last part.</summary>
        public static bool IsTake(string take, string clip) => string.Equals(TakeName(take), clip, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// How an attack clip is played so that its strike lands when the game's strike does (work order 011). The game's
    /// attack is a windup (the telegraph), the strike, then a recovery; the clip has its own strike time. Through the
    /// windup the clip plays from <see cref="Start"/> at <see cref="WindupSpeed"/>, so it reaches its strike exactly
    /// as the windup ends; the speed is kept between half and double, and the clip starts part-way in when even
    /// double speed would be too slow, or holds its first frame for <see cref="Hold"/> when even half speed would be
    /// too fast. After the strike the rest plays at <see cref="RecoverySpeed"/>, which ends it with the recovery when
    /// that is within the same bounds.
    /// </summary>
    public struct AttackClipPlan
    {
        public double Start;          // s into the clip at the start of the windup
        public double Hold;           // s at the start of the windup that the clip stays on Start
        public double WindupSpeed;
        public double RecoverySpeed;
        public double Strike;         // s into the clip of its strike
        public double Length;         // s, the clip

        public static AttackClipPlan For(double windup, double recovery, double strike, double length,
                                         double minSpeed = EnemyModelSpec.MinAttackSpeed, double maxSpeed = EnemyModelSpec.MaxAttackSpeed)
        {
            var plan = new AttackClipPlan { Strike = strike, Length = length };
            if (windup <= 0)
            {
                plan.Start = strike;
                plan.WindupSpeed = 1;
            }
            else
            {
                plan.WindupSpeed = Clamp(strike / windup, minSpeed, maxSpeed);
                double covered = windup * plan.WindupSpeed;
                plan.Start = Math.Max(0, strike - covered);
                // Even at the slowest it would get there early: it waits on its first frame for the difference.
                plan.Hold = covered > strike ? windup - strike / plan.WindupSpeed : 0;
            }
            double rest = length - strike;
            plan.RecoverySpeed = recovery <= 0 ? maxSpeed : Clamp(rest / recovery, minSpeed, maxSpeed);
            return plan;
        }

        /// <summary>Seconds into the clip, <paramref name="sinceWindup"/> seconds after the windup began (up to the strike).</summary>
        public double InWindup(double sinceWindup)
        {
            if (sinceWindup <= Hold) return Start;
            double t = Start + (sinceWindup - Hold) * WindupSpeed;
            return t > Strike ? Strike : t;
        }

        /// <summary>Seconds into the clip, <paramref name="sinceStrike"/> seconds after the strike. It stays on its last frame once it ends.</summary>
        public double AfterStrike(double sinceStrike)
        {
            double t = Strike + Math.Max(0, sinceStrike) * RecoverySpeed;
            return t > Length ? Length : t;
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }

    public enum EnemyGait
    {
        Stand,
        Walk,
        Run,
    }

    /// <summary>
    /// Which clip an enemy's legs play for how fast it goes over the ground, and how fast it plays (work order 011):
    /// standing under a slow shuffle, the walk up to 1.7 times its stride speed, the run above; each at the speed that
    /// matches its stride to the ground speed, between <see cref="MinRate"/> and <see cref="MaxRate"/> (three times,
    /// for the swarmer's short legs at its speed). A little overlap at each change keeps it from flickering between two.
    /// </summary>
    public static class EnemyGaits
    {
        public const double StandBelow = 0.15;     // m/s: under this it stands, once it is standing...
        public const double MoveAbove = 0.3;       // m/s: ...and over this it moves
        public const double RunOver = 1.7;         // times the walk's stride speed: over this it runs...
        public const double WalkUnder = 1.5;       // ...and under this, running, it walks again
        public const double MinRate = 0.5;
        public const double MaxRate = 3;

        public static EnemyGait For(double speed, EnemyGait current, double walkStride, double runStride)
        {
            if (current == EnemyGait.Stand ? speed <= MoveAbove : speed < StandBelow) return EnemyGait.Stand;
            if (runStride <= 0) return EnemyGait.Walk;
            double runFrom = walkStride * (current == EnemyGait.Run ? WalkUnder : RunOver);
            return speed > runFrom ? EnemyGait.Run : EnemyGait.Walk;
        }

        /// <summary>How fast the clip plays so its stride covers <paramref name="speed"/>.</summary>
        public static double Rate(double speed, double stride)
        {
            if (stride <= 0) return 1;
            double r = speed / stride;
            return r < MinRate ? MinRate : r > MaxRate ? MaxRate : r;
        }
    }
}
