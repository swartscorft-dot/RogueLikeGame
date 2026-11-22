using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// What a skill of <see cref="PlayerCharacter"/> does when it lands: its hit and its area, the marks it adds, a tag spread, and
    /// the hooks of a use that hit, killed, dazed or countered (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// A use of a skill hit something (work order 017, the one hook for a hit, a kill, a daze and a counter): a cut to
        /// a cooldown for the hit (Tied Trigger), half its own cooldown off for a kill (Fresh Shells) or a daze (Follow
        /// Through), and a skill made ready by a counter (Riposte).
        /// </summary>
        void UseLanded(SkillDefinition skill, int slot, bool killed, bool dazed, bool countered)
        {
            float now = Time.time;
            if (skill.HitCooldownCut > 0f)
            {
                int cut = string.IsNullOrEmpty(skill.HitCutsSkill) ? slot : SlotOf(skill.HitCutsSkill);
                if (cut >= 0) CooldownFor(cut).CutSeconds(now, skill.HitCooldownCut);
            }
            // Once a use, however many blasts or hits it has (the second reviews of 2026-11-20, work order 033: Steady Hand's
            // second Scatter Blast with Fresh Shells made it ready again at once).
            if (slot >= 0 && killed && skill.KillCooldownCut > 0f && !_useKillCut)
            {
                _useKillCut = true;
                CooldownFor(slot).CutSeconds(now, CooldownOf(slot) * Mathf.Clamp01(skill.KillCooldownCut));
            }
            if (slot >= 0 && dazed && skill.DazeCooldownCut > 0f && !_useDazeCut)
            {
                _useDazeCut = true;
                CooldownFor(slot).CutSeconds(now, CooldownOf(slot) * Mathf.Clamp01(skill.DazeCooldownCut));
            }
            if (countered && !string.IsNullOrEmpty(skill.CounterReadies))
            {
                int ready = SlotOf(skill.CounterReadies);
                if (ready >= 0) CooldownFor(ready).MakeReady(now);
            }
        }

        /// <summary>
        /// A tagged target died (work order 017, Spreading Tag): the tag of the skill she has that spreads it jumps to the
        /// nearest other living target within its reach, with its time renewed.
        /// </summary>
        void SpreadTag(Damageable dead)
        {
            IReadOnlyList<SkillDefinition> skills = Skills;
            SkillDefinition spreads = null;
            for (int slot = 0; slot < skills.Count && spreads == null; slot++)
                if (skills[slot] != null && skills[slot].TagJump > 0f && skills[slot].TagSeconds > 0f && IsEquipped(slot)) spreads = skills[slot];
            if (spreads == null) return;
            Damageable nearest = null;
            float best = spreads.TagJump;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == dead || !d.IsAlive) continue;
                float distance = d.GroundPosition.DistanceTo(dead.GroundPosition);
                if (distance > best) continue;
                best = distance;
                nearest = d;
            }
            if (nearest == null) return;
            nearest.ApplyTag(spreads.TagSeconds, spreads.TagDamageShare);
            MarkPips.For(nearest, ShotFxMaterial);
            TagsSpread++;
        }

        /// <summary>Puts marks on a target, shows them over it and reports it. <paramref name="slot"/> is -1 for the basic shot.</summary>
        void AddMarks(Damageable target, int marks, int slot)
        {
            if (marks <= 0 || target == null || !target.IsAlive) return;
            // Work order 029 (Marked Target, a piece of the rack between runs): a hit that marks a target with none gives more.
            if (Modifiers != null && Modifiers.Meta.FirstMarkExtra > 0 && target.MarkCount == 0) marks += Modifiers.Meta.FirstMarkExtra;
            KitValues kit = Kit;
            // The run's Lasting marks (work order 025): they stay longer.
            int after = target.AddMarks(marks, kit.MaxMarks, kit.MarkLifetime + (Modifiers != null ? Modifiers.MarkLifetimeBonus : 0f));
            MarkPips pips = MarkPips.For(target, ShotFxMaterial);
            if (pips != null) pips.Max = kit.MaxMarks;
            Report(new PlayerReport { Kind = PlayerReportKind.MarkAdded, Slot = slot, Target = target, Marks = marks, MarksAfter = after });
        }

        /// <summary>
        /// The marks a builder's hit adds to a target: the skill's, or for a clean tap the clean tap's (a clean spend
        /// takes the marks and never comes here). A skill with a
        /// mark interval marks a target at most that often in one use (Fan Fire: once a second, not every tick).
        /// </summary>
        void MarkFromSkill(SkillDefinition skill, Damageable target, int primedExtra = 0)
        {
            // Primed (work order 025): a mark more than it would add, on each target it hits; a skill that adds none adds one.
            int marks = (_castClean ? MarkRules.CleanTapMarks(skill, Kit, _castFullClean) : skill.MarksAdded) + Mathf.Max(0, primedExtra); // work order 064: a full clean chain's mark more
            if (marks <= 0) return;
            // A run's upgrade (work order 012): more marks from this skill's hit on a target that has none.
            if (Modifiers != null && target.MarkCount == 0) marks += Modifiers.FirstMarkBonus(skill.Name);
            if (skill.MarkInterval > 0f)
            {
                float now = Time.time;
                if (_markedAt.TryGetValue(target, out float at) && now - at < skill.MarkInterval - 1e-4f) return;
                _markedAt[target] = now;
            }
            AddMarks(target, marks, _castSlot);
        }

        /// <summary>
        /// One hit of a skill: every living target its area touches takes the damage. True if any did. Cover takes its
        /// share away first (<see cref="SkillArea"/>, Kristian, 2026-11-19): a line ends at the first cover, a cone and
        /// a circle hit only what they see, and a circle lands short of cover in its way. A target cover saves is not
        /// hit at all: no damage, stagger, marks or focus.
        /// </summary>
        bool ResolveSkillHit(SkillDefinition skill, float charge)
        {
            SkillShotsFired++;
            GroundPoint origin = GroundPosition;
            Vector3 flatAim = Flat(_aimPoint - transform.position);
            // With nothing to aim at (the cursor on the character), a line or cone goes where the character faces.
            if (flatAim.sqrMagnitude < 1e-6f && skill.Shape != SkillShape.Circle) flatAim = Flat(transform.forward);
            var aim = new GroundPoint(origin.X + flatAim.x, origin.Z + flatAim.z);
            var area = new SkillArea(skill, origin, aim, CoverQuery);

            // Long Shot's perfect band (work order 015): let go by the key inside it, the shot deals the perfect scale
            // times the full-charge damage and stagger, gains focus as a clean tap does, and is drawn that much wider.
            // Snap Shot (work order 017): a skill with no charging is always a perfect release.
            // Primed by Hair Trigger (work order 025): let go by the key at half a charge or more is perfect too.
            bool perfect = skill.Input == SkillInput.Charge
                           && (skill.NoCharge || (_cast.ReleasedByKey && (PoolRules.IsPerfect(skill, _cast.ChargedSeconds(Time.time))
                                                                          || (_castPrime == PrimeUse.PerfectRelease && FocusModeRules.PrimedPerfect(charge, FocusModeValues)))));
            Look look = LookFor(skill, perfect ? 1f : charge);
            if (perfect) look.Width *= Mathf.Max(1f, skill.PerfectScale);
            _shotWeight = LastShotWeight = FiringWeightRules.Of(skill, perfect, _castFullClean); // work order 064: its weight on the screen
            // A keg (work order 015) lands and lies on its fuse: nothing is hit yet.
            if (skill.Fuse > 0f && skill.Shape == SkillShape.Circle)
            {
                PlaceKeg(skill, area, look);
                Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = false, Charge = charge });
                return false;
            }
            Vector3 shotPoint = SpawnSkillFx(skill, look, area);
            PlaySound(look); // once per hit of a burst, per tick of a hold, per landing

            int damage = perfect ? PoolRules.PerfectDamage(skill) : SkillRules.Damage(skill, charge);
            // Work order 050 (decision 0012): in a run with the stats on, her flat damage by the skill's weight is added, and
            // a perfect release and a clean press are crits (sureCrit): what their own scale gave beyond a crit at her start
            // stays (CritRules.Folded), the crit itself is dealt in Deal. A clean spend (Backstep) has no scale of its own:
            // its crit is folded instead (CritRules.ApplyFolded; the fourth review of 2026-11-21, work order 055: it was
            // paid 1.5 on top). Off, exactly as before.
            bool sureCrit = StatsOn && (perfect || _castClean);
            bool foldedCrit = sureCrit && !perfect && _castClean && MarkRules.HasCleanSpend(skill);
            if (StatsOn)
                damage = perfect ? Mathf.RoundToInt((SkillRules.Damage(skill, 1f) + FlatFor(skill)) * Mathf.Max(0f, skill.PerfectScale) * CritRules.Folded(1f, Modifiers.StatValues) * CleanPressNow) // work order 065
                                 : damage + FlatFor(skill);
            float stagger = perfect ? skill.Stagger * Mathf.Max(0f, skill.PerfectScale) : skill.Stagger;
            // A clean tap (decision 0005) hits harder by itself; a clean spend (Backstep, amended 2026-11-19) instead
            // takes the marks, as a spender does, for its own bonus per mark.
            // Work order 064: the clean tap's scale from its one place (MarkRules.CleanTapScale: 1.75, twice that for the last tap
            // of a full clean chain), folded with the stats on as work order 050 and 055 have it. Work order 065: her Clean press
            // (1 off) times whatever the clean bonus is, here where it enters the damage; a clean spend (Backstep's) has no bonus
            // of its own here, so its hit takes the stat on its own.
            if (MarkRules.IsCleanTap(skill, _castClean))
            {
                float scale = MarkRules.CleanTapScale(Kit, _castFullClean);
                damage = Mathf.RoundToInt(damage * (StatsOn ? CritRules.Folded(scale, Modifiers.StatValues) * CleanPressNow : scale));
            }
            else if (StatsOn && _castClean && !perfect) damage = Mathf.RoundToInt(damage * CleanPressNow);
            if (perfect)
            {
                PerfectReleases++;
                GainFocus(Identity.FocusPerCleanTap);
            }
            bool landed = HitArea(skill, _castSlot, area, look, shotPoint, damage, stagger, _castClean, out Damageable first, _castPrime, sureCrit, foldedCrit);

            // Tag Round (work order 015): the first target it hit is tagged; a tag on a tagged target starts it again.
            if (skill.TagSeconds > 0f && first != null && first.IsAlive)
            {
                first.ApplyTag(skill.TagSeconds, skill.TagDamageShare);
                MarkPips.For(first, ShotFxMaterial); // its sign shows beside the marks
            }
            // A line goes through every lying keg on it, and sets each off (work order 015).
            if (skill.Shape == SkillShape.Line && _kegs.Count > 0)
                SetOffKegs(area.Origin, area.Aim, skill.Range, skill.HalfWidth, area.Stop);
            // Held: the seconds charged, so the timing feedback can tell a release before the band from one after it (work order 019).
            Report(new PlayerReport
            {
                Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = landed, Charge = charge, Clean = perfect,
                Held = _cast.ChargedSeconds(Time.time),
            });
            return landed;
        }

        /// <summary>
        /// Every living target the area touches takes the hit: damage, stagger, marks or the spending of them, a counter.
        /// True if any took damage. <paramref name="first"/> is the target nearest the area's origin that took damage.
        /// </summary>
        bool HitArea(SkillDefinition skill, int slot, SkillArea area, Look look, Vector3 shotPoint, int damage, float stagger,
                     bool clean, out Damageable first, PrimeUse prime = PrimeUse.None, bool sureCrit = false, bool foldedCrit = false)
        {
            first = null;
            float firstDistance = float.PositiveInfinity;
            bool spender = MarkRules.Spends(skill, clean);
            float bonusPerMark = MarkRules.BonusPerMark(skill, clean);
            bool landed = false, countered = false, killed = false, dazed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!d.IsAlive) continue;
                if (!area.Hits(d.GroundPosition, d.Radius)) continue;
                bool empowered = IsEmpowered;
                // Point Blank (work order 017): a spender with a close range spends, at its scaled bonus, only on a target
                // within it; one farther away takes the hit's own damage and keeps its marks.
                bool spends = spender && (skill.CloseRange <= 0f || d.GroundPosition.DistanceTo(area.Origin) - d.Radius <= skill.CloseRange);
                float bonus = skill.CloseRange > 0f ? bonusPerMark * skill.CloseMarkScale : bonusPerMark;
                // A spender takes every mark off the target and deals its bonus per mark on this hit (decision 0005);
                // Breaker Slug's bonus per mark is stagger, not damage (work order 015).
                bool marked = d.MarkCount > 0; // work order 065: before a spender takes them
                int taken = spends ? d.TakeMarks() : 0;
                int had = taken; // the marks it really had: only these give focus (the second reviews of 2026-11-20, work order 033)
                // Primed (work order 025): a spender counts a mark more than the target has, up to the cap.
                if (spends && prime == PrimeUse.SpenderExtraMark) taken = FocusModeRules.SpenderCount(taken, FocusModeValues.PrimedExtraMarks, Kit.MaxMarks);
                int dealt = spends ? MarkRules.SpenderDamage(damage, taken, bonus) : damage;
                // Execution (work order 017): more to a dazed target.
                if (skill.DazedDamageScale != 1f && d.IsDazed) dealt = Mathf.RoundToInt(dealt * Mathf.Max(0f, skill.DazedDamageScale));
                float hitStagger = skill.MarkStaggerBonus > 0f ? PoolRules.SpenderStagger(stagger, taken, skill.MarkStaggerBonus) : stagger;
                // Stopping Shot (work order 015): a hit on an enemy winding up a counterable attack, from its front. Tried
                // before the hit's stagger is dealt (the review of 2026-11-20, work order 026): a hit that empties a bar
                // dazes, the daze drops the windup, and the counter would then find nothing to counter.
                bool counteredThis = skill.CounterDaze > 0f && d.TryGetComponent(out Enemy enemy)
                                     && enemy.TryCounter(transform.position, skill.CounterHalfAngle, skill.CounterDaze);
                if (counteredThis) countered = true;
                bool wasDazed = d.IsDazed;
                int applied = Deal(d, dealt, out float focus, hitStagger, sureCrit, foldedCrit, marked); // work order 050: a clean press's or a perfect release's hit is a crit
                if (!d.IsAlive) killed = true;
                else if (!wasDazed && d.IsDazed) dazed = true;
                if (spends)
                {
                    // The primed extra mark counts for the damage (or Breaker Slug's stagger) only: a primed spender must
                    // not pay for its own part with focus (work order 033).
                    float markFocus = GainFocus(had * Identity.FocusPerMarkSpent);
                    Report(new PlayerReport
                    {
                        Kind = PlayerReportKind.MarksSpent, Slot = slot, Target = d, Marks = taken, Damage = applied, Focus = markFocus,
                    });
                }
                if (applied <= 0) continue;
                ReportHit(slot, d, applied, empowered, focus, counteredThis);
                SpawnHit(look, d, shotPoint);
                SkillShotHits++;
                landed = true;
                if (!spender) MarkFromSkill(skill, d, prime == PrimeUse.ExtraMark ? FocusModeValues.PrimedExtraMarks : 0);
                float distance = d.GroundPosition.DistanceTo(area.Origin);
                if (distance < firstDistance)
                {
                    firstDistance = distance;
                    first = d;
                }
            }
            if (countered)
            {
                Counters++;
                GainFocus(Identity.MaxFocus * Mathf.Clamp01(skill.CounterFocus));
            }
            if (landed || countered) UseLanded(skill, slot, killed, dazed, countered);
            return landed;
        }
    }
}
