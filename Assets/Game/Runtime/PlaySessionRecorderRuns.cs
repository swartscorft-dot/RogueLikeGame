using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// What <see cref="PlaySessionRecorder"/> is told of the floors and the runs: a floor built, a room entered, a run's start,
    /// floors, rewards, chests, elite rooms, focus choice and end, and what lasts between runs (work order 081 moved them here
    /// from PlaySessionRecorder.cs, code unchanged).
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A generated level was built (<see cref="GeneratedLevels"/>): writes its seed, version and number of rooms,
        /// and starts listening to the enemies it brought.
        /// </summary>
        public void NoteLevelBuilt(string seedText, int version, int rooms)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.LevelBuilt, seedText);
                e.AddExtra("version", version.ToString(CultureInfo.InvariantCulture));
                e.AddExtra("rooms", rooms.ToString(CultureInfo.InvariantCulture));
                Add(e);
                if (_player != null) FindEnemies();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>
        /// She entered a room of the generated level: its place in the run (from 0) and its kind, and (work order 028) its
        /// theme in words when it has one.
        /// </summary>
        public void NoteRoomEntered(int room, string kind, string theme = null)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.RoomEntered, kind);
                e.AddExtra("room", room.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(theme)) e.AddExtra("theme", theme);
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------- Runs (work order 012) ----------------

        void Note(string type, string name, params string[] extras)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), type, name);
                for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>A run started, with this seed; and her basic shot's interval in it (work order 055).</summary>
        public void NoteRunStarted(string runSeed)
        {
            Note(SessionEventTypes.RunStart, runSeed);
            _frameRuns++; // work order 074: the frame times' parts say which run
            NoteShotInterval();
        }

        float _shotInterval = -1f;

        /// <summary>
        /// Her basic shot's interval (<see cref="PlayerCharacter.BasicShotInterval"/>), written when it is not the one last
        /// written: when she is found, at a run's start, and when her attack speed changes it (the fourth review of
        /// 2026-11-21, work order 055: the summary measured the shot against the feel's 0.225 s).
        /// </summary>
        void NoteShotInterval()
        {
            if (!_recording || _player == null) return;
            float interval = _player.BasicShotInterval;
            if (Mathf.Abs(interval - _shotInterval) < 1e-5f) return;
            _shotInterval = interval;
            Note(SessionEventTypes.ShotInterval, null, "seconds", interval.ToString("0.#####", CultureInfo.InvariantCulture));
        }

        /// <summary>She entered floor <paramref name="floor"/> of the run, of this seed.</summary>
        public void NoteFloorEntered(int floor, string seed)
        {
            EndFramePart(FrameTimeEvents.PartFloor, _frameRuns, floor); // work order 074: the part before ends, this floor's begins
            Note(SessionEventTypes.FloorEnter, seed, "floor", Int(floor));
        }

        /// <summary>The floor's exit room was cleared: how long the floor took (game time: a choice's pause does not count) and the health she had left.</summary>
        public void NoteFloorFinished(int floor, float seconds, int health) =>
            Note(SessionEventTypes.FloorFinish, null, "floor", Int(floor), "seconds", Text(seconds), "health", Int(health));

        /// <summary>The floor reward offered: the skills, in the order shown; work order 057: with <paramref name="tiers"/> given (the skill tiers on), each card's tier too.</summary>
        public void NoteRewardOffered(int floor, IList<string> skills, IList<string> tiers = null)
        {
            if (tiers == null) Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", skills));
            else Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", skills), "tiers", string.Join(" | ", tiers));
        }

        /// <summary>A floor reward taken; work order 057: a rare or epic card's tier, and an epic's change by name ("" for a rare).</summary>
        public void NoteRewardTaken(int floor, string skill, string tier = null, string with = null)
        {
            if (string.IsNullOrEmpty(tier) || tier == UpgradeTiers.Word(UpgradeTier.Common)) Note(SessionEventTypes.RewardTake, skill, "floor", Int(floor));
            else Note(SessionEventTypes.RewardTake, skill, "floor", Int(floor), "tier", tier, "with", with ?? "");
        }

        /// <summary>Work order 023: she was healed as floor <paramref name="floor"/>'s reward was taken: what was given, and her health after.</summary>
        public void NoteFloorHeal(int floor, int healed, int health) =>
            Note(SessionEventTypes.FloorHeal, null, "floor", Int(floor), "healed", Int(healed), "health", Int(health));

        /// <summary>Every enemy of a combat room is dead and she was healed (work order 030): the room, whether it is the elite room, what was given and her health after.</summary>
        public void NoteRoomHeal(int floor, int room, bool elite, int healed, int health) =>
            Note(SessionEventTypes.RoomHeal, null, "floor", Int(floor), "room", Int(room), "elite", elite ? "true" : "false", "healed", Int(healed), "health", Int(health));

        /// <summary>
        /// A room's fight started (work order 070): every enemy of the room woke at once. The floor, the room counted from 1
        /// without the passages (as the run box counts), its place in the floor, its kind (combat, elite, exit), how it started
        /// (point, seen, hit, provoked), whether she was inside the room then, and how many woke. Work order 135: with a line-up
        /// (<paramref name="lineUp"/>, its name, or "full" for the floor's full room) its name as "line_up"; none written without one.
        /// </summary>
        public void NoteRoomFight(int floor, int room, int order, string kind, string how, bool inside, int woke, string lineUp = null)
        {
            if (lineUp == null)
                Note(SessionEventTypes.RoomFight, how, "floor", Int(floor), "room", Int(room), "order", Int(order), "kind", kind,
                     "inside", inside ? "true" : "false", "woke", Int(woke));
            else
                Note(SessionEventTypes.RoomFight, how, "floor", Int(floor), "room", Int(room), "order", Int(order), "kind", kind,
                     "inside", inside ? "true" : "false", "woke", Int(woke), "line_up", lineUp);
        }

        /// <summary>
        /// A chest opened: what it offered, and what was taken (short forms). Work order 050: for a tiered pick also its
        /// stat, tier and size (its amount, and its heal when it heals), and her stats after it in one line.
        /// </summary>
        public void NoteChestOpened(int floor, int room, IList<string> offered, string taken, Upgrade tiered = null, string stats = null)
        {
            if (tiered == null)
            {
                Note(SessionEventTypes.ChestOpen, taken, "floor", Int(floor), "room", Int(room), "offered", string.Join(" | ", offered));
                return;
            }
            Note(SessionEventTypes.ChestOpen, taken, "floor", Int(floor), "room", Int(room), "offered", string.Join(" | ", offered),
                 "stat", tiered.Stat, "tier", tiered.TierWord, "size", Text(tiered.Amount), "heal", Int(tiered.Heal), "stats", stats ?? "");
        }

        /// <summary>She entered the floor's elite room for the first time (work order 017).</summary>
        public void NoteEliteEntered(int floor, int room) => Note(SessionEventTypes.EliteEnter, null, "floor", Int(floor), "room", Int(room));

        /// <summary>The elite room was cleared (work order 017): the seconds since she first entered it (game time), and the health she had left.</summary>
        public void NoteEliteCleared(int floor, float seconds, int health) =>
            Note(SessionEventTypes.EliteClear, null, "floor", Int(floor), "seconds", Text(seconds), "health", Int(health));

        /// <summary>The skill upgrades offered (work order 017), "Skill: Name (kind)" in the order shown; none, and a heal instead.</summary>
        public void NoteSkillUpgradeOffered(int floor, IList<string> offered, bool heal) =>
            Note(SessionEventTypes.SkillUpgradeOffer, null, "floor", Int(floor), "offered", string.Join(" | ", offered), "heal", heal ? "true" : "false");

        /// <summary>A skill upgrade was taken (work order 017): its full name and kind.</summary>
        public void NoteSkillUpgradeTaken(int floor, string fullName, string kind) =>
            Note(SessionEventTypes.SkillUpgradeTake, fullName, "floor", Int(floor), "kind", kind);

        /// <summary>The focus choice offered (work order 025): the two ways, in the order shown.</summary>
        public void NoteFocusChoiceOffered(int floor, IList<string> offered) =>
            Note(SessionEventTypes.FocusChoiceOffer, null, "floor", Int(floor), "offered", string.Join(" | ", offered));

        /// <summary>The focus choice taken (work order 025), by its name.</summary>
        public void NoteFocusChoiceTaken(int floor, string name) => Note(SessionEventTypes.FocusChoiceTake, name, "floor", Int(floor));

        /// <summary>The run ended: how (death, act cleared, left), where (floor, room from 1 and its kind), and when.</summary>
        public void NoteRunEnded(string how, int floor, int room, string kind, float seconds, string stats = null)
        {
            EndFramePart(FrameTimeEvents.PartPracticeRoom, 0, 0); // work order 074: the floor's frames end with the run
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.RunEnd) { Result = how };
                e.AddExtra("floor", Int(floor));
                e.AddExtra("room", Int(room));
                if (!string.IsNullOrEmpty(kind)) e.AddExtra("kind", kind);
                e.AddExtra("seconds", Text(seconds));
                if (!string.IsNullOrEmpty(stats)) e.AddExtra("stats", stats); // work order 050: her final stats
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------- Between runs (work order 029; only with that trial switched on) ----------------

        /// <summary>
        /// A run started with the rack switched on: the pieces equipped, the currency then, the budget and the points used.
        /// Work order 060: <paramref name="weapon"/>, her pistols' parts fitted joined by " | " (or "stock"), only when the
        /// attachments are on; null, the event is as before.
        /// </summary>
        public void NoteBetweenRunsLoadout(IList<string> pieces, int currency, int budget, int used, string weapon = null, string ranks = null)
        {
            // Work order 099: the ranks she starts with ("Health 3, Damage 1"), only when she has any; none, the event is as before.
            if (ranks != null)
            {
                if (weapon == null) Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used), "ranks", ranks);
                else Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used), "weapon", weapon, "ranks", ranks);
                return;
            }
            if (weapon == null) Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used));
            else Note(SessionEventTypes.MetaLoadout, string.Join(" | ", pieces), "currency", Int(currency), "budget", Int(budget), "used", Int(used), "weapon", weapon);
        }

        /// <summary>Work order 099: a rank bought on the screen between runs: the stat's name, the rank it is now, its price, the Echoes left.</summary>
        public void NoteBetweenRunsRank(string stat, int rank, int price, int left) =>
            Note(SessionEventTypes.MetaRank, stat, "rank", Int(rank), "price", Int(price), "left", Int(left));

        /// <summary>The run earned currency: how much, from what (floor, elite room, act boss), on which floor.</summary>
        public void NoteBetweenRunsEarned(int amount, string from, int floor) =>
            Note(SessionEventTypes.MetaEarn, from, "amount", Int(amount), "floor", Int(floor));

        /// <summary>A feat done for the first time, by its name in the save, when the run ended.</summary>
        public void NoteBetweenRunsFeat(string feat) => Note(SessionEventTypes.MetaFeat, feat);
    }
}
