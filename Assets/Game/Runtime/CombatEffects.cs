using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Effects for everything that had none (work order 024, 2026-11-20): the enemies' strikes, windups, deaths, dazes
    /// and arrivals, the boss's charge, anger, calls and death, her counters, perfect releases, marks, kegs, dash and
    /// hurts, and the run's chests and choices. In the style of the shot effects Kristian approved on 2026-11-18 ("love
    /// the effects, exactly the style I had in mind"), built of the same pieces (<see cref="CombatFx"/>). Pillar 4,
    /// readable combat: every one is short, flat and sized to what it tells, and nothing lingers.
    ///
    /// It only shows what already happens: it finds each moment as the placeholder sounds do (work order 021), from what
    /// the game reports (<see cref="Enemy.Reported"/>, <see cref="Enemy.Appeared"/>, <see cref="PlayerCharacter.Reported"/>,
    /// <see cref="PlayerHealth.Damaged"/>) and what it shows (the boss's charge, a chest opened, a pick in the run's
    /// record), so a sound and its effect come together; and it changes nothing in a fight. Two hooks only: it draws an
    /// enemy's strike in place of the flat stand-in (<see cref="Enemy.StrikeDrawer"/>, and for a strike away from the enemy,
    /// a caster's circle bursting, <see cref="Enemy.StrikeAtDrawer"/>: work order 034), and while it is switched off the
    /// four pool skills borrow their nearest's look again (<see cref="PoolLooksBorrowed"/>).
    ///
    /// The limits (<see cref="FxBudget"/>): at most so many pieces live, the least going first; a kind that comes many
    /// times a frame in a crowd capped per frame; a counter, the boss's anger and death, and five marks spent never dropped.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="TimingFeedback"/> (never in a test run); tests
    /// make one with <see cref="Ensure"/>. <see cref="CombatFxValues.On"/> off gives back the game as it looked before.
    /// </summary>
    public sealed class CombatEffects : MonoBehaviour
    {
        const float GroundHeight = 0.06f; // m, as the shot effects' rings

        /// <summary>The one running now, or null.</summary>
        public static CombatEffects Current { get; private set; }

        /// <summary>Every piece drawn, as it is made. Tests listen here.</summary>
        public static event Action<CombatFx> Spawned;

        /// <summary>
        /// True while the one running is switched off: the four pool skills borrow their nearest's look again, as before
        /// work order 024. With none running they have their own.
        /// </summary>
        public static bool PoolLooksBorrowed => Current != null && !Current.Values.On;

        /// <summary>The numbers and colours, defaults in code (work order 024: first guesses).</summary>
        public CombatFxValues Values = new CombatFxValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The pieces it has live now.</summary>
        public int Live => _budget.Live;
        /// <summary>For tests (work order 033): the enemies it listens to, and how many times it looked for gone ones.</summary>
        public int Listening => _enemies.Count;
        public int GoneLooks { get; private set; }

        struct Due
        {
            public float At;
            public Action Act;
            public bool Unscaled; // At is real time (work order 033)
        }

        struct Charge
        {
            public EnemyBoss Boss;
            public Vector3 From, Dir;
            public int Dust;
        }

        struct LyingKeg
        {
            public Vector3 Centre;
            public CombatFx Ring;
        }

        struct PendingHit
        {
            public int Slot;
            public Damageable Target;
        }

        readonly FxBudget _budget = new FxBudget(null);
        readonly Dictionary<int, CombatFx> _live = new Dictionary<int, CombatFx>();
        readonly List<int> _dropped = new List<int>();
        readonly Dictionary<Enemy, Action<EnemyReport>> _enemies = new Dictionary<Enemy, Action<EnemyReport>>();
        readonly Dictionary<Enemy, CombatFx> _orbits = new Dictionary<Enemy, CombatFx>();
        readonly Dictionary<Enemy, float> _tops = new Dictionary<Enemy, float>();
        readonly List<Enemy> _appeared = new List<Enemy>();
        readonly List<Due> _due = new List<Due>();
        readonly List<Due> _dueNow = new List<Due>();
        readonly List<Charge> _charges = new List<Charge>();
        readonly List<LyingKeg> _kegs = new List<LyingKeg>();
        readonly List<PendingHit> _hits = new List<PendingHit>();
        readonly HashSet<int> _chestsSeen = new HashSet<int>();
        Func<Enemy, EnemyAttackDefinition, float, bool> _drawer;
        Func<Enemy, EnemyAttackDefinition, Vector3, bool> _drawerAt; // work order 034
        int _nextId = 1;

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerCharacter _listeningPlayer;
        PlayerHealth _listeningHealth;
        bool _warned;

        int _hurt;           // damage she took this frame that no strike has claimed
        int _hurtFrame = -1;
        int _slideSlot = -1;
        Vector3 _slideFrom;
        GeneratedLevel _levelSeen;
        GeneratedLevel _dueLevel; // the floor what waits in _due was asked on (work order 033)
        bool _dueInRun;
        RunRecord _recordSeen;
        int _picksSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            Spawned = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static CombatEffects Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("CombatEffects");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<CombatEffects>();
            return Current;
        }

        void Awake()
        {
            Enemy.Appeared += OnAppeared;
            _drawer = DrawStrike;
            Enemy.StrikeDrawer = _drawer;
            _drawerAt = DrawStrikeAt; // work order 034: the caster's circles, where they burst
            Enemy.StrikeAtDrawer = _drawerAt;
            CombatFx.Ended += OnEnded;
            Safely(Find);
        }

        void OnDestroy()
        {
            Enemy.Appeared -= OnAppeared;
            if (_drawer != null && Enemy.StrikeDrawer == _drawer) Enemy.StrikeDrawer = null;
            if (_drawerAt != null && Enemy.StrikeAtDrawer == _drawerAt) Enemy.StrikeAtDrawer = null;
            CombatFx.Ended -= OnEnded;
            Listen(null, null);
            foreach (KeyValuePair<Enemy, Action<EnemyReport>> pair in _enemies)
                if (pair.Key != null) pair.Key.Reported -= pair.Value;
            _enemies.Clear();
            if (Current == this) Current = null;
        }

        // ---------------- Which report is which moment ----------------

        /// <summary>The moment an enemy's report is, or none (work order 024; as <see cref="PlaceholderSounds.MomentOf(EnemyReportKind)"/>).</summary>
        public static FxMoment? MomentOf(EnemyReportKind kind, EnemyKind enemy = EnemyKind.Brute)
        {
            switch (kind)
            {
                case EnemyReportKind.AttackStarted: return FxMoment.WindupStarted;
                case EnemyReportKind.Struck: return FxMoment.StrikeHitHer; // when it landed; the strike's own shape is drawn as it goes off
                case EnemyReportKind.Died: return enemy == EnemyKind.Boss ? FxMoment.BossDied : FxMoment.EnemyDied;
                case EnemyReportKind.Dazed: return FxMoment.EnemyDazed;
                case EnemyReportKind.Charged: return FxMoment.BossChargeStep;
                case EnemyReportKind.CalledPack: return FxMoment.BossCalled;
                case EnemyReportKind.Angered: return FxMoment.BossAngered;
                default: return null;
            }
        }

        /// <summary>The moment one of her reports is, or none, for the skill it names (null for the basic shot).</summary>
        public static FxMoment? MomentOf(PlayerReport r, SkillDefinition skill)
        {
            switch (r.Kind)
            {
                case PlayerReportKind.Hit:
                    if (r.Countered) return FxMoment.Countered;
                    if (skill != null && skill.MarkStaggerBonus > 0f && r.Damage > 0) return FxMoment.BreakerSlugHit;
                    return null;
                case PlayerReportKind.SkillFired:
                    if (skill == null) return null;
                    if (skill.Fuse > 0f) return r.Clean ? FxMoment.KegSetOffByShot : (FxMoment?)null;
                    if (skill.Input == SkillInput.Charge && r.Clean) return FxMoment.PerfectRelease;
                    if (skill.TagSeconds > 0f && r.Landed) return FxMoment.TagRoundTagged;
                    return null;
                case PlayerReportKind.SkillStarted:
                    return skill != null && skill.Input == SkillInput.KeyMove ? FxMoment.SlideStarted : (FxMoment?)null;
                case PlayerReportKind.SkillEnded:
                    return skill != null && skill.Input == SkillInput.KeyMove ? FxMoment.SlideEnded : (FxMoment?)null;
                case PlayerReportKind.MarksSpent: return r.Marks > 0 ? FxMoment.MarksSpent : (FxMoment?)null;
                case PlayerReportKind.MarkAdded: return r.Marks > 0 ? FxMoment.MarkAdded : (FxMoment?)null;
                case PlayerReportKind.DashStarted: return FxMoment.Dashed;
                default: return null;
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
            if (_listeningHealth != null) _listeningHealth.Damaged -= OnHurt;
            _listeningPlayer = player;
            _listeningHealth = health;
            if (player != null) player.Reported += OnPlayer;
            if (health != null) health.Damaged += OnHurt;
        }

        void Hear(Enemy enemy)
        {
            if (enemy == null || _enemies.ContainsKey(enemy)) return;
            StartHearing(enemy);
        }

        /// <summary>
        /// Work order 090: the handler made apart from <see cref="Hear"/>, which asks every enemy every frame. A lambda that captures
        /// a parameter has its closure made as the method begins, before any return: in Hear it was one for every enemy of the floor
        /// every frame (sixty in a floor of 2026-11-23), the most of this file's garbage a frame. Now only for one not heard yet.
        /// </summary>
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
            List<Enemy> gone = _gone;
            gone.Clear();
            foreach (Enemy e in _enemies.Keys) if (e == null) gone.Add(e);
            foreach (Enemy e in gone) _enemies.Remove(e);
            gone.Clear();
            foreach (Enemy e in _tops.Keys) if (e == null) gone.Add(e);
            foreach (Enemy e in gone) _tops.Remove(e);
            gone.Clear();
            foreach (Enemy e in _orbits.Keys) if (e == null) gone.Add(e);
            foreach (Enemy e in gone) _orbits.Remove(e);
            gone.Clear();
        }

        /// <summary>Nothing it does may break the fight it shows: a fault here is logged once and dropped.</summary>
        void Safely(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[CombatEffects] " + e);
            }
        }

        void OnAppeared(Enemy enemy) => Safely(() =>
        {
            Hear(enemy);
            if (enemy != null) _appeared.Add(enemy); // drawn appearing at the boss's call, or at the end of the frame in a fight
        });

        void OnEnded(CombatFx fx)
        {
            if (fx == null) return;
            _budget.Gone(fx.Id);
            _live.Remove(fx.Id);
        }

        // ---------------- Drawing a piece ----------------

        Material Material => _player != null ? _player.ShotFxMaterial : null;

        /// <summary>May a moment's effect be drawn now: on, and not past its cap this frame?</summary>
        bool Ask(FxMoment moment, FxTier tier)
        {
            if (!Values.On || Material == null) return false;
            _budget.Limits = Values.Limits;
            return _budget.MayAsk(moment, Time.frameCount, tier);
        }

        /// <summary>
        /// Draws one piece, under the limits: null when it is not let in. The pieces it pushes out go at once. Public for
        /// tests, which count what a thousand leave behind.
        /// </summary>
        public CombatFx Put(FxPiece p)
        {
            Material material = Material;
            if (!Values.On || material == null) return null;
            int id = _nextId++;
            _budget.Limits = Values.Limits;
            _dropped.Clear();
            if (!_budget.Admit(id, p.Tier, p.Size, _dropped)) return null;
            foreach (int d in _dropped)
            {
                if (!_live.TryGetValue(d, out CombatFx old)) continue;
                _live.Remove(d);
                if (old != null)
                {
                    old.gameObject.SetActive(false);
                    Destroy(old.gameObject);
                }
            }
            CombatFx fx = CombatFx.Make(p, id, material);
            if (fx == null)
            {
                _budget.Gone(id);
                return null;
            }
            _live[id] = fx;
            Spawned?.Invoke(fx);
            return fx;
        }

        FxPiece Piece(FxShape shape, FxMoment moment, FxTier tier, Vector3 at, float size, float seconds, Color rim, Color core,
                      bool faint = false) =>
            new FxPiece
            {
                Shape = shape, Moment = moment, Tier = tier, At = at, End = at, Size = size, Seconds = seconds, Rim = rim, Core = core,
                RimGlow = faint ? Values.FaintRimGlow : Values.RimGlow, CoreGlow = faint ? Values.FaintCoreGlow : Values.CoreGlow,
            };

        /// <summary>A band on the ground; a wide one glows less in its core, as the shot effects' blast does, so it does not bloom into a blob.</summary>
        FxPiece Band(FxShape shape, FxMoment moment, FxTier tier, Vector3 centre, float radius, float seconds, Color rim, Color core,
                     bool faint = false)
        {
            FxPiece p = Piece(shape, moment, tier, new Vector3(centre.x, GroundHeight, centre.z), radius, seconds, rim, core, faint);
            if (radius >= 2.5f && !faint) p.CoreGlow = Mathf.Lerp(1f, p.CoreGlow, 0.4f);
            return p;
        }

        FxPiece Streak(FxMoment moment, FxTier tier, Vector3 from, Vector3 to, float width, float seconds, Color rim, Color core,
                       bool faint = false)
        {
            FxPiece p = Piece(FxShape.Streak, moment, tier, from, width, seconds, rim, core, faint);
            p.End = to;
            Vector3 d = to - from;
            p.Direction = new Vector3(d.x, 0f, d.z).normalized;
            return p;
        }

        // ---------------- Where things are ----------------

        /// <summary>The height of the top of an enemy over its ground point: its model's head, or what is drawn for it.</summary>
        float TopOf(Enemy e)
        {
            if (e.ModelHeight > 0f) return e.ModelHeight;
            if (_tops.TryGetValue(e, out float top)) return top;
            float highest = float.NegativeInfinity;
            foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r.GetComponent<MarkPips>() != null || r.GetComponent<StaggerBarView>() != null) continue;
                highest = Mathf.Max(highest, r.bounds.max.y);
            }
            top = float.IsNegativeInfinity(highest) ? 2f : Mathf.Clamp(highest - e.transform.position.y, 0.5f, 6f);
            _tops[e] = top;
            return top;
        }

        static Vector3 Feet(Component c) => Flat(c.transform.position);

        Vector3 Middle(Enemy e) => Feet(e) + Vector3.up * (TopOf(e) * 0.55f);

        Vector3 Middle(Damageable d)
        {
            if (d.TryGetComponent(out Enemy e)) return Middle(e);
            return Feet(d) + Vector3.up * 1f;
        }

        Vector3 HerChest => _player.Position + Vector3.up * Values.HerChest;

        static Vector3 AimOf(Enemy e)
        {
            Vector3 dir = Flat(e.AimPoint - e.transform.position);
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Flat(e.transform.forward).normalized;
        }

        float KindScale(EnemyKind kind) => (Values.Sizes ?? new FxSizes()).KindScale(kind);
        FxSizes Sizes => Values.Sizes ?? (Values.Sizes = new FxSizes());

        SkillDefinition SkillAt(int slot)
        {
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            return skills != null && slot >= 0 && slot < skills.Count ? skills[slot] : null;
        }

        ShotLook LookOf(SkillDefinition skill)
        {
            ShotLook look = _player != null && _player.ShotLooks != null ? _player.ShotLooks.For(skill.Name) : null;
            return look ?? ShotFxDefaults.For(skill.Name);
        }

        void Later(float seconds, Action act, bool unscaled = false) =>
            _due.Add(new Due { At = (unscaled ? Time.unscaledTime : Time.time) + Mathf.Max(0f, seconds), Act = act, Unscaled = unscaled });

        // ---------------- The enemies ----------------

        void OnEnemy(Enemy enemy, EnemyReport r) => Safely(() =>
        {
            if (enemy == null) return;
            switch (r.Kind)
            {
                case EnemyReportKind.AttackStarted: Windup(enemy); break;
                case EnemyReportKind.Struck:
                    if (r.Outcome == StrikeOutcome.Landed) StrikeOnHer(enemy, r.Attack);
                    break;
                case EnemyReportKind.Died:
                    if (enemy.Kind == EnemyKind.Boss) BossDeath(enemy);
                    else Death(enemy);
                    break;
                case EnemyReportKind.Dazed: Dazed(enemy); break;
                case EnemyReportKind.Charged:
                    if (enemy is EnemyBoss boss) ChargeBegan(boss);
                    break;
                case EnemyReportKind.CalledPack:
                    if (enemy is EnemyBoss caller) Called(caller, r.Count);
                    break;
                case EnemyReportKind.Angered: Angered(enemy); break;
            }
        });

        /// <summary>
        /// 1. A strike, in place of the flat stand-in: asked by the enemy as it strikes (<see cref="Enemy.StrikeDrawer"/>).
        /// False leaves the stand-in: switched off, or nothing to draw with.
        /// </summary>
        bool DrawStrike(Enemy e, EnemyAttackDefinition a, float reach) => e != null && DrawStrikeFrom(e, a, reach, Feet(e));

        /// <summary>
        /// 1. A strike that goes off away from where the enemy stands (work order 034: a caster's circle as it bursts), drawn
        /// at <paramref name="at"/> as a strike of its shape is, red or blue as its telegraph: asked by the enemy
        /// (<see cref="Enemy.StrikeAtDrawer"/>). False leaves the enemy's own stand-in.
        /// </summary>
        bool DrawStrikeAt(Enemy e, EnemyAttackDefinition a, Vector3 at) =>
            e != null && a != null && DrawStrikeFrom(e, a, a.Length, new Vector3(at.x, Feet(e).y, at.z));

        bool DrawStrikeFrom(Enemy e, EnemyAttackDefinition a, float reach, Vector3 o)
        {
            try
            {
                if (!Values.On || a == null || e == null) return false;
                if (_player == null) Find();
                if (Material == null) return false;
                if (!Ask(FxMoment.EnemyStruck, FxTier.Plain)) return true; // capped: drawn by none, the stand-in neither
                bool blue = PoolRules.IsCounterable(a);
                Color rim = blue ? Values.CounterRim : Values.StrikeRim, core = blue ? Values.CounterCore : Values.StrikeCore;
                Vector3 dir = AimOf(e);
                float k = KindScale(e.Kind);
                switch (a.Shape)
                {
                    case EnemyAttackShape.Circle:
                        Put(Band(FxShape.Ring, FxMoment.EnemyStruck, FxTier.Plain, o, a.Radius, Values.StrikeRingSeconds, rim, core));
                        FxPiece star = Piece(FxShape.Spark, FxMoment.EnemyStruck, FxTier.Plain, o + Vector3.up * Values.StrikeStarHeight,
                                             Values.StrikeStar * k, Values.StrikeStarSeconds, rim, core);
                        star.TowardCamera = 0.5f;
                        Put(star);
                        break;
                    case EnemyAttackShape.Cone:
                    {
                        FxPiece wedge = Band(FxShape.Wedge, FxMoment.EnemyStruck, FxTier.Plain, o, reach, Values.StrikeWedgeSeconds, rim, core);
                        wedge.Direction = dir;
                        wedge.Angle = a.Angle;
                        wedge.CoreGlow = Mathf.Lerp(1f, Values.CoreGlow, 0.4f);
                        Put(wedge);
                        break;
                    }
                    default:
                    {
                        Vector3 lift = Vector3.up * Values.StrikeLineHeight;
                        Put(Streak(FxMoment.EnemyStruck, FxTier.Plain, o + lift, o + dir * reach + lift,
                                   a.HalfWidth * Values.StrikeLineWidth, Values.StrikeLineSeconds, rim, core));
                        FxPiece sides = Piece(FxShape.SideSpikes, FxMoment.EnemyStruck, FxTier.Plain, new Vector3(o.x, GroundHeight, o.z),
                                              reach, Values.StrikeLineSeconds, rim, core);
                        sides.Direction = dir;
                        sides.Angle = a.HalfWidth;
                        Put(sides);
                        break;
                    }
                }
                if (e is EnemyShooter shooter) Arrow(shooter, dir, reach);
                return true;
            }
            catch (Exception ex)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[CombatEffects] " + ex);
                }
                return false;
            }
        }

        /// <summary>6. The shooter's arrow: a thin pale streak down its line at the height it sees from, as it is let go.</summary>
        void Arrow(EnemyShooter s, Vector3 dir, float reach)
        {
            if (!Ask(FxMoment.ArrowLoosed, FxTier.Plain)) return;
            Vector3 from = Feet(s) + Vector3.up * s.Tuning.SightHeight;
            Put(Streak(FxMoment.ArrowLoosed, FxTier.Plain, from, from + dir * reach, Values.ArrowWidth, Values.ArrowSeconds,
                       Values.ArrowRim, Values.ArrowCore));
        }

        /// <summary>1. A strike that hurt her: a spark on her in its colours, sized by the hit. It is her hurt spark for that hit.</summary>
        void StrikeOnHer(Enemy e, string attack)
        {
            if (_hurtFrame != Time.frameCount || _hurt <= 0 || _player == null) return;
            int amount = _hurt;
            _hurt = 0;
            if (!Ask(FxMoment.StrikeHitHer, FxTier.Plain)) return;
            // A caster's circle bursts on its own time, after the cast or the caster itself is over: its colour is that of
            // the cast it belongs to, by the report's name (work order 034).
            EnemyAttackDefinition a = e is EnemyCaster caster ? caster.Tuning.Find(attack) ?? e.Attack : e.Attack; // its orbs too (work order 036)
            bool blue = PoolRules.IsCounterable(a);
            FxPiece p = Piece(FxShape.Spark, FxMoment.StrikeHitHer, FxTier.Plain, HerChest,
                              Sizes.HurtSize(amount, _health != null ? _health.Max : Placeholders.PlayerHealth), Values.HurtSeconds,
                              blue ? Values.CounterRim : Values.StrikeRim, blue ? Values.CounterCore : Values.StrikeCore);
            p.TowardCamera = 0.6f;
            Put(p);
        }

        /// <summary>
        /// 2. A windup begins: a small, quick star of spikes converging on the enemy, in the telegraph's colour; for one
        /// read on its body, its glint instead (21, work order 035).
        /// </summary>
        void Windup(Enemy e)
        {
            if (e.ReadOnBody)
            {
                Glint(e);
                return;
            }
            if (!Ask(FxMoment.WindupStarted, FxTier.Minor)) return;
            bool blue = PoolRules.IsCounterable(e.Attack);
            float look = e is EnemyShooter shooter ? shooter.WindupLook : 1f; // work order 036: a fan's draw shows larger
            FxPiece p = Piece(FxShape.Converge, FxMoment.WindupStarted, FxTier.Minor, Middle(e), Values.WindupSize * KindScale(e.Kind) * look,
                              Values.WindupSeconds, blue ? Values.CounterRim : Values.StrikeRim, blue ? Values.CounterCore : Values.StrikeCore);
            p.Count = Values.WindupSpikes;
            p.TowardCamera = 0.4f;
            Put(p);
        }

        /// <summary>3. A death: a burst of bone-coloured shards as its death clip starts, and a thin pale ring where it lay when the body is hidden.</summary>
        void Death(Enemy e)
        {
            if (!Ask(FxMoment.EnemyDied, FxTier.Plain)) return;
            float k = KindScale(e.Kind);
            Vector3 at = Middle(e);
            Shards(FxMoment.EnemyDied, FxTier.Plain, at, k * Values.DeathStar, k * Values.ShardReach, k * Values.ShardSize, Sizes.ShardCount(e.Kind));
            Vector3 feet = Feet(e);
            Later(e.ShownDeadFor(e.Values), () =>
            {
                if (!HitChipsRules.RingWhenABodyGoes(HitChipsValues.On, HitChipsValues.RingWhenABodyGoes)) return; // work order 127: "du kan droppe den sirkelen når døde enemies despawner"
                if (!Ask(FxMoment.BodyHidden, FxTier.Plain)) return;
                Put(Band(FxShape.Outline, FxMoment.BodyHidden, FxTier.Plain, e != null ? Feet(e) : feet, Values.HiddenRing * k,
                         Values.HiddenRingSeconds, Values.BoneRim, Values.BoneCore));
            });
        }

        void Shards(FxMoment moment, FxTier tier, Vector3 at, float star, float reach, float piece, int count, bool unscaled = false)
        {
            FxPiece s = Piece(FxShape.Spark, moment, tier, at, star, Values.DeathStarSeconds, Values.BoneRim, Values.BoneCore);
            s.TowardCamera = 0.5f;
            s.Unscaled = unscaled;
            Put(s);
            FxPiece p = Piece(FxShape.Shards, moment, tier, at, reach, Values.ShardSeconds, Values.BoneRim, Values.BoneCore);
            p.Count = count;
            p.PieceSize = piece;
            p.TowardCamera = 0.5f;
            p.Unscaled = unscaled;
            Put(p);
        }

        /// <summary>4. Appearing in a fight: a ring closing in on the spot and a short spike up, in the boss's colour.</summary>
        void Appear(Enemy e)
        {
            if (e == null || !Ask(FxMoment.EnemyAppeared, FxTier.Plain)) return;
            float k = KindScale(e.Kind);
            Put(Band(FxShape.Closing, FxMoment.EnemyAppeared, FxTier.Plain, Feet(e), Values.AppearRing * k, Values.AppearSeconds,
                     Values.BossRim, Values.BossCore));
            float spike = Values.AppearSpike * k;
            Put(Piece(FxShape.Spike, FxMoment.EnemyAppeared, FxTier.Plain, Feet(e) + Vector3.up * (spike * 0.3f), spike,
                      Values.AppearSpikeSeconds, Values.BossRim, Values.BossCore));
        }

        /// <summary>5. Dazed: small light blue stars turning slowly over its head for as long as the daze lasts.</summary>
        void Dazed(Enemy e)
        {
            if (_orbits.TryGetValue(e, out CombatFx shown) && shown != null) return;
            if (!Ask(FxMoment.EnemyDazed, FxTier.Plain)) return;
            float k = KindScale(e.Kind);
            Enemy dazed = e;
            FxPiece p = Piece(FxShape.Orbit, FxMoment.EnemyDazed, FxTier.Plain, Feet(e), Values.DazedRadius * k, Values.DazedFade,
                              Values.DazedRim, Values.DazedCore);
            p.Follow = e.transform;
            p.FollowOffset = Vector3.up * (TopOf(e) + Values.DazedOverHead);
            p.At = Feet(e) + p.FollowOffset;
            p.Holds = () => dazed != null && dazed.IsDazed && !dazed.IsDead;
            p.HoldMost = Values.DazedMost;
            p.Spin = Values.DazedSpin;
            _orbits[e] = Put(p);
        }

        // ---------------- The boss ----------------

        /// <summary>7. The charge runs: a faint streak trailing it; the dust follows in <see cref="LateUpdate"/>.</summary>
        void ChargeBegan(EnemyBoss boss)
        {
            _charges.Add(new Charge { Boss = boss, From = Flat(boss.ChargeFrom), Dir = Flat(boss.ChargeDirection).normalized });
            if (!Ask(FxMoment.BossChargeStep, FxTier.Minor)) return;
            Vector3 lift = Vector3.up * Values.TrailHeight;
            FxPiece trail = Streak(FxMoment.BossChargeStep, FxTier.Plain, Flat(boss.ChargeFrom) + lift, Flat(boss.ChargeFrom) + lift,
                                   Values.TrailWidth, Values.TrailFade, Values.BossRim, Values.BossCore, faint: true);
            trail.Direction = Flat(boss.ChargeDirection).normalized;
            trail.Follow = boss.transform;
            trail.FollowOffset = lift;
            trail.TrailLength = Values.TrailLength;
            trail.Holds = () => boss != null && boss.IsCharging;
            trail.HoldMost = 10f;
            Put(trail);
        }

        void RunCharges()
        {
            for (int i = _charges.Count - 1; i >= 0; i--)
            {
                Charge c = _charges[i];
                if (c.Boss == null)
                {
                    _charges.RemoveAt(i);
                    continue;
                }
                float run = Vector3.Dot(Feet(c.Boss) - c.From, c.Dir);
                int due = CombatFxRules.DustDue(run, c.Dust);
                for (int n = 0; n < due; n++)
                {
                    c.Dust++;
                    if (!Ask(FxMoment.BossChargeStep, FxTier.Minor)) continue;
                    FxPiece dust = Band(FxShape.Wedge, FxMoment.BossChargeStep, FxTier.Minor, c.From + c.Dir * (c.Dust * CombatFxRules.ChargeDustEvery),
                                        Values.DustRadius, Values.DustSeconds, Values.DustRim, Values.DustCore, faint: true);
                    dust.Direction = -c.Dir;
                    dust.Angle = Values.DustAngle;
                    Put(dust);
                }
                _charges[i] = c;
                if (c.Boss.IsCharging) continue;
                _charges.RemoveAt(i);
                if (!c.Boss.IsDead && Ask(FxMoment.BossChargeEnded, FxTier.Plain))
                    Put(Band(FxShape.Ring, FxMoment.BossChargeEnded, FxTier.Plain, Feet(c.Boss), Values.ChargeEndRing, Values.ChargeEndSeconds,
                             Values.BossRim, Values.BossCore));
            }
        }

        /// <summary>8. Anger, once: a large ring bursting out from it and a tall spiky star, in its warm red-orange. Never dropped.</summary>
        void Angered(Enemy e)
        {
            if (!Ask(FxMoment.BossAngered, FxTier.Key)) return;
            Put(Band(FxShape.Ring, FxMoment.BossAngered, FxTier.Key, Feet(e), Values.AngerRing, Values.AngerSeconds, Values.AngryRim, Values.AngryCore));
            Put(Piece(FxShape.Spike, FxMoment.BossAngered, FxTier.Key, Feet(e) + Vector3.up * (Values.AngerSpike * 0.3f), Values.AngerSpike,
                      Values.AngerSpikeSeconds, Values.AngryRim, Values.AngryCore));
        }

        /// <summary>9. A call: a ring bursting from it on the ground in its colour, and the pack appearing (4) where it came.</summary>
        void Called(EnemyBoss boss, int count)
        {
            if (Ask(FxMoment.BossCalled, FxTier.Plain))
                Put(Band(FxShape.Ring, FxMoment.BossCalled, FxTier.Plain, Feet(boss), Values.CallRing, Values.CallSeconds, Values.BossRim, Values.BossCore));
            IReadOnlyList<Enemy> called = boss.Called;
            for (int i = Mathf.Max(0, called.Count - count); i < called.Count; i++)
            {
                Enemy e = called[i];
                if (e == null) continue;
                _appeared.Remove(e);
                Appear(e);
            }
        }

        /// <summary>
        /// 10. The boss's death: three staggered bursts of shards over half a second, and a wide ring. Never dropped. On real
        /// time (the second reviews of 2026-11-20, work order 033): the act is cleared as it dies and the game slows to a
        /// stop over half a second of real time, only about a quarter of a second of game time; all of it is seen on the
        /// "Act cleared" screen.
        /// </summary>
        void BossDeath(Enemy e)
        {
            if (!Ask(FxMoment.BossDied, FxTier.Key)) return;
            float top = TopOf(e);
            Vector3 feet = Feet(e);
            FxPiece ring = Band(FxShape.Ring, FxMoment.BossDied, FxTier.Key, feet, Values.BossDeathRing, Values.BossDeathSeconds, Values.BossRim, Values.BossCore);
            ring.Unscaled = true;
            Put(ring);
            var offsets = new[] { new Vector3(0f, 0.55f, 0f), new Vector3(-0.7f, 0.4f, 0.35f), new Vector3(0.6f, 0.75f, -0.3f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 at = feet + new Vector3(offsets[i].x, offsets[i].y * top, offsets[i].z);
                Later(i * Values.BossBurstGap, () => Shards(FxMoment.BossDied, FxTier.Key, at, Values.BossBurstStar, Values.BossShardReach,
                                                            Values.BossShardSize, Sizes.ShardCount(EnemyKind.Boss), unscaled: true), unscaled: true);
            }
            Later(e.ShownDeadFor(e.Values), () =>
            {
                if (HitChipsRules.RingWhenABodyGoes(HitChipsValues.On, HitChipsValues.RingWhenABodyGoes) && Ask(FxMoment.BodyHidden, FxTier.Plain)) // work order 127: its body's ring too; its death's ring above stays
                    Put(Band(FxShape.Outline, FxMoment.BodyHidden, FxTier.Plain, feet, Values.HiddenRing * KindScale(EnemyKind.Boss),
                             Values.HiddenRingSeconds, Values.BoneRim, Values.BoneCore));
            });
        }

        // ---------------- Her ----------------

        void OnPlayer(PlayerReport r) => Safely(() =>
        {
            SkillDefinition skill = SkillAt(r.Slot);
            switch (r.Kind)
            {
                case PlayerReportKind.Hit:
                    if (r.Target == null) break;
                    _hits.Add(new PendingHit { Slot = r.Slot, Target = r.Target });
                    if (r.Countered) Counter(r.Target);
                    if (skill != null && skill.MarkStaggerBonus > 0f && r.Damage > 0) SlugHit(skill, r.Target);
                    break;
                case PlayerReportKind.SkillFired:
                    if (skill != null) Fired(r, skill);
                    _hits.RemoveAll(h => h.Slot == r.Slot);
                    break;
                case PlayerReportKind.ShotFired:
                    _hits.RemoveAll(h => h.Slot < 0);
                    break;
                case PlayerReportKind.SkillStarted:
                    if (skill != null && skill.Input == SkillInput.KeyMove) SlideStarted(r.Slot);
                    break;
                case PlayerReportKind.SkillEnded:
                    if (r.Slot == _slideSlot) SlideEnded();
                    break;
                case PlayerReportKind.MarksSpent:
                    if (r.Target != null && r.Marks > 0) MarksSpent(r.Target, r.Marks);
                    break;
                case PlayerReportKind.MarkAdded:
                    if (r.Target != null && r.Marks > 0) MarkAdded(r.Target, r.MarksAfter);
                    break;
                case PlayerReportKind.DashStarted:
                    Dashed();
                    break;
                case PlayerReportKind.Stunned: HerStunned(); break; // work order 035
                case PlayerReportKind.Pulled: HerPulled(); break;
            }
        });

        void OnHurt(int amount) => Safely(() =>
        {
            if (_hurtFrame != Time.frameCount) _hurt = 0;
            _hurtFrame = Time.frameCount;
            _hurt += amount;
        });

        void Fired(PlayerReport r, SkillDefinition skill)
        {
            if (skill.Fuse > 0f)
            {
                Keg(r, skill);
                return;
            }
            if (skill.Input == SkillInput.Charge && r.Clean) Perfect(skill, r.Slot);
            if (skill.TagSeconds > 0f) Tagged(r.Slot);
        }

        /// <summary>Of her hits waiting from this slot, the target nearest her that passes the test; null for none.</summary>
        Damageable NearestHit(int slot, Func<Damageable, bool> test = null)
        {
            Damageable best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (PendingHit h in _hits)
            {
                if (h.Slot != slot || h.Target == null || (test != null && !test(h.Target))) continue;
                float d = Vector3.SqrMagnitude(h.Target.transform.position - _player.Position);
                if (d < bestDistance) { bestDistance = d; best = h.Target; }
            }
            return best;
        }

        /// <summary>11. Tag Round: a small green diamond burst on the target it tagged.</summary>
        void Tagged(int slot)
        {
            Damageable target = NearestHit(slot, d => d.IsTagged);
            if (target == null || !Ask(FxMoment.TagRoundTagged, FxTier.Plain)) return;
            FxPiece p = Piece(FxShape.Diamond, FxMoment.TagRoundTagged, FxTier.Plain, Middle(target), Values.TagDiamond, Values.TagSeconds,
                              Values.TagRim, Values.TagCore);
            p.TowardCamera = target.Radius + 0.3f;
            Put(p);
        }

        /// <summary>11. Breaker Slug: a heavy ring on what it hits, in its own look's colours.</summary>
        void SlugHit(SkillDefinition skill, Damageable target)
        {
            if (!Ask(FxMoment.BreakerSlugHit, FxTier.Plain)) return;
            ShotLook look = LookOf(skill);
            if (SkillLookValues.EffectsNotShapes) { Put(Chips(FxMoment.BreakerSlugHit, FxTier.Plain, Middle(target), SkillLookValues.Live.SlugChipReach, SkillLookValues.Live.SlugChipSize, SkillLookValues.Live.SlugChips, SkillLookValues.Live.SlugChipSeconds, look.Rim, look.Core)); return; } // work order 123: chips off it, no ring on the ground
            Put(Band(FxShape.Ring, FxMoment.BreakerSlugHit, FxTier.Plain, Feet(target), Values.SlugRing, Values.SlugSeconds, look.Rim, look.Core));
        }

        /// <summary>11. Slide: a low dust wedge kicked back where she starts.</summary>
        void SlideStarted(int slot)
        {
            _slideSlot = slot;
            _slideFrom = Flat(_player.Position);
            if (!Ask(FxMoment.SlideStarted, FxTier.Plain)) return;
            Vector3 move = _player.MoveInput;
            Vector3 dir = Flat(move).sqrMagnitude > 1e-6f ? Flat(move).normalized : Flat(_player.transform.forward).normalized;
            if (SkillLookValues.EffectsNotShapes) { SkillEffects.Ensure().Watch(_player).SlideDust(_slideFrom, -dir); return; } // work order 123: puffs, no wedge
            FxPiece dust = Band(FxShape.Wedge, FxMoment.SlideStarted, FxTier.Plain, _slideFrom, Values.SlideDust, Values.SlideDustSeconds,
                                Values.DustRim, Values.DustCore, faint: true);
            dust.Direction = -dir;
            dust.Angle = Values.SlideDustAngle;
            Put(dust);
        }

        /// <summary>11. Slide ended: a faint streak along the way she slid.</summary>
        void SlideEnded()
        {
            _slideSlot = -1;
            Vector3 to = Flat(_player.Position);
            if (Vector3.Distance(to, _slideFrom) < 0.3f || !Ask(FxMoment.SlideEnded, FxTier.Plain)) return;
            Vector3 lift = Vector3.up * Values.SlideHeight;
            Put(Streak(FxMoment.SlideEnded, FxTier.Plain, _slideFrom + lift, to + lift, Values.SlideTrailWidth, Values.SlideTrailSeconds,
                       Values.HerRim, Values.HerCore, faint: true));
        }

        /// <summary>12. A counter: a blue starburst and a ring on the countered enemy, larger than a hit spark and the brightest thing on screen. Never dropped.</summary>
        void Counter(Damageable target)
        {
            if (!Ask(FxMoment.Countered, FxTier.Key)) return;
            FxPiece star = Piece(FxShape.Spark, FxMoment.Countered, FxTier.Key, Middle(target), Values.CounterStar, Values.CounterSeconds,
                                 Values.CounterRim, Values.CounterCore);
            star.CoreGlow = Values.BrightestGlow;
            star.TowardCamera = target.Radius + 0.4f;
            Put(star);
            if (SkillLookValues.EffectsNotShapes) { Put(Chips(FxMoment.Countered, FxTier.Key, Middle(target), SkillLookValues.Live.CounterChipReach, SkillLookValues.Live.CounterChipSize, SkillLookValues.Live.CounterChips, SkillLookValues.Live.CounterChipSeconds, Values.CounterRim, Values.CounterCore)); return; } // work order 123
            Put(Band(FxShape.Ring, FxMoment.Countered, FxTier.Key, Feet(target), Values.CounterRing, Values.CounterRingSeconds,
                     Values.CounterRim, Values.CounterCore));
        }

        /// <summary>13. A perfect release: a ring at her feet under the muzzle, and a larger spark where it lands.</summary>
        void Perfect(SkillDefinition skill, int slot)
        {
            if (!Ask(FxMoment.PerfectRelease, FxTier.Plain)) return;
            if (!SkillLookValues.EffectsNotShapes) // work order 123: no ring on the ground at her feet; the larger spark where it lands stays
            Put(Band(FxShape.Ring, FxMoment.PerfectRelease, FxTier.Plain, _player.Position, Values.PerfectRing, Values.PerfectRingSeconds,
                     Values.HerRim, Values.HerCore));
            ShotLook look = LookOf(skill);
            Damageable first = NearestHit(slot);
            Vector3 at;
            if (first != null) at = Middle(first);
            else
            {
                // Nothing hit: where its line ends, at the cover that stopped it or its full range.
                Vector3 dir = Flat(_player.AimPoint - _player.Position);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Flat(_player.transform.forward).normalized;
                float reach = skill.Range;
                if (_player.CoverQuery != null) reach = Cover.ClearDistance(Flat(_player.Position), dir, reach, Cover.RayHeight);
                at = Flat(_player.Position) + dir * reach + Vector3.up * Values.HerChest;
            }
            FxPiece spark = Piece(FxShape.Spark, FxMoment.PerfectRelease, FxTier.Plain, at, look.HitSize * Values.PerfectSparkScale,
                                  Mathf.Max(0.12f, look.HitSeconds * 1.3f), Values.HerRim, Values.HerCore);
            spark.TowardCamera = first != null ? first.Radius + 0.4f : 0.4f;
            Put(spark);
        }

        /// <summary>14. Marks spent: over the target, its pips burst outward as small orange stars, more and larger with more marks. Five is never dropped.</summary>
        void MarksSpent(Damageable target, int marks)
        {
            int cap = _player != null ? _player.Kit.MaxMarks : 5;
            FxTier tier = CombatFxRules.TierOf(FxMoment.MarksSpent, marks, cap);
            if (!Ask(FxMoment.MarksSpent, tier)) return;
            bool full = marks >= cap;
            Color rim = full ? Values.MarkFullRim : Values.MarkRim, core = full ? Values.MarkFullCore : Values.MarkCore;
            int stars = Sizes.MarkStars(marks, cap);
            float size = Sizes.MarkStarSize(marks, cap);
            int slots = Mathf.Max(cap, marks);
            MarkPips pips = target.GetComponentInChildren<MarkPips>(true);
            Vector3 row = pips != null ? pips.RowCentre() : Feet(target) + Vector3.up * 2.4f;
            for (int i = 0; i < stars; i++)
            {
                int pip = i % marks;
                Vector3 at = pips != null ? pips.PipPoint(pip, slots) : row;
                Vector3 outward = at - row;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.zero;
                float speed = Values.MarkBurstSpeed * (i >= marks ? 1.7f : 1f);
                FxPiece p = Piece(FxShape.Spark, FxMoment.MarksSpent, tier, at, size, Values.MarkBurstSeconds, rim, core);
                p.Velocity = (outward + Vector3.up * (i >= marks ? 0.9f : 0.5f)).normalized * speed;
                p.TowardCamera = 0.3f;
                Put(p);
            }
            if (full)
            {
                FxPiece spike = Piece(FxShape.Spike, FxMoment.MarksSpent, tier, row, Values.MarkCapSpike, Values.MarkBurstSeconds, rim, core);
                spike.TowardCamera = 0.3f;
                Put(spike);
            }
        }

        /// <summary>15. A mark gained: a tiny orange pop at its pip.</summary>
        void MarkAdded(Damageable target, int after)
        {
            if (after <= 0 || !Ask(FxMoment.MarkAdded, FxTier.Minor)) return;
            int cap = _player != null ? _player.Kit.MaxMarks : 5;
            MarkPips pips = target.GetComponentInChildren<MarkPips>(true);
            Vector3 at = pips != null ? pips.PipPoint(after - 1, Mathf.Max(cap, after)) : Feet(target) + Vector3.up * 2.4f;
            bool full = after >= cap;
            FxPiece p = Piece(FxShape.Spark, FxMoment.MarkAdded, FxTier.Minor, at, Values.MarkPop, Values.MarkPopSeconds,
                              full ? Values.MarkFullRim : Values.MarkRim, full ? Values.MarkFullCore : Values.MarkCore);
            p.TowardCamera = 0.35f;
            Put(p);
        }

        /// <summary>16. A keg: while it lies, a thin ring closing in to its centre over the fuse; set off by a shot, a burst larger than its fuse's.</summary>
        void Keg(PlayerReport r, SkillDefinition skill)
        {
            ShotLook look = LookOf(skill);
            if (_player.KegsLying > _kegs.Count)
            {
                Vector3 centre = Flat(_player.LastKegPoint);
                CombatFx ring = null;
                if (!SkillLookValues.EffectsNotShapes && Ask(FxMoment.KegLying, FxTier.Plain)) // work order 123: the keg itself is drawn (SkillEffects), no ring
                {
                    FxPiece p = Band(FxShape.Closing, FxMoment.KegLying, FxTier.Plain, centre, skill.Radius, skill.Fuse, look.Rim, look.Core);
                    ring = Put(p);
                }
                _kegs.Add(new LyingKeg { Centre = centre, Ring = ring });
                return;
            }
            // A burst: its ring goes with it.
            var at = new Vector3(r.At.X, 0f, r.At.Z);
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < _kegs.Count; i++)
            {
                float d = Vector3.Distance(_kegs[i].Centre, at);
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest >= 0)
            {
                if (_kegs[nearest].Ring != null) Destroy(_kegs[nearest].Ring.gameObject);
                _kegs.RemoveAt(nearest);
            }
            if (!r.Clean || !Ask(FxMoment.KegSetOffByShot, FxTier.Plain)) return;
            float scale = Values.KegShotScale * Mathf.Max(1f, skill.ShotBurstScale);
            FxPiece spark = Piece(FxShape.Spark, FxMoment.KegSetOffByShot, FxTier.Plain, at + Vector3.up * 1f, skill.Radius * 1.1f * scale,
                                  Values.KegShotSeconds, look.Rim, look.Core);
            spark.TowardCamera = skill.Radius + 1f;
            Put(spark);
            if (!SkillLookValues.EffectsNotShapes) // work order 123: its blast's own dust runs out to the radius (SkillEffects.Blast)
            Put(Band(FxShape.Ring, FxMoment.KegSetOffByShot, FxTier.Plain, at, skill.Radius * Values.KegShotRing, Values.KegShotSeconds, look.Rim, look.Core));
        }

        /// <summary>17. The dash: a flash where she starts and a streak along her path, to where the dash will stop.</summary>
        void Dashed()
        {
            if (!Ask(FxMoment.Dashed, FxTier.Plain)) return;
            Vector3 from = Flat(_player.Position), dir = Flat(_player.transform.forward).normalized;
            Vector3 to = from + dir * _player.Feel.DashDistance;
            if (NavMesh.Raycast(_player.Position, _player.Position + dir * _player.Feel.DashDistance, out NavMeshHit edge, NavMesh.AllAreas))
                to = Flat(edge.position);
            Vector3 lift = Vector3.up * Values.DashHeight;
            FxPiece flash = Piece(FxShape.Flash, FxMoment.Dashed, FxTier.Plain, from + lift, Values.DashFlash, Values.DashFlashSeconds,
                                  Values.HerRim, Values.HerCore);
            flash.Direction = -dir; // it bursts back from where she leaves
            Put(flash);
            if (Vector3.Distance(from, to) > 0.2f)
                Put(Streak(FxMoment.Dashed, FxTier.Plain, from + lift, to + lift, Values.DashWidth, Values.DashSeconds, Values.HerRim, Values.HerCore));
        }

        /// <summary>18. She is hurt (by anything but a strike, which draws its own spark on her): a red spark on her, sized by the hit.</summary>
        void Hurt()
        {
            if (_hurt <= 0 || _hurtFrame != Time.frameCount || _player == null) return;
            int amount = _hurt;
            _hurt = 0;
            if (!Ask(FxMoment.HerHurt, FxTier.Plain)) return;
            FxPiece p = Piece(FxShape.Spark, FxMoment.HerHurt, FxTier.Plain, HerChest,
                              Sizes.HurtSize(amount, _health != null ? _health.Max : Placeholders.PlayerHealth), Values.HurtSeconds,
                              Values.HurtRim, Values.HurtCore);
            p.TowardCamera = 0.6f;
            Put(p);
        }

        // ---------------- Melee you read (work order 035, 2026-11-20; decision 0010) ----------------

        /// <summary>
        /// 21. The glint: what a melee enemy read on its body is about to do, at its weapon or front at about chest height,
        /// on it as it moves: red for an attack that only hurts, blue for one Stopping Shot can counter
        /// (<see cref="MeleeYouReadRules.GlintIsBlue"/>). A skill's grows through its windup and snaps at the strike; an
        /// ordinary attack's is one small flick (<see cref="MeleeYouReadRules.GlintGrows"/>). Nothing flat on the floor.
        /// </summary>
        void Glint(Enemy e)
        {
            EnemyAttackDefinition a = e.Attack;
            if (a == null) return;
            bool grows = MeleeYouReadRules.GlintGrows(a);
            // A rapid ordinary attack (the swarmer's bite, three a second; work order 039) flicks on every other one only:
            // eight swarmers on her flicking at every bite would strobe.
            if (!grows && e is MeleeEnemy melee && !MeleeYouReadRules.FlickShows(a, melee.OrdinaryBegun)) return;
            FxTier tier = grows ? FxTier.Plain : FxTier.Minor;
            if (!Ask(FxMoment.WindupStarted, tier)) return;
            bool blue = MeleeYouReadRules.GlintIsBlue(a);
            float k = KindScale(e.Kind) * Mathf.Max(0.5f, e.transform.lossyScale.y); // an elite is shown larger
            Vector3 front = Flat(e.transform.forward);
            front = front.sqrMagnitude > 1e-6f ? front.normalized : Vector3.forward;
            Vector3 offset = Vector3.up * (TopOf(e) * Values.GlintHeight) + front * (Values.GlintForward * k);
            FxPiece p = Piece(FxShape.Spark, FxMoment.WindupStarted, tier, Feet(e) + offset, (grows ? Values.GlintSize : Values.FlickSize) * k,
                              grows ? Values.GlintSnapSeconds : Values.FlickSeconds, blue ? Values.CounterRim : Values.StrikeRim,
                              blue ? Values.CounterCore : Values.StrikeCore);
            p.Follow = e.transform;
            p.FollowOffset = offset;
            p.TowardCamera = 0.5f;
            if (grows)
            {
                Enemy enemy = e;
                float windup = Mathf.Max(1e-3f, a.Windup);
                p.Holds = () => enemy != null && !enemy.IsDead && !enemy.IsDazed && enemy.State == EnemyState.Windup && enemy.Attack == a;
                p.HoldMost = a.Windup + 0.5f;
                p.Grow = () => enemy != null ? 1f - enemy.WindupRemaining / windup : 1f;
            }
            Put(p);
        }

        float _herTop = -1f;

        /// <summary>The height of the top of what is drawn for her, measured once.</summary>
        float HerTop
        {
            get
            {
                if (_herTop > 0f || _player == null) return _herTop > 0f ? _herTop : 1.8f;
                float highest = float.NegativeInfinity;
                foreach (Renderer r in _player.GetComponentsInChildren<Renderer>())
                    if (!(r is LineRenderer)) highest = Mathf.Max(highest, r.bounds.max.y);
                _herTop = float.IsNegativeInfinity(highest) ? 1.8f : Mathf.Clamp(highest - _player.transform.position.y, 1f, 3f);
                return _herTop;
            }
        }

        /// <summary>22. She is stunned: small blue stars turning fast over her head for as long as the stun lasts.</summary>
        void HerStunned()
        {
            if (_player == null || !Ask(FxMoment.EnemyDazed, FxTier.Plain)) return;
            PlayerCharacter her = _player;
            FxPiece p = Piece(FxShape.Orbit, FxMoment.EnemyDazed, FxTier.Plain, Feet(her), Values.StunnedRadius, Values.DazedFade,
                              Values.CounterRim, Values.CounterCore);
            p.Follow = her.transform;
            p.FollowOffset = Vector3.up * (HerTop + Values.StunnedOverHead);
            p.At = Feet(her) + p.FollowOffset;
            p.Holds = () => her != null && her.IsStunned;
            p.HoldMost = 5f;
            p.Spin = Values.StunnedSpin;
            Put(p);
        }

        /// <summary>23. She is pulled: a blue streak trailing her along the pull, for as long as it moves her.</summary>
        void HerPulled()
        {
            if (_player == null || !Ask(FxMoment.Dashed, FxTier.Plain)) return;
            PlayerCharacter her = _player;
            HeldState held = her.HeldBy;
            Vector3 from = new Vector3(held.From.X, 0f, held.From.Z), to = new Vector3(held.To.X, 0f, held.To.Z);
            Vector3 lift = Vector3.up * Values.PulledHeight;
            FxPiece trail = Streak(FxMoment.Dashed, FxTier.Plain, from + lift, from + lift, Values.PulledWidth, Values.TrailFade,
                                   Values.CounterRim, Values.CounterCore);
            Vector3 dir = Flat(to - from);
            trail.Direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : Flat(her.transform.forward).normalized;
            trail.Follow = her.transform;
            trail.FollowOffset = lift;
            trail.TrailLength = Mathf.Max(0.5f, dir.magnitude);
            trail.Holds = () => her != null && her.IsPulled;
            trail.HoldMost = 2f;
            Put(trail);
        }

        // ---------------- The run ----------------

        /// <summary>The run's moments, found by looking as the placeholder sounds find them: a chest opened, a pick taken.</summary>
        void LookAtTheRun()
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            if (level != _levelSeen)
            {
                _levelSeen = level;
                _chestsSeen.Clear();
                if (levels != null) foreach (int order in levels.ChestsOpened) _chestsSeen.Add(order);
            }
            // Work order 090: looked through only when a chest was opened since (the chests seen are the floor's opened ones, and the
            // floor's only grow): the look over the interface's collection made an enumerator every frame.
            else if (levels != null && levels.ChestsOpened.Count != _chestsSeen.Count)
            {
                foreach (int order in levels.ChestsOpened)
                {
                    if (!_chestsSeen.Add(order)) continue;
                    foreach (GeneratedLevel.PlacedProp chest in levels.Chests())
                        if (chest.Room != null && chest.Room.Order == order && chest.Object != null) ChestOpened(chest.Object.transform.position);
                }
            }
            RunRecord record = levels != null ? levels.Record : null;
            // The focus choice is a pick too (work order 025), and an elite room's skill upgrade: the one count the sounds
            // use as well (the second reviews of 2026-11-20, work order 033).
            int picks = record != null ? record.Picks : 0;
            if (record != null && record == _recordSeen && picks > _picksSeen) ChoiceTaken();
            _recordSeen = record;
            _picksSeen = picks;
        }

        /// <summary>19. A chest opens: a short fountain of cyan stars and a ring. On real time: its choice holds the game at once.</summary>
        void ChestOpened(Vector3 chest)
        {
            if (!Ask(FxMoment.ChestOpened, FxTier.Plain)) return;
            Vector3 feet = Flat(chest);
            FxPiece ring = Band(FxShape.Ring, FxMoment.ChestOpened, FxTier.Plain, feet, Values.ChestRing, Values.ChestRingSeconds, Values.WorldRim, Values.WorldCore);
            ring.Unscaled = true;
            Put(ring);
            int n = Mathf.Max(1, Values.ChestStars);
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                FxPiece star = Piece(FxShape.Spark, FxMoment.ChestOpened, FxTier.Plain, feet + Vector3.up * 0.8f, Values.ChestStar,
                                     Values.ChestStarSeconds, Values.WorldRim, Values.WorldCore);
                star.Velocity = new Vector3(Mathf.Cos(a) * Values.ChestSpread, Values.ChestRise * (0.85f + 0.3f * ((i * 7) % n) / n), Mathf.Sin(a) * Values.ChestSpread);
                star.Gravity = Values.ChestGravity;
                star.Unscaled = true;
                Put(star);
            }
        }

        /// <summary>20. A choice is taken: a cyan ring rising over her and three small stars. On real time.</summary>
        void ChoiceTaken()
        {
            if (_player == null || !Ask(FxMoment.ChoiceTaken, FxTier.Plain)) return;
            Vector3 feet = Flat(_player.Position);
            FxPiece ring = Piece(FxShape.Ring, FxMoment.ChoiceTaken, FxTier.Plain, feet + Vector3.up * 0.3f, Values.ChoiceRing, Values.ChoiceSeconds,
                                 Values.WorldRim, Values.WorldCore);
            ring.Velocity = Vector3.up * Values.ChoiceRise;
            ring.Unscaled = true;
            Put(ring);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                FxPiece star = Piece(FxShape.Spark, FxMoment.ChoiceTaken, FxTier.Plain,
                                     feet + new Vector3(Mathf.Cos(a) * 0.6f, 1.6f, Mathf.Sin(a) * 0.6f), Values.ChoiceStar, Values.ChoiceStarSeconds,
                                     Values.WorldRim, Values.WorldCore);
                star.Velocity = Vector3.up * (Values.ChoiceRise * 0.5f);
                star.TowardCamera = 0.3f;
                star.Unscaled = true;
                Put(star);
            }
        }

        // ---------------- Every frame ----------------

        void LateUpdate()
        {
            // Work order 090: the frame's steps, each still on its own (a fault in one is logged once and the others go on), as
            // delegates made once: a method passed as an Action is a new delegate each time, seven a frame.
            if (_frameSteps == null) _frameSteps = new Action[] { Find, Hurt, Appearances, RunCharges, RunDue, LookAtTheRun, TidyKegs };
            for (int i = 0; i < _frameSteps.Length; i++) Safely(_frameSteps[i]);
            _hits.Clear();
        }

        Action[] _frameSteps;

        /// <summary>4. The enemies that came this frame and not at a call: drawn appearing while a fight is on.</summary>
        void Appearances()
        {
            if (_appeared.Count == 0) return;
            IReadOnlyList<Enemy> all = Enemy.All;
            foreach (Enemy e in _appeared)
            {
                if (e == null) continue;
                bool fight = false;
                for (int i = 0; i < all.Count && !fight; i++)
                    fight = all[i] != null && all[i] != e && !_appeared.Contains(all[i]) && all[i].IsAwake;
                if (CombatFxRules.AppearsInAFight(fight)) Appear(e);
            }
            _appeared.Clear();
        }

        void RunDue()
        {
            // Work order 033 (the second reviews of 2026-11-20): what waits belongs to the floor it was asked on. All of it
            // is cancelled when that floor is removed or another is built, and what waits on game time when the run ends
            // (the end's slow-down would hold it until the screen is answered, and then let it go at the old place).
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            bool inRun = levels != null && levels.InRun;
            if (!ReferenceEquals(level, _dueLevel)) _due.Clear(); // by reference: a floor destroyed since is not "no floor"
            else if (_dueInRun && !inRun) _due.RemoveAll(d => !d.Unscaled);
            _dueLevel = level;
            _dueInRun = inRun;
            if (_due.Count == 0) return;
            float now = Time.time, real = Time.unscaledTime;
            _dueNow.Clear();
            for (int i = _due.Count - 1; i >= 0; i--)
            {
                if (_due[i].At > (_due[i].Unscaled ? real : now)) continue;
                _dueNow.Add(_due[i]);
                _due.RemoveAt(i);
            }
            for (int i = _dueNow.Count - 1; i >= 0; i--) Safely(_dueNow[i].Act);
        }

        /// <summary>Kegs taken away without a burst (a teleport, a new level): their rings go too.</summary>
        void TidyKegs()
        {
            if (_kegs.Count == 0 || _player == null || _player.KegsLying > 0) return;
            foreach (LyingKeg k in _kegs) if (k.Ring != null) Destroy(k.Ring.gameObject);
            _kegs.Clear();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Work order 123: chips flying off a hit, facing the camera, in place of a ring on the ground under it.</summary>
        FxPiece Chips(FxMoment moment, FxTier tier, Vector3 at, float reach, float size, int count, float seconds, Color rim, Color core)
        {
            FxPiece p = Piece(FxShape.Shards, moment, tier, at, reach, seconds, rim, core);
            p.Count = count;
            p.PieceSize = size;
            p.TowardCamera = 0.5f;
            return p;
        }
    }
}
