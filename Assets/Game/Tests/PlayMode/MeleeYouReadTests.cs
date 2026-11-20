using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Melee you read (decision 0010; work order 035, 2026-11-20), through the intents in the ControlRoom scene: each kind's
    /// ordinary attack lands on her standing in reach and misses when she steps out or dashes; no floor shape for these
    /// kinds with the switch on and the shapes with it off; the glint on the body, red or blue, growing for a skill; a Leap
    /// lands beside her when she sidesteps; a Shield bash stuns her, is dropped by Stopping Shot from the front and misses
    /// when she steps aside; a Drag pulls her, is broken by a wall or a tall prop and dropped by Stopping Shot, and a pull
    /// stops at what blocks walking; a Crush hurts; stunned she cannot move or act and then can; none of it while paused;
    /// the recorder counts the new attacks; and the damage per ten seconds of an enemy left to attack her freely, before
    /// and after, at floor 1's and floor 5's scale.
    ///
    /// The practice brute is switched off: it stays in the scene as the template every enemy here is made from. Every
    /// enemy is made where the test puts it, north of her, and takes no turns. Her health is made large so that none of
    /// it ends in her death. Waits are in game time or real time with a limit, never frames of a supposed length.
    /// </summary>
    public class MeleeYouReadTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        static readonly Vector3 EnemyAt = new Vector3(8f, 0f, 4f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        CombatEffects _fx;
        PlaySessionRecorder _recorder;
        string _folder;
        readonly List<Object> _made = new List<Object>();
        readonly List<CombatFx> _pieces = new List<CombatFx>();
        readonly List<PlaceholderSoundRequest> _sounds = new List<PlaceholderSoundRequest>();

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
            Assert.That(_player.MeleeYouRead, Is.True, "on by default");
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _fx = CombatEffects.Ensure();
            CombatEffects.Spawned += OnPiece;
            _player.Teleport(Stand);
            yield return null;
            _pieces.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            CombatEffects.Spawned -= OnPiece;
            PlaceholderSounds.Requested -= OnSound;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (PlaceholderSounds.Current != null) Object.Destroy(PlaceholderSounds.Current.gameObject);
            if (_fx != null) Object.Destroy(_fx.gameObject);
            _fx = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _pieces.Clear();
            _sounds.Clear();
        }

        void OnPiece(CombatFx fx) => _pieces.Add(fx);
        void OnSound(PlaceholderSoundRequest r) => _sounds.Add(r);

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        /// <summary>One enemy of a kind made where it stands, from the practice enemy, awake once provoked; it stands still unless <paramref name="walks"/>.</summary>
        T Make<T>(EnemyKind kind, Vector3 at, bool walks = true) where T : MeleeEnemy
        {
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2f, NavMesh.AllAreas)) at = hit.position;
            Enemy e = EnemyFactory.Make(kind, at, null, _practiceEnemy, "Melee_" + kind);
            Assert.That(e, Is.Not.Null, kind + " made");
            _made.Add(e.gameObject);
            if (e is EnemyBrute brute)
            {
                // Its own copy of the asset's numbers, as a floor's brute has (the asset is never written).
                brute.FloorValues = new LevelValues().FloorBruteValues(brute.Settings != null ? brute.Settings.Values : null);
                brute.enabled = true;
            }
            if (!walks) e.Values.MoveSpeed = 0f;
            return (T)e;
        }

        /// <summary>Puts her <paramref name="distance"/> south of the enemy, facing nothing in particular.</summary>
        void HerAt(Enemy e, float distance) => _player.Teleport(Flat3(e.transform.position) + Vector3.back * distance);

        static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

        IEnumerator UntilWindup(Enemy e, string attack, float timeout)
        {
            yield return Until(() => e.State == EnemyState.Windup && e.Attack != null && e.Attack.Name == attack, timeout);
            Assert.That(e.State, Is.EqualTo(EnemyState.Windup), e.Kind + " winds up " + attack);
            Assert.That(e.Attack.Name, Is.EqualTo(attack));
        }

        IEnumerator UntilStruck(Enemy e, int strikes, float timeout)
        {
            yield return Until(() => e.Strikes > strikes, timeout);
            Assert.That(e.Strikes, Is.EqualTo(strikes + 1), "it struck");
        }

        // ---------------- The ordinary attacks ----------------

        IEnumerator OrdinaryLands_AndMissesOutOfReach_AndOnTheDash(EnemyKind kind, string name, float standOff)
        {
            MeleeEnemy e = Make<MeleeEnemy>(kind, EnemyAt, walks: false); // it strikes where it stands: no skill comes in between
            EnemyAttackDefinition attack = MeleeAttacks.Find(e.AttacksNow, name);
            Assert.That(attack.Ordinary, Is.True);
            yield return null;
            HerAt(e, standOff);
            e.Provoke();

            // Standing in reach: it lands.
            yield return UntilWindup(e, name, 8f);
            int strikes = e.Strikes, landed = e.StrikesLanded, hp = _health.Current;
            yield return UntilStruck(e, strikes, attack.Windup + 1f);
            Assert.That(e.StrikesLanded, Is.EqualTo(landed + 1), "it lands on her standing in reach");
            Assert.That(hp - _health.Current, Is.EqualTo(attack.Damage), "for its damage");
            Assert.That(e.IsTelegraphing, Is.False, "nothing drawn on the floor");

            // Stepping out of reach in its short tell: it misses.
            yield return UntilWindup(e, name, 8f);
            strikes = e.Strikes;
            landed = e.StrikesLanded;
            hp = _health.Current;
            _player.Teleport(Stand); // ten metres off
            yield return UntilStruck(e, strikes, attack.Windup + 1f);
            Assert.That(e.StrikesLanded, Is.EqualTo(landed), "out of reach: it misses");
            Assert.That(_health.Current, Is.EqualTo(hp));

            // The dash in its tell: it misses.
            yield return For(attack.Recovery + 0.1f);
            HerAt(e, standOff);
            yield return UntilWindup(e, name, 8f);
            strikes = e.Strikes;
            landed = e.StrikesLanded;
            hp = _health.Current;
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 6f), Is.EqualTo(IntentResult.Started));
            yield return UntilStruck(e, strikes, attack.Windup + 1f);
            Assert.That(e.StrikesLanded, Is.EqualTo(landed), "the dash avoids it, as every strike");
            Assert.That(_health.Current, Is.EqualTo(hp));
        }

        [UnityTest]
        public IEnumerator TheSwarmersBite_LandsInReach_MissesOutOfReach_AndOnTheDash() =>
            OrdinaryLands_AndMissesOutOfReach_AndOnTheDash(EnemyKind.Swarmer, MeleeAttacks.Bite, 1f);

        [UnityTest]
        public IEnumerator TheBrutesStroke_LandsInReach_MissesOutOfReach_AndOnTheDash() =>
            OrdinaryLands_AndMissesOutOfReach_AndOnTheDash(EnemyKind.Brute, MeleeAttacks.Stroke, 1.5f);

        [UnityTest]
        public IEnumerator TheGuardsFist_LandsInReach_MissesOutOfReach_AndOnTheDash() =>
            OrdinaryLands_AndMissesOutOfReach_AndOnTheDash(EnemyKind.Guard, MeleeAttacks.Fist, 2f);

        [UnityTest]
        public IEnumerator TheBite_KeepsMovingWithHer_AndTakesNoTurn()
        {
            Enemy.Turns.Clear();
            var e = Make<EnemySwarmer>(EnemyKind.Swarmer, EnemyAt);
            e.TakesTurns = true; // as a floor's: an ordinary attack still takes none
            yield return null;
            HerAt(e, 1f);
            e.Provoke();
            yield return UntilWindup(e, MeleeAttacks.Bite, 6f);
            Assert.That(Enemy.Turns.WindingUp(TurnSort.Light, Time.time), Is.EqualTo(0), "a bite counts as no wind-up for the turns");
            // She walks away: it keeps after her through its bites.
            Vector3 before = e.transform.position;
            _player.Steer(Vector3.back);
            float moved = 0f;
            bool movedWhileBiting = false;
            float end = Time.time + 1.2f;
            while (Time.time < end)
            {
                yield return null;
                bool biting = e.State == EnemyState.Windup || e.State == EnemyState.Recover;
                moved = Flat(e.transform.position, before);
                if (biting && moved > 0.5f) movedWhileBiting = true;
            }
            _player.Steer(Vector3.zero);
            Assert.That(movedWhileBiting, Is.True, "it does not stop to wind up: moved " + moved.ToString("0.00") + " m");
        }

        [UnityTest]
        public IEnumerator AnElitesBite_DealsTheElitesScale()
        {
            var e = Make<EnemySwarmer>(EnemyKind.Swarmer, EnemyAt);
            EnemyFactory.MakeElite(e, 2000, 1.25f, 4f / 3f, 0.5f);
            yield return null;
            HerAt(e, 1.2f);
            e.Provoke();
            yield return UntilWindup(e, MeleeAttacks.Bite, 6f);
            int hp = _health.Current, strikes = e.Strikes, landed = e.StrikesLanded;
            yield return UntilStruck(e, strikes, 1f);
            Assume.That(e.StrikesLanded, Is.EqualTo(landed + 1));
            Assert.That(hp - _health.Current, Is.EqualTo(Mathf.RoundToInt(MeleeAttacks.Find(e.Tuning.Melee, MeleeAttacks.Bite).Damage * 1.25f)));
        }

        // ---------------- No floor shape; the sign on the body ----------------

        IEnumerator WindupOf(EnemyKind kind, bool on, float standOff, string attack, Action<MeleeEnemy> got)
        {
            _player.MeleeYouRead = on;
            MeleeEnemy e = Make<MeleeEnemy>(kind, EnemyAt, walks: false);
            yield return null;
            HerAt(e, standOff);
            e.Provoke();
            yield return UntilWindup(e, attack, 8f);
            got(e);
        }

        [UnityTest]
        public IEnumerator NoFloorShape_ForTheseKinds_WithTheSwitchOn_AndTheirShapesWithItOff()
        {
            var cases = new[]
            {
                (EnemyKind.Swarmer, 1f, MeleeAttacks.Bite, "Lunge"),
                (EnemyKind.Brute, 1.5f, MeleeAttacks.Stroke, "Slam"),
                (EnemyKind.Guard, 2f, MeleeAttacks.Crush, "Slam"),
            };
            foreach ((EnemyKind kind, float standOff, string onAttack, string offAttack) in cases)
            {
                MeleeEnemy e = null;
                yield return WindupOf(kind, true, standOff, onAttack, x => e = x);
                Assert.That(e.ReadOnBody, Is.True);
                Assert.That(e.Telegraph, Is.Not.Null, kind + ": it has a telegraph to draw with...");
                Assert.That(e.IsTelegraphing, Is.False, kind + ": ...and draws nothing on the floor");
                _pieces.Clear();
                int strikes = e.Strikes;
                yield return UntilStruck(e, strikes, 3f);
                Assert.That(_pieces.Any(p => p != null && p.Moment == FxMoment.EnemyStruck), Is.False, kind + ": no strike drawn on the ground");
                Assert.That(Object.FindObjectsByType<Transform>().Any(t => t.name == "StrikeFx"), Is.False, kind + ": no stand-in");
                Object.Destroy(e.gameObject);
                yield return null;

                yield return WindupOf(kind, false, standOff, offAttack, x => e = x);
                Assert.That(e.ReadOnBody, Is.False);
                Assert.That(e.IsTelegraphing, Is.True, kind + ": with the switch off its shape is on the floor, as before");
                _pieces.Clear();
                strikes = e.Strikes;
                yield return UntilStruck(e, strikes, 3f);
                Assert.That(_pieces.Any(p => p != null && p.Moment == FxMoment.EnemyStruck), Is.True, kind + ": its strike drawn as before");
                Object.Destroy(e.gameObject);
                yield return null;
            }
            _player.MeleeYouRead = true;

            // The practice room's keys spawn the kinds with the switch as it stands.
            _practiceEnemy.enabled = true;
            List<Enemy> spawned = _levels.SpawnPractice(EnemyKind.Guard);
            Assert.That(spawned.Single().ReadOnBody, Is.True);
            _player.MeleeYouRead = false;
            Assert.That(spawned.Single().ReadOnBody, Is.False, "read live");
            _player.MeleeYouRead = true;
            _levels.ClearSpawned();
            _practiceEnemy.enabled = false;
        }

        [UnityTest]
        public IEnumerator TheGlint_IsOnTheBody_RedForTheStroke_BlueAndGrowingForTheBash()
        {
            // The stroke: one small red flick at its front, at about chest height, on it.
            EnemyBrute brute = null;
            _pieces.Clear();
            yield return WindupOf(EnemyKind.Brute, true, 1.5f, MeleeAttacks.Stroke, x => brute = (EnemyBrute)x);
            List<CombatFx> glints = _pieces.Where(p => p != null && p.Moment == FxMoment.WindupStarted).ToList();
            Assert.That(glints.Count, Is.EqualTo(1));
            FxPiece flick = glints[0].Piece;
            Assert.That(flick.Shape, Is.EqualTo(FxShape.Spark), "a glint, not the windup's spikes");
            Assert.That(flick.Follow, Is.EqualTo(brute.transform), "on the enemy");
            Assert.That(flick.FollowOffset.y, Is.InRange(0.6f, 1.6f), "at about chest or weapon height");
            Assert.That(flick.Holds, Is.Null, "a flick: it does not hold");
            Assert.That(flick.Rim.r, Is.GreaterThan(flick.Rim.b), "red");
            Object.Destroy(brute.gameObject);
            yield return null;

            // The bash: blue, held through the windup and growing, then it snaps.
            _pieces.Clear();
            yield return WindupOf(EnemyKind.Brute, true, 4.5f, MeleeAttacks.ShieldBash, x => brute = (EnemyBrute)x);
            glints = _pieces.Where(p => p != null && p.Moment == FxMoment.WindupStarted).ToList();
            Assert.That(glints.Count, Is.EqualTo(1));
            CombatFx glint = glints[0];
            Assert.That(glint.Piece.Rim.b, Is.GreaterThan(glint.Piece.Rim.r), "blue: Stopping Shot can counter it");
            Assert.That(glint.Piece.Size, Is.GreaterThan(flick.Size), "larger than a flick");
            float early = glint.transform.localScale.x;
            yield return For(0.5f);
            Assert.That(glint != null && glint.IsHolding, Is.True, "held through the windup");
            Assert.That(glint.transform.localScale.x, Is.GreaterThan(early * 1.5f), "growing");
            int strikes = brute.Strikes;
            yield return UntilStruck(brute, strikes, 2f);
            yield return null;
            Assert.That(glint == null || !glint.IsHolding, Is.True, "it snaps at the strike");
            Assert.That(_pieces.Any(p => p != null && p.Moment == FxMoment.EnemyStruck), Is.False, "nothing flat on the floor");
        }

        [UnityTest]
        public IEnumerator TheSounds_ASkillsWindupAsBefore_AnOrdinaryAttacksNone_AndEveryStrikeItsSound()
        {
            PlaceholderSounds.Ensure();
            PlaceholderSounds.Requested += OnSound;
            EnemyBrute brute = null;
            yield return WindupOf(EnemyKind.Brute, true, 1.5f, MeleeAttacks.Stroke, x => brute = (EnemyBrute)x);
            yield return null;
            Assert.That(_sounds.Any(s => s.Sound == PlaceholderSound.WindupRed || s.Sound == PlaceholderSound.WindupBlue), Is.False, "the stroke's windup is silent");
            int strikes = brute.Strikes;
            yield return UntilStruck(brute, strikes, 2f);
            yield return null;
            Assert.That(_sounds.Any(s => s.Sound == PlaceholderSound.Strike), Is.True, "its strike has the strike's sound");
            Object.Destroy(brute.gameObject);
            yield return null;
            _sounds.Clear();
            yield return WindupOf(EnemyKind.Brute, true, 4.5f, MeleeAttacks.ShieldBash, x => brute = (EnemyBrute)x);
            yield return null;
            Assert.That(_sounds.Any(s => s.Sound == PlaceholderSound.WindupBlue), Is.True, "the bash winds up blue, as a counterable attack did");
        }

        // ---------------- The swarmer's Leap ----------------

        [UnityTest]
        public IEnumerator ALeap_BitesHerWhereSheStood_AndLandsBesideHerWhenSheSidesteps()
        {
            var e = Make<EnemySwarmer>(EnemyKind.Swarmer, EnemyAt, walks: false);
            EnemyAttackDefinition leap = MeleeAttacks.Find(e.Tuning.Melee, MeleeAttacks.Leap);
            yield return null;
            HerAt(e, 5.5f);
            e.Provoke();

            // She stands: it hops to her and bites on landing.
            yield return UntilWindup(e, MeleeAttacks.Leap, 4f);
            Vector3 from = e.transform.position;
            yield return Until(() => e.IsMoving, 1f);
            Assert.That(e.IsMoving, Is.True, "it hops");
            int strikes = e.Strikes, landed = e.StrikesLanded, hp = _health.Current;
            yield return UntilStruck(e, strikes, 1f);
            Assert.That(Flat(e.transform.position, from), Is.GreaterThan(3.5f), "it covered the distance");
            Assert.That(e.StrikesLanded, Is.EqualTo(landed + 1), "and bit her on landing");
            Assert.That(hp - _health.Current, Is.EqualTo(leap.Damage));

            // She steps aside as it leaves the ground: it lands where she was, beside her.
            yield return For(0.5f);
            HerAt(e, 5.5f);
            yield return UntilWindup(e, MeleeAttacks.Leap, leap.Cooldown + 2f);
            yield return Until(() => e.IsMoving, 1f);
            Vector3 stoodAt = _player.Position;
            _player.Steer(Vector3.right);
            strikes = e.Strikes;
            landed = e.StrikesLanded;
            hp = _health.Current;
            yield return UntilStruck(e, strikes, 1f);
            _player.Steer(Vector3.zero);
            Assert.That(Flat(e.MoveTarget, stoodAt), Is.LessThan(1f), "aimed where she was when it left the ground");
            Assert.That(Flat(_player.Position, stoodAt), Is.GreaterThan(1.5f), "she stepped aside");
            Assert.That(e.StrikesLanded, Is.EqualTo(landed), "it landed beside her");
            Assert.That(_health.Current, Is.EqualTo(hp));
        }

        // ---------------- The brute's Shield bash ----------------

        [UnityTest]
        public IEnumerator AShieldBash_LungesAndStunsHer()
        {
            var brute = Make<EnemyBrute>(EnemyKind.Brute, EnemyAt, walks: false);
            EnemyAttackDefinition bash = MeleeAttacks.Find(brute.Melee, MeleeAttacks.ShieldBash);
            yield return null;
            HerAt(brute, 4.5f);
            brute.Provoke();
            yield return UntilWindup(brute, MeleeAttacks.ShieldBash, 4f);
            Assert.That(brute.IsTelegraphing, Is.False);
            Vector3 from = brute.transform.position;
            int strikes = brute.Strikes, hp = _health.Current;
            yield return UntilStruck(brute, strikes, 2f);
            Assert.That(Flat(brute.transform.position, from), Is.GreaterThan(2.5f), "it lunged");
            Assert.That(hp - _health.Current, Is.EqualTo(bash.Damage));
            Assert.That(_player.IsStunned, Is.True, "stunned");
            Assert.That(_player.TimesStunned, Is.EqualTo(1));
            float stunnedAt = Time.time;
            yield return Until(() => !_player.IsStunned, 2f);
            Assert.That(Time.time - stunnedAt, Is.EqualTo(bash.Stun).Within(0.1f), "for half a second");
        }

        [UnityTest]
        public IEnumerator AShieldBash_IsDroppedByStoppingShotFromTheFront()
        {
            var brute = Make<EnemyBrute>(EnemyKind.Brute, EnemyAt, walks: false);
            yield return null;
            HerAt(brute, 4.5f);
            brute.Provoke();
            yield return UntilWindup(brute, MeleeAttacks.ShieldBash, 4f);
            int strikes = brute.Strikes;
            Assert.That(_player.PressSkill(Slot("Stopping Shot"), brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => brute.Countered > 0 || brute.State != EnemyState.Windup, 3f);
            Assert.That(brute.Countered, Is.EqualTo(1), "countered");
            Assert.That(brute.IsDazed, Is.True, "and dazed");
            yield return For(0.3f);
            Assert.That(brute.Strikes, Is.EqualTo(strikes), "no strike");
            Assert.That(_player.IsStunned, Is.False, "no stun");
        }

        [UnityTest]
        public IEnumerator AShieldBash_MissesWhenSheStepsAside()
        {
            var brute = Make<EnemyBrute>(EnemyKind.Brute, EnemyAt, walks: false);
            yield return null;
            HerAt(brute, 4.5f);
            brute.Provoke();
            yield return UntilWindup(brute, MeleeAttacks.ShieldBash, 4f);
            yield return Until(() => brute.IsMoving, 2f);
            Assert.That(brute.IsMoving, Is.True, "it lunges");
            _player.Steer(Vector3.right);
            int strikes = brute.Strikes, landed = brute.StrikesLanded, hp = _health.Current;
            yield return UntilStruck(brute, strikes, 1f);
            _player.Steer(Vector3.zero);
            Assert.That(brute.StrikesLanded, Is.EqualTo(landed), "the line was fixed as it started: it misses");
            Assert.That(_health.Current, Is.EqualTo(hp));
            Assert.That(_player.IsStunned, Is.False);
        }

        // ---------------- The guard's Drag and Crush ----------------

        [UnityTest]
        public IEnumerator ADrag_PullsHerIn_AndTheCrushThatFollowsHurts()
        {
            var guard = Make<EnemyGuard>(EnemyKind.Guard, EnemyAt, walks: false);
            EnemyAttackDefinition drag = MeleeAttacks.Find(guard.Tuning.Melee, MeleeAttacks.Drag);
            EnemyAttackDefinition crush = MeleeAttacks.Find(guard.Tuning.Melee, MeleeAttacks.Crush);
            yield return null;
            HerAt(guard, 6f);
            guard.Provoke();
            yield return UntilWindup(guard, MeleeAttacks.Drag, 4f);
            Assert.That(guard.IsTelegraphing, Is.False);
            int strikes = guard.Strikes;
            yield return UntilStruck(guard, strikes, 2f);
            Assert.That(_player.IsPulled, Is.True, "pulled");
            Assert.That(_player.IsLocked, Is.True, "she cannot act meanwhile");
            yield return Until(() => !_player.IsPulled, 1f);
            Assert.That(Flat(_player.Position, guard.transform.position), Is.EqualTo(drag.PullTo).Within(0.35f), "to 2 m in front of it");
            Assert.That(_player.IsLocked, Is.False, "and acts at once");

            int hp = _health.Current;
            yield return UntilWindup(guard, MeleeAttacks.Crush, 2f);
            strikes = guard.Strikes;
            yield return UntilStruck(guard, strikes, crush.Windup + 1f);
            Assert.That(hp - _health.Current, Is.EqualTo(crush.Damage), "the crush hurts");
        }

        IEnumerator DragBrokenBy(Func<EnemyGuard, GameObject> blocker, string what)
        {
            var guard = Make<EnemyGuard>(EnemyKind.Guard, EnemyAt, walks: false);
            yield return null;
            HerAt(guard, 6f);
            guard.Provoke();
            yield return UntilWindup(guard, MeleeAttacks.Drag, 4f);
            GameObject wall = blocker(guard);
            _made.Add(wall);
            Physics.SyncTransforms();
            Vector3 her = _player.Position;
            int strikes = guard.Strikes;
            yield return UntilStruck(guard, strikes, 2f);
            yield return null;
            Assert.That(_player.TimesPulled, Is.EqualTo(0), what + " between them breaks it");
            Assert.That(Flat(_player.Position, her), Is.LessThan(0.1f), "she stays where she was");
        }

        [UnityTest]
        public IEnumerator ADrag_IsBrokenByAWall() => DragBrokenBy(g =>
        {
            Vector3 mid = (g.transform.position + _player.Position) * 0.5f;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = new Vector3(mid.x, 1.5f, mid.z);
            wall.transform.localScale = new Vector3(4f, 3f, 0.5f);
            wall.AddComponent<Cover>();
            return wall;
        }, "a wall");

        [UnityTest]
        public IEnumerator ADrag_IsBrokenByATallProp() => DragBrokenBy(g =>
        {
            Vector3 mid = (g.transform.position + _player.Position) * 0.5f;
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "TestPillar";
            pillar.transform.position = new Vector3(mid.x, 1.2f, mid.z);
            pillar.transform.localScale = new Vector3(1f, 1.2f, 1f); // 2.4 m tall: cover, as a tall prop is
            pillar.AddComponent<Cover>();
            return pillar;
        }, "a tall prop");

        [UnityTest]
        public IEnumerator ADrag_IsDroppedByStoppingShot()
        {
            var guard = Make<EnemyGuard>(EnemyKind.Guard, EnemyAt, walks: false);
            yield return null;
            HerAt(guard, 6f);
            guard.Provoke();
            yield return UntilWindup(guard, MeleeAttacks.Drag, 4f);
            int strikes = guard.Strikes;
            Assert.That(_player.PressSkill(Slot("Stopping Shot"), guard.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => guard.Countered > 0 || guard.State != EnemyState.Windup, 3f);
            Assert.That(guard.Countered, Is.EqualTo(1), "countered");
            Assert.That(guard.IsDazed, Is.True);
            yield return For(0.3f);
            Assert.That(guard.Strikes, Is.EqualTo(strikes), "it never went off");
            Assert.That(_player.TimesPulled, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator APull_StopsAtWhatBlocksWalking()
        {
            var guard = Make<EnemyGuard>(EnemyKind.Guard, EnemyAt, walks: false);
            yield return null;
            HerAt(guard, 6f);
            // A low block across her way in, which she cannot walk through and the guard sees over: no cover.
            Vector3 her = _player.Position, g = guard.transform.position;
            float blockZ = her.z + 2f;
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "TestLowBlock";
            block.transform.position = new Vector3(her.x, 0.25f, blockZ);
            block.transform.localScale = new Vector3(4f, 0.5f, 0.6f);
            _made.Add(block);
            var obstacle = block.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            yield return For(0.25f); // the NavMesh is carved round it
            Assume.That(g.z, Is.GreaterThan(blockZ), "the guard beyond it");
            guard.Provoke();
            yield return UntilWindup(guard, MeleeAttacks.Drag, 4f);
            int strikes = guard.Strikes;
            yield return UntilStruck(guard, strikes, 2f);
            Assert.That(_player.IsPulled, Is.True, "a low block does not break it");
            yield return Until(() => !_player.IsPulled, 1f);
            Assert.That(_player.Position.z, Is.LessThan(blockZ - 0.3f + 0.05f), "she stopped at it");
            Assert.That(_player.Position.z - her.z, Is.GreaterThan(0.5f), "after being pulled toward it");
        }

        // ---------------- Stunned, for her ----------------

        [UnityTest]
        public IEnumerator Stunned_SheCannotMoveShootUseASkillOrDash_ThenCan()
        {
            yield return null;
            Vector3 at = _player.Position;
            int shots = _player.BasicShotsFired, dashes = _player.DashesStarted, actions = _player.ActionsStarted;
            Assert.That(_player.Stun(0.5f, "test"), Is.True);
            Assert.That(_player.IsStunned, Is.True);
            _player.Steer(Vector3.right);
            _player.Shoot(at + Vector3.forward * 5f);
            Assert.That(_player.PressSkill(0, at + Vector3.forward * 5f), Is.Not.EqualTo(IntentResult.Started), "no skill");
            Assert.That(_player.DashToward(at + Vector3.forward * 5f), Is.Not.EqualTo(IntentResult.Started), "no dash");
            yield return For(0.3f);
            Assert.That(Flat(_player.Position, at), Is.LessThan(0.05f), "she cannot move");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(shots), "nor shoot");
            Assert.That(_player.DashesStarted, Is.EqualTo(dashes));
            Assert.That(_player.ActionsStarted, Is.EqualTo(actions));
            Assert.That(_player.Stun(0.5f), Is.False, "it never stacks");
            yield return Until(() => !_player.IsStunned, 1f);
            yield return For(0.2f);
            Assert.That(Flat(_player.Position, at), Is.GreaterThan(0.3f), "then she moves, the key still held");
            Assert.That(_player.DashesStarted, Is.EqualTo(dashes), "a press from the stun's start is not kept past what the buffer allows");
            _player.Steer(Vector3.zero);
            Assert.That(_player.Stun(0.5f), Is.False, "nor chains: not again so soon");
            yield return For(HeldState.StunGrace);
            Assert.That(_player.Stun(0.2f), Is.True, "1.5 s after one ends, again");
        }

        [UnityTest]
        public IEnumerator AStun_KeepsAPressMadeJustBeforeItEnds_AsTheBufferAllows()
        {
            yield return null;
            int dashes = _player.DashesStarted;
            Assert.That(_player.Stun(0.4f), Is.True);
            yield return For(0.4f - _player.Feel.InputBuffer * 0.5f);
            Assume.That(_player.IsStunned, Is.True);
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 5f), Is.EqualTo(IntentResult.Buffered));
            yield return Until(() => !_player.IsStunned, 1f);
            yield return null;
            Assert.That(_player.DashesStarted, Is.EqualTo(dashes + 1), "the dash pressed in the buffer's time goes at once");
        }

        [UnityTest]
        public IEnumerator NoneOfIt_WhilePaused_OrDead()
        {
            var e = Make<EnemySwarmer>(EnemyKind.Swarmer, EnemyAt);
            yield return null;
            HerAt(e, 1f);
            e.Provoke();
            yield return UntilWindup(e, MeleeAttacks.Bite, 6f);
            Assert.That(_levels.Pause(), Is.True);
            int strikes = e.Strikes, hp = _health.Current;
            float end = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Assert.That(e.Strikes, Is.EqualTo(strikes), "nothing strikes while paused");
            Assert.That(_health.Current, Is.EqualTo(hp));
            Assert.That(_player.Stun(0.5f), Is.False, "she is not stunned while paused");
            Assert.That(_player.Pull(_player.Position + Vector3.forward, 0.3f), Is.False, "nor pulled");
            Assert.That(_levels.Resume(), Is.True);
            Object.Destroy(e.gameObject);
            yield return null;

            _health.TakeHit(_health.Current);
            Assume.That(_health.IsAlive, Is.False);
            Assert.That(_player.Stun(0.5f), Is.False, "nor when dead");
        }

        [UnityTest]
        public IEnumerator DeathOrATeleport_EndsAStunOrAPull()
        {
            yield return null;
            Assert.That(_player.Stun(0.5f), Is.True);
            _player.Teleport(Stand + Vector3.right); // a restart, a new floor
            Assert.That(_player.IsHeld, Is.False);
            Assert.That(_player.Pull(_player.Position + Vector3.forward * 3f, 0.3f), Is.True);
            _health.TakeHit(_health.Current); // she dies: her death drops what she was doing
            Assert.That(_player.IsHeld, Is.False);
        }

        // ---------------- The recorder ----------------

        [UnityTest]
        public IEnumerator TheRecorder_CountsTheNewAttacks_AndStunsAndPulls()
        {
            _folder = Path.Combine(Path.GetTempPath(), "MeleeYouReadTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            var brute = Make<EnemyBrute>(EnemyKind.Brute, EnemyAt, walks: false);
            yield return null;
            HerAt(brute, 4.5f);
            brute.Provoke();
            yield return UntilWindup(brute, MeleeAttacks.ShieldBash, 4f);
            yield return UntilStruck(brute, brute.Strikes, 2f);
            yield return Until(() => brute.Strikes >= 2 && brute.State != EnemyState.Windup, 4f); // a stroke follows in reach
            Assume.That(_player.TimesStunned, Is.EqualTo(1));
            Assert.That(_player.Pull(_player.Position + Vector3.forward, 0.3f, "test"), Is.True);
            yield return For(0.4f);

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Any(ev => ev.Type == SessionEventTypes.EnemyAttack && ev.Name == MeleeAttacks.ShieldBash && ev.Enemy == "brute"));
            Assert.That(events.Any(ev => ev.Type == SessionEventTypes.EnemyStrike && ev.Name == MeleeAttacks.ShieldBash && ev.Result == SessionWords.Landed));
            Assert.That(events.Any(ev => ev.Type == SessionEventTypes.EnemyStrike && ev.Name == MeleeAttacks.Stroke));
            Assert.That(events.Count(ev => ev.Type == SessionEventTypes.PlayerStunned && ev.Name == MeleeAttacks.ShieldBash), Is.EqualTo(1));
            Assert.That(events.Count(ev => ev.Type == SessionEventTypes.PlayerPulled), Is.EqualTo(1));
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("  " + MeleeAttacks.ShieldBash, summary);
            StringAssert.Contains("  " + MeleeAttacks.Stroke, summary);
            StringAssert.Contains("Stunned / pulled", summary);
            Debug.Log("[035] summary\n" + summary);
        }

        // ---------------- Keeping it hard ----------------

        /// <summary>
        /// One enemy of a kind left to attack her freely: she stands in its reach in front of it and never dodges, for
        /// twenty seconds of game time, at a floor's damage scale. The damage she took.
        /// </summary>
        IEnumerator Freely(EnemyKind kind, bool on, int floor, Action<int> got)
        {
            _player.MeleeYouRead = on;
            MeleeEnemy e = Make<MeleeEnemy>(kind, EnemyAt);
            e.DamageScale = new LevelValues().DamageScaleOf(floor);
            yield return null;
            float standOff = kind == EnemyKind.Swarmer ? 1f : kind == EnemyKind.Brute ? 1.5f : 2f;
            HerAt(e, standOff);
            _health.RestoreFull();
            int taken = 0;
            Action<int> hurt = d => taken += d;
            _health.Damaged += hurt;
            e.Provoke();
            yield return For(20f);
            _health.Damaged -= hurt;
            Object.Destroy(e.gameObject);
            yield return null;
            got(taken);
        }

        [UnityTest]
        public IEnumerator DamagePerTenSeconds_LeftToAttackHerFreely_IsAboutWhatItWas_OrUpToAThirdMore()
        {
            Time.timeScale = 5f; // twenty seconds of game time each, four seconds of real time
            var lines = new List<string>();
            foreach (EnemyKind kind in new[] { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Guard })
                foreach (int floor in new[] { 1, 5 })
                {
                    int before = 0, after = 0;
                    yield return Freely(kind, false, floor, d => before = d);
                    yield return Freely(kind, true, floor, d => after = d);
                    float ratio = before > 0 ? after / (float)before : float.NaN;
                    string line = $"[035] {kind} floor {floor} (scale {new LevelValues().DamageScaleOf(floor):0.0}): per 10 s before {before / 2f:0.#}, after {after / 2f:0.#}, x{ratio:0.00}";
                    lines.Add(line);
                    Debug.Log(line);
                    TestContext.WriteLine(line);
                    Assert.That(before, Is.GreaterThan(0), kind + " dealt something before");
                    Assert.That(ratio, Is.InRange(0.9f, 1.4f), line);
                }
            Time.timeScale = 1f;
            _player.MeleeYouRead = true;
            Debug.Log(string.Join("\n", lines));
        }
    }
}
