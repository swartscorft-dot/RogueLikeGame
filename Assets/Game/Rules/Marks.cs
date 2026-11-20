using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the kit as a whole (decision 0005, 2026-11-19). They live in the skill settings asset, beside the
    /// identity gauge's.
    /// </summary>
    [Serializable]
    public sealed class KitValues
    {
        public int MaxMarks = 5;               // marks a target can carry at once
        public float MarkLifetime = 6f;        // s after the last mark was added that they all fall off
        public int BasicShotsPerMark = 5;      // every this many basic shot hits, the hit that completes the count marks its target (4 until 2026-11-19)
        public bool StartWithAllSkills = false; // every skill had from the start, for trying the ones that wait (the first eight on the keys: work order 015)
        public float CleanShare = 1f / 3f;     // the last share of the final recast window in which a press is clean
        public float CleanDamage = 1.5f;       // a clean tap deals this times the damage (not a clean spend: that has its bonus per mark)
    }

    /// <summary>
    /// The marks on one target (decision 0005, 2026-11-19: the pistols set up and the shotgun cashes in). Builders add
    /// them, up to a cap; they all fall off a lifetime after the last one was added; a spender takes them all. A dead
    /// or reset target has none (<see cref="Clear"/>). Time is passed in, in seconds. The cap and the lifetime are read
    /// when marks are added, so a change applies from the next mark.
    /// </summary>
    public sealed class Marks
    {
        int _count;
        float _fallOffAt = float.NegativeInfinity;

        /// <summary>The marks it carries now: none once they have fallen off.</summary>
        public int Count(float now) => now < _fallOffAt ? _count : 0;

        /// <summary>When the marks it carries fall off; negative infinity when it carries none.</summary>
        public float FallOffAt => _count > 0 ? _fallOffAt : float.NegativeInfinity;

        /// <summary>Adds marks, no more than the cap in all, and starts their lifetime again. Returns the count after.</summary>
        public int Add(int marks, float now, int max, float lifetime)
        {
            if (marks <= 0) return Count(now);
            int have = Count(now);
            _count = Math.Min(Math.Max(0, max), have + marks);
            _fallOffAt = now + Math.Max(0f, lifetime);
            return _count;
        }

        /// <summary>Takes every mark it carries, for a spender. Returns how many were taken.</summary>
        public int TakeAll(float now)
        {
            int taken = Count(now);
            _count = 0;
            _fallOffAt = float.NegativeInfinity;
            return taken;
        }

        /// <summary>
        /// Marks that have fallen off by now and were not yet counted as such: returns how many and forgets them.
        /// For the play session log (marks that fell off unused); it changes nothing a hit sees.
        /// </summary>
        public int TakeFallenOff(float now)
        {
            if (_count <= 0 || now < _fallOffAt) return 0;
            int fallen = _count;
            _count = 0;
            _fallOffAt = float.NegativeInfinity;
            return fallen;
        }

        /// <summary>Removes every mark at once (a dead or reset target). Returns how many it carried.</summary>
        public int Clear(float now) => TakeAll(now);
    }

    /// <summary>
    /// The rules around marks that are not a target's own (decision 0005, 2026-11-19): when the basic shot marks, and
    /// what a spender's hit deals.
    /// </summary>
    public static class MarkRules
    {
        /// <summary>
        /// A spender's hit: <paramref name="bonusPerMark"/> more of the damage for each mark it took. 0.25 means +25 %
        /// per mark, so +125 % at five. Rounded to a whole number.
        /// </summary>
        public static int SpenderDamage(int damage, int marksTaken, float bonusPerMark) =>
            (int)Math.Round(damage * (1f + Math.Max(0f, bonusPerMark) * Math.Max(0, marksTaken)));

        /// <summary>True for a skill that spends marks: it has a bonus per mark.</summary>
        public static bool IsSpender(SkillDefinition skill) => skill != null && skill.MarkDamageBonus > 0f;

        /// <summary>
        /// True when a clean last press of this skill spends the marks instead of being a clean tap: it has a clean bonus
        /// per mark (Backstep; decision 0005, amended 2026-11-19 after Kristian played).
        /// </summary>
        public static bool HasCleanSpend(SkillDefinition skill) => skill != null && skill.CleanMarkDamageBonus > 0f;

        /// <summary>
        /// True when a hit of this skill takes the marks: every hit of a spender, and the hit of a clean press of a
        /// skill with a clean spend. <paramref name="clean"/>: the shot came from a clean press.
        /// </summary>
        public static bool Spends(SkillDefinition skill, bool clean) =>
            IsSpender(skill) || (clean && HasCleanSpend(skill)) || (skill != null && skill.MarkStaggerBonus > 0f); // Breaker Slug (work order 015)

        /// <summary>
        /// The bonus per mark taken that a hit of this skill deals: the clean spend's for a clean press of a skill that
        /// has one, else the spender's own. 0 for a hit that spends nothing.
        /// </summary>
        public static float BonusPerMark(SkillDefinition skill, bool clean)
        {
            if (skill == null) return 0f;
            if (clean && HasCleanSpend(skill)) return skill.CleanMarkDamageBonus;
            return Math.Max(0f, skill.MarkDamageBonus);
        }

        /// <summary>True when a clean press of this skill is a clean tap: it has one and no clean spend takes its place.</summary>
        public static bool IsCleanTap(SkillDefinition skill, bool clean) => clean && !HasCleanSpend(skill);
    }

    /// <summary>
    /// Counts the basic shot's hits, whatever the target: every <see cref="KitValues.BasicShotsPerMark"/>-th hit marks
    /// the target it hit (decision 0005, 2026-11-19). While the focus window runs every hit marks and the count stands.
    /// </summary>
    public sealed class BasicShotMarks
    {
        int _hits;

        /// <summary>Hits counted toward the next mark.</summary>
        public int Counted => _hits;

        /// <summary>A basic shot hit. True if this hit marks its target.</summary>
        public bool Hit(int perMark, bool everyHit)
        {
            if (everyHit) return true;
            _hits++;
            if (_hits < Math.Max(1, perMark)) return false;
            _hits = 0;
            return true;
        }

        public void Reset() => _hits = 0;
    }
}
