using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// One floor's room tone (work order 113): today's tone (<see cref="RoomMoodPlan.RoomTone"/>) a step lower and emptier
    /// for each floor down. A row of <see cref="RoomSoundValues.Tones"/>; floor 1's row gives today's tone exactly.
    /// </summary>
    [Serializable]
    public struct RoomToneRow
    {
        /// <summary>The floor of the act it is for, from 1.</summary>
        public int Floor;
        /// <summary>Times every frequency of today's tone: the air's two low-passes (240 Hz and 80 Hz) and the drips. 1: today's.</summary>
        public float Pitch;
        /// <summary>s of the air's slow beat, one swell a loop, which is also the loop's length. 7: today's.</summary>
        public float Beat;
        /// <summary>Times the brighter of the air's two layers of noise (the one low-passed at 240 Hz): less is emptier. 1: today's.</summary>
        public float Noise;
        /// <summary>How many of today's two drips fall in a loop (the first with its echo, then the second): 2 is today's, 0 none.</summary>
        public int Drips;

        public RoomToneRow(int floor, float pitch, float beat, float noise, int drips)
        {
            Floor = floor;
            Pitch = pitch;
            Beat = beat;
            Noise = noise;
            Drips = drips;
        }
    }

    /// <summary>
    /// The sounds of the rooms (work order 113; Kristian, 2026-11-24, ticking "Utseende og stemning" ("lyd ... pusses") and
    /// saying yes to the sprint plan's row of sounds for the new things: "Kodelagde lyder du kan bytte ut med egne opptak senere
    /// ... en romtone per etasje. Ingen musikk."; of the door, 2026-11-23: "føles veldig riktig at den lukker seg"). Held by
    /// <see cref="PlaceholderSounds"/> as its <c>Rooms</c>; the sounds' levels are lines of <see cref="PlaceholderSoundValues.Sounds"/>
    /// beside every other placeholder's. First guesses, made without hearing them: he tunes them by playing.
    /// </summary>
    [Serializable]
    public sealed class RoomSoundValues
    {
        /// <summary>
        /// The switch (work order 113, ON; Kristian 2026-11-24: "Kodelagde lyder du kan bytte ut med egne opptak senere ... en
        /// romtone per etasje. Ingen musikk."). On: a door's block is heard rising shut (stone on stone, a thud as it seats) and
        /// sinking open (a lighter grind) where it stands; a room's fight is heard waking (a short low swell, once a room) and a
        /// combat room cleared letting go (a release, in place of the stone shift "way opens"); the stairs to the next floor are
        /// heard; and each floor down has its room tone a step lower and emptier (<see cref="Tones"/>). Off: as before, field for
        /// field: the doors' heavy impact as they shut and nothing as they open, the room's waking the boss's growl, the cleared
        /// room the way opening, no sound for the stairs, and today's room tone on every floor.
        /// </summary>
        public bool On = true;

        /// <summary>
        /// The room tone a floor (work order 113: "today's tone as it is on floor 1, a step lower and emptier each floor down"), five
        /// rows, one a floor of the act (<see cref="LevelValues.FloorsPerAct"/>); a floor past the last has the last row. The pitch a
        /// whole tone (two semitones, 0.891) lower a floor; the slow beat a second longer a floor; the brighter air thinned by 0.15
        /// a floor; the drips going, one by one. Its level is the mood's own (<see cref="MoodValues.ToneVolume"/>, 0.05, the same on
        /// every floor: it sits under everything). Changed once, when a floor is built.
        /// </summary>
        public RoomToneRow[] Tones = DefaultTones();

        public static RoomToneRow[] DefaultTones() => new[]
        {
            new RoomToneRow(1, 1f, 7f, 1f, 2),       // today's tone, exactly
            new RoomToneRow(2, 0.891f, 8f, 0.85f, 2),
            new RoomToneRow(3, 0.794f, 9f, 0.7f, 1),
            new RoomToneRow(4, 0.707f, 10f, 0.55f, 1),
            new RoomToneRow(5, 0.63f, 11f, 0.4f, 0),
        };

        /// <summary>
        /// 0 to 1: how much a door's sound is placed in the world at the door (1 fully, 0 plain in both ears as the other
        /// placeholders): placed, so that she hears which door; a fifth kept in both ears, so that a door to one side is not in
        /// one ear alone.
        /// </summary>
        public float DoorPlaced = 0.8f;
        /// <summary>
        /// m from the listener (the camera, 28 m from her: <see cref="FeelValues.CameraDistance"/>) within which a door is heard
        /// at its full level; farther it falls as this over the distance (a door 20 m from her at about 0.9, one 40 m away at
        /// about 0.6): the far door of a room is quieter, never gone.
        /// </summary>
        public float DoorFullWithin = 30f;
        /// <summary>m beyond which a door falls no further.</summary>
        public float DoorFarthest = 200f;
        /// <summary>Voices for the doors' sounds, each with its own place in the world; when all are busy the oldest is cut off.</summary>
        public int DoorVoices = 6;
    }

    /// <summary>The rules of the sounds of the rooms (work order 113), with no engine in them.</summary>
    public static class RoomSoundRules
    {
        /// <summary>
        /// The Resources path a recorded room tone must have to be played in place of the code-made one, beside the other
        /// placeholders' (<see cref="PlaceholderSoundRules.ResourceFolder"/>): "Sounds/Placeholders/RoomTone". A recorded tone
        /// plays on every floor, on a floor down at its row's <see cref="RoomToneRow.Pitch"/> (its beat, its noise and its drips
        /// are the recording's own).
        /// </summary>
        public static string ToneResourcePath => PlaceholderSoundRules.ResourceFolder + "/RoomTone";

        /// <summary>The five sounds of work order 113, in their order at the end of <see cref="PlaceholderSound"/>.</summary>
        public static readonly PlaceholderSound[] Sounds =
        {
            PlaceholderSound.DoorShuts, PlaceholderSound.DoorOpens, PlaceholderSound.RoomWakes, PlaceholderSound.RoomLetsGo, PlaceholderSound.WayDown,
        };

        /// <summary>Is this one of the sounds placed in the world at a door (not plain in both ears)?</summary>
        public static bool PlacedAtADoor(PlaceholderSound sound) => sound == PlaceholderSound.DoorShuts || sound == PlaceholderSound.DoorOpens;

        /// <summary>The row of a floor: its own, or the nearest (the first for a floor before 1, the last past the last). Null for none.</summary>
        public static RoomToneRow? RowOf(RoomToneRow[] rows, int floor)
        {
            if (rows == null || rows.Length == 0) return null;
            RoomToneRow best = rows[0];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Floor == floor) return rows[i];
                if (rows[i].Floor <= floor && rows[i].Floor > best.Floor) best = rows[i];
            }
            return best;
        }

        /// <summary>Is a row today's tone as it is (every column at today's)? Such a floor keeps the mood's own clip.</summary>
        public static bool IsToday(RoomToneRow row) =>
            row.Pitch == 1f && row.Beat == TodaysBeat && row.Noise == 1f && row.Drips >= TodaysDrips;

        /// <summary>s: today's loop, one swell of the air (<see cref="RoomMoodPlan.RoomTone"/>).</summary>
        public const float TodaysBeat = 7f;
        /// <summary>Today's drips: the first (with its echo) and the second.</summary>
        public const int TodaysDrips = 2;

        /// <summary>
        /// The tone of a row: today's recipe (<see cref="RoomMoodPlan.RoomTone"/>) made its row's way. Its length and the air's
        /// swell stretched to <see cref="RoomToneRow.Beat"/> (one swell a loop still, so it loops without a seam), every drip
        /// where it was in the loop's share; every frequency times <see cref="RoomToneRow.Pitch"/>; the brighter air times
        /// <see cref="RoomToneRow.Noise"/>; the drips past <see cref="RoomToneRow.Drips"/> left out (the last first). A row at
        /// today's gives today's recipe, number for number.
        /// </summary>
        public static SynthRecipe ToneOf(RoomToneRow row)
        {
            SynthRecipe today = RoomMoodPlan.RoomTone();
            float beat = row.Beat > 0f ? row.Beat : today.Seconds;
            float stretch = beat / today.Seconds;
            float pitch = row.Pitch > 0f ? row.Pitch : 1f;
            float noise = Math.Max(0f, row.Noise);

            // Today's layers: the two of air (noise), then the drips (sines): the first, its echo, the second.
            var air = new List<SynthLayer>();
            var drips = new List<SynthLayer>();
            foreach (SynthLayer l in today.Layers)
                (l.Wave == SynthWave.Noise ? air : drips).Add(l);
            int keep = DripLayersKept(drips.Count, row.Drips);

            var layers = new List<SynthLayer>();
            for (int i = 0; i < air.Count; i++)
            {
                SynthLayer l = air[i];
                l.Length *= stretch;
                if (l.LowPass > 0f) l.LowPass *= pitch;
                if (l.LowPassTo > 0f) l.LowPassTo *= pitch;
                if (l.HighPass > 0f) l.HighPass *= pitch;
                if (l.HighPassTo > 0f) l.HighPassTo *= pitch;
                if (l.Tremolo > 0f) l.Tremolo /= stretch;
                if (i == 0) l.Gain *= noise; // the brighter air, low-passed at 240 Hz today
                layers.Add(l);
            }
            for (int i = 0; i < keep; i++)
            {
                SynthLayer l = drips[i];
                l.Start *= stretch;
                l.From *= pitch;
                if (l.To > 0f) l.To *= pitch;
                layers.Add(l);
            }
            today.Seconds = beat;
            today.Layers = layers.ToArray();
            return today;
        }

        /// <summary>
        /// How many of today's drip layers stay for <paramref name="drips"/> drips: all for two or more; for one, all but the
        /// last (the second drip; the first keeps its echo); none for none.
        /// </summary>
        public static int DripLayersKept(int layers, int drips) =>
            drips >= TodaysDrips ? layers : drips <= 0 ? 0 : Math.Max(0, layers - 1);

        /// <summary>
        /// A room's fight is heard waking once a room (work order 113): when it starts for the first time on its floor
        /// (<see cref="RoomFight.Starts"/> from 0), not when it wakes again after she got away; and not with the fight's own cue off.
        /// </summary>
        public static bool WakeHeard(int startsSeen, int startsNow, bool cue) => cue && startsSeen <= 0 && startsNow > 0;

        /// <summary>
        /// She took the way down to the next floor (work order 113): in a run that goes on (the same run's record), the floor's
        /// number went up from one she was on. Not the run's first floor, not a new run, not the room lab's same room again.
        /// </summary>
        public static bool WentDown(bool inRun, bool sameRun, int floorSeen, int floorNow) =>
            inRun && sameRun && floorSeen >= 1 && floorNow > floorSeen;
    }

    /// <summary>
    /// At most so many plays a second (work order 113, the doors' sounds, which are placed and so do not go through the crowd
    /// rule's frame): the times of the last plays in a fixed ring, nothing allocated. Time is passed in, in seconds.
    /// </summary>
    public sealed class SecondCap
    {
        readonly float[] _at;
        int _next;

        public SecondCap(int most)
        {
            _at = new float[Math.Max(1, most)];
            for (int i = 0; i < _at.Length; i++) _at[i] = float.NegativeInfinity;
        }

        /// <summary>May one more play now under a cap of <paramref name="perSecond"/>? If so, it counts.</summary>
        public bool Ask(float now, int perSecond)
        {
            int cap = Math.Min(Math.Max(0, perSecond), _at.Length);
            if (cap == 0) return false;
            int within = 0;
            for (int i = 0; i < _at.Length; i++)
            {
                if (_at[i] > now) _at[i] = float.NegativeInfinity; // a clock that went back: a new start
                if (now - _at[i] < 1f - 1e-4f) within++;
            }
            if (within >= cap) return false;
            _at[_next] = now;
            _next = (_next + 1) % _at.Length;
            return true;
        }
    }
}
