using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Runs of generated floors from inside the practice scene (work order 006; runs since work order 012, decision
    /// 0008, 2026-11-19). No scene, prefab or asset is made: a floor is built at run time, far to one side of the
    /// practice room (<see cref="GeneratedLevel"/>), and the character is moved there and back with
    /// <see cref="PlayerCharacter.Teleport"/>.
    ///
    /// A run is one attempt at an act of <see cref="LevelValues.FloorsPerAct"/> floors. It has a seed of its own
    /// (eight characters); floor n's seed is made from it and n (<see cref="RunSeeds.FloorSeed"/>). Keys, read only while
    /// <see cref="PlayerInputDriver.ReadRealDevices"/> is true: F5 starts a new run at floor 1 with the start skills and
    /// full health, F6 starts the same run seed again from floor 1, F7 goes back to the practice room and ends the run.
    /// Health carries from floor to floor; taking a floor's reward heals her by <see cref="RunValues.FloorHeal"/> of her
    /// maximum (work order 023, a breath between floors), said in the lines of what was taken; and when every enemy of a
    /// combat room is dead she is healed by <see cref="RunValues.RoomHeal"/>, the elite room
    /// <see cref="RunValues.EliteRoomHeal"/>, once per room (work order 030, <see cref="RoomHeals"/>), shown as a green
    /// number over her and told to the recorder. A floor is finished when every enemy of its exit room is dead; on floors 1
    /// to 4 the floor reward is offered then (<see cref="FloorRewards"/>: three skills she lacks, picked with 1, 2 or 3
    /// or a click, the game paused), and taking it puts her at the next floor's entrance. After the last floor the act is
    /// cleared: the act-cleared screen shows (work order 018). Death ends the run: the game slows to a stop and the death
    /// screen shows the floor and room she reached; she stays on the floor until a key (work order 018).
    ///
    /// Chests (work order 012): standing within <see cref="RunValues.ChestReach"/> of a treasure room's chest she has
    /// not opened, a prompt shows over it and the interact key (E, <see cref="InputBindings.Interact"/>) opens it, once:
    /// two upgrades (<see cref="ChestUpgrades"/>), picked the same way. What is taken goes into the run's
    /// <see cref="RunModifiers"/>, put on the character for the run and cleared when a run starts or ends.
    ///
    /// The elite room (work order 017, decision 0008 as amended 2026-11-20, Kristian: "one hard room in the middle of every
    /// floor; when it is cleared she picks one of three upgrades to skills she has"): the first time every enemy of the
    /// floor's elite room is dead in a run, the skill upgrade choice comes up, once (<see cref="SkillUpgradeOffer"/>: three
    /// upgrades to skills she has, picked like the floor reward, the game paused); none left to offer, she is healed in
    /// full instead, and the screen says so. The doors are open as everywhere: an elite room she walks past gives nothing.
    /// What is taken goes into the run's <see cref="SkillUpgrades"/>, through which the character reads its skills for the
    /// run; cleared when a run starts or ends.
    ///
    /// Tests use intents: <see cref="StartRun"/>, <see cref="RestartRun"/>, <see cref="BackToPracticeRoom"/>,
    /// <see cref="PickReward"/>, <see cref="OpenChest(GeneratedLevel.PlacedProp)"/>, <see cref="PickUpgrade"/>,
    /// <see cref="PickSkillUpgrade"/>, <see cref="FinishFloorForTests"/> and <see cref="FinishEliteRoomForTests"/>; and
    /// <see cref="Build"/> builds one floor alone, outside a run (work order 006).
    ///
    /// In the practice room (work order 008), under the same rule: F9 spawns a pack of swarmers, F10 a shooter, F11 a
    /// guard, F8 the act boss with a third of its health (work order 014), C a caster (work order 027; F4 until work order 034), each about <see cref="SpawnDistance"/> from her on the practice floor, and F12 removes everything spawned
    /// this way. They stay dead when killed. The intents are <see cref="SpawnPractice"/> and <see cref="ClearSpawned"/>.
    /// Nothing is spawned while she is in a generated level.
    ///
    /// It installs itself when Play starts, under the same rule as the play session recorder, so never in a test run
    /// (<see cref="SessionRecordingRule.MayStartByItself"/>); tests make one with <see cref="Ensure"/>. It does
    /// nothing while there is no <see cref="PlayerCharacter"/> in the scene. It logs the seed and the text map of every
    /// floor it builds, and tells the play session recorder about the run, its floors, rooms, rewards and chests.
    ///
    /// The focus choice (work order 025, 2026-11-20) is in GeneratedLevelsFocus.cs: once a run, right after floor 1's reward
    /// is taken and before floor 2, paused, two ways the gauge works for the rest of the run; and the practice room's keys
    /// to try each (F2, F3, and F4 for today's).
    ///
    /// The end of a run, the pause and the run's record (work order 018, 2026-11-20) are in GeneratedLevelsScreens.cs:
    /// death no longer takes her to the practice room at once; the game slows to a stop and the death screen waits for
    /// a key, and so does the act-cleared screen.
    ///
    /// What lasts between runs (work order 029, decision 0009, a proposal he has not ruled on) is in
    /// GeneratedLevelsBetweenRuns.cs, behind a switch that is off by default; off, none of it does anything.
    ///
    /// A reward on the ground and the guard on every choice (work order 042, Kristian's answers of 2026-11-20) are in
    /// GeneratedLevelsDrops.cs: the elite room's upgrade and the floor's reward land as a drop she picks up with the
    /// interact key, and no choice takes a key or a click in its first 0.35 s, nor one that went down before.
    /// </summary>
    public sealed partial class GeneratedLevels : MonoBehaviour
    {
        public const Key NewLevelKey = Key.F5;
        public const Key SameLevelKey = Key.F6;
        public const Key PracticeRoomKey = Key.F7;
        /// <summary>Work order 014: the act boss in the practice room, with a share of its health (<see cref="BossValues.PracticeHealthShare"/>).</summary>
        public const Key BossKey = Key.F8;
        public const Key SwarmersKey = Key.F9;
        public const Key ShooterKey = Key.F10;
        public const Key GuardKey = Key.F11;
        public const Key ClearSpawnsKey = Key.F12;
        /// <summary>
        /// Work order 027: a caster in the practice room (F8 to F11 were taken). F4 until work order 034 (2026-11-20), when
        /// main's focus choice took F4 for today's gauge and no F-key was left: C, the caster's letter on the text map, which
        /// no binding of <see cref="InputBindings"/> uses and no Editor shortcut takes while the Game view has the keys.
        /// </summary>
        public const Key CasterKey = Key.C;
        /// <summary>Metres from her that a spawned enemy (or a pack's middle) stands.</summary>
        public const float SpawnDistance = 12f;

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static ulong s_freshCounter;

        /// <summary>The one running now, or null.</summary>
        public static GeneratedLevels Current { get; private set; }

        /// <summary>The level values in use; tests and tools may replace them before building.</summary>
        public LevelValues Values = new LevelValues();
        /// <summary>The run's values (work order 012).</summary>
        public RunValues RunValues = new RunValues();

        /// <summary>The floor she can walk now, or null.</summary>
        public GeneratedLevel Level => _level != null ? _level : null;
        public bool IsInLevel => Level != null;
        /// <summary>The seed of the last floor built, also after leaving it; null before the first.</summary>
        public ulong? LastSeed { get; private set; }
        /// <summary>The room she stands in now, or null (outside every room, or not in a floor).</summary>
        public LevelRoom CurrentRoom { get; private set; }
        /// <summary>The rooms of this floor she has been in, by their place in the run.</summary>
        public IReadOnlyCollection<int> RoomsEntered => _entered;
        /// <summary>True once every enemy of this floor's exit room is dead: the floor is finished (work order 012).</summary>
        public bool Cleared { get; private set; }
        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        // ---------------- The run (work order 012) ----------------

        public enum ChoiceKind { None, FloorReward, Chest, SkillUpgrade, FocusChoice }

        /// <summary>True from the start of a run until it ends (death, the act cleared, or F7).</summary>
        public bool InRun { get; private set; }
        /// <summary>The seed of the run going on, or of the last one; null before the first.</summary>
        public ulong? RunSeed { get; private set; }
        /// <summary>The floor of the run she is on, from 1; 0 outside a run.</summary>
        public int Floor { get; private set; }
        /// <summary>True once the last floor's exit room is cleared, until the next run starts.</summary>
        public bool ActCleared { get; private set; }
        /// <summary>The run's upgrades: on the character while the run goes on, empty otherwise.</summary>
        public RunModifiers Modifiers { get; } = new RunModifiers();
        /// <summary>The skills taken as floor rewards this run, in order.</summary>
        public IReadOnlyList<string> SkillsTaken => _skillsTaken;
        /// <summary>The line about how the last run ended, for the practice room's box; empty before.</summary>
        public string LastRunLine { get; private set; } = "";
        /// <summary>The choice on screen, if any; while there is one the game is paused.</summary>
        public ChoiceKind Choice { get; private set; }
        /// <summary>The slots of the skills offered as the floor reward on screen.</summary>
        public IReadOnlyList<int> OfferedSkills => _offeredSkills;
        /// <summary>The upgrades the open chest offers.</summary>
        public IReadOnlyList<Upgrade> OfferedUpgrades => _offeredUpgrades;
        /// <summary>The treasure rooms (by their place in the floor) whose chest has been opened this floor.</summary>
        public IReadOnlyCollection<int> ChestsOpened => _chestsOpened;

        // ---------------- The elite room and the skill upgrades (work order 017) ----------------

        /// <summary>The numbers of the skill upgrades: how many are offered, and the table.</summary>
        public SkillUpgradeValues UpgradeValues = new SkillUpgradeValues();
        /// <summary>The run's skill upgrades: on the character while the run goes on, empty otherwise.</summary>
        public RunSkillUpgrades SkillUpgrades { get; } = new RunSkillUpgrades();
        /// <summary>The skill upgrades the choice on screen offers.</summary>
        public IReadOnlyList<SkillUpgradeDefinition> OfferedSkillUpgrades => _offeredSkillUpgrades;
        /// <summary>True once she has been in this floor's elite room.</summary>
        public bool EliteRoomEntered { get; private set; }
        /// <summary>True once this floor's elite room has been cleared in the run, and its offer made.</summary>
        public bool EliteRoomDone { get; private set; }
        /// <summary>Elite rooms cleared this run, each one an offer (or a heal when none was left).</summary>
        public int SkillUpgradeOffers { get; private set; }
        /// <summary>A line on screen for a moment (work order 017: the heal when no upgrade is left); empty when none.</summary>
        public string Notice => Time.unscaledTime < _noticeUntil ? _notice : "";

        readonly List<SkillUpgradeDefinition> _offeredSkillUpgrades = new List<SkillUpgradeDefinition>();
        float _eliteEnteredAt;
        string _notice = "";
        float _noticeUntil = float.NegativeInfinity;
        const float NoticeSeconds = 4f;

        GeneratedLevel _level;
        PlayerCharacter _player;
        PlayerHealth _health;
        Vector3 _practiceSpot;
        bool _practiceSpotKnown;
        readonly HashSet<int> _entered = new HashSet<int>();
        readonly StringBuilder _text = new StringBuilder();
        GUIStyle _style;
        GUIStyle _choiceStyle;
        GUIStyle _promptStyle;

        readonly List<string> _skillsTaken = new List<string>();
        readonly List<int> _offeredSkills = new List<int>();
        readonly List<Upgrade> _offeredUpgrades = new List<Upgrade>();
        readonly HashSet<int> _chestsOpened = new HashSet<int>();
        GeneratedLevel.PlacedProp _openChest;
        float _runStartedAt;
        float _floorStartedAt;
        float _actTime;
        float _timeScaleBefore = 1f;
        bool _diedPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            s_freshCounter = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static GeneratedLevels Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("GeneratedLevels");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<GeneratedLevels>();
            return Current;
        }

        void OnDestroy()
        {
            CloseChoice();
            CloseScreens();
            if (_player != null && _player.Modifiers == Modifiers) _player.Modifiers = null;
            Modifiers.Clear();
            if (_player != null && _player.SkillUpgrades == SkillUpgrades) _player.SkillUpgrades = null;
            SkillUpgrades.Clear();
            if (_player != null && _player.RunFocus == FocusChoice) _player.RunFocus = null; // work order 025
            FocusChoice.Clear();
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.RefreshMax();
            }
            if (_level != null) Destroy(_level.gameObject);
            if (_spawnRoot != null) Destroy(_spawnRoot.gameObject);
            ForgetDevBox(); // work order 055: its texture made in code
            if (Current == this) Current = null;
        }

        // ---------------- Practice spawns (work order 008) ----------------

        Transform _spawnRoot;
        readonly List<Enemy> _spawned = new List<Enemy>();

        /// <summary>The enemies spawned in the practice room and not yet cleared, dead ones too.</summary>
        public IReadOnlyList<Enemy> Spawned => _spawned;

        /// <summary>
        /// Spawns enemies of a kind about <see cref="SpawnDistance"/> from her on the practice floor: for swarmers a pack
        /// (<see cref="SwarmerValues.PracticePack"/>, round a circle of the level values' pack radius), else one. Returns
        /// what it made; nothing while she is in a generated level, without a character or without the practice enemy
        /// to copy.
        /// </summary>
        public List<Enemy> SpawnPractice(EnemyKind kind)
        {
            var made = new List<Enemy>();
            PlayerCharacter player = FindPlayer();
            EnemyBrute template = PracticeEnemy();
            if (player == null || template == null || _level != null) return made;
            if (_spawnRoot == null)
            {
                // In the scene, not under this object that lives across scene loads: a new scene takes them away.
                _spawnRoot = new GameObject("PracticeSpawns").transform;
                SceneManager.MoveGameObjectToScene(_spawnRoot.gameObject, player.gameObject.scene);
            }
            Vector3 middle = SpawnPoint(player.Position);
            int count = kind == EnemyKind.Swarmer ? new SwarmerValues().PracticePack : 1;
            List<Vector3> places = EnemyFactory.PackPlaces(middle, count, Values.PackRadius);
            for (int i = 0; i < places.Count; i++)
            {
                Enemy e = EnemyFactory.Make(kind, places[i], _spawnRoot, template, "Spawned_" + kind + "_" + (_spawned.Count + 1));
                if (e == null) continue;
                if (e is EnemyBoss boss) boss.HealthOverride = Mathf.RoundToInt(boss.Tuning.Enemy.Health * boss.Tuning.PracticeHealthShare);
                made.Add(e);
                _spawned.Add(e);
            }
            return made;
        }

        /// <summary>Removes everything spawned in the practice room.</summary>
        public void ClearSpawned()
        {
            _spawned.Clear();
            if (_spawnRoot == null) return;
            _spawnRoot.gameObject.SetActive(false);
            Destroy(_spawnRoot.gameObject);
            _spawnRoot = null;
        }

        /// <summary>
        /// A point on the NavMesh about <see cref="SpawnDistance"/> from her: the first of eight ways round (north first)
        /// whose straight line from her stays on the NavMesh all the way, else the farthest one gets. Ground height.
        /// </summary>
        static Vector3 SpawnPoint(Vector3 from)
        {
            Vector3 feet = new Vector3(from.x, 0f, from.z);
            if (NavMesh.SamplePosition(feet, out NavMeshHit start, 2f, NavMesh.AllAreas)) feet = start.position;
            Vector3 best = feet;
            float bestDistance = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 to = feet + dir * SpawnDistance;
                if (!NavMesh.Raycast(feet, to, out NavMeshHit hit, NavMesh.AllAreas)) return new Vector3(to.x, 0f, to.z);
                float d = Vector3.Distance(feet, hit.position);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = hit.position - dir * 1.5f; // off the edge a little, so a pack fits
                }
            }
            return new Vector3(best.x, 0f, best.z);
        }

        // ---------------- Floors ----------------

        /// <summary>
        /// Builds the floor of this seed and number, removing the one before, and puts her at its entrance. Null without a
        /// character. Outside a run it is a floor alone (work order 006's F5); a run builds its floors through it too.
        /// </summary>
        public GeneratedLevel Build(ulong seed, int floor = 1)
        {
            PlayerCharacter player = FindPlayer();
            if (player == null) return null;
            LevelLayout layout = LevelGenerator.Generate(seed, floor, Values);

            if (_level != null) Remove();
            else
            {
                _practiceSpot = player.Position;
                _practiceSpotKnown = true;
            }

            EnemyBrute template = PracticeEnemy();
            GameObject practiceFloor = GameObject.Find("Floor");
            Material baseMaterial = practiceFloor != null && practiceFloor.TryGetComponent(out Renderer floorRenderer) ? floorRenderer.sharedMaterial : null;
            _level = GeneratedLevel.Build(layout, Values, template, baseMaterial);
            LastSeed = seed;
            Cleared = false;
            _entered.Clear();
            _chestsOpened.Clear();
            CurrentRoom = null;
            EliteRoomEntered = false;
            EliteRoomDone = false;
            _roomHeals.Clear();

            player.Teleport(_level.PlayerStart);
            Debug.Log(string.Format(s_Inv, "Generated floor {0} of {9} (seed {1}, generator version {2}, attempt {3}): {4} rooms, {5} enemies, {6} props{7}\n{8}",
                                    layout.SeedText, layout.Seed, layout.Version, layout.Attempt, layout.Rooms.Count, _level.Enemies.Count,
                                    _level.Props.Count, template == null ? " (no enemy in the scene to copy)" : "", LevelMap.Draw(layout), layout.Floor));
            PlaySessionRecorder.Current?.NoteLevelBuilt(layout.SeedText, layout.Version, layout.Rooms.Count);
            TrackRoom();
            return _level;
        }

        /// <summary>A floor alone from a new seed (outside a run).</summary>
        public GeneratedLevel BuildNew() => Build(FreshSeed());

        /// <summary>The last floor's seed again, alone, from nothing; a new seed's if there was none.</summary>
        public GeneratedLevel Rebuild() => LastSeed.HasValue ? Build(LastSeed.Value, _level != null ? _level.Layout.Floor : 1) : BuildNew();

        static ulong FreshSeed()
        {
            ulong entropy = (ulong)DateTime.UtcNow.Ticks ^ (++s_freshCounter * 0x9E3779B97F4A7C15UL);
            return LevelSeed.Fresh(entropy);
        }

        /// <summary>
        /// Takes her back to where she was in the practice room and removes the floor; a run going on ends ("left").
        /// Nothing without a floor.
        /// </summary>
        public void BackToPracticeRoom()
        {
            bool fromRun = InRun || Ending != EndKind.None; // work order 026
            CloseChoice();
            CloseScreens();
            if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
            if (_level == null) return;
            PlayerCharacter player = FindPlayer();
            if (player != null) player.Teleport(_practiceSpotKnown ? _practiceSpot : Vector3.zero);
            // Work order 026 (the review of 2026-11-20): after a death she is alive at once, where the practice room puts
            // her, not dead for a moment and then at the scene's start; and a run's focus and cooldowns stay in the run.
            if (_health != null && !_health.IsAlive) _health.RestoreFull();
            if (fromRun && player != null) player.StartFresh();
            Remove();
        }

        void Remove()
        {
            // Off at once, so its NavMesh and colliders are gone before anything else is built or warped; destroyed at the end of the frame.
            _level.gameObject.SetActive(false);
            Destroy(_level.gameObject);
            _level = null;
            CurrentRoom = null;
            Cleared = false;
            _entered.Clear();
            _chestsOpened.Clear();
        }

        // ---------------- Runs (work order 012) ----------------

        public string RunSeedText => RunSeed.HasValue ? LevelSeed.ToText(RunSeed.Value) : "";

        /// <summary>
        /// Starts a run with this seed at floor 1: the start skills equipped, the run's upgrades cleared, full health.
        /// A run going on ends first ("left"). Returns floor 1, or null without a character.
        /// </summary>
        public GeneratedLevel StartRun(ulong runSeed)
        {
            PlayerCharacter player = FindPlayer();
            if (player == null) return null;
            InterfaceStyle.LoadIcons(); // work order 055: every icon decoded now, none in a frame of play
            CloseChoice();
            CloseScreens();
            if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
            ClearSpawned(); // what the practice room spawned, of every kind, never follows her into a run (the review of 2026-11-20, work order 026)
            RunSeed = runSeed;
            InRun = true;
            ActCleared = false;
            LastRunLine = "";
            _diedPending = false;
            _skillsTaken.Clear();
            Modifiers.Clear();
            Modifiers.CooldownFloor = RunValues.CooldownFloor;
            Modifiers.StatsAndTiers = RunValues.StatsAndTiers; // work order 050: her stats and the tiers, for this run
            Modifiers.StatValues = RunValues.Stats;
            player.SeedCrits(runSeed); // the crits' stream, from the run's seed
            ApplyBetweenRuns(); // work order 029: the rack's pieces, only with the switch on
            player.Modifiers = Modifiers;
            SkillUpgrades.Clear(); // a new run starts with no skill upgrade (work order 017)
            player.SkillUpgrades = SkillUpgrades;
            SkillUpgradeOffers = 0;
            FocusChoice.Clear(); // today's gauge until the choice after floor 1 (work order 025)
            player.RunFocus = FocusChoice;
            player.EquipStartSkills();
            player.StartFresh(); // work order 026: no focus or cooldown carries into a run
            _diedWithTheAct.Clear();
            if (_health != null) _health.RestoreFull();
            _runStartedAt = Time.time;
            Record = new RunRecord(RunSeedText, Values.FloorsPerAct);
            PlaySessionRecorder.Current?.NoteRunStarted(RunSeedText);
            return EnterFloor(1);
        }

        /// <summary>
        /// A new run from a new seed (F5). Work order 029: with what lasts between runs switched on and no run going on, the
        /// screen between runs comes up instead (null is returned), and its start begins the run.
        /// </summary>
        public GeneratedLevel StartNewRun() => OpensBetweenRuns(null) ? null : StartRun(FreshSeed());

        /// <summary>The same run seed again from floor 1 (F6); a new run if there was none. The screen between runs as for F5.</summary>
        public GeneratedLevel RestartRun() =>
            RunSeed.HasValue ? (OpensBetweenRuns(RunSeed) ? null : StartRun(RunSeed.Value)) : StartNewRun();

        GeneratedLevel EnterFloor(int floor)
        {
            Floor = floor;
            ulong seed = RunSeeds.FloorSeed(RunSeed ?? 0UL, floor);
            GeneratedLevel level = Build(seed, floor);
            RecordFloor(level);
            _floorStartedAt = Time.time;
            PlaySessionRecorder.Current?.NoteFloorEntered(floor, LevelSeed.ToText(seed));
            BetweenRunsFloorEntered(floor); // work order 029
            return level;
        }

        /// <summary>
        /// Ends the run going on: the upgrades come off and are cleared (her maximum health goes back), and the recorder
        /// is told how. The floor stays until she leaves it (the act cleared) or is removed by the caller.
        /// </summary>
        void EndRun(string how, string line)
        {
            if (!InRun) return;
            InRun = false;
            LastRunLine = line;
            RecordEnd(how);
            BetweenRunsRunEnded(); // work order 029: what the run earned is kept, with the switch on
            PlaySessionRecorder.Current?.NoteRunEnded(how, Floor, CurrentRoom != null ? CurrentRoom.Order + 1 : 0,
                                                      CurrentRoom != null ? CurrentRoom.Kind.ToString().ToLowerInvariant() : "", Time.time - _runStartedAt,
                                                      Modifiers.StatsAndTiers ? StatsLine() : null); // work order 050: her final stats
            if (_player != null && _player.Modifiers == Modifiers) _player.Modifiers = null;
            Modifiers.Clear();
            if (_player != null && _player.SkillUpgrades == SkillUpgrades) _player.SkillUpgrades = null; // work order 017
            SkillUpgrades.Clear();
            if (_player != null && _player.RunFocus == FocusChoice) _player.RunFocus = null; // work order 025: back to the practice room's own
            FocusChoice.Clear();
            if (_health != null) _health.RefreshMax();
            Debug.Log("Run ended (" + how + "): " + line);
        }

        /// <summary>Her stats in one line (work order 050), for the recorder.</summary>
        string StatsLine()
        {
            FeelValues f = _player != null ? _player.Feel : new FeelValues();
            // Work order 060: from her pistols' interval before her attack speed (the feel's with no part fitted).
            float baseInterval = _player != null ? _player.BasicShotBaseInterval : f.BasicShotStartup + f.BasicShotActive + f.BasicShotRecovery;
            return Modifiers.Stats.OneLine(baseInterval, _health != null ? _health.MaxHealth : 100);
        }

        string RoomWords() =>
            CurrentRoom != null ? string.Format(s_Inv, ", room {0} ({1})", CurrentRoom.Order + 1, CurrentRoom.Kind.ToString().ToLowerInvariant()) : "";

        void OnDied()
        {
            if (InRun) _diedPending = true; // handled at the frame's end or the next Update (Settle), out of the enemy's strike that killed her
        }

        /// <summary>The floor is finished: a floor reward to choose on floors 1 to 4, the act cleared after the last.</summary>
        void FloorFinished()
        {
            Cleared = true;
            Debug.Log("Floor cleared: " + _level.Layout.SeedText);
            if (!InRun) return;
            float took = Time.time - _floorStartedAt;
            PlaySessionRecorder.Current?.NoteFloorFinished(Floor, took, _health != null ? _health.Current : 0);
            BetweenRunsFloorCleared(); // work order 029
            FloorGoesOn();
        }

        /// <summary>
        /// What follows a finished floor: the act cleared after the last, else the floor's reward. Where the floor would go on
        /// by itself with the elite room's drop still lying, its upgrade comes first and this follows its pick (the third
        /// review of 2026-11-20, work order 047; <see cref="EliteRewardFirst"/>).
        /// </summary>
        void FloorGoesOn()
        {
            if (Floor >= Values.FloorsPerAct)
            {
                if (EliteRewardFirst()) return; // work order 047: it would be left lying on the act's last floor
                ActCleared = true;
                _actTime = Time.time - _runStartedAt;
                BetweenRunsActCleared(); // work order 029: the act boss
                EndRun("act cleared", "ACT CLEARED   run " + RunSeedText + "   time " + SessionSummary.Duration(_actTime));
                EndTheCalled();
                OpenEnding(EndKind.ActCleared);
                return;
            }
            OfferFloorReward();
        }

        /// <summary>
        /// The act is cleared (work order 026, the review of 2026-11-20): every swarmer the boss called that is still
        /// alive dies on the spot, so none of theirs lands after it, and those deaths are not her kills.
        /// </summary>
        void EndTheCalled()
        {
            foreach (Enemy e in _level.Called)
            {
                if (e == null || e.IsDead) continue;
                _diedWithTheAct.Add(e);
                e.Body.TakeHit(1000000);
            }
        }

        readonly HashSet<Enemy> _diedWithTheAct = new HashSet<Enemy>();

        void OfferFloorReward()
        {
            PlayerCharacter player = FindPlayer();
            var equipped = new List<bool>();
            for (int slot = 0; slot < player.Skills.Count; slot++) equipped.Add(player.IsEquipped(slot));
            StrikeOutOfOffer(player, equipped); // work order 029: Not That One
            _offeredSkills.Clear();
            _offeredSkills.AddRange(FloorRewards.Offer(RunSeed ?? 0UL, Floor, equipped, RunValues.SkillsOffered));
            if (_offeredSkills.Count == 0)
            {
                if (FocusChoiceDue) OfferFocusChoice(); // nothing left to offer, but the focus choice still comes (work order 025)
                else if (!EliteRewardFirst()) EnterFloor(Floor + 1); // nothing left to offer; an elite's drop lying first (work order 047)
                return;
            }
            PlaySessionRecorder.Current?.NoteRewardOffered(Floor, _offeredSkills.Select(s => player.Skills[s].Name).ToList());
            OpenChoice(ChoiceKind.FloorReward);
        }

        /// <summary>
        /// Takes offered skill <paramref name="index"/> (0, 1 or 2): it is equipped in its own slot for the run, the game
        /// goes on, and she is put at the next floor's entrance with the health she has. False when no reward is offered.
        /// </summary>
        public bool PickReward(int index)
        {
            PickUpForAPick(ChoiceKind.FloorReward); // work order 042: its drop, if it lies, is picked up first
            if (Choice != ChoiceKind.FloorReward || index < 0 || index >= _offeredSkills.Count) return false;
            CardPicked(index); // work order 045: its card flashes as it goes
            PlayerCharacter player = FindPlayer();
            int slot = _offeredSkills[index];
            player.EquipSkill(slot);
            string name = player.Skills[slot].Name;
            _skillsTaken.Add(name);
            Record?.TakeSkill(name);
            PlaySessionRecorder.Current?.NoteRewardTaken(Floor, name);
            // A breath between floors (work order 023): a share of her maximum back as the reward is taken.
            if (_health != null)
            {
                int healed = _health.Heal(FloorHeals.Amount(_health.Max, RunValues.FloorHeal));
                Record?.FloorHealed(Floor, healed);
                PlaySessionRecorder.Current?.NoteFloorHeal(Floor, healed, _health.Current);
            }
            // Work order 025: after floor 1's reward the focus choice comes up, still paused, before floor 2.
            if (FocusChoiceDue)
            {
                OfferFocusChoice();
                return true;
            }
            CloseChoice();
            EnterFloor(Floor + 1);
            return true;
        }

        /// <summary>The chests of this floor, in the order of the rooms.</summary>
        public List<GeneratedLevel.PlacedProp> Chests() =>
            _level == null ? new List<GeneratedLevel.PlacedProp>() : _level.Props.Where(p => p.Prop.Model == PropModel.Chest).ToList();

        /// <summary>The chest she can open now: unopened, within reach, in a run; null if none.</summary>
        public GeneratedLevel.PlacedProp ChestInReach()
        {
            if (!InRun || _level == null || _player == null || Choice != ChoiceKind.None) return null;
            foreach (GeneratedLevel.PlacedProp chest in _level.Props) // no list made per call (work order 026)
            {
                if (chest.Prop.Model != PropModel.Chest || _chestsOpened.Contains(chest.Room.Order) || chest.Object == null) continue;
                Vector3 c = chest.Object.transform.position, p = _player.Position;
                if (new Vector2(c.x - p.x, c.z - p.z).magnitude <= RunValues.ChestReach) return chest;
            }
            return null;
        }

        /// <summary>
        /// The chest the prompt shows over now: <see cref="ChestInReach"/>, found once a frame and not once per GUI event
        /// (the review of 2026-11-20, work order 026).
        /// </summary>
        public GeneratedLevel.PlacedProp ChestPrompt
        {
            get
            {
                if (_promptFrame != Time.frameCount)
                {
                    _promptFrame = Time.frameCount;
                    _promptChest = ChestInReach();
                }
                return _promptChest;
            }
        }

        int _promptFrame = -1;
        GeneratedLevel.PlacedProp _promptChest;

        /// <summary>Opens the chest in reach, if any (the interact key). True when one opened.</summary>
        public bool OpenChest() => OpenChest(ChestInReach());

        /// <summary>
        /// Opens this chest, once, in a run: its two upgrades are offered and the game pauses. False for a chest already
        /// opened, outside a run, or while another choice is up.
        /// </summary>
        public bool OpenChest(GeneratedLevel.PlacedProp chest)
        {
            if (chest == null || !InRun || Choice != ChoiceKind.None || Paused || _chestsOpened.Contains(chest.Room.Order)) return false; // not under the pause menu (work order 026)
            PlayerCharacter player = FindPlayer();
            var equipped = new List<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot)) equipped.Add(player.Skills[slot].Name);
            int extra = FirstChestExtra; // work order 029: Deep Chests, asked before this chest counts
            _chestsOpened.Add(chest.Room.Order);
            _offeredUpgrades.Clear();
            // Work order 025: no Mend at full health, no row the run has taken its most times.
            bool fullHealth = _health == null || _health.Current >= _health.Max;
            _offeredUpgrades.AddRange(ChestUpgrades.Offer(RunSeed ?? 0UL, Floor, chest.Room.Order, equipped, RunValues, Modifiers, fullHealth, extra));
            if (_offeredUpgrades.Count == 0) return true;
            _openChest = chest;
            OpenChoice(ChoiceKind.Chest);
            return true;
        }

        /// <summary>Takes offered upgrade <paramref name="index"/> (0 or 1) into the run's upgrades; the game goes on. False with no chest open.</summary>
        public bool PickUpgrade(int index)
        {
            if (Choice != ChoiceKind.Chest || index < 0 || index >= _offeredUpgrades.Count) return false;
            CardPicked(index); // work order 045
            Upgrade u = _offeredUpgrades[index];
            Modifiers.Take(u);
            Record?.TakeUpgrade(u.Short);
            if (_health != null)
            {
                _health.RefreshMax();
                if (u.Heal > 0) _health.Heal(u.Heal);
            }
            PlaySessionRecorder.Current?.NoteChestOpened(Floor, _openChest != null ? _openChest.Room.Order : -1,
                                                         _offeredUpgrades.Select(o => o.Short).ToList(), u.Short,
                                                         u.Tiered ? u : null, u.Tiered ? StatsLine() : null); // work order 050: its stat, tier and size, and her stats after
            _openChest = null;
            CloseChoice();
            return true;
        }

        /// <summary>
        /// The elite room was cleared, in a run (work order 017): the skill upgrade choice, once a floor; none left to
        /// offer, a full heal instead, said on screen.
        /// </summary>
        void EliteRoomFinished()
        {
            EliteRoomDone = true;
            PlayerCharacter player = FindPlayer();
            PlaySessionRecorder.Current?.NoteEliteCleared(Floor, EliteRoomEntered ? Time.time - _eliteEnteredAt : 0f, _health != null ? _health.Current : 0);
            var had = new HashSet<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot) && player.Skills[slot] != null) had.Add(player.Skills[slot].Name);
            _offeredSkillUpgrades.Clear();
            _offeredSkillUpgrades.AddRange(SkillUpgradeOffer.Offer(RunSeed ?? 0UL, Floor, UpgradeValues.Table, had, SkillUpgrades.Taken,
                                                                   UpgradeValues.Offered + Modifiers.Meta.EliteOffersExtra)); // work order 029: Knight's Trophy
            SkillUpgradeOffers++;
            BetweenRunsEliteCleared(); // work order 029
            PlaySessionRecorder.Current?.NoteSkillUpgradeOffered(Floor, _offeredSkillUpgrades.Select(u => u.FullName + " (" + u.KindWord + ")").ToList(),
                                                                 _offeredSkillUpgrades.Count == 0);
            if (_offeredSkillUpgrades.Count == 0)
            {
                if (_health != null) _health.RestoreFull();
                Say("ELITE ROOM CLEARED   no upgrade left to take: fully healed");
                return;
            }
            OpenChoice(ChoiceKind.SkillUpgrade);
        }

        /// <summary>
        /// Takes offered skill upgrade <paramref name="index"/> (0, 1 or 2) into the run's upgrades (work order 017): the
        /// skill is read through it from now on, and the game goes on. False with no skill upgrade offered.
        /// </summary>
        public bool PickSkillUpgrade(int index)
        {
            PickUpForAPick(ChoiceKind.SkillUpgrade); // work order 042: its drop, if it lies, is picked up first
            if (Choice != ChoiceKind.SkillUpgrade || index < 0 || index >= _offeredSkillUpgrades.Count) return false;
            CardPicked(index); // work order 045
            SkillUpgradeDefinition u = _offeredSkillUpgrades[index];
            SkillUpgrades.Take(u);
            Record?.TakeSkillUpgrade(u.FullName);
            PlaySessionRecorder.Current?.NoteSkillUpgradeTaken(Floor, u.FullName, u.KindWord);
            if (!FloorRewardFollows()) CloseChoice(); // work order 042: the floor's reward, when it was picked up with this one
            return true;
        }

        /// <summary>For tests (work order 017): kills every enemy of this floor's elite room, so it is cleared on the next frame.</summary>
        public void FinishEliteRoomForTests()
        {
            if (_level == null) return;
            FinishedForTests(true); // work order 042: its choice comes up as before, without a drop
            foreach (Enemy e in _level.EliteRoomEnemies)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        void Say(string line)
        {
            _notice = line;
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            Debug.Log(line);
        }

        /// <summary>For tests: kills every enemy of this floor's exit room, so the floor is finished on the next frame.</summary>
        public void FinishFloorForTests()
        {
            if (_level == null) return;
            FinishedForTests(false); // work order 042: its choice comes up as before, without a drop
            foreach (Enemy e in _level.ExitEnemies)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(1000000);
        }

        void OpenChoice(ChoiceKind kind)
        {
            if (LeaveADrop(kind)) return; // work order 042: what she earns by killing lands on the ground first (GeneratedLevelsDrops.cs)
            if (Choice == ChoiceKind.None) _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f; // a hit stop's 0 is not kept
            Choice = kind;
            Time.timeScale = 0f;
            PlayerInputDriver.HeldByChoice = true;
            GuardTheChoice(); // work order 042: no key or click picks for a moment, and only a fresh press then
        }

        void CloseChoice()
        {
            if (Choice == ChoiceKind.None) return;
            // A chest whose choice closes without a pick (an end screen) is not used up (the second reviews of 2026-11-20,
            // work order 033). A pick lets go of the chest first.
            if (Choice == ChoiceKind.Chest && _openChest != null) _chestsOpened.Remove(_openChest.Room.Order);
            Choice = ChoiceKind.None;
            _offeredSkills.Clear();
            _offeredUpgrades.Clear();
            _offeredSkillUpgrades.Clear();
            _openChest = null;
            Time.timeScale = _timeScaleBefore;
            PlayerInputDriver.HeldByChoice = false;
        }

        PlayerCharacter FindPlayer()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                if (_player != null)
                {
                    _health = _player.GetComponent<PlayerHealth>();
                    if (_health != null) _health.Died += OnDied;
                }
            }
            return _player;
        }

        /// <summary>The scene's own enemy: one that comes back and is not part of a generated level.</summary>
        EnemyBrute PracticeEnemy()
        {
            foreach (EnemyBrute e in FindObjectsByType<EnemyBrute>(FindObjectsSortMode.InstanceID))
                if (e.ComesBack && e.GetComponentInParent<GeneratedLevel>() == null) return e;
            return null;
        }

        // ---------------- Every frame ----------------

        void Update()
        {
            if (FindPlayer() == null) return;
            // Death first (work order 026, the second review of 2026-11-20): before a choice (a chest opened on the frame
            // of the lethal hit), whose screen the death screen closes, and before the interact key.
            SettleDeath();
            if (Choice != ChoiceKind.None)
            {
                Time.timeScale = 0f; // a hit stop that ends while the choice is up must not start the game again
                if (ChoiceKeys is Keyboard choiceKeys) // the real keyboard, or a test's (work order 042)
                {
                    // 4 for a fourth line, which only the rack between runs gives (work order 029: Knight's Trophy).
                    int pick = choiceKeys.digit1Key.wasPressedThisFrame ? 0 : choiceKeys.digit2Key.wasPressedThisFrame ? 1
                             : choiceKeys.digit3Key.wasPressedThisFrame ? 2 : choiceKeys.digit4Key.wasPressedThisFrame ? 3 : -1;
                    if (pick >= 0 && ChoiceKeyMayPick) // work order 042: a press inside the guard picks nothing
                    {
                        if (Choice == ChoiceKind.FloorReward) PickReward(pick);
                        else if (Choice == ChoiceKind.SkillUpgrade) PickSkillUpgrade(pick);
                        else if (Choice == ChoiceKind.FocusChoice) PickFocusChoice(pick); // work order 025: 1 or 2
                        else PickUpgrade(pick);
                    }
                    else if (choiceKeys[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                    else if (choiceKeys[RedrawKey].wasPressedThisFrame && ChoiceKeyMayPick) RedrawOffer(); // work order 029: Second Look, only when it can; after the guard, as a pick (work order 047)
                }
                return;
            }
            HoldScreens();
            if (InRun) Record?.SetTime(Time.time - _runStartedAt);
            if (ChoiceKeys is Keyboard keyboard)
            {
                if (keyboard[NewLevelKey].wasPressedThisFrame) StartNewRun();
                else if (keyboard[SameLevelKey].wasPressedThisFrame) RestartRun();
                else if (keyboard[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                else if (Paused || Ending != EndKind.None || BetweenRunsUp) { } // F5 to F7 only, while the pause menu or an end screen is up (work order 018), or the screen between runs (029)
                else if (keyboard[BossKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Boss);
                else if (keyboard[SwarmersKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Swarmer);
                else if (keyboard[ShooterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Shooter);
                else if (keyboard[GuardKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Guard);
                else if (keyboard[CasterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Caster);
                else if (keyboard[ClearSpawnsKey].wasPressedThisFrame) ClearSpawned();
                else if (keyboard[HairTriggerKey].wasPressedThisFrame) TryFocusInPractice(FocusMode.HairTrigger); // work order 025
                else if (keyboard[SteadyHandKey].wasPressedThisFrame) TryFocusInPractice(FocusMode.SteadyHand);
                else if (keyboard[TodaysFocusKey].wasPressedThisFrame) TryFocusInPractice(FocusMode.Today);
                else if (keyboard[InteractKey].wasPressedThisFrame && !PlayerInputDriver.LetGoThisFrame) Interact(); // work order 026; a drop first (042)
            }
            if (_level == null)
            {
                CurrentRoom = null;
                return;
            }
            TrackRoom();
            if (InRun) BetweenRunsTick(); // work order 029: the elite room's feat; after the room is tracked (work order 033)
        }

        // A hit stop puts back the time scale it found when it ends, in its own Update: the choice holds the pause after it.
        void LateUpdate()
        {
            if (FindPlayer() != null) Settle();
            if (Choice != ChoiceKind.None) Time.timeScale = 0f;
            else HoldScreens(); // and the pause menu's, and the end's slow-down (work order 018)
        }

        /// <summary>
        /// One place decides what a frame brings, in a fixed priority (the second reviews of 2026-11-20, work order 033): her
        /// death, then the pause, then a cleared room. It runs after every Update of the frame, so Esc's pause (the HUD's, in
        /// its own Update) and a chest opened with the interact key are seen here whichever Update ran first; a cleared room
        /// waits while a choice, the pause menu or an end screen is up, and comes when it has closed.
        /// </summary>
        void Settle()
        {
            SettleDeath();
            if (_level == null || Choice != ChoiceKind.None) return;
            if (InRun && !Paused) HealClearedRooms(); // work order 030: before the elite room's choice pauses the game
            // The elite room first (work order 017): its choice comes up, and the floor's waits for the next frame.
            // One screen at a time (work order 026): while paused, both wait until the game resumes.
            if (InRun && !EliteRoomDone && !Paused && _level.EliteRoomCleared)
            {
                EliteRoomFinished();
                if (Choice != ChoiceKind.None) return;
            }
            if (!Cleared && !Paused && _level.ExitRoomCleared) FloorFinished();
        }

        /// <summary>Her death in a run, once: the run ends, and the death screen comes (work orders 018 and 026).</summary>
        void SettleDeath()
        {
            if (!_diedPending) return;
            _diedPending = false;
            string line = "DIED on floor " + Floor + RoomWords() + "   run " + RunSeedText;
            EndRun("death", line);
            // She stays where she fell until a key on the death screen (work order 018); the game slows to a stop.
            if (_level != null) OpenEnding(EndKind.Died);
        }

        Key InteractKey
        {
            get
            {
                PlayerInputDriver driver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
                return driver != null ? driver.ActiveBindings.Interact : Key.E;
            }
        }

        void TrackRoom()
        {
            // Every point of the floor's ground is in a room, doorways included; none means she has left it.
            LevelRoom room = _level.RoomAt(_player.Position);
            if (room == null) CurrentRoom = null;
            if (room == null || room == CurrentRoom) return;
            CurrentRoom = room;
            _entered.Add(room.Order);
            if (InRun) Record?.EnterRoom(room.Order, room.Kind, room.Elite);
            PlaySessionRecorder.Current?.NoteRoomEntered(room.Order, room.Kind.ToString().ToLowerInvariant(), RoomThemes.Word(room.Theme)); // work order 028: and its theme
            if (room.Elite && !EliteRoomEntered)
            {
                EliteRoomEntered = true;
                _eliteEnteredAt = Time.time;
                if (InRun) PlaySessionRecorder.Current?.NoteEliteEntered(Floor, room.Order);
            }
        }

        /// <summary>The combat rooms healed for on this floor (work order 030, lever A).</summary>
        readonly RoomHeals _roomHeals = new RoomHeals();

        /// <summary>
        /// Health back inside a floor (work order 030, lever A): each combat room whose enemies are all dead heals her once,
        /// by <see cref="RunValues.RoomHeal"/> of her maximum, the elite room by <see cref="RunValues.EliteRoomHeal"/>; a
        /// green number over her says how much, and the recorder is told.
        /// </summary>
        void HealClearedRooms()
        {
            if (_health == null || !_health.IsAlive) return;
            List<LevelRoom> rooms = _level.Layout.Rooms;
            for (int r = 0; r < rooms.Count; r++) // every frame: no LINQ, nothing made
            {
                LevelRoom room = rooms[r];
                if (room.Kind != RoomKind.Combat || _roomHeals.Healed(room.Order)) continue;
                if (!_level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) || list.Count == 0) continue;
                bool alive = false;
                for (int i = 0; i < list.Count && !alive; i++) alive = list[i] != null && !list[i].IsDead;
                if (alive) continue;
                int healed = _health.Heal(_roomHeals.Heal(room.Order, room.Elite, _health.Max, RunValues));
                BetweenRunsRoomCleared(room); // work order 060: an ordinary room's Echoes, only with the attachments on
                PlaySessionRecorder.Current?.NoteRoomHeal(Floor, room.Order, room.Elite, healed, _health.Current);
                PlaceholderSounds.Current?.AskFor(SoundMoment.CombatRoomCleared); // work order 048: the way on opens
                if (healed > 0) TimingFeedback.Current?.ShowHeal(healed);
            }
        }

        /// <summary>The enemies of a room still alive, and the swarmers the boss called into it (work order 026).</summary>
        int AliveIn(LevelRoom room) =>
            _level != null && room != null ? Alive(_level.EnemiesByRoom, room) + Alive(_level.CalledByRoom, room) : 0;

        static int Alive(IReadOnlyDictionary<int, List<Enemy>> byRoom, LevelRoom room) =>
            byRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count(e => e != null && !e.IsDead) : 0;

        /// <summary>What the box on screen says, line by line; empty when she is not in a floor.</summary>
        public string StatusText()
        {
            if (_level == null) return "";
            LevelLayout layout = _level.Layout;
            _text.Clear();
            if (InRun || ActCleared)
                _text.Append(string.Format(s_Inv, "run {0}   floor {1} of {2}   (floor seed {3})", RunSeedText, Floor, Values.FloorsPerAct, layout.SeedText)).Append('\n');
            else
                _text.Append(string.Format(s_Inv, "level {0}   floor {1}   generator v{2}", layout.SeedText, layout.Floor, layout.Version)).Append('\n');
            _text.Append(CurrentRoom != null
                ? string.Format(s_Inv, "room {0} of {1} ({2}), {3} enemies left in it", CurrentRoom.Order + 1, layout.Rooms.Count,
                                CurrentRoom.Kind.ToString().ToLowerInvariant(), AliveIn(CurrentRoom))
                : string.Format(s_Inv, "outside the level ({0} rooms)", layout.Rooms.Count)).Append('\n');
            _text.Append(string.Format(s_Inv, "rooms entered {0} of {1}   exit room enemies left {2}   v{3}", _entered.Count, layout.Rooms.Count,
                                       AliveIn(layout.ExitRoom), layout.Version)).Append('\n');
            if (InRun)
            {
                _text.Append("skills taken: ").Append(_skillsTaken.Count == 0 ? "none" : string.Join(", ", _skillsTaken));
                string heal = Record != null ? Record.FloorHealText : "";
                if (heal.Length > 0) _text.Append("   (").Append(heal).Append(')'); // work order 023
                _text.Append('\n');
                _text.Append("upgrades: ").Append(Modifiers.ShortText()).Append('\n');
                _text.Append("skill upgrades: ").Append(SkillUpgrades.Text()).Append('\n');
                _text.Append("focus: ").Append(FocusChoice.Taken ? FocusModeRules.Name(FocusChoice.Mode) : "today's, until the choice").Append('\n'); // work order 025
            }
            if (ActCleared) _text.Append(LastRunLine).Append('\n');
            else if (Cleared) _text.Append("FLOOR CLEARED   seed ").Append(layout.SeedText).Append('\n');
            _text.Append("F5 new run   F6 same run   F7 practice room" + (InRun ? "   E open a chest" : ""));
            return _text.ToString();
        }

        void OnGUI()
        {
            if (_player == null) return;
            bool inLevel = _level != null;
            if (!inLevel && !StartedByItself && Choice == ChoiceKind.None && LastRunLine.Length == 0) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, wordWrap = true };

            // Laid out for a view 720 pixels high and scaled to the real height, like the readout in the other corner.
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            const float width = 330f;
            float viewWidth = Screen.width / scale;
            float x = viewWidth - width - 10f;
            // The developer's box: shown or not, and how far down, as the HUD says (work order 018: F1); as before without one.
            bool kinetic = InterfaceStyle.Kinetic; // work order 045: in the interface's skin (GeneratedLevelsKinetic.cs)
            if (inLevel)
            {
                if (ShowDevBox) // the status text is built only when it is shown (work order 026)
                {
                    string text = StatusText();
                    int lines = text.Split('\n').Length;
                    GUI.Box(new Rect(x, DevBoxTop, width, 12 + lines * 15), text, DevBoxStyle());
                }
                if (kinetic) DrawChestPromptKinetic();
                else DrawChestPrompt(scale);
            }
            else if (ShowDevBox)
            {
                string text = "F5 new run" + (RunSeed.HasValue ? "   F6 run " + RunSeedText + " again" : "")
                            + "\nF8 boss   F9 swarmers   F10 shooter   F11 guard   F12 clear"
                            + "\n" + CasterKey + " caster" // work order 034: its own line, the line above being full
                            + "\n" + PracticeFocusLine() // work order 025
                            + (LastRunLine.Length > 0 ? "\n" + LastRunLine : "");
                GUI.Box(new Rect(x, DevBoxTop, width, 12 + text.Split('\n').Length * 15), text, DevBoxStyle());
            }
            if (Choice != ChoiceKind.None && kinetic) DrawGuardedChoiceKinetic(); // work order 045: slim tilted cards at the right edge
            else if (Choice != ChoiceKind.None) DrawGuardedChoice(viewWidth); // work order 042: dim, and deaf to clicks, for a moment
            else if (Notice.Length > 0 && kinetic) DrawNoticeKinetic();
            else if (Notice.Length > 0)
            {
                if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect((viewWidth - 460f) * 0.5f, SkillBar.LayoutHeight * 0.3f, 460f, 30f), Notice, _promptStyle);
            }
            if (kinetic) DrawPickFlash();
            GUI.matrix = matrixBefore;
        }

        /// <summary>A card's tier as its coloured edge (work order 050): white common, teal rare, gold epic.</summary>
        static void TierEdge(Rect r, UpgradeTier tier)
        {
            float[] c = UpgradeTiers.Rgb(tier);
            Color before = GUI.color;
            GUI.color = new Color(c[0], c[1], c[2], before.a);
            const float t = 3f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = before;
        }

        /// <summary>A small prompt over the chest she can open (work order 012).</summary>
        void DrawChestPrompt(float scale)
        {
            GeneratedLevel.PlacedProp chest = ChestPrompt;
            Camera cam = Camera.main;
            if (chest == null || cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(chest.Object.transform.position + Vector3.up * 1.4f);
            if (screen.z <= 0f) return;
            if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            float px = screen.x / scale, py = (Screen.height - screen.y) / scale;
            GUI.Box(new Rect(px - 60f, py - 14f, 120f, 26f), InteractKey + "  open chest", _promptStyle);
        }

        /// <summary>
        /// The choice on screen (work order 012), in the middle, drawn like the other boxes: a title, and a button per
        /// option with its number; a click or the number's key picks it.
        /// </summary>
        void DrawChoice(float viewWidth)
        {
            if (_choiceStyle == null) _choiceStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            if (Choice == ChoiceKind.FocusChoice)
            {
                DrawFocusChoice(viewWidth); // work order 025
                return;
            }
            PlayerCharacter player = _player;
            bool reward = Choice == ChoiceKind.FloorReward;
            bool skillUpgrade = Choice == ChoiceKind.SkillUpgrade; // work order 017
            int count = reward ? _offeredSkills.Count : skillUpgrade ? _offeredSkillUpgrades.Count : _offeredUpgrades.Count;
            float rowHeight = reward || skillUpgrade ? 74f : 44f, width = 520f;
            float height = 40f + count * (rowHeight + 6f) + 24f;
            float left = (viewWidth - width) * 0.5f, top = (SkillBar.LayoutHeight - height) * 0.5f;
            string title = reward
                ? string.Format(s_Inv, "FLOOR {0} CLEARED   choose a new skill (1-{1} or click)", Floor, count)
                : skillUpgrade
                ? string.Format(s_Inv, "ELITE ROOM CLEARED   choose a skill upgrade (1-{0} or click)", count)
                : string.Format(s_Inv, "CHEST   choose an upgrade (1-{0} or click)", count);
            GUI.Box(new Rect(left, top, width, height), title, _style);
            InputBindings bindings = player != null && player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            for (int i = 0; i < count; i++)
            {
                string text;
                if (reward)
                {
                    int slot = _offeredSkills[i];
                    SkillDefinition s = player.Skills[slot];
                    text = string.Format(s_Inv, "{0}   {1}   key {2}   cooldown {3:0.#} s   {4}\n{5}", i + 1, s.Name,
                                         bindings != null ? bindings.SkillLabel(player.FirstFreeKey) : "?", // the key it will go on (work order 015)
                                         s.Cooldown, SkillDefaults.MarkRole(s), SkillDefaults.Line(s.Name));
                }
                else if (skillUpgrade)
                {
                    // The skill's name and key, the upgrade's name, its kind and its line (work order 017).
                    SkillUpgradeDefinition u = _offeredSkillUpgrades[i];
                    int key = player.KeyOfSkill(player.SlotOf(u.Skill));
                    text = string.Format(s_Inv, "{0}   {1}   key {2}   {3}   [{4}]\n{5}", i + 1, u.Skill,
                                         bindings != null && key >= 0 ? bindings.SkillLabel(key) : "-", u.Name.ToUpperInvariant(), u.KindWord, u.Line);
                }
                else
                {
                    Upgrade up = _offeredUpgrades[i]; // work order 050: a tiered card says its tier first
                    text = string.Format(s_Inv, "{0}   {1}", i + 1, up.Tiered ? up.TierWord.ToUpperInvariant() + "   " + up.Text : up.Text);
                }
                var card = new Rect(left + 10f, top + 32f + i * (rowHeight + 6f), width - 20f, rowHeight);
                bool picked = GUI.Button(card, text, _choiceStyle);
                if (!reward && !skillUpgrade && _offeredUpgrades[i].Tiered) TierEdge(card, _offeredUpgrades[i].Tier); // work order 050
                if (picked)
                {
                    if (reward) PickReward(i);
                    else if (skillUpgrade) PickSkillUpgrade(i);
                    else PickUpgrade(i);
                    return;
                }
            }
            GUI.Label(new Rect(left + 12f, top + height - 22f, width - 24f, 20f), "The game is paused.   F7 leaves the run." + RedrawWords());
        }
    }
}
