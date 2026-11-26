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
    /// on each. The model's shape is written to the log (its box and its outline along the barrel) to check the mount points by.
    /// 2026-11-26, the high-detail original of her pistol on the stage: each of the nine parts alone on its mount, from the side and
    /// at rest, as the stage's own picture (its texture, twice the layout's pixels: to judge a fit at 100 %); the model in the six
    /// views of the reference sheet of her in-hand pistol, under the sheet when ROGUELIKE_WEAPON_REFERENCE names it; the same in
    /// the toon look; and what the stage costs (A0).
    /// Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\139'
    ///   run-tests.ps1 PlayMode -Filter "UpgradeTabsPicturesTests"
    /// </summary>
    public class UpgradeTabsPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        /// <summary>The six views of her in-hand pistol rendered on 2026-11-26 (codex\images\menu\round3\weapon\weapon_reference.png), if named.</summary>
        const string ReferenceVariable = "ROGUELIKE_WEAPON_REFERENCE";
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
            WeaponStageValues.ToonOnStage = false;
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
            yield return Picture("weapon-right-side", "nothing fitted, square on: her pistol's right side (the muzzle to the left)");
            _menu.Stage.Turn.Distance = WeaponStageValues.NearestDistance;
            yield return HeldAt(WeaponStageValues.RestYaw, WeaponStageValues.RestPitch);
            yield return Picture("weapon-near", "nothing fitted, at rest, the wheel at its nearest");
            _menu.Stage.Turn.Distance = WeaponStageValues.RestDistance;
            _menu.ReleaseModel();
            yield return Close();

            // The same in a run's toon look, for the owner to choose between (WeaponStageValues.ToonOnStage).
            WeaponStageValues.ToonOnStage = true;
            try
            {
                yield return OpenWeapon();
                yield return Picture("weapon-rest-toon", "nothing fitted, at rest, in the toon look her kit wears in a run");
                yield return HeldAt(180f, 0f);
                yield return Picture("weapon-right-side-toon", "her pistol's right side in the toon look");
                yield return SixViews("same-gun-toon", "the toon look");
                _menu.ReleaseModel();
                yield return Close();
            }
            finally { WeaponStageValues.ToonOnStage = false; }

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

        [UnityTest, Timeout(900000)]
        public IEnumerator D_TheModel_InTheReferencesSixViews_AndEachPartAlone_FromTheSideAndAtRest()
        {
            Save(120, null, null);
            yield return OpenWeapon();
            yield return SixViews("same-gun", "the lit material");
            _menu.ReleaseModel();
            yield return Close();
            string[] all =
            {
                WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.ReflexSight,
                WeaponAttachments.BullBarrel, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
                WeaponAttachments.RunnersGrip, WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
            };
            foreach (string part in all)
            {
                Save(120, new[] { part }, new[] { part });
                yield return OpenWeapon();
                string name = part.ToLowerInvariant().Replace(' ', '-').Replace("'", "");
                yield return HeldAt(0f, 0f);
                StagePicture("part-" + name + "-side", part + " alone, square on (the muzzle to the right)");
                yield return HeldAt(180f, 0f);
                StagePicture("part-" + name + "-other-side", part + " alone, the other side");
                yield return HeldAt(WeaponStageValues.RestYaw + 40f, 28f);
                StagePicture("part-" + name + "-above", part + " alone, from in front and above");
                _menu.ReleaseModel();
                yield return Close();
            }
            // Each family of three together in one picture of the tab (each part fitted in turn is what the tab shows; here the
            // three of a family side by side are three pictures of the stage made into one).
            string[][] families =
            {
                new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.ReflexSight },
                new[] { WeaponAttachments.BullBarrel, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator },
                new[] { WeaponAttachments.RunnersGrip, WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip },
            };
            string[] familyNames = { "sights", "barrels", "grips" };
            for (int f = 0; f < families.Length; f++)
            {
                RenderTexture sheet = null;
                try
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Save(120, families[f], new[] { families[f][k] });
                        yield return OpenWeapon();
                        WeaponStage stage = _menu.Stage;
                        if (sheet == null) sheet = new RenderTexture(stage.Texture.width * 3 / 2, stage.Texture.height / 2, 0, RenderTextureFormat.ARGB32);
                        DrawInto(sheet, stage.Texture, new Rect(k * sheet.width / 3f, 0f, sheet.width / 3f, sheet.height));
                        yield return Close();
                    }
                    SaveTexture(sheet, "family-" + familyNames[f], "the " + familyNames[f] + ", each fitted alone at rest, side by side: " + string.Join(", ", families[f]));
                }
                finally
                {
                    if (sheet != null)
                    {
                        sheet.Release();
                        Object.Destroy(sheet);
                    }
                }
            }
        }

        /// <summary>
        /// The model held in the six views of the reference sheet of her in-hand pistol (rendered 2026-11-26), drawn in the sheet's
        /// layout, and under the sheet itself when it is named: so it can be seen at a glance that it is the same gun.
        /// </summary>
        IEnumerator SixViews(string name, string what)
        {
            WeaponStage stage = _menu.Stage;
            Assume.That(stage, Is.Not.Null);
            // The reference's views, left to right and top to bottom: its left side (muzzle left), its right side (muzzle right), from
            // in front at its right, from behind at its left, from above, the muzzle face on.
            float[][] views = { new[] { 180f, 0f }, new[] { 0f, 0f }, new[] { -55f, 12f }, new[] { 145f, 12f }, new[] { 0f, 89f }, new[] { -90f, 0f } };
            const int SheetW = 2400, Half = 1500;
            Texture2D reference = null;
            string referencePath = Environment.GetEnvironmentVariable(ReferenceVariable);
            if (!string.IsNullOrEmpty(referencePath) && File.Exists(referencePath))
            {
                reference = new Texture2D(2, 2);
                if (!reference.LoadImage(File.ReadAllBytes(referencePath))) reference = null;
            }
            var sheet = new RenderTexture(SheetW, reference != null ? Half * 2 : Half, 0, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = sheet;
                GL.Clear(true, true, new Color(0.115f, 0.125f, 0.14f).linear); // the stage's backdrop (the target is written as linear)
                RenderTexture.active = was;
                if (reference != null) DrawInto(sheet, reference, new Rect(0f, 0f, SheetW, Half));
                float top = reference != null ? Half : 0f;
                float distance = stage.Turn.Distance;
                stage.Turn.Distance = WeaponStageValues.RestDistance;
                for (int i = 0; i < views.Length; i++)
                {
                    yield return HeldAt(views[i][0], views[i][1]);
                    stage.DrawIfChanged();
                    float w = SheetW / 3f, h = Half / 2f;
                    DrawInto(sheet, stage.Texture, new Rect((i % 3) * w, top + (i / 3) * h, w, h));
                }
                stage.Turn.Distance = distance;
                SaveTexture(sheet, name, "her pistol on the stage (" + what + ") in the reference's six views" + (reference != null ? ", under the reference sheet of her in-hand pistol" : ""));
            }
            finally
            {
                sheet.Release();
                Object.Destroy(sheet);
                if (reference != null) Object.Destroy(reference);
            }
        }

        /// <summary>A texture drawn into a rectangle of <paramref name="target"/> (pixels from its top left), kept to its own shape (fitted inside it, centred).</summary>
        static void DrawInto(RenderTexture target, Texture source, Rect into)
        {
            float aspectIn = into.width / into.height, aspectSource = source.width / (float)source.height;
            if (aspectSource > aspectIn) into = new Rect(into.x, into.center.y - into.width / aspectSource * 0.5f, into.width, into.width / aspectSource);
            else into = new Rect(into.center.x - into.height * aspectSource * 0.5f, into.y, into.height * aspectSource, into.height);
            var uv = new Rect(0f, 0f, 1f, 1f);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, target.width, target.height, 0f);
            Graphics.DrawTexture(into, source, uv, 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = was;
        }

        /// <summary>The stage's own picture (its texture, at its own size) as it stands now, written as a file.</summary>
        void StagePicture(string name, string what)
        {
            WeaponStage stage = _menu.Stage;
            if (stage == null) return;
            stage.DrawIfChanged();
            SaveTexture(stage.Texture, name, what);
        }

        void SaveTexture(RenderTexture rt, string name, string what)
        {
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}", DateTime.Now, name, rt.width, rt.height, what));
        }

        // ---------------- What the stage costs ----------------

        /// <summary>
        /// The stage's cost (the producer's question: friends play on weaker machines): the first opening of WEAPON with nothing of
        /// the model loaded (Resources unloaded first: the prefab, its 470,344-triangle mesh and its textures read and sent to the
        /// GPU, the stage built and drawn once), measured round that work and as the longest frame; then one draw of the stage with
        /// the GPU waited for (a pixel read back), and a menu frame with the tab standing still. Written to the log; nothing asserted
        /// but that it was measured. Editor, batch mode, no window: a built player's numbers differ.
        /// </summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator A0_TheStagesCost_TheFirstOpening_ADraw_AFrame()
        {
            Save(120, null, null);
            if (WeaponStage.Current != null) Object.Destroy(WeaponStage.Current.gameObject);
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assume.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _menu.Tick(null); // the stage made and built: the model loaded
            WeaponStage stage = WeaponStage.Current;
            Assume.That(stage, Is.Not.Null);
            double built = watch.Elapsed.TotalMilliseconds;
            stage.DrawIfChanged(); // its first draw: the textures and the mesh go to the GPU
            ReadOnePixel(stage.Texture);
            double drawn = watch.Elapsed.TotalMilliseconds;
            float longest = 0f;
            for (int i = 0; i < 10; i++)
            {
                float before = Time.realtimeSinceStartup;
                yield return null;
                longest = Mathf.Max(longest, Time.realtimeSinceStartup - before);
            }
            var draws = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                stage.Turn.Yaw += 1f;
                var w = System.Diagnostics.Stopwatch.StartNew();
                stage.DrawIfChanged();
                ReadOnePixel(stage.Texture);
                draws.Add(w.Elapsed.TotalMilliseconds);
            }
            draws.Sort();
            var frames = new List<double>();
            for (int i = 0; i < 40; i++)
            {
                var w = System.Diagnostics.Stopwatch.StartNew();
                _menu.Tick(null);
                _hud.Tick();
                _menu.LayOutForTests();
                frames.Add(w.Elapsed.TotalMilliseconds);
            }
            frames.Sort();
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} COST first opening of WEAPON, nothing loaded: the model loaded and the stage built {1:0} ms, with its first draw on the GPU {2:0} ms; the longest of the next ten frames {3:0} ms. One draw of the stage ({4}x{5}, {6} samples) with the GPU waited for: median {7:0.00} ms, slowest {8:0.00}. A menu frame with the tab still (CPU): median {9:0.000} ms. Model from {10}, {11} triangles. Editor, batch mode, no window.",
                                   DateTime.Now, built, drawn, longest * 1000f, stage.Texture.width, stage.Texture.height, stage.Texture.antiAliasing, draws[draws.Count / 2], draws[draws.Count - 1], frames[frames.Count / 2], stage.ModelSource, Triangles(stage)));
            // What the model holds in memory once loaded (the engine's own count: the mesh, and each texture of its material).
            var memory = new List<string>();
            long all = 0;
            foreach (Mesh m in stage.ModelMeshes)
            {
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m);
                all += bytes;
                memory.Add(string.Format(Inv, "mesh {0} ({1} vertices) {2:0.0} MB", m.name, m.vertexCount, bytes / 1048576.0));
            }
            foreach (Renderer r in stage.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial == null || !stage.ModelMeshes.Contains(r.GetComponent<MeshFilter>().sharedMesh)) continue;
                foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                {
                    Texture tex = r.sharedMaterial.HasProperty(slot) ? r.sharedMaterial.GetTexture(slot) : null;
                    if (tex == null) continue;
                    long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    all += bytes;
                    memory.Add(string.Format(Inv, "{0} {1}x{2} {3} {4:0.0} MB", slot, tex.width, tex.height, tex.graphicsFormat, bytes / 1048576.0));
                }
                break;
            }
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} COST memory of the model (Profiler.GetRuntimeMemorySizeLong, Editor): {1}; in all {2:0.0} MB", DateTime.Now, string.Join("; ", memory), all / 1048576.0));
            // Whole frames (the game's camera, the menu and, on WEAPON, the stage drawn again each frame while it turns by itself),
            // free running without a window: WEAPON turning, then RANKS and RACK standing.
            stage.Turn.Idle = WeaponStageValues.AutoTurnAfter + WeaponStageValues.AutoTurnEaseIn + 1f;
            double weaponFrame = 0, ranksFrame = 0, rackFrame = 0;
            int drawsBefore = stage.Draws;
            yield return FrameTime(v => weaponFrame = v);
            int drawsTurning = stage.Draws - drawsBefore;
            double stillFrame = 0;
            stage.Turn.Reset(); // still: it turns by itself again only after four seconds
            int drawsBeforeStill = stage.Draws;
            yield return FrameTime(v => stillFrame = v);
            int drawsStill = stage.Draws - drawsBeforeStill;
            Assume.That(_menu.OpenPage(UpgradesPage.Ranks), Is.True);
            yield return FrameTime(v => ranksFrame = v);
            Assume.That(_menu.OpenPage(UpgradesPage.Rack), Is.True);
            yield return FrameTime(v => rackFrame = v);
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} COST whole frames, median of 90 (Editor, batch mode, no window, free running): WEAPON turning by itself {1:0.00} ms ({2} stage draws in 110 frames), WEAPON standing still {5:0.00} ms ({6} draws), RANKS {3:0.00} ms, RACK {4:0.00} ms.", DateTime.Now, weaponFrame, drawsTurning, ranksFrame, rackFrame, stillFrame, drawsStill));
            Assert.That(drawn, Is.GreaterThan(0.0));
            yield return Close();
        }

        /// <summary>The median whole frame (ms) of 90, after 20 to settle.</summary>
        static IEnumerator FrameTime(Action<double> into)
        {
            for (int i = 0; i < 20; i++) yield return null;
            var times = new List<double>();
            for (int i = 0; i < 90; i++)
            {
                float before = Time.realtimeSinceStartup;
                yield return null;
                times.Add((Time.realtimeSinceStartup - before) * 1000.0);
            }
            times.Sort();
            into(times[times.Count / 2]);
        }

        static long Triangles(WeaponStage stage)
        {
            long n = 0;
            foreach (Mesh m in stage.ModelMeshes)
                for (int s = 0; s < m.subMeshCount; s++) n += (long)m.GetIndexCount(s) / 3;
            return n;
        }

        static void ReadOnePixel(RenderTexture rt)
        {
            if (rt == null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            Object.Destroy(tex);
        }

        // ---------------- The model's shape, for the mount points ----------------

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
