using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The loadout between runs (work order 060, 2026-11-21; decision 0014, the producer's proposal; the idea is Kristian's,
    /// 2026-11-20: "attachments til våpen. laser sikte = bedre range", and on 2026-11-21 he called it a loadout put together
    /// before a run as more attachments are unlocked, per weapon): her pistols' parts in this order, as part of
    /// <see cref="GeneratedLevels"/>, on the currency, the save and the screen of what lasts between runs (work order 029).
    /// The rules are <see cref="WeaponRack"/>'s; the weapon's panel on the screen is drawn by the HUD (RunHudWeapon.cs).
    ///
    /// The switch (<see cref="AttachmentsOn"/>): <see cref="MetaValues.Attachments"/>, false in code, unless the pause menu's
    /// line "Weapon attachments (trial)" says otherwise (<see cref="AttachmentsSwitch"/>, kept in PlayerPrefs, read only when
    /// the game installs itself in play, never in a test run); and only with the between-runs trial on (<see cref="MetaOn"/>).
    /// Off, nothing here does anything: no part is applied, no room pays, the screen and the end screen are 029's, and the
    /// save keeps any parts it holds, unchanged and unused.
    ///
    /// On:
    /// - a run started with it on gets the fitted parts on its modifiers (<see cref="RunModifiers.Weapon"/>), read by her
    ///   basic shot only; nothing is written to the feel settings or to what the practice room reads;
    /// - each ordinary combat room cleared pays <see cref="MetaValues.PerRoom"/> (the elite room keeps its own);
    /// - the screen between runs has the weapon's panel; its intents are <see cref="BuyAttachment"/>,
    ///   <see cref="FitAttachment"/>, <see cref="TakeOffAttachment"/> and <see cref="UseAttachment"/>.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>True when her pistols' parts are on: the between-runs trial on, and the attachments' switch or the pause menu's line.</summary>
        public bool AttachmentsOn => MetaOn && AttachmentsSwitch.IsOn(MetaValues.Attachments);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LoadAttachmentsSwitch()
        {
            // As the trial's own switch (work order 029): read only in play, never in a test run, under the recorder's rule.
            if (SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) AttachmentsSwitch.Load();
        }

        // ---------------- The screen's intents (work order 060) ----------------

        /// <summary>Buys a part on the screen, if it may be bought: it is fitted at once, in place of what its slot held. The save is written.</summary>
        public MetaBuy BuyAttachment(string id)
        {
            if (!BetweenRunsUp || !AttachmentsOn) return MetaBuy.Unknown;
            WeaponAttachment before = PartInSlotOf(id);
            MetaBuy r = WeaponRack.Buy(BetweenRunsState, MetaValues, id);
            BetweenRunsNote = AttachmentBuyWords(r, id, before);
            if (r == MetaBuy.Bought) RackChanged("bought and fitted " + id);
            return r;
        }

        /// <summary>Fits an owned part on the screen, in place of what its slot held. The save is written.</summary>
        public AttachmentFit FitAttachment(string id)
        {
            if (!BetweenRunsUp || !AttachmentsOn) return AttachmentFit.Unknown;
            WeaponAttachment before = PartInSlotOf(id);
            AttachmentFit r = WeaponRack.Fit(BetweenRunsState, MetaValues, id);
            WeaponAttachment p = WeaponRack.Find(MetaValues, id);
            string name = p != null ? p.Name : id;
            BetweenRunsNote = r == AttachmentFit.Fitted ? "Fitted " + name + (before != null && before != p ? " (" + before.Name + " comes off)." : ".")
                            : r == AttachmentFit.NotOwned ? name + " is not owned." : "";
            if (r == AttachmentFit.Fitted) RackChanged("fitted " + id);
            return r;
        }

        /// <summary>Takes a fitted part off on the screen: its slot is the stock gun's. The save is written.</summary>
        public bool TakeOffAttachment(string id)
        {
            if (!BetweenRunsUp || !AttachmentsOn || !WeaponRack.TakeOff(BetweenRunsState, MetaValues, id)) return false;
            BetweenRunsNote = "";
            RackChanged("took off " + id);
            return true;
        }

        /// <summary>A click or Enter on a part: takes it off if it is fitted, else fits it if owned, else buys it (and fits it).</summary>
        public bool UseAttachment(string id)
        {
            if (!BetweenRunsUp || !AttachmentsOn || WeaponRack.Find(MetaValues, id) == null) return false;
            MetaState s = BetweenRunsState;
            if (WeaponRack.IsFitted(s, id)) return TakeOffAttachment(id);
            if (WeaponRack.Owns(s, id)) return FitAttachment(id) == AttachmentFit.Fitted;
            return BuyAttachment(id) == MetaBuy.Bought;
        }

        WeaponAttachment PartInSlotOf(string id)
        {
            WeaponAttachment p = WeaponRack.Find(MetaValues, id);
            return p != null ? WeaponRack.FittedIn(BetweenRunsState, MetaValues, p.Weapon, p.Slot) : null;
        }

        string AttachmentBuyWords(MetaBuy r, string id, WeaponAttachment before)
        {
            WeaponAttachment p = WeaponRack.Find(MetaValues, id);
            string name = p != null ? p.Name : id;
            switch (r)
            {
                case MetaBuy.Bought: return "Bought " + name + ": fitted" + (before != null && before != p ? " (" + before.Name + " comes off, still yours)." : ".");
                case MetaBuy.NotEnough: return "Not enough " + MetaValues.CurrencyName + " for " + name + ".";
                case MetaBuy.AlreadyOwned: return name + " is owned already.";
                default: return "";
            }
        }

        // ---------------- In a run (work order 060) ----------------

        /// <summary>
        /// Called by <see cref="ApplyBetweenRuns"/> as a meta run starts: with the attachments on, the loadout put together on
        /// the screen goes on its modifiers, the pistols' parts on <see cref="RunModifiers.Weapon"/> (a second weapon's would go
        /// on its own effects here).
        /// </summary>
        void ApplyWeapon(MetaState s)
        {
            Modifiers.Weapon.Clear();
            if (!AttachmentsOn) return;
            WeaponRack.Apply(s, MetaValues, Modifiers.Weapon, WeaponKind.Pistols);
            Debug.Log("Between runs: her pistols start with " + (Modifiers.Weapon.Any ? string.Join(", ", Modifiers.Weapon.Parts) : "the stock gun")
                      + " (" + Modifiers.Weapon + ")");
        }

        /// <summary>The recorder's "weapon" field: the fitted parts joined by " | ", or "stock"; null with the attachments off.</summary>
        string WeaponLoadoutWords() => AttachmentsOn ? (Modifiers.Weapon.Any ? string.Join(" | ", Modifiers.Weapon.Parts) : "stock") : null;

        /// <summary>
        /// A combat room's enemies are all dead (called once a room from <see cref="HealClearedRooms"/>): in a meta run with the
        /// attachments on, an ordinary one pays <see cref="MetaValues.PerRoom"/>. The elite room keeps its own pay.
        /// </summary>
        void BetweenRunsRoomCleared(LevelRoom room)
        {
            if (!_metaRun || Earnings == null || !AttachmentsOn || room == null || room.Elite || room.Kind != RoomKind.Combat) return;
            Earnings.RoomsCleared++;
            PlaySessionRecorder.Current?.NoteBetweenRunsEarned(MetaValues.PerRoom, "room", Floor);
        }
    }

    /// <summary>
    /// The pause menu's line "Weapon attachments (trial): off / on" (work order 060), kept in PlayerPrefs under
    /// <see cref="Key"/>, copied from <see cref="BetweenRunsSwitch"/>. It overrides <see cref="MetaValues.Attachments"/>; it is
    /// read from PlayerPrefs only when the game installs itself in play (<see cref="Load"/>), never in a test run; tests turn
    /// it on through the values or the menu line, and put the key back.
    /// </summary>
    public static class AttachmentsSwitch
    {
        public const string Key = "Roguelike.Settings.Attachments";
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
