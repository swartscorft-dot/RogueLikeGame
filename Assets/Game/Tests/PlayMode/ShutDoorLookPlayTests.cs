using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 100 (Kristian, 2026-11-24: the shut door "Mørk stein eller jern, glød i kantene"; the entrance "Varmt som de
    /// andre"), in the ControlRoom scene through the game's intents. In the game's view a shut door's block is the dark body, not
    /// the door orange, with its rims the glowing door orange, on every face but the bottom, the top's too; it is one mesh in
    /// metres, never scaled, moved whole while it rises and sinks, so the band neither stretches nor swims. In the greybox view
    /// the whole block is the door orange glowing as before, and back in the game the dark face again; with the dark face off the
    /// game shows work order 086's orange block. Every material and the mesh a door makes go with it. Nothing allocates while a
    /// block rises or sinks. The entrance's main lamps (its two braziers) are lit in the warm colour of their named value on floor
    /// 1 and floor 2 of work order 091's run seed, and the mood's own lights there are the candles' colour; every other room's
    /// braziers and every lantern stay teal. No component of this order reads anything in Awake.
    /// </summary>
    public class ShutDoorLookPlayTests
    {
        const ulong RunSeed = 0x41414141UL;            // RoomDoorsPlayTests' run: its first room after the passage is a combat room
        const ulong EntranceRunSeed = 0x09100001UL;    // work order 091's run seed (226K2223)

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        bool _hadPref;
        int _pref;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _hadPref = PlayerPrefs.HasKey(GameSettings.GreyboxKey);
            _pref = PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null)
            {
                _player.Stop();
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.GreyboxKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.GreyboxKey);
            PlayerPrefs.Save();
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

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        GeneratedLevel StartRun(Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static LevelRoom FirstRoom(GeneratedLevel level)
        {
            LevelRoom room = level.Layout.Rooms[2];
            Assert.That((level.Layout.Rooms[1].Kind, room.Kind), Is.EqualTo((RoomKind.Passage, RoomKind.Combat)));
            return room;
        }

        /// <summary>She stands past the line of the room: the room wakes and its doors shut. The room's doors.</summary>
        IEnumerator ShutIn(GeneratedLevel level, LevelRoom room, Action<GeneratedLevels.RoomDoors> done)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, 5.5, 0d)));
            yield return Until(() => _levels.CurrentRoom == room, 30);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            Assert.That(d, Is.Not.Null, "the room has its doors");
            yield return Until(() => d.Shut, 10);
            Assert.That(d.Shut, Is.True, "past the line: shut");
            done(d);
        }

        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static float Diff(Color a, Rgb b) => Diff(a, CryptKit.Picked(b));
        static Color BaseOf(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        static Color Emission(Material m) => m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor") : Color.black;
        static Color Times(Rgb c, float k) => new Color(c.R * k, c.G * k, c.B * k, 1f);

        /// <summary>The block's mesh checked: its size the collider's, the edge submesh only within the band of a face's rim and on every face but the bottom, the top's too.</summary>
        static void CheckMesh(RoomDoor b)
        {
            Mesh mesh = b.Mesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(b.Block.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.subMeshCount, Is.EqualTo(2), "body and edge");
            Vector3 size = b.Box.size;
            Assert.That(Vector3.Distance(mesh.bounds.size, size), Is.LessThan(1e-3f), "the block's size in metres: " + mesh.bounds.size + " for " + size);
            Vector3 h = size * 0.5f;
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            float w = b.EdgeWidth;
            Assert.That(w, Is.GreaterThan(0f));
            var facesWithEdge = new HashSet<Vector3>();
            foreach (int i in mesh.GetTriangles(1))
            {
                Vector3 p = v[i], normal = n[i];
                facesWithEdge.Add(normal);
                // On the face: the two other axes' distances from the rim; the least of them is within the band.
                float least = float.MaxValue;
                for (int axis = 0; axis < 3; axis++)
                {
                    if (Mathf.Abs(normal[axis]) > 0.5f)
                    {
                        Assert.That(Mathf.Abs(Mathf.Abs(p[axis]) - h[axis]), Is.LessThan(1e-4f), "on the box's surface");
                        continue;
                    }
                    least = Mathf.Min(least, h[axis] - Mathf.Abs(p[axis]));
                }
                Assert.That(least, Is.LessThanOrEqualTo(w + 1e-4f), "the edge lies within its band of the rim");
            }
            Assert.That(facesWithEdge.Contains(Vector3.up), Is.True, "the top's rim glows (the camera looks down on it)");
            Assert.That(facesWithEdge.Contains(Vector3.down), Is.False, "the bottom (never seen) has none");
            Assert.That(facesWithEdge.Count, Is.EqualTo(5));
            // The body's inner parts keep clear of the band (all but the bottom).
            foreach (int i in mesh.GetTriangles(0))
            {
                if (n[i].y < -0.5f) continue;
                float least = float.MaxValue;
                for (int axis = 0; axis < 3; axis++) if (Mathf.Abs(n[i][axis]) < 0.5f) least = Mathf.Min(least, h[axis] - Mathf.Abs(v[i][axis]));
                Assert.That(least, Is.GreaterThanOrEqualTo(w - 1e-4f), "the body inside the band");
            }
        }

        // ---------------- The game's view ----------------

        [UnityTest]
        public IEnumerator InTheGame_TheBlockIsTheDarkBody_NotTheDoorOrange_ItsRimsGlowInTheDoorOrange_OneMeshInMetresNeverScaled()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            RoomFightValues v = _levels.Values.Fights;
            Rgb orange = new GreyboxValues().Door;
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level), x => d = x);
            yield return GameTime(v.DoorRiseSeconds + 0.1f);
            Assert.That(d.Blocks.Count, Is.EqualTo(d.Doors.Count));
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.GreyboxLook, Is.False);
                Material[] ms = b.BlockRenderer.sharedMaterials;
                Assert.That(ms.Length, Is.EqualTo(2));
                Assert.That(ms[0], Is.SameAs(b.Body), "the body first");
                Assert.That(ms[1], Is.SameAs(b.Edge), "the edge second");
                Assert.That(b.BlockRenderer.sharedMaterial, Is.SameAs(b.Body));
                Assert.That(Diff(BaseOf(b.Body), v.DoorBody), Is.LessThan(0.01f), "the body is the dark value: " + BaseOf(b.Body));
                Assert.That(Diff(BaseOf(b.Body), orange), Is.GreaterThan(0.5f), "and not the door orange");
                Assert.That(Emission(b.Body), Is.EqualTo(Color.black), "the body does not glow: the lamps light it");
                Assert.That(Diff(BaseOf(b.Edge), orange), Is.LessThan(0.01f), "the edge is the door orange");
                Assert.That(Diff(Emission(b.Edge), Times(orange, v.DoorEdgeGlow)), Is.LessThan(0.02f), "and glows by its named value: " + Emission(b.Edge));
                Assert.That(b.Block.localScale, Is.EqualTo(Vector3.one), "never scaled");
                Assert.That(Vector3.Distance(b.Block.lossyScale, Vector3.one), Is.LessThan(1e-4f));
                Assert.That(b.EdgeWidth, Is.EqualTo(v.DoorEdgeWidth).Within(1e-5f), "the band as named");
                CheckMesh(b);
            }
        }

        [UnityTest]
        public IEnumerator RisingAndSinking_MoveTheBlockWhole_TheEdgeNeitherStretchesNorSwims()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelRoom room = FirstRoom(level);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            RoomDoor b = d.Blocks[0];
            Mesh mesh = b.Mesh;
            Vector3[] was = mesh.vertices;
            var seen = new List<float>();
            void Look()
            {
                Assert.That(b.Block.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh), "the same mesh");
                Assert.That(b.Block.localScale, Is.EqualTo(Vector3.one), "never scaled while it moves");
                Assert.That(b.Block.localPosition.x == 0f && b.Block.localPosition.z == 0f, "moved only up and down");
                if (b.Raised > 0f && b.Raised < 1f && !seen.Contains(b.Raised)) seen.Add(b.Raised);
            }
            float deadline = Time.realtimeSinceStartup + 20f; // frames without a window are short: the rise takes many of them
            while (d.Moving && Time.realtimeSinceStartup < deadline)
            {
                Look();
                yield return null;
            }
            Look();
            Assert.That(b.Raised, Is.EqualTo(1f), "risen");
            foreach (Enemy e in level.EnemiesByRoom[room.Order].ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
            yield return Until(() => !d.Shut, 30);
            Assert.That(d.Shut, Is.False, "cleared: open, sinking");
            deadline = Time.realtimeSinceStartup + 20f;
            while (d.Moving && Time.realtimeSinceStartup < deadline)
            {
                Look();
                yield return null;
            }
            Assert.That(b.Raised, Is.EqualTo(0f), "sunk");
            Assert.That(seen.Count, Is.GreaterThan(2), "seen part risen on its way up and down");
            Assert.That(mesh.vertices, Is.EqualTo(was), "its vertices never changed: the band is where it was on every face");
        }

        [UnityTest]
        public IEnumerator WithTheDarkFaceOff_TheGameShowsWorkOrder086sOrangeBlock()
        {
            GeneratedLevel level = StartRun(v => v.Fights.DoorDarkFace = false);
            yield return Frames(2);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level), x => d = x);
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.Body, Is.Null, "no body made");
                Assert.That(b.Edge, Is.Null, "no edge made");
                Assert.That(b.BlockRenderer.sharedMaterials.All(m => m == b.DoorOrange), "the door orange all over");
            }
        }

        // ---------------- The greybox view ----------------

        [UnityTest]
        public IEnumerator InTheGreyboxView_TheWholeBlockIsTheDoorOrangeGlowingAsBefore_AndBackInTheGame_TheDarkFace()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            RoomFightValues v = _levels.Values.Fights;
            Rgb orange = new GreyboxValues().Door;
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(2);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, "the greybox view is on");
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level), x => d = x); // made while the greybox view is on
            void Orange(string when)
            {
                foreach (RoomDoor b in d.Blocks)
                {
                    Assert.That(b.GreyboxLook, Is.True, when);
                    Material[] ms = b.BlockRenderer.sharedMaterials;
                    Assert.That(ms.Length == 2 && ms[0] == b.DoorOrange && ms[1] == b.DoorOrange, "the door orange all over: " + when);
                    Assert.That(Diff(BaseOf(b.DoorOrange), orange), Is.LessThan(0.01f), when);
                    Assert.That(Diff(Emission(b.DoorOrange), Times(orange, v.DoorGlow)), Is.LessThan(0.02f), "glowing by DoorGlow as before: " + when);
                }
            }
            Orange("made in the greybox view");
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(2);
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.GreyboxLook, Is.False);
                Assert.That(b.BlockRenderer.sharedMaterials, Is.EqualTo(new[] { b.Body, b.Edge }), "the game's dark face again");
            }
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(2);
            Orange("turned on again with the door shut");
        }

        // ---------------- What it makes, and its frame ----------------

        [UnityTest]
        public IEnumerator EveryMaterialAndTheMesh_ADoorMakes_GoWithIt()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level), x => d = x);
            var made = new List<Object>();
            foreach (RoomDoor b in d.Blocks) made.AddRange(new Object[] { b.DoorOrange, b.Body, b.Edge, b.Mesh });
            Assert.That(made.All(o => o != null), "made");
            Assert.That(made.Distinct().Count(), Is.EqualTo(made.Count), "each its own");
            // The floor goes (a new one is built): its doors' blocks go with its root, and all they made with them.
            _levels.Build(0x10010001UL, 1);
            yield return Frames(3);
            Assert.That(made.All(o => o == null), "all destroyed with the floor: " + string.Join(", ", made.Where(o => o != null).Select(o => o.name)));
        }

        [UnityTest]
        public IEnumerator NothingAllocates_WhileABlockRisesOrSinks()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, FirstRoom(level), x => d = x);
            yield return GameTime(_levels.Values.Fights.DoorRiseSeconds + 0.1f);
            List<RoomDoor> blocks = d.Blocks.ToList();
            for (int i = 0; i < 10; i++) foreach (RoomDoor b in blocks) b.Show((i % 10) * 0.1f + 0.05f);
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                    for (int k = 0; k < blocks.Count; k++) blocks[k].Show((i % 19) / 19f + 0.01f); // every step a new height, as rising and sinking go
            }, AllocExt.AllocatingGCMemory(Is.Not), "a block moving: nothing");
            foreach (RoomDoor b in blocks) b.Show(1f);
            // The doors' own step while the blocks move (they rise again from part way: the controller moves them).
            d.Moving = true;
            for (int i = 0; i < 10; i++) _levels.TickRoomDoorsForTests();
            d.Moving = true;
            Assert.That(() => { for (int i = 0; i < 300; i++) { d.Moving = true; _levels.TickRoomDoorsForTests(); } }, AllocExt.AllocatingGCMemory(Is.Not), "the doors' step while moving: nothing");
        }

        // ---------------- The entrance ----------------

        [UnityTest]
        public IEnumerator TheEntrancesMainLamps_AreWarmByTheirNamedValue_OnFloorOneAndTwo_TheOtherBraziersAndTheLanternsStayTeal()
        {
            LampValues lamps = LamplitView.Lamps;
            MoodValues mood = new LevelValues().Mood;
            GameSettings.SetLightStep(3, keep: false);
            foreach (int floor in new[] { 1, 2 })
            {
                _levels.Values = new LevelValues();
                GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(EntranceRunSeed, floor), floor);
                Assert.That(level, Is.Not.Null);
                yield return null;
                _levels.FollowLamplit();
                if (_levels.Lamplit != null) _levels.Lamplit.Tick();
                yield return null;
                LamplitView view = _levels.Lamplit;
                Assert.That(view != null && view.On != null && view.Root != null, "lamplit C on, its lamps made");
                int entrance = level.Layout.Rooms.First(r => r.Kind == RoomKind.Entrance).Order;
                int warm = 0, teal = 0;
                foreach (Light l in view.Root.GetComponentsInChildren<Light>(true))
                {
                    string[] parts = l.name.Split('_'); // Lamp_<kind>_<room>_<n>
                    if (parts.Length < 4 || parts[0] != "Lamp") continue;
                    int room = int.Parse(parts[2]);
                    if (parts[1] == "Brazier" && room == entrance)
                    {
                        warm++;
                        FloorPaletteView palette = level.GetComponent<FloorPaletteView>(); // changed deliberately (work order 105): on floor 2 times its palette's lamp tint
                        Rgb want = palette != null ? FloorPaletteRules.Times(lamps.EntranceBrazier, palette.Palette.Lamps) : lamps.EntranceBrazier;
                        Assert.That(Diff(l.color, want), Is.LessThan(1e-3f), "floor " + floor + ": the entrance's brazier warm by its named value: " + l.color);
                    }
                    else if (parts[1] == "Brazier" || parts[1] == "Lantern")
                    {
                        teal++;
                        Assert.That(Diff(l.color, lamps.Teal), Is.LessThan(1e-3f), "floor " + floor + ": " + l.name + " stays teal");
                    }
                }
                Assert.That(warm, Is.EqualTo(2), "floor " + floor + ": the entrance's two braziers, each a light of its own");
                Assert.That(teal, Is.GreaterThan(0), "floor " + floor + ": teal lamps elsewhere");
                RoomMood.RoomParts parts0 = level.Mood != null ? level.Mood.Of(entrance) : null;
                Assert.That(parts0, Is.Not.Null, "the entrance's mood");
                Assert.That(parts0.Lights.Count, Is.GreaterThan(0));
                foreach (Light l in parts0.Lights)
                    Assert.That(Diff(l.color, mood.CandleColour), Is.LessThan(1e-3f), "floor " + floor + ": the mood's own light at an entrance brazier in the candles' colour (as before light)");
            }
        }
    }
}
