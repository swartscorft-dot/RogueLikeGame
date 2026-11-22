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
    /// Work order 024 (2026-11-20), the effects for everything that had none, in the ControlRoom scene, through the
    /// intents and the ways the other tests make enemies act (the practice room's spawns, hurting them, moving her): each
    /// moment draws its effect once, at its place, in its colours, and none when it did not happen; a counterable strike
    /// that lands is blue and a plain one red; the four pool skills have looks of their own; a thousand pieces leave
    /// nothing behind; the off value gives back the stand-ins and draws none of the new ones; and a fight deals and takes
    /// the same damage in the same time with the effects on and off. All through what was drawn (<see cref="CombatEffects.Spawned"/>,
    /// its shape, place, size and colours), as the shot effect tests do: nobody but Kristian can see whether they look right.
    ///
    /// The practice brute is switched off unless a test needs it. Waits are in frames, game time or real time with a
    /// limit, never frames of a supposed length.
    /// </summary>
    public class CombatEffectsTests
    {
        const ulong RunSeed = 0x12345678UL;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here
        static readonly Vector3 OpenGround = new Vector3(-8f, 0f, -8f);

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a;
        EnemyBrute _brute;
        GeneratedLevels _levels;
        CombatEffects _fx;
        readonly List<Seen> _seen = new List<Seen>();
        readonly List<ShotSeen> _shots = new List<ShotSeen>();
        readonly HashSet<ShotFx> _knownShots = new HashSet<ShotFx>();

        /// <summary>A shot effect as it was in the frame it appeared.</summary>
        struct ShotSeen
        {
            public ShotFxKind Kind;
            public float Size, CoreBrightness;
        }

        /// <summary>A piece as it was made, and when.</summary>
        struct Seen
        {
            public CombatFx Fx;
            public FxPiece P;
            public int Frame;
            public float At;
        }

        CombatFxValues V => _fx.Values;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            CombatEffects.Spawned += Record;
            yield return Load();
            ThingsThatFly.Ensure().Values.On = false; // work order 036: these are the tests of the attacks as they were
            _fx = CombatEffects.Ensure();
            yield return null;
            _seen.Clear();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.MeleeYouRead = false; // work order 035: these tests are of the swarmer's, brute's and guard's attacks from before
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
            CombatEffects.Spawned -= Record;
            if (_fx != null) Object.Destroy(_fx.gameObject);
            _fx = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _seen.Clear();
            _shots.Clear();
            _knownShots.Clear();
        }

        void Record(CombatFx fx) => _seen.Add(new Seen { Fx = fx, P = fx.Piece, Frame = Time.frameCount, At = Time.time });

        // ---------------- Helpers ----------------

        List<Seen> Of(FxMoment m) => _seen.Where(s => s.P.Moment == m).ToList();
        List<Seen> Of(FxMoment m, FxShape shape) => _seen.Where(s => s.P.Moment == m && s.P.Shape == shape).ToList();
        string Drawn => string.Join(", ", _seen.Select(s => s.P.Moment + "/" + s.P.Shape + "@" + s.Frame));

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

        static IEnumerator FramesUntil(Func<bool> condition)
        {
            for (int i = 0; i < 600 && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>Until the condition holds, noting the frame it first held in; that frame's end follows.</summary>
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
            float deadline = Time.realtimeSinceStartup + seconds + 5f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 5f);

        /// <summary>Until the condition holds, noting every shot effect new in each frame.</summary>
        IEnumerator WatchingShots(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                foreach (ShotFx s in Object.FindObjectsByType<ShotFx>())
                    if (_knownShots.Add(s)) _shots.Add(new ShotSeen { Kind = s.Kind, Size = s.Size, CoreBrightness = s.CoreBrightness });
                if (condition() || Time.realtimeSinceStartup >= deadline) yield break;
                yield return null;
            }
        }

        T SpawnOne<T>(EnemyKind kind) where T : Enemy
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            return (T)made[0];
        }

        EnemyBoss SpawnBoss() => SpawnOne<EnemyBoss>(EnemyKind.Boss);

        /// <summary>The practice brute on, her this far from its home, toward where she stands.</summary>
        void BruteAt(float distance)
        {
            _brute.enabled = true;
            Vector3 home = _brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * distance);
        }

        int StandIns() => Object.FindObjectsByType<MeshRenderer>().Count(r => r.name == "StrikeFx");

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Paint(Seen s, Color rim, Color core, string what)
        {
            Assert.That(s.P.Rim, Is.EqualTo(rim), what + ": its rim");
            Assert.That(s.P.Core, Is.EqualTo(core), what + ": its core");
        }

        static bool IsRed(Color c) => c.r > 0.8f && c.g < 0.4f && c.b < 0.4f;
        static bool IsBlue(Color c) => c.b > 0.8f && c.r < 0.4f && c.b > c.g;
        static bool IsCyan(Color c) => c.r < 0.4f && c.g > 0.7f && c.b > 0.7f;
        static bool IsWarm(Color c) => c.r > 0.9f && c.g > 0.3f && c.b < 0.4f;

        // ---------------- Which report is which moment ----------------

        [Test]
        public void EachEnemyReport_IsItsMoment_OrNone()
        {
            var table = new Dictionary<EnemyReportKind, FxMoment?>
            {
                { EnemyReportKind.Woke, null }, { EnemyReportKind.AttackStarted, FxMoment.WindupStarted },
                { EnemyReportKind.Struck, FxMoment.StrikeHitHer }, { EnemyReportKind.GaveUp, null },
                { EnemyReportKind.Died, FxMoment.EnemyDied }, { EnemyReportKind.Removed, null },
                { EnemyReportKind.StaggerTaken, null }, { EnemyReportKind.Dazed, FxMoment.EnemyDazed },
                { EnemyReportKind.DazeEnded, null }, { EnemyReportKind.Charged, FxMoment.BossChargeStep },
                { EnemyReportKind.CalledPack, FxMoment.BossCalled }, { EnemyReportKind.Angered, FxMoment.BossAngered },
                // Work order 034, added on purpose: work order 027's caster reports have no effect of their own (its cast
                // is AttackStarted, each burst is drawn at its circle as it goes off and Struck when it lands, the
                // counter's daze is Dazed).
                { EnemyReportKind.CirclePlaced, null }, { EnemyReportKind.CircleOffGround, null },
                { EnemyReportKind.CircleFaded, null }, { EnemyReportKind.Countered, null },
                // Work order 073, added on purpose: the bomber's blast on the others has no effect of its own (its blast is
                // drawn where it goes off, as a caster's circle, and Struck when it lands on her).
                { EnemyReportKind.Blasted, null },
                // Work order 079, added on purpose: the warder draws its wards itself (its glow, the thread, the shell and its
                // burst); the warded are drawn dazed as a ward breaks.
                { EnemyReportKind.WardRaising, null }, { EnemyReportKind.WardRaised, null }, { EnemyReportKind.WardBroken, null },
            };
            foreach (EnemyReportKind kind in Enum.GetValues(typeof(EnemyReportKind)))
            {
                Assert.That(table.ContainsKey(kind), Is.True, kind + " is in the table");
                Assert.That(CombatEffects.MomentOf(kind), Is.EqualTo(table[kind]), kind.ToString());
            }
            Assert.That(CombatEffects.MomentOf(EnemyReportKind.Died, EnemyKind.Boss), Is.EqualTo(FxMoment.BossDied));

            var skills = SkillDefaults.Create();
            SkillDefinition Named(string n) => skills.First(s => s.Name == n);
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.Hit, Damage = 10, Countered = true }, Named("Stopping Shot")), Is.EqualTo(FxMoment.Countered));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.Hit, Damage = 10 }, Named("Stopping Shot")), Is.Null, "a plain hit: the shot's own spark");
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.Hit, Damage = 10 }, Named("Breaker Slug")), Is.EqualTo(FxMoment.BreakerSlugHit));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillFired, Clean = true }, Named("Long Shot")), Is.EqualTo(FxMoment.PerfectRelease));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillFired, Clean = false }, Named("Long Shot")), Is.Null);
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillFired, Clean = true }, Named("Powder Keg")), Is.EqualTo(FxMoment.KegSetOffByShot));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillFired, Landed = true }, Named("Tag Round")), Is.EqualTo(FxMoment.TagRoundTagged));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillStarted }, Named("Slide")), Is.EqualTo(FxMoment.SlideStarted));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.SkillEnded }, Named("Slide")), Is.EqualTo(FxMoment.SlideEnded));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.MarksSpent, Marks = 3 }, null), Is.EqualTo(FxMoment.MarksSpent));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.MarkAdded, Marks = 1 }, null), Is.EqualTo(FxMoment.MarkAdded));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.DashStarted }, null), Is.EqualTo(FxMoment.Dashed));
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.DashPressed }, null), Is.Null, "a press is not a dash");
            Assert.That(CombatEffects.MomentOf(new PlayerReport { Kind = PlayerReportKind.ShotFired }, null), Is.Null, "the gun has its own effects");
        }

        // ---------------- 1, 2: strikes and windups ----------------

        [UnityTest]
        public IEnumerator APlainCircleStrike_IsARedRingToItsEdge_AndAStar_ItsHitOnHerARedSpark_AndNoStandIn()
        {
            BruteAt(2.5f);
            int struckAt = -1, standIns = -1;
            int hp = _health.Current;
            yield return Watch(() => _brute.Strikes > 0, 8f, f => { struckAt = f; standIns = StandIns(); });
            Assume.That(struckAt, Is.GreaterThan(0), "the brute struck");
            Assume.That(_brute.Attack?.Name, Is.EqualTo("Slam"));
            EnemyAttackDefinition slam = _brute.Attack;
            Assert.That(PoolRules.IsCounterable(slam), Is.False, "a red attack");
            Assert.That(standIns, Is.EqualTo(0), "no flat stand-in");
            List<Seen> rings = Of(FxMoment.EnemyStruck, FxShape.Ring), stars = Of(FxMoment.EnemyStruck, FxShape.Spark);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(stars.Count, Is.EqualTo(1), Drawn);
            Assert.That(Of(FxMoment.EnemyStruck).Count, Is.EqualTo(2), "and nothing else for the strike");
            Assert.That(FlatDistance(rings[0].P.At, _brute.transform.position), Is.LessThan(0.05f), "from the enemy");
            Assert.That(rings[0].P.Size, Is.EqualTo(slam.Radius).Within(1e-4f), "to the edge of its circle");
            Assert.That(FlatDistance(stars[0].P.At, _brute.transform.position), Is.LessThan(0.05f), "the star at the centre");
            Paint(rings[0], V.StrikeRim, V.StrikeCore, "the ring");
            Paint(stars[0], V.StrikeRim, V.StrikeCore, "the star");
            Assert.That(IsRed(rings[0].P.Rim), Is.True, "red");
            Assert.That(rings[0].Frame, Is.EqualTo(struckAt), "in the frame of the strike");

            Assume.That(_health.Current, Is.LessThan(hp), "it hit her");
            List<Seen> onHer = Of(FxMoment.StrikeHitHer);
            Assert.That(onHer.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(onHer[0].P.At, _player.Position), Is.LessThan(0.3f), "on her");
            Assert.That(IsRed(onHer[0].P.Rim), Is.True, "red");
            Assert.That(Of(FxMoment.HerHurt), Is.Empty, "the strike's spark is her hurt spark: one, not two");
            _brute.enabled = false;
        }

        [UnityTest]
        public IEnumerator ACounterableLine_WindsUpBlue_StrikesAsABlueStreakWithSideSpikes_AndItsHitOnHerIsBlue()
        {
            BruteAt(7f);
            int windupAt = -1;
            yield return Watch(() => _brute.State == EnemyState.Windup, 6f, f => windupAt = f);
            Assume.That(windupAt, Is.GreaterThan(0), "the brute winds up");
            Assume.That(_brute.Attack?.Name, Is.EqualTo("Shockwave"));
            EnemyAttackDefinition wave = _brute.Attack;
            Assert.That(PoolRules.IsCounterable(wave), Is.True, "blue");
            List<Seen> windups = Of(FxMoment.WindupStarted);
            Assert.That(windups.Count, Is.EqualTo(1), Drawn);
            Assert.That(windups[0].P.Shape, Is.EqualTo(FxShape.Converge), "spikes pulling in");
            Assert.That(windups[0].Frame, Is.EqualTo(windupAt), "at its start");
            Assert.That(FlatDistance(windups[0].P.At, _brute.transform.position), Is.LessThan(0.05f), "on the enemy");
            Assert.That(windups[0].P.At.y, Is.GreaterThan(0.3f), "on its body, not the ground");
            Paint(windups[0], V.CounterRim, V.CounterCore, "the windup");
            Assert.That(Of(FxMoment.EnemyStruck), Is.Empty, "no strike yet");

            int hp = _health.Current, struckAt = -1;
            yield return Watch(() => _brute.Strikes > 0, 6f, f => struckAt = f);
            Assume.That(struckAt, Is.GreaterThan(0), "it struck");
            List<Seen> streaks = Of(FxMoment.EnemyStruck, FxShape.Streak), sides = Of(FxMoment.EnemyStruck, FxShape.SideSpikes);
            Assert.That(streaks.Count, Is.EqualTo(1), Drawn);
            Assert.That(sides.Count, Is.EqualTo(1), Drawn);
            Vector3 dir = Flat(_brute.AimPoint - _brute.transform.position).normalized;
            Assert.That(FlatDistance(streaks[0].P.At, _brute.transform.position), Is.LessThan(0.05f), "from the enemy");
            Assert.That(FlatDistance(streaks[0].P.End, _brute.transform.position + dir * wave.Length), Is.LessThan(0.1f), "down its whole length");
            Assert.That(sides[0].P.Size, Is.EqualTo(wave.Length).Within(0.05f));
            Assert.That(sides[0].P.Angle, Is.EqualTo(wave.HalfWidth).Within(1e-4f), "off the sides of its strip");
            Paint(streaks[0], V.CounterRim, V.CounterCore, "a counterable strike not countered");
            Assert.That(IsBlue(sides[0].P.Rim), Is.True, "blue");

            Assume.That(_health.Current, Is.LessThan(hp), "it hit her");
            List<Seen> onHer = Of(FxMoment.StrikeHitHer);
            Assert.That(onHer.Count, Is.EqualTo(1), Drawn);
            Assert.That(IsBlue(onHer[0].P.Rim), Is.True, "a counterable strike that lands is blue");
            _brute.enabled = false;
        }

        [UnityTest]
        public IEnumerator ACone_IsAJaggedWedgeOverIt_InItsTelegraphsColour_AsIsItsWindup()
        {
            var guard = SpawnOne<EnemyGuard>(EnemyKind.Guard);
            guard.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            guard.Body.TakeHit(1);
            EnemyAttackDefinition sweep = guard.Tuning.Enemy.Attacks.First(a => a.Shape == EnemyAttackShape.Cone);
            _player.Teleport(guard.transform.position + Vector3.back * (0.5f * (sweep.MinDistance + sweep.MaxDistance)));
            yield return null;
            Assert.That(Of(FxMoment.WindupStarted), Is.Empty, "none before a windup");
            yield return Until(() => guard.Strikes > 0, 8f);
            Assume.That(guard.Strikes, Is.GreaterThan(0), "it struck");
            Assume.That(guard.Attack?.Name, Is.EqualTo(sweep.Name));
            List<Seen> windups = Of(FxMoment.WindupStarted);
            Assert.That(windups.Count, Is.GreaterThanOrEqualTo(1));
            // The guard's sweep is counterable (work order 015): blue, as its telegraph.
            bool blue = PoolRules.IsCounterable(sweep);
            Color rim = blue ? V.CounterRim : V.StrikeRim, core = blue ? V.CounterCore : V.StrikeCore;
            Paint(windups.Last(), rim, core, "the windup in its telegraph's colour");
            Assert.That(windups.Last().P.Size, Is.EqualTo(V.WindupSize * V.Sizes.KindScale(EnemyKind.Guard)).Within(1e-4f), "the guard's size");
            List<Seen> wedges = Of(FxMoment.EnemyStruck, FxShape.Wedge);
            Assert.That(wedges.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(wedges[0].P.At, guard.transform.position), Is.LessThan(0.05f), "from its apex at the enemy");
            Assert.That(wedges[0].P.Angle, Is.EqualTo(sweep.Angle).Within(1e-4f), "over its cone");
            Assert.That(wedges[0].P.Size, Is.EqualTo(sweep.Length).Within(0.05f), "to its reach");
            Vector3 dir = Flat(guard.AimPoint - guard.transform.position).normalized;
            Assert.That(Vector3.Dot(wedges[0].P.Direction.normalized, dir), Is.GreaterThan(0.99f), "where it aimed");
            Paint(wedges[0], rim, core, "in its telegraph's colour");
            Object.Destroy(guard.gameObject);
        }

        // ---------------- 3: deaths ----------------

        [UnityTest]
        public IEnumerator ADeath_BurstsBoneShards_ThenAPaleRingWhereItLay_AHitThatLeavesItAlive_None()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            shooter.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            shooter.Body.TakeHit(1);
            yield return Frames(3);
            Assert.That(Of(FxMoment.EnemyDied), Is.Empty, "alive");
            Vector3 lay = shooter.transform.position;
            shooter.Body.TakeHit(100000);
            yield return Frames(3);
            Assert.That(shooter.IsDead, Is.True);
            List<Seen> shards = Of(FxMoment.EnemyDied, FxShape.Shards), stars = Of(FxMoment.EnemyDied, FxShape.Spark);
            Assert.That(shards.Count, Is.EqualTo(1), Drawn);
            Assert.That(stars.Count, Is.EqualTo(1), Drawn);
            Assert.That(shards[0].P.Count, Is.EqualTo(V.Sizes.ShardCount(EnemyKind.Shooter)), "five to eight");
            Assert.That(shards[0].P.Count, Is.InRange(5, 8));
            Assert.That(FlatDistance(shards[0].P.At, lay), Is.LessThan(0.05f), "where it died");
            Paint(shards[0], V.BoneRim, V.BoneCore, "bone");
            Assert.That(shards[0].P.Seconds, Is.LessThan(0.5f), "short");
            Assert.That(Of(FxMoment.BodyHidden), Is.Empty, "the body is still seen");

            float shown = shooter.ShownDeadFor(shooter.Values);
            yield return Until(() => Of(FxMoment.BodyHidden).Count > 0, shown + 3f);
            List<Seen> rings = Of(FxMoment.BodyHidden);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(rings[0].P.Shape, Is.EqualTo(FxShape.Outline), "a thin ring");
            Assert.That(rings[0].At - shards[0].At, Is.EqualTo(shown).Within(0.1f), "when the body is hidden");
            Assert.That(FlatDistance(rings[0].P.At, lay), Is.LessThan(0.05f), "where it lay");
            Paint(rings[0], V.BoneRim, V.BoneCore, "pale");
            yield return Frames(5);
            Assert.That(Of(FxMoment.EnemyDied).Count, Is.EqualTo(2), "once");
        }

        [UnityTest]
        public IEnumerator ADeath_IsSmallForASwarmer_LargerForAGuard()
        {
            List<Enemy> pack = _levels.SpawnPractice(EnemyKind.Swarmer);
            var guard = SpawnOne<EnemyGuard>(EnemyKind.Guard);
            yield return Frames(2);
            pack[0].Body.TakeHit(100000);
            guard.Body.TakeHit(100000);
            yield return Frames(3);
            List<Seen> shards = Of(FxMoment.EnemyDied, FxShape.Shards);
            Assert.That(shards.Count, Is.EqualTo(2), Drawn);
            Seen small = shards.First(s => FlatDistance(s.P.At, pack[0].transform.position) < 0.05f);
            Seen large = shards.First(s => FlatDistance(s.P.At, guard.transform.position) < 0.05f);
            FxSizes sizes = V.Sizes;
            Assert.That(large.P.Size / small.P.Size, Is.EqualTo(sizes.KindScale(EnemyKind.Guard) / sizes.KindScale(EnemyKind.Swarmer)).Within(1e-3f));
            Assert.That(small.P.Count, Is.LessThan(large.P.Count), "fewer shards for the small one");
            _levels.ClearSpawned();
        }

        // ---------------- 4, 9: the boss's call and the pack appearing ----------------

        [UnityTest]
        public IEnumerator TheBossesCall_IsARingFromIt_AndThePackAppears_EnemiesMadeOutOfAFight_DoNot()
        {
            _levels.SpawnPractice(EnemyKind.Swarmer);
            yield return Frames(3);
            Assert.That(Of(FxMoment.EnemyAppeared), Is.Empty, "made with no fight on: no arrival drawn");

            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            Assert.That(Of(FxMoment.EnemyAppeared), Is.Empty, "nor the boss itself");
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f)); // under two thirds: a call
            yield return Frames(3);
            Assert.That(boss.PacksCalled, Is.EqualTo(1));
            List<Seen> call = Of(FxMoment.BossCalled);
            Assert.That(call.Count, Is.EqualTo(1), Drawn);
            Assert.That(call[0].P.Shape, Is.EqualTo(FxShape.Ring));
            Assert.That(FlatDistance(call[0].P.At, boss.transform.position), Is.LessThan(0.1f), "from it");
            Paint(call[0], V.BossRim, V.BossCore, "its colour");

            List<Seen> closing = Of(FxMoment.EnemyAppeared, FxShape.Closing), spikes = Of(FxMoment.EnemyAppeared, FxShape.Spike);
            Assert.That(boss.Called.Count, Is.GreaterThan(0));
            Assert.That(closing.Count, Is.EqualTo(boss.Called.Count), "a closing ring for each that came: " + Drawn);
            Assert.That(spikes.Count, Is.EqualTo(boss.Called.Count), "and a spike up");
            foreach (Enemy e in boss.Called)
                Assert.That(closing.Count(s => FlatDistance(s.P.At, e.transform.position) < 0.3f), Is.EqualTo(1), e.name + ": on its spot");
            Paint(closing[0], V.BossRim, V.BossCore, "the boss's colour");
            Assert.That(closing[0].P.Size, Is.EqualTo(V.AppearRing * V.Sizes.KindScale(EnemyKind.Swarmer)).Within(1e-4f), "a swarmer's size");
            _levels.ClearSpawned();
        }

        // ---------------- 5: dazed ----------------

        [UnityTest]
        public IEnumerator ADaze_IsAnOrbitOfLightBlueStarsOverItsHead_ForAsLongAsItLasts_ABarNotEmptied_None()
        {
            var guard = SpawnOne<EnemyGuard>(EnemyKind.Guard);
            guard.Tuning.Enemy.MoveSpeed = 0f;
            yield return Frames(2);
            guard.Body.TakeHit(1, guard.Tuning.Stagger.Max * 0.3f);
            yield return Frames(3);
            Assert.That(guard.IsDazed, Is.False);
            Assert.That(Of(FxMoment.EnemyDazed), Is.Empty);
            guard.Body.TakeHit(1, guard.Tuning.Stagger.Max);
            yield return Frames(2);
            Assert.That(guard.IsDazed, Is.True);
            List<Seen> orbit = Of(FxMoment.EnemyDazed);
            Assert.That(orbit.Count, Is.EqualTo(1), Drawn);
            Assert.That(orbit[0].P.Shape, Is.EqualTo(FxShape.Orbit));
            Assert.That(orbit[0].P.Follow, Is.SameAs(guard.transform), "it stays with it");
            Assert.That(FlatDistance(orbit[0].P.At, guard.transform.position), Is.LessThan(0.05f));
            float head = guard.ModelHeight > 0f ? guard.ModelHeight : guard.Tuning.BodyHeight;
            Assert.That(orbit[0].P.At.y, Is.GreaterThan(head), "over its head");
            Paint(orbit[0], V.DazedRim, V.DazedCore, "light blue");
            yield return For(guard.Tuning.Stagger.DazeSeconds * 0.5f);
            Assert.That(orbit[0].Fx != null && orbit[0].Fx.IsHolding, Is.True, "still turning while it is dazed");
            Assert.That(Of(FxMoment.EnemyDazed).Count, Is.EqualTo(1), "once");
            yield return Until(() => !guard.IsDazed, guard.Tuning.Stagger.DazeSeconds + 3f);
            yield return For(V.DazedFade + 0.1f);
            yield return Frames(2);
            Assert.That(orbit[0].Fx == null, Is.True, "gone with the daze");
            Object.Destroy(guard.gameObject);
        }

        // ---------------- 6: the shooter's arrow ----------------

        [UnityTest]
        public IEnumerator TheShootersShot_IsAThinPaleArrowDownItsLine_OverItsRedStrike()
        {
            var shooter = SpawnOne<EnemyShooter>(EnemyKind.Shooter);
            yield return null;
            shooter.Body.TakeHit(1);
            int shotAt = -1;
            yield return Watch(() => shooter.Strikes > 0, 6f, f => shotAt = f);
            Assume.That(shotAt, Is.GreaterThan(0), "the shooter shot");
            List<Seen> arrows = Of(FxMoment.ArrowLoosed);
            Assert.That(arrows.Count, Is.EqualTo(1), Drawn);
            Assert.That(arrows[0].Frame, Is.EqualTo(shotAt), "as it is let go");
            Assert.That(arrows[0].P.Shape, Is.EqualTo(FxShape.Streak));
            Assert.That(FlatDistance(arrows[0].P.At, shooter.transform.position), Is.LessThan(0.05f), "from the shooter");
            Assert.That(arrows[0].P.At.y, Is.EqualTo(shooter.Tuning.SightHeight).Within(0.05f), "at the height it sees from");
            Assert.That(arrows[0].P.Size, Is.EqualTo(V.ArrowWidth).Within(1e-4f), "thin");
            Paint(arrows[0], V.ArrowRim, V.ArrowCore, "pale");
            Assert.That(Of(FxMoment.EnemyStruck, FxShape.Streak).Count, Is.EqualTo(1), "and the line's own strike");
            Assert.That(IsRed(Of(FxMoment.EnemyStruck, FxShape.Streak)[0].P.Rim), Is.True, "red");
            Object.Destroy(shooter.gameObject);
        }

        // ---------------- 7, 8, 10: the boss ----------------

        [UnityTest]
        public IEnumerator TheBossesCharge_KicksDustEveryHalfMetre_TrailsAStreak_AndEndsInARing()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.CallsAt = new float[0]; // no packs in the way here
            yield return Frames(2);
            boss.Body.TakeHit(1);
            yield return Until(() => boss.State == EnemyState.Windup && boss.Attack != null && boss.Attack.Name == BossValues.Charge, 8f);
            Assume.That(boss.Attack?.Name, Is.EqualTo(BossValues.Charge), "a charge at her 12 m away");
            yield return Until(() => boss.IsCharging, 3f);
            Assert.That(boss.IsCharging, Is.True, "it runs");
            Vector3 from = Flat(boss.ChargeFrom), dir = Flat(boss.ChargeDirection).normalized;
            float reach = boss.ChargeReach;
            yield return Until(() => !boss.IsCharging, 4f);
            yield return Frames(2);
            List<Seen> trail = Of(FxMoment.BossChargeStep, FxShape.Streak);
            Assert.That(trail.Count, Is.EqualTo(1), "one trail: " + Drawn);
            Assert.That(trail[0].P.Follow, Is.SameAs(boss.transform), "it trails the boss");
            List<Seen> dust = Of(FxMoment.BossChargeStep, FxShape.Wedge);
            int expected = Mathf.FloorToInt(reach / CombatFxRules.ChargeDustEvery + 1e-3f);
            Assert.That(dust.Count, Is.InRange(expected - 1, expected), "every half metre of " + reach + " m");
            for (int i = 0; i < dust.Count; i++)
            {
                Assert.That(FlatDistance(dust[i].P.At, from + dir * ((i + 1) * CombatFxRules.ChargeDustEvery)), Is.LessThan(0.05f), "dust " + i + " at its feet");
                Assert.That(Vector3.Dot(dust[i].P.Direction.normalized, -dir), Is.GreaterThan(0.99f), "kicked back");
            }
            Paint(dust[0], V.DustRim, V.DustCore, "dust");
            List<Seen> end = Of(FxMoment.BossChargeEnded);
            Assert.That(end.Count, Is.EqualTo(1), Drawn);
            Assert.That(end[0].P.Shape, Is.EqualTo(FxShape.Ring));
            Assert.That(FlatDistance(end[0].P.At, boss.transform.position), Is.LessThan(0.3f), "where it stopped");
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator TheBossesAnger_IsALargeRingAndATallStar_InItsWarmRedOrange_Once_NeverDropped()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            boss.Tuning.CallsAt = new float[0];
            yield return Frames(2);
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.45f)); // under half: angry
            yield return Frames(3);
            Assert.That(boss.IsAngry, Is.True);
            List<Seen> rings = Of(FxMoment.BossAngered, FxShape.Ring), stars = Of(FxMoment.BossAngered, FxShape.Spike);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(stars.Count, Is.EqualTo(1), Drawn);
            Assert.That(rings[0].P.Size, Is.EqualTo(V.AngerRing).Within(1e-4f), "large");
            Assert.That(stars[0].P.Size, Is.EqualTo(V.AngerSpike).Within(1e-4f), "tall");
            Assert.That(FlatDistance(rings[0].P.At, boss.transform.position), Is.LessThan(0.1f), "from it");
            Paint(rings[0], V.AngryRim, V.AngryCore, "its tint");
            Assert.That(rings[0].P.Tier, Is.EqualTo(FxTier.Key), "never dropped");
            yield return Frames(5);
            Assert.That(Of(FxMoment.BossAngered).Count, Is.EqualTo(2), "once");
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator TheBossesDeath_IsThreeStaggeredBurstsOverHalfASecond_AndAWideRing()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            boss.Tuning.CallsAt = new float[0];
            yield return Frames(2);
            boss.Body.TakeHit(10000000);
            yield return Frames(3);
            Assert.That(boss.IsDead, Is.True);
            List<Seen> ring = Of(FxMoment.BossDied, FxShape.Ring);
            Assert.That(ring.Count, Is.EqualTo(1), Drawn);
            Assert.That(ring[0].P.Size, Is.EqualTo(V.BossDeathRing).Within(1e-4f), "wide");
            yield return For(2f * V.BossBurstGap + 0.2f);
            List<Seen> bursts = Of(FxMoment.BossDied, FxShape.Shards);
            Assert.That(bursts.Count, Is.EqualTo(3), "three bursts: " + Drawn);
            Assert.That(bursts[1].At - bursts[0].At, Is.EqualTo(V.BossBurstGap).Within(0.05f), "staggered");
            Assert.That(bursts[2].At - bursts[0].At, Is.EqualTo(2f * V.BossBurstGap).Within(0.05f), "over half a second");
            Assert.That(bursts.All(b => b.P.Tier == FxTier.Key), Is.True, "never dropped");
            Assert.That(Of(FxMoment.EnemyDied), Is.Empty, "not an ordinary death");
            _levels.ClearSpawned();
        }

        // ---------------- 11: the four pool skills ----------------

        ShotLook OwnLook(string name) => (_player.ShotLooks != null ? _player.ShotLooks.For(name) : null) ?? ShotFxDefaults.For(name);

        IEnumerator FireAtA(string name)
        {
            yield return UntilFree();
            int slot = Slot(name);
            int hits = _a.HitsTaken;
            _shots.Clear();
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started), name);
            yield return WatchingShots(() => _a.HitsTaken > hits && _shots.Any(s => s.Kind == ShotFxKind.Streak), 3f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFourPoolSkills_HaveLooksOfTheirOwn_NotBorrowed()
        {
            foreach (string name in new[] { "Tag Round", "Breaker Slug", "Stopping Shot", "Slide" })
            {
                Assume.That(_player.ShotLooks == null || _player.ShotLooks.For(name) == null, Is.True, name + ": the asset has no look for it today");
                ShotLook own = ShotFxDefaults.For(name), borrowed = ShotFxDefaults.Borrowed(name);
                Assert.That(own.Name, Is.EqualTo(name));
                Assert.That(own.Rim != borrowed.Rim || own.StreakWidth != borrowed.StreakWidth || own.FlashSize != borrowed.FlashSize, Is.True,
                            name + " looks unlike " + ShotFxDefaults.Nearest(name));
            }
            Assert.That(ShotFxDefaults.For("Tag Round").Core.g, Is.GreaterThan(ShotFxDefaults.For("Tag Round").Core.r), "a green-tinted core");
            Assert.That(ShotFxDefaults.For("Breaker Slug").StreakWidth, Is.GreaterThan(ShotFxDefaults.For("Scatter Blast").StreakWidth * 2f), "very thick");
            Assert.That(ShotFxDefaults.For("Tag Round").StreakWidth, Is.LessThan(ShotFxDefaults.For("Quick Shot").StreakWidth), "thin");

            _player.Teleport(NearA);
            yield return null;
            foreach (string name in new[] { "Tag Round", "Breaker Slug", "Stopping Shot" })
            {
                yield return FireAtA(name);
                List<ShotSeen> streaks = _shots.Where(s => s.Kind == ShotFxKind.Streak).ToList();
                Assert.That(streaks.Count, Is.GreaterThan(0), name + " drew a streak");
                Assert.That(streaks[0].Size, Is.EqualTo(OwnLook(name).StreakWidth).Within(1e-4f), name + ": its own streak");
                ShotLook nearest = _player.ShotLooks != null ? _player.ShotLooks.For(ShotFxDefaults.Nearest(name)) : null;
                if (nearest != null && Mathf.Abs(nearest.StreakWidth - OwnLook(name).StreakWidth) > 1e-3f)
                    Assert.That(streaks[0].Size, Is.Not.EqualTo(nearest.StreakWidth).Within(1e-4f), name + ": not " + nearest.Name + "'s");
            }
        }

        [UnityTest]
        public IEnumerator TagRound_BurstsAGreenDiamondOnItsTarget_BreakerSlug_ARingOnWhatItHits()
        {
            _player.Teleport(NearA);
            yield return null;
            yield return FireAtA("Tag Round");
            Assert.That(_a.IsTagged, Is.True);
            List<Seen> diamonds = Of(FxMoment.TagRoundTagged);
            Assert.That(diamonds.Count, Is.EqualTo(1), Drawn);
            Assert.That(diamonds[0].P.Shape, Is.EqualTo(FxShape.Diamond));
            Assert.That(FlatDistance(diamonds[0].P.At, _a.transform.position), Is.LessThan(0.05f), "on the tagged target");
            Paint(diamonds[0], V.TagRim, V.TagCore, "green");
            Assert.That(Of(FxMoment.BreakerSlugHit), Is.Empty);

            yield return FireAtA("Breaker Slug");
            List<Seen> rings = Of(FxMoment.BreakerSlugHit);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(rings[0].P.Shape, Is.EqualTo(FxShape.Ring));
            Assert.That(FlatDistance(rings[0].P.At, _a.transform.position), Is.LessThan(0.05f), "on what it hit");
            Assert.That(IsWarm(rings[0].P.Rim), Is.True, "her colours: warm");
            Assert.That(Of(FxMoment.TagRoundTagged).Count, Is.EqualTo(1), "no second diamond");
        }

        [UnityTest]
        public IEnumerator Slide_KicksLowDustBackWhereSheStarts_AndLeavesAFaintStreakAlongHerPath()
        {
            int slide = Slot("Slide");
            SkillDefinition skill = _player.Skills[slide];
            _player.Teleport(OpenGround);
            yield return null;
            Vector3 from = _player.Position;
            _player.Steer(Vector3.right);
            Assert.That(_player.PressSkill(slide, from + Vector3.forward * 5f), Is.EqualTo(IntentResult.Started));
            _player.Stop();
            List<Seen> dust = Of(FxMoment.SlideStarted);
            Assert.That(dust.Count, Is.EqualTo(1), Drawn);
            Assert.That(dust[0].P.Shape, Is.EqualTo(FxShape.Wedge));
            Assert.That(FlatDistance(dust[0].P.At, from), Is.LessThan(0.05f), "where she starts");
            Assert.That(dust[0].P.At.y, Is.LessThan(0.1f), "low");
            Assert.That(Vector3.Dot(dust[0].P.Direction.normalized, Vector3.left), Is.GreaterThan(0.99f), "kicked back");
            Assert.That(Of(FxMoment.SlideEnded), Is.Empty, "not ended yet");
            yield return Until(() => !_player.IsKeyMoving, 2f);
            yield return null;
            List<Seen> trail = Of(FxMoment.SlideEnded);
            Assert.That(trail.Count, Is.EqualTo(1), Drawn);
            Assert.That(trail[0].P.Shape, Is.EqualTo(FxShape.Streak));
            Assert.That(FlatDistance(trail[0].P.At, from), Is.LessThan(0.05f));
            Assert.That(FlatDistance(trail[0].P.End, from + Vector3.right * skill.MoveDistance), Is.LessThan(0.2f), "along her path");
            Assert.That(trail[0].P.CoreGlow, Is.LessThan(V.CoreGlow), "faint");
            Assert.That(IsWarm(trail[0].P.Rim), Is.True, "her colours");
        }

        // ---------------- 12: a counter ----------------

        [UnityTest]
        public IEnumerator ACounter_IsABlueStarburstAndARing_LargerThanAHitSpark_TheBrightestThingOnScreen()
        {
            int stop = Slot("Stopping Shot");
            BruteAt(7f);
            yield return Until(() => _brute.State == EnemyState.Windup, 6f);
            Assume.That(_brute.Attack?.Name, Is.EqualTo("Shockwave"));
            Assert.That(_player.PressSkill(stop, _brute.transform.position), Is.EqualTo(IntentResult.Started));
            float brightestOther = 0f;
            yield return WatchingShots(() => _brute.Countered > 0, 3f);
            Assert.That(_brute.Countered, Is.EqualTo(1));
            List<Seen> stars = Of(FxMoment.Countered, FxShape.Spark), rings = Of(FxMoment.Countered, FxShape.Ring);
            Assert.That(stars.Count, Is.EqualTo(1), Drawn);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(stars[0].P.At, _brute.transform.position), Is.LessThan(0.1f), "on the countered enemy");
            Assert.That(FlatDistance(rings[0].P.At, _brute.transform.position), Is.LessThan(0.1f));
            Paint(stars[0], V.CounterRim, V.CounterCore, "blue");
            Assert.That(stars[0].P.Tier, Is.EqualTo(FxTier.Key), "never dropped");
            List<ShotSeen> hits = _shots.Where(s => s.Kind == ShotFxKind.Spark).ToList();
            float hitSize = hits.Count > 0 ? hits.Max(h => h.Size) : OwnLook("Stopping Shot").HitSize;
            Assert.That(stars[0].P.Size, Is.GreaterThan(hitSize), "larger than a hit spark");
            foreach (ShotFx s in Object.FindObjectsByType<ShotFx>()) brightestOther = Mathf.Max(brightestOther, s.CoreBrightness);
            foreach (CombatFx c in Object.FindObjectsByType<CombatFx>())
                if (c != stars[0].Fx) brightestOther = Mathf.Max(brightestOther, c.CoreBrightness);
            Assume.That(stars[0].Fx != null, Is.True, "still showing");
            Assert.That(stars[0].Fx.CoreBrightness, Is.GreaterThan(brightestOther), "the brightest thing on screen");
            _brute.enabled = false;
        }

        // ---------------- 13: a perfect release ----------------

        IEnumerator LongShotReleasedAt(float seconds)
        {
            int slot = Slot("Long Shot");
            _player.Teleport(NearA);
            yield return null;
            int hits = _a.HitsTaken;
            Assert.That(_player.PressSkill(slot, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillChargedSeconds >= seconds, 5f);
            _player.ReleaseSkill(slot);
            yield return Until(() => _a.HitsTaken > hits, 3f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator APerfectRelease_IsARingAtHerFeet_AndALargerSparkWhereItLands()
        {
            SkillDefinition skill = _player.Skills[Slot("Long Shot")];
            int perfect = _player.PerfectReleases;
            yield return LongShotReleasedAt(0.5f * (skill.PerfectFrom + skill.PerfectTo));
            Assume.That(_player.PerfectReleases, Is.GreaterThan(perfect), "perfect");
            List<Seen> rings = Of(FxMoment.PerfectRelease, FxShape.Ring), sparks = Of(FxMoment.PerfectRelease, FxShape.Spark);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(sparks.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(rings[0].P.At, _player.Position), Is.LessThan(0.3f), "at her, under the muzzle");
            Assert.That(FlatDistance(sparks[0].P.At, _a.transform.position), Is.LessThan(0.05f), "on what it hit");
            Assert.That(sparks[0].P.Size, Is.EqualTo(OwnLook("Long Shot").HitSize * V.PerfectSparkScale).Within(1e-4f), "larger than its hit spark");
            Assert.That(IsWarm(rings[0].P.Rim), Is.True, "her colours");
        }

        [UnityTest]
        public IEnumerator AReleaseOutsideTheBand_DrawsNoPerfect()
        {
            int perfect = _player.PerfectReleases;
            yield return LongShotReleasedAt(0.4f);
            Assume.That(_player.PerfectReleases, Is.EqualTo(perfect), "not perfect");
            Assert.That(Of(FxMoment.PerfectRelease), Is.Empty, Drawn);
        }

        // ---------------- 14, 15: marks ----------------

        IEnumerator SpendOnA(int marks)
        {
            _player.Teleport(NearA);
            yield return null;
            _a.AddMarks(marks, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            MarkPips.For(_a, _player.ShotFxMaterial);
            yield return Frames(2);
            int hits = _a.HitsTaken;
            Assert.That(_player.PressSkill(Slot("Scatter Blast"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken > hits, 3f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveMarksSpent_BurstTenLargeRedStarsFromThePips_AndASpike_NeverDropped()
        {
            int cap = _player.Kit.MaxMarks;
            Assume.That(cap, Is.EqualTo(5));
            yield return SpendOnA(cap);
            Assert.That(_a.MarkCount, Is.EqualTo(0), "taken");
            List<Seen> stars = Of(FxMoment.MarksSpent, FxShape.Spark), spikes = Of(FxMoment.MarksSpent, FxShape.Spike);
            Assert.That(stars.Count, Is.EqualTo(V.Sizes.MarkStars(cap, cap)), Drawn);
            Assert.That(stars.Count, Is.EqualTo(10));
            Assert.That(spikes.Count, Is.EqualTo(1), "and a spike: unmistakable");
            MarkPips pips = _a.GetComponentInChildren<MarkPips>(true);
            for (int i = 0; i < cap; i++)
                Assert.That(Vector3.Distance(stars[i].P.At, pips.PipPoint(i, cap)), Is.LessThan(0.05f), "star " + i + " from its pip");
            foreach (Seen s in stars)
            {
                Assert.That(s.P.Size, Is.EqualTo(V.Sizes.MarkStarSize(cap, cap)).Within(1e-4f));
                Assert.That(s.P.Tier, Is.EqualTo(FxTier.Key), "never dropped");
                Assert.That(s.P.Velocity.sqrMagnitude, Is.GreaterThan(0.1f), "bursting outward");
                Paint(s, V.MarkFullRim, V.MarkFullCore, "the pips' colour at the cap");
            }
        }

        [UnityTest]
        public IEnumerator TwoMarksSpent_BurstTwoSmallOrangeStars()
        {
            yield return SpendOnA(2);
            List<Seen> stars = Of(FxMoment.MarksSpent, FxShape.Spark);
            Assert.That(stars.Count, Is.EqualTo(2), Drawn);
            Assert.That(Of(FxMoment.MarksSpent, FxShape.Spike), Is.Empty, "no spike short of the cap");
            Assert.That(stars[0].P.Size, Is.EqualTo(V.Sizes.MarkStarSize(2, 5)).Within(1e-4f));
            Assert.That(stars[0].P.Size, Is.LessThan(V.Sizes.MarkStarSize(5, 5)), "smaller than five");
            Assert.That(stars[0].P.Tier, Is.EqualTo(FxTier.Plain));
            Paint(stars[0], V.MarkRim, V.MarkCore, "orange");
        }

        [UnityTest]
        public IEnumerator AMarkGained_IsATinyOrangePopAtItsPip()
        {
            SkillDefinition quick = _player.Skills[Slot("Quick Shot")];
            Assume.That(quick.MarksAdded, Is.GreaterThan(0), "Quick Shot marks");
            _player.Teleport(NearA);
            yield return null;
            int hits = _a.HitsTaken;
            Assert.That(_player.PressSkill(Slot("Quick Shot"), _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.MarkCount > 0, 3f);
            yield return null;
            int marks = _a.MarkCount;
            List<Seen> pops = Of(FxMoment.MarkAdded);
            Assert.That(pops.Count, Is.EqualTo(1), Drawn);
            Assert.That(pops[0].P.Shape, Is.EqualTo(FxShape.Spark));
            Assert.That(pops[0].P.Size, Is.EqualTo(V.MarkPop).Within(1e-4f), "very small");
            Assert.That(pops[0].P.Size, Is.LessThan(0.5f));
            Assert.That(pops[0].P.Tier, Is.EqualTo(FxTier.Minor));
            MarkPips pips = _a.GetComponentInChildren<MarkPips>(true);
            Assert.That(Vector3.Distance(pops[0].P.At, pips.PipPoint(marks - 1, _player.Kit.MaxMarks)), Is.LessThan(0.05f), "at its pip");
            Paint(pops[0], V.MarkRim, V.MarkCore, "orange");
        }

        // ---------------- 16: a keg ----------------

        [UnityTest]
        public IEnumerator AKeg_ShowsItsFuseAsARingClosingIn_SetOffByAShot_ALargerBurst()
        {
            int keg = Slot("Powder Keg");
            SkillDefinition skill = _player.Skills[keg];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsLying > 0, 2f);
            List<Seen> fuse = Of(FxMoment.KegLying);
            Assert.That(fuse.Count, Is.EqualTo(1), Drawn);
            Assert.That(fuse[0].P.Shape, Is.EqualTo(FxShape.Closing), "its ring closing in");
            Assert.That(FlatDistance(fuse[0].P.At, _player.LastKegPoint), Is.LessThan(0.05f), "on the keg");
            Assert.That(fuse[0].P.Size, Is.EqualTo(skill.Radius).Within(1e-4f), "from its circle's edge");
            Assert.That(fuse[0].P.Seconds, Is.EqualTo(skill.Fuse).Within(1e-4f), "over its fuse");
            yield return UntilFree();
            int fired = _player.BasicShotsFired;
            _shots.Clear();
            _player.Shoot(_a.transform.position);
            yield return WatchingShots(() => _player.BasicShotsFired > fired && _player.KegsLying == 0, 2f);
            yield return null;
            Assert.That(_player.KegsSetOffByShots, Is.EqualTo(1));
            Assert.That(fuse[0].Fx == null, Is.True, "its ring went with it");
            List<Seen> bursts = Of(FxMoment.KegSetOffByShot, FxShape.Spark);
            Assert.That(bursts.Count, Is.EqualTo(1), Drawn);
            Assert.That(Of(FxMoment.KegSetOffByShot, FxShape.Ring).Count, Is.EqualTo(1));
            Assert.That(FlatDistance(bursts[0].P.At, fuse[0].P.At), Is.LessThan(0.05f), "where it lay");
            float fuseBurst = skill.Radius * 1.1f; // the burst a keg makes by itself (PlayerCharacter.SpawnSkillFx)
            Assert.That(bursts[0].P.Size, Is.GreaterThan(fuseBurst * 1.2f), "larger than the burst by its fuse");
        }

        [UnityTest]
        public IEnumerator AKegBurstByItsFuse_DrawsNoShotBurst_AndItsRingGoes()
        {
            int keg = Slot("Powder Keg");
            SkillDefinition skill = _player.Skills[keg];
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(keg, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.KegsLying > 0, 2f);
            List<Seen> fuse = Of(FxMoment.KegLying);
            Assert.That(fuse.Count, Is.EqualTo(1));
            yield return Until(() => _player.KegsBurst > 0, skill.Fuse + 2f);
            yield return Frames(2);
            Assert.That(_player.KegsBurst, Is.EqualTo(1));
            Assert.That(fuse[0].Fx == null, Is.True, "its ring went with it");
            Assert.That(Of(FxMoment.KegSetOffByShot), Is.Empty, Drawn);
        }

        // ---------------- 17, 18: her dash, her pain ----------------

        [UnityTest]
        public IEnumerator TheDash_IsAFlashWhereSheStarts_AndAStreakAlongHerPath_ARejectedOne_None()
        {
            _player.Teleport(OpenGround);
            yield return null;
            Vector3 from = _player.Position;
            Assert.That(_player.DashToward(from + Vector3.right * 5f), Is.EqualTo(IntentResult.Started));
            List<Seen> flash = Of(FxMoment.Dashed, FxShape.Flash), streak = Of(FxMoment.Dashed, FxShape.Streak);
            Assert.That(flash.Count, Is.EqualTo(1), Drawn);
            Assert.That(streak.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(flash[0].P.At, from), Is.LessThan(0.05f), "where she starts");
            Assert.That(FlatDistance(streak[0].P.At, from), Is.LessThan(0.05f));
            Assert.That(FlatDistance(streak[0].P.End, from + Vector3.right * _player.Feel.DashDistance), Is.LessThan(0.2f), "along her path");
            Assert.That(IsWarm(streak[0].P.Rim), Is.True, "her colours");
            yield return Frames(2);
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 5f), Is.EqualTo(IntentResult.Rejected));
            yield return Frames(2);
            Assert.That(Of(FxMoment.Dashed).Count, Is.EqualTo(2), "none for a rejected dash");
        }

        [UnityTest]
        public IEnumerator SheIsHurt_ARedSparkOnHer_LargerForABiggerHit()
        {
            Assert.That(_health.TakeHit(5), Is.EqualTo(5));
            yield return Frames(2);
            List<Seen> hurt = Of(FxMoment.HerHurt);
            Assert.That(hurt.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(hurt[0].P.At, _player.Position), Is.LessThan(0.05f), "on her");
            Paint(hurt[0], V.HurtRim, V.HurtCore, "red");
            Assert.That(_health.TakeHit(30), Is.EqualTo(30));
            yield return Frames(2);
            hurt = Of(FxMoment.HerHurt);
            Assert.That(hurt.Count, Is.EqualTo(2));
            Assert.That(hurt[1].P.Size, Is.GreaterThan(hurt[0].P.Size), "larger for a bigger hit");
            Assert.That(hurt[1].P.Size, Is.EqualTo(V.Sizes.HurtSize(30, _health.Max)).Within(1e-4f));
            Assert.That(Of(FxMoment.StrikeHitHer), Is.Empty, "no strike");
            yield return Frames(3);
            Assert.That(Of(FxMoment.HerHurt).Count, Is.EqualTo(2), "once each");
        }

        // ---------------- 19, 20: the run ----------------

        [UnityTest]
        public IEnumerator AChest_IsAFountainOfCyanStarsAndARing_AndThePick_ARingRisingOverHer()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return Frames(2);
            _seen.Clear();
            Assert.That(_levels.OpenChest(), Is.True);
            yield return Frames(2);
            List<Seen> stars = Of(FxMoment.ChestOpened, FxShape.Spark), rings = Of(FxMoment.ChestOpened, FxShape.Ring);
            Assert.That(stars.Count, Is.EqualTo(V.ChestStars), Drawn);
            Assert.That(rings.Count, Is.EqualTo(1), Drawn);
            Assert.That(FlatDistance(rings[0].P.At, c), Is.LessThan(0.05f), "at the chest");
            Assert.That(stars.All(s => FlatDistance(s.P.At, c) < 0.05f && s.P.Velocity.y > 0f), Is.True, "a fountain from it");
            Assert.That(IsCyan(rings[0].P.Rim), Is.True, "the world's cyan");
            Assert.That(rings[0].P.Unscaled, Is.True, "it plays while its choice holds the game");
            Assert.That(Of(FxMoment.ChoiceTaken), Is.Empty);

            Assert.That(_levels.PickUpgrade(0), Is.True);
            yield return Frames(2);
            List<Seen> pick = Of(FxMoment.ChoiceTaken, FxShape.Ring), little = Of(FxMoment.ChoiceTaken, FxShape.Spark);
            Assert.That(pick.Count, Is.EqualTo(1), Drawn);
            Assert.That(little.Count, Is.EqualTo(3), "three small stars");
            Assert.That(FlatDistance(pick[0].P.At, _player.Position), Is.LessThan(0.05f), "over her");
            Assert.That(pick[0].P.Velocity.y, Is.GreaterThan(0f), "rising");
            Assert.That(IsCyan(pick[0].P.Rim), Is.True);
            Assert.That(_levels.OpenChest(chest), Is.False);
            yield return Frames(2);
            Assert.That(Of(FxMoment.ChestOpened, FxShape.Ring).Count, Is.EqualTo(1), "an opened chest does not open again");
            _levels.BackToPracticeRoom();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFloorReward_Picked_IsAChoiceTaken()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests();
            yield return FramesUntil(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            yield return Frames(2);
            Assert.That(Of(FxMoment.ChoiceTaken), Is.Empty, "offered, not taken");
            Assert.That(_levels.PickReward(0), Is.True);
            yield return Frames(2);
            Assert.That(Of(FxMoment.ChoiceTaken, FxShape.Ring).Count, Is.EqualTo(1), Drawn);
            _levels.BackToPracticeRoom();
            yield return null;
        }

        // ---------------- The limits, and nothing left behind ----------------

        [UnityTest]
        public IEnumerator AThousandPieces_NeverMoreThanTheCapLive_AndNothingIsLeftBehind()
        {
            yield return null;
            int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int sharedBefore = CombatFx.SharedMeshes;
            var shapes = new[] { FxShape.Spark, FxShape.Ring, FxShape.Shards, FxShape.Streak, FxShape.Orbit, FxShape.Closing, FxShape.Converge, FxShape.Wedge, FxShape.SideSpikes, FxShape.Diamond };
            int made = 0, most = 0, keys = 0;
            for (int frame = 0; frame < 10; frame++)
            {
                for (int i = 0; i < 100; i++)
                {
                    FxShape shape = shapes[(frame * 100 + i) % shapes.Length];
                    bool key = i == 0;
                    var p = new FxPiece
                    {
                        Shape = shape, Moment = key ? FxMoment.Countered : FxMoment.HerHurt, Tier = key ? FxTier.Key : FxTier.Plain,
                        At = Stand + new Vector3(i % 10, 0.5f, i / 10), End = Stand + new Vector3(i % 10 + 2f, 0.5f, i / 10), Direction = Vector3.forward,
                        Size = 0.3f + 0.01f * i, Angle = 60f, Seconds = 0.4f, Count = 6, PieceSize = 0.2f, Spin = 90f,
                        Rim = V.HurtRim, Core = V.HurtCore, RimGlow = V.RimGlow, CoreGlow = V.CoreGlow,
                    };
                    if (_fx.Put(p) != null)
                    {
                        made++;
                        if (key) keys++;
                    }
                }
                Assert.That(_fx.Live, Is.LessThanOrEqualTo(new FxLimits().MostLive + keys), "frame " + frame);
                yield return null;
                int live = Object.FindObjectsByType<CombatFx>().Length;
                most = Mathf.Max(most, live);
                Assert.That(live, Is.LessThanOrEqualTo(V.Limits.MostLive + keys), "frame " + frame + ": no more drawn than the cap");
            }
            Assert.That(made, Is.GreaterThan(160), "most were let in, the least pushed out");
            Assert.That(keys, Is.EqualTo(10), "every key piece let in");
            yield return For(1f);
            yield return Frames(3);
            Assert.That(Object.FindObjectsByType<CombatFx>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, "every piece has gone");
            Assert.That(_fx.Live, Is.EqualTo(0), "and the limits know it");
            Assert.That(CombatFx.Alive, Is.EqualTo(0));
            int meshesAfter = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            Debug.Log($"[024] a thousand pieces: {made} let in, at most {most} live; meshes {meshesBefore} before, {meshesAfter} after, {CombatFx.SharedMeshes - sharedBefore} shared made");
            Assert.That(meshesAfter, Is.LessThanOrEqualTo(meshesBefore + (CombatFx.SharedMeshes - sharedBefore)), "only the shared meshes, made once, stay");
            Assert.That(CombatFx.SharedMeshes - sharedBefore, Is.LessThanOrEqualTo(shapes.Length), "one a shape in these colours at most");
        }

        // ---------------- Off ----------------

        [UnityTest]
        public IEnumerator TheOffValue_GivesBackTheStandIns_AndTheBorrowedLooks_AndDrawsNoneOfTheNew()
        {
            V.On = false;
            Assert.That(CombatEffects.PoolLooksBorrowed, Is.True);
            BruteAt(2.5f);
            int standIns = -1;
            yield return Watch(() => _brute.Strikes > 0, 8f, f => standIns = StandIns());
            Assume.That(_brute.Strikes, Is.GreaterThan(0), "the brute struck");
            Assert.That(standIns, Is.EqualTo(1), "the flat stand-in, as before");
            _brute.enabled = false;
            _player.Teleport(NearA);
            yield return null;
            _health.TakeHit(5);
            yield return FireAtA("Tag Round");
            List<ShotSeen> drawn = _shots.Where(s => s.Kind == ShotFxKind.Streak).ToList();
            Assert.That(drawn.Count, Is.GreaterThan(0));
            ShotSeen streak = drawn[0];
            ShotLook borrowed = (_player.ShotLooks != null ? _player.ShotLooks.For("Quick Shot") : null) ?? ShotFxDefaults.Borrowed("Tag Round");
            Assert.That(streak.Size, Is.EqualTo(borrowed.StreakWidth).Within(1e-4f), "Tag Round borrows Quick Shot's look again");
            yield return UntilFree();
            _player.DashToward(_player.Position + Vector3.right * 5f);
            yield return Frames(3);
            Assert.That(_seen, Is.Empty, "nothing drawn: " + Drawn);
            Assert.That(Object.FindObjectsByType<CombatFx>(), Is.Empty);
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
        public IEnumerator AFight_DealsAndTakesTheSameDamage_InTheSameTime_WithTheEffectsOnAndOff()
        {
            Fight on = default, off = default;
            yield return FightOnce(f => on = f);
            Assert.That(_seen.Count, Is.GreaterThan(0), "the effects were at work: " + Drawn);

            Object.Destroy(_fx.gameObject);
            _fx = null;
            yield return null;
            yield return Load();
            Assert.That(CombatEffects.Current, Is.Null, "off");
            yield return FightOnce(f => off = f);

            Debug.Log($"[024] on: dealt {string.Join(", ", on.Dealt)}, taken {string.Join(", ", on.Taken)} after {on.TakenAfter:0.000} s;"
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
