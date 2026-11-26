using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// The faults the second pair of reviews of 2026-11-20 found (work order 033), in the ControlRoom scene through the
    /// intents: the boss's death bursts on real time and nothing pending after its floor; a chest's choice never replaced
    /// by a room cleared on the same frame, and not used up by an end screen; a restart's basic shot; her death, then Esc,
    /// then a cleared room, whichever Update runs first; the effects' geometry kept and not rebuilt while frozen; an elite
    /// room's pick drawn; the listeners on a crowded floor; Quit and the elite room's feat under the trial; what is drawn
    /// over an elite; a focus window ended by the focus choice; Fresh Shells once a use; a primed spender's extra mark gives
    /// no focus; a save that cannot be read now is not overwritten.
    ///
    /// The save is written only under a temporary folder of each test (<see cref="GeneratedLevels.BetweenRunsSavePath"/>).
    /// A paused or stopped game has a time scale of 0, and a frame without a window is about 0.3 ms: the waits count
    /// frames or real time, or game time only where the game runs.
    /// </summary>
    public class ReviewFix2Tests
    {
        const ulong RunSeed = 0x0260A026UL;
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here

        PlayerCharacter _player;
        PlayerHealth _health;
        Damageable _a, _b;
        GeneratedLevels _levels;
        RunHud _hud;
        CombatEffects _fx;
        PlaceholderSounds _sounds;
        ActsFirstInFrame _first;
        ActsLastInFrame _last;
        string _folder;
        EnemySettings _enemySettings;
        bool _modelsWere;
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<Seen> _seen = new List<Seen>();

        struct Seen
        {
            public CombatFx Fx;
            public FxPiece P;
            public int Frame;
        }

        string SavePath => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            BetweenRunsSwitch.Unload();
            _folder = Path.Combine(Path.GetTempPath(), "ReviewFix2Tests-" + Guid.NewGuid().ToString("N"));
            GeneratedLevels.BetweenRunsSavePath = SavePath;
            CombatEffects.Spawned += Record;
            yield return Load();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            if (_levels == null)
            {
                _levels = GeneratedLevels.Ensure();
                _levels.MetaValues.Enabled = false; // work order 117: the trial is on in code now; this class's tests were written with it off and turn it on themselves (TrialOn)
                _levels.MetaValues.Attachments = false;
            }
            _reports.Clear();
            _player.Reported += r => _reports.Add(r);
            _first = new GameObject("ActsFirstInFrame").AddComponent<ActsFirstInFrame>();
            _last = new GameObject("ActsLastInFrame").AddComponent<ActsLastInFrame>();
        }

        [TearDown]
        public void CleanUp()
        {
            CombatEffects.Spawned -= Record;
            if (_enemySettings != null) _enemySettings.Values.UseModels = _modelsWere;
            _enemySettings = null;
            if (_first != null) Object.Destroy(_first.gameObject);
            if (_last != null) Object.Destroy(_last.gameObject);
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_fx != null) Object.Destroy(_fx.gameObject);
            _fx = null;
            if (_sounds != null) Object.Destroy(_sounds.gameObject);
            _sounds = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.SkillUpgrades = null;
            BetweenRunsSwitch.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            _seen.Clear();
        }

        void Record(CombatFx fx) => _seen.Add(new Seen { Fx = fx, P = fx.Piece, Frame = Time.frameCount });

        // ---------------- Helpers ----------------

        List<Seen> Of(FxMoment m) => _seen.Where(s => s.P.Moment == m).ToList();
        List<Seen> Of(FxMoment m, FxShape shape) => _seen.Where(s => s.P.Moment == m && s.P.Shape == shape).ToList();

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        /// <summary>Both helpers have done what they were given (the one not given anything counts as done).</summary>
        IEnumerator UntilActed()
        {
            yield return Frames(() => _first.Act == null && _last.Act == null);
            yield return null;
        }

        int Slot(string name)
        {
            int slot = _player.SlotOf(name);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no skill " + name);
            return slot;
        }

        void Give(string skill, string name)
        {
            SkillUpgradeDefinition row = SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, name);
            Assert.That(row, Is.Not.Null, skill + ": " + name);
            if (_player.SkillUpgrades == null) _player.SkillUpgrades = new RunSkillUpgrades();
            _player.SkillUpgrades.Take(row);
        }

        IEnumerator FinishFloorAndWaitForTheReward()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
        }

        IEnumerator ClearTheEliteRoom()
        {
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
        }

        /// <summary>A run to its fifth floor, the act boss's, with health nothing can take.</summary>
        IEnumerator ToTheBoss()
        {
            _health.MaxHealth = 1000000;
            _levels.StartRun(RunSeed);
            yield return null;
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return FinishFloorAndWaitForTheReward();
                Assert.That(_levels.PickReward(0), Is.True, "floor " + floor);
                if (floor == 1 && !_levels.RunValues.MixedFloorReward) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice"); // changed deliberately (work order 138): no focus screen with the floor reward of three kinds
                yield return null;
            }
            Assert.That(_levels.Floor, Is.EqualTo(5));
            Assert.That(_levels.Level.Boss, Is.Not.Null, "the act boss");
        }

        IEnumerator Die()
        {
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen is coming");
        }

        void TrialOn() => _levels.MetaValues.Enabled = true;

        int SavedCurrency() => MetaSaveFile.Load(SavePath, new MetaValues(), out _, out _).Currency;

        // ---------------- 1. The boss's death: on real time, and nothing pending after its floor ----------------

        [UnityTest]
        public IEnumerator TheBossesDeath_OnTheActClearedScreen_AllThreeBurstsPlay_OnRealTime_AndNothingFiresOnceItIsAnswered()
        {
            _fx = CombatEffects.Ensure();
            yield return ToTheBoss();
            _seen.Clear();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.ActCleared);
            Assert.That(_levels.ActCleared, Is.True);
            yield return new WaitForSecondsRealtime(Stopped);
            yield return Frames(2);
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "stopped");
            List<Seen> bursts = Of(FxMoment.BossDied, FxShape.Shards), ring = Of(FxMoment.BossDied, FxShape.Ring);
            Assert.That(bursts.Count, Is.EqualTo(3), "all three, though the game slowed to a stop");
            Assert.That(bursts.All(b => b.P.Unscaled), Is.True, "on real time, seen on the screen");
            Assert.That(ring.Count, Is.EqualTo(1));
            Assert.That(ring[0].P.Unscaled, Is.True);

            int answered = Time.frameCount;
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            yield return new WaitForSeconds(8f); // game time again: longer than any body is shown dead
            Assert.That(_seen.Where(s => s.Frame > answered && (s.P.Moment == FxMoment.BodyHidden || s.P.Moment == FxMoment.BossDied)), Is.Empty,
                        "nothing that waited on the old floor fires after it is gone");
        }

        [UnityTest]
        public IEnumerator ABossKilled_ThenARunStarts_ItsPendingBurstsNeverFire()
        {
            _fx = CombatEffects.Ensure();
            _player.Teleport(Stand);
            yield return null;
            var boss = (EnemyBoss)_levels.SpawnPractice(EnemyKind.Boss).Single();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            boss.Tuning.CallsAt = new float[0];
            yield return Frames(2);
            _seen.Clear();
            boss.Body.TakeHit(10000000);
            yield return Frames(() => Of(FxMoment.BossDied, FxShape.Shards).Count > 0);
            Assert.That(Of(FxMoment.BossDied, FxShape.Shards).Count, Is.EqualTo(1), "the first burst at once");
            _levels.StartRun(RunSeed);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Of(FxMoment.BossDied, FxShape.Shards).Count, Is.EqualTo(1), "the two that waited were cancelled with the place they were for");
        }

        // ---------------- 2. A chest and a room cleared on the same frame; a chest closed by an end screen ----------------

        [UnityTest]
        public IEnumerator AChestOpened_OnTheFrameTheEliteRoomIsCleared_KeepsItsChoice_InBothOrders_AndTheEliteRoomsComesAfter()
        {
            foreach (bool chestFirst in new[] { true, false })
            {
                _levels.StartRun(RunSeed);
                yield return null;
                GeneratedLevel.PlacedProp chest = _levels.Chests().First();
                bool opened = false;
                if (chestFirst) _first.Act = () => { _levels.FinishEliteRoomForTests(); opened = _levels.OpenChest(chest); };
                else
                {
                    _first.Act = () => _levels.FinishEliteRoomForTests();
                    _last.Act = () => opened = _levels.OpenChest(chest);
                }
                yield return UntilActed();
                yield return Frames(2);
                string order = chestFirst ? "the chest before the level's update" : "the chest after it";
                Assert.That(opened, Is.True, order + ": E opens it");
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest), order + ": its choice, not replaced");
                Assert.That(_levels.PickUpgrade(0), Is.True, order);
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
                Assert.That(_levels.EliteRoomDone, Is.True, order + ": the elite room's turn comes after the pick");
                _levels.BackToPracticeRoom();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AChestWhoseChoiceTheDeathScreenCloses_IsNotUsedUp()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Assert.That(_levels.OpenChest(chest), Is.True);
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), "closed without a pick");
            Assert.That(_levels.ChestsOpened, Has.No.Member(chest.Room.Order), "not used up");
        }

        // ---------------- 3. The basic shot starts fresh ----------------

        PlayerReport LastClick() => _reports.Last(r => r.Kind == PlayerReportKind.ShotClicked);

        [UnityTest]
        public IEnumerator ARunStartsAndEnds_WithTheBasicShotFresh_NoCountTowardAMark_AndNoWaitForTheNextShot()
        {
            _a.SetMaxHealth(1000000);
            _player.Teleport(NearA);
            yield return null;
            Assume.That(_player.Kit.BasicShotsPerMark, Is.GreaterThan(1));
            int hits = _player.BasicShotHits;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotHits > hits, 3f);
            Assume.That(_player.BasicShotsTowardMark, Is.EqualTo(1), "one hit counted toward a mark");
            _levels.StartRun(RunSeed); // in the shot's recovery
            Assert.That(_player.BasicShotsTowardMark, Is.EqualTo(0), "the run's first hits start their own count");
            _player.Shoot(_player.Position + Vector3.forward * 5f);
            Assert.That(LastClick().Result, Is.EqualTo(IntentResult.Started), "the run's first shot at once");

            yield return new WaitForSeconds(0.5f);
            yield return UntilFree();
            int fired = _player.BasicShotsFired;
            _player.Shoot(_player.Position + Vector3.forward * 5f);
            Assert.That(LastClick().Result, Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            _levels.BackToPracticeRoom(); // just after it went off
            _player.Shoot(_player.Position + Vector3.forward * 5f);
            Assert.That(LastClick().Result, Is.EqualTo(IntentResult.Started), "the practice room's first shot at once");
        }

        // ---------------- 4. Her death, then Esc, then a cleared room, in both orders ----------------

        [UnityTest]
        public IEnumerator EscOnTheFrameTheFloorIsCleared_ThePauseWins_WhicheverUpdateRunsFirst_AndTheRewardWaits()
        {
            _hud = RunHud.Ensure();
            foreach (bool escFirst in new[] { true, false })
            {
                _levels.StartRun(RunSeed);
                yield return null;
                bool escaped = false;
                if (escFirst) _first.Act = () => { _levels.FinishFloorForTests(); escaped = _hud.Escape(); };
                else
                {
                    _first.Act = () => _levels.FinishFloorForTests();
                    _last.Act = () => escaped = _hud.Escape();
                }
                yield return UntilActed();
                yield return Frames(3);
                string order = escFirst ? "Esc before the level's update" : "Esc after it";
                Assert.That(escaped, Is.True, order + ": Esc pauses");
                Assert.That(_levels.Paused, Is.True, order);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), order + ": no reward under the pause menu");
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(_levels.Resume(), Is.True);
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), order + ": the reward once resumed");
                _levels.BackToPracticeRoom();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HerDeath_OnTheFrameOfEscAndAClearedFloor_WinsOverBoth_WhicheverUpdateRunsFirst()
        {
            _hud = RunHud.Ensure();
            foreach (bool escFirst in new[] { true, false })
            {
                _levels.StartRun(RunSeed);
                yield return null;
                if (escFirst) _first.Act = () => { _levels.FinishFloorForTests(); _health.TakeHit(100000); _hud.Escape(); };
                else
                {
                    _first.Act = () => { _levels.FinishFloorForTests(); _health.TakeHit(100000); };
                    _last.Act = () => _hud.Escape();
                }
                yield return UntilActed();
                yield return Frames(3);
                string order = escFirst ? "Esc before the level's update" : "Esc after it";
                Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), order + ": the death screen");
                Assert.That(_levels.Paused, Is.False, order + ": no pause menu");
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), order + ": no reward");
                Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
                yield return null;
            }
        }

        // ---------------- 5. The effects' geometry ----------------

        static Vector3 FlatV(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        /// <summary>The band on the ground as CombatFx drew it before work order 033, vertex by vertex (its own copy of the old code).</summary>
        static Vector3[] OldBand(float inner, float outer, float angle, bool jagged, float coreShare, Vector3 direction)
        {
            outer = Mathf.Max(0f, outer);
            inner = Mathf.Clamp(inner, 0f, outer);
            float mid = Mathf.Lerp(inner, outer, 1f - coreShare);
            int segments = Mathf.Max(8, Mathf.CeilToInt(angle / 4f));
            bool closed = angle >= 359.9f;
            Vector3 forward = FlatV(direction).sqrMagnitude > 1e-6f ? FlatV(direction).normalized : Vector3.forward;
            var vertices = new Vector3[(segments + 1) * 4];
            for (int i = 0; i <= segments; i++)
            {
                float yaw = closed ? 360f * i / segments : Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / segments);
                Vector3 d = Quaternion.Euler(0f, yaw, 0f) * forward;
                float spike = jagged && i % 2 == 1 && i < segments ? 1.12f : 1f;
                int v = i * 4;
                vertices[v] = d * inner;
                vertices[v + 1] = d * mid;
                vertices[v + 2] = d * mid + Vector3.up * 0.01f;
                vertices[v + 3] = d * (mid + (outer - mid) * spike) + Vector3.up * 0.01f;
            }
            return vertices;
        }

        static Vector3[] OldPicture(FxPiece p, float t)
        {
            switch (p.Shape)
            {
                case FxShape.Wedge:
                    float outer = p.Size * Mathf.Lerp(0.55f, 1f, EaseOut(Mathf.Clamp01(t / 0.35f)));
                    return OldBand(outer * Mathf.Pow(t, 1.5f), outer, p.Angle, true, 0.12f, p.Direction);
                case FxShape.Ring:
                    float edge = p.Size * Mathf.Lerp(0.25f, 1f, EaseOut(t));
                    return OldBand(edge - p.Size * 0.35f * (1f - t), edge, 360f, false, 0.28f, p.Direction);
                default: // the outline
                    return OldBand(p.Size - Mathf.Min(p.Size, 0.22f) * (1f - t * t), p.Size, 360f, false, 0.28f, p.Direction);
            }
        }

        static void AssertSamePicture(CombatFx fx, string when)
        {
            float t = fx.Age / fx.Piece.Seconds;
            Vector3[] want = OldPicture(fx.Piece, t);
            Vector3[] got = fx.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(got.Length, Is.EqualTo(want.Length), fx.Shape + " " + when + ": its vertices");
            for (int i = 0; i < want.Length; i++)
                Assert.That(Vector3.Distance(got[i], want[i]), Is.LessThan(1e-5f), fx.Shape + " " + when + ": vertex " + i + " at t " + t);
        }

        [UnityTest]
        public IEnumerator ARingAWedgeAndAnOutline_LookExactlyAsBefore_UploadTheirIndicesOnce_AndAreNotRebuiltWhileFrozen()
        {
            _fx = CombatEffects.Ensure();
            _player.Teleport(Stand);
            yield return null;
            CombatFxValues v = _fx.Values;
            var made = new List<CombatFx>();
            foreach (FxShape shape in new[] { FxShape.Ring, FxShape.Wedge, FxShape.Outline })
            {
                CombatFx fx = _fx.Put(new FxPiece
                {
                    Shape = shape, Moment = FxMoment.HerHurt, Tier = FxTier.Key, At = Stand + new Vector3(0f, 0.06f, 3f),
                    Direction = new Vector3(1f, 0f, 2f).normalized, Size = 2.4f, Angle = 70f, Seconds = 1.6f,
                    Rim = v.HurtRim, Core = v.HurtCore, RimGlow = v.RimGlow, CoreGlow = v.CoreGlow,
                });
                Assert.That(fx, Is.Not.Null, shape + " drawn");
                made.Add(fx);
            }
            foreach (CombatFx fx in made) AssertSamePicture(fx, "as made");
            foreach (float at in new[] { 0.2f, 0.55f, 1.1f })
            {
                yield return Until(() => made[0] == null || made[0].Age >= at, 3f);
                foreach (CombatFx fx in made) AssertSamePicture(fx, "at " + at + " s");
            }
            foreach (CombatFx fx in made) Assert.That(fx.IndexUploads, Is.EqualTo(1), fx.Shape + ": its index list uploaded once");

            Time.timeScale = 0f; // a pause or a hit stop
            yield return null;
            int[] builds = made.Select(f => f.Builds).ToArray();
            yield return Frames(5);
            for (int i = 0; i < made.Count; i++)
            {
                Assert.That(made[i].Builds, Is.EqualTo(builds[i]), made[i].Shape + ": nothing rebuilt while it is frozen");
                AssertSamePicture(made[i], "frozen");
            }
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            for (int i = 0; i < made.Count; i++)
            {
                Assert.That(made[i].Builds, Is.GreaterThan(builds[i]), made[i].Shape + ": it goes on once time runs");
                AssertSamePicture(made[i], "after");
            }
        }

        // ---------------- 6. An elite room's pick ----------------

        [UnityTest]
        public IEnumerator AnEliteRoomsUpgradePick_IsDrawnAsAChoiceTaken()
        {
            _fx = CombatEffects.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            yield return ClearTheEliteRoom();
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade), "an upgrade to pick");
            yield return Frames(2);
            _seen.Clear();
            Assert.That(_levels.PickSkillUpgrade(0), Is.True);
            yield return Frames(2);
            Assert.That(Of(FxMoment.ChoiceTaken, FxShape.Ring).Count, Is.EqualTo(1), "the pick's ring, as for every other pick");
        }

        // ---------------- 7. The listeners on a crowded floor ----------------

        [UnityTest]
        public IEnumerator ACrowd_TheListenersLookForGoneEnemies_OnlyWhenOneWasDestroyed()
        {
            _fx = CombatEffects.Ensure();
            _sounds = PlaceholderSounds.Ensure();
            _player.Teleport(Stand);
            yield return null;
            var made = new List<Enemy>();
            for (int i = 0; i < 14; i++) made.AddRange(_levels.SpawnPractice(EnemyKind.Swarmer));
            yield return null;
            foreach (Enemy e in made) e.enabled = false; // still, out of the fight: heard already
            yield return Frames(3);
            Assume.That(_fx.Listening, Is.GreaterThan(64), "a crowd");
            Assume.That(_sounds.Listening, Is.GreaterThan(64));
            int fx = _fx.GoneLooks, sounds = _sounds.GoneLooks;
            yield return Frames(4);
            Assert.That(_levels.Pause(), Is.True);
            yield return Frames(4);
            Assert.That(_levels.Resume(), Is.True);
            Assert.That(_fx.GoneLooks, Is.EqualTo(fx), "the effects: nothing destroyed, nothing looked for, paused or not");
            Assert.That(_sounds.GoneLooks, Is.EqualTo(sounds), "the sounds: the same");

            int fxListening = _fx.Listening, soundsListening = _sounds.Listening;
            Object.Destroy(made[0].gameObject);
            yield return Frames(2);
            Assert.That(_fx.GoneLooks, Is.EqualTo(fx + 1), "one destroyed: one look");
            Assert.That(_sounds.GoneLooks, Is.EqualTo(sounds + 1));
            Assert.That(_fx.Listening, Is.EqualTo(fxListening - 1), "and it is forgotten");
            Assert.That(_sounds.Listening, Is.EqualTo(soundsListening - 1));
            yield return Frames(3);
            Assert.That(_fx.GoneLooks, Is.EqualTo(fx + 1), "and no more after");
            _levels.ClearSpawned();
        }

        // ---------------- 8. Quit banks the run (the trial) ----------------

        [UnityTest]
        public IEnumerator Quit_WithTheTrialOn_EndsTheRunAsLeavingDoes_SoWhatItEarnedIsSaved_AndWithItOffChangesNothing()
        {
            TrialOn();
            _hud = RunHud.Ensure();
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            yield return ClearTheEliteRoom();
            if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
            yield return null;
            int earned = _levels.Earnings.Total(_levels.MetaValues);
            Assert.That(earned, Is.GreaterThan(0));
            Assert.That(_hud.Escape(), Is.True, "the pause menu");
            Assert.That(File.Exists(SavePath), Is.False);
            _levels.EndRunToQuit();
            Assert.That(_levels.InRun, Is.False, "the run ended");
            Assert.That(File.Exists(SavePath), Is.True, "the save is written");
            Assert.That(SavedCurrency(), Is.EqualTo(earned), "with what the run earned");
            yield return null;

            _levels.MetaValues.Enabled = false;
            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            _levels.EndRunToQuit();
            Assert.That(_levels.InRun, Is.True, "the trial off: nothing changes");
            Assert.That(_levels.Paused, Is.True);
            Assert.That(SavedCurrency(), Is.EqualTo(earned));
        }

        // ---------------- 9. The elite room's feat (the trial) ----------------

        LevelRoom EliteRoom => _levels.Level.Layout.Rooms.First(r => r.Elite);

        [UnityTest]
        public IEnumerator TheEliteFeat_IsNotLost_ByDroppingUnderHalfOutsideTheRoom()
        {
            TrialOn();
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            LevelRoom elite = EliteRoom;
            _player.Teleport(_levels.Level.Middle(elite));
            yield return Frames(() => _levels.CurrentRoom == elite);
            Assume.That(_levels.CurrentRoom, Is.SameAs(elite), "in the elite room");
            _player.Teleport(_levels.Level.PlayerStart);
            yield return Frames(() => _levels.CurrentRoom != elite);
            Assert.That(_levels.CurrentRoom, Is.Not.SameAs(elite), "stepped out");
            _health.Untouchable = false;
            _health.TakeHit(_health.Max * 6 / 10);
            _health.Untouchable = true;
            yield return Frames(3);
            _health.RestoreFull();
            yield return Frames(2);
            yield return ClearTheEliteRoom();
            Assert.That(_levels.EliteRoomDone, Is.True);
            Assert.That(_levels.Earnings.Feats, Has.Member(MetaFeat.EliteAboveHalf), "under half outside the room is not in the room");
        }

        [UnityTest]
        public IEnumerator TheEliteFeat_IsLost_ByDroppingUnderHalfOnTheFrameSheEntersTheRoom()
        {
            TrialOn();
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            LevelRoom elite = EliteRoom;
            _player.Teleport(_levels.Level.Middle(elite));
            _health.Untouchable = false;
            _health.TakeHit(_health.Max * 6 / 10);
            _health.Untouchable = true;
            yield return null; // the frame she is first in the room
            _health.RestoreFull();
            Assume.That(_levels.CurrentRoom, Is.SameAs(elite), "in the elite room");
            yield return Frames(3);
            yield return ClearTheEliteRoom();
            Assert.That(_levels.EliteRoomDone, Is.True);
            Assert.That(_levels.Earnings.Feats, Has.No.Member(MetaFeat.EliteAboveHalf), "under half in the room, the frame she came in");
        }

        // ---------------- 11. What is drawn over an elite ----------------

        IEnumerator WithModels(bool on)
        {
            _enemySettings = Object.FindAnyObjectByType<EnemyBrute>().Settings;
            Assert.That(_enemySettings, Is.Not.Null, "the enemy settings");
            _modelsWere = _enemySettings.Values.UseModels;
            _enemySettings.Values.UseModels = on;
            if (_first != null) Object.Destroy(_first.gameObject);
            if (_last != null) Object.Destroy(_last.gameObject);
            yield return Load();
        }

        /// <summary>The top of what is shown of it over its ground point: its model's head, or the top of its renderers.</summary>
        static float ShownTop(Enemy e)
        {
            if (e.Look != null) return (float)e.Look.Spec.Height * e.transform.lossyScale.y;
            float top = float.NegativeInfinity;
            foreach (Renderer r in e.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.GetComponent<MarkPips>() != null || r.GetComponent<StaggerBarView>() != null) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            return top - e.transform.position.y;
        }

        IEnumerator OverAnElite(bool models)
        {
            yield return WithModels(models);
            _fx = CombatEffects.Ensure();
            _player.Teleport(Stand);
            yield return null;
            Material material = _player.ShotFxMaterial;
            float barGap = float.NaN;
            foreach (EnemyKind kind in new[] { EnemyKind.Guard, EnemyKind.Brute, EnemyKind.Swarmer, EnemyKind.Shooter })
            {
                Enemy plain = _levels.SpawnPractice(kind)[0];
                Enemy elite = _levels.SpawnPractice(kind)[0];
                EnemyFactory.MakeElite(elite, 400, 1.5f, 4f / 3f, 0.5f);
                yield return Frames(2);
                foreach (Enemy e in _levels.Spawned) e.enabled = false; // they stand where they are
                string what = kind + (models ? " (models)" : " (capsules)");
                if (models) Assert.That(elite.ModelHeight, Is.EqualTo(ShownTop(elite)).Within(1e-3f), what + ": the height as shown");

                var pips = new Dictionary<Enemy, MarkPips>();
                foreach (Enemy e in new[] { plain, elite })
                {
                    pips[e] = MarkPips.For(e.Body, material);
                    e.Body.AddMarks(1, 5, 30f);
                    e.Body.DazeFor(2f);
                }
                yield return Frames(2);
                float Gap(Enemy e, float y) => y - e.transform.position.y - ShownTop(e);
                Assert.That(Gap(elite, pips[elite].RowCentre().y), Is.EqualTo(Gap(plain, pips[plain].RowCentre().y)).Within(0.05f),
                            what + ": its marks as far over its head as an ordinary one's");
                List<Seen> orbits = Of(FxMoment.EnemyDazed, FxShape.Orbit);
                Seen orbitOf(Enemy e) => orbits.FirstOrDefault(s => s.P.Follow == e.transform);
                Assume.That(orbitOf(plain).Fx, Is.Not.Null, what + ": the dazed stars over the ordinary one");
                Assume.That(orbitOf(elite).Fx, Is.Not.Null, what + ": and over the elite");
                Assert.That(orbitOf(elite).P.FollowOffset.y - ShownTop(elite), Is.EqualTo(orbitOf(plain).P.FollowOffset.y - ShownTop(plain)).Within(0.05f),
                            what + ": its dazed stars too");
                StaggerBarView bar = elite.GetComponentInChildren<StaggerBarView>(true);
                Assert.That(bar, Is.Not.Null, what + ": an elite has a stagger bar");
                if (kind == EnemyKind.Guard)
                {
                    StaggerBarView plainBar = plain.GetComponentInChildren<StaggerBarView>(true);
                    barGap = Gap(plain, plainBar.transform.position.y);
                }
                Assert.That(Gap(elite, bar.transform.position.y), Is.EqualTo(barGap).Within(0.05f), what + ": its stagger bar as far over its head as an ordinary guard's");
                _levels.ClearSpawned();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WhatIsDrawnOverAnElite_IsAsFarOverItsHeadAsOverAnOrdinaryOne_WithModels() => OverAnElite(true);

        [UnityTest]
        public IEnumerator WhatIsDrawnOverAnElite_IsAsFarOverItsHeadAsOverAnOrdinaryOne_WithCapsules() => OverAnElite(false);

        // ---------------- 12. A focus window and the focus choice ----------------

        [UnityTest]
        public IEnumerator AFocusWindowOpenedOnFloor1_EndsWithTheFocusChoice_EitherWay_AndTheFocusIsKept()
        {
            KitSwitchesForTests.SkillsOnlyReward(_levels); // changed deliberately (work order 138): this pins the focus screen after floor 1, which its reward of three kinds takes away
            foreach (int pick in new[] { 1, 0 })
            {
                _levels.StartRun(RunSeed);
                yield return null;
                _player.FillFocus();
                Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "Z in floor 1's last fight");
                Assert.That(_player.IsEmpowered, Is.True);
                yield return FinishFloorAndWaitForTheReward();
                Assert.That(_levels.PickReward(0), Is.True);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FocusChoice));
                Assert.That(_player.IsEmpowered, Is.True, "frozen through the choices");
                float focus = _player.Focus;
                Assert.That(_levels.PickFocusChoice(pick), Is.True);
                string way = pick == 1 ? "Steady Hand" : "Hair Trigger";
                Assert.That(_player.IsEmpowered, Is.False, way + ": the old window is over");
                Assert.That(_player.IsSteadyHandWindow, Is.False, way + ": no free window");
                Assert.That(_player.IsPrimed, Is.False, way);
                Assert.That(_player.Focus, Is.EqualTo(focus), way + ": the focus is kept");
                yield return null;
                Assert.That(_player.IsEmpowered, Is.False, way);
                _levels.BackToPracticeRoom();
                yield return null;
            }
        }

        // ---------------- 13. Fresh Shells, once a use ----------------

        [UnityTest]
        public IEnumerator FreshShells_TwoBlastsInSteadyHandsWindow_EachKilling_CutTheCooldownOnce()
        {
            int sb = Slot("Scatter Blast");
            Give("Scatter Blast", "Fresh Shells");
            Assert.That(_levels.SetPracticeFocus(FocusMode.SteadyHand), Is.True);
            _player.Teleport(NearA);
            yield return null;
            int damage = _player.Skills[sb].Damage;
            _a.SetMaxHealth(1);
            _b.SetMaxHealth(damage + 1); // the first blast leaves it alive, the second kills it
            _b.transform.position = _a.transform.position + (Vector3.right * 0.7f);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assume.That(_player.IsSteadyHandWindow, Is.True);
            Assume.That(_player.Skills[sb].KillCooldownCut, Is.EqualTo(0.5f), "Fresh Shells in the window's view");
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_a.IsAlive && !_b.IsAlive, 3f);
            Assume.That(!_a.IsAlive && !_b.IsAlive, Is.True, "a kill on each blast");
            float full = _player.CooldownOf(sb);
            // Changed deliberately for work order 071: in the window the cooldown runs faster (the gains), so the 0.6 s the
            // two blasts may take are that many times as long on it.
            Assert.That(_player.SkillCooldownRemaining(sb), Is.GreaterThan(full * 0.5f - 0.6f * _player.FocusCooldownRate(sb)), "half off once, not twice");
            Assert.That(_player.SkillChargesReady(sb), Is.EqualTo(0), "not ready again at once");
        }

        // ---------------- 14. A primed spender's extra mark ----------------

        [UnityTest]
        public IEnumerator APrimedSpender_ItsExtraMarkCountsForDamage_ButGivesNoFocus()
        {
            Assert.That(_levels.SetPracticeFocus(FocusMode.HairTrigger), Is.True);
            int sb = Slot("Scatter Blast");
            _a.SetMaxHealth(1000000);
            _player.Teleport(NearA);
            yield return null;

            _a.AddMarks(2, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _reports.Any(r => r.Kind == PlayerReportKind.MarksSpent && r.Target == _a), 3f);
            PlayerReport plain = _reports.Last(r => r.Kind == PlayerReportKind.MarksSpent && r.Target == _a);
            yield return UntilFree();

            _player.StartFresh();
            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started), "a part: the next skill primed");
            Assert.That(_player.IsPrimed, Is.True);
            _reports.Clear();
            _a.AddMarks(2, _player.Kit.MaxMarks, _player.Kit.MarkLifetime);
            Assert.That(_player.PressSkill(sb, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _reports.Any(r => r.Kind == PlayerReportKind.MarksSpent && r.Target == _a), 3f);
            PlayerReport primed = _reports.Last(r => r.Kind == PlayerReportKind.MarksSpent && r.Target == _a);

            Assert.That(plain.Marks, Is.EqualTo(2));
            Assert.That(primed.Marks, Is.EqualTo(3), "primed: a mark more counted");
            Assert.That(primed.Damage, Is.GreaterThan(plain.Damage), "for its damage");
            Assert.That(plain.Focus, Is.GreaterThan(0f));
            Assert.That(primed.Focus, Is.EqualTo(plain.Focus).Within(1e-3f), "but the focus of the two real marks only");
        }

        // ---------------- 15. A save that cannot be read now (the trial) ----------------

        [UnityTest]
        public IEnumerator ASaveThatCannotBeReadWhenARunStarts_IsNotOverwritten_AndIsReadTheNextTime()
        {
            TrialOn();
            Assert.That(MetaSaveFile.Save(SavePath, new MetaState { Currency = 50 }, out string error), Is.True, error);
            using (new FileStream(SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // locked for a moment
                _levels.StartRun(RunSeed);
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Unreadable), "could not read");
            Assert.That(_levels.Earnings, Is.Null, "the trial is inert for this run");
            yield return null;
            _health.Untouchable = true;
            yield return ClearTheEliteRoom();
            if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
            yield return null;
            yield return Die();
            Assert.That(SavedCurrency(), Is.EqualTo(50), "nothing saved over it");
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            yield return null;

            _levels.StartRun(RunSeed);
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Read), "tried again, and read");
            Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(50));
            Assert.That(_levels.Earnings, Is.Not.Null, "this run earns");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(SavedCurrency(), Is.EqualTo(50));
        }
    }
}
