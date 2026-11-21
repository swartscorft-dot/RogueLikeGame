using System;
using System.Collections.Generic;
using Roguelike.Rules;
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
        /// <summary>Work order 064: which of the basic shot's variations it was, or -1 for a sound with none.</summary>
        public int Variation;
        /// <summary>Work order 064: the variation's low-pass in Hz (0: none), and the share of the clip that sounds before its fade.</summary>
        public float Cutoff, Tail;
    }

    /// <summary>
    /// The sounds of the shots: plain 2D sounds (the camera is 28 m up, so there is no falloff), from a small pool of
    /// voices so that the tails of quick shots overlap instead of cutting each other off. A sound starts past its
    /// clip's leading silence (the look's <see cref="ShotLook.SoundStart"/>), so the bang lands with the flash.
    /// Work order 064: a sound may come as a variation (<see cref="ShotVariation"/>): a low-pass on its voice for the click's
    /// brightness, and its tail faded out early; every voice has its filter, off unless a variation asks for it.
    /// Presentation only: nothing here changes a shot.
    /// </summary>
    public sealed class ShotSounds : MonoBehaviour
    {
        /// <summary>Every sound asked for, in order. Tests listen here instead of to the speakers.</summary>
        public static event Action<ShotSoundRequest> Requested;

        [Tooltip("How many shot sounds can sound at once. When all are busy the oldest is cut off.")]
        public int Voices = 10;

        const float OpenCutoff = 20000f; // Hz: at or over this a variation's low-pass is left off

        readonly List<AudioSource> _voices = new List<AudioSource>();
        readonly List<AudioLowPassFilter> _filters = new List<AudioLowPassFilter>();
        readonly List<float> _startedAt = new List<float>();
        readonly List<float> _endsAt = new List<float>(); // by the clock too, so a voice counts as busy even without an audio device
        readonly List<float> _fadeFrom = new List<float>(); // work order 064: real time a shortened tail starts to fade; infinity: none
        readonly List<float> _fadeSeconds = new List<float>();
        readonly List<float> _volume = new List<float>();
        bool _fading;

        void Awake()
        {
            for (int i = 0; i < Mathf.Max(1, Voices); i++)
            {
                // Each voice on its own child, so each has its own low-pass (a filter works on every source of its object).
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;
                var filter = child.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000f;
                filter.enabled = false;
                _voices.Add(source);
                _filters.Add(filter);
                _startedAt.Add(float.NegativeInfinity);
                _endsAt.Add(float.NegativeInfinity);
                _fadeFrom.Add(float.PositiveInfinity);
                _fadeSeconds.Add(0f);
                _volume.Add(0f);
            }
        }

        bool Busy(int i) => _voices[i].isPlaying || Time.unscaledTime < _endsAt[i];

        /// <summary>Plays a sound once: a free voice, or the one that started longest ago.</summary>
        public void Play(AudioClip clip, float volume, float pitch, float startAt) =>
            Play(clip, volume, pitch, startAt, new ShotVariation { Index = -1, Cutoff = 0f, Pitch = 1f, Volume = 1f, Tail = 1f });

        /// <summary>
        /// Plays a sound once as a variation (work order 064): its low-pass (off at 20 kHz or more) and its tail, the share of
        /// the clip that sounds before a short fade. The pitch and volume given are the variation's already.
        /// </summary>
        public void Play(AudioClip clip, float volume, float pitch, float startAt, ShotVariation variation)
        {
            if (clip == null || volume <= 0f || _voices.Count == 0) return;
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
            bool filtered = variation.Cutoff > 0f && variation.Cutoff < OpenCutoff;
            AudioLowPassFilter filter = _filters[pick];
            filter.enabled = filtered;
            if (filtered) filter.cutoffFrequency = variation.Cutoff;
            voice.Play();
            float now = Time.unscaledTime;
            float length = (clip.length - voice.time) / voice.pitch;
            float tail = variation.Tail > 0f && variation.Tail < 1f ? variation.Tail : 1f;
            _startedAt[pick] = now;
            _volume[pick] = voice.volume;
            if (tail < 1f)
            {
                float fade = Mathf.Max(0.005f, variation.TailFade);
                _fadeFrom[pick] = now + Mathf.Max(0f, length * tail - fade);
                _fadeSeconds[pick] = fade;
                _endsAt[pick] = now + length * tail;
                _fading = true;
            }
            else
            {
                _fadeFrom[pick] = float.PositiveInfinity;
                _endsAt[pick] = now + length;
            }
            Requested?.Invoke(new ShotSoundRequest
            {
                Clip = clip, Volume = voice.volume, Pitch = voice.pitch, StartAt = voice.time, Frame = Time.frameCount, CutOffAnother = cut,
                Variation = variation.Index, Cutoff = filtered ? variation.Cutoff : 0f, Tail = tail,
            });
        }

        // A shortened tail fades out on real time and its voice stops at its end (work order 064). Nothing allocated.
        void Update()
        {
            if (!_fading) return;
            float now = Time.unscaledTime;
            bool any = false;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (float.IsPositiveInfinity(_fadeFrom[i])) continue;
                if (now >= _endsAt[i])
                {
                    _voices[i].Stop();
                    _voices[i].volume = _volume[i];
                    _fadeFrom[i] = float.PositiveInfinity;
                    continue;
                }
                any = true;
                if (now > _fadeFrom[i])
                    _voices[i].volume = _volume[i] * Mathf.Clamp01(1f - (now - _fadeFrom[i]) / Mathf.Max(1e-4f, _fadeSeconds[i]));
            }
            _fading = any;
        }
    }

    /// <summary>
    /// The clips of the basic shot's variations from the recordings' path (work order 064): "BasicShot_1", "BasicShot_2" ...
    /// under Resources (<see cref="ShotVariationValues.ClipFolder"/>), looked for once. When two or more are there, the
    /// variation of index i plays clip i (wrapping round); with fewer, each variation plays the look's own clip.
    /// </summary>
    public static class ShotVariationClips
    {
        static AudioClip[] s_Clips;
        static string s_Folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Clips = null;
            s_Folder = null;
        }

        /// <summary>The clip for a variation, or null for the look's own.</summary>
        public static AudioClip For(ShotVariationValues v, int index)
        {
            if (v == null || string.IsNullOrEmpty(v.ClipFolder) || index < 0) return null;
            if (s_Clips == null || s_Folder != v.ClipFolder) Load(v.ClipFolder);
            return s_Clips.Length >= 2 ? s_Clips[index % s_Clips.Length] : null;
        }

        /// <summary>How many variation clips were found. For tests.</summary>
        public static int Found(ShotVariationValues v)
        {
            if (v == null || string.IsNullOrEmpty(v.ClipFolder)) return 0;
            if (s_Clips == null || s_Folder != v.ClipFolder) Load(v.ClipFolder);
            return s_Clips.Length;
        }

        static void Load(string folder)
        {
            s_Folder = folder;
            var found = new List<AudioClip>();
            for (int i = 1; i <= 16; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>(folder + "_" + i);
                if (clip == null) break;
                found.Add(clip);
            }
            s_Clips = found.ToArray();
        }
    }
}
