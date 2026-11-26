using System;

namespace Roguelike.Rules
{
    /// <summary>The parts of the boss's entrance in time (work order 130).</summary>
    public enum EntrancePhase { None, TravelIn, Hold, TravelBack, Done }

    /// <summary>
    /// The boss's entrance (work order 130; Kristian, 2026-11-25 at about 18:50: "så får det heller være en litt cinematic inngang,
    /// forstørre kameraet litt og lage en kul intro animasjon. nå i første omgang, så syntes jeg intro videoen bare skal være en
    /// skikkelig tørr vits"). As she crosses the point where the boss room's fight wakes, everything holds for <see cref="Total"/>
    /// seconds of real time: the play camera travels from her to the boss and frames it closer (<see cref="CloseDistance"/>, the
    /// same rig, its pitch and field of view its own), holds while the boss makes its entrance and a card shows its name and one dry
    /// line (<see cref="BossEntranceLines"/>), and travels back to her; then the fight wakes as it would have at the point. Any of
    /// her action keys, Enter, Space or Esc ends it at once (the producer's addition: it plays every run). Every number is a first
    /// guess, his to tune; lengths in metres, times in seconds of real time, the card in layout pixels of a view 1080 high.
    /// </summary>
    [Serializable]
    public sealed class BossEntranceValues
    {
        /// <summary>
        /// The order's switch (work order 130; his words above). On (the default): the entrance plays once a boss room. Off: entering
        /// the boss room is as before the order, the fight waking at the point.
        /// </summary>
        public static bool On = true;

        // ---- The timeline (the order's first guess: about four seconds in all) ----
        public float TravelInSeconds = 0.9f;    // s the camera takes from her to the boss
        public float HoldSeconds = 2.4f;        // s it holds on the boss: the entrance and the card
        public float TravelBackSeconds = 0.7f;  // s it takes back to her; the fight wakes as it arrives
        public bool Eased = true;               // the travels ease in and out (smoothstep); false: at an even speed

        // ---- The camera (his "forstørre kameraet litt", read as: the boss shown larger) ----
        public float CloseDistance = 17f;       // m from the camera to where it looks, against the play camera's 28 (OUR angle): the boss shown close to twice as tall (about 25 % of the screen's height against 14.6 %); 16 in the first pictures, where the weapon it holds up over its head went out at the top
        public float FocusNearer = -0.2f;       // m the close camera looks short of the boss's feet, toward the camera; below 0, past them: the boss stands a little lower on the screen, so the weapon over its head stays in the frame (0.6 in the first pictures); at -0.4 a foot its Idle steps forward went under the card's top

        // ---- The skip (the producer's addition) ----
        public float SkipGraceSeconds = 0f;     // s after it begins before a key may end it: 0, at once (the order); raise it if a click as she walks in throws it away

        // ---- The card (in the interface's style: a slanted plate, Barlow Condensed; as the start menu's name plate) ----
        public float CardDelay = 0.15f;         // s into the hold before the card begins to show
        public float CardFadeIn = 0.2f;         // s it takes to show
        public float CardFadeOut = 0.25f;       // s it takes to go, ending as the hold ends
        public float CardTop = 688f;            // the name plate's top: under the boss's feet (a little under the screen's middle), so the card never covers it
        public float NamePlateWidth = 780f;     // the plate the boss's name is on
        public float NamePlateHeight = 116f;
        public float NamePad = 44f;             // from the plate's left to the name
        public int NameSize = 76;               // the name, in the heavy italic, in capitals
        public float LineGap = 18f;             // from the plate's bottom to the line's top
        public int LineSize = 30;               // the one dry line, in the lighter italic: small, stated as plain fact
        public float LineWidth = 1000f;         // the line's box, centred under the plate
        public float CardSlant = 0.16f;         // the plate's lean (the start menu's name plate's, half the headings')

