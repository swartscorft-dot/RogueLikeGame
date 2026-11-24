using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>What a sound placed at a door asked to play (work order 113): for tests and tools, through <see cref="PlaceholderSounds.Placed"/>.</summary>
    public struct PlacedSoundRequest
    {
        public PlaceholderSound Sound;
        public AudioClip Clip;
        /// <summary>Where in the world it plays: the door's block, in the doorway.</summary>
        public Vector3 At;
        /// <summary>The volume it was asked to play at, before the voice clamps it to 0..1 (and before the distance takes its share).</summary>
        public float Volume;
        public float Pitch;
        public int Frame;
        /// <summary>A recorded clip from the Resources folder, not the code-made one.</summary>
        public bool Recorded;
        /// <summary>The voice it plays on, placed at <see cref="At"/>.</summary>
        public AudioSource Voice;
    }

    /// <summary>
    /// The sounds of the rooms (work order 113; Kristian 2026-11-24: "Kodelagde lyder du kan bytte ut med egne opptak senere ...
    /// en romtone per etasje. Ingen musikk."; <see cref="RoomSoundValues"/>, its switch <see cref="RoomSoundValues.On"/>). What the
    /// level does is made heard, as every placeholder, by looking at what the floor shows (<see cref="GeneratedLevels"/>) and
    /// changing nothing in it:
    ///
    /// - A room's doors shutting (<see cref="GeneratedLevels.RoomDoors.Closings"/> up): each door's block is heard rising shut and
    ///   seating, <see cref="PlaceholderSound.DoorShuts"/>, once a door, placed in the world where that block stands, so that she
    ///   hears where; their opening (<see cref="GeneratedLevels.RoomDoors.Shut"/> off) the lighter <see cref="PlaceholderSound.DoorOpens"/>,
    ///   the same way. The doors' heavy impact of work order 086 (<see cref="RoomFightValues.DoorCloseSound"/>) is not asked then.
    /// - A room's fight waking for the first time on its floor (<see cref="RoomFight.Starts"/> from 0): <see cref="PlaceholderSound.RoomWakes"/>,
    ///   once a room, in place of the boss's growl of work order 070 (<see cref="RoomFightValues.CueSound"/>); none with its cue off.
    /// - A combat room cleared: <see cref="PlaceholderSound.RoomLetsGo"/> in place of the way opening (<see cref="PlaceholderSound.WayOpens"/>),
    ///   at the same moment (the run's heal for it asks for it), and not quieted by her kill that won the room (work order 080's
    ///   rule keeps the way opening's stone shift from sitting on that kill; the release is the room's, under it).
    /// - The way down: in a run that goes on, the floor's number up from one she was on: <see cref="PlaceholderSound.WayDown"/>.
    /// - A room tone a floor: when a floor is built, its mood's tone (<see cref="RoomMood.Tone"/>) is given its floor's row
    ///   (<see cref="RoomSoundValues.Tones"/>, <see cref="RoomSoundRules.ToneOf"/>), made in code once a row and kept; floor 1's
    ///   is today's clip itself. A recording at <see cref="RoomSoundRules.ToneResourcePath"/> plays in its place on every floor, a
    ///   floor down at its row's pitch. The level is the mood's (<see cref="MoodValues.ToneVolume"/>); the mood still sets it.
    ///
    /// Each sound is a <see cref="PlaceholderSound"/>, so a recording of his under its name replaces it as every other; each at its
    /// line's level in <see cref="PlaceholderSoundValues.Sounds"/>, under the same rules (nothing while the sounds are off or the game
    /// paused). The doors' voices are its own, placed (<see cref="RoomSoundValues.DoorPlaced"/>), made once with its first frame;
    /// nothing is allocated when one plays, nor in a frame while nothing changes (but a floor's tone made the first time its row is
    /// met). Off: every one of these as before, and today's tone on every floor.
    /// </summary>
    public sealed partial class PlaceholderSounds
    {
        const uint RoomPitchSeed = 113u;
        const int MostDoorCap = 8; // the ring of a door sound's plays in the last second (its cap is at most this)

        /// <summary>Work order 113's switch and numbers, defaults in code (first guesses).</summary>
        public RoomSoundValues Rooms = new RoomSoundValues();

        /// <summary>Every sound placed at a door, as it plays. Tests listen here (and to <see cref="Requested"/>, which hears them too).</summary>
        public static event Action<PlacedSoundRequest> Placed;

        /// <summary>
        /// True while the sounds of the rooms are on (work order 113): the floor asks then for none of the sounds they take the
        /// place of (the doors' impact, the room's growl). False with no placeholder sounds at all: as before.
        /// </summary>
        public static bool RoomsOn
        {
            get
            {
                PlaceholderSounds s = Current;
                return s != null && s.Rooms != null && s.Rooms.On;
            }
        }

        /// <summary>For tests: the floor's tone as this gave it, its row's floor (0: none given, today's as the mood made it).</summary>
        public int ToneFloorGiven { get; private set; }
        /// <summary>For tests: the doors' voices (made with the first frame), and how many room tones were made in code.</summary>
        public IReadOnlyList<AudioSource> DoorVoices => _doorVoices;
        public int TonesMade { get; private set; }
        /// <summary>True when a recorded room tone was found under <see cref="RoomSoundRules.ToneResourcePath"/>.</summary>
        public bool ToneRecorded => _recordedTone != null;

        readonly PitchVariation _roomPitch = new PitchVariation(RoomPitchSeed);
        readonly SecondCap _shutCap = new SecondCap(MostDoorCap), _openCap = new SecondCap(MostDoorCap);
        readonly List<int> _wakesSeen = new List<int>();
        readonly List<int> _closingsSeen = new List<int>();
        readonly List<bool> _shutSeen = new List<bool>();
        readonly List<AudioSource> _doorVoices = new List<AudioSource>();
        readonly List<float> _doorEnds = new List<float>(), _doorStarted = new List<float>();
        readonly List<RoomToneRow> _toneRows = new List<RoomToneRow>();
        readonly List<AudioClip> _toneClips = new List<AudioClip>();
        GeneratedLevel _roomsLevel;
        bool _roomsLooked;
        int _floorSeen;
        RunRecord _runSeen;
        AudioSource _toneSource;
        AudioClip _toneWas, _recordedTone;
        bool _toneGiven, _toneLooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRoomStatics() => Placed = null;

        /// <summary>
        /// Work order 113: what a combat room cleared plays. With the rooms' switch on, the room's release in place of the way
        /// opening; every other sound as it is.
        /// </summary>
        PlaceholderSound RoomsInPlaceOf(PlaceholderSound sound) =>
            sound == PlaceholderSound.WayOpens && Rooms != null && Rooms.On ? PlaceholderSound.RoomLetsGo : sound;

        /// <summary>The frame's look at the floor for work order 113 (before the crowd is let through). A fault is logged once and dropped.</summary>
        void HearRooms()
        {
            try { LookAtTheRooms(); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[PlaceholderSounds] " + e);
            }
        }

        void LookAtTheRooms()
        {
            if (Rooms == null) return;
            if (_doorVoices.Count == 0) MakeDoorVoices();
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            bool on = Rooms.On;

            // The way down: the floor's number up in the same run.
            int floor = levels != null ? levels.Floor : 0;
            RunRecord run = levels != null ? levels.Record : null;
            if (on && _roomsLooked && RoomSoundRules.WentDown(levels != null && levels.InRun, run != null && run == _runSeen, _floorSeen, floor))
                Ask(PlaceholderSoundRules.SoundFor(SoundMoment.WentDown), lookedFor: true);
            _floorSeen = floor;
            _runSeen = run;

            if (level != _roomsLevel || !_roomsLooked)
            {
                // A new floor (or none): what it shows now is seen, nothing heard for it.
                _roomsLevel = level;
                _wakesSeen.Clear();
                _closingsSeen.Clear();
                _shutSeen.Clear();
            }
            _roomsLooked = true;
            if (levels != null && level != null)
            {
                IReadOnlyList<RoomFight> fights = levels.RoomFights;
                RoomFightValues fv = levels.Values != null ? levels.Values.Fights : null;
                bool cue = fv == null || fv.Cue;
                for (int i = 0; i < fights.Count; i++) // every frame: indexed, nothing made
                {
                    int starts = fights[i] != null ? fights[i].Starts : 0;
                    if (i >= _wakesSeen.Count) _wakesSeen.Add(starts); // grown once a floor
                    else
                    {
                        if (on && RoomSoundRules.WakeHeard(_wakesSeen[i], starts, cue))
                            Ask(PlaceholderSoundRules.SoundFor(SoundMoment.RoomFightWoke), lookedFor: true);
                        _wakesSeen[i] = starts;
                    }
                }
                IReadOnlyList<GeneratedLevels.RoomDoors> doors = levels.RoomDoorList;
                for (int i = 0; i < doors.Count; i++)
                {
                    GeneratedLevels.RoomDoors d = doors[i];
                    if (d == null) continue;
                    if (i >= _closingsSeen.Count)
                    {
                        _closingsSeen.Add(d.Closings);
                        _shutSeen.Add(d.Shut);
                        continue;
                    }
                    if (on && d.Closings > _closingsSeen[i]) AtEachDoor(d, PlaceholderSoundRules.SoundFor(SoundMoment.RoomDoorShut));
                    else if (on && _shutSeen[i] && !d.Shut) AtEachDoor(d, PlaceholderSoundRules.SoundFor(SoundMoment.RoomDoorOpened));
                    _closingsSeen[i] = d.Closings;
                    _shutSeen[i] = d.Shut;
                }
            }
            KeepTheTone(level);
        }

        /// <summary>One play of the sound at each of a room's doors, where its block stands.</summary>
        void AtEachDoor(GeneratedLevels.RoomDoors d, PlaceholderSound? sound)
        {
            if (!sound.HasValue) return;
            for (int k = 0; k < d.Blocks.Count; k++)
                if (d.Blocks[k] != null) PlayAt(sound.Value, d.Blocks[k].transform.position);
        }

        /// <summary>
        /// A sound placed in the world (work order 113): under the same rules as every placeholder (its switch, the sounds' switch,
        /// paused is silent, its line's level and cap per second), on a voice of its own at <paramref name="at"/>. Nothing allocated.
        /// </summary>
        void PlayAt(PlaceholderSound sound, Vector3 at)
        {
            if (!Values.On || Rooms == null || !Rooms.On) return;
            if (!PlaceholderSoundRules.MayStart(sound, _heldBefore && Held)) return; // found by looking, as the other looked-for moments
            SecondCap cap = sound == PlaceholderSound.DoorOpens ? _openCap : _shutCap;
            if (!cap.Ask(Time.unscaledTime, Values.PerSecond(sound))) return;
            AudioClip clip = _clips[(int)sound];
            float volume = Values.Volume(sound) * Values.Master * BasicShotVolume();
            float pitch = _roomPitch.Next(Values.PitchSpread);
            if (clip == null || volume <= 0f || _doorVoices.Count == 0) return;
            int pick = FreeDoorVoice();
            AudioSource voice = _doorVoices[pick];
            Place(voice);
            voice.transform.position = at;
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            voice.Play();
            float now = Time.unscaledTime;
            _doorStarted[pick] = now;
            _doorEnds[pick] = now + clip.length / voice.pitch;
            bool recorded = _recorded[(int)sound];
            Requested?.Invoke(new PlaceholderSoundRequest
            {
                Sound = sound, Clip = clip, Volume = volume, Pitch = pitch, Asked = 1, Frame = Time.frameCount, Recorded = recorded, Bells = 1,
            });
            Placed?.Invoke(new PlacedSoundRequest
            {
                Sound = sound, Clip = clip, At = at, Volume = volume, Pitch = pitch, Frame = Time.frameCount, Recorded = recorded, Voice = voice,
            });
        }

        /// <summary>A share of the basic shot's volume, as every placeholder is played (<see cref="Flush"/>).</summary>
        float BasicShotVolume()
        {
            if (_player != null && _player.ShotLooks != null && _player.ShotLooks.Basic != null)
                return _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
            return 1f;
        }

        /// <summary>A free door voice, or the one that started longest ago.</summary>
        int FreeDoorVoice()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _doorVoices.Count; i++)
                if (!_doorVoices[i].isPlaying && now >= _doorEnds[i]) return i;
            int oldest = 0;
            for (int i = 1; i < _doorVoices.Count; i++) if (_doorStarted[i] < _doorStarted[oldest]) oldest = i;
            return oldest;
        }

        /// <summary>The doors' voices, each on its own child to be placed: made once, with the first frame.</summary>
        void MakeDoorVoices()
        {
            var root = new GameObject("RoomDoorVoices");
            root.transform.SetParent(transform, false);
            int n = Mathf.Max(1, Rooms.DoorVoices);
            for (int i = 0; i < n; i++)
            {
                var child = new GameObject("DoorVoice" + i);
                child.transform.SetParent(root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                Place(source);
                _doorVoices.Add(source);
                _doorEnds.Add(float.NegativeInfinity);
                _doorStarted.Add(float.NegativeInfinity);
            }
        }

        /// <summary>A door voice placed as the numbers say now: mostly in the world, full within its reach of the listener, then falling.</summary>
        void Place(AudioSource source)
        {
            source.spatialBlend = Mathf.Clamp01(Rooms.DoorPlaced);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(0.01f, Rooms.DoorFullWithin);
            source.maxDistance = Mathf.Max(source.minDistance, Rooms.DoorFarthest);
            source.dopplerLevel = 0f;
            source.spread = 0f;
        }

        // ---------------- A room tone a floor ----------------

        /// <summary>
        /// The floor's tone given its row, once, when the floor is built (a new mood's tone, or the mood built again); with the
        /// switch off, today's put back. Nothing in a frame where nothing changed.
        /// </summary>
        void KeepTheTone(GeneratedLevel level)
        {
            RoomMood mood = level != null ? level.Mood : null;
            AudioSource tone = mood != null ? mood.Tone : null;
            if (tone != _toneSource)
            {
                _toneSource = tone;
                _toneWas = tone != null ? tone.clip : null;
                _toneGiven = false;
                ToneFloorGiven = 0;
            }
            if (tone == null) return;
            if (Rooms.On && !_toneGiven)
            {
                _toneGiven = true;
                RoomToneRow? row = RoomSoundRules.RowOf(Rooms.Tones, level.Layout.Floor);
                if (!row.HasValue) return;
                AudioClip recorded = RecordedTone();
                AudioClip clip = recorded != null ? recorded : RoomSoundRules.IsToday(row.Value) ? _toneWas : ToneClip(row.Value);
                SetTone(tone, clip, recorded != null ? Mathf.Clamp(row.Value.Pitch, 0.1f, 3f) : 1f);
                ToneFloorGiven = row.Value.Floor;
            }
            else if (!Rooms.On && _toneGiven) PutTodaysToneBack();
        }

        void PutTodaysToneBack()
        {
            _toneGiven = false;
            ToneFloorGiven = 0;
            if (_toneSource != null) SetTone(_toneSource, _toneWas, 1f);
        }

        static void SetTone(AudioSource tone, AudioClip clip, float pitch)
        {
            if (clip == null || (tone.clip == clip && Mathf.Approximately(tone.pitch, pitch))) return;
            bool playing = tone.isPlaying;
            tone.clip = clip;
            tone.pitch = pitch;
            if (playing) tone.Play();
        }

        /// <summary>A recorded room tone, looked for once (<see cref="RecordedClips"/>, as every placeholder's), or null.</summary>
        AudioClip RecordedTone()
        {
            if (_toneLooked) return _recordedTone;
            _toneLooked = true;
            try { _recordedTone = RecordedClips?.Invoke(RoomSoundRules.ToneResourcePath); }
            catch (Exception e) { Debug.LogWarning("[PlaceholderSounds] " + e.Message); }
            return _recordedTone;
        }

        /// <summary>The code-made tone of a row: made the first time the row is met and kept (made again if its numbers changed).</summary>
        AudioClip ToneClip(RoomToneRow row)
        {
            for (int i = 0; i < _toneRows.Count; i++)
                if (_toneRows[i].Floor == row.Floor)
                {
                    if (Same(_toneRows[i], row) && _toneClips[i] != null) return _toneClips[i];
                    if (_toneClips[i] != null) Destroy(_toneClips[i]);
                    _toneRows.RemoveAt(i);
                    _toneClips.RemoveAt(i);
                    break;
                }
            float[] samples = Synth.Render(RoomSoundRules.ToneOf(row), Synth.SampleRate);
            AudioClip clip = AudioClip.Create("RoomTone_Floor" + row.Floor, samples.Length, 1, Synth.SampleRate, false);
            clip.SetData(samples, 0);
            _toneRows.Add(row);
            _toneClips.Add(clip);
            TonesMade++;
            return clip;
        }

        static bool Same(RoomToneRow a, RoomToneRow b) =>
            a.Floor == b.Floor && a.Pitch == b.Pitch && a.Beat == b.Beat && a.Noise == b.Noise && a.Drips == b.Drips;

        /// <summary>With the sounds gone: today's tone back where this gave its own, and the tones it made destroyed (a recording is not).</summary>
        void ForgetRooms()
        {
            if (_toneGiven) PutTodaysToneBack();
            foreach (AudioClip c in _toneClips) if (c != null) Destroy(c);
            _toneClips.Clear();
            _toneRows.Clear();
        }
    }
}
