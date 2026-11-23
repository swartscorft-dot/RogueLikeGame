using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 094 (Kristian, playing main fa61ce7 on 2026-11-23: "når jeg ser vest eller øst ... er siktet litt opp", and
    /// "peker man nord eller sør, er laser siktet alltid rett på musen, men så øker forskjellen jo nærmere øst/vest man peker"):
    /// the shot goes where the cursor is ON THE SCREEN. Measured with the game's own camera at its default distance, through
    /// the input driver as the mouse feeds it, with the Laser Sight fitted: the cursor is put on the screen 6 m from her gun
    /// at eight bearings (east and west on the row of her gun), and for each the angle on the screen between (her gun's screen
    /// point to the cursor) and (the drawn line's two screen ends), and how far the line passes from the cursor, in pixels at
    /// 1080 rows and in metres at the line's height. Standing, and running north at full speed. Then the body under the
    /// cursor at 8 m east is hit, and a skill placed on the ground still lands under the cursor.
    /// </summary>
    public class AimAtTheCursorTests
    {
        /// <summary>The ceiling: the drawn line passes within this many pixels (at 1080 rows) of the cursor.</summary>
        const float MostPixels = 2f;
        /// <summary>And this many degrees on the screen.</summary>
        const float MostDegrees = 0.3f;
        /// <summary>How far from her gun the cursor is put (work order 094: 6 m).</summary>
        const float CursorMetres = 6f;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        Camera _cam;
        Vector2 _cursor;
        Vector2 _move;
        bool _shootOnce;
        int _skillOnce;
        readonly HashSet<ShotFx> _seenStreaks = new HashSet<ShotFx>();
        readonly List<Damageable> _putAway = new List<Damageable>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            GameSettings.SetCameraDistanceStep(0, keep: false); // the feel's own distance, 28 m: the default
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = _player.GetComponent<PlayerInputDriver>();
            _cam = Camera.main;
            Assert.That(_cam, Is.Not.Null);
            Assert.That(_cam.GetComponent<CameraRig>(), Is.Not.Null, "the game's own camera");
            _cursor = new Vector2(_cam.pixelWidth * 0.5f, _cam.pixelHeight * 0.5f);
            _move = Vector2.zero;
            _shootOnce = false;
            _skillOnce = 0;
            _seenStreaks.Clear();
            _driver.InputSource = () =>
            {
                var f = new InputFrame { CursorScreen = _cursor, Move = _move, ShootPressed = _shootOnce, ShootHeld = _shootOnce, SkillsPressed = _skillOnce, SkillsHeld = _skillOnce };
                _shootOnce = false;
                _skillOnce = 0;
                return f;
            };
            // Nothing in the way of the line: every target is put away (a test that wants one brings it back).
            _putAway.Clear();
            _putAway.AddRange(Damageable.All);
            foreach (Damageable d in _putAway) d.gameObject.SetActive(false);
            // The Laser Sight fitted, as a run puts it.
            var m = new RunModifiers();
            m.Weapon.Add(WeaponRack.Find(new MetaValues(), WeaponAttachments.LaserSight));
            _player.Modifiers = m;
            _player.Teleport(Vector3.zero);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.Modifiers = null;
            if (_player != null) _player.Feel.AimAtShotHeight = true; // the default: a test that turns it off turns it back
            GameSettings.Unload();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Standing ----------------

        static readonly (string name, float bearing)[] Bearings =
        {
            ("east", 90f), ("west", 270f), ("north", 0f), ("south", 180f),
            ("north-east", 45f), ("south-east", 135f), ("south-west", 225f), ("north-west", 315f),
        };

        [UnityTest]
        public IEnumerator Standing_TheDrawnLinePassesThroughTheCursor_AtEveryBearing()
        {
            yield return UntilLaser();
            var table = new StringBuilder("Standing, cursor 6 m from her gun (screen degrees, + = the line lies anticlockwise of the cursor; px at 1080 rows; m at the line's height)\n");
            float worstPx = 0f, worstDeg = 0f;
            foreach ((string name, float bearing) in Bearings)
            {
                Vector3 gun = _player.Laser.From;
                Vector3 at = gun + Compass(bearing) * CursorMetres; // at the gun's height: east and west lie on its row of the screen
                _cursor = ScreenOf(at);
                for (int i = 0; i < 4; i++) yield return null;
                Sample s = Measure();
                table.AppendLine($"  {name,-11} {s}");
                worstPx = Mathf.Max(worstPx, Mathf.Abs(s.Px1080));
                worstDeg = Mathf.Max(worstDeg, Mathf.Abs(s.Degrees));
            }
            Report(table.ToString());
            Assert.That(worstPx, Is.LessThan(MostPixels), "the line passes within a few pixels of the cursor at every bearing");
            Assert.That(worstDeg, Is.LessThan(MostDegrees));
        }

        // ---------------- Running ----------------

        [UnityTest]
        public IEnumerator Running_NorthAtFullSpeed_PointingEast_TheLineStaysOnTheCursor_AndTheShotGoesThere()
        {
            Time.captureDeltaTime = 1f / 60f; // frames of 1/60 s of game time, whatever the machine: her move per frame is a run's
            _player.Teleport(new Vector3(0f, 0f, -9f));
            yield return null;
            yield return null;
            yield return UntilLaser();
            _cursor = ScreenOf(_player.Laser.From + Vector3.right * CursorMetres);
            _move = Vector2.up; // W: up the screen, north
            Vector3 last = _player.Position;
            int moving = 0;
            for (int i = 0; i < 60 && moving < 3; i++)
            {
                yield return null;
                float speed = Flat(_player.Position - last).magnitude * 60f;
                last = _player.Position;
                moving = speed > _player.Feel.RunSpeed * 0.95f ? moving + 1 : 0;
            }
            Assume.That(moving, Is.GreaterThanOrEqualTo(3), "she runs at full speed");

            float worstPx = 0f, worstDeg = 0f, sumDeg = 0f, speedSeen = 0f;
            int samples = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                speedSeen = Flat(_player.Position - last).magnitude * 60f;
                last = _player.Position;
                Sample s = Measure();
                worstPx = Mathf.Max(worstPx, Mathf.Abs(s.Px1080));
                worstDeg = Mathf.Max(worstDeg, Mathf.Abs(s.Degrees));
                sumDeg += s.Degrees;
                samples++;
            }

            // The shot on the move: fired, it goes along the cursor's line as it is on the screen at that moment.
            NewestStreak();
            int fired = _player.BasicShotsFired;
            _shootOnce = true;
            float shotDeg = float.NaN;
            for (int i = 0; i < 60 && float.IsNaN(shotDeg); i++)
            {
                yield return null; // after the Updates: she stands where the shot went off from
                if (_player.BasicShotsFired == fired) continue;
                ShotFx streak = NewestStreak();
                Assert.That(streak, Is.Not.Null, "the shot's streak");
                Vector3 from = _player.Position;
                yield return null; // the camera of the frame the shot is first drawn in: where the cursor lies in it
                Vector3 truth = OnLevel(_cursor, streak.Origin.y);
                shotDeg = Vector3.SignedAngle(Flat(truth - from), Flat(streak.End - from), Vector3.up);
            }
            Report($"Running north at {speedSeen:0.00} m/s (frames of 1/60 s), cursor 6 m east of her gun, {samples} frames:\n" +
                   $"  the line: worst {worstDeg:0.00} deg, mean {sumDeg / samples:+0.00;-0.00} deg, worst {worstPx:0.0} px at 1080\n" +
                   $"  the shot fired on the run: {shotDeg:+0.00;-0.00} deg on the ground from the cursor's line (+ = clockwise, seen from above)");
            Assert.That(worstPx, Is.LessThan(MostPixels), "the line stays on the cursor while she runs");
            Assert.That(worstDeg, Is.LessThan(MostDegrees));
            Assert.That(Mathf.Abs(shotDeg), Is.LessThan(MostDegrees), "and the shot goes where the cursor is");
        }

        // ---------------- Hits follow ----------------

        [UnityTest]
        public IEnumerator TheCursorOnTheMiddleOfAnEnemysDrawnBody_At8mEast_IsAHit()
        {
            Damageable a = _putAway.Find(d => d != null && d.name == "Dummy_A");
            Assert.That(a, Is.Not.Null, "Dummy_A");
            a.gameObject.SetActive(true);
            a.SetMaxHealth(1000000);
            a.transform.position = new Vector3(8f, a.transform.position.y, 0f);
            yield return null;
            Bounds body = BodyOf(a);
            yield return UntilLaser();

            var table = new StringBuilder("The cursor on an enemy's drawn body, 8 m east (touch = the line's half width + its radius)" + "\n");
            float touch = _player.BasicShotHalfWidth + a.Radius;
            bool middleHit = false;
            foreach (bool on in new[] { false, true })
            {
                _player.Feel.AimAtShotHeight = on;
                foreach ((string name, Vector3 point) in new[] { ("middle", body.center), ("top", new Vector3(body.center.x, body.max.y - 0.1f, body.center.z)) })
                {
                    _cursor = ScreenOf(point);
                    for (int i = 0; i < 4; i++) yield return null;
                    yield return For(() => !_player.IsLocked && _player.Action == ActionKind.None, 3f);
                    int hits = a.HitsTaken, fired = _player.BasicShotsFired;
                    float off = OffLine(_player.Position, _player.AimCursor, a.transform.position);
                    _shootOnce = true;
                    yield return For(() => _player.BasicShotsFired > fired, 2f);
                    Assert.That(_player.BasicShotsFired, Is.GreaterThan(fired), "the shot went off");
                    yield return null;
                    bool hit = a.HitsTaken > hits;
                    if (on && name == "middle") middleHit = hit;
                    table.AppendLine($"  {(on ? "new aim" : "old aim")}  {name,-7} (y {point.y:0.00} m): the shot's line passes {off:0.00} m from its middle, touch {touch:0.00} m: {(hit ? "HIT" : "miss")}");
                }
            }
            Report(table.ToString());
            Assert.That(middleHit, Is.True, "pointing at the body she sees hits it");
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_GivesTheOldAim_TheCursorsGroundPoint_AndOn_ThePointAtTheGunsHeight()
        {
            yield return UntilLaser();
            _cursor = ScreenOf(_player.Laser.From + Vector3.right * CursorMetres);
            _player.Feel.AimAtShotHeight = false;
            for (int i = 0; i < 4; i++) yield return null;
            Vector3 ground = Flat(OnLevel(_cursor, 0f));
            Assert.That(Vector3.Distance(_player.AimCursor, ground), Is.LessThan(1e-3f), "off: the cursor's ground point, as before");
            Sample off = Measure();
            Assert.That(off.Degrees, Is.GreaterThan(4f), "and the line off the cursor, as he saw it");

            _player.Feel.AimAtShotHeight = true;
            for (int i = 0; i < 4; i++) yield return null;
            Vector3 level = Flat(OnLevel(_cursor, _player.ShotHeight));
            Assert.That(Vector3.Distance(_player.AimCursor, level), Is.LessThan(0.02f), "on: under where the cursor's ray meets the gun's height");
            Sample on = Measure();
            Report($"The switch, cursor 6 m east: off {off}; on {on}");
            Assert.That(Mathf.Abs(on.Px1080), Is.LessThan(MostPixels));
        }

        // ---------------- Placed on the ground ----------------

        [UnityTest]
        public IEnumerator ASkillPlacedOnTheGround_LandsUnderTheCursor()
        {
            int slot = _player.SlotOf("Powder Keg");
            Assume.That(slot, Is.GreaterThanOrEqualTo(0), "Powder Keg in the kit");
            _player.EquipSkill(slot);
            int key = _player.KeyOfSkill(slot);
            Assume.That(key, Is.GreaterThanOrEqualTo(0), "on a key");
            yield return null;
            Vector3 ground = _player.Position + new Vector3(5f, 0f, 0f); // east: where the gun's height and the ground differ most
            ground.y = 0f;
            _cursor = ScreenOf(ground);
            for (int i = 0; i < 4; i++) yield return null;
            int before = _player.KegsLying;
            _skillOnce = 1 << key;
            yield return For(() => _player.KegsLying > before, 3f);
            Assert.That(_player.KegsLying, Is.GreaterThan(before), "the keg lands");
            float off = Flat(_player.LastKegPoint - ground).magnitude;
            Report($"Powder Keg, cursor on the ground 5 m east: it lands {off:0.000} m from the ground point under the cursor");
            Assert.That(off, Is.LessThan(0.05f), "under the cursor, on the ground");
        }

        // ---------------- Measuring ----------------

        struct Sample
        {
            public float Degrees, Px1080, Metres;
            public override string ToString() => $"{Degrees,6:+0.00;-0.00;0.00} deg   {Px1080,5:0.0} px   {Metres:0.000} m";
        }

        /// <summary>The drawn line (the Laser Sight's two ends, on the screen) against the cursor.</summary>
        Sample Measure()
        {
            Vector3 from = _player.Laser.From, to = _player.Laser.To;
            Vector2 fs = ScreenOf(from), ts = ScreenOf(to);
            Vector2 line = ts - fs, toCursor = _cursor - fs;
            // How far the cursor lies off the line, + when the line passes anticlockwise of it (above it, pointing east).
            float px = (line.y * toCursor.x - line.x * toCursor.y) / Mathf.Max(1e-4f, line.magnitude);
            Vector3 c = OnLevel(_cursor, from.y);
            return new Sample
            {
                Degrees = Vector2.SignedAngle(toCursor, line), // the line's angle from the cursor's direction: + anticlockwise
                Px1080 = px * 1080f / _cam.pixelHeight,
                Metres = OffLine(from, to, c),
            };
        }

        /// <summary>Where the cursor's ray meets the level plane at this height.</summary>
        Vector3 OnLevel(Vector2 screen, float height)
        {
            Ray ray = _cam.ScreenPointToRay(screen);
            new Plane(Vector3.up, new Vector3(0f, height, 0f)).Raycast(ray, out float d);
            return ray.GetPoint(d);
        }

        /// <summary>How far a point lies from the line through two points, on the ground's compass (heights ignored).</summary>
        static float OffLine(Vector3 from, Vector3 to, Vector3 point)
        {
            Vector3 dir = Flat(to - from).normalized, rel = Flat(point - from);
            return Mathf.Abs(dir.x * rel.z - dir.z * rel.x);
        }

        Vector2 ScreenOf(Vector3 world) => _cam.WorldToScreenPoint(world);

        static Vector3 Compass(float bearing) =>
            new Vector3(Mathf.Sin(bearing * Mathf.Deg2Rad), 0f, Mathf.Cos(bearing * Mathf.Deg2Rad));

        static Bounds BodyOf(Damageable d)
        {
            Renderer[] rs = d.GetComponentsInChildren<Renderer>();
            Assert.That(rs.Length, Is.GreaterThan(0), "a drawn body");
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        IEnumerator UntilLaser()
        {
            yield return For(() => _player.Laser != null && _player.Laser.Shown, 2f);
            Assert.That(_player.Laser != null && _player.Laser.Shown, Is.True, "the Laser Sight's line shows");
        }

        /// <summary>Frames until the condition holds, or this many seconds of game time (and a few more of real time) have gone.</summary>
        static IEnumerator For(System.Func<bool> done, float seconds)
        {
            float until = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 5f;
            while (!done() && Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>A streak that was not there at the last call: the one the latest shot made.</summary>
        ShotFx NewestStreak()
        {
            ShotFx found = null;
            foreach (ShotFx fx in Object.FindObjectsByType<ShotFx>())
                if (fx.Kind == ShotFxKind.Streak && _seenStreaks.Add(fx)) found = fx;
            return found;
        }

        static void Report(string text)
        {
            Debug.Log("[094] " + text);
            TestContext.Out.WriteLine(text);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
