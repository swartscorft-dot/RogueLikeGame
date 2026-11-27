using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The rehearsal (work order 148, 2026-11-27; Kristian ticked "Også «generalprøve» med siste bygg": a switch that starts right in
    /// the throne room with the upgrades from your last run; the rules and numbers are <see cref="RehearsalValues"/>'), as part of
    /// <see cref="GeneratedLevels"/>.
    ///
    /// The build of the last run is kept: when a run ends (her death, the act cleared, or left) having reached floor 2 or further,
    /// what she carried (<see cref="RehearsalBuild"/>) is written to a file of its own beside the other saves
    /// (<see cref="RehearsalFileInUse"/>), one build only, the newest run's. The room lab's runs and rehearsals keep nothing.
    ///
    /// A rehearsal (<see cref="StartRehearsal()"/>, the main menu's entry "Rehearsal: the boss" while a build is stored) is a run
    /// of its own (<see cref="InRun"/>, <see cref="InRehearsal"/>) on the act's last floor, built whole by the ordinary generator
    /// from a new seed: she has the stored build, full health, an empty focus and every cooldown ready, and stands outside the
    /// throne room's door. Every enemy outside the throne room is taken away before it wakes (none killed: no kills, no deaths
    /// heard), every room behind her is as a cleared one (no heal owed, the elite room's offer made, the chests opened), and the
    /// doors are open as a cleared room's. She walks in; the boss's entrance and fight start the ordinary way.
    ///
    /// A rehearsal is not a run: it earns nothing (no Echoes, no feats, nothing saved between runs), counts in no record, does
    /// not replace the stored build, and the recorder writes it as a session of its own ("rehearsal":"true" in its start line,
    /// PlaySessionRecorderRehearsal.cs). When it ends (her death, the boss's), the end screen's Enter is "Again" (a new rehearsal
    /// at once, a new seed), its second answer the main menu, Esc the practice room; it says the fight's length and what struck
    /// her last, when an enemy's landed strike says so.
    ///
    /// With <see cref="RehearsalValues.On"/> off nothing here does anything: nothing is stored, no entry, no rehearsal starts.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Where the stored build is written and read: null for the game's own (<see cref="DefaultRehearsalSavePath"/>). Tests set a
        /// temporary one, and put null back.
        /// </summary>
        public static string RehearsalSavePath { get; set; }
        public static string DefaultRehearsalSavePath => Path.Combine(Application.persistentDataPath, RehearsalValues.FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRehearsalStatics() => RehearsalSavePath = null;

        /// <summary>
        /// The file these floors read and write the build in: the path given, else the game's own only when they installed
        /// themselves in play; null for floors a test made without a path, which keep the build in memory only (never his file).
        /// </summary>
        public string RehearsalFileInUse => !string.IsNullOrEmpty(RehearsalSavePath) ? RehearsalSavePath : StartedByItself ? DefaultRehearsalSavePath : null;

        /// <summary>True while a rehearsal goes on (a run of its own, <see cref="InRun"/> too).</summary>
        public bool InRehearsal { get; private set; }
        /// <summary>True from a rehearsal's start until she leaves the rehearsals (a real run, the practice room, the main menu): its end screen's too.</summary>
        public bool Rehearsing => _rehearsing;
        /// <summary>True while the end screen up is a rehearsal's: its answers are "Again", the main menu and the practice room.</summary>
        public bool RehearsalEnding => _rehearsing && !InRehearsal && Ending != EndKind.None;
        /// <summary>The last rehearsal's fight: seconds from the boss room's fight waking to its end; negative when it never woke.</summary>
        public float RehearsalFightSeconds { get; private set; } = -1f;
        /// <summary>What struck her last in the last rehearsal she died in ("boss: Slam"); empty when no enemy's landed strike said so.</summary>
        public string RehearsalKilledBy { get; private set; } = "";
        /// <summary>How many rehearsals were started (for tests).</summary>
        public int RehearsalsStarted { get; private set; }
        /// <summary>The skills of the stored build that she could not be given (a skill the game no longer has); empty as it should be.</summary>
        public IReadOnlyList<string> RehearsalSkillsMissing => _rehearsalMissing;

        bool _rehearsing;
        ulong? _seedBeforeRehearsal;  // the last real run's seed, given back as a rehearsal ends (as the room lab's)
        RehearsalBuild _rehearsalBuild; // the build kept in memory: the one written, or the one a test made without a file
        readonly List<string> _rehearsalMissing = new List<string>();
        bool _strikeKnown;
        bool _strikeElite;
        EnemyKind _strikeKind;
        string _strikeAttack;

        static readonly CultureInfo s_Inv148 = CultureInfo.InvariantCulture;

        /// <summary>The stored build: read from its file when one is in use (each time: it is small), else the one in memory; null for none, or a broken file.</summary>
        public RehearsalBuild StoredRehearsalBuild
        {
            get
            {
                string path = RehearsalFileInUse;
                return path == null ? _rehearsalBuild : RehearsalFile.Load(path);
            }
        }

        /// <summary>True when the main menu shows "Rehearsal: the boss": the switch on and a build stored.</summary>
        public bool HasRehearsalBuild => RehearsalValues.On && StoredRehearsalBuild != null;

        // ---------------- The build of the last run ----------------

        /// <summary>The build she carries now, in the run going on: her bar, the run's modifiers, skill upgrades and focus choice. A copy, read back from its own JSON.</summary>
        public RehearsalBuild CaptureBuild()
        {
            PlayerCharacter player = FindPlayer();
            var keys = new List<string>();
            var had = new List<string>();
            if (player != null)
            {
                IReadOnlyList<SkillDefinition> skills = player.Skills;
                for (int k = 0; k < player.KeyCount; k++)
                {
                    int slot = player.SkillOnKey(k);
                    string name = slot >= 0 && slot < skills.Count && skills[slot] != null ? skills[slot].Name : "";
                    keys.Add(name);
                    if (name.Length > 0 && player.IsEquipped(slot)) had.Add(name);
                }
                for (int slot = 0; slot < skills.Count; slot++)
                    if (skills[slot] != null && player.IsEquipped(slot) && player.KeyOfSkill(slot) < 0) had.Add(skills[slot].Name);
            }
            RehearsalBuild b = RehearsalBuild.Capture(Modifiers, SkillUpgrades, FocusChoice, keys, had);
            b.MaxHealth = _health != null ? _health.Max : 0;
            b.Stats = Modifiers.StatsAndTiers ? StatsLine() : "";
            return RehearsalFile.Parse(RehearsalJson.Write(b)) ?? b; // as it is stored: proven to read back
        }

        /// <summary>
        /// Called by EndRun before the run's upgrades come off: a rehearsal ends here; a real run that reached floor 2 keeps its
        /// build (<see cref="RehearsalRules.Keeps"/>).
        /// </summary>
        void RehearsalRunEnds(string how)
        {
            if (InRehearsal)
            {
                RehearsalEnded(how);
                return;
            }
            if (!RehearsalRules.Keeps(RehearsalValues.On, InLab, false, Floor)) return;
            RehearsalBuild b = CaptureBuild();
            b.Run = RunSeedText;
            b.Floor = Floor;
            b.Ended = how;
            b.Saved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", s_Inv148);
            _rehearsalBuild = b;
            string path = RehearsalFileInUse;
            if (path == null) return; // a test's floors without a path: in memory only
            if (RehearsalFile.Save(path, b, out string error)) Debug.Log("Rehearsal: the build of run " + b.Run + " (floor " + b.Floor.ToString(s_Inv148) + ", " + how + ") kept at " + path);
            else Debug.LogWarning("Rehearsal: the build could not be kept: " + error);
        }

        // ---------------- A rehearsal ----------------

        /// <summary>A rehearsal from a new seed; null with the switch off, no build stored, or no character.</summary>
        public GeneratedLevel StartRehearsal() => StartRehearsal(FreshSeed());

        /// <summary>
        /// A rehearsal of this seed (see the class): the act's last floor built whole, every room but the throne room as cleared,
        /// she with the stored build outside the throne room's door. A run going on ends first ("left"). Null with the switch off,
        /// no build stored, or no character.
        /// </summary>
        public GeneratedLevel StartRehearsal(ulong seed)
        {
            if (!RehearsalValues.On) return null;
            PlayerCharacter player = FindPlayer();
            if (player == null || StoredRehearsalBuild == null) return null;
            InterfaceStyle.LoadIcons(); // every icon decoded now, none in a frame of play (as a run's start)
            CloseChoice();
            CloseScreens();
            if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
            RehearsalBuild build = StoredRehearsalBuild; // read after: a real run that ended just now is the newest
            if (build == null) return null;
            ClearSpawned(); // what the practice room spawned never follows her
            if (!_rehearsing) _seedBeforeRehearsal = RunSeed;
            _rehearsing = true;
            PlaySessionRecorder.SwitchSession(rehearsal: true);

            // A run of its own, as StartRun begins one, with the stored build in place of nothing taken; no trial between runs.
            RunSeed = seed;
            InRun = true;
            InRehearsal = true;
            ActCleared = false;
            LastRunLine = "";
            _diedPending = false;
            _metaRun = false;
            Earnings = null; // nothing earned, and the end screen says none
            _skillsTaken.Clear();
            Modifiers.Clear();
            Modifiers.CooldownFloor = RunValues.CooldownFloor;
            Modifiers.StatsAndTiers = RunValues.StatsAndTiers;
            Modifiers.StatValues = RunValues.Stats;
            player.SeedCrits(seed);
            player.NewSkillsOn = RunValues.NewSkills;
            build.ApplyTo(Modifiers, SkillUpgrades, FocusChoice, RunValues.BasicShot, player.Kit.BasicShotsPerMark, player.Kit.MaxMarks);
            player.Modifiers = Modifiers;
            player.SkillUpgrades = SkillUpgrades;
            SkillUpgradeOffers = 0;
            player.RunFocus = FocusChoice;
            player.EquipStartSkills();
            _rehearsalMissing.Clear();
            foreach (string name in build.Skills)
            {
                int slot = player.SlotOf(name);
                if (slot < 0)
                {
                    _rehearsalMissing.Add(name);
                    continue;
                }
                player.EquipSkill(slot);
                if (!player.Skills[slot].StartEquipped) _skillsTaken.Add(name);
            }
            if (_rehearsalMissing.Count > 0) Debug.LogWarning("Rehearsal: the stored build's " + string.Join(", ", _rehearsalMissing) + " is not a skill of hers now: left out");
            player.StartFresh(); // focus empty, every cooldown ready (and Steady Start's focus, as a run's start gives it)
            _diedWithTheAct.Clear();
            if (_health != null) _health.RestoreFull();
            _runStartedAt = Time.time;
            Record = new RunRecord(LevelSeed.ToText(seed), Values.FloorsPerAct);
            RecordTheBuild(player, build);
            RehearsalFightSeconds = -1f;
            RehearsalKilledBy = "";
            _strikeKnown = false;
            RehearsalsStarted++;
            PlaySessionRecorder.Current?.NoteRunStarted(RunSeedText);

            _wayDownShown = null; // the act's last floor told no floor before it, as a floor alone
            GeneratedLevel level = EnterFloor(Values.FloorsPerAct);
            if (level == null) return null;
            ClearBehindHer(level);
            PutHerAtTheDoor(level);
            ListenForWhatStrikesHer(level);
            Debug.Log("Rehearsal: run " + RunSeedText + " on floor " + Floor.ToString(s_Inv148) + " with the build of run " + build.Run + " (floor "
                      + build.Floor.ToString(s_Inv148) + ", " + build.Ended + ", kept " + build.Saved + ")");
            return level;
        }

        /// <summary>The end screen's record says what she carries, as a run's says what it took.</summary>
        void RecordTheBuild(PlayerCharacter player, RehearsalBuild build)
        {
            foreach (string name in _skillsTaken) Record.TakeSkill(name, SkillUpgrades.TierOf(name));
            foreach (Upgrade u in build.Upgrades) Record.TakeUpgrade(u.Short, u.Tiered ? u.Tier : UpgradeTier.Common);
            foreach (RehearsalShotTier s in build.BasicShot) Record.TakeUpgrade(BasicShotTexts.Short(s.Kind, s.Tier), BasicShotTexts.InkTier(s.Tier));
            foreach (SkillUpgradeDefinition u in build.SkillUpgrades)
                if (u != null && !u.TierStep) Record.TakeSkillUpgrade(u.FullName);
            if (build.FocusTaken) Record.TakeFocusChoice(FocusModeRules.Name(build.Focus));
        }

        /// <summary>
        /// Every room but the throne room as cleared: its enemies taken away before they wake (switched off at once, destroyed at
        /// the frame's end; no death is heard or counted), no room's heal owed, the elite room's offer made, the chests opened.
        /// The doors stay open, as a cleared room's.
        /// </summary>
        void ClearBehindHer(GeneratedLevel level)
        {
            LevelRoom throne = level.Layout.ExitRoom;
            level.EnemiesByRoom.TryGetValue(throne.Order, out List<Enemy> stay);
            int gone = 0;
            foreach (Enemy e in level.Enemies)
            {
                if (e == null || e == level.Boss || (stay != null && stay.Contains(e))) continue;
                e.gameObject.SetActive(false);
                Destroy(e.gameObject);
                gone++;
            }
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if (room.Order == throne.Order) continue;
                if (room.Kind == RoomKind.Combat) _roomHeals.Heal(room.Order, room.Elite, 0, RunValues); // counted as healed: 0 given, nothing heard
                if (room.Kind == RoomKind.Treasure) _chestsOpened.Add(room.Order);
            }
            EliteRoomDone = true;
            Debug.Log("Rehearsal: " + gone.ToString(s_Inv148) + " enemies outside the throne room taken away; " + (stay != null ? stay.Count : 0).ToString(s_Inv148) + " in it");
        }

        /// <summary>She stands <see cref="RehearsalValues.DoorStandOff"/> outside the throne room's door, on the walkable floor.</summary>
        void PutHerAtTheDoor(GeneratedLevel level)
        {
            LevelRoom throne = level.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, throne);
            if (door == null || _player == null)
            {
                Debug.LogWarning("Rehearsal: the throne room has no door in; she stays at the floor's entrance");
                return;
            }
            Vector3 p = FromTheDoor(level, throne, door, -RehearsalValues.DoorStandOff);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, RehearsalValues.DoorSnap, NavMesh.AllAreas)) p = hit.position;
            _player.Teleport(p);
            TrackRoom();
        }

        /// <summary>
        /// The point <paramref name="depth"/> m straight in from the middle of <paramref name="door"/> into <paramref name="room"/>;
        /// a negative depth is outside it (the room or passage before).
        /// </summary>
        public static Vector3 FromTheDoor(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            return level.ToWorld(p);
        }

        /// <summary>What struck her last: the throne room's enemies' landed strikes, and those of the swarmers the boss calls (the reports the recorder writes).</summary>
        void ListenForWhatStrikesHer(GeneratedLevel level)
        {
            if (level.EnemiesByRoom.TryGetValue(level.Layout.ExitRoom.Order, out List<Enemy> list))
                foreach (Enemy e in list) ListenToStrikes(e);
            level.EnemyCalled += ListenToStrikes;
        }

        void ListenToStrikes(Enemy e)
        {
            if (e != null) e.Reported += OnStrikeReported;
        }

        void OnStrikeReported(EnemyReport r)
        {
            if (!InRehearsal || r.Kind != EnemyReportKind.Struck || r.Outcome != StrikeOutcome.Landed) return;
            _strikeKnown = true;
            _strikeElite = r.Elite;
            _strikeKind = r.EnemyKind;
            _strikeAttack = r.Attack;
        }

        /// <summary>Her death or the boss's (from EndRun): the fight's length and what struck her are kept for the end screen; the real run's seed comes back.</summary>
        void RehearsalEnded(string how)
        {
            InRehearsal = false;
            RoomFight fight = _level != null ? FightOf(_level.Layout.ExitRoom) : null;
            RehearsalFightSeconds = fight != null && fight.Starts > 0 ? Time.time - fight.StartedAt : -1f;
            RehearsalKilledBy = how == "death" && _strikeKnown
                ? (_strikeElite ? "elite " : "") + EnemyKinds.Word(_strikeKind) + (string.IsNullOrEmpty(_strikeAttack) ? "" : ": " + _strikeAttack)
                : "";
            RunSeed = _seedBeforeRehearsal;
            Debug.Log("Rehearsal ended (" + how + ")" + (RehearsalFightSeconds >= 0f ? string.Format(s_Inv148, ", the fight {0:0.0} s", RehearsalFightSeconds) : "")
                      + (RehearsalKilledBy.Length > 0 ? ", struck last by " + RehearsalKilledBy : ""));
        }

        /// <summary>
        /// She leaves the rehearsals (from StartRun and BackToPracticeRoom, after a rehearsal going on has ended): the recorder's
        /// rehearsal session ends and an ordinary one begins. Nothing when she was not rehearsing.
        /// </summary>
        void RehearsalOver()
        {
            if (!_rehearsing) return;
            _rehearsing = false;
            InRehearsal = false;
            PlaySessionRecorder.SwitchSession(rehearsal: false);
        }

        /// <summary>
        /// A rehearsal's end screen answered (from AnswerEndScreen): Enter's "Again" a new rehearsal at once, a new seed; the second
        /// answer the main menu (the practice room without it); Esc the practice room. False when the end screen is not a rehearsal's.
        /// </summary>
        bool AnswerRehearsalEnd(EndAnswer answer)
        {
            if (!RehearsalEnding) return false;
            if (answer == EndAnswer.NewRun)
            {
                if (StartRehearsal() == null) BackToPracticeRoom(); // the build gone or the switch off since: back, as Esc
            }
            else if (answer == EndAnswer.SameRun)
            {
                if (!StartMenu.TakeEndScreen(this)) BackToPracticeRoom();
            }
            else BackToPracticeRoom();
            return true;
        }
    }
}
