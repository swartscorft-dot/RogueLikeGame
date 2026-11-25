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
    /// What <see cref="PlaySessionRecorder"/> writes of what happens: her reports, the enemies', what they loose, marks lost, the
    /// damage she takes and her death (work order 081 moved them here from PlaySessionRecorder.cs, code unchanged).
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        string SkillName(int slot)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            return skills != null && slot >= 0 && slot < skills.Count && skills[slot] != null
                ? skills[slot].Name
                : "slot " + slot.ToString(CultureInfo.InvariantCulture);
        }

        SkillInput? InputOf(int slot)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            return skills != null && slot >= 0 && slot < skills.Count && skills[slot] != null ? skills[slot].Input : (SkillInput?)null;
        }

        string TargetKind(Damageable target)
        {
            if (target == null) return "";
            if (_targetKinds.TryGetValue(target, out string kind)) return kind;
            kind = target.GetComponent<Enemy>() != null ? SessionWords.Enemy
                 : target.GetComponent<TrainingDummy>() != null ? SessionWords.Dummy
                 : target.name;
            _targetKinds[target] = kind;
            return kind;
        }

        static string Word(IntentResult result) =>
            result == IntentResult.Started ? SessionWords.Started
            : result == IntentResult.Buffered ? SessionWords.Buffered
            : SessionWords.Rejected;

        void OnPlayer(PlayerReport r)
        {
            if (!_recording) return;
            try
            {
                double t = Now();
                if (RecordSprint(r, t)) return; // work order 092 (PlaySessionRecorderSprint.cs)
                if (RecordNewSkills(r, t)) return; // work order 109 (PlaySessionRecorderNewSkills.cs)
                switch (r.Kind)
                {
                    case PlayerReportKind.SkillPressed:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillPress, SkillName(r.Slot)) { Slot = r.Slot, Result = Word(r.Result) };
                        if (r.Result == IntentResult.Rejected)
                        {
                            e.Reason = r.NotEquipped ? SessionWords.NotEquipped : r.CooldownLeft > 0f ? SessionWords.Cooldown : "no such skill";
                            if (r.CooldownLeft > 0f) e.CooldownLeft = r.CooldownLeft;
                        }
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.MarkAdded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.MarkAdd, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null, Target = TargetKind(r.Target), Marks = r.MarksAfter,
                        };
                        e.AddExtra("added", r.Marks.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.MarksSpent:
                        Add(new SessionEvent(t, SessionEventTypes.MarksSpent, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot) // work order 138: Tap Out's
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null, Target = TargetKind(r.Target), Marks = r.Marks, Damage = r.Damage, Focus = r.Focus,
                        });
                        break;
                    case PlayerReportKind.RecastPressed:
                    {
                        var press = new SessionEvent(t, SessionEventTypes.RecastPress, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Shot = r.Shot, Clean = r.Clean, Focus = r.Clean ? r.Focus : (float?)null,
                        };
                        // Work order 069: how it fell by its own window's clean part, and the window's clock at the press (s after the
                        // shot before went off; for a press that waited, when it fired), so that the clean parts can be set from his presses.
                        press.AddExtra("timing", r.Timing.ToString().ToLowerInvariant());
                        press.AddExtra("since", r.Seconds.ToString("0.000", CultureInfo.InvariantCulture));
                        Add(press);
                        break;
                    }
                    case PlayerReportKind.ChainEnded:
                        Add(new SessionEvent(t, SessionEventTypes.ChainEnd, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Shot = r.Shot, Result = r.Early ? SessionWords.Early : SessionWords.Complete,
                        });
                        break;
                    case PlayerReportKind.CooldownsCut:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.CooldownCut);
                        if (r.Cuts != null)
                            for (int slot = 0; slot < r.Cuts.Length; slot++)
                                if (r.Cuts[slot] > 0f) e.AddExtra("cut." + SkillName(slot), Text(r.Cuts[slot]));
                        if (r.DashCut > 0f) e.AddExtra("cut." + SessionWords.Dash, Text(r.DashCut));
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.SkillReleased:
                        Add(new SessionEvent(t, SessionEventTypes.SkillRelease, SkillName(r.Slot)) { Slot = r.Slot });
                        break;
                    case PlayerReportKind.SkillStarted:
                        Add(new SessionEvent(t, SessionEventTypes.SkillStart, SkillName(r.Slot)) { Slot = r.Slot });
                        break;
                    case PlayerReportKind.SkillFired:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillFired, SkillName(r.Slot)) { Slot = r.Slot, Hit = r.Landed };
                        if (InputOf(r.Slot) == SkillInput.Charge) e.Charge = r.Charge;
                        if (r.Clean) e.Clean = true; // work order 015: a perfect release, or a keg set off by a shot
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.SkillEnded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.SkillEnd, SkillName(r.Slot))
                        {
                            Slot = r.Slot, Hit = r.Fired,
                            Result = r.Ending == SkillEnding.RanOut ? SessionWords.RanOut
                                   : r.Ending == SkillEnding.CancelledByDash ? SessionWords.CancelledByDash
                                   : SessionWords.Cancelled,
                        };
                        SkillInput? input = InputOf(r.Slot);
                        if (input == SkillInput.Holding || input == SkillInput.Charge) e.Held = r.Held;
                        if (input == SkillInput.Charge) e.Charge = r.Charge;
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.BufferDropped:
                        Add(new SessionEvent(t, SessionEventTypes.BufferDropped, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.Dash)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                        });
                        break;
                    case PlayerReportKind.ShotClicked:
                        Add(new SessionEvent(t, SessionEventTypes.Click, SessionWords.BasicShot)
                        {
                            Result = r.Result == IntentResult.Started ? SessionWords.Started : SessionWords.Waiting,
                        });
                        break;
                    case PlayerReportKind.ShotDropped:
                        Add(new SessionEvent(t, SessionEventTypes.ClickDropped, SessionWords.BasicShot));
                        break;
                    case PlayerReportKind.ShotStarted:
                        Add(new SessionEvent(t, SessionEventTypes.ShotStart, SessionWords.BasicShot) { Moving = r.Moving });
                        break;
                    case PlayerReportKind.ShotFired:
                        Add(new SessionEvent(t, SessionEventTypes.ShotFired, SessionWords.BasicShot) { Hit = r.Landed });
                        break;
                    case PlayerReportKind.DashPressed:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.DashPress, SessionWords.Dash) { Result = Word(r.Result) };
                        if (r.Result == IntentResult.Rejected)
                        {
                            e.Reason = r.AlreadyDashing ? SessionWords.Dashing : SessionWords.Cooldown;
                            if (r.CooldownLeft > 0f) e.CooldownLeft = r.CooldownLeft;
                        }
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.DashStarted:
                        Add(new SessionEvent(t, SessionEventTypes.DashStart, SessionWords.Dash));
                        break;
                    case PlayerReportKind.Hit:
                    {
                        Enemy enemy = r.Target != null ? r.Target.GetComponent<Enemy>() : null;
                        var hit = new SessionEvent(t, SessionEventTypes.Hit, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                        {
                            Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                            Damage = r.Damage, Target = TargetKind(r.Target), Empowered = r.Empowered,
                            Focus = r.Focus > 0f ? r.Focus : (float?)null,
                            Enemy = enemy != null ? enemy.LogWord : null,
                            EnemyId = enemy != null ? enemy.Id : (int?)null,
                        };
                        if (r.Crit) hit.AddExtra("crit", "true"); // work order 050
                        Add(hit);
                        break;
                    }
                    case PlayerReportKind.FocusPressed:
                        Add(new SessionEvent(t, SessionEventTypes.FocusPress) { Result = Word(r.Result) });
                        if (r.Result == IntentResult.Started)
                        {
                            // What it was spent as (work order 025): today's full gauge, a part, or Steady Hand's window.
                            var spent = new SessionEvent(t, SessionEventTypes.FocusSpent);
                            spent.AddExtra("as", r.FocusMode == FocusMode.HairTrigger ? "part" : r.FocusMode == FocusMode.SteadyHand ? "window" : "today");
                            Add(spent);
                        }
                        break;
                    case PlayerReportKind.PrimeUsed:
                        Add(new SessionEvent(t, SessionEventTypes.PrimeUsed, SkillName(r.Slot))
                        {
                            Slot = r.Slot,
                            Result = r.Prime == PrimeUse.CleanPress ? "clean press" : r.Prime == PrimeUse.PerfectRelease ? "perfect release"
                                   : r.Prime == PrimeUse.SpenderExtraMark ? "a mark more counted" : "a mark more",
                        });
                        break;
                    case PlayerReportKind.PrimeLapsed:
                        Add(new SessionEvent(t, SessionEventTypes.PrimeLapsed));
                        break;
                    // Melee you read (work order 035): two things that happen to her.
                    case PlayerReportKind.Stunned:
                    case PlayerReportKind.Pulled:
                    {
                        var e = new SessionEvent(t, r.Kind == PlayerReportKind.Stunned ? SessionEventTypes.PlayerStunned : SessionEventTypes.PlayerPulled, r.By);
                        e.AddExtra("seconds", Text(r.Seconds));
                        Add(e);
                        break;
                    }
                    // Taking cover (2026-11-19, an experiment).
                    case PlayerReportKind.CoverEntered:
                        Add(new SessionEvent(t, SessionEventTypes.CoverEnter, r.Tall ? SessionWords.Tall : SessionWords.Low));
                        break;
                    case PlayerReportKind.CoverLeft:
                        Add(new SessionEvent(t, SessionEventTypes.CoverLeave)
                        {
                            Result = r.Leaving == CoverLeaving.Released ? SessionWords.Released
                                   : r.Leaving == CoverLeaving.Dash ? SessionWords.DashLeft
                                   : r.Leaving == CoverLeaving.MovingSkill ? SessionWords.MovingSkill
                                   : SessionWords.Other,
                        });
                        break;
                    // The quick duck (work order 043).
                    case PlayerReportKind.Ducked:
                        Add(new SessionEvent(t, SessionEventTypes.Duck, r.By));
                        break;
                    case PlayerReportKind.DuckEnded:
                    {
                        var e = new SessionEvent(t, SessionEventTypes.DuckEnd)
                        {
                            Result = r.DuckEnd == DuckEnd.Rose ? SessionWords.Rose : r.DuckEnd == DuckEnd.Moved ? SessionWords.Moved
                                   : r.DuckEnd == DuckEnd.Dashed ? SessionWords.DashLeft
                                   : r.DuckEnd == DuckEnd.Released ? SessionWords.Released : r.DuckEnd == DuckEnd.Acted ? SessionWords.Acted // work order 086
                                   : SessionWords.Other,
                        };
                        e.AddExtra("stopped", r.Shot.ToString(CultureInfo.InvariantCulture));
                        e.AddExtra("seconds", Text(r.Seconds)); // work order 086: how long she was down
                        Add(e);
                        break;
                    }
                    case PlayerReportKind.DuckNothing:
                        Add(new SessionEvent(t, SessionEventTypes.DuckNothing));
                        break;
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        void OnEnemy(EnemyReport r)
        {
            if (!_recording) return;
            try
            {
                double t = Now();
                // Every enemy event says which enemy and its kind (work order 008).
                SessionEvent Of(string type, string name = null) =>
                    new SessionEvent(t, type, name) { Enemy = (r.Elite ? "elite " : "") + EnemyKinds.Word(r.EnemyKind), EnemyId = r.Id };
                switch (r.Kind)
                {
                    case EnemyReportKind.Woke:
                        Add(Of(SessionEventTypes.EnemyWake));
                        break;
                    case EnemyReportKind.AttackStarted:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyAttack, r.Attack);
                        if (r.EnemyKind == EnemyKind.Bomber) e.AddExtra("fuse", SessionWords.Fuse(r.Count)); // work order 073: how its fuse was lit
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Struck:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyStrike, r.Attack);
                        e.Result = r.Outcome == StrikeOutcome.Landed ? SessionWords.Landed
                                 : r.Outcome == StrikeOutcome.OutOfArea ? SessionWords.OutOfArea
                                 : r.Outcome == StrikeOutcome.AvoidedByDash ? SessionWords.AvoidedByDash
                                 : r.Outcome == StrikeOutcome.StoppedByCover ? SessionWords.StoppedByCover
                                 : r.Outcome == StrikeOutcome.StoppedByWall ? SessionWords.StoppedByWall
                                 : SessionWords.NoEffect;
                        e.Damage = r.Damage > 0 ? r.Damage : (int?)null;
                        if (r.Flown) e.AddExtra("flown", "true"); // work order 036: the end of a thing that flew
                        if (r.EnemyKind == EnemyKind.Lancer && r.Count > 0) e.AddExtra(SessionSummary.LancerCrashExtra, LancerRules.StopperWord((LancerStopper)r.Count)); // work order 108: what its charge crashed on
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.GaveUp:
                        Add(Of(SessionEventTypes.EnemyGaveUp));
                        break;
                    case EnemyReportKind.Removed:
                    {
                        SessionEvent e = Of(SessionEventTypes.EnemyGaveUp);
                        e.Result = SessionWords.Removed;
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Died:
                        Add(Of(SessionEventTypes.EnemyDeath));
                        NoteFightDeath(r.Id); // work order 097: where it died, in a room's fight (PlaySessionRecorderRoomUse.cs)
                        break;
                    case EnemyReportKind.StaggerTaken:
                    {
                        SessionEvent e = Of(SessionEventTypes.StaggerTaken);
                        e.Stagger = r.Stagger;
                        e.AddExtra("bar", Text(r.StaggerLeft));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Dazed:
                        Add(Of(SessionEventTypes.EnemyDazed));
                        break;
                    case EnemyReportKind.DazeEnded:
                        Add(Of(SessionEventTypes.DazeEnded));
                        break;
                    case EnemyReportKind.Charged:
                        Add(Of(SessionEventTypes.BossCharge, r.Attack));
                        break;
                    case EnemyReportKind.CalledPack:
                    {
                        SessionEvent e = Of(SessionEventTypes.BossCall);
                        e.AddExtra("called", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Angered:
                        Add(Of(SessionEventTypes.BossAnger));
                        break;
                    // The caster (work order 027).
                    case EnemyReportKind.CirclePlaced:
                    case EnemyReportKind.CircleOffGround:
                    {
                        SessionEvent e = Of(SessionEventTypes.CasterCircle, r.Attack);
                        e.Result = r.Kind == EnemyReportKind.CirclePlaced ? SessionWords.Placed : SessionWords.OffTheGround;
                        e.AddExtra("circle", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.CircleFaded:
                    {
                        SessionEvent e = Of(SessionEventTypes.CasterCircleFaded, r.Attack);
                        e.AddExtra("circle", r.Count.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    case EnemyReportKind.Countered:
                        Add(Of(SessionEventTypes.EnemyCountered, r.Attack));
                        break;
                    // The bomber's blast on the others (work order 073); on her it is an enemy_strike as every enemy's.
                    case EnemyReportKind.Blasted:
                    {
                        SessionEvent e = Of(SessionEventTypes.BomberBlast, r.Attack);
                        e.Damage = r.Damage > 0 ? r.Damage : (int?)null;
                        e.AddExtra("enemies", r.Count.ToString(CultureInfo.InvariantCulture));
                        e.AddExtra("killed", r.Killed.ToString(CultureInfo.InvariantCulture));
                        Add(e);
                        break;
                    }
                    // The warder's wards (work order 079): one raised once it is on; one ended, why, and what it took off.
                    case EnemyReportKind.WardRaised:
                        Add(Of(SessionEventTypes.WardRaised, WarderValues.Ward));
                        break;
                    case EnemyReportKind.WardBroken:
                    {
                        SessionEvent e = Of(SessionEventTypes.WardBroken, WarderValues.Ward);
                        e.Damage = r.Damage;
                        e.AddExtra("why", SessionWords.WardWhy(r.Count));
                        Add(e);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Things that left an enemy at its release (work order 036): which attack, how many at once.</summary>
        void OnLoosed(Enemy enemy, EnemyAttackDefinition attack, int count)
        {
            if (!_recording || enemy == null) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.EnemyLoose, attack?.Name) { Enemy = enemy.LogWord, EnemyId = enemy.Id };
                e.AddExtra("count", count.ToString(CultureInfo.InvariantCulture));
                RangedOpening.Note(e, enemy); // work order 124: whether its shooter was on screen
                Add(e);
            }
            catch (Exception e) { Fail(e); }
        }

        /// <summary>Marks lost unused, on any target (decision 0005).</summary>
        void OnMarksLost(Damageable target, int marks, MarkLoss why)
        {
            if (!_recording) return;
            try
            {
                Add(new SessionEvent(Now(), SessionEventTypes.MarksLost)
                {
                    Target = TargetKind(target), Marks = marks,
                    Result = why == MarkLoss.FellOff ? SessionWords.FellOff : why == MarkLoss.Died ? SessionWords.Died : SessionWords.Reset,
                });
            }
            catch (Exception e) { Fail(e); }
        }

        void OnDamaged(int amount)
        {
            if (!_recording) return;
            try { Add(new SessionEvent(Now(), SessionEventTypes.PlayerDamaged) { Damage = amount }); }
            catch (Exception e) { Fail(e); }
        }

        void OnDied()
        {
            if (!_recording) return;
            try { Add(new SessionEvent(Now(), SessionEventTypes.PlayerDeath)); }
            catch (Exception e) { Fail(e); }
        }
    }
}
