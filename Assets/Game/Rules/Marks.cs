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
        public float CleanShare = 1f / 3f;     // the last share of the final recast window in which a press is clean (with CleanEarly off)
        // Work order 040 (Kristian's second session of 2026-11-20: "the timing must come much earlier"): the clean part from
        // CleanFrom to CleanTo seconds after the window opens (CleanPart), his own rhythm held a hair, instead of the last
        // CleanShare. First guesses. The asset holds none of these three, so these defaults are what the game reads.
        public bool CleanEarly = true;         // off: the clean part is the window's last CleanShare, as before
        public float CleanFrom = 0.10f;        // s after the window opened that the clean part begins
        public float CleanTo = 0.30f;          // s after it opened that it ends; a press after it, in the window, is a plain shot, late
        public bool RecastOnRealTime = true;   // a hit stop does not hold the recast window: it runs on real seconds, slowed with a slow-down, never sped up (work order 038, Kristian's answer of 2026-11-20); off, as before
        // Work order 064 (Kristian, 2026-11-21: "det må også belønes mer for å treffe høyre klik"): 1.75, up from 1.5. The skill
        // settings asset (Skills.asset, Kit) still holds 1.5, and the game reads the asset: the producer sets it there.
        public float CleanDamage = 1.75f;      // a clean tap deals this times the damage (not a clean spend: that has its bonus per mark)

        // Work order 064 (Kristian, 2026-11-21): a Triple Tap chain whose every recast press fell in its clean part (a full clean
        // chain) ends with more on its clean last tap: the thing worth learning. First guesses; the asset holds neither, so these
        // defaults are what the game reads. 1 and 0 give the chain as it was.
        public float FullChainScale = 2f;      // the clean last tap of a full clean chain deals this times a clean tap
        public int FullChainMarks = 1;         // and adds this many marks more than a clean tap's

        // Work order 064 (Kristian, 2026-11-21: "F føles ikke like smooth lenger ... syntes skuddet bør gå av kjappere"; the
        // producer's numbers of the same day). Backstep's shot after its hop (a chain that opens with a move): in his log of
        // 12:12 his second press came a median 0.066 s after the hop ended, against 0.135 s the evening before, and 14 of 20
        // fell before the clean part's 0.10 s. The clean part of the window after an opening move moves early by the same
        // 0.20 s width, as work order 040 moved the first one; a press that waited from the hop is still never clean. The
        // asset holds none of these, so these defaults are what the game reads. Each below 0 gives the morning's.
        public float OpeningMoveCleanFrom = 0.03f;  // s after the hop's end that the clean part begins; below 0, CleanFrom (0.10)
        public float OpeningMoveCleanTo = 0.23f;    // s after it that the clean part ends; below 0, CleanTo (0.30)
        public float OpeningMoveShotStartup = 0.08f; // s from the second press to the shot; below 0, the skill's Startup (0.2)
        // (OpeningMoveCooldownBuffer, Backstep's own 0.25 s, became CooldownBuffer below in work order 066.)
        public bool OpeningMoveCutsRecovery = true; // the hop is the escape: it starts in another skill's free recovery at once; off, it waits it out

        // Work order 066 (Kristian, 2026-11-21, asked whether work order 064's buffer for Backstep should hold for every skill:
        // "Ja, alle ferdigheter"). One value for all skills, Backstep's 0.25 s of work order 064 (CooldownBufferRules). The
        // asset holds none of it, so this default is what the game reads.
        public float CooldownBuffer = 0.25f;  // s: a press of any skill this long or less before its cooldown ends waits and goes; 0: refused, as this morning

        // Work order 069 (Kristian, 2026-11-21 evening: "høyreklik er litt klunky. jeg vil at de to første skal kome relativt
        // fort, og det 3. litt senere"): a chain that opens with a shot (Triple Tap; not Backstep, whose chain opens with its
        // hop) has a rhythm: tap-tap, a beat, then the last. Every press before the last (the second; with Fourth Tap or Steady
        // Hand the third and fourth too) is a quick press, its window short and its clean part early; the last press waits a
        // beat, its window longer and its clean part later (RecastRhythm). Seconds after the shot before the press went off, on
        // the window's clock. They are the kit's and not the skill's because the asset holds none of them: as kit values their
        // defaults here are what the game reads with no edit of the asset, and the switch gives the morning's back at once.
        // From his own presses of that evening (2026-11-21_20-20-54): his quick presses came a median 0.23 s after the shot on
        // the window's clock (p10 0.13, p90 0.31), so the quick part is centred on them; the last part's middle (0.54 s) is
        // about twice his beat from press to press (0.15 s of the shot's startup and 0.21 s: 0.36 s; 0.69 s for the last).
        // First guesses; he tunes them by playing.
        public bool ChainRhythm = true;            // off: every window of a chain the skill's RecastWindow (0.6) and its clean part CleanFrom to CleanTo, as this morning
        public float QuickPressWindow = 0.45f;     // s after the shot that a quick press's window stays open; below 0, the skill's RecastWindow
        public float QuickPressCleanFrom = 0.12f;  // s after the shot that a quick press's clean part begins (the producer's guess was 0.10); below 0, CleanFrom
        public float QuickPressCleanTo = 0.30f;    // s after the shot that it ends (the producer's guess was 0.28); below 0, CleanTo
        public float LastPressWindow = 0.80f;      // s after the shot before it that the last press's window stays open; below 0, the skill's RecastWindow
        public float LastPressCleanFrom = 0.42f;   // s after that shot that the last press's clean part begins: the beat; below 0, CleanFrom
        public float LastPressCleanTo = 0.66f;     // s after it that it ends; below 0, CleanTo

        // Work order 138 (Kristian, 2026-11-25: "Jeg tror også vi har litt for mange spell slots. Kanskje fjerne 2 spell slots";
        // his tick "To av de tomme": four skills from the start, two found on the way): six skill slots, Q, 1, 2, R, F and the
        // right button; keys 3 and 4 hold nothing (SkillSlots). The asset holds none of it, so this default is what the game
        // reads. Off: eight slots, as before.
        public bool SixSlots = true;
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

        /// <summary>
        /// THE one place a clean tap's bonus enters its damage (work order 064; order 065's chest stat "clean press" multiplies
        /// it here): <see cref="KitValues.CleanDamage"/>, times <see cref="KitValues.FullChainScale"/> for the clean last tap of a
        /// full clean chain (<paramref name="fullChain"/>). With the stats on, the caller folds it (CritRules.Folded): the sure
        /// crit is dealt after. A clean spend's bonus is not here: it is its bonus per mark (<see cref="BonusPerMark"/>).
        /// </summary>
        public static float CleanTapScale(KitValues kit, bool fullChain)
        {
            kit = kit ?? new KitValues();
            float scale = Math.Max(0f, kit.CleanDamage);
            return fullChain ? scale * Math.Max(0f, kit.FullChainScale) : scale;
        }

        /// <summary>The marks a clean tap adds: the skill's clean tap marks, and for the last tap of a full clean chain <see cref="KitValues.FullChainMarks"/> more (work order 064).</summary>
        public static int CleanTapMarks(SkillDefinition skill, KitValues kit, bool fullChain) =>
            skill == null ? 0 : skill.CleanTapMarks + (fullChain && skill.CleanTapMarks > 0 ? Math.Max(0, (kit ?? new KitValues()).FullChainMarks) : 0);

        /// <summary>
        /// True when a clean last press ends a full clean chain (work order 064): a clean tap (not a clean spend), a chain of
        /// three or more presses, and every press after the first in the clean part of its window.
        /// </summary>
        public static bool IsFullCleanChain(SkillDefinition skill, bool clean, int presses, bool allInRhythm) =>
            IsCleanTap(skill, clean) && skill != null && skill.CleanTapMarks > 0 && presses >= 3 && allInRhythm;
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
