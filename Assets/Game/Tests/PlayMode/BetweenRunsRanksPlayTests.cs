using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 099 (the ranks bought between runs), in the ControlRoom scene through the intents and a test keyboard: a run
    /// started with ranks in the save has the raised health and damage, and her sheet shows them; with the trial off a run is
    /// field for field the same whatever the save holds, and the save is never read or written; the screen's ranks measure
    /// inside their shapes at 1920 by 1080 and 1280 by 720, in the concept's look and this morning's, with and without the
    /// weapon's panel; a rank is bought by a click and by the keys, one press each, the last rank takes none; nothing is
    /// allocated in a frame with the screen up and nothing changing; the recorder notes each rank bought.
    ///
    /// The save is written only under a temporary folder of each test (<see cref="GeneratedLevels.BetweenRunsSavePath"/>), and
    /// the game's own save file is checked untouched at the end. Nobody has seen the screen: these are numbers.
    /// </summary>
    public class BetweenRunsRanksPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        const int MostFrames = 300;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };

        static string s_realPath;
        static bool s_realExisted;
        static long s_realLength;
        static DateTime s_realWritten;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        string _folder;
        string _recordings;
        PlaySessionRecorder _recorder;
        int? _switchBefore;

        string SavePath => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [OneTimeSetUp]
        public void RememberTheRealSave()
        {
            s_realPath = GeneratedLevels.DefaultBetweenRunsSavePath;
            s_realExisted = File.Exists(s_realPath);
            s_realLength = s_realExisted ? new FileInfo(s_realPath).Length : -1;
            s_realWritten = s_realExisted ? File.GetLastWriteTimeUtc(s_realPath) : DateTime.MinValue;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _switchBefore = PlayerPrefs.HasKey(BetweenRunsSwitch.Key) ? PlayerPrefs.GetInt(BetweenRunsSwitch.Key) : (int?)null;
            BetweenRunsSwitch.Unload();
            _folder = Path.Combine(Path.GetTempPath(), "BetweenRunsRanksPlayTests-" + Guid.NewGuid().ToString("N"));
            GeneratedLevels.BetweenRunsSavePath = SavePath; // never the game's own save
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            yield return null;
            Assert.That(GeneratedLevels.BetweenRunsSavePathInUse, Is.EqualTo(SavePath), "never the game's own save");
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_recordings != null && Directory.Exists(_recordings)) Directory.Delete(_recordings, true);
            _recordings = null;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            InterfaceStyle.Shared.Menus.Concept = true;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            if (_player != null) _player.Modifiers = null;
            GameSettings.Unload();
            BetweenRunsSwitch.Unload();
            PlayerPrefs.DeleteKey(BetweenRunsSwitch.Key);
            if (_switchBefore.HasValue) PlayerPrefs.SetInt(BetweenRunsSwitch.Key, _switchBefore.Value);
            PlayerPrefs.Save();
            GeneratedLevels.BetweenRunsSavePath = null;
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

        IEnumerator Press(params Key[] down)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));
            yield return Frames(() => down.All(k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed));
            yield return null;
        }

        IEnumerator Tap(Key key)
        {
            yield return Press(key);
            yield return Press();
        }

        /// <summary>A save of this many ranks of each (health, damage, crit chance, room heal) and this currency, written where this test reads.</summary>
        MetaState WriteSave(int currency, int health, int damage, int crit, int heal, Action<MetaState> more = null)
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 100000 };
            for (int k = 0; k < health; k++) MetaRanks.Buy(s, v, MetaRanks.Health);
            for (int k = 0; k < damage; k++) MetaRanks.Buy(s, v, MetaRanks.Damage);
            for (int k = 0; k < crit; k++) MetaRanks.Buy(s, v, MetaRanks.CritChance);
            for (int k = 0; k < heal; k++) MetaRanks.Buy(s, v, MetaRanks.RoomHeal);
            s.Currency = currency;
            more?.Invoke(s);
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            return s;
        }

        static string Fields(object o)
        {
            var parts = new List<string>();
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object x = f.GetValue(o);
                parts.Add(f.Name + "=" + (x is IEnumerable list && !(x is string) ? string.Join("|", list.Cast<object>()) : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)));
            }
            return string.Join(", ", parts);
        }

        static string Stat(IEnumerable<SheetStat> lines, string name) => lines.First(s => s.Name == name).Value;

        // ---------------- In a run ----------------

        [UnityTest]
        public IEnumerator ARunStartedWithRanksInTheSave_HasTheRaisedHealthAndDamage_AndHerSheetShowsThem()
        {
            WriteSave(0, 3, 2, 1, 2);
            _levels.MetaValues.Enabled = true;
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True, "the screen comes up");
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null, "the run starts");
            yield return null;
            yield return null;
            int own = _health.MaxHealth;
            Assert.That(_levels.Modifiers.Meta.Ranks, Is.EqualTo(new[] { "Health 3", "Damage 2", "Crit chance 1", "Room heal 2" }));
            Assert.That(_health.Max, Is.EqualTo((int)Math.Round(own * 1.3, MidpointRounding.AwayFromZero)), "+30 % of her own");
            Assert.That(_health.Current, Is.EqualTo(_health.Max), "full at the start");
            Assert.That(_levels.Modifiers.DamageShare, Is.EqualTo(1.12f).Within(1e-5f), "+12 % damage");
            Assert.That(_levels.Modifiers.ScaleDamage(100), Is.EqualTo(112));
            Assert.That(_levels.Modifiers.Stats.CritChance, Is.EqualTo(_levels.RunValues.Stats.StartCritChance + 0.03f).Within(1e-5f), "3 points of crit chance");
            Assert.That(_levels.Modifiers.Meta.RankRoomHeal, Is.EqualTo(0.04f).Within(1e-6f), "4 points of room heal");

            // Her sheet (Tab): the raised values, as part of her start.
            Assert.That(_hud.OpenSheet(), Is.True);
            yield return null;
            IReadOnlyList<SheetStat> sheet = _hud.SheetStats;
            Assert.That(Stat(sheet, "Health"), Is.EqualTo(_health.Max + " / " + _health.Max));
            Assert.That(Stat(sheet, "Damage dealt"), Is.EqualTo("+12 %"));
            Assert.That(Stat(sheet, "Crit chance"), Is.EqualTo("8 %"));
            Assert.That(Stat(sheet, "Room heal"), Is.EqualTo(FloorHeals.Amount(_health.Max, 0.12f).ToString()), "12 % of her maximum");
            Debug.Log("RANKS her sheet with health 3, damage 2, crit 1, room heal 2: " + string.Join("; ", sheet.Select(s => s.Name + " " + s.Value)));
            _hud.CloseSheet();
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_health.Max, Is.EqualTo(own), "her own maximum again in the practice room");
        }

        [UnityTest]
        public IEnumerator TheTrialOff_ARunIsFieldForFieldTheSame_WhateverTheSaveHolds_AndTheSaveIsNeverReadOrWritten()
        {
            Assert.That(_levels.MetaOn, Is.False, "off");
            // Run A: no save at all.
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            yield return null;
            string metaA = Fields(_levels.Modifiers.Meta), weaponA = _levels.Modifiers.Weapon.Any.ToString();
            int maxA = _health.Max;
            float damageA = _levels.Modifiers.DamageShare, critA = _levels.Modifiers.Stats.CritChance;
            Assert.That(_hud.OpenSheet(), Is.True);
            yield return null;
            string sheetA = string.Join("\n", _hud.SheetStats.Select(s => s.Name + "=" + s.Value + "/" + s.Change));
            _hud.CloseSheet();
            _levels.BackToPracticeRoom();
            yield return null;

            // Run B: the same seed with a save holding every rank, pieces equipped and parts fitted.
            WriteSave(5000, 10, 10, 8, 5, s =>
            {
                s.Feats.AddRange(MetaFeats.All.Select(MetaFeats.Id));
                s.Owned.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand });
                s.Equipped.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand });
                s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
                s.AttachmentsFitted.Add(WeaponAttachments.BullBarrel);
            });
            byte[] bytes = File.ReadAllBytes(SavePath);
            DateTime written = File.GetLastWriteTimeUtc(SavePath);
            Assert.That(_levels.StartNewRun(), Is.Not.Null, "F5: a run at once, no screen");
            Assert.That(_levels.BetweenRunsUp, Is.False);
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            yield return null;
            Assert.That(Fields(_levels.Modifiers.Meta), Is.EqualTo(metaA), "the effects, field for field");
            Assert.That(Fields(_levels.Modifiers.Meta), Is.EqualTo(Fields(new RunMetaEffects())), "a fresh one's");
            Assert.That(_levels.Modifiers.Weapon.Any.ToString(), Is.EqualTo(weaponA));
            Assert.That(_health.Max, Is.EqualTo(maxA));
            Assert.That(_health.Max, Is.EqualTo(_health.MaxHealth), "her own");
            Assert.That(_levels.Modifiers.DamageShare, Is.EqualTo(damageA));
            Assert.That(_levels.Modifiers.Stats.CritChance, Is.EqualTo(critA));
            Assert.That(_hud.OpenSheet(), Is.True);
            yield return null;
            Assert.That(string.Join("\n", _hud.SheetStats.Select(s => s.Name + "=" + s.Value + "/" + s.Change)), Is.EqualTo(sheetA), "her sheet, line for line");
            _hud.CloseSheet();
            Assert.That(_hud.BetweenRunsRankLines, Is.Empty);
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Missing), "the save was never read");
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(bytes), "nor written");
            Assert.That(File.GetLastWriteTimeUtc(SavePath), Is.EqualTo(written));
            Assert.That(_hud.PickRankLine(0), Is.False, "no screen, no rank");
        }

        // ---------------- The screen ----------------

        [UnityTest]
        public IEnumerator TheScreensRanks_MeasureInsideTheirShapes_AtBothSizes_InBothLooks_WithAndWithoutTheWeapon()
        {
            WriteSave(60, 2, 0, 1, 5); // health: 80 with 60 (short); damage: 40 (can pay); crit 55 (short of nothing); room heal at its last
            _levels.MetaValues.Enabled = true;
            foreach (bool weapon in new[] { false, true })
            {
                _levels.MetaValues.Attachments = weapon;
                Assert.That(_levels.OpenBetweenRuns(), Is.True, "the screen up");
                yield return null;
                yield return null;
                Assert.That(_hud.BetweenRunsRankLines.Count, Is.EqualTo(4), "a line a stat");
                StringAssert.StartsWith("Health   rank 2 of 10   80 Echoes, 20 more\n", _hud.BetweenRunsRankLines[0]);
                StringAssert.StartsWith("Damage   rank 0 of 10   price 40 Echoes\n", _hud.BetweenRunsRankLines[1]);
                StringAssert.StartsWith("Room heal   rank 5 of 5   last rank\n", _hud.BetweenRunsRankLines[3]);
                Assert.That(_hud.RankRowFirst, Is.EqualTo(1 + _levels.MetaValues.Pieces.Length + (weapon ? WeaponRack.InSlotOrder(_levels.MetaValues).Count : 0)),
                            "the ranks' rows after the rack's and the weapon's");

                // The concept's look.
                InterfaceStyle.Shared.Menus.Concept = true;
                int first = _hud.BetweenRankCardFirst;
                Assert.That(_hud.BetweenCardNames.Skip(first), Is.EqualTo(new[] { "Health", "Damage", "Crit chance", "Room heal" }));
                Assert.That(_hud.BetweenCardStates.Skip(first), Is.EqualTo(new[] { "80 Echoes", "40 Echoes", "55 Echoes", "last rank" }));
                foreach (Vector2Int size in Sizes)
                {
                    InterfaceStyle.ScreenForPictures = size;
                    string at = size.x + " by " + size.y + (weapon ? ", with the weapon" : "");
                    float viewWidth = size.x / InterfaceStyle.Scale;
                    Assert.That(_hud.BetweenTextOutside(), Is.Empty, at + ": every word inside its shape");
                    for (int i = 0; i < _hud.BetweenCardNames.Count; i++)
                    {
                        Rect r = _hud.BetweenCardRect(i);
                        Assert.That(r.xMin >= 0f && r.yMin >= 0f && r.xMax <= viewWidth && r.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": card " + i + " on screen, " + r);
                        for (int j = 0; j < i; j++) Assert.That(r.Overlaps(_hud.BetweenCardRect(j)), Is.False, at + ": cards " + j + " and " + i + " apart");
                    }
                    Debug.Log("RANKS concept at " + at + ": fit " + _hud.BetweenFit + ", the ranks' cards " + string.Join(", ", Enumerable.Range(first, 4).Select(i => _hud.BetweenCardRect(i).ToString())));
                }

                // This morning's look, drawn by hand (a batch run sends no OnGUI).
                if (ImguiByHand.Available)
                {
                    InterfaceStyle.Shared.Menus.Concept = false;
                    foreach (Vector2Int size in Sizes)
                    {
                        InterfaceStyle.ScreenForPictures = size;
                        string at = size.x + " by " + size.y + (weapon ? ", with the weapon" : "") + ", this morning's look";
                        List<ImguiByHand.Drawn> order = ImguiByHand.Draw(null);
                        ImguiByHand.Drawn hud = order.First(d => d.Piece == _hud);
                        Assert.That(hud.Error, Is.Null, at + ": drawn without a fault");
                        float viewWidth = size.x / (size.y / SkillBar.LayoutHeight);
                        Rect rack = _hud.BetweenFlatRack, ranks = _hud.BetweenFlatRanks, gun = _hud.BetweenFlatWeapon;
                        foreach (Rect p in new[] { rack, ranks }.Concat(weapon ? new[] { gun } : new Rect[0]))
                            Assert.That(p.width > 0f && p.xMin >= 0f && p.yMin >= 0f && p.xMax <= viewWidth && p.yMax <= SkillBar.LayoutHeight, Is.True, at + ": a panel on screen, " + p);
                        Assert.That(ranks.Overlaps(rack), Is.False, at + ": the ranks beside the rack");
                        if (weapon) Assert.That(ranks.Overlaps(gun) || gun.Overlaps(rack), Is.False, at + ": the three apart");
                        Assert.That(rack.width, Is.GreaterThan(ranks.width), at + ": the rack the larger part");
                        Assert.That(_hud.BetweenFlatRanksWordsFit, Is.True, at + ": the ranks' heading and line inside their panel");
                        Debug.Log("RANKS this morning's look at " + at + ": rack " + rack + ", weapon " + gun + ", ranks " + ranks);
                    }
                    InterfaceStyle.Shared.Menus.Concept = true;
                }
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                Assert.That(_levels.LeaveBetweenRuns(), Is.True);
                yield return null;
            }
            if (!ImguiByHand.Available) Assert.Inconclusive("the engine's IMGUI entry points moved: this morning's look was not drawn by hand");
        }

        [UnityTest]
        public IEnumerator ARank_IsBoughtByAClick_AndByTheKeys_OnePressEach_TheLastRankTakesNone_AndTheRecorderNotesIt()
        {
            _recordings = Path.Combine(Path.GetTempPath(), "BetweenRunsRanksPlayTests-rec-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_recordings);
            WriteSave(200, 0, 0, 0, 0);
            _levels.MetaValues.Enabled = true;
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            yield return null;
            MetaState s = _levels.BetweenRunsState;

            // A click on Health's card: the hit test the drawing's buttons use, then their click.
            int first = _hud.BetweenRankCardFirst;
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                for (int i = 0; i < 4; i++)
                    Assert.That(_hud.BetweenCardAt(_hud.BetweenCardRect(first + i).center), Is.EqualTo(first + i), size + ": the point over rank card " + i);
            }
            Assert.That(_hud.ClickBetweenCard(_hud.BetweenCardAt(_hud.BetweenCardRect(first).center)), Is.True, "one click buys");
            Assert.That(s.RankOf(MetaRanks.Health), Is.EqualTo(1));
            Assert.That(s.Currency, Is.EqualTo(160));
            Assert.That(_hud.BetweenRunsSelected, Is.EqualTo(_hud.RankRowFirst), "the arrows' place on it");
            yield return null;
            StringAssert.Contains("Bought Health rank 1 for 40 Echoes.", _hud.BetweenRunsText);
            StringAssert.StartsWith("Health   rank 1 of 10   price 55 Echoes", _hud.BetweenRunsRankLines[0]);

            // The keys: down to Damage, Enter.
            yield return Tap(Key.DownArrow);
            Assert.That(_hud.BetweenRunsSelected, Is.EqualTo(_hud.RankRowFirst + 1), "on Damage");
            yield return Tap(Key.Enter);
            Assert.That(s.RankOf(MetaRanks.Damage), Is.EqualTo(1), "Enter bought it");
            Assert.That(s.Currency, Is.EqualTo(120), "one press, one rank");
            // From the start line, down the whole rack to the first rank, and up again round the end.
            for (int i = 0; i < 60 && _hud.BetweenRunsSelected != 0; i++) yield return Tap(Key.DownArrow);
            Assert.That(_hud.BetweenRunsSelected, Is.Zero, "round to the start line");
            yield return Tap(Key.UpArrow);
            Assert.That(_hud.BetweenRunsSelected, Is.EqualTo(_hud.RankRowFirst + 3), "up from the start: the last rank's line");

            // The last rank takes no press; too little buys nothing.
            s.Ranks[MetaRanks.RoomHeal] = 5;
            yield return null;
            string note = _levels.BetweenRunsNote;
            yield return Tap(Key.Enter);
            Assert.That(s.RankOf(MetaRanks.RoomHeal), Is.EqualTo(5));
            Assert.That(s.Currency, Is.EqualTo(120), "nothing taken at the last rank");
            Assert.That(_levels.BetweenRunsNote, Is.EqualTo(note), "and nothing said");
            Assert.That(_hud.ClickBetweenCard(first + 3), Is.False, "a click neither");
            s.Currency = 10;
            Assert.That(_hud.PickRankLine(2), Is.False);
            Assert.That(s.RankOf(MetaRanks.CritChance), Is.Zero);
            Assert.That(_levels.BetweenRunsNote, Does.StartWith("Not enough Echoes for Crit chance rank 1 (40)"));

            // The save holds them (written as they were bought), and the recorder noted each.
            MetaState saved = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That((saved.RankOf(MetaRanks.Health), saved.RankOf(MetaRanks.Damage), saved.Currency), Is.EqualTo((1, 1, 120)));
            _recorder.StopRecording();
            List<SessionEvent> ranks = _recorder.Events.Where(e => e.Type == SessionEventTypes.MetaRank).ToList();
            Assert.That(ranks.Select(e => e.Name + " " + e.ExtraValue("rank") + " " + e.ExtraValue("price") + " " + e.ExtraValue("left")),
                        Is.EqualTo(new[] { "Health 1 40 160", "Damage 1 40 120" }));
            StringAssert.Contains("Ranks bought between runs   Health rank 1 for 40 (160 left);  Damage rank 1 for 40 (120 left)", SessionSummary.Format(SessionSummary.Compute(_recorder.Events)));
        }

        [UnityTest]
        public IEnumerator WithTheScreenUp_AndNothingChanging_AFrameAllocatesNothing_AndItsLinesAreMadeOncePerChange()
        {
            WriteSave(300, 1, 0, 0, 0);
            _levels.MetaValues.Enabled = true;
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            yield return null;
            int made = _hud.BetweenRunsLinesMade;
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                _hud.Tick();
                float k = _hud.BetweenFit; // laid out for this size once
                Assert.That(() => { for (int i = 0; i < 300; i++) { _hud.Tick(); k = _hud.BetweenFit; } }, NotAllocating, size + ": nothing allocated in a frame with the screen up");
            }
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(made), "not made again while nothing changed");
            Assert.That(_hud.PickRankLine(1), Is.True);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(made + 1), "a rank bought: made once");
            StringAssert.StartsWith("Damage   rank 1 of 10", _hud.BetweenRunsRankLines[1]);
        }

        /// <summary>Last by name: after every other test of the class, the game's own save is as it was.</summary>
        [Test]
        public void Z_TheGamesOwnSave_IsNeverTouchedByTheseTests()
        {
            Assert.That(File.Exists(s_realPath), Is.EqualTo(s_realExisted), s_realPath);
            if (!s_realExisted) return;
            Assert.That(new FileInfo(s_realPath).Length, Is.EqualTo(s_realLength));
            Assert.That(File.GetLastWriteTimeUtc(s_realPath), Is.EqualTo(s_realWritten));
        }
    }
}
