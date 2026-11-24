using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// The sprint (work order 092; Kristian 2026-11-23: a sprint on Shift, no shots meanwhile, a jump over the blue props, a small
    /// stagger when hit; <see cref="SprintRules"/>), in a floor of a fixed seed built in the ControlRoom scene, its enemies taken away,
    /// through the intents (<see cref="PlayerCharacter.HoldSprint"/>, <see cref="PlayerCharacter.Steer(Vector3)"/>,
    /// <see cref="PlayerCharacter.Shoot"/>) at a fixed 60 frames a second of game time: 15.6 m in 2 s where she ran 12; a click while
    /// sprinting fires nothing and is not kept, one after the release fires at once; she crosses a low prop the floor placed and
    /// stands on its far side on the NavMesh, and a tall one stops her; an arrow in her path meets her in the air and she is staggered
    /// on landing; hit while sprinting she stands still for the stagger, hit while running she does not; with the switch off she moves
    /// as before; and nothing allocates in a frame of the sprint.
    /// </summary>
    public class SprintTests
    {
        const float Frame = 1f / 60f;
        const ulong Seed = 7;
        const int FloorNumber = 2;

        PlayerCharacter _player;
        PlayerHealth _health;
        FeelValues _feel;
        GeneratedLevels _levels;
        GeneratedLevel _level;
        EnemyShooter _shooter;
        bool _sprintWas, _takeCoverWas, _quickDuckWas;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _feel = _player.Feel;
            _sprintWas = _feel.Sprint;
            _takeCoverWas = _feel.TakeCover;
            _quickDuckWas = _feel.QuickDuck;
            _feel.Sprint = true;
            _feel.TakeCover = true;
            _feel.QuickDuck = true;
            _reports.Clear();
            _player.Reported += r => _reports.Add(r);

            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues { RoomFights = false };
            _level = _levels.Build(Seed, FloorNumber);
            yield return null;
            yield return null;
            // One shooter is kept to loose an arrow by hand (it does nothing of its own); the rest go.
            _shooter = null;
            foreach (List<Enemy> enemies in _level.EnemiesByRoom.Values)
                foreach (Enemy e in enemies)
                {
                    if (e == null) continue;
                    if (_shooter == null && e is EnemyShooter s)
                    {
                        _shooter = s;
                        s.Values.MoveSpeed = 0f;
                        s.enabled = false;
                        continue;
                    }
                    Object.Destroy(e.gameObject);
                }
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Object.Destroy(d);
            }
            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) Object.Destroy(brute.gameObject);
            yield return null;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            if (_player != null)
            {
                _player.HoldSprint(false);
                _player.HoldCover(false);
                _player.Stop();
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_feel != null)
            {
                _feel.Sprint = _sprintWas;
                _feel.TakeCover = _takeCoverWas;
                _feel.QuickDuck = _quickDuckWas;
            }
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null)
            {
                _levels.ClearSpawned();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- How fast ----------------

        [UnityTest]
        public IEnumerator Sprinting_SheGoes15Point6MetresIn2Seconds_WhereSheRan12()
        {
            Assert.That(FreeLine(out Vector3 a, out Vector3 b), Is.True, "a stretch of free floor in the built floor (seed " + Seed + " floor " + FloorNumber + ")");
            float ran = 0f, sprinted = 0f;
            yield return PingPong(a, b, sprint: false, metres => ran = metres);
            yield return PingPong(a, b, sprint: true, metres => sprinted = metres);
            TestContext.WriteLine("SPRINT in 2 s of game time at 60 fps: ran " + ran.ToString("0.00", CultureInfo.InvariantCulture) + " m, sprinted "
                                  + sprinted.ToString("0.00", CultureInfo.InvariantCulture) + " m on a stretch of " + Flat(b - a).magnitude.ToString("0.0", CultureInfo.InvariantCulture) + " m");
            Assert.That(ran, Is.EqualTo(12f).Within(0.4f), "her run speed, 6 m/s");
            Assert.That(sprinted, Is.EqualTo(15.6f).Within(0.5f), "the sprint's 7.8 m/s, full within 0.1 s");
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.SprintStarted), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_TheKeyHeld_SheRunsAsBefore_ShootsAndDoesNotJump()
        {
            Assert.That(FindJumpable(out Shelter prop, out Vector3 from, out Vector3 way), Is.True, "a low prop to run at, that the sprint would jump");
            _feel.Sprint = false;
            Assert.That(FreeLine(out Vector3 a, out Vector3 b), Is.True);
            float metres = 0f;
            yield return PingPong(a, b, sprint: true, m => metres = m);
            Assert.That(metres, Is.EqualTo(12f).Within(0.4f), "her run speed with the key held");
            Assert.That(_player.IsSprinting, Is.False);
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.SprintStarted), Is.EqualTo(0));
            prop.TryShape(out CoverShape shape);
            float nearest = float.PositiveInfinity;
            yield return RunAt(from, way, sprint: true, seconds: 1.5f, each: () =>
            {
                nearest = Mathf.Min(nearest, shape.Distance(_player.GroundPosition));
                Assert.That(_player.IsJumping, Is.False);
            });
            Assert.That(_player.Jumps, Is.EqualTo(0), "no jump with the switch off");
            Assert.That(nearest, Is.GreaterThan(0.3f), "never over the prop: it stopped her or she went round it, as today");
            _player.HoldSprint(true);
            _player.Steer(way);
            yield return null;
            int started = _player.ActionsStarted;
            _player.Shoot(_player.Position + way * 5f);
            Assert.That(_player.ActionsStarted, Is.EqualTo(started + 1), "a click fires, the key held");
        }

        // ---------------- No shot ----------------

        [UnityTest]
        public IEnumerator AClickWhileSprinting_FiresNothing_AndIsNotKept_OneAfterTheReleaseFiresAtOnce()
        {
            Assert.That(FreeLine(out Vector3 a, out Vector3 b), Is.True);
            _player.Teleport(a);
            yield return null;
            Time.captureDeltaTime = Frame;
            Vector3 way = Flat(b - a).normalized;
            _player.HoldSprint(true);
            _player.Steer(way);
            yield return Frames(6);
            Assert.That(_player.IsSprinting, Is.True);
            int started = _player.ActionsStarted, fired = _player.BasicShotsFired;
            _player.Shoot(_player.Position + way * 5f);
            int slot = Enumerable.Range(0, _player.Skills.Count).FirstOrDefault(s => _player.IsEquipped(s) && _player.SkillCooldownRemaining(s) <= 0f);
            Assert.That(_player.PressSkill(slot, _player.Position + way * 5f), Is.EqualTo(IntentResult.Rejected), "a skill key does nothing");
            _player.ReleaseSkill(slot);
            Assert.That(_player.HasPendingShot, Is.False, "the click is not kept");
            Assert.That(_player.HasBufferedPress, Is.False, "nor the skill key");
            Assert.That(_player.RefusedWhileSprinting, Is.EqualTo(2));
            yield return Frames(6);
            _player.HoldSprint(false);
            yield return Frames(30);
            Assert.That(_player.ActionsStarted, Is.EqualTo(started), "nothing fired later");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(fired));
            _player.Shoot(_player.Position + way * 5f);
            Assert.That(_player.ActionsStarted, Is.EqualTo(started + 1), "the key up: a click starts its shot in that call");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot));
            yield return Frames(10);
            Time.captureDeltaTime = 0f;
            Assert.That(_player.BasicShotsFired, Is.EqualTo(fired + 1));
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.SprintRefused), Is.EqualTo(2));
        }

        // ---------------- The jump ----------------

        [UnityTest]
        public IEnumerator SprintingIntoAPlacedLowProp_SheGoesOverIt_AndStandsOnItsFarSideOnTheNavMesh()
        {
            Assert.That(FindJumpable(out Shelter prop, out Vector3 from, out Vector3 way), Is.True, "a low prop with free floor on both sides (seed " + Seed + ")");
            bool lifted = false, wasJumping = false, landedOnMesh = false;
            Vector3 landed = Vector3.zero, agentAt = Vector3.zero;
            NavMeshAgent agent = _player.GetComponent<NavMeshAgent>();
            yield return RunAt(from, way, sprint: true, seconds: 0.9f, each: () =>
            {
                if (_player.IsJumping && _player.JumpLift > 0.1f) lifted = true;
                if (wasJumping && !_player.IsJumping)
                {
                    landed = _player.Position;
                    agentAt = agent.nextPosition;
                    landedOnMesh = NavMesh.SamplePosition(Flat(landed), out NavMeshHit on, 0.3f, NavMesh.AllAreas) && Flat(on.position - landed).magnitude < 0.2f;
                    _player.HoldSprint(false); // landed: for the look, no more sprint
                }
                wasJumping = _player.IsJumping;
            });
            Assert.That(_player.Jumps, Is.EqualTo(1), "one jump: " + _player.LastJumpRefusal);
            Assert.That(lifted, Is.True, "the model lifted in the air");
            Assert.That(_player.IsJumping, Is.False, "landed");
            Vector3 past = Flat(landed - prop.transform.position);
            Assert.That(Vector3.Dot(past, way), Is.GreaterThan(0.5f), "on its far side: landed at " + landed + ", the prop at " + prop.transform.position);
            Assert.That(landedOnMesh, Is.True, "on the NavMesh at " + landed + " (the agent at " + agentAt + ")");
            Assert.That(Flat(agentAt - landed).magnitude, Is.LessThan(0.05f), "the agent put down where she is: " + agentAt + " against " + landed);
            Assert.That(agent.isOnNavMesh, Is.True);
            PlayerReport jump = _reports.Single(r => r.Kind == PlayerReportKind.SprintJumped);
            Assert.That(jump.By, Is.EqualTo(prop.name));
            Assert.That(jump.Metres, Is.LessThanOrEqualTo(_feel.SprintJumpMost + 1e-3f));
            TestContext.WriteLine("SPRINT jumped " + prop.name + ", " + jump.Metres.ToString("0.00", CultureInfo.InvariantCulture) + " m, in "
                                  + (jump.Metres / (_feel.RunSpeed * _feel.SprintSpeedShare)).ToString("0.000", CultureInfo.InvariantCulture) + " s");
        }

        [UnityTest]
        public IEnumerator SprintingIntoATallProp_SheDoesNotJump_ItStopsHer()
        {
            Assert.That(FindTall(out Shelter prop, out Vector3 from, out Vector3 way), Is.True, "a tall prop to run at (seed " + Seed + ")");
            prop.TryShape(out CoverShape shape);
            float nearest = float.PositiveInfinity;
            yield return RunAt(from, way, sprint: true, seconds: 1.2f, each: () =>
            {
                nearest = Mathf.Min(nearest, shape.Distance(_player.GroundPosition));
                Assert.That(_player.IsJumping, Is.False);
            });
            Assert.That(_player.Jumps, Is.EqualTo(0));
            Assert.That(nearest, Is.GreaterThan(0.3f), "never over it: it stopped her or she went round it, as at a tall prop today");
        }

        [UnityTest]
        public IEnumerator AnArrowInHerPath_MeetsHerInTheAir_AndSheIsStaggeredOnLanding()
        {
            Assert.That(FindJumpable(out Shelter prop, out Vector3 from, out Vector3 way), Is.True);
            EnemyShooter shooter = KeepAShooter();
            yield return null;
            var strikes = new List<(StrikeOutcome outcome, bool inTheAir)>();
            shooter.Reported += r => { if (r.Kind == EnemyReportKind.Struck) strikes.Add((r.Outcome, _player.IsJumping)); };
            bool fired = false, staggeredAtLanding = false, wasJumping = false;
            int health = _health.Current;
            yield return RunAt(from, way, sprint: true, seconds: 1.2f, each: () =>
            {
                if (_player.IsJumping && !fired)
                {
                    fired = true;
                    // Across her way at the jump's middle, timed to meet her there (it waits a frame at the bow).
                    Vector3 mid = (_player.JumpFrom + _player.JumpTo) * 0.5f;
                    float toMid = (_player.JumpLandsAt - Time.time) * 0.5f - Frame;
                    Vector3 side = Vector3.Cross(Vector3.up, way);
                    Vector3 bow = mid + side * (shooter.Tuning.ArrowSpeed * Mathf.Max(Frame, toMid));
                    ThingsThatFly.Ensure().Fire(shooter, Arrow(shooter), ProjectileRules.Arrow(shooter.Tuning, new GroundPoint(bow.x, bow.z), new GroundPoint(-side.x, -side.z)));
                }
                if (wasJumping && !_player.IsJumping && _player.IsStunned) staggeredAtLanding = true;
                wasJumping = _player.IsJumping;
            });
            Assert.That(fired, Is.True, "she jumped: " + _player.LastJumpRefusal);
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].outcome, Is.EqualTo(StrikeOutcome.Landed), "it met her");
            Assert.That(strikes[0].inTheAir, Is.True, "in the air");
            Assert.That(_health.Current, Is.LessThan(health));
            Assert.That(staggeredAtLanding, Is.True, "staggered on landing, not in the air");
            Assert.That(_player.SprintStaggers, Is.EqualTo(1));
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.Stunned && r.By == SprintRules.StaggerName), Is.EqualTo(1), "told as the stun is");
        }

        // ---------------- The stagger ----------------

        [UnityTest]
        public IEnumerator HitWhileSprinting_SheStandsStillForTheStagger_HitWhileRunning_SheDoesNot()
        {
            Assert.That(FreeLine(out Vector3 a, out Vector3 b), Is.True);
            Vector3 way = Flat(b - a).normalized;
            _player.Teleport(a);
            yield return null;
            Time.captureDeltaTime = Frame;
            _player.HoldSprint(true);
            _player.Steer(way);
            yield return Frames(6);
            Assert.That(_player.IsSprinting, Is.True);
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            Assert.That(_player.IsStunned, Is.True, "staggered");
            Assert.That(_player.IsSprinting, Is.False, "the sprint ends");
            Vector3 at = _player.Position;
            float hitAt = Time.time;
            int moved = -1;
            for (int f = 0; f < 60 && moved < 0; f++)
            {
                _player.Steer(way);
                yield return null;
                if (Flat(_player.Position - at).magnitude > 0.02f) moved = f;
            }
            float still = Time.time - hitAt;
            Assert.That(still, Is.EqualTo(_feel.SprintStagger).Within(2.5f * Frame), "she stood still for the stagger's time: " + still);
            int started = _player.ActionsStarted;

            // Running, the key up: a hit does not hold her.
            _player.HoldSprint(false);
            _player.Teleport(a);
            yield return Frames(2);
            _player.Steer(way);
            yield return Frames(6);
            Assert.That(_player.IsSprinting, Is.False);
            at = _player.Position;
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            Assert.That(_player.IsStunned, Is.False, "no stagger");
            _player.Steer(way);
            yield return Frames(3);
            Time.captureDeltaTime = 0f;
            Assert.That(Flat(_player.Position - at).magnitude, Is.GreaterThan(0.2f), "she ran on");
            Assert.That(_player.SprintStaggers, Is.EqualTo(1));
        }

        // ---------------- Its frame ----------------

        [UnityTest]
        public IEnumerator AFrameOfTheSprint_AndItsLookForAJump_AllocatesNothing()
        {
            Assert.That(FindJumpable(out Shelter prop, out Vector3 from, out Vector3 way), Is.True);
            _player.Teleport(from);
            yield return null;
            _player.HoldSprint(true);
            _player.Steer(way);
            yield return Frames(3);
            Assert.That(_player.IsSprinting, Is.True);
            var look = (System.Func<FeelValues, float, bool>)System.Delegate.CreateDelegate(typeof(System.Func<FeelValues, float, bool>), _player,
                typeof(PlayerCharacter).GetMethod("TryJump", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            FeelValues feel = _player.Feel;
            for (int i = 0; i < 5; i++) look(feel, Time.time);
            Assert.That(() => { for (int i = 0; i < 200; i++) look(feel, Time.time); },
                        UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not), "the look for a jump allocates nothing");
        }

        // ---------------- Helpers ----------------

        /// <summary>
        /// Runs her from <paramref name="from"/> along <paramref name="way"/> for <paramref name="seconds"/> of game time at 60 fps,
        /// sprinting or not; <paramref name="each"/> after every frame.
        /// </summary>
        IEnumerator RunAt(Vector3 from, Vector3 way, bool sprint, float seconds, System.Action each = null)
        {
            _player.Teleport(from);
            yield return null;
            Time.captureDeltaTime = Frame;
            _player.HoldSprint(sprint);
            int frames = Mathf.RoundToInt(seconds / Frame);
            for (int f = 0; f < frames; f++)
            {
                _player.Steer(way);
                yield return null;
                each?.Invoke();
            }
            _player.HoldSprint(false);
            _player.Stop();
            yield return null;
            Time.captureDeltaTime = 0f;
        }

        /// <summary>Two seconds of game time to and fro between two points, the metres she covered summed frame by frame.</summary>
        IEnumerator PingPong(Vector3 a, Vector3 b, bool sprint, System.Action<float> done)
        {
            _player.Teleport(a);
            yield return null;
            Time.captureDeltaTime = Frame;
            _player.HoldSprint(sprint);
            Vector3 way = Flat(b - a).normalized, target = b;
            float metres = 0f;
            Vector3 last = _player.Position;
            for (int f = 0; f < 120; f++)
            {
                if (Flat(target - _player.Position).magnitude < 0.6f)
                {
                    target = target == b ? a : b;
                    way = -way;
                }
                _player.Steer(way);
                yield return null;
                metres += Flat(_player.Position - last).magnitude;
                last = _player.Position;
            }
            _player.HoldSprint(false);
            _player.Stop();
            yield return null;
            Time.captureDeltaTime = 0f;
            done(metres);
        }

        /// <summary>A straight stretch of free floor at least 8 m long in a combat room, through one of its enemies' points.</summary>
        bool FreeLine(out Vector3 a, out Vector3 b)
        {
            float best = 0f;
            a = b = Vector3.zero;
            foreach (LevelRoom room in _level.Layout.Rooms)
            {
                if (room.Kind != RoomKind.Combat) continue;
                foreach (LevelPoint q in room.EnemyPoints)
                {
                    Vector3 p = _level.ToWorld(q);
                    if (!NavMesh.SamplePosition(p, out NavMeshHit on, 0.5f, NavMesh.AllAreas)) continue;
                    p = on.position;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 d = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                        float fwd = Free(p, d), back = Free(p, -d);
                        if (fwd + back <= best) continue;
                        best = fwd + back;
                        a = p - d * Mathf.Max(0f, back - 1f);
                        b = p + d * Mathf.Max(0f, fwd - 1f);
                    }
                }
            }
            return Flat(b - a).magnitude >= 8f;
        }

        static float Free(Vector3 p, Vector3 d) => NavMesh.Raycast(p, p + d * 30f, out NavMeshHit hit, NavMesh.AllAreas) ? hit.distance : 30f;

        /// <summary>
        /// A low prop the floor placed that the sprint's rule jumps, with a run-up of free floor before it: where she starts (2.5 m
        /// before the point the jump would go from) and the way.
        /// </summary>
        bool FindJumpable(out Shelter prop, out Vector3 from, out Vector3 way) => FindProp(low: true, out prop, out from, out way);

        bool FindTall(out Shelter prop, out Vector3 from, out Vector3 way) => FindProp(low: false, out prop, out from, out way);

        bool FindProp(bool low, out Shelter prop, out Vector3 from, out Vector3 way)
        {
            var shapes = new List<JumpShape>();
            foreach (Shelter s in Shelter.All)
                if (s != null && s.TryShape(out CoverShape shape)) shapes.Add(new JumpShape(shape, s.LowProp && !s.IsTall));
            foreach (Shelter s in Shelter.All.OrderBy(x => x.name, System.StringComparer.Ordinal))
            {
                if (s == null || !s.name.StartsWith("Prop_") || !s.TryShape(out CoverShape shape)) continue;
                bool isLow = s.LowProp && !s.IsTall;
                if (isLow != low || (!low && !s.IsTall)) continue;
                Vector3 c = new Vector3(shape.Centre.X, 0f, shape.Centre.Z);
                foreach (Vector3 axis in new[] { s.transform.forward, -s.transform.forward, s.transform.right, -s.transform.right })
                {
                    Vector3 d = Flat(axis).normalized;
                    var g = new GroundPoint(d.x, d.z);
                    float half = shape.Exit(shape.Centre, g * -1f);
                    if (half <= 0f) continue;
                    Vector3 takeOff = c - d * (half + 0.75f);
                    Vector3 start = takeOff - d * 2.5f;
                    if (!NavMesh.SamplePosition(start, out NavMeshHit on, 0.1f, NavMesh.AllAreas)) continue;
                    if (NavMesh.Raycast(start, takeOff, out NavMeshHit _, NavMesh.AllAreas)) continue; // the run-up is free
                    if (low)
                    {
                        JumpPlan plan = SprintRules.Decide(_feel, new GroundPoint(takeOff.x, takeOff.z), g, shapes, null, Walkable, Cover.Line);
                        if (!plan.Jumps) continue;
                    }
                    prop = s;
                    from = start;
                    way = d;
                    return true;
                }
            }
            prop = null;
            from = way = Vector3.zero;
            return false;
        }

        static bool Walkable(GroundPoint p)
        {
            var at = new Vector3(p.X, 0f, p.Z);
            return NavMesh.SamplePosition(at, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) && Flat(hit.position - at).magnitude <= 0.15f;
        }

        /// <summary>The floor's shooter kept to loose an arrow by hand (it does nothing of its own).</summary>
        EnemyShooter KeepAShooter()
        {
            Assume.That(_shooter, Is.Not.Null, "a shooter on the floor");
            return _shooter;
        }

        /// <summary>Its arrow, from its attacks (the one named so, else the first that does damage).</summary>
        static EnemyAttackDefinition Arrow(Enemy e)
        {
            EnemyAttackDefinition a = e.Values.Attacks.FirstOrDefault(x => x != null && x.Name == ShooterValues.Arrow)
                                      ?? e.Values.Attacks.FirstOrDefault(x => x != null && x.Damage > 0);
            Assert.That(a, Is.Not.Null, "its arrow");
            return a;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
