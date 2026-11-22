using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where the concept's pieces stand (work order 068; ui_b_kinetic_anime.png, set by looking at pictures of the game beside
    /// it), in layout pixels of a view 1080 high, top left 0,0: the plate at the top left (the corner's wedge kept for a
    /// portrait, her health as a number on a red plate, FOCUS and its bar under it) and the combo bar at the bottom middle,
    /// over the strip.
    /// </summary>
    public static class InterfacePlate
    {
        // ---------------- The plate at the top left ----------------

        /// <summary>The dark wedge in the corner where a portrait would stand: it runs off the screen's top and left.</summary>
        public static UiBox Corner(InterfaceValues v)
        {
            const float off = 40f; // past the screen's edges, so only its leaning right edge shows
            return new UiBox(-off, -off * 0.5f, Math.Max(0f, v.CornerWidth) + off, Math.Max(0f, v.CornerHeight) + off * 0.5f);
        }

        /// <summary>The red plate her health is on.</summary>
        public static UiBox Health(InterfaceValues v) => new UiBox(v.HealthPlateX, v.HealthPlateY, v.HealthPlateWidth, v.HealthPlateHeight);

        /// <summary>The focus bar under it.</summary>
        public static UiBox Focus(InterfaceValues v) => new UiBox(v.FocusBarX, v.FocusBarY, v.FocusBarWidth, v.FocusBarHeight);

        /// <summary>The word FOCUS, left of its bar, its middle on the bar's middle.</summary>
        public static UiBox FocusWord(InterfaceValues v)
        {
            UiBox bar = Focus(v);
            float h = v.FocusWordSize * 1.3f, w = v.FocusWordSize * 3.4f;
            return new UiBox(bar.X - w - 8f, bar.CenterY - h * 0.5f, w, h);
        }

        /// <summary>The key plate at a full gauge's right end (the focus key, "Z").</summary>
        public static UiBox FocusKey(InterfaceValues v)
        {
            UiBox bar = Focus(v);
            float h = bar.H + 10f, w = h * 1.25f;
            return new UiBox(bar.Right + v.HeavyOutline + 10f, bar.CenterY - h * 0.5f, w, h);
        }

        /// <summary>How far a box <paramref name="h"/> high leans past each side at a slant (half its lean).</summary>
        public static float HalfLean(float h, float slant) => h * slant * 0.5f;

        /// <summary>Everything the top plate covers, outlines and leans included: for the overlap checks.</summary>
        public static UiBox Whole(InterfaceValues v)
        {
            UiBox c = Corner(v), h = Health(v), f = Focus(v), k = FocusKey(v);
            float o = Math.Max(0f, v.HeavyOutline) + Math.Max(0f, v.OuterStroke), s = v.PlateSlant;
            float right = Math.Max(c.Right + HalfLean(c.H, s), Math.Max(h.Right + HalfLean(h.H, s) + o + v.Depth, Math.Max(f.Right, k.Right) + HalfLean(k.H, s) + o));
            float bottom = Math.Max(c.Bottom, Math.Max(h.Bottom, Math.Max(f.Bottom, k.Bottom)) + o + v.Depth);
            return new UiBox(0f, 0f, right, bottom);
        }

        // ---------------- The combo bar ----------------

        /// <summary>
        /// The combo bar's track, at the bottom middle: its bottom <see cref="InterfaceValues.ComboOverStrip"/> over the strip's
        /// plate, centred on the view; the label's plate stands at its left inside its width.
        /// </summary>
        public static UiBox ComboBar(int keys, float viewWidth, InterfaceValues v)
        {
            UiBox strip = InterfaceRules.StripPlate(keys, viewWidth, v);
            float w = Math.Min(Math.Max(0f, v.ComboWidth), viewWidth - 80f), h = Math.Max(4f, v.ComboHeight);
            float bottom = strip.Y - Math.Max(0f, v.ComboOverStrip);
            return new UiBox((viewWidth - w) * 0.5f, bottom - h, w, h);
        }

        /// <summary>The label's plate at the bar's left: taller than the bar, over its left end.</summary>
        public static UiBox ComboLabel(UiBox bar, InterfaceValues v)
        {
            float w = Math.Min(Math.Max(0f, v.ComboLabelWidth), bar.W * 0.4f), grow = 8f;
            return new UiBox(bar.X, bar.Y - grow, w, bar.H + 2f * grow);
        }

        /// <summary>The track the zones and the blade run on: right of the label, to the bar's end.</summary>
        public static UiBox ComboTrack(UiBox bar, InterfaceValues v)
        {
            UiBox label = ComboLabel(bar, v);
            float left = label.Right + 10f + HalfLean(label.H, v.ComboSlant);
            return new UiBox(left, bar.Y, Math.Max(10f, bar.Right - left), bar.H);
        }

        /// <summary>A tag over the track at a share <paramref name="x"/> of it (a press's number, "PRESS" and its key), <paramref name="w"/> wide.</summary>
        public static UiBox ComboTag(UiBox track, float x, float w, InterfaceValues v)
        {
            float h = v.ComboTagSize * 1.3f;
            return new UiBox(track.X + track.W * x - w * 0.5f, track.Y - h - 6f, w, h);
        }

        /// <summary>
        /// A press's number over the track at a share <paramref name="x"/> of it (<see cref="InterfaceValues.ComboNumberSize"/>),
        /// its foot just over the track. A number at the line's very start is moved right to stand clear of the label's lean.
        /// </summary>
        public static UiBox ComboNumber(UiBox track, float x, InterfaceValues v)
        {
            float h = NumberHeight(v), w = Math.Max(1, v.ComboNumberSize) * 1.1f;
            float cx = Math.Max(track.X + track.W * x, track.X + w * 0.5f + 4f);
            return new UiBox(cx - w * 0.5f, track.Y - h - 1f, w, h);
        }

        static float NumberHeight(InterfaceValues v) => Math.Max(1, v.ComboNumberSize) * 1.15f;

        /// <summary>Everything the combo bar covers, tags, outline and leans included: for the overlap checks.</summary>
        public static UiBox ComboWhole(int keys, float viewWidth, InterfaceValues v)
        {
            UiBox bar = ComboBar(keys, viewWidth, v), label = ComboLabel(bar, v);
            float tag = Math.Max(v.ComboTagSize * 1.3f + 6f, NumberHeight(v) + 1f), o = Math.Max(0f, v.HeavyOutline) + Math.Max(0f, v.OuterStroke), lean = HalfLean(label.H, v.ComboSlant);
            float top = Math.Min(label.Y, bar.Y - tag) - o, bottom = label.Bottom + o + v.Depth;
            return new UiBox(bar.X - lean - o, top, bar.W + 2f * (lean + o) + v.Depth, bottom - top);
        }
    }
}
