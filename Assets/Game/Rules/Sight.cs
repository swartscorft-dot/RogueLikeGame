using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The sight (work order 144): the game's own pointer in a run, drawn at the cursor in place of the Windows arrow (Kristian,
    /// playing main on 2026-11-26: "vi har ingen muspeker design"; his tick of round 1's sheet 2, "armen langs ringen": an open
    /// ring sight with a centre dot; a bronze arm on a pin at the ring's lower right swings up along the ring's outside to a short
    /// row of bronze teeth; while the arm's tip is at the teeth it is the right moment). Part A's round 2 drew it to these
    /// numbers (<c>codex\images\sight\round2\sight_numbers.py</c>, <c>compose.py</c>, make "V": every shape drawn in code) and the
    /// producer chose its opening, variant P (<c>p_variant.py</c>): the dot, a thin teal ring and the arm never leave the cursor
    /// and never change size; four gunmetal brackets with a dim teal line sit snug round the ring when she is accurate and
    /// travel out to the cone's projected width (work order 143's cone, <see cref="ConeAtTheCursor"/>).
    ///
    /// Sizes are layout px of a screen 1080 high (scaled with the screen's height), angles degrees clockwise from the top of
    /// the screen, as part A's. Colours are part A's (0 to 255 there). First guesses: he tunes by playing.
    /// </summary>
    [Serializable]
    public sealed class SightValues
    {
        /// <summary>
        /// The order's switch (work order 144, his words above and his pick, sheet 2): ON, the sight is the pointer in a run (the
        /// Windows arrow hidden while she plays), 143's plain brackets give way to its own, and "Sight" is a fourth timing cue
        /// (<see cref="TimingCue.Sight"/>) with its line on the Trials page. Off, the game is as before the order, field for field:
        /// the Windows arrow, 143's brackets, 134's three cues and the bar only.
        /// </summary>
        public static bool On = true;

        /// <summary>
        /// The opening (the producer's change to his pick, 2026-11-27, variant P, "the pointer stays"): ON, the dot, the teal ring
        /// and the arm stay at the cursor at one size and only the four brackets travel to the cone's edges; OFF, the whole ring
        /// spreads as his sheet's sixth drawing (round 2's "V": the four segments travel, the arm with the right one). Why ON: at
        /// 24 degrees and 10 m the spread ring is four small arcs 252 px apart and a lone dot, the arm 130 px from the aim. A
        /// choice on the Trials page for him to compare (<see cref="SightLines"/>).
        /// </summary>
        public static bool PointerStays = true;

        /// <summary>The numbers in use (made anew with each Play session).</summary>
        public static SightValues Live = new SightValues();

        // ---------------- The ring and the brackets (sight_numbers.py, p_variant.py) ----------------
        public float RingInner = 26f;          // px: the brackets' band (V's ring band) from here ...
        public float RingOuter = 32f;          // px: ... to here (RING_R 29, RING_W 6): the inside clear to 26 px, as 134's band left it
        public float Outline = 1.5f;           // px of near-black outline round the brackets, the dot and the head (OUTLINE)
        public float BracketSpan = 74f;        // degrees each of the four brackets spans, centred top, right, bottom, left (gaps of 16)
        public float BracketLineAlpha = 120f / 255f; // the brackets' teal line's opacity: 80 in the producer's pick of round 2b ("dim line"), too faint on the dark blue floors he plays on; 120 with the gunmetal a step lighter is the least that is seen at a glance there while the pointer's ring stays the brightest thing (work order 144, round 2: capturesdrackets)
        public float LineInset = 1.25f;        // px from the band's inner edge to the teal line's middle (TEAL_R 27.25)
        public float LineWidth = 2f;           // px: the teal line (the pointer's ring, and each bracket's dim line)
        public float PointerOutline = 1f;      // px of dark outline either side of the pointer's ring (TEAL_OL)
        public float DotRadius = 2.5f;         // px: the centre dot (with its outline 4): exactly on the cursor, the floor round it open

        // ---------------- The mechanism (sight_numbers.py; compose.py, make V, round 2b) ----------------
        public float PinAngle = 150f;          // degrees: the pin at the ring's lower right (his pick)
        public float RestGap = 14f;            // degrees from the pin to the head's middle at rest (folded against the pin)
        public float Rate = 100f;              // degrees a second the head climbs from the press: ONE rate for every press, so a zone's width is its seconds
        public float HeadHalf = 5f;            // degrees: the head's half-width along its path
        public float Tip = 1f;                 // degrees its leading catch stands beyond the head's body: the leading edge is the judged entry
        public float CatchFlat = 2f;           // degrees: the catch's flat outer tip behind its leading edge (the part that runs under the teeth's tips)
        public float HeadRadius = 37.5f;       // px: the arm's band and the pin stand on this circle
        public float HeadInner = 34.5f;        // px: the head's body from here ...
        public float HeadOuter = 40.5f;        // px: ... to here,
        public float CatchOuter = 42f;         // px: and its catch out to here, past the teeth's tips (TeethInner)
        public float CatchFront = 39f;         // px: the catch's leading edge runs in from CatchOuter to here (HeadOuter less 1.5)
        public float TeethInner = 41f;         // px: the teeth's tips (pointing in)
        public float RackInner = 44.5f;        // px: the rack the teeth stand on, from here ...
        public float RackOuter = 49f;          // px: ... to here
        public float ToothWidth = 4f;          // px: a tooth's width at its tip
        public float ToothEvery = 8f;          // degrees of zone a tooth (at least two: the first and last flush with the zone's ends)
        public float TeethOutline = 1f;        // px: the teeth's outline, inside their own edge
        public float ArmWidth = 3.5f;          // px: the arm's band from the pin to the head (its dark outline Outline a side)
        public float PinOuter = 4.5f;          // px: the pin's dark disc,
        public float PinRadius = 3f;           // its coloured disc,
        public float PinHole = 1.3f;           // and its dark middle
        public float MostSweep = 330f;         // degrees the head may climb at most (a window longer than 3.3 s never wraps it onto its pin)

        // ---------------- Colours (compose.py; 0 to 255 there) ----------------
        public Rgba Dark = new Rgba(14f / 255f, 14f / 255f, 18f / 255f);         // the outlines
        public Rgba Gunmetal = new Rgba(96f / 255f, 105f / 255f, 117f / 255f);   // the brackets (and the whole ring's segments): a step lighter than part A's (64, 70, 78), which sank into the dark floors (round 2)
        public Rgba Bronze = new Rgba(205f / 255f, 152f / 255f, 82f / 255f);     // the arm, the pin, the teeth while a press is timed
        public Rgba BronzeDim = new Rgba(150f / 255f, 112f / 255f, 64f / 255f);  // the teeth too late
        public Rgba Teal = new Rgba(0.05f, 0.95f, 0.88f);                        // the ring, the dot, the teeth and pin at "now" (InterfaceValues.Teal)
        public Rgba Late = new Rgba(178f / 255f, 58f / 255f, 54f / 255f);        // the arm too late: a dull red, not the enemies' warning red (0.96, 0.14, 0.24)
        public Rgba LateGrey = new Rgba(150f / 255f, 153f / 255f, 158f / 255f);  // the ring's line too late
        public Rgba White = new Rgba(1f, 1f, 1f);                                // the ring's line and the head at "now"

        // ---------------- Drawing ----------------
        public float Feather = 1f;             // screen px over which every edge fades (the box filter part A's pictures were reduced with)
        public int Depth = -100;               // GUI.depth: over everything, as the Windows arrow it stands for (0 in the first pictures: the developer's run box drew over it); not drawn under any screen anyway
        public float StepDegrees = 3f;         // degrees between the points of a drawn arc (0.03 px from the true circle at 49 px)

        /// <summary>The teal line's middle (the pointer's ring, and the brackets' dim line).</summary>
        public float LineRadius => RingInner + LineInset;
        /// <summary>The brackets' band's middle.</summary>
        public float RingMiddle => (RingInner + RingOuter) * 0.5f;
    }

    /// <summary>
    /// The arm's timing (work order 144, part A's numbers): the head leaves its pin at the press of the shot before and climbs at
    /// <see cref="SightValues.Rate"/>; the teeth zone stands from where its catch is at the clean part's start to where it is at
    /// its end, by work order 142's rule (<see cref="DrawnEdges"/>: a drawn edge is the judged edge, line widths counted): the
    /// catch's leading edge first touches the zone's first tooth on the frame the press turns clean, and the catch's flat tip
    /// has wholly left the last tooth on the first frame it is late. So the zone is the clean part's seconds times the rate less
    /// the catch's own width (<see cref="CatchSpan"/>): Triple Tap's last press 27 degrees, Backstep's 20, Triple Tap's second
    /// 15 (part A drew the zone to the leading edge at both ends: 30, 23 and 18, its exit 30 ms late). The clock is the one
    /// every cue reads (<see cref="TimingCueClock"/>): the judge's seconds since the window opened, and before it the seconds
    /// since the press counted back from the shot's startup (the lead). Angles fall as the head climbs (150 at the pin, 0 at the top).
    /// </summary>
    public static class SightArm
    {
        /// <summary>The head's middle at rest (and too late: back at its pin).</summary>
        public static float RestAngle(SightValues v) => v.PinAngle - v.RestGap;

        /// <summary>The head's middle <paramref name="sincePress"/> seconds after the press of the shot before.</summary>
        public static float HeadAngle(float sincePress, SightValues v)
        {
            float rate = Math.Max(0f, v.Rate);
            float most = rate > 0f ? Math.Max(0f, v.MostSweep) / rate : 0f;
            return RestAngle(v) - rate * Math.Max(0f, Math.Min(most, sincePress));
        }

        /// <summary>The catch's leading edge for the head's middle at <paramref name="head"/>.</summary>
        public static float LeadAngle(float head, SightValues v) => head - v.HeadHalf - v.Tip;

        /// <summary>Degrees the catch's flat tip spans behind its leading edge.</summary>
        public static float CatchSpan(SightValues v) => v.Tip + v.CatchFlat;

        /// <summary>The seconds since the press of the shot before, from the cues' clock (<see cref="TimingCueClock.Since"/>) and that shot's startup.</summary>
        public static float SincePress(float since, float lead) => since + Math.Max(0f, lead);

        /// <summary>
        /// The teeth zone for a clean part, its window opening <paramref name="lead"/> seconds after the press: from
        /// <paramref name="start"/> (where the catch's leading edge is as the clean part begins) up to <paramref name="end"/>
        /// (where the catch's tip's back is as it ends); start &gt; end. False with no clean part.
        /// </summary>
        public static bool Zone(CleanPart clean, float lead, SightValues v, out float start, out float end)
        {
            start = end = 0f;
            if (!clean.Any) return false;
            start = LeadAngle(HeadAngle(SincePress(clean.From, lead), v), v);
            end = LeadAngle(HeadAngle(SincePress(clean.To, lead), v), v) + CatchSpan(v);
            return start > end;
        }

        /// <summary>Part A's zone, for comparison: to the catch's leading edge at both ends (its exit a catch's width late).</summary>
        public static void ZoneOfPartA(CleanPart clean, float lead, SightValues v, out float start, out float end)
        {
            start = LeadAngle(HeadAngle(SincePress(clean.From, lead), v), v);
            end = LeadAngle(HeadAngle(SincePress(clean.To, lead), v), v);
        }

        /// <summary>The judge's rounding at either end of a clean part, in degrees at the rate.</summary>
        public static float EdgeDegrees(SightValues v) => TimingCueClock.Edge * Math.Max(0f, v.Rate);

        /// <summary>The catch (its leading edge at <paramref name="lead"/>) touches or lies in the zone, as drawn.</summary>
        public static bool CatchInZone(float lead, float start, float end, SightValues v)
        {
            float e = EdgeDegrees(v);
            return start > end && lead <= start + e && lead + CatchSpan(v) >= end - e;
        }

        /// <summary>The teeth: how many (the first and last flush with the zone's ends), and each one's width in degrees at its tip.</summary>
        public static int Teeth(float start, float end, SightValues v, out float toothDegrees)
        {
            toothDegrees = (float)(Math.Max(0f, v.ToothWidth) / Math.Max(1e-3f, v.TeethInner) * 180.0 / Math.PI);
            return Math.Max(2, (int)Math.Round((start - end) / Math.Max(1e-3f, v.ToothEvery)) + 1);
        }

        /// <summary>Tooth <paramref name="i"/> of <paramref name="count"/>: its start (the larger angle); it spans down to start less a tooth's width.</summary>
        public static float ToothStart(int i, int count, float start, float end, float toothDegrees) =>
            count <= 1 ? start : start - (start - end - toothDegrees) * i / (count - 1);

        /// <summary>What the sight says now: the window is open and the catch is in the zone (the whole of it computed from the clock).</summary>
        public static bool SaysClean(float sinceOpened, float window, CleanPart clean, float lead, SightValues v)
        {
            if (sinceOpened < 0f || sinceOpened > window + TimingCueClock.Edge || !Zone(clean, lead, v, out float start, out float end)) return false;
            return CatchInZone(LeadAngle(HeadAngle(SincePress(sinceOpened, lead), v), v), start, end, v);
        }

        /// <summary>Too late: the window still open, the clean part behind it.</summary>
        public static bool IsLate(float sinceOpened, float window, CleanPart clean) =>
            sinceOpened >= 0f && sinceOpened <= window + TimingCueClock.Edge && clean.IsAfter(sinceOpened);
    }

    /// <summary>
    /// The opening (work order 144, variant P): the brackets' innermost ink (their dark outline) stands at the cone's edge at the
    /// cursor's distance, work order 142's rule (part A put their gunmetal there, the outline 1.5 px inside the cone); never
    /// inside their closed place round the ring.
    /// </summary>
    public static class SightOpening
    {
        /// <summary>The brackets' innermost ink when closed.</summary>
        public static float ClosedInk(SightValues v) => v.RingInner - v.Outline;

        /// <summary>px (layout) the brackets travel out for a cone <paramref name="halfWidth"/> layout px wide either side of the cursor.</summary>
        public static float Shift(float halfWidth, SightValues v) => Math.Max(0f, halfWidth - ClosedInk(v));
    }

    /// <summary>The sight's line on the Trials page (work order 144): the opening, "Brackets" (the pointer stays) or "Whole ring".</summary>
    public static class SightLines
    {
        public const string Name = "Sight opening";
        public const string Brackets = "Brackets", WholeRing = "Whole ring";

        /// <summary>A stored number: 1 the pointer stays, 0 the whole ring spreads, anything else none stored (the switch's own).</summary>
        public static bool PointerStays(int stored) => stored == 1 || (stored != 0 && SightValues.PointerStays);

        public static string WordsOf(bool pointerStays) => pointerStays ? Brackets : WholeRing;
    }
}
