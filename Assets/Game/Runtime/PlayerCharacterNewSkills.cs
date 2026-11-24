using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The three skills of work order 109 (2026-11-24; Kristian's yes by name to the producer's "De tre nye skillsene") in
    /// <see cref="PlayerCharacter"/>: Ricochet's shot and its jumps, Smoke Round's cloud, Deadeye's shots at the marked enemies
    /// and its refusal, and the switch that keeps the three out of her list (<see cref="NewSkillsOn"/>). The rules are
    /// <see cref="NewSkillRules"/>; each skill's numbers are its own fields (<see cref="SkillDefinition.RicochetJumps"/> and the others).
    /// - Ricochet (a Normal press, light): one shot along her aim, the first living target its line meets before cover, as the
    ///   basic shot; from it the shot jumps to the nearest other living target within its reach, and once more from there, never
    ///   the same twice, along a clear line past cover. Each hit deals its damage and adds its marks; each jump is a thin streak
    ///   from the one it left to the one it hit, and a ping.
    /// - Smoke Round (a Point press, light): the round lands where a circle skill lands (the cursor's point, pulled in to its
    ///   range, short of cover in its way) and a cloud stands there its seconds (<see cref="SmokeClouds"/>). No damage, no marks.
    ///   An enemy in it, or whose line to her crosses it, does not see her (the shooter, the caster and the warder begin no
    ///   attack; a melee enemy begins none unless she is within <see cref="SmokeValues.MeleeReach"/>).
    /// - Deadeye (a Casting press: its half second's cast with the cast bar, committed as a cast is): when the cast is done, one
    ///   shot at every marked target she has a clear line to within its range, at most its most, nearest first; each takes that
    ///   target's marks for its bonus a mark, as a spender's hit is counted. A press with no marked target in sight is refused,
    ///   as a press on cooldown is: nothing starts and no cooldown is spent.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// Work order 109: the three new skills in her list (<see cref="SettingsSkills"/>), set from <see cref="RunValues.NewSkills"/>
        /// by the runs (<see cref="GeneratedLevels"/>) when they find her and when a run starts. Off, the list is the twelve.
        /// </summary>
        [System.NonSerialized] public bool NewSkillsOn = true;
        bool _completedWithNew = true;
        SkillDefinition[] _fallbackSwitched;
        bool _fallbackSwitchedOn;

        /// <summary>The skills from memory (no settings asset), as the switch has them.</summary>
        SkillDefinition[] FallbackSkillsNow
        {
            get
            {
                if (_fallbackSwitched == null || _fallbackSwitchedOn != NewSkillsOn)
                {
                    _fallbackSwitched = NewSkillRules.Switched(_fallbackSkills, NewSkillsOn);
                    _fallbackSwitchedOn = NewSkillsOn;
                }
                return _fallbackSwitched;
            }
        }

        /// <summary>Ricochet's jumps so far (work order 109): one for each hit after the first.</summary>
        public int RicochetJumpsMade { get; private set; }
        /// <summary>Smoke Round's clouds so far (work order 109).</summary>
        public int SmokeCloudsMade { get; private set; }
        /// <summary>Deadeye's shots so far (work order 109), one per marked target shot at.</summary>
        public int DeadeyeShotsFired { get; private set; }
        /// <summary>Deadeye presses refused for want of a marked target in sight (work order 109).</summary>
        public int DeadeyeRefused { get; private set; }

        readonly List<TargetInfo> _newSkillTargets = new List<TargetInfo>();
        readonly List<int> _newSkillMarks = new List<int>();
        readonly List<int> _newSkillPicks = new List<int>();

        /// <summary>Thin as the jumps' streaks are drawn, against the skill's own streak (work order 109: "thin streaks from enemy to enemy").</summary>
        public const float RicochetStreakScale = 0.5f;

        /// <summary>
        /// One hit of a skill, if it is one of the three (work order 109): what it does, and true; any other skill false, and the
        /// ordinary hit goes on. <paramref name="landed"/> is true when it dealt damage.
        /// </summary>
        bool ResolveNewSkill(SkillDefinition skill, float charge, out bool landed)
        {
            landed = false;
            if (skill == null) return false;
            if (skill.RicochetJumps > 0)
            {
                landed = FireRicochet(skill);
                return true;
            }
            if (skill.SmokeSeconds > 0f && skill.Shape == SkillShape.Circle)
            {
                PlaceSmoke(skill);
                return true;
            }
            if (skill.DeadeyeMost > 0)
            {
                landed = FireDeadeye(skill);
                return true;
            }
            return false;
        }

        /// <summary>A skill of the three began (work order 109): Deadeye's aim rings its rising tone over its cast.</summary>
        void NewSkillStarted(SkillDefinition skill)
        {
            if (skill != null && skill.DeadeyeMost > 0) PlaceholderSounds.Current?.AskFor(SoundMoment.DeadeyeAimed);
        }

        /// <summary>The living targets now, and their marks, into the scratch lists: nothing allocated once they have grown.</summary>
        void GatherTargets()
        {
            IReadOnlyList<Damageable> all = Damageable.All;
            _newSkillTargets.Clear();
            _newSkillMarks.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                _newSkillTargets.Add(all[i].Info);
                _newSkillMarks.Add(all[i].IsAlive ? all[i].MarkCount : 0);
            }
        }

        // ---------------- Ricochet ----------------

        bool FireRicochet(SkillDefinition skill)
        {
            GroundPoint origin = GroundPosition;
            Vector3 flatAim = Flat(_aimPoint - transform.position);
            if (flatAim.sqrMagnitude < 1e-6f) flatAim = Flat(transform.forward);
            var aim = new GroundPoint(origin.X + flatAim.x, origin.Z + flatAim.z);
            Look look = LookFor(skill, charge: 1f);
            _shotWeight = LastShotWeight = FiringWeightRules.Of(skill, false, _castFullClean);

            GatherTargets();
            IReadOnlyList<Damageable> all = Damageable.All;
            CoverLine cover = CoverQuery;
            float stop = CoverRules.LineStop(origin, aim, skill.Range, cover);
            int first = LineShot.FirstHit(origin, aim, skill.Range, skill.HalfWidth, _newSkillTargets, stop);
            NewSkillRules.Chain(_newSkillTargets, first, skill.RicochetJumps, skill.RicochetReach, cover, _newSkillPicks);

            // The shot along her aim: to the first it hit, or out to where it ends, as the basic shot's streak.
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            Vector3 endPoint = first >= 0 ? all[first].transform.position : new Vector3(aim.X, 0f, aim.Z);
            bool stopped = first < 0 && stop < skill.Range;
            if (first < 0)
            {
                GroundPoint end = origin + LineShot.Direction(origin, aim, new GroundPoint(transform.forward.x, transform.forward.z), 1e-4f) * Mathf.Min(stop, skill.Range);
                endPoint = stopped ? NoteCoverStop(origin, aim, stop) : new Vector3(end.X, 0f, end.Z);
            }
            Vector3 shotEnd = new Vector3(endPoint.x, muzzle.y, endPoint.z);
            SpawnFlash(look, muzzle, shotEnd - muzzle);
            SpawnStreak(look, muzzle, shotEnd, 1f);
            if (stopped) SpawnCoverHit(look, shotEnd);
            PlaySound(look);
            // A lying keg on the shot's way, up to where it ended, goes off at once (as the basic shot and a line skill do).
            if (_kegs.Count > 0)
            {
                float reach = first >= 0 ? all[first].GroundPosition.DistanceTo(origin) : Mathf.Min(stop, skill.Range);
                SetOffKegs(origin, aim, skill.Range, skill.HalfWidth, reach);
            }

            int damage = SkillRules.Damage(skill, 1f) + FlatFor(skill); // work order 050: her flat damage by its weight
            bool landed = false, killed = false, dazed = false;
            Vector3 from = muzzle;
            for (int n = 0; n < _newSkillPicks.Count; n++)
            {
                Damageable d = all[_newSkillPicks[n]];
                if (n > 0)
                {
                    // A jump: a thin streak from the one it left to the one it hit, at the shot's height, and its ping.
                    Vector3 to = new Vector3(d.transform.position.x, muzzle.y, d.transform.position.z);
                    SpawnStreak(look, from, to, RicochetStreakScale);
                    RicochetJumpsMade++;
                    PlaceholderSounds.Current?.AskFor(SoundMoment.RicochetJumped);
                    Report(new PlayerReport { Kind = PlayerReportKind.RicochetJumped, Slot = _castSlot, Target = d, Shot = n + 1 });
                }
                bool wasDazed = d.IsDazed;
                bool empowered = IsEmpowered;
                int dealt = skill.DazedDamageScale != 1f && d.IsDazed ? Mathf.RoundToInt(damage * Mathf.Max(0f, skill.DazedDamageScale)) : damage;
                int applied = Deal(d, dealt, out float focus, skill.Stagger, marked: d.MarkCount > 0);
                if (!d.IsAlive) killed = true;
                else if (!wasDazed && d.IsDazed) dazed = true;
                Vector3 came = from; // the spark on the side the shot came from
                from = new Vector3(d.transform.position.x, muzzle.y, d.transform.position.z);
                if (applied <= 0) continue;
                ReportHit(_castSlot, d, applied, empowered, focus);
                SpawnHit(look, d, came);
                SkillShotHits++;
                landed = true;
                MarkFromSkill(skill, d, _castPrime == PrimeUse.ExtraMark ? FocusModeValues.PrimedExtraMarks : 0);
            }
            if (landed) UseLanded(skill, _castSlot, killed, dazed, false);
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = landed, Charge = 1f });
            return landed;
        }

        // ---------------- Smoke Round ----------------

        void PlaceSmoke(SkillDefinition skill)
        {
            GroundPoint origin = GroundPosition;
            var aim = new GroundPoint(_aimPoint.x, _aimPoint.z);
            GroundPoint centre = CoverRules.CircleCenter(skill, origin, aim, CoverQuery); // where a circle lands: short of cover in its way
            float now = Time.time;
            SmokeClouds.Place(new SmokeCloud(centre, skill.Radius, now, now + skill.SmokeSeconds));
            SmokeCloudsMade++;
            PlaceholderSounds.Current?.AskFor(SoundMoment.SmokeCameUp);
            _shotWeight = LastShotWeight = ShotWeight.Skill;
            Report(new PlayerReport { Kind = PlayerReportKind.SmokeLanded, Slot = _castSlot, At = centre, Seconds = skill.SmokeSeconds });
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = false, Charge = 1f, At = centre });
        }

        // ---------------- Deadeye ----------------

        /// <summary>Deadeye's targets now (work order 109), into the picks: marked, in sight past cover, within its range, nearest first.</summary>
        void PickDeadeyeTargets(SkillDefinition skill)
        {
            GatherTargets();
            NewSkillRules.DeadeyeTargets(_newSkillTargets, _newSkillMarks, GroundPosition, skill.Range, skill.DeadeyeMost, CoverQuery, _newSkillPicks);
        }

        /// <summary>
        /// Work order 109: a press of Deadeye with no marked target in sight is refused, as a press on cooldown is (nothing starts,
        /// no cooldown is spent). True when the slot's skill is refused now; <paramref name="report"/>: told as a press refused.
        /// </summary>
        bool RefusesWithNoTarget(int slot, bool report)
        {
            if (slot < 0 || slot >= Skills.Count) return false;
            SkillDefinition skill = Skills[slot];
            if (skill == null || skill.DeadeyeMost <= 0) return false;
            PickDeadeyeTargets(skill);
            if (_newSkillPicks.Count > 0) return false;
            DeadeyeRefused++;
            if (report) Report(new PlayerReport { Kind = PlayerReportKind.SkillPressed, Slot = slot, Result = IntentResult.Rejected, NoTarget = true });
            return true;
        }

        bool FireDeadeye(SkillDefinition skill)
        {
            PickDeadeyeTargets(skill);
            Look look = LookFor(skill, charge: 1f);
            _shotWeight = LastShotWeight = FiringWeightRules.Of(skill, false, _castFullClean);
            IReadOnlyList<Damageable> all = Damageable.All;
            int damage = SkillRules.Damage(skill, 1f) + FlatFor(skill);
            bool landed = false, killed = false, dazed = false;
            if (_newSkillPicks.Count > 0) PlaySound(look);
            for (int n = 0; n < _newSkillPicks.Count; n++)
            {
                Damageable d = all[_newSkillPicks[n]];
                // A shot from a pistol, the hands in turn, to the target's middle.
                Vector3 muzzle = NextMuzzle(out bool leftHand);
                Kick(leftHand, look);
                Vector3 to = new Vector3(d.transform.position.x, muzzle.y, d.transform.position.z);
                SpawnFlash(look, muzzle, to - muzzle);
                SpawnStreak(look, muzzle, to, 1f);
                DeadeyeShotsFired++;
                bool empowered = IsEmpowered;
                bool marked = d.MarkCount > 0;
                int taken = d.TakeMarks();
                int had = taken; // only the marks it really had give focus (work order 033's rule)
                if (_castPrime == PrimeUse.SpenderExtraMark) taken = FocusModeRules.SpenderCount(taken, FocusModeValues.PrimedExtraMarks, Kit.MaxMarks);
                int dealt = NewSkillRules.DeadeyeDamage(damage, taken, skill.MarkDamageBonus);
                if (skill.DazedDamageScale != 1f && d.IsDazed) dealt = Mathf.RoundToInt(dealt * Mathf.Max(0f, skill.DazedDamageScale));
                bool wasDazed = d.IsDazed;
                int applied = Deal(d, dealt, out float focus, skill.Stagger, marked: marked);
                if (!d.IsAlive) killed = true;
                else if (!wasDazed && d.IsDazed) dazed = true;
                float markFocus = GainFocus(had * Identity.FocusPerMarkSpent);
                Report(new PlayerReport { Kind = PlayerReportKind.MarksSpent, Slot = _castSlot, Target = d, Marks = taken, Damage = applied, Focus = markFocus });
                if (applied <= 0) continue;
                ReportHit(_castSlot, d, applied, empowered, focus);
                SpawnHit(look, d, muzzle);
                SkillShotHits++;
                landed = true;
            }
            if (landed) UseLanded(skill, _castSlot, killed, dazed, false);
            Report(new PlayerReport { Kind = PlayerReportKind.DeadeyeFired, Slot = _castSlot, Shot = _newSkillPicks.Count });
            Report(new PlayerReport { Kind = PlayerReportKind.SkillFired, Slot = _castSlot, Landed = landed, Charge = 1f });
            return landed;
        }
    }
}
