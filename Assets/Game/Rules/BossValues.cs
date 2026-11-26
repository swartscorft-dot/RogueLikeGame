using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The act boss's numbers (work order 014, 2026-11-20, first guesses): the crypt knight, 3 m tall, with three attacks
    /// that each wind up with a telegraph on the ground and leave it standing after the strike. The cleave is a wide cone
    /// in front of it and the spin a circle round it, alternating when she is close; the charge is a line toward where
    /// she stood when it began, run down at <see cref="EnemyAttackDefinition.Rush"/>, when she is farther and it sees
    /// her. Below <see cref="AngerBelow"/> of its health it is angry: shorter windups, and a charge is followed by a
    /// second. At each share of <see cref="CallsAt"/> it calls a pack of swarmers, once each.
    /// </summary>
    [Serializable]
    public sealed class BossValues
    {
        public const string Cleave = "Cleave";
        public const string Spin = "Spin";
        public const string Charge = "Charge";

        public string Title = "The Crypt Knight";  // over its bar on screen: a placeholder
        public EnemyValues Enemy = new EnemyValues
        {
            // AggroRange: it also wakes as she enters its room, wherever it is; LeashRange: it never gives up.
            Health = 18000, MoveSpeed = 3f, AggroRange = 9f, LeashRange = 1000f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    Name = Cleave, Shape = EnemyAttackShape.Cone, MinDistance = 0f, MaxDistance = 5f,
                    Windup = 1.1f, Recovery = 1.3f, Damage = 40, Length = 6.5f, Angle = 110f,
                },
                new EnemyAttackDefinition
                {
                    Name = Spin, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 4f,
                    Windup = 1.0f, Recovery = 1.4f, Damage = 35, Radius = 5.5f,
                },
                new EnemyAttackDefinition
                {
                    // Recovery: from where the run stops.
                    Name = Charge, Shape = EnemyAttackShape.Line, MinDistance = 7f, MaxDistance = 16f,
                    Windup = 0.9f, Recovery = 1.6f, Damage = 45, Length = 14f, HalfWidth = 1.5f, Rush = 12f,
                    // The producer, 2026-11-20, when work orders 014 and 015 met: the charge is the boss's blue attack.
                    // Stopping Shot from its front during the wind-up drops it and dazes the boss (pillar 4: counter).
                    Counterable = true,
                },
            },
        };
        /// <summary>Half again the guard's bar; it refills as the guard's does, full when the daze ends.</summary>
        public StaggerValues Stagger = new StaggerValues { Max = 150f };
        public float BodyRadius = 1.2f;         // m, the capsule it is hit as and walks as
        public float BodyHeight = 3f;           // m
        public float SightHeight = 0.5f;        // m over the ground of the line it sees her along for a charge, as the shooter's
        /// <summary>
        /// m a charge stops short of the first wall or edge of the walkable ground on its line: its body is wider than
        /// the agent the NavMesh is built for (0.5 m), so its middle stops this much sooner and the body stays out.
        /// </summary>
        public float ChargeStopShort = 0.7f;
        public float AngerBelow = 0.5f;         // share of its health under which it is angry
        public float AngerWindup = 0.8f;        // angry, every windup is this many times as long
        public int AngryCharges = 2;            // angry, a charge comes as this many, one after the other
        public float[] CallsAt = { 0.66f, 0.33f }; // shares of its health at which it calls a pack, once each
        public int CallCount = 6;               // swarmers a call brings
        public float CalledLeash = 1000f;       // m: the called swarmers come for her wherever she is on the floor
        public float CallNearRadius = 4f;       // m from it that a pack it calls in the practice room stands
        public float PracticeHealthShare = 1f / 3f; // the boss spawned in the practice room (F8) has this share of its health

        // ---------------- Things that fly (work order 036, 2026-11-20; decision 0010; first guesses) ----------------

        public const string Wave = "Cleave Wave";

        /// <summary>
        /// With things that fly on, its Cleave, when it lands, also sends a wave straight ahead along the ground: half the
        /// Cleave's damage, its path not drawn on the floor. She sidesteps it or dashes through it; walls stop it, props do
        /// not. A swing whose Cleave hit her does not hurt her again by its wave.
        /// </summary>
        public EnemyAttackDefinition WaveAttack = new EnemyAttackDefinition
        {
            Name = Wave, Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = 5f,
            Windup = 0f, Recovery = 0f, Damage = 20, Length = 14f, HalfWidth = 3f,
        };
        public float WaveWidth = 6f;            // m from edge to edge
        public float WaveDepth = 0.3f;          // m from its middle line to its front and its back
        public float WaveSpeed = 10f;           // m/s
        public float WaveFlight = 14f;          // m, how far it goes

        /// <summary>
        /// The values it fights with angry: the same, with every windup <see cref="AngerWindup"/> times as long. A copy;
        /// these values are left as they are.
        /// </summary>
        public EnemyValues Angry()
        {
            var attacks = new EnemyAttackDefinition[Enemy.Attacks?.Length ?? 0];
            for (int i = 0; i < attacks.Length; i++)
            {
                attacks[i] = Enemy.Attacks[i]?.Copy();
                if (attacks[i] != null) attacks[i].Windup *= AngerWindup;
            }
            return new EnemyValues
            {
                Health = Enemy.Health, MoveSpeed = Enemy.MoveSpeed, AggroRange = Enemy.AggroRange, LeashRange = Enemy.LeashRange,
                RespawnDelay = Enemy.RespawnDelay, UseModels = Enemy.UseModels, Attacks = attacks,
            };
        }

        // ---------------- The finale (work order 106, 2026-11-24; BossFinale.cs) ----------------

        /// <summary>
        /// The finale's switch (work order 106), on. Kristian, 2026-11-24, ticked "Fiender og boss" and, asked by name, said yes
        /// to the producer's "Bossens fase to og belønning": "Et tydelig brudd ved 50 % helse, ett nytt angrep i fase to, og
        /// første drap åpner en navngitt del på stativet mellom løp." On: at half its health the fight breaks once (a roar, the
        /// room's lamps, the bar's two halves), phase two adds the Ground Slam, and the rack between runs has Knight's Resolve,
        /// opened by the first kill. Off: the fight, the bar, the room and the rack are as before, field for field. One switch
        /// for the whole game (the rack reads it too), so it is static; tests set it and put it back.
        /// </summary>
        public static bool Finale = true;

        /// <summary>
        /// Work order 140's first part, ON by default. Kristian, 2026-11-26, asked whether the boss, alone on floor 5 since work order
        /// 130, should still call its own swarmers (a pack of six at two thirds of its health and another at one third), ticked
        /// "Ingen flokker": on, it calls nobody through the whole fight, at neither share of <see cref="CallsAt"/> (no pack, no call's
        /// ring or sound, nothing told to the log); its anger, its break and its attacks are as before. Off: it calls its packs as before,
        /// field for field. One switch for the whole game (the practice room's boss too), so it is static; tests set it and put it back.
        /// </summary>
        public static bool NoPacks = true;
        /// <summary>The finale's numbers (work order 106): the roar, the lamps' colour, the Ground Slam.</summary>
        public BossFinaleValues Phases = new BossFinaleValues();
    }

    /// <summary>
    /// What the boss decides, with no engine in it (work order 014): which attack for a distance, when it turns angry,
    /// when it calls a pack, and how far a charge goes. The rest (walking, the telegraph, the strike) is
    /// <see cref="EnemyBrain"/>'s and the caller's.
    /// </summary>
    public sealed class BossTactics
    {
        string _lastClose;
        int _calls;

        /// <summary>True once its health has fallen under <see cref="BossValues.AngerBelow"/>; it stays so.</summary>
        public bool Angry { get; private set; }
        /// <summary>The packs called so far.</summary>
        public int Calls => _calls;
        /// <summary>The name of the last attack chosen that strikes where it stands, or null.</summary>
        public string LastClose => _lastClose;

        /// <summary>
        /// The attack for this distance, remembering it (<see cref="Choose(float, bool, string, EnemyAttackDefinition[])"/>).
        /// </summary>
        public EnemyAttackDefinition Choose(float distance, bool seesHer, EnemyValues values)
        {
            EnemyAttackDefinition a = Choose(distance, seesHer, _lastClose, values?.Attacks);
            if (a != null && a.Rush <= 0f) _lastClose = a.Name;
            return a;
        }

        /// <summary>
        /// The attack for this distance: of those that strike where it stands (no <see cref="EnemyAttackDefinition.Rush"/>)
        /// and whose band holds her, the one alone, or with more than one the first that is not
        /// <paramref name="lastClose"/>, so the cleave and the spin take turns when both reach; else a rushing attack whose
        /// band holds her, if it sees her; else none, and it walks toward her.
        /// </summary>
        public static EnemyAttackDefinition Choose(float distance, bool seesHer, string lastClose, EnemyAttackDefinition[] attacks)
        {
            if (attacks == null) return null;
            EnemyAttackDefinition first = null, other = null;
            foreach (EnemyAttackDefinition a in attacks)
            {
                if (a == null || a.Rush > 0f || distance < a.MinDistance || distance > a.MaxDistance) continue;
                if (first == null) first = a;
                if (other == null && a.Name != lastClose) other = a;
            }
            if (first != null) return other ?? first;
            if (!seesHer) return null;
            foreach (EnemyAttackDefinition a in attacks)
                if (a != null && a.Rush > 0f && distance >= a.MinDistance && distance <= a.MaxDistance) return a;
            return null;
        }

        /// <summary>True on the one call in which its health share first falls under <see cref="BossValues.AngerBelow"/>.</summary>
        public bool BecomesAngry(float healthShare, BossValues v)
        {
            if (Angry || healthShare >= v.AngerBelow) return false;
            Angry = true;
            return true;
        }

        /// <summary>
        /// How many packs it calls now: one for each share of <see cref="BossValues.CallsAt"/> (highest first) that its
        /// health has come down to since the last call, each once. A hit that takes it past both calls both. None, ever, with
        /// <see cref="BossValues.NoPacks"/> on (work order 140).
        /// </summary>
        public int CallsDue(float healthShare, BossValues v)
        {
            if (BossValues.NoPacks) return 0; // work order 140: "Ingen flokker"
            int due = 0;
            float[] at = v.CallsAt ?? Array.Empty<float>();
            while (_calls < at.Length && healthShare <= at[_calls])
            {
                _calls++;
                due++;
            }
            return due;
        }

        /// <summary>
        /// How far a charge runs (work order 014): its length, cut at the first wall or tall prop and at the end of the
        /// walkable ground on its line, whichever is nearer, less <paramref name="stopShort"/> when it is cut; never
        /// below 0.
        /// </summary>
        public static float ChargeLength(float length, float wallDistance, float walkableDistance, float stopShort)
        {
            float clear = Math.Min(wallDistance, walkableDistance);
            if (clear >= length) return length;
            return Math.Max(0f, clear - stopShort);
        }

        /// <summary>
        /// Does a rush from <paramref name="from"/> to <paramref name="to"/>, a strip <paramref name="halfWidth"/> to each
        /// side, pass over the target? The charge asks it of each stretch it runs.
        /// </summary>
        public static bool RushPasses(GroundPoint from, GroundPoint to, float halfWidth, GroundPoint target, float targetRadius)
        {
            float length = from.DistanceTo(to);
            if (length < 1e-5f) return from.DistanceTo(target) <= halfWidth + targetRadius;
            return LineShot.Hits(from, to, length, halfWidth, target, targetRadius);
        }

        /// <summary>
        /// The places a called pack may stand in a room (work order 014): points along its walls, <paramref name="inset"/>
        /// in from them, every <paramref name="step"/>, that keep the level's rules for an enemy's place
        /// (<see cref="LevelGeometry.EnemyPlaceFault"/>), in order round the room.
        /// </summary>
        public static List<LevelPoint> WallPlaces(LevelLayout level, LevelRoom room, LevelValues v, double inset, double step)
        {
            var places = new List<LevelPoint>();
            double cs = level.CellSize;
            double x0 = room.Rect.X * cs + inset, x1 = room.Rect.Right * cs - inset;
            double y0 = room.Rect.Y * cs + inset, y1 = room.Rect.Top * cs - inset;
            if (x1 <= x0 || y1 <= y0 || step <= 0) return places;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            var doors = new List<LevelDoor>(level.DoorsOf(room.Order));
            void Try(double x, double y)
            {
                var p = new LevelPoint(x, y);
                if (LevelGeometry.EnemyPlaceFault(level, room, p, entry, doors, v) == null) places.Add(p);
            }
            // Round the room: the south side west to east, the east side north, the north side west, the west side south.
            for (double x = x0; x < x1; x += step) Try(x, y0);
            for (double y = y0; y < y1; y += step) Try(x1, y);
            for (double x = x1; x > x0; x -= step) Try(x, y1);
            for (double y = y1; y > y0; y -= step) Try(x0, y);
            return places;
        }

        /// <summary><paramref name="count"/> of the places, evenly spread through the list (all of them when there are no more).</summary>
        public static List<T> Spread<T>(IReadOnlyList<T> places, int count)
        {
            var picked = new List<T>();
            if (places == null || places.Count == 0 || count <= 0) return picked;
            if (places.Count <= count)
            {
                picked.AddRange(places);
                return picked;
            }
            for (int i = 0; i < count; i++) picked.Add(places[(int)((long)i * places.Count / count)]);
            return picked;
        }
    }
}
