using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Her pistols' parts (work order 060, 2026-11-21; decision 0014, the producer's proposal, a draft Kristian has not ruled
    /// on), in the ControlRoom scene, through the intents. Off (the default): with the between-runs trial on and a save that
    /// owns every part, the screen, a run's basic shot, its earnings and the save are 029's, and no laser object exists; with
    /// the trial off nothing is shown, earned or written. On: a run that clears floor 1 earns its rooms too, the end screen
    /// says so and what the Echoes buy; buying the Laser Sight on the screen fits it, and in the next run its beams show from
    /// her muzzles and never end on the dummy her shot aims at, and the shot still ends at cover (work order 102); each part is felt only when fitted (reach, width, damage, stagger, the interval counted over real
    /// time, the speed on the move); no timing window changes; the practice room keeps the stock gun; the pause menu's line
    /// turns both switches on, and its stats name the parts; the recorder writes the weapon only when on; a run started as the
    /// test pilot starts one has the stock gun; and the beams allocate nothing per frame.
    ///
    /// The save is written only under a temporary folder of each test (<see cref="GeneratedLevels.BetweenRunsSavePath"/>, set
    /// before any switch is on), and the game's own save file is checked untouched, its bytes and its time, at the end. Every
    /// PlayerPrefs key used is cleared before each test and after it, and what this machine had under it is put back. A paused
    /// game has a time scale of 0, and a frame without a window is about 0.3 ms: the waits count frames or real time.
    /// </summary>
    public class WeaponAttachmentsTests
    {
        const ulong RunSeed = 0x0E117E17UL; // an elite brute on floor 1 (BetweenRunsTests, SkillUpgradeTests)
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here
        static readonly Vector3 RunFrom = new Vector3(-6f, 0f, 0f); // MoveShotTests'
        static readonly string[] Keys = { BetweenRunsSwitch.Key, AttachmentsSwitch.Key, GameSettings.DashKey, GameSettings.VolumeKey, GameSettings.DamageNumbersKey };

        // The game's own save, as it was before these tests (OneTimeSetUp): there or not, its bytes and its time.
        static string s_realPath;
        static bool s_realExisted;
        static byte[] s_realBytes;
        static DateTime s_realWritten;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        Damageable _a;
        string _folder;
        string _recordings;
        PlaySessionRecorder _recorder;
        readonly Dictionary<string, int?> _prefsBefore = new Dictionary<string, int?>();
        readonly List<Object> _made = new List<Object>();
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<double> _reportRealTimes = new List<double>();

        string SavePath => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);

        [OneTimeSetUp]
        public void RememberTheRealSave()
        {
            s_realPath = GeneratedLevels.DefaultBetweenRunsSavePath;
            s_realExisted = File.Exists(s_realPath);
            s_realBytes = s_realExisted ? File.ReadAllBytes(s_realPath) : null;
            s_realWritten = s_realExisted ? File.GetLastWriteTimeUtc(s_realPath) : DateTime.MinValue;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _prefsBefore.Clear();
            foreach (string k in Keys) _prefsBefore[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            ClearKeys();
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            _folder = Path.Combine(Path.GetTempPath(), "WeaponAttachmentsTests-" + Guid.NewGuid().ToString("N"));
            GeneratedLevels.BetweenRunsSavePath = SavePath; // before any switch is on
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _a.SetMaxHealth(1000000);
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _levels.RunValues.StatsAndTiers = false; // the feel's numbers (a test that wants her stats turns them on)
            _levels.DropValues.On = false; // a cleared room's choice comes up by itself, as the test pilot's harness has it (work order 042)
            _hud = RunHud.Ensure();
            _reports.Clear();
            _reportRealTimes.Clear();
            _player.Reported += r => { _reports.Add(r); _reportRealTimes.Add(Time.realtimeSinceStartupAsDouble); };
            yield return null;
            Assert.That(GeneratedLevels.BetweenRunsSavePathInUse, Is.EqualTo(SavePath), "never the game's own save");
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_recordings != null && Directory.Exists(_recordings)) Directory.Delete(_recordings, true);
            _recordings = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (CastBar.Current != null) Object.Destroy(CastBar.Current.gameObject);
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.Modifiers = null;
            GameSettings.Unload();
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            ClearKeys();
            foreach (KeyValuePair<string, int?> p in _prefsBefore)
                if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value);
            PlayerPrefs.Save();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static void ClearKeys()
        {
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator UntilFree() =>
            Until(() => !_player.IsLocked && _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f && _player.RecastSlot < 0, 5f);

        /// <summary>The between-runs trial on, through the values.</summary>
        void On() => _levels.MetaValues.Enabled = true;
        /// <summary>The attachments on, through the values (the pause menu's line is its own test).</summary>
        void Attach() => _levels.MetaValues.Attachments = true;

        static readonly string[] All = { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip };
        static readonly string[] Three = { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip };

        /// <summary>A temporary save, written before the game reads it: every part owned, three fitted (one a slot), and some currency.</summary>
        MetaState WriteFullSave(int currency = 100)
        {
            var s = new MetaState { Currency = currency };
            s.AttachmentsOwned.AddRange(All);
            s.AttachmentsFitted.AddRange(Three);
            Directory.CreateDirectory(_folder);
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            return s;
        }

        /// <summary>A run's modifiers in the practice room with these parts on, as a run puts them (the way other tests make "a run").</summary>
        RunModifiers Fitted(params string[] ids)
        {
            var m = new RunModifiers();
            foreach (string id in ids) m.Weapon.Add(WeaponRack.Find(_levels.MetaValues, id));
            _player.Modifiers = m;
            return m;
        }

        IEnumerable<PlayerReport> Reports(PlayerReportKind kind) => _reports.Where(r => r.Kind == kind);

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>Every dummy but A, and the practice brute, out of the way of the shots here.</summary>
        void ClearTheRange()
        {
            int k = 0;
            foreach (string name in new[] { "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Put(d.transform, new Vector3(-17f + 3f * k++, 0f, -17f)); // a corner of the practice floor (40 m across), off every line here
            }
            EnemyBrute brute = Object.FindAnyObjectByType<EnemyBrute>();
            if (brute != null) brute.enabled = false;
        }

        /// <summary>One basic shot from <paramref name="from"/> toward <paramref name="cursor"/>; true when A took it.</summary>
        IEnumerator ShootFrom(Vector3 from, Vector3 cursor, bool[] hit)
        {
            yield return UntilFree();
            _player.Teleport(from);
            Physics.SyncTransforms();
            yield return null;
            int hits = _a.HitsTaken, fired = _player.BasicShotsFired;
            _player.Shoot(cursor);
            yield return Until(() => _player.BasicShotsFired > fired, 2f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(fired + 1), "the shot went off");
            yield return null;
            hit[0] = _a.HitsTaken > hits;
            yield return new WaitForSeconds(0.35f); // past any shot's recovery
        }

        /// <summary>Kills every combat room's enemies of the floor, a room at a time, waiting a frame so each counts as cleared; takes the elite room's choice.</summary>
        IEnumerator ClearEveryCombatRoom()
        {
            GeneratedLevel level = _levels.Level;
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if (room.Kind != RoomKind.Combat || !level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) continue;
                foreach (Enemy e in list.ToList())
                    if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
                yield return null;
                yield return null;
                if (room.Elite) yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
                if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
                yield return null;
            }
        }

        int OrdinaryCombatRooms()
        {
            GeneratedLevel level = _levels.Level;
            return level.Layout.Rooms.Count(r => r.Kind == RoomKind.Combat && !r.Elite && level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) && l.Count > 0);
        }

        IEnumerator FinishFloor1()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
            Assert.That(_levels.PickReward(0), Is.True, "floor 1's reward");
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(2));
        }

        IEnumerator Die()
        {
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen is coming");
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
        }

        /// <summary>A direction from her in the floor's room with no cover for <paramref name="metres"/>.</summary>
        Vector3 ClearWay(float metres)
        {
            GroundPoint from = _player.GroundPosition;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Cover.ClearDistance(from, new GroundPoint(from.X + dir.x * metres, from.Z + dir.z * metres)) >= metres - 1e-3f) return dir;
            }
            Assert.Fail("no clear way of " + metres + " m from her");
            return Vector3.forward;
        }

        /// <summary>A pillar as a level builds it (CoverTests): on the walls' layer, cover.</summary>
        GameObject Pillar(Vector3 at)
        {
            LevelProp prop = LevelProp.Make(PropModel.Pillar, new LevelPoint(at.x, at.z), 0, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_Pillar", 0.9f);
            if (prop.IsCover(new PropValues())) go.AddComponent<Cover>();
            _made.Add(go);
            return go;
        }

        Dictionary<string, int?> Prefs() => Keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null);

        // ---------------- Off: no trace ----------------

        [UnityTest]
        public IEnumerator Off_TheTrialOn_ASaveWithEveryPart_TheScreensLinesAreThoseOfASaveWithNone_AndNoWeaponLines()
        {
            MetaState full = WriteFullSave();
            On();
            Assert.That(_levels.AttachmentsOn, Is.False, "off, as this class sets it (work order 117: on in code now)"); // changed deliberately: it said "off in code"
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            List<string> withParts = _hud.BetweenRunsLines.ToList();
            Assert.That(_hud.WeaponLines, Is.Empty, "no weapon lines");
            Assert.That(_levels.BetweenRunsState.AttachmentsOwned, Is.EqualTo(full.AttachmentsOwned), "the save's parts are read and kept");

            // The same save with no parts: the same lines.
            MetaState s = _levels.BetweenRunsState;
            var owned = s.AttachmentsOwned.ToList();
            var fitted = s.AttachmentsFitted.ToList();
            s.AttachmentsOwned.Clear();
            s.AttachmentsFitted.Clear();
            yield return null;
            Assert.That(_hud.BetweenRunsLines, Is.EqualTo(withParts), "the screen of 029, word for word");
            Assert.That(_hud.WeaponLines, Is.Empty);
            s.AttachmentsOwned.AddRange(owned);
            s.AttachmentsFitted.AddRange(fitted);

            // Its intents do nothing; the rack's own keys are 029's: the arrows go round its lines only.
            Assert.That(_levels.BuyAttachment(WeaponAttachments.GhostRing), Is.EqualTo(MetaBuy.Unknown));
            Assert.That(_levels.UseAttachment(WeaponAttachments.LaserSight), Is.False);
            Assert.That(_levels.TakeOffAttachment(WeaponAttachments.LaserSight), Is.False);
            Assert.That(_levels.FitAttachment(WeaponAttachments.GhostRing), Is.EqualTo(AttachmentFit.Unknown));
            Assert.That(_hud.PickWeaponLine(0), Is.False);
            Assert.That(s.AttachmentsFitted, Is.EqualTo(Three), "unchanged");
            Assert.That(s.Currency, Is.EqualTo(100));
            _levels.LeaveBetweenRuns();

            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            // The pause menu: its line, reading "off", the only trace.
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.MenuLines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  off"));
            _hud.Escape();
            Assert.That(Prefs().Values.All(v => v == null), "no key written");
        }

        [UnityTest]
        public IEnumerator Off_ARunsBasicShot_IsTheFeels_NoLaser_ItEarns029sTotal_AndTheSaveKeepsItsParts()
        {
            MetaState full = WriteFullSave();
            On();
            FeelValues feel = _player.Feel;
            Assert.That(_levels.StartNewRun(), Is.Null, "the screen");
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            _health.Untouchable = true;
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False, "nothing applied");
            Assert.That(_player.BasicShotRange, Is.EqualTo(feel.BasicShotRange));
            Assert.That(_player.BasicShotHalfWidth, Is.EqualTo(feel.BasicShotHalfWidth));
            Assert.That(_player.BasicShotInterval, Is.EqualTo(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery).Within(1e-6f));
            Assert.That(_player.BasicShotBaseInterval, Is.EqualTo(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery).Within(1e-6f));
            Assert.That(_player.Weapon.MovingShare(feel.MovingShotSpeedShare), Is.EqualTo(feel.MovingShotSpeedShare));
            Assert.That(_player.Weapon.BackpedalShare(feel.BackpedalSpeedShare), Is.EqualTo(feel.BackpedalSpeedShare));

            // Her shot at a dummy in the floor's first room: the feel's damage.
            Vector3 way = ClearWay(5f);
            Put(_a.transform, _player.Position + way * 3f);
            Physics.SyncTransforms();
            yield return null;
            var dealt = new List<int>();
            _a.Damaged += d => dealt.Add(d);
            _player.Shoot(_a.transform.position);
            yield return Until(() => dealt.Count > 0, 2f);
            Assert.That(dealt, Is.EqualTo(new[] { Placeholders.BasicShotDamage }), "40");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_player.Laser.Object, Is.Null, "no laser object");
            Assert.That(GameObject.Find("LaserSight"), Is.Null);
            Assert.That(_hud.StatLines(), Is.Empty, "no weapon line in the pause menu's stats");

            // Every combat room cleared, the elite room among them, and the floor: 029's 25, no rooms.
            yield return ClearEveryCombatRoom();
            Assert.That(OrdinaryCombatRooms(), Is.GreaterThan(0), "rooms that would pay with the attachments on");
            yield return FinishFloor1();
            Assert.That(_levels.Earnings.RoomsCleared, Is.Zero);
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(25), "10 for the floor, 15 for its elite room: 029's");

            yield return Die();
            Assert.That(_hud.EndScreenText, Does.Not.Contain(", rooms ").And.Not.Contain("(rooms ").And.Not.Contain("Weapon"));
            StringAssert.Contains("Echoes  +25 this run  (floors 10, elite rooms 15),  125 in all", _hud.EndScreenText);
            MetaState saved = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(saved.Currency, Is.EqualTo(125));
            Assert.That(saved.AttachmentsOwned, Is.EqualTo(full.AttachmentsOwned), "the parts kept, unchanged");
            Assert.That(saved.AttachmentsFitted, Is.EqualTo(full.AttachmentsFitted));
        }

        [UnityTest]
        public IEnumerator Off_TheTrialOff_NothingShownEarnedOrWritten_AndAnAttachmentsLineStoredOn_DoesNothing()
        {
            WriteFullSave();
            byte[] before = File.ReadAllBytes(SavePath);
            DateTime written = File.GetLastWriteTimeUtc(SavePath);
            _levels.MetaValues.Attachments = true; // the attachments' own switch on, the trial off
            AttachmentsSwitch.Set(true);            // and the line stored on, as a machine might have it
            Assert.That(_levels.MetaOn, Is.False);
            Assert.That(_levels.AttachmentsOn, Is.False, "nothing without the trial");
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  off"), "the trial off: off, whatever is stored");
            _hud.Escape();

            Assert.That(_levels.StartNewRun(), Is.Not.Null, "a run at once, no screen");
            Assert.That(_levels.BetweenRunsUp, Is.False);
            yield return null;
            _health.Untouchable = true;
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False);
            Assert.That(_player.BasicShotRange, Is.EqualTo(_player.Feel.BasicShotRange));
            Assert.That(_levels.Earnings, Is.Null, "no meta run");
            yield return ClearEveryCombatRoom();
            yield return FinishFloor1();
            Assert.That(_levels.Earnings, Is.Null, "nothing earned");
            Assert.That(_hud.WeaponLines, Is.Empty);
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.OpenBetweenRuns(), Is.False, "no screen");
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(before), "the save not written");
            Assert.That(File.GetLastWriteTimeUtc(SavePath), Is.EqualTo(written));
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Missing), "not even read");
        }

        // ---------------- The loop ----------------

        [UnityTest]
        public IEnumerator TheLoop_ClearingFloor1_EarnsItsRooms_TheEndScreenSaysSo_AndBuyingTheLaserSight_FitsIt()
        {
            On();
            Attach();
            Assert.That(_levels.AttachmentsOn, Is.True);
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True);
            yield return null;
            Assert.That(_hud.WeaponLines[0], Is.EqualTo("Loadout (trial)"));
            Assert.That(_hud.WeaponLines[1], Is.EqualTo("put together before the run: owned parts fit and come off freely"));
            Assert.That(_hud.WeaponLines.Where(l => !l.Contains("\n")).Skip(2), Is.EqualTo(new[] { "Pistols:  the basic shot", "SIGHT", "BARREL", "GRIP" }),
                        "the weapon's head, then a head each slot");
            StringAssert.StartsWith("Laser Sight   30 Echoes, 30 more\nA red beam on her pistols. Reach 12 m.\nCost:  The shot's line is narrower: aim truer.", _hud.WeaponText.Split(new[] { "SIGHT\n" }, StringSplitOptions.None)[1]); // work order 102: its words
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            _health.Untouchable = true;
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False, "nothing owned yet: the stock gun");

            int ordinary = OrdinaryCombatRooms();
            yield return ClearEveryCombatRoom();
            Assert.That(_levels.Earnings.RoomsCleared, Is.EqualTo(ordinary), "each ordinary combat room, once");
            Assert.That(_levels.Earnings.ElitesCleared, Is.EqualTo(1));
            yield return FinishFloor1();
            int expected = 10 + 15 + 3 * ordinary;
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(expected), "the rules' total, rooms included");
            Debug.Log("Work order 060: floor 1 of run " + LevelSeed.ToText(RunSeed) + " has " + ordinary + " ordinary combat rooms; clearing it earns " + expected);

            yield return Die();
            string text = _hud.EndScreenText;
            StringAssert.Contains("Echoes  +" + expected + " this run  (floors 10, rooms " + 3 * ordinary + ", elite rooms 15),  " + expected + " in all", text);
            WeaponNextBuy next = WeaponRack.NextBuy(_levels.BetweenRunsState, _levels.MetaValues);
            StringAssert.Contains("Weapon  " + next.Words(), text);
            if (expected >= 30) StringAssert.Contains("Weapon  enough for Laser Sight (30)", text);
            Assert.That(_hud.EndScreenLines.Last(), Does.StartWith("Run seed"), "the seed stays last");

            // The screen: buying the Laser Sight fits it at once.
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True);
            yield return null;
            int part = WeaponRack.InSlotOrder(_levels.MetaValues).FindIndex(p => p.Id == WeaponAttachments.LaserSight);
            Assert.That(_hud.PickWeaponLine(part), Is.True, "a click on its row buys it");
            Assert.That(WeaponRack.IsFitted(_levels.BetweenRunsState, WeaponAttachments.LaserSight), Is.True, "and fits it");
            Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(expected - 30));
            StringAssert.Contains("Bought Laser Sight: fitted.", _levels.BetweenRunsNote);
            yield return null;
            StringAssert.Contains("Laser Sight   FITTED", _hud.WeaponText);
            MetaState saved = MetaSaveFile.Load(SavePath, new MetaValues(), out _, out _);
            Assert.That(saved.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.LaserSight }), "the save is written");

            // The run from the screen: in floor 1's first room, the beams show from her muzzles (work order 102: no longer a line
            // that ends on the dummy her aim passes, nor a square on it).
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Weapon.Parts, Is.EqualTo(new[] { "Laser Sight" }));
            Assert.That(_player.BasicShotRange, Is.EqualTo(12f).Within(1e-5f));
            Vector3 way = ClearWay(6f);
            Put(_a.transform, _player.Position + way * 4f);
            _player.AimAt(_player.Position + way * 8f);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            LaserSight laser = _player.Laser;
            Assert.That(laser.Shown, Is.True, "the beams show");
            for (int b = 0; b < LaserSight.Count; b++)
            {
                Transform muzzle = b == LaserSight.Right ? _player.RightMuzzle : _player.LeftMuzzle;
                Assert.That(laser.ShownOf(b), Is.True, "from each pistol");
                Assert.That(Vector3.Distance(laser.FromOf(b), muzzle.position), Is.LessThan(0.05f), "at its muzzle");
                Assert.That(laser.DotShownOf(b), Is.False, "no dot or square");
                Assert.That(Vector3.Distance(Flat(laser.EndOf(b)), Flat(_a.transform.position)), Is.GreaterThan(0.3f), "never ending on the dummy's middle");
                Assert.That(laser.LengthOf(b), Is.LessThanOrEqualTo(laser.Values.Length + 1e-4f));
            }

            // Cover between: the shot still ends at the cover (the beams stop at what is in their own way: LaserBeamPlayTests).
            Vector3 pillarAt = _player.Position + way * 2f;
            Pillar(pillarAt);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            float stop = Cover.ClearDistance(_player.GroundPosition, new GroundPoint(_player.Position.x + way.x * 12f, _player.Position.z + way.z * 12f));
            Assert.That(stop, Is.LessThan(2f));
            int hits = _a.HitsTaken;
            Vector3 shotFrom = _player.Position;
            _player.Shoot(_player.Position + way * 8f);
            yield return Until(() => _player.ShotsStoppedByCover > 0, 2f);
            Assert.That(Vector3.Distance(Flat(_player.LastCoverStop), Flat(shotFrom)), Is.EqualTo(stop).Within(0.02f), "the shot ends at the cover");
            Assert.That(_a.HitsTaken, Is.EqualTo(hits));

            // Work order 102: a screen that holds the game leaves them as they are (the world stands still under it), and the
            // dash shows them ("hele tiden").
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_player.Laser.Shown, Is.True, "under the pause menu too");
            _hud.Escape();
            yield return null;
            Assert.That(_player.Laser.Shown, Is.True);
            yield return UntilFree();
            Assert.That(_player.DashToward(_player.Position - way * 3f), Is.EqualTo(IntentResult.Started));
            yield return null;
            Assert.That(_player.IsDashing, Is.True);
            Assert.That(_player.Laser.Shown, Is.True, "while she dashes too");
        }

        [UnityTest]
        public IEnumerator TheLaserSight_ReachesATargetAt11_5m_WhereTheStockGunMisses_AndMissesOneTheStockLineTouchesOnlyAtItsEdge()
        {
            ClearTheRange();
            float r = _a.Radius;
            Vector3 a = Flat(_a.transform.position);
            var hit = new bool[1];

            // Its near edge 11.5 m away, straight ahead.
            Vector3 far = a + Vector3.forward * (11.5f + r);
            Fitted();
            yield return ShootFrom(far, a, hit);
            Assert.That(hit[0], Is.False, "the stock gun: out of reach (10 m and its half width)");
            Fitted(WeaponAttachments.LaserSight);
            yield return ShootFrom(far, a, hit);
            Assert.That(hit[0], Is.True, "the Laser Sight: 12 m");

            // Off the line by the stock line's half width less 0.07: inside its outer 0.15 m.
            float off = 0.5f + r - 0.07f;
            Vector3 from = a + Vector3.forward * 6f + Vector3.right * off;
            Vector3 cursor = a + Vector3.right * off;
            Fitted();
            yield return ShootFrom(from, cursor, hit);
            Assert.That(hit[0], Is.True, "the stock line touches it");
            Fitted(WeaponAttachments.LaserSight);
            yield return ShootFrom(from, cursor, hit);
            Assert.That(hit[0], Is.False, "the Laser Sight's narrower line misses it");
            _player.Modifiers = null;
        }

        // ---------------- Each part, felt only when fitted ----------------

        [UnityTest]
        public IEnumerator GhostRing_HitsATarget07mOffTheLine_AndMissesOneWhoseEdgeIsPast9m()
        {
            ClearTheRange();
            float r = _a.Radius;
            Vector3 a = Flat(_a.transform.position);
            var hit = new bool[1];
            float off = 0.7f + r; // its edge 0.7 m off the line's middle
            Vector3 from = a + Vector3.forward * 5f + Vector3.right * off;
            Vector3 cursor = a + Vector3.right * off;
            Fitted();
            yield return ShootFrom(from, cursor, hit);
            Assert.That(hit[0], Is.False, "the stock line (0.5 m to a side) misses it");
            Fitted(WeaponAttachments.GhostRing);
            yield return ShootFrom(from, cursor, hit);
            Assert.That(hit[0], Is.True, "the Ghost Ring's (0.8 m) hits it");

            Vector3 far = a + Vector3.forward * (9.1f + r);
            Fitted();
            yield return ShootFrom(far, a, hit);
            Assert.That(hit[0], Is.True, "the stock gun reaches it");
            Fitted(WeaponAttachments.GhostRing);
            yield return ShootFrom(far, a, hit);
            Assert.That(hit[0], Is.False, "the Ghost Ring's 8 m (and its half width) does not");
            _player.Modifiers = null;
        }

        [UnityTest]
        public IEnumerator BullBarrel_Deals54AtTheDummy_TakesStaggerOffABar_AndShoots135TimesSlower_CountedOverRealTime()
        {
            ClearTheRange();
            _a.Stagger = new StaggerBar(new StaggerValues { Max = 100000f });
            var stagger = new List<float>();
            _a.StaggerTaken += (taken, left) => stagger.Add(taken);
            var dealt = new List<int>();
            _a.Damaged += d => dealt.Add(d);

            Fitted(); // stats off: no crit, no flat damage
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => dealt.Count >= 1, 2f);
            Assert.That(dealt[0], Is.EqualTo(40), "the stock gun");
            Assert.That(stagger, Is.Empty, "no stagger from a basic shot of its own");
            yield return UntilFree();
            yield return new WaitForSeconds(0.3f);

            Fitted(WeaponAttachments.BullBarrel);
            _player.Shoot(_a.transform.position);
            yield return Until(() => dealt.Count >= 2, 2f);
            Assert.That(dealt[1], Is.EqualTo(54), "35 % harder");
            Assert.That(stagger, Is.EqualTo(new[] { 3f }), "3 off the bar, as Quick Shot");
            yield return UntilFree();

            // The interval: shots at nothing (no hit, so no hit stop), the click held, their times taken in real time.
            Vector3 nowhere = NearA + Vector3.back * 8f;
            double stock = 0.0, bull = 0.0;
            for (int round = 0; round < 2; round++)
            {
                if (round == 0) Fitted();
                else Fitted(WeaponAttachments.BullBarrel);
                yield return new WaitForSeconds(0.4f);
                int from = _reports.Count;
                const int shots = 9;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (Reports(PlayerReportKind.ShotFired).Count() - _reports.Take(from).Count(x => x.Kind == PlayerReportKind.ShotFired) < shots && Time.realtimeSinceStartup < deadline)
                {
                    _player.Shoot(nowhere);
                    yield return null;
                }
                var times = new List<double>();
                for (int i = from; i < _reports.Count; i++)
                    if (_reports[i].Kind == PlayerReportKind.ShotFired) times.Add(_reportRealTimes[i]);
                Assert.That(times.Count, Is.GreaterThanOrEqualTo(shots), "round " + round);
                double interval = (times[shots - 1] - times[0]) / (shots - 1);
                if (round == 0) stock = interval;
                else bull = interval;
                yield return UntilFree();
            }
            FeelValues f = _player.Feel;
            float own = f.BasicShotStartup + f.BasicShotActive + f.BasicShotRecovery;
            Debug.Log("Work order 060: the basic shot's interval counted over real time: stock " + stock.ToString("0.0000") + " s, Bull Barrel " + bull.ToString("0.0000")
                      + " s, " + (bull / stock).ToString("0.000") + " times");
            Assert.That(stock, Is.EqualTo(own).Within(own * 0.05), "the stock gun's 0.225 s");
            Assert.That(bull / stock, Is.EqualTo(1.35).Within(0.05), "1.35 times as long");
            _player.Modifiers = null;
        }

        /// <summary>
        /// Work order 082 (review 6, finding 10): with her stats on, a chest's flat damage goes through the barrel: 40 and 6 flat,
        /// times Bull Barrel's 1.35, is 62 (the flat damage added after the barrel would give 60). The crit is ruled out by
        /// a start chance of 0 in this run's numbers.
        /// </summary>
        [UnityTest]
        public IEnumerator BullBarrel_WithSixFlatDamage_Deals62_TheFlatDamageTakesTheBarrel()
        {
            ClearTheRange();
            var dealt = new List<int>();
            _a.Damaged += d => dealt.Add(d);
            RunModifiers m = Fitted(WeaponAttachments.BullBarrel);
            m.StatsAndTiers = true;
            m.StatValues.StartCritChance = 0f; // no crit
            m.Take(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 6f, Tier = UpgradeTier.Common, Tiered = true });
            Assert.That(m.Stats.FlatFor(null), Is.EqualTo(6), "her six flat damage");
            _player.Teleport(NearA);
            yield return null;
            yield return UntilFree();
            _player.Shoot(_a.transform.position);
            yield return Until(() => dealt.Count >= 1, 2f);
            Assert.That(dealt, Is.Not.Empty, "it hit");
            Assert.That(dealt[0], Is.EqualTo(62), "(40 + 6) times 1.35");
            _player.Modifiers = null;
        }

        [UnityTest]
        public IEnumerator RunnersGrip_ShootingOnTheMove_At075OfRunSpeed_32OnTheMove_40Standing()
        {
            FeelValues feel = _player.Feel;
            var dealt = new List<int>();
            _a.Damaged += d => dealt.Add(d);
            float[] limit = new float[2];
            for (int round = 0; round < 2; round++)
            {
                if (round == 0) Fitted();
                else Fitted(WeaponAttachments.RunnersGrip);
                yield return UntilFree();
                yield return new WaitForSeconds(0.5f);
                _player.Teleport(RunFrom);
                yield return null;
                _player.Steer(Vector3.right);
                yield return Until(() => _player.Position.x >= -1f, 4f);
                int before = dealt.Count;
                _player.Shoot(_a.transform.position);
                Assert.That(_player.IsShootingOnTheMove, Is.True, "on the move, round " + round);
                float slowAt = Time.time + feel.MovingShotSlowdown + 0.01f;
                yield return Until(() => Time.time >= slowAt, 2f);
                limit[round] = _player.SpeedLimit;
                yield return Until(() => dealt.Count > before, 2f);
                Assert.That(dealt.Count, Is.EqualTo(before + 1), "it hit, round " + round);
                Assert.That(dealt.Last(), Is.EqualTo(round == 0 ? 40 : 32), round == 0 ? "the stock gun: all of it" : "the grip: 20 % less on the move");
                _player.Steer(Vector3.zero);
                _player.Stop();
            }
            Assert.That(limit[0], Is.EqualTo(feel.RunSpeed * feel.MovingShotSpeedShare).Within(0.1f), "the stock gun: half run speed");
            Assert.That(limit[1], Is.EqualTo(feel.RunSpeed * 0.75f).Within(0.1f), "the grip: 75 %");

            // Standing, the grip's shot deals all of it.
            yield return UntilFree();
            yield return new WaitForSeconds(0.6f);
            _player.Teleport(NearA);
            yield return null;
            int n = dealt.Count;
            _player.Shoot(_a.transform.position);
            Assert.That(_player.IsShootingOnTheMove, Is.False);
            yield return Until(() => dealt.Count > n, 2f);
            Assert.That(dealt.Last(), Is.EqualTo(40), "standing: 40");
            _player.Modifiers = null;
        }

        /// <summary>
        /// Work order 082 (review 6, finding 14): Runner's Grip's backpedal cap in play, as WasdTests' backpedal round: S held
        /// with the cursor up the screen and the basic shot clicked in rhythm, she backs away at the grip's 75 % of run speed
        /// (about 4.5 m/s), where the stock gun holds her to its 50 % (about 3 m/s).
        /// </summary>
        [UnityTest]
        public IEnumerator RunnersGrip_Backpedalling_WhileShooting_At075OfRunSpeed_TheStockGunAt05()
        {
            ClearTheRange();
            FeelValues feel = _player.Feel;
            PlayerInputDriver driver = _player.GetComponent<PlayerInputDriver>();
            Vector3 up = driver.ScreenToGround(new Vector2(0f, 1f)), down = -up;
            float cycle = feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery;
            var fastest = new float[2];
            for (int round = 0; round < 2; round++)
            {
                if (round == 0) Fitted();
                else Fitted(WeaponAttachments.RunnersGrip);
                yield return UntilFree();
                yield return new WaitForSeconds(0.5f);
                _player.Teleport(new Vector3(-8f, 0f, 8f));
                yield return null;
                _player.Steer(down);
                yield return new WaitForSeconds(0.3f);
                int clicks = 0;
                float start = Time.time, deadline = Time.realtimeSinceStartup + 6f;
                while (Time.time < start + 2f && Time.realtimeSinceStartup < deadline)
                {
                    Vector3 aim = _player.Position + up * 8f;
                    _player.AimAt(aim);
                    if (Time.time >= start + clicks * (cycle + 0.03f)) { _player.Shoot(aim); clicks++; }
                    yield return null;
                    if (Time.time > start + 0.6f) fastest[round] = Mathf.Max(fastest[round], _player.Speed);
                }
                _player.Steer(Vector3.zero);
                _player.Stop();
                Assert.That(clicks, Is.GreaterThanOrEqualTo(6), "shooting in rhythm, round " + round);
            }
            Debug.Log("Work order 082: backpedalling while shooting, fastest " + fastest[0].ToString("0.00") + " m/s stock, " + fastest[1].ToString("0.00") + " m/s with Runner's Grip (run speed " + feel.RunSpeed + ")");
            Assert.That(fastest[0], Is.LessThan(feel.RunSpeed * feel.BackpedalSpeedShare + 0.25f), "the stock gun: the feel's backpedal cap");
            Assert.That(fastest[1], Is.EqualTo(feel.RunSpeed * 0.75f).Within(0.4f), "the grip: 75 % of run speed backwards");
            _player.Modifiers = null;
        }

        // ---------------- The windows ----------------

        /// <summary>Triple Tap's window and clean part for each recast, the cast bar over it, Long Shot's perfect band on the bar, and the dash's invulnerability, in words.</summary>
        IEnumerator Windows(List<string> into)
        {
            var bar = CastBar.Ensure();
            int triple = _player.SlotOf("Triple Tap"), longShot = _player.SlotOf("Long Shot");
            Assert.That(triple, Is.GreaterThanOrEqualTo(0), "Triple Tap");
            Assert.That(longShot, Is.GreaterThanOrEqualTo(0), "Long Shot");
            if (!_player.IsEquipped(triple)) _player.EquipSkill(triple);
            if (!_player.IsEquipped(longShot)) _player.EquipSkill(longShot); // a pool skill: given, as the floor reward gives it
            _player.StartFresh();
            _player.Teleport(NearA);
            yield return null;
            Vector3 at = _a.transform.position;
            Assert.That(_player.PressSkill(triple, at), Is.EqualTo(IntentResult.Started));
            for (int press = 2; press <= 3; press++)
            {
                yield return Until(() => _player.IsRecastWindowOpen || _player.RecastSlot != triple, 3f);
                Assert.That(_player.RecastSlot, Is.EqualTo(triple), "the chain goes on");
                yield return null;
                CastBarView v = bar.View;
                into.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                       "Triple Tap press {0}: window {1:0.0000} s, clean {2:0.0000} to {3:0.0000}, of {4}; bar {5} {6:0.0000} dim {7:0.0000} zone {8:0.0000} to {9:0.0000}",
                                       press, _player.RecastWindowSeconds, _player.RecastCleanFrom, _player.RecastCleanTo, _player.RecastCount,
                                       v.Kind, v.Seconds, v.DimTo, v.ZoneFrom, v.ZoneTo));
                if (press == 3) // only the last press of the chain can be clean: the clean third
                {
                    yield return Until(() => _player.RecastPressWouldBeClean || !_player.IsRecastWindowOpen, 3f);
                    Assert.That(_player.RecastPressWouldBeClean, Is.True, "its clean part comes");
                }
                Assert.That(_player.PressSkill(triple, at), Is.EqualTo(IntentResult.Started));
            }
            yield return UntilFree();
            List<PlayerReport> recasts = Reports(PlayerReportKind.RecastPressed).ToList();
            into.Add("clean presses " + string.Join(",", recasts.Skip(recasts.Count - 2).Select(p => p.Clean)));

            _player.StartFresh();
            yield return null;
            Assert.That(_player.PressSkill(longShot, at), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.SkillPhase == SkillPhase.Channel, 3f);
            yield return null;
            CastBarView c = bar.View;
            into.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Long Shot: bar {0} {1:0.0000} zone {2:0.0000} to {3:0.0000}",
                                   c.Kind, c.Seconds, c.ZoneFrom, c.ZoneTo));
            _player.ReleaseSkill(longShot);
            yield return UntilFree();

            _player.StartFresh();
            yield return null;
            Assert.That(_player.DashToward(_player.Position + Vector3.back * 3f), Is.EqualTo(IntentResult.Started));
            float start = Time.time, end = Time.time;
            bool was = _player.IsInvulnerable;
            yield return Until(() =>
            {
                if (_player.IsInvulnerable) end = Time.time;
                return !_player.IsInvulnerable && !_player.IsDashing;
            }, 3f);
            into.Add("dash invulnerable " + was + " for " + Math.Round(end - start, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s");
            yield return UntilFree();
        }

        [UnityTest]
        public IEnumerator TheWindows_TripleTap_LongShot_TheDash_AndTheCastBar_ReadExactlyAsWithNoParts()
        {
            ClearTheRange();
            var none = new List<string>();
            var fitted = new List<string>();
            Fitted();
            yield return Windows(none);
            yield return new WaitForSeconds(0.3f);
            Fitted(WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip);
            yield return Windows(fitted);
            Debug.Log("Work order 060, the windows with no parts:\n" + string.Join("\n", none));
            Assert.That(fitted, Is.EqualTo(none));
            Assert.That(none.Any(l => l.Contains("clean presses") && l.EndsWith("False,True")), Is.True, "the second press plain, the third clean");
            _player.Modifiers = null;
        }

        // ---------------- The practice room, the pause menu, the recorder, the pilot ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoomsShot_IsStock_AfterARunWithPartsFitted()
        {
            WriteFullSave();
            On();
            Attach();
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Weapon.Parts, Is.EqualTo(new[] { "Laser Sight", "Bull Barrel", "Runner's Grip" }));
            Assert.That(_player.BasicShotRange, Is.EqualTo(12f).Within(1e-5f));
            yield return null;
            Assert.That(_player.Laser.Shown, Is.True, "the line in the run");
            _levels.BackToPracticeRoom();
            yield return null;
            yield return null;

            FeelValues feel = _player.Feel;
            Assert.That(_player.Modifiers, Is.Null);
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False, "cleared when the run ended");
            Assert.That(_player.BasicShotRange, Is.EqualTo(feel.BasicShotRange));
            Assert.That(_player.BasicShotHalfWidth, Is.EqualTo(feel.BasicShotHalfWidth));
            Assert.That(_player.BasicShotInterval, Is.EqualTo(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery).Within(1e-6f));
            Assert.That(_player.Laser.Shown, Is.False, "no line in the practice room");
            Assert.That(feel.BasicShotRange, Is.EqualTo(10f), "the feel settings are never written");
            Assert.That(feel.BasicShotHalfWidth, Is.EqualTo(0.5f));
            Assert.That(feel.MovingShotSpeedShare, Is.EqualTo(0.5f));
            ClearTheRange();
            var dealt = new List<int>();
            _a.Damaged += d => dealt.Add(d);
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => dealt.Count > 0, 2f);
            Assert.That(dealt, Is.EqualTo(new[] { 40 }), "the stock gun's 40");
        }

        [UnityTest]
        public IEnumerator ThePauseMenusLine_TurnsBothOn_IsKeptInPlayerPrefs_AndRebuildsThePauseLists()
        {
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.MenuLines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "last");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  off"));
            int made = _hud.PauseListsMade;
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Attachments, 1), Is.True);
            yield return null;
            Assert.That(_hud.PauseListsMade, Is.GreaterThan(made), "the pause screen's lists made again");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  on"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.BetweenRuns), Is.EqualTo("Between runs (trial):  on"), "one line gives the whole loop");
            Assert.That(PlayerPrefs.GetInt(AttachmentsSwitch.Key, -1), Is.EqualTo(1), "kept in PlayerPrefs");
            Assert.That(PlayerPrefs.GetInt(BetweenRunsSwitch.Key, -1), Is.EqualTo(1));
            Assert.That(_levels.MetaOn, Is.True);
            Assert.That(_levels.AttachmentsOn, Is.True);
            Assert.That(_levels.MetaValues.Attachments, Is.False, "the values are not written");
            Assert.That(_levels.MetaValues.Enabled, Is.False);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True, "on: the screen");
            yield return null;
            Assert.That(_hud.WeaponLines, Is.Not.Empty, "with the weapon");
            _hud.Escape();

            // Off again: the trial stays on.
            _hud.Escape();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Attachments), Is.True);
            Assert.That(PlayerPrefs.GetInt(AttachmentsSwitch.Key, -1), Is.EqualTo(0));
            Assert.That(_levels.AttachmentsOn, Is.False);
            Assert.That(_levels.MetaOn, Is.True);
            // On, then the trial off: the line reads off.
            _hud.ChangeSetting(RunHud.MenuLine.Attachments, -1);
            Assert.That(_levels.AttachmentsOn, Is.True);
            _hud.ChangeSetting(RunHud.MenuLine.BetweenRuns, 1);
            Assert.That(_levels.MetaOn, Is.False);
            Assert.That(PlayerPrefs.GetInt(AttachmentsSwitch.Key, -1), Is.EqualTo(1), "stored on");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  off"), "the trial off: off");
            _hud.Escape();
        }

        [UnityTest]
        public IEnumerator ThePauseMenusStats_NameTheParts_AndMeasureHerStartAsTheyMakeIt()
        {
            WriteFullSave();
            On();
            Attach();
            _levels.RunValues.StatsAndTiers = true;
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            Assert.That(_levels.TakeOffAttachment(WeaponAttachments.RunnersGrip), Is.True);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            List<string> lines = _hud.StatLines();
            Assert.That(lines.Count, Is.EqualTo(19), "her eighteen (eleven of work order 050, five of 065, two of 067), and the weapon's"); // changed deliberately (work order 067): 17 before
            Assert.That(lines.Last(), Is.EqualTo("Loadout  Pistols: Laser Sight, Bull Barrel"));
            Assert.That(lines.Single(l => l.StartsWith("Reach")), Is.EqualTo("Reach  12 m  (12 m)"), "the sight's reach is her start");
            StringAssert.StartsWith("Attack speed  2.8 shots/s  (2.8)", lines.Single(l => l.StartsWith("Attack speed")), "0.85 over 0.304 s: the barrel's rate");
            Assert.That(_player.BasicShotInterval, Is.EqualTo(0.30375f / 0.85f).Within(1e-5f));
            _hud.Escape();
            _levels.BackToPracticeRoom();
            yield return null;

            // No part fitted: "stock".
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            _levels.TakeOffAttachment(WeaponAttachments.LaserSight);
            _levels.TakeOffAttachment(WeaponAttachments.BullBarrel);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.StatLines().Last(), Is.EqualTo("Loadout  Pistols: stock"));
            _hud.Escape();
        }

        [UnityTest]
        public IEnumerator TheRecorder_WritesTheWeapon_OnlyWhenOn()
        {
            WriteFullSave();
            _recordings = Path.Combine(Path.GetTempPath(), "WeaponAttachmentsTests-rec-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_recordings);
            On();
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            _levels.BackToPracticeRoom();
            yield return null;
            Attach();
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            _health.Untouchable = true;
            yield return ClearEveryCombatRoom();
            _levels.BackToPracticeRoom();
            yield return null;
            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            List<SessionEvent> loadouts = events.Where(e => e.Type == SessionEventTypes.MetaLoadout).ToList();
            Assert.That(loadouts.Count, Is.EqualTo(2));
            Assert.That(loadouts[0].ExtraValue("weapon"), Is.Null, "the attachments off: no field");
            Assert.That(loadouts[1].ExtraValue("weapon"), Is.EqualTo("Laser Sight | Bull Barrel | Runner's Grip"));
            List<SessionEvent> rooms = events.Where(e => e.Type == SessionEventTypes.MetaEarn && e.Name == "room").ToList();
            Assert.That(rooms.Count, Is.GreaterThan(0), "the rooms' Echoes come in as earnings");
            Assert.That(rooms.All(e => e.ExtraValue("amount") == "3"));
            // The interval the recorder writes is her pistols' (work order 055's ShotInterval).
            Assert.That(events.Any(e => e.Type == SessionEventTypes.ShotInterval && e.ExtraValue("seconds") == "0.30375"), Is.True, "the barrel's interval");
            SessionStats stats = SessionSummary.Compute(events);
            string text = SessionSummary.Format(stats);
            Assert.That(stats.Runs[0].MetaWeapon, Is.Null);
            Assert.That(stats.Runs[1].MetaWeapon, Is.EqualTo("Laser Sight, Bull Barrel, Runner's Grip"));
            StringAssert.Contains("between runs     equipped nothing;  weapon Laser Sight, Bull Barrel, Runner's Grip;  earned ", text);
            StringAssert.Contains("room " + 3 * rooms.Count, text, "the rooms' Echoes in the summary, by themselves");
            Assert.That(text.Split('\n').Count(l => l.Contains("weapon ")), Is.EqualTo(1), "only the run that had it");
        }

        [UnityTest]
        public IEnumerator ARunStartedAsThePilotStartsOne_HasTheStockGun_WhateverTheSaveFits()
        {
            MetaState full = WriteFullSave();
            Assert.That(full.AttachmentsFitted, Is.EqualTo(Three));
            FeelValues feel = _player.Feel;
            // As PilotFlight starts a flight: StartRun(seed), the values as tests have them.
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False);
            Assert.That(_player.BasicShotRange, Is.EqualTo(feel.BasicShotRange));
            Assert.That(_player.BasicShotHalfWidth, Is.EqualTo(feel.BasicShotHalfWidth));
            Assert.That(_player.BasicShotBaseInterval, Is.EqualTo(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery).Within(1e-6f));
            _levels.BackToPracticeRoom();
            yield return null;
            // Changed deliberately (work order 117): this said "the attachments are off in code, so a flight flies the stock gun".
            // Both trials are on in code now; a flight's floors are made by a test with no save path, so what lasts is kept in
            // memory from an empty save: a flight still flies the stock gun, whatever any save fits.
            _levels.MetaValues.Enabled = new MetaValues().Enabled;
            _levels.MetaValues.Attachments = new MetaValues().Attachments;
            Assert.That(_levels.AttachmentsOn, Is.True, "both on, as in code");
            GeneratedLevels.BetweenRunsSavePath = null; // as PilotFlight has it
            Assert.That(_levels.SaveFileInUse, Is.Null, "floors a test made without a path read and write no file");
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Weapon.Any, Is.False);
            Assert.That(_player.BasicShotRange, Is.EqualTo(feel.BasicShotRange));
            Assert.That(_player.Laser.Object, Is.Null);
        }

        // ---------------- The beams' cost ----------------

        [UnityTest]
        public IEnumerator TheLine_WithTwentyTargetsAlive_AllocatesNothingPerFrame_AndItsCostIsMeasured()
        {
            ClearTheRange();
            Fitted(WeaponAttachments.LaserSight);
            _player.Teleport(NearA);
            int alive = Damageable.All.Count(d => d.IsAlive);
            for (int i = alive; i < 20; i++)
            {
                GameObject copy = Object.Instantiate(_a.gameObject, new Vector3(-12f + 1.5f * i, 0f, 8f + (i % 3)), Quaternion.identity);
                copy.name = "WeaponTest_Target_" + i;
                _made.Add(copy);
            }
            yield return null;
            Assert.That(Damageable.All.Count(d => d.IsAlive), Is.GreaterThanOrEqualTo(20), "twenty targets alive");
            _player.AimAt(_a.transform.position);
            yield return null;
            yield return null;
            Assert.That(_player.Laser.Shown, Is.True, "the beams show"); // work order 102: they end on no target of the aim

            // The measure is the test framework's own (it counts the GC.Alloc samples on this thread); first, that it sees one.
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            for (int i = 0; i < 10; i++) _player.TickLaser();
            Assert.That(() => { for (int i = 0; i < 300; i++) _player.TickLaser(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "the beams' frame allocates nothing");

            var watch = Stopwatch.StartNew();
            const int ticks = 2000;
            for (int i = 0; i < ticks; i++) _player.TickLaser();
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds / ticks;
            Debug.Log("Work order 102: the Laser Sight's beams cost " + ms.ToString("0.0000") + " ms a frame with " + Damageable.All.Count(d => d.IsAlive) + " targets alive");
            Assert.That(ms, Is.LessThan(0.5), "well under a frame's budget");
            _player.Modifiers = null;
            yield return null;
            Assert.That(_player.Laser.Shown, Is.False, "out of a run: gone");
        }

        static object s_junk;

        /// <summary>
        /// The fifth review of 2026-11-21 (work order 077): the weapon's lines of the screen between runs are made when it opens
        /// and again only when what they say changes (a part bought, fitted or taken off, the currency), not every frame it is
        /// up, as the pause screen's lists (<see cref="RunHud.PauseListsMade"/>).
        /// </summary>
        [UnityTest]
        public IEnumerator TheScreensWeaponLines_AreMadeOnceWhileItIsUp_AndAgainOnlyWhenWhatTheySayChanges()
        {
            Directory.CreateDirectory(_folder);
            Assert.That(MetaSaveFile.Save(SavePath, new MetaState { Currency = 100 }, out string error), Is.True, error);
            On();
            Attach();
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(0), "nothing made before the screen");
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(1), "made when it opens");
            StringAssert.Contains("Laser Sight   price 30 Echoes", _hud.WeaponText);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(1), "30 frames up, nothing changed: made once");

            int part = WeaponRack.InSlotOrder(_levels.MetaValues).FindIndex(p => p.Id == WeaponAttachments.LaserSight);
            Assert.That(_hud.PickWeaponLine(part), Is.True, "bought and fitted");
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(2), "made again when a part is bought");
            StringAssert.Contains("Laser Sight   FITTED", _hud.WeaponText);
            StringAssert.Contains("Bull Barrel   price 40 Echoes", _hud.WeaponText, "70 left");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(2));

            Assert.That(_hud.PickWeaponLine(part), Is.True, "taken off");
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(3), "made again when a part comes off");
            StringAssert.Contains("Laser Sight   owned", _hud.WeaponText);

            _levels.BetweenRunsState.Currency = 10; // the currency changed from elsewhere: seen too
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(4));
            StringAssert.Contains("Bull Barrel   40 Echoes, 30 more", _hud.WeaponText);

            Assert.That(_levels.LeaveBetweenRuns(), Is.True);
            yield return null;
            Assert.That(_hud.WeaponLines, Is.Empty, "gone with the screen");
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(5), "made afresh when it opens again");
            Assert.That(_hud.WeaponLines, Is.Not.Empty);
        }

        /// <summary>Last by name: after every other test of the class, the game's own save is as it was, its bytes and its time.</summary>
        [Test]
        public void Z_TheGamesOwnSave_IsNeverTouchedByTheseTests()
        {
            Assert.That(File.Exists(s_realPath), Is.EqualTo(s_realExisted), s_realPath);
            if (!s_realExisted) return;
            Assert.That(File.ReadAllBytes(s_realPath), Is.EqualTo(s_realBytes), "its bytes");
            Assert.That(File.GetLastWriteTimeUtc(s_realPath), Is.EqualTo(s_realWritten), "its time");
        }
    }
}
