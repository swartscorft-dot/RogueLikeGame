using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>A loop (or the room-cleared sound) told to start on a bar line: for tests and tools, through <see cref="MusicPlayer.Scheduled"/>.</summary>
    public struct MusicSchedule
    {
        public MusicState State;
        /// <summary>The bar line it starts on, by its number on the music's clock (0 is the music's start).</summary>
        public long Bar;
        /// <summary>The audio clock's time (s) of that line, as given to AudioSource.PlayScheduled.</summary>
        public double Line;
        /// <summary>The audio clock's time it was asked at.</summary>
        public double AskedAt;
        /// <summary>The clip it starts from its first sample; null when its state has no clip (silence) or the loop heard plays on.</summary>
        public AudioClip Clip;
        /// <summary>The loop heard plays on into the new state (the same clip: the boss's room with no Boss clip, after a fight).</summary>
        public bool PlaysOn;
        /// <summary>The room-cleared sound is told to start on the same line.</summary>
        public bool Cleared;
    }

    /// <summary>A change of the music's state that landed: for tests and tools, through <see cref="MusicPlayer.Landed"/>.</summary>
    public struct MusicLanding
    {
        public MusicState From, To;
        public long Bar;
        /// <summary>The audio clock's time of its line; for a change at once (silence, off), the time it was seen.</summary>
        public double Line;
        public bool AtOnce;
    }

    /// <summary>
    /// The music (work order 116, a trial; Kristian, 2026-11-24, of the loops he made: "Må høre det i spillet først"): his two
    /// loops, calm (<see cref="MusicClips.Calm"/>) while no room's fight is on and the fight's (<see cref="MusicClips.Fight"/>)
    /// while one is, changing on the bar. Its numbers are <see cref="MusicValues"/> (<see cref="Values"/>, the session's one, read
    /// live); its switch is <see cref="MusicValues.On"/>, which the pause menu's "Music" line turns.
    ///
    /// One clock (<see cref="MusicClock"/>, <see cref="MusicConductor"/>): 128 beats a minute, four a bar, on the audio clock
    /// (AudioSettings.dspTime) from the music's start. When the state the game wants changes, the new loop is told to start from
    /// its first sample on the next bar line at least <see cref="MusicValues.LeastLead"/> away (AudioSource.PlayScheduled: the
    /// audio thread starts it on that sample, whatever the frame), and the loop heard fades out from that line
    /// (<see cref="MusicFade"/>; its end told the audio clock too, AudioSource.SetScheduledEndTime). The fades' levels are set a
    /// frame at a time. A change that flips back before its line is cancelled: the new loop is stopped before it is heard. Each
    /// loop goes round by the engine's own loop, sample for sample; both clips are whole bars, so every round starts on a line.
    ///
    /// What it follows: a room's fight on (any room's, the room lab's too: <see cref="GeneratedLevels.RoomFights"/>) is FIGHT; the
    /// boss's room's fight is BOSS (a clip <see cref="MusicClips.Boss"/>, else the fight loop playing on); none is CALM (the practice
    /// room too). A combat room's fight ending with the room cleared also starts <see cref="MusicClips.Cleared"/> on the calm loop's
    /// line, if there is such a clip. Her death and the act's end fade it out over <see cref="MusicValues.EndFade"/>; it is silent on
    /// the end screen; under the screen between runs and in a new run it is calm from the next bar. The pause menu and the choice
    /// screens do not stop it: it plays on at <see cref="MusicValues.HeldShare"/> of its level. The room tone and every other sound
    /// are as they were.
    ///
    /// Levels: each state's is a share of her basic shot's volume (as the placeholder sounds are counted), times the master music
    /// volume (<see cref="MusicValues.VolumeStep"/>, the pause menu's "Music volume" line), under the game's own volume (the
    /// listener's). Three audio sources at most, made once on its own object the first time the music is on and kept for the
    /// session; nothing made or destroyed after. Its frame allocates nothing.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="PlaceholderSounds"/> (never in a test run); tests make
    /// one with <see cref="Ensure"/>, and may put their own clock, rate and clips in first.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Audio sources it may keep: two loops crossing and the room-cleared sound (or a third loop while an old one still fades).</summary>
        public const int VoiceCount = 3;
        /// <summary>The rate the clock counts in when the engine reports none (no audio device).</summary>
        const int FallbackRate = 48000;
        /// <summary>Real s between looks for her character while she is not found: never every frame.</summary>
        const float HerLookGap = 1f;

        sealed class Voice
        {
            public AudioSource Source;
            public bool Busy;
            public MusicState State;
            public bool Cleared;
            public double StartAt;
            public double FadeFrom = double.PositiveInfinity;
            public double FadeLength;
            public float Applied = -1f;
        }

        /// <summary>The one running now, or null.</summary>
        public static MusicPlayer Current { get; private set; }

        /// <summary>The music's numbers for the session (work order 116): read live; the pause menu's lines change them.</summary>
        public static MusicValues Values = new MusicValues();

        /// <summary>Where a clip is looked for, by its Resources path (<see cref="MusicClips.PathOf"/>): Resources.Load. Tests may put another in; null for none.</summary>
        public static Func<string, AudioClip> Clips = LoadFromResources;

        /// <summary>The audio clock (s): AudioSettings.dspTime. Tests may put their own in before <see cref="Ensure"/>.</summary>
        public static Func<double> Clock = DspTime;

        /// <summary>The audio output's sample rate: AudioSettings.outputSampleRate. Tests may put their own in.</summary>
        public static Func<int> OutputRate = DspRate;

        /// <summary>Every loop told to start on a line, in order. Tests listen here instead of to the speakers.</summary>
        public static event Action<MusicSchedule> Scheduled;
        /// <summary>Every change of state that landed, in order.</summary>
        public static event Action<MusicLanding> Landed;

        static AudioClip LoadFromResources(string path) => Resources.Load<AudioClip>(path);
        static double DspTime() => AudioSettings.dspTime;
        static int DspRate() => AudioSettings.outputSampleRate;

        readonly MusicConductor _conductor = new MusicConductor(null);
        readonly Voice[] _voices = { new Voice(), new Voice(), new Voice() };
        bool _voicesMade, _clipsLoaded, _warned;
        AudioClip _calm, _fight, _boss, _cleared;
        int _main = -1, _next = -1, _sting = -1;
        bool _nextPlaysOn;
        float _held = 1f;
        PlayerCharacter _player;
        PlayerHealth _health;
        float _lookedForHer = float.NegativeInfinity;
        RoomFight _lastFight;
        GeneratedLevel _lastFightLevel;

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The state heard now (the last that landed).</summary>
        public MusicState Playing => _conductor.Playing;
        /// <summary>A change waits for its line.</summary>
        public bool HasPending => _conductor.HasPending;
        public MusicState Pending => _conductor.Pending;
        public long PendingBar => _conductor.PendingBar;
        /// <summary>The audio clock's time of the waiting change's line (NaN with none).</summary>
        public double PendingLine => _conductor.PendingLine;
        /// <summary>The music's clock has started (its origin set).</summary>
        public bool ClockStarted => _conductor.Started;
        /// <summary>The output sample bar 0 falls on, and the rate the clock counts in.</summary>
        public long Origin => _conductor.Origin;
        public int Rate => _conductor.Rate;
        /// <summary>The audio clock's time of a bar line.</summary>
        public double LineTime(long bar) => _conductor.LineTime(bar);
        /// <summary>The audio sources it has made (0 until the music is first on; never more than <see cref="VoiceCount"/>).</summary>
        public int SourcesMade { get; private set; }
        /// <summary>It has looked for its clips (only once the music is on).</summary>
        public bool ClipsLoaded => _clipsLoaded;
        /// <summary>The share of its level it plays at now for the screens (1, or gliding to <see cref="MusicValues.HeldShare"/>).</summary>
        public float HeldNow => _held;
        /// <summary>The changes that landed since it was made.</summary>
        public int ChangesLanded { get; private set; }

        /// <summary>The clip a state plays: the boss's falls back to the fight's; null for silence.</summary>
        public AudioClip ClipOf(MusicState state)
        {
            switch (state)
            {
                case MusicState.Calm: return _calm;
                case MusicState.Fight: return _fight;
                case MusicState.Boss: return _boss != null ? _boss : _fight;
                default: return null;
            }
        }

        /// <summary>The room-cleared sound's clip, or null (none yet).</summary>
        public AudioClip ClearedClip => _cleared;

        /// <summary>One of its audio sources, or null before they are made.</summary>
        public AudioSource Source(int i) => i >= 0 && i < VoiceCount ? _voices[i].Source : null;
        /// <summary>The source of the loop heard now, or null.</summary>
        public AudioSource MainSource => _main >= 0 ? _voices[_main].Source : null;
        /// <summary>The source told to start on the waiting change's line, or null.</summary>
        public AudioSource NextSource => _next >= 0 ? _voices[_next].Source : null;
        /// <summary>A source is in use (playing, fading or told to start).</summary>
        public bool InUse(int i) => i >= 0 && i < VoiceCount && _voices[i].Busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Values = new MusicValues();
            Clips = LoadFromResources;
            Clock = DspTime;
            OutputRate = DspRate;
            Scheduled = null;
            Landed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static MusicPlayer Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<MusicPlayer>();
            return Current;
        }

        void Awake()
        {
            if (Current == null) Current = this;
            // With the switch on, its sources and clips at once; off, nothing until it is turned on.
            MusicValues v = Values;
            if (v != null && v.On) Ready();
        }

        void OnDestroy()
        {
            for (int i = 0; i < VoiceCount; i++)
                if (_voices[i].Source != null) _voices[i].Source.Stop();
            if (Current == this) Current = null;
        }

        void Update() => Tick();

        /// <summary>
        /// The frame's work: what the game wants, the clock's step, a change told to the audio clock or landed, each source's level.
        /// Public for tests (the frame that allocates nothing).
        /// </summary>
        public void Tick()
        {
            MusicValues v = Values;
            if (v == null) Values = v = new MusicValues();
            _conductor.Values = v;
            GeneratedLevels levels = GeneratedLevels.Current;
            FindHer();
            MusicState target = Wanted(v, levels);
            if (target != MusicState.None) Ready();
            double now = Clock != null ? Clock() : AudioSettings.dspTime;
            MusicChange c = _conductor.Step(now, target, RateNow());
            if (c.Landed && !c.AtOnce) Land(v, c);
            if (c.Cancelled) Cancel();
            if (c.Landed && c.AtOnce) LandAtOnce(v, c, now);
            if (c.Scheduled) Schedule(v, c, now, levels);
            SetLevels(v, now, levels);
        }

        static int RateNow()
        {
            int r = OutputRate != null ? OutputRate() : 0;
            return r > 0 ? r : FallbackRate;
        }

        /// <summary>Its sources and clips, made and loaded the first time the music is on, and kept.</summary>
        void Ready()
        {
            if (!_voicesMade)
            {
                _voicesMade = true;
                for (int i = 0; i < VoiceCount; i++)
                {
                    AudioSource s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.loop = true;
                    s.spatialBlend = 0f; // plain 2D: the camera is 28 m up
                    s.priority = 0;      // never the voice the engine drops when many play
                    s.volume = 0f;
                    _voices[i].Source = s;
                    SourcesMade++;
                }
            }
            if (_clipsLoaded) return;
            _clipsLoaded = true;
            _calm = Load(MusicClips.Calm);
            _fight = Load(MusicClips.Fight);
            _boss = Load(MusicClips.Boss);
            _cleared = Load(MusicClips.Cleared);
        }

        AudioClip Load(string name)
        {
            AudioClip clip = null;
            try { clip = Clips?.Invoke(MusicClips.PathOf(name)); }
            catch (Exception e) { Warn(e); }
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip;
        }

        void Warn(Exception e)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[MusicPlayer] " + e.Message);
        }

        void FindHer()
        {
            if (_player != null || Time.unscaledTime - _lookedForHer < HerLookGap) return;
            _lookedForHer = Time.unscaledTime;
            _player = FindAnyObjectByType<PlayerCharacter>();
            _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
        }

        /// <summary>The state the game wants now (<see cref="MusicRules.Target"/>), from what it already knows. Indexed: nothing made.</summary>
        MusicState Wanted(MusicValues v, GeneratedLevels levels)
        {
            bool ended = false, between = false, any = false, boss = false;
            if (levels != null)
            {
                ended = levels.Ending != GeneratedLevels.EndKind.None;
                between = levels.BetweenRunsUp;
                GeneratedLevel level = levels.Level;
                Enemy b = level != null ? level.Boss : null;
                RoomFight bossFight = b != null ? b.Fight : null;
                IReadOnlyList<RoomFight> fights = levels.RoomFights;
                for (int i = 0; i < fights.Count; i++)
                {
                    RoomFight f = fights[i];
                    if (f == null || !f.On) continue;
                    any = true;
                    if (f == bossFight) boss = true;
                    _lastFight = f;
                    _lastFightLevel = level;
                }
            }
            bool dead = _health != null && !_health.IsAlive;
            return MusicRules.Target(v.On, ended, dead, between, boss, any);
        }

        /// <summary>The pause menu or a choice screen (or the screen between runs) is up: it plays on, quieter.</summary>
        static bool Held(GeneratedLevels levels) =>
            levels != null && (levels.Paused || levels.Choice != GeneratedLevels.ChoiceKind.None || levels.BetweenRunsUp);

        // ---------------- Changes ----------------

        /// <summary>A source to start on a line: a free one; else the one whose fade ends first, cut (three changes inside two bars).</summary>
        int FreeVoice(int except)
        {
            for (int i = 0; i < VoiceCount; i++)
                if (i != except && i != _main && !_voices[i].Busy) return i;
            int best = -1;
            for (int i = 0; i < VoiceCount; i++)
            {
                if (i == except || i == _main) continue;
                if (best < 0 || _voices[i].FadeFrom + _voices[i].FadeLength < _voices[best].FadeFrom + _voices[best].FadeLength) best = i;
            }
            if (best >= 0) Free(best);
            return best;
        }

        void Free(int i)
        {
            Voice voice = _voices[i];
            if (voice.Source != null) voice.Source.Stop();
            voice.Busy = false;
            voice.Cleared = false;
            voice.State = MusicState.None;
            voice.FadeFrom = double.PositiveInfinity;
            voice.FadeLength = 0.0;
            voice.Applied = -1f;
            if (_main == i) _main = -1;
            if (_next == i) _next = -1;
            if (_sting == i) _sting = -1;
        }

        void StartOn(int i, AudioClip clip, MusicState state, bool cleared, double line)
        {
            Voice voice = _voices[i];
            voice.Busy = true;
            voice.State = state;
            voice.Cleared = cleared;
            voice.StartAt = line;
            voice.FadeFrom = double.PositiveInfinity;
            voice.FadeLength = 0.0;
            voice.Applied = -1f;
            AudioSource s = voice.Source;
            s.clip = clip;
            s.loop = !cleared;
            s.timeSamples = 0; // from its first sample
            s.volume = 0f;     // its level is set with the others' below, before the line
            s.PlayScheduled(line);
        }

        /// <summary>A change told: its loop on its line from its first sample, the loop heard to fade out from the same line.</summary>
        void Schedule(MusicValues v, MusicChange c, double now, GeneratedLevels levels)
        {
            double line = _conductor.LineTime(c.ScheduledBar);
            AudioClip clip = ClipOf(c.ScheduledState);
            Voice main = _main >= 0 ? _voices[_main] : null;
            _next = -1;
            _nextPlaysOn = clip != null && main != null && main.Source.clip == clip;
            if (!_nextPlaysOn)
            {
                if (clip != null)
                {
                    _next = FreeVoice(-1);
                    if (_next >= 0) StartOn(_next, clip, c.ScheduledState, false, line);
                }
                if (main != null)
                {
                    main.FadeFrom = line;
                    main.FadeLength = MusicFade.OutSeconds(v, c.ScheduledState);
                }
            }
            bool cleared = c.ScheduledState == MusicState.Calm && _cleared != null && RoomWasCleared(levels);
            if (cleared)
            {
                _sting = FreeVoice(_next);
                if (_sting >= 0) StartOn(_sting, _cleared, MusicState.None, true, line);
            }
            Scheduled?.Invoke(new MusicSchedule
            {
                State = c.ScheduledState, Bar = c.ScheduledBar, Line = line, AskedAt = now,
                Clip = _nextPlaysOn ? null : clip, PlaysOn = _nextPlaysOn, Cleared = cleared && _sting >= 0,
            });
        }

        /// <summary>The fight that was heard has ended with its room's every enemy dead (its own and the boss's called ones).</summary>
        bool RoomWasCleared(GeneratedLevels levels)
        {
            if (_lastFight == null || _lastFight.On || levels == null || levels.Level == null || levels.Level != _lastFightLevel) return false;
            if (_conductor.Playing != MusicState.Fight && _conductor.Playing != MusicState.Boss) return false;
            GeneratedLevel level = levels.Level;
            return NoneAlive(level.EnemiesByRoom, _lastFight.Room) && NoneAlive(level.CalledByRoom, _lastFight.Room);
        }

        static bool NoneAlive(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (byRoom == null || !byRoom.TryGetValue(room, out List<Enemy> list)) return true;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) return false;
            return true;
        }

        /// <summary>The change waiting was cancelled before its line: what was told to start is stopped unheard, the loop heard holds.</summary>
        void Cancel()
        {
            if (_next >= 0) Free(_next);
            if (_sting >= 0) Free(_sting);
            _next = -1;
            _sting = -1;
            _nextPlaysOn = false;
            if (_main >= 0)
            {
                _voices[_main].FadeFrom = double.PositiveInfinity;
                _voices[_main].FadeLength = 0.0;
            }
        }

        /// <summary>A change landed on its line: the new loop is the one heard, the old one's end told to the audio clock.</summary>
        void Land(MusicValues v, MusicChange c)
        {
            double line = _conductor.LineTime(c.LandedBar);
            if (_nextPlaysOn)
            {
                if (_main >= 0)
                {
                    _voices[_main].State = c.To;
                    _voices[_main].Applied = -1f;
                }
            }
            else
            {
                if (_main >= 0)
                {
                    Voice old = _voices[_main];
                    old.FadeFrom = line;
                    old.FadeLength = MusicFade.OutSeconds(v, c.To);
                    old.Source.SetScheduledEndTime(line + old.FadeLength);
                }
                _main = _next;
            }
            _next = -1;
            _sting = -1; // the room-cleared sound rings out by itself
            _nextPlaysOn = false;
            Tell(c, line);
        }

        /// <summary>Silence (her death, the run's end) fades the loop heard out from now; the switch off stops everything at once.</summary>
        void LandAtOnce(MusicValues v, MusicChange c, double now)
        {
            if (c.To == MusicState.None)
            {
                for (int i = 0; i < VoiceCount; i++) if (_voices[i].Busy) Free(i);
                _main = _next = _sting = -1;
            }
            else if (_main >= 0)
            {
                Voice old = _voices[_main];
                old.FadeFrom = now;
                old.FadeLength = MusicFade.OutSeconds(v, c.To);
                old.Source.SetScheduledEndTime(now + old.FadeLength);
                _main = -1;
            }
            _nextPlaysOn = false;
            if (c.From != c.To) Tell(c, now);
        }

        void Tell(MusicChange c, double line)
        {
            ChangesLanded++;
            PlaySessionRecorder.Current?.NoteMusic(c.From, c.To, c.LandedBar);
            Landed?.Invoke(new MusicLanding { From = c.From, To = c.To, Bar = c.LandedBar, Line = line, AtOnce = c.AtOnce });
        }

        // ---------------- Levels, every frame ----------------

        /// <summary>
        /// Each source's level now: its state's share of her basic shot, the master music volume, the screens' share, its fade. A
        /// source is told only when its level changed; one whose fade has ended, or whose short sound has rung out, is freed.
        /// </summary>
        void SetLevels(MusicValues v, double now, GeneratedLevels levels)
        {
            _held = MusicFade.Toward(_held, Held(levels) ? Mathf.Clamp01(v.HeldShare) : 1f, Time.unscaledDeltaTime, v.HeldGlide);
            if (!_voicesMade) return;
            float common = BasicShot() * v.Volume * _held;
            for (int i = 0; i < VoiceCount; i++)
            {
                Voice voice = _voices[i];
                if (!voice.Busy) continue;
                bool faded = voice.FadeFrom < double.PositiveInfinity && now >= voice.FadeFrom + voice.FadeLength;
                bool rungOut = voice.Cleared && voice.Source.clip != null && now >= voice.StartAt + voice.Source.clip.length;
                if (faded || rungOut)
                {
                    Free(i);
                    continue;
                }
                float level = voice.Cleared ? v.ClearedLevel : v.Level(voice.State);
                float volume = Mathf.Clamp01(level * common * MusicFade.Gain(now, voice.FadeFrom, voice.FadeLength));
                if (volume == voice.Applied) continue;
                voice.Source.volume = volume;
                voice.Applied = volume;
            }
        }

        /// <summary>Her basic shot's volume, as the shot itself is played (PlayerCharacter.PlaySound); 1 without her.</summary>
        float BasicShot()
        {
            if (_player == null || _player.ShotLooks == null || _player.ShotLooks.Basic == null) return 1f;
            return _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
        }
    }
}
