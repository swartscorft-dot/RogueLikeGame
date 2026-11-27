using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The lancer's warning mark (work order 145). Work order 140 found that with "only when seen" on no lancer charged even once
    /// (<see cref="RangedOpeningRules.LancerToo"/>: it keeps 12 to 16 m from her and his screen reaches about 7.7 m toward its
    /// bottom), and asked how it should come into view; Kristian answered, 2026-11-27: "Utenomfra, men et lite varsel ikon kan være
    /// nice. Et utropstegn". So the lancer goes on charging from off his screen exactly as before, and the one new thing is this:
    /// when a lancer begins its charge's wind-up while it stands outside what the camera shows of the ground (the real camera's
    /// view, <see cref="ScreenGround"/>, with <see cref="ScreenMargin"/>), one plain "!" stands at the edge of his screen, a small
    /// inset inside it, on the line from her to that lancer as the camera shows it. It stays while that lancer is in its wind-up or
    /// its charge and still off his screen; it goes the moment the lancer is on screen, the charge is over or the lancer dies, and
    /// it does not come back in the same charge. A lancer that begins on screen gets none; no other enemy does. No sound, arrow,
    /// flash, slowing, text, nothing over the lancer itself, nothing at the cursor or the sight.
    ///
    /// The values are in layout pixels of a view 1080 high (as the sight's cues and the duck's key sign), first guesses; he tunes
    /// by playing. The runtime (<c>LancerWarningMarks</c>) keeps a small fixed pool of <see cref="Pool"/> tracks and asks
    /// <see cref="Step"/> and <see cref="Place"/>; nothing here knows the engine.
    /// </summary>
    public static class LancerWarningMark
    {
        /// <summary>
        /// Work order 145's switch, ON by default. Kristian, 2026-11-27, asked how the lancer should come into view: "Utenomfra, men
        /// et lite varsel ikon kan være nice. Et utropstegn". Off: the game as before, field for field (no mark, nothing tracked).
        /// One switch for the whole game, so it is static; tests set it and put it back.
        /// </summary>
        public static bool On = true;

        /// <summary>
        /// m inside what the camera shows of the ground the lancer's feet must stand to count as on his screen: 0, the plain edge
        /// (the order: without the shooters' rule's half metre, <see cref="RangedAroundValues.ScreenMargin"/>). Over 0, the mark
        /// stays until the lancer is that far inside.
        /// </summary>
        public static float ScreenMargin = 0f;

        /// <summary>How many lancers charging at once a mark can be kept for: the pool's size (a room holds one to three).</summary>
        public const int Pool = 4;

        /// <summary>Layout px: the whole "!" from the top of its bar to the foot of its dot, without the outline. About the skill strip's small signs.</summary>
        public static float Height = 34f;
        /// <summary>Layout px: the width of its bar, and its dot's diameter.</summary>
        public static float Width = 9f;
        /// <summary>Layout px between the bar's foot and the dot.</summary>
        public static float DotGap = 4f;
        /// <summary>Layout px of dark outline round the bar and the dot (the sight's cues lie on a dark line 3 px wider: 1.5 a side; a little more here, alone at the edge on any floor).</summary>
        public static float Outline = 2.5f;
        /// <summary>The outline's opacity (the sight's cues: 0.75; the skill strip's black plates are opaque).</summary>
        public static float OutlineAlpha = 0.85f;

        /// <summary>Layout px from the screen's left, top and right edges to the mark's box (its outline included).</summary>
        public static float Inset = 28f;
        /// <summary>
        /// Layout px from the screen's bottom edge to the mark's box: over the skill strip, which stands at the bottom in the middle
        /// (its boxes' top 183 px up at the defaults, <see cref="InterfaceRules.Slot"/>, and the pips and the chamber row over them)
        /// and is drawn over the words and signs: a mark under it would be hidden.
        /// </summary>
        public static float BottomInset = 210f;

        /// <summary>
        /// Its colour: the lancer's own warning, the blue of the glint that grows on it through the wind-up (its charge can be
        /// countered: <see cref="MeleeYouReadRules.GlintIsBlue"/>, <c>CombatFxValues.CounterRim</c> (0.15, 0.45, 1)). The enemies'
        /// other telegraph colour, the red of a strike, is <c>CombatFxValues.StrikeRim</c> (1, 0.16, 0.1).
        /// </summary>
        public static Rgba Colour = new Rgba(0.15f, 0.45f, 1f);

        /// <summary>Half the mark's box, its outline included: across and up.</summary>
        public static float HalfWidth => Width * 0.5f + Outline;
        public static float HalfHeight => Height * 0.5f + Outline;

        /// <summary>What one lancer's mark remembers between frames.</summary>
        public struct Track
        {
            /// <summary>It was in its wind-up or its charge at the last step.</summary>
            public bool InCharge;
            /// <summary>Its mark is up: it began this charge off his screen and has not been on it since.</summary>
            public bool Marked;
        }

        /// <summary>
        /// One frame of one lancer: <paramref name="inCharge"/>, it is in its charge's wind-up or its charge's run; <paramref name="onScreen"/>,
        /// its feet stand on his screen (with <see cref="ScreenMargin"/>). At the first step of a charge the mark is up if it stands
        /// off screen; it goes when it is seen, and does not come back before the next charge. Off, dead or out of its charge, the
        /// track is cleared. True while the mark is up.
        /// </summary>
        public static bool Step(ref Track t, bool on, bool alive, bool inCharge, bool onScreen)
        {
            if (!on || !alive || !inCharge)
            {
                t = default;
                return false;
            }
            if (!t.InCharge)
            {
                t.InCharge = true;
                t.Marked = !onScreen; // begun off his screen: a mark; begun on it: none for this charge
            }
            else if (t.Marked && onScreen) t.Marked = false; // seen: he has the lancer itself
            return t.Marked;
        }

        /// <summary>
        /// Where the mark's middle stands, in layout px from the top left of a view <paramref name="viewWidth"/> by
        /// <paramref name="viewHeight"/>: on the ray from her point (<paramref name="herX"/>, <paramref name="herY"/>) along the way
        /// the line to the lancer runs on the screen (<paramref name="dirX"/>, <paramref name="dirY"/>, y down), where it meets the box
        /// the mark's middle may stand in (its whole box <see cref="Inset"/> inside the left, top and right edges and
        /// <see cref="BottomInset"/> over the bottom). Her point outside that box is taken at its nearest point in it. False with no
        /// way (a lancer on her point).
        /// </summary>
        public static bool Place(float herX, float herY, float dirX, float dirY, float viewWidth, float viewHeight, out float x, out float y) =>
            Place(herX, herY, dirX, dirY, viewWidth, viewHeight, default, out x, out y);

        /// <summary>
        /// As <see cref="Place(float, float, float, float, float, float, out float, out float)"/>, and kept off a piece of the HUD
        /// drawn over the words and signs (<paramref name="keepOut"/>, layout px; none when its width or height is 0): the top plate
        /// at the top left (<see cref="InterfacePlate.Whole"/>), which would hide a mark at the top edge left of about 560 px. Where
        /// the line would end with the mark's box on that piece, the mark stops on the same line short of it, <see cref="Inset"/> off it.
        /// </summary>
        public static bool Place(float herX, float herY, float dirX, float dirY, float viewWidth, float viewHeight, UiBox keepOut, out float x, out float y)
        {
            Bounds(viewWidth, viewHeight, out float left, out float top, out float right, out float bottom);
            float hx = Clamp(herX, left, right), hy = Clamp(herY, top, bottom);
            x = hx;
            y = hy;
            double length = Math.Sqrt((double)dirX * dirX + (double)dirY * dirY);
            if (length < 1e-6) return false;
            double dx = dirX / length, dy = dirY / length;
            double t = double.PositiveInfinity;
            if (dx > 1e-9) t = Math.Min(t, (right - hx) / dx);
            else if (dx < -1e-9) t = Math.Min(t, (left - hx) / dx);
            if (dy > 1e-9) t = Math.Min(t, (bottom - hy) / dy);
            else if (dy < -1e-9) t = Math.Min(t, (top - hy) / dy);
            if (double.IsInfinity(t)) return false;
            if (keepOut.W > 0f && keepOut.H > 0f)
            {
                // The ray's entry into the piece grown by the mark's half box and the inset: the mark's box stays that far off it.
                double l = keepOut.X - HalfWidth - Inset, r = keepOut.X + keepOut.W + HalfWidth + Inset;
                double tp = keepOut.Y - HalfHeight - Inset, bt = keepOut.Y + keepOut.H + HalfHeight + Inset;
                bool inside = hx > l && hx < r && hy > tp && hy < bt;
                if (!inside && Entry(hx, hy, dx, dy, l, tp, r, bt, out double enter) && enter < t) t = Math.Max(0.0, enter);
            }
            x = Clamp((float)(hx + dx * t), left, right);
            y = Clamp((float)(hy + dy * t), top, bottom);
            return true;
        }

        /// <summary>The box the mark's middle may stand in, layout px: its whole box inside the insets.</summary>
        public static void Bounds(float viewWidth, float viewHeight, out float left, out float top, out float right, out float bottom)
        {
            left = Inset + HalfWidth;
            right = Math.Max(left, viewWidth - Inset - HalfWidth);
            top = Inset + HalfHeight;
            bottom = Math.Max(top, viewHeight - BottomInset - HalfHeight);
        }

        /// <summary>The bar's height: the whole height less the gap and the dot, never under the dot's.</summary>
        public static float BarHeight => Math.Max(Width, Height - DotGap - Width);

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Where a ray from outside a box first meets it (the slabs' way): false when it misses it.</summary>
        static bool Entry(double ox, double oy, double dx, double dy, double l, double t, double r, double b, out double enter)
        {
            enter = 0.0;
            double near = double.NegativeInfinity, far = double.PositiveInfinity;
            if (!Slab(ox, dx, l, r, ref near, ref far) || !Slab(oy, dy, t, b, ref near, ref far)) return false;
            if (far < near || far < 0.0) return false;
            enter = near;
            return true;
        }

        static bool Slab(double o, double d, double lo, double hi, ref double near, ref double far)
        {
            if (Math.Abs(d) < 1e-12) return o >= lo && o <= hi;
            double a = (lo - o) / d, b = (hi - o) / d;
            if (a > b) { double s = a; a = b; b = s; }
            near = Math.Max(near, a);
            far = Math.Min(far, b);
            return true;
        }
    }
}
