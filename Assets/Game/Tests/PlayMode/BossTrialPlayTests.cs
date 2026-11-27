using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 147: each attack of the trial fight alone, on a stand-in for her driven through her intents: standing still she is
    /// hit, and the answer section 7.2 names is not (a sidestep, a step into the gap and back, a straight run past the led circles, a
    /// walk out of the landing and a dash through the ring, shooting a seed, breaking the ward); the off-screen rule; the transition;
    /// the hurt guard; the switch off is work order 146's boss; nothing is allocated in a frame of its patterns. The answers react a
    /// quarter of a second after the tell begins.
    /// </summary>
    public class BossTrialPlayTests : BossTrialHarness
    {
        const float React = 0.25f;

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_TheBossIsWorkOrder146s_FieldForField()
        {
            BossTrialSettings.ForTests = false;
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Boss);
            var boss = (EnemyBoss)made[0];
            yield return null;
            Assert.That(boss.InTrial, Is.False);
            Assert.That(boss.TrialPhase, Is.EqualTo(0));
            Assert.That(boss.Values, Is.SameAs(boss.Tuning.Enemy), "its own values, not the trial's");
            Assert.That(boss.Body.MaxHealth, Is.EqualTo(Mathf.RoundToInt(18000 * (1f / 3f))), "a third of 18,000 in the practice room, as before");
            Assert.That(boss.TrialLines, Is.Null, "nothing of the trial made");
            BossValues.Trial = false;
            try
            {
                BossTrialSettings.ForTests = true;
                Assert.That(BossTrialSettings.InUse, Is.False, "the static switch off: no trial even when a test asks");
            }
            finally { BossValues.Trial = true; }
            BossTrialSettings.ForTests = null;
            Assert.That(BossTrialSettings.InUse, Is.False, "nobody at the keys and no test's word: as before");
        }

        [UnityTest]
        public IEnumerator TheTrial_FullHealthInThePracticeRoom_ItsOwnAttacks_NoAnger()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b);
            Assert.That(boss.Body.MaxHealth, Is.EqualTo(boss.TrialValues.Health), "the trial fight at full health, as F8 starts it");
            Assert.That(boss.TrialPhase, Is.EqualTo(1));
            Assert.That(boss.Values.Attacks.Length, Is.EqualTo(6));
            Assert.That(System.Array.Exists(boss.Values.Attacks, a => a.Name == BossValues.Spin), Is.False, "no Spin");
            boss.Body.TakeHit(boss.Body.CurrentHealth - 20000);
            yield return Run(0.1f);
            Assert.That(boss.IsAngry, Is.False, "no anger below half");
            Assert.That(boss.HasBroken, Is.False, "no finale break");
        }

        // ---------------- 1. Cleave and Wave ----------------

        [UnityTest]
        public IEnumerator Cleave_StandingStill_IsHit_AStepBackAndSidewaysOutOfTheConeAndItsWaveIsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 4f);
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.Cleave), Is.True);
            yield return Run(2.5f);
            Assert.That(Landed(BossTrialValues.Cleave), Is.EqualTo(1), "standing still in its cone");

            yield return Run(1.5f); // its recovery and her guard over
            yield return Again(boss, 4f);
            Vector3 toBoss = Flat(boss.transform.position - _player.Position).normalized;
            // From 4 m a straight sidestep does not leave its 110 degree cone in time; back and sideways (about 57 degrees off straight
            // back) leaves the cone's length and the Wave's 3 m half width both in 0.7 s.
            Vector3 way = Clear((-toBoss * 0.55f + Side(toBoss) * 0.85f).normalized, (-toBoss * 0.55f - Side(toBoss) * 0.85f).normalized, 6f);
            Assert.That(boss.TrialBegin(BossTrialAttack.Cleave), Is.True);
            float began = Time.time;
            Vector3 from = _player.Position, atStrike = default;
            yield return Run(3.2f, () =>
            {
                if (Time.time - began < React || Time.time - began > React + 1f) Stand();
                else Walk(way);
                if (atStrike == default && boss.State == EnemyState.Recover) atStrike = _player.Position;
            });
            Assert.That(Landed(BossTrialValues.Cleave), Is.EqualTo(0), "out of the cone: she went from " + from + " to " + atStrike + " at its strike, it at " + boss.transform.position);
            Assert.That(Landed(BossTrialValues.Wave), Is.EqualTo(0), "and out of the Wave's line");
            Assert.That(Struck(BossTrialValues.Wave, StrikeOutcome.OutOfArea), Is.EqualTo(1), "the Wave went, and missed");
        }

        [UnityTest]
        public IEnumerator TheWave_SteppingBackOutOfTheCone_IsHitByTheWave_ItsPathDrawnAsItLeaves()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 4f);
            Vector3 away = Flat(_player.Position - boss.transform.position).normalized;
            Assert.That(boss.TrialBegin(BossTrialAttack.Cleave), Is.True);
            float began = Time.time;
            int shownAtLeave = -1;
            yield return Run(2.5f, () =>
            {
                if (Time.time - began < React) Stand();
                else if (Flat(_player.Position - boss.transform.position).magnitude < boss.TrialValues.CleaveLength + 1.5f) Walk(away);
                else Stand();
                if (boss.State == EnemyState.Recover) shownAtLeave = Mathf.Max(shownAtLeave, boss.TrialLines.Shown); // its path shows 0.15 s after the strike
            });
            Assert.That(Landed(BossTrialValues.Cleave), Is.EqualTo(0), "back out of the cone");
            Assert.That(Landed(BossTrialValues.Wave), Is.EqualTo(1), "but on the Wave's line");
            Assert.That(shownAtLeave, Is.GreaterThanOrEqualTo(2), "its path and its front drawn as it leaves");
        }

        // ---------------- 2. Charge ----------------

        [UnityTest]
        public IEnumerator Charge_StandingStill_IsHit_ASidestepIsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 10f);
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.Charge), Is.True);
            yield return null;
            Assert.That(boss.TelegraphIsCounterable, Is.True, "blue: Stopping Shot counters it");
            yield return Run(3.5f);
            Assert.That(Landed(BossTrialValues.Charge), Is.EqualTo(1), "standing still on its line");

            yield return Run(1f);
            _reports.Clear();
            Place(boss, _player.Position + Flat(boss.transform.position - _player.Position).normalized * 10f);
            Vector3 toBoss = Flat(boss.transform.position - _player.Position).normalized;
            yield return Run(0.1f);
            Assert.That(boss.TrialBegin(BossTrialAttack.Charge), Is.True);
            float began = Time.time;
            yield return Run(3.5f, () =>
            {
                if (Time.time - began < React || Time.time - began > React + 0.6f) Stand();
                else Walk(Side(toBoss));
            });
            Assert.That(Landed(BossTrialValues.Charge), Is.EqualTo(0), "a sidestep of about 2 m out of its 3 m strip");
        }

        // ---------------- 3. Grave Marks ----------------

        [UnityTest]
        public IEnumerator GraveMarks_StandingStill_IsHit_RunningStraightPastThemIsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 9f);
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.GraveMarks), Is.True);
            yield return Run(3f);
            Assert.That(Landed(BossTrialValues.GraveMark), Is.GreaterThanOrEqualTo(2), "standing still: the first, and one more after her guard");

            yield return Run(1f);
            _reports.Clear();
            Vector3 way = Side(Flat(boss.transform.position - _player.Position));
            if (!NavMesh.Raycast(_player.Position, _player.Position + way * 16f, out _, NavMesh.AllAreas)) { }
            else way = -way;
            Assert.That(boss.TrialBegin(BossTrialAttack.GraveMarks), Is.True);
            float began = Time.time;
            yield return Run(3f, () =>
            {
                if (Time.time - began < React) Stand();
                else Walk(way);
            });
            Assert.That(Landed(BossTrialValues.GraveMark), Is.LessThanOrEqualTo(1), "running straight at her full speed she outruns the led circles (all but the first, under her as she stood)");
        }

        [UnityTest]
        public IEnumerator GraveMarks_LeadHerMotion_WithTheChoiceOn_AndSitWhereSheIs_WithItOff()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 9f);
            _health.Untouchable = true;
            Vector3 way = Side(Flat(boss.transform.position - _player.Position));
            foreach (bool lead in new[] { true, false })
            {
                BossTrialValues.GraveMarksLead = lead;
                Assert.That(boss.TrialBegin(BossTrialAttack.GraveMarks), Is.True);
                Vector3 her = default;
                // At the second circle's placing (she running at full speed by then): where she is, and where it is.
                yield return Run(1f, () => { Walk(way); her = Flat(_player.Position); }, () => boss.MarksLive >= 2);
                Assert.That(boss.MarksLive, Is.GreaterThanOrEqualTo(2));
                float ahead = Vector3.Dot(Flat(boss.NewestMark) - her, way.normalized);
                if (lead) Assert.That(ahead, Is.InRange(2.3f, 3.3f), "led 0.6 s of her 6 m/s, at most 3 m");
                else Assert.That(Flat(boss.NewestMark - her).magnitude, Is.LessThan(0.8f), "the choice off: where she is");
                yield return Run(2.5f, () => Walk(-way));
            }
        }

        // ---------------- 4. Spokes ----------------

        [UnityTest]
        public IEnumerator Spokes_StandingStill_IsHitOnce_AStepIntoAGapAndBackIsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 6f);
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.Spokes), Is.True);
            Assert.That(boss.SpokesLive, Is.True);
            yield return Run(3.2f);
            Assert.That(Landed(BossTrialValues.Spokes), Is.EqualTo(1), "standing still: one of the two sets");

            yield return Run(1f);
            yield return Again(boss, 6f);
            Assert.That(boss.TrialBegin(BossTrialAttack.Spokes), Is.True);
            float began = Time.time;
            int set = 0;
            float setAt = began;
            yield return Run(3.2f, () =>
            {
                if (!boss.SpokesLive) { Stand(); return; }
                if (boss.SpokesSet != set) { set = boss.SpokesSet; setAt = Time.time; }
                if (Time.time - setAt < React) { Stand(); return; }
                // To the nearest place, at her distance, midway between two strips of the set showing.
                Vector3 o = boss.SpokeOrigin, me = Flat(_player.Position) - o;
                float r = Mathf.Max(4.5f, me.magnitude), best = float.PositiveInfinity; // within about 4 m there is no gap: out to 4.5 m
                Vector3 target = Flat(_player.Position);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 mid = Quaternion.Euler(0f, 30f, 0f) * boss.SpokeDir(set, i);
                    Vector3 p = o + mid.normalized * r;
                    float d = (p - Flat(_player.Position)).magnitude;
                    if (d < best) { best = d; target = p; }
                }
                Vector3 go = target - Flat(_player.Position);
                if (go.magnitude > 0.3f) Walk(go); else Stand();
            });
            Assert.That(Landed(BossTrialValues.Spokes), Is.EqualTo(0), "into a gap of set 1, then back where set 1 was; she ended at "
                        + Flat(_player.Position - boss.SpokeOrigin).magnitude.ToString("0.0") + " m from it, " + _player.Position);
        }

        // ---------------- 5. Slam and Shockwave ----------------

        [UnityTest]
        public IEnumerator Slam_StandingStill_IsHitByTheLanding_WalkingOutAndDashingThroughTheRing_IsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 8f);
            boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f)); // phase 2
            yield return Run(boss.TrialValues.TransitionSeconds + 0.2f);
            _reports.Clear();
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.Slam), Is.True);
            yield return Run(3.5f);
            Assert.That(Landed(BossTrialValues.Slam), Is.EqualTo(1), "standing still where it leapt for");

            yield return Run(1f);
            yield return Again(boss, 8f);
            Vector3 landing = default;
            Assert.That(boss.TrialBegin(BossTrialAttack.Slam), Is.True);
            landing = Flat(boss.SlamLanding);
            float began = Time.time;
            bool dashed = false;
            yield return Run(4f, () =>
            {
                Vector3 me = Flat(_player.Position);
                if (Time.time - began < React) { Stand(); return; }
                if (!boss.RingLive && boss.IsSlamming) { Walk(Out(me, landing, boss)); return; } // out of the landing circle
                if (boss.RingLive && !dashed)
                {
                    float gap = (me - Flat(boss.RingMiddle)).magnitude - boss.RingRadiusNow - boss.TrialValues.RingThickness * 0.5f;
                    if (gap < 0.9f)
                    {
                        dashed = _player.DashToward(Flat(boss.RingMiddle)) == IntentResult.Started;
                        return;
                    }
                    Walk(Out(me, landing, boss));
                    return;
                }
                Stand();
            });
            Assert.That(Landed(BossTrialValues.Slam), Is.EqualTo(0), "out of the landing circle in its leap");
            Assert.That(dashed, Is.True, "she dashed");
            Assert.That(Landed(BossTrialValues.Shockwave), Is.EqualTo(0), "through the ring in the dash's protected part");
            Assert.That(Struck(BossTrialValues.Shockwave, StrikeOutcome.AvoidedByDash), Is.EqualTo(1), "told as avoided by the dash");
        }

        [UnityTest]
        public IEnumerator TheRing_WithoutADash_CannotBeOutrun()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 8f);
            boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f));
            yield return Run(boss.TrialValues.TransitionSeconds + 0.2f);
            _reports.Clear();
            Assert.That(boss.TrialBegin(BossTrialAttack.Slam), Is.True);
            Vector3 landing = Flat(boss.SlamLanding);
            float began = Time.time;
            yield return Run(4.5f, () =>
            {
                if (Time.time - began < React) Stand();
                else Walk(Out(Flat(_player.Position), landing, boss));
            });
            Assert.That(Landed(BossTrialValues.Slam), Is.EqualTo(0), "out of the landing");
            Assert.That(Landed(BossTrialValues.Shockwave), Is.EqualTo(1), "the ring at 7 m/s catches her at 6");
        }

        // ---------------- 6. Seeds ----------------

        [UnityTest]
        public IEnumerator Seeds_StandingBesideOne_IsHit_ShootingItThreeTimesIsNot()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 9f);
            boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f));
            yield return Run(boss.TrialValues.TransitionSeconds + 0.2f);
            _reports.Clear();
            Assert.That(boss.TrialBegin(BossTrialAttack.Seeds), Is.True);
            yield return Run(1.4f, null, () => boss.SeedsLive > 0);
            Assert.That(boss.SeedsLive, Is.GreaterThanOrEqualTo(2), "thrown round her");
            Assert.That(boss.Seed(First(boss), out Vector3 seed, out _, out _), Is.True);
            Assert.That(Flat(seed - _player.Position).magnitude, Is.InRange(boss.TrialValues.SeedNear - 0.6f, boss.TrialValues.SeedFar + 1.6f), "4 to 7 m from her");
            Assert.That(Enemy.All.Count, Is.EqualTo(1), "seeds are not enemies");
            for (int i = 0; i < boss.SeedSlots; i++)
                if (boss.Seed(i, out Vector3 s0, out _, out _))
                    Assert.That(BossTrialScreen.Seen(s0, boss.TrialValues.SeedCircle, boss.TrialValues.SeedHeight, boss.TrialValues.SeenMarginPx) && BossTrialScreen.Unhidden(s0, boss.TrialValues.SeedHeight),
                                Is.True, "each seed and its circle seen, clear of her plates, not behind anything");
            Assert.That(OnFloor(seed + Side(seed - boss.transform.position) * 1f, 1.5f, out Vector3 beside), Is.True);
            _player.Teleport(beside);
            yield return Run(boss.TrialValues.SeedFill + 0.5f, Stand);
            Assert.That(Landed(BossTrialValues.Seed), Is.EqualTo(1), "standing beside one: its burst");

            yield return Run(2f);
            _reports.Clear();
            Assert.That(boss.TrialBegin(BossTrialAttack.Seeds), Is.True);
            yield return Run(1.4f, null, () => boss.SeedsLive > 0);
            int i0 = First(boss);
            Assert.That(boss.Seed(i0, out seed, out _, out _), Is.True);
            OnFloor(seed + Flat(_player.Position - seed).normalized * 1.2f, 1.5f, out beside);
            _player.Teleport(beside);
            yield return Run(boss.TrialValues.SeedFill + 0.5f, () =>
            {
                Stand();
                if (boss.Seed(i0, out Vector3 s, out _, out _))
                {
                    _player.AimAt(s);
                    _player.Shoot(s);
                }
            });
            Assert.That(boss.SeedsShot, Is.GreaterThanOrEqualTo(1), "three shots break it");
            Assert.That(Landed(BossTrialValues.Seed), Is.EqualTo(0), "the one beside her shot, the others too far");
        }

        /// <summary>The boss put back at that distance from her (between two tries it walks to her), the reports cleared.</summary>
        IEnumerator Again(EnemyBoss boss, float distance)
        {
            Vector3 d = Flat(boss.transform.position - _player.Position);
            if (d.magnitude < 0.5f) d = Vector3.forward;
            Place(boss, _player.Position + d.normalized * distance);
            yield return Run(0.1f, Stand);
            _reports.Clear();
        }

        /// <summary>Away from the landing; standing on it, away from where the boss leapt from (along clear ground).</summary>
        Vector3 Out(Vector3 me, Vector3 landing, EnemyBoss boss)
        {
            Vector3 d = me - landing;
            if (d.magnitude > 0.5f) return d;
            Vector3 away = Flat(landing - boss.SlamFrom).normalized;
            if (away.sqrMagnitude < 0.5f) away = Vector3.forward;
            return Clear(away, Side(away), 8f);
        }

        /// <summary>The first way whose line has walkable ground for that far, else the second, else its opposite.</summary>
        Vector3 Clear(Vector3 a, Vector3 b, float metres)
        {
            Vector3 p = _player.Position;
            if (!NavMesh.Raycast(p, p + Flat(a).normalized * metres, out _, NavMesh.AllAreas)) return a;
            if (!NavMesh.Raycast(p, p + Flat(b).normalized * metres, out _, NavMesh.AllAreas)) return b;
            return -a;
        }

        static int First(EnemyBoss boss)
        {
            for (int i = 0; i < boss.SeedSlots; i++) if (boss.Seed(i, out _, out _, out _)) return i;
            return -1;
        }

        // ---------------- 7. Judgement ----------------

        [UnityTest]
        public IEnumerator Judgement_BreakingTheWard_DazesItFourSecondsTakingHalfAgainMore_NotBreakingItIsRuin()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 7f);
            Stand();
            Assert.That(boss.TrialBegin(BossTrialAttack.Judgement), Is.True);
            yield return Run(10f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.Ward);
            Assert.That(boss.Judgement, Is.EqualTo(EnemyBoss.JudgementStage.Ward), "it kneels behind its ward");
            int before = boss.Body.CurrentHealth;
            for (int i = 0; i < 40; i++) boss.Body.TakeHit(100);
            Assert.That(before - boss.Body.CurrentHealth, Is.EqualTo(40), "her hits wear the ward, not its health (1 a hit)");
            Assert.That(boss.WardTaken, Is.EqualTo(4000));
            yield return Run(0.1f);
            Assert.That(boss.WardsBroken, Is.EqualTo(1));
            Assert.That(boss.IsDazed, Is.True, "broken: dazed");
            int h = boss.Body.CurrentHealth;
            boss.Body.TakeHit(100);
            Assert.That(h - boss.Body.CurrentHealth, Is.EqualTo(150), "taking 1.5 times");
            yield return Run(boss.TrialValues.WardDaze + 0.3f);
            Assert.That(boss.IsDazed, Is.False);
            Assert.That(boss.Judgement, Is.EqualTo(EnemyBoss.JudgementStage.None));

            _reports.Clear();
            Assert.That(boss.TrialBegin(BossTrialAttack.Judgement), Is.True);
            yield return Run(10f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.Ward);
            Assert.That(boss.Judgement, Is.EqualTo(EnemyBoss.JudgementStage.Ward));
            yield return Run(boss.TrialValues.WardSeconds + boss.RuinTime + 1f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.None);
            Assert.That(boss.WardsFailed, Is.EqualTo(1));
            Assert.That(Landed(BossTrialValues.Ruin), Is.EqualTo(1), "in the open of the practice room: Ruin");
            Assert.That(Landed(BossTrialValues.GraveMark), Is.GreaterThanOrEqualTo(1), "and a Grave Mark through the ward, standing still");
        }

        // ---------------- The frame ----------------

        [UnityTest]
        public IEnumerator TheTransition_TheCrossingHitCounts_ThenTwoAndAHalfSecondsUntouchable_ThenSpokesAlone()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 7f, hold: false);
            int max = boss.Body.MaxHealth;
            int crossing = boss.Body.CurrentHealth - Mathf.FloorToInt(max * 0.65f) + 1;
            Assert.That(boss.Body.TakeHit(crossing), Is.EqualTo(crossing), "the crossing hit counts whole");
            yield return null;
            Assert.That(boss.InTransition, Is.True);
            Assert.That(boss.TrialPhase, Is.EqualTo(2));
            Assert.That(boss.Body.TakeHit(500), Is.EqualTo(0), "nothing after it counts");
            Assert.That(boss.State, Is.EqualTo(EnemyState.Chase), "it stands");
            yield return Run(boss.TrialValues.TransitionSeconds - 0.2f);
            Assert.That(boss.InTransition, Is.True);
            Assert.That(boss.TrialAttack, Is.EqualTo(BossTrialAttack.None), "nothing begun in it");
            yield return Run(0.4f);
            Assert.That(boss.InTransition, Is.False);
            Assert.That(boss.Body.TakeHit(10), Is.EqualTo(10));
            _player.Teleport(boss.transform.position + Flat(_player.Position - boss.transform.position).normalized * 7f);
            yield return Run(4f, Stand, () => boss.TrialAttack != BossTrialAttack.None);
            Assert.That(boss.TrialAttack, Is.EqualTo(BossTrialAttack.Spokes), "phase 2 opens with Spokes, alone");
        }

        [UnityTest]
        public IEnumerator NoAttackBegins_WhileItsBodyIsOffHisScreen_ItWalksUntilItIsInView_AndNotBeforeTwoSeconds()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 6f, hold: false);
            // She 15 m below it on his screen (his camera shows 11.3 m above her): it is off his screen and walks to her.
            Vector3 up = Flat(Camera.main.transform.forward).normalized;
            if (!OnFloor(boss.transform.position - up * 15f, 1f, out Vector3 below) || NavMesh.Raycast(below, boss.transform.position, out _, NavMesh.AllAreas))
                Assert.Inconclusive("no open floor 15 m below the boss in the practice room");
            _player.Teleport(below);
            yield return Run(0.3f, Stand);
            Assert.That(boss.TrialSeen, Is.False, "off his screen");
            float woke = Time.time;
            var began = new List<(float t, bool seen)>();
            boss.Reported += r =>
            {
                if (r.Kind == EnemyReportKind.AttackStarted) began.Add((Time.time, boss.TrialSeen));
            };
            yield return Run(12f, Stand);
            Assert.That(began.Count, Is.GreaterThanOrEqualTo(2), "it walks into view and fights");
            Assert.That(began[0].t - woke, Is.GreaterThanOrEqualTo(1f), "nothing while it walked into view");
            foreach ((float t, bool seen) b in began) Assert.That(b.seen, Is.True, "an attack began at " + b.t + " with its body off his screen");
            // Under the top-left plate (her health and focus) is not seen, though inside the camera's view (the second round).
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.07f, 0.9f, 0f));
            Assert.That(new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter), Is.True);
            Vector3 underPlate = ray.GetPoint(enter);
            Assert.That(BossTrialScreen.Seen(underPlate, boss.Tuning.BodyRadius, boss.Tuning.BodyHeight, boss.TrialValues.SeenMarginPx), Is.False, "under her plate");
            Ray mid = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.6f, 0f));
            new Plane(Vector3.up, Vector3.zero).Raycast(mid, out enter);
            Assert.That(BossTrialScreen.Seen(mid.GetPoint(enter), boss.Tuning.BodyRadius, boss.Tuning.BodyHeight, boss.TrialValues.SeenMarginPx), Is.True, "in the open middle of his screen");
        }

        // ---------------- The hurt guard ----------------

        [UnityTest]
        public IEnumerator TheHurtGuard_ASecondHitWithinPointEightSecondsDoesNothing_OnlyInTheTrialFight_AndHerBodyFlickers()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 12f);
            HurtGuard guard = _player.GetComponent<HurtGuard>();
            Assert.That(guard, Is.Not.Null, "on her as the trial fight woke");
            int hp = _health.Current;
            Assert.That(_health.TakeHit(10), Is.EqualTo(10));
            Assert.That(_health.TakeHit(10), Is.EqualTo(0), "held off by the guard");
            bool hidden = false;
            yield return Run(0.6f, () => hidden |= guard.FlickerHidden);
            Assert.That(hidden, Is.True, "her body flickers");
            Assert.That(_health.TakeHit(10), Is.EqualTo(0));
            yield return Run(0.3f);
            Assert.That(_health.TakeHit(10), Is.EqualTo(10), "after 0.8 s");
            Assert.That(guard.FlickerHidden, Is.False);
            Object.Destroy(boss.gameObject); // the fight taken away
            yield return Run(0.1f);
            yield return Run(1f);
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            Assert.That(_health.TakeHit(5), Is.EqualTo(5), "no guard outside the trial fight");
        }

        // ---------------- The recorder ----------------

        [UnityTest]
        public IEnumerator TheRecorder_ThePhaseOnEveryBossLine_HerSpeedAndDistanceAtTellAndStrike_TheFightsLineWithWhatKilledHer()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BossTrialPlayTests-" + System.Guid.NewGuid().ToString("N"));
            PlaySessionRecorder rec = PlaySessionRecorder.StartRecording(folder);
            try
            {
                EnemyBoss boss = null;
                yield return PracticeBoss(b => boss = b, distance: 4f);
                _health.MaxHealth = 20;
                _health.RestoreFull();
                Stand();
                Assert.That(boss.TrialBegin(BossTrialAttack.Cleave), Is.True);
                yield return Run(2.5f, Stand, () => !_health.IsAlive);
                Assert.That(_health.IsAlive, Is.False, "the Cleave's 25 killed her 20");
                yield return Run(0.5f);
                var lines = new List<SessionEvent>(rec.Events);
                SessionEvent tell = lines.Find(e => e.Type == SessionEventTypes.EnemyAttack && e.Name == BossTrialValues.Cleave);
                Assert.That(tell, Is.Not.Null);
                Assert.That(tell.ExtraValue(BossTrialLog.PhaseKey), Is.EqualTo("1"));
                Assert.That(tell.ExtraValue(BossTrialLog.HerDistance), Is.Not.Null.And.Not.Empty);
                Assert.That(tell.ExtraValue(BossTrialLog.HerSpeed), Is.EqualTo("0.00"), "she stood");
                SessionEvent strike = lines.Find(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == BossTrialValues.Cleave);
                Assert.That(strike.ExtraValue(BossTrialLog.HerDistance), Is.Not.Null.And.Not.Empty);
                foreach (SessionEvent e in lines)
                    if (e.Enemy == "boss") Assert.That(e.ExtraValue(BossTrialLog.PhaseKey), Is.Not.Null, e.Type + " without its phase");
                SessionEvent fight = lines.Find(e => e.Type == BossTrialLog.Fight);
                Assert.That(fight, Is.Not.Null, "the fight's line");
                Assert.That(fight.ExtraValue("ended"), Is.EqualTo(BossFightEvents.HerDeath));
                Assert.That(fight.ExtraValue("killed_by"), Is.EqualTo(BossTrialValues.Cleave));
                Assert.That(fight.ExtraValue("hits." + BossTrialValues.Cleave), Is.EqualTo("1"));
                Assert.That(lines.Exists(e => e.Type == BossFightEvents.Fight), Is.False, "not the finale's line");
                Assert.That(SessionSummary.Format(SessionSummary.Compute(lines)), Does.Contain("Boss (work order 147)"));
            }
            finally
            {
                rec.StopRecording();
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        // ---------------- No garbage ----------------

        [UnityTest]
        public IEnumerator AFrameOfItsPatterns_AllocatesNothing()
        {
            EnemyBoss boss = null;
            yield return PracticeBoss(b => boss = b, distance: 8f);
            boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f));
            yield return Run(boss.TrialValues.TransitionSeconds + 0.2f);
            _health.Untouchable = true;
            Assert.That(boss.TrialBegin(BossTrialAttack.Slam), Is.True);
            yield return Run(3f, Stand, () => boss.RingLive);
            Assert.That(boss.RingLive, Is.True, "the ring growing");
            float now = Time.time;
            for (int i = 0; i < 5; i++) boss.TrialFrame(now);
            Assert.That(() => { for (int i = 0; i < 200; i++) boss.TrialFrame(now); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of the ring allocates nothing");
            yield return Run(3f);
            Assert.That(boss.TrialBegin(BossTrialAttack.Spokes), Is.True);
            yield return Run(0.2f);
            now = Time.time;
            for (int i = 0; i < 5; i++) boss.TrialFrame(now);
            Assert.That(() => { for (int i = 0; i < 200; i++) boss.TrialFrame(now); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of the spokes allocates nothing");
            int made = boss.TrialLines.Made;
            yield return Run(6f);
            Assert.That(boss.TrialLines.Made, Is.EqualTo(made), "no line made once the pool is warm");
        }
    }
}
