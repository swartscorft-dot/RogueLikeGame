using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The rooms' mood (work order 044; Kristian, 2026-11-20: "The rooms must also feel more alive and create atmosphere like
    /// Diablo and Path of Exile"), in the ControlRoom scene through <see cref="GeneratedLevels"/>: with the master on a built
    /// floor has its sources, pools, fog, dust and wear, inside every cap and the light budget; the practice room is as it was;
    /// removing a floor removes everything and puts the scene's light back; each switch off removes its layer; the master off
    /// gives exactly today's objects; her light goes with her; the enemies keep a floor on their light; nothing blocks walking
    /// (a path from each door of a room to each other is as it was); its frame allocates nothing. Writes what the largest room
    /// of floor 5 costs ("MOODCOST"). Nothing here looks at the picture: the look is Kristian's to judge by playing.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames, and nothing hangs on a frame's length.
    ///
    /// Work order 082 (review 6, finding 2): the caps and the budget, the master off, the enemies' light floor, the walking
    /// from door to door and the frame run with the passages off (the rooms touching, as the mood was built for them) and on
    /// (the floor he plays: no mood of a passage's own, the air ahead past the passage). The scene's light, the switches, her
    /// light and the room tone are kept with the passages off: a passage changes nothing of them.
    /// </summary>
    public class RoomMoodTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        const ulong Seed = 0x5EED5UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;

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
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null)
            {
                _levels.BackToPracticeRoom();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel Build(int floor = 5, Action<LevelValues> change = null, ulong seed = Seed, bool passages = false)
        {
            // Work order 059: the rooms touching, as the mood was built for them (her room and the next one in the run); the mood
            // across a passage is PassagesPlayTests.InAPassage_TheAirAheadIsOn_AndBehindOff_InARoom_TheRoomPastThePassage.
            // Work order 082: and with the passages on where a test says so.
            _levels.Values = new LevelValues { Passages = passages };
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        IEnumerator Into(GeneratedLevel level, LevelRoom room)
        {
            _player.Teleport(room.Kind == RoomKind.Passage ? InPassage(level, room) : level.Middle(room));
            yield return Frames(3);
        }

        /// <summary>A passage's middle cell of its path, on the ground (as PassagesPlayTests).</summary>
        static Vector3 InPassage(GeneratedLevel level, LevelRoom passage)
        {
            List<(int x, int y)> path = Passages.PathOf(level.Layout, passage);
            (int x, int y) c = path[path.Count / 2];
            double cs = level.Layout.CellSize;
            return level.ToWorld(new LevelPoint((c.x + 0.5) * cs, (c.y + 0.5) * cs));
        }

        /// <summary>The first room after this one in the run that is not a passage (the room whose air is on with hers), or -1.</summary>
        static int NextRoom(LevelLayout layout, int here)
        {
            for (int k = here + 1; k < layout.Rooms.Count; k++)
                if (layout.Rooms[k].Kind != RoomKind.Passage) return k;
            return -1;
        }

        static Light Key()
        {
            Light best = null;
            foreach (Light l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (best == null || l.intensity > best.intensity)) best = l;
            return best;
        }

        static bool SameProbe(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
        {
            for (int c = 0; c < 3; c++)
                for (int k = 0; k < 9; k++)
                    if (Mathf.Abs(a[c, k] - b[c, k]) > 1e-5f) return false;
            return true;
        }

        static int MoodRoots() => SceneManager.GetActiveScene().GetRootGameObjects().Count(g => g.name.StartsWith(RoomMood.RootName + "_", StringComparison.Ordinal));

        /// <summary>The lights of the floor and its mood that are on: the lanterns', the door's, the sources', hers.</summary>
        static int LightsOn(GeneratedLevel level) =>
            Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled && l.type != LightType.Directional
                && (l.transform.root == level.transform || (level.Mood != null && level.Mood.Root != null && l.transform.root == level.Mood.Root.transform)));

        // ---------------- With the master on ----------------

        [UnityTest]
        public IEnumerator MasterOn_AFloorHasItsSources_Pools_Fog_Dust_AndWear_InsideEveryCap_AndTheBudget([Values(false, true)] bool passages)
        {
            GeneratedLevel level = Build(passages: passages);
            yield return Frames(2);
            RoomMood mood = level.Mood;
            MoodValues v = level.Values.Mood;
            Assert.That(mood, Is.Not.Null, "on by default");
            Assert.That(mood.Root, Is.Not.Null);
            Assert.That(level.Layout.Rooms.Any(r => r.Kind == RoomKind.Passage), Is.EqualTo(passages));
            int rooms = LevelGeometry.RoomCount(level.Layout); // work order 082: a passage has no mood of its own
            Assert.That(mood.Rooms.Count, Is.EqualTo(rooms));
            Assert.That(mood.Rooms.All(p => level.Layout.Rooms[p.Order].Kind != RoomKind.Passage));
            Assert.That(mood.Plan.WearCount, Is.GreaterThan(rooms * 2), "a worn floor");
            Assert.That(mood.Rooms.Sum(r => r.Sources.Count), Is.GreaterThan(0), "sources");
            Assert.That(mood.Rooms.All(r => r.Glow != null), "pools in every room (the middle's at least)");
            Assert.That(mood.Rooms.Count(r => r.Decals != null), Is.GreaterThan(rooms / 2), "stains and cracks");
            Assert.That(mood.Rooms.All(r => r.Fog != null && r.Fog.main.maxParticles == v.FogMostPerRoom), "fog in every room, capped");
            Assert.That(mood.Dust, Is.Not.Null);
            Assert.That(mood.Dust.main.maxParticles, Is.EqualTo(v.DustMost));
            Assert.That(mood.Rooms.SelectMany(r => r.Embers).All(e => e.main.maxParticles == v.EmbersPerBrazier), "embers capped");
            Assert.That(mood.Rooms.SelectMany(r => r.Embers).Count(), Is.GreaterThan(0), "embers at the braziers");
            Assert.That(mood.Tone, Is.Not.Null);
            Assert.That(mood.Tone.loop && mood.Tone.isPlaying, "the room tone loops");
            Assert.That(mood.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing of it has a collider");
            Assert.That(mood.Root.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == CryptKit.Layer), "every piece on Ignore Raycast");
            Assert.That(mood.Rooms.Count(r => r.LargePropPlace != null), Is.GreaterThan(rooms / 2), "empty places for large props");
            Assert.That(mood.Rooms.Where(r => r.LargePropPlace != null).All(r => r.LargePropPlace.childCount == 0 && r.LargePropPlace.GetComponents<Component>().Length == 1), "empty");

            int budget = level.Values.Crypt.LightsPerRoom + level.Values.Signs.LightsMost + 1;
            int mostOn = 0;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                yield return Into(level, room);
                Assert.That(mood.Here, Is.EqualTo(room.Order));
                int on = LightsOn(level);
                mostOn = Mathf.Max(mostOn, on);
                Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
                int next = NextRoom(level.Layout, room.Order);
                Assert.That(mood.Next, Is.EqualTo(next), "the next room, past a passage");
                foreach (RoomMood.RoomParts p in mood.Rooms)
                {
                    bool air = p.Order == room.Order || p.Order == next;
                    Assert.That(p.Fog.isEmitting, Is.EqualTo(air), "fog only in her room and the next, room " + p.Order + ", she in " + room.Order);
                    Assert.That(p.Lights.All(l => l.enabled == (p.Order == room.Order)), "real lights only in her room");
                    Assert.That(p.Fog.particleCount, Is.LessThanOrEqualTo(v.FogMostPerRoom));
                    foreach (ParticleSystem e in p.Embers) Assert.That(e.particleCount, Is.LessThanOrEqualTo(v.EmbersPerBrazier));
                }
                Assert.That(mood.Dust.particleCount, Is.LessThanOrEqualTo(v.DustMost));
            }
            Debug.Log("MOODCOST light budget floor 5: at most " + mostOn + " lights on at once (the kit's " + level.Values.Crypt.LightsPerRoom
                      + " a room, the door's " + level.Values.Signs.LightsMost + ", hers 1)");

            // What the largest room costs.
            LevelRoom largest = level.Layout.Rooms.Where(r => r.Kind != RoomKind.Passage).OrderByDescending(r => r.Rect.Width * r.Rect.Height).First();
            RoomMood.RoomParts lp = mood.Of(largest.Order);
            long tris = 0;
            int meshes = 0;
            foreach (MeshRenderer r in new[] { lp.Lit, lp.Decals, lp.Glow })
            {
                if (r == null) continue;
                meshes++;
                Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                tris += m.GetIndexCount(0) / 3;
            }
            int enemies = level.EnemiesByRoom.TryGetValue(largest.Order, out List<Enemy> list) ? list.Count : 0;
            int overlays = list == null ? 0 : list.Sum(e => e.GetComponentsInChildren<Renderer>(true).Count(r => r.sharedMaterials.Any(m => m != null && m.name == "Mood_EnemyFloor")));
            MoodRoom plan = mood.Plan.Rooms.First(r => r.Room.Order == largest.Order);
            int pools = lp.Sources.Count + (v.Darkness && v.MiddleFill > 0 ? 1 : 0);
            int flames = plan.Sources.Count(s => s.Flame);
            int systems = (lp.Fog != null ? 1 : 0) + lp.Embers.Count + 1;
            int caps = v.FogMostPerRoom + lp.Embers.Count * v.EmbersPerBrazier + v.DustMost;
            long allTris = mood.Triangles(out int allMeshes);
            Debug.Log(string.Format(s_Inv,
                "MOODCOST floor 5 seed {0}, the largest room {1}{2} ({3}x{4} cells, {5}): real lights {6} of its sources and hers (the door's 1 more when lit); " +
                "emissive pools {7} (sources {8} and the middle's), flames on {9} sources; particle systems {10} playing there (fog cap {11}, embers {12} x {13}, dust cap {14}: {15} particles at most); " +
                "extra meshes {16} with {17} triangles (wear {18} pieces, eye-catcher {19} pieces); extra draw calls about {20} (one a mesh, one a playing system) and {21} enemy meshes drawn twice ({22} enemies); " +
                "the whole floor's mood {23} meshes, {24} triangles",
                level.Layout.SeedText, largest.Letter, largest.Order, largest.Rect.Width, largest.Rect.Height, RoomThemes.Word(largest.Theme),
                lp.Lights.Count, pools, lp.Sources.Count, flames, systems, v.FogMostPerRoom, lp.Embers.Count, v.EmbersPerBrazier, v.DustMost, caps,
                meshes, tris, plan.Wear.Count, plan.Pieces.Count, meshes + systems, overlays, enemies, allMeshes, allTris));
        }

        // ---------------- The scene's light, the practice room ----------------

        [UnityTest]
        public IEnumerator RemovingTheFloor_RemovesEverything_AndPutsTheScenesLightBack()
        {
            Light key = Key();
            Assert.That(key, Is.Not.Null, "the scene's key light");
            float intensity = key.intensity;
            Color colour = key.color;
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            Assert.That(MoodRoots(), Is.EqualTo(0), "the practice room has none");
            Assert.That(RoomMood.SceneLightTaken, Is.False);

            GeneratedLevel level = Build();
            yield return Frames(2);
            MoodValues v = level.Values.Mood;
            Assert.That(key.intensity, Is.EqualTo(intensity * v.KeyLevel).Within(1e-4f), "the key light down on a floor");
            Assert.That(RenderSettings.ambientProbe[1, 0], Is.EqualTo(probe[1, 0] * v.AmbientLevel * v.DarkTint.G).Within(1e-4f), "the ambient down on a floor");
            Assert.That(MoodRoots(), Is.EqualTo(1));

            // A floor over a floor (the next of a run): the light stays down once, and the first floor's mood goes.
            GeneratedLevel second = Build(3);
            yield return Frames(2);
            Assert.That(key.intensity, Is.EqualTo(intensity * v.KeyLevel).Within(1e-4f), "down once, not twice");
            Assert.That(MoodRoots(), Is.EqualTo(1), "the first floor's mood went with it");
            Assert.That(level == null, "the first floor is gone");

            _levels.BackToPracticeRoom();
            yield return Frames(2);
            Assert.That(second == null, "the floor is gone");
            Assert.That(MoodRoots(), Is.EqualTo(0), "and everything of its mood");
            Assert.That(Object.FindObjectsByType<RoomMood>(), Is.Empty);
            Assert.That(key.intensity, Is.EqualTo(intensity), "the key light as it was");
            Assert.That(key.color, Is.EqualTo(colour));
            Assert.That(SameProbe(RenderSettings.ambientProbe, probe), "the ambient as it was");
            Assert.That(RoomMood.SceneLightTaken, Is.False);
        }

        // ---------------- The switches ----------------

        [UnityTest]
        public IEnumerator EachSwitchOff_RemovesItsLayer()
        {
            Light key = Key();
            float intensity = key.intensity;

            GeneratedLevel level = Build(5, v => v.Mood.Darkness = false);
            yield return Frames(2);
            Assert.That(key.intensity, Is.EqualTo(intensity), "darkness off: today's light");
            Assert.That(RoomMood.SceneLightTaken, Is.False);

            level = Build(5, v => v.Mood.Sources = false);
            yield return Frames(2);
            Assert.That(level.Mood.Rooms.All(r => r.Sources.Count == 0 && r.Lights.Count == 0), "sources off: no pool, flame or light of theirs");

            level = Build(5, v => v.Mood.HerLight = false);
            yield return Frames(2);
            Assert.That(level.Mood.HerLight, Is.Null);

            level = Build(5, v => v.Mood.Fog = false);
            yield return Frames(2);
            Assert.That(level.Mood.Rooms.All(r => r.Fog == null));

            level = Build(5, v => v.Mood.Dust = false);
            yield return Frames(2);
            Assert.That(level.Mood.Dust, Is.Null);

            level = Build(5, v => v.Mood.Embers = false);
            yield return Frames(2);
            Assert.That(level.Mood.Rooms.All(r => r.Embers.Count == 0));

            level = Build(5, v => v.Mood.Wear = false);
            yield return Frames(2);
            Assert.That(level.Mood.Plan.WearCount, Is.EqualTo(0));
            Assert.That(level.Mood.Rooms.All(r => r.Decals == null), "no stain, crack or patch");

            level = Build(5, v => v.Mood.Compose = false);
            yield return Frames(2);
            Assert.That(level.Mood.Rooms.All(r => r.LargePropPlace == null) && level.Mood.Plan.Rooms.All(r => r.Pieces.Count == 0));

            level = Build(5, v => v.Mood.Tone = false);
            yield return Frames(2);
            Assert.That(level.Mood.Tone, Is.Null);
            Assert.That(level.Mood.Root.GetComponentsInChildren<AudioSource>(true), Is.Empty);

            level = Build(5, v => v.Mood.EnemyLightFloor = 0f);
            yield return Frames(2);
            Assert.That(level.Mood.FlooredRenderers, Is.EqualTo(0));

            // Turned while playing: the fog off, then the master off, take their layers away at once.
            level = Build();
            yield return Frames(2);
            int builds = level.Mood.Builds;
            level.Values.Mood.Fog = false;
            yield return Frames(2);
            Assert.That(level.Mood.Builds, Is.EqualTo(builds + 1), "built again");
            Assert.That(level.Mood.Rooms.All(r => r.Fog == null));
            level.Values.RoomMood = false;
            yield return Frames(2);
            Assert.That(level.Mood.Built, Is.False);
            Assert.That(MoodRoots(), Is.EqualTo(0));
            Assert.That(key.intensity, Is.EqualTo(intensity), "the master off while playing: today's light");
        }

        /// <summary>The names of the level's own objects, but what is under a prop (an ossuary's piles may stand elsewhere, or one fewer, with the master on).</summary>
        static List<string> Names(GeneratedLevel level) =>
            level.GetComponentsInChildren<Transform>(true).Where(t =>
            {
                for (Transform a = t; a != null && a != level.transform; a = a.parent)
                    if (a.name.StartsWith("Prop_", StringComparison.Ordinal)) return false;
                return true;
            }).Select(t => t.name).ToList();

        [UnityTest]
        public IEnumerator MasterOff_GivesExactlyTodaysObjects([Values(false, true)] bool passages)
        {
            Light key = Key();
            float intensity = key.intensity;
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            GeneratedLevel off = Build(5, v => v.RoomMood = false, passages: passages);
            yield return Frames(2);
            Assert.That(off.Mood, Is.Null);
            Assert.That(off.GetComponent<RoomMood>(), Is.Null);
            Assert.That(MoodRoots(), Is.EqualTo(0));
            Assert.That(key.intensity, Is.EqualTo(intensity));
            Assert.That(SameProbe(RenderSettings.ambientProbe, probe));
            Assert.That(off.Enemies.SelectMany(e => e.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.name == "Mood_EnemyFloor"), Is.False);
            List<string> namesOff = Names(off);
            int lightsOff = off.GetComponentsInChildren<Light>(true).Length;

            // The mood never adds to the level's own objects: the same names on, under the level.
            GeneratedLevel on = Build(5, passages: passages);
            yield return Frames(2);
            Assert.That(Names(on), Is.EqualTo(namesOff), "the level's own objects, the same with the master on");
            Assert.That(on.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(lightsOff));
        }

        // ---------------- Her light, the enemies, walking ----------------

        [UnityTest]
        public IEnumerator HerLight_GoesWithHer()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            Light light = level.Mood.HerLight;
            Assert.That(light, Is.Not.Null);
            foreach (LevelRoom room in level.Layout.Rooms.Take(3))
            {
                yield return Into(level, room);
                Vector3 her = _player.Position;
                Assert.That(light.enabled, Is.True);
                Assert.That(new Vector2(light.transform.position.x - her.x, light.transform.position.z - her.z).magnitude, Is.LessThan(0.01f), "over her in " + room.Letter + room.Order);
                Assert.That(light.transform.position.y - her.y, Is.EqualTo(level.Values.Mood.HerLightHeight).Within(0.01f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            }
        }

        [UnityTest]
        public IEnumerator TheEnemies_KeepAFloorOnTheirLight_AndTheMiddleIsNeverTheDarkest([Values(false, true)] bool passages)
        {
            GeneratedLevel level = Build(passages: passages);
            yield return Frames(2);
            Assert.That(level.Enemies.Count, Is.GreaterThan(0));
            int floored = 0;
            foreach (Enemy e in level.Enemies)
            {
                Renderer[] shown = e.GetComponentsInChildren<Renderer>(true).Where(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && r.sharedMaterial != null
                    && (r.sharedMaterial.name.StartsWith(EnemyLook.MaterialPrefix, StringComparison.Ordinal) || r.sharedMaterial.name.StartsWith(EnemyFactory.OwnMaterialPrefix, StringComparison.Ordinal))).ToArray();
                Assert.That(shown, Is.Not.Empty, e.name);
                foreach (Renderer r in shown)
                {
                    Material last = r.sharedMaterials[r.sharedMaterials.Length - 1];
                    Assert.That(last.name, Is.EqualTo("Mood_EnemyFloor"), e.name + " drawn with the light floor");
                    floored++;
                }
            }
            Assert.That(level.Mood.FlooredRenderers, Is.EqualTo(floored));
            foreach (RoomMood.RoomParts p in level.Mood.Rooms)
                Assert.That(p.VertexSource.Contains(-1), "a pool over the middle of room " + p.Order);
            // The practice room's enemy is untouched.
            foreach (Enemy e in Object.FindObjectsByType<Enemy>().Where(e => !level.Enemies.Contains(e)))
                Assert.That(e.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.name == "Mood_EnemyFloor"), Is.False, e.name);

            // Gone with the mood: the master off while playing takes the floor off them again.
            level.Values.RoomMood = false;
            yield return Frames(2);
            foreach (Enemy e in level.Enemies)
                Assert.That(e.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.name == "Mood_EnemyFloor"), Is.False, e.name);
        }

        /// <summary>A point a metre into a room from a door's middle, on the NavMesh.</summary>
        static Vector3 InsideDoor(GeneratedLevel level, LevelRoom room, LevelDoor d)
        {
            d.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            var mid = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            LevelPoint m = room.Middle(level.Layout.CellSize);
            double dx = m.X - mid.X, dy = m.Y - mid.Y, len = Math.Sqrt(dx * dx + dy * dy);
            Vector3 p = level.ToWorld(new LevelPoint(mid.X + (d.Vertical ? Math.Sign(dx) : 0), mid.Y + (d.Vertical ? 0 : Math.Sign(dy))));
            return NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position : p;
        }

        static Dictionary<string, (NavMeshPathStatus status, float length)> Paths(GeneratedLevel level)
        {
            var result = new Dictionary<string, (NavMeshPathStatus, float)>();
            var path = new NavMeshPath();
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if (room.Kind == RoomKind.Passage) continue; // work order 082: no mood in a passage; the way through one is PassagesPlayTests'
                List<LevelDoor> doors = level.Layout.DoorsOf(room.Order).ToList();
                for (int i = 0; i < doors.Count; i++)
                    for (int j = 0; j < doors.Count; j++)
                    {
                        if (i == j) continue;
                        // Each door of a room to each other, and a door to the next room's far door through it.
                        Vector3 from = InsideDoor(level, room, doors[i]), to = InsideDoor(level, room, doors[j]);
                        NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);
                        float length = 0f;
                        for (int k = 1; k < path.corners.Length; k++) length += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                        result[room.Order + ":" + i + "-" + j + (room.Theme == RoomTheme.Ossuary ? " ossuary" : "")] = (path.status, length);
                    }
            }
            return result;
        }

        [UnityTest]
        public IEnumerator NothingBlocksWalking_ThePathsFromDoorToDoor_AreAsTheyWere([Values(false, true)] bool passages)
        {
            int compared = 0;
            foreach (int floor in new[] { 1, 5 })
            {
                GeneratedLevel off = Build(floor, v => v.RoomMood = false, passages: passages);
                yield return Frames(2);
                Dictionary<string, (NavMeshPathStatus status, float length)> before = Paths(off);
                GeneratedLevel on = Build(floor, passages: passages);
                yield return Frames(2);
                Assert.That(on.Mood.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Dictionary<string, (NavMeshPathStatus status, float length)> after = Paths(on);
                Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
                foreach (KeyValuePair<string, (NavMeshPathStatus status, float length)> pair in before)
                {
                    (NavMeshPathStatus status, float length) now = after[pair.Key];
                    Assert.That(now.status, Is.EqualTo(pair.Value.status), "floor " + floor + " room:doors " + pair.Key);
                    Assert.That(now.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "a whole path, floor " + floor + " room:doors " + pair.Key);
                    // An ossuary's piles stand elsewhere with the master on (on purpose): its paths are whole, their lengths may differ;
                    // changed deliberately (work order 052): so a room's with a large prop that blocks walking.
                    int order = int.Parse(pair.Key.Substring(0, pair.Key.IndexOf(':')));
                    bool large = on.LargeProps.Any(p => p.Room.Order == order && !PropSize.Of(p.Prop.Model).Walkable);
                    if (!pair.Key.EndsWith("ossuary", StringComparison.Ordinal) && !large)
                        Assert.That(now.length, Is.EqualTo(pair.Value.length).Within(0.3f), "as long as it was, floor " + floor + " room:doors " + pair.Key);
                    compared++;
                }
            }
            Assert.That(compared, Is.GreaterThan(0));
        }

        // ---------------- Its frame ----------------

        [UnityTest]
        public IEnumerator ItsFrame_AllocatesNothing([Values(false, true)] bool passages)
        {
            GeneratedLevel level = Build(passages: passages);
            yield return Frames(2);
            LevelRoom combat = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat);
            yield return Into(level, combat);
            RoomMood mood = level.Mood;
            mood.Tick();
            mood.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) mood.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the mood's frame allocates nothing");
        }

        [UnityTest]
        public IEnumerator TheRoomTone_FollowsItsVolume_AndLoopsSevenSeconds()
        {
            GeneratedLevel level = Build();
            yield return Frames(2);
            AudioSource tone = level.Mood.Tone;
            Assert.That(tone.clip.length, Is.EqualTo(7f).Within(0.01f));
            float master = PlaceholderSounds.Current == null ? 1f : PlaceholderSounds.Current.Values.On ? PlaceholderSounds.Current.Values.Master : 0f;
            Assert.That(tone.volume, Is.EqualTo(level.Values.Mood.ToneVolume * master).Within(1e-4f));
            Assert.That(tone.spatialBlend, Is.EqualTo(0f));
            level.Values.Mood.ToneVolume = 0.02f;
            yield return Frames(2);
            Assert.That(tone.volume, Is.EqualTo(0.02f * master).Within(1e-4f), "turned while playing");
        }
    }
}
