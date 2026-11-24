using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// A floor's palette on a generated floor (work order 105; Kristian, 2026-11-24, "Paletter per etasje": "Steinfarge,
    /// lampevarme og tåke endres per etasje, kaldere og dypere nedover. Ingen ny grafikk."). The level adds it last when it is
    /// built (<see cref="Add"/>), with <see cref="FloorPaletteValues.On"/> and on a floor whose palette is not today's (floors 2
    /// to 5); otherwise nothing is added and the floor is as before. The floor's row of the table is read once, here, and
    /// nothing of it a frame: it has no Update.
    ///
    /// What it does, once: the crypt kit's stone (the materials of its walls, niches, low walls, piers, posts and arches, of the
    /// floors, and of the stone the dark cave stacks over the far walls) is multiplied by the palette's stone tint, lifted
    /// (<see cref="FloorPaletteRules.StoneTint"/>); those materials are the floor's own, made when it was built and destroyed with
    /// it. The lanterns, the props, the carved cells' tops, the doors, the enemies and every glow are not touched. The rooms' mood
    /// is given the palette's base light and fog (<see cref="RoomMood.SetPalette"/>), and the lamplit lamps take its lamp tint when
    /// they are made (<see cref="LampColour"/>, asked by <see cref="LamplitView"/>). The greybox view draws over it with its own
    /// materials and light, as before.
    /// </summary>
    public sealed class FloorPaletteView : MonoBehaviour
    {
        /// <summary>The table and the switch (one for the whole game, as <see cref="LamplitView.Lamps"/>).</summary>
        public static readonly FloorPaletteValues Values = new FloorPaletteValues();

        GeneratedLevel _level;

        /// <summary>The floor's row of the table.</summary>
        public FloorPalette Palette { get; private set; }
        /// <summary>The tint the stone was given (the row's, lifted) and the lift.</summary>
        public Rgb StoneTint { get; private set; }
        public float StoneLift { get; private set; }
        /// <summary>The materials tinted (each once).</summary>
        public IReadOnlyList<Material> Tinted => _tinted;
        readonly List<Material> _tinted = new List<Material>();

        /// <summary>The palette of a built floor, applied; null (nothing added) with the switch off or on a floor that looks as today.</summary>
        public static FloorPaletteView Add(GeneratedLevel level)
        {
            if (level == null || level.Layout == null) return null;
            FloorPalette row = Values.Of(level.Layout.Floor); // once a floor
            if (row == null || row.IsToday) return null;
            var view = level.gameObject.AddComponent<FloorPaletteView>();
            view._level = level;
            view.Palette = row;
            MoodValues mood = level.Values != null && level.Values.Mood != null ? level.Values.Mood : new MoodValues();
            view.StoneLift = FloorPaletteRules.StoneLift(row, LamplitView.Lamps, mood);
            view.StoneTint = FloorPaletteRules.StoneTint(row, LamplitView.Lamps, mood);
            view.TintStone();
            if (level.Mood != null) level.Mood.SetPalette(row);
            return view;
        }

        /// <summary>A lamplit lamp's light colour on this floor (its own colour given): a warm one takes the palette's lamp tint.</summary>
        public Rgb LampColour(Lamp lamp, Rgb colour) =>
            Palette == null || !FloorPaletteRules.TakesLampTint(lamp) ? colour : FloorPaletteRules.Times(colour, Palette.Lamps);

        void TintStone()
        {
            if (_level.KitPlan == null) return; // the kit off: the grey boxes, as before
            foreach (GeneratedLevel.KitPiece piece in _level.KitPieces)
                if (piece.Object != null && piece.Kind != CryptPiece.Lantern) TintAll(piece.Object);
            foreach (GameObject box in _level.FloorBoxes)
                if (box != null && box.TryGetComponent(out Renderer r)) Tint(r.sharedMaterial);
            if (_level.Cave != null)
                foreach (GameObject stacked in _level.Cave.Stacked)
                    if (stacked != null) TintAll(stacked);
        }

        void TintAll(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) Tint(r.sharedMaterial);
        }

        void Tint(Material m)
        {
            if (m == null || _tinted.Contains(m)) return;
            _tinted.Add(m);
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            Rgb t = StoneTint;
            var tinted = new Color(c.r * t.R, c.g * t.G, c.b * t.B, c.a);
            m.color = tinted;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tinted);
        }
    }
}
