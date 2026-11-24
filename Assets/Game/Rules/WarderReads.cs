using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The warder that reads (work order 107, 2026-11-24: Kristian ticked "Fiender og boss", whose text named "vokteren som ikke
    /// leses"; in his sessions of 2026-11-23 and 2026-11-24 he killed six warders and broke nine wards without a word about them,
    /// and the producer's reading was that the warder looks like a caster and its green thread and ring are lost at lamplit C).
    /// What it serves: she sees that two enemies take less damage, sees who does it, and chooses to kill that one first.
    ///
    /// Its numbers, all first guesses chosen from the pictures of work order 107 (taken off screen at lamplit C, the game's
    /// camera: 55 degrees, 28 m), for him to tune by playing. The ward's own numbers (how much, how far, how many, how long) are
    /// <see cref="WarderValues"/>'s and are not changed. With <see cref="WarderValues.Reads"/> off none of this is made or
    /// heard, and the warder is as it was.
    /// </summary>
    [Serializable]
    public sealed class WarderReadValues
    {
        // ---------------- Its own body: a staff it carries, with the ward's light on top, and a halo ----------------
        // Made from what the project has: two primitives (a cylinder and a sphere) and a line ring. A caster carries nothing
        // and glows cyan; the warder carries a tall staff whose green light stands over a crowd's heads. (No real light: a
        // floor's lights are counted against its budget.)
        public bool Staff = true;           // the staff and its light
        public float StaffTop = 2.75f;      // m over the ground, the top of its light: over its own 2.2 m head, and under the bar
                                            // over it (3.3 m and up), which hid a light at 3.1 m in the pictures
        public float StaffRadius = 0.05f;   // m, the staff's pole
        public float StaffSide = 0.5f;      // m to its right of its middle, where the staff stands (it does not swing with the arms)
        public float StaffAhead = 0.1f;     // m in front of its middle
        public float OrbRadius = 0.2f;      // m, the ward's light on top of the staff
        public float OrbGlow = 1.4f;        // times the ward's colour the light glows: bright, still green (2 and over reads white)
        public bool Halo = true;            // a ring of the ward's light over its head: the same ring the warded wear round them
        public float HaloOver = -0.05f;     // m over its model's height as made (it stoops: the halo sits just over its hood)
        public float HaloAhead = 0.15f;     // m in front of its middle, where its stooped head is
        public float HaloRadius = 0.34f;    // m
        public float HaloWidth = 0.07f;     // m, the halo's line
        public bool GreenGlows = true;      // the caster's model's own glows (eyes, palms, amulet) in the ward's green, not the caster's cyan
        public float GreenGlowStrength = 3f;// times the ward's colour, as the caster's cyan is 3 times its own
        public float BodyGlow = 1f;         // times the ward's colour its whole body glows (its painted texture lit green): a green
                                            // figure in the lamplit dark where a caster is a dark one; 0: only its glows, green

        // ---------------- It works: while a ward of its is on, its light and its halo pulse ----------------
        public float PulseSeconds = 0.9f;   // s from one pulse to the next
        public float PulseDepth = 0.35f;    // the share the light, the thread and the ring swell by at a pulse

        // ---------------- The ward seen: the thread from its light, the ring on the warded ----------------
        public bool ThreadFromLight = true; // the thread runs from the staff's light down to the warded, not from its chest
        public float ThreadWidth = 0.13f;   // m, the thread's line (was 0.06)
        public float ShellWidth = 0.17f;    // m, the ring's line round a warded enemy (was 0.08)
        public float ShellHitWidth = 0.42f; // m, the ring's line as a hit flashes it (was 0.25)

        // ---------------- The hit on a warded enemy: its number marked ----------------
        public bool MarkNumbers = true;     // a warded hit's damage number carries a small shield and an ordinary one turns green
        public float MarkShare = 0.78f;     // the shield's height, a share of the number's
        public float MarkGap = 3f;          // px of a 1080 view between the shield and the number, the shield on its left
        public float MarkAspect = 0.82f;    // the shield's width over its height

        // ---------------- The first time, told once ----------------
        public bool Hint = true;            // the first warded hit of a run shows one short line by the target, never again that run
        public float HintSeconds = 2.6f;    // s the line shows
        public float HintFadeFrom = 0.75f;  // the share of its life after which it fades
        public int HintSize = 22;           // px of a 1080 view
        public float HintBelow = 30f;       // px of a 1080 view under the place a number of the target starts

        // ---------------- The ward heard ----------------
        public bool Hum = true;             // a soft hum while any ward stands (PlaceholderSound.WardHum, looped)
        public bool BreakSound = true;      // a break when a ward falls (PlaceholderSound.WardBreaks): the warder killed or staggered, or too far
    }

    /// <summary>The rules of the warder that reads (work order 107), with no engine in them.</summary>
    public static class WarderReadRules
    {
        /// <summary>The line the first warded hit of a run shows, by the target (the work order's words).</summary>
        public const string HintText = "Warded: kill the warder";

        /// <summary>Is the warder of these values read (work order 107's switch)?</summary>
        public static bool On(WarderValues v) => v != null && v.Reads;

        /// <summary>The reading's values of these, or the defaults.</summary>
        public static WarderReadValues R(WarderValues v) => v?.Reading ?? s_Defaults;

        /// <summary>Do its threads run from the light on its staff (the switch on, a staff, and the thread from it)?</summary>
        public static bool FromLight(WarderValues v) => On(v) && R(v).Staff && R(v).ThreadFromLight;
        /// <summary>Are its wards heard as a hum while they stand?</summary>
        public static bool Hums(WarderValues v) => On(v) && R(v).Hum;
        /// <summary>Is a ward of its heard falling?</summary>
        public static bool BreakSounds(WarderValues v) => On(v) && R(v).BreakSound;
        /// <summary>Does a hit on an enemy it wards carry the shield on its number?</summary>
        public static bool MarksNumbers(WarderValues v) => On(v) && R(v).MarkNumbers;
        /// <summary>Does the first hit of a run on an enemy it wards show the line?</summary>
        public static bool Hints(WarderValues v) => On(v) && R(v).Hint;
        static readonly WarderReadValues s_Defaults = new WarderReadValues();

        /// <summary>
        /// The pulse <paramref name="now"/> s into the game, 0 to 1: a smooth swell once every <paramref name="seconds"/> (1 at its
        /// top). 0 for no period.
        /// </summary>
        public static float Pulse(float now, float seconds)
        {
            if (!(seconds > 0f)) return 0f;
            double phase = now / (double)seconds;
            phase -= Math.Floor(phase);
            return (float)(0.5 - 0.5 * Math.Cos(phase * 2.0 * Math.PI));
        }

        /// <summary>Times its size a pulsing thing is now: 1, up to 1 + the depth at a pulse's top.</summary>
        public static float Swell(WarderValues v, float now)
        {
            WarderReadValues r = R(v);
            return 1f + Math.Max(0f, r.PulseDepth) * Pulse(now, r.PulseSeconds);
        }

        /// <summary>The thread's width now: as before with the switch off; wider and pulsing with it on.</summary>
        public static float ThreadWidth(WarderValues v, float now) => On(v) ? R(v).ThreadWidth * Swell(v, now) : v.ThreadWidth;

        /// <summary>The ring's width now, flashed by a hit or not: as before with the switch off; wider and pulsing with it on.</summary>
        public static float ShellWidth(WarderValues v, float now, bool flashing)
        {
            if (!On(v)) return flashing ? v.ShellHitWidth : v.ShellWidth;
            return flashing ? R(v).ShellHitWidth : R(v).ShellWidth * Swell(v, now);
        }

        /// <summary>The ring's width at a hit, where a broken ward's ring bursts from: as before with the switch off.</summary>
        public static float ShellHitWidth(WarderValues v) => On(v) ? R(v).ShellHitWidth : v.ShellHitWidth;

        /// <summary>Does a ward broken for this reason make the break sound? The ones that daze: the warder killed or staggered, or too far.</summary>
        public static bool BreakHeard(WardEnd why) => WarderRules.Dazes(why);

        /// <summary>Is the hum wanted now: placeholder sounds on, not paused, and at least one ward on?</summary>
        public static bool HumWanted(int wardsOn, bool soundsOn, bool paused) => soundsOn && !paused && wardsOn > 0;

        /// <summary>A warded hit's number: is its own ink the ward's green (an ordinary hit's white turns green), or kept (a crit's, a clean press's)?</summary>
        public static bool GreenInk(NumberKind kind) => kind == NumberKind.Plain;

        /// <summary>
        /// The shield mark's shape: how much of the point (<paramref name="u"/> across, -1 to 1; <paramref name="v"/> down, 0 at
        /// its top to 1 at its point) is inside it, 0 or 1. A heater shield: straight sides down to the middle, then curving in to a
        /// point at the bottom.
        /// </summary>
        public static bool InShield(float u, float v)
        {
            if (v < 0f || v > 1f) return false;
            float a = Math.Abs(u);
            if (v <= ShieldShoulder) return a <= 1f;
            float t = (v - ShieldShoulder) / (1f - ShieldShoulder);
            return a <= 1f - (float)Math.Pow(t, ShieldCurve);
        }

        /// <summary>The share of the shield's height its straight sides run before they curve in.</summary>
        public const float ShieldShoulder = 0.42f;
        /// <summary>How the sides curve to the point: over 1 a rounder belly.</summary>
        public const float ShieldCurve = 1.6f;

        /// <summary>The line's opacity <paramref name="age"/> s into its life: full, then fading from its share to 0 at its end.</summary>
        public static float HintAlpha(float age, WarderReadValues r) => FeedbackRules.Alpha(age, r.HintSeconds, r.HintFadeFrom);
    }

    /// <summary>
    /// Once a run (work order 107): the first warded hit of a run shows its line, never again that run. A run is known by its
    /// record (any object the game gives; none for the practice room before a run, which counts as one of its own).
    /// </summary>
    public sealed class OncePerRun
    {
        object _run;
        bool _any;

        /// <summary>Times it has said yes.</summary>
        public int Taken { get; private set; }

        /// <summary>May it show now, in this run? If so it counts as shown for the run.</summary>
        public bool Take(object run)
        {
            if (_any && ReferenceEquals(run, _run)) return false;
            _any = true;
            _run = run;
            Taken++;
            return true;
        }

        /// <summary>Has it shown in this run?</summary>
        public bool ShownIn(object run) => _any && ReferenceEquals(run, _run);

        public void Clear()
        {
            _any = false;
            _run = null;
        }
    }

    /// <summary>
    /// The hum's gate (work order 107): it starts when it is first wanted (the first ward of any warder that reads is on) and
    /// stops when it no longer is (the last is gone), each once.
    /// </summary>
    public sealed class HumGate
    {
        /// <summary>Is it playing now?</summary>
        public bool On { get; private set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }

        /// <summary>One frame: true when it changed (started or stopped).</summary>
        public bool Step(bool wanted)
        {
            if (wanted == On) return false;
            On = wanted;
            if (wanted) Starts++;
            else Stops++;
            return true;
        }

        public void Clear()
        {
            On = false;
            Starts = 0;
            Stops = 0;
        }
    }
}
