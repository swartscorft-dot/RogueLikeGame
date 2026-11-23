using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Work order 095 (her sheet on Tab), in the ControlRoom scene through a test keyboard and the intents: Tab opens it and Tab
    /// or Esc closes it, the game standing still under it as under the pause menu; it is not offered under a choice or the end
    /// screen; the mouse hovering a tile and the arrow keys pick a skill and make its panel; every word inside its shape at 1920
    /// by 1080 and 1280 by 720, for every skill's panel; nothing allocated in a frame with it open and still; the pause menu's
    /// stats are the sheet's; the key is listed with the others.
    /// </summary>
    public class SheetPlayTests
    {
        const ulong RunSeed = 0x0E117E17UL;
        const int MostFrames = 300;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        string _saves;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _saves = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SheetPlayTests-" + Guid.NewGuid().ToString("N"));
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
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            _hud = RunHud.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            GameSettings.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_saves != null && System.IO.Directory.Exists(_saves)) System.IO.Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

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

        IEnumerator InARun(params Upgrade[] steps)
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            foreach (Upgrade u in steps) _levels.Modifiers.Take(u);
            _health.RefreshMax();
            yield return null;
        }

        // ---------------- Tab, Esc, the pause ----------------

        [UnityTest]
        public IEnumerator Tab_OpensTheSheet_TabOrEscClosesIt_AndTheGameStandsStillUnderIt()
        {
            yield return InARun();
            var fresh = ScriptableObject.CreateInstance<InputBindings>();
            Assert.That(fresh.Sheet, Is.EqualTo(Key.Tab), "Tab by default");
            Object.Destroy(fresh);
            yield return Tap(Key.Tab);
            Assert.That(_hud.SheetUp, Is.True, "Tab opened it");
            Assert.That(_levels.Paused, Is.True, "the pause menu's own path");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the game stands still");
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True, "her input held, as under the pause menu");
            float clock = _levels.Record.Seconds;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(_levels.Record.Seconds, Is.EqualTo(clock), "the run's clock stood");
            yield return Tap(Key.Tab);
            Assert.That(_hud.SheetUp, Is.False, "Tab closed it");
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the game given back");

            yield return Tap(Key.Tab);
            Assert.That(_hud.SheetUp, Is.True);
            yield return Tap(Key.Escape);
            Assert.That(_hud.SheetUp || _levels.Paused, Is.False, "Esc closed it, and did not open the pause menu");

            // From the pause menu, Tab turns it into the sheet.
            yield return Tap(Key.Escape);
            Assume.That(_levels.Paused, Is.True, "the pause menu up");
            yield return Tap(Key.Tab);
            Assert.That(_hud.SheetUp && _levels.Paused, Is.True, "the menu turned into the sheet, the game still paused");
            yield return Tap(Key.Escape);
            Assert.That(_hud.SheetUp || _levels.Paused, Is.False);
        }

        [UnityTest]
        public IEnumerator TheSheet_IsNotOffered_UnderAChoice_OrTheEndScreen()
        {
            yield return InARun();
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assume.That(_levels.Choice, Is.Not.EqualTo(GeneratedLevels.ChoiceKind.None), "a choice up");
            yield return Tap(Key.Tab);
            Assert.That(_hud.SheetUp, Is.False, "not under a choice");
            Assert.That(_hud.OpenSheet(), Is.False);
            while (_levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FloorReward) _levels.PickReward(0);
                else _levels.PickFocusChoice(0);
                yield return null;
            }
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending != GeneratedLevels.EndKind.None);
            Assert.That(_hud.OpenSheet(), Is.False, "not under the end screen");
        }

        [UnityTest]
        public IEnumerator ANewRun_ClosesTheSheet()
        {
            yield return InARun();
            Assert.That(_hud.OpenSheet(), Is.True);
            _levels.StartRun(RunSeed + 1);
            yield return null;
            yield return null;
            Assert.That(_hud.SheetUp, Is.False, "the pause it stood on ended");
        }

        // ---------------- Picking a skill ----------------

        [UnityTest]
        public IEnumerator TheArrowKeys_AndTheMouse_PickASkill_AndItsPanelIsMadeOnlyWhenItChanges()
        {
            yield return InARun(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 6f });
            yield return Tap(Key.Tab);
            Assume.That(_hud.SheetUp, Is.True);
            Assert.That(_hud.SheetTileNames[0], Is.EqualTo(HerSheet.BasicShotName), "her basic shot first");
            Assert.That(_hud.SheetTileNames.Count, Is.EqualTo(1 + _player.Skills.Where((s, i) => _player.IsEquipped(i)).Count()), "a tile for her basic shot and each skill she has");
            Assert.That(_hud.SheetPanel, Is.Null, "no panel until one is picked");
            int panels = _hud.SheetPanelsMade;
            yield return Tap(Key.RightArrow);
            Assert.That(_hud.SheetSelected, Is.EqualTo(0));
            Assert.That(_hud.SheetPanel.Name, Is.EqualTo(HerSheet.BasicShotName));
            Assert.That(_hud.SheetPanel.ValueOf("your damage"), Is.EqualTo("takes 1 × your +6 damage: +6"));
            yield return Tap(Key.RightArrow);
            Assert.That(_hud.SheetSelected, Is.EqualTo(1));
            Assert.That(_hud.SheetPanel.Name, Is.EqualTo(_hud.SheetTileNames[1]));
            yield return Tap(Key.LeftArrow);
            yield return Tap(Key.LeftArrow);
            Assert.That(_hud.SheetSelected, Is.EqualTo(_hud.SheetTileNames.Count - 1), "round the row's end");
            Assert.That(_hud.SheetPanelsMade, Is.EqualTo(panels + 4), "a panel each time the skill changed");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.SheetPanelsMade, Is.EqualTo(panels + 4), "and none in a frame that changed nothing");

            // The mouse: the point the OnGUI tests (TileAtRaw under the sheet's matrix, as the pause menu's rows test theirs).
            // Round 2: a move sent through an IMGUI container by hand never reached a tile at any scale tried (the engine puts the
            // mouse elsewhere in such a container), so the test asks the same hit test the drawing asks, and that a still mouse
            // drawn again changes nothing.
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                for (int i = 0; i < _hud.SheetTileNames.Count; i++)
                    Assert.That(_hud.SheetTileAt(_hud.SheetTileRect(i).center), Is.EqualTo(i), size + ": the point over tile " + i);
                Assert.That(_hud.SheetTileAt(new Vector2(5f, 5f)), Is.EqualTo(-1), size + ": no tile at the view's corner");
            }
            Assert.That(_hud.HoverSheetSkill(_hud.SheetTileAt(_hud.SheetTileRect(2).center)), Is.True);
            Assert.That(_hud.SheetSelected, Is.EqualTo(2));
            if (!SheetImgui.Available) Assert.Inconclusive("the engine's IMGUI entry points moved");
            int made = _hud.SheetPanelsMade;
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            for (int i = 0; i < 3; i++) SheetImgui.Call(_hud, EventType.Repaint, new Vector2(-1000f, -1000f));
            Assert.That((_hud.SheetSelected, _hud.SheetPanelsMade), Is.EqualTo((2, made)), "a still mouse off the row, drawn again, changes nothing");
        }

        // ---------------- On screen ----------------

        [UnityTest]
        public IEnumerator EveryWord_IsInsideItsShape_AtBothSizes_ForEverySkillsPanel()
        {
            yield return InARun(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 9f }, new Upgrade { Kind = UpgradeKind.AttackSpeed, Amount = 0.6f },
                                new Upgrade { Kind = UpgradeKind.Blast, Amount = 0.4f }, new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.05f, Skill = "Scatter Blast" });
            Assert.That(_hud.OpenSheet(), Is.True);
            yield return null;
            Assert.That(_hud.SheetStats.Any(s => s.AtCap), Is.True, "a stat at its cap");
            for (int pick = -1; pick < _hud.SheetTileNames.Count; pick++)
            {
                _hud.HoverSheetSkill(pick);
                foreach (Vector2Int size in Sizes)
                {
                    InterfaceStyle.ScreenForPictures = size;
                    string at = size.x + " by " + size.y + ", " + (pick < 0 ? "no skill" : _hud.SheetTileNames[pick]);
                    float viewWidth = size.x / InterfaceStyle.Scale;
                    Rect whole = _hud.SheetWhole;
                    Assert.That(whole.xMin >= 0f && whole.yMin >= 0f && whole.xMax <= viewWidth && whole.yMax <= InterfaceStyle.V.LayoutHeight, Is.True, at + ": on screen, " + whole);
                    Assert.That(_hud.SheetTextOutside(), Is.Empty, at + ": every word inside its shape");
                    Assert.That(whole.width, Is.LessThan(viewWidth * 0.9f), at + ": compact panels, not a slab");
                    if (pick >= 0)
                    {
                        Rect panel = _hud.SheetPanelRect, tile = _hud.SheetTileRect(pick);
                        Assert.That(panel.yMin, Is.GreaterThan(tile.yMax), at + ": the panel under its tile");
                        Assert.That(panel.Overlaps(_hud.SheetStatsPanelRect), Is.False, at + ": the panel beside the stats, not over them");
                    }
                }
            }
            Debug.Log("SHEET fit " + _hud.SheetFit + "; stats:\n" + string.Join("\n", _hud.SheetStats));
        }

        [UnityTest]
        public IEnumerator WithTheSheetOpen_AndTheMouseStill_AFrameAllocatesNothing()
        {
            yield return InARun(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.05f });
            Assert.That(_hud.OpenSheet(), Is.True);
            _hud.HoverSheetSkill(1);
            yield return null;
            int lists = _hud.SheetListsMade, panels = _hud.SheetPanelsMade;
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                _hud.Tick();
                float k = _hud.SheetFit; // laid out for this size once
                Assert.That(() => { for (int i = 0; i < 300; i++) { _hud.Tick(); k = _hud.SheetFit; } }, NotAllocating, size + ": nothing allocated in a frame with the sheet open");
            }
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That((_hud.SheetListsMade, _hud.SheetPanelsMade), Is.EqualTo((lists, panels)), "no frame made its lists again");
        }

        // ---------------- The pause menu, the keys ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusStats_AreTheSheets_AndTheKeyIsListed()
        {
            yield return InARun(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.05f });
            Assert.That(_hud.Escape(), Is.True, "paused");
            yield return null;
            var pause = _hud.PauseStats.Select(s => s.Name + "=" + s.Value + "/" + s.Change).ToList();
            Assert.That(_hud.PauseStats.Count, Is.GreaterThan(20), "every stat that matters");
            Assert.That(_hud.PauseKeyCaps.Any(k => k.Key == "Tab" && k.Words == "Skills and all stats"), Is.True, "the sheet's key with the others: " + string.Join(", ", _hud.PauseKeyCaps.Select(k => k.Key + " " + k.Words)));
            Assert.That(_hud.PauseKeyCaps.Any(k => k.Key == "M" && k.Words.StartsWith("Map")), Is.True, "the map held on M now");
            Assert.That(_hud.OpenSheet(), Is.True, "from the pause menu");
            yield return null;
            Assert.That(_hud.SheetStats.Select(s => s.Name + "=" + s.Value + "/" + s.Change), Is.EqualTo(pause), "the same lines");
            foreach (Vector2Int size in Sizes)
            {
                _hud.CloseSheet();
                Assert.That(_hud.Escape(), Is.True);
                yield return null;
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_hud.PauseTextOutside(), Is.Empty, size + ": the pause menu's words inside their shapes, with the sheet's stats");
                Rect whole = _hud.PauseWhole;
                Assert.That(whole.yMax, Is.LessThanOrEqualTo(InterfaceStyle.V.LayoutHeight), size + ": on screen");
                Debug.Log("SHEET the pause menu at " + size + ": fit " + _hud.PauseFit);
            }
        }

        /// <summary>One OnGUI of the HUD called by hand for one event with the mouse where given (as <see cref="ImguiByHand"/> does it, work order 068).</summary>
        static class SheetImgui
        {
            const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

            public static bool Available => Find(out _, out _, out _, out _, out _);

            static bool Find(out Type state, out MethodInfo beginContainer, out MethodInfo endContainer, out MethodInfo beginGui, out MethodInfo endGui)
            {
                Type gu = typeof(GUIUtility);
                state = gu.Assembly.GetType("UnityEngine.ObjectGUIState");
                beginContainer = endContainer = beginGui = endGui = null;
                foreach (MethodInfo m in gu.GetMethods(Any))
                {
                    ParameterInfo[] p = m.GetParameters();
                    if (m.Name == "BeginContainer" && p.Length == 1 && p[0].ParameterType == state) beginContainer = m;
                    if (m.Name == "EndContainer" && p.Length == 0) endContainer = m;
                    if (m.Name == "BeginGUI" && p.Length == 3) beginGui = m;
                    if (m.Name == "EndGUI" && p.Length == 1) endGui = m;
                }
                return state != null && beginContainer != null && endContainer != null && beginGui != null && endGui != null;
            }

            public static void Call(MonoBehaviour piece, EventType type, Vector2 mouse)
            {
                if (!Find(out Type state, out MethodInfo beginContainer, out MethodInfo endContainer, out MethodInfo beginGui, out MethodInfo endGui)) return;
                MethodInfo onGui = piece.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                beginContainer.Invoke(null, new[] { Activator.CreateInstance(state, true) });
                beginGui.Invoke(null, Args(beginGui, 0, 0, 1));
                Event saved = Event.current;
                Event.current = new Event { type = type, mousePosition = mouse };
                try { onGui.Invoke(piece, null); }
                finally
                {
                    try { endGui.Invoke(null, Args(endGui, 1)); } catch (Exception) { }
                    endContainer.Invoke(null, null);
                    Event.current = saved;
                }
            }

            static object[] Args(MethodInfo m, params int[] values)
            {
                ParameterInfo[] p = m.GetParameters();
                var a = new object[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    Type t = p[i].ParameterType;
                    int value = i < values.Length ? values[i] : 0;
                    if (t == typeof(int)) a[i] = value;
                    else if (t == typeof(bool)) a[i] = value != 0;
                    else if (t.IsEnum) a[i] = Enum.ToObject(t, value);
                    else a[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                return a;
            }
        }
    }
}
