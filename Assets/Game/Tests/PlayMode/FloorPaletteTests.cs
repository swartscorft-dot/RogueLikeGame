using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// A palette a floor (work order 105), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>, on the
    /// five floors of work order 091's run seed (226K2223) at lamplit C (the default): the switch off, every floor as before
    /// (nothing added, the kit's stone, the lamps, the scene's light and the fog by today's values), and floor 1 with it on the
    /// same, value for value; floors 2 to 5 with it on, the stone, the warm lamps, the base light and the fog by the floor's row,
    /// the teal lamps and the lanterns as before, the row read once a floor and nothing a frame; the greybox view as before over
    /// a floor with a palette; the room lab shows the palette of its mix's floor; nothing allocates in a frame.
    /// </summary>
    public class FloorPaletteTests
    {
        const ulong RunSeed = 0x09100001UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        bool _onWas;
        Light _key;
        Color _keyColour;
        float _keyIntensity;
        SphericalHarmonicsL2 _probe;
        readonly List<Material> _made = new List<Material>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _onWas = FloorPaletteView.Values.On;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
            GameSettings.SetLightStep(LightSteps.Default, keep: false);
            // The scene's light before any floor takes it down (as RoomMood finds its key).
            _key = RenderSettings.sun;
            if (_key == null)
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.isActiveAndEnabled && (_key == null || l.intensity > _key.intensity)) _key = l;
            Assert.That(_key, Is.Not.Null, "the scene's key light");
            _keyColour = _key.color;
            _keyIntensity = _key.intensity;
            _probe = RenderSettings.ambientProbe;
        }

        [TearDown]
        public void CleanUp()
        {
            FloorPaletteView.Values.On = _onWas;
            foreach (Material m in _made) if (m != null) Object.Destroy(m);
            _made.Clear();
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                _player.GetComponent<PlayerHealth>().Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>The run's floor built, lamplit C on, the mood and the lamps looked at.</summary>
        IEnumerator Floor(int floor, Action<GeneratedLevel> done)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(RunSeed, floor), floor);
            Assert.That(level, Is.Not.Null, "floor " + floor);
            yield return null;
            Follow(level);
            yield return null;
            Follow(level);
            done(level);
        }

        void Follow(GeneratedLevel level)
        {
            _levels.FollowGreybox();
            _levels.FollowLamplit();
            if (level.Mood != null && level.Mood.isActiveAndEnabled) level.Mood.Tick();
            if (_levels.Lamplit != null) _levels.Lamplit.Tick();
        }

        static Color BaseOf(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static Color Of(Rgb c) => new Color(c.R, c.G, c.B, 1f);
        static string F(Color c) => string.Format(Inv, "({0:R}, {1:R}, {2:R}, {3:R})", c.r, c.g, c.b, c.a);

        /// <summary>Each stone material of the floor once, with what it is: a kit piece's (by its kind), a floor's (by its room), a stacked stone's.</summary>
        static List<(string what, CryptPiece? piece, LevelRoom room, Material m)> Stone(GeneratedLevel level)
        {
            var list = new List<(string, CryptPiece?, LevelRoom, Material)>();
            var seen = new HashSet<Material>();
            foreach (GeneratedLevel.KitPiece p in level.KitPieces)
                foreach (Renderer r in p.Object.GetComponentsInChildren<Renderer>(true))
                    if (seen.Add(r.sharedMaterial)) list.Add((p.Kind.ToString(), p.Kind, null, r.sharedMaterial));
            for (int i = 0; i < level.FloorBoxes.Count; i++)
            {
                Material m = level.FloorBoxes[i].GetComponent<Renderer>().sharedMaterial;
                if (seen.Add(m)) list.Add(("floor of " + level.FloorRoomOf(i).Kind + (level.FloorRoomOf(i).Elite ? " elite" : ""), null, level.FloorRoomOf(i), m));
            }
            if (level.Cave != null)
                foreach (GameObject go in level.Cave.Stacked)
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                        if (seen.Add(r.sharedMaterial)) list.Add(("stacked " + r.sharedMaterial.name, null, null, r.sharedMaterial));
            return list;
        }

        /// <summary>The lamplit lamps' lights in the plan's order (the carried ones have none), each with its lamp.</summary>
        List<(Lamp lamp, Light light)> LampLights()
        {
            LamplitView view = _levels.Lamplit;
            Assert.That(view != null && view.On != null && view.Plan != null, "lamplit C on, its lamps made");
            List<Light> lights = view.Root.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("Lamp_", StringComparison.Ordinal)).ToList();
            List<Lamp> own = view.Plan.Lamps.Where(l => l.CarriedBy == null).ToList();
            Assert.That(lights.Count, Is.EqualTo(own.Count), "a light a lamp with a light of its own");
            return own.Zip(lights, (l, x) => (l, x)).ToList();
        }

        /// <summary>Everything the palette may touch, as text: the stone's colours, the lamps' colours and ranges, the scene's light, the fog's colour.</summary>
        string Look(GeneratedLevel level)
        {
            var sb = new StringBuilder();
            foreach ((string what, CryptPiece? kind, LevelRoom of, Material m) in Stone(level)) sb.Append(what).Append(' ').Append(m.name).Append(' ').Append(F(BaseOf(m))).Append('\n');
            foreach ((Lamp lamp, Light light) in LampLights()) sb.Append(light.name).Append(' ').Append(F(light.color)).Append(' ').Append(light.range.ToString("R", Inv)).Append('\n');
            sb.Append("key ").Append(F(_key.color)).Append(' ').Append(_key.intensity.ToString("R", Inv)).Append('\n');
            SphericalHarmonicsL2 p = RenderSettings.ambientProbe;
            for (int rgb = 0; rgb < 3; rgb++)
                for (int c = 0; c < 9; c++) sb.Append(p[rgb, c].ToString("R", Inv)).Append(' ');
            sb.Append('\n');
            foreach (RoomMood.RoomParts parts in level.Mood.Rooms)
                if (parts.Fog != null) sb.Append("fog ").Append(parts.Order).Append(' ').Append(F(parts.Fog.main.startColor.color)).Append('\n');
            return sb.ToString();
        }

        Material Fresh(CryptPiece? piece, LevelRoom room, LevelValues values)
        {
            Material m = piece.HasValue ? CryptKit.MakeMaterial(piece.Value, values.Crypt) : CryptKit.MakeFloorMaterial(room.Kind, values.Crypt, room.Elite);
            _made.Add(m);
            return m;
        }

        /// <summary>The scene's light and the fog as the mood makes them from a base colour, a base level share and the fog's colour (at lamplit C).</summary>
        void AssertSceneLightAndFog(GeneratedLevel level, Rgb dark, float baseLevel, Color fog, string where)
        {
            LamplitValues c = LightSteps.Of(LightSteps.Default);
            Assert.That(_key.intensity, Is.EqualTo(_keyIntensity * c.KeyLevel * baseLevel).Within(1e-4f), where + ": the key light's level");
            Assert.That(Diff(_key.color, new Color(_keyColour.r * dark.R, _keyColour.g * dark.G, _keyColour.b * dark.B, 1f)), Is.LessThan(1e-4f), where + ": the key light's colour " + _key.color);
            SphericalHarmonicsL2 p = RenderSettings.ambientProbe;
            float a = c.AmbientLevel * baseLevel;
            Assert.That(p[0, 0], Is.EqualTo(_probe[0, 0] * a * dark.R).Within(1e-4f), where + ": the ambient's red");
            Assert.That(p[1, 0], Is.EqualTo(_probe[1, 0] * a * dark.G).Within(1e-4f), where + ": the ambient's green");
            Assert.That(p[2, 0], Is.EqualTo(_probe[2, 0] * a * dark.B).Within(1e-4f), where + ": the ambient's blue");
            int fogs = 0;
            foreach (RoomMood.RoomParts parts in level.Mood.Rooms)
                if (parts.Fog != null)
                {
                    fogs++;
                    Assert.That(Diff(parts.Fog.main.startColor.color, fog), Is.LessThan(1e-5f), where + ": the fog's colour in " + parts.Order);
                }
            Assert.That(fogs, Is.GreaterThan(0), where + ": the floor has fog");
        }

        // ---------------- Off, and floor 1 ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator TheSwitchOff_EveryFloorAsBefore_AndFloorOneWithItOn_ValueForValue()
        {
            MoodValues mood = new LevelValues().Mood;
            LampValues lamps = LamplitView.Lamps;
            FloorPaletteView.Values.On = false;
            string floorOneOff = null;
            GeneratedLevel level = null;
            for (int f = 1; f <= 5; f++)
            {
                yield return Floor(f, l => level = l);
                string where = "off, floor " + f;
                Assert.That(level.GetComponent<FloorPaletteView>(), Is.Null, where + ": nothing added");
                Assert.That(level.Mood.HasPalette, Is.False, where + ": the mood has none");
                foreach ((string what, CryptPiece? piece, LevelRoom room, Material m) in Stone(level))
                {
                    if (what.StartsWith("stacked", StringComparison.Ordinal))
                    {
                        Color want = (m.name.EndsWith("Pier", StringComparison.Ordinal) ? Of(level.Values.Crypt.PaleStoneTint) : Color.white) * level.Values.CaveRules.UpperTint;
                        Assert.That(Diff(BaseOf(m), want), Is.LessThan(1e-4f), where + ": " + what + " as the cave makes it");
                        continue;
                    }
                    Assert.That(BaseOf(m), Is.EqualTo(BaseOf(Fresh(piece, room, level.Values))), where + ": " + what + " as the kit makes it");
                }
                foreach ((Lamp lamp, Light light) in LampLights())
                    Assert.That(Diff(light.color, Of(lamps.ColourOf(lamp))), Is.LessThan(1e-6f), where + ": " + light.name + " by its kind's colour");
                AssertSceneLightAndFog(level, mood.DarkTint, 1f, new Color(mood.FogColour.R * mood.FogLevel, mood.FogColour.G * mood.FogLevel, mood.FogColour.B * mood.FogLevel, 1f), where);
                if (f == 1) floorOneOff = Look(level);
            }

            FloorPaletteView.Values.On = true;
            yield return Floor(1, l => level = l);
            Assert.That(level.GetComponent<FloorPaletteView>(), Is.Null, "on, floor 1: nothing added (its palette is today's)");
            Assert.That(level.Mood.HasPalette, Is.False);
            Assert.That(Look(level), Is.EqualTo(floorOneOff), "on, floor 1: the stone, the lamps, the scene's light and the fog value for value as with the switch off");
        }

        // ---------------- Floors 2 to 5 ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator OnFloorsTwoToFive_TheStoneTheWarmLampsTheBaseLightAndTheFog_FollowTheFloorsRow_ReadOnceAFloor()
        {
            FloorPaletteValues table = FloorPaletteView.Values;
            table.On = true;
            MoodValues mood = new LevelValues().Mood;
            LampValues lamps = LamplitView.Lamps;
            FloorPalette[] rows = table.Rows;
            GeneratedLevel level = null;
            for (int f = 2; f <= 5; f++)
            {
                string where = "floor " + f;
                int reads = table.Reads;
                yield return Floor(f, l => level = l);
                Assert.That(table.Reads, Is.EqualTo(reads + 1), where + ": its row read once, when it was built");
                FloorPaletteView view = level.GetComponent<FloorPaletteView>();
                Assert.That(view, Is.Not.Null, where + ": the palette on");
                FloorPalette p = rows[f - 1];
                Assert.That(view.Palette, Is.SameAs(p), where + ": its own row");
                Rgb tint = FloorPaletteRules.StoneTint(p, lamps, mood);
                Assert.That(view.StoneTint, Is.EqualTo(tint));
                int stones = 0;
                foreach ((string what, CryptPiece? piece, LevelRoom room, Material m) in Stone(level))
                {
                    if (what.StartsWith("stacked", StringComparison.Ordinal))
                    {
                        Color upper = (m.name.EndsWith("Pier", StringComparison.Ordinal) ? Of(level.Values.Crypt.PaleStoneTint) : Color.white) * level.Values.CaveRules.UpperTint;
                        Assert.That(Diff(BaseOf(m), new Color(upper.r * tint.R, upper.g * tint.G, upper.b * tint.B, 1f)), Is.LessThan(1e-3f), where + ": " + what + " tinted");
                        stones++;
                        continue;
                    }
                    Color fresh = BaseOf(Fresh(piece, room, level.Values));
                    if (piece == CryptPiece.Lantern)
                    {
                        Assert.That(BaseOf(m), Is.EqualTo(fresh), where + ": the lantern not touched");
                        continue;
                    }
                    Assert.That(Diff(BaseOf(m), new Color(fresh.r * tint.R, fresh.g * tint.G, fresh.b * tint.B, fresh.a)), Is.LessThan(1e-3f), where + ": " + what + " tinted: " + BaseOf(m));
                    stones++;
                }
                Assert.That(stones, Is.GreaterThan(5), where + ": walls, piers, arches and floors tinted");
                int warm = 0, teal = 0;
                foreach ((Lamp lamp, Light light) in LampLights())
                {
                    Rgb want = FloorPaletteRules.LampColour(p, lamp, lamps);
                    Assert.That(Diff(light.color, Of(want)), Is.LessThan(1e-5f), where + ": " + light.name);
                    if (FloorPaletteRules.TakesLampTint(lamp)) warm++;
                    else
                    {
                        teal++;
                        Assert.That(Diff(light.color, Of(lamps.Teal)), Is.LessThan(1e-6f), where + ": " + light.name + " keeps the game's cyan");
                    }
                }
                Assert.That(warm, Is.GreaterThan(0), where + ": warm lamps");
                Assert.That(teal, Is.GreaterThan(0), where + ": teal lamps");
                Rgb fog = FloorPaletteRules.FogColour(p, mood);
                AssertSceneLightAndFog(level, FloorPaletteRules.Times(mood.DarkTint, p.Base), p.BaseLevel, Of(fog), where);

                yield return Frames(30);
                Assert.That(table.Reads, Is.EqualTo(reads + 1), where + ": nothing of it read in a frame");
            }
            Assert.That(typeof(FloorPaletteView).GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null, "the palette's view has no frame");
            Assert.That(typeof(FloorPaletteView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null);

            // A frame of the mood and the lamps on a floor with a palette allocates nothing.
            for (int i = 0; i < 10; i++) { level.Mood.Tick(); _levels.Lamplit.Tick(); }
            Assert.That(() => { for (int i = 0; i < 300; i++) { level.Mood.Tick(); _levels.Lamplit.Tick(); } }, AllocExt.AllocatingGCMemory(Is.Not), "a frame allocates nothing");
        }

        // ---------------- The greybox view, the room lab ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator TheGreyboxView_IsAsBefore_OverAFloorWithAPalette_AndOffAgainThePaletteIsBack()
        {
            FloorPaletteView.Values.On = true;
            GameSettings.SetGreybox(true, keep: false);
            GeneratedLevel level = null;
            string Grey(GeneratedLevel l)
            {
                var sb = new StringBuilder();
                foreach (Material m in l.FloorBoxes.Select(b => b.GetComponent<Renderer>().sharedMaterial).Concat(l.WallBoxes.Select(b => b.GetComponent<Renderer>().sharedMaterial)).Distinct())
                    sb.Append(m.name).Append(' ').Append(F(BaseOf(m))).Append('\n');
                sb.Append("key ").Append(F(_key.color)).Append(' ').Append(_key.intensity.ToString("R", Inv)).Append('\n');
                for (int rgb = 0; rgb < 3; rgb++) sb.Append(RenderSettings.ambientProbe[rgb, 0].ToString("R", Inv)).Append(' ');
                sb.Append(_levels.Lamplit == null || _levels.Lamplit.On == null ? "no lamplit" : "LAMPLIT ON");
                return sb.ToString();
            }
            yield return Floor(1, l => level = l);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True, "the greybox view on");
            string one = Grey(level);
            yield return Floor(3, l => level = l);
            Assert.That(level.GetComponent<FloorPaletteView>(), Is.Not.Null, "floor 3 has its palette");
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True);
            Assert.That(Grey(level), Is.EqualTo(one), "the greybox view draws floor 3 as floor 1: its own materials, its even light, no lamps");
            Assert.That(_key.intensity, Is.EqualTo(_keyIntensity), "the scene's light as it is in the practice room");

            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(2);
            Follow(level);
            Rgb tint = level.GetComponent<FloorPaletteView>().StoneTint;
            Material floor = level.FloorBoxes[0].GetComponent<Renderer>().sharedMaterial;
            Color fresh = BaseOf(Fresh(null, level.FloorRoomOf(0), level.Values));
            Assert.That(Diff(BaseOf(floor), new Color(fresh.r * tint.R, fresh.g * tint.G, fresh.b * tint.B, 1f)), Is.LessThan(1e-3f), "off again: the floor's stone tinted, as before the greybox view");
            Assert.That(level.Mood.HasPalette && level.Mood.isActiveAndEnabled, Is.True, "the mood back, with its palette");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheRoomLab_ShowsThePaletteOfItsMixsFloor()
        {
            FloorPaletteView.Values.On = true;
            _levels.Values = new LevelValues();
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse("KCT7B24U", out ulong seed), Is.True);
            for (int mix = 0; mix < RoomLab.Mixes.Length; mix++)
            {
                GeneratedLevel level = _levels.StartLab(nave, mix, seed);
                Assert.That(level, Is.Not.Null);
                yield return null;
                int floor = RoomLab.Mixes[mix].Floor;
                FloorPaletteView view = level.GetComponent<FloorPaletteView>();
                if (floor == 1) Assert.That(view, Is.Null, RoomLab.Mixes[mix].Name + ": as today");
                else Assert.That(view != null && view.Palette == FloorPaletteView.Values.Rows[floor - 1], Is.True, RoomLab.Mixes[mix].Name + ": its floor's palette");
            }
        }
    }
}
