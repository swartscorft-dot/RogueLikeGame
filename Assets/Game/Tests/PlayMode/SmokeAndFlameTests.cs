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
    /// Work order 126 (2026-11-25): her skills' smoke, flame and blast in the drawn look (<see cref="SkillLookValues.ToonLook"/>),
    /// in the ControlRoom scene through the intents. The honest extent of work order 123 holds for what this order adds:
    /// - Powder Keg's fire runs out to exactly its radius (times <see cref="SkillLookValues.FireSize"/>) in exactly the blast's
    ///   seconds, never past it, and burns out in <see cref="SkillLookValues.FireBurnOutSeconds"/>; its embers stay inside the radius;
    ///   its starburst lasts the blast's seconds; its light is lit while it burns and out after;
    /// - Smoke Round's puffs are whole while the cloud stands and eaten away (not faded) in its last
    ///   <see cref="SkillLookValues.SmokeEatSeconds"/>, gone as its time ends; the smoke is cleared round her when she stands in it;
    /// - the look is drawn with the project's own shader, and with the look off everything is drawn as 123 drew it;
    /// - a frame with fire and smoke allocates nothing.
    /// Game time runs at a fixed 1/60 s a frame. Nobody has played it.
    /// </summary>
    public class SmokeAndFlameTests
    {
        const float Metres = 0.15f;      // as SkillLookTests
        const float Seconds = 2f / 60f;  // two frames
        const float Frame = 1f / 60f;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        CombatEffects _combat;
        Vector3 _stand, _ahead, _side;
        readonly List<SkillEffects.Shown> _shown = new List<SkillEffects.Shown>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SkillLookValues.EffectsNotShapes = true;
            SkillLookValues.Live.ToonLook = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = Frame;
            PlayerInputDriver.ReadRealDevices = false;
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
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _stand = _player.Position;
            _ahead = MostRoom(_stand, 20f);
            _side = Vector3.Cross(Vector3.up, _ahead);
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d == null) continue;
                d.GetComponent<Damageable>().SetMaxHealth(10000000);
                d.transform.position = new Vector3(0f, d.transform.position.y, 0f) + Flat(_stand - _ahead * 25f + _side * (k++ * 7f - 17f));
            }
            Physics.SyncTransforms();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SkillLookValues.EffectsNotShapes = true;
            SkillLookValues.Live.ToonLook = true;
            if (_combat != null) Object.Destroy(_combat.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            SmokeClouds.Clear();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
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

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        float KegBlastSeconds => _player.ShotLooks != null && _player.ShotLooks.For("Powder Keg") != null
            ? _player.ShotLooks.For("Powder Keg").BlastSeconds : ShotFxDefaults.For("Powder Keg").BlastSeconds;

        IEnumerator BurstAKeg()
        {
            int slot = Slot("Powder Keg");
            _player.EquipSkill(slot);
            _player.StartFresh();
            Assert.That(_player.PressSkill(slot, Ahead(6f)), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(slot);
            int burst = _player.KegsBurst;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_player.KegsBurst == burst && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_player.KegsBurst, Is.GreaterThan(burst), "it burst");
        }

        [Test]
        public void TheDrawnLook_IsDrawnWithTheProjectsOwnShader_KeptInTheBuildByItsResourcesFolder()
        {
            Assert.That(SkillEffectArt.Ready, Is.True, "Resources/" + SkillEffectArt.ShaderPath + " found and supported");
            Assert.That(Resources.Load<Shader>(SkillEffectArt.ShaderPath), Is.Not.Null, "in a Resources folder: every build keeps it");
            Assert.That(SkillEffectArt.Shader.name, Is.EqualTo(SkillEffectArt.ShaderName));
        }

        [UnityTest]
        public IEnumerator PowderKeg_TheFireRunsOutToItsRadiusInTheBlastsTime_BurnsOut_AndNothingOfItPassesTheRadius()
        {
            SkillLookValues v = SkillLookValues.Live;
            SkillDefinition s = _player.Skills[Slot("Powder Keg")];
            yield return BurstAKeg();
            Vector3 c = Flat(_player.LastKegPoint);
            float burstAt = Time.time, blast = KegBlastSeconds, reach = s.Radius * Mathf.Clamp01(v.FireSize);
            SkillEffects fx = SkillEffects.Current;
            Assert.That(fx, Is.Not.Null);
            Assert.That(fx.DrawnLook, Is.True, "the drawn look");
            float fireFarthest = 0f, fireAtBlastEnd = 0f, lastFire = burstAt, lastStar = burstAt, emberFarthest = 0f;
            int stars = 0, fires = 0, flames = 0, embers = 0, litFrames = 0;
            var faults = new List<string>();
            while (Time.time < burstAt + blast + v.FireBurnOutSeconds + 1.2f)
            {
                fx.ListPieces(_shown);
                float age = Time.time - burstAt;
                foreach (SkillEffects.Shown p in _shown)
                {
                    if (!p.Toon) faults.Add(p.Use + " not in the drawn look");
                    float edge = FlatDistance(p.Foot, c) + p.Half;
                    switch (p.Use)
                    {
                        case SkillEffects.Use.Fire:
                        case SkillEffects.Use.Flame:
                            if (edge > reach + Metres) faults.Add($"{p.Use} at {age:0.000} s reaches {edge:0.00} m, past {reach:0.00}");
                            fireFarthest = Mathf.Max(fireFarthest, edge);
                            if (age <= blast + Frame * 0.5f) fireAtBlastEnd = Mathf.Max(fireAtBlastEnd, edge);
                            lastFire = Time.time;
                            if (p.Use == SkillEffects.Use.Fire) fires++; else flames++;
                            break;
                        case SkillEffects.Use.Ember:
                            if (edge > s.Radius + Metres) faults.Add($"an ember at {age:0.000} s reaches {edge:0.00} m, past the radius");
                            emberFarthest = Mathf.Max(emberFarthest, edge);
                            embers++;
                            break;
                        case SkillEffects.Use.Star:
                            Assert.That(p.Life, Is.EqualTo(blast).Within(1e-4f), "the starburst lasts the blast's seconds");
                            lastStar = Time.time;
                            stars++;
                            break;
                        case SkillEffects.Use.AfterSmoke:
                        case SkillEffects.Use.RingDust:
                            if (edge > s.Radius + Metres) faults.Add($"{p.Use} at {age:0.000} s reaches {edge:0.00} m, past the radius");
                            break;
                    }
                }
                if (fx.GlowsLit > 0)
                {
                    litFrames++;
                    if (age > blast + v.FireBurnOutSeconds + Seconds) faults.Add($"its light still lit at {age:0.000} s");
                }
                yield return null;
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults.Take(20)));
            Assert.That(fires, Is.GreaterThan(0), "fire balls drawn");
            Assert.That(flames, Is.GreaterThan(0), "tongues drawn");
            Assert.That(embers, Is.GreaterThan(0), "embers drawn");
            Assert.That(stars, Is.GreaterThan(0), "its starburst drawn (kept)");
            Assert.That(fireAtBlastEnd, Is.EqualTo(reach).Within(Metres), "the fire reaches its radius as the blast's time is out");
            Assert.That(fireFarthest, Is.LessThanOrEqualTo(reach + Metres));
            Assert.That(lastFire - burstAt, Is.LessThanOrEqualTo(blast + v.FireBurnOutSeconds + Seconds), "burnt out in its time");
            Assert.That(lastStar - burstAt, Is.LessThanOrEqualTo(blast + Seconds), "the starburst gone in the blast's seconds");
            Assert.That(litFrames, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(blast / Frame)), "its light lit while it burns");
            Assert.That(fx.GlowsLit, Is.Zero, "and out after");
        }

        [UnityTest]
        public IEnumerator SmokeRound_WholeWhileItStands_EatenAwayAtItsEnd_NotFaded_AndClearedRoundHerInIt()
        {
            SkillLookValues v = SkillLookValues.Live;
            int slot = Slot("Smoke Round");
            _player.EquipSkill(slot);
            _player.StartFresh();
            Assert.That(_player.PressSkill(slot, Ahead(1.2f)), Is.EqualTo(IntentResult.Started), "thrown round her");
            _player.ReleaseSkill(slot);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SmokeClouds.Up == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SmokeClouds.Up, Is.EqualTo(1));
            SmokeCloud cloud = SmokeClouds.At(0);
            SkillEffects fx = SkillEffects.Current;
            bool cleared = false;
            float wornMid = 1f, wornLate = 0f;
            while (SmokeClouds.Up > 0 && Time.realtimeSinceStartup < deadline + 10f)
            {
                yield return null;
                if (SmokeClouds.Up == 0) break;
                fx.ListPieces(_shown);
                List<SkillEffects.Shown> puffs = _shown.Where(p => p.Use == SkillEffects.Use.Smoke).ToList();
                Assert.That(puffs.All(p => p.Toon), Is.True, "in the drawn look");
                float now = Time.time;
                // (a puff inside the cloud may start being eaten up to 0.12 s early: SkillEffects' InnerEarly)
                if (now > cloud.From + v.SmokeSwellSeconds + v.SmokeStagger + Frame && now < cloud.Until - v.SmokeEatSeconds - 0.2f)
                    wornMid = Mathf.Min(wornMid, 1f - puffs.Max(p => p.Worn));
                if (now > cloud.Until - 2f * Frame) wornLate = Mathf.Max(wornLate, puffs.Min(p => p.Worn));
                cleared |= fx.FiguresCleared > 0;
                foreach (SkillEffects.Shown p in puffs)
                    Assert.That(p.Alpha, Is.GreaterThanOrEqualTo(0f));
            }
            Assert.That(wornMid, Is.EqualTo(1f).Within(1e-4f), "whole while it stands (not eaten, not faded)");
            Assert.That(wornLate, Is.GreaterThan(0.85f), "eaten away as its time ends");
            Assert.That(cleared, Is.True, "cleared round her while she stands in it");
            yield return null;
            Assert.That(fx.FiguresCleared, Is.Zero, "nothing cleared with no smoke");
        }

        [UnityTest]
        public IEnumerator TheLookOff_DrawsAs123_ItsSoftPuffsAndTheShotEffectsStar()
        {
            SkillLookValues.Live.ToonLook = false;
            var stars = new List<ShotFx>();
            yield return BurstAKeg();
            yield return null;
            SkillEffects fx = SkillEffects.Current;
            Assert.That(fx.DrawnLook, Is.False);
            fx.ListPieces(_shown);
            Assert.That(_shown, Is.Not.Empty);
            Assert.That(_shown.Any(p => p.Toon), Is.False, "nothing in the drawn look");
            Assert.That(_shown.Any(p => p.Use == SkillEffects.Use.Fire || p.Use == SkillEffects.Use.Flame || p.Use == SkillEffects.Use.Star), Is.False, "123 had no fire");
            Assert.That(Object.FindObjectsByType<ShotFx>(FindObjectsSortMode.None).Any(f => f.Kind == ShotFxKind.Spark), Is.True, "the shot effects' star, as 123");
        }

        [UnityTest]
        public IEnumerator AFrameWithFireAndSmoke_AllocatesNothing()
        {
            SkillEffects fx = SkillEffects.Ensure().Watch(_player);
            Vector3 c = Ahead(6f);
            fx.Blast(c, 2.5f, 0.35f); // warm
            yield return new WaitForSeconds(0.1f);
            int smoke = Slot("Smoke Round");
            _player.EquipSkill(smoke);
            _player.StartFresh();
            Assert.That(_player.PressSkill(smoke, Ahead(1.2f)), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(smoke);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SmokeClouds.Up == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            fx.Blast(c, 2.5f, 0.35f);
            fx.Step();
            Assert.That(fx.FiguresCleared, Is.GreaterThan(0), "the clearing round her runs too");
            Assert.That(() => { for (int i = 0; i < 10; i++) fx.Step(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "fire, embers, light and smoke moving");
        }
    }
}
