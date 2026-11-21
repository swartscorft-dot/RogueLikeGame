using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// What lasts between runs, a first version behind a switch (work order 029, 2026-11-20; decision 0009, a proposal by
    /// the producer that Kristian has not ruled on), as part of <see cref="GeneratedLevels"/>. The rules are
    /// <see cref="MetaRack"/>'s; the screen is drawn by the HUD (<see cref="RunHud"/>).
    ///
    /// The switch (<see cref="MetaOn"/>): <see cref="MetaValues.Enabled"/>, false in code, unless the pause menu's line
    /// "Between runs (trial)" says otherwise (<see cref="BetweenRunsSwitch"/>, kept in PlayerPrefs; read only when the game
    /// installs itself in play, never in a test run). Off, nothing here does anything: no run is a meta run, nothing is
    /// earned or shown, the save is neither read nor written, and F5, F6 and the end screens start runs as before.
    ///
    /// On:
    /// - A run started with it on earns (<see cref="Earnings"/>): each floor cleared, each elite room cleared, the act boss;
    ///   and does feats. When the run ends, by death, the act cleared or leaving, what it earned and its new feats are kept
    ///   and the save is written (<see cref="BetweenRunsSavePath"/>).
    /// - The screen between runs (<see cref="BetweenRunsUp"/>) comes up where a run would have started from the practice room
    ///   (F5, F6, the pause menu's "Start a run") and when an end screen is answered with a new run or the same run; she is
    ///   put back in the practice room under it. The game is paused (time scale 0, held every frame, the scale from before
    ///   given back), no key reaches her, and nothing hurts her, as under the pause menu. Its start (<see cref="StartFromBetweenRuns"/>)
    ///   starts the run it was opened for: a new seed, or the same one again. Esc leaves it to the practice room.
    /// - When a run starts, the equipped pieces are put on the run's modifiers (<see cref="RunModifiers.Meta"/>), read where
    ///   each is used; nothing is written to a settings asset or to what the practice room reads.
    ///
    /// The intents, for tests and the HUD: <see cref="OpenBetweenRuns"/>, <see cref="StartFromBetweenRuns"/>,
    /// <see cref="LeaveBetweenRuns"/>, <see cref="BuyPiece"/>, <see cref="EquipPiece"/>, <see cref="TakeOffPiece"/>,
    /// <see cref="UsePiece"/>, <see cref="ChangeStruckOut"/>, and in a run <see cref="RedrawOffer"/>.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The save's file name, under <see cref="Application.persistentDataPath"/>.</summary>
        public const string BetweenRunsFileName = "between-runs.json";
        /// <summary>
        /// Second Look's key on the floor reward's offer: draw the skills again. X, bound to nothing and no developer key; Tab
        /// was the map's key too, and spent the redraw when pressed to look at the map (the third review of 2026-11-20, work
        /// order 047).
        /// </summary>
        public const Key RedrawKey = Key.X;

        /// <summary>The numbers of what lasts between runs, and the switch (work order 029).</summary>
        public MetaValues MetaValues = new MetaValues();

        /// <summary>
        /// Where the save is written and read: null for the game's own (<see cref="DefaultBetweenRunsSavePath"/>). Tests set
        /// a temporary one before they turn the switch on, and put null back.
        /// </summary>
        public static string BetweenRunsSavePath { get; set; }
        public static string DefaultBetweenRunsSavePath => Path.Combine(Application.persistentDataPath, BetweenRunsFileName);
        public static string BetweenRunsSavePathInUse => string.IsNullOrEmpty(BetweenRunsSavePath) ? DefaultBetweenRunsSavePath : BetweenRunsSavePath;

        /// <summary>True when what lasts between runs is on: the switch in code, or the pause menu's line.</summary>
        public bool MetaOn => BetweenRunsSwitch.IsOn(MetaValues.Enabled);

        /// <summary>What lasts, read from the save the first time it is asked for; only ever asked for with the switch on.</summary>
        public MetaState BetweenRunsState
        {
            get
            {
                if (_metaState == null) LoadBetweenRuns();
                return _metaState;
            }
        }

        /// <summary>How the save was read, once it was.</summary>
        public MetaLoad BetweenRunsLoad { get; private set; }

        /// <summary>True while the screen between runs is up.</summary>
        public bool BetweenRunsUp { get; private set; }
        /// <summary>The run seed the screen starts: the same run again, or null for a new one.</summary>
        public ulong? BetweenRunsSeed { get; private set; }
        /// <summary>A short line on the screen about the last thing tried on it ("Not enough Echoes"); empty when none.</summary>
        public string BetweenRunsNote { get; private set; } = "";

        /// <summary>
        /// What the run going on has earned, or the last one; null when that run was not started with the switch on. Kept
        /// after the run ends, with its new feats and the total, for the end screen.
        /// </summary>
        public RunEarnings Earnings { get; private set; }

        MetaState _metaState;
        bool _metaRun;          // the run going on started with the switch on
        bool _eliteEntrySeen;
        bool _eliteDipped;      // under half health in this floor's elite room, while she was in it (work order 033)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LoadBetweenRunsSwitch()
        {
            // The pause menu's choice is read only in play, never in a test run, under the same rule as the recorder.
            if (SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) BetweenRunsSwitch.Load();
        }

        void LoadBetweenRuns()
        {
            string path = BetweenRunsSavePathInUse;
            _metaState = MetaSaveFile.Load(path, MetaValues, out MetaLoad how, out string keptAs);
            BetweenRunsLoad = how;
            if (how == MetaLoad.Unreadable)
                Debug.LogWarning("Between runs: the save at " + path + " is there but could not be read now. What lasts between runs does nothing "
                                 + "until it can be: nothing is earned or saved over it, and it is tried again the next time it is needed.");
            else if (how == MetaLoad.Damaged || how == MetaLoad.Newer)
                Debug.LogWarning("Between runs: the save at " + path + " could not be used (" + how + ")"
                                 + (keptAs.Length > 0 ? "; kept as " + keptAs : "") + ". Starting from an empty save.");
            else Debug.Log("Between runs: save " + (how == MetaLoad.Read ? "read from " : "not found, an empty one at ") + path);
        }

        /// <summary>
        /// The save is needed now (a run starts, the screen opens): read it if it never was, or if it could not be read the
        /// last time (the second reviews of 2026-11-20, work order 033). False while it still cannot be read.
        /// </summary>
        bool BetweenRunsReadable()
        {
            if (_metaState == null || BetweenRunsLoad == MetaLoad.Unreadable) LoadBetweenRuns();
            return BetweenRunsLoad != MetaLoad.Unreadable;
        }

        void SaveBetweenRuns()
        {
            if (!MetaOn || _metaState == null) return;
            if (BetweenRunsLoad == MetaLoad.Unreadable)
            {
                // Work order 033: never an empty save written over one that could not be read.
                Debug.LogWarning("Between runs: not saved, the save at " + BetweenRunsSavePathInUse + " could not be read");
                return;
            }
            if (!MetaSaveFile.Save(BetweenRunsSavePathInUse, _metaState, out string error))
                Debug.LogWarning("Between runs: the save could not be written: " + error);
        }

        /// <summary>The pool skills, by name: the skills a run does not start with. Not That One strikes one of them out.</summary>
        public List<string> PoolSkillNames()
        {
            PlayerCharacter player = FindPlayer();
            var names = new List<string>();
            if (player == null) return names;
            foreach (SkillDefinition s in player.SettingsSkills)
                if (s != null && !s.StartEquipped) names.Add(s.Name);
            return names;
        }

        /// <summary>The pool skill Not That One strikes out now: the one kept in the save, or the pool's first.</summary>
        public string StruckOutSkill => MetaRack.StruckOut(BetweenRunsState.StruckOut, PoolSkillNames());

        // ---------------- The screen (work order 029) ----------------

        /// <summary>
        /// Opens the screen between runs, to start <paramref name="seed"/> (null: a new seed). She is put back in the practice
        /// room first, and any end screen, pause or choice closes. False with the switch off, in a run, when it is already up,
        /// or without a character.
        /// </summary>
        public bool OpenBetweenRuns(ulong? seed = null)
        {
            if (!MetaOn || InRun || BetweenRunsUp || FindPlayer() == null) return false;
            CloseChoice();
            if (_level != null) BackToPracticeRoom(); // closes the screens, brings her back to life, removes the floor
            else CloseScreens();
            BetweenRunsReadable(); // read the save now, again if it could not be read before (work order 033)
            BetweenRunsSeed = seed;
            BetweenRunsNote = "";
            _screenScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            BetweenRunsUp = true;
            Time.timeScale = 0f;
            PlayerInputDriver.HeldByScreen = true;
            if (_health != null) _health.Untouchable = true;
            Debug.Log("Between runs: the screen is up (" + (seed.HasValue ? "the same run " + LevelSeed.ToText(seed.Value) : "a new run") + ")");
            return true;
        }

        /// <summary>The screen's start: the run it was opened for begins at floor 1, with the pieces equipped. Null when it is not up.</summary>
        public GeneratedLevel StartFromBetweenRuns()
        {
            if (!BetweenRunsUp) return null;
            return StartRun(BetweenRunsSeed ?? FreshSeed());
        }

        /// <summary>Esc on the screen: it closes, and she is in the practice room. False when it is not up.</summary>
        public bool LeaveBetweenRuns()
        {
            if (!BetweenRunsUp) return false;
            CloseScreens();
            return true;
        }

        /// <summary>Buys a piece on the screen, if it may be bought; the save is written.</summary>
        public MetaBuy BuyPiece(string id)
        {
            if (!BetweenRunsUp) return MetaBuy.Unknown;
            MetaBuy r = MetaRack.Buy(BetweenRunsState, MetaValues, id);
            BetweenRunsNote = BuyWords(r, id);
            if (r == MetaBuy.Bought) RackChanged("bought " + id);
            return r;
        }

        /// <summary>Equips a piece on the screen, if it is owned and fits the budget; the save is written.</summary>
        public MetaEquip EquipPiece(string id)
        {
            if (!BetweenRunsUp) return MetaEquip.Unknown;
            MetaEquip r = MetaRack.Equip(BetweenRunsState, MetaValues, id);
            BetweenRunsNote = EquipWords(r, id);
            if (r == MetaEquip.Equipped) RackChanged("equipped " + id);
            return r;
        }

        /// <summary>Takes a piece off on the screen (and any that needs it); the save is written.</summary>
        public bool TakeOffPiece(string id)
        {
            if (!BetweenRunsUp || !MetaRack.TakeOff(BetweenRunsState, MetaValues, id)) return false;
            BetweenRunsNote = "";
            RackChanged("took off " + id);
            return true;
        }

        /// <summary>A click or Enter on a piece: takes it off if it is equipped, else equips it if owned, else buys it.</summary>
        public bool UsePiece(string id)
        {
            if (!BetweenRunsUp || MetaRack.Find(MetaValues, id) == null) return false;
            MetaState s = BetweenRunsState;
            if (s.IsEquipped(id)) return TakeOffPiece(id);
            if (s.Owns(id)) return EquipPiece(id) == MetaEquip.Equipped;
            return BuyPiece(id) == MetaBuy.Bought;
        }

        /// <summary>Not That One's skill, one step along the pool either way (the left and right arrows); the save is written.</summary>
        public string ChangeStruckOut(int step)
        {
            if (!BetweenRunsUp) return "";
            string next = MetaRack.StruckOut(StruckOutSkill, PoolSkillNames(), step);
            if (next != BetweenRunsState.StruckOut)
            {
                BetweenRunsState.StruckOut = next;
                RackChanged("struck out " + next);
            }
            return next;
        }

        void RackChanged(string what)
        {
            SaveBetweenRuns();
            Debug.Log("Between runs: " + what);
        }

        string BuyWords(MetaBuy r, string id)
        {
            MetaPiece p = MetaRack.Find(MetaValues, id);
            string name = p != null ? p.Name : id;
            switch (r)
            {
                case MetaBuy.Bought: return "Bought " + name + ".";
                case MetaBuy.NotEnough: return "Not enough " + MetaValues.CurrencyName + " for " + name + ".";
                case MetaBuy.NeedsFirst: return name + " needs " + (MetaRack.Find(MetaValues, p?.Needs)?.Name ?? "another piece") + " first.";
                case MetaBuy.NotForSale: return name + " is not sold: it comes from a feat.";
                case MetaBuy.AlreadyOwned: return name + " is owned already.";
                default: return "";
            }
        }

        string EquipWords(MetaEquip r, string id)
        {
            MetaPiece p = MetaRack.Find(MetaValues, id);
            string name = p != null ? p.Name : id;
            switch (r)
            {
                case MetaEquip.OverBudget: return "Not enough points left for " + name + ": take something off first.";
                case MetaEquip.NeedsFirst: return name + " needs " + (MetaRack.Find(MetaValues, p?.Needs)?.Name ?? "another piece") + " equipped.";
                case MetaEquip.NotOwned: return name + " is not owned.";
                default: return "";
            }
        }

        // ---------------- In a run (work order 029) ----------------

        /// <summary>
        /// Called by <see cref="StartNewRun"/> and <see cref="RestartRun"/>: with the switch on and no run going on, the screen
        /// comes up instead of the run. False (start the run) otherwise, and always with the switch off.
        /// </summary>
        bool OpensBetweenRuns(ulong? seed) => MetaOn && !InRun && !BetweenRunsUp && OpenBetweenRuns(seed);

        /// <summary>A run starts: with the switch on, the equipped pieces go on its modifiers, and it earns.</summary>
        void ApplyBetweenRuns()
        {
            _metaRun = false;
            Earnings = null;
            if (!MetaOn) return;
            if (!BetweenRunsReadable()) return; // work order 033: the save is there but cannot be read now: this run is no meta run
            MetaState s = BetweenRunsState;
            MetaRack.Apply(s, MetaValues, Modifiers.Meta, StruckOutSkill);
            ApplyWeapon(s); // work order 060: her pistols' parts, only with the attachments on (GeneratedLevelsWeapon.cs)
            _metaRun = true;
            Earnings = new RunEarnings();
            _eliteEntrySeen = false;
            _eliteDipped = false;
            Debug.Log("Between runs: the run starts with " + (Modifiers.Meta.Any ? string.Join(", ", Modifiers.Meta.Pieces) : "nothing equipped")
                      + " (" + Modifiers.Meta + ")");
        }

        void BetweenRunsFloorEntered(int floor)
        {
            if (!_metaRun) return;
            _eliteEntrySeen = false;
            _eliteDipped = false;
            if (floor == 1)
            {
                MetaState s = BetweenRunsState;
                PlaySessionRecorder.Current?.NoteBetweenRunsLoadout(Modifiers.Meta.Pieces, s.Currency, MetaRack.Budget(s, MetaValues), MetaRack.PointsUsed(s, MetaValues),
                                                                    WeaponLoadoutWords()); // work order 060: null with the attachments off
            }
            if (floor >= 3) Earnings.Do(MetaFeat.ReachFloor3);
            if (floor >= 5) Earnings.Do(MetaFeat.ReachFloor5);
        }

        void BetweenRunsFloorCleared()
        {
            if (!_metaRun) return;
            Earnings.FloorsCleared++;
            if (Floor == 1) Earnings.Do(MetaFeat.FirstFloor);
            PlaySessionRecorder.Current?.NoteBetweenRunsEarned(MetaValues.PerFloor, "floor", Floor);
        }

        void BetweenRunsEliteCleared()
        {
            if (!_metaRun) return;
            Earnings.ElitesCleared++;
            if (!_eliteDipped) Earnings.Do(MetaFeat.EliteAboveHalf);
            PlaySessionRecorder.Current?.NoteBetweenRunsEarned(MetaValues.PerElite, "elite room", Floor);
        }

        void BetweenRunsActCleared()
        {
            if (!_metaRun) return;
            Earnings.BossKilled = true;
            Earnings.Do(MetaFeat.ActBoss);
            PlaySessionRecorder.Current?.NoteBetweenRunsEarned(MetaValues.PerBoss, "act boss", Floor);
        }

        /// <summary>
        /// The run ended: what it earned and its new feats are kept, and the save is written. A run during which the switch
        /// was turned off keeps nothing.
        /// </summary>
        void BetweenRunsRunEnded()
        {
            if (!_metaRun) return;
            _metaRun = false;
            if (!MetaOn)
            {
                Earnings = null;
                return;
            }
            List<MetaFeat> fresh = MetaRack.Commit(BetweenRunsState, MetaValues, Earnings);
            foreach (MetaFeat f in fresh) PlaySessionRecorder.Current?.NoteBetweenRunsFeat(MetaFeats.Id(f));
            SaveBetweenRuns();
            Debug.Log("Between runs: the run earned " + Earnings.Total(MetaValues) + " " + MetaValues.CurrencyName + ", " + Earnings.TotalAfter + " in all"
                      + (fresh.Count > 0 ? "; feats: " + string.Join(", ", fresh.Select(MetaFeats.Words)) : ""));
        }

        /// <summary>
        /// Every frame of a run, after the room she is in is tracked: is she under half health in the elite room? Only
        /// there (the second reviews of 2026-11-20, work order 033): a dip after stepping out of it is not in it.
        /// </summary>
        void BetweenRunsTick()
        {
            if (!_metaRun || _health == null || EliteRoomDone) return;
            if (EliteRoomEntered && !_eliteEntrySeen)
            {
                _eliteEntrySeen = true;
                _eliteDipped = false;
            }
            if (CurrentRoom != null && CurrentRoom.Elite && _health.Current * 2 < _health.Max) _eliteDipped = true;
        }

        /// <summary>
        /// The pause menu's Quit, before the game quits (the second reviews of 2026-11-20, work order 033): a run started
        /// with what lasts between runs on ends the way leaving does (F7), so what it earned reaches the save. With the
        /// trial off nothing changes.
        /// </summary>
        public void EndRunToQuit()
        {
            if (InRun && _metaRun) BackToPracticeRoom();
        }

        /// <summary>Not That One: the struck-out skill counts as had, so the floor reward never draws it.</summary>
        void StrikeOutOfOffer(PlayerCharacter player, List<bool> had)
        {
            string struck = Modifiers.Meta.StruckOut;
            if (string.IsNullOrEmpty(struck)) return;
            int slot = player.SlotOf(struck);
            if (slot >= 0 && slot < had.Count) had[slot] = true;
        }

        /// <summary>Deep Chests: the first chest opened on a floor offers more. Asked before the chest counts as opened.</summary>
        int FirstChestExtra => _chestsOpened.Count == 0 ? Modifiers.Meta.FirstChestExtra : 0;

        /// <summary>True while the floor reward is on screen and Second Look can still draw it again this run.</summary>
        public bool CanRedraw => Choice == ChoiceKind.FloorReward && Modifiers.Meta.RedrawsLeft > 0;

        /// <summary>
        /// Second Look (work order 029): the floor reward on screen is drawn again, once a run (<see cref="RewardRedraw"/>).
        /// False when no reward is offered or no redraw is left.
        /// </summary>
        public bool RedrawOffer()
        {
            if (!CanRedraw) return false;
            PlayerCharacter player = FindPlayer();
            var had = new List<bool>();
            for (int slot = 0; slot < player.Skills.Count; slot++) had.Add(player.IsEquipped(slot));
            StrikeOutOfOffer(player, had);
            List<int> again = RewardRedraw.Offer(RunSeed ?? 0UL, Floor, had, RunValues.SkillsOffered, _offeredSkills);
            if (again.Count == 0) return false;
            Modifiers.Meta.RedrawsLeft--;
            _offeredSkills.Clear();
            _offeredSkills.AddRange(again);
            PlaySessionRecorder.Current?.NoteRewardOffered(Floor, _offeredSkills.Select(s => player.Skills[s].Name).ToList());
            Debug.Log("Between runs: Second Look drew the floor reward again");
            return true;
        }

        /// <summary>The words under the floor reward when Second Look can draw it again; empty otherwise.</summary>
        string RedrawWords() => CanRedraw ? "   " + RedrawKey + " draws again (Second Look, once a run)." : "";
    }

    /// <summary>
    /// The pause menu's line "Between runs (trial): off / on" (work order 029), kept in PlayerPrefs under <see cref="Key"/>
    /// like the other settings (<see cref="GameSettings"/>). It overrides <see cref="MetaValues.Enabled"/>. It is read from
    /// PlayerPrefs only when the game installs itself in play (<see cref="Load"/>), never in a test run, so a machine where
    /// it was turned on never turns it on under the tests; tests turn it on through the values or the menu line, and put
    /// the key back.
    /// </summary>
    public static class BetweenRunsSwitch
    {
        public const string Key = "Roguelike.Settings.BetweenRuns";
        public const int NoOverride = -1;

        static int s_stored = NoOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_stored = NoOverride;

        /// <summary>The stored value: -1 none, 0 off, 1 on.</summary>
        public static int Stored => s_stored;

        public static void Load()
        {
            int v = PlayerPrefs.GetInt(Key, NoOverride);
            s_stored = v == 0 || v == 1 ? v : NoOverride;
        }

        /// <summary>Forgets it until the next <see cref="Load"/>: the values' switch rules again.</summary>
        public static void Unload() => s_stored = NoOverride;

        /// <summary>On or off: the stored choice if there is one, else <paramref name="fallback"/>, the values' switch.</summary>
        public static bool IsOn(bool fallback) => s_stored == NoOverride ? fallback : s_stored == 1;

        /// <summary>Turns it on or off and keeps it.</summary>
        public static void Set(bool on)
        {
            s_stored = on ? 1 : 0;
            PlayerPrefs.SetInt(Key, s_stored);
            PlayerPrefs.Save();
        }
    }
}
