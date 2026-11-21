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
        /// <summary>Work order 048: the bells it rings, the offer's two for an epic card (the second a little later); 1 for every other.</summary>
        public int Bells;
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
    ///
    /// The new things of 2026-11-20 (work order 048, Kristian's wish for their sounds), each from what the game reports:
    /// what flies stopping on a wall, a pillar or a prop (the end of a thing that flew, <see cref="EnemyReport.Flown"/>, but
    /// not one that met her or ran out of flight, and not one her quick duck stopped, which has its own impact), no more
    /// often than <see cref="PlaceholderSoundValues.StoppedGap"/>; the caster's orb let go (<see cref="ThingsThatFly.Loosed"/>);
    /// her quick duck, her stun and her pull (her reports); a drop coming to rest and being picked up (<see cref="RewardDrop"/>,
    /// <see cref="GeneratedLevels"/> tell it with <see cref="AskFor(SoundMoment)"/>), a combat room cleared (the run's heal
    /// for it tells it); a crit of hers, unless the clean press's or the perfect release's own tone rang that frame
    /// (<see cref="TimingFeedback.TonePlayed"/>); and a choice coming up with a rare or an epic card, found by looking.
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

        /// <summary>For tests (work order 033): the enemies it listens to, and how many times it looked for gone ones.</summary>
        public int Listening => _enemies.Count;
        public int GoneLooks { get; private set; }

        readonly AudioClip[] _clips = new AudioClip[PlaceholderSoundRules.All.Length];
        readonly bool[] _recorded = new bool[PlaceholderSoundRules.All.Length];
        readonly SoundCrowd _crowd = new SoundCrowd();
        readonly RapidStrikeGate _rapidStrikes = new RapidStrikeGate(); // work order 039
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

        // Work order 048.
        readonly RapidStrikeGate _stops = new RapidStrikeGate(); // what flies stopping: one knock for a fan
        GeneratedLevels.ChoiceKind _choiceSeen;
        int _crits;              // crits of hers this frame, heard at its end unless a tone of their own rang
        bool _ownTick;           // the clean press's or the perfect release's tone rang this frame
        int _bells;              // the bells of the offer asked for this frame
        float _secondBellAt = -1f;
        float _secondBellVolume, _secondBellPitch;
        TimingFeedback _listeningFeedback;
        Action<FeedbackTone> _onTone;
        Action _find, _look, _flush; // made once: nothing is allocated in a frame while nothing plays

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

        /// <summary>
        /// A sound asked for by another of the game's signs (work order 037: the clean window's tick), under the same rules
        /// as its own: nothing while it is off or the game is paused, and the crowd rule at the end of the frame.
        /// </summary>
        public void AskFor(PlaceholderSound sound, float gain = 1f) => Safely(() => Ask(sound, gain));

        /// <summary>
        /// A moment told by the place it happens (work order 048: a drop landing, a drop picked up, a combat room cleared),
        /// its sound as <see cref="PlaceholderSoundRules.SoundFor"/> picks it, under the same rules as every other.
        /// </summary>
        public void AskFor(SoundMoment moment) => Safely(() => Ask(PlaceholderSoundRules.SoundFor(moment)));

        void Awake()
        {
            _find = Find;
            _look = Look;
            _flush = Flush;
            _onTone = OnTone;
            MakeClips();
            // Its own voices, of the kind the shots use: plain 2D sounds, the listener's volume over them.
            var voices = new GameObject("PlaceholderVoices");
            voices.SetActive(false);
            voices.transform.SetParent(transform, false);
            _voices = voices.AddComponent<ShotSounds>();
            _voices.Voices = VoiceCount;
            voices.SetActive(true);
            Enemy.Appeared += OnAppeared;
            ThingsThatFly.Loosed += OnLoosed; // work order 036
            Safely(_find);
        }

        void OnDestroy()
        {
            Enemy.Appeared -= OnAppeared;
            ThingsThatFly.Loosed -= OnLoosed;
            Listen(null, null);
            ListenToTones(null);
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

        /// <summary>
        /// The moment one of her reports is, or none: a hit that did damage, a dash that started; and (work order 048) a
        /// quick duck, a stun and a pull that began. A crit is a hit's too, heard at the frame's end (<see cref="OnPlayer"/>).
        /// </summary>
        public static SoundMoment? MomentOf(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.Hit && r.Damage > 0) return SoundMoment.EnemyHit;
            if (r.Kind == PlayerReportKind.DashStarted) return SoundMoment.Dashed;
            if (r.Kind == PlayerReportKind.Ducked) return SoundMoment.Ducked;
            if (r.Kind == PlayerReportKind.Stunned) return SoundMoment.HerStunned;
            if (r.Kind == PlayerReportKind.Pulled) return SoundMoment.HerPulled;
            return null;
        }

        /// <summary>
        /// The moment the end of a thing that flew is (work order 048), or none: stopped by a wall, a pillar or a prop, or by
        /// what she is behind. One that met her is her hurt's; one that ran out of flight, or passed her, is silent.
        /// </summary>
        public static SoundMoment? MomentOfFlown(StrikeOutcome outcome) =>
            outcome == StrikeOutcome.StoppedByWall || outcome == StrikeOutcome.StoppedByCover ? SoundMoment.ProjectileStopped : (SoundMoment?)null;

        // ---------------- Listening ----------------

        void Find()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            }
            if (_player != _listeningPlayer || _health != _listeningHealth) Listen(_player, _health);
            TimingFeedback feedback = TimingFeedback.Current; // work order 048: its clean and perfect tones
            if (!ReferenceEquals(feedback, _listeningFeedback)) ListenToTones(feedback);
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++) Hear(all[i]);
            // Looked for only when a target was destroyed since the last look (the second reviews of 2026-11-20, work
            // order 033, as work order 026 did in TimingFeedback): not every frame once there are many.
            if (_enemies.Count > 64 && Damageable.Destroyed != _forgotAt) ForgetGone();
        }

        int _forgotAt = -1;
        readonly List<Enemy> _gone = new List<Enemy>();

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

        void ListenToTones(TimingFeedback feedback)
        {
            if (_listeningFeedback != null) _listeningFeedback.TonePlayed -= _onTone;
            _listeningFeedback = feedback;
            if (feedback != null) feedback.TonePlayed += _onTone;
        }

        /// <summary>A clean press's or a perfect release's tone rang: a crit of this frame has its tick already (work order 048).</summary>
        void OnTone(FeedbackTone tone)
        {
            if (tone == FeedbackTone.Clean || tone == FeedbackTone.Perfect) _ownTick = true;
        }

        void OnAppeared(Enemy enemy) => Safely(() => Hear(enemy));

        /// <summary>
        /// Listens to an enemy not yet heard. Asked for every enemy every frame (<see cref="Find"/>), so the handler's closure
        /// is made in <see cref="StartHearing"/> alone: a lambda that captures a parameter has its closure made as the method
        /// begins, before any early return, and here that was one allocation an enemy a frame (the first Unity run of the
        /// night's combination, 2026-11-21; work order 056).
        /// </summary>
        void Hear(Enemy enemy)
        {
            if (enemy == null || _enemies.ContainsKey(enemy)) return;
            StartHearing(enemy);
        }

        void StartHearing(Enemy enemy)
        {
            Action<EnemyReport> handler = r => OnEnemy(enemy, r);
            enemy.Reported += handler;
            _enemies[enemy] = handler;
        }

        void ForgetGone()
        {
            GoneLooks++;
            _forgotAt = Damageable.Destroyed;
            _gone.Clear();
            foreach (Enemy e in _enemies.Keys) if (e == null) _gone.Add(e);
            foreach (Enemy e in _gone) _enemies.Remove(e);
            _gone.Clear();
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
            // Work order 036: the end of a thing that flew is no strike to hear: it was heard leaving, and her hurt is heard on her.
            // Work order 048: but one stopped by a wall, a pillar or a prop knocks, one knock for a fan; one her quick duck
            // stopped has the duck's own impact (PlayerCharacter), and no knock over it.
            SoundMoment? moment = r.Flown ? MomentOfFlown(r.Outcome) : MomentOf(r.Kind);
            if (!moment.HasValue) return;
            if (moment.Value == SoundMoment.ProjectileStopped
                && ((r.Outcome == StrikeOutcome.StoppedByCover && _player != null && _player.IsQuickDucking) || !_stops.Ask(Time.time, Values.StoppedGap)))
                return;
            // The windup's attack is the enemy's own at its report: blue when it can be countered (work order 015).
            bool counterable = r.Kind == EnemyReportKind.AttackStarted && enemy != null && PoolRules.IsCounterable(enemy.Attack);
            // An ordinary attack's windup is silent (work order 035): the windup sounds mean a skill.
            bool ordinary = r.Kind == EnemyReportKind.AttackStarted && enemy != null && enemy.Attack != null && enemy.Attack.Ordinary;
            // A rapid ordinary attack's strike (the swarmer's bite, three a second; work order 039) is heard, across all
            // enemies, no more often than RapidStrikeGap: eight swarmers on her must not buzz.
            if (r.Kind == EnemyReportKind.Struck && enemy is MeleeEnemy melee
                && MeleeYouReadRules.IsRapid(MeleeAttacks.Find(melee.AttacksNow, r.Attack))
                && !_rapidStrikes.Ask(Time.time, Values.RapidStrikeGap))
                return;
            Ask(PlaceholderSoundRules.SoundFor(moment.Value, r.EnemyKind, counterable, ordinary));
        });

        /// <summary>
        /// Work order 036: what flies is heard as it leaves, with its kind's strike (the shooter's arrow, the others' strike);
        /// the caster's orb with a sound of its own since work order 048.
        /// </summary>
        void OnLoosed(Enemy enemy, EnemyAttackDefinition attack, int count) => Safely(() =>
        {
            if (enemy != null) Ask(PlaceholderSoundRules.SoundFor(SoundMoment.Loosed, enemy.Kind));
        });

        void OnPlayer(PlayerReport r) => Safely(() =>
        {
            SoundMoment? moment = MomentOf(r);
            if (moment.HasValue) Ask(PlaceholderSoundRules.SoundFor(moment.Value));
            // Work order 048: a crit (only with the stats and tiers on; Deal rolls none otherwise) waits for the frame's end,
            // when it is known whether the clean press's or the perfect release's tone rang for it. Paused as it happened: none.
            if (moment == SoundMoment.EnemyHit && r.Crit && Values.On && PlaceholderSoundRules.MayStart(PlaceholderSound.Crit, Held)) _crits++;
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

        void LateUpdate() => Tick();

        /// <summary>
        /// The frame's work: listening, looking, the crowd let through at the frame's end. Public for the test that counts
        /// what it allocates (work order 048): nothing while nothing plays.
        /// </summary>
        public void Tick()
        {
            Safely(_find);
            Safely(_look);
            AskForCrits();
            Safely(_flush);
            RingSecondBell();
            _bells = 0;
            _heldBefore = Held;
        }

        /// <summary>
        /// Work order 048: this frame's crits, one play for them all (the crowd rule), unless the clean press's or the
        /// perfect release's tone rang this frame (<see cref="PlaceholderSoundRules.CritSound"/>).
        /// </summary>
        void AskForCrits()
        {
            PlaceholderSound? crit = _crits > 0 && Values.On ? PlaceholderSoundRules.CritSound(_ownTick) : null;
            if (crit.HasValue)
                for (int i = 0; i < _crits; i++) _crowd.Ask(crit.Value); // paused or not was judged as each happened
            _crits = 0;
            _ownTick = false;
        }

        /// <summary>An epic offer's second bell, when its time has come (real time: the choice holds the game).</summary>
        void RingSecondBell()
        {
            if (_secondBellAt < 0f || Time.unscaledTime < _secondBellAt) return;
            _secondBellAt = -1f;
            AudioClip clip = _clips[(int)PlaceholderSound.RareOffer];
            if (Values.On && clip != null && _voices != null) _voices.Play(clip, _secondBellVolume, _secondBellPitch, 0f);
        }

        /// <summary>The moments found by looking: the run's (a floor or the act cleared, a chest, a pick), the pause menu's, her focus.</summary>
        void Look()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            bool cleared = levels != null && levels.Cleared;
            int chests = levels != null ? levels.ChestsOpened.Count : 0;
            RunRecord record = levels != null ? levels.Record : null;
            // A skill upgrade from an elite room is a pick too (where work orders 017 and 021 met, 2026-11-20).
            // And the focus choice (work order 025). The one count the effects use as well (work order 033).
            int picks = record != null ? record.Picks : 0;
            bool paused = levels != null && levels.Paused;
            int line = RunHud.Current != null ? RunHud.Current.Selected : 0;
            bool focus = _player != null && _player.IsFocusFull;
            GeneratedLevels.ChoiceKind choice = levels != null ? levels.Choice : GeneratedLevels.ChoiceKind.None;

            // Work order 048: a choice that came up with a rare or an epic card rings, heard with it up as a chest's is.
            if (_looked && choice != GeneratedLevels.ChoiceKind.None && choice != _choiceSeen)
            {
                int bells = PlaceholderSoundRules.OfferBells(BestTier(levels.OfferedUpgrades));
                if (bells > 0)
                {
                    _bells = Math.Max(_bells, bells);
                    Ask(PlaceholderSoundRules.SoundFor(SoundMoment.RareOffered), lookedFor: true);
                }
            }
            _choiceSeen = choice;

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

        /// <summary>The best tier of the cards on offer that have one (work order 050's chest cards), or none. Indexed: nothing made.</summary>
        static UpgradeTier? BestTier(IReadOnlyList<Upgrade> offer)
        {
            UpgradeTier? best = null;
            if (offer == null) return null;
            for (int i = 0; i < offer.Count; i++)
            {
                Upgrade u = offer[i];
                if (u == null || !u.Tiered) continue;
                if (!best.HasValue || u.Tier > best.Value) best = u.Tier;
            }
            return best;
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
                // Work order 048: an epic offer's bell rings twice, the second a fourth up a moment later.
                int bells = play.Sound == PlaceholderSound.RareOffer ? Math.Max(1, _bells) : 1;
                Requested?.Invoke(new PlaceholderSoundRequest
                {
                    Sound = play.Sound, Clip = clip, Volume = volume, Pitch = pitch, Asked = play.Asked,
                    Frame = Time.frameCount, Recorded = _recorded[(int)play.Sound], Bells = bells,
                });
                if (_voices != null) _voices.Play(clip, volume, pitch, 0f);
                if (bells > 1)
                {
                    _secondBellAt = Time.unscaledTime + Mathf.Max(0f, Values.EpicNoteGap);
                    _secondBellVolume = volume;
                    _secondBellPitch = pitch * PlaceholderSoundRules.EpicNoteStep;
                }
            }
        }
    }
}
