using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// What shows her skills in the world in place of the flat shapes on the ground (work order 123, 2026-11-25; Kristian: "Jeg vil
    /// heller ikke at alle abilities får en cone/telegraf etter man har skutt de, prosjektiler og røyk/flammer skal heller vise
    /// hvordan spellsene utspiller seg"), with <see cref="SkillLookValues.EffectsNotShapes"/> on:
    /// - dust kicked up where each of a cone's tracers ends, and gun smoke at the muzzle after (Scatter Blast, Fan Fire);
    /// - a blast's ring of dust and sparks running out from its middle to exactly its radius, and smoke left inside it after (Powder Keg);
    /// - the keg or the smoke round in the air over the startup, from her hand to exactly where the rule lands it;
    /// - the keg itself while it lies on its fuse, sparks off its fuse, for exactly as long as the rule's keg lies;
    /// - Smoke Round's cloud as puffs of smoke whose outer side lies on its radius, drawn exactly while the rule's cloud stands;
    /// - Slide's dust kicked back where she starts.
    /// Every place and time is the rule's (<see cref="SkillLookRules"/>); nothing here changes what a skill does.
    ///
    /// Made in code, no asset: soft puffs (a lumpy disc whose alpha falls to nothing at its edge, alpha-blended, facing the camera),
    /// small sparks (the shot effects' material, their glow at most 1, under the bloom's threshold: her kit's extras are quiet), and
    /// a small barrel for the keg and a canister for the smoke round. Pooled: the pieces are made the first time one is wanted and
    /// lent out; past <see cref="SkillLookValues.MostPieces"/> live a new one is not drawn. Nothing is made or allocated per frame
    /// once warm, and with nothing live a frame does nothing but look.
    ///
    /// It lives in the scene, made by whatever first wants it (<see cref="Ensure"/>): a cone's or a blast's effects, a keg that lands,
    /// a smoke cloud, a throw seen by the aim outline. With the switch off nothing makes it, and nothing of it is drawn.
    /// </summary>
    public sealed class SkillEffects : MonoBehaviour
    {
        static SkillEffects s_current;

        /// <summary>The one in the scene now, or null.</summary>
        public static SkillEffects Current => s_current != null ? s_current : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_current = null;

        /// <summary>The one in the scene, made if there is none.</summary>
        public static SkillEffects Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("SkillEffects");
            return go.AddComponent<SkillEffects>();
        }

        /// <summary>What a piece is there for.</summary>
        public enum Use { TracerKick, MuzzleSmoke, RingDust, RingSpark, AfterSmoke, FuseSpark, ThrowTrail, Smoke, SlideDust }

        /// <summary>What one piece looks like now, for tests and tools.</summary>
        public struct Shown
        {
            public Use Use;
            public Vector3 Position;   // in the world
            public float Half;         // m: half its width (its mesh reaches this far from its middle)
            public float Age, Life;    // s
            public float Alpha;        // its alpha now (sparks: 1)
            public int Cloud;          // a smoke puff: the place of its cloud in SmokeClouds; -1 otherwise
            public Vector3 Centre;     // a blast's ring: its middle
            public float Reach;        // a blast's ring: how far its middle goes from the blast's
            public bool Drawn;
        }

        sealed class Piece
        {
            public bool Live;
            public Use Use;
            public bool Spark;          // drawn as a spark (opaque), else a puff (blended)
            public int Cloud = -1;
            public float Born, Life;
            public Vector3 From, Velocity, Centre, Out;
            public float Gravity, Reach, Arrive, Height;
            public float Half, Alpha, AlphaSet = -1f, Spin;
            public float SmokeFrom, SmokeUntil;
            public GameObject Go;
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        sealed class Barrel
        {
            public GameObject Go;
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public float NextSpark;
        }

        readonly List<Piece> _pool = new List<Piece>();
        readonly List<Piece> _free = new List<Piece>();
        readonly List<Piece> _live = new List<Piece>();
        readonly List<Barrel> _kegs = new List<Barrel>();
        Barrel _thrown;
        bool _throwing, _throwSmoke;
        Vector3 _throwFrom, _throwAt;
        float _nextTrail;
        readonly bool[] _cloudShown = new bool[SmokeValues.MostClouds];
        readonly float[] _cloudFrom = new float[SmokeValues.MostClouds];
        readonly float[] _cloudX = new float[SmokeValues.MostClouds];
        readonly float[] _cloudZ = new float[SmokeValues.MostClouds];
        PlayerCharacter _player;
        Material _puffMaterial, _ownSpark;
        Mesh[] _puffs;
        Mesh _spark, _keg, _canister;
        MaterialPropertyBlock _block;
        int _colorId;
        GeneratedLevel _level;
        bool _levelKnown;
        int _seed = 1;

        /// <summary>Pieces live now.</summary>
        public int Live => _live.Count;
        /// <summary>The pool's size: every piece made so far.</summary>
        public int Pooled => _pool.Count;
        /// <summary>Objects made since it started (pieces, barrels, meshes): for the test that nothing is made per use once warm.</summary>
        public int Made { get; private set; }
        /// <summary>Pieces asked for and not drawn because <see cref="SkillLookValues.MostPieces"/> were live.</summary>
        public int Capped { get; private set; }
        /// <summary>Pieces drawn since it started, by what for (indexed by <see cref="Use"/>).</summary>
        public readonly int[] DrawnBy = new int[9];
        /// <summary>Kegs drawn lying now.</summary>
        public int KegsShown { get; private set; }
        /// <summary>True while a throw is drawn; where the round is now.</summary>
        public bool ThrowShown => _throwing && _thrown != null && _thrown.Go.activeSelf;
        public Vector3 ThrowAt => _throwAt;
        /// <summary>Where a lying keg is drawn, for tests.</summary>
        public Vector3 KegAt(int i) => i >= 0 && i < _kegs.Count && _kegs[i].Go.activeSelf ? _kegs[i].T.position : Vector3.zero;

        SkillLookValues Values => SkillLookValues.Live;

        void Awake()
        {
            if (s_current != null && s_current != this)
            {
                Destroy(this);
                return;
            }
            s_current = this;
            _block = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID("_BaseColor");
        }

        void OnDestroy()
        {
            if (s_current == this) s_current = null;
            if (_puffs != null) foreach (Mesh m in _puffs) if (m != null) Destroy(m);
            if (_spark != null) Destroy(_spark);
            if (_keg != null) Destroy(_keg);
            if (_canister != null) Destroy(_canister);
            if (_puffMaterial != null) Destroy(_puffMaterial);
            if (_ownSpark != null) Destroy(_ownSpark);
        }

        /// <summary>The player whose skills it shows: the throw, the lying kegs and the smoke are read from her each frame.</summary>
        public SkillEffects Watch(PlayerCharacter player)
        {
            if (player != null) _player = player;
            return this;
        }

        // ---------------- Asked for by her skills (PlayerCharacterSkillLook.cs, CombatEffects) ----------------

        /// <summary>Dust kicked up under where a cone's tracer ends (its reach: the range, or the cover that stopped it).</summary>
        public void TracerEnd(Vector3 end)
        {
            SkillLookValues v = Values;
            Piece p = Take(Use.TracerKick, spark: false, v.KickSeconds, v.KickSize * 0.5f, v.Dust.A);
            if (p == null) return;
            p.From = new Vector3(end.x, v.KickHeight, end.z);
            p.Velocity = Vector3.up * v.KickRise;
            Place(p);
        }

        /// <summary>Gun smoke at the muzzle after a cone's shot: <paramref name="puffs"/> grey puffs drifting up and along it.</summary>
        public void MuzzleSmoke(Vector3 muzzle, Vector3 dir, int puffs)
        {
            SkillLookValues v = Values;
            Vector3 along = new Vector3(dir.x, 0f, dir.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            for (int i = 0; i < puffs; i++)
            {
                Piece p = Take(Use.MuzzleSmoke, spark: false, v.MuzzleSmokeSeconds * (0.8f + 0.4f * Next()), v.MuzzleSmokeSize * 0.5f, v.SmokeAlpha * 1.2f);
                if (p == null) return;
                p.From = muzzle + along * (0.15f + 0.25f * i);
                p.Velocity = (Vector3.up + along * 0.6f + new Vector3(Next() - 0.5f, 0f, Next() - 0.5f) * 0.6f).normalized * v.MuzzleSmokeRise;
                Place(p);
            }
        }

        /// <summary>
        /// A blast of <paramref name="radius"/> at <paramref name="centre"/> over <paramref name="seconds"/>: a ring of dust and sparks
        /// running out from the middle, slowing, until its outer side lies on the radius; and smoke left inside it after.
        /// </summary>
        public void Blast(Vector3 centre, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            SkillLookValues v = Values;
            var c = new Vector3(centre.x, 0f, centre.z);
            float turn = Next() * 360f;
            int dust = Mathf.Max(0, v.RingPuffs), sparks = Mathf.Max(0, v.RingSparks);
            for (int i = 0; i < dust; i++)
            {
                float half = Mathf.Min(v.RingPuffSize * 0.5f, radius * 0.5f);
                Piece p = Take(Use.RingDust, spark: false, seconds, half, v.Dust.A);
                if (p == null) break;
                Ring(p, c, turn + 360f * i / dust, radius - half, v.RingHeight * (0.7f + 0.6f * Next()), v.ArriveShare);
            }
            for (int i = 0; i < sparks; i++)
            {
                float half = Mathf.Min(v.RingSparkSize * 0.5f, radius * 0.5f);
                Piece p = Take(Use.RingSpark, spark: true, seconds, half, 1f);
                if (p == null) break;
                Ring(p, c, turn + 360f * (i + 0.5f) / sparks, radius - half, v.RingHeight * (0.8f + 0.8f * Next()), v.ArriveShare * 0.85f);
            }
            float smokeHalf = Mathf.Min(v.AfterSmokeSize * 0.5f, radius * v.AfterSmokeReach * 0.5f);
            float within = Mathf.Max(0f, radius * v.AfterSmokeReach - smokeHalf);
            for (int i = 0; i < v.AfterSmokePuffs; i++)
            {
                Piece p = Take(Use.AfterSmoke, spark: false, v.AfterSmokeSeconds * (0.85f + 0.3f * Next()), smokeHalf, v.SmokeAlpha * 1.3f);
                if (p == null) break;
                float a = Next() * Mathf.PI * 2f, r = within * Mathf.Sqrt(Next());
                p.From = c + new Vector3(Mathf.Cos(a) * r, 0.4f + 0.6f * Next(), Mathf.Sin(a) * r);
                p.Velocity = Vector3.up * v.MuzzleSmokeRise * 0.5f; // straight up: it never leaves the circle
                Place(p);
            }
        }

        /// <summary>Slide's dust, kicked back from where she starts along <paramref name="back"/>.</summary>
        public void SlideDust(Vector3 from, Vector3 back)
        {
            SkillLookValues v = Values;
            Vector3 b = new Vector3(back.x, 0f, back.z);
            b = b.sqrMagnitude > 1e-6f ? b.normalized : Vector3.back;
            int n = Mathf.Max(0, v.SlideDustPuffs);
            for (int i = 0; i < n; i++)
            {
                Piece p = Take(Use.SlideDust, spark: false, v.SlideDustSeconds, v.SlideDustSize * 0.5f, v.Dust.A);
                if (p == null) return;
                float yaw = n > 1 ? Mathf.Lerp(-v.SlideDustSpread * 0.5f, v.SlideDustSpread * 0.5f, i / (n - 1f)) : 0f;
                p.From = new Vector3(from.x, 0.2f, from.z);
                p.Velocity = Quaternion.Euler(0f, yaw, 0f) * b * v.SlideDustSpeed + Vector3.up * 0.4f;
                Place(p);
            }
        }

        // ---------------- Every frame ----------------

        void Update() => Step();

        /// <summary>The frame's work: what is read from her and the clouds, and every live piece moved. Public for the allocation test.</summary>
        public void Step()
        {
            float now = Time.time;
            GeneratedLevels levels = GeneratedLevels.Current;
            GeneratedLevel level = levels != null ? levels.Level : null;
            // Gone with the floor it was drawn on (by reference: a destroyed floor is not "no floor"), as what flies is.
            if (_levelKnown && !ReferenceEquals(level, _level)) ClearAll();
            _level = level;
            _levelKnown = true;
            bool on = SkillLookValues.EffectsNotShapes && _player != null;
            Camera cam = Camera.main;
            ThrowNow(on, now, cam);
            KegsNow(on, now, cam);
            CloudsNow(SkillLookValues.EffectsNotShapes, now); // the clouds are read from SmokeClouds, not from her
            for (int i = _live.Count - 1; i >= 0; i--) Tick(_live[i], i, now, cam);
        }

        void ThrowNow(bool on, float now, Camera cam)
        {
            SkillDefinition s = on ? _player.ActiveSkill : null;
            bool throwing = s != null && s.Input == SkillInput.Point && s.Shape == SkillShape.Circle && _player.SkillPhase == SkillPhase.Startup;
            if (!throwing)
            {
                if (_thrown != null && _thrown.Go.activeSelf) _thrown.Go.SetActive(false);
                _throwing = false;
                return;
            }
            if (!_throwing)
            {
                _throwing = true;
                _throwFrom = _player.ThrowHand;
                _throwSmoke = s.SmokeSeconds > 0f;
                _nextTrail = now;
                if (_thrown == null) _thrown = MakeBarrel("SkillEffects_Thrown");
                _thrown.Filter.sharedMesh = _throwSmoke ? _canister : _keg;
                _thrown.Renderer.sharedMaterial = SparkMaterial;
            }
            // Where the rule lands it: the circle's centre as it is placed at the hit (short of cover in its way), from where she
            // stands now and the point it was pressed at.
            GroundPoint c = CoverRules.CircleCenter(s, _player.GroundPosition, new GroundPoint(_player.AimPoint.x, _player.AimPoint.z), _player.CoverQuery);
            float k = Mathf.Clamp01(_player.SkillProgress);
            var to = new Vector3(c.X, 0f, c.Z);
            _throwAt = Vector3.Lerp(_throwFrom, to, k) + Vector3.up * SkillLookRules.ThrowLift(k, Values.ThrowPeak);
            _thrown.T.SetPositionAndRotation(_throwAt, Quaternion.Euler(k * 300f, k * 140f, 0f));
            if (!_thrown.Go.activeSelf) _thrown.Go.SetActive(true);
            if (now < _nextTrail) return;
            _nextTrail = now + Mathf.Max(0.005f, Values.ThrowTrailEvery);
            SkillLookValues v = Values;
            if (_throwSmoke)
            {
                Piece p = Take(Use.ThrowTrail, spark: false, v.MuzzleSmokeSeconds * 0.6f, v.MuzzleSmokeSize * 0.3f, v.SmokeAlpha);
                if (p == null) return;
                p.From = _throwAt;
                p.Velocity = Vector3.up * 0.3f;
                Place(p);
            }
            else
            {
                Piece p = Take(Use.ThrowTrail, spark: true, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f, 1f);
                if (p == null) return;
                p.From = _throwAt + Vector3.up * v.KegHeight * 0.5f;
                p.Velocity = new Vector3(Next() - 0.5f, 1f, Next() - 0.5f) * 0.8f;
                p.Gravity = 3f;
                Place(p);
            }
        }

        void KegsNow(bool on, float now, Camera cam)
        {
            int n = on ? _player.KegsLying : 0;
            while (_kegs.Count < n) _kegs.Add(MakeBarrel("SkillEffects_Keg_" + (_kegs.Count + 1)));
            KegsShown = 0;
            SkillLookValues v = Values;
            for (int i = 0; i < _kegs.Count; i++)
            {
                Barrel b = _kegs[i];
                if (i >= n || !_player.LyingKegAt(i, out Vector3 centre, out float burstAt, out _))
                {
                    if (b.Go.activeSelf) b.Go.SetActive(false);
                    continue;
                }
                if (!b.Go.activeSelf)
                {
                    b.Filter.sharedMesh = _keg;
                    b.Renderer.sharedMaterial = SparkMaterial;
                    b.Go.SetActive(true);
                    b.NextSpark = now;
                }
                b.T.SetPositionAndRotation(new Vector3(centre.x, 0f, centre.z), Quaternion.Euler(0f, (centre.x * 37f + centre.z * 53f) % 360f, 0f));
                KegsShown++;
                if (now < b.NextSpark || now >= burstAt) continue;
                b.NextSpark = now + Mathf.Max(0.01f, v.FuseEvery);
                Piece p = Take(Use.FuseSpark, spark: true, v.FuseSparkSeconds, v.FuseSparkSize * 0.5f, 1f);
                if (p == null) continue;
                p.From = new Vector3(centre.x, v.KegHeight + 0.08f, centre.z);
                p.Velocity = new Vector3((Next() - 0.5f) * 0.8f, v.FuseSparkRise, (Next() - 0.5f) * 0.8f);
                p.Gravity = 2.5f;
                Place(p);
            }
        }

        void CloudsNow(bool on, float now)
        {
            int count = SmokeClouds.Count;
            for (int i = 0; i < _cloudShown.Length; i++)
            {
                SmokeCloud cloud = i < count ? SmokeClouds.At(i) : default;
                bool up = on && i < count && cloud.IsUp(now) && SmokeClouds.ViewAt(i) != null;
                if (!up)
                {
                    if (_cloudShown[i]) ClearCloud(i);
                    continue;
                }
                if (_cloudShown[i] && _cloudFrom[i] == cloud.From && _cloudX[i] == cloud.Centre.X && _cloudZ[i] == cloud.Centre.Z) continue;
                if (_cloudShown[i]) ClearCloud(i);
                ShowCloud(i, cloud);
            }
        }

        void ShowCloud(int place, SmokeCloud cloud)
        {
            SkillLookValues v = Values;
            _cloudShown[place] = true;
            _cloudFrom[place] = cloud.From;
            _cloudX[place] = cloud.Centre.X;
            _cloudZ[place] = cloud.Centre.Z;
            int n = Mathf.Max(1, v.SmokePuffs);
            float half = Mathf.Max(0.05f, v.SmokePuffShare * cloud.Radius * 0.5f);
            for (int i = 0; i < n; i++)
            {
                Piece p = Take(Use.Smoke, spark: false, Mathf.Max(1e-3f, cloud.Until - cloud.From), half, v.SmokeAlpha);
                if (p == null) return;
                GroundPoint at = SkillLookRules.SmokePuffAt(i, n, v.SmokeEdgePuffs, cloud.Radius, half);
                float h = Mathf.Lerp(v.SmokeLowest, v.SmokeHighest, ((i * 7) % n) / (float)n);
                p.Born = cloud.From;
                p.Cloud = place;
                p.SmokeFrom = cloud.From;
                p.SmokeUntil = cloud.Until;
                p.From = new Vector3(cloud.Centre.X + at.X, h, cloud.Centre.Z + at.Z);
                p.Velocity = Vector3.up * v.SmokeRise;
                p.Spin = (i % 2 == 0 ? 1f : -1f) * v.SmokeSpin;
                Place(p);
            }
        }

        void ClearCloud(int place)
        {
            _cloudShown[place] = false;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].Cloud == place) Return(_live[i], i);
        }

        /// <summary>Everything drawn now is gone at once (a new floor; tests).</summary>
        public void ClearAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Return(_live[i], i);
            for (int i = 0; i < _cloudShown.Length; i++) _cloudShown[i] = false;
            foreach (Barrel b in _kegs) if (b.Go.activeSelf) b.Go.SetActive(false);
            if (_thrown != null && _thrown.Go.activeSelf) _thrown.Go.SetActive(false);
            _throwing = false;
            KegsShown = 0;
        }

        void Tick(Piece p, int index, float now, Camera cam)
        {
            float age = now - p.Born;
            float t = age / Mathf.Max(1e-4f, p.Life);
            if (t >= 1f || (p.Use == Use.Smoke && now >= p.SmokeUntil))
            {
                Return(p, index);
                return;
            }
            Pose(p, age, t, now, cam);
        }

        void Pose(Piece p, float age, float t, float now, Camera cam)
        {
            Vector3 at;
            float size = 1f, alpha = p.Alpha;
            switch (p.Use)
            {
                case Use.RingDust:
                case Use.RingSpark:
                {
                    at = p.Centre + p.Out * SkillLookRules.RingReach(p.Reach, t, p.Arrive) + Vector3.up * p.Height;
                    float after = t <= p.Arrive ? 0f : (t - p.Arrive) / Mathf.Max(1e-3f, 1f - p.Arrive);
                    size = p.Spark ? (t < 0.15f ? Mathf.Lerp(0.6f, 1f, t / 0.15f) : 1f - after) : Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t / Mathf.Max(1e-3f, p.Arrive))) * (1f - 0.5f * after);
                    alpha = p.Alpha * (1f - after);
                    break;
                }
                case Use.Smoke:
                    at = p.From + p.Velocity * Mathf.Max(0f, age);
                    alpha = p.Alpha * SkillLookRules.SmokeShare(now, p.SmokeFrom, p.SmokeUntil, Values.SmokeFadeIn, Values.SmokeFadeOut);
                    break;
                default:
                    at = p.From + p.Velocity * age + Vector3.down * (0.5f * p.Gravity * age * age);
                    if (p.Spark) size = t < 0.2f ? Mathf.Lerp(0.6f, 1.1f, t / 0.2f) : 1.1f * (1f - (t - 0.2f) / 0.8f);
                    else
                    {
                        size = Mathf.Lerp(0.6f, 1f, 1f - (1f - t) * (1f - t));
                        alpha = p.Alpha * (1f - t) * (1f - t * 0.5f);
                    }
                    break;
            }
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
            p.T.SetPositionAndRotation(at, p.Spin != 0f ? face * Quaternion.Euler(0f, 0f, p.Spin * age) : face);
            p.T.localScale = Vector3.one * Mathf.Max(1e-3f, p.Half * Mathf.Max(0f, size));
            if (!p.Spark && Mathf.Abs(alpha - p.AlphaSet) > 0.004f)
            {
                p.AlphaSet = alpha;
                Color tint = p.Use == Use.Smoke || p.Use == Use.MuzzleSmoke || p.Use == Use.AfterSmoke || (p.Use == Use.ThrowTrail && _throwSmoke)
                    ? Picked(Values.Smoke) : Picked(Values.Dust);
                tint.a = Mathf.Clamp01(alpha);
                _block.SetColor(_colorId, tint);
                p.Renderer.SetPropertyBlock(_block);
            }
        }

        // ---------------- The pool ----------------

        Piece Take(Use use, bool spark, float life, float half, float alpha)
        {
            if (!SkillLookValues.EffectsNotShapes || life <= 0f || half <= 0f) return null;
            if (_live.Count >= Mathf.Max(1, Values.MostPieces))
            {
                Capped++;
                return null;
            }
            EnsureLooks();
            if (_free.Count == 0) Grow(_pool.Count == 0 ? Mathf.Max(1, Values.PoolStart) : Mathf.Max(1, Values.PoolGrow));
            Piece p = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            p.Live = true;
            p.Use = use;
            p.Spark = spark;
            p.Cloud = -1;
            p.Born = Time.time;
            p.Life = life;
            p.Half = half;
            p.Alpha = alpha;
            p.AlphaSet = -1f;
            p.Spin = 0f;
            p.Gravity = 0f;
            p.Velocity = Vector3.zero;
            p.SmokeUntil = float.PositiveInfinity;
            p.Filter.sharedMesh = spark ? _spark : _puffs[(Pooled + _live.Count + (int)use) % _puffs.Length];
            p.Renderer.sharedMaterial = spark ? SparkMaterial : _puffMaterial;
            if (spark) p.Renderer.SetPropertyBlock(null);
            DrawnBy[(int)use]++;
            return p;
        }

        void Ring(Piece p, Vector3 centre, float degrees, float reach, float height, float arrive)
        {
            float a = degrees * Mathf.Deg2Rad;
            p.Centre = centre;
            p.Out = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            p.Reach = Mathf.Max(0f, reach);
            p.Height = height;
            p.Arrive = arrive;
            Place(p);
        }

        void Place(Piece p)
        {
            _live.Add(p);
            Pose(p, 0f, 0f, Time.time, Camera.main);
            p.Go.SetActive(true);
        }

        void Return(Piece p, int index)
        {
            p.Live = false;
            p.Cloud = -1;
            p.Go.SetActive(false);
            int last = _live.Count - 1;
            _live[index] = _live[last];
            _live.RemoveAt(last);
            _free.Add(p);
        }

        void Grow(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var p = new Piece();
                p.Go = new GameObject("SkillEffects_" + (_pool.Count + 1));
                p.T = p.Go.transform;
                p.T.SetParent(transform, false);
                p.Filter = p.Go.AddComponent<MeshFilter>();
                p.Renderer = p.Go.AddComponent<MeshRenderer>();
                p.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                p.Renderer.receiveShadows = false;
                p.Go.SetActive(false);
                Made++;
                _pool.Add(p);
                _free.Add(p);
            }
        }

        Barrel MakeBarrel(string name)
        {
            EnsureLooks();
            var b = new Barrel();
            b.Go = new GameObject(name);
            b.T = b.Go.transform;
            b.T.SetParent(transform, false);
            b.Filter = b.Go.AddComponent<MeshFilter>();
            b.Filter.sharedMesh = _keg;
            b.Renderer = b.Go.AddComponent<MeshRenderer>();
            b.Renderer.sharedMaterial = SparkMaterial;
            b.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            b.Renderer.receiveShadows = false;
            b.Go.SetActive(false);
            Made++;
            return b;
        }

        /// <summary>What every piece is drawn with, made the first time one is wanted.</summary>
        void EnsureLooks()
        {
            if (_puffs != null) return;
            SkillLookValues v = Values;
            _puffs = new[] { BuildPuff(0), BuildPuff(1), BuildPuff(2) };
            _spark = BuildSpark(Drawn(v.SparkRim, v.SparkGlow), Drawn(v.SparkCore, v.SparkGlow));
            _keg = BuildBarrel("SkillEffects_KegMesh", v.KegRadius, v.KegHeight, Drawn(v.KegWood, 1f), Drawn(v.KegHoop, 1f));
            _canister = BuildBarrel("SkillEffects_RoundMesh", v.KegRadius * 0.45f, v.KegHeight * 0.55f, Drawn(v.RoundShell, 1f), Drawn(v.KegHoop, 1f));
            Made += 6;
            Shader shader = Shader.Find(MoodShapes.UnlitShaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            // The puffs: unlit, blended by alpha, no depth written, both sides (as the smoke cloud's discs were, work order 109).
            _puffMaterial = new Material(shader) { name = "SkillEffects_Puff" };
            if (_puffMaterial.HasProperty("_BaseColor")) _puffMaterial.SetColor("_BaseColor", Color.white);
            _puffMaterial.SetFloat("_Surface", 1f);
            _puffMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _puffMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _puffMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            _puffMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            _puffMaterial.SetFloat("_ZWrite", 0f);
            _puffMaterial.SetFloat("_Cull", (float)CullMode.Off);
            _puffMaterial.renderQueue = (int)RenderQueue.Transparent;
            // Its pictures of 2026-11-25 drew the puffs solid with the floats alone: the shader blends by the keyword, as the
            // material inspector sets it, with no depth pass and the transparent tag.
            _puffMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _puffMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _puffMaterial.DisableKeyword("_ALPHAMODULATE_ON");
            _puffMaterial.SetOverrideTag("RenderType", "Transparent");
            _puffMaterial.SetShaderPassEnabled("DepthOnly", false);
            _puffMaterial.SetShaderPassEnabled("SHADOWCASTER", false);
            _ownSpark = new Material(shader) { name = "SkillEffects_Spark" };
        }

        /// <summary>The sparks and the barrels: the shot effects' material (unlit, its vertices' colours), as every shot effect is drawn; one of its own without her.</summary>
        Material SparkMaterial => _player != null && _player.ShotFxMaterial != null ? _player.ShotFxMaterial : _ownSpark;

        // ---------------- For tests and tools ----------------

        /// <summary>The pieces live now, into a list the caller owns.</summary>
        public void ListPieces(List<Shown> into)
        {
            into.Clear();
            foreach (Piece p in _live)
                into.Add(new Shown
                {
                    Use = p.Use, Position = p.T.position, Half = p.T.localScale.x, Age = Time.time - p.Born, Life = p.Life,
                    Alpha = p.Spark ? 1f : p.AlphaSet, Cloud = p.Cloud, Centre = p.Centre, Reach = p.Reach,
                    Drawn = p.Go.activeSelf && p.Renderer.enabled,
                });
        }

        // ---------------- Meshes, made once ----------------

        /// <summary>The share of a puff's radius that is fully its colour; past it the alpha falls to nothing at its edge.</summary>
        const float PuffSolid = 0.55f;

        static Color Picked(Rgba c) => new Color(c.R, c.G, c.B, c.A);

        static Color Drawn(Rgba picked, float glow)
        {
            Color p = Picked(picked);
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        float Next()
        {
            // A small fixed stream: the same pattern each run, no allocation, not Unity's own random (the game's draws stay as they were).
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            return (_seed % 10007) / 10007f;
        }

        /// <summary>
        /// A soft, lumpy puff: a disc of radius at most 1 in the local XY plane, white, its alpha full out to <see cref="PuffSolid"/> of
        /// its radius and falling to none at its edge.
        /// </summary>
        static Mesh BuildPuff(int variant)
        {
            const int edge = 18;
            var vertices = new Vector3[1 + edge * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[edge * 18];
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;
            int t = 0;
            for (int i = 0; i < edge; i++)
            {
                float a = i * Mathf.PI * 2f / edge;
                // Lumps no farther out than 1: the puff never reaches past the half it is scaled to.
                float r = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(a * (3 + variant) + variant * 1.7f));
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[1 + i] = d * (r * PuffSolid);
                colors[1 + i] = Color.white;
                vertices[1 + edge + i] = d * r;
                colors[1 + edge + i] = new Color(1f, 1f, 1f, 0f);
                int i0 = 1 + i, i1 = 1 + (i + 1) % edge, o0 = i0 + edge, o1 = i1 + edge;
                foreach (int k in new[] { 0, i0, i1, 0, i1, i0, i0, o0, o1, i0, o1, i1, i0, o1, o0, i0, i1, o1 }) triangles[t++] = k;
            }
            var mesh = new Mesh { name = "SkillEffects_Puff" + variant };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A small four-pointed star of radius 1, a rim and a core over it, both sides.</summary>
        static Mesh BuildSpark(Color rim, Color core)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                float k = layer == 0 ? 1f : 0.5f;
                float z = layer == 0 ? 0f : -0.01f;
                Color col = layer == 0 ? rim : core;
                int b = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                c.Add(col);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float r = (i % 2 == 0 ? 1f : 0.35f) * k;
                    v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
                    c.Add(col);
                    t.Add(b); t.Add(b + 1 + i); t.Add(b + 1 + (i + 1) % 8);
                    t.Add(b); t.Add(b + 1 + (i + 1) % 8); t.Add(b + 1 + i);
                }
            }
            var mesh = new Mesh { name = "SkillEffects_Spark" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A small barrel standing on its foot at the origin: ten staves, a little wider at its middle, two dark hoops, a lid. Unlit,
        /// so its sides are shaded in its colours (lighter toward the room's usual key light, from the upper left).
        /// </summary>
        static Mesh BuildBarrel(string name, float radius, float height, Color wood, Color hoop)
        {
            const int sides = 10;
            float[] heights = { 0f, 0.14f, 0.2f, 0.5f, 0.8f, 0.86f, 1f };
            float[] widths = { 0.9f, 0.96f, 0.98f, 1.06f, 0.98f, 0.96f, 0.9f };
            bool[] hoops = { true, true, false, false, true, true, false };
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            Vector3 light = new Vector3(-0.5f, 0.4f, 0.75f).normalized;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 normal = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                float shade = 0.6f + 0.4f * Mathf.Max(0f, Vector3.Dot(normal, light));
                for (int h = 0; h + 1 < heights.Length; h++)
                {
                    Color col = (hoops[h] ? hoop : wood) * shade;
                    col.a = 1f;
                    int b = v.Count;
                    float y0 = heights[h] * height, y1 = heights[h + 1] * height, r0 = widths[h] * radius, r1 = widths[h + 1] * radius;
                    v.Add(new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0));
                    v.Add(new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0));
                    v.Add(new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1));
                    v.Add(new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1));
                    c.Add(col); c.Add(col); c.Add(col); c.Add(col);
                    t.Add(b); t.Add(b + 2); t.Add(b + 1);
                    t.Add(b); t.Add(b + 3); t.Add(b + 2);
                }
            }
            // The lid: lighter wood, its end grain.
            int centre = v.Count;
            Color lid = wood * 1.15f;
            lid.a = 1f;
            v.Add(new Vector3(0f, height, 0f));
            c.Add(lid);
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Cos(a) * radius * 0.9f, height, Mathf.Sin(a) * radius * 0.9f));
                c.Add(lid);
            }
            for (int s = 0; s < sides; s++)
            {
                t.Add(centre); t.Add(centre + 1 + (s + 1) % sides); t.Add(centre + 1 + s);
            }
            // Both sides, as every shot effect's mesh is: it is seen right whichever way the material culls.
            int n = t.Count;
            for (int i = 0; i < n; i += 3)
            {
                t.Add(t[i]);
                t.Add(t[i + 2]);
                t.Add(t[i + 1]);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
