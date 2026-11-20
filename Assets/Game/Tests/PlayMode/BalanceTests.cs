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
    /// floor heal: she is healed when a floor's reward is taken, and not when a chest's upgrade is.
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
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _maxHealthBefore = _health.MaxHealth;
            _practice = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practice, Is.Not.Null, "the practice enemy, to copy");
            _practice.enabled = false;
            _turnsBefore = Enemy.Turns.Values;
            Enemy.Turns.Values = new TurnValues();
            Enemy.Turns.Clear();
            _levels = GeneratedLevels.Ensure();
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
            Enemy.Turns.Values = new TurnValues { On = false };
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
                Assert.That(b.TakesTurns, Is.True, b.name + " takes turns");
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
            Assert.That(_health.Current, Is.EqualTo(Math.Min(_health.Max, before + heal)), "healed by 35 % of her maximum");
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.Record.FloorHealText, Does.Contain("+" + heal), "said on screen with what was taken");
            StringAssert.Contains("+" + heal + " health", _levels.StatusText(), "and in the line of what was taken");
            SessionEvent healed = _recorder.Events.SingleOrDefault(e => e.Type == SessionEventTypes.FloorHeal);
            Assert.That(healed, Is.Not.Null, "the recorder is told");
            Assert.That(healed.ExtraValue("healed"), Is.EqualTo(heal.ToString()));
            Assert.That(healed.ExtraValue("floor"), Is.EqualTo("1"));
        }
    }
}
