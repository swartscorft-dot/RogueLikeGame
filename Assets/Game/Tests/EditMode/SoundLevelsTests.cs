using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The sound's levels (work order 131; a friend's first run: "Lyden er helt sjukt høy første gang man logger inn, også er det
    /// forskjell i volum på de høye skytelydene"; <see cref="SoundLevels"/>): the master trim on the listener, the step for a first
    /// start, a saved step left alone, each shot recording's gain, and the switch off giving every number as before. The
    /// measurements the gains come from (ffmpeg's ebur128 on 2026-11-25) are written here, so the levelling itself is checked:
    /// one shot's variations within a decibel of each other, the skills' shots only the differences with a reason.
    /// </summary>
    public class SoundLevelsTests
    {
        /// <summary>A shot as played before the order at a full master, from the measurement: momentary loudness, LUFS.</summary>
        struct Measured
        {
            public string Name, Clip;
            public float Lufs;
            public float RoleTarget; // its role's target, LUFS (a skill's shot -14, heavy -13, a stream -17)

            public Measured(string name, string clip, float lufs, float target)
            {
                Name = name;
                Clip = clip;
                Lufs = lufs;
                RoleTarget = target;
            }
        }

        const float Skill = -14f, Heavy = -13f, Stream = -17f;

        static readonly Measured[] Shots =
        {
            new Measured("Basic shot, variation 0 (open, pitch 1, whole tail)", "gunshot_54", -11.9f, Stream),
            new Measured("Basic shot, variation 1 (9 kHz, 0.95, 0.75 of its tail)", "gunshot_54", -11.7f, Stream),
            new Measured("Basic shot, variation 2 (15 kHz, 1.04, 0.9)", "gunshot_54", -12.0f, Stream),
            new Measured("Basic shot, variation 3 (6 kHz, 0.98, 0.6)", "gunshot_54", -11.9f, Stream),
            new Measured("Quick Shot", "gunshot_42", -13.0f, Skill),
            new Measured("Fan Fire, a tick", "gunshot_6", -13.2f, Stream),
            new Measured("Long Shot", "deagle_50ae", -16.6f, Heavy),
            new Measured("Scatter Blast", "shotgun_blast", -13.3f, Heavy),
            new Measured("Powder Keg (0.85 pitch)", "shotgun_blast", -13.2f, Heavy),
        };

        bool _was;

        [SetUp]
        public void Remember() => _was = SoundLevelsValues.On;

        [TearDown]
        public void PutBack() => SoundLevelsValues.On = _was;

        static float Db(float gain) => (float)(20.0 * Math.Log10(gain));

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheValuesAreTheOrdersChoices()
        {
            Assert.That(_was, Is.True, "work order 131's switch, on");
            var v = new SoundLevelsValues();
            Assert.That(v.MasterTrimDb, Is.EqualTo(-6f));
            Assert.That(v.FirstStartVolumeStep, Is.EqualTo(8));
            Assert.That(v.ClipGains.Length, Is.EqualTo(5), "one gain for each of the five shot recordings");
        }

        [Test]
        public void TheTrim_IsUnderEveryStep_SixDecibels()
        {
            SoundLevelsValues.On = true;
            Assert.That(SoundLevels.Trim(), Is.EqualTo(0.5012f).Within(1e-4f), "-6 dB");
            for (int step = 0; step <= SettingsRules.VolumeSteps; step++)
                Assert.That(SoundLevels.ListenerVolume(step), Is.EqualTo(step / 10f * 0.501187f).Within(1e-5f), "step " + step);
            Assert.That(Db(SoundLevels.ListenerVolume(10)), Is.EqualTo(-6f).Within(1e-3f), "the top step: the trim alone");
            Assert.That(SoundLevels.ListenerVolume(0), Is.EqualTo(0f), "0 is silence still");
            var other = new SoundLevelsValues { MasterTrimDb = -10f };
            Assert.That(Db(SoundLevels.ListenerVolume(10, other)), Is.EqualTo(-10f).Within(1e-3f), "the named value is the one used");
        }

        [Test]
        public void AFirstStart_IsStepEight_AndTheLoudestMomentMeasuredCannotClipAtTheTopStep()
        {
            SoundLevelsValues.On = true;
            Assert.That(SoundLevels.FirstStartStep(), Is.EqualTo(8));
            Assert.That(SoundLevels.StepAtStart(false, 0), Is.EqualTo(8), "nothing saved: the first start's step");
            Assert.That(Db(SoundLevels.ListenerVolume(SoundLevels.FirstStartStep())), Is.EqualTo(-7.94f).Within(0.01f), "80 % under the trim");
            // The measurement (ffmpeg, the shots levelled, no trim): the loudest moment +4.7 dBTP, her ordinary fight -14.7 LUFS.
            const float loudestPeak = 4.7f, ordinary = -14.7f;
            Assert.That(loudestPeak + Db(SoundLevels.ListenerVolume(10)), Is.LessThanOrEqualTo(-1f), "at 10 of 10: under -1 dBTP");
            Assert.That(ordinary + Db(SoundLevels.ListenerVolume(SoundLevels.FirstStartStep())), Is.EqualTo(-22.6f).Within(0.1f),
                        "a first start's fight: about -23 LUFS");
        }

        [Test]
        public void ASavedStep_IsKeptAsItIs()
        {
            SoundLevelsValues.On = true;
            for (int step = 0; step <= SettingsRules.VolumeSteps; step++)
                Assert.That(SoundLevels.StepAtStart(true, step), Is.EqualTo(step), "saved " + step + ": kept");
            Assert.That(SoundLevels.StepAtStart(true, 10), Is.EqualTo(10), "someone who chose the top keeps it");
            Assert.That(SoundLevels.StepAtStart(true, 14), Is.EqualTo(10), "made safe as before");
        }

        [Test]
        public void EachClipsGain_IsTheNamedOne_AndAClipNotListedPlaysAsItDid()
        {
            SoundLevelsValues.On = true;
            (string clip, float db)[] expected =
            {
                ("gunshot_54", -5.1f), ("gunshot_42", -1.0f), ("gunshot_6", -3.8f), ("deagle_50ae", 3.6f), ("shotgun_blast", 0.3f),
            };
            foreach ((string clip, float db) in expected)
            {
                Assert.That(SoundLevels.ClipGainDb(clip), Is.EqualTo(db), clip);
                Assert.That(SoundLevels.ClipGainOf(clip), Is.EqualTo((float)Math.Pow(10.0, db / 20.0)).Within(1e-6f), clip);
            }
            Assert.That(SoundLevels.ClipGainOf("BossAnger"), Is.EqualTo(1f), "his roar: not a shot, not touched");
            Assert.That(SoundLevels.ClipGainOf("Explore"), Is.EqualTo(1f), "the music: not touched");
            Assert.That(SoundLevels.ClipGainOf(null), Is.EqualTo(1f));
            Assert.That(SoundLevels.ClipGainOf(""), Is.EqualTo(1f));
            Assert.That(SoundLevels.ClipGainOf("Gunshot_54"), Is.EqualTo(1f), "by the exact name");
        }

        [Test]
        public void WithTheGains_OneShotsVariationsAreWithinADecibel_AndEachShotLandsOnItsRolesTarget()
        {
            SoundLevelsValues.On = true;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Measured m in Shots)
            {
                float after = m.Lufs + SoundLevels.ClipGainDb(m.Clip);
                Assert.That(after, Is.EqualTo(m.RoleTarget).Within(0.35f), m.Name + ": " + after + " LUFS");
                if (m.Clip == "gunshot_54")
                {
                    lo = Math.Min(lo, after);
                    hi = Math.Max(hi, after);
                }
            }
            Assert.That(hi - lo, Is.LessThan(1f), "the basic shot's four variations within a decibel of each other (0.3 dB)");
            // Before: 4.7 dB from the loudest (the basic shot) to the quietest (Long Shot). After: only the reasoned 3 + 1 dB.
            Assert.That(Skill - Stream, Is.EqualTo(3f), "a stream of shots 3 dB under a skill's (work order 064's GainDb)");
            Assert.That(Heavy - Skill, Is.EqualTo(1f), "the heavy guns 1 dB over (work order 064's ShotWeight.Heavy)");
        }

        [Test]
        public void TheGains_KeepEveryShotsVolumeUnderOne_SoNothingIsCutAtTheVoice()
        {
            SoundLevelsValues.On = true;
            // The ShotFx asset's volumes (Basic 0.55 x its -3 dB; the others' own), times the gain: a voice clamps at 1.
            (string clip, float volume)[] looks =
            {
                ("gunshot_54", 0.55f * 0.708f), ("gunshot_42", 0.65f), ("gunshot_6", 0.4f), ("deagle_50ae", 0.65f), ("shotgun_blast", 0.87589747f),
            };
            foreach ((string clip, float volume) in looks)
                Assert.That(volume * SoundLevels.ClipGainOf(clip), Is.LessThan(1f), clip);
        }

        [Test]
        public void TheSwitchOff_EveryNumberIsAsBefore()
        {
            SoundLevelsValues.On = false;
            Assert.That(SoundLevels.Trim(), Is.EqualTo(1f), "no trim");
            for (int step = 0; step <= SettingsRules.VolumeSteps; step++)
                Assert.That(SoundLevels.ListenerVolume(step), Is.EqualTo(SettingsRules.Volume(step)), "step " + step + ": its own volume, exactly");
            Assert.That(SoundLevels.FirstStartStep(), Is.EqualTo(SettingsRules.DefaultVolumeStep), "a first start at the top step, as before");
            Assert.That(SoundLevels.StepAtStart(false, 0), Is.EqualTo(10));
            Assert.That(SoundLevels.StepAtStart(true, 7), Is.EqualTo(7));
            foreach (ClipGain g in SoundLevelsValues.Live.ClipGains)
            {
                Assert.That(SoundLevels.ClipGainOf(g.Clip), Is.EqualTo(1f), g.Clip + ": the volume it had");
                Assert.That(0.65f * SoundLevels.ClipGainOf(g.Clip), Is.EqualTo(0.65f), "field for field");
            }
        }
    }
}
