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
    /// Work order 023 (2026-11-20), the first balance pass, in the ControlRoom scene. The turns to attack: in a crowd of
    /// six shooters and four brutes spawned here and made to take turns as a floor's enemies do, never more than the caps
    /// wind up at once and each attacks within <see cref="FirstAttackWithin"/> of the fight's start and again within
    /// <see cref="NextAttackWithin"/> of its last; with the rule off they wind up all at once as before; a held-back
    /// brute walks on and a held-back shooter keeps its distance; the practice room's enemy is untouched by the rule. The
    /// floor heal: she is healed when a floor's reward is taken, and not when a chest's upgrade is. Work order 030, the
    /// second pass: the levers it made, each with its tests below.
    ///
    /// She has a lot of health for the crowd's tests, so the fight lasts; she stands still. The practice brute is
    /// switched off but for its own test. The crowd's tests run at time scale 4; every wait is in game time.
    /// </summary>
    public class BalanceTests
    {
        const float FirstAttackWithin = 8f;   // s of game time from the fight's start
        const float NextAttackWithin = 10f;   // s of game time from its last wind-up's start
        const float CrowdSeconds = 24f;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practice;
        GeneratedLevels _levels;
        int _maxHealthBefore;
        TurnValues _turnsBefore;
        string _folder;
        PlaySessionRecorder _recorder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.MeleeYouRead = false; // work order 035: these tests are of the swarmer's, brute's and guard's attacks from before
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _maxHealthBefore = _health.MaxHealth;
            _practice = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practice, Is.Not.Null, "the practice enemy, to copy");
            _practice.enabled = false;
            _turnsBefore = Enemy.Turns.Values;
            // The first pass's rule, a limit for each sort (work order 030 shares one, lever D; its test turns it on).
            Enemy.Turns.Values = new TurnValues { Shared = false };
            Enemy.Turns.Clear();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.StatsAndTiers = false; // work order 050: the chest's heal test leans on today's offer (no Mend in it)
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_health != null)
            {
                _health.MaxHealth = _maxHealthBefore;
                _health.RestoreFull();
            }
            Enemy.Turns.Values = _turnsBefore ?? new TurnValues();
            Enemy.Turns.Clear();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static IEnumerator Frames(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        /// <summary>Six shooters and four brutes, spawned in the practice room, taking turns as a floor's do, all woken.</summary>
        List<Enemy> SpawnCrowd(bool takeTurns = true)
        {
            var crowd = new List<Enemy>();
            for (int i = 0; i < 6; i++) crowd.AddRange(_levels.SpawnPractice(EnemyKind.Shooter));
            for (int i = 0; i < 4; i++) crowd.AddRange(_levels.SpawnPractice(EnemyKind.Brute));
            Assert.That(crowd.Count(e => e is EnemyShooter), Is.EqualTo(6));
            Assert.That(crowd.Count(e => e is EnemyBrute), Is.EqualTo(4));
            foreach (Enemy e in crowd)
            {
                e.TakesTurns = takeTurns;
                e.HealthOverride = 1000000;
                e.enabled = true; // a copy of the switched-off practice brute is switched off too
                e.Provoke();
            }
            return crowd;
        }

        void LotsOfHealth()
        {
            _health.MaxHealth = 1000000;
            _health.RestoreFull();
        }

        sealed class Watch
        {
            public int MostHeavy, MostRanged;
            public readonly Dictionary<int, List<float>> Begins = new Dictionary<int, List<float>>();
            public readonly Dictionary<int, EnemyState> Last = new Dictionary<int, EnemyState>();
            // Held back: seconds a brute waited farther than 3 m from her, and the metres it walked meanwhile.
            public float BruteHeldFar, BruteWalkedHeld;
            // Held back: frames a shooter waited, and those it stood outside the distance it keeps.
            public int ShooterHeldFrames, ShooterHeldOutside;
            public float ShooterHeldNearest = float.MaxValue, ShooterHeldFarthest;
        }

        /// <summary>Watches the crowd for that much game time, a frame at a time.</summary>
        IEnumerator WatchCrowd(List<Enemy> crowd, Watch w, float seconds)
        {
            var values = new ShooterValues();
            float start = Time.time;
            var was = crowd.ToDictionary(e => e.Id, e => e.transform.position);
            foreach (Enemy e in crowd) w.Begins[e.Id] = new List<float>();
            while (Time.time - start < seconds)
            {
                yield return null;
                float now = Time.time;
                int heavy = 0, ranged = 0;
                foreach (Enemy e in crowd)
                {
                    EnemyState s = e.State;
                    if (s == EnemyState.Windup)
                    {
                        if (e is EnemyBrute) heavy++;
                        else ranged++;
                        if (!w.Last.TryGetValue(e.Id, out EnemyState before) || before != EnemyState.Windup) w.Begins[e.Id].Add(now - start);
                    }
                    w.Last[e.Id] = s;
                    Vector3 p = e.transform.position;
                    bool held = s == EnemyState.Chase && Enemy.Turns.IsWaiting(e.Id, now);
                    float d = Flat(p, _player.Position);
                    if (held && e is EnemyBrute && d > 3f)
                    {
                        w.BruteHeldFar += Time.deltaTime;
                        w.BruteWalkedHeld += Flat(p, was[e.Id]);
                    }
                    if (held && e is EnemyShooter)
                    {
                        w.ShooterHeldFrames++;
                        w.ShooterHeldNearest = Mathf.Min(w.ShooterHeldNearest, d);
                        w.ShooterHeldFarthest = Mathf.Max(w.ShooterHeldFarthest, d);
                        if (d < values.BackOffUnder - 0.5f || d > values.KeepFar + 1.5f) w.ShooterHeldOutside++;
                    }
                    was[e.Id] = p;
                }
                w.MostHeavy = Math.Max(w.MostHeavy, heavy);
                w.MostRanged = Math.Max(w.MostRanged, ranged);
            }
        }

        [UnityTest]
        public IEnumerator ACrowdOfSixShootersAndFourBrutes_NeverMoreThanTheCapsWindUp_AndEachAttacksInTime()
        {
            LotsOfHealth();
            List<Enemy> crowd = SpawnCrowd();
            var w = new Watch();
            Time.timeScale = 4f;
            yield return WatchCrowd(crowd, w, CrowdSeconds);
            Time.timeScale = 1f;
            TurnValues v = Enemy.Turns.Values;
            string times = string.Join("; ", crowd.Select(e => e.Kind + " " + e.Id + ": " + string.Join(", ", w.Begins[e.Id].Select(t => t.ToString("0.0")))));
            Debug.Log($"[023] crowd: most heavies at once {w.MostHeavy}, most ranged {w.MostRanged}; wind-ups at {times}");
            Assert.That(w.MostHeavy, Is.LessThanOrEqualTo(v.HeavyAtOnce), "never more than two brutes winding up at once");
            Assert.That(w.MostRanged, Is.LessThanOrEqualTo(v.RangedAtOnce), "never more than two shooters winding up at once");
            Assert.That(w.MostHeavy, Is.EqualTo(v.HeavyAtOnce), "and the turns are used");
            Assert.That(w.MostRanged, Is.EqualTo(v.RangedAtOnce), "and the turns are used");
            foreach (Enemy e in crowd)
            {
                List<float> begins = w.Begins[e.Id];
                Assert.That(begins.Count, Is.GreaterThanOrEqualTo(2), e.Kind + " " + e.Id + " attacked, and again");
                Assert.That(begins[0], Is.LessThanOrEqualTo(FirstAttackWithin), e.Kind + " " + e.Id + "'s first attack");
                for (int i = 1; i < begins.Count; i++)
                    Assert.That(begins[i] - begins[i - 1], Is.LessThanOrEqualTo(NextAttackWithin), e.Kind + " " + e.Id + " waited too long");
                Assert.That(CrowdSeconds - begins[begins.Count - 1], Is.LessThanOrEqualTo(NextAttackWithin), e.Kind + " " + e.Id + " at the end");
            }
        }

        [UnityTest]
        public IEnumerator WithTheRuleOff_TheCrowdWindsUpAllAtOnce_AsBefore()
        {
            LotsOfHealth();
            Enemy.Turns.Values = new TurnValues { On = false, Shared = false };
            List<Enemy> crowd = SpawnCrowd();
            var w = new Watch();
            Time.timeScale = 4f;
            yield return WatchCrowd(crowd, w, 8f);
            Time.timeScale = 1f;
            Debug.Log($"[023] rule off: most heavies at once {w.MostHeavy}, most ranged {w.MostRanged}");
            Assert.That(w.MostRanged, Is.GreaterThan(2), "more shooters than the cap wind up at once");
            Assert.That(w.MostHeavy, Is.GreaterThan(2), "more brutes than the cap wind up at once");
            Assert.That(crowd.All(e => !Enemy.Turns.IsWaiting(e.Id, Time.time)), Is.True, "nobody waits");
        }

        [UnityTest]
        public IEnumerator AHeldBackBrute_KeepsWalking_AndAHeldBackShooter_KeepsItsDistance()
        {
            LotsOfHealth();
            List<Enemy> crowd = SpawnCrowd();
            var w = new Watch();
            Time.timeScale = 4f;
            yield return WatchCrowd(crowd, w, 12f);
            Time.timeScale = 1f;
            var values = new ShooterValues();
            Debug.Log($"[023] held back: brutes {w.BruteHeldFar:0.00} s far from her, walked {w.BruteWalkedHeld:0.0} m; shooters {w.ShooterHeldFrames} frames, "
                      + $"{w.ShooterHeldNearest:0.0} to {w.ShooterHeldFarthest:0.0} m from her, {w.ShooterHeldOutside} frames outside {values.BackOffUnder - 0.5f} to {values.KeepFar + 1.5f} m");
            Assert.That(w.BruteHeldFar, Is.GreaterThan(0.2f), "a brute was held back while still far from her");
            Assert.That(w.BruteWalkedHeld / w.BruteHeldFar, Is.GreaterThan(1f), "and walked on toward her meanwhile (m/s)");
            Assert.That(w.ShooterHeldFrames, Is.GreaterThan(0), "a shooter was held back");
            Assert.That(w.ShooterHeldOutside, Is.EqualTo(0), "held back, it kept its distance");
        }

        [UnityTest]
        public IEnumerator ThePracticeRoomsEnemy_IsUntouchedByTheRule()
        {
            // Both heavy turns are taken by wind-ups that last the test: a brute that takes turns could not begin one.
            Enemy.Turns.Began(-1, TurnSort.Heavy, Time.time, 1000f);
            Enemy.Turns.Began(-2, TurnSort.Heavy, Time.time, 1000f);
            Assert.That(Enemy.Turns.Ask(-3, TurnSort.Heavy, Time.time + 1f), Is.False, "the heavy turns are taken");
            Assert.That(_practice.TakesTurns, Is.False, "the practice brute takes no turns");
            _practice.enabled = true;
            _player.Teleport(_practice.transform.position + Vector3.right * 2f);
            yield return Frames(() => _practice.State == EnemyState.Windup, 2000);
            Assert.That(_practice.State, Is.EqualTo(EnemyState.Windup), "it winds up as ever");
            Assert.That(Enemy.Turns.IsWaiting(_practice.Id, Time.time), Is.False, "it never asked");
            List<Enemy> spawned = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(spawned.Single().TakesTurns, Is.False, "nor does one spawned in the practice room");
        }

        // ---------------- The floors' brutes' Shockwave ----------------

        [UnityTest]
        public IEnumerator AFloorsBrute_HasTheShorterShockwave_AndThePracticeBruteKeepsTheAssets()
        {
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            yield return null;
            List<EnemyBrute> brutes = level.Enemies.OfType<EnemyBrute>().ToList();
            Assert.That(brutes, Is.Not.Empty, "floor 1 has brutes");
            var values = new LevelValues();
            foreach (EnemyBrute b in brutes)
            {
                EnemyAttackDefinition wave = b.Values.Attacks.Single(a => a.Name == "Shockwave");
                Assert.That(wave.MaxDistance, Is.EqualTo(values.ShockwaveFarthest), b.name);
                Assert.That(wave.Length, Is.EqualTo(values.ShockwaveLength), b.name);
                // An elite takes no turn (work order 030, the merge: work order 023's rule made so now that elites exist).
                Assert.That(b.TakesTurns, Is.EqualTo(!b.IsElite), b.name + (b.IsElite ? " is the elite: it takes no turns" : " takes turns"));
            }
            EnemyAttackDefinition own = _practice.Values.Attacks.Single(a => a.Name == "Shockwave");
            Assert.That(_practice.FloorValues, Is.Null);
            Assert.That(own.MaxDistance, Is.EqualTo(_practice.Settings.Values.Attacks.Single(a => a.Name == "Shockwave").MaxDistance), "the practice brute reads the asset");
            Assert.That(own.MaxDistance, Is.GreaterThan(values.ShockwaveFarthest), "and the asset keeps its longer reach");
            Assert.That(own.Length, Is.GreaterThan(values.ShockwaveLength));
        }

        // ---------------- The floor heal ----------------

        [UnityTest]
        public IEnumerator SheIsHealed_WhenAFloorsRewardIsTaken_AndNotWhenAChestsUpgradeIs()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeBalanceTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _levels.StartRun(0x12345678UL);
            yield return null;
            var run = new RunValues();

            // A chest: no floor heal (an upgrade that heals by itself is not taken).
            _health.TakeHit(60);
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            _player.Teleport(c + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * 1.4f);
            yield return null;
            yield return null;
            Assert.That(_levels.OpenChest(), Is.True);
            int pick = _levels.OfferedUpgrades.Count > 1 && _levels.OfferedUpgrades[0].Kind == UpgradeKind.MaxHealth ? 1 : 0;
            Assume.That(_levels.OfferedUpgrades[pick].Kind, Is.Not.EqualTo(UpgradeKind.MaxHealth), "an upgrade that does not heal");
            int beforeChest = _health.Current;
            Assert.That(_levels.PickUpgrade(pick), Is.True);
            Assert.That(_health.Current, Is.EqualTo(beforeChest), "a chest step heals nothing");

            // The floor's reward: healed by the share of her maximum.
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            int before = _health.Current;
            int heal = FloorHeals.Amount(_health.Max, run.FloorHeal);
            Assert.That(heal, Is.EqualTo(Mathf.RoundToInt(_health.Max * 0.35f)));
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025): the first way offered
            Assert.That(_health.Current, Is.EqualTo(Math.Min(_health.Max, before + heal)), "healed by 35 % of her maximum");
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.Record.FloorHealText, Does.Contain("+" + heal), "said on screen with what was taken");
            StringAssert.Contains("+" + heal + " health", _levels.StatusText(), "and in the line of what was taken");
            SessionEvent healed = _recorder.Events.SingleOrDefault(e => e.Type == SessionEventTypes.FloorHeal);
            Assert.That(healed, Is.Not.Null, "the recorder is told");
            Assert.That(healed.ExtraValue("healed"), Is.EqualTo(heal.ToString()));
            Assert.That(healed.ExtraValue("floor"), Is.EqualTo("1"));
        }

        // ---------------- The room heal (work order 030, lever A) ----------------

        [UnityTest]
        public IEnumerator SheIsHealed_WhenACombatRoomIsCleared_AndByTheEliteRoomsAmountThere_Once()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeBalanceTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            TimingFeedback feedback = TimingFeedback.Ensure();
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            yield return null;
            var run = new RunValues();

            // A combat room: every enemy of it killed, she gets 8 % of her maximum, once.
            _health.TakeHit(60);
            int before = _health.Current;
            LevelRoom room = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite);
            foreach (Enemy e in level.EnemiesByRoom[room.Order]) e.Body.TakeHit(100000000);
            yield return Frames(() => _health.Current != before, 60);
            int heal = FloorHeals.Amount(_health.Max, run.RoomHeal);
            Assert.That(heal, Is.EqualTo(Mathf.RoundToInt(_health.Max * 0.08f)));
            Assert.That(_health.Current, Is.EqualTo(before + heal), "healed by 8 % of her maximum");
            yield return null;
            Assert.That(feedback.Numbers.Any(n => n.Kind == NumberKind.Heal && n.Text == "+" + heal && n.OverHer), Is.True, "a green +" + heal + " over her");
            Assert.That(FeedbackRules.NumberTint(NumberKind.Heal), Is.EqualTo(FeedbackTint.Clean), "green");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_health.Current, Is.EqualTo(before + heal), "once");

            // The elite room: 20 %, besides its upgrade, once.
            _health.TakeHit(40);
            int beforeElite = _health.Current;
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade, 60);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "its upgrade is offered too");
            int eliteHeal = FloorHeals.Amount(_health.Max, run.EliteRoomHeal);
            Assert.That(eliteHeal, Is.EqualTo(Mathf.RoundToInt(_health.Max * 0.2f)));
            Assert.That(_health.Current, Is.EqualTo(beforeElite + eliteHeal), "healed by 20 % of her maximum, not 8 %");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_health.Current, Is.EqualTo(beforeElite + eliteHeal), "once");

            List<SessionEvent> heals = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomHeal).ToList();
            Assert.That(heals.Count, Is.EqualTo(2), "the recorder is told of each");
            Assert.That(heals[0].ExtraValue("healed"), Is.EqualTo(heal.ToString()));
            Assert.That(heals[0].ExtraValue("elite"), Is.EqualTo("false"));
            Assert.That(heals[0].ExtraValue("room"), Is.EqualTo(room.Order.ToString()));
            Assert.That(heals[1].ExtraValue("healed"), Is.EqualTo(eliteHeal.ToString()));
            Assert.That(heals[1].ExtraValue("elite"), Is.EqualTo("true"));
        }

        // ---------------- The danger by floor (work order 030, lever B) ----------------

        /// <summary>She stands 1.5 m in front of it until one of its strikes lands (or that much game time): what it dealt, -1 for none.</summary>
        IEnumerator LandOne(Enemy e, int[] took, float seconds = 12f)
        {
            took[0] = -1;
            void Watch(EnemyReport r)
            {
                if (r.Kind == EnemyReportKind.Struck && r.Outcome == StrikeOutcome.Landed && took[0] < 0) took[0] = r.Damage;
            }
            e.Reported += Watch;
            e.enabled = true;
            e.Provoke();
            float until = Time.time + seconds;
            while (took[0] < 0 && Time.time < until && _health.IsAlive)
            {
                _player.Teleport(e.transform.position + Vector3.back * 1.5f);
                yield return null;
            }
            e.Reported -= Watch;
        }

        static bool OneOfItsAttacks(Enemy e, int took, float scale) =>
            e.Values.Attacks.Any(a => a != null && Mathf.RoundToInt(a.Damage * scale) == took);

        /// <summary>The floors before <paramref name="floor"/> finished for tests, their rewards taken.</summary>
        IEnumerator ToFloor(int floor)
        {
            while (_levels.Floor < floor)
            {
                _levels.FinishFloorForTests();
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                Assert.That(_levels.PickReward(0), Is.True);
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025): the first way offered
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AFloorOneEnemy_HurtsHerBy06OfItsAttack_AFloorFourOne_By08_TheBossAndThePracticeEnemies_ByAllOfIt()
        {
            LotsOfHealth();
            var values = new LevelValues();
            Time.timeScale = 2f;
            var took = new int[1];

            // Floor 1: 0.6 (work order 046's flights; 0.7 before).
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            yield return null;
            Enemy one = level.Enemies.First(e => e.Kind == EnemyKind.Brute && !e.IsElite);
            Assert.That(one.DamageScale, Is.EqualTo(0.6f).Within(1e-6f), "a floor-1 enemy deals 0.6");
            Assert.That(level.Elite.DamageScale, Is.EqualTo(values.EliteDamageScale * 0.6f).Within(1e-5f), "its elite 1.25 x 0.6");
            Assert.That(level.Enemies.Where(e => !e.IsElite).All(e => Mathf.Abs(e.DamageScale - 0.6f) < 1e-6f), Is.True, "every one of floor 1");
            yield return LandOne(one, took);
            Assume.That(took[0], Is.GreaterThan(0), "it struck her");
            Assert.That(OneOfItsAttacks(one, took[0], 0.6f), Is.True, "0.6 of one of its attacks: " + took[0]);
            Assert.That(OneOfItsAttacks(one, took[0], 1f), Is.False, "not all of it: " + took[0]);

            // Floor 4: 0.8 (work order 046's flights; all of it before).
            Time.timeScale = 1f;
            yield return ToFloor(4);
            level = _levels.Level;
            Assert.That(_levels.Floor, Is.EqualTo(4));
            Enemy four = level.Enemies.First(e => e.Kind == EnemyKind.Brute && !e.IsElite);
            Assert.That(four.DamageScale, Is.EqualTo(0.8f).Within(1e-6f), "a floor-4 enemy deals 0.8");
            LotsOfHealth();
            Time.timeScale = 2f;
            yield return LandOne(four, took);
            Assume.That(took[0], Is.GreaterThan(0), "it struck her");
            Assert.That(OneOfItsAttacks(four, took[0], 0.8f), Is.True, "0.8 of one of its attacks: " + took[0]);

            // Floor 5's boss: not scaled.
            Time.timeScale = 1f;
            yield return ToFloor(5);
            Assert.That(_levels.Level.Boss, Is.Not.Null);
            Assert.That(_levels.Level.Boss.DamageScale, Is.EqualTo(1f), "the act boss is not scaled");
            _levels.BackToPracticeRoom();
            yield return null;

            // The practice room: all of it.
            LotsOfHealth();
            Enemy guard = _levels.SpawnPractice(EnemyKind.Guard).Single();
            Enemy boss = _levels.SpawnPractice(EnemyKind.Boss).Single();
            Assert.That(guard.DamageScale, Is.EqualTo(1f));
            Assert.That(boss.DamageScale, Is.EqualTo(1f));
            Object.Destroy(boss.gameObject);
            Time.timeScale = 2f;
            yield return LandOne(guard, took);
            Time.timeScale = 1f;
            Assume.That(took[0], Is.GreaterThan(0), "the practice guard struck her");
            Assert.That(OneOfItsAttacks(guard, took[0], 1f), Is.True, "all of one of its attacks: " + took[0]);
        }

        // ---------------- One crowd, one queue (work order 030, lever D) ----------------

        const float SharedFirstWithin = 10f;  // s of game time from the fight's start to each one's first wind-up, shared queue on
        const float SharedNextWithin = 12f;   // s of game time between two wind-ups of one enemy, shared queue on

        [UnityTest]
        public IEnumerator WithTheSharedQueue_FourBrutesAndFourShootersOnFloorOne_NeverMoreThanTwoWindUp_AndEachAttacksInTime()
        {
            LotsOfHealth();
            Enemy.Turns.Values = new TurnValues { Shared = true }; // off by default: on for this test
            Enemy.Turns.Floor = 1;
            var crowd = new List<Enemy>();
            for (int i = 0; i < 4; i++) crowd.AddRange(_levels.SpawnPractice(EnemyKind.Shooter));
            for (int i = 0; i < 4; i++) crowd.AddRange(_levels.SpawnPractice(EnemyKind.Brute));
            foreach (Enemy e in crowd)
            {
                e.TakesTurns = true;
                e.HealthOverride = 1000000;
                e.enabled = true;
                e.Provoke();
            }
            var w = new Watch();
            int mostTogether = 0;
            float start = Time.time;
            Time.timeScale = 4f;
            IEnumerator watch = WatchCrowd(crowd, w, CrowdSeconds);
            while (watch.MoveNext())
            {
                yield return watch.Current;
                mostTogether = Math.Max(mostTogether, crowd.Count(e => e.State == EnemyState.Windup));
            }
            Time.timeScale = 1f;
            string times = string.Join("; ", crowd.Select(e => e.Kind + " " + e.Id + ": " + string.Join(", ", w.Begins[e.Id].Select(t => t.ToString("0.0")))));
            Debug.Log($"[030] shared queue, floor 1: most winding up together {mostTogether}; wind-ups at {times}");
            Assert.That(Enemy.Turns.Values.Shared, Is.True);
            Assert.That(mostTogether, Is.LessThanOrEqualTo(2), "never more than two winding up at once, brutes and shooters together");
            Assert.That(mostTogether, Is.EqualTo(2), "and both turns are used");
            foreach (Enemy e in crowd)
            {
                List<float> begins = w.Begins[e.Id];
                Assert.That(begins.Count, Is.GreaterThanOrEqualTo(2), e.Kind + " " + e.Id + " attacked, and again");
                Assert.That(begins[0], Is.LessThanOrEqualTo(SharedFirstWithin), e.Kind + " " + e.Id + "'s first attack");
                for (int i = 1; i < begins.Count; i++)
                    Assert.That(begins[i] - begins[i - 1], Is.LessThanOrEqualTo(SharedNextWithin), e.Kind + " " + e.Id + " waited too long");
            }
        }
    }

    /// <summary>
    /// Work order 030, part 1 (2026-11-20): a doorway that blocked her. Five of the first balance pass's flights ended
    /// stuck the same way: she fought an enemy back through a door, killed it in the doorway, and could not walk through
    /// that doorway afterwards with nothing in sight. Here an enemy that stays dead is killed in a floor's first doorway,
    /// its body is let hide, and she is steered straight through the door from 4 m before it to 4 m past it, as a player
    /// holding the key would: she must get through. For a guard (the flights' case), an elite, the act boss and a swarmer
    /// (the boss's called swarmers are made as these are); and an enemy that comes back (the practice room's) has its agent
    /// back, at home.
    /// </summary>
    public class DoorwayTests
    {
        const float ThroughWithin = 6f; // s of game time to walk 8 m at her run speed, with room to spare

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practice;
        GeneratedLevels _levels;

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
            _practice = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practice, Is.Not.Null, "the practice enemy, to copy");
            _levels = GeneratedLevels.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_health != null) _health.RestoreFull();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>A floor with its enemies out of the way (as the pilot's walk test has it); the first door's middle and the way through it from the entrance.</summary>
        GeneratedLevel FloorWithNobody(out Vector3 middle, out Vector3 through)
        {
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            // Switched off rather than removed: a room of removed enemies counts as cleared, and the elite room's choice or
            // the floor's end would pause the game.
            foreach (Enemy e in level.Enemies.ToList()) e.gameObject.SetActive(false);
            LevelDoor door = level.Layout.Doors.First(d => d.Joins(0));
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            Vector3 wa = level.ToWorld(a), wb = level.ToWorld(b);
            middle = (wa + wb) * 0.5f;
            Vector3 along = (wb - wa).normalized;
            through = new Vector3(-along.z, 0f, along.x);
            Vector3 entrance = level.Middle(level.Layout.Rooms[0]);
            if (Vector3.Dot(middle - entrance, through) < 0f) through = -through;
            return level;
        }

        Enemy MakeIn(GeneratedLevel level, EnemyKind kind, Vector3 at, bool elite)
        {
            Enemy e = EnemyFactory.Make(kind, at, level.transform, _practice, "Doorway_" + kind);
            Assert.That(e, Is.Not.Null);
            e.enabled = true; // a copy of the practice brute is as switched on as it
            e.HealthOverride = 100;
            if (elite) EnemyFactory.MakeElite(e, 100, 1.25f, 1.25f, 0.5f);
            return e;
        }

        /// <summary>Steers her straight along <paramref name="dir"/> for that much game time or until she is past <paramref name="goal"/>; how far past the door's middle she got.</summary>
        IEnumerator SteerThrough(Vector3 middle, Vector3 dir, float seconds, float[] reached)
        {
            float until = Time.time + seconds, realUntil = Time.realtimeSinceStartup + 60f;
            reached[0] = float.MinValue;
            while (Time.time < until && Time.realtimeSinceStartup < realUntil)
            {
                _player.Steer(dir);
                yield return null;
                reached[0] = Mathf.Max(reached[0], Vector3.Dot(_player.Position - middle, dir));
                if (reached[0] > 3f) break;
            }
            _player.Stop();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AnEnemyKilledInADoorway_DoesNotBlockHer_OnceItsBodyIsHidden(
            [Values("guard", "elite guard", "brute", "elite brute", "elite shooter", "boss", "swarmer", "caster", "elite caster")] string which)
        {
            // Work order 034: the caster and the elite caster added, on purpose: a dead caster is no obstacle either.
            bool elite = which.StartsWith("elite ");
            EnemyKind kind = which.EndsWith("guard") ? EnemyKind.Guard : which.EndsWith("brute") ? EnemyKind.Brute
                           : which.EndsWith("shooter") ? EnemyKind.Shooter : which.EndsWith("caster") ? EnemyKind.Caster
                           : which == "boss" ? EnemyKind.Boss : EnemyKind.Swarmer;
            GeneratedLevel level = FloorWithNobody(out Vector3 middle, out Vector3 through);
            yield return null;
            Enemy dead = MakeIn(level, kind, middle, elite);
            yield return null;
            yield return null;
            Assert.That(dead.ComesBack, Is.False, "an enemy of a floor stays dead");
            dead.Body.TakeHit(1000000);
            float hideAt = Time.time + dead.ShownDeadFor(dead.Values) + 0.3f;
            float realUntil = Time.realtimeSinceStartup + 60f;
            while (Time.time < hideAt && Time.realtimeSinceStartup < realUntil) yield return null;
            Debug.Log($"[030] doorway, a dead {which}: hidden after {dead.ShownDeadFor(dead.Values):0.00} s, time scale {Time.timeScale}");
            Assert.That(dead.IsDead, Is.True);
            Assert.That(dead.BodyRenderer == null || !dead.BodyRenderer.enabled, Is.True, "its body is hidden");

            _player.Teleport(middle - through * 4f);
            yield return null;
            yield return null;
            var reached = new float[1];
            Time.timeScale = 2f;
            yield return SteerThrough(middle, through, ThroughWithin, reached);
            Time.timeScale = 1f;
            Debug.Log($"[030] doorway, a dead {which}: she got {reached[0]:0.00} m past the door's middle");
            Assert.That(reached[0], Is.GreaterThan(3f), "she walked through the doorway where the " + (elite ? "elite " : "") + kind + " died");
            NavMeshAgentOf(dead, out UnityEngine.AI.NavMeshAgent agent);
            Assert.That(agent == null || !agent.enabled, Is.True, "a dead enemy's agent takes no part in avoidance");
        }

        static void NavMeshAgentOf(Enemy e, out UnityEngine.AI.NavMeshAgent agent) => agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>();

        [UnityTest, Timeout(180000)]
        public IEnumerator AnEnemyThatComesBack_GetsItsAgentBack_AtHome()
        {
            // The practice enemy comes back: its agent is switched on again where it stands at home, and it walks.
            Assert.That(_practice.ComesBack, Is.True);
            _player.Teleport(_practice.Home + Vector3.right * 6f);
            yield return null;
            _practice.Body.TakeHit(1000000);
            yield return null;
            yield return null;
            NavMeshAgentOf(_practice, out UnityEngine.AI.NavMeshAgent agent);
            Assert.That(agent.enabled, Is.False, "dead, its agent is off");
            float until = Time.time + _practice.Values.RespawnDelay + 1f;
            float realUntil = Time.realtimeSinceStartup + 60f;
            while (_practice.IsDead && Time.time < until && Time.realtimeSinceStartup < realUntil) yield return null;
            Assert.That(_practice.IsDead, Is.False, "it came back");
            yield return null;
            Assert.That(agent.enabled, Is.True, "and its agent is on again");
            Assert.That(agent.isOnNavMesh, Is.True, "on the NavMesh");
            Assert.That(Flat(_practice.transform.position, _practice.Home), Is.LessThan(0.5f), "at home");
            // An obstacle again: one more enemy for the crowd to walk round, as before it died.
            Assert.That(agent.obstacleAvoidanceType, Is.Not.EqualTo(UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance));
        }
    }
}
