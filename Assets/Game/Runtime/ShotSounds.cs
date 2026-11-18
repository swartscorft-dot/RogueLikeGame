using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>What a shot asked to play: for tests and tools, through <see cref="ShotSounds.Requested"/>.</summary>
    public struct ShotSoundRequest
    {
        public AudioClip Clip;
        public float Volume, Pitch, StartAt;
        public int Frame;
        /// <summary>The voice it went to was still playing an earlier sound, which this one cut off.</summary>
        public bool CutOffAnother;
    }

    /// <summary>
    /// The sounds of the shots: plain 2D sounds (the camera is 28 m up, so there is no falloff), from a small pool of
    /// voices so that the tails of quick shots overlap instead of cutting each other off. A sound starts past its
    /// clip's leading silence (the look's <see cref="ShotLook.SoundStart"/>), so the bang lands with the flash.
    /// Presentation only: nothing here changes a shot.
    /// </summary>
    public sealed class ShotSounds : MonoBehaviour
    {
        /// <summary>Every sound asked for, in order. Tests listen here instead of to the speakers.</summary>
        public static event Action<ShotSoundRequest> Requested;

        [Tooltip("How many shot sounds can sound at once. When all are busy the oldest is cut off.")]
        public int Voices = 10;

        readonly List<AudioSource> _voices = new List<AudioSource>();
        readonly List<float> _startedAt = new List<float>();
        readonly List<float> _endsAt = new List<float>(); // by the clock too, so a voice counts as busy even without an audio device

        void Awake()
        {
            for (int i = 0; i < Mathf.Max(1, Voices); i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;
                _voices.Add(source);
                _startedAt.Add(float.NegativeInfinity);
                _endsAt.Add(float.NegativeInfinity);
            }
        }

        bool Busy(int i) => _voices[i].isPlaying || Time.unscaledTime < _endsAt[i];

        /// <summary>Plays a sound once: a free voice, or the one that started longest ago.</summary>
        public void Play(AudioClip clip, float volume, float pitch, float startAt)
        {
            if (clip == null || volume <= 0f) return;
            int pick = -1;
            for (int i = 0; i < _voices.Count; i++)
                if (!Busy(i)) { pick = i; break; }
            if (pick < 0)
            {
                pick = 0;
                for (int i = 1; i < _voices.Count; i++) if (_startedAt[i] < _startedAt[pick]) pick = i;
            }
            bool cut = Busy(pick);
            AudioSource voice = _voices[pick];
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            voice.time = Mathf.Clamp(startAt, 0f, Mathf.Max(0f, clip.length - 0.01f));
            voice.Play();
            _startedAt[pick] = Time.unscaledTime;
            _endsAt[pick] = Time.unscaledTime + (clip.length - voice.time) / voice.pitch;
            Requested?.Invoke(new ShotSoundRequest
            {
                Clip = clip, Volume = voice.volume, Pitch = voice.pitch, StartAt = voice.time, Frame = Time.frameCount, CutOffAnother = cut,
            });
        }
    }
}
