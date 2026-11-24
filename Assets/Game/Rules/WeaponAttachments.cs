using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The weapons a loadout is put together for (work order 060; Kristian, 2026-11-21: "setter sammen et slags loadout før
    /// run ettersom man unlocker flere attachments. Samme til shotgunnet osv. og nye våpen", a kind of loadout put together
    /// before a run as more attachments are unlocked, the same for the shotgun and new weapons later). A weapon has slots
    /// (<see cref="WeaponAttachments.SlotsOf"/>), a slot holds one part, a part belongs to one weapon's slot. One weapon in
    /// this order: her pistols, whose parts act on the basic shot. The shotgun, or a new weapon, is a value here, its slots,
    /// and its parts as rows of <see cref="WeaponAttachments.Create"/>.
    /// </summary>
    public enum WeaponKind { Pistols }

    /// <summary>
    /// A weapon's slots (work order 060; decision 0014, the producer's proposal of 2026-11-21, a draft Kristian has not
    /// ruled on; the idea is his, 2026-11-20: "attachments til våpen. laser sikte = bedre range, osv."). One part a slot of a
    /// weapon; an empty slot is that weapon as it is (the stock gun). The pistols' three each ask one question: the sight how
    /// far against how forgiving, the barrel how fast against how heavy, the grip standing against moving. No magazine: the
    /// game has no ammunition. Another weapon's slots are added here (the shotgun's choke, say) and listed for it in
    /// <see cref="WeaponAttachments.SlotsOf"/>; a slot is always a weapon's, so the pistols' sight and a rifle's sight would
    /// be two slots though they share this value.
    /// </summary>
    public enum WeaponSlot { Sight, Barrel, Grip }

    /// <summary>
    /// One part of a weapon's loadout (work order 060): one line of data, belonging to one weapon's slot
    /// (<see cref="Weapon"/>, <see cref="Slot"/>). Fixed, named, unlocked once by buying it with the one currency between
    /// runs, and fitted or taken off freely before a run; never dropped, never random, never rolled (Kristian, 2026-11-21,
    /// asked whether such parts are inside "no gear or loot with stats": "Ja"). A pistols' part acts on the basic shot only,
    /// through five knobs; a knob at its neutral value changes nothing (0 for an add, 1 for a scale, 0 for
    /// <see cref="MovingSpeedShare"/>: the feel's own, false for <see cref="ShowsLine"/>). The knobs are read in one place
    /// only, <see cref="AttachmentEffects"/>. A part need not cost anything ("ikke nødvendigvis et bytte", not necessarily a
    /// trade): the first four each are a trade, by the proposal, but nothing here asks for one.
    /// </summary>
    [Serializable]
    public sealed class WeaponAttachment
    {
        /// <summary>Its name in the save: one id across every weapon, so the save's lists need no weapon of their own.</summary>
        public string Id = "";
        public string Name = "";
        /// <summary>The weapon it is fitted to.</summary>
        public WeaponKind Weapon = WeaponKind.Pistols;
        /// <summary>The slot of that weapon it goes in.</summary>
        public WeaponSlot Slot;
        public int Price;
        /// <summary>What it does, in plain words.</summary>
        public string Line = "";
        /// <summary>What it costs, in plain words.</summary>
        public string CostLine = "";

        /// <summary>Metres added to the basic shot's reach (the feel's 10 m), before a chest's Longer barrel.</summary>
        public float ReachAdd;
        /// <summary>The basic shot's half width (the feel's 0.5 m) times this.</summary>
        public float HalfWidthScale = 1f;
        /// <summary>Each basic shot's damage, the flat damage of a chest with it, times this.</summary>
        public float DamageScale = 1f;
        /// <summary>The time from one basic shot to the next times this; the startup never changes, only the recovery.</summary>
        public float IntervalScale = 1f;
        /// <summary>Stagger each basic shot takes off a bar, added (the basic shot's own is 0).</summary>
        public float Stagger;
        /// <summary>The share of run speed a shot on the move slows her to, and the backpedal cap; 0: the feel's own.</summary>
        public float MovingSpeedShare;
        /// <summary>A basic shot started on the move deals this share of its damage.</summary>
        public float MovingDamageScale = 1f;
        /// <summary>A line shows where the basic shot would end (the Laser Sight).</summary>
        public bool ShowsLine;

        public WeaponAttachment() { }

        public WeaponAttachment(string id, string name, WeaponKind weapon, WeaponSlot slot, int price, string line, string costLine)
        {
            Id = id;
            Name = name;
            Weapon = weapon;
            Slot = slot;
            Price = price;
            Line = line;
            CostLine = costLine;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The weapons, their slots and the parts (work order 060). The first four parts, all the pistols' (work order 060's
    /// table, in its order: decision 0014's proposal, every number a first guess he will judge by playing). Each is unlocked
    /// by buying it for <see cref="WeaponAttachment.Price"/> Echoes, and fitted at once. Each of these four is a trade, by the
    /// proposal; a later part may be a plain gain (Kristian, 2026-11-21: "ikke nødvendigvis et bytte").
    /// - Laser Sight (sight, 30): reach +2 m (10 to 12), a line shows where the shot would end; its cost a narrower shot
    ///   (half width 0.5 to 0.35 m). Kristian's own example, given a cost so it is a choice (0014: a plain "+range" sight
    ///   would be the chest's Longer barrel bought for good).
    /// - Ghost Ring (sight, 30): a wider shot (half width 0.5 to 0.8 m); its cost reach 10 to 8 m.
    /// - Bull Barrel (barrel, 40): each shot 35 % harder (40 to 54) and stagger 3, as Quick Shot's; its cost 35 % slower
    ///   between shots (0.225 to 0.304 s with the feel's numbers).
    /// - Runner's Grip (grip, 50): shooting on the move, and the backpedal cap, at 75 % of run speed, not 50 %; its cost a
    ///   shot fired on the move deals 20 % less (40 to 32).
    /// </summary>
    public static class WeaponAttachments
    {
        public const string LaserSight = "laser-sight";
        public const string GhostRing = "ghost-ring";
        public const string BullBarrel = "bull-barrel";
        public const string RunnersGrip = "runners-grip";

        /// <summary>The weapons of the loadout, in the order the screen shows them: her pistols only, in this order.</summary>
        public static readonly WeaponKind[] Weapons = { WeaponKind.Pistols };

        static readonly WeaponSlot[] s_PistolSlots = { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip };

        /// <summary>A weapon's slots, in the order the screen shows them.</summary>
        public static WeaponSlot[] SlotsOf(WeaponKind weapon) => s_PistolSlots; // one weapon: a switch on it when a second comes

        /// <summary>The weapon's name on the screen and in the pause menu.</summary>
        public static string WeaponName(WeaponKind weapon) => "Pistols";
        /// <summary>What of hers its parts act on.</summary>
        public static string WeaponActsOn(WeaponKind weapon) => "the basic shot";

        public static WeaponAttachment[] Create() => new[]
        {
            new WeaponAttachment(LaserSight, "Laser Sight", WeaponKind.Pistols, WeaponSlot.Sight, 30,
                                 "A line shows where the shot would end. Reach 12 m.", "The shot's line is narrower: aim truer.")
                { ReachAdd = 2f, HalfWidthScale = 0.7f, ShowsLine = true },
            new WeaponAttachment(GhostRing, "Ghost Ring", WeaponKind.Pistols, WeaponSlot.Sight, 30,
                                 "A wider shot, forgiving up close.", "Reach 8 m.")
                { HalfWidthScale = 1.6f, ReachAdd = -2f },
            new WeaponAttachment(BullBarrel, "Bull Barrel", WeaponKind.Pistols, WeaponSlot.Barrel, 40,
                                 "Each shot hits 35 % harder and staggers.", "35 % slower between shots.")
                { DamageScale = 1.35f, IntervalScale = 1.35f, Stagger = 3f },
            new WeaponAttachment(RunnersGrip, "Runner's Grip", WeaponKind.Pistols, WeaponSlot.Grip, 50,
                                 "Shooting on the move slows you to 75 %, not 50 %.", "Shots on the move deal 20 % less.")
                { MovingSpeedShare = 0.75f, MovingDamageScale = 0.8f },
        };

        /// <summary>The slot's head on the screen: SIGHT, BARREL, GRIP.</summary>
        public static string SlotWord(WeaponSlot slot) => slot == WeaponSlot.Sight ? "SIGHT" : slot == WeaponSlot.Barrel ? "BARREL" : "GRIP";
    }

    /// <summary>
    /// What one weapon's fitted parts do to one run (work order 060): the pistols' are held by the run's modifiers
    /// (<see cref="RunModifiers.Weapon"/>) and cleared with them; another weapon would have its own, beside it. Adds add, scales multiply, <see cref="MovingSpeedShare"/> takes the largest,
    /// <see cref="ShowsLine"/> is true if any part's is. Its methods are the only place the knobs are read; empty
    /// (<see cref="Clear"/>), every one of them gives back exactly what it is given: the stock gun.
    /// </summary>
    public sealed class AttachmentEffects
    {
        /// <summary>The basic shot never reaches less than this (m), whatever is fitted.</summary>
        public const float LeastReach = 4f;
        /// <summary>The basic shot's half width is never under this (m), whatever is fitted.</summary>
        public const float LeastHalfWidth = 0.1f;

        public float ReachAdd;
        public float HalfWidthScale = 1f;
        public float DamageScale = 1f;
        public float IntervalScale = 1f;
        public float Stagger;
        public float MovingSpeedShare;
        public float MovingDamageScale = 1f;
        public bool ShowsLine;
        /// <summary>The names of the parts fitted, in the table's order.</summary>
        public readonly List<string> Parts = new List<string>();

        public bool Any => Parts.Count > 0;

        public void Clear()
        {
            ReachAdd = 0f;
            HalfWidthScale = 1f;
            DamageScale = 1f;
            IntervalScale = 1f;
            Stagger = 0f;
            MovingSpeedShare = 0f;
            MovingDamageScale = 1f;
            ShowsLine = false;
            Parts.Clear();
        }

        /// <summary>Puts one part's knobs on.</summary>
        public void Add(WeaponAttachment p)
        {
            if (p == null) return;
            Parts.Add(p.Name);
            ReachAdd += p.ReachAdd;
            HalfWidthScale *= p.HalfWidthScale;
            DamageScale *= p.DamageScale;
            IntervalScale *= p.IntervalScale;
            Stagger += p.Stagger;
            MovingSpeedShare = Math.Max(MovingSpeedShare, p.MovingSpeedShare);
            MovingDamageScale *= p.MovingDamageScale;
            ShowsLine |= p.ShowsLine;
        }

        /// <summary>The basic shot's reach from the feel's: the parts' metres added, never under <see cref="LeastReach"/>.</summary>
        public float Reach(float feelReach) => ReachAdd == 0f ? feelReach : Math.Max(LeastReach, feelReach + ReachAdd);

        /// <summary>The basic shot's half width from the feel's: times the parts' scale, never under <see cref="LeastHalfWidth"/>.</summary>
        public float HalfWidth(float feelHalfWidth) => HalfWidthScale == 1f ? feelHalfWidth : Math.Max(LeastHalfWidth, feelHalfWidth * HalfWidthScale);

        /// <summary>
        /// A basic shot's damage (<paramref name="damage"/>: its own and a chest's flat damage): times the parts' scale, and on
        /// the move times the grip's; rounded half away from zero, as <see cref="FlatDamage.For"/> rounds.
        /// </summary>
        public int ShotDamage(int damage, bool onTheMove)
        {
            double scale = DamageScale * (onTheMove ? MovingDamageScale : 1f);
            if (damage <= 0 || scale == 1.0) return damage;
            return (int)Math.Round(damage * scale, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The basic shot's recovery that makes the whole time from shot to shot <see cref="IntervalScale"/> times the one
        /// these numbers give. The startup (the Shoot clip's firing moment, work order 050) and the active part never change;
        /// never under 0. Her attack speed (decision 0012) then works on this as it worked on the feel's.
        /// </summary>
        public float Recovery(float startup, float active, float recovery) =>
            IntervalScale == 1f ? recovery : Math.Max(0f, (startup + active + recovery) * IntervalScale - startup - active);

        /// <summary>The time from shot to shot with these numbers, before her attack speed.</summary>
        public float Interval(float startup, float active, float recovery) => startup + active + Recovery(startup, active, recovery);

        /// <summary>Stagger a basic shot takes off a bar, added to what it takes already (a quick duck's peek shot).</summary>
        public float ShotStagger => Stagger;

        /// <summary>The share of run speed a shot on the move slows her to: the grip's, or the feel's.</summary>
        public float MovingShare(float feelShare) => MovingSpeedShare > 0f ? MovingSpeedShare : feelShare;

        /// <summary>The backpedal cap's share of run speed: the grip's, or the feel's.</summary>
        public float BackpedalShare(float feelShare) => MovingSpeedShare > 0f ? MovingSpeedShare : feelShare;

        /// <summary>The numbers in one line, for tests and the log.</summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                          "reach {0:+0.##;-0.##;+0} m, width x{1:0.##}, damage x{2:0.##}, interval x{3:0.##}, stagger +{4:0.##}, moving {5:0.##}, moving damage x{6:0.##}, line {7}",
                          ReachAdd, HalfWidthScale, DamageScale, IntervalScale, Stagger, MovingSpeedShare, MovingDamageScale, ShowsLine ? "on" : "off");
    }

    public enum AttachmentFit { Fitted, AlreadyFitted, NotOwned, Unknown }

    /// <summary>What a click or Enter on a part did (<see cref="WeaponRack.Use"/>).</summary>
    public enum AttachmentUse { TakenOff, Fitted, Bought, NotEnough, Unknown }

    /// <summary>For the end screen (work order 060): what the currency buys now, or the cheapest part not owned and how many more it needs.</summary>
    public sealed class WeaponNextBuy
    {
        /// <summary>The parts not owned that the currency buys now, in the table's order.</summary>
        public readonly List<WeaponAttachment> Affordable = new List<WeaponAttachment>();
        /// <summary>None affordable: the cheapest not owned (the first in the table on a tie); null when every part is owned.</summary>
        public WeaponAttachment Cheapest;
        /// <summary>How many more Echoes <see cref="Cheapest"/> needs.</summary>
        public int More;

        public bool AllOwned => Affordable.Count == 0 && Cheapest == null;

        /// <summary>"enough for Laser Sight (30), Ghost Ring (30)", "Bull Barrel at 40: 12 more", or "every part owned".</summary>
        public string Words()
        {
            if (Affordable.Count > 0)
                return "enough for " + string.Join(", ", Affordable.Select(p => p.Name + " (" + p.Price.ToString(CultureInfo.InvariantCulture) + ")"));
            if (Cheapest != null)
                return Cheapest.Name + " at " + Cheapest.Price.ToString(CultureInfo.InvariantCulture) + ": " + More.ToString(CultureInfo.InvariantCulture) + " more";
            return "every part owned";
        }
    }

    /// <summary>
    /// The rules of the loadout (work order 060): the parts of each weapon, beside the rack's (<see cref="MetaRack"/>), on the
    /// same save and currency. Parts have no points: a weapon's slot is their limit, one part each. They never touch the rack's
    /// budget, and the rack never touches them. Buying (unlocking) takes the price off once and fits the part at once, in
    /// place of what its weapon's slot held; the part replaced stays owned, and owned parts are fitted and taken off freely
    /// before a run. The save keeps two lists of part ids (owned, fitted); a part's id names its weapon and slot through the
    /// table, so a second weapon's parts go in the same two lists and the save keeps its shape.
    /// </summary>
    public static class WeaponRack
    {
        public static WeaponAttachment Find(MetaValues v, string id) =>
            id == null ? null : v?.Parts?.FirstOrDefault(p => p != null && p.Id == id);

        public static bool Owns(MetaState s, string id) => s.AttachmentsOwned.Contains(id);
        public static bool IsFitted(MetaState s, string id) => s.AttachmentsFitted.Contains(id);

        /// <summary>True when the two parts go in the same slot of the same weapon: one of them at a time.</summary>
        public static bool SameSlot(WeaponAttachment a, WeaponAttachment b) => a != null && b != null && a.Weapon == b.Weapon && a.Slot == b.Slot;

        /// <summary>The part fitted in this slot of this weapon, or null: the weapon as it is.</summary>
        public static WeaponAttachment FittedIn(MetaState s, MetaValues v, WeaponKind weapon, WeaponSlot slot)
        {
            foreach (string id in s.AttachmentsFitted)
            {
                WeaponAttachment p = Find(v, id);
                if (p != null && p.Weapon == weapon && p.Slot == slot) return p;
            }
            return null;
        }

        /// <summary>The parts in the order the screen lists them: weapon by weapon, slot by slot, each slot's in the table's order.</summary>
        public static List<WeaponAttachment> InSlotOrder(MetaValues v)
        {
            var list = new List<WeaponAttachment>();
            if (v?.Parts == null) return list;
            foreach (WeaponKind weapon in WeaponAttachments.Weapons)
                foreach (WeaponSlot slot in WeaponAttachments.SlotsOf(weapon))
                    foreach (WeaponAttachment p in v.Parts)
                        if (p != null && p.Weapon == weapon && p.Slot == slot) list.Add(p);
            return list;
        }

        /// <summary>The whole set's price.</summary>
        public static int WholePrice(MetaValues v) => v?.Parts?.Where(p => p != null).Sum(p => p.Price) ?? 0;

        public static MetaBuy CanBuy(MetaState s, MetaValues v, string id)
        {
            WeaponAttachment p = Find(v, id);
            if (p == null) return MetaBuy.Unknown;
            if (Owns(s, id)) return MetaBuy.AlreadyOwned;
            if (s.Currency < p.Price) return MetaBuy.NotEnough;
            return MetaBuy.Bought;
        }

        /// <summary>Buys the part if it may: its price comes off the currency, once, and it is fitted at once.</summary>
        public static MetaBuy Buy(MetaState s, MetaValues v, string id)
        {
            MetaBuy r = CanBuy(s, v, id);
            if (r != MetaBuy.Bought) return r;
            s.Currency -= Find(v, id).Price;
            s.AttachmentsOwned.Add(id);
            Fit(s, v, id);
            return r;
        }

        public static AttachmentFit CanFit(MetaState s, MetaValues v, string id)
        {
            if (Find(v, id) == null) return AttachmentFit.Unknown;
            if (IsFitted(s, id)) return AttachmentFit.AlreadyFitted;
            if (!Owns(s, id)) return AttachmentFit.NotOwned;
            return AttachmentFit.Fitted;
        }

        /// <summary>Fits an owned part: whatever its weapon's slot held comes off (and stays owned).</summary>
        public static AttachmentFit Fit(MetaState s, MetaValues v, string id)
        {
            AttachmentFit r = CanFit(s, v, id);
            if (r != AttachmentFit.Fitted) return r;
            WeaponAttachment part = Find(v, id);
            s.AttachmentsFitted.RemoveAll(other => SameSlot(Find(v, other), part));
            s.AttachmentsFitted.Add(id);
            return r;
        }

        /// <summary>Takes the part off: its slot is the stock gun's. False when it was not fitted.</summary>
        public static bool TakeOff(MetaState s, MetaValues v, string id) => s.AttachmentsFitted.Remove(id);

        /// <summary>A click or Enter on a part: fitted, it is taken off; owned, it is fitted; otherwise it is bought (and fitted).</summary>
        public static AttachmentUse Use(MetaState s, MetaValues v, string id)
        {
            if (Find(v, id) == null) return AttachmentUse.Unknown;
            if (IsFitted(s, id)) return TakeOff(s, v, id) ? AttachmentUse.TakenOff : AttachmentUse.Unknown;
            if (Owns(s, id)) return Fit(s, v, id) == AttachmentFit.Fitted ? AttachmentUse.Fitted : AttachmentUse.Unknown;
            MetaBuy r = Buy(s, v, id);
            return r == MetaBuy.Bought ? AttachmentUse.Bought : r == MetaBuy.NotEnough ? AttachmentUse.NotEnough : AttachmentUse.Unknown;
        }

        /// <summary>
        /// A save read back (called by <see cref="MetaRack.Tidy"/>): parts it does not know, or owns twice, are dropped; a part
        /// fitted but not owned comes off; at most one is fitted a slot, the first kept. Owned and fitted parts stay in the save
        /// whether the switch is on or off; off, they are never applied.
        /// </summary>
        public static void Tidy(MetaState s, MetaValues v)
        {
            var seen = new HashSet<string>();
            s.AttachmentsOwned.RemoveAll(id => id == null || Find(v, id) == null || !seen.Add(id));
            seen.Clear();
            var slots = new HashSet<(WeaponKind, WeaponSlot)>(); // a slot is a weapon's
            s.AttachmentsFitted.RemoveAll(id =>
            {
                if (id == null || !Owns(s, id) || !seen.Add(id)) return true;
                WeaponAttachment p = Find(v, id);
                return !slots.Add((p.Weapon, p.Slot));
            });
        }

        /// <summary>
        /// What one weapon's fitted parts do to the next run (the pistols' by default: the only weapon), put on
        /// <paramref name="into"/> (cleared first), in the table's order. Another weapon's parts are never put on it.
        /// </summary>
        public static void Apply(MetaState s, MetaValues v, AttachmentEffects into, WeaponKind weapon = WeaponKind.Pistols)
        {
            into.Clear();
            if (s == null || v?.Parts == null) return;
            foreach (WeaponAttachment p in v.Parts)
                if (p != null && p.Weapon == weapon && IsFitted(s, p.Id)) into.Add(p);
        }

        /// <summary>For the end screen: the parts the currency buys now, or else the cheapest not owned and how many more it needs.</summary>
        public static WeaponNextBuy NextBuy(MetaState s, MetaValues v)
        {
            var r = new WeaponNextBuy();
            if (v?.Parts == null) return r;
            foreach (WeaponAttachment p in v.Parts)
                if (p != null && !Owns(s, p.Id) && p.Price <= s.Currency) r.Affordable.Add(p);
            if (r.Affordable.Count > 0) return r;
            foreach (WeaponAttachment p in v.Parts)
                if (p != null && !Owns(s, p.Id) && (r.Cheapest == null || p.Price < r.Cheapest.Price)) r.Cheapest = p;
            if (r.Cheapest != null) r.More = r.Cheapest.Price - s.Currency;
            return r;
        }

        /// <summary>The fitted parts' names for the log and the recorder: "Laser Sight | Bull Barrel", or "stock".</summary>
        public static string FittedWords(MetaState s, MetaValues v, string joint = " | ")
        {
            var names = new List<string>();
            if (v?.Parts != null)
                foreach (WeaponAttachment p in v.Parts)
                    if (p != null && IsFitted(s, p.Id)) names.Add(p.Name);
            return names.Count == 0 ? "stock" : string.Join(joint, names);
        }
    }

    /// <summary>A rectangle of the screen's layout, in the HUD's layout pixels (720 high).</summary>
    public struct LayoutRect
    {
        public float X, Y, Width, Height;

        public LayoutRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public bool Overlaps(LayoutRect o) => Width > 0f && o.Width > 0f && X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.#}, {1:0.#}, {2:0.#} x {3:0.#})", X, Y, Width, Height);
    }

    /// <summary>
    /// Where the screen between runs puts its panels (work order 060), as a rule EditMode can test. With the attachments off
    /// it is work order 029's: the rack's panel <see cref="RackWidth"/> wide in the middle. On, the weapon's panel stands to
    /// the right of the rack's: both side by side and centred where they fit (1280 layout pixels and wider, which a 16:9
    /// view is); narrower, the rack's panel narrows to what is left, never under <see cref="LeastRack"/>, and then the
    /// weapon's narrows, so neither overlaps the other or leaves the view. First guesses.
    /// </summary>
    public static partial class BetweenRunsLayout // work order 099: partial, its three panels' rule in BetweenRunsRanks.cs
    {
        /// <summary>The rack's panel, as work order 029 drew it.</summary>
        public const float RackWidth = 680f;
        /// <summary>The weapon's panel at its width.</summary>
        public const float WeaponWidth = 440f;
        public const float Gap = 16f;
        /// <summary>Kept clear at each side of the view.</summary>
        public const float Margin = 12f;
        /// <summary>The rack's panel is never narrower than this.</summary>
        public const float LeastRack = 520f;
        /// <summary>The top of a panel never above this (029's).</summary>
        public const float LeastTop = 6f;

        /// <summary>The panels' left edges and widths (their Y and Height are 0: see <see cref="Top"/>).</summary>
        public static void Panels(float viewWidth, bool weapon, out LayoutRect rack, out LayoutRect weaponPanel)
        {
            if (!weapon)
            {
                rack = new LayoutRect((viewWidth - RackWidth) * 0.5f, 0f, RackWidth, 0f);
                weaponPanel = new LayoutRect(0f, 0f, 0f, 0f);
                return;
            }
            float room = viewWidth - 2f * Margin;
            float rackW = RackWidth, weaponW = WeaponWidth;
            if (rackW + Gap + weaponW > room) rackW = Math.Max(LeastRack, room - Gap - weaponW);
            if (rackW + Gap + weaponW > room) weaponW = Math.Max(0f, room - Gap - rackW);
            float x = Math.Max(Margin, (viewWidth - (rackW + Gap + weaponW)) * 0.5f);
            rack = new LayoutRect(x, 0f, rackW, 0f);
            weaponPanel = new LayoutRect(x + rackW + Gap, 0f, weaponW, 0f);
        }

        /// <summary>A panel's top for its height: in the middle of the view, never above <see cref="LeastTop"/> (029's).</summary>
        public static float Top(float viewHeight, float height) => Math.Max(LeastTop, (viewHeight - height) * 0.5f);
    }
}
