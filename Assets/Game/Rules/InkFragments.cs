using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The measure of an ink line's fragmentation (work order 125): Kristian, 2026-11-25, of work order 119's pictures, "man ser
    /// mange fragmenterte pixler rundt omkring". In a picture of a figure on a plain ground, the ink pixels (those the line made
    /// darker: the same frame drawn with the line and without) are split into connected pieces (a pixel touches its eight
    /// neighbours). A clean line is a few large pieces; a broken one is many specks. And the figure's outline (its pixels beside
    /// the ground) is looked along: a clean line runs beside nearly all of it, a broken one leaves gaps.
    /// </summary>
    public static class InkFragments
    {
        /// <summary>A piece of at most this many pixels is a speck.</summary>
        public const int SpeckPixels = 4;
        /// <summary>A piece of at least this many pixels is a large piece.</summary>
        public const int LargePixels = 40;

        // The pass marks, taken from the figures whose line was judged clean by eye at 100 % in work order 125's pictures (the
        // swarmer, the brute, the shooter, the bomber and the warder close up, the clean line multisampled: 94.9 to 98.3 % of the
        // ink in large pieces, 0 to 5.4 specks for every 1000 ink pixels), against work order 119's line on the same frames
        // (52.8 to 77.0 % and 34 to 93 specks: it fails every close-up).
        /// <summary>At least this share of the ink pixels is in large pieces.</summary>
        public const float PassShareInLarge = 0.94f;
        /// <summary>At most this many specks for every 1000 ink pixels.</summary>
        public const float PassSpecksPerThousand = 6f;
        /// <summary>
        /// At least this share of the figure's outline has ink beside it (within <see cref="BesideReach"/> px outside): a line with
        /// a side missing. Reported beside the pass, not in it: close up, a figure's outline also runs along its own inner gaps
        /// and its parts against the dark wall, so clean lines read 57 to 80 % there; at the game's camera 81 to 100 %.
        /// </summary>
        public const float PassOutlineInked = 0.9f;
        /// <summary>How far from an outline pixel the ink may be and still count as beside it, in px.</summary>
        public const int BesideReach = 2;
        /// <summary>From one frame to the next the ink pixels' count may change by at most this share of their mean.</summary>
        public const float PassFrameJump = 0.2f;

        /// <summary>What the measure found in one picture.</summary>
        public struct Result
        {
            public int InkPixels;
            public int Pieces;
            public int Specks;
            public int Largest;
            public float ShareInLarge;
            public float SpecksPerThousand;
            /// <summary>Share of the figure's outline pixels with ink beside them (NaN when no figure was given).</summary>
            public float OutlineInked;
            public int OutlinePixels;

            /// <summary>True when the line is whole: nearly all of it in large pieces, hardly a speck.</summary>
            public bool Passes => InkPixels > 0 && ShareInLarge >= PassShareInLarge && SpecksPerThousand <= PassSpecksPerThousand;

            /// <summary>True when ink runs beside nearly all of the figure's outline (no side missing).</summary>
            public bool OutlineWhole => !float.IsNaN(OutlineInked) && OutlineInked >= PassOutlineInked;

            public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} ink px in {1} pieces (largest {2}), {3:0.0} % in pieces of {4}+ px, {5} specks of <= {6} px ({7:0.0} per 1000 ink px), outline inked {8:0.0} % of {9} px: {10}",
                InkPixels, Pieces, Largest, 100f * ShareInLarge, LargePixels, Specks, SpeckPixels, SpecksPerThousand, 100f * OutlineInked, OutlinePixels, Passes ? "PASSES" : "FAILS");
        }

        /// <summary>
        /// The ink pixels of a picture: those the line made clearly darker (by at least 15 % and 12 levels of 255: an
        /// anti-aliased line's partly covered pixels count, as the eye sees them as line; at 30 % a multisampled line fell apart
        /// into pieces in the measure while it was whole to the eye). <paramref name="with"/> and <paramref name="without"/>
        /// are the same frame's luminances (0 to 255) drawn with the line and without it.
        /// </summary>
        public static bool[] InkOf(float[] with, float[] without, float darkerShare = 0.85f, float darkerBy = 12f)
        {
            if (with == null || without == null || with.Length != without.Length) throw new ArgumentException("two pictures of one size");
            var ink = new bool[with.Length];
            for (int i = 0; i < with.Length; i++) ink[i] = with[i] < without[i] * darkerShare && without[i] - with[i] > darkerBy;
            return ink;
        }

        /// <summary>Measures <paramref name="ink"/> (w by h); with <paramref name="figure"/> (the figure's own pixels, drawn without the line) its outline is looked along too.</summary>
        public static Result Measure(bool[] ink, int w, int h, bool[] figure = null)
        {
            if (ink == null || ink.Length != w * h) throw new ArgumentException("ink must be w by h");
            var r = new Result { OutlineInked = float.NaN };
            var seen = new bool[ink.Length];
            var stack = new Stack<int>();
            int inLarge = 0;
            for (int start = 0; start < ink.Length; start++)
            {
                if (!ink[start] || seen[start]) continue;
                int size = 0;
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    size++;
                    int x = i % w, y = i / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int n = ny * w + nx;
                            if (ink[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
                        }
                }
                r.Pieces++;
                r.InkPixels += size;
                if (size <= SpeckPixels) r.Specks++;
                if (size >= LargePixels) inLarge += size;
                if (size > r.Largest) r.Largest = size;
            }
            r.ShareInLarge = r.InkPixels > 0 ? (float)inLarge / r.InkPixels : 0f;
            r.SpecksPerThousand = r.InkPixels > 0 ? 1000f * r.Specks / r.InkPixels : 0f;
            if (figure != null)
            {
                if (figure.Length != w * h) throw new ArgumentException("figure must be w by h");
                int outline = 0, inked = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!figure[i] || !BesideGround(figure, w, h, x, y)) continue;
                        outline++;
                        if (InkNear(ink, figure, w, h, x, y)) inked++;
                    }
                r.OutlinePixels = outline;
                r.OutlineInked = outline > 0 ? (float)inked / outline : float.NaN;
            }
            return r;
        }

        /// <summary>The largest change of the ink pixels' count from one frame to the next, as a share of their mean count.</summary>
        public static float FrameJump(IReadOnlyList<int> inkPixelsPerFrame)
        {
            if (inkPixelsPerFrame == null || inkPixelsPerFrame.Count < 2) return 0f;
            double mean = 0;
            foreach (int n in inkPixelsPerFrame) mean += n;
            mean /= inkPixelsPerFrame.Count;
            if (mean <= 0) return 0f;
            int worst = 0;
            for (int i = 1; i < inkPixelsPerFrame.Count; i++) worst = Math.Max(worst, Math.Abs(inkPixelsPerFrame[i] - inkPixelsPerFrame[i - 1]));
            return (float)(worst / mean);
        }

        /// <summary>The same for any measure a frame (the ink pixels for every outline pixel, say).</summary>
        public static float FrameJump(IReadOnlyList<float> perFrame)
        {
            if (perFrame == null || perFrame.Count < 2) return 0f;
            double mean = 0;
            foreach (float n in perFrame) mean += n;
            mean /= perFrame.Count;
            if (mean <= 0) return 0f;
            double worst = 0;
            for (int i = 1; i < perFrame.Count; i++) worst = Math.Max(worst, Math.Abs(perFrame[i] - perFrame[i - 1]));
            return (float)(worst / mean);
        }

        static bool BesideGround(bool[] figure, int w, int h, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (!figure[ny * w + nx]) return true;
                }
            return false;
        }

        static bool InkNear(bool[] ink, bool[] figure, int w, int h, int x, int y)
        {
            for (int dy = -BesideReach; dy <= BesideReach; dy++)
                for (int dx = -BesideReach; dx <= BesideReach; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int n = ny * w + nx;
                    if (ink[n]) return true;
                }
            return false;
        }
    }
}
