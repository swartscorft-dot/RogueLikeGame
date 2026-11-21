using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 048 (Kristian's wish of 2026-11-20 for sounds for the new things), in the ControlRoom scene through the
    /// intents and the game's own reports: an arrow into a pillar knocks once, one that hits her or flies out does not; a
    /// caster's orb leaves with its own sound; a quick duck thuds; the Shield bash's stun rings and the Drag's pull whips; a
    /// drop chimes as it lands and sweeps as it is taken, the pick's chord after; a combat room cleared opens the way, the
    /// exit room is the floor's notes; a seeded crit ticks, a clean press's crit only rings its own tone; a chest's offer
    /// with a rare card rings a bell, with an epic two; each switch off silences its sounds; a clip under a new sound's name
    /// replaces it; and nothing is allocated per frame while nothing plays. All through what was asked to play
    /// (<see cref="PlaceholderSounds.Requested"/>, <see cref="ShotSounds.Requested"/> for the voices): nobody but Kristian
    /// can hear whether they sound right.
    ///
    /// The practice brute is switched off. Waits are in frames, game time or real time with a limit, never frames of a
    /// supposed length.
    /// </summary>
    public class NewThingsSoundTests
    {
        const ulong RunSeed = 0x048048UL;
        static readonly Vector3 FlyAt = new Vector3(0f, 0f, 8f);       // where an enemy that shoots stands (the fly tests')
        static readonly Vector3 MeleeAt = new Vector3(8f, 0f, 4f);     // where a melee enemy stands (the melee tests')
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f);       // Dummy_A is 2.3 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        Damageable _a;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        PlaceholderSounds _sounds;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas, _quickDuckWas;
        Func<string, AudioClip> _recordedBefore;
        readonly List<PlaceholderSoundRequest> _played = new List<PlaceholderSoundRequest>();
        readonly List<ShotSoundRequest> _voiced = new List<ShotSoundRequest>();
        readonly List<Object> _made = new List<Object>();

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _recordedBefore = PlaceholderSounds.RecordedClips;
            PlaceholderSounds.Requested += Record;
            ShotSounds.Requested += Voice;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _quickDuckWas = _feel.QuickDuck;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice brute");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            _played.Clear();
            _voiced.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= Record;
            ShotSounds.Requested -= Voice;
            PlaceholderSounds.RecordedClips = _recordedBefore;
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
                _feel.QuickDuck = _quickDuckWas;
            }
            if (_player != null && (_levels == null || _player.Modifiers != _levels.Modifiers)) _player.Modifiers = null; // those put on by hand
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
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
        string Heard => string.Join(", ", _played.Select(r => r.Sound + "@" + r.Frame));

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

        static IEnumerator GameTime(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        /// <summary>The dummies out of the way of what flies, as the fly tests put them.</summary>
        void DummiesAway()
        {
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) d.transform.position = new Vector3(-17f + 3f * k++, d.transform.position.y, 18f);
            }
            if (_practiceEnemy.TryGetComponent(out NavMeshAgent agent)) agent.Warp(new Vector3(-17f, 0f, -17f));
        }

        /// <summary>One enemy of a kind from the practice room's intent, unable to walk, put at <paramref name="at"/>.</summary>
        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        /// <summary>A melee enemy made from the practice brute where it stands, unable to walk, as the melee tests make them.</summary>
        T Melee<T>(EnemyKind kind, Vector3 at) where T : MeleeEnemy
        {
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
            Enemy e = EnemyFactory.Make(kind, at, null, _practiceEnemy, "Melee_" + kind);
            Assert.That(e, Is.Not.Null, kind + " made");
            _made.Add(e.gameObject);
            if (e is EnemyBrute brute)
            {
                brute.FloorValues = new LevelValues().FloorBruteValues(brute.Settings != null ? brute.Settings.Values : null);
                brute.enabled = true;
            }
            e.Values.MoveSpeed = 0f;
            return (T)e;
        }

        void StandSouthOf(Enemy e, float distance) => _player.Teleport(Flat(e.transform.position) + Vector3.back * distance);

        /// <summary>A prop as a level builds it: cover when it is tall enough (low under the duck's sort), something to duck behind unless slender.</summary>
        GameObject Prop(PropModel model, Vector3 at, Vector3 faceToward)
        {
            Vector3 toHer = Flat(faceToward - at);
            int rotation = toHer.sqrMagnitude > 1e-6f ? (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360 : 0;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        /// <summary>The strikes an enemy reports, in order.</summary>
        static List<EnemyReport> Strikes(Enemy e)
        {
            var list = new List<EnemyReport>();
            e.Reported += r => { if (r.Kind == EnemyReportKind.Struck) list.Add(r); };
            return list;
        }

        /// <summary>A shooter 12 m from her draws; a pillar is put between them before the release, or not. Returns its first strike.</summary>
        IEnumerator ShooterLooses(bool pillar, Action<EnemyReport> got)
        {
            EnemyShooter shooter = Spawn<EnemyShooter>(EnemyKind.Shooter, FlyAt);
            List<EnemyReport> strikes = Strikes(shooter);
            StandSouthOf(shooter, pillar ? 12f : 8f);
            shooter.Provoke();
            yield return Until(() => shooter.State == EnemyState.Windup, 5f);
            Assume.That(shooter.State, Is.EqualTo(EnemyState.Windup), "it draws at her");
            if (pillar)
            {
                GameObject p = Prop(PropModel.Pillar, Flat(shooter.transform.position) + Vector3.back * 7f, _player.Position);
                Assert.That(p.GetComponent<Cover>(), Is.Not.Null, "a tall prop");
                Physics.SyncTransforms();
            }
            yield return Until(() => strikes.Count > 0, 6f);
            Assume.That(strikes.Count, Is.GreaterThan(0), "its arrow ended");
            got(strikes[0]);
            _levels.ClearSpawned();
            yield return Frames(2); // the sounds go at the frame's end
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            yield return null;
        }

        // ---------------- What flies ----------------

        [UnityTest]
        public IEnumerator AnArrowIntoAPillar_KnocksOnce_AnArrowThatHitsHer_DoesNot()
        {
            DummiesAway();
            yield return null;
            EnemyReport end = default;
            yield return ShooterLooses(true, r => end = r);
            Assert.That(end.Flown, Is.True);
            Assert.That(end.Outcome, Is.EqualTo(StrikeOutcome.StoppedByWall), "the pillar stopped it");
            Assert.That(Count(PlaceholderSound.Arrow), Is.EqualTo(1), "heard leaving, as before: " + Heard);
            Assert.That(Count(PlaceholderSound.ProjectileStopped), Is.EqualTo(1), "and knocking on the pillar, once: " + Heard);
            PlaceholderSoundRequest knock = _played.Single(r => r.Sound == PlaceholderSound.ProjectileStopped);
            Assert.That(knock.Clip, Is.SameAs(_sounds.ClipOf(PlaceholderSound.ProjectileStopped)));
            Assert.That(knock.Clip.length, Is.EqualTo(0.08f).Within(0.002f));
            Assert.That(_voiced.Count(v => v.Clip == knock.Clip), Is.EqualTo(1), "played through its voices, once");

            _played.Clear();
            yield return ShooterLooses(false, r => end = r);
            Assert.That(end.Outcome, Is.EqualTo(StrikeOutcome.Landed), "standing at 8 m she is hit");
            Assert.That(Count(PlaceholderSound.Hurt), Is.EqualTo(1), "her hurt: " + Heard);
            Assert.That(Count(PlaceholderSound.ProjectileStopped), Is.EqualTo(0), "no knock for one that met her");
        }

        [UnityTest]
        public IEnumerator ACastersOrb_LeavesWithItsOwnSound_NotTheStrike()
        {
            DummiesAway();
            yield return null;
            EnemyCaster caster = Spawn<EnemyCaster>(EnemyKind.Caster, FlyAt);
            StandSouthOf(caster, 10f);
            caster.Provoke();
            int fired = _fly.Fired;
            yield return Until(() => caster.Strikes > 0 || _fly.Fired > fired, 8f);
            Assume.That(_fly.Fired, Is.GreaterThan(fired), "an orb flies");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.OrbLoosed), Is.EqualTo(1), "its orb let go: " + Heard);
            Assert.That(Count(PlaceholderSound.Strike), Is.EqualTo(0), "not the heavy impact it had before");
            Assert.That(Count(PlaceholderSound.Arrow), Is.EqualTo(0));
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator WithWhatFliesOff_NeitherTheKnockNorTheOrbsSound()
        {
            DummiesAway();
            _fly.Values.On = false;
            yield return null;
            EnemyReport end = default;
            yield return ShooterLooses(true, r => end = r);
            Assert.That(end.Flown, Is.False, "the shooter's old line, nothing flew");
            Assert.That(Count(PlaceholderSound.Arrow), Is.EqualTo(1), "its shot heard as before: " + Heard);

            EnemyCaster caster = Spawn<EnemyCaster>(EnemyKind.Caster, FlyAt);
            StandSouthOf(caster, 10f);
            caster.Provoke();
            yield return Until(() => caster.Strikes > 0, 8f);
            Assume.That(caster.Strikes, Is.GreaterThan(0), "it cast its circles");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.ProjectileStopped), Is.EqualTo(0), "no knock: " + Heard);
            Assert.That(Count(PlaceholderSound.OrbLoosed), Is.EqualTo(0), "no orb");
            _levels.ClearSpawned();
        }

        // ---------------- Her: the duck, the stun, the pull ----------------

        [UnityTest]
        public IEnumerator AQuickDuck_ThudsOnce_NotTheDash_AndWithTheDuckOff_None()
        {
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
            _feel.QuickDuck = true;
            DummiesAway();
            Vector3 stand = Vector3.zero; // the duck tests' place
            _player.Teleport(stand);
            yield return null;
            PropSize size = PropSize.Of(PropModel.Sarcophagus);
            Prop(PropModel.Sarcophagus, stand + Vector3.right * (0.5f + (float)size.Depth * 0.5f), stand);
            Physics.SyncTransforms();
            yield return Frames(2);
            _played.Clear();

            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "beside a low prop: down");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Duck), Is.EqualTo(1), "the thud: " + Heard);
            Assert.That(Count(PlaceholderSound.Dash), Is.EqualTo(0), "not the dash's whoosh");
            Assert.That(_sounds.ClipOf(PlaceholderSound.Duck), Is.Not.SameAs(_sounds.ClipOf(PlaceholderSound.Dash)));
            yield return Until(() => !_player.IsQuickDucking, 2f);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.Duck), Is.EqualTo(1), "once: rising is silent");

            yield return GameTime(_feel.DuckWait + 0.1f);
            _feel.QuickDuck = false;
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Off), "the duck off");
            _player.HoldCover(true);
            yield return Frames(3);
            _player.HoldCover(false);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Duck), Is.EqualTo(1), "no thud with the quick duck off: " + Heard);
        }

        [UnityTest]
        public IEnumerator AShieldBash_StunsHerWithTheDullRing_ADrag_PullsHerWithTheWhip()
        {
            Assume.That(_player.MeleeYouRead, Is.True, "on by default");
            var brute = Melee<EnemyBrute>(EnemyKind.Brute, MeleeAt);
            yield return null;
            StandSouthOf(brute, 4.5f);
            brute.Provoke();
            yield return Until(() => brute.State == EnemyState.Windup && brute.Attack != null && brute.Attack.Name == MeleeAttacks.ShieldBash, 4f);
            Assume.That(brute.Attack?.Name, Is.EqualTo(MeleeAttacks.ShieldBash));
            yield return Until(() => _player.IsStunned, 3f);
            Assume.That(_player.IsStunned, Is.True, "stunned");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Stunned), Is.EqualTo(1), "the stun's ring: " + Heard);
            Assert.That(Count(PlaceholderSound.Pulled), Is.EqualTo(0));
            Object.Destroy(brute.gameObject);
            yield return Until(() => !_player.IsStunned, 2f);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Stunned), Is.EqualTo(1), "once");

            var guard = Melee<EnemyGuard>(EnemyKind.Guard, MeleeAt);
            yield return null;
            StandSouthOf(guard, 6f);
            guard.Provoke();
            yield return Until(() => guard.State == EnemyState.Windup && guard.Attack != null && guard.Attack.Name == MeleeAttacks.Drag, 4f);
            Assume.That(guard.Attack?.Name, Is.EqualTo(MeleeAttacks.Drag));
            yield return Until(() => _player.IsPulled, 3f);
            Assume.That(_player.IsPulled, Is.True, "pulled");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.Pulled), Is.EqualTo(1), "the pull's whip: " + Heard);
            Assert.That(Count(PlaceholderSound.Stunned), Is.EqualTo(1), "no ring for a pull");
            Assert.That(_sounds.ClipOf(PlaceholderSound.Pulled).length, Is.EqualTo(0.18f).Within(0.002f));
            Assert.That(_sounds.ClipOf(PlaceholderSound.Stunned).length, Is.EqualTo(0.45f).Within(0.002f));
        }

        // ---------------- The run: the drop, the rooms, the offer ----------------

        IEnumerator StartRun()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true; // nothing here is about her health
        }

        static IEnumerator Kill(IReadOnlyList<Enemy> enemies)
        {
            List<Enemy> alive = enemies.Where(e => e != null && !e.IsDead).ToList();
            Assume.That(alive.Count, Is.GreaterThan(0));
            foreach (Enemy e in alive) e.Body.TakeHit(100000000);
            yield return null;
        }

        IEnumerator StandBy(Vector3 point)
        {
            Vector3 best = point;
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = point + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                best = new Vector3(hit.position.x, 0f, hit.position.z);
                break;
            }
            _player.Teleport(best);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADrop_ChimesAsItLands_SweepsAsItIsTaken_AndThePickIsTheChord()
        {
            yield return StartRun();
            Assume.That(_levels.DropValues.On, Is.True, "drops on by default");
            yield return Kill(_levels.Level.EliteRoomEnemies);
            yield return FramesUntil(() => _levels.EliteDrop != null);
            RewardDrop drop = _levels.EliteDrop;
            Assume.That(drop, Is.Not.Null, "a drop is down");
            Assert.That(Count(PlaceholderSound.DropLands), Is.EqualTo(0), "in the air: not yet");
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.1f);
            yield return Frames(2);
            Assert.That(drop.Rang, Is.True, "it came to rest");
            Assert.That(Count(PlaceholderSound.DropLands), Is.EqualTo(1), "its chime: " + Heard);
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(0), "no longer the chest's");

            yield return StandBy(drop.Landing);
            _played.Clear();
            Assert.That(_levels.Interact(), Is.True, "picked up");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "its choice is up");
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.DropTaken), Is.EqualTo(1), "the sweep, though its choice came up at once: " + Heard);
            Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(0), "nothing picked yet");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(1), "the pick's chord, as before");
            Assert.That(Count(PlaceholderSound.DropTaken), Is.EqualTo(1), "once");
            Assert.That(Count(PlaceholderSound.DropLands), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator WithTheDropsOff_NeitherDropSound()
        {
            yield return StartRun();
            _levels.DropValues.On = false;
            yield return Kill(_levels.Level.EliteRoomEnemies);
            yield return FramesUntil(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "the choice opens at once, as before");
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            yield return GameTime(_levels.DropValues.SettleSeconds + 0.2f);
            Assert.That(Count(PlaceholderSound.ChoiceTaken), Is.EqualTo(1), "the pick heard: " + Heard);
            Assert.That(Count(PlaceholderSound.DropLands) + Count(PlaceholderSound.DropTaken), Is.EqualTo(0), "no drop, no drop sound");
        }

        [UnityTest]
        public IEnumerator ACombatRoomCleared_OpensTheWay_Once_TheExitRoom_IsTheFloorsNotes()
        {
            yield return StartRun();
            GeneratedLevel level = _levels.Level;
            LevelRoom room = level.Layout.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Combat && !r.Elite
                && level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) && l.Count > 0);
            Assume.That(room, Is.Not.Null, "a combat room with enemies");
            _played.Clear();
            yield return Kill(level.EnemiesByRoom[room.Order]);
            yield return FramesUntil(() => Count(PlaceholderSound.WayOpens) > 0);
            yield return Frames(3);
            Assert.That(Count(PlaceholderSound.WayOpens), Is.EqualTo(1), "the way on: " + Heard);
            Assert.That(Count(PlaceholderSound.FloorCleared), Is.EqualTo(0), "not the floor's notes");
            Assert.That(_sounds.ClipOf(PlaceholderSound.WayOpens).length, Is.EqualTo(0.6f).Within(0.002f));

            _levels.FinishFloorForTests();
            yield return FramesUntil(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            yield return Frames(2);
            Assert.That(Count(PlaceholderSound.FloorCleared), Is.EqualTo(1), "the exit room: the floor's notes: " + Heard);
            Assert.That(Count(PlaceholderSound.WayOpens), Is.EqualTo(1), "and not the way's");
        }

        GeneratedLevel.PlacedProp FirstChest() => _levels.Chests().First();

        IEnumerator StandByChest(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }

        /// <summary>A run, its first chest opened with these odds of common, rare and epic cards (and the stats and tiers as given).</summary>
        IEnumerator OpenAChest(float[] odds, bool tiers, Action<UpgradeTier?> best)
        {
            _levels.RunValues.StatsAndTiers = tiers;
            yield return StartRun();
            _levels.RunValues.Stats.ChestOdds = odds;
            yield return StandByChest(FirstChest());
            _played.Clear();
            _voiced.Clear();
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            UpgradeTier? top = null;
            foreach (Upgrade u in _levels.OfferedUpgrades)
                if (u.Tiered && (!top.HasValue || u.Tier > top.Value)) top = u.Tier;
            best(top);
            yield return Frames(2);
            yield return new WaitForSecondsRealtime(0.4f); // an epic's second bell, in real time
            yield return null;
        }

        IEnumerator CloseTheChest()
        {
            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return null;
            _levels.BackToPracticeRoom();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AChestsOffer_WithARareCard_RingsABell_WithAnEpic_TwoRising_WithCommons_None()
        {
            UpgradeTier? best = null;
            AudioClip bell = _sounds.ClipOf(PlaceholderSound.RareOffer);

            yield return OpenAChest(new[] { 0f, 100f, 0f }, true, b => best = b);
            Assume.That(best, Is.EqualTo(UpgradeTier.Rare), "a rare card on offer");
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1), "the chest, as before: " + Heard);
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(1), "a bell, with its choice up: " + Heard);
            Assert.That(_played.Single(r => r.Sound == PlaceholderSound.RareOffer).Bells, Is.EqualTo(1), "one");
            Assert.That(_voiced.Count(v => v.Clip == bell), Is.EqualTo(1), "rung once");
            yield return CloseTheChest();

            yield return OpenAChest(new[] { 0f, 0f, 100f }, true, b => best = b);
            Assume.That(best, Is.EqualTo(UpgradeTier.Epic), "an epic card on offer");
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(1), "one sound: " + Heard);
            Assert.That(_played.Single(r => r.Sound == PlaceholderSound.RareOffer).Bells, Is.EqualTo(2), "two bells");
            List<ShotSoundRequest> rung = _voiced.Where(v => v.Clip == bell).ToList();
            Assert.That(rung.Count, Is.EqualTo(2), "rung twice");
            Assert.That(rung[1].Pitch / rung[0].Pitch, Is.EqualTo(PlaceholderSoundRules.EpicNoteStep).Within(1e-3f), "the second a fourth up");
            Assert.That(rung[1].Frame, Is.GreaterThan(rung[0].Frame), "a moment later");
            yield return CloseTheChest();

            yield return OpenAChest(new[] { 100f, 0f, 0f }, true, b => best = b);
            Assume.That(best, Is.EqualTo(UpgradeTier.Common), "commons only");
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1));
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(0), "no bell for commons: " + Heard);
            yield return CloseTheChest();

            yield return OpenAChest(new[] { 0f, 0f, 100f }, false, b => best = b);
            Assert.That(best, Is.Null, "the stats and tiers off: no tiers");
            Assert.That(Count(PlaceholderSound.ChestOpens), Is.EqualTo(1));
            Assert.That(Count(PlaceholderSound.RareOffer), Is.EqualTo(0), "no bell: " + Heard);
            Assert.That(_voiced.Count(v => v.Clip == bell), Is.EqualTo(0));
            yield return CloseTheChest();
        }

        // ---------------- Her crits ----------------

        RunModifiers ByHand(float critChance, bool tiers = true)
        {
            var m = new RunModifiers { StatsAndTiers = tiers, StatValues = new StatValues { StartCritChance = critChance } };
            _player.Modifiers = m;
            return m;
        }

        [UnityTest]
        public IEnumerator ASeededCrit_TicksOverItsHit_ACleanPresssCrit_OnlyRingsItsOwnTone_AndWithTheSwitchOff_None()
        {
            _a.SetMaxHealth(1000000);
            var hits = new List<PlayerReport>();
            var hitFrames = new List<int>();
            _player.Reported += r =>
            {
                if (r.Kind != PlayerReportKind.Hit || r.Target != _a) return;
                hits.Add(r);
                hitFrames.Add(Time.frameCount);
            };
            _player.Teleport(NearA);
            yield return null;

            // The switch off: a crit chance of 1 rolls nothing.
            ByHand(1f, tiers: false);
            for (int i = 0; i < 2; i++)
            {
                int had = hits.Count;
                yield return UntilFree();
                _player.Shoot(_a.transform.position);
                yield return Until(() => hits.Count > had, 2f);
            }
            yield return Frames(2);
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits.Any(h => h.Crit), Is.False, "no crit with the stats and tiers off");
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(2));
            Assert.That(Count(PlaceholderSound.Crit), Is.EqualTo(0), "and no crit's tick");

            // On: where the seeded stream rolls one.
            hits.Clear();
            hitFrames.Clear();
            _played.Clear();
            ByHand(0.5f);
            _player.SeedCrits(50UL);
            for (int i = 0; i < 6; i++)
            {
                int had = hits.Count;
                yield return UntilFree();
                _player.Shoot(_a.transform.position);
                yield return Until(() => hits.Count > had, 2f);
                yield return new WaitForSeconds(0.1f); // apart, under the tick's cap
            }
            yield return Frames(2);
            Assert.That(hits.Count, Is.EqualTo(6));
            Assume.That(hits.Any(h => h.Crit) && hits.Any(h => !h.Crit), Is.True, "the seed gives both");
            var critFrames = hits.Select((h, i) => h.Crit ? hitFrames[i] : -1).Where(f => f >= 0).ToList();
            Assert.That(Count(PlaceholderSound.Crit), Is.EqualTo(critFrames.Count), "one tick for each crit: " + Heard);
            Assert.That(_played.Where(r => r.Sound == PlaceholderSound.Crit).Select(r => r.Frame), Is.EqualTo(critFrames), "each over its hit, in its frame");
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.EqualTo(6), "the hit tick under every one, as before");
            Assert.That(_sounds.ClipOf(PlaceholderSound.Crit).length, Is.EqualTo(0.06f).Within(0.002f));

            // A clean press: a crit by itself, its own tone rings, the crit's tick does not.
            TimingFeedback feedback = TimingFeedback.Ensure();
            var tones = new List<FeedbackTone>();
            feedback.TonePlayed += t => tones.Add(t);
            ByHand(0f); // nothing crits by a roll: only the clean press
            yield return UntilFree();
            yield return null;
            hits.Clear();
            _played.Clear();
            int tap = _player.SlotOf("Triple Tap");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.IsRecastWindowOpen, 2f);
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.RecastPressWouldBeClean, 2f);
            Assume.That(_player.RecastPressWouldBeClean, Is.True, "the clean part of the last window");
            Assert.That(_player.PressSkill(tap, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => hits.Count(h => h.Slot == tap) >= 3, 2f);
            yield return Frames(2);
            Assume.That(hits.Where(h => h.Slot == tap).Select(h => h.Crit), Is.EqualTo(new[] { false, false, true }), "the clean press is a crit");
            Assert.That(tones.Count(t => t == FeedbackTone.Clean), Is.EqualTo(1), "its own tone rang");
            Assert.That(Count(PlaceholderSound.Crit), Is.EqualTo(0), "and the crit's tick did not: " + Heard);
            Assert.That(Count(PlaceholderSound.EnemyHit), Is.GreaterThanOrEqualTo(1), "its hits ticked as ever");
        }

        // ---------------- Replaced, and nothing per frame ----------------

        static readonly PlaceholderSound[] TheTen =
        {
            PlaceholderSound.ProjectileStopped, PlaceholderSound.OrbLoosed, PlaceholderSound.Duck, PlaceholderSound.Stunned,
            PlaceholderSound.Pulled, PlaceholderSound.DropLands, PlaceholderSound.DropTaken, PlaceholderSound.WayOpens,
            PlaceholderSound.Crit, PlaceholderSound.RareOffer,
        };

        [UnityTest]
        public IEnumerator AClipUnderANewSoundsName_IsPlayedInPlaceOfItsSynthesisedOne()
        {
            Object.Destroy(_sounds.gameObject);
            yield return null;
            Assert.That(PlaceholderSounds.Current, Is.Null);

            // Clips made here stand for files at Resources/Sounds/Placeholders/<name>: no file is added to the project.
            var recorded = new Dictionary<string, AudioClip>();
            foreach (PlaceholderSound s in TheTen)
            {
                var clip = AudioClip.Create("Recorded" + s, 2205, 1, 44100, false);
                var data = new float[2205];
                for (int i = 0; i < data.Length; i++) data[i] = 0.4f * Mathf.Sin(i * 0.07f) * (1f - i / (float)data.Length);
                clip.SetData(data, 0);
                _made.Add(clip);
                recorded["Sounds/Placeholders/" + s] = clip;
            }
            var looked = new List<string>();
            PlaceholderSounds.RecordedClips = path =>
            {
                looked.Add(path);
                return recorded.TryGetValue(path, out AudioClip c) ? c : null;
            };
            _sounds = PlaceholderSounds.Ensure();
            yield return null;
            foreach (PlaceholderSound s in TheTen)
            {
                Assert.That(looked, Does.Contain("Sounds/Placeholders/" + s), s + " looked for under its name");
                Assert.That(_sounds.IsRecorded(s), Is.True, s.ToString());
                Assert.That(_sounds.ClipOf(s), Is.SameAs(recorded["Sounds/Placeholders/" + s]), s.ToString());
            }
            Assert.That(_sounds.IsRecorded(PlaceholderSound.Strike), Is.False, "the others stay synthesised");

            // Played through the game's own report: a stun.
            _played.Clear();
            Assert.That(_player.Stun(0.5f, "test"), Is.True);
            yield return Frames(2);
            PlaceholderSoundRequest stun = _played.Single(r => r.Sound == PlaceholderSound.Stunned);
            Assert.That(stun.Clip, Is.SameAs(recorded["Sounds/Placeholders/Stunned"]), "the recorded clip played");
            Assert.That(stun.Recorded, Is.True);
            Assert.That(_voiced.Count(v => v.Clip == stun.Clip), Is.EqualTo(1));
            Object.Destroy(_sounds.gameObject);
            _sounds = null;
            yield return null;
            Assert.That(recorded.Values.All(c => c != null), Is.True, "a recorded clip is not destroyed with the sounds");
        }

        [UnityTest]
        public IEnumerator WhileNothingPlays_NothingIsAllocatedPerFrame()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            TimingFeedback.Ensure();
            yield return StartRun(); // a floor of enemies, all listened to
            yield return Frames(3);
            _sounds.Tick();
            Assume.That(_sounds.Listening, Is.GreaterThan(0), "it listens to the floor's enemies");
            _played.Clear();
            Assert.That(() => { for (int i = 0; i < 300; i++) _sounds.Tick(); }, NotAllocating, "a frame of the sounds allocates nothing");
            Assert.That(_played, Is.Empty, "nothing asked for in those frames: " + Heard);
        }

        static byte[] s_junk;
    }
}
