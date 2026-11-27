using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 147's measure for him: A BOT'S NUMBERS, not a player's. Whole trial fights in floor 5's throne rooms (both templates,
    /// several seeds of the draws), the boss choosing and striking as in the game, time in fixed steps. Three stand-ins for her, each
    /// with his run's 140 health:
    /// - Stands: stands still where she came in and shoots (today's fault). It must die; it is said after how long and to what.
    /// - Circles: only runs round the room's middle at full speed, shooting. It must fail the wards.
    /// - Answers: does each attack's answer of section 7.2 with a reaction of 0.25 s, and stands and shoots between.
    /// Her damage is MODELLED, not shot (except at the seeds, which the answering bot really shoots): his 700 a second at full uptime
    /// (his fight of 2026-11-27), times the share of the boss's body her precision cone covers at her speed and distance (work order
    /// 143's numbers: accurate at or under 35 % of run speed). The numbers go to the log, marked "BOT".
    /// </summary>
    public class BossTrialBotTests : BossTrialHarness
    {
        const float Dps = 700f, React = 0.25f, Most = 300f, HerHealth = 140;

        enum Kind { Stands, Circles, Answers }

        sealed class Result
        {
            public Kind Bot;
            public string Template, Floor;
            public int Seed;
            public float Length, ShootShare, StoodShare, FloorShare, HealthLeft;
            public bool Died, Killed;
            public string KilledBy = "";
            public int WardsBroken, WardsFailed, SeedsShot, SeedsBurst, Crashes;
            public float[] Phases = new float[4];
            public Dictionary<string, int> Hits = new Dictionary<string, int>();
            public int HitsTaken => Hits.Values.Sum();

            public string Line()
            {
                string F(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);
                string P(float f) => (f * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
                return "BOT " + Bot + " " + Template + " " + Floor + " seed " + Seed + ": " + (Killed ? "killed it" : Died ? "died" : "timed out") + " at " + F(Length) + " s"
                       + (Died ? " to " + KilledBy : "") + ", its health left " + P(HealthLeft) + "; phases " + F(Phases[1]) + "/" + F(Phases[2]) + "/" + F(Phases[3])
                       + " s; hits " + HitsTaken + " (" + string.Join(", ", Hits.OrderByDescending(h => h.Value).Select(h => h.Key + " " + h.Value)) + ")"
                       + "; could shoot " + P(ShootShare) + ", stood " + P(StoodShare) + ", floor " + P(FloorShare)
                       + "; wards broken " + WardsBroken + " failed " + WardsFailed + "; seeds shot " + SeedsShot + " burst " + SeedsBurst + "; crashes " + Crashes;
            }
        }

        static readonly List<Result> s_All = new List<Result>();

        [UnityTest, Timeout(3000000)]
        public IEnumerator TheThreeBots_InBothThroneRooms_AgainstSevenPointSevensTargets()
        {
            s_All.Clear();
            var floors = new List<ulong>();
            Assert.That(LevelSeed.TryParse(HisFloor, out ulong his), Is.True);
            floors.Add(his);
            floors.AddRange(Floors);
            var templates = new HashSet<string>();
            for (int f = 0; f < floors.Count; f++)
                foreach (Kind bot in new[] { Kind.Stands, Kind.Circles, Kind.Answers })
                {
                    if (bot != Kind.Answers && f >= 2) continue; // the two faults on two floors; the answers on all four
                    for (int seed = 0; seed < (bot == Kind.Answers ? 2 : 1); seed++)
                    {
                        Result r = null;
                        yield return Fight(floors[f], 147 + 31 * f + seed, bot, x => r = x);
                        r.Floor = LevelSeed.ToText(floors[f]);
                        templates.Add(r.Template);
                        s_All.Add(r);
                        Debug.Log(r.Line());
                        TestContext.WriteLine(r.Line());
                    }
                }
            var sb = new StringBuilder("BOT summary (work order 147; a bot's numbers):\n");
            foreach (Kind bot in new[] { Kind.Stands, Kind.Circles, Kind.Answers })
            {
                List<Result> rs = s_All.Where(x => x.Bot == bot).ToList();
                sb.Append("  ").Append(bot).Append(": ").Append(rs.Count(x => x.Died)).Append(" of ").Append(rs.Count).Append(" died, mean length ")
                  .Append(rs.Average(x => x.Length).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, mean hits ")
                  .Append(rs.Average(x => x.HitsTaken).ToString("0.0", CultureInfo.InvariantCulture)).Append(", wards failed ")
                  .Append(rs.Sum(x => x.WardsFailed)).Append(", broken ").Append(rs.Sum(x => x.WardsBroken)).Append('\n');
            }
            Debug.Log(sb.ToString());
            TestContext.WriteLine(sb.ToString());
            Assert.That(templates, Is.SupersetOf(new[] { "throne-nave", "throne-arcade" }), "both throne rooms");
            foreach (Result r in s_All.Where(x => x.Bot == Kind.Stands))
                Assert.That(r.Died, Is.True, "today's fault, standing still and shooting, must die: " + r.Line());
            foreach (Result r in s_All.Where(x => x.Bot == Kind.Circles))
                Assert.That(r.WardsFailed > 0 || (r.Died && r.WardsBroken == 0), Is.True, "only running in circles must fail the wards: " + r.Line());
        }

        IEnumerator Fight(ulong floor, int seed, Kind bot, System.Action<Result> got)
        {
            BossTrialTesting.Seed = seed;
            GeneratedLevel level = null;
            EnemyBoss boss = null;
            yield return ThroneRoom(floor, (l, b) => { level = l; boss = b; }, hold: false);
            _health.MaxHealth = (int)HerHealth;
            _health.RestoreFull();
            var r = new Result { Bot = bot, Template = boss.TrialTemplate, Seed = seed };
            var brain = new Brain(this, boss, bot);
            float start = Time.time, shoot = 0f, owed = 0f;
            int deaths = _health.Deaths;
            Time.captureDeltaTime = Step;
            while (Time.time - start < Most)
            {
                brain.Tick();
                // Her damage, modelled: 700 a second times the share of its body her cone covers now.
                float share = brain.ShotShare();
                if (share > 0f && !boss.IsDead && !boss.InTransition)
                {
                    shoot += Step * (share >= 0.999f ? 1f : 0f);
                    owed += Dps * share * Step;
                    int whole = (int)owed;
                    if (whole > 0)
                    {
                        owed -= whole;
                        boss.Body.TakeHit(whole);
                    }
                }
                yield return null;
                if (boss.IsDead) { r.Killed = true; break; }
                if (_health.Deaths > deaths || !_health.IsAlive) { r.Died = true; break; }
            }
            r.Length = Time.time - start;
            r.KilledBy = boss.TrialKilledBy;
            foreach (KeyValuePair<string, int> h in boss.TrialHitsBy) r.Hits[h.Key] = h.Value;
            r.ShootShare = shoot / Mathf.Max(0.01f, r.Length);
            r.StoodShare = boss.TrialStoodShare;
            r.FloorShare = boss.TrialFloorShare;
            r.HealthLeft = boss.HealthShare;
            r.WardsBroken = boss.WardsBroken;
            r.WardsFailed = boss.WardsFailed;
            r.SeedsShot = boss.SeedsShot;
            r.SeedsBurst = boss.SeedsBurst;
            r.Crashes = boss.Crashes;
            float end = Time.time;
            for (int p = 1; p <= 3; p++)
            {
                float a = boss.TrialPhaseStarted(p);
                if (a <= 0f) continue;
                float b = end;
                for (int q = p + 1; q <= 3; q++) if (boss.TrialPhaseStarted(q) > 0f) { b = boss.TrialPhaseStarted(q); break; }
                r.Phases[p] = b - a;
            }
            Time.captureDeltaTime = 0f;
            Object.Destroy(level.gameObject);
            yield return null;
            yield return null;
            got(r);
        }

        /// <summary>A stand-in's mind: what it does each frame, from what it saw a reaction ago.</summary>
        sealed class Brain
        {
            readonly BossTrialBotTests _t;
            readonly EnemyBoss _boss;
            readonly Kind _kind;
            readonly Vector3 _start;
            BossTrialAttack _seen;
            int _spokeSet = -1;
            float _seenAt = -1f;
            Vector3 _way, _back, _axis;
            bool _dashed;
            NavMeshPath _path;
            readonly Vector3[] _corners = new Vector3[32];
            float _pathAt = -1f;
            Vector3 _goal;
            int _marksSeen;
            Vector3 _markWay;
            float _markAt = -10f;

            public Brain(BossTrialBotTests t, EnemyBoss boss, Kind kind)
            {
                _t = t;
                _boss = boss;
                _kind = kind;
                _start = Flat(t._player.Position);
                _path = new NavMeshPath();
            }

            PlayerCharacter Her => _t._player;
            Vector3 Me => Flat(Her.Position);
            Vector3 Boss => Flat(_boss.transform.position);
            float Now => Time.time;

            /// <summary>How much of its body her shot covers now: 1 standing (accurate), less at speed by the cone; 0 when she does not shoot.</summary>
            public float ShotShare()
            {
                if (_shooting == false) return 0f;
                float d = (Boss - Me).magnitude;
                if (d > Her.BasicShotRange) return 0f;
                float share = Her.Velocity.magnitude / 6f;
                float cone = PrecisionRules.Cone(share, new PrecisionValues());
                if (cone <= 0f) return 1f;
                float width = PrecisionRules.WidthAt(cone, d);
                return Mathf.Clamp01(2f * _boss.Tuning.BodyRadius / Mathf.Max(0.01f, width));
            }

            bool _shooting = true;

            public void Tick()
            {
                _shooting = true;
                switch (_kind)
                {
                    case Kind.Stands:
                        Her.Stop();
                        return;
                    case Kind.Circles:
                        Circle();
                        return;
                    default:
                        Answer();
                        return;
                }
            }

            void Circle()
            {
                Vector3 mid = Flat(_boss.TrialRoomMiddle);
                Vector3 off = Me - mid;
                if (off.magnitude < 0.5f) off = Vector3.right;
                float r = off.magnitude, want = 9.5f; // about where she kites it, outside its Cleave
                Vector3 tangent = Vector3.Cross(Vector3.up, off.normalized);
                Vector3 steer = tangent + off.normalized * Mathf.Clamp((want - r) * 0.5f, -1f, 1f);
                Her.Steer(steer);
            }

            void Answer()
            {
                BossTrialAttack a = _boss.TrialAttack;
                if (_boss.Judgement != EnemyBoss.JudgementStage.None) a = BossTrialAttack.Judgement;
                if (a != _seen)
                {
                    _seen = a;
                    _seenAt = Now;
                    _dashed = false;
                    _spokeSet = -1;
                    Vector3 toBoss = (Boss - Me).normalized;
                    _axis = toBoss;
                    _way = ClearSide(Vector3.Cross(Vector3.up, toBoss), 8f);
                    _back = ClearSide(-toBoss * 0.55f + Vector3.Cross(Vector3.up, toBoss) * 0.85f, 6f);
                }
                // What strikes soonest first: the attack being told (after her reaction), the ring, Ruin; then a Grave Mark on her; then
                // a seed near her; then stand and shoot.
                bool reacting = Now - _seenAt < React && a != BossTrialAttack.None;
                if (!reacting)
                    switch (a)
                    {
                        case BossTrialAttack.Cleave:
                            if (Now - _seenAt < React + 1f) { _shooting = false; Her.Steer(_back); return; }
                            break;
                        case BossTrialAttack.Charge:
                            if (Now - _seenAt < React + 0.5f && _boss.State == EnemyState.Windup) { _shooting = false; Her.Steer(_way); return; }
                            break;
                        case BossTrialAttack.Spokes:
                            Spokes();
                            return;
                        case BossTrialAttack.Slam:
                            Slam();
                            return;
                    }
                if (_boss.RingLive) { Ring(); return; }
                if (_boss.Judgement == EnemyBoss.JudgementStage.Ruin) { Judgement(); return; }
                // A Grave Mark on her (Grave Marks, Spokes' combo, Judgement's): run straight across it, away from the boss's line.
                // One way for the whole run of circles (turning back runs into the led ones); a quarter turn only where the floor ends.
                if (_boss.MarksLive > 0 && _boss.NextMark(out Vector3 mark, out float burst, out float radius))
                {
                    if (_marksSeen == 0) { _marksSeen = 1; _markAt = Now; _markWay = ClearSide(Vector3.Cross(Vector3.up, (Boss - Me).normalized), 10f); }
                    if (Now - _markAt >= React && ((Me - Flat(mark)).magnitude < radius + 1.2f || a == BossTrialAttack.GraveMarks))
                    {
                        if (NavMesh.Raycast(Her.Position, Her.Position + _markWay * 1.5f, out _, NavMesh.AllAreas))
                            _markWay = ClearSide(Vector3.Cross(Vector3.up, _markWay), 4f);
                        _shooting = false;
                        Her.Steer(_markWay);
                        return;
                    }
                }
                else _marksSeen = 0;
                if (ShootASeed()) return;
                Hold();
            }

            /// <summary>Between tells: stand and shoot, 7 to 11 m from it.</summary>
            void Hold()
            {
                float d = (Boss - Me).magnitude;
                if (d < 6.5f) { Her.Steer(ClearSide(-(Boss - Me).normalized, 4f)); return; }
                if (d > 12f) { GoTo(Boss); return; }
                Her.Stop();
            }

            void Spokes()
            {
                if (!_boss.SpokesLive) { Hold(); return; }
                int set = _boss.SpokesSet;
                if (set != _spokeSet) { _spokeSet = set; _seenAt = Now; }
                if (Now - _seenAt < React) { Her.Stop(); return; }
                Vector3 o = _boss.SpokeOrigin, me = Me - o;
                float r = Mathf.Max(4.5f, me.magnitude), best = float.PositiveInfinity;
                Vector3 target = Me;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 mid = Quaternion.Euler(0f, 30f, 0f) * _boss.SpokeDir(set, i);
                    Vector3 p = o + mid.normalized * r;
                    float dd = (p - Me).magnitude;
                    if (dd < best) { best = dd; target = p; }
                }
                Vector3 go = target - Me;
                if (go.magnitude > 0.3f) { _shooting = false; Her.Steer(go); }
                else Her.Stop();
            }

            void Slam()
            {
                _shooting = false;
                Vector3 landing = Flat(_boss.SlamLanding);
                Vector3 away = Me - landing;
                if (away.magnitude < 0.3f) away = _way;
                Her.Steer(ClearSide(away.normalized, 3f));
            }

            void Ring()
            {
                float gap = (Me - Flat(_boss.RingMiddle)).magnitude - _boss.RingRadiusNow - _boss.TrialValues.RingThickness * 0.5f;
                if (_boss.RingSheltered(Her.Position)) { Her.Stop(); return; }
                if (!_dashed && gap < 0.9f && gap > -0.3f && Her.DashCooldownRemaining <= 0f)
                {
                    _dashed = Her.DashToward(Flat(_boss.RingMiddle)) == IntentResult.Started;
                    return;
                }
                _shooting = false;
                Her.Steer(Me - Flat(_boss.RingMiddle));
            }

            void Judgement()
            {
                if (_boss.Judgement == EnemyBoss.JudgementStage.Ruin)
                {
                    _shooting = false;
                    if (_boss.InRuinShadow(Her.Position)) { Her.Stop(); return; }
                    if (Now - _pathAt > 0.5f) { _goal = NearestShadow(); _pathAt = Now; }
                    GoTo(_goal);
                    return;
                }
                Hold();
            }

            Vector3 NearestShadow()
            {
                Vector3 best = Me;
                float bestD = float.PositiveInfinity;
                for (float x = -16f; x <= 16f; x += 1f)
                    for (float z = -16f; z <= 16f; z += 1f)
                    {
                        Vector3 p = Me + new Vector3(x, 0f, z);
                        if (!_boss.InRuinShadow(p) || !OnFloor(p, 0.4f, out Vector3 at)) continue;
                        float d = (at - Me).magnitude;
                        if (d < bestD) { bestD = d; best = at; }
                    }
                return best;
            }

            bool ShootASeed()
            {
                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int i = 0; i < _boss.SeedSlots; i++)
                {
                    if (!_boss.Seed(i, out Vector3 at, out float burstAt, out _)) continue;
                    float d = (Flat(at) - Me).magnitude;
                    if (d < _boss.TrialValues.SeedCircle + 1.5f && d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) return false;
                _boss.Seed(best, out Vector3 seed, out float burst, out _);
                // Out of its circle if its burst is near; else stand and shoot it.
                if (burst - Now < 0.7f)
                {
                    _shooting = false;
                    Her.Steer(Me - Flat(seed));
                    return true;
                }
                Her.Stop();
                Her.AimAt(seed);
                Her.Shoot(seed);
                _shooting = false; // her shots go to the seed
                return true;
            }

            void GoTo(Vector3 goal)
            {
                if (!NavMesh.CalculatePath(Her.Position, goal, NavMesh.AllAreas, _path)) { Her.Steer(goal - Me); return; }
                int n = _path.GetCornersNonAlloc(_corners);
                Vector3 next = n > 1 ? _corners[1] : goal;
                Her.Steer(Flat(next) - Me);
            }

            /// <summary>The way, or its opposite, whose line has walkable ground for <paramref name="metres"/>.</summary>
            Vector3 ClearSide(Vector3 way, float metres)
            {
                way = Flat(way).normalized;
                if (!NavMesh.Raycast(Her.Position, Her.Position + way * metres, out _, NavMesh.AllAreas)) return way;
                if (!NavMesh.Raycast(Her.Position, Her.Position - way * metres, out _, NavMesh.AllAreas)) return -way;
                return (Boss - Me).sqrMagnitude > 0.01f ? -(Boss - Me).normalized : way;
            }
        }
    }
}
