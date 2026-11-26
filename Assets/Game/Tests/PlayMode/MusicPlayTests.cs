using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The music (work order 116, a trial; Kristian, 2026-11-24, of his own loops: "Må høre det i spillet først";
    /// <see cref="MusicPlayer"/>), in the ControlRoom scene through the game's own intents: his two clips load, PCM, whole bars to the
    /// sample at 128 BPM; a room's fight waking tells the fight loop to start on the next bar line of the music's clock and its
    /// clearing the calm loop, each landing there, the old loop fading as the order says, the recorder told; the pause keeps it
    /// playing at half; her death fades it out and the end screen is silent, a new run calm on the next bar; the pause menu's
    /// two lines turn it and step its volume; the switch off is silent and makes nothing; nothing is allocated a frame in calm or
    /// in a fight; a missing clip is silence without an error; and asked before it has woken it answers.
    ///
    /// The music's clock is the test's own (<see cref="MusicPlayer.Clock"/>), moved by hand, at a 48 kHz rate: the engine's audio
    /// clock is not needed and nothing waits for it. Nobody hears anything here (the listener is turned to 0 for the tests): what
    /// is checked is what each source is told, when, and at what level. She cannot be hurt but where a test kills her.
    /// </summary>
    public class MusicPlayTests
    {
        const ulong RunSeed = 0x41414141UL; // the room fights' tests' run: a combat room after the first passage
        const int Rate = 48000;
        const double Start = 100.0;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        MusicPlayer _music;
        RunHud _hud;
        PlaySessionRecorder _recorder;
        string _folder;
        double _now;
        float _listenerWas;
        MusicValues _valuesWas;
        Func<double> _clockWas;
        Func<int> _rateWas;
        Func<string, AudioClip> _clipsWas;
        readonly List<MusicSchedule> _told = new List<MusicSchedule>();
        readonly List<MusicLanding> _landed = new List<MusicLanding>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _listenerWas = AudioListener.volume;
            AudioListener.volume = 0f; // nothing out of the speakers
            _valuesWas = MusicPlayer.Values;
            _clockWas = MusicPlayer.Clock;
            _rateWas = MusicPlayer.OutputRate;
            _clipsWas = MusicPlayer.Clips;
            MusicPlayer.Values = new MusicValues();
            _now = Start;
            MusicPlayer.Clock = () => _now;
            MusicPlayer.OutputRate = () => Rate;
            MusicPlayer.Scheduled += OnTold;
            MusicPlayer.Landed += OnLanded;
            _told.Clear();
            _landed.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            MusicPlayer.Scheduled -= OnTold;
            MusicPlayer.Landed -= OnLanded;
            if (_music != null) Object.Destroy(_music.gameObject);
            _music = null;
            if (MusicPlayer.Current != null) Object.Destroy(MusicPlayer.Current.gameObject);
            MusicPlayer.Values = _valuesWas;
            MusicPlayer.Clock = _clockWas;
            MusicPlayer.OutputRate = _rateWas;
            MusicPlayer.Clips = _clipsWas;
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_health != null) _health.Untouchable = false;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_hud != null)
            {
                Object.Destroy(_hud.gameObject);
                GameSettings.Unload();
            }
            _hud = null;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            AudioListener.volume = _listenerWas;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _told.Clear();
            _landed.Clear();
        }

        void OnTold(MusicSchedule s) => _told.Add(s);
        void OnLanded(MusicLanding l) => _landed.Add(l);

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static double Bar => MusicClock.BarSeconds(MusicPlayer.Values);
        static double Beat => MusicClock.BeatSeconds(MusicPlayer.Values);

        /// <summary>The music's clock moved to a time, and two frames for the player (and the floor) to see it.</summary>
        IEnumerator ClockTo(double t)
        {
            _now = t;
            yield return Frames(2);
        }

        GeneratedLevel StartRun()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        /// <summary>A run begun and the music started: calm landed on bar 0.</summary>
        IEnumerator CalmInARun()
        {
            StartRun();
            _music = MusicPlayer.Ensure();
            yield return Frames(2);
            Assert.That(_music.ClockStarted, Is.True, "the clock started at the first look");
            Assert.That(_music.LineTime(0), Is.EqualTo(Start + MusicPlayer.Values.LeastLead).Within(1.0 / Rate), "its origin the least lead ahead");
            yield return ClockTo(_music.LineTime(0) + 0.01);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "calm in the run");
            Assert.That(_music.MainSource, Is.Not.Null);
            Assert.That(_music.MainSource.clip, Is.SameAs(_music.ClipOf(MusicState.Calm)));
        }

        (LevelRoom room, RoomFight fight, List<Enemy> own) FirstFight()
        {
            GeneratedLevel level = _levels.Level;
            LevelRoom room = level.Layout.Rooms[2];
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat), "the run's first combat room, after its passage");
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "it has its fight");
            return (room, fight, level.EnemiesByRoom[room.Order]);
        }

        /// <summary>Her into the room's middle and one of its enemies hit: the room's fight wakes.</summary>
        IEnumerator Wake(LevelRoom room, RoomFight fight, List<Enemy> own)
        {
            GeneratedLevel level = _levels.Level;
            _player.Teleport(level.ToWorld(room.Middle(level.Layout.CellSize)));
            own.First(e => !e.IsDead).Body.TakeHit(1);
            for (int i = 0; i < 30 && !fight.On; i++) yield return null;
            Assert.That(fight.On, Is.True, "the room's fight is on");
            yield return Frames(2);
        }

        IEnumerator Clear(LevelRoom room, RoomFight fight, List<Enemy> own)
        {
            foreach (Enemy e in own) if (e != null && !e.IsDead) e.Body.TakeHit(100000000);
            if (_levels.Level.CalledByRoom.TryGetValue(room.Order, out List<Enemy> called))
                foreach (Enemy e in called) if (e != null && !e.IsDead) e.Body.TakeHit(100000000);
            for (int i = 0; i < 30 && fight.On; i++) yield return null;
            Assert.That(fight.On, Is.False, "the room cleared, its fight over");
            yield return Frames(2);
        }

        /// <summary>The told line is a line of the music's clock: its bar's own, after the ask by at least the least lead and by no more than a bar and it.</summary>
        void OnItsLine(MusicSchedule s, string what)
        {
            Assert.That(s.Line, Is.EqualTo(_music.LineTime(s.Bar)).Within(1e-9), what + ": on its bar's line");
            double bars = (s.Line - _music.LineTime(0)) / Bar;
            Assert.That(Math.Abs(bars - Math.Round(bars)), Is.LessThan(1e-4), what + ": a whole number of bars from the start (" + bars + ")");
            Assert.That(s.Line - s.AskedAt, Is.GreaterThanOrEqualTo(MusicPlayer.Values.LeastLead - 1e-9), what + ": at least the least lead after the ask");
            Assert.That(s.Line - s.AskedAt, Is.LessThanOrEqualTo(Bar + MusicPlayer.Values.LeastLead + 1e-9), what + ": within a bar of it (1.875 s)");
        }

        float Basic => _player.ShotLooks != null && _player.ShotLooks.Basic != null ? _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume : 1f;

        static int SourcesInScene() => Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;

        // ---------------- His clips ----------------

        [UnityTest]
        public IEnumerator HisClips_Load_UncompressedAtTheirOwnRate_AndAreWholeBarsToTheSample_At128Bpm()
        {
            MusicValues v = new MusicValues();
            foreach ((string name, long samples, long bars) in new[] { (MusicClips.Calm, 661500L, 8L), (MusicClips.Fight, 1323000L, 16L) })
            {
                var clip = Resources.Load<AudioClip>(MusicClips.PathOf(name));
                Assert.That(clip, Is.Not.Null, name + " in Resources/Music");
                Assert.That(clip.frequency, Is.EqualTo(44100), name + ": its own 44.1 kHz");
                Assert.That(clip.channels, Is.EqualTo(2), name + ": stereo");
                Assert.That(clip.samples, Is.EqualTo(samples), name + ": every sample of the file, none padded or cut");
                Assert.That(MusicClock.IsWholeBars(v, clip.samples, clip.frequency, out long whole), Is.True, name + ": whole bars");
                Assert.That(whole, Is.EqualTo(bars), name);
                Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), name + ": decompressed on load, in memory");
