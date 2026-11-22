using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Roguelike
{
    /// <summary>
    /// The room lab (work order 084; Kristian ticked "Gråboks-bryter og romlab" on 2026-11-22: a room judged by playing it,
    /// changing a value and playing it again), as part of <see cref="GeneratedLevels"/>. A developer's tool, behind the one
    /// switch of the developer's keys (work order 066): off in a built player.
    ///
    /// In the practice room (and in the lab) <see cref="LabKey"/> opens a small picker in the developer's plain box: the room's
    /// KIND (ordinary, elite, treasure, exit), the ROOM of that kind (every template of the library, work order 072's second set
    /// too whatever <see cref="LevelValues.MoreTemplates"/> says, marked "(not in the floors yet)" while the floors do not hold
    /// it; or the generator's drawn shapes, <see cref="RoomLab.Entries"/>) and the ENEMY MIX (<see cref="RoomLab.Mixes"/>: the floors'
    /// own numbers); up and down choose a line, left and right change it, Enter builds, <see cref="LabKey"/> closes it. The room
    /// is built alone (<see cref="RoomLab.Build"/>, the generator's stages) with a short passage on its door in, by the floor's
    /// level builder (<see cref="Build"/>, so <see cref="GeneratedLevel"/> with the kit, the mood, the cave, the door signs and
    /// the room's fight), and she is put in the passage.
    ///
    /// The lab room plays as a room of a floor in a run: it is a run of its own (<see cref="InRun"/>, <see cref="InLab"/>) with
    /// the start skills, full health and nothing taken, on the mix's floor, so the fight wakes at its point, an ordinary room
    /// heals her when cleared, the elite room's upgrade and the exit room's reward land as their drops, a treasure room's chest
    /// opens. Where it is not a floor's: a reward taken leaves her in the lab (no next floor, no focus choice); the act's last
    /// exit room says so and shows no end screen; a death builds the same room again at once with her health full (no death
    /// screen); and no run is told to the recorder: only the lab's own events (<see cref="LabRoomEvents"/>), so the summary
    /// counts the lab in its own table (<see cref="LabRoomsSummary"/>) and not among the runs.
    ///
    /// In the lab: <see cref="LabNewSeedKey"/> the same room with a new seed, <see cref="LabNextKey"/> and
    /// <see cref="LabPreviousKey"/> the next and the previous room of the list (the same seed and mix), F6 the same room and
    /// seed again, <see cref="LabKey"/> the picker, F7 back to the practice room (the lab's floor goes as a floor goes).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The picker. No F-key is free: L, for lab, which no binding of <see cref="InputBindings"/> uses. A developer's key.</summary>
        public const Key LabKey = Key.L;
        /// <summary>In the lab: the same room, a new seed (N, for new).</summary>
        public const Key LabNewSeedKey = Key.N;
        /// <summary>In the lab: the next and the previous room of the list (the keys of . and , in the same place on a Norwegian keyboard as on an English one).</summary>
        public const Key LabNextKey = Key.Period;
        public const Key LabPreviousKey = Key.Comma;

        /// <summary>True while she is in the room lab (a run of its own, <see cref="InRun"/> too).</summary>
        public bool InLab { get; private set; }
        /// <summary>The lab room built last: what it is, its mix, its seed; null before the first.</summary>
        public LabEntry LabEntry { get; private set; }
        public int LabMix { get; private set; }
        public ulong LabSeed { get; private set; }
        /// <summary>Real milliseconds the last lab room took to build: the room's layout, the floor's build, its NavMesh.</summary>
        public float LabBuiltMs { get; private set; }
        /// <summary>True once the lab room is cleared (every enemy dead; a treasure room's chest opened).</summary>
        public bool LabCleared { get; private set; }
        /// <summary>How many lab rooms were built (for tests).</summary>
        public int LabBuilds { get; private set; }

        LevelLayout _labLayout;      // the room the next Build builds in place of a floor's (one call)
        ulong? _runSeedBeforeLab;    // the last real run's seed, given back when the lab ends
        float _labStartedAt;
        LevelRoom _labRoom;

        // ---------------- Intents ----------------

        /// <summary>
        /// Builds a lab room (see the class) and puts her in its passage; a real run going on ends first ("left"). Null without
        /// a character, or when the room cannot be built (said on screen).
        /// </summary>
        public GeneratedLevel StartLab(LabEntry entry, int mix, ulong seed, string how = "rebuilt")
        {
            PlayerCharacter player = FindPlayer();
            if (player == null || entry == null) return null;
            mix = Mathf.Clamp(mix, 0, RoomLab.Mixes.Length - 1);
            var watch = Stopwatch.StartNew();
            LevelLayout layout;
            try { layout = RoomLab.Build(entry, RoomLab.Mixes[mix], seed, Values); }
            catch (InvalidOperationException e)
            {
                Say("ROOM LAB: " + e.Message);
                return null;
            }
            ClosePicker();
            InterfaceStyle.LoadIcons(); // every icon decoded now, none in a frame of play (as a run's start)
            CloseChoice();
            CloseScreens();
            if (InLab) PlaySessionRecorder.Current?.NoteLabLeft(how);
            else
            {
                if (InRun) EndRun("left", "Left the run on floor " + Floor + RoomWords() + "   run " + RunSeedText);
                _runSeedBeforeLab = RunSeed;
            }
            ClearSpawned(); // what the practice room spawned never follows her

            // A run of its own, as StartRun begins one: the start skills, full health, nothing taken; no trial between runs.
            RunSeed = seed;
            InRun = true;
            InLab = true;
            ActCleared = false;
            LastRunLine = "";
            _diedPending = false;
            _metaRun = false;
            _skillsTaken.Clear();
            Modifiers.Clear();
            Modifiers.CooldownFloor = RunValues.CooldownFloor;
            Modifiers.StatsAndTiers = RunValues.StatsAndTiers;
            Modifiers.StatValues = RunValues.Stats;
            player.SeedCrits(seed);
            player.Modifiers = Modifiers;
            SkillUpgrades.Clear();
            player.SkillUpgrades = SkillUpgrades;
            SkillUpgradeOffers = 0;
            FocusChoice.Clear();
            player.RunFocus = FocusChoice;
            player.EquipStartSkills();
            player.StartFresh();
            _diedWithTheAct.Clear();
            if (_health != null) _health.RestoreFull();
            _runStartedAt = Time.time;
            Record = new RunRecord(LevelSeed.ToText(seed), Values.FloorsPerAct);
            Floor = layout.Floor;

            _labLayout = layout;
            GeneratedLevel level;
            try { level = Build(seed, layout.Floor); }
            finally { _labLayout = null; }
            if (level == null) return null;
            RecordFloor(level);
            _floorStartedAt = Time.time;
            LabBuiltMs = (float)watch.Elapsed.TotalMilliseconds;
            LabEntry = entry;
            LabMix = mix;
            LabSeed = seed;
            LabCleared = false;
            LabBuilds++;
            _labStartedAt = Time.time;
            _labRoom = layout.Rooms.Count > 1 ? layout.Rooms[1] : null;
            PlaySessionRecorder.Current?.NoteLabRoom(entry.Name, RoomLab.Word(entry.Kind), RoomLab.Mixes[mix].Name, layout.Floor, LevelSeed.ToText(seed), LabBuiltMs);
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "Room lab: {0} built in {1:0} ms (attempt {2})",
                                    RoomLab.Describe(entry, RoomLab.Mixes[mix], seed), LabBuiltMs, layout.Attempt));
            return level;
        }

        /// <summary>In the lab: the same room with a new seed (N). Null outside it.</summary>
        public GeneratedLevel LabNewSeed() => InLab ? StartLab(LabEntry, LabMix, FreshSeed(), "new seed") : null;

        /// <summary>In the lab: the same room and seed again (F6). Null outside it.</summary>
        public GeneratedLevel LabSameAgain() => InLab ? StartLab(LabEntry, LabMix, LabSeed, "again") : null;

        /// <summary>In the lab: the room <paramref name="step"/> on in the list (<see cref="RoomLab.Entries"/>, round at its ends), the same mix and seed.</summary>
        public GeneratedLevel LabStep(int step)
        {
            if (!InLab) return null;
            List<LabEntry> entries = RoomLab.Entries(Values);
            int at = IndexOf(entries, LabEntry);
            int next = ((at < 0 ? 0 : at) + step % entries.Count + entries.Count) % entries.Count;
            return StartLab(entries[next], LabMix, LabSeed, step > 0 ? "next" : "previous");
        }

        static int IndexOf(List<LabEntry> entries, LabEntry e)
        {
            if (e == null) return -1;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Template == e.Template && entries[i].Kind == e.Kind) return i;
            return -1;
        }

        // ---------------- Where a lab room is not a floor's (the hooks in GeneratedLevels.cs) ----------------

        /// <summary>The run ends (EndRun): the lab's too, told to the recorder as the lab's; the last real run's seed comes back.</summary>
        void LabRunEnded(string how)
        {
            if (!InLab) return;
            InLab = false;
            PlaySessionRecorder.Current?.NoteLabLeft(how);
            LastRunLine = "Left the room lab: " + (LabEntry != null ? RoomLab.Describe(LabEntry, RoomLab.Mixes[LabMix], LabSeed) : "");
            RunSeed = _runSeedBeforeLab;
            _labRoom = null;
        }

        /// <summary>Her death in the lab (SettleDeath): no death screen; the same room is built again at once, her health full.</summary>
        bool LabDied()
        {
            if (!InLab) return false;
            StartLab(LabEntry, LabMix, LabSeed, "died");
            Say("ROOM LAB: died; the same room again");
            return true;
        }

        /// <summary>The floor goes on (FloorGoesOn): in the lab the act's last exit room shows no end screen; any other gives its reward as a floor's.</summary>
        bool LabFloorGoesOn()
        {
            if (!InLab || Floor < Values.FloorsPerAct) return false;
            Say("ROOM LAB: the act's last exit room cleared (in a run, the act is cleared here)");
            return true;
        }

        /// <summary>A floor's reward taken (PickReward): in the lab she stays in the room, with no next floor and no focus choice.</summary>
        bool LabTookTheReward()
        {
            if (!InLab) return false;
            CloseChoice();
            return true;
        }

        // ---------------- Every frame ----------------

        /// <summary>Once a frame in the lab: the room cleared is told once. Allocates nothing.</summary>
        void LabTick()
        {
            if (!InLab || LabCleared || _level == null || _labRoom == null) return;
            bool cleared;
            float from = _labStartedAt;
            if (_labRoom.Kind == RoomKind.Treasure) cleared = _chestsOpened.Contains(_labRoom.Order);
            else
            {
                cleared = _level.EnemiesByRoom.TryGetValue(_labRoom.Order, out List<Enemy> own) && own.Count > 0 && AliveOf(_labRoom.Order) == 0;
                RoomFight fight = FightOf(_labRoom);
                if (fight != null && fight.Starts > 0) from = fight.StartedAt;
            }
            if (!cleared) return;
            LabCleared = true;
            PlaySessionRecorder.Current?.NoteLabCleared(Time.time - from);
        }

        /// <summary>For tests: the tools' frame (the greybox view following its setting, the lab's tick), as Update runs it.</summary>
        public void TickToolsForTests() => TickTools();

        // ---------------- The picker ----------------

        /// <summary>True while the lab's picker is up.</summary>
        public bool PickerOpen { get; private set; }
        /// <summary>The picker's line: 0 the kind, 1 the room, 2 the enemy mix.</summary>
        public int PickerLine { get; private set; }
        public LabKind PickerKind { get; private set; }
        public int PickerRoom { get; private set; }
        public int PickerMix { get; private set; }
        /// <summary>The picker's text, made when it changes.</summary>
        public string PickerText { get; private set; } = "";
        List<LabEntry> _pickerRooms = new List<LabEntry>();
        RoomLabGui _gui;

        /// <summary>Opens the picker, at the lab room's choices or the first room's; in the practice room or the lab only.</summary>
        public bool OpenPicker()
        {
            if (_level != null && !InLab) return false;
            PickerOpen = true;
            if (LabEntry != null)
            {
                PickerKind = LabEntry.Kind;
                PickerMix = LabMix;
            }
            _pickerRooms = RoomLab.EntriesOf(PickerKind, Values);
            PickerRoom = Mathf.Max(0, IndexOf(_pickerRooms, LabEntry));
            if (_gui == null) _gui = gameObject.AddComponent<RoomLabGui>();
            PickerChanged();
            return true;
        }

        public void ClosePicker() => PickerOpen = false;

        /// <summary>Up and down: the picker's line.</summary>
        public void PickerMove(int delta)
        {
            PickerLine = (PickerLine + delta % 3 + 3) % 3;
            PickerChanged();
        }

        /// <summary>Left and right: the line's choice one on or back, round at its ends.</summary>
        public void PickerChange(int delta)
        {
            if (PickerLine == 0)
            {
                int kinds = Enum.GetValues(typeof(LabKind)).Length;
                PickerKind = (LabKind)(((int)PickerKind + delta % kinds + kinds) % kinds);
                _pickerRooms = RoomLab.EntriesOf(PickerKind, Values);
                PickerRoom = 0;
            }
            else if (PickerLine == 1 && _pickerRooms.Count > 0) PickerRoom = (PickerRoom + delta % _pickerRooms.Count + _pickerRooms.Count) % _pickerRooms.Count;
            else if (PickerLine == 2) PickerMix = (PickerMix + delta % RoomLab.Mixes.Length + RoomLab.Mixes.Length) % RoomLab.Mixes.Length;
            PickerChanged();
        }

        /// <summary>Enter: builds the room picked, with a new seed.</summary>
        public GeneratedLevel PickerBuild()
        {
            if (!PickerOpen || _pickerRooms.Count == 0) return null;
            return StartLab(_pickerRooms[Mathf.Clamp(PickerRoom, 0, _pickerRooms.Count - 1)], PickerMix, FreshSeed(), "picked");
        }

        void PickerChanged()
        {
            var sb = new StringBuilder();
            sb.Append("ROOM LAB     up/down a line   left/right change   Enter builds   ").Append(LabKey).Append(" closes\n");
            LabEntry room = _pickerRooms.Count > 0 ? _pickerRooms[Mathf.Clamp(PickerRoom, 0, _pickerRooms.Count - 1)] : null;
            sb.Append(PickerLine == 0 ? "> " : "   ").Append("Kind:     < ").Append(RoomLab.Word(PickerKind)).Append(" >\n");
            sb.Append(PickerLine == 1 ? "> " : "   ").Append("Room:    < ").Append(room != null ? room.Label : "-") // work order 072's second set marked: not in the floors yet
              .Append(string.Format(CultureInfo.InvariantCulture, " >   ({0} of {1})", PickerRoom + 1, _pickerRooms.Count));
            if (room?.Template != null) sb.Append(string.Format(CultureInfo.InvariantCulture, "  {0} x {1} m", room.Template.Width * Values.CellSize, room.Template.Height * Values.CellSize));
            sb.Append('\n');
            sb.Append(PickerLine == 2 ? "> " : "   ").Append("Enemies: < ").Append(RoomLab.Mixes[PickerMix].Name).Append(" >")
              .Append(PickerKind == LabKind.Treasure ? "   (a treasure room holds none)" : "").Append('\n');
            sb.Append("   Seed: a new one each build");
            PickerText = sb.ToString();
        }

        /// <summary>The lab's keys (see the class); true when one was read, and nothing else reads this frame's keys.</summary>
        bool LabKeys(Keyboard k)
        {
            if (PickerOpen)
            {
                if (_level != null && !InLab)
                {
                    ClosePicker(); // a floor came: the picker is the practice room's and the lab's
                    return false;
                }
                if (k[LabKey].wasPressedThisFrame) ClosePicker();
                else if (k.upArrowKey.wasPressedThisFrame) PickerMove(-1);
                else if (k.downArrowKey.wasPressedThisFrame) PickerMove(1);
                else if (k.leftArrowKey.wasPressedThisFrame) PickerChange(-1);
                else if (k.rightArrowKey.wasPressedThisFrame) PickerChange(1);
                else if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) PickerBuild();
                return true;
            }
            if (k[LabKey].wasPressedThisFrame) return OpenPicker();
            if (!InLab) return false;
            if (k[LabNewSeedKey].wasPressedThisFrame) LabNewSeed();
            else if (k[LabNextKey].wasPressedThisFrame) LabStep(1);
            else if (k[LabPreviousKey].wasPressedThisFrame) LabStep(-1);
            else return false;
            return true;
        }

        // ---------------- The developer's box ----------------

        static readonly string s_labKeys = LabNewSeedKey + " new seed   , previous   . next   F6 again" + "\n" + LabKey + " picker   F7 leave the lab";

        void AppendLab(StringBuilder text)
        {
            if (!InLab || LabEntry == null) return;
            text.Append("\nROOM LAB  ").Append(RoomLab.Word(LabEntry.Kind)).Append(": ").Append(LabEntry.Name);
            if (!LabEntry.InFloors) text.Append("\n  ").Append(RoomLab.NotInTheFloors); // work order 072's second set, off in the floors
            text.Append(string.Format(CultureInfo.InvariantCulture, "\n  {0}, seed {1}, built in {2:0} ms{3}", RoomLab.Mixes[LabMix].Name, LevelSeed.ToText(LabSeed), LabBuiltMs,
                                      LabCleared ? ", cleared" : ""));
            text.Append('\n').Append(s_labKeys);
        }
    }

    /// <summary>
    /// The room lab's picker on screen (work order 084): the developer's plain box (<see cref="GUI.Box"/>, the old skin, as the
    /// developer's box is), its text made when it changes, drawn while the picker is up. A tool, not interface.
    /// </summary>
    public sealed class RoomLabGui : MonoBehaviour
    {
        GeneratedLevels _levels;
        GUIStyle _style;

        void Awake() => _levels = GetComponent<GeneratedLevels>();

        void OnGUI()
        {
            if (_levels == null || !_levels.PickerOpen) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = false };
            GUI.depth = -50;
            float scale = InterfaceStyle.ScreenHeight / SkillBar.LayoutHeight;
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = InterfaceStyle.ScreenWidth / scale;
            const float width = 640f, height = 118f; // wide enough for a room's name with "(not in the floors yet)" after it
            GUI.Box(new Rect((viewWidth - width) * 0.5f, SkillBar.LayoutHeight * 0.22f, width, height), _levels.PickerText, _style);
            GUI.matrix = before;
        }
    }
}
