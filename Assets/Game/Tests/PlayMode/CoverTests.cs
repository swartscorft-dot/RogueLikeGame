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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Walls and tall props stop her shots and skills (work order 010; Kristian, 2026-11-19: "Skudd skal stoppes av vegg
    /// og søyler"), in the ControlRoom scene through the intents. Most tests build their own cover in the practice room,
    /// as a generated level builds it: props from <see cref="PropFactory"/> and walls as grey cubes, on the walls' layer
    /// (Default), with <see cref="Cover"/> on the walls and on the props tall enough to be cover. One test shoots at a
    /// wall of a real generated level.
    ///
    /// She stands at the practice room's middle and everything is laid out ahead of her along <c>_ahead</c>, the axis
    /// that points most away from the practice brute (switched off, as in the enemy tests), with the dummies not in use
    /// put behind her. Physics is synced after cover is built (<see cref="Physics.SyncTransforms"/>, and a frame), since
    /// frames without a window are far shorter than a physics step; no assertion counts frames.
    /// </summary>
    public class CoverTests
    {
        const int Quick = 0, Keg = 1, Scatter = 4;
        static readonly Vector3 Stand = Vector3.zero;

        PlayerCharacter _player;
        FeelValues _feel;
        bool _coverWas;
        Damageable _a, _b;
        Vector3 _ahead, _side;
        GeneratedLevels _levels;
        readonly List<Object> _made = new List<Object>();
        readonly HashSet<ShotFx> _seen = new HashSet<ShotFx>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false; // work order 036: these are the tests of the attacks as they were
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _feel = _player.Feel;
            _coverWas = _feel.CoverStopsShots;
            _feel.CoverStopsShots = true;
            _seen.Clear();

            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice enemy");
            brute.enabled = false;
            Vector3 away = Flat(Stand - brute.transform.position);
            Vector3[] axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            _ahead = axes.OrderByDescending(a => Vector3.Dot(a, away.normalized)).First();
            _side = Vector3.Cross(Vector3.up, _ahead);

            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            int k = 0;
            foreach (string name in new[] { "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Put(d.transform, Stand - _ahead * 12f + _side * (k++ * 3f - 4.5f));
            }
            Put(_a.transform, Stand - _ahead * 9f + _side * 6f);
            Put(_b.transform, Stand - _ahead * 9f - _side * 6f);
            _player.Teleport(Stand);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_feel != null) _feel.CoverStopsShots = _coverWas; // the feel values live in an asset
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The basic shot ----------------

        [UnityTest]
        public IEnumerator ABasicShot_AtATargetBehindAPillar_DoesNoDamage_AndItsStreakEndsAtThePillar()
        {
            GameObject pillar = Prop(PropModel.Pillar, Ahead(5f));
            Assert.That(pillar.GetComponent<Cover>(), Is.Not.Null, "a pillar is cover");
            Put(_a.transform, Ahead(8f));
            yield return Synced();

            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            ShotFx streak = Newest(ShotFxKind.Streak);
            ShotFx spark = Newest(ShotFxKind.Spark);
            yield return new WaitForSeconds(0.1f);

            float face = 5f - (float)PropSize.Of(PropModel.Pillar).Radius;
            Assert.That(_a.HitsTaken, Is.EqualTo(0), "the pillar took the shot");
            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(1));
            Assert.That(streak, Is.Not.Null, "the shot shows its streak");
            Assert.That(Along(streak.End), Is.EqualTo(face).Within(0.05f), "the streak ends at the pillar's face");
            Assert.That(Across(streak.End), Is.EqualTo(0f).Within(0.05f));
            Assert.That(spark, Is.Not.Null, "with a hit spark");
            Assert.That(FlatDistance(spark.Origin, streak.End), Is.LessThan(0.05f), "where the streak ends");
        }

        [UnityTest]
        public IEnumerator ABasicShot_PastAPillarsEdge_Hits()
        {
            // The pillar's edge is 0.3 m off the shot's centre line, inside its half width: the side grazes it.
            float r = (float)PropSize.Of(PropModel.Pillar).Radius;
            Assume.That(_feel.BasicShotHalfWidth, Is.GreaterThan(0.3f));
            Prop(PropModel.Pillar, Ahead(5f) + _side * (r + 0.3f));
            Put(_a.transform, Ahead(8f));
            yield return Synced();

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "a shot whose side grazes a pillar is not stopped");
            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ABasicShot_OverALowProp_Hits()
        {
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, Ahead(5f));
            Assert.That(sarcophagus.GetComponent<Cover>(), Is.Null, "a sarcophagus is not cover");
            Put(_a.transform, Ahead(8f));
            yield return Synced();
            Assert.That(Physics.Raycast(Stand + Vector3.up * Cover.RayHeight, _ahead, out RaycastHit hit, 8f), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(sarcophagus), "its collider stands in the line: the test means something");

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "she shoots over it");
            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ABlockedShot_AddsNoMark_AndNoFocus()
        {
            Prop(PropModel.Pillar, Ahead(5f));
            Put(_a.transform, Ahead(8f));
            yield return Synced();
            SkillDefinition quick = _player.Skills[Quick];
            Assume.That(quick.MarksAdded, Is.GreaterThan(0), "Quick Shot is a builder");
            Assume.That(quick.Range, Is.GreaterThan(8f));
            float focus = _player.Focus;

            int shots = _player.Kit.BasicShotsPerMark;
            for (int i = 0; i < shots; i++)
            {
                _player.Shoot(_a.transform.position);
                yield return Until(() => _player.BasicShotsFired > i, 2f);
                yield return Until(() => _player.Action == ActionKind.None, 2f);
            }
            Assert.That(_player.BasicShotsFired, Is.EqualTo(shots));
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillShotsFired >= 1 && _player.Action == ActionKind.None, 3f);
            yield return new WaitForSeconds(0.1f);

            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(shots + 1), "every shot was stopped, the skill's too");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));
            Assert.That(_a.MarkCount, Is.EqualTo(0), "no marks");
            Assert.That(_player.BasicShotsTowardMark, Is.EqualTo(0), "no basic shot hit counted toward a mark");
            Assert.That(_player.Focus, Is.EqualTo(focus), "no focus");
        }

        // ---------------- Skills ----------------

        [UnityTest]
        public IEnumerator ScatterBlast_HitsATargetInTheOpen_NotOneBehindAWall_InTheSameCone()
        {
            SkillDefinition scatter = _player.Skills[Scatter];
            Assert.That(scatter.Shape, Is.EqualTo(SkillShape.Cone));
            // A wall over one side of the cone, 2 m out, from 0.5 m to 3 m off the middle.
            Wall(Ahead(2f) + _side * 1.75f, 2.5f);
            Put(_a.transform, Ahead(3.5f) - _side * 1f);
            Put(_b.transform, Ahead(3.5f) + _side * 1.5f);
            yield return Synced();
            foreach (Damageable d in new[] { _a, _b })
                Assume.That(SkillRules.InCone(_player.GroundPosition, Ground(Ahead(3.5f)), scatter.Range, scatter.Angle, d.GroundPosition, d.Radius),
                            Is.True, d.name + " is in the cone");

            Assert.That(_player.PressSkill(Scatter, Ahead(3.5f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillShotsFired >= 1 && _player.Action == ActionKind.None, 3f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.GreaterThanOrEqualTo(1), "the target in the open is hit");
            Assert.That(_b.HitsTaken, Is.EqualTo(0), "the one behind the wall is not");
        }

        [UnityTest]
        public IEnumerator PowderKeg_AimedPastAWall_LandsShortOfIt_AndDoesNotHurtWhatIsBehind()
        {
            _player.EquipSkill(Keg);
            SkillDefinition keg = _player.Skills[Keg];
            Assert.That(keg.Shape, Is.EqualTo(SkillShape.Circle));
            const float thickness = 0.3f;
            Wall(Ahead(4f), 6f, thickness);
            Put(_a.transform, Ahead(2.5f) + _side * 0.5f); // her side of the wall
            Put(_b.transform, Ahead(6f));                  // the far side, where it was aimed
            yield return Synced();
            Assume.That(keg.Range, Is.GreaterThanOrEqualTo(6f));

            Assert.That(_player.PressSkill(Keg, Ahead(6f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillShotsFired >= 1, 3f);
            ShotFx outline = Newest(ShotFxKind.Outline);
            yield return Until(() => _player.KegsBurst > 0, 4f); // work order 015: it lies on its fuse, then bursts
            yield return new WaitForSeconds(0.1f);

            Assert.That(outline, Is.Not.Null, "the circle shows where it landed");
            float landed = 4f - thickness * 0.5f - CoverRules.ShortOfCover;
            Assert.That(Along(outline.Origin), Is.EqualTo(landed).Within(0.05f), "0.3 m short of the wall");
            Assert.That(Across(outline.Origin), Is.EqualTo(0f).Within(0.05f));
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "it hurts what is on her side");
            Assert.That(_b.HitsTaken, Is.EqualTo(0), "and not what is on the far side");
        }

        // ---------------- The switch ----------------

        [UnityTest]
        public IEnumerator WithTheSwitchOff_EverythingPassesThroughAsBefore()
        {
            _feel.CoverStopsShots = false;
            _player.EquipSkill(Keg);
            Prop(PropModel.Pillar, Ahead(5f));
            Wall(Ahead(2f) + _side * 1.75f, 2.5f);
            Wall(Ahead(4f), 6f);
            Put(_a.transform, Ahead(8f));
            yield return Synced();

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            ShotFx streak = Newest(ShotFxKind.Streak);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the basic shot passes through the wall and the pillar");
            Assert.That(streak, Is.Not.Null);
            Assert.That(Along(streak.End), Is.EqualTo(Along(_a.transform.position)).Within(0.05f), "its streak runs to the target");
            yield return Until(() => _player.Action == ActionKind.None, 2f);

            Put(_b.transform, Ahead(3.5f) + _side * 1.5f);
            yield return null;
            Assert.That(_player.PressSkill(Scatter, Ahead(3.5f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _b.HitsTaken >= 1, 3f);
            Assert.That(_b.HitsTaken, Is.EqualTo(1), "Scatter Blast reaches behind the wall");
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 4f);

            Put(_b.transform, Ahead(6f));
            yield return null;
            Assert.That(_player.PressSkill(Keg, Ahead(6f)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _b.HitsTaken >= 2, 3f);
            Assert.That(_b.HitsTaken, Is.EqualTo(2), "Powder Keg lands where it was aimed, past the wall");
            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(0));
        }

        // ---------------- The shooter ----------------

        [UnityTest]
        public IEnumerator TheShooter_DoesNotShootThroughAWall_ButShootsOverALowProp()
        {
            _levels = GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(made.Count, Is.EqualTo(1));
            var shooter = (EnemyShooter)made[0];
            shooter.Tuning.Enemy.MoveSpeed = 0f; // it would walk round: here it must stand where it is
            Vector3 her = _player.Position, it = shooter.transform.position;
            Vector3 mid = Flat((her + it) * 0.5f);
            Vector3 along = Flat(it - her).normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, along);
            int k = 0;
            foreach (Damageable d in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Select(t => t.GetComponent<Damageable>()))
                if (d != null) Put(d.transform, her + perp * (8f + 1.5f * k++)); // well off its line

            GameObject wall = Wall(mid, 4f, 0.5f, along);
            yield return Synced();
            Assert.That(shooter.Sees(her), Is.False, "the wall is between them");
            Object.Destroy(wall);
            yield return null;

            int turn = Mathf.RoundToInt(Quaternion.LookRotation(along).eulerAngles.y);
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, mid, ((turn % 360) + 360) % 360);
            yield return Synced();
            float sight = shooter.Tuning.SightHeight;
            Vector3 from = Flat(it) + Vector3.up * sight, to = Flat(her) + Vector3.up * sight;
            Assert.That(Physics.Raycast(from, (to - from).normalized, out RaycastHit hit, Vector3.Distance(from, to)), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(sarcophagus), "the low prop stands in its line of sight: before this order it hid her");
            Assert.That(shooter.Sees(her), Is.True, "it sees over the low prop");

            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            int before = health.Current;
            shooter.Body.TakeHit(1);
            yield return Until(() => shooter.Strikes > 0, 6f);
            yield return null;
            Assert.That(shooter.Strikes, Is.GreaterThan(0), "it shoots");
            Assert.That(shooter.StrikesLanded, Is.EqualTo(shooter.Strikes), "and its line reaches her over the low prop");
            Assert.That(health.Current, Is.LessThan(before));
            _levels.ClearSpawned();
        }

        // ---------------- A generated level ----------------

        [UnityTest]
        public IEnumerator AGeneratedLevel_ItsWallsAndTallPropsAreCover_AndAShotStopsAtAWall()
        {
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.Build(42UL);
            Assert.That(level, Is.Not.Null);
            yield return null;

            Transform[] walls = level.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Wall_") || t.name.StartsWith("LowWall_")).ToArray();
            Assert.That(walls.Length, Is.EqualTo(level.Walls));
            Assert.That(walls.All(w => w.GetComponent<Cover>() != null), "every wall is cover, the low ones too");
            PropValues values = level.Values.PropRules;
            foreach (GeneratedLevel.PlacedProp p in level.Props)
            {
                bool tall = p.Prop.Height >= values.CoverHeight - 1e-3 && !p.Prop.Slender; // work order 028: a slender prop is never cover
                Assert.That(p.IsCover, Is.EqualTo(tall), p.Prop.ToString());
                Assert.That(p.Object.GetComponent<Cover>() != null, Is.EqualTo(tall), "cover as tall as " + values.CoverHeight + " m: " + p.Prop);
            }
            Assert.That(level.Props.Any(p => p.IsCover) && level.Props.Any(p => !p.IsCover), "both kinds stand in this level");

            // From a cell next to the start room's middle, a shot at the room's wall on that side, where there is no door.
            LevelLayout layout = level.Layout;
            LevelRoom room = layout.Rooms[0];
            float cs = layout.CellSize, half = level.Values.WallThickness * 0.5f;
            LevelPoint middle = room.Middle(cs);
            bool found = false, alongX = false;
            Vector3 at = default, dir = default;
            int sign = 0, wallLine = 0;
            foreach (int sx in new[] { 1, -1 })
            foreach (int sy in new[] { 1, -1 })
            foreach (bool east in new[] { true, false })
            {
                if (found) break;
                var p = new LevelPoint(middle.X + sx * cs * 0.5, middle.Y + sy * cs * 0.5);
                int cellX = (int)Math.Floor(p.X / cs), cellY = (int)Math.Floor(p.Y / cs);
                int line = east ? (sx > 0 ? room.Rect.Right : room.Rect.X) : (sy > 0 ? room.Rect.Top : room.Rect.Y);
                int cell = east ? cellY : cellX;
                if (layout.Doors.Any(d => d.Vertical == east && d.Line == line && cell >= d.From && cell < d.From + d.Width)) continue;
                if (!NavMesh.SamplePosition(level.ToWorld(p), out NavMeshHit onMesh, 0.1f, NavMesh.AllAreas)) continue;
                found = true;
                alongX = east;
                at = onMesh.position;
                sign = east ? sx : sy;
                wallLine = line;
                dir = east ? new Vector3(sx, 0f, 0f) : new Vector3(0f, 0f, sy);
            }
            Assert.That(found, "a cell beside the start room's middle with a wall, not a door, on its side\n" + LevelMap.Draw(layout));

            _player.Teleport(at);
            yield return null;
            yield return null;
            Vector3 stood = _player.Position;
            LevelPoint q = level.ToLevel(stood);
            float expected = (float)(sign * (wallLine * cs - (alongX ? q.X : q.Y))) - half; // to the wall's face
            Assume.That(expected, Is.LessThan(_feel.BasicShotRange));
            _player.Shoot(stood + dir * 9f);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            ShotFx streak = Newest(ShotFxKind.Streak);
            Assert.That(_player.ShotsStoppedByCover, Is.EqualTo(1), "the wall stopped it");
            Assert.That(FlatDistance(_player.LastCoverStop, stood), Is.EqualTo(expected).Within(0.05f), "at the wall's face");
            Assert.That(streak, Is.Not.Null);
            Assert.That(FlatDistance(streak.End, _player.LastCoverStop), Is.LessThan(0.05f), "and its streak ends there");
            _levels.BackToPracticeRoom();
        }

        // ---------------- Helpers ----------------

        Vector3 Ahead(float metres) => Stand + _ahead * metres;
        float Along(Vector3 p) => Vector3.Dot(Flat(p - _player.Position), _ahead);
        float Across(Vector3 p) => Vector3.Dot(Flat(p - _player.Position), _side);

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>A prop as a level builds it: on the walls' layer, cover when it is tall enough.</summary>
        GameObject Prop(PropModel model, Vector3 at, int rotation = -1)
        {
            if (rotation < 0) rotation = Mathf.RoundToInt(Quaternion.LookRotation(_ahead).eulerAngles.y) % 360; // its long side across the line
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 0.9f);
            if (prop.IsCover(new PropValues())) go.AddComponent<Cover>();
            _made.Add(go);
            return go;
        }

        /// <summary>A grey wall as a level builds it, 3 m high, <paramref name="length"/> across <paramref name="facing"/> (ahead by default).</summary>
        GameObject Wall(Vector3 centre, float length, float thickness = 0.3f, Vector3? facing = null)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(centre.x, 1.5f, centre.z), Quaternion.LookRotation(facing ?? _ahead));
            wall.transform.localScale = new Vector3(length, 3f, thickness);
            wall.AddComponent<Cover>();
            _made.Add(wall);
            return wall;
        }

        /// <summary>Colliders made this frame are where their transforms are only once physics is synced.</summary>
        static IEnumerator Synced()
        {
            Physics.SyncTransforms();
            yield return null;
        }

        /// <summary>The newest effect of a kind that was not there at the last call.</summary>
        ShotFx Newest(ShotFxKind kind)
        {
            ShotFx found = null;
            foreach (ShotFx fx in Object.FindObjectsByType<ShotFx>())
                if (fx.Kind == kind && _seen.Add(fx)) found = fx;
            return found;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static GroundPoint Ground(Vector3 v) => new GroundPoint(v.x, v.z);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
