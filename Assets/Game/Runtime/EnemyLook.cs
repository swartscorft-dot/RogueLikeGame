using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Roguelike
{
    /// <summary>
    /// An enemy's model and its animation (work order 011). The model of its kind (<see cref="EnemyModelSpec"/>) is
    /// loaded from Resources by name and put on it in place of the stand-in capsule, which is switched off: under a
    /// "Model" pivot that scales it to the height it was made to (the skinned mesh as bound to its bones), lowers it to
    /// the floor if it is clearly off it, and turns it a quarter turn if it looks clearly to a side (each measured once
    /// per model; as imported the four need none of it: scale 1, on the floor, looking along +Z). Each mesh gets a
    /// Simple Lit material made here with its painted texture, as the props do (<see cref="PropFactory"/>); the body's
    /// is the one the daze tints.
    ///
    /// The clips are played by a Playables graph of its own, an <see cref="AnimationMixerPlayable"/> with one input
    /// per clip, driven by hand from game time: each frame the enemy tells it the time, and it sets each clip's time
    /// and weight and evaluates. No Animator Controller. A new clip blends in over
    /// <see cref="EnemyModelSpec.FadeSeconds"/>; an attack cut short by a daze or a death is left at once.
    /// Standing it plays Idle, moving its walk or run at the speed of its feet (<see cref="EnemyGaits"/>), winding up
    /// and recovering its attack's clip timed so the clip's strike lands on the game's (<see cref="AttackClipPlan"/>),
    /// dazed its Stagger once and then the last frame of it, dead its Death and then the last frame.
    ///
    /// The archer shoots far to its left, so the body is turned to the right as its Shoot blends in, and back as it
    /// blends out: by the angle the arrow (from the hand at the string through the hand on the bow) makes with the
    /// facing at the clip's release, measured once on the clip (about 97 degrees; the table's 98 if it cannot be).
    /// Its bow is set on the hand as exported while it shoots; otherwise it hangs upright, a turn measured on the hand
    /// at the first frame of Idle.
    /// </summary>
    public sealed class EnemyLook
    {
        public const string PivotName = "Model";
        /// <summary>The start of the name of a material made here for one enemy's model; it goes with the enemy.</summary>
        public const string MaterialPrefix = "EnemyModel_";
        /// <summary>s a dead enemy lies on the floor after its Death has played, before it is gone.</summary>
        public const float LieSeconds = 1f;
        /// <summary>The colour a hit flashes on a model: over white, so the painted texture shows bright, not flat white.</summary>
        public static readonly Color FlashColor = new Color(2.4f, 2.4f, 2.4f, 1f);

        /// <summary>What was measured of a model as imported, once per model.</summary>
        public sealed class Measure
        {
            public float ImportedHeight;     // m, the top of the skinned mesh over its lowest point, as imported
            public float ImportedBottom;     // m, its lowest point
            public float BindHeight;         // m, the skinned mesh in the pose it was bound in
            public float FacingYaw;          // degrees from +Z the body faces, as imported
            /// <summary>
            /// Degrees to the right the body turns while it shoots, so the left arm points where it faced: measured on
            /// the clip at its release; the table's figure when it cannot be.
            /// </summary>
            public float ShotTurn;
            public bool ShotTurnMeasured;
            public float Scale = 1f;
            public float Yaw;                // degrees the pivot turns it by: the facing, put right to the nearest quarter turn
            public float Lift;
        }

        static readonly Dictionary<string, GameObject> s_models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, AnimationClip[]> s_clips = new Dictionary<string, AnimationClip[]>();
        static readonly Dictionary<string, Measure> s_measures = new Dictionary<string, Measure>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_models.Clear();
            s_clips.Clear();
            s_measures.Clear();
        }

        public readonly EnemyModelSpec Spec;
        public readonly Transform Pivot;
        public readonly GameObject Model;
        public readonly SkinnedMeshRenderer Skin;
        public readonly Renderer[] Renderers;
        public readonly Measure Measured;
        public readonly Animator Animator;

        readonly List<Material> _materials = new List<Material>();
        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly EnemyClipSpec[] _specs;
        readonly AnimationClip[] _clipOf;
        readonly AnimationClipPlayable[] _playables;
        readonly float[] _weights;
        readonly double[] _times;
        readonly double[] _rates;
        int _target = -1;
        float _last = -1f;

        // What it shows, and since when.
        enum Mode { None, Move, Attack, Dazed, Dead }
        Mode _mode;
        EnemyGait _gait;
        EnemyAttackDefinition _attack;
        EnemyState _attackState;
        AttackClipPlan _plan;
        float _windupAt, _strikeAt, _dazedAt, _deadAt;

        // The archer's bow.
        readonly Transform _bow;
        readonly Quaternion _bowShooting;
        Quaternion _bowResting;
        bool _bowRestMeasured;
        readonly int _shootInput = -1;

        public bool IsValid => _graph.IsValid();
        /// <summary>The times it was shown (<see cref="Tick"/>): for tests (work order 026).</summary>
        public int Ticks { get; private set; }
        /// <summary>The clip it is playing or blending into.</summary>
        public string Playing => _target >= 0 ? _specs[_target].Name : null;
        /// <summary>Seconds into the clip it is playing or blending into.</summary>
        public double PlayingTime => _target >= 0 ? _playables[_target].GetTime() : 0;
        /// <summary>How fast the clip it plays runs, for the walk and the run.</summary>
        public double PlayingRate => _target >= 0 ? _rates[_target] : 0;
        /// <summary>The attack clip's timing for the attack it plays, while it plays one.</summary>
        public AttackClipPlan Plan => _plan;
        public float BowRestAngleBefore { get; private set; } = -1f;
        public bool HasBow => _bow != null;

        /// <summary>The imported model of a kind, loaded once; null when it is not in the project.</summary>
        public static GameObject Source(EnemyModelSpec spec)
        {
            if (s_models.TryGetValue(spec.Name, out GameObject go) && go != null) return go;
            go = Resources.Load<GameObject>(spec.ResourcePath);
            s_models[spec.Name] = go;
            return go;
        }

        /// <summary>The model's animation takes, without the importer's preview copies.</summary>
        public static AnimationClip[] Clips(EnemyModelSpec spec)
        {
            if (s_clips.TryGetValue(spec.Name, out AnimationClip[] clips) && clips != null) return clips;
            var list = new List<AnimationClip>();
            foreach (AnimationClip c in Resources.LoadAll<AnimationClip>(spec.ResourcePath))
                if (c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal)) list.Add(c);
            clips = list.ToArray();
            s_clips[spec.Name] = clips;
            return clips;
        }

        /// <summary>The take that is this clip (by the end of its name), or null.</summary>
        public static AnimationClip FindClip(EnemyModelSpec spec, string name)
        {
            foreach (AnimationClip c in Clips(spec))
                if (EnemyModelSpec.IsTake(c.name, name)) return c;
            return null;
        }

        /// <summary>
        /// Puts the model of the enemy's kind on it, in place of the capsule and its nose. Null, and the capsule left as
        /// it is, when the model cannot be loaded.
        /// </summary>
        public static EnemyLook Dress(Enemy enemy)
        {
            EnemyModelSpec spec = EnemyModelSpec.Of(enemy.Kind);
            GameObject source = Source(spec);
            if (source == null)
            {
                Debug.LogWarning("No model for the " + EnemyKinds.Word(enemy.Kind) + " at Resources/" + spec.ResourcePath + ": it stays a capsule.");
                return null;
            }
            // A copy of an enemy that already wore a model (a generated brute is a copy of the practice one) brings
            // that one along: it is taken off, and this enemy gets its own.
            Transform worn = enemy.transform.Find(PivotName);
            if (worn != null)
            {
                worn.gameObject.SetActive(false);
                worn.SetParent(null, false);
                Object.Destroy(worn.gameObject);
            }
            foreach (Transform child in enemy.transform)
                if (child.name == "Body" || child.name == "Facing") child.gameObject.SetActive(false);
            return new EnemyLook(enemy, spec, source);
        }

        EnemyLook(Enemy enemy, EnemyModelSpec spec, GameObject source)
        {
            Spec = spec;
            var pivot = new GameObject(PivotName);
            pivot.layer = enemy.gameObject.layer;
            Pivot = pivot.transform;
            Pivot.SetParent(enemy.transform, false);
            Model = Object.Instantiate(source, Pivot, false);
            Model.name = spec.Name;
            foreach (Collider c in Model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Transform t in Model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = enemy.gameObject.layer;
            foreach (Transform t in Model.GetComponentsInChildren<Transform>(true))
                if (!_bones.ContainsKey(t.name)) _bones[t.name] = t;
            Renderers = Model.GetComponentsInChildren<Renderer>(true);
            Skin = MainSkin(Model);

            Measured = MeasureOf(spec, Model, Skin, enemy.transform);
            Pivot.localScale = Vector3.one * Measured.Scale;
            Pivot.localPosition = Vector3.up * Measured.Lift;
            Pivot.localRotation = Quaternion.Euler(0f, Measured.Yaw, 0f);

            // The materials: each mesh its own painted texture.
            foreach (Renderer r in Renderers)
            {
                Material m = MakeMaterial(spec, r);
                _materials.Add(m);
                var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = m;
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }

            // The animation: a mixer with one input per clip of the table that the model has.
            Animator = Model.GetComponent<Animator>();
            if (Animator == null) Animator = Model.AddComponent<Animator>();
            Animator.runtimeAnimatorController = null;
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _specs = spec.Clips;
            int n = _specs.Length;
            _clipOf = new AnimationClip[n];
            _playables = new AnimationClipPlayable[n];
            _weights = new float[n];
            _times = new double[n];
            _rates = new double[n];
            _graph = PlayableGraph.Create("Enemy " + spec.Name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", Animator);
            _mixer = AnimationMixerPlayable.Create(_graph, n);
            output.SetSourcePlayable(_mixer);
            for (int i = 0; i < n; i++)
            {
                _clipOf[i] = FindClip(spec, _specs[i].Name);
                _rates[i] = 1;
                if (_clipOf[i] == null) continue;
                AnimationClipPlayable p = AnimationClipPlayable.Create(_graph, _clipOf[i]);
                p.SetApplyFootIK(false);
                p.SetApplyPlayableIK(false);
                p.SetSpeed(0); // its time is set by hand each frame
                _graph.Connect(p, 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _playables[i] = p;
            }
            _graph.Play();

            if (spec.ShotLeftOfFacing != 0)
            {
                EnemyClipSpec shoot = spec.AttackClips.Length >= 2 ? spec.Clip(spec.AttackClips[1]) : null;
                _shootInput = shoot != null ? Array.IndexOf(_specs, shoot) : -1;
                foreach (Renderer r in Renderers)
                {
                    if (r == Skin || r.name.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _bow = r.transform;
                    _bowShooting = _bow.localRotation;
                    _bowResting = _bowShooting;
                    break;
                }
                if (!Measured.ShotTurnMeasured) MeasureShotTurn(enemy.transform);
            }
            Show(Index("Idle"), true, restart: true);
            Apply();
        }

        /// <summary>
        /// Poses the shot's release, the body not turned, and reads how far to the left of where it faces the arrow
        /// points (<see cref="ShotDirection"/>): the turn to the right that puts the arrow where the body faced. Once
        /// per model.
        /// </summary>
        void MeasureShotTurn(Transform enemy)
        {
            Measured.ShotTurnMeasured = true;
            Measured.ShotTurn = (float)Spec.ShotLeftOfFacing;
            if (_shootInput < 0 || _clipOf[_shootInput] == null) return;
            for (int i = 0; i < _specs.Length; i++)
                if (_clipOf[i] != null) _mixer.SetInputWeight(i, i == _shootInput ? 1f : 0f);
            _playables[_shootInput].SetTime(_specs[_shootInput].Strike);
            Pivot.localRotation = Quaternion.Euler(0f, Measured.Yaw, 0f);
            _graph.Evaluate(0f);
            Vector3 arm = enemy.InverseTransformDirection(ShotDirection());
            if (new Vector2(arm.x, arm.z).sqrMagnitude > 1e-6f) Measured.ShotTurn = -Mathf.Atan2(arm.x, arm.z) * Mathf.Rad2Deg;
        }

        /// <summary>The bone of that name (by the end of its name), or null.</summary>
        public Transform Bone(string name)
        {
            if (_bones.TryGetValue(name, out Transform t)) return t;
            foreach (KeyValuePair<string, Transform> kv in _bones)
                if (EnemyModelSpec.IsTake(kv.Key, name)) return _bones[name] = kv.Value;
            return null;
        }

        /// <summary>The weight the clip has in the mix now, 0 to 1.</summary>
        public float Weight(string clip)
        {
            int i = Index(clip);
            return i < 0 ? 0f : _weights[i];
        }

        public bool Has(string clip)
        {
            int i = Index(clip);
            return i >= 0 && _clipOf[i] != null;
        }

        /// <summary>Flat, the way the left arm points, shoulder to hand: the archer's shot leaves along it.</summary>
        public Vector3 LeftArmDirection()
        {
            Transform arm = Bone("LeftArm"), hand = Bone("LeftHand");
            if (arm == null || hand == null) return Vector3.zero;
            Vector3 d = hand.position - arm.position;
            d.y = 0f;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.zero;
        }

        /// <summary>
        /// Flat, the way an arrow drawn on the bow points: from the hand at the string (the right) through the hand on
        /// the bow (the left). The left arm's way without both hands.
        /// </summary>
        public Vector3 ShotDirection()
        {
            Transform draw = Bone("RightHand"), bow = Bone("LeftHand");
            if (draw == null || bow == null) return LeftArmDirection();
            Vector3 d = bow.position - draw.position;
            d.y = 0f;
            return d.sqrMagnitude > 1e-8f ? d.normalized : LeftArmDirection();
        }

        /// <summary>Degrees between the bow's long side and straight up; -1 without a bow.</summary>
        public float BowUpAngle() => _bow == null ? -1f : UpAngle(_bow);

        /// <summary>
        /// Shows the enemy at this moment: picks the clip for what it does, sets the clips' times and weights, and
        /// evaluates. Called by the enemy each frame, after it has thought and walked.
        /// </summary>
        public void Tick(Enemy enemy, Vector3 velocity, float now)
        {
            if (!_graph.IsValid()) return;
            Ticks++;
            float dt = _last < 0f ? 0f : Mathf.Max(0f, now - _last);
            _last = now;

            if (enemy.IsDead)
            {
                if (_mode != Mode.Dead)
                {
                    bool cut = _mode == Mode.Attack;
                    _mode = Mode.Dead;
                    _deadAt = now;
                    _attack = null;
                    Show(Index("Death"), cut, restart: true);
                }
                SetOneShot(Index("Death"), now - _deadAt, holdEnd: true);
            }
            else if (enemy.IsDazed)
            {
                if (_mode != Mode.Dazed)
                {
                    // A windup the daze cuts short is left at once.
                    _mode = Mode.Dazed;
                    _dazedAt = now;
                    _attack = null;
                    Show(Spec.HasStagger && Has("Stagger") ? Index("Stagger") : Index("Idle"), true, restart: true);
                }
                if (_target == Index("Stagger")) SetOneShot(_target, now - _dazedAt, holdEnd: true);
                else _rates[_target] = 0.5;
            }
            else
            {
                if (_mode == Mode.Dead) Reset(); // it stood up again: as itself, at once
                EnemyAttackDefinition attack = enemy.Attack;
                EnemyState state = enemy.State;
                EnemyClipSpec clip = attack != null && (state == EnemyState.Windup || state == EnemyState.Recover)
                    ? Spec.ClipForAttack(attack.Name) : null;
                int input = clip != null ? Array.IndexOf(_specs, clip) : -1;
                if (input >= 0 && _clipOf[input] != null)
                {
                    bool fresh = _mode != Mode.Attack || attack != _attack || (state == EnemyState.Windup && _attackState != EnemyState.Windup);
                    if (fresh)
                    {
                        _mode = Mode.Attack;
                        _attack = attack;
                        _plan = AttackClipPlan.For(attack.Windup, attack.Recovery, clip.Strike, clip.Length);
                        _windupAt = now;
                        _strikeAt = now;
                        Show(input, false, restart: true);
                    }
                    if (state == EnemyState.Recover && _attackState != EnemyState.Recover) _strikeAt = now;
                    _attackState = state;
                    double t = state == EnemyState.Windup ? _plan.InWindup(now - _windupAt) : _plan.AfterStrike(now - _strikeAt);
                    _times[input] = Math.Min(t, LastFrame(input));
                    _rates[input] = state == EnemyState.Windup ? _plan.WindupSpeed : _plan.RecoverySpeed;
                }
                else
                {
                    if (_mode != Mode.Move) _gait = EnemyGait.Stand;
                    _mode = Mode.Move;
                    _attack = null;
                    _attackState = EnemyState.Idle;
                    velocity.y = 0f;
                    float speed = velocity.magnitude;
                    // The strides were measured on the model as imported: scaled with it.
                    double walk = Spec.WalkStride * Measured.Scale, run = Spec.RunStride * Measured.Scale;
                    _gait = EnemyGaits.For(speed, _gait, walk, Has("Run") ? run : 0);
                    int legs = _gait == EnemyGait.Stand ? Index("Idle") : _gait == EnemyGait.Run ? Index("Run") : Index(Spec.Walk);
                    if (legs < 0 || _clipOf[legs] == null) legs = Index("Idle");
                    Show(legs, false);
                    if (_gait != EnemyGait.Stand && legs >= 0)
                        _rates[legs] = EnemyGaits.Rate(speed, _gait == EnemyGait.Run ? run : walk);
                    else if (legs >= 0) _rates[legs] = 1;
                }
            }

            Blend(dt);
            AdvanceTheOthers(dt);
            Apply();
        }

        /// <summary>Back to standing at its first frame of Idle, as when it stands up at home after a death.</summary>
        public void Reset()
        {
            _mode = Mode.None;
            _attack = null;
            _attackState = EnemyState.Idle;
            _gait = EnemyGait.Stand;
            int idle = Index("Idle");
            Show(idle, true, restart: true);
        }

        /// <summary>The time its Death takes to play, or 0 without one.</summary>
        public float DeathSeconds
        {
            get
            {
                int i = Index("Death");
                return i >= 0 && _clipOf[i] != null ? _clipOf[i].length : 0f;
            }
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            foreach (Material m in _materials)
                if (m != null) Object.Destroy(m);
            _materials.Clear();
        }

        // ---------------- The mix ----------------

        int Index(string clip)
        {
            for (int i = 0; i < _specs.Length; i++)
                if (string.Equals(_specs[i].Name, clip, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        double LastFrame(int i) => _clipOf[i] == null ? 0 : Math.Max(0, _clipOf[i].length - 1e-4);

        /// <summary>Makes this clip the one it plays: at once (every other clip out) or blending in.</summary>
        void Show(int input, bool atOnce, bool restart = false)
        {
            if (input < 0) return;
            if (input != _target && (restart || _weights[input] <= 0f)) _times[input] = 0;
            else if (restart && input == _target) _times[input] = 0;
            _target = input;
            if (!atOnce) return;
            for (int i = 0; i < _weights.Length; i++) _weights[i] = i == input ? 1f : 0f;
        }

        void SetOneShot(int input, double since, bool holdEnd)
        {
            if (input < 0 || _clipOf[input] == null) return;
            _times[input] = holdEnd ? Math.Min(Math.Max(0, since), LastFrame(input)) : since;
            _rates[input] = 1;
        }

        void Blend(float dt)
        {
            if (_target < 0) return;
            float w = Mathf.Min(1f, _weights[_target] + dt / (float)EnemyModelSpec.FadeSeconds);
            float rest = 0f;
            for (int i = 0; i < _weights.Length; i++)
                if (i != _target) rest += _weights[i];
            float scale = rest > 1e-6f ? (1f - w) / rest : 0f;
            for (int i = 0; i < _weights.Length; i++)
                _weights[i] = i == _target ? (rest > 1e-6f ? w : 1f) : _weights[i] * scale;
        }

        /// <summary>The clips other than the one it plays go on as they were while they blend out; a loop wraps round.</summary>
        void AdvanceTheOthers(float dt)
        {
            for (int i = 0; i < _specs.Length; i++)
            {
                if (_clipOf[i] == null) continue;
                bool driven = i == _target && (_mode == Mode.Attack || _mode == Mode.Dead || (_mode == Mode.Dazed && _specs[i].Name == "Stagger"));
                if (!driven && (_weights[i] > 0f || i == _target))
                {
                    if (_specs[i].Loop)
                    {
                        double len = Math.Max(1e-3, _clipOf[i].length);
                        _times[i] = (_times[i] + dt * _rates[i]) % len;
                    }
                    else _times[i] = Math.Min(_times[i] + dt * _rates[i], LastFrame(i));
                }
            }
        }

        void Apply()
        {
            for (int i = 0; i < _specs.Length; i++)
            {
                if (_clipOf[i] == null) continue;
                _mixer.SetInputWeight(i, _weights[i]);
                _playables[i].SetTime(_times[i]);
            }
            float shooting = _shootInput >= 0 ? _weights[_shootInput] : 0f;
            Pivot.localRotation = Quaternion.Euler(0f, Measured.Yaw + Measured.ShotTurn * shooting, 0f);
            _graph.Evaluate(0f);

            if (_bow == null) return;
            if (!_bowRestMeasured && _target >= 0 && _specs[_target].Name == "Idle")
            {
                // At rest the bow hangs upright beside the leg: the turn on the hand that stands it up, measured once.
                _bowRestMeasured = true;
                BowRestAngleBefore = UpAngle(_bow);
                if (BowRestAngleBefore > 25f)
                {
                    Vector3 axis = LongAxis(_bow);
                    Vector3 world = _bow.rotation * axis;
                    if (Vector3.Dot(world, Vector3.up) < 0f) world = -world;
                    Quaternion upright = Quaternion.FromToRotation(world, Vector3.up) * _bow.rotation;
                    _bowResting = Quaternion.Inverse(_bow.parent.rotation) * upright;
                }
            }
            bool shoot = _shootInput >= 0 && (_target == _shootInput || _weights[_shootInput] > 0f);
            _bow.localRotation = shoot ? _bowShooting : _bowResting;
        }

        // ---------------- The model ----------------

        static SkinnedMeshRenderer MainSkin(GameObject model)
        {
            SkinnedMeshRenderer best = null;
            foreach (SkinnedMeshRenderer s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null && (best == null || s.sharedMesh.vertexCount > best.sharedMesh.vertexCount)) best = s;
            return best;
        }

        /// <summary>
        /// The height, lowest point and facing of the model as imported, under its pivot not yet moved, measured once
        /// per model; and the scale, lift and turn that put it right.
        /// </summary>
        public static Measure MeasureOf(EnemyModelSpec spec, GameObject model, SkinnedMeshRenderer skin, Transform space)
        {
            if (s_measures.TryGetValue(spec.Name, out Measure m)) return m;
            m = new Measure();
            if (skin != null && SkinBounds(skin, space, out Vector3 lo, out Vector3 hi))
            {
                m.ImportedBottom = lo.y;
                m.ImportedHeight = hi.y - lo.y;
            }
            m.BindHeight = BindHeight(skin, space);
            m.FacingYaw = FacingOf(model, space);
            // Turned only when it looks clearly to a side or back, as an exporter's wrong axis would leave it.
            m.Yaw = Mathf.Abs(m.FacingYaw) <= 60f ? 0f : -Mathf.Round(m.FacingYaw / 90f) * 90f;
            // The table's heights are the models as made, in their rest pose (standing in Idle they stoop lower).
            float made = m.BindHeight > 1e-3f ? m.BindHeight : m.ImportedHeight;
            m.Scale = made > 1e-3f ? (float)spec.Height / made : 1f;
            // Lifted or lowered only when it is clearly off the floor: a few centimetres are the pose's.
            m.Lift = Mathf.Abs(m.ImportedBottom) > (float)spec.FloorShare * m.ImportedHeight ? -m.ImportedBottom * m.Scale : 0f;
            s_measures[spec.Name] = m;
            return m;
        }

        /// <summary>The skinned mesh as posed now, as a box in the space of <paramref name="space"/>.</summary>
        public static bool SkinBounds(SkinnedMeshRenderer skin, Transform space, out Vector3 lo, out Vector3 hi)
        {
            lo = Vector3.positiveInfinity;
            hi = Vector3.negativeInfinity;
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                Matrix4x4 toSpace = space.worldToLocalMatrix * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                Vector3[] verts = baked.vertices;
                if (verts.Length == 0) return false;
                foreach (Vector3 v in verts)
                {
                    Vector3 p = toSpace.MultiplyPoint3x4(v);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
                return true;
            }
            finally
            {
                Object.Destroy(baked);
            }
        }

        /// <summary>
        /// Degrees from +Z (to the right is positive) that the model looks: from its head to the "headfront" bone in
        /// front of its face. The clips' stances stand the body bladed some 30 degrees and turn the toes out, so the head
        /// is what tells where it faces. Without that bone, square to its hips and shoulders.
        /// </summary>
        public static float FacingOf(GameObject model, Transform space) => FacingOf(model, space, true);

        /// <summary>As <see cref="FacingOf(GameObject, Transform)"/>, or by the hips and shoulders alone.</summary>
        public static float FacingOf(GameObject model, Transform space, bool byHead)
        {
            Vector3 dir = Vector3.zero;
            Transform head = Find(model.transform, "Head"), front = Find(model.transform, "headfront");
            if (byHead && head != null && front != null) dir = space.InverseTransformVector(front.position - head.position);
            if (new Vector2(dir.x, dir.z).sqrMagnitude < 1e-8f)
            {
                Vector3 right = Vector3.zero;
                foreach (string pair in new[] { "UpLeg", "Shoulder" })
                {
                    Transform l = Find(model.transform, "Left" + pair), r = Find(model.transform, "Right" + pair);
                    if (l != null && r != null) right += space.InverseTransformVector(r.position - l.position);
                }
                right.y = 0f;
                dir = Vector3.Cross(right, Vector3.up);
            }
            return new Vector2(dir.x, dir.z).sqrMagnitude < 1e-8f ? 0f : Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The skinned mesh's height as it was bound to its bones (its rest pose), in the space of
        /// <paramref name="space"/>: from the mesh's box, as the import keeps no vertices to read.
        /// </summary>
        public static float BindHeight(SkinnedMeshRenderer skin, Transform space)
        {
            if (skin == null || skin.sharedMesh == null) return 0f;
            Matrix4x4 toSpace = space.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            Bounds b = skin.sharedMesh.bounds;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                float y = toSpace.MultiplyPoint3x4(corner).y;
                lo = Mathf.Min(lo, y);
                hi = Mathf.Max(hi, y);
            }
            return hi > lo ? hi - lo : 0f;
        }

        /// <summary>The transform under <paramref name="root"/> with that name, or one whose name ends with it as its last part.</summary>
        public static Transform Find(Transform root, string name)
        {
            Transform loose = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
                if (loose == null && EnemyModelSpec.IsTake(t.name, name)) loose = t;
            }
            return loose;
        }

        /// <summary>The longest side of a mesh's box, in its own space.</summary>
        static Vector3 LongAxis(Transform t)
        {
            Mesh mesh = t.TryGetComponent(out MeshFilter f) ? f.sharedMesh : t.TryGetComponent(out SkinnedMeshRenderer s) ? s.sharedMesh : null;
            if (mesh == null) return Vector3.up;
            Vector3 size = mesh.bounds.size;
            return size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        }

        static float UpAngle(Transform t)
        {
            Vector3 world = t.rotation * LongAxis(t);
            float a = Vector3.Angle(world, Vector3.up);
            return Mathf.Min(a, 180f - a);
        }

        /// <summary>
        /// A new material for one mesh of the model (the look destroys it): Simple Lit with the mesh's painted texture,
        /// found beside the model by the mesh's name, else what the import gave it, else the model's own.
        /// </summary>
        static Material MakeMaterial(EnemyModelSpec spec, Renderer r)
        {
            Material imported = r.sharedMaterial;
            Texture texture = Resources.Load<Texture2D>(spec.TexturePath(r.name));
            if (texture == null && r is SkinnedMeshRenderer sk && sk.sharedMesh != null)
                texture = Resources.Load<Texture2D>(spec.TexturePath(sk.sharedMesh.name));
            if (texture == null && imported != null)
                texture = imported.HasProperty("_BaseMap") && imported.GetTexture("_BaseMap") != null ? imported.GetTexture("_BaseMap") : imported.mainTexture;
            if (texture == null) texture = Resources.Load<Texture2D>(spec.TexturePath(spec.Name));

            Shader shader = Shader.Find(PropFactory.ShaderName);
            if (shader == null) shader = imported != null ? imported.shader : Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = MaterialPrefix + r.name };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            m.mainTexture = texture;
            EnemyFactory.Paint(m, Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            // Work order 027: a model with a glow (the caster's eyes, palms and amulet) has its emission map in the game's
            // cyan, as the crypt kit's pieces (CryptKit.MakeMaterial).
            Texture2D emission = spec.Glow > 0 ? Resources.Load<Texture2D>(spec.EmissionPath) : null;
            if (emission != null && m.HasProperty("_EmissionColor"))
            {
                m.SetTexture("_EmissionMap", emission);
                Color lit = CryptKit.Glowing(new CryptKitValues().Cyan, (float)spec.Glow);
                m.SetColor("_EmissionColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? lit.gamma : lit);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        /// <summary>The emission map a material made here shows, or null when it has none (work order 027).</summary>
        public static Texture EmissionOf(Renderer r)
        {
            Material m = r != null ? r.sharedMaterial : null;
            return m != null && m.HasProperty("_EmissionMap") && m.IsKeywordEnabled("_EMISSION") ? m.GetTexture("_EmissionMap") : null;
        }

        /// <summary>The texture a material made here shows, for tests.</summary>
        public static Texture TextureOf(Renderer r)
        {
            Material m = r != null ? r.sharedMaterial : null;
            if (m == null) return null;
            return m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap") : m.mainTexture;
        }
    }
}
