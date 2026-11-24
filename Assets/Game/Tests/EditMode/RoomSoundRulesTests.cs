using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 113, the sounds of the rooms as rules: the five sounds at the end of the list, each with its moment, its line in
    /// the table (a level under the guns, a cap) and its recipe of its length; the door's thud where the block seats; the release
    /// one falling sound; the five rows of the room tone, floor 1's today's tone to the sample, each floor down lower and emptier and
    /// still a seamless loop; once a room for the waking; the way down only in a run going on; the doors' cap a second. The switch
    /// is on. Nobody can hear here whether they sound right.
    /// </summary>
    public class RoomSoundRulesTests
    {
        static readonly PlaceholderSoundValues V = new PlaceholderSoundValues();

        /// <summary>The five, their lengths (s) and levels (a share of her basic shot's volume), as the work order's report gives them.</summary>
        static readonly Dictionary<PlaceholderSound, (float seconds, float level)> Five = new Dictionary<PlaceholderSound, (float, float)>
        {
            { PlaceholderSound.DoorShuts, (0.7f, 0.4f) }, { PlaceholderSound.DoorOpens, (0.9f, 0.25f) },
            { PlaceholderSound.RoomWakes, (0.7f, 0.3f) }, { PlaceholderSound.RoomLetsGo, (1.4f, 0.3f) },
            { PlaceholderSound.WayDown, (1.1f, 0.3f) },
        };

        static IEnumerable<PlaceholderSound> FiveSounds() => Five.Keys;

        // ---------------- The five sounds ----------------

        [Test]
        public void TheFive_AreAtTheEndOfTheList_AfterTheThirtyEight_WhichKeepTheirPlaces()
        {
            Assert.That((int)PlaceholderSound.OffTick, Is.EqualTo(37), "the thirty-eight where they were");
            Assert.That(RoomSoundRules.Sounds.Select(s => (int)s), Is.EqualTo(new[] { 38, 39, 40, 41, 42 }));
            Assert.That(RoomSoundRules.Sounds, Is.EquivalentTo(Five.Keys));
        }

        [TestCaseSource(nameof(FiveSounds))]
        public void Each_HasItsLine_AtItsLevel_UnderTheGuns_AndACap(PlaceholderSound sound)
        {
            Assert.That(V.Sounds.Count(s => s.Sound == sound), Is.EqualTo(1), "one line in the table");
            Assert.That(V.Volume(sound), Is.EqualTo(Five[sound].level).Within(1e-6f), "its level");
            Assert.That(V.Volume(sound), Is.LessThanOrEqualTo(V.Volume(PlaceholderSound.Hurt)), "never over her pain");
            Assert.That(V.Volume(sound), Is.LessThan(1f), "under her basic shot");
            Assert.That(V.PerSecond(sound), Is.InRange(1, 8));
        }

        [TestCaseSource(nameof(FiveSounds))]
        public void Each_IsItsLength_FromSilenceToSilence_UnderFullScale(PlaceholderSound sound)
        {
            float[] s = Synth.Render(PlaceholderRecipes.For(sound));
            Assert.That(PlaceholderRecipes.Seconds(sound), Is.EqualTo(Five[sound].seconds).Within(1e-6f));
            Assert.That(s.Length, Is.EqualTo((int)Math.Round(Five[sound].seconds * Synth.SampleRate)));
            Assert.That(s.Max(v => Math.Abs(v)), Is.InRange(0.3f, 0.9f + 1e-4f));
            Assert.That(s.Any(v => float.IsNaN(v)), Is.False);
            Assert.That(s[0], Is.EqualTo(0f));
            Assert.That(s[s.Length - 1], Is.EqualTo(0f));
        }

        [Test]
        public void EachMoment_GivesItsSound()
        {
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.RoomDoorShut), Is.EqualTo(PlaceholderSound.DoorShuts));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.RoomDoorOpened), Is.EqualTo(PlaceholderSound.DoorOpens));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.RoomFightWoke), Is.EqualTo(PlaceholderSound.RoomWakes));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.RoomLetGo), Is.EqualTo(PlaceholderSound.RoomLetsGo));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WentDown), Is.EqualTo(PlaceholderSound.WayDown));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.CombatRoomCleared), Is.EqualTo(PlaceholderSound.WayOpens), "the moment's own sound as before: the switch swaps it as it plays");
            Assert.That(RoomSoundRules.Sounds.Where(RoomSoundRules.PlacedAtADoor), Is.EqualTo(new[] { PlaceholderSound.DoorShuts, PlaceholderSound.DoorOpens }));
        }

        [Test]
        public void TheFive_AreUnlikeEveryOtherSound()
        {
            foreach (PlaceholderSound mine in RoomSoundRules.Sounds)
            {
                float[] a = Synth.Render(PlaceholderRecipes.For(mine));
                foreach (PlaceholderSound other in PlaceholderSoundRules.All.Where(o => o != mine))
                    Assert.That(a.SequenceEqual(Synth.Render(PlaceholderRecipes.For(other))), Is.False, mine + " and " + other);
            }
        }

        [Test]
        public void TheDoorShutting_IsLoudestWhereTheBlockSeats_AndItsOpening_HasNoThud()
        {
            var rise = new RoomFightValues().DoorRiseSeconds;
            float[] shut = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.DoorShuts));
            int loudest = Array.IndexOf(shut, shut.OrderByDescending(Math.Abs).First());
            float at = loudest / (float)Synth.SampleRate;
            Assert.That(at, Is.InRange(rise - 0.02f, rise + 0.12f), "the thud as it seats, at the end of its rise (" + rise + " s)");
            Assert.That(Rms(shut, 0f, rise - 0.05f), Is.GreaterThan(0.02f), "and stone heard grinding while it rises");

            float[] open = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.DoorOpens));
            Assert.That(open.Length / (float)Synth.SampleRate, Is.GreaterThanOrEqualTo(new RoomFightValues().DoorSinkSeconds), "as long as it sinks");
            Assert.That(Lowness(open), Is.LessThan(Lowness(shut)), "lighter: less of it low");
        }

        [Test]
        public void TheRelease_IsOneFallingSound_NoNotesInARow()
        {
            float[] s = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.RoomLetsGo));
            int half = s.Length / 2;
            Assert.That(Crossings(s, half, s.Length), Is.LessThan(Crossings(s, 0, half)), "it falls: duller as it goes");
            // Not a jingle: its loudness never rises again by much once past its start (one breath out, no second note coming in).
            float first = Rms(s, 0.1f, 0.3f);
            for (float t = 0.3f; t + 0.2f <= 1.4f; t += 0.1f)
                Assert.That(Rms(s, t, t + 0.2f), Is.LessThan(first * 1.05f), "at " + t + " s no new note");
        }

        // ---------------- A room tone a floor ----------------

        [Test]
        public void TheTone_HasFiveRows_OneAFloor_OfTheAct()
        {
            RoomToneRow[] rows = new RoomSoundValues().Tones;
            Assert.That(rows.Length, Is.EqualTo(5));
            Assert.That(rows.Select(r => r.Floor), Is.EqualTo(Enumerable.Range(1, new LevelValues().FloorsPerAct)));
            Assert.That(new RoomSoundValues().On, Is.True, "the switch is on");
        }

        [Test]
        public void OnFloorOne_TheToneIsTodays_ToTheSample()
        {
            RoomToneRow one = RoomSoundRules.RowOf(new RoomSoundValues().Tones, 1).Value;
            Assert.That(RoomSoundRules.IsToday(one), Is.True);
            float[] today = Synth.Render(RoomMoodPlan.RoomTone()), made = Synth.Render(RoomSoundRules.ToneOf(one));
            Assert.That(made, Is.EqualTo(today), "floor 1's tone is today's, sample for sample");
            Assert.That(made.Length, Is.EqualTo(7 * Synth.SampleRate));
        }

        [Test]
        public void EachFloorDown_IsLowerAndEmptier()
        {
            RoomToneRow[] rows = new RoomSoundValues().Tones;
            for (int i = 1; i < rows.Length; i++)
            {
                RoomToneRow up = rows[i - 1], down = rows[i];
                Assert.That(down.Pitch, Is.LessThan(up.Pitch), "lower, floor " + down.Floor);
                Assert.That(down.Beat, Is.GreaterThan(up.Beat), "a slower beat");
                Assert.That(down.Noise, Is.LessThan(up.Noise), "less of the brighter air");
                Assert.That(down.Drips, Is.LessThanOrEqualTo(up.Drips), "no more drips");
                Assert.That(RoomSoundRules.IsToday(down), Is.False);
                float[] above = Synth.Render(RoomSoundRules.ToneOf(up)), below = Synth.Render(RoomSoundRules.ToneOf(down));
                Assert.That(below.Length, Is.EqualTo((int)Math.Round(down.Beat * Synth.SampleRate)), "as long as its beat");
                Assert.That(Crossings(below, 0, below.Length) / (double)below.Length, Is.LessThan(Crossings(above, 0, above.Length) / (double)above.Length),
                            "deeper: fewer crossings a second, floor " + down.Floor);
            }
            Assert.That(rows[rows.Length - 1].Drips, Is.EqualTo(0), "the last floor down has no drip");
        }

        [Test]
        public void EachFloorsTone_Loops_OneSwellALoop_FromSilenceToSilence_AtTodaysPeak()
        {
            foreach (RoomToneRow row in new RoomSoundValues().Tones)
            {
                SynthRecipe r = RoomSoundRules.ToneOf(row);
                Assert.That(r.Seconds, Is.EqualTo(row.Beat));
                SynthLayer air = r.Layers.First(l => l.Tremolo > 0f);
                Assert.That(air.Tremolo * r.Seconds, Is.EqualTo(1f).Within(1e-5f), "one swell a loop: no seam, floor " + row.Floor);
                Assert.That(r.Layers.Where(l => l.Wave == SynthWave.Noise).All(l => Math.Abs(l.Length - r.Seconds) < 1e-4f), "the air the whole loop");
                Assert.That(r.Layers.All(l => l.Start + l.Length <= r.Seconds + 1e-4f), "every drip inside the loop");
                Assert.That(r.Layers.Count(l => l.Wave == SynthWave.Sine), Is.EqualTo(RoomSoundRules.DripLayersKept(3, row.Drips)));
                float[] s = Synth.Render(r);
                Assert.That(s[0], Is.EqualTo(0f));
                Assert.That(s[s.Length - 1], Is.EqualTo(0f));
                Assert.That(s.Max(v => Math.Abs(v)), Is.EqualTo(RoomMoodPlan.RoomTone().Peak).Within(1e-4f), "as quiet a peak as today's");
            }
        }

        [Test]
        public void AFloorPastTheRows_HasTheLast_AndBeforeTheFirst_TheFirst()
        {
            RoomToneRow[] rows = new RoomSoundValues().Tones;
            Assert.That(RoomSoundRules.RowOf(rows, 3).Value.Floor, Is.EqualTo(3));
            Assert.That(RoomSoundRules.RowOf(rows, 9).Value.Floor, Is.EqualTo(5));
            Assert.That(RoomSoundRules.RowOf(rows, 0).Value.Floor, Is.EqualTo(1));
            Assert.That(RoomSoundRules.RowOf(new RoomToneRow[0], 1).HasValue, Is.False);
            Assert.That(RoomSoundRules.RowOf(null, 1).HasValue, Is.False);
        }

        [Test]
        public void TheDrips_GoLastFirst_TheFirstKeepingItsEcho()
        {
            Assert.That(RoomSoundRules.DripLayersKept(3, 2), Is.EqualTo(3));
            Assert.That(RoomSoundRules.DripLayersKept(3, 1), Is.EqualTo(2));
            Assert.That(RoomSoundRules.DripLayersKept(3, 0), Is.EqualTo(0));
            SynthRecipe one = RoomSoundRules.ToneOf(new RoomToneRow(3, 1f, 7f, 1f, 1));
            Assert.That(one.Layers.Where(l => l.Wave == SynthWave.Sine).Select(l => l.Start), Is.EqualTo(new[] { 2.1f, 2.27f }), "the first drip and its echo");
        }

        // ---------------- Once a room, the way down, the doors' cap ----------------

        [TestCase(0, 1, true, true, TestName = "Wakes_TheFirstStart_IsHeard")]
        [TestCase(1, 2, true, false, TestName = "Wakes_AStartAgain_AfterSheGotAway_IsNot")]
        [TestCase(1, 1, true, false, TestName = "Wakes_NoNewStart_IsNot")]
        [TestCase(0, 0, true, false, TestName = "Wakes_Asleep_IsNot")]
        [TestCase(0, 1, false, false, TestName = "Wakes_WithTheFightsCueOff_IsNot")]
        public void TheRoomWaking_IsHeardOnceARoom(int seen, int now, bool cue, bool heard) =>
            Assert.That(RoomSoundRules.WakeHeard(seen, now, cue), Is.EqualTo(heard));

        [TestCase(true, true, 1, 2, true, TestName = "WayDown_FromOneToTwo_InTheRun")]
        [TestCase(true, true, 4, 5, true, TestName = "WayDown_FromFourToFive")]
        [TestCase(true, true, 0, 1, false, TestName = "WayDown_TheRunsFirstFloor_IsNot")]
        [TestCase(true, false, 3, 4, false, TestName = "WayDown_ANewRun_IsNot")]
        [TestCase(false, true, 2, 3, false, TestName = "WayDown_OutOfARun_IsNot")]
        [TestCase(true, true, 3, 3, false, TestName = "WayDown_TheSameFloor_IsNot")]
        [TestCase(true, true, 3, 1, false, TestName = "WayDown_Back_IsNot")]
        public void TheWayDown_IsHeardOnlyInARunGoingOn(bool inRun, bool sameRun, int seen, int now, bool heard) =>
            Assert.That(RoomSoundRules.WentDown(inRun, sameRun, seen, now), Is.EqualTo(heard));

        [Test]
        public void TheDoorsCap_LetsSoManyASecond_ThenMoreAfterTheSecond()
        {
            var cap = new SecondCap(8);
            for (int i = 0; i < 6; i++) Assert.That(cap.Ask(10f + i * 0.01f, 6), Is.True, "play " + i);
            Assert.That(cap.Ask(10.2f, 6), Is.False, "the seventh in the second");
            Assert.That(cap.Ask(11.05f, 6), Is.True, "a second later");
            Assert.That(cap.Ask(11.06f, 0), Is.False, "a cap of none");
            Assert.That(cap.Ask(2f, 6), Is.True, "a clock that went back starts anew");
        }

        // ---------------- Helpers ----------------

        static int Crossings(float[] s, int from, int to)
        {
            int n = 0;
            for (int i = Math.Max(1, from); i < to; i++) if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }

        static float Rms(float[] s, float from, float to)
        {
            int a = (int)(from * Synth.SampleRate), b = Math.Min(s.Length, (int)(to * Synth.SampleRate));
            double sum = 0;
            for (int i = a; i < b; i++) sum += s[i] * s[i];
            return b > a ? (float)Math.Sqrt(sum / (b - a)) : 0f;
        }

        /// <summary>The share of a sound's energy under about 150 Hz (a one-pole low-pass), 0 to 1.</summary>
        static float Lowness(float[] s)
        {
            double k = 1.0 - Math.Exp(-2.0 * Math.PI * 150.0 / Synth.SampleRate), low = 0, all = 0, lowSum = 0;
            for (int i = 0; i < s.Length; i++)
            {
                low += k * (s[i] - low);
                lowSum += low * low;
                all += s[i] * s[i];
            }
            return all > 0 ? (float)(lowSum / all) : 0f;
        }
    }
}
