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
    /// (so no reward, chest or heal comes up), and the room is the combat room with the most enemies (the first of them
    /// in the floor's order when two hold as many; not the elite room), or floor 5's exit room. She stands in the
    /// room's middle (the boss's room: <see cref="BossStandBack"/> south of it, toward the camera, so she is not inside
    /// the boss), cannot be hurt (<see cref="PlayerHealth.Untouchable"/>), and presses the basic shot every frame
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
        /// <summary>Metres south of the exit room's middle she stands in the boss scenario.</summary>
        public const float BossStandBack = 6f;

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
                stage.Level = levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
                if (stage.Level == null) return stage;
                stage.Room = scenario == BenchmarkScenario.Floor5Boss ? stage.Level.Layout.ExitRoom : FullestCombatRoom(stage.Level);
                Vector3 middle = stage.Room != null ? stage.Level.Middle(stage.Room) : stage.Level.PlayerStart;
                if (scenario == BenchmarkScenario.Floor5Boss) middle += Vector3.back * BossStandBack;
                stage.Stand = OnNavMesh(middle);
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
