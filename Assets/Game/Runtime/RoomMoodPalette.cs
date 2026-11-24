using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The rooms' mood on a floor with a palette (work order 105, <see cref="FloorPaletteView"/>; Kristian, 2026-11-24, "Paletter
    /// per etasje": "Steinfarge, lampevarme og tåke endres per etasje, kaldere og dypere nedover. Ingen ny grafikk."), as part of
    /// <see cref="RoomMood"/>: the base light's colour and level (times <see cref="MoodValues.DarkTint"/> and the ambient and key
    /// levels of the light step in use) and the fog's colour and share (times <see cref="MoodValues.FogColour"/> and its level),
    /// copied once from the floor's palette when the floor is built. Without one (the switch off, floor 1, the practice room) the
    /// darkness and the fog are computed exactly as before.
    /// </summary>
    public sealed partial class RoomMood
    {
        Rgb _paletteBase = FloorPaletteRules.One, _paletteFog = FloorPaletteRules.One;
        float _paletteBaseLevel = 1f, _paletteFogShare = 1f;

        /// <summary>True once the floor's palette was given (<see cref="SetPalette"/>).</summary>
        public bool HasPalette { get; private set; }

        /// <summary>The floor's palette, its base light and fog copied once; the darkness and the fog are told again at the next tick. Nothing for null or a palette of ones.</summary>
        public void SetPalette(FloorPalette p)
        {
            if (p == null || p.IsToday) return;
            _paletteBase = p.Base;
            _paletteBaseLevel = p.BaseLevel;
            _paletteFog = p.Fog;
            _paletteFogShare = p.FogShare;
            HasPalette = true;
            s_appliedAmbient = -1f; // the scene's light told again
            _fogLevel = -1f;        // the fog's colour too
        }

        /// <summary>The base light's colour: the mood's, times the palette's (the mood's itself without one).</summary>
        Rgb PaletteDark(Rgb dark) => HasPalette ? FloorPaletteRules.Times(dark, _paletteBase) : dark;

        /// <summary>A level of the base light (ambient or key), times the palette's (itself without one).</summary>
        float PaletteLevel(float level) => HasPalette ? level * _paletteBaseLevel : level;

        /// <summary>The fog's grey at a share of it: the mood's colour times the share, times the palette's colour and share (as before without one).</summary>
        Color PaletteFog(MoodValues v, float share)
        {
            if (!HasPalette) return new Color(v.FogColour.R * share, v.FogColour.G * share, v.FogColour.B * share, 1f);
            float s = share * _paletteFogShare;
            return new Color(v.FogColour.R * _paletteFog.R * s, v.FogColour.G * _paletteFog.G * s, v.FogColour.B * _paletteFog.B * s, 1f);
        }
    }
}
