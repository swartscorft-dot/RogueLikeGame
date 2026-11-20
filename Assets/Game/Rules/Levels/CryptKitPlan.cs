using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>A colour as plain numbers, red, green and blue from 0 to 1 as picked (sRGB), for values kept without the engine.</summary>
    [Serializable]
    public struct Rgb
    {
        public float R, G, B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", R, G, B);
    }

    /// <summary>
    /// The numbers of the crypt wall kit that dresses a generated floor (work order 016, 2026-11-20). All first guesses,
    /// kept together here with their defaults in code. None of them is read by the generator: changing one changes how a
    /// floor looks and nothing else. Lengths in metres.
    /// </summary>
    [Serializable]
    public sealed class CryptKitValues
    {
        // Where the pieces go (the plan, drawn from the floor's seed on the stream "crypt kit")
        public float NicheShare = 0.2f;          // of the full-height sections, about this share are the one with the niche, toward a room
        public float LanternShare = 1f / 3f;     // of the faces of full-height sections toward a room, about this share hold a lantern
        public float LanternHeight = 2.2f;       // the lantern's pivot (the back of its plate, on the wall) over the floor
        public bool NicheFacesPlusZ = false;     // the niche is on the model's -Z face as Unity imports it (the Blender job believed +Z; the import test reads it from the mesh)

        // Doors
        public bool ArchToGap = true;            // stretch the arch along the wall so its clear opening is as wide as the gap the wall boxes leave
        public float ArchOpeningShare = 3.10f / 4.00f; // the arch's clear opening at the floor, as a share of its length (by construction)

        // Floors
        public float FloorTile = 4f;             // one copy of a floor texture covers this many metres on a side
        public Rgb EntranceFloorTint = new Rgb(0.91f, 1f, 0.93f);  // the old grey-box tints, each brought most of the way to white
        public Rgb CombatFloorTint = new Rgb(1f, 0.98f, 0.96f);
        public Rgb TreasureFloorTint = new Rgb(0.87f, 0.92f, 1f);
        public Rgb ExitFloorTint = new Rgb(1f, 0.87f, 0.87f);
        public Rgb EliteFloorTint = new Rgb(1f, 0.94f, 0.86f);     // work order 017: the elite room's, a faint warm gold

        // Stone
        public Rgb PaleStoneTint = new Rgb(0.94f, 0.85f, 0.83f);   // on the pier and the arch, modelled by script and paler and greyer than the walls

        // Glow: emission = the map x the game's cyan x this, in linear light; the bloom's threshold is 1.2 (about 1.5 linear)
        public Rgb Cyan = new Rgb(0.25f, 0.85f, 0.85f);           // the game's cyan (the HUD's accent; the emission maps are painted in it too)
        public float StoneGlow = 3f;             // walls, low wall, pier, post and arch: the brightest texels of their maps just reach the bloom
        public float LanternGlow = 5f;           // the lanterns' crystals: they bloom

        // Lights (optional, off by default: see the work order's report)
        public bool LanternLights = false;       // small cyan point lights at lanterns, without shadows, on only in the room she is in
        public int LightsPerRoom = 2;
        public float LightRange = 5f;
        public float LightIntensity = 1.5f;
        public float LightOut = 0.5f;            // from the wall's face into the room
    }

    /// <summary>
    /// One 4 m wall section of the kit: on a vertical line (x = <see cref="Line"/>, the cell y = <see cref="Cell"/>) or a
    /// horizontal one (y = Line, x = Cell). <see cref="Facing"/> is the side its front (the model's +Z, the niche's face)
    /// is turned to: +1 east or north, -1 west or south. <see cref="RoomMinus"/> and <see cref="RoomPlus"/> are the rooms
    /// on its west or south and its east or north side, by their place in the run, -1 for none.
    /// </summary>
    public sealed class CryptKitSection
    {
        public bool Vertical;
        public int Line, Cell;
        public bool Low;
        public bool Niche;
        public int Facing;
        public int RoomMinus, RoomPlus;
        /// <summary>A door is in the cell before or after it on the same line.</summary>
        public bool AtDoor;

        public int RoomOn(int side) => side < 0 ? RoomMinus : RoomPlus;
    }

    /// <summary>A door arch filling one door cell of a full-height line.</summary>
    public sealed class CryptKitArch
    {
        public bool Vertical;
        public int Line, Cell;
        /// <summary>The door's place in the layout's list of doors.</summary>
        public int Door;
    }

    /// <summary>A grid point (whole cells) with a pier (a full-height wall touches it) or a post (only low walls do).</summary>
    public struct CryptKitPoint
    {
        public int X, Y;
        public bool Pier;
    }

    /// <summary>A lantern on one face of a full-height section: the section's place in the plan's list, the side (+1 or -1) and the room it faces.</summary>
    public sealed class CryptKitLantern
    {
        public int Section;
        public int Side;
        public int Room;
    }

    /// <summary>
    /// Where the crypt wall kit's pieces go on a floor (work order 016, 2026-11-20), from its layout alone: plain data, no
    /// engine, so the rules can be tested and the level only places what this says.
    ///
    /// The walls are found by the rule the grey boxes are built by (<c>GeneratedLevel.BuildWalls</c>): a cell edge with
    /// a different room (or nothing) on each side and no door is a wall; on a vertical line it is full height; on a
    /// horizontal line it is low when a room is to its north (it is that room's south wall, between the camera and the
    /// room), full height otherwise. Each wall edge is one section: a full one is the plain wall or, drawn, the one with
    /// the niche toward a room; a low one the low wall, its front to the south, toward the camera. A door cell on a full
    /// line gets an arch; one on a low line none (the arch would stand between the camera and the room). Every grid
    /// point a section ends at, and the two ends of every door, gets one pier when a full-height section or arch touches
    /// it, else one post; never a point inside a door wider than a cell.
    ///
    /// From the seed: the stream "crypt kit" of the floor's seed and number (<see cref="LevelRandom"/>), which no other
    /// stage reads, so the layout, props and enemies of every seed are what they were. Every full-height section draws
    /// four numbers in the order the sections are listed, whatever it becomes: the niche (<see cref="CryptKitValues.NicheShare"/>),
    /// which side the niche faces when there is a room on both, and a lantern for its west or south face and for its
    /// east or north face (<see cref="CryptKitValues.LanternShare"/>). A face holds a lantern when its draw says so, a
    /// room is on that side, the section has no niche and no door is next to it on its line; a wall with a room on both
    /// sides can hold one on each face, each for its own room.
    /// </summary>
    public sealed class CryptKitPlan
    {
        public const string Stage = "crypt kit";

        public readonly List<CryptKitSection> Sections = new List<CryptKitSection>();
        public readonly List<CryptKitArch> Arches = new List<CryptKitArch>();
        public readonly List<CryptKitPoint> Points = new List<CryptKitPoint>();
        public readonly List<CryptKitLantern> Lanterns = new List<CryptKitLantern>();

        public static CryptKitPlan Make(LevelLayout layout, CryptKitValues values)
        {
            values = values ?? new CryptKitValues();
            var plan = new CryptKitPlan();
            if (layout.Rooms.Count == 0) return plan;
            LevelRandom rng = LevelRandom.Stream(layout.Seed, Stage, layout.Floor);
            CellRect box = layout.Bounds();

            int Owner(int x, int y) => layout.RoomAtCell(x, y)?.Order ?? -1;
            int DoorAt(bool vertical, int line, int cell)
            {
                for (int i = 0; i < layout.Doors.Count; i++)
                {
                    LevelDoor d = layout.Doors[i];
                    if (d.Vertical == vertical && d.Line == line && cell >= d.From && cell < d.From + d.Width) return i;
                }
                return -1;
            }

            var pointIndex = new Dictionary<long, int>();
            void Touch(int x, int y, bool pier)
            {
                long key = ((long)x << 32) ^ (uint)y;
                if (pointIndex.TryGetValue(key, out int i))
                {
                    if (pier && !plan.Points[i].Pier) plan.Points[i] = new CryptKitPoint { X = x, Y = y, Pier = true };
                    return;
                }
                pointIndex[key] = plan.Points.Count;
                plan.Points.Add(new CryptKitPoint { X = x, Y = y, Pier = pier });
            }

            void Edge(bool vertical, int line, int cell)
            {
                int minus = vertical ? Owner(line - 1, cell) : Owner(cell, line - 1);
                int plus = vertical ? Owner(line, cell) : Owner(cell, line);
                if (minus == plus) return;
                bool full = vertical || plus < 0;
                int door = DoorAt(vertical, line, cell);
                if (door >= 0)
                {
                    if (full) plan.Arches.Add(new CryptKitArch { Vertical = vertical, Line = line, Cell = cell, Door = door });
                    return;
                }
                var s = new CryptKitSection
                {
                    Vertical = vertical, Line = line, Cell = cell, Low = !full, RoomMinus = minus, RoomPlus = plus,
                    AtDoor = DoorAt(vertical, line, cell - 1) >= 0 || DoorAt(vertical, line, cell + 1) >= 0,
                    Facing = full ? (plus >= 0 ? 1 : -1) : -1,
                };
                if (full)
                {
                    bool niche = Chance(rng, values.NicheShare);
                    bool plusSide = Chance(rng, 0.5f);
                    bool lanternMinus = Chance(rng, values.LanternShare);
                    bool lanternPlus = Chance(rng, values.LanternShare);
                    if (minus >= 0 && plus >= 0) s.Facing = plusSide ? 1 : -1;
                    s.Niche = niche;
                    if (!niche && !s.AtDoor)
                    {
                        if (lanternMinus && minus >= 0) plan.Lanterns.Add(new CryptKitLantern { Section = plan.Sections.Count, Side = -1, Room = minus });
                        if (lanternPlus && plus >= 0) plan.Lanterns.Add(new CryptKitLantern { Section = plan.Sections.Count, Side = 1, Room = plus });
                    }
                }
                plan.Sections.Add(s);
                if (vertical)
                {
                    Touch(line, cell, full);
                    Touch(line, cell + 1, full);
                }
                else
                {
                    Touch(cell, line, full);
                    Touch(cell + 1, line, full);
                }
            }

            // In the order the grey boxes are built: vertical lines west to east, each south to north; then horizontal
            // lines south to north, each west to east.
            for (int x = box.X; x <= box.Right; x++)
                for (int y = box.Y; y < box.Top; y++) Edge(true, x, y);
            for (int y = box.Y; y <= box.Top; y++)
                for (int x = box.X; x < box.Right; x++) Edge(false, y, x);

            // The two ends of every door: a pier on a full line, a post on a low one (none inside a wide door).
            foreach (LevelDoor d in layout.Doors)
            {
                bool full = d.Vertical || Owner(d.From, d.Line) < 0;
                if (d.Vertical)
                {
                    Touch(d.Line, d.From, full);
                    Touch(d.Line, d.From + d.Width, full);
                }
                else
                {
                    Touch(d.From, d.Line, full);
                    Touch(d.From + d.Width, d.Line, full);
                }
            }
            return plan;
        }

        /// <summary>True with about this chance: one draw of 53 bits.</summary>
        static bool Chance(LevelRandom rng, float share) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53)) < share;

        public int Niches
        {
            get
            {
                int n = 0;
                foreach (CryptKitSection s in Sections)
                    if (s.Niche) n++;
                return n;
            }
        }

        public int LowSections
        {
            get
            {
                int n = 0;
                foreach (CryptKitSection s in Sections)
                    if (s.Low) n++;
                return n;
            }
        }

        public int Piers
        {
            get
            {
                int n = 0;
                foreach (CryptKitPoint p in Points)
                    if (p.Pier) n++;
                return n;
            }
        }

        /// <summary>Every piece as text: two plans with the same text put the same pieces in the same places.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            foreach (CryptKitSection s in Sections)
                sb.Append(s.Low ? 'l' : s.Niche ? 'n' : 'w').Append(s.Vertical ? 'V' : 'H').Append(s.Line).Append('_').Append(s.Cell)
                  .Append(s.Facing > 0 ? '+' : '-').Append(';');
            foreach (CryptKitArch a in Arches) sb.Append('a').Append(a.Vertical ? 'V' : 'H').Append(a.Line).Append('_').Append(a.Cell).Append(';');
            foreach (CryptKitPoint p in Points) sb.Append(p.Pier ? 'p' : 'o').Append(p.X).Append(',').Append(p.Y).Append(';');
            foreach (CryptKitLantern l in Lanterns) sb.Append('L').Append(l.Section).Append(l.Side > 0 ? '+' : '-').Append(';');
            return sb.ToString();
        }
    }
}
