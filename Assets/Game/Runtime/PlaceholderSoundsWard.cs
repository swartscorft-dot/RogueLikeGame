using System;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The ward heard (work order 107, the warder that reads; Kristian's tick of 2026-11-24, "vokteren som ikke leses"), as part
    /// of <see cref="PlaceholderSounds"/>, both sounds by name so a recording of his replaces each, as every placeholder:
    ///
    /// - a soft hum (<see cref="PlaceholderSound.WardHum"/>) while any ward of a warder that reads stands: it starts with the
    ///   first ward on and stops with the last gone (<see cref="HumGate"/>), looped on a voice of its own (the crowd's voices play
    ///   each sound once). Paused it is silent, as every placeholder is, and it comes back when the game goes on;
    /// - a break (<see cref="PlaceholderSound.WardBreaks"/>) when a ward falls: its warder killed or staggered, or the warded too
    ///   far (<see cref="WarderReadRules.BreakHeard"/>, the reasons that daze), under the crowd rule (two at once are one, louder).
    ///   A ward whose ally died, or whose warder went home, simply goes, silent.
    ///
    /// With <see cref="WarderValues.Reads"/> off on every warder neither is heard. Nothing is allocated in a frame.
    /// </summary>
    public sealed partial class PlaceholderSounds
    {
        AudioSource _hum;
        readonly HumGate _humGate = new HumGate();
        Action _humFrame;

        /// <summary>Is the hum on now (wanted and started; <see cref="HumPlaying"/> is its voice, silent where there is no audio)?</summary>
        public bool HumOn => _humGate.On;
        /// <summary>Is the hum's voice playing now?</summary>
        public bool HumPlaying => _hum != null && _hum.isPlaying;
        /// <summary>The hum's starts and stops so far, for tests.</summary>
        public int HumStarts => _humGate.Starts;
        public int HumStops => _humGate.Stops;
        /// <summary>Wards heard falling so far (several in one frame are one play), for tests.</summary>
        public int WardBreaksHeard { get; private set; }

        /// <summary>A warder's report: a ward that fell is heard, when its warder reads.</summary>
        void HearWard(Enemy enemy, EnemyReport r)
        {
            if (r.Kind != EnemyReportKind.WardBroken || !(enemy is EnemyWarder w) || !WarderReadRules.BreakSounds(w.Tuning)) return;
            if (!WarderReadRules.BreakHeard((WardEnd)r.Count)) return;
            WardBreaksHeard++;
            Ask(PlaceholderSoundRules.SoundFor(SoundMoment.WardFell));
        }

        /// <summary>The hum's frame: started with the first ward on, stopped with the last gone, silent while paused.</summary>
        void HumFrame()
        {
            bool wanted = WarderReadRules.HumWanted(EnemyWarder.HummingWardsOn, Values.On, Held);
            if (!_humGate.Step(wanted)) return;
            if (wanted) StartHum();
            else if (_hum != null) _hum.Stop();
        }

        void StartHum()
        {
            AudioClip clip = _clips[(int)PlaceholderSound.WardHum];
            if (clip == null) return;
            if (_hum == null)
            {
                var go = new GameObject("WardHum");
                go.transform.SetParent(transform, false);
                _hum = go.AddComponent<AudioSource>();
                _hum.playOnAwake = false;
                _hum.loop = true;
                _hum.spatialBlend = 0f; // a plain 2D sound, as the crowd's voices are
            }
            // A share of the basic shot's volume, as every placeholder is played (Flush).
            float basic = 1f;
            if (_player != null && _player.ShotLooks != null && _player.ShotLooks.Basic != null)
                basic = _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
            float volume = Values.Volume(PlaceholderSound.WardHum) * Values.Master * basic;
            _hum.clip = clip;
            _hum.volume = Mathf.Clamp01(volume);
            _hum.pitch = 1f;
            _hum.Play();
            Requested?.Invoke(new PlaceholderSoundRequest
            {
                Sound = PlaceholderSound.WardHum, Clip = clip, Volume = volume, Pitch = 1f, Asked = 1,
                Frame = Time.frameCount, Recorded = _recorded[(int)PlaceholderSound.WardHum], Bells = 1,
            });
        }
    }
}
