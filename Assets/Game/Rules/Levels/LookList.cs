using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 112, the look list of the light check (work order 091's list, lines 44 to 53 of its pictures.txt). Kristian,
    /// 2026-11-24, ticked "Utseende og stemning" for the day's sprint with the plan's row: "Blokkene med svarte topper som leses som
    /// hull, den kalde mørke midten i ossuariet, og den blågrønne første gangen mellom varme rom"; he had approved the lamplit light
    /// at level C in the nave by playing (work order 085) and had the entrance made "Varmt som de andre" (work order 100).
    /// One switch for the three (<see cref="On"/>), and a named one for each under it. Off: every floor, plan and light as before,
    /// value for value. The values are first guesses, judged from the order's pictures; he tunes by playing. Read when a floor's
    /// plans and lamps are made and when the light's level changes, never every frame. Plain data: no engine.
    /// </summary>
    public static class LookListValues
    {
        /// <summary>The order's switch (Kristian, 2026-11-24, ticked "Utseende og stemning": the three faults of the light check put right). Off: as before.</summary>
        public static bool On = true;

        // 1. The tops of the solid blocks and of the walls' piers (091: "flat black tops that read as holes in the floor rather than masses")
        public static bool LitTops = true;          // a lid on each top that catches the lamps' light as the floor beside it does, a step darker, with a lighter rim
        public static float TopShare = 0.5f;        // the lid's top: this share of the light the floor under it would have (a step darker than the floor)
        public static float RimShare = 1.1f;        // its outer edge: this share (the lighter rim), fading to the top's share across RimWidth
        public static float RimWidth = 0.2f;        // m
        public static float TopBase = 0.9f;         // the light a lid has with no lamp near, times the level's ambient share (C's 0.07 gives 0.063): about the floor's own base light
        public static float TopMost = 1.2f;         // a lid's light is never more than this (the floor right under a lamp is brighter still)
        public static float TopGrid = 0.5f;         // m between the lid's points that carry the light (the light is worked out at each and blended between)
        public static float TopTile = 4f;           // m one copy of the floor's stone covers on a lid (the kit's FloorTile)
        public static Rgb TopTint = new Rgb(0.92f, 0.88f, 0.84f);   // the lid's stone: the floor's texture, a little greyer (a block, not a piece of floor)
        public static Rgb PierTint = new Rgb(0.94f, 0.85f, 0.83f);  // a pier's lid: the kit's pale stone of the pier (CryptKitValues.PaleStoneTint)
        public static float LidLift = 0.01f;        // m a lid lies over the black cap it covers
        public static float RaisedMargin = 0.3f;    // m: a pier stacked higher than its own top by more than this (up a tall far wall of the dark look, fading into the dark) gets no lid
        public static float PostsFrom = 2.4f;       // m: a door's post that stands this high or higher gets a lid (the lamps hang lower: sconces 2.3, lanterns 2.4); a lower one the lamps light

        // 2. The ossuary's middle (091: "the middle of the room is a cold blue-grey, the darkest of the theme rooms")
        public static bool OssuaryCandles = true;   // sets of candles at the ossuary's props nearest its middle, as the other themes have at theirs
        public static int CandleSets = 3;           // at most this many sets in one ossuary
        public static float CandleReach = 5.5f;       // m: only at a prop whose edge is this near the room's middle, and only where the set itself is this near it (a candle's light reaches 6 m)
        public static float CandleGap = 0.3f;       // m from the prop's edge, on its side toward the middle
        public static float CandleSpacing = 2.5f;   // m between two sets of candles
        public static float CandleFromPlaces = 1.0f; // m from her start and every enemy's place
        public static float CandleFromDoors = 1.5f; // m from a door's opening (as every set of candles of the mood)

        // 3. The passages between two warm rooms (100's report: "a cool teal strip between two warm rooms")
        public static bool WarmPassages = true;     // a warm sconce of the kit's on a passage's far wall, its teal lanterns turned down to accents
        public static float LanternShare = 0.5f;    // a passage's lanterns light at this share of a lantern's own
        public static float FloorPerSconce = 60f;   // m² of a passage's floor for each warm sconce it gets (at least one)
        public static float SconceSpacing = 6f;     // m between two warm sconces of one passage
        public static float SconceAlong = 1.3f;     // m along its wall from the section's middle, when a lantern hangs there

        public static bool Tops => On && LitTops;
        public static bool Candles => On && OssuaryCandles;
        public static bool Passages => On && WarmPassages;

        /// <summary>Every value back to the order's first guesses (for tests).</summary>
        public static void Reset()
        {
            On = LitTops = OssuaryCandles = WarmPassages = true;
            TopShare = 0.5f; RimShare = 1.1f; RimWidth = 0.2f; TopBase = 0.9f; TopMost = 1.2f; TopGrid = 0.5f; TopTile = 4f;
            TopTint = new Rgb(0.92f, 0.88f, 0.84f); PierTint = new Rgb(0.94f, 0.85f, 0.83f); LidLift = 0.01f; RaisedMargin = 0.3f; PostsFrom = 2.4f;
            CandleSets = 3; CandleReach = 5.5f; CandleGap = 0.3f; CandleSpacing = 2.5f; CandleFromPlaces = 1.0f; CandleFromDoors = 1.5f;
            LanternShare = 0.5f; FloorPerSconce = 60f; SconceSpacing = 6f; SconceAlong = 1.3f;
        }
    }

    /// <summary>
    /// Work order 112's rules for the lamps (<see cref="LampPlan"/>): a warm main light in every passage, and work order 091's
    /// measure of the lamps' light on the floor, for the tests and the report.
    /// </summary>
    public static class LookListLamps
    {
        /// <summary>
        /// A warm main light in every passage (<see cref="LookListValues.WarmPassages"/>): its lanterns light at
        /// <see cref="LookListValues.LanternShare"/> (teal accents still), and it gets warm sconces until it has one for every
        /// <see cref="LookListValues.FloorPerSconce"/> of its floor (at least one; the sconces the room rule already gave it count):
        /// on the face toward it of a full far section of its own (never a near lip, a niche or a section by a door), the first at
        /// the place nearest the middle of its floor, each next the farthest from the others and at least
        /// <see cref="LookListValues.SconceSpacing"/> from them; where a lantern hangs on that face, beside it,
        /// <see cref="LookListValues.SconceAlong"/> along the wall. Nothing else of the plan changes.
        /// </summary>
        public static void WarmPassages(LampPlan plan, LevelLayout layout, CryptKitPlan kit, LampValues v)
        {
            if (plan == null || layout == null || kit == null) return;
            double cs = layout.CellSize;
            foreach (LevelRoom room in layout.Rooms)
            {
                if (room.Kind != RoomKind.Passage) continue;
                var warm = new List<Lamp>();
                foreach (Lamp l in plan.Lamps)
                {
                    if (l.Room != room.Order) continue;
                    if (l.Kind == LampKind.Lantern) l.Share = LookListValues.LanternShare;
                    else if (l.Kind == LampKind.Sconce) warm.Add(l);
                }
                List<LevelPoint> floor = LampPlan.FloorPoints(layout, room);
                if (floor.Count == 0) continue;
                double mx = 0, my = 0;
                foreach (LevelPoint p in floor) { mx += p.X; my += p.Y; }
                var middle = new LevelPoint(mx / floor.Count, my / floor.Count);
                double square = cs / per(cs), area = floor.Count * square * square; // FloorPoints: a point a square metre (a cell's side in whole metres)
                int wanted = Math.Max(1, (int)Math.Round(area / Math.Max(1f, LookListValues.FloorPerSconce)));

                var places = new List<Lamp>();
                for (int i = 0; i < kit.Sections.Count; i++)
                {
                    CryptKitSection s = kit.Sections[i];
                    if (s.Low || s.Niche || s.AtDoor || CaveWalls.IsNear(s, CaveWalls.GameYaw)) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (s.RoomOn(side) != room.Order) continue;
                        LevelPoint face = LampPlan.OnFace(s, side, cs, v.WallFace + v.SconceOut);
                        if (!HasLantern(kit, i, side))
                            places.Add(new Lamp { Room = room.Order, Kind = LampKind.Sconce, At = face, Height = v.SconceHeight, Section = i, Side = side, ByLookList = true });
                        else
                            foreach (int k in new[] { -1, 1 })
                            {
                                double along = k * LookListValues.SconceAlong;
                                LevelPoint at = s.Vertical ? new LevelPoint(face.X, face.Y + along) : new LevelPoint(face.X + along, face.Y);
                                places.Add(new Lamp { Room = room.Order, Kind = LampKind.Sconce, At = at, Height = v.SconceHeight, Section = i, Side = side, Along = along, ByLookList = true });
                            }
                    }
                }
                while (warm.Count < wanted && places.Count > 0)
                {
                    Lamp best = null;
                    double bestScore = double.NegativeInfinity;
                    foreach (Lamp p in places)
                    {
                        double score;
                        if (warm.Count == 0) score = -p.At.DistanceTo(middle);
                        else
                        {
                            score = double.PositiveInfinity;
                            foreach (Lamp w in warm) score = Math.Min(score, p.At.DistanceTo(w.At));
                            if (score < LookListValues.SconceSpacing) continue;
                        }
                        if (score > bestScore) { bestScore = score; best = p; }
                    }
                    if (best == null) break;
                    plan.Lamps.Add(best);
                    warm.Add(best);
                    places.Remove(best);
                }
            }
        }

        static int per(double cs) => Math.Max(1, (int)Math.Round(cs)); // as LampPlan.FloorPoints

        static bool HasLantern(CryptKitPlan kit, int section, int side)
        {
            foreach (CryptKitLantern l in kit.Lanterns)
                if (l.Section == section && l.Side == side) return true;
            return false;
        }

        static double Luma(Rgb c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

        /// <summary>
        /// Work order 091's measure, on the plan alone: the lamps' light on the floor at a ground point while she stands in
        /// <paramref name="room"/>: the sum over every lamp with a light of its own of the rooms the cave shows from there
        /// (<see cref="CaveSight.Shown"/>), of its intensity at the level times its colour's luminance times the URP point light's
        /// fall-off (1/d², times (1 - (d²/r²)²)², d from the light to the floor point, r its range). Also the distance on the ground
        /// to the nearest lamp of those rooms. (The pictures' test measures the same on the NavMesh's points.)
        /// </summary>
        public static double LampLight(LevelLayout layout, LampPlan plan, LampValues v, float lampLevel, int room, LevelPoint at, bool[] shown, out double nearest)
        {
            CaveSight.Shown(layout, room, shown);
            double light = 0;
            nearest = double.PositiveInfinity;
            foreach (Lamp l in plan.Lamps)
            {
                if (l.Room < 0 || l.Room >= shown.Length || !shown[l.Room]) continue;
                double flat = l.At.DistanceTo(at);
                if (flat < nearest) nearest = flat;
                if (l.CarriedBy != null) continue;
                v.Of(l.Kind, out _, out float range);
                double d2 = flat * flat + l.Height * l.Height;
                double f = d2 / Math.Max(1e-4, range * range);
                double smooth = Math.Min(1, Math.Max(0, 1 - f * f));
                light += l.Intensity(v) * Math.Max(0f, lampLevel) * Luma(v.ColourOf(l)) * smooth * smooth / Math.Max(d2, 1e-4);
            }
            return light;
        }

        /// <summary>Lamp light below this is none (091's).</summary>
        public const double NoLampLight = 0.005;

        /// <summary>A room's darkest open-floor point by <see cref="LampLight"/> (the least light; among points with none, the farthest from every lamp), and its light at the room's middle.</summary>
        public static void Darkest(LevelLayout layout, LampPlan plan, LampValues v, float lampLevel, LevelRoom room, out double darkest, out double darkNearest, out double middle)
        {
            var shown = new bool[layout.Rooms.Count];
            darkest = double.PositiveInfinity;
            darkNearest = 0;
            foreach (LevelPoint p in LampPlan.FloorPoints(layout, room))
            {
                double light = LampLight(layout, plan, v, lampLevel, room.Order, p, shown, out double nearest);
                bool darker = light < NoLampLight && darkest < NoLampLight ? nearest > darkNearest : light < darkest;
                if (darker) { darkest = light; darkNearest = nearest; }
            }
            middle = LampLight(layout, plan, v, lampLevel, room.Order, room.Middle(layout.CellSize), shown, out _);
        }
    }
}
