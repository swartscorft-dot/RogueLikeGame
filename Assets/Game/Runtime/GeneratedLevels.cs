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
    /// Health carries from floor to floor. A floor is finished when every enemy of its exit room is dead; on floors 1
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
    /// Tests use intents: <see cref="StartRun"/>, <see cref="RestartRun"/>, <see cref="BackToPracticeRoom"/>,
    /// <see cref="PickReward"/>, <see cref="OpenChest(GeneratedLevel.PlacedProp)"/>, <see cref="PickUpgrade"/> and
    /// <see cref="FinishFloorForTests"/>; and <see cref="Build"/> builds one floor alone, outside a run (work order 006).
    ///
    /// In the practice room (work order 008), under the same rule: F9 spawns a pack of swarmers, F10 a shooter, F11 a
    /// guard, each about <see cref="SpawnDistance"/> from her on the practice floor, and F12 removes everything spawned
    /// this way. They stay dead when killed. The intents are <see cref="SpawnPractice"/> and <see cref="ClearSpawned"/>.
    /// Nothing is spawned while she is in a generated level.
    ///
    /// It installs itself when Play starts, under the same rule as the play session recorder, so never in a test run
    /// (<see cref="SessionRecordingRule.MayStartByItself"/>); tests make one with <see cref="Ensure"/>. It does
    /// nothing while there is no <see cref="PlayerCharacter"/> in the scene. It logs the seed and the text map of every
    /// floor it builds, and tells the play session recorder about the run, its floors, rooms, rewards and chests.
    ///
    /// The end of a run, the pause and the run's record (work order 018, 2026-11-20) are in GeneratedLevelsScreens.cs:
    /// death no longer takes her to the practice room at once; the game slows to a stop and the death screen waits for
    /// a key, and so does the act-cleared screen.
    /// </summary>
    public sealed partial class GeneratedLevels : MonoBehaviour
    {
        public const Key NewLevelKey = Key.F5;
        public const Key SameLevelKey = Key.F6;
        public const Key PracticeRoomKey = Key.F7;
        public const Key SwarmersKey = Key.F9;
        public const Key ShooterKey = Key.F10;
        public const Key GuardKey = Key.F11;
        public const Key ClearSpawnsKey = Key.F12;
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

        public enum ChoiceKind { None, FloorReward, Chest }

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
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.RefreshMax();
            }
            if (_level != null) Destroy(_level.gameObject);
            if (_spawnRoot != null) Destroy(_spawnRoot.gameObject);
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
            CloseChoice();
            CloseScreens();
            if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
            if (_level == null) return;
            PlayerCharacter player = FindPlayer();
            if (player != null) player.Teleport(_practiceSpotKnown ? _practiceSpot : Vector3.zero);
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
            CloseChoice();
            CloseScreens();
            if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
            RunSeed = runSeed;
            InRun = true;
            ActCleared = false;
            LastRunLine = "";
            _diedPending = false;
            _skillsTaken.Clear();
            Modifiers.Clear();
            Modifiers.CooldownFloor = RunValues.CooldownFloor;
            player.Modifiers = Modifiers;
            player.EquipStartSkills();
            if (_health != null) _health.RestoreFull();
            _runStartedAt = Time.time;
            Record = new RunRecord(RunSeedText, Values.FloorsPerAct);
            PlaySessionRecorder.Current?.NoteRunStarted(RunSeedText);
            return EnterFloor(1);
        }

        /// <summary>A new run from a new seed (F5).</summary>
        public GeneratedLevel StartNewRun() => StartRun(FreshSeed());

        /// <summary>The same run seed again from floor 1 (F6); a new run if there was none.</summary>
        public GeneratedLevel RestartRun() => RunSeed.HasValue ? StartRun(RunSeed.Value) : StartNewRun();

        GeneratedLevel EnterFloor(int floor)
        {
            Floor = floor;
            ulong seed = RunSeeds.FloorSeed(RunSeed ?? 0UL, floor);
            GeneratedLevel level = Build(seed, floor);
            RecordFloor(level);
            _floorStartedAt = Time.time;
            PlaySessionRecorder.Current?.NoteFloorEntered(floor, LevelSeed.ToText(seed));
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
            PlaySessionRecorder.Current?.NoteRunEnded(how, Floor, CurrentRoom != null ? CurrentRoom.Order + 1 : 0,
                                                      CurrentRoom != null ? CurrentRoom.Kind.ToString().ToLowerInvariant() : "", Time.time - _runStartedAt);
            if (_player != null && _player.Modifiers == Modifiers) _player.Modifiers = null;
            Modifiers.Clear();
            if (_health != null) _health.RefreshMax();
            Debug.Log("Run ended (" + how + "): " + line);
        }

        string RoomWords() =>
            CurrentRoom != null ? string.Format(s_Inv, ", room {0} ({1})", CurrentRoom.Order + 1, CurrentRoom.Kind.ToString().ToLowerInvariant()) : "";

        void OnDied()
        {
            if (InRun) _diedPending = true; // handled in the next Update, out of the enemy's strike that killed her
        }

        /// <summary>The floor is finished: a floor reward to choose on floors 1 to 4, the act cleared after the last.</summary>
        void FloorFinished()
        {
            Cleared = true;
            Debug.Log("Floor cleared: " + _level.Layout.SeedText);
            if (!InRun) return;
            float took = Time.time - _floorStartedAt;
            PlaySessionRecorder.Current?.NoteFloorFinished(Floor, took, _health != null ? _health.Current : 0);
            if (Floor >= Values.FloorsPerAct)
            {
                ActCleared = true;
                _actTime = Time.time - _runStartedAt;
                EndRun("act cleared", "ACT CLEARED   run " + RunSeedText + "   time " + SessionSummary.Duration(_actTime));
                OpenEnding(EndKind.ActCleared);
                return;
            }
            OfferFloorReward();
        }

        void OfferFloorReward()
        {
            PlayerCharacter player = FindPlayer();
            var equipped = new List<bool>();
            for (int slot = 0; slot < player.Skills.Count; slot++) equipped.Add(player.IsEquipped(slot));
            _offeredSkills.Clear();
            _offeredSkills.AddRange(FloorRewards.Offer(RunSeed ?? 0UL, Floor, equipped, RunValues.SkillsOffered));
            if (_offeredSkills.Count == 0)
            {
                EnterFloor(Floor + 1); // nothing left to offer
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
            if (Choice != ChoiceKind.FloorReward || index < 0 || index >= _offeredSkills.Count) return false;
            PlayerCharacter player = FindPlayer();
            int slot = _offeredSkills[index];
            player.EquipSkill(slot);
            string name = player.Skills[slot].Name;
            _skillsTaken.Add(name);
            Record?.TakeSkill(name);
            PlaySessionRecorder.Current?.NoteRewardTaken(Floor, name);
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
            foreach (GeneratedLevel.PlacedProp chest in Chests())
            {
                if (_chestsOpened.Contains(chest.Room.Order) || chest.Object == null) continue;
                Vector3 c = chest.Object.transform.position, p = _player.Position;
                if (new Vector2(c.x - p.x, c.z - p.z).magnitude <= RunValues.ChestReach) return chest;
            }
            return null;
        }

        /// <summary>Opens the chest in reach, if any (the interact key). True when one opened.</summary>
        public bool OpenChest() => OpenChest(ChestInReach());

        /// <summary>
        /// Opens this chest, once, in a run: its two upgrades are offered and the game pauses. False for a chest already
        /// opened, outside a run, or while another choice is up.
        /// </summary>
        public bool OpenChest(GeneratedLevel.PlacedProp chest)
        {
            if (chest == null || !InRun || Choice != ChoiceKind.None || _chestsOpened.Contains(chest.Room.Order)) return false;
            PlayerCharacter player = FindPlayer();
            var equipped = new List<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot)) equipped.Add(player.Skills[slot].Name);
            _chestsOpened.Add(chest.Room.Order);
            _offeredUpgrades.Clear();
            _offeredUpgrades.AddRange(ChestUpgrades.Offer(RunSeed ?? 0UL, Floor, chest.Room.Order, equipped, RunValues));
            if (_offeredUpgrades.Count == 0) return true;
            _openChest = chest;
            OpenChoice(ChoiceKind.Chest);
            return true;
        }

        /// <summary>Takes offered upgrade <paramref name="index"/> (0 or 1) into the run's upgrades; the game goes on. False with no chest open.</summary>
        public bool PickUpgrade(int index)
        {
            if (Choice != ChoiceKind.Chest || index < 0 || index >= _offeredUpgrades.Count) return false;
            Upgrade u = _offeredUpgrades[index];
            Modifiers.Take(u);
            Record?.TakeUpgrade(u.Short);
            if (_health != null)
            {
                _health.RefreshMax();
                if (u.Heal > 0) _health.Heal(u.Heal);
            }
            PlaySessionRecorder.Current?.NoteChestOpened(Floor, _openChest != null ? _openChest.Room.Order : -1,
                                                         _offeredUpgrades.Select(o => o.Short).ToList(), u.Short);
            _openChest = null;
            CloseChoice();
            return true;
        }

        /// <summary>For tests: kills every enemy of this floor's exit room, so the floor is finished on the next frame.</summary>
        public void FinishFloorForTests()
        {
            if (_level == null) return;
            foreach (Enemy e in _level.ExitEnemies)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(1000000);
        }

        void OpenChoice(ChoiceKind kind)
        {
            if (Choice == ChoiceKind.None) _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f; // a hit stop's 0 is not kept
            Choice = kind;
            Time.timeScale = 0f;
            PlayerInputDriver.HeldByChoice = true;
        }

        void CloseChoice()
        {
            if (Choice == ChoiceKind.None) return;
            Choice = ChoiceKind.None;
            _offeredSkills.Clear();
            _offeredUpgrades.Clear();
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
            if (Choice != ChoiceKind.None)
            {
                Time.timeScale = 0f; // a hit stop that ends while the choice is up must not start the game again
                if (PlayerInputDriver.ReadRealDevices && Keyboard.current is Keyboard choiceKeys)
                {
                    int pick = choiceKeys.digit1Key.wasPressedThisFrame ? 0 : choiceKeys.digit2Key.wasPressedThisFrame ? 1
                             : choiceKeys.digit3Key.wasPressedThisFrame ? 2 : -1;
                    if (pick >= 0)
                    {
                        if (Choice == ChoiceKind.FloorReward) PickReward(pick);
                        else PickUpgrade(pick);
                    }
                    else if (choiceKeys[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                }
                return;
            }
            HoldScreens();
            if (InRun) Record?.SetTime(Time.time - _runStartedAt);
            if (PlayerInputDriver.ReadRealDevices && Keyboard.current is Keyboard keyboard)
            {
                if (keyboard[NewLevelKey].wasPressedThisFrame) StartNewRun();
                else if (keyboard[SameLevelKey].wasPressedThisFrame) RestartRun();
                else if (keyboard[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                else if (Paused || Ending != EndKind.None) { } // F5 to F7 only, while the pause menu or an end screen is up (work order 018)
                else if (keyboard[SwarmersKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Swarmer);
                else if (keyboard[ShooterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Shooter);
                else if (keyboard[GuardKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Guard);
                else if (keyboard[ClearSpawnsKey].wasPressedThisFrame) ClearSpawned();
                else if (keyboard[InteractKey].wasPressedThisFrame) OpenChest();
            }
            if (_diedPending)
            {
                _diedPending = false;
                string line = "DIED on floor " + Floor + RoomWords() + "   run " + RunSeedText;
                EndRun("death", line);
                // She stays where she fell until a key on the death screen (work order 018); the game slows to a stop.
                if (_level != null) OpenEnding(EndKind.Died);
            }
            if (_level == null)
            {
                CurrentRoom = null;
                return;
            }
            TrackRoom();
            if (!Cleared && _level.ExitRoomCleared) FloorFinished();
        }

        // A hit stop puts back the time scale it found when it ends, in its own Update: the choice holds the pause after it.
        void LateUpdate()
        {
            if (Choice != ChoiceKind.None) Time.timeScale = 0f;
            else HoldScreens(); // and the pause menu's, and the end's slow-down (work order 018)
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
            if (InRun) Record?.EnterRoom(room.Order, room.Kind);
            PlaySessionRecorder.Current?.NoteRoomEntered(room.Order, room.Kind.ToString().ToLowerInvariant());
        }

        /// <summary>The enemies of a room still alive.</summary>
        int AliveIn(LevelRoom room) =>
            _level != null && room != null && _level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count(e => e != null && !e.IsDead) : 0;

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
                _text.Append("skills taken: ").Append(_skillsTaken.Count == 0 ? "none" : string.Join(", ", _skillsTaken)).Append('\n');
                _text.Append("upgrades: ").Append(Modifiers.ShortText()).Append('\n');
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
            if (inLevel)
            {
                string text = StatusText();
                int lines = text.Split('\n').Length;
                if (ShowDevBox) GUI.Box(new Rect(x, DevBoxTop, width, 12 + lines * 15), text, _style);
                DrawChestPrompt(scale);
            }
            else if (ShowDevBox)
            {
                string text = "F5 new run" + (RunSeed.HasValue ? "   F6 run " + RunSeedText + " again" : "")
                            + "\nF9 swarmers   F10 shooter   F11 guard   F12 clear"
                            + (LastRunLine.Length > 0 ? "\n" + LastRunLine : "");
                GUI.Box(new Rect(x, DevBoxTop, width, 12 + text.Split('\n').Length * 15), text, _style);
            }
            if (Choice != ChoiceKind.None) DrawChoice(viewWidth);
            GUI.matrix = matrixBefore;
        }

        /// <summary>A small prompt over the chest she can open (work order 012).</summary>
        void DrawChestPrompt(float scale)
        {
            GeneratedLevel.PlacedProp chest = ChestInReach();
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
            PlayerCharacter player = _player;
            bool reward = Choice == ChoiceKind.FloorReward;
            int count = reward ? _offeredSkills.Count : _offeredUpgrades.Count;
            float rowHeight = reward ? 74f : 44f, width = 520f;
            float height = 40f + count * (rowHeight + 6f) + 24f;
            float left = (viewWidth - width) * 0.5f, top = (SkillBar.LayoutHeight - height) * 0.5f;
            string title = reward
                ? string.Format(s_Inv, "FLOOR {0} CLEARED   choose a new skill (1-{1} or click)", Floor, count)
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
                else
                {
                    text = string.Format(s_Inv, "{0}   {1}", i + 1, _offeredUpgrades[i].Text);
                }
                if (GUI.Button(new Rect(left + 10f, top + 32f + i * (rowHeight + 6f), width - 20f, rowHeight), text, _choiceStyle))
                {
                    if (reward) PickReward(i);
                    else PickUpgrade(i);
                    return;
                }
            }
            GUI.Label(new Rect(left + 12f, top + height - 22f, width - 24f, 20f), "The game is paused.   F7 leaves the run.");
        }
    }
}
