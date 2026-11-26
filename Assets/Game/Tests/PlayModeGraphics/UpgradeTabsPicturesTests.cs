using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Work order 139: pictures of the upgrades in three tabs, taken without a window as work order 128's
    /// <c>SettingsPicturesTests</c> takes the menus' (the camera into a texture, every OnGUI called by hand, <see cref="ImguiByHand"/>),
    /// at 1920 by 1080 and each tab at 1280 by 720, to compare with the start menu's concept and to judge at 100 %: WEAPON with nothing
    /// fitted; with three parts fitted at the three mounts, at rest and in the middle of a drag at two angles (the nine parts in three
    /// sets of three, so each shape is seen); with the preview of a part not owned; RANKS; RACK with a piece open; a refused purchase
    /// on each. The stand-in's shape is written to the log (its box and its outline along the barrel) to set the mount points by.
    /// Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\139'
    ///   run-tests.ps1 PlayMode -Filter "UpgradeTabsPicturesTests"
    /// </summary>
    public class UpgradeTabsPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const int W = 1920, H = 1080;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SkillBar _bar;
        string _folder, _saves;
        Dictionary<string, int?> _prefs;
        readonly List<string> _log = new List<string>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 139)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _prefs = SettingsPrefs.Save();
            UpgradeTabsValues.On = true;
            WeaponPartValues.GentlerCosts = true;
            MenuHelpValues.On = true;
            WeaponStage.LoadHook = null;
            _saves = Path.Combine(Path.GetTempPath(), "UpgradeTabsPicturesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            PlayerCharacter player = Object.FindAnyObjectByType<PlayerCharacter>();
            player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (WeaponStage.Current != null) Object.Destroy(WeaponStage.Current.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            AttachmentsSwitch.Unload();
            SettingsPrefs.Restore(_prefs);
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Save(int echoes, string[] owned, string[] fitted, Action<MetaState> more = null)
        {
            var s = new MetaState { Currency = echoes, EarnedInAll = 400, Runs = 6 };
            if (owned != null) s.AttachmentsOwned.AddRange(owned);
            if (fitted != null) s.AttachmentsFitted.AddRange(fitted);
            more?.Invoke(s);
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            // The save is read once: after that the state in memory is the one shown, so it is set too.
            MetaState live = _levels.BetweenRunsState;
            if (live != null && !ReferenceEquals(live, s))
            {
                live.Currency = s.Currency;
                live.AttachmentsOwned.Clear();
                live.AttachmentsOwned.AddRange(s.AttachmentsOwned);
                live.AttachmentsFitted.Clear();
                live.AttachmentsFitted.AddRange(s.AttachmentsFitted);
                live.Owned.Clear();
                live.Owned.AddRange(s.Owned);
                live.Equipped.Clear();
                live.Equipped.AddRange(s.Equipped);
                live.Ranks.Clear();
                foreach (var r in s.Ranks) live.Ranks[r.Key] = r.Value;
            }
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
        }

        IEnumerator OpenWeapon()
        {
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assume.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            for (int i = 0; i < 4; i++) yield return null;
        }

        IEnumerator Close()
        {
            _menu.Close();
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>The model turned to an angle in the middle of a drag (held, so it neither coasts nor turns by itself).</summary>
        IEnumerator HeldAt(float yaw, float pitch)
        {
            WeaponStage stage = _menu.Stage;
            Assume.That(stage, Is.Not.Null);
            stage.Turn.BeginDrag();
            stage.Turn.Yaw = yaw;
            stage.Turn.Pitch = pitch;
            yield return null;
            yield return null;
        }

        void Hover(WeaponSlot slot, string id)
        {
            int row = _hud.TabItemOf(_menu, slot, id);
            Assume.That(row, Is.GreaterThanOrEqualTo(0));
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, row).center);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator A_Weapon_NothingFitted_ThenThreeSetsOfThreeParts_AtRestAndInADragAtTwoAngles()
        {
            Save(120, null, null);
            yield return OpenWeapon();
            LogShape();
            yield return Picture("weapon-rest", "WEAPON, nothing fitted, at rest, the cursor on SIGHT's none");
            yield return Picture("weapon-rest-720", "the same at 1280 by 720", 1280, 720);
            // The model's two sides square on, and near (the wheel's nearest), to judge it at 100 %.
            yield return HeldAt(0f, 0f);
            yield return Picture("weapon-left-side", "nothing fitted, square on: her pistol's left side (the muzzle to the right)");
            yield return HeldAt(180f, 0f);
            yield return Picture("weapon-right-side", "nothing fitted, square on: her pistol's right side (the muzzle to the left), where the generator's preview had a grey band, a glint and grey patches");
            _menu.Stage.Turn.Distance = WeaponStageValues.NearestDistance;
            yield return HeldAt(WeaponStageValues.RestYaw, WeaponStageValues.RestPitch);
            yield return Picture("weapon-near", "nothing fitted, at rest, the wheel at its nearest");
            _menu.Stage.Turn.Distance = WeaponStageValues.RestDistance;
            _menu.ReleaseModel();
            yield return Close();

            string[][] sets =
            {
                new[] { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip },
                new[] { WeaponAttachments.ReflexSight, WeaponAttachments.LongBarrel, WeaponAttachments.WeightedGrip },
                new[] { WeaponAttachments.GhostRing, WeaponAttachments.Compensator, WeaponAttachments.QuickDrawGrip },
            };
            for (int k = 0; k < sets.Length; k++)
            {
                Save(120, sets[k], sets[k]);
                yield return OpenWeapon();
                string names = string.Join(", ", sets[k]);
                yield return Picture("weapon-set" + (k + 1) + "-rest", "WEAPON with " + names + " fitted, at rest");
                Hover(WeaponSlot.Barrel, sets[k][1]);
                yield return HeldAt(WeaponStageValues.RestYaw + 75f, -8f);
                LogMounts("set " + (k + 1) + " angle 1");
                yield return Picture("weapon-set" + (k + 1) + "-drag-a", "the same in a drag: turned 75 degrees, tipped down (the cursor on the barrel: its line to the muzzle)");
                Hover(WeaponSlot.Grip, sets[k][2]);
                yield return HeldAt(WeaponStageValues.RestYaw + 150f, 32f);
                LogMounts("set " + (k + 1) + " angle 2");
                yield return Picture("weapon-set" + (k + 1) + "-drag-b", "the same in a drag: turned 150 degrees, tipped up (the cursor on the grip)");
                _menu.ReleaseModel();
                yield return Close();
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator B_Weapon_ThePreviewOfAPartNotOwned_AndARefusedPurchase()
        {
            Save(20, new[] { WeaponAttachments.LaserSight }, new[] { WeaponAttachments.LaserSight });
            yield return OpenWeapon();
            Hover(WeaponSlot.Barrel, WeaponAttachments.BullBarrel);
            yield return null;
            yield return Picture("weapon-preview", "the cursor on the Bull Barrel, not owned (40 Echoes, she has 20): ghosted on the model, the numbers' changes beside them");
            int row = _hud.TabItemOf(_menu, WeaponSlot.Barrel, WeaponAttachments.BullBarrel);
            _menu.UseItem(row);
            yield return Picture("weapon-refused", "the try at it (one frame in): its price and the Echoes plate red, the line saying how many more");
            yield return Close();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator C_Ranks_AndRack_AndARefusedPurchaseOnEach()
        {
            MetaValues v = _levels.MetaValues;
            Save(60, null, null, s =>
            {
                s.Ranks[MetaRanks.Health] = 3;
                s.Ranks[MetaRanks.Damage] = 1;
                s.Ranks[MetaRanks.RoomHeal] = 5;
                MetaPiece steady = v.Pieces.First(p => p.Name == "Steady Start");
                s.Owned.Add(steady.Id);
                s.Equipped.Add(steady.Id);
                s.Owned.Add(v.Pieces.First(p => p.Name == "Thick Hide").Id);
            });
            yield return OpenWeapon();
            Assume.That(_menu.OpenPage(UpgradesPage.Ranks), Is.True);
            yield return null;
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, 0).center);
            yield return Picture("ranks", "RANKS: health 3 of 10, damage 1, crit 0, room heal 5 of 5 (60 Echoes; the cursor on health)");
            yield return Picture("ranks-720", "the same at 1280 by 720", 1280, 720);
            _menu.UseItem(0);
            yield return Picture("ranks-refused", "a try at health's fourth rank (110 Echoes, she has 60): the refusal, one frame in");

            _levels.BetweenRunsState.Currency = 20; // so that some pieces are too dear
            Assume.That(_menu.OpenPage(UpgradesPage.Rack), Is.True);
            yield return null;
            yield return null;
            int open = Enumerable.Range(0, _menu.ItemCount).First(i => _hud.TabItemName(_menu, i) == "Marked Target" || i == _menu.ItemCount - 1);
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, open).center);
            yield return Picture("rack", "RACK with " + _hud.TabItemName(_menu, open) + " open (Steady Start equipped, Thick Hide owned)");
            yield return Picture("rack-720", "the same at 1280 by 720", 1280, 720);
            int dear = Enumerable.Range(0, _menu.ItemCount).FirstOrDefault(i => _hud.TabItemUnaffordable(_menu, i));
            _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, dear).center);
            _menu.UseItem(dear);
            yield return Picture("rack-refused", "a try at " + _hud.TabItemName(_menu, dear) + ", too dear: the refusal, one frame in");
            yield return Close();
        }

        // ---------------- The stand-in's shape, for the mount points ----------------

        void LogShape()
        {
            WeaponStage stage = _menu.Stage;
            if (stage == null) return;
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} STAGE model from {1}; half size on the stage ({2:0.###}, {3:0.###}, {4:0.###}); meshes: {5}",
                                   DateTime.Now, stage.ModelSource, stage.Half.x, stage.Half.y, stage.Half.z, string.Join(", ", stage.ModelMeshes.Select(m => m.name + (m.isReadable ? " (readable)" : " (not readable)")))));
            // Its outline along the barrel, in its box (-1 to 1), read off the stage's own picture square on (yaw 0, no tip): for twenty
            // slices from -X to +X, the highest and lowest row the model covers, taken back to the plane through its middle.
            float yaw = stage.Turn.Yaw, pitch = stage.Turn.Pitch;
            stage.Turn.Yaw = 0f;
            stage.Turn.Pitch = 0f;
            stage.DrawIfChanged();
            RenderTexture rt = stage.Texture;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            Color32[] px = tex.GetPixels32();
            Color32 back = px[0];
            Camera cam = stage.Camera;
            Transform turn = stage.MountOn(WeaponSlot.Sight).parent;
            var words = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                float bx = -0.95f + i * 0.1f;
                Vector3 vp = cam.WorldToViewportPoint(turn.TransformPoint(new Vector3(bx * stage.Half.x, 0f, 0f)));
                int col = Mathf.Clamp(Mathf.RoundToInt(vp.x * (rt.width - 1)), 0, rt.width - 1);
                int top = -1, bottom = -1;
                for (int row = 0; row < rt.height; row++)
                {
                    Color32 c = px[row * rt.width + col];
                    bool model = Math.Abs(c.r - back.r) + Math.Abs(c.g - back.g) + Math.Abs(c.b - back.b) > 12;
                    if (!model) continue;
                    if (bottom < 0) bottom = row;
                    top = row;
                }
                float BoxY(int row)
                {
                    Ray ray = cam.ViewportPointToRay(new Vector3(vp.x, (row + 0.5f) / rt.height, 0f));
                    var plane = new Plane(turn.forward, turn.position);
                    plane.Raycast(ray, out float d);
                    return turn.InverseTransformPoint(ray.GetPoint(d)).y / Mathf.Max(1e-5f, stage.Half.y);
                }
                words.Add(top < 0 ? string.Format(Inv, "x {0:0.00}: none", bx) : string.Format(Inv, "x {0:0.00}: y {1:0.00} to {2:0.00}", bx, BoxY(bottom), BoxY(top)));
            }
            Object.Destroy(tex);
            stage.Turn.Yaw = yaw;
            stage.Turn.Pitch = pitch;
            stage.DrawIfChanged();
            _log.Add("STAGE outline in the box (x along the barrel, y up; read off its picture square on): " + string.Join("; ", words));
        }

        void LogMounts(string when)
        {
            WeaponStage stage = _menu.Stage;
            if (stage == null) return;
            var words = new List<string>();
            foreach (WeaponSlot slot in new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip })
                words.Add(stage.MountInPicture(slot, out Vector2 at) ? string.Format(Inv, "{0} at ({1:0.000}, {2:0.000}) of the picture", slot, at.x, at.y) : slot + " behind");
            _log.Add("STAGE " + when + ": " + string.Join("; ", words));
        }

        // ---------------- A picture ----------------

        IEnumerator Picture(string name, string what, int width = W, int height = H)
        {
            yield return null;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(width, height);
                cam.targetTexture = rt;
                if (_bar != null) _bar.Tick();
                _hud.Tick();
                _menu.Tick(null);
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                string outside = StartMenu.Holds ? _menu.TextOutside() : "";
                WeaponStage stage = WeaponStage.Current;
                string stageWords = stage != null && stage.Texture != null
                    ? string.Format(Inv, "; stage {0}x{1} x{2} samples, {3} draws, yaw {4:0} pitch {5:0}", stage.Texture.width, stage.Texture.height, stage.Texture.antiAliasing, stage.Draws, stage.Turn.Yaw, stage.Turn.Pitch)
                    : "";
                _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}; Echoes {5}; note [{6}]; text outside: [{7}]{8}; drawn: {9}",
                                       DateTime.Now, name, width, height, what, _levels.BetweenRunsState.Currency, _menu.Note, outside, stageWords, drew));
            }
            catch (Exception e) { _log.Add(name + " failed: " + e); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                rt.Release();
                Object.Destroy(rt);
            }
        }
    }
}
