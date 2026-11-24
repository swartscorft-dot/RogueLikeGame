using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The look of the Laser Sight's beam (work order 102), as numbers: what Kristian asked for on 2026-11-24, having played with
    /// the part fitted ("bare en liten rød laser aktig stråle, trenger ikke å peke hele veien ... vil at de skal følge våpenets
    /// retning hele tiden. men igjen, bare en tyn rød laser stråle. gjør gjerne litt research på vårdan laser oppfører seg når
    /// det peker i tåke og støv/røyk"), and the research notes for the order (runner\research\laser-in-fog\notes.md). Every
    /// value is a first guess he tunes by playing; the producer's starts are named where they are his.
    /// </summary>
    [Serializable]
    public sealed class LaserBeamValues
    {
        // The beam itself (the order's points 3 and 4).
        public Rgb Colour = new Rgb(1f, 0.08f, 0.06f);   // a real sight's red (the producer's start), not her orange
        public float Width = 0.012f;                     // m across at the muzzle (the producer's start; the notes: "a few millimetres at the gun")
        public float EndWidth = 0.016f;                  // m across at its far end: "widening very slightly" (the notes, point 8)
        public float LeastPixels = 1.3f;                 // the drawn width is never under this many pixels of the picture, its light spread thinner to match, so a beam thinner than a pixel does not break into dashes
        public float Length = 4f;                        // m: fully gone this far out (the producer's start); what it lands on short of it stops it
        public float FadePower = 1.6f;                   // the fade's shape: its light at a share s of the length is (1 - s) to this power; 1 a straight fade, more fades sooner
        public float Glow = 0.26f;                       // its light at the muzzle in clear air: dim, under the bloom (its threshold is 1.2), not the first thing the eye finds

        // A little real (the notes' points 2 to 6): faint in clear air, brighter in the room's low fog, uneven, drifting.
        public float FogGlow = 0.45f;                    // light added at the muzzle where the fog is full (the notes, point 2: particles make it seen)
        public float FogReach = 3f;                      // m from the line the fog is laid along (the edge of the room's open floor, RoomMood) where it is gone
        public float FogFull = 0.6f;                     // m from that line within which it is full
        public float FogLow = 0.5f;                      // m over the floor up to which the fog is full (it lies low: MoodValues.FogHeight is 0.3)
        public float FogTop = 2f;                        // m over the floor where it is gone, thinning between: her guns are held at 1.06 to 1.49 m, so a beam at their height by a wall is in about half of it
        public float WispDepth = 0.4f;                   // how much of the fog's part comes and goes in wisps (the notes, point 4), 0 to 1
        public float WispAir = 0.25f;                    // and of the clear air's part (a haze is never even either)
        public float WispSize = 0.9f;                    // m: the wisps' size along the air
        public float WispDrift = 0.22f;                  // m/s: how fast the air moves the wisps (slow)

        // Dust motes (the notes, point 3: few particles give separate bright points that come and go).
        public int Specks = 6;                           // motes at a time on each beam
        public float SpeckSize = 0.022f;                 // m across
        public float SpeckGlow = 0.85f;                  // a mote's light at its brightest, at the muzzle (still under the bloom)
        public float SpeckLifeLeast = 0.9f;              // s a mote takes to drift through the beam, the shortest
        public float SpeckLifeMost = 2.2f;               // and the longest
        public float SpeckSeen = 0.3f;                   // the share of its drift a mote is in the beam and lit (it winks out either side)
        public float SpeckDrift = 0.05f;                 // m a mote drifts along the beam while it is lit

        // The dot where it lands (the order's point 6): built, off.
        public bool DotOn = false;                       // the producer's choice: a dot dancing over the floor and the enemies as her arms swing would be the dominating thing he does not want
        public float DotSize = 0.03f;                    // m across
        public float DotGlow = 0.7f;                     // its light landing at the muzzle
        public float DotFade = 0.6f;                     // the share of that it has lost landing at the full length (the notes, point 5: the dot is weaker too)

        // What stops it (the order's point 2).
        public float BodyTop = 2f;                       // m: an enemy's body is taken as standing from the floor up to this, as wide as its radius (they carry no collider)
    }

    /// <summary>
    /// The beam's light along its length, the fog's share at a point, the wisps and the motes (work order 102). Plain numbers,
    /// no engine: the drawing (<c>LaserSight</c>) asks these each frame and allocates nothing.
    /// </summary>
    public static class LaserBeam
    {
        /// <summary>The fade at a share <paramref name="s"/> of the full length: 1 at the muzzle, 0 at the end, (1 - s) to <see cref="LaserBeamValues.FadePower"/>.</summary>
        public static float Fade(float s, LaserBeamValues v)
        {
            if (s <= 0f) return 1f;
            if (s >= 1f) return 0f;
            return (float)Math.Pow(1f - s, Math.Max(0.01f, v.FadePower));
        }

        /// <summary>The dot's light landing at a share <paramref name="s"/> of the full length; 0 past it.</summary>
        public static float DotLight(float s, LaserBeamValues v) => s < 0f || s > 1f ? 0f : v.DotGlow * (1f - Clamp01(v.DotFade) * s);

        /// <summary>The drawn width at a share <paramref name="s"/> of the full length, in metres, before the least pixels.</summary>
        public static float WidthAt(float s, LaserBeamValues v) => v.Width + (v.EndWidth - v.Width) * Clamp01(s);

        /// <summary>
        /// The width drawn for a true width and the size of a pixel there (m), and the share of light that keeps its total: a
        /// beam narrower than <see cref="LaserBeamValues.LeastPixels"/> is drawn that wide and that much dimmer.
        /// </summary>
        public static float Drawn(float width, float pixel, LaserBeamValues v, out float lightShare)
        {
            float least = Math.Max(0f, v.LeastPixels) * Math.Max(0f, pixel);
            if (width >= least || least <= 0f)
            {
                lightShare = 1f;
                return width;
            }
            lightShare = width / least;
            return least;
        }

        /// <summary>
        /// The fog's share at a point, 0 to 1: full within <see cref="LaserBeamValues.FogFull"/> of the line it is laid along and
        /// low down, gone at <see cref="LaserBeamValues.FogReach"/> from it and at <see cref="LaserBeamValues.FogTop"/>, smooth
        /// between. <paramref name="fromLine"/> is the distance on the ground to that line (either side), <paramref name="height"/>
        /// over the floor.
        /// </summary>
        public static float FogAt(float fromLine, float height, LaserBeamValues v)
        {
            float across = 1f - Smooth(v.FogFull, v.FogReach, Math.Abs(fromLine));
            float up = 1f - Smooth(v.FogLow, v.FogTop, height);
            return across * up;
        }

        /// <summary>
        /// How far a ground point lies from the edge of a level rectangle (its middle <paramref name="cx"/>, <paramref name="cz"/>,
        /// its half sizes <paramref name="hx"/>, <paramref name="hz"/>), inside or out: the room's fog is laid along that edge.
        /// </summary>
        public static float FromEdge(float x, float z, float cx, float cz, float hx, float hz)
        {
            float dx = Math.Abs(x - cx) - hx, dz = Math.Abs(z - cz) - hz;
            if (dx <= 0f && dz <= 0f) return Math.Min(-dx, -dz); // inside: to the nearer side
            float ox = Math.Max(dx, 0f), oz = Math.Max(dz, 0f);
            return (float)Math.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>
        /// The wisps at a point of the air, 0 to 1, at a time: a slow uneven pattern fixed in the air (not on the beam, so a beam
        /// swung through it brightens and dims where the air does), drifting at <see cref="LaserBeamValues.WispDrift"/>.
        /// </summary>
        public static float Wisp(float x, float y, float z, float time, LaserBeamValues v)
        {
            float k = 1f / Math.Max(0.05f, v.WispSize), t = time * v.WispDrift * k;
            double a = Math.Sin((x * 0.83 + z * 0.56) * k + t * 1.0);
            double b = Math.Sin((x * -0.41 + z * 0.91 + y * 0.7) * k * 1.73 - t * 0.77 + 1.9);
            double c = Math.Sin((x * 0.29 - z * 0.33 + y * 1.1) * k * 2.91 + t * 1.41 + 4.1);
            return Clamp01((float)(0.5 + 0.5 * (0.55 * a + 0.30 * b + 0.15 * c)));
        }

        /// <summary>
        /// The beam's light at a point along it: the fade at its share <paramref name="s"/> of the full length, times the clear
        /// air's glow and the fog's, each uneven by the wisps there (<paramref name="wisp"/>, 0 to 1).
        /// </summary>
        public static float Light(float s, float fog, float wisp, LaserBeamValues v)
        {
            float air = v.Glow * (1f - v.WispAir + v.WispAir * 2f * wisp);
            float mist = v.FogGlow * Clamp01(fog) * (1f - v.WispDepth + v.WispDepth * 2f * wisp);
            return Fade(s, v) * Math.Max(0f, air + mist);
        }

        /// <summary>
        /// Mote <paramref name="i"/> of a beam (<paramref name="seed"/> tells the two beams apart) at a time: where along the
        /// beam it is, as a share of the beam as far as it reaches (<paramref name="along"/>), and how lit it is, 0 to 1 (<paramref name="lit"/>):
        /// each drifts across the beam in its own time, lit only while it is in it (a bump in the middle of its drift, winking
        /// out either side), then the next mote of its line comes at another place. No allocation.
        /// </summary>
        public static void Speck(int i, int seed, float time, LaserBeamValues v, out float along, out float lit)
        {
            float life = v.SpeckLifeLeast + (v.SpeckLifeMost - v.SpeckLifeLeast) * Hash01(i * 7919 + seed * 104729 + 17);
            life = Math.Max(0.05f, life);
            double phase = time / life + Hash01(i * 15485863 + seed * 32452843 + 3);
            long cycle = (long)Math.Floor(phase);
            float p = (float)(phase - cycle);
            int c = unchecked((int)cycle);
            along = 0.04f + 0.92f * Hash01(c * 2654435 + i * 40503 + seed * 9973 + 11);
            along += (p - 0.5f) * v.SpeckDrift / Math.Max(0.01f, v.Length);
            float x = (p - 0.5f) / Math.Max(0.01f, v.SpeckSeen * 0.5f); // -1 to 1 while in the beam
            lit = x <= -1f || x >= 1f ? 0f : (1f - x * x) * (1f - x * x);
        }

        /// <summary>A number from 0 to 1 that looks random for each integer (no allocation, the same every run).</summary>
        public static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n;
                h ^= h >> 16;
                h *= 0x7feb352dU;
                h ^= h >> 15;
                h *= 0x846ca68bU;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// Where a ray from <paramref name="ox"/>, <paramref name="oy"/>, <paramref name="oz"/> along the unit direction
        /// <paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/> first meets an upright body standing on the floor at
        /// <paramref name="bx"/>, <paramref name="bz"/> (a cylinder of <paramref name="radius"/> from 0 up to <paramref name="top"/>),
        /// as a distance along it; false when it does not within <paramref name="most"/>. A ray that starts inside it does not see it.
        /// </summary>
        public static bool HitsBody(float ox, float oy, float oz, float dx, float dy, float dz, float bx, float bz, float radius, float top, float most, out float distance)
        {
            distance = 0f;
            float px = ox - bx, pz = oz - bz;
            float a = dx * dx + dz * dz;
            if (a < 1e-10f || radius <= 0f) return false; // straight up or down: no side to meet
            float b = 2f * (px * dx + pz * dz), c = px * px + pz * pz - radius * radius;
            if (c <= 0f) return false; // starts inside it
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float t = (-b - (float)Math.Sqrt(disc)) / (2f * a);
            if (t < 0f || t > most) return false;
            float y = oy + dy * t;
            if (y < 0f || y > top) return false;
            distance = t;
            return true;
        }

        static float Smooth(float from, float to, float x)
        {
            if (to <= from) return x < from ? 0f : 1f;
            float t = Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
