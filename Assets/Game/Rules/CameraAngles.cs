using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// One camera angle: how far the camera looks down (its pitch, in degrees from the ground), how wide it sees (its vertical
    /// field of view, in degrees) and how far it stands from her (in metres, along its look). The yaw is always the game's
    /// (<see cref="CaveWalls.GameYaw"/>): an angle never turns the camera round her. Plain data.
    /// </summary>
    public readonly struct CameraAngle
    {
        public readonly string Name;
        public readonly float Pitch;        // degrees down from the ground (90 straight down)
        public readonly float FieldOfView;  // degrees, vertical, as Unity's Camera.fieldOfView
        public readonly float Distance;     // m from the camera to the point on the ground under her, along the look

        public CameraAngle(string name, float pitch, float fieldOfView, float distance)
        {
            Name = name;
            Pitch = pitch;
            FieldOfView = fieldOfView;
            Distance = distance;
        }

        public override string ToString() => Name + " (" + Pitch + ", " + FieldOfView + ", " + Distance + ")";
    }

    /// <summary>
    /// The pause menu's "Camera angle" (work order 121; Kristian, 2026-11-25, after two runs: "Jeg vil også utforske vinklene vi
    /// har litt og. Kan vi få en kopi av diablo, en av league og en av path of exile? Så kan vi vurdere de forskjellige"): four
    /// named angles he steps through while playing. "Ours" is the feel's own camera, untouched (<see cref="FeelValues.CameraPitch"/>,
    /// <see cref="FeelValues.FieldOfView"/>, <see cref="FeelValues.CameraDistance"/>, read from the settings asset as before);
    /// the other three are each in the manner of that game's default camera at its default zoom: its pitch and its field of
    /// view as near as the sources allow, and a distance that makes her (1.8 m) as tall on the screen as that game's character
    /// is at a 16:9 screen, so about as much ground is seen round her. Every number's source and kind is beside it, and in the
    /// order's report. The yaw is NOT copied: Diablo's and League's worlds lie diagonal to their screens; ours stays square to
    /// it (<see cref="CaveWalls.GameYaw"/> 0), as the wall kit, the far and near walls and the lamps are built for it.
    ///
    /// The camera distance's steps (<see cref="CameraDistanceSteps"/>) still work on top of an angle: at "Ours" exactly as
    /// before (28, 25, 22 m); at another angle each step scales that angle's own distance by the same share as it scales the
    /// feel's (25/28 and 22/28). Kept in PlayerPrefs by the game's settings as the distance's step is. Plain data.
    /// </summary>
    public static class CameraAngles
    {
        /// <summary>
        /// The order's switch (work order 121): on, the pause menu has the "Camera angle" line, the camera follows its choice and
        /// the session log tells each change; off, no line, the camera is "Ours" whatever is stored, and the log is as before.
        /// </summary>
        public static bool On = true;

        public const int Ours = 0, Diablo = 1, League = 2, PathOfExile = 3;
        public const int Count = 4;

        /// <summary>The step a player gets who has none stored: "Ours" (the order: the default angle is not this order's to change).</summary>
        public const int Default = Ours;

        /// <summary>The words of each angle on the menu's line, in the order's own: "Ours / Diablo / League / PoE".</summary>
        public static readonly string[] Names = { "Ours", "Diablo", "League", "PoE" };

        // ---------------- The four angles' numbers (sources: work order 121's report) ----------------

        /// <summary>Ours: today's code defaults (FeelValues), for the table and the tests; the game reads the feel's own (the asset's).</summary>
        public const float OursPitch = 55f, OursFieldOfView = 30f, OursDistance = 28f;

        // Diablo IV's default camera (PC: "View Distance: Far", the default since season 4 by the developer's word, PezRadar,
        // https://devtrackers.gg/diablo4/p/7399fcf4-camera-will-now-zoom-out). Its camera file, datamined:
        // https://raw.githubusercontent.com/DiabloTools/d4data/master/json/base/meta/Observer/far.obs.json (and observer.obs.json).
        public const float DiabloPitch = 47f;        // CONFIG VALUE (derived): the file's camera offset (9.56, 9.56, 14.5), z up: atan(14.5 / (9.56·√2)) = 47.0 degrees down; players call it steep. Diablo's offset also turns the camera 45 degrees round her (the world diagonal on the screen): NOT copied, the yaw stays ours
        public const float DiabloFieldOfView = 35f;  // CONFIG VALUE: aFOV 0.6109 rad = 35.0 degrees, vertical (the engine's projection, as decompiled from Diablo III's: cot(fov/2)/aspect across)
        public const float DiabloDistance = 15.6f;   // MEASURED FROM A PICTURE, then worked out: Far's distance is 19.83 units (yards, about 0.91 m), but its models are larger than life (about 2.3 units tall by the picture below), so the distance is chosen for her height on the screen instead: 13.6 % of the screen's height at Far (165 px of 1080, 15.3 %, measured at Standard, 0.9 of Far's distance, in https://images.launchbox-app.com/22732b9c-2f45-4e5c-8152-2f0c9151aca0.png), DistanceForShare(47, 35, 1.8 m, 0.136) = 15.6 m

        // League of Legends, the default locked camera on Summoner's Rift at its default (furthest) zoom.
        public const float LeaguePitch = 56f;        // CONFIG VALUE: Riot's own replay tool League Director shows the game camera's rotation (0, 56, 0) (https://raw.githubusercontent.com/RiotGames/leaguedirector/master/resources/screenshot.png); "default 56 degree camera angle" (https://summonersrift.mvilim.dev/about.html); a ring on the ground measured 0.84 as tall as wide (sin 56 = 0.83) in https://i.ytimg.com/vi/vczYSIgZgVE/maxres2.jpg
        public const float LeagueFieldOfView = 40f;  // CONFIG VALUE: the same screenshot's field of view 40, vertical (the game is Hor+: a wider screen sees more to the sides, PCGamingWiki)
        public const float LeagueDistance = 15.3f;   // MEASURED FROM A PICTURE, then worked out: the camera stands 2250 units from the champion (players' memory value for the furthest zoom; the minimap's view box, 4135 by 2290 units in https://i.ytimg.com/vi/7pY7cChqgc0/maxres2.jpg, fits 56 / 40 / 2250), and a champion is drawn 10.4 to 11 % of the screen's height (Garen, a big one, 75 to 79 px of 720 in that picture); 10 % taken for a champion of a human's size (Ashe's selection height 155 against Garen's 189): DistanceForShare(56, 40, 1.8 m, 0.10) = 15.3 m

        // Path of Exile (the first game) at its default zoom (PoE 1 has no zoom in play; PoE 2's is in the report).
        public const float PoePitch = 53f;           // ESTIMATED: worked out from a player's calibrated camera matrix, view direction (-0.43, 0.43, -0.80): 52.7 degrees, plus or minus 3 for its rounding (https://nicholastsmith.wordpress.com/2017/07/09/calibrating-a-projection-matrix-for-path-of-exile/); no developer number found. Its camera too is turned 45 degrees round the world grid: NOT copied
        public const float PoeFieldOfView = 44f;     // ESTIMATED: the same matrix's focal length, 748 px at 800 by 600: 2·atan(300 / 748) = 43.7 degrees vertical; taken as vertical at 16:9 too (not known)
        public const float PoeDistance = 12.6f;      // MEASURED FROM PICTURES, then worked out: her height 12 % of the screen's (13.3 %, 96 px of 720, in https://i.ytimg.com/vi/XAogqH-ylZU/maxres2.jpg; 10.7 %, 77 px, in https://i.ytimg.com/vi/T3yJGMh6lIE/maxres3.jpg): DistanceForShare(53, 44, 1.8 m, 0.12) = 12.6 m

        // To swap in another Diablo (the report's numbers, by changing the three Diablo values above):
        //   Diablo III: pitch about 51, field of view about 41 (ESTIMATED, a ring of a Monk's eight copies measured in
        //     https://images.launchbox-app.com/06876293-2f84-4634-8588-d4974b02a67f.jpg; no camera file found), her 12.2 % of the
        //     screen's height (MEASURED, https://images.launchbox-app.com/e24954d3-2efc-4b6a-89e7-c741c360bc9e.jpg): distance 13.8 m.
        //   Diablo II and Resurrected: ORTHOGRAPHIC, 2:1 tiles, pitch 30 (sin 30 = 0.5) and the world at 45 degrees; her 11 to
        //     11.8 % of the screen's height (MEASURED): nearest with this perspective camera, pitch 30, field of view 10, distance 76 m.
        //   Diablo IV at "Standard" (the Steam Deck's default): pitch 47, field of view 35, distance 0.9 of Far's: 14.1 m.

        /// <summary>The four, by step. Ours' entry holds today's code defaults; <see cref="InUse"/> reads the feel's own for it.</summary>
        public static readonly CameraAngle[] Presets =
        {
            new CameraAngle(Names[Ours], OursPitch, OursFieldOfView, OursDistance),
            new CameraAngle(Names[Diablo], DiabloPitch, DiabloFieldOfView, DiabloDistance),
            new CameraAngle(Names[League], LeaguePitch, LeagueFieldOfView, LeagueDistance),
            new CameraAngle(Names[PathOfExile], PoePitch, PoeFieldOfView, PoeDistance),
        };

        // ---------------- Stepping, as the menu's other lines step ----------------

        /// <summary>A stored value made a step: anything that is not a step is "Ours".</summary>
        public static int Step(int stored) => stored >= 0 && stored < Count ? stored : Default;

        /// <summary>The step after this one: Ours, Diablo, League, PoE and back to Ours (Enter, a click, the right arrow).</summary>
        public static int Next(int step) => (Step(step) + 1) % Count;

        /// <summary>The step before (the left arrow): Ours, PoE, League, Diablo and back.</summary>
        public static int Previous(int step) => (Step(step) + Count - 1) % Count;

        /// <summary>
        /// The camera in use for an angle step and a distance step: at "Ours" (or with the switch off) the feel's own pitch and
        /// field of view and the distance step's distance, number for number what the camera was before this order; at another
        /// angle its own pitch and field of view, and its own distance scaled by the distance step's share of the feel's.
        /// </summary>
        public static CameraAngle InUse(int angle, int distanceStep, float feelPitch, float feelFieldOfView, float feelDistance)
        {
            int a = On ? Step(angle) : Ours;
            float stepped = CameraDistanceSteps.Distance(distanceStep, feelDistance);
            if (a == Ours) return new CameraAngle(Names[Ours], feelPitch, feelFieldOfView, stepped);
            CameraAngle p = Presets[a];
            float share = feelDistance > 0f ? stepped / feelDistance : 1f;
            return new CameraAngle(p.Name, p.Pitch, p.FieldOfView, p.Distance * share);
        }

        // ---------------- What an angle shows (for the choice of the numbers, the tests and the report) ----------------

        static double Rad(double degrees) => degrees * Math.PI / 180.0;

        /// <summary>
        /// How tall a figure <paramref name="height"/> m tall standing on the point the camera looks at is on the screen, as a
        /// share of the screen's height: its feet at the middle, its head h·cos(pitch) up and h·sin(pitch) nearer the camera.
        /// </summary>
        public static double HeightShare(float pitch, float fieldOfView, float distance, float height)
        {
            double p = Rad(pitch), t = Math.Tan(Rad(fieldOfView) * 0.5);
            double up = height * Math.Cos(p), depth = distance - height * Math.Sin(p);
            return depth <= 0.0 ? double.PositiveInfinity : up / depth / (2.0 * t);
        }

        /// <summary>The ground's width seen across the screen at her feet, in metres, on a screen <paramref name="aspect"/> wide to its height.</summary>
        public static double GroundWidthAtHer(float fieldOfView, float distance, float aspect) =>
            2.0 * distance * aspect * Math.Tan(Rad(fieldOfView) * 0.5);

        /// <summary>The ground seen between her feet and the screen's bottom edge, toward the camera, in metres.</summary>
        public static double GroundBehind(float pitch, float fieldOfView, float distance)
        {
            double p = Rad(pitch), half = Rad(fieldOfView) * 0.5;
            double height = distance * Math.Sin(p), back = distance * Math.Cos(p);
            double bottom = p + half;
            double toBottom = bottom >= Math.PI / 2 ? -height * Math.Tan(bottom - Math.PI / 2) : height / Math.Tan(bottom);
            return back - toBottom;
        }

        /// <summary>The ground seen between her feet and the screen's top edge, away from the camera, in metres (infinite when the top edge sees the horizon).</summary>
        public static double GroundAhead(float pitch, float fieldOfView, float distance)
        {
            double p = Rad(pitch), half = Rad(fieldOfView) * 0.5;
            double height = distance * Math.Sin(p), back = distance * Math.Cos(p);
            double top = p - half;
            return top <= 0.0 ? double.PositiveInfinity : height / Math.Tan(top) - back;
        }

        /// <summary>
        /// How much a circle on the ground at her feet is squashed on the screen: its height over its width. Near the screen's
        /// middle this is about sin(pitch): the measure players and the report use to read a game's pitch off a picture.
        /// </summary>
        public static double CircleSquash(float pitch) => Math.Sin(Rad(pitch));

        /// <summary>The distance at which a figure of <paramref name="height"/> m is <paramref name="share"/> of the screen's height (the inverse of <see cref="HeightShare"/>).</summary>
        public static float DistanceForShare(float pitch, float fieldOfView, float height, double share)
        {
            double p = Rad(pitch), t = Math.Tan(Rad(fieldOfView) * 0.5);
            return (float)(height * Math.Cos(p) / (share * 2.0 * t) + height * Math.Sin(p));
        }
    }

    /// <summary>The session log's event for the camera angle (work order 121), in the recorder's manner (as <see cref="MusicEvents"/>).</summary>
    public static class CameraAngleEvents
    {
        /// <summary>
        /// The camera angle in use, written when the log starts and every time the angle or the distance step changes: name the
        /// angle's, pitch, field_of_view and distance its three numbers (its own distance, at the distance step's 0),
        /// distance_step the step and distance_in_use the distance the camera stands at now.
        /// </summary>
        public const string Change = "camera_angle";
    }
}
