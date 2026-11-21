using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>How a run ended, or that it goes on (work order 018).</summary>
    public enum RunOutcome { Going, Died, ActCleared, Left }

    /// <summary>
    /// What a run has done so far, as plain data from one place (work order 018, 2026-11-20): the HUD, the death screen
    /// and the act-cleared screen read it, and a later order can add to it. It is filled as the run goes by the run
    /// itself (<c>GeneratedLevels</c>): a new record when a run starts, the floor and the room she is in, the skills taken
    /// as floor rewards in order, the chest upgrades taken (short forms), the kills and the time; and how it ended, with
    /// the health she had then. Work order 017: the skill upgrades taken from elite rooms, in order, and whether the room
    /// she is in is the elite room. Work order 025: the focus choice taken after floor 1's reward.
    ///
    /// A kill is an enemy of the run's floors reporting its death: the same report the play session recorder counts
    /// kills from, so the two never disagree on what a kill is. The time is game time from the start of the run, so the
    /// pauses (a choice, the pause menu, the end's slow-down) do not count, as in the recorder's floor times.
    /// </summary>
    public sealed class RunRecord
    {
        readonly List<string> _skills = new List<string>();
        readonly List<string> _upgrades = new List<string>();
        readonly List<string> _skillUpgrades = new List<string>();

        public RunRecord(string seed, int floorsPerAct)
        {
            Seed = seed ?? "";
            FloorsPerAct = floorsPerAct;
        }

        /// <summary>The run's seed, as eight characters.</summary>
        public string Seed { get; }
        public int FloorsPerAct { get; }
        /// <summary>The floor she is on (or ended on), from 1; 0 before the first.</summary>
        public int Floor { get; private set; }
        /// <summary>The rooms of that floor.</summary>
        public int Rooms { get; private set; }
        /// <summary>The last room of the floor she was in, by its place in the floor (from 0); -1 before she was in one.</summary>
        public int Room { get; private set; } = -1;
        public RoomKind RoomKind { get; private set; }
        /// <summary>True when that room is the floor's elite room (work order 017).</summary>
        public bool RoomElite { get; private set; }
        /// <summary>Seconds of game time since the run started; frozen when it ends.</summary>
        public float Seconds { get; private set; }
        public int Kills { get; private set; }
        /// <summary>The skills taken as floor rewards, in order.</summary>
        public IReadOnlyList<string> Skills => _skills;
        /// <summary>The chest upgrades taken, in short form, in order.</summary>
        public IReadOnlyList<string> Upgrades => _upgrades;
        public int ChestsOpened => _upgrades.Count;
        /// <summary>The skill upgrades taken (work order 017), "Quick Shot: Double Tap", in order.</summary>
        public IReadOnlyList<string> SkillUpgrades => _skillUpgrades;
        public RunOutcome Outcome { get; private set; } = RunOutcome.Going;
        public bool IsOver => Outcome != RunOutcome.Going;
        /// <summary>The focus choice taken (work order 025), "Hair Trigger"; empty until it is taken.</summary>
        public string FocusChoice { get; private set; } = "";
        /// <summary>
        /// Every pick of the run: the skills, the chest upgrades, the skill upgrades and the focus choice. The one count by
        /// which the placeholder sounds and the effects both find a pick (the second reviews of 2026-11-20, work order
        /// 033: the effects did not count the skill upgrades), so the two cannot drift apart again.
        /// </summary>
        public int Picks => _skills.Count + _upgrades.Count + _skillUpgrades.Count + (FocusChoice.Length > 0 ? 1 : 0);
        /// <summary>Her health and maximum when the run ended; 0 while it goes on.</summary>
        public int HealthLeft { get; private set; }
        public int MaxHealth { get; private set; }

        /// <summary>She is on floor <paramref name="floor"/> now, of <paramref name="rooms"/> rooms; the room is not known yet.</summary>
        public void EnterFloor(int floor, int rooms)
        {
            if (IsOver) return;
            Floor = floor;
            Rooms = rooms;
            Room = -1;
            RoomKind = RoomKind.Entrance;
            RoomElite = false;
        }

        /// <summary>She is in this room of the floor now; <paramref name="elite"/> for the floor's elite room (work order 017).</summary>
        public void EnterRoom(int order, RoomKind kind, bool elite = false)
        {
            if (IsOver) return;
            Room = order;
            RoomKind = kind;
            RoomElite = elite;
        }

        /// <summary>
        /// A skill taken as a floor reward; work order 057: with the tier it was taken at and an epic's change, which
        /// <see cref="SkillsText"/> shows beside it (<see cref="Skills"/> stays the names).
        /// </summary>
        public void TakeSkill(string name, UpgradeTier tier = UpgradeTier.Common, string change = null)
        {
            if (IsOver) return;
            _skills.Add(name ?? "?");
            _skillsWith.Add(SkillTiers.WithText(tier, change));
        }

        readonly List<string> _skillsWith = new List<string>();

        public void TakeUpgrade(string shortForm)
        {
            if (!IsOver) _upgrades.Add(shortForm ?? "?");
        }

        /// <summary>A skill upgrade taken from an elite room (work order 017), by its full name.</summary>
        public void TakeSkillUpgrade(string fullName)
        {
            if (!IsOver) _skillUpgrades.Add(fullName ?? "?");
        }

        /// <summary>The last heal between floors (work order 023): how much, and after which floor; 0 before the first.</summary>
        public int LastFloorHeal { get; private set; }
        public int LastFloorHealFloor { get; private set; }

        /// <summary>She was healed by <paramref name="amount"/> when floor <paramref name="floor"/>'s reward was taken (work order 023).</summary>
        public void FloorHealed(int floor, int amount)
        {
            if (IsOver) return;
            LastFloorHeal = Math.Max(0, amount);
            LastFloorHealFloor = floor;
        }

        /// <summary>"+35 health after floor 2", for the line of what was taken; empty before the first heal or for one of nothing.</summary>
        public string FloorHealText =>
            LastFloorHeal <= 0 ? "" : string.Format(CultureInfo.InvariantCulture, "+{0} health after floor {1}", LastFloorHeal, LastFloorHealFloor);

        /// <summary>The focus choice was taken (work order 025), by its name.</summary>
        public void TakeFocusChoice(string name)
        {
            if (!IsOver) FocusChoice = name ?? "?";
        }

        /// <summary>
        /// An enemy of the run's floors died. Counted also just after the end: the last enemies of the exit room report
        /// their deaths on the frames after the floor is seen to be cleared.
        /// </summary>
        public void Kill() => Kills++;

        /// <summary>The game time since the start, while the run goes on.</summary>
        public void SetTime(float seconds)
        {
            if (!IsOver) Seconds = Math.Max(0f, seconds);
        }

        /// <summary>The run ends: how, when, and with what health. Only the first end counts.</summary>
        public void End(RunOutcome outcome, float seconds, int health, int maxHealth)
        {
            if (IsOver || outcome == RunOutcome.Going) return;
            Seconds = Math.Max(0f, seconds);
            Outcome = outcome;
            HealthLeft = Math.Max(0, health);
            MaxHealth = Math.Max(0, maxHealth);
        }

        /// <summary>"Floor 2 of 5".</summary>
        public string FloorText => RunWords.Floor(Floor, FloorsPerAct);

        /// <summary>The room in words (<see cref="RunWords.Room"/>); empty before she was in one.</summary>
        public string RoomText => Room < 0 ? "" : RunWords.Room(RoomKind, Room, Rooms, RoomElite);

        /// <summary>The upgrades taken in short form, the same ones counted together: "+10 hp x2, +5% dmg"; "none" without.</summary>
        public string UpgradesText => RunWords.Grouped(_upgrades);

        /// <summary>
        /// The skills taken in order, "Fan Fire, Long Shot"; "none" without. Work order 057: a rare or epic one with its tier
        /// and an epic's change, "Long Shot (epic, Overcharge), Fan Fire (rare), Slide".
        /// </summary>
        public string SkillsText => _skills.Count == 0 ? "none" : string.Join(", ", _skills.Select((s, i) => SkillTiers.Named(s, _skillsWith[i])));

        /// <summary>The skill upgrades taken in order (work order 017), "Quick Shot: Double Tap, Backstep: Long Step"; "none" without.</summary>
        public string SkillUpgradesText => _skillUpgrades.Count == 0 ? "none" : string.Join(", ", _skillUpgrades);

        /// <summary>The focus choice (work order 025): its name, or "not chosen" before it is taken.</summary>
        public string FocusChoiceText => FocusChoice.Length == 0 ? "not chosen" : FocusChoice;
    }

    /// <summary>The words the HUD and the end screens use (work order 018).</summary>
    public static class RunWords
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        public static string Floor(int floor, int floors) => string.Format(s_Inv, "Floor {0} of {1}", floor, floors);

        /// <summary>
        /// A room in words: the treasure rooms "Treasure room", the exit room "Exit room", the entrance "Entrance", and
        /// any other "Room 3 of 6", numbered by its place in the floor from 1 (the entrance is room 1); the elite room
        /// "Elite room, 4 of 8" (work order 017).
        /// </summary>
        public static string Room(RoomKind kind, int order, int rooms, bool elite = false)
        {
            if (elite) return string.Format(s_Inv, "Elite room, {0} of {1}", order + 1, rooms);
            switch (kind)
            {
                case RoomKind.Treasure: return "Treasure room";
                case RoomKind.Exit: return "Exit room";
                case RoomKind.Entrance: return "Entrance";
                default: return string.Format(s_Inv, "Room {0} of {1}", order + 1, rooms);
            }
        }

        /// <summary>Enemies left in her room: "3 enemies left", "1 enemy left", "Room clear".</summary>
        public static string EnemiesLeft(int alive) =>
            alive <= 0 ? "Room clear" : alive == 1 ? "1 enemy left" : string.Format(s_Inv, "{0} enemies left", alive);

        /// <summary>A run's time as a clock: "0:07", "12:34"; with tenths "0:07.3".</summary>
        public static string Clock(float seconds, bool tenths = false)
        {
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            if (tenths)
            {
                double t = Math.Floor(seconds * 10.0) / 10.0;
                int m = (int)(t / 60.0);
                return m.ToString(s_Inv) + ":" + (t - m * 60.0).ToString("00.0", s_Inv);
            }
            int whole = (int)Math.Floor(seconds);
            return (whole / 60).ToString(s_Inv) + ":" + (whole % 60).ToString("00", s_Inv);
        }

        /// <summary>Short forms with the same ones counted together, in the order first taken: "+10 hp x2, +5% dmg"; "none" without.</summary>
        public static string Grouped(IEnumerable<string> shortForms)
        {
            List<string> list = (shortForms ?? Enumerable.Empty<string>()).ToList();
            if (list.Count == 0) return "none";
            var sb = new StringBuilder();
            foreach (IGrouping<string, string> g in list.GroupBy(s => s))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(g.Key);
                if (g.Count() > 1) sb.Append(" x").Append(g.Count().ToString(s_Inv));
            }
            return sb.ToString();
        }
    }
}
