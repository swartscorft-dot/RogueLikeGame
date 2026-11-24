using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The Laser Sight's beams (work order 102; Kristian, 2026-11-24: "vil at de skal følge våpenets retning hele tiden. men
    /// igjen, bare en tyn rød laser stråle"), in the ControlRoom scene, the part fitted as a run fits it: each beam starts at its
    /// pistol's muzzle and goes along its barrel, standing, aiming and running; moving the cursor with her arms still does not
    /// move them, and aiming turns them along the aim; an enemy beside a beam does not bend it and no square or dot shows; an
    /// enemy's body or a wall in its way stops it there; never longer than its named length; red by its named value, added
    /// light; the dot off by default and on with its switch; shown in the dash and when stunned, not when she is dead, without
    /// the part, or with her pistols hidden; no allocation per frame (the fog's room read too, on a built floor, where it is
    /// brighter by a wall than in the room's middle); everything it makes goes with the scene; and asked before she has woken.
    /// </summary>
    public class LaserBeamPlayTests
    {
        /// <summary>Degrees a beam may lie off its pistol's barrel.</summary>
        const float BarrelDegrees = 0.5f;

        PlayerCharacter _player;
        PlayerHealth _health;
        CharacterAnimation _look;
        Damageable _a;
        readonly List<Object> _made = new List<Object>();
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _look = Object.FindAnyObjectByType<CharacterAnimation>();
            Assert.That(_look, Is.Not.Null, "her rigged model");
            Assert.That(_player.RightMuzzle, Is.Not.Null);
            Assert.That(_player.LeftMuzzle, Is.Not.Null);
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.gameObject.SetActive(false);
            foreach (Damageable d in Damageable.All.ToList())
            {
                if (d.name == "Dummy_A") _a = d;
                else d.gameObject.SetActive(false); // nothing in the beams' way unless a test puts it there
            }
            Assert.That(_a, Is.Not.Null, "Dummy_A");
            _a.SetMaxHealth(1000000);
            Put(_a.transform, new Vector3(-15f, 0f, -15f));
            _player.Teleport(Vector3.zero);
            Fit();
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.Modifiers = null;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        void Fit(params string[] parts)
        {
            var m = new RunModifiers();
            foreach (string id in parts.Length > 0 ? parts : new[] { WeaponAttachments.LaserSight }) m.Weapon.Add(WeaponRack.Find(new MetaValues(), id));
            _player.Modifiers = m;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);
        Transform Muzzle(int beam) => beam == LaserSight.Right ? _player.RightMuzzle : _player.LeftMuzzle;
        LaserSight Laser => _player.Laser;

        static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        // What the frames' ends saw (LooksAtTheFramesEnd, after every LateUpdate: the pose as it is shown, the beams as drawn).
        int _looked, _unshown;
        float _worstFrom, _worstAngle, _longest;

        void StartLooking()
        {
            if (_end == null)
            {
                var go = new GameObject("LaserTest_FramesEnd");
                _made.Add(go);
                _end = go.AddComponent<LooksAtTheFramesEnd>();
                _end.Look = LookAtTheEnd;
            }
            _looked = _unshown = 0;
            _worstFrom = _worstAngle = _longest = 0f;
        }

        LooksAtTheFramesEnd _end;

        void LookAtTheEnd()
        {
            if (_player == null) return;
            _looked++;
            for (int b = 0; b < LaserSight.Count; b++)
            {
                if (!Laser.ShownOf(b)) { _unshown++; continue; }
                Transform m = Muzzle(b);
                _worstFrom = Mathf.Max(_worstFrom, Vector3.Distance(Laser.FromOf(b), m.position));
                _worstAngle = Mathf.Max(_worstAngle, Vector3.Angle(Laser.DirectionOf(b), m.right));
                _longest = Mathf.Max(_longest, Laser.LengthOf(b));
            }
        }

        /// <summary>Over the next <paramref name="frames"/> frames, at each frame's end: each beam shown, at its muzzle and along its barrel, never longer than its length.</summary>
        IEnumerator AtTheBarrels(string pose, int frames = 3, System.Action between = null)
        {
            StartLooking();
            for (int i = 0; i < frames; i++)
            {
                between?.Invoke();
                yield return null;
            }
            yield return null; // the last frame's end has been looked at
            Assert.That(_looked, Is.GreaterThanOrEqualTo(frames), pose + ": frames looked at");
            Assert.That(_unshown, Is.Zero, pose + ": both beams show in every frame");
            Assert.That(_worstFrom, Is.LessThan(1e-3f), pose + ": each starts at its muzzle");
            Assert.That(_worstAngle, Is.LessThan(BarrelDegrees), pose + ": each goes along its barrel");
            Assert.That(_longest, Is.GreaterThan(0f).And.LessThanOrEqualTo(Laser.Values.Length + 1e-4f), pose + ": never longer than its length");
            TestContext.Out.WriteLine(string.Format("{0}: {1} frames, worst {2:0.00000} m off the muzzle, {3:0.000} degrees off the barrel, longest {4:0.00} m", pose, _looked, _worstFrom, _worstAngle, _longest));
        }

        /// <summary>A shot east, then frames until her arms are fully aimed (the shot's pose or the hold after it).</summary>
        IEnumerator AimedEast()
        {
            _player.AimAt(_player.Position + Vector3.right * 8f);
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None, 2f);
            int fired = _player.BasicShotsFired;
            _player.Shoot(_player.Position + Vector3.right * 8f);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            float firedAt = Time.time;
            yield return Until(() => Time.time >= firedAt + 0.22f, 2f); // past the pistol's kick (0.04 s up, 0.16 s back), inside the arms' hold
            Assert.That(_look.AimWeight, Is.EqualTo(1f), "her arms aimed");
        }

        // ---------------- Fixed to the gun ----------------

        [UnityTest]
        public IEnumerator EachBeam_StartsAtItsMuzzle_AlongItsBarrel_Standing_Aiming_AndRunning()
        {
            for (int i = 0; i < 10; i++) yield return null;
            yield return AtTheBarrels("standing");
            var log = new System.Text.StringBuilder("Work order 102, the beams' directions (x, y, z), right then left:\n");
            log.AppendLine("  standing  " + Laser.DirectionOf(0).ToString("F2") + "  " + Laser.DirectionOf(1).ToString("F2"));

            yield return AimedEast();
            _player.TickLaser(); // the pose as the last frame left it, her arms aimed
            log.AppendLine("  aiming    " + Laser.DirectionOf(0).ToString("F2") + "  " + Laser.DirectionOf(1).ToString("F2"));
            for (int b = 0; b < LaserSight.Count; b++)
                Assert.That(Vector3.Angle(Flat(Laser.DirectionOf(b)), Vector3.right), Is.LessThan(15f), "aiming east: beam " + b + " points along her aim (the arms converge 6 m out)");
            yield return AtTheBarrels("aiming", 2);

            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _look.AimWeight <= 0f, 3f);
            _player.Teleport(new Vector3(-6f, 0f, -6f));
            yield return null;
            _player.Steer(Vector3.forward);
            for (int i = 0; i < 20; i++) yield return null;
            yield return AtTheBarrels("running", 20);
            Assert.That(_player.Speed, Is.GreaterThan(_player.Feel.RunSpeed * 0.8f), "she runs");
            log.AppendLine("  running   " + Laser.DirectionOf(0).ToString("F2") + "  " + Laser.DirectionOf(1).ToString("F2"));
            _player.Stop();
            Debug.Log(log.ToString());
            TestContext.Out.WriteLine(log.ToString());
        }

        [UnityTest]
        public IEnumerator MovingTheCursor_WithHerArmsStill_DoesNotMoveTheBeams()
        {
            for (int i = 0; i < 10; i++) yield return null;
            Vector3[] bearings = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            var seen = new List<Vector3>[] { new List<Vector3>(), new List<Vector3>() };
            foreach (Vector3 bearing in bearings)
            {
                _player.AimAt(_player.Position + bearing * 6f);
                for (int i = 0; i < 6; i++) yield return null;
                Assert.That(_look.AimWeight, Is.EqualTo(0f), "her arms are not raised: no shot");
                yield return AtTheBarrels("cursor " + bearing);
                for (int b = 0; b < LaserSight.Count; b++) seen[b].Add(Laser.DirectionOf(b));
            }
            for (int b = 0; b < LaserSight.Count; b++)
            {
                float most = 0f;
                foreach (Vector3 d in seen[b]) most = Mathf.Max(most, Vector3.Angle(d, seen[b][0]));
                TestContext.Out.WriteLine("beam " + b + ": the most it turned while the cursor went round her: " + most.ToString("0.00") + " degrees");
                Assert.That(most, Is.LessThan(10f), "beam " + b + " stays with the gun (only her idle sway) while the cursor goes round her");
            }
        }

        // ---------------- No snap; what stops it ----------------

        [UnityTest]
        public IEnumerator AnEnemyBesideTheBeam_DoesNotBendIt_NoSquare_AndOneInItsWay_StopsItAtItsBody()
        {
            yield return AimedEast();
            const int b = LaserSight.Right;
            _player.TickLaser();
            Vector3 from = Laser.FromOf(b), dir = Laser.DirectionOf(b);
            float clear = Laser.LengthOf(b);
            Assert.That(clear, Is.GreaterThan(2.5f), "aimed, nothing near: it runs past 2.5 m");
            Vector3 flat = Flat(dir).normalized, side = Vector3.Cross(Vector3.up, flat);

            // Beside: its middle 0.65 m off the beam's line at 1.5 m, its edge 0.15 m from it (inside the shot's own band).
            Vector3 onLine = from + dir * 1.5f;
            Assume.That(onLine.y, Is.InRange(0.2f, Laser.Values.BodyTop - 0.2f), "the beam crosses a body's height there");
            Put(_a.transform, onLine + side * (_a.Radius + 0.15f));
            _player.TickLaser();
            Assert.That(Laser.BodyOf(b), Is.Null, "an enemy beside it does not stop it");
            Assert.That(Vector3.Angle(Laser.DirectionOf(b), dir), Is.LessThan(1e-3f), "nor bend it");
            Assert.That(Laser.LengthOf(b), Is.EqualTo(clear).Within(1e-3f));
            Assert.That(Laser.DotShownOf(b), Is.False, "no square, no dot");

            // In its way: stopped at the body's side, not its middle.
            Put(_a.transform, onLine);
            _player.TickLaser();
            Assert.That(Laser.BodyOf(b), Is.SameAs(_a), "an enemy in its way stops it");
            Vector3 feet = _a.transform.position;
            Assert.That(LaserBeam.HitsBody(from.x, from.y - feet.y, from.z, dir.x, dir.y, dir.z, feet.x, feet.z, _a.Radius, Laser.Values.BodyTop, 10f, out float expected), Is.True);
            Assert.That(Laser.LengthOf(b), Is.EqualTo(expected).Within(1e-3f), "at its body");
            Assert.That(Vector3.Distance(Flat(Laser.EndOf(b)), Flat(feet)), Is.EqualTo(_a.Radius).Within(0.02f), "at its side, not its middle");
            Assert.That(Vector3.Angle(Laser.DirectionOf(b), dir), Is.LessThan(1e-3f), "still along the barrel");
            Assert.That(Laser.DotShownOf(b), Is.False, "the dot is off");
        }

        [UnityTest]
        public IEnumerator AWallInItsWay_StopsItThere_AndTheDot_OffByDefault_ShowsWithItsSwitch()
        {
            yield return AimedEast();
            const int b = LaserSight.Right;
            _player.TickLaser();
            Vector3 from = Laser.FromOf(b), dir = Laser.DirectionOf(b);
            Vector3 flat = Flat(dir).normalized;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "LaserTest_Wall";
            _made.Add(wall);
            Vector3 face = from + dir * 1.5f;
            wall.transform.SetPositionAndRotation(new Vector3(face.x, 1.5f, face.z) + flat * 0.1f, Quaternion.LookRotation(flat, Vector3.up));
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            _player.TickLaser();
            Assert.That(Laser.ColliderOf(b), Is.SameAs(wall.GetComponent<Collider>()), "the wall stops it");
            Assert.That(Laser.LandedOf(b), Is.True);
            Assert.That(Laser.LengthOf(b), Is.EqualTo(1.5f).Within(0.05f), "at its face");
            Assert.That(Laser.Values.DotOn, Is.False, "the dot's switch is off by default");
            Assert.That(Laser.DotShownOf(b), Is.False, "so no dot where it lands");

            Laser.Values.DotOn = true;
            _player.TickLaser();
            Assert.That(Laser.DotShownOf(b), Is.True, "with the switch on, a dot where it lands");
            Assert.That(Vector3.Distance(Laser.DotAt(b), Laser.EndOf(b)), Is.LessThan(0.05f), "at the wall's face");
            Laser.Values.DotOn = false;
        }

        // ---------------- Thin, red, added light ----------------

        [UnityTest]
        public IEnumerator TheBeam_IsRedByItsNamedValue_AddedLight_UnderTheBloom()
        {
            for (int i = 0; i < 4; i++) yield return null;
            _player.TickLaser();
            LaserBeamValues v = Laser.Values;
            Assert.That((v.Colour.R, v.Colour.G, v.Colour.B), Is.EqualTo((1f, 0.08f, 0.06f)));
            Color named = new Color(v.Colour.R, v.Colour.G, v.Colour.B);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) named = named.linear;
            Mesh mesh = Laser.MeshOf(LaserSight.Right);
            Color[] colours = mesh.colors;
            Color atMuzzle = colours[1]; // the first row's middle (its edges are dark: soft across)
            Assert.That(colours[0].maxColorComponent, Is.Zero, "dark at its edge");
            Assert.That(atMuzzle.r, Is.GreaterThan(0f).And.LessThan(1.2f), "lit, under the bloom's threshold");
            Assert.That(atMuzzle.g / atMuzzle.r, Is.EqualTo(named.g / named.r).Within(1e-4f), "its red");
            Assert.That(atMuzzle.b / atMuzzle.r, Is.EqualTo(named.b / named.r).Within(1e-4f));
            Material m = Laser.Material;
            Assert.That(m, Is.Not.Null);
            Assert.That(m.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.One), "added onto the picture");
            Assert.That(m.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
            Vector3[] verts = mesh.vertices;
            float across = Vector3.Distance(verts[0], verts[2]) * 0.5f; // where it is half lit: from edge to edge is twice that
            TestContext.Out.WriteLine("drawn across at the muzzle, at half its light: " + across.ToString("0.0000") + " m (named " + v.Width + " m, never under " + v.LeastPixels + " pixels)");
            Camera cam = Camera.main;
            float pixel = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight * Vector3.Distance(cam.transform.position, Laser.FromOf(LaserSight.Right));
            float expected = Mathf.Max(v.Width, v.LeastPixels * pixel);
            TestContext.Out.WriteLine("a pixel there spans " + pixel.ToString("0.0000") + " m (the camera " + cam.pixelWidth + " by " + cam.pixelHeight + " pixels)");
            Assert.That(across, Is.EqualTo(expected).Within(expected * 0.05f), "its named width, or a few pixels where that is less than a pixel");
            Assert.That(v.Width * 1080f / (2f * Mathf.Tan(15f * Mathf.Deg2Rad) * 28f), Is.LessThan(1.3f), "at the game's camera (28 m, 30 degrees, 1080 rows) the named width is under a pixel and a half: thin, never a band");
        }

        // ---------------- When it shows ----------------

        [UnityTest]
        public IEnumerator ItShows_InTheDash_AndWhenStunned_NotWhenSheIsDead_NorWithoutThePart_NorWithHerPistolsHidden()
        {
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(Laser.Shown, Is.True, "fitted: shown");
            Assert.That(_player.DashToward(_player.Position + Vector3.right * 4f), Is.EqualTo(IntentResult.Started));
            yield return null;
            Assert.That(_player.IsDashing, Is.True);
            yield return AtTheBarrels("in the dash", 2);
            yield return Until(() => !_player.IsDashing, 2f);

            Assert.That(_player.Stun(0.5f, "test"), Is.True, "stunned");
            yield return null;
            Assert.That(_player.IsStunned, Is.True);
            yield return AtTheBarrels("stunned", 2);
            yield return Until(() => !_player.IsStunned, 2f);

            Fit(WeaponAttachments.BullBarrel);
            yield return null;
            Assert.That(Laser.Shown, Is.False, "another part only: none");
            _player.Modifiers = null;
            yield return null;
            Assert.That(Laser.Shown, Is.False, "no run's parts: none");
            Fit();
            yield return null;
            Assert.That(Laser.Shown, Is.True);

            Transform pistols = _player.RightMuzzle.parent;
            pistols.gameObject.SetActive(false);
            yield return null;
            Assert.That(Laser.ShownOf(LaserSight.Right), Is.False, "her right pistol hidden: no beam from it");
            Assert.That(Laser.ShownOf(LaserSight.Left), Is.True, "the left still shows");
            pistols.gameObject.SetActive(true);
            yield return null;

            _health.TakeHit(100000);
            Assert.That(_health.IsAlive, Is.False);
            yield return null;
            Assert.That(Laser.Shown, Is.False, "dead: none");
        }

        // ---------------- Allocation, the fog, teardown ----------------

        [UnityTest]
        public IEnumerator OnABuiltFloor_ItAllocatesNothingPerFrame_AndIsBrighterByAWallThanInTheMiddle()
        {
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(0x09100001UL, 1), 1);
            Assert.That(level, Is.Not.Null);
            yield return null;
            Fit();
            _health.Untouchable = true;
            LevelRoom room = level.Layout.Rooms[0]; // the entrance: nobody there
            Vector3 middle = OnFloor(level.Middle(room));
            _player.Teleport(middle);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(level.Mood, Is.Not.Null, "the room's mood");
            Assert.That(level.Mood.Here, Is.EqualTo(room.Order), "she is in the entrance");

            // The fog's share along the beams: in the room's middle, and by its west wall, the beams pointed at the floor.
            float FogOn()
            {
                _player.TickLaser();
                return Mathf.Max(Laser.FogOf(0), Laser.FogOf(1));
            }
            float inMiddle = FogOn();
            float cs = level.Layout.CellSize;
            Vector3 byWall = OnFloor(level.ToWorld(new LevelPoint((room.Rect.X + 0.4) * cs + 0.9, (room.Rect.Y + room.Rect.Height * 0.5) * cs)));
            _player.Teleport(byWall);
            for (int i = 0; i < 5; i++) yield return null;
            float atWall = FogOn();
            string words = "Work order 102, the fog's mean share along the beams: in the entrance's middle " + inMiddle.ToString("0.000") + ", by its west wall " + atWall.ToString("0.000")
                           + " (she at " + byWall.ToString("F1") + ")";
            Debug.Log(words);
            TestContext.Out.WriteLine(words);
            Assert.That(atWall, Is.GreaterThan(inMiddle), "brighter where the fog lies");

            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            for (int i = 0; i < 10; i++) _player.TickLaser();
            Assert.That(() => { for (int i = 0; i < 300; i++) _player.TickLaser(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of the beams with the fog read allocates nothing");
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) _player.TickLaser();
            watch.Stop();
            string cost = "Work order 102: the beams cost " + (watch.Elapsed.TotalMilliseconds / 1000).ToString("0.0000") + " ms a frame on a built floor (two rays, " + Damageable.All.Count(d => d.IsAlive) + " targets)";
            Debug.Log(cost);
            TestContext.Out.WriteLine(cost);
            Assert.That(watch.Elapsed.TotalMilliseconds / 1000, Is.LessThan(0.5));
            _health.Untouchable = false;
            GameSettings.Unload();
        }

        static object s_junk;

        static Vector3 OnFloor(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : p;

        [UnityTest]
        public IEnumerator EverythingItMakes_GoesWithHerScene()
        {
            for (int i = 0; i < 4; i++) yield return null;
            GameObject root = Laser.Object;
            Assert.That(root, Is.Not.Null, "made");
            Assert.That(root.GetComponent<LaserSightLate>(), Is.Not.Null, "with its late tick");
            Mesh right = Laser.MeshOf(LaserSight.Right), left = Laser.MeshOf(LaserSight.Left);
            Material material = Laser.Material;
            Assert.That(right != null && left != null && material != null, Is.True);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single); // she goes with her scene
            yield return null;
            Assert.That(root == null, Is.True, "its object is gone");
            Assert.That(right == null && left == null, Is.True, "its meshes");
            Assert.That(material == null, Is.True, "its material");
            _player = null;
        }

        [Test]
        public void TheLateTick_RunsAfterHerArmsArePosed_AndTheCamerasMove()
        {
            int Order(System.Type t) => (System.Attribute.GetCustomAttribute(t, typeof(DefaultExecutionOrder)) as DefaultExecutionOrder)?.order ?? 0;
            Assert.That(Order(typeof(LaserSightLate)), Is.GreaterThan(Order(typeof(CharacterAnimation))), "after the arms are aimed");
            Assert.That(Order(typeof(LaserSightLate)), Is.GreaterThan(Order(typeof(CameraRig))), "and the camera has moved");
        }

        [UnityTest]
        public IEnumerator Asked_BeforeSheHasWoken_ItShowsNothing_MakesNothing_AndThrowsNothing()
        {
            var go = new GameObject("HerBeforeAwake_102");
            go.SetActive(false); // nothing on it wakes
            _made.Add(go);
            go.AddComponent<PlayerHealth>(); // brings her character and its agent with it
            PlayerCharacter her = go.GetComponent<PlayerCharacter>();
            var m = new RunModifiers();
            m.Weapon.Add(WeaponRack.Find(new MetaValues(), WeaponAttachments.LaserSight));
            her.Modifiers = m;
            Assert.DoesNotThrow(() => her.TickLaser());
            Assert.That(her.Laser.Shown, Is.False, "no pistols yet: no beams");
            Assert.That(her.Laser.Object, Is.Null, "and nothing made");
            yield return null;
        }
    }
}
