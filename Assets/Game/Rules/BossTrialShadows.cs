using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>An upright rectangle on the ground (work order 147): a pier or a corner mass of a template, in metres.</summary>
    public readonly struct GroundRect
    {
        public readonly float X0, Z0, X1, Z1;
        public GroundRect(float x0, float z0, float x1, float z1)
        {
            X0 = Math.Min(x0, x1); X1 = Math.Max(x0, x1);
            Z0 = Math.Min(z0, z1); Z1 = Math.Max(z0, z1);
        }
        public bool Contains(float x, float z, float grow = 0f) => x >= X0 - grow && x <= X1 + grow && z >= Z0 - grow && z <= Z1 + grow;
    }

    /// <summary>
    /// Ruin's shadows (work order 147, 7.2: "each pillar casts a pale shadow strip away from the boss"): seen from where it kneels, a
    /// place is in a shadow when the straight line from the boss to it is stopped by a wall or a tall prop first. The game finds the
    /// stopping distance in each of <see cref="BossTrialValues.ShadowSamples"/> directions once (a physics ray each), draws each shadow
    /// from those distances and judges her by them, so the drawn edge is the judged edge. Here also the same from a template's grid,
    /// with no engine, for the measure of both throne rooms (each solid cell a 4 m block).
    /// </summary>
    public static class RuinShadows
    {
        /// <summary>Is she (her middle) in a shadow: farther in her direction than the line from the boss runs clear?</summary>
        public static bool InShadow(float[] clear, GroundPoint boss, GroundPoint her) => BossTrialRules.Sheltered(clear, boss, her);

        /// <summary>The clear distance in each direction from <paramref name="from"/>: to the first block, else to the room's edge.</summary>
        public static float[] Table(GroundPoint from, IReadOnlyList<GroundRect> blocks, GroundRect room, int samples)
        {
            var clear = new float[Math.Max(1, samples)];
            for (int i = 0; i < clear.Length; i++)
            {
                GroundPoint d = BossTrialRules.SampleDirection(i, clear.Length);
                float best = Exit(from, d, room);
                if (blocks != null)
                    foreach (GroundRect b in blocks)
                    {
                        float t = Enter(from, d, b);
                        if (t >= 0f && t < best) best = t;
                    }
                clear[i] = best;
            }
            return clear;
        }

        /// <summary>The distance to the room's edge in each direction (where a shadow's far side is drawn).</summary>
        public static float[] Edges(GroundPoint from, GroundRect room, int samples)
        {
            var edge = new float[Math.Max(1, samples)];
            for (int i = 0; i < edge.Length; i++) edge[i] = Exit(from, BossTrialRules.SampleDirection(i, edge.Length), room);
            return edge;
        }

        /// <summary>m along a ray from a point inside a rectangle to where it leaves it.</summary>
        public static float Exit(GroundPoint p, GroundPoint d, GroundRect r)
        {
            float t = float.PositiveInfinity;
            if (d.X > 1e-6f) t = Math.Min(t, (r.X1 - p.X) / d.X);
            else if (d.X < -1e-6f) t = Math.Min(t, (r.X0 - p.X) / d.X);
            if (d.Z > 1e-6f) t = Math.Min(t, (r.Z1 - p.Z) / d.Z);
            else if (d.Z < -1e-6f) t = Math.Min(t, (r.Z0 - p.Z) / d.Z);
            return float.IsInfinity(t) ? 0f : Math.Max(0f, t);
        }

        /// <summary>m along a ray to where it enters a rectangle, or -1 when it misses (or starts inside).</summary>
        public static float Enter(GroundPoint p, GroundPoint d, GroundRect r)
        {
            float tmin = 0f, tmax = float.PositiveInfinity;
            if (!Slab(p.X, d.X, r.X0, r.X1, ref tmin, ref tmax)) return -1f;
            if (!Slab(p.Z, d.Z, r.Z0, r.Z1, ref tmin, ref tmax)) return -1f;
            return tmin > 0f ? tmin : -1f;
        }

        static bool Slab(float p, float d, float lo, float hi, ref float tmin, ref float tmax)
        {
            if (Math.Abs(d) < 1e-7f) return p >= lo && p <= hi;
            float a = (lo - p) / d, b = (hi - p) / d;
            if (a > b) { float t = a; a = b; b = t; }
            tmin = Math.Max(tmin, a);
            tmax = Math.Min(tmax, b);
            return tmin <= tmax;
        }

        /// <summary>A throne template's solid cells as blocks, in its own metres (x across, z from the door's wall), and its room.</summary>
        public static List<GroundRect> Blocks(RoomTemplate t, float cell, out GroundRect room)
        {
            var blocks = new List<GroundRect>();
            room = new GroundRect(0f, 0f, t.Width * cell, t.Height * cell);
            for (int ay = 0; ay < t.Height; ay++)
                for (int ax = 0; ax < t.Width; ax++)
                    if (t.At(ax, ay) == '#') blocks.Add(new GroundRect(ax * cell, ay * cell, (ax + 1) * cell, (ay + 1) * cell));
            return blocks;
        }

        /// <summary>What the measure of one room found (<see cref="Measure"/>).</summary>
        public sealed class Reach
        {
            /// <summary>m on foot from the worst place within the circle to the nearest shadow, and that place.</summary>
            public float Farthest;
            public GroundPoint Worst;
            /// <summary>Places measured, and of them those a shadow holds already.</summary>
            public int Places, InShadow;
            /// <summary>Shadows (runs of directions stopped short of the room's edge).</summary>
            public int Shadows;
            /// <summary>s she needs from the worst place: <paramref name="react"/> and the walk at her speed.</summary>
            public float Needed;
        }

        /// <summary>
        /// The measure the order asks (work order 147, part 7): from every place she can stand within <paramref name="within"/> m of
        /// the boss, the walk to the nearest place a shadow holds her middle, round the blocks (a grid of <paramref name="grid"/> m, her
        /// body kept <paramref name="herRadius"/> off every block and wall), and the time it needs with <paramref name="react"/> s to
        /// react at <paramref name="speed"/>. She is never inside its body: places nearer it than <paramref name="bodyRadius"/> and her
        /// radius are not hers to stand on.
        /// </summary>
        public static Reach Measure(GroundPoint boss, IReadOnlyList<GroundRect> blocks, GroundRect room, int samples, float within,
                                    float grid, float herRadius, float speed, float react, float bodyRadius = 0f)
        {
            float[] clear = Table(boss, blocks, room, samples);
            int nx = (int)Math.Ceiling((room.X1 - room.X0) / grid), nz = (int)Math.Ceiling((room.Z1 - room.Z0) / grid);
            var walk = new bool[nx * nz];
            var dist = new float[nx * nz];
            var open = new List<int>();
            var r = new Reach();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    float px = room.X0 + (x + 0.5f) * grid, pz = room.Z0 + (z + 0.5f) * grid;
                    bool ok = px - herRadius >= room.X0 && px + herRadius <= room.X1 && pz - herRadius >= room.Z0 && pz + herRadius <= room.Z1;
                    if (ok && new GroundPoint(px, pz).DistanceTo(boss) < bodyRadius + herRadius) ok = false; // its body
                    if (ok && blocks != null)
                        foreach (GroundRect b in blocks)
                            if (b.Contains(px, pz, herRadius)) { ok = false; break; }
                    walk[i] = ok;
                    dist[i] = float.PositiveInfinity;
                    if (ok && InShadow(clear, boss, new GroundPoint(px, pz)))
                    {
                        dist[i] = 0f;
                        open.Add(i);
                    }
                }
            // Dijkstra on the grid's eight ways (a plain list: the grid is small).
            var done = new bool[nx * nz];
            while (open.Count > 0)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++) if (dist[open[k]] < dist[open[bi]]) bi = k;
                int c = open[bi];
                open[bi] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                if (done[c]) continue;
                done[c] = true;
                int cx = c % nx, cz = c / nx;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                        int n = z * nx + x;
                        if (!walk[n] || done[n]) continue;
                        if (dx != 0 && dz != 0 && (!walk[cz * nx + x] || !walk[z * nx + cx])) continue; // no corner cut
                        float step = (dx != 0 && dz != 0 ? 1.41421356f : 1f) * grid;
                        if (dist[c] + step < dist[n])
                        {
                            dist[n] = dist[c] + step;
                            open.Add(n);
                        }
                    }
            }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int i = z * nx + x;
                    if (!walk[i]) continue;
                    var p = new GroundPoint(room.X0 + (x + 0.5f) * grid, room.Z0 + (z + 0.5f) * grid);
                    if (p.DistanceTo(boss) > within) continue;
                    r.Places++;
                    if (dist[i] <= 0f) r.InShadow++;
                    if (dist[i] > r.Farthest)
                    {
                        r.Farthest = dist[i];
                        r.Worst = p;
                    }
                }
            r.Shadows = Runs(clear, Edges(boss, room, samples), 0.5f);
            r.Needed = react + (speed > 0f ? r.Farthest / speed : float.PositiveInfinity);
            return r;
        }

        /// <summary>The shadows: runs of directions in which the line stops more than <paramref name="depth"/> short of the room's edge.</summary>
        public static int Runs(float[] clear, float[] edge, float depth)
        {
            if (clear == null || edge == null || clear.Length != edge.Length || clear.Length == 0) return 0;
            int n = clear.Length, runs = 0;
            for (int i = 0; i < n; i++)
            {
                bool now = edge[i] - clear[i] > depth;
                bool before = edge[(i + n - 1) % n] - clear[(i + n - 1) % n] > depth;
                if (now && !before) runs++;
            }
            if (runs == 0 && edge[0] - clear[0] > depth) runs = 1; // all round
            return runs;
        }
    }
}
