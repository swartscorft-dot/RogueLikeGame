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

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 021 (2026-11-20), the placeholder sounds, in the ControlRoom scene, through the intents and the ways the
    /// other tests make enemies act (the practice room's spawns, hurting them, moving her): each moment asks for its
    /// sound once, and none when it did not happen; a red windup and a blue one ask for different sounds; twenty hits in
    /// one frame ask for one; nothing new starts while paused or while a choice is up, except the menu's tick and the
    /// choice-taken chord; the off value silences all; a clip under the Resources path replaces its synthesised sound;
    /// and a fight deals and takes the same damage in the same time with the sounds on and off. All through what was
    /// asked to play (<see cref="PlaceholderSounds.Requested"/>, and <see cref="ShotSounds.Requested"/> for the voices), as
    /// the sound tests do: nobody but Kristian can hear whether they sound right.
    ///
    /// The practice brute is switched off unless a test needs it, so it does not join in. Waits are in frames, game time
    /// or real time with a limit, never frames of a supposed length.
    /// </summary>
    public class PlaceholderSoundTests
    {
        const ulong RunSeed = 0x12345678UL;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        EnemyBrute _brute;
        GeneratedLevels _levels;
        PlaceholderSounds _sounds;
        RunHud _hud;
        Func<string, AudioClip> _recordedBefore;
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>();
        readonly List<ShotSoundRequest> _voiced = new List<ShotSoundRequest>();
        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _recordedBefore = PlaceholderSounds.RecordedClips;
            PlaceholderSounds.Requested += Record;
            ShotSounds.Requested += Voice;
            yield return Load();
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            _played.Clear();
            _voiced.Clear();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_brute, Is.Not.Null, "the practice brute");
            _brute.enabled = false; // it joins in only where a test asks it to
            if (_levels == null) _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            ShotSounds.Requested -= Voice;
            PlaceholderSounds.RecordedClips = _recordedBefore;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _played.Clear();
            _voiced.Clear();
        }

        void Record(PlaceholderSoundRequest r) => _played.Add(r);
        void Voice(ShotSoundRequest r) => _voiced.Add(r);

        // ---------------- Helpers ----------------

        int Count(PlaceholderSound sound) => _played.Count(r => r.Sound == sound);
        int CountIn(PlaceholderSound sound, int frame) => _played.Count(r => r.Sound == sound && r.Frame == frame);
        PlaceholderSoundRequest Last(PlaceholderSound sound) => _played.Last(r => r.Sound == sound);
        string Heard => string.Join(", ", _played.Select(r => r.Sound + "@" + r.Frame));

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Frames until the condition holds or 600 have gone: frames, so a paused game still counts.</summary>
        static IEnumerator FramesUntil(Func<bool> condition)
        {
            for (int i = 0; i < 600 && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Until the condition holds, noting the frame it first held in; the frame's own end follows (the sounds go there).</summary>
        static IEnumerator Watch(Func<bool> condition, float timeoutSeconds, Action<int> atFrame)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition())
                {
                    atFrame(Time.frameCount);
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            atFrame(-1);
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        float BasicVolume => _player.ShotLooks != null && _player.ShotLooks.Basic != null
            ? _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume : 1f;

        T SpawnOne<T>(EnemyKind kind) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            return (T)made[0];
        }

        /// <summary>The practice brute on, her this far from its home, toward where she stands.</summary>
        void BruteAt(float distance)
        {
            _brute.enabled = true;
            Vector3 home = _brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * distance);
        }

        // ---------------- Her hits, her dash, her pain ----------------

        [UnityTest]
        public IEnumerator AHit_AsksForTheHitTickOnce_ThroughItsVoices_AMiss_ForNone()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(new Vector3(-10f, 0f, -10f)); // open floor: a miss
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return Frames(3);
            Assert.That(_player.BasicShotHits, Is.EqualTo(0), "a miss");
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(0), "no tick for a miss: " + Heard);

            yield return UntilFree();
            yield return For(0.5f);
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            yield return Frames(3);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "a hit");
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(1), "one tick: " + Heard);
            PlaceholderSoundRequest tick = Last(PlaceholderSound.EnemyHit);
            Assert.That(tick.Clip, Is.SameAs(_sounds.ClipOf(PlaceholderSound.EnemyHit)));
            Assert.That(tick.Recorded, Is.False, "no recorded clip exists today: the synthesised one");
            Assert.That(tick.Volume, Is.EqualTo(_sounds.Values.Volume(PlaceholderSound.EnemyHit) * BasicVolume).Within(1e-4f), "its share of the basic shot's volume");
            Assert.That(tick.Volume, Is.LessThan(BasicVolume), "quieter than the gun");
            Assert.That(tick.Pitch, Is.InRange(1f - _sounds.Values.PitchSpread - 1e-4f, 1f + _sounds.Values.PitchSpread + 1e-4f));
            Assert.That(_voiced.Count(v => v.Clip == tick.Clip), Is.EqualTo(1), "played through its own voices, once");
            Assert.That(tick.Clip.length, Is.EqualTo(0.05f).Within(0.002f));
        }

        [UnityTest]
        public IEnumerator SheIsHurt_AsksForTheCrack_LouderForABiggerHit_AndHerDeathOnce()
        {
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(1), Heard);
            float small = Last(PlaceholderSound.Hurt).Volume;
            Assert.That(_health.TakeHit(30), Is.EqualTo(30));
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(2));
            float big = Last(PlaceholderSound.Hurt).Volume;
            Assert.That(big, Is.GreaterThan(small), "louder for a bigger hit");
            Assert.That(big, Is.EqualTo(_sounds.Values.Volume(PlaceholderSound.Hurt) * BasicVolume).Within(1e-4f), "a third of her health: full");
            Assert.That(Count(PlaceholderSound.HerDeath), Is.EqualTo(0), "alive");

            _health.TakeHit(100000);
            yield return Frames(2);
            Assert.That(_health.IsAlive, Is.False);
            Assert.That(Count(PlaceholderSound.HerDeath), Is.EqualTo(1), "her death, once: " + Heard);
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(3), "and the hit that killed her");
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.HerDeath), Is.EqualTo(1), "still once");
        }

        [UnityTest]
        public IEnumerator ADash_AsksForTheWhooshOnce_ARejectedOne_ForNone()
        {
            yield return null;
            Assert.That(_player.DashToward(_player.Position + Vector3.right * 5f), Is.EqualTo(IntentResult.Started));
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Dash), Is.EqualTo(1), Heard);
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 5f), Is.EqualTo(IntentResult.Rejected), "dashing already, or on cooldown");
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.Dash), Is.EqualTo(1), "none for a rejected dash");
        }

        [UnityTest]
        public IEnumerator HerFocusFilled_AsksForTheChimeOnce()
        {
            Assume.That(_player.IsFocusFull, Is.False);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.FocusFull), Is.EqualTo(0));
            _player.FillFocus();
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.FocusFull), Is.EqualTo(1), Heard);
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.FocusFull), Is.EqualTo(1), "once, while it stays full");
        }

        // ---------------- Enemies ----------------

        [UnityTest]
        public IEnumerator AnEnemyKilled_AsksForItsDeathOnce_AHitThatLeavesItAlive_ForNone()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            shooter.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            shooter.Body.TakeHit(1);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(0), "alive");
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(0), "not her hit: no tick");
            shooter.Body.TakeHit(100000);
            yield return Frames(3);
            Assert.That(shooter.IsDead, Is.True);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(1), Heard);
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.EnemyDies), Is.EqualTo(1), "once");
        }

        [UnityTest]
        public IEnumerator ARedWindupAndABlueOne_AskForDifferentSounds_EachAtItsStart()
        {
            // Blue: the brute's Shockwave, counterable (work order 015).
            BruteAt(7f);
            int blueAt = -1;
            yield return Watch(() => _brute.State == EnemyState.Windup, 6f, f => blueAt = f);
            Assume.That(blueAt, Is.GreaterThan(0), "the brute winds up");
            Assume.That(_brute.Attack?.Name, Is.EqualTo("Shockwave"));
            Assert.That(_brute.TelegraphIsCounterable, Is.True, "drawn blue");
            Assert.That(CountIn(PlaceholderSound.WindupBlue, blueAt), Is.EqualTo(1), "the blue windup at its start: " + Heard);
            Assert.That(Count(PlaceholderSound.WindupRed), Is.EqualTo(0), "not the red");
            _brute.enabled = false;
            _player.Teleport(Stand);
            yield return Frames(2);

            // Red: a swarmer's Lunge.
            List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
            yield return Frames(2);
            pack[0].Body.TakeHit(1);
            int redAt = -1;
            yield return Watch(() => pack.Any(e => e.State == EnemyState.Windup), 10f, f => redAt = f);
            Assume.That(redAt, Is.GreaterThan(0), "a swarmer winds up");
            Assert.That(PoolRules.IsCounterable(pack.First(e => e.State == EnemyState.Windup).Attack), Is.False, "a red attack");
            Assert.That(CountIn(PlaceholderSound.WindupRed, redAt), Is.EqualTo(1), "the red windup at its start, one however many began: " + Heard);
            Assert.That(Count(PlaceholderSound.WindupBlue), Is.EqualTo(1), "no blue for it");
            Assert.That(_sounds.ClipOf(PlaceholderSound.WindupRed), Is.Not.SameAs(_sounds.ClipOf(PlaceholderSound.WindupBlue)), "different sounds");
        }

        [UnityTest]
        public IEnumerator AStrike_AsksForTheImpact_WhetherOrNotItHits_TheShootersShot_ForTheArrow()
        {
            BruteAt(7f);
            int struckAt = -1;
            yield return Watch(() => _brute.Strikes > 0, 8f, f => struckAt = f);
            Assume.That(struckAt, Is.GreaterThan(0), "the brute struck");
            Assert.That(CountIn(PlaceholderSound.Strike, struckAt), Is.EqualTo(1), Heard);
            Assert.That(Count(PlaceholderSound.Arrow), Is.EqualTo(0));
            _brute.enabled = false;
            _player.Teleport(Stand);
            yield return Frames(2);

            int strikes = Count(PlaceholderSound.Strike);
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            yield return null;
            shooter.Body.TakeHit(1);
            int shotAt = -1;
            yield return Watch(() => shooter.Strikes > 0, 6f, f => shotAt = f);
            Assume.That(shotAt, Is.GreaterThan(0), "the shooter shot");
            Assert.That(CountIn(PlaceholderSound.Arrow, shotAt), Is.EqualTo(1), "its arrow let go: " + Heard);
            Assert.That(Count(PlaceholderSound.Strike), Is.EqualTo(strikes), "not the heavy impact");
        }

        [UnityTest]
        public IEnumerator ADaze_AsksForTheCrackAndRingOnce_ABarNotEmptied_ForNone()
        {
            var guard = SpawnOne<EnemyGuard>(EnemyKind.Guard);
            guard.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            guard.Body.TakeHit(1, guard.Tuning.Stagger.Max * 0.3f);
            yield return Frames(3);
            Assert.That(guard.IsDazed, Is.False);
            Assert.That(Count(PlaceholderSound.Dazed), Is.EqualTo(0));
            guard.Body.TakeHit(1, guard.Tuning.Stagger.Max);
            yield return Frames(2);
            Assert.That(guard.IsDazed, Is.True);
            Assert.That(Count(PlaceholderSound.Dazed), Is.EqualTo(1), Heard);
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.Dazed), Is.EqualTo(1), "once");
        }

        [UnityTest]
        public IEnumerator TwentyHitsInOneFrame_AskForOneHitTick_ALittleLouder()
        {
            var pack = new List<Enemy>();
            for (int i = 0; i < 4; i++) pack.AddRange(_levels.SpawnPractice(EnemyKind.Swarmer));
            Assert.That(pack.Count, Is.EqualTo(20), "twenty swarmers");
            foreach (Enemy e in pack)
            {
                var s = (EnemySwarmer)e;
                s.Tuning.Enemy.MoveSpeed = 0f;
                s.Tuning.Spacing = 0f; // they stand where they came
            }
            Vector3 middle = Vector3.zero;
            foreach (Enemy e in pack) middle += e.transform.position;
            middle /= pack.Count;
            middle.y = 0f;
            Vector3 back = Vector3.ProjectOnPlane(Stand - middle, Vector3.up).normalized;
            _player.Teleport(middle + back * 7f);
            yield return Frames(3);

            var hitsByFrame = new Dictionary<int, int>();
            _player.Reported += r =>
            {
                if (r.Kind != PlayerReportKind.Hit || r.Damage <= 0) return;
                hitsByFrame.TryGetValue(Time.frameCount, out int n);
                hitsByFrame[Time.frameCount] = n + 1;
            };
            _played.Clear();
            Assert.That(_player.PressSkill(Slot("Powder Keg"), middle), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsBurst > 0, 5f);
            yield return Frames(2);
            Assert.That(_player.KegsBurst, Is.EqualTo(1), "the keg burst on its fuse");
            Assert.That(hitsByFrame.Count, Is.EqualTo(1), "its hits all in one frame");
            int frame = hitsByFrame.Keys.Single();
            Assert.That(hitsByFrame[frame], Is.GreaterThanOrEqualTo(20), "twenty hits");
            Assert.That(CountIn(PlaceholderSound.EnemyHit, frame), Is.EqualTo(1), "one tick for them all: " + Heard);
            PlaceholderSoundRequest tick = _played.Single(r => r.Sound == PlaceholderSound.EnemyHit);
            Assert.That(tick.Asked, Is.EqualTo(hitsByFrame[frame]));
            Assert.That(tick.Volume, Is.EqualTo(_sounds.Values.Volume(PlaceholderSound.EnemyHit) * BasicVolume * _sounds.Values.MergeMost).Within(1e-4f),
                        "a little louder, no more than the cap");
        }

        // ---------------- The boss ----------------

        EnemyBoss SpawnBoss()
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Boss);
            Assert.That(made.Count, Is.EqualTo(1), "one boss");
            return (EnemyBoss)made[0];
        }

        [UnityTest]
        public IEnumerator TheBoss_AsksForItsCallAndItsAnger_EachOnce_AtItsMoment()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f)); // under two thirds: a call
            yield return Frames(3);
            Assert.That(boss.PacksCalled, Is.EqualTo(1));
            Assert.That(Count(PlaceholderSound.BossCall), Is.EqualTo(1), Heard);
            Assert.That(Count(PlaceholderSound.BossAnger), Is.EqualTo(0), "not angry yet");

            boss.Body.TakeHit(boss.Body.CurrentHealth - Mathf.FloorToInt(max * 0.45f)); // under half: angry, not yet a third
            yield return Frames(3);
            Assert.That(boss.IsAngry, Is.True);
            Assert.That(boss.PacksCalled, Is.EqualTo(1));
            Assert.That(Count(PlaceholderSound.BossAnger), Is.EqualTo(1), Heard);
            Assert.That(Count(PlaceholderSound.BossCall), Is.EqualTo(1), "still one call");
            yield return Frames(5);
            Assert.That(Count(PlaceholderSound.BossAnger), Is.EqualTo(1), "once");
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator TheBoss_AsksForTheRumble_WhenItsChargeBeginsToRun()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.CallsAt = new float[0]; // no packs in the way here
            yield return Frames(2);
            boss.Body.TakeHit(1);
            int windup = -1;
            yield return Watch(() => boss.State == EnemyState.Windup && boss.Attack != null && boss.Attack.Name == BossValues.Charge, 8f, f => windup = f);
            Assume.That(windup, Is.GreaterThan(0), "a charge at her 12 m away");
            Assert.That(CountIn(PlaceholderSound.WindupBlue, windup), Is.EqualTo(1), "the charge is a blue windup");
            Assert.That(Count(PlaceholderSound.BossCharge), Is.EqualTo(0), "not running yet");
            int ran = -1;
            yield return Watch(() => boss.IsCharging, 3f, f => ran = f);
            Assert.That(ran, Is.GreaterThan(0), "it runs");
            Assert.That(CountIn(PlaceholderSound.BossCharge, ran), Is.EqualTo(1), Heard);
            Assert.That(Count(PlaceholderSound.BossCharge), Is.EqualTo(1));
            _levels.ClearSpawned();
        }

        // ---------------- The run ----------------

        GeneratedLevel.PlacedProp FirstChest() => _levels.Chests().First();

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
        public IEnumerator AChest_AsksForTheArpeggioOnce_NothingElseWhileItsChoiceIsUp_AndThePick_ForTheChord()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return StandBy(FirstChest());
            _played.Clear();
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1), "heard, though its choice is up at once: " + Heard);

            // The choice is up: nothing new starts.
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            _player.FillFocus();
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(0), "not while a choice is up");
            Assert.That(Count(PlaceholderSound.FocusFull), Is.EqualTo(0));

            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(1), "the pick: " + Heard);
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1), "once");
            Assert.That(_levels.OpenChest(FirstChest()), Is.False);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1), "an opened chest does not open again");
        }

        [UnityTest]
        public IEnumerator EachFloor_AsksForTheThreeNotes_EachPick_ForTheChord_AndTheLastFloor_ForTheFanfare()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            for (int floor = 1; floor <= 4; floor++)
            {
                _levels.FinishFloorForTests();
                yield return FramesUntil(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                yield return Frames(2);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "floor " + floor);
                Assert.That(Count(PlaceholderSound.FloorCleared), Is.EqualTo(floor), "floor " + floor + " cleared, heard with its reward up: " + Heard);
                Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(floor - 1));
                Assert.That(_levels.PickReward(0), Is.True);
                yield return Frames(2);
                Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(floor), "the pick, floor " + floor);
                // The floors go by far faster than she could clear them: past the cap of two a second on the notes.
                yield return new WaitForSecondsRealtime(0.6f);
            }
            Assert.That(Count(PlaceholderSound.ActCleared), Is.EqualTo(0));
            _levels.FinishFloorForTests();
            yield return FramesUntil(() => _levels.ActCleared);
            yield return Frames(2);
            Assert.That(_levels.ActCleared, Is.True);
            Assert.That(Count(PlaceholderSound.ActCleared), Is.EqualTo(1), "the fanfare: " + Heard);
            Assert.That(Count(PlaceholderSound.FloorCleared), Is.EqualTo(4), "not the three notes as well");
            Assert.That(Count(PlaceholderSound.BossCharge) + Count(PlaceholderSound.BossCall), Is.EqualTo(0), "the boss died at once");
            _levels.BackToPracticeRoom();
            yield return null;
        }

        // ---------------- The pause ----------------

        [UnityTest]
        public IEnumerator ThePauseMenu_TicksAsItOpensAndCloses_AndNothingElseStartsWhileItIsUp()
        {
            _hud = RunHud.Ensure();
            yield return Frames(2);
            _played.Clear();
            Assert.That(_hud.Escape(), Is.True, "paused");
            Assert.That(_levels.Paused, Is.True);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Menu), Is.EqualTo(1), "the tick as it opens: " + Heard);

            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            _player.FillFocus();
            yield return Frames(4);
            Assert.That(_played.Where(r => r.Sound != PlaceholderSound.Menu), Is.Empty, "nothing else while paused: " + Heard);
            Assert.That(_voiced.Count(v => v.Clip == _sounds.ClipOf(PlaceholderSound.Hurt)), Is.EqualTo(0), "not played either");

            Assert.That(_hud.Escape(), Is.True, "resumed");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Menu), Is.EqualTo(2), "the tick as it closes");
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(1), "and the game sounds again");
        }

        [UnityTest]
        public IEnumerator AHitStop_IsNotAPause()
        {
            Time.timeScale = 0f; // as a hit stop holds it, with no menu and no choice up
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            yield return Frames(2);
            Time.timeScale = 1f;
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(1), Heard);
        }

        // ---------------- Off, and replaced ----------------

        [UnityTest]
        public IEnumerator TheOffValue_SilencesAll()
        {
            _sounds.Values.On = false;
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            _health.TakeHit(5);
            _player.FillFocus();
            yield return UntilFree();
            _player.DashToward(_player.Position + Vector3.right * 5f);
            _hud = RunHud.Ensure();
            yield return null;
            _hud.Escape();
            yield return Frames(2);
            _hud.Escape();
            yield return Frames(3);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the shot hit");
            Assert.That(_played, Is.Empty, "nothing asked for: " + Heard);
            foreach (PlaceholderSound s in PlaceholderSoundRules.All)
                Assert.That(_voiced.Count(v => v.Clip == _sounds.ClipOf(s)), Is.EqualTo(0), s + " not played");
        }

        [UnityTest]
        public IEnumerator ARecordedClipUnderTheResourcesPath_IsPlayedInPlaceOfItsSynthesisedSound()
        {
            // Kristian records his own sounds one by one (the first, the boss's roar, on 2026-11-20): a sound is
            // recorded exactly when a clip lies under its name. Below, the test supplies its own clips, so the real
            // files do not matter to it.
            foreach (PlaceholderSound s in PlaceholderSoundRules.All)
                Assert.That(_sounds.IsRecorded(s), Is.EqualTo(Resources.Load<AudioClip>(PlaceholderSoundRules.ResourcePath(s)) != null),
                    s + ": recorded exactly when a clip lies under its name");
            Object.Destroy(_sounds.gameObject);
            yield return null;
            Assert.That(PlaceholderSounds.Current, Is.Null);

            // A clip made here stands for a file at Resources/Sounds/Placeholders/Hurt.
            var recorded = AudioClip.Create("RecordedHurt", 4410, 1, 44100, false);
            var data = new float[4410];
            for (int i = 0; i < data.Length; i++) data[i] = 0.5f * Mathf.Sin(i * 0.05f) * (1f - i / (float)data.Length);
            recorded.SetData(data, 0);
            _made.Add(recorded);
            var looked = new List<string>();
            PlaceholderSounds.RecordedClips = path =>
            {
                looked.Add(path);
                return path == "Sounds/Placeholders/Hurt" ? recorded : null;
            };
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            Assert.That(looked, Is.EquivalentTo(PlaceholderSoundRules.All.Select(s => "Sounds/Placeholders/" + s)), "each sound looked for under its name");
            Assert.That(_sounds.IsRecorded(PlaceholderSound.Hurt), Is.True);
            Assert.That(_sounds.ClipOf(PlaceholderSound.Hurt), Is.SameAs(recorded));
            Assert.That(_sounds.IsRecorded(PlaceholderSound.Strike), Is.False, "the others stay synthesised");

            _played.Clear();
            _health.TakeHit(5);
            yield return Frames(2);
            PlaceholderSoundRequest hurt = _played.Single(r => r.Sound == PlaceholderSound.Hurt);
            Assert.That(hurt.Clip, Is.SameAs(recorded), "the recorded clip played");
            Assert.That(hurt.Recorded, Is.True);
            Assert.That(_voiced.Count(v => v.Clip == recorded), Is.EqualTo(1));
            Object.Destroy(_sounds.gameObject);
            _sounds = null;
            yield return null;
            Assert.That(recorded != null, Is.True, "a recorded clip is not destroyed with the sounds");
        }

        // ---------------- Which report is which moment ----------------

        [Test]
        public void EachEnemyReport_IsItsMoment_OrNone()
        {
            var table = new Dictionary<EnemyReportKind, SoundMoment?>
            {
                { EnemyReportKind.Woke, null }, { EnemyReportKind.AttackStarted, SoundMoment.WindupStarted },
                { EnemyReportKind.Struck, SoundMoment.EnemyStruck }, { EnemyReportKind.GaveUp, null },
                { EnemyReportKind.Died, SoundMoment.EnemyDied }, { EnemyReportKind.Removed, null },
                { EnemyReportKind.StaggerTaken, null }, { EnemyReportKind.Dazed, SoundMoment.EnemyDazed },
                { EnemyReportKind.DazeEnded, null }, { EnemyReportKind.Charged, SoundMoment.BossCharged },
                { EnemyReportKind.CalledPack, SoundMoment.BossCalled }, { EnemyReportKind.Angered, SoundMoment.BossAngered },
            };
            foreach (EnemyReportKind kind in Enum.GetValues(typeof(EnemyReportKind)))
            {
                Assert.That(table.ContainsKey(kind), Is.True, kind + " is in the table");
                Assert.That(PlaceholderSounds.MomentOf(kind), Is.EqualTo(table[kind]), kind.ToString());
            }
            Assert.That(PlaceholderSounds.MomentOf(new PlayerReport { Kind = PlayerReportKind.Hit, Damage = 40 }), Is.EqualTo(SoundMoment.EnemyHit));
            Assert.That(PlaceholderSounds.MomentOf(new PlayerReport { Kind = PlayerReportKind.Hit, Damage = 0 }), Is.Null, "no damage, no tick");
            Assert.That(PlaceholderSounds.MomentOf(new PlayerReport { Kind = PlayerReportKind.DashStarted }), Is.EqualTo(SoundMoment.Dashed));
            Assert.That(PlaceholderSounds.MomentOf(new PlayerReport { Kind = PlayerReportKind.DashPressed }), Is.Null, "a press is not a dash");
            Assert.That(PlaceholderSounds.MomentOf(new PlayerReport { Kind = PlayerReportKind.ShotFired }), Is.Null, "the gun has its own sound");
        }

        // ---------------- Nothing changes in the fight ----------------

        struct Fight
        {
            public List<int> Dealt;
            public List<int> Taken;
            public List<float> Leads;
            public float TakenAfter;
        }

        IEnumerator FightOnce(Action<Fight> got)
        {
            var fight = new Fight { Dealt = new List<int>(), Taken = new List<int>(), Leads = new List<float>(), TakenAfter = -1f };
            _a.Damaged += d => fight.Dealt.Add(d);
            _health.Damaged += d => fight.Taken.Add(d);
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(3, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            foreach (string name in new[] { "Quick Shot", "Scatter Blast", "Long Shot" })
            {
                int slot = Slot(name);
                int before = fight.Dealt.Count;
                float pressed = Time.time;
                Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), name);
                if (_player.Skills[slot].Input == SkillInput.Charge)
                {
                    SkillDefinition s = _player.Skills[slot];
                    yield return Until(() => _player.SkillChargedSeconds >= 0.5f * (s.PerfectFrom + s.PerfectTo), 5f);
                    pressed = Time.time;
                    _player.ReleaseSkill(slot);
                }
                yield return Until(() => fight.Dealt.Count > before, 3f);
                fight.Leads.Add(Time.time - pressed);
                yield return UntilFree();
            }
            // She stands 7 m from the brute: its Shockwave lands on her.
            BruteAt(7f);
            float from = Time.time;
            yield return Until(() => fight.Taken.Count > 0, 8f);
            fight.TakenAfter = fight.Taken.Count > 0 ? Time.time - from : -1f;
            _brute.enabled = false;
            got(fight);
        }

        [UnityTest]
        public IEnumerator AFight_DealsAndTakesTheSameDamage_InTheSameTime_WithTheSoundsOnAndOff()
        {
            Fight on = default, off = default;
            yield return FightOnce(f => on = f);
            Assert.That(_played.Count, Is.GreaterThan(0), "the sounds were at work: " + Heard);

            Object.Destroy(_sounds.gameObject);
            _sounds = null;
            yield return null;
            yield return Load();
            Assert.That(PlaceholderSounds.Current, Is.Null, "off");
            yield return FightOnce(f => off = f);

            Debug.Log($"[021] on: dealt {string.Join(", ", on.Dealt)}, taken {string.Join(", ", on.Taken)} after {on.TakenAfter:0.000} s;"
                      + $" off: dealt {string.Join(", ", off.Dealt)}, taken {string.Join(", ", off.Taken)} after {off.TakenAfter:0.000} s");
            Assert.That(on.Taken.Count, Is.GreaterThan(0), "she was hit");
            Assert.That(off.Dealt, Is.EqualTo(on.Dealt), "the same damage dealt, hit for hit");
            Assert.That(off.Taken, Is.EqualTo(on.Taken), "the same damage taken");
            for (int i = 0; i < on.Leads.Count; i++)
                Assert.That(off.Leads[i], Is.EqualTo(on.Leads[i]).Within(0.05f), "the same time from press to hit, skill " + i);
            Assert.That(off.TakenAfter, Is.EqualTo(on.TakenAfter).Within(0.1f), "the brute's strike came in the same time");
        }
    }
}
