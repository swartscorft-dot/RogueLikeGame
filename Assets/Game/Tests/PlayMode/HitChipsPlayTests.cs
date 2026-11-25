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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 127 ("kan vi drope at de flasher sånn når de blir truffet, heller at litt bein eller ligende spruter av. [...] du kan
    /// droppe den sirkelen når døde enemies despawner"), in the ControlRoom scene with enemies spawned through the practice room's
    /// intents and hit through their own health. With the switch off a hit flashes and the ring comes as before, and no chip is made.
    /// On: a hit throws chips from its side facing her, away from her along the shot, and no renderer of the enemy is flashed; a crit's
    /// spray is larger and harder; a fan of pellets into one body is one larger spray under its cap; a hit that takes a part off throws
    /// half, a killing hit that breaks the body none; the chips' stuff follows the body; they wear the shader the body wears and no ink
    /// line; they touch nothing; they lie, shrink and are gone in their time, never more than the cap, the oldest first; no ring piece
    /// is made when a body is hidden; the dummy throws chips of its own material and does not flash; a frame allocates nothing.
    /// </summary>
    public class HitChipsPlayTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        int _most;
        double _lie;
        readonly List<FxMoment> _fx = new List<FxMoment>(); // each piece's moment as it was made (a piece is gone after its short life)

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            HitChipsValues.On = true;
            HitChipsValues.RingWhenABodyGoes = false;
            BoneBreakValues.On = true;
            _most = HitChipsValues.Live.MostChips;
            _lie = HitChipsValues.Live.LieSeconds;
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
            CombatEffects.Spawned += Seen;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            CombatEffects.Spawned -= Seen;
            _fx.Clear();
            HitChipsValues.Live.MostChips = _most;
            HitChipsValues.Live.LieSeconds = _lie;
            HitChipsValues.On = true;
            HitChipsValues.RingWhenABodyGoes = false;
            BoneBreakValues.On = true;
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (CombatEffects.Current != null) Object.Destroy(CombatEffects.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (HitChips.Current != null) Object.Destroy(HitChips.Current.gameObject);
            if (BonePieces.Current != null) Object.Destroy(BonePieces.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Seen(CombatFx fx) => _fx.Add(fx.Piece.Moment);

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
            // A brute is a copy of the practice brute, which must have its brain on to be copied as in the game (see BoneBreakPlayTests).
            bool was = _practiceEnemy.enabled;
            _practiceEnemy.enabled = true;
            List<Enemy> made = _levels.SpawnPractice(kind);
            _practiceEnemy.enabled = was;
            Assert.That(made.Count, Is.GreaterThanOrEqualTo(1), kind + " spawned");
            yield return null;
            yield return null;
            foreach (Enemy m in made) Hold(m);
            for (int k = 1; k < made.Count; k++) made[k].GetComponent<NavMeshAgent>().Warp(made[0].transform.position + Vector3.back * (6f + k));
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

        static HitChips Chips => HitChips.Current;

        static List<int> LiveOf(Damageable source)
        {
            var list = new List<int>();
            HitChips c = Chips;
            if (c == null) return list;
            for (int i = 0; i < c.Made; i++)
                if (c.StateOf(i) != ChipState.Free && (source == null || c.SourceOf(i) == source)) list.Add(i);
            return list;
        }

        static bool AnyBlock(Enemy e) => e.GetComponentsInChildren<Renderer>(true).Any(r => !(r is LineRenderer) && r.HasPropertyBlock());

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator SwitchOff_AHitFlashes_TheDummyFlashes_AndTheRingComes_ExactlyAsBefore_NoChip()
        {
            HitChipsValues.On = false;
            CombatEffects.Ensure();
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Shooter, got);
            Enemy e = got[0];
            HitFlash flash = e.GetComponent<HitFlash>();
            e.Body.TakeHit(1);
            yield return null;
            Assert.That(flash.IsFlashing, Is.True, "the hit flashes, as before");
            var block = new MaterialPropertyBlock();
            e.Look.Skin.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(EnemyLook.FlashColor), "over its model, as before");
            Assert.That(Chips == null || Chips.Thrown == 0, Is.True, "no chip");

            TrainingDummy dummy = GameObject.Find("Dummy_A").GetComponent<TrainingDummy>();
            dummy.GetComponent<Damageable>().TakeHit(1);
            yield return null;
            Assert.That(dummy.IsFlashing, Is.True, "the dummy flashes, as before");

            e.enabled = true; // its brain on: it reports its death (and so its effects come) from its own frame
            e.Body.TakeHit(e.Body.CurrentHealth);
            yield return For(e.ShownDeadFor(e.Values) + 0.5f);
            Assert.That(_fx.Count(m => m == FxMoment.BodyHidden), Is.EqualTo(1), "the ring where it lay, as before");
            Assert.That(Chips == null || Chips.Thrown == 0, Is.True, "and no chip was ever made");
        }

        // ---------------- A hit ----------------

        [UnityTest]
        public IEnumerator AHit_ThrowsChipsAwayFromTheShot_FromItsSideFacingHer_AndNoRendererOfTheEnemyIsFlashed()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            Enemy e = got[0];
            HitFlash flash = e.GetComponent<HitFlash>();
            Vector3 along = Flat(e.transform.position - _player.Position).normalized;
            Assert.That(Chips, Is.Not.Null, "made ready as the enemy appeared");
            e.Body.SetMaxHealth(100000); // no part of it comes off at this hit (122): the plain spray
            yield return null;
            e.Body.TakeHit(Placeholders.BasicShotDamage);
            yield return null;
            Assert.That(flash.IsFlashing, Is.False, "no flash");
            Assert.That(AnyBlock(e), Is.False, "no renderer of it has anything laid over it");
            List<int> chips = LiveOf(e.Body);
            Assert.That(chips.Count, Is.EqualTo(HitChipsRules.Count(Placeholders.BasicShotDamage, false, HitChipsValues.Live)), "her basic shot's spray");
            Assert.That(Vector3.Dot(Flat(Chips.LastFrom - e.transform.position), along), Is.LessThan(0f), "from its side facing her");
            Assert.That(Chips.LastFrom.y - e.transform.position.y, Is.InRange(0.4f, 2.2f), "at about its chest");
            float meanAlong = chips.Average(i => Vector3.Dot(Flat(Chips.VelocityOf(i)), along));
            Assert.That(meanAlong, Is.GreaterThan(1f), "thrown along the shot");
            Assert.That(chips.All(i => Chips.VelocityOf(i).y > 0f), Is.True, "and upward, to fall");
            var from = chips.ToDictionary(i => i, i => Chips.PositionOf(i));
            yield return For(0.15f);
            foreach (int i in chips)
                Assert.That(Vector3.Dot(Flat(Chips.PositionOf(i) - from[i]), along), Is.GreaterThan(-0.2f), "none flies back at her");
            Assert.That(chips.Average(i => Vector3.Dot(Flat(Chips.PositionOf(i) - from[i]), along)), Is.GreaterThan(0.1f), "away from her");
            Assert.That(flash.IsFlashing, Is.False);
            Assert.That(AnyBlock(e), Is.False);
            // They fall to its floor and lie there.
            yield return Until(() => LiveOf(e.Body).All(i => Chips.StateOf(i) == ChipState.Lying), 3f);
            foreach (int i in LiveOf(e.Body))
                Assert.That(Chips.PositionOf(i).y - e.transform.position.y, Is.InRange(-0.01f, 0.2f), "on the floor");
        }

        [UnityTest]
        public IEnumerator ACrit_ThrowsALargerAndHarderSpray_ThanThePlainHit()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Guard, got);
            Enemy e = got[0];
            e.Body.SetMaxHealth(100000);
            yield return null;
            e.Body.TakeHit(80);
            yield return null;
            int plain = Chips.LastCount;
            float plainSpeed = Chips.LastSpeed;
            Assert.That(Chips.LastCrit, Is.False);
            e.Body.TakeHit(80);
            HitChips.Crit(e.Body); // as her stats tell it, after the hit
            yield return null;
            Assert.That(Chips.LastCrit, Is.True);
            Assert.That(Chips.LastCount, Is.EqualTo(HitChipsRules.Count(80, true, HitChipsValues.Live)));
            Assert.That(Chips.LastCount, Is.GreaterThanOrEqualTo(plain * 1.5f), "more chips");
            Assert.That(Chips.LastSpeed, Is.GreaterThan(plainSpeed * 1.3f), "thrown harder");
            Assert.That(e.GetComponent<HitFlash>().IsCritFlash, Is.False, "no hard white frame");
            Assert.That(AnyBlock(e), Is.False);
        }

        [UnityTest]
        public IEnumerator AFanOfPellets_IntoOneBody_IsOneLargerSpray_UnderItsCap()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Caster, got);
            Enemy e = got[0];
            e.Body.SetMaxHealth(100000);
            yield return null;
            int sprays = Chips.Sprays;
            for (int k = 0; k < 6; k++) e.Body.TakeHit(12);
            yield return null;
            Assert.That(Chips.Sprays, Is.EqualTo(sprays + 1), "one spray");
            Assert.That(Chips.LastCount, Is.EqualTo(HitChipsRules.ForFrame(6, 12, false, false, false, HitChipsValues.Live)));
            Assert.That(Chips.LastCount, Is.LessThanOrEqualTo(HitChipsValues.Live.MostPerTarget));
        }

        [UnityTest]
        public IEnumerator TogetherWith122_APartOff_ThrowsHalf_AKillingHitThatBreaksTheBody_None()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            Enemy e = got[0];
            BoneBreak b = e.GetComponent<BoneBreak>();
            Assume.That(b, Is.Not.Null);
            int hit = e.Body.CurrentHealth - Mathf.FloorToInt(e.Body.MaxHealth * 0.6f);
            e.Body.TakeHit(hit);
            Assert.That(b.PartsLost, Is.EqualTo(1), "its skull off");
            yield return null;
            Assert.That(Chips.LastCount, Is.EqualTo(HitChipsRules.ForFrame(1, hit, false, true, false, HitChipsValues.Live)), "half the chips under its flying part");
            int thrown = (int)Chips.Thrown;
            e.Body.TakeHit(e.Body.CurrentHealth);
            Assert.That(b.Broken, Is.True);
            yield return null;
            Assert.That((int)Chips.Thrown, Is.EqualTo(thrown), "no chips over its breaking body");
        }

        [UnityTest]
        public IEnumerator TheChips_AreOfTheBodysStuff_WearItsShader_AndNoInkLine_AndTouchNothing()
        {
            foreach (EnemyKind kind in new[] { EnemyKind.Brute, EnemyKind.Guard, EnemyKind.Caster, EnemyKind.Lancer, EnemyKind.Boss })
            {
                var got = new List<Enemy>();
                yield return Spawn(kind, got);
                Enemy e = got[0];
                e.Body.SetMaxHealth(100000);
                yield return null;
                e.Body.TakeHit(40);
                yield return null;
                ChipStuff stuff = HitChipsRules.StuffOf(kind);
                Material[] mine = { Chips.MaterialOf(HitChipsRules.First(stuff)), Chips.MaterialOf(HitChipsRules.Second(stuff)) };
                List<int> chips = LiveOf(e.Body);
                Assert.That(chips.Count, Is.GreaterThan(0), kind.ToString());
                Shader body = e.Look.Skin.sharedMaterial.shader;
                foreach (int i in chips)
                {
                    Renderer r = Chips.RendererOf(i);
                    Assert.That(mine, Does.Contain(r.sharedMaterial), kind + ": its own stuff's colours");
                    Assert.That(r.sharedMaterial.shader, Is.SameAs(body), kind + ": the shader its body wears");
                    Assert.That(ToonInkFigures.Has(r), Is.False, kind + ": not marked for the line");
                    if (r.sharedMaterial.HasProperty("_InkNeedsMark"))
                        Assert.That(r.sharedMaterial.GetFloat("_InkNeedsMark"), Is.EqualTo(1f), kind + ": its line asks for a mark no chip has: none");
                    Assert.That(r.GetComponents<Collider>(), Is.Empty, "no collider");
                    Assert.That(r.GetComponent<Rigidbody>(), Is.Null, "no rigid body");
                    Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                }
                Assert.That(e.Body.CurrentHealth, Is.EqualTo(100000 - 40), "the fight is its own");
                Object.Destroy(e.gameObject);
                yield return null;
            }
            Rgb steel = HitChipsValues.Live.Steel;
            Color c = Chips.MaterialOf(ChipColour.Steel).GetColor("_BaseColor");
            Assert.That(c.r, Is.EqualTo(steel.R).Within(0.02f), "the boss's are steel");
        }

        // ---------------- How long and how many ----------------

        [UnityTest]
        public IEnumerator TheChips_AreGoneAfterTheirTime_AndNeverMoreThanTheCap_TheOldestFirst()
        {
            HitChipsValues.Live.MostChips = 30;
            var got = new List<Enemy>();
            for (int k = 0; k < 3; k++) yield return Spawn(EnemyKind.Swarmer, got);
            foreach (Enemy e in got) e.Body.SetMaxHealth(100000);
            yield return null;
            for (int round = 0; round < 6; round++)
            {
                foreach (Enemy e in got) e.Body.TakeHit(40);
                yield return null;
                Assert.That(Chips.Live, Is.LessThanOrEqualTo(30), "the cap holds");
            }
            Assert.That(Chips.TakenFromTheOldest, Is.GreaterThan(0), "the oldest taken");
            Assert.That(LiveOf(got[2].Body).Count, Is.GreaterThanOrEqualTo(Chips.LastCount), "the newest spray keeps all its chips");
            HitChipsValues v = HitChipsValues.Live;
            yield return Until(() => Chips.Live == 0, (float)(v.MostFlySeconds + v.LieSeconds + v.ShrinkSeconds) + 3f);
            Assert.That(Chips.Live, Is.EqualTo(0), "all gone in their time");
            for (int i = 0; i < Chips.Made; i++) Assert.That(Chips.RendererOf(i).gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(Chips.Running, Is.False, "and the pool does nothing while none lives");
            Assert.That(Chips.enabled, Is.True, "its component stays on (for the play start check)");
        }

        [UnityTest]
        public IEnumerator NoRingPiece_IsMadeWhenABodyIsHidden_TheDeathsShardsStay()
        {
            CombatEffects.Ensure();
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Shooter, got);
            yield return Spawn(EnemyKind.Guard, got);
            foreach (Enemy x in got) x.enabled = true; // their brains on: each reports its death (and so its effects come) from its own frame
            BoneBreakValues.On = false; // its body lies and is hidden as before 122, so a hidden body is there to be ringed
            got[1].Body.TakeHit(got[1].Body.CurrentHealth);
            BoneBreakValues.On = true;
            got[0].Body.TakeHit(got[0].Body.CurrentHealth);
            yield return null;
            Assert.That(_fx.Count(m => m == FxMoment.EnemyDied), Is.GreaterThan(0), "the death's shards stay");
            yield return For(Mathf.Max(got[0].ShownDeadFor(got[0].Values), got[1].ShownDeadFor(got[1].Values)) + 0.5f);
            Assert.That(_fx.Count(m => m == FxMoment.BodyHidden), Is.EqualTo(0), "no ring where a body lay");

            // Its own bool gives it back.
            HitChipsValues.RingWhenABodyGoes = true;
            var more = new List<Enemy>();
            yield return Spawn(EnemyKind.Shooter, more);
            more[0].enabled = true;
            more[0].Body.TakeHit(more[0].Body.CurrentHealth);
            yield return For(more[0].ShownDeadFor(more[0].Values) + 0.5f);
            Assert.That(_fx.Count(m => m == FxMoment.BodyHidden), Is.EqualTo(1), "asked for, the ring");
        }

        // ---------------- The dummy ----------------

        [UnityTest]
        public IEnumerator TheDummy_ThrowsChipsOfItsOwnMaterial_AndDoesNotFlash()
        {
            TrainingDummy dummy = GameObject.Find("Dummy_A").GetComponent<TrainingDummy>();
            Damageable d = dummy.GetComponent<Damageable>();
            d.SetMaxHealth(100000);
            yield return null;
            d.TakeHit(40);
            yield return null;
            Assert.That(dummy.IsFlashing, Is.False, "no flash");
            Assert.That(dummy.GetComponent<Renderer>().HasPropertyBlock(), Is.False);
            List<int> chips = LiveOf(d);
            Assert.That(chips.Count, Is.EqualTo(HitChipsRules.Count(40, false, HitChipsValues.Live)));
            foreach (int i in chips) Assert.That(Chips.RendererOf(i).sharedMaterial, Is.SameAs(dummy.GetComponent<Renderer>().sharedMaterial), "its own material");
            // Her crit on it throws a crit's spray (her stats hand it on).
            d.TakeHit(80);
            HitChips.Crit(d);
            yield return null;
            Assert.That(Chips.LastCrit, Is.True);
            Assert.That(Chips.LastCount, Is.EqualTo(HitChipsRules.Count(80, true, HitChipsValues.Live)));
        }

        // ---------------- Nothing a frame ----------------

        [UnityTest]
        public IEnumerator AFrameOfThePool_Idle_Flying_AndThrowingASpray_AllocatesNothing()
        {
            var got = new List<Enemy>();
            yield return Spawn(EnemyKind.Brute, got);
            Enemy e = got[0];
            e.Body.SetMaxHealth(100000);
            yield return null;
            HitChips c = Chips;
            yield return For(0.1f);
            Assume.That(c.Running, Is.False);
            Assert.That(() => c.Frame(), AllocExt.AllocatingGCMemory(Is.Not), "idle");
            e.Body.TakeHit(40); // the first spray finds her (once)
            yield return null;
            Assert.That(() => c.Frame(), AllocExt.AllocatingGCMemory(Is.Not), "chips flying");
            Assert.That(() => { HitChips.Hit(e.Body, 40); c.Frame(); }, AllocExt.AllocatingGCMemory(Is.Not), "a hit handed on and its spray");
            yield return For(0.8f);
            Assert.That(() => c.Frame(), AllocExt.AllocatingGCMemory(Is.Not), "chips lying");
        }
    }
}
