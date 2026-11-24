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
    /// two upgrades (<see cref="ChestUpgrades"/>; three since work order 065, with the stats and tiers on), picked the same way. What is taken goes into the run's
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
    ///
    /// Work order 081 moved, code unchanged: the practice spawns to GeneratedLevelsPractice.cs, the choices of a run (the
    /// floor's reward, chests, upgrades, the elite room) to GeneratedLevelsChoices.cs, and what it draws to GeneratedLevelsGui.cs.
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
        /// <summary>
        /// Work order 073: a bomber in the practice room. No F-key is free since work order 034: B, the bomber's own letter,
        /// which no binding of <see cref="InputBindings"/> uses and no Editor shortcut takes while the Game view has the keys.
        /// A developer's key (work order 066, <see cref="DeveloperKeys"/>).
        /// </summary>
        public const Key BomberKey = Key.B;
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

        // ---------------- Floors ----------------

        /// <summary>
        /// Builds the floor of this seed and number, removing the one before, and puts her at its entrance. Null without a
        /// character. Outside a run it is a floor alone (work order 006's F5); a run builds its floors through it too.
        /// </summary>
        public GeneratedLevel Build(ulong seed, int floor = 1)
        {
            PlayerCharacter player = FindPlayer();
            if (player == null) return null;
            bool clocked = FloorBuildClock.Begin(); // work order 081: the build's steps, for the build-time table
            LevelLayout layout = _labLayout ?? LevelGenerator.Generate(seed, floor, Values); // work order 084: or the room lab's one room (GeneratedLevelsLab.cs)
            FloorBuildClock.Step("plan: the generator");

            if (_level != null) Remove();
            else
            {
                _practiceSpot = player.Position;
                _practiceSpotKnown = true;
            }

            EnemyBrute template = PracticeEnemy();
            GameObject practiceFloor = GameObject.Find("Floor");
            Material baseMaterial = practiceFloor != null && practiceFloor.TryGetComponent(out Renderer floorRenderer) ? floorRenderer.sharedMaterial : null;
            FloorBuildClock.Step("the floor before removed, the template found");
            _level = GeneratedLevel.Build(layout, Values, template, baseMaterial);
            LastSeed = seed;
            Cleared = false;
            _entered.Clear();
            _chestsOpened.Clear();
            CurrentRoom = null;
            EliteRoomEntered = false;
            EliteRoomDone = false;
            _roomHeals.Clear();
            SetUpRoomFights(); // work order 070
            FloorBuildClock.Step("room fights set up");

            player.Teleport(_level.PlayerStart);
            FloorBuildClock.Step("she is put at the entrance");
            Debug.Log(string.Format(s_Inv, "Generated floor {0} of {9} (seed {1}, generator version {2}, attempt {3}): {4} rooms, {5} enemies, {6} props{7}\n{8}",
                                    layout.SeedText, layout.Seed, layout.Version, layout.Attempt, layout.Rooms.Count, _level.Enemies.Count,
                                    _level.Props.Count, template == null ? " (no enemy in the scene to copy)" : "", LevelMap.Draw(layout), layout.Floor));
            FloorBuildClock.Step("the log's line and map");
            PlaySessionRecorder.Current?.NoteLevelBuilt(layout.SeedText, layout.Version, layout.Rooms.Count);
            TrackRoom();
            FloorBuildClock.Step("the recorder told, the room tracked");
            FloorBuildClock.End(clocked);
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
            ForgetRoomFights(); // work order 070
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
            bool clocked = FloorBuildClock.Begin(); // work order 081: the run's start and its first floor, for the build-time table
            InterfaceStyle.LoadIcons(); // work order 055: every icon decoded now, none in a frame of play
            FloorBuildClock.Step("run: the icons loaded");
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
            player.NewSkillsOn = RunValues.NewSkills; // work order 109: the switch as it stands at the run's start
            player.EquipStartSkills();
            player.StartFresh(); // work order 026: no focus or cooldown carries into a run
            _diedWithTheAct.Clear();
            if (_health != null) _health.RestoreFull();
            _runStartedAt = Time.time;
            Record = new RunRecord(RunSeedText, Values.FloorsPerAct);
            PlaySessionRecorder.Current?.NoteRunStarted(RunSeedText);
            FloorBuildClock.Step("run: her skills, upgrades and health set");
            GeneratedLevel first = EnterFloor(1);
            FloorBuildClock.End(clocked);
            return first;
        }

        /// <summary>
        /// A new run from a new seed (F5). Work order 029: with what lasts between runs switched on and no run going on, the
        /// screen between runs comes up instead (null is returned), and its start begins the run.
        /// </summary>
        public GeneratedLevel StartNewRun() => OpensBetweenRuns(null) ? null : StartRun(FreshSeed());

        /// <summary>The same run seed again from floor 1 (F6); a new run if there was none. The screen between runs as for F5.</summary>
        public GeneratedLevel RestartRun() =>
            InLab ? LabSameAgain() : // work order 084: in the room lab, the same room again
            RunSeed.HasValue ? (OpensBetweenRuns(RunSeed) ? null : StartRun(RunSeed.Value)) : StartNewRun();

        GeneratedLevel EnterFloor(int floor)
        {
            Floor = floor;
            bool clocked = FloorBuildClock.Begin(); // work order 081: a floor of a run, from the choice before it
            ulong seed = RunSeeds.FloorSeed(RunSeed ?? 0UL, floor);
            GeneratedLevel level = Build(seed, floor);
            RecordFloor(level);
            _floorStartedAt = Time.time;
            PlaySessionRecorder.Current?.NoteFloorEntered(floor, LevelSeed.ToText(seed));
            NoteRoomCovers(level, floor); // work order 088
            BetweenRunsFloorEntered(floor); // work order 029
            FloorBuildClock.Step("the run's record and the recorder told");
            FloorBuildClock.End(clocked);
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
            LabRunEnded(how); // work order 084: the room lab's run, told as the lab's
            RecordEnd(how);
            BetweenRunsRunEnded(); // work order 029: what the run earned is kept, with the switch on
            // The fifth review (work order 077): the room's number without the passages, as the end screen's record; 0 in a passage.
            PlaySessionRecorder.Current?.NoteRunEnded(how, Floor, CurrentRoom != null && CurrentRoom.Kind != RoomKind.Passage ? LevelGeometry.RoomNumber(_level.Layout, CurrentRoom) + 1 : 0,
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

        // The fifth review (work order 077): counted without the passages, as the end screen's record; a passage has no number.
        string RoomWords() =>
            CurrentRoom == null ? "" : CurrentRoom.Kind == RoomKind.Passage ? ", in a passage"
            : string.Format(s_Inv, ", room {0} ({1})", LevelGeometry.RoomNumber(_level.Layout, CurrentRoom) + 1, CurrentRoom.Kind.ToString().ToLowerInvariant());

        void OnDied()
        {
            MeleeEnemy.Drags.Clear(); // work order 093: her death ends the hold of the guards' Drags
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
            if (LabFloorGoesOn()) return; // work order 084: the act's last exit room in the room lab shows no end screen
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

        PlayerCharacter FindPlayer()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                if (_player != null)
                {
                    _health = _player.GetComponent<PlayerHealth>();
                    if (_health != null) _health.Died += OnDied;
                    _player.NewSkillsOn = RunValues.NewSkills; // work order 109: the three new skills in her list, by the switch
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
                    else if (DeveloperKeys.On && choiceKeys[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom(); // work order 066: a developer key
                    else if (choiceKeys[RedrawKey].wasPressedThisFrame && ChoiceKeyMayPick) RedrawOffer(); // work order 029: Second Look, only when it can; after the guard, as a pick (work order 047)
                }
                return;
            }
            HoldScreens();
            if (InRun) Record?.SetTime(Time.time - _runStartedAt);
            if (ChoiceKeys is Keyboard keyboard)
            {
                // Work order 066: F5 works in every build; F6, F7, F8 to F12 and C only while the developer's keys are on.
                bool dev = DeveloperKeys.On;
                if (dev && ToolKeys(keyboard)) { } // work order 084: the greybox view's and the room lab's keys (GeneratedLevelsGreybox.cs)
                else if (keyboard[NewLevelKey].wasPressedThisFrame) StartNewRun();
                else if (dev && keyboard[SameLevelKey].wasPressedThisFrame) RestartRun();
                else if (dev && keyboard[PracticeRoomKey].wasPressedThisFrame) BackToPracticeRoom();
                else if (Paused || Ending != EndKind.None || BetweenRunsUp) { } // F5 to F7 only, while the pause menu or an end screen is up (work order 018), or the screen between runs (029)
                else if (dev && keyboard[BossKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Boss);
                else if (dev && keyboard[SwarmersKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Swarmer);
                else if (dev && keyboard[ShooterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Shooter);
                else if (dev && keyboard[GuardKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Guard);
                else if (dev && keyboard[CasterKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Caster);
                else if (dev && keyboard[BomberKey].wasPressedThisFrame) SpawnPractice(EnemyKind.Bomber); // work order 073
                else if (dev && keyboard[WarderKey].wasPressedThisFrame) SpawnWarder(keyboard.shiftKey.isPressed); // work order 079: with Shift, its pack
                else if (dev && keyboard[ClearSpawnsKey].wasPressedThisFrame) ClearSpawned();
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
            TickRoomFights(); // work order 070: after her room is tracked
            TickTools(); // work order 084: the greybox view follows its setting
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
            if (LabDied()) return; // work order 084: in the room lab, the same room again at once
            string line = "DIED on floor " + Floor + RoomWords() + "   run " + RunSeedText;
            EndRun("death", line);
            // She stays where she fell until a key on the death screen (work order 018); the game slows to a stop.
            if (_level != null) OpenEnding(EndKind.Died);
        }

        Key InteractKey
        {
            get
            {
                // Work order 081: asked every frame of play, so her driver is found once for her, not each frame.
                if (_player == null) return Key.E;
                if (_driverOf != _player)
                {
                    _driverOf = _player;
                    _driver = _player.GetComponent<PlayerInputDriver>();
                }
                return _driver != null ? _driver.ActiveBindings.Interact : Key.E;
            }
        }
        PlayerCharacter _driverOf;
        PlayerInputDriver _driver;

        void TrackRoom()
        {
            // Every point of the floor's ground is in a room, doorways included; none means she has left it.
            LevelRoom room = _level.RoomAt(_player.Position);
            if (room == null) CurrentRoom = null;
            if (room == null || room == CurrentRoom) return;
            CurrentRoom = room;
            _entered.Add(room.Order);
            if (InRun) Record?.EnterRoom(LevelGeometry.RoomNumber(_level.Layout, room), room.Kind, room.Elite); // work order 059: counted without the passages
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
                // Work order 065: her Mending's health more from every cleared room (0 off).
                // Work order 099: the ranks' room heal points (0 with none, or with the trial off).
                int healed = _health.Heal(_roomHeals.Heal(room.Order, room.Elite, _health.Max, RunValues, Modifiers.Meta.RankRoomHeal) + Modifiers.Stats.Mending);
                BetweenRunsRoomCleared(room); // work order 060: an ordinary room's Echoes, only with the attachments on
                PlaySessionRecorder.Current?.NoteRoomHeal(Floor, room.Order, room.Elite, healed, _health.Current);
                PlaceholderSounds.Current?.AskFor(SoundMoment.CombatRoomCleared); // work order 048: the way on opens
                if (healed > 0) TimingFeedback.Current?.ShowHeal(healed);
            }
        }

        /// <summary>The enemies of a room still alive, and the swarmers the boss called into it (work order 026).</summary>
        int AliveIn(LevelRoom room) =>
            _level != null && room != null ? Alive(_level.EnemiesByRoom, room) + Alive(_level.CalledByRoom, room) : 0;

        // Work order 081: asked every frame of a run (the run box's enemies left): a loop, not LINQ's count over a boxed enumerator.
        static int Alive(IReadOnlyDictionary<int, List<Enemy>> byRoom, LevelRoom room)
        {
            if (!byRoom.TryGetValue(room.Order, out List<Enemy> list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) n++;
            return n;
        }
    }
}
