using System;

namespace Roguelike.Rules
{
    /// <summary>How a damage number is drawn in the kinetic numbers (work order 064).</summary>
    public enum HitNumberLook
    {
        /// <summary>As the interface's skin drew it before (marks spent, a dazed target, hers, a heal).</summary>
        AsBefore,
        /// <summary>An ordinary hit: white and small.</summary>
        Plain,
        /// <summary>A crit: larger, hot teal, with a hard slanted stroke under it of its own.</summary>
        Crit,
        /// <summary>A clean press's or a perfect release's hit: the largest, on a short slanted teal plate, black.</summary>
        Top,
    }

    /// <summary>
    /// The damage numbers in the style of the kinetic anime (work order 064, Kristian, 2026-11-21: "syntes effekter og hvordan
    /// skadetall dukker opp også kan være mere i stil med kinetisk anime. skal være litt dopamin hit"; the type sheet and the
    /// first kinetic concept): the heavy condensed italic of the interface, a number pops (starts larger and snaps to size),
    /// drifts a short way along the slant and is gone soon. Decision 0011's line that numbers are smaller than on the type
    /// sheet holds for ordinary hits; only the ones that matter are large. Sizes in pixels of a view 1080 high, times in
    /// seconds; first guesses with their defaults in code (no asset holds them). Off: the numbers as this morning.
    /// </summary>
    [Serializable]
    public sealed class HitNumberValues
    {
        public bool On = true;
        public int PlainSize = 20;          // px: an ordinary hit, as small as the skin's (InterfaceValues.NumberSize)
        public int CritSize = 30;           // px: a crit
        public int TopSize = 38;            // px: a clean press's, a perfect release's
        public float Pop = 1.5f;            // a number starts this many times its size
        public float PopSeconds = 0.08f;    // and snaps to size in this long
        public float PlainSeconds = 0.5f;   // s an ordinary number lives after its latest hit
        public float CritSeconds = 0.65f;   // a crit's, a little longer
        public float TopSeconds = 0.75f;    // a clean press's or a perfect release's
        public float FadeFrom = 0.6f;       // the share of its life after which it fades
        public float Drift = 26f;           // px it drifts along the slant over its life
        public float LaneStep = 18f;        // px along the slant between the lanes of numbers that start together
        public float Together = 0.05f;      // s: numbers started this close are one crowd, each in its own lane
        public float PlateSlant = 0.32f;    // the plate's and the stroke's lean (the skin's own, InterfaceValues.Slant)
        public float PlatePadX = 8f;        // px of plate either side of the number
        public float StrokeHeight = 4f;     // px: the crit's stroke
    }

    /// <summary>The rules of the kinetic numbers (work order 064), with no engine in them.</summary>
    public static class HitNumberRules
    {
        /// <summary>How a kind is drawn.</summary>
        public static HitNumberLook LookOf(NumberKind kind)
        {
            switch (kind)
            {
                case NumberKind.Plain: return HitNumberLook.Plain;
                case NumberKind.Crit: return HitNumberLook.Crit;
                case NumberKind.Perfect: return HitNumberLook.Top;
                default: return HitNumberLook.AsBefore;
            }
        }

        /// <summary>A number's size for its kind; <paramref name="asBefore"/> for the kinds drawn as before.</summary>
        public static int Size(NumberKind kind, HitNumberValues v, int asBefore)
        {
            if (v == null || !v.On) return asBefore;
            switch (LookOf(kind))
            {
                case HitNumberLook.Plain: return v.PlainSize;
                case HitNumberLook.Crit: return v.CritSize;
                case HitNumberLook.Top: return v.TopSize;
                default: return asBefore;
            }
        }

        /// <summary>A number's life after its latest hit: by its kind, or <paramref name="fallback"/> (the feedback's) with the switch off and for the kinds drawn as before.</summary>
        public static float Life(NumberKind kind, HitNumberValues v, float fallback)
        {
            if (v == null || !v.On) return fallback;
            switch (LookOf(kind))
            {
                case HitNumberLook.Plain: return v.PlainSeconds;
                case HitNumberLook.Crit: return v.CritSeconds;
                case HitNumberLook.Top: return v.TopSeconds;
                default: return fallback;
            }
        }

        /// <summary>The size factor <paramref name="age"/> s after it appeared: the pop, snapping down to 1.</summary>
        public static float Pop(float age, HitNumberValues v) => FeedbackRules.PopScale(age, v.Pop, v.PopSeconds);

        /// <summary>
        /// Where it is <paramref name="age"/> s into a life of <paramref name="life"/> s, from its place: along the slant (up
        /// and to the right, as the italic leans), quick at first and settling (dx, dy in layout pixels, y down).
        /// </summary>
        public static void DriftAt(float age, float life, HitNumberValues v, out float dx, out float dy)
        {
            float k = life <= 0f ? 1f : Math.Max(0f, Math.Min(1f, age / life));
            float eased = 1f - (1f - k) * (1f - k);
            Along(v.Drift * eased, v.PlateSlant, out dx, out dy);
        }

        /// <summary>The lane's offset along the slant: 0, then one step either way in turn (1 up, 2 down, 3 two up ...).</summary>
        public static void LaneOffset(int lane, HitNumberValues v, out float dx, out float dy)
        {
            if (lane <= 0)
            {
                dx = dy = 0f;
                return;
            }
            int steps = (lane + 1) / 2;
            float sign = lane % 2 == 1 ? 1f : -1f;
            Along(sign * steps * v.LaneStep, v.PlateSlant, out dx, out dy);
        }

        /// <summary><paramref name="distance"/> px along the slant: a unit up the lean (slant to the right per unit up).</summary>
        static void Along(float distance, float slant, out float dx, out float dy)
        {
            float len = (float)Math.Sqrt(1f + slant * slant);
            dx = distance * slant / len;
            dy = -distance / len;
        }
    }
}
