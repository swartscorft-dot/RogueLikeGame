using System;
using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>One word or number as it shows now: for drawing, and for tests (work order 019).</summary>
    public struct FeedbackShown
    {
        public int Id;
        public string Text;
        public Color Color;
        /// <summary>The size of its text, in pixels of a view 720 high, before the pop.</summary>
        public int FontSize;
        /// <summary>Where it is anchored in the world: over its target, her, or a keg.</summary>
        public Vector3 World;
        /// <summary>The target it is over, or null for her or a keg.</summary>
        public Damageable Target;
        public bool OverHer;
        public float Age;
        public float Alpha;
        // Numbers only.
        public int Sum;
        public int Hits;
        public NumberKind Kind;
        // Work order 045: what the interface's skin draws it by.
        /// <summary>True for a word, false for a number.</summary>
        public bool IsWord;
        /// <summary>A word's moment, colour and size as the rules gave them.</summary>
        public FeedbackMoment Moment;
        public FeedbackTint Tint;
        public FeedbackSize Size;
        // Work order 064: the kinetic numbers.
        /// <summary>A number's lane in a crowd (<see cref="TallyNumber.Lane"/>), and its life after its latest hit.</summary>
        public int Lane;
        public float Life;
    }

    /// <summary>
    /// The signs that she got it right (work order 019, 2026-11-20; pillar 2, "high tempo with skill expression": a
    /// timing skill that gives no sign of success is guessing). It only shows and sounds what already happens: it listens
    /// to what the game reports (<see cref="PlayerCharacter.Reported"/>, <see cref="Damageable.StaggerTaken"/>,
    /// <see cref="PlayerHealth.Damaged"/>) and changes nothing in a fight.
    ///
    /// Words, over the place it happened, rising a little and fading over <see cref="FeedbackValues.WordSeconds"/>:
    /// CLEAN over the target a clean tap or Backstep's clean second press hit (over her if it hit nothing), PERFECT over
    /// the first target a perfect release hit, COUNTER over a countered enemy, "x3" over each target marks were taken
    /// from (red and larger at the cap), BOOM over a keg set off by a shot, DAZED over a target whose stagger bar was
    /// emptied, and EARLY or LATE over her, small and grey, for a press that missed a timing that exists
    /// (<see cref="FeedbackRules.RecastMiss"/>, <see cref="FeedbackRules.ChargeMiss"/>). One word per target, eight at
    /// most, the newest win (<see cref="WordBoard"/>).
    ///
    /// Damage numbers: each hit of hers over its target, hits close together summed (<see cref="DamageTally"/>), larger
    /// and in their word's colour for marks spent, a perfect release or a dazed target; what she takes, red over her.
    /// The pause menu turns them off (<see cref="GameSettings.DamageNumbers"/>); the words stay.
    ///
    /// Tones, made in code at start-up (<see cref="ToneRules"/>), through a <see cref="ShotSounds"/> of its own at a
    /// share of the basic shot's volume: one for a clean tap, one for a perfect release, one for a counter, each once,
    /// with its word. A missed timing has none.
    ///
    /// Drawn with IMGUI like the skill bar, laid out for a view 720 pixels high; what it shows is worked out every frame
    /// also without a window, for tests (<see cref="Words"/>, <see cref="Numbers"/>). It installs itself when Play
    /// starts under the same rule as <see cref="RunHud"/> (never in a test run); tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed partial class TimingFeedback : MonoBehaviour
    {
        /// <summary>The key words and numbers over her go under; a keg's word gets its own, below it.</summary>
        const int HerKey = 0;
        const int HerHealKey = int.MinValue; // work order 030: her green heal numbers, apart from her red ones
        const float KegHeight = 1f;          // m over a keg's middle: where its bang is drawn
        const float OverHead = 1.7f;         // m over the top of a target: above its mark pips and its stagger bar
        const float NumberSide = 40f;        // px right of a word's place that a number goes, so they do not cover each other
        const float HerNumberSide = -44f;    // px: her red numbers go to the left
        const float Outline = 2f;            // px of dark outline round the text
        const int ToneVoices = 4;

        // The colours, each taken from where the order names it.
        static readonly Color s_Clean = new Color(0.4f, 1f, 0.5f, 1f);        // SkillBar's clean colour
        static readonly Color s_Counter = new Color(0.15f, 0.45f, 1f, 1f);    // Enemy's counterable telegraph
        static readonly Color s_Marks = new Color(1f, 0.55f, 0.1f, 1f);       // MarkPips' rim
        static readonly Color s_MarksFull = new Color(1f, 0.15f, 0.08f, 1f);  // MarkPips' rim at the cap
        static readonly Color s_Boom = new Color(1f, 0.62f, 0.15f, 1f);
        static readonly Color s_Dazed = new Color(0.55f, 0.75f, 1f, 1f);      // Enemy's daze tint
        static readonly Color s_Miss = new Color(0.7f, 0.7f, 0.7f, 1f);
        static readonly Color s_Plain = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Hurt = new Color(1f, 0.22f, 0.18f, 1f);
        static readonly Color s_Shadow = new Color(0f, 0f, 0f, 0.9f);

        /// <summary>The one running now, or null.</summary>
        public static TimingFeedback Current { get; private set; }

        /// <summary>Are damage numbers shown now? The pause menu's choice, else the default of the one running.</summary>
        public static bool NumbersOn => GameSettings.DamageNumbers(Current != null ? Current.Values.DamageNumbers : s_DefaultNumbers);
        static readonly bool s_DefaultNumbers = new FeedbackValues().DamageNumbers;

        /// <summary>The numbers, defaults in code (work order 019: first guesses).</summary>
        public FeedbackValues Values = new FeedbackValues();

        /// <summary>
        /// The damage numbers in the kinetic anime's style (work order 064: sizes, pop, drift, lives, lanes), defaults in code;
        /// off, the numbers as this morning.
        /// </summary>
        public HitNumberValues NumberLook = new HitNumberValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>A word was put up: its text, where, over what. For tests.</summary>
        public event Action<FeedbackShown> WordShown;
        /// <summary>A tone was asked for. For tests; the sound itself goes through <see cref="ShotSounds"/>.</summary>
        public event Action<FeedbackTone> TonePlayed;

        /// <summary>The words showing now, oldest first.</summary>
        public IReadOnlyList<FeedbackShown> Words => _shownWords;
        /// <summary>The damage numbers showing now, oldest first.</summary>
        public IReadOnlyList<FeedbackShown> Numbers => _shownNumbers;

        public AudioClip CleanTone => _tones[(int)FeedbackTone.Clean];
        public AudioClip PerfectTone => _tones[(int)FeedbackTone.Perfect];
        public AudioClip CounterTone => _tones[(int)FeedbackTone.Counter];
        public AudioClip ToneClip(FeedbackTone tone) => _tones[(int)tone];

        struct Anchor
        {
            public Damageable Target;
            public bool Her;
            public Vector3 Last;     // where it was last seen, kept when the target is gone
            public float Height;     // m over its ground point
        }

        struct PendingHit
        {
            public int Slot;
            public Damageable Target;
            public int Damage;
            public int MarksSpent;
            public bool OnDazed;
            public bool Crit; // work order 050: shown as the strong kind, the perfect release's
        }

        readonly WordBoard _board = new WordBoard();
        readonly DamageTally _tally = new DamageTally();
        readonly Dictionary<int, Anchor> _anchors = new Dictionary<int, Anchor>();
        readonly Dictionary<Damageable, int> _keys = new Dictionary<Damageable, int>();
        readonly Dictionary<Damageable, int> _marksSpent = new Dictionary<Damageable, int>();
        readonly Dictionary<Damageable, int> _emptiedAt = new Dictionary<Damageable, int>(); // the frame its bar was emptied
        readonly Dictionary<Damageable, Action<float, float>> _listening = new Dictionary<Damageable, Action<float, float>>();
        readonly HashSet<int> _cleanPending = new HashSet<int>(); // slots whose shot comes from a clean press and has not gone off
        readonly List<PendingHit> _pending = new List<PendingHit>();
        readonly List<FeedbackShown> _shownWords = new List<FeedbackShown>();
        readonly List<FeedbackShown> _shownNumbers = new List<FeedbackShown>();
        readonly List<int> _scratch = new List<int>();
        readonly AudioClip[] _tones = new AudioClip[3];
        int _nextKey = 1;
        int _nextKeg = -1;

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerCharacter _listeningPlayer;
        PlayerHealth _listeningHealth;
        ShotSounds _sounds;
        GUIStyle _style;
        bool _warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static TimingFeedback Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("TimingFeedback");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<TimingFeedback>();
            return Current;
        }

        void Awake()
        {
            MakeTones();
            // Its own voices, of the kind the shots use: plain 2D sounds, the listener's volume over them.
            var voices = new GameObject("FeedbackTones");
            voices.SetActive(false);
            voices.transform.SetParent(transform, false);
            _sounds = voices.AddComponent<ShotSounds>();
            _sounds.Voices = ToneVoices;
            voices.SetActive(true);
            Find();
        }

        void OnDestroy()
        {
            Listen(null, null);
            foreach (KeyValuePair<Damageable, Action<float, float>> pair in _listening)
                if (pair.Key != null) pair.Key.StaggerTaken -= pair.Value;
            _listening.Clear();
            foreach (AudioClip clip in _tones) if (clip != null) Destroy(clip);
            if (Current == this) Current = null;
        }

        void MakeTones()
        {
            foreach (FeedbackTone tone in new[] { FeedbackTone.Clean, FeedbackTone.Perfect, FeedbackTone.Counter })
            {
                float[] samples = ToneRules.Samples(tone, ToneRules.SampleRate);
                AudioClip clip = AudioClip.Create("Tone" + tone, samples.Length, 1, ToneRules.SampleRate, false);
                clip.SetData(samples, 0);
                _tones[(int)tone] = clip;
            }
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
            // Every target with a stagger bar, for DAZED: those made since the last look too.
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || _listening.ContainsKey(d)) continue;
                ListenTo(d);
            }
        }

        /// <summary>
        /// Listens to a target's stagger bar. Its own method (work order 045): a lambda capturing a variable of the loop's
        /// body makes the compiler allocate the closure at the top of that body, on every pass of every frame, also for the
        /// targets already listened to; here it is made only for a new one.
        /// </summary>
        void ListenTo(Damageable d)
        {
            Action<float, float> handler = (taken, left) => OnStaggerTaken(d, taken, left);
            d.StaggerTaken += handler;
            _listening[d] = handler;
        }

        void Listen(PlayerCharacter player, PlayerHealth health)
        {
            if (_listeningPlayer != null) _listeningPlayer.Reported -= OnPlayer;
            if (_listeningHealth != null) _listeningHealth.Damaged -= OnHurt;
            _listeningPlayer = player;
            _listeningHealth = health;
            if (player != null) player.Reported += OnPlayer;
            if (health != null) health.Damaged += OnHurt;
        }

        /// <summary>Nothing it does may break the fight it listens to: a fault here is logged once and dropped.</summary>
        void Safely(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[TimingFeedback] " + e);
            }
        }

        void OnPlayer(PlayerReport r) => Safely(() => Take(r));

        /// <summary>
        /// Health she got back (work order 030: a combat room cleared): a green "+8" over her, to the other side from her
        /// red numbers. Off with the damage numbers.
        /// </summary>
        public void ShowHeal(int amount) => Safely(() =>
        {
            if (amount <= 0 || !NumbersOn) return;
            _tally.Add(HerHealKey, amount, NumberKind.Heal, Time.time, MergeWindow, Values.MaxNumbers);
            if (!_anchors.ContainsKey(HerHealKey) && _player != null)
                _anchors[HerHealKey] = new Anchor { Her = true, Last = _player.Position, Height = TopOf(_player.gameObject, null) + OverHead };
        });

        void OnHurt(int amount) => Safely(() =>
        {
            if (NumbersOn) _tally.Add(HerKey, amount, NumberKind.Hurt, Time.time, MergeWindow, Values.MaxNumbers);
            AnchorHer();
        });

        void OnStaggerTaken(Damageable target, float taken, float left) => Safely(() =>
        {
            // The bar was emptied by this hit: it is dazed now (StaggerBar.Take leaves it at 0 when it does).
            if (taken <= 0f || left > 1e-4f || target == null) return;
            _emptiedAt[target] = Time.frameCount;
            ShowWord(target, FeedbackRules.Word(FeedbackMoment.Dazed));
        });

        float MergeWindow => Values.MergeSeconds + Values.MergeGrace;

        void Take(PlayerReport r)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            SkillDefinition skill = skills != null && r.Slot >= 0 && r.Slot < skills.Count ? skills[r.Slot] : null;
            switch (r.Kind)
            {
                case PlayerReportKind.MarksSpent:
                    if (r.Marks <= 0 || r.Target == null) break;
                    _marksSpent[r.Target] = r.Marks;
                    ShowWord(r.Target, FeedbackRules.Word(FeedbackMoment.MarksSpent, r.Marks, _player != null ? _player.Kit.MaxMarks : 0));
                    break;
                case PlayerReportKind.Hit:
                    if (r.Target == null) break;
                    int spent = _marksSpent.TryGetValue(r.Target, out int m) ? m : 0;
                    _marksSpent.Remove(r.Target);
                    // A hit on a dazed target hits harder (StaggerBar.Scale); the hit that emptied the bar did not.
                    bool onDazed = r.Target.Stagger != null && r.Target.Stagger.IsDazed(Time.time)
                                   && !(_emptiedAt.TryGetValue(r.Target, out int frame) && frame == Time.frameCount);
                    _pending.Add(new PendingHit { Slot = r.Slot, Target = r.Target, Damage = r.Damage, MarksSpent = spent, OnDazed = onDazed, Crit = r.Crit });
                    if (r.Countered)
                    {
                        ShowWord(r.Target, FeedbackRules.Word(FeedbackMoment.Counter));
                        PlayTone(FeedbackTone.Counter);
                    }
                    break;
                case PlayerReportKind.RecastPressed:
                    if (r.Clean) _cleanPending.Add(r.Slot);
                    else ShowMiss(FeedbackRules.RecastMiss(skill, r.Shot, r.Clean, r.Late)); // LATE after the clean part (work order 040)
                    break;
                case PlayerReportKind.SkillFired:
                    Fired(r, skill);
                    break;
                case PlayerReportKind.ShotFired:
                    Flush(false, -1); // the basic shot's own hits
                    break;
                case PlayerReportKind.SkillEnded:
                    // A clean press whose shot never went off (a dash, a teleport) shows nothing.
                    if (r.Ending != SkillEnding.RanOut) _cleanPending.Remove(r.Slot);
                    break;
                // Work order 035: two things that happen to her, a word over her each.
                case PlayerReportKind.Stunned: ShowWord(null, FeedbackRules.Word(FeedbackMoment.Stunned)); break;
                case PlayerReportKind.Pulled: ShowWord(null, FeedbackRules.Word(FeedbackMoment.Pulled)); break;
                // Work order 043: a quick duck stopped something.
                case PlayerReportKind.DuckStopped: ShowWord(null, FeedbackRules.Word(FeedbackMoment.Ducked)); break;
            }
        }

        /// <summary>One hit of a skill went off: its hits were reported just before it.</summary>
        void Fired(PlayerReport r, SkillDefinition skill)
        {
            bool perfect = r.Clean && skill != null && skill.Input == SkillInput.Charge;
            bool boom = r.Clean && skill != null && skill.Fuse > 0f;
            Damageable first = NearestPending(r.Slot);
            if (boom) ShowKegWord(new Vector3(r.At.X, 0f, r.At.Z), FeedbackRules.Word(FeedbackMoment.Boom));
            if (perfect)
            {
                ShowWord(first, FeedbackRules.Word(FeedbackMoment.Perfect));
                PlayTone(FeedbackTone.Perfect);
            }
            else if (skill != null && skill.Input == SkillInput.Charge)
                ShowMiss(FeedbackRules.ChargeMiss(skill, r.Held, false, Values.MissFromCharge));
            bool clean = _cleanPending.Remove(r.Slot);
            if (clean)
            {
                ShowWord(first, FeedbackRules.Word(FeedbackMoment.Clean));
                PlayTone(FeedbackTone.Clean);
            }
            // Work order 064: a clean press's hit is the strongest number, as a perfect release's (with the kinetic numbers on).
            Flush(perfect || (clean && NumberLook.On), r.Slot);
        }

        /// <summary>Of the hits waiting to be counted, the target nearest her: "the first target it hit". Null for none.</summary>
        Damageable NearestPending(int slot)
        {
            Damageable best = null;
            float bestDistance = float.PositiveInfinity;
            Vector3 her = _player != null ? _player.Position : Vector3.zero;
            foreach (PendingHit hit in _pending)
            {
                if (hit.Slot != slot || hit.Target == null) continue;
                float d = Vector3.SqrMagnitude(hit.Target.transform.position - her);
                if (d < bestDistance) { bestDistance = d; best = hit.Target; }
            }
            return best;
        }

        /// <summary>
        /// Counts the waiting hits into the damage numbers: at the shot that made them, or at the end of the frame. A shot
        /// takes only its own hits, by its slot (-1 the basic shot; work order 026, the second review of 2026-11-20): a keg
        /// a Long Shot sets off reports its burst in the middle of the Long Shot's hit, and must not take that hit with it.
        /// Null takes every one, at the end of the frame.
        /// </summary>
        void Flush(bool perfect, int? slot = null)
        {
            if (_pending.Count == 0) return;
            bool shown = NumbersOn;
            int cap = _player != null ? _player.Kit.MaxMarks : 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingHit hit = _pending[i];
                if (slot.HasValue && hit.Slot != slot.Value) continue;
                if (!shown || hit.Target == null) continue;
                // Work order 064: a crit its own kind with the kinetic numbers on (work order 050 showed it as a perfect hit's).
                NumberKind kind = NumberLook.On ? FeedbackRules.HitKind(perfect, hit.Crit, hit.MarksSpent, cap, hit.OnDazed)
                                                : FeedbackRules.HitKind(perfect || hit.Crit, hit.MarksSpent, cap, hit.OnDazed);
                int key = KeyOf(hit.Target);
                _tally.Add(key, hit.Damage, kind, Time.time, MergeWindow, Values.MaxNumbers, NumberLook.On ? NumberLook.Together : 0f);
            }
            if (!slot.HasValue) _pending.Clear();
            else for (int i = _pending.Count - 1; i >= 0; i--) if (_pending[i].Slot == slot.Value) _pending.RemoveAt(i);
            _marksSpent.Clear();
        }

        // ---------------- Words and tones ----------------

        void ShowMiss(TimingMiss miss)
        {
            FeedbackMoment? word = FeedbackRules.Word(miss);
            if (word.HasValue) ShowWord(null, FeedbackRules.Word(word.Value));
        }

        /// <summary>A word over a target, or over her for null.</summary>
        void ShowWord(Damageable target, FeedbackWord word)
        {
            int key = target != null ? KeyOf(target) : HerKey;
            if (target == null) AnchorHer();
            Put(key, word);
        }

        void ShowKegWord(Vector3 at, FeedbackWord word)
        {
            int key = _nextKeg--;
            _anchors[key] = new Anchor { Last = at, Height = KegHeight };
            Put(key, word);
        }

        void Put(int key, FeedbackWord word)
        {
            int id = _board.Add(key, word, Time.time, Time.frameCount, Values.MaxWords);
            if (id == 0 || WordShown == null) return;
            WordShown(Shown(new BoardWord { Id = id, Target = key, Word = word, Born = Time.time }, Time.time));
        }

        void PlayTone(FeedbackTone tone)
        {
            if (!Values.Tones) return;
            TonePlayed?.Invoke(tone);
            if (_sounds == null) return;
            // A share of the basic shot's volume, as the shot itself is played (PlayerCharacter.PlaySound).
            float basic = 1f;
            if (_player != null && _player.ShotLooks != null && _player.ShotLooks.Basic != null)
                basic = _player.ShotLooks.Basic.SoundVolume * _player.ShotLooks.SoundVolume;
            _sounds.Play(_tones[(int)tone], Values.ToneVolume * basic, 1f, 0f);
        }

        // ---------------- Where things are ----------------

        int KeyOf(Damageable target)
        {
            if (!_keys.TryGetValue(target, out int key))
            {
                key = _nextKey++;
                _keys[target] = key;
            }
            if (!_anchors.ContainsKey(key))
                _anchors[key] = new Anchor { Target = target, Last = target.transform.position, Height = TopOf(target.gameObject, target) + OverHead };
            return key;
        }

        void AnchorHer()
        {
            if (_anchors.ContainsKey(HerKey) || _player == null) return;
            _anchors[HerKey] = new Anchor { Her = true, Last = _player.Position, Height = TopOf(_player.gameObject, null) + OverHead };
        }

        /// <summary>
        /// The height of the top of what is drawn for it, over its ground point, as the mark pips find it: its renderers
        /// but not its pips, its stagger bar or its telegraph, and an enemy's model by its head.
        /// </summary>
        static float TopOf(GameObject go, Damageable target)
        {
            if (target != null && target.TryGetComponent(out Enemy enemy) && enemy.ModelHeight > 0f) return enemy.ModelHeight;
            float top = float.NegativeInfinity;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.GetComponent<MarkPips>() != null || r.GetComponent<StaggerBarView>() != null) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            if (float.IsNegativeInfinity(top)) return 2f;
            return Mathf.Clamp(top - go.transform.position.y, 0.5f, 6f);
        }

        Vector3 WorldOf(int key)
        {
            if (!_anchors.TryGetValue(key, out Anchor a)) return Vector3.zero;
            Vector3 at = a.Last;
            if (a.Her && _player != null) at = _player.Position;
            else if (a.Target != null) at = a.Target.transform.position;
            a.Last = at;
            _anchors[key] = a;
            return new Vector3(at.x, at.y + a.Height, at.z);
        }

        // ---------------- Every frame ----------------

        void LateUpdate() => Tick();

        /// <summary>The frame's work: what shows now (work order 045: public, so tests can count what it allocates).</summary>
        public void Tick()
        {
            Find();
            Flush(false); // hits no shot claimed
            float now = Time.time;
            _board.Expire(now, Values.WordSeconds);
            _tally.Expire(now, NumberLook, Values.NumberSeconds); // work order 064: each by its kind's life
            if (!NumbersOn) _tally.Clear();

            // Indexed, not enumerated through the interface (work order 045: nothing allocated in a frame).
            IReadOnlyList<BoardWord> words = _board.Words;
            IReadOnlyList<TallyNumber> numbers = _tally.Numbers;
            _shownWords.Clear();
            for (int i = 0; i < words.Count; i++) _shownWords.Add(Shown(words[i], now));
            _shownNumbers.Clear();
            for (int i = 0; i < numbers.Count; i++) _shownNumbers.Add(Shown(numbers[i], now));

            // Forget the places nothing shows over any more.
            _scratch.Clear();
            foreach (int key in _anchors.Keys)
            {
                bool used = false;
                for (int i = 0; i < words.Count; i++) used |= words[i].Target == key;
                for (int i = 0; i < numbers.Count; i++) used |= numbers[i].Target == key;
                if (!used) _scratch.Add(key);
            }
            foreach (int key in _scratch) _anchors.Remove(key);
            // Targets gone with their level or scene: looked for only when one was destroyed since the last look (work
            // order 026), not every frame once there are many.
            if (Damageable.Destroyed != _forgotAt && (_keys.Count > 64 || _emptiedAt.Count > 64 || _listening.Count > 64))
            {
                _forgotAt = Damageable.Destroyed;
                if (_keys.Count > 64) ForgetGone(_keys);
                if (_emptiedAt.Count > 64) ForgetGone(_emptiedAt);
                if (_listening.Count > 64) ForgetGone(_listening);
            }
        }

        int _forgotAt = -1;
        static readonly List<Damageable> s_Gone = new List<Damageable>();

        static void ForgetGone<T>(Dictionary<Damageable, T> map)
        {
            s_Gone.Clear();
            foreach (Damageable d in map.Keys) if (d == null) s_Gone.Add(d);
            foreach (Damageable d in s_Gone) map.Remove(d);
            s_Gone.Clear();
        }

        FeedbackShown Shown(BoardWord w, float now)
        {
            _anchors.TryGetValue(w.Target, out Anchor a);
            float age = now - w.Born;
            return new FeedbackShown
            {
                Id = w.Id, Text = w.Word.Text, Color = ColorOf(w.Word.Tint), FontSize = SizeOf(w.Word.Size),
                World = WorldOf(w.Target), Target = a.Target, OverHer = a.Her, Age = age,
                Alpha = FeedbackRules.Alpha(age, Values.WordSeconds, Values.FadeFrom),
                IsWord = true, Moment = w.Word.Moment, Tint = w.Word.Tint, Size = w.Word.Size,
            };
        }

        FeedbackShown Shown(TallyNumber n, float now)
        {
            _anchors.TryGetValue(n.Target, out Anchor a);
            if (n.Target == HerKey || n.Target == HerHealKey) a.Her = true;
            // The texts made once (work order 045), the same words as before.
            string text = n.Kind == NumberKind.Heal ? InterfaceStyle.Plus(n.Sum) : InterfaceStyle.Number(n.Sum); // "+8": work order 030
            float life = HitNumberRules.Life(n.Kind, NumberLook, Values.NumberSeconds); // work order 064
            float fadeFrom = NumberLook.On && HitNumberRules.LookOf(n.Kind) != HitNumberLook.AsBefore ? NumberLook.FadeFrom : Values.FadeFrom;
            return new FeedbackShown
            {
                Id = n.Id, Text = text, Color = ColorOf(FeedbackRules.NumberTint(n.Kind)),
                FontSize = n.Kind == NumberKind.Hurt ? Values.HurtNumberSize : FeedbackRules.IsBig(n.Kind) ? Values.BigNumberSize : Values.NumberSize,
                World = WorldOf(n.Target), Target = a.Target, OverHer = a.Her, Age = now - n.First,
                Alpha = FeedbackRules.Alpha(now - n.Last, life, fadeFrom),
                Sum = n.Sum, Hits = n.Hits, Kind = n.Kind, Lane = n.Lane, Life = life,
            };
        }

        int SizeOf(FeedbackSize size) =>
            size == FeedbackSize.Large ? Values.BigWordSize : size == FeedbackSize.Small ? Values.SmallWordSize : Values.WordSize;

        static Color ColorOf(FeedbackTint tint)
        {
            switch (tint)
            {
                case FeedbackTint.Clean: return s_Clean;
                case FeedbackTint.Counter: return s_Counter;
                case FeedbackTint.Marks: return s_Marks;
                case FeedbackTint.MarksFull: return s_MarksFull;
                case FeedbackTint.Boom: return s_Boom;
                case FeedbackTint.Dazed: return s_Dazed;
                case FeedbackTint.Miss: return s_Miss;
                case FeedbackTint.Hurt: return s_Hurt;
                default: return s_Plain;
            }
        }

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_shownWords.Count == 0 && _shownNumbers.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (InterfaceStyle.Kinetic)
            {
                DrawKinetic(cam); // work order 045 (TimingFeedbackKinetic.cs)
                return;
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
                _style.normal.textColor = Color.white;
            }
            GUI.depth = 1; // under the skill bar, the HUD and its screens
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Color before = GUI.color;

            foreach (FeedbackShown w in _shownWords)
                Draw(cam, scale, w, 0f, Values.WordRise * Mathf.Clamp01(w.Age / Values.WordSeconds));
            foreach (FeedbackShown n in _shownNumbers)
            {
                float side = n.Kind == NumberKind.Heal ? -HerNumberSide // work order 030: her heal to the right of her red numbers
                           : n.OverHer ? HerNumberSide : NumberSide + (n.Id % 3) * 10f; // the next number a little aside
                Draw(cam, scale, n, side, Values.NumberRise * Mathf.Clamp01(n.Age / Values.NumberSeconds));
            }

            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void Draw(Camera cam, float scale, FeedbackShown s, float side, float rise)
        {
            if (s.Alpha <= 0.001f) return;
            Vector3 p = cam.WorldToScreenPoint(s.World);
            if (p.z <= 0f) return;
            float x = p.x / scale + side, y = (Screen.height - p.y) / scale - rise;
            _style.fontSize = Mathf.Max(8, Mathf.RoundToInt(s.FontSize * FeedbackRules.PopScale(s.Age, Values.Pop, Values.PopSeconds)));
            var content = new GUIContent(s.Text);
            Vector2 size = _style.CalcSize(content);
            var r = new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y);
            // A dark outline, so it reads over a bright floor and in a crowd.
            GUI.color = new Color(s_Shadow.r, s_Shadow.g, s_Shadow.b, s_Shadow.a * s.Alpha);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * Outline, r.y + dy * Outline, r.width, r.height), content, _style);
            GUI.color = new Color(s.Color.r, s.Color.g, s.Color.b, s.Alpha);
            GUI.Label(r, content, _style);
        }
    }
}