        // ---- The boss's entrance, made in code on its own model (no clip of its own fits: work order 130's report) ----
        // From the start of the hold: it turns to face the camera (the viewer: its front shown while the card is up, not its side or its
        // back), gathers itself, springs up a little and lands; as the camera travels back it turns to face her, and stands as its fight starts.
        public float TurnFrom = 0f;             // s into the hold it begins to turn toward the camera
        public float TurnSeconds = 0.7f;        // s the turn takes (the turn to her takes the travel back)
        public float CrouchFrom = 0.55f;        // s into the hold it begins to gather itself
        public float CrouchSeconds = 0.35f;     // s it takes to crouch (ends as it springs)
        public float CrouchSquash = 0.9f;       // its height in the crouch, a share of its own (its slam's crouch is 0.8)
        public float LeapSeconds = 0.45f;       // s in the air (it springs as the crouch ends)
        public float LeapHeight = 0.4f;         // m it rises (its slam's arc is far higher)
        public float SpringShare = 0.3f;        // of the leap it takes to stand up from the crouch to its full height
        public float LandSeconds = 0.22f;       // s it gives at the knees as it lands
        public float LandSquash = 0.94f;        // its height at the deepest of the landing

        /// <summary>The whole entrance: the travel in, the hold, the travel back.</summary>
        public float Total => Math.Max(0f, TravelInSeconds) + Math.Max(0f, HoldSeconds) + Math.Max(0f, TravelBackSeconds);
    }

    /// <summary>
    /// The one dry line under the boss's name (work order 130): the producer's first drafts, to be judged by Kristian, who may strike
    /// and add lines here. One is drawn a run from the run's seed (the list instead of one line is the producer's addition: one joke read
    /// every run dies by the third run). Deadpan: plain fact, no exclamation mark, no wink. The boss is the Crypt Knight
    /// (<see cref="BossValues.Title"/>), a knight: "he" fits, and the words are the drafts' own.
    /// </summary>
    public static class BossEntranceLines
    {
        public static string[] Lines =
        {
            "Has guarded this room for four hundred years. Nobody has asked him to.",
            "He prepared a speech for this moment. He has forgotten it.",
            "The exit is behind him. He would like you to know that.",
            "Employee of the month, 1387 to present.",
            "He is not angry. He is disappointed. He is also angry.",
        };

        const ulong Salt = 0x130B055E17A9CE01UL; // the draw's own, so it is not the floors' or the crits' from the same seed

        /// <summary>The index of the line a seed draws from a list this long; -1 for an empty list.</summary>
        public static int IndexFor(ulong seed, int count)
        {
            if (count <= 0) return -1;
            ulong z = seed ^ Salt; // SplitMix64's finaliser: every bit of the seed reaches the draw
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z % (ulong)count);
        }

