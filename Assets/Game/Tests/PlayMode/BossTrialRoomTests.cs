using System.Collections;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.TestTools;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 147: the attacks that use the throne room's pillars, in floor 5's throne rooms (both templates): a Charge baited from
    /// in front of a pier crashes into it and leaves the boss dazed 2 s taking 1.5 times; the Shockwave stops at a pier, so standing
    /// in its shadow is the walked answer; Ruin hits her in the open and not in a pillar's shadow; Judgement kneels clear of every pier.
    /// </summary>
    public class BossTrialRoomTests : BossTrialHarness
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator TheCharge_BaitedInFrontOfAPier_CrashesIntoIt_DazedTwoSecondsTakingHalfAgainMore()
        {
            foreach (ulong floor in new[] { Floors[0], Floors[1] })
            {
                GeneratedLevel level = null;
                EnemyBoss boss = null;
                yield return ThroneRoom(floor, (l, b) => { level = l; boss = b; });
                Vector3 mid = Flat(boss.TrialRoomMiddle);
                Assert.That(Pier(boss, mid, out Vector3 face, out Vector3 dir), Is.True, boss.TrialTemplate + ": a pier seen from the middle");
                // She 2.5 m in front of the pier's face, the boss 11 m behind her on the same line.
                Assert.That(OnFloor(face - dir * 2.5f, 1f, out Vector3 her), Is.True);
                _player.Teleport(her);
                _health.Untouchable = true; // the charge passes her (counted, nothing taken) and runs on into the pier
                Place(boss, her - dir * 11f);
                yield return Run(0.3f, Stand);
                int crashesBefore = boss.Crashes;
                Assert.That(boss.TrialBegin(BossTrialAttack.Charge), Is.True);
                yield return Run(3f, Stand, () => boss.IsDazed);
                Assert.That(boss.Crashes, Is.EqualTo(crashesBefore + 1), boss.TrialTemplate + ": it crashed into the pier (ran " + boss.ChargeReach.ToString("0.0") + " m)");
                Assert.That(boss.IsDazed, Is.True);
                int h = boss.Body.CurrentHealth;
                boss.Body.TakeHit(100);
                Assert.That(h - boss.Body.CurrentHealth, Is.EqualTo(150), "taking 1.5 times");
                float dazedAt = Time.time;
                yield return Run(3f, Stand, () => !boss.IsDazed);
                Assert.That(Time.time - dazedAt, Is.EqualTo(boss.TrialValues.CrashDaze).Within(0.15f), "2 s");
                // Through open floor it runs its length or meets her, and is not dazed.
                _player.Teleport(mid);
                Place(boss, mid + Side(dir) * 9f);
                yield return Run(0.3f, Stand);
                crashesBefore = boss.Crashes;
                Assert.That(boss.TrialBegin(BossTrialAttack.Charge), Is.True);
                yield return Run(3f, Stand);
                _health.Untouchable = false;
                Object.Destroy(level.gameObject);
                yield return null;
                yield return null;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheShockwave_StopsAtAPier_ItsShadowIsTheWalkedAnswer()
        {
            GeneratedLevel level = null;
            EnemyBoss boss = null;
            yield return ThroneRoom(Floors[0], (l, b) => { level = l; boss = b; });
            boss.Body.TakeHit(boss.Body.CurrentHealth - (int)(boss.Body.MaxHealth * 0.6f));
            yield return Run(boss.TrialValues.TransitionSeconds + 0.2f, Stand);
            Vector3 mid = Flat(boss.TrialRoomMiddle);
            Assert.That(Pier(boss, mid, out Vector3 face, out Vector3 dir), Is.True);
            _player.Teleport(mid); // it leaps for where she stands
            Place(boss, mid - dir * 8f);
            yield return Run(0.3f, Stand);
            _reports.Clear();
            Assert.That(boss.TrialBegin(BossTrialAttack.Slam), Is.True);
            // In its leap she walks round the pier into its far side, behind it from the landing.
            Assert.That(OnFloor(face + dir * 6f, 2f, out Vector3 behind), Is.True, "floor behind the pier");
            _player.Teleport(behind);
            yield return Run(4f, Stand);
            Assert.That(Landed(BossTrialValues.Slam), Is.EqualTo(0));
            Assert.That(boss.RingSheltered(behind) || Landed(BossTrialValues.Shockwave) == 0, Is.True);
            Assert.That(Landed(BossTrialValues.Shockwave), Is.EqualTo(0), "the pier stopped the ring");
            Object.Destroy(level.gameObject);
            yield return null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Ruin_HitsHerInTheOpen_NotInAPillarsShadow_AndJudgementKneelsClearOfEveryPier()
        {
            foreach (ulong floor in new[] { Floors[0], Floors[1] })
            {
                GeneratedLevel level = null;
                EnemyBoss boss = null;
                yield return ThroneRoom(floor, (l, b) => { level = l; boss = b; });
                _health.Untouchable = true;
                for (int round = 0; round < 2; round++)
                {
                    _reports.Clear();
                    Assert.That(boss.TrialBegin(BossTrialAttack.Judgement), Is.True);
                    yield return Run(12f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.Ward);
                    Assert.That(boss.Judgement, Is.EqualTo(EnemyBoss.JudgementStage.Ward), boss.TrialTemplate + ": it kneels");
                    Vector3 k = Flat(boss.transform.position);
                    for (int a = 0; a < 16; a++)
                    {
                        var d = new Vector3(Mathf.Cos(a * Mathf.PI / 8f), 0f, Mathf.Sin(a * Mathf.PI / 8f));
                        Assert.That(Cover.ClearDistance(k, d, 3f, Cover.RayHeight), Is.GreaterThanOrEqualTo(boss.Tuning.BodyRadius), "its body clear of every pier and wall");
                    }
                    // Round 0 in the open, round 1 in the nearest shadow.
                    Vector3 spot = default;
                    bool found = false;
                    float best = float.PositiveInfinity;
                    for (float x = -14f; x <= 14f; x += 0.5f)
                        for (float z = -14f; z <= 14f; z += 0.5f)
                        {
                            Vector3 p = k + new Vector3(x, 0f, z);
                            if (!OnFloor(p, 0.3f, out Vector3 at)) continue;
                            if (boss.InRuinShadow(at) != (round == 1)) continue;
                            float dd = (at - k).magnitude;
                            if (dd < 4f) continue;
                            if (dd < best) { best = dd; spot = at; found = true; }
                        }
                    Assert.That(found, Is.True, "a place " + (round == 1 ? "in a shadow" : "in the open"));
                    _player.Teleport(spot);
                    _health.Untouchable = false;
                    yield return Run(boss.TrialValues.WardSeconds + boss.RuinTime + 0.6f, Stand, () => boss.Judgement == EnemyBoss.JudgementStage.None);
                    _health.Untouchable = true;
                    Assert.That(boss.WardsFailed, Is.EqualTo(round + 1));
                    Assert.That(Landed(BossTrialValues.Ruin), Is.EqualTo(round == 1 ? 0 : 1), boss.TrialTemplate + (round == 1 ? ": in a pillar's shadow, Ruin misses" : ": in the open, Ruin hits"));
                    Assert.That(boss.ShadowsDrawn, Is.GreaterThanOrEqualTo(2), "the pillars' shadows drawn");
                    yield return Run(3f, Stand);
                    _health.RestoreFull();
                }
                Object.Destroy(level.gameObject);
                yield return null;
                yield return null;
            }
        }
    }
}
