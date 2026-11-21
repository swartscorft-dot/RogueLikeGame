using System;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The play session's frame times (work order 074, 2026-11-21: the developer asked "Hva ligger fps på i play?" and
    /// the log had no frame time). The recorder measures every frame of play into a <see cref="FrameHistogram"/>, one per
    /// part of the session (each floor of a run, and the practice room), and when a part ends writes it to the log as one
    /// <see cref="Times"/> event and its slowest frames as <see cref="Hitch"/> events; the summary's block is computed
    /// from those (<see cref="FrameTimesSummary"/>), so it comes out the same from a log read back.
    /// </summary>
    public static class FrameTimeEvents
    {
        /// <summary>
        /// Once, at the recorder's first frame: where it ran ("ran_in" editor or player), the display's refresh rate
        /// ("refresh_hz"), vsync ("vsync", QualitySettings.vSyncCount), the target frame rate ("target_fps", -1 for the
        /// platform's default), whether the FrameTimingManager gives the main thread's and the GPU's times
        /// ("frame_timing"), and the histogram's step ("bucket_ms").
        /// </summary>
        public const string Setup = "frame_setup";
        /// <summary>
        /// A part's frames, written when the part ends: "part" ("floor" or "practice room"), "run" (the session's runs
        /// counted from 1; 0 in the practice room), "floor", "frames", "sum_ms", "min_ms", "max_ms", "over_16_7",
        /// "over_33_3", "main_frames", "main_sum_ms", "gpu_frames", "gpu_sum_ms", and "buckets", the histogram's
        /// non-empty buckets as "index:count" joined by ",".
        /// </summary>
        public const string Times = "frame_times";
        /// <summary>
        /// One of a part's slowest frames, written with its <see cref="Times"/> event: "rank" (1 the slowest), "ms", "at"
        /// (seconds since the session started, the log's clock, when it was seen), "into" (seconds into the part, game time
        /// as the floor times are), and the part's "part", "run" and "floor".
        /// </summary>
        public const string Hitch = "frame_hitch";

        public const string PartFloor = "floor";
        public const string PartPracticeRoom = "practice room";

        /// <summary>The slowest frames of each part that go to the log as hitches: the work order's five.</summary>
        public const int HitchesPerPart = 5;
        /// <summary>The seconds before a hitch in which the summary looks for what happened: the work order's half second.</summary>
        public const double CauseWindow = 0.5;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string Ms(double v) => v.ToString("0.###", Inv);
        static string Int(int v) => v.ToString(Inv);

        /// <summary>The part's frames as its log event, at <paramref name="t"/> (the log's clock).</summary>
        public static SessionEvent TimesEvent(double t, FrameHistogram h, string part, int run, int floor)
        {
            var e = new SessionEvent(t, Times);
            e.AddExtra("part", part);
            e.AddExtra("run", Int(run));
            e.AddExtra("floor", Int(floor));
            e.AddExtra("frames", Int(h.Frames));
            e.AddExtra("sum_ms", Ms(h.SumMs));
            e.AddExtra("min_ms", Ms(h.Frames > 0 ? h.MinMs : 0d));
            e.AddExtra("max_ms", Ms(h.MaxMs));
            e.AddExtra("over_16_7", Int(h.Over60));
            e.AddExtra("over_33_3", Int(h.Over30));
            e.AddExtra("main_frames", Int(h.MainFrames));
            e.AddExtra("main_sum_ms", Ms(h.MainSumMs));
            e.AddExtra("gpu_frames", Int(h.GpuFrames));
            e.AddExtra("gpu_sum_ms", Ms(h.GpuSumMs));
            e.AddExtra("buckets", h.BucketsText());
            return e;
        }

        /// <summary>The part's slowest frame of this rank (0 the slowest, below <see cref="FrameHistogram.Hitches"/>) as its log event, at <paramref name="t"/>.</summary>
        public static SessionEvent HitchEvent(double t, FrameHistogram h, int rank, string part, int run, int floor)
        {
            var e = new SessionEvent(t, Hitch);
            e.AddExtra("rank", Int(rank + 1));
            e.AddExtra("ms", Ms(h.HitchMs(rank)));
            e.AddExtra("at", h.HitchAt(rank).ToString("0.000", Inv));
            e.AddExtra("into", h.HitchInto(rank).ToString("0.000", Inv));
            e.AddExtra("part", part);
            e.AddExtra("run", Int(run));
            e.AddExtra("floor", Int(floor));
            return e;
        }

        /// <summary>A part's histogram read back from its <see cref="Times"/> event (its hitches come from their own events).</summary>
        public static FrameHistogram Read(SessionEvent e)
        {
            var h = new FrameHistogram();
            if (e == null) return h;
            h.ReadBuckets(e.ExtraValue("buckets"));
            h.Frames = Number(e, "frames");
            h.SumMs = Double(e, "sum_ms");
            h.MinMs = h.Frames > 0 ? Double(e, "min_ms") : double.PositiveInfinity;
            h.MaxMs = Double(e, "max_ms");
            h.Over60 = Number(e, "over_16_7");
            h.Over30 = Number(e, "over_33_3");
            h.MainFrames = Number(e, "main_frames");
            h.MainSumMs = Double(e, "main_sum_ms");
            h.GpuFrames = Number(e, "gpu_frames");
            h.GpuSumMs = Double(e, "gpu_sum_ms");
            return h;
        }

        internal static int Number(SessionEvent e, string key) =>
            int.TryParse(e.ExtraValue(key), NumberStyles.Integer, Inv, out int n) ? n : 0;

        internal static double Double(SessionEvent e, string key) =>
            double.TryParse(e.ExtraValue(key), NumberStyles.Float, Inv, out double d) ? d : 0d;
    }

    /// <summary>
    /// What counts as time in play for the frame times (work order 074): the game is stopped under the pause menu, an
    /// offer (a floor reward, a chest, a skill upgrade, the focus choice), the end screen and the screen between runs, and
    /// a window without focus is throttled, so those frames say nothing about the game and are not counted.
    /// </summary>
    public static class FramePlayRule
    {
        /// <summary>Is the game in play now: a character to play, the window focused, and nothing stopping the game.</summary>
        public static bool InPlay(bool hasCharacter, bool focused, bool paused, bool choiceUp, bool screenUp) =>
            hasCharacter && focused && !paused && !choiceUp && !screenUp;

        /// <summary>
        /// Does the frame that just ended count? When the game was in play as it began or is now, so the frame that opens
        /// an offer and the frame that builds the next floor as it closes are both counted, and those wholly under a
        /// screen are not; never across a gap (focus lost or got back, the Editor paused, frames not seen), whose length
        /// is not the game's.
        /// </summary>
        public static bool Counts(bool inPlayBefore, bool inPlayNow, bool gap) => !gap && (inPlayBefore || inPlayNow);
    }

    /// <summary>
    /// The frame times of one part of a play session (work order 074), kept with no list of frames and nothing allocated
    /// per frame: a fixed histogram of <see cref="BucketMs"/> steps up to <see cref="TopMs"/> (and one bucket for every
    /// slower frame), the sums, the fastest and slowest frame, the frames over 16.7 ms and 33.3 ms, the main thread's and
    /// the GPU's sums where they are given, and the <see cref="FrameTimeEvents.HitchesPerPart"/> slowest frames with when
    /// they were seen.
    /// </summary>
    public sealed class FrameHistogram
    {
        /// <summary>The histogram's step, and so the percentiles' resolution: a quarter millisecond tells 60 Hz (16.7 ms) and 144 Hz (6.9 ms) apart.</summary>
        public const double BucketMs = 0.25;
        /// <summary>The top of the stepped buckets; every frame slower falls into the last bucket, and its percentile is the slowest frame.</summary>
        public const double TopMs = 100.0;
        /// <summary>The stepped buckets (TopMs / BucketMs); the array holds one more for the slower frames.</summary>
        public const int Buckets = 400;
        /// <summary>A frame slower than this misses a 60 Hz display's refresh: the work order's 16.7 ms.</summary>
        public const double Over60Ms = 1000.0 / 60.0;
        /// <summary>A frame slower than this misses 30 Hz: the work order's 33.3 ms.</summary>
        public const double Over30Ms = 1000.0 / 30.0;

        readonly int[] _counts = new int[Buckets + 1];
        readonly double[] _hitchMs = new double[FrameTimeEvents.HitchesPerPart];
        readonly double[] _hitchAt = new double[FrameTimeEvents.HitchesPerPart];
        readonly double[] _hitchInto = new double[FrameTimeEvents.HitchesPerPart];

        public int Frames;
        public double SumMs;
        public double MinMs = double.PositiveInfinity;
        public double MaxMs;
        public int Over60;
        public int Over30;
        public int MainFrames;
        public double MainSumMs;
        public int GpuFrames;
        public double GpuSumMs;
        /// <summary>How many of the slowest frames are kept (up to <see cref="FrameTimeEvents.HitchesPerPart"/>).</summary>
        public int Hitches { get; private set; }

        public double MeanMs => Frames > 0 ? SumMs / Frames : double.NaN;
        public double Fps => Frames > 0 && SumMs > 0d ? 1000.0 * Frames / SumMs : double.NaN;
        public double MainMeanMs => MainFrames > 0 ? MainSumMs / MainFrames : double.NaN;
        public double GpuMeanMs => GpuFrames > 0 ? GpuSumMs / GpuFrames : double.NaN;

        /// <summary>The slowest frames kept, slowest first: its time, when it was seen (the log's clock), and how far into the part.</summary>
        public double HitchMs(int rank) => _hitchMs[rank];
        public double HitchAt(int rank) => _hitchAt[rank];
        public double HitchInto(int rank) => _hitchInto[rank];

        /// <summary>The bucket a frame of this many milliseconds falls into.</summary>
        public static int BucketOf(double ms)
        {
            if (!(ms > 0d)) return 0;
            if (ms >= TopMs) return Buckets;
            return Math.Min(Buckets - 1, (int)(ms / BucketMs));
        }

        public int CountIn(int bucket) => bucket >= 0 && bucket <= Buckets ? _counts[bucket] : 0;

        /// <summary>One frame of <paramref name="ms"/>, seen at <paramref name="at"/> on the log's clock, <paramref name="into"/> seconds into the part.</summary>
        public void Add(double ms, double at = 0d, double into = 0d)
        {
            if (double.IsNaN(ms) || ms < 0d) ms = 0d;
            _counts[BucketOf(ms)]++;
            Frames++;
            SumMs += ms;
            if (ms < MinMs) MinMs = ms;
            if (ms > MaxMs) MaxMs = ms;
            if (ms > Over60Ms) Over60++;
            if (ms > Over30Ms) Over30++;
            KeepIfSlow(ms, at, into);
        }

        /// <summary>The FrameTimingManager's main thread and GPU times of a frame; a time of 0 or less is not given and not counted.</summary>
        public void AddTimings(double mainMs, double gpuMs)
        {
            if (mainMs > 0d) { MainFrames++; MainSumMs += mainMs; }
            if (gpuMs > 0d) { GpuFrames++; GpuSumMs += gpuMs; }
        }

        /// <summary>Into the slowest frames, slowest first, when it is slower than the slowest kept's last; an equal one keeps the earlier.</summary>
        public void KeepIfSlow(double ms, double at, double into)
        {
            int n = _hitchMs.Length;
            if (Hitches == n && !(ms > _hitchMs[n - 1])) return;
            int i = Hitches < n ? Hitches++ : n - 1;
            while (i > 0 && ms > _hitchMs[i - 1])
            {
                _hitchMs[i] = _hitchMs[i - 1];
                _hitchAt[i] = _hitchAt[i - 1];
                _hitchInto[i] = _hitchInto[i - 1];
                i--;
            }
            _hitchMs[i] = ms;
            _hitchAt[i] = at;
            _hitchInto[i] = into;
        }

        /// <summary>
        /// The frame time below which this share of the frames lie (0.95 for the 95th percentile), by nearest rank from the
        /// histogram: the top of the bucket that holds the frame of that rank, never above the slowest frame or below the
        /// fastest; so to <see cref="BucketMs"/>. NaN with no frames.
        /// </summary>
        public double Percentile(double share)
        {
            int total = 0;
            for (int b = 0; b <= Buckets; b++) total += _counts[b];
            if (total == 0) return double.NaN;
            int rank = Math.Max(1, (int)Math.Ceiling(share * total - 1e-9));
            int seen = 0;
            for (int b = 0; b <= Buckets; b++)
            {
                seen += _counts[b];
                if (seen < rank) continue;
                double top = b == Buckets ? MaxMs : (b + 1) * BucketMs;
                if (top > MaxMs) top = MaxMs;
                if (Frames > 0 && top < MinMs) top = MinMs;
                return top;
            }
            return MaxMs;
        }

        /// <summary>Adds another part's frames to these (the whole session, the practice room's stretches): sums, buckets and slowest frames.</summary>
        public void Merge(FrameHistogram other)
        {
            if (other == null) return;
            for (int b = 0; b <= Buckets; b++) _counts[b] += other._counts[b];
            Frames += other.Frames;
            SumMs += other.SumMs;
            if (other.Frames > 0 && other.MinMs < MinMs) MinMs = other.MinMs;
            if (other.MaxMs > MaxMs) MaxMs = other.MaxMs;
            Over60 += other.Over60;
            Over30 += other.Over30;
            MainFrames += other.MainFrames;
            MainSumMs += other.MainSumMs;
            GpuFrames += other.GpuFrames;
            GpuSumMs += other.GpuSumMs;
            for (int i = 0; i < other.Hitches; i++) KeepIfSlow(other._hitchMs[i], other._hitchAt[i], other._hitchInto[i]);
        }

        /// <summary>Empty again, for the next part; nothing is made anew.</summary>
        public void Clear()
        {
            Array.Clear(_counts, 0, _counts.Length);
            Frames = 0;
            SumMs = 0d;
            MinMs = double.PositiveInfinity;
            MaxMs = 0d;
            Over60 = Over30 = 0;
            MainFrames = GpuFrames = 0;
            MainSumMs = GpuSumMs = 0d;
            Hitches = 0;
        }

        /// <summary>The non-empty buckets as "index:count" joined by ",", for the log.</summary>
        public string BucketsText()
        {
            var sb = new StringBuilder();
            for (int b = 0; b <= Buckets; b++)
            {
                if (_counts[b] == 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(b.ToString(CultureInfo.InvariantCulture)).Append(':').Append(_counts[b].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Adds the buckets of a <see cref="BucketsText"/>; a piece it cannot read is skipped.</summary>
        public void ReadBuckets(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (string piece in text.Split(','))
            {
                int colon = piece.IndexOf(':');
                if (colon <= 0) continue;
                if (!int.TryParse(piece.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int b)) continue;
                if (!int.TryParse(piece.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int c)) continue;
                if (b < 0 || b > Buckets || c < 0) continue;
                _counts[b] += c;
            }
        }
    }
}
