using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 122 ("bein flyr rund etter slik fysikkens lover tilsier ... Gjerne knuse i biter"), in the ControlRoom scene with
    /// enemies spawned through the practice room's intents and hit through their own health: with the switch off an enemy is hit
    /// and dies exactly as before (no part off, its death seen, no piece); on, a part comes off at two thirds and one third of its
    /// health, thrown away from her, and the enemy fights on as itself; its death breaks it into its pieces, thrown by the killing
    /// hit, harder for a harder hit; the pieces come to rest on the floor, sink and are gone in their time; they touch neither her
    /// nor an enemy and no shot or sight ray meets them; the cap holds, the oldest first; the practice brute comes back whole; every
    /// kind with a skeleton's body has its pieces and the boss none; the bomber breaks at its blast; the boxes made in code still
    /// break; a frame of the pieces and of a body allocates nothing. Physics cannot be pinned: bounds, counts and directions.
    /// Writes a line per model ("BONEBREAK").
    /// </summary>
    public class BoneBreakPlayTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        double _lie, _sink;
        int _most;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            BoneBreakValues.On = true;
            BoneBreakModel.ForceShapesMadeInCode = false;
            BoneBreakValues v = BoneBreakValues.Live;
            _lie = v.LieSeconds;
            _sink = v.SinkSeconds;
            _most = v.MostPieces;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            BoneBreakValues v = BoneBreakValues.Live;
            v.LieSeconds = _lie;
            v.SinkSeconds = _sink;
            v.MostPieces = _most;
            BoneBreakValues.On = true;
            BoneBreakModel.ForceShapesMadeInCode = false;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (BonePieces.Current != null) Object.Destroy(BonePieces.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 20f;
            while (Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>One enemy of a kind, held where it stands (its brain off: it is only hit here), and she 5 m west of it.</summary>
        IEnumerator Spawn(EnemyKind kind, List<Enemy> into)
        {
            // A brute is a copy of the practice brute: copied while that one's brain is off, the copy's is off too and it never
            // appears (Enemy.Appeared) until it is switched on. In the game the practice brute's brain is on; so it is here, for the copy.
            bool was = _practiceEnemy.enabled;
            _practiceEnemy.enabled = true;
            List<Enemy> made = _levels.SpawnPractice(kind);
            _practiceEnemy.enabled = was;
            Assert.That(made.Count, Is.GreaterThanOrEqualTo(1), kind + " spawned");
            yield return null;
            yield return null;
            foreach (Enemy m in made) Hold(m);
            _player.Teleport(Flat(made[0].transform.position) + Vector3.left * 5f);
            yield return null;
            into.Add(made[0]);
        }

        static void Hold(Enemy e)
        {
            e.enabled = false;
            NavMeshAgent agent = e.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
        }

        /// <summary>Hurt it down to this share of its health, through its health as a hit would.</summary>
        static void HurtTo(Enemy e, float share)
        {
            int to = Mathf.FloorToInt(e.Body.MaxHealth * share);
            int hit = e.Body.CurrentHealth - to;
            if (hit > 0) e.Body.TakeHit(hit);
        }

        static BonePieces Pieces => BonePieces.Current;

        static List<int> LiveOf(BoneBreak source)
        {
            var list = new List<int>();
            BonePieces p = Pieces;
            if (p == null) return list;
            for (int i = 0; i < p.Made; i++)
                if (p.StateOf(i) != BonePieceState.Free && (source == null || p.SourceOf(i) == source)) list.Add(i);
            return list;
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator SwitchOff_AnEnemyIsHitAndDies_ExactlyAsBefore()
        {
            BoneBreakValues.On = false;
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            Enemy e = got[0];
            Transform head = EnemyLook.Find(e.Look.Model.transform, "Head");
            Vector3 headScale = head.localScale;
            e.enabled = true;
            HurtTo(e, 0.2f);
            yield return null;
            Assert.That(head.localScale, Is.EqualTo(headScale), "no part off");
            Assert.That(Pieces == null || Pieces.Thrown == 0, Is.True, "no piece made");
            e.Body.TakeHit(e.Body.CurrentHealth);
            yield return null;
            yield return null;
            Assert.That(e.IsDead, Is.True);
            Assert.That(e.Look.Playing, Is.EqualTo("Death"));
            Assert.That(e.Look.Skin.enabled, Is.True, "it is seen falling, as before");
            yield return For(e.ShownDeadFor(e.Values) + 0.2f);
            Assert.That(e.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer)).All(r => !r.enabled), Is.True, "then it is gone, as before");
            Assert.That(Pieces == null || Pieces.Thrown == 0, Is.True, "and no piece was ever made");

            // An enemy that appears with the switch off has nothing of the order on it.
            var more = new List<Enemy>();
            yield return Spawn(EnemyKind.Caster, more);
            Assert.That(more[0].GetComponent<BoneBreak>(), Is.Null);
        }

        // ---------------- A part comes off ----------------

        [UnityTest]
        public IEnumerator APart_ComesOffAtTwoThirds_AndAnotherAtOneThird_ThrownAwayFromHer_AndItFightsOnAsItself()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            Enemy e = got[0];
            BoneBreak b = e.GetComponent<BoneBreak>();
            Assert.That(b, Is.Not.Null, "on a skeleton as it appears");
            Vector3 along = Flat(e.transform.position - _player.Position).normalized;

            HurtTo(e, 0.7f);
            yield return null;
            Assert.That(b.PartsLost, Is.EqualTo(0), "none above two thirds");
            Assert.That(LiveOf(b).Count, Is.EqualTo(0));

            int max = e.Body.MaxHealth;
            HurtTo(e, 0.6f);
            Assert.That(b.PartsLost, Is.EqualTo(1));
            Assert.That(b.HasLost(BonePart.Skull), Is.True, "the skull first");
            Assert.That(b.RootOf(BonePart.Skull).localScale, Is.EqualTo(Vector3.zero), "gone from its body");
            List<int> skull = LiveOf(b);
            Assert.That(skull.Count, Is.GreaterThanOrEqualTo(1), "its piece flies");
            var from = skull.ToDictionary(i => i, i => Pieces.BodyOf(i).position);
            yield return For(0.35f);
            foreach (int i in skull)
            {
                Vector3 moved = Flat(Pieces.BodyOf(i).position - from[i]);
                Assert.That(Vector3.Dot(moved, along), Is.GreaterThan(0.1f), "thrown away from her, along her shot");
            }
            // The fight is its own: health as the hit left it, its values and size as they were, alive.
            Assert.That(e.Body.MaxHealth, Is.EqualTo(max));
            Assert.That(e.Body.CurrentHealth, Is.EqualTo(Mathf.FloorToInt(max * 0.6f)));
            Assert.That(e.IsDead, Is.False);
            yield return null;
            Assert.That(b.RootOf(BonePart.Skull).localScale, Is.EqualTo(Vector3.zero), "held off after its pose");

            HurtTo(e, 0.3f);
            Assert.That(b.PartsLost, Is.EqualTo(2));
            bool left = b.HasLost(BonePart.ShinLeft), right = b.HasLost(BonePart.ShinRight);
            Assert.That(left ^ right, Is.True, "one shin and foot");
            bool nearIsLeft = Vector3.Dot(e.transform.right, along) > 0f;
            Assert.That(left, Is.EqualTo(nearIsLeft), "the one on her side");
            Assert.That(b.HasLost(BonePart.ForearmLeft) || b.HasLost(BonePart.ForearmRight) || b.HasLost(BonePart.UpperArmLeft) || b.HasLost(BonePart.UpperArmRight),
                        Is.False, "never an arm before its death");

            // It still fights: its brain on, it wakes and comes for her and winds up as before.
            e.enabled = true;
            _player.Teleport(Flat(e.transform.position) + Vector3.left * 1.5f);
            yield return Until(() => e.State == EnemyState.Windup || e.State == EnemyState.Recover, 6f);
            Assert.That(e.State == EnemyState.Windup || e.State == EnemyState.Recover, Is.True, "it attacks as before, State " + e.State);
        }

        // ---------------- Its death ----------------

        [UnityTest]
        public IEnumerator ADeath_BreaksTheBody_IntoItsPieces_ThrownByTheKillingHit_HarderForAHarderHit()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            yield return Spawn(EnemyKind.Guard, got);
            float soft = 0f, hard = 0f;
            for (int k = 0; k < 2; k++)
            {
                Enemy e = got[k];
                BoneBreak b = e.GetComponent<BoneBreak>();
                _player.Teleport(Flat(e.transform.position) + Vector3.left * 5f);
                Vector3 along = Flat(e.transform.position - _player.Position).normalized;
                int damage = k == 0 ? Placeholders.BasicShotDamage : 2000;
                e.Body.SetMaxHealth(damage); // one hit from full kills it: the killing hit is this hit
                e.Body.TakeHit(damage);
                Assert.That(e.IsDead, Is.True);
                Assert.That(b.Broken, Is.True, "broken at once");
                List<int> pieces = LiveOf(b);
                Assert.That(pieces.Count, Is.GreaterThanOrEqualTo(10), "into its pieces: " + pieces.Count);
                Assert.That(e.Look.Renderers.All(r => r == null || !r.enabled), Is.True, "its body is gone from where it stood");
                float sum = 0f;
                foreach (int i in pieces) sum += Vector3.Dot(Flat(Pieces.BodyOf(i).linearVelocity), along);
                float mean = sum / pieces.Count;
                Assert.That(mean, Is.GreaterThan(0.5f), "on the whole thrown along the shot");
                if (k == 0) soft = mean; else hard = mean;
                Debug.Log(string.Format(s_Inv, "BONEBREAK death of a {0} by a hit of {1}: {2} pieces, mean speed along the shot {3:0.00} m/s", e.LogWord, damage, pieces.Count, mean));
            }
            Assert.That(hard, Is.GreaterThan(soft * 1.5f), "a harder hit throws harder");
        }

        [UnityTest]
        public IEnumerator ThePieces_ComeToRestOnTheFloor_InBounds_ThenSink_AndAreGoneInTheirTime()
        {
            BoneBreakValues v = BoneBreakValues.Live;
            v.LieSeconds = 1.0;
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Shooter, got);
            Enemy e = got[0];
            Vector3 at = e.transform.position;
            e.Body.SetMaxHealth(Placeholders.BasicShotDamage); // killed by her basic shot, as most are
            e.Body.TakeHit(Placeholders.BasicShotDamage);
            BoneBreak b = e.GetComponent<BoneBreak>();
            List<int> pieces = LiveOf(b);
            Assume.That(pieces.Count, Is.GreaterThan(0));
            yield return Until(() => LiveOf(b).All(i => Pieces.StateOf(i) != BonePieceState.Flying), (float)v.MostFlySeconds + 3f);
            List<int> resting = LiveOf(b).Where(i => Pieces.StateOf(i) == BonePieceState.Resting).ToList();
            Assert.That(resting.Count, Is.GreaterThan(0), "they came to rest");
            foreach (int i in resting)
            {
                Vector3 p = Pieces.BodyOf(i).position;
                Assert.That(p.y, Is.InRange(-0.3f, 1.5f), "on the floor: " + p);
                Assert.That(Flat(p - at).magnitude, Is.LessThan(15f), "near where it died");
                Assert.That(Pieces.BodyOf(i).linearVelocity.magnitude, Is.LessThan(1f), "still");
            }
            yield return Until(() => Pieces.Live == 0, (float)(v.LieSeconds + v.SinkSeconds + v.MostFlySeconds) + 3f);
            Assert.That(Pieces.Live, Is.EqualTo(0), "all gone in their time");
            for (int i = 0; i < Pieces.Made; i++) Assert.That(Pieces.BodyOf(i).gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(Pieces.enabled, Is.False, "and the pool does not run while none lives");
        }

        [UnityTest]
        public IEnumerator ThePieces_NeverTouchHerOrAnEnemy_AndNoShotOrSightRayMeetsThem()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            yield return Spawn(EnemyKind.Guard, got);
            Enemy dying = got[0], standing = got[1];
            // The standing one 1.2 m beyond the dying one along her shot, she 1.2 m before it: pieces fly through both places.
            Vector3 o = Flat(dying.transform.position);
            standing.GetComponent<NavMeshAgent>().Warp(o + Vector3.right * 1.2f);
            _player.Teleport(o + Vector3.left * 1.2f);
            yield return null;
            Vector3 her = _player.Position, its = standing.transform.position;
            Assert.That(_player.GetComponentsInChildren<Collider>(true), Is.Empty, "she has no collider: nothing physical can push her");
            Assert.That(_player.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(standing.GetComponentsInChildren<Collider>(true).Where(c => c.enabled), Is.Empty, "nor an enemy");
            dying.Body.SetMaxHealth(2000);
            dying.Body.TakeHit(2000);
            List<int> pieces = LiveOf(null);
            Assume.That(pieces.Count, Is.GreaterThan(0));
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                foreach (int i in LiveOf(null))
                {
                    Rigidbody body = Pieces.BodyOf(i);
                    Assert.That(body.gameObject.layer, Is.EqualTo(BonePieces.PieceLayer), "on the layer no query of the game asks");
                    Collider c = body.GetComponent<Collider>();
                    Vector3 p = body.worldCenterOfMass;
                    // A ray as the game's shots, sight lines and the laser ask (their default layers) through the piece: never it.
                    if (c.enabled && Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        Assert.That(hit.collider, Is.Not.SameAs(c), "a ray with the game's layers met a piece");
                }
                yield return null;
            }
            Assert.That(Flat(_player.Position - her).magnitude, Is.LessThan(1e-3f), "she was not moved");
            Assert.That(Flat(standing.transform.position - its).magnitude, Is.LessThan(1e-3f), "nor the enemy");
        }

        // ---------------- The cap ----------------

        [UnityTest]
        public IEnumerator TheCap_Holds_TheOldestPiecesGoFirst()
        {
            BoneBreakValues.Live.MostPieces = 20;
            var got = new List<Enemy>();
            for (int k = 0; k < 3; k++) yield return Spawn(EnemyKind.Brute, got);
            foreach (Enemy e in got)
            {
                e.Body.TakeHit(e.Body.CurrentHealth);
                Assert.That(Pieces.Live, Is.LessThanOrEqualTo(20));
                yield return null;
            }
            Assert.That(Pieces.TakenFromTheOldest, Is.GreaterThan(0), "the oldest taken");
            BoneBreak last = got[2].GetComponent<BoneBreak>(), first = got[0].GetComponent<BoneBreak>();
            Assert.That(LiveOf(last).Count, Is.GreaterThanOrEqualTo(10), "the newest death keeps its pieces");
            Assert.That(LiveOf(first).Count, Is.LessThan(LiveOf(last).Count), "the oldest gave theirs up first");
        }

        // ---------------- Coming back whole ----------------

        [UnityTest]
        public IEnumerator ThePracticeBrute_LosesItsSkull_Dies_AndComesBackWhole()
        {
            BoneBreak b = _practiceEnemy.GetComponent<BoneBreak>();
            Assert.That(b, Is.Not.Null, "the practice brute too");
            _practiceEnemy.enabled = true;
            _player.Teleport(Flat(_practiceEnemy.transform.position) + Vector3.back * 30f);
            yield return null;
            Transform head = b.RootOf(BonePart.Skull);
            Vector3 scale = head.localScale;
            HurtTo(_practiceEnemy, 0.5f);
            Assert.That(head.localScale, Is.EqualTo(Vector3.zero));
            _practiceEnemy.Body.TakeHit(_practiceEnemy.Body.CurrentHealth);
            Assert.That(b.Broken, Is.True);
            yield return Until(() => !_practiceEnemy.IsDead, _practiceEnemy.Values.RespawnDelay + 3f);
            yield return null;
            Assert.That(_practiceEnemy.IsDead, Is.False, "it comes back");
            Assert.That(head.localScale, Is.EqualTo(scale), "whole");
            Assert.That(b.PartsLost, Is.EqualTo(0));
            Assert.That(b.Broken, Is.False);
            Assert.That(_practiceEnemy.Look.Skin.enabled, Is.True, "and seen");
        }

        // ---------------- Every kind ----------------

        [UnityTest]
        public IEnumerator EveryKindWithASkeletonsBody_HasItsPieces_FromItsOwnMesh_TheBossNone()
        {
            foreach (EnemyKind kind in new[] { EnemyKind.Brute, EnemyKind.Swarmer, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Bomber, EnemyKind.Warder, EnemyKind.Lancer, EnemyKind.Boss })
            {
                var got = new List<Enemy>();
                yield return Spawn(kind, got);
                Enemy e = got[0];
                BoneBreak b = e.GetComponent<BoneBreak>();
                if (kind == EnemyKind.Boss)
                {
                    Assert.That(b, Is.Null, "the boss does not break");
                    continue;
                }
                Assert.That(b, Is.Not.Null, kind + " breaks");
                BoneBreakModel m = b.Model;
                Assert.That(m, Is.Not.Null);
                Assert.That(m.Split, Is.True, kind + ": its own mesh split by its bones");
                var skin = m.Shapes.Where(s => !s.OwnMesh).ToList();
                Assert.That(skin.Select(s => s.Part).Distinct().Count(), Is.GreaterThanOrEqualTo(10), kind + ": a piece for most of its bones' groups");
                Assert.That(skin.Any(s => s.Part == (int)BonePart.Skull), Is.True, kind + ": a skull");
                foreach (BoneBreakModel.Shape s in m.Shapes)
                {
                    float longest = Mathf.Max(s.Size.x, Mathf.Max(s.Size.y, s.Size.z));
                    Assert.That(longest, Is.InRange(0.01f, 3.5f), kind + ": a piece of a body's size, " + (BonePart)s.Part);
                }
                Debug.Log(string.Format(s_Inv, "BONEBREAK {0} ({1}): {2} pieces ({3} of its skin, {4} rigid meshes: {5}), {6} that break again, {7} triangles in all, made in {8:0.0} ms; parts: {9}",
                    kind, e.Look.Spec.Name, m.Shapes.Count, skin.Count, m.Shapes.Count - skin.Count,
                    string.Join("/", m.Shapes.Where(s => s.OwnMesh).Select(s => e.Look.Renderers[s.Renderer].name)),
                    m.Shapes.Count(s => s.Halves != null), m.Shapes.Sum(s => s.Triangles), m.MadeInMs,
                    string.Join(", ", skin.Select(s => (BonePart)s.Part + " " + s.Triangles + " tris " + s.Size.ToString("0.00")))));
            }
        }

        [UnityTest]
        public IEnumerator AnElite_Breaks_ItsPiecesAsLargeAsItIs()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            yield return Spawn(EnemyKind.Brute, got);
            EnemyFactory.MakeElite(got[1], 6000, 1.25f, 4f / 3f, 0.5f);
            yield return null;
            float size0 = 0f, size1 = 0f;
            for (int k = 0; k < 2; k++)
            {
                BoneBreak b = got[k].GetComponent<BoneBreak>();
                got[k].Body.TakeHit(got[k].Body.CurrentHealth);
                List<int> p = LiveOf(b);
                Assert.That(p.Count, Is.GreaterThanOrEqualTo(10));
                float s = p.Sum(i => Pieces.RendererOf(i).transform.lossyScale.magnitude) / p.Count; // as shown: its scale, whatever its turn
                if (k == 0) size0 = s; else size1 = s;
            }
            Assert.That(size1 / size0, Is.EqualTo(4f / 3f).Within(0.02f), "the elite's pieces are as much larger as it is");
        }

        [UnityTest]
        public IEnumerator TheBomber_BreaksAtItsBlast_NotAtItsDeath()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Bomber, got);
            Enemy e = got[0];
            e.enabled = true; // its fuse burns in its own Update
            _player.Teleport(Flat(e.transform.position) + Vector3.left * 30f);
            BoneBreak b = e.GetComponent<BoneBreak>();
            e.Body.TakeHit(e.Body.CurrentHealth);
            yield return null;
            Assert.That(e.IsDead, Is.True);
            Assert.That(b.Broken, Is.False, "dead, its fuse burning on its body");
            Assert.That(LiveOf(b).Count, Is.EqualTo(0));
            yield return Until(() => b.Broken, 6f);
            Assert.That(b.Broken, Is.True, "broken as it blows");
            Assert.That(LiveOf(b).Count, Is.GreaterThanOrEqualTo(8));
        }

        [UnityTest]
        public IEnumerator BoxesMadeInCode_TheLastChoice_StillBreak()
        {
            BoneBreakModel.ForceShapesMadeInCode = true;
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Caster, got);
            BoneBreak b = got[0].GetComponent<BoneBreak>();
            Assert.That(b.Model.Split, Is.False);
            Assert.That(b.Model.Shapes.Count(s => !s.OwnMesh), Is.EqualTo(BoneParts.Count));
            got[0].Body.TakeHit(got[0].Body.CurrentHealth);
            Assert.That(LiveOf(b).Count, Is.GreaterThanOrEqualTo(BoneParts.Count));
        }

        // ---------------- Nothing a frame ----------------

        [UnityTest]
        public IEnumerator AFrameOfThePieces_AndOfABodyHoldingALostPart_AllocatesNothing()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            yield return Spawn(EnemyKind.Guard, got);
            HurtTo(got[0], 0.5f);
            got[1].Body.TakeHit(got[1].Body.CurrentHealth);
            yield return null;
            BoneBreak b = got[0].GetComponent<BoneBreak>();
            BonePieces p = Pieces;
            p.Tick();
            b.Hold();
            Assert.That(() => p.Tick(), AllocExt.AllocatingGCMemory(Is.Not), "the pieces flying");
            Assert.That(() => b.Hold(), AllocExt.AllocatingGCMemory(Is.Not), "a body holding its lost parts off");
            yield return For(2f);
            Assert.That(() => p.Tick(), AllocExt.AllocatingGCMemory(Is.Not), "the pieces resting");
        }
    }
}
