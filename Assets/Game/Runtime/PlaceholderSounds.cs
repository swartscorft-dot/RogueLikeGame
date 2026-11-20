using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>What a placeholder sound asked to play: for tests and tools, through <see cref="PlaceholderSounds.Requested"/>.</summary>
    public struct PlaceholderSoundRequest
    {
        public PlaceholderSound Sound;
        public AudioClip Clip;
        /// <summary>The volume it was asked to play at, before the voice clamps it to 0..1.</summary>
        public float Volume;
        public float Pitch;
        /// <summary>How many asked for it in the frame: several are one play, a little louder (the crowd rule).</summary>
        public int Asked;
        public int Frame;
        /// <summary>A recorded clip from the Resources folder, not the synthesised one.</summary>
        public bool Recorded;
    }

    /// <summary>
    /// The placeholder sounds (work order 021, 2026-11-20; pillar 4, "telegraphs, stagger and counter make you lose to
    /// your own mistakes": a windup she can hear is one she can dodge while looking elsewhere; the cut list's
    /// "placeholders": no sound file is added, each is made in code at start-up, <see cref="PlaceholderRecipes"/>). It
    /// only makes heard what already happens: it listens to what the game reports (<see cref="Enemy.Reported"/>,
    /// <see cref="PlayerCharacter.Reported"/>, <see cref="PlayerHealth.Damaged"/> and <see cref="PlayerHealth.Died"/>) and
    /// looks at what the run, the pause menu and the focus gauge show (<see cref="GeneratedLevels"/>, <see cref="RunHud"/>,
    /// <see cref="PlayerCharacter.IsFocusFull"/>), and changes nothing in a fight. Without it the game plays as before.
    ///
    /// Plain 2D sounds through a <see cref="ShotSounds"/> of its own (the camera is 28 m up), each at a share of the basic
    /// shot's volume (<see cref="PlaceholderSoundValues"/>), its pitch moved a few percent from a seeded source of its own
    /// (<see cref="PitchVariation"/>; never Unity's Random, which the game's own draws use). The crowd rule
    /// (<see cref="SoundCrowd"/>): asks are gathered through a frame and let through at its end, one play per sound,
    /// louder for several, and no more than so many a second.
    ///
    /// Paused is silent: while the pause menu or a choice is up nothing new starts, except the menu's tick and the
    /// choice-taken chord. Unity plays audio whatever the time scale (nothing in the game pauses the listener), so a sound
    /// already playing when the game pauses rings out (all are under 1.2 s), and the rule is kept here, by not starting
    /// new ones. A hit stop's time scale of 0 is not a pause. A moment found by looking (a floor cleared, a chest opened)
    /// is found at the end of the frame it happened in, when the choice it opens is already up: it counts as paused only
    /// when the game was paused at the end of the frame before too.
    ///
    /// Replaceable: a recorded clip at Resources/<see cref="PlaceholderSoundRules.ResourceFolder"/>/&lt;the sound's
    /// name&gt; (for example Assets/Game/Resources/Sounds/Placeholders/EnemyHit.wav) is played in place of the
    /// synthesised one, with no change to the code that decides when.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="TimingFeedback"/> (never in a test run);
    /// tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed class PlaceholderSounds : MonoBehaviour
    {
        const int VoiceCount = 12;
        const uint PitchSeed = 21u;

        /// <summary>The one running now, or null.</summary>
        public static PlaceholderSounds Current { get; private set; }

        /// <summary>Every sound asked to play, in order. Tests listen here instead of to the speakers.</summary>
        public static event Action<PlaceholderSoundRequest> Requested;

        /// <summary>
        /// Where a recorded clip is looked for, by its Resources path (<see cref="PlaceholderSoundRules.ResourcePath"/>):
        /// Resources.Load. Tests may put another in before <see cref="Ensure"/>; null for none.
        /// </summary>
        public static Func<string, AudioClip> RecordedClips = LoadFromResources;

        static AudioClip LoadFromResources(string path) => Resources.Load<AudioClip>(path);

        /// <summary>The numbers, defaults in code (work order 021: first guesses).</summary>
        public PlaceholderSoundValues Values = new PlaceholderSoundValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        readonly AudioClip[] _clips = new AudioClip[PlaceholderSoundRules.All.Length];
        readonly bool[] _recorded = new bool[PlaceholderSoundRules.All.Length];
        readonly SoundCrowd _crowd = new SoundCrowd();
        readonly PitchVariation _pitch = new PitchVariation(PitchSeed);
        readonly Dictionary<Enemy, Action<EnemyReport>> _enemies = new Dictionary<Enemy, Action<EnemyReport>>();
        ShotSounds _voices;
        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerCharacter _listeningPlayer;
        PlayerHealth _listeningHealth;
        bool _warned;

        // What was seen at the end of the frame before, for the moments found by looking.
        bool _looked;
        bool _heldBefore;
        GeneratedLevel _levelSeen;
        bool _clearedSeen;
        int _chestsSeen;
        RunRecord _recordSeen;
        int _picksSeen;
        bool _pausedSeen;
        int _lineSeen;
        bool _focusSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Requested = null;
            RecordedClips = LoadFromResources;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static PlaceholderSounds Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("PlaceholderSounds");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<PlaceholderSounds>();
            return Current;
        }

        /// <summary>The clip a sound plays: the recorded one if there is one, else the synthesised one.</summary>
        public AudioClip ClipOf(PlaceholderSound sound) => _clips[(int)sound];

        /// <summary>True when a sound plays a recorded clip from the Resources folder.</summary>
        public bool IsRecorded(PlaceholderSound sound) => _recorded[(int)sound];

        void Awake()
        {
            MakeClips();
            // Its own voices, of the kind the shots use: plain 2D sounds, the listener's volume over them.
            var voices = new GameObject("PlaceholderVoices");
            voices.SetActive(false);
            voices.transform.SetParent(transform, false);
            _voices = voices.AddComponent<ShotSounds>();
            _voices.Voices = VoiceCount;
            voices.SetActive(true);
            Enemy.Appeared += OnAppeared;
            Safely(Find);
        }

        void OnDestroy()
        {
            Enemy.Appeared -= OnAppeared;
            Listen(null, null);
            foreach (KeyValuePair<Enemy, Action<EnemyReport>> pair in _enemies)
                if (pair.Key != null) pair.Key.Reported -= pair.Value;
            _enemies.Clear();
            for (int i = 0; i < _clips.Length; i++)
                if (_clips[i] != null && !_recorded[i]) Destroy(_clips[i]);
            if (Current == this) Current = null;
        }

        void MakeClips()
        {
            Func<string, AudioClip> recorded = RecordedClips;
            foreach (PlaceholderSound sound in PlaceholderSoundRules.All)
            {
                AudioClip clip = null;
                try { clip = recorded?.Invoke(PlaceholderSoundRules.ResourcePath(sound)); }
                catch (Exception e) { Debug.LogWarning("[PlaceholderSounds] " + e.Message); }
                _recorded[(int)sound] = clip != null;
                if (clip == null)
                {
                    float[] samples = Synth.Render(PlaceholderRecipes.For(sound), Synth.SampleRate);
                    clip = AudioClip.Create("Placeholder" + sound, samples.Length, 1, Synth.SampleRate, false);
                    clip.SetData(samples, 0);
                }
                _clips[(int)sound] = clip;
            }
        }

        // ---------------- Which report is which moment ----------------

        /// <summary>The moment an enemy's report is, or none (work order 021).</summary>
        public static SoundMoment? MomentOf(EnemyReportKind kind)
        {
            switch (kind)
            {
                case EnemyReportKind.AttackStarted: return SoundMoment.WindupStarted;
                case EnemyReportKind.Struck: return SoundMoment.EnemyStruck;
                case EnemyReportKind.Died: return SoundMoment.EnemyDied;
                case EnemyReportKind.Dazed: return SoundMoment.EnemyDazed;
                case EnemyReportKind.Charged: return SoundMoment.BossCharged;
                case EnemyReportKind.CalledPack: return SoundMoment.BossCalled;
                case EnemyReportKind.Angered: return SoundMoment.BossAngered;
                default: return null;
            }
        }

        /// <summary>The moment one of her reports is, or none: a hit that did damage, a dash that started.</summary>
        public static SoundMoment? MomentOf(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.Hit && r.Damage > 0) return SoundMoment.EnemyHit;
            if (r.Kind == PlayerReportKind.DashStarted) return SoundMoment.Dashed;
            return null;
        }

        // ---------------- Listening ----------------

        void Find()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            }
            if (_player != _listeningPlayer || _health != _listeningHealth) Listen(_player, _health);
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++) Hear(all[i]);
            if (_enemies.Count > 64) ForgetGone();
        }

        void Listen(PlayerCharacter player, PlayerHealth health)
        {
            if (_listeningPlayer != null) _listeningPlayer.Reported -= OnPlayer;
            if (_listeningHealth != null)
            {
                _listeningHealth.Damaged -= OnHurt;
                _listeningHealth.Died -= OnDied;
            }
            _listeningPlayer = player;
            _listeningHealth = health;
            if (player != null) player.Reported += OnPlayer;
            if (health != null)
            {
                health.Damaged += OnHurt;
                health.Died += OnDied;
            }
        }

        void OnAppeared(Enemy enemy) => Safely(() => Hear(enemy));

        void Hear(Enemy enemy)
        {
            if (enemy == null || _enemies.ContainsKey(enemy)) return;
            Action<EnemyReport> handler = r => OnEnemy(enemy, r);
            enemy.Reported += handler;
            _enemies[enemy] = handler;
        }

        void ForgetGone()
        {
            var gone = new List<Enemy>();
            foreach (Enemy e in _enemies.Keys) if (e == null) gone.Add(e);
            foreach (Enemy e in gone) _enemies.Remove(e);
        }

        /// <summary>Nothing it does may break the fight it listens to: a fault here is logged once and dropped.</summary>
        void Safely(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[PlaceholderSounds] " + e);
            }
        }

        void OnEnemy(Enemy enemy, EnemyReport r) => Safely(() =>
        {
            SoundMoment? moment = MomentOf(r.Kind);
            if (!moment.HasValue) return;
            // The windup's attack is the enemy's own at its report: blue when it can be countered (work order 015).
            bool counterable = r.Kind == EnemyReportKind.AttackStarted && enemy != null && PoolRules.IsCounterable(enemy.Attack);
            Ask(PlaceholderSoundRules.SoundFor(moment.Value, r.EnemyKind, counterable));
        });

        void OnPlayer(PlayerReport r) => Safely(() =>
        {
            SoundMoment? moment = MomentOf(r);
            if (moment.HasValue) Ask(PlaceholderSoundRules.SoundFor(moment.Value));
        });

        void OnHurt(int amount) => Safely(() =>
            Ask(PlaceholderSound.Hurt, PlaceholderSoundRules.HurtGain(amount, _health != null ? _health.Max : Placeholders.PlayerHealth, Values)));

        void OnDied() => Safely(() => Ask(PlaceholderSound.HerDeath));

        /// <summary>The pause menu or a choice is up now.</summary>
        static bool Held
        {
            get
            {
                GeneratedLevels levels = GeneratedLevels.Current;
                return levels != null && (levels.Paused || levels.Choice != GeneratedLevels.ChoiceKind.None);
            }
        }

        /// <summary>
        /// Asks for a sound this frame, unless it is off or the game is paused. <paramref name="lookedFor"/>: a moment found
        /// at the end of the frame, judged by whether the game was paused at the end of the frame before.
        /// </summary>
        void Ask(PlaceholderSound? sound, float gain = 1f, bool lookedFor = false)
        {
            if (!sound.HasValue || !Values.On) return;
            bool paused = lookedFor ? _heldBefore && Held : Held;
            if (!PlaceholderSoundRules.MayStart(sound.Value, paused)) return;
            _crowd.Ask(sound.Value, gain);
        }

        // ---------------- Every frame ----------------

        void LateUpdate()
        {
            Safely(Find);
            Safely(Look);
            Safely(Flush);
            _heldBefore = Held;
        }

        /// <summary>The moments found by looking: the run's (a floor or the act cleared, a chest, a pick), the pause menu's, her focus.</summary>
        void Look()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            bool cleared = levels != null && levels.Cleared;
            int chests = levels != null ? levels.ChestsOpened.Count : 0;
            RunRecord record = levels != null ? levels.Record : null;
            int picks = record != null ? record.Skills.Count + record.Upgrades.Count : 0;
            bool paused = levels != null && levels.Paused;
            int line = RunHud.Current != null ? RunHud.Current.Selected : 0;
            bool focus = _player != null && _player.IsFocusFull;

            if (_looked)
            {
                if (level != null && level == _levelSeen)
                {
                    if (cleared && !_clearedSeen)
                        Ask(levels.ActCleared ? PlaceholderSound.ActCleared : PlaceholderSound.FloorCleared, lookedFor: true);
                    if (chests > _chestsSeen) Ask(PlaceholderSound.ChestOpens, lookedFor: true);
                }
                if (record != null && record == _recordSeen && picks > _picksSeen) Ask(PlaceholderSound.ChoiceTaken, lookedFor: true);
                if (paused != _pausedSeen) Ask(PlaceholderSound.Menu, lookedFor: true);
                else if (paused && line != _lineSeen) Ask(PlaceholderSound.Menu, lookedFor: true);
                if (focus && !_focusSeen) Ask(PlaceholderSound.FocusFull, lookedFor: true);
            }
            _looked = true;
            _levelSeen = level;
            _clearedSeen = cleared;
            _chestsSeen = chests;
            _recordSeen = record;
            _picksSeen = picks;
            _pausedSeen = paused;
            _lineSeen = line;
            _focusSeen = focus;
        }

        void Flush()
        {
            if (!_crowd.Waiting) return;
            IReadOnlyList<SoundPlay> plays = _crowd.Flush(Time.unscaledTime, Values);
            if (plays.Count == 0) return;
            // A share of the basic shot's volume, as the shot itself is played (PlayerCharacter.PlaySound).
            float basic = 1f;
            if (_player != null && _player.ShotLooks != null && _player.ShotLooks.Basic != null)
                basic = _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
            for (int i = 0; i < plays.Count; i++)
            {
                SoundPlay play = plays[i];
                AudioClip clip = _clips[(int)play.Sound];
                float volume = Values.Volume(play.Sound) * Values.Master * basic * play.Gain;
                float pitch = _pitch.Next(Values.PitchSpread);
                if (clip == null || volume <= 0f) continue;
                Requested?.Invoke(new PlaceholderSoundRequest
                {
                    Sound = play.Sound, Clip = clip, Volume = volume, Pitch = pitch, Asked = play.Asked,
                    Frame = Time.frameCount, Recorded = _recorded[(int)play.Sound],
                });
                if (_voices != null) _voices.Play(clip, volume, pitch, 0f);
            }
        }
    }
}
