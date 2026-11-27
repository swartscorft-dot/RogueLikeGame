using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 148, the rehearsal, in the ControlRoom scene through the game's intents, with what lasts between runs and her
    /// pistols' parts on (the game as it ships): a short real run with upgrades taken (the floor reward, the elite room's
    /// upgrade, a chest's step, a basic-shot upgrade, a skill, the focus choice, the rack's piece, a rank and a part) ends in her
    /// death on floor 2 and keeps its build; the main menu then shows REHEARSAL under START RUN, every word inside its shape; the
    /// rehearsal started from it puts her outside the throne room's door on floor 5 with the same build, field for field, full
    /// health, an empty focus and every cooldown ready, every other room's enemies gone and its doors open; the boss's entrance and
    /// fight wake the ordinary way as she walks in; the boss's death and her death both end it, the end screen says so with the
    /// fight's length and what killed her, and its "Again" starts a new rehearsal from a new seed and its second answer goes back to
    /// the main menu; the recorder writes it as a session of its own with "rehearsal":"true"; the between-runs save, the records
    /// and the stored build are byte for byte what they were before. A run that ends on floor 1 keeps nothing; a broken file is no
    /// entry and no error; the switch off stores nothing and shows nothing. Every file is under a temporary folder of each test,
    /// and the game's own saves are checked untouched at the end (as BetweenRunsTests.Z_TheGamesOwnSave_IsNeverTouchedByTheseTests).
    /// </summary>
    public class RehearsalPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL;     // an elite room on its floors (BetweenRunsTests' seed)
        const int MostFrames = 600;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
        static readonly float EntranceSeconds = new BossEntranceValues().Total * 3f + 10f; // the most real time the entrance (which runs on real time) may take on a busy machine
        const float MostGameSecondsForTheBossToStrike = 30f; // game seconds the boss is given to land a strike on her (it took 1.6 on 2026-11-27)
        const float MostRealSecondsForAnything = 600f;      // only a guard against a test that hangs: every wait here counts frames or game time

        // The game's own saves, as they were before these tests (OneTimeSetUp) and never touched by them.
        static string[] s_realPaths;
        static byte[][] s_realBytes;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        Keyboard _keyboard;
        PlaySessionRecorder _recorder;
        string _folder;
        bool _onBefore, _entranceBefore;

        string BetweenRunsFile => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);
        string RecordsFile => Path.Combine(_folder, StartMenu.RecordsFileName);
        string BuildFile => Path.Combine(_folder, RehearsalValues.FileName);
        string Recordings => Path.Combine(_folder, "sessions");

        [OneTimeSetUp]
        public void RememberTheRealSaves()
        {
            s_realPaths = new[] { GeneratedLevels.DefaultBetweenRunsSavePath, StartMenu.DefaultRecordsPath, GeneratedLevels.DefaultRehearsalSavePath };
            s_realBytes = s_realPaths.Select(p => File.Exists(p) ? File.ReadAllBytes(p) : null).ToArray();
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _onBefore = RehearsalValues.On;
            _entranceBefore = BossEntranceValues.On;
            RehearsalValues.On = true;
            BossEntranceValues.On = true;
            _folder = Path.Combine(Path.GetTempPath(), "RehearsalPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            GeneratedLevels.BetweenRunsSavePath = BetweenRunsFile; // never the game's own save
            StartMenu.RecordsPath = RecordsFile;                    // never his records
            GeneratedLevels.RehearsalSavePath = BuildFile;          // never his stored build
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            _menu.TestKeyboard = _keyboard;
            InterfaceStyle.ScreenForPictures = Sizes[0];
            yield return null;
            Assert.That(_levels.RehearsalFileInUse, Is.EqualTo(BuildFile), "never the game's own file");
        }

        [TearDown]
        public void CleanUp()
        {
            RehearsalValues.On = _onBefore;
            BossEntranceValues.On = _entranceBefore;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (PlaySessionRecorder.Current != null) PlaySessionRecorder.Current.StopRecording(); // the one this test started, or the rehearsal's that followed it
            _recorder = null;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            if (_player != null) _player.Modifiers = null;
            GameSettings.Unload();
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            GeneratedLevels.RehearsalSavePath = null;
            StartMenu.RecordsPath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float realSeconds)
        {
            float deadline = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>What lasts between runs on, with Thick Hide equipped, a rank of the first stat and the Laser Sight fitted.</summary>
        void TrialsOn()
        {
            MetaPiece hide = _levels.MetaValues.Pieces.First(p => p.Name == "Thick Hide");
            var s = new MetaState { Currency = 500 };
            s.Owned.Add(hide.Id);
            s.Equipped.Add(hide.Id);
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            s.Ranks[_levels.MetaValues.Ranks[0].Id] = 2;
            Assert.That(MetaSaveFile.Save(BetweenRunsFile, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
        }

        /// <summary>A choice up now is answered with its first card, as a press of 1 does.</summary>
        void AnswerAChoice()
        {
            switch (_levels.Choice)
            {
                case GeneratedLevels.ChoiceKind.FloorReward: _levels.PickReward(0); break;
                case GeneratedLevels.ChoiceKind.SkillUpgrade: _levels.PickSkillUpgrade(0); break;
                case GeneratedLevels.ChoiceKind.FocusChoice: _levels.PickFocusChoice(0); break;
                case GeneratedLevels.ChoiceKind.Chest: _levels.PickUpgrade(0); break;
            }
        }

        /// <summary>Choices are answered with their first card until none is up and <paramref name="done"/> holds.</summary>
        IEnumerator AnswerChoicesUntil(Func<bool> done)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None || done());
                if (_levels.Choice == GeneratedLevels.ChoiceKind.None && done()) yield break;
                AnswerAChoice();
                yield return Frames(2);
            }
        }

        /// <summary>
        /// A short real run: floor 1's reward taken, on to floor 2, its elite room's upgrade taken, and steps of a chest, a basic-shot
        /// card, a skill and the focus choice put on as their picks put them. She is on floor 2 after it.
        /// </summary>
        IEnumerator AShortRunWithUpgrades_OnFloor2()
        {
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            _levels.FinishFloorForTests();
            yield return AnswerChoicesUntil(() => _levels.Floor >= 2);
            Assert.That(_levels.Floor, Is.EqualTo(2), "on floor 2 after floor 1's reward");
            yield return Frames(3);
            _levels.FinishEliteRoomForTests();
            yield return AnswerChoicesUntil(() => _levels.EliteRoomDone);
            Assert.That(_levels.EliteRoomDone, Is.True, "floor 2's elite room done");
            yield return Frames(3);

            // As a chest's pick, a basic-shot card's, a skill reward's and the focus choice's take put them on.
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.06f, Tier = UpgradeTier.Rare, Tiered = true });
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 12f, Heal = 12, Tier = UpgradeTier.Epic, Tiered = true });
            _health.RefreshMax();
            _levels.Modifiers.BasicShot.Values = _levels.RunValues.BasicShot;
            _levels.Modifiers.BasicShot.KitPerMark = _player.Kit.BasicShotsPerMark;
            _levels.Modifiers.BasicShot.MarkCap = _player.Kit.MaxMarks;
            _levels.Modifiers.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            _levels.Modifiers.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            int lacking = Enumerable.Range(0, _player.Skills.Count).First(s => !_player.IsEquipped(s));
            _player.EquipSkill(lacking);
            if (!_levels.FocusChoice.Taken)
            {
                _levels.FocusChoice.Offer();
                _levels.FocusChoice.Take(FocusMode.SteadyHand);
            }
            yield return null;
            Assert.That(_levels.Modifiers.Meta.Any, Is.True, "Thick Hide on the run");
            Assert.That(_levels.Modifiers.Meta.AnyRanks, Is.True, "the rank on the run");
            Assert.That(_levels.Modifiers.Weapon.Parts, Is.Not.Empty, "the Laser Sight fitted");
            Assert.That(_levels.SkillUpgrades.Count, Is.GreaterThan(0), "the elite room's upgrade taken");
        }

        IEnumerator Die()
        {
            _health.Untouchable = false;
            _health.TakeHit(1000000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died));
            yield return null;
        }

        static byte[] Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        /// <summary>A session log's first line, read while the recorder may still hold it open.</summary>
        static string FirstLine(string path)
        {
            Assert.That(path, Is.Not.Null, "a session log");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                return reader.ReadLine() ?? "";
        }

        /// <summary>She walks in past the throne room's point; the entrance plays its whole length and the fight wakes.</summary>
        IEnumerator WalkInAndWakeTheFight(RoomFight fight)
        {
            GeneratedLevel level = _levels.Level;
            LevelRoom throne = level.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, throne);
            int begun = _levels.EntrancesBegun;
            Assert.That(fight.On, Is.False, "outside the door, the fight sleeps");
            _player.Teleport(GeneratedLevels.FromTheDoor(level, throne, door, 5.5));
            yield return Until(() => _levels.EntranceHolds || fight.On, 3f);
            Assert.That(_levels.EntrancesBegun, Is.EqualTo(begun + 1), "the boss's entrance begins the ordinary way");
            yield return Until(() => !_levels.EntranceHolds && fight.On, EntranceSeconds);
            Assert.That(_levels.EntranceHolds, Is.False, "the entrance over");
            Assert.That(fight.On, Is.True, "the fight awake at its end");
        }

        void TheBuildIsTheStoredOne(RehearsalBuild stored, string when)
        {
            RehearsalBuild now = _levels.CaptureBuild();
            List<string> differences = RehearsalRules.Differences(stored, now);
            Assert.That(differences, Is.Empty, when + ": the build she has is the stored one, field for field:\n" + string.Join("\n", differences));
            for (int k = 0; k < _player.KeyCount; k++)
            {
                int slot = _player.SkillOnKey(k);
                Assert.That(slot >= 0 ? _player.Skills[slot].Name : "", Is.EqualTo(stored.Keys[k]), when + ": the skill on key " + k);
            }
            foreach (string name in stored.Skills) Assert.That(_player.IsEquipped(_player.SlotOf(name)), Is.True, when + ": she has " + name);
            Assert.That(_levels.RehearsalSkillsMissing, Is.Empty, when + ": no skill left out");
            Assert.That(_health.Max, Is.EqualTo(stored.MaxHealth), when + ": her maximum");
            Assert.That(_health.Current, Is.EqualTo(_health.Max), when + ": full health");
            Assert.That(_player.Focus, Is.EqualTo(0f), when + ": focus empty");
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot)) Assert.That(_player.SkillCooldownRemaining(slot), Is.EqualTo(0f), when + ": " + _player.Skills[slot].Name + " ready");
            Assert.That(_player.DashCooldownRemaining, Is.EqualTo(0f), when + ": the dash ready");
            Assert.That(_player.Modifiers, Is.SameAs(_levels.Modifiers));
            Assert.That(_player.SkillUpgrades, Is.SameAs(_levels.SkillUpgrades));
            Assert.That(_levels.Modifiers.Weapon.Parts, Is.EqualTo(stored.Weapon.Parts), when + ": her pistols' parts mounted");
            Assert.That(_levels.Modifiers.Meta.Pieces, Is.EqualTo(stored.Meta.Pieces), when + ": the rack's pieces");
            Assert.That(_levels.Modifiers.Meta.Ranks, Is.EqualTo(stored.Meta.Ranks), when + ": the ranks");
            foreach (RehearsalShotTier s in stored.BasicShot) Assert.That(_player.ShotTier(s.Kind), Is.EqualTo(s.Tier), when + ": " + s.Kind + " works at its tier");
            foreach (RehearsalSkillTier t in stored.SkillTiers) Assert.That(_levels.SkillUpgrades.TierOf(t.Skill), Is.EqualTo(t.Tier), when + ": " + t.Skill + "'s tier");
        }

        void TheFloorBehindHerIsCleared(string when)
        {
            GeneratedLevel level = _levels.Level;
            LevelRoom throne = level.Layout.ExitRoom;
            Assert.That(_levels.InRehearsal && _levels.InRun, Is.True, when);
            Assert.That(_levels.Floor, Is.EqualTo(5), when + ": the throne room's floor");
            Assert.That(level.Layout.Floor, Is.EqualTo(5));
            Assert.That(level.Layout.Seed, Is.EqualTo(RunSeeds.FloorSeed(_levels.RunSeed.Value, 5)), when + ": the floor of the rehearsal's own seed, built by the generator");
            Assert.That(level.Boss, Is.Not.Null.And.Property("IsDead").False, when + ": the boss there");
            foreach (Enemy e in level.Enemies)
            {
                bool inThrone = level.EnemiesByRoom.TryGetValue(throne.Order, out List<Enemy> stay) && stay.Contains(e);
                if (inThrone) Assert.That(e != null && !e.IsDead, Is.True, when + ": the throne room's enemies stay");
                else Assert.That(e == null, Is.True, when + ": an enemy outside the throne room is gone");
            }
            Assert.That(_levels.CurrentRoom, Is.Not.Null, when + ": she stands in a room or passage");
            Assert.That(_levels.CurrentRoom, Is.Not.SameAs(throne), when + ": outside the throne room");
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, throne);
            Vector3 doorMiddle = GeneratedLevels.FromTheDoor(level, throne, door, 0.0);
            Assert.That(Flat(_player.Position, doorMiddle), Is.LessThanOrEqualTo(RehearsalValues.DoorStandOff + RehearsalValues.DoorSnap), when + ": at the throne room's door");
            foreach (GeneratedLevels.RoomDoors d in _levels.RoomDoorList) Assert.That(d.Shut, Is.False, when + ": every door open");
            foreach (LevelRoom room in level.Layout.Rooms)
                if (room.Kind == RoomKind.Treasure) Assert.That(_levels.ChestsOpened, Does.Contain(room.Order), when + ": a chest behind her is as opened");
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.None), when + ": no choice (the elite room's offer is behind her)");
            Assert.That(_levels.Earnings, Is.Null, when + ": nothing to earn");
            Assert.That(_menu.Up, Is.False, when + ": the menu gone");
        }

        // ---------------- Tests ----------------

        [UnityTest]
        public IEnumerator ARealRunsBuild_IsKept_AndTheRehearsalFromTheMenu_HasIt_AtTheThroneRoomsDoor_EndsBothWays_AndTouchesNoSave()
        {
            TrialsOn();
            yield return AShortRunWithUpgrades_OnFloor2();
            RehearsalBuild carried = _levels.CaptureBuild();
            yield return Die();
            RehearsalBuild stored = RehearsalFile.Load(BuildFile);
            Assert.That(stored, Is.Not.Null, "the run reached floor 2: its build is kept, in its own file");
            Assert.That(RehearsalRules.Differences(carried, stored), Is.Empty, "the stored build is what she carried: " + string.Join("\n", RehearsalRules.Differences(carried, stored)));
            Assert.That(stored.Floor, Is.EqualTo(2));
            Assert.That(stored.Ended, Is.EqualTo("death"));
            Assert.That(stored.Upgrades.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(stored.BasicShot.Single(s => s.Kind == BasicShotUpgradeKind.PiercingRound).Tier, Is.EqualTo(2));
            Assert.That(stored.FocusTaken, Is.True);
            Assert.That(File.ReadAllText(BetweenRunsFile), Does.Not.Contain("Keys"), "not inside the between-runs save");

            // The end screen's Enter: the main menu, with the rehearsal's line under START RUN.
            Assert.That(_levels.RehearsalEnding, Is.False, "a real run's end screen is as before");
            Assert.That(RunHud.EndNewRunButton, Is.EqualTo("Enter  main menu"));
            _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
            yield return Frames(3);
            Assert.That(_menu.Up && _menu.Screen == MenuScreen.Main, Is.True, "the main menu");
            Assert.That(_menu.MainLines[RehearsalValues.MenuPlace], Is.EqualTo(MainLine.Rehearsal), "REHEARSAL right under START RUN");
            Assert.That(_menu.MainLines[0], Is.EqualTo(MainLine.StartRun));
            Assert.That(_menu.MainLines.Count(l => l == MainLine.Rehearsal), Is.EqualTo(1), "one entry");
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_menu.TextOutside(), Is.Empty, "every word of the main menu inside its shape at " + size);
            }
            InterfaceStyle.ScreenForPictures = Sizes[0];
            GUIStyle mainStyle = InterfaceStyle.Text(true, InterfaceStyle.V.Menus.Start.MainTextSize, TextAnchor.MiddleLeft);
            Debug.Log("Work order 148: the main menu's plate holds " + (InterfaceStyle.V.Menus.Start.MainWidth - 70f) + " of words; REHEARSAL measures "
                      + InterfaceStyle.Measure("REHEARSAL", mainStyle).x + ", REHEARSAL: THE BOSS would measure " + InterfaceStyle.Measure("REHEARSAL: THE BOSS", mainStyle).x);

            // What must not change: his between-runs save, his records, the stored build.
            yield return Frames(3);
            byte[] betweenBefore = Bytes(BetweenRunsFile), recordsBefore = Bytes(RecordsFile), buildBefore = Bytes(BuildFile);
            Assert.That(betweenBefore, Is.Not.Null);
            Assert.That(recordsBefore, Is.Not.Null, "the real run was counted in the records");
            int runsInRecords = _menu.Records.RunsStarted;
            MetaState metaBefore = MetaSaveFile.Load(BetweenRunsFile, _levels.MetaValues, out _, out _);

            // The recorder is running, as in his play.
            Directory.CreateDirectory(Recordings);
            _recorder = PlaySessionRecorder.StartRecording(Recordings);
            yield return Frames(2);
            Assert.That(_recorder.IsRehearsalSession, Is.False);

            // The rehearsal from the menu entry.
            Assert.That(_menu.PickMain(MainLine.Rehearsal), Is.True, "REHEARSAL picked");
            yield return Frames(3);
            TheFloorBehindHerIsCleared("the first rehearsal");
            TheBuildIsTheStoredOne(stored, "the first rehearsal");
            ulong firstSeed = _levels.RunSeed.Value;
            Assert.That(_levels.Record.FloorText, Does.Contain("5"));
            PlaySessionRecorder session = PlaySessionRecorder.Current;
            Assert.That(session, Is.Not.Null.And.Not.SameAs(_recorder), "a session of its own");
            Assert.That(session.IsRehearsalSession, Is.True);
            string startLine = FirstLine(session.LogPath);
            StringAssert.Contains("\"rehearsal\":\"true\"", startLine, "the start line says it is a rehearsal's");
            Assert.That(FirstLine(_recorder.LogPath), Does.Not.Contain("\"rehearsal\":"), "the session before it is an ordinary one");

            // She walks in: the entrance and the fight, the ordinary way; then the boss dies.
            RoomFight fight = _levels.FightOf(_levels.Level.Layout.ExitRoom);
            Assert.That(fight, Is.Not.Null, "the throne room's fight");
            yield return WalkInAndWakeTheFight(fight);
            yield return Frames(30);
            _levels.FinishFloorForTests(); // the boss's death, as RunTests clears the act
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.ActCleared);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.ActCleared), "the boss's death ends it");
            Assert.That(_levels.InRun || _levels.InRehearsal, Is.False);
            Assert.That(_levels.RehearsalEnding, Is.True);
            Assert.That(_levels.RehearsalFightSeconds, Is.GreaterThan(0f), "the fight's length");
            yield return Frames(3);
            Assert.That(_hud.EndScreenLines[0], Is.EqualTo(RunHud.RehearsalCleared));
            Assert.That(_hud.EndScreenLines.Any(l => l.StartsWith("Rehearsal fight  ", StringComparison.Ordinal)), Is.True, string.Join(" | ", _hud.EndScreenLines));
            Assert.That(RunHud.EndNewRunButton, Is.EqualTo(RunHud.RehearsalAgain), "Enter: again");
            Assert.That(RunHud.EndSameRunButton, Is.EqualTo(RunHud.RehearsalMainMenu), "the way back to the start");
            Assert.That(Bytes(BetweenRunsFile), Is.EqualTo(betweenBefore), "no Echoes, no feats: the between-runs save byte for byte");
            Assert.That(Bytes(BuildFile), Is.EqualTo(buildBefore), "a rehearsal does not replace the stored build");

            // Again: a new rehearsal at once, from a new seed, with the same build.
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            yield return Frames(3);
            Assert.That(_levels.RehearsalsStarted, Is.EqualTo(2));
            Assert.That(_levels.RunSeed.Value, Is.Not.EqualTo(firstSeed), "a new seed");
            TheFloorBehindHerIsCleared("again");
            TheBuildIsTheStoredOne(stored, "again");
            Assert.That(PlaySessionRecorder.Current, Is.SameAs(session), "Again goes on in the rehearsal's session");

            // Her death at the boss's hands: she walks in, is left with 1 health, and the boss's strike ends it.
            fight = _levels.FightOf(_levels.Level.Layout.ExitRoom);
            yield return WalkInAndWakeTheFight(fight);
            _health.TakeHit(_health.Current - 1);
            float from = Time.time; // by game time, never real seconds: a busy machine gives fewer frames, not less fight
            yield return Until(() => _levels.Ending != GeneratedLevels.EndKind.None || Time.time - from > MostGameSecondsForTheBossToStrike, MostRealSecondsForAnything);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "her death ends it");
            Assert.That(_levels.RehearsalEnding, Is.True);
            Assert.That(_levels.RehearsalKilledBy, Is.Not.Empty, "what killed her: the last strike that landed");
            yield return Frames(3);
            Assert.That(_hud.EndScreenLines[0], Is.EqualTo(RunHud.RehearsalDied));
            Assert.That(_hud.EndScreenLines, Does.Contain("Killed by  " + _levels.RehearsalKilledBy));
            Assert.That(_hud.EndScreenLines.Last(), Does.StartWith("Run seed"), "the seed's line stays last");

            // The way back to the start: the main menu, an ordinary session again.
            Assert.That(_levels.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun), Is.True);
            yield return Frames(3);
            Assert.That(_menu.Up && _menu.Screen == MenuScreen.Main, Is.True, "back on the main menu");
            Assert.That(_levels.IsInLevel || _levels.Rehearsing, Is.False);
            Assert.That(PlaySessionRecorder.Current.IsRehearsalSession, Is.False, "an ordinary session after the rehearsals");
            Assert.That(_menu.MainLines, Does.Contain(MainLine.Rehearsal), "the build still stored");

            Assert.That(Bytes(BetweenRunsFile), Is.EqualTo(betweenBefore), "the between-runs save byte for byte");
            Assert.That(Bytes(RecordsFile), Is.EqualTo(recordsBefore), "the records byte for byte");
            Assert.That(Bytes(BuildFile), Is.EqualTo(buildBefore), "the stored build byte for byte");
            Assert.That(_menu.Records.RunsStarted, Is.EqualTo(runsInRecords), "no run counted");
            MetaState metaAfter = MetaSaveFile.Load(BetweenRunsFile, _levels.MetaValues, out _, out _);
            Assert.That(metaAfter.Currency, Is.EqualTo(metaBefore.Currency));
            Assert.That(metaAfter.Runs, Is.EqualTo(metaBefore.Runs));
        }

        [UnityTest]
        public IEnumerator ARunEndedOnFloor1_DoesNotReplaceTheStoredBuild()
        {
            var kept = new RehearsalBuild { Run = "an earlier run", Floor = 4, Ended = "death" };
            kept.Keys.Add("Quick Shot");
            Assert.That(RehearsalFile.Save(BuildFile, kept, out string error), Is.True, error);
            byte[] before = Bytes(BuildFile);
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            yield return Die();
            Assert.That(Bytes(BuildFile), Is.EqualTo(before), "a death on floor 1 does not wipe a good build");
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            _levels.BackToPracticeRoom(); // left on floor 1
            yield return null;
            Assert.That(Bytes(BuildFile), Is.EqualTo(before), "nor leaving on floor 1");
        }

        [UnityTest]
        public IEnumerator ABrokenOrMissingFile_ShowsNoEntry_AndNoError()
        {
            Assert.That(_levels.HasRehearsalBuild, Is.False, "no file");
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.MainLines, Has.No.Member(MainLine.Rehearsal));
            _menu.Close();
            File.WriteAllText(BuildFile, "{\"Version\": 1, \"Keys\": [");
            byte[] broken = Bytes(BuildFile);
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.MainLines, Has.No.Member(MainLine.Rehearsal), "a broken file: no entry");
            Assert.That(_levels.StartRehearsal(), Is.Null, "and no rehearsal");
            Assert.That(Bytes(BuildFile), Is.EqualTo(broken), "left as it was");
            _menu.Close();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AFileFromAnOlderGame_WithASkillAndAnUpgradeThatAreGone_StillShowsTheEntry_AndStartsWithTheRest()
        {
            // His file is kept across updates: one from a game that had a skill and an upgrade kind this one does not.
            var old = new RehearsalBuild { Run = "an older game's run", Floor = 3, Ended = "death" };
            string kept = _player.Skills.First(k => k != null && !k.StartEquipped).Name;
            foreach (SkillDefinition k in _player.Skills)
                if (k != null && k.StartEquipped) old.Skills.Add(k.Name);
            old.Skills.Add("A Skill Long Gone");
            old.Skills.Add(kept);
            old.Upgrades.Add(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f, Tiered = true });
            old.Upgrades.Add(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 3f, Tiered = true });
            old.SkillUpgrades.Add(new SkillUpgradeDefinition("A Skill Long Gone", "Gone Too", SkillUpgradeKind.Change, "", null, SkillPatch.Scale("Cooldown", 0.5f)));
            string text = RehearsalJson.Write(old).Replace("\"Kind\": \"FlatDamage\"", "\"Kind\": \"AKindLongGone\"");
            File.WriteAllText(BuildFile, text);
            byte[] before = Bytes(BuildFile);

            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.MainLines, Does.Contain(MainLine.Rehearsal), "the entry: what is gone is dropped, the rest kept");
            Assert.That(_menu.PickMain(MainLine.Rehearsal), Is.True, "the rehearsal starts");
            yield return Frames(3);
            Assert.That(_levels.InRehearsal, Is.True);
            Assert.That(_levels.RehearsalSkillsMissing, Is.EqualTo(new[] { "A Skill Long Gone" }), "the skill that is gone left out");
            Assert.That(_player.IsEquipped(_player.SlotOf(kept)), Is.True, "the skill after it given");
            Assert.That(_levels.Modifiers.Taken.Count, Is.EqualTo(1), "the upgrade of a kind that is gone dropped, the other kept");
            Assert.That(_levels.Modifiers.Taken[0].Kind, Is.EqualTo(UpgradeKind.Damage));
            Assert.That(Bytes(BuildFile), Is.EqualTo(before), "his file left as it was");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TheSwitchOff_StoresNothing_ShowsNoEntry_AndStartsNoRehearsal()
        {
            RehearsalValues.On = false;
            TrialsOn();
            yield return AShortRunWithUpgrades_OnFloor2();
            yield return Die();
            Assert.That(File.Exists(BuildFile), Is.False, "nothing stored with the switch off");
            Assert.That(_levels.StoredRehearsalBuild, Is.Null);
            Assert.That(RunHud.EndNewRunButton, Is.EqualTo("Enter  main menu"), "the end screen as before");
            _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
            yield return Frames(3);
            Assert.That(_menu.MainLines, Has.No.Member(MainLine.Rehearsal), "no entry");
            Assert.That(_menu.MainLines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Settings }), "the main menu as before");

            // Even with a build stored (by a run before the switch went off), nothing.
            var kept = new RehearsalBuild { Run = "an earlier run", Floor = 3, Ended = "death" };
            Assert.That(RehearsalFile.Save(BuildFile, kept, out string error), Is.True, error);
            byte[] before = Bytes(BuildFile);
            _menu.Close();
            yield return null;
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.MainLines, Has.No.Member(MainLine.Rehearsal), "no entry");
            Assert.That(_levels.HasRehearsalBuild, Is.False);
            Assert.That(_levels.StartRehearsal(), Is.Null, "no rehearsal starts");
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(Bytes(BuildFile), Is.EqualTo(before));
        }

        /// <summary>Last by name: after every other test of the class, the game's own saves are as they were, byte for byte.</summary>
        [Test]
        public void Z_TheGamesOwnSaves_AreNeverTouchedByTheseTests()
        {
            for (int i = 0; i < s_realPaths.Length; i++)
            {
                byte[] now = File.Exists(s_realPaths[i]) ? File.ReadAllBytes(s_realPaths[i]) : null;
                Assert.That(now, Is.EqualTo(s_realBytes[i]), s_realPaths[i]);
            }
        }
    }
}
