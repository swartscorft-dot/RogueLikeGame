using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>Which of the focus feel's own sounds played (work order 071).</summary>
    public enum FocusFeelSound { Spend, Ending, PartReady }

    /// <summary>What the focus feel asked to play: for tests, through <see cref="FocusFeel.Requested"/>, instead of the speakers.</summary>
    public struct FocusFeelSoundRequest
    {
        public FocusFeelSound Sound;
        public float Volume, Pitch;
        public int Frame;
    }

    /// <summary>
    /// Spent focus, felt (work order 071, Kristian, 2026-11-21: "glemmer å bruke den heller, fordi man ikke føler det"). It
    /// only shows and sounds what the character already does (<see cref="PlayerCharacter.IsFocusReady"/>,
    /// <see cref="PlayerCharacter.IsFocusBoosted"/>, her report of a spend) and changes nothing in a fight; the freeze at the
    /// moment of spending is the hit stop the hits already have (<see cref="HitStop"/>), on real time and under 0.1 s.
    ///
    /// 1. The moment of spending: a ring of teal light running out from her on the ground, a second inside it, a hard star
    ///    at her chest, the teal light on her at its peak and fading, a freeze of a few hundredths, the camera's kick
    ///    (<see cref="FiringWeight.Push"/>) and the spending sound, low and loud. A Hair Trigger part: the same, smaller.
    /// 2. While the gains run: a teal light on her, a thin teal ring at her feet and teal stars turning round her chest,
    ///    following her; her shots teal and larger (<see cref="ShotLookNow"/>, read where her shots' look is made), and the
    ///    screen's edge in teal (<see cref="FocusEdge"/>).
    /// 3. The last second: the aura and the light flicker, and a falling sound plays once.
    /// 4. Ready and unspent: a quiet teal glow on her (the light, dim and pulsing slowly: never as bright as the aura); a
    ///    Hair Trigger part that fills has a soft chime of its own (a full gauge has the placeholder sounds' chime already).
    /// Nothing shows while she is dead, and nothing while a choice, an end screen or the pause menu is up.
    ///
    /// Drawn with the effects' own pieces (<see cref="ShotFx"/>, <see cref="CombatFx"/> through <see cref="CombatEffects"/>,
    /// whose switch rules them) and one real light, made once and kept; its sounds are made in code at start-up
    /// (<see cref="FocusFeelRules"/>' recipes) and played through voices of its own. No particle system, texture file or
    /// sound file. Off (<see cref="FocusFeelValues.On"/>): focus looks and sounds as before the order. It installs itself
    /// when Play starts under the same rule as the other effects (never in a test run); tests make one with
    /// <see cref="Ensure"/>. Nothing is allocated in a frame while nothing changes.
    /// </summary>
    public sealed class FocusFeel : MonoBehaviour
    {
        const int VoiceCount = 3;
        const float HoldMost = 3600f;
        const float GroundHeight = 0.06f;

        static readonly Color s_Rim = new Color(0.25f, 0.85f, 0.85f);  // the HUD's accent
        static readonly Color s_Core = new Color(0.85f, 1f, 1f);

        /// <summary>The one running now, or null.</summary>
        public static FocusFeel Current { get; private set; }

        /// <summary>Every sound it asked to play, in order. Tests listen here instead of to the speakers.</summary>
        public static event Action<FocusFeelSoundRequest> Requested;

        /// <summary>
        /// The look her shots take while the gains run: the running one's values when it is on, else null (the asset's
        /// empowered look, as before the order). Read by <see cref="PlayerCharacter"/> where a shot's look is made.
        /// </summary>
        public static FocusFeelValues ShotLookNow => Current != null && Current.isActiveAndEnabled && Current.Values.On ? Current.Values : null;

        /// <summary>The numbers and switches, defaults in code (work order 071: first guesses).</summary>
        public FocusFeelValues Values = new FocusFeelValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        // ---------------- What it shows, worked out every frame (for tests too) ----------------

        public FocusFeelPhase Phase { get; private set; }
        /// <summary>The aura's or the ready glow's level now, 0 to 1.</summary>
        public float Level { get; private set; }
        /// <summary>The screen's edge's opacity now (<see cref="FocusEdge"/> draws it).</summary>
        public float EdgeAlpha { get; private set; }
        /// <summary>The teal light: on, and its intensity.</summary>
        public bool LightOn => _light != null && _light.enabled;
        public float LightIntensity => LightOn ? _light.intensity : 0f;
        public Light TealLight => _light;
        /// <summary>Bursts drawn (spends) and falling sounds played since it started.</summary>
        public int Bursts { get; private set; }
        public int Endings { get; private set; }
        /// <summary>The aura's pieces live now (the ring at her feet and the stars round her), and whether they show.</summary>
        public int AuraPieces => (_ring != null ? 1 : 0) + (_orbit != null ? 1 : 0);
        public bool AuraShown { get; private set; }
        public CombatFx AuraRing => _ring;
        public CombatFx AuraOrbit => _orbit;

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerCharacter _listening;
        Action<PlayerReport> _onReport;
        Func<bool> _holds;
        Light _light;
        ShotSounds _voices;
        AudioClip _spendClip, _endingClip, _readyClip;
        CombatFx _ring, _orbit;
        MeshRenderer _ringRenderer, _orbitRenderer;
        bool _auraMade;
        float _burstAt = float.NegativeInfinity; // real time of the last spend
        float _burstShare = 1f;
        float _readySince;
        FocusFeelPhase _phaseSeen;
        bool _endingPlayed;
        int _partsSeen;
        bool _looked;
        bool _warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Requested = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none, with its screen edge beside it. It lives across scene loads.</summary>
        public static FocusFeel Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("FocusFeel");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<FocusFeel>();
            go.AddComponent<FocusEdge>();
            return Current;
        }

        void Awake()
        {
            _onReport = OnReport;
            _holds = HoldsAura;
            // The light: one, on a child of its own, made once and kept (as the firing weight's lights are).
            var lightObject = new GameObject("FocusLight");
            lightObject.transform.SetParent(transform, false);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
            // Its voices, of the kind the shots use, set up before they wake (as the placeholder sounds make theirs).
            var voices = new GameObject("FocusVoices");
            voices.SetActive(false);
            voices.transform.SetParent(transform, false);
            _voices = voices.AddComponent<ShotSounds>();
            _voices.Voices = VoiceCount;
            voices.SetActive(true);
            _spendClip = Clip("FocusSpend", FocusFeelRules.SpendRecipe());
            _endingClip = Clip("FocusEnding", FocusFeelRules.EndingRecipe());
            _readyClip = Clip("FocusPartReady", FocusFeelRules.ReadyRecipe());
        }

        static AudioClip Clip(string name, SynthRecipe recipe)
        {
            float[] samples = Synth.Render(recipe, Synth.SampleRate);
            AudioClip clip = AudioClip.Create(name, Mathf.Max(1, samples.Length), 1, Synth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnDisable() => StopShowing();

        void OnDestroy()
        {
            StopShowing();
            Listen(null);
            if (_spendClip != null) Destroy(_spendClip);
            if (_endingClip != null) Destroy(_endingClip);
            if (_readyClip != null) Destroy(_readyClip);
            if (Current == this) Current = null;
        }

        bool HoldsAura() => this != null && isActiveAndEnabled && _auraMade;

        void Listen(PlayerCharacter player)
        {
            if (_listening != null) _listening.Reported -= _onReport;
            _listening = player;
            if (player != null) player.Reported += _onReport;
        }

        // ---------------- Every frame ----------------

        void LateUpdate() => Tick();

        /// <summary>The frame's work: where focus stands, the aura, the light, the edge, the sounds. Public for tests.</summary>
        public void Tick()
        {
            try { Step(); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[FocusFeel] " + e);
            }
        }

        void Step()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            }
            if (_player != _listening) Listen(_player);
            if (_player == null || !Values.On)
            {
                StopShowing();
                return;
            }
            bool alive = _health == null || _health.IsAlive;
            bool held = PlayerSigns.ScreenUp;
            float remaining = _player.FocusBoostRemaining;
            FocusFeelPhase phase = alive ? FocusFeelRules.Phase(_player.IsFocusReady, _player.IsFocusBoosted, remaining, Values) : FocusFeelPhase.None;

            // A Hair Trigger part that fills: its own soft chime (the full gauge's is the placeholder sounds').
            int parts = _player.FocusPartsFull;
            if (_looked && alive && FocusFeelRules.PartReadySound(_player.FocusMode == FocusMode.HairTrigger, _partsSeen, parts, _player.IsFocusFull))
                Play(FocusFeelSound.PartReady, _readyClip, Values.ReadyVolume, 1f, held);
            _partsSeen = parts;

            if (phase == FocusFeelPhase.Ready && _phaseSeen != FocusFeelPhase.Ready) _readySince = Time.time;
            // The last second: the falling sound, once (again only after more seconds were added and ran out again).
            if (phase == FocusFeelPhase.Ending && !_endingPlayed)
            {
                _endingPlayed = true;
                Endings++;
                Play(FocusFeelSound.Ending, _endingClip, Values.EndingVolume, 1f, held);
            }
            if (phase != FocusFeelPhase.Ending) _endingPlayed = false;

            float level = phase == FocusFeelPhase.Ready ? FocusFeelRules.ReadyLevel(Time.time - _readySince, Values)
                                                        : FocusFeelRules.AuraLevel(phase, remaining, Values);
            Phase = phase;
            Level = level;

            // The aura: made when the gains start, gone the moment they end.
            bool aura = phase == FocusFeelPhase.Running || phase == FocusFeelPhase.Ending;
            if (aura && !_auraMade) MakeAura();
            else if (!aura && _auraMade) DestroyAura();
            AuraShown = aura && !held && level >= 0.5f;
            if (_ringRenderer != null) _ringRenderer.enabled = AuraShown;
            if (_orbitRenderer != null) _orbitRenderer.enabled = AuraShown;

            // The light on her: the ready glow, the aura, and over them the burst of a spend.
            float since = Time.unscaledTime - _burstAt;
            float intensity = alive && !held ? FocusFeelRules.LightIntensity(phase, level, since, _burstShare, Values) : 0f;
            if (_light != null)
            {
                bool on = intensity > 1e-3f;
                if (on)
                {
                    Vector3 p = _player.Position;
                    _light.transform.position = new Vector3(p.x, p.y + Values.AuraHeight, p.z);
                    _light.color = new Color(Values.LightR, Values.LightG, Values.LightB);
                    _light.range = since >= 0f && since < Values.BurstLightSeconds ? Values.BurstLightRange : Values.AuraRange;
                    _light.intensity = intensity;
                }
                if (_light.enabled != on) _light.enabled = on;
            }

            EdgeAlpha = held || !alive ? 0f : FocusFeelRules.EdgeAlpha(phase, level, Values);
            _phaseSeen = phase;
            _looked = true;
        }

        // ---------------- The moment of spending ----------------

        void OnReport(PlayerReport r)
        {
            if (r.Kind != PlayerReportKind.FocusPressed || r.Result != IntentResult.Started) return;
            try { Burst(r.FocusMode == FocusMode.HairTrigger ? Mathf.Clamp01(Values.PartShare) : 1f); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[FocusFeel] " + e);
            }
        }

        void Burst(float share)
        {
            if (!Values.On || _player == null) return;
            Bursts++;
            _burstAt = Time.unscaledTime;
            _burstShare = share;
            Vector3 feet = _player.Position;
            Material material = _player.ShotFxMaterial;
            bool draw = material != null && (CombatEffects.Current == null || CombatEffects.Current.Values.On);
            if (draw)
            {
                float rimGlow = _player.ShotLooks != null ? _player.ShotLooks.RimGlow : ShotFxSettings.StartRimGlow;
                float coreGlow = _player.ShotLooks != null ? _player.ShotLooks.CoreGlow : ShotFxSettings.StartCoreGlow;
                var paint = new ShotPaint(s_Rim, s_Core, rimGlow, coreGlow);
                // A wide ring glowing as hard as a thin streak would bloom into a blob: its core glows less (as the blasts do).
                var wide = new ShotPaint(s_Rim, s_Core, rimGlow, Mathf.Lerp(1f, coreGlow, 0.4f));
                ShotFx.Ring(feet, Values.BurstRadius * share, Values.BurstSeconds, wide, material);
                ShotFx.Ring(feet, Values.BurstRadius * share * 0.5f, Values.BurstSeconds * 0.75f, paint, material);
                ShotFx.Spark(feet + Vector3.up * Values.AuraHeight, Values.BurstStar * share, Values.BurstStarSeconds, paint, material, towardCamera: 1f);
            }
            HitStop.Trigger(share >= 1f ? Values.BurstFreeze : Values.PartFreeze);
            Vector3 aim = _player.AimCursor - feet;
            aim.y = 0f;
            if (aim.sqrMagnitude < 1e-6f) aim = _player.transform.forward;
            FiringWeight.Running?.Push(aim, Values.BurstKick * share);
            Play(FocusFeelSound.Spend, _spendClip, Values.SpendVolume, share >= 1f ? 1f : Values.PartPitch, PlayerSigns.ScreenUp);
        }

        // ---------------- The aura ----------------

        void MakeAura()
        {
            _auraMade = true;
            CombatEffects fx = CombatEffects.Current;
            if (fx == null || !fx.Values.On || _player == null) return;
            Vector3 feet = _player.Position;
            var ring = new FxPiece
            {
                Shape = FxShape.Outline, Moment = FxMoment.PerfectRelease, Tier = FxTier.Key, At = new Vector3(feet.x, GroundHeight, feet.z),
                Size = Values.AuraRing, Seconds = 0.1f, Rim = s_Rim, Core = s_Core, RimGlow = fx.Values.RimGlow, CoreGlow = fx.Values.CoreGlow,
                Follow = _player.transform, FollowOffset = Vector3.up * (GroundHeight - feet.y), Holds = _holds, HoldMost = HoldMost,
            };
            ring.End = ring.At;
            _ring = fx.Put(ring);
            if (_ring != null)
            {
                _ring.gameObject.name = "FocusFeel_AuraRing";
                _ringRenderer = _ring.GetComponent<MeshRenderer>();
            }
            Vector3 chest = feet + Vector3.up * Values.AuraHeight;
            var orbit = new FxPiece
            {
                Shape = FxShape.Orbit, Moment = FxMoment.PerfectRelease, Tier = FxTier.Key, At = chest, End = chest,
                Size = Values.AuraOrbit, Seconds = 0.1f, Rim = s_Rim, Core = s_Core, RimGlow = fx.Values.RimGlow, CoreGlow = fx.Values.CoreGlow,
                Follow = _player.transform, FollowOffset = chest - feet, Holds = _holds, HoldMost = HoldMost, Spin = Values.AuraSpin,
            };
            _orbit = fx.Put(orbit);
            if (_orbit != null)
            {
                _orbit.gameObject.name = "FocusFeel_AuraStars";
                _orbitRenderer = _orbit.GetComponent<MeshRenderer>();
            }
        }

        void DestroyAura()
        {
            _auraMade = false;
            Gone(ref _ring);
            Gone(ref _orbit);
            _ringRenderer = null;
            _orbitRenderer = null;
            AuraShown = false;
        }

        static void Gone(ref CombatFx piece)
        {
            if (piece != null)
            {
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
            piece = null;
        }

        void StopShowing()
        {
            if (_auraMade) DestroyAura();
            if (_light != null && _light.enabled) _light.enabled = false;
            Phase = FocusFeelPhase.None;
            Level = 0f;
            EdgeAlpha = 0f;
            _phaseSeen = FocusFeelPhase.None;
        }

        // ---------------- Its sounds ----------------

        /// <summary>
        /// One of its sounds, at a share of the basic shot's volume (as the placeholder sounds play theirs); none while a
        /// screen holds the game, or with the placeholder sounds switched off.
        /// </summary>
        void Play(FocusFeelSound sound, AudioClip clip, float share, float pitch, bool held)
        {
            if (held || clip == null || share <= 0f) return;
            if (PlaceholderSounds.Current != null && !PlaceholderSounds.Current.Values.On) return;
            float basic = 1f;
            if (_player != null && _player.ShotLooks != null && _player.ShotLooks.Basic != null)
                basic = _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
            float volume = share * basic;
            Requested?.Invoke(new FocusFeelSoundRequest { Sound = sound, Volume = volume, Pitch = pitch, Frame = Time.frameCount });
            if (_voices != null) _voices.Play(clip, volume, pitch, 0f);
        }
    }
}
