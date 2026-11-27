using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The upgrades in three tabs of three forms (work order 139). Kristian, 2026-11-25: "Yes, jeg liker heller ikke så godt
    /// oppgraderings siden. At alt er kort er ikke så kult. Skulle gjerne ha delt de opp. Weapon upgrade som en egen fane, der man
    /// ser modellen til våpenet i høy res, kan dra og se rundt. Så kan man legge til attachments som gir buff" (that everything is
    /// cards is not so cool; split them up; weapon upgrade as a tab of its own, the weapon's model in high resolution, dragged
    /// to look round it; attachments that give a buff), and he ticked "Tre faner, tre former": WEAPON with the model and the
    /// attachments, RANKS as rows with steps, RACK as a list of its own.
    /// </summary>
    public static class UpgradeTabsValues
    {
        /// <summary>
        /// The switch, on by default (his tick above). Off, the start menu's UPGRADES is the one screen of cards of work order
        /// 128's follow-up, field for field.
        /// </summary>
        public static bool On = true;
    }

    /// <summary>The three pages of the upgrades (work order 139), in the tabs' order.</summary>
    public enum UpgradesPage { Weapon, Ranks, Rack }

    /// <summary>
    /// The start menu's tabs with the upgrades in three (work order 139): START RUN, WEAPON, RANKS, RACK, STATS, the three
    /// pages on one screen (<see cref="MenuScreen.Upgrades"/>) told apart by a page. No engine.
    /// </summary>
    public static class UpgradeTabsFlow
    {
        /// <summary>The five tabs' words, in their order (the three pages between START RUN and STATS).</summary>
        public static readonly string[] TabWords = { "START RUN", "WEAPON", "RANKS", "RACK", "STATS" };

        /// <summary>The tab of the first page (WEAPON); the pages follow it in <see cref="UpgradesPage"/>'s order.</summary>
        public const int FirstPageTab = 1;
        /// <summary>STATS' tab.</summary>
        public const int StatsTab = 4;
        public const int Pages = 3;

        /// <summary>The tab a screen and page sit under: START RUN's two steps under the first, a page under its own, STATS last.</summary>
        public static int TabOf(MenuScreen s, UpgradesPage page) =>
            s == MenuScreen.Upgrades ? FirstPageTab + (int)page : s == MenuScreen.Stats ? StatsTab : 0;

        /// <summary>The screen a tab opens: START RUN's first step, the upgrades (with <see cref="TabPage"/>), STATS.</summary>
        public static MenuScreen TabScreen(int tab) =>
            tab >= FirstPageTab && tab < FirstPageTab + Pages ? MenuScreen.Upgrades : tab == StatsTab ? MenuScreen.Stats : MenuScreen.Character;

        /// <summary>The page a tab opens (WEAPON for a tab that is not a page's).</summary>
        public static UpgradesPage TabPage(int tab) =>
            tab >= FirstPageTab && tab < FirstPageTab + Pages ? (UpgradesPage)(tab - FirstPageTab) : UpgradesPage.Weapon;

        /// <summary>One page on (Tab, E) or back (Q), round the three.</summary>
        public static UpgradesPage Step(UpgradesPage page, int delta) => (UpgradesPage)StartMenuFlow.Step((int)page, delta, Pages);
    }

    /// <summary>
    /// The parts' drawbacks halved (work order 139; Kristian, 2026-11-25: "Jeg syntes også drawbacken på noen av de er for
    /// streng", and asked how, he ticked "Halver alle ulempene": every part keeps a drawback, half as large; the benefits stand).
    /// "Half" is half of the step away from the stock gun, in the cost's own unit, rounded where a half would read badly:
    /// - Laser Sight: the shot's width x0.7 becomes x0.85 (1 m wide to 0.85 m, not 0.7 m);
    /// - Ghost Ring: reach -2 m becomes -1 m (9 m, not 8 m);
    /// - Bull Barrel: 35 % slower between shots becomes 17.5 % (0.225 s to 0.264 s, not 0.304 s);
    /// - Runner's Grip: a shot on the move deals 20 % less becomes 10 % less (36, not 32);
    /// - Reflex Sight: reach -1.5 m becomes -0.75 m (9.25 m, not 8.5 m);
    /// - Long Barrel: 20 % slower between shots becomes 10 % (0.248 s, not 0.27 s);
    /// - Compensator: each shot 20 % less becomes 10 % less (36, not 32);
    /// - Weighted Grip: shooting on the move at 35 % of run speed (15 points under the 50 %) becomes 42.5 % (7.5 points under);
    /// - Quick-Draw Grip: each shot 10 % less becomes 5 % less (38, not 36).
    /// </summary>
    public static class WeaponPartValues
    {
        /// <summary>The switch, on by default (his tick above). Off, every part's cost is work orders 060's and 110's as before.</summary>
        public static bool GentlerCosts = true;

        /// <summary>The table with the halved costs when <see cref="GentlerCosts"/> is on (the same array, its rows changed in place); off, as it is.</summary>
        public static WeaponAttachment[] WithCosts(WeaponAttachment[] parts)
        {
            if (!GentlerCosts || parts == null) return parts;
            foreach (WeaponAttachment p in parts) Gentler(p);
            return parts;
        }

        /// <summary>One part's cost halved, its words with it; its benefit untouched. A part the table does not name is left as it is.</summary>
        static void Gentler(WeaponAttachment p)
        {
            if (p == null) return;
            switch (p.Id)
            {
                case WeaponAttachments.LaserSight:
                    p.HalfWidthScale = 0.85f;
                    p.CostLine = "The shot's line is 15 % narrower: aim truer.";
                    break;
                case WeaponAttachments.GhostRing:
                    p.ReachAdd = -1f;
                    p.CostLine = "Reach 9 m.";
                    break;
                case WeaponAttachments.BullBarrel:
                    p.IntervalScale = 1.175f;
                    p.CostLine = "17.5 % slower between shots.";
                    break;
                case WeaponAttachments.RunnersGrip:
                    p.MovingDamageScale = 0.9f;
                    p.CostLine = "Shots on the move deal 10 % less.";
                    break;
                case WeaponAttachments.ReflexSight:
                    p.ReachAdd = -0.75f;
                    p.CostLine = "Reach 9.25 m.";
                    break;
                case WeaponAttachments.LongBarrel:
                    p.IntervalScale = 1.1f;
                    p.CostLine = "10 % slower between shots.";
                    break;
                case WeaponAttachments.Compensator:
                    p.DamageScale = 0.9f;
                    p.CostLine = "Each shot deals 10 % less.";
                    break;
                case WeaponAttachments.WeightedGrip:
                    p.MovingSpeedShare = 0.425f;
                    p.CostLine = "Shooting on the move slows you to 42.5 %, not 50 %.";
                    break;
                case WeaponAttachments.QuickDrawGrip:
                    p.DamageScale = 0.95f;
                    p.CostLine = "Each shot deals 5 % less.";
                    break;
            }
        }
    }

    // ---------------- Her pistols' numbers on the WEAPON tab ----------------

    /// <summary>The numbers of her basic shot the WEAPON tab shows, in its order (work order 139): what the parts touch.</summary>
    public enum WeaponNumber { Damage, ShotsASecond, Reach, Width, CritChance, Stagger, MovingSpeed, MovingDamage, FirstShot }

    /// <summary>
    /// Her pistols' numbers as a run would start with them (work order 139: "the basic shot's damage, shots a second, reach and
    /// whatever else the parts touch, as they are with the parts now fitted, and beside each the change the previewed part would
    /// make"), read from her sheet's own rules (<see cref="HerSheet.BasicShotDealt"/>, <see cref="HerSheet.BasicInterval"/>,
    /// <see cref="HerSheet.BasicReach"/>) and the parts' (<see cref="AttachmentEffects"/>), so they are the run's. No engine.
    /// </summary>
    public static class WeaponNumbers
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        public const int Count = 9;

        /// <summary>A number's name on the tab (short: the panel is narrow; "Speed shooting" is her speed while shooting on the move, as a share of her run).</summary>
        public static string Name(WeaponNumber n)
        {
            switch (n)
            {
                case WeaponNumber.Damage: return "Damage a shot";
                case WeaponNumber.ShotsASecond: return "Shots a second";
                case WeaponNumber.Reach: return "Reach";
                case WeaponNumber.Width: return "Shot width";
                case WeaponNumber.CritChance: return "Crit chance";
                case WeaponNumber.Stagger: return "Stagger a shot";
                case WeaponNumber.MovingSpeed: return "Speed shooting";
                case WeaponNumber.MovingDamage: return "Damage moving";
                default: return "First shot after dash";
            }
        }

        /// <summary>The numbers for <paramref name="c"/> (its modifiers' weapon as fitted), in <see cref="WeaponNumber"/>'s order, into <paramref name="into"/> (<see cref="Count"/> long).</summary>
        public static void Of(OfferContext c, double[] into)
        {
            FeelValues f = c.Feel ?? new FeelValues();
            RunModifiers m = c.Modifiers;
            into[(int)WeaponNumber.Damage] = HerSheet.BasicShotDealt(c);
            float interval = HerSheet.BasicInterval(c);
            into[(int)WeaponNumber.ShotsASecond] = interval > 1e-6f ? 1.0 / interval : 0.0;
            into[(int)WeaponNumber.Reach] = HerSheet.BasicReach(c);
            into[(int)WeaponNumber.Width] = 2.0 * m.Weapon.HalfWidth(f.BasicShotHalfWidth);
            into[(int)WeaponNumber.CritChance] = m.Weapon.ShotCritChance(m.Stats.CritChance);
            into[(int)WeaponNumber.Stagger] = m.Weapon.ShotStagger;
            into[(int)WeaponNumber.MovingSpeed] = m.Weapon.MovingShare(f.MovingShotSpeedShare);
            into[(int)WeaponNumber.MovingDamage] = m.ScaleDamage(m.Weapon.ShotDamage(c.BasicShotDamage + m.Stats.FlatFor(null), true));
            into[(int)WeaponNumber.FirstShot] = m.Weapon.ShotStartup(f.BasicShotStartup, true);
        }

        /// <summary>A number in words: "40", "4.4", "10 m", "1 m", "5 %", "3", "50 %", "0.08 s".</summary>
        public static string Words(WeaponNumber n, double v)
        {
            switch (n)
            {
                case WeaponNumber.Damage:
                case WeaponNumber.MovingDamage: return Math.Round(v).ToString("0", s_Inv);
                case WeaponNumber.ShotsASecond: return v.ToString("0.0#", s_Inv);
                case WeaponNumber.Reach:
                case WeaponNumber.Width: return v.ToString("0.##", s_Inv) + " m";
                case WeaponNumber.CritChance:
                case WeaponNumber.MovingSpeed: return (v * 100.0).ToString("0.#", s_Inv) + " %";
                case WeaponNumber.Stagger: return v.ToString("0.#", s_Inv);
                default: return v.ToString("0.0##", s_Inv) + " s";
            }
        }

        /// <summary>The change from <paramref name="now"/> to <paramref name="then"/> in words ("+14", "-0.6", "+1 m", "-7.5 %", "-0.06 s"); empty when there is none.</summary>
        public static string Change(WeaponNumber n, double now, double then)
        {
            double d = then - now;
            if (Math.Abs(d) < Least(n)) return "";
            string sign = d > 0 ? "+" : "-";
            double a = Math.Abs(d);
            switch (n)
            {
                case WeaponNumber.Damage:
                case WeaponNumber.MovingDamage: return sign + Math.Round(a).ToString("0", s_Inv);
                case WeaponNumber.ShotsASecond: return sign + a.ToString("0.0#", s_Inv);
                case WeaponNumber.Reach:
                case WeaponNumber.Width: return sign + a.ToString("0.##", s_Inv) + " m";
                case WeaponNumber.CritChance:
                case WeaponNumber.MovingSpeed: return sign + (a * 100.0).ToString("0.#", s_Inv) + " %";
                case WeaponNumber.Stagger: return sign + a.ToString("0.#", s_Inv);
                default: return sign + a.ToString("0.0##", s_Inv) + " s";
            }
        }

        /// <summary>A change smaller than this is none (half a point of damage, a hundredth of the rest).</summary>
        static double Least(WeaponNumber n) => n == WeaponNumber.Damage || n == WeaponNumber.MovingDamage ? 0.5 : n == WeaponNumber.FirstShot ? 0.0005 : 0.005;

        /// <summary>+1 when going from <paramref name="now"/> to <paramref name="then"/> is better for her, -1 when worse, 0 when it is the same. Only the first shot's time is better lower.</summary>
        public static int Better(WeaponNumber n, double now, double then)
        {
            double d = then - now;
            if (Math.Abs(d) < Least(n)) return 0;
            bool up = d > 0;
            return n == WeaponNumber.FirstShot ? (up ? -1 : 1) : (up ? 1 : -1);
        }

        /// <summary>
        /// The parts a preview puts on: those fitted, with the slot of <paramref name="previewSlot"/> holding
        /// <paramref name="previewId"/> instead (null: that slot empty), into <paramref name="into"/> (cleared first), in the
        /// table's order as <see cref="WeaponRack.Apply"/> puts them on.
        /// </summary>
        public static void Preview(MetaState s, MetaValues v, WeaponSlot previewSlot, string previewId, AttachmentEffects into)
        {
            into.Clear();
            if (s == null || v?.Parts == null) return;
            foreach (WeaponAttachment p in v.Parts)
            {
                if (!WeaponRack.InStore(v, p) || p.Weapon != WeaponKind.Pistols) continue;
                bool on = p.Slot == previewSlot ? p.Id == previewId : WeaponRack.IsFitted(s, p.Id);
                if (on) into.Add(p);
            }
        }
    }

    // ---------------- The weapon's stage: where the parts sit, how it turns ----------------

    /// <summary>A point or a direction on the stage's model, in its box: -1 to 1 along each axis is the model's whole size (work order 139).</summary>
    [Serializable]
    public struct StagePoint
    {
        public float X, Y, Z;

        public StagePoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", X, Y, Z);
    }

    /// <summary>
    /// Where a part of a slot sits on the model and which way it faces (work order 139): <see cref="At"/> in the model's box,
    /// <see cref="Forward"/> the way the part runs from there (the barrel's way for a sight and a barrel, down the handle for a
    /// grip), <see cref="Up"/> the way its top faces. A part's shape is drawn along these.
    /// </summary>
    [Serializable]
    public struct WeaponMount
    {
        public StagePoint At, Forward, Up;

        public WeaponMount(StagePoint at, StagePoint forward, StagePoint up)
        {
            At = at;
            Forward = forward;
            Up = up;
        }
    }

    /// <summary>
    /// The WEAPON tab's stage (work order 139): its model laid as the drawing expects it (the barrel along +X, its top along +Y,
    /// the grip below), the three mount points, the camera's limits and how the model turns. The model's own way is undone by
    /// <see cref="ModelTurn"/>; the mount points are in the model's box, so a new model is set by these values alone, never by
    /// the drawing. Set for the high-detail original of the pistol in her hands (2026-11-26).
    /// </summary>
    public static class WeaponStageValues
    {
        /// <summary>
        /// Where the model is looked for (work order 139: "Load the weapon's model from one named place"): Resources/Weapon/Pistol,
        /// a prefab or a model file, or a mesh with a material beside it of the same name.
        /// </summary>
        public const string ModelPath = "Weapon/Pistol";

        /// <summary>
        /// The turn (Euler degrees, X then Y then Z as the engine applies them) that lays the model with its barrel along +X and its
        /// top along +Y. The high-detail original (Meshy, 2026-11-17) has its muzzle at -X and its top at +Z in Blender's axes, which
        /// Unity's import of the FBX brings in as +X and +Y (it turns X round and puts the up axis's turn on the model's root, which
        /// the stage keeps): (0, 0, 0) lays it, as the pictures of 2026-11-26 show.
        /// </summary>
        public static StagePoint ModelTurn = new StagePoint(0f, 0f, 0f);
        /// <summary>
        /// The stage draws the model in the toon look her pistol wears in a run (her toon shader with the model's own colours, its
        /// teal lit, the ink line round it) instead of its own lit material (URP Lit with the original's colour, normal, metallic and
        /// smoothness maps, its high detail). Off: the lit material, large and clean (the producer's choice, 2026-11-26: the owner
        /// asked to see the model "i høy res"; the toon look flattens the normal map's detail away). Read when the stage is built.
        /// </summary>
        public static bool ToonOnStage = false;
        /// <summary>The model's longest side on the stage, in the stage's units (the camera's distances are in the same).</summary>
        public const float ModelLength = 1f;

        // Set from the high-detail original's own vertices (counted in Blender, 2026-11-26), in its box (-1 to 1, x toward the muzzle,
        // y up): the barrel's top rib runs at 0.845 from x -0.1 to 0.75 (the slide's bronze block behind it at 0.88, the rear sight
        // at 1.0 round x -0.65, the front sight at 0.96 by the muzzle); the muzzle's ring spans y 0.415 to 0.785 round its bore at
        // 0.6; the grip's panels run from y -0.2 to -0.7, their middle line from x -0.59 to -0.715 (leaning back 21 degrees), 0.26
        // of the model's length deep square to it, 0.83 of the model's thickness thick at the top and 0.74 lower down; below them
        // the magazine's bronze base, level, its bottom at y -1 round x -0.70, 0.29 deep and 0.75 thick.
        /// <summary>The sight: on the barrel's top rib, ahead of the slide's bronze block, facing the barrel's way.</summary>
        public static WeaponMount Sight = new WeaponMount(new StagePoint(0.2f, 0.845f, 0f), new StagePoint(1f, 0f, 0f), new StagePoint(0f, 1f, 0f));
        /// <summary>The barrel: at the muzzle, on the bore's line (the middle of the muzzle's ring), facing out of it.</summary>
        public static WeaponMount Barrel = new WeaponMount(new StagePoint(1f, 0.6f, 0f), new StagePoint(1f, 0f, 0f), new StagePoint(0f, 1f, 0f));
        /// <summary>The grip: the middle of the grip's panels, facing down the handle (which leans back 21 degrees), its top toward the muzzle.</summary>
        public static WeaponMount Grip = new WeaponMount(new StagePoint(-0.66f, -0.45f, 0f), new StagePoint(-0.38f, -1f, 0f), new StagePoint(1f, -0.38f, 0f));

        /// <summary>The handle's depth front to back (square to it), as a share of the model's length: the grip's parts are sized to it.</summary>
        public const float HandleDepth = 0.265f;
        /// <summary>The Runner's Grip's three bands, flat straps: this far apart along the handle, this wide along it, this thick either side of their middle, this far off the handle, their corners this round (a share of the shorter half side), all shares of the model's length but the last.</summary>
        public const float GripBandSpacing = 0.05f, GripBandWidth = 0.022f, GripBandTube = 0.0035f, GripBandGap = 0.002f, GripBandCorner = 0.95f; // corners: 0.6 stood off the handle's rounded front and back
        /// <summary>The handle's thickness where each band sits (top, middle, bottom), as shares of the model's whole thickness.</summary>
        public static readonly float[] GripBandThickness = { 0.834f, 0.77f, 0.737f };
        /// <summary>The handle's foot (the middle of the magazine base's bottom face) in the model's box, where the foot's parts sit level.</summary>
        public static StagePoint FootAt = new StagePoint(-0.7f, -1f, 0f);
        /// <summary>The foot's depth front to back (a share of the model's length) and its thickness (a share of the model's thickness).</summary>
        public const float FootDepth = 0.29f, FootThickness = 0.75f;
        /// <summary>
        /// The model and its mounts stand back from the turn's middle by this share of how far the barrel part shown sticks out past
        /// the muzzle, so the pistol with its barrel stays inside the frame (2026-11-26: with the nearer camera the first long barrel
        /// ran out of it).
        /// </summary>
        public const float FrameShiftShare = 0.65f;

        /// <summary>The mount of a slot.</summary>
        public static WeaponMount MountOf(WeaponSlot slot) => slot == WeaponSlot.Sight ? Sight : slot == WeaponSlot.Barrel ? Barrel : Grip;

        // ---------------- The camera ----------------

        /// <summary>The stage camera's field of view (degrees, up and down): narrow, so the model is drawn with little distortion.</summary>
        public const float FieldOfView = 24f;
        /// <summary>
        /// The camera's distance from the model's middle at rest, and the wheel's limits (his words: "between named limits"): nearest
        /// and farthest. 2026-11-26: nearer than the first 2.35 (and 1.8 and 4.2), so the pistol fills more of its frame at rest
        /// (about seven tenths of its width, from six).
        /// </summary>
        public const float RestDistance = 2.1f;
        public const float NearestDistance = 1.45f;
        public const float FarthestDistance = 3.6f;
        /// <summary>One notch of the wheel comes this much nearer or goes back.</summary>
        public const float WheelStep = 0.25f;

        // ---------------- The turn ----------------

        /// <summary>At rest (and after R): the model turned this far about the up axis (its muzzle a little toward the camera) and tipped this far toward the camera.</summary>
        public const float RestYaw = -24f; // 2026-11-26: -28 before; less turned, its muzzle less near the camera, so it fits the nearer frame
        public const float RestPitch = 14f;
        /// <summary>A drag turns it this many degrees a layout pixel (1080-high layout), across about the up axis, up and down about the side axis.</summary>
        public const float DegreesPerPixel = 0.32f;
        /// <summary>The tip up or down is held inside this (degrees either way), so the model never turns over.</summary>
        public const float MostPitch = 70f;
        /// <summary>After a release it coasts on its last speed, which falls by e every this many seconds (the "little inertia"), and stops under <see cref="StopSpeed"/>.</summary>
        public const float CoastSeconds = 0.28f;
        public const float StopSpeed = 4f;
        /// <summary>The fastest a release lets it coast (degrees a second), so a flick does not spin it away.</summary>
        public const float MostCoastSpeed = 720f;
        /// <summary>Left alone this long (seconds), it turns slowly by itself about the up axis at this speed (degrees a second), eased in over a second.</summary>
        public const float AutoTurnAfter = 4f;
        public const float AutoTurnSpeed = 14f;
        public const float AutoTurnEaseIn = 1f;
        /// <summary>The two keys (Left and Right, A and D) turn it at this speed while held (degrees a second).</summary>
        public const float KeyTurnSpeed = 110f;

        // ---------------- The render ----------------

        /// <summary>The stage is drawn at this many pixels a layout pixel at 1080 lines (more on a larger screen), then down: with its own anti-aliasing (<see cref="Samples"/>) the model's edges are smooth.</summary>
        public const float Supersample = 2f;
        /// <summary>The stage texture's multisamples.</summary>
        public const int Samples = 4;
        /// <summary>The stage texture is never larger than this on a side (pixels).</summary>
        public const int MostTexture = 4096;

        // ---------------- The parts as models (work order 150) ----------------

        /// <summary>
        /// The nine parts are drawn on the stage as the models made for them in Blender (2026-11-26 and 27: the three sights, then
        /// the six others, one shared set of materials) instead of the shapes made in code. Kristian, 2026-11-26, shown the three
        /// sights and asked about the six others: "Ja, lag de seks og bygg inn". Off: the shapes made in code, as before work
        /// order 150. Read each time a part is made on the stage.
        /// </summary>
        public static bool ModelledParts = true;
        /// <summary>Where a part's model is looked for: Resources/Weapon/Parts/&lt;its model's name&gt; (a prefab of the model file in Art/Weapons/Parts, as the pistol's).</summary>
        public const string PartModelFolder = "Weapon/Parts/";

        /// <summary>The name of a part's model (its file in Art/Weapons/Parts and its prefab in Resources), or null for an id with none.</summary>
        public static string PartModelName(string id)
        {
            switch (id)
            {
                case WeaponAttachments.LaserSight: return "LaserSight";
                case WeaponAttachments.GhostRing: return "GhostRing";
                case WeaponAttachments.ReflexSight: return "ReflexSight";
                case WeaponAttachments.BullBarrel: return "BullBarrel";
                case WeaponAttachments.LongBarrel: return "LongBarrel";
                case WeaponAttachments.Compensator: return "Compensator";
                case WeaponAttachments.RunnersGrip: return "RunnersGrip";
                case WeaponAttachments.WeightedGrip: return "WeightedGrip";
                case WeaponAttachments.QuickDrawGrip: return "QuickDrawGrip";
                default: return null;
            }
        }

        /// <summary>A part's model's path for Resources.Load (null for an id with none).</summary>
        public static string PartModelPath(string id)
        {
            string name = PartModelName(id);
            return name == null ? null : PartModelFolder + name;
        }

        /// <summary>
        /// How far each modelled barrel reaches past the muzzle on the stage (stage units: the pistol 1 long), measured on the models
        /// in Blender (their length out of the muzzle's face over the pistol's 1.903): the Bull Barrel 0.243, the Long Barrel 0.50
        /// and the Compensator 0.26 of the pistol's own units. The frame's shift (<see cref="FrameShiftShare"/>) takes these while
        /// <see cref="ModelledParts"/> is on (the shapes made in code reach 0.115, 0.26 and 0.125).
        /// </summary>
        public const float BullBarrelReach = 0.128f, LongBarrelReach = 0.263f, CompensatorReach = 0.135f;

        /// <summary>
        /// The models' teal glows by this share of its colour: the producer's note from the Blender pictures (2026-11-27), "about
        /// 0.05", so that no part's teal is brighter than the pistol's own painted teal lines (at a full glow the parts' teal stood
        /// out brighter than the pistol's, as the shapes made in code still do at <c>AccentGlow</c> 1).
        /// </summary>
        public const float PartTealGlow = 0.05f;
    }

    /// <summary>
    /// How the model on the WEAPON tab turns (work order 139: "Drag with the mouse to turn it freely about two axes with a little
    /// inertia, the wheel to come nearer and back between named limits, a key to set it back; left alone it turns slowly by
    /// itself. With the keyboard only, two keys turn it."). Plain numbers, stepped by the caller with the real (unscaled) time: the
    /// menu stands the game still.
    /// </summary>
    public sealed class WeaponTurn
    {
        /// <summary>The turn about the up axis and the tip about the side axis (degrees), and the camera's distance.</summary>
        public float Yaw = WeaponStageValues.RestYaw, Pitch = WeaponStageValues.RestPitch, Distance = WeaponStageValues.RestDistance;
        /// <summary>The coasting speeds (degrees a second).</summary>
        public float YawSpeed, PitchSpeed;
        /// <summary>Seconds since it was last dragged, wheeled or turned by a key.</summary>
        public float Idle;
        public bool Dragging { get; private set; }
        float _dragX, _dragY;
        int _keyTurn;

        /// <summary>Back to rest (R), still.</summary>
        public void Reset()
        {
            Yaw = WeaponStageValues.RestYaw;
            Pitch = WeaponStageValues.RestPitch;
            Distance = WeaponStageValues.RestDistance;
            YawSpeed = PitchSpeed = 0f;
            Idle = 0f;
            Dragging = false;
            _dragX = _dragY = 0f;
        }

        /// <summary>The mouse is pressed on the model: it is held, its coast stops.</summary>
        public void BeginDrag()
        {
            Dragging = true;
            YawSpeed = PitchSpeed = 0f;
            Idle = 0f;
        }

        /// <summary>The mouse moved this many layout pixels while held (right turns it right, down tips it toward the camera): taken in at the next <see cref="Step"/>.</summary>
        public void Drag(float dx, float dy)
        {
            if (!Dragging) BeginDrag();
            _dragX += dx;
            _dragY += dy;
            Idle = 0f;
        }

        /// <summary>The mouse let go: it coasts on.</summary>
        public void EndDrag()
        {
            Dragging = false;
            Idle = 0f;
        }

        /// <summary>The wheel: <paramref name="notches"/> away from her (positive, as the engine's scroll down) goes back, toward her comes nearer, inside the limits.</summary>
        public void Wheel(float notches)
        {
            Distance = Clamp(Distance + notches * WeaponStageValues.WheelStep, WeaponStageValues.NearestDistance, WeaponStageValues.FarthestDistance);
            Idle = 0f;
        }

        /// <summary>The keys held now: -1 left, 1 right, 0 none (read every frame).</summary>
        public void KeyTurn(int direction) => _keyTurn = direction < 0 ? -1 : direction > 0 ? 1 : 0;

        /// <summary>One frame of <paramref name="dt"/> real seconds: the drag taken in (its speed kept for the release), the coast, the keys, the slow turn by itself.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            if (Dragging)
            {
                float dYaw = _dragX * WeaponStageValues.DegreesPerPixel, dPitch = _dragY * WeaponStageValues.DegreesPerPixel;
                _dragX = _dragY = 0f;
                Yaw += dYaw;
                Pitch = Clamp(Pitch + dPitch, -WeaponStageValues.MostPitch, WeaponStageValues.MostPitch);
                // The speed it lets go with: this frame's, eased with the last ones' (a still frame in a drag slows it).
                float k = 1f - (float)Math.Exp(-dt / 0.05);
                YawSpeed += (Clamp(dYaw / dt, -WeaponStageValues.MostCoastSpeed, WeaponStageValues.MostCoastSpeed) - YawSpeed) * k;
                PitchSpeed += (Clamp(dPitch / dt, -WeaponStageValues.MostCoastSpeed, WeaponStageValues.MostCoastSpeed) - PitchSpeed) * k;
                return;
            }
            if (_keyTurn != 0)
            {
                Yaw += _keyTurn * WeaponStageValues.KeyTurnSpeed * dt;
                YawSpeed = PitchSpeed = 0f;
                Idle = 0f;
                return;
            }
            if (YawSpeed != 0f || PitchSpeed != 0f)
            {
                Yaw += YawSpeed * dt;
                Pitch = Clamp(Pitch + PitchSpeed * dt, -WeaponStageValues.MostPitch, WeaponStageValues.MostPitch);
                float fall = (float)Math.Exp(-dt / WeaponStageValues.CoastSeconds);
                YawSpeed *= fall;
                PitchSpeed *= fall;
                if (Math.Abs(YawSpeed) < WeaponStageValues.StopSpeed && Math.Abs(PitchSpeed) < WeaponStageValues.StopSpeed) YawSpeed = PitchSpeed = 0f;
                Idle = 0f;
                return;
            }
            Idle += dt;
            float auto = Idle - WeaponStageValues.AutoTurnAfter;
            if (auto > 0f) Yaw += WeaponStageValues.AutoTurnSpeed * Math.Min(1f, auto / WeaponStageValues.AutoTurnEaseIn) * dt;
        }

        /// <summary>Is it moving now (a drag, a coast, a key or the slow turn by itself): the stage draws again only then.</summary>
        public bool Moving => Dragging || _keyTurn != 0 || YawSpeed != 0f || PitchSpeed != 0f || Idle > WeaponStageValues.AutoTurnAfter;

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    // ---------------- Where the three pages put things ----------------

    /// <summary>
    /// Where the three pages put their parts (work order 139), in the start menu's 1080-high layout, laid out on a 1920-wide
    /// plan and fitted into a narrower view (<see cref="Fit"/>); a rule EditMode tests for overlaps. First guesses.
    /// - WEAPON: the three short lists at the left (a slot's head, "none" and its parts), the stage in the middle (the model,
    ///   large), the part's panel under the stage, her numbers at the right.
    /// - RANKS: one row a rank across the middle: its name, a row of steps, the next step's gain, its price.
    /// - RACK: the pieces as a list at the left, one large panel at the right for the piece under the cursor.
    /// </summary>
    public static class UpgradeTabsLayout
    {
        /// <summary>The plan's width, and the band its pages stand in: from under the tabs to over the foot's note.</summary>
        public const float PlanWidth = 1920f;
        public const float Top = 124f;
        public const float Bottom = 948f;
        public const float Left = 48f;
        public const float Right = 1872f;
        public const float Gap = 26f;

        // WEAPON
        public const float ListsWidth = 420f;
        public const float ListHead = 40f;
        public const float ListRow = 46f;
        public const float ListGap = 12f;
        public const float NumbersWidth = 410f;
        public const float NumbersRow = 46f;
        public const float PartPanelHeight = 170f;
        // RANKS
        public const float RanksWidth = 1600f;
        public const float RankRow = 132f;
        public const float RankGap = 22f;
        public const float RankNameWidth = 290f;
        public const float RankStepsWidth = 580f;
        public const float RankPriceWidth = 190f;
        public const int MostSteps = 10;
        // RACK
        public const float RackListWidth = 640f;
        public const float RackRow = 54f;

        /// <summary>The fit and the left edge of the plan in a view <paramref name="viewWidth"/> wide: 1 and centred when it fits, smaller when not.</summary>
        public static float Fit(float viewWidth, out float x0)
        {
            float fit = Math.Min(1f, Math.Max(0.1f, viewWidth / PlanWidth));
            x0 = (viewWidth / fit - PlanWidth) * 0.5f;
            return fit;
        }

        public static LayoutRect Lists => new LayoutRect(Left, Top, ListsWidth, Bottom - Top);
        public static LayoutRect Numbers => new LayoutRect(Right - NumbersWidth, Top, NumbersWidth, PanelHeadHeight + WeaponNumbers.Count * NumbersRow + 24f);
        public static LayoutRect Stage
        {
            get
            {
                float x = Left + ListsWidth + Gap, r = Right - NumbersWidth - Gap;
                return new LayoutRect(x, Top, r - x, Bottom - Top - PartPanelHeight - Gap);
            }
        }
        public static LayoutRect PartPanel
        {
            get
            {
                LayoutRect s = Stage;
                return new LayoutRect(s.X, s.Bottom + Gap, s.Width, PartPanelHeight);
            }
        }

        /// <summary>The panels' head (their heading's room, as the menus' <c>PanelHeadHeight</c> and <c>PanelPad</c> give it).</summary>
        public const float PanelHeadHeight = 64f;

        /// <summary>The WEAPON lists' rows: for each slot a head then <paramref name="rowsPerSlot"/> rows ("none" and its parts), top to bottom.</summary>
        public static LayoutRect ListRowRect(int slot, int row, IReadOnlyList<int> rowsPerSlot)
        {
            float y = Top + PanelHeadHeight;
            for (int s = 0; s < slot && s < rowsPerSlot.Count; s++) y += ListHead + rowsPerSlot[s] * ListRow + ListGap;
            return new LayoutRect(Left + 18f, y + ListHead + row * ListRow, ListsWidth - 36f, ListRow - 4f);
        }

        public static LayoutRect ListHeadRect(int slot, IReadOnlyList<int> rowsPerSlot)
        {
            LayoutRect first = ListRowRect(slot, 0, rowsPerSlot);
            return new LayoutRect(first.X, first.Y - ListHead, first.Width, ListHead);
        }

        public static LayoutRect NumberRow(int i)
        {
            LayoutRect n = Numbers;
            return new LayoutRect(n.X + 18f, n.Y + PanelHeadHeight + i * NumbersRow, n.Width - 36f, NumbersRow);
        }

        /// <summary>The RANKS page's panel for <paramref name="ranks"/> rows, centred on the plan.</summary>
        public static LayoutRect RanksPanel(int ranks) =>
            new LayoutRect((PlanWidth - RanksWidth) * 0.5f, Top + 20f, RanksWidth, PanelHeadHeight + ranks * (RankRow + RankGap) + 8f);

        public static LayoutRect RankRowRect(int i, int ranks)
        {
            LayoutRect p = RanksPanel(ranks);
            return new LayoutRect(p.X + 24f, p.Y + PanelHeadHeight + i * (RankRow + RankGap), p.Width - 48f, RankRow);
        }

        /// <summary>The steps of a rank row: <paramref name="most"/> of them (at most <see cref="MostSteps"/> wide) across its steps' room.</summary>
        public static LayoutRect StepRect(LayoutRect row, int step, int most)
        {
            float room = RankStepsWidth, gap = 10f;
            int n = Math.Max(1, Math.Max(most, MostSteps));
            float w = (room - (n - 1) * gap) / n, h = 46f;
            return new LayoutRect(row.X + RankNameWidth + step * (w + gap), row.Y + 30f, w, h);
        }

        public static LayoutRect RackList(int pieces) => new LayoutRect(Left, Top, RackListWidth, PanelHeadHeight + pieces * RackRow + 20f);
        public static LayoutRect RackRowRect(int i) => new LayoutRect(Left + 18f, Top + PanelHeadHeight + i * RackRow, RackListWidth - 36f, RackRow - 6f);
        public static LayoutRect RackPanel => new LayoutRect(Left + RackListWidth + Gap + 14f, Top, Right - (Left + RackListWidth + Gap + 14f), RackPanelHeight);
        /// <summary>The rack's large panel's height: its words and its press, no empty half under them.</summary>
        public const float RackPanelHeight = 600f;
    }
}
