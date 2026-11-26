using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The upgrades in three tabs (work order 139, <see cref="UpgradeTabsValues.On"/>; Kristian, 2026-11-25, ticked "Tre faner, tre
    /// former"), as part of <see cref="StartMenu"/>: five tabs at the top, START RUN, WEAPON, RANKS, RACK and STATS, the three in
    /// the middle the pages of the one upgrades screen (<see cref="MenuScreen.Upgrades"/> and <see cref="Page"/>). The main menu's
    /// UPGRADES opens WEAPON. Keys: the arrows (W, S) along the page's rows, Enter (Space) presses the row as the cards' screen
    /// did; Tab or E the next page, Q the one before; on WEAPON the left and right arrows (A, D) turn the model while held, R
    /// sets it back; the mouse dragged on the model turns it, the wheel over it comes nearer or goes back. The pages are drawn by
    /// the HUD (RunHudUpgradeTabs.cs) and the model by its stage (<see cref="WeaponStage"/>).
    /// </summary>
    public sealed partial class StartMenu
    {
        /// <summary>The upgrades' page up (WEAPON when the upgrades are not up).</summary>
        public UpgradesPage Page { get; private set; }

        static readonly string[] s_WeaponHint = { "Up", "Down", "choose", "Enter", "buy, fit, take off", "<", ">", "or drag: turn", "R", "set back", "Tab", "next tab", "Esc", "back" };
        static readonly bool[] s_WeaponHintCap = { true, true, false, true, false, true, true, false, true, false, true, false, true, false };
        static readonly string[] s_PageHint = { "Up", "Down", "choose", "Enter", "buy", "Tab", "next tab", "Esc", "back" };
        static readonly bool[] s_PageHintCap = { true, true, false, true, false, true, false, true, false };

        /// <summary>The tabs' words now: five with the switch on, else the three of work order 114.</summary>
        static string[] TabsNow => UpgradeTabsValues.On ? UpgradeTabsFlow.TabWords : TabWords;

        /// <summary>The tab the screen up sits under.</summary>
        int TabHere => UpgradeTabsValues.On ? UpgradeTabsFlow.TabOf(Screen, Page) : StartMenuFlow.TabOf(Screen);

        /// <summary>The upgrades' line of keys for the page up.</summary>
        string[] UpgradesHint => UpgradeTabsValues.On ? (Page == UpgradesPage.Weapon ? s_WeaponHint : s_PageHint) : s_UpgradesHint;
        bool[] UpgradesHintCap => UpgradeTabsValues.On ? (Page == UpgradesPage.Weapon ? s_WeaponHintCap : s_PageHintCap) : s_UpgradesHintCap;

        /// <summary>Is the WEAPON page up now.</summary>
        public bool OnWeaponPage => UpgradeTabsValues.On && Up && Screen == MenuScreen.Upgrades && Page == UpgradesPage.Weapon;

        /// <summary>The stage while the WEAPON page is up, else null (for tests).</summary>
        public WeaponStage Stage => OnWeaponPage ? WeaponStage.Current : null;

        /// <summary>A page of the upgrades opened (its tab clicked, Tab or Q or E): false when the menu is not up or the switch is off.</summary>
        public bool OpenPage(UpgradesPage page)
        {
            if (!Up || !UpgradeTabsValues.On) return false;
            if (Screen != MenuScreen.Upgrades)
            {
                Page = page;
                SetScreen(MenuScreen.Upgrades);
                return true;
            }
            if (page == Page) return true;
            Page = page;
            Note = "";
            Selected = 0;
            _laidOut = false;
            PageOpened();
            return true;
        }

        /// <summary>A tab clicked with the five tabs: START RUN's first step, a page, or STATS.</summary>
        bool OpenTabOfFive(int tab)
        {
            MenuScreen to = UpgradeTabsFlow.TabScreen(tab);
            if (to == MenuScreen.Upgrades) return OpenPage(UpgradeTabsFlow.TabPage(tab));
            if (UpgradeTabsFlow.TabOf(Screen, Page) == tab && Screen != MenuScreen.Main) return true;
            SetScreen(to);
            return true;
        }

        /// <summary>At the end of every change of screen: away from the upgrades the page goes back to WEAPON; on a page, the keys start where the page wants.</summary>
        void TabsScreenSet(MenuScreen next)
        {
            if (!UpgradeTabsValues.On) return;
            if (next != MenuScreen.Upgrades)
            {
                Page = UpgradesPage.Weapon;
                return;
            }
            PageOpened();
        }

        void PageOpened()
        {
            RunHud hud = RunHud.Current;
            if (hud == null) return;
            if (Page == UpgradesPage.Weapon) Selected = hud.TabRestRow(this); // the preview is what is fitted
            _lastCount = ItemCount;
        }

        /// <summary>The pages' own keys (read when a key went down): Tab and E the next page, Q the one before, R sets the model back. True when one was used.</summary>
        bool TabsKeys(Keyboard k)
        {
            if (!UpgradeTabsValues.On || Screen != MenuScreen.Upgrades) return false;
            if (k.tabKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) return OpenPage(UpgradeTabsFlow.Step(Page, 1));
            if (k.qKey.wasPressedThisFrame) return OpenPage(UpgradeTabsFlow.Step(Page, -1));
            if (k.rKey.wasPressedThisFrame && Page == UpgradesPage.Weapon) return ResetModel();
            return false;
        }

        /// <summary>Every frame with the menu up: on WEAPON the stage is kept, the turning keys held are read, the preview follows the cursor. Nothing made in a frame that changes nothing.</summary>
        void TabsFrame(Keyboard keys)
        {
            if (!OnWeaponPage) return;
            RunHud hud = RunHud.Current;
            if (hud == null) return;
            hud.KeepStage(this);
            WeaponStage stage = WeaponStage.Current;
            if (stage == null) return;
            int turn = 0;
            if (keys != null && !SettingsScreen.Holds)
            {
                if (keys.leftArrowKey.isPressed || keys.aKey.isPressed) turn -= 1;
                if (keys.rightArrowKey.isPressed || keys.dKey.isPressed) turn += 1;
            }
            stage.Turn.KeyTurn(turn);
        }

        /// <summary>The mouse on the stage (from OnGUI, in the 1080 layout): pressed on the model it holds it, dragged it turns it, let go it coasts; the wheel over it comes nearer or goes back.</summary>
        void TabsMouse(Event e)
        {
            if (e == null || !OnWeaponPage || SettingsScreen.Holds) return;
            WeaponStage stage = WeaponStage.Current;
            RunHud hud = RunHud.Current;
            if (stage == null || hud == null) return;
            Rect at = hud.TabStageRect;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && at.Contains(e.mousePosition)) stage.Turn.BeginDrag();
                    break;
                case EventType.MouseDrag:
                    if (stage.Turn.Dragging) stage.Turn.Drag(e.delta.x, e.delta.y);
                    break;
                case EventType.MouseUp:
                    if (stage.Turn.Dragging) stage.Turn.EndDrag();
                    break;
                case EventType.ScrollWheel:
                    if (at.Contains(e.mousePosition)) stage.Turn.Wheel(e.delta.y > 0f ? 1f : e.delta.y < 0f ? -1f : 0f);
                    break;
            }
        }

        // ---------------- The model's intents (the mouse's and the keys' alike; for tests) ----------------

        /// <summary>The model dragged by this many layout pixels (held from now on until <see cref="ReleaseModel"/>). False when WEAPON is not up.</summary>
        public bool DragModel(Vector2 layoutPixels)
        {
            WeaponStage stage = Stage;
            if (stage == null) return false;
            stage.Turn.Drag(layoutPixels.x, layoutPixels.y);
            return true;
        }

        /// <summary>The mouse let go of the model: it coasts and stops.</summary>
        public bool ReleaseModel()
        {
            WeaponStage stage = Stage;
            if (stage == null) return false;
            stage.Turn.EndDrag();
            return true;
        }

        /// <summary>The wheel turned this many notches over the model (positive: back, negative: nearer).</summary>
        public bool WheelModel(float notches)
        {
            WeaponStage stage = Stage;
            if (stage == null) return false;
            stage.Turn.Wheel(notches);
            return true;
        }

        /// <summary>R: the model back as it first stood.</summary>
        public bool ResetModel()
        {
            WeaponStage stage = Stage;
            if (stage == null) return false;
            stage.Turn.Reset();
            return true;
        }
    }
}
