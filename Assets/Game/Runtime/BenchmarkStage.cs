using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>The benchmark's scenarios (work order 032, 2026-11-20), in the order they run.</summary>
    public enum BenchmarkScenario
    {
        /// <summary>The practice room as the scene starts, nothing spawned, nobody shooting.</summary>
        PracticeIdle = 1,
        /// <summary>Floor 1's fullest combat room, every enemy of it awake and coming for her.</summary>
        Floor1Combat = 2,
        /// <summary>Floor 5's fullest combat room, the same.</summary>
        Floor5Combat = 3,
        /// <summary>Floor 5's exit room: the boss awake, the room's enemies awake, and both of the boss's packs called.</summary>
        Floor5Boss = 4,
    }

    /// <summary>
    /// Sets up one scenario of the benchmark (work order 032, 2026-11-20) and keeps it so while it is measured, from the
    /// benchmark's own code: no switch is added to the game for it, only what the game exposes is used (the run's
    /// intents, the level's rooms and enemies, the enemies' own <see cref="Enemy.Provoke"/>, the input driver's
    /// <see cref="PlayerInputDriver.InputSource"/>), and where it must reach in, it reaches in from here.
    ///
    /// Every scenario is the same each time: the floors are those of <see cref="RunSeed"/> (floor n's seed is
    /// <see cref="RunSeeds.FloorSeed"/> of it and n), built alone outside a run with <see cref="GeneratedLevels.Build"/>
    /// (so no reward, chest or heal comes up) and with the line-ups (work order 135) and the waves (work order 136) off, so
    /// their rooms are version 24's and wake whole (the merge of 2026-11-27, W2), with no caller (137) and today's elite in place of
    /// the Grave Ram (149; the merge of 2026-11-27, W3), and the room is the combat room with the most enemies (the first of them
    /// in the floor's order when two hold as many; not the elite room), or floor 5's exit room. She stands where the
    /// fight is (<see cref="StandFor"/>): the place of the room, on its NavMesh, nearest to the farthest of its enemies,
    /// and clear of each of them (the boss too), so that every one of them is within its leash and comes for her when
    /// woken (an enemy farther than its <see cref="EnemyValues.LeashRange"/> from her is not woken by
    /// <see cref="Enemy.Provoke"/>: the game's rule; on a floor in one row, work order 041, a pack of floor 5's exit room
    /// stood 22.5 m from the room's middle less 6 m, where she stood before). She cannot be hurt
    /// (<see cref="PlayerHealth.Untouchable"/>), and presses the basic shot every frame
    /// toward the nearest living enemy of the scenario, through the input driver as the mouse would: a press while she
    /// cannot shoot waits for the next shot, so she fires as fast as the shot fires. The camera is the game's own.
    ///
    /// So that the load stays the same for the whole measurement, no enemy of the floor dies in it: each is given
    /// <see cref="EnemyHealth"/> health before its first frame or as soon as it is seen (the swarmers the boss calls too).
    /// The boss is then hurt to <see cref="BossShareLeft"/> of that, below both its calls (two thirds and one third),
    /// so it calls both packs and is angry, as late in the fight. The practice room's scenario stages nothing: she
    /// stands where the scene puts her and does not shoot.
    /// </summary>
    public sealed class BenchmarkStage
    {
        /// <summary>The run seed every floor of the benchmark comes from ("BENCH032" read as a seed's text).</summary>
        public static readonly ulong RunSeed = LevelSeed.TryParse("BENCH032", out ulong s) ? s : 32UL;
        /// <summary>The health every enemy of a scenario has, so none dies while it is measured.</summary>
        public const int EnemyHealth = 100000000;
        /// <summary>The share of its health the boss is left with: under its second call at one third.</summary>
        public const float BossShareLeft = 0.3f;
        /// <summary>Metres she stands at least from the edge of any enemy's body (<see cref="Damageable.Radius"/>).</summary>
        public const float StandClear = 2.5f;
        /// <summary>Metres between the places of the room tried for her stand.</summary>
        public const float StandStep = 1f;

        public BenchmarkScenario Scenario { get; private set; }
        public PlayerCharacter Player { get; private set; }
        public PlayerHealth PlayerHealth { get; private set; }
        public GeneratedLevel Level { get; private set; }
        public LevelRoom Room { get; private set; }
        public Vector3 Stand { get; private set; }
        public EnemyBoss Boss { get; private set; }
        /// <summary>True once the boss has been hurt to <see cref="BossShareLeft"/>.</summary>
        public bool BossHurt { get; private set; }
        /// <summary>Does she shoot in this scenario?</summary>
        public bool Shoots => Scenario != BenchmarkScenario.PracticeIdle;

        readonly List<Enemy> _fighting = new List<Enemy>();
        readonly HashSet<Enemy> _strong = new HashSet<Enemy>();
        int _begunFrame;

        /// <summary>The enemies she fights: the room's own, and in the boss's room the swarmers it has called.</summary>
        public IReadOnlyList<Enemy> Fighting
        {
            get
            {
                _fighting.Clear();
                if (Level == null || Room == null) return _fighting;
                if (Level.EnemiesByRoom.TryGetValue(Room.Order, out List<Enemy> own)) _fighting.AddRange(own);
                if (Level.CalledByRoom.TryGetValue(Room.Order, out List<Enemy> called)) _fighting.AddRange(called);
                return _fighting;
            }
        }

        /// <summary>
        /// Sets the scenario up: builds its floor (removing the one before) and puts her in its room, or takes her back
        /// to the practice room. Null without a character in the scene.
        /// </summary>
        public static BenchmarkStage Begin(BenchmarkScenario scenario)
        {
            PlayerCharacter player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (player == null) return null;
            var stage = new BenchmarkStage { Scenario = scenario, Player = player, PlayerHealth = player.GetComponent<PlayerHealth>(), _begunFrame = Time.frameCount };
            GeneratedLevels levels = GeneratedLevels.Ensure();
            if (stage.PlayerHealth != null) stage.PlayerHealth.RestoreFull();

            if (scenario == BenchmarkScenario.PracticeIdle)
            {
                if (levels.IsInLevel) levels.BackToPracticeRoom();
                stage.Stand = player.Position;
            }
            else
            {
                int floor = scenario == BenchmarkScenario.Floor1Combat ? 1 : 5;
                // Work order 136: the scenario's room wakes whole, as before the waves, so its load is the same from build to build.
                // The merge of 2026-11-27 (W2), for the same reason: its floors are built with work order 135's line-ups off, version 24's
                // rooms field for field, so the fullest room and its enemies are those every benchmark before measured.
                bool waves = levels.Values != null && levels.Values.Waves;
                bool lineUps = levels.Values != null && levels.Values.LineUps;
                bool callers = levels.Values != null && levels.Values.Callers; // work order 137: nor a caller, for the same reason
                // The merge of 2026-11-27 (W3), for the same reason: floor 1's elite room holds today's elite and its others, not work order
                // 149's Grave Ram alone (read as the floor is built; with nobody at the keys, as here, its switch's own value). Work order
                // 147's trial boss needs nothing here: the trial fight is in use only for a player at the keys (BossTrialSettings.InUse), and
                // the benchmark plays with nobody at them, so floor 5's boss is 146's, as every benchmark before measured.
                bool unique = EliteValues.Unique;
                EliteValues.Unique = false;
                bool timedChests = LevelValues.TimedChests; // work order 153: nor a timed chest, for the same reason
                LevelValues.TimedChests = false;
                if (levels.Values != null)
                {
                    levels.Values.Waves = false;
                    levels.Values.LineUps = false;
                    levels.Values.Callers = false;
                }
                try
                {
                    stage.Level = levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                }
                finally
                {
                    if (levels.Values != null)
                    {
                        levels.Values.Waves = waves;
                        levels.Values.LineUps = lineUps;
                        levels.Values.Callers = callers;
                    }
                    EliteValues.Unique = unique;
                    LevelValues.TimedChests = timedChests;
                }
                if (stage.Level == null) return stage;
                stage.Room = scenario == BenchmarkScenario.Floor5Boss ? stage.Level.Layout.ExitRoom : FullestCombatRoom(stage.Level);
                stage.Stand = stage.Room != null ? StandFor(stage.Level, stage.Room) : OnNavMesh(stage.Level.PlayerStart);
                player.Teleport(stage.Stand);
                stage.Boss = scenario == BenchmarkScenario.Floor5Boss ? stage.Level.Boss as EnemyBoss : null;
            }

            PlayerInputDriver driver = player.GetComponent<PlayerInputDriver>();
            if (driver != null) driver.InputSource = stage.Input;
            stage.Keep();
            return stage;
        }

        /// <summary>The combat room (not the elite room) with the most enemies; the first in the floor's order of those.</summary>
        public static LevelRoom FullestCombatRoom(GeneratedLevel level)
        {
            LevelRoom best = null;
            int most = -1;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if (room.Kind != RoomKind.Combat || room.Elite) continue;
                int count = level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count : 0;
                if (count > most)
                {
                    most = count;
                    best = room;
                }
            }
            return best;
        }

        /// <summary>
        /// Where she stands in a room (work order 032, as amended for the floor in one row of work order 041): of the
        /// places of the room <see cref="StandStep"/> apart, those on its NavMesh, in the room and at least
        /// <see cref="StandClear"/> from every enemy's body, the one whose farthest enemy of the room is nearest (the first
        /// of them, west to east and south to north). The room's middle, on the NavMesh, when none fits or it has no enemy.
        /// </summary>
        public static Vector3 StandFor(GeneratedLevel level, LevelRoom room)
        {
            Vector3 middle = OnNavMesh(level.Middle(room));
            if (!level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> enemies) || enemies.Count == 0) return middle;
            double cs = level.Layout.CellSize;
            Vector3 lo = level.ToWorld(new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs));
            Vector3 hi = level.ToWorld(new LevelPoint(room.Rect.Right * cs, room.Rect.Top * cs));
            Vector3 best = middle;
            float bestFarthest = float.PositiveInfinity;
            for (float z = lo.z + StandStep; z < hi.z; z += StandStep)
            {
                for (float x = lo.x + StandStep; x < hi.x; x += StandStep)
                {
                    var p = new Vector3(x, 0f, z);
                    if (level.RoomAt(p) != room) continue;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    float farthest = 0f;
                    bool clear = true;
                    foreach (Enemy e in enemies)
                    {
                        if (e == null) continue;
                        Vector3 d = e.transform.position - p;
                        float flat = new Vector2(d.x, d.z).magnitude;
                        float body = e.Body != null ? e.Body.Radius : 0.5f;
                        if (flat < body + StandClear) { clear = false; break; }
                        farthest = Mathf.Max(farthest, flat);
                    }
                    if (!clear || farthest >= bestFarthest) continue;
                    bestFarthest = farthest;
                    best = new Vector3(hit.position.x, 0f, hit.position.z);
                }
            }
            return best;
        }

        static Vector3 OnNavMesh(Vector3 p) =>
            NavMesh.SamplePosition(new Vector3(p.x, 0f, p.z), out NavMeshHit hit, 6f, NavMesh.AllAreas) ? new Vector3(hit.position.x, 0f, hit.position.z) : p;

        /// <summary>
        /// Keeps the scenario as it is meant to be, once a frame after every Update: she cannot be hurt, every enemy of the
        /// floor has <see cref="EnemyHealth"/> (one made this frame, as a called swarmer, before its first frame), the enemies
        /// she fights are awake, and the boss, once it has had its first frame, is hurt to <see cref="BossShareLeft"/>.
        /// </summary>
        public void Keep()
        {
            if (PlayerHealth != null)
            {
                PlayerHealth.Untouchable = true;
                if (!PlayerHealth.IsAlive) PlayerHealth.RestoreFull();
            }
            if (Level == null) return;
            IReadOnlyList<Enemy> all = Level.Enemies;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || _strong.Contains(e)) continue;
                _strong.Add(e);
                e.HealthOverride = EnemyHealth; // read by its Start, if it has not had its first frame
                if (e.Body != null && e.Body.MaxHealth < EnemyHealth) e.Body.SetMaxHealth(EnemyHealth);
            }
            IReadOnlyList<Enemy> fighting = Fighting;
            for (int i = 0; i < fighting.Count; i++)
                if (fighting[i] != null && !fighting[i].IsDead) fighting[i].Provoke();
            if (Boss != null && !BossHurt && !Boss.IsDead && Boss.Body != null && Boss.Body.MaxHealth >= EnemyHealth && Boss.Body.CurrentHealth >= EnemyHealth && Time.frameCount > _begunFrame)
            {
                BossHurt = true;
                Boss.Body.TakeHit(Mathf.CeilToInt(EnemyHealth * (1f - BossShareLeft)));
            }
        }

        /// <summary>The nearest living enemy she fights, or null.</summary>
        public Enemy Target()
        {
            if (!Shoots) return null;
            Enemy best = null;
            float bestSq = float.PositiveInfinity;
            Vector3 her = Player.Position;
            IReadOnlyList<Enemy> fighting = Fighting;
            for (int i = 0; i < fighting.Count; i++)
            {
                Enemy e = fighting[i];
                if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
                Vector3 d = e.transform.position - her;
                float sq = d.x * d.x + d.z * d.z;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>What the input driver reads each frame: the cursor on the nearest enemy and the shot pressed; nothing when idle.</summary>
        public InputFrame? Input()
        {
            if (!Shoots || Player == null) return null;
            Camera cam = Camera.main;
            Enemy target = Target();
            if (cam == null) return null;
            Vector3 at = target != null ? target.transform.position : Player.Position + Vector3.forward;
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(at.x, 0f, at.z));
            return new InputFrame { CursorScreen = new Vector2(screen.x, screen.y), ShootPressed = target != null };
        }

        /// <summary>Lets go of her: the input driver reads the devices again, and she can be hurt.</summary>
        public void End()
        {
            if (Player != null && Player.GetComponent<PlayerInputDriver>() is PlayerInputDriver driver && driver.InputSource == Input)
                driver.InputSource = null;
            if (PlayerHealth != null) PlayerHealth.Untouchable = false;
        }
    }
}
