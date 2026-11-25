using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The boss's entrance in play (work order 130; Kristian, 2026-11-25: "en litt cinematic inngang, forstørre kameraet litt og lage en
    /// kul intro animasjon ... bare skal være en skikkelig tørr vits"), on a floor 5 built alone, through the game's own intents: she is
    /// put past the boss room's point as she would walk there. Crossing the point begins it once and only once a room; while it plays
    /// she takes no damage, the boss starts no attack, its bar is not up and time holds; after its end and after a skip the camera is
    /// the play camera, the fight is awake and the state is the same; the run's clock did not count it; Esc, Space or her action key
    /// ends it at once and opens no pause; with the switch off none of it happens.
    /// </summary>
    public class BossEntrancePlayTests
    {
        const ulong Seed = 0x130B055UL;        // a floor 5 seed of this order's own
        const int MostFrames = 600;
        static readonly float RealSeconds = new BossEntranceValues().Total + 4f; // the most real time an entrance may take here

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        bool _onBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _onBefore = BossEntranceValues.On;
            BossEntranceValues.On = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _levels.RunValues.SkillTiers = false;
            _hud = RunHud.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            BossEntranceValues.On = _onBefore;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_health != null) _health.Untouchable = false;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float realSeconds)
        {
            float deadline = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A ground point <paramref name="depth"/> m straight in from the middle of <paramref name="door"/>, into <paramref name="room"/> (RoomFightsPlayTests').</summary>
        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            return level.ToWorld(p);
        }

        sealed class Floor
        {
            public GeneratedLevel Level;
            public EnemyBoss Boss;
            public LevelRoom Room;
            public LevelDoor Door;
            public RoomFight Fight;
        }

        IEnumerator BuildFloor5(Action<Floor> got, ulong seed = Seed)
        {
            _practiceEnemy.enabled = true; // the floor's enemies are copies of it (BossFinaleTests' floor test)
            GeneratedLevel level = _levels.Build(seed, 5);
            _practiceEnemy.enabled = false;
            yield return Frames(2);
            var f = new Floor { Level = level, Boss = level.Boss as EnemyBoss, Room = level.Layout.ExitRoom };
            Assert.That(f.Boss, Is.Not.Null, "floor 5's act boss");
            f.Door = LevelGeometry.EntryDoor(level.Layout, f.Room);
            f.Fight = _levels.FightOf(f.Room);
            Assert.That(f.Fight, Is.Not.Null, "the boss room's fight");
            Assert.That(f.Boss.Fight, Is.SameAs(f.Fight));
            Assert.That(level.EnemiesByRoom[f.Room.Order].Count, Is.EqualTo(1), "the boss alone in its room (work order 130)");
            got(f);
        }

        /// <summary>She stands a metre inside the boss room's door (short of the point), then past the point.</summary>
        IEnumerator WalkIn(Floor f)
        {
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 1.0));
            for (int i = 0; i < 30 && _levels.CurrentRoom != f.Room; i++) yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(f.Room), "she is in the boss room");
            yield return Frames(3);
            Assert.That(_levels.EntranceHolds, Is.False, "a metre inside the door, short of the point: nothing yet");
            Assert.That(f.Fight.On, Is.False);
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 5.5));
            yield return Until(() => _levels.EntranceHolds || f.Fight.On, 2f);
        }

        IEnumerator Press(params Key[] down)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));
            for (int i = 0; i < 60 && !(Array.TrueForAll(down, k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed)); i++) yield return null;
        }

        IEnumerator Tap(Key key)
        {
            yield return Press(key);
            yield return Press();
        }

        /// <summary>The play camera's pose for her where she stands now, as the rig works it out with no shot.</summary>
        static void PlayPose(CameraRig rig, out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            FeelValues feel = rig.Settings != null ? rig.Settings.Values : new FeelValues();
            CameraAngle angle = GameSettings.CameraNow(feel);
            rotation = Quaternion.Euler(angle.Pitch, 0f, 0f);
            Vector3 focus = new Vector3(rig.Target.position.x, 0f, rig.Target.position.z);
            position = focus - rotation * Vector3.forward * angle.Distance;
            fieldOfView = angle.FieldOfView;
        }

        static CameraRig Rig()
        {
            CameraRig rig = Camera.main != null ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig == null) rig = Object.FindAnyObjectByType<CameraRig>();
            Assert.That(rig, Is.Not.Null, "the play camera's rig");
            return rig;
        }

        /// <summary>What an entrance leaves behind, for the full one and the skipped one alike.</summary>
        struct After
        {
            public float TimeScale;
            public bool Held, Untouchable, FightOn, BossEnabled, ShotNull, Card;
            public Vector3 PivotPosition, PivotScale;
            public float CameraOff, CameraTurn, FieldOfViewOff;
            public override string ToString() =>
                $"time scale {TimeScale}, input held {Held}, untouchable {Untouchable}, fight on {FightOn}, boss on {BossEnabled}, shot gone {ShotNull}, card {Card}, pivot {PivotPosition:F4} {PivotScale:F4}, camera off the play camera by {CameraOff:F5} m and {CameraTurn:F4} degrees, field of view by {FieldOfViewOff:F4}";
        }

        IEnumerator Leaves(Floor f, Action<After> got)
        {
            var a = new After
            {
                TimeScale = Time.timeScale, Held = PlayerInputDriver.HeldByScreen, Untouchable = _health.Untouchable, FightOn = f.Fight.On,
                BossEnabled = f.Boss.enabled, ShotNull = Rig().Shot == null, Card = _levels.EntranceCardShows > 0f,
            };
            EnemyLook look = f.Boss.Look;
            if (look != null)
            {
                a.PivotPosition = look.Pivot.localPosition - Vector3.up * look.Measured.Lift;
                a.PivotScale = look.Pivot.localScale / look.Measured.Scale;
            }
            yield return null; // the rig's LateUpdate of this frame
            CameraRig rig = Rig();
            PlayPose(rig, out Vector3 p, out Quaternion r, out float fov);
            Camera cam = rig.GetComponent<Camera>();
            a.CameraOff = (rig.transform.position - p).magnitude;
            a.CameraTurn = Quaternion.Angle(rig.transform.rotation, r);
            a.FieldOfViewOff = Mathf.Abs(cam.fieldOfView - fov);
            got(a);
        }

        static void AssertPlay(After a, string what)
        {
            Assert.That(a.TimeScale, Is.EqualTo(1f), what + ": the time scale given back");
            Assert.That(a.Held, Is.False, what + ": her input let go");
            Assert.That(a.Untouchable, Is.False, what + ": she can be hurt again");
            Assert.That(a.FightOn, Is.True, what + ": the fight is awake");
            Assert.That(a.BossEnabled, Is.True, what + ": the boss goes on");
            Assert.That(a.ShotNull, Is.True, what + ": the rig is the play camera's alone");
            Assert.That(a.Card, Is.False, what + ": no card");
            Assert.That(a.PivotPosition.magnitude, Is.LessThan(1e-5f), what + ": the boss on the floor");
            Assert.That((a.PivotScale - Vector3.one).magnitude, Is.LessThan(1e-5f), what + ": the boss at its own height");
            Assert.That(a.CameraOff, Is.LessThan(1e-3f), what + ": the camera where the play camera stands");
            Assert.That(a.CameraTurn, Is.LessThan(1e-3f), what + ": and looking as it looks");
            Assert.That(a.FieldOfViewOff, Is.LessThan(1e-4f), what + ": with its field of view");
        }

        // ---------------- The tests ----------------

        [UnityTest, Timeout(180000)]
        public IEnumerator CrossingThePoint_BeginsTheEntranceOnce_AndOnlyOnceARoom()
        {
            Floor f = null;
            yield return BuildFloor5(x => f = x);
            int begun = _levels.EntrancesBegun;
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True, "past the point: the entrance");
            Assert.That(_levels.EntrancesBegun, Is.EqualTo(begun + 1));
            Assert.That(f.Fight.On, Is.False, "the fight waits for it");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "everything holds");
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True, "she cannot act");
            Assert.That(_levels.EntranceName, Is.EqualTo(f.Boss.Tuning.Title.ToUpperInvariant()), "the boss's own name");
            Assert.That(BossEntranceLines.Lines, Does.Contain(_levels.EntranceLine));
            Assert.That(_levels.EntranceLine, Is.EqualTo(BossEntranceLines.For(Seed, BossEntranceLines.Lines)), "drawn from the floor's seed outside a run");
            yield return Until(() => !_levels.EntranceHolds, RealSeconds);
            Assert.That(_levels.EntranceHolds, Is.False, "it ends by itself");
            Assert.That(_levels.LastEntranceSkipped, Is.False);
            Assert.That(f.Fight.On, Is.True, "the fight wakes at its end");
            Assert.That(f.Fight.StartedBy, Is.EqualTo(RoomFightStart.Point), "as at the point");
            // Out of the room and in again past the point, the fight ended meanwhile: no second entrance.
            f.Fight.End();
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 1.0));
            yield return Frames(3);
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 5.5));
            yield return Frames(10);
            Assert.That(_levels.EntrancesBegun, Is.EqualTo(begun + 1), "once a room");
            Assert.That(_levels.EntranceHolds, Is.False);
            Assert.That(f.Fight.On, Is.True, "the second time the fight wakes at the point at once");
            // A new floor: its boss room has its own entrance.
            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntrancesBegun, Is.EqualTo(begun + 2), "a new floor's boss room");
            Assert.That(_levels.SkipEntrance(), Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DuringIt_SheTakesNoDamage_TheBossStartsNoAttack_ItsBarIsDown_AndTimeHolds()
        {
            Floor f = null;
            yield return BuildFloor5(x => f = x);
            var reports = new List<EnemyReportKind>();
            f.Boss.Reported += r => reports.Add(r.Kind);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            int health = _health.Current;
            float gameTime = Time.time;
            var bar = f.Boss.GetComponent<BossHealthBar>();
            bool sawHold = false, sawCard = false, sawBack = false, barUp = false, notIdle = false, hurt = false;
            float closest = float.MaxValue;
            Camera cam = Rig().GetComponent<Camera>();
            while (_levels.EntranceHolds)
            {
                int took = _health.TakeHit(40); // a blow from anywhere: none lands while it plays
                hurt |= took > 0 || _health.Current < health;
                notIdle |= f.Boss.State != EnemyState.Idle || f.Boss.Attack != null || f.Boss.IsTelegraphing;
                barUp |= bar != null && bar.Showing;
                EntrancePhase phase = _levels.EntrancePhaseNow;
                sawHold |= phase == EntrancePhase.Hold;
                sawBack |= phase == EntrancePhase.TravelBack;
                sawCard |= _levels.EntranceCardShows >= 1f;
                closest = Mathf.Min(closest, Flat(cam.transform.position, f.Boss.transform.position));
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                yield return null;
            }
            Assert.That(hurt, Is.False, "she took no damage");
            Assert.That(notIdle, Is.False, "the boss neither woke nor wound up");
            Assert.That(reports, Has.No.Member(EnemyReportKind.AttackStarted));
            Assert.That(reports, Has.No.Member(EnemyReportKind.Woke), "not while it played");
            Assert.That(barUp, Is.False, "its bar down under the card");
            Assert.That(sawHold && sawCard && sawBack, Is.True, "the hold, the card and the travel back were seen");
            Assert.That(Time.time - gameTime, Is.LessThan(0.2f), "game time held");
            var v = new BossEntranceValues();
            float playFlat = CameraAngles.OursDistance * Mathf.Cos(CameraAngles.OursPitch * Mathf.Deg2Rad);
            Assert.That(closest, Is.LessThan(playFlat * 0.75f), "the camera came closer to the boss than the play camera stands from her");
            Debug.Log($"[130] the camera came within {closest:F2} m (flat) of the boss; the play camera stands {playFlat:F2} m (flat) from her; close distance {v.CloseDistance} m");
            yield return Frames(2);
            Assert.That(f.Boss.IsAwake, Is.True, "awake as the fight begins");
            Assert.That(reports, Has.Member(EnemyReportKind.Woke));
            Assert.That(_health.TakeHit(1), Is.GreaterThan(0), "and she can be hurt again");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator AfterItsEnd_AndAfterASkip_TheCameraIsThePlayCamera_TheFightIsAwake_AndTheStateIsTheSame()
        {
            Floor f = null;
            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            yield return Until(() => !_levels.EntranceHolds, RealSeconds);
            After full = default;
            yield return Leaves(f, a => full = a);
            AssertPlay(full, "after its end");

            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            yield return Until(() => _levels.EntrancePhaseNow == EntrancePhase.Hold && _levels.EntranceCardShows >= 1f, RealSeconds);
            Assert.That(_levels.EntranceCardShows, Is.EqualTo(1f), "the camera on the boss with the card");
            yield return Press(Key.Space);
            Assert.That(_levels.EntranceHolds, Is.False, "Space ends it at once");
            Assert.That(_levels.LastEntranceSkipped, Is.True);
            After skipped = default;
            yield return Leaves(f, a => skipped = a);
            yield return Press();
            AssertPlay(skipped, "after a skip");
            Assert.That(skipped.ToString(), Is.EqualTo(full.ToString()), "a skip leaves what the end leaves");
            Debug.Log("[130] after its end: " + full + "\n[130] after a skip: " + skipped);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Esc_AndHerActionKey_EndItAtOnce_AndOpenNoPause()
        {
            Floor f = null;
            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            yield return Frames(3);
            yield return Press(Key.Escape);
            Assert.That(_levels.EntranceHolds, Is.False, "Esc ends it");
            yield return Frames(2);
            Assert.That(_levels.Paused, Is.False, "and opens no pause menu");
            Assert.That(f.Fight.On, Is.True);
            yield return Press();

            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            yield return Frames(3);
            yield return Press(Key.Q); // her first skill's key
            Assert.That(_levels.EntranceHolds, Is.False, "her action key ends it");
            Assert.That(f.Fight.On, Is.True);
            yield return Press();

            yield return BuildFloor5(x => f = x);
            yield return WalkIn(f);
            Assert.That(_levels.EntranceHolds, Is.True);
            yield return Frames(3);
            yield return Press(Key.Enter);
            Assert.That(_levels.EntranceHolds, Is.False, "Enter ends it");
            yield return Press();
            Assert.That(_levels.EntrancesSkipped, Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheRunsClock_DoesNotCountIt()
        {
            _levels.MetaValues.Enabled = false; // no screen between runs before the run
            GeneratedLevel first = _levels.StartRun(Seed);
            Assert.That(first, Is.Not.Null);
            yield return Frames(3);
            Floor f = null;
            yield return BuildFloor5(x => f = x, RunSeeds.FloorSeed(Seed, 5)); // the run's own floor 5, built at once
            Assert.That(_levels.InRun, Is.True);
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 1.0));
            yield return Frames(5);
            float clock = _levels.Record.Seconds, real = Time.realtimeSinceStartup;
            _player.Teleport(InFrom(f.Level, f.Room, f.Door, 5.5));
            yield return Until(() => _levels.EntranceHolds, 2f);
            Assert.That(_levels.EntranceHolds, Is.True);
            Assert.That(_levels.EntranceLine, Is.EqualTo(BossEntranceLines.For(Seed, BossEntranceLines.Lines)), "drawn from the run's seed");
            yield return Until(() => !_levels.EntranceHolds, RealSeconds);
            yield return null;
            float took = Time.realtimeSinceStartup - real, counted = _levels.Record.Seconds - clock;
            Debug.Log($"[130] the run's clock counted {counted:F3} s of the {took:F2} s of real time the entrance took");
            Assert.That(took, Is.GreaterThan(new BossEntranceValues().Total - 0.1f), "the entrance took its real time");
            Assert.That(counted, Is.LessThan(0.25f), "the run's clock did not count it");
            Assert.That(f.Fight.On, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WithTheSwitchOff_NoneOfItHappens()
        {
            BossEntranceValues.On = false;
            Floor f = null;
            yield return BuildFloor5(x => f = x);
            int begun = _levels.EntrancesBegun;
            yield return WalkIn(f);
            yield return Frames(3);
            Assert.That(_levels.EntrancesBegun, Is.EqualTo(begun), "no entrance");
            Assert.That(_levels.EntranceHolds, Is.False);
            Assert.That(f.Fight.On, Is.True, "the fight wakes at the point, as before");
            Assert.That(f.Fight.StartedBy, Is.EqualTo(RoomFightStart.Point));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            Assert.That(Rig().Shot, Is.Null);
            Assert.That(_levels.EntranceCardShows, Is.EqualTo(0f));
            Assert.That(f.Boss.enabled, Is.True);
        }
    }
}
