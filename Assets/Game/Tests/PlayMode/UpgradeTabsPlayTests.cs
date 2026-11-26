using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    /// Work order 139 in the ControlRoom scene through the start menu's intents, a test keyboard and its one hit test: the main
    /// menu's UPGRADES opens WEAPON and each of the five tabs opens its page (Tab, Q, E too); each page buys what the cards' screen
    /// bought for the same price (a rank, a piece, a part; "none" takes off); a drag turns the model and a release lets it coast and
    /// stop, the wheel stays inside its limits, R sets it back; a fitted part is drawn at its mount and turns with the model; the
    /// preview changes the numbers shown and not the saved ones; the stage shows whatever is at the model's named place, and her
    /// pistol without it; the screen between runs still works; the refused purchase and the dim row of the menus' help; the
    /// attachments switched off are switched on by a fit, and the next run uses what is fitted; every word inside its shape at
    /// 1920 by 1080 and 1280 by 720; nothing allocated while a page stands still; the switch off gives the cards' screen. His
    /// saved things are never touched: the save and the records are the test's own, the attachments' stored choice is put back.
    /// </summary>
    public class UpgradeTabsPlayTests
    {
        const int MostFrames = 600;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        Keyboard _keyboard;
        string _saves;
        bool _hadAttachmentsKey;
        int _attachmentsKey;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _saves = Path.Combine(Path.GetTempPath(), "UpgradeTabsPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // never the game's own save
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName); // never his records
            _hadAttachmentsKey = PlayerPrefs.HasKey(AttachmentsSwitch.Key); // his stored choice, put back after
            _attachmentsKey = PlayerPrefs.GetInt(AttachmentsSwitch.Key, AttachmentsSwitch.NoOverride);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            UpgradeTabsValues.On = true;
            WeaponPartValues.GentlerCosts = true;
            MenuHelpValues.On = true;
            WeaponStage.LoadHook = null;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // each test turns the trials on itself
            _levels.MetaValues.Attachments = false;
            _levels.RunValues.SkillTiers = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            _menu.TestKeyboard = _keyboard;
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            WeaponStage.LoadHook = null;
            if (WeaponStage.Current != null) Object.Destroy(WeaponStage.Current.gameObject);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            if (_hadAttachmentsKey) PlayerPrefs.SetInt(AttachmentsSwitch.Key, _attachmentsKey);
            else PlayerPrefs.DeleteKey(AttachmentsSwitch.Key);
            PlayerPrefs.Save();
            AttachmentsSwitch.Unload();
            UpgradeTabsValues.On = true;
            WeaponPartValues.GentlerCosts = true;
            MenuHelpValues.On = true;
            GameSettings.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
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

        static IEnumerator Seconds(float s)
        {
            float until = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < until) yield return null;
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

        /// <summary>The trials on, a save with these Echoes and what it owns and has fitted.</summary>
        MetaState TrialsOn(int echoes = 500, string[] parts = null, string[] fitted = null)
        {
            var s = new MetaState { Currency = echoes };
            if (parts != null) s.AttachmentsOwned.AddRange(parts);
            if (fitted != null) s.AttachmentsFitted.AddRange(fitted);
            Assert.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
            return s;
        }

        IEnumerator OpenUpgrades(UpgradesPage page = UpgradesPage.Weapon)
        {
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            yield return null;
            if (page != UpgradesPage.Weapon) Assert.That(_menu.OpenPage(page), Is.True);
            yield return null;
            yield return null;
        }

        MetaState Save => _levels.BetweenRunsState;
        int Row(WeaponSlot slot, string id) => _hud.TabItemOf(_menu, slot, id);

        // ---------------- The tabs ----------------

        [UnityTest]
        public IEnumerator TheMainMenusUpgrades_OpensWeapon_AndEveryTabOpensItsPage()
        {
            TrialsOn();
            yield return OpenUpgrades();
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Upgrades));
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Weapon), "UPGRADES opens WEAPON");
            for (int t = 0; t < 5; t++) Assert.That(_menu.HitRect(StartMenu.HitKind.Tab, t).width, Is.GreaterThan(0f), "tab " + t);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 2).center), Is.True);
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Ranks));
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 3).center), Is.True);
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Rack));
            Assert.That(_menu.ItemCount, Is.EqualTo(_levels.MetaValues.Pieces.Length), "the rack's list: a row a piece");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 4).center), Is.True);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Stats));
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 1).center), Is.True);
            Assert.That(new object[] { _menu.Screen, _menu.Page }, Is.EqualTo(new object[] { MenuScreen.Upgrades, UpgradesPage.Weapon }));
            yield return Tap(Key.Tab);
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Ranks), "Tab: the next page");
            Assert.That(_menu.ItemCount, Is.EqualTo(_levels.MetaValues.Ranks.Length), "a row a rank");
            yield return Tap(Key.E);
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Rack), "E: the next");
            yield return Tap(Key.Q);
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Ranks), "Q: the one before");
            yield return Tap(Key.Escape);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "Esc: back to the main menu");
            Assert.That(_menu.Page, Is.EqualTo(UpgradesPage.Weapon), "and UPGRADES opens WEAPON again");
            yield return null;
            yield return null;
            Assert.That(WeaponStage.Current, Is.Null, "the stage goes with the tab");
        }

        [UnityTest]
        public IEnumerator SwitchedOff_UpgradesIsTheOneScreenOfCards_WithThreeTabs()
        {
            UpgradeTabsValues.On = false;
            TrialsOn();
            yield return OpenUpgrades();
            Assert.That(_hud.MenuCards.Count, Is.GreaterThan(0), "the cards");
            Assert.That(_menu.HitRect(StartMenu.HitKind.Tab, 3).width, Is.EqualTo(0f), "three tabs, as before");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 2).center), Is.True);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Stats), "the third tab is STATS");
            yield return null;
            yield return null;
            Assert.That(WeaponStage.Current, Is.Null, "no stage");
        }

        // ---------------- Buying, as the cards bought ----------------

        [UnityTest]
        public IEnumerator EachPage_BuysWhatTheCardsBought_ForTheSamePrice()
        {
            TrialsOn(500);
            MetaValues v = _levels.MetaValues;
            yield return OpenUpgrades(UpgradesPage.Ranks);
            int before = Save.Currency;
            Assert.That(_menu.UseItem(0), Is.True, "a rank");
            Assert.That(before - Save.Currency, Is.EqualTo(MetaRanks.Price(v, 1)), "its price, as the card's");
            Assert.That(Save.RankOf(v.Ranks[0].Id), Is.EqualTo(1));

            Assert.That(_menu.OpenPage(UpgradesPage.Rack), Is.True);
            yield return null;
            int piece = Enumerable.Range(0, v.Pieces.Length).First(i => MetaRack.CanBuy(Save, v, v.Pieces[i].Id) == MetaBuy.Bought);
            before = Save.Currency;
            Assert.That(_menu.UseItem(piece), Is.True, "a piece");
            Assert.That(before - Save.Currency, Is.EqualTo(v.Pieces[piece].Price));
            Assert.That(Save.Owns(v.Pieces[piece].Id), Is.True);
            Assert.That(_menu.UseItem(piece), Is.False, "owned: it refuses, as on the cards");
            Assert.That(_menu.Note, Is.EqualTo(StartMenu.OwnedAlready));

            Assert.That(_menu.OpenPage(UpgradesPage.Weapon), Is.True);
            yield return null;
            before = Save.Currency;
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, WeaponAttachments.LaserSight)), Is.True, "a part");
            Assert.That(before - Save.Currency, Is.EqualTo(WeaponRack.Find(v, WeaponAttachments.LaserSight).Price), "its price");
            Assert.That(WeaponRack.IsFitted(Save, WeaponAttachments.LaserSight), Is.True, "bought and fitted at once");
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, WeaponAttachments.LaserSight)), Is.True, "one press takes it off");
            Assert.That(WeaponRack.IsFitted(Save, WeaponAttachments.LaserSight), Is.False);
            Assert.That(WeaponRack.Owns(Save, WeaponAttachments.LaserSight), Is.True, "still hers");
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, WeaponAttachments.LaserSight)), Is.True, "one press fits it again");
            before = Save.Currency;
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, null)), Is.True, "\"none\" takes it off");
            Assert.That(WeaponRack.FittedIn(Save, v, WeaponKind.Pistols, WeaponSlot.Sight), Is.Null);
            Assert.That(Save.Currency, Is.EqualTo(before), "for nothing");
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, null)), Is.False, "the stock gun already");
            Assert.That(MetaSaveFile.Load(GeneratedLevels.BetweenRunsSavePathInUse, _levels.MetaValues, out MetaLoad _, out string _).AttachmentsOwned, Does.Contain(WeaponAttachments.LaserSight), "written to the save as the cards' presses write it");
        }

        // ---------------- The model ----------------

        [UnityTest]
        public IEnumerator ADrag_TurnsTheModel_ARelease_CoastsAndStops_TheWheelInItsLimits_RSetsItBack()
        {
            TrialsOn();
            yield return OpenUpgrades();
            WeaponStage stage = _menu.Stage;
            Assert.That(stage, Is.Not.Null, "the stage stands with WEAPON");
            yield return Frames(() => stage.Texture != null);
            Assert.That(stage.Texture, Is.Not.Null);
            Assert.That(stage.Texture.antiAliasing, Is.EqualTo(WeaponStageValues.Samples), "drawn with anti-aliasing");
            Assert.That(stage.Camera.cullingMask, Is.EqualTo(1 << WeaponStage.Layer), "its camera sees the stage only");
            Assert.That(stage.Camera.enabled, Is.False, "and never draws by itself");
            Assert.That(stage.transform.position.y, Is.LessThan(-1000f), "far below the floor");
            Quaternion rest = stage.MountOn(WeaponSlot.Barrel).parent.rotation;
            float yaw0 = stage.Turn.Yaw;
            for (int i = 0; i < 6; i++)
            {
                Assert.That(_menu.DragModel(new Vector2(25f, 0f)), Is.True);
                yield return Seconds(0.02f);
            }
            Assert.That(stage.Turn.Yaw - yaw0, Is.EqualTo(150f * WeaponStageValues.DegreesPerPixel).Within(0.01f), "the drag turns it");
            Assert.That(Quaternion.Angle(rest, stage.MountOn(WeaponSlot.Barrel).parent.rotation), Is.GreaterThan(10f), "the model turned on the stage");
            int drawsBefore = stage.Draws;
            _menu.ReleaseModel();
            float atRelease = stage.Turn.Yaw;
            yield return Seconds(0.1f);
            Assert.That(stage.Turn.Yaw, Is.GreaterThan(atRelease), "it coasts on");
            Assert.That(stage.Draws, Is.GreaterThan(drawsBefore), "and is drawn again as it goes");
            yield return Frames(() => stage.Turn.YawSpeed == 0f);
            yield return null;
            float stopped = stage.Turn.Yaw;
            int draws = stage.Draws;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(stage.Turn.Yaw, Is.EqualTo(stopped), "it stopped");
            Assert.That(stage.Draws, Is.EqualTo(draws), "still: not drawn again");
            _menu.WheelModel(-100f);
            Assert.That(stage.Turn.Distance, Is.EqualTo(WeaponStageValues.NearestDistance), "the wheel's nearest");
            _menu.WheelModel(100f);
            Assert.That(stage.Turn.Distance, Is.EqualTo(WeaponStageValues.FarthestDistance), "and farthest");
            yield return Tap(Key.R);
            Assert.That(new[] { stage.Turn.Yaw, stage.Turn.Pitch, stage.Turn.Distance }, Is.EqualTo(new[] { WeaponStageValues.RestYaw, WeaponStageValues.RestPitch, WeaponStageValues.RestDistance }), "R sets it back");
            yield return Press(Key.D);
            float held = stage.Turn.Yaw;
            yield return Seconds(0.2f);
            Assert.That(stage.Turn.Yaw, Is.GreaterThan(held), "D held turns it");
            yield return Press();
        }

        [UnityTest]
        public IEnumerator AFittedPart_IsDrawnAtItsMount_AndTurnsWithTheModel_OnEachOfTheThree()
        {
            string[] three = { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip };
            TrialsOn(0, three, three);
            yield return OpenUpgrades();
            WeaponStage stage = _menu.Stage;
            yield return Frames(() => stage.Texture != null);
            Vector3 half = stage.Half;
            foreach (WeaponSlot slot in new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip })
            {
                Transform part = stage.PartOn(slot), mount = stage.MountOn(slot);
                Assert.That(part, Is.Not.Null, slot + ": drawn");
                Assert.That(stage.Shown(slot), Is.EqualTo(three[(int)slot]));
                Assert.That(stage.Ghosted(slot), Is.False, "owned: not ghosted");
                Assert.That(part.parent, Is.SameAs(mount), "at its mount");
                WeaponMount m = WeaponStageValues.MountOf(slot);
                Assert.That(Vector3.Distance(mount.localPosition, new Vector3(m.At.X * half.x, m.At.Y * half.y, m.At.Z * half.z)), Is.LessThan(1e-4f), "the mount where the values put it in the model's box");
                Renderer[] shapes = part.GetComponentsInChildren<Renderer>();
                Assert.That(shapes.Length, Is.GreaterThan(0));
                Bounds b = shapes[0].bounds;
                foreach (Renderer r in shapes) b.Encapsulate(r.bounds);
                Assert.That(Vector3.Distance(b.center, mount.position), Is.LessThan(0.3f * WeaponStageValues.ModelLength), slot + ": its shape is on the mount, not off it");
            }
            Vector3 sightBefore = stage.PartOn(WeaponSlot.Sight).position;
            Vector3 local = stage.MountOn(WeaponSlot.Sight).parent.InverseTransformPoint(sightBefore);
            _menu.DragModel(new Vector2(120f, 40f));
            yield return null;
            yield return null;
            _menu.ReleaseModel();
            Vector3 sightAfter = stage.PartOn(WeaponSlot.Sight).position;
            Assert.That(Vector3.Distance(sightBefore, sightAfter), Is.GreaterThan(0.05f), "it moved with the turn");
            Assert.That(Vector3.Distance(local, stage.MountOn(WeaponSlot.Sight).parent.InverseTransformPoint(sightAfter)), Is.LessThan(1e-4f), "and stayed where it sits on the model");
            Assert.That(stage.MountInPicture(WeaponSlot.Barrel, out Vector2 at), Is.True);
            Assert.That(at.x, Is.InRange(0f, 1f), "the muzzle in the picture, for the line from its row");
            Assert.That(at.y, Is.InRange(0f, 1f));
        }

        [UnityTest]
        public IEnumerator ThePreview_ChangesTheNumbersShown_AndTheModel_NotTheSavedThings()
        {
            TrialsOn(0);
            yield return OpenUpgrades();
            WeaponStage stage = _menu.Stage;
            yield return Frames(() => stage.Texture != null);
            byte[] saved = File.ReadAllBytes(GeneratedLevels.BetweenRunsSavePathInUse);
            int bull = Row(WeaponSlot.Barrel, WeaponAttachments.BullBarrel);
            Assert.That(bull, Is.GreaterThanOrEqualTo(0));
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, bull).center);
            Assert.That(_menu.Selected, Is.EqualTo(bull), "the cursor on the Bull Barrel");
            yield return null;
            yield return null;
            _hud.MakeTabNumbers(_menu);
            Assert.That(_hud.TabNumbersPreview[(int)WeaponNumber.Damage], Is.GreaterThan(_hud.TabNumbersNow[(int)WeaponNumber.Damage]), "harder, shown beside the number");
            Assert.That(_hud.TabNumbersPreview[(int)WeaponNumber.ShotsASecond], Is.LessThan(_hud.TabNumbersNow[(int)WeaponNumber.ShotsASecond]), "slower");
            Assert.That(_hud.TabNumberChanges[(int)WeaponNumber.Stagger], Is.EqualTo("+3"));
            Assert.That(stage.Shown(WeaponSlot.Barrel), Is.EqualTo(WeaponAttachments.BullBarrel), "shown on the model at once");
            Assert.That(stage.Ghosted(WeaponSlot.Barrel), Is.True, "ghosted: not hers");
            Assert.That(Save.AttachmentsFitted, Is.Empty, "nothing fitted");
            Assert.That(Save.AttachmentsOwned, Is.Empty, "nothing bought");
            Assert.That(File.ReadAllBytes(GeneratedLevels.BetweenRunsSavePathInUse), Is.EqualTo(saved), "the save untouched");
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, Row(WeaponSlot.Barrel, null)).center);
            yield return null;
            Assert.That(stage.Shown(WeaponSlot.Barrel), Is.Null, "the cursor off it: the slot as it is");
        }

        [UnityTest]
        public IEnumerator TheStage_ShowsWhateverIsAtTheModelsNamedPlace_AndHerPistolWithoutIt()
        {
            TrialsOn();
            // The model at Resources/Weapon/Pistol (the new one from Meshy): the stage shows it.
            var named = Resources.Load<GameObject>(WeaponStageValues.ModelPath);
            Assert.That(named, Is.Not.Null, "the model is at the named place");
            yield return OpenUpgrades();
            WeaponStage stage = _menu.Stage;
            yield return Frames(() => stage.Texture != null);
            Assert.That(stage.ModelSource, Is.EqualTo(WeaponStage.Source.NamedPlace));
            Assert.That(stage.ModelMeshes, Is.EqualTo(named.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray()), "its meshes, and nothing else");
            Assert.That(Mathf.Max(stage.Half.x, Mathf.Max(stage.Half.y, stage.Half.z)), Is.EqualTo(WeaponStageValues.ModelLength * 0.5f).Within(1e-3f), "its longest side the named length");
            yield return Tap(Key.Escape);
            yield return null;
            yield return null;
            Assert.That(WeaponStage.Current, Is.Null);

            // Nothing there (the hook says so): her pistol from her character model.
            WeaponStage.LoadHook = () => null;
            Assert.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            yield return null;
            yield return null;
            stage = _menu.Stage;
            yield return Frames(() => stage.Texture != null);
            Assert.That(stage.ModelSource, Is.EqualTo(WeaponStage.Source.HerPistol));
            CharacterAnimation her = Object.FindAnyObjectByType<CharacterAnimation>();
            Assert.That(her != null && her.RightWeapon != null, Is.True, "her pistol in her right hand");
            Mesh hers = her.RightWeapon.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(stage.ModelMeshes, Does.Contain(hers), "her own pistol's mesh");
            yield return Tap(Key.Escape);
            yield return null;
            yield return null;

            // Another model put there (a test's, through the hook, as Resources would give it): the stage shows that one.
            var mesh = new Mesh { name = "TheNewPistol", vertices = new[] { new Vector3(-0.9f, -0.3f, -0.1f), new Vector3(1f, 0.2f, 0.1f), new Vector3(0f, 0.4f, 0f) }, triangles = new[] { 0, 1, 2 } };
            var model = new GameObject("Pistol");
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>();
            WeaponStage.LoadHook = () => model;
            try
            {
                Assert.That(_menu.PickMain(MainLine.Upgrades), Is.True);
                yield return null;
                yield return null;
                stage = _menu.Stage;
                yield return Frames(() => stage.Texture != null);
                Assert.That(stage.ModelSource, Is.EqualTo(WeaponStage.Source.NamedPlace));
                Assert.That(stage.ModelMeshes, Is.EqualTo(new[] { mesh }), "the stage shows what is at the named place, and nothing else");
                Assert.That(stage.Half.x, Is.EqualTo(WeaponStageValues.ModelLength * 0.5f).Within(1e-3f), "scaled to the named length");
            }
            finally
            {
                WeaponStage.LoadHook = null;
                Object.Destroy(model);
                Object.Destroy(mesh);
            }
        }

        // ---------------- The menus' help, the trial, the screen between runs ----------------

        [UnityTest]
        public IEnumerator TheRefusedPurchase_AndTheDimRow_StillWork_OnEveryPage()
        {
            TrialsOn(10);
            yield return OpenUpgrades();
            int laser = Row(WeaponSlot.Sight, WeaponAttachments.LaserSight);
            Assert.That(_hud.MenuItemUnaffordable(_menu, laser), Is.True, "30 Echoes with 10: dimmer before she tries");
            Assert.That(_hud.MenuItemUnaffordable(_menu, Row(WeaponSlot.Sight, null)), Is.False, "\"none\" costs nothing");
            int refusals = _hud.EchoesRefusals;
            Assert.That(_menu.UseItem(laser), Is.False);
            Assert.That(_menu.Note, Is.EqualTo(MenuHelpRules.NotEnough(20, _levels.MetaValues.CurrencyName, "Laser Sight")), "how many more");
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 1), "the flash, the shake and the tick");
            Assert.That(Save.Currency, Is.EqualTo(10), "nothing bought");

            Assert.That(_menu.OpenPage(UpgradesPage.Ranks), Is.True);
            yield return null;
            Assert.That(_hud.MenuItemUnaffordable(_menu, 0), Is.True, "a rank at 40");
            Assert.That(_menu.UseItem(0), Is.False);
            Assert.That(_menu.Note, Does.StartWith("Not enough"));
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 2));

            Assert.That(_menu.OpenPage(UpgradesPage.Rack), Is.True);
            yield return null;
            MetaValues v = _levels.MetaValues;
            int dear = Enumerable.Range(0, v.Pieces.Length).First(i => MetaRack.CanBuy(Save, v, v.Pieces[i].Id) == MetaBuy.NotEnough);
            Assert.That(_hud.MenuItemUnaffordable(_menu, dear), Is.True);
            Assert.That(_menu.UseItem(dear), Is.False);
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 3));
            Assert.That(Save.Currency, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator TheAttachmentsSwitchedOff_AFitSwitchesThemOn_AndTheNextRunUsesWhatIsFitted()
        {
            TrialsOn(100, new[] { WeaponAttachments.GhostRing });
            AttachmentsSwitch.Set(false); // his choice in the settings: off
            Assert.That(_levels.AttachmentsOn, Is.False);
            yield return OpenUpgrades();
            Assert.That(_menu.ItemCount, Is.GreaterThan(3), "the parts are on the WEAPON tab all the same");
            // A fit switches them on (a refused buy does not: the next test).
            Assert.That(_menu.UseItem(Row(WeaponSlot.Sight, WeaponAttachments.GhostRing)), Is.True, "fitted");
            Assert.That(AttachmentsSwitch.Stored, Is.EqualTo(1), "switched on, and kept");
            Assert.That(PlayerPrefs.GetInt(AttachmentsSwitch.Key, -1), Is.EqualTo(1));
            Assert.That(_levels.AttachmentsOn, Is.True);
            Assert.That(Save.AttachmentsOwned, Does.Contain(WeaponAttachments.GhostRing), "what he owned is kept");
            Assert.That(WeaponRack.IsFitted(Save, WeaponAttachments.GhostRing), Is.True);
            _menu.Open(MenuScreen.Loadout);
            yield return null;
            Assert.That(_menu.StartTheRun(), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Weapon.Parts, Is.EqualTo(new[] { "Ghost Ring" }), "the next run uses what is fitted");
            Assert.That(_levels.Modifiers.Weapon.Reach(10f), Is.EqualTo(9f).Within(1e-5f), "with its halved cost");
        }

        [UnityTest]
        public IEnumerator AFitWithTheAttachmentsOff_ButTooFewEchoes_RefusesAndLeavesTheSwitch()
        {
            TrialsOn(5);
            AttachmentsSwitch.Set(false);
            yield return OpenUpgrades();
            int refusals = _hud.EchoesRefusals;
            Assert.That(_menu.UseItem(Row(WeaponSlot.Barrel, WeaponAttachments.BullBarrel)), Is.False);
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 1), "the menus' help answers it");
            Assert.That(_menu.Note, Is.EqualTo(MenuHelpRules.NotEnough(35, _levels.MetaValues.CurrencyName, "Bull Barrel")));
            Assert.That(AttachmentsSwitch.Stored, Is.EqualTo(0), "still off: nothing was fitted");
        }

        [UnityTest]
        public IEnumerator TheScreenBetweenRuns_StillWorks_WithTheTabsOn()
        {
            TrialsOn(500);
            Assert.That(_levels.OpenBetweenRuns(null), Is.True);
            _hud.Tick();
            yield return null;
            Assert.That(_hud.BetweenCardNames, Does.Contain("Laser Sight"), "its cards, as before");
            int before = Save.Currency;
            Assert.That(_hud.PickWeaponLine(0), Is.True);
            Assert.That(before - Save.Currency, Is.EqualTo(30));
            Assert.That(_hud.PickRankLine(0), Is.True);
            _levels.LeaveBetweenRuns();
            yield return null;
        }

        // ---------------- Measured ----------------

        [UnityTest]
        public IEnumerator EveryWord_InsideItsShape_OnEveryPage_AtBothSizes()
        {
            TrialsOn(45, new[] { WeaponAttachments.LaserSight }, new[] { WeaponAttachments.LaserSight });
            yield return OpenUpgrades();
            foreach (UpgradesPage page in new[] { UpgradesPage.Weapon, UpgradesPage.Ranks, UpgradesPage.Rack })
            {
                Assert.That(_menu.OpenPage(page), Is.True);
                yield return null;
                for (int item = 0; item < _menu.ItemCount; item++)
                {
                    _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, item).center);
                    foreach (Vector2Int size in Sizes)
                    {
                        InterfaceStyle.ScreenForPictures = size;
                        Assert.That(_menu.TextOutside(), Is.Empty, page + " with row " + item + " under the cursor at " + size);
                    }
                    InterfaceStyle.ScreenForPictures = Sizes[0];
                }
            }
        }

        [UnityTest]
        public IEnumerator NothingAllocated_WhileAPageStandsStill()
        {
            TrialsOn(45, new[] { WeaponAttachments.LaserSight }, new[] { WeaponAttachments.LaserSight });
            yield return OpenUpgrades();
            foreach (UpgradesPage page in new[] { UpgradesPage.Ranks, UpgradesPage.Rack, UpgradesPage.Weapon })
            {
                Assert.That(_menu.OpenPage(page), Is.True);
                for (int i = 0; i < 4; i++)
                {
                    _menu.Tick(null);
                    _hud.Tick();
                    _menu.LayOutForTests();
                    yield return null;
                }
                WeaponStage stage = WeaponStage.Current;
                if (page == UpgradesPage.Weapon) Assert.That(stage, Is.Not.Null);
                Assert.That(() =>
                {
                    for (int i = 0; i < 300; i++)
                    {
                        _menu.Tick(null);
                        _hud.Tick();
                        _menu.LayOutForTests();
                        if (stage != null) stage.DrawIfChanged();
                    }
                }, NotAllocating, page + ": nothing allocated in a frame that changes nothing");
            }
            // For the report: what one draw of the stage costs the heap (the engine's render of a camera by hand).
            WeaponStage st = WeaponStage.Current;
            long before = GC.GetAllocatedBytesForCurrentThread();
            st.Turn.Yaw += 1f;
            st.DrawIfChanged();
            long one = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine("one draw of the stage allocated " + one + " bytes");
            Debug.Log("UpgradeTabsPlayTests: one draw of the stage allocated " + one + " bytes");
        }
    }
}
