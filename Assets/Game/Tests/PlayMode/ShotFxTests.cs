using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The shot effects: a flash at a pistol's barrel end, a streak along the shot's path, a spark on every
    /// target hit, and a blast over the area of some skills. They are pictures only and change nothing about
    /// the hit. Values come from the settings assets in the scene. Where a barrel end is, is worked out here
    /// from the bounds of the pistol's mesh, not read from the muzzles the scene builder placed.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3), a group D, E, F at about (-10.5, 6.7).
    /// </summary>
    public class ShotFxTests
    {
        const int Quick = 0, Keg = 1, Fan = 2, Scatter = 4, Volley = 5, Triple = 7; // key order: Q W E R, S D F G
        const float AtTheBarrel = 0.03f; // m

        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        CharacterAnimation _view;
        Damageable _a;

        /// <summary>An effect as it was in the frame it appeared, and which pistol's barrel end was nearest to its origin.</summary>
        struct Seen
        {
            public ShotFx Fx;
            public ShotFxKind Kind;
            public Vector3 Origin, End, Direction;
            public float Size, Angle, Seconds, At;
            public int Frame;
            public Transform NearestWeapon;
            public float FromBarrel;
        }

        readonly List<Seen> _seen = new List<Seen>();
        readonly HashSet<ShotFx> _known = new HashSet<ShotFx>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();

            // The real mouse and keyboard must not steer a test.
            _driver.InputSource = () => null;
            _seen.Clear();
            _known.Clear();
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The basic shot ----------------

        [UnityTest]
        public IEnumerator BasicShot_FlashAndStreakFromABarrelEnd_SparkOnTheTarget_AndAllRemoveThemselves()
        {
            AssumePistols();
            _player.Teleport(NearA);
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<ShotFx>(), Is.Empty, "no effects before the shot");

            _player.Shoot(_a.transform.position);
            yield return Watching(() => _player.BasicShotsFired >= 1, 3f);
            _player.Stop();
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "the shot went off");
            Assert.That(Of(ShotFxKind.Flash).Count, Is.EqualTo(1), "one flash");
            Assert.That(Of(ShotFxKind.Streak).Count, Is.EqualTo(1), "one streak");
            Assert.That(Of(ShotFxKind.Spark).Count, Is.EqualTo(1), "one spark");
            Assert.That(_seen.Count, Is.EqualTo(3), "and nothing else");

            Seen flash = Of(ShotFxKind.Flash)[0], streak = Of(ShotFxKind.Streak)[0], spark = Of(ShotFxKind.Spark)[0];
            Assert.That(flash.FromBarrel, Is.LessThan(AtTheBarrel), "the flash is at the barrel end of " + Hand(flash));
            Assert.That(streak.FromBarrel, Is.LessThan(AtTheBarrel), "the streak starts at a barrel end");
            Assert.That(Vector3.Distance(flash.Origin, streak.Origin), Is.LessThan(1e-3f), "the same one");
            Assert.That(FlatDistance(streak.End, _a.transform.position), Is.LessThan(0.01f), "the streak ends at the target");
            Assert.That(Vector3.Dot(streak.Direction, Flat(_a.transform.position - streak.Origin).normalized), Is.GreaterThan(0.999f));
            Assert.That(FlatDistance(spark.Origin, _a.transform.position), Is.LessThanOrEqualTo(_a.Radius + 0.01f), "the spark is on the target");

            float due = _seen.Max(s => s.At + s.Seconds) + 0.05f;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.time < due && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(due), "waited out their time");
            foreach (Seen s in _seen) Assert.That(s.Fx == null, Is.True, s.Kind + " is gone");
            Assert.That(Object.FindObjectsByType<ShotFx>(), Is.Empty, "and no effect is left");
        }

        [UnityTest]
        public IEnumerator TwoShotsInARow_ComeFromDifferentHands()
        {
            AssumePistols();
            _player.Teleport(NearA);
            yield return null;

            _player.Shoot(_a.transform.position);
            bool secondClick = false;
            yield return Watching(() =>
            {
                // Click to shoot: a second click for the second shot.
                if (!secondClick && _player.BasicShotsFired >= 1) { _player.Shoot(_a.transform.position); secondClick = true; }
                return _player.BasicShotsFired >= 2;
            }, 3f);
            _player.Stop();
            List<Seen> streaks = Of(ShotFxKind.Streak);
            Assert.That(streaks.Count, Is.EqualTo(2), "two shots, one streak each");
            Assert.That(streaks[0].FromBarrel, Is.LessThan(AtTheBarrel), "the first starts at a barrel end");
            Assert.That(streaks[1].FromBarrel, Is.LessThan(AtTheBarrel), "the second starts at a barrel end");
            Assert.That(Hand(streaks[1]), Is.Not.EqualTo(Hand(streaks[0])), "the second shot comes from the other hand");
        }

        [UnityTest]
        public IEnumerator WithoutPistols_AShotStartsOverTheCharacter()
        {
            _player.RightMuzzle = null;
            _player.LeftMuzzle = null;
            _player.Teleport(NearA);
            yield return null;

            _player.Shoot(_a.transform.position);
            yield return Watching(() => _player.BasicShotsFired >= 1, 3f);
            _player.Stop();
            Assert.That(Of(ShotFxKind.Streak).Count, Is.EqualTo(1));
            Vector3 p = _player.Position;
            var expected = new Vector3(p.x, PlayerCharacter.FallbackMuzzleHeight, p.z);
            Assert.That(Vector3.Distance(Of(ShotFxKind.Streak)[0].Origin, expected), Is.LessThan(1e-3f), "the streak");
            Assert.That(Vector3.Distance(Of(ShotFxKind.Flash)[0].Origin, expected), Is.LessThan(1e-3f), "the flash");
        }

        [UnityTest]
        public IEnumerator TheDamage_LandsInTheFrameTheEffectsAppear()
        {
            _player.Teleport(NearA);
            yield return null;

            _player.Shoot(_a.transform.position);
            int frameOfTheHit = -1;
            yield return Watching(() =>
            {
                if (frameOfTheHit < 0 && _a.HitsTaken >= 1) frameOfTheHit = Time.frameCount;
                return _player.BasicShotsFired >= 1;
            }, 3f);
            _player.Stop();
            Assert.That(_seen.Count, Is.GreaterThan(0));
            Assert.That(_seen.All(s => s.Frame == frameOfTheHit), Is.True, "the target is hit in the frame the effects appear");
            Assert.That(Lost(_a), Is.EqualTo(Placeholders.BasicShotDamage), "with all its damage");

            // The same for a skill.
            int hitsBefore = _a.HitsTaken;
            _seen.Clear();
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            int frameOfTheSkillHit = -1;
            yield return Watching(() =>
            {
                if (frameOfTheSkillHit < 0 && _a.HitsTaken > hitsBefore) frameOfTheSkillHit = Time.frameCount;
                return _player.SkillShotsFired >= 1;
            }, 3f);
            Assert.That(Of(ShotFxKind.Streak).Count, Is.EqualTo(1));
            Assert.That(_seen.All(s => s.Frame == frameOfTheSkillHit), Is.True, "the skill's hit lands in the frame its effects appear");
        }

        // ---------------- Skills ----------------

        [UnityTest]
        public IEnumerator ANarrowLine_OneFlash_AndOneStreakOverTheSkillsRangeAlongTheAim()
        {
            AssumePistols();
            SkillDefinition skill = _player.Skills[Quick];
            Assume.That(skill.Shape, Is.EqualTo(SkillShape.Line));
            Assume.That(skill.HalfWidth * 2f, Is.LessThan(2f * PlayerCharacter.LineStreakSpacing), "a narrow line");
            _player.Teleport(NearA);
            yield return null;
            Vector3 aim = Flat(_a.transform.position - _player.Position).normalized;

            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Watching(() => _player.SkillShotsFired >= 1, 3f);
            Assert.That(Of(ShotFxKind.Flash).Count, Is.EqualTo(1), "one flash");
            Assert.That(Of(ShotFxKind.Streak).Count, Is.EqualTo(1), "one streak");
            Seen streak = Of(ShotFxKind.Streak)[0];
            Assert.That(streak.FromBarrel, Is.LessThan(AtTheBarrel), "it starts at a barrel end");
            Assert.That(Of(ShotFxKind.Flash)[0].FromBarrel, Is.LessThan(AtTheBarrel), "and so does the flash");
            Vector3 reach = Flat(streak.End - _player.Position);
            Assert.That(reach.magnitude, Is.EqualTo(skill.Range).Within(0.05f), "it ends the skill's range from the character");
            Assert.That(Vector3.Dot(reach.normalized, aim), Is.GreaterThan(0.9999f), "along the aim");
        }

        [UnityTest]
        public IEnumerator AWideLine_ShowsStreaksSideBySideAcrossItsWidth()
        {
            SkillDefinition skill = _player.Skills[Volley];
            Assume.That(skill.Shape, Is.EqualTo(SkillShape.Line));
            float width = skill.HalfWidth * 2f;
            Assume.That(width, Is.GreaterThanOrEqualTo(2f * PlayerCharacter.LineStreakSpacing), "a wide line");
            Vector3 aim = Flat(_a.transform.position - _player.Position).normalized;
            Vector3 across = new Vector3(aim.z, 0f, -aim.x);

            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Watching(() => _player.SkillShotsFired >= 1, 3f);
            int count = PlayerCharacter.StreaksAcross(width);
            Assert.That(count, Is.GreaterThan(1));
            List<Seen> streaks = Of(ShotFxKind.Streak);
            Assert.That(streaks.Count, Is.EqualTo(count), "one streak per " + PlayerCharacter.LineStreakSpacing + " m of width");

            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (Seen s in streaks)
            {
                Assert.That(Vector3.Dot(Flat(s.Direction).normalized, aim), Is.GreaterThan(0.995f), "each runs along the aim");
                Vector3 reach = Flat(s.End - _player.Position);
                Assert.That(Vector3.Dot(reach, aim), Is.EqualTo(skill.Range).Within(0.05f), "each to the end of the strip");
                float side = Vector3.Dot(reach, across);
                Assert.That(Mathf.Abs(side), Is.LessThanOrEqualTo(skill.HalfWidth), "inside the strip");
                min = Mathf.Min(min, side);
                max = Mathf.Max(max, side);
            }
            Assert.That(max - min, Is.EqualTo(width * (count - 1) / count).Within(0.02f), "spread across the width");
            Assert.That(Of(ShotFxKind.Flash).Count, Is.EqualTo(1), "one flash");
        }

        [UnityTest]
        public IEnumerator ScatterBlast_StreaksAcrossTheCone_AndAWedgeOverIt()
        {
            SkillDefinition skill = _player.Skills[Scatter];
            Assume.That(skill.Shape, Is.EqualTo(SkillShape.Cone));
            ShotLook look = LookOf(skill);
            Assume.That(look.Rays, Is.GreaterThanOrEqualTo(5), "its streaks go edge to edge");
            Assume.That(look.BlastSeconds, Is.GreaterThan(0f), "it has a blast");
            _player.Teleport(NearA);
            yield return null;
            Vector3 aim = Flat(_a.transform.position - _player.Position).normalized;

            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Watching(() => _player.SkillShotsFired >= 1, 3f);
            List<Seen> streaks = Of(ShotFxKind.Streak);
            Assert.That(streaks.Count, Is.EqualTo(look.Rays), "a streak per ray");

            var angles = new List<float>();
            foreach (Seen s in streaks)
            {
                Vector3 reach = Flat(s.End - _player.Position);
                Assert.That(reach.magnitude, Is.EqualTo(skill.Range).Within(0.05f), "each ends the skill's range away");
                angles.Add(Vector3.SignedAngle(aim, reach, Vector3.up));
            }
            angles.Sort();
            Assert.That(angles[0], Is.EqualTo(-skill.Angle * 0.5f).Within(0.5f), "the first along one edge");
            Assert.That(angles[angles.Count - 1], Is.EqualTo(skill.Angle * 0.5f).Within(0.5f), "the last along the other edge");

            List<Seen> wedges = Of(ShotFxKind.Wedge);
            Assert.That(wedges.Count, Is.EqualTo(1), "one wedge");
            Seen wedge = wedges[0], flash = Of(ShotFxKind.Flash)[0];
            Vector3 apex = Flat(wedge.Origin - _player.Position);
            Assert.That(wedge.Origin.y, Is.EqualTo(flash.Origin.y).Within(0.01f), "level with the gun that fired");
            Assert.That(Vector3.Dot(Flat(flash.Origin - wedge.Origin), aim), Is.EqualTo(0f).Within(0.01f), "as far ahead as its muzzle");
            Assert.That((apex - Vector3.Dot(apex, aim) * aim).magnitude, Is.LessThan(0.01f), "on the aim line through the character");
            Assert.That(Vector3.Dot(wedge.Direction, aim), Is.GreaterThan(0.9999f), "along the aim");
            Assert.That(wedge.Angle, Is.EqualTo(skill.Angle).Within(1e-3f), "as wide as the cone");
            Assert.That(Vector3.Dot(apex, aim) + wedge.Size, Is.EqualTo(skill.Range).Within(1e-3f), "and reaching as far as it");
        }

        [UnityTest]
        public IEnumerator FanFire_EveryTickFiresInsideTheCone_AndLeavesNothingBehind()
        {
            SkillDefinition skill = _player.Skills[Fan];
            Assume.That(skill.Input, Is.EqualTo(SkillInput.Holding));
            Assume.That(skill.Shape, Is.EqualTo(SkillShape.Cone));
            ShotLook look = LookOf(skill);
            _player.Teleport(NearA);
            yield return null;
            Vector3 aim = Flat(_a.transform.position - _player.Position).normalized;

            Assert.That(_player.PressSkill(Fan, _a.transform.position), Is.EqualTo(IntentResult.Started));
            float release = Time.time + skill.Startup + skill.TickInterval * 3.5f;
            yield return Watching(() => Time.time >= release, 5f);
            _player.ReleaseSkill(Fan);
            yield return Watching(() => _player.Action == ActionKind.None, 3f);

            int hits = _player.SkillShotsFired;
            Assert.That(hits, Is.GreaterThanOrEqualTo(4), "it fired several times");
            Assert.That(Of(ShotFxKind.Flash).Count, Is.EqualTo(hits), "a flash per tick");
            Assert.That(Of(ShotFxKind.Streak).Count, Is.EqualTo(hits * Mathf.Max(1, look.Rays)), "its streaks per tick");
            foreach (Seen s in Of(ShotFxKind.Streak))
            {
                Vector3 reach = Flat(s.End - _player.Position);
                Assert.That(reach.magnitude, Is.EqualTo(skill.Range).Within(0.05f), "each ends the skill's range away");
                Assert.That(Mathf.Abs(Vector3.SignedAngle(aim, reach, Vector3.up)), Is.LessThanOrEqualTo(skill.Angle * 0.5f + 0.01f), "inside the cone");
            }

            float due = _seen.Max(s => s.At + s.Seconds) + 0.05f;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.time < due && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<ShotFx>(), Is.Empty, "every effect has removed itself");
        }

        [UnityTest]
        public IEnumerator PowderKeg_NoFlashOrStreak_AnOutlineOfItsCircle_ABurst_AndASparkOnEveryTargetHit()
        {
            SkillDefinition skill = _player.Skills[Keg];
            Assume.That(skill.Shape, Is.EqualTo(SkillShape.Circle));
            ShotLook look = LookOf(skill);
            Assume.That(look.BlastSeconds, Is.GreaterThan(0f), "it has a blast");
            Damageable[] group = { Dummy("Dummy_D"), Dummy("Dummy_E"), Dummy("Dummy_F") };
            Vector3 centre = group.Aggregate(Vector3.zero, (sum, d) => sum + d.transform.position) / group.Length;
            centre.y = 0f;
            _player.Teleport(new Vector3(centre.x, 0f, centre.z - skill.Range * 0.7f));
            yield return null;
            foreach (Damageable d in group)
                Assume.That(FlatDistance(d.transform.position, centre), Is.LessThan(skill.Radius), "the group fits in the circle");

            Assert.That(_player.PressSkill(Keg, centre), Is.EqualTo(IntentResult.Started));
            yield return Watching(() => _player.SkillShotsFired >= 1, 3f);
            Assert.That(Of(ShotFxKind.Flash), Is.Empty, "no muzzle flash");
            List<Seen> outlines = Of(ShotFxKind.Outline);
            Assert.That(outlines.Count, Is.EqualTo(1), "one outline");
            Assert.That(FlatDistance(outlines[0].Origin, centre), Is.LessThan(0.01f), "around the circle");
            Assert.That(outlines[0].Size, Is.EqualTo(skill.Radius).Within(1e-3f), "exactly at its edge");
            Assert.That(Of(ShotFxKind.Spark).Any(s => FlatDistance(s.Origin, centre) < 0.01f && s.Size >= skill.Radius), Is.True,
                        "a burst over the middle, at least as big as the circle's radius");
            Assert.That(Of(ShotFxKind.Streak), Is.Empty, "no streak");
            List<Seen> rings = Of(ShotFxKind.Ring);
            Assert.That(rings.Count, Is.EqualTo(1), "one ring");
            Assert.That(FlatDistance(rings[0].Origin, centre), Is.LessThan(0.01f), "on the circle");
            Assert.That(rings[0].Size, Is.EqualTo(skill.Radius).Within(1e-3f), "out to its edge");
            foreach (Damageable d in group)
                Assert.That(Of(ShotFxKind.Spark).Any(s => FlatDistance(s.Origin, d.transform.position) <= d.Radius + 0.01f), Is.True,
                            "a spark on " + d.name);
        }

        [UnityTest]
        public IEnumerator EachHitOfABurst_HasItsOwnStreak_FromTheHandsInTurn()
        {
            AssumePistols();
            SkillDefinition skill = _player.Skills[Triple];
            Assume.That(skill.HitCount, Is.EqualTo(3));
            Assume.That(skill.HalfWidth * 2f, Is.LessThan(2f * PlayerCharacter.LineStreakSpacing), "a narrow line");
            _player.Teleport(NearA);
            yield return null;

            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Watching(() => _player.Action == ActionKind.None, 3f);
            Assert.That(_player.SkillShotsFired, Is.EqualTo(3));
            List<Seen> streaks = Of(ShotFxKind.Streak);
            Assert.That(streaks.Count, Is.EqualTo(3), "a streak for each hit");
            Assert.That(Of(ShotFxKind.Spark).Count, Is.EqualTo(3), "a spark for each hit on A");
            Assert.That(streaks[1].Frame, Is.GreaterThan(streaks[0].Frame), "one after another");
            foreach (Seen s in streaks) Assert.That(s.FromBarrel, Is.LessThan(AtTheBarrel), "each starts at a barrel end");
            Assert.That(Hand(streaks[1]), Is.Not.EqualTo(Hand(streaks[0])), "the second from the other hand");
            Assert.That(Hand(streaks[2]), Is.EqualTo(Hand(streaks[0])), "and the third from the first hand again");
        }

        // ---------------- Helpers ----------------

        // ---------------- Recoil ----------------

        struct Pose1
        {
            public int Frame;
            public float At, RightUp, LeftUp, RightDot, LeftDot;
        }

        [UnityTest]
        public IEnumerator Recoil_KicksOnlyThePistolThatFired_AndEachIsBackOnTheTargetBeforeItsNextShot()
        {
            AssumePistols();
            ShotFxSettings looks = _player.ShotLooks;
            Assume.That(looks != null && looks.Basic.Recoil > 0f, Is.True, "the basic shot kicks");
            float kick = looks.RecoilKickSeconds, settle = looks.RecoilSettleSeconds;
            _player.Teleport(NearA);
            yield return null;
            Vector3 toTarget = Flat(_a.transform.position - _player.Position).normalized;

            var poses = new List<Pose1>();
            float strength = 0f;
            int clicks = 0;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                // Click to shoot: a click whenever none is waiting, so the shots come once a cycle. Five clicks: the
                // fifth keeps her aiming through the kick of the fourth, as a player clicking on would.
                if (clicks < 5 && !_player.HasPendingShot) { _player.Shoot(_a.transform.position); clicks++; }
                Note();
                poses.Add(new Pose1
                {
                    Frame = Time.frameCount, At = Time.time,
                    RightUp = Up(_view.RightWeapon), LeftUp = Up(_view.LeftWeapon),
                    RightDot = Vector3.Dot(_view.RightWeapon.right, toTarget), LeftDot = Vector3.Dot(_view.LeftWeapon.right, toTarget),
                });
                if (_player.BasicShotsFired >= 1 && strength == 0f) strength = _player.KickStrength;
                List<Seen> sofar = Of(ShotFxKind.Flash);
                if (sofar.Count >= 4 && Time.time > sofar[3].At + kick + settle + 0.05f) break;
                yield return null;
            }
            _player.Stop();

            List<Seen> flashes = Of(ShotFxKind.Flash);
            Assert.That(flashes.Count, Is.GreaterThanOrEqualTo(4), "a burst of four");
            float peak = _view.PeakKickAngle(strength);
            Assert.That(peak, Is.GreaterThan(3f), "a kick big enough to see");
            for (int i = 0; i < 4; i++)
            {
                Seen f = flashes[i];
                string shot = "shot " + (i + 1) + " (" + Hand(f) + "): ";
                Assert.That(f.FromBarrel, Is.LessThan(AtTheBarrel), shot + "the flash starts at the barrel end; the kick did not move it");
                if (i > 0) Assert.That(f.NearestWeapon, Is.Not.SameAs(flashes[i - 1].NearestWeapon), shot + "the hands take turns");
                bool right = f.NearestWeapon == _view.RightWeapon;
                int at = poses.FindIndex(p => p.Frame == f.Frame);
                Assert.That(at, Is.GreaterThanOrEqualTo(0));
                // The pose read in the shot's frame is the one from before the shot.
                Assert.That(Dot(poses[at], right), Is.GreaterThan(0.97f), shot + "that pistol was back on the target when it fired");
                float baseUp = UpOf(poses[at], right), maxUp = float.NegativeInfinity;
                for (int k = at + 1; k < poses.Count && poses[k].At <= f.At + kick + settle; k++)
                {
                    if (poses[k].At <= f.At + kick + 0.05f) maxUp = Mathf.Max(maxUp, UpOf(poses[k], right) - baseUp);
                    Assert.That(Dot(poses[k], !right), Is.GreaterThan(0.97f), shot + "the other pistol stays on the target");
                    Assert.That(Mathf.Abs(UpOf(poses[k], !right) - UpOf(poses[at], !right)), Is.LessThan(2f), shot + "the other pistol's muzzle does not flip up");
                }
                Assert.That(maxUp, Is.InRange(peak * 0.7f, peak * 1.1f), shot + "the muzzle flips up by the kick's angle");
                int after = poses.FindIndex(p => p.At > f.At + kick + settle + 0.02f);
                if (after >= 0 && (i + 1 >= flashes.Count || poses[after].Frame <= flashes[i + 1].Frame + 1))
                {
                    Assert.That(Mathf.Abs(UpOf(poses[after], right) - baseUp), Is.LessThan(2f), shot + "settled back after the kick");
                    Assert.That(Dot(poses[after], right), Is.GreaterThan(0.97f), shot + "on the target again");
                }
            }
        }

        static float Up(Transform weapon) => Mathf.Asin(Mathf.Clamp(weapon.right.y, -1f, 1f)) * Mathf.Rad2Deg;
        static float UpOf(Pose1 p, bool right) => right ? p.RightUp : p.LeftUp;
        static float Dot(Pose1 p, bool right) => right ? p.RightDot : p.LeftDot;

        void AssumePistols()
        {
            Assume.That(_view != null && _view.RightWeapon != null && _view.LeftWeapon != null, Is.True,
                        "this figure holds a pistol in each hand");
        }

        ShotLook LookOf(SkillDefinition skill) =>
            (_player.ShotLooks != null ? _player.ShotLooks.For(skill.Name) : null) ?? ShotFxDefaults.For(skill.Name);

        List<Seen> Of(ShotFxKind kind) => _seen.Where(s => s.Kind == kind).ToList();

        static Damageable Dummy(string name) => GameObject.Find(name).GetComponent<Damageable>();

        /// <summary>Waits until the condition holds, writing down every new effect in the frame it appears.</summary>
        IEnumerator Watching(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                Note();
                if (condition() || Time.realtimeSinceStartup >= deadline) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// Writes down the effects that are new since the last look. The test resumes after every Update, so the
        /// pistols are posed as they were when the shot went off.
        /// </summary>
        void Note()
        {
            foreach (ShotFx fx in Object.FindObjectsByType<ShotFx>())
            {
                if (!_known.Add(fx)) continue;
                var s = new Seen
                {
                    Fx = fx, Kind = fx.Kind, Origin = fx.Origin, End = fx.End, Direction = fx.Direction, Size = fx.Size,
                    Angle = fx.Angle, Seconds = fx.Seconds, At = Time.time, Frame = Time.frameCount,
                    FromBarrel = float.PositiveInfinity,
                };
                if (_view != null)
                {
                    foreach (Transform weapon in new[] { _view.RightWeapon, _view.LeftWeapon })
                    {
                        if (weapon == null) continue;
                        float d = FromBarrelEnd(weapon, fx.Origin);
                        if (d >= s.FromBarrel) continue;
                        s.FromBarrel = d;
                        s.NearestWeapon = weapon;
                    }
                }
                _seen.Add(s);
            }
        }

        /// <summary>
        /// Metres from a point to the front end of a pistol: the face of the pistol's box (its mesh bounds, in
        /// the weapon's own axes) that lies farthest along the barrel, the weapon's +X. The mesh cannot be read
        /// vertex by vertex in Play mode, so the box stands in for the shape: a point on that face is at the
        /// barrel end, within the pistol's outline seen from the front.
        /// </summary>
        static float FromBarrelEnd(Transform weapon, Vector3 point)
        {
            var box = new Bounds();
            bool any = false;
            foreach (MeshFilter filter in weapon.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Bounds b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                    Vector3 local = weapon.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) box = new Bounds(local, Vector3.zero);
                    else box.Encapsulate(local);
                    any = true;
                }
            }
            Assert.That(any, Is.True, weapon.name + " has a mesh");
            Vector3 p = weapon.InverseTransformPoint(point);
            var onFace = new Vector3(box.max.x, Mathf.Clamp(p.y, box.min.y, box.max.y), Mathf.Clamp(p.z, box.min.z, box.max.z));
            return Vector3.Distance(point, weapon.TransformPoint(onFace));
        }

        static string Hand(Seen s) => s.NearestWeapon != null ? s.NearestWeapon.name : "no pistol";
        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
