using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// THE RULE for every drawing of a timed press (work order 142; Kristian, 2026-11-26, with the ring cue on: "selvom jeg for
    /// sirkelen innenfor strekene får jeg feil"): <b>a drawn edge is the judged edge, to the frame, line widths included.</b>
    /// The moment a moving mark's ink first TOUCHES the ink that marks the clean part must be the frame the press turns clean, and
    /// the moment it has fully LEFT that ink must be the first frame the press is late, within one frame (1/60 s) of the judge.
    /// A line drawn centred on the judged edge breaks it by half its width plus half the moving mark's: the ring's light lines
    /// touched 2.5 px (32 to 43 ms) and their dark outlines 5.5 px (71 to 94 ms) before the press was clean, and his early third
    /// presses (0.25 to 0.41 s, the clean part from 0.42) fell exactly there. So the marking lines are drawn INSIDE the judged
    /// part by those half-widths, a dark outline never reaches past its light line on the side the mark comes from, and the
    /// moving mark's own outline is thin enough to stay inside the frame.
    ///
    /// A later drawing of the cue (a new reticle) inherits this: give it its geometry here (<see cref="Band"/>, <see cref="NotchPost"/>)
    /// and its measure in <c>Tests/EditMode/DrawnEdgesTests.cs</c> (the table of entry and exit in px and ms for every press) and
    /// <c>Tests/PlayMode/CleanPressJudgedTests.cs</c> (frame by frame against the judge).
    /// </summary>
    public static class DrawnEdges
    {
        /// <summary>
        /// The order's switch (work order 142, his words above): ON, the ring's band lines stand inside the judged radii, the
        /// cylinder's notch posts stand where a chamber's edge meets them at the judged angle, and the combo bar's blade leans as
        /// its zones do. Off, every drawing as work order 134 and 068 drew it.
        /// </summary>
        public static bool On = true;

        /// <summary>One frame of a person's 60 a second: the most a drawn edge may stand from the judged one.</summary>
        public const float OneFrame = 1f / 60f;

        /// <summary>px: the band's two lines (light), as work order 134 drew them.</summary>
        public const float BandLine = 2f;
        /// <summary>px: how much wider a light line's dark outline is (TimingAtTheSightDraw's DarkMore).</summary>
        public const float DarkMore = 3f;
        /// <summary>px: with the switch on, the closing ring's dark outline is this much wider than its light line (3 px before: 1.5 px each side, a frame and a half at the third press).</summary>
        public const float RingDarkMore = 1f;

        /// <summary>The ring's band as drawn, in layout px (before the screen's scale): the lines' middles, and how far ink reaches.</summary>
        public struct RingBand
        {
            /// <summary>The outer and inner lines' middles.</summary>
            public float Outer, Inner;
            /// <summary>Half a band line (light).</summary>
            public float LineHalf;
            /// <summary>How far the band's ink (dark or light) reaches outward of the outer line's middle, and inward of the inner's.</summary>
            public float InkOut, InkIn;
            /// <summary>Half the closing ring's light line, and how far its ink (with its outline) reaches either side of its middle.</summary>
            public float RingHalf, RingInkHalf;
            /// <summary>The dark outline's width under each band line, and where its middle stands (outer, inner).</summary>
            public float DarkWidth, OuterDark, InnerDark;
            /// <summary>The closing ring's dark outline's width.</summary>
            public float RingDarkWidth;

            /// <summary>The ring's radius at which its light line first touches the outer light line (entering).</summary>
            public float LightEnters => Outer + LineHalf + RingHalf;
            /// <summary>The ring's radius at which any of its ink first touches any of the band's (entering).</summary>
            public float InkEnters => Outer + InkOut + RingInkHalf;
            /// <summary>The ring's radius at which its light line has fully left the inner light line (leaving).</summary>
            public float LightLeaves => Inner - LineHalf - RingHalf;
            /// <summary>The ring's radius at which all its ink has left all of the band's (leaving).</summary>
            public float InkLeaves => Inner - InkIn - RingInkHalf;
            /// <summary>The ring's light line touches the band's light lines or lies between them: as drawn, "inside the lines".</summary>
            public bool LooksInside(float ringRadius) => ringRadius <= LightEnters + 1e-4f && ringRadius >= LightLeaves - 1e-4f;
        }

        /// <summary>The ring's band as it is drawn, for these values, with the switch as it is now (<paramref name="honest"/>).</summary>
        public static RingBand Band(TimingAtTheSightValues v, bool honest)
        {
            v = v ?? new TimingAtTheSightValues();
            float lineHalf = BandLine * 0.5f, ringHalf = Math.Max(0f, v.RingStroke) * 0.5f;
            var b = new RingBand { LineHalf = lineHalf, RingHalf = ringHalf, DarkWidth = BandLine + DarkMore };
            if (!honest)
            {
                // Work order 134: both lines centred on the judged radii, their outlines and the ring's on both sides.
                b.Outer = v.BandOuter;
                b.Inner = v.BandInner;
                b.InkOut = b.InkIn = (BandLine + DarkMore) * 0.5f;
                b.RingDarkWidth = v.RingStroke + DarkMore;
                b.OuterDark = b.Outer;
                b.InnerDark = b.Inner;
            }
            else
            {
                // The light lines inside by the two half-widths: the light lines touch exactly at the judged radius. Each band
                // line's outline lies only on the band's own side of it, so nothing of the band reaches out toward the ring
                // before its light line does; the ring's own outline is half a px a side.
                b.Outer = v.BandOuter - lineHalf - ringHalf;
                b.Inner = v.BandInner + lineHalf + ringHalf;
                b.InkOut = b.InkIn = lineHalf;
                b.RingDarkWidth = v.RingStroke + RingDarkMore;
                b.OuterDark = b.Outer + lineHalf - b.DarkWidth * 0.5f;
                b.InnerDark = b.Inner - lineHalf + b.DarkWidth * 0.5f;
            }
            b.RingInkHalf = b.RingDarkWidth * 0.5f;
            return b;
        }

        /// <summary>
        /// The cylinder's notch posts' angle from the top (degrees), each side: the chamber waited for, its middle at the notch's
        /// edge (half <paramref name="notch"/>, the judged edge), has its light edge exactly at the post's inner light edge. The
        /// post stands <paramref name="postRadius"/> out, so its half-width is turned into degrees there; work order 134 turned it
        /// at the cylinder's radius, 1.4 degrees too wide (about 10 to 14 ms early).
        /// </summary>
        public static float NotchPost(float notch, float chamberRadius, float cylinderRadius, float postWidth, float postRadius, bool honest)
        {
            float halfChamber = (float)(Math.Asin(Math.Max(0f, Math.Min(1f, chamberRadius / Math.Max(1e-3f, cylinderRadius)))) * 180.0 / Math.PI);
            float at = honest ? postRadius : cylinderRadius;
            float halfPost = (float)(Math.Atan2(postWidth * 0.5f, Math.Max(1e-3f, at)) * 180.0 / Math.PI);
            return notch * 0.5f + halfChamber + halfPost;
        }

        /// <summary>The angle, from the notch's middle, at which the chamber's light edge meets the post's inner light edge (degrees).</summary>
        public static float NotchMeets(float postAngle, float chamberRadius, float cylinderRadius, float postWidth, float postRadius)
        {
            float halfChamber = (float)(Math.Asin(Math.Max(0f, Math.Min(1f, chamberRadius / Math.Max(1e-3f, cylinderRadius)))) * 180.0 / Math.PI);
            float halfPost = (float)(Math.Atan2(postWidth * 0.5f, Math.Max(1e-3f, postRadius)) * 180.0 / Math.PI);
            return postAngle - halfPost - halfChamber;
        }

        /// <summary>The combo bar's blade's lean: the zones' own with the switch on (its edge and theirs agree at every height), 0.35 before.</summary>
        public static float BladeLean(float zoneLean, bool honest) => honest ? zoneLean : 0.35f;
    }
}
