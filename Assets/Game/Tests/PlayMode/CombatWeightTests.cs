using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 080 (weight in a hit, a kill and a clean press), in the ControlRoom scene through the intents and the
    /// game's own reports: a hit of each weight starts its own impact and not the morning's tick; her kill starts its own
    /// sound and a burst, not the enemy's death thump; the last kill there starts the room won, and no kill under it; a
    /// clean Triple Tap chain ticks a step up and ends on the top note, an unclean press is silent and resets the row, and
    /// the clean tap's tone is not voiced over the tick; the switch off gives the morning's sounds; the burst is pooled;
    /// and nothing is allocated per frame while nothing happens. All counted through what was asked to play
    /// (<see cref="PlaceholderSounds.Requested"/>, <see cref="ShotSounds.Requested"/> for the voices): nobody has heard any of it.
    /// </summary>
    public class CombatWeightTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);   // Dummy_A is 2.3 m away here (the sound tests' place)
        static readonly Vector3 TargetAt = new Vector3(0f, 0f, 8f); // where a burst is shown by hand
        static readonly Vector3 Facing = new Vector3(0f, 0f, 1f);   // her, on open floor, when a practice enemy to kill is spawned off her

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        Damageable _a;
        GeneratedLevels _levels;
        PlaceholderSounds _sounds;
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>();
        readonly List<ShotSoundRequest> _voiced = new List<ShotSoundRequest>();

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            PlaceholderSounds.Requested += Record;
            ShotSounds.Requested += Voice;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true; // nothing here is about her health
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice brute");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _sounds = PlaceholderSounds.Ensure();
            Assert.That(_sounds.Weight.On, Is.True, "on by default");
            yield return null;
            _played.Clear();
            _voiced.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            ShotSounds.Requested -= Voice;
            if (_player != null && (_levels == null || _player.Modifiers != _levels.Modifiers)) _player.Modifiers = null;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
            _played.Clear();
            _voiced.Clear();
        }

        void Record(PlaceholderSoundRequest r) => _played.Add(r);
        void Voice(ShotSoundRequest r) => _voiced.Add(r);

        // ---------------- Helpers ----------------

        int Count(PlaceholderSound sound) => _played.Count(r => r.Sound == sound);
        string Heard => string.Join(", ", _played.Select(r => r.Sound + "@" + r.Frame + (r.Variant > 0 ? "#" + r.Variant : "")));

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        IEnumerator UntilNoChain() => Until(() => _player.RecastCount == 0 && !_player.IsLocked && _player.Action == ActionKind.None, 5f);

        void ByHand(float critChance) =>
            _player.Modifiers = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues { StartCritChance = critChance } };

        /// <summary>A practice shooter spawned off her standing at <paramref name="from"/>, standing still, with this much health.</summary>
        EnemyShooter Shooter(Vector3 from, int health)
        {
            _player.Teleport(from);
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(made.Count, Is.EqualTo(1), "a shooter spawned");
            var shooter = (EnemyShooter)made[0];
            shooter.Tuning.Enemy.MoveSpeed = 0f;
            shooter.Body.SetMaxHealth(health);
            return shooter;
        }

        /// <summary>One with one point of health, spawned off her where she will shoot from: her next hit kills it.</summary>
        EnemyShooter OneToKill() => Shooter(Facing, 1);

        IEnumerator ShootToKill(Enemy enemy)
        {
            yield return UntilFree();
            _player.Shoot(enemy.transform.position);
            yield return Until(() => enemy.IsDead, 2f);
            Assert.That(enemy.IsDead, Is.True, "her shot killed it");
            yield return Frames(4); // its own Update reports its death
        }

        // ---------------- 1. A hit by its weight ----------------

        [UnityTest]
        public IEnumerator AHitOfEachWeight_StartsItsOwnImpact_NotTheMorningsTick()
        {
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.HitBasic), Is.EqualTo(1), "the basic shot's impact: " + Heard);
            Assert.That(Count(PlaceholderSound.EnemyHit) + Count(PlaceholderSound.HitHeavy) + Count(PlaceholderSound.HitCrit), Is.EqualTo(0), "and nothing else: " + Heard);
            Assert.That(_played.Single(r => r.Sound == PlaceholderSound.HitBasic).Variant, Is.InRange(1, 4), "one of its variations");

            // A skill's hit: Triple Tap's first press.
            _played.Clear();
            yield return UntilFree();
            int tap = _player.SlotOf("Triple Tap");
            int had = _a.HitsTaken;
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken > had, 2f);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.HitHeavy), Is.EqualTo(1), "a skill's impact: " + Heard);
            Assert.That(Count(PlaceholderSound.HitBasic) + Count(PlaceholderSound.EnemyHit), Is.EqualTo(0), Heard);
            yield return UntilNoChain();

            // A crit: every hit crits.
            ByHand(1f);
            _played.Clear();
            yield return UntilFree();
            had = _a.HitsTaken;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken > had, 2f);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.HitCrit), Is.EqualTo(1), "a crit's crack: " + Heard);
            Assert.That(Count(PlaceholderSound.Crit) + Count(PlaceholderSound.EnemyHit) + Count(PlaceholderSound.HitBasic), Is.EqualTo(0),
                        "in place of the hit tick and the crit's tick: " + Heard);
            Assert.That(_sounds.ImpactsHeard, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator TwoImpactsInARow_AreNeverTheSameVariation()
        {
            _player.Teleport(NearA);
            yield return null;
            for (int i = 0; i < 5; i++)
            {
                yield return UntilFree();
                int had = _a.HitsTaken;
                _player.Shoot(_a.transform.position);
                yield return Until(() => _a.HitsTaken > had, 2f);
                yield return new WaitForSeconds(0.3f); // past the window's limit
            }
            List<int> variants = _played.Where(r => r.Sound == PlaceholderSound.HitBasic).Select(r => r.Variant).ToList();
            Assert.That(variants.Count, Is.EqualTo(5), Heard);
            for (int i = 1; i < variants.Count; i++) Assert.That(variants[i], Is.Not.EqualTo(variants[i - 1]), "no two in a row: " + Heard);
        }

        // ---------------- 2. A kill pays ----------------

        [UnityTest]
        public IEnumerator HerKill_StartsTheKillSound_AndABurst_NotTheEnemysDeathThump()
        {
            // Another one alive, off to the side (the practice brute is switched off, and so not among the enemies there).
            EnemyShooter other = Shooter(NearA + new Vector3(4f, 0f, 0f), 1000000);
            EnemyShooter shooter = OneToKill();
            yield return Frames(2);
            Assume.That(Enemy.All.Count(e => e != null && !e.IsDead), Is.EqualTo(2), "two alive: the room is not won by the first");
            _played.Clear();
            yield return ShootToKill(shooter);
            Assert.That(Count(PlaceholderSound.Kill), Is.EqualTo(1), "her kill: " + Heard);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(0), "not the enemy's death thump over it: " + Heard);
            Assert.That(Count(PlaceholderSound.RoomWon), Is.EqualTo(0), "another is alive");
            Assert.That(Count(PlaceholderSound.HitBasic), Is.EqualTo(1), "the killing hit's impact under it");
            Assert.That(_sounds.KillsHeard, Is.EqualTo(1));
            Assert.That(_sounds.Bursts, Is.Not.Null);
            Assert.That(_sounds.Bursts.Shown, Is.EqualTo(1), "a burst where it stood");
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.Kill), Is.EqualTo(1), "once");

            // A death not by her hand is the enemy's as ever.
            _played.Clear();
            other.Body.TakeHit(100000000);
            yield return Frames(4);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(1), Heard);
            Assert.That(Count(PlaceholderSound.Kill) + Count(PlaceholderSound.RoomWon), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheLastKillThere_StartsTheRoomWon_Once_AndNoKillUnderIt()
        {
            EnemyShooter shooter = OneToKill();
            yield return Frames(2);
            Assume.That(Enemy.All.Count(e => e != null && !e.IsDead), Is.EqualTo(1), "the shooter is the last one alive");
            _played.Clear();
            yield return ShootToKill(shooter);
            Assert.That(Count(PlaceholderSound.RoomWon), Is.EqualTo(1), "the room is won: " + Heard);
            Assert.That(Count(PlaceholderSound.Kill) + Count(PlaceholderSound.EnemyDies), Is.EqualTo(0), "one sound for it: " + Heard);
            Assert.That(_sounds.RoomsWonHeard, Is.EqualTo(1));
            Assert.That(_sounds.Bursts.LastSize, Is.EqualTo(_sounds.Weight.Kills.BurstSize * _sounds.Weight.Kills.RoomWonScale).Within(1e-4f), "its burst larger");
            Assert.That(PlaceholderRecipes.Seconds(PlaceholderSound.RoomWon), Is.GreaterThan(PlaceholderRecipes.Seconds(PlaceholderSound.Kill)));
        }

        [UnityTest]
        public IEnumerator TheBurst_IsPooled_AndRestsWhenNoneLives()
        {
            KillWeightValues v = _sounds.Weight.Kills;
            KillBursts bursts = KillBursts.Make(_sounds.transform);
            for (int i = 0; i < 3 * v.BurstPool; i++) bursts.Burst(TargetAt, i % 5 == 0, v, _player.ShotFxMaterial);
            Assert.That(bursts.Made, Is.EqualTo(v.BurstPool), "never more pieces than the pool");
            Assert.That(bursts.Live, Is.EqualTo(v.BurstPool));
            Assert.That(bursts.Shown, Is.EqualTo(3 * v.BurstPool));
            Assert.That(bursts.transform.childCount, Is.EqualTo(v.BurstPool));
            yield return new WaitForSeconds(v.BurstSeconds * v.RoomWonScale + 0.15f);
            yield return null;
            Assert.That(bursts.Live, Is.EqualTo(0), "all gone in their time");
            Assert.That(bursts.enabled, Is.False, "and it does not run while none lives");
            Assert.That(bursts.GetComponentsInChildren<MeshRenderer>(false).Length, Is.EqualTo(0), "nothing shown");
            bursts.Burst(TargetAt, false, v, _player.ShotFxMaterial);
            Assert.That(bursts.Made, Is.EqualTo(v.BurstPool), "a new one takes a piece of the pool");
            Assert.That(bursts.enabled, Is.True);
        }

        // ---------------- 3. A clean press heard, a chain rising ----------------

        [UnityTest]
        public IEnumerator ACleanChain_TicksAStepUp_AndEndsOnTheTop_AnUncleanPress_IsSilent_AndResetsTheRow()
        {
            TimingFeedback feedback = TimingFeedback.Ensure();
            var tones = new List<FeedbackTone>();
            feedback.TonePlayed += t => tones.Add(t);
            _player.Teleport(NearA);
            yield return null;
            int tap = _player.SlotOf("Triple Tap");
            yield return UntilFree();
            _played.Clear();
            _voiced.Clear();

            // Clean all through: press 2 a step, press 3 the top.
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(Count(PlaceholderSound.CleanTick) + Count(PlaceholderSound.ChainTop), Is.EqualTo(0), "the first press starts the chain: no tick");
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "press 2 in its clean part");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Frames(1);
            Assert.That(Count(PlaceholderSound.CleanTick), Is.EqualTo(1), "press 2 ticks: " + Heard);
            PlaceholderSoundRequest step1 = _played.Single(r => r.Sound == PlaceholderSound.CleanTick);
            Assert.That(step1.Variant, Is.EqualTo(1), "the first step");
            Assert.That(step1.Pitch, Is.EqualTo(1f).Within(1e-5f), "at its own pitch, exactly");
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "press 3 in its clean part");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Frames(1);
            Assert.That(Count(PlaceholderSound.ChainTop), Is.EqualTo(1), "clean all through: the top note: " + Heard);
            Assert.That(Count(PlaceholderSound.CleanTick), Is.EqualTo(1), "and no step under it");
            yield return Until(() => tones.Contains(FeedbackTone.Clean), 2f);
            yield return Frames(2);
            Assert.That(tones.Count(t => t == FeedbackTone.Clean), Is.EqualTo(1), "the clean tap's tone is still told");
            Assert.That(_voiced.Count(r => r.Clip == feedback.CleanTone), Is.EqualTo(0), "but not voiced over the press's own tick");
            Assert.That(Count(PlaceholderSound.HitCleanEdge), Is.GreaterThanOrEqualTo(1), "a clean press's hit has its edge: " + Heard);

            // Unclean: press 2 the moment its window opens (before its clean part), silent; press 3 clean is step 1, not the top.
            yield return UntilNoChain();
            yield return new WaitForSeconds(0.3f);
            _played.Clear();
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.False, "the window just opened: before its clean part");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Frames(1);
            Assert.That(Count(PlaceholderSound.CleanTick) + Count(PlaceholderSound.OffTick), Is.EqualTo(0), "off its clean part: silent: " + Heard);
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "press 3 in its clean part");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Frames(1);
            Assert.That(Count(PlaceholderSound.ChainTop), Is.EqualTo(0), "not clean all through: no top note: " + Heard);
            Assert.That(_played.Where(r => r.Sound == PlaceholderSound.CleanTick).Select(r => r.Variant), Is.EqualTo(new[] { 1 }), "the row began again: step 1");
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_GivesTheMorningsSounds_AndNoBurst()
        {
            _sounds.Weight.On = false;
            TimingFeedback feedback = TimingFeedback.Ensure();
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(1), "the morning's hit tick: " + Heard);

            // A clean chain: no tick of its own, the clean tap's tone voiced as this morning.
            int tap = _player.SlotOf("Triple Tap");
            yield return UntilFree();
            _voiced.Clear();
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= 3; press++)
            {
                yield return Until(() => _player.RecastPressWouldBeClean, 2f);
                Assume.That(_player.RecastPressWouldBeClean, Is.True);
                Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            }
            yield return Until(() => _voiced.Any(r => r.Clip == feedback.CleanTone), 2f);
            Assert.That(_voiced.Count(r => r.Clip == feedback.CleanTone), Is.EqualTo(1), "the clean tap's tone at its shot");
            yield return UntilNoChain();

            EnemyShooter shooter = OneToKill();
            yield return Frames(2);
            yield return ShootToKill(shooter);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(1), "the enemy's death thump: " + Heard);
            PlaceholderSound[] mine =
            {
                PlaceholderSound.HitBasic, PlaceholderSound.HitHeavy, PlaceholderSound.HitCrit, PlaceholderSound.HitCleanEdge, PlaceholderSound.Kill,
                PlaceholderSound.RoomWon, PlaceholderSound.CleanTick, PlaceholderSound.ChainTop, PlaceholderSound.OffTick,
            };
            Assert.That(_played.Where(r => mine.Contains(r.Sound)), Is.Empty, "none of work order 080's: " + Heard);
            Assert.That(_sounds.Bursts, Is.Null, "no burst was made");
            Assert.That(PlaceholderSounds.CleanToneTaken, Is.False);
        }

        // ---------------- Nothing per frame ----------------

        [UnityTest]
        public IEnumerator WhileNothingHappens_NothingIsAllocatedPerFrame_WithTheWeightOn()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            TimingFeedback.Ensure();
            EnemyShooter shooter = OneToKill();
            yield return Frames(2);
            yield return ShootToKill(shooter); // a kill: the burst's pool is made
            yield return new WaitForSeconds(_sounds.Weight.Kills.BurstSeconds * _sounds.Weight.Kills.RoomWonScale + 0.1f);
            yield return Frames(3);
            Assume.That(_sounds.Bursts, Is.Not.Null);
            Assert.That(_sounds.Bursts.enabled, Is.False, "the bursts rest while none lives");
            _sounds.Tick();
            _played.Clear();
            Assert.That(() => { for (int i = 0; i < 300; i++) _sounds.Tick(); }, NotAllocating, "a frame of the sounds allocates nothing");
            Assert.That(_played, Is.Empty, "nothing asked for in those frames: " + Heard);
        }

        static byte[] s_junk;
    }
}