        /// <summary>The line a seed draws from a list; "" for none.</summary>
        public static string For(ulong seed, string[] lines)
        {
            int i = IndexFor(seed, lines != null ? lines.Length : 0);
            return i >= 0 ? lines[i] ?? "" : "";
        }
    }

    /// <summary>The boss's body at a moment of its entrance (work order 130): its turn toward her, its lift and its height, as shares.</summary>
    public readonly struct EntrancePose
    {
        public readonly float Turn;    // 0 as it stood, 1 facing the camera
        public readonly float ToHer;   // 0 as the turn left it, 1 facing her
        public readonly float Lift;    // m off the floor
        public readonly float Height;  // its height, a share of its own (1 standing)

        public EntrancePose(float turn, float toHer, float lift, float height)
        {
            Turn = turn;
            ToHer = toHer;
            Lift = lift;
            Height = height;
        }

        /// <summary>The pose its fight starts from: facing her, on the floor, at its own height.</summary>
        public static EntrancePose Fight => new EntrancePose(1f, 1f, 0f, 1f);

        public bool IsFightPose => Math.Abs(ToHer - 1f) < 1e-6f && Math.Abs(Lift) < 1e-6f && Math.Abs(Height - 1f) < 1e-6f;
    }

    /// <summary>The boss's entrance as plain rules of time (work order 130): which part it is in, where the camera is, the card, the boss's body.</summary>
    public static class BossEntranceRules
    {
        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>Smoothstep, or the share itself when not eased.</summary>
        public static float Ease(float share, bool eased)
        {
            float s = Clamp01(share);
            return eased ? s * s * (3f - 2f * s) : s;
        }

        /// <summary>The part of the entrance at <paramref name="t"/> seconds in.</summary>
        public static EntrancePhase PhaseAt(float t, BossEntranceValues v)
        {
            if (v == null || t < 0f) return EntrancePhase.None;
            float a = Math.Max(0f, v.TravelInSeconds), b = a + Math.Max(0f, v.HoldSeconds), c = b + Math.Max(0f, v.TravelBackSeconds);
            return t < a ? EntrancePhase.TravelIn : t < b ? EntrancePhase.Hold : t < c ? EntrancePhase.TravelBack : EntrancePhase.Done;
        }

        /// <summary>Seconds into the hold (negative before it).</summary>
        public static float IntoHold(float t, BossEntranceValues v) => t - Math.Max(0f, v.TravelInSeconds);

        /// <summary>Where the camera is between her (0) and the boss (1).</summary>
        public static float CameraShare(float t, BossEntranceValues v)
        {
            switch (PhaseAt(t, v))
            {
                case EntrancePhase.TravelIn: return Ease(v.TravelInSeconds > 0f ? t / v.TravelInSeconds : 1f, v.Eased);
                case EntrancePhase.Hold: return 1f;
                case EntrancePhase.TravelBack:
                    float back = t - Math.Max(0f, v.TravelInSeconds) - Math.Max(0f, v.HoldSeconds);
                    return 1f - Ease(v.TravelBackSeconds > 0f ? back / v.TravelBackSeconds : 1f, v.Eased);
                default: return 0f;
            }
        }

        /// <summary>The camera's distance at a share: the play camera's at 0, <see cref="BossEntranceValues.CloseDistance"/> at 1.</summary>
        public static float Distance(float share, float play, BossEntranceValues v) => play + (v.CloseDistance - play) * Clamp01(share);

        /// <summary>How much the card shows, 0 to 1: during the hold only, in after its delay and out as the hold ends.</summary>
        public static float CardAlpha(float t, BossEntranceValues v)
        {
            if (PhaseAt(t, v) != EntrancePhase.Hold) return 0f;
            float h = IntoHold(t, v);
            float fadeIn = v.CardFadeIn > 0f ? Clamp01((h - v.CardDelay) / v.CardFadeIn) : (h >= v.CardDelay ? 1f : 0f);
            float fadeOut = v.CardFadeOut > 0f ? Clamp01((v.HoldSeconds - h) / v.CardFadeOut) : 1f;
            return Math.Min(fadeIn, fadeOut);
        }

        /// <summary>When the boss's entrance in the hold is over, in seconds into the hold: it stands facing the camera from then, and turns to her in the travel back.</summary>
        public static float MotionEnds(BossEntranceValues v) =>
            Math.Max(v.TurnFrom + v.TurnSeconds, v.CrouchFrom + v.CrouchSeconds + v.LeapSeconds + v.LandSeconds);

        /// <summary>
        /// The boss's body at <paramref name="t"/> seconds into the entrance: during the travel in as it stood; in the hold it turns
        /// toward the camera, gathers itself, springs up and lands; in the travel back it turns to face her; after the entrance its
        /// fight's pose.
        /// </summary>
        public static EntrancePose PoseAt(float t, BossEntranceValues v)
        {
            EntrancePhase phase = PhaseAt(t, v);
            if (phase == EntrancePhase.None || phase == EntrancePhase.TravelIn) return new EntrancePose(0f, 0f, 0f, 1f);
            if (phase == EntrancePhase.TravelBack)
            {
                float back = t - Math.Max(0f, v.TravelInSeconds) - Math.Max(0f, v.HoldSeconds);
                return new EntrancePose(1f, Ease(v.TravelBackSeconds > 0f ? back / v.TravelBackSeconds : 1f, true), 0f, 1f);
            }
            if (phase != EntrancePhase.Hold) return EntrancePose.Fight;
            float h = IntoHold(t, v);
            float turn = Ease(v.TurnSeconds > 0f ? (h - v.TurnFrom) / v.TurnSeconds : (h >= v.TurnFrom ? 1f : 0f), true);
            float leapFrom = v.CrouchFrom + v.CrouchSeconds, landFrom = leapFrom + v.LeapSeconds, landEnds = landFrom + v.LandSeconds;
            float height = 1f, lift = 0f;
            if (h >= v.CrouchFrom && h < leapFrom)
                height = 1f - (1f - v.CrouchSquash) * Ease(v.CrouchSeconds > 0f ? (h - v.CrouchFrom) / v.CrouchSeconds : 1f, true);
            else if (h >= leapFrom && h < landFrom)
            {
                float s = v.LeapSeconds > 0f ? (h - leapFrom) / v.LeapSeconds : 1f;
                lift = Math.Max(0f, v.LeapHeight) * 4f * s * (1f - s);
                height = v.CrouchSquash + (1f - v.CrouchSquash) * Ease(v.SpringShare > 0f ? s / v.SpringShare : 1f, true);
            }
            else if (h >= landFrom && h < landEnds)
            {
                float s = v.LandSeconds > 0f ? (h - landFrom) / v.LandSeconds : 1f;
                height = 1f - (1f - v.LandSquash) * (float)Math.Sin(Math.PI * s);
            }
            return new EntrancePose(turn, 0f, lift, height);
        }

        // ---------------- The card's place, in layout pixels of a view 1080 high ----------------

        /// <summary>The plate the boss's name is on, centred across the view.</summary>
        public static UiBox NamePlate(float viewWidth, BossEntranceValues v) =>
            new UiBox((viewWidth - v.NamePlateWidth) * 0.5f, v.CardTop, v.NamePlateWidth, v.NamePlateHeight);

        /// <summary>The line's box, centred under the plate.</summary>
        public static UiBox LineBox(float viewWidth, BossEntranceValues v)
        {
            UiBox plate = NamePlate(viewWidth, v);
            float w = Math.Min(v.LineWidth, viewWidth - 80f);
            return new UiBox((viewWidth - w) * 0.5f, plate.Bottom + v.LineGap, w, v.LineSize * 1.5f);
        }

        /// <summary>Everything the card covers, its lean, outline and stroke included (for the overlap checks).</summary>
        public static UiBox Whole(float viewWidth, BossEntranceValues v, InterfaceValues ui)
        {
            UiBox plate = NamePlate(viewWidth, v), line = LineBox(viewWidth, v);
            float o = Math.Max(0f, ui.HeavyOutline) + Math.Max(0f, ui.OuterStroke) + Math.Max(0f, ui.Depth) * 2f;
            float lean = InterfacePlate.HalfLean(plate.H, v.CardSlant);
            float left = Math.Min(plate.X - lean, line.X) - o, right = Math.Max(plate.Right + lean, line.Right) + o;
            return new UiBox(left, plate.Y - o, right - left, line.Bottom - plate.Y + o);
        }

        /// <summary>
        /// How tall the boss stands on the screen in the close shot, as a share of the screen's height, its feet at the middle (the
        /// camera looks at its feet): <see cref="CameraAngles.HeightShare"/> at the close distance.
        /// </summary>
        public static double BossShare(float pitch, float fieldOfView, float height, BossEntranceValues v) =>
            CameraAngles.HeightShare(pitch, fieldOfView, v.CloseDistance, height);
    }
}
