using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Work order 149 (Kristian, 2026-11-27: "Egentlig vil jeg ha unike elites, som har et eget sett med utfordringer slik som i hades";
    /// <see cref="EliteValues.Unique"/>): the Grave Ram in floor 1's elite room (knights-apse, entered from the south, so the room's
    /// depth runs up his screen), fought on a stand-in for her: she stands where the test puts her, or steps or walks as the answer the
    /// research names, a quarter second after the tell begins. Each attack hits her standing and misses the answer; a Ram baited into
    /// each pier, a wall and a low prop crashes and dazes, its strip drawn where it ran; it fights alone; nothing before 2 s nor off his
    /// screen; the hurt guard; phase 2; Stopping Shot; the reward as today; the bar; the recorder; nothing allocated by its frames; the
    /// switch off is today's elite. The camera's picture is his 16:9. Nobody has played it: these are numbers.
    /// </summary>
    public class GraveRamPlayTests
    {
        const float React = 0.25f; // s, the stand-in's reaction (R7 5.2: a 1.5 m step takes 0.25 s; the order's sidestepping bot)
        const ulong From = 0x149A0UL;

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        LevelValues _values;
        bool _uniqueBefore;
        string _folder;
        PlaySessionRecorder _recorder;
        static ulong s_seed;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _uniqueBefore = EliteValues.Unique;
            EliteValues.Unique = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _levels = GeneratedLevels.Ensure();
            _values = new LevelValues();
            _levels.Values = _values;
            _levels.RunValues.SkillTiers = false;
            if (Camera.main != null) Camera.main.aspect = 16f / 9f; // his screen
            if (s_seed == 0UL) s_seed = GraveRamTestRoom.ApseSeed(_values, From);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Time.captureDeltaTime = 0f;
            EliteValues.Unique = _uniqueBefore;
            EliteHurtGuard.Clear();
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_health != null) _health.Untouchable = false;
            if (_player != null) _player.Steer(Vector3.zero);
            if (Camera.main != null) Camera.main.ResetAspect();
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        IEnumerator Room(Action<GraveRamTestRoom> got, bool wake = true) =>
            GraveRamTestRoom.Build(_levels, _practiceEnemy, _player, s_seed, got, wake);

        /// <summary>Her and the Ram at two places of the room, whatever the Ram was doing dropped.</summary>
        IEnumerator Place(GraveRamTestRoom r, Vector3 her, Vector3 ram)
        {
            _player.Teleport(her);
            yield return r.Put(ram);
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        Vector3 Floor(GraveRamTestRoom r, double d, double s)
        {
            Assert.That(GraveRamTestRoom.OnFloor(r.World(d, s), 0.7f, out Vector3 at), Is.True, "walkable floor at " + d + ", " + s);
            return at;
        }

        /// <summary>Runs until the Ram begins a wind-up of <paramref name="attack"/> (her held where she is), at most <paramref name="most"/> s; false if it did not.</summary>
        IEnumerator UntilWindup(GraveRamTestRoom r, string attack, Vector3 her, float most, Action<bool> got)
        {
            bool began = false;
            yield return GraveRamTestRoom.Run(most, () => r.Ram.State == EnemyState.Windup && r.Ram.Attack != null && r.Ram.Attack.Name == attack,
                                              () => _player.Teleport(her));
            began = r.Ram.State == EnemyState.Windup && r.Ram.Attack != null && r.Ram.Attack.Name == attack;
            got(began);
        }

        // ---------------- It fights alone; the switch off ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheEliteRoom_HoldsTheRamAlone_TheLayoutTheSame_AndTheSwitchOff_IsTodaysElite()
        {
            LevelLayout on = LevelGenerator.Generate(s_seed, 1, _values);
            GraveRamTestRoom r = null;
            yield return Room(x => r = x, wake: false);
            Assert.That(r.Level.EliteRoomEnemies.Count, Is.EqualTo(1), "nothing beside it");
            Assert.That(r.Level.EliteRoomEnemies[0], Is.SameAs(r.Ram));
            Assert.That(r.R.EnemyGroups.Count(g => !g.Elite), Is.GreaterThan(0), "the layout still has the room's others: they are not made");
            Assert.That(r.Ram.IsElite, Is.True);
            Assert.That(r.Ram.Body.MaxHealth, Is.EqualTo(10000), "its own health");
            Assert.That(r.Ram.DamageScale, Is.EqualTo(1f), "its damage final: not floor 1's 0.6");
            Assert.That(r.Ram.TakesTurns, Is.False);
            Assert.That(r.Ram.Fight, Is.SameAs(r.Fight), "its room's fight is its");
            Assert.That(Object.FindObjectsByType<EnemyGraveRam>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(r.Ram.transform.lossyScale.y, Is.EqualTo(1.6f).Within(1e-4f), "the brute's model 1.6 times");
            Assert.That(r.Ram.Look, Is.Not.Null, "wearing the brute's model");
            Assert.That(r.Ram.Look.Spec.Name, Is.EqualTo(EnemyModelSpec.Brute.Name));
            Assert.That(r.Ram.ModelHeight, Is.EqualTo(1.8f * 1.6f).Within(0.01f));
            Color tint = r.Ram.BodyColor;
            Assert.That(tint.r, Is.EqualTo(0.86f).Within(0.01f), "ash white");
            Assert.That(r.Ram.Body.Radius, Is.EqualTo(0.8f));
            int otherRooms = r.Level.Enemies.Count(e => e != r.Ram);

            Assert.That(LevelMap.Draw(r.Level.Layout), Is.EqualTo(LevelMap.Draw(on)), "the generator's output is the same with the switch on");
            // The switch off: today's elite room, field for field: its elite a gold elite brute of floor 1's numbers, the others there.
            EliteValues.Unique = false;
            _practiceEnemy.enabled = true;
            GeneratedLevel off = _levels.Build(s_seed, 1);
            _practiceEnemy.enabled = false;
            yield return null;
            Assert.That(LevelMap.Draw(off.Layout), Is.EqualTo(LevelMap.Draw(on)), "the generator's output is the same either way");
            Assert.That(Object.FindObjectsByType<EnemyGraveRam>(FindObjectsSortMode.None).Length, Is.EqualTo(0), "no Ram");
            Enemy elite = off.Elite;
            Assert.That(elite, Is.InstanceOf<EnemyBrute>(), "today's elite brute");
            Assert.That(elite.IsElite, Is.True);
            Assert.That(elite.Body.MaxHealth, Is.EqualTo(_values.EliteHealthOf(EnemyKind.Brute)));
            Assert.That(elite.DamageScale, Is.EqualTo(_values.EliteDamageScale * _values.DamageScaleOf(1)).Within(1e-5f));
            Assert.That(elite.BodyColor, Is.EqualTo(EnemyFactory.EliteTint));
            int made = off.EliteRoomEnemies.Count;
            int planned = off.Layout.EliteRoom.EnemyPlaces.Sum(p => p.Length);
            Assert.That(made, Is.EqualTo(planned), "every one the plan put there");
            Assert.That(off.Enemies.Count, Is.EqualTo(otherRooms + planned), "the other rooms the same");
            foreach (GeneratedLevel.PlacedGroup g in off.Groups)
                foreach (Enemy e in g.Members)
                {
                    Assert.That(e.Body.MaxHealth, Is.EqualTo(g.Group.Elite ? _values.EliteHealthOf(g.Group.Kind) : _values.HealthOf(g.Group.Kind, g.Group.Boss)), e.name);
                    Assert.That(e.DamageScale, Is.EqualTo((g.Group.Elite ? _values.EliteDamageScale : 1f) * _values.DamageScaleOf(1)).Within(1e-5f), e.name);
                }
        }

        // ---------------- The Ram ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheRam_HitsHerStandingStill_ForItsFinalEighteen_ItsStripBlue_ItsBodyLeaning()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            _health.Untouchable = false;
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Ram, her, 6f, b => began = b);
            Assert.That(began, Is.True, "a Ram at her 8 m off in the open");
            Assert.That(r.Ram.IsTelegraphing && r.Ram.TelegraphIsCounterable, Is.True, "its strip on the floor, blue");
            Assert.That(r.Ram.Telegraph.positionCount, Is.EqualTo(4), "a strip's four corners");
            yield return GraveRamTestRoom.Run(0.7f, null, () => _player.Teleport(her));
            Assert.That(r.Ram.Lowered, Is.GreaterThan(0.9f), "leaning by the middle of its tell");
            int before = _health.Current;
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.LastRamEnd != RamEnd.None, () => _player.Teleport(her));
            Assert.That(r.Ram.LastRamEnd, Is.EqualTo(RamEnd.MetHer));
            Assert.That(before - _health.Current, Is.EqualTo(18), "18: its own, not floor 1's scale");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheRam_SteppedAsideAQuarterSecondIn_PassesHer_RunsOnToTheWall_AndCrashes()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            _health.Untouchable = false;
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Ram, her, 6f, b => began = b);
            Assume.That(began, Is.True);
            Assert.That(r.Ram.RamCrashes, Is.True, "its strip ends in stone beyond her (the west wall, or a prop before it): a crash if it misses her");
            Assert.That(r.Ram.RamStopper, Is.Not.EqualTo(LancerStopper.None));
            yield return GraveRamTestRoom.Run(React, null);
            // The answer: a step of 2.5 m out of the strip, walked (a quarter second after the tell began).
            Vector3 aside = Floor(r, 3.5, -4);
            yield return Walk(aside, 1f);
            int before = _health.Current;
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.LastRamEnd != RamEnd.None);
            Assert.That(r.Ram.LastRamEnd, Is.EqualTo(RamEnd.Crashed), "it ran on into the stone");
            Assert.That(_health.Current, Is.EqualTo(before), "missed her");
            Assert.That(r.Ram.IsDazed && r.Ram.CrashDazed, Is.True, "dazed");
        }

        /// <summary>She walks toward a point at her run speed (her move intent), for at most <paramref name="most"/> s.</summary>
        IEnumerator Walk(Vector3 to, float most)
        {
            yield return GraveRamTestRoom.Run(most, () => FlatDistance(_player.Position, to) < 0.3f, () =>
            {
                Vector3 d = GraveRamTestRoom.Flat(to - _player.Position);
                _player.Steer(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.zero);
            });
            _player.Steer(Vector3.zero);
        }

        static IEnumerable<string> BaitCases() => new[] { "left pier", "right pier", "wall", "low prop" };

        /// <summary>
        /// A Ram baited into stone: she stands with it behind her on the Ram's line, steps out a quarter second into the tell; it runs on
        /// and its body meets the stone: a crash, dazed 2.5 s, taking 1.5 times; its strip ended where it stopped, its body's front at the
        /// stone. The piers from their inner sides (it charges across the room), the west wall, a low prop of the room.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ARam_BaitedIntoStone_Crashes_AndIsDazedTwoAndAHalfSeconds_TakingOneAndAHalfTimes([ValueSource(nameof(BaitCases))] string into)
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            // A place for her before the stone, the Ram across his screen from her, found by its own sweep: the piers (4 m square, 8 m to
            // each side of the room's middle, 8 to 12 m in), the outer wall, a low prop of the room.
            LancerStopper wanted = into == "low prop" ? LancerStopper.LowProp : LancerStopper.Wall;
            Func<float, bool, LancerStopper, Vector3, Vector3, bool> want = (run, crash, stopper, stop, h) =>
                crash && stopper == wanted
                && (into == "left pier" ? r.Near(stop, 10, -8, 3.6f) : into == "right pier" ? r.Near(stop, 10, 8, 3.6f)
                    : into == "wall" ? !r.Near(stop, 10, -8, 4.5f) && !r.Near(stop, 10, 8, 4.5f) : true);
            Assume.That(r.FindRam(want, out Vector3 her, out Vector3 ram, out Vector3 step), Is.True, "a place to bait it into the " + into);
            yield return Place(r, her, ram);
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Ram, her, 8f, b => began = b);
            Assert.That(began, Is.True, "a Ram at her");
            Assert.That(r.Ram.RamCrashes, Is.True, "its line ends in stone behind her");
            Assert.That(r.Ram.RamStopper, Is.EqualTo(wanted));
            float strip = r.Ram.StripLength, run = r.Ram.RamRun;
            Vector3 from = GraveRamTestRoom.Flat(r.Ram.transform.position);
            yield return GraveRamTestRoom.Run(React, null);
            yield return Walk(step, 1f);
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.LastRamEnd != RamEnd.None);
            Assert.That(r.Ram.LastRamEnd, Is.EqualTo(RamEnd.Crashed), into);
            Assert.That(r.Ram.LastStopper, Is.EqualTo(wanted));
            float ran = FlatDistance(r.Ram.transform.position, from);
            Assert.That(ran, Is.EqualTo(run).Within(0.15f), "it ran its strip: the drawn edge is where it stopped");
            Assert.That(strip - run, Is.EqualTo(r.Ram.Tuning.BodyRadius).Within(1e-4f), "the strip drawn to its body's front");
            // Its body's front at the stone: a ball of its radius a little further on meets it.
            Vector3 dir = r.Ram.RunDirection;
            Vector3 low = r.Ram.transform.position + Vector3.up * (r.Ram.Tuning.ProbeLift + r.Ram.Tuning.BodyRadius);
            Assert.That(Physics.SphereCast(low - dir * 0.2f, r.Ram.Tuning.BodyRadius, dir, out RaycastHit _, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                        Is.True, "its body meets the stone where it stopped");
            Assert.That(r.Ram.CrashDazed, Is.True, "dazed");
            float dazedAt = Time.time;
            int hit = r.Ram.Body.TakeHit(100);
            Assert.That(hit, Is.EqualTo(150), "it takes 1.5 times while dazed");
            yield return GraveRamTestRoom.Run(4f, () => !r.Ram.IsDazed);
            Assert.That(Time.time - dazedAt, Is.EqualTo(r.Ram.Tuning.CrashDaze).Within(0.1f), "for 2.5 s");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ARam_IsNeverBegun_WhoseLineEndsInStoneShortOfHer_ItWalksRoundInstead()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            // A place of hers and of its where its thin line to her is clear past a pier's corner, but its body's sweep meets the corner short of her.
            Assume.That(CornerCase(r, out Vector3 her, out Vector3 ram), Is.True, "a pier's corner between them for its body, not for its sight");
            yield return Place(r, her, ram);
            int crashes = r.Ram.Crashes;
            yield return GraveRamTestRoom.Run(10f, () => r.Reached > 0, () => _player.Teleport(her));
            Assert.That(r.Ram.Crashes - crashes, Is.EqualTo(0), "no crash on the corner while she stands still: " + r.At(her) + " from " + r.At(ram));
            Assert.That(r.Reached, Is.GreaterThan(0), "and it reached her within 10 s");
        }

        /// <summary>Over the room's 2 m grid: her place and its, 6 to 18 m apart, its sight clear, its Ram's line ending in a pier short of her.</summary>
        static bool CornerCase(GraveRamTestRoom r, out Vector3 her, out Vector3 ram)
        {
            her = ram = Vector3.zero;
            var cells = new List<Vector3>();
            double left = r.Space.Left;
            for (int row = 0; row < r.Space.Rows; row++)
                for (int col = 0; col < r.Space.Cols; col++)
                    if (r.Space.IsWalkable(row, col) && GraveRamTestRoom.OnFloor(r.World((row + 0.5) * RoomUseValues.CellMetres, left + (col + 0.5) * RoomUseValues.CellMetres), 0.8f, out Vector3 p))
                        cells.Add(p);
            foreach (Vector3 h in cells)
                foreach (Vector3 m in cells)
                {
                    Vector3 d = GraveRamTestRoom.Flat(h - m);
                    float length = d.magnitude;
                    if (length < 6.5f || length > 16f) continue;
                    Vector3 dir = d / length;
                    if (Cover.ClearDistance(GraveRamTestRoom.Flat(m), dir, length, r.Ram.Tuning.SightHeight) < length - 1e-3f) continue;
                    if (NavMesh.Raycast(m, h, out NavMeshHit _, NavMesh.AllAreas)) continue;
                    float run = r.Ram.RunFrom(m, dir, r.Ram.Tuning.RamAttack.Length, out bool crash, out _, out LancerStopper stopper);
                    if (!crash || stopper != LancerStopper.Wall) continue;
                    if (GraveRamRules.RamReachesHer(run, r.Ram.Tuning.BodyRadius, length, Placeholders.PlayerRadius)) continue;
                    her = h;
                    ram = m;
                    return true;
                }
            return false;
        }

        // ---------------- The Stomp ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheStomp_HitsHerAgainstIt_ForFourteen_AndWalkingOutAQuarterSecondIn_Misses()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            _health.Untouchable = false;
            float touching = r.Ram.Tuning.BodyRadius + Placeholders.PlayerRadius + 0.05f;
            Vector3 ram = Floor(r, 6, 0), her = Floor(r, 6, -touching);
            yield return Place(r, her, ram);
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Stomp, her, 6f, b => began = b);
            Assert.That(began, Is.True, "the Stomp, she against it");
            Assert.That(r.Ram.IsTelegraphing && !r.Ram.TelegraphIsCounterable, Is.True, "its red circle");
            int before = _health.Current;
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.State == EnemyState.Recover, () => _player.Teleport(her));
            Assert.That(before - _health.Current, Is.EqualTo(14), "standing: 14");

            // Again, she walking straight out a quarter second after its tell began, from against its body (the worst place).
            EliteHurtGuard.Clear();
            yield return GraveRamTestRoom.Run(4f, () => r.Ram.State == EnemyState.Chase);
            yield return GraveRamTestRoom.Run(1.2f, null, () => _player.Teleport(her)); // its gap
            r.Ram.Tactics.Began(r.Ram.Tuning.RamAttack, Time.time); // the last was a Ram: the Stomp may come again
            yield return UntilWindup(r, GraveRamValues.Stomp, her, 6f, b => began = b);
            Assume.That(began, Is.True);
            yield return GraveRamTestRoom.Run(React, null);
            before = _health.Current;
            Vector3 outOf = GraveRamTestRoom.Flat(her - ram).normalized;
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.State == EnemyState.Recover, () => _player.Steer(outOf));
            _player.Steer(Vector3.zero);
            Assert.That(_health.Current, Is.EqualTo(before), "walked out in time");
        }

        // ---------------- The Grave Stones ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheStones_HitHerStandingBehindAPier_TheHurtGuardSparingTheSecond_AndKeepingMoving_MissesThemAll()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            _health.Untouchable = false;
            Vector3 her = Floor(r, 10, 12.8), ram = Floor(r, 10, 1.5);
            yield return r.HoldPastTheStart(ram, her);
            Assert.That(r.Ram.LineClear(), Is.False, "the right pier between them");
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Stones, her, 6f, b => began = b);
            Assert.That(began, Is.True, "the Grave Stones at her behind the pier");
            int before = _health.Current;
            yield return GraveRamTestRoom.Run(3f, () => !r.Ram.Stones.Any(s => s.Pending) && r.Ram.State != EnemyState.Windup, () => _player.Teleport(her));
            Assert.That(r.Ram.Stones.Select(s => GraveRamTestRoom.Flat(s.Center)).All(c => FlatDistance(c, her) < 0.6f), Is.True, "each where she stood");
            Assert.That(before - _health.Current, Is.EqualTo(16), "standing: the first and the third (8 each); the hurt guard spared the second");
            Assert.That(r.Ram.Guarded, Is.EqualTo(1));

            EliteHurtGuard.Clear();
            yield return GraveRamTestRoom.Run(4.5f, null, () => _player.Teleport(her)); // it walks round meanwhile
            yield return r.Put(ram); // back where it threw from, its throw's rest nearly over
            yield return GraveRamTestRoom.Run(4f, () => r.Ram.State == EnemyState.Windup && r.Ram.Attack != null && r.Ram.Attack.Name == GraveRamValues.Stones, () => _player.Teleport(her));
            Assume.That(r.Ram.Attack != null && r.Ram.Attack.Name == GraveRamValues.Stones, Is.True, "a second throw");
            yield return GraveRamTestRoom.Run(React, null);
            before = _health.Current;
            Vector3 south = GraveRamTestRoom.Flat(r.World(0, 12.8) - r.World(10, 12.8)).normalized;
            yield return GraveRamTestRoom.Run(2.5f, () => !r.Ram.Stones.Any(s => s.Pending), () => _player.Steer(south));
            _player.Steer(Vector3.zero);
            Assert.That(_health.Current, Is.EqualTo(before), "she kept moving: none landed");
        }

        // ---------------- His screen and the start ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator NothingBeforeTwoSecondsAfterTheWake_NorOffHisScreen_ItWalksIntoViewFirst()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x, wake: false);
            Vector3 her = Floor(r, 4, 0);
            float firstAt = -1f;
            bool offWhenBegun = false;
            r.Ram.Reported += e =>
            {
                if (e.Kind != EnemyReportKind.AttackStarted || firstAt >= 0f) return;
                firstAt = Time.time;
                offWhenBegun = !r.Ram.BodyOnScreen();
            };
            Assert.That(r.Ram.BodyOnScreen(), Is.False, "at the apse mouth, 20 m in, it is off his screen from the door");
            yield return r.Wake();
            float woke = r.Ram.WokeAt;
            yield return GraveRamTestRoom.Run(12f, () => firstAt >= 0f, () => _player.Teleport(her));
            Assert.That(firstAt, Is.GreaterThanOrEqualTo(0f), "it attacked");
            Assert.That(firstAt - woke, Is.GreaterThanOrEqualTo(2f - 1e-3f), "not before 2.0 s after the wake");
            Assert.That(offWhenBegun, Is.False, "its whole body on his screen as it began");
        }

        // ---------------- The hurt guard ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator TheHurtGuard_SparesHerForPointEightSecondsAfterItsHit_AndHerBodyFlickers()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            _health.Untouchable = false;
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.MetHer > 0, () => _player.Teleport(her));
            Assume.That(r.Ram.MetHer, Is.EqualTo(1));
            Assert.That(EliteHurtGuard.Holds(_health), Is.True, "the guard on");
            Assert.That(_health.TakeHit(10), Is.EqualTo(0), "a hit at once does nothing");
            var flicker = _player.GetComponent<EliteHurtGuard>();
            Assert.That(flicker, Is.Not.Null);
            bool hidden = false, shown = false;
            yield return GraveRamTestRoom.Run(0.7f, null, () => { hidden |= flicker.Hidden; shown |= !flicker.Hidden; });
            Assert.That(hidden && shown, Is.True, "her body flickers");
            Assert.That(_health.TakeHit(10), Is.EqualTo(0), "0.7 s on, still guarded");
            yield return GraveRamTestRoom.Run(0.15f, null);
            Assert.That(EliteHurtGuard.Holds(_health), Is.False);
            Assert.That(_health.TakeHit(10), Is.EqualTo(10), "past 0.8 s a hit lands");
            Assert.That(flicker.Hidden, Is.False, "and she is shown");
        }

        // ---------------- Phase 2 ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator AtHalfItsHealth_ItRoarsOnePointFiveSeconds_ThenItsRamComesTwice_TheSecondAtWhereSheStandsThen()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            // A line with no stone on it, she 6.5 to 8 m along it and its end 6.5 to 10.5 m past her (on his screen): she steps out of the first Ram, it misses and runs on past her,
            // and its second comes at where she stands then.
            Assume.That(r.FindRam((run, crash, stopper, stop, h) => !crash && Vector3.Distance(stop, h) >= 6.5f && Vector3.Distance(stop, h) <= 10.5f,
                                  out Vector3 her, out Vector3 ram, out Vector3 step, 6.5f, 10f, 3.5f),
                        Is.True, "a long clear line in the room");
            yield return Place(r, her, ram);
            r.Ram.Body.TakeHit(5100);
            yield return GraveRamTestRoom.Run(0.1f, null, () => _player.Teleport(her));
            Assert.That(r.Ram.PhaseTwo && r.Ram.IsRoaring, Is.True, "it roars");
            Assert.That(r.Ram.Stagger, Is.Not.Null);
            Assert.That(r.Ram.Body.Stagger, Is.Null, "its bar held aside: it cannot be staggered while it roars");
            Assert.That(r.Ram.Body.TakeHit(100), Is.EqualTo(100), "her damage counts while it roars (the boss's roar)");
            float roarAt = Time.time;
            yield return GraveRamTestRoom.Run(2f, () => !r.Ram.IsRoaring, () => _player.Teleport(her));
            Assert.That(Time.time - roarAt, Is.EqualTo(1.5f).Within(0.15f));
            var windups = new List<float>();
            r.Ram.Reported += e => { if (e.Kind == EnemyReportKind.AttackStarted && e.Attack == GraveRamValues.Ram) windups.Add(Time.time); };
            yield return GraveRamTestRoom.Run(6f, () => windups.Count >= 1, () => _player.Teleport(her));
            Assert.That(windups.Count, Is.EqualTo(1), "a Ram after the roar");
            Assert.That(r.Ram.Attack.Windup, Is.EqualTo(1.02f).Within(1e-4f), "its tell 0.85 times");
            yield return GraveRamTestRoom.Run(React, null);
            yield return Walk(step, 1f);
            yield return GraveRamTestRoom.Run(3f, () => windups.Count >= 2 || r.Ram.LastRamEnd == RamEnd.Crashed);
            Assert.That(r.Ram.LastRamEnd, Is.EqualTo(RamEnd.Missed), "the first missed, nothing in its way");
            Assert.That(windups.Count, Is.EqualTo(2), "a second Ram at once; refused: " + r.Ram.ChainRefused + "; she at " + r.At(_player.Position) + ", it at " + r.At(r.Ram.transform.position));
            Assert.That(r.Ram.AimPoint.x, Is.EqualTo(_player.Position.x).Within(0.3f), "aimed where she stands then");
            Assert.That(r.Ram.AimPoint.z, Is.EqualTo(_player.Position.z).Within(0.3f));
        }

        // ---------------- Stopping Shot ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator StoppingShotFromItsFront_DropsARam_AndDazesIt()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Ram, her, 6f, b => began = b);
            Assume.That(began, Is.True);
            Assert.That(r.Ram.TryCounter(her, 45f, 1.5f), Is.True, "from its front");
            yield return GraveRamTestRoom.Run(0.1f, null, () => _player.Teleport(her));
            Assert.That(r.Ram.IsDazed && !r.Ram.IsRamming, Is.True);
            Assert.That(r.Ram.Counters, Is.EqualTo(1));
        }

        // ---------------- The reward ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator ItsDeath_GivesTodaysEliteReward_AndHeal()
        {
            _levels.StartRun(0x149B1UL);
            yield return null;
            yield return null;
            var ram = _levels.Level.Elite as EnemyGraveRam;
            Assert.That(ram, Is.Not.Null, "a run's floor 1: the Ram");
            Assert.That(_levels.Level.EliteRoomEnemies.Count, Is.EqualTo(1));
            _health.Untouchable = false;
            _health.MaxHealth = 100;
            _health.RestoreFull();
            _health.TakeHit(50);
            int low = _health.Current;
            ram.Body.TakeHit(100000000);
            for (int i = 0; i < 600 && _levels.EliteDrop == null && _levels.Choice == GeneratedLevels.ChoiceKind.None; i++) yield return null;
            Assert.That(_levels.EliteDrop != null || _levels.Choice != GeneratedLevels.ChoiceKind.None, Is.True, "the elite's reward, as today");
            Assert.That(_health.Current - low, Is.EqualTo(Mathf.RoundToInt(_health.Max * _levels.RunValues.EliteRoomHeal)).Within(1), "the elite room's heal, 20 %");
        }

        // ---------------- The bar and the recorder ----------------

        [UnityTest, Timeout(240000)]
        public IEnumerator ItsBar_NamesIt_WhileItsFightIsOn_AndTheRecorderTellsTheFight()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GraveRamTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GraveRamTestRoom r = null;
            yield return Room(x => r = x, wake: false);
            var bar = r.Ram.GetComponent<GraveRamBar>();
            Assert.That(bar, Is.Not.Null);
            Assert.That(bar.Showing, Is.False, "asleep: no bar");
            yield return r.Wake();
            Assert.That(bar.Showing, Is.True, "its fight on: its bar");
            Assert.That(bar.Title, Is.EqualTo("The Grave Ram"));
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            yield return GraveRamTestRoom.Run(8f, () => r.Ram.MetHer > 0, () => _player.Teleport(her));
            r.Ram.Body.TakeHit(100000000);
            yield return GraveRamTestRoom.Run(0.2f, null);
            Assert.That(bar.Showing, Is.False, "dead: gone");
            SessionEvent line = _recorder.Events.LastOrDefault(e => e.Type == GraveRamEvents.Fight);
            Assert.That(line, Is.Not.Null, "its fight's line");
            Assert.That(line.Name, Is.EqualTo(GraveRamValues.Title));
            Assert.That(line.ExtraValue("ended"), Is.EqualTo("died"));
            Assert.That(line.ExtraValue("met"), Is.EqualTo("1"));
            Assert.That(line.ExtraValue("rams"), Is.Not.EqualTo("0"));
            string summary = SessionSummary.Format(SessionSummary.Compute(_recorder.Events.ToList()));
            StringAssert.Contains("Elite (work order 149): The Grave Ram", summary);
        }

        // ---------------- Nothing allocated ----------------

        static byte[] s_junk;

        [UnityTest, Timeout(240000)]
        public IEnumerator ItsFrames_InTheChase_TheTell_TheRun_TheDaze_AndTheStones_AllocateNothing()
        {
            GraveRamTestRoom r = null;
            yield return Room(x => r = x);
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            Vector3 her = Floor(r, 6, -4), ram = Floor(r, 6, 4);
            yield return Place(r, her, ram);
            r.Ram.FrameNow();
            Assert.That(() => { for (int i = 0; i < 60; i++) r.Ram.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "the chase and its looks");
            bool began = false;
            yield return UntilWindup(r, GraveRamValues.Ram, her, 6f, b => began = b);
            Assume.That(began, Is.True);
            r.Ram.FrameNow();
            Assert.That(() => { for (int i = 0; i < 60; i++) r.Ram.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "its tell");
            yield return GraveRamTestRoom.Run(0.3f, null, () => _player.Teleport(Floor(r, 2.5, -4)));
            yield return GraveRamTestRoom.Run(2f, () => r.Ram.IsRamming);
            if (r.Ram.IsRamming)
            {
                r.Ram.FrameNow();
                Assert.That(() => { for (int i = 0; i < 5; i++) r.Ram.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "its run");
            }
            yield return GraveRamTestRoom.Run(3f, () => r.Ram.CrashDazed);
            Assume.That(r.Ram.CrashDazed, Is.True);
            r.Ram.FrameNow();
            Assert.That(() => { for (int i = 0; i < 60; i++) r.Ram.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "its daze");
            Vector3 hidden = Floor(r, 10, 12.8);
            yield return r.HoldPastTheStart(Floor(r, 10, 1.5), hidden);
            yield return UntilWindup(r, GraveRamValues.Stones, hidden, 8f, b => began = b);
            Assume.That(began, Is.True);
            r.Ram.FrameNow();
            Assert.That(() => { for (int i = 0; i < 20; i++) r.Ram.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "its stones on the ground");
        }
    }
}
