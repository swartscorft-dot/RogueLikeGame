using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Light from the lamps (work order 085), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/> and
    /// <see cref="RunHud"/>, in the style room (the nave of the room lab, floor 1, seed KCT7B24U): "as before" leaves every light
    /// of the floor as it is on main, and nothing is added; the four lightings switched through in the middle of a run leave the
    /// colliders and the NavMesh as they were and build no floor; the pause menu's line under the greybox view, its words, the
    /// setting kept in PlayerPrefs (put back as it was after each test); with the greybox on the lamplit lights are off; a
    /// telegraph's and an arrow's materials are no dimmer at level C than as before; a shot's light comes and goes without
    /// allocating, a blast's brighter; nothing allocates in a frame while nothing changes.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames or real seconds, never a frame's length.
    /// </summary>
    public class LamplitPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        bool _hadPref;
        int _pref;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            // The setting as this machine keeps it, put back after the test: a test keeps nothing.
            _hadPref = PlayerPrefs.HasKey(GameSettings.LightKey);
            _pref = PlayerPrefs.GetInt(GameSettings.LightKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetLightStep(LightSteps.AsBefore, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.LightKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.LightKey);
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>The style room: the nave in the room lab, ordinary, the mix "floor 1", seed KCT7B24U; she in its passage.</summary>
        GeneratedLevel StyleRoom()
        {
            _levels.Values = new LevelValues();
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            GeneratedLevel level = _levels.StartLab(nave, 0, seed);
            Assert.That(level, Is.Not.Null, "the nave built");
            return level;
        }

        static Vector3 AtDepth(GeneratedLevel level, double depth)
        {
            LevelLayout l = level.Layout;
            LevelRoom room = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = room.Rect.Y * cs + depth;
            for (int k = 0; k < room.Rect.Width && l.RoomAt(x, y) != room; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            return level.ToWorld(new LevelPoint(x, y));
        }

        IEnumerator UseLight(int step)
        {
            GameSettings.SetLightStep(step, keep: false);
            yield return Frames(3);
            LamplitValues want = LightSteps.Of(step);
            if (want != null) Assert.That(_levels.Lamplit != null && _levels.Lamplit.On == want, Is.True, "the floor follows the setting: " + LightSteps.Names[step]);
            else Assert.That(_levels.Lamplit == null || _levels.Lamplit.On == null, Is.True, "as before: no lamplit light");
        }

        // ---------------- As before ----------------

        sealed class LightPrint
        {
            public string Name;
            public bool Enabled;
            public float Intensity, Range;
            public Color Colour;
            public Vector3 At;
        }

        static Dictionary<Light, LightPrint> Lights() =>
            Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(l => l, l => new LightPrint
            {
                Name = l.name, Enabled = l.isActiveAndEnabled, Intensity = l.intensity, Range = l.range, Colour = l.color, At = l.transform.position,
            });

        [UnityTest]
        public IEnumerator AsBefore_Chosen_AddsNothing_AndLeavesEveryLightAsOnMain_AlsoAfterALamplitLevelWasOnAndOff()
        {
            Assert.That(GameSettings.LightStep, Is.EqualTo(LightSteps.AsBefore), "chosen in the set-up (the default is lamplit C)");
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 2.0));
            yield return Frames(4);
            Assert.That(_levels.Lamplit, Is.Null, "as before: nothing is added to the floor");
            Assert.That(level.GetComponent<LamplitView>(), Is.Null);
            Assert.That(LamplitView.Active, Is.Null, "no shot lights anything");
            RoomMood mood = level.Mood;
            Assume.That(mood, Is.Not.Null, "the mood is on");
            Assert.That(mood.Lamplit, Is.Null);
            // The values of main 6adfc56: her light 1.4, 9 m, 3 m up; the scene's key light at 0.4 of its own.
            var main = new MoodValues();
            Assert.That(mood.HerLight.intensity, Is.EqualTo(main.HerLightIntensity));
            Assert.That(mood.HerLight.range, Is.EqualTo(main.HerLightRange));
            Assert.That(mood.HerLight.transform.position.y - _player.Position.y, Is.EqualTo(main.HerLightHeight).Within(1e-3));
            Light key = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
            Assume.That(key, Is.Not.Null);
            Dictionary<Light, LightPrint> before = Lights();
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            float keyBefore = key.intensity;

            // Lamplit B on: the light changes.
            yield return UseLight(2);
            LamplitView view = _levels.Lamplit;
            Assert.That(RenderSettings.ambientProbe[0, 0], Is.LessThan(probe[0, 0]), "the ambient went down to B's base");
            Assert.That(key.intensity, Is.LessThan(keyBefore), "and the key light");
            Assert.That(view.LampLightsOn, Is.GreaterThan(0), "the lamps are lit");
            Assert.That(mood.Rooms.SelectMany(p => p.Lights).Count(l => l.enabled), Is.EqualTo(0), "the mood's own few lights stand aside");

            // Back to as before: every light as it was.
            yield return UseLight(0);
            Assert.That(view.LampLightsOn, Is.EqualTo(0), "no lamp lit");
            Assert.That(view.FlashesLit, Is.EqualTo(0));
            Assert.That(view.SconceRenderers.Count(r => r.enabled), Is.EqualTo(0), "no sconce drawn");
            Assert.That(mood.Lamplit, Is.Null);
            Assert.That(LamplitView.Active, Is.Null);
            Assert.That(key.intensity, Is.EqualTo(keyBefore), "the key light exactly as before");
            for (int c = 0; c < 9; c++)
                for (int rgb = 0; rgb < 3; rgb++)
                    Assert.That(RenderSettings.ambientProbe[rgb, c], Is.EqualTo(probe[rgb, c]), "the ambient exactly as before");
            Dictionary<Light, LightPrint> after = Lights();
            var flicker = Mathf.Max(main.CandleFlicker, main.BrazierFlicker) * 1.01f;
            var moodLights = new HashSet<Light>(mood.Rooms.SelectMany(p => p.Lights));
            foreach (KeyValuePair<Light, LightPrint> pair in before)
            {
                Assert.That(after.ContainsKey(pair.Key), Is.True, pair.Value.Name + " is still there");
                LightPrint a = after[pair.Key], b = pair.Value;
                Assert.That(a.Enabled, Is.EqualTo(b.Enabled), b.Name + " on or off as before");
                Assert.That(a.Range, Is.EqualTo(b.Range), b.Name + "'s range");
                Assert.That(a.Colour, Is.EqualTo(b.Colour), b.Name + "'s colour");
                Assert.That((a.At - b.At).magnitude, Is.LessThan(1e-3f), b.Name + " where it was");
                if (moodLights.Contains(pair.Key) && b.Intensity > 0f) Assert.That(Mathf.Abs(a.Intensity / b.Intensity - 1f), Is.LessThanOrEqualTo(flicker * 2f), b.Name + "'s intensity within its flicker");
                else if (pair.Key.GetComponentInParent<RewardDrop>() == null) Assert.That(a.Intensity, Is.EqualTo(b.Intensity), b.Name + "'s intensity");
            }
            foreach (KeyValuePair<Light, LightPrint> pair in after)
                if (!before.ContainsKey(pair.Key)) Assert.That(pair.Value.Enabled, Is.False, pair.Value.Name + ": a light the lamplit view made, off");
        }

        // ---------------- Switching mid-run ----------------

        static string Colliders()
        {
            var lines = new List<string>();
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.GetComponentInParent<PlayerCharacter>() != null || c.GetComponentInParent<Enemy>() != null) continue; // they move
                Bounds b = c.bounds;
                lines.Add(string.Format(Inv, "{0}|{1}|{2}|{3}|{4:F3},{5:F3},{6:F3}|{7:F3},{8:F3},{9:F3}", c.name, c.GetType().Name, c.enabled, c.isTrigger,
                                        b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z));
            }
            lines.Sort(StringComparer.Ordinal);
            return lines.Count + "\n" + string.Join("\n", lines);
        }

        static string NavMeshPrint()
        {
            NavMeshTriangulation t = NavMesh.CalculateTriangulation();
            double sx = 0, sy = 0, sz = 0;
            foreach (Vector3 v in t.vertices)
            {
                sx += v.x;
                sy += v.y;
                sz += v.z;
            }
            long idx = 0;
            foreach (int i in t.indices) idx = idx * 31 + i;
            return string.Format(Inv, "{0} vertices, {1} indices, sums {2:F2} {3:F2} {4:F2}, index print {5}", t.vertices.Length, t.indices.Length, sx, sy, sz, idx);
        }

        [UnityTest]
        public IEnumerator SwitchingThroughTheFour_MidRun_BuildsNoFloor_AndLeavesTheCollidersAndTheNavMeshAsTheyWere()
        {
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 2.0));
            yield return Frames(3);
            string colliders = Colliders(), navMesh = NavMeshPrint();
            int builds = _levels.LabBuilds;
            foreach (int step in new[] { 1, 2, 3, 0, 3, 1, 0 })
            {
                yield return UseLight(step);
                Assert.That(_levels.Level, Is.SameAs(level), LightSteps.Names[step] + ": the same floor, not built again");
                Assert.That(_levels.LabBuilds, Is.EqualTo(builds), "no lab room built again");
                Assert.That(_levels.InRun, Is.True);
                Assert.That(Colliders(), Is.EqualTo(colliders), LightSteps.Names[step] + ": every collider as it was");
                Assert.That(NavMeshPrint(), Is.EqualTo(navMesh), LightSteps.Names[step] + ": the NavMesh as it was");
            }
            LamplitView view = _levels.Lamplit;
            Assert.That(view.Root.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing the lamplit view made has a collider");
            Assert.That(view.Plan.Lamps.Count(l => l.Kind == LampKind.Sconce), Is.GreaterThan(0), "the nave got sconces");
            TestContext.WriteLine("LAMPLIT " + view.Plan.Describe(level.Layout.Rooms[1], "the nave") + string.Format(Inv, "; {0} lamp lights made", view.LampLights));
        }

        [UnityTest]
        public IEnumerator ALevel_TheLampsOfHerRoomLit_HerOwnSmallLight_TheBaseDown_AndTheNextLevelDarker()
        {
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 3.0));
            yield return Frames(3);
            float lastAmbient = float.MaxValue;
            foreach (int step in new[] { 1, 2, 3 })
            {
                yield return UseLight(step);
                LamplitValues l = LightSteps.Of(step);
                LamplitView view = _levels.Lamplit;
                RoomMood mood = level.Mood;
                Assert.That(mood.HerLight.enabled, Is.True, l.Name + ": she has her light");
                Assert.That(mood.HerLight.intensity, Is.EqualTo(l.HerLightIntensity), l.Name + ": her light's");
                Assert.That(mood.HerLight.range, Is.EqualTo(l.HerLightRange));
                Assert.That(mood.HerLight.transform.position.z, Is.LessThan(_player.Position.z), "a little toward the camera");
                int nave = level.Layout.Rooms[1].Order;
                int lit = view.Plan.Lamps.Count(x => x.Room <= nave && x.CarriedBy == null); // the passage and the nave: both drawn while she is in the nave
                Assert.That(view.LampLightsOn, Is.EqualTo(lit), l.Name + ": every lamp of her room and the passage lit");
                float ambient = RenderSettings.ambientProbe[0, 0] + RenderSettings.ambientProbe[1, 0] + RenderSettings.ambientProbe[2, 0];
                Assert.That(ambient, Is.GreaterThan(0f), l.Name + ": never pitch black");
                Assert.That(ambient, Is.LessThan(lastAmbient), l.Name + " darker than the level before");
                lastAmbient = ambient;
            }
        }

        // ---------------- The menu's line ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusLine_UnderTheGreyboxView_StepsTheFour_TheFloorFollowsAtOnce_AndTheSettingIsKept()
        {
            _hud = RunHud.Ensure(); // it loads the settings: this machine's, put back after the test
            yield return null;
            GameSettings.SetLightStep(LightSteps.AsBefore); // kept: as before
            GameSettings.SetGreybox(false, keep: false); // whatever this machine keeps for the greybox (it wins over the light): off for the test
            GeneratedLevel level = StyleRoom();
            yield return Frames(2);
            Assert.That(_levels.Pause(), Is.True);
            yield return null;
            List<RunHud.MenuLine> lines = _hud.MenuLines.ToList();
            Assert.That(lines.IndexOf(RunHud.MenuLine.Light), Is.EqualTo(lines.IndexOf(RunHud.MenuLine.Greybox) + 1), "under the greybox view, beside the camera's lines");
            Assert.That(lines.IndexOf(RunHud.MenuLine.Greybox), Is.EqualTo(lines.IndexOf(RunHud.MenuLine.CameraDistance) + 1), "the camera's lines as they were");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Light), Is.EqualTo("Light:  as before"), "as before, as kept above");
            var seen = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Light), Is.True);
                seen.Add(_hud.MenuText(RunHud.MenuLine.Light));
            }
            Assert.That(seen, Is.EqualTo(new[] { "Light:  lamplit A", "Light:  lamplit B", "Light:  lamplit C", "Light:  as before" }), "Enter steps on, round");
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Light, -1), Is.True);
            Assert.That(_hud.MenuText(RunHud.MenuLine.Light), Is.EqualTo("Light:  lamplit C"), "the left arrow steps back");
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Light, -1), Is.True);
            Assert.That(_hud.MenuText(RunHud.MenuLine.Light), Is.EqualTo("Light:  lamplit B"));
            Assert.That(() => { _hud.MenuText(RunHud.MenuLine.Light); }, AllocExt.AllocatingGCMemory(Is.Not), "the line's words are made once");
            yield return Frames(2);
            Assert.That(_levels.Lamplit != null && _levels.Lamplit.On == LightSteps.Of(2), Is.True, "the floor follows it under the pause menu, at once");
            Assert.That(PlayerPrefs.GetInt(GameSettings.LightKey, 0), Is.EqualTo(2), "kept in PlayerPrefs, as the camera's lines");
            _levels.Resume();
            yield return Frames(2);
            Assert.That(_levels.Level, Is.SameAs(level), "the run goes on, on the same floor");
            GameSettings.Unload();
            Assert.That(GameSettings.LightStep, Is.EqualTo(LightSteps.Default), "unloaded: the default, lamplit C, as for a test that never made the HUD");
            GameSettings.Load();
            Assert.That(GameSettings.LightStep, Is.EqualTo(2), "loaded again: lamplit B, as it was kept");
            PlayerPrefs.SetInt(GameSettings.LightKey, 17);
            GameSettings.Load();
            Assert.That(GameSettings.LightStep, Is.EqualTo(LightSteps.Default), "a stored value that is no step is the default");
            PlayerPrefs.DeleteKey(GameSettings.LightKey);
            GameSettings.Load();
            Assert.That(GameSettings.LightStep, Is.EqualTo(LightSteps.Default), "nothing stored: lamplit C (Kristian: C er best)");
        }

        // ---------------- The greybox wins ----------------

        [UnityTest]
        public IEnumerator WithTheGreyboxOn_TheLamplitLightIsOff_AndOffAgain_ItComesBack()
        {
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 2.0));
            yield return UseLight(3);
            LamplitView view = _levels.Lamplit;
            Assert.That(view.LampLightsOn, Is.GreaterThan(0));
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(3);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True);
            Assert.That(view.On, Is.Null, "the greybox wins: no lamplit level");
            Assert.That(view.LampLightsOn, Is.EqualTo(0), "no lamp lit");
            Assert.That(view.SconceRenderers.Count(r => r.enabled), Is.EqualTo(0), "no sconce drawn");
            Assert.That(LamplitView.Active, Is.Null, "no shot lights anything");
            Assert.That(level.Mood.Lamplit, Is.Null, "the mood as before (and off, under the greybox)");
            Assert.That(RoomMood.SceneLightTaken, Is.False, "the scene's light given back: the greybox's even light");
            LamplitView.ShotWentOff(ShotFxKind.Flash, _player.Position + Vector3.up, Vector3.forward, 0.6f, Color.white);
            Assert.That(view.FlashesLit, Is.EqualTo(0), "a shot under the greybox lights nothing");
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(3);
            Assert.That(view.On, Is.SameAs(LightSteps.Of(3)), "the greybox off: lamplit C again");
            Assert.That(view.LampLightsOn, Is.GreaterThan(0));
            Assert.That(level.Mood.Lamplit, Is.SameAs(LightSteps.Of(3)));
        }

        // ---------------- Reading comes first ----------------

        [UnityTest]
        public IEnumerator ATelegraphsAndAnArrowsMaterials_AreNoDimmerAtLevelC_ThanAsBefore_WhereverTheyAre()
        {
            ThingsThatFly.Ensure();
            GeneratedLevel level = StyleRoom();
            yield return Frames(3);
            var telegraphs = level.Enemies.Where(e => e != null).SelectMany(e => e.GetComponentsInChildren<LineRenderer>(true)).Where(r => r.sharedMaterial != null).ToList();
            Assume.That(telegraphs.Count, Is.GreaterThan(0), "the nave's enemies have their telegraphs");
            Material arrow = _player.ShotFxMaterial;
            Assume.That(arrow, Is.Not.Null, "the arrows' and orbs' material (ThingsThatFly draws them with hers)");
            Color Of(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            var before = telegraphs.ToDictionary(r => r, r => Of(r.sharedMaterial));
            Color arrowBefore = Of(arrow);
            yield return UseLight(3);
            foreach (LineRenderer r in telegraphs)
            {
                Assert.That(r.sharedMaterial.shader.name, Does.Contain("Unlit"), r.name + ": a telegraph is unlit: no light in the room dims it, wherever it is");
                Color c = Of(r.sharedMaterial);
                Assert.That(c.maxColorComponent, Is.GreaterThanOrEqualTo(before[r].maxColorComponent), r.name + ": no dimmer");
                Assert.That(c, Is.EqualTo(before[r]), r.name + ": its colour as before");
            }
            Assert.That(arrow.shader.name, Does.Contain("Unlit"), "an arrow or an orb is unlit");
            Assert.That(Of(arrow), Is.EqualTo(arrowBefore), "its colour as before");
            Assert.That(level.Mood.FlooredRenderers, Is.GreaterThan(0), "every enemy drawn with the light floor");
        }

        // ---------------- The default, and the fog ----------------

        [UnityTest]
        public IEnumerator WithNothingChosen_AFloorIsLamplitC_TheGreyboxStillWins()
        {
            GameSettings.Unload(); // as for a player with nothing stored (and a test that never made the HUD)
            Assert.That(GameSettings.LightStep, Is.EqualTo(LightSteps.Default));
            GeneratedLevel level = StyleRoom();
            yield return Frames(3);
            Assert.That(_levels.Lamplit != null && _levels.Lamplit.On == LightSteps.Of(3), Is.True, "lamplit C, Kristian's choice");
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(3);
            Assert.That(_levels.Lamplit.On, Is.Null, "the greybox wins over the default too");
        }

        [UnityTest]
        public IEnumerator TheFog_AtLamplitC_IsSeeThrough_NothingRoundHer_TheRestAtCsShare_AndAsBeforeItIsAsItWas()
        {
            GeneratedLevel level = StyleRoom();
            LevelRoom nave = level.Layout.Rooms[1];
            _player.Teleport(AtDepth(level, 3.0));
            yield return UseLight(0);
            RoomMood.RoomParts parts = level.Mood.Of(nave.Order);
            Assume.That(parts != null && parts.Fog != null, "the nave has its fog");
            yield return new WaitForSecondsRealtime(0.3f);
            var buffer = new ParticleSystem.Particle[level.Values.Mood.FogMostPerRoom];
            int n = parts.Fog.GetParticles(buffer);
            Assume.That(n, Is.GreaterThan(0), "fog alive in the nave (prewarmed)");
            MoodValues mv = level.Values.Mood;
            Color32 fog = new Color(mv.FogColour.R * mv.FogLevel, mv.FogColour.G * mv.FogLevel, mv.FogColour.B * mv.FogLevel, 1f);
            for (int i = 0; i < n; i++) Assert.That(buffer[i].startColor, Is.EqualTo(fog), "as before: every fog particle the fog's own grey");

            yield return UseLight(3);
            LamplitValues c = LightSteps.Of(3);
            Assert.That(c.ChangesFog, Is.True);
            n = parts.Fog.GetParticles(buffer);
            int cleared = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = buffer[i].position, her = _player.Position;
                float d = new Vector2(at.x - her.x, at.z - her.z).magnitude;
                Color32 got = buffer[i].startColor;
                Assert.That(got.b, Is.LessThanOrEqualTo((byte)Mathf.CeilToInt(fog.b * c.FogLevel) + 1), "never more than C's share of the grey");
                if (d < c.FogClearNear - 0.2f) // she moves a little between the frames: a margin
                {
                    Assert.That(got.r + got.g + got.b, Is.LessThanOrEqualTo(3), "nothing added round her: she and the floor under her read through it");
                    cleared++;
                }
            }
            TestContext.WriteLine(string.Format(Inv, "LAMPLIT fog: {0} particles in the nave, {1} round her cleared", n, cleared));
            Assert.That(() => { for (int i = 0; i < 100; i++) level.Mood.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the fog's frame allocates nothing");

            yield return UseLight(0);
            n = parts.Fog.GetParticles(buffer);
            for (int i = 0; i < n; i++) Assert.That(buffer[i].startColor, Is.EqualTo(fog), "back to as before: every particle the fog's own grey again");
        }

        // ---------------- Her shots ----------------

        [UnityTest]
        public IEnumerator AShotsLight_ComesAndGoes_WithoutAllocating_ABlastBrighterThanAShot_AndARealShotLightsTheRoom()
        {
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 3.0));
            yield return UseLight(2);
            LamplitView view = _levels.Lamplit;
            Assert.That(view.FlashesMade, Is.EqualTo(LamplitView.Lamps.MostFlashes), "the pool made with the lamps, before any shot");
            Assert.That(LamplitView.TakesTheMuzzle, Is.True, "the shot's weight's own muzzle light stands aside");
            Vector3 muzzle = _player.Position + Vector3.up * 1.2f;
            LamplitView.ShotWentOff(ShotFxKind.Flash, muzzle, Vector3.forward, 0.6f, Color.white); // once, so the call itself is warm
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(view.FlashesLit, Is.EqualTo(0));
            Assert.That(() => LamplitView.ShotWentOff(ShotFxKind.Flash, muzzle, Vector3.forward, 0.6f, Color.white), AllocExt.AllocatingGCMemory(Is.Not), "a shot allocates nothing");
            Assert.That(view.FlashesLit, Is.EqualTo(1), "its light is lit");
            float shot = view.BrightestFlash;
            Assert.That(shot, Is.GreaterThan(0f));
            yield return new WaitForSecondsRealtime(LamplitView.Lamps.MuzzleSeconds + 0.1f);
            yield return null;
            Assert.That(view.FlashesLit, Is.EqualTo(0), "and gone after its seconds");
            Assert.That(() => LamplitView.ShotWentOff(ShotFxKind.Ring, muzzle, Vector3.zero, 3f, Color.white), AllocExt.AllocatingGCMemory(Is.Not), "a blast allocates nothing");
            Assert.That(view.BrightestFlash, Is.GreaterThan(shot), "a blast lights more than a single shot");
            yield return new WaitForSecondsRealtime(LamplitView.Lamps.BlastSeconds + 0.1f);
            yield return null;
            Assert.That(view.FlashesLit, Is.EqualTo(0));
            for (int i = 0; i < 3 * LamplitView.Lamps.MostFlashes; i++)
                LamplitView.ShotWentOff(ShotFxKind.Spark, muzzle + Vector3.right * (2f * i), Vector3.zero, 0.8f, Color.cyan); // 2 m apart: none shares a light
            Assert.That(view.FlashesMade, Is.EqualTo(LamplitView.Lamps.MostFlashes), "many at once: never more lights than the pool");

            // A real shot of hers, at the nearest enemy: its muzzle lights, and its hit.
            yield return new WaitForSecondsRealtime(0.3f);
            Enemy target = level.Enemies.Where(e => e != null).OrderBy(e => (e.transform.position - _player.Position).sqrMagnitude).First();
            int asked = view.FlashesAsked;
            bool fired = false;
            Action<PlayerReport> heard = r => { if (r.Kind == PlayerReportKind.ShotFired) fired = true; };
            _player.Reported += heard;
            _player.Shoot(target.transform.position);
            float until = Time.realtimeSinceStartup + 3f;
            while (!fired && Time.realtimeSinceStartup < until) yield return null;
            _player.Reported -= heard;
            Assert.That(fired, Is.True, "the shot went off");
            Assert.That(view.FlashesAsked, Is.GreaterThan(asked), "and lit the room at its muzzle");
        }

        [UnityTest]
        public IEnumerator AFrame_AllocatesNothing_WhileNothingChanges_LamplitAndAsBefore()
        {
            GeneratedLevel level = StyleRoom();
            _player.Teleport(AtDepth(level, 3.0));
            yield return UseLight(2);
            LamplitView view = _levels.Lamplit;
            for (int i = 0; i < 10; i++)
            {
                _levels.FollowLamplit();
                view.Tick();
                level.Mood.Tick();
            }
            Assert.That(() => { for (int i = 0; i < 300; i++) { _levels.FollowLamplit(); view.Tick(); level.Mood.Tick(); } }, AllocExt.AllocatingGCMemory(Is.Not), "lamplit B: a frame allocates nothing");
            yield return UseLight(0);
            for (int i = 0; i < 10; i++) _levels.FollowLamplit();
            Assert.That(() => { for (int i = 0; i < 300; i++) { _levels.FollowLamplit(); view.Tick(); level.Mood.Tick(); } }, AllocExt.AllocatingGCMemory(Is.Not), "as before: nothing");
        }
    }
}
