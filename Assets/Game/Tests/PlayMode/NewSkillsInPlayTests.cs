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
    /// Work order 109 (2026-11-24, the three new skills), narrow, in the ControlRoom scene through the intents: Ricochet's three
    /// hits on three dummies and one hit on one alone; Smoke Round's cloud stopping a shooter and a caster from beginning an attack
    /// and letting them again when it clears; Deadeye's shots at three marked dummies with a fourth behind a wall left out, and its
    /// press refused with no marked one in sight; nothing allocated while a cloud stands; the switch in a run, on and off. Every
    /// number is read from the skills. Waits are in game time or real time with a limit, never frames of a supposed length.
    /// Nobody has played any of it.
    /// </summary>
    public class NewSkillsInPlayTests
    {
        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        FeelValues _feel;
        bool _coverWas;
        Vector3 _stand, _ahead, _side;
        Transform _root;
        int _made;
        readonly List<Object> _objects = new List<Object>();
        readonly Dictionary<string, Damageable> _dummies = new Dictionary<string, Damageable>();
        static readonly string[] DummyNames = { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" };

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Roguelike.Rules.SkillLookValues.EffectsNotShapes = false; // work order 123: these pin the drawings from before; the switch on is SkillLookTests
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false; // the attacks as they begin are what is counted here, not what flies
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _feel = _player.Feel;
            _coverWas = _feel.CoverStopsShots;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _stand = _player.Position;
            // Everything is laid out ahead of her, along the way with the most floor, the practice brute well to one side.
            _ahead = MostRoom(_stand, 20f, out _);
            _side = Vector3.Cross(Vector3.up, _ahead);
            _dummies.Clear();
            int k = 0;
            foreach (string name in DummyNames)
            {
                GameObject d = GameObject.Find(name);
                if (d == null) continue;
                _dummies[name] = d.GetComponent<Damageable>();
                Put(d.transform, _stand - _ahead * 25f + _side * (k++ * 7f - 17f)); // far behind her, 7 m apart: no jump between them
            }
            Put(_practiceEnemy.transform, _stand - _ahead * 25f - _side * 30f);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Roguelike.Rules.SkillLookValues.EffectsNotShapes = true; // work order 123: back to its default
            if (_feel != null) _feel.CoverStopsShots = _coverWas; // the feel values live in an asset
            foreach (Object o in _objects) if (o != null) Object.Destroy(o);
            _objects.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            SmokeClouds.Clear();
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
            while (Time.time < end) yield return null;
        }

        static IEnumerator Synced()
        {
            Physics.SyncTransforms(); // frames without a window are far shorter than a physics step
            yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);
        Vector3 Ahead(float metres, float across = 0f) => _stand + _ahead * metres + _side * across;

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        int Equip(string name)
        {
            int slot = Slot(name);
            _player.EquipSkill(slot);
            return slot;
        }

        static float Room(Vector3 from, Vector3 dir, float max)
        {
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)) return 0f;
            if (!NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas)) return max;
            return Flat(hit.position, start.position);
        }

        static Vector3 MostRoom(Vector3 from, float max, out float room)
        {
            Vector3 best = Vector3.forward;
            room = -1f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                float r = Room(from, dir, max);
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        T Make<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            if (_root == null) _root = new GameObject("NewSkillsTestSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, Flat(at), _root, _practiceEnemy, "NewSkillsTest_" + kind + "_" + (++_made));
            Assert.That(e, Is.InstanceOf<T>());
            return (T)e;
        }

        GameObject Wall(Vector3 centre, float length, Vector3 facing)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "NewSkillsTestWall";
            wall.transform.SetPositionAndRotation(new Vector3(centre.x, 1.5f, centre.z), Quaternion.LookRotation(facing));
            wall.transform.localScale = new Vector3(length, 3f, 0.3f);
            wall.AddComponent<Cover>();
            _objects.Add(wall);
            return wall;
        }

        // ---------------- Ricochet ----------------

        [UnityTest]
        public IEnumerator Ricochet_ThreeDummies4mApart_AllThreeHit_EachOnce_AndMarked()
        {
            int slot = Equip(NewSkillRules.Ricochet);
            SkillDefinition r = _player.Skills[slot];
            Damageable a = _dummies["Dummy_A"], b = _dummies["Dummy_B"], c = _dummies["Dummy_C"];
            Put(a.transform, Ahead(4f));
            Put(b.transform, Ahead(4f, 4f));  // 4 m from A, to the side: only a jump reaches it
            Put(c.transform, Ahead(8f, 4f));  // 4 m from B, 5.7 m from A
            yield return Synced();
            int hitsA = a.HitsTaken, hitsB = b.HitsTaken, hitsC = c.HitsTaken, jumps = _player.RicochetJumpsMade;
            var dealt = new List<int>();
            void Count(int d) => dealt.Add(d);
            a.Damaged += Count; b.Damaged += Count; c.Damaged += Count;
            Assert.That(_player.PressSkill(slot, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => c.HitsTaken > hitsC, 3f);
            yield return For(0.2f);
            Assert.That(a.HitsTaken - hitsA, Is.EqualTo(1), "the first: where she aimed");
            Assert.That(b.HitsTaken - hitsB, Is.EqualTo(1), "the first jump");
            Assert.That(c.HitsTaken - hitsC, Is.EqualTo(1), "the second jump");
            Assert.That(_player.RicochetJumpsMade - jumps, Is.EqualTo(2));
            Assert.That(dealt, Is.EqualTo(new[] { r.Damage, r.Damage, r.Damage }), "its damage on each, under a Quick Shot's");
            Assert.That(new[] { a.MarkCount, b.MarkCount, c.MarkCount }, Is.EqualTo(new[] { 1, 1, 1 }), "a mark each");
            foreach (string other in new[] { "Dummy_D", "Dummy_E", "Dummy_F" })
                if (_dummies.TryGetValue(other, out Damageable d)) Assert.That(d.MarkCount, Is.Zero, other + " untouched");
        }

        [UnityTest]
        public IEnumerator Ricochet_OneDummyAlone_IsOneHit()
        {
            int slot = Equip(NewSkillRules.Ricochet);
            Damageable a = _dummies["Dummy_A"];
            Put(a.transform, Ahead(5f));
            yield return Synced();
            int hits = a.HitsTaken, jumps = _player.RicochetJumpsMade, fired = _player.SkillShotsFired;
            Assert.That(_player.PressSkill(slot, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => a.HitsTaken > hits, 3f);
            yield return For(0.3f);
            Assert.That(a.HitsTaken - hits, Is.EqualTo(1), "one hit");
            Assert.That(_player.RicochetJumpsMade - jumps, Is.Zero, "nothing within 5 m to jump to");
            Assert.That(_player.SkillShotsFired - fired, Is.EqualTo(1));
        }

        // ---------------- Smoke Round ----------------

        EnemyShooter _shooter;
        EnemyCaster _caster;
        int _shooterBegun, _casterBegun;

        /// <summary>A shooter and a caster side by side 8 m ahead, asleep, standing still; their attacks begun counted.</summary>
        IEnumerator TwoThatHoldTheirPlace()
        {
            _shooter = Make<EnemyShooter>(EnemyKind.Shooter, Ahead(8f, -1f));
            _caster = Make<EnemyCaster>(EnemyKind.Caster, Ahead(8f, 1f));
            _shooter.Tuning.Enemy.MoveSpeed = 0f;
            _shooter.Tuning.Enemy.AggroRange = 0.5f;
            _caster.Tuning.Enemy.MoveSpeed = 0f;
            _caster.Tuning.Enemy.AggroRange = 0.5f;
            _shooterBegun = _casterBegun = 0;
            _shooter.Reported += e => { if (e.Kind == EnemyReportKind.AttackStarted) _shooterBegun++; };
            _caster.Reported += e => { if (e.Kind == EnemyReportKind.AttackStarted) _casterBegun++; };
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SmokeRound_OnAShooterAndACaster_TheyBeginNoAttack_WhileItStands_AndDoAgainWhenItClears()
        {
            int slot = Equip(NewSkillRules.SmokeRound);
            SkillDefinition s = _player.Skills[slot];
            yield return TwoThatHoldTheirPlace();
            Assert.That(_shooter.SeesHer() && _caster.SeesHer(), Is.True, "before the smoke both see her");

            Assert.That(_player.PressSkill(slot, Ahead(8f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => SmokeClouds.Up > 0, 2f);
            Assert.That(SmokeClouds.Up, Is.EqualTo(1), "a cloud stands");
            SmokeCloud cloud = SmokeClouds.At(0);
            Assert.That(cloud.Radius, Is.EqualTo(s.Radius));
            Assert.That(Flat(new Vector3(cloud.Centre.X, 0f, cloud.Centre.Z), Ahead(8f)), Is.LessThan(0.05f), "where she threw it");
            Assert.That(SmokeClouds.ViewAt(0), Is.Not.Null, "and is drawn");
            Assert.That(_shooter.SeesHer(), Is.False, "the shooter in it does not see her");
            Assert.That(_caster.SeesHer(), Is.False, "nor the caster");
            float clearsAt = cloud.Until;

            // Woken in the smoke, with her in their reach: neither begins an attack while it stands.
            _shooter.Body.TakeHit(1);
            _caster.Body.TakeHit(1);
            yield return Until(() => Time.time >= clearsAt - 0.05f, s.SmokeSeconds + 2f);
            Assert.That(_shooter.IsAwake && _caster.IsAwake, Is.True, "both awake");
            Assert.That(_shooterBegun, Is.Zero, "no shot begun in the smoke");
            Assert.That(_casterBegun, Is.Zero, "no cast begun in the smoke");

            yield return Until(() => SmokeClouds.Up == 0, 2f);
            Assert.That(SmokeClouds.Up, Is.Zero, "cleared");
            Assert.That(_shooter.SeesHer() && _caster.SeesHer(), Is.True, "they see her again");
            yield return Until(() => _shooterBegun > 0 && _casterBegun > 0, 8f);
            Assert.That(_shooterBegun, Is.GreaterThan(0), "the shooter shoots again");
            Assert.That(_casterBegun, Is.GreaterThan(0), "the caster casts again");
            yield return null;
            Assert.That(Object.FindObjectsByType<SmokeCloudView>(FindObjectsSortMode.None), Is.Empty, "its picture is gone");
        }

        [UnityTest]
        public IEnumerator SmokeRound_BetweenHerAndAShooter_ItDoesNotSeeHerThroughIt()
        {
            int slot = Equip(NewSkillRules.SmokeRound);
            yield return TwoThatHoldTheirPlace();
            Assert.That(_player.PressSkill(slot, Ahead(4f)), Is.EqualTo(IntentResult.Started)); // the cloud between, 4 m from both
            yield return Until(() => SmokeClouds.Up > 0, 2f);
            Assert.That(SmokeClouds.Hides(_shooter.transform.position, _player.Position), Is.True, "its line to her crosses the cloud");
            Assert.That(NewSkillRules.Inside(new GroundPoint(_shooter.transform.position.x, _shooter.transform.position.z), SmokeClouds.At(0)), Is.False,
                        "it does not stand in it");
            Assert.That(_shooter.SeesHer(), Is.False);
            _player.Teleport(_stand + _side * 9f); // she steps out to the side: the line misses the cloud
            yield return null;
            Assert.That(_shooter.SeesHer(), Is.True, "seen past the cloud's side");
        }

        [UnityTest]
        public IEnumerator WhileACloudStands_ItsAskingAndItsPicture_AllocateNothing()
        {
            int slot = Equip(NewSkillRules.SmokeRound);
            yield return TwoThatHoldTheirPlace();
            Assert.That(_player.PressSkill(slot, Ahead(8f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => SmokeClouds.Up > 0, 2f);
            Assert.That(SmokeClouds.Up, Is.EqualTo(1));
            Vector3 her = _player.Position, at = _shooter.transform.position;
            bool seen = true;
            // The measure is the test framework's own; first, that it sees one.
            Assert.That(() => { _junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            for (int i = 0; i < 10; i++) seen &= _shooter.SeesHer() | _caster.SeesHer() | SmokeClouds.Hides(at, her);
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++) seen &= _shooter.SeesHer() | _caster.SeesHer() | SmokeClouds.Hides(at, her) | SmokeClouds.Up > 0;
            }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "the enemies' question and the clouds' count allocate nothing");
            Assert.That(seen, Is.True);
            // Its picture changes nothing in a frame: its mesh is made once.
            SmokeCloudView view = SmokeClouds.ViewAt(0);
            Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
            yield return For(0.5f);
            Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh), "the same mesh, not made again");
        }

        static byte[] _junk;

        // ---------------- Deadeye ----------------

        [UnityTest]
        public IEnumerator Deadeye_ThreeMarkedDummies_EachShotOnce_TheirMarksSpent_TheOneBehindAWallLeftOut()
        {
            _feel.CoverStopsShots = true;
            int slot = Equip(NewSkillRules.Deadeye);
            SkillDefinition d = _player.Skills[slot];
            Damageable a = _dummies["Dummy_A"], b = _dummies["Dummy_B"], c = _dummies["Dummy_C"], hid = _dummies["Dummy_D"];
            Put(a.transform, Ahead(4f, -3f));
            Put(b.transform, Ahead(6f, 3f));
            Put(c.transform, Ahead(9f));
            Put(hid.transform, _stand - _side * 7f);
            Wall(_stand - _side * 4f, 4f, _side); // between her and the fourth
            yield return Synced();
            KitValues kit = _player.Kit;
            int[] marks = { 1, 3, 5, 2 };
            Damageable[] all = { a, b, c, hid };
            for (int i = 0; i < all.Length; i++) all[i].AddMarks(marks[i], kit.MaxMarks, 60f);
            var dealt = new Dictionary<Damageable, int>();
            foreach (Damageable t in all)
            {
                Damageable target = t;
                target.Damaged += x => dealt[target] = (dealt.TryGetValue(target, out int had) ? had : 0) + x;
            }
            int shots = _player.DeadeyeShotsFired;

            Assert.That(_player.PressSkill(slot, Ahead(5f)), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.ActiveSkill, Is.Not.Null, "its cast: the aim of half a second");
            yield return For(d.Startup * 0.5f);
            Assert.That(dealt, Is.Empty, "nothing before the cast is done");
            yield return Until(() => _player.DeadeyeShotsFired > shots, 3f);
            yield return For(0.2f);
            Assert.That(_player.DeadeyeShotsFired - shots, Is.EqualTo(3), "a shot at each of the three in sight");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(dealt.TryGetValue(all[i], out int got) ? got : 0, Is.EqualTo(NewSkillRules.DeadeyeDamage(d.Damage, marks[i], d.MarkDamageBonus)),
                            all[i].name + ": its damage and its bonus for " + marks[i] + " marks");
                Assert.That(all[i].MarkCount, Is.Zero, all[i].name + ": its marks spent");
            }
            Assert.That(dealt.ContainsKey(hid), Is.False, "the one behind the wall is not shot");
            Assert.That(hid.MarkCount, Is.EqualTo(marks[3]), "and keeps its marks");
        }

        [UnityTest]
        public IEnumerator Deadeye_WithNoMarkedEnemyInSight_ThePressIsRefused_AndItsCooldownNotSpent()
        {
            int slot = Equip(NewSkillRules.Deadeye);
            Damageable a = _dummies["Dummy_A"];
            Put(a.transform, Ahead(5f));
            yield return Synced();
            int refused = _player.DeadeyeRefused;
            Assert.That(_player.PressSkill(slot, a.transform.position), Is.EqualTo(IntentResult.Rejected), "no mark on it");
            Assert.That(_player.ActiveSkill, Is.Null, "nothing started");
            Assert.That(_player.SkillCooldownRemaining(slot), Is.Zero, "no cooldown spent");
            Assert.That(_player.DeadeyeRefused - refused, Is.EqualTo(1));
            a.AddMarks(1, _player.Kit.MaxMarks, 60f);
            Assert.That(_player.PressSkill(slot, a.transform.position), Is.EqualTo(IntentResult.Started), "marked: it goes");
        }

        // ---------------- The switch, in a run ----------------

        [UnityTest]
        public IEnumerator InARun_TheSwitchOn_HerListHoldsFifteen_Off_TheTwelve_AndTheTableWithoutTheirRows()
        {
            _levels.StartRun(0x0109CAFEUL);
            yield return null;
            Assert.That(_player.Skills.Count, Is.EqualTo(15), "on: the twelve and the three");
            Assert.That(_player.Skills.Skip(12).Select(s => s.Name), Is.EqualTo(NewSkillRules.Names));
            Assert.That(_levels.UpgradeTableNow.Count(u => NewSkillRules.IsNew(u.Skill)), Is.EqualTo(6));

            _levels.RunValues.NewSkills = false;
            KitSwitchesForTests.SkillsOnlyReward(_levels); // changed deliberately (work order 138): the three skills of the reward of before, pinned here
            _levels.StartRun(0x0109CAFEUL);
            yield return null;
            Assert.That(_player.Skills.Count, Is.EqualTo(12), "off: the twelve");
            Assert.That(_player.Skills.Any(s => NewSkillRules.IsNew(s.Name)), Is.False);
            Assert.That(_levels.UpgradeTableNow.Any(u => NewSkillRules.IsNew(u.Skill)), Is.False);
            _levels.FinishFloorForTests();
            for (int i = 0; i < 600 && _levels.Choice != GeneratedLevels.ChoiceKind.FloorReward; i++) yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            Assert.That(_levels.OfferedSkills, Is.EqualTo(FloorRewards.Offer(0x0109CAFEUL, 1, Enumerable.Range(0, 12).Select(_player.IsEquipped).ToList(), 3)),
                        "the floor reward drawn from the eight, as before");
            Assert.That(_levels.OfferedSkills.All(s => s < 12), Is.True);
            _levels.RunValues.NewSkills = true;
        }
    }
}