#if UNITY_EDITOR
                var importer = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Game/Resources/Music/" + name + ".wav");
                Assert.That(importer, Is.Not.Null, name);
                UnityEditor.AudioImporterSampleSettings s = importer.defaultSampleSettings;
                Assert.That(s.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM), name + ": PCM, no codec padding its ends");
                Assert.That(s.sampleRateSetting, Is.EqualTo(UnityEditor.AudioSampleRateSetting.PreserveSampleRate), name + ": not resampled at import");
                Assert.That(s.preloadAudioData, Is.True, name + ": loaded before it is needed");
                Assert.That(importer.forceToMono, Is.False, name);
#endif
            }
            Assert.That(Resources.Load<AudioClip>(MusicClips.PathOf(MusicClips.Boss)), Is.Null, "no Boss clip yet");
            Assert.That(Resources.Load<AudioClip>(MusicClips.PathOf(MusicClips.Cleared)), Is.Null, "no RoomCleared clip yet");
            _music = MusicPlayer.Ensure();
            yield return null;
            Assert.That(_music.ClipOf(MusicState.Boss), Is.SameAs(_music.ClipOf(MusicState.Fight)), "the boss's room plays the fight loop");
            Assert.That(_music.ClearedClip, Is.Null, "and a room cleared has no sound of its own");
        }

        // ---------------- On the bar ----------------

        [UnityTest]
        public IEnumerator ARoomsFightWaking_TellsTheFightLoopToStartOnTheNextBarLine_AndItsClearing_TheCalmLoop_EachLandingThere()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeMusicTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return CalmInARun();
            AudioSource calm = _music.MainSource;
            (LevelRoom room, RoomFight fight, List<Enemy> own) = FirstFight();
            yield return ClockTo(_music.LineTime(3) + 0.4 * Bar); // well into bar 3
            Assert.That(fight.On, Is.False);
            _told.Clear();

            yield return Wake(room, fight, own);
            Assert.That(_told.Count, Is.EqualTo(1), "one change told");
            MusicSchedule toFight = _told[0];
            Assert.That(toFight.State, Is.EqualTo(MusicState.Fight));
            Assert.That(toFight.Bar, Is.EqualTo(4), "the next bar line");
            Assert.That(toFight.Clip, Is.SameAs(_music.ClipOf(MusicState.Fight)), "his fight loop, from its first sample");
            OnItsLine(toFight, "the fight");
            Assert.That(_music.NextSource, Is.Not.Null.And.Not.SameAs(calm), "on a source of its own");
            Assert.That(_music.NextSource.clip, Is.SameAs(toFight.Clip));
            Assert.That(_music.NextSource.timeSamples, Is.EqualTo(0), "from its first sample");
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "calm until the line");
            float calmFull = MusicPlayer.Values.CalmLevel * Basic * MusicPlayer.Values.Volume;
            Assert.That(calm.volume, Is.EqualTo(calmFull).Within(1e-4), "the calm at its level until the line");

            yield return ClockTo(toFight.Line + 0.001);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Fight), "landed on its line");
            Assert.That(_landed.Last().To, Is.EqualTo(MusicState.Fight));
            Assert.That(_landed.Last().Bar, Is.EqualTo(4));
            AudioSource fightSource = _music.MainSource;
            Assert.That(fightSource.clip, Is.SameAs(_music.ClipOf(MusicState.Fight)));
            Assert.That(fightSource.loop, Is.True, "it goes round for as long as the fight holds");
            Assert.That(fightSource.volume, Is.EqualTo(MusicPlayer.Values.FightLevel * Basic * MusicPlayer.Values.Volume).Within(1e-4), "the fight at its level");
            yield return ClockTo(toFight.Line + Beat * 0.5);
            Assert.That(calm.volume, Is.EqualTo(calmFull * Mathf.Sqrt(0.5f)).Within(2e-3), "the calm half-way out, half a beat after the line");
            yield return ClockTo(toFight.Line + Beat + 0.001);
            Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Count(_music.InUse), Is.EqualTo(1), "the calm gone within a beat: only the fight's source in use");

            _told.Clear();
            yield return ClockTo(_music.LineTime(9) + 0.2 * Bar);
            yield return Clear(room, fight, own);
            Assert.That(_told.Count, Is.EqualTo(1), "one change told");
            MusicSchedule toCalm = _told[0];
            Assert.That(toCalm.State, Is.EqualTo(MusicState.Calm));
            Assert.That(toCalm.Bar, Is.EqualTo(10), "the next bar line");
            Assert.That(toCalm.Clip, Is.SameAs(_music.ClipOf(MusicState.Calm)));
            Assert.That(toCalm.Cleared, Is.False, "no room-cleared clip: nothing over it");
            OnItsLine(toCalm, "the calm");
            yield return ClockTo(toCalm.Line + 0.001);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "calm landed on its line");
            float fightFull = MusicPlayer.Values.FightLevel * Basic * MusicPlayer.Values.Volume;
            yield return ClockTo(toCalm.Line + Bar); // half-way through the fight's two bars
            Assert.That(fightSource.volume, Is.EqualTo(fightFull * Mathf.Sqrt(0.5f)).Within(2e-3), "the fight fading over two bars, half-way at one");
            Assert.That(_music.MainSource.volume, Is.EqualTo(calmFull).Within(1e-4), "the calm in under it, at its level");
            yield return ClockTo(toCalm.Line + 2 * Bar + 0.001);
            Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Count(_music.InUse), Is.EqualTo(1), "the fight gone after two bars");

            List<SessionEvent> notes = _recorder.Events.Where(e => e.Type == MusicEvents.Change).ToList();
            Assert.That(notes.Select(e => e.ExtraValue("from") + ">" + e.ExtraValue("to") + "@" + e.ExtraValue("bar")),
                        Is.EqualTo(new[] { "none>calm@0", "calm>fight@4", "fight>calm@10" }), "the recorder told each change: from, to, the bar");
            Assert.That(_music.SourcesMade, Is.EqualTo(MusicPlayer.VoiceCount), "three sources, made once");
        }

        // ---------------- The practice room and the room lab ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoom_IsCalm_AndTheRoomLab_CalmUntilItsRoomsFightWakes_ThenTheFight()
        {
            _music = MusicPlayer.Ensure();
            yield return Frames(2);
            yield return ClockTo(_music.LineTime(0) + 0.01);
            Assert.That(_levels.IsInLevel, Is.False, "the practice room");
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "calm in the practice room");

            LabEntry entry = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null);
            GeneratedLevel level = _levels.StartLab(entry, 0, 0x116UL);
            Assert.That(level, Is.Not.Null, entry + " was built");
            yield return Frames(3);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "the lab room asleep: calm");
            Assert.That(_music.HasPending, Is.False);
            LevelRoom room = level.Layout.Rooms[1];
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "the lab room has its fight");
            _told.Clear();
            yield return ClockTo(_music.LineTime(2) + 0.5 * Bar);
            yield return Wake(room, fight, level.EnemiesByRoom[room.Order]);
            Assert.That(_told.Count, Is.EqualTo(1));
            Assert.That((_told[0].State, _told[0].Bar), Is.EqualTo((MusicState.Fight, 3L)), "the lab room's fight: the fight loop on the next bar");
            OnItsLine(_told[0], "the lab's fight");
        }

        // ---------------- On the engine's own audio clock ----------------

        [UnityTest]
        public IEnumerator OnTheEnginesAudioClock_TheCalmLoopStartsOnItsLine_ItsPlaceInTheClipIsTheTimeSinceThatLine()
        {
            MusicPlayer.Clock = _clockWas;  // the engine's: AudioSettings.dspTime
            MusicPlayer.OutputRate = _rateWas;
            double first = AudioSettings.dspTime;
            float until = Time.realtimeSinceStartup + 1f;
            while (AudioSettings.dspTime == first && Time.realtimeSinceStartup < until) yield return null;
            Assume.That(AudioSettings.dspTime, Is.GreaterThan(first), "this machine's Unity has a running audio clock (no device: nothing to measure)");
            _music = MusicPlayer.Ensure();
            yield return null;
            until = Time.realtimeSinceStartup + 2f;
            while (_music.Playing != MusicState.Calm && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "landed on the engine's clock");
            Assert.That(_music.Rate, Is.EqualTo(AudioSettings.outputSampleRate), "counted in the output's own samples");
            AudioSource calm = _music.MainSource;
            double line = _music.LineTime(0);
            until = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            double since = AudioSettings.dspTime - line;
            Assert.That(calm.isPlaying, Is.True, "the calm plays");
            double heard = calm.timeSamples / (double)calm.clip.frequency;
            // dspTime and the source's place both move a mixing buffer at a time (about 21 ms), and the mixer works ahead: within a tenth of a second (a bar is 1.875 s).
            Assert.That(heard, Is.EqualTo(since).Within(0.1), "its place in the clip is the time since its line: it began there, on its first sample");
        }

        // ---------------- The screens, her death, a new run ----------------

        [UnityTest]
        public IEnumerator ThePause_KeepsItPlayingAtHalf_HerDeath_FadesItOutOverHalfASecond_TheEndScreenIsSilent_ANewRunIsCalmOnTheNextBar()
        {
            yield return CalmInARun();
            AudioSource calm = _music.MainSource;
            float full = MusicPlayer.Values.CalmLevel * Basic * MusicPlayer.Values.Volume;
            Assert.That(_levels.Pause(), Is.True);
            float until = Time.realtimeSinceStartup + 1f;
            while (_music.HeldNow > MusicPlayer.Values.HeldShare && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Assert.That(_music.HeldNow, Is.EqualTo(MusicPlayer.Values.HeldShare), "under the pause menu: half");
            Assert.That(_music.MainSource, Is.SameAs(calm), "still the calm, playing on");
            Assert.That(calm.volume, Is.EqualTo(full * MusicPlayer.Values.HeldShare).Within(1e-4));
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm));
            _levels.Resume();
            until = Time.realtimeSinceStartup + 1f;
            while (_music.HeldNow < 1f && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Assert.That(calm.volume, Is.EqualTo(full).Within(1e-4), "back to its level");

            yield return ClockTo(_music.LineTime(2) + 0.3);
            double died = _now;
            _health.Untouchable = false;
            _health.TakeHit(100000000);
            yield return Frames(3);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died));
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Silent), "silent at her death, at once");
            Assert.That(_landed.Last().AtOnce, Is.True);
            yield return ClockTo(died + MusicPlayer.Values.EndFade * 0.5);
            Assert.That(calm.volume, Is.EqualTo(full * Mathf.Sqrt(0.5f)).Within(2e-2), "half-way out a quarter second after (the screens' share may still glide)");
            yield return ClockTo(died + MusicPlayer.Values.EndFade + 0.01);
            Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Any(_music.InUse), Is.False, "gone after half a second: the end screen is silent");
            until = Time.realtimeSinceStartup + 3f;
            while (!_levels.EndScreenShown && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
            yield return Frames(3);
            Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Any(_music.InUse), Is.False, "nothing on the end screen");
            _health.Untouchable = true;

            _told.Clear();
            yield return ClockTo(_music.LineTime(7) + 0.7 * Bar);
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            yield return Frames(3);
            Assert.That(_told.Count, Is.EqualTo(1));
            Assert.That(_told[0].State, Is.EqualTo(MusicState.Calm), "a new run: calm");
            Assert.That(_told[0].Bar, Is.EqualTo(8), "on the next bar of the same clock");
            OnItsLine(_told[0], "the new run's calm");
        }

        // ---------------- The pause menu ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusTwoLines_StandBeforeTheAttachments_TurnTheMusicOffAndOn_AndStepItsVolume()
        {
            _hud = RunHud.Ensure();
            AudioListener.volume = 0f; // the HUD put the game's own volume back on the listener
            yield return CalmInARun();
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            Assert.That(_hud.Escape(), Is.True, "paused");
            float until = Time.realtimeSinceStartup + 1f;
            while (_music.HeldNow > MusicPlayer.Values.HeldShare && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Assert.That(_music.HeldNow, Is.EqualTo(MusicPlayer.Values.HeldShare), "under the menu, at its share");
            List<RunHud.MenuLine> lines = _hud.MenuLines.ToList();
            int music = lines.IndexOf(RunHud.MenuLine.Music), volume = lines.IndexOf(RunHud.MenuLine.MusicVolume);
            Assert.That(music, Is.GreaterThan(0), "the music's line is shown");
            Assert.That(volume, Is.EqualTo(music + 1), "its volume right under it");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(lines.IndexOf(RunHud.MenuLine.BetweenRuns), Is.EqualTo(music - 1), "under the between-runs line");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Music), Is.EqualTo("Music:  on"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.MusicVolume), Is.EqualTo("Music volume:  100 %"));
            yield return null;
            Assert.That(_hud.PauseRowWords[music], Is.EqualTo("Music"));
            Assert.That(_hud.PauseRowValues[music], Is.EqualTo("on"), "a setting's row, its value apart");
            Assert.That(_hud.PauseRowValues[volume], Is.EqualTo("100 %"));

            // The volume: a tenth down, up and stopping at 100 %, Enter round to 0.
            float before = _music.MainSource.volume;
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.MusicVolume, -1), Is.True);
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(9));
            Assert.That(_hud.MenuText(RunHud.MenuLine.MusicVolume), Is.EqualTo("Music volume:  90 %"));
            yield return Frames(2);
            Assert.That(_music.MainSource.volume, Is.EqualTo(before * 0.9f).Within(1e-3), "the loop heard a tenth quieter, at once");
            _hud.ChangeSetting(RunHud.MenuLine.MusicVolume, 1);
            _hud.ChangeSetting(RunHud.MenuLine.MusicVolume, 1);
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(10), "stops at 100 %");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.MusicVolume), Is.True);
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(0), "Enter on it at 100 %: round to 0");
            yield return Frames(2);
            Assert.That(_music.MainSource.volume, Is.EqualTo(0f), "0 %: silent");
            _hud.PickMenuLine(RunHud.MenuLine.MusicVolume);
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(1));
            MusicPlayer.Values.VolumeStep = 10;

            // The switch: off stops it at once, on starts it again on the next bar.
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Music, 1), Is.True);
            Assert.That(MusicPlayer.Values.On, Is.False, "the order's switch, off");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Music), Is.EqualTo("Music:  off"));
            yield return Frames(2);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.None), "off");
            Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Any(_music.InUse), Is.False, "every source stopped at once");
            _told.Clear();
            yield return ClockTo(_music.LineTime(5) + 0.5 * Bar);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Music), Is.True);
            Assert.That(MusicPlayer.Values.On, Is.True, "Enter on it: on again");
            yield return Frames(2);
            Assert.That(_told.Count, Is.EqualTo(1));
            Assert.That((_told[0].State, _told[0].Bar), Is.EqualTo((MusicState.Calm, 6L)), "calm again on the next bar of the same clock");
            Assert.That(_music.SourcesMade, Is.EqualTo(MusicPlayer.VoiceCount), "no source made for it");
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_IsSilent_AndMakesNothing_ThroughARoomsFight()
        {
            MusicPlayer.Values.On = false;
            int before = SourcesInScene();
            StartRun();
            yield return Frames(2);
            int withFloor = SourcesInScene();
            _music = MusicPlayer.Ensure();
            yield return Frames(3);
            (LevelRoom room, RoomFight fight, List<Enemy> own) = FirstFight();
            yield return Wake(room, fight, own);
            yield return ClockTo(Start + 10.0);
            yield return Clear(room, fight, own);
            yield return ClockTo(Start + 20.0);
            Assert.That(_music.SourcesMade, Is.EqualTo(0), "no audio source made");
            Assert.That(_music.GetComponents<AudioSource>(), Is.Empty, "none on it");
            Assert.That(SourcesInScene(), Is.EqualTo(withFloor), "the scene's sources as without it");
            Assert.That(_music.ClipsLoaded, Is.False, "no clip loaded");
            Assert.That(_music.ClipOf(MusicState.Calm), Is.Null);
            Assert.That(_told, Is.Empty, "nothing told to start");
            Assert.That(_landed, Is.Empty, "nothing changed");
            Assert.That(_music.Playing, Is.EqualTo(MusicState.None));
            Assert.That(_music.ClockStarted, Is.False, "its clock never started");
            Assert.That(before, Is.LessThanOrEqualTo(withFloor));
        }

        // ---------------- Nothing a frame ----------------

        [UnityTest]
        public IEnumerator NothingIsAllocatedPerFrame_InCalm_AndInAFight()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            yield return CalmInARun();
            yield return ClockTo(_music.LineTime(1) + 0.3);
            _music.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) _music.Tick(); }, NotAllocating, "a frame in calm allocates nothing");
            (LevelRoom room, RoomFight fight, List<Enemy> own) = FirstFight();
            yield return Wake(room, fight, own);
            Assert.That(_music.HasPending, Is.True);
            yield return ClockTo(_music.PendingLine + 2.0);
            Assert.That(_music.Playing, Is.EqualTo(MusicState.Fight));
            _music.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) _music.Tick(); }, NotAllocating, "a frame in a fight allocates nothing");
            Assert.That(fight.On, Is.True, "still the fight");
        }

        static byte[] s_junk;

        // ---------------- A missing clip ----------------

        [UnityTest]
        public IEnumerator AMissingClip_IsSilenceForItsState_WithoutAnError()
        {
            var logged = new List<string>();
            Application.LogCallback listen = (text, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || text.Contains("[MusicPlayer]")) logged.Add(type + ": " + text);
            };
            Application.logMessageReceived += listen;
            try
            {
                MusicPlayer.Clips = path => path == MusicClips.PathOf(MusicClips.Fight) ? null : Resources.Load<AudioClip>(path);
                yield return CalmInARun();
                AudioSource calm = _music.MainSource;
                (LevelRoom room, RoomFight fight, List<Enemy> own) = FirstFight();
                _told.Clear();
                yield return Wake(room, fight, own);
                Assert.That(_told.Count, Is.EqualTo(1));
                Assert.That(_told[0].State, Is.EqualTo(MusicState.Fight));
                Assert.That(_told[0].Clip, Is.Null, "no fight clip: nothing to start");
                Assert.That(_music.NextSource, Is.Null);
                yield return ClockTo(_told[0].Line + Beat + 0.01);
                Assert.That(_music.Playing, Is.EqualTo(MusicState.Fight), "the fight, silent");
                Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Any(_music.InUse), Is.False, "the calm faded out as for a fight; nothing plays");
                Assert.That(calm.isPlaying && calm.volume > 0f, Is.False);
                yield return Clear(room, fight, own);
                yield return ClockTo(_music.PendingLine + 0.01);
                Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm), "calm again after the fight");
                Assert.That(_music.MainSource, Is.Not.Null);

                // Every clip missing: nothing plays at all, and nothing is logged.
                Object.Destroy(_music.gameObject);
                _music = null;
                yield return null;
                MusicPlayer.Clips = _ => null;
                _music = MusicPlayer.Ensure();
                yield return Frames(3);
                yield return ClockTo(_now + 5.0);
                Assert.That(_music.Playing, Is.EqualTo(MusicState.Calm));
                Assert.That(Enumerable.Range(0, MusicPlayer.VoiceCount).Any(_music.InUse), Is.False, "silence");
            }
            finally
            {
                Application.logMessageReceived -= listen;
            }
            Assert.That(logged, Is.Empty, "no error, no warning of the music's");
        }

        // ---------------- Asked before it has woken ----------------

        [UnityTest]
        public IEnumerator AskedBeforeItHasWoken_ItAnswersWithNothing_AndOnWaking_TheSwitchOnMakesItsThreeSources_OffNone()
        {
            foreach (bool on in new[] { true, false })
            {
                MusicPlayer.Values = new MusicValues { On = on };
                var go = new GameObject("MusicBeforeAwake");
                go.SetActive(false); // nothing on it wakes
                MusicPlayer music = go.AddComponent<MusicPlayer>();
                try
                {
                    Assert.That(music.Playing, Is.EqualTo(MusicState.None), "not yet woken: none, and no exception");
                    Assert.That(music.SourcesMade, Is.EqualTo(0));
                    Assert.That(music.ClipOf(MusicState.Calm), Is.Null);
                    Assert.That(music.MainSource, Is.Null);
                    Assert.That(music.HasPending, Is.False);
                    Assert.That(double.IsNaN(music.PendingLine), Is.True);
                    go.SetActive(true); // Awake reads the switch
                    Assert.That(music.SourcesMade, Is.EqualTo(on ? MusicPlayer.VoiceCount : 0), "on: three sources at waking; off: none");
                    Assert.That(music.GetComponents<AudioSource>().Length, Is.EqualTo(on ? MusicPlayer.VoiceCount : 0));
                    Assert.That(music.ClipsLoaded, Is.EqualTo(on));
                    if (on) Assert.That(music.ClipOf(MusicState.Calm), Is.Not.Null, "his calm loop loaded");
                    Assert.That(MusicPlayer.Current, Is.SameAs(music));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
                Assert.That(MusicPlayer.Current, Is.Null, "gone with it");
                yield return null;
            }
        }
    }
}
