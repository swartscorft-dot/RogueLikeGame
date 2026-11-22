using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The bomber (work order 073, 2026-11-21: Kristian ticked "Ny fiendetype"; the producer's proposal, for him to correct
    /// after playing): a small, fast thing that runs at her and blows up. Its numbers are <see cref="BomberValues"/>, its
    /// fuse <see cref="BomberFuse"/>, its rules <see cref="BomberRules"/>.
    ///
    /// It wakes, chases and gives up through the brain exactly as the brute, the swarmer and the guard do (<see cref="Enemy"/>:
    /// its aggro range, a hurt, its leash), with no waking rule of its own; in the chase it runs straight at her. Its brain
    /// has no attack: within <see cref="BomberValues.FuseRange"/> of her, awake and alive, it stops where it is and lights its
    /// fuse. From that moment the circle of its blast is on the ground round it: its outline (a copy of the telegraph the
    /// factory gave it, which it draws nothing else with) thickening as every telegraph does, and a second ring filling it
    /// from the middle out over the fuse; its body swells and flashes faster and faster. When the fuse is out it blows: she
    /// takes the blast's damage if she touches the circle (none in the dash's protected part, as for any strike), every
    /// other enemy in it takes <see cref="BomberValues.EnemyDamage"/>, and another bomber in it that is not lit lights its
    /// own full fuse (a chain, each with its full fuse, never an instant one); then it is gone, dead by its own blast.
    ///
    /// Killed before it lit, it does not vanish: it drops, and lights the short fuse (<see cref="BomberValues.ShortFuseSeconds"/>)
    /// where it fell, the same circle and the same blast. Killed, dazed (an elite's bar, a counter) or moved while its fuse
    /// burns, the fuse goes on: it is already lit. The circle follows its body (work order 073 asked what a pull or a push
    /// does: nothing in the game pushes or pulls an enemy today, the Blast stat of work order 067 widens areas and the keg
    /// bursts where it lies; so the circle is drawn round the body every frame and the blast goes off where the body is).
    /// It never blows without its circle having been on the ground for the fuse's whole time: the telegraph is the rule
    /// (decision 0010).
    ///
    /// Heard and seen with what exists: its fuse lit is reported as the start of its Blast (the red wind-up sound and star),
    /// its blast on her as a strike (the strike sound; the circle strike's ring and star drawn by the effects where it
    /// blew, else the flat stand-in), and a spark of the keg's kind at the blast. For the log (<see cref="Enemy.Reported"/>):
    /// the fuse lit and how (<see cref="FuseCause"/>), the blast on her, and the blast on the others
    /// (<see cref="EnemyReportKind.Blasted"/>: how many it hurt and killed).
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyBomber : Enemy
    {
        // As Enemy's telegraph and strike.
        const float TelegraphHeight = 0.07f;
        const float TelegraphThin = 0.05f;
        const float TelegraphThick = 0.3f;
        const float BurstFxSeconds = 0.2f;
        // A spark of the keg's kind at the blast (work order 073: the keg's burst): this share of the circle, this long.
        const float SparkShare = 0.8f;
        const float SparkSeconds = 0.3f;
        static readonly ShotPaint s_SparkPaint = new ShotPaint(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.9f, 0.55f), 2f, 3f);

        static readonly List<Enemy> s_near = new List<Enemy>(); // the enemies a blast looks at, copied: it may change them

        /// <summary>What its last blast did.</summary>
        public struct BlastResult
        {
            public Vector3 Centre;
            public bool HerInside;      // she touched the circle
            public bool HerHurt;        // and took its damage
            public int Hurt;            // other enemies it hurt
            public int Killed;          // of them, those it killed
            public int Lit;             // bombers it set off
        }

        public BomberValues Tuning = new BomberValues();

        readonly BomberFuse _fuse = new BomberFuse();
        LineRenderer _ring, _fill;
        Transform _swelling;
        Vector3 _swellFrom;
        bool _flashShown;

        public override EnemyKind Kind => EnemyKind.Bomber;
        public override EnemyValues Values => Tuning.Enemy;

        /// <summary>Its fuse: lit or not, when, for how long, blown.</summary>
        public BomberFuse Fuse => _fuse;
        /// <summary>Its blasts: none, or one.</summary>
        public int Blasts { get; private set; }
        /// <summary>What its blast did, once it blew.</summary>
        public BlastResult LastBlast { get; private set; }
        /// <summary>The outline of its blast's circle is on the ground now.</summary>
        public bool CircleShown => _ring != null && _ring.enabled;
        /// <summary>The ring filling the circle is on the ground now.</summary>
        public bool FillShown => _fill != null && _fill.enabled;
        /// <summary>Where the middle of the circle is drawn now (the ring's first point is its edge: its middle is its body's).</summary>
        public Vector3 CircleCentre { get; private set; }
        /// <summary>m from the middle to the filling ring now, out of <see cref="BomberValues.BlastRadius"/>.</summary>
        public float FillRadius { get; private set; }
        /// <summary>The outline's width now: thin as it is lit, thick as it blows.</summary>
        public float CircleWidth => _ring != null ? _ring.widthMultiplier : 0f;
        /// <summary>Times its size it is shown now (it swells while its fuse burns).</summary>
        public float SwellNow { get; private set; } = 1f;
        /// <summary>Its body is lit up by a flash now.</summary>
        public bool FlashShown => _flashShown;

        protected override void Awake()
        {
            base.Awake();
            // The telegraph the factory gave it is the outline of its blast's circle; a copy of it is the ring that fills it.
            _ring = Telegraph;
            Telegraph = null;
            if (_ring != null)
            {
                _ring.name = "BomberCircle";
                _ring.enabled = false;
                GameObject fill = Instantiate(_ring.gameObject, transform, false);
                fill.name = "BomberFill";
                _fill = fill.GetComponent<LineRenderer>();
                _fill.loop = true;
                _fill.useWorldSpace = true;
                _fill.enabled = false;
            }
            // Its model is the swarmer's, tinted (work order 073): the tint is its body's colour, which the daze and the flashes go back to.
            if (Look != null && BodyRenderer != null)
            {
                BodyColor = EnemyFactory.BomberTint;
                EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
            }
        }

        protected override void Update()
        {
            base.Update();
            float now = Time.time;
            if (IsDead && !_fuse.IsLit) Light(now, FuseCause.Dropped); // killed before it lit: the short fuse, where it fell
            if (!_fuse.IsBurning) return;
            if (_fuse.Due(now)) Blow(now);
            else ShowFuse(now);
        }

        // ---------------- The chase ----------------

        protected override void Chase(Vector3 toPlayer, float distance)
        {
            if (_fuse.IsLit) Halt();
            else Walk(Player.Position);
        }

        protected override void AfterMove()
        {
            if (_fuse.IsLit)
            {
                Halt(); // lit, it stays where it is
                return;
            }
            if (Player == null) return;
            float distance = Flat(Player.Position - transform.position).magnitude;
            if (BomberRules.Lights(!IsDead, State == EnemyState.Chase, PlayerHealth == null || PlayerHealth.IsAlive, distance, Tuning))
                Light(Time.time, FuseCause.Range);
        }

        // ---------------- The fuse ----------------

        /// <summary>
        /// Another bomber's blast caught it (work order 073): it lights its own full fuse, if it is not lit yet. True when it
        /// was lit now.
        /// </summary>
        public bool SetOff(float now) => Light(now, FuseCause.Chain);

        bool Light(float now, FuseCause cause)
        {
            if (!_fuse.Light(now, cause, Tuning)) return false;
            if (!IsDead) Halt();
            _swelling = Look != null ? Look.Pivot : BodyRenderer != null ? BodyRenderer.transform : null;
            if (_swelling != null) _swellFrom = _swelling.localScale;
            ShowFuse(now);
            Report(EnemyReportKind.AttackStarted, Tuning.BlastAttack, count: (int)cause);
            return true;
        }

        /// <summary>The circle round its body as it is now, filled as far as the fuse has burnt; its body swollen and flashing.</summary>
        void ShowFuse(float now)
        {
            float p = _fuse.Progress(now);
            Vector3 centre = Flat(transform.position);
            float radius = Tuning.BlastRadius;
            CircleCentre = centre;
            FillRadius = radius * p;
            if (_ring != null)
            {
                if (!_ring.enabled) _ring.enabled = true; // also after its body is hidden, dead
                GroundOutline.Circle(_ring, centre, radius, TelegraphHeight);
                _ring.widthMultiplier = Mathf.Lerp(TelegraphThin, TelegraphThick, p);
            }
            if (_fill != null)
            {
                if (!_fill.enabled) _fill.enabled = true;
                GroundOutline.Circle(_fill, centre, FillRadius, TelegraphHeight);
                _fill.widthMultiplier = Mathf.Lerp(TelegraphThin, TelegraphThick, p);
            }
            SwellNow = BomberRules.Swell(p, Tuning);
            if (_swelling != null) _swelling.localScale = _swellFrom * SwellNow;
            Flash(BomberRules.FlashOn(now - _fuse.LitAt, _fuse.Seconds, Tuning));
        }

        /// <summary>Draws its fuse as it is now, as every frame does while it burns: for the test of what a frame allocates.</summary>
        public void DrawFuseNow()
        {
            if (_fuse.IsBurning) ShowFuse(Time.time);
        }

        void Flash(bool on)
        {
            if (on == _flashShown) return;
            _flashShown = on;
            if (BodyRenderer != null) EnemyFactory.Paint(BodyRenderer.sharedMaterial, on ? EnemyLook.FlashColor : BodyColor);
        }

        // ---------------- The blast ----------------

        void Blow(float now)
        {
            if (!_fuse.Blow(now)) return;
            Blasts++;
            Vector3 centre = Flat(transform.position);
            GroundPoint at = Ground(centre);
            if (_ring != null) _ring.enabled = false;
            if (_fill != null) _fill.enabled = false;
            Flash(false);
            if (_swelling != null) _swelling.localScale = _swellFrom;
            SwellNow = 1f;
            SpawnBlastFx(centre);

            var result = new BlastResult { Centre = centre };
            // Her: as a strike, by its damage scale (the floor's), none in the dash's protected part.
            if (Player != null)
            {
                result.HerInside = BomberRules.InBlast(at, Tuning, Player.GroundPosition, Placeholders.PlayerRadius);
                int landed = StrikesLanded;
                Land(Tuning.BlastAttack, result.HerInside);
                result.HerHurt = StrikesLanded > landed;
            }
            // Its own side: every other enemy in the circle is hurt, a bomber not yet lit lights its own fuse.
            int dealt = 0;
            s_near.Clear();
            foreach (Enemy e in All) s_near.Add(e);
            foreach (Enemy e in s_near)
            {
                if (e == null || e == this || e.Body == null) continue;
                var bomber = e as EnemyBomber;
                bool inside = BomberRules.InBlast(at, Tuning, e.Body.GroundPosition, e.Body.Radius);
                switch (BomberRules.What(inside, !e.IsDead, bomber != null, bomber != null && bomber._fuse.IsLit))
                {
                    case BlastEffect.Lights:
                        if (bomber.SetOff(now)) result.Lit++;
                        break;
                    case BlastEffect.Hurts:
                    {
                        int applied = e.Body.TakeHit(Mathf.Max(0, Tuning.EnemyDamage));
                        if (applied <= 0) break;
                        result.Hurt++;
                        dealt += applied;
                        if (!e.Body.IsAlive) result.Killed++;
                        break;
                    }
                }
            }
            s_near.Clear();
            LastBlast = result;
            Report(EnemyReportKind.Blasted, Tuning.BlastAttack, damage: dealt, count: result.Hurt, killed: result.Killed);

            // It is gone with its blast: dead by it, if it was not, and not seen any more.
            if (_self != null && _self.IsAlive)
            {
                _self.WardShare = 0f; // work order 079: a warded bomber's own end is not lessened (its ward goes with it)
                _self.TakeHit(_self.CurrentHealth);
            }
            Hide();
        }

        void Hide()
        {
            if (Look != null)
                foreach (Renderer r in Look.Renderers)
                    if (r != null) r.enabled = false;
            if (BodyRenderer != null) BodyRenderer.enabled = false;
        }

        void SpawnBlastFx(Vector3 centre)
        {
            // The keg's kind of spark at the blast (work order 073), when she has the shot effects' material.
            Material shotFx = Player != null ? Player.ShotFxMaterial : null;
            if (shotFx != null) ShotFx.Spark(centre + Vector3.up * 0.8f, Tuning.BlastRadius * SparkShare, SparkSeconds, s_SparkPaint, shotFx, 0.5f);
            // A circle strike where it blew, drawn by the effects in the strike's red (work order 034's StrikeAtDrawer); the
            // flat stand-in below when they are not running.
            if (StrikeAtDrawer != null && StrikeAtDrawer(this, Tuning.BlastAttack, centre)) return;
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fx.name = "StrikeFx";
            Destroy(fx.GetComponent<Collider>());
            fx.transform.SetParent(transform.parent, false);
            fx.transform.position = centre + Vector3.up * 0.04f;
            fx.transform.localScale = new Vector3(Tuning.BlastRadius * 2f, 0.02f, Tuning.BlastRadius * 2f);
            if (StrikeMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = StrikeMaterial;
            Destroy(fx, BurstFxSeconds);
        }
    }
}
