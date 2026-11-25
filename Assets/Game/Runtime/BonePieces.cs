using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The loose pieces of bone (work order 122): rigid bodies of Unity's physics, with gravity, a mass by their size, and bone's
    /// friction and bounce on stone, that fly, tumble, bounce off the floor, the walls and the props and off each other, come to
    /// rest, lie a while and sink away. They are on Unity's Ignore Raycast layer (2), which no physics query of the game asks with
    /// its default layers, so no shot, sight line or cover ray ever meets one; and neither she nor any enemy has a collider (they
    /// walk on the NavMesh), so a piece can never push either, and the NavMesh is built before any piece exists. Nothing in the
    /// fight reads them.
    ///
    /// Pooled and capped (<see cref="BoneBreakValues.MostPieces"/>), every slot made as the first skeleton appears (<see cref="Prewarm"/>): a piece takes a free slot, else a new one up to the cap, else
    /// the oldest (<see cref="BoneBreakRules.Slot"/>). Each made once and kept; while no piece lives the pool does not run at all
    /// (its Update is off), so nothing is made or done in a frame while nothing breaks. A piece wears the material its enemy wears
    /// at that moment (the renderer's own, every slot of it), so it follows whatever look the enemies have. It lives in the scene,
    /// not under a level or an enemy, and goes with the scene; the pieces of an enemy taken out of the game go with it at once.
    /// </summary>
    public sealed class BonePieces : MonoBehaviour
    {
        /// <summary>Unity's Ignore Raycast layer: no query of the game with its default layers sees it (as the crypt kit's, <see cref="CryptKit.Layer"/>).</summary>
        public const int PieceLayer = 2;
        /// <summary>Most landings remembered between two frames for breaking again; more in one frame wait for none.</summary>
        const int MostLandings = 64;
        /// <summary>Share of the most tumble a half gets as a kick of its own as it breaks off.</summary>
        const double HalfKick = 0.3;

        static BonePieces s_current;

        /// <summary>The one in the scene now, or null.</summary>
        public static BonePieces Current => s_current != null ? s_current : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_current = null;

        /// <summary>The one in the scene, made if there is none.</summary>
        public static BonePieces Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("BonePieces");
            return go.AddComponent<BonePieces>();
        }

        sealed class Slot
        {
            public GameObject Go;
            public Transform T;
            public MeshFilter F;
            public MeshRenderer R;
            public BoxCollider C;
            public Rigidbody B;
            public float Age, Still, Since, SinkDepth;
            public Vector3 SinkFrom;
            public bool Whole;
            public BoneBreakModel.Shape Shape;
            public BoneBreak Source;
        }

        Slot[] _slots = new Slot[0];
        BonePieceState[] _states = new BonePieceState[0];
        double[] _born = new double[0];
        int _made;
        PhysicsMaterial _stone;
        readonly List<Material> _materials = new List<Material>();
        readonly int[] _landed = new int[MostLandings];
        int _landings;
        BoneBreakRandom _random = new BoneBreakRandom(122);

        /// <summary>For tests and the report: pieces living now, the most at once since made, pieces made into slots, pieces thrown, pieces broken again, pieces taken from the oldest.</summary>
        public int Live { get; private set; }
        public int MostLive { get; private set; }
        public int Made => _made;
        public int Thrown { get; private set; }
        public int BrokenAgain { get; private set; }
        public int TakenFromTheOldest { get; private set; }

        /// <summary>Starts counting the most at once again (the benchmark's measure).</summary>
        public void ResetMost() => MostLive = Live;

        // ---------------- For tests ----------------

        public BonePieceState StateOf(int i) => i >= 0 && i < _made ? _states[i] : BonePieceState.Free;
        public Rigidbody BodyOf(int i) => i >= 0 && i < _made ? _slots[i].B : null;
        public Renderer RendererOf(int i) => i >= 0 && i < _made ? _slots[i].R : null;
        public BoneBreak SourceOf(int i) => i >= 0 && i < _made ? _slots[i].Source : null;
        public bool IsWhole(int i) => i >= 0 && i < _made && _slots[i].Whole;

        void Awake()
        {
            s_current = this;
            BoneBreakValues v = BoneBreakValues.Live;
            _stone = new PhysicsMaterial("BoneOnStone")
            {
                dynamicFriction = (float)v.DynamicFriction, staticFriction = (float)v.StaticFriction, bounciness = (float)v.Bounce,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Average,
            };
            enabled = false;
        }

        void OnDestroy()
        {
            if (s_current == this) s_current = null;
            if (_stone != null) Destroy(_stone);
        }

        /// <summary>
        /// Throws one piece: <paramref name="mesh"/> at this place, turn and scale, its box <paramref name="centre"/> and
        /// <paramref name="size"/> in its own frame, wearing <paramref name="materials"/> as <paramref name="look"/> wears them, leaving
        /// at <paramref name="velocity"/> and tumbling at <paramref name="spin"/>. <paramref name="shape"/> and <paramref name="whole"/>
        /// tell whether it may break again. Returns its slot.
        /// </summary>
        public int Throw(Mesh mesh, List<Material> materials, Renderer look, Vector3 position, Quaternion turn, Vector3 scale,
                         Vector3 centre, Vector3 size, Vector3 velocity, Vector3 spin, BoneBreakModel.Shape shape, bool whole, BoneBreak source)
        {
            if (mesh == null) return -1;
            BoneBreakValues v = BoneBreakValues.Live;
            int i = BoneBreakRules.Slot(_states, _born, _made, v);
            if (i == _made)
            {
                Grow(Mathf.Max(Mathf.Max(1, v.MostPieces), _made + 1));
                Make(_made++);
            }
            else if (_states[i] != BonePieceState.Free)
            {
                TakenFromTheOldest++;
                Free(i);
            }
            Slot s = _slots[i];
            s.F.sharedMesh = mesh;
            s.R.SetSharedMaterials(materials);
            if (look != null)
            {
                s.R.shadowCastingMode = look.shadowCastingMode;
                s.R.renderingLayerMask = look.renderingLayerMask;
            }
            s.C.center = centre;
            s.C.size = Vector3.Max(size, Vector3.one * 0.01f);
            s.C.enabled = true;
            s.T.SetPositionAndRotation(position, turn);
            s.T.localScale = scale;
            s.Go.SetActive(true);
            s.B.isKinematic = false;
            s.B.interpolation = RigidbodyInterpolation.Interpolate;
            s.B.position = position;
            s.B.rotation = turn;
            Vector3 shown = Vector3.Scale(size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            s.B.mass = (float)BoneBreakRules.Mass(shown.x, shown.y, shown.z, v);
            s.B.ResetCenterOfMass();
            s.B.ResetInertiaTensor();
            s.B.linearVelocity = velocity;
            s.B.angularVelocity = spin;
            s.B.WakeUp();
            s.Age = s.Still = s.Since = 0f;
            s.Whole = whole;
            s.Shape = shape;
            s.Source = source;
            _states[i] = BonePieceState.Flying;
            _born[i] = Time.timeAsDouble + i * 1e-9;
            Live++;
            if (Live > MostLive) MostLive = Live;
            Thrown++;
            enabled = true;
            return i;
        }

        /// <summary>
        /// Every slot made now, up to the cap (as a skeleton appears, while a floor is put together): so that no piece is made in a
        /// fight, and what a death makes is the same every time.
        /// </summary>
        public void Prewarm()
        {
            int most = Mathf.Max(1, BoneBreakValues.Live.MostPieces);
            Grow(most);
            while (_made < most) Make(_made++);
        }

        /// <summary>The pieces of an enemy taken out of the game go with it at once (they wear its materials, which go with it).</summary>
        public void Forget(BoneBreak source)
        {
            for (int i = 0; i < _made; i++)
                if (_states[i] != BonePieceState.Free && _slots[i].Source == source) Free(i);
        }

        /// <summary>Told by a piece as it lands: a whole piece that lands hard enough breaks again in two, in the next Update.</summary>
        internal void Landed(int i, float impact)
        {
            if (i < 0 || i >= _made || _states[i] != BonePieceState.Flying) return;
            Slot s = _slots[i];
            if (!BoneBreakRules.Shatters(s.Whole, s.Shape != null && s.Shape.Halves != null, impact, BoneBreakValues.Live)) return;
            if (_landings >= MostLandings) return;
            s.Whole = false; // once
            _landed[_landings++] = i;
            enabled = true;
        }

        void Update() => Tick();

        /// <summary>A frame of the pieces (its Update; public for the test of what a frame allocates): each piece's life moved on.</summary>
        public void Tick()
        {
            if (Live == 0 && _landings == 0)
            {
                enabled = false;
                return;
            }
            BoneBreakValues v = BoneBreakValues.Live;
            if (_landings > 0) BreakAgain(v);
            float dt = Time.deltaTime;
            for (int i = 0; i < _made; i++)
            {
                BonePieceState state = _states[i];
                if (state == BonePieceState.Free) continue;
                Slot s = _slots[i];
                s.Age += dt;
                s.Since += dt;
                if (state == BonePieceState.Flying)
                {
                    bool still = s.B.IsSleeping() || BoneBreakRules.Still(s.B.linearVelocity.magnitude, s.B.angularVelocity.magnitude, v);
                    s.Still = still ? s.Still + dt : 0f;
                }
                else if (state == BonePieceState.Sinking)
                {
                    float k = v.SinkSeconds > 0 ? Mathf.Clamp01(s.Since / (float)v.SinkSeconds) : 1f;
                    s.T.position = s.SinkFrom + Vector3.down * (s.SinkDepth * k);
                }
                BonePieceState next = BoneBreakRules.Next(state, s.Age, s.Still, s.Since, s.T.position.y, v);
                if (next == state) continue;
                s.Since = 0f;
                if (next == BonePieceState.Free)
                {
                    Free(i);
                    continue;
                }
                _states[i] = next;
                if (next == BonePieceState.Sinking)
                {
                    s.B.interpolation = RigidbodyInterpolation.None;
                    s.B.isKinematic = true;
                    s.C.enabled = false;
                    s.SinkFrom = s.T.position;
                    s.SinkDepth = Mathf.Max((float)v.SinkLeast, s.R.bounds.size.y * (float)v.SinkDepthShare);
                }
            }
        }

        /// <summary>Each piece that landed hard enough is put away and its two halves thrown from where it was, parting along the cut.</summary>
        void BreakAgain(BoneBreakValues v)
        {
            int n = _landings;
            _landings = 0;
            for (int k = 0; k < n; k++)
            {
                int i = _landed[k];
                if (_states[i] != BonePieceState.Flying) continue;
                Slot s = _slots[i];
                BoneBreakModel.Shape shape = s.Shape;
                if (shape == null || shape.Halves == null) continue;
                Vector3 position = s.T.position, scale = s.T.localScale;
                Quaternion turn = s.T.rotation;
                Vector3 velocity = s.B.linearVelocity, spin = s.B.angularVelocity;
                BoneBreak source = s.Source;
                Renderer look = s.R; // its renderer keeps its settings when put away: the halves take them from it
                s.R.GetSharedMaterials(_materials);
                Free(i);
                Vector3 apart = turn * shape.CutAxis * (float)v.ShatterApart;
                for (int h = 0; h < 2; h++)
                {
                    Vector3 kick = new Vector3((float)_random.Signed(), (float)_random.Signed(), (float)_random.Signed()) * (float)(v.MostSpin * HalfKick);
                    Throw(shape.Halves[h], _materials, look, position, turn, scale, shape.HalfCentre[h], shape.HalfSize[h],
                          velocity + (h == 0 ? -apart : apart), spin + kick, shape, false, source);
                }
                BrokenAgain++;
            }
        }

        void Free(int i)
        {
            if (_states[i] == BonePieceState.Free) return;
            Slot s = _slots[i];
            _states[i] = BonePieceState.Free;
            s.Source = null;
            s.Shape = null;
            if (s.Go != null) s.Go.SetActive(false);
            Live--;
        }

        void Grow(int most)
        {
            if (_slots.Length >= most) return;
            System.Array.Resize(ref _slots, most);
            System.Array.Resize(ref _states, most);
            System.Array.Resize(ref _born, most);
        }

        void Make(int i)
        {
            BoneBreakValues v = BoneBreakValues.Live;
            var go = new GameObject("BonePiece") { layer = PieceLayer };
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var s = new Slot { Go = go, T = go.transform };
            s.F = go.AddComponent<MeshFilter>();
            s.R = go.AddComponent<MeshRenderer>();
            s.C = go.AddComponent<BoxCollider>();
            s.C.sharedMaterial = _stone;
            s.B = go.AddComponent<Rigidbody>();
            s.B.useGravity = true;
            s.B.linearDamping = (float)v.Drag;
            s.B.angularDamping = (float)v.AngularDrag;
            s.B.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            s.B.interpolation = RigidbodyInterpolation.Interpolate;
            var piece = go.AddComponent<BonePiece>();
            piece.Owner = this;
            piece.Index = i;
            _slots[i] = s;
            _states[i] = BonePieceState.Free;
        }
    }
}
