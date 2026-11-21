using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// Wide window (work order 067, 2026-11-21: Kristian asked for "og mere stats" and ticked "Wide window og Blast" for the
    /// evening): the clean part of every recast window and the perfect band of a charge are wider. This is the one rule every
    /// reader goes through: the clean part is widened in <see cref="CleanPart.Of(KitValues, float, bool, float)"/>, which the
    /// character reads in one place for its judging, its signs, the cast blade and the strip's box; the perfect band is
    /// widened on the skill as she has it (<see cref="SkillStatLayer"/>), which the judging (<see cref="PoolRules.IsPerfect"/>),
    /// the cast blade (<see cref="CastBarRules.PerfectBand"/>) and the timing's words read. So what she is shown is what is
    /// judged.
    ///
    /// A part is widened ROUND ITS MIDDLE by its share of its own width (+10 %: a 0.20 s part is 0.22 s, 0.01 s more at either
    /// edge), so that a press a little early and one a little late both gain; never beyond the window it lies in. What would
    /// go past the window's start or end is given to the other side, so the width gained is always the stat's: Backstep's
    /// clean part (0.03 to 0.23 s after the hop) at +50 % is 0 to 0.30 s, not 0 to 0.28 s (the producer's guess of the order).
    /// Only a part wider than the whole window is cut: it is then the whole window. With the stat at 0 the part is exactly
    /// what it was.
    /// </summary>
    public static class WideWindow
    {
        /// <summary>
        /// The part <paramref name="from"/> to <paramref name="to"/>, lying in the window <paramref name="windowFrom"/> to
        /// <paramref name="windowTo"/> (seconds), widened by <paramref name="share"/> of its width round its middle, inside the
        /// window. A share of 0 or less, a part with no width or a window with none: the part as it is.
        /// </summary>
        public static void Widen(float from, float to, float windowFrom, float windowTo, float share, out float wideFrom, out float wideTo)
        {
            wideFrom = from;
            wideTo = to;
            if (!(share > 0f) || !(to > from) || !(windowTo > windowFrom)) return;
            float width = to - from, room = windowTo - windowFrom;
            float wide = width * (1f + share);
            if (wide >= room)
            {
                wideFrom = windowFrom;
                wideTo = windowTo;
                return;
            }
            float half = (wide - width) * 0.5f;
            float f = from - half, t = to + half;
            if (f < windowFrom) // clamped at the window's start: what is cut there is given to its end
            {
                t += windowFrom - f;
                f = windowFrom;
            }
            if (t > windowTo) // clamped at its end: given to its start
            {
                f -= t - windowTo;
                t = windowTo;
            }
            wideFrom = Math.Max(windowFrom, f);
            wideTo = Math.Min(windowTo, t);
        }

        /// <summary>A clean part of a window <paramref name="window"/> seconds long (it lies from 0 to there), widened.</summary>
        public static CleanPart Widen(CleanPart part, float window, float share)
        {
            if (!(share > 0f) || !part.Any) return part;
            Widen(part.From, part.To, 0f, Math.Max(0f, window), share, out float f, out float t);
            return new CleanPart(f, t);
        }

        /// <summary>
        /// The window a charge's perfect band lies in: from the start of the charge to where the charge fires by itself
        /// (<see cref="SkillDefinition.HoldMax"/>), or to its full charge or the band's end if either is later.
        /// </summary>
        public static float BandWindow(SkillDefinition s) => s == null ? 0f : Math.Max(Math.Max(0f, s.HoldMax), Math.Max(s.ChargeTime, s.PerfectTo));

        /// <summary>True when the skill has a perfect band (a Charge skill with one; Long Shot).</summary>
        public static bool HasBand(SkillDefinition s) => s != null && s.Input == SkillInput.Charge && s.PerfectTo > s.PerfectFrom;

        /// <summary>True when the skill's last recast press can be clean: it has a clean part to widen (Backstep, Triple Tap).</summary>
        public static bool HasCleanPart(SkillDefinition s) => s != null && s.RecastCount > 1 && RecastChain.HasCleanPress(s);

        /// <summary>Widens the skill's perfect band in place (a copy of the run's: <see cref="SkillStatLayer.Apply"/>); nothing without a band.</summary>
        public static void WidenBand(SkillDefinition s, float share)
        {
            if (!HasBand(s) || !(share > 0f)) return;
            Widen(s.PerfectFrom, s.PerfectTo, 0f, BandWindow(s), share, out float f, out float t);
            s.PerfectFrom = f;
            s.PerfectTo = t;
        }

        /// <summary>The clean part of this skill's last window as the kit places it, widened by <paramref name="share"/> (for the card's numbers).</summary>
        public static CleanPart CleanPartOf(SkillDefinition s, KitValues kit, float share) =>
            s == null ? default : CleanPart.Of(kit, s.RecastWindow, s.OpeningMove, share);
    }

    /// <summary>
    /// Blast (work order 067): her area skills are larger. Each area skill has ONE number that is its area and grows, by
    /// the shape it has as she has it (so an elite upgrade that changes its shape changes which number grows): a burst's
    /// radius (Powder Keg), a cone's angle (Scatter Blast, Fan Fire), a line's width (Heavy Volley, Breaker Slug, and Scatter
    /// Blast with Slug, its line in place of a cone). Never a skill's reach or range (that is another stat's). A skill with no
    /// area (a single shot down a thin line, a hop, a counter: Quick Shot, Long Shot, Backstep, Triple Tap, Tag Round, Slide,
    /// Stopping Shot) is untouched. Which skills are area skills is a list of names (<see cref="StatValues.BlastSkills"/>), so a
    /// skill tuned in the settings keeps its place. The growth is a plain multiplication on the skill as her run's upgrades
    /// make it, so an elite upgrade that scales an area and the stat multiply in either order (Big Keg 2.5 m to 3.33 m, then
    /// +8 %: 3.6 m).
    /// </summary>
    public static class Blast
    {
        /// <summary>True when the skill is one of the area skills named.</summary>
        public static bool IsArea(SkillDefinition s, IReadOnlyList<string> areaSkills)
        {
            if (s == null || areaSkills == null) return false;
            for (int i = 0; i < areaSkills.Count; i++)
                if (areaSkills[i] == s.Name) return true;
            return false;
        }

        /// <summary>The skill's one area number, by its shape: a circle's radius (m), a cone's angle (degrees), a line's width (m, both sides).</summary>
        public static float Of(SkillDefinition s) =>
            s == null ? 0f : s.Shape == SkillShape.Circle ? s.Radius : s.Shape == SkillShape.Cone ? s.Angle : 2f * s.HalfWidth;

        /// <summary>What the number is called on a card: "burst", "cone", "width".</summary>
        public static string Word(SkillDefinition s) =>
            s == null ? "" : s.Shape == SkillShape.Circle ? "burst" : s.Shape == SkillShape.Cone ? "cone" : "width";

        /// <summary>Grows the skill's one area number in place by <paramref name="share"/> (a copy of the run's: <see cref="SkillStatLayer.Apply"/>).</summary>
        public static void Grow(SkillDefinition s, float share)
        {
            if (s == null || !(share > 0f)) return;
            float by = 1f + share;
            switch (s.Shape)
            {
                case SkillShape.Circle: s.Radius *= by; break;
                case SkillShape.Cone: s.Angle *= by; break;
                default: s.HalfWidth *= by; break;
            }
        }
    }

    /// <summary>
    /// Her stats that change a skill as she has it (work order 067): Blast's share and Wide window's, with the names of the
    /// area skills. Laid on each skill after her run's upgrades, in the run's view of the skills
    /// (<see cref="RunSkillUpgrades.View(IReadOnlyList{SkillDefinition}, SkillStatLayer)"/>): the one place a skill's area and
    /// its perfect band are read as she has them, so the hit, the aim shape, the effects, the keg's fuse circle, the cast
    /// blade and the judging all read the same number. None (both 0, the stats off) changes nothing.
    /// </summary>
    public readonly struct SkillStatLayer : IEquatable<SkillStatLayer>
    {
        public readonly float Blast, WideWindow;
        public readonly IReadOnlyList<string> AreaSkills;

        public SkillStatLayer(float blast, float wideWindow, IReadOnlyList<string> areaSkills)
        {
            Blast = Math.Max(0f, blast);
            WideWindow = Math.Max(0f, wideWindow);
            AreaSkills = areaSkills;
        }

        /// <summary>True when it changes anything.</summary>
        public bool Any => Blast > 0f || WideWindow > 0f;

        /// <summary>
        /// The skill with the layer on it: a copy when it changes it (which keeps the skill it came from as
        /// <see cref="SkillDefinition.BeforeStats"/>, for the cards that reckon an upgrade on it), else the skill itself.
        /// </summary>
        public SkillDefinition Apply(SkillDefinition skill)
        {
            if (skill == null || !Any) return skill;
            bool area = Blast > 0f && Rules.Blast.IsArea(skill, AreaSkills);
            bool band = WideWindow > 0f && Rules.WideWindow.HasBand(skill);
            if (!area && !band) return skill;
            SkillDefinition copy = skill.Copy();
            copy.SetBeforeStats(skill.BeforeStats);
            if (area) Rules.Blast.Grow(copy, Blast);
            if (band) Rules.WideWindow.WidenBand(copy, WideWindow);
            return copy;
        }

        public bool Equals(SkillStatLayer other) => Blast == other.Blast && WideWindow == other.WideWindow && ReferenceEquals(AreaSkills, other.AreaSkills);
        public override bool Equals(object obj) => obj is SkillStatLayer o && Equals(o);
        public override int GetHashCode() => Blast.GetHashCode() * 31 + WideWindow.GetHashCode();
    }
}
