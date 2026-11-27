using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 149: a bot's numbers, marked as a bot's (nobody has played the Grave Ram). Three bots fight it in floor 1's elite room
    /// with her real basic shot (her real precision, her real cover), her health 100 as at a run's start, its hurt guard on, from her walk
    /// in: one stands still where it came in and shoots; one shoots and sidesteps each tell a quarter second after it begins (no
    /// anticipation: out of the Ram's strip sideways, out of the Stomp's circle, out of a stone's circle); one does the same and between
    /// attacks walks to stand with a pier behind her on the Ram's line, so its Rams end in stone. Her damage is raised to the research's
    /// 450 a second for floor 1 fought alone (R7 5.1) by <see cref="Boost"/>: her bare pistol's rate times it (a bot's assumption; her
    /// bare pistol is about 180 a second, a floor-1 build of his more). Each fight's length, her death, the hits she took by attack, the
    /// Rams and crashes, written to <c>captures\149\bots.csv</c> and its summary to <c>bots.txt</c>. The only assertion is that each fight
    /// ended (the Ram or she died, or 180 s): the numbers are for the report.
    /// </summary>
    public class GraveRamBotsTests
    {
        const float React = 0.25f;     // s from a tell's start to her answer (the order's sidestepping bot)
        const float Rate = 450f;       // her damage a second fought alone on floor 1, full uptime (R7 5.1: a guess)
        const float MostSeconds = 180f;
        const int Fights = 5;          // fights a bot, each in its own knights-apse floor (the producer's round 2: at least five)
        const ulong From = 0x149A0UL;
        const string Captures = @"C:\Users\kriss\.claude-automation\runner\captures\149";

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        LevelValues _values;
        bool _uniqueBefore;

        public enum Bot { StandsStill, Sidesteps, Baits }

        public sealed class Result
        {
            public Bot Bot;
            public string Seed;
            public float Seconds, DealtRate;
            public bool Won, Died;
            public int Health;
            public readonly Dictionary<string, int> Hits = new Dictionary<string, int>();
            public int Rams, Crashes, Missed, Met, Stomps, Throws, Staggered;
            public string Moves = "";
            /// <summary>The Ram's health in this fight, and the share of it left at the end.</summary>
            public int RamHealth;
            public float Gap, Daze;
            public float RamLeft;
        }

        static readonly List<Result> s_results = new List<Result>();

        /// <summary>Her damage times this: the research's rate over her bare pistol's (40 a shot, a shot each startup, active and recovery).</summary>
        float Boost
        {
            get
            {
                FeelValues f = _player.Feel;
                float bare = Placeholders.BasicShotDamage / Mathf.Max(0.05f, f.BasicShotStartup + f.BasicShotActive + f.BasicShotRecovery);
                return Mathf.Max(1f, Rate / bare);
            }
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _uniqueBefore = EliteValues.Unique;
            EliteValues.Unique = true;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            _levels = GeneratedLevels.Ensure();
            _values = new LevelValues();
            _levels.Values = _values;
            _levels.RunValues.SkillTiers = false;
            if (Camera.main != null) Camera.main.aspect = 16f / 9f;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            EliteValues.Unique = _uniqueBefore;
            EliteHurtGuard.Clear();
            if (_player != null) _player.Steer(Vector3.zero);
            if (_health != null)
            {
                _health.Untouchable = false;
                _health.MaxHealth = Placeholders.PlayerHealth;
            }
            if (Camera.main != null) Camera.main.ResetAspect();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static List<ulong> s_seeds;

        List<ulong> Seeds()
        {
            if (s_seeds != null) return s_seeds;
            s_seeds = new List<ulong>();
            ulong from = From;
            while (s_seeds.Count < Fights)
            {
                ulong seed = GraveRamTestRoom.ApseSeed(_values, from);
                s_seeds.Add(seed);
                from = seed + 1UL;
            }
            return s_seeds;
        }

        static IEnumerable<Bot> AllBots() => new[] { Bot.StandsStill, Bot.Sidesteps, Bot.Baits };

        [UnityTest, Timeout(3000000)]
        public IEnumerator ABot_FightsTheRam_OnFiveFloors([ValueSource(nameof(AllBots))] Bot bot)
        {
            foreach (ulong seed in Seeds())
            {
                Result res = null;
                yield return Fight(bot, seed, 0, x => res = x);
                Assert.That(res.Won || res.Died || res.Seconds >= MostSeconds - 0.1f, Is.True, "the fight ended");
                s_results.Add(res);
            }
            Write(s_results, "bots");
        }

        const string SweepVariable = "ROGUELIKE_SWEEP_149";
        static readonly List<Result> s_sweep = new List<Result>();

        /// <summary>
        /// The producer's round 2: each bot on the five floors at each of the Ram's healths named in ROGUELIKE_SWEEP_149 (for example
        /// "10000,12000,14000"), the bots' table by health and floor to captures\149\bots-sweep.txt. Ignored without it.
        /// </summary>
        [UnityTest, Timeout(6000000)]
        public IEnumerator ABot_AtEachHealth_OnFiveFloors([ValueSource(nameof(AllBots))] Bot bot)
        {
            string healths = Environment.GetEnvironmentVariable(SweepVariable);
            if (string.IsNullOrEmpty(healths)) Assert.Ignore("the sweep runs only when " + SweepVariable + " names the healths (work order 149)");
            foreach (string h in healths.Split(','))
            {
                string[] parts = h.Trim().Split('/'); // "health" or "health/gap"
                int health = int.Parse(parts[0], CultureInfo.InvariantCulture);
                float gap = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : -1f;
                float daze = parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : -1f; // "health/gap/crash daze"
                foreach (ulong seed in Seeds())
                {
                    Result res = null;
                    yield return Fight(bot, seed, health, x => res = x, gap, daze);
                    s_sweep.Add(res);
                }
            }
            Write(s_sweep, "bots-sweep");
        }

        IEnumerator Fight(Bot bot, ulong seed, int health, Action<Result> got, float gap = -1f, float daze = -1f)
        {
            GraveRamTestRoom r = null;
            yield return GraveRamTestRoom.Build(_levels, _practiceEnemy, _player, seed, x => r = x, wake: false);
            if (health > 0) r.Ram.Body.SetMaxHealth(health); // the sweep's health, before the fight wakes
            if (gap >= 0f) r.Ram.Tuning.Gap = gap; // the sweep's phase-1 gap
            if (daze >= 0f) r.Ram.Tuning.CrashDaze = daze; // the sweep's crash daze
            var res = new Result { Bot = bot, Seed = LevelSeed.ToText(seed), RamHealth = r.Ram.Body.MaxHealth, Gap = r.Ram.Tuning.Gap, Daze = r.Ram.Tuning.CrashDaze };
            _health.MaxHealth = Placeholders.PlayerHealth;
            _health.RestoreFull();
            _health.Untouchable = false;
            EliteHurtGuard.Clear();
            // Her damage raised to the research's rate (the bot's assumption): each hit of hers on the Ram deals Boost times, the stagger bar's
            // dazed share counted once.
            float boost = Boost;
            bool adding = false;
            int dealt = 0;
            r.Ram.Body.Damaged += amount =>
            {
                dealt += amount;
                if (adding || r.Ram.IsDead) return;
                adding = true;
                float dazed = r.Ram.Stagger != null && r.Ram.Stagger.IsDazed(Time.time) ? r.Ram.Stagger.Values.DazedDamageMultiplier : 1f;
                r.Ram.Body.TakeHit(Mathf.RoundToInt(amount * (boost - 1f) / dazed));
                adding = false;
            };
            r.Ram.Reported += e =>
            {
                if (e.Kind == EnemyReportKind.AttackStarted)
                {
                    if (e.Attack == GraveRamValues.Stomp) res.Stomps++;
                    else if (e.Attack == GraveRamValues.Stones) res.Throws++;
                }
                if (e.Kind == EnemyReportKind.Dazed && r.Ram.State != EnemyState.Idle && !r.Ram.CrashDazed) res.Staggered++;
                if (e.Kind == EnemyReportKind.Struck && e.Outcome == StrikeOutcome.Landed && e.Damage > 0)
                {
                    res.Hits.TryGetValue(e.Attack, out int n);
                    res.Hits[e.Attack] = n + 1;
                }
            };
            // She walks in as he does: to the room's point.
            Time.captureDeltaTime = GraveRamTestRoom.Step;
            _player.Teleport(r.World(1.0, 0.0));
            for (int i = 0; i < 30 && _levels.CurrentRoom != r.R; i++) yield return null;
            Vector3 stand = r.World(6.0, 0.0);
            yield return WalkTo(stand, 2f);
            for (int i = 0; i < 120 && !(r.Fight.On && r.Ram.IsAwake); i++) yield return null;
            Assert.That(r.Fight.On, Is.True, "the fight woke");
            float start = Time.time;
            var brain = new BotBrain(this, r, bot, stand);
            while (Time.time - start < MostSeconds && !r.Ram.IsDead && _health.IsAlive)
            {
                brain.Tick();
                yield return null;
            }
            _player.Steer(Vector3.zero);
            res.Seconds = Time.time - start;
            res.Won = r.Ram.IsDead;
            res.Died = !_health.IsAlive;
            res.Health = _health.Current;
            res.Rams = r.Ram.Rams;
            res.Crashes = r.Ram.Crashes;
            res.Missed = r.Ram.Missed;
            res.Met = r.Ram.MetHer;
            res.DealtRate = res.Seconds > 0f ? dealt / res.Seconds : 0f;
            res.RamLeft = r.Ram.HealthShare;
            res.Moves = string.Format(CultureInfo.InvariantCulture, "dazed by her shots' stagger {11}; held {0:0.0} s, to a place {1:0.0} s, to her {2:0.0} s; looks {3}, found {4}; turned down: ground {5}, not straight {6}, band {7}, sight {8}, screen {9}, short {10}",
                r.Ram.HoldSeconds, r.Ram.ToSpotSeconds, r.Ram.ToHerSeconds, r.Ram.SeekLooks, r.Ram.SeekFinds, r.Ram.SeekRejects[0], r.Ram.SeekRejects[1],
                r.Ram.SeekRejects[2], r.Ram.SeekRejects[3], r.Ram.SeekRejects[4], r.Ram.SeekRejects[5], res.Staggered);
            _health.Untouchable = true;
            Time.captureDeltaTime = 0f;
            got(res);
        }

        IEnumerator WalkTo(Vector3 to, float most)
        {
            yield return GraveRamTestRoom.Run(most, () => Vector3.Distance(GraveRamTestRoom.Flat(_player.Position), GraveRamTestRoom.Flat(to)) < 0.3f, () =>
            {
                Vector3 d = GraveRamTestRoom.Flat(to - _player.Position);
                _player.Steer(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.zero);
            });
            _player.Steer(Vector3.zero);
        }

        /// <summary>One bot's frame by frame: its answer to each tell after its reaction, its shots, its walk to a bait spot.</summary>
        sealed class BotBrain
        {
            readonly GraveRamBotsTests _t;
            readonly GraveRamTestRoom _r;
            readonly Bot _bot;
            readonly Vector3 _stand;
            EnemyAttackDefinition _seen;
            float _seenAt = -1f;
            int _stonesSeen;
            float _stoneAt = -1f;
            Vector3 _goal;
            bool _going;

            public BotBrain(GraveRamBotsTests t, GraveRamTestRoom r, Bot bot, Vector3 stand)
            {
                _t = t;
                _r = r;
                _bot = bot;
                _stand = stand;
            }

            PlayerCharacter Her => _t._player;
            EnemyGraveRam Ram => _r.Ram;

            public void Tick()
            {
                float now = Time.time;
                Vector3 her = GraveRamTestRoom.Flat(Her.Position), ram = GraveRamTestRoom.Flat(Ram.transform.position);
                // Every frame she shoots at its middle (her real shot: her cone, cover, the pistol's own rate), but while she walks out of a
                // tell (round 2: shooting slows her walk, and a player stops to step out).
                Vector3 aim = ram + Vector3.up * 1.2f;
                Her.AimAt(aim);
                if (!(_going && _dodging)) Her.Shoot(aim);
                if (_bot == Bot.StandsStill)
                {
                    Her.Steer(Vector3.zero);
                    return;
                }
                // A new tell: noted now, answered a quarter second on.
                EnemyAttackDefinition a = Ram.State == EnemyState.Windup ? Ram.Attack : null;
                if (a != null && a != _seen)
                {
                    _seen = a;
                    _seenAt = now;
                    _going = false;
                }
                if (a == null) _seen = null;
                int pending = Ram.Stones.Count(s => s.Pending);
                if (pending > _stonesSeen) _stoneAt = now;
                _stonesSeen = pending;
                if (_seen != null && now - _seenAt >= React)
                {
                    if (_seen.Name == GraveRamValues.Ram) Answer(StepOutOfStrip(her));
                    else if (_seen.Name == GraveRamValues.Stomp) Out(ram, her, _seen.Radius + Placeholders.PlayerRadius + 0.6f);
                }
                if (_stoneAt >= 0f && now - _stoneAt >= React)
                {
                    for (int i = 0; i < Ram.Stones.Length; i++)
                    {
                        EnemyGraveRam.Stone s = Ram.Stones[i];
                        if (!s.Pending) continue;
                        Vector3 c = GraveRamTestRoom.Flat(s.Center);
                        if (Vector3.Distance(c, her) < Ram.Tuning.StonesAttack.Radius + Placeholders.PlayerRadius + 0.2f)
                        {
                            Out(c, her, Ram.Tuning.StonesAttack.Radius + Placeholders.PlayerRadius + 0.8f);
                            break;
                        }
                    }
                }
                if (!_going && _bot == Bot.Baits && Ram.State != EnemyState.Windup && !Ram.IsRamming) Answer(BaitSpot(ram), quiet: true);
                if (_going)
                {
                    Vector3 d = _goal - her;
                    if (d.magnitude < 0.25f)
                    {
                        _going = false;
                        Her.Steer(Vector3.zero);
                    }
                    else Her.Steer(d.normalized);
                }
                else Her.Steer(Vector3.zero);
            }

            bool _dodging;

            void Answer(Vector3 to, bool quiet = false)
            {
                if (!GraveRamTestRoom.OnFloor(to, 1.2f, out Vector3 at)) return;
                if (quiet && _going) return;
                _goal = GraveRamTestRoom.Flat(at);
                _going = true;
                _dodging = !quiet;
            }

            /// <summary>Out of a circle round <paramref name="c"/> to <paramref name="reach"/> from it: straight away, or turned up to 90 degrees each way where a wall is in the way.</summary>
            void Out(Vector3 c, Vector3 her, float reach)
            {
                Vector3 away = Away(c, her);
                foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f })
                {
                    Vector3 to = c + Quaternion.Euler(0f, turn, 0f) * away * reach;
                    if (!GraveRamTestRoom.OnFloor(to, 0.4f, out Vector3 at) || NavMesh.Raycast(her, at, out NavMeshHit _, NavMesh.AllAreas)) continue;
                    Answer(at);
                    return;
                }
                Answer(c + away * reach);
            }

            static Vector3 Away(Vector3 from, Vector3 her)
            {
                Vector3 d = GraveRamTestRoom.Flat(her - from);
                return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.right;
            }

            /// <summary>Out of the Ram's strip sideways, to the side with more walkable ground.</summary>
            Vector3 StepOutOfStrip(Vector3 her)
            {
                Vector3 dir = GraveRamTestRoom.Flat(Ram.AimPoint - Ram.transform.position).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float need = Ram.Tuning.RamAttack.HalfWidth + Placeholders.PlayerRadius + 0.8f;
                Vector3 a = her + side * need, b = her - side * need;
                bool okA = GraveRamTestRoom.OnFloor(a, 0.3f, out _) && !NavMesh.Raycast(her, a, out NavMeshHit _, NavMesh.AllAreas);
                bool okB = GraveRamTestRoom.OnFloor(b, 0.3f, out _) && !NavMesh.Raycast(her, b, out NavMeshHit _, NavMesh.AllAreas);
                return okA || !okB ? a : b;
            }

            /// <summary>A spot 2.5 m before the face of the nearer pier that faces the Ram, the pier behind her on its line.</summary>
            Vector3 BaitSpot(Vector3 ram)
            {
                Vector3 best = _stand;
                float bestScore = float.MaxValue;
                foreach (double s in new[] { -8.0, 8.0 })
                {
                    Vector3 pier = GraveRamTestRoom.Flat(_r.World(10.0, s));
                    Vector3 toRam = GraveRamTestRoom.Flat(ram - pier);
                    // The pier's face toward the Ram: along its larger axis of the frame.
                    Vector3 inDir = GraveRamTestRoom.Flat(_r.World(1.0, 0.0) - _r.World(0.0, 0.0)).normalized;
                    Vector3 rightDir = Vector3.Cross(Vector3.up, inDir) * -1f;
                    float din = Vector3.Dot(toRam, inDir), dside = Vector3.Dot(toRam, rightDir);
                    Vector3 face = Mathf.Abs(din) > Mathf.Abs(dside) ? inDir * Mathf.Sign(din) : rightDir * Mathf.Sign(dside);
                    Vector3 spot = pier + face * (2f + 2.5f);
                    if (!GraveRamTestRoom.OnFloor(spot, 0.6f, out Vector3 at)) continue;
                    float score = Vector3.Distance(at, GraveRamTestRoom.Flat(Her.Position));
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = at;
                    }
                }
                return best;
            }
        }

        void Write(List<Result> results, string name)
        {
            if (!Directory.Exists(Captures)) return;
            var inv = CultureInfo.InvariantCulture;
            var csv = new List<string> { "bot,ram_health,seed,seconds,won,died,health_left,ram_left,dealt_per_s,hits_ram,hits_stomp,hits_stones,rams,crashes,missed,met,stomps,throws" };
            foreach (Result r in results)
                csv.Add(string.Join(",", r.Bot, r.RamHealth, r.Seed, r.Seconds.ToString("0.0", inv), r.Won ? 1 : 0, r.Died ? 1 : 0, r.Health, r.RamLeft.ToString("0.00", inv),
                                    r.DealtRate.ToString("0", inv), H(r, GraveRamValues.Ram), H(r, GraveRamValues.Stomp), H(r, GraveRamValues.Stones),
                                    r.Rams, r.Crashes, r.Missed, r.Met, r.Stomps, r.Throws));
            File.WriteAllLines(Path.Combine(Captures, name + ".csv"), csv);
            var text = new List<string> { "A bot's numbers (work order 149), not a player's. Her damage raised to " + Rate.ToString("0", inv) + " a second at full uptime (x" + Boost.ToString("0.00", inv) + " her bare pistol)." };
            foreach (var set in results.Select(x => (x.RamHealth, x.Gap, x.Daze)).Distinct())
                foreach (Bot b in AllBots())
                {
                    List<Result> rs = results.Where(x => x.Bot == b && x.RamHealth == set.RamHealth && Mathf.Approximately(x.Gap, set.Gap) && Mathf.Approximately(x.Daze, set.Daze)).ToList();
                    if (rs.Count == 0) continue;
                    text.Add("Ram " + set.RamHealth + ", gap " + set.Gap.ToString("0.0#", inv) + ", crash daze " + set.Daze.ToString("0.0#", inv) + ", " + b + ": " + rs.Count(x => x.Won) + " of " + rs.Count + " won; " + rs.Count(x => x.Died) + " died; mean fight "
                             + rs.Average(x => x.Seconds).ToString("0", inv) + " s; by floor: "
                             + string.Join(" | ", rs.Select(x => x.Seed + " " + x.Seconds.ToString("0", inv) + " s " + (x.Won ? "won" : x.Died ? "died" : "time")
                                                                 + ", her health " + x.Health + ", Ram left " + (x.RamLeft * 100f).ToString("0", inv) + " %"
                                                                 + ", hits " + H(x, GraveRamValues.Ram) + "/" + H(x, GraveRamValues.Stomp) + "/" + H(x, GraveRamValues.Stones)
                                                                 + ", crashes " + x.Crashes + " of " + x.Rams
                                                                 + ", " + (x.DealtRate / Rate * 100f).ToString("0", inv) + " % uptime")));
                }
            text.Add("");
            foreach (Result r in results) text.Add(r.Bot + " " + r.RamHealth + " " + r.Seed + ": " + r.Moves);
            File.WriteAllLines(Path.Combine(Captures, name + ".txt"), text);
            Debug.Log(string.Join("\n", text));
        }

        static int H(Result r, string attack) => r.Hits.TryGetValue(attack, out int n) ? n : 0;
    }
}
