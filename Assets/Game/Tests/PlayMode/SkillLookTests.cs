using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 123 (2026-11-25; Kristian: "Jeg vil heller ikke at alle abilities får en cone/telegraf etter man har skutt de,
    /// prosjektiler og røyk/flammer skal heller vise hvordan spellsene utspiller seg"), in the ControlRoom scene through the intents,
    /// every one of her fifteen skills played the way its key is played:
    /// - with <see cref="SkillLookValues.EffectsNotShapes"/> on, none draws a flat shape on the ground at or after firing (no wedge,
    ///   ring, outline or closing ring of the shot effects or the combat effects, no aim outline but Long Shot's while its key is held,
    ///   no smoke disc), and each is shown by something in the world;
    /// - each effect's reach and time are its rule's, within <see cref="Metres"/> and <see cref="Seconds"/>: a cone's tracers on its
    ///   edges and out to its range, Fan Fire's every tick, the keg in the air landing where the rule lands it and lying exactly as long,
    ///   the blast's ring stopping at its radius within its blast's seconds, the smoke reaching its radius and gone as its time ends;
    /// - off, each draws what it drew before (the outline over its startup, the wedge, the rings, the keg's circles, the smoke's discs)
    ///   and nothing of this order is made;
    /// - the effects are pooled and capped and allocate nothing in a frame.
    /// Game time runs at a fixed 1/60 s a frame here (<see cref="Time.captureDeltaTime"/>), so a frame's step is known, not supposed.
    /// Nobody has played it.
    /// </summary>
    public class SkillLookTests
    {
        /// <summary>m an effect's reach may be off its rule's: a piece moves at most a frame's step past where it is sampled, and a puff's lumps.</summary>
        const float Metres = 0.15f;
        /// <summary>s an effect's time may be off its rule's: two frames at the fixed step here.</summary>
        const float Seconds = 2f / 60f;
        const float Frame = 1f / 60f;

        static readonly string[] HerFifteen =
        {
            "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap",
            "Tag Round", "Breaker Slug", "Slide", "Stopping Shot", "Ricochet", "Smoke Round", "Deadeye",
        };

        /// <summary>The combat effects' moments that are hers and her skills'.</summary>
        static readonly FxMoment[] HerMoments =
        {
            FxMoment.TagRoundTagged, FxMoment.BreakerSlugHit, FxMoment.SlideStarted, FxMoment.SlideEnded, FxMoment.Countered,
            FxMoment.PerfectRelease, FxMoment.MarksSpent, FxMoment.MarkAdded, FxMoment.KegLying, FxMoment.KegSetOffByShot,
        };
        /// <summary>The shapes that lie flat on the ground.</summary>
        static readonly FxShape[] FlatShapes = { FxShape.Wedge, FxShape.Ring, FxShape.Outline, FxShape.Closing, FxShape.SideSpikes };
        static readonly ShotFxKind[] FlatKinds = { ShotFxKind.Wedge, ShotFxKind.Ring, ShotFxKind.Outline };

        PlayerCharacter _player;
        SkillIndicator _indicator;
        EnemyBrute _brute;
        GeneratedLevels _levels;
        CombatEffects _combat;
        Vector3 _stand, _ahead, _side;
        readonly Dictionary<string, Damageable> _dummies = new Dictionary<string, Damageable>();
        static readonly string[] DummyNames = { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" };

        // What was seen while a skill was used.
        readonly HashSet<ShotFx> _knownShots = new HashSet<ShotFx>();
        readonly List<(ShotFxKind kind, ShotFx fx, int frame)> _shots = new List<(ShotFxKind, ShotFx, int)>();
        readonly List<(FxPiece p, int frame)> _pieces = new List<(FxPiece, int)>();
        readonly List<string> _outlineFrames = new List<string>();
        readonly List<int> _firedFrames = new List<int>();
        readonly List<SkillEffects.Shown> _shown = new List<SkillEffects.Shown>();
        int _outlineSeen, _smokeDiscs, _shotsAtStart;
        string _lastState = "none"; // the skill and phase of the frame before: the outline seen now is what its LateUpdate drew for that frame

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SkillLookValues.EffectsNotShapes = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = Frame;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            CombatEffects.Spawned += Record;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false;
            _combat = CombatEffects.Ensure();
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            health.MaxHealth = 100000;
            health.RestoreFull();
            _player.MeleeYouRead = false;
            _indicator = Object.FindAnyObjectByType<SkillIndicator>();
            Assert.That(_indicator, Is.Not.Null, "the aim outline in the scene");
            _brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (_brute != null) _brute.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _stand = _player.Position;
            _ahead = MostRoom(_stand, 20f);
            _side = Vector3.Cross(Vector3.up, _ahead);
            _dummies.Clear();
            foreach (string name in DummyNames)
            {
                GameObject d = GameObject.Find(name);
                if (d == null) continue;
                Damageable body = d.GetComponent<Damageable>();
                body.SetMaxHealth(10000000);
                _dummies[name] = body;
            }
            PutDummiesAway();
            _player.Reported += OnReport;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SkillLookValues.EffectsNotShapes = true;
            CombatEffects.Spawned -= Record;
            if (_player != null) _player.Reported -= OnReport;
            if (_combat != null) Object.Destroy(_combat.gameObject);
            _combat = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            SmokeClouds.Clear();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        void Record(CombatFx fx) => _pieces.Add((fx.Piece, Time.frameCount));

        void OnReport(PlayerReport r)
        {
            if (r.Kind == PlayerReportKind.SkillFired) _firedFrames.Add(Time.frameCount);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);
        Vector3 Ahead(float metres, float across = 0f) => _stand + _ahead * metres + _side * across;

        static Vector3 MostRoom(Vector3 from, float max)
        {
            Vector3 best = Vector3.forward;
            float room = -1f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                float r = max;
                if (NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)
                    && NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas))
                    r = FlatDistance(hit.position, start.position);
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        void PutDummiesAway()
        {
            int k = 0;
            foreach (Damageable d in _dummies.Values) Put(d.transform, _stand - _ahead * 25f + _side * (k++ * 7f - 17f));
            Physics.SyncTransforms(); // the practice brute stays at its home, switched off: the counter's test wakes it there
        }

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds * 4f + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        void StartWatching()
        {
            _knownShots.Clear();
            foreach (ShotFx s in Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None)) _knownShots.Add(s);
            _shots.Clear();
            _pieces.Clear();
            _outlineFrames.Clear();
            _firedFrames.Clear();
            _outlineSeen = 0;
            _smokeDiscs = 0;
            _lastState = "none";
            SkillEffects fx = SkillEffects.Current;
            _shotsAtStart = fx != null ? fx.DrawnBy.Sum() : 0;
        }

        /// <summary>What is drawn this frame: new shot effects, the outline (with the skill's input and phase), smoke discs.</summary>
        void Look()
        {
            foreach (ShotFx s in Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None))
                if (_knownShots.Add(s)) _shots.Add((s.Kind, s, Time.frameCount));
            // The outline is set in LateUpdate, after this look in the frame: what is seen here was drawn for the frame before, so it is
            // judged by that frame's skill and phase.
            SkillDefinition skill = _player.ActiveSkill;
            string state = (skill != null ? skill.Name + " " + skill.Input : "none") + " " + _player.SkillPhase;
            if (_indicator.IsVisible)
            {
                _outlineSeen++;
                _outlineFrames.Add(_lastState);
            }
            _lastState = state;
            foreach (SmokeCloudView v in Object.FindObjectsByType<SmokeCloudView>(FindObjectsSortMode.None))
                if (v.GetComponent<MeshRenderer>() != null) _smokeDiscs++;
        }

        IEnumerator Watching(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds * 4f + 10f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline)
            {
                Look();
                yield return null;
            }
            Look();
        }

        IEnumerator WatchingUntil(Func<bool> done, float most)
        {
            float end = Time.time + most;
            float deadline = Time.realtimeSinceStartup + most * 4f + 10f;
            while (!done() && Time.time < end && Time.realtimeSinceStartup < deadline)
            {
                Look();
                yield return null;
            }
            Look();
        }

        bool Free => !_player.IsLocked && _player.Action == ActionKind.None && _player.KegsLying == 0 && SmokeClouds.Up == 0 && !_player.IsKeyMoving;

        /// <summary>The target and the dummies for a skill, laid out ahead of her; where she aims it.</summary>
        Vector3 Lay(string name)
        {
            PutDummiesAway();
            Damageable a = _dummies["Dummy_A"], b = _dummies["Dummy_B"], c = _dummies["Dummy_C"];
            switch (name)
            {
                case "Scatter Blast":
                case "Fan Fire":
                    Put(a.transform, Ahead(3f, 0.5f));
                    Put(b.transform, Ahead(4f, -1f));
                    Physics.SyncTransforms();
                    return Ahead(6f);
                case "Powder Keg":
                case "Smoke Round":
                    Put(a.transform, Ahead(6f, 0.8f));
                    Put(b.transform, Ahead(6.5f, -1f));
                    Physics.SyncTransforms();
                    return Ahead(6f);
                case "Ricochet":
                    Put(a.transform, Ahead(4f));
                    Put(b.transform, Ahead(4f, 3f));
                    Put(c.transform, Ahead(7f, 3f));
                    Physics.SyncTransforms();
                    return a.transform.position;
                case "Deadeye":
                    Put(a.transform, Ahead(4f, -2f));
                    Put(b.transform, Ahead(6f, 2f));
                    Physics.SyncTransforms();
                    a.AddMarks(2, _player.Kit.MaxMarks, 30f);
                    b.AddMarks(3, _player.Kit.MaxMarks, 30f);
                    return Ahead(6f);
                case "Breaker Slug":
                    Put(a.transform, Ahead(4f));
                    Physics.SyncTransforms();
                    return a.transform.position;
                default:
                    Put(a.transform, Ahead(6f));
                    Physics.SyncTransforms();
                    return a.transform.position;
            }
        }

        /// <summary>One use of a skill, as its key is played, watched from before the press until all it drew is gone.</summary>
        IEnumerator Use(string name)
        {
            int slot = Slot(name);
            _player.EquipSkill(slot);
            _player.Teleport(_stand);
            _player.StartFresh();
            Vector3 aim = Lay(name);
            yield return null;
            SkillDefinition s = _player.Skills[slot];
            StartWatching();
            switch (s.Input)
            {
                case SkillInput.KeyMove:
                    _player.Steer(_side);
                    Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), name);
                    _player.Stop();
                    break;
                case SkillInput.Holding:
                    Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), name);
                    yield return Watching(0.7f);
                    _player.ReleaseSkill(slot);
                    break;
                case SkillInput.Charge:
                    Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), name);
                    float charged = 0.5f * (s.PerfectFrom + s.PerfectTo);
                    yield return WatchingUntil(() => _player.SkillChargedSeconds >= charged, 3f);
                    _player.ReleaseSkill(slot);
                    break;
                default:
                    Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), name);
                    _player.ReleaseSkill(slot);
                    for (int press = 1; press < s.RecastCount; press++)
                    {
                        // The next press of a recast: once the shot (or the hop) it follows has gone off, inside its window.
                        int fired = _firedFrames.Count;
                        yield return WatchingUntil(() => _player.Action == ActionKind.None || (_firedFrames.Count > fired && !s.OpeningMove), 2f);
                        yield return Watching(0.1f);
                        _player.PressSkill(slot, aim);
                        _player.ReleaseSkill(slot);
                    }
                    break;
            }
            yield return WatchingUntil(() => Free, 8f);
            yield return Watching(1.2f); // all its effects after, out to the smoke after a blast
        }

        string Flats() =>
            string.Join(", ", _shots.Where(x => FlatKinds.Contains(x.kind)).Select(x => "shot effect " + x.kind)
                              .Concat(_pieces.Where(x => HerMoments.Contains(x.p.Moment) && FlatShapes.Contains(x.p.Shape)).Select(x => x.p.Moment + "/" + x.p.Shape)));

        // ---------------- The switch on ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator SwitchOn_NoneOfHerFifteen_DrawsAFlatShapeOnTheGround_AtOrAfterFiring_EachIsShownInTheWorld()
        {
            var faults = new List<string>();
            foreach (string name in HerFifteen)
            {
                yield return Use(name);
                SkillEffects fx = SkillEffects.Current;
                int ours = fx != null ? fx.DrawnBy.Sum() - _shotsAtStart : 0;
                string flats = Flats();
                if (flats.Length > 0) faults.Add(name + ": flat shapes on the ground: " + flats);
                // The outline: only while Long Shot's key is held to aim, before it fires; work order 140 (changed deliberately, his tick
                // "Vis under kast og hold"): and while a Casting skill is cast and a Holding skill's key is held and it fires.
                bool castAndHold = SkillLookValues.OutlineInCastAndHold;
                foreach (string f in _outlineFrames.Distinct())
                    if (!f.EndsWith("Charge Channel") && !(castAndHold && (f.EndsWith("Casting Startup") || f.EndsWith("Holding Startup") || f.EndsWith("Holding Channel"))))
                        faults.Add(name + ": the aim outline showed at " + f);
                if (name == "Long Shot" && _outlineSeen == 0) faults.Add("Long Shot: no aim outline while its key was held (it must stay)");
                if (castAndHold && (name == "Heavy Volley" || name == "Deadeye" || name == "Fan Fire") && _outlineSeen == 0)
                    faults.Add(name + ": no aim outline during its cast or its hold (work order 140)");
                if (_smokeDiscs > 0) faults.Add(name + ": the smoke's flat discs drawn");
                int shown = _shots.Count(x => x.kind == ShotFxKind.Streak || x.kind == ShotFxKind.Spark || x.kind == ShotFxKind.Flash) + ours;
                if (shown == 0) faults.Add(name + ": nothing showed it");
                if ((name == "Powder Keg" || name == "Smoke Round" || name == "Scatter Blast" || name == "Slide") && ours == 0)
                    faults.Add(name + ": none of this order's effects drawn");
                Debug.Log($"[SkillLookTests] {name}: shot effects {string.Join(" ", _shots.GroupBy(x => x.kind).Select(g => g.Count() + " " + g.Key))}; " +
                          $"combat effects {string.Join(" ", _pieces.GroupBy(x => x.p.Moment + "/" + x.p.Shape).Select(g => g.Count() + " " + g.Key))}; " +
                          $"this order's pieces {ours}; outline frames {_outlineSeen}");
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        [UnityTest]
        public IEnumerator SwitchOn_TheEffectsFaceTheCamera_TheyLieFlatNowhere()
        {
            yield return Use("Powder Keg");
            // One more keg, watched in its blast: every piece faces the camera (a billboard), none lies level.
            int slot = Slot("Powder Keg");
            _player.StartFresh();
            Assert.That(_player.PressSkill(slot, Ahead(6f)), Is.EqualTo(IntentResult.Started));
            int burst = _player.KegsBurst;
            yield return WatchingUntil(() => _player.KegsBurst > burst, 4f);
            yield return null;
            SkillEffects fx = SkillEffects.Current;
            Assert.That(fx, Is.Not.Null);
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            int checkedPieces = 0;
            foreach (Transform t in fx.transform)
            {
                if (!t.gameObject.activeSelf || t.name.StartsWith("SkillEffects_Keg") || t.name.StartsWith("SkillEffects_Thrown")) continue;
                checkedPieces++;
                Vector3 normal = t.rotation * Vector3.forward;
                Assert.That(Mathf.Abs(Vector3.Dot(normal, cam.transform.forward)), Is.GreaterThan(0.99f), t.name + " faces the camera");
                Assert.That(Mathf.Abs(Vector3.Dot(normal, Vector3.up)), Is.LessThan(0.99f), t.name + " does not lie level");
            }
            Assert.That(checkedPieces, Is.GreaterThan(0), "the blast's pieces were there to look at");
        }

        [UnityTest]
        public IEnumerator SwitchOn_ACounter_IsAStarburstAndChips_NoRingOnTheGround()
        {
            Assume.That(_brute, Is.Not.Null, "the practice brute");
            int stop = Slot("Stopping Shot");
            _player.EquipSkill(stop);
            _brute.enabled = true;
            Vector3 home = _brute.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(_player.Position - home, Vector3.up);
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            _player.Teleport(home + away.normalized * 7f);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (_brute.State != EnemyState.Windup && Time.realtimeSinceStartup < deadline) yield return null;
            Assume.That(_brute.Attack?.Name, Is.EqualTo("Shockwave"));
            StartWatching();
            Assert.That(_player.PressSkill(stop, _brute.transform.position), Is.EqualTo(IntentResult.Started));
            yield return WatchingUntil(() => _brute.Countered > 0, 3f);
            yield return Watching(0.3f);
            _brute.enabled = false;
            Assume.That(_brute.Countered, Is.EqualTo(1));
            Assert.That(_pieces.Count(x => x.p.Moment == FxMoment.Countered && x.p.Shape == FxShape.Spark), Is.EqualTo(1), "its starburst stays");
            Assert.That(_pieces.Count(x => x.p.Moment == FxMoment.Countered && x.p.Shape == FxShape.Shards), Is.EqualTo(1), "chips off it");
            Assert.That(_pieces.Count(x => x.p.Moment == FxMoment.Countered && x.p.Shape == FxShape.Ring), Is.Zero, "no ring on the ground");
            Assert.That(_pieces.First(x => x.p.Moment == FxMoment.Countered && x.p.Shape == FxShape.Shards).p.Tier, Is.EqualTo(FxTier.Key), "never dropped, as the ring was not");
        }

        // ---------------- Each effect's reach and time are its rule's ----------------

        /// <summary>The tracers that went off in a frame: each one's yaw from her aim (degrees) and its reach from her (m, flat).</summary>
        List<(float yaw, float reach)> TracersIn(int frame, Vector3 from, Vector3 aimDir) =>
            _shots.Where(x => x.frame == frame && x.kind == ShotFxKind.Streak && x.fx != null)
                  .Select(x => (Vector3.SignedAngle(aimDir, Flat(x.fx.End - from), Vector3.up), FlatDistance(x.fx.End, from))).ToList();

        [UnityTest]
        public IEnumerator ScatterBlast_ItsTracersLieOnTheConesEdges_AndRunToItsRange_DustWhereEachEnds()
        {
            int slot = Slot("Scatter Blast");
            _player.EquipSkill(slot);
            SkillDefinition s = _player.Skills[slot];
            Vector3 aim = Lay("Scatter Blast");
            _player.Teleport(_stand);
            yield return null;
            StartWatching();
            Vector3 from = _player.Position, dir = Flat(aim - from).normalized;
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            yield return WatchingUntil(() => _firedFrames.Count > 0, 2f);
            Assert.That(_firedFrames, Is.Not.Empty, "it fired");
            from = _player.Position;
            List<(float yaw, float reach)> tracers = TracersIn(_firedFrames[0], from, dir);
            ShotLook look = _player.ShotLooks != null ? _player.ShotLooks.For("Scatter Blast") : null;
            Assert.That(tracers.Count, Is.EqualTo(Mathf.Max(1, (look ?? ShotFxDefaults.For("Scatter Blast")).Rays)), "its tracers");
            Assert.That(tracers.Min(t => t.yaw), Is.EqualTo(-s.Angle * 0.5f).Within(1f), "one on each edge of the cone");
            Assert.That(tracers.Max(t => t.yaw), Is.EqualTo(s.Angle * 0.5f).Within(1f));
            foreach ((float yaw, float reach) t in tracers) Assert.That(t.reach, Is.EqualTo(s.Range).Within(Metres), "out to its range, nothing in the way");
            SkillEffects fx = SkillEffects.Current;
            Assert.That(fx, Is.Not.Null);
            fx.ListPieces(_shown);
            List<SkillEffects.Shown> kicks = _shown.Where(p => p.Use == SkillEffects.Use.TracerKick).ToList();
            Assert.That(kicks.Count, Is.EqualTo(tracers.Count), "dust where each ends");
            foreach (SkillEffects.Shown k in kicks)
                Assert.That(FlatDistance(k.Position, from), Is.EqualTo(s.Range).Within(Metres), "at its reach");
            Assert.That(_shown.Count(p => p.Use == SkillEffects.Use.MuzzleSmoke), Is.EqualTo(SkillLookValues.Live.BlastSmokePuffs), "gun smoke");
            Assert.That(Flats(), Is.Empty, "no wedge");
        }

        [UnityTest]
        public IEnumerator FanFire_EveryTicksTracers_SpanItsCone_AndRunToItsRange_ForAsLongAsItIsHeld()
        {
            int slot = Slot("Fan Fire");
            _player.EquipSkill(slot);
            SkillDefinition s = _player.Skills[slot];
            Vector3 aim = Lay("Fan Fire");
            _player.Teleport(_stand);
            yield return null;
            StartWatching();
            Vector3 dir = Flat(aim - _player.Position).normalized;
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            yield return Watching(1f);
            _player.ReleaseSkill(slot);
            int heldTicks = _firedFrames.Count;
            yield return WatchingUntil(() => Free, 3f);
            Assert.That(heldTicks, Is.GreaterThan(3), "it ticked while held");
            Assert.That(_firedFrames.Count - heldTicks, Is.LessThanOrEqualTo(1), "and stopped when let go");
            Vector3 from = _player.Position;
            foreach (int frame in _firedFrames.Distinct())
            {
                List<(float yaw, float reach)> tracers = TracersIn(frame, from, dir);
                if (tracers.Count < 2) continue;
                Assert.That(tracers.Min(t => t.yaw), Is.EqualTo(-s.Angle * 0.5f).Within(1f), "this tick spans the cone");
                Assert.That(tracers.Max(t => t.yaw), Is.EqualTo(s.Angle * 0.5f).Within(1f));
                foreach ((float yaw, float reach) t in tracers) Assert.That(t.reach, Is.EqualTo(s.Range).Within(Metres));
            }
            // Work order 140 (changed deliberately, "Vis under kast og hold"): its cone's outline stands while its key is held and it fires.
            if (SkillLookValues.OutlineInCastAndHold) Assert.That(_outlineSeen, Is.GreaterThan(0), "its cone's outline while it is held");
            else Assert.That(_outlineSeen, Is.Zero, "no cone drawn on the ground while it fires");
        }

        [UnityTest]
        public IEnumerator PowderKeg_InTheAirOverItsStartup_LandsWhereTheRuleLandsIt_LiesExactlyAsLong_ItsBlastStopsAtItsRadius()
        {
            int slot = Slot("Powder Keg");
            _player.EquipSkill(slot);
            SkillDefinition s = _player.Skills[slot];
            Vector3 aim = Lay("Powder Keg");
            _player.Teleport(_stand);
            yield return null;
            StartWatching();
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            // The throw.
            var thrown = new List<Vector3>();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_player.KegsLying == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                SkillEffects f = SkillEffects.Current;
                if (f != null && f.ThrowShown) thrown.Add(f.ThrowAt);
                Look();
            }
            Assert.That(_player.KegsLying, Is.EqualTo(1), "it landed");
            Vector3 landed = _player.LastKegPoint;
            Assert.That(thrown.Count, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(0.5f * s.Startup / Frame)), "drawn in the air over its startup");
            Assert.That(thrown.Max(p => p.y), Is.GreaterThan(1.2f), "up in an arc");
            float step = FlatDistance(_player.ThrowHand, landed) / Mathf.Max(1f, s.Startup / Frame);
            Assert.That(FlatDistance(thrown.Last(), landed), Is.LessThan(step * 2f + Metres), "its last place in the air is where the rule lands it");
            // Lying: the keg drawn at its place for exactly as long as it lies.
            SkillEffects fx = SkillEffects.Current;
            int lyingFrames = 0, shownFrames = 0;
            float landedAt = Time.time;
            deadline = Time.realtimeSinceStartup + 10f;
            int burst = _player.KegsBurst;
            while (_player.KegsBurst == burst && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Look();
                if (_player.KegsLying == 0) break;
                lyingFrames++;
                if (fx.KegsShown == 1)
                {
                    shownFrames++;
                    Assert.That(FlatDistance(fx.KegAt(0), landed), Is.LessThan(0.01f), "where it lies");
                }
            }
            Assert.That(shownFrames, Is.GreaterThanOrEqualTo(lyingFrames - 1), "drawn every frame it lies (the frame it lands perhaps not yet)");
            Assert.That(Time.time - landedAt, Is.EqualTo(s.Fuse).Within(Seconds + Frame), "its fuse");
            // The blast: the ring runs out to the radius and no farther, and is gone in its blast's seconds.
            float burstAt = Time.time, blast = _player.ShotLooks != null && _player.ShotLooks.For("Powder Keg") != null ? _player.ShotLooks.For("Powder Keg").BlastSeconds : ShotFxDefaults.For("Powder Keg").BlastSeconds;
            float farthest = 0f, lastRing = burstAt, smokeFarthest = 0f;
            bool kegGone = false;
            deadline = Time.realtimeSinceStartup + 10f;
            while (Time.time < burstAt + blast + SkillLookValues.Live.AfterSmokeSeconds * 1.2f + 0.1f && Time.realtimeSinceStartup < deadline)
            {
                fx.ListPieces(_shown);
                foreach (SkillEffects.Shown p in _shown)
                {
                    float edge = FlatDistance(p.Position, landed) + p.Half;
                    if (p.Use == SkillEffects.Use.RingDust || p.Use == SkillEffects.Use.RingSpark)
                    {
                        Assert.That(edge, Is.LessThanOrEqualTo(s.Radius + Metres), "the ring never past the radius");
                        Assert.That(p.Life, Is.EqualTo(blast).Within(1e-4f), "its blast's seconds");
                        farthest = Mathf.Max(farthest, edge);
                        lastRing = Time.time;
                    }
                    if (p.Use == SkillEffects.Use.AfterSmoke)
                    {
                        Assert.That(edge, Is.LessThanOrEqualTo(s.Radius + Metres), "the smoke after inside the circle");
                        smokeFarthest = Mathf.Max(smokeFarthest, edge);
                    }
                }
                Look();
                yield return null;
                kegGone |= fx.KegsShown == 0;
            }
            Assert.That(kegGone, Is.True, "the keg is gone as it bursts");
            Assert.That(farthest, Is.EqualTo(s.Radius).Within(Metres), "the ring reaches the radius");
            Assert.That(lastRing - burstAt, Is.LessThanOrEqualTo(blast + Seconds), "gone within its blast's seconds");
            Assert.That(smokeFarthest, Is.GreaterThan(0f), "smoke after it");
            Assert.That(Flats(), Is.Empty, "no rings or circles on the ground");
        }

        [UnityTest]
        public IEnumerator SmokeRound_ItsPuffsReachItsRadius_NoFarther_DrawnExactlyWhileItStands()
        {
            int slot = Slot("Smoke Round");
            _player.EquipSkill(slot);
            SkillDefinition s = _player.Skills[slot];
            Vector3 aim = Lay("Smoke Round");
            _player.Teleport(_stand);
            yield return null;
            StartWatching();
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SmokeClouds.Up == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SmokeClouds.Up, Is.EqualTo(1));
            SmokeCloud cloud = SmokeClouds.At(0);
            Vector3 centre = new Vector3(cloud.Centre.X, 0f, cloud.Centre.Z);
            Assert.That(SmokeClouds.ViewAt(0).GetComponent<MeshRenderer>() == null, Is.True, "no flat discs");
            SkillEffects fx = SkillEffects.Current;
            Assert.That(fx, Is.Not.Null);
            float farthest = 0f;
            int upFrames = 0, drawnFrames = 0;
            while (SmokeClouds.Up > 0 && Time.realtimeSinceStartup < deadline + 10f)
            {
                yield return null;
                fx.ListPieces(_shown);
                List<SkillEffects.Shown> puffs = _shown.Where(p => p.Use == SkillEffects.Use.Smoke).ToList();
                if (SmokeClouds.Up == 0)
                {
                    Assert.That(puffs, Is.Empty, "gone the frame its time is out");
                    break;
                }
                upFrames++;
                if (puffs.Count == SkillLookValues.Live.SmokePuffs) drawnFrames++;
                foreach (SkillEffects.Shown p in puffs)
                {
                    float edge = FlatDistance(p.Position, centre) + p.Half;
                    Assert.That(edge, Is.LessThanOrEqualTo(cloud.Radius + Metres), "inside its circle");
                    farthest = Mathf.Max(farthest, edge);
                    Assert.That(p.Cloud, Is.EqualTo(0));
                }
            }
            Assert.That(Time.time, Is.EqualTo(cloud.Until).Within(Seconds), "it ended when the rule's cloud did");
            Assert.That(drawnFrames, Is.GreaterThanOrEqualTo(upFrames - 1), "all its puffs drawn every frame it stood");
            Assert.That(upFrames * Frame, Is.EqualTo(s.SmokeSeconds).Within(Seconds + Frame), "for its seconds");
            Assert.That(farthest, Is.EqualTo(cloud.Radius).Within(Metres), "out to its radius");
            fx.ListPieces(_shown);
            Assert.That(_shown.Count(p => p.Use == SkillEffects.Use.Smoke), Is.Zero, "none after");
        }

        [UnityTest]
        public IEnumerator SmokeRound_ANewFloorOrARestart_TakesItsPuffsWithIt()
        {
            int slot = Slot("Smoke Round");
            _player.EquipSkill(slot);
            Vector3 aim = Lay("Smoke Round");
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SmokeClouds.Up == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            SkillEffects fx = SkillEffects.Current;
            fx.ListPieces(_shown);
            Assert.That(_shown.Count(p => p.Use == SkillEffects.Use.Smoke), Is.GreaterThan(0));
            _player.Teleport(_stand); // a new floor, a restart: SmokeClouds.Clear()
            yield return null;
            yield return null;
            fx.ListPieces(_shown);
            Assert.That(_shown.Count(p => p.Use == SkillEffects.Use.Smoke), Is.Zero, "gone with the cloud");
        }

        // ---------------- Off: as before ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator SwitchOff_EverySkill_DrawsWhatItDrewBefore_AndNothingOfThisOrderIsMade()
        {
            SkillLookValues.EffectsNotShapes = false;
            var faults = new List<string>();
            foreach (string name in HerFifteen)
            {
                yield return Use(name);
                SkillDefinition s = _player.Skills[Slot(name)];
                if (s.Input != SkillInput.KeyMove && _outlineSeen == 0) faults.Add(name + ": no aim outline over its startup, as before");
                switch (name)
                {
                    case "Scatter Blast":
                        if (!_shots.Any(x => x.kind == ShotFxKind.Wedge)) faults.Add(name + ": no wedge");
                        break;
                    case "Powder Keg":
                        if (_shots.Count(x => x.kind == ShotFxKind.Outline) < 2) faults.Add(name + ": not its two outlines (the fuse's, the burst's)");
                        if (!_shots.Any(x => x.kind == ShotFxKind.Ring)) faults.Add(name + ": no ring at its burst");
                        if (!_pieces.Any(x => x.p.Moment == FxMoment.KegLying && x.p.Shape == FxShape.Closing)) faults.Add(name + ": no closing ring over its fuse");
                        break;
                    case "Smoke Round":
                        if (_smokeDiscs == 0) faults.Add(name + ": no smoke discs");
                        break;
                    case "Breaker Slug":
                        if (!_pieces.Any(x => x.p.Moment == FxMoment.BreakerSlugHit && x.p.Shape == FxShape.Ring)) faults.Add(name + ": no ring on what it hit");
                        break;
                    case "Long Shot":
                        if (!_pieces.Any(x => x.p.Moment == FxMoment.PerfectRelease && x.p.Shape == FxShape.Ring)) faults.Add(name + ": no perfect ring at her feet");
                        break;
                    case "Slide":
                        if (!_pieces.Any(x => x.p.Moment == FxMoment.SlideStarted && x.p.Shape == FxShape.Wedge)) faults.Add(name + ": no dust wedge");
                        break;
                }
                if (_pieces.Any(x => x.p.Shape == FxShape.Shards && (x.p.Moment == FxMoment.BreakerSlugHit || x.p.Moment == FxMoment.Countered))) faults.Add(name + ": this order's chips");
            }
            Assert.That(SkillEffects.Current, Is.Null, "nothing of this order made");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        // ---------------- Cheap ----------------

        [UnityTest]
        public IEnumerator TheEffects_ArePooledAndCapped_AndAFrameAllocatesNothing()
        {
            SkillEffects fx = SkillEffects.Ensure().Watch(_player);
            Vector3 c = Ahead(6f);
            fx.Blast(c, 2.5f, 0.35f); // warm
            yield return For(1.5f);
            int made = fx.Made;
            for (int i = 0; i < 40; i++)
            {
                fx.Blast(c, 2.5f, 0.35f);
                fx.TracerEnd(c);
                fx.MuzzleSmoke(c + Vector3.up, Vector3.forward, 3);
                yield return null;
            }
            Assert.That(fx.Live, Is.LessThanOrEqualTo(SkillLookValues.Live.MostPieces), "capped");
            Assert.That(fx.Pooled, Is.LessThanOrEqualTo(SkillLookValues.Live.MostPieces + SkillLookValues.Live.PoolGrow), "the pool grows no farther than the cap");
            Assert.That(fx.Capped, Is.GreaterThan(0), "a crowd of blasts was capped");
            yield return For(1.5f);
            Assert.That(fx.Live, Is.Zero, "all gone");
            Assert.That(fx.Made, Is.GreaterThan(made), "the crowd grew the pool");
            int pooled = fx.Pooled, warm = fx.Made;
            for (int i = 0; i < 20; i++)
            {
                fx.Blast(c, 2.5f, 0.35f);
                yield return For(0.5f);
            }
            Assert.That(fx.Pooled, Is.EqualTo(pooled), "warm: no piece more made");
            Assert.That(fx.Made, Is.EqualTo(warm), "nothing made per use");
            // A frame's work allocates nothing: with nothing live, and while a blast's pieces move.
            Assert.That(() => { _junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            fx.Step();
            Assert.That(() => fx.Step(), AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "nothing live");
            fx.Blast(c, 2.5f, 0.35f);
            fx.Step();
            Assert.That(() => { for (int i = 0; i < 10; i++) fx.Step(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a blast's pieces moving");
            // And with a smoke cloud standing.
            int smoke = Slot("Smoke Round");
            _player.EquipSkill(smoke);
            _player.StartFresh();
            Assert.That(_player.PressSkill(smoke, Ahead(6f)), Is.EqualTo(IntentResult.Started));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SmokeClouds.Up == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return For(0.5f);
            fx.Step();
            Assert.That(() => { for (int i = 0; i < 10; i++) fx.Step(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a cloud standing");
        }

        static byte[] _junk;
    }
}
