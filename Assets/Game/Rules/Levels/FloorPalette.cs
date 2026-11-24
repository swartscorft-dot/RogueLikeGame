using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// One floor's palette (work order 105; Kristian, 2026-11-24, ticked "Utseende og stemning" ("etasjene ser forskjellige ut")
    /// and, asked by name, said yes to the producer's "Paletter per etasje": "Steinfarge, lampevarme og tåke endres per etasje,
    /// kaldere og dypere nedover. Ingen ny grafikk."). Every value is a multiplier onto what the floor has today, so a palette of
    /// ones is today exactly: the stone's tint onto the crypt kit's walls, piers, arches and floors (their own textures and tints
    /// kept under it), the lamps' tint onto every warm lamp's light (the candles, the sconces, the chests' lights, the vault's
    /// gold, the entrance's warm braziers; the teal braziers and lanterns keep the game's cyan), the base light's colour and level
    /// onto the mood's (<see cref="MoodValues.DarkTint"/>, and the ambient and key levels of the light step in use), the fog's
    /// colour and share onto the mood's fog (<see cref="MoodValues.FogColour"/> and its brightness). Colours as picked, plain
    /// numbers, no engine.
    /// </summary>
    [Serializable]
    public sealed class FloorPalette
    {
        public string Name;
        public Rgb Stone = FloorPaletteRules.One;   // times the kit's stone; lifted by the least that keeps every light's reflection off it at least as bright as today (FloorPaletteRules.StoneLift)
        public Rgb Lamps = FloorPaletteRules.One;   // times every warm lamp's light colour
        public Rgb Base = FloorPaletteRules.One;    // times the base light's colour (MoodValues.DarkTint)
        public float BaseLevel = 1f;                // times the base light's level (the ambient and key light kept)
        public Rgb Fog = FloorPaletteRules.One;     // times the fog's grey (MoodValues.FogColour)
        public float FogShare = 1f;                 // times the fog's brightness: more is a thicker fog

        /// <summary>All ones: the floor as today, and nothing of it is touched.</summary>
        public bool IsToday =>
            FloorPaletteRules.IsOne(Stone) && FloorPaletteRules.IsOne(Lamps) && FloorPaletteRules.IsOne(Base) && FloorPaletteRules.IsOne(Fog)
            && BaseLevel == 1f && FogShare == 1f;
    }

    /// <summary>
    /// The act's five palettes, one a floor, as one table (work order 105), and the order's switch. Floor 1 is today exactly; each
    /// floor down is a step colder and deeper, from the producer's start: the stone from warm sandstone-grey to blue-grey slate,
    /// the lamps warm still but a little paler and fewer degrees of orange, the base light from neutral to cold blue, the fog a
    /// little thicker and bluer. Floor 5, the boss's, is the row's last step, the coldest, not a palette apart. First guesses
    /// judged from the pictures in captures\105; he tunes by playing. A floor past the act's fifth takes floor 5's.
    /// </summary>
    [Serializable]
    public sealed class FloorPaletteValues
    {
        /// <summary>
        /// The order's switch (Kristian, 2026-11-24, "Paletter per etasje": "Steinfarge, lampevarme og tåke endres per etasje,
        /// kaldere og dypere nedover. Ingen ny grafikk."). Off: every floor looks as before the order, field for field.
        /// </summary>
        public bool On = true;

        // Stone: red down, blue up, a step a floor, the first steps the widest (round 1's pictures: floors 2 and 3 hard to tell
        // apart with even steps of 0.05 red and 0.09 blue): the kit's sandstone-grey (0.38, 0.29, 0.25) at its middle towards a
        // slate (0.32, 0.31, 0.37) on floor 5, with the lift. Lamps: blue and a little green up, red down (warm (1, 0.66, 0.36)
        // towards (0.89, 0.70, 0.49) on floor 5: still red over green over blue). Base: the mood's (0.84, 0.88, 1) towards a cold
        // blue (0.67, 0.88, 1.3) on floor 5; its level 1 on every floor (under 1 the stone's lift rises to keep the unlit floor as
        // bright as today, so a lower base only brightens the rest). Fog: bluer, and a tenth thicker a floor.
        public FloorPalette Floor1 = new FloorPalette { Name = "floor 1: as today" };
        public FloorPalette Floor2 = new FloorPalette
        {
            Name = "floor 2",
            Stone = new Rgb(0.92f, 1f, 1.1f), Lamps = new Rgb(0.96f, 1.02f, 1.12f),
            Base = new Rgb(0.92f, 1f, 1.1f), BaseLevel = 1f,
            Fog = new Rgb(0.92f, 1f, 1.12f), FogShare = 1.1f,
        };
        public FloorPalette Floor3 = new FloorPalette
        {
            Name = "floor 3",
            Stone = new Rgb(0.86f, 1f, 1.19f), Lamps = new Rgb(0.93f, 1.04f, 1.22f),
            Base = new Rgb(0.86f, 1f, 1.18f), BaseLevel = 1f,
            Fog = new Rgb(0.86f, 1f, 1.22f), FogShare = 1.2f,
        };
        public FloorPalette Floor4 = new FloorPalette
        {
            Name = "floor 4",
            Stone = new Rgb(0.82f, 1f, 1.27f), Lamps = new Rgb(0.91f, 1.05f, 1.3f),
            Base = new Rgb(0.82f, 1f, 1.25f), BaseLevel = 1f,
            Fog = new Rgb(0.82f, 1f, 1.3f), FogShare = 1.3f,
        };
        public FloorPalette Floor5 = new FloorPalette
        {
            Name = "floor 5 (the boss's): the row's coldest",
            Stone = new Rgb(0.8f, 1f, 1.36f), Lamps = new Rgb(0.89f, 1.06f, 1.37f),
            Base = new Rgb(0.8f, 1f, 1.3f), BaseLevel = 1f,
            Fog = new Rgb(0.8f, 1f, 1.38f), FogShare = 1.4f,
        };

        /// <summary>How many times a floor's palette was looked up (for the tests: once a floor, nothing a frame).</summary>
        [NonSerialized] public int Reads;

        /// <summary>The five rows, floor 1 first.</summary>
        public FloorPalette[] Rows => new[] { Floor1, Floor2, Floor3, Floor4, Floor5 };

        /// <summary>The palette of a floor (1 to 5; under 1 floor 1's, past 5 floor 5's); null with the switch off. Counted in <see cref="Reads"/>.</summary>
        public FloorPalette Of(int floor)
        {
            Reads++;
            if (!On) return null;
            switch (floor)
            {
                case 2: return Floor2;
                case 3: return Floor3;
                case 4: return Floor4;
                default: return floor >= 5 ? Floor5 : Floor1;
            }
        }
    }

    /// <summary>
    /// What a palette makes of the floor's colours (work order 105), and the measure that no walkable point gets darker than it is
    /// today (work order 091's measure, carried to colour and to the stone: <see cref="LightAt"/>). Plain data, no engine.
    /// </summary>
    public static class FloorPaletteRules
    {
        public static readonly Rgb One = new Rgb(1f, 1f, 1f);

        public static bool IsOne(Rgb c) => c.R == 1f && c.G == 1f && c.B == 1f;

        /// <summary>A colour's luminance (Rec. 709 weights, as work order 091's measure).</summary>
        public static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        public static Rgb Times(Rgb a, Rgb b) => new Rgb(a.R * b.R, a.G * b.G, a.B * b.B);

        public static Rgb Scaled(Rgb a, float k) => new Rgb(a.R * k, a.G * k, a.B * k);

        static Rgb Plus(Rgb a, Rgb b) => new Rgb(a.R + b.R, a.G + b.G, a.B + b.B);

        /// <summary>Does a lamp's light take the palette's lamp tint? The warm ones: candles, sconces, chests, the vault's gold, the entrance's warm braziers. The teal ones (braziers, lanterns) keep the game's cyan.</summary>
        public static bool TakesLampTint(Lamp lamp) =>
            lamp != null && (lamp.Warm || lamp.Kind == LampKind.Candle || lamp.Kind == LampKind.Sconce || lamp.Kind == LampKind.Chest || lamp.Kind == LampKind.Gold);

        /// <summary>A lamp's light colour on a floor with this palette (today's with none).</summary>
        public static Rgb LampColour(FloorPalette p, Lamp lamp, LampValues v)
        {
            Rgb c = v.ColourOf(lamp);
            return p == null || p.IsToday || !TakesLampTint(lamp) ? c : Times(c, p.Lamps);
        }

        /// <summary>The base light's colour on a floor with this palette, its level folded in (relative to today's level: today's is <see cref="MoodValues.DarkTint"/>).</summary>
        public static Rgb BaseLight(FloorPalette p, MoodValues mood) =>
            p == null || p.IsToday ? mood.DarkTint : Scaled(Times(mood.DarkTint, p.Base), p.BaseLevel);

        /// <summary>The fog's grey on a floor with this palette, its share folded in (today's: <see cref="MoodValues.FogColour"/> times its level).</summary>
        public static Rgb FogColour(FloorPalette p, MoodValues mood) =>
            p == null || p.IsToday ? Scaled(mood.FogColour, mood.FogLevel) : Scaled(Times(mood.FogColour, p.Fog), mood.FogLevel * p.FogShare);

        /// <summary>
        /// Every light the stone is lit by, as it is today and as it is with the palette: the lamps' colours (each lamp kind, the
        /// entrance's warm brazier), the mood's own real lights at the light step "as before" (which keep their colours), the
        /// shots' flashes, her own light, and the base light (its level folded in).
        /// </summary>
        public static IEnumerable<(string what, Rgb today, Rgb now)> Lights(FloorPalette p, LampValues lamps, MoodValues mood)
        {
            foreach (LampKind kind in new[] { LampKind.Candle, LampKind.Brazier, LampKind.Gold, LampKind.Lantern, LampKind.Sconce, LampKind.Chest })
            {
                var lamp = new Lamp { Kind = kind };
                yield return ("a " + kind.ToString().ToLowerInvariant() + "'s light", lamps.ColourOf(lamp), LampColour(p, lamp, lamps));
            }
            var entrance = new Lamp { Kind = LampKind.Brazier, Warm = lamps.WarmEntrance };
            yield return ("the entrance's brazier's light", lamps.ColourOf(entrance), LampColour(p, entrance, lamps));
            yield return ("the mood's candle light (as before)", mood.CandleColour, mood.CandleColour);
            yield return ("the mood's brazier light (as before)", mood.Teal, mood.Teal);
            yield return ("the mood's gold light (as before)", mood.GoldColour, mood.GoldColour);
            yield return ("a shot's flash", lamps.FlashColour, lamps.FlashColour);
            yield return ("her own light", mood.HerLightColour, mood.HerLightColour);
            yield return ("the base light", mood.DarkTint, BaseLight(p, mood));
        }

        /// <summary>
        /// The least factor (1 or more) the palette's stone tint is lifted by so that no light's reflection off the stone is less
        /// bright than today's off the untinted stone: a bluer stone under the warm lamps would otherwise give back less light.
        /// 1 for a palette of ones.
        /// </summary>
        public static float StoneLift(FloorPalette p, LampValues lamps, MoodValues mood)
        {
            if (p == null || p.IsToday) return 1f;
            float lift = 1f;
            foreach ((string _, Rgb today, Rgb now) in Lights(p, lamps, mood))
            {
                float back = Luma(Times(p.Stone, now));
                if (back > 1e-6f) lift = Math.Max(lift, Luma(today) / back);
            }
            return lift;
        }

        /// <summary>The tint the stone is given on a floor with this palette: its stone tint, lifted (<see cref="StoneLift"/>); ones for none.</summary>
        public static Rgb StoneTint(FloorPalette p, LampValues lamps, MoodValues mood) =>
            p == null || p.IsToday ? One : Scaled(p.Stone, StoneLift(p, lamps, mood));

        /// <summary>
        /// Work order 091's measure, carried to colour and to the stone: the light seen off the floor at a ground point while she
        /// stands in <paramref name="room"/>, today and with the palette. The sum over every lamp with a light of its own lit from
        /// there (the lamps of the rooms the cave shows, <see cref="CaveSight.Shown"/>) of its intensity at the level (no flicker),
        /// its colour and the URP point light's fall-off (1/d², times (1 - (d²/r²)²)², d from the light to the point, r its
        /// range), plus the base light (the scene's ambient taken as white at 1, times the level's ambient share and the base
        /// colour), multiplied by the stone's tint, its luminance. Her own light and the flashes are not in it.
        /// </summary>
        public static void LightAt(LevelLayout layout, LampPlan plan, LampValues v, LamplitValues at, MoodValues mood, FloorPalette p,
                                   LevelRoom room, LevelPoint point, bool[] shown, out float today, out float now)
        {
            CaveSight.Shown(layout, room.Order, shown);
            var lampsToday = new Rgb(0f, 0f, 0f);
            var lampsNow = new Rgb(0f, 0f, 0f);
            float level = Math.Max(0f, at.LampLevel);
            foreach (Lamp l in plan.Lamps)
            {
                if (l.Room < 0 || l.Room >= shown.Length || !shown[l.Room] || l.CarriedBy != null) continue;
                v.Of(l.Kind, out _, out float range);
                double dx = l.At.X - point.X, dy = l.At.Y - point.Y;
                float d2 = (float)(dx * dx + dy * dy + l.Height * l.Height);
                float f = d2 / Math.Max(1e-4f, range * range);
                float smooth = Math.Min(1f, Math.Max(0f, 1f - f * f));
                float weight = l.Intensity(v) * level * smooth * smooth / Math.Max(d2, 1e-4f);
                if (weight <= 0f) continue;
                lampsToday = Plus(lampsToday, Scaled(v.ColourOf(l), weight));
                lampsNow = Plus(lampsNow, Scaled(LampColour(p, l, v), weight));
            }
            float ambient = Math.Max(0f, at.AmbientLevel);
            today = Luma(Plus(lampsToday, Scaled(mood.DarkTint, ambient)));
            now = Luma(Times(StoneTint(p, v, mood), Plus(lampsNow, Scaled(BaseLight(p, mood), ambient))));
        }
    }
}
