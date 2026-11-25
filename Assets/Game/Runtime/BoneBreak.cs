using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// A skeleton's body that comes apart (work order 122, a look order; Kristian 2026-11-25: "jeg har lyst at fiendene mister
    /// skjellet deler når de blir skutt på og at bein flyr rund etter slik fysikkens lover tilsier at det skal være. Gjerne knuse i
    /// biter"). Put on every enemy of a kind that breaks (<see cref="BoneBreakRules.Breaks"/>) and wears its model, as it appears
    /// (<see cref="Enemy.Appeared"/>): no line of the enemies' own code calls it. It listens to the enemy's hits as the hit flash
    /// does (<see cref="Damageable.Damaged"/>), and:
    ///
    /// - as its health falls past each <see cref="BoneBreakValues.PartShare"/> it loses a part (its skull, then the shin and foot on
    ///   her side): the part's root bone is scaled to nothing, which takes it off the skinned body, and the part's piece is thrown
    ///   from where it was along her shot (from her to it), harder for a harder hit (<see cref="BoneBreakRules.ThrowSpeed"/>);
    /// - at its death its body breaks into all its pieces still on it, thrown by the killing hit and bursting from its middle, and
    ///   its model is hidden at once (its death clip goes on unseen, and it is hidden again when its corpse would have been); the
    ///   bomber's breaks at its blast instead (<see cref="BoneBreakRules.WaitsForItsBlast"/>), outward from where it blows.
    ///
    /// Nothing of the fight changes: its health, its brain, its walk, its attacks and its timings are the enemy's own and are not
    /// touched, and nothing of the game reads its bones but the archer's hands, which never go. An enemy that comes back (the
    /// practice brute) stands up whole. With <see cref="BoneBreakValues.On"/> off it does nothing at all.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoneBreak : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => BoneBreakValues.On = true; // every Play starts on

        /// <summary>
        /// Listens for every enemy as it appears; after the enemies' own reset of their statics (SubsystemRegistration), which
        /// clears that event's listeners.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            Enemy.Appeared -= OnAppeared;
            Enemy.Appeared += OnAppeared;
        }

        static void OnAppeared(Enemy e)
        {
            if (!BoneBreakValues.On || e == null || e.Look == null || !BoneBreakRules.Breaks(e.Kind)) return;
            if (!e.TryGetComponent(out BoneBreak b)) b = e.gameObject.AddComponent<BoneBreak>();
            b.Bind(e);
        }

        Enemy _enemy;
        EnemyLook _look;
        Damageable _body;
        BoneBreakModel _model;
        Transform[] _frames = new Transform[0];
        readonly Transform[] _roots = new Transform[BoneParts.Count];
        readonly Vector3[] _rootScale = new Vector3[BoneParts.Count];
        readonly List<Material> _materials = new List<Material>();
        int _lost;          // the parts it has lost, a bit each (BonePart)
        int _partsLost;     // how many it has lost alive
        bool _broken, _waitingForBlast;
        int _killingDamage;
        BoneBreakRandom _random;

        /// <summary>For tests and pictures: the model's pieces, the parts lost alive, whether its body has broken.</summary>
        public BoneBreakModel Model => _model;
        public int PartsLost => _partsLost;
        public bool Broken => _broken;
        public bool HasLost(BonePart part) => (_lost & (1 << (int)part)) != 0;
        /// <summary>The bone a part hangs from on this enemy's model, or null.</summary>
        public Transform RootOf(BonePart part) => _roots[(int)part];

        /// <summary>Takes up an enemy that has just appeared: its model's pieces (made if they are not yet), its bones, its hits.</summary>
        public void Bind(Enemy e)
        {
            if (_body != null) _body.Damaged -= OnDamaged;
            _enemy = e;
            _look = e.Look;
            _body = e.Body;
            _model = BoneBreakModel.Of(e);
            _random = new BoneBreakRandom((ulong)Mathf.Max(1, e.Id));
            _lost = _partsLost = 0;
            _broken = _waitingForBlast = false;
            Transform model = _look.Model.transform;
            int n = _model != null ? _model.Shapes.Count : 0;
            _frames = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                BoneBreakModel.Shape shape = _model.Shapes[i];
                Renderer r = shape.Renderer < _look.Renderers.Length ? _look.Renderers[shape.Renderer] : null;
                _frames[i] = shape.OwnMesh ? (r != null ? r.transform : null) : EnemyLook.Find(model, shape.Bone);
            }
            for (int p = 0; p < BoneParts.Count; p++)
            {
                _roots[p] = EnemyLook.Find(model, BoneParts.RootBone((BonePart)p));
                _rootScale[p] = _roots[p] != null ? _roots[p].localScale : Vector3.one;
            }
            if (_body != null) _body.Damaged += OnDamaged;
            BonePieces.Ensure().Prewarm();
            enabled = false;
        }

        void OnDestroy()
        {
            if (_body != null) _body.Damaged -= OnDamaged;
            if (BonePieces.Current != null) BonePieces.Current.Forget(this);
        }

        // ---------------- A hit ----------------

        void OnDamaged(int amount)
        {
            if (!BoneBreakValues.On || _enemy == null || _look == null || _model == null || _broken || _waitingForBlast) return;
            Vector3 along = AlongHerShot();
            if (!_body.IsAlive)
            {
                if (BoneBreakRules.WaitsForItsBlast(_enemy.Kind))
                {
                    _waitingForBlast = true;
                    _killingDamage = amount;
                    enabled = true;
                    return;
                }
                Break(along, amount, false);
                return;
            }
            int due = BoneBreakRules.PartsDue(_body.CurrentHealth, _body.MaxHealth, BoneBreakValues.Live);
            if (due <= _partsLost) return;
            bool nearIsLeft = Vector3.Dot(_enemy.transform.right, along) > 0f; // its left faces her when her shot comes in from its left
            _look.PoseNow();
            while (_partsLost < due)
            {
                BonePart part = BoneBreakRules.AlivePart(_partsLost, nearIsLeft);
                _partsLost++;
                Lose(part, along, amount);
            }
        }

        /// <summary>Flat, the way her shot went: from her to it; its back when there is no her.</summary>
        Vector3 AlongHerShot()
        {
            Vector3 from = _enemy.Player != null ? _enemy.Player.Position : _enemy.transform.position + _enemy.transform.forward;
            Vector3 d = _enemy.transform.position - from;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = -_enemy.transform.forward;
            d.y = 0f;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.back;
        }

        /// <summary>A part off its body: its pieces thrown, its root bone scaled to nothing (held so after every pose).</summary>
        void Lose(BonePart part, Vector3 along, int damage)
        {
            if (HasLost(part) || _roots[(int)part] == null) return;
            Vector3 middle = Middle();
            for (int i = 0; i < _frames.Length; i++)
                if (_model.Shapes[i].Part == (int)part) ThrowShape(i, along, damage, middle, false, false);
            _lost |= 1 << (int)part;
            _roots[(int)part].localScale = Vector3.zero;
            enabled = true;
        }

        /// <summary>Its body into its pieces (those still on it), and its model hidden.</summary>
        void Break(Vector3 along, int damage, bool blast)
        {
            _look.PoseNow();
            Vector3 middle = Middle();
            for (int i = 0; i < _frames.Length; i++)
            {
                BoneBreakModel.Shape shape = _model.Shapes[i];
                if ((_lost & (1 << shape.Part)) != 0) continue;
                Renderer r = shape.Renderer < _look.Renderers.Length ? _look.Renderers[shape.Renderer] : null;
                if (r == null || !r.gameObject.activeInHierarchy || (!blast && !r.enabled)) continue; // hidden before (the lancer's sword)
                ThrowShape(i, along, damage, middle, true, blast);
            }
            foreach (Renderer r in _look.Renderers)
                if (r != null) r.enabled = false;
            _broken = true;
            _waitingForBlast = false;
            enabled = _enemy.ComesBack; // to stand it up whole when it comes back
        }

        Vector3 Middle()
        {
            float h = _enemy.ModelHeight > 0f ? _enemy.ModelHeight : 1f;
            return _enemy.transform.position + Vector3.up * (h * 0.5f);
        }

        void ThrowShape(int i, Vector3 along, int damage, Vector3 middle, bool death, bool blast)
        {
            Transform f = _frames[i];
            BoneBreakModel.Shape shape = _model.Shapes[i];
            if (f == null || shape.Whole == null) return;
            Renderer look = _look.Renderers[shape.Renderer];
            Vector3 scale = shape.OwnMesh ? f.lossyScale : Vector3.one * Mathf.Abs(_enemy.transform.lossyScale.x);
            Vector3 centre = f.position + f.rotation * Vector3.Scale(shape.Centre, scale);
            BoneBreakValues v = BoneBreakValues.Live;

            Vector3 out_ = centre - middle;
            out_.y = 0f;
            out_ = out_.sqrMagnitude > 1e-6f ? out_.normalized : new Vector3((float)_random.Signed(), 0f, (float)_random.Signed()).normalized;
            Vector3 way = blast ? out_ : along;
            float speed = (float)(BoneBreakRules.ThrowSpeed(damage, v) * (1.0 + v.ThrowSpread * _random.Signed()));
            Vector3 side = Vector3.Cross(Vector3.up, way);
            Vector3 velocity = way * speed + side * (float)(speed * v.ThrowSpread * _random.Signed())
                               + Vector3.up * (float)(v.UpBase + v.ThrowUp * speed * (0.6 + 0.4 * _random.Next()));
            if (death && !blast) velocity += out_ * (float)v.DeathScatter;
            Vector3 axis = new Vector3((float)_random.Signed(), (float)_random.Signed(), (float)_random.Signed());
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
            Vector3 spin = axis.normalized * (float)(v.MostSpin * (0.3 + 0.7 * _random.Next()));

            look.GetSharedMaterials(_materials);
            BonePieces.Ensure().Throw(shape.Whole, _materials, look, f.position, f.rotation, scale, shape.Centre, shape.Size,
                                      velocity, spin, shape, true, this);
        }

        // ---------------- Every frame, only while it has something to hold ----------------

        void LateUpdate() => Hold();

        /// <summary>Its frame (its LateUpdate; public for the test of what a frame allocates): what it holds, held.</summary>
        public void Hold()
        {
            if (_enemy == null || _look == null)
            {
                enabled = false;
                return;
            }
            if (_waitingForBlast)
            {
                // The bomber's body is hidden as it blows: it breaks then, from the pose it blew in.
                if (_look.Skin == null || !_look.Skin.enabled) Break(Vector3.zero, _killingDamage, true);
                return;
            }
            if (_enemy.IsDead)
            {
                // Dead: nothing to hold, unless it comes back (then it is watched for standing up whole).
                if (!_enemy.ComesBack || (!_broken && _lost == 0)) enabled = false;
                return;
            }
            if (_broken || !BoneBreakValues.On)
            {
                StandUpWhole(); // come back, or the switch turned off while a part was off: whole again
                return;
            }
            // Its lost parts stay off after every pose (the clips may set a bone's scale).
            for (int p = 0; p < BoneParts.Count; p++)
                if ((_lost & (1 << p)) != 0 && _roots[p] != null && _roots[p].localScale != Vector3.zero) _roots[p].localScale = Vector3.zero;
        }

        /// <summary>Come back (the practice brute): every part on again; its renderers are shown again by its own coming back.</summary>
        void StandUpWhole()
        {
            for (int p = 0; p < BoneParts.Count; p++)
                if (_roots[p] != null) _roots[p].localScale = _rootScale[p];
            _lost = _partsLost = 0;
            _broken = false;
            enabled = false;
        }
    }
}
