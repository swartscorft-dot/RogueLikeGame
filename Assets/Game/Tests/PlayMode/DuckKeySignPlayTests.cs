using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The duck's key sign and its slightly larger reach (work order 132; Kristian, 2026-11-25: "Ctrl/shift når det er mulig. Kanskje
    /// gjøre området litt større"), in the ControlRoom scene through the intents: the sign shows exactly while the quick duck's rim
    /// does, names the key the cover action is bound to (and follows a rebinding), and stands beside the rim, not over the prop;
    /// the reach's factor moves the distance at which the rim first shows and a tap ducks her (1.25 m on, 1 m off); a duck from
    /// the new edge still stops an arrow from beyond the prop; a frame of the sign allocates nothing; off, no sign and the old
    /// reach. And, as numbers for the report: how often a duck is possible in the fullest fight room of floor 5 of the
    /// benchmark's seed, at 1 m and at 1.25 m.
    ///
    /// One picture of the sign at the rim at the game's own camera, 1920 by 1080, drawn off screen, is taken only when
    /// ROGUELIKE_CAPTURE names a folder:  $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\132'
    ///
    /// The practice room has nothing to duck behind, so a test builds its props as a level builds them (QuickDuckTests' way).
    /// </summary>
    public class DuckKeySignPlayTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        static readonly Vector3 Stand = Vector3.zero;
        static readonly Vector2Int Picture1080 = new Vector2Int(1920, 1080);

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        InputBindings _bindingsWas;
        FeelValues _feel;
        bool _takeCoverWas, _quickDuckWas, _duckHoldWas, _coverStopsWas;
        Vector3 _ahead, _side;
        DuckKeySign _sign;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        RunHud _hud;
        readonly List<Object> _made = new List<Object>();
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            DuckKeySignValues.On = true;
            DuckKeySignValues.ReachFactor = 1.25f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _bindingsWas = _driver.Bindings;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _quickDuckWas = _feel.QuickDuck;
            _duckHoldWas = _feel.DuckHold;
            _coverStopsWas = _feel.CoverStopsShots;
            _feel.TakeCover = true;
            _feel.QuickDuck = true;
            _feel.CoverStopsShots = true;
            _feel.DuckHold = false; // the tap, as QuickDuckTests; the held duck where a test says so

            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice enemy");
            brute.enabled = false;
            Vector3 away = Flat(Stand - brute.transform.position);
            Vector3[] axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            _ahead = axes.OrderByDescending(a => Vector3.Dot(a, away.normalized)).First();
            _side = Vector3.Cross(Vector3.up, _ahead);
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Put(d.transform, Stand - _ahead * 13f + _side * (k++ * 3f - 7.5f));
            }
            _sign = DuckKeySign.Ensure();
            InterfaceStyle.ScreenForPictures = Picture1080; // its place is worked out for a screen of 1920 by 1080
            _player.Teleport(Stand);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            DuckKeySignValues.On = true;
            DuckKeySignValues.ReachFactor = 1.25f;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_driver != null) _driver.Bindings = _bindingsWas;
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.QuickDuck = _quickDuckWas;
                _feel.DuckHold = _duckHoldWas;
                _feel.CoverStopsShots = _coverStopsWas;
            }
            if (_health != null) _health.Untouchable = false;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_sign != null) Object.Destroy(_sign.gameObject);
            _sign = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null)
            {
                _levels.ClearSpawned();
                if (_levels.IsInLevel) _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The sign ----------------

        [UnityTest]
        public IEnumerator TheSign_ShowsExactlyWhileTheRimShows_NamesTheBoundKey_AndStandsBesideTheRim()
        {
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.8f));
            yield return Synced();
            int frames = 0, shownFrames = 0;
            IEnumerator Watch(int n)
            {
                for (int i = 0; i < n; i++)
                {
                    yield return null;
                    frames++;
                    bool rim = _player.DuckRimOn != null;
                    if (rim) shownFrames++;
                    Assert.That(_sign.Showing, Is.EqualTo(rim), "frame " + frames + ": the sign shows exactly while the rim does");
                }
            }

            yield return Watch(5);
            Assert.That(_player.DuckRimOn, Is.SameAs(sarcophagus.GetComponent<Shelter>()), "in reach: the rim");
            Assert.That(_sign.Showing, Is.True);
            Assert.That(_sign.KeyText, Is.EqualTo("Ctrl"), "the cover key as the start bindings have it: Left Ctrl");
            Assert.That(_sign.Layout(), Is.True, "placed");
            Rect plate = _sign.PlateRect, rim = _sign.RimRect;
            Assert.That(plate.Overlaps(rim), Is.False, "beside the rim's box on the screen, never over the prop: plate " + plate + ", rim " + rim);
            Assert.That(Mathf.Min(Mathf.Abs(plate.xMin - rim.xMax), Mathf.Abs(rim.xMin - plate.xMax)), Is.EqualTo(DuckKeySignValues.Gap).Within(0.01f), "at the rim");
            Assert.That(plate.xMin >= 0f && plate.yMin >= 0f && plate.xMax <= 1920f && plate.yMax <= 1080f, Is.True, "on the screen");
            Assert.That(plate.height, Is.LessThan(25f), "small");
            Vector3 her = Camera.main.WorldToScreenPoint(_player.Position);
            Assert.That(her.x <= rim.center.x ? plate.xMin >= rim.xMax : plate.xMax <= rim.xMin, Is.True, "on the side away from her");
            TestContext.WriteLine($"plate {plate} (layout px at 1920 by 1080), the rim's box {rim}, her x {her.x:0}");

            _player.Teleport(Stand - _ahead * 1.5f); // out of reach
            yield return Watch(5);
            Assert.That(_sign.Showing, Is.False, "out of reach: neither");
            _player.Teleport(Stand);
            yield return Watch(3);
            Assert.That(_sign.Showing, Is.True);

            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked));
            yield return Watch(3);
            Assert.That(_sign.Showing, Is.False, "down: a tap would not duck her, so no rim and no sign");
            float deadline = Time.realtimeSinceStartup + 4f;
            while ((_player.IsQuickDucking || _player.DuckWaitLeft > 0f) && Time.realtimeSinceStartup < deadline) yield return Watch(1);
            yield return Watch(3);
            Assert.That(_sign.Showing, Is.True, "the wait over: both again");

            // Rebound: the sign follows the bindings in use.
            var rebound = ScriptableObject.CreateInstance<InputBindings>();
            _made.Add(rebound);
            rebound.TakeCover = Key.C;
            _driver.Bindings = rebound;
            yield return Watch(2);
            Assert.That(_sign.KeyText, Is.EqualTo("C"), "the key it is really bound to");
            rebound.TakeCover = Key.RightShift;
            yield return Watch(2);
            Assert.That(_sign.KeyText, Is.EqualTo("Shift"));
            _driver.Bindings = _bindingsWas;

            _feel.QuickDuck = false;
            yield return Watch(3);
            Assert.That(_sign.Showing, Is.False, "the duck's switch off: no rim, no sign");
            Assert.That(shownFrames, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SwitchOff_TheRimShowsAsBefore_WithNoSign_AndTheReachIsTheOldOne()
        {
            DuckKeySignValues.On = false;
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.8f));
            yield return Synced();
            yield return null;
            Assert.That(_player.DuckRimOn, Is.Not.Null, "the rim as before");
            Assert.That(_sign.Showing, Is.False, "and no sign");
            Assert.That(_player.DuckReachInUse, Is.EqualTo(_feel.DuckReach), "the old reach");
            DuckKeySignValues.On = true;
            Assert.That(_player.DuckReachInUse, Is.EqualTo(_feel.DuckReach * 1.25f), "on: a quarter more");
        }

        // ---------------- The reach ----------------

        [UnityTest]
        public IEnumerator TheReachsFactor_MovesTheDistanceAtWhichTheRimFirstShows_AndATapDucksHer()
        {
            var cases = new (bool on, float factor, float expected)[] { (true, 1.25f, 1.25f), (false, 1.25f, 1f), (true, 1.5f, 1.5f) };
            GameObject prop = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 2.2f));
            yield return Synced();
            Shelter shelter = prop.GetComponent<Shelter>();
            Assert.That(shelter.TryShape(out CoverShape shape), Is.True);
            foreach ((bool on, float factor, float expected) in cases)
            {
                DuckKeySignValues.On = on;
                DuckKeySignValues.ReachFactor = factor;
                float lastOut = float.PositiveInfinity, firstIn = float.NaN;
                // Walking up to it in steps of 2 cm, from 2 m off its face.
                for (float face = 2f; face > 0.3f; face -= 0.02f)
                {
                    _player.Teleport(Stand + _ahead * (2.2f - face));
                    yield return null;
                    yield return null;
                    float d = shape.Distance(_player.GroundPosition);
                    if (_player.DuckRimOn == shelter) { firstIn = d; break; }
                    lastOut = d;
                }
                string what = (on ? "on" : "off") + ", factor " + factor.ToString(CultureInfo.InvariantCulture);
                TestContext.WriteLine($"{what}: the rim first shows {firstIn:0.000} m from the face (last without it {lastOut:0.000} m); the reach in use {_player.DuckReachInUse:0.000} m");
                Assert.That(firstIn, Is.LessThanOrEqualTo(expected + 1e-3f), what + ": not before the reach");
                Assert.That(lastOut, Is.GreaterThan(expected - 1e-3f), what + ": and at it");
                Assert.That(firstIn, Is.GreaterThan(expected - 0.05f), what);

                // A tap just inside ducks her; just outside does nothing.
                _player.Teleport(Stand + _ahead * (2.2f - (expected - 0.04f)));
                yield return null;
                Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), what + ": a tap just inside the reach");
                _player.Teleport(Stand + _ahead * (2.2f - (expected + 0.06f)));
                yield return null;
                Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.NothingInReach), what + ": just outside, nothing");
            }
        }

        [UnityTest]
        public IEnumerator ADuck_FromTheNewEdgeOfTheReach_StillStopsAnArrowFromBeyondTheProp_SheTakesNothing()
        {
            Assert.That(_player.DuckReachInUse, Is.EqualTo(1.25f).Within(1e-4f));
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 1.2f)); // out of the old reach of 1 m
            yield return Synced();
            yield return null;
            Assert.That(_player.DuckRimOn, Is.SameAs(sarcophagus.GetComponent<Shelter>()), "1.2 m off: in the new reach");
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            EnemyShooter shooter = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _ahead * 9f);
            var strikes = new List<EnemyReport>();
            shooter.Reported += r => { if (r.Kind == EnemyReportKind.Struck) strikes.Add(r); };
            int health = _health.Current;
            shooter.Provoke();
            float speed = shooter.Tuning.ArrowSpeed;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Eta(shooter, speed, shooter.Tuning.ArrowRadius) >= 0.25f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Eta(shooter, speed, shooter.Tuning.ArrowRadius), Is.LessThan(0.25f), "an arrow on its way, about to arrive");
            Assert.That(_player.TapCover(), Is.EqualTo(DuckTap.Ducked), "a tap at 1.2 m ducks her");
            deadline = Time.realtimeSinceStartup + 2f;
            while (strikes.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(strikes.Count, Is.EqualTo(1));
            Assert.That(strikes[0].Outcome, Is.EqualTo(StrikeOutcome.StoppedByCover), "the prop she ducked at stopped it, as at 0.5 m");
            Assert.That(_health.Current, Is.EqualTo(health), "she took nothing");
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(1));
        }

        // ---------------- Its cost ----------------

        [UnityTest]
        public IEnumerator AFrameOfTheSign_AllocatesNothing()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.8f));
            yield return Synced();
            yield return null;
            Assert.That(_sign.Showing, Is.True);
            _sign.Tick();
            _sign.Layout();
            Assert.That(() => { for (int i = 0; i < 300; i++) { _sign.Tick(); _sign.Layout(); } }, AllocExt.AllocatingGCMemory(Is.Not), "the sign's frame and its place allocate nothing");
        }

        // ---------------- How often, in floor 5's fight (numbers for the report) ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Floor5OfTheBenchmarksSeed_HowOftenADuckIsPossible_AtTheOldAndTheNewReach()
        {
            _levels = GeneratedLevels.Ensure();
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Level, Is.Not.Null, "floor 5 of the benchmark's seed");
            Assert.That(stage.Room, Is.Not.Null, "its fullest fight room");
            yield return null;
            var log = new List<string> { $"Floor 5 of the benchmark's seed (0x{BenchmarkStage.RunSeed:X}), its fullest fight room (order {stage.Room.Order}):" };

            // 1. The benchmark's fight itself: she stands where it puts her, shooting the nearest enemy every frame. The benchmark
            // makes her untouchable, and no duck is possible then (MayBeHeldNow), so here she can be hit and is healed each frame:
            // the enemies' stuns and pulls keep the rim off as they would in play.
            float standNearest = NearestLowProp(stage.Stand, out _);
            foreach (bool on in new[] { false, true })
            {
                DuckKeySignValues.On = on;
                int frames = 0, shown = 0;
                float until = Time.time + 3f, stop = Time.realtimeSinceStartup + 30f;
                while (Time.time < until && Time.realtimeSinceStartup < stop)
                {
                    stage.Keep();
                    _health.Untouchable = false;
                    if (_health.Current < _health.Max / 2) _health.RestoreFull();
                    yield return null;
                    frames++;
                    if (_player.DuckRimOn != null) shown++;
                }
                log.Add($"  the benchmark's fight, 3 s, reach {_player.DuckReachInUse:0.00} m: the rim shown in {shown} of {frames} frames ({Share(shown, frames)}); her stand is {standNearest:0.00} m from the nearest low prop's edge");
            }
            stage.End();
            DuckKeySignValues.On = true;

            // 2. Every place she can stand in that room and in the floor's rooms: from how many is a duck possible (a low prop within reach)?
            foreach (bool wholeFloor in new[] { false, true })
            {
                int places = 0, at1 = 0, at125 = 0;
                var propsAt1 = new HashSet<Shelter>();
                var propsAt125 = new HashSet<Shelter>();
                foreach (LevelRoom room in stage.Level.Layout.Rooms)
                {
                    if (!wholeFloor && room != stage.Room) continue;
                    foreach (Vector3 p in PlacesIn(stage.Level, room, 0.25f))
                    {
                        places++;
                        float d = NearestLowProp(p, out Shelter s);
                        if (d <= 1f) { at1++; propsAt1.Add(s); }
                        if (d <= 1.25f) { at125++; propsAt125.Add(s); }
                    }
                }
                string where = wholeFloor ? "every room of the floor" : "the fight room";
                log.Add($"  {where}: {places} places on its floor (a 0.25 m grid on the NavMesh); a duck possible from {at1} ({Share(at1, places)}) at 1 m, from {at125} ({Share(at125, places)}) at 1.25 m: {Ratio(at125, at1)} as many; low props in reach of some place {propsAt1.Count} and {propsAt125.Count}");
                Assert.That(at125, Is.GreaterThanOrEqualTo(at1));
            }
            foreach (string line in log) TestContext.WriteLine(line);
            Debug.Log("Work order 132:\n" + string.Join("\n", log));
        }

        // ---------------- The picture (only with ROGUELIKE_CAPTURE) ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator Picture_TheKeySignAtTheRim_InFloor5_AtTheGamesCamera_1920By1080()
        {
            string folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 132)");
            Directory.CreateDirectory(folder);
            GameSettings.SetCameraDistanceStep(0, keep: false); // the feel's own distance: the default
            InterfaceStyle.Shared.Kinetic = true;
            _feel.DuckHold = _duckHoldWas; // the game's own duck
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(BenchmarkStage.RunSeed, 5), 5);
            Assert.That(level, Is.Not.Null);
            LevelRoom room = BenchmarkStage.FullestCombatRoom(level);
            Vector3 middle = level.Middle(room);
            // The low prop of the room nearest its middle, and a place 0.9 m off its face toward the middle, on the NavMesh.
            Shelter best = null;
            CoverShape bestShape = default;
            float bestD = float.PositiveInfinity;
            foreach (Shelter s in Shelter.All)
            {
                if (s == null || s.IsTall || !s.TryShape(out CoverShape shape) || level.RoomAt(s.transform.position) != room) continue;
                float d = Flat(s.transform.position - middle).magnitude;
                if (d < bestD) { bestD = d; best = s; bestShape = shape; }
            }
            Assert.That(best, Is.Not.Null, "a low prop in the fight room");
            GroundPoint edge = bestShape.Closest(new GroundPoint(middle.x, middle.z));
            Vector3 edgeW = new Vector3(edge.X, 0f, edge.Z);
            Vector3 outward = Flat(middle - edgeW).normalized;
            Vector3 at = edgeW + outward * 0.9f;
            Assert.That(NavMesh.SamplePosition(at, out NavMeshHit hit, 0.5f, NavMesh.AllAreas), Is.True, "her place on the NavMesh");
            foreach (Enemy e in level.Enemies) if (e != null) e.enabled = false; // asleep, where the floor put them (she is not made untouchable: no duck then)
            _player.Teleport(hit.position);
            _hud = RunHud.Ensure();
            float settle = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Assert.That(_player.DuckRimOn, Is.SameAs(best), "the rim on it");
            Assert.That(_sign.Showing, Is.True, "and the sign");

            const int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            var lines = new List<string>();
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(width, height);
                cam.targetTexture = rt;
                _hud.Tick();
                SkillBar bar = Object.FindAnyObjectByType<SkillBar>();
                if (bar != null) bar.Tick();
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                _sign.Layout();
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(folder, "key-sign-at-the-rim.png"), tex.EncodeToPNG());
                // At 100 %: the part round the sign, pixel for pixel (the layout's pixels are the picture's at 1080 rows).
                Rect plate = _sign.PlateRect;
                int cw = 480, ch = 270;
                int cx = Mathf.Clamp(Mathf.RoundToInt(plate.center.x) - cw / 2, 0, width - cw);
                int cy = Mathf.Clamp(height - Mathf.RoundToInt(plate.center.y) - ch / 2, 0, height - ch);
                var crop = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                crop.SetPixels(tex.GetPixels(cx, cy, cw, ch));
                crop.Apply();
                File.WriteAllBytes(Path.Combine(folder, "key-sign-at-the-rim-100-percent.png"), crop.EncodeToPNG());
                Object.Destroy(crop);
                Object.Destroy(tex);
                lines.Add($"{DateTime.Now:HH:mm:ss} key-sign-at-the-rim: {width}x{height}, prop {best.name}, her {_player.Position}, sign '{_sign.KeyText}' at {plate}, the rim's box {_sign.RimRect}, drawn: {drew}");
            }
            catch (Exception e) { lines.Add("the picture failed: " + e); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Picture1080;
                rt.Release();
                Object.Destroy(rt);
            }
            File.AppendAllLines(Path.Combine(folder, "pictures-log.txt"), lines);
            foreach (string l in lines) TestContext.WriteLine(l);
            Assert.That(lines.Any(l => l.Contains("failed")), Is.False, string.Join("\n", lines));
        }

        // ---------------- Helpers ----------------

        static string Share(int n, int of) => of > 0 ? (100.0 * n / of).ToString("0.0", CultureInfo.InvariantCulture) + " %" : "-";
        static string Ratio(int a, int b) => b > 0 ? ((double)a / b).ToString("0.00", CultureInfo.InvariantCulture) + " times" : "-";

        /// <summary>Metres from a point to the edge of the nearest low prop (a shelter that is not tall), and which; infinity with none.</summary>
        static float NearestLowProp(Vector3 p, out Shelter which)
        {
            which = null;
            float best = float.PositiveInfinity;
            var her = new GroundPoint(p.x, p.z);
            foreach (Shelter s in Shelter.All)
            {
                if (s == null || s.IsTall || !s.TryShape(out CoverShape shape)) continue;
                float d = shape.Distance(her);
                if (d < best) { best = d; which = s; }
            }
            return best;
        }

        /// <summary>Places on a grid of <paramref name="step"/> m in a room, on its NavMesh (where she can stand).</summary>
        static IEnumerable<Vector3> PlacesIn(GeneratedLevel level, LevelRoom room, float step)
        {
            double cs = level.Layout.CellSize;
            Vector3 lo = level.ToWorld(new LevelPoint(room.Rect.X * cs, room.Rect.Y * cs));
            Vector3 hi = level.ToWorld(new LevelPoint(room.Rect.Right * cs, room.Rect.Top * cs));
            for (float z = lo.z + step * 0.5f; z < hi.z; z += step)
                for (float x = lo.x + step * 0.5f; x < hi.x; x += step)
                {
                    var p = new Vector3(x, 0f, z);
                    if (level.RoomAt(p) != room) continue;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.05f, NavMesh.AllAreas)) continue;
                    yield return new Vector3(hit.position.x, 0f, hit.position.z);
                }
        }

        float Eta(Enemy owner, float speed, float radius)
        {
            _fly.ListFlying(_shown);
            float best = float.PositiveInfinity;
            foreach (ThingsThatFly.Shown s in _shown)
            {
                if (s.Owner != owner || s.Ending || s.Passed) continue;
                float d = Flat(s.Position - _player.Position).magnitude - radius - Placeholders.PlayerRadius;
                best = Mathf.Min(best, Mathf.Max(0f, d) / Mathf.Max(0.1f, speed));
            }
            return best;
        }

        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            _levels = _levels != null ? _levels : GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        /// <summary>Where a prop's centre goes for its near face to stand <paramref name="face"/> metres from her stand along <paramref name="way"/>.</summary>
        static Vector3 FaceAt(PropModel model, Vector3 way, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            return Stand + way * (face + half);
        }

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>A prop as a level builds it (QuickDuckTests' way): a box turned to face her stand.</summary>
        GameObject Prop(PropModel model, Vector3 at)
        {
            Vector3 toHer = Flat(Stand - at);
            int rotation = toHer.sqrMagnitude > 1e-6f ? (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360 : 0;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        static IEnumerator Synced()
        {
            Physics.SyncTransforms();
            yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
