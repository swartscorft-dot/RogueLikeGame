using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Her stats and the tiers (work order 050, decision 0012) in the ControlRoom scene, through the intents: the basic
    /// shot's interval at a run's start and after attack speed steps; flat damage on a basic shot and on skills by their
    /// weight; a seeded crit; a clean Triple Tap and a perfect Long Shot as crits; a chest's tiered offer and its pick; the
    /// pause menu's list of her stats; the recorder's pick and her final stats; and with the switch off, all as before.
    /// The practice dummy's tests put a run's modifiers on her by hand, the switch on and the start's crit chance at 0
    /// unless the test is of crits, so that a number is never a roll. Waits are real time with a limit, or game time read
    /// from the reports; none leans on a frame's length.
    /// </summary>
    public class StatsAndTiersTests
    {
        const ulong RunSeed = 0x5EED0050UL;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        GeneratedLevels _levels;
        RunHud _hud;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<float> _times = new List<float>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            _reports.Clear();
            _times.Clear();
            _player.Reported += r => { _reports.Add(r); _times.Add(Time.time); };
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null && (_levels == null || _player.Modifiers != _levels.Modifiers)) _player.Modifiers = null; // those put on by hand
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        int Slot(string name) => _player.SlotOf(name);

        static Upgrade Step(UpgradeKind kind, float amount) => new Upgrade { Kind = kind, Amount = amount, Tiered = true };

        /// <summary>A run's modifiers by hand, the switch on; the start's crit chance as given (0: nothing crits by a roll).</summary>
        RunModifiers ByHand(float critChance = 0f)
        {
            var m = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = critChance } };
            _player.Modifiers = m;
            return m;
        }

        List<PlayerReport> Hits(int slot) => _reports.Where(r => r.Kind == PlayerReportKind.Hit && r.Target == _a && r.Slot == slot).ToList();

        /// <summary>Two basic shots, the second clicked while the first runs: the game time from the first's start to the second's.</summary>
        IEnumerator TwoShots(Vector3 at, Action<float> gap)
        {
            yield return UntilFree();
            yield return new WaitForSeconds(0.5f);
            int before = _reports.Count;
            _player.Shoot(at);
            _player.Shoot(at); // waits for the next shot
            yield return Until(() => _reports.Skip(before).Count(r => r.Kind == PlayerReportKind.ShotStarted) >= 2, 3f);
            var starts = Enumerable.Range(before, _reports.Count - before).Where(i => _reports[i].Kind == PlayerReportKind.ShotStarted).Select(i => _times[i]).ToList();
            Assert.That(starts.Count, Is.GreaterThanOrEqualTo(2), "two shots started");
            gap(starts[1] - starts[0]);
        }

        // ---------------- Attack speed ----------------

        [UnityTest]
        public IEnumerator TheBasicShotsInterval_IsSlowerAtARunsStart_AndShorterAfterAttackSpeedSteps()
        {
            FeelValues feel = _player.Feel;
            float own = feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery;
            Assert.That(_player.BasicShotInterval, Is.EqualTo(own).Within(1e-6f), "the practice room: the feel's");
            _levels.StartRun(RunSeed);
            yield return null;
            Vector3 ahead = _player.Position + _player.transform.forward * 4f;
            Assert.That(_player.BasicShotInterval, Is.EqualTo(own / 0.85f).Within(1e-5f), "a run starts about 15 % slower");
            float gap = 0f;
            yield return TwoShots(ahead, g => gap = g);
            Assert.That(gap, Is.GreaterThanOrEqualTo(own / 0.85f - 1e-4f), "the second shot waited for her interval");
            Assert.That(gap, Is.LessThan(own / 0.85f + 0.05f));

            _levels.Modifiers.Take(Step(UpgradeKind.AttackSpeed, 0.18f));
            _levels.Modifiers.Take(Step(UpgradeKind.AttackSpeed, 0.12f));
            float faster = own / (0.85f * 1.3f);
            Assert.That(_player.BasicShotInterval, Is.EqualTo(faster).Within(1e-5f));
            yield return TwoShots(ahead, g => gap = g);
            Assert.That(gap, Is.GreaterThanOrEqualTo(faster - 1e-4f));
            Assert.That(gap, Is.LessThan(faster + 0.05f), "shorter after the steps");

            for (int i = 0; i < 10; i++) _levels.Modifiers.Take(Step(UpgradeKind.AttackSpeed, 0.18f));
            Assert.That(_player.BasicShotInterval, Is.EqualTo(own / 1.3f).Within(1e-5f), "never faster than the cap");
            Assert.That(_player.CooldownOf(Slot("Quick Shot")), Is.EqualTo(_player.Skills[Slot("Quick Shot")].Cooldown), "no skill's cooldown is touched");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_player.BasicShotInterval, Is.EqualTo(own).Within(1e-6f), "out of the run: the feel's again");
        }

        // ---------------- Flat damage ----------------

        [UnityTest]
        public IEnumerator FlatDamage_IsAddedToABasicShot_AndToASkill_ByItsWeight()
        {
            RunModifiers m = ByHand();
            m.Take(Step(UpgradeKind.FlatDamage, 6f));
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => Hits(-1).Count > 0, 2f);
            Assert.That(Hits(-1).Single().Damage, Is.EqualTo(Placeholders.BasicShotDamage + 6), "a basic shot: all of it");
            yield return UntilFree();

            int quick = Slot("Quick Shot"), scatter = Slot("Scatter Blast");
            Assert.That(_player.PressSkill(quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => Hits(quick).Count > 0, 2f);
            Assert.That(Hits(quick).Single().Damage, Is.EqualTo(_player.Skills[quick].Damage + 6), "Quick Shot, a quick single shot: weight 1");
            yield return UntilFree();
            _a.TakeMarks(); // Quick Shot's mark off: Scatter Blast's own damage
            Assert.That(_player.PressSkill(scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => Hits(scatter).Count > 0, 2f);
            Assert.That(Hits(scatter).Single().Damage, Is.EqualTo(_player.Skills[scatter].Damage + 12), "Scatter Blast, a heavy one: weight 2");
            Assert.That(Hits(-1).Concat(Hits(quick)).Concat(Hits(scatter)).Any(h => h.Crit), Is.False, "no crit with the chance at 0");
        }

        // ---------------- Crits ----------------

        [UnityTest]
        public IEnumerator ASeededCrit_FallsWhereTheStreamSays()
        {
            ByHand(critChance: 0.5f);
            const ulong seed = 50UL;
            _player.SeedCrits(seed);
            LevelRandom stream = LevelRandom.Stream(seed, PlayerCharacter.CritStage);
            var expected = Enumerable.Range(0, 6).Select(_ => CritRules.Rolls(stream, 0.5f)).ToList();
            Assume.That(expected.Contains(true) && expected.Contains(false), "the seed gives both");
            _player.Teleport(NearA);
            yield return null;
            int crits = _player.Crits;
            for (int i = 0; i < 6; i++)
            {
                int had = Hits(-1).Count;
                yield return UntilFree();
                _player.Shoot(_a.transform.position);
                yield return Until(() => Hits(-1).Count > had, 2f);
            }
            List<PlayerReport> hits = Hits(-1);
            Assert.That(hits.Count, Is.EqualTo(6));
            Assert.That(hits.Select(h => h.Crit), Is.EqualTo(expected), "crit where the seeded stream rolls one");
            Assert.That(hits.Select(h => h.Damage), Is.EqualTo(expected.Select(c => c ? 60 : 40)), "a crit: +50 %");
            Assert.That(_player.Crits - crits, Is.EqualTo(expected.Count(c => c)));
        }

        [UnityTest]
        public IEnumerator ACleanTripleTap_AndAPerfectLongShot_AreCrits_DealingWhatTheyDealtBefore()
        {
            RunModifiers m = ByHand();
            int tap = Slot("Triple Tap"), shot = Slot("Long Shot");
            _player.EquipSkill(shot);
            _player.Teleport(NearA);
            yield return null;

            // Triple Tap: two plain presses, the last one clean.
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "the clean part of the last window");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => Hits(tap).Count >= 3, 2f);
            yield return UntilFree();
            List<PlayerReport> taps = Hits(tap);
            int damage = _player.Skills[tap].Damage;
            Assert.That(taps.Select(h => h.Crit), Is.EqualTo(new[] { false, false, true }), "the clean press is a crit");
            Assert.That(taps.Select(h => h.Damage), Is.EqualTo(new[] { damage, damage, Mathf.RoundToInt(damage * _player.Kit.CleanDamage) }),
                        "105 as before: the clean tap's 1.5 is the crit's, not paid twice");

            // Long Shot let go in its band: a crit, 600 as before; with +10 % crit damage, 640.
            SkillDefinition s = _player.Skills[shot];
            int[] perfect = new int[2];
            for (int round = 0; round < 2; round++)
            {
                if (round == 1)
                {
                    m.Take(Step(UpgradeKind.CritDamage, 0.1f));
                    _player.StartFresh();
                }
                int had = Hits(shot).Count;
                Assert.That(_player.PressSkill(shot, _a.transform.position), Is.EqualTo(IntentResult.Started));
                yield return Until(() => _player.SkillChargedSeconds >= 0.5f * (s.PerfectFrom + s.PerfectTo), 3f);
                _player.ReleaseSkill(shot);
                yield return Until(() => Hits(shot).Count > had, 2f);
                yield return UntilFree();
                PlayerReport hit = Hits(shot).Last();
                Assert.That(hit.Crit, Is.True, "a perfect release is a crit");
                perfect[round] = hit.Damage;
            }
            Assert.That(perfect[0], Is.EqualTo(PoolRules.PerfectDamage(s)), "600, as before");
            Assert.That(perfect[1], Is.EqualTo(Mathf.RoundToInt(PoolRules.PerfectDamage(s) / 1.5f * 1.6f)), "a crit damage step raises it, once");
        }

        // ---------------- A chest, the pause menu, the recorder ----------------

        IEnumerator StandBy(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AChestsOffer_ShowsTiers_AndAPick_AppliesItsStep_AndTheRecorderWritesIt()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeStatsTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(RunSeed);
            yield return null;
            _health.TakeHit(30);
            yield return StandBy(_levels.Chests().First());
            Assert.That(_levels.OpenChest(), Is.True);
            IReadOnlyList<Upgrade> offer = _levels.OfferedUpgrades;
            Assert.That(offer.Count, Is.EqualTo(3), "work order 065: three");
            Assert.That(offer.All(u => u.Tiered), Is.True, "every card has a tier");
            Assert.That(offer.Select(u => u.Stat).Distinct().Count(), Is.EqualTo(3), "three different stats");
            Assert.That(offer.All(u => u.TierWord == "common" || u.TierWord == "rare" || u.TierWord == "epic"), Is.True);

            Upgrade u0 = offer[0];
            HerStats stats = _levels.Modifiers.Stats;
            float before = Measure(u0, stats);
            int maxBefore = _health.Max, healthBefore = _health.Current;
            Assert.That(_levels.PickUpgrade(0), Is.True);
            float after = Measure(u0, stats);
            if (u0.Kind == UpgradeKind.MaxHealth) Assert.That(_health.Max, Is.EqualTo(maxBefore + (int)u0.Amount), "her maximum by the step");
            else if (u0.Kind == UpgradeKind.Mend) Assert.That(_health.Current, Is.EqualTo(Mathf.Min(maxBefore, healthBefore + u0.Heal)));
            else Assert.That(after, Is.Not.EqualTo(before), u0.Short + ": the stat it names moved");

            SessionEvent chest = _recorder.Events.Last(e => e.Type == SessionEventTypes.ChestOpen);
            Assert.That(chest.Name, Is.EqualTo(u0.Short));
            Assert.That(chest.ExtraValue("stat"), Is.EqualTo(u0.Stat));
            Assert.That(chest.ExtraValue("tier"), Is.EqualTo(u0.TierWord));
            Assert.That(float.Parse(chest.ExtraValue("size"), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(u0.Amount).Within(1e-4f));
            StringAssert.StartsWith("dmg +", chest.ExtraValue("stats"), "her stats after the pick");
            _levels.BackToPracticeRoom();
            yield return null;
            SessionEvent end = _recorder.Events.Last(e => e.Type == SessionEventTypes.RunEnd);
            StringAssert.StartsWith("dmg +", end.ExtraValue("stats"), "her final stats when the run ended");
        }

        /// <summary>The number a step of this kind moves.</summary>
        float Measure(Upgrade u, HerStats s)
        {
            switch (u.Kind)
            {
                case UpgradeKind.FlatDamage: return s.Damage;
                case UpgradeKind.AttackSpeed: return s.AttackSpeed;
                case UpgradeKind.CritChance: return s.CritChance;
                case UpgradeKind.CritDamage: return s.CritDamage;
                case UpgradeKind.Luck: return s.Luck;
                case UpgradeKind.RunSpeed: return _player.RunSpeedShare;
                case UpgradeKind.Cooldown: return _player.CooldownOf(Slot(u.Skill));
                case UpgradeKind.DashCooldown: return _levels.Modifiers.DashCooldownShare;
                case UpgradeKind.LongerBarrel: return _player.BasicShotRange;
                case UpgradeKind.HeavyHits: return s.Stagger;
                case UpgradeKind.Damage: return s.DamageShare;
                case UpgradeKind.LastingMarks: return _levels.Modifiers.MarkLifetimeBonus;
                case UpgradeKind.KeenFocus: return _levels.Modifiers.FocusGainShare;
                case UpgradeKind.FirstMark: return _levels.Modifiers.FirstMarkBonus(u.Skill);
                // Work order 065's six.
                case UpgradeKind.AllCooldowns: return s.AllCooldowns;
                case UpgradeKind.CleanPress: return s.CleanPress;
                case UpgradeKind.MarkedPrey: return s.MarkedPrey;
                case UpgradeKind.Mending: return s.Mending;
                case UpgradeKind.LongFocus: return s.LongFocus;
                case UpgradeKind.SecondDash: return _player.DashMaxCharges;
                default: return 0f;
            }
        }

        [UnityTest]
        public IEnumerator ThePauseMenu_ListsHerStats_InARun()
        {
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(_hud.StatLines(), Is.Empty, "not in the practice room");
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Take(Step(UpgradeKind.FlatDamage, 3f));
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.Paused, Is.True);
            List<string> lines = _hud.StatLines();
            Assert.That(lines.Count, Is.EqualTo(16), "work order 065: its five lines after work order 050's eleven");
            Assert.That(lines[0], Is.EqualTo("Damage  +3 a shot, skills by weight  (+0)"));
            StringAssert.StartsWith("Attack speed  3.8 shots/s  (3.8)", lines[1]);
            Assert.That(lines.Single(l => l.StartsWith("Max health")), Is.EqualTo("Max health  " + _health.Max + "  (" + _health.MaxHealth + ")"));
            Assert.That(_hud.Escape(), Is.True);
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_HerStart_TheChest_AndTheNumbers_AreAsBefore()
        {
            _levels.RunValues.StatsAndTiers = false;
            _hud = RunHud.Ensure();
            FeelValues feel = _player.Feel;
            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_player.BasicShotInterval, Is.EqualTo(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery).Within(1e-6f), "the feel's");
            Assert.That(_levels.Modifiers.Stats.CritChance, Is.Zero);
            yield return StandBy(_levels.Chests().First());
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.OfferedUpgrades.Any(u => u.Tiered), Is.False, "no tier");
            List<string> equipped = Enumerable.Range(0, _player.Skills.Count).Where(_player.IsEquipped).Select(s => _player.Skills[s].Name).ToList();
            Assert.That(_levels.OfferedUpgrades.Select(u => u.Short),
                        Is.EqualTo(ChestUpgrades.Offer(RunSeed, 1, _levels.Chests().First().Room.Order, equipped, new RunValues { StatsAndTiers = false }, new RunModifiers(), true).Select(u => u.Short)),
                        "today's draw");
            _levels.PickUpgrade(0);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_hud.StatLines(), Is.Empty, "no list in the pause menu");
            Assert.That(_hud.Escape(), Is.True);
            _levels.BackToPracticeRoom();
            yield return null;

            // At the dummy with a run's modifiers off: no flat damage, no crit, a clean tap's 1.5 as before.
            var m = new RunModifiers();
            m.Take(Step(UpgradeKind.FlatDamage, 6f));
            _player.Modifiers = m;
            _player.Teleport(NearA);
            yield return null;
            for (int i = 0; i < 5; i++)
            {
                int had = Hits(-1).Count;
                yield return UntilFree();
                _player.Shoot(_a.transform.position);
                yield return Until(() => Hits(-1).Count > had, 2f);
            }
            Assert.That(Hits(-1).Select(h => h.Damage), Is.All.EqualTo(Placeholders.BasicShotDamage), "40, every one");
            Assert.That(Hits(-1).Any(h => h.Crit), Is.False);
            _player.Modifiers = null;
        }
    }
}
