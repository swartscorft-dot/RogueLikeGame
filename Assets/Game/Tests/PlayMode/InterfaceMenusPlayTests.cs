using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 078 (the menus in the concept's style), in the ControlRoom scene through the intents, as work order 068's
    /// <see cref="InterfaceConceptPlayTests"/>: the pause menu and the end screen on screen at 1920 by 1080 and at 1280 by 720,
    /// every word measured inside its shape; the rows' words as the menu's lines say them; the selected row stands out; a menu
    /// drawn over everything of the fight; the lists made when the menu opens and when a setting changes, not every frame;
    /// nothing allocated in a frame with a menu up and nothing changing; one switch gives this morning's menus.
    /// </summary>
    public class InterfaceMenusPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };

        PlayerCharacter _player;
        PlayerHealth _health;
        SkillBar _bar;
        GeneratedLevels _levels;
        RunHud _hud;
        CastBar _cast;
        string _saves;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _saves = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InterfaceMenusPlayTests-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = System.IO.Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // never the game's own save
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _cast = CastBar.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            InterfaceStyle.Shared.Menus.Concept = true;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _cast, _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            GameSettings.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_saves != null && System.IO.Directory.Exists(_saves)) System.IO.Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator PausedInARun()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.Modifiers.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.05f });
            yield return null;
            Assert.That(_hud.Escape(), Is.True, "paused");
            yield return null;
        }

        // ---------------- The pause menu ----------------

        [UnityTest]
        public IEnumerator ThePauseMenu_StandsOnScreen_EveryWordInsideItsShape_AtBothSizes()
        {
            yield return PausedInARun();
            Assert.That(_hud.MenusInConcept, Is.True);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                string at = size.x + " by " + size.y;
                float viewWidth = size.x / InterfaceStyle.Scale;
                Rect whole = _hud.PauseWhole;
                Assert.That(whole.xMin >= 0f && whole.yMin >= 0f && whole.xMax <= viewWidth && whole.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": on screen, " + whole);
                Assert.That(_hud.PauseTextOutside(), Is.Empty, at + ": every word inside its shape");
                PauseLayout l = _hud.PauseLayoutNow;
                Assert.That(l.Side.H, Is.GreaterThan(0f), at + ": her stats and the keys beside the rows");
                Assert.That(l.Rows.Right, Is.LessThan(l.Side.X), at + ": the rows and the panels apart");
                Assert.That(whole.width, Is.LessThan(viewWidth * 0.8f), at + ": compact, not a slab across the screen");
                for (int i = 1; i < _hud.MenuLines.Count; i++)
                    Assert.That(_hud.PauseRowRect(i).yMin, Is.GreaterThan(_hud.PauseRowRect(i - 1).yMax), at + ": row " + i + " under the one before");
            }
        }

        [UnityTest]
        public IEnumerator TheRowsWords_AreTheMenusLines_ReadApart_TheStatsTheirs_AndTheKeysCaps()
        {
            yield return PausedInARun();
            IReadOnlyList<RunHud.MenuLine> lines = _hud.MenuLines;
            Assert.That(_hud.PauseRowWords.Count, Is.EqualTo(lines.Count));
            for (int i = 0; i < lines.Count; i++)
            {
                string text = _hud.MenuText(lines[i]);
                string joined = _hud.PauseRowValues[i].Length > 0 ? _hud.PauseRowWords[i] + ":  " + _hud.PauseRowValues[i]
                                : _hud.PauseRowKeys[i].Length > 0 ? _hud.PauseRowWords[i] + "   (" + _hud.PauseRowKeys[i] + ")" : _hud.PauseRowWords[i];
                Assert.That(joined, Is.EqualTo(text), "row " + i + " says what its line says");
            }
            Assert.That(_hud.PauseRowKeys[0], Is.EqualTo("Esc"), "Resume's key on its cap");
            // Work order 095: the panel draws her sheet's lines (HerSheet.Stats), the sheet's one list.
            Assert.That(_hud.PauseStats.Count, Is.EqualTo(_hud.SheetStats.Count), "every stat line of her sheet drawn");
            Assert.That(_hud.PauseStats.Count, Is.GreaterThan(20), "every stat that matters");
            bool critChanged = false;
            foreach (MenuStat s in _hud.PauseStats) if (s.Name == "Crit chance") critChanged = s.Changed;
            Assert.That(critChanged, Is.True, "the step taken shows as changed");
            Assert.That(_hud.PauseKeyCaps.Count, Is.EqualTo(_hud.KeyLines().Count), "a cap for every key line");
            Assert.That(_hud.PauseKeyCaps[0].Key, Is.EqualTo("WASD"), "the four keys of moving on one cap");
        }

        [UnityTest]
        public IEnumerator TheSelectedRow_StandsOut_AndMovesWithTheArrows()
        {
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            yield return PausedInARun();
            Rect resume = _hud.PauseRowRect(0), next = _hud.PauseRowRect(1);
            Assert.That(resume.x, Is.GreaterThan(next.x), "the selected row (Resume, on opening) moved out to the right");
            Assert.That(_hud.SelectMenuLine(RunHud.MenuLine.CameraKick), Is.True);
            int kick = new List<RunHud.MenuLine>(_hud.MenuLines).IndexOf(RunHud.MenuLine.CameraKick);
            Assert.That(_hud.PauseRowRect(kick).x, Is.GreaterThan(_hud.PauseRowRect(0).x), "now the camera kick's row");
        }

        [UnityTest]
        public IEnumerator TheLists_AreMadeWhenTheMenuOpens_AndWhenASettingChanges_NotEveryFrame_AndAFrameAllocatesNothing()
        {
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            _levels.StartRun(RunSeed);
            yield return null;
            int made = _hud.PauseListsMade, weapon = _hud.WeaponListsMade;
            Assert.That(_hud.Escape(), Is.True, "paused");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.PauseListsMade, Is.EqualTo(made + 1), "made once as it opened");
            int kick = new List<RunHud.MenuLine>(_hud.MenuLines).IndexOf(RunHud.MenuLine.CameraKick);
            string before = _hud.PauseRowValues[kick];
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.CameraKick, 1), Is.True);
            yield return null;
            Assert.That(_hud.PauseListsMade, Is.EqualTo(made + 2), "and again when a setting it shows changed");
            Assert.That(_hud.PauseRowValues[kick], Is.Not.EqualTo(before), "its row says the new value");
            _hud.ChangeSetting(RunHud.MenuLine.CameraKick, 1); // back
            yield return null;
            Assert.That(_hud.WeaponListsMade, Is.EqualTo(weapon), "the weapon's lines untouched (work order 077's count)");
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                _hud.Tick();
                Rect _ = _hud.PauseWhole; // laid out for this size once
                Assert.That(() => { for (int i = 0; i < 300; i++) { _hud.Tick(); _ = _hud.PauseWhole; } }, NotAllocating, size + ": nothing allocated in a frame with the menu up");
            }
            Assert.That(_hud.PauseListsMade, Is.EqualTo(made + 3), "no frame made them again");
        }

        // ---------------- The run's end ----------------

        [UnityTest]
        public IEnumerator TheEndScreen_AfterADeath_SaysDEAD_ShowsThePicks_EveryWordInsideItsShape_AtBothSizes_AndAFrameAllocatesNothing()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 3f);
            bool picked = false;
            yield return Until(() => picked = _levels.PickReward(0), 3f);
            Assume.That(picked, Is.True, "the floor's skill taken");
            yield return null;
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) yield return Until(() => _levels.PickFocusChoice(0), 3f);
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Until(() => _levels.EndScreenShown, 5f);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True, "the end screen up");
            Assert.That(_hud.EndVerdict, Is.EqualTo("DEAD"));
            Assert.That(_hud.EndChips.Count, Is.GreaterThanOrEqualTo(1), "the run's picks as chips");
            Assert.That(_hud.EndChips[0], Is.EqualTo(_levels.Record.SkillNamed(0)), "the skill taken first");
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                string at = size.x + " by " + size.y;
                float viewWidth = size.x / InterfaceStyle.Scale;
                Assert.That(_hud.EndTextOutside(), Is.Empty, at + ": every word inside its shape");
                for (int b = 0; b < 3; b++)
                {
                    Rect r = _hud.EndButtonRect(b);
                    Assert.That(r.xMin >= 0f && r.xMax <= viewWidth && r.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": answer " + b + " on screen, " + r);
                    if (b > 0) Assert.That(r.xMin, Is.GreaterThan(_hud.EndButtonRect(b - 1).xMax), at + ": the answers apart");
                }
            }
            int made = _hud.EndLinesMade;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.EndLinesMade, Is.EqualTo(made), "the end screen's lines are not made again every frame");
            float k = _hud.EndFit;
            Assert.That(() => { for (int i = 0; i < 300; i++) { _hud.Tick(); k = _hud.EndFit; } }, NotAllocating, "nothing allocated in a frame with the end screen up");
            Assert.That(_hud.EndScreenLines[0], Is.EqualTo("You died"), "its lines say what they said");
        }

        // ---------------- Between runs ----------------

        [UnityTest]
        public IEnumerator BetweenRuns_ThePiecesAndTheParts_AreCards_WithTheirStates_EveryWordInsideItsShape_AtBothSizes()
        {
            var s = new MetaState { Currency = 45 };
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            Assert.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
            Assume.That(_levels.OpenBetweenRuns(), Is.True, "the screen between runs up (the trial's switch may be off on this machine)");
            yield return null;
            yield return null;
            int pieces = _levels.MetaValues.Pieces.Length, parts = WeaponRack.InSlotOrder(_levels.MetaValues).Count;
            int ranks = _levels.MetaValues.Ranks.Length; // work order 099: and a card for every stat of the ranks
            Assert.That(_hud.BetweenCardNames.Count, Is.EqualTo(pieces + parts + ranks), "a card for every piece of the rack, every part and every rank");
            int laser = new List<string>(_hud.BetweenCardNames).IndexOf("Laser Sight"), bull = new List<string>(_hud.BetweenCardNames).IndexOf("Bull Barrel");
            Assert.That(_hud.BetweenCardStates[laser], Is.EqualTo("FITTED"));
            Assert.That(_hud.BetweenCardStates[bull], Is.EqualTo("owned"));
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                string at = size.x + " by " + size.y;
                float viewWidth = size.x / InterfaceStyle.Scale;
                Assert.That(_hud.BetweenTextOutside(), Is.Empty, at + ": every word inside its shape");
                for (int i = 0; i < _hud.BetweenCardNames.Count; i++)
                {
                    Rect r = _hud.BetweenCardRect(i);
                    Assert.That(r.xMin >= 0f && r.yMin >= 0f && r.xMax <= viewWidth && r.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": card " + i + " on screen, " + r);
                    for (int j = 0; j < i; j++) Assert.That(r.Overlaps(_hud.BetweenCardRect(j)), Is.False, at + ": cards " + j + " and " + i + " apart");
                }
            }
        }

        // ---------------- Over everything; this morning's look ----------------

        [UnityTest]
        public IEnumerator AMenu_IsDrawnOverEverythingOfTheFight_InTheConceptAndInThisMorningsLook()
        {
            if (!ImguiByHand.Available) Assert.Inconclusive("the engine's IMGUI entry points moved: the order cannot be run by hand");
            yield return PausedInARun();
            foreach (bool concept in new[] { true, false })
            {
                InterfaceStyle.Shared.Menus.Concept = concept;
                Assert.That(_hud.MenusInConcept, Is.EqualTo(concept), "the one switch");
                _bar.Tick();
                _cast.Tick();
                _hud.Tick();
                List<ImguiByHand.Drawn> order = ImguiByHand.Draw(null);
                int hud = order.FindIndex(d => d.Piece == _hud);
                Assert.That(hud, Is.EqualTo(order.Count - 1), "the menu drawn last, over everything: " + ImguiByHand.Describe(order));
                Assert.That(order[hud].Depth, Is.EqualTo(InterfaceDepth.Screens));
                Assert.That(order[hud].Error, Is.Null, "drawn without a fault: " + ImguiByHand.Describe(order));
            }
        }
    }
}
